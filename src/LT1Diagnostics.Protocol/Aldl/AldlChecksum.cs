namespace LT1Diagnostics.Protocol.Aldl;

public static class AldlChecksum
{
    public static byte Compute(ReadOnlySpan<byte> bytesWithoutChecksum)
    {
        byte sum = 0;
        foreach (byte value in bytesWithoutChecksum)
        {
            sum = unchecked((byte)(sum + value));
        }

        return unchecked((byte)(0 - sum));
    }

    public static bool IsValid(ReadOnlySpan<byte> completeFrame)
    {
        if (completeFrame.Length < AldlProtocolConstants.MinimumFrameLength)
        {
            return false;
        }

        byte sum = 0;
        foreach (byte value in completeFrame)
        {
            sum = unchecked((byte)(sum + value));
        }

        return sum == 0;
    }
}
