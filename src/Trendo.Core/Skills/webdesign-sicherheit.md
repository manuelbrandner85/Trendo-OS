---
name: Web: Sicherheit von Web-Apps
description: Für Formulare, Anmeldung, Eingaben, Geheimnisse und den sicheren Umgang mit Daten in Web-Apps.
studios: CORE-07, X-03
stichworte: sicherheit, sicher, formular, anmeldung, login, passwort, eingabe, geheimnis, schlüssel, datenbank, angriff, xss, datenschutz
lizenz: Trendo OS (eigener Inhalt)
---

# Sicherheit von Web-Apps

## Eingaben und Ausgaben
- Jede Eingabe gilt als feindlich: auf dem Server prüfen, auch wenn der Browser schon geprüft hat.
- Ausgaben immer passend maskieren, nie als rohes HTML einsetzen. Datenbankabfragen nur mit Parametern.
- Links und Bildquellen aus Eingaben nur mit erlaubten Adressarten; `javascript:` und `data:` werden entfernt.

## Geheimnisse und Zugang
- Schlüssel und Passwörter stehen nie im Code oder in ausgelieferten Dateien, sondern in der Umgebung.
- Passwörter nur als starker, gesalzener Hash speichern. Sitzungen laufen ab, Cookies sind `HttpOnly`, `Secure` und `SameSite`.
- Nur so viele Rechte und Daten wie nötig; Fremddienste nur mit eigenem Konto und nach Zustimmung.

## Checkliste
- Eingaben werden auf dem Server geprüft
- Keine Geheimnisse im Code oder Export
- Keine gefährlichen Linkarten
- Ausgaben sind maskiert
- Nur nötige Daten werden erhoben
