# Bericht T-0774: KI schreibt ins offene Dokument – PDF-Werkstatt und Dokumente scannen (Q1)

**Auftrag:** `tasks/T-0774-ki-dokument.md` (Freigabe 26.11.2026, Etappe 0.9.112) · **Branch:** `claude/T-0774-ki-dokument` (Basis main 0.9.111 = 2b1ffa923) · eigene Kopie in `%TEMP%\trendo-auftrag-T-0774`.

## 2. Nachbesserung (zweite Gegenprüfung)
| Nr. | Befund | Behebung | Test (rot auf 1703134a3 → grün) |
|---|---|---|---|
| H1 | Sperre verglich `NormalizePath(pfad).Split('/')[0]` ohne Trim – „ folder“ kam durch, CheckEdit/DocPath schrieben trotzdem | Gemeinsame Zerlegung `AssistChain.PathParts` (Trim, Tabs, leere Teile) für CheckEdit und die Sperre; Vergleich ohne Groß/klein | `Locked_path_variants` („ folder“, „folder “, Tab, „Folder“, „FOLDER“, „ fOlDeR⇥“, „/folder/“) |
| M1 | Aufräumen durchsuchte rekursiv den Ausgabeordner (außerhalb des try) und löschte auch fremde, gleichzeitig entstandene Dateien endgültig | `ProgramKiAction.Produce` bekommt eine Meldefunktion und meldet jede selbst angelegte Datei vor dem Schreiben (PDF-Werkstatt über `PwOut` und die Teile von „Teilen“, Scannen über `ScanSavePdf`). Bei einem Fehler werden genau diese nach „.rueckgaengig“ gelegt, nicht gelöscht. Kein Durchsuchen mehr, `OutDir`/`Listing` entfernt | `Foreign_file_survives_failed_apply` (fremde Datei entsteht während des Laufs, alter Stand löschte sie); `Failed_apply_leaves_no_files` prüft jetzt „beiseitegelegt statt gelöscht“ |
| N1 | Ausnahme aus `ChildWrite.DemandMove` in KiFilesStepped | Verschieben in `KiMoveAside` gekapselt: jede Ausnahme (außer Speicher) wird protokolliert und im Programm gemeldet, Rückgängig/Wiederholen werfen nie. Ehrlich: `UnauthorizedAccessException` wurde schon vorher abgefangen; der Test belegt den Fall „Datei gesperrt“ | `Undo_with_locked_file_does_not_throw` (auf dem alten Stand nur die Meldung anders) |
| N2 | `pdf.ausfuehren` fiel still aus dem Plan | Ursache: `AssistPlanner.FromKi` überging unbekannte Aktionen (z. B. „pdf.ausführen“) und unerlaubte Auswahlwerte (z. B. „Wasserzeichen“ statt „wasserzeichen“) ohne Spur – dann lief das gerade gewählte Werkzeug. Jetzt stehen beide unter „Nicht verstanden“; in Programmen mit Fachaktion läuft so ein Plan gar nicht, sondern endet mit deutlicher Meldung | `Run_without_tool_after_password_is_reported` (drei Fälle) |
| N3 | – | nur notiert, keine Änderung | – |

Rot-Lauf: `reports/T-0774/rot-gegenpruefung-2.txt` – 5 von 16 rot auf 1703134a3. Endstand: 15 bestanden, 1 übersprungen (Echtlauf ohne Variable). Der Echtlauf mit qwen3:14b lief neu, die Belege und `SHA256.txt` sind neu erzeugt.
Neu berührt: `src/Trendo.Core/Programs/AssistChain.cs` (`PathParts`).

