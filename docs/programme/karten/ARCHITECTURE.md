# Karten & Touren – Architektur

Studio: Routen & Karten (SPEC-13) · Bereich: Technik & Fachgebiete · Seite: `karten` · Stand: 0.9.28 (Stand 4ec46e4)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.Spec13.cs`

## Bedienelemente der Arbeitsfläche
- Karte
- Menüband
- Eigenschaften
- Statusleiste
- Dokument-Reiter

## Fach-Engine (Trendo.Core)
- `MapDoc`
- `GeoPlus`
- `RouteService`
- `MapPoint`
- `ElevationService`
- `TilePack`

## Dateiformate (im Code belegt)
- .geojson
- .gpx
- .json
- .kml
- .tdoc
- .txt
- .zip

## Externe Programme und Dienste
- OpenStreetMap-Kacheln (Internet, Namensnennung)

## Bausteine (Masterprompt 131 / 5)
A Programmfenster mit Menüband und Kürzeln · B Arbeitsfläche · C Werkzeuge ändern das Dokument · D .tdoc-Projektdatei mit Autosave/Wiederherstellen · E Rückgängig/Wiederholen und Verlauf · I Prüfungen · J Startmenü, Suche, Ordner, Bedienstufen
