# Bericht T-0692: D1a – Trendo Text als durchgehender Texteditor

**Branch:** `claude/T-0692-text-editor` (abgezweigt von `claude/T-0685-b1-integration`), eigene Kopie in `%TEMP%\trendo-auftrag-T-0692`.
**Grundlage:** Audit-Teilbericht 1 (SYS-10 „Trendo Text“: absatzweise Eingabefelder, keine Auswahl über Absätze, kein Seitenumbruch am Bildschirm, „Seite 1 von …“, Zellen nur reiner Text) und V-20 aus Teilbericht 7.

## Was war → was ist jetzt

| Punkt | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| 1 Durchgehende Bearbeitung | Nur der Absatz mit dem Cursor war ein `TextBox` mit einer Schrift für den ganzen Absatz (`MainWindow.Text.cs` alt 18-19, 382-389) | Neues Steuerelement `TextPageView` (eigenes Avalonia-Control, kein AvaloniaEdit) über das ganze Dokument. Cursor/Auswahl über Absatz- und Seitengrenzen, gemischte Formate je Absatz, Tastatur: Pfeile, Pos1/Ende, Strg+Pfeil, Strg+Pos1/Ende, Bild auf/ab, Umschalt-Auswahl, Strg+A/C/X/V/Z/Y/B/I/U, Rücktaste/Entf (auch Strg+), Enter, Umschalt+Enter, Strg+Enter (Seitenumbruch), Tab (Liste/Zelle). Maus: Klick, Umschalt+Klick, Ziehen, Doppel-/Dreifachklick, Rechtsklick-Menü. Zwischenablage: in Trendo mit Format, nach außen reiner Text; Fremdtext wird als reiner Text eingefügt. Zeichen-/Absatzformate, Schrift, Größe, Farbe, Hervorhebung wirken auf die ganze Auswahl über mehrere Absätze. Fett ohne Auswahl wird fürs Weitertippen vorgemerkt (wie Word). `TextDoc` bleibt die Wahrheit; Bearbeiten nur über `TextEdit` (Core). | `T0692_TextEditorUi.Keyboard_across_paragraphs`, `.Mouse_selection` (echte Tastendrücke/Mausklicks im Headless-Fenster); Core `T0692_TextEditorTests.Edit_across_paragraphs`, `.Insert_keeps_formats`, `.Caret_and_hit` |
| 2 Seitenansicht | Ein endlos hohes Blatt, Fußzeile fest „Seite 1 von …“ | Seitensatz `FlowDoc` (`TextFlow.cs`) aus dem bisherigen PDF-Satz herausgelöst; **Bildschirm und PDF nutzen denselben Satz** (`TextPdf` malt nur noch). Echte Seiten mit Rändern, Kopf-/Fußzeile, Wasserzeichen, Spalten, „Seite x von y“; Statuszeile „Seite x von y“ folgt dem Cursor. Gezeichnet werden nur sichtbare Seiten. | `Flow_matches_pdf` (Seitenzahl = Seiten der geschriebenen PDF-Datei, gelesen mit `PdfTools.Read`), `Pages_on_screen`, `T0230_TrendoTextProUi` (Seitenzahl jetzt „Seite 1 von 1“) |
| 3 Tabellen | Zellen nur reiner Text; verbundene Zellen beim Import verloren | Modell: `CellFmt` (Zeichenformate, Markierungen, Ausrichtung, ColSpan/RowSpan) und Spaltenbreiten; Zellen im Schreibfeld direkt bearbeitbar (Klick, Tippen, Tab zur nächsten Zelle bzw. neue Zeile, Formate), „Verbinden →/↓“, „Verbindung lösen“, Spaltenbreite an der Linie ziehen, „Spalten gleich breit“, Zeile/Spalte am Cursor löschen. DOCX (gridSpan, vMerge, tblGrid) und ODT (spanned, covered-table-cell, Spaltenstile) lesen und schreiben. Suchen/Ersetzen behält Zellformate. | `Table_model`, `Roundtrip_docx`, `Roundtrip_odt` (auch ODT→DOCX), `Fremd_keeps_merged_cells`, UI `Table_cells` (Speichern DOCX+ODT, Rückgängig) |
| 4 Design V-20 | 42 Elemente, drei Leistenzeilen, leere Seite ohne Hinweis | „Einfach“: eine Werkzeugzeile (Neu, Öffnen, **Speichern** als einzige goldene Fläche, Rückgängig, Absatzart, Größe, Farbe, F/K/U, Listen, Ausrichtung, „Mehr …“); alles Weitere liegt unter „Mehr …“ (keine Funktion verloren); leere Seite zeigt „Hier schreiben …“; eingeschaltete Knöpfe mit Goldrand statt Goldfläche. Ab „Erweitert“ unverändert vollständig. | `Simple_one_row`, `UebersichtEinfach_Ui` (keine Funktion verloren), `UebersichtVorschlaege_Ui`; Bilder `reports/T-0692/text_leer.png`, `text_rechnung.png` |
| 5 Importbericht (T-0682) | `fremd-text.docx`: kopf.weitere, kopf.zeilen, textfeld, **verbunden (2×)**; `fremd-text.odt`: feld, textfeld, **verbunden** | Verbundene Zellen bleiben erhalten → Punkt entfällt; DOCX-Zellen mit fett/Farbe/Ausrichtung/Zeilenwechsel/Link werden nicht mehr gemeldet. Weiter gemeldet (weil weiter nicht gehalten): Zellhintergrund, verschiedene Ausrichtungen in einer Zelle, Aufzählung und Fußnoten in Zellen, alte hMerge-Verbindung, Textfelder, Felder, Kopfzeilen-Besonderheiten. | `ImportReportTests` (angepasst: verbundene Zellen jetzt im Dokument geprüft), `Import_report_cells`, `OfficeImportUiTests` |

