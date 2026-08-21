using WECPBXR.Core.Models;

namespace WECPBXR.Core.Configuration;

public sealed class MixerBindingConfiguration
{
    public string OscAddress { get; set; } = string.Empty;

    public MixerValueKind ValueKind { get; set; } = MixerValueKind.Level;
}
