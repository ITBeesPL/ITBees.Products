using System.Text.RegularExpressions;
using ITBees.Products.Services.Labels.Pdf;

namespace ITBees.Products.Services.Labels.Svg;

/// <summary>
/// 2D affine transform in SVG notation: x' = A x + C y + E, y' = B x + D y + F.
/// </summary>
internal readonly record struct SvgMatrix(double A, double B, double C, double D, double E, double F)
{
    public static readonly SvgMatrix Identity = new(1, 0, 0, 1, 0, 0);

    private static readonly Regex TransformFunction = new(
        @"(matrix|translate|scale|rotate|skewX|skewY)\s*\(([^()]*)\)",
        RegexOptions.CultureInvariant);

    public static SvgMatrix Translation(double x, double y) => new(1, 0, 0, 1, x, y);

    /// <summary>Scale factor applied to lengths such as the stroke width.</summary>
    public double AverageScale => Math.Sqrt(Math.Abs(A * D - B * C));

    /// <summary>
    /// Uniform scale, rotation, reflection and translation only - a stroke keeps one width in
    /// every direction under such a transform.
    /// </summary>
    public bool IsSimilarity
    {
        get
        {
            var xLength = A * A + B * B;
            var yLength = C * C + D * D;
            var tolerance = 1e-9 * Math.Max(xLength, yLength);
            return Math.Abs(A * C + B * D) <= tolerance && Math.Abs(xLength - yLength) <= tolerance;
        }
    }

    public (double X, double Y) Apply(double x, double y) => (A * x + C * y + E, B * x + D * y + F);

    /// <summary>Transform that applies <paramref name="inner"/> first, then this one.</summary>
    public SvgMatrix Multiply(SvgMatrix inner)
    {
        return new SvgMatrix(
            A * inner.A + C * inner.B,
            B * inner.A + D * inner.B,
            A * inner.C + C * inner.D,
            B * inner.C + D * inner.D,
            A * inner.E + C * inner.F + E,
            B * inner.E + D * inner.F + F);
    }

    /// <summary>Parses an SVG transform list, e.g. "translate(10 5) rotate(-30 50 50)".</summary>
    public static SvgMatrix Parse(string transform)
    {
        var result = Identity;
        var consumed = 0;
        foreach (Match match in TransformFunction.Matches(transform))
        {
            if (!IsSeparatorOnly(transform, consumed, match.Index))
            {
                break;
            }

            consumed = match.Index + match.Length;
            var args = SvgValues.ParseNumberList(match.Groups[2].Value, "transform");
            result = result.Multiply(Function(match.Groups[1].Value, args, transform));
        }

        if (!IsSeparatorOnly(transform, consumed, transform.Length))
        {
            throw new FormatException($"The label pictogram has an invalid transform \"{transform}\".");
        }

        return result;
    }

    private static SvgMatrix Function(string name, List<double> a, string transform)
    {
        // Browsers ignore a transform with a wrong number of arguments - so it is rejected here.
        var valid = name switch
        {
            "matrix" => a.Count == 6,
            "translate" or "scale" => a.Count is 1 or 2,
            "rotate" => a.Count is 1 or 3,
            _ => a.Count == 1
        };
        if (!valid)
        {
            throw new FormatException($"The label pictogram has an invalid transform \"{transform}\".");
        }

        double Arg(int index) => a[index];

        switch (name)
        {
            case "matrix":
                return new SvgMatrix(Arg(0), Arg(1), Arg(2), Arg(3), Arg(4), Arg(5));
            case "translate":
                return Translation(Arg(0), a.Count > 1 ? a[1] : 0);
            case "scale":
                return new SvgMatrix(Arg(0), 0, 0, a.Count > 1 ? a[1] : a[0], 0, 0);
            case "rotate":
            {
                var angle = Arg(0) * Math.PI / 180;
                var rotation = new SvgMatrix(Math.Cos(angle), Math.Sin(angle), -Math.Sin(angle), Math.Cos(angle), 0, 0);
                if (a.Count < 3)
                {
                    return rotation;
                }

                // rotate(a cx cy) = translate(cx cy) rotate(a) translate(-cx -cy)
                return Translation(a[1], a[2]).Multiply(rotation).Multiply(Translation(-a[1], -a[2]));
            }
            case "skewX":
                return new SvgMatrix(1, 0, Math.Tan(Arg(0) * Math.PI / 180), 1, 0, 0);
            default: // skewY
                return new SvgMatrix(1, Math.Tan(Arg(0) * Math.PI / 180), 0, 1, 0, 0);
        }
    }

    private static bool IsSeparatorOnly(string text, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            if (!char.IsWhiteSpace(text[i]) && text[i] != ',')
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Collects path segments in SVG user space and stores them transformed to logo coordinates.
/// Quadratic curves and elliptical arcs are converted to cubic Beziers, the only curve PDF has.
/// </summary>
internal sealed class SvgPathBuilder
{
    // Control point distance of a cubic Bezier approximating a quarter of a circle.
    private const double Kappa = 0.5522847498307936;

    // Logo coordinates beyond this are not a pictogram but a numeric accident.
    private const double MaxCoordinate = 1e7;

    private readonly SvgMatrix _transform;
    private readonly List<PdfPathSegment> _segments = new();
    private bool _hasCurrentPoint;
    private bool _subpathClosed;

    public SvgPathBuilder(SvgMatrix transform)
    {
        _transform = transform;
    }

    public IReadOnlyList<PdfPathSegment> Segments => _segments;

    /// <summary>Current point in user space.</summary>
    public double X { get; private set; }

    public double Y { get; private set; }

    private double StartX { get; set; }
    private double StartY { get; set; }

    public void MoveTo(double x, double y)
    {
        var (tx, ty) = Transform(x, y);
        _segments.Add(new PdfPathSegment(PdfPathSegmentType.MoveTo, X3: tx, Y3: ty));
        X = StartX = x;
        Y = StartY = y;
        _hasCurrentPoint = true;
        _subpathClosed = false;
    }

    public void LineTo(double x, double y)
    {
        EnsureSubpath();
        var (tx, ty) = Transform(x, y);
        _segments.Add(new PdfPathSegment(PdfPathSegmentType.LineTo, X3: tx, Y3: ty));
        X = x;
        Y = y;
    }

    public void CurveTo(double x1, double y1, double x2, double y2, double x, double y)
    {
        EnsureSubpath();
        var (tx1, ty1) = Transform(x1, y1);
        var (tx2, ty2) = Transform(x2, y2);
        var (tx, ty) = Transform(x, y);
        _segments.Add(new PdfPathSegment(PdfPathSegmentType.CurveTo, tx1, ty1, tx2, ty2, tx, ty));
        X = x;
        Y = y;
    }

    public void QuadraticTo(double qx, double qy, double x, double y)
    {
        CurveTo(
            X + 2.0 / 3 * (qx - X), Y + 2.0 / 3 * (qy - Y),
            x + 2.0 / 3 * (qx - x), y + 2.0 / 3 * (qy - y),
            x, y);
    }

    public void Close()
    {
        if (!_hasCurrentPoint || _subpathClosed)
        {
            return;
        }

        _segments.Add(new PdfPathSegment(PdfPathSegmentType.Close));
        X = StartX;
        Y = StartY;
        _subpathClosed = true;
    }

    /// <summary>Elliptical arc (SVG "A" command) to the point, as cubic Beziers of at most 90 degrees.</summary>
    public void ArcTo(double rx, double ry, double rotationDegrees, bool largeArc, bool sweep, double x, double y)
    {
        var x1 = X;
        var y1 = Y;
        if (x1 == x && y1 == y)
        {
            return;
        }

        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        if (rx == 0 || ry == 0)
        {
            LineTo(x, y);
            return;
        }

        // Endpoint to center parameterization - SVG 1.1 implementation notes F.6.5.
        var phi = rotationDegrees * Math.PI / 180;
        var cos = Math.Cos(phi);
        var sin = Math.Sin(phi);
        var dx2 = (x1 - x) / 2;
        var dy2 = (y1 - y) / 2;
        var x1p = cos * dx2 + sin * dy2;
        var y1p = -sin * dx2 + cos * dy2;

        // Radii too small to reach the end point are scaled up - F.6.6.
        var lambda = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
        if (lambda > 1)
        {
            rx *= Math.Sqrt(lambda);
            ry *= Math.Sqrt(lambda);
        }

        var numerator = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
        var denominator = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
        var coefficient = (largeArc != sweep ? 1 : -1) * Math.Sqrt(Math.Max(0, numerator / denominator));
        var cxp = coefficient * rx * y1p / ry;
        var cyp = coefficient * -ry * x1p / rx;
        var cx = cos * cxp - sin * cyp + (x1 + x) / 2;
        var cy = sin * cxp + cos * cyp + (y1 + y) / 2;

        var ux = (x1p - cxp) / rx;
        var uy = (y1p - cyp) / ry;
        var vx = (-x1p - cxp) / rx;
        var vy = (-y1p - cyp) / ry;
        var theta1 = Math.Atan2(uy, ux);
        var deltaTheta = Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
        if (!sweep && deltaTheta > 0)
        {
            deltaTheta -= 2 * Math.PI;
        }
        else if (sweep && deltaTheta < 0)
        {
            deltaTheta += 2 * Math.PI;
        }

        var count = Math.Max(1, (int)Math.Ceiling(Math.Abs(deltaTheta) / (Math.PI / 2) - 1e-9));
        var delta = deltaTheta / count;
        var t = 4.0 / 3 * Math.Tan(delta / 4);

        (double X, double Y) Point(double angle) => (
            cx + rx * Math.Cos(angle) * cos - ry * Math.Sin(angle) * sin,
            cy + rx * Math.Cos(angle) * sin + ry * Math.Sin(angle) * cos);

        (double X, double Y) Derivative(double angle) => (
            -rx * Math.Sin(angle) * cos - ry * Math.Cos(angle) * sin,
            -rx * Math.Sin(angle) * sin + ry * Math.Cos(angle) * cos);

        for (var i = 0; i < count; i++)
        {
            var a1 = theta1 + i * delta;
            var a2 = a1 + delta;
            var p1 = Point(a1);
            (double X, double Y) p2 = i == count - 1 ? (x, y) : Point(a2);
            var d1 = Derivative(a1);
            var d2 = Derivative(a2);
            CurveTo(p1.X + t * d1.X, p1.Y + t * d1.Y, p2.X - t * d2.X, p2.Y - t * d2.Y, p2.X, p2.Y);
        }
    }

    public void AddEllipse(double cx, double cy, double rx, double ry)
    {
        if (rx <= 0 || ry <= 0)
        {
            return;
        }

        var kx = rx * Kappa;
        var ky = ry * Kappa;
        MoveTo(cx + rx, cy);
        CurveTo(cx + rx, cy + ky, cx + kx, cy + ry, cx, cy + ry);
        CurveTo(cx - kx, cy + ry, cx - rx, cy + ky, cx - rx, cy);
        CurveTo(cx - rx, cy - ky, cx - kx, cy - ry, cx, cy - ry);
        CurveTo(cx + kx, cy - ry, cx + rx, cy - ky, cx + rx, cy);
        Close();
    }

    public void AddRectangle(double x, double y, double width, double height, double rx, double ry)
    {
        if (rx <= 0 || ry <= 0)
        {
            MoveTo(x, y);
            LineTo(x + width, y);
            LineTo(x + width, y + height);
            LineTo(x, y + height);
            Close();
            return;
        }

        var kx = rx * Kappa;
        var ky = ry * Kappa;
        MoveTo(x + rx, y);
        LineTo(x + width - rx, y);
        CurveTo(x + width - rx + kx, y, x + width, y + ry - ky, x + width, y + ry);
        LineTo(x + width, y + height - ry);
        CurveTo(x + width, y + height - ry + ky, x + width - rx + kx, y + height, x + width - rx, y + height);
        LineTo(x + rx, y + height);
        CurveTo(x + rx - kx, y + height, x, y + height - ry + ky, x, y + height - ry);
        LineTo(x, y + ry);
        CurveTo(x, y + ry - ky, x + rx - kx, y, x + rx, y);
        Close();
    }

    /// <summary>
    /// The point in logo coordinates - which must be a sane number: an overflowing arc or skew
    /// would otherwise write NaN or Infinity into the PDF, which strict readers refuse.
    /// </summary>
    private (double X, double Y) Transform(double x, double y)
    {
        var (tx, ty) = _transform.Apply(x, y);
        if (!double.IsFinite(tx) || !double.IsFinite(ty) || Math.Abs(tx) > MaxCoordinate || Math.Abs(ty) > MaxCoordinate)
        {
            throw new FormatException("The label pictogram has a point far outside the drawing (check arcs and transforms).");
        }

        return (tx, ty);
    }

    /// <summary>
    /// A drawing command right after a close starts a new subpath at the closed subpath's start,
    /// as in SVG; PDF gets an explicit move there.
    /// </summary>
    private void EnsureSubpath()
    {
        if (!_hasCurrentPoint)
        {
            throw new FormatException("The label pictogram has a path that does not start with a move (M).");
        }

        if (_subpathClosed)
        {
            MoveTo(X, Y);
        }
    }
}

/// <summary>Parser of the SVG path data syntax ("d" attribute) - every command, absolute and relative.</summary>
internal sealed class SvgPathData
{
    private readonly string _text;
    private int _position;

    private SvgPathData(string text)
    {
        _text = text;
    }

    public static void Parse(string d, SvgPathBuilder path)
    {
        new SvgPathData(d).Run(path);
    }

    private void Run(SvgPathBuilder path)
    {
        var command = '\0';
        // Control point reflected by S / T - only valid right after a curve of the same kind.
        double? lastCubicX = null, lastCubicY = null, lastQuadX = null, lastQuadY = null;

        while (true)
        {
            SkipSeparators();
            if (_position >= _text.Length)
            {
                break;
            }

            var c = _text[_position];
            if (char.IsLetter(c) && c != 'e' && c != 'E')
            {
                command = c;
                _position++;
            }
            else if (command == '\0')
            {
                throw Invalid();
            }
            else if (command is 'Z' or 'z')
            {
                throw Invalid();
            }
            else if (command == 'M')
            {
                // Coordinates following a move are implicit line commands.
                command = 'L';
            }
            else if (command == 'm')
            {
                command = 'l';
            }

            var relative = char.IsLower(command);
            var ox = relative ? path.X : 0;
            var oy = relative ? path.Y : 0;
            double? cubicX = null, cubicY = null, quadX = null, quadY = null;

            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                {
                    var x = Number() + ox;
                    var y = Number() + oy;
                    path.MoveTo(x, y);
                    break;
                }
                case 'L':
                {
                    var x = Number() + ox;
                    var y = Number() + oy;
                    path.LineTo(x, y);
                    break;
                }
                case 'H':
                    path.LineTo(Number() + ox, path.Y);
                    break;
                case 'V':
                    path.LineTo(path.X, Number() + oy);
                    break;
                case 'C':
                {
                    var x1 = Number() + ox;
                    var y1 = Number() + oy;
                    var x2 = Number() + ox;
                    var y2 = Number() + oy;
                    var x = Number() + ox;
                    var y = Number() + oy;
                    path.CurveTo(x1, y1, x2, y2, x, y);
                    (cubicX, cubicY) = (x2, y2);
                    break;
                }
                case 'S':
                {
                    var x1 = lastCubicX.HasValue ? 2 * path.X - lastCubicX.Value : path.X;
                    var y1 = lastCubicY.HasValue ? 2 * path.Y - lastCubicY.Value : path.Y;
                    var x2 = Number() + ox;
                    var y2 = Number() + oy;
                    var x = Number() + ox;
                    var y = Number() + oy;
                    path.CurveTo(x1, y1, x2, y2, x, y);
                    (cubicX, cubicY) = (x2, y2);
                    break;
                }
                case 'Q':
                {
                    var qx = Number() + ox;
                    var qy = Number() + oy;
                    var x = Number() + ox;
                    var y = Number() + oy;
                    path.QuadraticTo(qx, qy, x, y);
                    (quadX, quadY) = (qx, qy);
                    break;
                }
                case 'T':
                {
                    var qx = lastQuadX.HasValue ? 2 * path.X - lastQuadX.Value : path.X;
                    var qy = lastQuadY.HasValue ? 2 * path.Y - lastQuadY.Value : path.Y;
                    var x = Number() + ox;
                    var y = Number() + oy;
                    path.QuadraticTo(qx, qy, x, y);
                    (quadX, quadY) = (qx, qy);
                    break;
                }
                case 'A':
                {
                    var rx = Number();
                    var ry = Number();
                    var rotation = Number();
                    var largeArc = Flag();
                    var sweep = Flag();
                    var x = Number() + ox;
                    var y = Number() + oy;
                    path.ArcTo(rx, ry, rotation, largeArc, sweep, x, y);
                    break;
                }
                case 'Z':
                    path.Close();
                    break;
                default:
                    throw Invalid();
            }

            (lastCubicX, lastCubicY, lastQuadX, lastQuadY) = (cubicX, cubicY, quadX, quadY);
        }
    }

    private void SkipSeparators()
    {
        while (_position < _text.Length && (char.IsWhiteSpace(_text[_position]) || _text[_position] == ','))
        {
            _position++;
        }
    }

    /// <summary>Reads a number; SVG allows "1.5.5" (1.5, .5) and "1-2" (1, -2) without separators.</summary>
    private double Number()
    {
        SkipSeparators();
        var start = _position;
        if (_position < _text.Length && _text[_position] is '+' or '-')
        {
            _position++;
        }

        var digits = 0;
        while (_position < _text.Length && char.IsAsciiDigit(_text[_position]))
        {
            _position++;
            digits++;
        }

        if (_position < _text.Length && _text[_position] == '.')
        {
            _position++;
            while (_position < _text.Length && char.IsAsciiDigit(_text[_position]))
            {
                _position++;
                digits++;
            }
        }

        if (digits == 0)
        {
            throw Invalid();
        }

        if (_position < _text.Length && _text[_position] is 'e' or 'E')
        {
            var exponentStart = _position;
            _position++;
            if (_position < _text.Length && _text[_position] is '+' or '-')
            {
                _position++;
            }

            var exponentDigits = 0;
            while (_position < _text.Length && char.IsAsciiDigit(_text[_position]))
            {
                _position++;
                exponentDigits++;
            }

            if (exponentDigits == 0)
            {
                _position = exponentStart;
            }
        }

        return SvgValues.ParseNumber(_text[start.._position], "path");
    }

    /// <summary>Arc flags are single digits and may be written without separators ("a5 5 0 01 10 0").</summary>
    private bool Flag()
    {
        SkipSeparators();
        if (_position < _text.Length && _text[_position] is '0' or '1')
        {
            return _text[_position++] == '1';
        }

        throw Invalid();
    }

    private FormatException Invalid()
    {
        var context = _text.Length <= 40 ? _text : _text[..40] + "...";
        return new FormatException($"The label pictogram has invalid path data near position {_position}: \"{context}\".");
    }
}
