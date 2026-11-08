# Trendo-Level als Tiled-Karten – 08.11.2026

Alle Studios, Runde 7, CORE-06 „Spiele bauen“.

Trendo schreibt das offene Kartenformat von **Tiled** (`.tmj`, JSON, Version 1.10). Das Format ist dokumentiert, die Lesebibliothek libtiled steht unter BSD-2. Tiled selbst (GPL) wird **nicht** eingebaut. Trendos Code ist eigener C#-Code in `src/Trendo.Core/Programs/TiledExport.cs` und nutzt keine Fremdbibliothek. Das Kachelbild erzeugt der vorhandene PNG-Schreiber (`Mesh3D.Png`).

## Was die Nutzerin oder der Nutzer sieht
Im Spiel-Editor (Bereich „Level“) gibt es den Knopf **„Als Tiled-Karten ausgeben“**. Er schreibt in `Spiele/Tiled/<Spiel>/`:
- je Level `NN-name.tmj` mit drei Ebenen:
  - **Hintergrund** (Deko-Kacheln)
  - **Boden** (Boden, Tür)
  - **Objekte**: Start, Ziel, Münze, Gegner, Stacheln, Feder, Speicherpunkt, Schlüssel, Gegenstand, Figur, Fahrzeug. Jedes Objekt ist ein Kachel-Objekt mit Art und Eigenschaften, etwa `hp` und `speed` beim Gegner, `value` bei der Münze oder Texte bei Figuren.
- `trendo-kacheln.png`: 18 Kachelarten à 32 × 32 px.
- `spiel.world`: alle Level nebeneinander als Tiled-Welt.

Weiterverwenden lassen sich die Karten in Tiled, in Godot über einen Tiled-Importer sowie in Phaser, Defold und LÖVE (STI).

## Echte Gegenprobe mit einem unabhängigen Leser
**pytiled-parser 2.2.9** (MIT) ist der Tiled-Leser der Python-Spielbibliothek Arcade. Er lag nur in einer Prüfumgebung und wird nicht mitgeliefert. Damit habe ich Trendos Dateien gelesen. Ergebnis für das Level „Wiese“ (20 × 3):
- **Karte:** 20 × 3 Kacheln à 32 px.
- **Tileset:** „Trendo“ mit 18 Kacheln, Bild 576 px breit.
- **Ebenen:** Hintergrund 10 belegte Kacheln, Boden 23.
- **Objekte:** Start (0, 32), Münze, Gegner mit `{hp: 3, speed: 1.5}`, Schlüssel, Ziel, Stacheln.
- **Welt:** zwei Karten nebeneinander (x = 0 und x = 768).

Mit den gelesenen Daten habe ich das Level neu gezeichnet: `tiled-wiese-pytiled.png`, SHA-256 1292da7a67a96d65…. Das Bild entspricht dem Trendo-Level.

## Tests
- **`TiledExportTests`** (7 Tests, grün):
  - Aufbau der Karte
  - Ebeneninhalte
  - Objekt-Lage (unten links)
  - Eigenschaften
  - Rückweg Zeichen für Zeichen
  - Dateien, echtes PNG und Welt-Datei
  - Ablehnung von leeren oder riesigen Leveln
- **`TiledReferenzTests`** (1 Echtlauf, grün): pytiled-parser liest die Karten. Der Test findet die Prüfumgebung automatisch (`EchtWerkzeuge.PruefPython`); fehlt sie, wird er übersprungen.
- **`GameTiled_Ui`** (1 Oberflächentest, grün):
  - Zwei Level ergeben vier Dateien.
  - Level 1 wird gleich zurückgelesen.
  - Der Münzwert aus Level 2 steht am richtigen Objekt.
  - Eigenschaften aus Level 1 landen nicht in Level 2.
- Die bisherigen Spiel-Editor-Tests (`SpielEditor_Ui`) bleiben grün.

## Nicht geprüft (NOT RUN)
- Öffnen der Karten im Programm Tiled. Das wäre ein heruntergeladenes Fremdprogramm und läuft nur mit Freigabe.
- Import in Godot über einen Tiled-Importer.
- Einlesen von Tiled-Karten zurück nach Trendo. Bisher gibt es nur eine Kontrollfunktion im Kern, keinen Import in der Oberfläche.
