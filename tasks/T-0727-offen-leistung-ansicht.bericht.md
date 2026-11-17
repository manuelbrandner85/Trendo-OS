# Bericht T-0727 – Offene Probleme Gruppe B: Leistung und Ansicht

**Branch:** `claude/T-0727-offen-leistung-ansicht` (vom Integrationszweig `claude/T-0685-b1-integration`, eigene Kopie unter `%TEMP%\trendo-auftrag-T-0727`)
**Status:** B2, B3, B5, B6, B7 geändert und geprüft; B1 und B4 Ursache gemessen, keine risikofreie Behebung möglich (ehrlich offen). Kein Volltest (macht die Hauptsitzung).
Zahlen, Rohlogs und Bilder: `reports/T-0727/README.md` (+ `SHA256SUMS`).

## Je Punkt: Ursache (Messung) → Behebung → vorher/nachher → Urteil
| Punkt | Ursache (Messung) | Behebung | vorher → nachher | Urteil |
|---|---|---|---|---|
| B1 Kaltstart | Zeit liegt vor `Main` (kalt 2,4–7,5 s, warm 0,1 s) und beim ersten Laden von Avalonia/Skia; gleicher Inhalt in neuem Ordner wieder kalt; Init-Wellen kalt wie warm < 40 ms → erstes Laden frisch geschriebener, unsignierter Dateien durch Windows/Echtzeitschutz | keine (kein risikofreier Hebel im Programm); ReadyToRun bleibt – ohne R2R kalt 5,5–10,3 s statt 4,9–5,1 s, warm 2,4–2,7 s statt 1,2–1,6 s | unverändert | Ursache belegt; Hebel: Codesignatur (offen) |
| B2 3D-Aufbau 40 000 Dreiecke | dotnet-trace: JSON des ganzen Netzes als Cache-Schlüssel (`PartSolid`, `PreviewBytes`), je Fläche neuer `SolidColorBrush`/`Pen`, Kanten je neuer Ansicht neu | `PartKey` (Werte + SHA-256 des Netzes), unveränderliche Pinsel/Stifte, Kanten schwach je Körper gemerkt | Median **3 273 → 1 906 ms** bis Ansicht steht (5 Läufe je Stand, abwechselnd) | verbessert, 3D-Bild pixelgleich |
| B3 3D-Höhe | Programmfenster 1366×768 ≈ 150 px, 1600×900 ≈ 180 px Ansicht, Seitenleisten abgeschnitten | Seite füllt die Höhe, wird aber verschiebbar, statt die Ansicht unter 200 px zu drücken (Grenze aus vorhandener Abnahme `W4_Core05_Ui`) | sichtbar ≥ 200 px, alles erreichbar; maximiert und ≥ 200 px unverändert | verbessert; ganz gelöst erst mit kompaktem Kopf (Audit S-01/V-01) |
| B4 Überblendung | beide Stücke ab Schlüsselbild dekodieren: Stück b allein 294 ms, beide ohne Effekte 357 ms, gesamt 433 ms; HW-Dekodieren langsamer (d3d11va 847, cuda 626 ms); Umstellen/Threads/PNG ohne Gewinn | keine | unverändert: 0,43 s unter Last, 0,32–0,36 s auf ruhigem PC | Ziel 0,3 s knapp verfehlt; ohne andere Pixel kein Hebel gefunden |
| B5 Mischer ohne Demucs | `MsStemsPanel` endete nach „Einrichten“ | Mischer + Stimme, wenn die Spuren vorliegen; „Einrichten“ nur fürs Neu-Trennen | rot → grün | behoben |
| B6 Modellwahl Test/Render | App: Ollama (/api/tags) → Manifeste in echten Ordnern; 0,5B nur ohne jedes allgemeine Modell (richtig). Isoliertes Testfenster kennt absichtlich keine Modelle | `Ui.UseRealModels(w)` (Weg der App) für Echtläufe; Assist- und Code-KI-Echtlauf umgestellt | echt: 9 Modelle → qwen3:14b über den App-Weg | sauber gemacht |
| B7 `ClaudeOperate_Ui.Ribbon_and_context` | Test griff unsichtbaren Text eines geschlossenen Untermenüs (Menüleiste T-0710); Ereignis erreichte das Fenster nie | Test nimmt nur Text, der im Fenster hängt, keine Menüeinträge | 2/3 → 3/3 | Test falsch, Code richtig |

