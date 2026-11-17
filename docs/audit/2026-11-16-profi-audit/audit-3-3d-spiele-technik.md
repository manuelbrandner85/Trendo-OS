# Audit Gruppe 3 – „3D, Spiele & VR“ und „Technik & Fachgebiete“

Stand: Arbeitskopie `studios-aufraeumen` (ba52927a8), nur gelesen, nichts gebaut, keine Tests gestartet.
Pfade relativ zum Repo. „App/“ = `src/Trendo.App/`, „Core/“ = `src/Trendo.Core/`.
Urteile: VOLLWERTIG / TEILWEISE / FEHLT / BLOCKIERT / NICHT GETESTET. Was nicht belegt werden konnte, steht als „nicht geprüft“.

Vorab drei Tatsachen, die für alle Abschnitte gelten:

1. **Zwei Bauarten.** Programme mit Programm-Hülle (`RegisterProgram(... new ProgramSpec ...)`): architektur, elektronik, technik, cnc, karten, punktwolke, spiele-programm, konsole-programm. Sie erben aus `App/MainWindow.ProgramShell.cs` und `Core/Programs/ProgramDocument.cs`: Rückgängig/Wiederholen bis 100 Schritte (`ProgramDocument.cs:38,87,94`), Strg+Z/Y (`ProgramShell.cs:349-350`), Autosave alle 30 s, 1,5 s nach Änderung und beim Beenden (`ProgramShell.cs:85-88,228`), Versionen bis 20 (`ProgramDocument.cs:151`), Wiederherstellen ungespeicherter Dokumente auf der Startseite (`ProgramShell.cs:520-526`, Test „Programm: ungespeichertes Dokument wird automatisch gesichert und auf der Startseite wiederhergestellt“). Programme **ohne** Hülle (katalog.json `Shell: false`): 3d, cad, 3d-druck, scan3d, xr, fahrzeug, zwilling, roboter, spiele (Werkstatt) – dort gilt das alles nicht automatisch.
2. **Externe Werkzeuge sind in den Tests ersetzt** (`UnrealRunOverride`, `EngineRunOverride`, `IfcRunOverride`, `ScanRunOverride`, `ConsoleSendOverride`, `LibGodotOverride`, Blender-Overrides in `Spec04Pro.cs:38`). Als Ende-zu-Ende-Nachweis zählen nur die Echtlauf-Tests (`tests/Trendo.Core.Tests/EchtWerkzeuge.cs`, werden übersprungen, wenn das Werkzeug fehlt – `reports/alle-studios/echtlauf-automatisch.md`) und die Belege unter `reports/paket01/` und `reports/alle-studios/`.
3. **Studio-Liste ≠ Auftrag.** `App/StudioCatalog.cs:145`: SPEC-04, SPEC-16 und CORE-09 stehen in `Removed` („auf Wunsch vom 09.11.2026 … nirgends mehr angeboten“). `StudioCatalog.cs:150`: SPEC-10 ist in SPEC-05 eingegliedert. Der Code dieser Studios existiert weiter; ich bewerte ihn, werte das Fehlen in der Übersicht aber nicht als Mangel.

## Übersicht

| Studio-Id | Name | Programme | Gesamturteil | größte Lücke | Aufwand |
|---|---|---|---|---|---|
| CORE-05 | 3D-Modelle erstellen | 3d, cad, 3d-druck | TEILWEISE | 3D-Studio ohne Autosave/Wiederholen/Absturzschutz, keine Netzbearbeitung (Punkte/Kanten/Flächen); CAD ohne Import; G-Code nur über Bambu Studio | groß |
| CORE-06 | Spiele bauen | spiele-programm, spiele (Werkstatt) | TEILWEISE | spielbar ist real nur der 2D-Baukasten (4 feste Spielarten); Unreal/Godot/Unity sind in allen automatischen Tests ersetzt, echte Läufe nur als Einzelbelege vom 06./07.11. | sehr groß |
| STUDIO-CONSOLE-01 | Konsolenspiele portieren | konsole-programm | NICHT GETESTET | Assistent, der Aufträge an Claude Code schickt; kein eigener Portierweg, kein Beleg eines echten Laufs im Repo, Katalogstatus `IN_PROGRESS / NOT_RUN` | groß |
| SPEC-04 | VR & AR gestalten (entfernt) | xr | TEILWEISE | Modell-Prüfer, kein Gestaltungswerkzeug; keine Brille, kein Szenen-Editor | groß |
| SPEC-09 | Gegenstände einscannen | punktwolke, scan3d | TEILWEISE | Fotoscan (COLMAP/Meshroom) nie echt belegt; KI-Aktion `scan.starten` ist Blocked | mittel |
| SPEC-01 | Wohnung planen | architektur | TEILWEISE | kein Import von DXF/IFC in den bearbeitbaren Plan; DXF nur R12-Export ohne Rundreise-Test | mittel |
| SPEC-02 | Schaltungen berechnen | elektronik | TEILWEISE | kein Import (SPICE/KiCad), Gerber nicht gegen ein Fremdwerkzeug geprüft, kleiner Bauteilvorrat | mittel |
| SPEC-03 | Statik nachrechnen | technik | TEILWEISE | nur ebenes Stabwerk, kein Import/Austauschformat, keine Gegenprobe gegen Fremdsoftware belegt | mittel |
| SPEC-10 | Geräte überwachen (in SPEC-05) | zwilling | TEILWEISE | Anzeige/Dashboard ohne Hülle (kein Rückgängig, kein Dokument); nur MQTT lesen und CSV | mittel |
| SPEC-13 | Routen & Karten | karten | TEILWEISE | Routing nur über selbst eingetragenen OSRM-Server; KI-Aktion liefert nur Streckenlänge als Text | mittel |
| SPEC-14 | Fräsen & Fertigen | cnc | TEILWEISE | kein CAM: G-Code entsteht nur für drei feste Formen (Tasche, Kontur, Lochkreis), nicht aus DXF/STL/CAD | groß |
| SPEC-16 | Auto & Fahrten (entfernt) | fahrzeug | TEILWEISE | Formular-/Listenprogramm ohne Hülle, nur CSV | klein |
| CORE-09 | Kameras & Roboter (entfernt) | roboter | TEILWEISE | Roboter nur Simulation, echte Hardware nie belegt; KI-Aktionen `kamera.nennen` (Text) und `kamera.steuern` (gesperrt) | groß |

Zählung Gesamturteile (13 Studios): VOLLWERTIG 0 · TEILWEISE 12 · NICHT GETESTET 1 · FEHLT 0 · BLOCKIERT 0.

---

## CORE-05 3D-Modelle erstellen

### Programm „3d“ (3D-Studio, ohne Hülle)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Echte 3D-Ansicht mit Anklicken, Ziehen, Anfassern (`App/MainWindow.Studio3DViewport.cs:11-13,100-126`), aber nur ganze Teile; keine Punkt-/Kanten-/Flächenbearbeitung, daneben Zahlenfelder (`App/MainWindow.W4Studio3D.cs:187-191`) |
| 2 | Werkzeuge | TEILWEISE | Formen, Abrunden, Loch, Glätten, Spiegeln, Wandstärke (`W4Studio3D.cs:187-191`); Modell = Liste parametrischer Teile (`Core/Scene3D.cs:134-180`) |
| 3 | Neues Projekt | VOLLWERTIG | `NewScene` `W4Studio3D.cs:438` |
| 4 | Import | VOLLWERTIG | OBJ/STL/GLB/glTF/FBX direkt (`W4Studio3D.cs:534-535`, `Core/MeshIO.cs:131,232`), Blend/binäres FBX über Blender (`W4Studio3D.cs:592`), Drag-and-drop (`W4Studio3D.cs:257-259`), weitere Formate über assimp (Echtlauf `reports/alle-studios/assimp-e2e.md`) |
| 5 | Vorhandenes bearbeiten | TEILWEISE | `Open3DProject` lädt `3d/szene.json` (`W4Studio3D.cs:422-436`); importierte Netze nur als Ganzes verschieb-/skalierbar |
| 6 | Rückgängig | TEILWEISE | eigener Stapel, 30 Schritte, nur im Speicher (`W4Studio3D.cs:27,720-724`); **kein Wiederholen**, kein Strg+Z in der Ansicht (`Studio3DViewport.cs:102-111` kennt nur Pfeile, Entf, Strg+D, Esc, G/R/S) |
| 7 | Vorschau | VOLLWERTIG | eigener Software-Renderer `Mesh3D.RenderPng` (`Core/Mesh3D.cs:863`), f3d-Vorschau echt belegt (`reports/alle-studios/f3d-e2e.md`), Cycles-Render über Blender (`Core/Studio3DPro.cs:91`) |
| 8 | Speichern/Autosave | TEILWEISE | nur Knopf „Speichern“ (`W4Studio3D.cs:77,392-420`); **kein Autosave** (kein Treffer für autosave in `W4Studio3D.cs`/`Studio3D*.cs`) |
| 9 | Wiederherstellung | FEHLT | Szene liegt nur in `_scene`; kein Sicherungs- oder Wiederherstellweg im Code gefunden |
| 10 | Export | VOLLWERTIG | OBJ, STL, GLB, FBX nativ (`Core/MeshIO.cs:17,40,56,266`), .blend über Blender (`Core/Studio3D.cs:376`), „Für Engine“ Unity/Unreal/Godot/Web (`Core/Studio3DEngines.cs`) |
| 11 | Rundreise | TEILWEISE | Blender-Rundlauf FBX/GLB ohne Verlust belegt (`reports/paket01/s2-17-blender-rundlauf.md`); dort ausdrücklich „Nicht geprüft: Import in Unity/Unreal/Godot selbst“ |
| 12 | Fachfunktion real | TEILWEISE | echte Netze und Dateien entstehen; „bearbeitbares Modell“ heißt hier Baukasten aus Grundformen, kein Modellierer |
| 13 | Claude/KI | TEILWEISE | Aktion `modell.bauen` schreibt OBJ/MTL/PNG/JSON (`Core/PromptAction/ModelPromptAdapter.cs:16`), `modell.blender` ist Blocked (`ModelPromptAdapter.cs:42`); Claude ändert die Teileliste als Textzeilen (`App/MainWindow.Claude3D.cs:16-21,50`) |
| 14 | E2E-Tests | TEILWEISE | UI-Tests headless (`W4Studio3DUiTests`, „3D-Arbeitsfläche: Formen einsetzen …“); Blender in Tests ersetzt; echt: f3d, assimp, Blender-Rundlauf (Belege oben) |
| 15 | Bedienung | TEILWEISE | Einfach/Erweitert (Test „3D-Modelle, Einfach: Grundfunktionen sichtbar …“), Tasten G/R/S, Drag-and-drop; F11 allgemein (`App/MainWindow.ProgramFrame.cs:217`); Rechtsklick in der 3D-Ansicht nicht gefunden |

