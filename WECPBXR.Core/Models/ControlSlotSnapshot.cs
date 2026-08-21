namespace WECPBXR.Core.Models;

public sealed record ControlSlotSnapshot(
    string Id,
    string Label,
    ControlKind Kind,
    MidiBinding? MidiBinding,
    MixerBinding? MixerBinding,
    double? ControllerValue,
    double? MixerValue,
    bool IsLocked);
