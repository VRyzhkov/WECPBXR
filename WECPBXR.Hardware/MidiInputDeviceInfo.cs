namespace WECPBXR.Hardware;

public sealed class MidiInputDeviceInfo(
    int index, 
    string name)
{
    public int Index { get; } = index;

    public string Name { get; } = name;
}
