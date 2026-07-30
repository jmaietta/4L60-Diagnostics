using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Protocol.Aldl;
using LT1Diagnostics.Protocol.Scheduling;
using LT1Diagnostics.Transport.Abstractions;

namespace LT1Diagnostics.Acquisition.A276;

public enum A276AcquisitionStage
{
    ObservingBus,
    DisablingNormalCommunications,
    RequestingIdentity,
    RequestingTransmissionData,
    RestoringNormalCommunications,
    Completed,
    Incomplete,
}

public enum A276AcquisitionOutcome
{
    Completed,
    PcmNotObserved,
    DisableCommunicationsTimeout,
    IdentityTimeout,
    TransmissionDataTimeout,
    TransportEnded,
}

public enum A276ControlAcknowledgement
{
    None,
    Explicit,
    AmbiguousEchoOrAcknowledgement,
}

public sealed record A276AcquisitionOptions(
    TimeSpan InitialObservationWindow,
    TimeSpan ResponseTimeout,
    TimeSpan EchoWindow)
{
    public void Validate()
    {
        if (InitialObservationWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(InitialObservationWindow),
                "The initial bus-observation window must be positive and explicitly selected.");
        }

        if (ResponseTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ResponseTimeout),
                "The response timeout must be positive and explicitly selected.");
        }

        if (EchoWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(EchoWindow),
                "The echo window must be positive and explicitly selected.");
        }
    }
}

public sealed record A276AcquisitionProgress(A276AcquisitionStage Stage, string Detail);

public sealed record A276AcquisitionResult(
    A276AcquisitionOutcome Outcome,
    string Detail,
    IReadOnlyList<byte> ObservedModuleAddresses,
    A276ControlAcknowledgement DisableAcknowledgement,
    AldlFrame? IdentityResponse,
    AldlFrame? TransmissionResponse,
    bool RestorationAttempted,
    bool RestorationCompleted,
    long ReceivedChunkCount,
    long ReceivedByteCount,
    long ValidFrameCount,
    long ChecksumFailureCount,
    long InvalidLengthCount,
    long NoiseByteCount,
    long ExactEchoCount,
    long? FirstDataTimestamp,
    long? LastDataTimestamp,
    IReadOnlyList<A276TransmissionObservation> TransmissionObservations)
{
    public bool IsComplete => Outcome == A276AcquisitionOutcome.Completed;
}

public sealed class A276AcquisitionCoordinator
{
    private static readonly byte[] AllDeviceAddresses = Enumerable.Range(0, 256)
        .Select(value => checked((byte)value))
        .ToArray();

