using System.Globalization;
using ITBees.Products.Services.Labels.Pdf;

namespace ITBees.Products.Services.Labels;

/// <summary>
/// Warehouse label, 50 x 30 mm: a QR code on the left and, next to it, a column with the
/// company pictogram and name at the top, then - without captions - the last characters of our
/// internal serial number, the last characters of the manufacturer serial number and, when the
/// label data carries one, the purchase date. One label per PDF page, the page being exactly the
/// label - no margins.
/// </summary>
public class StockLabelPdfGenerator : IStockLabelPdfGenerator
{
    public const double LabelWidthMm = 50;
    public const double LabelHeightMm = 30;

    private const double MarginMm = 1.5;

    // The QR quiet zone already separates the code from the text, so the gap stays small.
    private const double ColumnGapMm = 1.0;

    // Keeps at least ~19 mm of the label width for the text column.
    private const double MaxQrSideMm = 26;
    private const double MaxModuleMm = 0.75;

    // Two modules of explicit quiet zone; the blank label margin around provides the rest.
    private const int QuietZoneModules = 2;

    // 203 dpi (8 dots/mm) is the usual label printer resolution. A module made of whole
    // printer dots prints with crisp edges.
    private const double PrinterDotMm = 0.125;

    // Abutting rectangles can show hairline seams on screen - a tiny overlap hides them and
    // is far below anything a printer can resolve.
    private const double SeamOverlapPoints = 0.03;

    // Company header: pictogram square plus the name in up to two lines beside it.
    private const double LogoSizeMm = 6;
    private const double MinLogoSizeMm = 4.5;
    private const double LogoToNameGapMm = 1.0;
    private const double MinNameWidthMm = 10;
    private const double CompanyNameMaxSize = 7;
    private const double CompanyNameLineAdvance = 1.2;
    private const double ThirdNameLineMinGain = 1.15;
    private const double HeaderToCodesMinGapMm = 1.5;

    // Codes under the header, largest first. The internal code is what people read out.
    private const double InternalCodeMaxSize = 13;
    private const double SerialNumberMaxSize = 10;
    private const double PurchaseDateMaxSize = 7;
    private const double CodeLineGap = 3.2;

    public byte[] Generate(IReadOnlyCollection<StockLabelData> labels)
    {
        if (labels == null || labels.Count == 0)
        {
            throw new ArgumentException("At least one label is required.", nameof(labels));
        }

        var document = new SimplePdfDocument();
        foreach (var label in labels)
        {
            DrawLabel(document.AddPage(Mm(LabelWidthMm), Mm(LabelHeightMm)), label);
        }

        return document.Save($"Warehouse labels ({labels.Count})");
    }

    private static void DrawLabel(PdfPage page, StockLabelData label)
    {
        var qr = QrCodeMatrix.Encode(label.QrContent);
        var totalModules = qr.Size + 2 * QuietZoneModules;

        var moduleMm = Math.Min(MaxModuleMm, MaxQrSideMm / totalModules);
        var wholeDotsModuleMm = Math.Floor(moduleMm / PrinterDotMm) * PrinterDotMm;
        if (wholeDotsModuleMm >= 2 * PrinterDotMm)
        {
            moduleMm = wholeDotsModuleMm;
        }

        var qrSideMm = totalModules * moduleMm;
        var qrLeftMm = MarginMm;
        var qrTopMm = (LabelHeightMm - qrSideMm) / 2;

        page.FillRectangles(qr.GetDarkRuns().Select(run =>
        {
            var leftMm = qrLeftMm + (QuietZoneModules + run.Column) * moduleMm;
            var bottomMm = qrTopMm + (QuietZoneModules + run.Row + 1) * moduleMm;
            return (
                Mm(leftMm),
                page.Height - Mm(bottomMm) - SeamOverlapPoints,
                Mm(run.Length * moduleMm) + SeamOverlapPoints,
                Mm(moduleMm) + SeamOverlapPoints);
        }));

        var column = new TextColumn(
            LeftMm: qrLeftMm + qrSideMm + ColumnGapMm,
            WidthMm: LabelWidthMm - MarginMm - (qrLeftMm + qrSideMm + ColumnGapMm),
            // The column lines up with the printed part of the QR code (inside its quiet zone),
            // never reaching into the label margins.
            TopMm: Math.Max(MarginMm, qrTopMm + QuietZoneModules * moduleMm),
            BottomMm: Math.Min(LabelHeightMm - MarginMm, qrTopMm + qrSideMm - QuietZoneModules * moduleMm));

        DrawTextColumn(page, column, label);
    }

