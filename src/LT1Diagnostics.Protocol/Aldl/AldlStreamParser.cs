namespace LT1Diagnostics.Protocol.Aldl;

public enum AldlParseDisposition
{
    ValidFrame,
    InvalidChecksum,
    InvalidLength,
    Noise,
}

public sealed record AldlParseResult(
    AldlParseDisposition Disposition,
    ReadOnlyMemory<byte> RawBytes,
    AldlFrame? Frame = null,
    string? Detail = null);

public sealed class AldlStreamParser
{
    private readonly HashSet<byte> _acceptedDeviceAddresses;
    private readonly List<byte> _buffer = [];

    public AldlStreamParser(IEnumerable<byte> acceptedDeviceAddresses)
    {
        ArgumentNullException.ThrowIfNull(acceptedDeviceAddresses);
        _acceptedDeviceAddresses = acceptedDeviceAddresses.ToHashSet();
        if (_acceptedDeviceAddresses.Count == 0)
        {
            throw new ArgumentException("At least one ALDL device address is required.", nameof(acceptedDeviceAddresses));
        }
    }

    public int BufferedByteCount => _buffer.Count;

    public IReadOnlyList<AldlParseResult> Push(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            _buffer.Add(value);
        }

        var results = new List<AldlParseResult>();
        while (_buffer.Count > 0)
        {
            int startIndex = _buffer.FindIndex(_acceptedDeviceAddresses.Contains);
            if (startIndex < 0)
            {
                EmitAndRemove(results, AldlParseDisposition.Noise, _buffer.Count, "No accepted device address was present.");
                break;
            }

            if (startIndex > 0)
            {
                EmitAndRemove(results, AldlParseDisposition.Noise, startIndex, "Bytes preceded an accepted device address.");
                continue;
            }

            if (_buffer.Count < 2)
            {
                break;
            }

            int totalLength = _buffer[1] - AldlProtocolConstants.EncodedLengthBias;
            if (totalLength is < AldlProtocolConstants.MinimumFrameLength or > AldlProtocolConstants.MaximumFrameLength)
            {
                byte[] invalidHeader = _buffer.GetRange(0, 2).ToArray();
                _buffer.RemoveAt(0);
                results.Add(new AldlParseResult(
                    AldlParseDisposition.InvalidLength,
                    invalidHeader,
                    Detail: "The encoded ALDL length is outside the documented envelope."));
                continue;
            }

            if (_buffer.Count < totalLength)
            {
                break;
            }

            byte[] candidate = _buffer.GetRange(0, totalLength).ToArray();
            _buffer.RemoveRange(0, totalLength);
            if (!AldlFrameBuilder.TryParse(candidate, out AldlFrame? frame) || frame is null)
            {
                results.Add(new AldlParseResult(
                    AldlParseDisposition.InvalidLength,
                    candidate,
                    Detail: "The frame length did not match its encoded length."));
                continue;
            }

            results.Add(frame.ChecksumValid
                ? new AldlParseResult(AldlParseDisposition.ValidFrame, candidate, frame)
                : new AldlParseResult(
                    AldlParseDisposition.InvalidChecksum,
                    candidate,
                    frame,
                    "The modulo-256 sum of all frame bytes was not zero."));
        }

        return results;
    }

    public ReadOnlyMemory<byte> DrainIncomplete()
    {
        byte[] remaining = _buffer.ToArray();
        _buffer.Clear();
        return remaining;
    }

    private void EmitAndRemove(
        ICollection<AldlParseResult> results,
        AldlParseDisposition disposition,
        int count,
        string detail)
    {
        byte[] bytes = _buffer.GetRange(0, count).ToArray();
        _buffer.RemoveRange(0, count);
        results.Add(new AldlParseResult(disposition, bytes, Detail: detail));
    }
}
