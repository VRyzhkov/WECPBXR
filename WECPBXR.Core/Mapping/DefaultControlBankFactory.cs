using WECPBXR.Core.Models;

namespace WECPBXR.Core.Mapping;

public static class DefaultControlBankFactory
{
    public const int DefaultBankCount = 7;
    public const int KnobCount = 24;
    public const int FaderCount = 9;
    public const int AssignableButtonCount = 20;
    private const int MatrixButtonCount = 16;

    private static readonly (string Name, RgbColor Color)[] DefaultBanks =
    [
        ("Red", new RgbColor(255, 0, 0)),
        ("Orange", new RgbColor(255, 127, 0)),
        ("Yellow", new RgbColor(255, 255, 0)),
        ("Green", new RgbColor(0, 255, 0)),
        ("Cyan", new RgbColor(0, 255, 255)),
        ("Blue", new RgbColor(0, 0, 255)),
        ("Violet", new RgbColor(139, 0, 255))
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
                new MidiBinding(MidiMessageKind.ControlChange, Channel: 1, Number: i)));
        }

        for (int i = 1; i <= FaderCount; i++)
        {
            slots.Add(new ControlSlot(
                Id("fader", i),
                $"{bankName} Fader {i}",
                ControlKind.Fader,
                new MidiBinding(MidiMessageKind.ControlChange, Channel: 1, Number: KnobCount + i)));
        }

        slots.Add(new ControlSlot(
            "bank-prev",
            "BANK L",
            ControlKind.Button));

        slots.Add(new ControlSlot(
            "bank-next",
            "BANK R",
            ControlKind.Button));

        slots.Add(new ControlSlot(
            "solo",
            "SOLO",
            ControlKind.Button));

        slots.Add(new ControlSlot(
            "send-all",
            "SEND ALL",
            ControlKind.Button));

        for (int i = 1; i <= MatrixButtonCount; i++)
        {
            slots.Add(new ControlSlot(
                Id("button", i),
                $"{bankName} Button {i}",
                ControlKind.Button,
                new MidiBinding(MidiMessageKind.NoteOn, Channel: 1, Number: KnobCount + FaderCount + i)));
        }

        return new ControlBank(index, bankName, color, slots, []);
    }

    private static string Id(string prefix, int number)
    {
        return $"{prefix}-{number:00}";
    }
}
