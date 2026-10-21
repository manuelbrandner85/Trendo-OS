# P2 Linux-Edition – Testbericht (21.10.2026, nur Box/VM)
Status: **IN_PROGRESS**. Keine echten Datenträger berührt, nichts geflasht; Boot nur in QEMU mit virtuellem CD-ROM.

| Prüfung | Ergebnis |
|---|---|
| ISO-Build Debian 13 + KDE Plasma (live-build) | ✅ `trendo-os-p2-amd64.hybrid.iso`, 1,8 GB (SHA-256 in `ISO-SHA256SUMS.txt`) |
| Statische ISO-Prüfung `tests/test-image.sh` | ✅ 18/18 PASS (`test-image.log`) |
| QEMU UEFI (OVMF): Bootloader + Menü | ✅ `qemu-uefi-bootmenu.png` |
| QEMU BIOS: Bootloader + Menü | ✅ `qemu-bios-bootmenu.png` |
| Boot bis Anmeldebildschirm (UEFI + BIOS) | ✅ SDDM auf Deutsch erreicht (`qemu-*-300s.png`) |
| Automatische Anmeldung in Trendo-Sitzung | ❌ FAIL – kein Autologin konfiguriert; Fix (SDDM-Autologin) eingecheckt, Neubau nötig |
| Tastatur Deutsch im Anmeldebildschirm | ❌ FAIL – zeigt „us“; Fix `/etc/default/keyboard` eingecheckt, Neubau nötig |
| Trendo-App im Live-System gestartet | NOT_RUN (hängt am Autologin) |
| Linux-Tests der App (Box, Debian 13) | ✅ Core 41/41, API 14/14, UI 12/12 (Stand ISO-Build), API-Selbsttest bestanden |
| LUKS-Persistenz echt angelegt | NOT_RUN (nur Bootoption + Skript vorhanden) |
| Echte Hardware / USB-SSD / Secure Boot / NVIDIA | NOT_RUN – braucht Manuel |
Hinweise: KVM hängt in der Box-VM (verschachtelt) → Emulation (TCG), daher langsam. Das ISO enthält noch die App-Version vor dem neuen Design (0.3.0-Stand).
