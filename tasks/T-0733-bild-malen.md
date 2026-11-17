# T-0733: Bildbearbeitung + Künstlerstudio (Profi-Audit Rang 3 und 8)

**Ziel:** Die Bildbearbeitung (`bild`, CORE-08) speichert und öffnet WebP, öffnet TIFF und hat Werkzeug-Tasten wie Profiprogramme inkl. Zoom (Z); das Künstlerstudio (STUDIO-ART-01) bekommt eine Auswahl (Rechteck/Lasso), deren Inhalt sich verschieben und skalieren lässt – rückgängig machbar.
**Branch:** claude/T-0733-bild-malen
**Dateien/Bereich:** `src/Trendo.App/MainWindow.Bildbearbeitung.cs`, `ImageCanvas.cs`, `ImageRender.cs`, `MainWindow.Kuenstlerstudio.cs`, `ArtControls.cs`, neu `src/Trendo.Core/Kreativ/KuenstlerAuswahl.cs`; Tests `tests/Trendo.App.Tests/T0733BildMalenUiTests.cs`, `tests/Trendo.Core.Tests/KuenstlerAuswahlTests.cs`; Belege `reports/T-0733/`.
**Fertig, wenn:**
- [x] WebP-Export über SkiaSharp als eigener Export „WebP (klein, fürs Web)“; WebP-Import als Ebene
- [x] TIFF-Import: SkiaSharp kann es nicht (geprüft) → ehrlich über das eingerichtete FFmpeg, sonst klare Meldung
- [x] Werkzeug-Tasten V B E M T Z (und die übrigen) im Menü „Werkzeuge“ und im Tooltip, `ProgramMenu.Conflicts` leer; Z = neues Zoom-Werkzeug
- [x] Künstlerstudio: Auswahl Rechteck/Lasso (Taste M), Inhalt mit der Maus verschieben, an den Ecken skalieren, Skalieren per Zahl, Inhalt löschen, Rückgängig je Schritt
- [x] Echtbeleg: WebP mit ffprobe und Pillow gelesen, Rohlog + SHA-256 unter `reports/T-0733/`
- [x] gezielte Testklassen grün (kein Volltest – macht die Hauptsitzung)
**Nicht tun:** keine gemeinsamen Dateien (MenuBar, ProgramShell, StudioCatalog, requirements.yaml, installer) anfassen; kein Volltest, kein Merge, kein Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0733-bild-malen.bericht.md`.
