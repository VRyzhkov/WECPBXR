using WECPBXR18.Core.Models;

namespace WECPBXR18.Core.Mapping;

public sealed class SlotStateChangedEventArgs : EventArgs
{
    public SlotStateChangedEventArgs(ControlSlotSnapshot slot, MappingUpdateKind updateKind)
    {
        Slot = slot;
        UpdateKind = updateKind;
    }

    public ControlSlotSnapshot Slot { get; }

    public MappingUpdateKind UpdateKind { get; }
}
