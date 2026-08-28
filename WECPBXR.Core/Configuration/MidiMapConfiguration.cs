namespace WECPBXR.Core.Configuration;

public sealed class MidiMapConfiguration
{
    public int Version { get; set; } = 1;

    public string ControllerProfileId { get; set; } = "wecpbxr-default";

    public List<BankMapConfiguration> Banks { get; set; } = [];
}