### Programm „cad“ (Trendo CAD, ohne Hülle)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | 3D-Ansicht mit Anfassern (`App/CadView.cs:140-178`), Bauplan als Schrittliste, Maße als Formeln (`App/MainWindow.Cad.cs:12-13,217,249`) |
| 2 | Werkzeuge | TEILWEISE | 14 Schritte: Quader … Extrusion, Drehteil, Bohrung, Tasche, Rundung, Fase, Muster (`Core/Cad/CadDoc.cs:9`); Skizze nur Rechteck/Kreis/Vieleck in XY (`CadDoc.cs:11-14`); echte CSG (`Core/Cad/CadCsg.cs`) |
| 3 | Neu | VOLLWERTIG | `CadNew` `MainWindow.Cad.cs:48` |
| 4 | Import | FEHLT | kein STEP-/DXF-/STL-Import in `MainWindow.Cad.cs` (nur `CadOpen` für eigenes `.tcad`, Zeile 111) |
| 5 | Bearbeiten | VOLLWERTIG | `.tcad` laden, Schritte ändern, Parameter rechnen neu (Test „N23: Bauschritte über das Menüband, Maß als Formel ändern …“) |
| 6 | Rückgängig | VOLLWERTIG | Undo/Redo 80 Schritte (`MainWindow.Cad.cs:104-107`) |
| 7 | Vorschau | VOLLWERTIG | Körper wird nach jeder Änderung neu gebaut (`MainWindow.Cad.cs:108`) |
| 8 | Speichern/Autosave | TEILWEISE | nur Menü „Speichern“ (`MainWindow.Cad.cs:110,181`); **kein Autosave**, `CadDirty` wird nur angezeigt (Zeile 165), keine Rückfrage beim Schließen gefunden |
| 9 | Wiederherstellung | FEHLT | kein Sicherungsweg gefunden |
| 10 | Export | TEILWEISE | STL, 3MF, STEP, PDF-Zeichnung (`MainWindow.Cad.cs:124-127`); STEP ist **facettiert**, keine echten Flächen (`Core/Cad/CadExport.cs:110`); kein DXF |
| 11 | Rundreise | NICHT GETESTET | 3MF wird im Druck-Kern wieder gelesen (Test „Druck-Kern: … 3MF lesen in mm“); STEP in FreeCAD/Fusion geöffnet: kein Beleg unter `reports/` |
| 12 | Fachfunktion | TEILWEISE | echtes parametrisches Datenmodell mit Masse, Schwerpunkt, Normbohrungen (`Core/Cad/CadMeasure.cs:19,61`); kein B-Rep, keine Zwangsbedingungen in der Skizze |
| 13 | Claude/KI | TEILWEISE | keine eigene Aktion; nur allgemeines Seite-lesen-und-bedienen (`App/MainWindow.ClaudeOperate.cs:35`), kein Feldsatz wie beim 3D-Studio |
| 14 | E2E-Tests | TEILWEISE | „N23: Export – STL (mm), 3MF …, STEP (facettiert), Zeichnung als PDF“; kein Fremdprogramm-Lauf |
| 15 | Bedienung | TEILWEISE | Menü, Werkzeugleiste (`MainWindow.Cad.cs:181-213`); Tastenkürzel/Rechtsklick im CAD: nicht geprüft |

### Programm „3d-druck“ (ohne Hülle)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Slicer-artiger Aufbau mit Druckplatte in 3D (`App/MainWindow.Print3D.cs:154`) |
| 2 | Werkzeuge | TEILWEISE | Aufstellen, Auto-Ausrichten, Überhang, Schichtvorschau, Kopien, Anordnen (`Core/Cad/PrintPrep.cs:56-124`, `Core/Cad/Slicer.cs:93-166`) |
| 3–5 | Neu/Import/Bearbeiten | TEILWEISE | 3MF, STL, STEP, OBJ wählen (`Print3D.cs:157`); kein eigenes Projektdokument |
| 6 | Rückgängig | FEHLT | kein Rückgängig in `Print3D*.cs` |
| 7 | Vorschau | TEILWEISE | eigene Schicht-/Füllvorschau und Schätzung (`Slicer.cs:93,104`) |
| 8–9 | Speichern/Wiederherstellung | FEHLT | kein Dokument, kein Autosave |
| 10 | Export | BLOCKIERT | **G-Code schreibt Trendo nicht selbst**: „G-Code schreibt weiterhin der Fachslicer“ (`Core/Cad/Slicer.cs:5`); Drucken nur mit `.gcode.3mf` aus Bambu Studio (`Print3D.cs:86`) |
| 11 | Rundreise | NICHT GETESTET | kein Beleg |
| 12 | Fachfunktion | NICHT GETESTET | Bambu-LAN (MQTT, Kamera, Start) im Kern vorhanden (`Core/Print3D/BambuLan.cs:99-193`), Tests gegen nachgebildete Verbindung (`App/MainWindow.Print3DPro.cs:20-27`); kein Lauf an einem echten Drucker oder mit echtem Bambu Studio unter `reports/` |
| 13 | Claude/KI | FEHLT | keine Aktion für Druck |
| 14 | E2E-Tests | NICHT GETESTET | alle „CORE-05 B1–B5“-Tests laufen gegen Ersatz |
| 15 | Bedienung | nicht geprüft | – |

**Gesamturteil CORE-05: TEILWEISE.** Drei brauchbare Bausteine mit echten Dateien, aber keiner ist ein vollwertiger Modellierer, und zwei von drei verlieren Arbeit bei einem Absturz.

**Was fehlt (priorisiert):**
1. 3D-Studio und CAD auf die Programm-Hülle heben (Autosave, Wiederholen, Versionen, Wiederherstellung).
2. Netzbearbeitung im 3D-Studio (Punkte/Kanten/Flächen, Extrudieren) oder ehrlich als „Baukasten + Blender“ benennen.
3. CAD-Import (STEP, DXF, STL als Referenz) und DXF-Export; STEP als echte Flächen statt Facetten.
4. Eigener G-Code-Export oder nachgewiesener Bambu-Studio-Lauf; ein belegter Druck an echter Hardware.
5. Rundreise-Belege: STEP in FreeCAD, FBX/GLB in Unreal/Unity/Godot.
6. Claude-Anbindung für CAD (Bauplan als Feldsatz wie `Claude3DFields`).

**Abnahmekriterien:**
- App während einer ungespeicherten 3D-Szene und eines CAD-Teils hart beenden; nach Neustart werden beide angeboten und sind vollständig.
- Ein CAD-Teil als STEP exportieren, in FreeCAD öffnen, Volumen weicht < 0,5 % ab (Rohlog + SHA-256 unter `reports/`).
- Eine fremde STEP- und eine DXF-Datei lassen sich laden und weiterbearbeiten.
- Aus einem CAD-Teil entsteht ohne Handarbeit eine druckbare Datei, deren Schichtzahl und Filamentmenge mit dem Slicer-Bericht übereinstimmen.
- Strg+Z und Strg+Y wirken in der 3D-Ansicht.

**Aufwand:** groß · **Risiko:** mittel (Umzug auf die Hülle berührt 3 große Dateien; B-Rep-STEP ist fachlich schwer).
**Dateien:** `App/MainWindow.W4Studio3D.cs`, `App/MainWindow.Studio3DViewport.cs`, `App/MainWindow.Cad.cs`, `App/CadView.cs`, `App/MainWindow.Print3D.cs`, `App/MainWindow.Print3DPro.cs`, `Core/Mesh3D.cs`, `Core/MeshIO.cs`, `Core/Scene3D.cs`, `Core/Cad/*`, `Core/Print3D/*`, `tools/blender/inspect_asset.py`, `tools/blender/smoke_asset.py`.

