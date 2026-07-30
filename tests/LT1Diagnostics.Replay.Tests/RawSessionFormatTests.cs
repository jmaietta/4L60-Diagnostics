using LT1Diagnostics.Acquisition.RawSessions;

namespace LT1Diagnostics.Replay.Tests;

public sealed class RawSessionFormatTests
{
    [Fact]
    public async Task WriterReaderRoundTripPreservesOpaqueBytesAndMetadata()
    {
        using var stream = new MemoryStream();
        await using (var writer = new RawSessionWriter(stream, leaveOpen: true))
        {
            await writer.AppendAsync(
                RawSessionRecordType.BytesReceived,
                1234,
                DateTimeOffset.UnixEpoch.AddSeconds(5),
                RawSessionRecordAttributes.SourceReportedCorrupt,
                new byte[] { 0x00, 0xFF, 0x42 });
        }

        List<RawSessionRecord> records = await ReadAllAsync(stream);
        RawSessionRecord record = Assert.Single(records);
        Assert.Equal(RawSessionRecordType.BytesReceived, record.KnownType);
        Assert.Equal(0, record.Sequence);
        Assert.Equal(1234, record.MonotonicTimestamp);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(5), record.WallClockTimestamp);
        Assert.Equal([0x00, 0xFF, 0x42], record.Payload.ToArray());
        Assert.Equal(RawSessionIntegrityStatus.Valid, record.IntegrityStatus);
        Assert.True(record.Attributes.HasFlag(RawSessionRecordAttributes.SourceReportedCorrupt));
    }

    [Fact]
    public async Task ReopeningACompleteFileContinuesTheSequence()
    {
        using var stream = new MemoryStream();
        await using (var first = new RawSessionWriter(stream, leaveOpen: true))
        {
            await first.AppendAsync(RawSessionRecordType.OperatorMarker, 1, DateTimeOffset.UnixEpoch, RawSessionRecordAttributes.None, new byte[] { 1 });
        }

        await using (var second = new RawSessionWriter(stream, leaveOpen: true))
        {
            await second.AppendAsync(RawSessionRecordType.OperatorMarker, 2, DateTimeOffset.UnixEpoch, RawSessionRecordAttributes.None, new byte[] { 2 });
        }

        List<RawSessionRecord> records = await ReadAllAsync(stream);
        Assert.Equal([0L, 1L], records.Select(record => record.Sequence));
    }

    [Fact]
    public async Task UnknownTypeRemainsReadable()
    {
        using var stream = new MemoryStream();
        await using (var writer = new RawSessionWriter(stream, leaveOpen: true))
        {
            await writer.AppendAsync((RawSessionRecordType)65000, 0, DateTimeOffset.UnixEpoch, RawSessionRecordAttributes.None, new byte[] { 9 });
        }

        RawSessionRecord record = Assert.Single(await ReadAllAsync(stream));
        Assert.Equal(65000, record.TypeId);
        Assert.Null(record.KnownType);
        Assert.Equal(RawSessionIntegrityStatus.Valid, record.IntegrityStatus);
    }

    [Fact]
    public void StandardCrc32VectorMatchesPublishedAlgorithmValue()
    {
        Assert.Equal(0xCBF43926u, Crc32.Compute("123456789"u8));
    }

    private static async Task<List<RawSessionRecord>> ReadAllAsync(Stream stream)
    {
        var records = new List<RawSessionRecord>();
        var reader = new RawSessionReader(stream);
        await foreach (RawSessionRecord record in reader.ReadAllAsync())
        {
            records.Add(record);
        }

        return records;
    }
}
