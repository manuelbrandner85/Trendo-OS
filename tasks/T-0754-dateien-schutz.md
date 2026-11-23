# T-0754: Dateiaktionen rückgängig machen (CORE-01) und ehrlicher Hinweis „Sicherungen unverschlüsselt“ (CORE-11)

**Ziel:** Restliste `docs/audit/2026-11-16-profi-audit/d7-restliste.md`, Rang 31 und 32 – vom Nutzer am 23.11.2026 freigegeben.
**Branch:** claude/T-0754-dateien-schutz
**Dateien/Bereich:** Dateimanager (`src/Trendo.Core/Shell/FileBrowser.cs`, `FileUndo.cs`, `OsBehavior.cs`, `src/Trendo.App/MainWindow.Dateien.cs`, Mac-Menüleiste in `MainWindow.OsStyles.cs`); Sicherung und Schutz (`src/Trendo.Core/Backup.cs`, `W3/Security.cs`, `src/Trendo.App/MainWindow.Setup.cs`, `MainWindow.W3.cs`, `StudioCatalog.cs`).

**Fertig, wenn:**
- [x] (1) Strg+Z / „Rückgängig“ (Befehlsleiste, Rechtsklick auf die freie Fläche, Menü „⋯“/„☰“, Mac-Menüleiste „Bearbeiten“) für Verschieben, Umbenennen, Kopieren, Neuer Ordner, Löschen.
- [x] Löschen geht in den Windows-Papierkorb (bestehender Weg `FileSystem.DeleteFile … SendToRecycleBin`) bzw. in den Testordner-Papierkorb; Rückgängig holt aus dem Papierkorb zurück (bestehende `$I`-Lesehilfe `Trash.ParseWindowsInfo`).
- [x] Strg+Y (Explorer), Strg+Umschalt+Z (GNOME), ⌘⇧Z (Finder) wiederholen.
- [x] Mehrere Schritte, Liste der Sitzung („Letzte Dateiaktionen“ im Rechtsklick).
- [x] Ehrliche Meldung, wenn es nicht mehr geht (extern geändert, alter Platz belegt, Ordner nicht leer, Papierkorb geleert) – dann wird nichts geändert und nichts überschrieben.
- [x] Rot-Grün-Tests mit echten Dateien im Temp-Ordner, ein Test mit dem echten Windows-Papierkorb.
- [x] (2) Keine Verschlüsselung (Entscheidung des Nutzers), deutlicher Hinweis in der Oberfläche, im Ergebnis der Sicherung und im Vermerk jeder ZIP; Ordner zeigen und öffnen; Empfehlung verschlüsseltes Laufwerk (BitLocker) nur als Text.
- [x] Ampel und Studio-Text versprechen nichts Falsches; Prüflesen aus T-0681 bleibt.

**Nicht tun:** keine Verschlüsselung einbauen, nichts am System ändern (kein BitLocker-Aufruf), kein Volltest, kein Merge/Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0754-dateien-schutz.bericht.md`.
