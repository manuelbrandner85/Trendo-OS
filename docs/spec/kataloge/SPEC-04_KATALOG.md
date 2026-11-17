# SPEC-04 – 3D-Modelle ansehen (Modell-Prüfer, früher „VR & AR gestalten“): Funktionskatalog

Funktionen nach den Vorbildern (glTF-Viewer, Khronos glTF-Validator, Leistungsrichtlinien der Brillen-Hersteller – nur Funktionen, Clean-Room), Stand 06.11.2026.
„Stand“: umgesetzt = im Code mit Test; „Test“ nennt die Testklasse.

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| XR-01 | 3D-Betrachter mit Drehen, Zoomen, Drahtgitter, Ansichten | glTF-Viewer | umgesetzt | SPEC04_Ui |
| XR-02 | Kennzahlen ohne Zielgerät: Maße, Dreiecke, Teile, Materialien, Texturen, Dateigröße; Fehler im Modell (T-0722 statt Leistungsbudget je Brille) | glTF-Viewer | umgesetzt | SPEC04_Ui |
| XR-03 | Größte Textur gegen die Gerätegrenze (Quest 2: 1024, Quest 3/Handy-AR: 2048, PC-VR: 4096 px) | Hersteller-Richtlinien | entfernt (T-0722, Nutzerentscheidung 16.11.2026) | – |
| XR-04 | Vorschau aus Augenhöhe (1,60 m, 2 m Abstand, 90° Bildwinkel, Boden bis zum Horizont) | VR-Vorschau in Engines | umgesetzt | ScanXrPro_Ui |
| XR-05 | Maßstab-Prüfung (Zentimeter/Millimeter statt Meter erkannt) | glTF-Validator | umgesetzt | SPEC04_Ui, ScanXrProTests |
| XR-06 | Brillen-Tauglichkeit mit einem Klick reparieren | – | entfernt (T-0722, Nutzerentscheidung 16.11.2026) | – |
| XR-07 | Prüfbericht, Rechner für Detailstufen und Texturschärfe (Bildzeit und Bewegungskomfort entfernt mit T-0722) | – | umgesetzt | SPEC04_Ui |