## Nachbesserung nach der Gegenprüfung („nicht freigabefähig“)
| Nr. | Befund | Behebung | Regressionstest (rot auf 6588330f8 → grün) |
|---|---|---|---|
| 1 (hoch) | Mehrere Fachaktionen liefen auf dem Endstand („1-2“, dann „4-5“ → zweimal 4–5) | Höchstens eine Fachaktion je Auftrag, nur als letzter Schritt; jeder weitere Schritt bricht mit deutlicher Meldung ab („… höchstens eine neue Datei …, bitte den Auftrag aufteilen“). Die Fachaktion läuft damit genau auf dem Stand der Vorschau | `One_production_per_plan` |
| 2 (hoch) | Vorschau zeigte „Wasserzeichen“, danach `tool=passwort`/`pwMode=entfernen` → entsperrte Kopie | Folgt aus 1 (nichts nach der Fachaktion); zusätzlich prüft `PwKiAllowed` vor der Vorschau **und** beim Erzeugen am echten Dokument: KI-Pläne führen „Passwort“ (schützen/entfernen) nie aus | `No_password_tools_from_ki` (alter Stand erzeugte `…-entsperrt.pdf`) |
| 3 (hoch) | `KiFilesRestored` hing an `Restored` und verschob Dateien auch bei Vorschau-Zurückrollen (Textsuche) | Neues Ereignis `ProgramDocument.Stepped` nur bei echtem `Undo()`/`Redo()` (nie bei `Rollback`), Zuordnung über die Schrittnummer (`TopStep`), Wiederholen-Liste behält die Nummer | `Preview_rollback_never_moves_files` (Ergebnisliste leeren + neue Vorschau + „Nein“ → Datei bleibt) |
| 4 (mittel) | KI konnte `folder` (absolut/UNC) für Scans setzen | `ProgramSpec.KiLocked = ["folder"]` – `dokument.setzen` dort abgelehnt; Fotos-Ordner der KI: Netzwerkpfade abgelehnt | `Ki_cannot_set_scan_folder` |
| 5 (mittel) | Fehler beim Übernehmen ließ schon geschriebene Dateien liegen | (in der 2. Nachbesserung ersetzt, siehe M1) | `Failed_apply_leaves_no_files` (zweite PDF nach der Vorschau beschädigt) |
| 6 (mittel) | Wiederholen konnte gleichnamige fremde Datei treffen | Name belegt → nicht verschieben, Meldung im Programm, Protokoll `assist.datei.belegt` | `Redo_never_overwrites` |
| 7 (mittel) | `SHA256.txt` passte nicht zum Rohlog (Zeilenende-Umwandlung) | `.gitattributes`: `reports/T-0774/** -text`; Echtlauf neu, Rohlog nennt Befehl und Testnamen, Liste neu erzeugt | Prüfung `sha256sum -c` vor dem Commit |
| 8 (niedrig) | „3 bis Ende“/„3-“, Unbekanntes still verworfen | → `3-z`; unbekannte Wörter werden gemeldet | `Small_fixes` |
| 9 (niedrig) | Meldung blieb nach „Nein“ stehen | Vorbereitungen setzen keine Programm-Meldung mehr bzw. stellen sie wieder her | `Small_fixes` |
| 10 (niedrig) | Regel-Planer setzte Modus „foto“ bei „Fotos“ | `AssistParam.Words` gilt jetzt auch für Auswahl-Parameter (nur wenn angegeben, nur `scan.pdf` nutzt es): Modus nur bei „Modus …“ | `Small_fixes` |
| 11 (niedrig) | Echtlauf ohne Variable meldete „bestanden“ | `Assert.Skip` mit Grund | Testlauf zeigt „Skipped: 1“ |
| 12 (niedrig) | Verschieben ohne Kinderschutz-Prüfung | `ChildWrite.DemandMove` vor jedem Verschieben | – (Erwachsene unverändert) |
| 13 (niedrig) | Kommentar hing am falschen Typ | Doku von `ProgramFirstStep` wieder an seinem Typ, `ProgramKiAction` mit eigener Doku | – |

Rot-Lauf: `reports/T-0774/rot-gegenpruefung.txt` – neue Testdatei gegen den Code von 6588330f8: 7 von 12 rot (Echtlauf übersprungen). Grün auf dem Endstand: 11 bestanden, 1 übersprungen (Echtlauf ohne Variable), zweimal hintereinander.
Neu berührt: `src/Trendo.Core/Programs/ProgramDocument.cs` (Ereignis `Stepped`, `TopStep`), `src/Trendo.Core/Programs/Assist.cs` (Words bei Auswahl), `.gitattributes`.

