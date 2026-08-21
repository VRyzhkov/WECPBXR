using System.Net;

namespace WECPBXR18.Hardware;

public sealed class Xr18ConnectionSettings
{
    public const int DefaultOscPort = 10024;
    public const int DefaultXRemoteIntervalSeconds = 5;

    public Xr18ConnectionSettings(string mixerAddress)
        : this(mixerAddress, DefaultOscPort, IPAddress.Any, DefaultOscPort, TimeSpan.FromSeconds(DefaultXRemoteIntervalSeconds))
    {
    }

    public Xr18ConnectionSettings(
        string mixerAddress,
        int mixerPort,
        IPAddress localAddress,
        int localPort,
        TimeSpan xRemoteInterval)
    {
        if (string.IsNullOrWhiteSpace(mixerAddress))
        {
            throw new ArgumentException("Mixer address is required.", nameof(mixerAddress));
        }

        if (mixerPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(mixerPort), "Mixer port must be in range 1-65535.");
        }

        if (localPort is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(localPort), "Local port must be in range 0-65535.");
        }

        if (xRemoteInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(xRemoteInterval), "XRemote interval must be positive.");
        }

        MixerAddress = mixerAddress;
        MixerPort = mixerPort;
        LocalAddress = localAddress;
        LocalPort = localPort;
        XRemoteInterval = xRemoteInterval;
    }

    public string MixerAddress { get; }

    public int MixerPort { get; }

    public IPAddress LocalAddress { get; }

    public int LocalPort { get; }

    public TimeSpan XRemoteInterval { get; }
}
