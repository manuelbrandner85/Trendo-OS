# Bericht T-0222: Studio-Programme mit Trendo OS mitliefern

**Auftrag:** siehe `tasks/T-0222-programme-mitliefern.md` (Manuel, 2026-10-25: „Mitliefern + nachbauen“, Liste „alles gut“).
**Branch/Worktree:** `claude/T-02xx`, Worktree `integration`

## Ergebnis
- **Grundsatz:** GRUNDSATZ-01 hat eine Ergänzung vom 2026-10-25 (`requirements.yaml` → `principles[0].amendments`, gleich im Generator `scripts/generate_requirements.py`): Studio-Programme werden mitinstalliert statt geladen; nur nicht weitergebbare Programme bleiben verlinkt.
- **Vollpaket gebaut:** 20 Pakete, 4,25 GB, `artifacts/vollpaket/out/Programme/` (nicht im Repo). Gebaut mit `python tools/vollpaket/build.py` aus `tools/vollpaket/programme.json`. Alles kommt aus offiziellen Quellen, 16 davon mit der vom Hersteller veröffentlichten Prüfsumme. Bei SumatraPDF, Python, Tesseract und dem KI-Modell veröffentlicht der Hersteller keine; dort ist die Prüfsumme beim Laden berechnet und im Manifest festgehalten.
- **Inkscape:** Die zuerst genommene Adresse lieferte eine andere Datei als die veröffentlichte Prüfsumme. Die Prüfung hat sie verworfen; genommen ist jetzt der Link der Inkscape-Seite (Prüfsumme stimmt).
- **Tesseract:** Der Installer lädt Sprachen sonst erst beim Installieren. Darum liegt die deutsche Texterkennung (`deu.traineddata` aus tessdata_fast, Commit fest) im Paket.
- **Einrichten:** `Vollpaket.InstallMissing` (Core) richtet jedes Paket nur bei passender Prüfsumme im Werkzeugordner `%LOCALAPPDATA%\TrendoWerkzeuge\<paket>` ein – ohne Internet, ohne Admin. Nach dem Setup (Velopack-Erststart) läuft das automatisch, wenn das Vollpaket gefunden wird: Ordner neben dem Setup, Laufwerkswurzel/USB, `Downloads\TrendoOS*` oder `TRENDO_VOLLPAKET`.
- **Studios:** 21 Katalogeinträge stehen auf `DeliveryMode.Included`. Statt „Installieren“ heißt es „Aus dem Vollpaket einrichten“ (geht auch offline); winget wird für sie nie aufgerufen. Die mitgelieferte Fassung geht in der Prüfung vor älteren eigenen Installationen. VS Code ist durch VSCodium (MIT) ersetzt; adb kommt aus dem scrcpy-Paket. Das mitgelieferte KI-Modell nutzt Ollama über `OLLAMA_MODELS`, aber nur, wenn der Nutzer selbst keine Modelle hat.
- **Verlinkt geblieben:** Unreal, Chrome, Claude Code, Qwen2.5 3B (über Ollama nachladbar), Meshroom, Tor.

## Nachweis am PC
`reports/t0222-vollpaket/einrichten.log` (geschwärzt), `manifest.json`, `programme.SHA256SUMS`, `SHA256SUMS`:
- `TrendoOS.exe --install-deps vollpaket --offline --vollpaket <Programme> --werkzeuge %TEMP%\trendo-vp-werkzeuge` → `PASS 20 Pakete eingerichtet` in 2 min 52 s.
- Danach: alle 21 mitgelieferten Komponenten „Working“, mit der mitgelieferten Version (z. B. Blender 5.2.3 LTS, LibreOffice 26.2.4.2, Krita 6.0.4, Git 2.56.0, Python 3.13.17, COLMAP 4.2.1, OBS 33.0.0, VSCodium 1.140). `tesseract --list-langs` zeigt `deu`.
- Erster Lauf zeigte einen echten Fehler: Schon installierte Programme (Git, Python, LibreOffice, Krita in älteren Versionen) galten als „vorhanden“, das Vollpaket wurde für sie übersprungen. Behoben: Nach dem Setup werden immer die eigenen Kopien eingerichtet, und die Prüfung bevorzugt sie.
- Prüfordner danach gelöscht.

## Geänderte Dateien
`src/Trendo.Core/Vollpaket.cs` (neu), `src/Trendo.Core/Dependencies.cs`, `src/Trendo.Core/ToolCheck.cs`, `src/Trendo.Core/Fachprogramm.cs`, `src/Trendo.Core/PromptAction/ConnectedAdapters.cs`, `src/Trendo.App/VollpaketStart.cs` (neu), `src/Trendo.App/MainWindow.axaml.cs`, `src/Trendo.App/MainWindow.Setup.cs`, `src/Trendo.App/MainWindow.W1Apps.cs`, `src/Trendo.App/UninstallPrompt.cs`, `src/Trendo.App/Program.cs`, `tools/vollpaket/build.py` und `programme.json` (neu), `tests/Trendo.Core.Tests/VollpaketTests.cs` (neu, 5 Tests), sieben ältere Tests auf die neue Lieferart umgestellt (`PhotoScanTests`, `QpdfDepTests`, `Tests`, `W7PhoneLinkTests`, `FachprogrammTests`, `ConnectedPromptTests`), `requirements.yaml`, `scripts/generate_requirements.py`, `.gitattributes`.

## Testergebnis
`dotnet build Trendo.sln -c Release` → 0 Warnungen, 0 Fehler. `dotnet test Trendo.sln -c Release --no-build --blame-hang …` → Exitcode 0:

| Projekt | Bestanden | Übersprungen | Fehler | Gesamt |
|---|---:|---:|---:|---:|
| Trendo.Api.Tests | 15 | 0 | 0 | 15 |
| Trendo.Core.Tests | 678 | 1 | 0 | 679 |
| Trendo.App.Tests | 329 | 2 | 0 | 331 (29 min 2 s) |
| Pakettests | 8 | – | 0 | 8 |

Im Lauf davor schlugen 7 Oberflächentests fehl, weil sie noch winget-Aufrufe und den Text „Jetzt installieren?“ erwarteten; sie prüfen jetzt, dass für Mitgeliefertes nichts geladen wird und das Einrichten auch offline angeboten wird.

## Was NICHT geprüft wurde / offen
- **Kein neues Setup gebaut** (Release braucht Manuels OK). Darum ist die automatische Einrichtung direkt nach `Setup.exe` vom USB-Stick noch nicht am echten Setup getestet; geprüft ist derselbe Code über die Kommandozeile.
- „Ohne Internet“ ist mit `--offline` geprüft, nicht mit gezogenem Netzwerkkabel.
- GitHub erlaubt höchstens 2 GB je Release-Datei: Das Vollpaket kann nicht als ein Download dort liegen (Ollama allein 1,5 GB passt, das Ganze nicht). Verteilung als USB/ISO oder geteilter Ordner.
- Lizenztexte: Jedes Paket enthält die Lizenzdateien des Herstellers; eine gemeinsame Übersicht im Vollpaket fehlt noch.
- Linux- und Pi-Edition: eigener Auftrag.
