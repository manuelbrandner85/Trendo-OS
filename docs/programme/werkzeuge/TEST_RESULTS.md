# Werkzeuge: Notizen, Rechner, Wecker – Testergebnisse

Studio: Kleine Helfer & Musik (SYS-02) · Bereich: Arbeiten & Büro · Seite: `werkzeuge` · Stand: 0.9.36 (Stand 9ed3c01)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 29 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 2

| Test | Ergebnis |
|---|---|
| Fenster: Einstellung übernehmen (Design-Knopf per Mausklick) schließt keine Fenster – alle kommen mit Lage und Zustand zurück | ✓ bestanden |
| Fenster: Größe an Ecke und Rand per Maus ziehen; an linken/rechten Rand ziehen rastet auf halbe Fläche ein | ✓ bestanden |
| Fenster: Startmenü öffnet Dokumente mit Taskleisten-Eintrag, Startbildschirm bleibt dahinter | ✓ bestanden |
| Fenster: Ziehen an der Titelleiste verschiebt; Klick ins hintere Fenster holt es nach vorn; Reiter teilen ein Fenster | ✓ bestanden |
| Fenster: maximiert und daneben geöffnet bleiben im AutomationPeer-Baum, mit Namen und endlichem Rechteck | ✓ bestanden |
| Fenster: wechseln per Taskleiste, aktives hervorgehoben, maximieren, schließen | ✓ bestanden |
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
| SYS-13 (UI): Rückfall-Sicherung und vorige Version; Protokoll-Prüfung | ✓ bestanden |
| SYS-13-P13: Jede Seite gehört zu einem Modul aus requirements.yaml | ✓ bestanden |
| SYS-13-P14: Fehler in einer Seite zeigt freundliche Fehlerseite statt Absturz | ✓ bestanden |
| SYS-13-P15/P16: Start < 3 s, Seitenwechsel im Median < 300 ms, 100 Wechsel ohne > 50 MB Zuwachs | ✓ bestanden |
| Start: das Hauptfenster ist sichtbar und kommt aus dem minimierten Zustand zurück | ✓ bestanden |
