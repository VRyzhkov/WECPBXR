using WECPBXR.Core.Models;

namespace WECPBXR.Core.Mapping;

public sealed class BankChangedEventArgs(
    ControlBank previousBank, 
    ControlBank currentBank) : EventArgs
{
    public ControlBank PreviousBank { get; } = previousBank;

    public ControlBank CurrentBank { get; } = currentBank;
}
