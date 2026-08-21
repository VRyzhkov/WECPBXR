using WECPBXR.Core.Models;

namespace WECPBXR.Core.Configuration;

public sealed class MidiBindingConfiguration
{
    public MidiMessageKind Kind { get; set; }

    public int Channel { get; set; }

    public int Number { get; set; }
}
