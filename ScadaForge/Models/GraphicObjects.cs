using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ScadaForge.Models;

/// <summary>
/// Base class for all visual process objects on the HMI canvas.
/// Supports selection, positioning, and tag binding.
/// </summary>
public abstract partial class GraphicObject : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _width = 80;
    [ObservableProperty] private double _height = 60;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isEnabled = true;

    /// <summary>
    /// Bound process tags (name → Tag). Updated live by the TagService.
    /// </summary>
    public ObservableDictionary<string, Tag> BoundTags { get; } = new();

    public virtual string ObjectType => "Graphic";
}

/// <summary>
/// Motor / Pump object – matches P-101 in the Water Treatment Plant overview.
/// </summary>
public partial class Motor : GraphicObject
{
    public override string ObjectType => "Motor";

    [ObservableProperty] private string _state = "Stopped";          // Running / Stopped / Fault
    [ObservableProperty] private string _command = "Stop";           // Start / Stop
    [ObservableProperty] private double _speedRpm;
    [ObservableProperty] private double _runtimeHours;
    [ObservableProperty] private DateTime? _lastStart;
    [ObservableProperty] private double _ratedPowerKw = 15.0;
    [ObservableProperty] private double _ratedCurrentA = 28.5;
    [ObservableProperty] private double _minSpeedRpm = 300;
    [ObservableProperty] private string _tagPrefix = "P-";

    public Motor()
    {
        Width = 90;
        Height = 70;
    }
}

/// <summary>
/// Process tank (Raw Water, Clarifier, Aeration, Clean Water, etc.)
/// </summary>
public partial class Tank : GraphicObject
{
    public override string ObjectType => "Tank";

    [ObservableProperty] private double _levelPercent = 65;
    [ObservableProperty] private double _volumeM3;
    [ObservableProperty] private string _content = "Water";

    public Tank()
    {
        Width = 70;
        Height = 110;
    }
}

/// <summary>
/// Control or isolation valve.
/// </summary>
public partial class ControlValve : GraphicObject
{
    public override string ObjectType => "ControlValve";

    [ObservableProperty] private double _positionPercent = 100; // 0 = closed, 100 = open
    [ObservableProperty] private bool _isOpen = true;
    [ObservableProperty] private string _valveType = "Control"; // Control / Isolation

    public ControlValve()
    {
        Width = 40;
        Height = 40;
    }
}

/// <summary>
/// Field instrument (flow, pressure, DO, etc.)
/// </summary>
public partial class Instrument : GraphicObject
{
    public override string ObjectType => "Instrument";

    [ObservableProperty] private string _instrumentType = "AnalogInput"; // AnalogInput / DigitalInput
    [ObservableProperty] private string _displayLabel = "";

    public Instrument()
    {
        Width = 50;
        Height = 30;
    }
}

/// <summary>
/// Simple observable dictionary helper for BoundTags.
/// </summary>
public class ObservableDictionary<TKey, TValue> : ObservableCollection<KeyValuePair<TKey, TValue>>
    where TKey : notnull
{
    public TValue? this[TKey key]
    {
        get
        {
            foreach (var kvp in this)
                if (EqualityComparer<TKey>.Default.Equals(kvp.Key, key))
                    return kvp.Value;
            return default;
        }
        set
        {
            for (int i = 0; i < Count; i++)
            {
                if (EqualityComparer<TKey>.Default.Equals(this[i].Key, key))
                {
                    this[i] = new KeyValuePair<TKey, TValue>(key, value!);
                    return;
                }
            }
            Add(new KeyValuePair<TKey, TValue>(key, value!));
        }
    }

    public bool ContainsKey(TKey key)
    {
        foreach (var kvp in this)
            if (EqualityComparer<TKey>.Default.Equals(kvp.Key, key))
                return true;
        return false;
    }
}
