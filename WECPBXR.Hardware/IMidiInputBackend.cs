namespace WECPBXR.Hardware;

internal interface IMidiInputBackend : IDisposable
{
    event EventHandler<MidiControlChangedEventArgs>? ControlChanged;

    event EventHandler<MidiRawEventReceivedEventArgs>? RawEventReceived;

    string? ConnectedDeviceName { get; }

    bool IsConnected { get; }

    IReadOnlyList<MidiInputDeviceInfo> GetInputDevices();

    void ConnectByIndex(int index);

    void ConnectByName(string deviceName);

    void Disconnect();
}