---

## CORE-06 Spiele bauen

### Programm „spiele-programm“ (mit Hülle)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Level-Raster zum Malen mit Kachel-Palette (`App/MainWindow.ProgGame.cs:452-490`, Test „Level-Editor: Münzen ziehend malen …“); übrige Bereiche (Spielbibel, Figuren, Welt, Missionen, Produktion) sind Listen und Formulare (`ProgGame.cs:104-113`) |
| 2 | Werkzeuge | TEILWEISE | Prüfen, Bauen, Spielen (`ProgGame.cs:28-30`), Regeln, Fähigkeiten, Waffe, Menü, mehrere Level (`Core/Programs/GameDesign.cs:170-189`) |
| 3 | Neu | VOLLWERTIG | Vorlagen `GameDesign.Templates` (`GameDesign.cs:55-68`), Spiel aus einem Satz (`App/MainWindow.ProgGamePrompt.cs:29`) |
| 4 | Import | TEILWEISE | Bilder, Bildstreifen, Töne ins Spiel (`GameDesign.cs:179-190`); freie Assets, Mixamo; kein Import fremder Spielprojekte (Tiled nur Export) |
| 5 | Bearbeiten | VOLLWERTIG | `.tdoc` laden, weiterbauen; vorhandener Spielordner wird aktualisiert (`GameDesign.cs:205`) |
| 6 | Rückgängig | VOLLWERTIG | Hülle; „Rückgängig je Strich“ im Level-Editor-Test |
| 7 | Vorschau | TEILWEISE | Probespiel/Testmodus mit `PlatformSim` (`Core/Programs/PlatformSim.cs:212`), nur für die Spielart „plattform“ (`ProgGame.cs:91`) |
| 8 | Speichern/Autosave | VOLLWERTIG | Hülle |
| 9 | Wiederherstellung | VOLLWERTIG | Hülle |
| 10 | Export | TEILWEISE | spielbares HTML5-Spiel (Canvas + JS, `Core/GameStudio.cs:216-331`), ZIP, Tiled `.tmj` (`reports/alle-studios/tiled-e2e.md`), Unity-/Godot-Projekt (`Core/Programs/EngineTargets.cs:137`), Unreal-Projekt mit C++-Spiel (`GameDesign.cs:221-235`, `Core/Programs/UnrealGame.cs`, 3468 Zeilen), Windows-Paket über Unreals Paketierer (`App/MainWindow.ProgGameRelease.cs:107`) |
| 11 | Rundreise | NICHT GETESTET | gebautes Spiel zurück ins Dokument: kein Weg gefunden |
| 12 | Fachfunktion | TEILWEISE | siehe „Was läuft echt“ unten |
| 13 | Claude/KI | TEILWEISE | `spiel.browser` schreibt HTML/JS/MD, `spiel.unreal` schreibt nur `.uproject`, `spiel.starten` ist Blocked (`Core/PromptAction/GamePromptAdapter.cs:16,43,70`); Claude setzt Level als Textzeilen direkt im Programm (`App/MainWindow.ClaudeGame.cs:15-62`) |
| 14 | E2E-Tests | TEILWEISE | automatische Tests ersetzen jede Engine (`App/MainWindow.ProgGameUnreal.cs:15-19`, `ProgGameEngines.cs:15-17`, `tests/Trendo.App.Tests/GamePackageUiTests.cs:27-41`, `GameStage3UiTests.cs:35-36`); echte Läufe nur als Belege, siehe unten |
| 15 | Bedienung | TEILWEISE | Einfach zeigt nur Auftrag und nächsten Schritt (`ProgGame.cs:98-100`); Tastenkürzel der Hülle; Rechtsklick/Drag-and-drop im Level-Raster: nicht geprüft |

### Programm „spiele“ (Werkstatt, ohne Hülle)

Spielesammlung mit Regeln, Aussehen, Level-Raster, ZIP-Export und „Unreal öffnen“ (`App/MainWindow.W4Game.cs:9,99`, Tests `GameEditorTests`). Kein Rückgängig, kein Autosave gefunden (FEHLT bei 6, 8, 9); sonst wie oben, aber kleiner. Überschneidet sich fachlich fast ganz mit „spiele-programm“.

### Was läuft echt, was ist ersetzt

| Weg | Stand | Beleg |
|---|---|---|
| Browser (HTML5) | **echt spielbar**, vier feste Spielarten: Schlange, Mauerbrecher, Hüpfspiel, Plattformer (`Core/GameStudio.cs:9-14,216-331`). „Indie 2D“ nutzt denselben Baukasten (`GameDesign.cs:14-15`) | Test „Referenz: Bauen erzeugt echte Spieldateien mit Titel und Level des Dokuments“; ein Browser-Lauf mit Tasteneingabe ist nicht automatisiert belegt (nicht geprüft) |
| Godot 4 | echter Lauf einmalig belegt (Import, Level-Prüfung, 300 Bilder, Gameplay-Prüfung, Gegenprobe) | `reports/paket01/s1-01-godot-lauf.md`, `s1-05-gameplay-export.md` (06.11.2026); im Testlauf ersetzt (`EngineRunOverride`) |
| Unity 6 | echter Lauf einmalig belegt (EditMode 3/3, PlayMode 1/1, Windows-Build 95,9 MB) | `reports/paket01/s1-01-unity-lauf.md`; im Testlauf ersetzt |
| Unreal 5 | echte Läufe mit UE 5.8 belegt (Kampf, Kollision, Menschen, Fahrzeug), mit offen genannten Mängeln: Ragdoll fällt durchs Gelände, Wagen beschleunigt kaum, kein Navigationsnetz | `reports/paket01/unreal-kampf-e2e.md` u. a. `unreal-*-e2e.md` (07.11.2026); im Testlauf ersetzt (`UnrealRunOverride`) |
| Export nach Godot/Unity | nur der gebaute **Plattformer** (`App/MainWindow.ProgGameEngines.cs:52`) | – |
| „AAA+“ | setzt nur fünf Render-Schalter in `DefaultEngine.ini` (`GameDesign.cs:231-233`) | – |

**Gesamturteil CORE-06: TEILWEISE.** Es entsteht ein wirklich spielbares Spiel, aber nur im 2D-Baukasten; alles Größere hängt an einer installierten Engine und ist im Regelbetrieb ungeprüft.

**Was fehlt:**
1. Wiederholbarer Echtlauf je Engine im Testsatz (wie `EchtWerkzeuge` für IFC/Open3D), statt einmaliger Belege.
2. Frei definierbare Spiellogik statt vier fester Spielarten (eigene Objekte, Ereignisse, Szenen).
3. Szenen-/Welt-Editor für 3D statt Formular plus generiertem C++.
4. Godot-/Unity-Export für alle Spielarten, nicht nur Plattformer.
5. Rundreise: in der Engine geänderte Level zurück ins Trendo-Dokument.
6. Versprechen im Text anpassen („bis zum AAA+-Projekt“, `ProgGame.cs:22`), solange AAA+ nur Schalter setzt.

**Abnahmekriterien:**
- Ein neues Spiel aus Vorlage wird gebaut, im Browser gestartet, per Tastatur bis zum Ziel gespielt; ein automatischer Lauf weist Start, Bewegung, Sieg und Niederlage nach.
- Mit installiertem Godot läuft `play_check.gd` im Testsatz grün; ohne Godot wird der Test sichtbar übersprungen, nicht ersetzt.
- Ein Unreal-Spiel wird kompiliert, gestartet und als Windows-ZIP gepackt; Rohlog und SHA-256 liegen unter `reports/`.
- Ein Spiel mit einer selbst definierten Regel, die keine der vier Vorlagen kennt, ist baubar.

**Aufwand:** sehr groß · **Risiko:** hoch (Engine-Versionen, Lizenz-/Installationsabhängigkeit, lange Läufe).
**Dateien:** `App/MainWindow.ProgGame*.cs` (13 Dateien), `App/MainWindow.W4Game.cs`, `App/MainWindow.ClaudeGame.cs`, `Core/GameStudio.cs`, `Core/Programs/GameDesign.cs`, `Core/Programs/EngineTargets.cs`, `Core/Programs/UnrealGame.cs`, `Core/Programs/PlatformSim.cs`, `Core/PromptAction/GamePromptAdapter.cs`, `tools/unreal/validate_project.py`, `reports/paket01/`.

---

