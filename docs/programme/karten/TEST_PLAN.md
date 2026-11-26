# Karten & Touren – Testplan

Studio: Routen & Karten (SPEC-13) · Bereich: Technik & Fachgebiete · Seite: `karten` · Stand: 0.9.112 (Stand d6f9fcd)

22 Tests sprechen dieses Programm an.

- Programmkatalog: Karten-Programm aus dem Code – Engine MapDoc, Formate GPX/KML, OSM-Kacheln, 20 Katalogfunktionen, Vorbild-Analyse, Tests
- SPEC-13 F02: GPX-Routen
- SPEC-13 F13: UTM → Grad
- SPEC-13 F14: Richtung
- SPEC-13 F15: Zielpunkt
- SPEC-13 F16: Fläche
- SPEC-13 F17: Höhenprofil
- SPEC-13 F18: Wanderzeit
- SPEC-13 F19: Vereinfachen
- SPEC-13 F20: GPX schreiben
- SPEC-13 UI: GPX laden – Spur mit Höhenprofil und Kennzahlen; Spur auf der Karte anklicken, umbenennen, exportieren
- SPEC-13 UI: Höhen ergänzen fragt erst – „Nein“ sendet nichts, „Ja“ holt die Höhen; Route entlang von Wegen über den eingetragenen Server
- SPEC-13 UI: Kachel-Ordner als Offline-Paket laden – Karte springt hin, Info zeigt Kacheln und Zoom, bleibt nach Neustart gemerkt
- SPEC-13 UI: Route mit der Maus zeichnen (Doppelklick beendet), Wegpunkt setzen, Fläche messen (Enter), Rechner rechnen sofort
- SPEC-13: GPX lesen, km/Höhenmeter/Dauer, GeoJSON, Koordinaten DMS/UTM, Luftlinie
- T-0719 Karten: GPX im Garmin-Aufbau auf die Karte ziehen – jede Spur/Route einzeln, ein Rückgängig-Schritt; alte Karten-Datei öffnen und bearbeiten; Assistenzweg schreibt einen Wegpunkt ins Dokument
- T-0719 Karten: leere Karte → erster Schritt → Route mit der Maus, Punkt ziehen, Punkt aus der Mitte einfügen, Rechtsklick löschen, Wegpunkt setzen/ziehen/umbenennen, Rückgängig je Schritt, GPX-Datei
- T-0719 Tagesplaner: alte Datei (Plan mit Termin-Kopien) öffnen – Zeitleiste zeigt alles, Termin ziehen hält Plan und Termin gleich; Assistenzweg schreibt einen Block, der in der Zeitleiste steht
- T-0719 Tagesplaner: leerer Tag → erster Block, im Block benennen, ziehen (verschieben), Kante ziehen (Dauer), Doppelklick neu, Rechtsklick Termin/länger, Pfeiltaste, Entf, Rückgängig je Schritt, Kalenderdatei mit Ical.Net geprüft
- T-0726 A6: Kartenkacheln aus dem Internet nur nach Zustimmung über das Freigabe-Tor – Nein lädt nichts, Ja gilt als Einstellung (im Protokoll), ausschaltbar; offline kein Angebot
- T-0726 A7: Lernkarte löschen lässt sich rückgängig machen (Knopf und Strg+Z) – mit Lernstand und an alter Stelle; Bearbeiten nach der Tipp-Pause gespeichert
- Vektorkarte-Oberfläche: .pmtiles mit MVT → MapLibre füllt die Arbeitsfläche statt der Rasterkarte; bereit → Start, Kachel → Antwort; Schließen
