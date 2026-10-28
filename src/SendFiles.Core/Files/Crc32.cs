namespace SendFiles.Core.Files;

/// <summary>CRC-32 (IEEE 802.3, the zlib/PNG one) — matches the phone's tiny JS implementation.</summary>
public sealed class Crc32
{
    private static readonly uint[] Table = BuildTable();
    private uint _crc = 0xFFFFFFFF;

    public uint Value => ~_crc;

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = new Crc32();
        crc.Append(data);
        return crc.Value;
    }

    public void Append(ReadOnlySpan<byte> data)
    {
        var crc = _crc;
        foreach (var b in data)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        _crc = crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[i] = c;
        }

        return table;
    }
}
