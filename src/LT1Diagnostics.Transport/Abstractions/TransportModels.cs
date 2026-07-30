namespace LT1Diagnostics.Transport.Abstractions;

[Flags]
public enum TransportCapabilities
{
    None = 0,
    Discovery = 1 << 0,
    Read = 1 << 1,
    Write = 1 << 2,
    Replay = 1 << 3,
    Deterministic = 1 << 4,
    DeviceRemovalDetection = 1 << 5,
    InputPurge = 1 << 6,
}

public sealed record TransportDevice(
    string Id,
    string DisplayName,
    string? VendorId = null,
    string? ProductId = null,
    IReadOnlyDictionary<string, string>? Properties = null);

public sealed record TransportSettings
{
    public int BaudRate { get; init; } = 8192;

    public TimeSpan ReadTimeout { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan WriteTimeout { get; init; } = TimeSpan.FromSeconds(1);

    public bool PurgeInputOnConnect { get; init; } = true;
}

public enum TransportChunkKind
{
    Data,
    Connected,
    Disconnected,
    Error,
    OperatorStep,
}

[Flags]
public enum TransportQuality
{
    None = 0,
    Echo = 1 << 0,
    UnexpectedTraffic = 1 << 1,
    SourceReportedCorrupt = 1 << 2,
    SimulatedFault = 1 << 3,
    ReplayInjectedLoss = 1 << 4,
    ReplayInjectedCorruption = 1 << 5,
}

public sealed record TransportDiagnostics(
    TransportQuality Quality = TransportQuality.None,
    string? Detail = null,
    long? QueuedTimestamp = null,
    long? WriteStartTimestamp = null,
    long? WriteEndTimestamp = null,
    long? FirstByteTimestamp = null,
    long? LastByteTimestamp = null);

public sealed record TransportChunk(
    ReadOnlyMemory<byte> Bytes,
    long MonotonicTimestamp,
    DateTimeOffset WallClockTimestamp,
    TransportChunkKind Kind = TransportChunkKind.Data,
    TransportDiagnostics? Diagnostics = null);
