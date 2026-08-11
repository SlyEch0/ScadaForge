using System.Diagnostics;
using ScadaForge.Models;

namespace ScadaForge.Services;

/// <summary>
/// Realistic process simulation for the Water Treatment Plant overview.
/// Drives all tags and graphic objects shown in the screenshot (P-101, levels, DO, flows, ΔP, etc.).
/// Designed so that switching to live OPC UA / AVEVA is a one-line change in TagService.
/// </summary>
public sealed class SimulationEngine : IDisposable
{
    private readonly LogService _log;
    private readonly Project _project;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private readonly Random _rng = new();
    private readonly Stopwatch _runtime = Stopwatch.StartNew();

    // Process state
    private double _rawLevel = 72.0;
    private double _clarifierLevel = 58.0;
    private double _aerationLevel = 65.0;
    private double _cleanLevel = 80.0;
    private double _pumpSpeed = 1450.0;
    private bool _pumpRunning = true;
    private double _do = 2.1;
    private double _filterDp = 0.32;
    private double _flow = 250.0;

    public bool IsRunning { get; private set; }

    public SimulationEngine(Project project, LogService log)
    {
        _project = project;
        _log = log;
    }

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        IsRunning = true;
        _log.Info("Simulation started");
        _loopTask = Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _cts?.Cancel();
        IsRunning = false;
        _log.Info("Simulation stopped");
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Tick();
                await Task.Delay(500, ct); // 2 Hz update – good for HMI
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Error($"Simulation tick failed: {ex.Message}");
            }
        }
    }

    private void Tick()
    {
        // Simple but believable dynamics
        if (_pumpRunning)
        {
            _pumpSpeed = 1450 + Math.Sin(_runtime.Elapsed.TotalSeconds * 0.3) * 12 + (_rng.NextDouble() - 0.5) * 8;
            _flow = 248 + Math.Sin(_runtime.Elapsed.TotalSeconds * 0.25) * 4 + (_rng.NextDouble() - 0.5) * 3;
            _rawLevel = Math.Clamp(_rawLevel - 0.08 + (_rng.NextDouble() * 0.15), 40, 95);
            _clarifierLevel = Math.Clamp(_clarifierLevel + 0.05 + (_rng.NextDouble() - 0.5) * 0.1, 35, 85);
            _aerationLevel = Math.Clamp(_aerationLevel + (_rng.NextDouble() - 0.5) * 0.2, 50, 80);
            _cleanLevel = Math.Clamp(_cleanLevel + 0.04 + (_rng.NextDouble() - 0.5) * 0.1, 60, 95);
            _do = Math.Clamp(2.05 + Math.Sin(_runtime.Elapsed.TotalSeconds * 0.15) * 0.15 + (_rng.NextDouble() - 0.5) * 0.05, 1.6, 2.6);
            _filterDp = Math.Clamp(0.30 + Math.Sin(_runtime.Elapsed.TotalSeconds * 0.1) * 0.03 + (_rng.NextDouble() - 0.5) * 0.02, 0.18, 0.55);
        }
        else
        {
            _pumpSpeed = Math.Max(0, _pumpSpeed - 40);
            _flow = Math.Max(0, _flow - 15);
        }

        // Occasionally inject Uncertain quality to match the sample log
        var quality = _rng.NextDouble() < 0.015 ? TagQuality.Uncertain : TagQuality.Good;

        // Update tags
        UpdateTag("P-101.Speed", Math.Round(_pumpSpeed, 0), "RPM", quality);
        UpdateTag("P-101.Flow", Math.Round(_flow, 1), "m³/h", quality);
        UpdateTag("P-101.State", _pumpRunning ? "Running" : "Stopped", "", TagQuality.Good);
        UpdateTag("P-101.Runtime", Math.Round(_runtime.Elapsed.TotalHours + 125.4, 1), "h", TagQuality.Good);

        UpdateTag("RawWater.Level", Math.Round(_rawLevel, 1), "%", quality);
        UpdateTag("Clarifier.Level", Math.Round(_clarifierLevel, 1), "%", TagQuality.Good);
        UpdateTag("Aeration.Level", Math.Round(_aerationLevel, 1), "%", TagQuality.Good);
        UpdateTag("Aeration.DO", Math.Round(_do, 1), "mg/L", TagQuality.Good);
        UpdateTag("Aeration.AirFlow", 120.0 + (_rng.NextDouble() - 0.5) * 4, "Nm³/h", TagQuality.Good);
        UpdateTag("Filter.DP", Math.Round(_filterDp, 2), "bar", TagQuality.Good);
        UpdateTag("Filter.Flow", Math.Round(_flow * 0.92, 1), "m³/h", TagQuality.Good);
        UpdateTag("CleanWater.Level", Math.Round(_cleanLevel, 1), "%", TagQuality.Good);

        // Sync graphic objects
        foreach (var obj in _project.Objects)
        {
            if (obj is Motor m && m.Name == "P-101")
            {
                m.State = _pumpRunning ? "Running" : "Stopped";
                m.SpeedRpm = Math.Round(_pumpSpeed, 0);
                m.RuntimeHours = Math.Round(_runtime.Elapsed.TotalHours + 125.4, 1);
                m.Command = _pumpRunning ? "Start" : "Stop";
            }
            else if (obj is Tank t)
            {
                t.LevelPercent = t.Name switch
                {
                    "Raw Water Inlet" => _rawLevel,
                    "Clarifier" => _clarifierLevel,
                    "Aeration Tank" => _aerationLevel,
                    "Clean Water Outlet" => _cleanLevel,
                    _ => t.LevelPercent
                };
            }
        }
    }

    private void UpdateTag(string name, object value, string units, TagQuality quality)
    {
        var tag = _project.Tags.FirstOrDefault(t => t.Name == name);
        if (tag is null)
        {
            tag = new Tag
            {
                Name = name,
                EngineeringUnits = units,
                Source = "Simulation"
            };
            _project.Tags.Add(tag);
        }

        tag.Update(value, quality);
        tag.EngineeringUnits = units;
    }

    public void SetPumpRunning(bool running)
    {
        _pumpRunning = running;
        _log.Info($"P-101 command: {(running ? "Start" : "Stop")}");
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
