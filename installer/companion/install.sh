#!/bin/sh
# Installiert den Trendo-Begleiter NUR für den aktuellen Benutzer (kein root, keine Systemdateien).
set -e
DIR="$HOME/.local/share/trendo-companion"
mkdir -p "$DIR" "$HOME/.config/systemd/user"
cp "$(dirname "$0")/trendo-companion" "$DIR/" && chmod 755 "$DIR/trendo-companion"
cp "$(dirname "$0")/trendo-companion.service" "$HOME/.config/systemd/user/"
systemctl --user daemon-reload && systemctl --user enable --now trendo-companion.service
echo "Installiert. Kopplungscode: $DIR/trendo-companion pair"
