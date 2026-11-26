# T-0770: Abnahme Installieren, Update, Deinstallieren, Leistung (Phase E, Etappe 0.9.111)

**Ziel:** Echter Lebenszyklus von Trendo OS mit eigenen Abnahme-Paketen (Setup still, Start, Update A→B über den Update-Weg der App, Deinstallation über Update.exe) plus Leistungszahlen des Release-Builds – belegt unter `reports/T-0770/` mit Rohlogs und SHA-256.
**Branch:** claude/T-0770-abnahme-installation
**Dateien/Bereich:** `tools/abnahme/`, `reports/T-0770/`, `tools/Trendo.Render/Program.cs` (Messmodus `TRENDO_GROSS`), kleine Umgebungsvariablen für die Abschottung in `src/Trendo.Core/DataLocation.cs` (`TRENDO_INSTANZ`) und `src/Trendo.App/TrendoUpdater.cs` (`TRENDO_UPDATE_QUELLE`), Tests dazu.
**Harte Grenze:** Die echte Installation (`%LOCALAPPDATA%\TrendoOS`, Daten `%APPDATA%\TrendoOS`, Werkzeuge `%LOCALAPPDATA%\TrendoWerkzeuge`, Verknüpfungen, HKCU-Eintrag `TrendoOS`, Prozesse) wird nie angefasst, gestartet, aktualisiert oder deinstalliert – nur lesend vorher/nachher verglichen.
**Fertig, wenn:**
- [x] zwei Pakete mit eigener Id `TrendoOSAbnahme` (0.9.110-abnahme1, 0.9.110-abnahme2) und lokaler Update-Ordner gebaut, SHA-256 festgehalten
- [x] Setup A still in eigenen Ordner, Dateien da, Start mit Hauptfenster, Ende per PID
- [x] Update A→B über den Update-Weg der App mit lokaler Quelle, B läuft, Testdatei im Datenordner bleibt
- [x] Deinstallation über Update.exe, Reste (Ordner, Verknüpfungen, HKCU) aufgelistet und bewertet
- [x] Kalt-/Warmstart (je 3, Median), Speicher Leerlauf/5 Programme, große Dateien (500 Seiten, 100.000 Zeilen, 8000×8000)
- [x] wiederholbares Skript `tools/abnahme/lebenszyklus.ps1` ohne Benutzernamen
- [x] berührte Testklassen grün
**Nicht tun:** kein Upload, kein Release, kein Admin, kein winget/msiexec, keine eigenen Registry-Schreibzugriffe, nichts an der echten Installation, kein Volltest.
**Am Ende:** eigene Abnahme-Installation und Ordner entfernen, Branch pushen, Kopie löschen, Bericht `tasks/T-0770-abnahme.bericht.md`.
