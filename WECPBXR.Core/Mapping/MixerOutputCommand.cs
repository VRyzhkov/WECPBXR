using WECPBXR.Core.Models;

namespace WECPBXR.Core.Mapping;

public sealed record MixerOutputCommand(string OscAddress, double Value, MixerValueKind ValueKind);
