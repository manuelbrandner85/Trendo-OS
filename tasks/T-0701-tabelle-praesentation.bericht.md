# T-0701 D1b – Trendo Tabelle und Trendo Präsentation: fremde Dateien ohne Verlust – Bericht

**Branch:** `claude/T-0701-tabelle-praesentation`, abgezweigt vom Integrationszweig `claude/T-0685-b1-integration`. Kein Merge, kein Release.
**Status:** IMPLEMENTED, gezielt getestet (Kern + Oberfläche ohne Bildschirm + echtes LibreOffice), im Bild geprüft. Kein Volltest (macht die Hauptsitzung).

## Was war (Beleg) → was ist jetzt → Nachweis

### Tabelle
| Punkt | Vorher (T-0682-Bericht / Code) | Jetzt | Nachweis |
|---|---|---|---|
| Gemeinsame Formeln | Folgezellen nur als Wert gelesen, Formel nach dem Speichern weg | Jede Folgezelle bekommt ihre Formel, relativ verschoben (`$` bleibt fest, auch Bezüge auf andere Blätter), rechnet, wird als eigene Formel geschrieben (`Formula.CopyRefs`, `SheetFiles.ReadXlsx`) | `T0701_TabellePraesentation.Gemeinsame_Formeln` (B5 geändert → D5 rechnet 48, Datei enthält `<f>B5*C5</f><v>48</v>`), Gegenprobe: mit abgeschalteter Behandlung liest der Test `28,125` statt `=B4*C4` (rot); echt mit LibreOffice: `LibreOffice_rundreise` |
| Verbundene Zellen | nicht im Modell | `Sheet.Merges` lesen (XLSX `mergeCells`, ODS `number-columns/rows-spanned`), anzeigen (ein Feld im Raster, Auswahl springt auf den ganzen Verbund), verbinden/aufheben (Menüband, Rechtsklick), Zeilen/Spalten einfügen/löschen verschieben den Bereich, schreiben (XLSX) | `Verbinden`, `Zellaussehen_rundreise`, UI `Tabelle_fremd_bearbeiten_und_speichern`, LibreOffice liest `A1:C1` zurück |
| Zahlenformate | 8 feste Formate, fremde Codes wurden „Zahl“ | Jeder Formatcode bleibt unverändert (`Cell.NumCode`), eingebaute Excel-Formate behalten ihre Nummer; Anzeige nach Code auf Deutsch (`SheetNumberFormat`: Abschnitte, Bedingungen, `[Rot]`, %, Tausender, Skalierung, E+, Brüche, Datum/Zeit, `[h]`, Text); eigenes Format über „Eigenes“/Rechtsklick (deutsch eingegeben) | `Formatcodes` (22 Fälle), `Xlsx_fremd` („1,235 kg“), LibreOffice-Abgleich (`dd/mm/yyyy` und `0,00 %` von LibreOffice werden gleich angezeigt) |
| Zellformate | fett, Füllfarbe, Rahmen ja/nein | dazu kursiv, Unterstreichung (auch doppelt), durchgestrichen, Schriftfarbe, -größe, -art, Grundschrift der Mappe, Ausrichtung waagerecht/senkrecht, Umbruch, Einzug, Drehung, Verkleinern, Rahmen je Seite mit Art und Farbe, Designfarben (theme + tint) und alte Palette werden feste Farben | `Zellaussehen_rundreise`, `Designfarben`, Bild `budget.png` |
| Spaltenbreiten / Zeilenhöhen / fixiert | Breiten und Fixieren gab es, Zeilenhöhen nicht | Zeilenhöhen lesen/zeigen/schreiben | `Zellaussehen_rundreise` |
| Diagramme | Art, Bereich, Titel | dazu Achsentitel, Reihenfarben, Lage der Legende (auch „keine“) – lesen, zeigen, schreiben | `Diagramm_rundreise`, `Xlsx_fremd`, LibreOffice-PDF zeigt Achsentitel, Farben, Legende rechts |
| ODS | nur Inhalte | dazu verbundene Zellen, Notizen, Zellaussehen (Schrift, Füllung, Ausrichtung, Umbruch, Rahmen, Drehung) | `Odf_fremd` |
| Importbericht | meldete u. a. verbundene Zellen, Zahlenformate, Schrift, Rahmenarten, Ausrichtung, gemeinsame Formeln, Zeilenhöhen, Diagramm-Einzelheiten | meldet nur noch, was weiter nicht geht: hoch/tief, Kontur/Schatten, Füllmuster/Verläufe, Diagonalen, Leserichtung, unbekannte eingebaute Formatnummern, Matrixformeln, Diagramm-Rest (Datenbeschriftungen, Achsenformate …) und alles Bisherige (Makros, Pivot, Bilder …) | `Einzelne_Verluste` (hoch­gestellt, Leserichtung, Matrixformel werden gemeldet), `Eigene_Dateien_ohne_Verlust` |

