namespace WECPBXR.Hardware;

public sealed class MidiControlChange(
    MidiControlKind kind,
    int channel,
    int number,
    int value,
    double normalizedValue,
    string rawEvent)
{
    public MidiControlKind Kind { get; } = kind;

    public int Channel { get; } = channel;

    public int Number { get; } = number;

    public int Value { get; } = value;

    public double NormalizedValue { get; } = normalizedValue;

    public string RawEvent { get; } = rawEvent;
}
