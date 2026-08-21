namespace WECPBXR18.Hardware;

public sealed class MidiControlChangedEventArgs : EventArgs
{
    public MidiControlChangedEventArgs(MidiControlChange change)
    {
        Change = change;
    }

    public MidiControlChange Change { get; }
}
