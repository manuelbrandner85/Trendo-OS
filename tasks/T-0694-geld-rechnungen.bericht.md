# Bericht T-0694 – „Geld & Rechnungen“ als vollwertiges Programm (D5b)

**Branch:** `claude/T-0694-geld-rechnungen` (abgezweigt von `claude/T-0685-b1-integration`), eigene Kopie in `%TEMP%\trendo-auftrag-T-0694`.

## Herkunft übernommener Teile
Aus `sicherung/T-0675-business-a` (Commit 5129be879) übernommen und angepasst: `Business/Rechnungen.cs` (Rechnungs-Kern: Belege, Summen je Satz, § 14-Prüfung,
Nummernkreis, Kette Angebot → Rechnung, Storno), `Business/BizPdf.cs` + `RechnungPdf.cs` (PDF-Satz), `ERechnung.WriteDetailed/Unit`, `Ledger.InvoiceNumbers` (RE-*.json),
Tests INV-01…INV-05 (aus `BusinessGeldTests`). Nicht übernommen: Mahnwesen, Belege, Preise, die geplanten Einzelstudios, deren Prompt-Adapter.
Angepasst: Speichern über `AtomicFile`, Steuersätze als Tabelle, Käufer-Anschrift sauber zerlegt, „Meine Firma“-Texte, Anrede neutral.

## Was war → was ist (je Punkt)
1. **Rechnungen/Angebote** – war: eine Position, nur HTML, keine Steuernummer (`W5Finance.cs` 64-65). Jetzt: Reiter „Rechnungen“ mit Belegliste (Status Entwurf/offen/überfällig/bezahlt/storniert),
   Editor mit Positionstabelle (Menge, Einheit, Einzelpreis, Rabatt, USt aus der Satz-Tabelle), Live-Summen je Satz, § 14-Prüfliste (✓/✗ mit Hinweis), „Meine Firma“ lokal,
   Festschreiben mit lückenloser Nummer → PDF; ZUGFeRD-PDF (factur-x.xml, PDF/A-3-Kennung in XMP) und XRechnung-XML; Zahlung zuordnen; Angebot → Rechnung; Storno-Rechnung.
   Schnelle Rechnung (alter Weg) bleibt, schreibt jetzt zusätzlich PDF und nimmt die Steuernummer aus „Meine Firma“. Beleg: `GeldRechnungenTests` (INV-01…05, Zugferd_pdf_rundreise,
   Pdf_text_pflichtangaben mit **echtem pdftotext**), `GeldProgramm_Ui.Rechnung_von_null_bis_datei`.
2. **Bankimport** – war: nur CSV mit einer Kopfzeilen-Heuristik. Jetzt: CSV (Sparkasse, DKB, ING, Volksbank, N26, Soll/Haben; unbekannte Spalten werden abgefragt und gemerkt),
   CAMT.053 (Versionen 02/08, Sammelbuchung, Storno, Vormerkungen ausgelassen, Saldenkontrolle), MT940 (strukturiertes :86:, SEPA-Felder, Storno RC/RD, Jahreswechsel, Windows-1252);
   Dubletten auch zwischen den Formaten; Zahlungsvorschläge (Nummer + Betrag = sicher, Teilzahlung, „nur Betrag“ = unsicher). Dateidialog, Strg+O, Ziehen auf die Buchungsliste.
   Beleg: `Camt053`, `Mt940_lesen`, `Csv_layouts`, `Csv_spalten_lernen`, `Dubletten_und_Zahlungsvorschlag`, `GeldProgramm_Ui.Kontoauszug_import`. Testdateien handgeschrieben – **keine echten Bank-Exporte**.
