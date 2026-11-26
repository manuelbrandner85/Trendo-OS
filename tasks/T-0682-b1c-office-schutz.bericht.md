# T-0682 B1c – Fremde Office-Dateien schützen: Bericht

**Auftrag:** Paket B1 „Datenverlust schließen“, Teil B1c (Audit vom 16.11.2026, Teilbericht 1, Abschnitt SYS-10).
**Branch/Worktree:** `claude/T-0682-b1c-office-schutz`, von `origin/main` (f68974c6d). Kein Push, kein Merge, kein Release.
**Status:** IMPLEMENTED, gezielt getestet. Kein Volltest (macht die Hauptsitzung), keine Sichtprüfung der Leiste im laufenden Fenster.

## Befund, gegen den Code auf origin/main geprüft

| Aussage des Audits | Ergebnis der Prüfung |
|---|---|
| Strg+S überschreibt das Original ohne Warnung | Bestätigt: `TextOpen` setzte `TextPath` auf die Originaldatei, Strg+S rief `TextSaveAs(TextPath …)`. Ebenso `SheetSaveClick` und `DeckSaveClick`. Mit abgeschaltetem Schutz schlagen die neuen Tests genau an der Stelle „Original bytegleich“ fehl (Gegenprobe, siehe unten). |
| Text: Tabellenzellen nur als reiner Text, keine verbundenen Zellen, keine Textfelder | Bestätigt (`DocxFormat.Read`: je Zelle nur `w:t`; `mc:AlternateContent`/`w:pict` werden nicht gelesen). |
| Tabelle: „keine Zahlenformate“ | Nur zum Teil richtig: Es gibt acht Zahlenformate, fett, Füllfarbe, Rahmen, Diagramme (Art, Bereich, Titel), bedingte Formate, Auswahllisten, Notizen. Verloren gehen andere Zahlenformate, verbundene Zellen, Schrift/Ausrichtung, Diagramm-Einzelheiten u. a. |
| Präsentation: ein Textformat je Objekt | Bestätigt (`ReadSp` nimmt das erste `a:rPr`). |
| Zusätzlich gefunden | Excel legt gleiche Formeln als „gemeinsame Formel“ ab; Trendo liest in den Folgezellen nur den Wert – die Formel ist nach dem Speichern weg. Wird jetzt als eigener Punkt gemeldet. Außerdem: Strg+S auf einer geöffneten ODS-Datei warf eine nicht abgefangene `ArgumentException` (Trendo Tabelle schreibt kein ODS) – jetzt abgefangen. |

## Was jetzt anders ist

