using System.Runtime.CompilerServices;
using LT1Diagnostics.Transport.Abstractions;

namespace LT1Diagnostics.Simulator;

public sealed class SimulatorTransport : ITransport
{
    private readonly SimulatorScenario _scenario;
    private bool _connected;
    private bool _disposed;

    public SimulatorTransport(SimulatorScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        _scenario = scenario;
    }

    public string TransportId => $"simulator:{_scenario.Id}";

    public TransportCapabilities Capabilities =>
        TransportCapabilities.Discovery |
        TransportCapabilities.Read |
        TransportCapabilities.Write |
        TransportCapabilities.Deterministic |
        TransportCapabilities.DeviceRemovalDetection;

    public Task<IReadOnlyList<TransportDevice>> DiscoverAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<TransportDevice> devices =
        [
            new($"simulator:{_scenario.Id}", _scenario.DisplayName),
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
        if (!string.Equals(device.Id, $"simulator:{_scenario.Id}", StringComparison.Ordinal))
        {
            throw new ArgumentException("The selected device does not belong to this simulator.", nameof(device));
        }

        _connected = true;
        return Task.CompletedTask;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_connected)
        {
            return ValueTask.FromException(new InvalidOperationException("The simulator is not connected."));
        }

        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<TransportChunk> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_connected)
        {
            throw new InvalidOperationException("The simulator is not connected.");
        }

        DateTimeOffset epoch = DateTimeOffset.UnixEpoch;
        foreach (SimulatorStep step in _scenario.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return new TransportChunk(
                step.Bytes,
                step.Offset.Ticks,
                epoch + step.Offset,
                step.Kind,
                new TransportDiagnostics(step.Quality, step.Detail));
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _connected = false;
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
}
