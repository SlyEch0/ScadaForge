using System.Collections.ObjectModel;
using ScadaForge.Models;

namespace ScadaForge.Services;

/// <summary>
/// Central logging service that feeds the OUTPUT panel at the bottom of the IDE.
/// Thread-safe for use from simulation and OPC UA callbacks.
/// </summary>
public sealed class LogService
{
    private readonly object _lock = new();
    private readonly ObservableCollection<LogEntry> _entries = new();

    public ObservableCollection<LogEntry> Entries => _entries;

    public event Action? LogChanged;

    public void Info(string message) => Add("INFO", message);
    public void Warn(string message) => Add("WARN", message);
    public void Error(string message) => Add("ERROR", message);

    private void Add(string level, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message
        };

        lock (_lock)
        {
            if (_entries.Count > 500)
                _entries.RemoveAt(0);

            _entries.Add(entry);
        }

        LogChanged?.Invoke();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
        LogChanged?.Invoke();
    }
}
