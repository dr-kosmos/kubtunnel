# KubeTunnel

A cross-platform desktop app for managing multiple `kubectl port-forward` tunnels with profiles, auto-reconnect, and theme support.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![Avalonia](https://img.shields.io/badge/Avalonia-11.3-8B44AC) ![License](https://img.shields.io/badge/license-MIT-green)

## Features

- **Multi-service tunneling** — Forward multiple services across namespaces simultaneously
- **Profiles** — Save and switch between tunnel configurations for different environments
- **Auto-reconnect** — Automatically re-establishes tunnels when connections drop
- **DNS mode (Linux)** — Access services using their cluster domain names (e.g. `myservice.namespace.svc.cluster.local`)
- **Keyboard-driven workflow** — Search, select, configure, and add services without touching the mouse
- **Themes** — Built-in presets (Default Light/Dark, Nord, Dracula, Solarized Dark)
- **Cross-platform** — Runs on Windows, Linux, and macOS

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- `kubectl` configured and available on PATH
- Active kubeconfig context pointing to a cluster

## Build & Run

```bash
dotnet run --project src/KubeTunnel/KubeTunnel.csproj
```

Or build a release:

```bash
dotnet publish src/KubeTunnel/KubeTunnel.csproj -c Release
```

## Install on Linux (Fedora/GNOME)

Publish a self-contained binary and install as a desktop app:

```bash
dotnet publish src/KubeTunnel/KubeTunnel.csproj \
    -c Release -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

chmod +x linux/install.sh
./linux/install.sh
```

The install script:
- Installs the binary to `/opt/kubetunnel`
- Creates a `.desktop` entry for GNOME application search
- Sets up the `kubtunnel` group and a scoped sudoers rule for DNS mode (see below)

To remove:

```bash
chmod +x linux/uninstall.sh
./linux/uninstall.sh
```

## Usage

1. Services from the current kubectl context are loaded automatically
2. Search and select a service from the available services list
3. Set a local port and press **Add** (or Enter)
4. Click **Run** (or Ctrl+R) to start all tunnels
5. **Save** (Ctrl+S) persists the current profile

> **Note:** Only TCP services are shown. `kubectl port-forward` does not support UDP.

### DNS Mode (Linux)

When DNS mode is enabled, services can be accessed using their full cluster domain names instead of `localhost:<port>`. For example:

```
curl http://myservice.mynamespace.svc.cluster.local:8080/health
```

This works by:
1. Adding `/etc/hosts` entries that resolve service FQDNs to `127.0.0.1`
2. Starting TCP relays when the local port differs from the remote port
3. Cleaning up hosts entries automatically on exit

DNS mode requires the `kubtunnel` group and sudoers rule installed by `linux/install.sh`. No password prompts are needed during normal use.

> DNS mode is currently Linux-only. Windows and macOS support may be added in the future.

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+R | Start tunnels |
| Ctrl+A | Stop tunnels |
| Ctrl+S | Save profile |
| Ctrl+P | Create new profile |
| Enter | Navigate through search > grid > port > local port > add |

## Project Structure

```
src/KubeTunnel/
  Models/          Domain models (Config, AppTheme, ServiceInfo, PortForwardConfig)
  Services/        ConfigService (JSON persistence), TunnelService (kubectl management),
                   DnsRelayService (hosts management + TCP relay)
  ViewModels/      MainWindowViewModel (MVVM)
  Views/           Avalonia XAML views
linux/
  install.sh       Linux installer (binary, desktop entry, sudoers)
  uninstall.sh     Linux uninstaller
  kubtunnel-hosts  Helper script for safe /etc/hosts management
```

## Cleanup

KubeTunnel kills all spawned `kubectl` processes on exit and removes any `/etc/hosts` entries added by DNS mode. This is handled through three layers:

1. Window close event
2. Application shutdown event
3. `ProcessExit` safety net for unexpected termination
