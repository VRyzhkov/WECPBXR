using WECPBXR18.Core.Models;

namespace WECPBXR18.Core.Mapping;

public sealed class BankChangedEventArgs : EventArgs
{
    public BankChangedEventArgs(ControlBank previousBank, ControlBank currentBank)
    {
        PreviousBank = previousBank;
        CurrentBank = currentBank;
    }

    public ControlBank PreviousBank { get; }

    public ControlBank CurrentBank { get; }
}
