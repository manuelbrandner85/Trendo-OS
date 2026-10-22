# T-0100: UI41-0 Baseline – Vorher-Screenshots und Messwerte am Windows-PC

**Ziel:** Echten Ausgangszustand der Windows-App festhalten, bevor Design 41 umgesetzt wird.
**Branch:** grok/T-0100-ui41-baseline
**Dateien/Bereich:** nur `reports/design-41/pc-vorher/` und `tasks/T-0100-ui41-baseline.bericht.md`. Kein Code.
**Vorgehen:** Release-Build starten mit `--offline --no-first-run --page <seite> --screenshot …` für start, studios, brief, ki, settings, system; Startmenü per Win-Taste öffnen und fotografieren; Auflösungen 1366×768 und 1920×1080 (soweit Fenstergröße einstellbar), DPI wie am PC; Kalt-/Warmstart (StartupTrace), RAM im Leerlauf messen. Referenz: `docs/spec/design-41/GROK_BUILD_DESIGN_41_MASTERPROMPT.md` §6 UI41-0.
**Fertig, wenn:**
- [ ] Screenshots aller 7 Ansichten in 2 Auflösungen mit Commit-SHA im Bericht
- [ ] Startzeit und RAM mit Hardwareangabe dokumentiert
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** keine Quellcodeänderung, keine persönlichen Daten in Screenshots (eigenes `--data-root`).
**Am Ende:** Branch pushen, Bericht `tasks/T-0100-ui41-baseline.bericht.md`. Keinen PR öffnen (macht Grok Bot).
