# CORE-05 – 3D-Modelle erstellen: Funktionskatalog

Funktionen nach den Vorbildern (Blender, SketchUp, Tinkercad; FreeCAD, Fusion, Onshape; PrusaSlicer, Cura, Bambu Studio – nur Funktionen, Clean-Room), Stand 04.11.2026.
„Stand“: umgesetzt = im Code mit Test; „Test“ nennt die Testklasse.

## 3D

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| D3-01 | 3D-Ansicht mit Drehen, Zoomen, Auswählen | Blender, SketchUp | umgesetzt | Studio3DViewport_Ui |
| D3-02 | Formen, Szene, Export | Tinkercad | umgesetzt | W4_Core05_Ui |
| D3-03 | Freie Modelle in die Bibliothek | Blender Asset Browser | umgesetzt | Library3D_Ui |

## Trendo CAD

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| CA-01 | Boolesche Operationen (Vereinigung, Abzug, Schnitt) | FreeCAD Part | umgesetzt | CadTests |
| CA-02 | Parametrisch mit Formeln | Fusion, Onshape | umgesetzt | CadTests |
| CA-03 | Extrudieren, Tasche, Drehteil, Rundung, Fase, Muster, Spiegeln | Fusion | umgesetzt | CadTests |
| CA-04 | Export STL, 3MF, STEP, Zeichnung als PDF | FreeCAD TechDraw | umgesetzt | CadTests |
| CA-05 | Bauschritte im Menüband, Rückgängig | Fusion Timeline | umgesetzt | N23N24_CadPrintUi |
| CA-06 | Eigenschaften: Volumen, Oberfläche, Masse je Werkstoff, Schwerpunkt | Fusion Eigenschaften | umgesetzt | CadMeasureTests |
| CA-07 | Querschnittsfläche | Fusion Querschnittsanalyse | umgesetzt | CadMeasureTests |
| CA-08 | Normbohrungen M2–M12 | Fusion Bohrung | umgesetzt | N23N24_CadPrintUi |
| CA-09 | Skizzen mit Bedingungen | FreeCAD Sketcher | offen | – |
| CA-10 | Gelenke in Baugruppen | Fusion Joints | offen | – |

## Trendo 3D-Druck

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| DR-01 | Druckplatte, Aufstellen, Prüfungen | PrusaSlicer | umgesetzt | PrintPreview_Ui |
| DR-02 | Umrisse und Füllung je Schicht | Cura Vorschau | umgesetzt | SlicerTests |
| DR-03 | Zeit, Filament, Kosten aus den Bahnen | PrusaSlicer | umgesetzt | SlicerTests |
| DR-04 | Material, Schichthöhe, Wände, Füllung | Cura, Bambu Studio | umgesetzt | PrintPreview_Ui |
| DR-05 | Stützen-Schätzung | PrusaSlicer | umgesetzt | SlicerTests |
| DR-06 | Skalieren, Kopien anordnen | PrusaSlicer | umgesetzt | SlicerTests |
| DR-07 | Drucker im Heimnetz, Druck nach Bestätigung | Bambu Studio | umgesetzt | N23N24_CadPrintUi |
| DR-08 | Mehrere verschiedene Modelle auf einer Platte | PrusaSlicer | offen | – |
