using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Rug.Osc;
using WECPBXR18.Core.Mapping;
using WECPBXR18.Core.Models;
using WECPBXR18.Hardware;

namespace WECPBXR18.UI.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly BankSet _bankSet;
    private readonly MappingEngine _mappingEngine;
    private readonly MidiMapEditor _mapEditor;
    private readonly MidiInputManager _midi;
    private readonly Dictionary<string, ControlSlotViewModel> _slotLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _mappingLock = new();

    private string _bankTitle = string.Empty;
    private string _bankColorText = string.Empty;
    private Brush _bankBrush = Brushes.Red;
    private Brush _bankTextBrush = Brushes.Black;
    private string _status = "Ready";
    private string _mixerAddress = "192.168.1.100";
    private string _mixerStatus = "XR18: disconnected";
    private string _midiStatus = "MIDI: disconnected";
    private MidiInputDeviceInfo? _selectedMidiDevice;
    private Xr18MixerClient? _mixer;
    private bool _disposed;

    public MainWindowViewModel()
    {
        _bankSet = DefaultControlBankFactory.CreateDefaultBankSet();
        _mappingEngine = new MappingEngine(_bankSet);
        _mapEditor = new MidiMapEditor(_bankSet);
        _midi = new MidiInputManager();

        _mappingEngine.BankChanged += (_, _) => RefreshCurrentBank();
        _mappingEngine.SlotStateChanged += (_, eventArgs) => RunOnUiThread(() => UpdateSlot(eventArgs.Slot));
        _midi.ControlChanged += OnMidiControlChanged;

        LoadMapCommand = new RelayCommand(LoadMap);
        BankPreviousCommand = new RelayCommand(() => _mappingEngine.PreviousBank());
        BankNextCommand = new RelayCommand(() => _mappingEngine.NextBank());
        SimulateFaderCommand = new RelayCommand(SimulateFader);
        SimulateMuteCommand = new RelayCommand(SimulateMute);
        RefreshMidiDevicesCommand = new RelayCommand(RefreshMidiDevices);
        ConnectMidiCommand = new RelayCommand(ConnectMidi);
        DisconnectMidiCommand = new RelayCommand(DisconnectMidi);
        ConnectMixerCommand = new AsyncRelayCommand(ConnectMixerAsync);
        DisconnectMixerCommand = new AsyncRelayCommand(DisconnectMixerAsync);

        TryAutoLoadMap();
        RefreshMidiDevices();
        RefreshCurrentBank();
    }

    public ObservableCollection<ControlSlotViewModel> Knobs { get; } = new();

    public ObservableCollection<ControlSlotViewModel> Faders { get; } = new();

    public ObservableCollection<ControlSlotViewModel> Buttons { get; } = new();

    public ObservableCollection<MidiInputDeviceInfo> MidiDevices { get; } = new();

    public ICommand LoadMapCommand { get; }

    public ICommand BankPreviousCommand { get; }

    public ICommand BankNextCommand { get; }

    public ICommand SimulateFaderCommand { get; }

    public ICommand SimulateMuteCommand { get; }

    public ICommand RefreshMidiDevicesCommand { get; }

    public ICommand ConnectMidiCommand { get; }

    public ICommand DisconnectMidiCommand { get; }

    public ICommand ConnectMixerCommand { get; }

    public ICommand DisconnectMixerCommand { get; }

    public MidiInputDeviceInfo? SelectedMidiDevice
    {
        get => _selectedMidiDevice;
        set => SetProperty(ref _selectedMidiDevice, value);
    }

    public string MixerAddress
    {
        get => _mixerAddress;
        set => SetProperty(ref _mixerAddress, value);
    }

    public string MixerStatus
    {
        get => _mixerStatus;
        private set => SetProperty(ref _mixerStatus, value);
    }

    public string MidiStatus
    {
        get => _midiStatus;
        private set => SetProperty(ref _midiStatus, value);
    }

    public string BankTitle
    {
        get => _bankTitle;
        private set => SetProperty(ref _bankTitle, value);
    }

    public string BankColorText
    {
        get => _bankColorText;
        private set => SetProperty(ref _bankColorText, value);
    }

    public Brush BankBrush
    {
        get => _bankBrush;
        private set => SetProperty(ref _bankBrush, value);
    }

    public Brush BankTextBrush
    {
        get => _bankTextBrush;
        private set => SetProperty(ref _bankTextBrush, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    private void TryAutoLoadMap()
    {
        string path = GetDefaultMidiMapPath();

        if (!File.Exists(path))
        {
            Status = "Map was not found. Built-in default map is used.";
            return;
        }

        try
        {
            _mapEditor.LoadAsync(path).GetAwaiter().GetResult();
            Status = $"Map loaded: {path}";
        }
        catch (Exception exception)
        {
            Status = $"Map load failed: {exception.Message}";
        }
    }

    private void LoadMap()
    {
        TryAutoLoadMap();
        RefreshCurrentBank();
    }

    private void RefreshMidiDevices()
    {
        try
        {
            string? selectedName = SelectedMidiDevice?.Name;
            MidiDevices.Clear();

            foreach (MidiInputDeviceInfo device in _midi.GetInputDevices())
            {
                MidiDevices.Add(device);
            }

            SelectedMidiDevice = MidiDevices.FirstOrDefault(device => device.Name == selectedName)
                ?? MidiDevices.FirstOrDefault();

            MidiStatus = MidiDevices.Count == 0
                ? "MIDI: no input devices"
                : $"MIDI: {MidiDevices.Count} input device(s)";
        }
        catch (Exception exception)
        {
            MidiStatus = $"MIDI: {exception.Message}";
        }
    }

    private void ConnectMidi()
    {
        if (SelectedMidiDevice is null)
        {
            MidiStatus = "MIDI: select input device";
            return;
        }

        try
        {
            _midi.ConnectByIndex(SelectedMidiDevice.Index);
            MidiStatus = $"MIDI: connected {SelectedMidiDevice.Name}";
        }
        catch (Exception exception)
        {
            MidiStatus = $"MIDI: {exception.Message}";
        }
    }

    private void DisconnectMidi()
    {
        _midi.Disconnect();
        MidiStatus = "MIDI: disconnected";
    }

    private async Task ConnectMixerAsync()
    {
        if (string.IsNullOrWhiteSpace(MixerAddress))
        {
            MixerStatus = "XR18: enter address";
            return;
        }

        await DisconnectMixerAsync().ConfigureAwait(true);

        Xr18MixerClient mixer = new(new Xr18ConnectionSettings(MixerAddress.Trim()));
        mixer.MessageReceived += OnMixerMessageReceived;

        try
        {
            MixerStatus = $"XR18: connecting {MixerAddress.Trim()}";
            await mixer.StartAsync().ConfigureAwait(true);
            _mixer = mixer;
            MixerStatus = $"XR18: connected {MixerAddress.Trim()}";
        }
        catch (Exception exception)
        {
            mixer.MessageReceived -= OnMixerMessageReceived;
            await mixer.DisposeAsync().ConfigureAwait(true);
            MixerStatus = $"XR18: {exception.Message}";
        }
    }

    private async Task DisconnectMixerAsync()
    {
        if (_mixer is null)
        {
            MixerStatus = "XR18: disconnected";
            return;
        }

        Xr18MixerClient mixer = _mixer;
        _mixer = null;
        mixer.MessageReceived -= OnMixerMessageReceived;
        await mixer.DisposeAsync().ConfigureAwait(true);
        MixerStatus = "XR18: disconnected";
    }

    private void RefreshCurrentBank()
    {
        ControlBank bank = _mappingEngine.CurrentBank;
        BankTitle = $"{bank.Index + 1}. {bank.Name}";
        BankColorText = $"{bank.Color.ToHexString()} rgb=({bank.Color.Red},{bank.Color.Green},{bank.Color.Blue})";
        BankBrush = new SolidColorBrush(Color.FromRgb(bank.Color.Red, bank.Color.Green, bank.Color.Blue));
        BankTextBrush = GetReadableTextBrush(bank.Color);

        _slotLookup.Clear();
        Knobs.Clear();
        Faders.Clear();
        Buttons.Clear();

        foreach (ControlSlot slot in bank.Slots)
        {
            ControlSlotViewModel viewModel = CreateSlotViewModel(slot.Snapshot());
            _slotLookup[viewModel.Id] = viewModel;

            switch (slot.Kind)
            {
                case ControlKind.Knob:
                    Knobs.Add(viewModel);
                    break;

                case ControlKind.Fader:
                    Faders.Add(viewModel);
                    break;

                case ControlKind.Button:
                    Buttons.Add(viewModel);
                    break;
            }
        }
    }

    private void UpdateSlot(ControlSlotSnapshot snapshot)
    {
        if (_slotLookup.TryGetValue(snapshot.Id, out ControlSlotViewModel? slot))
        {
            slot.Update(snapshot);
        }
    }

    private void SimulateFader()
    {
        MappingResult result;

        lock (_mappingLock)
        {
            _mappingEngine.HandleMixerChange(new MixerValueChange("/ch/01/mix/fader", 0.62));
            result = _mappingEngine.HandleControllerChange(new ControllerInputChange(
                MidiMessageKind.ControlChange,
                Channel: 1,
                Number: 26,
                Value: 79 / 127.0,
                RawEvent: "ui sim fader"));
        }

        Status = DescribeResult("Fader simulation", result);
    }

    private void SimulateMute()
    {
        MappingResult result;

        lock (_mappingLock)
        {
            _mappingEngine.HandleMixerChange(new MixerValueChange("/ch/01/mix/on", 1));
            result = _mappingEngine.HandleControllerChange(new ControllerInputChange(
                MidiMessageKind.NoteOn,
                Channel: 1,
                Number: 34,
                Value: 1,
                RawEvent: "ui sim mute"));
        }

        Status = DescribeResult("Mute simulation", result);
    }

    private async void OnMidiControlChanged(object? sender, MidiControlChangedEventArgs eventArgs)
    {
        MappingResult result;

        lock (_mappingLock)
        {
            result = _mappingEngine.HandleControllerChange(ToControllerInputChange(eventArgs.Change));
        }

        RunOnUiThread(() =>
        {
            MidiStatus = $"MIDI: {eventArgs.Change.Kind} ch={eventArgs.Change.Channel} #{eventArgs.Change.Number} value={eventArgs.Change.Value}";
            Status = DescribeResult("MIDI", result);
        });

        if (result.MixerCommand is not null && _mixer is not null)
        {
            try
            {
                await _mixer.SendOscValueAsync(
                    result.MixerCommand.OscAddress,
                    result.MixerCommand.Value,
                    sendInteger: result.MixerCommand.ValueKind == MixerValueKind.Toggle).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                RunOnUiThread(() => MixerStatus = $"XR18 send: {exception.Message}");
            }
        }
    }

    private void OnMixerMessageReceived(object? sender, Xr18OscMessageReceivedEventArgs eventArgs)
    {
        if (!TryCreateMixerValueChange(eventArgs.Message.Address, GetFirstOscArgument(eventArgs.Message), out MixerValueChange? change) ||
            change is null)
        {
            return;
        }

        MappingResult result;

        lock (_mappingLock)
        {
            result = _mappingEngine.HandleMixerChange(change);
        }

        if (result.IsMapped)
        {
            RunOnUiThread(() =>
            {
                MixerStatus = FormattableString.Invariant($"XR18: {change.OscAddress}={change.Value:0.###}");
                Status = DescribeResult("XR18", result);
            });
        }
    }

    private static ControlSlotViewModel CreateSlotViewModel(ControlSlotSnapshot snapshot)
    {
        string[] idParts = snapshot.Id.Split('-', StringSplitOptions.RemoveEmptyEntries);
        int number = idParts.Length == 2 && int.TryParse(idParts[1], out int parsedNumber) ? parsedNumber : 1;

        return snapshot.Kind switch
        {
            ControlKind.Knob => CreateKnob(snapshot, number),
            ControlKind.Fader => CreateFader(snapshot, number),
            ControlKind.Button => CreateButton(snapshot, number),
            _ => new ControlSlotViewModel(snapshot, 0, 0, 80, 80)
        };
    }

    private static ControlSlotViewModel CreateKnob(ControlSlotSnapshot snapshot, int number)
    {
        int column = (number - 1) % 8;
        int row = (number - 1) / 8;
        return new ControlSlotViewModel(snapshot, 185 + column * 92, 42 + row * 112, 76, 104);
    }

    private static ControlSlotViewModel CreateFader(ControlSlotSnapshot snapshot, int number)
    {
        return new ControlSlotViewModel(snapshot, 82 + (number - 1) * 92, 488, 78, 178);
    }

    private static ControlSlotViewModel CreateButton(ControlSlotSnapshot snapshot, int number)
    {
        int column = (number - 1) % 8;
        int row = (number - 1) / 8;
        return new ControlSlotViewModel(snapshot, 188 + column * 92, 382 + row * 58, 72, 44);
    }

    private static string DescribeResult(string prefix, MappingResult result)
    {
        if (result.MixerCommand is not null)
        {
            return FormattableString.Invariant(
                $"{prefix}: OSC ready {result.MixerCommand.OscAddress} {result.MixerCommand.Value:0.###}");
        }

        return result.Message is null
            ? $"{prefix}: mapped"
            : $"{prefix}: {result.Message}";
    }

    private static ControllerInputChange ToControllerInputChange(MidiControlChange change)
    {
        return new ControllerInputChange(
            ToCoreMidiMessageKind(change.Kind),
            change.Channel,
            change.Number,
            change.NormalizedValue,
            change.RawEvent);
    }

    private static MidiMessageKind ToCoreMidiMessageKind(WECPBXR18.Hardware.MidiControlKind kind)
    {
        return kind switch
        {
            WECPBXR18.Hardware.MidiControlKind.ControlChange => MidiMessageKind.ControlChange,
            WECPBXR18.Hardware.MidiControlKind.NoteOn => MidiMessageKind.NoteOn,
            WECPBXR18.Hardware.MidiControlKind.NoteOff => MidiMessageKind.NoteOff,
            WECPBXR18.Hardware.MidiControlKind.PitchBend => MidiMessageKind.PitchBend,
            _ => MidiMessageKind.Other
        };
    }

    private static object? GetFirstOscArgument(OscMessage message)
    {
        return message.Count == 0 ? null : message[0];
    }

    private static bool TryCreateMixerValueChange(string oscAddress, object? value, out MixerValueChange? change)
    {
        change = null;

        if (!TryReadOscNumber(value, out double number))
        {
            return false;
        }

        change = new MixerValueChange(oscAddress, number);
        return true;
    }

    private static bool TryReadOscNumber(object? value, out double number)
    {
        switch (value)
        {
            case float floatValue:
                number = floatValue;
                return true;

            case double doubleValue:
                number = doubleValue;
                return true;

            case int intValue:
                number = intValue;
                return true;

            case bool boolValue:
                number = boolValue ? 1.0 : 0.0;
                return true;

            default:
                number = 0;
                return false;
        }
    }

    private static string GetDefaultMidiMapPath()
    {
        string outputPath = Path.Combine(AppContext.BaseDirectory, "midi-map.json");

        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        string sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "midi-map.json"));

        return File.Exists(sourcePath) ? sourcePath : outputPath;
    }

    private static Brush GetReadableTextBrush(RgbColor color)
    {
        double luminance = (0.299 * color.Red) + (0.587 * color.Green) + (0.114 * color.Blue);
        return luminance > 150 ? Brushes.Black : Brushes.White;
    }

    private static void RunOnUiThread(Action action)
    {
        Application.Current.Dispatcher.Invoke(action);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _midi.ControlChanged -= OnMidiControlChanged;
        _midi.Dispose();

        if (_mixer is not null)
        {
            _mixer.MessageReceived -= OnMixerMessageReceived;
            _mixer.Dispose();
            _mixer = null;
        }

        _disposed = true;
    }
}
