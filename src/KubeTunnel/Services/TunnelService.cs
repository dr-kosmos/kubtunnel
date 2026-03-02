using System.Collections.Concurrent;
using System.Diagnostics;
using KubeTunnel.Models;

namespace KubeTunnel.Services;

public enum TunnelStatus
{
    Idle,
    Connecting,
    Active,
    Reconnecting,
    Failed
}

public class TunnelService
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();
    private readonly ConcurrentDictionary<string, TunnelStatus> _statuses = new();
    private readonly List<Task> _tasks = [];
    private CancellationTokenSource? _globalCts;

    public bool IsRunning => _globalCts is { IsCancellationRequested: false };

    public event Action<string, string>? LogMessage;       // (timestamp, message)
    public event Action<string, TunnelStatus>? StatusChanged; // (serviceKey, status)

    public TunnelStatus GetStatus(string serviceKey) =>
        _statuses.GetValueOrDefault(serviceKey, TunnelStatus.Idle);

    private string ServiceKey(PortForwardConfig c) => $"{c.Namespace}/{c.Service}";

    private void Log(string message)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss");
        LogMessage?.Invoke(ts, message);
    }

    private void SetStatus(string key, TunnelStatus status)
    {
        _statuses[key] = status;
        StatusChanged?.Invoke(key, status);
    }

    public void StartAll(IEnumerable<PortForwardConfig> configs)
    {
        StopAll();

        _globalCts = new CancellationTokenSource();
        var globalToken = _globalCts.Token;

        foreach (var config in configs)
        {
            var key = ServiceKey(config);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(globalToken);
            _cancellationTokens[key] = cts;
            SetStatus(key, TunnelStatus.Connecting);

            var task = Task.Run(async () => await RunTunnel(config, key, cts.Token), cts.Token);
            _tasks.Add(task);
        }
    }

    public void StopAll()
    {
        _globalCts?.Cancel();

        foreach (var kvp in _cancellationTokens)
        {
            kvp.Value.Cancel();
        }

        try
        {
            Task.WaitAll(_tasks.ToArray(), TimeSpan.FromSeconds(5));
        }
        catch
        {
            // ignored — tasks may be cancelled
        }

        foreach (var key in _statuses.Keys.ToList())
        {
            SetStatus(key, TunnelStatus.Idle);
        }

        _cancellationTokens.Clear();
        _tasks.Clear();
        _globalCts?.Dispose();
        _globalCts = null;
    }

    private async Task RunTunnel(PortForwardConfig config, string key, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var (result, process) = StartKubectlPortForward(config);

            switch (result)
            {
                case PortForwardResult.Success:
                    SetStatus(key, TunnelStatus.Active);
                    Log($"Forwarding: {config.Service}@{config.LocalPort}...");

                    if (process != null)
                    {
                        var startTime = DateTime.UtcNow;
                        var stderr = await WaitForExitOrCancellation(process, ct);

                        if (!ct.IsCancellationRequested)
                        {
                            SetStatus(key, TunnelStatus.Reconnecting);

                            if (!string.IsNullOrWhiteSpace(stderr))
                                Log($"Error from {config.Service}: {stderr.Trim()}");

                            Log($"Connection lost: {config.Service}. Reconnecting...");

                            // If the process exited quickly, wait before retrying to avoid a tight loop
                            var elapsed = DateTime.UtcNow - startTime;
                            if (elapsed.TotalSeconds < 5)
                            {
                                try { await Task.Delay(5000, ct); }
                                catch (OperationCanceledException) { return; }
                            }
                        }
                    }

                    break;

                case PortForwardResult.RetryableFailure:
                    SetStatus(key, TunnelStatus.Reconnecting);
                    Log($"Failed to forward: {config.Service} (retryable). Retrying in 10s...");

                    try
                    {
                        await Task.Delay(10000, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    break;

                case PortForwardResult.PermanentFailure:
                    SetStatus(key, TunnelStatus.Failed);
                    Log($"Failed to forward: {config.Service} (permanent error)");
                    return;
            }
        }
    }

    private (PortForwardResult, Process?) StartKubectlPortForward(PortForwardConfig config)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "kubectl",
            Arguments =
                $"port-forward svc/{config.Service} {config.LocalPort}:{config.RemotePort} -n {config.Namespace}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        try
        {
            if (process.Start())
                return (PortForwardResult.Success, process);

            return (PortForwardResult.PermanentFailure, null);
        }
        catch
        {
            return (PortForwardResult.PermanentFailure, null);
        }
    }

    private static async Task<string> WaitForExitOrCancellation(Process process, CancellationToken ct)
    {
        // Drain stdout and capture stderr to prevent buffer deadlock and capture diagnostics
        var stderrTask = process.StandardError.ReadToEndAsync();
        _ = process.StandardOutput.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try { process.Kill(); }
                catch { /* ignored */ }
            }

            return string.Empty;
        }

        try { return await stderrTask; }
        catch { return string.Empty; }
    }

    private enum PortForwardResult
    {
        Success,
        RetryableFailure,
        PermanentFailure
    }
}
