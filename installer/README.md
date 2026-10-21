# Windows-Installer
**Empfohlen: `TrendoOS-Setup.exe`** (Velopack) – Doppelklick genügt.
- Installation nur für den aktuellen Benutzer nach `%LOCALAPPDATA%\TrendoOS`, **ohne Adminrechte/UAC**.
- Legt Verknüpfungen im Startmenü und auf dem Desktop an (Velopack-Standard; Desktop-Verknüpfung ist nicht abwählbar).
- Eintrag unter *Einstellungen → Apps → Installierte Apps* (HKCU-Uninstall-Schlüssel), dort deinstallierbar.
- Still: `TrendoOS-Setup.exe --silent` · Deinstallation still: `%LOCALAPPDATA%\TrendoOS\Update.exe --uninstall --silent`
- **Nicht signiert** (kein Code-Signing-Zertifikat) → Windows SmartScreen kann warnen („Weitere Informationen → Trotzdem ausführen“).

Bauen: `installer/build-setup.sh [Version]` (Linux oder Windows mit Git-Bash; benötigt `vpk`).
Alternativ weiterhin: `build-package.ps1` + `install.ps1` (Skript-Installation).
