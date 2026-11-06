# Medien – Testergebnisse

Studio: Kleine Helfer & Musik (SYS-02) · Bereich: Arbeiten & Büro · Seite: `medien` · Stand: 0.9.36 (Stand 5b38700)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 24 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 2

| Test | Ergebnis |
|---|---|
| G3: Fachstudio zeigt keine Reiter fremder Studios; G10: keine Schrift unter 12 pt auf der Seite | ✓ bestanden |
| G6: Dateifeld zeigt Vorschau der gewählten Datei (Name, Größe) | ✓ bestanden |
| G7: Ergebnis-Karte mit Kopieren und Verlauf früherer Ergebnisse; G12: Benachrichtigung erscheint | ✓ bestanden |
| G8: Strg+Q öffnet „Was möchtest du tun?“, findet Studios und Knöpfe der Seite und führt sie aus; G9/G13: F11 Fokusmodus | ✓ bestanden |
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
