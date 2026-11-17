# T-0696: D4a – Animation & Motion und Logos & Marken: Bühne zum Anfassen statt Formular

**Ziel:** Beide Programme bekommen einen gemeinsamen kleinen Vektor-Editor (Formen, Bézier-Pfade, Text, Bilder/SVG direkt auf der Fläche); Logos werden bearbeitbare Vektor-Dokumente mit echtem Export-Paket, Animation bekommt Bühne + Zeitleiste mit ziehbaren Schlüsselbildern und echt gerendertes Video.
**Branch:** claude/T-0696-animation-logos (Basis: claude/T-0685-b1-integration)
**Grundlage:** Audit 2026-11-16, Teilbericht 2 (STUDIO-ANIM-01, STUDIO-LOGO-01), Teilbericht 7 Abschnitt 3.
**Dateien/Bereich:** `MainWindow.KreativStudios.cs` (nur Logo-Abschnitt), `MainWindow.KreativStudios2.cs` (nur Animations-Abschnitt), neu `VectorEditor.cs`, `VectorSkia.cs`, `KeyTimeline.cs`, `MainWindow.KreativVektor.cs`, `Trendo.Core/Kreativ/VectorKit.cs`; Tests; `reports/T-0696/`.
**Fertig, wenn:**
- [x] Gemeinsamer Vektor-Editor (Formen, Bézier-Pfade, Text, Griffe, Ausrichten, Gruppen, Reihenfolge, Füllung/Kontur/Verlauf, Raster, Rückgängig, Kürzel, Rechtsklick, Ziehen-und-Ablegen)
- [x] Logos: Formular = „Vorschlag erzeugen“, Ergebnis bearbeitbar; SVG, PNG 512/1024/2048, PDF, ICO, Paket hell/dunkel/einfarbig/negativ, Markenpaket aus dem echten Logo; SVG in Chrome gerendert und Pixel verglichen
- [x] Animation: Bühne mit demselben Editor, Zeitleiste mit ziehbaren Schlüsseln, Kurven, Abspielen, animiertes SVG, MP4 und GIF mit echtem FFmpeg geprüft
- [x] Erster Schritt im Leerzustand, eine goldene Hauptaktion, im Bild nachgeprüft
**Nicht tun:** `MainWindow.ProgramShell.cs`, Comics/Podcast, `SimpleWords.cs`, Bildbearbeitung selbst; kein Volltest.
**Am Ende:** Branch pushen, Bericht `tasks/T-0696-animation-logos.bericht.md`.
