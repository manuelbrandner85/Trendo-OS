# Bericht T-0712: Vorhandene lokale Modelle sicher erkennen

**Zweig:** `claude/T-0712-modelle-erkennen` (vom Integrationszweig `claude/T-0685-b1-integration`) · **Stand:** 16.11.2026

## 1. Ursache (nur gelesen)
- Das laufende Ollama ist Trendos eigenes: `%LOCALAPPDATA%\TrendoWerkzeuge\ollama\ollama.exe serve`. Seine Modelle liegen in **`%LOCALAPPDATA%\TrendoWerkzeuge\model`** (9 Manifeste unter `manifests\registry.ollama.ai\library` und zusätzlich im neueren Format `manifests-v2\ollama.com\library`). `OLLAMA_MODELS` ist weder in der Prozess- noch in der Benutzer- noch in der Systemumgebung gesetzt, `~\.ollama\models` gibt es nicht. Trendo setzt `OLLAMA_MODELS` nur für den von ihm gestarteten Ollama-Prozess (`Vollpaket.ConfigureOllama`).
- `KiModel.ModelRoots(toolsRoot)` enthielt diesen Ordner schon – aber nur über `_deps.ToolsRoot`. Im **isolierten Testfenster** (`Ui.Open`, `opt.Isolated`) zeigt `_deps.ToolsRoot` absichtlich auf `<Testdaten>\werkzeuge`. Der T-0707-Echtlauf lief in diesem Fenster → nur `~\.ollama\models` (fehlt) und ein leerer Testordner → keine Modelle → qwen2.5:0.5b. Die installierte App (echter Werkzeugordner) hätte die Manifeste gefunden; Beleg: `modelle-dieser-pc.log`, Abschnitt 2 (nur Manifeste, dieselben Ordner wie der alte Code → 9 Modelle, qwen3:14b).
- Schwachstellen, die trotzdem bestanden: (a) Ollama mit eigenem Ordner (z. B. `OLLAMA_MODELS` nur in der Benutzerumgebung, Ollama aus einer anderen Quelle) wurde nicht gefunden; (b) eine Ollama-Version, die nur noch `manifests-v2` schreibt, wäre unsichtbar gewesen; (c) die Liste kam nie vom laufenden Ollama selbst.

## 2. Was jetzt gilt
- `KiModel.Discover(api, roots)`: **1.** das laufende Ollama (GET `/api/tags`, kennt genau die ladbaren Modelle, egal wo sie liegen); **2.** ist es nicht erreichbar, wirft es oder meldet es keine Modelle → Manifeste. Ergebnis mit Quelle und Erklärung (`ModelScan`).
- `KiModel.ModelRoots(toolsRoot, env?)`: `OLLAMA_MODELS` (Prozess) → `OLLAMA_MODELS` (Benutzer, nur gelesen, nur Windows) → `~\.ollama\models` → `<Werkzeuge>\model`; leere Werte und Doppelte fallen weg.
- `KiModel.ScanManifests` liest zusätzlich `manifests-v2/ollama.com/library` (gleiches Manifest-Format, geprüft an einer Datei dieses PCs); ein Modell zählt einmal.
- `KiModel.ParseTags(json)` liest die `/api/tags`-Antwort (Name, Größe); `OllamaBackend.InstalledAsync()` (vorhandener Client, 2 s Zeitgrenze) liefert sie, null wenn Ollama aus ist. Kein neuer Client, kein zweiter KI-Kern, keine Download-Logik.
- `MainWindow.InstalledModels`: Ersatz für Tests (`InstalledModelsOverride`) wie bisher; sonst `Discover` mit 30 s Zwischenspeicher. `RefreshKi` liest die Liste im Hintergrund neu (Oberfläche hält nicht an). Das isolierte Testfenster fragt das echte Ollama nur, wenn ein Test `OllamaTagsOverride` setzt – normale Tests bleiben unabhängig vom PC. Vorher gab es keinen Zwischenspeicher (jede Abfrage las alle Manifeste neu).

## 3. Nachweis
**Echt auf diesem PC** (`reports/T-0712/`, Benutzerpfade durch `C:\Users\<user>` ersetzt, `SHA256SUMS` geprüft mit `sha256sum -c`):
- `modelle-dieser-pc.log` (Core `KiModelT0712Tests.Real_this_pc`): Rohantwort von `/api/tags`, 31,9 GB Arbeitsspeicher, 11,9 GB Grafikspeicher (nvidia-smi). Quelle Ollama-API → 9 Modelle → **Standard qwen3:14b**. Nur Manifeste (als wäre Ollama aus) → 9 Modelle in `C:\Users\<user>\AppData\Local\TrendoWerkzeuge\model` → **qwen3:14b**.
- `app-weg.log` (App `KiModelT0712_Ui.Real_app_path`): `MainWindow.InstalledModels → KiModel.Discover → OllamaBackend.InstalledAsync` gegen das echte Ollama → Quelle `ollama-api`, **Standard qwen3:14b**. Ersetzt blieb hier nur, dass das Testfenster isoliert ist: die echte API-Abfrage wird über `OllamaTagsOverride` eingehängt, der Grafikspeicher über `VramOverride` (echter nvidia-smi-Wert).
- `rohlog-echtlauf.txt`, `rohlog-tests.txt`: Testausgaben.

