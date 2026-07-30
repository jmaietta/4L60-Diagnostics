namespace LT1Diagnostics.Domain.Connection;

public sealed record ConnectionDataQualityMetrics
{
    public required long ReceivedChunkCount { get; init; }

    public required long SuccessfulPacketCount { get; init; }

    public required long ChecksumFailureCount { get; init; }

    public required long TimeoutCount { get; init; }

    public required long EchoChunkCount { get; init; }

    public required long UnexpectedTrafficCount { get; init; }

    public required long ReconnectCount { get; init; }

    public required long ReceivedByteCount { get; init; }

    public required TimeSpan ObservationDuration { get; init; }

    public required TimeSpan? MeanRequestResponseLatency { get; init; }

    public required TimeSpan? LongestAcquisitionGap { get; init; }

    public double? PacketSuccessRate => Ratio(SuccessfulPacketCount, SuccessfulPacketCount + ChecksumFailureCount);

    public double? ChecksumFailureRate => Ratio(ChecksumFailureCount, SuccessfulPacketCount + ChecksumFailureCount);

    public double? TimeoutRate => Ratio(TimeoutCount, SuccessfulPacketCount + ChecksumFailureCount + TimeoutCount);

    public double? EffectiveSampleRateHz => ObservationDuration > TimeSpan.Zero
        ? ReceivedChunkCount / ObservationDuration.TotalSeconds
        : null;

    private static double? Ratio(long numerator, long denominator) => denominator > 0
        ? (double)numerator / denominator
        : null;
}

