#!/bin/bash
# Statische Prüfungen des gebauten ISO ohne Boot und ohne Loop-Mount (xorriso + unsquashfs). Ausgabe: PASS/FAIL je Prüfung.
set -uo pipefail
ISO="${1:?ISO}"; T=$(mktemp -d); fails=0
ok(){ echo "PASS  $1"; }; ko(){ echo "FAIL  $1"; fails=$((fails+1)); }
LS=$(xorriso -indev "$ISO" -find / -type f 2>/dev/null | tr -d "'")
SQP=$(grep -m1 'filesystem.squashfs$' <<<"$LS")
[[ -n "$SQP" ]] && ok "squashfs vorhanden ($SQP)" || ko "squashfs fehlt"
grep -qiE '^/EFI/boot/bootx64.efi$' <<<"$LS" && ok "UEFI-Bootloader vorhanden" || ko "UEFI-Bootloader fehlt"
grep -qE 'isolinux/isolinux.bin$|boot/grub/i386-pc' <<<"$LS" && ok "BIOS-Bootloader vorhanden" || ko "BIOS-Bootloader fehlt"
xorriso -osirrox on -indev "$ISO" -extract "$SQP" "$T/fs.sq" -extract /boot/grub/grub.cfg "$T/grub.cfg" >/dev/null 2>&1
grep -rqs "persistence-encryption=luks" "$T/grub.cfg" && ok "Boot-Option LUKS-Persistenz (GRUB)" || ko "LUKS-Persistenz fehlt in GRUB"
L=$(unsquashfs -lls "$T/fs.sq" 2>/dev/null | sed -E 's/^.*squashfs-root/squashfs-root/; s/ -> .*$//' | sed 's#^squashfs-root##')
has(){ grep -qxE "$1" <<<"$L"; }
has '/opt/trendo/TrendoOS' && ok "Trendo-App unter /opt/trendo" || ko "Trendo-App fehlt"
has '/etc/udev/rules.d/90-trendo-protect-windows.rules' && ok "udev-Schutzregel Windows/BitLocker" || ko "udev-Regel fehlt"
has '/etc/xdg/autostart/trendo-os.desktop' && ok "Autostart-Eintrag" || ko "Autostart fehlt"
has '/etc/sddm.conf.d/10-trendo-autologin.conf' && ok "SDDM-Autologin-Konfiguration" || ko "Autologin-Konfiguration fehlt"
unsquashfs -cat "$T/fs.sq" etc/default/keyboard 2>/dev/null | grep -q 'XKBLAYOUT="de"' && ok "Tastatur de (etc/default/keyboard)" || ko "Tastatur nicht de"
unsquashfs -cat "$T/fs.sq" etc/sddm.conf.d/10-trendo-autologin.conf 2>/dev/null | grep -q "User=trendo" && ok "Autologin-Benutzer trendo" || ko "Autologin-Benutzer fehlt"
has '/usr/sbin/user-setup|/usr/lib/user-setup/user-setup-apply' || grep -q user-setup <<<"$L" && ok "user-setup (Live-Benutzer wird angelegt)" || ko "user-setup fehlt – kein Live-Benutzer, kein Autologin"
has '/etc/X11/xorg.conf.d/00-keyboard.conf' && ok "X11-Tastatur de (Anmeldebildschirm)" || ko "X11-Tastatur fehlt"
has '/etc/skel/.config/kxkbrc' && ok "Plasma-Tastatur de (kxkbrc)" || ko "kxkbrc fehlt"
has '/etc/skel/.config/kscreenlockerrc' && ok "Bildschirmsperre im Live-System aus" || ko "kscreenlockerrc fehlt"
for b in chromium libreoffice mpv okular dolphin konsole git python3 tesseract cryptsetup; do
  grep -qE "^/(usr/)?s?bin/$b$" <<<"$L" && ok "Programm: $b" || ko "Programm fehlt: $b"
done
grep -qs "locales=de_DE.UTF-8" "$T/grub.cfg" && grep -qs "keyboard-layouts=de" "$T/grub.cfg" && ok "Sprache de_DE + Tastatur de (Bootparameter)" || ko "Sprache/Tastatur nicht de"
rm -rf "$T"
echo "ERGEBNIS: $([[ $fails -eq 0 ]] && echo 'ALLE BESTANDEN' || echo "$fails FEHLER")"; exit $fails
