# Bericht T-0739: KI-Modelle vergleichen (Fragenreihe) und Modelldateien mit Prüfsumme

**Zweig:** `claude/T-0739-modelle-vergleichen-pruefsumme` (Basis `main`, Sammelstand 0.9.104)
**Belege:** `reports/T-0739/` (Rohlogs, Exporte, Bild; SHA-256 in `reports/T-0739/SHA256SUMS.txt`, Ordner mit `-text`, damit die Summen gegen die Commit-Blobs stimmen)

## STUDIO-MODELS-01 „KI-Modelle vergleichen“ – Fragenreihe

**Vorher (audit-4):** nur Einzelfragen von Hand; keine Fragenliste aus Datei; kein Abbrechen; UI-Test nur mit Ersatz-Backends, kein belegter Echtlauf.

**Jetzt:**
- Karte „Fragenreihe“ und Menü „Fragenreihe“: eine Frage je Zeile eintragen oder „Fragen aus Datei …“ (CSV mit Spalte „Frage“/„question“/„prompt“ oder erste Spalte; Trenner `;` `,` Tab; Anführungszeichen und Zeilenumbrüche im Feld; TXT eine Frage je Zeile, `#` = Notiz; höchstens 200 Fragen, 1 MB).
- „Fragenreihe starten“: jede Frage geht nacheinander an jedes gewählte Modell – nur über den gemeinsamen Kern `IntelligenceCore.AskAsync`, auf je eine Quelle beschränkt (wie die Einzelfrage). Kein zweiter KI-Kern. Claude bleibt wie bisher nur als Quelle über das eigene Abo wählbar.
- Erreichbarkeit wird einmal vorab geprüft; ist keines der gewählten Modelle erreichbar: Meldung **„BLOCKIERT: Keines der gewählten Modelle ist erreichbar …“** mit Voraussetzung, nichts wird gefragt.
- Fortschritt: Fortschrittsbalken und Text „Frage 3 von 10 · Ollama · qwen2.5:3b · 5 von 20 erledigt“.
- „Abbrechen“ hält sofort an (auch mitten in einer Antwort), Erhaltenes bleibt; der Knopf heißt danach „Fragenreihe fortsetzen“ und fragt nur, was fehlt.
- Fehler einer Quelle stehen je Frage als Zeile (⚠ + Meldung), der Rest läuft weiter. Antwortet ein anderes Modell als gewählt, zählt das als Fehler.
- Tabelle je Frage und Modell: Antwort, Zeit, Zeichen, Fehler; Übersicht je Modell (Antworten, Fehler, Dauer, Zeichen, Zeichen/s). Tokens liefert der gemeinsame Kern nicht – darum ehrlich Zeichen (und Wörter im Export), keine geschätzten Tokens.
- Export „Fragenreihe (.csv)“ (Semikolon, UTF-8 mit BOM) und „Fragenreihe (.md)“.
- Jede Antwort steht sofort im Dokument (Notsicherung der Programm-Hülle verliert höchstens die laufende Frage); der ganze Durchgang ist **ein** Rückgängig-Schritt „Fragenreihe“ (die Antworten drängen frühere Schritte nicht aus dem Stapel).
- Neue Vorlage „Beispiel: Fragenreihe“ (fünf Alltagsfragen).

**Beleg (echt):** `ModelsBatchLive_Ui` gegen Ollama auf diesem PC mit `qwen2.5:1.5b` und `qwen2.5:3b` (beide waren schon geladen – nichts wurde geladen). Über die Oberfläche: TXT mit zehn Fragen importiert (ersetzt nur die Dateiauswahl des Systems), „Quellen suchen“ geklickt, zwei Modelle angehakt, „Fragenreihe starten“ geklickt → **20 von 20 Antworten**, Tabelle mit 20 Zeilen, Export `durchgang1.csv`/`.md`. Zweiter Durchgang (3 Fragen × 2 Modelle) nach der ersten Antwort „Abbrechen“ geklickt → **angehalten nach 90 ms, 1 Antwort behalten**; „Fragenreihe fortsetzen“ → 6 Antworten, keine doppelt gefragt (`durchgang2-abgebrochen-fortgesetzt.csv`). Rohlog `reports/T-0739/modelle/`.
Hinweis zur Messung: Während Durchgang 1 belegte ein anderer, paralleler Lauf Ollama dauerhaft mit `qwen3:14b` (10 GB Grafikspeicher). Die Zeiten der Fragen 1–4 (30–174 s) sind darum Wartezeit auf die Grafikkarte; ab Frage 5 lagen sie bei 0,1–0,4 s (wie im ersten, ungestörten Versuch um 18:49 mit 12,2 s für alle 20 Antworten). Die Werte sind ungeschönt im Log.

