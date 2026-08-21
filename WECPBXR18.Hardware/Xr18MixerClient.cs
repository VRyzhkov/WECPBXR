using System.Globalization;
using System.Net;
using Rug.Osc;

namespace WECPBXR18.Hardware;

public sealed class Xr18MixerClient : IAsyncDisposable, IDisposable
{
    private readonly Xr18ConnectionSettings _settings;
    private readonly object _sendLock = new();

    private OscSender? _sender;
    private OscReceiver? _receiver;
    private CancellationTokenSource? _lifetimeCts;
    private Task? _receiveTask;
    private Task? _xRemoteTask;
    private bool _disposed;

    public Xr18MixerClient(Xr18ConnectionSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public event EventHandler<Xr18OscMessageReceivedEventArgs>? MessageReceived;

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

        _sender = new OscSender(mixerAddress, _settings.MixerPort);
        _receiver = new OscReceiver(_settings.LocalAddress, _settings.LocalPort);

        _sender.Connect();
        _receiver.Connect();

        _lifetimeCts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoop(_lifetimeCts.Token), CancellationToken.None);
        _xRemoteTask = Task.Run(() => XRemoteLoop(_lifetimeCts.Token), CancellationToken.None);

        SendXRemote();
    }

    public async Task StopAsync()
    {
        if (_lifetimeCts is null)
        {
            return;
        }

        _lifetimeCts.Cancel();
        _receiver?.Close();

        await WaitForBackgroundTasksAsync().ConfigureAwait(false);

        _sender?.Close();
        _sender?.Dispose();
        _receiver?.Dispose();
        _lifetimeCts.Dispose();

        _sender = null;
        _receiver = null;
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
            throw new ArgumentOutOfRangeException(nameof(channel), "XR18 channel must be in range 1-18.");
        }
    }

    private void ReceiveLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                OscPacket packet = _receiver?.Receive() ?? throw new ObjectDisposedException(nameof(OscReceiver));
                PrintPacket(packet);
                RaiseMessageEvents(packet);
            }
            catch (Exception exception) when (cancellationToken.IsCancellationRequested || IsExpectedShutdownException(exception))
            {
                return;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"XR18 OSC receive error: {exception.Message}");
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
                Console.Error.WriteLine($"XR18 /xremote send error: {exception.Message}");
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

        OscSender sender = _sender ?? throw new InvalidOperationException("XR18 mixer client is not started.");

        lock (_sendLock)
        {
            sender.Send(message);
            sender.WaitForAllMessagesToComplete();
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
            MessageReceived?.Invoke(this, new Xr18OscMessageReceivedEventArgs(message));
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
        Task[] tasks = new[] { _receiveTask, _xRemoteTask }
            .Where(task => task is not null)
            .Cast<Task>()
            .ToArray();

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
