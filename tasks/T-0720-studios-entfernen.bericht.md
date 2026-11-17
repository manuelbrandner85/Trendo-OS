# Bericht T-0720: Drei Studios löschen

**Auftrag:** Nutzerentscheidung 16.11.2026 – „VR/AR (SPEC-04), Auto & Fahrten (SPEC-16) sowie Kameras & Roboter (CORE-09) kannst ruhig löschen“; kein anderes Studio und keine Funktion eines anderen Studios darf verloren gehen.
**Branch:** `claude/T-0720-studios-entfernen` (vom Integrationszweig `claude/T-0685-b1-integration`, T-0717 hereingeholt).

## Ergebnis in einem Satz

Auto & Fahrten (SPEC-16) und Kameras & Roboter (CORE-09) sind gelöscht. VR & AR (SPEC-04) bleibt als Studio entfernt (seit T-0607), **seine Seiten bleiben aber**: Modell-Prüfer (`xr`) und 3D-Bibliothek (`bib3d`) laufen unter derselben Kennung SPEC-04 und werden von 3D-Druck (CORE-05, „Freie 3D-Modelle suchen“) und Architektur (SPEC-01, „3D-Bibliothek öffnen“) genutzt; die 3D-Bibliothek öffnet Modelle mit „Ansehen“ im Modell-Prüfer. Alles, was an SPEC-04 hängt (KI-Auftrag Bildzeit, Team, Freie-KI-Knöpfe, Ideen, Rolle, Fachwissen), erscheint auf diesen beiden Seiten. Löschen hätte also Funktionen anderer Studios gekostet. Darum ist bei SPEC-04 nichts gelöscht.

## Stücke und Entscheidung

