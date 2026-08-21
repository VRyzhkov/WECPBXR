namespace WECPBXR18.Core.Models;

public sealed class ControlSlot
{
    public ControlSlot(
        string id,
        string label,
        ControlKind kind,
        MidiBinding? midiBinding = null,
        MixerBinding? mixerBinding = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Control slot id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Control slot label is required.", nameof(label));
        }

        Id = id;
        Label = label;
        Kind = kind;
        MidiBinding = midiBinding;
        MixerBinding = mixerBinding;
    }

    public string Id { get; }

    public string Label { get; }

    public ControlKind Kind { get; }

    public MidiBinding? MidiBinding { get; private set; }

    public MixerBinding? MixerBinding { get; private set; }

    public double? ControllerValue { get; private set; }

    public double? MixerValue { get; private set; }

    public bool IsLocked { get; private set; } = true;

    public ControlSlotSnapshot Snapshot()
    {
        return new ControlSlotSnapshot(
            Id,
            Label,
            Kind,
            MidiBinding,
            MixerBinding,
            ControllerValue,
            MixerValue,
            IsLocked);
    }

    public void SetMidiBinding(MidiBinding? midiBinding)
    {
        MidiBinding = midiBinding;
    }

    public void SetMixerBinding(MixerBinding? mixerBinding)
    {
        MixerBinding = mixerBinding;
    }

    internal void SetControllerValue(double value)
    {
        ControllerValue = Clamp01(value);
    }

    internal void SetMixerValue(double value)
    {
        MixerValue = Clamp01(value);
    }

    internal void SetLocked(bool isLocked)
    {
        IsLocked = isLocked;
    }

    private static double Clamp01(double value)
    {
        return Math.Clamp(value, 0.0, 1.0);
    }
}
