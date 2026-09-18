using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace ITBees.Products.Services.Labels.Pdf;

/// <summary>
/// Minimal PDF writer - just enough for warehouse labels: pages of an exact physical size,
/// filled rectangles (QR modules), vector paths (the company pictogram) and text set in the
/// standard Helvetica faces. Those fonts are built into every PDF viewer and printer driver, so
/// nothing is embedded and no font or native library has to exist on the server (the API runs in
/// a bare Linux container). Text is limited to printable ASCII plus the Polish letters - see
/// <see cref="PdfText"/>. All coordinates are PDF points, origin bottom-left.
/// </summary>
internal sealed class SimplePdfDocument
{
    public const double PointsPerMillimeter = 72.0 / 25.4;

    // Fixed object numbers; pages and their content streams follow.
    private const int CatalogObject = 1;
    private const int PagesObject = 2;
    private const int FontRegularObject = 3;
    private const int FontBoldObject = 4;
    private const int InfoObject = 5;
    private const int EncodingObject = 6;
    private const int FirstPageObject = 7;

    private readonly List<PdfPage> _pages = new();

    public PdfPage AddPage(double widthPoints, double heightPoints)
    {
        var page = new PdfPage(widthPoints, heightPoints);
        _pages.Add(page);
        return page;
    }

    public byte[] Save(string title)
    {
        using var output = new MemoryStream();
        var objectOffsets = new List<long>();

        void Write(string text) => output.Write(Encoding.ASCII.GetBytes(text));

        void BeginObject(int expectedNumber)
        {
            objectOffsets.Add(output.Position);
            if (objectOffsets.Count != expectedNumber)
            {
                throw new InvalidOperationException("PDF objects must be written in numbering order.");
            }

            Write($"{expectedNumber} 0 obj\n");
        }

        Write("%PDF-1.6\n");
        // Binary marker - tells transfer tools the file is not plain text.
        output.Write(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' });

        BeginObject(CatalogObject);
        // PrintScaling None: the print dialog starts at "actual size", which is what a label
        // printer needs - any "fit to page" scaling would break the 1:1 label dimensions.
        Write($"<< /Type /Catalog /Pages {PagesObject} 0 R /ViewerPreferences << /PrintScaling /None >> >>\nendobj\n");

        BeginObject(PagesObject);
        var kids = string.Join(" ", _pages.Select((_, i) => $"{PageObjectNumber(i)} 0 R"));
        Write($"<< /Type /Pages /Kids [{kids}] /Count {_pages.Count} >>\nendobj\n");

        BeginObject(FontRegularObject);
        Write($"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding {EncodingObject} 0 R >>\nendobj\n");

        BeginObject(FontBoldObject);
        Write($"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding {EncodingObject} 0 R >>\nendobj\n");

        BeginObject(InfoObject);
        Write($"<< /Title ({PdfText.EscapeAscii(title)}) /Producer (ITBees.Products) " +
              $"/CreationDate (D:{DateTime.UtcNow:yyyyMMddHHmmss}Z) >>\nendobj\n");

        BeginObject(EncodingObject);
        Write($"{PdfText.EncodingDictionary}\nendobj\n");

        for (var i = 0; i < _pages.Count; i++)
        {
            var page = _pages[i];

            BeginObject(PageObjectNumber(i));
            Write($"<< /Type /Page /Parent {PagesObject} 0 R " +
                  $"/MediaBox [0 0 {PdfText.Number(page.Width)} {PdfText.Number(page.Height)}] " +
                  $"/Resources << /Font << /{PdfPage.RegularFontName} {FontRegularObject} 0 R " +
                  $"/{PdfPage.BoldFontName} {FontBoldObject} 0 R >> >> " +
                  $"/Contents {ContentObjectNumber(i)} 0 R >>\nendobj\n");

            var content = Deflate(Encoding.ASCII.GetBytes(page.GetContent()));
            BeginObject(ContentObjectNumber(i));
            Write($"<< /Length {content.Length} /Filter /FlateDecode >>\nstream\n");
            output.Write(content);
            Write("\nendstream\nendobj\n");
        }

        var xrefOffset = output.Position;
        Write($"xref\n0 {objectOffsets.Count + 1}\n");
        // Every cross-reference entry is exactly 20 bytes long, line ending included.
        Write("0000000000 65535 f \n");
        foreach (var offset in objectOffsets)
        {
            Write($"{offset.ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n");
        }

        Write($"trailer\n<< /Size {objectOffsets.Count + 1} /Root {CatalogObject} 0 R /Info {InfoObject} 0 R >>\n");
        Write($"startxref\n{xrefOffset}\n%%EOF\n");

        return output.ToArray();
    }

    private static int PageObjectNumber(int pageIndex) => FirstPageObject + pageIndex * 2;

    private static int ContentObjectNumber(int pageIndex) => FirstPageObject + pageIndex * 2 + 1;

    private static byte[] Deflate(byte[] data)
    {
        using var compressed = new MemoryStream();
        // FlateDecode expects the zlib wrapper (RFC 1950), which is what ZLibStream produces.
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return compressed.ToArray();
    }
}

internal enum PdfFont
{
    Helvetica,
    HelveticaBold
}

/// <summary>The two inks of a monochrome label.</summary>
internal enum PdfPaint
{
    Black,
    White
}

internal enum PdfLineCap
{
    Butt = 0,
    Round = 1,
    Square = 2
}

internal enum PdfLineJoin
{
    Miter = 0,
    Round = 1,
    Bevel = 2
}

internal enum PdfPathSegmentType
{
    MoveTo,
    LineTo,
    CurveTo,
    Close
}

/// <summary>
/// One step of a vector path in PDF points: MoveTo / LineTo use <see cref="X3"/>, <see cref="Y3"/>
/// (the end point); CurveTo is a cubic Bezier with two control points; Close has no points.
/// </summary>
internal readonly record struct PdfPathSegment(
    PdfPathSegmentType Type,
    double X1 = 0, double Y1 = 0,
    double X2 = 0, double Y2 = 0,
    double X3 = 0, double Y3 = 0);

/// <summary>How a path is painted: fill and/or stroke, each optional.</summary>
internal sealed class PdfPathStyle
{
    public PdfPaint? Fill { get; init; }

