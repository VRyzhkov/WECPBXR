using System.Globalization;
using Rug.Osc;
using WECPBXR.Core.Configuration;
using WECPBXR.Core.Mapping;
using WECPBXR.Core.Models;
using WECPBXR.Hardware;

Console.WriteLine("WECPBXR diagnostics");
Console.WriteLine();

if (args.Length > 0 && args[0].Equals("--write-default-map", StringComparison.OrdinalIgnoreCase))
{
    string outputPath = args.Length >= 2 ? args[1] : GetDefaultMidiMapPath();
    await MidiMapConfigurationStore.SaveAsync(DefaultControlBankFactory.CreateDefaultBankSet(), outputPath);
    Console.WriteLine($"Default map saved: {outputPath}");
    return 0;
}

BXrNetworkScanner scanner = new();
using MidiInputManager midi = new();
BankSet bankSet = DefaultControlBankFactory.CreateDefaultBankSet();
MappingEngine mappingEngine = new(bankSet);
MidiMapEditor mapEditor = new(bankSet);
string midiMapPath = GetDefaultMidiMapPath();
BXrMixerClient? mixer = null;
string? connectedMixerAddress = null;
object mappingLock = new();

mappingEngine.BankChanged += (_, eventArgs) =>
{
    PrintCurrentBank("BANK", eventArgs.CurrentBank);
    Console.WriteLine("BANK physical bank button color command is not implemented yet; protocol is unknown.");
};

midi.ControlChanged += async (_, eventArgs) =>
{
    try
    {
        MappingResult result;

        lock (mappingLock)
        {
            result = mappingEngine.HandleControllerChange(ToControllerInputChange(eventArgs.Change));
        }

        PrintMappingResult("CORE", result);
        await SendMixerCommandIfConnectedAsync(mixer, result.MixerCommand);
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"MIDI mapping error: {exception.Message}");
    }
};

await TryLoadDefaultMapAsync(mapEditor, midiMapPath);
PrintHelp();

while (true)
{
    Console.Write("> ");
    string? line = Console.ReadLine();

    if (line is null)
    {
        break;
    }

    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    if (parts.Length == 0)
    {
        continue;
    }

    string command = parts[0].ToLowerInvariant();

    try
    {
        switch (command)
        {
            case "m":
            case "mute":
                await GetConnectedMixer(mixer).MuteChannelAsync(ReadChannel(parts));
                break;

            case "u":
            case "unmute":
                await GetConnectedMixer(mixer).UnmuteChannelAsync(ReadChannel(parts));
                break;

            case "h":
            case "help":
                PrintHelp();
                break;

            case "s":
            case "scan":
                await PrintScanResultsAsync(scanner);
                break;

            case "midi":
                HandleMidiCommand(midi, parts);
                break;

            case "mixer":
                (mixer, connectedMixerAddress) = await HandleMixerCommandAsync(
                    mixer,
                    connectedMixerAddress,
                    mappingEngine,
                    mappingLock,
                    scanner,
                    parts);
                break;

            case "bank":
                HandleBankCommand(mappingEngine, parts);
                break;

            case "map":
                midiMapPath = await HandleMapCommandAsync(mapEditor, mappingEngine, midiMapPath, parts);
                break;

            case "sim":
                HandleSimCommand(mappingEngine, parts);
                break;

            case "q":
            case "quit":
            case "exit":
                if (mixer is not null)
                {
                    await mixer.DisposeAsync();
                }

                return 0;

            default:
                Console.WriteLine($"Unknown command '{parts[0]}'. Type 'help' to see commands.");
                break;
        }
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
    }
}

if (mixer is not null)
{
    await mixer.DisposeAsync();
}

return 0;

static string GetDefaultMidiMapPath()
{
    string outputPath = Path.Combine(AppContext.BaseDirectory, "midi-map.json");

    if (File.Exists(outputPath))
    {
        return outputPath;
    }

    string sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "midi-map.json"));

    return File.Exists(sourcePath) ? sourcePath : outputPath;
}

static async Task TryLoadDefaultMapAsync(MidiMapEditor mapEditor, string path)
{
    try
    {
        await mapEditor.LoadAsync(path);
        Console.WriteLine($"Map loaded: {path}");
        Console.WriteLine();
    }
    catch (Exception exception)
    {
        Console.WriteLine($"Map was not loaded: {exception.Message}");
        Console.WriteLine("Using built-in default bank map.");
        Console.WriteLine();
    }
}