## STUDIO-CONSOLE-01 Konsolenspiele portieren

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | FEHLT | geführter Assistent mit Feldern (Spiel, Abbild, Besitz, Wunsch) (`App/MainWindow.ConsolePort.cs:31`); Katalog nennt als Oberfläche „Bildansicht“ |
| 2 | Werkzeuge | TEILWEISE | Abbild prüfen, Skills per Git holen, Auftrag an Claude (`Core/ConsolePort/ConsolePort.cs:44,59,67,76`) |
| 3 | Neu | VOLLWERTIG | eine Vorlage (`ConsolePort.cs` App, Zeile 31) |
| 4 | Import | TEILWEISE | eigenes Disc-Abbild wird nur geprüft (`ConsolePort.cs:44`) |
| 5 | Bearbeiten | NICHT GETESTET | Fortschritt nur aus `console/STATE.md` gelesen (`ConsolePort.cs:89`) |
| 6, 8, 9 | Rückgängig, Autosave, Wiederherstellung | VOLLWERTIG (nur für die Formulardaten) | Hülle |
| 7 | Vorschau | FEHLT | – |
| 10–11 | Export, Rundreise | NICHT GETESTET | Paket-Auftrag geht als Text an Claude (`ConsolePort.cs:82`); Katalog-Formate nur `.md`, `.tdoc` |
| 12 | Fachfunktion | NICHT GETESTET | „Die Arbeit macht Claude“ (`App/MainWindow.ConsolePort.cs:12`); ohne verbundenes Claude nur Hinweis (`ConsolePort.cs:141-142`) |
| 13 | Claude/KI | TEILWEISE | Aktion `konsole.plan` liefert nur Text (`Core/PromptAction/StudyPromptAdapter.cs:231`); der eigentliche Weg ist ein Prompt an Claude Code |
| 14 | E2E-Tests | NICHT GETESTET | 2 Tests, beide mit `ConsoleSendOverride`/`ConsoleGitOverride` (`ConsolePort.cs:17-20`); kein Beleg unter `reports/`, Katalogstatus `IN_PROGRESS / Test NOT_RUN` (`docs/programme/katalog.json`, Eintrag konsole-programm) |
| 15 | Bedienung | nicht geprüft | – |

**Gesamturteil: NICHT GETESTET.** Ein Startpult für fremde Skills und Claude Code, kein eigenes Fachprogramm; ob je ein Spiel herauskommt, ist im Repo nicht belegt.

**Was fehlt:** 1. ein dokumentierter echter Portierlauf mit einem frei lizenzierten Titel unter `reports/`; 2. Fortschritts- und Ergebnisansicht (Protokoll, Bildschirmfoto, Startknopf für das fertige Programm); 3. klare Trennung zur Spiele-Bibliothek (`App/MainWindow.GameLibrary.cs`), die im selben Programm liegt; 4. ehrliche Beschriftung, dass Claude Code Voraussetzung ist.
**Abnahmekriterien:** Ein MIT-lizenziertes PS2-Demo wird aus Trendo heraus bis zur startbaren Windows-Datei gebracht, mit Rohlog und SHA-256; ohne Claude zeigt das Programm vor dem ersten Schritt, was fehlt; das fertige Programm startet per Knopf.
**Aufwand:** groß · **Risiko:** hoch (Fremdwerkzeug PS2Recomp, Rechtefragen, lange Läufe).
**Dateien:** `App/MainWindow.ConsolePort.cs`, `App/MainWindow.GameLibrary.cs`, `Core/ConsolePort/ConsolePort.cs`, `Core/ConsolePort/GameLibrary.cs`.

---

## SPEC-04 VR & AR gestalten (aus der Liste entfernt, `StudioCatalog.cs:145`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | 3D-Ansicht nur zum Ansehen; Selbstbeschreibung „Modell-Prüfer“ (`App/MainWindow.Spec04.cs:9`) |
| 2 | Werkzeuge | TEILWEISE | Geräte-Grenzen, Augenhöhe, Brillen-Ansicht, Reparatur über Blender (`App/MainWindow.Spec04Pro.cs:9-41`), Khronos-Prüfer und glTF-Transform (`App/MainWindow.Spec04Gltf.cs:21,30`) |
| 3 | Neu | FEHLT | kein Projektdokument |
| 4 | Import | VOLLWERTIG | GLB/glTF öffnen (Katalog F01 „GLB lesen“) |
| 5–6, 8–9 | Bearbeiten, Rückgängig, Autosave, Wiederherstellung | FEHLT | ohne Hülle; kein Rückgängig-Treffer in `Spec04*.cs` |
| 7 | Vorschau | TEILWEISE | Stereo-Vorschau am Bildschirm, keine Brille (`Spec04Pro.cs:19`) |
| 10 | Export | TEILWEISE | „…-web.glb“ und „…-brille.glb“ als neue Dateien (`Spec04Gltf.cs:30`, `Spec04Pro.cs:41`) |
| 11 | Rundreise | VOLLWERTIG | gepacktes Modell wird sofort wieder geprüft; Echtlauf 532 KB → 105 KB, 0 Fehler (`reports/alle-studios/gltf-e2e.md`) |
| 12 | Fachfunktion | TEILWEISE | Prüfen und Packen echt; „gestalten“ findet nicht statt |
| 13 | Claude/KI | TEILWEISE | `bildzeit.pruefen` gibt nur Text aus (`Core/PromptAction/StudyPromptAdapter.cs:356`) |
| 14 | E2E-Tests | TEILWEISE | glTF-Werkzeuge echt belegt; Blender-Reparatur in Tests ersetzt (`Spec04Pro.cs:38`) |
| 15 | Bedienung | nicht geprüft | – |

**Gesamturteil: TEILWEISE.** Solider Prüf- und Packdienst für GLB, aber kein Werkzeug zum Gestalten von VR/AR.
**Was fehlt (falls das Studio zurückkommt):** Szenen-Editor (Raum, Platzierung, Maßstab), Ausgabe als WebXR-Seite oder Engine-Szene, Test an einer echten Brille.
**Abnahmekriterien:** Ein Raum mit drei Modellen wird platziert, gespeichert, wieder geöffnet; die Ausgabe besteht den Khronos-Prüfer; eine WebXR-Seite startet im Browser.
**Aufwand:** groß · **Risiko:** mittel. **Dateien:** `App/MainWindow.Spec04.cs`, `Spec04Gltf.cs`, `Spec04Pro.cs`, `Core/W6/XrModels*.cs`, `Core/W6/XrPro.cs`, `Core/W6/GltfTools.cs`.

---

## SPEC-09 Gegenstände einscannen

### Programm „punktwolke“ (mit Hülle)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | drehbare Punktwolken-Ansicht mit Messen (`App/PointCloudView.cs`, `App/MainWindow.Spec09.cs:55,67`); Bearbeitung über Befehle, keine Auswahl mit Lasso/Pinsel |
| 2 | Werkzeuge | VOLLWERTIG | Störpunkte, Ausdünnen, Boden, Zentrieren, Drehen, Einheit, Zuschneiden als nicht zerstörende Schrittliste (`Spec09.cs:39-47,67`) |
| 3–5 | Neu, Import, Bearbeiten | VOLLWERTIG | PLY (ASCII/binär), XYZ, PTS, CSV, OBJ (`Spec09.cs:33`) |
| 6, 8, 9 | Rückgängig, Autosave, Wiederherstellung | VOLLWERTIG | Hülle |
| 7 | Vorschau | VOLLWERTIG | Ansichten und Färbung nach Höhe (`Spec09.cs:50-54`) |
| 10 | Export | TEILWEISE | PLY, XYZ, Messbericht (`Spec09.cs:60-62`); kein E57/LAS |
| 11 | Rundreise | TEILWEISE | PLY lesen und schreiben getestet (Katalog-Tests F01, F17); Fremdprogramm: nicht geprüft |
| 12 | Fachfunktion | VOLLWERTIG | Volumen, Dichte, Vergleich, Zusammenführen (Tests SPEC-09 F10–F20) |
| 13 | Claude/KI | BLOCKIERT | `scan.starten` Blocked (`StudyPromptAdapter.cs:381`, doppelt in `SystemPromptAdapter.cs:31`); Claude kann das `.tdoc` ändern (allgemeiner Weg) |
| 14 | E2E-Tests | TEILWEISE | Kern- und UI-Tests; Open3D-Glätten echt belegt (`reports/alle-studios/open3d-e2e.md`) |
| 15 | Bedienung | TEILWEISE | Maus drehen/schieben/zoomen (`Spec09.cs:67`); Rechtsklick-Menü nicht gefunden |

### Programm „scan3d“ (Aus Fotos, ohne Hülle)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1–2 | Arbeitsfläche, Werkzeuge | TEILWEISE | Ablauf Fotos → Rechnung → Ergebnis; Nachbearbeitung Beschneiden, Reste, Löcher, echte Größe (`App/MainWindow.Spec09ScanPro.cs:45-108`) |
| 4 | Import | VOLLWERTIG | Fotos oder Video einlesen und prüfen (`App/MainWindow.Spec09Scan.cs:38`) |
| 6 | Rückgängig | TEILWEISE | eine Sicherung vor Modelländerungen (`Spec09ScanPro.cs:45`) |
| 8–9 | Autosave, Wiederherstellung | FEHLT | ohne Hülle |
| 10 | Export | TEILWEISE | OBJ, STL, GLB (`Spec09Scan.cs:127`) |
| 12 | Fachfunktion | NICHT GETESTET | COLMAP/Meshroom nur als Ersatz im Test („führt die Rechnung aus, ohne COLMAP oder Meshroom zu starten“, `Spec09Scan.cs:26`); unter `reports/` kein Lauf mit echten Fotos (Treffer für COLMAP/Meshroom nur in Installationslisten) |
| 13 | Claude/KI | BLOCKIERT | „COLMAP und Meshroom werden aus dem Auftrag nicht gestartet“ (`Core/PromptAction/CapabilityCatalog.cs:61`) |
| 14 | E2E-Tests | NICHT GETESTET | siehe 12 |

