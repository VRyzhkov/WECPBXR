using WECPBXR.Core.Models;

namespace WECPBXR.Core.Mapping;

public sealed record MixerCommandDefinition(
    string Key,
    string Description,
    MixerValueKind ValueKind,
    int? MinIndex,
    int? MaxIndex,
    string AddressPattern);
