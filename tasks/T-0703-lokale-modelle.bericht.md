# Bericht T-0703: Lokale KI – bestes vorhandenes Modell als Standard, Bildverstehens-Modell eingerichtet

**Zweig:** `claude/T-0703-lokale-modelle` (vom Integrationszweig `claude/T-0685-b1-integration`) · **Stand:** 16.11.2026

## 1. Bestes Modell als Standard
**War:** `KiModel.Current` kannte nur `qwen2.5:0.5b` und `qwen2.5:7b`; der Echtlauf-Helfer fragte fest das 0,5B-Modell → „17 + 25 = 32“ (`reports/echtlauf/2026-11-16/ERGEBNIS.md`, ROT). Ein Modell, das nicht lädt oder hängt, brach die Frage ab.

**Ist:**
- `KiModel.Best/Choose` (Regel im Code dokumentiert, `src/Trendo.Core/KiModel.cs`): nur allgemeine Sprachmodelle (Programmieren, Bilder verstehen, Suche/Einbettung, Übersetzen ausgeschlossen – über die Art in der KI-Bibliothek und den Namen); passt zum PC: mit Grafikkarte ≥ 4 GB höchstens 85 % des Grafikspeichers, sonst ein Viertel des Arbeitsspeichers (max. 5 GB), nie mehr als die Hälfte des Arbeitsspeichers; unter den passenden das größte. Schwacher PC (< 15 GB, keine Grafikkarte ≥ 4 GB): das kleine Modell bleibt.
- Vorhandene Modelle kommen aus den Ollama-Manifesten auf der Platte (`ScanManifests`, auch ohne laufendes Ollama), Grafikspeicher einmal per nvidia-smi.
- Eigene Wahl hat Vorrang (`ki-model.txt`, jetzt jedes vorhandene allgemeine Modell, Knopf „… verwenden“), „Automatisch (empfohlen)“ hebt sie auf. Alte Wahl (0,5B aus dem Hardware-Ratschlag) gilt weiter.
- Ausweichen: `IntelligenceCore.LocalFallbacks/LocalFirstAnswerTimeout/LocalModelFailed` – lädt das große Modell nicht oder sagt es 90 s nichts, antwortet das nächstkleinere (höchstens zwei Ausweichmodelle: das größte kleinere und das kleinste). Nachdenken (`thinking` von qwen3) zählt als Lebenszeichen. Das Modell wird 7 Tage übersprungen (`ki/modell-ausweichen.json`, Audit `ki.model.fallback`/`ki.model.ausweichen`). Gilt für die Haupt-KI und „lokal“-Agenten; Abbruch durch die Person weicht nicht aus; ohne Ausweichliste unverändert.
- Echtlauf-Helfer fragt jetzt das automatisch gewählte Modell.

**Belege:** `KiModelT0703Tests` (Regel mit nachgebildeten Modelllisten, eigene Wahl, Ausweichen bei Ladefehler/Zeitgrenze, Nachdenken, Manifeste – Modell ERSETZT); **echt:** `reports/T-0703/ki-17plus25.md` (automatisch qwen3:14b, „17 + 25 = 42“, 8,6 s, über `Intel.AskAsync`) und `echtlauf-helfer-ollama.log` (qwen3:14b → 42).

## 2. Bildverstehens-Modell
**War:** kein Bildmodell in Ollama, lokale Beschreibung BLOCKIERT (T-0699). Der Ollama-Weg der KI-Bibliothek konnte gar nicht laden: die Freigabe wurde auf `m.Id` angelegt, aber gegen `m.Tag` ausgeführt → `ApprovalException` bei jedem Klick (im neuen Test aufgedeckt, behoben). Laden ohne Fortschritt und ohne Speicherprüfung.

**Ist:** Derselbe Einrichten-Weg (`KiLibraryInstall`, Freigabe `ki.pull`), jetzt mit Speicherprüfung (Modell + 5 GB Reserve), Fortschritt je Prozent in der Meldung und im Protokoll `ki/laden/<modell>.log`, Prüfsumme durch Ollama („verifying sha256 digest“), Audit `ki.model.pull.ok` mit Quelle, Lizenz und SHA-256 der Gewichte. Keine neue Download-Logik (Ollama `/api/pull` mit Streaming statt ohne).
- Modell: `qwen2.5vl:7b` (Ollama-Bibliothek), **Apache-2.0** – geprüft an der Lizenzschicht im offiziellen Manifest von registry.ollama.ai; 5 969 233 408 Bytes, SHA-256 der Gewichte `a99b7f834d754b88f122d865f32758ba9f0994a83f8363df2c1e71c17605a025`.
- Speicher vorher: 41 GB frei (> 15 GB). Wirklich geladen in 20 min (`reports/T-0703/vlm-laden.*`).
- „Bilder erkennen“ beschreibt das Testfoto lokal: „Im Bild ist ein Hund auf einer Veranda zu sehen, der neben einem roten Fahrrad sitzt …“ (`vlm-beschreibung.md`).
- Dabei gefunden und behoben: qwen2.5vl schreibt „Schlagworte: …“ hinter den letzten Satz statt in eine eigene Zeile → es kamen keine Schlagworte an. `VisionTags.ParseDescription` erkennt das jetzt (Test mit der wörtlichen Modellantwort). Nach dem Laden sucht „Bilder erkennen“ das Bildmodell neu (vorher erst nach Neustart).
- Grafikspeicher: vor dem Lauf kein Ollama-Modell geladen (`/api/ps` leer), nichts entladen oder beendet.