**Gesamturteil SPEC-09: TEILWEISE.** Die Punktwolke ist ein ordentliches kleines Fachprogramm; der Fotoscan, also das Versprechen im Studionamen, ist nie echt gelaufen.
**Was fehlt:** 1. Echtlauf Fotos → Modell mit COLMAP oder Meshroom, Beleg mit Maßvergleich; 2. scan3d auf die Hülle; 3. Auswahlwerkzeuge in der Wolke (Rahmen/Lasso); 4. E57/LAS; 5. KI-Aktion, die mindestens die Prüfung der Fotos ausführt.
**Abnahmekriterien:** 30 Fotos eines vermessenen Gegenstands ergeben ein Netz, dessen Kantenlänge < 3 % abweicht; der Lauf ist als Echtlauf-Test vorhanden; ein Abbruch mitten in der Rechnung lässt sich fortsetzen oder sauber neu starten.
**Aufwand:** mittel · **Risiko:** mittel (GPU-/Laufzeitabhängigkeit).
**Dateien:** `App/MainWindow.Spec09.cs`, `Spec09Scan.cs`, `Spec09ScanPro.cs`, `Spec09Open3d.cs`, `App/PointCloudView.cs`, `Core/W6/PointCloud*.cs`, `Core/W6/PhotoScan.cs`, `Core/W6/ScanDoc.cs`, `Core/W6/ScanPro.cs`.

---

## SPEC-01 Wohnung planen (architektur, mit Hülle)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG | eigener Grundriss-Editor: Räume aufziehen, Wände Punkt für Punkt, Länge tippen (`App/FloorPlanView.cs:86,224-260`, Test „SPEC-01 UI: Grundriss – leerer Plan, Raum mit der Maus aufziehen …“) |
| 2 | Werkzeuge | VOLLWERTIG | Raum, Wand, Bogenwand, Tür, Fenster, Möbel, Maße, Geschosse, Plan prüfen (`App/MainWindow.Spec01.cs:54-66,99-110`) |
| 3 | Neu | VOLLWERTIG | fünf Vorlagen (`Spec01.cs:45-49`) |
| 4 | Import | TEILWEISE | Bild als Vorlage mit Maßstab über zwei Punkte (`Spec01.cs:114-125,391`); IFC nur **ansehen** (`Spec01.cs:65-66`), kein DXF-/IFC-Import in den bearbeitbaren Plan |
| 5 | Bearbeiten | VOLLWERTIG | `.tdoc` |
| 6, 8, 9 | Rückgängig, Autosave, Wiederherstellung | VOLLWERTIG | Hülle; `ArchEdit` läuft über `d.Edit` (`Spec01.cs:88-93`) |
| 7 | Vorschau | VOLLWERTIG | 2D/3D-Umschalter, Foto über Blender mit Sonnenstand (`Spec01.cs:54-55`, Beleg `reports/alle-studios/realismus-werkzeuge-e2e.md`) |
| 10 | Export | VOLLWERTIG | PDF, SVG, DXF, Raumbuch-CSV, OBJ, Materialliste, IFC4 (`Spec01.cs:71-77`) |
| 11 | Rundreise | TEILWEISE | IFC wird nach dem Export wieder eingelesen (`App/MainWindow.Spec01Ifc.cs:42-44`), Echtlauf mit Volumenprüfung (`reports/alle-studios/ifc-e2e.md`, Test `IfcExchangeTests.Real_template_roundtrip`); DXF ist R12 (`Core/Arch/PlanDrawing.cs:226`), Öffnen in einem CAD-Programm: kein Beleg |
| 12 | Fachfunktion | VOLLWERTIG | Flächen nach WoFlV, Fenster-/Türprüfung, Bedarf (Tests SPEC-01 F08–F16) |
| 13 | Claude/KI | TEILWEISE | `wohnung.flaeche` schreibt SVG und `.tdoc` (`Core/PromptAction/MeasurePromptAdapter.cs:70,433`); Claude setzt Wände direkt im Dokument (Test „Grundriss live: Claude setzt die Wände einer 2-Raum-Wohnung auf einmal …“, `tests/Trendo.App.Tests/ClaudeLiveSyncUiTests.cs:37`) |
| 14 | E2E-Tests | TEILWEISE | UI-Tests headless; IFC echt; Blender-Foto echt belegt, im Test ersetzt |
| 15 | Bedienung | TEILWEISE | Entf, R, Pfeile, Leertaste-Ziehen, Mausrad (`FloorPlanView.cs:227-260`), Stufen (`Spec01.cs:39`); Rechtsklick-Menü im Plan nicht gefunden; kein Drag-and-drop von Dateien |

**Gesamturteil: TEILWEISE (nah an vollwertig).** Echter Editor mit echtem Datenmodell und brauchbaren Ausgaben; es fehlt der Weg, fremde Pläne zu übernehmen.
**Was fehlt:** 1. DXF- und IFC-Import in den Plan; 2. DXF-Rundreise-Test und Beleg mit einem CAD-Programm; 3. Rechtsklick-Menü; 4. Schnitt/Ansicht und Bemaßungsketten für Bauantrags-taugliche Pläne (nicht geprüft, ob vorhanden); 5. mehrere Geschosse im IFC (Export nimmt das „aktive Geschoss“, `Spec01Ifc.cs:29`).
**Abnahmekriterien:** Ein DXF eines Architekten wird geladen, Wände werden erkannt oder nachgezeichnet, Flächen stimmen auf 1 %; DXF-Export öffnet in LibreCAD/QCAD mit allen sechs Ebenen; ein zweigeschossiges Haus landet vollständig im IFC.
**Aufwand:** mittel · **Risiko:** niedrig.
**Dateien:** `App/MainWindow.Spec01.cs`, `Spec01Ifc.cs`, `Spec01IfcView.cs`, `App/FloorPlanView.cs`, `App/MainWindow.PlanPhoto.cs`, `Core/Arch/FloorPlan.cs`, `Core/Arch/PlanDrawing.cs`, `Core/Arch/IfcExchange.cs`, `Core/Arch/Plan3D.cs`.

---

## SPEC-02 Schaltungen berechnen (elektronik, mit Hülle)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG | Schaltplan-Zeichenfläche auf Raster (`App/CircuitCanvas.cs`, `App/MainWindow.Spec02.cs:84`) und Platinen-Ansicht (`Spec02.cs:67`) |
| 2 | Werkzeuge | TEILWEISE | Bauteile: Draht, R, C, L, Quellen, Schalter, Diode, LED, Lampe, Transistor, OPV (`Core/Elektro/Circuit.cs:118,132-133`); keine ICs, keine Bibliothek |
| 3 | Neu | VOLLWERTIG | acht Vorlagen (`Spec02.cs:50-57`) |
| 4 | Import | FEHLT | kein Dateiimport in `Spec02.cs`/`CircuitCanvas.cs` gefunden |
| 5, 6, 8, 9 | Bearbeiten, Rückgängig, Autosave, Wiederherstellung | VOLLWERTIG | Hülle |
| 7 | Vorschau | VOLLWERTIG | Live-Simulation mit Tempo, Oszilloskop, Bode (`Spec02.cs:45,62-64,57`) |
| 10 | Export | VOLLWERTIG | Gerber-ZIP, SPICE-Netzliste `.cir`, Stückliste CSV, SVG (`Spec02.cs:76-79`) |
| 11 | Rundreise | NICHT GETESTET | Netzliste in ngspice/LTspice geöffnet oder Gerber in einem Betrachter: kein Beleg |
| 12 | Fachfunktion | VOLLWERTIG | eigene Simulation stimmt mit SpiceSharp überein, größte Abweichung 0,059 mV (`reports/alle-studios/spicesharp-gegenprobe-e2e.md`, Tests `SpiceCrossCheckTest…`) |
| 13 | Claude/KI | TEILWEISE | `schaltung.rechnen` gibt nur eine Zahl als Text (`MeasurePromptAdapter.cs:96`); Schaltplan ändern nur über den allgemeinen Dokumentweg |
| 14 | E2E-Tests | TEILWEISE | „SPEC-02 UI: Platine – … Leiterbahnen verlegen …, Gerber-ZIP“; Gegenprobe echt; Fertigungsdaten ungeprüft |
| 15 | Bedienung | TEILWEISE | Maus, R dreht (Test oben), Strg+Q, F11 (Test „G8 … G9/G13“); Rechtsklick auf der Fläche nicht gefunden |

