using System.Globalization;
using System.Windows.Media;
using WECPBXR18.Core.Models;

namespace WECPBXR18.UI.ViewModels;

public sealed class ControlSlotViewModel : ObservableObject
{
    private ControlSlotSnapshot _snapshot;

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

    public string ControllerText => $"C {FormatValue(_snapshot.ControllerValue)}";

    public string MixerText => $"M {FormatValue(_snapshot.MixerValue)}";

    public string BindingText => _snapshot.MixerBinding?.OscAddress ?? "not assigned";

    public Brush StateBrush => _snapshot.IsLocked ? Brushes.DarkOrange : Brushes.LimeGreen;

    public void Update(ControlSlotSnapshot snapshot)
    {
        _snapshot = snapshot;
        OnPropertyChanged(string.Empty);
    }

    private double ToBarWidth(double? value)
    {
        return Math.Clamp(value ?? 0, 0, 1) * Math.Max(0, Width - 16);
    }

    private static string FormatValue(double? value)
    {
        return value is null
            ? "--"
            : value.Value.ToString("0.000", CultureInfo.InvariantCulture);
    }
}
