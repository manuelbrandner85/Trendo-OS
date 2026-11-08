# Echtlauf-Tests finden ihre Programme selbst – 08.11.2026

Wunsch des Nutzers: „gut kannst das automatisch“. Die Echtlauf-Tests (echte Fremdprogramme über die Trendo-Adapter) brauchen keine Umgebungsvariable mehr.
`tests/Trendo.Core.Tests/EchtWerkzeuge.cs` sucht für jeden Test in dieser Reihenfolge:

1. die Umgebungsvariable (Vorrang, z. B. `TRENDO_ASSIMP_DLL`),
2. den Ort, an dem Trendo das Werkzeug selbst einrichtet (`%LOCALAPPDATA%\TrendoWerkzeuge\…`, Python-Werkzeuge unter `ki\laufzeit\<name>\env`),
3. den Prüfordner `%TEMP%\trendo-tools-b`.

Fehlt ein Programm, wird der Test übersprungen. Es wird nie etwas heruntergeladen.

| Test | Programm | Fundort auf diesem PC |
|---|---|---|
| typst, mediamtx, streamlink | offizielle Zips | Prüfordner |
| IFC echt | IfcOpenShell (Python) | Prüfordner |
| Open3D echt | Open3D (Python) | Prüfordner |
| PMTiles-Vergleich | go-pmtiles | Prüfordner |
| Vektorkarte echt | Protomaps-Ausschnitt | Prüfordner |
| assimp echt (2) | assimp-DLL | Prüfordner |
| MLT echt | melt aus Kdenlive, FFmpeg | TrendoWerkzeuge (mitgeliefert) |

Zusätzlich:
- **streamlink:** wird übersprungen, wenn arte.tv nicht erreichbar ist (kein Internet), statt rot zu werden.
- **PMTiles-Vergleich:** von etwa 4 Minuten auf wenige Sekunden.
  - Das Test-MBTiles hat jetzt den Index `tile_index` wie in der MBTiles-Spezifikation. Ohne ihn brauchte go-pmtiles 4 min 48 s statt 9 s für 87 381 Kacheln.
  - Ab Zoom 7 wird eine gestreute Stichprobe verglichen (etwa jede neunte Kachel). Die Blattverzeichnisse werden weiterhin gelesen.

Lauf ohne gesetzte Umgebungsvariablen: 68 Tests (alle betroffenen Klassen einschließlich der Echtläufe) bestanden, 0 übersprungen, 9 s.
