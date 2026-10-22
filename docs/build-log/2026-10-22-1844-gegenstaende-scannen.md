# SPEC-09 – Fotos werden ein 3D-Modell

- 22.10.2026, 18:44 Uhr (Europe/Berlin), Linux-Testrechner, Branch `cursor/gegenstaende-scannen-db3a`
- Studio „Gegenstände einscannen“: mindestens drei Fotos oder Einzelbilder aus einem Video. Meshroom, sonst COLMAP. Fehlt das Programm, nur die offizielle Seite, keine Installation.
- Ergebnis als OBJ ins Studio „3D-Modelle erstellen“. Nur Punkte: äußere Hülle, und der Text sagt das. Die alten Punktwolken-Werkzeuge bleiben.
- Tests: Core 447 bestanden / 10 übersprungen / 0 Fehler. App 187 bestanden im Gesamtlauf, der GitHub-Suchtest einmal leer, später bestanden. Api 14 bestanden / 0 Fehler.
- Nicht geprüft: echtes Meshroom, echtes COLMAP, echtes Video. Kein PC_VERIFIED.
