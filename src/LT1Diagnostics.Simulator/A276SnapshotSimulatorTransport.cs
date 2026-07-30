using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Protocol.Aldl;
using LT1Diagnostics.Transport.Abstractions;

namespace LT1Diagnostics.Simulator;

public sealed record A276SnapshotSimulatorOptions
{
    public bool IncludeInitialPcmTraffic { get; init; } = true;

    public byte? DroppedDatasetResponse { get; init; }

    public byte? CorruptedDatasetResponse { get; init; }
}

public sealed class A276SnapshotSimulatorTransport : ITransport
{
    private readonly A276SnapshotSimulatorOptions _options;
    private readonly Lock _gate = new();
    private readonly List<byte[]> _transmittedRequests = [];
    private Channel<TransportChunk>? _chunks;
    private long _timestamp;
    private int _generatedSampleIndex;
    private bool _connected;
    private bool _disposed;

    public A276SnapshotSimulatorTransport(A276SnapshotSimulatorOptions? options = null)
    {
        _options = options ?? new A276SnapshotSimulatorOptions();
    }

    public string TransportId => "simulator:a276-snapshot";

    public TransportCapabilities Capabilities =>
        TransportCapabilities.Discovery |
        TransportCapabilities.Read |
        TransportCapabilities.Write |
        TransportCapabilities.Deterministic |
        TransportCapabilities.DeviceRemovalDetection;

    public IReadOnlyList<byte[]> TransmittedRequests
    {
        get
        {
            lock (_gate)
            {
                return _transmittedRequests.Select(request => request.ToArray()).ToArray();
            }
        }
    }