Gefundener und behobener Fehler beim Testen: Tippen an derselben Stelle wurde mit einem früheren Tipp-Schritt zu einem Rückgängig-Schritt zusammengelegt, obwohl dazwischen eine andere Änderung lag – jede andere Änderung beendet den Tipp-Schritt jetzt.

## Angepasste bestehende Tests (nicht geschwächt)
- `TextUiTests`, `TextLookUiTests`, `TextF3UiTests`, `TextProUiTests`, `OfficeImportUiTests`: suchten das alte Eingabefeld `TextBox "text-edit"` bzw. `TextBlock "text-para-*"`, `text-sheet`, `text-footer-view`; jetzt dasselbe über das Schreibfeld bzw. die Seiten des Seitensatzes. Prüfinhalt gleich, Fußzeile jetzt strenger („Seite 1 von 1“ statt „Seite 1“).
- `ImportReportTests`/`OfficeImportUiTests`: erwarteten den Verlust „verbundene Zellen“. Dieser Verlust besteht nicht mehr; der Test prüft jetzt, dass die Verbindungen im Dokument und in der gespeicherten Datei stehen (strenger).
- `OfficeRecoveryUiTests`: setzte Fett ohne Markierung, was früher fälschlich „geändert“ meldete, ohne etwas zu ändern. Jetzt wird vorher markiert – eine echte Änderung; die Prüfung der Wiederherstellung ist unverändert.

Rot-Grün: Die neuen Tests nutzen `TextPageView`, `FlowDoc`, `TextEdit`, `Table.Merge/SpanOf` – auf dem alten Stand nicht übersetzbar, damit rot; der alte Leser hätte `docx.verbunden` gemeldet (bestehender Test belegte genau das).

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler.
- Core gezielt (TextDoc, TextF3, TextLook, TextPro, ImportReport, LetterOffice, OfficeConvert, ImportTempo, WritingAid, Spelling, T0692): **92 bestanden, 0 Fehler, 2 übersprungen** (beide brauchen das deutsche Wörterbuch, das auf dem PC fehlt – unabhängig von diesem Paket); `NoFixedUserTests` 1/1. Rohlog `reports/T-0692/core-tests.log`.
- App, 26 Klassen (T0692_TextEditorUi, UebersichtEinfach_Ui, UebersichtVorschlaege_Ui, OfficeRecoveryUiTests, T0223_TrendoTextUi, TextF3UiTests, Pilot_TextLookUi, T0230_TrendoTextProUi, OfficeImportUiTests, TextAiProposalTests, WritingAid_Ui, ClaudeLive*, ClaudePolish_Ui, SYS05_Ui, StudiosBueroGeld_Ui, W2_Ui, X05_Ui, CORE02/03/10/12, KreativStudios 1–3, Bildbearbeitung_Ui): **alle 0 Fehler** (zusammen 113 Tests). Rohlog `reports/T-0692/app-tests.log`.
- Leistung („Pilot Text Leistung“, Grenze unverändert 3 s / 1,5 s): Aufbau 284 ms, Formatwechsel 271 ms (vorheriger Messwert dieses PCs: 691 ms / 441 ms).
- Kein Volltest (macht die Hauptsitzung).

