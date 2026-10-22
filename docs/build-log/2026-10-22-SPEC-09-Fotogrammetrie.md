# SPEC-09 – Fotos oder Video werden zum 3D-Modell

- 22.10.2026, Linux-Testrechner, Branch `cursor/gegenstaende-scannen-fec1`
- Studio „Gegenstände einscannen“: Ordner mit mindestens 3 Fotos oder ein Video. COLMAP (CPU, ohne CUDA) oder Meshroom. FFmpeg zieht aus dem Video zwei Bilder pro Sekunde.
- Vor der Einrichtung fragt die vorhandene Werkzeugprüfung. COLMAP kommt als offizielles Windows-ZIP in den Benutzerordner, Prüfsumme fest. Meshroom nur über die offizielle Seite. Das Studio öffnet auch ohne das Werkzeug, die Punktwolken-Werkzeuge bleiben.
- Fortschritt in der Seite, Stopp bricht ab. Ergebnis `modell.obj`, längste Seite 20 cm. „Im 3D-Studio öffnen“ liest genau diese Datei ein.
- Tests: Core 450 bestanden / 10 übersprungen / 0 Fehler. App 187 bestanden / 0 Fehler. Api 14 bestanden / 0 Fehler.
- Nicht geprüft: echtes COLMAP, echtes Meshroom, echtes FFmpeg, Windows-ZIP, ein echter Gegenstand. Kein PC_VERIFIED.
