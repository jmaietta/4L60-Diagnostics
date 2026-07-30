using LT1Diagnostics.Domain.Connection;

namespace LT1Diagnostics.Domain.Tests;

public sealed class ConnectionDataQualityMetricsTests
{
    [Fact]
    public void ComputesRatesOnlyFromObservedCounts()
    {
        var metrics = new ConnectionDataQualityMetrics
        {
            ReceivedChunkCount = 50,
            SuccessfulPacketCount = 90,
            ChecksumFailureCount = 10,
            TimeoutCount = 5,
            EchoChunkCount = 2,
            UnexpectedTrafficCount = 1,
            ReconnectCount = 0,
            ReceivedByteCount = 500,
            ObservationDuration = TimeSpan.FromSeconds(10),
            MeanRequestResponseLatency = TimeSpan.FromMilliseconds(25),
            LongestAcquisitionGap = TimeSpan.FromMilliseconds(80),
        };

        Assert.Equal(0.9, metrics.PacketSuccessRate);
        Assert.Equal(0.1, metrics.ChecksumFailureRate);
        Assert.Equal(5.0 / 105.0, metrics.TimeoutRate);
        Assert.Equal(5, metrics.EffectiveSampleRateHz);
    }

    [Fact]
    public void ReturnsNullWhenAStatisticalDenominatorIsUnavailable()
    {
        var metrics = new ConnectionDataQualityMetrics
        {
            ReceivedChunkCount = 0,
            SuccessfulPacketCount = 0,
            ChecksumFailureCount = 0,
            TimeoutCount = 0,
            EchoChunkCount = 0,
            UnexpectedTrafficCount = 0,
            ReconnectCount = 0,
            ReceivedByteCount = 0,
            ObservationDuration = TimeSpan.Zero,
            MeanRequestResponseLatency = null,
            LongestAcquisitionGap = null,
        };

        Assert.Null(metrics.PacketSuccessRate);
        Assert.Null(metrics.ChecksumFailureRate);
        Assert.Null(metrics.TimeoutRate);
        Assert.Null(metrics.EffectiveSampleRateHz);
    }
}

