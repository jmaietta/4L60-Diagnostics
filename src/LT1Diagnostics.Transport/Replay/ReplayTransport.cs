using System.Runtime.CompilerServices;
using LT1Diagnostics.Transport.Abstractions;

namespace LT1Diagnostics.Transport.Replay;

public enum ReplayTimingMode
{
    Original,
    Accelerated,
    Step,
}

public sealed record ReplayOptions
{
    public ReplayTimingMode TimingMode { get; init; } = ReplayTimingMode.Original;

    public double AccelerationFactor { get; init; } = 1;

    public int? DropEveryNthDataChunk { get; init; }

    public int? CorruptEveryNthDataChunk { get; init; }

    public byte CorruptionMask { get; init; } = 0x01;
}

public sealed record ReplayItem(TimeSpan Offset, TransportChunk Chunk);

public sealed class ReplayTransport : ITransport
{
    private readonly IReadOnlyList<ReplayItem> _items;
    private readonly ReplayOptions _options;
    private readonly SemaphoreSlim _stepSignal = new(0);
    private bool _connected;
    private bool _disposed;

    public ReplayTransport(IReadOnlyList<ReplayItem> items, ReplayOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.OrderBy(item => item.Offset).ToArray();
        _options = options ?? new ReplayOptions();

        if (_options.AccelerationFactor <= 0 || !double.IsFinite(_options.AccelerationFactor))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Acceleration must be finite and greater than zero.");
        }

        ValidateImpairment(_options.DropEveryNthDataChunk, nameof(ReplayOptions.DropEveryNthDataChunk));
        ValidateImpairment(_options.CorruptEveryNthDataChunk, nameof(ReplayOptions.CorruptEveryNthDataChunk));
    }

    public string TransportId => "raw-session-replay";

    public TransportCapabilities Capabilities =>
        TransportCapabilities.Discovery |
        TransportCapabilities.Read |
        TransportCapabilities.Replay |
        TransportCapabilities.Deterministic;

    public Task<IReadOnlyList<TransportDevice>> DiscoverAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<TransportDevice> devices =
        [
            new("replay:loaded", "Loaded raw session"),
        ];
        return Task.FromResult(devices);
    }

    public Task ConnectAsync(
        TransportDevice device,
        TransportSettings settings,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(device.Id, "replay:loaded", StringComparison.Ordinal))
        {
            throw new ArgumentException("The selected device does not belong to this replay transport.", nameof(device));
        }

        _connected = true;
        return Task.CompletedTask;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken) =>
        ValueTask.FromException(new NotSupportedException("Replay transport is read-only."));

    public async IAsyncEnumerable<TransportChunk> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_connected)
        {
            throw new InvalidOperationException("The replay transport is not connected.");
        }

        TimeSpan previousOffset = TimeSpan.Zero;
        int dataIndex = 0;

        foreach (ReplayItem item in _items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WaitForTimingAsync(item.Offset - previousOffset, cancellationToken).ConfigureAwait(false);
            previousOffset = item.Offset;

            TransportChunk chunk = item.Chunk;
            if (chunk.Kind == TransportChunkKind.Data)
            {
                dataIndex++;
                if (IsNth(dataIndex, _options.DropEveryNthDataChunk))
                {
                    continue;
                }

                if (IsNth(dataIndex, _options.CorruptEveryNthDataChunk) && chunk.Bytes.Length > 0)
                {
                    byte[] corrupted = chunk.Bytes.ToArray();
                    corrupted[^1] ^= _options.CorruptionMask;
                    chunk = chunk with
                    {
                        Bytes = corrupted,
                        Diagnostics = MergeDiagnostics(
                            chunk.Diagnostics,
                            TransportQuality.ReplayInjectedCorruption,
                            "Deterministic replay corruption was injected."),
                    };
                }
            }

            yield return chunk;
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _connected = false;
        return Task.CompletedTask;
    }

    public void AdvanceOne()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _stepSignal.Release();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        _stepSignal.Dispose();
        _disposed = true;
    }

    private async Task WaitForTimingAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        switch (_options.TimingMode)
        {
            case ReplayTimingMode.Step:
                await _stepSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                break;
            case ReplayTimingMode.Original when delay > TimeSpan.Zero:
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                break;
            case ReplayTimingMode.Accelerated when delay > TimeSpan.Zero:
                await Task.Delay(TimeSpan.FromTicks((long)(delay.Ticks / _options.AccelerationFactor)), cancellationToken)
                    .ConfigureAwait(false);
                break;
        }
    }

    private static bool IsNth(int index, int? interval) => interval is > 0 && index % interval.Value == 0;

    private static void ValidateImpairment(int? value, string name)
    {
        if (value is <= 0)
        {
            throw new ArgumentOutOfRangeException(name, "An impairment interval must be greater than zero.");
        }
    }

    private static TransportDiagnostics MergeDiagnostics(
        TransportDiagnostics? diagnostics,
        TransportQuality flag,
        string detail) => new(
            (diagnostics?.Quality ?? TransportQuality.None) | flag,
            detail,
            diagnostics?.QueuedTimestamp,
            diagnostics?.WriteStartTimestamp,
            diagnostics?.WriteEndTimestamp,
            diagnostics?.FirstByteTimestamp,
            diagnostics?.LastByteTimestamp);
}
