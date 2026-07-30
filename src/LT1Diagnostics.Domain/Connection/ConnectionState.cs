namespace LT1Diagnostics.Domain.Connection;

public enum ConnectionState
{
    Disconnected,
    Discovering,
    Connecting,
    Connected,
    Reconnecting,
    Faulted,
}

public sealed record ConnectionSnapshot(
    string TransportId,
    string? DeviceId,
    ConnectionState State,
    DateTimeOffset ObservedAt,
    ConnectionDataQualityMetrics Metrics,
    string? Detail);

