using WECPBXR.Core.Models;

namespace WECPBXR.Core.Mapping;

public sealed record ControllerInputChange(
    MidiMessageKind Kind,
    int Channel,
    int Number,
    double Value,
    string RawEvent);
