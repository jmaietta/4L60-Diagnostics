using LT1Diagnostics.Acquisition.A276;
using LT1Diagnostics.Acquisition.RawSessions;
using LT1Diagnostics.Acquisition.Recording;
using LT1Diagnostics.Protocol.A276;
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
}
