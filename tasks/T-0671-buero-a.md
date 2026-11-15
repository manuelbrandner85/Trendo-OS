# T-0671 Arbeiten & Büro, Teil A: Bewerbung & Lebenslauf, Karten & Einladungen, Verträge-Ordner

- Freigabe: 79 neue Studios am 09.11.2026 („Ja“), 15.11.2026 „Ja, weiter“
- Zweig: `claude/T-0671-buero-a`
- Studios: `STUDIO-CV-01` (`bewerbung-programm`), `STUDIO-CARDS-01` (`karten-programm`), `STUDIO-CONTRACTS-01` (`vertraege-programm`), Kategorie „Arbeiten & Büro“
- Muster: Künstlerstudio (T-0670) – Kern in `src/Trendo.Core/Buero/`, Adapter in `src/Trendo.Core/PromptAction/BueroPromptAdapters.cs`, Oberfläche `src/Trendo.App/MainWindow.Bewerbung.cs`, `.Karten.cs`, `.Vertraege.cs`
- Wiederverwendet: PDFsharp und `PdfTools.Merge`, Brief-Kern (`LetterDraft`, `LetterKit`-Vorlage „Kündigung“, `LetterPdf`), Übergabe an „Briefe schreiben“, `DocxFormat` von „Trendo Text“, `Sys.Ics.Export` des Kalenders, KI-Kern `Intel.AskAsync`
- Anforderungen: `docs/nachbau/bewerbung|karten|vertraege/anforderungen.md`; Vorbild-Analysen in `docs/claude/vorbilder/`
- Tests: Core `BueroTests` (21), UI `BueroStudios_Ui` (5)
- Nicht angefasst (Hauptsitzung): Zähl-Tests, `docs/programme/`, Capability-Matrix, Version, Neuerungen, Studiobilder
