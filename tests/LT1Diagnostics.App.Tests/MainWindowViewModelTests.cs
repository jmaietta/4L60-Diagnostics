using System.Runtime.CompilerServices;
using LT1Diagnostics.App.ViewModels;
using LT1Diagnostics.Simulator;
using LT1Diagnostics.Transport.Abstractions;

namespace LT1Diagnostics.App.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task SimulatorDrivesLiveMetricsAndCanDisconnect()
    {
        await using var viewModel = CreateViewModel(
            static () => new FakeTransport([]),
            static () => new A276SnapshotSimulatorTransport());

        await viewModel.SimulatorCommand.ExecuteAsync();

        Assert.True(viewModel.HasActiveTransport);
        Assert.True(viewModel.IsConnected);
        Assert.Equal("Snapshot ready", viewModel.ConnectionStatus);
        Assert.Equal("READY", viewModel.LinkBadgeStatus);
        Assert.Equal("RESTORED", viewModel.SerialLinkStatus);
        Assert.Equal("CLEAN", viewModel.QualityStatus);
        Assert.True(viewModel.ReceivedChunkCount > 0);
        Assert.True(viewModel.ReceivedByteCount > 0);
        Assert.True(viewModel.ValidProtocolFrameCount >= 4);
        Assert.Equal(0, viewModel.ChecksumFailureCount);
        Assert.Equal("SNAPSHOT", viewModel.ProtocolDecodeStatus);
        Assert.NotEqual("--", viewModel.ChunkRateText);
        Assert.Equal("memory.lt1raw", viewModel.RawSessionPath);
        Assert.NotNull(viewModel.LatestTransmissionSample);
        Assert.True(viewModel.HasTransmissionTimeline);
        Assert.Equal("7", viewModel.SessionSampleCountText);
        Assert.NotEqual("--", viewModel.EngineSpeedText);

        await viewModel.DisconnectCommand.ExecuteAsync();

        Assert.False(viewModel.HasActiveTransport);
        Assert.False(viewModel.IsConnected);
        Assert.Equal("Offline", viewModel.ConnectionStatus);
    }

    [Fact]
    public async Task WorkspaceNavigationIsClickableAndRetainsSnapshotState()
    {
        await using var viewModel = CreateViewModel(
            static () => new FakeTransport([]),
            static () => new A276SnapshotSimulatorTransport());

        Assert.True(viewModel.IsOverviewPage);

        viewModel.NavigateGuideCommand.Execute(null);
        Assert.True(viewModel.IsGuidePage);

        viewModel.NavigateOverviewCommand.Execute(null);
        Assert.True(viewModel.IsOverviewPage);

        viewModel.NavigateConnectCommand.Execute(null);
        Assert.True(viewModel.IsConnectPage);
        await viewModel.SimulatorCommand.ExecuteAsync();

        viewModel.NavigateTransmissionCommand.Execute(null);
        Assert.True(viewModel.IsTransmissionPage);
        Assert.True(viewModel.HasTransmissionData);

        viewModel.NavigateTroubleCodesCommand.Execute(null);
        Assert.True(viewModel.IsTroubleCodesPage);

        viewModel.NavigateSessionsCommand.Execute(null);
        Assert.True(viewModel.IsSessionsPage);

        viewModel.NavigateReportsCommand.Execute(null);
        Assert.True(viewModel.IsReportsPage);
    }

    [Fact]
    public async Task GuidedDemoLoadsDataAndOpensMeasurements()
    {
        await using var viewModel = CreateViewModel(
            static () => new FakeTransport([]),
            static () => new A276SnapshotSimulatorTransport());

        await viewModel.RunDemoCommand.ExecuteAsync();

        Assert.True(viewModel.IsLiveDataPage);
        Assert.True(viewModel.IsDemoSession);
        Assert.Equal("View demo results  →", viewModel.DemoButtonText);
        Assert.True(viewModel.HasTransmissionData);
        Assert.Equal("Snapshot ready", viewModel.ConnectionStatus);
    }

    [Fact]
    public async Task GuidedDemoWorksThroughTheButtonCommandInterface()
    {
        await using var viewModel = CreateViewModel(
            static () => new FakeTransport([]),
            static () => new A276SnapshotSimulatorTransport());

        viewModel.RunDemoCommand.Execute(null);

        await WaitUntilAsync(() => viewModel.IsDemoSession);

        Assert.True(viewModel.IsLiveDataPage);
        Assert.Equal("Snapshot ready", viewModel.ConnectionStatus);
        Assert.NotNull(viewModel.LatestTransmissionSample);
    }

    [Fact]
    public async Task DiscoveryPopulatesSelectionAndPhysicalTransportLifecycle()
    {
        var createdTransports = new List<FakeTransport>();
        ITransport CreateSerialTransport()
        {
            var transport = new FakeTransport(
            [
                new TransportDevice("COM4", "Workshop ALDL — COM4"),
                new TransportDevice("COM9", "Bench interface — COM9"),
            ], holdReadOpen: true);
            createdTransports.Add(transport);
            return transport;
        }

        await using var viewModel = CreateViewModel(
            CreateSerialTransport,
            static () => new FakeTransport([]));

        await viewModel.DiscoverCommand.ExecuteAsync();

        Assert.Equal(2, viewModel.DiscoveredDevices.Count);
        Assert.Equal("COM4", viewModel.SelectedDevice?.Id);
        Assert.True(viewModel.CanConnect);
        Assert.True(viewModel.ShowConnectAction);
        Assert.Contains("2 cables were found", viewModel.CableDiscoveryMessage, StringComparison.Ordinal);

        viewModel.SelectedDevice = viewModel.DiscoveredDevices[1];
        await viewModel.ConnectCommand.ExecuteAsync();

        FakeTransport connectedTransport = createdTransports[1];
        Assert.True(connectedTransport.ConnectCalled);
        Assert.Equal("COM9", connectedTransport.ConnectedDevice?.Id);
        Assert.True(viewModel.HasActiveTransport);
        Assert.Equal("Bench interface — COM9", viewModel.DeviceSummary);
        Assert.Equal("Incomplete", viewModel.ConnectionStatus);

        await viewModel.DisconnectCommand.ExecuteAsync();

        Assert.True(connectedTransport.DisconnectCalled);
        Assert.True(connectedTransport.DisposeCalled);
        Assert.False(viewModel.HasActiveTransport);
    }

    [Fact]
    public async Task EmptyCableScanExplainsTheResultAndHidesConnectAction()
    {
        await using var viewModel = CreateViewModel(
            static () => new FakeTransport([]),
            static () => new FakeTransport([]));

        await viewModel.DiscoverCommand.ExecuteAsync();

        Assert.Empty(viewModel.DiscoveredDevices);
        Assert.False(viewModel.ShowConnectAction);
        Assert.False(viewModel.CanConnect);
        Assert.Equal("NOT FOUND", viewModel.InterfaceStatus);
        Assert.Equal("Scan again", viewModel.FindCablesButtonText);
        Assert.Contains("No diagnostic cable was detected", viewModel.CableDiscoveryMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoveryFailureProducesActionableFaultState()
    {
        await using var viewModel = CreateViewModel(
            static () => new FakeTransport([], discoveryException: new UnauthorizedAccessException("Serial access denied.")),
            static () => new FakeTransport([]));

        await viewModel.DiscoverCommand.ExecuteAsync();

        Assert.Equal("Unavailable", viewModel.ConnectionStatus);
        Assert.Equal("FAULT", viewModel.LinkBadgeStatus);
        Assert.Equal("Serial access denied.", viewModel.DeviceSummary);
        Assert.Contains("Cable scan failed", viewModel.CableDiscoveryMessage, StringComparison.Ordinal);
        Assert.False(viewModel.HasActiveTransport);
    }

    [Fact]
    public async Task BrowseSessionReplaysValidRawFileAndOpensMeasurements()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.lt1raw");
        try
        {
            await WriteTransmissionSessionAsync(path);
            await using var viewModel = CreateViewModel(
                static () => new FakeTransport([]),
                static () => new FakeTransport([]),
                () => Task.FromResult<string?>(path));

            await viewModel.BrowseSessionCommand.ExecuteAsync();

            Assert.True(viewModel.IsReplaySession);
            Assert.True(viewModel.IsLiveDataPage);
            Assert.Equal("Replay complete", viewModel.ReplayStatus);
            Assert.Equal("Replay ready", viewModel.ConnectionStatus);
            Assert.Equal("CLEAN", viewModel.QualityStatus);
            Assert.Equal(path, viewModel.RawSessionPath);
            Assert.NotNull(viewModel.LatestTransmissionSample);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReplayedTroubleCodeShowsExplanationRankedCausesAndNextCheck()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.lt1raw");
        try
        {
            await WriteTransmissionSessionAsync(path, data => data[2] = 1 << 3);
            await using var viewModel = CreateViewModel(
                static () => new FakeTransport([]),
                static () => new FakeTransport([]),
                () => Task.FromResult<string?>(path));

            await viewModel.BrowseSessionCommand.ExecuteAsync();

            DtcDisplayItem displayed = Assert.Single(viewModel.LoggedTransmissionDtcs);
            Assert.Equal(73, displayed.Code);
            Assert.Equal("Pressure-control-solenoid current fault", displayed.Title);
            Assert.Contains("Poor contact", displayed.LikelyCausesText, StringComparison.Ordinal);
            Assert.Contains("commanded", displayed.NextTest, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("DOCUMENTARY — VEHICLE CHECK PENDING", displayed.EvidenceStatus);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task BrowseSessionRejectsNonRawFileWithoutCrashing()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.lt1raw");
        try
        {
            await File.WriteAllBytesAsync(path, [0x01, 0x02, 0x03]);
            await using var viewModel = CreateViewModel(
                static () => new FakeTransport([]),
                static () => new FakeTransport([]),
                () => Task.FromResult<string?>(path));

            await viewModel.BrowseSessionCommand.ExecuteAsync();

            Assert.False(viewModel.IsReplaySession);
            Assert.Equal("Session could not be replayed", viewModel.ReplayStatus);
            Assert.Equal("Replay failed", viewModel.ConnectionStatus);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReportButtonWritesReadableHtmlForLoadedDemo()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        try
        {
            await using var viewModel = CreateViewModel(
                static () => new FakeTransport([]),
                static () => new A276SnapshotSimulatorTransport(),
                reportSavePicker: (_, _) => Task.FromResult<string?>(path));
            await viewModel.RunDemoCommand.ExecuteAsync();

            await viewModel.ExportReportCommand.ExecuteAsync();

            Assert.True(File.Exists(path));
            string report = await File.ReadAllTextAsync(path);
            Assert.Contains("Maietta Diagnostics", report, StringComparison.Ordinal);
            Assert.Contains("GM 4L60E diagnostic report", report, StringComparison.Ordinal);
            Assert.Contains("DEMO DATA", report, StringComparison.Ordinal);
            Assert.Contains("Recorded timeline", report, StringComparison.Ordinal);
            Assert.StartsWith("Report saved:", viewModel.ReportStatus, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static MainWindowViewModel CreateViewModel(
        Func<ITransport> serialTransportFactory,
        Func<ITransport> simulatorTransportFactory,
        Func<Task<string?>>? rawSessionFilePicker = null,
        Func<string, string, Task<string?>>? reportSavePicker = null) =>
        new(
            serialTransportFactory,
            simulatorTransportFactory,
            static () => new RawSessionTarget(
                new Acquisition.RawSessions.RawSessionWriter(new MemoryStream()),
                "memory.lt1raw"),
            new Acquisition.A276.A276AcquisitionOptions(
                TimeSpan.FromMilliseconds(5),
                TimeSpan.FromMilliseconds(10),
                TimeSpan.FromMilliseconds(5)),
            rawSessionFilePicker ?? (static () => Task.FromResult<string?>(null)),
            reportSavePicker);

    private static async Task WriteTransmissionSessionAsync(
        string path,
        Action<byte[]>? configureData = null)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
        await using var writer = new Acquisition.RawSessions.RawSessionWriter(stream);
        var data = new byte[Protocol.A276.A276MessageFactory.GetMode1DataByteCount(1)];
        configureData?.Invoke(data);
        byte[] response = Protocol.Aldl.AldlFrameBuilder.Build(
            Protocol.A276.A276MessageFactory.DeviceAddress,
            0x01,
            data);
        await writer.AppendAsync(
            Acquisition.RawSessions.RawSessionRecordType.BytesReceived,
            1,
            DateTimeOffset.UtcNow,
            Acquisition.RawSessions.RawSessionRecordAttributes.None,
            response);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class FakeTransport(
        IReadOnlyList<TransportDevice> devices,
        bool holdReadOpen = false,
        Exception? discoveryException = null) : ITransport
    {
        private readonly CancellationTokenSource _disconnectCancellation = new();

        public string TransportId => "test";

        public TransportCapabilities Capabilities =>
            TransportCapabilities.Discovery |
            TransportCapabilities.Read |
            TransportCapabilities.Write;

        public bool ConnectCalled { get; private set; }

        public bool DisconnectCalled { get; private set; }

        public bool DisposeCalled { get; private set; }

        public TransportDevice? ConnectedDevice { get; private set; }

        public Task<IReadOnlyList<TransportDevice>> DiscoverAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return discoveryException is null
                ? Task.FromResult(devices)
                : Task.FromException<IReadOnlyList<TransportDevice>>(discoveryException);
        }

        public Task ConnectAsync(
            TransportDevice device,
            TransportSettings settings,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConnectCalled = true;
            ConnectedDevice = device;
            return Task.CompletedTask;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public async IAsyncEnumerable<TransportChunk> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disconnectCancellation.Token);
            yield return new TransportChunk(
                ReadOnlyMemory<byte>.Empty,
                0,
                DateTimeOffset.UnixEpoch,
                TransportChunkKind.Connected);
            if (holdReadOpen)
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, linkedCancellation.Token);
                }
                catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
                {
                }
            }
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DisconnectCalled = true;
            _disconnectCancellation.Cancel();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalled = true;
            _disconnectCancellation.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