**Tests** (`dotnet build Trendo.sln -c Release`: 0 Fehler):
| Klasse | Ergebnis |
|---|---|
| Core `KiModelT0712Tests` (6 neu + 1 Echtlauf) + `KiModelT0703Tests` (9) | 16/16 bestanden; Echtlauf 1/1 |
| App `KiModelT0712_Ui` (2 neu + 1 Echtlauf) | 3/3 |
| App `KiModelT0703_Ui` | 5/5 |
| App `KiLibraryUiTests` | 3/3 |
| App `AssistChain_Ui` | 6/6 |
| App `T0225_KiUi`, `OllamaContextTests`, `W2_Ui`, `CORE02_Backends`, `KiSourceSetupUiTests`, `X05_Ui` | 3/3, 1/1, 1/1, 2/2, 2/2, 1/1 |

Rot-Grün: Die neuen Tests kompilieren auf dem alten Code nicht (`Discover`, `ParseTags`, `ModelRoots(…, env)`, `OllamaTagsOverride` fehlten). Inhaltlich: `Manifests_v2` und `Manifests_when_ollama_off` (nur v2-Manifest) fänden mit dem alten `ScanManifests` nichts; der erste App-Echtlauf zeigte einen echten Fehler (Zwischenspeicher aus dem Fensterstart überdeckte den gesetzten Ersatz → 0,5B), behoben durch Leeren beim Setzen.

## Abnahmekriterien
1. Leere Vorlage → Ergebnis über die Oberfläche: **NICHT GETESTET** (Fehlerbehebung an der Modellwahl; die Oberfläche zeigt den Standard-Satz wie in T-0703).
2. Fremddatei/Export: **NICHT GETESTET** (nicht Teil des Pakets).
3. Prozessabbruch ≤ 30 s: **NICHT GETESTET** (nichts Neues gespeichert; Zwischenspeicher nur im Arbeitsspeicher).
4. KI-Fachaufgabe: **TEILWEISE** – echte Modellwahl qwen3:14b über die App-Kette belegt; eine Antwort wurde in diesem Paket nicht erneut erzeugt (T-0703/T-0707 belegen 17 + 25 = 42 bzw. drei Programme mit qwen3:14b).
5. Klick bis Datei: **NICHT GETESTET**.
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **NICHT GETESTET** (keine Oberflächenänderung).

## Nicht geprüft
- Ein Ollama, das nur über `OLLAMA_MODELS` in der Benutzerumgebung einen fremden Ordner nutzt, oder ein reines `manifests-v2`-Ollama – nur nachgebildet (auf diesem PC nicht vorhanden; nichts umkonfiguriert).
- Die installierte App selbst (Fenster nicht isoliert) wurde nicht gestartet; der App-Weg lief im Testfenster mit eingehängter echter API.
- Hängendes Ollama: die erste Abfrage beim Fensterstart wartet höchstens 3 s auf dem UI-Thread (nur mit nachgebildetem Ausfall gedacht, nicht gemessen).
- Kein Volltest (macht die Hauptsitzung).

## Offene Punkte
- `tools/echtlauf/Trendo.Echtlauf/Program.cs` liest weiter nur die Manifeste (`ScanManifests(ModelRoots(tools))`) – mit echtem Werkzeugordner korrekt, nicht angefasst.
- `AssistChain_Ui.Real_local_ki` (T-0707) liest `/api/tags` weiter selbst; könnte künftig `OllamaTagsOverride` nutzen.
- Die Größen aus `/api/tags` umfassen alle Schichten (ein paar KB mehr als die Gewichte im Manifest) – für die Regel unerheblich.

## Berührte Dateien
- geändert: `src/Trendo.Core/KiModel.cs`, `src/Trendo.App/KiBackends.cs` (neue Methode `InstalledAsync`), `src/Trendo.App/MainWindow.axaml.cs` (nur `InstalledModels`, `OllamaTagsOverride`, `LastModelScan`, erste Zeile von `RefreshKi`)
- neu: `tests/Trendo.Core.Tests/KiModelT0712Tests.cs`, `tests/Trendo.App.Tests/KiModelT0712UiTests.cs`, `reports/T-0712/**`, `tasks/T-0712-modelle-erkennen.md`, `tasks/T-0712-modelle-erkennen.bericht.md`
- nicht angefasst: `MainWindow.ProgramShell.cs`, Menüleiste, Hülle (T-0710/T-0711)

## Rust
Nicht eingesetzt – kleine Erweiterung des vorhandenen C#-Bestands, keine Rechenlast.