static async Task<string> GetMixerAddressAsync(BXrNetworkScanner scanner, string? addressFromCommand)
{
    if (!string.IsNullOrWhiteSpace(addressFromCommand))
    {
        return addressFromCommand.Trim();
    }

    while (true)
    {
        Console.Write("XR address or scan: ");
        string? address = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(address))
        {
            continue;
        }

        address = address.Trim();

        if (address.Equals("scan", StringComparison.OrdinalIgnoreCase) ||
            address.Equals("s", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<BXrDiscoveredMixer> mixers = await PrintScanResultsAsync(scanner);

            if (mixers.Count == 0)
            {
                continue;
            }

            Console.Write("Select mixer number or press Enter to type address manually: ");
            string? selected = Console.ReadLine();

            if (int.TryParse(selected, out int number) && number >= 1 && number <= mixers.Count)
            {
                return mixers[number - 1].Address.ToString();
            }

            continue;
        }

        return address;
    }
}

static async Task<IReadOnlyList<BXrDiscoveredMixer>> PrintScanResultsAsync(BXrNetworkScanner scanner)
{
    Console.WriteLine("Scanning local /24 subnet(s) for XR /xinfo responses...");

    IReadOnlyList<BXrDiscoveredMixer> mixers = await BXrNetworkScanner.ScanAsync();

    if (mixers.Count == 0)
    {
        Console.WriteLine("No XR responses found.");
        return mixers;
    }

    Console.WriteLine("Found:");

    for (int index = 0; index < mixers.Count; index++)
    {
        BXrDiscoveredMixer mixer = mixers[index];
        Console.WriteLine($"  {index + 1}. {mixer.Address}");

        foreach (string message in mixer.Messages)
        {
            Console.WriteLine($"     {message}");
        }
    }

    return mixers;
}

static async Task<(BXrMixerClient? Mixer, string? Address)> HandleMixerCommandAsync(
    BXrMixerClient? currentMixer,
    string? connectedMixerAddress,
    MappingEngine mappingEngine,
    object mappingLock,
    BXrNetworkScanner scanner,
    string[] parts)
{
    if (parts.Length < 2)
    {
        PrintMixerHelp();
        return (currentMixer, connectedMixerAddress);
    }

    string command = parts[1].ToLowerInvariant();

    switch (command)
    {
        case "connect":
            string? addressFromCommand = parts.Length >= 3 ? parts[2] : null;
            string mixerAddress = await GetMixerAddressAsync(scanner, addressFromCommand);

            if (currentMixer is not null)
            {
                await currentMixer.DisposeAsync();
            }

            BXrMixerClient mixer = new(new BXrConnectionSettings(mixerAddress));
            mixer.MessageReceived += (_, eventArgs) =>
            {
                if (!TryCreateMixerValueChange(eventArgs.Message, out MixerValueChange? change) || change is null)
                {
                    return;
                }

                MappingResult result;

                lock (mappingLock)
                {
                    result = mappingEngine.HandleMixerChange(change);
                }

                if (result.IsMapped)
                {
                    PrintMappingResult("MIXER", result);
                }
            };

            Console.WriteLine($"Connecting to XR at {mixerAddress}:{BXrConnectionSettings.DefaultOscPort}...");

            try
            {
                await mixer.StartAsync();
            }
            catch
            {
                await mixer.DisposeAsync();
                throw;
            }

            Console.WriteLine("Mixer connected. Incoming OSC values are printed as they arrive.");
            return (mixer, mixerAddress);

        case "disconnect":
            if (currentMixer is null)
            {
                Console.WriteLine("Mixer is not connected.");
                return (null, null);
            }

            await currentMixer.DisposeAsync();
            Console.WriteLine("Mixer disconnected.");
            return (null, null);

        case "status":
            Console.WriteLine(currentMixer is not null
                ? $"Mixer connected: {connectedMixerAddress}:{BXrConnectionSettings.DefaultOscPort}"
                : "Mixer is not connected.");
            return (currentMixer, connectedMixerAddress);

        case "scan":
            await PrintScanResultsAsync(scanner);
            return (currentMixer, connectedMixerAddress);

        case "help":
            PrintMixerHelp();
            return (currentMixer, connectedMixerAddress);

        default:
            Console.WriteLine($"Unknown mixer command '{parts[1]}'.");
            PrintMixerHelp();
            return (currentMixer, connectedMixerAddress);
    }
}

