using WECPBXR18.Core.Models;

namespace WECPBXR18.Core.Mapping;

public sealed class MappingEngine
{
    private readonly BankSet _bankSet;
    private readonly MappingEngineOptions _options;

    public MappingEngine(BankSet bankSet, MappingEngineOptions? options = null)
    {
        _bankSet = bankSet ?? throw new ArgumentNullException(nameof(bankSet));
        _options = options ?? new MappingEngineOptions();
    }

    public event EventHandler<SlotStateChangedEventArgs>? SlotStateChanged;

    public event EventHandler<BankChangedEventArgs>? BankChanged
    {
        add => _bankSet.BankChanged += value;
        remove => _bankSet.BankChanged -= value;
    }

    public IReadOnlyList<ControlBank> Banks => _bankSet.Banks;

    public ControlBank CurrentBank => _bankSet.CurrentBank;

    public string CurrentBankName => CurrentBank.Name;

    public RgbColor CurrentBankColor => CurrentBank.Color;

    public ControlBank NextBank()
    {
        return _bankSet.NextBank();
    }

    public ControlBank PreviousBank()
    {
        return _bankSet.PreviousBank();
    }

    public ControlBank SelectBank(int index)
    {
        return _bankSet.SelectBank(index);
    }

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
