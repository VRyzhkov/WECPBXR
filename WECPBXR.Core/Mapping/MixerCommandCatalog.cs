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
            new("bus", "Input channel send level to bus 1-6", MixerValueKind.Level, 1, 6, "/ch/{channel:00}/mix/{index:00}/level"),
            new("aux", "Alias for bus 1-6, normally routed to XR18 AUX outputs", MixerValueKind.Level, 1, 6, "/ch/{channel:00}/mix/{index:00}/level"),
            new("fx", "Input channel send level to FX 1-4. Uses send indexes 7-10 until verified on hardware", MixerValueKind.Level, 1, 4, "/ch/{channel:00}/mix/{fxSendIndex:00}/level"),
            new("bus-on", "Input channel send on/off to bus 1-6", MixerValueKind.Toggle, 1, 6, "/ch/{channel:00}/mix/{index:00}/on"),
            new("fx-on", "Input channel send on/off to FX 1-4. Uses send indexes 7-10 until verified on hardware", MixerValueKind.Toggle, 1, 4, "/ch/{channel:00}/mix/{fxSendIndex:00}/on")
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

        if (channel is < 1 or > 18)
        {
            throw new ArgumentOutOfRangeException(nameof(channel), "XR18 channel must be in range 1-18.");
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
            .Replace("{fxSendIndex:00}", fxSendIndex.ToString("00"), StringComparison.Ordinal);

        return new MixerBinding(address, command.ValueKind);
    }
}
