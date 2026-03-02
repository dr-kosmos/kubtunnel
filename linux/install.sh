#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
INSTALL_DIR="/opt/kubetunnel"
DESKTOP_FILE="$HOME/.local/share/applications/kubetunnel.desktop"
ICON_DIR="$HOME/.local/share/icons/hicolor/256x256/apps"

# Check if publish output exists
PUBLISH_DIR="$SCRIPT_DIR/../src/KubeTunnel/bin/Release/net10.0/linux-x64/publish"
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Publish output not found. Building..."
    dotnet publish "$SCRIPT_DIR/../src/KubeTunnel/KubeTunnel.csproj" \
        -c Release \
        -r linux-x64 \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true
fi

# Install binary
echo "Installing to $INSTALL_DIR (requires sudo)..."
sudo mkdir -p "$INSTALL_DIR"
sudo cp -r "$PUBLISH_DIR"/. "$INSTALL_DIR/"
sudo chmod +x "$INSTALL_DIR/KubeTunnel"

# Install .desktop file
mkdir -p "$(dirname "$DESKTOP_FILE")"
cp "$SCRIPT_DIR/kubetunnel.desktop" "$DESKTOP_FILE"

# Install icon (convert ico to png if possible, otherwise use ico directly)
mkdir -p "$ICON_DIR"
ICO_PATH="$SCRIPT_DIR/../src/KubeTunnel/Assets/kube-tunnel.ico"
if command -v magick &>/dev/null; then
    magick "$ICO_PATH" -resize 256x256 "$ICON_DIR/kubetunnel.png"
elif command -v convert &>/dev/null; then
    convert "$ICO_PATH[0]" -resize 256x256 "$ICON_DIR/kubetunnel.png"
else
    echo "Warning: ImageMagick not found. Install it for icon conversion:"
    echo "  sudo dnf install ImageMagick"
    cp "$ICO_PATH" "$ICON_DIR/kubetunnel.ico"
fi

# Update desktop database
update-desktop-database "$HOME/.local/share/applications/" 2>/dev/null || true
gtk-update-icon-cache "$HOME/.local/share/icons/hicolor/" 2>/dev/null || true

echo "Done! KubeTunnel should now appear in your GNOME application search."
