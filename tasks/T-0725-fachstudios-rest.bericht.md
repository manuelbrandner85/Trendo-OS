# Bericht T-0725 – D7: Übrige Fachstudios nach den Lücken der Teilberichte

**Auftrag:** `tasks/T-0725-fachstudios-rest.md` (Audit-Paket D7, freigegeben: „alle Studios bleiben erhalten … aber müssen funktionieren“).
**Zweig:** `claude/T-0725-fachstudios-rest` (Basis `claude/T-0685-b1-integration`), eigene Kopie in `%TEMP%`, nach dem Push gelöscht.
**Restliste:** `docs/audit/2026-11-16-profi-audit/d7-restliste.md` – 8 Programme bearbeitet (A), 41 Einträge als Folgeetappen mit Reihenfolge (B).

## Je Programm: Lücke/vorher → jetzt → Beleg → Urteil

| Programm | Lücke / vorher (Teilbericht) | Jetzt | Beleg (UI-Test echt / Fremdwerkzeug) | Urteil der Lücke |
|---|---|---|---|---|
| Briefe | fremde DOCX/ODT nicht als Brief öffnbar (audit-1 K4/K5) | „Vorhandenen Brief öffnen (Word, LibreOffice) …“ in Schritt 1 und Ablegen aufs Formular: Anschrift, Ort/Datum, Betreff, Anrede, Text (Absätze), Gruß, Anlagen erkannt (`LetterImport`); Absender aus Einstellungen bleibt; Speichern = neuer Trendo-Brief, Original unverändert | `T0725_Fachstudios_Ui.Letter_open_foreign` (Klick, Tippen, Speichern → DOCX, Ablegen .txt); `T0725_Echtlauf_Ui.Letter_from_libreoffice_docx`: LibreOffice schreibt den Brief als DOCX → Trendo → gespeicherter Brief öffnet in LibreOffice → PDF, qpdf ok, Betreff/Empfänger im Text, Original-SHA gleich | geschlossen (Erkennung heuristisch: Briefe ohne „Betreff:“/Anrede landen vollständig im Text) |
| Bücher | ein Markdown-Feld, kein PDF/DOCX, kein Import (audit-2) | „Manuskript öffnen …“ (DOCX/ODT/TXT/MD) + Ablegen, ein Rückgängig-Schritt; Export Word, Druck-PDF, OpenDocument über die Schreiber von Trendo Text (Titelseite, je Kapitel neue Seite, fett/kursiv/Aufzählung, `BookKit.ToTextDoc/FromTextDoc`) | `Book_open_and_export` (Klick, Rückgängig/Wiederholen, Ablegen, 3 Exporte geprüft); Echtlauf: LibreOffice-DOCX mit Formatvorlagen „Heading 1“/„Title“ → 2 Kapitel, fett/kursiv erhalten; Trendo-DOCX und -ODT öffnen in LibreOffice (→ PDF ≥ 3 Seiten, qpdf ok), Trendo-PDF qpdf ok | geschlossen |
| Fotos verwalten | keine Bildansicht im Programm (audit-2) | Raster mit echten Vorschaubildern (bis 120, im Hintergrund dekodiert), Doppelte gold umrandet, Klick = Großansicht mit Datum/Größe/Ordner/Ziel, Doppelklick/Rechtsklick → Bildbearbeitung | `Photos_grid_with_thumbnails` (Ordnerwahl, Probelauf, 5 Kacheln mit Bild, 1 doppelt, Klick, Rechtsklick-Menü, Übergabe; Zielordner bleibt leer) | geschlossen (Bewertung/Stichwörter/RAW bleiben offen) |
| Texte zusammenfassen | kein Dateiimport (audit-4) | „Datei öffnen …“ + Ablegen: PDF (PDFium), DOCX, ODT, TXT, MD; Scan-PDF ohne Text → ehrliche Meldung | `Summary_open_files`; Echtlauf: LibreOffice schreibt Meldung als PDF und DOCX, beide eingelesen | geschlossen (Zusammenfassung selbst weiter Satzauswahl; Sprachmodell-Fassung über „Mit Claude“) |
| Neues Projekt (Vorhaben) | kein Export des Plans (audit-1 K10) | „Schritte als Tabelle (.csv)“ (Semikolon, UTF-8 BOM) und „Zeitplan als Kalenderdatei (.ics)“ (je Schritt ganztägig über die volle Dauer) in die Mappe | `Project_export_csv_ics` (Ical.Net liest Start/Ende); Echtlauf: LibreOffice Calc öffnet die CSV (→ ODS → PDF, Schritte im Text), Ical.Net zählt alle Balken | geschlossen für CSV/ICS (PDF/Bild des Zeitplans, CSV-Import offen) |
| Wissen merken | kein Export in der Oberfläche (audit-4) | „Als Markdown sichern …“: je Seite eine .md (Verweise bleiben), atomar, keine Überschreibung; Wiedereinlesen über „Ordner wählen …“ | `Knowledge_export_and_reimport` (zweites, leeres Trendo liest alle 3 Seiten mit Rückverweis) | geschlossen |
| Karteikarten | kein Rückgängig fürs Löschen, kein Datei-Import per Dialog (audit-4) | „Datei öffnen …“ + Ablegen (eigene CSV, Excel-Text, Anki „Notizen als Klartext“: Kopfzeilen, HTML, Schlagwort-Spalte); gelöschte Karte samt Lernstand zurückholen | `Flashcards_file_import_and_undo_delete` | geschlossen für Text-Export; **.apkg (Anki-Paket) BLOCKIERT** – ehrliche Meldung, kein Anki auf diesem PC |
| Messwerte | CSV nur über eingetippten Pfad; kein PNG (audit-4) | „CSV öffnen …“ + CSV auf die Tabelle ziehen (Rückgängig), Diagramm als PNG 1600×1000 (SkiaSharp aus denselben Formen wie am Bildschirm) | `Measure_csv_open_and_png`; Echtlauf: LibreOffice öffnet das PNG (→ PDF, qpdf ok) | geschlossen (PDF-Bericht, XLSX offen) |

