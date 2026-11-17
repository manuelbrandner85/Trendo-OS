# T-0680 B1a: Programm-Hülle absturzsicher

**Ziel:** Nach einem Absturz, einem zweiten Fenster oder einer beschädigten Datei geht in den Programmen der Hülle (`RegisterProgram`) keine Arbeit still verloren.
**Branch:** claude/T-0680-b1a-absturzsicher
**Dateien/Bereich:** `src/Trendo.Core/Programs/ProgramDocument.cs` (mit `ProgramRecent`), neu `src/Trendo.Core/AtomicFile.cs` und `src/Trendo.Core/Programs/ProgramLocks.cs`, `src/Trendo.App/MainWindow.ProgramShell.cs` (nur Speichern/Sichern/Wiederherstellen), zugehörige Tests, Hilfsprogramm `tests/Trendo.CrashProbe`.
**Grundlage:** Audit vom 16.11.2026, Teilbericht 6 „Architektur“, Abschnitt 1 (Paket B1, vom Nutzer freigegeben).

**Fertig, wenn:**
- [x] Dokument, Notsicherung, Versionen und `zuletzt.json` werden atomar geschrieben (Zwischendatei im selben Ordner → auf den Datenträger → ersetzen); eine gemeinsame Hilfsmethode im Kern.
- [x] Eine neuere Notsicherung (neben der Datei oder im Sicherungsordner) wird beim Öffnen bzw. auf der Startseite klar angeboten (Wiederherstellen / Verwerfen, mit Zeitpunkt); kaputte Sicherungen werden übersprungen und gemeldet; die Sicherung verschwindet erst nach erfolgreichem Speichern.
- [x] Sperre je geöffneter Datei mit Prozesskennung und Zeit; verwaiste Sperren werden übernommen; ein zweites Öffnen bietet „Nur lesen“ oder „Als Kopie“; von außen geänderte Dateien werden nicht ohne Rückfrage überschrieben.
- [x] Beschädigte `.tdoc`: verständliche Meldung mit Angebot des letzten Stands; `"version"` wird gelesen, neuere Fassungen werden mit Hinweis abgelehnt (keine Migration).
- [x] Tests mit echtem Prozessabbruch (mindestens 20 Abbrüche) und ein Oberflächentest „Fenster stürzt ab → neues Fenster bietet Wiederherstellen an“.
- [x] Größter Verlust nach einem Abbruch gemessen und berichtet.
- [ ] alle drei Testprojekte grün – der Volltest läuft in der Hauptsitzung; hier gezielt die betroffenen Klassen (Zahlen im Bericht).

**Nicht tun:** Office-Seiten (Text, Tabelle, Präsentation), `Backup.cs`, Abläufe, AppCenter, Code-Editor, Podcast. Kein neues Dateiformat, keine Migration, kein Speichern abseits des Oberflächen-Fadens, keine Auslagerung großer Binärdaten. Kein Push, kein Merge, kein Release.
**Am Ende:** Bericht `tasks/T-0680-b1a-absturzsicher.bericht.md`.