static int ReadChannel(string[] parts)
{
    if (parts.Length < 2)
    {
        throw new ArgumentException("Channel is required. Example: mute 1");
    }

    if (!int.TryParse(parts[1], out int channel))
    {
        throw new ArgumentException($"'{parts[1]}' is not a valid channel number.");
    }

    return channel;
}

static BXrMixerClient GetConnectedMixer(BXrMixerClient? mixer)
{
    if (mixer is null)
    {
        throw new InvalidOperationException("Mixer is not connected. Use 'mixer connect' first.");
    }

    return mixer;
}

static ControllerInputChange ToControllerInputChange(MidiControlChange change)
{
    return new ControllerInputChange(
        ToCoreMidiMessageKind(change.Kind),
        change.Channel,
        change.Number,
        change.NormalizedValue,
        change.RawEvent);
}

static MidiMessageKind ToCoreMidiMessageKind(MidiControlKind kind)
{
    return kind switch
    {
        MidiControlKind.ControlChange => MidiMessageKind.ControlChange,
        MidiControlKind.NoteOn => MidiMessageKind.NoteOn,
        MidiControlKind.NoteOff => MidiMessageKind.NoteOff,
        MidiControlKind.PitchBend => MidiMessageKind.PitchBend,
        _ => MidiMessageKind.Other
    };
}

static void HandleBankCommand(MappingEngine mappingEngine, string[] parts)
{
    if (parts.Length < 2)
    {
        PrintBankHelp();
        return;
    }

    string command = parts[1].ToLowerInvariant();

    switch (command)
    {
        case "list":
            PrintBankList(mappingEngine);
            break;

        case "status":
            PrintBankStatus(mappingEngine);
            break;

        case "layout":
            PrintBankLayout(mappingEngine);
            break;

        case "next":
        case "r":
        case "right":
        case "bankr":
            PrintCurrentBank("BANK", mappingEngine.NextBank());
            break;

        case "prev":
        case "previous":
        case "l":
        case "left":
        case "bankl":
            PrintCurrentBank("BANK", mappingEngine.PreviousBank());
            break;

        case "select":
            SelectBank(mappingEngine, parts);
            break;

        case "rename":
            RenameBank(mappingEngine, parts);
            break;

        case "color":
            SetBankColor(mappingEngine, parts);
            break;

        case "help":
            PrintBankHelp();
            break;

        default:
            Console.WriteLine($"Unknown bank command '{parts[1]}'.");
            PrintBankHelp();
            break;
    }
}

static async Task<string> HandleMapCommandAsync(
    MidiMapEditor mapEditor,
    MappingEngine mappingEngine,
    string currentPath,
    string[] parts)
{
    if (parts.Length < 2)
    {
        PrintMapHelp();
        return currentPath;
    }

    string command = parts[1].ToLowerInvariant();

    switch (command)
    {
        case "save":
            string savePath = parts.Length >= 3 ? parts[2] : currentPath;
            await mapEditor.SaveAsync(savePath);
            Console.WriteLine($"Map saved: {savePath}");
            return savePath;

        case "load":
            string loadPath = parts.Length >= 3 ? parts[2] : currentPath;
            await mapEditor.LoadAsync(loadPath);
            Console.WriteLine($"Map loaded: {loadPath}");
            return loadPath;

        case "list":
            PrintMapSlots(mappingEngine);
            return currentPath;

        case "show":
            ShowMapSlot(mapEditor, mappingEngine, parts);
            return currentPath;

        case "set":
            SetMapValue(mapEditor, mappingEngine, parts);
            return currentPath;

        case "clear":
            ClearMapValue(mapEditor, mappingEngine, parts);
            return currentPath;

        case "commands":
            PrintMixerCommandCatalog(mapEditor.CommandCatalog);
            return currentPath;

        case "path":
            Console.WriteLine($"Current map path: {currentPath}");
            return currentPath;

        case "help":
            PrintMapHelp();
            return currentPath;

        default:
            Console.WriteLine($"Unknown map command '{parts[1]}'.");
            PrintMapHelp();
            return currentPath;
    }
}

static void PrintMapSlots(MappingEngine mappingEngine)
{
    Console.WriteLine($"Map slots for bank {mappingEngine.CurrentBank.Index + 1}: {mappingEngine.CurrentBank.Name}");

    foreach (ControlSlot slot in mappingEngine.CurrentBank.Slots)
    {
        Console.WriteLine($"{slot.Id}: {slot.Label} midi={FormatMidiBinding(slot.MidiBinding)} mixer={FormatMixerBinding(slot.MixerBinding)}");
    }
}

