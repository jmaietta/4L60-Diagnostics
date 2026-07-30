using LT1Diagnostics.Transport.Abstractions;
using LT1Diagnostics.Transport.Replay;

namespace LT1Diagnostics.Transport.Tests;

public sealed class ReplayTransportTests
{
    [Fact]
    public async Task AcceleratedReplayPreservesOrderAndBytes()
    {
        IReadOnlyList<ReplayItem> items =
        [
            Item(0, [1, 2]),
            Item(10, [3, 4]),
            Item(20, [5, 6]),
        ];
        await using var transport = new ReplayTransport(items, new ReplayOptions
        {
            TimingMode = ReplayTimingMode.Accelerated,
            AccelerationFactor = 1_000_000,
        });
        TransportDevice device = Assert.Single(await transport.DiscoverAsync(CancellationToken.None));
        await transport.ConnectAsync(device, new TransportSettings(), CancellationToken.None);

        var actual = new List<byte[]>();
        await foreach (TransportChunk chunk in transport.ReadAllAsync(CancellationToken.None))
        {
            actual.Add(chunk.Bytes.ToArray());
        }

        Assert.Equal(3, actual.Count);
        Assert.Equal([1, 2], actual[0]);
        Assert.Equal([3, 4], actual[1]);
        Assert.Equal([5, 6], actual[2]);
    }

    [Fact]
    public async Task CorruptionInjectionIsDeterministicAndFlagged()
    {
        await using var transport = new ReplayTransport(
            [Item(0, [0x10]), Item(1, [0x20])],
            new ReplayOptions
            {
                TimingMode = ReplayTimingMode.Accelerated,
                AccelerationFactor = 1_000_000,
                CorruptEveryNthDataChunk = 2,
                CorruptionMask = 0x01,
            });
        TransportDevice device = Assert.Single(await transport.DiscoverAsync(CancellationToken.None));
        await transport.ConnectAsync(device, new TransportSettings(), CancellationToken.None);

        var chunks = new List<TransportChunk>();
        await foreach (TransportChunk chunk in transport.ReadAllAsync(CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(0x21, chunks[1].Bytes.Span[0]);
        Assert.True(chunks[1].Diagnostics?.Quality.HasFlag(TransportQuality.ReplayInjectedCorruption));
    }

    [Fact]
    public async Task PacketLossInjectionDropsTheConfiguredOrdinal()
    {
        await using var transport = new ReplayTransport(
            [Item(0, [0x10]), Item(1, [0x20]), Item(2, [0x30])],
            new ReplayOptions
            {
                TimingMode = ReplayTimingMode.Accelerated,
                AccelerationFactor = 1_000_000,
                DropEveryNthDataChunk = 2,
            });
        TransportDevice device = Assert.Single(await transport.DiscoverAsync(CancellationToken.None));
        await transport.ConnectAsync(device, new TransportSettings(), CancellationToken.None);

        var bytes = new List<byte>();
        await foreach (TransportChunk chunk in transport.ReadAllAsync(CancellationToken.None))
        {
            bytes.Add(chunk.Bytes.Span[0]);
        }

        Assert.Equal([0x10, 0x30], bytes);
    }

    private static ReplayItem Item(int milliseconds, byte[] bytes) => new(
        TimeSpan.FromMilliseconds(milliseconds),
        new TransportChunk(bytes, TimeSpan.FromMilliseconds(milliseconds).Ticks, DateTimeOffset.UnixEpoch));
}
