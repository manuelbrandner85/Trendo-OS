# T-0740 Bericht: Eigene KI trainieren nutzbar, KI-Anweisungen als Datei

**Auftrag:** `tasks/T-0740-training-anweisungen.md` (Restliste `d7-restliste.md` Rang 13 STUDIO-TRAIN-01 und Rang 15 CORE-12)
**Branch:** `claude/T-0740-training-anweisungen`, eigene Kopie von `main` (Stand Sammelstand 0.9.104) in `%TEMP%\trendo-auftrag-T-0740`
**Status:** umgesetzt, gezielt getestet, kein Volltest (macht die Hauptsitzung)

## Vorher → Nachher

### STUDIO-TRAIN-01 „Eigene KI trainieren“
- **Vorher:** Trainieren, Prüfen und „Ausprobieren“ eines einzelnen Textes; das Modell wurde sonst nirgends benutzt (audit-4, Teil A1).
- **Wie das Training heute läuft (geprüft):** `TextClassifier` in `src/Trendo.Core/Kreativ/KreativKits3.cs` – multinomiales Naive Bayes, Modell als JSON im Dokumentfeld `model`. Darauf baut der Stapel auf; kein zweites Lernsystem.
- **Nachher:**
  - `ClassifierBatch` (Core): CSV je Zeile (Kopfzeile und Textspalte werden erkannt, alle Spalten bleiben, dazu `Vorhersage`, `Sicherheit`, `Bekannte Wörter`), Ordner je Textdatei (.txt, .md, .eml, .csv, .log, .html, .json, .xml, mit Unterordnern), höchstens 50 000 Einträge, Fortschritt über `IProgress`, Abbruch über `CancellationToken`. Texte ohne bekanntes Wort bekommen keine geratene Kategorie. Zellen, die wie Formeln beginnen, werden mit `'` entschärft.
  - Oberfläche: Menüband-Gruppe „Anwenden“ (CSV einordnen …, Ordner einordnen …, Vorhersagen speichern …), Karte „Auf Dateien anwenden“ mit Fortschrittsbalken, „x von y“ und „Abbrechen“, Vorschau der ersten 12 Zeilen, Rechtsklick-Menü, Ziehen und Ablegen von CSV/Ordner auf die Arbeitsfläche, Menü „Modell“ mit F5 (Trainieren), Strg+E (CSV), Strg+Umschalt+E (Ordner).
  - Ergebnis liegt im Dokument (Rückgängig/Wiederholen, wird mit dem Dokument gesichert) und geht als Datei hinaus: „Vorhersagen speichern …“ bzw. Datei ▸ Exportieren ▸ „Vorhersagen (.csv)“ – UTF-8 mit BOM, Semikolon, atomar geschrieben.
  - Abbrechen übernimmt nichts (kein halbes Ergebnis).

### CORE-12 „KI-Anweisungen“
- **Vorher:** Formular (Name, Bereich, `TextBox`), kein Import/Export (audit-4, Teil B: Kriterien 4, 10, 11 FEHLT).
- **Nachher:**
  - Datei: „Als Datei …“ je Vorlage (`.md` mit Kopfzeilen `format/name/bereich/version` oder `.json`), „Alle speichern …“ (`.json`-Sammlung), „Aus Datei …“ und Ablegen auf der Seite (`.md`, `.txt`, `.json`; fremde Kopfzeilen `title`/`category` werden verstanden, fremdes `format` wird abgelehnt). Gleicher Name: Karte mit „Ersetzen“ (neue Version, alte bleibt unter „Frühere Versionen“), „Als Kopie (…)“, „Überspringen“.
  - Editor: AvaloniaEdit (derselbe Baustein wie im Code-Editor) statt `TextBox` – Rückgängig/Wiederholen im Feld (Strg+Z/Strg+Y und Knöpfe ↶ ↷), Lücken `{{…}}` golden hervorgehoben, ein offenes `{{` rot unterlegt, „Lücke einfügen“ als genau ein Rückgängig-Schritt, Rechtsklick-Menü. Entwurf, Rückgängig der Hülle und Notsicherung (T-0711) hängen weiter am Feld.
  - Speicherformat `prompts/vorlagen.json` unverändert; bestehende Vorlagen bleiben lesbar (Test `Import_conflict` liest die Datei mit einer neuen `PromptLibrary`).