## 3. Oberfläche
Ein Satz „Standard für Chat und Agenten: <Modell>. <Grund>“ in KI-Quellen (`src-standard`), KI-Bibliothek bei Chat/Nachdenken (`kilib-standard`, Karte mit „· Standard“) und auf „Trendo KI (lokal)“ (mit „Automatisch (empfohlen)“ und je vorhandenem allgemeinen Modell „… verwenden“). Keine neue goldene Fläche (Auswahl mit ✓). Im Bild geprüft: `reports/T-0703/ki-quellen.png`, `ki-modell.png` (Render-Umgebung ohne Modelle → Satz für „noch kein Modell“).

## Tests
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core gezielt (KiModelT0703 9 neu, VisionVideoT0699, KiActivity, Model_choice, AgentRun, AgentAnswer, NoFixedUser): 46/46 bestanden.
- App je Klasse: siehe Tabelle unten.
- Rot-Grün: die neuen Tests kompilieren auf altem Code nicht (Methoden fehlten); `Library_pull_with_progress` deckte den Freigabe-Fehler auf; `Inline_tags_from_real_answer` schlug mit der echten Modellantwort vor der Korrektur fehl (Echtlauf zeigte leere Schlagworte).
- `KiLibraryUiTests.Hosts` läuft jetzt auf dem UI-Thread: als normaler `[Fact]` löste er je nach Testreihenfolge (hängt vom Ordnerpfad ab) die statische Initialisierung von `MainWindow` auf einem fremden Thread aus, danach scheiterten die beiden anderen Tests der Klasse („different thread owns it“). Gleicher Code in einem anderen Ordner: grün. Kein Test geschwächt.

| App-Testklasse | Ergebnis |
|---|---|
| KiModelT0703_Ui | 5 gesamt, 0 Fehler, 0 übersprungen |
| KiLibraryUiTests | 3 gesamt, 0 Fehler, 0 übersprungen |
| T0225_KiUi | 3 gesamt, 0 Fehler, 0 übersprungen |
| OllamaContextTests | 1 gesamt, 0 Fehler, 0 übersprungen |
| KiPy_Ui | 3 gesamt, 0 Fehler, 0 übersprungen |
| AgentRun_Ui | 5 gesamt, 0 Fehler, 0 übersprungen |
| W2_Ui | 1 gesamt, 0 Fehler, 0 übersprungen |
| CORE02_Backends | 2 gesamt, 0 Fehler, 0 übersprungen |
| X05_Ui | 1 gesamt, 0 Fehler, 0 übersprungen |
| VisionVideoT0699_Ui | 5 gesamt, 0 Fehler, 1 übersprungen |
| KiSourceSetupUiTests | 2 gesamt, 0 Fehler, 0 übersprungen |
| SimpleMode_Ui | 5 gesamt, 0 Fehler, 0 übersprungen |
| EveryButton_Alltag | 1 gesamt, 0 Fehler, 0 übersprungen |
| InstallFeedbackUiTests | 6 gesamt, 0 Fehler, 0 übersprungen |
| ProposalsUiTests | 3 gesamt, 0 Fehler, 0 übersprungen |
| StudioHeadingTests | 2 gesamt, 0 Fehler, 0 übersprungen |
| Design41RolloutTests | 3 gesamt, 0 Fehler, 0 übersprungen |
| ComponentsUi | 1 gesamt, 0 Fehler, 0 übersprungen |
| UebersichtEinfach_Ui | 2 gesamt, 2 Fehler, 0 übersprungen (schon auf dem Integrationszweig rot, siehe Offene Punkte) |
| Echtläufe (`KiModelT0703_Ui.Real_*` mit TRENDO_ECHT_KI / TRENDO_ECHT_VLM_PULL / TRENDO_ECHT_VLM) | je 1/1 bestanden (ohne Variablen kehren sie sofort zurück) |


## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis über die Oberfläche: **VOLLWERTIG** für „Bilder erkennen“ (leere Vorlage → „Ordner erkennen …“ → lokale Beschreibung mit Schlagworten); Modellwahl per Knopf (UI-Test).
2. Fremddatei importieren/exportieren, Export im Fremdprogramm: **NICHT GETESTET** (nicht Teil dieses Pakets; Foto als Fremddatei echt erkannt).
3. Prozessabbruch ≤ 30 s: **TEILWEISE** – Wahl, Ausweichliste atomar (`AtomicFile`) bzw. sofort gespeichert; ein abgebrochener Download setzt Ollama beim nächsten Klick fort (nicht eigens mit Abbruch geprüft).
4. KI führt Fachaufgabe aus, Ergebnis im Dokument, rückgängig: **VOLLWERTIG** für die Bildbeschreibung (landet als „Erkannt“ im Dokument, rückgängig über die Hülle – Weg aus T-0699, hier echt mit lokalem Modell); Chat-Antwort 42 echt.
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** – Laden (Weg des Knopfs, echte 5,97 GB) und „Ordner erkennen“ mit echtem Modell; Modellwahl-Knöpfe im UI-Test.
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **TEILWEISE** – Standard-Satz und Knöpfe in allen Stufen; keine neuen Kürzel/Rechtsklick.

## Nicht geprüft
- Ausweichen mit echtem Ollama (Ladefehler/Zeitgrenze nur mit ersetztem Modell geprüft; echt antwortete qwen3:14b in 8–19 s).
- Auswahl auf einem echten schwachen PC und ohne NVIDIA-Karte (nur nachgebildet; Grafikspeicher wird nur über nvidia-smi gemessen – AMD/Intel zählen als „keine Grafikkarte“ → Arbeitsspeicher-Regel).
- Oberfläche mit echten Modellen im Bild (Render-Umgebung isoliert); Fall qwen3:14b nur im UI-Test.
- Kein Volltest (macht die Hauptsitzung).

## Offene Punkte
- `UebersichtEinfach_Ui` (2 Tests: Seite „3d“ 43 Knöpfe, Comic-Pinsel in „Einfach“) schlägt schon auf dem Integrationszweig fehl – nicht von T-0703; KI-Quellen bleibt mit 37 Knöpfen unter 40.
- Qwen3 denkt vor jeder Antwort (hier 8–19 s für 17 + 25). Ein Schalter „schnell antworten“ (`think:false`) wäre ein eigener Vorschlag.
- AMD/Intel-Grafikspeicher messen (heute nur NVIDIA).
- Seite „Trendo KI (lokal)“: lange Download-Knöpfe in der schmalen Seitenspalte werden abgeschnitten (schon vorher so; ein Umbruch-Versuch zeigte im Bild keine Wirkung und wurde zurückgenommen).
- Bildmodell in „Bilder erkennen“: es wird weiter das erste vorhandene der KI-Bibliothek genommen (qwen2.5vl zuerst) – keine Größenregel wie beim Chat.

## Berührte Dateien
- geändert: `src/Trendo.Core/KiModel.cs`, `src/Trendo.Core/W2/IntelligenceCore.cs`, `src/Trendo.Core/Kreativ/VisionTags.cs` (nur `ParseDescription`), `src/Trendo.App/KiBackends.cs`, `src/Trendo.App/MainWindow.KiLibrary.cs`, `src/Trendo.App/MainWindow.X05.cs` (eine Zeile), `src/Trendo.App/MainWindow.axaml.cs` (Modellwahl/`KiModelChooser`), `src/Trendo.App/MainWindow.W2.cs` (eine Zeile), `src/Trendo.App/MainWindow.AgentRun.cs` (eine Zeile), `tools/echtlauf/Trendo.Echtlauf/Program.cs` (Fachlauf ollama), `tests/Trendo.App.Tests/KiLibraryUiTests.cs` (Hosts auf UI-Thread)
- neu: `tests/Trendo.Core.Tests/KiModelT0703Tests.cs`, `tests/Trendo.App.Tests/KiModelT0703UiTests.cs`, `reports/T-0703/**`, `tasks/T-0703-lokale-modelle.md`, `tasks/T-0703-lokale-modelle.bericht.md`
- nicht angefasst: `MainWindow.ProgramShell.cs`, Claude-Anbindung, `SimpleWords.cs`

## Rust
Nicht eingesetzt – kleine Erweiterung des vorhandenen C#-Bestands; die Rechenlast liegt in Ollama.