### 1. Verlust erkennen – Importbericht am Dokument
- Neu `ImportReport`/`ImportLoss`/`ImportCheck` (`src/Trendo.Core/Office/ImportReport.cs`, `ImportCheck.Text.cs`, `.Sheet.cs`, `.Slide.cs`). `TextFiles.Open`, `SheetFiles.Open`, `SlideFiles.Open` hängen den Bericht an (`TextDoc.Import`, `SheetDoc.Import`, `SlideDoc.Import`): je Punkt eine feste Kennung, ein Text für den Nutzer, die Anzahl und – wo möglich – die Fundstelle („Tabelle 1“, „Tabelle1: A1:C1“, „Folie 1: „…““).
- Die Prüfung folgt den Lesern: Sie zählt, was der jeweilige Leser überspringt oder vereinfacht, dazu unbekannte Bausteine im Text/Blatt/auf der Folie und Paketteile, die kein Leser kennt (Makros, eingebettete Dateien, Pivot …).
- Erkannt werden u. a.:
  - **DOCX:** verbundene Zellen, Formatierung/Bilder/Listen/Umbrüche/Links in Tabellenzellen, verschachtelte Tabellen, Tabellenvorlagen, Textfelder, Formen, Diagramme, SmartArt, eingebettete Objekte, nicht übernommene und frei platzierte Bilder, Formeln, Felder, Formularfelder, unbekannte Formatvorlagen, Zeichenformate (Kapitälchen …), Absatzrahmen, Tabulatoren, Nummerierungsarten/-start, nachverfolgte Formatänderungen, Verfasser/Datum, mehrere Abschnitte, Papierformat, Kopf-/Fußzeile (eigene für erste/gerade Seiten, Tabelle, Bild, Textfeld, mehrere Zeilen, Felder), Makros, Kommentar-Antworten.
  - **ODT:** verbundene Zellen, Zellformat/Listen/Bilder in Zellen, Textrahmen, Formen, Objekte, Felder (der Text fehlt in Trendo ganz), nachverfolgte Änderungen, weitere Verzeichnisse, Absatzrahmen, Tabulatoren, mehrere Seitenvorlagen, Kopf-/Fußzeilen-Besonderheiten, Papierformat, Makros.
  - **XLSX:** verbundene Zellen, nicht genau bekannte Zahlenformate (mit Formatcode), Schrift, Füllmuster/Designfarben, Rahmenarten, Ausrichtung/Umbruch, gemeinsame Formeln (werden Werte), Matrixformeln, Zeilenhöhen, ausgeblendete Spalten/Blätter, Gliederung, Filtereinstellungen, Links, „Als Tabelle formatiert“, Druckkopf, Seitenumbrüche, nicht bekannte bedingte Formate und Eingaberegeln, Sparklines, Bilder, Formen, Diagrammart, Diagramm-Einzelheiten, nicht lesbare Diagramme, Diagrammblätter, benannte Bereiche, Druckbereiche, Mappenschutz, Mischformat in einer Zelle, Makros, Pivot, externe Verknüpfungen, Datenschnitte, Datenverbindungen.
  - **ODS:** verbundene Zellen, Aussehen der Zellen (Trendo liest aus ODS nur Inhalte), Zahlenformate, Notizen, Links, Diagramme/Objekte, Bilder, Formen, Ausgeblendetes, Blattschutz, Eingaberegeln, benannte Bereiche, Filter, bedingte Formate, Pivot.
  - **PPTX:** fremdes Design, Elemente der Folienvorlage, mehrere Textformate in einem Feld, kursiv/unterstrichen, Links, nummerierte Listen, Zeilenumbrüche im Absatz, Formen außer Rechteck/Ellipse/Pfeil, Formeffekte, Gruppen (fehlen ganz), Linien, Video/Ton, verknüpfte Bilder, Zuschnitt, verbundene Tabellenzellen, Tabellenvorlagen, weitere Datenreihen und Einzelheiten in Diagrammen, Diagrammart, SmartArt, eingebettete Objekte, Hintergrund mit Bild/Verlauf, ausgeblendete Folien, Übergänge, Animationen (nicht bekannte, ersetzte, Zeitablauf), Kommentare, Makros.
  - **ODP:** Design, Animationen (werden aus ODP gar nicht gelesen), Schriftformat in Textfeldern, Mischformat, Gruppen, Linien, Objekte, Diagramm-Einzelheiten, verbundene Tabellenzellen, Hintergrund.
- Lässt sich eine Datei nicht prüfen, steht genau das im Bericht („ließ sich nicht vollständig prüfen“) – der Schutz greift dann ebenfalls.
- Dateien aus Trendos eigenen Schreibern ergeben einen leeren Bericht (eigener Test über alle Vorlagen und alle Funktionen).

### 2. Original schützen
- Neu `src/Trendo.App/MainWindow.OfficeImport.cs`; eingehängt in `TextOpen/TextSaveAs`, `SheetOpen/SheetSaveAs`, `DeckOpen/DeckSaveAs` (die Wege, die auch Knöpfe, Menü, Strg+S und die KI nehmen). `…SaveAs` liefert jetzt `bool` (false = aufgehalten).
- Hat der Import etwas nicht übernommen, zeigt das Programm eine Hinweisleiste: „Diese Datei enthält Dinge, die Trendo nicht darstellt: … Beim Speichern in diese Datei gingen sie verloren.“ mit
  - **„Als Kopie speichern“** (hervorgehoben): schreibt `<name> (Trendo).<endung>` neben das Original (bei Bedarf mit Nummer; darf dort nicht geschrieben werden, in den Dokumente-Ordner) und arbeitet in der Kopie weiter;
  - **„Trotzdem überschreiben“**: fragt erst „Wirklich überschreiben? …“ mit „Abbrechen“ (hervorgehoben) und „Ja, überschreiben“. Erst nach „Ja“ wird das Original als `<name>.original.<endung>` daneben gesichert und dann überschrieben. Gelingt die Sicherung nicht, wird nicht überschrieben. Liegt dort schon eine andere Datei, entsteht `<name>.original 2.<endung>` – eine ältere Sicherung wird nie überschrieben.
  - **„Alles zeigen“**: die vollständige Liste mit Fundstellen.
