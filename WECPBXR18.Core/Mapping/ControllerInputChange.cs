using WECPBXR18.Core.Models;

namespace WECPBXR18.Core.Mapping;

public sealed record ControllerInputChange(
    MidiMessageKind Kind,
    int Channel,
    int Number,
    double Value,
    string RawEvent);
