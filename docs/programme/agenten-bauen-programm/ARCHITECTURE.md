# KI-Agenten bauen – Architektur

Studio: KI-Agenten bauen (STUDIO-AGENTS-01) · Bereich: KI & Automatik · Seite: `agenten-bauen-programm` · Stand: 0.9.113 (Stand 1978f1a)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.KreativStudios3.cs`

## Bedienelemente der Arbeitsfläche
- Texteditor
- Menüband
- Eigenschaften
- Statusleiste
- Dokument-Reiter

## Fach-Engine (Trendo.Core)
- `VideoAiKit`
- `KreativSafe`
- `ModelCompare`
- `TextClassifier`
- `ImageAiKit`
- `AgentBlueprint`
- `AgentRunState`
- `AgentCenter`
- `AgentTools`
- `IntelligenceCore`

## Dateiformate (im Code belegt)
- .json
- .md
- .tdoc

## Externe Programme und Dienste
- Ollama – lokale KI (optional)
- FFmpeg
- LibreOffice (optional)
- Piper (Vorlesen)

## Bausteine (Masterprompt 131 / 5)
A Programmfenster mit Menüband und Kürzeln · B Arbeitsfläche · C Werkzeuge ändern das Dokument · D .tdoc-Projektdatei mit Autosave/Wiederherstellen · E Rückgängig/Wiederholen und Verlauf · I Prüfungen · J Startmenü, Suche, Ordner, Bedienstufen
