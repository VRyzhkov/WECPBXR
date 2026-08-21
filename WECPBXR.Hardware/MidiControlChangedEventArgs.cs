namespace WECPBXR.Hardware;

public sealed class MidiControlChangedEventArgs(
    MidiControlChange change) : EventArgs
{
    public MidiControlChange Change { get; } = change;
}
