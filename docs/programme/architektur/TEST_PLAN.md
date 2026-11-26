# Grundriss & Wohnen – Testplan

Studio: Wohnung planen (SPEC-01) · Bereich: Technik & Fachgebiete · Seite: `architektur` · Stand: 0.9.112 (Stand d6f9fcd)

32 Tests sprechen dieses Programm an.

- IFC-Ansicht: Menüband „IFC ansehen“ exportiert und öffnet die 3D-Ansicht; bereit → Datei als Base64; geladen → Bauteile; nochmal klicken schließt
- IFC-Oberfläche: ohne Einrichtung klarer Hinweis; mit IfcOpenShell → .ifc im Ordner Grundrisse, Kontrollbericht, Hilfsdateien aufgeräumt
- SPEC-01 F08: Fliesenbedarf
- SPEC-01 F09: Tapetenrollen
- SPEC-01 F10: Möbel-Stellprüfung
- SPEC-01 F11: Heizlast-Schätzung
- SPEC-01 F12: Licht-Bedarf
- SPEC-01 F13: Kostenschätzung Renovierung
- SPEC-01 F14: Treppe berechnen
- SPEC-01 F15: Wohnfläche nach WoFlV
- SPEC-01 F16: Raumliste als CSV
- SPEC-01 UI Ausbau: Material, Tapete, Heizung, Licht, Treppe und Wohnfläche direkt in den Eigenschaften
- SPEC-01 UI: Bogenwand – Anfang und Ende klicken, Wölbung eintippen, Radius stimmt; Nordrichtung dreht den Nordpfeil; Rückgängig
- SPEC-01 UI: Grundriss – Vorlage, Raum auswählen und umbenennen, Boden wählen, Wandlänge eintippen, Plan prüfen, 3D, Exporte
- SPEC-01 UI: Grundriss – leerer Plan, Raum mit der Maus aufziehen, Tür und Fenster setzen, Möbel stellen und ziehen, Entf, Rückgängig
- SPEC-01 UI: Wand-Werkzeug – Wandzug Punkt für Punkt, Länge tippen + Enter, Esc beendet
- SPEC-01: Raumplaner rechnet Fläche, Farbe, Boden, Leisten und speichert dauerhaft
- Studio-Seiten: Überschrift ist der Name aus dem Studio-Katalog
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
- T-0775 ECHT Technik: Claude verschiebt im Programm „Grundriss & Wohnen“ eine Wand im offenen Plan – Strg+Z nimmt es zurück (nur mit TRENDO_ECHT_CLAUDE)
- T-0776 echt: Wohnung planen (SPEC-01) – fremde Wohnungs-DXF in Trendo bearbeitet und als DXF exportiert; FreeCAD 1.1 liest Wände und Umriss in Plangröße
- Wohnung UI: Uhrzeit und Monat gehen als echter Sonnenstand in die Szene, ohne Wahl bleibt das Licht wie bisher
