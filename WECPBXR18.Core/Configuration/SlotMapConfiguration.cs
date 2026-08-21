namespace WECPBXR18.Core.Configuration;

public sealed class SlotMapConfiguration
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public MidiBindingConfiguration? Midi { get; set; }

    public MixerBindingConfiguration? Mixer { get; set; }
}
