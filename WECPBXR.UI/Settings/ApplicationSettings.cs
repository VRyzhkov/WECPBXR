namespace WECPBXR.UI.Settings;

public sealed class ApplicationSettings
{
    public XrApplicationSettings XR { get; set; } = new();

    public MidiApplicationSettings MIDI { get; set; } = new();

    public ControllerApplicationSettings Controller { get; set; } = new();

    public MapApplicationSettings Map { get; set; } = new();
}

public sealed class XrApplicationSettings
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

public sealed class ControllerApplicationSettings
{
    public string ProfileId { get; set; } = "wecpbxr-default";

    public int BankCount { get; set; } = 8;
}

public sealed class MapApplicationSettings
{
    public string Path { get; set; } = "midi-map.json";
}
