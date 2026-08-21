namespace WECPBXR.Hardware;

public sealed class MidiRawEventReceivedEventArgs(string deviceName, string rawEvent) : EventArgs
{
    public string DeviceName { get; } = deviceName;

    public string RawEvent { get; } = rawEvent;
}
