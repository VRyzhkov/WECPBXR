namespace WECPBXR18.Hardware;

public sealed class MidiControlChange
{
    public MidiControlChange(
        MidiControlKind kind,
        int channel,
        int number,
        int value,
        double normalizedValue,
        string rawEvent)
    {
        Kind = kind;
        Channel = channel;
        Number = number;
        Value = value;
        NormalizedValue = normalizedValue;
        RawEvent = rawEvent;
    }

    public MidiControlKind Kind { get; }

    public int Channel { get; }

    public int Number { get; }

    public int Value { get; }

    public double NormalizedValue { get; }

    public string RawEvent { get; }
}
