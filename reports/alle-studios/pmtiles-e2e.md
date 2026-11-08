# Echter Lauf: PMTiles-Kartenpakete (Routen & Karten) – 08.11.2026

Freigabe des Nutzers: „Ja, alle testen“. Trendo liest PMTiles v3 mit **eigenem Code** (`src/Trendo.Core/Karten/PmTiles.cs`) – ohne Fremdbibliothek, die Spezifikation ist BSD-3-Clause.
Zum Prüfen diente das offizielle Programm go-pmtiles 1.31.2 (BSD-3-Clause). Es wird nur in den Tests gestartet und nicht mitgeliefert. Herkunft: GitHub-Release `go-pmtiles_1.31.2_Windows_x86_64.zip`, SHA-256 a658baa4d7e55020aef6ca17bd9ff9faa1582671266b36f58c52db0ac8e785a1.

## Lauf
- Test-MBTiles (z0–4, 341 eigene PNG-Kacheln) → `pmtiles convert` → `pmtiles show`: spec version 3, tile type png, internal compression gzip, tile compression none, 341 adressierte Kacheln.
- Test `PmTilesTests.Real_convert_compare` (nur mit `TRENDO_PMTILES_EXE`): MBTiles mit z0–4 (341) und z0–8 (**87 381** Kacheln) aus C#, umgewandelt mit go-pmtiles.
  - Danach jede einzelne Kachel über `TilePack` aus beiden Paketen gelesen: **byte-gleich**.
  - Zoomstufen und Anzahl stimmen überein.
  - Beim großen Paket gibt es Blattverzeichnisse, sie werden richtig gelesen.
  - Grün, Dauer ≈ 5 Minuten.

## Tests (12, alle grün)
- Kachelnummern nach Spezifikation (z1: 1–4 in Hilbert-Reihenfolge, z12/3423/1763 → 19078479, z4 lückenlos 85–340).
- Verzeichnis lesen (Differenzen, Lauflängen, „0 = direkt danach“).
- Kartenpaket: Zoomstufen, Anzahl, Gebiet, jede Kachel. Fehlende Kachel ergibt null.
- Abgelehnt: Vektorkarte (MVT) mit verständlicher Meldung, falsche Kennung, Fassung 2, zu kurze Datei, Abschnitt hinter dem Dateiende, kaputtes Verzeichnis.
- Die Datei ist nach dem Schließen nicht mehr gesperrt.

## Grenzen
- Nur Rasterkarten (PNG/JPEG/WebP). Vektorkarten (MVT) brauchen eine Darstellung wie MapLibre: NOT RUN.
- zstd-komprimierte Pakete werden abgelehnt, weil .NET kein zstd mitbringt.
