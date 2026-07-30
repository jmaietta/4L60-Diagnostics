namespace LT1Diagnostics.Acquisition.RawSessions;

public enum RawSessionRecordType : ushort
{
    SessionHeader = 1,
    VehicleProfileSnapshot = 2,
    TransportConnected = 3,
    TransportDisconnected = 4,
    BytesReceived = 5,
    BytesTransmitted = 6,
    ParsedFrame = 7,
    DecodeResult = 8,
    DtcSnapshot = 9,
    OperatorMarker = 10,
    TestStateTransition = 11,
    ExternalSensorSample = 12,
    ApplicationError = 13,
    SessionFooter = 14,
}

[Flags]
public enum RawSessionRecordAttributes : uint
{
    None = 0,
    SourceReportedCorrupt = 1 << 0,
    Echo = 1 << 1,
    UnexpectedTraffic = 1 << 2,
    Simulated = 1 << 3,
}

public enum RawSessionIntegrityStatus
{
    Valid,
    PayloadChecksumMismatch,
    HeaderChecksumMismatch,
    InvalidRecordMagic,
    InvalidPayloadLength,
    Truncated,
    UnsupportedRecordVersion,
}

public sealed record RawSessionRecord(
    ushort TypeId,
    ushort RecordVersion,
    long Sequence,
    long MonotonicTimestamp,
    DateTimeOffset WallClockTimestamp,
    RawSessionRecordAttributes Attributes,
    ReadOnlyMemory<byte> Payload,
    RawSessionIntegrityStatus IntegrityStatus,
    ReadOnlyMemory<byte> RawHeader)
{
    public RawSessionRecordType? KnownType => Enum.IsDefined(typeof(RawSessionRecordType), TypeId)
        ? (RawSessionRecordType)TypeId
        : null;
}