    private static void DrawTextColumn(PdfPage page, TextColumn column, StockLabelData label)
    {
        var header = CompanyHeader.Build(column.WidthMm, label.CompanyName, label.Logo);
        var codes = BuildCodeLines(label, Mm(column.WidthMm));
        var codesHeightMm = codes.Sum(x => x.Size * PdfFontMetrics.CapHeight) / SimplePdfDocument.PointsPerMillimeter
                            + CodeLineGap * (codes.Count - 1) / SimplePdfDocument.PointsPerMillimeter;
        var headerHeightMm = header?.HeightMm + HeaderToCodesMinGapMm ?? 0;

        // A tall text block (e.g. with the purchase date next to a small QR code) may use the
        // whole label height instead.
        if (headerHeightMm + codesHeightMm > column.BottomMm - column.TopMm)
        {
            column = column with { TopMm = MarginMm, BottomMm = LabelHeightMm - MarginMm };
        }

        var codesTopMm = column.TopMm;
        if (header != null)
        {
            header.Draw(page, column.LeftMm, column.TopMm);
            codesTopMm += headerHeightMm;
        }

        // The codes sit in the middle of the space left under the header.
        var fromTop = Mm(codesTopMm + Math.Max(0, (column.BottomMm - codesTopMm - codesHeightMm) / 2));
        foreach (var line in codes)
        {
            fromTop += line.Size * PdfFontMetrics.CapHeight;
            page.DrawText(line.Font, line.Size, Mm(column.LeftMm), page.Height - fromTop, line.Text);
            fromTop += CodeLineGap;
        }
    }

    /// <summary>
    /// The codes, each at the largest size up to its maximum that fits the column. No captions:
    /// the internal code is the large bold one, the manufacturer serial number follows with
    /// leading dots, the date is a plain date.
    /// </summary>
    private static List<CodeLine> BuildCodeLines(StockLabelData label, double width)
    {
        var serialSuffix = StockLabelFormat.SerialNumberSuffix(label.SerialNumber);
        var serialIsCut = (label.SerialNumber ?? string.Empty).Trim().Length > serialSuffix.Length;

        var lines = new List<CodeLine>
        {
            new(PdfFont.HelveticaBold, StockLabelFormat.InternalCode(label.DeviceGuid), InternalCodeMaxSize),
            // The dots tell the reader these are only the last characters of the number.
            new(PdfFont.HelveticaBold, (serialIsCut ? "..." : string.Empty) + serialSuffix, SerialNumberMaxSize)
        };

        if (label.PurchaseDate.HasValue)
        {
            lines.Add(new CodeLine(PdfFont.Helvetica,
                label.PurchaseDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), PurchaseDateMaxSize));
        }