**Gesamturteil: TEILWEISE (nah an vollwertig).** Zeichnen, Simulieren und Ausgaben sind echt und nachgerechnet; für mehr als Lernschaltungen fehlen Import und Bauteilvielfalt.
**Was fehlt:** 1. Gerber-Prüfung mit einem Fremdwerkzeug (gerbv/KiCad) als Beleg; 2. Import SPICE-Netzliste oder KiCad-Schaltplan; 3. Bauteilbibliothek mit ICs und Gehäusen; 4. KI-Aktion, die eine Schaltung als Dokument anlegt statt eine Zahl auszugeben; 5. Rechtsklick-Menü.
**Abnahmekriterien:** Gerber-ZIP der Vorlage „Transistor als Schalter“ lädt fehlerfrei in gerbv; die `.cir` läuft in ngspice und liefert dieselben Knotenspannungen auf 1 mV; der Auftrag „LED an 9 V mit Vorwiderstand“ erzeugt ein offenes, simulierbares Dokument.
**Aufwand:** mittel · **Risiko:** niedrig.
**Dateien:** `App/MainWindow.Spec02.cs`, `App/CircuitCanvas.cs`, `Core/Elektro/Circuit.cs`, `Core/Elektro/Pcb.cs`, `Core/Elektro/AcAnalysis.cs`, `Core/Elektro/CircuitDrawing.cs`, `Core/W6/Electronics*.cs`.

---

## SPEC-03 Statik nachrechnen (technik, mit Hülle)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG | Tragwerks-Zeichenfläche: Stäbe zeichnen, Lager/Lasten klicken (`App/FrameCanvas.cs`, `App/MainWindow.Spec03.cs:70`, Test „SPEC-03 UI: Tragwerk – Stab mit der Maus zeichnen …“) |
| 2 | Werkzeuge | VOLLWERTIG | Lagerarten, Gelenke, Strecken-/Trapez-/Einzellasten, Lagersetzung, Eigengewicht (`Core/Statik/Frame.cs:145-177`, `Spec03.cs:57`) |
| 3 | Neu | VOLLWERTIG | sechs Vorlagen (`Spec03.cs:36-41`) |
| 4 | Import | FEHLT | kein Dateiimport gefunden |
| 5, 6, 8, 9 | Bearbeiten, Rückgängig, Autosave, Wiederherstellung | VOLLWERTIG | Hülle |
| 7 | Vorschau | VOLLWERTIG | M/V/N-Linien, Biegelinie, Reaktionen, 3D-Ansicht (`Spec03.cs:46-51`) |
| 10 | Export | TEILWEISE | PDF-Bericht, Eurocode-Nachweis `.md`, SVG, CSV (`Spec03.cs:62-65`); kein Austauschformat (DXF, IFC-Struktur, SAF) |
| 11 | Rundreise | FEHLT | nichts zum Wiedereinlesen außer `.tdoc` |
| 12 | Fachfunktion | TEILWEISE | Weggrößenverfahren für ebene Rahmen (`Core/Statik/Frame.cs:230-232`), Eurocode EC 3/EC 5 mit 1,35·G + 1,5·Q (`Spec03.cs:55`, `Core/Statik/Eurocode.cs`); nur 2D, Gegenprobe gegen Fremdsoftware: kein Beleg gefunden (nicht geprüft, ob Tests Handrechnungen vergleichen) |
| 13 | Claude/KI | TEILWEISE | `balken.pruefen` gibt nur Prüftext aus (`MeasurePromptAdapter.cs:122`) |
| 14 | E2E-Tests | TEILWEISE | drei UI-Tests (siehe Testnamen „SPEC-03 UI: …“); kein externer Vergleich |
| 15 | Bedienung | TEILWEISE | Stufen (Test „Bedienstufen: Einfach blendet Fachwerkzeuge aus …“); Rechtsklick nicht gefunden |

**Gesamturteil: TEILWEISE.** Nachvollziehbare Rechnung mit echter Zeichenfläche und Bericht; begrenzt auf ebene Stabwerke und ohne Austausch mit anderer Software.
**Was fehlt:** 1. belegte Gegenprobe (z. B. gegen ein freies FEM-Werkzeug oder Tabellenwerte) wie bei SPEC-02; 2. Lastfälle und Kombinationen als eigene Objekte; 3. DXF-Export des Systems und Import; 4. Nachweis-Bericht als PDF mit Formeln je Schritt; 5. KI-Aktion, die ein Tragwerk-Dokument anlegt.
**Abnahmekriterien:** Einfeldträger, Durchlaufträger und Zweigelenkrahmen stimmen mit einer Referenzlösung auf 0,5 % überein (Beleg unter `reports/`); der PDF-Bericht zeigt je Nachweis Eingangswerte, Formel und Ausnutzung; ein bewegliches System wird gemeldet (Test vorhanden).
**Aufwand:** mittel · **Risiko:** mittel (fachliche Haftung; Hinweis „keine Prüfung durch eine Fachperson“ steht in `CapabilityCatalog.cs:64`).
**Dateien:** `App/MainWindow.Spec03.cs`, `App/FrameCanvas.cs`, `Core/Statik/Frame.cs`, `Core/Statik/Eurocode.cs`, `Core/Statik/FrameDrawing.cs`, `Core/Statik/Frame3D.cs`.

---

## SPEC-10 Geräte überwachen (zwilling, ohne Hülle, in SPEC-05 eingegliedert)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Anlagenbaum, Kacheln, Zeitdiagramm (`App/MainWindow.Spec10.cs:8`); Anzeige, keine Bearbeitungsfläche |
| 2 | Werkzeuge | TEILWEISE | PC messen, Live alle 5 s, Zeiträume, Speicher begrenzen (`Spec10.cs:33,54,215`) |
| 3–5 | Neu, Import, Bearbeiten | TEILWEISE | Anlagen/Sensoren anlegen, CSV-Import (Katalogtest „SPEC-10: Zwilling mit Sensoren, CSV-Import, Ampel …“) |
| 6 | Rückgängig | FEHLT | kein Treffer in `Spec10*.cs` |
| 7 | Vorschau | VOLLWERTIG | Live-Diagramm |
| 8 | Speichern | TEILWEISE | `Twin.Save()` direkt nach Änderungen (`Spec10.cs:171,215`) |
| 9 | Wiederherstellung | nicht geprüft | – |
| 10–11 | Export, Rundreise | TEILWEISE | CSV, SVG (Katalog-Formate); Rundreise nicht geprüft |
| 12 | Fachfunktion | VOLLWERTIG | MQTT-Abo mit echtem Broker im Test (`reports/alle-studios/mqtt-e2e.md`, Test `TwinMqttTests.Real_broker`); nur lesen (`App/MainWindow.Spec10Mqtt.cs:8`) |
| 13 | Claude/KI | TEILWEISE | `messreihe.zeigen` nur Text (`MeasurePromptAdapter.cs:148`) |
| 14 | E2E-Tests | TEILWEISE | Broker echt, aber im selben Prozess; kein echtes Gerät |
| 15 | Bedienung | nicht geprüft | – |

**Gesamturteil: TEILWEISE.** Funktionierende Messwert-Anzeige mit echtem MQTT; kein „digitaler Zwilling“ im Fachsinn und ohne Rückgängig.
**Was fehlt:** Rückgängig für Stammdaten, Alarmregeln mit Benachrichtigung (nicht geprüft, ob vorhanden), weitere Quellen (Modbus, HTTP), Lauf gegen ein echtes Gerät.
**Abnahmekriterien:** Ein echter Sensor (z. B. Tasmota-Steckdose) liefert 24 h Werte ohne Lücke; eine Grenzwertverletzung erzeugt genau eine Meldung; ein versehentlich gelöschter Sensor lässt sich zurückholen.
**Aufwand:** mittel · **Risiko:** niedrig. **Dateien:** `App/MainWindow.Spec10.cs`, `Spec10Mqtt.cs`, `Core/W6/DigitalTwin*.cs`, `Core/W6/TwinMqtt.cs`, `Core/W6/TwinChart.cs`.

---

## SPEC-13 Routen & Karten (karten, mit Hülle)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG | Karte mit Zoomen, Schieben, Route/Fläche zeichnen, Messen (`App/MapCanvas.cs`, `App/MainWindow.Spec13.cs:67`, Test „SPEC-13 UI: Route mit der Maus zeichnen …“) |
| 2 | Werkzeuge | TEILWEISE | Route, Wegpunkt, Fläche, Messen, Höhen ergänzen, Route entlang von Wegen nur über **selbst eingetragenen** OSRM-Server (`Core/Karten/GeoServices.cs:63-64`) |
| 3 | Neu | VOLLWERTIG | zwei Vorlagen (`Spec13.cs:48-49`) |
| 4 | Import | TEILWEISE | GPX über Pfadfeld (`Spec13.cs:83-87,174`); kein KML-/GeoJSON-Import gefunden, kein Drag-and-drop |
| 5, 6, 8, 9 | Bearbeiten, Rückgängig, Autosave, Wiederherstellung | VOLLWERTIG | Hülle, `MapEdit` (`Spec13.cs:87`) |
| 7 | Vorschau | VOLLWERTIG | OSM-Kacheln, Offline-Paket, Vektorkarte (`Spec13.cs:54`, `reports/alle-studios/vektorkarten-e2e.md`, `pmtiles-e2e.md`) |
| 10 | Export | VOLLWERTIG | GPX, GeoJSON, KML (`Spec13.cs:60-62`) |
| 11 | Rundreise | TEILWEISE | GPX lesen und schreiben getestet (Katalogtests F02, F20); Fremdgerät/QGIS: nicht geprüft |
| 12 | Fachfunktion | VOLLWERTIG | Strecke, Höhenmeter, Wanderzeit, UTM, Fläche (Tests SPEC-13 F13–F20) |
| 13 | Claude/KI | TEILWEISE | `route.messen` nur Text (`MeasurePromptAdapter.cs:174`); `CapabilityCatalog.cs:66` behauptet „Es wird keine Karte geladen“ |
| 14 | E2E-Tests | TEILWEISE | PMTiles und Vektorkarte echt; Höhen- und Routendienst ersetzt (Test „Höhen ergänzen fragt erst …“) |
| 15 | Bedienung | TEILWEISE | Maus, Doppelklick, Enter, Esc (`Spec13.cs:67`); Rechtsklick nicht gefunden |

