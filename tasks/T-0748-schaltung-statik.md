# T-0748: Schaltungen: Gerber-Beleg und Import; Statik: Gegenprobe

**Ziel:** Restliste D7 Rang 23 und 24 (docs/audit/2026-11-16-profi-audit/d7-restliste.md): SPEC-02 bekommt eine Prüfung der Fertigungsdaten und einen KiCad-Import, SPEC-03 eine Gegenprobe jeder Rechnung.
**Branch:** claude/T-0748-schaltung-statik
**Dateien/Bereich:** `src/Trendo.Core/Elektro/` (neu `GerberCheck.cs`, `KiCadNetlist.cs`, kleine Korrektur `Pcb.cs`), `src/Trendo.Core/Statik/FrameCrossCheck.cs` (neu), `src/Trendo.App/MainWindow.Spec02.cs`, `src/Trendo.App/MainWindow.Spec03.cs`, Tests, `reports/T-0748/`.
**Fertig, wenn:**
- [x] Gerber- und Bohrdateien werden von einem Fremdwerkzeug gelesen – vorher prüfen (gerbv, kicad-cli, pygerber/gerbonara), nichts installieren; fehlt alles: eigener RS-274X/Excellon-Plausibilitätsleser, ehrlich benannt
- [x] Import einer KiCad-Netzliste (.net) als neue, simulierbare Schaltung
- [x] Gegenprobe jeder Tragwerksrechnung: Formeln (Einfeld, Kragarm, Zweifeld) und zweite Methode (Teilstab-Rechnung); Oberfläche zeigt „Gegenprobe ✓ Abweichung x %“ oder eine Warnung
- [x] Testfälle mit selbst nachgerechneten Lehrbuchwerten; Hinweis „ersetzt keine Prüfstatik“
- [x] Belege unter `reports/T-0748/` mit SHA-256; gezielte Testklassen grün
**Nicht tun:** nichts installieren, kein Volltest, kein Push nach main, keine fremden Dateien.
**Am Ende:** Branch pushen, Bericht `tasks/T-0748-schaltung-statik.bericht.md`.
