using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Rug.Osc;

namespace WECPBXR18.Hardware;

public sealed class Xr18NetworkScanner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    public async Task<IReadOnlyList<Xr18DiscoveredMixer>> ScanAsync(CancellationToken cancellationToken = default)
    {
        return await ScanAsync(DefaultTimeout, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Xr18DiscoveredMixer>> ScanAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Scan timeout must be positive.");
        }

        Dictionary<IPAddress, List<string>> foundMixers = new();

        foreach (IPAddress localAddress in GetLocalIPv4Addresses())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ScanSubnetAsync(localAddress, timeout, foundMixers, cancellationToken).ConfigureAwait(false);
        }

        return foundMixers
            .OrderBy(pair => ToUInt32(pair.Key))
            .Select(pair => new Xr18DiscoveredMixer(pair.Key, pair.Value))
            .ToArray();
    }

    private static async Task ScanSubnetAsync(
        IPAddress localAddress,
        TimeSpan timeout,
        Dictionary<IPAddress, List<string>> foundMixers,
        CancellationToken cancellationToken)
    {
        using UdpClient udpClient = new(new IPEndPoint(localAddress, 0));

        byte[] request = new OscMessage("/xinfo").ToByteArray();
        IPAddress[] targets = GetSlash24Targets(localAddress).ToArray();

        foreach (IPAddress target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await udpClient.SendAsync(request, request.Length, new IPEndPoint(target, Xr18ConnectionSettings.DefaultOscPort))
                .ConfigureAwait(false);
        }

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (!timeoutCts.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await udpClient.ReceiveAsync(timeoutCts.Token).ConfigureAwait(false);
                string message = TryFormatOscPacket(result.Buffer, result.Buffer.Length, result.RemoteEndPoint);

                if (!foundMixers.TryGetValue(result.RemoteEndPoint.Address, out List<string>? messages))
                {
                    messages = new List<string>();
                    foundMixers.Add(result.RemoteEndPoint.Address, messages);
                }

                messages.Add(message);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }
        }
    }

    private static IEnumerable<IPAddress> GetLocalIPv4Addresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Where(address =>
                address.Address.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(address.Address))
            .Select(address => address.Address)
            .Distinct();
    }

    private static IEnumerable<IPAddress> GetSlash24Targets(IPAddress localAddress)
    {
        byte[] bytes = localAddress.GetAddressBytes();

        for (int host = 1; host <= 254; host++)
        {
            if (bytes[3] == host)
            {
                continue;
            }

            yield return new IPAddress(new[] { bytes[0], bytes[1], bytes[2], (byte)host });
        }
    }

    private static string TryFormatOscPacket(byte[] buffer, int length, IPEndPoint remoteEndPoint)
    {
        try
        {
            OscPacket packet = OscPacket.Read(buffer, length, remoteEndPoint);
            return FormatPacket(packet);
        }
        catch (Exception exception)
        {
            return $"Unparsed UDP response ({length} bytes): {exception.Message}";
        }
    }

    private static string FormatPacket(OscPacket packet)
    {
        if (packet is OscMessage message)
        {
            return FormatMessage(message);
        }

        if (packet is OscBundle bundle)
        {
            return string.Join("; ", bundle.Select(FormatPacket));
        }

        return packet.ToString() ?? packet.GetType().Name;
    }

    private static string FormatMessage(OscMessage message)
    {
        string arguments = string.Join(
            ", ",
            Enumerable.Range(0, message.Count).Select(index => Convert.ToString(message[index], CultureInfo.InvariantCulture)));

        return arguments.Length == 0
            ? message.Address
            : $"{message.Address}: {arguments}";
    }

    private static uint ToUInt32(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }
}
