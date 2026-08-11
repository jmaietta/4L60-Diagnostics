using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LT1Diagnostics.Acquisition.A276;
using LT1Diagnostics.Acquisition.RawSessions;
using LT1Diagnostics.Acquisition.Recording;
using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Protocol.Aldl;
using LT1Diagnostics.Simulator;
using LT1Diagnostics.Transport.Abstractions;

namespace LT1Diagnostics.Replay.Tests;

public sealed class A276AcquisitionCoordinatorTests
{
    private static readonly A276AcquisitionOptions TestOptions = new(
        InitialObservationWindow: TimeSpan.FromMilliseconds(10),
        ResponseTimeout: TimeSpan.FromMilliseconds(100),
        EchoWindow: TimeSpan.FromMilliseconds(25));

    [Fact]
    public async Task DocumentarySnapshotRecordsAndCorrelatesReadOnlySequence()
    {
        var simulator = new A276SnapshotSimulatorTransport();
        using var stream = new MemoryStream();
        await using var writer = new RawSessionWriter(stream, leaveOpen: true);
        await using var recording = new RecordingTransport(simulator, writer);
        TransportDevice device = Assert.Single(await recording.DiscoverAsync(CancellationToken.None));
        await recording.ConnectAsync(device, new TransportSettings(), CancellationToken.None);

        A276AcquisitionResult result = await new A276AcquisitionCoordinator(TestOptions)
            .AcquireSnapshotAsync(recording);
        await recording.DisconnectAsync(CancellationToken.None);

        Assert.True(result.IsComplete);
        Assert.Equal(A276AcquisitionOutcome.Completed, result.Outcome);
        Assert.Equal(A276ControlAcknowledgement.Explicit, result.DisableAcknowledgement);
        Assert.Equal(45, result.IdentityResponse?.Payload.Length);
        Assert.Equal(46, result.TransmissionResponse?.Payload.Length);
        Assert.True(result.RestorationAttempted);
        Assert.True(result.RestorationCompleted);
        Assert.Contains(A276MessageFactory.DeviceAddress, result.ObservedModuleAddresses);

        byte[][] expectedRequests =
        [
            A276MessageFactory.CreateDisableNormalCommunicationsRequest(),
            A276MessageFactory.CreateMode1Request(4),
            A276MessageFactory.CreateMode1Request(1),
            A276MessageFactory.CreateEnableNormalCommunicationsRequest(),
            A276MessageFactory.CreateReturnToNormalModeRequest(),
        ];
        Assert.Equal(expectedRequests.Length, simulator.TransmittedRequests.Count);
        for (int index = 0; index < expectedRequests.Length; index++)
        {
            Assert.Equal(expectedRequests[index], simulator.TransmittedRequests[index]);
        }

        var records = new List<RawSessionRecord>();
        await foreach (RawSessionRecord record in new RawSessionReader(stream).ReadAllAsync())
        {
            records.Add(record);
        }

        Assert.Equal(expectedRequests.Length, records.Count(record => record.KnownType == RawSessionRecordType.BytesTransmitted));
        Assert.True(records.Count(record => record.KnownType == RawSessionRecordType.BytesReceived) >= expectedRequests.Length);
        Assert.All(records, record => Assert.Equal(RawSessionIntegrityStatus.Valid, record.IntegrityStatus));
    }

    [Fact]
    public async Task NoObservedPcmMeansNoRequestIsTransmitted()
    {
        await using var simulator = new A276SnapshotSimulatorTransport(new A276SnapshotSimulatorOptions
        {
            IncludeInitialPcmTraffic = false,
        });
        TransportDevice device = Assert.Single(await simulator.DiscoverAsync(CancellationToken.None));
        await simulator.ConnectAsync(device, new TransportSettings(), CancellationToken.None);

        A276AcquisitionResult result = await new A276AcquisitionCoordinator(TestOptions)
            .AcquireSnapshotAsync(simulator);

        Assert.Equal(A276AcquisitionOutcome.PcmNotObserved, result.Outcome);
        Assert.Empty(simulator.TransmittedRequests);
        Assert.Contains((byte)0xEA, result.ObservedModuleAddresses);
        Assert.False(result.RestorationAttempted);
    }

    [Fact]
    public async Task IdentityTimeoutStillRestoresNormalCommunications()
    {
        var simulator = new A276SnapshotSimulatorTransport(new A276SnapshotSimulatorOptions
        {
            DroppedDatasetResponse = 4,
        });
        await using (simulator)
        {
            TransportDevice device = Assert.Single(await simulator.DiscoverAsync(CancellationToken.None));
            await simulator.ConnectAsync(device, new TransportSettings(), CancellationToken.None);

            A276AcquisitionResult result = await new A276AcquisitionCoordinator(TestOptions)
                .AcquireSnapshotAsync(simulator);

            Assert.Equal(A276AcquisitionOutcome.IdentityTimeout, result.Outcome);
            Assert.True(result.RestorationAttempted);
            Assert.True(result.RestorationCompleted);
            Assert.Contains(
                simulator.TransmittedRequests,
                request => request.SequenceEqual(A276MessageFactory.CreateEnableNormalCommunicationsRequest()));
            Assert.Contains(
                simulator.TransmittedRequests,
                request => request.SequenceEqual(A276MessageFactory.CreateReturnToNormalModeRequest()));
            Assert.DoesNotContain(
                simulator.TransmittedRequests,
                request => request.SequenceEqual(A276MessageFactory.CreateMode1Request(1)));
        }
    }

