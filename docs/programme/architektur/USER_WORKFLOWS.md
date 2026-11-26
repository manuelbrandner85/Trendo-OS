# Grundriss & Wohnen – Arbeitsabläufe

Studio: Wohnung planen (SPEC-01) · Bereich: Technik & Fachgebiete · Seite: `architektur` · Stand: 0.9.112 (Stand d6f9fcd)

Die Abläufe unten sind die Oberflächentests, die das Programm über den echten Eingabeweg bedienen (Klicks, Tastatur).

- SPEC-01 UI Ausbau: Material, Tapete, Heizung, Licht, Treppe und Wohnfläche direkt in den Eigenschaften
- SPEC-01 UI: Bogenwand – Anfang und Ende klicken, Wölbung eintippen, Radius stimmt; Nordrichtung dreht den Nordpfeil; Rückgängig
- SPEC-01 UI: Grundriss – Vorlage, Raum auswählen und umbenennen, Boden wählen, Wandlänge eintippen, Plan prüfen, 3D, Exporte
- SPEC-01 UI: Grundriss – leerer Plan, Raum mit der Maus aufziehen, Tür und Fenster setzen, Möbel stellen und ziehen, Entf, Rückgängig
- SPEC-01 UI: Wand-Werkzeug – Wandzug Punkt für Punkt, Länge tippen + Enter, Esc beendet
- T-0257 UI: Doppelklick erkennt Raum, Raum Punkt für Punkt (Enter), Bemaßen mit drei Klicks, Text setzen und ändern
- T-0257 UI: Geschoss anlegen und wechseln, Vorlage-Bild laden und Maßstab über zwei Klicks festlegen, OBJ-Export
- T-0280 UI: 3D-Ansicht zeigt echte Möbelmodelle statt Kisten
- T-0280 UI: Foto-Ansicht – Raumansicht mit gewähltem Raum, Fortschritt, Abbrechen, Bild zeigen und speichern
- T-0280 UI: Modell aus der 3D-Bibliothek im Möbelkatalog wählen und stellen – Maße aus dem Modell, in 3D als Modell
- T-0280 UI: Ohne Blender klare Meldung statt Absturz
- T-0747 UI: CNC – „Aus DXF/SVG …“ → Bahnen mit Versatz; Haltestege eintippen; Rückgängig; Rechtsklick-Menü; Export .nc
- T-0747 UI: Grundriss – „DXF einlesen …“ → Wände, Tür, Räume; Rückgängig; Wand ändern; Export DXF liest sich wieder
- T-0747-fix UI: Fräser-Ø/Vorschub geändert → Bahnen aus der Zeichnung neu (ein Schritt); geht es nicht, ist der Export gesperrt
- T-0747-fix UI: große DXF (12 000 Linien) im Hintergrund – Karte mit Abbrechen, Ergebnis als ein Schritt; Abbrechen ändert nichts
- T-0747-fix2 UI: Trendo CAD zeigt, dass eine Zoll-DXF in mm umgerechnet wurde
- Wohnung UI: Uhrzeit und Monat gehen als echter Sonnenstand in die Szene, ohne Wahl bleibt das Licht wie bisher
