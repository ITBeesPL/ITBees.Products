using ZXing;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace ITBees.Products.Services.Labels;

/// <summary>QR code as a square grid of dark / light modules, without any quiet zone.</summary>
internal sealed class QrCodeMatrix
{
    private readonly bool[,] _modules;

    private QrCodeMatrix(bool[,] modules)
    {
        _modules = modules;
        Size = modules.GetLength(0);
    }

    /// <summary>Number of modules along one side.</summary>
    public int Size { get; }

    public bool IsDark(int column, int row) => _modules[column, row];

    public static QrCodeMatrix Encode(string content)
    {
        var hints = new Dictionary<EncodeHintType, object>
        {
            // Level M (~15% recovery) - a label gets scratched, but has to stay small.
            { EncodeHintType.ERROR_CORRECTION, ErrorCorrectionLevel.M },
            { EncodeHintType.MARGIN, 0 }
        };

        // Requested size 0 makes the writer return exactly one cell per module.
        var bits = new QRCodeWriter().encode(content, BarcodeFormat.QR_CODE, 0, 0, hints);

        var modules = new bool[bits.Width, bits.Height];
        for (var row = 0; row < bits.Height; row++)
        {
            for (var column = 0; column < bits.Width; column++)
            {
                modules[column, row] = bits[column, row];
            }
        }

        return new QrCodeMatrix(modules);
    }

    /// <summary>
    /// Dark modules merged into horizontal runs - far fewer shapes to draw than one per module.
    /// </summary>
    public IEnumerable<(int Column, int Row, int Length)> GetDarkRuns()
    {
        for (var row = 0; row < Size; row++)
        {
            var column = 0;
            while (column < Size)
            {
                if (!IsDark(column, row))
                {
                    column++;
                    continue;
                }

                var start = column;
                while (column < Size && IsDark(column, row))
                {
                    column++;
                }

                yield return (start, row, column - start);
            }
        }
    }
}
