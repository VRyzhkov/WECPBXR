namespace WECPBXR.Core.Models;

public sealed record NavigationControl(string Id, string Label, NavigationControlKind Kind, MidiBinding? MidiBinding);
