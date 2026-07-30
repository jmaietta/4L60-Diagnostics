using System.Diagnostics;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using LT1Diagnostics.Transport.Abstractions;

namespace LT1Diagnostics.Transport.Serial;

public sealed class SerialPortTransport : ITransport
{
    private readonly Lock _gate = new();
    private SerialPort? _port;
    private CancellationTokenSource? _connectionCancellation;
    private bool _disposed;

    public string TransportId => "serial-port";

    public TransportCapabilities Capabilities =>
        TransportCapabilities.Discovery |
        TransportCapabilities.Read |
        TransportCapabilities.Write |
        TransportCapabilities.DeviceRemovalDetection |
        TransportCapabilities.InputPurge;

    public Task<IReadOnlyList<TransportDevice>> DiscoverAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<TransportDevice> devices = SerialPort.GetPortNames()
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(name => new TransportDevice(name, name))
            .ToArray();

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

        lock (_gate)
        {
            if (_port is { IsOpen: true })
            {
                throw new InvalidOperationException("The transport is already connected.");
            }

            var port = new SerialPort(device.Id, settings.BaudRate, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                DtrEnable = false,
                RtsEnable = false,
                ReadTimeout = ToSerialTimeout(settings.ReadTimeout),
                WriteTimeout = ToSerialTimeout(settings.WriteTimeout),
            };

            try
            {
                port.Open();
                if (settings.PurgeInputOnConnect)
                {
                    port.DiscardInBuffer();
                }

                _connectionCancellation = new CancellationTokenSource();
                _port = port;
            }
            catch
            {
                port.Dispose();
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        SerialPort port = GetConnectedPort();
        await port.BaseStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        await port.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<TransportChunk> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        SerialPort port = GetConnectedPort();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _connectionCancellation?.Token ?? CancellationToken.None);
        var buffer = new byte[4096];

        while (!linkedCancellation.IsCancellationRequested)
        {
            int count = 0;
            string? disconnectDetail = null;
            try
            {
                count = await port.BaseStream
                    .ReadAsync(buffer.AsMemory(), linkedCancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
            {
                yield break;
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                disconnectDetail = exception.Message;
            }

            if (disconnectDetail is not null)
            {
                yield return CreateEventChunk(TransportChunkKind.Disconnected, disconnectDetail);
                break;
            }

            if (count == 0)
            {
                yield return CreateEventChunk(TransportChunkKind.Disconnected, "The serial device returned end-of-stream.");
                yield break;
            }

            var bytes = new byte[count];
            buffer.AsSpan(0, count).CopyTo(bytes);
            long timestamp = MonotonicTicks();
            yield return new TransportChunk(
                bytes,
                timestamp,
                DateTimeOffset.UtcNow,
                TransportChunkKind.Data,
                new TransportDiagnostics(
                    FirstByteTimestamp: timestamp,
                    LastByteTimestamp: timestamp));
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _connectionCancellation?.Cancel();
            _connectionCancellation?.Dispose();
            _connectionCancellation = null;

            if (_port is not null)
            {
                if (_port.IsOpen)
                {
                    _port.Close();
                }

                _port.Dispose();
                _port = null;
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

    private SerialPort GetConnectedPort()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            return _port is { IsOpen: true } port
                ? port
                : throw new InvalidOperationException("The transport is not connected.");
        }
    }

    private static int ToSerialTimeout(TimeSpan timeout)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            return SerialPort.InfiniteTimeout;
        }

        return checked((int)Math.Clamp(timeout.TotalMilliseconds, 1, int.MaxValue));
    }

    private static TransportChunk CreateEventChunk(TransportChunkKind kind, string detail) => new(
        ReadOnlyMemory<byte>.Empty,
        MonotonicTicks(),
        DateTimeOffset.UtcNow,
        kind,
        new TransportDiagnostics(Detail: detail));

    private static long MonotonicTicks() => Stopwatch.GetElapsedTime(0, Stopwatch.GetTimestamp()).Ticks;
}
