#!/usr/bin/env bash
set -euo pipefail

echo "Removing KubeTunnel..."

sudo rm -rf /opt/kubetunnel
rm -f "$HOME/.local/share/applications/kubetunnel.desktop"
rm -f "$HOME/.local/share/icons/hicolor/256x256/apps/kubetunnel.png"
rm -f "$HOME/.local/share/icons/hicolor/256x256/apps/kubetunnel.ico"

update-desktop-database "$HOME/.local/share/applications/" 2>/dev/null || true
gtk-update-icon-cache "$HOME/.local/share/icons/hicolor/" 2>/dev/null || true

echo "Done."
