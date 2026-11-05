# Trendo 3D-Druck – Testplan

Studio: 3D-Modelle erstellen (CORE-05) · Bereich: 3D, Spiele & VR · Seite: `3d-druck` · Stand: 0.9.36 (Stand 208ed9e)

13 Tests sprechen dieses Programm an.

- CAD wie Fusion 360: Normbohrung M5 per Auswahl (ein Schritt, Rückgängig), Eigenschaften mit Masse je Werkstoff und Querschnitt
- CORE-05: Echtes Blender – Modell anlegen, Vorschau rendern, in glTF/OBJ/STL/FBX umwandeln, Inhalt prüfen
- CORE-05: Eigenschaften: Quader 40×30×10 mm – Volumen 12 cm³, Oberfläche 38 cm², Masse je Werkstoff, Schwerpunkt in der Mitte
- CORE-05: Füllung: 100 % deckt die Fläche (Länge ≈ Fläche / Linienbreite), läuft nicht in die Lücke zwischen zwei Körpern, Winkel wechselt
- CORE-05: Normbohrungen: M3 Durchgang 3,4 mm, M6 Kernloch 5 mm, M8 Senkschraube 9/16,4 mm; eigenes Maß zuerst
- CORE-05: Querschnitt: Quader mit Bohrung – Fläche ohne das Loch; über dem Körper 0
- CORE-05: Schätzung aus Bahnen: massiver Würfel wiegt etwa Volumen × Dichte, 15 % Füllung deutlich weniger, PETG schwerer und teurer als PLA
- CORE-05: Skalieren, Anordnen in Reihen (mittig, mit Abstand), Kopien auf der Platte; zu viele Teile oder zu groß gibt eine klare Meldung
- CORE-05: Stützen: Tisch-Form (Platte auf dünnem Fuß) braucht Stützen unter der Platte, Würfel nicht
- CORE-05: Umrisse: Würfelschnitt ergibt einen geschlossenen Umriss (Umfang 80 mm, Fläche 400 mm²); zwei Körper zwei Umrisse
- CORE-05: Unbekannte Formate und Formen werden abgewiesen
- N23: Bauschritte über das Menüband, Maß als Formel ändern, Parameter ändern rechnet neu, Rückgängig, Export 3MF/STEP/Zeichnung
- N24: Drucker eintragen (Zugangscode geschützt), mit nachgebautem Drucker verbinden, Status sehen; Druck nur nach Bestätigung
