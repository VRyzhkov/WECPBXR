using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WECPBXR.Hardware;

internal sealed class AlsaMidiInputBackend : IMidiInputBackend
{
    private const string AlsaLibrary = "libasound.so.2";
    private const int OpenInput = 2;
    private const int PortCapabilityRead = 1;
    private const int PortCapabilityWrite = 2;
    private const int PortCapabilitySubscriptionRead = 32;
    private const int PortCapabilitySubscriptionWrite = 64;
    private const int PortTypeMidiGeneric = 2;
    private const int PortTypeApplication = 1 << 20;
    private const int EventBufferSize = 32;
    private const int ErrorAgain = 11;

    private readonly object _sync = new();
    private readonly List<MidiInputDeviceInfo> _devices = [];
    private IntPtr _seq;
    private IntPtr _midiEventDecoder;
    private Thread? _readerThread;
    private volatile bool _stopReader;
    private int _inputPort = -1;
    private string? _connectedDeviceName;
    private bool _disposed;

    public event EventHandler<MidiControlChangedEventArgs>? ControlChanged;

    public event EventHandler<MidiRawEventReceivedEventArgs>? RawEventReceived;

    public string? ConnectedDeviceName => _connectedDeviceName;

    public bool IsConnected => _seq != IntPtr.Zero;

    public IReadOnlyList<MidiInputDeviceInfo> GetInputDevices()
    {
        ThrowIfDisposed();

        List<MidiInputDeviceInfo> devices = ExecuteWithAlsaLibraryHint(() =>
        {
            using AlsaSequencerHandle seq = OpenSequencer();
            return EnumerateInputPorts(seq.Handle);
        });

        lock (_sync)
        {
            _devices.Clear();
            _devices.AddRange(devices);
        }

        return devices;
    }

    public void ConnectByIndex(int index)
    {
        ThrowIfDisposed();
        Disconnect();

        MidiInputDeviceInfo device = ResolveDeviceByIndex(index);

        if (device.Client < 0 || device.Port < 0)
        {
            throw new InvalidOperationException($"ALSA MIDI device '{device.Name}' does not expose a sequencer address.");
        }

        IntPtr seq = IntPtr.Zero;
        IntPtr decoder = IntPtr.Zero;

        ExecuteWithAlsaLibraryHint(() =>
        {
            try
            {
                CheckError(snd_seq_open(out seq, "default", OpenInput, 0), "open ALSA sequencer");
                CheckError(snd_seq_set_client_name(seq, "WECPBXR"), "set ALSA client name");
                CheckError(snd_seq_nonblock(seq, 1), "set ALSA sequencer non-blocking mode");

                int inputPort = snd_seq_create_simple_port(
                    seq,
                    "MIDI input",
                    PortCapabilityWrite | PortCapabilitySubscriptionWrite,
                    PortTypeMidiGeneric | PortTypeApplication);
                CheckError(inputPort, "create ALSA MIDI input port");

                CheckError(snd_seq_connect_from(seq, inputPort, device.Client, device.Port), $"connect ALSA MIDI device '{device.Name}'");
                CheckError(snd_midi_event_new(EventBufferSize, out decoder), "create ALSA MIDI decoder");
                snd_midi_event_no_status(decoder, 1);

                lock (_sync)
                {
                    _seq = seq;
                    _midiEventDecoder = decoder;
                    _inputPort = inputPort;
                    _connectedDeviceName = device.Name;
                    _stopReader = false;
                }

                seq = IntPtr.Zero;
                decoder = IntPtr.Zero;

                _readerThread = new Thread(ReadEvents)
                {
                    IsBackground = true,
                    Name = "WECPBXR ALSA MIDI input"
                };
                _readerThread.Start();
            }
            finally
            {
                if (decoder != IntPtr.Zero)
                {
                    snd_midi_event_free(decoder);
                }

                if (seq != IntPtr.Zero)
                {
                    snd_seq_close(seq);
                }
            }

            return true;
        });
    }

