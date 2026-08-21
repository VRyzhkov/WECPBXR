using WECPBXR18.Core.Configuration;
using WECPBXR18.Core.Models;

namespace WECPBXR18.Core.Mapping;

public sealed class MidiMapEditor
{
    private readonly BankSet _bankSet;
    private readonly MidiMapConfigurationStore _store;
    private readonly MixerCommandCatalog _commandCatalog;

    public MidiMapEditor(
        BankSet bankSet,
        MidiMapConfigurationStore? store = null,
        MixerCommandCatalog? commandCatalog = null)
    {
        _bankSet = bankSet ?? throw new ArgumentNullException(nameof(bankSet));
        _store = store ?? new MidiMapConfigurationStore();
        _commandCatalog = commandCatalog ?? new MixerCommandCatalog();
    }

    public MixerCommandCatalog CommandCatalog => _commandCatalog;

    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        await _store.SaveAsync(_bankSet, path, cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        MidiMapConfiguration configuration = await _store.LoadAsync(path, cancellationToken).ConfigureAwait(false);
        _store.Apply(_bankSet, configuration);
    }

    public void SetSlotLabel(int bankIndex, string slotId, string label)
    {
        GetSlot(bankIndex, slotId).SetLabel(label);
    }

    public void SetMidiBinding(int bankIndex, string slotId, MidiBinding? binding)
    {
        GetSlot(bankIndex, slotId).SetMidiBinding(binding);
    }

    public void SetMixerBinding(int bankIndex, string slotId, MixerBinding? binding)
    {
        GetSlot(bankIndex, slotId).SetMixerBinding(binding);
    }

    public void AssignMixerCommand(int bankIndex, string slotId, string commandKey, int channel, int? index = null)
    {
        SetMixerBinding(bankIndex, slotId, _commandCatalog.CreateBinding(commandKey, channel, index));
    }

    public ControlSlot GetSlot(int bankIndex, string slotId)
    {
        ControlBank bank = GetBank(bankIndex);

        return bank.FindSlotById(slotId)
            ?? throw new ArgumentException($"Slot '{slotId}' was not found in bank {bankIndex + 1}.", nameof(slotId));
    }

    public ControlBank GetBank(int bankIndex)
    {
        if (bankIndex < 0 || bankIndex >= _bankSet.Banks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(bankIndex), "Bank index is outside bank range.");
        }

        return _bankSet.Banks[bankIndex];
    }
}