**Beleg (ersetzt):** `ModelsBatch_Ui` (Ersatzquellen): CSV-Import mit Kopfzeile und Semikolon im Feld, Fehler bei Frage 2 von Modell B steht als Zeile und Frage 3 läuft weiter, Übersicht und Tabelle, CSV/MD-Export, ein Rückgängig-Schritt mit Wiederholen; Abbrechen mitten in einer hängenden Antwort (< 3 s), Fortsetzen ohne Doppelfragen, BLOCKIERT ohne erreichbares Modell. `ModelBatchTests` (Core): Lesen TXT/CSV, Grenzen, Reihenfolge, Fortschritt, Fehler, Abbrechen/Fortsetzen, Export.

## X-05 „Kostenlose KI finden“ – feste Prüfsummen

**Vorher (audit-4):** Laufzeiten mit fester SHA-256, Modelldateien nur mit Größe; SHA nur, wenn Hugging Face sie im Kopf mitschickte. Eine veränderte Datei mit richtiger Größe wurde installiert (der alte `KiLibraryUiTests` installierte so Null-Bytes als Stimme).

**Jetzt:**
- `KiFile` hat `Sha256` und `ShaSource`. **Alle 18 Hugging-Face-Dateieinträge** im Katalog (16 verschiedene Dateien; zwei werden von zwei Modellen geteilt) haben eine feste SHA-256:
  - 14 große Dateien (16 Einträge): SHA-256 des Git-LFS-Objekts über die offizielle Hugging-Face-API `paths-info` (abgefragt 17.11.2026); die gemeldete Größe stimmte bei jeder Datei mit dem Katalog überein.
  - 2 kleine Begleitdateien (`*.onnx.json` der Stimmen) liegen ohne LFS: Summe selbst aus der offiziellen Datei berechnet (Stand `rhasspy/piper-voices` 4a409e3c), so im Katalog vermerkt.
- Nicht geraten: Die drei MediaPipe-Dateien der Video-Prüfung (Trendos eigene Ablage) bleiben ohne Summe; Python-KIs ohne Dateien (TripoSR, MMAudio, ACE-Step-Modelle …) ebenso. Die KI-Bibliothek zeigt je Modell ehrlich „✓ Prüfsumme: SHA-256 je Datei fest im Katalog“, „✓ Programmpaket mit fester SHA-256“, „Ollama kontrolliert jede Schicht selbst“ oder „⚠ ohne Prüfsumme – nur Größe wird geprüft“ (Herkunft als Hinweis beim Darüberfahren).
- `KiLibraryStore.InstallModelFile`: die Katalog-Summe entscheidet; nur ohne sie zählt die Server-Summe. Größe falsch, Summe falsch oder Server widerspricht dem Katalog → **Datei wird gelöscht, Meldung „… Prüfsumme passt nicht (Datei beschädigt oder verändert) – die Datei wurde verworfen, nichts installiert.“**, Audit `ki.model.file.rejected`.
- Beim erneuten Laden überspringt Trendo eine vorhandene Datei nur, wenn auch ihre SHA-256 stimmt (`ModelFileOk`), nicht mehr nur bei gleicher Größe.

**Beleg (echt):** `KiChecksumLive_Ui`: die echte Datei `de_DE-eva_k-x_low.onnx.json` (4 158 Bytes) über Trendos eigenen Ladeweg (`KiLibraryInstall` → `KiDownload`, erlaubte Server, Größenkontrolle) von huggingface.co geladen → SHA-256 `ef14b3dc…9625` = Katalog → installiert. Dieselben echten Bytes mit einem veränderten Byte (Byte 100 0x0a → 0x2a) → „Prüfsumme passt nicht … verworfen, nichts installiert“, keine installierte Datei, Download gelöscht. Rohlog `reports/T-0739/pruefsumme/`. Ersetzt im zweiten Teil: nur der Download (liefert die manipulierten Bytes) und die Piper-Laufzeitdatei (wird nicht gestartet).
**Beleg (ersetzt/Core):** `KiLibraryTests.Catalog_checksums`, `Catalog_sha_enforced` (manipuliert, Widerspruch Server/Katalog, richtig, nachträglich verändert); `KiLibraryUiTests.Install_try_remove` prüft jetzt zusätzlich, dass Ersatzdateien mit falschen Bytes abgelehnt werden.

