using WECPBXR18.Core.Models;

namespace WECPBXR18.Core.Configuration;

public sealed class MidiBindingConfiguration
{
    public MidiMessageKind Kind { get; set; }

    public int Channel { get; set; }

    public int Number { get; set; }
}