static void ShowMapSlot(MidiMapEditor mapEditor, MappingEngine mappingEngine, string[] parts)
{
    if (parts.Length < 3)
    {
        throw new ArgumentException("Slot id is required. Example: map show fader-01");
    }

    ControlSlot slot = mapEditor.GetSlot(mappingEngine.CurrentBank.Index, parts[2]);
    Console.WriteLine($"{slot.Id}: {slot.Label}");
    Console.WriteLine($"  MIDI:  {FormatMidiBinding(slot.MidiBinding)}");
    Console.WriteLine($"  Mixer: {FormatMixerBinding(slot.MixerBinding)}");
}

static void SetMapValue(MidiMapEditor mapEditor, MappingEngine mappingEngine, string[] parts)
{
    if (parts.Length < 4)
    {
        throw new ArgumentException("Map set command is incomplete. Type 'map help'.");
    }

    string target = parts[2].ToLowerInvariant();
    string slotId = parts[3];
    int bankIndex = mappingEngine.CurrentBank.Index;

    switch (target)
    {
        case "label":
            if (parts.Length < 5)
            {
                throw new ArgumentException("Label is required. Example: map set label fader-01 Vocal 1");
            }

            mapEditor.SetSlotLabel(bankIndex, slotId, string.Join(' ', parts.Skip(4)));
            break;

        case "midi":
            if (parts.Length < 7)
            {
                throw new ArgumentException("MIDI binding requires kind, channel and number. Example: map set midi fader-01 cc 1 25");
            }

            mapEditor.SetMidiBinding(bankIndex, slotId, new MidiBinding(
                ParseMidiMessageKind(parts[4]),
                ReadInt(parts[5], "MIDI channel"),
                ReadInt(parts[6], "MIDI number")));
            break;

        case "osc":
            if (parts.Length < 5)
            {
                throw new ArgumentException("OSC address is required. Example: map set osc fader-01 /ch/01/mix/fader");
            }

            mapEditor.SetMixerBinding(bankIndex, slotId, new MixerBinding(parts[4], parts.Length >= 6 ? ParseMixerValueKind(parts[5]) : MixerValueKind.Level));
            break;

        case "command":
            if (parts.Length < 6)
            {
                throw new ArgumentException("Mixer command requires key and channel. Example: map set command fader-01 main 1");
            }

            string commandKey = parts[4];
            int channel = ReadInt(parts[5], "XR channel");
            int? index = parts.Length >= 7 ? ReadInt(parts[6], "command index") : null;
            mapEditor.AssignMixerCommand(bankIndex, slotId, commandKey, channel, index);
            break;

        default:
            throw new ArgumentException($"Unknown map set target '{parts[2]}'.");
    }

    ControlSlot slot = mapEditor.GetSlot(bankIndex, slotId);
    Console.WriteLine($"{slot.Id}: {slot.Label} midi={FormatMidiBinding(slot.MidiBinding)} mixer={FormatMixerBinding(slot.MixerBinding)}");
}

static void ClearMapValue(MidiMapEditor mapEditor, MappingEngine mappingEngine, string[] parts)
{
    if (parts.Length < 4)
    {
        throw new ArgumentException("Map clear command is incomplete. Example: map clear midi fader-01");
    }

    string target = parts[2].ToLowerInvariant();
    string slotId = parts[3];
    int bankIndex = mappingEngine.CurrentBank.Index;

    switch (target)
    {
        case "midi":
            mapEditor.SetMidiBinding(bankIndex, slotId, null);
            break;

        case "osc":
        case "mixer":
            mapEditor.SetMixerBinding(bankIndex, slotId, null);
            break;

        default:
            throw new ArgumentException($"Unknown map clear target '{parts[2]}'.");
    }

    ControlSlot slot = mapEditor.GetSlot(bankIndex, slotId);
    Console.WriteLine($"{slot.Id}: {slot.Label} midi={FormatMidiBinding(slot.MidiBinding)} mixer={FormatMixerBinding(slot.MixerBinding)}");
}

static void PrintMixerCommandCatalog(MixerCommandCatalog commandCatalog)
{
    foreach (MixerCommandDefinition command in commandCatalog.Commands.OrderBy(command => command.Key))
    {
        string index = command.MinIndex is null ? "none" : $"{command.MinIndex}-{command.MaxIndex}";
        Console.WriteLine($"{command.Key}: kind={command.ValueKind} index={index} pattern={command.AddressPattern}");
        Console.WriteLine($"  {command.Description}");
    }
}