## Rot-Grün
- Neue Tests (`ModelBatchTests`, `ModelsBatch_Ui`, `Catalog_checksums`, `Catalog_sha_enforced`, Live-Klassen) übersetzen auf altem Code nicht (Typen/Mitglieder fehlen).
- Verhaltensrot: Auf `main` installierte `KiLibraryUiTests.Install_try_remove` Null-Bytes als Stimme „Thorsten“ – jetzt wird genau das abgelehnt (der Test prüft es).
- Angepasste bestehende Tests (nicht geschwächt): `KiLibraryTests.Store_checks_files` prüft den Weg „Summe vom Server“ jetzt an einer Kopie ohne Katalog-Summe und zusätzlich, dass die abgelehnte Datei verworfen wird; `KiLibraryUiTests.Install_try_remove` führt Ausprobieren/Entfernen mit einer Kopie ohne Summe weiter, nachdem die Ablehnung geprüft ist.

## Abnahmekriterien
1. **Aus leerer Vorlage echtes Ergebnis:** VOLLWERTIG – leere Vorlage → Fragen importiert → echter Durchgang gegen Ollama → Tabelle und Exporte (Echtlauf).
2. **Fremddatei importieren/bearbeiten/exportieren, Export im Fremdprogramm:** TEILWEISE – CSV/TXT-Import und CSV/MD-Export echt; das Öffnen des CSV in einem Tabellenprogramm wurde nicht geprüft (nur Format: Semikolon, BOM, RFC-4180-Anführung).
3. **Prozessabbruch ≤ 30 s:** TEILWEISE – jede Antwort steht sofort im Dokument, die Notsicherung der Programm-Hülle greift; ein echter Prozessabbruch mitten in der Reihe wurde nicht getestet.
4. **Claude/KI führt Fachaufgabe aus, im Dokument, rückgängig:** VOLLWERTIG für die lokale KI (Ergebnis im offenen Dokument, ein Rückgängig-Schritt); Claude als Quelle nicht im Echtlauf (bewusst nur Ollama).
5. **Test vom Klick bis zur echten Datei:** VOLLWERTIG – Echtlauf klickt Import, Quellen suchen, Start, Abbrechen, Fortsetzen; Export-Dateien liegen unter `reports/T-0739/modelle/`. X-05: echter Download bis zur installierten, geprüften Datei.
6. **Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen und Ablegen:** TEILWEISE – Menü „Fragenreihe“ in der Hülle (damit über Befehlssuche erreichbar); kein eigenes Tastenkürzel, kein Rechtsklick, kein Ablegen einer Fragen-Datei auf die Fläche.

## Nicht geprüft
- Claude als Quelle in der Fragenreihe (kein Echtlauf; nur der bestehende Weg über das Abo).
- Mehr als 20 echte Antworten; Verhalten bei 200 × 12 Antworten (Darstellung zeigt höchstens 400 Zeilen, Export alles – nur im Code).
- Download der großen Modelldateien mit Katalog-Summe (nur die 4-KB-Datei echt; große Dateien nicht geladen, um nichts zu installieren).
- Öffnen der Exporte in LibreOffice/Excel.
- Volltest (macht die Hauptsitzung).

## Offene Punkte
- Automatische Vorbewertung (Soll-Antwort oder Richter-Modell), Zeit bis zum ersten Wort, Tokens/s – braucht Zählwerte aus dem Kern (heute nicht geliefert).
- Rechtsklick/Ablegen einer CSV auf die Arbeitsfläche, Tastenkürzel für Start/Abbrechen.
- MediaPipe-Dateien (eigene Ablage) brauchen eine belegte Summe aus Googles offizieller Quelle, dann im Katalog eintragen.
- Die Summen gelten für den Stand `main` der Hugging-Face-Projekte; ändert ein Anbieter die Datei, lehnt Trendo sie ab, bis der Katalog nachgezogen ist (gewollt, aber zu pflegen).