    /// <summary>Even-odd fill rule instead of nonzero winding - SVG "fill-rule: evenodd".</summary>
    public bool EvenOdd { get; init; }

    public PdfPaint? Stroke { get; init; }
    public double StrokeWidth { get; init; } = 1;
    public PdfLineCap LineCap { get; init; } = PdfLineCap.Butt;
    public PdfLineJoin LineJoin { get; init; } = PdfLineJoin.Miter;
    public double MiterLimit { get; init; } = 4;
}

internal sealed class PdfPage
{
    public const string RegularFontName = "F1";
    public const string BoldFontName = "F2";

    private readonly StringBuilder _content = new("0 g\n");

    public PdfPage(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public double Width { get; }
    public double Height { get; }

    /// <summary>Fills all rectangles (x, y = bottom-left corner) black, as a single path.</summary>
    public void FillRectangles(IEnumerable<(double X, double Y, double Width, double Height)> rectangles)
    {
        var any = false;
        foreach (var r in rectangles)
        {
            _content.Append(PdfText.Number(r.X)).Append(' ')
                .Append(PdfText.Number(r.Y)).Append(' ')
                .Append(PdfText.Number(r.Width)).Append(' ')
                .Append(PdfText.Number(r.Height)).Append(" re\n");
            any = true;
        }

        if (any)
        {
            // Nonzero winding fill - rectangles may overlap each other safely.
            _content.Append("f\n");
        }
    }

    /// <summary>
    /// Paints one path. The graphics state is saved and restored around it, so the colors and
    /// line settings of the path never leak into what is drawn next.
    /// </summary>
    public void DrawPath(IReadOnlyCollection<PdfPathSegment> segments, PdfPathStyle style)
    {
        var fill = style.Fill;
        var stroke = style.Stroke is not null && style.StrokeWidth > 0 ? style.Stroke : null;
        if (segments.Count == 0 || (fill is null && stroke is null))
        {
            return;
        }

        _content.Append("q\n");
        if (fill is not null)
        {
            _content.Append(fill == PdfPaint.White ? "1 g\n" : "0 g\n");
        }

        if (stroke is not null)
        {
            _content.Append(stroke == PdfPaint.White ? "1 G\n" : "0 G\n")
                .Append(PdfText.Number(style.StrokeWidth)).Append(" w ")
                .Append((int)style.LineCap).Append(" J ")
                .Append((int)style.LineJoin).Append(" j ")
                .Append(PdfText.Number(Math.Max(1, style.MiterLimit))).Append(" M\n");
        }

        foreach (var s in segments)
        {
            switch (s.Type)
            {
                case PdfPathSegmentType.MoveTo:
                    AppendPoint(s.X3, s.Y3).Append("m\n");
                    break;
                case PdfPathSegmentType.LineTo:
                    AppendPoint(s.X3, s.Y3).Append("l\n");
                    break;
                case PdfPathSegmentType.CurveTo:
                    AppendPoint(s.X1, s.Y1);
                    AppendPoint(s.X2, s.Y2);
                    AppendPoint(s.X3, s.Y3).Append("c\n");
                    break;
                case PdfPathSegmentType.Close:
                    _content.Append("h\n");
                    break;
            }
        }

        var paintOperator = (fill, stroke, style.EvenOdd) switch
        {
            (not null, not null, true) => "B*",
            (not null, not null, false) => "B",
            (not null, null, true) => "f*",
            (not null, null, false) => "f",
            _ => "S"
        };
        _content.Append(paintOperator).Append("\nQ\n");
    }

    /// <summary>
    /// Saves the graphics state and limits painting to the rectangle (x, y = bottom-left corner)
    /// until the matching <see cref="PopClip"/>.
    /// </summary>
    public void PushClipRectangle(double x, double y, double width, double height)
    {
        _content.Append("q\n");
        AppendPoint(x, y);
        AppendPoint(width, height).Append("re W n\n");
    }

    /// <summary>Restores the graphics state saved by <see cref="PushClipRectangle"/>.</summary>
    public void PopClip()
    {
        _content.Append("Q\n");
    }

    public void DrawText(PdfFont font, double size, double x, double baselineY, string text)
    {
        _content.Append("BT\n/")
            .Append(font == PdfFont.HelveticaBold ? BoldFontName : RegularFontName).Append(' ')
            .Append(PdfText.Number(size)).Append(" Tf\n")
            .Append(PdfText.Number(x)).Append(' ').Append(PdfText.Number(baselineY)).Append(" Td\n(")
            .Append(PdfText.Escape(text)).Append(") Tj\nET\n");
    }

    public string GetContent() => _content.ToString();

    private StringBuilder AppendPoint(double x, double y)
    {
        return _content.Append(PdfText.Number(x)).Append(' ').Append(PdfText.Number(y)).Append(' ');
    }
}

/// <summary>
/// Text encoding of the label fonts: WinAnsiEncoding (which already has Ó and ó) with the other
/// Polish letters placed on the otherwise unused codes 128-143 through a /Differences array. The
/// standard Helvetica faces carry glyphs under exactly these names, so no font is embedded.
/// Anything else outside printable ASCII becomes '?'.
/// </summary>
internal static class PdfText
{
    private const int FirstPolishCode = 128;

