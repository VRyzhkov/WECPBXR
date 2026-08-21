namespace WECPBXR18.Hardware;

public sealed class MidiInputDeviceInfo
{
    public MidiInputDeviceInfo(int index, string name)
    {
        Index = index;
        Name = name;
    }

    public int Index { get; }

    public string Name { get; }
}