| Stück | Studio | Entscheidung | Grund / wer nutzt es |
|---|---|---|---|
| `MainWindow.Spec16.cs` (Seite `fahrzeug`, Fahrzeugbuch) | SPEC-16 | gelöscht | nur SPEC-16 |
| `W6/Vehicles.cs`, `Vehicles2.cs`, `VehicleChart.cs`, `Logbook.cs` (Garage, Tankbuch, Fahrtenbuch, VehiclePlus) | SPEC-16 | gelöscht | nur SPEC-16 und dessen KI-Auftrag |
| T-0716-Anschluss Fahrzeugbuch (`CarLink`, `LINK-FAHRZEUG`) | SPEC-16 | gelöscht (stand in Spec16.cs) | `LiveSlot` in `MainWindow.LinkLive.cs` **bleibt** (Baukasten, Anlagen-Monitor); nur Kommentar angepasst |
| `MainWindow.Core09.cs`, `Core09Cam.cs` (Seite `roboter`: Roboterarm, Kameras, ONVIF, Fahrt, Not-Aus des Arms) | CORE-09 | gelöscht | nur CORE-09 |
| `ArmView.cs`, `CameraNetwork.cs`, `W6/Cameras.cs`, `W6/RobotDrive.cs` | CORE-09 | gelöscht | nur CORE-09 |
| `MainWindow.Core09Objects.cs` – Kamera-Knöpfe „Objekte erkennen“ | CORE-09 | gelöscht | nur Kamera-Seite |
| ebd. – `ObjectsModel`, `ObjectsModelPath`, `ObjectsReady`, `ObjectsThreshold`, `ObjectsRunOverride` | geteilt | **behalten**, unverändert nach `MainWindow.Objekterkennung.cs` verschoben | „Bilder erkennen“ (STUDIO-VISION-01, `MainWindow.VisionErkennen.cs`); nur der Ortstext des Modells sagt jetzt „Im Programm „Bilder erkennen““ |
| `W6/ObjectDetect.cs`, `Assets/ki/trendo_objekte.py` | geteilt | behalten | Bilder erkennen, KiPyApps, VisionTags |
| `W6/Robotics.cs` – `RgbImage`, `Vision.Load` (PNG/BMP) | geteilt | **behalten**, unverändert nach `W6/Vision.cs` | Spielbild-Prüfung (`GameShotCheck`) |
| `W6/Robotics2.cs` – `VisionPlus.EncodePng`, `Homography`, `Apply` | geteilt | **behalten**, unverändert nach `W6/Vision.cs` | Dokumente scannen (`DocScan`), Tests von Spielen/Karten |
| übrige Teile von Robotics/Robotics2 (Farbsuche, Kalibrierung, Maske, Belichtung, Schärfe, Bewegung, `RobotArm`, `ArmPlus`, `ArmConfig`, `Joints`, `ProgramStep`, `MoveProfile`, `Blob`, `Exposure`, `MotionResult`) | CORE-09 | gelöscht | keine Nutzung außerhalb CORE-09 (Referenzsuche + Build) |
| KI-Aufträge `VehicleCostAdapter` (fahrt.kosten), `CameraListAdapter` (kamera.nennen), Sperre „kamera.steuern“, `PromptServices.VideoSys`, `Intent.Camera` | SPEC-16 / CORE-09 | gelöscht | nur diese Studios |
| Einträge in `CapabilityCatalog`, `StudioKnowledge` (dazu Verweis von SPEC-13 auf SPEC-16), `StudioIdeas`, `StudioProfiles`, `StudioTeams`, `HardwareCompat` (CORE-09), `Licensing` (SPEC-16), Interessen-Profil „kreativ“ (CORE-09) | SPEC-16 / CORE-09 | gelöscht | nur diese Studios |
| NotStopp: „Roboterarm Not-Aus“, Menüleiste: Seite `roboter`, Programmkatalog: „Roboter-Ansicht“, Fachauswahl `rob-*`, Render-Werkzeug `TRENDO_CAR` und E4-Fahrzeug-Demo | CORE-09 / SPEC-16 | gelöscht | hingen nur an den gelöschten Seiten |
| `StudioCatalog.ById`, `StudioIcons`, `Assets/studios/*.jpg`, `tools/studio_bilder.py` für SPEC-16/CORE-09 | – | behalten (wie SPEC-07) | requirements.yaml behält die Einträge (als entfernt markiert); Name, Symbol und Bild je Kennung verlangen `StudioIconTests`/`StudioBilder`; `StudioCatalog.Removed` hält sie aus Übersicht, Suche, Start und Desktop, alte Desktop-Symbole/Verläufe laufen auf die Statusseite statt ins Leere |
| `StudioCatalog.Removed` | – | Einträge bleiben, Kommentar ergänzt | Begründung wie eben |
| SPEC-04: `MainWindow.Spec04*.cs`, `Library3D*.cs`, `W6/Xr*.cs`, `GltfTools.cs`, KI-Auftrag Bildzeit, Team, Ideen, Rolle, Fachwissen, Freie KI, Hardware-Hinweis, MCP-Zuordnung | SPEC-04 | **alles behalten** | Modell-Prüfer und 3D-Bibliothek werden von CORE-05 und SPEC-01 genutzt (siehe oben) |
| Paketordner `docs/programme/fahrzeug/`, `docs/programme/roboter/`, Vorbild-Analysen `fahrzeugbuch-spritmonitor.md`, `roboter-robodk.md` | SPEC-16 / CORE-09 | gelöscht | Programme gibt es nicht mehr |

Entfernte Zeilen gesamt (Commit `052e8a2c5`): 88 Dateien, +203 / −4485 (Code `src`+`tools` −2621, Tests −858, Doku −1006).

## Entfernte Tests (gedeckt durch die Nutzerentscheidung)