    /// <summary>Letter, its glyph name in the standard fonts and the letter whose width it shares.</summary>
    private static readonly (char Letter, string GlyphName, char WidthOf)[] PolishLetters =
    {
        ('Ą', "Aogonek", 'A'), ('ą', "aogonek", 'a'),
        ('Ć', "Cacute", 'C'), ('ć', "cacute", 'c'),
        ('Ę', "Eogonek", 'E'), ('ę', "eogonek", 'e'),
        ('Ł', "Lslash", 'L'), ('ł', "lslash", 'l'),
        ('Ń', "Nacute", 'N'), ('ń', "nacute", 'n'),
        ('Ś', "Sacute", 'S'), ('ś', "sacute", 's'),
        ('Ź', "Zacute", 'Z'), ('ź', "zacute", 'z'),
        ('Ż', "Zdotaccent", 'Z'), ('ż', "zdotaccent", 'z')
    };

    // WinAnsiEncoding codes of the two Polish letters it has natively.
    private const byte WinAnsiOacuteUpper = 0xD3;
    private const byte WinAnsiOacuteLower = 0xF3;

    public static readonly string EncodingDictionary =
        "<< /Type /Encoding /BaseEncoding /WinAnsiEncoding /Differences [" + FirstPolishCode + " " +
        string.Join(" ", PolishLetters.Select(x => "/" + x.GlyphName)) + "] >>";

