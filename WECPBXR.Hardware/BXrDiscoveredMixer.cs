using System.Net;

namespace WECPBXR.Hardware;

public sealed class BXrDiscoveredMixer(
    IPAddress address, 
    IReadOnlyList<string> messages)
{
    public IPAddress Address { get; } = address;

    public IReadOnlyList<string> Messages { get; } = messages;
}
