# Testübersicht: Routen & Karten

Studio SPEC-13 · Routen & Karten · Bereich Technik & Fachgebiete

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

Gezählte Tests des Studios: 92. Gefordert für die Abnahme 4.0: mindestens 20 eigene positive und negative Testfälle.

## Dem Studio zugeordnete Tests

Die Zuordnung kommt aus dem Programmkatalog und ist ungenau: Sie kann Tests anderer Studios enthalten (siehe REA-Audit).

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
- Vektorkarte-Oberfläche: .pmtiles mit MVT → MapLibre füllt die Arbeitsfläche statt der Rasterkarte; bereit → Start, Kachel → Antwort; Schließen

## Pflichtprüfungen der Abnahme 4.0

Alle Punkte offen, bis ein Testartefakt vorliegt: alte Funktionen, neue Funktionen, Bedienbarkeit, Erreichbarkeit, Dateikompatibilität, Fehlerbehandlung, Rechte, Leistung, Speicher, Stabilität, Tastatur und Maus, Ziehen und Ablegen, Rückgängig, Wiederherstellung, Exporte, Zusammenspiel mit anderen Studios.