static void HandleSimCommand(MappingEngine mappingEngine, string[] parts)
{
    if (parts.Length < 2)
    {
        PrintSimHelp();
        return;
    }

    string command = parts[1].ToLowerInvariant();

    switch (command)
    {
        case "midi":
            SimulateMidi(mappingEngine, parts);
            break;

        case "mixer":
            SimulateMixer(mappingEngine, parts);
            break;

        case "help":
            PrintSimHelp();
            break;

        default:
            Console.WriteLine($"Unknown sim command '{parts[1]}'.");
            PrintSimHelp();
            break;
    }
}

static void SimulateMidi(MappingEngine mappingEngine, string[] parts)
{
    if (parts.Length < 6)
    {
        throw new ArgumentException("MIDI simulation requires kind, channel, number and value. Example: sim midi cc 1 26 80");
    }

    MidiMessageKind kind = ParseMidiMessageKind(parts[2]);
    int channel = ReadInt(parts[3], "MIDI channel");
    int number = ReadInt(parts[4], "MIDI number");
    int rawValue = ReadInt(parts[5], "MIDI value");

    if (rawValue is < 0 or > 127)
    {
        throw new ArgumentOutOfRangeException(nameof(rawValue), "MIDI value must be in range 0-127.");
    }

    MappingResult result = mappingEngine.HandleControllerChange(new ControllerInputChange(
        kind,
        channel,
        number,
        rawValue / 127.0,
        $"sim midi {kind} ch={channel} number={number} value={rawValue}"));

    PrintMappingResult("SIM MIDI", result);
}

static void SimulateMixer(MappingEngine mappingEngine, string[] parts)
{
    if (parts.Length < 4)
    {
        throw new ArgumentException("Mixer simulation requires OSC address and value. Example: sim mixer /ch/01/mix/fader 0.75");
    }

    string oscAddress = parts[2];
    double value = ReadDouble(parts[3], "mixer value");

    MappingResult result = mappingEngine.HandleMixerChange(new MixerValueChange(oscAddress, value));

    PrintMappingResult("SIM MIXER", result);
}

static bool TryCreateMixerValueChange(OscMessage message, out MixerValueChange? change)
{
    change = null;

    if (message.Count == 0 || !TryReadOscNumber(message[0], out double value))
    {
        return false;
    }

    change = new MixerValueChange(message.Address, value);
    return true;
}

static bool TryReadOscNumber(object? value, out double number)
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

static void PrintMappingResult(string prefix, MappingResult result)
{
    if (result.Slot is not null)
    {
        PrintSlotState(prefix, result.Slot);
    }

    if (result.Message is not null)
    {
        Console.WriteLine($"{prefix} {result.Message}");
    }

    if (result.MixerCommand is not null)
    {
        PrintMixerOutputCommand(prefix, result.MixerCommand);
    }
}

static async Task SendMixerCommandIfConnectedAsync(BXrMixerClient? mixer, MixerOutputCommand? command)
{
    if (mixer is null || command is null)
    {
        return;
    }

    await mixer.SendOscValueAsync(
        command.OscAddress,
        command.Value,
        sendInteger: command.ValueKind is MixerValueKind.Toggle or MixerValueKind.Action);
}

static void PrintMixerOutputCommand(string prefix, MixerOutputCommand command)
{
    Console.WriteLine($"{prefix} OSC ready: {command.OscAddress} {command.Value.ToString("0.###", CultureInfo.InvariantCulture)} kind={command.ValueKind}");
}

static double ReadDouble(string value, string name)
{
    if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double result))
    {
        throw new ArgumentException($"'{value}' is not a valid {name}.");
    }

    return result;
}

static MidiMessageKind ParseMidiMessageKind(string value)
{
    return value.ToLowerInvariant() switch
    {
        "cc" or "controlchange" => MidiMessageKind.ControlChange,
        "note" or "noteon" => MidiMessageKind.NoteOn,
        "noteoff" => MidiMessageKind.NoteOff,
        "pitch" or "pitchbend" => MidiMessageKind.PitchBend,
        _ => throw new ArgumentException($"Unknown MIDI kind '{value}'.")
    };
}

static MixerValueKind ParseMixerValueKind(string value)
{
    return value.ToLowerInvariant() switch
    {
        "level" => MixerValueKind.Level,
        "toggle" or "bool" or "button" => MixerValueKind.Toggle,
        "pan" => MixerValueKind.Pan,
        "action" => MixerValueKind.Action,
        _ => throw new ArgumentException($"Unknown mixer value kind '{value}'.")
    };
}

