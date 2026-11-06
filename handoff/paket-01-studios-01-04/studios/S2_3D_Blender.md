# STUDIO 2 – 3D & Blender – genehmigte Verbesserungen

Trendo-Katalog: `CORE-05` · **20 Nutzer-JA** · Stand: **APPROVED_SPEC** (Status der einzelnen Funktionen per Live-Audit verifizieren)

## Bewährte Steuerung
Reihenfolge für jede Funktion: vorhandenes Trendo prüfen → REA-Clean-Room/Native Integration bewerten → Schnittstellen/Dateien/Versionierung definieren → echten vertikalen Slice bauen → positive/negative Tests → Performance/Security/License/QC → Berichte/Revision. Unity-Relevanz je Studio steht in `03_GLOBALE_REGELN_REA_UNITY.md`.

| Nr. | Verbindliche Verbesserung | Konkreter Funktionsumfang | Claude-Agent | Ziel-Artefakt |
|---|---|---|---|---|
| 01 | **Ultimate 3D Modeling** | Professionelle Mesh-Bearbeitung, Subdivision, Boolean, Sculpting und Hard-Surface-Modellierung. | `12-blender-assets` | Editierbares Mesh-Projekt und Export |
| 02 | **AI Reference-to-3D** | Aus Bildern und Beschreibungen 3D-Modelle rekonstruieren, mit überprüfbarer Maß- und Formtreue. | `12-blender-assets` | Referenz, 3D-Ergebnis, Vergleich |
| 03 | **Photorealistic Character Creator** | Menschen, Gesichter, Tiere, Kreaturen, Haare, Haut und Kleidung. | `12-blender-assets` | Charakter mit Material- und Rig-Daten |
| 04 | **Advanced Sculpting Studio** | Organische Formen mit digitalen Pinseln modellieren und verfeinern. | `12-blender-assets` | Sculpt-Layer/Brush-Verlauf und Mesh |
| 05 | **Automatic Retopology Director** | High-Poly-Modelle in optimierte Low-Poly-Modelle überführen. | `12-blender-assets` | Quell- und Zielmesh mit Qualitätsmetriken |
| 06 | **UV & Texture Studio** | UV-Unwrapping, UDIM, Texture Painting, Baking und hochwertige Texturen. | `12-blender-assets` | UV-Layout, Bake, Texturen |
| 07 | **PBR Material Laboratory** | Realistische Materialien wie Glas, Haut, Metall, Holz, Wasser und Stoff. | `12-blender-assets` | PBR-Materialgraph und Testszene |
| 08 | **Geometry Nodes GODMODE** | Prozedurale Gebäude, Landschaften, Vegetation und komplexe Objektsysteme. | `12-blender-assets` | Parametrische Node-Gruppe mit Seeds |
| 09 | **Professional Rigging Studio** | Skelette, Gewichte, IK/FK, Gesichtsrigger und Rig-Validierung. | `12-blender-assets` | Rig mit Pose- und Deformationstests |
| 10 | **Ultimate Animation Director** | Keyframes, Motion Capture, Retargeting, Shape Keys und Animationstimeline. | `12-blender-assets` | Clipbibliothek und Abspielnachweis |
| 11 | **Physics & Simulation Studio** | Stoffe, Haare, Flüssigkeiten, Rauch, Feuer, Kollisionen und Simulationen. | `12-blender-assets` | Reproduzierbarer Sim-Cache |
| 12 | **Cinematic Rendering Director** | Cycles, EEVEE, Lichtsetzung, Kameras, Compositing und Renderverwaltung. | `14-media-vfx` | Szenendatei, Bildsequenz, Renderlog |
| 13 | **Architecture & Environment Creator** | Gebäude, Innenräume, Städte, Landschaften und komplette Umgebungen. | `12-blender-assets` | Modulare Szene, Maßstab und Assetliste |
| 14 | **Vehicle & Mechanical Creator** | Autos, Flugzeuge, Maschinen, bewegliche Bauteile und technische Objekte. | `12-blender-assets` | Bauteilhierarchie, Pivot/Bewegungstest |
| 15 | **3D Scan Reconstruction** | Fotogrammetrie, Punktwolken, Scanbereinigung und Mesh-Rekonstruktion. | `12-blender-assets` | Pointcloud, Mesh, Unsicherheitsbericht |
| 16 | **Universal 3D Asset Library** | Wiederverwendbare Modelle, Materialien, Animationen, Vorschaubilder und Lizenznachweise. | `06-data-memory` | Index, Vorschau, Lizenz- und Provenienzdaten |
| 17 | **Blender–Unreal–Web Fusion** | Geprüfte FBX-, glTF-, USD- und weitere Exportpipelines für Spiele und Webseiten. | `12-blender-assets` | Import-/Exportvergleich pro Engine |
| 18 | **Autonomous Blender Production** | Claude steuert Blender über Python und geeignete Adapter, erstellt echte Dateien und prüft Ergebnisse. | `12-blender-assets` | Laufprotokoll, .blend und Preview |
| 19 | **3D Optimization & QA Director** | Geometrie, LOD, Texturen, UVs, Rigging, Speicherverbrauch und Exportqualität prüfen. | `19-qa-verification` | Messbericht mit Grenzwerten |
| 20 | **Native Trendo 3D Editor** | Professionelle Arbeitsfläche mit Viewport, Szenenhierarchie, Eigenschaften, Drag-and-drop und direkter Bearbeitung. | `04-ux-avalonia` | Nativ editierbares 3D-Dokument und Undo |

## Technische Grenzen und QA-Trigger
- `CORE-05` und `MainWindow.Studio3DPro.cs` / `MainWindow.Studio3DViewport.cs` zuerst prüfen; Blender/Python API nur bei vorhandener kompatibler Installation.
- Blender-Projekt `.blend` als Quelle, Texturen und Lizenznachweise im `ProjectStore`; Export zu Unity/Unreal/Godot mit objektbezogenem Importtest.
- REA: Native Trendo-Grundfunktionen entwickeln; vollständigen Blender-Nachbau **nicht** als Pflicht verlangen. Fremde Bibliotheken lizenzprüfen.
- Negativtest: Materialverlust, fehlende Normalen, kaputtes Armature, polygon-budget-Überlauf, FBX ohne Exporter, ungültiger Pfad.

## Spezifische Testfälle
Zu allen 20 Verbesserungen stehen in `qa/TESTFAELLE.csv` jeweils ein überprüfbarer Positiv- und Negativfall (insgesamt 40). Die Tests sind **nicht** durchgeführt; Claude muss automatisierte Testprogramme nach Implementierung hinterlegen.

## Ergebnisbericht
Jede Teilaufgabe erzeugt: veränderte echte Projektdatei, Vorschaubild oder Audio, Build/Test-Log mit Exitcode, Performance-/Lizenzbericht, Git-Diff, verbleibende Einschränkungen. Kein TESTED ohne Beleg.
