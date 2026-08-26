using System.Runtime.InteropServices;

namespace WECPBXR.Hardware;

public sealed class MidiInputManager : IDisposable
{
    private readonly IMidiInputBackend _backend;

    public MidiInputManager()
    {
        _backend = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? new AlsaMidiInputBackend()
            : new DryWetMidiInputBackend();

        _backend.ControlChanged += OnControlChanged;
        _backend.RawEventReceived += OnRawEventReceived;
    }

    public event EventHandler<MidiControlChangedEventArgs>? ControlChanged;

    public event EventHandler<MidiRawEventReceivedEventArgs>? RawEventReceived;

    public string? ConnectedDeviceName => _backend.ConnectedDeviceName;

    public bool IsConnected => _backend.IsConnected;

    public IReadOnlyList<MidiInputDeviceInfo> GetInputDevices()
    {
        return _backend.GetInputDevices();
    }

    public void ConnectByIndex(int index)
    {
        _backend.ConnectByIndex(index);
    }

    public void ConnectByName(string deviceName)
    {
        _backend.ConnectByName(deviceName);
    }

    public void Disconnect()
    {
        _backend.Disconnect();
    }

    public void Dispose()
    {
        _backend.ControlChanged -= OnControlChanged;
        _backend.RawEventReceived -= OnRawEventReceived;
        _backend.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnControlChanged(object? sender, MidiControlChangedEventArgs eventArgs)
    {
        ControlChanged?.Invoke(this, eventArgs);
    }

    private void OnRawEventReceived(object? sender, MidiRawEventReceivedEventArgs eventArgs)
    {
        RawEventReceived?.Invoke(this, eventArgs);
    }
}
