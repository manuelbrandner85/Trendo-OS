# Testübersicht: Programme nachbauen

Studio X-10 · Programme nachbauen · Bereich Entwickeln

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

Gezählte Tests des Studios: 82. Gefordert für die Abnahme 4.0: mindestens 20 eigene positive und negative Testfälle.

## Dem Studio zugeordnete Tests

Die Zuordnung kommt aus dem Programmkatalog und ist ungenau: Sie kann Tests anderer Studios enthalten (siehe REA-Audit).

- T-0201: Datei aufs Feld oder aufs Studio ziehen füllt das passende Feld
- T-0201: Mausklick auf „Datei wählen …“ füllt das Feld, die Datei steht danach unter „Zuletzt“; Eintippen geht weiter
- T-0201: Ordnerfeld öffnet den Ordner-Dialog
- T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“
- X-10 (UI): Projekt anlegen, Tagebuch-Eintrag, Prüfung ohne Treffer schreibt Bericht
- X-10: Projekt mit Rollen-Ordnern, Tagebuch, Vergleich zweier Ordner und Prüfbericht
- X-10: Sauberer Nachbau – gleicher Code hat Ähnlichkeit 1, eigener Code mit anderer Struktur niedrig; Vergleich meldet nur Dateien über der Schwelle
- X-10: Ähnlichkeit erkennt kopierten Code trotz Umbenennen, eigener Code bleibt unauffällig

## Pflichtprüfungen der Abnahme 4.0

Alle Punkte offen, bis ein Testartefakt vorliegt: alte Funktionen, neue Funktionen, Bedienbarkeit, Erreichbarkeit, Dateikompatibilität, Fehlerbehandlung, Rechte, Leistung, Speicher, Stabilität, Tastatur und Maus, Ziehen und Ablegen, Rückgängig, Wiederherstellung, Exporte, Zusammenspiel mit anderen Studios.
