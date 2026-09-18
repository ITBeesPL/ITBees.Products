using System.Reflection;
using ITBees.Products.Services.Labels.Pdf;
using ITBees.Products.Services.Labels.Svg;

namespace ITBees.Products.Services.Labels;

/// <summary>
/// Small monochrome pictogram - e.g. the company logo - printed on warehouse labels next to the
/// company name. Parsed once from a simple SVG drawing and drawn as vector paths, so it prints
/// sharp at any label printer resolution. See <see cref="FromSvg"/> for what the SVG may contain.
/// </summary>
public sealed class StockLabelLogo
{
    internal StockLabelLogo(double width, double height, IReadOnlyList<LogoShape> shapes)
    {
        Width = width;
        Height = height;
        Shapes = shapes;
    }

    /// <summary>Width of the drawing in SVG user units (the viewBox width).</summary>
    public double Width { get; }

    /// <summary>Height of the drawing in SVG user units (the viewBox height).</summary>
    public double Height { get; }

    /// <summary>Shapes in painting order, in logo coordinates: origin top-left, y pointing down.</summary>
    internal IReadOnlyList<LogoShape> Shapes { get; }

    /// <summary>
    /// Parses a pictogram from SVG markup. Supported: the elements path, rect, circle, ellipse,
    /// line, polyline, polygon and g; the presentation attributes (also inside a style attribute)
    /// fill, fill-rule, stroke, stroke-width, stroke-linecap, stroke-linejoin, stroke-miterlimit,
    /// opacity, fill-opacity, stroke-opacity, color, display, visibility and transform. Labels
    /// print in black only, so every color is printed either black or - when light - white, never
    /// gray; a paint less than half opaque is not printed. Title, desc, metadata, defs and elements
    /// of other XML namespaces (editor data) are skipped. Everything else - text, images, use,
    /// gradients, CSS classes, clip paths, masks, filters, dashes - is rejected with a
    /// <see cref="FormatException"/>, so a pictogram never prints differently from how it looks
    /// in a browser.
    /// </summary>
    /// <exception cref="FormatException">The markup is not a supported SVG drawing.</exception>
    public static StockLabelLogo FromSvg(string svg) => SvgPictogramParser.Parse(svg);

    /// <summary>Reads the SVG drawing from a file - see <see cref="FromSvg"/>.</summary>
    public static StockLabelLogo FromSvgFile(string path) => FromSvg(File.ReadAllText(path));

    /// <summary>
    /// Reads the SVG drawing from a resource embedded in <paramref name="assembly"/> - the
    /// simplest way to ship the pictogram with an application. See <see cref="FromSvg"/>.
    /// </summary>
    /// <param name="assembly">Assembly the SVG file is embedded in (EmbeddedResource).</param>
    /// <param name="resourceName">Full manifest name, e.g. "MyApp.Assets.label-logo.svg".</param>
    public static StockLabelLogo FromEmbeddedResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new FileNotFoundException(
                               $"Embedded resource '{resourceName}' not found in {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return FromSvg(reader.ReadToEnd());
    }

    /// <summary>
    /// Draws the pictogram scaled to fit the box and centered in it, keeping its proportions.
    /// Like a browser, it clips to the drawing's own viewBox.
    /// </summary>
    /// <param name="page">Page to draw on.</param>
    /// <param name="left">Left edge of the box, in points.</param>
    /// <param name="top">Top edge of the box, in PDF points (origin bottom-left).</param>
    /// <param name="width">Box width, in points.</param>
    /// <param name="height">Box height, in points.</param>
    internal void Draw(PdfPage page, double left, double top, double width, double height)
    {
        var scale = Math.Min(width / Width, height / Height);
        var originX = left + (width - Width * scale) / 2;
        var originY = top - (height - Height * scale) / 2;

        page.PushClipRectangle(originX, originY - Height * scale, Width * scale, Height * scale);
        foreach (var shape in Shapes)
        {
            var segments = shape.Segments
                .Select(s => s with
                {
                    X1 = originX + s.X1 * scale, Y1 = originY - s.Y1 * scale,
                    X2 = originX + s.X2 * scale, Y2 = originY - s.Y2 * scale,
                    X3 = originX + s.X3 * scale, Y3 = originY - s.Y3 * scale
                })
                .ToList();

            page.DrawPath(segments, new PdfPathStyle
            {
                Fill = shape.Style.Fill,
                EvenOdd = shape.Style.EvenOdd,
                Stroke = shape.Style.Stroke,
                StrokeWidth = shape.Style.StrokeWidth * scale,
                LineCap = shape.Style.LineCap,
                LineJoin = shape.Style.LineJoin,
                MiterLimit = shape.Style.MiterLimit
            });
        }

        page.PopClip();
    }
}

/// <summary>One painted path of a pictogram, in logo coordinates (origin top-left, y down).</summary>
internal sealed class LogoShape
{
    public LogoShape(IReadOnlyList<PdfPathSegment> segments, PdfPathStyle style)
    {
        Segments = segments;
        Style = style;
    }

    public IReadOnlyList<PdfPathSegment> Segments { get; }

    /// <summary>Paint of the path; the stroke width is in logo units.</summary>
    public PdfPathStyle Style { get; }
}