## Belege
`reports/T-0740/BELEG.md` mit Rohlogs und `SHA256SUMS.txt`:
- Training aus der Beispielvorlage im Repo, Anwendung auf `reports/T-0740/daten/neue-texte.csv` (30 ungesehene Texte): **29 von 30 richtig** (unabhängig mit Python geprüft), Ordner mit 6 Dateien: 6 von 6.
- Vorlagen-Rundreise `.md` → geändert → Import → Ersetzen/Als Kopie; `.json`-Sammlung; fremde `.md` mit `title:`/`category:`.
- Bildprüfung (nicht im Repo): Arbeitsfläche „Eigene KI trainieren“ mit Karte „Auf Dateien anwenden“ und Vorschau, gerendert mit `tools/Trendo.Render` und dem Dokumentstand aus dem Testlauf (`TRENDO_PROGDATA`); Seite „KI-Anweisungen“ mit „Aus Datei …“ und „Alle speichern …“. Eine goldene Hauptaktion bleibt („Trainieren“). Der Editor selbst wurde **nicht** im Bild geprüft (Render-Werkzeug hat keinen Einstieg in den Vorlagen-Editor; nicht ergänzt, weil gemeinsame Datei).

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler
- `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter "FullyQualifiedName~T0740Tests|FullyQualifiedName~KreativKits3Tests|FullyQualifiedName~NoFixedUserTests|FullyQualifiedName~Runde11Tests|FullyQualifiedName~ClaudeExtrasTests"` → Bestanden, 52 von 52
- `Trendo.App.Tests.exe -class T0740_Ui -class KreativStudios3_Ui -class CORE12_Ui -class T0711_HuelleEtappe1b_Ui -class T0710_MenuBar_Ui` → Total 30, Failed 0 (letzter Stand)
- vorher mit Zwischenstand: `-class KreativStudios3_Ui -class CORE12_Ui -class W2_Ui -class Runde11_Ui -class T0710_MenuBar_Ui -class T0711_HuelleEtappe1b_Ui -class ProgramHeadDesign_Ui -class SimpleMode_Ui -class DesignPaket3_Ui` → Total 53, Failed 0
- Beleglauf `T0740_Ui` (5 Tests, Exitcode 0) und `pruefe.py` (Exitcode 0), siehe Rohlogs
- Rot-Grün: die neuen Tests nutzen `ClassifierBatch`, `PromptLibrary.ParseFile/Import/ToMarkdown`, `TrainApply` und den Editor `pedit-text` – auf dem alten Stand übersetzen sie nicht bzw. finden die Knöpfe nicht.

## Angepasste bestehende Tests (nicht abgeschwächt)
- `T0711HuelleEtappe1bUiTests.cs` (`Prompt_editor`): tippt jetzt mit echten Tastenereignissen in den Texteditor statt in eine `TextBox`; Ablauf (Rückgängig, Notsicherung, Zurückholen) unverändert.
- `W2UiTests.cs` (`CORE12_Ui`): setzt den Text im Editor statt in der `TextBox`.

## Offene Punkte / Risiken
- Excel/LibreOffice auf diesem PC nicht nutzbar → Öffnen der Exporte im Fremdprogramm **BLOCKIERT**; ersetzt durch Pythons `csv`/`json`.
- Ziehen und Ablegen: Handler eingebaut, im Test über denselben Einstieg (`TrainDropped`, `PromptImportFile`) geprüft, kein echtes Drag-Ereignis.
- Tastenkürzel: im Menü eingetragen und konfliktfrei geprüft, Tastendruck selbst nicht im Test.
- Abbruch des Prozesses während eines laufenden Stapels: der Lauf ist verloren (kein Teilergebnis), das Dokument selbst sichert die Hülle wie bisher; nicht eigens getestet.
- Audit-Wünsche außerhalb des Auftrags bleiben offen: Modell in einem anderen Programm nutzen, mehrere Aufteilungen, bessere Modelle, Tabelle > 300 Zeilen.
- Gemeinsame Dateien: `.gitattributes` (eine Zeile), `src/Trendo.App/MainWindow.KreativStudios3.cs` (nur Abschnitt Training), zwei bestehende Testdateien (siehe oben).

## Was NICHT geprüft wurde
Volltest aller drei Testprojekte und der Paket-Tests; Öffnen in Excel/LibreOffice; echte Maus-Drag-Ereignisse; Tastendruck der neuen Kürzel; Editor im gerenderten Bild; Linux/Pi.

## Geänderte Dateien
- neu: `src/Trendo.Core/Kreativ/ClassifierBatch.cs`, `src/Trendo.Core/W2/PromptFiles.cs`, `src/Trendo.App/MainWindow.TrainBatch.cs`, `src/Trendo.App/MainWindow.PromptFiles.cs`, `tests/Trendo.Core.Tests/T0740Tests.cs`, `tests/Trendo.App.Tests/T0740UiTests.cs`, `reports/T-0740/**`, `tasks/T-0740-training-anweisungen.md`, `tasks/T-0740-training-anweisungen.bericht.md`
- geändert: `src/Trendo.App/MainWindow.KreativStudios3.cs`, `src/Trendo.App/MainWindow.Prompts.cs`, `tests/Trendo.App.Tests/T0711HuelleEtappe1bUiTests.cs`, `tests/Trendo.App.Tests/W2UiTests.cs`, `.gitattributes`

