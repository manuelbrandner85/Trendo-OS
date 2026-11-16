# T-0682: B1c – Fremde Office-Dateien schützen

**Ziel:** Öffnet man in Trendo Text, Tabelle oder Präsentation eine fremde Datei, deren Inhalt Trendo nur zum Teil hält, geht beim Speichern nichts mehr still verloren.
**Branch:** claude/T-0682-b1c-office-schutz
**Dateien/Bereich:** `src/Trendo.Core/Office/*`, `src/Trendo.App/MainWindow.Text*.cs`, `MainWindow.Tabelle*.cs`, `MainWindow.Praesentation*.cs`, `MainWindow.OfficeRecovery.cs`, neu `MainWindow.OfficeImport.cs`; Tests und `tests/Daten/office/`.
**Fertig, wenn:**
- [x] Die Leser liefern einen Importbericht am Dokument: was nicht übernommen wurde, wie oft, wo (nur Erkanntes).
- [x] Strg+S überschreibt ein Original mit erkanntem Verlust nicht mehr still: Hinweisleiste, „Als Kopie speichern“ (Standard, `<name> (Trendo).<endung>`), „Trotzdem überschreiben“ erst nach Bestätigung, Original vorher als `<name>.original.<endung>` gesichert.
- [x] Ohne erkannten Verlust bleibt das Verhalten wie bisher.
- [x] Fremddateien mit echtem Inhalt unter `tests/Daten/office/` (nicht aus Trendos Schreibern) und Tests, die den echten Weg prüfen (Bytes auf der Platte).
- [ ] alle drei Testprojekte grün – der Volltest läuft in der Hauptsitzung; hier nur die betroffenen Klassen (Zahlen im Bericht).
**Nicht tun:** Kein Ausbau des Dokumentmodells (Paket D1). `ProgramDocument.cs`, `MainWindow.ProgramShell.cs`, Backup, Workflows, AppCenter, CodeEditor nicht anfassen. Die Abgleichtests, die ohne LibreOffice still zurückkehren, gehören zu T-0681.
**Am Ende:** Nur committen (kein Push, kein Merge, kein Release), Bericht `tasks/T-0682-b1c-office-schutz.bericht.md`.
