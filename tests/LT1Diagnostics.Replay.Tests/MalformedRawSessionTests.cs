using LT1Diagnostics.Acquisition.RawSessions;

namespace LT1Diagnostics.Replay.Tests;

public sealed class MalformedRawSessionTests
{
    [Fact]
    public async Task CorruptedPayloadIsPreservedAndFlagged()
    {
        byte[] file = await CreateFileAsync([0x10, 0x20, 0x30]);
        file[^1] ^= 0xFF;
        using var stream = new MemoryStream(file);

        RawSessionRecord record = Assert.Single(await ReadAllAsync(stream));
        Assert.Equal([0x10, 0x20, 0xCF], record.Payload.ToArray());
        Assert.Equal(RawSessionIntegrityStatus.PayloadChecksumMismatch, record.IntegrityStatus);
    }

    [Fact]
    public async Task TruncatedPayloadIsPreservedAndFlagged()
    {
        byte[] file = await CreateFileAsync([0x10, 0x20, 0x30]);
        Array.Resize(ref file, file.Length - 1);
        using var stream = new MemoryStream(file);

        RawSessionRecord record = Assert.Single(await ReadAllAsync(stream));
        Assert.Equal([0x10, 0x20], record.Payload.ToArray());
        Assert.Equal(RawSessionIntegrityStatus.Truncated, record.IntegrityStatus);
    }

    [Fact]
    public async Task CorruptedHeaderIsFlaggedWhilePayloadIsPreserved()
    {
        byte[] file = await CreateFileAsync([0xAA, 0xBB]);
        file[RawSessionFormat.PreambleLength + 4] ^= 0x01;
        using var stream = new MemoryStream(file);

        RawSessionRecord record = Assert.Single(await ReadAllAsync(stream));
        Assert.Equal([0xAA, 0xBB], record.Payload.ToArray());
        Assert.Equal(RawSessionIntegrityStatus.HeaderChecksumMismatch, record.IntegrityStatus);
    }

    [Fact]
    public async Task InvalidPayloadLengthIsFlaggedBeforeAllocation()
    {
        byte[] file = await CreateFileAsync([0x01]);
        int lengthOffset = RawSessionFormat.PreambleLength + 36;
        BitConverter.GetBytes(RawSessionFormat.MaximumPayloadLength + 1).CopyTo(file, lengthOffset);
        using var stream = new MemoryStream(file);

        RawSessionRecord record = Assert.Single(await ReadAllAsync(stream));
        Assert.Equal(RawSessionIntegrityStatus.InvalidPayloadLength, record.IntegrityStatus);
        Assert.Empty(record.Payload.ToArray());
    }

    [Fact]
    public async Task TruncatedHeaderIsReturnedAsAFlaggedRecord()
    {
        byte[] file = await CreateFileAsync([0x01]);
        Array.Resize(ref file, RawSessionFormat.PreambleLength + 10);
        using var stream = new MemoryStream(file);

        RawSessionRecord record = Assert.Single(await ReadAllAsync(stream));
        Assert.Equal(RawSessionIntegrityStatus.Truncated, record.IntegrityStatus);
        Assert.Equal(10, record.RawHeader.Length);
    }

    [Fact]
    public async Task InvalidPreambleIsRejectedWithoutAllocatingRecords()
    {
        using var stream = new MemoryStream(new byte[RawSessionFormat.PreambleLength]);
        var reader = new RawSessionReader(stream);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (RawSessionRecord _ in reader.ReadAllAsync())
            {
            }
        });
    }

    private static async Task<byte[]> CreateFileAsync(byte[] payload)
    {
        using var stream = new MemoryStream();
        await using (var writer = new RawSessionWriter(stream, leaveOpen: true))
        {
            await writer.AppendAsync(RawSessionRecordType.BytesReceived, 0, DateTimeOffset.UnixEpoch, RawSessionRecordAttributes.None, payload);
        }

        return stream.ToArray();
    }

    private static async Task<List<RawSessionRecord>> ReadAllAsync(Stream stream)
    {
        var records = new List<RawSessionRecord>();
        await foreach (RawSessionRecord record in new RawSessionReader(stream).ReadAllAsync())
        {
            records.Add(record);
        }

        return records;
    }
}
