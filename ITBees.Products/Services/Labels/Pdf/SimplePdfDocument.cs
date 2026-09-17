using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace ITBees.Products.Services.Labels.Pdf;

/// <summary>
/// Minimal PDF writer - just enough for warehouse labels: pages of an exact physical size,
/// filled rectangles (QR modules) and text set in the standard Helvetica faces. Those fonts are
/// built into every PDF viewer and printer driver, so nothing is embedded and no font or native
/// library has to exist on the server (the API runs in a bare Linux container).
/// Text is limited to printable ASCII. All coordinates are PDF points, origin bottom-left.
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
    private const int FirstPageObject = 6;

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
        Write("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");

        BeginObject(FontBoldObject);
        Write("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

        BeginObject(InfoObject);
        Write($"<< /Title ({PdfText.Escape(title)}) /Producer (ITBees.Products) " +
              $"/CreationDate (D:{DateTime.UtcNow:yyyyMMddHHmmss}Z) >>\nendobj\n");

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

    public void DrawText(PdfFont font, double size, double x, double baselineY, string text)
    {
        _content.Append("BT\n/")
            .Append(font == PdfFont.HelveticaBold ? BoldFontName : RegularFontName).Append(' ')
            .Append(PdfText.Number(size)).Append(" Tf\n")
            .Append(PdfText.Number(x)).Append(' ').Append(PdfText.Number(baselineY)).Append(" Td\n(")
            .Append(PdfText.Escape(text)).Append(") Tj\nET\n");
    }

    public string GetContent() => _content.ToString();
}

internal static class PdfText
{
    /// <summary>Invariant number formatting - a decimal comma would corrupt the file.</summary>
    public static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Printable ASCII only; anything else (the standard fonts cannot show it) becomes '?'.</summary>
    public static string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var sanitized = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sanitized.Append(c is >= ' ' and <= '~' ? c : '?');
        }

        return sanitized.ToString();
    }

    /// <summary>Sanitizes and escapes text for use inside a PDF literal string "( )".</summary>
    public static string Escape(string? text)
    {
        return Sanitize(text).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
