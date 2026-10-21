#!/bin/bash
# Legt auf einem ZIELGERÄT eine LUKS-verschlüsselte Persistenzpartition für Trendo OS an.
# ACHTUNG: ändert Partitionen. Nur mit ausdrücklicher Freigabe von Manuel und nur auf dem externen Ziel-Medium.
# Schutz: verweigert Geräte mit Windows-/BitLocker-Partitionen, interne (nicht-removable) Laufwerke ohne --allow-fixed,
# und verlangt die Eingabe der Seriennummer-Endung zur Bestätigung. Für Tests: Image-Datei statt Gerät angeben.
set -euo pipefail
DEV="${1:?Gerät oder Image-Datei angeben}"; ALLOW_FIXED="${2:-}"
if [[ -b "$DEV" ]]; then
  name=$(basename "$DEV")
  [[ "$(cat /sys/block/$name/removable 2>/dev/null || echo 0)" == "1" || "$ALLOW_FIXED" == "--allow-fixed" ]] || { echo "ABBRUCH: $DEV ist nicht als Wechselmedium markiert."; exit 2; }
  if lsblk -no FSTYPE,PARTTYPE "$DEV" | grep -qiE 'ntfs|bitlocker|e3c9e316|de94bba4'; then echo "ABBRUCH: Windows/BitLocker auf $DEV erkannt."; exit 3; fi
  ser=$(lsblk -dno SERIAL "$DEV" | tail -c 5); read -rp "Zur Bestätigung letzte 4 Zeichen der Seriennummer von $DEV eingeben: " c; [[ "$c" == "$ser" ]] || { echo "ABBRUCH: Bestätigung falsch."; exit 4; }
  TARGET="$DEV"
else
  [[ -f "$DEV" ]] || { echo "Weder Blockgerät noch Datei: $DEV"; exit 1; }
  TARGET=$(sudo losetup --find --show --partscan "$DEV"); trap 'sudo losetup -d "$TARGET"' EXIT
fi
start=$(sudo parted -sm "$TARGET" unit MiB print free | awk -F: '/free/ {s=$2} END {print s}')
sudo parted -s "$TARGET" mkpart persistence "${start}" 100%
sudo partprobe "$TARGET" || true; sleep 1
PART=$(lsblk -lnpo NAME "$TARGET" | tail -1)
echo "Erzeuge LUKS2 auf $PART (Passphrase wird abgefragt)…"
sudo cryptsetup luksFormat --type luks2 --label persistence "$PART"
sudo cryptsetup open "$PART" trendo-persist
sudo mkfs.ext4 -q -L persistence /dev/mapper/trendo-persist
sudo mount /dev/mapper/trendo-persist /mnt && echo "/ union" | sudo tee /mnt/persistence.conf >/dev/null && sudo umount /mnt
sudo cryptsetup close trendo-persist
echo "Persistenzpartition angelegt."
