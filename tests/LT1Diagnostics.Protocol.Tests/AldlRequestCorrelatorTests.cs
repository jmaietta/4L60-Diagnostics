using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Protocol.Aldl;
using LT1Diagnostics.Protocol.Scheduling;

namespace LT1Diagnostics.Protocol.Tests;

public sealed class AldlRequestCorrelatorTests
{
    [Fact]
    public void ResponseMatchesOldestOutstandingRequestForItsDataset()
    {
        var correlator = new AldlRequestCorrelator();
        correlator.Register(new AldlOutstandingRequest("first", 1, 100));
        correlator.Register(new AldlOutstandingRequest("second", 1, 110));
        byte[] raw = AldlFrameBuilder.Build(0xF4, 0x01, new byte[46]);
        Assert.True(AldlFrameBuilder.TryParse(raw, out AldlFrame? frame));

        AldlCorrelationResult result = correlator.Correlate(frame!, 140);

        Assert.True(result.Matched);
        Assert.Equal("first", result.RequestId);
        Assert.Equal(40, result.ResponseLatencyTicks);
        Assert.Equal(1, correlator.OutstandingCount);
    }

    [Fact]
    public void InvalidChecksumCannotMatchRequest()
    {
        var correlator = new AldlRequestCorrelator();
        correlator.Register(new AldlOutstandingRequest("request", 1, 100));
        byte[] raw = AldlFrameBuilder.Build(0xF4, 0x01, new byte[46]);
        raw[^1] ^= 1;
        Assert.True(AldlFrameBuilder.TryParse(raw, out AldlFrame? frame));

        AldlCorrelationResult result = correlator.Correlate(frame!, 140);

        Assert.False(result.Matched);
        Assert.Equal(1, correlator.OutstandingCount);
    }

    [Fact]
    public void ExpirationReturnsAndRemovesOnlyOldRequests()
    {
        var correlator = new AldlRequestCorrelator();
        correlator.Register(new AldlOutstandingRequest("old", 1, 99));
        correlator.Register(new AldlOutstandingRequest("current", 4, 100));

        AldlOutstandingRequest expired = Assert.Single(correlator.ExpireBefore(100));

        Assert.Equal("old", expired.RequestId);
        Assert.Equal(1, correlator.OutstandingCount);
    }
}
