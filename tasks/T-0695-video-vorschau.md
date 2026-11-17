# T-0695: Videoschnitt – Vorschau zeigt, was exportiert wird (D3)

**Ziel:** Die Vorschau im Videoschnitt zeigt am Abspielkopf und beim Abspielen das fertige Bild – mit derselben Pipeline-Beschreibung wie der Export (Farbe/LUT, Einblendungen, Übergänge, Keyframes, Effekte, Geschwindigkeit) –, nachgewiesen mit echtem FFmpeg.
**Branch:** claude/T-0695-video-vorschau (von `claude/T-0685-b1-integration`)
**Dateien/Bereich:** `src/Trendo.App/MainWindow.VideoPreview.cs`, `MainWindow.VideoSchnitt.cs`, `EditTimeline.cs`, Vorschau-Code in `src/Trendo.Core/Media/` (neue Dateien bevorzugt), Tests, `reports/T-0695/`.
**Grundlage:** Audit-Teilbericht 2 (`audit-2-kreativ-medien.md`), CORE-08 Videoschnitt, Kriterium 7 „Vorschau“: zeigt nur das rohe Stück.
**Fertig, wenn:**
- [ ] Standbild am Abspielkopf aus demselben Filtergraphen wie der Export, im Hintergrund, abbrechbar, zwischengespeichert; Wartezeit auf einem 1080p-Projekt gemessen (Ziel < 300 ms)
- [ ] Abspielen mit Effekten in reduzierter Auflösung flüssig oder ehrlich „Vorschau wird berechnet“
- [ ] Nachweis mit echtem FFmpeg: Farbkorrektur, Titel, Überblendung, Keyframe – Vorschau gegen exportiertes MP4 an mindestens 4 Zeitpunkten, Schwelle begründet, Beleg unter `reports/T-0695/` (Bilder, Rohlog, SHA-256)
- [ ] Bedienung: Abspielkopf ziehen zeigt das fertige Bild, Leertaste/J/K/L, Pfeile bildweise, Vollbild (F), Dateien auf die Zeitleiste ziehen, Rechtsklick; genau eine goldene Hauptaktion „Video exportieren“
- [ ] Im Bild nachgeprüft (Render-Werkzeug, eigener `TRENDO_RENDER_ROOT`)
- [ ] betroffene Testklassen grün (kein Volltest – macht die Hauptsitzung)
**Nicht tun:** `MainWindow.ProgramShell.cs`, `MainWindow.FilmStudio.cs`, `MainWindow.FilmSchnitt.cs`, `src/Trendo.Core/Film/*`, `SimpleWords.cs`; T-0693-Fix (harter Schnitt + Überblendung) nicht zurückdrehen; kein Merge, kein Release, kein Push auf main/Integrationszweig.
**Am Ende:** Branch pushen, Bericht `tasks/T-0695-video-vorschau.bericht.md`.
