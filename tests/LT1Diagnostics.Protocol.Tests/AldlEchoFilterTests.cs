using LT1Diagnostics.Protocol.Aldl;

namespace LT1Diagnostics.Protocol.Tests;

public sealed class AldlEchoFilterTests
{
    [Fact]
    public void FragmentedExactEchoIsRemovedAndFollowingResponseIsPreserved()
    {
        byte[] request = [0xF4, 0x57, 0x01, 0x01, 0xB3];
        byte[] response = [0xF4, 0x56, 0x08, 0xAE];
        var filter = new AldlEchoFilter();
        filter.Expect(request, expiresAtTimestamp: 100);

        AldlEchoFilterResult first = filter.Process(request.AsSpan(0, 2), timestamp: 10);
        AldlEchoFilterResult second = filter.Process([.. request.AsSpan(2), .. response], timestamp: 11);

        Assert.Empty(first.VehicleBytes.ToArray());
        Assert.Empty(first.EchoBytes.ToArray());
        Assert.Equal(request, second.EchoBytes.ToArray());
        Assert.Equal(response, second.VehicleBytes.ToArray());
        Assert.True(second.ExactEchoCompleted);
    }

    [Fact]
    public void PrefixMismatchReturnsEveryTentativeByteAsVehicleData()
    {
        var filter = new AldlEchoFilter();
        filter.Expect([0xF4, 0x57, 0x01], expiresAtTimestamp: 100);

        AldlEchoFilterResult result = filter.Process([0xF4, 0x56, 0x08], timestamp: 10);

        Assert.Equal([0xF4, 0x56, 0x08], result.VehicleBytes.ToArray());
        Assert.Empty(result.EchoBytes.ToArray());
    }

    [Fact]
    public void ExpiredTentativePrefixIsNotDiscarded()
    {
        var filter = new AldlEchoFilter();
        filter.Expect([0xF4, 0x57, 0x01], expiresAtTimestamp: 10);
        _ = filter.Process([0xF4], timestamp: 5);

        AldlEchoFilterResult result = filter.Process([0x56, 0x08], timestamp: 11);

        Assert.Equal([0xF4, 0x56, 0x08], result.VehicleBytes.ToArray());
    }
}
