# Bericht T-0260 – Karten & Touren

## Neu
- `src/Trendo.Core/Karten/MapDoc.cs` – Kartenprojekt (Spuren, Routen, Wegpunkte, Flächen), GPX-Import, Export GPX/GeoJSON/KML, Web-Mercator
- `src/Trendo.App/MapCanvas.cs` – OSM-Kacheln mit Zwischenspeicher (Nutzungsrichtlinie), Karte mit Werkzeugen, Maßstab, Namensnennung, Höhenprofil
- `src/Trendo.App/MainWindow.Spec13.cs` – Programm statt Formular, Live-Rechner
- Tests: `tests/Trendo.Core.Tests/MapDocTests.cs` (3), `tests/Trendo.App.Tests/MapUiTests.cs` (2, echte Maus/Tastatur); alte SPEC-13-Formulartests ersetzt

## Nicht geprüft / offen
- Bedienung am PC; Kachelabruf nur einmal für das Bildschirmfoto online geprüft (Tests laufen offline)
- Routing entlang von Wegen, Höhen für gezeichnete Routen, Offline-Kartenpakete
