using System.Globalization;
using ITBees.Products.Services.Labels.Pdf;

namespace ITBees.Products.Services.Labels;

/// <summary>
/// Warehouse label, 50 x 30 mm: a QR code on the left and, next to it, the last characters of
/// our internal serial number, the last characters of the manufacturer serial number and the
/// purchase date. One label per PDF page, the page being exactly the label - no margins.
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

    private const double CaptionSize = 5;
    private const double CaptionToValueGap = 1.5;
    private const double LineGap = 4.5;

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

        var textLeftMm = qrLeftMm + qrSideMm + ColumnGapMm;
        var textWidth = Mm(LabelWidthMm - MarginMm - textLeftMm);
        DrawTextColumn(page, Mm(textLeftMm), textWidth, BuildLines(label));
    }

    private static List<LabelLine> BuildLines(StockLabelData label)
    {
        var serialSuffix = StockLabelFormat.SerialNumberSuffix(label.SerialNumber);
        var serialIsCut = (label.SerialNumber ?? string.Empty).Trim().Length > serialSuffix.Length;

        var lines = new List<LabelLine>
        {
            new("ID", StockLabelFormat.InternalCode(label.DeviceGuid), 16),
            // The dots tell the reader these are only the last characters of the number.
            new("S/N", (serialIsCut ? "..." : string.Empty) + serialSuffix, 13)
        };

        if (label.PurchaseDate.HasValue)
        {
            lines.Add(new LabelLine("DATA ZAKUPU",
                label.PurchaseDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 10));
        }

        return lines;
    }

    /// <summary>Caption + value pairs stacked top-down, the whole block centered vertically.</summary>
    private static void DrawTextColumn(PdfPage page, double left, double width, List<LabelLine> lines)
    {
        foreach (var line in lines)
        {
            line.ValueSize = PdfFontMetrics.FitSize(PdfFont.HelveticaBold, line.MaxValueSize, width, line.Value);
        }

        // Captions, digits and capitals have no descenders, so a line is as tall as its capitals.
        var blockHeight = lines.Sum(x => CaptionSize * PdfFontMetrics.CapHeight + CaptionToValueGap +
                                         x.ValueSize * PdfFontMetrics.CapHeight)
                          + LineGap * (lines.Count - 1);

        var fromTop = (page.Height - blockHeight) / 2;
        foreach (var line in lines)
        {
            fromTop += CaptionSize * PdfFontMetrics.CapHeight;
            page.DrawText(PdfFont.Helvetica, CaptionSize, left, page.Height - fromTop, line.Caption);

            fromTop += CaptionToValueGap + line.ValueSize * PdfFontMetrics.CapHeight;
            page.DrawText(PdfFont.HelveticaBold, line.ValueSize, left, page.Height - fromTop, line.Value);

            fromTop += LineGap;
        }
    }

    private static double Mm(double millimeters) => millimeters * SimplePdfDocument.PointsPerMillimeter;

    private sealed class LabelLine
    {
        public LabelLine(string caption, string value, double maxValueSize)
        {
            Caption = caption;
            Value = value;
            MaxValueSize = maxValueSize;
            ValueSize = maxValueSize;
        }

        public string Caption { get; }
        public string Value { get; }
        public double MaxValueSize { get; }
        public double ValueSize { get; set; }
    }
}
