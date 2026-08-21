using WECPBXR.Core.Models;

namespace WECPBXR.Core.Mapping;

public sealed class SlotStateChangedEventArgs(
    ControlSlotSnapshot slot, 
    MappingUpdateKind updateKind) : EventArgs
{
    public ControlSlotSnapshot Slot { get; } = slot;

    public MappingUpdateKind UpdateKind { get; } = updateKind;
}
