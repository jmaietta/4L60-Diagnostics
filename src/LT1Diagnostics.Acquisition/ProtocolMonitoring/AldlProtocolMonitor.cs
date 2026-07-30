using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Protocol.Aldl;

namespace LT1Diagnostics.Acquisition.ProtocolMonitoring;

public sealed record AldlProtocolHealthSnapshot(
    long ValidFrameCount,
    long ChecksumFailureCount,
    long InvalidLengthCount,
    long NoiseByteCount,
    int BufferedByteCount)
{
    public long CompletedFrameCount => ValidFrameCount + ChecksumFailureCount;
}

public sealed class AldlProtocolMonitor
{
    private readonly AldlStreamParser _parser = new([A276MessageFactory.DeviceAddress]);
    private long _validFrameCount;
    private long _checksumFailureCount;
    private long _invalidLengthCount;
    private long _noiseByteCount;

    public AldlProtocolHealthSnapshot Observe(ReadOnlySpan<byte> bytes)
    {
        foreach (AldlParseResult result in _parser.Push(bytes))
        {
            switch (result.Disposition)
            {
                case AldlParseDisposition.ValidFrame:
                    _validFrameCount++;
                    break;
                case AldlParseDisposition.InvalidChecksum:
                    _checksumFailureCount++;
                    break;
                case AldlParseDisposition.InvalidLength:
                    _invalidLengthCount++;
                    break;
                case AldlParseDisposition.Noise:
                    _noiseByteCount += result.RawBytes.Length;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported ALDL parse disposition: {result.Disposition}.");
            }
        }

        return Snapshot;
    }

    public AldlProtocolHealthSnapshot Snapshot => new(
        _validFrameCount,
        _checksumFailureCount,
        _invalidLengthCount,
        _noiseByteCount,
        _parser.BufferedByteCount);

    public void Reset()
    {
        _ = _parser.DrainIncomplete();
        _validFrameCount = 0;
        _checksumFailureCount = 0;
        _invalidLengthCount = 0;
        _noiseByteCount = 0;
    }
}
