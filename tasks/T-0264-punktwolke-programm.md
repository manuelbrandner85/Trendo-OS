# T-0264 – 3D-Scan als echtes Programm „Punktwolke“

- Freigabe: Manuel im Chat am 31.10.2026 („alles Offene in jedem Studio umsetzen“, Masterprompt GODMODE 131)
- Vorbilder (Funktionsumfang, öffentlich dokumentiert): CloudCompare, MeshLab; kein Code übernommen (siehe `docs/claude/vorbilder/punktwolke-cloudcompare.md`)
- Umgesetzt: eigene 3D-Ansicht (Drehen links, Verschieben rechts/Umschalt, Mausrad-Zoom, Ansichten Schräg/Oben/Vorne/Seite, Farbe Scan/Höhe, Begrenzungsrahmen, Achsenkreuz), Messen per Klick (Abstand, Höhenunterschied), nicht-destruktive Schritte im Menüband (Störpunkte, Ausdünnen, Boden entfernen, Zentrieren, Drehen, Einheit, Zuschneiden, Zusammenführen) mit Liste und Entfernen einzelner Schritte, Analyse (Boden, Volumen, Punktdichte, Vergleich), Export PLY/XYZ/Messbericht, Beispielraum zum Ausprobieren. Der Reiter „Aus Fotos“ (COLMAP/Meshroom) bleibt daneben.
- Tests: Core 2 (Beispielraum mit bekannten Maßen, Schritte, JSON), App 2 (echte Maus: Drehen, Zoomen, zwei Punkte messen; Laden, Export) + PhotoScanUiTests auf den Fotos-Reiter umgestellt
- Offen: Netz aus Punktwolke (Poisson), Registrierung zweier Scans (ICP), E57/LAS-Format