    /// <summary>Invariant number formatting - a decimal comma would corrupt the file.</summary>
    public static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Font encoding codes of the text; characters the label fonts cannot show become '?'. Line
    /// breaks and other control characters count as unsupported too.
    /// </summary>
    public static byte[] Encode(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<byte>();
        }

        var codes = new byte[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            codes[i] = EncodeCharacter(text[i]);
        }

        return codes;
    }

    /// <summary>
    /// ASCII character whose advance width equals the width of the glyph behind the code - the
    /// Polish letters are exactly as wide as their base letters in the standard Helvetica faces.
    /// </summary>
    public static char WidthCharacter(byte code)
    {
        if (code is >= (byte)' ' and <= (byte)'~')
        {
            return (char)code;
        }

        if (code >= FirstPolishCode && code < FirstPolishCode + PolishLetters.Length)
        {
            return PolishLetters[code - FirstPolishCode].WidthOf;
        }

        return code switch
        {
            WinAnsiOacuteUpper => 'O',
            WinAnsiOacuteLower => 'o',
            _ => '?'
        };
    }

    /// <summary>Encodes and escapes text for use inside a PDF literal string "( )" shown in a label font.</summary>
    public static string Escape(string? text)
    {
        var escaped = new StringBuilder();
        foreach (var code in Encode(text))
        {
            switch (code)
            {
                case (byte)'\\':
                case (byte)'(':
                case (byte)')':
                    escaped.Append('\\').Append((char)code);
                    break;
                case > (byte)'~':
                    // Octal escape keeps the content stream plain ASCII.
                    escaped.Append('\\').Append(Convert.ToString(code, 8).PadLeft(3, '0'));
                    break;
                default:
                    escaped.Append((char)code);
                    break;
            }
        }

        return escaped.ToString();
    }

    /// <summary>
    /// Printable ASCII only, escaped for a PDF literal string - for document metadata, which does
    /// not use the label font encoding.
    /// </summary>
    public static string EscapeAscii(string? text)
    {
        var sanitized = new StringBuilder();
        foreach (var c in text ?? string.Empty)
        {
            sanitized.Append(c is >= ' ' and <= '~' ? c : '?');
        }

        return sanitized.ToString().Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }

    private static byte EncodeCharacter(char c)
    {
        if (c is >= ' ' and <= '~')
        {
            return (byte)c;
        }

        switch (c)
        {
            case 'Ó':
                return WinAnsiOacuteUpper;
            case 'ó':
                return WinAnsiOacuteLower;
        }

        for (var i = 0; i < PolishLetters.Length; i++)
        {
            if (PolishLetters[i].Letter == c)
            {
                return (byte)(FirstPolishCode + i);
            }
        }

        return (byte)'?';
    }
}