    private readonly A276AcquisitionOptions _options;
    private readonly AldlStreamParser _parser = new(AllDeviceAddresses);
    private readonly AldlEchoFilter _echoFilter = new();
    private readonly Lock _echoGate = new();
    private readonly Lock _parserGate = new();
    private readonly ConcurrentDictionary<byte, byte> _observedAddresses = new();
    private readonly ConcurrentQueue<A276TransmissionObservation> _transmissionObservations = new();
    private readonly Channel<InboundObservation> _inbound = Channel.CreateUnbounded<InboundObservation>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false,
        });
    private long _receivedChunkCount;
    private long _receivedByteCount;
    private long _validFrameCount;
    private long _checksumFailureCount;
    private long _invalidLengthCount;
    private long _noiseByteCount;
    private long _exactEchoCount;
    private long _lastInboundTimestamp;
    private long _firstDataTimestamp = long.MinValue;
    private long _lastDataTimestamp = long.MinValue;

    public A276AcquisitionCoordinator(A276AcquisitionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
    }

    public async Task<A276AcquisitionResult> AcquireSnapshotAsync(
        ITransport connectedTransport,
        IProgress<A276AcquisitionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectedTransport);
        if (!connectedTransport.Capabilities.HasFlag(TransportCapabilities.Read) ||
            !connectedTransport.Capabilities.HasFlag(TransportCapabilities.Write))
        {
            throw new ArgumentException("A276 acquisition requires a readable and writable byte transport.", nameof(connectedTransport));
        }

        using var readCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task readTask = ReadTransportAsync(connectedTransport, readCancellation.Token);
        A276AcquisitionOutcome outcome = A276AcquisitionOutcome.TransportEnded;
        string detail = "The transport ended before the documentary snapshot sequence completed.";
        A276ControlAcknowledgement disableAcknowledgement = A276ControlAcknowledgement.None;
        AldlFrame? identityResponse = null;
        AldlFrame? transmissionResponse = null;
        bool disableSent = false;
        bool restorationAttempted = false;
        bool restorationCompleted = false;

        try
        {
            progress?.Report(new A276AcquisitionProgress(
                A276AcquisitionStage.ObservingBus,
                "Preserving initial traffic and identifying valid ALDL module addresses."));
            await Task.Delay(_options.InitialObservationWindow, cancellationToken).ConfigureAwait(false);

            if (!_observedAddresses.ContainsKey(A276MessageFactory.DeviceAddress))
            {
                outcome = A276AcquisitionOutcome.PcmNotObserved;
                detail = "No checksum-valid F4 PCM frame was observed, so the application sent no bus-control request.";
            }
            else
            {
                progress?.Report(new A276AcquisitionProgress(
                    A276AcquisitionStage.DisablingNormalCommunications,
                    "Sending the documented F4 Mode 8 request; no other observed module will be controlled."));
                disableSent = true;
                disableAcknowledgement = await SendControlAndWaitAsync(
                    connectedTransport,
                    A276MessageFactory.CreateDisableNormalCommunicationsRequest(),
                    expectedMode: 0x08,
                    cancellationToken).ConfigureAwait(false);

                if (disableAcknowledgement == A276ControlAcknowledgement.None)
                {
                    outcome = A276AcquisitionOutcome.DisableCommunicationsTimeout;
                    detail = "The documented F4 Mode 8 request was sent, but no acknowledgement or indistinguishable exact echo was received.";
                }
                else
                {
                    progress?.Report(new A276AcquisitionProgress(
                        A276AcquisitionStage.RequestingIdentity,
                        "Requesting documented A276 Mode 1 Message 4 identity data."));
                    identityResponse = await SendDatasetAndWaitAsync(
                        connectedTransport,
                        requestId: "a276-message-4-identity",
                        datasetId: 4,
                        cancellationToken).ConfigureAwait(false);

                    if (identityResponse is null)
                    {
                        outcome = A276AcquisitionOutcome.IdentityTimeout;
                        detail = "The PCM did not return a valid documented Message 4 response before the explicit timeout.";
                    }
                    else
                    {
                        progress?.Report(new A276AcquisitionProgress(
                            A276AcquisitionStage.RequestingTransmissionData,
                            "Requesting documented A276 Mode 1 Message 1 transmission data."));
                        transmissionResponse = await SendDatasetAndWaitAsync(
                            connectedTransport,
                            requestId: "a276-message-1-transmission",
                            datasetId: 1,
                            cancellationToken).ConfigureAwait(false);

                        if (transmissionResponse is null)
                        {
                            outcome = A276AcquisitionOutcome.TransmissionDataTimeout;
                            detail = "The PCM did not return a valid documented Message 1 response before the explicit timeout.";
                        }
                        else
                        {
                            outcome = A276AcquisitionOutcome.Completed;
                            detail = disableAcknowledgement == A276ControlAcknowledgement.Explicit
                                ? "Identity and transmission snapshots were correlated to their documentary A276 requests."
                                : "Identity and transmission snapshots were received; the Mode 8 acknowledgement was indistinguishable from a cable echo and remains vehicle-verification evidence.";
                        }
                    }
                }
            }
        }
        finally
        {
            if (disableSent)
            {
                restorationAttempted = true;
                progress?.Report(new A276AcquisitionProgress(
                    A276AcquisitionStage.RestoringNormalCommunications,
                    "Attempting documented Mode 9 and Mode 0 restoration before releasing the transport."));
                restorationCompleted = await RestoreNormalCommunicationsAsync(connectedTransport).ConfigureAwait(false);
            }

            readCancellation.Cancel();
            try
            {
                await readTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (readCancellation.IsCancellationRequested)
            {
            }
        }

        progress?.Report(new A276AcquisitionProgress(
            outcome == A276AcquisitionOutcome.Completed
                ? A276AcquisitionStage.Completed
                : A276AcquisitionStage.Incomplete,
            detail));

        return new A276AcquisitionResult(
            outcome,
            detail,
            _observedAddresses.Keys.Order().ToArray(),
            disableAcknowledgement,
            identityResponse,
            transmissionResponse,
            restorationAttempted,
            restorationCompleted,
            Interlocked.Read(ref _receivedChunkCount),
            Interlocked.Read(ref _receivedByteCount),
            Interlocked.Read(ref _validFrameCount),
            Interlocked.Read(ref _checksumFailureCount),
            Interlocked.Read(ref _invalidLengthCount),
            Interlocked.Read(ref _noiseByteCount),
            Interlocked.Read(ref _exactEchoCount),
            ReadOptionalTimestamp(ref _firstDataTimestamp),
            ReadOptionalTimestamp(ref _lastDataTimestamp),
            _transmissionObservations.ToArray());
    }

    private async Task ReadTransportAsync(ITransport transport, CancellationToken cancellationToken)
    {
        Exception? completionException = null;
        try
        {
            await foreach (TransportChunk chunk in transport.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (chunk.Kind == TransportChunkKind.Data)
                {
                    ProcessData(chunk);
                }
                else if (chunk.Kind is TransportChunkKind.Disconnected or TransportChunkKind.Error)
                {
                    completionException = new IOException(chunk.Diagnostics?.Detail ?? "The transport ended during A276 acquisition.");
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            completionException = exception;
        }
        finally
        {
            _inbound.Writer.TryComplete(completionException);
        }
    }

    private void ProcessData(TransportChunk chunk)
    {
        Interlocked.Increment(ref _receivedChunkCount);
        Interlocked.Add(ref _receivedByteCount, chunk.Bytes.Length);
        Interlocked.Exchange(ref _lastInboundTimestamp, chunk.MonotonicTimestamp);
        SetFirstTimestamp(chunk.MonotonicTimestamp);
        Interlocked.Exchange(ref _lastDataTimestamp, chunk.MonotonicTimestamp);

        AldlEchoFilterResult filtered;
        lock (_echoGate)
        {
            filtered = _echoFilter.Process(chunk.Bytes.Span, chunk.MonotonicTimestamp);
        }

        if (filtered.ExactEchoCompleted)
        {
            Interlocked.Increment(ref _exactEchoCount);
            _inbound.Writer.TryWrite(new InboundObservation(null, true, chunk.MonotonicTimestamp));
        }

        lock (_parserGate)
        {
            foreach (AldlParseResult parseResult in _parser.Push(filtered.VehicleBytes.Span))
            {
                ObserveParseResult(parseResult, chunk.MonotonicTimestamp);
            }
        }
    }

    private async Task<A276ControlAcknowledgement> SendControlAndWaitAsync(
        ITransport transport,
        byte[] request,
        byte expectedMode,
        CancellationToken cancellationToken)
    {
        ExpectEcho(request);
        await transport.WriteAsync(request, cancellationToken).ConfigureAwait(false);
        bool exactEchoObserved = false;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.ResponseTimeout);
        try
        {
            while (await _inbound.Reader.WaitToReadAsync(timeout.Token).ConfigureAwait(false))
            {
                while (_inbound.Reader.TryRead(out InboundObservation? observation))
                {
                    exactEchoObserved |= observation.ExactEcho;
                    if (observation.Frame is { } frame &&
                        frame.DeviceAddress == A276MessageFactory.DeviceAddress &&
                        frame.Mode == expectedMode &&
                        frame.Payload.IsEmpty &&
                        frame.ChecksumValid)
                    {
                        return A276ControlAcknowledgement.Explicit;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
        catch (ChannelClosedException)
        {
        }

        return exactEchoObserved
            ? A276ControlAcknowledgement.AmbiguousEchoOrAcknowledgement
            : A276ControlAcknowledgement.None;
    }

    private async Task<AldlFrame?> SendDatasetAndWaitAsync(
        ITransport transport,
        string requestId,
        byte datasetId,
        CancellationToken cancellationToken)
    {
        byte[] request = A276MessageFactory.CreateMode1Request(datasetId);
        ExpectEcho(request);
        long writeTimestamp = Interlocked.Read(ref _lastInboundTimestamp);
        await transport.WriteAsync(request, cancellationToken).ConfigureAwait(false);

        var correlator = new AldlRequestCorrelator();
        correlator.Register(new AldlOutstandingRequest(requestId, datasetId, writeTimestamp));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.ResponseTimeout);
        try
        {
            while (await _inbound.Reader.WaitToReadAsync(timeout.Token).ConfigureAwait(false))
            {
                while (_inbound.Reader.TryRead(out InboundObservation? observation))
                {
                    if (observation.Frame is not { } frame)
                    {
                        continue;
                    }

                    AldlCorrelationResult correlation = correlator.Correlate(frame, observation.Timestamp);
                    if (correlation.Matched)
                    {
                        return frame;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
        catch (ChannelClosedException)
        {
        }

        return null;
    }

    private async Task<bool> RestoreNormalCommunicationsAsync(ITransport transport)
    {
        try
        {
            _ = await SendControlAndWaitAsync(
                transport,
                A276MessageFactory.CreateEnableNormalCommunicationsRequest(),
                expectedMode: 0x09,
                CancellationToken.None).ConfigureAwait(false);

            using var returnToNormalTimeout = new CancellationTokenSource(_options.ResponseTimeout);
            byte[] returnToNormal = A276MessageFactory.CreateReturnToNormalModeRequest();
            ExpectEcho(returnToNormal);
            await transport.WriteAsync(returnToNormal, returnToNormalTimeout.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or InvalidOperationException)
        {
            return false;
        }
    }

    private void ExpectEcho(ReadOnlySpan<byte> request)
    {
        long lastTimestamp = Interlocked.Read(ref _lastInboundTimestamp);
        long basis = lastTimestamp == 0 ? MonotonicTicks() : lastTimestamp;
        lock (_echoGate)
        {
            ReadOnlyMemory<byte> pending = _echoFilter.Cancel();
            if (!pending.IsEmpty)
            {
                lock (_parserGate)
                {
                    foreach (AldlParseResult parseResult in _parser.Push(pending.Span))
                    {
                        ObserveParseResult(parseResult, basis);
                    }
                }
            }

            _echoFilter.Expect(request, checked(basis + _options.EchoWindow.Ticks));
        }
    }

    private void ObserveParseResult(AldlParseResult parseResult, long timestamp)
    {
        switch (parseResult.Disposition)
        {
            case AldlParseDisposition.ValidFrame:
                Interlocked.Increment(ref _validFrameCount);
                AldlFrame frame = parseResult.Frame
                    ?? throw new InvalidDataException("A valid ALDL parse result did not include its frame.");
                _observedAddresses.TryAdd(frame.DeviceAddress, frame.DeviceAddress);
                if (A276MessageFactory.TryIdentifyMode1Dataset(frame, out byte datasetId) && datasetId == 1)
                {
                    _transmissionObservations.Enqueue(new A276TransmissionObservation(
                        timestamp,
                        A276TransmissionDecoder.DecodeMode1Message1(frame)));
                }

                _inbound.Writer.TryWrite(new InboundObservation(frame, false, timestamp));
                break;
            case AldlParseDisposition.InvalidChecksum:
                Interlocked.Increment(ref _checksumFailureCount);
                break;
            case AldlParseDisposition.InvalidLength:
                Interlocked.Increment(ref _invalidLengthCount);
                break;
            case AldlParseDisposition.Noise:
                Interlocked.Add(ref _noiseByteCount, parseResult.RawBytes.Length);
                break;
            default:
                throw new InvalidOperationException($"Unsupported ALDL parse disposition: {parseResult.Disposition}.");
        }
    }

    private void SetFirstTimestamp(long timestamp)
    {
        _ = Interlocked.CompareExchange(ref _firstDataTimestamp, timestamp, long.MinValue);
    }

    private static long? ReadOptionalTimestamp(ref long location)
    {
        long value = Interlocked.Read(ref location);
        return value == long.MinValue ? null : value;
    }

    private static long MonotonicTicks() => Stopwatch.GetElapsedTime(0, Stopwatch.GetTimestamp()).Ticks;

    private sealed record InboundObservation(AldlFrame? Frame, bool ExactEcho, long Timestamp);
}
