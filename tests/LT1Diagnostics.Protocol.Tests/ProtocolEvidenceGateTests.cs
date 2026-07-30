using LT1Diagnostics.Protocol;

namespace LT1Diagnostics.Protocol.Tests;

public sealed class ProtocolEvidenceGateTests
{
    [Fact]
    public void DocumentaryImplementationDoesNotClaimVehicleVerification()
    {
        Assert.False(ProtocolPhaseStatus.HasVerifiedRoadmasterDefinitions);
        Assert.Contains("capture", ProtocolPhaseStatus.Detail, StringComparison.OrdinalIgnoreCase);
    }
}
