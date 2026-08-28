namespace WECPBXR.Core.Models;

public sealed class ControllerProfile
{
    private readonly Dictionary<string, ControllerControlDefinition> _controlsBySlotId;

    public ControllerProfile(
        string id,
        string name,
        double surfaceWidth,
        double surfaceHeight,
        IEnumerable<ControllerControlDefinition> controls)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Controller profile id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Controller profile name is required.", nameof(name));
        }

        Id = id;
        Name = name;
        SurfaceWidth = surfaceWidth;
        SurfaceHeight = surfaceHeight;
        Controls = [.. controls];
        _controlsBySlotId = Controls.ToDictionary(control => control.SlotId, StringComparer.OrdinalIgnoreCase);
    }

    public string Id { get; }

    public string Name { get; }

    public double SurfaceWidth { get; }

    public double SurfaceHeight { get; }

    public IReadOnlyList<ControllerControlDefinition> Controls { get; }

    public ControllerControlDefinition? FindControl(string slotId)
    {
        return _controlsBySlotId.TryGetValue(slotId, out ControllerControlDefinition? control)
            ? control
            : null;
    }
}