## Abnahmekriterien
1. Aus leerer Vorlage echtes Ergebnis über die Oberfläche: **VOLLWERTIG** – Tippen, Formate, Tabelle, Speichern DOCX/ODT, PDF (`Keyboard_across_paragraphs`, `Table_cells`).
2. Fremddatei importieren, bearbeiten, exportieren, im Fremdprogramm öffnen: **TEILWEISE** – Import/Bearbeiten/Export der Fremd-Testdateien belegt; Öffnen im Fremdprogramm **NICHT GETESTET**: kein LibreOffice (`C:\Program Files\LibreOffice\program` enthält nur `logs`, keine `soffice.exe`) und kein Word auf dem PC (`reports/T-0692/fremdprogramm-pruefung.txt`).
3. Prozessabbruch verliert höchstens 30 s: **TEILWEISE** – unverändert über die bestehende Sicherung (`OfficeRecoveryUiTests` grün); nicht neu gemessen.
4. KI-Fachaufgabe im offenen Dokument, rückgängig: **TEILWEISE** – unverändert (KI-Vorschlag mit Annehmen/Verwerfen, `TextAiProposalTests` grün); kein neuer Adapter in diesem Paket.
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** (`Table_cells`: Klick in Zelle, Tippen, Strg+B, Verbinden, Speichern, Wiederöffnen).
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: **TEILWEISE** – Stufen, Kürzel, Rechtsklick, Ziehen zum Markieren und Spaltenbreite ziehen belegt; Text per Ziehen verschieben und Dateien auf die Seite ablegen fehlen.

## Nicht geprüft
- Abgleich mit LibreOffice/Word (nicht vorhanden).
- Bildprüfung der Stufe „Professionell“: das Render-Werkzeug setzt die Stufe nur mit Programmvorlage; „Erweitert“ ist über die UI-Tests geprüft, das Bild zeigt „Einfach“.
- Systemzwischenablage im echten Windows-Fenster (Headless-Fenster hat keine; Weg mit Trendos Zwischenspeicher und Fremdtext ist getestet).
- Eingabe über IME/Zusammensetzen (z. B. Akzent-Tasten) und Bildschirmleser-Unterstützung des neuen Schreibfelds.

## Offen
- Mehrere Absätze, Aufzählungen, Bilder und Fußnoten in Tabellenzellen (Zellen halten einen Absatz mit Zeilenwechseln); Zellhintergrund; verschachtelte Tabellen; Textfelder.
- Tabellen werden nicht über Seiten geteilt (eine Zeile bzw. verbundene Zeilengruppe bleibt zusammen, eine sehr lange Zelle kann über den unteren Rand ragen).
- Auswahl, die in einer Tabellenzelle beginnt, bleibt in dieser Zelle; eine Auswahl aus Absätzen heraus umfasst Tabellen dazwischen ganz.
- Text per Ziehen und Ablegen verschieben; Bildgröße mit der Maus.
- Silbentrennung wirkt weiter nur in Word/LibreOffice, nicht im eigenen Seitensatz.

## Berührte Dateien
`src/Trendo.App/MainWindow.Text.cs`, `MainWindow.TextLook.cs`, `MainWindow.TextPro.cs`, neu `src/Trendo.App/TextPageView.cs`; `src/Trendo.Core/Office/TextDoc.cs`, `DocxFormat.cs`, `OdtFormat.cs`, `TextPdf.cs`, `ImportCheck.Text.cs`, neu `TextFlow.cs`, `TextEdit.cs`; Tests `tests/Trendo.Core.Tests/TextEditorTests.cs` (neu), `ImportReportTests.cs`, `tests/Trendo.App.Tests/TextEditorUiTests.cs` (neu), `TextUiTests.cs`, `TextLookUiTests.cs`, `TextF3UiTests.cs`, `TextProUiTests.cs`, `OfficeImportUiTests.cs`, `OfficeRecoveryUiTests.cs`; `reports/T-0692/*`; `tasks/T-0692-text-editor*.md`.

**Status:** umgesetzt und gezielt getestet; nicht gemergt, kein Release.
