using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ScadaForge.Models;

/// <summary>
/// Represents a SCADA Forge project (.scs conceptually).
/// Contains screens, tags, and graphic objects for the Water Treatment Plant example.
/// </summary>
public partial class Project : ObservableObject
{
    [ObservableProperty] private string _name = "Water Treatment Plant";
    [ObservableProperty] private string _description = "SCADA Forge demo project for AVEVA System Platform 2023 + ControlLogix";
    [ObservableProperty] private DateTime _created = DateTime.UtcNow;
    [ObservableProperty] private DateTime _lastModified = DateTime.UtcNow;
    [ObservableProperty] private int _unsavedChangeCount;

    public ObservableCollection<GraphicObject> Objects { get; } = new();
    public ObservableCollection<Tag> Tags { get; } = new();
    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    public void MarkDirty()
    {
        UnsavedChangeCount++;
        LastModified = DateTime.UtcNow;
    }

    public void MarkClean()
    {
        UnsavedChangeCount = 0;
    }
}

/// <summary>
/// Simple log entry for the OUTPUT panel.
/// </summary>
public partial class LogEntry : ObservableObject
{
    [ObservableProperty] private DateTime _timestamp = DateTime.Now;
    [ObservableProperty] private string _level = "INFO"; // INFO | WARN | ERROR
    [ObservableProperty] private string _message = string.Empty;

    public string Formatted => $"{Timestamp:HH:mm:ss} [{Level}]  {Message}";
}

/// <summary>
/// Alarm summary item.
/// </summary>
public partial class AlarmItem : ObservableObject
{
    [ObservableProperty] private string _tagName = string.Empty;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private string _priority = "Normal";
    [ObservableProperty] private DateTime _time = DateTime.Now;
    [ObservableProperty] private bool _isActive = true;
}
