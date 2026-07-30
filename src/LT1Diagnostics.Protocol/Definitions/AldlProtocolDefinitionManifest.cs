using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using LT1Diagnostics.Domain.Definitions;
using LT1Diagnostics.Protocol.Aldl;

namespace LT1Diagnostics.Protocol.Definitions;

public sealed record AldlProtocolDefinitionManifest(
    string SchemaVersion,
    string DefinitionVersion,
    VerificationStatus VerificationStatus,
    bool ProductionEligible,
    string DefinitionId,
    string Application,
    byte DeviceAddress,
    int BaudRate,
    string DataPin,
    int EncodedLengthBias,
    string Checksum,
    IReadOnlyList<AldlMode1DatasetManifest> Mode1Datasets,
    IReadOnlyList<AldlControlModeManifest> ControlModes,
    IReadOnlyList<AldlSignalManifest> Signals,
    IReadOnlyList<string> SourceReferences);

public sealed record AldlMode1DatasetManifest(
    byte DatasetId,
    byte RequestLengthByte,
    byte ResponseLengthByte,
    int DataByteCount,
    string SourceReference);

public sealed record AldlControlModeManifest(
    byte Mode,
    string Name,
    byte RequestLengthByte,
    string SourceReference);

public sealed record AldlSignalManifest(
    string SignalId,
    byte DatasetId,
    int DataByteOffset,
    string DataType,
    double Scale,
    double Offset,
    string? Unit,
    string SourceReference,
    IReadOnlyList<AldlBitManifest>? Bits = null);

public sealed record AldlBitManifest(int Bit, string Name, int? Code);

public sealed record AldlDecodedBit(int Bit, string Name, int? Code, bool IsSet);

public sealed record AldlDecodedSignal(
    string SignalId,
    long RawValue,
    double EngineeringValue,
    string? Unit,
    IReadOnlyList<AldlDecodedBit> Bits,
    string DefinitionVersion,
    VerificationStatus VerificationStatus,
    bool ProductionEligible,
    string SourceReference);

public static class AldlProtocolDefinitionLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<AldlProtocolDefinitionManifest> LoadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        AldlProtocolDefinitionManifest manifest = await JsonSerializer
            .DeserializeAsync<AldlProtocolDefinitionManifest>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException("The protocol definition was empty.");
        Validate(manifest);
        return manifest;
    }

    public static IReadOnlyList<AldlDecodedSignal> Decode(
        AldlProtocolDefinitionManifest manifest,
        byte datasetId,
        AldlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(frame);
        AldlMode1DatasetManifest dataset = manifest.Mode1Datasets.SingleOrDefault(item => item.DatasetId == datasetId)
            ?? throw new ArgumentOutOfRangeException(nameof(datasetId), datasetId, "The definition does not contain this Mode 1 dataset.");
        if (frame.DeviceAddress != manifest.DeviceAddress || frame.Mode != 1 || !frame.ChecksumValid || frame.Payload.Length != dataset.DataByteCount)
        {
            throw new ArgumentException("The frame does not match the selected protocol dataset.", nameof(frame));
        }

        ReadOnlySpan<byte> payload = frame.Payload.Span;
        var decoded = new List<AldlDecodedSignal>();
        foreach (AldlSignalManifest signal in manifest.Signals.Where(signal => signal.DatasetId == datasetId))
        {
            long raw = signal.DataType switch
            {
                "uint8" or "bitfield" => payload[signal.DataByteOffset],
                "uint16be" => BinaryPrimitives.ReadUInt16BigEndian(payload[signal.DataByteOffset..]),
                "int16be" => BinaryPrimitives.ReadInt16BigEndian(payload[signal.DataByteOffset..]),
                _ => throw new InvalidDataException($"Unsupported ALDL data type: {signal.DataType}."),
            };
            IReadOnlyList<AldlDecodedBit> bits = signal.Bits?
                .Select(bit => new AldlDecodedBit(bit.Bit, bit.Name, bit.Code, (raw & (1L << bit.Bit)) != 0))
                .ToArray()
                ?? [];
            decoded.Add(new AldlDecodedSignal(
                signal.SignalId,
                raw,
                (raw * signal.Scale) + signal.Offset,
                signal.Unit,
                bits,
                manifest.DefinitionVersion,
                manifest.VerificationStatus,
                manifest.ProductionEligible && manifest.VerificationStatus == VerificationStatus.Verified,
                signal.SourceReference));
        }

        return decoded.AsReadOnly();
    }

    private static void Validate(AldlProtocolDefinitionManifest manifest)
    {
        if (manifest.SchemaVersion != "1.0.0")
        {
            throw new InvalidDataException($"Unsupported protocol schema version: {manifest.SchemaVersion}.");
        }

        if (manifest.VerificationStatus == VerificationStatus.Unverified && manifest.ProductionEligible)
        {
            throw new InvalidDataException("An unverified protocol definition cannot be production eligible.");
        }

        if (manifest.EncodedLengthBias != AldlProtocolConstants.EncodedLengthBias)
        {
            throw new InvalidDataException("The definition length bias is not supported by this parser version.");
        }

        if (manifest.Mode1Datasets.Select(dataset => dataset.DatasetId).Distinct().Count() != manifest.Mode1Datasets.Count)
        {
            throw new InvalidDataException("Mode 1 dataset IDs must be unique.");
        }

        if (manifest.Signals.Select(signal => signal.SignalId).Distinct(StringComparer.Ordinal).Count() != manifest.Signals.Count)
        {
            throw new InvalidDataException("Signal IDs must be unique.");
        }

        foreach (AldlMode1DatasetManifest dataset in manifest.Mode1Datasets)
        {
            int expectedResponseLength = dataset.DataByteCount + AldlProtocolConstants.MinimumFrameLength;
            if (dataset.ResponseLengthByte != expectedResponseLength + manifest.EncodedLengthBias)
            {
                throw new InvalidDataException($"Dataset {dataset.DatasetId} response length is inconsistent with its byte count.");
            }
        }

        foreach (AldlSignalManifest signal in manifest.Signals)
        {
            AldlMode1DatasetManifest dataset = manifest.Mode1Datasets.SingleOrDefault(item => item.DatasetId == signal.DatasetId)
                ?? throw new InvalidDataException($"Signal {signal.SignalId} refers to an unknown dataset.");
            int width = signal.DataType switch
            {
                "uint8" or "bitfield" => 1,
                "uint16be" or "int16be" => 2,
                _ => throw new InvalidDataException($"Signal {signal.SignalId} has unsupported data type {signal.DataType}."),
            };
            if (signal.DataByteOffset < 0 || signal.DataByteOffset + width > dataset.DataByteCount)
            {
                throw new InvalidDataException($"Signal {signal.SignalId} exceeds dataset {signal.DatasetId} bounds.");
            }
        }
    }
}
