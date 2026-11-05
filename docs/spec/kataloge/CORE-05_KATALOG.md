# CORE-05 – 3D-Modelle erstellen: Funktionskatalog

Funktionen nach den Vorbildern (Blender, SketchUp, Tinkercad; FreeCAD, Fusion, Onshape; PrusaSlicer, Cura, Bambu Studio – nur Funktionen, Clean-Room), Stand 05.11.2026.
„Stand“: umgesetzt = im Code mit Test; „Test“ nennt die Testklasse.

## 3D

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| D3-01 | 3D-Ansicht mit Drehen, Zoomen, Auswählen | Blender, SketchUp | umgesetzt | Studio3DViewport_Ui |
| D3-02 | Formen, Szene, Export | Tinkercad | umgesetzt | W4_Core05_Ui |
| D3-03 | Freie Modelle in die Bibliothek | Blender Asset Browser | umgesetzt | Library3D_Ui |
| D3-04 | Arbeitsschritte Modellieren → UV → Material → Licht → Kamera → Render mit Stand je Schritt | Blender-Arbeitsablauf | umgesetzt | Studio3DProTests, Studio3DPro_Ui |
| D3-05 | Anfasser in der Ansicht: Verschieben, Drehen, Skalieren je Achse (Raster, Tasten G/R/S) | Blender Gizmos | umgesetzt | Studio3DPro_Ui |
| D3-06 | Szenenbaum mit Gruppen und Ausblenden; Gruppe gemeinsam bewegen und einfärben | Blender Outliner | umgesetzt | Studio3DPro_Ui |
| D3-07 | PBR-Materialien (Metall, Rauheit, Glas) mit Vorschau in der Ansicht und im Blender-Render | Blender Principled BSDF | umgesetzt | Studio3DProTests, Studio3DPro_Ui |
| D3-08 | Licht-Vorgaben (Studio, Tageslicht, Abendsonne, Innenraum) und Kamera mit Brennweite und Blickhöhe | Blender Licht/Kamera | umgesetzt | Studio3DProTests, Studio3DPro_Ui |
| D3-09 | Fotorealistischer Render (Cycles, Entrauschen, AgX, UV automatisch) mit Verlauf und Vorher/Nachher-Regler | Blender Render | umgesetzt (Blender-Lauf nicht im Test) | Studio3DProTests, Studio3DPro_Ui |
| D3-10 | Claude baut aus Satz oder Foto ein Modell mit echten Maßen, Materialien, Licht und Kamera – Plan mit 3D-Vorschau, danach Render zur Prüfung | Claude-Ausbau 1 | umgesetzt | Claude3D_Ui |

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
