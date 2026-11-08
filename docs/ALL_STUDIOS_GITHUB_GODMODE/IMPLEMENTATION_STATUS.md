# Umsetzungsstand (08.11.2026)

| Bereich | Stand |
|---|---|
| Bestandsaufnahme 56 Studios | erledigt |
| Runde 1–4 Recherche/Bewertung (16 Studios) | erledigt |
| CORE-05 f3d-Vorschaubilder | eingebaut, echter Lauf geprüft (reports/alle-studios/f3d-e2e.md), 10 Kern- und 1 Oberflächentest |
| CORE-05/CORE-06 Claude in Blender/Unreal/Godot (C5/C6) | MCP je Projekt + sichtbarer Start eingebaut, 10 Kern- und 1 Oberflächentest; Zusammenspiel mit dem Claude-Chat (Sitzung A, C1–C4) NOT RUN |
| STUDIO-MUSIK-01 basic-pitch (Noten auch mehrstimmig) | eingebaut als KI-Laufzeit „Noten“ (ohne TensorFlow, ONNX), echter Lauf (reports/alle-studios/basic-pitch-e2e.md), 8 Kern- und 1 Oberflächentest grün; Einrichtung per uv NOT RUN |
| Runde 1 übrige (MLT, PMTiles/MapLibre, assimp) | Freigabe erteilt 08.11.2026; assimp nur als DLL, MLT ohne offizielles Windows-Paket – NOT RUN |
| SYS-10 typst (Brief/Rechnung als PDF) | eingebaut, echter Lauf über den Adapter (reports/alle-studios/runde2-werkzeuge-e2e.md), Tests grün |
| SPEC-15 mediamtx (eigener Sendeserver, nur 127.0.0.1) | eingebaut, echter Lauf mit Teststream, Tests grün; OBS-Lauf NOT RUN |
| STUDIO-TV-01 streamlink (Das Erste/ARD, arte live) | eingebaut, echter Lauf (arte → m3u8), Tests grün; Abspielen im Player NOT RUN |
| Runde 2 übrige (Open3D) | NOT RUN |
| CORE-03 KI-Team: Team-Lauf fortsetzen (Konzept LangGraph, eigener Code) | eingebaut (5d45824cd), 3 Kerntests grün, KI-Team-Oberflächentests grün; echter Lauf mit Claude NOT RUN |
| SPEC-01 IFC-Export (IfcOpenShell) | eingebaut: Exportieren „IFC (BIM)“ + „BIM → IFC einrichten“, echter Lauf mit Vorlage, Geometrie nachgemessen (reports/alle-studios/ifc-e2e.md); Öffnen in BIM-Programmen NOT RUN |
| Runde 3 übrige Umsetzung (Node-RED-Anbindung, IFC-Ansicht ThatOpen, weitere KI-Team-Konzepte) | NOT RUN |
| SPEC-06 FSRS-Planer für Karteikarten (Runde 4) | eingebaut, umschaltbar im Lernplan, 12 Kern- und 1 Oberflächentest grün |
| Runde 4 übrige (SpiceSharp, Math.NET Numerics, Statik-Konzepte) | geplant, NOT RUN – NuGet-Pakete bzw. eigener Code |
| Runden 5+ (40 Studios) | NOT RUN |
| Web Design Studio Modul 05 (Design-Tokens) | eingebaut, 2 Tests, im Browser geprüft (docs/web-design-godmode) |
| Volltest nach den Änderungen | grün: Api 16, Core 1546, App 829 (reports/alle-studios/volltest-2026-11-08.md) |