    [Fact]
    public async Task CorruptedIdentityResponseIsRejectedAndRestorationStillRuns()
    {
        await using var simulator = new A276SnapshotSimulatorTransport(new A276SnapshotSimulatorOptions
        {
            CorruptedDatasetResponse = 4,
        });
        TransportDevice device = Assert.Single(await simulator.DiscoverAsync(CancellationToken.None));
        await simulator.ConnectAsync(device, new TransportSettings(), CancellationToken.None);

        A276AcquisitionResult result = await new A276AcquisitionCoordinator(TestOptions)
            .AcquireSnapshotAsync(simulator);

        Assert.Equal(A276AcquisitionOutcome.IdentityTimeout, result.Outcome);
        Assert.True(result.ChecksumFailureCount > 0);
        Assert.True(result.RestorationCompleted);
        Assert.Contains(
            simulator.TransmittedRequests,
            request => request.SequenceEqual(A276MessageFactory.CreateReturnToNormalModeRequest()));
    }

    [Fact]
    public void OptionsRequireExplicitPositiveTiming()
    {
        var invalid = new A276AcquisitionOptions(TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        Assert.Throws<ArgumentOutOfRangeException>(() => new A276AcquisitionCoordinator(invalid));
    }

    [Fact]
    public async Task LateIdentityResponseTimesOutAndRestorationStillRuns()
    {
        await using var transport = new LateIdentityResponseTransport(identityDelay: TimeSpan.FromMilliseconds(150));
        TransportDevice device = Assert.Single(await transport.DiscoverAsync(CancellationToken.None));
        await transport.ConnectAsync(device, new TransportSettings(), CancellationToken.None);

        A276AcquisitionResult result = await new A276AcquisitionCoordinator(TestOptions)
            .AcquireSnapshotAsync(transport);

        Assert.Equal(A276AcquisitionOutcome.IdentityTimeout, result.Outcome);
        Assert.Null(result.IdentityResponse);
        Assert.True(result.RestorationAttempted);
        Assert.True(result.RestorationCompleted);
        Assert.DoesNotContain(
            transport.TransmittedRequests,
            request => request.SequenceEqual(A276MessageFactory.CreateMode1Request(1)));
        Assert.Contains(
            transport.TransmittedRequests,
            request => request.SequenceEqual(A276MessageFactory.CreateEnableNormalCommunicationsRequest()));
        Assert.Contains(
            transport.TransmittedRequests,
            request => request.SequenceEqual(A276MessageFactory.CreateReturnToNormalModeRequest()));
    }

    private sealed class LateIdentityResponseTransport(TimeSpan identityDelay) : ITransport
    {
        private readonly Lock _gate = new();
        private readonly List<byte[]> _transmittedRequests = [];
        private Channel<TransportChunk>? _chunks;
        private long _timestamp;
        private bool _connected;
        private bool _disposed;

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

        public string TransportId => "late-identity-response";

        public TransportCapabilities Capabilities =>
            TransportCapabilities.Discovery | TransportCapabilities.Read | TransportCapabilities.Write;

        public Task<IReadOnlyList<TransportDevice>> DiscoverAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<TransportDevice> devices =
            [
                new("late-identity-response", "Late identity response test transport"),
            ];
            return Task.FromResult(devices);
        }

        public Task ConnectAsync(
            TransportDevice device,
            TransportSettings settings,
            CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_connected)
                {
                    throw new InvalidOperationException("The transport is already connected.");
                }

                _chunks = Channel.CreateUnbounded<TransportChunk>(new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false,
                });
                _timestamp = TimeSpan.FromMilliseconds(1).Ticks;
                _connected = true;

                for (int index = 0; index < 3; index++)
                {
                    QueueData(AldlFrameBuilder.Build(A276MessageFactory.DeviceAddress, 0x01, new byte[46]));
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
                if (!_connected || _chunks is null)
                {
                    throw new InvalidOperationException("The transport is not connected.");
                }

                byte[] request = data.ToArray();
                _transmittedRequests.Add(request);

                if (!AldlFrameBuilder.TryParse(request, out AldlFrame? frame) ||
                    frame is null ||
                    !frame.ChecksumValid ||
                    frame.DeviceAddress != A276MessageFactory.DeviceAddress)
                {
                    return ValueTask.CompletedTask;
                }

                switch (frame.Mode)
                {
                    case 0x08 or 0x09:
                        QueueData(request);
                        QueueData(request);
                        break;
                    case 0x01 when frame.Payload.Length == 1 && frame.Payload.Span[0] == 4:
                        QueueData(request);
                        byte[] lateResponse = AldlFrameBuilder.Build(
                            A276MessageFactory.DeviceAddress,
                            0x01,
                            new byte[A276MessageFactory.GetMode1DataByteCount(4)]);
                        _ = QueueAfterDelayAsync(lateResponse, identityDelay);
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
                if (!_connected || _chunks is null)
                {
                    throw new InvalidOperationException("The transport is not connected.");
                }

                reader = _chunks.Reader;
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

        private async Task QueueAfterDelayAsync(byte[] bytes, TimeSpan delay)
        {
            await Task.Delay(delay).ConfigureAwait(false);
            lock (_gate)
            {
                if (_connected && _chunks is not null)
                {
                    QueueData(bytes);
                }
            }
        }

        private void QueueData(byte[] bytes)
        {
            _timestamp = checked(_timestamp + TimeSpan.FromMilliseconds(1).Ticks);
            _chunks!.Writer.TryWrite(new TransportChunk(
                bytes,
                _timestamp,
                DateTimeOffset.UnixEpoch + TimeSpan.FromTicks(_timestamp),
                TransportChunkKind.Data,
                new TransportDiagnostics(
                    TransportQuality.SimulatedFault,
                    "Synthetic late-response traffic; not vehicle evidence.",
                    FirstByteTimestamp: _timestamp,
                    LastByteTimestamp: _timestamp)));
        }
    }
}
