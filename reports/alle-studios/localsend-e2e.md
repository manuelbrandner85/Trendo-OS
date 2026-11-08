# Ans Handy senden (LocalSend-Protokoll) – 08.11.2026

Alle Studios, Runde 9, SYS-06 „Dateien teilen“.

**Grundlage:** das offene LocalSend-Protokoll v2 (Beschreibung: github.com/localsend/protocol, Stand v2.2). Die App LocalSend (Apache-2.0) läuft auf dem Handy. **In Trendo steckt eigener C#-Code nach der Beschreibung, nur die Sendeseite**, ohne neues Paket und ohne Programm auf dem PC.

## Was die Nutzerin oder der Nutzer sieht
In „Teilen im Heimnetz“ gibt es die Karte **„Ans Handy senden (LocalSend)“**:
1. Datei(en) angeben, **„Handys suchen“** drücken.
2. Gefundene Geräte erscheinen mit Name und Modell, dort **„Senden“** drücken.
3. Das Handy fragt selbst, ob es annimmt. Verlangt es eine PIN, kommt sie ins PIN-Feld.

Trendo meldet verständlich: abgelehnt, PIN nötig, Handy beschäftigt, Prüfsumme falsch.

## Sicherheit
- **Nur eigenes Netz:** Antworten und Ziele außerhalb privater Adressen werden ignoriert oder abgelehnt.
- **Kein Server:** Trendo öffnet dafür keinen Server, sendet nur.
- **HTTPS:** LocalSend nutzt selbst signierte Zertifikate. Trendo akzeptiert nur das Zertifikat, dessen SHA-256 zum im Netz angekündigten Fingerabdruck passt.
- **Inhalt:** Jede Datei geht mit SHA-256; nur Dateinamen ohne Pfad werden übertragen.
- **Abbruch:** Bei einem Fehler wird die Sitzung am Handy mit `cancel` freigegeben.

## Umsetzung
- **`LocalSend` (Kern, ohne Netz, QA-019):** Ankündigung, Geräte lesen, `prepare-upload`, Adressen, Fehlertexte, Zertifikatsprüfung.
- **App:** UDP-Multicast auf 224.0.0.167:53317, HTTP(S) über `HttpClient`. Weil Trendo keinen Server öffnet, antworten Geräte über den im Protokoll vorgesehenen UDP-Rückweg.

## Tests
- **`LocalSendTests`** (14 Tests, grün): Ankündigung, Geräte lesen, nur eigenes Netz (9 Adressen), ignorierte Nachrichten, `prepare-upload`, Zertifikat, Fehlertexte.
- **`LocalSend_Ui`** (2 Tests, grün), über **echte Verbindungen** gegen einen Empfänger, der die Protokollbeschreibung umsetzt:
  - HTTP: Dateien kommen Byte für Byte an, Token und SHA-256 werden geprüft.
  - Am Empfänger abgewählte Datei entfällt.
  - Ablehnung (403), PIN (401, danach mit PIN erfolgreich), Prüfsummenfehler (422, mit `cancel`).
  - UDP: Die Antwort eines Geräts erscheint in der Liste; ohne Antwort kommt ein klarer Hinweis.

## Nicht geprüft (NOT RUN)
- **Echtes Handy mit der LocalSend-App.** Der Empfänger im Test ist nach der Beschreibung gebaut, aber nicht die App selbst. Die Prüfung mit einem echten Handy steht aus.
- **HTTPS-Verbindung gegen ein echtes selbst signiertes Zertifikat** (die Fingerabdruck-Prüfung selbst ist getestet).
- **Multicast über ein echtes WLAN** (getestet wurde der UDP-Rückweg auf diesem PC).
- **Empfangen auf dem PC.** Bisher nur Senden; Empfangen bräuchte einen Server und eine eigene Abwägung.
