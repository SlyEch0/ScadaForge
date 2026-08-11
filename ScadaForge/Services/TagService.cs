using ScadaForge.Models;

namespace ScadaForge.Services;

/// <summary>
/// Unified tag facade. Currently supports Simulation (always available) and live OPC UA.
/// AVEVA MXAccess bridge can be added later as another source without changing consumers.
/// </summary>
public sealed class TagService : IDisposable
{
    private readonly Project _project;
    private readonly LogService _log;
    private readonly SimulationEngine _simulation;
    private readonly OpcUaClientService _opcUa;

    public enum DataSource { Simulation, OpcUa }

    public DataSource CurrentSource { get; private set; } = DataSource.Simulation;

    public TagService(Project project, LogService log)
    {
        _project = project;
        _log = log;
        _simulation = new SimulationEngine(project, log);
        _opcUa = new OpcUaClientService(log);

        _opcUa.TagValueChanged += OnOpcUaValueChanged;
    }

    public SimulationEngine Simulation => _simulation;
    public OpcUaClientService OpcUa => _opcUa;

    public void StartSimulation()
    {
        CurrentSource = DataSource.Simulation;
        _simulation.Start();
        _log.Info("Data source switched to Simulation");
    }

    public async Task ConnectOpcUaAsync(string endpointUrl)
    {
        await _opcUa.ConnectAsync(endpointUrl);
        CurrentSource = DataSource.OpcUa;
        _simulation.Stop();
        _log.Info("Data source switched to OPC UA");
    }

    public async Task DisconnectOpcUaAsync()
    {
        await _opcUa.DisconnectAsync();
        CurrentSource = DataSource.Simulation;
        _simulation.Start();
    }

    private void OnOpcUaValueChanged(string name, object? value, TagQuality quality)
    {
        var tag = _project.Tags.FirstOrDefault(t => t.Name == name);
        if (tag is null)
        {
            tag = new Tag { Name = name, Source = "OpcUa" };
            _project.Tags.Add(tag);
        }
        tag.Update(value, quality);
        tag.Source = "OpcUa";
    }

    public Tag? GetTag(string name) => _project.Tags.FirstOrDefault(t => t.Name == name);

    public void Dispose()
    {
        _simulation.Dispose();
        _opcUa.Dispose();
    }
}
