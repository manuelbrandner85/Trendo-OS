---
name: Spiele-Entwicklung
description: Spielgefühl, Leveldesign, Balancing und saubere Spiellogik für Spiele und interaktive Welten.
studios: CORE-06, SPEC-04
lizenz: Trendo OS (eigener Inhalt)
---

# Spiele-Entwicklung

## Spielgefühl
- Jede Eingabe bekommt sofort Rückmeldung: Ton, kurze Animation, Reaktion im Bild – innerhalb von 100 ms.
- Steuerung zuerst: Bewegung, Sprung und Kamera müssen sich gut anfühlen, bevor Inhalte dazukommen.
- Kurze Nachsicht beim Springen (Coyote Time) und Eingabepuffer; die Kamera folgt weich mit leichtem Vorlauf in Laufrichtung.

## Leveldesign
- Neue Mechanik: zeigen (sicher) → üben (leicht) → prüfen (schwerer) → verbinden (mit Bekanntem).
- Wege lesbar machen: Licht, Farbe und Formen führen den Blick; Umwege belohnen.
- Schwierigkeit als Welle, nicht als Gerade: Anspannung und Erholung wechseln sich ab.

## Systeme und Balancing
- Werte (Schaden, Leben, Kosten) zentral in Datentabellen, nicht im Code verstreut.
- Jede Entscheidung des Spielers braucht einen erkennbaren Vor- und Nachteil.
- Spielstand speichern und laden mit Versionsnummer; nie Fortschritt verlieren.

## Technik
- Feste Simulationsschritte für die Physik, Darstellung unabhängig davon.
- Zustände als Zustandsautomat (Menü, Spiel, Pause, Ende) statt verstreuter Schalter.
- Ziel 60 Bilder pro Sekunde; Objekte wiederverwenden statt ständig neu anlegen.

## Checkliste
- Steuerung reagiert sofort und ist ohne Anleitung verständlich
- Jede neue Mechanik wird sicher eingeführt, bevor sie gefordert wird
- Pause, Neustart und Speichern funktionieren jederzeit
- Werte stehen zentral in Daten, nicht verstreut im Code
- Läuft flüssig (Ziel 60 Bilder pro Sekunde) auch auf schwächerer Hardware
- Untertitel bzw. Text für alle wichtigen Töne, Steuerung umbelegbar
