# Vorbild-Analyse Modell-Prüfer: glTF-Viewer, Validator (Clean-Room)

- Programme: „3D-Modelle ansehen“
- Recherche: glTF-Viewer (Web-Betrachter mit Statistik), glTF-Validator (Khronos), Leistungsrichtwerte für Standalone-Brillen und Handy-AR
- Werkzeug: Funktionsumfang aus öffentlicher Doku; kein Download, kein Code übernommen. REA nicht eingesetzt.

## Übernommen in Trendo „Modell-Prüfer“ (0.9.28)
3D-Betrachter mit Orbit und Drahtgitter, Statistik (Dreiecke, Zeichenaufrufe, Texturen, Größe), Richtwert-Ampel je Gerät, Maßstab-Prüfung, Grafikspeicher je Textur, Prüfbericht.

## Profi- und Claude-Ausbau (06.11.2026)
- ✓ Texturgrenze je Brille als eigene Anzeige (größte Textur gegen Gerätegrenze)
- ✓ Vorschau aus Augenhöhe: 1,60 m, 2 m Abstand, 90° Bildwinkel wie eine Brille, Boden bis zum Horizont
- ✓ Brillen-Tauglichkeit mit einem Klick reparieren (Blender): Dreiecke auf 90 % des Budgets, Texturen verkleinern, Maßstab auf Meter – als neue Datei, Original bleibt; Claude sieht die Prüfung und kann „Reparieren“ anstoßen

## Noch nicht
Texturen in der Ansicht, Animationen abspielen, volle Validierung nach Khronos-Regeln, Live-Vorschau auf der Brille.

## T-0722 (16.11.2026, Nutzerentscheidung)
Die Seitengruppe heißt „3D-Modelle ansehen“. Entfernt sind die VR-Teile: Richtwert-Ampel je Brille, Texturgrenze je Brille, Brillen-Ansicht,
„Für die Brille reparieren“, Komfort- und Bildzeit-Rechner. Statt der Geräte-Ampel zeigt der Prüfer Maße, Dreiecke, Teile, Materialien,
Texturen und „Fehler im Modell“ ohne Zielgerät – für 3D-Druck und Architektur. Die Vorschau aus Augenhöhe bleibt (stehend im Raum).
