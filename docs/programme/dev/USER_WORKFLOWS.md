# Webseite bauen – Arbeitsabläufe

Studio: Webseite bauen (CORE-07) · Bereich: Entwickeln · Seite: `dev` · Stand: 0.9.113 (Stand 1978f1a)

Die Abläufe unten sind die Oberflächentests, die das Programm über den echten Eingabeweg bedienen (Klicks, Tastatur).

- T-0718 UI Baukasten: „Veröffentlichen …“ fragt im echten Dialog; nach Ja liegt die Seite im Zielordner, Prüfung grün
- T-0718 UI Kinderprofil: keine Rückfrage, nichts veröffentlicht, Hinweis „Erwachsener“, im Protokoll abgelehnt
- T-0718 UI Ordner: echter Dialog nennt Wirkung, Ziel, Dateien, Größe; Abbrechen = nichts im Ziel; Ja = genau eine Ausführung, Rückmeldung mit Ort, Dateien und Prüfung
- T-0718 UI Testmodus: Server-Weg lädt nie ins Internet; Netlify ohne eigenes Konto: BLOCKIERT mit „Was tun“, keine Anfrage im Tor
- T-0718 UI ZIP: nur nach Ja im Tor; Paket enthält genau die gezeigten Dateien; Rückmeldung nennt „nichts hochgeladen“
- T-0718 UI eigener Server: echter Upload per HTTP an WebDAV auf 127.0.0.1 nach Ja – Dateien liegen byte-gleich auf dem Server, Nachprüfung, Anmeldung nur im geschützten Speicher; falsches Passwort → Meldung mit „Was tun“
