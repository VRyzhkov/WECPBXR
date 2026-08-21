namespace WECPBXR.Core.Models;

public sealed record MixerBinding(string OscAddress, MixerValueKind ValueKind = MixerValueKind.Level);
