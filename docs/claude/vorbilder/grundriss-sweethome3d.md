# Vorbild-Analyse Grundriss: Sweet Home 3D 7.5 (Clean-Room)

- Quelle: offizielle JAR `SweetHome3D-7.5.jar` von sourceforge.net/projects/sweethome3d (Download mit Zustimmung von Manuel am 31.10.2026), SHA-256 `1a0f025a0b4d2956098469114fa9e12b994819699c683dfda2d6854d8d829590`, Lizenz GPL-2.0
- Werkzeug: REA (`open_binary`, `inspect_artifact`, `extract_artifact`), Evidence `ev_e5e09afc…` (Inventar, 4.087 Dateien), `ev_e0eadc58…` (Entpacken); dazu das öffentliche Benutzerhandbuch
- Übernommen wird **nur Funktionsumfang und Bedienverhalten** (Clean-Room). Kein Code, keine Grafiken, keine Texte. Trendo bleibt eigener Code.
- Grenzen: rein statische Auswertung von Paketinhalt, Klassennamen und Oberflächentexten. Das Programm wurde nicht ausgeführt, Verhalten im Detail ist daraus abgeleitet.

## Beobachtet (Bedienzustände des Plan-Editors, Klassen `PlanController$…State`)
Auswahl, Rechteckauswahl, Verschieben, Verschieben der Ansicht (Pan); Wand: Erstellen, Zeichnen, Größe ändern, Bogen (Arc extent); Raum: Erstellen, Zeichnen (Punkt für Punkt), Größe ändern, Name/Fläche verschieben und drehen; Bemaßung: Erstellen, Zeichnen, Versatz, Größe, Höhe, Drehung; Polylinie: Erstellen, Zeichnen, Ändern; Text: Erstellen, Drehen, Höhe; Möbel: Drehen, Höhe, Breite/Tiefe, Höhenlage, Name verschieben/drehen; Kompass drehen/Größe; Besucherkamera (Körper-/Kopfwinkel, Augenhöhe); Ziehen & Ablegen.

## Beobachtet (Menübefehle „Plan“, „Ebenen“, „3D-Ansicht“)
Auswählen, Verschieben, Wände/Räume/Polylinien/Bemaßungen/Texte erstellen; Grundplan sperren; Magnetismus an/aus; horizontal/vertikal spiegeln; Kompass; Wände ändern, verbinden, Richtung umkehren, teilen; Räume ändern, Punkt hinzufügen/entfernen, Punkte neu berechnen; Textstil (größer/kleiner, fett, kursiv); Hintergrundbild importieren/ändern/ausblenden/löschen (Maßstab über zwei Punkte, Ursprung); Ebenen: hinzufügen, auf gleicher Höhe hinzufügen, sichtbar/unsichtbar, nur diese sichtbar, alle sichtbar, ändern, löschen; Zoom; Export SVG, OBJ, CSV (Möbelliste); Drucken/PDF mit Seiteneinrichtung; Luftbild und virtueller Besuch, gespeicherte Blickpunkte, Foto, Video.

## Abgleich mit Trendo „Grundriss & Wohnen“ (0.9.24)
| Vorbild-Funktion | Trendo 0.9.24 | Plan |
|---|---|---|
| Wände zeichnen, Länge eintippen, Fang | vorhanden | Winkel in 15°-Schritten ergänzen (T-0257) |
| Raum per Doppelklick erkennen | vorhanden | Maus-Test ergänzen (T-0257) |
| Raum Punkt für Punkt zeichnen | fehlt | T-0257 |
| Bemaßung als eigenes Werkzeug | fehlt (nur automatische Außenmaße) | T-0257 |
| Texte im Plan | fehlt | T-0257 |
| Ebenen/Geschosse | fehlt | T-0257 |
| Hintergrundbild mit Maßstab über 2 Punkte | fehlt | T-0257 |
| Wand teilen, Möbel spiegeln | fehlt | T-0257 |
| Export OBJ | fehlt | T-0257 |
| Dachschrägen (Wohnfläche) | fehlt | T-0257 (Kniestock + Schrägentiefe) |
| Wandbögen, Polylinien, Kompass, virtueller Besuch, Foto/Video, Texturen | fehlt | später |
