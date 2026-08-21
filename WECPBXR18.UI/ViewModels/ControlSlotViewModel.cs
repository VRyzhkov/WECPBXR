using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WECPBXR18.Core.Models;

namespace WECPBXR18.UI.ViewModels;

public sealed class ControlSlotViewModel : ObservableObject
{
    private ControlSlotSnapshot _snapshot;
    private bool _isSelected;

    public ControlSlotViewModel(ControlSlotSnapshot snapshot, double left, double top, double width, double height)
    {
        _snapshot = snapshot;
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    public string Id => _snapshot.Id;

    public string Label => _snapshot.Label;

    public ControlKind Kind => _snapshot.Kind;

    public double? ControllerValue => _snapshot.ControllerValue;

    public double? MixerValue => _snapshot.MixerValue;

    public bool IsLocked => _snapshot.IsLocked;

    public double Left { get; }

    public double Top { get; }

    public double Width { get; }

    public double Height { get; }

    public double ControllerBarWidth => ToBarWidth(_snapshot.ControllerValue);

    public double MixerBarWidth => ToBarWidth(_snapshot.MixerValue);

    public string ControllerText => $"C {FormatDisplayValue(_snapshot.ControllerValue)}";

    public string MixerText => $"M {FormatDisplayValue(_snapshot.MixerValue)}";

    public string BindingText =>
        $"{Label}\n" +
        $"MIDI: {FormatMidiBinding(_snapshot.MidiBinding)}\n" +
        $"OSC: {FormatMixerBinding(_snapshot.MixerBinding)}\n" +
        $"Controller: {FormatValue(_snapshot.ControllerValue)}\n" +
        $"Mixer: {FormatValue(_snapshot.MixerValue)}\n" +
        $"Takeover: {(IsLocked ? "locked" : "unlocked")}";

    public Brush StateBrush => _snapshot.IsLocked ? Brushes.DarkOrange : Brushes.LimeGreen;

    public bool IsSelected
    {
        get => _isSelected;
        private set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(SelectionBorderBrush));
                OnPropertyChanged(nameof(SelectionBorderThickness));
            }
        }
    }

    public Brush SelectionBorderBrush => IsSelected ? Brushes.White : Brushes.Transparent;

    public Thickness SelectionBorderThickness => IsSelected ? new Thickness(2) : new Thickness(0);

    public void Update(ControlSlotSnapshot snapshot)
    {
        _snapshot = snapshot;
        OnPropertyChanged(string.Empty);
    }

    public void SetSelected(bool isSelected)
    {
        IsSelected = isSelected;
    }

    private double ToBarWidth(double? value)
    {
        return Math.Clamp(value ?? 0, 0, 1) * Math.Max(0, Width - 16);
    }

    private string FormatDisplayValue(double? value)
    {
        return _snapshot.MixerBinding?.ValueKind == MixerValueKind.Pan
            ? FormatPanValue(value)
            : FormatValue(value);
    }

    private static string FormatValue(double? value)
    {
        return value is null
            ? "--"
            : value.Value.ToString("0.000", CultureInfo.InvariantCulture);
    }

    private static string FormatPanValue(double? value)
    {
        if (value is null)
        {
            return "--";
        }

        double offset = value.Value - 0.5;

        if (Math.Abs(offset) < 0.005)
        {
            return "C";
        }

        int percent = (int)Math.Round(Math.Abs(offset) * 200);
        return offset < 0 ? $"L{percent}" : $"R{percent}";
    }

    private static string FormatMidiBinding(MidiBinding? binding)
    {
        return binding is null
            ? "not assigned"
            : $"{binding.Kind} ch={binding.Channel} #{binding.Number}";
    }

    private static string FormatMixerBinding(MixerBinding? binding)
    {
        return binding is null
            ? "not assigned"
            : $"{binding.OscAddress} ({binding.ValueKind})";
    }
}
