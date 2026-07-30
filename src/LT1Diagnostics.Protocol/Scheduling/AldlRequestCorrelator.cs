using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Protocol.Aldl;

namespace LT1Diagnostics.Protocol.Scheduling;

public sealed record AldlOutstandingRequest(
    string RequestId,
    byte DatasetId,
    long WriteCompletedTimestamp);

public sealed record AldlCorrelationResult(
    bool Matched,
    string? RequestId,
    byte? DatasetId,
    long? ResponseLatencyTicks,
    string Detail);

public sealed class AldlRequestCorrelator
{
    private readonly List<AldlOutstandingRequest> _outstanding = [];

    public int OutstandingCount => _outstanding.Count;

    public void Register(AldlOutstandingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            throw new ArgumentException("A request correlation ID cannot be blank.", nameof(request));
        }

        _ = A276MessageFactory.GetMode1DataByteCount(request.DatasetId);
        _outstanding.Add(request);
    }

    public AldlCorrelationResult Correlate(AldlFrame response, long responseCompletedTimestamp)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!A276MessageFactory.TryIdentifyMode1Dataset(response, out byte datasetId))
        {
            return new AldlCorrelationResult(false, null, null, null, "The frame is not a valid documented A276 Mode 1 response.");
        }

        int matchIndex = _outstanding.FindIndex(request => request.DatasetId == datasetId);
        if (matchIndex < 0)
        {
            return new AldlCorrelationResult(false, null, datasetId, null, "No outstanding request expects this dataset.");
        }

        AldlOutstandingRequest request = _outstanding[matchIndex];
        _outstanding.RemoveAt(matchIndex);
        long latency = checked(responseCompletedTimestamp - request.WriteCompletedTimestamp);
        return new AldlCorrelationResult(true, request.RequestId, datasetId, latency, "Matched by A276 Mode 1 response length and dataset definition.");
    }

    public IReadOnlyList<AldlOutstandingRequest> ExpireBefore(long oldestAllowedWriteTimestamp)
    {
        AldlOutstandingRequest[] expired = _outstanding
            .Where(request => request.WriteCompletedTimestamp < oldestAllowedWriteTimestamp)
            .ToArray();
        _outstanding.RemoveAll(request => request.WriteCompletedTimestamp < oldestAllowedWriteTimestamp);
        return expired;
    }
}