## Gegenprüfung behoben (17.11.2026)
Unabhängige Gegenprüfung mit 8 Befunden; alle behoben, je mit Test. Rot-Probe: neue Tests gegen den alten Stand (eigener Worktree aus dem gepushten Commit `e2b6fbdf2`, nur die neue Testdatei dazu).

| # | Befund | Änderung | Test (Rot → Grün) |
|---|---|---|---|
| 1 HOCH | `UebersichtEinfach_Ui.Simple_keeps_every_function` rot: `prog-export-pred` fehlt in „Einfach“ | Ersatzweg in `Replaced` eingetragen: Menüleiste „Datei ▸ Exportieren ▸ Vorhersagen (.csv)“ (T-0710: dort in jeder Stufe alle Formate) und Knopf „Vorhersagen speichern …“ auf der Arbeitsfläche. Erreichbarkeit in „Einfach“ echt geprüft. | `UebersichtEinfach_Ui` vorher rot (Gegenprüfung), jetzt grün; `T0740Gegenpruefung_Ui.Simple_export_via_menu`: Stufe Einfach, kein Knopf `prog-export-pred`, Menüeintrag `menu-prog-export-pred` angeklickt → echte CSV mit BOM |
| 2 MITTEL | ganze Ergebnis-CSV im Dokument → je Änderung großer Rückgängig-Schnappschuss | Ergebnis liegt als Datei im Datenordner `ki-training/vorhersagen/<Prüfsumme>.csv` (UTF-8 mit BOM, `AtomicFile`, Name aus SHA-256, nie überschrieben). Im Dokument nur Verweis `batchFile`, Kennzahlen und Vorschau (Kopf + 12 Zeilen). Einordnen, CSV-Bau und Schreiben laufen im Hintergrund, nicht auf dem UI-Faden. Rückgängig nimmt den Verweis zurück, Wiederholen findet dieselbe Datei. Alte Dokumente mit `batch` bleiben lesbar und exportierbar. Fehlt die Datei (Dokument von einem anderen Rechner), sagt die Karte das und der Export meldet „bitte noch einmal einordnen“. Dateiname wird gegen `^[0-9a-f]{24}\.csv$` geprüft (kein Pfad aus fremden Dokumenten). | `Big_result_stays_out_of_document` (20 000 Zeilen): alt **2.363.678 Zeichen** Dokument → rot; neu < 64 KB, auch der Schnappschuss nach einer kleinen Änderung; Export hat alle 20 001 Zeilen; Rückgängig ×2 entfernt den Verweis, Wiederholen liefert byte-gleichen Export |
| 3 MITTEL | CSV und Ordnerdateien nur UTF-8 | `ClassifierBatch.ApplyCsvFile` und das Lesen der Ordnerdateien nutzen `Sys.TextFileCodec.Decode` (UTF-8 mit/ohne BOM, UTF-16 mit BOM, sonst Windows-1252); Kürzen nicht mitten im Zeichen (`KiAttachments.WholeChars`, nur auf `internal` gestellt) | Core `Folder_windows1252` (alt: „Gr��e aus K�ln“ → rot), App `Csv_windows1252` (alt: „Müller GmbH“ fehlt → rot) – je mit echter 1252-Datei |
| 4 MITTEL | über 50 000 still gekürzt | `Result` hat `Total` und `Truncated`; Meldung „⚠ Nur die ersten 50.000 von N Zeilen eingeordnet …“, Karte zeigt denselben Hinweis (`train-batch-truncated`), Protokoll nennt „von N“ | Core `Csv_truncated` (neu); App `Truncation_is_told` (alt: „✓ 50000 Zeilen eingeordnet“ → rot) |
| 5 NIEDRIG | Entschärfung auch bei -5; Tab/CR fehlten | `ClassifierBatch.Safe`: = + - @ Tab CR am Anfang → Hochkomma, außer reinen Zahlen (`-5`, `+3,2`, `1e5`, `12 %`) | Core `Csv_formula_guard` (alt: `'-5`, `\tx` → rot) |
| 6 NIEDRIG | CSV ohne Kopf, kurzes erstes Wort → erste Zeile verloren | Erkennung: kennt das Modell das erste Wort, ist es Inhalt. Dazu Schalter „Erste Zeile ist Überschrift“ (`train-batch-header`) auf der Karte, ordnet dieselbe Datei neu ein; `ApplyCsv(..., header:)` | Core `Csv_short_first_word` (alt: Kopf „Rechnung“ → rot); App `Header_switch` (alt: 2 statt 3 Zeilen → rot; Schalter → 2 Zeilen) |
| 7 NIEDRIG | alte Vorhersagen ohne Hinweis nach neuem Training | Fingerabdruck des Modells `batchModel` bei den Vorhersagen; weicht er ab: „⚠ Mit einem älteren Modell erstellt …“ (`train-batch-oldmodel`) | App `Old_model_hint` (alt → rot) |
| 8 NIEDRIG | Test-Regex ≠ Colorizer-Regex | Eine Regel: `PromptLibrary.PlaceholderSpans` (Core) nutzt dieselbe `VarRx` wie `Variables`/`Fill`; der Editor-Colorizer und `PromptPlaceholderSpans`/`PromptHighlightSpans` rufen sie auf (ganzer Text, je Textstand einmal) | App `Placeholder_rule_is_shared` (alt: `{{a.b}}` gold, obwohl Ausfüllen es nicht als Lücke kennt → rot) |

