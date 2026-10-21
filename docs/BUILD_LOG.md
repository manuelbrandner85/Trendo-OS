# Bau-Protokoll (laufend, neueste oben) – Zeiten MESZ

## 21.10.2026
- 20:10 Branch `w0/setup` (Welle 0 + 0b), Release v0.7.1-preview1 gebaut (Setup SHA ea396d58…). Nächster Schritt: Test auf dem PC.
- 20:4x PC-Test: Auto-Update von 0.7.1 auf 0.7.2 über GitHub Releases erfolgreich (reports/w0/update-test.txt). Systemcheck: LibreOffice wird nicht mehr per --version gestartet (das hing), Prüfungen laufen parallel, ein hängender Prozess wird bei Zeitüberschreitung beendet. Der „Geister-Ring“ auf dem Startbildschirm gehört zum Hintergrundbild „bergsee-nacht“ und ist kein Fehler. v0.7.3.
- 20:3x Systemcheck blockierte den UI-Thread (GetResult) und hängte die PC-Screenshots, jetzt asynchron. v0.7.2 veröffentlicht. Hinweis: Start-Process -Wait wartet auf Kindprozesse, daher künftig WaitForExit.
- 20:00 PC: zwei hängende winget-Prozesse von 18:45 (LibreOffice/Tesseract, von Trendo gestartet) sperrten den Programmordner, deshalb schlug das Update fehl (Setup exit 1). LibreOffice 26.x und Tesseract 5.4 waren schon installiert, also beide winget-Prozesse beendet. Fix: Arbeitsverzeichnis gestarteter Programme liegt nicht mehr im Programmordner.
- 19:5x Welle 0b: Designs Dunkel/Hell/Kontrast, Akzentfarbe, Hintergrund-Galerie (4 Bilder, per Lanczos und Schärfen auf 2560×1440 hochskaliert, kein KI-Upscaler verfügbar), Tageszeit-Wechsel, Animationen und „Bewegung reduzieren“, Hilfe „?“ mit Hinweisen beim ersten Öffnen, Startbildschirm anpassen, einfache Sprache (docs/SPRACHE.md und Test).
- 19:3x Welle 0: Assistent, Basispaket (SumatraPDF, mpv, 7-Zip, DIN-5008-Vorlagen), Studio-Pakete, KI nach Hardware, Sicherung und Wiederherstellung, Velopack-Updates über GitHub, Systemcheck mit Reparieren.
- 19:40 PR #79 gemergt: ISO Build 6 (Autologin PASS), BUILD_PLAN, Fortschritt-Seite, Issues #14–#78.
