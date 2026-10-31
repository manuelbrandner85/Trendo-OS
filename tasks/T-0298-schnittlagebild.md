# T-0298 – Kleidung & Produkte: Schnittlagebild

- Freigabe: Manuel im Chat am 31.10.2026 (Auswahl „Schnittlagebild für Kleidung“).
- Vorher: Zuschnitt-Vorschau als starres Bild in Reihen gelegt, Stoffbedarf nur als Zahl.
- Jetzt (`MarkerCanvas`, Kern `Trendo.Core.Marker`):
  - Stoffbahn maßstäblich mit Webkanten, 10-cm-Raster und Zentimeter-Leiste; Teile mit Schnittkante und gestrichelter Nahtlinie (1,5 cm Nahtzugabe), Name und Maß.
  - Teil ziehen verschiebt (1-cm-Raster), Doppelklick oder „↻ Drehen“ dreht; Lage wird im Produktdokument gespeichert (Rückgängig).
  - „Automatisch legen“: Skyline-Verfahren, beide Drehungen je Teil – nie mehr Stoff als das bisherige Reihenlegen (Test).
  - Verbrauchslinie in m, Ausnutzung und Verschnitt in %, Überlappungen und Teile über dem Stoffrand rot mit Meldung.
  - „Als PDF“: A4 mit maßstäblicher Lage, Raster, Kennzahlen und Teileliste zum Ausdrucken.
- Programm-Hülle: neue Einstellung `ScrollWorkspace` – Arbeitsflächen aus Karten scrollen bei voller Fensterhöhe (seit T-0289 wird die Arbeitsfläche begrenzt).
- **Fehler behoben:** Der erste Klick auf ein Teil baute die Ansicht neu, dadurch kam ein Doppelklick nie an – Klick markiert jetzt ohne Neuaufbau.
- Tests: `Marker_Ui` (Kern: ohne Überlappung, sparsamer oder gleich wie bisher, Prüfungen, zu breit, PDF; Oberfläche: echtes Ziehen, Doppelklick dreht, Überlappung gemeldet, automatisch legen, PDF); `ProgramShellUiTests`, `SPEC08_Ui` grün.
