# Testübersicht: Trendo erweitern

Studio X-04 · Trendo erweitern · Bereich Entwickeln

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

Gezählte Tests des Studios: 124. Gefordert für die Abnahme 4.0: mindestens 20 eigene positive und negative Testfälle.

## Dem Studio zugeordnete Tests

Die Zuordnung kommt aus dem Programmkatalog und ist ungenau: Sie kann Tests anderer Studios enthalten (siehe REA-Audit).

- T-0241 (UI): Not-Stopp verhindert das Installieren; Entfernen fragt vorher; Abbrechen ändert nichts
- X-04 (UI): Beispiel installieren, im Baukasten öffnen, ausschalten sperrt Benutzen
- X-04 Katalog (UI): mitgelieferten Katalog zeigen und geprüft installieren
- X-04 Katalog: lokaler Katalog mit Prüfsummen, Installation nur bei passender Prüfsumme
- X-04 Katalog: nur GitHub-https oder lokale ZIP, Prüfsumme Pflicht, Kennung muss passen
- X-04: Beispiel-Erweiterung installieren (Ordner und ZIP), ein/aus, Prüfsumme stabil, entfernen
- X-04: Erweiterungs-Katalog – nur GitHub über https oder lokale ZIP, Prüfsumme mit 64 Hexzeichen Pflicht, unbekannte Art abgelehnt; Weiterleitungen nur zu GitHub auf 443
- X-04: Programmcode, falsche Rechte, kaputte Beschreibung und ZIP-Ausbruch werden abgewiesen

## Pflichtprüfungen der Abnahme 4.0

Alle Punkte offen, bis ein Testartefakt vorliegt: alte Funktionen, neue Funktionen, Bedienbarkeit, Erreichbarkeit, Dateikompatibilität, Fehlerbehandlung, Rechte, Leistung, Speicher, Stabilität, Tastatur und Maus, Ziehen und Ablegen, Rückgängig, Wiederherstellung, Exporte, Zusammenspiel mit anderen Studios.