static int ReadInt(string value, string name)
{
    if (!int.TryParse(value, out int result))
    {
        throw new ArgumentException($"'{value}' is not a valid {name}.");
    }

    return result;
}

static string FormatMidiBinding(MidiBinding? binding)
{
    return binding is null
        ? "<none>"
        : $"{binding.Kind} ch={binding.Channel} number={binding.Number}";
}

static string FormatMixerBinding(MixerBinding? binding)
{
    return binding is null
        ? "<none>"
        : $"{binding.OscAddress} kind={binding.ValueKind}";
}

static void PrintBankStatus(MappingEngine mappingEngine)
{
    PrintCurrentBank("BANK", mappingEngine.CurrentBank);
    Console.WriteLine($"Assignable controls: {mappingEngine.CurrentBank.Slots.Count}");

    foreach (ControlSlot slot in mappingEngine.CurrentBank.Slots.OrderBy(slot => GetPhysicalSortKey(slot.Id)))
    {
        PrintSlotDetails("BANK", slot);
    }
}

static void PrintBankLayout(MappingEngine mappingEngine)
{
    PrintCurrentBank("LAYOUT", mappingEngine.CurrentBank);
    PrintSlotRow(mappingEngine.CurrentBank, "Special buttons", ["bank-prev", "bank-next", "solo", "send-all"]);
    PrintSlotRow(mappingEngine.CurrentBank, "Master", ["fader-01"]);
    PrintSlotRow(mappingEngine.CurrentBank, "Levels", Enumerable.Range(2, 8).Select(number => $"fader-{number:00}"));
    PrintSlotRow(mappingEngine.CurrentBank, "Bus 1", Enumerable.Range(1, 8).Select(number => $"knob-{number:00}"));
    PrintSlotRow(mappingEngine.CurrentBank, "Bus 2", Enumerable.Range(9, 8).Select(number => $"knob-{number:00}"));
    PrintSlotRow(mappingEngine.CurrentBank, "Bus 3", Enumerable.Range(17, 8).Select(number => $"knob-{number:00}"));
    PrintSlotRow(mappingEngine.CurrentBank, "Mute", Enumerable.Range(1, 8).Select(number => $"button-{number:00}"));
    PrintSlotRow(mappingEngine.CurrentBank, "Buttons 2", Enumerable.Range(9, 8).Select(number => $"button-{number:00}"));
}

static void PrintSlotRow(ControlBank bank, string title, IEnumerable<string> slotIds)
{
    Console.WriteLine($"{title}:");

    foreach (string slotId in slotIds)
    {
        ControlSlot? slot = bank.FindSlotById(slotId);

        if (slot is null)
        {
            Console.WriteLine($"  {slotId}: <missing>");
            continue;
        }

        PrintSlotDetails("  ", slot);
    }
}

static int GetPhysicalSortKey(string slotId)
{
    switch (slotId.ToLowerInvariant())
    {
        case "bank-prev":
            return 10;

        case "bank-next":
            return 20;

        case "solo":
            return 30;

        case "send-all":
            return 40;
    }

    string[] parts = slotId.Split('-', StringSplitOptions.RemoveEmptyEntries);

    if (parts.Length != 2 || !int.TryParse(parts[1], out int number))
    {
        return int.MaxValue;
    }

    return parts[0] switch
    {
        "fader" => number == 1 ? 0 : 100 + number,
        "knob" => 200 + number,
        "button" => 300 + number,
        _ => int.MaxValue
    };
}

static void PrintSlotDetails(string prefix, ControlSlot slot)
{
    PrintSlotState(prefix, slot.Snapshot());
    Console.WriteLine($"{prefix}  midi={FormatMidiBinding(slot.MidiBinding)} mixer={FormatMixerBinding(slot.MixerBinding)}");
}

static void PrintBankList(MappingEngine mappingEngine)
{
    foreach (ControlBank bank in mappingEngine.Banks)
    {
        string marker = bank == mappingEngine.CurrentBank ? "*" : " ";
        Console.WriteLine($"{marker} {bank.Index + 1}. {bank.Name} color={bank.Color.ToHexString()}");
    }
}

