# SPEC-09 – Gegenstände einscannen (Aus Fotos): Funktionskatalog

Funktionen nach den Vorbildern (Meshroom, RealityCapture, COLMAP – nur Funktionen, Clean-Room), Stand 06.11.2026.
„Stand“: umgesetzt = im Code mit Test; „Test“ nennt die Testklasse.

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| SC-01 | Fotos oder Video einlesen und prüfen, Rechnen mit COLMAP oder Meshroom, Schritte mit Fortschritt | Meshroom | umgesetzt | PhotoScanUiTests |
| SC-02 | Kamerapositionen im Ergebnis sichtbar (COLMAP images.bin/images.txt, Meshroom cameras.sfm) | RealityCapture | umgesetzt | ScanXrProTests, ScanXrPro_Ui |
| SC-03 | Modell beschneiden (unten/rundum), kleine Reste entfernen, Rückgängig | RealityCapture Rekonstruktionsbereich | umgesetzt | ScanXrProTests, ScanXrPro_Ui |
| SC-04 | Löcher schließen (offene Ränder zu Flächen) | Meshroom MeshFilling | umgesetzt | ScanXrProTests, ScanXrPro_Ui |
| SC-05 | Echte Größe setzen: direkt je Achse oder über einen bekannten Gegenstand (Kreditkarte, A4, Münze, Dose) | RealityCapture Kontrollpunkte | umgesetzt | ScanXrProTests, ScanXrPro_Ui |
| SC-06 | Texturqualität wählen (1K–8K, Meshroom) | Meshroom Texturing | umgesetzt (echter Meshroom-Lauf nicht im Test) | ScanXrProTests, ScanXrPro_Ui |
| SC-07 | Gute Fotos auswählen: wahrscheinlich unscharfe und doppelte aussortieren, nur gute als Kopie verwenden (Claude-Ausbau 4) | RealityCapture Bildauswahl | umgesetzt | ScanXrProTests, ScanXrPro_Ui |
