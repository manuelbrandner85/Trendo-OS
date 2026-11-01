# Medien – Testplan

Studio: Kleine Helfer & Musik (SYS-02) · Bereich: Arbeiten & Büro · Seite: `medien` · Stand: 0.9.32 (Stand 345fe84)

26 Tests sprechen dieses Programm an.

- G3: Fachstudio zeigt keine Reiter fremder Studios; G10: keine Schrift unter 12 pt auf der Seite
- G6: Dateifeld zeigt Vorschau der gewählten Datei (Name, Größe)
- G7: Ergebnis-Karte mit Kopieren und Verlauf früherer Ergebnisse; G12: Benachrichtigung erscheint
- G8: Strg+Q öffnet „Was möchtest du tun?“, findet Studios und Knöpfe der Seite und führt sie aus; G9/G13: F11 Fokusmodus
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
- T-0302: Fotos nach Monaten, Klick/Strg-Klick wählt, Album + Favoriten, Doppelklick groß mit ← →/Esc, Papierkorb nur nach Ja, Doppelte gefunden
- T-0306: Musik – Album öffnen und abspielen, Fortschritt/Pause, Titelende spielt den nächsten, Wiederholen, Springen per Leiste, Warteschlange, Liste anlegen und füllen
