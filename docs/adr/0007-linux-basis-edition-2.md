# ADR-0007: Linux-Basis der bootbaren Edition (P2)
**Status:** PROPOSED · **Datum:** 2026-10-21 · **Bezug:** OFFENE_ENTSCHEIDUNGEN Nr. 3/4, ROADMAP P2, SYSTEMBIBEL §2 Edition 2

## Kontext
Edition 2 soll ohne Pi von externer SSD/USB booten, persistent und verschlüsselt sein, Windows/BitLocker/Secure Boot schützen und Essentials ab Erststart bieten. Die Trendo-App (.NET 10 + Avalonia, ADR-0005) läuft nativ unter Linux x64.

## Vorschlag
- **Debian 13 „trixie“ (amd64)**, gebaut reproduzierbar mit **live-build** (`linux-image/`).
- **KDE Plasma** (Qt; in der Spezifikation als KDE/Qt-Ansatz genannt), SDDM, Dolphin.
- Boot: GRUB-EFI (UEFI) + Syslinux (BIOS), ISO-Hybrid. Secure Boot über Debians signierte shim/grub-Kette – **noch nicht verifiziert**.
- Persistenz: live-boot `persistence` mit **LUKS** (`persistence-encryption=luks`), separate Datenpartition `persistence`.
- Schutz Windows: udev-Regel – NTFS/BitLocker/Microsoft-Reserved-Partitionen werden nicht automatisch eingebunden (`UDISKS_IGNORE`).
- Essentials-Kandidaten: Chromium, LibreOffice, mpv, Okular, Dolphin, Git, Python, Tesseract (Lizenzprüfung vor Auslieferung offen).

## Alternativen
Ubuntu 24.04 LTS (größere Hardwareabdeckung, Snap-Zwang bei Chromium), Fedora KDE (kurze Lebenszyklen), openSUSE (Kiwi-Builds). Debian wegen Stabilität, freier Lizenzlage und live-build gewählt.

## Offen / Entscheidung durch Manuel
Distribution bestätigen, SSD-Größe und Partitionsplan (Nr. 4), Secure-Boot-Anforderung, proprietäre Firmware (WLAN/GPU, NVIDIA-Treiber für RTX 5070), Update-Strategie.
