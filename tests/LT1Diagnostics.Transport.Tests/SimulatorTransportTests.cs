using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Protocol.Aldl;
using LT1Diagnostics.Simulator;
using LT1Diagnostics.Transport.Abstractions;
using LT1Diagnostics.Transport.Serial;

namespace LT1Diagnostics.Transport.Tests;

public sealed class SimulatorTransportTests
{
    [Fact]
    public void CatalogIncludesEveryRequiredDeterministicScenario()
    {
        Assert.Equal(Enum.GetValues<SimulatorScenarioId>().Length, SimulatorScenarioCatalog.All.Count);
        Assert.All(SimulatorScenarioCatalog.All, scenario => Assert.False(scenario.ContainsVerifiedProtocolData));
    }

    [Fact]
    public async Task SameScenarioProducesSameChunksOnEveryConnection()
    {
        SimulatorScenario scenario = SimulatorScenarioCatalog.Get(SimulatorScenarioId.HealthyRoadTest);
        byte[][] first = await ReadDataAsync(new SimulatorTransport(scenario));
        byte[][] second = await ReadDataAsync(new SimulatorTransport(scenario));

        Assert.Equal(first.Length, second.Length);
        for (int index = 0; index < first.Length; index++)
        {
            Assert.Equal(first[index], second[index]);
        }
    }

    [Fact]
    public async Task HealthySimulatorTraversesProductionParserAsValidA276Frames()
    {
        byte[][] chunks = await ReadDataAsync(new SimulatorTransport(
            SimulatorScenarioCatalog.Get(SimulatorScenarioId.HealthyIdle)));
        var parser = new AldlStreamParser([A276MessageFactory.DeviceAddress]);

        AldlParseResult[] results = chunks.SelectMany(chunk => parser.Push(chunk)).ToArray();

        Assert.Equal(3, results.Length);
        Assert.All(results, result => Assert.Equal(AldlParseDisposition.ValidFrame, result.Disposition));
    }

    [Fact]
    public async Task BadChecksumScenarioIsPreservedAndRejectedByProductionParser()
    {
        byte[][] chunks = await ReadDataAsync(new SimulatorTransport(
            SimulatorScenarioCatalog.Get(SimulatorScenarioId.BadChecksum)));
        var parser = new AldlStreamParser([A276MessageFactory.DeviceAddress]);

        AldlParseResult[] results = chunks.SelectMany(chunk => parser.Push(chunk)).ToArray();

        Assert.Equal(2, results.Count(result => result.Disposition == AldlParseDisposition.ValidFrame));
        Assert.Single(results, result => result.Disposition == AldlParseDisposition.InvalidChecksum);
    }

    [Fact]
    public async Task DisconnectReconnectScenarioCompletesWithoutThrowing()
    {
        var transport = new SimulatorTransport(
            SimulatorScenarioCatalog.Get(SimulatorScenarioId.DeviceDisconnectReconnect));
        TransportDevice device = Assert.Single(await transport.DiscoverAsync(CancellationToken.None));
        await transport.ConnectAsync(device, new TransportSettings(), CancellationToken.None);

        var kinds = new List<TransportChunkKind>();
        await foreach (TransportChunk chunk in transport.ReadAllAsync(CancellationToken.None))
        {
            kinds.Add(chunk.Kind);
        }

        Assert.Contains(TransportChunkKind.Disconnected, kinds);
        Assert.True(kinds.Count(kind => kind == TransportChunkKind.Connected) >= 2);
        await transport.DisposeAsync();
    }

    [Fact]
    public async Task SerialDiscoveryIsByteTransportEnumerationOnly()
    {
        await using var transport = new SerialPortTransport();
        IReadOnlyList<TransportDevice> devices = await transport.DiscoverAsync(CancellationToken.None);
        Assert.All(devices, device => Assert.False(string.IsNullOrWhiteSpace(device.Id)));
    }

    private static async Task<byte[][]> ReadDataAsync(SimulatorTransport transport)
    {
        await using (transport)
        {
            TransportDevice device = Assert.Single(await transport.DiscoverAsync(CancellationToken.None));
            await transport.ConnectAsync(device, new TransportSettings(), CancellationToken.None);
            var chunks = new List<byte[]>();
            await foreach (TransportChunk chunk in transport.ReadAllAsync(CancellationToken.None))
            {
                if (chunk.Kind == TransportChunkKind.Data)
                {
                    chunks.Add(chunk.Bytes.ToArray());
                }
            }

            return chunks.ToArray();
        }
    }
}
