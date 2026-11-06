# Trendo CAD – Umsetzungsstand

Studio: 3D-Modelle erstellen (CORE-05) · Bereich: 3D, Spiele & VR · Seite: `cad` · Stand: 0.9.36 (Stand df8e056)

Stufen: Geplant · In Arbeit · Implementiert · Getestet · Blockiert. „Getestet“ nur, wenn der zugehörige Test im letzten echten Lauf bestanden hat.

| Nr | Funktion | Stand | Test |
|---|---|---|---|
| D3-01 | 3D-Ansicht mit Drehen, Zoomen, Auswählen | Implementiert | `Studio3DViewport_Ui` |
| D3-02 | Formen, Szene, Export | Implementiert | `W4_Core05_Ui` |
| D3-03 | Freie Modelle in die Bibliothek | Implementiert | `Library3D_Ui` |
| D3-04 | Arbeitsschritte Modellieren → UV → Material → Licht → Kamera → Render mit Stand je Schritt | Implementiert | `Studio3DProTests, Studio3DPro_Ui` |
| D3-05 | Anfasser in der Ansicht: Verschieben, Drehen, Skalieren je Achse (Raster, Tasten G/R/S) | Implementiert | `Studio3DPro_Ui` |
| D3-06 | Szenenbaum mit Gruppen und Ausblenden; Gruppe gemeinsam bewegen und einfärben | Implementiert | `Studio3DPro_Ui` |
| D3-07 | PBR-Materialien (Metall, Rauheit, Glas) mit Vorschau in der Ansicht und im Blender-Render | Implementiert | `Studio3DProTests, Studio3DPro_Ui` |
| D3-08 | Licht-Vorgaben (Studio, Tageslicht, Abendsonne, Innenraum) und Kamera mit Brennweite und Blickhöhe | Implementiert | `Studio3DProTests, Studio3DPro_Ui` |
| D3-09 | Fotorealistischer Render (Cycles, Entrauschen, AgX, UV automatisch) mit Verlauf und Vorher/Nachher-Regler | Implementiert | `Studio3DProTests, Studio3DPro_Ui` |
| D3-10 | Claude baut aus Satz oder Foto ein Modell mit echten Maßen, Materialien, Licht und Kamera – Plan mit 3D-Vorschau, danach Render zur Prüfung | Implementiert | `Claude3D_Ui` |
| D3-11 | Freie Modelle aus Thingiverse (offizielle Schnittstelle, eigener App-Schlüssel), Smithsonian (CC0, eigener Schlüssel), NASA (gemeinfrei), Sketchfab (Suche frei, Laden mit eigenem Schlüssel) | Implementiert | `FreeModelsTests, Library3DPro_Ui` |
| D3-12 | Seiten ohne Schnittstelle (Printables, MakerWorld, Cults3D, MyMiniFactory, Thangs): im Browser laden, Trendo übernimmt die Datei aus dem Download-Ordner – kein Abgreifen, kein Passwort | Implementiert | `FreeModelsTests, Library3DPro_Ui` |
| D3-13 | Lizenz je Modell sichtbar, Filter „nur kommerziell“, Lizenzdatei mit Namensnennung | Implementiert | `FreeModelsTests, Library3DPro_Ui` |
| D3-14 | Übernehmen ins 3D-Studio oder in den 3D-Druck (Maßeinheit erkennt der Import) | Implementiert | `Library3DPro_Ui` |
| CA-01 | Boolesche Operationen (Vereinigung, Abzug, Schnitt) | Implementiert | `CadTests` |
| CA-02 | Parametrisch mit Formeln | Implementiert | `CadTests` |
| CA-03 | Extrudieren, Tasche, Drehteil, Rundung, Fase, Muster, Spiegeln | Implementiert | `CadTests` |
| CA-04 | Export STL, 3MF, STEP, Zeichnung als PDF | Implementiert | `CadTests` |
| CA-05 | Bauschritte im Menüband, Rückgängig | Implementiert | `N23N24_CadPrintUi` |
| CA-06 | Eigenschaften: Volumen, Oberfläche, Masse je Werkstoff, Schwerpunkt | Implementiert | `CadMeasureTests` |
| CA-07 | Querschnittsfläche | Implementiert | `CadMeasureTests` |
| CA-08 | Normbohrungen M2–M12 | Implementiert | `N23N24_CadPrintUi` |
| CA-09 | Skizzen mit Bedingungen | Geplant | `–` |
| CA-10 | Gelenke in Baugruppen | Geplant | `–` |
| DR-01 | Druckplatte, Aufstellen, Prüfungen | Implementiert | `PrintPreview_Ui` |
| DR-02 | Umrisse und Füllung je Schicht | Implementiert | `SlicerTests` |
| DR-03 | Zeit, Filament, Kosten aus den Bahnen | Implementiert | `SlicerTests` |
| DR-04 | Material, Schichthöhe, Wände, Füllung | Implementiert | `PrintPreview_Ui` |
| DR-05 | Stützen-Schätzung | Implementiert | `SlicerTests` |
| DR-06 | Skalieren, Kopien anordnen | Implementiert | `SlicerTests` |
| DR-07 | Drucker im Heimnetz, Druck nach Bestätigung | Implementiert | `N23N24_CadPrintUi` |
| DR-08 | Mehrere verschiedene Modelle auf einer Platte | Geplant | `–` |
| DR-09 | Bambu-Drucker im Netz finden und übernehmen, gleiches Netz prüfen | Implementiert | `BambuProTests, BambuPro_Ui` |
| DR-10 | Einrichtung: WLAN, Nur LAN, Entwicklermodus, Zugangscode; WLAN-Signal in Klartext | Implementiert | `BambuProTests, BambuPro_Ui` |
| DR-11 | Steuern: Licht, Tempo, Düse, Bett, Lüfter (mit Grenzen) | Implementiert | `BambuProTests, BambuPro_Ui` |
| DR-12 | AMS-Filamente, Druckermeldungen (HMS) mit Nachschlagen | Implementiert | `BambuProTests, BambuPro_Ui` |
| DR-13 | Live-Kamera, Fertig-Meldung, Zeitraffer aufnehmen und holen | Implementiert | `BambuProTests, BambuPro_Ui` |
| DR-14 | Direkt aus Trendo vorbereiten (Bambu Studio im Hintergrund) und drucken | Implementiert | `BambuProTests, BambuPro_Ui` |
| DR-15 | Mehrere Drucker mit Zustand, Auswählen, Entfernen | Implementiert | `BambuProTests, BambuPro_Ui` |

0 von 39 Funktionen getestet.
