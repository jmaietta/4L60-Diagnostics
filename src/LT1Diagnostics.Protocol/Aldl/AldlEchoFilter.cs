namespace LT1Diagnostics.Protocol.Aldl;

public sealed record AldlEchoFilterResult(
    ReadOnlyMemory<byte> VehicleBytes,
    ReadOnlyMemory<byte> EchoBytes,
    bool ExactEchoCompleted);

public sealed class AldlEchoFilter
{
    private byte[]? _expectedEcho;
    private readonly List<byte> _tentativeEcho = [];
    private int _matchedCount;
    private long _expiresAtTimestamp;

    public void Expect(ReadOnlySpan<byte> transmittedBytes, long expiresAtTimestamp)
    {
        if (transmittedBytes.IsEmpty)
        {
            throw new ArgumentException("An expected echo cannot be empty.", nameof(transmittedBytes));
        }

        _expectedEcho = transmittedBytes.ToArray();
        _tentativeEcho.Clear();
        _matchedCount = 0;
        _expiresAtTimestamp = expiresAtTimestamp;
    }

    public AldlEchoFilterResult Process(ReadOnlySpan<byte> receivedBytes, long timestamp)
    {
        if (_expectedEcho is null || timestamp > _expiresAtTimestamp)
        {
            byte[] expiredPrefix = _tentativeEcho.ToArray();
            Reset();
            return new AldlEchoFilterResult(Concat(expiredPrefix, receivedBytes), ReadOnlyMemory<byte>.Empty, false);
        }

        var vehicle = new List<byte>();
        var echo = new List<byte>();
        foreach (byte value in receivedBytes)
        {
            if (_expectedEcho is not null && value == _expectedEcho[_matchedCount])
            {
                _tentativeEcho.Add(value);
                _matchedCount++;
                if (_matchedCount == _expectedEcho.Length)
                {
                    echo.AddRange(_tentativeEcho);
                    Reset();
                }

                continue;
            }

            if (_tentativeEcho.Count > 0)
            {
                vehicle.AddRange(_tentativeEcho);
                Reset();
            }

            vehicle.Add(value);
        }

        return new AldlEchoFilterResult(vehicle.ToArray(), echo.ToArray(), echo.Count > 0);
    }

    public ReadOnlyMemory<byte> Cancel()
    {
        byte[] tentative = _tentativeEcho.ToArray();
        Reset();
        return tentative;
    }

    private static byte[] Concat(ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> suffix)
    {
        var result = new byte[prefix.Length + suffix.Length];
        prefix.CopyTo(result);
        suffix.CopyTo(result.AsSpan(prefix.Length));
        return result;
    }

    private void Reset()
    {
        _expectedEcho = null;
        _tentativeEcho.Clear();
        _matchedCount = 0;
        _expiresAtTimestamp = 0;
    }
}
