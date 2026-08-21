using WECPBXR.Core.Models;

namespace WECPBXR.Core.Mapping;

public sealed record MappingResult(
    bool IsMapped,
    ControlSlotSnapshot? Slot,
    string? Message,
    MixerOutputCommand? MixerCommand = null)
{
    public static MappingResult Unmapped(string message)
    {
        return new MappingResult(false, null, message);
    }

    public static MappingResult Mapped(ControlSlotSnapshot slot, string? message = null, MixerOutputCommand? mixerCommand = null)
    {
        return new MappingResult(true, slot, message, mixerCommand);
    }
}
