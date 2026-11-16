# T-0681 B1b: Sicherung und gefundene Fehler

**Ziel:** Die im Audit vom 16.11.2026 belegten Fehler bei Sicherung, Abläufen, Podcast, App-Center, Code-Editor und still bestehenden Tests beheben – je mit Test, der vorher rot und nachher grün ist.
**Branch:** claude/T-0681-b1b-fehler (von origin/main)
**Dateien/Bereich:** `src/Trendo.Core/Backup.cs`, `src/Trendo.Core/W2/Workflows.cs`, `src/Trendo.Core/Cli.cs`, `src/Trendo.App/MainWindow.{Setup,Workflows,KreativStudios,AppCenter,CodeEditor}.cs`, Tests. Nicht anfassen: `ProgramDocument.cs`, `MainWindow.ProgramShell.cs`, Office-Seiten (Paket B1a).
**Fertig, wenn:**
- [x] Sicherung: schreibt in eine vorläufige Datei, liest sie zur Prüfung zurück (Anzahl, Größe, Prüfsummen), legt sie erst dann ab; gesperrte Dateien stehen als „nicht gesichert“ im Ergebnis, die Sicherung gilt als unvollständig und wird so angezeigt; Reste werden aufgeräumt; Wiederherstellen prüft vorher und überspringt Beschädigtes mit Meldung
- [x] Abläufe: Fehlschlag setzt „zuletzt gelaufen“ nicht; erneuter Versuch beim nächsten Takt mit Obergrenze und sichtbarem Fehlerstand; Speichern atomar; beschädigte Datei wird als `.beschaedigt-<zeit>` aufgehoben und gemeldet
- [x] Podcast: Beschäftigt-Merker wird in jedem Fall zurückgesetzt, Meldung nennt den Einrichten-Weg
- [x] App-Center: echte Rückfrage vor Installieren, Aktualisieren und Entfernen; ohne Bestätigung passiert nichts; Ablehnen steht im Protokoll
- [x] Code-Editor: „Ausführen“/„Testen“ im Hintergrund, Ausgabe laufend, „Abbrechen“ beendet den eigenen Kindprozess
- [x] Stille Test-Rückkehr (`if (…) return;`) auf sichtbares Überspringen umgestellt, keine Prüfung abgeschwächt
- [x] gezielte Testklassen grün (Zahlen im Bericht); Volltest macht die Hauptsitzung
**Nicht tun:** kein Push, kein Merge, kein Release; keine zweite Engine; bestehende Dateiformate bleiben lesbar.
**Am Ende:** Bericht `tasks/T-0681-b1b-fehler.bericht.md`.
