# T-0701: D1b – Trendo Tabelle und Trendo Präsentation: fremde Dateien ohne Verlust

**Ziel:** Fremde XLSX/ODS/PPTX werden gelesen, bearbeitet und gespeichert, ohne dass gemeinsame Formeln, verbundene Zellen, Zahlenformate, Zellaussehen, Diagramm-Einzelheiten, Textformate je Feld, fremdes Design oder Animationen verloren gehen; was weiter nicht geht, meldet der Importbericht ehrlich.
**Branch:** claude/T-0701-tabelle-praesentation (vom Integrationszweig claude/T-0685-b1-integration)
**Grundlage:** Audit 2026-11-16 Teilbericht 1 (SYS-10, Tabelle/Präsentation), Bericht T-0682 (`tasks/T-0682-b1c-office-schutz.bericht.md`).
**Dateien/Bereich:** `src/Trendo.Core/Office/Sheet*.cs`, `Slide*.cs`, `ImportCheck.Sheet.cs`, `ImportCheck.Slide.cs`, `Formula.cs` (eine Funktion „relativ kopieren“), `src/Trendo.App/MainWindow.Tabelle*.cs`, `MainWindow.Praesentation*.cs`, `SheetGridView.cs`, `SheetChartView.cs`, `SlideView.cs`, `tools/Trendo.Render` (Datei öffnen für die Sichtprüfung), Tests, `reports/T-0701/`.
**Fertig, wenn:**
- [x] Gemeinsame Formeln: jede Folgezelle hat ihre Formel (relativ verschoben, $ fest), rechnet, steht nach dem Speichern als Formel in der Datei.
- [x] Verbundene Zellen lesen (XLSX, ODS), anzeigen, verbinden/aufheben, schreiben (XLSX).
- [x] Formatcodes lesen, wie Excel anzeigen (deutsch), unverändert zurückschreiben; eingebaute Formate behalten ihre Nummer.
- [x] Schrift, Ausrichtung, Umbruch, Einzug, Drehung, Rahmenarten, Designfarben, Zeilenhöhen, fixierte Zeilen, Achsentitel/Reihenfarben/Legende bleiben.
- [x] Präsentation: mehrere Textformate je Feld (lesen, zeigen, bearbeiten, schreiben, PDF), Griffe an allen Ecken/Kanten, fremdes Design und Animationen/Übergänge/Hintergründe werden unverändert durchgereicht.
- [x] Importbericht der Fremd-Testdateien: vorher je 3 Punkte, jetzt leer – und das stimmt (Tests prüfen die gespeicherte Datei).
- [x] Echter Abgleich mit LibreOffice, Beleg unter `reports/T-0701/`.
- [x] Oberfläche im Bild geprüft.
- [ ] Volltest (macht die Hauptsitzung am Schluss).
**Nicht tun:** Trendo Text (T-0692), `MainWindow.ProgramShell.cs`, `SimpleWords.cs`, „Präsentationen gestalten“; kein Merge, kein Release, nicht nach main oder auf den Integrationszweig pushen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0701-tabelle-praesentation.bericht.md`.
