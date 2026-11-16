# T-0692: D1a – Trendo Text als durchgehender Texteditor

**Ziel:** In Trendo Text schreibt man wie in Word/Writer direkt auf echten Seiten – ein Schreibfeld über das ganze Dokument, Auswahl über Absätze, derselbe Seitenumbruch wie im PDF.
**Branch:** claude/T-0692-text-editor (Basis: claude/T-0685-b1-integration)
**Dateien/Bereich:** `src/Trendo.App/MainWindow.Text*.cs`, neues Steuerelement `src/Trendo.App/TextPageView.cs`, `src/Trendo.Core/Office/TextDoc.cs`, `DocxFormat.cs`, `OdtFormat.cs`, `TextPdf.cs`, neu `TextFlow.cs` und `TextEdit.cs`, Importprüfung `ImportCheck.Text.cs` (T-0682 mitpflegen), Tests, `reports/T-0692/`.
Grundlage: Audit-Teilbericht 1 (SYS-10 „Trendo Text“), Design-Vorschlag V-20 (Audit-Teilbericht 7).
**Fertig, wenn:**
- [x] Ein Schreibfeld über das ganze Dokument: Cursor und Auswahl über Absatzgrenzen, gemischte Zeichenformate je Absatz, Tastatur (Pfeile, Pos1/Ende, Strg+Pfeil, Umschalt-Auswahl, Bild auf/ab, Strg+A/C/X/V/Z/Y/B/I/U, Rücktaste/Entf, Enter, Umschalt+Enter, Strg+Enter), Maus (Klick, Umschalt+Klick, Ziehen, Doppel-/Dreifachklick), Zwischenablage mit Format in Trendo und als reiner Text nach außen.
- [x] Echte Seiten am Bildschirm mit Rändern, Kopf-/Fußzeile, „Seite x von y“ – ein Seitensatz für Bildschirm und PDF.
- [x] Tabellen: Zellen mit formatiertem Text, verbundene Zellen (lesen, anzeigen, bearbeiten, schreiben DOCX/ODT), Spaltenbreiten.
- [x] V-20: eine Werkzeugzeile in „Einfach“, Rest unter „Mehr …“, „Hier schreiben …“ auf der leeren Seite, genau eine goldene Hauptaktion.
- [x] Rundreise-Tests DOCX/ODT, Fremd-Testdateien behalten mehr (Importbericht kürzer), UI-Tests mit echten Tastendrücken und Maus, Leistungstest unverändert grün.
**Nicht tun:** Tabelle, Präsentation, PDF-Programm, `MainWindow.ProgramShell.cs`, `SimpleWords.cs`. Kein Volltest (macht die Hauptsitzung), kein Merge, kein Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0692-text-editor.bericht.md`.
