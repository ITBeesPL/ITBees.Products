using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ITBees.Products.Services.Labels.Pdf;

namespace ITBees.Products.Services.Labels.Svg;

/// <summary>
/// Turns a small SVG drawing into label pictogram shapes - the subset described on
/// <see cref="StockLabelLogo.FromSvg"/>. Follows SVG semantics (default black fill, inherited
/// presentation attributes, nested transforms) so the label shows what a browser shows.
/// </summary>
internal static class SvgPictogramParser
{
    private const string SvgNamespace = "http://www.w3.org/2000/svg";

    private static readonly HashSet<string> SkippedElements = new(StringComparer.Ordinal)
    {
        "title", "desc", "metadata", "defs"
    };

    private static readonly HashSet<string> ShapeElements = new(StringComparer.Ordinal)
    {
        "path", "rect", "circle", "ellipse", "line", "polyline", "polygon"
    };

    public static StockLabelLogo Parse(string svg)
    {
        if (string.IsNullOrWhiteSpace(svg))
        {
            throw new FormatException("The label pictogram SVG is empty.");
        }

        XDocument document;
        try
        {
            // DTDs are ignored, never resolved - a DOCTYPE line from a drawing program is harmless.
            using var reader = XmlReader.Create(new StringReader(svg),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
            document = XDocument.Load(reader);
        }
        catch (XmlException e)
        {
            throw new FormatException($"The label pictogram is not valid SVG: {e.Message}", e);
        }

        var root = document.Root;
        if (root == null || root.Name.LocalName != "svg" || !IsSvgElement(root))
        {
            throw new FormatException("The label pictogram must be an <svg> document.");
        }

        var (minX, minY, width, height) = ReadViewBox(root);
        var shapes = new List<LogoShape>();
        VisitChildren(root, PaintState.Initial.Apply(ReadProperties(root)),
            SvgMatrix.Translation(-minX, -minY), shapes);

        if (shapes.Count == 0)
        {
            throw new FormatException("The label pictogram does not draw anything.");
        }

        return new StockLabelLogo(width, height, shapes);
    }

    private static void VisitChildren(XElement parent, PaintState state, SvgMatrix transform, List<LogoShape> shapes)
    {
        foreach (var element in parent.Elements())
        {
            // Editor data (Inkscape, Illustrator, RDF metadata) lives in other namespaces.
            if (!IsSvgElement(element))
            {
                continue;
            }

            var name = element.Name.LocalName;
            if (SkippedElements.Contains(name))
            {
                continue;
            }

            if (name != "g" && !ShapeElements.Contains(name))
            {
                throw new FormatException(
                    $"The label pictogram uses <{name}>, which is not supported - use only paths and basic shapes.");
            }

            var properties = ReadProperties(element);
            if (properties.TryGetValue("display", out var display) && display == "none")
            {
                continue;
            }

            var elementState = state.Apply(properties);
            var elementTransform = properties.TryGetValue("transform", out var transformText)
                ? transform.Multiply(SvgMatrix.Parse(transformText))
                : transform;

            if (name == "g")
            {
                VisitChildren(element, elementState, elementTransform, shapes);
                continue;
            }

            var shape = BuildShape(element, name, elementState, elementTransform);
            if (shape != null)
            {
                shapes.Add(shape);
            }
        }
    }

    private static LogoShape? BuildShape(XElement element, string name, PaintState state, SvgMatrix transform)
    {
        if (!state.Visible)
        {
            return null;
        }

        var path = new SvgPathBuilder(transform);
        switch (name)
        {
            case "path":
                SvgPathData.Parse(Attribute(element, "d") ?? string.Empty, path);
                break;
            case "rect":
                AddRectangle(element, path);
                break;
            case "circle":
            {
                var r = Length(element, "r");
                path.AddEllipse(Length(element, "cx"), Length(element, "cy"), r, r);
                break;
            }
            case "ellipse":
                path.AddEllipse(Length(element, "cx"), Length(element, "cy"), Length(element, "rx"), Length(element, "ry"));
                break;
            case "line":
                path.MoveTo(Length(element, "x1"), Length(element, "y1"));
                path.LineTo(Length(element, "x2"), Length(element, "y2"));
                break;
            case "polyline":
            case "polygon":
                AddPolyline(Attribute(element, "points") ?? string.Empty, name == "polygon", path);
                break;
        }

        if (path.Segments.Count == 0)
        {
            return null;
        }

        // A line has no inside to fill.
        var fill = name == "line" ? null : state.ResolveFill();
        var stroke = state.ResolveStroke();
        if (fill == null && stroke == null)
        {
            return null;
        }

        return new LogoShape(path.Segments, new PdfPathStyle
        {
            Fill = fill,
            EvenOdd = state.EvenOdd,
            Stroke = stroke,
            StrokeWidth = state.StrokeWidth * transform.AverageScale,
            LineCap = state.LineCap,
            LineJoin = state.LineJoin,
            MiterLimit = state.MiterLimit
        });
    }

    private static void AddRectangle(XElement element, SvgPathBuilder path)
    {
        var x = Length(element, "x");
        var y = Length(element, "y");
        var width = Length(element, "width");
        var height = Length(element, "height");
        if (width <= 0 || height <= 0)
        {
            return;
        }

        // A missing corner radius takes the other one; both are capped at half the side.
        var rxText = Attribute(element, "rx");
        var ryText = Attribute(element, "ry");
        var rx = rxText != null ? SvgValues.ParseLength(rxText, "rx") : ryText != null ? SvgValues.ParseLength(ryText, "ry") : 0;
        var ry = ryText != null ? SvgValues.ParseLength(ryText, "ry") : rx;
        path.AddRectangle(x, y, width, height, Math.Clamp(rx, 0, width / 2), Math.Clamp(ry, 0, height / 2));
    }

    private static void AddPolyline(string points, bool close, SvgPathBuilder path)
    {
        var numbers = SvgValues.ParseNumberList(points, "points");
        for (var i = 0; i + 1 < numbers.Count; i += 2)
        {
            if (i == 0)
            {
                path.MoveTo(numbers[i], numbers[i + 1]);
            }
            else
            {
                path.LineTo(numbers[i], numbers[i + 1]);
            }
        }

        if (close && numbers.Count >= 4)
        {
            path.Close();
        }
    }

    private static (double MinX, double MinY, double Width, double Height) ReadViewBox(XElement root)
    {
        var viewBox = Attribute(root, "viewBox");
        if (viewBox != null)
        {
            var values = SvgValues.ParseNumberList(viewBox, "viewBox");
            if (values.Count != 4 || values[2] <= 0 || values[3] <= 0)
            {
                throw new FormatException($"The label pictogram has an invalid viewBox \"{viewBox}\".");
            }

            return (values[0], values[1], values[2], values[3]);
        }

        // Without a viewBox the drawing is in pixels of the given size.
        var width = Attribute(root, "width");
        var height = Attribute(root, "height");
        if (width == null || height == null)
        {
            throw new FormatException("The label pictogram needs a viewBox (or width and height in pixels).");
        }

        var w = SvgValues.ParseLength(width, "width");
        var h = SvgValues.ParseLength(height, "height");
        if (w <= 0 || h <= 0)
        {
            throw new FormatException("The label pictogram has a zero width or height.");
        }

        return (0, 0, w, h);
    }

    /// <summary>
    /// Presentation attributes of the element, overridden by its style attribute as in CSS.
    /// Rejects what would make the label differ from the drawing.
    /// </summary>
    private static Dictionary<string, string> ReadProperties(XElement element)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attribute in element.Attributes())
        {
            if (attribute.Name.NamespaceName.Length == 0 && attribute.Name.LocalName != "style")
            {
                properties[attribute.Name.LocalName] = attribute.Value.Trim();
            }
        }

