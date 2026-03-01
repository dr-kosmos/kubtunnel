# DNS Mode Port Forwarding

Route traffic via Kubernetes DNS names (e.g. `redis.cache.svc.cluster.local:6379`) instead of `localhost:12345`.

## How It Works

Each service gets a unique loopback IP (`127.0.0.2`, `127.0.0.3`, ...) and a hosts file entry:

```
127.0.0.2  redis.cache.svc.cluster.local
```

kubectl binds to that IP on the original port:

```
kubectl port-forward --address 127.0.0.2 svc/redis 6379:6379 -n cache
```

Apps connect to the real DNS name and port — no localhost remapping needed.

## Changes

### Small changes (existing files)
- `PortForwardConfig` — add `Mode` enum (Localhost / DNS)
- `Config` / profile serialization — persist mode per profile
- `TunnelService` — branch on mode: add `--address` flag, use remote port as local port
- `MainWindowViewModel` — toggle local port field visibility based on mode
- `MainWindow.axaml` — bind local port visibility, simplify Add flow for DNS mode
- `TextInputDialog` → upgrade to "Create Profile" dialog with mode radio buttons + elevation check

### New files
- `HostsFileService` — read/write hosts entries with `# KubeTunnel` markers, startup cleanup
- `LoopbackService` — IP allocation (`127.0.0.x` counter), loopback alias setup on Linux/macOS (Windows doesn't need it)

## Elevation

- DNS mode requires admin/root to write the hosts file
- Check `Environment.IsPrivilegedProcess` at profile creation time
- If not elevated, warn and prevent DNS profile creation
- Localhost mode works without elevation

## Hosts File Lifecycle

```
App starts  → remove any stale entries from previous crash
Run clicked → write entries to hosts file
Stop/Close  → remove entries from hosts file
Crash       → ProcessExit removes entries
Hard crash  → next launch cleans up
```

## Platform Details

| | Loopback alias needed | Hosts file path |
|---|---|---|
| Linux | `ip addr add 127.0.0.x/8 dev lo` | `/etc/hosts` |
| Windows | No (127.0.0.0/8 already routed) | `C:\Windows\System32\drivers\etc\hosts` |
| macOS | `ifconfig lo0 alias 127.0.0.x` | `/etc/hosts` |

## Estimate

~8-10 files touched, ~400-500 lines of new code.