- Strg+S, „Speichern“ und „Speichern unter“ auf die Originaldatei schreiben bis zur Entscheidung nichts; in der Statuszeile steht warum.
- ODS und ODP kann Trendo nicht zurückschreiben: Dort gibt es nur „Als Kopie speichern“ (XLSX bzw. PPTX).
- Nach Kopie oder bestätigtem Überschreiben ist die Datei Trendos eigene: kein Hinweis, Speichern wie bisher. Ohne erkannten Verlust ändert sich nichts.
- Jede Entscheidung steht im Audit-Log (`office.import.verlust`, `.kopie`, `.ueberschrieben`).
- Briefe: geprüft, kein Handlungsbedarf – die Brief-Programme öffnen keine fremden Textdateien zum Bearbeiten (`MainWindow.LetterPro.cs:109` liest nur eine Tabelle als Datenquelle, geschrieben wird immer in neue Dateien).

### 3. Testdateien mit echtem Inhalt
`tests/Daten/office/` – **Herkunft:** handgeschriebenes Roh-OOXML/-ODF, gebaut mit `erzeugen.py` (nur Python-Standardbibliothek, bytegleich wiederholbar). Nicht aus Trendos Schreibern, aber auch **nicht** mit Word oder LibreOffice erzeugt oder dort gegengeprüft (auf dem PC ist weder LibreOffice noch Microsoft Office vorhanden – geprüft, nichts installiert).

| Datei | Inhalt | SHA-256 |
|---|---|---|
| fremd-text.docx | Tabelle mit verbundenen Zellen (Spalten und Zeilen), Textfeld (wie Word: neue Form + VML-Rückfall), zweizeilige Kopfzeile, eigene Kopfzeile für die erste Seite | 886acb93a6b535ce76f75568f15fcb58103f94d0fb5d6d627cbd6609c9ed533e |
| fremd-tabelle.xlsx | Zahlenformat `0.000" kg"`, verbundene Zellen A1:C1, Säulendiagramm mit zwei farbigen Reihen und Achsentiteln | 7b3ddc2b149d11406f13e022cd1a862a5532871924fff0fa637e4470f3eac80b |
| fremd-praesentation.pptx | Textfeld mit zwei Textformaten (18 pt / 28 pt fett rot), eine Animation (Hervorheben „Drehen“), fremdes Design | 151179c9f62af5ee7e2dc3f593d559f3f30a9516dd2f75eed031bde823631080 |
| fremd-text.odt | verbundene Zellen, Textrahmen, Datumsfeld, einfache Kopfzeile | 37397a09f3babe8eb0978cf5893cf1a46724f72576d393e8cd206d289aabfb68 |
| fremd-tabelle.ods | verbundene Zellen, Zellformat, Notiz | 1dd736a07125eb3a9c8042998575295a409ef18c9837c709d66f114951acfae4 |
| fremd-praesentation.odp | Textformat im Textfeld, Animation | d37eaa51da5d3ee2b443167164e1859896d749530c370209b9fa51936ee21061 |

Zur Kopfzeile: Eine einfache einzeilige Kopfzeile übernimmt Trendo vollständig – sie steht deshalb zu Recht **nicht** im Bericht (in der ODT-Datei so geprüft). Die DOCX-Testdatei hat darum eine Kopfzeile mit Besonderheiten (zwei Zeilen, eigene erste Seite), die Trendo wirklich nicht hält.

## Tests

Kommandos: `dotnet build Trendo.sln -c Release` (0 Fehler), dann gezielt.

**Core** – `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter …` über `T0682_ImportBericht` und alle Klassen, die die geänderten Office-Dateien berühren (ImportTempoTests, OfficeConvertTests, Pilot_TextLook, SheetCalcTests, SheetChartPdfTests, SheetExtrasTests, SheetFilesTests, SheetFunctionTests, SheetPivotTests, SlideTests, T0223_TrendoText, T0255_OfficeZip, TextF3Tests, TextProTests, NoFixedUser…): **Fehler 0, erfolgreich 137, übersprungen 1, gesamt 138** (übersprungen: „T-0223 T8 Rechtschreibung“ – braucht ein Wörterbuch, unabhängig von diesem Auftrag). `T0682_ImportBericht` allein: 7 von 7.