        var style = Attribute(element, "style");
        if (!string.IsNullOrWhiteSpace(style))
        {
            foreach (var declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var colon = declaration.IndexOf(':');
                if (colon > 0)
                {
                    var value = declaration[(colon + 1)..].Replace("!important", string.Empty).Trim();
                    properties[declaration[..colon].Trim()] = value;
                }
            }
        }

        if (properties.TryGetValue("class", out var cssClass) && cssClass.Length > 0)
        {
            throw new FormatException(
                "The label pictogram uses CSS classes, which are not supported - use presentation attributes (fill, stroke ...).");
        }

        foreach (var unsupported in new[] { "clip-path", "mask", "filter", "stroke-dasharray" })
        {
            if (properties.TryGetValue(unsupported, out var value) && value != "none")
            {
                throw new FormatException($"The label pictogram uses {unsupported}, which is not supported.");
            }
        }

        return properties;
    }

    private static bool IsSvgElement(XElement element)
    {
        var ns = element.Name.NamespaceName;
        return ns.Length == 0 || ns == SvgNamespace;
    }

    private static string? Attribute(XElement element, string name) => element.Attribute(name)?.Value;

    private static double Length(XElement element, string name)
    {
        var value = Attribute(element, name);
        return value == null ? 0 : SvgValues.ParseLength(value, name);
    }

    /// <summary>Inherited painting properties, as in SVG.</summary>
    private sealed record PaintState(
        SvgPaint Fill,
        bool EvenOdd,
        double FillOpacity,
        SvgPaint Stroke,
        double StrokeWidth,
        PdfLineCap LineCap,
        PdfLineJoin LineJoin,
        double MiterLimit,
        double StrokeOpacity,
        double GroupOpacity,
        SvgPaint Color,
        bool Visible)
    {
        public static readonly PaintState Initial = new(
            SvgPaint.Black, false, 1, SvgPaint.None, 1, PdfLineCap.Butt, PdfLineJoin.Miter, 4, 1, 1,
            SvgPaint.Black, true);

        public PaintState Apply(IReadOnlyDictionary<string, string> p)
        {
            var state = this;
            if (p.TryGetValue("fill", out var fill) && fill != "inherit")
            {
                state = state with { Fill = SvgPaint.Parse(fill) };
            }

            if (p.TryGetValue("fill-rule", out var fillRule) && fillRule != "inherit")
            {
                state = state with { EvenOdd = fillRule == "evenodd" };
            }

            if (p.TryGetValue("fill-opacity", out var fillOpacity) && fillOpacity != "inherit")
            {
                state = state with { FillOpacity = SvgValues.ParseOpacity(fillOpacity) };
            }

            if (p.TryGetValue("stroke", out var stroke) && stroke != "inherit")
            {
                state = state with { Stroke = SvgPaint.Parse(stroke) };
            }

            if (p.TryGetValue("stroke-width", out var strokeWidth) && strokeWidth != "inherit")
            {
                state = state with { StrokeWidth = SvgValues.ParseLength(strokeWidth, "stroke-width") };
            }

            if (p.TryGetValue("stroke-linecap", out var lineCap) && lineCap != "inherit")
            {
                state = state with
                {
                    LineCap = lineCap switch
                    {
                        "round" => PdfLineCap.Round,
                        "square" => PdfLineCap.Square,
                        _ => PdfLineCap.Butt
                    }
                };
            }

            if (p.TryGetValue("stroke-linejoin", out var lineJoin) && lineJoin != "inherit")
            {
                state = state with
                {
                    LineJoin = lineJoin switch
                    {
                        "round" => PdfLineJoin.Round,
                        "bevel" => PdfLineJoin.Bevel,
                        _ => PdfLineJoin.Miter
                    }
                };
            }

            if (p.TryGetValue("stroke-miterlimit", out var miterLimit) && miterLimit != "inherit")
            {
                state = state with { MiterLimit = SvgValues.ParseLength(miterLimit, "stroke-miterlimit") };
            }

            if (p.TryGetValue("stroke-opacity", out var strokeOpacity) && strokeOpacity != "inherit")
            {
                state = state with { StrokeOpacity = SvgValues.ParseOpacity(strokeOpacity) };
            }

            // Group opacity is not inherited in SVG, but it fades everything inside the group.
            if (p.TryGetValue("opacity", out var opacity) && opacity != "inherit")
            {
                state = state with { GroupOpacity = GroupOpacity * SvgValues.ParseOpacity(opacity) };
            }

            if (p.TryGetValue("color", out var color) && color != "inherit")
            {
                state = state with { Color = SvgPaint.Parse(color) };
            }

            if (p.TryGetValue("visibility", out var visibility) && visibility != "inherit")
            {
                state = state with { Visible = visibility == "visible" };
            }

            return state;
        }

        public PdfPaint? ResolveFill() => Resolve(Fill, FillOpacity);

        public PdfPaint? ResolveStroke() => StrokeWidth > 0 ? Resolve(Stroke, StrokeOpacity) : null;

        private PdfPaint? Resolve(SvgPaint paint, double opacity)
        {
            var color = paint.IsCurrentColor ? Color : paint;
            if (color.IsNone || color.Alpha * opacity * GroupOpacity < 0.5)
            {
                return null;
            }

            return color.IsLight ? PdfPaint.White : PdfPaint.Black;
        }
    }
}

