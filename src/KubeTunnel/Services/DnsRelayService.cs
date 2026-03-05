using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace KubeTunnel.Services;

public class DnsRelayService : IDisposable
{
    private const string HostsHelper = "/opt/kubetunnel/kubtunnel-hosts";
    private static readonly IPAddress ListenAddress = IPAddress.Loopback;

    private CancellationTokenSource? _cts;
    private readonly List<TcpListener> _tcpRelays = [];
    private readonly List<Task> _tcpRelayTasks = [];

    /// <param name="domains">List of FQDN domains to add to /etc/hosts</param>
    /// <param name="portMap">remotePort → localPort (for TCP relay)</param>
    public void Start(List<string> domains, Dictionary<int, int> portMap)
    {
        _cts = new CancellationTokenSource();

        // Start TCP relays: listen on remotePort, forward to localPort.
        // Skip when ports match — kubectl already listens on the right port.
        foreach (var (remotePort, localPort) in portMap)
        {
            if (remotePort == localPort) continue;
            try
            {
                var tcpListener = new TcpListener(ListenAddress, remotePort);
                tcpListener.Start();
                _tcpRelays.Add(tcpListener);
                _tcpRelayTasks.Add(Task.Run(() => TcpRelayLoop(tcpListener, localPort, _cts.Token)));
            }
            catch (SocketException ex)
            {
                StopTcpRelays();
                _cts.Dispose();
                _cts = null;
                throw new InvalidOperationException(
                    $"Cannot bind TCP relay to {ListenAddress}:{remotePort} — port is already in use.", ex);
            }
        }

        AddHostsEntries(domains);
    }

    public void Stop()
    {
        RemoveHostsEntries();

        _cts?.Cancel();
        StopTcpRelays();
        _cts?.Dispose();
        _cts = null;
    }

    private void StopTcpRelays()
    {
        foreach (var relay in _tcpRelays)
        {
            try { relay.Stop(); } catch { /* ignored */ }
        }

        try { Task.WaitAll(_tcpRelayTasks.ToArray(), TimeSpan.FromSeconds(2)); } catch { /* ignored */ }

        _tcpRelays.Clear();
        _tcpRelayTasks.Clear();
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private static async Task TcpRelayLoop(TcpListener listener, int localPort, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => RelayTcpConnection(client, localPort, ct), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch { /* ignore individual accept failures */ }
        }
    }

    private static async Task RelayTcpConnection(TcpClient client, int localPort, CancellationToken ct)
    {
        try
        {
            using var upstream = new TcpClient();
            await upstream.ConnectAsync(ListenAddress, localPort, ct);

            using (client)
            {
                var clientStream = client.GetStream();
                var upstreamStream = upstream.GetStream();

                await Task.WhenAny(
                    clientStream.CopyToAsync(upstreamStream, ct),
                    upstreamStream.CopyToAsync(clientStream, ct));
            }
        }
        catch { /* connection closed or cancelled */ }
    }

    private static void AddHostsEntries(List<string> domains)
    {
        var args = "add " + string.Join(" ", domains);
        RunProcess("sudo", $"{HostsHelper} {args}");
    }

    public static void RemoveHostsEntries()
    {
        RunProcess("sudo", $"{HostsHelper} remove");
    }

    private static void RunProcess(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(5000);
        }
        catch
        {
            // ignored
        }
    }
}