static void SelectBank(MappingEngine mappingEngine, string[] parts)
{
    if (parts.Length < 3)
    {
        throw new ArgumentException("Bank number is required. Example: bank select 2");
    }

    if (!int.TryParse(parts[2], out int bankNumber))
    {
        throw new ArgumentException($"'{parts[2]}' is not a valid bank number.");
    }

    ControlBank bank = mappingEngine.SelectBank(bankNumber - 1);
    PrintCurrentBank("BANK", bank);
}

static void RenameBank(MappingEngine mappingEngine, string[] parts)
{
    if (parts.Length < 3)
    {
        throw new ArgumentException("Bank name is required. Example: bank rename Vocals");
    }

    string name = string.Join(' ', parts.Skip(2));
    mappingEngine.CurrentBank.Rename(name);
    PrintCurrentBank("BANK", mappingEngine.CurrentBank);
}

static void SetBankColor(MappingEngine mappingEngine, string[] parts)
{
    if (parts.Length < 5)
    {
        throw new ArgumentException("RGB values are required. Example: bank color 255 0 0");
    }

    RgbColor color = new(ReadByte(parts[2], "red"), ReadByte(parts[3], "green"), ReadByte(parts[4], "blue"));
    mappingEngine.CurrentBank.SetColor(color);
    PrintCurrentBank("BANK", mappingEngine.CurrentBank);
    Console.WriteLine("BANK physical bank button color command is not implemented yet; protocol is unknown.");
}

static byte ReadByte(string value, string name)
{
    if (!byte.TryParse(value, out byte result))
    {
        throw new ArgumentException($"'{value}' is not a valid {name} value. Use 0-255.");
    }

    return result;
}

static void PrintCurrentBank(string prefix, ControlBank bank)
{
    Console.WriteLine($"{prefix} current={bank.Index + 1}. {bank.Name} color={bank.Color.ToHexString()} rgb=({bank.Color.Red},{bank.Color.Green},{bank.Color.Blue})");
}

static void PrintSlotState(string prefix, ControlSlotSnapshot slot)
{
    Console.WriteLine(
        $"{prefix} {slot.Label}: controller={FormatValue(slot.ControllerValue)} mixer={FormatValue(slot.MixerValue)} locked={slot.IsLocked}");
}

static string FormatValue(double? value)
{
    return value is null ? "<none>" : value.Value.ToString("0.000", CultureInfo.InvariantCulture);
}

static void HandleMidiCommand(MidiInputManager midi, string[] parts)
{
    if (parts.Length < 2)
    {
        PrintMidiHelp();
        return;
    }

    string command = parts[1].ToLowerInvariant();

    switch (command)
    {
        case "list":
        case "devices":
            PrintMidiDevices(midi);
            break;

        case "connect":
            ConnectMidiDevice(midi, parts);
            break;

        case "disconnect":
            midi.Disconnect();
            Console.WriteLine("MIDI disconnected.");
            break;

        case "status":
            Console.WriteLine(midi.IsConnected
                ? $"MIDI connected: {midi.ConnectedDeviceName}"
                : "MIDI is not connected.");
            break;

        case "help":
            PrintMidiHelp();
            break;

        default:
            Console.WriteLine($"Unknown MIDI command '{parts[1]}'.");
            PrintMidiHelp();
            break;
    }
}

static void PrintMidiDevices(MidiInputManager midi)
{
    IReadOnlyList<MidiInputDeviceInfo> devices = midi.GetInputDevices();

    if (devices.Count == 0)
    {
        Console.WriteLine("No MIDI input devices found.");
        return;
    }

    Console.WriteLine("MIDI input devices:");

    foreach (MidiInputDeviceInfo device in devices)
    {
        Console.WriteLine($"  {device.Index}: {device.Name}");
    }
}

static void ConnectMidiDevice(MidiInputManager midi, string[] parts)
{
    if (parts.Length < 3)
    {
        throw new ArgumentException("MIDI device index is required. Example: midi connect 0");
    }

    if (!int.TryParse(parts[2], out int index))
    {
        throw new ArgumentException($"'{parts[2]}' is not a valid MIDI device index.");
    }

    midi.ConnectByIndex(index);
    Console.WriteLine($"MIDI connected: {midi.ConnectedDeviceName}");
}

static void PrintMidiHelp()
{
    Console.WriteLine();
    Console.WriteLine("MIDI commands:");
    Console.WriteLine("  midi list             List MIDI input devices");
    Console.WriteLine("  midi connect <index>  Connect MIDI input device");
    Console.WriteLine("  midi disconnect       Disconnect MIDI input device");
    Console.WriteLine("  midi status           Show MIDI connection status");
    Console.WriteLine("  midi help             Show MIDI commands");
    Console.WriteLine();
}

