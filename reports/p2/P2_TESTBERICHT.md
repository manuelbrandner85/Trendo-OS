# P2 Linux-Edition – Testbericht (21.10.2026, nur Box/VM)
Status: **IN_PROGRESS**. Keine echten Datenträger berührt, nichts geflasht; Boot nur in QEMU mit virtuellem CD-ROM.

| Prüfung | Ergebnis |
|---|---|
| ISO-Build Debian 13 + KDE Plasma (live-build) | ✅ `trendo-os-p2-amd64.hybrid.iso`, 1,8 GB (SHA-256 in `ISO-SHA256SUMS.txt`) |
| Statische ISO-Prüfung `tests/test-image.sh` | ✅ 18/18 PASS (`test-image.log`) |
| QEMU UEFI (OVMF): Bootloader + Menü | ✅ `qemu-uefi-bootmenu.png` |
| QEMU BIOS: Bootloader + Menü | ✅ `qemu-bios-bootmenu.png` |
| Boot bis Anmeldebildschirm (UEFI + BIOS) | ✅ SDDM auf Deutsch erreicht (`qemu-*-300s.png`) |
| Automatische Anmeldung in Trendo-Sitzung | ✅ PASS (Build 6, UEFI + BIOS, QEMU/TCG). Build 4 schlug fehl, weil das Paket `user-setup` fehlte und kein Live-Benutzer angelegt wurde (`boot-v4-fehlgeschlagen/`). Build 5: Autologin ok, nach 5 min aber Bildschirmsperre (`boot-v5/`). Build 6: Sperre im Live-System aus. Belege: `boot-v6/uefi-300s.png`, `boot-v6/bios-300s.png` |
| Tastatur Deutsch | ⚠️ TEIL: Konfiguration im Image geprüft (PASS: `/etc/default/keyboard` de, X11 `00-keyboard.conf` de, Plasma `kxkbrc` de, Bootparameter). Der Tipp-Test in der VM ist NICHT schlüssig: Alt+F2 öffnete KRunner nicht, die Tasten gingen ins Trendo-Fenster (`boot-v6/*-keyboard.png`). Ein Tipp-Test auf echter Hardware bzw. per Maus-Fokus folgt. |
| Trendo-App im Live-System gestartet | ✅ PASS – startet automatisch, neues Design mit Hintergrund-Blur (App aus main nach v0.6.1) |
| Linux-Tests der App (Box, Debian 13) | ✅ Core 41/41, API 14/14, UI 12/12 (Stand ISO-Build), API-Selbsttest bestanden |
| LUKS-Persistenz echt angelegt | NOT_RUN (nur Bootoption + Skript vorhanden) |
| Echte Hardware / USB-SSD / Secure Boot / NVIDIA | NOT_RUN – braucht Manuel |
Hinweise: KVM hängt in der Box-VM (verschachtelt) → Emulation (TCG), daher langsam. Das ISO enthält noch die App-Version vor dem neuen Design (0.3.0-Stand).

## Build 6 (21.10.2026, 19:2x MESZ)
- ISO SHA-256: 03f0e2afd4cd5b4f4ba96227a44c43b6cb24312dd8770c1c70bb0b9041ebd396 (ISO nicht im Repo, Prüfsumme in `boot-v6/ISO-SHA256SUMS`)
- Statischer Test: 25/25 PASS (`boot-v6/test-image-v6.log`)
- Boot UEFI + BIOS: Desktop mit Autologin und Trendo-App nach ≤300 s (TCG ohne KVM, daher langsam)
- Nicht auf echter Hardware getestet. Kein Laufwerk beschrieben, kein Flashen.
