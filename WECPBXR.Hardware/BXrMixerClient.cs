using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Rug.Osc;

namespace WECPBXR.Hardware;

public sealed class BXrMixerClient(
    BXrConnectionSettings settings) : IAsyncDisposable, IDisposable
{
    private readonly BXrConnectionSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly object _sendLock = new();

    private UdpClient? _udpClient;
    private TaskCompletionSource<bool>? _connectionConfirmed;
    private CancellationTokenSource? _lifetimeCts;
    private Task? _receiveTask;
    private Task? _xRemoteTask;
    private bool _disposed;

    public event EventHandler<BXrOscMessageReceivedEventArgs>? MessageReceived;

    public bool IsStarted => _lifetimeCts is not null;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (IsStarted)
        {
            return;
        }

        IPAddress mixerAddress = await ResolveMixerAddressAsync(_settings.MixerAddress, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            // XR replies and /xremote updates target the request's source IP/port.
            // Sending and receiving must therefore share the same UDP socket.
            _udpClient = new UdpClient(new IPEndPoint(_settings.LocalAddress, _settings.LocalPort));
            _udpClient.Connect(mixerAddress, _settings.MixerPort);
            _connectionConfirmed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _lifetimeCts = new CancellationTokenSource();
            CancellationToken lifetimeToken = _lifetimeCts.Token;
            _receiveTask = Task.Run(() => ReceiveLoopAsync(lifetimeToken), CancellationToken.None);
            _xRemoteTask = Task.Run(() => XRemoteLoop(lifetimeToken), CancellationToken.None);

            SendXRemote();
            SendMessage(new OscMessage("/xinfo"));
            await _connectionConfirmed.Task.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            await StopAsync().ConfigureAwait(false);
            throw new TimeoutException($"No OSC response from XR mixer at {_settings.MixerAddress}:{_settings.MixerPort}. Check the address, network and firewall.");
        }
        catch
        {
            await StopAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync()
    {
        _lifetimeCts?.Cancel();
        _udpClient?.Dispose();

        await WaitForBackgroundTasksAsync().ConfigureAwait(false);

        _lifetimeCts?.Dispose();

        _udpClient = null;
        _connectionConfirmed = null;
        _lifetimeCts = null;
        _receiveTask = null;
        _xRemoteTask = null;
    }

    public Task SetChannelMuteAsync(int channel, bool muted, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateChannel(channel);

        string address = FormattableString.Invariant($"/ch/{channel:00}/mix/on");
        int enabledValue = muted ? 0 : 1;

        SendMessage(new OscMessage(address, enabledValue));

        return Task.CompletedTask;
    }

    public Task MuteChannelAsync(int channel, CancellationToken cancellationToken = default)
    {
        return SetChannelMuteAsync(channel, muted: true, cancellationToken);
    }

    public Task UnmuteChannelAsync(int channel, CancellationToken cancellationToken = default)
    {
        return SetChannelMuteAsync(channel, muted: false, cancellationToken);
    }

    public Task SendOscValueAsync(
        string oscAddress,
        double value,
        bool sendInteger = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(oscAddress))
        {
            throw new ArgumentException("OSC address is required.", nameof(oscAddress));
        }

        // Box the integer before the conditional can promote it to a float.
        object oscValue = sendInteger ? (object)(int)Math.Round(value) : (float)value;
        SendMessage(new OscMessage(oscAddress, oscValue));

        return Task.CompletedTask;
    }

    public Task RequestOscValueAsync(string oscAddress, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(oscAddress))
        {
            throw new ArgumentException("OSC address is required.", nameof(oscAddress));
        }

        SendMessage(new OscMessage(oscAddress));

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopAsync().GetAwaiter().GetResult();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static async Task<IPAddress> ResolveMixerAddressAsync(string hostNameOrAddress, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(hostNameOrAddress, out IPAddress? parsedAddress))
        {
            return parsedAddress;
        }

        IPAddress[] addresses = await Dns.GetHostAddressesAsync(hostNameOrAddress, cancellationToken)
            .ConfigureAwait(false);

        return addresses.FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault()
            ?? throw new InvalidOperationException($"Cannot resolve mixer address '{hostNameOrAddress}'.");
    }

    private static void ValidateChannel(int channel)
    {
        if (channel is < 1 or > 18)
        {
            throw new ArgumentOutOfRangeException(nameof(channel), "XR channel must be in range 1-18.");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                UdpClient client = _udpClient ?? throw new ObjectDisposedException(nameof(UdpClient));
                UdpReceiveResult result = await client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                OscPacket packet = OscPacket.Read(result.Buffer, result.Buffer.Length, result.RemoteEndPoint);
                PrintPacket(packet);
                RaiseMessageEvents(packet);
            }
            catch (Exception exception) when (cancellationToken.IsCancellationRequested || IsExpectedShutdownException(exception))
            {
                return;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"XR OSC receive error: {exception.Message}");
            }
        }
    }

    private async Task XRemoteLoop(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(_settings.XRemoteInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
                SendXRemote();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"XR /xremote send error: {exception.Message}");
            }
        }
    }

    private void SendXRemote()
    {
        SendMessage(new OscMessage("/xremote"));
    }

    private void SendMessage(OscMessage message)
    {
        ThrowIfDisposed();

        UdpClient client = _udpClient ?? throw new InvalidOperationException("XR mixer client is not started.");

        lock (_sendLock)
        {
            byte[] bytes = message.ToByteArray();
            client.Send(bytes, bytes.Length);
        }
    }

    private static void PrintPacket(OscPacket packet)
    {
        if (packet is OscMessage message)
        {
            Console.WriteLine(FormatMessage(message));
            return;
        }

        if (packet is OscBundle bundle)
        {
            foreach (OscPacket childPacket in bundle)
            {
                PrintPacket(childPacket);
            }
        }
    }

    private void RaiseMessageEvents(OscPacket packet)
    {
        if (packet is OscMessage message)
        {
            if (message.Address == "/xinfo" && message.Count > 0)
            {
                _connectionConfirmed?.TrySetResult(true);
            }

            MessageReceived?.Invoke(this, new BXrOscMessageReceivedEventArgs(message));
            return;
        }

        if (packet is OscBundle bundle)
        {
            foreach (OscPacket childPacket in bundle)
            {
                RaiseMessageEvents(childPacket);
            }
        }
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

    private async Task WaitForBackgroundTasksAsync()
    {
        Task[] tasks = [.. new[] { _receiveTask, _xRemoteTask }
            .Where(task => task is not null)
            .Cast<Task>()];

        if (tasks.Length == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsExpectedShutdownException(exception))
        {
        }
    }

    private static bool IsExpectedShutdownException(Exception exception)
    {
        return exception is ObjectDisposedException
            or OperationCanceledException
            or ThreadInterruptedException
            or System.Net.Sockets.SocketException;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