## Geänderte/neue Dateien
- `src/Trendo.App/CadView.cs` – unveränderliche Pinsel/Stifte, Kanten-Cache je Körper (B2)
- `src/Trendo.App/MainWindow.Studio3DViewport.cs` – `PartKey`, `MinFill`/`MinFillHost`, `MinStage3D` (B2, B3)
- `src/Trendo.App/MainWindow.W4Studio3D.cs` – Vorschau-Schlüssel ohne ganzes Netz, Seite in `MinFill` (B2, B3)
- `src/Trendo.App/MainWindow.MusikSpuren.cs` – Mischer ohne Demucs (B5)
- `tools/Trendo.Render/Program.cs` – `TRENDO_3D_OBJ=<datei>` liest eine 3D-Datei ein (Bildvergleich)
- Tests: neu `tests/Trendo.App.Tests/T0727OffenUiTests.cs`; geändert `UiTests.cs` (`Ui.UseRealModels`), `AssistChainUiTests.cs`, `CodeAiKiUiTests.cs` (Echtläufe über den App-Weg), `ClaudeOperateUiTests.cs` (B7), `Studio3DViewportUiTests.cs` (Hilfe `ShowStage`: erst zur Ansicht scrollen, Prüfungen unverändert)
- `reports/T-0727/**`, `tasks/T-0727-offen-leistung-ansicht.md`, dieser Bericht

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler
- App, Pflichtklassen in einem Lauf (`Trendo.App.Tests.exe -class …` 46 Klassen): siehe „Testergebnis“ unten
- Core: `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter "Core_works_offline|Blocked_network_core_still_works|KiModelT0712Tests|KiModelT0703Tests|VideoLookTests|VideoBaselineTests"` → Bestanden, 23/23 (QA-019 beide Tests grün, nicht als „bestanden“ markiert)
- Funktions-Baseline `python -m unittest discover -s tests/claude_handoff` → 11 Tests OK
- Rot-Grün: `T0727_Offen_Ui` auf altem Code 4/4 rot („Mischer fehlt“, „Ansicht nur 4 px / 90 px hoch“), neu grün; `ClaudeOperate_Ui` vorher 2/3, jetzt 3/3

## Testergebnis
| Lauf | Ergebnis |
|---|---|
| App, 46 Pflicht- und berührte Klassen in einem Lauf (Studio3DViewport_Ui, Studio3DMeshEditUiTests, Library3D_Ui, FilmSchnitt_Ui, VideoSchnitt*/VideoLook_Ui, alle Musik-Klassen, ClaudeOperate_Ui, KiModelT0703_Ui, KiModelT0712_Ui, LeistungT0724Ui, UebersichtEinfach_Ui, ProgramHeadDesign_Ui, W4_Core05_Ui, Studio3DPro_Ui, Claude3D_Ui, T0715_HuelleE3_Ui, AssistChain_Ui, CodeAiKi_Ui, T0727_Offen_Ui) | Total 146, Failed 1, Skipped 1 (`reports/T-0727/tests-app-pflichtklassen.log`) |
| derselbe Lauf auf dem **alten Stand** (ohne T0727-Klasse) | Total 141, Failed 2: `FilmSchnitt_Ui.Render_click_to_file_real` und `ClaudeOperate_Ui.Ribbon_and_context` (`tests-app-pflichtklassen-alter-stand.log`) |
| `FilmSchnitt_Ui` allein (neuer Stand) | 5/5 grün |
| 3D-Klassen nach der letzten Änderung (T0727_Offen_Ui, Studio3DViewport_Ui, W4_Core05_Ui; davor dazu Studio3DMeshEditUiTests, Library3D_Ui, Studio3DPro_Ui, Claude3D_Ui, LeistungT0724Ui) | 13/13 bzw. 27/27 grün |
| Core (QA-019, KiModel T0703/T0712, VideoLook, VideoBaseline) | 23/23 (`tests-core.log`) |
| Funktions-Baseline `tests/claude_handoff` | 11 OK (`tests-funktions-baseline.log`) |

