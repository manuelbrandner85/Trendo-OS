# TV & Live – Testergebnisse

Studio: TV & Live (STUDIO-TV-01) · Bereich: Kreativ & Medien · Seite: `tv` · Stand: 0.9.110 (Stand 0eb9d52)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 15 · Fehlgeschlagen 0 · Übersprungen 1 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| STUDIO-TV-01 Legale Quellen: drei Arten, jede Kategorie hat Anbieter, nur https, keine doppelten Kennungen, keine Piraterie-Seiten | ✓ bestanden |
| STUDIO-TV-01 Mediathek-Antwort lesen: Titel, Sender, Dauer, Datum, HD-Adresse; ohne https übergangen; Fehler verständlich | ✓ bestanden |
| STUDIO-TV-01 Mediathek-Suche: Suchtext vor Kategorie, Filme ab 60 Minuten, Kinder nur KiKA, Sender als zweite Bedingung | ✓ bestanden |
| STUDIO-TV-01 Mediatheken & Streaming: ohne Senderliste erreichbar, Arten und Kategorien, Mediathek suchen und in Trendo abspielen, Abo-Kachel öffnet nur die offizielle Seite | ✓ bestanden |
| STUDIO-TV-01 Video-Plattformen: ohne Senderliste erreichbar, Kategorien wie bei YouTube, PeerTube in Trendo abspielen, Dailymotion auf der Original-Seite, YouTube-Suche und -Link öffnen den offiziellen Player | ✓ bestanden |
| T-0300: Raster mit Jetzt-Linie, Tage und 20:15, Sparten-Filter, Suche springt zur Sendung, Aufnahme meldet Überschneidung, Erinnerung zeigt Hinweis | ✓ bestanden |
| T-0746 ECHT TV & Live: lokaler HLS-Teststream (127.0.0.1) → Senderliste laden, abspielen (mpv), aufnehmen, ffprobe liest die Aufnahme (nur mit TRENDO_ECHT_STREAM) | übersprungen |
| TV & Live: Filme und Serien kategorisiert wählbar (Xtream) – Kategorie, Suche, Film abspielen, Serie → Staffel → Folge | ✓ bestanden |
| TV & Live: KI-Assistent („Behalte nur deutsche Sender“, „Zeig Netflix-Serien“) und Anbieter-/Sprachwahl bei Filmen und Serien | ✓ bestanden |
| TV & Live: Programmführer zeigt Sendungen, Aufnahme planen und starten, Mehrfachansicht, Quelle entfernen | ✓ bestanden |
| TV & Live: Quelle mit Programmführer laden, Sender schauen und umschalten, Favorit, gesperrte Gruppe nur mit PIN | ✓ bestanden |
| TV & Live: Xtream-Zugang abgelehnt → klare Meldung ohne Passwort; Server nicht erreichbar → verständlich | ✓ bestanden |
| TV Highend: Startseite, Film-Infos/Trailer, Weiterschauen, nächste Folge, Kinder-Profil, Bild im Bild, Bild & Ton, Regeln, Fernbedienung, Qualität | ✓ bestanden |
| TV-Vollbild: kleines Fenster „Verkleinern“ beim Start und bei Mausbewegung, ein Klick verkleinert | ✓ bestanden |
| Updates: nach dem Neustart kommen die Fenster zurück und „Was ist neu?“ erscheint einmal | ✓ bestanden |
| streamlink-Oberfläche: Mediathek-Seite zeigt Das Erste und arte; Klick löst den Stream auf und wechselt zum Player | ✓ bestanden |
