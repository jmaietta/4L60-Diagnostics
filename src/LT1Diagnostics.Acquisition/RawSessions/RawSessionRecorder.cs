using System.Text;
using LT1Diagnostics.Transport.Abstractions;
using LT1Diagnostics.Transport.Replay;

namespace LT1Diagnostics.Acquisition.RawSessions;

public sealed class RawSessionRecorder(RawSessionWriter writer)
{
    public async Task RecordAsync(ITransport transport, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        byte[] transportId = Encoding.UTF8.GetBytes(transport.TransportId);
        await writer.AppendAsync(
            RawSessionRecordType.TransportConnected,
            0,
            DateTimeOffset.UtcNow,
            RawSessionRecordAttributes.None,
            transportId,
            cancellationToken).ConfigureAwait(false);

        await foreach (TransportChunk chunk in transport.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            RawSessionRecordType type = chunk.Kind switch
            {
                TransportChunkKind.Data => RawSessionRecordType.BytesReceived,
                TransportChunkKind.Disconnected => RawSessionRecordType.TransportDisconnected,
                TransportChunkKind.Error => RawSessionRecordType.ApplicationError,
                _ => RawSessionRecordType.OperatorMarker,
            };

            await writer.AppendAsync(
                type,
                chunk.MonotonicTimestamp,
                chunk.WallClockTimestamp,
                ToRecordAttributes(chunk.Diagnostics?.Quality ?? TransportQuality.None),
                chunk.Bytes,
                cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static RawSessionRecordAttributes ToRecordAttributes(TransportQuality quality)
    {
        RawSessionRecordAttributes result = RawSessionRecordAttributes.None;
        if (quality.HasFlag(TransportQuality.SourceReportedCorrupt))
        {
            result |= RawSessionRecordAttributes.SourceReportedCorrupt;
        }

        if (quality.HasFlag(TransportQuality.Echo))
        {
            result |= RawSessionRecordAttributes.Echo;
        }

        if (quality.HasFlag(TransportQuality.UnexpectedTraffic))
        {
            result |= RawSessionRecordAttributes.UnexpectedTraffic;
        }

        if (quality.HasFlag(TransportQuality.SimulatedFault))
        {
            result |= RawSessionRecordAttributes.Simulated;
        }

        return result;
    }
}

public static class RawSessionReplayProjector
{
    public static IReadOnlyList<ReplayItem> Project(IEnumerable<RawSessionRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        RawSessionRecord[] dataRecords = records
            .Where(record => record.KnownType == RawSessionRecordType.BytesReceived)
            .OrderBy(record => record.Sequence)
            .ToArray();

        if (dataRecords.Length == 0)
        {
            return [];
        }

        long firstTimestamp = dataRecords[0].MonotonicTimestamp;
        return dataRecords.Select(record =>
        {
            TransportQuality quality = TransportQuality.None;
            if (record.Attributes.HasFlag(RawSessionRecordAttributes.SourceReportedCorrupt))
            {
                quality |= TransportQuality.SourceReportedCorrupt;
            }

            if (record.IntegrityStatus != RawSessionIntegrityStatus.Valid)
            {
                quality |= TransportQuality.SourceReportedCorrupt;
            }

            var chunk = new TransportChunk(
                record.Payload,
                record.MonotonicTimestamp,
                record.WallClockTimestamp,
                TransportChunkKind.Data,
                new TransportDiagnostics(quality, $"Raw record integrity: {record.IntegrityStatus}."));

            return new ReplayItem(TimeSpan.FromTicks(Math.Max(0, record.MonotonicTimestamp - firstTimestamp)), chunk);
        }).ToArray();
    }
}
