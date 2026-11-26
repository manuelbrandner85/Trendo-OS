# T-0780: Zwei neue Studios Wissen & Lernen – „Referate & Hausarbeiten“ und „Zeitleisten“ (Etappe 0.9.113)

**Ziel:** Zwei neue Studios der Kategorie Wissen & Lernen mit echtem Fachkern in der Programm-Hülle: Referate als Kette Thema → Gliederung → Text → Literatur → Folien (Ausgabe DOCX/ODT/PDF, Folien PPTX/ODP), Zeitleisten als Editor mit Ereignissen und Zeiträumen in Spuren (auch v. Chr. und ungefähr), Zoom und Verschieben, Bilder, Ausgabe SVG/PNG/PDF, Austausch CSV/JSON.
**Freigabe:** Nutzer, 26.11.2026 (Etappe 0.9.113 „Wissen & Lernen“).
**Branch:** claude/T-0780-referate-zeitleisten (Basis main 0.9.112 = 184fcbbfc)
**Grundlage:** `docs/audit/2026-11-26-neubewertung/fehlende-studios.md` (Wissen & Lernen, Rang 2 und 6), Paket-Brief und Studio-Brief der Hauptsitzung.
**Dateien/Bereich:** neu `src/Trendo.Core/Wissen/`, `src/Trendo.Core/Office/SlideOdp.cs`, `src/Trendo.Core/PromptAction/WissenPromptAdapters.cs`, `src/Trendo.App/MainWindow.Referate.cs`, `MainWindow.Zeitleisten.cs`, `TimelineCanvas.cs`; Einträge am Ende von StudioCatalog, StudioIcons, CapabilityCatalog, StudioProfiles, StudioTeams, StudioKnowledge, PromptChips, IntelligencePromptModel, W6, `tools/studio_bilder.py`, Bilder `Assets/studios/`; Tests, Belege `reports/T-0780/`, Anforderungen `docs/nachbau/referate/`, `docs/nachbau/zeitleisten/`.
**Fertig, wenn:**
- [x] Vorbilder recherchiert, Anforderungen mit Prüfung je Zeile (Clean-Room).
- [x] Beide Studios in der Hülle (AtomicFile/Sicherung/Wiederherstellung der Hülle, Rückgängig, Einfach/Profi, Rechtsklick, Ziehen).
- [x] Klick bis Datei getestet, Rot-Grün belegt.
- [x] Fremdformate in LibreOffice (DOCX/ODT/PPTX/ODP/CSV) und Inkscape (SVG) geöffnet, Rohlog + SHA-256.
- [x] KI-Fachaktion je Studio (Vorschau → „Ja, behalten“ → ein Rückgängig), Echtlauf mit lokaler KI.
- [x] requirements.yaml-Vorschlag im Bericht (Datei gesperrt).
**Nicht tun:** requirements.yaml ändern, Claude-Echtlauf, Volltest, Merge, Release, nach main pushen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0780-referate-zeitleisten.bericht.md`.
