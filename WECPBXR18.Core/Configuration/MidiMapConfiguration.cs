namespace WECPBXR18.Core.Configuration;

public sealed class MidiMapConfiguration
{
    public int Version { get; set; } = 1;

    public List<BankMapConfiguration> Banks { get; set; } = [];
}
