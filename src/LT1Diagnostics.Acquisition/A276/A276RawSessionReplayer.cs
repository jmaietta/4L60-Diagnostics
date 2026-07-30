using LT1Diagnostics.Acquisition.RawSessions;
using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Protocol.Aldl;
using LT1Diagnostics.Transport.Abstractions;
using LT1Diagnostics.Transport.Replay;

namespace LT1Diagnostics.Acquisition.A276;

public sealed record A276RawSessionReplayResult(
    long RecordCount,
    long CorruptRecordCount,
    long ReceivedChunkCount,
    long ReceivedByteCount,
    long ValidFrameCount,
    long ChecksumFailureCount,
    long InvalidLengthCount,
    long NoiseByteCount,
    long? FirstDataTimestamp,
    long? LastDataTimestamp,
    IReadOnlyList<byte> ObservedModuleAddresses,
    AldlFrame? IdentityResponse,
    AldlFrame? TransmissionResponse,
    IReadOnlyList<A276TransmissionObservation> TransmissionObservations,
    bool ContainsSimulatedData,
    string Detail)
{
    public bool HasIntegrityFailures => CorruptRecordCount > 0;

    public bool HasTransmissionSnapshot => TransmissionResponse is not null;
}

public sealed class A276RawSessionReplayer
{
    private static readonly byte[] AllDeviceAddresses = Enumerable.Range(0, 256)
        .Select(value => checked((byte)value))
        .ToArray();

    public async Task<A276RawSessionReplayResult> ReplayFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await ReplayAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    public async Task<A276RawSessionReplayResult> ReplayAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var records = new List<RawSessionRecord>();
        await foreach (RawSessionRecord record in new RawSessionReader(stream)
            .ReadAllAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            records.Add(record);
        }

        long corruptRecordCount = records.LongCount(record =>
            record.IntegrityStatus != RawSessionIntegrityStatus.Valid);
        bool containsSimulatedData = records.Any(record =>
            record.Attributes.HasFlag(RawSessionRecordAttributes.Simulated));
        IReadOnlyList<ReplayItem> items = RawSessionReplayProjector.Project(records);

        await using var replay = new ReplayTransport(items, new ReplayOptions
        {
            TimingMode = ReplayTimingMode.Accelerated,
            AccelerationFactor = 1_000_000_000,
        });
        TransportDevice device = AssertSingleReplayDevice(
            await replay.DiscoverAsync(cancellationToken).ConfigureAwait(false));
        await replay.ConnectAsync(device, new TransportSettings(), cancellationToken).ConfigureAwait(false);

        var parser = new AldlStreamParser(AllDeviceAddresses);
        var observedAddresses = new HashSet<byte>();
        long receivedChunkCount = 0;
        long receivedByteCount = 0;
        long validFrameCount = 0;
        long checksumFailureCount = 0;
        long invalidLengthCount = 0;
        long noiseByteCount = 0;
        AldlFrame? identityResponse = null;
        AldlFrame? transmissionResponse = null;
        var transmissionObservations = new List<A276TransmissionObservation>();

        await foreach (TransportChunk chunk in replay.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (chunk.Kind != TransportChunkKind.Data)
            {
                continue;
            }

            receivedChunkCount++;
            receivedByteCount += chunk.Bytes.Length;
            if (chunk.Diagnostics?.Quality.HasFlag(TransportQuality.SourceReportedCorrupt) == true)
            {
                continue;
            }

            foreach (AldlParseResult parseResult in parser.Push(chunk.Bytes.Span))
            {
                switch (parseResult.Disposition)
                {
                    case AldlParseDisposition.ValidFrame:
                        validFrameCount++;
                        AldlFrame frame = parseResult.Frame
                            ?? throw new InvalidDataException("A valid replay parse result did not include its frame.");
                        observedAddresses.Add(frame.DeviceAddress);
                        if (A276MessageFactory.TryIdentifyMode1Dataset(frame, out byte datasetId))
                        {
                            if (datasetId == 4)
                            {
                                identityResponse = frame;
                            }
                            else if (datasetId == 1)
                            {
                                transmissionResponse = frame;
                                transmissionObservations.Add(new A276TransmissionObservation(
                                    chunk.MonotonicTimestamp,
                                    A276TransmissionDecoder.DecodeMode1Message1(frame)));
                            }
                        }

                        break;
                    case AldlParseDisposition.InvalidChecksum:
                        checksumFailureCount++;
                        break;
                    case AldlParseDisposition.InvalidLength:
                        invalidLengthCount++;
                        break;
                    case AldlParseDisposition.Noise:
                        noiseByteCount += parseResult.RawBytes.Length;
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unsupported ALDL replay disposition: {parseResult.Disposition}.");
                }
            }
        }

        ReadOnlyMemory<byte> incomplete = parser.DrainIncomplete();
        noiseByteCount += incomplete.Length;
        await replay.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);

        string detail = CreateDetail(
            items.Count,
            corruptRecordCount,
            transmissionResponse is not null);
        return new A276RawSessionReplayResult(
            records.Count,
            corruptRecordCount,
            receivedChunkCount,
            receivedByteCount,
            validFrameCount,
            checksumFailureCount,
            invalidLengthCount,
            noiseByteCount,
            items.Count > 0 ? items[0].Chunk.MonotonicTimestamp : null,
            items.Count > 0 ? items[^1].Chunk.MonotonicTimestamp : null,
            observedAddresses.Order().ToArray(),
            identityResponse,
            transmissionResponse,
            transmissionObservations.AsReadOnly(),
            containsSimulatedData,
            detail);
    }

    private static TransportDevice AssertSingleReplayDevice(IReadOnlyList<TransportDevice> devices) =>
        devices.Count == 1
            ? devices[0]
            : throw new InvalidDataException("The raw-session replay transport did not expose exactly one source.");

    private static string CreateDetail(
        int replayItemCount,
        long corruptRecordCount,
        bool hasTransmissionSnapshot)
    {
        if (replayItemCount == 0)
        {
            return "The file is valid, but it contains no received vehicle data.";
        }

        if (corruptRecordCount > 0)
        {
            return hasTransmissionSnapshot
                ? "The replay completed, but damaged records were excluded from decoding."
                : "The replay is incomplete because the file contains damaged records.";
        }

        return hasTransmissionSnapshot
            ? "The saved snapshot was replayed and decoded without storage-integrity errors."
            : "The file replayed successfully, but it does not contain a complete transmission snapshot.";
    }
}
