using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    [Fact]
    public async Task TransmittedRecordCarriesWriteStartTimestamp()
    {
        var writeDelay = TimeSpan.FromMilliseconds(100);
        using var stream = new MemoryStream();
        await using (var writer = new RawSessionWriter(stream, leaveOpen: true))
        await using (var recording = new RecordingTransport(new DelayedWriteTransport(writeDelay), writer))
        {
            TransportDevice device = Assert.Single(await recording.DiscoverAsync(CancellationToken.None));
            await recording.ConnectAsync(device, new TransportSettings(), CancellationToken.None);

            long before = Stopwatch.GetElapsedTime(0, Stopwatch.GetTimestamp()).Ticks;
            await recording.WriteAsync(new byte[] { 0xA2 }, CancellationToken.None);
            long after = Stopwatch.GetElapsedTime(0, Stopwatch.GetTimestamp()).Ticks;

            var records = new List<RawSessionRecord>();
            await foreach (RawSessionRecord record in new RawSessionReader(stream).ReadAllAsync())
            {
                records.Add(record);
            }

            RawSessionRecord transmitted = Assert.Single(
                records,
                record => record.KnownType == RawSessionRecordType.BytesTransmitted);

            // The recorded timestamp must come from the start of the write, not from
            // after the delayed transmission completed.
            Assert.InRange(transmitted.MonotonicTimestamp, before, after - (writeDelay.Ticks / 2));
        }
    }

    private sealed class DelayedWriteTransport(TimeSpan writeDelay) : ITransport
    {
        public string TransportId => "delayed-write";

        public TransportCapabilities Capabilities =>
            TransportCapabilities.Discovery | TransportCapabilities.Read | TransportCapabilities.Write;

        public Task<IReadOnlyList<TransportDevice>> DiscoverAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TransportDevice>>(
                [new TransportDevice("delayed-write", "Delayed write test transport")]);

        public Task ConnectAsync(
            TransportDevice device,
            TransportSettings settings,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken) =>
            await Task.Delay(writeDelay, cancellationToken).ConfigureAwait(false);

        public async IAsyncEnumerable<TransportChunk> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
