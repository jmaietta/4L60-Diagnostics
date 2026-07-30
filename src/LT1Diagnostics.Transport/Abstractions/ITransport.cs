namespace LT1Diagnostics.Transport.Abstractions;

public interface ITransport : IAsyncDisposable
{
    string TransportId { get; }

    TransportCapabilities Capabilities { get; }

    Task<IReadOnlyList<TransportDevice>> DiscoverAsync(CancellationToken cancellationToken);

    Task ConnectAsync(
        TransportDevice device,
        TransportSettings settings,
        CancellationToken cancellationToken);

    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

    IAsyncEnumerable<TransportChunk> ReadAllAsync(CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);
}

