using WECPBXR18.Core.Models;

namespace WECPBXR18.Core.Configuration;

public sealed class MixerBindingConfiguration
{
    public string OscAddress { get; set; } = string.Empty;

    public MixerValueKind ValueKind { get; set; } = MixerValueKind.Level;
}
