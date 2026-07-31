using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using LT1Diagnostics.Acquisition.A276;
using LT1Diagnostics.Acquisition.RawSessions;
using LT1Diagnostics.Acquisition.Recording;
using LT1Diagnostics.Analysis;
using LT1Diagnostics.Domain.Diagnostics;
using LT1Diagnostics.Knowledge.Dtcs;
using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Reporting;
using LT1Diagnostics.Simulator;
using LT1Diagnostics.Transport.Abstractions;
using LT1Diagnostics.Transport.Serial;

namespace LT1Diagnostics.App.ViewModels;

public sealed record RawSessionTarget(RawSessionWriter Writer, string DisplayPath);

public sealed record SavedSessionItem(
    string FileName,
    string FullPath,
    string CapturedText,
    string SizeText);

public sealed record DtcDisplayItem(
    int Code,
    string Title,
    string PlainEnglishMeaning,
    string LikelyCausesText,
    string NextTest,
    string SafetyLevel,
    string EvidenceStatus,
    string Limitation);

public sealed record TransmissionTimelineItem(
    string ElapsedText,
    string VehicleSpeedText,
    string EngineSpeedText,
    string CommandedGearText,
    string SlipText);

public enum WorkspacePage
{
    Guide,
    Overview,
    Connect,
    TroubleCodes,
    LiveData,
    Transmission,
    Sessions,
    Reports,
}

