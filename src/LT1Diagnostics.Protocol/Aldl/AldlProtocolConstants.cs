namespace LT1Diagnostics.Protocol.Aldl;

public static class AldlProtocolConstants
{
    public const int EncodedLengthBias = 0x52;

    public const int MinimumFrameLength = 4;

    public const int MaximumFrameLength = byte.MaxValue - EncodedLengthBias;
}
