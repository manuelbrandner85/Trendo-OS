#!/bin/bash
# Baut das Trendo-OS-Linux-ISO (Debian 13 + KDE Plasma, live-build). Nur auf einer Build-Maschine ausführen;
# schreibt KEIN Laufwerk. Ergebnis: linux-image/out/trendo-os-p2-amd64.hybrid.iso + SHA256SUMS
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"; REPO="$(dirname "$HERE")"
WORK="${WORK:-/tmp/trendo-lb}"; OUT="$HERE/out"
DOTNET="${DOTNET:-dotnet}"
echo "== 1) Trendo linux-x64 self-contained veröffentlichen"
rm -rf "$HERE/config/includes.chroot/opt/trendo"
"$DOTNET" publish "$REPO/src/Trendo.App/Trendo.App.csproj" -c Release -r linux-x64 --self-contained true -o "$HERE/config/includes.chroot/opt/trendo"
echo "== 2) live-build konfigurieren"
sudo rm -rf "$WORK"; mkdir -p "$WORK"; cp -r "$HERE/config" "$WORK/"; cd "$WORK"
lb config \
  --mode debian --distribution trixie --architectures amd64 \
  --archive-areas "main contrib non-free-firmware" \
  --mirror-bootstrap http://deb.debian.org/debian/ --mirror-chroot-security http://deb.debian.org/debian-security/ --mirror-binary-security http://deb.debian.org/debian-security/ \
  --binary-images iso-hybrid --bootloaders "grub-efi,syslinux" \
  --debian-installer none --memtest none --apt-recommends false \
  --iso-volume "TRENDO_OS_P2" --iso-application "Trendo OS" --iso-publisher "Trendo OS Projekt" \
  --bootappend-live "boot=live components quiet splash locales=de_DE.UTF-8 keyboard-layouts=de timezone=Europe/Berlin persistence persistence-encryption=luks username=trendo hostname=trendo-os"
echo "== 3) Image bauen (dauert)"
sudo lb build 2>&1 | tee "$WORK/build.log"
mkdir -p "$OUT"; cp "$WORK"/*.iso "$OUT/trendo-os-p2-amd64.hybrid.iso"; cp "$WORK/build.log" "$OUT/lb-build.log"
cp "$WORK"/*.packages "$OUT/packages.txt" 2>/dev/null || true
(cd "$OUT" && sha256sum trendo-os-p2-amd64.hybrid.iso > SHA256SUMS)
cat "$OUT/SHA256SUMS"
