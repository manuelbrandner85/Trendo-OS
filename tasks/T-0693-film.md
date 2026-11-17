# T-0693: D2 – „Filme machen“ erzeugt einen Film

**Ziel:** Die ganze Kette vom Drehbuch zum fertigen MP4 in einem Programm: Material je Einstellung, Rohschnitt automatisch, Film rendern über den FFmpeg-Weg des Videoschnitts, Übergabe an den Videoschnitt, KI schreibt ins offene Dokument.
**Branch:** claude/T-0693-film (von claude/T-0685-b1-integration)
**Grundlage:** Profi-Audit 16.11.2026, Teilbericht 2, STUDIO-FILM-01 (Urteil FEHLT: kein Film, nur Drehbuch, Figuren, Shotliste).
**Dateien/Bereich:** `src/Trendo.App/MainWindow.FilmStudio.cs`, neu `MainWindow.FilmSchnitt.cs`, neu `src/Trendo.Core/Film/FilmCut.cs`, Film-Adapter in `StudyPromptAdapter.cs`, Film-Zeilen in `CapabilityCatalog.cs`/`StudioKnowledge.cs` (+ erzeugte Matrix), Anschluss in `MainWindow.PromptAction.cs`, Videoschnitt-Kern nur nutzen (eine Fehlerbehebung in `VideoEdit.ExportArgs`), Tests, `reports/T-0693/`.
**Fertig, wenn:**
- [x] Jede Einstellung bekommt Material (Dateiauswahl, Ziehen-und-Ablegen), Dauer, Ein-/Ausstieg; ohne Material Storyboard-Bild (Bild-KI, falls eingerichtet) oder Texttafel
- [x] Rohschnitt als `VideoEdit`-Projekt: Titel, Zwischentitel je Akt/Szene, Abspann aus Figuren + Mitwirkenden, Überblenden, Musik, Sprecherton, Untertitel aus Dialogen (SRT oder eingebrannt)
- [x] „Film rendern“ (einzige goldene Hauptaktion) mit Fortschritt und Abbrechen; „Im Videoschnitt weiterbearbeiten“
- [x] Nachweis mit echtem FFmpeg (Kern- und UI-Test, ffprobe, Farbprüfung), Beleg unter `reports/T-0693/`
- [x] KI-Aktion schreibt ins offene Dokument (rückgängig); Katalogtext ehrlich
- [x] Oberfläche: links Szenen/Einstellungen, Mitte Vorschau + Storyboard-Streifen, rechts Eigenschaften; Leerzustand mit erstem Schritt
**Nicht tun:** `MainWindow.ProgramShell.cs`, `SimpleWords.cs` nicht anfassen; Videoschnitt nicht umbauen; kein Volltest (macht die Hauptsitzung).
**Am Ende:** Branch pushen, Bericht `tasks/T-0693-film.bericht.md`.
