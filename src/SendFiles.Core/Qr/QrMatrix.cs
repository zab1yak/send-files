using QRCoder;

namespace SendFiles.Core.Qr;

/// <summary>QR modules as a boolean grid without the quiet zone; the host draws it at any DPI.</summary>
public sealed class QrMatrix
{
    private const int QuietZone = 4;
    private readonly bool[,] _modules;

    private QrMatrix(bool[,] modules) => _modules = modules;

    public int Size => _modules.GetLength(0);

    public bool this[int row, int column] => _modules[row, column];

    public static QrMatrix Create(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);

        var full = data.ModuleMatrix;
        var size = full.Count - (2 * QuietZone);
        var modules = new bool[size, size];
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                modules[row, column] = full[row + QuietZone][column + QuietZone];
            }
        }

        return new QrMatrix(modules);
    }
}
