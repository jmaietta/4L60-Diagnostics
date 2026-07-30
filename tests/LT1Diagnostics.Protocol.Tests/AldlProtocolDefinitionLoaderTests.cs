using LT1Diagnostics.Domain.Definitions;
using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Protocol.Aldl;
using LT1Diagnostics.Protocol.Definitions;

namespace LT1Diagnostics.Protocol.Tests;

public sealed class AldlProtocolDefinitionLoaderTests
{
    [Fact]
    public async Task A276ManifestLoadsAndDecodesWithoutBecomingProductionEligible()
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, "definitions", "protocol", "a276-1994-bd-lt1.unverified.json");
        await using FileStream stream = File.OpenRead(path);
        AldlProtocolDefinitionManifest manifest = await AldlProtocolDefinitionLoader.LoadAsync(stream);
        byte[] data = new byte[46];
        data[7] = 0x19;
        data[8] = 0x00;
        data[32] = 100;
        byte[] raw = AldlFrameBuilder.Build(A276MessageFactory.DeviceAddress, 1, data);
        Assert.True(AldlFrameBuilder.TryParse(raw, out AldlFrame? frame));

        IReadOnlyList<AldlDecodedSignal> decoded = AldlProtocolDefinitionLoader.Decode(manifest, 1, frame!);

        Assert.Equal(VerificationStatus.Unverified, manifest.VerificationStatus);
        Assert.False(manifest.ProductionEligible);
        AldlDecodedSignal rpm = Assert.Single(decoded, signal => signal.SignalId == "engine-speed");
        Assert.Equal(800, rpm.EngineeringValue);
        AldlDecodedSignal tft = Assert.Single(decoded, signal => signal.SignalId == "transmission-fluid-temperature");
        Assert.Equal(35, tft.EngineeringValue);
        Assert.All(decoded, signal => Assert.False(signal.ProductionEligible));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BUILD_PLAN.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