3. **Auswertung** – Reiter „Auswertung“: Jahr/Monat, Kennzahlen, Monatsdiagramm, Kategorien netto/Steuer/brutto; Export PDF (mit Diagramm), XLSX (3 Blätter, Summen als Formeln), CSV;
   DATEV-Buchungsstapel (EXTF 700/21, Windows-1252, Konten als einstellbarer SKR-03-Vorschlag) mit Kennzeichnung „ungeprüft gegen DATEV-Software“; ELSTER mit Hinweis „bewusst nicht“.
   Steuerberater-ZIP nimmt jetzt auch PDF/XML-Rechnungen mit. Beleg: `Auswertung_export`, `Datev_stapel`, `GeldProgramm_Ui.Auswertung_datev_kredit`.
4. **Kredit-Rechner** – neu (Reiter „Kredit“): Annuität aus Laufzeit oder Rate, Rate aus anfänglicher Tilgung, Sondertilgung jährlich/einzeln, Zinsbindung (Restschuld), Gebühren,
   Effektivzins als interner Zinsfuß; Tilgungsplan als Tabelle, Jahresdiagramm, PDF/CSV. Gegenrechnung im Test: Zeile für Zeile, Barwert = Kreditbetrag, geschlossene Restschuld-Formel,
   (1+i/12)^12−1. 10.000 € / 5 % / 60 Monate → 188,71 €, Restschuld 0,00 €.
5. **Kein Datenverlust** – `Ledger`, Rechnungsbuch, Nummernkreis, Archiv, gelernte Kategorien, Budgets, Spalten-Gedächtnis schreiben über `AtomicFile`; Buchungen und Rechnungsbuch
   behalten die vorige Fassung als `.bak`; eine beschädigte Datei wird beiseitegelegt und zurückgeholt, die Oberfläche zeigt einen Hinweis (vorher: `Ledger.All()` warf).
   Rückgängig/Wiederholen (Knöpfe, Strg+Z/Strg+Y) für Buchungen **und** Rechnungen, auch für „Neue Buchung“ und „Rechnung erstellen“. Beleg: `Wiederherstellung`, UI-Tests.
6. **Oberfläche** – Reiter Übersicht · Buchungen · Rechnungen · Auswertung · Kredit; Tabelle mit direkter Bearbeitung (Datum, Text, Betrag, Kategorie, MwSt) und Rechtsklick
   (Stornieren/Doppeln/Löschen), Rechtsklick in der Belegliste; je Reiter eine goldene Hauptaktion; Leerzustand nennt den ersten Schritt; Einfach blendet DATEV, Steuersatz-Tabelle,
   XRechnung und Kredit-Profifelder aus. „Rechnung aus Beschreibung“ legt einen echten Entwurf im Buch an (rückgängig machbar) – Claude erreicht das Feld über die vorhandene Seitenbrücke.
   Bilder geprüft (Übersicht, Buchungen, Rechnungen, Auswertung; Umgebungsvariable `TRENDO_GELD_TAB` wählt für die Bildprüfung den Reiter vor).

## Abnahmekriterien
1. Aus leerer Vorlage echtes Ergebnis: **VOLLWERTIG** – UI-Test von leerem Programm bis PDF/ZUGFeRD/XRechnung.
2. Fremddatei importieren, bearbeiten, exportieren, im Fremdprogramm öffnen: **TEILWEISE** – CAMT/MT940/CSV nach Spezifikation (keine echten Bank-Exporte); PDFs von pdftotext gelesen
   (`reports/T-0694/`, SHA-256); XLSX nur mit Trendos eigenem Leser (kein Excel/LibreOffice auf dem PC); E-Rechnung nicht extern validiert. **Nachtrag T-0776:** „kein LibreOffice“ war falsch – LibreOffice 26.2.4 liegt im Trendo-Werkzeugordner (`%LOCALAPPDATA%\TrendoWerkzeuge\libreoffice`); gesucht wurde nur unter `C:\Program Files\LibreOffice`, dort liegt nur ein Restordner `logs` (`reports/T-0776/werkzeuge.txt`). Die Auswertung-XLSX öffnet in LibreOffice Calc mit drei Blättern, Calc rechnet die Formeln selbst nach (`reports/T-0776/libreoffice-geld-xlsx/`).
