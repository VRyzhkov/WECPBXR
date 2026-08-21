namespace WECPBXR18.Hardware;

public sealed class MidiRawEventReceivedEventArgs : EventArgs
{
    public MidiRawEventReceivedEventArgs(string deviceName, string rawEvent)
    {
        DeviceName = deviceName;
        RawEvent = rawEvent;
    }

    public string DeviceName { get; }

    public string RawEvent { get; }
}
