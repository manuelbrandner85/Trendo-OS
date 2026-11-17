# T-0732: CORE-08 Audio bearbeiten + Medien bearbeiten

**Ziel:** Audit-Restliste Rang 2 und 7 schließen – Aufnahme bis „Stopp“ mit Pegel, Leertaste spielt ab/hält an, Effekte mit einstellbaren Werten (`ton`); Stapelverarbeitung mit Fortschritt je Datei, Abbruch und Ausgabeordner (`kreativ-medien`).
**Branch:** claude/T-0732-ton-medien
**Dateien/Bereich:** `src/Trendo.App/MainWindow.AudioEditor.cs`, neu `MainWindow.AudioRecord.cs`, `WaveView.cs`, `MainWindow.W4Media.cs`, neu `MainWindow.MediaBatch.cs`; `src/Trendo.Core/MediaStudio.cs`, neu `MediaBatch.cs`, neu `Media/MicRecorder.cs`; Tests und Beleg `reports/T-0732/`.
**Fertig, wenn:**
- [x] Aufnahme läuft, bis „Stopp“ gedrückt wird; Zeit und Pegel sind sichtbar; die Aufnahme landet an der Marke (Test mit echtem FFmpeg)
- [x] Leertaste startet und hält die Wiedergabe an (nicht beim Tippen in Feldern), Eintrag „Wiedergabe“ in der Menüleiste
- [x] Lauter/Leiser, Normalisieren, Blenden, Echo, Tiefen/Höhen weg, Tempo, Rauschen, Kompressor, Stille mit einstellbaren Werten
- [x] Zehn Dateien in einem Lauf umwandeln, Fortschritt je Datei, Abbruch löscht die halbe Datei, Ergebnisse im Ausgabeordner (echtes FFmpeg)
- [x] Dateien wählen (Mehrfachauswahl) und aus dem Explorer auf die Werkbank ziehen
- [x] Echtbeleg unter `reports/T-0732/` mit Rohlog und SHA-256
- [x] betroffene Testklassen grün (kein Volltest – macht die Hauptsitzung)
**Nicht tun:** keine gemeinsamen Dateien (MenuBar, ProgramShell, StudioCatalog, requirements.yaml, installer/neuerungen), kein Mikrofon des Nutzers aufnehmen, nichts installieren.
**Am Ende:** Branch pushen, Bericht `tasks/T-0732-ton-medien.bericht.md`.