3. Prozessabbruch ≤ 30 s: **TEILWEISE** – jede Änderung wird sofort atomar geschrieben, .bak-Wiederherstellung getestet (beschädigte Datei); kein eigener Kill-Test für `finanzen`
   (AtomicFile selbst ist in `ProgramCrashTests` mit echtem Prozessabbruch geprüft). Das Programm läuft weiterhin **nicht** in der Programm-Hülle (Datei nicht anfassbar).
4. KI-Fachaufgabe im offenen Dokument, rückgängig: **TEILWEISE** – „Rechnung aus Beschreibung“ erzeugt einen echten Entwurf (rückgängig), erreichbar für Claude über `feld_setzen`/`klicken`;
   der Prompt-Adapter `InvoicePromptAdapter` schreibt weiterhin nur HTML (Tests verlangen genau eine HTML-Datei, nicht geändert). Kein Lauf mit echtem Claude.
5. Test vom Klick bis zur Datei: **VOLLWERTIG** – `GeldProgramm_Ui` (4 Tests), nur der Dateidialog ist durch `PickOverride` ersetzt.
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: **TEILWEISE** – alles vorhanden; Ziehen und Rechtsklick nicht automatisch getestet.

## Tests (alle im Release-Build, eigene Kopie)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core gezielt (GeldRechnungenTests, W5_Spec11, ERechnung*, Teil4*, Household*, PromptActionTests, NoFixedUser*, CashRegister*, ProgramSafety*): **143 bestanden, 0 Fehler**.
- App gezielt: GeldProgramm_Ui 4/4, W5_Spec11_Ui 1/1, W5_SmallBusiness_Ui 1/1, ERechnungUi 1/1, Haushaltsbuch_Ui 1/1, Teil4BusinessUiTests 3/3, DesignPaket1_Ui 31/31,
  UebersichtVorschlaege_Ui 13/13, StudiosBueroGeld_Ui 36/36, StudiosAlle_Ui 98/98, DeadButtonTests 78/78 (vor den letzten kleinen Änderungen gelaufen).
- Kein Volltest (macht die Hauptsitzung).

## Nicht geprüft
PDF/A-3- und XRechnung-Validierung (kein veraPDF/Mustang/KoSIT; Schematron nicht beigelegt), DATEV-Import, echte Bank-Exporte, XLSX in Excel (LibreOffice: Nachtrag T-0776), Drag-and-drop und Rechtsklick
im Test, Claude-Lauf, Linux/Pi.

## Offene Punkte
- Umzug in die Programm-Hülle (Dokumente, Versionen, Notsicherung der Hülle) – braucht `MainWindow.ProgramShell.cs` (gesperrt).
- `InvoicePromptAdapter` auf PDF/E-Rechnung umstellen, sobald die Tests dafür angepasst werden dürfen.
- Validierung mit freiem Validator (Mustang/KoSIT) nachholen, wenn installiert; mehrere Konten/Mandanten; Mahnwesen aus T-0675.

## Berührte Dateien
`src/Trendo.Core/Business/{Rechnungen,BizPdf,RechnungPdf,ZugferdPdf,BankImport,Kredit,GeldAuswertung}.cs` (neu), `src/Trendo.Core/{ERechnung,Finance,FinanceExtra}.cs`,
`src/Trendo.App/MainWindow.GeldProgramm.cs` (neu), `src/Trendo.App/MainWindow.{W5Finance,Haushaltsbuch,W5Money}.cs`, `tests/Trendo.Core.Tests/{GeldRechnungenTests,GeldBeispiele}.cs` (neu),
`tests/Trendo.App.Tests/GeldProgrammUiTests.cs` (neu), `tests/Trendo.App.Tests/Trendo.App.Tests.csproj`, `reports/T-0694/*`, `tasks/T-0694-geld-rechnungen*.md`.

**Status:** umgesetzt und gezielt getestet; Merge nur nach OK.