### Präsentation
| Punkt | Vorher | Jetzt | Nachweis |
|---|---|---|---|
| Mehrere Textformate je Feld | ein Format je Objekt (erstes `a:rPr`) | `SlideShape.Runs` (Größe, fett, kursiv, Unterstreichung, Farbe auch aus Designfarben mit lumMod/lumOff, Schrift); Anzeige, PDF-Export und PPTX schreiben je Abschnitt; Text ändern behält die Formate der übrigen Stellen (Eingefügtes bekommt das Format davor, Ersetztes das des ersten ersetzten Zeichens); Strg+B/I/U und die Knöpfe Fett/Kursiv/Unterstr. gelten im offenen Textfeld nur für die Markierung, sonst für den ganzen Text | `Textlaeufe_bearbeiten`, `Pptx_fremd`, UI `Praesentation_fremd_teilformat_und_speichern` (Wort markieren → Strg+I, Strg+B → Datei enthält `b="1" i="1"`), LibreOffice-PDF zeigt „12 Prozent“ rot/fett/größer |
| Bearbeiten auf der Folie | Text per Doppelklick, ein Griff unten rechts | acht Griffe (Ecken und Kanten) in Gold, links/oben verschieben die Kante | im Bild `praes.png`; Ziehen selbst: NICHT automatisiert getestet |
| Fremdes Design | durch Trendos Standarddesign ersetzt | Folienmaster, Layouts, Theme samt Bildern werden unverändert ins Paket kopiert (`PptxDesign`), Folien zeigen auf ihr ursprüngliches Layout, Platzhalter bleiben Platzhalter, geerbte Schriftgrößen/Farben werden nicht fest überschrieben, Foliengröße bleibt; neue Folien nehmen das passende Layout der Vorlage; Kachel „Design der Datei“ im Entwurf; wählt man ein Trendo-Design, wird bewusst ersetzt | `Fremdes_Design_bleibt` (Master/Layout/Theme bytegleich), `Uebergang_und_Hintergrund` |
| Animationen / Übergänge / Hintergrund | fremde Animationen weg, Übergänge angenähert | werden unverändert zurückgeschrieben, solange sie in Trendo nicht geändert werden; Objekte behalten ihre Nummer; fehlt ein Ziel-Objekt oder ändert man Animationen, schreibt Trendo seine eigenen (keine Animation zeigt ins Leere) | `Animationen_durchreichen`, `Uebergang_und_Hintergrund` |
| Importbericht | pptx.design, pptx.textformat, pptx.animation … | Design, Vorlagen-Elemente, Hintergrund, Textformate, kursiv/unterstrichen, Übergänge, Animationen werden nicht mehr gemeldet; gemeldet bleibt u. a. Animation an Objekten, die Trendo nicht übernimmt (Gruppen …), durchgestrichen/hochgestellt, Texteffekte, Links, Formen, Gruppen | `Einzelne_Verluste`, `Eigene_Dateien_ohne_Verlust` |

### Importbericht der Fremd-Testdateien (`tests/Daten/office/`)
| Datei | vorher (T-0682) | jetzt | stimmt das? |
|---|---|---|---|
| fremd-tabelle.xlsx | `xlsx.diagramm`, `xlsx.verbunden`, `xlsx.zahlenformat` | leer | ja: Tests prüfen in der gespeicherten Datei `mergeCell A1:C1`, Formatcode, Achsentitel, Farben, Legende; LibreOffice liest sie gleich zurück |
| fremd-praesentation.pptx | `pptx.animation`, `pptx.design`, `pptx.textformat` | leer | ja: Master/Layout/Theme bytegleich, `animRot`, `sz="2800" b="1"`, `C00000` in der Datei |
| fremd-tabelle.ods | `ods.format`, `ods.notiz`, `ods.verbunden` | leer | ja: fett, Farbe, Füllung, Notiz, Verbund im Dokument (`Odf_fremd`); ODS selbst kann Trendo weiter nicht schreiben (Kopie als XLSX, Schutz bleibt) |