**Gesamturteil: TEILWEISE (nah an vollwertig für Tourenplanung).** Echte Karte, echte Dateien; Routing hängt an einem Server, den der Nutzer selbst besorgen muss.
**Was fehlt:** 1. mitgelieferter oder einrichtbarer Offline-Router; 2. Import KML/GeoJSON, Drag-and-drop; 3. Adresssuche (nicht geprüft, ob vorhanden); 4. KI-Aktion, die eine Tour als Dokument anlegt; 5. Rechtsklick „Wegpunkt hier“.
**Abnahmekriterien:** Ohne eigene Servereinrichtung entsteht zwischen zwei Orten eine Route entlang von Wegen; eine KML-Datei aus Google Earth wird geladen; exportiertes GPX lädt in einem Fremdprogramm mit gleicher Länge (±0,1 %).
**Aufwand:** mittel · **Risiko:** mittel (Kartendaten, Nutzungsbedingungen).
**Dateien:** `App/MainWindow.Spec13.cs`, `Spec13Vector.cs`, `App/MapCanvas.cs`, `Core/Karten/*`, `Core/W6/Geo*.cs`.

---

## SPEC-14 Fräsen & Fertigen (cnc, mit Hülle)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | G-Code-Texteditor links, Bahnvorschau rechts (`App/MainWindow.Spec14.cs:66`, `App/CncPathView.cs`, 80 Zeilen); keine Zeichenfläche für Geometrie |
| 2 | Werkzeuge | TEILWEISE | Prüfen, Simulation, Nullpunkt, drei Generatoren: Tasche, Kontur, Lochkreis – jeweils Rechteck/Kreis mit festen Parametern (`Spec14.cs:52-54`, `Core/W6/Cnc2.cs:35,54,69`) |
| 3 | Neu | VOLLWERTIG | vier Vorlagen (`Spec14.cs:34-37`) |
| 4 | Import | TEILWEISE | G-Code-Datei über Pfadfeld (`Spec14.cs:188-189`); kein DXF/SVG/STL |
| 5, 6, 8, 9 | Bearbeiten, Rückgängig, Autosave, Wiederherstellung | VOLLWERTIG | Hülle |
| 7 | Vorschau | VOLLWERTIG | Bahn-Simulation mit Regler, Werkstück in 3D mit Abtrag (`Spec14.cs:42-49`, `reports/alle-studios/realismus-werkzeuge-e2e.md`) |
| 10 | Export | TEILWEISE | `.nc`, SVG, Prüfbericht (`Spec14.cs:59-61`); kein Postprozessor je Steuerung |
| 11 | Rundreise | TEILWEISE | eigener G-Code wird wieder gelesen (Test „SPEC-14: G-Code (G0/G1/G2/G3, G20/G91) …“) |
| 12 | Fachfunktion | TEILWEISE | Prüfung und Schnittwerte echt (`Core/W6/Cnc.cs:83`); **kein CAM** aus Zeichnung oder Modell |
| 13 | Claude/KI | TEILWEISE | `fraese.rechnen` nur Text, `fraese.starten` zu Recht Blocked (`MeasurePromptAdapter.cs:200,227`); G-Code-Feld kann Claude füllen (allgemeiner Weg) |
| 14 | E2E-Tests | TEILWEISE | „SPEC-14 UI: G-Code eintippen → Prüfung findet Fehler …“; keine Maschine, kein Fremdsimulator |
| 15 | Bedienung | TEILWEISE | F5, Strg+Enter (`Spec14.cs:66`); Rechtsklick nicht gefunden |

**Gesamturteil: TEILWEISE.** Guter G-Code-Prüfer und Simulator; „Fertigen“ aus einem eigenen Teil geht nicht.
**Was fehlt:** 1. CAM: Bahnen aus DXF/SVG-Konturen und aus CAD-Körpern (Anschluss an Trendo CAD); 2. Werkzeugtabelle und Radiuskorrektur für beliebige Konturen; 3. Postprozessoren (GRBL, LinuxCNC, Mach); 4. Gegenprobe des erzeugten Codes in einem Fremdsimulator; 5. DXF-Import.
**Abnahmekriterien:** Eine DXF-Kontur mit Innenausschnitt ergibt G-Code, dessen simulierte Endkontur ≤ 0,05 mm abweicht; derselbe Code läuft fehlerfrei in einem freien Fremdsimulator (Beleg); ein Teil aus Trendo CAD lässt sich ohne Umweg zum Fräsen übergeben.
**Aufwand:** groß · **Risiko:** hoch (falscher G-Code kann Maschinen beschädigen; Regel „Gefährliche Realwelt-Aktoren“ in `CLAUDE.md`).
**Dateien:** `App/MainWindow.Spec14.cs`, `App/CncPathView.cs`, `Core/W6/Cnc.cs`, `Cnc2.cs`, `CncSim.cs`, `CncStock.cs`.

---

## SPEC-16 Auto & Fahrten (fahrzeug, ohne Hülle, aus der Liste entfernt)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Tabellen mit Zellen-Eingabe für Tank- und Fahrtenbuch (`App/MainWindow.Spec16.cs:137-139,160`); Fachlich ist eine Liste hier angemessen |
| 2–5 | Werkzeuge, Neu, Import, Bearbeiten | TEILWEISE | Fahrzeuge anlegen, Schnelleingabe, Monate festschreiben (`Spec16.cs:11,160`); Import: nicht geprüft |
| 6 | Rückgängig | FEHLT | kein Treffer in `Spec16.cs` |
| 8 | Speichern | TEILWEISE | `VehicleBook` im Datenordner (`Spec16.cs:16-17`); Autosave-Verhalten nicht geprüft |
| 10 | Export | TEILWEISE | CSV (Katalog-Formate); PDF-Fahrtenbuch fürs Finanzamt: nicht geprüft |
| 12 | Fachfunktion | VOLLWERTIG | Volltank-Methode, Lückenprüfung, Anteile geschäftlich/privat (`Spec16.cs:81`, Katalogtest „SPEC-16: Tankbuch (Volltank-Methode), Fahrtenbuch mit Lüc…“) |
| 13 | Claude/KI | TEILWEISE | `fahrt.kosten` nur Text (`MeasurePromptAdapter.cs:257`) |
| 14 | E2E-Tests | TEILWEISE | Kern- und UI-Tests, keine externen Werkzeuge nötig |

**Gesamturteil: TEILWEISE.** Brauchbares Fahrzeugbuch als Formularprogramm; kein Rückgängig, nur CSV.
**Was fehlt:** Rückgängig, PDF-Ausgabe des Fahrtenbuchs, Import aus Spritmonitor-CSV, Umzug auf die Hülle.
**Abnahmekriterien:** Ein gelöschter Eintrag lässt sich zurückholen; ein festgeschriebener Monat ist als PDF mit Summen ausgebbar; eine Lücke im Kilometerstand wird gemeldet.
**Aufwand:** klein · **Risiko:** niedrig. **Dateien:** `App/MainWindow.Spec16.cs`, `Core/W6/Vehicles*.cs`, `Core/W6/VehicleChart.cs`, `Core/W6/Logbook.cs`.

---

## CORE-09 Kameras & Roboter steuern (roboter, ohne Hülle, aus der Liste entfernt)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Roboter-Planungsansicht und Kamerabild (`App/MainWindow.Core09.cs:12`); Wegpunkte als Liste |
| 2 | Werkzeuge | TEILWEISE | Kameras suchen (lokal, ONVIF), Foto, Aufnahme, Objekte erkennen (YOLOX), Arm planen, Weg abfahren, Not-Aus (`App/MainWindow.Core09Cam.cs`, Knöpfe `cam-*`, `rob-*`; `Core09Objects.cs:6`) |
| 4 | Import | TEILWEISE | Bild laden und auswerten (`Core09.cs:130-154`) |
| 6, 8, 9 | Rückgängig, Autosave, Wiederherstellung | FEHLT | ohne Hülle, kein Treffer |
| 7 | Vorschau | TEILWEISE | Simulation des Arms (`Core09.cs:33`) |
| 10 | Export | TEILWEISE | CSV, Bilder, Aufnahmen (Katalog-Formate) |
| 12 | Fachfunktion | NICHT GETESTET | echte Bewegung nur nach „Ja, wirklich bewegen“ (`Core09Cam.cs`, `rob-yes`); kein Beleg eines Laufs an echter Hardware; YOLOX echt belegt (`reports/alle-studios/yolox-e2e.md`) |
| 13 | Claude/KI | BLOCKIERT | `kamera.nennen` nur Namen als Text (`Core/PromptAction/ConnectedAdapters.cs:86`), `kamera.steuern` gesperrt (`SystemPromptAdapter.cs:253`) |
| 14 | E2E-Tests | NICHT GETESTET | Objekterkennung im Test ersetzt (`Core09Objects.cs:10`); Roboter nur simuliert |

