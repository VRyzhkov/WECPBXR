using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;

namespace WECPBXR18.Hardware;

public sealed class MidiInputManager : IDisposable
{
    private InputDevice? _inputDevice;
    private bool _disposed;

    public event EventHandler<MidiControlChangedEventArgs>? ControlChanged;

    public event EventHandler<MidiRawEventReceivedEventArgs>? RawEventReceived;

    public string? ConnectedDeviceName => _inputDevice?.Name;

    public bool IsConnected => _inputDevice is not null;

    public IReadOnlyList<MidiInputDeviceInfo> GetInputDevices()
    {
        ThrowIfDisposed();

        return InputDevice.GetAll()
            .Select((device, index) => new MidiInputDeviceInfo(index, device.Name))
            .ToArray();
    }

    public void ConnectByIndex(int index)
    {
        ThrowIfDisposed();
        Disconnect();

        InputDevice device = InputDevice.GetByIndex(index);
        Connect(device);
    }

    public void ConnectByName(string deviceName)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("MIDI device name is required.", nameof(deviceName));
        }

        Disconnect();

        InputDevice device = InputDevice.GetByName(deviceName);
        Connect(device);
    }

    public void Disconnect()
    {
        if (_inputDevice is null)
        {
            return;
        }

        _inputDevice.EventReceived -= OnEventReceived;

        if (_inputDevice.IsListeningForEvents)
        {
            _inputDevice.StopEventsListening();
        }

        _inputDevice.Dispose();
        _inputDevice = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Disconnect();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void Connect(InputDevice device)
    {
        _inputDevice = device;
        _inputDevice.EventReceived += OnEventReceived;
        _inputDevice.StartEventsListening();
    }

    private void OnEventReceived(object? sender, MidiEventReceivedEventArgs eventArgs)
    {
        string deviceName = ConnectedDeviceName ?? "Unknown MIDI device";
        string rawEvent = eventArgs.Event.ToString() ?? eventArgs.Event.GetType().Name;

        Console.WriteLine($"MIDI {deviceName}: {rawEvent}");
        RawEventReceived?.Invoke(this, new MidiRawEventReceivedEventArgs(deviceName, rawEvent));

        MidiControlChange? controlChange = TryCreateControlChange(eventArgs.Event, rawEvent);

        if (controlChange is not null)
        {
            ControlChanged?.Invoke(this, new MidiControlChangedEventArgs(controlChange));
        }
    }

    private static MidiControlChange? TryCreateControlChange(MidiEvent midiEvent, string rawEvent)
    {
        return midiEvent switch
        {
            ControlChangeEvent controlChangeEvent => new MidiControlChange(
                MidiControlKind.ControlChange,
                GetOneBasedChannel(controlChangeEvent),
                (int)controlChangeEvent.ControlNumber,
                (int)controlChangeEvent.ControlValue,
                (int)controlChangeEvent.ControlValue / 127.0,
                rawEvent),

            NoteOnEvent noteOnEvent => new MidiControlChange(
                MidiControlKind.NoteOn,
                GetOneBasedChannel(noteOnEvent),
                (int)noteOnEvent.NoteNumber,
                (int)noteOnEvent.Velocity,
                (int)noteOnEvent.Velocity / 127.0,
                rawEvent),

            NoteOffEvent noteOffEvent => new MidiControlChange(
                MidiControlKind.NoteOff,
                GetOneBasedChannel(noteOffEvent),
                (int)noteOffEvent.NoteNumber,
                (int)noteOffEvent.Velocity,
                (int)noteOffEvent.Velocity / 127.0,
                rawEvent),

            PitchBendEvent pitchBendEvent => new MidiControlChange(
                MidiControlKind.PitchBend,
                GetOneBasedChannel(pitchBendEvent),
                number: 0,
                pitchBendEvent.PitchValue,
                pitchBendEvent.PitchValue / 16383.0,
                rawEvent),

            _ => null
        };
    }

    private static int GetOneBasedChannel(ChannelEvent channelEvent)
    {
        return (int)channelEvent.Channel + 1;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
