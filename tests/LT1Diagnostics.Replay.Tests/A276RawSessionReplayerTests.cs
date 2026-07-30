using LT1Diagnostics.Acquisition.A276;
using LT1Diagnostics.Acquisition.RawSessions;
using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Protocol.Aldl;

namespace LT1Diagnostics.Replay.Tests;

public sealed class A276RawSessionReplayerTests
{
    [Fact]
    public async Task ReplaysValidTransmissionSnapshotThroughProductionParser()
    {
        using var stream = new MemoryStream();
        await WriteTransmissionRecordAsync(stream);

        A276RawSessionReplayResult result = await new A276RawSessionReplayer().ReplayAsync(stream);

        Assert.True(result.HasTransmissionSnapshot);
        Assert.False(result.HasIntegrityFailures);
        Assert.Equal(1, result.RecordCount);
        Assert.Equal(1, result.ReceivedChunkCount);
        Assert.Equal(1, result.ValidFrameCount);
        Assert.NotNull(result.TransmissionResponse);
        Assert.Single(result.TransmissionObservations);
        Assert.Equal(0, result.TransmissionObservations[0].Sample.EngineSpeedRpm);
    }

    [Fact]
    public async Task ExcludesStorageCorruptRecordFromDecoding()
    {
        using var stream = new MemoryStream();
        await WriteTransmissionRecordAsync(stream);
        byte[] file = stream.ToArray();
        file[RawSessionFormat.PreambleLength + RawSessionFormat.RecordHeaderLength + 3] ^= 0x01;
        using var corruptStream = new MemoryStream(file);

        A276RawSessionReplayResult result = await new A276RawSessionReplayer().ReplayAsync(corruptStream);

        Assert.True(result.HasIntegrityFailures);
        Assert.False(result.HasTransmissionSnapshot);
        Assert.Equal(1, result.CorruptRecordCount);
        Assert.Equal(0, result.ValidFrameCount);
    }

    private static async Task WriteTransmissionRecordAsync(Stream stream)
    {
        await using var writer = new RawSessionWriter(stream, leaveOpen: true);
        byte[] response = AldlFrameBuilder.Build(
            A276MessageFactory.DeviceAddress,
            0x01,
            new byte[A276MessageFactory.GetMode1DataByteCount(1)]);
        await writer.AppendAsync(
            RawSessionRecordType.BytesReceived,
            1,
            DateTimeOffset.UtcNow,
            RawSessionRecordAttributes.None,
            response);
    }
}
