using LT1Diagnostics.Acquisition.RawSessions;
using LT1Diagnostics.Acquisition.Recording;
using LT1Diagnostics.Simulator;
using LT1Diagnostics.Transport.Abstractions;

namespace LT1Diagnostics.Replay.Tests;

public sealed class RecordingTransportTests
{
    [Fact]
    public async Task DecoratorRecordsTransmittedAndReceivedBytes()
    {
        using var stream = new MemoryStream();
        await using (var writer = new RawSessionWriter(stream, leaveOpen: true))
        await using (var recording = new RecordingTransport(
            new SimulatorTransport(SimulatorScenarioCatalog.Get(SimulatorScenarioId.HealthyIdle)),
            writer))
        {
            TransportDevice device = Assert.Single(await recording.DiscoverAsync(CancellationToken.None));
            await recording.ConnectAsync(device, new TransportSettings(), CancellationToken.None);
            await recording.WriteAsync(new byte[] { 0xDE, 0xAD }, CancellationToken.None);
            await foreach (TransportChunk _ in recording.ReadAllAsync(CancellationToken.None))
            {
            }

            await recording.DisconnectAsync(CancellationToken.None);
        }

        var records = new List<RawSessionRecord>();
        await foreach (RawSessionRecord record in new RawSessionReader(stream).ReadAllAsync())
        {
            records.Add(record);
        }

        RawSessionRecord transmitted = Assert.Single(
            records,
            record => record.KnownType == RawSessionRecordType.BytesTransmitted);
        Assert.Equal([0xDE, 0xAD], transmitted.Payload.ToArray());
        Assert.Equal(3, records.Count(record => record.KnownType == RawSessionRecordType.BytesReceived));
    }
}
