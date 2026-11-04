# CNC-Werkstatt – Architektur

Studio: Fräsen & Fertigen (SPEC-14) · Bereich: Technik & Fachgebiete · Seite: `cnc` · Stand: 0.9.36 (Stand 9ed3c01)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.Spec14.cs`

## Bedienelemente der Arbeitsfläche
- Werkzeugbahn-Ansicht
- Menüband
- Eigenschaften
- Statusleiste
- Dokument-Reiter

## Fach-Engine (Trendo.Core)
- `CncJob`
- `CncPlus`
- `CncSim`

## Dateiformate (im Code belegt)
- .md
- .nc
- .svg
- .tdoc

## Externe Programme und Dienste
– keine

## Bausteine (Masterprompt 131 / 5)
A Programmfenster mit Menüband und Kürzeln · B Arbeitsfläche · C Werkzeuge ändern das Dokument · D .tdoc-Projektdatei mit Autosave/Wiederherstellen · E Rückgängig/Wiederholen und Verlauf · I Prüfungen · J Startmenü, Suche, Ordner, Bedienstufen