public sealed class MainWindowViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly Func<ITransport> _serialTransportFactory;
    private readonly Func<ITransport> _simulatorTransportFactory;
    private readonly Func<RawSessionTarget> _rawSessionTargetFactory;
    private readonly Func<Task<string?>> _rawSessionFilePicker;
    private readonly Func<string, string, Task<string?>> _reportSavePicker;
    private readonly A276AcquisitionOptions _acquisitionOptions;
    private readonly DtcKnowledgeCatalog _dtcKnowledgeCatalog;
    private readonly string _dtcKnowledgeStatus;
    private ITransport? _activeTransport;
    private RawSessionWriter? _rawSessionWriter;
    private CancellationTokenSource? _acquisitionCancellation;
    private Task<A276AcquisitionResult>? _acquisitionTask;
    private IReadOnlyList<TransportDevice> _discoveredDevices = [];
    private TransportDevice? _selectedDevice;
    private string _connectionStatus = "Offline";
    private string _deviceSummary = "No diagnostic cable selected";
    private string _linkBadgeStatus = "STANDBY";
    private string _chunkRateText = "--";
    private string _serialLinkDetail = "Raw capture starts before acquisition";
    private string _serialLinkStatus = "IDLE";
    private string _interfaceStatus = "SELECT";
    private string _qualityStatus = "NO DATA";
    private string _protocolDecodeStatus = "WAITING";
    private string _protocolLinkDetail = "Awaiting a checksum-valid F4 PCM frame";
    private string _rawSessionPath = "Created when a link opens";
    private bool _isConnected;
    private bool _isBusy;
    private bool _isDemoSession;
    private bool _isReplaySession;
    private bool _isSyntheticReplay;
    private bool _disposed;
    private long _receivedChunkCount;
    private long _receivedByteCount;
    private long _validProtocolFrameCount;
    private long _checksumFailureCount;
    private A276TransmissionSample? _latestTransmissionSample;
    private IReadOnlyList<TransmissionTimelineItem> _transmissionTimeline = [];
    private TransmissionSessionAnalysis _sessionAnalysis = TransmissionSessionAnalyzer.Analyze([]);
    private IReadOnlyList<TransmissionObservation> _domainTransmissionObservations = [];
    private IReadOnlyList<SavedSessionItem> _savedSessions = [];
    private SavedSessionItem? _selectedSavedSession;
    private string _sessionLibraryStatus = "Select Saved sessions to load previous captures.";
    private string _replayStatus = "No replay loaded";
    private string _replayDetail = "Select a saved session to view its measurements again.";
    private string _reportStatus = "Load a session to create a report or export its measurements.";
    private string? _lastReportPath;
    private bool _hasScannedForDevices;
    private string _cableDiscoveryMessage = "Connect the USB diagnostic cable, then select Find connected cables.";
    private WorkspacePage _selectedPage = WorkspacePage.Overview;

    public MainWindowViewModel()
        : this(
            static () => new SerialPortTransport(),
            static () => new A276SnapshotSimulatorTransport(),
            CreateDefaultRawSessionTarget,
            CreateDefaultAcquisitionOptions(),
            static () => Task.FromResult<string?>(null))
    {
    }

    public MainWindowViewModel(Func<Task<string?>> rawSessionFilePicker)
        : this(
            static () => new SerialPortTransport(),
            static () => new A276SnapshotSimulatorTransport(),
            CreateDefaultRawSessionTarget,
            CreateDefaultAcquisitionOptions(),
            rawSessionFilePicker)
    {
    }

    public MainWindowViewModel(
        Func<Task<string?>> rawSessionFilePicker,
        Func<string, string, Task<string?>> reportSavePicker)
        : this(
            static () => new SerialPortTransport(),
            static () => new A276SnapshotSimulatorTransport(),
            CreateDefaultRawSessionTarget,
            CreateDefaultAcquisitionOptions(),
            rawSessionFilePicker,
            reportSavePicker)
    {
    }

    public MainWindowViewModel(
        Func<ITransport> serialTransportFactory,
        Func<ITransport> simulatorTransportFactory)
        : this(
            serialTransportFactory,
            simulatorTransportFactory,
            CreateDefaultRawSessionTarget,
            CreateDefaultAcquisitionOptions(),
            static () => Task.FromResult<string?>(null))
    {
    }

    public MainWindowViewModel(
        Func<ITransport> serialTransportFactory,
        Func<ITransport> simulatorTransportFactory,
        Func<RawSessionTarget> rawSessionTargetFactory,
        A276AcquisitionOptions acquisitionOptions)
        : this(
            serialTransportFactory,
            simulatorTransportFactory,
            rawSessionTargetFactory,
            acquisitionOptions,
            static () => Task.FromResult<string?>(null))
    {
    }

    public MainWindowViewModel(
        Func<ITransport> serialTransportFactory,
        Func<ITransport> simulatorTransportFactory,
        Func<RawSessionTarget> rawSessionTargetFactory,
        A276AcquisitionOptions acquisitionOptions,
        Func<Task<string?>> rawSessionFilePicker,
        Func<string, string, Task<string?>>? reportSavePicker = null)
    {
        ArgumentNullException.ThrowIfNull(serialTransportFactory);
        ArgumentNullException.ThrowIfNull(simulatorTransportFactory);
        ArgumentNullException.ThrowIfNull(rawSessionTargetFactory);
        ArgumentNullException.ThrowIfNull(acquisitionOptions);
        ArgumentNullException.ThrowIfNull(rawSessionFilePicker);
        acquisitionOptions.Validate();

        _serialTransportFactory = serialTransportFactory;
        _simulatorTransportFactory = simulatorTransportFactory;
        _rawSessionTargetFactory = rawSessionTargetFactory;
        _acquisitionOptions = acquisitionOptions;
        _rawSessionFilePicker = rawSessionFilePicker;
        _reportSavePicker = reportSavePicker ?? (static (_, _) => Task.FromResult<string?>(null));
        (_dtcKnowledgeCatalog, _dtcKnowledgeStatus) = LoadDefaultDtcKnowledge();

        DiscoverCommand = new AsyncCommand(DiscoverAsync, () => !IsBusy && !HasActiveTransport);
        ConnectCommand = new AsyncCommand(ConnectSelectedAsync, () => CanConnect);
        SimulatorCommand = new AsyncCommand(SelectSimulatorAsync, () => !IsBusy && !HasActiveTransport);
        DisconnectCommand = new AsyncCommand(DisconnectAsync, () => !IsBusy && HasActiveTransport);
        RunDemoCommand = new AsyncCommand(
            RunDemoAsync,
            () => !IsBusy && (!HasActiveTransport || IsDemoSession));
        NavigateOverviewCommand = new RelayCommand(() => SelectedPage = WorkspacePage.Overview);
        NavigateConnectCommand = new RelayCommand(() => SelectedPage = WorkspacePage.Connect);
        NavigateTroubleCodesCommand = new RelayCommand(() => SelectedPage = WorkspacePage.TroubleCodes);
        NavigateLiveDataCommand = new RelayCommand(() => SelectedPage = WorkspacePage.LiveData);
        NavigateTransmissionCommand = new RelayCommand(() => SelectedPage = WorkspacePage.Transmission);
        NavigateSessionsCommand = new RelayCommand(OpenSessionsPage);
        NavigateReportsCommand = new RelayCommand(() => SelectedPage = WorkspacePage.Reports);
        OpenSessionFolderCommand = new RelayCommand(
            OpenSessionFolder,
            () => SelectedSavedSession is not null || HasSavedSession);
        RefreshSessionsCommand = new RelayCommand(RefreshSavedSessions);
        ReplaySelectedSessionCommand = new AsyncCommand(
            ReplaySelectedSessionAsync,
            () => !IsBusy && !HasActiveTransport && SelectedSavedSession is not null);
        BrowseSessionCommand = new AsyncCommand(
            BrowseSessionAsync,
            () => !IsBusy && !HasActiveTransport);
        NavigateGuideCommand = new RelayCommand(() => SelectedPage = WorkspacePage.Guide);
        ExportReportCommand = new AsyncCommand(
            () => ExportReportAsync("html"),
            () => !IsBusy && HasTransmissionData);
        ExportCsvCommand = new AsyncCommand(
            () => ExportReportAsync("csv"),
            () => !IsBusy && HasTransmissionData);
        OpenReportFolderCommand = new RelayCommand(
            OpenReportFolder,
            () => _lastReportPath is not null && File.Exists(_lastReportPath));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AsyncCommand DiscoverCommand { get; }

    public AsyncCommand ConnectCommand { get; }

    public AsyncCommand SimulatorCommand { get; }

    public AsyncCommand DisconnectCommand { get; }

    public AsyncCommand RunDemoCommand { get; }

    public RelayCommand NavigateOverviewCommand { get; }

    public RelayCommand NavigateConnectCommand { get; }

    public RelayCommand NavigateTroubleCodesCommand { get; }

    public RelayCommand NavigateLiveDataCommand { get; }

    public RelayCommand NavigateTransmissionCommand { get; }

    public RelayCommand NavigateSessionsCommand { get; }

    public RelayCommand NavigateReportsCommand { get; }

    public RelayCommand OpenSessionFolderCommand { get; }

    public RelayCommand RefreshSessionsCommand { get; }

    public AsyncCommand ReplaySelectedSessionCommand { get; }

    public AsyncCommand BrowseSessionCommand { get; }

    public RelayCommand NavigateGuideCommand { get; }

    public AsyncCommand ExportReportCommand { get; }

    public AsyncCommand ExportCsvCommand { get; }

    public RelayCommand OpenReportFolderCommand { get; }

    public int SimulatorScenarioCount => SimulatorScenarioCatalog.All.Count;

    public IReadOnlyList<TransportDevice> DiscoveredDevices
    {
        get => _discoveredDevices;
        private set
        {
            if (SetField(ref _discoveredDevices, value))
            {
                OnPropertyChanged(nameof(HasDiscoveredDevices));
                OnPropertyChanged(nameof(ShowDeviceSummary));
                OnPropertyChanged(nameof(ShowDeviceSelector));
                OnPropertyChanged(nameof(ShowConnectAction));
            }
        }
    }

    public TransportDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (!SetField(ref _selectedDevice, value))
            {
                return;
            }

            if (value is not null)
            {
                DeviceSummary = value.DisplayName;
            }

            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(ShowConnectAction));
            NotifyCommandStates();
        }
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetField(ref _connectionStatus, value);
    }

    public string DeviceSummary
    {
        get => _deviceSummary;
        private set
        {
            if (SetField(ref _deviceSummary, value))
            {
                OnPropertyChanged(nameof(EvidenceLabel));
            }
        }
    }

    public string LinkBadgeStatus
    {
        get => _linkBadgeStatus;
        private set => SetField(ref _linkBadgeStatus, value);
    }

    public string ChunkRateText
    {
        get => _chunkRateText;
        private set => SetField(ref _chunkRateText, value);
    }

    public string SerialLinkDetail
    {
        get => _serialLinkDetail;
        private set => SetField(ref _serialLinkDetail, value);
    }

    public string SerialLinkStatus
    {
        get => _serialLinkStatus;
        private set => SetField(ref _serialLinkStatus, value);
    }

    public string InterfaceStatus
    {
        get => _interfaceStatus;
        private set => SetField(ref _interfaceStatus, value);
    }

    public string QualityStatus
    {
        get => _qualityStatus;
        private set => SetField(ref _qualityStatus, value);
    }

    public string ProtocolDecodeStatus
    {
        get => _protocolDecodeStatus;
        private set => SetField(ref _protocolDecodeStatus, value);
    }

    public string ProtocolLinkDetail
    {
        get => _protocolLinkDetail;
        private set => SetField(ref _protocolLinkDetail, value);
    }

    public string RawSessionPath
    {
        get => _rawSessionPath;
        private set
        {
            if (SetField(ref _rawSessionPath, value))
            {
                OnPropertyChanged(nameof(HasSavedSession));
                OnPropertyChanged(nameof(SessionFileName));
                OpenSessionFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public IReadOnlyList<SavedSessionItem> SavedSessions
    {
        get => _savedSessions;
        private set
        {
            if (SetField(ref _savedSessions, value))
            {
                OnPropertyChanged(nameof(HasSavedSessions));
                OnPropertyChanged(nameof(ShowNoSavedSessions));
            }
        }
    }

    public SavedSessionItem? SelectedSavedSession
    {
        get => _selectedSavedSession;
        set
        {
            if (SetField(ref _selectedSavedSession, value))
            {
                ReplaySelectedSessionCommand.RaiseCanExecuteChanged();
                OpenSessionFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SessionLibraryStatus
    {
        get => _sessionLibraryStatus;
        private set => SetField(ref _sessionLibraryStatus, value);
    }

    public string ReplayStatus
    {
        get => _replayStatus;
        private set => SetField(ref _replayStatus, value);
    }

    public string ReplayDetail
    {
        get => _replayDetail;
        private set => SetField(ref _replayDetail, value);
    }

    public long ValidProtocolFrameCount
    {
        get => _validProtocolFrameCount;
        private set => SetField(ref _validProtocolFrameCount, value);
    }

    public long ChecksumFailureCount
    {
        get => _checksumFailureCount;
        private set => SetField(ref _checksumFailureCount, value);
    }

    public long ReceivedChunkCount
    {
        get => _receivedChunkCount;
        private set => SetField(ref _receivedChunkCount, value);
    }

    public long ReceivedByteCount
    {
        get => _receivedByteCount;
        private set => SetField(ref _receivedByteCount, value);
    }

    public A276TransmissionSample? LatestTransmissionSample
    {
        get => _latestTransmissionSample;
        private set
        {
            if (!SetField(ref _latestTransmissionSample, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasTransmissionData));
            OnPropertyChanged(nameof(ShowNoTransmissionData));
            OnPropertyChanged(nameof(HasLoggedTransmissionDtcs));
            OnPropertyChanged(nameof(ShowNoLoggedTransmissionDtcs));
            OnPropertyChanged(nameof(LoggedTransmissionDtcs));
            OnPropertyChanged(nameof(SnapshotDisplayStatus));
            OnPropertyChanged(nameof(EvidenceLabel));
            OnPropertyChanged(nameof(EngineSpeedText));
            OnPropertyChanged(nameof(VehicleSpeedText));
            OnPropertyChanged(nameof(IgnitionVoltageText));
            OnPropertyChanged(nameof(CoolantTemperatureText));
            OnPropertyChanged(nameof(FluidTemperatureText));
            OnPropertyChanged(nameof(CommandedGearText));
            OnPropertyChanged(nameof(OutputSpeedText));
            OnPropertyChanged(nameof(TorqueSignalPressureText));
            OnPropertyChanged(nameof(ForceMotorCurrentText));
            OnPropertyChanged(nameof(ForceMotorDutyText));
            OnPropertyChanged(nameof(SlipText));
            OnPropertyChanged(nameof(OneTwoShiftTimeText));
            OnPropertyChanged(nameof(TwoThreeShiftTimeText));
            OnPropertyChanged(nameof(TccStatusText));
            OnPropertyChanged(nameof(SolenoidStatusText));
            OnPropertyChanged(nameof(TransmissionPromIdText));
            ExportReportCommand.RaiseCanExecuteChanged();
            ExportCsvCommand.RaiseCanExecuteChanged();
        }
    }

    public IReadOnlyList<TransmissionTimelineItem> TransmissionTimeline
    {
        get => _transmissionTimeline;
        private set
        {
            if (SetField(ref _transmissionTimeline, value))
            {
                OnPropertyChanged(nameof(HasTransmissionTimeline));
            }
        }
    }

    public bool HasTransmissionTimeline => TransmissionTimeline.Count > 1;

    public string SessionSampleCountText => _sessionAnalysis.SampleCount.ToString(CultureInfo.CurrentCulture);

    public string SessionDurationText => _sessionAnalysis.Duration.TotalSeconds.ToString("0.0", CultureInfo.CurrentCulture) + " s";

    public string SessionEventCountText => _sessionAnalysis.Events.Count.ToString(CultureInfo.CurrentCulture);

    public string SessionInterpretationBoundary => _sessionAnalysis.InterpretationBoundary;

    public string ReportStatus
    {
        get => _reportStatus;
        private set => SetField(ref _reportStatus, value);
    }

    public bool CanExportReport => HasTransmissionData;

    public WorkspacePage SelectedPage
    {
        get => _selectedPage;
        private set
        {
            if (!SetField(ref _selectedPage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsOverviewPage));
            OnPropertyChanged(nameof(IsGuidePage));
            OnPropertyChanged(nameof(IsConnectPage));
            OnPropertyChanged(nameof(IsTroubleCodesPage));
            OnPropertyChanged(nameof(IsLiveDataPage));
            OnPropertyChanged(nameof(IsTransmissionPage));
            OnPropertyChanged(nameof(IsSessionsPage));
            OnPropertyChanged(nameof(IsReportsPage));
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetField(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(CanSelectDevice));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanConnect));
                OnPropertyChanged(nameof(CanSelectDevice));
                OnPropertyChanged(nameof(DemoButtonText));
                OnPropertyChanged(nameof(FindCablesButtonText));
                OnPropertyChanged(nameof(ShowConnectAction));
                NotifyCommandStates();
            }
        }
    }

    public bool IsDemoSession
    {
        get => _isDemoSession;
        private set
        {
            if (SetField(ref _isDemoSession, value))
            {
                OnPropertyChanged(nameof(DemoButtonText));
                OnPropertyChanged(nameof(ShowVehicleConnectionHelp));
                OnPropertyChanged(nameof(ConnectionSourceDescription));
                OnPropertyChanged(nameof(ConnectionSourceIcon));
                OnPropertyChanged(nameof(DisconnectButtonText));
                OnPropertyChanged(nameof(EvidenceLabel));
                RunDemoCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsReplaySession
    {
        get => _isReplaySession;
        private set
        {
            if (SetField(ref _isReplaySession, value))
            {
                OnPropertyChanged(nameof(EvidenceLabel));
                OnPropertyChanged(nameof(HasReplayResult));
                OnPropertyChanged(nameof(ConnectionDisplayStatus));
            }
        }
    }

    public string DemoButtonText => IsBusy
        ? "Loading demo..."
        : IsDemoSession
            ? "View demo results  →"
            : "Start demo  →";

    public bool ShowVehicleConnectionHelp => !IsDemoSession;

    public string ConnectionSourceDescription => IsDemoSession
        ? "Synthetic sample data — no vehicle or cable is connected"
        : "Select the USB diagnostic cable connected to this computer";

    public string ConnectionSourceIcon => IsDemoSession ? "DEMO" : "USB";

    public string FindCablesButtonText => IsBusy && ConnectionStatus == "Scanning"
        ? "Looking for cables..."
        : _hasScannedForDevices
            ? "Scan again"
            : "Find connected cables";

    public string CableDiscoveryMessage
    {
        get => _cableDiscoveryMessage;
        private set => SetField(ref _cableDiscoveryMessage, value);
    }

    public string DisconnectButtonText => IsDemoSession ? "End demo" : "Disconnect cable";

    public string ConnectionDisplayStatus => HasActiveTransport
        ? "CONNECTED"
        : IsReplaySession
            ? "REPLAY LOADED"
            : "NOT CONNECTED";

    public string SnapshotDisplayStatus => LatestTransmissionSample is not null ? "READY" : "WAITING";

    public bool HasActiveTransport => _activeTransport is not null;

    public bool HasDiscoveredDevices => DiscoveredDevices.Count > 0;

    public bool ShowDeviceSummary => !HasDiscoveredDevices || HasActiveTransport;

    public bool ShowDeviceSelector => HasDiscoveredDevices && !HasActiveTransport;

    public bool ShowConnectionActions => !HasActiveTransport;

    public bool ShowConnectAction => ShowConnectionActions && SelectedDevice is not null;

    public bool CanSelectDevice => !IsBusy && !HasActiveTransport;

    public bool CanConnect => !IsBusy && !HasActiveTransport && SelectedDevice is not null;

    public bool IsOverviewPage => SelectedPage == WorkspacePage.Overview;

    public bool IsGuidePage => SelectedPage == WorkspacePage.Guide;

    public bool IsConnectPage => SelectedPage == WorkspacePage.Connect;

    public bool IsTroubleCodesPage => SelectedPage == WorkspacePage.TroubleCodes;

    public bool IsLiveDataPage => SelectedPage == WorkspacePage.LiveData;

    public bool IsTransmissionPage => SelectedPage == WorkspacePage.Transmission;

    public bool IsSessionsPage => SelectedPage == WorkspacePage.Sessions;

    public bool IsReportsPage => SelectedPage == WorkspacePage.Reports;

    public bool HasTransmissionData => LatestTransmissionSample is not null;

    public bool ShowNoTransmissionData => !HasTransmissionData;

    public bool HasLoggedTransmissionDtcs => LatestTransmissionSample?.LoggedTransmissionDtcs.Count > 0;

    public bool ShowNoLoggedTransmissionDtcs => HasTransmissionData && !HasLoggedTransmissionDtcs;

    public IReadOnlyList<DtcDisplayItem> LoggedTransmissionDtcs =>
        LatestTransmissionSample?.LoggedTransmissionDtcs
            .Select(CreateDtcDisplayItem)
            .ToArray()
        ?? [];

    public string DtcKnowledgeStatus => _dtcKnowledgeStatus;

    public bool HasSavedSession => File.Exists(RawSessionPath);

    public bool HasSavedSessions => SavedSessions.Count > 0;

    public bool ShowNoSavedSessions => !HasSavedSessions;

    public bool HasReplayResult => IsReplaySession;

    public string SessionFileName => HasSavedSession ? Path.GetFileName(RawSessionPath) : "No session captured yet";

    public string EvidenceLabel => IsDemoSession
        ? "DEMO DATA"
        : IsReplaySession
            ? _isSyntheticReplay
                ? "REPLAYED DEMO DATA"
                : "REPLAYED VEHICLE DATA — VALIDATION PENDING"
            : "VEHICLE VALIDATION PENDING";

    public string EngineSpeedText => FormatSample(sample => sample.EngineSpeedRpm, "0", " rpm");

    public string VehicleSpeedText => FormatSample(sample => sample.VehicleSpeedMph, "0.0", " mph");

    public string IgnitionVoltageText => FormatSample(sample => sample.TransmissionIgnitionVoltage, "0.0", " V");

    public string CoolantTemperatureText => FormatSample(sample => sample.CoolantTemperatureCelsius, "0.0", " °C");

    public string FluidTemperatureText => FormatSample(sample => sample.TransmissionFluidTemperatureCelsius, "0.0", " °C");

    public string CommandedGearText => LatestTransmissionSample is { } sample
        ? sample.CommandedGear.ToString(CultureInfo.InvariantCulture)
        : "--";

    public string OutputSpeedText => FormatSample(sample => sample.OutputSpeedRpm, "0", " rpm");

    public string TorqueSignalPressureText => FormatSample(sample => sample.CurrentTorqueSignalPressurePsi, "0.0", " psi");

    public string ForceMotorCurrentText => LatestTransmissionSample is { } sample
        ? $"{sample.ActualForceMotorCurrentAmps:0.00} / {sample.ReferenceForceMotorCurrentAmps:0.00} A"
        : "--";

    public string ForceMotorDutyText => FormatSample(sample => sample.ForceMotorDutyCyclePercent, "0.0", " %");

    public string SlipText => FormatSample(sample => sample.SlipRpm, "0.0", " rpm");

    public string OneTwoShiftTimeText => FormatSample(sample => sample.LatestOneTwoShiftTimeSeconds, "0.000", " s");

    public string TwoThreeShiftTimeText => FormatSample(sample => sample.LatestTwoThreeShiftTimeSeconds, "0.000", " s");

    public string TccStatusText => LatestTransmissionSample is { } sample
        ? $"Command {(sample.TccControlCommanded ? "ON" : "OFF")} · Enable {(sample.TccEnabled ? "ON" : "OFF")}"
        : "--";

    public string SolenoidStatusText => LatestTransmissionSample is { } sample
        ? $"A {(sample.ShiftSolenoidACommanded ? "ON" : "OFF")} · B {(sample.ShiftSolenoidBCommanded ? "ON" : "OFF")}"
        : "--";

    public string TransmissionPromIdText => LatestTransmissionSample is { } sample
        ? sample.TransmissionPromId.ToString(CultureInfo.InvariantCulture)
        : "--";

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await ReleaseTransportAsync(updateInterface: false).ConfigureAwait(true);
        GC.SuppressFinalize(this);
    }

    private void OpenSessionsPage()
    {
        RefreshSavedSessions();
        SelectedPage = WorkspacePage.Sessions;
    }

    private void RefreshSavedSessions()
    {
        try
        {
            string directory = GetDefaultSessionDirectory();
            IEnumerable<string> paths = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "*.lt1raw", SearchOption.TopDirectoryOnly)
                : [];
            SavedSessionItem[] sessions = paths
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Select(file => new SavedSessionItem(
                    file.Name,
                    file.FullName,
                    file.LastWriteTime.ToString("MMM d, yyyy · h:mm tt", CultureInfo.CurrentCulture),
                    FormatFileSize(file.Length)))
                .ToArray();
            if (File.Exists(RawSessionPath) &&
                !sessions.Any(session => string.Equals(
                    session.FullPath,
                    RawSessionPath,
                    StringComparison.OrdinalIgnoreCase)))
            {
                var current = new FileInfo(RawSessionPath);
                sessions =
                [
                    new SavedSessionItem(
                        current.Name,
                        current.FullName,
                        current.LastWriteTime.ToString("MMM d, yyyy · h:mm tt", CultureInfo.CurrentCulture),
                        FormatFileSize(current.Length)),
                    .. sessions,
                ];
            }

            SavedSessions = sessions;
            SelectedSavedSession = sessions.FirstOrDefault(session =>
                string.Equals(session.FullPath, RawSessionPath, StringComparison.OrdinalIgnoreCase))
                ?? sessions.FirstOrDefault();
            SessionLibraryStatus = sessions.Length == 0
                ? "No sessions have been captured on this computer yet."
                : $"{sessions.Length:N0} saved session{(sessions.Length == 1 ? string.Empty : "s")}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SavedSessions = [];
            SelectedSavedSession = null;
            SessionLibraryStatus = $"Saved sessions could not be listed: {exception.Message}";
        }
    }

    private async Task BrowseSessionAsync()
    {
        string? path;
        try
        {
            path = await _rawSessionFilePicker().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            ReplayStatus = "Could not open the file picker";
            ReplayDetail = exception.Message;
            return;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await ReplaySessionAsync(path).ConfigureAwait(true);
    }

    private Task ReplaySelectedSessionAsync()
    {
        string path = SelectedSavedSession?.FullPath
            ?? throw new InvalidOperationException("Select a saved session before replaying it.");
        return ReplaySessionAsync(path);
    }

    private async Task ReplaySessionAsync(string path)
    {
        IsBusy = true;
        ReplayStatus = "Replaying session…";
        ReplayDetail = "Opening your saved measurements.";
        try
        {
            var replayer = new A276RawSessionReplayer();
            A276RawSessionReplayResult result = await replayer
                .ReplayFileAsync(path, CancellationToken.None)
                .ConfigureAwait(true);
            ApplyReplayResult(path, result);
            SelectedPage = result.HasTransmissionSnapshot
                ? WorkspacePage.LiveData
                : WorkspacePage.Sessions;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            IsReplaySession = false;
            _isSyntheticReplay = false;
            ReplayStatus = "Session could not be replayed";
            ReplayDetail = exception.Message;
            ConnectionStatus = "Replay failed";
            LinkBadgeStatus = "CHECK FILE";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyReplayResult(string path, A276RawSessionReplayResult result)
    {
        ResetMetrics();
        RawSessionPath = Path.GetFullPath(path);
        ReceivedChunkCount = result.ReceivedChunkCount;
        ReceivedByteCount = result.ReceivedByteCount;
        ValidProtocolFrameCount = result.ValidFrameCount;
        ChecksumFailureCount = result.ChecksumFailureCount;
        if (result.ReceivedChunkCount > 1 &&
            result.FirstDataTimestamp is { } first &&
            result.LastDataTimestamp is { } last &&
            last > first)
        {
            double seconds = TimeSpan.FromTicks(last - first).TotalSeconds;
            ChunkRateText = ((result.ReceivedChunkCount - 1) / seconds)
                .ToString("0.0", CultureInfo.CurrentCulture);
        }

        ApplyTransmissionObservations(result.TransmissionObservations);
        if (LatestTransmissionSample is null && result.TransmissionResponse is not null)
        {
            LatestTransmissionSample = A276TransmissionDecoder.DecodeMode1Message1(result.TransmissionResponse);
        }
        _isSyntheticReplay = result.ContainsSimulatedData;
        IsReplaySession = true;
        IsDemoSession = false;
        DeviceSummary = $"Replayed session — {Path.GetFileName(path)}";
        QualityStatus = result.HasIntegrityFailures ||
            result.ChecksumFailureCount > 0 ||
            result.InvalidLengthCount > 0
            ? "FLAGGED"
            : result.ReceivedByteCount > 0
                ? "CLEAN"
                : "NO DATA";
        SerialLinkStatus = "REPLAYED";
        SerialLinkDetail = $"{result.ReceivedChunkCount:N0} chunks · {result.ReceivedByteCount:N0} raw bytes";
        ProtocolDecodeStatus = result.HasTransmissionSnapshot ? "SNAPSHOT" : "INCOMPLETE";
        string addresses = result.ObservedModuleAddresses.Count == 0
            ? "none"
            : string.Join(", ", result.ObservedModuleAddresses.Select(
                address => address.ToString("X2", CultureInfo.InvariantCulture)));
        ProtocolLinkDetail = $"Replay observed module addresses: {addresses}.";
        ConnectionStatus = result.HasTransmissionSnapshot ? "Replay ready" : "Replay incomplete";
        LinkBadgeStatus = result.HasTransmissionSnapshot ? "REPLAY READY" : "CHECK REPLAY";
        ReplayStatus = result.HasTransmissionSnapshot ? "Replay complete" : "Replay incomplete";
        ReplayDetail = result.HasTransmissionSnapshot
            ? "Your saved measurements are ready."
            : "This session does not contain a complete set of measurements.";
        RefreshSavedSessions();
    }

    private static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes:N0} B",
        < 1024 * 1024 => $"{bytes / 1024d:N1} KB",
        _ => $"{bytes / (1024d * 1024d):N1} MB",
    };

    private DtcDisplayItem CreateDtcDisplayItem(A276LoggedDtc observed)
    {
        if (!_dtcKnowledgeCatalog.TryGet(observed.Code, out DtcKnowledgeDefinition? definition) ||
            definition is null)
        {
            return new DtcDisplayItem(
                observed.Code,
                observed.SourceTitle,
                "A detailed explanation for this flag has not yet been added.",
                "No ranked causes are available yet.",
                "Use the applicable factory diagnostic chart before replacing parts.",
                "Unknown",
                "EXPLANATION NOT AVAILABLE",
                "This flag alone does not identify a failed part.");
        }

        string causes = definition.LikelyCauses.Count == 0
            ? "No ranked causes are available yet."
            : string.Join(
                Environment.NewLine,
                definition.LikelyCauses
                    .OrderBy(cause => cause.Rank)
                    .Select(cause => $"{cause.Rank}. {cause.Cause}"));
        string nextTest = definition.ConfirmatoryTests.Count == 0
            ? "Use the applicable factory diagnostic chart before replacing parts."
            : definition.ConfirmatoryTests[0];
        return new DtcDisplayItem(
            observed.Code,
            definition.Title,
            definition.PlainEnglishMeaning ?? "No plain-English explanation is available yet.",
            causes,
            nextTest,
            definition.SafetyLevel ?? "Not classified",
            definition.ProductionEligible ? "VERIFIED" : "DOCUMENTARY — VEHICLE CHECK PENDING",
            "This code identifies a detected condition. It does not, by itself, prove that a specific part has failed.");
    }

    private async Task DiscoverAsync()
    {
        ConnectionStatus = "Scanning";
        IsBusy = true;
        LinkBadgeStatus = "SCANNING";
        CableDiscoveryMessage = "Looking for USB serial cables connected to this computer...";
        try
        {
            await using ITransport transport = _serialTransportFactory();
            IReadOnlyList<TransportDevice> devices = await transport
                .DiscoverAsync(CancellationToken.None)
                .ConfigureAwait(true);

            DiscoveredDevices = devices;
            SelectedDevice = devices.Count > 0 ? devices[0] : null;
            DeviceSummary = devices.Count switch
            {
                0 => "No diagnostic cables found",
                1 => devices[0].DisplayName,
                _ => $"{devices.Count} diagnostic cables found",
            };
            ConnectionStatus = devices.Count == 0 ? "Offline" : "Select cable";
            LinkBadgeStatus = "STANDBY";
            InterfaceStatus = devices.Count == 0 ? "NOT FOUND" : "FOUND";
            CableDiscoveryMessage = devices.Count switch
            {
                0 => "No diagnostic cable was detected. Check the USB connection and cable driver, then scan again.",
                1 => "One cable was found and selected. Select Connect to vehicle to begin.",
                _ => $"{devices.Count} cables were found. Choose the correct cable above, then select Connect to vehicle.",
            };
        }
        catch (Exception exception)
        {
            CableDiscoveryMessage = $"Cable scan failed: {exception.Message}";
            SetFailure(exception);
        }
        finally
        {
            _hasScannedForDevices = true;
            OnPropertyChanged(nameof(FindCablesButtonText));
            IsBusy = false;
        }
    }

    private async Task ConnectSelectedAsync()
    {
        TransportDevice device = SelectedDevice
            ?? throw new InvalidOperationException("Select a diagnostic cable before connecting.");
        IsBusy = true;
        IsDemoSession = false;
        try
        {
            await ActivateTransportAsync(
                _serialTransportFactory(),
                device,
                device.DisplayName).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            SetFailure(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SelectSimulatorAsync()
    {
        IsBusy = true;
        ConnectionStatus = "Loading";
        LinkBadgeStatus = "LOADING";
        ITransport? transport = null;
        try
        {
            transport = _simulatorTransportFactory();
            IReadOnlyList<TransportDevice> devices = await transport
                .DiscoverAsync(CancellationToken.None)
                .ConfigureAwait(true);
            TransportDevice device = devices.Count == 1
                ? devices[0]
                : throw new InvalidOperationException("The simulator did not expose exactly one device.");

            ITransport selectedTransport = transport;
            transport = null;
            await ActivateTransportAsync(
                selectedTransport,
                device,
                "Built-in demo — synthetic data").ConfigureAwait(true);
            IsDemoSession = LatestTransmissionSample is not null;
            if (IsDemoSession)
            {
                InterfaceStatus = "DEMO ACTIVE";
            }
        }
        catch (Exception exception)
        {
            if (transport is not null)
            {
                await transport.DisposeAsync().ConfigureAwait(true);
            }

            SetFailure(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunDemoAsync()
    {
        if (IsDemoSession && LatestTransmissionSample is not null)
        {
            SelectedPage = WorkspacePage.LiveData;
            return;
        }

        await SelectSimulatorAsync().ConfigureAwait(true);
        if (LatestTransmissionSample is not null)
        {
            SelectedPage = WorkspacePage.LiveData;
        }
    }

    private async Task ActivateTransportAsync(
        ITransport transport,
        TransportDevice device,
        string deviceSummary)
    {
        IsReplaySession = false;
        _isSyntheticReplay = false;
        ResetMetrics();
        ConnectionStatus = "Connecting";
        LinkBadgeStatus = "CONNECTING";
        InterfaceStatus = "OPENING";
        RawSessionTarget? target = null;
        RecordingTransport? recording = null;
        try
        {
            target = _rawSessionTargetFactory();
            RawSessionPath = target.DisplayPath;
            recording = new RecordingTransport(transport, target.Writer);
            await recording.ConnectAsync(device, new TransportSettings(), CancellationToken.None).ConfigureAwait(true);

            _activeTransport = recording;
            _rawSessionWriter = target.Writer;
            _acquisitionCancellation = new CancellationTokenSource();
            DeviceSummary = deviceSummary;
            IsConnected = true;
            ConnectionStatus = "Acquiring";
            LinkBadgeStatus = "CAPTURING";
            InterfaceStatus = "READY";
            SerialLinkStatus = "ACTIVE";
            SerialLinkDetail = $"Recording {Path.GetFileName(RawSessionPath)}";
            NotifyTransportStateChanged();

            var progress = new Progress<A276AcquisitionProgress>(UpdateAcquisitionProgress);
            var coordinator = new A276AcquisitionCoordinator(_acquisitionOptions);
            _acquisitionTask = coordinator.AcquireSnapshotAsync(
                recording,
                progress,
                _acquisitionCancellation.Token);
            A276AcquisitionResult result = await _acquisitionTask.ConfigureAwait(true);
            await target.Writer.FlushAsync(CancellationToken.None).ConfigureAwait(true);
            ApplyAcquisitionResult(result);
        }
        catch
        {
            if (ReferenceEquals(_activeTransport, recording))
            {
                await ReleaseTransportAsync(updateInterface: false).ConfigureAwait(true);
            }
            else
            {
                if (recording is not null)
                {
                    await recording.DisposeAsync().ConfigureAwait(true);
                }
                else
                {
                    await transport.DisposeAsync().ConfigureAwait(true);
                }

                if (target is not null)
                {
                    await target.Writer.DisposeAsync().ConfigureAwait(true);
                }
            }

            throw;
        }
    }

    private void UpdateAcquisitionProgress(A276AcquisitionProgress progress)
    {
        ConnectionStatus = progress.Stage switch
        {
            A276AcquisitionStage.ObservingBus => "Observing bus",
            A276AcquisitionStage.DisablingNormalCommunications => "Opening request window",
            A276AcquisitionStage.RequestingIdentity => "Reading identity",
            A276AcquisitionStage.RequestingTransmissionData => "Reading transmission",
            A276AcquisitionStage.RestoringNormalCommunications => "Restoring bus",
            A276AcquisitionStage.Completed => "Snapshot ready",
            _ => "Incomplete",
        };
        ProtocolLinkDetail = progress.Detail;
    }

    private void ApplyAcquisitionResult(A276AcquisitionResult result)
    {
        ReceivedChunkCount = result.ReceivedChunkCount;
        ReceivedByteCount = result.ReceivedByteCount;
        ValidProtocolFrameCount = result.ValidFrameCount;
        ChecksumFailureCount = result.ChecksumFailureCount;
        ApplyTransmissionObservations(result.TransmissionObservations);
        if (LatestTransmissionSample is null && result.TransmissionResponse is not null)
        {
            LatestTransmissionSample = A276TransmissionDecoder.DecodeMode1Message1(result.TransmissionResponse);
        }

        if (result.ReceivedChunkCount > 1 &&
            result.FirstDataTimestamp is { } first &&
            result.LastDataTimestamp is { } last &&
            last > first)
        {
            double seconds = TimeSpan.FromTicks(last - first).TotalSeconds;
            ChunkRateText = ((result.ReceivedChunkCount - 1) / seconds)
                .ToString("0.0", CultureInfo.CurrentCulture);
        }

        bool malformed = result.ChecksumFailureCount > 0 || result.InvalidLengthCount > 0;
        QualityStatus = malformed ? "FLAGGED" : result.ReceivedByteCount > 0 ? "CLEAN" : "NO DATA";
        SerialLinkStatus = result.RestorationAttempted
            ? result.RestorationCompleted ? "RESTORED" : "RESTORE CHECK"
            : "PASSIVE";
        SerialLinkDetail = $"{result.ReceivedChunkCount:N0} chunks · {result.ReceivedByteCount:N0} raw bytes · {Path.GetFileName(RawSessionPath)}";
        ProtocolDecodeStatus = result.IsComplete ? "SNAPSHOT" : "INCOMPLETE";
        string addresses = result.ObservedModuleAddresses.Count == 0
            ? "none"
            : string.Join(", ", result.ObservedModuleAddresses.Select(address => address.ToString("X2", CultureInfo.InvariantCulture)));
        ProtocolLinkDetail = result.IsComplete
            ? $"Message 4 + Message 1 correlated · observed {addresses}"
            : $"{result.Detail} Observed addresses: {addresses}.";
        ConnectionStatus = result.IsComplete ? "Snapshot ready" : "Incomplete";
        LinkBadgeStatus = result.IsComplete ? "READY" : "CHECK";
    }

    private async Task DisconnectAsync()
    {
        IsBusy = true;
        try
        {
            await ReleaseTransportAsync(updateInterface: true).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            SetFailure(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReleaseTransportAsync(bool updateInterface)
    {
        ITransport? transport = _activeTransport;
        RawSessionWriter? writer = _rawSessionWriter;
        CancellationTokenSource? acquisitionCancellation = _acquisitionCancellation;
        Task<A276AcquisitionResult>? acquisitionTask = _acquisitionTask;
        _activeTransport = null;
        _rawSessionWriter = null;
        _acquisitionCancellation = null;
        _acquisitionTask = null;

        acquisitionCancellation?.Cancel();
        Exception? releaseFailure = null;
        if (acquisitionTask is not null)
        {
            try
            {
                await acquisitionTask.ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (acquisitionCancellation?.IsCancellationRequested == true)
            {
            }
            catch (Exception exception)
            {
                releaseFailure = exception;
            }
        }

        if (transport is not null)
        {
            try
            {
                await transport.DisconnectAsync(CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                releaseFailure ??= exception;
            }
            finally
            {
                try
                {
                    await transport.DisposeAsync().ConfigureAwait(true);
                }
                catch (Exception exception)
                {
                    releaseFailure ??= exception;
                }
            }
        }

        if (writer is not null)
        {
            try
            {
                await writer.DisposeAsync().ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                releaseFailure ??= exception;
            }
        }

        acquisitionCancellation?.Dispose();
        IsConnected = false;
        IsDemoSession = false;
        if (updateInterface)
        {
            ConnectionStatus = "Offline";
            LinkBadgeStatus = "STANDBY";
            InterfaceStatus = HasDiscoveredDevices ? "FOUND" : "NOT FOUND";
            SerialLinkStatus = "IDLE";
            SerialLinkDetail = RawSessionPath == "Created when a link opens"
                ? "Raw capture starts before acquisition"
                : $"Saved {Path.GetFileName(RawSessionPath)}";
            DeviceSummary = SelectedDevice?.DisplayName ?? "No diagnostic cable selected";
        }

        NotifyTransportStateChanged();
        if (releaseFailure is not null)
        {
            throw releaseFailure;
        }
    }

    private void ResetMetrics()
    {
        ReceivedChunkCount = 0;
        ReceivedByteCount = 0;
        ChunkRateText = "--";
        QualityStatus = "NO DATA";
        SerialLinkDetail = "Raw capture starts before acquisition";
        ValidProtocolFrameCount = 0;
        ChecksumFailureCount = 0;
        LatestTransmissionSample = null;
        ApplyTransmissionObservations([]);
        ProtocolDecodeStatus = "WAITING";
        ProtocolLinkDetail = "Awaiting a checksum-valid F4 PCM frame";
    }

    private void ApplyTransmissionObservations(IReadOnlyList<A276TransmissionObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (observations.Count == 0)
        {
            TransmissionTimeline = [];
            _domainTransmissionObservations = [];
            _sessionAnalysis = TransmissionSessionAnalyzer.Analyze([]);
            NotifySessionAnalysisChanged();
            return;
        }

        long firstTimestamp = observations[0].MonotonicTimestamp;
        var domainObservations = new List<TransmissionObservation>(observations.Count);
        var timeline = new List<TransmissionTimelineItem>(observations.Count);
        foreach (A276TransmissionObservation observation in observations)
        {
            TimeSpan elapsed = TimeSpan.FromTicks(Math.Max(0, observation.MonotonicTimestamp - firstTimestamp));
            A276TransmissionSample sample = observation.Sample;
            domainObservations.Add(new TransmissionObservation(
                elapsed,
                sample.EngineSpeedRpm,
                sample.VehicleSpeedMph,
                sample.CommandedGear,
                sample.SlipRpm,
                sample.TransmissionFluidTemperatureCelsius,
                sample.TransmissionIgnitionVoltage,
                sample.CurrentTorqueSignalPressurePsi,
                sample.ReferenceForceMotorCurrentAmps,
                sample.ActualForceMotorCurrentAmps,
                sample.TccControlCommanded,
                sample.TccEnabled,
                sample.ShiftSolenoidACommanded,
                sample.ShiftSolenoidBCommanded,
                sample.VerificationStatus));
            timeline.Add(new TransmissionTimelineItem(
                elapsed.TotalSeconds.ToString("0.000", CultureInfo.CurrentCulture) + " s",
                sample.VehicleSpeedMph.ToString("0.0", CultureInfo.CurrentCulture) + " mph",
                sample.EngineSpeedRpm.ToString("0", CultureInfo.CurrentCulture) + " rpm",
                sample.CommandedGear.ToString(CultureInfo.CurrentCulture),
                sample.SlipRpm.ToString("0", CultureInfo.CurrentCulture) + " rpm"));
        }

        TransmissionTimeline = timeline.TakeLast(20).ToArray();
        _domainTransmissionObservations = domainObservations.AsReadOnly();
        _sessionAnalysis = TransmissionSessionAnalyzer.Analyze(domainObservations);
        LatestTransmissionSample = observations[^1].Sample;
        NotifySessionAnalysisChanged();
    }

    private void NotifySessionAnalysisChanged()
    {
        OnPropertyChanged(nameof(SessionSampleCountText));
        OnPropertyChanged(nameof(SessionDurationText));
        OnPropertyChanged(nameof(SessionEventCountText));
        OnPropertyChanged(nameof(SessionInterpretationBoundary));
    }

    private async Task ExportReportAsync(string extension)
    {
        if (!HasTransmissionData)
        {
            ReportStatus = "Load a session before exporting.";
            return;
        }

        IsBusy = true;
        try
        {
            string baseName = Path.GetFileNameWithoutExtension(RawSessionPath);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "4L60-Diagnostics-session";
            }

            string suggestedName = extension == "html"
                ? baseName + "-report.html"
                : baseName + "-measurements.csv";
            string? path = await _reportSavePicker(suggestedName, extension).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(path))
            {
                ReportStatus = "Export canceled.";
                return;
            }

            string content = extension == "html"
                ? DiagnosticReportGenerator.GenerateHtml(CreateReportInput())
                : DiagnosticReportGenerator.GenerateCsv(_domainTransmissionObservations);
            await File.WriteAllTextAsync(path, content, new UTF8Encoding(false)).ConfigureAwait(true);
            _lastReportPath = Path.GetFullPath(path);
            ReportStatus = extension == "html"
                ? $"Report saved: {Path.GetFileName(path)}"
                : $"Measurements saved: {Path.GetFileName(path)}";
            OpenReportFolderCommand.RaiseCanExecuteChanged();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ReportStatus = $"Export failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private DiagnosticReportInput CreateReportInput()
    {
        DiagnosticReportDtc[] dtcs = LoggedTransmissionDtcs
            .Select(item => new DiagnosticReportDtc(
                item.Code,
                item.Title,
                item.PlainEnglishMeaning,
                item.LikelyCausesText,
                item.NextTest,
                item.EvidenceStatus))
            .ToArray();
        return new DiagnosticReportInput(
            "1994 Buick Roadmaster · 5.7L LT1 · 4L60E",
            DateTimeOffset.Now,
            Path.GetFileName(RawSessionPath),
            EvidenceLabel,
            QualityStatus,
            _sessionAnalysis,
            _domainTransmissionObservations,
            dtcs);
    }

    private void OpenReportFolder()
    {
        if (_lastReportPath is not { } path || !File.Exists(path))
        {
            return;
        }

        OpenContainingFolder(path);
    }

    private void SetFailure(Exception exception)
    {
        ConnectionStatus = "Unavailable";
        LinkBadgeStatus = "FAULT";
        InterfaceStatus = "FAULT";
        SerialLinkStatus = "FAULT";
        DeviceSummary = exception.Message;
        SerialLinkDetail = exception.Message;
    }

    private static A276AcquisitionOptions CreateDefaultAcquisitionOptions() => new(
        InitialObservationWindow: TimeSpan.FromMilliseconds(250),
        ResponseTimeout: TimeSpan.FromMilliseconds(400),
        EchoWindow: TimeSpan.FromMilliseconds(100));

    private static (DtcKnowledgeCatalog Catalog, string Status) LoadDefaultDtcKnowledge()
    {
        try
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "definitions", "dtcs");
            DtcKnowledgeCatalog catalog = DtcKnowledgeLoader
                .LoadDirectoryAsync(directory)
                .GetAwaiter()
                .GetResult();
            return catalog.Count == 0
                ? (catalog, "No DTC explanations are installed.")
                : (catalog, $"{catalog.Count} source-backed explanations loaded · vehicle verification pending");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return (DtcKnowledgeCatalog.Empty, $"DTC explanations could not be loaded: {exception.Message}");
        }
    }

    private static RawSessionTarget CreateDefaultRawSessionTarget()
    {
        string sessionDirectory = GetDefaultSessionDirectory();
        Directory.CreateDirectory(sessionDirectory);
        string filename = $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.lt1raw";
        string path = Path.Combine(sessionDirectory, filename);
        var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        return new RawSessionTarget(new RawSessionWriter(stream), path);
    }

    private static string GetDefaultSessionDirectory()
    {
        string applicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string root = string.IsNullOrWhiteSpace(applicationData)
            ? Path.Combine(Path.GetTempPath(), "LT1Diagnostics")
            : Path.Combine(applicationData, "LT1Diagnostics");
        return Path.Combine(root, "Sessions");
    }

    private string FormatSample(Func<A276TransmissionSample, double> selector, string format, string suffix) =>
        LatestTransmissionSample is { } sample
            ? selector(sample).ToString(format, CultureInfo.CurrentCulture) + suffix
            : "--";

    private void OpenSessionFolder()
    {
        string? path = SelectedSavedSession?.FullPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            path = HasSavedSession ? RawSessionPath : null;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        OpenContainingFolder(path);
    }

    private static void OpenContainingFolder(string path)
    {
        string directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The selected file has no containing folder.");
        ProcessStartInfo startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
            : new ProcessStartInfo("xdg-open", directory);
        startInfo.UseShellExecute = true;
        _ = Process.Start(startInfo);
    }

    private void NotifyTransportStateChanged()
    {
        OnPropertyChanged(nameof(HasActiveTransport));
        OnPropertyChanged(nameof(ShowDeviceSummary));
        OnPropertyChanged(nameof(ShowDeviceSelector));
        OnPropertyChanged(nameof(ShowConnectionActions));
        OnPropertyChanged(nameof(ShowConnectAction));
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(CanSelectDevice));
        OnPropertyChanged(nameof(ConnectionDisplayStatus));
        NotifyCommandStates();
    }

    private void NotifyCommandStates()
    {
        DiscoverCommand.RaiseCanExecuteChanged();
        ConnectCommand.RaiseCanExecuteChanged();
        SimulatorCommand.RaiseCanExecuteChanged();
        DisconnectCommand.RaiseCanExecuteChanged();
        RunDemoCommand.RaiseCanExecuteChanged();
        ReplaySelectedSessionCommand.RaiseCanExecuteChanged();
        BrowseSessionCommand.RaiseCanExecuteChanged();
        ExportReportCommand.RaiseCanExecuteChanged();
        ExportCsvCommand.RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
