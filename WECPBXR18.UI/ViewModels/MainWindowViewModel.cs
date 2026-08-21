using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
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
    private const int WorkSurfaceOffsetY = 46;
    private const int LowerBlockOffsetY = 64;

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
    private bool _isAssignmentMode;
    private bool _isLearningMidi;
    private bool _isLogVisible;
    private bool _isMapDirty;
    private string _selectedAssignmentCommand = "main";
    private string _assignmentChannel = "1";
    private string _assignmentIndex = "1";
    private string _selectedSlotText = "slot: none";
    private string _saveMapText = "Save";
    private Brush _mixerIndicatorBrush = Brushes.DimGray;
    private Brush _midiIndicatorBrush = Brushes.DimGray;
    private ControlSlotViewModel? _selectedSlot;
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
        ToggleAssignmentModeCommand = new RelayCommand(ToggleAssignmentMode);
        ApplyAssignmentCommand = new RelayCommand(ApplyAssignment);
        ClearAssignmentCommand = new RelayCommand(ClearAssignment);
        SaveMapCommand = new RelayCommand(SaveMap);
        ToggleLogCommand = new RelayCommand(ToggleLog);
        LearnMidiCommand = new RelayCommand(StartLearnMidi);
        CheckMapCommand = new RelayCommand(CheckMap);
        RequestMixerValuesCommand = new AsyncRelayCommand(RequestMixerValuesAsync);

        foreach (MixerCommandDefinition command in _mapEditor.CommandCatalog.Commands.OrderBy(command => command.Key))
        {
            AssignmentCommands.Add(command.Key);
        }

        TryAutoLoadMap();
        RefreshMidiDevices();
        RefreshCurrentBank();
    }

    public ObservableCollection<ControlSlotViewModel> Knobs { get; } = new();

    public ObservableCollection<ControlSlotViewModel> Faders { get; } = new();

    public ObservableCollection<ControlSlotViewModel> Buttons { get; } = new();

    public ObservableCollection<MidiInputDeviceInfo> MidiDevices { get; } = new();

    public ObservableCollection<string> AssignmentCommands { get; } = new();

    public ObservableCollection<string> LogEntries { get; } = new();

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

    public ICommand ToggleAssignmentModeCommand { get; }

    public ICommand ApplyAssignmentCommand { get; }

    public ICommand ClearAssignmentCommand { get; }

    public ICommand SaveMapCommand { get; }

    public string SoftwareVersionText => $"Software version {GetApplicationVersion()}";

    public ICommand ToggleLogCommand { get; }

    public ICommand LearnMidiCommand { get; }

    public ICommand CheckMapCommand { get; }

    public ICommand RequestMixerValuesCommand { get; }

    public bool IsAssignmentMode
    {
        get => _isAssignmentMode;
        private set
        {
            if (SetProperty(ref _isAssignmentMode, value))
            {
                OnPropertyChanged(nameof(NormalPanelVisibility));
                OnPropertyChanged(nameof(AssignmentPanelVisibility));
                OnPropertyChanged(nameof(AssignmentModeText));
            }
        }
    }

    public Visibility NormalPanelVisibility => IsAssignmentMode ? Visibility.Collapsed : Visibility.Visible;

    public Visibility AssignmentPanelVisibility => IsAssignmentMode ? Visibility.Visible : Visibility.Collapsed;

    public string AssignmentModeText => IsAssignmentMode ? "Assign on" : "Assign";

    public Visibility LogVisibility => IsLogVisible ? Visibility.Visible : Visibility.Collapsed;

    public string LogToggleText => IsLogVisible ? "Log -" : "Log +";

    public bool IsLogVisible
    {
        get => _isLogVisible;
        private set
        {
            if (SetProperty(ref _isLogVisible, value))
            {
                OnPropertyChanged(nameof(LogVisibility));
                OnPropertyChanged(nameof(LogToggleText));
            }
        }
    }

    public string SaveMapText
    {
        get => _saveMapText;
        private set => SetProperty(ref _saveMapText, value);
    }

    public Brush MixerIndicatorBrush
    {
        get => _mixerIndicatorBrush;
        private set => SetProperty(ref _mixerIndicatorBrush, value);
    }

    public Brush MidiIndicatorBrush
    {
        get => _midiIndicatorBrush;
        private set => SetProperty(ref _midiIndicatorBrush, value);
    }

    public string SelectedAssignmentCommand
    {
        get => _selectedAssignmentCommand;
        set => SetProperty(ref _selectedAssignmentCommand, value);
    }

    public string AssignmentChannel
    {
        get => _assignmentChannel;
        set => SetProperty(ref _assignmentChannel, value);
    }

    public string AssignmentIndex
    {
        get => _assignmentIndex;
        set => SetProperty(ref _assignmentIndex, value);
    }

    public string SelectedSlotText
    {
        get => _selectedSlotText;
        private set => SetProperty(ref _selectedSlotText, value);
    }

    public bool IsMapDirty
    {
        get => _isMapDirty;
        private set
        {
            if (SetProperty(ref _isMapDirty, value))
            {
                SaveMapText = value ? "Save*" : "Save";
            }
        }
    }

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
            SetStatus("Map was not found. Built-in default map is used.");
            return;
        }

        try
        {
            _mapEditor.LoadAsync(path).GetAwaiter().GetResult();
            IsMapDirty = false;
            SetStatus("Map loaded.");
        }
        catch (Exception exception)
        {
            SetStatus($"Map load failed: {exception.Message}");
        }
    }

    private void LoadMap()
    {
        TryAutoLoadMap();
        RefreshCurrentBank();
    }

    private void SaveMap()
    {
        try
        {
            string path = GetDefaultMidiMapPath();
            _mapEditor.SaveAsync(path).GetAwaiter().GetResult();

            string sourcePath = GetSourceMidiMapPath();
            if (File.Exists(sourcePath) && !string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                _mapEditor.SaveAsync(sourcePath).GetAwaiter().GetResult();
            }

            IsMapDirty = false;
            SetStatus("Map saved.");
        }
        catch (Exception exception)
        {
            SetStatus($"Map save failed: {exception.Message}");
        }
    }

    private void ToggleAssignmentMode()
    {
        IsAssignmentMode = !IsAssignmentMode;
        SetStatus(IsAssignmentMode
            ? "Assignment mode: click a control, choose command, channel/index, then Set."
            : "Assignment mode off.");
    }

    private void ToggleLog()
    {
        IsLogVisible = !IsLogVisible;
    }

    public void SelectSlot(ControlSlotViewModel slot)
    {
        _selectedSlot?.SetSelected(false);
        _selectedSlot = slot;
        _selectedSlot.SetSelected(true);
        SelectedSlotText = $"slot: {slot.Id}";

        ControlSlot coreSlot = _mapEditor.GetSlot(_mappingEngine.CurrentBank.Index, slot.Id);
        LoadAssignmentFields(coreSlot);
        NormalizeSlotLabel(_mappingEngine.CurrentBank.Index, coreSlot);
        slot.Update(coreSlot.Snapshot());
        slot.SetSelected(true);

        SetStatus(coreSlot.MixerBinding is null
            ? $"Selected {slot.Id}: no OSC binding"
            : $"Selected {slot.Id}: {coreSlot.MixerBinding.OscAddress}");
    }

    private void ApplyAssignment()
    {
        if (_selectedSlot is null)
        {
            SetStatus("Assignment: click a control first.");
            return;
        }

        if (!int.TryParse(AssignmentChannel, out int channel))
        {
            SetStatus("Assignment: channel must be 1-18.");
            return;
        }

        int? index = string.IsNullOrWhiteSpace(AssignmentIndex)
            ? null
            : int.TryParse(AssignmentIndex, out int parsedIndex)
                ? parsedIndex
                : null;

        if (!string.IsNullOrWhiteSpace(AssignmentIndex) && index is null)
        {
            SetStatus("Assignment: index must be a number or empty.");
            return;
        }

        try
        {
            int bankIndex = _mappingEngine.CurrentBank.Index;
            _mapEditor.AssignMixerCommand(bankIndex, _selectedSlot.Id, SelectedAssignmentCommand, channel, index);
            _mapEditor.SetSlotLabel(bankIndex, _selectedSlot.Id, CreateAssignmentLabel(SelectedAssignmentCommand, channel, index));
            ControlSlot slot = _mapEditor.GetSlot(bankIndex, _selectedSlot.Id);
            _selectedSlot.Update(slot.Snapshot());
            _selectedSlot.SetSelected(true);
            IsMapDirty = true;
            SetStatus($"Assigned {_selectedSlot.Id}: {slot.MixerBinding?.OscAddress}");
        }
        catch (Exception exception)
        {
            SetStatus($"Assignment failed: {exception.Message}");
        }
    }

    private void ClearAssignment()
    {
        if (_selectedSlot is null)
        {
            SetStatus("Assignment: click a control first.");
            return;
        }

        int bankIndex = _mappingEngine.CurrentBank.Index;
        _mapEditor.SetMixerBinding(bankIndex, _selectedSlot.Id, null);
        _mapEditor.SetSlotLabel(bankIndex, _selectedSlot.Id, CreateUnassignedLabel(_selectedSlot.Id));
        ControlSlot slot = _mapEditor.GetSlot(bankIndex, _selectedSlot.Id);
        _selectedSlot.Update(slot.Snapshot());
        _selectedSlot.SetSelected(true);
        IsMapDirty = true;
        SetStatus($"Cleared {_selectedSlot.Id}.");
    }

    private void StartLearnMidi()
    {
        if (_selectedSlot is null)
        {
            SetStatus("Learn MIDI: click a control first.");
            return;
        }

        _isLearningMidi = true;
        SetStatus($"Learn MIDI: move a physical control for {_selectedSlot.Id}.");
    }

    private void CheckMap()
    {
        int missingOsc = 0;
        int unknownOsc = 0;
        List<string> duplicateMidi = new();
        Dictionary<string, List<string>> midiSlots = new(StringComparer.OrdinalIgnoreCase);

        foreach (ControlBank bank in _mappingEngine.Banks)
        {
            foreach (ControlSlot slot in bank.Slots)
            {
                if (slot.MixerBinding is null)
                {
                    missingOsc++;
                }
                else if (!TryResolveAssignment(slot.MixerBinding, out _))
                {
                    unknownOsc++;
                }

                if (slot.MidiBinding is null)
                {
                    continue;
                }

                string midiKey = $"{slot.MidiBinding.Kind}:{slot.MidiBinding.Channel}:{slot.MidiBinding.Number}";
                string slotKey = $"B{bank.Index + 1}/{slot.Id}";

                if (!midiSlots.TryGetValue(midiKey, out List<string>? slots))
                {
                    slots = new List<string>();
                    midiSlots[midiKey] = slots;
                }

                slots.Add(slotKey);
            }
        }

        foreach ((string midiKey, List<string> slots) in midiSlots)
        {
            if (slots.Count > 1)
            {
                duplicateMidi.Add($"{midiKey} -> {string.Join(", ", slots.Take(3))}");
            }
        }

        string summary = $"Map check: missing OSC={missingOsc}, unknown OSC={unknownOsc}, duplicate MIDI={duplicateMidi.Count}.";
        SetStatus(summary);

        foreach (string duplicate in duplicateMidi.Take(3))
        {
            AddLog($"Duplicate MIDI: {duplicate}");
        }
    }

    private async Task RequestMixerValuesAsync()
    {
        if (_mixer is null)
        {
            SetStatus("XR18 pull: mixer is not connected.");
            return;
        }

        string[] addresses = _mappingEngine.CurrentBank.Slots
            .Select(slot => slot.MixerBinding?.OscAddress)
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();

        try
        {
            foreach (string address in addresses)
            {
                await _mixer.RequestOscValueAsync(address).ConfigureAwait(true);
            }

            SetStatus($"XR18 pull: requested {addresses.Length} value(s).");
        }
        catch (Exception exception)
        {
            SetStatus($"XR18 pull failed: {exception.Message}");
        }
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
            AddLog(MidiStatus);
        }
        catch (Exception exception)
        {
            MidiStatus = $"MIDI: {exception.Message}";
            AddLog(MidiStatus);
        }
    }

    private void ConnectMidi()
    {
        if (SelectedMidiDevice is null)
        {
            MidiStatus = "MIDI: select input device";
            AddLog(MidiStatus);
            return;
        }

        try
        {
            _midi.ConnectByIndex(SelectedMidiDevice.Index);
            MidiStatus = $"MIDI: connected {SelectedMidiDevice.Name}";
            MidiIndicatorBrush = Brushes.LimeGreen;
            AddLog(MidiStatus);
        }
        catch (Exception exception)
        {
            MidiStatus = $"MIDI: {exception.Message}";
            MidiIndicatorBrush = Brushes.DarkOrange;
            AddLog(MidiStatus);
        }
    }

    private void DisconnectMidi()
    {
        _midi.Disconnect();
        MidiStatus = "MIDI: disconnected";
        MidiIndicatorBrush = Brushes.DimGray;
        AddLog(MidiStatus);
    }

    private async Task ConnectMixerAsync()
    {
        if (string.IsNullOrWhiteSpace(MixerAddress))
        {
            MixerStatus = "XR18: enter address";
            MixerIndicatorBrush = Brushes.DarkOrange;
            AddLog(MixerStatus);
            return;
        }

        await DisconnectMixerAsync().ConfigureAwait(true);

        Xr18MixerClient mixer = new(new Xr18ConnectionSettings(MixerAddress.Trim()));
        mixer.MessageReceived += OnMixerMessageReceived;

        try
        {
            MixerStatus = $"XR18: connecting {MixerAddress.Trim()}";
            MixerIndicatorBrush = Brushes.DarkOrange;
            AddLog(MixerStatus);
            await mixer.StartAsync().ConfigureAwait(true);
            _mixer = mixer;
            MixerStatus = $"XR18: connected {MixerAddress.Trim()}";
            MixerIndicatorBrush = Brushes.LimeGreen;
            AddLog(MixerStatus);
        }
        catch (Exception exception)
        {
            mixer.MessageReceived -= OnMixerMessageReceived;
            await mixer.DisposeAsync().ConfigureAwait(true);
            MixerStatus = $"XR18: {exception.Message}";
            MixerIndicatorBrush = Brushes.DarkOrange;
            AddLog(MixerStatus);
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
        MixerIndicatorBrush = Brushes.DimGray;
        AddLog(MixerStatus);
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
        _selectedSlot = null;
        SelectedSlotText = "slot: none";

        foreach (ControlSlot slot in bank.Slots)
        {
            NormalizeSlotLabel(bank.Index, slot);
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

        SetStatus(DescribeResult("Fader simulation", result));
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

        SetStatus(DescribeResult("Mute simulation", result));
    }

    private async void OnMidiControlChanged(object? sender, MidiControlChangedEventArgs eventArgs)
    {
        if (_isLearningMidi && _selectedSlot is not null)
        {
            RunOnUiThread(() =>
            {
                int bankIndex = _mappingEngine.CurrentBank.Index;
                _mapEditor.SetMidiBinding(bankIndex, _selectedSlot.Id, new MidiBinding(
                    ToCoreMidiMessageKind(eventArgs.Change.Kind),
                    eventArgs.Change.Channel,
                    eventArgs.Change.Number));

                ControlSlot slot = _mapEditor.GetSlot(bankIndex, _selectedSlot.Id);
                _selectedSlot.Update(slot.Snapshot());
                _selectedSlot.SetSelected(true);
                _isLearningMidi = false;
                IsMapDirty = true;
                MidiStatus = $"MIDI learned: {eventArgs.Change.Kind} ch={eventArgs.Change.Channel} #{eventArgs.Change.Number}";
                SetStatus($"Learned MIDI for {_selectedSlot.Id}.");
            });

            return;
        }

        MappingResult result;

        lock (_mappingLock)
        {
            result = _mappingEngine.HandleControllerChange(ToControllerInputChange(eventArgs.Change));
        }

        RunOnUiThread(() =>
        {
            MidiStatus = $"MIDI: {eventArgs.Change.Kind} ch={eventArgs.Change.Channel} #{eventArgs.Change.Number} value={eventArgs.Change.Value}";
            Status = DescribeResult("MIDI", result);
            AddLog(Status);
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
                AddLog(Status);
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
        return new ControlSlotViewModel(snapshot, 185 + column * 92, 78 + row * 100, 76, 104);
    }

    private static ControlSlotViewModel CreateFader(ControlSlotSnapshot snapshot, int number)
    {
        int left = number == 1
            ? 82
            : 184 + (number - 2) * 92;

        return new ControlSlotViewModel(snapshot, left, 488 + WorkSurfaceOffsetY - LowerBlockOffsetY, 78, 178);
    }

    private static ControlSlotViewModel CreateButton(ControlSlotSnapshot snapshot, int number)
    {
        int column = (number - 1) % 8;
        int row = (number - 1) / 8;
        return new ControlSlotViewModel(snapshot, 188 + column * 92, 392 + WorkSurfaceOffsetY - LowerBlockOffsetY + row * 52, 72, 38);
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

    private void LoadAssignmentFields(ControlSlot slot)
    {
        if (slot.MixerBinding is null)
        {
            AssignmentChannel = "1";
            AssignmentIndex = string.Empty;
            Status = "Selected slot has no OSC binding.";
            return;
        }

        if (TryResolveAssignment(slot.MixerBinding, out MixerAssignment? assignment) && assignment is not null)
        {
            SelectedAssignmentCommand = assignment.CommandKey;
            AssignmentChannel = assignment.Channel.ToString(CultureInfo.InvariantCulture);
            AssignmentIndex = assignment.Index?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            return;
        }

        AssignmentIndex = string.Empty;
        Status = $"Selected OSC binding is not in the command catalog: {slot.MixerBinding.OscAddress}";
    }

    private void NormalizeSlotLabel(int bankIndex, ControlSlot slot)
    {
        if (slot.MixerBinding is null)
        {
            return;
        }

        if (!TryResolveAssignment(slot.MixerBinding, out MixerAssignment? assignment) || assignment is null)
        {
            return;
        }

        string label = CreateAssignmentLabel(assignment.CommandKey, assignment.Channel, assignment.Index);

        if (!string.Equals(slot.Label, label, StringComparison.Ordinal))
        {
            _mapEditor.SetSlotLabel(bankIndex, slot.Id, label);
        }
    }

    private bool TryResolveAssignment(MixerBinding binding, out MixerAssignment? assignment)
    {
        foreach (MixerCommandDefinition command in _mapEditor.CommandCatalog.Commands.OrderBy(command => command.Key))
        {
            foreach (int channel in Enumerable.Range(1, 18))
            {
                IEnumerable<int?> indexes = GetCandidateIndexes(command);

                foreach (int? index in indexes)
                {
                    MixerBinding candidate;

                    try
                    {
                        candidate = _mapEditor.CommandCatalog.CreateBinding(command.Key, channel, index);
                    }
                    catch
                    {
                        continue;
                    }

                    if (candidate.ValueKind == binding.ValueKind &&
                        string.Equals(candidate.OscAddress, binding.OscAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        assignment = new MixerAssignment(command.Key, channel, index);
                        return true;
                    }
                }
            }
        }

        assignment = null;
        return false;
    }

    private static IEnumerable<int?> GetCandidateIndexes(MixerCommandDefinition command)
    {
        if (command.MinIndex is null || command.MaxIndex is null)
        {
            yield return null;
            yield break;
        }

        for (int index = command.MinIndex.Value; index <= command.MaxIndex.Value; index++)
        {
            yield return index;
        }
    }

    private static string CreateAssignmentLabel(string commandKey, int channel, int? index)
    {
        return commandKey.ToLowerInvariant() switch
        {
            "main" => $"Ch {channel} Level",
            "mute" => $"Ch {channel} Mute",
            "pan" => $"Ch {channel} Pan",
            "bus" or "aux" => $"Ch {channel} Bus {index}",
            "fx" => $"Ch {channel} FX {index}",
            "bus-on" => $"Ch {channel} Bus {index} On",
            "fx-on" => $"Ch {channel} FX {index} On",
            _ => $"Ch {channel} {commandKey}"
        };
    }

    private static string CreateUnassignedLabel(string slotId)
    {
        return $"Unassigned {slotId}";
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

    private static string GetSourceMidiMapPath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "midi-map.json"));
    }

    private static Brush GetReadableTextBrush(RgbColor color)
    {
        double luminance = (0.299 * color.Red) + (0.587 * color.Green) + (0.114 * color.Blue);
        return luminance > 150 ? Brushes.Black : Brushes.White;
    }

    private static string GetApplicationVersion()
    {
        Assembly assembly = typeof(MainWindowViewModel).Assembly;
        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            .Split('+')[0];

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        Version? version = assembly.GetName().Version;
        return version is null
            ? "1.0"
            : $"{version.Major}.{version.Minor}";
    }

    private static void RunOnUiThread(Action action)
    {
        Application.Current.Dispatcher.Invoke(action);
    }

    private void SetStatus(string message)
    {
        Status = message;
        AddLog(message);
    }

    private void AddLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        LogEntries.Insert(0, $"{timestamp} {message}");

        while (LogEntries.Count > 50)
        {
            LogEntries.RemoveAt(LogEntries.Count - 1);
        }
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

public sealed record MixerAssignment(string CommandKey, int Channel, int? Index);
