namespace LT1Diagnostics.Acquisition.RawSessions;

internal static class Crc32
{
    private const uint Polynomial = 0xEDB88320u;

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in data)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                uint mask = (uint)-(int)(crc & 1);
                crc = (crc >> 1) ^ (Polynomial & mask);
            }
        }

        return ~crc;
    }
}

