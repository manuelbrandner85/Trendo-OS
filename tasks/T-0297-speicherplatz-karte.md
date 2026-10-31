# T-0297 – Speicherplatz als Kachelkarte

- Freigabe: Manuel im Chat am 31.10.2026 (Auswahl „Speicher als Kachelkarte“).
- Neu auf „Speicher & Leistung“: Karte „Speicherplatz als Karte“.
  - Ordner einlesen im Hintergrund (Fortschritt, Abbrechen), ohne Verknüpfungen/Offline-Dateien zu folgen, nicht lesbare Ordner zählen 0.
  - Kachelkarte (Squarified-Verfahren): Fläche ∝ Größe, Farbe nach Dateiart (Video, Bild, Musik, Dokument, Archiv, Programm, Ordner), Ordner zeigen ihren Inhalt angedeutet; Klick wählt, zweiter Klick auf einen Ordner geht hinein, Pfad zum Zurückgehen.
  - Rechts: Auswahl mit Größe/Anteil, „Hinein“, „Im Ordner zeigen“, „In den Papierkorb …“ (erst nach „Ja“), Legende, größte Dateien; „Duplikate suchen“ markiert doppelte Dateien rot gestrichelt und nennt den möglichen Gewinn.
  - Papierkorb: Windows-Shell mit Rückholmöglichkeit, Linux ~/.local/share/Trash – nie endgültig gelöscht.
- Kern `Trendo.Core.Sys.DiskMap` (Einlesen, Squarify, Dateiarten, größte Dateien, Entfernen mit Nachziehen der Größen).
- Tests: `DiskMap_Ui` (Kern: Flächen proportional und innerhalb, Größen, Entfernen; Oberfläche: echte Maus wählen/hinein, Papierkorb erst nach Ja – mit Ersatz statt echtem Papierkorb).