Die T-0682-Schutztests (`OfficeImportUiTests`) brauchten eine Datei mit echtem Verlust: Ihre Kopie bekommt jetzt einen Makro-Teil (`xl/vbaProject.bin` usw.), so prüfen sie den Schutz weiter am echten Weg.

## Fremdprogramm
LibreOffice ist vorhanden (`%LOCALAPPDATA%\TrendoWerkzeuge\libreoffice\…\soffice.exe`, buildid 0229ac93…, siehe Rohlog) – entgegen T-0692 lauffähig. Beleg `reports/T-0701/` (`beleg.sh`, `rohlog.txt`, `sha256.txt`, `libreoffice/*`): Trendo speichert die bearbeitete Fremd-Tabelle (gemeinsame Formeln, B5 geändert) und die Fremd-Präsentation; LibreOffice macht daraus XLSX, ODP und PDF; Trendo liest LibreOffices XLSX zurück: Formel `=B5*C5` = 48, Verbund `A1:C1`, „1,235 kg“, Diagrammtitel. Die PDFs (Tabelle 2 Seiten, Folie 1 Seite) zeigen Formeln, Formate, Achsentitel, Farben, Legende und die Textformate. Dabei liefen auch die schon vorhandenen LibreOffice-Abgleiche (Tabelle C1, Präsentation, Text) echt – Tabelle C1 hatte zuerst einen echten Fehler aufgedeckt („/“ im Datumscode ist das Datumstrennzeichen des Landes; geschütztes Leerzeichen bei „0,00 %“), beides behoben.
Microsoft Excel/PowerPoint: NICHT GETESTET (nicht vorhanden).

## Oberfläche (im Bild geprüft, `TRENDO_OFFICE_OPEN` neu im Render-Werkzeug)
Tabelle: neue Gruppe „Ausrichtung“ (Links, Mitte, Rechts, Umbruch, Verbinden/Verbund lösen), „Kursiv“, „Unterstr.“, „Schriftfarbe“, „Eigenes“ (Formatcode), Rechtsklick-Menü im Raster, Strg+I/U; Auswahl und Rahmen in Gold statt Blau. Präsentation: Kursiv/Unterstr., Kachel „Design der Datei“, acht goldene Griffe, Reiter-Hervorhebung Gold statt Blau, Hinweistexte angepasst. Keine Funktion entfernt; die Startseiten (erster Schritt im Leerzustand) sind unverändert. Hinweis: Zustandsknöpfe (z. B. „Fett“ aktiv) sind wie bisher golden hinterlegt.

## Abnahmekriterien
1. Leere Vorlage → Ergebnis über die Oberfläche: VOLLWERTIG wie bisher (unverändert, nicht neu geprüft).
2. Fremddatei importieren, bearbeiten, exportieren, im Fremdprogramm öffnen: VOLLWERTIG für LibreOffice (Rohlog + SHA-256); Excel/PowerPoint NICHT GETESTET; Testdateien sind handgeschrieben, nicht aus Office.
3. Prozessabbruch ≤ 30 s: unverändert (Wiederherstellung T-0254), in diesem Paket NICHT GETESTET.
4. Claude/KI-Fachaufgabe im offenen Dokument: FEHLT in diesem Paket (nicht Auftrag).
5. Test vom Klick bis zur Datei: VOLLWERTIG (Strg+S im Fenster ohne Bildschirm → Bytes der Datei geprüft, beide Programme).
6. Bedienung: TEILWEISE – Tastenkürzel (Strg+I/U Tabelle, Strg+B/I/U im Folientext), Rechtsklick im Raster neu; Rechtsklick auf der Folie und Ziehen-und-Ablegen weiterhin nicht; Einfach/Profi unverändert.

