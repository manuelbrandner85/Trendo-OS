# Werkzeuge: Notizen, Rechner, Wecker – Testplan

Studio: Kleine Helfer & Musik (SYS-02) · Bereich: Arbeiten & Büro · Seite: `werkzeuge` · Stand: 0.9.36 (Stand 52e27a5)

31 Tests sprechen dieses Programm an.

- Fenster: Einstellung übernehmen (Design-Knopf per Mausklick) schließt keine Fenster – alle kommen mit Lage und Zustand zurück
- Fenster: Größe an Ecke und Rand per Maus ziehen; an linken/rechten Rand ziehen rastet auf halbe Fläche ein
- Fenster: Startmenü öffnet Dokumente mit Taskleisten-Eintrag, Startbildschirm bleibt dahinter
- Fenster: Ziehen an der Titelleiste verschiebt; Klick ins hintere Fenster holt es nach vorn; Reiter teilen ein Fenster
- Fenster: maximiert und daneben geöffnet bleiben im AutomationPeer-Baum, mit Namen und endlichem Rechteck
- Fenster: wechseln per Taskleiste, aktives hervorgehoben, maximieren, schließen
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
- SYS-13 (UI): Rückfall-Sicherung und vorige Version; Protokoll-Prüfung
- SYS-13-P13: Jede Seite gehört zu einem Modul aus requirements.yaml
- SYS-13-P14: Fehler in einer Seite zeigt freundliche Fehlerseite statt Absturz
- SYS-13-P15/P16: Start < 3 s, Seitenwechsel im Median < 300 ms, 100 Wechsel ohne > 50 MB Zuwachs
- Start: das Hauptfenster ist sichtbar und kommt aus dem minimierten Zustand zurück
