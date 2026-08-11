using LT1Diagnostics.Acquisition.RawSessions;

namespace LT1Diagnostics.Replay.Tests;

public sealed class RawSessionAtomicAppendTests
{
    [Fact]
    public async Task CancellationAfterHeaderWriteDoesNotTearRecord()
    {
        using var cancellation = new CancellationTokenSource();
        using var stream = new CancelAfterFirstAsyncWriteStream(cancellation);
        await using var writer = new RawSessionWriter(stream, leaveOpen: true);

        await writer.AppendAsync(
            RawSessionRecordType.BytesReceived,
            monotonicTimestamp: 42,
            DateTimeOffset.UnixEpoch,
            RawSessionRecordAttributes.None,
            new byte[] { 0xF4, 0x56, 0x08, 0xAE },
            cancellation.Token);
        await writer.FlushAsync();

        RawSessionRecord record = Assert.Single(await ReadAllAsync(stream));
        Assert.Equal(RawSessionIntegrityStatus.Valid, record.IntegrityStatus);
        Assert.Equal(new byte[] { 0xF4, 0x56, 0x08, 0xAE }, record.Payload.ToArray());
    }

    [Fact]
    public async Task AppendedRecordsAreDurableWithoutExplicitFlush()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lt1raw-durability-{Guid.NewGuid():N}.lt1raw");
        try
        {
            await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            {
                await using var writer = new RawSessionWriter(stream, leaveOpen: true);
                await writer.AppendAsync(
                    RawSessionRecordType.BytesReceived,
                    monotonicTimestamp: 42,
                    DateTimeOffset.UnixEpoch,
                    RawSessionRecordAttributes.None,
                    new byte[] { 0xF4, 0x56, 0x08, 0xAE });

                // A second independent reader must see the record even though the
                // writer has not been flushed or disposed: captured evidence cannot
                // depend on a clean application shutdown.
                using var verification = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                RawSessionRecord record = Assert.Single(await ReadAllAsync(verification));
                Assert.Equal(RawSessionIntegrityStatus.Valid, record.IntegrityStatus);
                Assert.Equal(new byte[] { 0xF4, 0x56, 0x08, 0xAE }, record.Payload.ToArray());
            }
        }
        finally
        {
            File.Delete(path);
        }
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

    private sealed class CancelAfterFirstAsyncWriteStream(CancellationTokenSource cancellation) : MemoryStream
    {
        private bool _canceled;

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ValueTask write = base.WriteAsync(buffer, cancellationToken);
            if (!_canceled)
            {
                _canceled = true;
                cancellation.Cancel();
            }

            return write;
        }
    }
}
