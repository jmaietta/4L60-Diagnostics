namespace LT1Diagnostics.Protocol.Aldl;

public sealed record AldlFrame(
    byte DeviceAddress,
    byte EncodedLength,
    byte Mode,
    ReadOnlyMemory<byte> Payload,
    byte Checksum,
    ReadOnlyMemory<byte> RawBytes)
{
    public int TotalLength => RawBytes.Length;

    public bool ChecksumValid => AldlChecksum.IsValid(RawBytes.Span);
}

public static class AldlFrameBuilder
{
    public static byte[] Build(byte deviceAddress, byte mode, ReadOnlySpan<byte> payload)
    {
        int totalLength = AldlProtocolConstants.MinimumFrameLength + payload.Length;
        if (totalLength > AldlProtocolConstants.MaximumFrameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payload),
                payload.Length,
                $"An ALDL frame may contain at most {AldlProtocolConstants.MaximumFrameLength - AldlProtocolConstants.MinimumFrameLength} payload bytes.");
        }

        var frame = new byte[totalLength];
        frame[0] = deviceAddress;
        frame[1] = checked((byte)(totalLength + AldlProtocolConstants.EncodedLengthBias));
        frame[2] = mode;
        payload.CopyTo(frame.AsSpan(3));
        frame[^1] = AldlChecksum.Compute(frame.AsSpan(0, frame.Length - 1));
        return frame;
    }

    public static bool TryParse(ReadOnlySpan<byte> rawBytes, out AldlFrame? frame)
    {
        frame = null;
        if (rawBytes.Length < AldlProtocolConstants.MinimumFrameLength)
        {
            return false;
        }

        int decodedLength = rawBytes[1] - AldlProtocolConstants.EncodedLengthBias;
        if (decodedLength != rawBytes.Length || decodedLength > AldlProtocolConstants.MaximumFrameLength)
        {
            return false;
        }

        byte[] preserved = rawBytes.ToArray();
        frame = new AldlFrame(
            preserved[0],
            preserved[1],
            preserved[2],
            preserved.AsMemory(3, preserved.Length - AldlProtocolConstants.MinimumFrameLength),
            preserved[^1],
            preserved);
        return true;
    }
}
