# T-0694: „Geld & Rechnungen“ als vollwertiges Programm (D5b)

**Ziel:** Das vorhandene Programm „Geld“ (Seite `finanzen`, SPEC-11) wird ein echtes Finanzprogramm: Rechnungen und Angebote mit Pflichtangaben-Prüfung, PDF und E-Rechnung, Bankimport (CSV/CAMT.053/MT940), Auswertung mit Export und DATEV-Stapel, Kredit-Rechner, kein Datenverlust.
**Branch:** claude/T-0694-geld-rechnungen (vom Integrationszweig claude/T-0685-b1-integration)
**Dateien/Bereich:** `src/Trendo.Core/Business/*` (neu), `ERechnung.cs`, `Finance.cs`, `FinanceExtra.cs`, `src/Trendo.App/MainWindow.W5Finance.cs`, `MainWindow.GeldProgramm.cs` (neu), `MainWindow.Haushaltsbuch.cs`, `MainWindow.W5Money.cs`, Tests, `reports/T-0694/`.
**Fertig, wenn:**
- [x] Angebot → Rechnung, Positionen mit Menge/Einheit/Preis/Rabatt, USt-Sätze als einstellbare Tabelle mit Stand, § 19 UStG, lückenlose Nummern, § 14-Prüfung sagt genau, was fehlt
- [x] PDF (PdfSharp), ZUGFeRD-PDF mit factur-x.xml, XRechnung-XML; wieder eingelesen stimmen die Beträge
- [x] Bankimport CSV (6 Layouts, lernbare Spalten), CAMT.053, MT940; Dubletten; Zahlungsvorschläge
- [x] Auswertung (Monat/Jahr/Kategorie, Diagramm), CSV/XLSX/PDF, DATEV-Buchungsstapel (gekennzeichnet)
- [x] Kredit-Rechner mit Tilgungsplan, Sondertilgung, Effektivzins-Näherung, Gegenrechnung im Test
- [x] AtomicFile + .bak-Wiederherstellung, Rückgängig/Wiederholen für Buchungen und Rechnungen
**Nicht tun:** `MainWindow.ProgramShell.cs`, `SimpleWords.cs`, „Online verkaufen“, POD; keine vier neuen Studios; ELSTER bewusst nicht.
**Am Ende:** Branch pushen, Bericht `tasks/T-0694-geld-rechnungen.bericht.md`.