        return lines
            .Where(x => x.Text.Length > 0)
            .Select(x => x with { Size = PdfFontMetrics.FitSize(x.Font, x.Size, width, x.Text) })
            .ToList();
    }

    private static double Mm(double millimeters) => millimeters * SimplePdfDocument.PointsPerMillimeter;

    private readonly record struct TextColumn(double LeftMm, double WidthMm, double TopMm, double BottomMm);

    private readonly record struct CodeLine(PdfFont Font, string Text, double Size);

    /// <summary>Pictogram and company name at the top of the text column.</summary>
    private sealed class CompanyHeader
    {
        private readonly StockLabelLogo? _logo;
        private readonly double _logoSizeMm;
        private readonly List<(PdfFont Font, string Text)> _nameLines;
        private readonly double _nameSize;

        private CompanyHeader(StockLabelLogo? logo, double logoSizeMm, List<(PdfFont, string)> nameLines, double nameSize)
        {
            _logo = logo;
            _logoSizeMm = logoSizeMm;
            _nameLines = nameLines;
            _nameSize = nameSize;
        }

        /// <summary>From the top of the pictogram / first capital letter to the lowest descender.</summary>
        public double HeightMm => Math.Max(_logo != null ? _logoSizeMm : 0, NameBlockHeight / SimplePdfDocument.PointsPerMillimeter);

        /// <summary>Cap height of the first line down to the descenders of the last one, in points.</summary>
        private double NameBlockHeight => _nameLines.Count == 0
            ? 0
            : _nameSize * (PdfFontMetrics.CapHeight + CompanyNameLineAdvance * (_nameLines.Count - 1) + PdfFontMetrics.Descent);

        public static CompanyHeader? Build(double columnWidthMm, string? companyName, StockLabelLogo? logo)
        {
            var words = (companyName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 && logo == null)
            {
                return null;
            }

            var logoSizeMm = 0.0;
            var nameWidthMm = columnWidthMm;
            if (logo != null)
            {
                // A narrow column keeps room for the name by shrinking the pictogram first.
                logoSizeMm = words.Length == 0
                    ? Math.Min(LogoSizeMm, columnWidthMm)
                    : Math.Clamp(columnWidthMm - LogoToNameGapMm - MinNameWidthMm, MinLogoSizeMm, LogoSizeMm);
                nameWidthMm = columnWidthMm - logoSizeMm - LogoToNameGapMm;
            }

            // Two lines as a rule; a long name that would have to shrink a lot gets a third one.
            var lines = SplitName(words, Math.Min(2, words.Length));
            var size = FitSize(lines, nameWidthMm);
            if (words.Length >= 3 && size < CompanyNameMaxSize)
            {
                var threeLines = SplitName(words, 3);
                var threeLinesSize = FitSize(threeLines, nameWidthMm);
                if (threeLinesSize > size * ThirdNameLineMinGain)
                {
                    (lines, size) = (threeLines, threeLinesSize);
                }
            }

            return new CompanyHeader(logo, logoSizeMm, lines, size);
        }

        private static double FitSize(List<(PdfFont Font, string Text)> lines, double widthMm)
        {
            return lines
                .Select(x => PdfFontMetrics.FitSize(x.Font, CompanyNameMaxSize, Mm(widthMm), x.Text))
                .DefaultIfEmpty(CompanyNameMaxSize)
                .Min();
        }

        public void Draw(PdfPage page, double leftMm, double topMm)
        {
            var nameLeftMm = leftMm;
            if (_logo != null)
            {
                var top = page.Height - Mm(topMm + (HeightMm - _logoSizeMm) / 2);
                _logo.Draw(page, Mm(leftMm), top, Mm(_logoSizeMm), Mm(_logoSizeMm));
                nameLeftMm += _logoSizeMm + LogoToNameGapMm;
            }

            if (_nameLines.Count == 0)
            {
                return;
            }

            // The name block is centered on the pictogram.
            var fromTop = Mm(topMm) + (Mm(HeightMm) - NameBlockHeight) / 2 + _nameSize * PdfFontMetrics.CapHeight;
            foreach (var (font, text) in _nameLines)
            {
                page.DrawText(font, _nameSize, Mm(nameLeftMm), page.Height - fromTop, text);
                fromTop += _nameSize * CompanyNameLineAdvance;
            }
        }

        /// <summary>
        /// Breaks the name into the given number of lines where the widest line is narrowest -
        /// "Octopark" / "spółka z o.o." - the first line bold, the rest regular.
        /// </summary>
        private static List<(PdfFont Font, string Text)> SplitName(string[] words, int lineCount)
        {
            List<(PdfFont Font, string Text)> Lines(IReadOnlyList<int> breaks)
            {
                var starts = new[] { 0 }.Concat(breaks).ToList();
                var ends = breaks.Append(words.Length).ToList();
                return starts
                    .Select((start, i) => (i == 0 ? PdfFont.HelveticaBold : PdfFont.Helvetica,
                        string.Join(' ', words[start..ends[i]])))
                    .ToList();
            }

            double WidestLine(List<(PdfFont Font, string Text)> lines) =>
                lines.Max(x => PdfFontMetrics.MeasureWidth(x.Font, 1, x.Text));

            if (lineCount <= 1 || words.Length <= 1)
            {
                return Lines(Array.Empty<int>());
            }

            // Names are a few words long - trying every break is cheap.
            var candidates = lineCount == 2
                ? Enumerable.Range(1, words.Length - 1).Select(x => new[] { x })
                : Enumerable.Range(1, words.Length - 1)
                    .SelectMany(first => Enumerable.Range(first + 1, words.Length - first - 1)
                        .Select(second => new[] { first, second }));

            return candidates
                .Select(Lines)
                .OrderBy(WidestLine)
                .First();
        }
    }
}
