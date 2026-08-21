namespace WECPBXR18.Core.Models;

public sealed record MidiBinding(MidiMessageKind Kind, int Channel, int Number)
{
    public bool Matches(MidiMessageKind kind, int channel, int number)
    {
        bool sameControl = Channel == channel && Number == number;
        bool sameKind = Kind == kind || Kind == MidiMessageKind.NoteOn && kind == MidiMessageKind.NoteOff;

        return sameControl && sameKind;
    }
}
