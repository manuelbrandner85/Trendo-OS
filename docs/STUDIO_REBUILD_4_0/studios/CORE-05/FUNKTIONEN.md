# Funktionsliste: 3D-Modelle erstellen

Studio CORE-05 · 3D-Modelle erstellen · Bereich 3D, Spiele & VR

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

| Nr | Funktion | Stand laut Katalog | Test |
|---|---|---|---|
| D3-01 | 3D-Ansicht mit Drehen, Zoomen, Auswählen | umgesetzt | Studio3DViewport_Ui |
| D3-02 | Formen, Szene, Export | umgesetzt | W4_Core05_Ui |
| D3-03 | Freie Modelle in die Bibliothek | umgesetzt | Library3D_Ui |
| D3-04 | Arbeitsschritte Modellieren → UV → Material → Licht → Kamera → Render mit Stand je Schritt | umgesetzt | Studio3DProTests, Studio3DPro_Ui |
| D3-05 | Anfasser in der Ansicht: Verschieben, Drehen, Skalieren je Achse (Raster, Tasten G/R/S) | umgesetzt | Studio3DPro_Ui |
| D3-06 | Szenenbaum mit Gruppen und Ausblenden; Gruppe gemeinsam bewegen und einfärben | umgesetzt | Studio3DPro_Ui |
| D3-07 | PBR-Materialien (Metall, Rauheit, Glas) mit Vorschau in der Ansicht und im Blender-Render | umgesetzt | Studio3DProTests, Studio3DPro_Ui |
| D3-08 | Licht-Vorgaben (Studio, Tageslicht, Abendsonne, Innenraum) und Kamera mit Brennweite und Blickhöhe | umgesetzt | Studio3DProTests, Studio3DPro_Ui |
| D3-09 | Fotorealistischer Render (Cycles, Entrauschen, AgX, UV automatisch) mit Verlauf und Vorher/Nachher-Regler | umgesetzt (Blender-Lauf nicht im Test) | Studio3DProTests, Studio3DPro_Ui |
| D3-10 | Claude baut aus Satz oder Foto ein Modell mit echten Maßen, Materialien, Licht und Kamera – Plan mit 3D-Vorschau, danach Render zur Prüfung | umgesetzt | Claude3D_Ui |
| D3-11 | Freie Modelle aus Thingiverse (offizielle Schnittstelle, eigener App-Schlüssel), Smithsonian (CC0, eigener Schlüssel), NASA (gemeinfrei), Sketchfab (Suche frei, Laden mit eigenem Schlüssel) | umgesetzt (echte Schlüssel-Abrufe nicht im Test) | FreeModelsTests, Library3DPro_Ui |
| D3-12 | Seiten ohne Schnittstelle (Printables, MakerWorld, Cults3D, MyMiniFactory, Thangs): im Browser laden, Trendo übernimmt die Datei aus dem Download-Ordner – kein Abgreifen, kein Passwort | umgesetzt | FreeModelsTests, Library3DPro_Ui |
| D3-13 | Lizenz je Modell sichtbar, Filter „nur kommerziell“, Lizenzdatei mit Namensnennung | umgesetzt | FreeModelsTests, Library3DPro_Ui |
| D3-14 | Übernehmen ins 3D-Studio oder in den 3D-Druck (Maßeinheit erkennt der Import) | umgesetzt | Library3DPro_Ui |
| CA-01 | Boolesche Operationen (Vereinigung, Abzug, Schnitt) | umgesetzt | CadTests |
| CA-02 | Parametrisch mit Formeln | umgesetzt | CadTests |
| CA-03 | Extrudieren, Tasche, Drehteil, Rundung, Fase, Muster, Spiegeln | umgesetzt | CadTests |
| CA-04 | Export STL, 3MF, STEP, Zeichnung als PDF | umgesetzt | CadTests |
| CA-05 | Bauschritte im Menüband, Rückgängig | umgesetzt | N23N24_CadPrintUi |
| CA-06 | Eigenschaften: Volumen, Oberfläche, Masse je Werkstoff, Schwerpunkt | umgesetzt | CadMeasureTests |
| CA-07 | Querschnittsfläche | umgesetzt | CadMeasureTests |
| CA-08 | Normbohrungen M2–M12 | umgesetzt | N23N24_CadPrintUi |
| CA-09 | Skizzen mit Bedingungen | offen | – |
| CA-10 | Gelenke in Baugruppen | offen | – |
| DR-01 | Druckplatte, Aufstellen, Prüfungen | umgesetzt | PrintPreview_Ui |
| DR-02 | Umrisse und Füllung je Schicht | umgesetzt | SlicerTests |
| DR-03 | Zeit, Filament, Kosten aus den Bahnen | umgesetzt | SlicerTests |
| DR-04 | Material, Schichthöhe, Wände, Füllung | umgesetzt | PrintPreview_Ui |
| DR-05 | Stützen-Schätzung | umgesetzt | SlicerTests |
| DR-06 | Skalieren, Kopien anordnen | umgesetzt | SlicerTests |
| DR-07 | Drucker im Heimnetz, Druck nach Bestätigung | umgesetzt | N23N24_CadPrintUi |
| DR-08 | Mehrere verschiedene Modelle auf einer Platte | offen | – |
| DR-09 | Bambu-Drucker im Netz finden und übernehmen, gleiches Netz prüfen | umgesetzt | BambuProTests, BambuPro_Ui |
| DR-10 | Einrichtung: WLAN, Nur LAN, Entwicklermodus, Zugangscode; WLAN-Signal in Klartext | umgesetzt | BambuProTests, BambuPro_Ui |
| DR-11 | Steuern: Licht, Tempo, Düse, Bett, Lüfter (mit Grenzen) | umgesetzt | BambuProTests, BambuPro_Ui |
| DR-12 | AMS-Filamente, Druckermeldungen (HMS) mit Nachschlagen | umgesetzt | BambuProTests, BambuPro_Ui |
| DR-13 | Live-Kamera, Fertig-Meldung, Zeitraffer aufnehmen und holen | umgesetzt (Kamera/FTPS nur am echten Drucker) | BambuProTests, BambuPro_Ui |
| DR-14 | Direkt aus Trendo vorbereiten (Bambu Studio im Hintergrund) und drucken | umgesetzt (echter Slicer-Lauf nicht im Test) | BambuProTests, BambuPro_Ui |
| DR-15 | Mehrere Drucker mit Zustand, Auswählen, Entfernen | umgesetzt | BambuProTests, BambuPro_Ui |
| D3-15 | Export für Unity (FBX), Unreal (FBX), Godot (GLB), Web (GLB) über Blender mit Engine-Einstellungen | umgesetzt, PC-Lauf mit Blender 5.2.3 (reports/paket01) | Studio3DEnginesTests, Studio3DEngines_Ui |
| D3-16 | Rundlauf-Prüfung: Export wieder einlesen und vergleichen (Teile, Dreiecke, Materialien, UV, Knochen, Animationen, Formschlüssel, Maße); Verlust = Fehler, gewollte Änderung = Hinweis; Bericht neben der Datei | umgesetzt | Studio3DEnginesTests, Studio3DEngines_Ui |
| D3-01 | 3D-Ansicht mit Drehen, Zoomen, Auswählen | umgesetzt | Studio3DViewport_Ui |
| D3-02 | Formen, Szene, Export | umgesetzt | W4_Core05_Ui |
| D3-03 | Freie Modelle in die Bibliothek | umgesetzt | Library3D_Ui |
| D3-04 | Arbeitsschritte Modellieren → UV → Material → Licht → Kamera → Render mit Stand je Schritt | umgesetzt | Studio3DProTests, Studio3DPro_Ui |
| D3-05 | Anfasser in der Ansicht: Verschieben, Drehen, Skalieren je Achse (Raster, Tasten G/R/S) | umgesetzt | Studio3DPro_Ui |
| D3-06 | Szenenbaum mit Gruppen und Ausblenden; Gruppe gemeinsam bewegen und einfärben | umgesetzt | Studio3DPro_Ui |
| D3-07 | PBR-Materialien (Metall, Rauheit, Glas) mit Vorschau in der Ansicht und im Blender-Render | umgesetzt | Studio3DProTests, Studio3DPro_Ui |
| D3-08 | Licht-Vorgaben (Studio, Tageslicht, Abendsonne, Innenraum) und Kamera mit Brennweite und Blickhöhe | umgesetzt | Studio3DProTests, Studio3DPro_Ui |
| D3-09 | Fotorealistischer Render (Cycles, Entrauschen, AgX, UV automatisch) mit Verlauf und Vorher/Nachher-Regler | umgesetzt (Blender-Lauf nicht im Test) | Studio3DProTests, Studio3DPro_Ui |
| D3-10 | Claude baut aus Satz oder Foto ein Modell mit echten Maßen, Materialien, Licht und Kamera – Plan mit 3D-Vorschau, danach Render zur Prüfung | umgesetzt | Claude3D_Ui |
| D3-11 | Freie Modelle aus Thingiverse (offizielle Schnittstelle, eigener App-Schlüssel), Smithsonian (CC0, eigener Schlüssel), NASA (gemeinfrei), Sketchfab (Suche frei, Laden mit eigenem Schlüssel) | umgesetzt (echte Schlüssel-Abrufe nicht im Test) | FreeModelsTests, Library3DPro_Ui |
| D3-12 | Seiten ohne Schnittstelle (Printables, MakerWorld, Cults3D, MyMiniFactory, Thangs): im Browser laden, Trendo übernimmt die Datei aus dem Download-Ordner – kein Abgreifen, kein Passwort | umgesetzt | FreeModelsTests, Library3DPro_Ui |
| D3-13 | Lizenz je Modell sichtbar, Filter „nur kommerziell“, Lizenzdatei mit Namensnennung | umgesetzt | FreeModelsTests, Library3DPro_Ui |
| D3-14 | Übernehmen ins 3D-Studio oder in den 3D-Druck (Maßeinheit erkennt der Import) | umgesetzt | Library3DPro_Ui |
| CA-01 | Boolesche Operationen (Vereinigung, Abzug, Schnitt) | umgesetzt | CadTests |
| CA-02 | Parametrisch mit Formeln | umgesetzt | CadTests |
| CA-03 | Extrudieren, Tasche, Drehteil, Rundung, Fase, Muster, Spiegeln | umgesetzt | CadTests |
| CA-04 | Export STL, 3MF, STEP, Zeichnung als PDF | umgesetzt | CadTests |
| CA-05 | Bauschritte im Menüband, Rückgängig | umgesetzt | N23N24_CadPrintUi |
| CA-06 | Eigenschaften: Volumen, Oberfläche, Masse je Werkstoff, Schwerpunkt | umgesetzt | CadMeasureTests |
| CA-07 | Querschnittsfläche | umgesetzt | CadMeasureTests |
| CA-08 | Normbohrungen M2–M12 | umgesetzt | N23N24_CadPrintUi |
| CA-09 | Skizzen mit Bedingungen | offen | – |
| CA-10 | Gelenke in Baugruppen | offen | – |
| DR-01 | Druckplatte, Aufstellen, Prüfungen | umgesetzt | PrintPreview_Ui |
| DR-02 | Umrisse und Füllung je Schicht | umgesetzt | SlicerTests |
| DR-03 | Zeit, Filament, Kosten aus den Bahnen | umgesetzt | SlicerTests |
| DR-04 | Material, Schichthöhe, Wände, Füllung | umgesetzt | PrintPreview_Ui |
| DR-05 | Stützen-Schätzung | umgesetzt | SlicerTests |
| DR-06 | Skalieren, Kopien anordnen | umgesetzt | SlicerTests |
| DR-07 | Drucker im Heimnetz, Druck nach Bestätigung | umgesetzt | N23N24_CadPrintUi |
| DR-08 | Mehrere verschiedene Modelle auf einer Platte | offen | – |
| DR-09 | Bambu-Drucker im Netz finden und übernehmen, gleiches Netz prüfen | umgesetzt | BambuProTests, BambuPro_Ui |
| DR-10 | Einrichtung: WLAN, Nur LAN, Entwicklermodus, Zugangscode; WLAN-Signal in Klartext | umgesetzt | BambuProTests, BambuPro_Ui |
| DR-11 | Steuern: Licht, Tempo, Düse, Bett, Lüfter (mit Grenzen) | umgesetzt | BambuProTests, BambuPro_Ui |
| DR-12 | AMS-Filamente, Druckermeldungen (HMS) mit Nachschlagen | umgesetzt | BambuProTests, BambuPro_Ui |
| DR-13 | Live-Kamera, Fertig-Meldung, Zeitraffer aufnehmen und holen | umgesetzt (Kamera/FTPS nur am echten Drucker) | BambuProTests, BambuPro_Ui |
| DR-14 | Direkt aus Trendo vorbereiten (Bambu Studio im Hintergrund) und drucken | umgesetzt (echter Slicer-Lauf nicht im Test) | BambuProTests, BambuPro_Ui |
| DR-15 | Mehrere Drucker mit Zustand, Auswählen, Entfernen | umgesetzt | BambuProTests, BambuPro_Ui |
| D3-15 | Export für Unity (FBX), Unreal (FBX), Godot (GLB), Web (GLB) über Blender mit Engine-Einstellungen | umgesetzt, PC-Lauf mit Blender 5.2.3 (reports/paket01) | Studio3DEnginesTests, Studio3DEngines_Ui |
| D3-16 | Rundlauf-Prüfung: Export wieder einlesen und vergleichen (Teile, Dreiecke, Materialien, UV, Knochen, Animationen, Formschlüssel, Maße); Verlust = Fehler, gewollte Änderung = Hinweis; Bericht neben der Datei | umgesetzt | Studio3DEnginesTests, Studio3DEngines_Ui |
| D3-01 | 3D-Ansicht mit Drehen, Zoomen, Auswählen | umgesetzt | Studio3DViewport_Ui |
| D3-02 | Formen, Szene, Export | umgesetzt | W4_Core05_Ui |
| D3-03 | Freie Modelle in die Bibliothek | umgesetzt | Library3D_Ui |
| D3-04 | Arbeitsschritte Modellieren → UV → Material → Licht → Kamera → Render mit Stand je Schritt | umgesetzt | Studio3DProTests, Studio3DPro_Ui |
| D3-05 | Anfasser in der Ansicht: Verschieben, Drehen, Skalieren je Achse (Raster, Tasten G/R/S) | umgesetzt | Studio3DPro_Ui |
| D3-06 | Szenenbaum mit Gruppen und Ausblenden; Gruppe gemeinsam bewegen und einfärben | umgesetzt | Studio3DPro_Ui |
| D3-07 | PBR-Materialien (Metall, Rauheit, Glas) mit Vorschau in der Ansicht und im Blender-Render | umgesetzt | Studio3DProTests, Studio3DPro_Ui |
| D3-08 | Licht-Vorgaben (Studio, Tageslicht, Abendsonne, Innenraum) und Kamera mit Brennweite und Blickhöhe | umgesetzt | Studio3DProTests, Studio3DPro_Ui |
| D3-09 | Fotorealistischer Render (Cycles, Entrauschen, AgX, UV automatisch) mit Verlauf und Vorher/Nachher-Regler | umgesetzt (Blender-Lauf nicht im Test) | Studio3DProTests, Studio3DPro_Ui |
| D3-10 | Claude baut aus Satz oder Foto ein Modell mit echten Maßen, Materialien, Licht und Kamera – Plan mit 3D-Vorschau, danach Render zur Prüfung | umgesetzt | Claude3D_Ui |
| D3-11 | Freie Modelle aus Thingiverse (offizielle Schnittstelle, eigener App-Schlüssel), Smithsonian (CC0, eigener Schlüssel), NASA (gemeinfrei), Sketchfab (Suche frei, Laden mit eigenem Schlüssel) | umgesetzt (echte Schlüssel-Abrufe nicht im Test) | FreeModelsTests, Library3DPro_Ui |
| D3-12 | Seiten ohne Schnittstelle (Printables, MakerWorld, Cults3D, MyMiniFactory, Thangs): im Browser laden, Trendo übernimmt die Datei aus dem Download-Ordner – kein Abgreifen, kein Passwort | umgesetzt | FreeModelsTests, Library3DPro_Ui |
| D3-13 | Lizenz je Modell sichtbar, Filter „nur kommerziell“, Lizenzdatei mit Namensnennung | umgesetzt | FreeModelsTests, Library3DPro_Ui |
| D3-14 | Übernehmen ins 3D-Studio oder in den 3D-Druck (Maßeinheit erkennt der Import) | umgesetzt | Library3DPro_Ui |
| CA-01 | Boolesche Operationen (Vereinigung, Abzug, Schnitt) | umgesetzt | CadTests |
| CA-02 | Parametrisch mit Formeln | umgesetzt | CadTests |
| CA-03 | Extrudieren, Tasche, Drehteil, Rundung, Fase, Muster, Spiegeln | umgesetzt | CadTests |
| CA-04 | Export STL, 3MF, STEP, Zeichnung als PDF | umgesetzt | CadTests |
| CA-05 | Bauschritte im Menüband, Rückgängig | umgesetzt | N23N24_CadPrintUi |
| CA-06 | Eigenschaften: Volumen, Oberfläche, Masse je Werkstoff, Schwerpunkt | umgesetzt | CadMeasureTests |
| CA-07 | Querschnittsfläche | umgesetzt | CadMeasureTests |
| CA-08 | Normbohrungen M2–M12 | umgesetzt | N23N24_CadPrintUi |
| CA-09 | Skizzen mit Bedingungen | offen | – |
| CA-10 | Gelenke in Baugruppen | offen | – |
| DR-01 | Druckplatte, Aufstellen, Prüfungen | umgesetzt | PrintPreview_Ui |
| DR-02 | Umrisse und Füllung je Schicht | umgesetzt | SlicerTests |
| DR-03 | Zeit, Filament, Kosten aus den Bahnen | umgesetzt | SlicerTests |
| DR-04 | Material, Schichthöhe, Wände, Füllung | umgesetzt | PrintPreview_Ui |
| DR-05 | Stützen-Schätzung | umgesetzt | SlicerTests |
| DR-06 | Skalieren, Kopien anordnen | umgesetzt | SlicerTests |
| DR-07 | Drucker im Heimnetz, Druck nach Bestätigung | umgesetzt | N23N24_CadPrintUi |
| DR-08 | Mehrere verschiedene Modelle auf einer Platte | offen | – |
| DR-09 | Bambu-Drucker im Netz finden und übernehmen, gleiches Netz prüfen | umgesetzt | BambuProTests, BambuPro_Ui |
| DR-10 | Einrichtung: WLAN, Nur LAN, Entwicklermodus, Zugangscode; WLAN-Signal in Klartext | umgesetzt | BambuProTests, BambuPro_Ui |
| DR-11 | Steuern: Licht, Tempo, Düse, Bett, Lüfter (mit Grenzen) | umgesetzt | BambuProTests, BambuPro_Ui |
| DR-12 | AMS-Filamente, Druckermeldungen (HMS) mit Nachschlagen | umgesetzt | BambuProTests, BambuPro_Ui |
| DR-13 | Live-Kamera, Fertig-Meldung, Zeitraffer aufnehmen und holen | umgesetzt (Kamera/FTPS nur am echten Drucker) | BambuProTests, BambuPro_Ui |
| DR-14 | Direkt aus Trendo vorbereiten (Bambu Studio im Hintergrund) und drucken | umgesetzt (echter Slicer-Lauf nicht im Test) | BambuProTests, BambuPro_Ui |
| DR-15 | Mehrere Drucker mit Zustand, Auswählen, Entfernen | umgesetzt | BambuProTests, BambuPro_Ui |
| D3-15 | Export für Unity (FBX), Unreal (FBX), Godot (GLB), Web (GLB) über Blender mit Engine-Einstellungen | umgesetzt, PC-Lauf mit Blender 5.2.3 (reports/paket01) | Studio3DEnginesTests, Studio3DEngines_Ui |
| D3-16 | Rundlauf-Prüfung: Export wieder einlesen und vergleichen (Teile, Dreiecke, Materialien, UV, Knochen, Animationen, Formschlüssel, Maße); Verlust = Fehler, gewollte Änderung = Hinweis; Bericht neben der Datei | umgesetzt | Studio3DEnginesTests, Studio3DEngines_Ui |

## Fehlende Funktionen laut Vorbild-Analyse

- ✗ Echte BREP-Flächen (STEP ist facettiert)
- ✗ Eigener G-Code – bewusst nicht: die Bahnen für den Drucker rechnet der Fachslicer (Sicherheit für Drucker und Düse)
- ✗ Import in Unity/Unreal/Godot selbst automatisch prüfen
- ✗ Mehrere verschiedene Modelle auf einer Platte (nur Kopien desselben)
- ✗ Skizzen mit Bedingungen (parallel, tangential, Maß gebunden)

## Teilweise vorhanden

- ◐ Baugruppen: Teile mit Lage, aber ohne Verbindungen/Gelenke
- ◐ Stützen werden geschätzt, aber nicht als Bahnen erzeugt