- `CameraRobotUiTests.cs` (5), `CamObjectsUiTests.cs`, `RobotUiTests.cs` (2), `VehicleUiTests.cs` (2), `CameraRobotTests.cs` (9), `LogbookTests.cs` (3) – testen nur gelöschte Teile.
- `W6Tests`: „SPEC-16: Tankbuch …“, „CORE-09: PNG/BMP lesen, Farbobjekt …, Kinematik …“, „T-0251 (V109): Kamera-Maße …“, Klasse `SPEC16_Plus` (8), `CORE09_Plus` F11–F15, F17–F20 – gelöschte Funktionen. F16 (Homographie) **bleibt** als `Homographie_Geteilt`, weil Dokumente scannen sie nutzt.
- `ConnectedPromptTests` „Kamera nennt Geräte …“, `PromptActionTests` „Fahrtkosten rechnen …“ – gelöschte KI-Aufträge.
- `T0711_HuelleEtappe1b_Ui` „Roboter-Werkstatt“, `T0716_HuelleE4_Ui` „Fahrzeugbuch“ und „Fahrtenbuch: Festschreiben …“, `RoundSix_Ui` „Fahrtenbuch (T-0311)“.
- Angepasst (nur die gelöschten Studios herausgenommen, Rest unverändert): `NotStopp_Ui` (Roboterarm-Zeile), `Modern_Ui` T4/T7 (nur noch Geräte überwachen), `StudioIconTests` (Reiterleiste jetzt an SPEC-14 geprüft), `StudiosTechnikWissen_Ui`, `T0710_MenuBar_Ui`, `T0716_HuelleE4_Ui` Menüleiste (Baukasten und Anlagen-Monitor), `PromptActionTests` (107 statt 110 Aktionen, 78 statt 80 teilweise fähig, Listen), `W6_Catalog` (ohne SPEC-16/CORE-09), `VorbildAnalysenTests` (ohne die zwei Programmnamen).

## Nachgezogen

- Programmkatalog: `katalog.json` und `KATALOG.md` – nur die Einträge `fahrzeug` und `roboter` entfernt (123 Programme in 80 Studios). Der volle Neulauf (`TRENDO_KATALOG=.`) wurde gemacht, änderte aber zusätzlich Messwerte, Werkzeuge und Testlisten von 120 anderen Programmen (Stand anderer Pakete, Rechner); diese fremden Änderungen sind bewusst nicht übernommen.
- PromptAction-Matrix (`TRENDO_UPDATE_MATRIX=1`): zwei Zeilen weg, 81 Einträge, 107 Aktionen.
- Funktions-Baseline: 71 Zeilen entfernt – alle mit Aufrufstelle in `MainWindow.Spec16.cs` (36), `Core09Cam.cs` (20), `Core09.cs` (13), `Core09Objects.cs` (2); keine Zeile eines anderen Programms.
- `requirements.yaml`: SPEC-16 und CORE-09 `approval_status: REJECTED_REMOVED_BY_USER`, `implementation_status: REMOVED`, `test_status: NOT_RUN`, Notiz T-0720; SPEC-04 nur Notiz (Seiten bleiben). Kein PC_VERIFIED.
- `requirements-delta.md`: **nicht geändert** – die Datei ist Beleg für 37 Module (SHA-256 in requirements.yaml, geprüft von `EvidenceTests`); eine Änderung wäre eine stille Änderung eines Belegs. Die Kennzeichnung steht stattdessen in requirements.yaml.
- `huelle-umzug.md`: Nachtrag T-0720, Zeilen `fahrzeug`/`roboter` als entfernt markiert.
- `einfach-vorher-ausgeblendet.txt`: `fahrzeug: page-team`, `roboter: page-team` entfernt.

## Tests

- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core gezielt (PromptAction, Evidence, QA015, QA019, W6*, Homographie, Connected, HardwareCompat, Licensing, DocScan, GameStage3, ObjectDetect, ScanXrPro, XrHeadset, GltfTools, DecisionRegister, Plan, StudioTeams/Ideas, KiPyApps, VisionVideo, Interests/Profile, VorbildAnalysen, NoFixedUser, Letter/PlanPhoto/W3/WebsiteStudio/EchtWerkzeuge/Kreativ): 296 + 120 + 1 bestanden, 0 Fehler.
- App Lauf 1 (UebersichtEinfach, ProgramHeadDesign, Library3D, Library3DPro, T0710, T0711, T0716, FreigabenKern, StudioIcon, NotStopp, RoundSix, Modern, StudiosTechnikWissen, StudioBilder, SPEC04, XrHeadset, ScanXrPro, VisionVideoT0699): 112, 1 Fehler, 1 übersprungen. Der Fehler `SPEC04_Ui.Heavy_model_and_calcs` („Sequence contains no matching element“ bei `xr-check-0`) tritt **gleich auf dem unveränderten Integrationszweig** auf (eigener Baum auf dem Vorgänger-Commit gebaut und geprüft) – nicht durch T-0720, Seite unverändert.
- App Lauf 2 (EveryButton_AllPages, EveryButton_Alltag, Sys13_Qualitaet_Ui, StudioTeams, StudioMcp, ClaudeEverywhere, KiPy, StudiosAlle, PromptBarTests, EinfachBedienen, StartMenu_Ui, StartMenu_StudioFenster, StudioClick, GameStage3, StudioDeps, W1_Search_Access, DesignHonesty): 159, 4 Fehler. Drei davon verlangten für jede Kennung in `StudioCatalog.ById` ein Claude-Profil, einen Abdeckungs-Eintrag bzw. ein Team. Neu: `StudioCatalog.Deleted` = {SPEC-16, CORE-09} (Teilmenge von `Removed`); die drei Tests nehmen genau diese Menge aus, wie `StudioTeams_Ui` schon SPEC-07 ausnimmt. Nachlauf ClaudeEverywhere_Ui, PromptBarTests, StudioTeams_Ui, StudioIconTests, StudioBilder: 15/15 bestanden. Der vierte Fehler „Drei Studios gleichzeitig bei 1366×768 …“ (`StartMenu_StudioFenster`, Abweichung in einer Fenstergröße) ist **auf dem unveränderten Integrationszweig gleich rot** (eigener Baum auf dem Vorgänger-Commit geprüft) – nicht durch T-0720.
- Paket-Tests `python -m unittest discover -s tests/claude_handoff`: 11 OK.

## Nicht geprüft

- Kein Volltest (macht die Hauptsitzung). Keine Sichtprüfung am Bildschirm (keine Oberfläche neu gebaut, nur entfernt).
- Abnahmekriterien 1–6 betreffen ein Löschpaket nicht: 1 FEHLT/entfällt, 2 entfällt, 3 entfällt, 4 entfällt, 5 entfällt, 6 entfällt.

## Offene Punkte (Nutzerentscheidung nötig)

- SPEC-04: Soll der Modell-Prüfer die VR-eigenen Teile verlieren (Zielgeräte/Brillen-Budgets, Brillen-Ansicht, „Für die Brille reparieren“, Komfort-Rechner, KI-Auftrag Bildzeit) und die Seitengruppe neutral heißen (heute steht im Reiter „VR & AR gestalten“)? Das wäre ein Umbau des Modell-Prüfers, den 3D-Druck und Architektur über die 3D-Bibliothek nutzen – darum nicht ohne Freigabe.
- `SPEC04_Ui.Heavy_model_and_calcs` und `StartMenu_StudioFenster` „Drei Studios gleichzeitig bei 1366×768“ sind schon auf dem Integrationszweig rot (nicht Teil dieses Pakets).
- Historische Doku (`docs/w6-katalog/SPEC-16.md`, `CORE-09.md`, `docs/STUDIO_REBUILD_4_0/studios/SPEC-16/…`, Build-Logs, `requirements-delta.md`) bleibt als Verlauf unverändert.

**Status:** gebaut, gezielt getestet, gepusht auf den eigenen Zweig; keine Abnahme durch eine Person.
