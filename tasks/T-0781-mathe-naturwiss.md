# T-0781: Zwei neue Studios Wissen & Lernen – „Mathe-Helfer“ und „Naturwissenschaft“

**Ziel:** Zwei echte Fach-Arbeitsplätze in der Programm-Hülle: ein eigener, geprüfter Rechenkern mit Lösungsweg (Mathe-Helfer) und Periodensystem, Molmasse, Reaktionen ausgleichen, Physik-Formeln mit Einheiten (Naturwissenschaft).
**Freigabe:** Nutzer, 26.11.2026 (Etappe 0.9.113, Wissen & Lernen).
**Branch:** claude/T-0781-mathe-naturwiss (Basis main 0.9.112 = 184fcbbfc)
**Grundlage:** `docs/audit/2026-11-26-neubewertung/fehlende-studios.md` (Wissen & Lernen: Mathe-Helfer Rang 3, Naturwissenschaft Rang 5), Paket-Brief und Studio-Brief 0.9.113.
**Dateien/Bereich:** neu `src/Trendo.Core/Wissen/*`, `src/Trendo.Core/PromptAction/WissenPromptAdapters.cs`, `src/Trendo.App/MainWindow.MatheHelfer.cs`, `src/Trendo.App/MainWindow.Naturwissenschaft.cs`; klein ergänzt: StudioCatalog, StudioIcons, CapabilityCatalog, PromptActions, StudioProfiles, StudioTeams, PromptChips, `tools/studio_bilder.py`, Bilder, `GraphView.cs` (Farben), `ProgramKiAction.Hinweis`, `MainWindow.W6.cs` (Init); Tests, `docs/nachbau/mathe-helfer/`, `docs/nachbau/naturwissenschaft/`, `reports/T-0781/`.
**Fertig, wenn:**
- [x] Rechenkern: Terme vereinfachen/ausmultiplizieren/faktorisieren, lineare und quadratische Gleichungen, Gleichungssysteme, Bruchrechnen, Ableitungen, einfache Integrale – je mit Lösungsweg und Probe, exakt mit Brüchen.
- [x] Naturwissenschaft: Periodensystem mit eigenen, abgeglichenen Daten; Molmasse; Reaktionen ausgleichen; Formelsammlung mit Einheiten und Dimensionsprüfung; Fehlerfortpflanzung aus SPEC-05 genutzt.
- [x] KI-Fachaktion Textaufgabe → Rechnung, Vorschau, ein Rückgängig-Schritt, Ergebnis nur vom Kern; Echtlauf lokale KI mit Rohlog.
- [x] Export PDF/LaTeX/Markdown bzw. PDF/CSV; Fremdprogramm-Beleg.
- [x] Einbindung (Katalog, Symbol, Fähigkeiten, Profil, Team, Bild), requirements.yaml-Vorschlag im Bericht.
- [x] betroffene Testklassen grün (kein Volltest).
**Nicht tun:** requirements.yaml ändern, Volltest, Merge, Release, Claude-Echtlauf.
**Am Ende:** Branch pushen, Bericht `tasks/T-0781-mathe-naturwiss.bericht.md`.
