# SPEC-04 – VR & AR gestalten (Modell-Prüfer): Funktionskatalog

Funktionen nach den Vorbildern (glTF-Viewer, Khronos glTF-Validator, Leistungsrichtlinien der Brillen-Hersteller – nur Funktionen, Clean-Room), Stand 06.11.2026.
„Stand“: umgesetzt = im Code mit Test; „Test“ nennt die Testklasse.

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| XR-01 | 3D-Betrachter mit Drehen, Zoomen, Drahtgitter, Ansichten | glTF-Viewer | umgesetzt | SPEC04_Ui |
| XR-02 | Leistungsbudget je Brille: Dreiecke, Zeichenaufrufe, Texturen, Dateigröße mit Ampel | Hersteller-Richtlinien | umgesetzt | SPEC04_Ui |
| XR-03 | Größte Textur gegen die Gerätegrenze (Quest 2: 1024, Quest 3/Handy-AR: 2048, PC-VR: 4096 px) | Hersteller-Richtlinien | umgesetzt | ScanXrProTests, ScanXrPro_Ui |
| XR-04 | Vorschau aus Augenhöhe (1,60 m, 2 m Abstand, 90° Bildwinkel wie eine Brille, Boden bis zum Horizont) | VR-Vorschau in Engines | umgesetzt | ScanXrPro_Ui |
| XR-05 | Maßstab-Prüfung (Zentimeter/Millimeter statt Meter erkannt) | glTF-Validator | umgesetzt | SPEC04_Ui, ScanXrProTests |
| XR-06 | Brillen-Tauglichkeit mit einem Klick reparieren: Dreiecke auf 90 % des Budgets, Texturen verkleinern, Maßstab auf Meter – neue Datei, Original bleibt (Claude-Ausbau 3) | – | umgesetzt (Blender-Lauf nicht im Test) | ScanXrProTests, ScanXrPro_Ui |
| XR-07 | Prüfbericht, Rechner für Bildzeit, Detailstufen, Texturschärfe, Bewegungskomfort | – | umgesetzt | SPEC04_Ui |
