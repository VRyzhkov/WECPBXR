using WECPBXR.Core.Models;

namespace WECPBXR.Core.Mapping;

public sealed class MixerCommandCatalog
{
    private readonly Dictionary<string, MixerCommandDefinition> _commands;

    public MixerCommandCatalog()
    {
        MixerCommandDefinition[] commands =
        [
            new("main", "Input channel main LR fader", MixerValueKind.Level, null, null, "/ch/{channel:00}/mix/fader"),
            new("mute", "Input channel mute. XR18 OSC uses /mix/on: 0 means muted, 1 means unmuted", MixerValueKind.Toggle, null, null, "/ch/{channel:00}/mix/on"),
            new("pan", "Input channel pan", MixerValueKind.Pan, null, null, "/ch/{channel:00}/mix/pan"),
            new("solo", "Input channel solo/PFL switch", MixerValueKind.Toggle, null, null, "/-stat/solosw/{channel:00}"),
            new("gain", "Input channel preamp gain. Verify scaling on hardware before live use", MixerValueKind.Level, null, null, "/ch/{channel:00}/preamp/gain", 1, 16),
            new("hpf", "Input channel high-pass filter frequency. Continuous normalized XR parameter", MixerValueKind.Level, null, null, "/ch/{channel:00}/preamp/hpf", 1, 16),
            new("hpf-on", "Input channel high-pass filter on/off", MixerValueKind.Toggle, null, null, "/ch/{channel:00}/preamp/hpon", 1, 16),
            new("gate-on", "Input channel gate on/off", MixerValueKind.Toggle, null, null, "/ch/{channel:00}/gate/on", 1, 16),
            new("gate-threshold", "Input channel gate threshold", MixerValueKind.Level, null, null, "/ch/{channel:00}/gate/thr", 1, 16),
            new("comp-on", "Input channel compressor on/off", MixerValueKind.Toggle, null, null, "/ch/{channel:00}/dyn/on", 1, 16),
            new("comp-threshold", "Input channel compressor threshold", MixerValueKind.Level, null, null, "/ch/{channel:00}/dyn/thr", 1, 16),
            new("eq-on", "Input channel EQ on/off", MixerValueKind.Toggle, null, null, "/ch/{channel:00}/eq/on", 1, 16),
            new("eq-low", "Input channel EQ band 1 gain", MixerValueKind.Level, null, null, "/ch/{channel:00}/eq/1/g", 1, 16),
            new("eq-lowmid", "Input channel EQ band 2 gain", MixerValueKind.Level, null, null, "/ch/{channel:00}/eq/2/g", 1, 16),
            new("eq-highmid", "Input channel EQ band 3 gain", MixerValueKind.Level, null, null, "/ch/{channel:00}/eq/3/g", 1, 16),
            new("eq-high", "Input channel EQ band 4 gain", MixerValueKind.Level, null, null, "/ch/{channel:00}/eq/4/g", 1, 16),
            new("bus", "Input channel send level to bus 1-6", MixerValueKind.Level, 1, 6, "/ch/{channel:00}/mix/{index:00}/level"),
            new("aux", "Alias for bus 1-6, normally routed to XR18 AUX outputs", MixerValueKind.Level, 1, 6, "/ch/{channel:00}/mix/{index:00}/level"),
            new("fx", "Input channel send level to FX 1-4. Uses send indexes 7-10 until verified on hardware", MixerValueKind.Level, 1, 4, "/ch/{channel:00}/mix/{fxSendIndex:00}/level"),
            new("bus-on", "Input channel send on/off to bus 1-6", MixerValueKind.Toggle, 1, 6, "/ch/{channel:00}/mix/{index:00}/on"),
            new("fx-on", "Input channel send on/off to FX 1-4. Uses send indexes 7-10 until verified on hardware", MixerValueKind.Toggle, 1, 4, "/ch/{channel:00}/mix/{fxSendIndex:00}/on"),
            new("master", "Main LR master fader", MixerValueKind.Level, null, null, "/lr/mix/fader"),
            new("master-mute", "Main LR on/off. 0 means muted, 1 means unmuted", MixerValueKind.Toggle, null, null, "/lr/mix/on"),
            new("bus-master", "Bus 1-6 master fader. Use index as bus number", MixerValueKind.Level, 1, 6, "/bus/{index}/mix/fader"),
            new("bus-master-mute", "Bus 1-6 master on/off. Use index as bus number", MixerValueKind.Toggle, 1, 6, "/bus/{index}/mix/on"),
            new("fx-send-master", "FX send 1-4 master fader. Verify OSC path on hardware", MixerValueKind.Level, 1, 4, "/fx/{index}/mix/fader"),
            new("fx-return", "FX return 1-4 level", MixerValueKind.Level, 1, 4, "/rtn/{index}/mix/fader"),
            new("fx-return-mute", "FX return 1-4 on/off", MixerValueKind.Toggle, 1, 4, "/rtn/{index}/mix/on"),
            new("mute-group", "Mute group 1-4 on/off. Use index as mute group number", MixerValueKind.Toggle, 1, 4, "/config/mute/{index}"),
            new("clear-solo", "Clear all active solos", MixerValueKind.Action, null, null, "/-stat/solo"),
            new("tap-tempo", "Tap tempo action for delay-style FX. Verify target OSC behavior on hardware", MixerValueKind.Action, null, null, "/-stat/tap"),
            new("scene-load", "Load internal snapshot 1-64. Use index as snapshot number and verify before live use", MixerValueKind.Action, 1, 64, "/-snap/load/{index}"),
            new("scene-prev", "Load previous snapshot. Verify behavior before live use", MixerValueKind.Action, null, null, "/-snap/prev"),
            new("scene-next", "Load next snapshot. Verify behavior before live use", MixerValueKind.Action, null, null, "/-snap/next")
        ];

        _commands = commands.ToDictionary(command => command.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<MixerCommandDefinition> Commands => _commands.Values;

    public MixerBinding CreateBinding(string key, int channel, int? index = null)
    {
        if (!_commands.TryGetValue(key, out MixerCommandDefinition? command))
        {
            throw new ArgumentException($"Unknown mixer command '{key}'.", nameof(key));
        }

        if (channel < command.MinChannel || channel > command.MaxChannel)
        {
            throw new ArgumentOutOfRangeException(nameof(channel), $"Command '{key}' channel must be in range {command.MinChannel}-{command.MaxChannel}.");
        }

        int resolvedIndex = 0;

        if (command.MinIndex is not null || command.MaxIndex is not null)
        {
            if (index is null)
            {
                throw new ArgumentException($"Command '{key}' requires index {command.MinIndex}-{command.MaxIndex}.", nameof(index));
            }

            if (index < command.MinIndex || index > command.MaxIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Command '{key}' index must be in range {command.MinIndex}-{command.MaxIndex}.");
            }

            resolvedIndex = index.Value;
        }

        int fxSendIndex = key.StartsWith("fx", StringComparison.OrdinalIgnoreCase)
            ? resolvedIndex + 6
            : resolvedIndex;

        string address = command.AddressPattern
            .Replace("{channel:00}", channel.ToString("00"), StringComparison.Ordinal)
            .Replace("{index:00}", resolvedIndex.ToString("00"), StringComparison.Ordinal)
            .Replace("{index}", resolvedIndex.ToString(), StringComparison.Ordinal)
            .Replace("{fxSendIndex:00}", fxSendIndex.ToString("00"), StringComparison.Ordinal);

        return new MixerBinding(address, command.ValueKind);
    }
}