## Was war → was ist
| Punkt | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| PDF-Werkstatt K4 | KI schrieb nur `pdf-auftrag.txt` (`BueroPdfPromptAdapters.cs:90`); die Kette kannte nur `dokument.setzen` (Einstellungen), „Ausführen“ (`pw-run`) war für die KI gesperrt (Filter `Risky`) | Fachaktionen `pdf.seiten_heraus` (Seiten wie „1-3“) und `pdf.ausfuehren` (Werkzeug) an der vorhandenen `AssistChain`: Vorschau bereitet nur vor und prüft die Seiten, „Ja, behalten“ ruft dieselbe Funktion wie der Knopf „Ausführen“ → neue PDF unter „Ergebnisse“, genau **ein** Rückgängig-Schritt „KI: …“ | `T0774KiDokument_Ui.Pdf_pages_out_into_open_program`, `Pdf_bad_pages_stop_in_preview` (KI ersetzt); Echtlauf qwen3:14b `Real_local_ki` |
| Dokumente scannen K4 | Adapter schrieb nur `scan-auftrag.txt` (`DeckScanPromptAdapters.cs:91`), „Als PDF ablegen“ für die KI gesperrt | Fachaktion `scan.pdf` „Fotos als PDF ablegen“ (Titel, Modus, optional Ordner mit Fotos) → dieselbe Ablage wie „Als PDF ablegen“ („Datum Titel.pdf“, Ordner nach Art), ein Rückgängig-Schritt | `Scan_photos_to_pdf_into_open_document` (KI ersetzt); Echtlauf qwen3:14b |
| Knopf „Scanner …“ | sah aus wie eine Scanner-Anbindung, war nur ein Hinweis | heißt „Hinweis: Papier-Scanner“, Text unverändert (WIA/TWAIN nicht angeschlossen, Weg über Fotos) | `Scanner_button_is_honest`; Bild geprüft (Profi-Stufe, Dunkel-Gold, Menüband „Ablegen“) |
| Rückgängig und Datei | – | nur echtes Rückgängig (Knopf oder Strg+Z) legt die von der KI erzeugte PDF in `.rueckgaengig` daneben (nichts gelöscht, nichts überschrieben), nur echtes Wiederholen holt sie zurück; Vorschau-Läufe bewegen keine Datei | Tests prüfen Datei weg/beiseite/zurück |

Gemeinsamer Baustein (kein neuer KI-Kern): `ProgramSpec.KiActions` (`ProgramKiAction` = Vorbereiten in der Vorschau + Erzeugen bei „Ja, behalten“) und `KiHint`; `ProgramAssistTarget` nimmt sie als Aktionen auf, `Session.Apply` fasst Daten + Fachaktion zu einem Schritt zusammen (bei Fehler wird alles zurückgerollt).
Nebenbefund behoben: Die Live-Vorschau beim Tippen lief nach einem Rückgängig noch einmal und leerte „Wiederholen“ – für Fachaktionen läuft sie jetzt nicht mehr (zeigt nur den Plan-Text). Für normale Menüband-Befehle besteht das Verhalten weiter (offener Punkt).

## Echtlauf mit lokaler KI (nichts installiert)
Ollama 127.0.0.1:11434 lief schon, Modell qwen3:14b (Auswahl nach T-0703). Bedienung über die Oberfläche (Vorlage, „Dateien/Fotos hinzufügen …“, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“).
- PDF: „Zieh aus der PDF die Seiten 1 bis 3 heraus …“ → KI-Plan `pdf.seiten_heraus {seiten: 1-3}` → `vertrag-teil1.pdf` mit 3 Seiten („Seite eins … drei“), Original-SHA unverändert, Rückgängig ok.
- Scan: „Mach aus den beiden Fotos eine PDF mit dem Titel „Rechnung Stadtwerke“ …“ → Plan `dokument.setzen title` + `scan.pdf` → `2026-11-26 Rechnung Stadtwerke.pdf` (2 Seiten) in `Scans/Rechnungen`, Rückgängig ok.
- Fremdprogramm: `pdftotext` (Xpdf 4.06 aus Git for Windows) liest beide PDFs (`fremdprogramm-pdftotext.log`). qpdf/LibreOffice nicht benutzt (qpdf nur in der echten Trendo-Installation, die nicht angefasst werden darf).
- Belege: `reports/T-0774/echtlauf-lokale-ki.md`, `rohlog-testlauf.txt` (1/1 bestanden, Exit 0), PDFs, `SHA256.txt`.
- **Ehrlich:** Der allererste Echtlauf scheiterte im Scan-Teil: Trendo wich nach einem Fehler von qwen3:14b auf `qwen2.5:0.5b` aus (Ausweichkette aus T-0703), das kleine Modell wählte `scan-add` statt `scan.pdf`. Der PDF-Teil bestand auch dort. Zwei weitere Läufe mit qwen3:14b bestanden vollständig (der zweite auf dem Endstand liegt als Beleg vor). Kleine Modelle bleiben für diese Aufgabe unzuverlässig.

