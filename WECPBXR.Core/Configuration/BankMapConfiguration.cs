namespace WECPBXR.Core.Configuration;

public sealed class BankMapConfiguration
{
    public int Index { get; set; }

    public string Name { get; set; } = string.Empty;

    public RgbColorConfiguration Color { get; set; } = new();

    public List<SlotMapConfiguration> Slots { get; set; } = [];
}