**App** – `Trendo.App.Tests.exe -class …` für OfficeImportUiTests, OfficeRecoveryUiTests, T0223_TrendoTextUi, T0228_TrendoTabelleUi, T0229_TrendoPraesentationUi, T0230_TrendoTextProUi, TabelleF3UiTests, TextF3UiTests, TextAiProposalTests, Pilot_TextLookUi, WritingAid_Ui, ClaudeLiveSync_Ui, ClaudeLive_Ui, Teil4BueroUiTests, LetterF3UiTests, ClaudeChat_Ui: **Total 48, Failed 0, Skipped 0**. `OfficeImportUiTests` allein: 5 von 5.

| Punkt | Test | echt oder ersetzt |
|---|---|---|
| Bericht nennt genau die Punkte (DOCX/XLSX/PPTX, dazu ODT/ODS/ODP) | `T0682_ImportBericht.Docx_fremd`, `Xlsx_fremd`, `Pptx_fremd`, `Odf_fremd` – vergleichen die vollständige Liste der Kennungen, Anzahl und Fundstelle | echt: echte Dateien von der Platte durch die echten Leser |
| Der Bericht behauptet nichts | dieselben Tests: das Gemeldete fehlt im gelesenen Dokument und in der von Trendo geschriebenen Datei (kein `gridSpan`, `txbxContent`, `mergeCell`, `0.000`, `animRot` …) | echt |
| Kein falscher Alarm | `Eigene_Dateien_ohne_Verlust` (alle Vorlagen + alle Funktionen, DOCX/ODT/XLSX/PPTX in allen Designs); `Fremdprogramm_ohne_Verlust_kein_Alarm` mit zwei **echten LibreOffice-26.2.4-Dateien** aus `reports/t0223-office/text-abgleich/` und den beiden mit Word für Mac geschriebenen DIN-Briefvorlagen | echt |
| Weitere Verluste einzeln | `Einzelne_Verluste` (Makro-Teil, eingebettete Datei, Feld, Formatvorlage, Verfasser, gemeinsame Formel, benannter Bereich, Gruppe …) | Fremddatei im Test gezielt verändert (Roh-XML) |
| Strg+S ohne Bestätigung lässt das Original bytegleich | `OfficeImportUiTests.Text_…`, `Tabelle_…`, `Praesentation_…`: Datei öffnen, ändern, Strg+S als Tastendruck auf das Eingabefeld/Gitter/die Folie, dann Bytevergleich der Datei; zusätzlich Knopf „Speichern“ und direkter `…SaveAs` | echt im Programmfenster ohne Bildschirm (Avalonia Headless), echte Dateien |
| „Als Kopie“ schreibt die Kopie | dieselben Tests: `<name> (Trendo).<endung>` existiert, lässt sich öffnen, Original bytegleich, weiteres Strg+S geht in die Kopie | echt |
| „Trotzdem“ legt die `.original`-Sicherung an | `Text_Trotzdem_ueberschreiben_sichert_Original` u. a.: erster Klick schreibt nichts, „Abbrechen“ schreibt nichts, nach „Ja“ ist `<name>.original.<endung>` bytegleich das Original; weiteres Speichern legt keine zweite Sicherung an | echt |
| ODS/ODP, vorhandene ältere Sicherung, „Neu“ räumt den Hinweis weg | `Odf_und_vorhandene_Sicherung` | echt |

**Gegenprobe:** Mit testweise abgeschaltetem Schutz (`ImportGuardStops` immer false) schlagen vier der fünf App-Tests fehl, drei davon genau am Bytevergleich nach Strg+S (Zeilen 49, 116, 145). Strg+S hätte das Original also wirklich überschrieben; die Tests prüfen den echten Weg. Die Änderung wurde danach zurückgenommen.

