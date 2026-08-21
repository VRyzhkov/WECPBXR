using WECPBXR18.Core.Models;

namespace WECPBXR18.Core.Mapping;

public sealed record MixerOutputCommand(string OscAddress, double Value, MixerValueKind ValueKind);