**Gesamturteil: TEILWEISE.** Kamera-Werkzeuge und eine Arm-Simulation; „Roboter steuern“ ist ohne Hardware-Nachweis ein Versprechen.
**Was fehlt:** belegter Lauf mit einem echten Arm oder ehrliche Umbenennung in „Roboter planen“; Projektdokument mit Rückgängig; Export eines Fahrprogramms.
**Abnahmekriterien:** Eine echte IP-Kamera liefert Bild und Aufnahme (Beleg); ein geplanter Weg wird als Datei ausgegeben und in der Simulation ohne Kollision abgefahren; ohne Bestätigung bewegt sich nichts (Test vorhanden).
**Aufwand:** groß · **Risiko:** hoch (Realwelt-Aktoren).
**Dateien:** `App/MainWindow.Core09.cs`, `Core09Cam.cs`, `Core09Objects.cs`, `Core/W6/Robotics*.cs`, `Core/W6/RobotDrive.cs`, `Core/W6/Cameras.cs`, `Core/W6/ObjectDetect.cs`.

---

## Auffälligkeiten der Gruppe

**Kein Schutz vor Datenverlust in Programmen ohne Hülle**
- 3D-Studio: Rückgängig nur 30 Schritte im Speicher, kein Wiederholen, kein Autosave, keine Wiederherstellung (`App/MainWindow.W4Studio3D.cs:27,392,450,720-724`).
- Trendo CAD: kein Autosave; `CadDirty` wird nur in der Statuszeile gezeigt (`App/MainWindow.Cad.cs:18,165`). Das Schließen der App prüft nur die Ersteinrichtung (`App/MainWindow.InstallOverlay.cs:72`).
- 3D-Druck, scan3d, xr, zwilling, fahrzeug, roboter, Werkstatt: kein Rückgängig gefunden.

**KI-Aktionen bleiben weit hinter den Programmen zurück**
- Zehn von 17 Aktionen der Gruppe liefern nur `["txt"]`: `schaltung.rechnen`, `balken.pruefen`, `messreihe.zeigen`, `route.messen`, `fraese.rechnen`, `fahrt.kosten`, `konsole.plan`, `bildzeit.pruefen`, `kamera.nennen` (`Core/PromptAction/MeasurePromptAdapter.cs:96-257`, `StudyPromptAdapter.cs:231,356`, `ConnectedAdapters.cs:86`). Das ist genau der Fall „Funktion gibt nur Text aus, wo ein Produkt versprochen wird“.
- Vier sind Blocked: `spiel.starten`, `modell.blender`, `scan.starten`, `fraese.starten` (letztere zu Recht).
- Echte Dateien erzeugen nur `modell.bauen`, `spiel.browser`, `spiel.unreal` (nur `.uproject`), `wohnung.flaeche`.
- `scan.starten` ist doppelt angelegt (`StudyPromptAdapter.cs:381` und `SystemPromptAdapter.cs:31`).
- Der zweite Weg (Claude ändert das `.tdoc` oder bedient die Seite) ist nur für Grundriss, 3D-Teileliste und Spiel-Level mit eigenem Test belegt.

**Doku ≠ Code**
- `Core/PromptAction/CapabilityCatalog.cs:61` „Es gibt keine Punktwolke“ – das Programm Punktwolke existiert (`App/MainWindow.Spec09.cs:25`).
- `CapabilityCatalog.cs:66` „Es wird keine Karte geladen“ – die Karte lädt OSM-Kacheln (`App/MainWindow.Spec13.cs:54`).
- `CapabilityCatalog.cs:65` „Es wird kein Gerät verbunden“ – MQTT-Abo vorhanden (`App/MainWindow.Spec10Mqtt.cs:37`).
- `CapabilityCatalog.cs:60` nennt für SPEC-04 „Raumskizze“ – das Programm ist ein Modell-Prüfer (`App/MainWindow.Spec04.cs:9`).
- `Core/GameStudio.cs:384` Kommentar „leeres Unreal-Projekt (Blueprint, ohne C++)“ – `GameDesign.BuildUnreal` schreibt danach ein C++-Spiel hinein (`Core/Programs/GameDesign.cs:226-230`).
- `docs/programme/katalog.json`: Eintrag „3d“ nennt als Formate `.docx`, `.mp3`, `.xlsx`, `.odt` und als Abhängigkeiten Tesseract, LibreOffice, Hunspell, OBS; Eintrag „architektur“ führt den Test „Fräsen UI: Werkstück in 3D …“. Die Felder sind offenbar automatisch gesammelt und für Aussagen über den Funktionsumfang unbrauchbar.
- `katalog.json` führt xr, fahrzeug und roboter als `IMPLEMENTED / PC_VERIFIED`, obwohl die Studios in `StudioCatalog.Removed` stehen; sie zählen in den „125 Programmen“ mit.
- 3d-druck führt `.gcode` als Format, obwohl Trendo keinen G-Code schreibt (`Core/Cad/Slicer.cs:5`).

**Versprechen größer als der Stand**
- „bis zum AAA+-Projekt in Unreal Engine 5“ (`App/MainWindow.ProgGame.cs:22`) – AAA+ setzt fünf Zeilen in `DefaultEngine.ini` (`Core/Programs/GameDesign.cs:231-233`).
- Skalenstufe „Indie 2D: Mehrere Level, eigene Grafik und Musik“ (`GameDesign.cs:15`) nutzt dieselben vier festen Browser-Spielarten wie „Retro“.
- „VR & AR gestalten“ prüft und packt nur; „Kameras & Roboter steuern“ simuliert nur; „Konsolenspiele portieren“ delegiert vollständig an Claude Code.
- STEP-Export „lässt sich in FreeCAD, Fusion, SolidWorks usw. öffnen“ (`Core/Cad/CadExport.cs:110`) – als Facetten, ohne Beleg eines Öffnens.

**Echte Läufe sind Einzelbelege, keine Dauerprüfung**
- Unreal, Godot, Unity: Belege vom 06./07.11.2026 unter `reports/paket01/`; im Testsatz durchgehend ersetzt. Eine spätere Änderung am erzeugten C++/GDScript/C# fällt automatisch nicht auf.
- Ohne jeden Echtlauf-Beleg im Repo: COLMAP/Meshroom-Fotoscan, TripoSR („3D aus Bild“), Bambu-Drucker und Bambu Studio, PS2-Portierung, Gerber-Ausgabe, STEP in Fremd-CAD, Mixamo-Weg, echte Kamera/Roboter-Hardware.
- Positiv und belastbar: IFC, Open3D, assimp, glTF-Validator/-Transform, f3d, PMTiles/Vektorkarte, MQTT, SpiceSharp-Gegenprobe (`reports/alle-studios/*.md`, Echtlauf-Tests).

**`tools/blender/` und `tools/unreal/`**
- Nur drei Skripte (`inspect_asset.py`, `smoke_asset.py`, `validate_project.py`). `CLAUDE.md` nennt sie selbst „CLI-Prüfhilfen, kein getesteter End-to-End-MCP-Server“. Die eigentliche Blender-Anbindung läuft über `Cli.Run` mit erzeugten Python-Skripten (`Core/Studio3D.cs:90-114,376`, `Core/Studio3DPro.cs:91`, `Core/Studio3DEngines.cs`); real geprüft ist davon der „Für Engine“-Rundlauf und das Render-Skript.

**Bedienung allgemein**
- Kein Rechtsklick-Menü in den Zeichenflächen gefunden (kein Treffer für `ContextMenu`/`ContextRequested` in `CircuitCanvas.cs`, `FrameCanvas.cs`, `FloorPlanView.cs`, `MapCanvas.cs`, `CncPathView.cs`, `PointCloudView.cs`, `CadView.cs`); vorhanden ist nur das allgemeine „Claude fragen“ (Test „Claude im Menüband jedes Fachprogramms und Rechtsklick …“).
- Drag-and-drop von Dateien nur im 3D-Studio (`App/MainWindow.W4Studio3D.cs:257`); Karten und CNC laden Dateien über ein Pfad-Textfeld (`Spec13.cs:174`, `Spec14.cs:188`).
- Dopplung: „spiele“ (Werkstatt) und „spiele-programm“ decken dasselbe ab, mit unterschiedlichem Schutz vor Datenverlust.

**Nicht geprüft (Grenzen dieses Audits)**
- Kein Programm wurde gestartet oder gebaut; alle Aussagen stammen aus Code, Testnamen und Belegdateien.
- Inhalt der einzelnen Testmethoden wurde nur stichprobenartig gelesen; ob Statik-Tests gegen Handrechnungen prüfen, ist offen.
- `docs/STUDIO_REBUILD_4_0/`, `docs/audit/`, `docs/claude/vorbilder/*.md` und `docs/improvement-42/studio-capability-matrix.md` wurden nicht gegengelesen.
- Die Datei `Core/Programs/UnrealGame.cs` (3468 Zeilen) wurde nicht inhaltlich geprüft.
