# Übersicht – Testplan

Studio: Kleine Helfer & Musik (SYS-02) · Bereich: Arbeiten & Büro · Seite: `alltag-start` · Stand: 0.9.36 (Stand dcf0568)

30 Tests sprechen dieses Programm an.

- Fenster-Verlauf: merkt Seitenwechsel (höchstens 30), nicht beim Neuzeichnen oder Zurückgehen
- Fenster: Alt+← geht im aktiven Fenster zurück; mehrere Schritte nacheinander
- Fenster: Klick auf eine Kategorie im Fenster bleibt im selben Fenster; ohne Klick eigenes Fenster; offene Seite wird wiederverwendet
- Fenster: Zurück-Knopf führt im selben Fenster zur vorigen Seite; ohne vorige Seite ausgegraut; kein neues Fenster
- Fenster: maximiert füllt den ganzen Bildschirm – über Kopfleiste und Taskleiste, ohne Rand; Wiederherstellen bringt die Leisten zurück
- N11: Rechner – Tasten, wissenschaftlich, Programmierer, Grafik, Datum, Verlauf
- N18: Zettel anlegen, Farbe wählen, auf der Startseite anheften und lösen
- N20: Wecker stellen und klingeln (Benachrichtigung mit Schlummern), Timer läuft ab, Stoppuhr-Runden, Weltuhr
- N5: Notizbuch – Abschnitt und Seite anlegen, schreiben (speichert), Schlagworte, Suche, der KI als Wissen geben
- N9: Editor öffnet ANSI/CRLF, sucht, ersetzt, speichert in gleicher Kodierung; Kodierung umstellbar
- SYS-02 (UI): Tabs vorhanden; Umbenennen mit Vorschau + Rückgängig; Erinnerung; Zwischenablage ohne Passwort; Einheiten
- SYS-02-P05: Zwischenablage max. 50, keine Passwörter/Schlüssel, löschbar
- SYS-02-P06/P10/P18: Drehen, Verkleinern, Schwärzen (Pixel überschrieben), Pipette – Original bleibt
- SYS-02-P07/P23: Aufnahme- und Audio-Befehle für ffmpeg
- SYS-02-P08/P09: Mediathek indiziert 2000 Dateien in < 5 s; Spielerwahl mpv → VLC → Standard
- SYS-02-P11: Umbenennen – Vorschau = Ergebnis, Rückgängig-Datei, Konflikte erkannt
- SYS-02-P12/P13: Doppelte per SHA-256 (nichts gelöscht), Speicherplatz-Summen stimmen
- SYS-02-P14: ZIP packen/entpacken, Zip-Slip wird blockiert (nichts entpackt)
- SYS-02-P15/P19: Erinnerung fällig ±2 s, nur einmal; Fokus endet automatisch
- SYS-02-P16: Kalender – ICS-Rundweg verlustfrei (Sonderzeichen, Zeilenumbruch)
- SYS-02-P17/P18: Einheiten, Währung nur mit eigenem Kurs, Farben HEX/RGB/HSL
- SYS-02-P21: Papierkorb lesen (Windows $I v2 + Linux), Leeren nur mit Bestätigung
- SYS-02-P22: Fotos ordnen – EXIF-Datum, Simulation ändert nichts, rückgängig
- SYS-02-P23 (T-0244 getrennt): Audio mit echtem ffmpeg umwandeln, abspielbar, Prüfsumme im Protokoll
- SYS-02-P24/P25: Wetter offline ohne Fehler (nur Auswertung), Warnung bei RAM > 90 % / < 5 GB frei
- SYS-02/SYS-10: Dateiregal filtert nach Art, neueste zuerst, ohne Office-Sperrdateien
- SYS-02: Notiz-Schnelltool öffnet Werkzeuge; Notiz wird gespeichert; Rechner rechnet bei Enter
- SYS-02: Notizen werden gespeichert, geändert und gelöscht
- SYS-02: Rechner lehnt Unsinn ab statt zu raten
- SYS-02: Rechner rechnet korrekt (Punkt vor Strich, Klammern, Komma, Prozent, Potenz)
