# Setup.exe – Test auf Windows-PC (21.10.2026, ohne Admin)
Artefakt: `TrendoOS-Setup.exe` 0.3.0 (Velopack 1.2.161, **unsigniert**), Release: https://github.com/manuelbrandner85/Trendo-OS/releases/tag/v0.3.0-preview1
| Prüfung | Ergebnis |
|---|---|
| Stille Installation `--silent`, ohne Admin/UAC | ExitCode 0 ✅ |
| Dateien in `%LOCALAPPDATA%\TrendoOS` | ✅ |
| Startmenü-Verknüpfung „Trendo OS“ | ✅ |
| Desktop-Verknüpfung | ✅ (Velopack-Standard, im Setup nicht abwählbar) |
| HKCU-Uninstall-Eintrag (Apps & Features), kein HKLM | ✅ |
| Installierte App startet, Screenshot | ✅ |
| Stille Deinstallation `Update.exe --uninstall --silent` | ExitCode 0, App/Verknüpfungen/Registry entfernt ✅ |
Nicht getestet: interaktive Doppelklick-Installation per Hand, SmartScreen-Verhalten (Code-Signing fehlt).
