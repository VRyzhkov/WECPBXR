namespace WECPBXR.Core.Models;

public sealed class ControlBank
{
    private readonly List<ControlSlot> _slots;
    private readonly List<NavigationControl> _navigationControls;

    public ControlBank(
        int index,
        string name,
        RgbColor color,
        IEnumerable<ControlSlot> slots,
        IEnumerable<NavigationControl> navigationControls)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Bank index cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Bank name is required.", nameof(name));
        }

        Index = index;
        Name = name;
        Color = color;
        _slots = [.. slots];
        _navigationControls = [.. navigationControls];
    }

    public int Index { get; }

    public string Name { get; private set; }

    public RgbColor Color { get; private set; }

    public IReadOnlyList<ControlSlot> Slots => _slots;

    public IReadOnlyList<NavigationControl> NavigationControls => _navigationControls;

    public ControlSlot? FindSlotById(string id)
    {
        return _slots.FirstOrDefault(slot => string.Equals(slot.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Bank name is required.", nameof(name));
        }

        Name = name;
    }

    public void SetColor(RgbColor color)
    {
        Color = color;
    }
}
