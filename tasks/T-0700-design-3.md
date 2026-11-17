# T-0700: Design-Paket 3 (Bild-Audit V-07, V-09, V-11, V-12, V-14, V-15, V-17 + offene Punkte aus T-0686)

**Ziel:** Gemeinsame Bausteine für Dateiauswahl, „Jetzt einrichten“, Alltagswörter, Schreibzeichen-Knöpfe und Datei-Aktionen; Reiter nur mit Inhalt; eine Flächenfarbe; „Los“ neutral neben der Hauptaktion; Hauptaktion für Programme ohne `Primary`; Gold in allen Bedienstilen; Ausrichtungs-Symbole statt Dreiecken.
**Freigabe:** Nutzer am 16.11.2026, alle 22 Vorschläge V-01…V-22 mit Ja (Audit `docs/audit/2026-11-16-profi-audit/audit-7-anordnung-design.md` auf `claude/T-0674-buero-etappe`).
**Branch:** claude/T-0700-design-3 (vom Integrationszweig `claude/T-0685-b1-integration`)
**Dateien/Bereich:** gemeinsame Bausteine (`MainWindow.Files.cs`, `SimpleWords.cs`, `PlainWords.cs`, neue `MainWindow.Blocker.cs`, `MainWindow.DocActions.cs`, `MainWindow.WriteMarksBar.cs`, `Trendo.Core/Text/WriteMarks.cs`), Hülle (`MainWindow.ProgramShell.cs`, `MainWindow.SimpleMode.cs`) und die im Bericht genannten Seiten.
**Fertig, wenn:**
- [x] jede Regel hat einen UI-Test (`DesignPaket3_Ui`) bzw. Core-Test (`WriteMarksTests`, `PathFieldsTests`)
- [x] keine Funktion entfällt (`UebersichtEinfach_Ui.Simple_keeps_every_function` grün, `einfach-vorher-ausgeblendet.txt` unverändert)
- [x] vorher/nachher im Bild geprüft (≥ 20 Seiten)
- [ ] Volltest (macht die Hauptsitzung)
**Nicht tun:** Inhalte/Logik von Videoschnitt-Vorschau, Animation, Logos, Comics, Podcast, KI-Agenten, Bilder erkennen, Videos mit KI.
**Am Ende:** Branch pushen, Bericht `tasks/T-0700-design-3.bericht.md`.