/// <summary>A fill or stroke color reduced to what a monochrome label can print.</summary>
internal readonly record struct SvgPaint(bool IsNone, bool IsCurrentColor, bool IsLight, double Alpha)
{
    public static readonly SvgPaint None = new(true, false, false, 0);
    public static readonly SvgPaint Black = new(false, false, false, 1);

    private static readonly Regex RgbFunction = new(
        @"^rgba?\(\s*([^,\s)]+)[\s,]+([^,\s)]+)[\s,]+([^,\s)/]+)(?:[\s,/]+([^,\s)]+))?\s*\)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static SvgPaint Parse(string value)
    {
        var text = value.Trim();
        if (text is "none" or "transparent")
        {
            return None;
        }

        if (text.Equals("currentColor", StringComparison.OrdinalIgnoreCase))
        {
            return new SvgPaint(false, true, false, 1);
        }

        if (text.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("The label pictogram uses a gradient or pattern paint, which is not supported.");
        }

        if (text.StartsWith('#'))
        {
            return FromHex(text);
        }

        var rgb = RgbFunction.Match(text);
        if (rgb.Success)
        {
            var alpha = rgb.Groups[4].Success ? SvgValues.ParseOpacity(rgb.Groups[4].Value) : 1;
            return FromRgb(Channel(rgb.Groups[1].Value), Channel(rgb.Groups[2].Value), Channel(rgb.Groups[3].Value), alpha);
        }

        var named = System.Drawing.Color.FromName(text);
        if (!named.IsKnownColor)
        {
            throw new FormatException($"The label pictogram uses an unknown color \"{value}\".");
        }

        return FromRgb(named.R, named.G, named.B, named.A / 255.0);
    }

    private static SvgPaint FromHex(string text)
    {
        var hex = text[1..];
        if (hex.Length is 3 or 4)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        if (hex.Length is not (6 or 8) || !hex.All(Uri.IsHexDigit))
        {
            throw new FormatException($"The label pictogram uses an invalid color \"{text}\".");
        }

        int Byte(int index) => Convert.ToInt32(hex.Substring(index, 2), 16);
        return FromRgb(Byte(0), Byte(2), Byte(4), hex.Length == 8 ? Byte(6) / 255.0 : 1);
    }

    private static double Channel(string value)
    {
        return value.EndsWith('%')
            ? SvgValues.ParseNumber(value[..^1], "color") * 2.55
            : SvgValues.ParseNumber(value, "color");
    }

    /// <summary>Light colors (luma of at least half) print white, the rest black.</summary>
    private static SvgPaint FromRgb(double r, double g, double b, double alpha)
    {
        var luma = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
        return new SvgPaint(false, false, luma >= 0.5, alpha);
    }
}

/// <summary>Number, length and list parsing with SVG rules (invariant culture, px only).</summary>
internal static class SvgValues
{
    public static double ParseNumber(string value, string what)
    {
        if (!double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
            double.IsNaN(number) || double.IsInfinity(number))
        {
            throw new FormatException($"The label pictogram has an invalid {what} \"{value}\".");
        }

        return number;
    }

    /// <summary>A length in user units; "px" is accepted, other units are not.</summary>
    public static double ParseLength(string value, string what)
    {
        var text = value.Trim();
        if (text.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^2];
        }

        return ParseNumber(text, what);
    }

    public static double ParseOpacity(string value)
    {
        var text = value.Trim();
        var opacity = text.EndsWith('%')
            ? ParseNumber(text[..^1], "opacity") / 100
            : ParseNumber(text, "opacity");
        return Math.Clamp(opacity, 0, 1);
    }

    public static List<double> ParseNumberList(string value, string what)
    {
        return value
            .Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => ParseNumber(x, what))
            .ToList();
    }
}
