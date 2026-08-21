using WECPBXR18.Core.Models;

namespace WECPBXR18.Core.Mapping;

public sealed class MappingEngine
{
    private readonly MappingEngineOptions _options;

    public MappingEngine(ControlBank currentBank, MappingEngineOptions? options = null)
    {
        CurrentBank = currentBank ?? throw new ArgumentNullException(nameof(currentBank));
        _options = options ?? new MappingEngineOptions();
    }

    public event EventHandler<SlotStateChangedEventArgs>? SlotStateChanged;

    public ControlBank CurrentBank { get; }

    public MappingResult HandleControllerChange(ControllerInputChange change)
    {
        ControlSlot? slot = CurrentBank.Slots.FirstOrDefault(slot =>
            slot.MidiBinding?.Matches(change.Kind, change.Channel, change.Number) == true);

        if (slot is null)
        {
            return MappingResult.Unmapped(
                $"Unmapped controller input: {change.Kind} ch={change.Channel} number={change.Number} value={change.Value:0.000}");
        }

        slot.SetControllerValue(change.Value);
        UpdateTakeoverState(slot);
        ControlSlotSnapshot snapshot = slot.Snapshot();
        SlotStateChanged?.Invoke(this, new SlotStateChangedEventArgs(snapshot, MappingUpdateKind.Controller));

        return MappingResult.Mapped(snapshot);
    }

    public MappingResult HandleMixerChange(MixerValueChange change)
    {
        ControlSlot? slot = CurrentBank.Slots.FirstOrDefault(slot =>
            string.Equals(slot.MixerBinding?.OscAddress, change.OscAddress, StringComparison.OrdinalIgnoreCase));

        if (slot is null)
        {
            return MappingResult.Unmapped($"Unmapped mixer value: {change.OscAddress}={change.Value:0.000}");
        }

        slot.SetMixerValue(change.Value);
        UpdateTakeoverState(slot);
        ControlSlotSnapshot snapshot = slot.Snapshot();
        SlotStateChanged?.Invoke(this, new SlotStateChangedEventArgs(snapshot, MappingUpdateKind.Mixer));

        return MappingResult.Mapped(snapshot);
    }

    public IReadOnlyList<ControlSlotSnapshot> GetSlotSnapshots()
    {
        return CurrentBank.Slots.Select(slot => slot.Snapshot()).ToArray();
    }

    private void UpdateTakeoverState(ControlSlot slot)
    {
        if (slot.ControllerValue is null || slot.MixerValue is null)
        {
            slot.SetLocked(true);
            return;
        }

        bool isLocked = Math.Abs(slot.ControllerValue.Value - slot.MixerValue.Value) > _options.SoftTakeoverThreshold;
        slot.SetLocked(isLocked);
    }
}
