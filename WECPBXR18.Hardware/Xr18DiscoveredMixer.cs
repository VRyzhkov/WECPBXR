using System.Net;

namespace WECPBXR18.Hardware;

public sealed class Xr18DiscoveredMixer
{
    public Xr18DiscoveredMixer(IPAddress address, IReadOnlyList<string> messages)
    {
        Address = address;
        Messages = messages;
    }

    public IPAddress Address { get; }

    public IReadOnlyList<string> Messages { get; }
}
