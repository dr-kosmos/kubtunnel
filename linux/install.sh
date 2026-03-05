#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REAL_USER="${SUDO_USER:-$USER}"
REAL_HOME=$(eval echo "~$REAL_USER")
INSTALL_DIR="/opt/kubetunnel"
DESKTOP_FILE="$REAL_HOME/.local/share/applications/kubetunnel.desktop"
ICON_DIR="$REAL_HOME/.local/share/icons/hicolor/256x256/apps"

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

# Create kubtunnel group and add current user
if ! getent group kubtunnel > /dev/null 2>&1; then
    echo "Creating 'kubtunnel' group..."
    sudo groupadd kubtunnel
fi
if ! id -nG "$REAL_USER" | grep -qw kubtunnel; then
    echo "Adding $REAL_USER to 'kubtunnel' group..."
    sudo usermod -aG kubtunnel "$REAL_USER"
    echo "Note: You may need to log out and back in for the group to take effect."
fi

# Install hosts helper (manages /etc/hosts entries for DNS mode)
HOSTS_HELPER="$INSTALL_DIR/kubtunnel-hosts"
echo "Installing hosts helper for DNS mode..."
sudo cp "$SCRIPT_DIR/kubtunnel-hosts" "$HOSTS_HELPER"
sudo chown root:root "$HOSTS_HELPER"
sudo chmod 755 "$HOSTS_HELPER"

# Allow kubtunnel group to run the hosts helper without a password
SUDOERS_FILE="/etc/sudoers.d/kubtunnel"
echo "Installing sudoers rule..."
sudo tee "$SUDOERS_FILE" > /dev/null << SUDOERS_EOF
%kubtunnel ALL=(root) NOPASSWD: $HOSTS_HELPER
SUDOERS_EOF
sudo chmod 440 "$SUDOERS_FILE"

# Remove old polkit rule if present (no longer needed)
sudo rm -f /etc/polkit-1/rules.d/50-kubtunnel-resolved.rules

# Update desktop database
update-desktop-database "$REAL_HOME/.local/share/applications/" 2>/dev/null || true
gtk-update-icon-cache "$REAL_HOME/.local/share/icons/hicolor/" 2>/dev/null || true

echo "Done! KubeTunnel should now appear in your GNOME application search."
