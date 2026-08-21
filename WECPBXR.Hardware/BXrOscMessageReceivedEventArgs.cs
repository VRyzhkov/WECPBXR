using Rug.Osc;

namespace WECPBXR.Hardware;

public sealed class BXrOscMessageReceivedEventArgs(
    OscMessage message) : EventArgs
{
    public OscMessage Message { get; } = message;
}
