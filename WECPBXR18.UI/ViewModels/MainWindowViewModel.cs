using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using WECPBXR18.Core.Mapping;
using WECPBXR18.Core.Models;

namespace WECPBXR18.UI.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly BankSet _bankSet;
    private readonly MappingEngine _mappingEngine;
    private readonly MidiMapEditor _mapEditor;
    private readonly Dictionary<string, ControlSlotViewModel> _slotLookup = new(StringComparer.OrdinalIgnoreCase);

    private string _bankTitle = string.Empty;
    private string _bankColorText = string.Empty;
    private Brush _bankBrush = Brushes.Red;
    private Brush _bankTextBrush = Brushes.Black;
    private string _status = "Ready";

    public MainWindowViewModel()
    {
        _bankSet = DefaultControlBankFactory.CreateDefaultBankSet();
        _mappingEngine = new MappingEngine(_bankSet);
        _mapEditor = new MidiMapEditor(_bankSet);

        _mappingEngine.BankChanged += (_, _) => RefreshCurrentBank();
        _mappingEngine.SlotStateChanged += (_, eventArgs) => UpdateSlot(eventArgs.Slot);

        LoadMapCommand = new RelayCommand(LoadMap);
        BankPreviousCommand = new RelayCommand(() => _mappingEngine.PreviousBank());
        BankNextCommand = new RelayCommand(() => _mappingEngine.NextBank());
        SimulateFaderCommand = new RelayCommand(SimulateFader);
        SimulateMuteCommand = new RelayCommand(SimulateMute);

        TryAutoLoadMap();
        RefreshCurrentBank();
    }

    public ObservableCollection<ControlSlotViewModel> Knobs { get; } = new();

    public ObservableCollection<ControlSlotViewModel> Faders { get; } = new();

    public ObservableCollection<ControlSlotViewModel> Buttons { get; } = new();

    public ICommand LoadMapCommand { get; }

    public ICommand BankPreviousCommand { get; }

    public ICommand BankNextCommand { get; }

    public ICommand SimulateFaderCommand { get; }

    public ICommand SimulateMuteCommand { get; }

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
        _mappingEngine.HandleMixerChange(new MixerValueChange("/ch/01/mix/fader", 0.62));
        MappingResult result = _mappingEngine.HandleControllerChange(new ControllerInputChange(
            MidiMessageKind.ControlChange,
            Channel: 1,
            Number: 26,
            Value: 79 / 127.0,
            RawEvent: "ui sim fader"));

        Status = DescribeResult("Fader simulation", result);
    }

    private void SimulateMute()
    {
        _mappingEngine.HandleMixerChange(new MixerValueChange("/ch/01/mix/on", 1));
        MappingResult result = _mappingEngine.HandleControllerChange(new ControllerInputChange(
            MidiMessageKind.NoteOn,
            Channel: 1,
            Number: 34,
            Value: 1,
            RawEvent: "ui sim mute"));

        Status = DescribeResult("Mute simulation", result);
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
}
