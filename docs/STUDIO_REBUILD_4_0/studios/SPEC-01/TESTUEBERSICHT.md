# Testübersicht: Wohnung planen

Studio SPEC-01 · Wohnung planen · Bereich Technik & Fachgebiete

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

Gezählte Tests des Studios: 113. Gefordert für die Abnahme 4.0: mindestens 20 eigene positive und negative Testfälle.

## Dem Studio zugeordnete Tests

Die Zuordnung kommt aus dem Programmkatalog und ist ungenau: Sie kann Tests anderer Studios enthalten (siehe REA-Audit).

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
- StudioCatalog.NameOf: Katalogname, sonst der bisherige Text
- T-0257 UI: Doppelklick erkennt Raum, Raum Punkt für Punkt (Enter), Bemaßen mit drei Klicks, Text setzen und ändern
- T-0257 UI: Geschoss anlegen und wechseln, Vorlage-Bild laden und Maßstab über zwei Klicks festlegen, OBJ-Export
- T-0280 UI: 3D-Ansicht zeigt echte Möbelmodelle statt Kisten
- T-0280 UI: Foto-Ansicht – Raumansicht mit gewähltem Raum, Fortschritt, Abbrechen, Bild zeigen und speichern
- T-0280 UI: Modell aus der 3D-Bibliothek im Möbelkatalog wählen und stellen – Maße aus dem Modell, in 3D als Modell
- T-0280 UI: Ohne Blender klare Meldung statt Absturz

## Pflichtprüfungen der Abnahme 4.0

Alle Punkte offen, bis ein Testartefakt vorliegt: alte Funktionen, neue Funktionen, Bedienbarkeit, Erreichbarkeit, Dateikompatibilität, Fehlerbehandlung, Rechte, Leistung, Speicher, Stabilität, Tastatur und Maus, Ziehen und Ablegen, Rückgängig, Wiederherstellung, Exporte, Zusammenspiel mit anderen Studios.
