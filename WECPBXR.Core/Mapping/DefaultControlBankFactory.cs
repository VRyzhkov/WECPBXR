using WECPBXR.Core.Models;

namespace WECPBXR.Core.Mapping;

public static class DefaultControlBankFactory
{
    public const int DefaultBankCount = 8;
    public const int KnobCount = 24;
    public const int FaderCount = 9;
    public const int AssignableButtonCount = 20;
    private const int MatrixButtonCount = 16;
    private const int MidiChannel = 1;
    private static readonly MixerCommandCatalog CommandCatalog = new();

    private static readonly (string Name, RgbColor Color)[] DefaultBanks =
    [
        ("Main mix 1-8", new RgbColor(255, 0, 0)),
        ("Main mix 9-16", new RgbColor(255, 127, 0)),
        ("Monitors", new RgbColor(255, 255, 0)),
        ("FX", new RgbColor(0, 255, 0)),
        ("Dynamics/EQ", new RgbColor(0, 255, 255)),
        ("Utility/Safety", new RgbColor(0, 0, 255)),
        ("Custom 1", new RgbColor(139, 0, 255)),
        ("Custom 2", new RgbColor(255, 0, 255))
    ];

    public static BankSet CreateDefaultBankSet()
    {
        return new BankSet(Enumerable.Range(0, DefaultBankCount).Select(CreateBank));
    }

    public static ControlBank CreateBank(int index = 0)
    {
        if (index < 0 || index >= DefaultBankCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Default bank index must be in range 0-{DefaultBankCount - 1}.");
        }

        (string bankName, RgbColor color) = DefaultBanks[index];
        List<ControlSlot> slots = [];

        for (int i = 1; i <= KnobCount; i++)
        {
            slots.Add(new ControlSlot(
                Id("knob", i),
                $"{bankName} Knob {i}",
                ControlKind.Knob,
                new MidiBinding(MidiMessageKind.ControlChange, MidiChannel, i)));
        }

        for (int i = 1; i <= FaderCount; i++)
        {
            slots.Add(new ControlSlot(
                Id("fader", i),
                $"{bankName} Fader {i}",
                ControlKind.Fader,
                new MidiBinding(MidiMessageKind.ControlChange, MidiChannel, KnobCount + i)));
        }

        slots.Add(new ControlSlot(
            "bank-prev",
            "BANK L",
            ControlKind.Button,
            new MidiBinding(MidiMessageKind.ControlChange, MidiChannel, 34)));

        slots.Add(new ControlSlot(
            "bank-next",
            "BANK R",
            ControlKind.Button,
            new MidiBinding(MidiMessageKind.ControlChange, MidiChannel, 35)));

        slots.Add(new ControlSlot(
            "solo",
            "SOLO",
            ControlKind.Button,
            new MidiBinding(MidiMessageKind.ControlChange, MidiChannel, 36)));

        slots.Add(new ControlSlot(
            "send-all",
            "SEND ALL",
            ControlKind.Button,
            new MidiBinding(MidiMessageKind.ControlChange, MidiChannel, 37)));

        for (int i = 1; i <= MatrixButtonCount; i++)
        {
            slots.Add(new ControlSlot(
                Id("button", i),
                $"{bankName} Button {i}",
                ControlKind.Button,
                new MidiBinding(MidiMessageKind.ControlChange, MidiChannel, 37 + i)));
        }

        ControlBank bank = new(index, bankName, color, slots, []);
        ApplyDefaultAssignments(bank);

        return bank;
    }

    private static string Id(string prefix, int number)
    {
        return $"{prefix}-{number:00}";
    }

    private static void ApplyDefaultAssignments(ControlBank bank)
    {
        switch (bank.Index)
        {
            case 0:
                ConfigureMainMixBank(bank, firstChannel: 1);
                break;

            case 1:
                ConfigureMainMixBank(bank, firstChannel: 9);
                break;

            case 2:
                ConfigureMonitorsBank(bank);
                break;

            case 3:
                ConfigureFxBank(bank);
                break;

            case 4:
                ConfigureDynamicsEqBank(bank);
                break;

            case 5:
                ConfigureUtilitySafetyBank(bank);
                break;
        }
    }

    private static void ConfigureMainMixBank(ControlBank bank, int firstChannel)
    {
        Assign(bank, "fader-01", "Main LR", "master");

        for (int offset = 0; offset < 8; offset++)
        {
            int channel = firstChannel + offset;
            int controlNumber = offset + 1;
            Assign(bank, Id("fader", controlNumber + 1), $"Ch {channel} Level", "main", channel);
            Assign(bank, Id("knob", controlNumber), $"Ch {channel} Pan", "pan", channel);
            Assign(bank, Id("button", controlNumber), $"Ch {channel} Mute", "mute", channel);
            Assign(bank, Id("button", controlNumber + 8), $"Ch {channel} Solo", "solo", channel);
        }

        Assign(bank, "solo", "Clear Solo", "clear-solo");
        Assign(bank, "send-all", "Main Mute", "master-mute");
    }

