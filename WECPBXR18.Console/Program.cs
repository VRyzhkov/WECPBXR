using WECPBXR18.Hardware;

Console.WriteLine("WECPBXR18 diagnostics");
Console.WriteLine();

Xr18NetworkScanner scanner = new();
using MidiInputManager midi = new();
Xr18MixerClient? mixer = null;
string? connectedMixerAddress = null;

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
                    scanner,
                    parts);
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

static async Task<string> GetMixerAddressAsync(Xr18NetworkScanner scanner, string? addressFromCommand)
{
    if (!string.IsNullOrWhiteSpace(addressFromCommand))
    {
        return addressFromCommand.Trim();
    }

    while (true)
    {
        Console.Write("XR18 address or scan: ");
        string? address = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(address))
        {
            continue;
        }

        address = address.Trim();

        if (address.Equals("scan", StringComparison.OrdinalIgnoreCase) ||
            address.Equals("s", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<Xr18DiscoveredMixer> mixers = await PrintScanResultsAsync(scanner);

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

static async Task<IReadOnlyList<Xr18DiscoveredMixer>> PrintScanResultsAsync(Xr18NetworkScanner scanner)
{
    Console.WriteLine("Scanning local /24 subnet(s) for XR18 /xinfo responses...");

    IReadOnlyList<Xr18DiscoveredMixer> mixers = await scanner.ScanAsync();

    if (mixers.Count == 0)
    {
        Console.WriteLine("No XR18 responses found.");
        return mixers;
    }

    Console.WriteLine("Found:");

    for (int index = 0; index < mixers.Count; index++)
    {
        Xr18DiscoveredMixer mixer = mixers[index];
        Console.WriteLine($"  {index + 1}. {mixer.Address}");

        foreach (string message in mixer.Messages)
        {
            Console.WriteLine($"     {message}");
        }
    }

    return mixers;
}

static async Task<(Xr18MixerClient? Mixer, string? Address)> HandleMixerCommandAsync(
    Xr18MixerClient? currentMixer,
    string? connectedMixerAddress,
    Xr18NetworkScanner scanner,
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

            Xr18MixerClient mixer = new(new Xr18ConnectionSettings(mixerAddress));

            Console.WriteLine($"Connecting to XR18 at {mixerAddress}:{Xr18ConnectionSettings.DefaultOscPort}...");

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
                ? $"Mixer connected: {connectedMixerAddress}:{Xr18ConnectionSettings.DefaultOscPort}"
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

static Xr18MixerClient GetConnectedMixer(Xr18MixerClient? mixer)
{
    if (mixer is null)
    {
        throw new InvalidOperationException("Mixer is not connected. Use 'mixer connect' first.");
    }

    return mixer;
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
    Console.WriteLine("  mixer connect [address]  Connect XR18. Without address, ask in console");
    Console.WriteLine("  mixer disconnect         Disconnect XR18");
    Console.WriteLine("  mixer status             Show mixer connection status");
    Console.WriteLine("  mixer scan               Search local /24 subnet(s)");
    Console.WriteLine("  mixer help               Show mixer commands");
    Console.WriteLine();
}

static void PrintHelp()
{
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  mixer connect [address]  Connect XR18");
    Console.WriteLine("  mixer disconnect         Disconnect XR18");
    Console.WriteLine("  mixer status             Show XR18 connection status");
    Console.WriteLine("  mixer scan               Search local /24 subnet(s)");
    Console.WriteLine("  scan                     Same as mixer scan");
    Console.WriteLine("  mute <1-18>              Mute channel, requires connected mixer");
    Console.WriteLine("  unmute <1-18>            Unmute channel, requires connected mixer");
    Console.WriteLine("  midi list                List MIDI input devices");
    Console.WriteLine("  midi connect <index>     Connect and log MIDI changes");
    Console.WriteLine("  midi disconnect          Disconnect MIDI input device");
    Console.WriteLine("  midi status              Show MIDI connection status");
    Console.WriteLine("  help                     Show commands");
    Console.WriteLine("  exit                     Stop diagnostics");
    Console.WriteLine();
}