## 4. Abgleich – was wirklich mit einem Fremdwerkzeug geprüft wurde
- **Kein Fremdwerkzeug vorhanden:** Auf diesem PC ist weder LibreOffice (`soffice`) noch Microsoft Office installiert (**Nachtrag T-0776:** für LibreOffice falsch – LibreOffice 26.2.4 liegt im Trendo-Werkzeugordner `%LOCALAPPDATA%\TrendoWerkzeuge\libreoffice`, `reports/T-0776/werkzeuge.txt`); `python-docx`, `openpyxl`, `python-pptx` ebenfalls nicht. Es wurde nichts installiert.
- **Keine neue Rundreise** mit Word, Excel, PowerPoint oder LibreOffice. Die sechs Testdateien hat kein Fremdprogramm geöffnet; ob Word/LibreOffice sie ohne Reparaturhinweis öffnen, ist **nicht geprüft**.
- **Einziger Bezug zu echten Fremdprogramm-Dateien:** Die Importprüfung lief über vier schon im Repo liegende Dateien – zwei von LibreOffice 26.2.4 geschrieben (`reports/t0223-office/text-abgleich/lo-aus-docx/probe.odt`, `lo-aus-odt/probe.docx`), zwei von Word für Mac (DIN-Briefvorlagen). Ergebnis: kein falscher Alarm. Das belegt nur „kein Fehlalarm bei bekannten Inhalten“, nicht die Vollständigkeit der Erkennung.
- Die Abgleichtests, die ohne LibreOffice still zurückkehren, wurden nicht angefasst (T-0681).

## Was NICHT geprüft wurde
- Volltest aller drei Testprojekte und Paket-Tests (Hauptsitzung).
- Aussehen der Hinweisleiste im echten Fenster (nur über die Bedienelemente im Headless-Lauf geprüft, kein Bildschirmfoto).
- Dateien aus echtem Word, Excel, PowerPoint: Der Bericht ist dort eine **Untergrenze**. Nicht erkannt wird z. B.: Tabellen ohne Rahmen (Trendo setzt Rahmen), Spaltenbreiten in Text-Tabellen, geänderte Standardschrift/Abstände der Vorlage „Standard“, Ausrichtung der Kopfzeile, Spaltenbreiten in ODS, Dokumenteigenschaften, Druckeinstellungen, Zellfarben in Folientabellen. Unbekannte Bausteine werden zwar gezählt, aber nur an den Stellen, an denen die Leser verzweigen.
- Sehr große Dateien: Die Prüfung liest die Datei ein zweites Mal (Blätter als Datenstrom). `ImportTempoTests` bleibt grün; eine eigene Zeitmessung gibt es nicht.
- Netzlaufwerke, schreibgeschützte Ordner (der Ausweich-Ordner für die Kopie ist nur gelesen, nicht getestet).

## Offene Punkte / Risiken
- Bei fremden PPTX erscheint der Hinweis praktisch immer (Trendo ersetzt jedes fremde Design durch sein eigenes). Das ist sachlich richtig, kann aber als viel empfunden werden – ändert sich erst mit dem Ausbau des Modells (Paket D1).
- Der Schutz hängt an der geöffneten Datei im laufenden Programm. Nach „Trotzdem überschreiben“ ist das Original nur noch als `.original`-Datei da; die räumt Trendo nicht selbst weg.
- `…SaveAs` gibt jetzt `bool` zurück; Aufrufer außerhalb des Bereichs wurden nicht geändert (sie ignorieren den Wert wie bisher; `MainWindow.ClaudeChat.cs`/`ClaudeLive.cs` rufen nur `…Open`).
- `SheetFiles.ChartXml` und `SlideFiles.ChartXml` sind jetzt `internal` (die Prüfung leitet daraus ab, was Trendos eigene Diagramme enthalten).

## Geänderte Dateien
- neu: `src/Trendo.Core/Office/ImportReport.cs`, `ImportCheck.Text.cs`, `ImportCheck.Sheet.cs`, `ImportCheck.Slide.cs`, `src/Trendo.App/MainWindow.OfficeImport.cs`
- geändert: `src/Trendo.Core/Office/TextDoc.cs`, `SheetDoc.cs`, `SlideDoc.cs` (Eigenschaft `Import`), `OdtFormat.cs` (`TextFiles.Open`), `SheetFiles.cs`, `SlideFiles.cs`; `src/Trendo.App/MainWindow.Text.cs`, `MainWindow.Tabelle.cs`, `MainWindow.Praesentation.cs`, `MainWindow.OfficeRecovery.cs`
- Tests: `tests/Trendo.Core.Tests/ImportReportTests.cs`, `tests/Trendo.App.Tests/OfficeImportUiTests.cs`, `tests/Daten/office/` (sechs Dateien + `erzeugen.py`)
- `tasks/T-0682-b1c-office-schutz.md`, dieser Bericht
