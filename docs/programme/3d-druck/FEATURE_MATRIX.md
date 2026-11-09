# Trendo 3D-Druck – Funktionsmatrix

Studio: 3D-Modelle erstellen (CORE-05) · Bereich: 3D, Spiele & VR · Seite: `3d-druck` · Stand: 0.9.36 (Stand 20e850a)

Quelle: `docs/spec/kataloge/CORE-05_KATALOG.md`

| Nr | Funktion | Einordnung | Stand laut Katalog |
|---|---|---|---|
| D3-01 | 3D-Ansicht mit Drehen, Zoomen, Auswählen | Vorhanden | umgesetzt |
| D3-02 | Formen, Szene, Export | Vorhanden | umgesetzt |
| D3-03 | Freie Modelle in die Bibliothek | Vorhanden | umgesetzt |
| D3-04 | Arbeitsschritte Modellieren → UV → Material → Licht → Kamera → Render mit Stand je Schritt | Vorhanden | umgesetzt |
| D3-05 | Anfasser in der Ansicht: Verschieben, Drehen, Skalieren je Achse (Raster, Tasten G/R/S) | Vorhanden | umgesetzt |
| D3-06 | Szenenbaum mit Gruppen und Ausblenden; Gruppe gemeinsam bewegen und einfärben | Vorhanden | umgesetzt |
| D3-07 | PBR-Materialien (Metall, Rauheit, Glas) mit Vorschau in der Ansicht und im Blender-Render | Vorhanden | umgesetzt |
| D3-08 | Licht-Vorgaben (Studio, Tageslicht, Abendsonne, Innenraum) und Kamera mit Brennweite und Blickhöhe | Vorhanden | umgesetzt |
| D3-09 | Fotorealistischer Render (Cycles, Entrauschen, AgX, UV automatisch) mit Verlauf und Vorher/Nachher-Regler | Vorhanden | umgesetzt (Blender-Lauf nicht im Test) |
| D3-10 | Claude baut aus Satz oder Foto ein Modell mit echten Maßen, Materialien, Licht und Kamera – Plan mit 3D-Vorschau, danach Render zur Prüfung | Vorhanden | umgesetzt |
| D3-11 | Freie Modelle aus Thingiverse (offizielle Schnittstelle, eigener App-Schlüssel), Smithsonian (CC0, eigener Schlüssel), NASA (gemeinfrei), Sketchfab (Suche frei, Laden mit eigenem Schlüssel) | Vorhanden | umgesetzt (echte Schlüssel-Abrufe nicht im Test) |
| D3-12 | Seiten ohne Schnittstelle (Printables, MakerWorld, Cults3D, MyMiniFactory, Thangs): im Browser laden, Trendo übernimmt die Datei aus dem Download-Ordner – kein Abgreifen, kein Passwort | Vorhanden | umgesetzt |
| D3-13 | Lizenz je Modell sichtbar, Filter „nur kommerziell“, Lizenzdatei mit Namensnennung | Vorhanden | umgesetzt |
| D3-14 | Übernehmen ins 3D-Studio oder in den 3D-Druck (Maßeinheit erkennt der Import) | Vorhanden | umgesetzt |
| CA-01 | Boolesche Operationen (Vereinigung, Abzug, Schnitt) | Vorhanden | umgesetzt |
| CA-02 | Parametrisch mit Formeln | Vorhanden | umgesetzt |
| CA-03 | Extrudieren, Tasche, Drehteil, Rundung, Fase, Muster, Spiegeln | Vorhanden | umgesetzt |
| CA-04 | Export STL, 3MF, STEP, Zeichnung als PDF | Vorhanden | umgesetzt |
| CA-05 | Bauschritte im Menüband, Rückgängig | Vorhanden | umgesetzt |
| CA-06 | Eigenschaften: Volumen, Oberfläche, Masse je Werkstoff, Schwerpunkt | Vorhanden | umgesetzt |
| CA-07 | Querschnittsfläche | Vorhanden | umgesetzt |
| CA-08 | Normbohrungen M2–M12 | Vorhanden | umgesetzt |
| CA-09 | Skizzen mit Bedingungen | Noch zu entwickeln | offen |
| CA-10 | Gelenke in Baugruppen | Noch zu entwickeln | offen |
| DR-01 | Druckplatte, Aufstellen, Prüfungen | Vorhanden | umgesetzt |
| DR-02 | Umrisse und Füllung je Schicht | Vorhanden | umgesetzt |
| DR-03 | Zeit, Filament, Kosten aus den Bahnen | Vorhanden | umgesetzt |
| DR-04 | Material, Schichthöhe, Wände, Füllung | Vorhanden | umgesetzt |
| DR-05 | Stützen-Schätzung | Vorhanden | umgesetzt |
| DR-06 | Skalieren, Kopien anordnen | Vorhanden | umgesetzt |
| DR-07 | Drucker im Heimnetz, Druck nach Bestätigung | Vorhanden | umgesetzt |
| DR-08 | Mehrere verschiedene Modelle auf einer Platte | Noch zu entwickeln | offen |
| DR-09 | Bambu-Drucker im Netz finden und übernehmen, gleiches Netz prüfen | Vorhanden | umgesetzt |
| DR-10 | Einrichtung: WLAN, Nur LAN, Entwicklermodus, Zugangscode; WLAN-Signal in Klartext | Vorhanden | umgesetzt |
| DR-11 | Steuern: Licht, Tempo, Düse, Bett, Lüfter (mit Grenzen) | Vorhanden | umgesetzt |
| DR-12 | AMS-Filamente, Druckermeldungen (HMS) mit Nachschlagen | Vorhanden | umgesetzt |
| DR-13 | Live-Kamera, Fertig-Meldung, Zeitraffer aufnehmen und holen | Vorhanden | umgesetzt (Kamera/FTPS nur am echten Drucker) |
| DR-14 | Direkt aus Trendo vorbereiten (Bambu Studio im Hintergrund) und drucken | Vorhanden | umgesetzt (echter Slicer-Lauf nicht im Test) |
| DR-15 | Mehrere Drucker mit Zustand, Auswählen, Entfernen | Vorhanden | umgesetzt |
| D3-15 | Export für Unity (FBX), Unreal (FBX), Godot (GLB), Web (GLB) über Blender mit Engine-Einstellungen | Vorhanden | umgesetzt, PC-Lauf mit Blender 5.2.3 (reports/paket01) |
| D3-16 | Rundlauf-Prüfung: Export wieder einlesen und vergleichen (Teile, Dreiecke, Materialien, UV, Knochen, Animationen, Formschlüssel, Maße); Verlust = Fehler, gewollte Änderung = Hinweis; Bericht neben der Datei | Vorhanden | umgesetzt |

## Werkzeuge der Oberfläche
- Los
- Hinzufügen …
- ✳ Claude
- Mehr ▾
- Datei wählen …
- Aus Trendo CAD
- Bambu Studio einrichten (kostenlos, AGPL)
- 🔍 Drucker im Netz finden
- Einrichtung (WLAN, Nur LAN, Entwicklermodus)
- Speichern
- Verbinden