## Tests
- Build `dotnet build Trendo.sln -c Release`: 0 Fehler (vor und nach dem Hereinholen des Integrationszweigs mit T-0722/T-0723/T-0724).
- Core neu `T0725_FachstudiosTests` 6/6; Core gezielt (QA-019, Letter*, Knowledge*, Wiki*, Fsrs*, Learn*, Creation*, KreativKits2, Book*, NoFixedUser*, Messdaten/Measure*): **123 bestanden, 1 übersprungen** (Rechtschreibung braucht das LibreOffice-Wörterbuch, vorbestehend).
- App neu `T0725_Fachstudios_Ui` **8/8**, `T0725_Echtlauf_Ui` **4/4** mit echtem LibreOffice 26.2, qpdf 12.4, Ical.Net (Rohlog `reports/T-0725/echtlauf.log`, `SHA256SUMS`).
- App Pflicht + berührt (39 Klassen in einem Lauf: UebersichtEinfach_Ui, ProgramHeadDesign_Ui, DesignPaket3_Ui, DesignPaket4_Ui, T0710_MenuBar_Ui, AssistChain_Ui, FreigabenKernUi, EveryButton_Alltag, KreativStudios2_Ui, KreativStudios_Ui, CORE10_Ui, NotizWiki_Ui, WissenDocs_Ui, WissenWoche_Ui, T0201_Dateiauswahl_Ui, T0711_HuelleEtappe1b_Ui, SPEC05_Ui, StudiosTechnikWissen_Ui, FsrsUi, T0717_HuelleE5_Ui, Gantt_Ui, VorhabenDemo_Ui, W4_X11_Ui, BueroStudios_Ui, ClaudeLetter_Ui, LetterDin_Ui, LetterF3UiTests, LetterNoBrandingUiTests, LetterOffice_Ui, LetterProUiTests, LetterSendUiTests, LetterWizard_Ui, T0708_HuelleEtappe1_Ui, W1_Letter, W1_LetterCases, QA017_Keyboard, QA019_OfflineUi, DesignHonesty, T0725_Fachstudios_Ui): **184/184 grün**.
- **Rot → grün in diesem Paket:** `UebersichtEinfach_Ui.Simple_keeps_every_function` meldete die neuen Exporte (Bücher Word/PDF/ODT, Messwerte PNG) nur in „Erweitert“ → `SimpleExports` erhöht, damit „Einfach“ keine Funktion verliert; danach grün.
- Nach dem Hereinholen des Integrationszweigs (Stand `5ff321799`): App 16 Klassen (UebersichtEinfach_Ui, ProgramHeadDesign_Ui, DesignPaket3_Ui, DesignPaket4_Ui, T0710_MenuBar_Ui, AssistChain_Ui, FreigabenKernUi, EveryButton_Alltag, KreativStudios2_Ui, KreativStudios_Ui, SPEC05_Ui, LetterWizard_Ui, W4_X11_Ui, CORE10_Ui, FsrsUi, T0725_Fachstudios_Ui) **76/76 grün**; Core QA-019 + T0725 **9/9**.
- `EveryButton_AllPages`: ohne `TRENDO_ALL_BUTTONS=1` übersprungen (eigene Bedingung). Mit `TRENDO_ALL_BUTTONS=1` gestartet – der Lauf gab nach dem Start über 50 Minuten keine Ausgabe mehr und wurde als eigener Prozess beendet: **NICHT GETESTET** (ob er auf dem Integrationszweig ebenso hängt, nicht geprüft).
- `python -m unittest discover -s tests/claude_handoff`: 11 Tests OK.
- Rot-Grün: die neuen Tests nutzen Knöpfe und Mitglieder, die es vorher nicht gab (`book-open`, `sum-open`, `ms-open`, `photos-grid`, `wissen-export`, `learn-import-file`, `learn-undo-del`, `suite-export-*`, `letter-open-foreign`, `BookKit.ToTextDoc`, `LetterImport`, `ExportMarkdown`, `ImportFile`) – auf altem Code nicht übersetzbar. Keine Tests geändert oder geschwächt.
- Im Bild geprüft (Trendo.Render, 1600×1000, eigener Render-Ordner): Bücher (eine goldene Hauptaktion „Neues Kapitel“, „Manuskript öffnen …“ neutral), Zusammenfassen („Datei öffnen …“ neutral), Briefe (Knopf zuerst in Schritt 3 – im Bild zu versteckt, daher nach Schritt 1 unter „Vorlage“ verlegt), Vorhaben, Wissen. Raster der Fotos nur im UI-Test geprüft (Bilder laden im Hintergrund).

