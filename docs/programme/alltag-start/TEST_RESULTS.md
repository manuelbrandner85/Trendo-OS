# Übersicht – Testergebnisse

Studio: Kleine Helfer & Musik (SYS-02) · Bereich: Arbeiten & Büro · Seite: `alltag-start` · Stand: 0.9.36 (Stand 862c4f1)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 28 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 2

| Test | Ergebnis |
|---|---|
| Fenster-Verlauf: merkt Seitenwechsel (höchstens 30), nicht beim Neuzeichnen oder Zurückgehen | ✓ bestanden |
| Fenster: Alt+← geht im aktiven Fenster zurück; mehrere Schritte nacheinander | ✓ bestanden |
| Fenster: Klick auf eine Kategorie im Fenster bleibt im selben Fenster; ohne Klick eigenes Fenster; offene Seite wird wiederverwendet | ✓ bestanden |
| Fenster: Zurück-Knopf führt im selben Fenster zur vorigen Seite; ohne vorige Seite ausgegraut; kein neues Fenster | ✓ bestanden |
| Fenster: maximiert füllt den ganzen Bildschirm – über Kopfleiste und Taskleiste, ohne Rand; Wiederherstellen bringt die Leisten zurück | ✓ bestanden |
| N11: Rechner – Tasten, wissenschaftlich, Programmierer, Grafik, Datum, Verlauf | ✓ bestanden |
| N18: Zettel anlegen, Farbe wählen, auf der Startseite anheften und lösen | ✓ bestanden |
| N20: Wecker stellen und klingeln (Benachrichtigung mit Schlummern), Timer läuft ab, Stoppuhr-Runden, Weltuhr | ✓ bestanden |
| N5: Notizbuch – Abschnitt und Seite anlegen, schreiben (speichert), Schlagworte, Suche, der KI als Wissen geben | ✓ bestanden |
| N9: Editor öffnet ANSI/CRLF, sucht, ersetzt, speichert in gleicher Kodierung; Kodierung umstellbar | ✓ bestanden |
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
