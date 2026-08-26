namespace WECPBXR.Hardware;

public sealed class MidiInputDeviceInfo(
    int index,
    string name,
    int client = -1,
    int port = -1)
{
    public int Index { get; } = index;

    public string Name { get; } = name;

    internal int Client { get; } = client;

    internal int Port { get; } = port;
}