    public void ConnectByName(string deviceName)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("MIDI device name is required.", nameof(deviceName));
        }

        MidiInputDeviceInfo? device = GetInputDevices()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, deviceName, StringComparison.OrdinalIgnoreCase));

        if (device is null)
        {
            throw new InvalidOperationException($"MIDI input device '{deviceName}' was not found.");
        }

        ConnectByIndex(device.Index);
    }

    public void Disconnect()
    {
        Thread? readerThread;
        IntPtr seq;
        IntPtr decoder;

        lock (_sync)
        {
            _stopReader = true;
            readerThread = _readerThread;
            _readerThread = null;
            seq = _seq;
            decoder = _midiEventDecoder;
            _seq = IntPtr.Zero;
            _midiEventDecoder = IntPtr.Zero;
            _inputPort = -1;
            _connectedDeviceName = null;
        }

        if (readerThread is not null && readerThread.IsAlive)
        {
            readerThread.Join(TimeSpan.FromSeconds(1));
        }

        if (decoder != IntPtr.Zero)
        {
            snd_midi_event_free(decoder);
        }

        if (seq != IntPtr.Zero)
        {
            snd_seq_close(seq);
        }
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

    private MidiInputDeviceInfo ResolveDeviceByIndex(int index)
    {
        List<MidiInputDeviceInfo> devices;

        lock (_sync)
        {
            devices = [.. _devices];
        }

        if (devices.Count == 0 || devices.All(device => device.Index != index))
        {
            devices = [.. GetInputDevices()];
        }

        return devices.FirstOrDefault(device => device.Index == index)
            ?? throw new ArgumentOutOfRangeException(nameof(index), index, "MIDI input device index was not found.");
    }

    private void ReadEvents()
    {
        byte[] buffer = new byte[EventBufferSize];

        while (!_stopReader)
        {
            IntPtr seq = _seq;
            IntPtr decoder = _midiEventDecoder;

            if (seq == IntPtr.Zero || decoder == IntPtr.Zero)
            {
                break;
            }

            int result = snd_seq_event_input(seq, out IntPtr ev);

            if (result == -ErrorAgain)
            {
                Thread.Sleep(5);
                continue;
            }

            if (result < 0)
            {
                Thread.Sleep(25);
                continue;
            }

            long decodedLength = snd_midi_event_decode(decoder, buffer, buffer.Length, ev).ToInt64();

            if (decodedLength <= 0)
            {
                continue;
            }

            MidiControlChange? controlChange = TryCreateControlChange(buffer.AsSpan(0, (int)decodedLength), out string rawEvent);
            string deviceName = ConnectedDeviceName ?? "Unknown ALSA MIDI device";

            Console.WriteLine($"MIDI {deviceName}: {rawEvent}");
            RawEventReceived?.Invoke(this, new MidiRawEventReceivedEventArgs(deviceName, rawEvent));

            if (controlChange is not null)
            {
                ControlChanged?.Invoke(this, new MidiControlChangedEventArgs(controlChange));
            }
        }
    }

    private static MidiControlChange? TryCreateControlChange(ReadOnlySpan<byte> message, out string rawEvent)
    {
        rawEvent = FormatRawEvent(message);

        if (message.Length == 0)
        {
            return null;
        }

        int status = message[0];
        int kind = status & 0xF0;
        int channel = (status & 0x0F) + 1;

        return kind switch
        {
            0x80 when message.Length >= 3 => new MidiControlChange(
                MidiControlKind.NoteOff,
                channel,
                message[1],
                message[2],
                message[2] / 127.0,
                rawEvent),

            0x90 when message.Length >= 3 => new MidiControlChange(
                message[2] == 0 ? MidiControlKind.NoteOff : MidiControlKind.NoteOn,
                channel,
                message[1],
                message[2],
                message[2] / 127.0,
                rawEvent),

            0xB0 when message.Length >= 3 => new MidiControlChange(
                MidiControlKind.ControlChange,
                channel,
                message[1],
                message[2],
                message[2] / 127.0,
                rawEvent),

            0xE0 when message.Length >= 3 => CreatePitchBend(channel, message[1], message[2], rawEvent),

            _ => null
        };
    }

    private static MidiControlChange CreatePitchBend(int channel, int leastSignificant, int mostSignificant, string rawEvent)
    {
        int value = leastSignificant | (mostSignificant << 7);

        return new MidiControlChange(
            MidiControlKind.PitchBend,
            channel,
            number: 0,
            value,
            value / 16383.0,
            rawEvent);
    }

    private static string FormatRawEvent(ReadOnlySpan<byte> message)
    {
        return $"ALSA MIDI bytes: {string.Join(' ', message.ToArray().Select(value => value.ToString("X2")))}";
    }

    private static List<MidiInputDeviceInfo> EnumerateInputPorts(IntPtr seq)
    {
        List<MidiInputDeviceInfo> devices = [];
        IntPtr clientInfo = IntPtr.Zero;
        IntPtr portInfo = IntPtr.Zero;

        try
        {
            CheckError(snd_seq_client_info_malloc(out clientInfo), "allocate ALSA client info");
            CheckError(snd_seq_port_info_malloc(out portInfo), "allocate ALSA port info");
            snd_seq_client_info_set_client(clientInfo, -1);

            while (snd_seq_query_next_client(seq, clientInfo) >= 0)
            {
                int client = snd_seq_client_info_get_client(clientInfo);
                snd_seq_port_info_set_client(portInfo, client);
                snd_seq_port_info_set_port(portInfo, -1);

                while (snd_seq_query_next_port(seq, portInfo) >= 0)
                {
                    int capability = snd_seq_port_info_get_capability(portInfo);

                    if ((capability & (PortCapabilityRead | PortCapabilitySubscriptionRead)) !=
                        (PortCapabilityRead | PortCapabilitySubscriptionRead))
                    {
                        continue;
                    }

                    int port = snd_seq_port_info_get_port(portInfo);
                    string clientName = Marshal.PtrToStringAnsi(snd_seq_client_info_get_name(clientInfo)) ?? $"Client {client}";
                    string portName = Marshal.PtrToStringAnsi(snd_seq_port_info_get_name(portInfo)) ?? $"Port {port}";
                    string name = $"{clientName}: {portName} ({client}:{port})";
                    devices.Add(new MidiInputDeviceInfo(devices.Count, name, client, port));
                }
            }
        }
        finally
        {
            if (portInfo != IntPtr.Zero)
            {
                snd_seq_port_info_free(portInfo);
            }

            if (clientInfo != IntPtr.Zero)
            {
                snd_seq_client_info_free(clientInfo);
            }
        }

        return devices;
    }

    private static AlsaSequencerHandle OpenSequencer()
    {
        CheckError(snd_seq_open(out IntPtr seq, "default", OpenInput, 0), "open ALSA sequencer");
        return new AlsaSequencerHandle(seq);
    }

    private static void CheckError(int result, string operation)
    {
        if (result >= 0)
        {
            return;
        }

        string message = Marshal.PtrToStringAnsi(snd_strerror(result)) ?? $"ALSA error {result}";
        throw new InvalidOperationException($"Failed to {operation}: {message}", new Win32Exception(-result));
    }

    private static T ExecuteWithAlsaLibraryHint<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (DllNotFoundException exception)
        {
            throw new InvalidOperationException("ALSA MIDI support requires libasound2. Install it with: sudo apt install libasound2 alsa-utils", exception);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int snd_seq_open(out IntPtr seq, string name, int streams, int mode);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int snd_seq_close(IntPtr seq);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int snd_seq_set_client_name(IntPtr seq, string name);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int snd_seq_nonblock(IntPtr seq, int nonblock);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int snd_seq_create_simple_port(IntPtr seq, string name, int caps, int type);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int snd_seq_connect_from(IntPtr seq, int myPort, int srcClient, int srcPort);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int snd_seq_event_input(IntPtr seq, out IntPtr ev);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int snd_seq_client_info_malloc(out IntPtr ptr);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern void snd_seq_client_info_free(IntPtr ptr);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern void snd_seq_client_info_set_client(IntPtr ptr, int client);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int snd_seq_client_info_get_client(IntPtr ptr);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr snd_seq_client_info_get_name(IntPtr ptr);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int snd_seq_query_next_client(IntPtr seq, IntPtr info);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int snd_seq_port_info_malloc(out IntPtr ptr);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern void snd_seq_port_info_free(IntPtr ptr);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern void snd_seq_port_info_set_client(IntPtr ptr, int client);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern void snd_seq_port_info_set_port(IntPtr ptr, int port);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int snd_seq_port_info_get_port(IntPtr ptr);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int snd_seq_port_info_get_capability(IntPtr ptr);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr snd_seq_port_info_get_name(IntPtr ptr);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int snd_seq_query_next_port(IntPtr seq, IntPtr info);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int snd_midi_event_new(IntPtr bufsize, out IntPtr rdev);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern void snd_midi_event_free(IntPtr dev);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern void snd_midi_event_no_status(IntPtr dev, int on);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr snd_midi_event_decode(IntPtr dev, byte[] buf, IntPtr count, IntPtr ev);

    [DllImport(AlsaLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr snd_strerror(int errnum);

    private sealed class AlsaSequencerHandle(IntPtr handle) : IDisposable
    {
        public IntPtr Handle { get; } = handle;

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                snd_seq_close(Handle);
            }
        }
    }
}
