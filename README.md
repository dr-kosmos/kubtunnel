# KubeTunnel

A cross-platform desktop app for managing multiple `kubectl port-forward` tunnels with profiles, auto-reconnect, and theme support.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![Avalonia](https://img.shields.io/badge/Avalonia-11.3-8B44AC) ![License](https://img.shields.io/badge/license-MIT-green)

## Features

- **Multi-service tunneling** — Forward multiple services across namespaces simultaneously
- **Profiles** — Save and switch between tunnel configurations for different environments
- **Auto-reconnect** — Automatically re-establishes tunnels when connections drop
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

KubeTunnel will appear in the GNOME application search. To remove:

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
  Services/        ConfigService (JSON persistence), TunnelService (kubectl management)
  ViewModels/      MainWindowViewModel (MVVM)
  Views/           Avalonia XAML views
```

## Cleanup

KubeTunnel kills all spawned `kubectl` processes on exit. This is handled through three layers:

1. Window close event
2. Application shutdown event
3. `ProcessExit` safety net for unexpected termination
