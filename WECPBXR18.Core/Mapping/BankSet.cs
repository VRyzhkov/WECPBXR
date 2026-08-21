using WECPBXR18.Core.Models;

namespace WECPBXR18.Core.Mapping;

public sealed class BankSet
{
    private readonly List<ControlBank> _banks;

    public BankSet(IEnumerable<ControlBank> banks, int currentBankIndex = 0)
    {
        _banks = banks.ToList();

        if (_banks.Count == 0)
        {
            throw new ArgumentException("At least one bank is required.", nameof(banks));
        }

        if (currentBankIndex < 0 || currentBankIndex >= _banks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(currentBankIndex), "Current bank index is outside bank range.");
        }

        CurrentBankIndex = currentBankIndex;
    }

    public event EventHandler<BankChangedEventArgs>? BankChanged;

    public IReadOnlyList<ControlBank> Banks => _banks;

    public int CurrentBankIndex { get; private set; }

    public ControlBank CurrentBank => _banks[CurrentBankIndex];

    public ControlBank NextBank()
    {
        int nextIndex = (CurrentBankIndex + 1) % _banks.Count;
        return SelectBank(nextIndex);
    }

    public ControlBank PreviousBank()
    {
        int previousIndex = CurrentBankIndex == 0 ? _banks.Count - 1 : CurrentBankIndex - 1;
        return SelectBank(previousIndex);
    }

    public ControlBank SelectBank(int index)
    {
        if (index < 0 || index >= _banks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Bank index is outside bank range.");
        }

        ControlBank previousBank = CurrentBank;

        if (CurrentBankIndex == index)
        {
            return CurrentBank;
        }

        CurrentBankIndex = index;
        ControlBank currentBank = CurrentBank;
        BankChanged?.Invoke(this, new BankChangedEventArgs(previousBank, currentBank));

        return currentBank;
    }
}
