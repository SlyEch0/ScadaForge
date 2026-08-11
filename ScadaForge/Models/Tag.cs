using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ScadaForge.Models;

/// <summary>
/// Quality of a process value – maps cleanly to both OPC UA StatusCode and AVEVA MXAccess quality.
/// </summary>
public enum TagQuality
{
    Good = 0,
    Uncertain = 1,
    Bad = 2
}

/// <summary>
/// Core tag model with full VTQ (Value – Timestamp – Quality).
/// Designed for dual use with OPC UA NodeIds and AVEVA Galaxy attribute paths.
/// </summary>
public partial class Tag : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private object? _value;

    [ObservableProperty]
    private DateTime _timestamp = DateTime.UtcNow;

    [ObservableProperty]
    private TagQuality _quality = TagQuality.Good;

    [ObservableProperty]
    private string _dataType = "Double";

    [ObservableProperty]
    private string _engineeringUnits = string.Empty;

    /// <summary>
    /// Source of truth: Simulation | OpcUa | AvevaGalaxy
    /// </summary>
    [ObservableProperty]
    private string _source = "Simulation";

    /// <summary>
    /// OPC UA NodeId string or AVEVA Galaxy "Object.Attribute" path.
    /// </summary>
    [ObservableProperty]
    private string? _address;

    /// <summary>
    /// Human-friendly display of the current value with units awareness.
    /// </summary>
    public string DisplayValue
    {
        get
        {
            if (Value is null) return "---";
            return Value switch
            {
                double d => $"{d:F1}",
                float f  => $"{f:F1}",
                int i    => i.ToString(),
                bool b   => b ? "TRUE" : "FALSE",
                _        => Value.ToString() ?? "---"
            };
        }
    }

    /// <summary>
    /// Updates the tag with new VTQ data and raises property change for DisplayValue.
    /// </summary>
    public void Update(object? value, TagQuality quality = TagQuality.Good, DateTime? timestamp = null)
    {
        Value = value;
        Quality = quality;
        Timestamp = timestamp ?? DateTime.UtcNow;
        OnPropertyChanged(nameof(DisplayValue));
    }

    public override string ToString() => $"{Name} = {DisplayValue} {EngineeringUnits} [{Quality}]";
}
