using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScadaForge.Models;
using ScadaForge.Services;

namespace ScadaForge.ViewModels;

/// <summary>
/// Main ViewModel for the SCADA Forge IDE shell.
/// Orchestrates project, selection, simulation, OPC UA, and the properties panel.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly LogService _log = new();
    private readonly Project _project = new();
    private readonly TagService _tagService;
    private readonly DispatcherTimer _uiTimer;

    [ObservableProperty] private string _projectName = "Water Treatment Plant";
    [ObservableProperty] private string _connectionStatus = "Simulation";
    [ObservableProperty] private string _connectionColor = "#4ADE80"; // green
    [ObservableProperty] private GraphicObject? _selectedObject;
    [ObservableProperty] private string _statusBarText = "Ready – SCADA Forge 1.0";
    [ObservableProperty] private int _unsavedChanges;
    [ObservableProperty] private string _opcEndpoint = "opc.tcp://localhost:4840";

    // Bound collections
    public ObservableCollection<GraphicObject> Objects => _project.Objects;
    public ObservableCollection<LogEntry> LogEntries => _log.Entries;
    public ObservableCollection<Tag> Tags => _project.Tags;

    // Commands
    public IRelayCommand StartSimulationCommand { get; }
    public IRelayCommand StopSimulationCommand { get; }
    public IRelayCommand ConnectOpcCommand { get; }
    public IRelayCommand DisconnectOpcCommand { get; }
    public IRelayCommand SelectObjectCommand { get; }
    public IRelayCommand ClearLogCommand { get; }
    public IRelayCommand StartPumpCommand { get; }
    public IRelayCommand StopPumpCommand { get; }

    public MainViewModel()
    {
        _tagService = new TagService(_project, _log);

        StartSimulationCommand = new RelayCommand(OnStartSimulation);
        StopSimulationCommand = new RelayCommand(OnStopSimulation);
        ConnectOpcCommand = new AsyncRelayCommand(OnConnectOpcAsync);
        DisconnectOpcCommand = new AsyncRelayCommand(OnDisconnectOpcAsync);
        SelectObjectCommand = new RelayCommand<GraphicObject>(OnSelectObject);
        ClearLogCommand = new RelayCommand(() => _log.Clear());
        StartPumpCommand = new RelayCommand(() => _tagService.Simulation.SetPumpRunning(true));
        StopPumpCommand = new RelayCommand(() => _tagService.Simulation.SetPumpRunning(false));

        // Build the demo Water Treatment Plant process
        BuildDemoProcess();

        // UI refresh timer (keeps properties panel live)
        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _uiTimer.Tick += (_, _) => RefreshSelectedProperties();
        _uiTimer.Start();

        // Start simulation immediately so the screen is alive
        _tagService.StartSimulation();
        _log.Info("Project loaded: Water Treatment Plant");
        _log.Info("Connected to Simulation engine");
        _log.Info("All systems nominal");
    }

    private void BuildDemoProcess()
    {
        // P-101 – the motor selected in the screenshot
        var p101 = new Motor
        {
            Name = "P-101",
            Description = "Raw Water Pump",
            X = 220,
            Y = 180,
            State = "Running",
            SpeedRpm = 1450,
            RuntimeHours = 125.4,
            Command = "Start",
            LastStart = new DateTime(2024, 5, 20, 10, 15, 0),
            RatedPowerKw = 15.0,
            RatedCurrentA = 28.5,
            MinSpeedRpm = 300,
            TagPrefix = "P-"
        };

        var rawTank = new Tank { Name = "Raw Water Inlet", Description = "Raw water storage", X = 80, Y = 150, LevelPercent = 72 };
        var clarifier = new Tank { Name = "Clarifier", Description = "Primary clarifier", X = 340, Y = 140, LevelPercent = 58 };
        var aeration = new Tank { Name = "Aeration Tank", Description = "Biological aeration", X = 480, Y = 130, LevelPercent = 65 };
        var filter = new Tank { Name = "Filter Unit", Description = "Sand filter", X = 640, Y = 140, LevelPercent = 0 }; // visual only
        var clean = new Tank { Name = "Clean Water Outlet", Description = "Treated water", X = 800, Y = 150, LevelPercent = 80 };

        var fv201 = new ControlValve { Name = "FV-201", Description = "Flow control to clarifier", X = 300, Y = 210, PositionPercent = 75 };
        var xv301 = new ControlValve { Name = "XV-301", Description = "Aeration isolation", X = 450, Y = 210, PositionPercent = 100 };
        var xv302 = new ControlValve { Name = "XV-302", Description = "Filter isolation", X = 600, Y = 210, PositionPercent = 100 };

        var ft101 = new Instrument { Name = "FT-101", Description = "Raw water flow", X = 200, Y = 300, InstrumentType = "AnalogInput", DisplayLabel = "FT-101" };
        var ait201 = new Instrument { Name = "AIT-201", Description = "Dissolved oxygen", X = 500, Y = 300, InstrumentType = "AnalogInput", DisplayLabel = "AIT-201" };
        var fit301 = new Instrument { Name = "FIT-301", Description = "Filter flow", X = 650, Y = 300, InstrumentType = "AnalogInput", DisplayLabel = "FIT-301" };
        var pt401 = new Instrument { Name = "PT-401", Description = "Filter differential pressure", X = 720, Y = 300, InstrumentType = "AnalogInput", DisplayLabel = "PT-401" };

        _project.Objects.Add(rawTank);
        _project.Objects.Add(p101);
        _project.Objects.Add(clarifier);
        _project.Objects.Add(aeration);
        _project.Objects.Add(filter);
        _project.Objects.Add(clean);
        _project.Objects.Add(fv201);
        _project.Objects.Add(xv301);
        _project.Objects.Add(xv302);
        _project.Objects.Add(ft101);
        _project.Objects.Add(ait201);
        _project.Objects.Add(fit301);
        _project.Objects.Add(pt401);

        // Pre-select P-101 so the properties panel matches the screenshot on launch
        SelectedObject = p101;
        p101.IsSelected = true;
    }

    private void OnSelectObject(GraphicObject? obj)
    {
        if (SelectedObject is not null)
            SelectedObject.IsSelected = false;

        SelectedObject = obj;
        if (obj is not null)
            obj.IsSelected = true;
    }

    private void RefreshSelectedProperties()
    {
        // Force property refresh for the selected motor so the panel stays live
        if (SelectedObject is Motor m)
        {
            OnPropertyChanged(nameof(SelectedObject));
        }
        UnsavedChanges = _project.UnsavedChangeCount;
        ConnectionStatus = _tagService.CurrentSource == TagService.DataSource.Simulation
            ? "Simulation"
            : (_tagService.OpcUa.IsConnected ? "OPC UA Connected" : "Disconnected");
        ConnectionColor = _tagService.CurrentSource == TagService.DataSource.Simulation || _tagService.OpcUa.IsConnected
            ? "#4ADE80"
            : "#F87171";
    }

    private void OnStartSimulation()
    {
        _tagService.StartSimulation();
        StatusBarText = "Simulation running";
    }

    private void OnStopSimulation()
    {
        _tagService.Simulation.Stop();
        StatusBarText = "Simulation stopped";
    }

    private async Task OnConnectOpcAsync()
    {
        try
        {
            StatusBarText = "Connecting to OPC UA…";
            await _tagService.ConnectOpcUaAsync(OpcEndpoint);
            StatusBarText = $"Connected to {OpcEndpoint}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"OPC UA connection failed:\n{ex.Message}", "SCADA Forge", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusBarText = "OPC UA connection failed";
        }
    }

    private async Task OnDisconnectOpcAsync()
    {
        await _tagService.DisconnectOpcUaAsync();
        StatusBarText = "Returned to Simulation";
    }
}
