using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Threading;
using System.Windows.Input;
using Avalonia.Media;
using Rug.Osc;
using WECPBXR.Core.Mapping;
using WECPBXR.Core.Models;
using WECPBXR.Hardware;
using WECPBXR.UI.Settings;
using WECPBXR.UI.Updates;

namespace WECPBXR.UI.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private BankSet _bankSet;
    private MappingEngine _mappingEngine;
    private MidiMapEditor _mapEditor;
    private readonly MidiInputManager _midi;
    private readonly ApplicationSettingsStore _settingsStore;
    private readonly ApplicationSettings _settings;
    private readonly ApplicationUpdateService _updateService;
    private ControllerProfile _controllerProfile;
    private readonly Dictionary<string, ControlSlotViewModel> _slotLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MixerAssignment> _assignmentLookup;
    private readonly object _mappingLock = new();

    private string _bankTitle = string.Empty;
    private string _bankColorText = string.Empty;
    private IBrush _bankBrush = Brushes.Red;
    private IBrush _bankTextBrush = Brushes.Black;
    private string _status = "Ready";
    private string _mixerAddress = "192.168.1.100";
    private string _mixerStatus = "XR: disconnected";
    private string _midiStatus = "MIDI: disconnected";
    private bool _isAssignmentMode;
    private bool _isLearningMidi;
    private bool _isLogVisible;
    private bool _isMapDirty;
    private string _selectedAssignmentCommand = "main";
    private string _assignmentChannel = "1";
    private string _assignmentIndex = "1";
    private string _assignmentMidiChannel = "1";
    private string _assignmentMidiNumber = "0";
    private string _selectedSlotText = "slot: none";
    private string _saveMapText = "Save";
    private int _bankCount;
    private IBrush _mixerIndicatorBrush = Brushes.DimGray;
    private IBrush _midiIndicatorBrush = Brushes.DimGray;
    private ControlSlotViewModel? _selectedSlot;
    private MidiInputDeviceInfo? _selectedMidiDevice;
    private ControllerProfileOption? _selectedControllerProfile;
    private BXrMixerClient? _mixer;
    private bool _disposed;

    public MainWindowViewModel()
    {
        _settingsStore = new ApplicationSettingsStore();
        _settings = _settingsStore.Load();
        _bankCount = Math.Clamp(_settings.Controller.BankCount, 1, DefaultControlBankFactory.MaximumBankCount);
        _settings.Controller.BankCount = _bankCount;
        _updateService = new ApplicationUpdateService();
        _controllerProfile = ControllerProfileCatalog.GetOrDefault(_settings.Controller.ProfileId);
        _bankSet = DefaultControlBankFactory.CreateDefaultBankSet(_controllerProfile, _bankCount);
        _mappingEngine = new MappingEngine(_bankSet);
        _mapEditor = new MidiMapEditor(_bankSet, _controllerProfile.Id);
        _midi = new MidiInputManager();
        _assignmentLookup = BuildAssignmentLookup(_mapEditor.CommandCatalog);

        if (!string.IsNullOrWhiteSpace(_settings.XR.Address))
        {
            _mixerAddress = _settings.XR.Address;
        }

        _mappingEngine.BankChanged += OnBankChanged;
        _mappingEngine.SlotStateChanged += OnSlotStateChanged;
        _midi.ControlChanged += OnMidiControlChanged;

        LoadMapCommand = new RelayCommand(LoadMap);
        BankPreviousCommand = new RelayCommand(() => _mappingEngine.PreviousBank());
        BankNextCommand = new RelayCommand(() => _mappingEngine.NextBank());
        RefreshMidiDevicesCommand = new RelayCommand(RefreshMidiDevices);
        ConnectMidiCommand = new RelayCommand(ConnectMidi);
        DisconnectMidiCommand = new RelayCommand(DisconnectMidi);
        ConnectMixerCommand = new AsyncRelayCommand(ConnectMixerAsync);
        DisconnectMixerCommand = new AsyncRelayCommand(DisconnectMixerAsync);
        ToggleAssignmentModeCommand = new RelayCommand(ToggleAssignmentMode);
        ApplyAssignmentCommand = new RelayCommand(ApplyAssignment);
        ApplyMidiBindingCommand = new RelayCommand(ApplyMidiBinding);
        ClearAssignmentCommand = new RelayCommand(ClearAssignment);
        SaveMapCommand = new RelayCommand(SaveMap);
        ToggleLogCommand = new RelayCommand(ToggleLog);
        LearnMidiCommand = new RelayCommand(StartLearnMidi);
        CheckMapCommand = new RelayCommand(CheckMap);
        RequestMixerValuesCommand = new AsyncRelayCommand(RequestMixerValuesAsync);
        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync);
        SelectControllerProfileCommand = new RelayCommand(SelectControllerProfile);
        SetBankCountCommand = new RelayCommand(SetBankCount);

        foreach (MixerCommandDefinition command in _mapEditor.CommandCatalog.Commands.OrderBy(command => command.Key))
        {
            AssignmentCommands.Add(command.Key);
        }

        foreach (ControllerProfile profile in ControllerProfileCatalog.All)
        {
            ControllerProfiles.Add(new ControllerProfileOption(profile.Id, profile.Name));
        }

        _selectedControllerProfile = ControllerProfiles.FirstOrDefault(profile => profile.Id == _controllerProfile.Id);

        TryAutoLoadMap();
        RefreshMidiDevices();
        RefreshCurrentBank();
        _ = AutoConnectAsync();
        _ = CheckForUpdatesAsync(silentWhenUnavailable: true);
    }

    public ObservableCollection<ControlSlotViewModel> Knobs { get; } = [];

    public ObservableCollection<ControlSlotViewModel> Faders { get; } = [];

    public ObservableCollection<ControlSlotViewModel> Buttons { get; } = [];

    public ObservableCollection<MidiInputDeviceInfo> MidiDevices { get; } = [];

    public ObservableCollection<ControllerProfileOption> ControllerProfiles { get; } = [];

    public ObservableCollection<string> AssignmentCommands { get; } = [];

    public ObservableCollection<string> LogEntries { get; } = [];

    public ICommand LoadMapCommand { get; }

    public ICommand BankPreviousCommand { get; }

    public ICommand BankNextCommand { get; }

    public ICommand RefreshMidiDevicesCommand { get; }

    public ICommand ConnectMidiCommand { get; }

    public ICommand DisconnectMidiCommand { get; }

    public ICommand ConnectMixerCommand { get; }

    public ICommand DisconnectMixerCommand { get; }

    public ICommand ToggleAssignmentModeCommand { get; }

    public ICommand ApplyAssignmentCommand { get; }

    public ICommand ApplyMidiBindingCommand { get; }

    public ICommand ClearAssignmentCommand { get; }

    public ICommand SaveMapCommand { get; }

    public string SoftwareVersionText => $"Software version {GetApplicationVersion()}";

    public string ControllerProfileName => _controllerProfile.Name;

    public string BankCountText => $"Banks: {_bankCount}";

    public bool IsWorldeProfile => !IsAkaiProfile;

    public bool IsAkaiProfile => string.Equals(
        _controllerProfile.Id,
        ControllerProfileCatalog.AkaiMidimixProfileId,
        StringComparison.OrdinalIgnoreCase);

    public string ControllerBrandName => IsAkaiProfile ? "AKAI" : "WORLDE";

    public ICommand ToggleLogCommand { get; }

    public ICommand LearnMidiCommand { get; }

    public ICommand CheckMapCommand { get; }

    public ICommand RequestMixerValuesCommand { get; }

    public ICommand CheckForUpdatesCommand { get; }

    public ICommand SelectControllerProfileCommand { get; }

    public ICommand SetBankCountCommand { get; }

    public bool IsAssignmentMode
    {
        get => _isAssignmentMode;
        private set
        {
            if (SetProperty(ref _isAssignmentMode, value))
            {
                OnPropertyChanged(nameof(IsNormalPanelVisible));
                OnPropertyChanged(nameof(IsAssignmentPanelVisible));
                OnPropertyChanged(nameof(AssignmentModeText));
            }
        }
    }

    public bool IsNormalPanelVisible => !IsAssignmentMode;

    public bool IsAssignmentPanelVisible => IsAssignmentMode;

    public string AssignmentModeText => IsAssignmentMode ? "Assign on" : "Assign";

    public bool IsLogPanelVisible => IsLogVisible;

    public string LogToggleText => IsLogVisible ? "Log -" : "Log +";

    public bool IsLogVisible
    {
        get => _isLogVisible;
        private set
        {
            if (SetProperty(ref _isLogVisible, value))
            {
                OnPropertyChanged(nameof(IsLogPanelVisible));
                OnPropertyChanged(nameof(LogToggleText));
            }
        }
    }

    public string SaveMapText
    {
        get => _saveMapText;
        private set => SetProperty(ref _saveMapText, value);
    }

    public IBrush MixerIndicatorBrush
    {
        get => _mixerIndicatorBrush;
        private set => SetProperty(ref _mixerIndicatorBrush, value);
    }

    public IBrush MidiIndicatorBrush
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

    public string AssignmentMidiChannel
    {
        get => _assignmentMidiChannel;
        set => SetProperty(ref _assignmentMidiChannel, value);
    }

    public string AssignmentMidiNumber
    {
        get => _assignmentMidiNumber;
        set => SetProperty(ref _assignmentMidiNumber, value);
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

    public ControllerProfileOption? SelectedControllerProfile
    {
        get => _selectedControllerProfile;
        set
        {
            if (value is null || string.Equals(value.Id, _controllerProfile.Id, StringComparison.OrdinalIgnoreCase))
            {
                SetProperty(ref _selectedControllerProfile, value);
                return;
            }

            if (IsMapDirty)
            {
                SetStatus("Controller profile change blocked: save the map first.");
                OnPropertyChanged(nameof(SelectedControllerProfile));
                return;
            }

            SetProperty(ref _selectedControllerProfile, value);
            ApplyControllerProfile(value.Id);
        }
    }

    private void SelectControllerProfile(object? parameter)
    {
        if (parameter is not string profileId)
        {
            return;
        }

        ControllerProfileOption? profile = ControllerProfiles.FirstOrDefault(option =>
            string.Equals(option.Id, profileId, StringComparison.OrdinalIgnoreCase));

        if (profile is not null)
        {
            SelectedControllerProfile = profile;
        }
    }

    private void SetBankCount(object? parameter)
    {
        int bankCount = parameter switch
        {
            int value => value,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) => value,
            _ => 0
        };

        if (bankCount < 1 ||
            bankCount > DefaultControlBankFactory.MaximumBankCount)
        {
            return;
        }

        if (bankCount == _bankCount)
        {
            SetStatus($"Bank count is already {_bankCount}.");
            return;
        }

        if (IsMapDirty)
        {
            SetStatus("Bank count change blocked: save the map first.");
            return;
        }

        int selectedBankIndex = Math.Min(_mappingEngine.CurrentBank.Index, bankCount - 1);

        lock (_mappingLock)
        {
            _mappingEngine.BankChanged -= OnBankChanged;
            _mappingEngine.SlotStateChanged -= OnSlotStateChanged;

            _bankCount = bankCount;
            _bankSet = DefaultControlBankFactory.CreateDefaultBankSet(_controllerProfile, _bankCount);
            _mappingEngine = new MappingEngine(_bankSet);
            _mapEditor = new MidiMapEditor(_bankSet, _controllerProfile.Id);

            _mappingEngine.BankChanged += OnBankChanged;
            _mappingEngine.SlotStateChanged += OnSlotStateChanged;
        }

        _settings.Controller.BankCount = _bankCount;
        SaveSettings();

        TryAutoLoadMap();
        _bankSet.SelectBank(selectedBankIndex);
        RefreshCurrentBank();
        IsMapDirty = true;
        OnPropertyChanged(nameof(BankCountText));
        SetStatus($"Bank count changed to {_bankCount}. Save the map to keep this bank set.");
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

    public IBrush BankBrush
    {
        get => _bankBrush;
        private set => SetProperty(ref _bankBrush, value);
    }

    public IBrush BankTextBrush
    {
        get => _bankTextBrush;
        private set => SetProperty(ref _bankTextBrush, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    private async Task AutoConnectAsync()
    {
        await Task.Yield();

        if (_settings.MIDI.AutoConnect)
        {
            ConnectMidi();
        }

        if (_settings.XR.AutoConnect)
        {
            await ConnectMixerAsync().ConfigureAwait(true);
        }
    }

    private Task CheckForUpdatesAsync()
    {
        return CheckForUpdatesAsync(silentWhenUnavailable: false);
    }

    private async Task CheckForUpdatesAsync(bool silentWhenUnavailable)
    {
        try
        {
            if (!silentWhenUnavailable)
            {
                SetStatus("Update: checking GitHub Releases.");
            }

            ApplicationUpdateResult result = await _updateService
                .CheckDownloadAndApplyAsync(progress => RunOnUiThread(() => SetStatus($"Update: downloading {progress}%.")))
                .ConfigureAwait(false);

            RunOnUiThread(() =>
            {
                switch (result.Kind)
                {
                    case ApplicationUpdateResultKind.NotInstalled:
                        if (!silentWhenUnavailable)
                        {
                            SetStatus("Update: Velopack install is required for self-update.");
                        }

                        break;

                    case ApplicationUpdateResultKind.NoUpdate:
                        SetStatus("Update: already on the latest version.");
                        break;

                    case ApplicationUpdateResultKind.Restarting:
                        SetStatus($"Update: restarting into {result.Version}.");
                        break;
                }
            });
        }
        catch (Exception exception)
        {
            RunOnUiThread(() => SetStatus($"Update failed: {exception.Message}"));
        }
    }

    private void TryAutoLoadMap()
    {
        string path = GetConfiguredMidiMapPath();

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

    private void ApplyControllerProfile(string profileId)
    {
        ControllerProfile nextProfile = ControllerProfileCatalog.GetOrDefault(profileId);

        lock (_mappingLock)
        {
            _mappingEngine.BankChanged -= OnBankChanged;
            _mappingEngine.SlotStateChanged -= OnSlotStateChanged;

            _controllerProfile = nextProfile;
            _bankSet = DefaultControlBankFactory.CreateDefaultBankSet(_controllerProfile, _bankCount);
            _mappingEngine = new MappingEngine(_bankSet);
            _mapEditor = new MidiMapEditor(_bankSet, _controllerProfile.Id);

            _mappingEngine.BankChanged += OnBankChanged;
            _mappingEngine.SlotStateChanged += OnSlotStateChanged;
        }

        _settings.Controller.ProfileId = _controllerProfile.Id;
        SaveSettings();

        TryAutoLoadMap();
        RefreshCurrentBank();
        OnPropertyChanged(nameof(ControllerProfileName));
        OnPropertyChanged(nameof(ControllerBrandName));
        OnPropertyChanged(nameof(IsWorldeProfile));
        OnPropertyChanged(nameof(IsAkaiProfile));
        SetStatus($"Controller profile: {_controllerProfile.Name}.");
    }

    private void OnBankChanged(object? sender, BankChangedEventArgs eventArgs)
    {
        RefreshCurrentBank();
    }

    private void OnSlotStateChanged(object? sender, SlotStateChangedEventArgs eventArgs)
    {
        RunOnUiThread(() => UpdateSlot(eventArgs.Slot));
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
            string path = GetConfiguredMidiMapPath();
            _mapEditor.SaveAsync(path).GetAwaiter().GetResult();

            string sourcePath = GetConfiguredSourceMidiMapPath();
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
        LoadMidiFields(coreSlot);
        NormalizeSlotLabel(_mappingEngine.CurrentBank.Index, coreSlot);
        slot.Update(coreSlot.Snapshot());
        slot.SetSelected(true);

        SetStatus(coreSlot.MixerBinding is null
            ? $"Selected {slot.Id}: no OSC binding"
            : $"Selected {slot.Id}: {coreSlot.MixerBinding.OscAddress}");
    }

    public void HandleSlotClick(ControlSlotViewModel slot)
    {
        if (IsAssignmentMode)
        {
            SelectSlot(slot);
            return;
        }

        switch (slot.Id.ToLowerInvariant())
        {
            case "bank-prev":
                _mappingEngine.PreviousBank();
                break;

            case "bank-next":
                _mappingEngine.NextBank();
                break;
        }
    }

    public string GetLogText()
    {
        return string.Join(Environment.NewLine, LogEntries);
    }

    public void NotifyLogCopied()
    {
        SetStatus("Log copied to clipboard.");
    }

    public void NotifyLogCopySkipped()
    {
        SetStatus("Log copy skipped: log is empty.");
    }

    public void NotifyLogCopyFailed(string message)
    {
        SetStatus($"Log copy failed: {message}");
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

    private void ApplyMidiBinding()
    {
        if (_selectedSlot is null)
        {
            SetStatus("MIDI assignment: click a control first.");
            return;
        }

        if (!int.TryParse(AssignmentMidiChannel, out int midiChannel) || midiChannel is < 1 or > 16)
        {
            SetStatus("MIDI assignment: channel must be 1-16.");
            return;
        }

        if (!int.TryParse(AssignmentMidiNumber, out int midiNumber) || midiNumber is < 0 or > 127)
        {
            SetStatus("MIDI assignment: CC must be 0-127.");
            return;
        }

        int bankIndex = _mappingEngine.CurrentBank.Index;
        MidiBinding binding = new(
            MidiMessageKind.ControlChange,
            midiChannel,
            midiNumber);

        SetMidiBinding(bankIndex, _selectedSlot.Id, binding);

        ControlSlot slot = _mapEditor.GetSlot(bankIndex, _selectedSlot.Id);
        _selectedSlot.Update(slot.Snapshot());
        _selectedSlot.SetSelected(true);
        IsMapDirty = true;
        SetStatus($"Assigned MIDI for {_selectedSlot.Id}: CC ch={midiChannel} #{midiNumber}.");
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
        List<string> duplicateMidi = [];
        Dictionary<string, List<string>> midiSlots = new(StringComparer.OrdinalIgnoreCase);

        foreach (ControlBank bank in _mappingEngine.Banks)
        {
            foreach (ControlSlot slot in bank.Slots)
            {
                if (slot.MixerBinding is null && !IsBankNavigationSlot(slot.Id))
                {
                    missingOsc++;
                }
                else if (slot.MixerBinding is not null && !TryResolveAssignment(slot.MixerBinding, out _))
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
                    slots = [];
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
            SetStatus("XR pull: mixer is not connected.");
            return;
        }

        string[] addresses = [.. _mappingEngine.CurrentBank.Slots
            .Select(slot => slot.MixerBinding?.OscAddress)
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()];

        try
        {
            foreach (string address in addresses)
            {
                await _mixer.RequestOscValueAsync(address).ConfigureAwait(true);
            }

            SetStatus($"XR pull: requested {addresses.Length} value(s).");
        }
        catch (Exception exception)
        {
            SetStatus($"XR pull failed: {exception.Message}");
        }
    }

    private void RefreshMidiDevices()
    {
        try
        {
            string? selectedName = SelectedMidiDevice?.Name;
            if (string.IsNullOrWhiteSpace(selectedName))
            {
                selectedName = _settings.MIDI.InputDeviceName;
            }

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
            _settings.MIDI.InputDeviceName = SelectedMidiDevice.Name;
            SaveSettings();
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
            MixerStatus = "XR: enter address";
            MixerIndicatorBrush = Brushes.DarkOrange;
            AddLog(MixerStatus);
            return;
        }

        await DisconnectMixerAsync().ConfigureAwait(true);

        BXrMixerClient mixer = new(new BXrConnectionSettings(MixerAddress.Trim()));
        mixer.MessageReceived += OnMixerMessageReceived;

        try
        {
            MixerStatus = $"XR: connecting {MixerAddress.Trim()}";
            MixerIndicatorBrush = Brushes.DarkOrange;
            AddLog(MixerStatus);
            await mixer.StartAsync().ConfigureAwait(true);
            _mixer = mixer;
            MixerStatus = $"XR: connected {MixerAddress.Trim()}";
            MixerIndicatorBrush = Brushes.LimeGreen;
            _settings.XR.Address = MixerAddress.Trim();
            SaveSettings();
            AddLog(MixerStatus);

            if (_settings.XR.PullOnConnect)
            {
                await RequestMixerValuesAsync().ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            mixer.MessageReceived -= OnMixerMessageReceived;
            await mixer.DisposeAsync().ConfigureAwait(true);
            MixerStatus = $"XR: {exception.Message}";
            MixerIndicatorBrush = Brushes.DarkOrange;
            AddLog(MixerStatus);
        }
    }

    private async Task DisconnectMixerAsync()
    {
        if (_mixer is null)
        {
            MixerStatus = "XR: disconnected";
            return;
        }

        BXrMixerClient mixer = _mixer;
        _mixer = null;
        mixer.MessageReceived -= OnMixerMessageReceived;
        await mixer.DisposeAsync().ConfigureAwait(true);
        MixerStatus = "XR: disconnected";
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

    private async void OnMidiControlChanged(object? sender, MidiControlChangedEventArgs eventArgs)
    {
        if (_isLearningMidi && _selectedSlot is not null)
        {
            RunOnUiThread(() =>
            {
                int bankIndex = _mappingEngine.CurrentBank.Index;
                MidiBinding binding = new(
                    ToCoreMidiMessageKind(eventArgs.Change.Kind),
                    eventArgs.Change.Channel,
                    eventArgs.Change.Number);

                SetMidiBinding(bankIndex, _selectedSlot.Id, binding);

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

            if (TryHandleNavigationSlot(result.Slot, eventArgs.Change))
            {
                return;
            }
        });

        if (result.MixerCommand is not null && _mixer is not null)
        {
            try
            {
                await _mixer.SendOscValueAsync(
                    result.MixerCommand.OscAddress,
                    result.MixerCommand.Value,
                    sendInteger: result.MixerCommand.ValueKind is MixerValueKind.Toggle or MixerValueKind.Action).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                RunOnUiThread(() => MixerStatus = $"XR send: {exception.Message}");
            }
        }
    }

    private void OnMixerMessageReceived(object? sender, BXrOscMessageReceivedEventArgs eventArgs)
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
                MixerStatus = FormattableString.Invariant($"XR: {change.OscAddress}={change.Value:0.###}");
                Status = DescribeResult("XR", result);
                AddLog(Status);
            });
        }
    }

    private ControlSlotViewModel CreateSlotViewModel(ControlSlotSnapshot snapshot)
    {
        ControllerControlDefinition? control = _controllerProfile.FindControl(snapshot.Id);
        if (control is not null)
        {
            return new ControlSlotViewModel(
                snapshot,
                control.Left,
                control.Top,
                control.Width,
                control.Height);
        }

        return new ControlSlotViewModel(snapshot, 0, 0, 80, 80);
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

    private void LoadMidiFields(ControlSlot slot)
    {
        AssignmentMidiChannel = slot.MidiBinding?.Channel.ToString(CultureInfo.InvariantCulture) ?? "1";
        AssignmentMidiNumber = slot.MidiBinding?.Number.ToString(CultureInfo.InvariantCulture) ?? "0";
    }

    private void SetMidiBinding(int bankIndex, string slotId, MidiBinding binding)
    {
        if (IsBankNavigationSlot(slotId))
        {
            foreach (ControlBank bank in _mappingEngine.Banks)
            {
                _mapEditor.SetMidiBinding(bank.Index, slotId, binding);
            }

            return;
        }

        _mapEditor.SetMidiBinding(bankIndex, slotId, binding);
    }

    private bool TryHandleNavigationSlot(ControlSlotSnapshot? slot, MidiControlChange change)
    {
        if (slot is null || !IsMidiPress(change))
        {
            return false;
        }

        switch (slot.Id.ToLowerInvariant())
        {
            case "bank-prev":
                _mappingEngine.PreviousBank();
                SetStatus("Bank previous.");
                return true;

            case "bank-next":
                _mappingEngine.NextBank();
                SetStatus("Bank next.");
                return true;

            default:
                return false;
        }
    }

    private static bool IsBankNavigationSlot(string slotId)
    {
        return string.Equals(slotId, "bank-prev", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(slotId, "bank-next", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMidiPress(MidiControlChange change)
    {
        return change.Kind switch
        {
            WECPBXR.Hardware.MidiControlKind.NoteOn => change.Value > 0,
            WECPBXR.Hardware.MidiControlKind.ControlChange => change.NormalizedValue >= 0.5,
            _ => false
        };
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
        return _assignmentLookup.TryGetValue(CreateAssignmentLookupKey(binding), out assignment);
    }

    private static Dictionary<string, MixerAssignment> BuildAssignmentLookup(MixerCommandCatalog commandCatalog)
    {
        Dictionary<string, MixerAssignment> lookup = new(StringComparer.OrdinalIgnoreCase);

        foreach (MixerCommandDefinition command in commandCatalog.Commands)
        {
            IEnumerable<int> channels = command.AddressPattern.Contains("{channel", StringComparison.Ordinal)
                ? Enumerable.Range(command.MinChannel, command.MaxChannel - command.MinChannel + 1)
                : [command.MinChannel];

            foreach (int channel in channels)
            {
                foreach (int? index in GetCandidateIndexes(command))
                {
                    MixerBinding binding;

                    try
                    {
                        binding = commandCatalog.CreateBinding(command.Key, channel, index);
                    }
                    catch
                    {
                        continue;
                    }

                    lookup.TryAdd(CreateAssignmentLookupKey(binding), new MixerAssignment(command.Key, channel, index));
                }
            }
        }

        return lookup;
    }

    private static string CreateAssignmentLookupKey(MixerBinding binding)
    {
        return $"{binding.ValueKind}:{binding.OscAddress}";
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
            "solo" => $"Ch {channel} Solo",
            "gain" => $"Ch {channel} Gain",
            "hpf" => $"Ch {channel} HPF",
            "hpf-on" => $"Ch {channel} HPF On",
            "gate-on" => $"Ch {channel} Gate On",
            "gate-threshold" => $"Ch {channel} Gate Thr",
            "comp-on" => $"Ch {channel} Comp On",
            "comp-threshold" => $"Ch {channel} Comp Thr",
            "eq-on" => $"Ch {channel} EQ On",
            "eq-low" => $"Ch {channel} EQ Low",
            "eq-lowmid" => $"Ch {channel} EQ LowMid",
            "eq-highmid" => $"Ch {channel} EQ HighMid",
            "eq-high" => $"Ch {channel} EQ High",
            "master" => "Main LR",
            "master-mute" => "Main Mute",
            "bus-master" => $"Bus {index} Master",
            "bus-master-mute" => $"Bus {index} Mute",
            "fx-send-master" => $"FX {index} Send",
            "fx-return" => $"FX {index} Return",
            "fx-return-mute" => $"FX {index} Return Mute",
            "mute-group" => $"Mute Group {index}",
            "clear-solo" => "Clear Solo",
            "tap-tempo" => "Tap Tempo",
            "scene-load" => $"Load Snapshot {index}",
            "scene-prev" => "Snapshot Prev",
            "scene-next" => "Snapshot Next",
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

    private static MidiMessageKind ToCoreMidiMessageKind(WECPBXR.Hardware.MidiControlKind kind)
    {
        return kind switch
        {
            WECPBXR.Hardware.MidiControlKind.ControlChange => MidiMessageKind.ControlChange,
            WECPBXR.Hardware.MidiControlKind.NoteOn => MidiMessageKind.NoteOn,
            WECPBXR.Hardware.MidiControlKind.NoteOff => MidiMessageKind.NoteOff,
            WECPBXR.Hardware.MidiControlKind.PitchBend => MidiMessageKind.PitchBend,
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

    private string GetConfiguredMidiMapPath()
    {
        string configuredPath = _settings.Map.Path;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return GetDefaultMidiMapPath();
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        string outputPath = Path.Combine(AppContext.BaseDirectory, configuredPath);
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        string sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", configuredPath));
        return File.Exists(sourcePath) ? sourcePath : outputPath;
    }

    private string GetConfiguredSourceMidiMapPath()
    {
        string configuredPath = _settings.Map.Path;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return GetSourceMidiMapPath();
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", configuredPath));
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

    private static IBrush GetReadableTextBrush(RgbColor color)
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
        Dispatcher.UIThread.Post(action);
    }

    private void SetStatus(string message)
    {
        Status = message;
        AddLog(message);
    }

    private void SaveSettings()
    {
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception exception)
        {
            AddLog($"Settings save failed: {exception.Message}");
        }
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

public sealed record ControllerProfileOption(string Id, string Name);
