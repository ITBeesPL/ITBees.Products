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

    // A pictogram is a few kilobytes; the limits only stop hostile input early.
    private const int MaxSvgLength = 256 * 1024;
    private const int MaxGroupDepth = 32;

    public static StockLabelLogo Parse(string svg)
    {
        if (string.IsNullOrWhiteSpace(svg))
        {
            throw new FormatException("The label pictogram SVG is empty.");
        }

        if (svg.Length > MaxSvgLength)
        {
            throw new FormatException($"The label pictogram SVG is larger than {MaxSvgLength / 1024} kB.");
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

        // The label always fits the whole viewBox into its box, keeping the proportions.
        var aspectRatio = Attribute(root, "preserveAspectRatio")?.ToLowerInvariant() ?? string.Empty;
        if (aspectRatio.Contains("none") || aspectRatio.Contains("slice"))
        {
            throw new FormatException(
                $"The label pictogram uses preserveAspectRatio=\"{aspectRatio}\", which is not supported.");
        }

        var shapes = new List<LogoShape>();
        VisitChildren(root, PaintState.Initial.Apply(ReadProperties(root)),
            SvgMatrix.Translation(-minX, -minY), shapes, 0);

        if (shapes.Count == 0)
        {
            throw new FormatException("The label pictogram does not draw anything.");
        }

        return new StockLabelLogo(width, height, shapes);
    }

    private static void VisitChildren(XElement parent, PaintState state, SvgMatrix transform, List<LogoShape> shapes,
        int depth)
    {
        if (depth > MaxGroupDepth)
        {
            throw new FormatException($"The label pictogram nests groups deeper than {MaxGroupDepth} levels.");
        }

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
                // Definitions are never drawn by themselves, but a style sheet in them applies to
                // the whole drawing in a browser.
                if (element.Descendants().Any(x => IsSvgElement(x) && x.Name.LocalName == "style" &&
                                                   !string.IsNullOrWhiteSpace(x.Value)))
                {
                    throw new FormatException(
                        "The label pictogram uses a <style> sheet, which is not supported - use presentation attributes (fill, stroke ...).");
                }

                continue;
            }

            if (name != "g" && !ShapeElements.Contains(name))
            {
                throw new FormatException(
                    $"The label pictogram uses <{name}>, which is not supported - use only paths and basic shapes.");
            }

            var properties = ReadProperties(element);
            if (properties.TryGetValue("display", out var display) &&
                display.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var elementState = state.Apply(properties);
            var elementTransform = properties.TryGetValue("transform", out var transformText)
                ? transform.Multiply(SvgMatrix.Parse(transformText))
                : transform;

            if (name == "g")
            {
                VisitChildren(element, elementState, elementTransform, shapes, depth + 1);
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

        // The stroke is drawn with one width in every direction, which is what the browser does
        // only under a uniform scale and rotation.
        if (stroke != null && !transform.IsSimilarity)
        {
            throw new FormatException(
                "The label pictogram strokes a shape under a non-uniform scale or skew, which is not supported.");
        }

        var strokeWidth = state.StrokeWidth * transform.AverageScale;
        if (!double.IsFinite(strokeWidth) || strokeWidth > 1e7)
        {
            throw new FormatException("The label pictogram has a stroke too wide to draw.");
        }

        return new LogoShape(path.Segments, new PdfPathStyle
        {
            Fill = fill,
            EvenOdd = state.EvenOdd,
            Stroke = stroke,
            StrokeWidth = strokeWidth,
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
        if (rx < 0 || ry < 0)
        {
            throw new FormatException("The label pictogram has a rectangle with a negative corner radius.");
        }

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

        foreach (var unsupported in new[]
                 {
                     "clip-path", "mask", "filter", "stroke-dasharray", "marker", "marker-start", "marker-mid",
                     "marker-end", "vector-effect"
                 })
        {
            if (properties.TryGetValue(unsupported, out var value) &&
                !value.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException($"The label pictogram uses {unsupported}, which is not supported.");
            }
        }

        // Transforms act around the user space origin, as with the SVG 1.1 defaults.
        if (properties.TryGetValue("transform-origin", out var origin) &&
            !origin.Replace("px", string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).All(x => x == "0"))
        {
            throw new FormatException("The label pictogram uses transform-origin, which is not supported.");
        }

        if (properties.TryGetValue("transform-box", out var box) &&
            !box.Equals("view-box", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("The label pictogram uses transform-box, which is not supported.");
        }

        // The fill is always painted first, then the stroke over it.
        if (properties.TryGetValue("paint-order", out var paintOrder))
        {
            var layers = paintOrder.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            var strokeIndex = layers.IndexOf("stroke");
            var fillIndex = layers.IndexOf("fill");
            if (strokeIndex >= 0 && (fillIndex < 0 || strokeIndex < fillIndex))
            {
                throw new FormatException(
                    "The label pictogram paints a stroke under the fill (paint-order), which is not supported.");
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

        /// <summary>
        /// The element's own properties applied over the inherited ones. "inherit" and "unset"
        /// keep the inherited value (these properties are inherited in SVG), "initial" restores the
        /// default; keywords are case-insensitive; an unknown keyword is rejected rather than
        /// guessed.
        /// </summary>
        public PaintState Apply(IReadOnlyDictionary<string, string> p)
        {
            var state = this;
            if (Value(p, "fill") is { } fill)
            {
                state = state with { Fill = fill == "initial" ? SvgPaint.Black : SvgPaint.Parse(fill) };
            }

            if (Keyword(p, "fill-rule", "nonzero", "nonzero", "evenodd") is { } fillRule)
            {
                state = state with { EvenOdd = fillRule == "evenodd" };
            }

            if (Value(p, "fill-opacity") is { } fillOpacity)
            {
                state = state with { FillOpacity = fillOpacity == "initial" ? 1 : SvgValues.ParseOpacity(fillOpacity) };
            }

            if (Value(p, "stroke") is { } stroke)
            {
                state = state with { Stroke = stroke == "initial" ? SvgPaint.None : SvgPaint.Parse(stroke) };
            }

            if (Value(p, "stroke-width") is { } strokeWidth)
            {
                var width = strokeWidth == "initial" ? 1 : SvgValues.ParseLength(strokeWidth, "stroke-width");
                if (width < 0)
                {
                    throw new FormatException($"The label pictogram has an invalid stroke-width \"{strokeWidth}\".");
                }

                state = state with { StrokeWidth = width };
            }

            if (Keyword(p, "stroke-linecap", "butt", "butt", "round", "square") is { } lineCap)
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

            if (Keyword(p, "stroke-linejoin", "miter", "miter", "round", "bevel") is { } lineJoin)
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

            if (Value(p, "stroke-miterlimit") is { } miterLimit)
            {
                state = state with
                {
                    MiterLimit = miterLimit == "initial" ? 4 : SvgValues.ParseLength(miterLimit, "stroke-miterlimit")
                };
            }

            if (Value(p, "stroke-opacity") is { } strokeOpacity)
            {
                state = state with
                {
                    StrokeOpacity = strokeOpacity == "initial" ? 1 : SvgValues.ParseOpacity(strokeOpacity)
                };
            }

            // Group opacity is not inherited in SVG, but it fades everything inside the group.
            if (Value(p, "opacity") is { } opacity && opacity != "initial")
            {
                state = state with { GroupOpacity = GroupOpacity * SvgValues.ParseOpacity(opacity) };
            }

            // "color: currentColor" keeps the inherited color.
            if (Value(p, "color") is { } color && color != "currentcolor")
            {
                state = state with { Color = color == "initial" ? SvgPaint.Black : SvgPaint.Parse(color) };
            }

            if (Keyword(p, "visibility", "visible", "visible", "hidden", "collapse") is { } visibility)
            {
                state = state with { Visible = visibility == "visible" };
            }

            return state;
        }

        /// <summary>
        /// The property's value, with the CSS-wide keywords lower-cased; null when it is not set
        /// or keeps the inherited value.
        /// </summary>
        private static string? Value(IReadOnlyDictionary<string, string> p, string name)
        {
            if (!p.TryGetValue(name, out var raw))
            {
                return null;
            }

            var value = raw.Trim();
            var keyword = value.ToLowerInvariant();
            if (keyword is "inherit" or "unset")
            {
                return null;
            }

            return keyword is "initial" or "currentcolor" ? keyword : value;
        }

        /// <summary>A keyword property: one of the allowed values (lower-case), "initial" mapped to its default.</summary>
        private static string? Keyword(IReadOnlyDictionary<string, string> p, string name, string initial,
            params string[] allowed)
        {
            var value = Value(p, name)?.ToLowerInvariant();
            if (value == null)
            {
                return null;
            }

            if (value == "initial")
            {
                return initial;
            }

            return allowed.Contains(value)
                ? value
                : throw new FormatException($"The label pictogram has an invalid {name} \"{value}\".");
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
        @"^rgba?\(\s*([^,\s)/]+)[\s,]+([^,\s)/]+)[\s,]+([^,\s)/]+)(?:\s*[,/]\s*([^,\s)/]+))?\s*\)$",
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
