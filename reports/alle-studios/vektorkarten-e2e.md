# Echter Lauf: Vektorkarten (MapLibre + PMTiles) im Karten-Studio – 08.11.2026

Freigabe des Nutzers: „Ja, alle testen“ und „mach einfach alles automatisch“.

## Aufbau
- **Kartenseite** `src/Trendo.App/Assets/karten` (Quellen `tools/karten-vektor`, Vite, ein klassisches Skript): MapLibre GL JS 6.17.0 (BSD-3) und der Kartenstil `@protomaps/basemaps` 5.7.2 (BSD-3), Sprache Deutsch.
  - Schriften Noto Sans Regular/Medium/Italic (Latin-Bereiche, OFL) und Kartensymbole (MIT) aus `protomaps/basemaps-assets` stecken im Bündel. Es wird nichts aus dem Netz geladen.
  - Lizenzliste `LIZENZEN.md`/`.txt`: 24 npm-Pakete, alle freizügig, dazu OFL und MIT.
- **Kacheln:** Die Seite fragt `trendo-kachel://z/x/y` über die WebView-Brücke an. Trendo antwortet aus dem eigenen PMTiles-Leser (`VectorMap`, entpackt, Base64).
- **Karten-Studio:** Ein `.pmtiles` mit MVT öffnet die Vektorkarte, die die Arbeitsfläche füllt. Der Inspektor zeigt den Hinweis und „Vektorkarte schließen“. Ein gemerktes Paket öffnet sich beim nächsten Start wieder.

## Befunde beim echten Lauf (behoben)
1. MapLibre 6 lädt seinen Worker als eigene Datei. Unter `file://` kam deshalb „Worker failed to load“. Behoben: Der Worker steckt als Text im Bündel und startet über eine Blob-Adresse.
2. MapLibre fragt Symbole als `light/@2x.json` an. Behoben: Der Pfad wird vereinheitlicht.
3. Im schmalen Inspektor ragte das native Browserfenster aus dem Bildlauf heraus. Behoben: Die Karte liegt jetzt in der Arbeitsfläche.
4. Das gemerkte Paket wurde erst beim Aufbau des Inspektors geladen, nach der Arbeitsfläche. Behoben: Die Arbeitsfläche lädt es zuerst.

## Testdaten
- Ausschnitt Innsbruck (11,38–11,42 O, 47,25–47,28 N, Zoom 0–15) aus dem öffentlichen Protomaps-Build vom 07.11.2026, geschnitten mit `pmtiles extract` (go-pmtiles 1.31.2).
- `ort.pmtiles` hat 3509649 Bytes, SHA-256 bb03ecdb619b37669fd2b1e7c27dc785421e9c2245abe7c7277ca9079f6928a9. Inhalt: MVT, Verzeichnis und Kacheln gzip, 54 Kacheln.
- Die Kartendaten stammen von © OpenStreetMap-Mitwirkenden (ODbL). Sie liegen nur im Temp-Ordner und nicht im Repository.

## Läufe
- **Browser** (Testkopie der Seite mit nachgebildeter Brücke, Kacheln von `pmtiles serve`): Karte geladen, keine Fehler, 53 Symbole, deutsche Beschriftung, Zoom 16 in der Altstadt mit Lokalen und Sehenswürdigkeiten.
- **Echtes Trendo-Fenster** (eigene Testfassung, eigener Datenordner, `file://`, Brücke zu C#; Prüfung per CDP):
  - Zoom 15: **1938 sichtbare Kartenobjekte** (Straßen 457, Gebäude 1319, Flächennutzung 117, Wasser 15, Orte 5, Lokale/Einrichtungen 22, Land 3).
  - Bild `vektorkarte-trendo-z15.png` (SHA-256 dd5c420bbfa66a568fc9c02f5aadd1c5397477cffb0a6a67fa2962edfada87c0).
- **Tests:**
  - `VectorMapTests` (Kern): 7 nachgebildet und 1 echter Lauf mit dem Protomaps-Ausschnitt. Alle 54 Kacheln werden gelesen und entpackt (beginnen mit dem MVT-Feld „layers“).
  - `VectorMapUi` (Oberfläche).
  - Alle grün.

## Grenzen
- Nur die Schriftzeichen der lateinischen Schrift sind im Bündel. Andere Schriften (z. B. Kyrillisch, Arabisch, Chinesisch) erscheinen in Beschriftungen nicht.
- Routen zeichnen geht weiter nur auf der Rasterkarte.
