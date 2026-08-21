using System.Text.Json;
using System.Text.Json.Serialization;
using WECPBXR.Core.Mapping;
using WECPBXR.Core.Models;

namespace WECPBXR.Core.Configuration;

public sealed class MidiMapConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static MidiMapConfiguration CreateConfiguration(BankSet bankSet)
    {
        return new MidiMapConfiguration
        {
            Banks = [.. bankSet.Banks.Select(CreateBankConfiguration)]
        };
    }

    public static async Task SaveAsync(BankSet bankSet, string path, CancellationToken cancellationToken = default)
    {
        MidiMapConfiguration configuration = CreateConfiguration(bankSet);
        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, configuration, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<MidiMapConfiguration> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = File.OpenRead(path);
        MidiMapConfiguration? configuration = await JsonSerializer.DeserializeAsync<MidiMapConfiguration>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return configuration ?? throw new InvalidOperationException($"MIDI map file '{path}' is empty.");
    }

    public static void Apply(BankSet bankSet, MidiMapConfiguration configuration)
    {
        foreach (BankMapConfiguration bankConfiguration in configuration.Banks)
        {
            ControlBank? bank = bankSet.Banks.FirstOrDefault(candidate => candidate.Index == bankConfiguration.Index);

            if (bank is null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(bankConfiguration.Name))
            {
                bank.Rename(bankConfiguration.Name);
            }

            bank.SetColor(new RgbColor(
                bankConfiguration.Color.Red,
                bankConfiguration.Color.Green,
                bankConfiguration.Color.Blue));

            foreach (SlotMapConfiguration slotConfiguration in bankConfiguration.Slots)
            {
                ControlSlot? slot = bank.FindSlotById(slotConfiguration.Id);

                if (slot is null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(slotConfiguration.Label))
                {
                    slot.SetLabel(slotConfiguration.Label);
                }

                slot.SetMidiBinding(slotConfiguration.Midi is null
                    ? null
                    : new MidiBinding(slotConfiguration.Midi.Kind, slotConfiguration.Midi.Channel, slotConfiguration.Midi.Number));

                slot.SetMixerBinding(slotConfiguration.Mixer is null
                    ? null
                    : new MixerBinding(slotConfiguration.Mixer.OscAddress, slotConfiguration.Mixer.ValueKind));
            }
        }
    }

    private static BankMapConfiguration CreateBankConfiguration(ControlBank bank)
    {
        return new BankMapConfiguration
        {
            Index = bank.Index,
            Name = bank.Name,
            Color = new RgbColorConfiguration
            {
                Red = bank.Color.Red,
                Green = bank.Color.Green,
                Blue = bank.Color.Blue
            },
            Slots = [.. bank.Slots.Select(CreateSlotConfiguration)]
        };
    }

    private static SlotMapConfiguration CreateSlotConfiguration(ControlSlot slot)
    {
        return new SlotMapConfiguration
        {
            Id = slot.Id,
            Label = slot.Label,
            Midi = slot.MidiBinding is null
                ? null
                : new MidiBindingConfiguration
                {
                    Kind = slot.MidiBinding.Kind,
                    Channel = slot.MidiBinding.Channel,
                    Number = slot.MidiBinding.Number
                },
            Mixer = slot.MixerBinding is null
                ? null
                : new MixerBindingConfiguration
                {
                    OscAddress = slot.MixerBinding.OscAddress,
                    ValueKind = slot.MixerBinding.ValueKind
                }
        };
    }
}