## Abnahmekriterien (für die bearbeiteten Lücken)
1. Leere Vorlage → echtes Ergebnis über die Oberfläche: **TEILWEISE** – Bücher, Zusammenfassen, Messwerte, Fotos, Vorhaben, Wissen, Karteikarten, Briefe je vom leeren Zustand bis zur Datei getestet; die übrigen 41 Einträge (Liste B) nicht.
2. Fremddatei rein/bearbeiten/raus, Export im Fremdprogramm: **VOLLWERTIG** für Bücher, Briefe (LibreOffice ↔ Trendo, qpdf), **TEILWEISE** für Zusammenfassen (nur rein), Vorhaben/Messwerte (nur raus, Calc/Draw öffnen), Karteikarten (Anki-Text-Format nach Doku nachgebildet, Anki selbst nicht vorhanden).
3. Prozessabbruch ≤ 30 s: **unverändert** aus der Hülle (T-0708…T-0717); neue Wege sind Rückgängig-Schritte der Hülle (Bücher, Zusammenfassen, Messwerte) bzw. sofort atomar gespeichert (Wissen, Karten, Vorhaben); kein eigener Abbruchtest in diesem Paket – NICHT GETESTET.
4. Claude/KI: **nicht Gegenstand** – unverändert.
5. Klick bis Datei: **VOLLWERTIG** für die 8 Programme (echte Maus-/Tasten-/Ablegeereignisse; ersetzt nur die System-Dateiauswahl).
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **TEILWEISE** – Ablegen in Bücher, Zusammenfassen, Messwerte, Karteikarten, Briefe; Rechtsklick in Fotos; keine neuen Tastenkürzel.

## Was NICHT geprüft wurde
- Word selbst (nur LibreOffice als Fremdprogramm), Anki selbst, ein echter Scanner.
- Die 41 Programme der Liste B.
- Volltest (macht die Hauptsitzung).
- Absätze, die nur eine Gliederungsebene haben (ohne Formatvorlage „Überschrift 1“), werden beim Buch-Import nicht als Kapitel erkannt (gefunden im ersten Echtlauf; Test nutzt jetzt echte Formatvorlagen, wie Word/Writer sie setzen).

## Offene Punkte
- Liste B in der angegebenen Reihenfolge (zuerst Kleine Helfer & Musik, Audio-Aufnahme, Bildbearbeitung WebP/Kürzel, KI fragen mit Anhang).
- Bücher: Word/PDF stehen in „Einfach“ nur über das Datei-Menü/„Erweitert“ im Menüband (Einfach zeigt den ersten Export, das E-Book).
- PDFsharp-Warnung „Flate-Strom ohne Abschluss“ bei qpdf (bekannt aus T-0705) auch beim Buch-PDF.

## Berührte Dateien
- neu: `src/Trendo.Core/Kreativ/BookFiles.cs`, `src/Trendo.Core/LetterImport.cs`, `src/Trendo.Core/CreationExport.cs`, `tests/Trendo.App.Tests/T0725FachstudiosUiTests.cs`, `tests/Trendo.App.Tests/T0725EchtlaufUiTests.cs`, `tests/Trendo.Core.Tests/T0725FachstudiosTests.cs`, `tools/echtlauf/fachstudios/t0725.py`, `docs/audit/2026-11-16-profi-audit/d7-restliste.md`, `reports/T-0725/…`, `tasks/T-0725-fachstudios-rest*.md`
- geändert: `src/Trendo.App/MainWindow.KreativStudios.cs` (Zusammenfassen), `MainWindow.KreativStudios2.cs` (Bücher, Fotos), `MainWindow.Spec05.cs`, `MainWindow.Spec06.cs`, `MainWindow.W1Office.cs` (Briefe), `MainWindow.W4Suite.cs`, `MainWindow.Wissen.cs`; `src/Trendo.Core/W2/Knowledge.cs` (+`ExportMarkdown`), `src/Trendo.Core/W6/Learning.cs` (+`ImportFile`, `Take`, `Restore`)

## Rust
Nicht eingesetzt: kleine Erweiterungen in bestehendem C#-Code (Avalonia/.NET), kein messbarer Engpass.