## Was NICHT geprüft wurde / offene Punkte
- ODS schreiben (gibt es in Trendo nicht; ODS wird als XLSX-Kopie gespeichert).
- Matrixformeln, Füllmuster/Verläufe, Diagonalen, hoch/tief, Datenbeschriftungen und Achsenformate in Diagrammen, Diagramme mit mehreren Arten, Gruppen/Linien/Freiformen in Folien, Links im Folientext, durchgestrichener Folientext – weiter nicht übernommen und gemeldet.
- Designfarben mit lumMod/lumOff im Folientext werden als feste Farbe geschrieben (sieht gleich aus, folgt aber einem späteren Designwechsel nicht).
- Folien: Bilder/Logos des Masters werden erhalten, aber in Trendos Anzeige nicht gezeichnet; Formen aus Layout-Platzhaltern (lstStyle-Größen) erben nur die Größen des Masters.
- Ziehen an den neuen Griffen nur im Bild, nicht automatisiert; Drehen/Gruppieren gibt es weiter nicht.
- Große Fremddateien (Laufzeit), Excel/PowerPoint selbst.
- Volltest aller Testprojekte und Paket-Tests (Hauptsitzung).

## Tests (Kommandos und Zahlen)
`dotnet build Trendo.sln -c Release` → 0 Fehler.
Core: `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter` über GeldRechnungenTests, ImportTempoTests, OfficeConvertTests, SheetCalcTests, SheetChartPdfTests, SheetExtrasTests, SheetFilesTests, SheetFunctionTests, SheetPivotTests, SheetSeriesTests, SlideTests, T0255_OfficeZip, T0682_ImportBericht, T0692_TextEditorTests, T0701_TabellePraesentation, NoFixedUser…, Deck…, Sys10, SheetPrompt… → **Bestanden: Fehler 0, erfolgreich 171, übersprungen 2, gesamt 173** (die LibreOffice-Abgleiche liefen dabei echt). Beleg-Lauf (`reports/T-0701/rohlog.txt`): 23 von 23 bestanden.
App: `Trendo.App.Tests.exe -class …` – T0701OfficeUiTests 2/2, OfficeImportUiTests 5/5, T0228_TrendoTabelleUi 7/7, T0229_TrendoPraesentationUi 4/4, TabelleF3UiTests 2/2, OfficeRecoveryUiTests 1/1, Teil4BueroUiTests 4/4, StudiosBueroGeld_Ui 36/36, ClaudeOperate_Ui 3/3, ClaudePrograms_Ui 6/6, Modern_Ui 13/13, SPEC05_Ui 2/2, RoundSix_Ui 3/3, GeldProgramm_Ui 4/4, Assist_Ui 3/3 – jeweils Failed 0.

## Rust
Nicht eingesetzt: Erweiterung bestehender C#-Leser/-Schreiber, kein messbarer Engpass.

## Geänderte Dateien
- neu: `src/Trendo.Core/Office/SheetNumberFormat.cs`, `SheetStyles.cs`; `tests/Trendo.Core.Tests/T0701TabellePraesentationTests.cs`; `tests/Trendo.App.Tests/T0701OfficeUiTests.cs`; `reports/T-0701/*`; `tasks/T-0701-tabelle-praesentation*.md`
- geändert Core: `Office/SheetDoc.cs`, `SheetFiles.cs`, `SheetCalc.cs` (Anzeige mit Code), `SheetPdf.cs`, `SheetExtras.cs` (Duplikate behalten alles Aussehen), `Formula.cs` (`CopyRefs`), `SlideDoc.cs`, `SlideRead.cs`, `SlideFiles.cs`, `SlidePdf.cs`, `ImportCheck.Sheet.cs`, `ImportCheck.Slide.cs`
- geändert App: `MainWindow.Tabelle.cs`, `MainWindow.Praesentation.cs`, `SheetGridView.cs`, `SheetChartView.cs`, `SlideView.cs`
- geändert Werkzeug: `tools/Trendo.Render/Program.cs` (`TRENDO_OFFICE_OPEN`, `TRENDO_OFFICE_SELECT`)
- geänderte Tests: `ImportReportTests.cs` (Erwartungen T-0682 → jetzt erhalten), `ImportTempoTests.cs` (Diagramm ohne Legende = `Legend: "none"`), `OfficeImportUiTests.cs` (Kopie mit Makro-Teil)