    public Task<IReadOnlyList<TransportDevice>> DiscoverAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<TransportDevice> devices =
        [
            new("simulator:a276-snapshot", "A276 documentary snapshot simulator"),
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
        if (!string.Equals(device.Id, "simulator:a276-snapshot", StringComparison.Ordinal))
        {
            throw new ArgumentException("The selected device does not belong to this simulator.", nameof(device));
        }

        lock (_gate)
        {
            if (_connected)
            {
                throw new InvalidOperationException("The simulator is already connected.");
            }

            _chunks = Channel.CreateUnbounded<TransportChunk>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });
            _timestamp = TimeSpan.FromMilliseconds(1).Ticks;
            _generatedSampleIndex = 0;
            _transmittedRequests.Clear();
            _connected = true;
            QueueEvent(TransportChunkKind.Connected, "Deterministic A276 snapshot simulator connected.");
            byte initialAddress = _options.IncludeInitialPcmTraffic
                ? A276MessageFactory.DeviceAddress
                : (byte)0xEA;
            for (int index = 0; index < 6; index++)
            {
                QueueData(
                    CreateDatasetResponse(1, initialAddress, _generatedSampleIndex++),
                    TransportQuality.SimulatedFault,
                    $"Synthetic initial {initialAddress:X2} traffic for bus observation; not vehicle evidence.");
            }
        }

        return Task.CompletedTask;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            EnsureConnected();
            byte[] request = data.ToArray();
            _transmittedRequests.Add(request);
            QueueData(request, TransportQuality.Echo | TransportQuality.SimulatedFault, "Deterministic exact cable echo.");

            if (!AldlFrameBuilder.TryParse(request, out AldlFrame? frame) || frame is null || !frame.ChecksumValid)
            {
                QueueEvent(TransportChunkKind.Error, "The simulator received a malformed request.");
                return ValueTask.CompletedTask;
            }

            if (frame.DeviceAddress != A276MessageFactory.DeviceAddress)
            {
                QueueEvent(TransportChunkKind.Error, "The simulator received a request for an unsupported module address.");
                return ValueTask.CompletedTask;
            }

            switch (frame.Mode)
            {
                case 0x00:
                    break;
                case 0x08:
                case 0x09:
                    QueueData(request, TransportQuality.SimulatedFault, "Synthetic documentary control acknowledgement.");
                    break;
                case 0x01 when frame.Payload.Length == 1:
                    byte datasetId = frame.Payload.Span[0];
                    _ = A276MessageFactory.GetMode1DataByteCount(datasetId);
                    if (_options.DroppedDatasetResponse != datasetId)
                    {
                        byte[] response = CreateDatasetResponse(
                            datasetId,
                            A276MessageFactory.DeviceAddress,
                            _generatedSampleIndex++);
                        TransportQuality quality = TransportQuality.SimulatedFault;
                        if (_options.CorruptedDatasetResponse == datasetId)
                        {
                            response[^1] ^= 0xFF;
                            quality |= TransportQuality.SourceReportedCorrupt;
                        }

                        QueueData(
                            response,
                            quality,
                            _options.CorruptedDatasetResponse == datasetId
                                ? $"Deliberately corrupted synthetic A276 Message {datasetId} response."
                                : $"Synthetic documentary A276 Message {datasetId} response; not vehicle evidence.");
                    }

                    break;
                default:
                    QueueEvent(TransportChunkKind.Error, "The simulator received a request outside its read-only documentary scenario.");
                    break;
            }
        }

        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<TransportChunk> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ChannelReader<TransportChunk> reader;
        lock (_gate)
        {
            EnsureConnected();
            reader = _chunks!.Reader;
        }

        await foreach (TransportChunk chunk in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_connected)
            {
                _connected = false;
                _chunks?.Writer.TryComplete();
            }
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true;
    }

    private void QueueData(byte[] bytes, TransportQuality quality, string detail)
    {
        _timestamp = checked(_timestamp + TimeSpan.FromMilliseconds(1).Ticks);
        _chunks!.Writer.TryWrite(new TransportChunk(
            bytes,
            _timestamp,
            DateTimeOffset.UnixEpoch + TimeSpan.FromTicks(_timestamp),
            TransportChunkKind.Data,
            new TransportDiagnostics(quality, detail, FirstByteTimestamp: _timestamp, LastByteTimestamp: _timestamp)));
    }

    private void QueueEvent(TransportChunkKind kind, string detail)
    {
        _timestamp = checked(_timestamp + TimeSpan.FromMilliseconds(1).Ticks);
        _chunks!.Writer.TryWrite(new TransportChunk(
            ReadOnlyMemory<byte>.Empty,
            _timestamp,
            DateTimeOffset.UnixEpoch + TimeSpan.FromTicks(_timestamp),
            kind,
            new TransportDiagnostics(TransportQuality.SimulatedFault, detail)));
    }

    private static byte[] CreateDatasetResponse(byte datasetId, byte deviceAddress, int sampleIndex)
    {
        var data = new byte[A276MessageFactory.GetMode1DataByteCount(datasetId)];
        if (datasetId == 1)
        {
            data[5] = 36;
            int engineSpeedRaw = (800 + (sampleIndex * 120)) * 8;
            data[7] = checked((byte)(engineSpeedRaw >> 8));
            data[8] = checked((byte)(engineSpeedRaw & 0xFF));
            data[9] = checked((byte)(Math.Min(sampleIndex * 6, 60) * 2));
            data[10] = 90;
            data[11] = 51;
            data[12] = 51;
            data[13] = 128;
            data[14] = 1 << 6;
            data[15] = 132;
            data[16] = sampleIndex switch
            {
                < 2 => 0,
                < 4 => 1,
                < 6 => 2,
                _ => 3,
            };
            short slipRaw = checked((short)((180 - (sampleIndex * 20)) * 8));
            data[21] = checked((byte)(slipRaw >> 8));
            data[22] = checked((byte)(slipRaw & 0xFF));
            data[31] = 80;
            data[32] = 70;
        }
        else if (datasetId == 4)
        {
            data[0] = 0xA2;
            data[1] = 0x76;
        }

        return AldlFrameBuilder.Build(deviceAddress, 0x01, data);
    }

    private void EnsureConnected()
    {
        if (!_connected || _chunks is null)
        {
            throw new InvalidOperationException("The simulator is not connected.");
        }
    }
}