## Ausgeführte Tests (ohne Volltest)
- Build `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core (gefiltert: ModelBatchTests, KiLibraryTests, KreativKits3Tests, KiPyAppsTests, NoFixedUserTests, BasicPitchTests, WebDesignSeiteAnlegenTests): 45/45 bestanden.
- App-Klassen (alle, die die geänderten Dateien oder alle Programme berühren), bestanden: ModelsBatch_Ui 2/2, KreativStudios3_Ui 5/5, KiLibraryUiTests 3/3, KiModelT0703_Ui 5/5, KiPy_Ui 3/3, MusikUiTests 5/5, DesignPaket1_Ui 31/31, DesignPaket3_Ui 14/14, BueroStudios_Ui 5/5, ClaudeBridge_Ui 7/7, ClaudePrograms_Ui 6/6, ClaudeOperate_Ui 3/3, ClaudePolish_Ui 3/3, ClaudeLiveSync_Ui 3/3, ClaudeLive_Ui 3/3, GameDesignDoc_Ui 13/13, GameRelease_Ui 2/2, GameRobust_Ui 3/3, ProgramHeadDesign_Ui 6/6, SimpleMode_Ui 5/5, StudioUebergabe_Ui 4/4, T0710_MenuBar_Ui 8/8.
- **Ausnahme ClaudeRunFix_Ui 2/3:** Fall „Am Programm vorbei gearbeitet …“ (Studio CORE-06) scheitert mit „The calling thread cannot access this object …“. Gegenprobe auf unverändertem `main` (Änderungen weggestellt, neu gebaut): **derselbe Fehler** – nicht von T-0739, fremder Bereich, nicht angefasst.
- Echt: `ModelsBatchLive_Ui` 1/1 (766 s, davon ~12 min Warten auf die belegte Grafikkarte), `KiChecksumLive_Ui` 1/1.

## Berührte Dateien
- neu: `src/Trendo.Core/Kreativ/ModelBatch.cs`, `src/Trendo.App/MainWindow.ModelsBatch.cs`, `tests/Trendo.Core.Tests/ModelBatchTests.cs`, `tests/Trendo.App.Tests/ModelsBatchUiTests.cs`, `tests/Trendo.App.Tests/KiChecksumLiveUiTests.cs`, `reports/T-0739/**`, `tasks/T-0739-*.md`
- geändert: `src/Trendo.App/MainWindow.KreativStudios3.cs` (nur Abschnitt „KI-Modelle vergleichen“), `src/Trendo.Core/W2/KiLibrary.cs`, `src/Trendo.Core/W2/KiLibraryStore.cs`, `src/Trendo.App/MainWindow.KiLibrary.cs`, `tests/Trendo.Core.Tests/KiLibraryTests.cs`, `tests/Trendo.App.Tests/KiLibraryUiTests.cs`
- gemeinsam: `.gitattributes` (eine Zeile `reports/T-0739/** -text`)

## Gegenprüfung behoben
Vier Befunde der unabhängigen Gegenprüfung, jeweils Rot-Grün (neuer Test auf dem gepushten Stand rot, nach der Behebung grün).

1. **MITTEL – CSV-Export entschärfte keine Formeln** (`ModelBatch.Csv`, alter `ModelCompare.Csv`). Jetzt eine gemeinsame kleine Hilfe `KreativSafe.CsvCell`:
   Zellen, die mit `= + - @`, Tab oder CR beginnen, bekommen ein Hochkomma; reine Zahlen wie `-5` oder `+3,2` bleiben unverändert. Beide Exporte nutzen sie.
   Beleg: `ModelBatchTests.Export_csv_formula_guard` („- Karotte“, „=1+1“, „@SUMME(A1)“, „+Quelle“, Tab-Frage, `-5`, `+3,2`, dazu `ModelCompare.Csv`).
   Rot vorher: `Assert.StartsWith() Failure … Expected start: "1;'=1+1;Ollama|a;ja;"`.
2. **MITTEL – SHA-256 vorhandener Modelldateien lief auf dem UI-Faden** (`MainWindow.KiLibrary.cs`). Jetzt `await Task.Run(() => store.ModelFileOk(m, f, ct), ct)`,
   vorher die Anzeige „<Datei> wird geprüft … (SHA-256 der vorhandenen Datei)“ im Einrichtungsfenster. Neues `KiLibraryStore.ModelFileOk(m, f, ct)` hasht in
   1-MB-Blöcken und prüft das Token dazwischen – „Abbrechen“ greift auch mitten in einer 13,8-GB-Datei.
   Belege: `KiLibraryUiTests.Existing_file_hashed_off_ui_thread` (3-MB-Datei mit passender Katalog-Summe: kein Download, Prüfung auf anderem Faden, Anzeige „wird geprüft“) –
   rot vorher: `Assert.NotEqual() Failure: Values are equal (7)`; `KiLibraryTests.ModelFileOk_cancellable` – rot vorher: `Assert.ThrowsAny() Failure: No exception was thrown`.
3. **MITTEL – kein Fortsetzen nach Absturz** (`ModelsBatchRun`). Fortsetzen hängt nicht mehr am Merker `batchCancelled`: Liegen zur unveränderten Liste
   Antworten vor und fehlen noch Paare, heißt der Knopf „Fragenreihe fortsetzen“ und fragt nur das Fehlende. Daneben „Neu starten“ – fragt vorher in einem
   eigenen Fenster („Abbrechen“ vorbelegt); „Nein“ lässt alles stehen, „Ja“ fragt alles neu (ein Rückgängig-Schritt holt die alten Antworten zurück).
   Beleg: `ModelsBatch_Ui.Batch_resume_after_crash_and_restart_asks` (Dokument mit zwei Teilergebnissen ohne Merker → nur Frage 3 wird gefragt; Rückfrage „Nein“/„Ja“).
   Rot vorher: `Expected: Fragenreihe fortsetzen · Actual: Fragenreihe starten`.
4. **NIEDRIG – Rückgängig während des Laufs** hängte danach Antworten an den zurückgeholten Stand. Jetzt gesperrt (Strg+Z/Strg+Y, Knöpfe, Menü, Verlauf,
   Version zurückholen) mit Hinweis „Rückgängig ist gesperrt, solange die Fragenreihe läuft – erst „Abbrechen“ drücken …“. Zusätzlich trägt jeder Lauf eine
   Kennung im Dokument; wird sein Stand auf anderem Weg verdrängt, hält er an und schreibt nichts mehr hinein.
   Beleg: `ModelsBatch_Ui.Batch_undo_blocked_while_running` – rot vorher: `Assert.Contains() Failure: Sub-string not found` (Rückgängig griff mitten im Lauf).

Ersetzt blieb in allen vier Belegen die KI-Quelle bzw. der Download (Test-Ersatz); kein Ollama-Echtlauf in dieser Runde (Vorgabe). Oberfläche („Neu starten“, Hinweiszeile) nicht im Bild nachgeprüft – der Zustand „Teilergebnisse vorhanden“ lässt sich mit `Trendo.Render` nicht herstellen.

Tests dieser Runde (ohne Volltest): Build 0 Fehler. Core `ModelBatchTests|KiLibraryTests|KreativKits3Tests` 25/25. App: ModelsBatch_Ui 4/4, KiLibraryUiTests 4/4, KreativStudios3_Ui 5/5, KiModelT0703_Ui 5/5, KiPy_Ui 3/3, DesignPaket1_Ui 31/31, Runde12_Ui 4/4, UebersichtEinfach_Ui 2/2.

**Nebenbefund dieser Runde (vom ersten T-0739-Stand, in der ersten Runde nicht ausgeführt):** `UebersichtEinfach_Ui` war 0/2 –
(a) die neuen Exporte „Fragenreihe (.csv/.md)“ fehlten in „Einfach“ ohne genannten Ersatzweg → in `UebersichtEinfachUiTests.Replaced` eingetragen (Weg: Menüleiste „Datei ▸ Exportieren“, dort zeigt jede Stufe alle Exporte – wie bei den 0.9.104-Formaten);
(b) „KI-Bibliothek“ hatte in „Einfach“ 447 statt höchstens 400 Wörter wegen der neuen Prüfsummen-Zeile → in „Einfach“ jetzt Kurzform („✓ Prüfsumme fest“, „⚠ Ollama-Prüfsumme“, „⚠ ohne Prüfsumme“), voller Text und Herkunft im Hinweis beim Darüberfahren; „Erweitert/Profi“ unverändert. Jetzt 391 Wörter.

Zusätzlich berührt: `src/Trendo.Core/Kreativ/KreativKits.cs` (`KreativSafe.CsvCell`), `src/Trendo.Core/Kreativ/KreativKits3.cs` (eine Zeile), `src/Trendo.App/MainWindow.ProgramShell.cs` (Sperre an den Rückgängig-Wegen), `tests/Trendo.App.Tests/ModelsBatchUiTests.cs`, `tests/Trendo.App.Tests/UebersichtEinfachUiTests.cs` (zwei Ersatzweg-Einträge).

## ENTWICKLUNGSBERICHT
- Projekt: Trendo OS · Sprachen: C# (.NET 10, Avalonia) · Rust eingesetzt: NEIN
- Grund: Erweiterung bestehender C#-Module (IntelligenceCore, KiLibraryStore); Engpass ist die KI-Antwortzeit, nicht der eigene Code – Rust brächte keinen Vorteil.