Rot-Probe App (alter Stand + neue Testklasse): Total 7, Failed 6 – der siebte (`Simple_export_via_menu`) ist auch alt grün, weil die Menüleiste das Format schon hatte; das Rot für Befund 1 war `UebersichtEinfach_Ui`. Rot-Probe Core: 3 von 3 neuen Tests rot (Kopf, Entschärfung, 1252); `Csv_truncated` übersetzt auf altem Stand nicht.

Ergebnisse nach der Behebung:
- `dotnet build Trendo.sln -c Release` → 0 Fehler
- Core `T0740Tests` → 12 von 12 bestanden
- App `-class T0740_Ui -class T0740Gegenpruefung_Ui -class UebersichtEinfach_Ui -class KreativStudios3_Ui -class T0711_HuelleEtappe1b_Ui -class W2_Ui -class CORE12_Ui -class T0710_MenuBar_Ui -class DeadButtonTests -class EveryButton_Alltag -class ProgramHeadDesign_Ui -class SimpleMode_Ui` → **Total 135, Failed 0**
- Belege: `T0740_Ui` mit `TRENDO_T0740_BELEG` neu laufen lassen – alle 6 Dateien unter `reports/T-0740/lauf/` byte-gleich; Belege und `SHA256SUMS.txt` deshalb unverändert.
- Bild: Karte „Auf Dateien anwenden“ mit allen drei Hinweisen und Schalter gerendert (`tools/Trendo.Render`, Dokumentstand aus dem Test), Hinweise in Warnfarbe, kein Blau, „Trainieren“ bleibt die einzige goldene Hauptaktion.
- Angepasst (nicht abgeschwächt): `T0740UiTests.cs` liest die Vorhersagen über `TrainBatchCsv(d)` statt aus dem Dokumentfeld `batch`.

Offen nach der Gegenprüfung:
- Die Ergebnisdateien im Datenordner werden nicht aufgeräumt (bewusst: Rückgängig/Wiederholen und gespeicherte Dokumente verweisen darauf). Bei sehr vielen Läufen wächst der Ordner; ein Aufräumen nach Alter wäre ein eigener Auftrag.
- Ein auf einen anderen Rechner kopiertes Dokument hat nur die Vorschau (ehrlicher Hinweis, Neu-Einordnen nötig).
- Kürzung über 50 000 für Ordner nur im Code, nicht mit 50 001 echten Dateien getestet (CSV getestet).
- Speicherbedarf beim Einlesen einer 20-MB-CSV selbst nicht gemessen (läuft im Hintergrund; die Rückgängig-Last ist behoben und gemessen).

Geänderte Dateien der Gegenprüfung: `src/Trendo.Core/Kreativ/ClassifierBatch.cs`, `src/Trendo.Core/W2/Prompts.cs`, `src/Trendo.Core/W2/KiAttachments.cs` (nur `internal`), `src/Trendo.App/MainWindow.TrainBatch.cs`, `src/Trendo.App/MainWindow.PromptFiles.cs`, `tests/Trendo.Core.Tests/T0740Tests.cs`, `tests/Trendo.App.Tests/T0740UiTests.cs`, `tests/Trendo.App.Tests/T0740GegenpruefungUiTests.cs` (neu), `tests/Trendo.App.Tests/UebersichtEinfachUiTests.cs`, dieser Bericht.

## ENTWICKLUNGSBERICHT
- Projekt: Trendo OS · Sprachen: C# (.NET 10, Avalonia 12, AvaloniaEdit), Python nur für die Belegprüfung · Rust eingesetzt: NEIN
- Grund der Sprachwahl: bestehende C#-Codebasis; der Stapel ist Datei-Lesen plus Wortzählen mit dem vorhandenen C#-Klassifikator; eine eigene Leistungsmessung wurde nicht gemacht, ein Rust-Baustein brächte eine zweite Sprache in den Kern ohne belegten Vorteil.
- Tests: siehe oben · Gemessene Verbesserungen: keine Leistungsmessung nötig · Offene Probleme: siehe „Offene Punkte“.
