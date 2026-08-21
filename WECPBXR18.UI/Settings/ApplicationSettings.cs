namespace WECPBXR18.UI.Settings;

public sealed class ApplicationSettings
{
    public Xr18ApplicationSettings XR18 { get; set; } = new();

    public MidiApplicationSettings MIDI { get; set; } = new();

    public MapApplicationSettings Map { get; set; } = new();
}

public sealed class Xr18ApplicationSettings
{
    public string Address { get; set; } = "192.168.1.100";

    public bool AutoConnect { get; set; }

    public bool PullOnConnect { get; set; } = true;
}

public sealed class MidiApplicationSettings
{
    public string InputDeviceName { get; set; } = string.Empty;

    public bool AutoConnect { get; set; }
}

public sealed class MapApplicationSettings
{
    public string Path { get; set; } = "midi-map.json";
}