Der eine rote Test im Sammellauf (`FilmSchnitt_Ui.Render_click_to_file_real`: „nicht sichtbar: prog-cmd-film-import-music“) ist auf dem alten Stand im selben Lauf ebenso rot und allein grün – nicht von diesem Paket verursacht, nicht angefasst.

## Abnahmekriterien (Brief)
1. Leere Vorlage → echtes Ergebnis: NICHT GETESTET (Fehlerbehebungen, kein neues Fachergebnis)
2. Fremddatei importieren/exportieren, im Fremdprogramm öffnen: TEILWEISE – echte OBJ (40 000 Dreiecke) über die Oberfläche eingelesen und gezeigt; Export nicht Teil des Pakets
3. Prozessabbruch ≤ 30 s: NICHT GETESTET (nichts am Speichern geändert; Spuren-Mischer nutzt weiter die Hülle aus T-0715)
4. KI-Fachaufgabe im Dokument: NICHT GETESTET – B6 belegt nur die echte Modellwahl (qwen3:14b) über den App-Weg
5. Klick bis echte Datei: TEILWEISE – Spuren-Mischer ohne Demucs: Stumm-Klick → „Als Lied speichern“ mischt eine echte WAV aus den Spur-Dateien (Demucs selbst ersetzt)
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: TEILWEISE – 3D-Seite im Bild in Einfach und Profi geprüft; Klicken/Ziehen/Pfeiltasten in der 3D-Ansicht über `Studio3DViewport_Ui` grün; Rechtsklick über `ClaudeOperate_Ui`

## Was NICHT geprüft wurde
- B1 im echten Setup.exe-Weg (BLOCKIERT wie T-0724) und auf einem PC ohne Defender; Codesignatur fehlt weiter.
- B2/B3 im sichtbaren Fenster mit GPU (gemessen headless bzw. mit dem Render-Werkzeug).
- B4 mit anderem Quellmaterial (kürzere Schlüsselbild-Abstände wären schneller) und mit Proxy-Dateien.
- B5 mit echtem Demucs (ersetzt durch `StemSplitOverride`, schreibt echte WAV-Spuren).
- Volltest (macht die Hauptsitzung).

## Offene Punkte / Risiken
- B1: Kaltstart nur über Codesignatur bzw. Reputation zu verbessern (eigener Auftrag, Zertifikat nötig).
- B3: Vollständig erst mit kompaktem Seitenkopf (Audit S-01/V-01); unter 200 px sichtbarer Höhe wird jetzt gescrollt.
- B4: Ziel 0,3 s in Überblendungen nicht erreicht; ehrlich offen.
- Kanten-Cache: ändert jemand einen Körper an Ort und Stelle mit gleicher Flächenzahl, bleiben die Kanten alt (vorher galt das je Ansicht genauso).
- Hängender Volltest (Hinweis der Hauptsitzung, App-Suite nach ≈ 32 min, 7 GB): in meinen Läufen (46 Klassen, ≈ 5 min) kein Hängen, keine Ursache gefunden. Vorschlag: `Trendo.App.Tests.exe -parallel none -verbose` bzw. `-longRunning 60`, dann nennt xUnit den hängenden Test. Unter Last war `FilmSchnitt_Ui.Render_click_to_file_real` einmal rot („nicht sichtbar: prog-cmd-film-import-music“), allein 5/5 grün – vermutlich lastabhängig, nicht von diesem Paket berührt.

## Rust
Nicht eingesetzt: B1 liegt vor Trendos Code, B4 in FFmpeg-Dekodierung, B2 war Datenstruktur-/Allokationsaufwand in C# (behoben). Keine Messung zeigt einen Vorteil von Rust.
