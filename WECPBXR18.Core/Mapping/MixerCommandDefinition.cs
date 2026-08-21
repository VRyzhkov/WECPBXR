using WECPBXR18.Core.Models;

namespace WECPBXR18.Core.Mapping;

public sealed record MixerCommandDefinition(
    string Key,
    string Description,
    MixerValueKind ValueKind,
    int? MinIndex,
    int? MaxIndex,
    string AddressPattern);
