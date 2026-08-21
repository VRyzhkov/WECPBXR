using WECPBXR.Core.Models;

namespace WECPBXR.Core.Mapping;

public sealed class MappingEngine(
    BankSet bankSet, 
    MappingEngineOptions? options = null)
{
    private readonly BankSet _bankSet = bankSet ?? throw new ArgumentNullException(nameof(bankSet));
    private readonly MappingEngineOptions _options = options ?? new MappingEngineOptions();

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
        MixerOutputCommand? mixerCommand = CreateMixerOutputCommand(slot, change);
        UpdateTakeoverState(slot);
        ControlSlotSnapshot snapshot = slot.Snapshot();
        SlotStateChanged?.Invoke(this, new SlotStateChangedEventArgs(snapshot, MappingUpdateKind.Controller));

        string? message = CreateControllerMessage(slot, change, mixerCommand);

        return MappingResult.Mapped(snapshot, message, mixerCommand);
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
        return [.. CurrentBank.Slots.Select(slot => slot.Snapshot())];
    }

    private void UpdateTakeoverState(ControlSlot slot)
    {
        if (slot.MixerBinding?.ValueKind == MixerValueKind.Toggle)
        {
            slot.SetLocked(slot.MixerValue is null);
            return;
        }

        if (slot.ControllerValue is null || slot.MixerValue is null)
        {
            slot.SetLocked(true);
            return;
        }

        bool isLocked = Math.Abs(slot.ControllerValue.Value - slot.MixerValue.Value) > _options.SoftTakeoverThreshold;
        slot.SetLocked(isLocked);
    }

    private static MixerOutputCommand? CreateMixerOutputCommand(ControlSlot slot, ControllerInputChange change)
    {
        if (slot.MixerBinding is null)
        {
            return null;
        }

        return slot.MixerBinding.ValueKind switch
        {
            MixerValueKind.Toggle => CreateToggleByPressCommand(slot, change),
            _ => CreateContinuousCommand(slot, change)
        };
    }

    private static string? CreateControllerMessage(
        ControlSlot slot,
        ControllerInputChange change,
        MixerOutputCommand? mixerCommand)
    {
        if (mixerCommand is not null)
        {
            return null;
        }

        if (slot.MixerBinding is null)
        {
            return "OSC blocked: slot has no mixer binding.";
        }

        if (slot.MixerBinding.ValueKind == MixerValueKind.Toggle)
        {
            if (!IsPress(change))
            {
                return "Toggle release ignored.";
            }

            return slot.MixerValue is null
                ? "Toggle blocked: mixer value is unknown."
                : "Toggle blocked.";
        }

        if (slot.MixerValue is null)
        {
            return "OSC blocked: waiting for mixer value for soft takeover.";
        }

        if (slot.IsLocked)
        {
            return FormattableString.Invariant(
                $"OSC blocked by soft takeover: controller={slot.ControllerValue:0.000} mixer={slot.MixerValue:0.000}.");
        }

        return "OSC blocked.";
    }

    private static MixerOutputCommand? CreateContinuousCommand(ControlSlot slot, ControllerInputChange change)
    {
        if (slot.IsLocked || slot.MixerBinding is null)
        {
            return null;
        }

        double value = slot.ControllerValue ?? change.Value;
        slot.SetMixerValue(value);

        return new MixerOutputCommand(slot.MixerBinding.OscAddress, value, slot.MixerBinding.ValueKind);
    }

    private static MixerOutputCommand? CreateToggleByPressCommand(ControlSlot slot, ControllerInputChange change)
    {
        if (!IsPress(change) || slot.MixerValue is null || slot.MixerBinding is null)
        {
            return null;
        }

        double nextValue = slot.MixerValue.Value >= 0.5 ? 0.0 : 1.0;
        slot.SetMixerValue(nextValue);

        return new MixerOutputCommand(slot.MixerBinding.OscAddress, nextValue, slot.MixerBinding.ValueKind);
    }

    private static bool IsPress(ControllerInputChange change)
    {
        return change.Kind switch
        {
            MidiMessageKind.NoteOn => change.Value > 0,
            MidiMessageKind.ControlChange => change.Value >= 0.5,
            _ => false
        };
    }
}
