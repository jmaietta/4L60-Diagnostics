using LT1Diagnostics.Acquisition.RawSessions;
using LT1Diagnostics.Simulator;
using LT1Diagnostics.Transport.Abstractions;
using LT1Diagnostics.Transport.Replay;

namespace LT1Diagnostics.Replay.Tests;

public sealed class SimulatorRecordingReplayTests
{
    [Fact]
    public async Task SimulatorSessionRecordsAndReplaysDataByteForByte()
    {
        SimulatorScenario scenario = SimulatorScenarioCatalog.Get(SimulatorScenarioId.HealthyRoadTest);
        await using var simulator = new SimulatorTransport(scenario);
        TransportDevice simulatorDevice = Assert.Single(await simulator.DiscoverAsync(CancellationToken.None));
        await simulator.ConnectAsync(simulatorDevice, new TransportSettings(), CancellationToken.None);

        using var stream = new MemoryStream();
        await using (var writer = new RawSessionWriter(stream, leaveOpen: true))
        {
            await new RawSessionRecorder(writer).RecordAsync(simulator);
        }

        var records = new List<RawSessionRecord>();
        await foreach (RawSessionRecord record in new RawSessionReader(stream).ReadAllAsync())
        {
            records.Add(record);
        }

        IReadOnlyList<ReplayItem> items = RawSessionReplayProjector.Project(records);
        await using var replay = new ReplayTransport(items, new ReplayOptions
        {
            TimingMode = ReplayTimingMode.Accelerated,
            AccelerationFactor = 1_000_000,
        });
        TransportDevice replayDevice = Assert.Single(await replay.DiscoverAsync(CancellationToken.None));
        await replay.ConnectAsync(replayDevice, new TransportSettings(), CancellationToken.None);

        var replayed = new List<byte[]>();
        await foreach (TransportChunk chunk in replay.ReadAllAsync(CancellationToken.None))
        {
            replayed.Add(chunk.Bytes.ToArray());
        }

        byte[][] expected = scenario.Steps
            .Where(step => step.Kind == TransportChunkKind.Data)
            .Select(step => step.Bytes.ToArray())
            .ToArray();
        Assert.Equal(expected.Length, replayed.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index], replayed[index]);
        }
    }
}

