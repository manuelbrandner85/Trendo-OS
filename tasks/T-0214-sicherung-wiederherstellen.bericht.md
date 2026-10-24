# T-0214 Bericht: Sicherung wiederherstellen

**Auftrag:** „Wiederherstellen“ ohne Absturz, mit Vorschau, Rückfrage, Vorsicherung und Ergebnis.
**Branch:** `claude/T-02xx` (Claude Code am Windows-PC)

## Stand vorher (main `59904c3`)
Rückfrage („Sicher?“), automatische Sicherung des aktuellen Stands mit eindeutigem Namen, Ersetzen schreibgeschützter Dateien (`.git/objects`), Meldung „Wiederhergestellt: n Dateien“ und Tests dazu waren bereits vorhanden.

## Geändert
- `src/Trendo.Core/Backup.cs`: neue `Preview(zip)` – zählt ersetzte und neue Dateien (ohne Audit-Ordner, mit Zip-Slip-Schutz) und nennt Beispiele, schreibt nichts.
- `src/Trendo.App/MainWindow.Setup.cs`: Rückfrage zeigt „Ersetzt n Dateien, ergänzt m (z. B. …)“; unlesbare Sicherung wird gemeldet statt abzustürzen.
- `tests/Trendo.Core.Tests/BackupRestoreTests.cs`: Test `Preview_lists_changes_without_writing`.
- `tests/Trendo.App.Tests/BackupUiTests.cs`: prüft den Vorschautext in der Rückfrage.

## Tests
- `dotnet test tests/Trendo.Core.Tests --filter Backup`: 4 bestanden, 0 Fehler.
- `dotnet test tests/Trendo.App.Tests --filter BackupUi`: 1 bestanden, 0 Fehler.
- Gesamtlauf aller drei Testprojekte: im Sammel-Commit des Blocks (Zahlen dort).

## Nicht geprüft
Wiederherstellen einer großen Sicherung (mehrere GB) auf dem PC; Verhalten bei vollem Datenträger.
