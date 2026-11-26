# Medien – Testergebnisse

Studio: Kleine Helfer & Musik (SYS-02) · Bereich: Arbeiten & Büro · Seite: `medien` · Stand: 0.9.110 (Stand 0eb9d52)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 29 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 2

| Test | Ergebnis |
|---|---|
| CORE-08 Videoschnitt Profi: Look Schwarzweiß, Ton 50 %, Übergang Wischen, Titel oben – als ein Schritt rückgängig; Logo auf die zweite Bildspur, Ecke unten links, entfernen | ✓ bestanden |
| G7: Ergebnis-Karte mit Kopieren und Verlauf früherer Ergebnisse; G12: Benachrichtigung erscheint | ✓ bestanden |
| S3-01/S3-03 Videoschnitt: Shotliste aus dem Drehbuch (CSV), Storyboard-Tafeln mit Shot-IDs in die Zeitleiste; nicht erzeugbare Tafel steht im Fehlprotokoll, der Rest wird übernommen | ✓ bestanden |
| S3-02 Multicam: zwei echte Kameradateien über den Ton synchronisieren (echtes FFmpeg), Wechsel 0:1, 3:2, 6:1 → drei Bild-Abschnitte mit richtigen Einstiegspunkten, Ton von Kamera 1 durchgehend; Unsinn bei den Wechseln wird gemeldet | ✓ bestanden |
| S3-02 Videoschnitt: Proxys erstellen (echtes FFmpeg) – Vorschau nutzt den Proxy, Export bleibt beim Original; ausschalten geht | ✓ bestanden |
| S3-09 Videoschnitt: Sprachaufnahme + Wortzeiten → Mundbilder (JSON, Moho), Prüfung OK, Vergleichsvorschau (echtes FFmpeg) in die Zeitleiste; Gesichtsvideo 150 ms versetzt → FAIL, nichts eingefügt; fehlende Aufnahme → FAIL | ✓ bestanden |
| S3-13 Videoschnitt: KI-Clip mit Zufallswert in die Zeitleiste, Herkunftsnachweis daneben (rückgängig machbar); ohne Modell nur Hinweis; liefert die KI nichts, wird nichts eingefügt | ✓ bestanden |
| SYS-02 (UI): Tabs vorhanden; Umbenennen mit Vorschau + Rückgängig; Erinnerung; Zwischenablage ohne Passwort; Einheiten | ✓ bestanden |
| SYS-02-P05: Zwischenablage max. 50, keine Passwörter/Schlüssel, löschbar | ✓ bestanden |
| SYS-02-P06/P10/P18: Drehen, Verkleinern, Schwärzen (Pixel überschrieben), Pipette – Original bleibt | ✓ bestanden |
| SYS-02-P07/P23: Aufnahme- und Audio-Befehle für ffmpeg | ✓ bestanden |
| SYS-02-P08/P09: Mediathek indiziert 2000 Dateien in < 5 s; Spielerwahl mpv → VLC → Standard | ✓ bestanden |
| SYS-02-P11: Umbenennen – Vorschau = Ergebnis, Rückgängig-Datei, Konflikte erkannt | ✓ bestanden |
| SYS-02-P12/P13: Doppelte per SHA-256 (nichts gelöscht), Speicherplatz-Summen stimmen | ✓ bestanden |
| SYS-02-P14: ZIP packen/entpacken, Zip-Slip wird blockiert (nichts entpackt) | ✓ bestanden |
| SYS-02-P15/P19: Erinnerung fällig ±2 s, nur einmal; Fokus endet automatisch | ✓ bestanden |
| SYS-02-P16: Kalender – ICS-Rundweg verlustfrei (Sonderzeichen, Zeilenumbruch) | ✓ bestanden |
| SYS-02-P17/P18: Einheiten, Währung nur mit eigenem Kurs, Farben HEX/RGB/HSL | ✓ bestanden |
| SYS-02-P21: Papierkorb lesen (Windows $I v2 + Linux), Leeren nur mit Bestätigung | ✓ bestanden |
| SYS-02-P22: Fotos ordnen – EXIF-Datum, Simulation ändert nichts, rückgängig | ✓ bestanden |
| SYS-02-P23 (T-0244 getrennt): Audio mit echtem ffmpeg umwandeln, abspielbar, Prüfsumme im Protokoll | ✓ bestanden |
| SYS-02-P24/P25: Wetter offline ohne Fehler (nur Auswertung), Warnung bei RAM > 90 % / < 5 GB frei | ✓ bestanden |
| SYS-02/SYS-10: Dateiregal filtert nach Art, neueste zuerst, ohne Office-Sperrdateien | ✓ bestanden |
| SYS-02: Notiz-Schnelltool öffnet Werkzeuge; Notiz wird gespeichert; Rechner rechnet bei Enter | ✓ bestanden |
| SYS-02: Notizen werden gespeichert, geändert und gelöscht | ✓ bestanden |
| SYS-02: Rechner lehnt Unsinn ab statt zu raten | nicht im Lauf |
| SYS-02: Rechner rechnet korrekt (Punkt vor Strich, Klammern, Komma, Prozent, Potenz) | nicht im Lauf |
| T-0302: Fotos nach Monaten, Klick/Strg-Klick wählt, Album + Favoriten, Doppelklick groß mit ← →/Esc, Papierkorb nur nach Ja, Doppelte gefunden | ✓ bestanden |
| T-0306: Musik – Album öffnen und abspielen, Fortschritt/Pause, Titelende spielt den nächsten, Wiederholen, Springen per Leiste, Warteschlange, Liste anlegen und füllen | ✓ bestanden |
| T-0731 (UI): Musik ohne mpv – MP3 spielt eingebaut bis zum Titelende; FLAC ohne Player meldet BLOCKIERT mit „Player einrichten“ | ✓ bestanden |
| T-0731 Gegenprüfung (UI): beschädigte MP3 im Musik-Player – Meldung „beschädigt … Titel ist beendet“ in der Leiste, Wiedergabe steht, Trendo läuft weiter | ✓ bestanden |
