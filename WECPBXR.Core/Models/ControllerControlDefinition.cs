namespace WECPBXR.Core.Models;

public sealed record ControllerControlDefinition(
    string SlotId,
    string DefaultLabel,
    ControlKind Kind,
    MidiBinding? DefaultMidiBinding,
    double Left,
    double Top,
    double Width,
    double Height);
