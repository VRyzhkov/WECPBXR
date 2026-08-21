using WECPBXR18.Core.Models;

namespace WECPBXR18.Core.Mapping;

public static class DefaultControlBankFactory
{
    public const int KnobCount = 24;
    public const int FaderCount = 9;
    public const int TotalHardwareButtonCount = 18;
    public const int AssignableButtonCount = 16;

    public static ControlBank CreateBank(int index = 0)
    {
        List<ControlSlot> slots = new();

        for (int i = 1; i <= KnobCount; i++)
        {
            slots.Add(new ControlSlot(
                Id("knob", i),
                $"Knob {i}",
                ControlKind.Knob,
                new MidiBinding(MidiMessageKind.ControlChange, Channel: 1, Number: i)));
        }

        for (int i = 1; i <= FaderCount; i++)
        {
            slots.Add(new ControlSlot(
                Id("fader", i),
                $"Fader {i}",
                ControlKind.Fader,
                new MidiBinding(MidiMessageKind.ControlChange, Channel: 1, Number: KnobCount + i)));
        }

        for (int i = 1; i <= AssignableButtonCount; i++)
        {
            slots.Add(new ControlSlot(
                Id("button", i),
                $"Button {i}",
                ControlKind.Button,
                new MidiBinding(MidiMessageKind.NoteOn, Channel: 1, Number: KnobCount + FaderCount + i)));
        }

        NavigationControl[] navigationControls =
        [
            new("bank-prev", "Bank Previous", NavigationControlKind.BankPrevious, null),
            new("bank-next", "Bank Next", NavigationControlKind.BankNext, null)
        ];

        return new ControlBank(index, slots, navigationControls);
    }

    private static string Id(string prefix, int number)
    {
        return $"{prefix}-{number:00}";
    }
}