static void PrintMixerHelp()
{
    Console.WriteLine();
    Console.WriteLine("Mixer commands:");
    Console.WriteLine("  mixer connect [address]  Connect XR. Without address, ask in console");
    Console.WriteLine("  mixer disconnect         Disconnect XR");
    Console.WriteLine("  mixer status             Show mixer connection status");
    Console.WriteLine("  mixer scan               Search local /24 subnet(s)");
    Console.WriteLine("  mixer help               Show mixer commands");
    Console.WriteLine();
}

static void PrintBankHelp()
{
    Console.WriteLine();
    Console.WriteLine("Bank commands:");
    Console.WriteLine("  bank list             Show all banks");
    Console.WriteLine("  bank status           Show current bank control states");
    Console.WriteLine("  bank layout           Show current bank in physical layout order");
    Console.WriteLine("  bank next             Switch to next bank, same as bankR");
    Console.WriteLine("  bank prev             Switch to previous bank, same as bankL");
    Console.WriteLine("  bank select <1-7>     Select bank by number");
    Console.WriteLine("  bank rename <name>    Rename current bank");
    Console.WriteLine("  bank color <r> <g> <b>");
    Console.WriteLine("  bank help             Show bank commands");
    Console.WriteLine();
}

static void PrintMapHelp()
{
    Console.WriteLine();
    Console.WriteLine("Map commands use the current bank selected by 'bank select'.");
    Console.WriteLine("  map save [path]");
    Console.WriteLine("  map load [path]");
    Console.WriteLine("  map path");
    Console.WriteLine("  map list");
    Console.WriteLine("  map show <slotId>");
    Console.WriteLine("  map set label <slotId> <label>");
    Console.WriteLine("  map set midi <slotId> <cc|note|noteoff|pitch> <channel> <number>");
    Console.WriteLine("  map set osc <slotId> <oscAddress> [level|toggle|pan|action]");
    Console.WriteLine("  map set command <slotId> <main|mute|pan|bus|aux|fx|bus-on|fx-on> <channel> [index]");
    Console.WriteLine("  map clear midi <slotId>");
    Console.WriteLine("  map clear osc <slotId>");
    Console.WriteLine("  map commands");
    Console.WriteLine("  map help");
    Console.WriteLine();
}

static void PrintSimHelp()
{
    Console.WriteLine();
    Console.WriteLine("Simulation commands:");
    Console.WriteLine("  sim midi <cc|note|noteoff|pitch> <channel> <number> <0-127>");
    Console.WriteLine("  sim mixer <oscAddress> <0.0-1.0>");
    Console.WriteLine("  sim help");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  sim midi cc 1 26 80");
    Console.WriteLine("  sim midi note 1 34 127");
    Console.WriteLine("  sim mixer /ch/01/mix/fader 0.75");
    Console.WriteLine();
}

static void PrintHelp()
{
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  mixer connect [address]  Connect XR");
    Console.WriteLine("  mixer disconnect         Disconnect XR");
    Console.WriteLine("  mixer status             Show XR connection status");
    Console.WriteLine("  mixer scan               Search local /24 subnet(s)");
    Console.WriteLine("  scan                     Same as mixer scan");
    Console.WriteLine("  mute <1-18>              Mute channel, requires connected mixer");
    Console.WriteLine("  unmute <1-18>            Unmute channel, requires connected mixer");
    Console.WriteLine("  midi list                List MIDI input devices");
    Console.WriteLine("  midi connect <index>     Connect and log MIDI changes");
    Console.WriteLine("  midi disconnect          Disconnect MIDI input device");
    Console.WriteLine("  midi status              Show MIDI connection status");
    Console.WriteLine("  bank list                Show all banks");
    Console.WriteLine("  bank next                Switch to next bank");
    Console.WriteLine("  bank prev                Switch to previous bank");
    Console.WriteLine("  bank status              Show current bank control states");
    Console.WriteLine("  bank layout              Show current bank physical layout");
    Console.WriteLine("  map load                 Load default MIDI/OSC map JSON");
    Console.WriteLine("  map help                 Show MIDI/OSC map editor commands");
    Console.WriteLine("  sim help                 Show simulation commands");
    Console.WriteLine("  help                     Show commands");
    Console.WriteLine("  exit                     Stop diagnostics");
    Console.WriteLine();
}
