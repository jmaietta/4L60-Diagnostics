using System.Runtime.CompilerServices;
using System.Text;
using LT1Diagnostics.Acquisition.RawSessions;
using LT1Diagnostics.Transport.Abstractions;

namespace LT1Diagnostics.Acquisition.Recording;

public sealed class RecordingTransport(ITransport inner, RawSessionWriter writer) : ITransport
{
    private bool _disposed;

    public string TransportId => $"recording:{inner.TransportId}";

    public TransportCapabilities Capabilities => inner.Capabilities;

    public Task<IReadOnlyList<TransportDevice>> DiscoverAsync(CancellationToken cancellationToken) =>
        inner.DiscoverAsync(cancellationToken);

    public async Task ConnectAsync(
        TransportDevice device,
        TransportSettings settings,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await inner.ConnectAsync(device, settings, cancellationToken).ConfigureAwait(false);
        await writer.AppendAsync(
            RawSessionRecordType.TransportConnected,
            MonotonicClock.GetTimestamp(),
            DateTimeOffset.UtcNow,
            RawSessionRecordAttributes.None,
            Encoding.UTF8.GetBytes(device.Id),
            CancellationToken.None).ConfigureAwait(false);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Record when transmission starts, not after it completes, so replay timing
        // matches the instant the bytes reached the wire.
        long writeStartTimestamp = MonotonicClock.GetTimestamp();
        var writeStartWallClock = DateTimeOffset.UtcNow;
        await inner.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        await writer.AppendAsync(
            RawSessionRecordType.BytesTransmitted,
            writeStartTimestamp,
            writeStartWallClock,
            RawSessionRecordAttributes.None,
            data,
            CancellationToken.None).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<TransportChunk> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await foreach (TransportChunk chunk in inner.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await writer.AppendAsync(
                ToRecordType(chunk.Kind),
                chunk.MonotonicTimestamp,
                chunk.WallClockTimestamp,
                ToRecordAttributes(chunk.Diagnostics?.Quality ?? TransportQuality.None),
                chunk.Bytes,
                CancellationToken.None).ConfigureAwait(false);
            yield return chunk;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await inner.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        await writer.AppendAsync(
            RawSessionRecordType.TransportDisconnected,
            MonotonicClock.GetTimestamp(),
            DateTimeOffset.UtcNow,
            RawSessionRecordAttributes.None,
            ReadOnlyMemory<byte>.Empty,
            CancellationToken.None).ConfigureAwait(false);
        await writer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await inner.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }

    private static RawSessionRecordType ToRecordType(TransportChunkKind kind) => kind switch
    {
        TransportChunkKind.Data => RawSessionRecordType.BytesReceived,
        TransportChunkKind.Connected => RawSessionRecordType.TransportConnected,
        TransportChunkKind.Disconnected => RawSessionRecordType.TransportDisconnected,
        TransportChunkKind.Error => RawSessionRecordType.ApplicationError,
        _ => RawSessionRecordType.OperatorMarker,
    };

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
