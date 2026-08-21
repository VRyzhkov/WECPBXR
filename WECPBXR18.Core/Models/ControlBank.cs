namespace WECPBXR18.Core.Models;

public sealed class ControlBank
{
    private readonly List<ControlSlot> _slots;
    private readonly List<NavigationControl> _navigationControls;

    public ControlBank(
        int index,
        IEnumerable<ControlSlot> slots,
        IEnumerable<NavigationControl> navigationControls)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Bank index cannot be negative.");
        }

        Index = index;
        _slots = slots.ToList();
        _navigationControls = navigationControls.ToList();
    }

    public int Index { get; }

    public IReadOnlyList<ControlSlot> Slots => _slots;

    public IReadOnlyList<NavigationControl> NavigationControls => _navigationControls;

    public ControlSlot? FindSlotById(string id)
    {
        return _slots.FirstOrDefault(slot => string.Equals(slot.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
