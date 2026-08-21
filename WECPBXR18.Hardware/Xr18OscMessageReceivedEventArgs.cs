using Rug.Osc;

namespace WECPBXR18.Hardware;

public sealed class Xr18OscMessageReceivedEventArgs : EventArgs
{
    public Xr18OscMessageReceivedEventArgs(OscMessage message)
    {
        Message = message;
    }

    public OscMessage Message { get; }
}