## Rot-Grün
Neue Testklasse auf dem alten Stand (2b1ffa923, eigener Worktree, danach entfernt): 4 von 5 rot (`reports/T-0774/rot-alter-stand.txt`; „kein Plan aus der KI-Antwort“, Knopf „Scanner …“). Dafür wurden nur die zwei Zeilen mit den neuen Hilfen `PwPageSpec`/`KiUndoPlace` herausgenommen, weil sie auf altem Code nicht übersetzen. Auf dem neuen Stand 5/5 grün, dreimal hintereinander.

## Tests (gezielt, kein Volltest)
`dotnet build Trendo.sln -c Release`: 0 Fehler. App-Klassen (Total/Failed): T0774KiDokument_Ui 5/0 (3×), AssistChain_Ui 6/0, Assist_Ui 3/0, T0719_KartenTagesplaner_Ui 4/0, T0728_Bedienung_Ui 7/0, PdfWerkstatt_Ui 8/0, DocScan_Ui 4/0, BueroAusbau_Ui 12/0, GrossprojektUebergabe_Ui 7/0, BueroEchtlauf_Ui 6/0 (echtes LibreOffice/qpdf/Tesseract dieses PCs). Core unverändert, daher keine Core-Tests nötig. Volltest macht die Hauptsitzung.

## Abnahmekriterien (nur für diesen Querschnitt)
1. Aus leerer Vorlage echtes Ergebnis: VOLLWERTIG (unverändert, vorher schon belegt) – hier zusätzlich über die KI.
2. Fremddatei rein/raus: TEILWEISE für diesen Beleg – Ergebnis-PDFs mit pdftotext geöffnet; der Eingang ist eine von PDFsharp erzeugte PDF, kein Fremdprogramm-Import in diesem Paket (bestehender Beleg `BueroEchtlauf_Ui` grün).
3. Abbruch ≤ 30 s: NICHT GETESTET in diesem Paket (Programm-Hülle unverändert, `BueroAusbau_Ui` grün).
4. KI im offenen Dokument, rückgängig: **VOLLWERTIG** für PDF-Werkstatt und Dokumente scannen (Echtlauf lokale KI + Rückgängig).
5. Test vom Klick bis zur echten Datei: VOLLWERTIG (Klick auf Vorlage, „Dateien/Fotos hinzufügen“, „Los“, „Ja, behalten“ → PDF auf der Platte).
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: unverändert, NICHT GETESTET hier.

## Nicht geprüft / offene Punkte
- Claude (Abo) als KI in dieser Kette nicht gelaufen – nur die lokale KI.
- `pdf.ausfuehren` nur im Code und über die Vorbereitung abgedeckt, kein eigener Echtlauf.
- Die Projekt-Adapter `pdf.auftrag`/`scan.auftrag` (Weg ohne offenes Dokument) schreiben weiter TXT – unverändert gelassen.
- Live-Vorschau leert „Wiederholen“ bei normalen Menüband-Befehlen (älteres Verhalten der Kette).
- Die KI bekommt im Programm auch Befehle wie „Dateien hinzufügen …“ (Dateiauswahl) angeboten – älteres Verhalten.
- WIA/TWAIN-Anbindung bewusst nicht gebaut. Logos & Marken gehört zu T-0775.

## Geänderte Dateien
- `src/Trendo.App/MainWindow.ProgramShell.cs` (ProgramKiAction, KiActions, KiHint)
- `src/Trendo.App/MainWindow.Assist.cs` (Fachaktionen in der Kette, ein Rückgängig-Schritt, Datei bei Rückgängig/Wiederholen, Live-Vorschau)
- `src/Trendo.App/MainWindow.PdfWerkstatt.cs` (pdf.seiten_heraus, pdf.ausfuehren, PwPageSpec)
- `src/Trendo.App/MainWindow.DocScan.cs` (scan.pdf, Knopf umbenannt)
- `tests/Trendo.App.Tests/T0774KiDokumentUiTests.cs` (neu)
- `src/Trendo.Core/Programs/ProgramDocument.cs`, `src/Trendo.Core/Programs/Assist.cs`, `src/Trendo.Core/Programs/AssistChain.cs`, `.gitattributes` (Nachbesserungen)
- `docs/nachbau/pdf-werkstatt/anforderungen.md` (PW-14), `docs/nachbau/scannen/anforderungen.md` (SC-12, SC-15)
- `reports/T-0774/*`, `tasks/T-0774-ki-dokument.md`, `tasks/T-0774-ki-dokument.bericht.md`

**Status:** umgesetzt und gezielt getestet; Merge erst nach OK.
