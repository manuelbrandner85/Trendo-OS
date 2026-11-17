# T-0697: D4b – Comics & Storyboards und Podcast aufnehmen als echte Programme

**Ziel:** Aus den beiden Formular-Programmen (Audit STUDIO-COMIC-01, STUDIO-PODCAST-01, Design-Audit Abschnitt 3) echte Fachprogramme machen.
**Branch:** claude/T-0697-comic-podcast (Basis: claude/T-0685-b1-integration)
**Dateien/Bereich:** Comic- und Podcast-Abschnitte von `MainWindow.KreativStudios.cs` (ausgelagert in `MainWindow.ComicStudio.cs`, `MainWindow.PodcastStudio.cs`), neue Steuerelemente (`ComicControls.cs`), Core-Kits (`Kreativ/ComicBook.cs`, `Kreativ/PodcastEdit.cs`), Tests, `reports/T-0697/`.
**Fertig, wenn:**
- [ ] Comic: frei gesetzte und verformbare Bildfelder (Raster nur Vorschlag), Zeichnen im Bildfeld mit dem Mal-Kern (Ebenen je Seite), Bilder einsetzen/zuschneiden, Sprechblasen und Textkästen frei mit ziehbarem Dorn, Schrift, Zeilenumbruch, mehrere Seiten, PDF (Druck), PNG je Seite, CBZ; Storyboard-Ansicht mit Übergabe an „Filme machen“.
- [ ] Podcast: Aufnahme im Programm (vorhandener FFmpeg-Mikrofonweg, ohne Gerät ehrlicher Hinweis), Sprecherspuren mit Wellenform, Stellen löschen, Pausen kürzen, −16 LUFS, Intro/Outro, Kapitel, MP3 mit ID3 und Kapiteln, RSS nur als Datei. Nachweis mit echtem FFmpeg (Mikrofon durch Test-WAV ersetzt, so gekennzeichnet) → ffprobe: Dauer, Lautheit ±1 LU, Kapitel, Tags.
- [ ] Erster Schritt im Leerzustand, eine goldene Hauptaktion, Tastenkürzel, Rechtsklick, Ziehen und Ablegen; im Bild geprüft.
- [ ] Gezielte Tests aller berührten Testklassen grün (kein Volltest – macht die Hauptsitzung).
**Nicht tun:** `MainWindow.ProgramShell.cs`, Animation/Logos, `SimpleWords.cs`, Audio-Programm nur über öffentliche Methoden; kein Push nach main/Integrationszweig, kein Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0697-comic-podcast.bericht.md`.
