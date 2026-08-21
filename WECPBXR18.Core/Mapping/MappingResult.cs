using WECPBXR18.Core.Models;

namespace WECPBXR18.Core.Mapping;

public sealed record MappingResult(bool IsMapped, ControlSlotSnapshot? Slot, string? Message)
{
    public static MappingResult Unmapped(string message)
    {
        return new MappingResult(false, null, message);
    }

    public static MappingResult Mapped(ControlSlotSnapshot slot, string? message = null)
    {
        return new MappingResult(true, slot, message);
    }
}