    private static void ConfigureMonitorsBank(ControlBank bank)
    {
        Assign(bank, "fader-01", "Main LR", "master");

        for (int bus = 1; bus <= 6; bus++)
        {
            Assign(bank, Id("fader", bus + 1), $"Bus {bus} Master", "bus-master", index: bus);
            Assign(bank, Id("button", bus), $"Bus {bus} Mute", "bus-master-mute", index: bus);
        }

        for (int channel = 1; channel <= 8; channel++)
        {
            Assign(bank, Id("knob", channel), $"Ch {channel} Bus 1", "bus", channel, 1);
            Assign(bank, Id("knob", channel + 8), $"Ch {channel} Bus 2", "bus", channel, 2);
            Assign(bank, Id("knob", channel + 16), $"Ch {channel} Bus 3", "bus", channel, 3);
        }

        Assign(bank, "solo", "Clear Solo", "clear-solo");
        Assign(bank, "send-all", "Mute Bus 1", "bus-master-mute", index: 1);
    }

    private static void ConfigureFxBank(ControlBank bank)
    {
        for (int fx = 1; fx <= 4; fx++)
        {
            Assign(bank, Id("fader", fx), $"FX {fx} Return", "fx-return", index: fx);
            Assign(bank, Id("fader", fx + 4), $"FX {fx} Send", "fx-send-master", index: fx);
            Assign(bank, Id("button", fx), $"FX {fx} Return Mute", "fx-return-mute", index: fx);
            Assign(bank, Id("button", fx + 4), $"FX {fx} Send On", "fx-on", 1, fx);
        }

        for (int channel = 1; channel <= 8; channel++)
        {
            Assign(bank, Id("knob", channel), $"Ch {channel} FX 1", "fx", channel, 1);
            Assign(bank, Id("knob", channel + 8), $"Ch {channel} FX 2", "fx", channel, 2);
            Assign(bank, Id("knob", channel + 16), $"Ch {channel} FX 3", "fx", channel, 3);
        }

        Assign(bank, "solo", "Tap Tempo", "tap-tempo");
        Assign(bank, "send-all", "FX Mute Group", "mute-group", index: 4);
    }

    private static void ConfigureDynamicsEqBank(ControlBank bank)
    {
        for (int channel = 1; channel <= 8; channel++)
        {
            Assign(bank, Id("fader", channel + 1), $"Ch {channel} Comp Thr", "comp-threshold", channel);
            Assign(bank, Id("knob", channel), $"Ch {channel} EQ Low", "eq-low", channel);
            Assign(bank, Id("knob", channel + 8), $"Ch {channel} EQ LowMid", "eq-lowmid", channel);
            Assign(bank, Id("knob", channel + 16), $"Ch {channel} EQ HighMid", "eq-highmid", channel);
            Assign(bank, Id("button", channel), $"Ch {channel} HPF On", "hpf-on", channel);
            Assign(bank, Id("button", channel + 8), $"Ch {channel} Comp On", "comp-on", channel);
        }

        Assign(bank, "fader-01", "Main LR", "master");
        Assign(bank, "solo", "Clear Solo", "clear-solo");
        Assign(bank, "send-all", "Mute Group 1", "mute-group", index: 1);
    }

    private static void ConfigureUtilitySafetyBank(ControlBank bank)
    {
        Assign(bank, "fader-01", "Main LR", "master");

        for (int group = 1; group <= 4; group++)
        {
            Assign(bank, Id("button", group), $"Mute Group {group}", "mute-group", index: group);
        }

        Assign(bank, "button-05", "Clear Solo", "clear-solo");
        Assign(bank, "button-06", "Tap Tempo", "tap-tempo");
        Assign(bank, "button-07", "Snapshot Prev", "scene-prev");
        Assign(bank, "button-08", "Snapshot Next", "scene-next");
        Assign(bank, "button-09", "Load Snapshot 1", "scene-load", index: 1);
        Assign(bank, "button-10", "Load Snapshot 2", "scene-load", index: 2);
        Assign(bank, "button-11", "Load Snapshot 3", "scene-load", index: 3);
        Assign(bank, "button-12", "Load Snapshot 4", "scene-load", index: 4);
        Assign(bank, "button-13", "Main Mute", "master-mute");
        Assign(bank, "button-14", "Bus 1 Mute", "bus-master-mute", index: 1);
        Assign(bank, "button-15", "Bus 2 Mute", "bus-master-mute", index: 2);
        Assign(bank, "button-16", "FX Mute Group", "mute-group", index: 4);
        Assign(bank, "solo", "Clear Solo", "clear-solo");
        Assign(bank, "send-all", "Main Mute", "master-mute");
    }

    private static void Assign(ControlBank bank, string slotId, string label, string commandKey, int channel = 1, int? index = null)
    {
        ControlSlot? slot = bank.FindSlotById(slotId);

        if (slot is null)
        {
            return;
        }

        slot.SetLabel(label);
        slot.SetMixerBinding(CommandCatalog.CreateBinding(commandKey, channel, index));
    }
}
