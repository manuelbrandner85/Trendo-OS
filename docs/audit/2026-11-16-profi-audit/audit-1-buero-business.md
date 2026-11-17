# Audit Gruppe 1 – Arbeiten & Büro, Business & Geld

Stand: Arbeitskopie `studios-aufraeumen` (ba52927a8), **nur gelesen** – kein Build, kein Testlauf. Urteile stützen sich auf Code und Testquelltext; Testnamen sind gelesen, nicht ausgeführt. „nicht geprüft“ = nicht belegt.
Pfad-Abkürzungen in den Abschnitten ab STUDIO-CV-01: `App/` = `src/Trendo.App/`, `Core/` = `src/Trendo.Core/`, `CT/` = `tests/Trendo.Core.Tests/`, `AT/` = `tests/Trendo.App.Tests/`. Urteils-Kürzel in Mehrprogramm-Tabellen: V = VOLLWERTIG, T = TEILWEISE, F = FEHLT, B = BLOCKIERT, NG = NICHT GETESTET.
Abgelehntes (SPEC-07, Verbesserung 86, TSE-Kasse „bewusst nicht“) ist nicht als Lücke gewertet.

16 Studios, 26 Programme. Gesamturteile: **1 × VOLLWERTIG** (PDF-Werkstatt), **15 × TEILWEISE**, 0 × FEHLT, 0 × BLOCKIERT. Einzelprogramm „E-Mail & Kontakte“ (`post`) ist als Fachprogramm FEHLT (Formular, gibt an fremdes Mailprogramm ab).

## Übersicht

| Studio-Id | Name | Programme | Gesamturteil | größte Lücke | Aufwand |
|---|---|---|---|---|---|
| SYS-10 | Texte & Tabellen | doku-start, text, tabelle, praesentation, pdf | TEILWEISE | Fremde DOCX/XLSX/PPTX werden beim Speichern ohne Warnung ärmer (Modell kennt u. a. keine Zellformate/verbundenen Zellen); Text ist ein Eingabefeld je Absatz; Abgleich mit Word/Excel/PowerPoint nie, mit LibreOffice im Normallauf übersprungen | sehr groß |
| SYS-09 | E-Mails & Termine | post, posteingang, kalender, kontakte, aufgaben | TEILWEISE | Mail ist nur Lese-Abruf von 50 Kopfzeilen + Klartext-Versand ohne Anhang; kein Server-Abgleich (CalDAV/CardDAV/POP3 fehlen), nie gegen echten Anbieter getestet | groß |
| SYS-02 | Kleine Helfer & Musik | alltag-start, werkzeuge, medien | TEILWEISE | Ton nur mit nachinstalliertem mpv (Windows-Pipe), Video nur in fremdem Fenster, Bildschirmfoto nur Trendo-Fenster bzw. ffmpeg | mittel |
| STUDIO-LETTER-01 | Briefe schreiben | brief | TEILWEISE | Formular-Assistent statt Schreiben auf der Seite; kein Autosave/Absturzschutz; fremde DOCX nicht öffenbar | mittel |
| X-11 | Neues Projekt starten | vorhaben | TEILWEISE | kein Export (PDF/CSV/Projektdatei), keine Zuständigen/Ressourcen, KI-Aktion legt etwas anderes an als das Programm | mittel |
| STUDIO-CV-01 | Bewerbung & Lebenslauf | 1 (bewerbung-programm) | TEILWEISE | Formular mit nachgebauter Vorschau, kein Import vorhandener Lebensläufe, KI-Aktion liefert nur TXT | mittel |
| STUDIO-CARDS-01 | Karten & Einladungen | 1 (karten-programm) | TEILWEISE | kein freies Gestalten auf der Karte (Text nur senkrecht per Regler), Farben als Hex-Text | groß |
| STUDIO-CONTRACTS-01 | Verträge-Ordner | 1 (vertraege-programm) | TEILWEISE | reine Liste + Formular, Dokumente nur verknüpft, Kalender nur als Datei | mittel |
| STUDIO-PDFTOOLS-01 | PDF-Werkstatt | 1 (pdf-werkstatt-programm) | VOLLWERTIG | KI-Aktion nur Auftragstext; Schwärzen macht die ganze Seite zum Bild; kein Ziehen-und-Ablegen | klein–mittel |
| STUDIO-FORMS-01 | Formulare ausfüllen | 1 (formulare-programm) | TEILWEISE | nur an selbst erzeugten Formularen getestet, kein XFA, frei Gesetztes nicht verschiebbar | mittel |
| STUDIO-DECKDESIGN-01 | Präsentationen gestalten | 1 (praesentation-gestalten-programm) | TEILWEISE | Gliederungstext → Folien-Generator, keine Bearbeitung auf der Folie; PPTX nur mit eigenem Leser geprüft | mittel–groß |
| STUDIO-DOCSCAN-01 | Dokumente scannen | 1 (scannen-programm) | TEILWEISE | kein Scanner (Knopf zeigt nur Hinweis), Texterkennung im Test ersetzt, nur künstliche Testfotos | mittel–groß |
| STUDIO-DAYPLAN-01 | Tagesplaner | 1 (tagesplaner-programm) | TEILWEISE | Listen/Textfelder statt Zeitleiste, kein Verschieben von Blöcken, nichts geht direkt in den Kalender | mittel |
| SPEC-11 | Geld & Rechnungen | finanzen („Geld“) | TEILWEISE | Rechnung nur als HTML ohne Steuernummer; kein PDF, kein MT940/CAMT, kein DATEV/ELSTER, kein Kredit-Rechner; läuft nicht in der Programm-Hülle | groß |
| STUDIO-ONLINE-BUSINESS-01 | Online verkaufen | verkaufen (Kasse · Online & Artikel · Auswertung), Reiter „Geschäftsidee“ | TEILWEISE | „Online verkaufen“ erzeugt nur eine statische HTML-Vorschau ohne Bestellung und Zahlung; Geschäftsidee ist Formular + Markdown-Bauplan | groß |
| STUDIO-POD-01 | Print on Demand | pod-programm | TEILWEISE | Anbieter-Anbindung (Printful, Printify, Stripe, Netlify) nie gegen echte Dienste belegt; echte Mockups nur mit zwei Fremdkonten; Gelato fehlt | mittel |

## SYS-10 Texte & Tabellen

Programme laut `docs/programme/katalog.json`: `doku-start` (Start), `text` (Trendo Text), `tabelle` (Trendo Tabelle), `praesentation` (Trendo Präsentation), `pdf` (Trendo PDF). Alle fünf laufen **nicht** in der Programm-Hülle (Katalog `"Shell": false`), sondern als eigene Seiten (`src/Trendo.App/MainWindow.Text.cs:38-42`, `MainWindow.Pdf.cs:31-36`). Alle Dateiformate sind selbst geschrieben, ohne Fremdbibliothek (`src/Trendo.Core/Office/DocxFormat.cs:9`, `OdtFormat.cs:9`, `SheetFiles.cs:10`, `SlideFiles.cs:9`). Nur gelesen, kein Build, kein Testlauf.

### Trendo Text (`text`)

| Nr | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | TEILWEISE | Weißes Blatt mit Rändern, Kopf-/Fußzeile, Zoom (`MainWindow.Text.cs:319-349`). Aber absatzweise: nur der Absatz mit dem Cursor ist ein Eingabefeld, ein gewöhnliches `TextBox` mit einer Schrift für den ganzen Absatz (`MainWindow.Text.cs:18-19`, `:382-389`); Auswahl gilt nur innerhalb eines Absatzes (`:69-84`). Kein Seitenumbruch auf dem Bildschirm, Fußzeile zeigt fest „Seite 1 von …“ (`:340`). |
| 2 | Werkzeuge real | VOLLWERTIG | Zeichen-/Absatzformat, Listen bis 9 Ebenen, Tabellen, Bilder, Suchen/Ersetzen, Inhaltsverzeichnis, Fuß-/Endnoten, Kommentare, Änderungen vergleichen/annehmen, Serienbrief, Etiketten, Umschläge, Barrierefreiheit (`TextDoc.cs:186-459`, `TextTools.cs:19-312`); Rechtschreibung Deutsch offline (`Spelling.cs:6-59`). |
| 3 | Neues Projekt | VOLLWERTIG | `TextNew` mit Vorlagen (`MainWindow.Text.cs:45-50`, `TextTemplates.cs`). |
| 4 | Echte Dateien importieren | TEILWEISE | DOCX, ODT, TXT werden gelesen (`OdtFormat.cs:498-504`, `DocxFormat.cs:324-326`). Das Modell kennt aber nur Absatz, Tabelle mit reinen Text-Zellen, Bild, Seitenumbruch, Verzeichnis (`TextDoc.cs:30-157`, Tabelle `:134-136`): keine Zellformate, verbundenen Zellen, Textfelder, Formen, Abschnitte, Felder. Altes `.doc` wird abgelehnt (Test „Office ohne LibreOffice …“, `OfficeConvertTests.cs:10`). Wie der Leser mit solchen Inhalten fremder Dateien umgeht: nicht geprüft. |
| 5 | Vorhandene bearbeiten | TEILWEISE | Öffnen setzt `TextPath` auf die Originaldatei, Strg+S schreibt dorthin zurück (`MainWindow.Text.cs:54`, `:423`). Ein Hinweis auf nicht übernommene Inhalte existiert nicht (Suche nach „Verlust/nicht übernommen“ in `Office/*.cs` und `MainWindow.Text*.cs` ohne Treffer) – Risiko: fremde Word-Datei wird beim Speichern ärmer. |
| 6 | Rückgängig | VOLLWERTIG | 80 Schritte als Dokumentkopien, Strg+Z/Y (`MainWindow.Text.cs:87-96`, `:420-421`). |
| 7 | Echte Vorschau | TEILWEISE | Blattansicht ohne Seitenumbruch (`:319-349`); echte Seiten erst im PDF/Druck über eigenen Seitensatz (`TextPdf.cs:7-20`, `MainWindow.Text.cs:304-316`). |
| 8 | Speichern/Autosave | VOLLWERTIG | Sicherung ungespeicherter Änderungen als DOCX im Datenordner, über den Zeitgeber der Hülle (`MainWindow.OfficeRecovery.cs:10-25`, Aufruf `MainWindow.ProgramShell.cs:248`). |
| 9 | Wiederherstellung | VOLLWERTIG | Banner „Wiederherstellen/Verwerfen“ (`MainWindow.OfficeRecovery.cs:33-63`); Test „T-0254: Text und Tabelle – ungespeichert gesichert, nach Neustart ‚Wiederherstellen‘ …“ (`OfficeRecoveryUiTests.cs:15`). Nur eine Sicherung je Programm (`:16`). |
| 10 | Export | VOLLWERTIG | DOCX mit echten Formatvorlagen, ODT, TXT, PDF mit Lesezeichen (`DocxFormat.cs:9-12`, `OdtFormat.cs:29`, `TextPdf.cs:20`; Test `TextDocTests.Pdf_export`). |
| 11 | Rundreise | TEILWEISE | Eigene Rundreise DOCX/ODT belegt (`TextDocTests.cs:109-143`). Fremdprogramm: LibreOffice-Abgleiche kehren ohne LibreOffice still mit „bestanden“ zurück (`TextDocTests.cs:147`, `TextProTests.cs:173`, `:193`); ein realer Lauf ist belegt (`reports/t0223-office/text-abgleich/ERGEBNIS.txt`, LibreOffice 26.2.4). Mit Microsoft Word: nicht geprüft. Keine Fremddatei als Testvorlage unter `tests/`. |
| 12 | Fachfunktion real | VOLLWERTIG | Es entstehen echte, in Fremdprogrammen lesbare Dateien (siehe 10/11). |
| 13 | KI arbeitet im Programm | TEILWEISE | Kein Adapter „Text schreiben“: für SYS-10 gibt es nur `tabelle.rechnen` (`SheetPromptAdapter.cs:10-14`). Claude live lädt nur `.md`/`.txt` in Trendo Text, keine DOCX (`MainWindow.ClaudeLive.cs:104-107`). KI-Vorschlag im Text mit Wort-Vergleich vorhanden (`TextTools.cs:204-205`, `TextAiProposalTests.cs`). |
| 14 | Ende-zu-Ende-Tests | TEILWEISE | Kern- und Oberflächentests echt (`TextUiTests`, `TextProUiTests`, `TextF3UiTests`); Fremdprogramm-Abgleich im Normallauf übersprungen (siehe 11). |
| 15 | Bedienung | TEILWEISE | Strg+B/I/U/Z/Y/F/S, Tab in Listen, Rechtsklick-Menü je Absatz (`MainWindow.Text.cs:392`, `:404-423`). Kein Ziehen-und-Ablegen im Programm (kein `DragDrop` in `MainWindow.Text*.cs`), Vollbild für Text: nicht geprüft. |

### Trendo Tabelle (`tabelle`)

| Nr | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | VOLLWERTIG | Eigenes Zellgitter mit Bereichsauswahl, Formelzeile, Blattregister (`SheetGridView.cs`, `MainWindow.Tabelle.cs:673-709`, `:850-853`). |
| 2 | Werkzeuge real | VOLLWERTIG | Formelrechner mit deutschen Funktionsnamen (über 100 Funktionen, `SheetFunctions.cs:9-40`, `Formula.cs`, `SheetCalc.cs`), Sortieren, Filter, Autofilter, bedingte Formate, Auswahllisten, Blattschutz, Fixieren, Notizen, Pivot, Diagramme (`SheetDoc.cs:72-86`, `:158-199`, `SheetPivot.cs`). |
| 3 | Neues Projekt | VOLLWERTIG | Vorlagen (`SheetTemplates.cs`), Startseite (`MainWindow.Tabelle.cs:335`). |
| 4 | Echte Dateien importieren | TEILWEISE | XLSX, ODS, CSV lesen (`SheetFiles.cs:14-23`). Zellformat des Modells: nur Fett, Füllfarbe, Rahmen ja/nein, 8 Zahlenformate (`SheetDoc.cs:47-61`); keine Schriftfarbe, Kursiv, Ausrichtung, verbundene Zellen (Suche „merge/verbund“ in `SheetDoc.cs`, `SheetFiles.cs`, `MainWindow.Tabelle*.cs` ohne Treffer). Altes `.xls`: abgelehnt (`OfficeConvertTests.cs:10`). |
| 5 | Vorhandene bearbeiten | TEILWEISE | Wie Text: Formatierung fremder Mappen außerhalb des Modells geht beim Speichern verloren, ohne Hinweis (kein Treffer für einen Verlust-Hinweis). ODS nur lesen, nicht schreiben (`SheetFiles.cs:10`). |
| 6 | Rückgängig | VOLLWERTIG | 100 Schritte (`MainWindow.Tabelle.cs:110-111`, `:341`). |
| 7 | Echte Vorschau | TEILWEISE | Gitter zeigt Rechenergebnisse und Formate live; Druck/PDF über `SheetPdf.cs`, Strg+P (`MainWindow.Tabelle.cs:709`). Seitenumbruch-Vorschau: nicht geprüft. |
| 8 | Speichern/Autosave | VOLLWERTIG | `MainWindow.OfficeRecovery.cs:23`. |
| 9 | Wiederherstellung | VOLLWERTIG | `OfficeRecoveryUiTests.cs:15`. |
| 10 | Export | VOLLWERTIG | XLSX mit englischer Formel und zwischengespeichertem Ergebnis (Test `Xlsx_roundtrip`: `<f>SUM(B2:B5)</f><v>1023.64</v>`, `SheetFilesTests.cs:63-82`), CSV, PDF (`SheetPdf.cs`, `SheetChartPdfTests`). |
| 11 | Rundreise | TEILWEISE | Eigene Rundreise mit Werten, Formeln, Formaten, Breiten, Blättern belegt (`SheetFilesTests.cs:63-82`). Abgleich mit LibreOffice Calc wird ohne LibreOffice still übersprungen (`SheetFilesTests.cs:129`); ein Beleg unter `reports/` für Tabelle: nicht gefunden. Mit Microsoft Excel: nicht geprüft. |
| 12 | Fachfunktion real | VOLLWERTIG | Formeln werden wirklich gerechnet (`SheetCalcTests`, `SheetFunctionTests`). |
| 13 | KI arbeitet im Programm | TEILWEISE | `tabelle.rechnen`, Stufe Partial: schreibt echte XLSX und liest sie zur Kontrolle zurück (`SheetPromptAdapter.cs:14`, `:44-65`) – aber über einen zweiten, einfacheren Schreiber `Ooxml.NewXlsx` (`Sys/Sys10.cs:11`), laut Katalog „Nur Grundrechenarten und SUMME“ (`CapabilityCatalog.cs:21`). Claude live nur über `.csv` (`MainWindow.ClaudeLive.cs:108-111`). |
| 14 | Ende-zu-Ende-Tests | TEILWEISE | 49 Tests laut Katalog; Fremdprogramm-Abgleich im Normallauf übersprungen. |
| 15 | Bedienung | TEILWEISE | Strg+C/X/V/Z/Y/B/S/A/D/E/P, Vollbild F11 (`MainWindow.Tabelle.cs:601`, `:698-709`). Rechtsklick auf Zellen fehlt (kein Rechtsklick in `SheetGridView.cs`; Menüs hängen nur an Knöpfen, `MainWindow.Tabelle.cs:538-663`). |

### Trendo Präsentation (`praesentation`)

| Nr | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | VOLLWERTIG | Folienfläche mit Auswählen, Ziehen, Größe ändern am Griff, Raster (`SlideView.cs:35-96`), Text direkt auf der Folie (`MainWindow.Praesentation.cs:588-589`). |
| 2 | Werkzeuge real | TEILWEISE | Layouts, Designs, Text, Bild, Form, Tabelle, Diagramm, Medien, Übergänge, Animationen (`SlideDoc.cs:6-46`). Ein Objekt hat nur ein Format für den ganzen Text (`SlideDoc.cs:26-34`); kein Drehen, Gruppieren, Folienmaster im Modell. |
| 3 | Neues Projekt | VOLLWERTIG | `SlideDoc.Empty`, Gliederung zu Folien (`SlideDoc.cs:108`, `:171-173`). |
| 4 | Echte Dateien importieren | TEILWEISE | PPTX und ODP lesen, Platzhalterlage aus dem Folienlayout (`SlideRead.cs:30`, `:89`, `:210`). Fremde Gestaltung außerhalb des Modells (Master, gemischte Textformate, Drehung): nicht darstellbar. Altes `.ppt`: abgelehnt. |
| 5 | Vorhandene bearbeiten | TEILWEISE | Wie oben: Speichern schreibt nur, was das Modell kennt; ODP nur lesen (`SlideFiles.cs:9`). |
| 6 | Rückgängig | VOLLWERTIG | 80 Schritte (`MainWindow.Praesentation.cs:181-187`). |
| 7 | Echte Vorschau | VOLLWERTIG | Vorführung im Vollbild mit Übergängen, Klick-Animationen, Laserpointer, Schwarzbild (`MainWindow.SlideShow.cs:13`, `:45`, `:148-154`). |
| 8 | Speichern/Autosave | VOLLWERTIG | `MainWindow.OfficeRecovery.cs:24`. |
| 9 | Wiederherstellung | TEILWEISE | Code vorhanden (`MainWindow.OfficeRecovery.cs:44`); der Test nennt nur Text und Tabelle (`OfficeRecoveryUiTests.cs:15`) – für Präsentation NICHT GETESTET. |
| 10 | Export | VOLLWERTIG | PPTX mit Animationen, PDF je Folie, wahlweise mit Notizen (`SlideFiles.cs:47`, `:221`, `SlidePdf.cs:11`). |
| 11 | Rundreise | TEILWEISE | Eigene Rundreise belegt (`SlideTests.cs:58-70`). Abgleich mit Impress ohne LibreOffice still übersprungen (`SlideTests.cs:117`); Beleg unter `reports/`: nicht gefunden. PowerPoint: nicht geprüft. |
| 12 | Fachfunktion real | VOLLWERTIG | Echte PPTX, echte Vorführung. |
| 13 | KI arbeitet im Programm | TEILWEISE | Kein eigener Adapter für SYS-10; Gliederungs-Prompt an die lokale KI (`SlideDoc.cs:216-217`). Die echte PPTX-Aktion liegt beim Studio STUDIO-DECKDESIGN-01 (`DeckScanPromptAdapters.cs:43-51`). |
| 14 | Ende-zu-Ende-Tests | TEILWEISE | `SlideTests`, `PraesentationUiTests` echt; Fremdprogramm übersprungen. |
| 15 | Bedienung | TEILWEISE | Strg+Z/Y/D/S/M, F5, Bild auf/ab, Alt+Pfeil (`MainWindow.Praesentation.cs:554-564`). Kein Rechtsklick auf der Folie (kein Rechtsklick in `SlideView.cs`). |

### Trendo PDF (`pdf`)

| Nr | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | TEILWEISE | Betrachter mit Miniaturleiste; Markieren, Notiz, Unterschrift direkt auf der Seite (`PdfView.cs:10-46`, `MainWindow.Pdf.cs:372`, `MainWindow.PdfF3.cs:50`). Vorhandenen Text oder Bilder der Seite ändern: nicht vorhanden (Funktionsliste `Sys/PdfTools.cs:105-290`). |
| 2 | Werkzeuge real | VOLLWERTIG | Zusammenfügen, Seiten wählen, Teilen, Drehen, Passwort setzen/entfernen, Markieren, Notiz, Formularfelder lesen/füllen (`PdfTools.cs:105-290`), Seiten ordnen (`MainWindow.PdfOrganize.cs:14-15`). |
| 3 | Neues Projekt | TEILWEISE | Nur leere Seite (`PdfTools.cs:191`). |
| 4 | Echte Dateien importieren | VOLLWERTIG | PDFium über PDFtoImage, auch mit Passwort (`PdfView.cs:10`, `MainWindow.Pdf.cs:40`). |
| 5 | Vorhandene bearbeiten | TEILWEISE | Nur Anmerkungen, Seiten, Formulare; Original bleibt, Arbeitskopie „Name (Trendo).pdf“ (`MainWindow.Pdf.cs:16-18`, `:88`). |
| 6 | Rückgängig | TEILWEISE | Nur in „Seiten ordnen“ (`MainWindow.PdfOrganize.cs:18`, `:125`); für Markierungen, Notizen, Unterschrift kein Rückgängig (kein Treffer in `MainWindow.Pdf.cs`, `MainWindow.PdfF3.cs`). |
| 7 | Echte Vorschau | VOLLWERTIG | Seiten werden mit PDFium gezeichnet, Anmerkungen und Formularwerte mit (`PdfView.cs:26`). |
| 8 | Speichern/Autosave | TEILWEISE | Jede Aktion schreibt sofort eine neue Datei (`MainWindow.Pdf.cs:16-18`); kein Autosave nötig, aber auch keine Sicherung offener „Seiten ordnen“-Schritte (`MainWindow.OfficeRecovery.cs:16` kennt nur Text, Tabelle, Präsentation). |
| 9 | Wiederherstellung | FEHLT | Siehe 8. |
| 10 | Export | VOLLWERTIG | PDF; durchsuchbare PDF mit Textebene (`MainWindow.PdfF3.cs:115`). |
| 11 | Rundreise | VOLLWERTIG | Ergebnis wird sofort wieder geöffnet und durchsucht (`PdfF3UiTests.cs:89-94`); `reports/t0223-office/qpdf-abgleich.log` vorhanden (Inhalt nicht geprüft). |
| 12 | Fachfunktion real | VOLLWERTIG | Siehe 2. |
| 13 | KI arbeitet im Programm | FEHLT | Kein Adapter für PDF unter SYS-10 (`SheetPromptAdapter.cs:10-11` ist der einzige); „PDF bearbeiten“ ist nur eine Aufgaben-Kachel (`StudioKnowledge.cs:369`). |
| 14 | Ende-zu-Ende-Tests | TEILWEISE | Texterkennung: Test endet ohne installiertes Tesseract nach dem Hinweistext (`PdfF3UiTests.cs:84`), also je nach PC NICHT GETESTET; übrige Wege echt (`PdfUiTests`, `PdfOrganizeUiTests`, `PdfToolsTests`). |
| 15 | Bedienung | TEILWEISE | Tasten nur Enter in Seiten-/Suchfeld (`MainWindow.Pdf.cs:189`, `:213`); kein Strg+Z, kein Rechtsklick. Seiten ziehen in „Seiten ordnen“ laut Kopfkommentar (`MainWindow.PdfOrganize.cs:14`), nicht einzeln geprüft. |

### Start (`doku-start`)

Startseite mit zuletzt benutzten Dateien, Filter nach Art und Zeitraum, Öffnen im passenden Trendo-Programm (`MainWindow.DokuStart.cs:14-22`), Umwandeln DOCX/XLSX/PPTX/ODT/ODS/ODP/CSV/MD zu PDF und DOCX zu ODT ohne Zusatzprogramm (`OfficeConvert.cs:8-34`, Test `OfficeConvertTests.Convert_all`). Kein Fachprogramm, sondern Einstieg: Kriterien 1, 6–9 nicht anwendbar; 4/10 VOLLWERTIG; 13 FEHLT; 15 nicht geprüft.

**Gesamturteil:** TEILWEISE – für selbst angelegte Dokumente ein echtes, rechnendes und exportierendes Büropaket; als Ersatz für Word/Excel/PowerPoint bei fremden Dateien nicht belastbar, weil das Modell vieles nicht kennt und beim Speichern ohne Warnung weglässt.

**Was fehlt zum vollwertigen Fachprogramm (priorisiert)**
1. Schutz fremder Dateien: beim Öffnen prüfen, was nicht übernommen wird, anzeigen und standardmäßig als Kopie speichern (heute überschreibt Strg+S das Original, `MainWindow.Text.cs:54`, `:423`).
2. Text: durchgehender Editor mit sichtbaren Zeichenformaten beim Tippen, Auswahl über Absätze hinweg und echten Seiten auf dem Bildschirm (heute ein `TextBox` je Absatz, `MainWindow.Text.cs:382-389`).
3. Modell-Lücken schließen: Tabellenzellen mit Format und verbundenen Zellen in Text; Schriftfarbe, Kursiv, Ausrichtung, verbundene Zellen in Tabelle; gemischte Textformate, Drehen, Gruppieren in Präsentation.
4. Fremdprogramm-Abgleich verpflichtend machen: echte Word-/Excel-/PowerPoint-Dateien als Testvorlagen unter `tests/`, und Abgleichtests, die ohne Gegenprogramm als „übersprungen“ statt „bestanden“ zählen.
5. KI-Aktionen für Text, Präsentation und PDF, die das offene Dokument ändern; `tabelle.rechnen` auf den vollen Schreiber `SheetFiles` umstellen.
6. Rechtsklick in Zellgitter und Folie, Rückgängig für PDF-Anmerkungen, ODS/ODP schreiben.

**Abnahmekriterien**
- Zehn echte Fremddateien je Format (DOCX, XLSX, PPTX aus Microsoft Office und LibreOffice) öffnen, speichern, im Ursprungsprogramm wieder öffnen: kein Inhalt fehlt, oder Trendo hat vorher genau benannt, was fehlt.
- In Trendo Text lässt sich über drei Absätze markieren und fett setzen; das Fett ist beim Weitertippen sichtbar; ab Seite 2 zeigt der Bildschirm einen Seitenumbruch und die richtige Seitenzahl.
- Eine XLSX mit verbundenen Zellen, farbiger Schrift und zentrierter Ausrichtung sieht nach Öffnen und Speichern in Excel gleich aus.
- Die Abgleichtests mit dem Fremdprogramm laufen im Volltest wirklich oder werden als übersprungen ausgewiesen; ein Rohlog liegt unter `reports/`.
- Prompt „Schreibe einen Bericht über …“ erzeugt eine DOCX, die in Trendo Text offen ist; Prompt „Füge Folie 3 ein Diagramm hinzu“ ändert die offene Präsentation.

**Aufwand:** sehr groß (Text-Editor und Modell-Lücken), Schutz fremder Dateien allein klein bis mittel. **Risiko:** hoch – stiller Datenverlust in fremden Dateien ist der schwerste Fehler der Gruppe; ein neuer Text-Editor berührt 48 bestehende Tests.

**Dateien:** `src/Trendo.Core/Office/{TextDoc,DocxFormat,OdtFormat,TextPdf,TextTools,Spelling,SheetDoc,SheetFiles,SheetCalc,SheetFunctions,Formula,SheetPivot,SheetPdf,SlideDoc,SlideFiles,SlideRead,SlidePdf,OfficeConvert}.cs`, `src/Trendo.Core/Sys/{PdfTools,PdfOrganize,PdfSignOcr,Sys10}.cs`, `src/Trendo.App/MainWindow.{Text,TextPro,TextLook,Tabelle,TabelleF3,Praesentation,SlideShow,Pdf,PdfF3,PdfOrganize,DokuStart,OfficeRecovery}.cs`, `src/Trendo.App/{SheetGridView,SlideView,PdfView}.cs`, `src/Trendo.Core/PromptAction/SheetPromptAdapter.cs`, Tests `tests/Trendo.Core.Tests/{TextDocTests,TextProTests,SheetFilesTests,SlideTests,OfficeConvertTests}.cs`, `tests/Trendo.App.Tests/{OfficeRecoveryUiTests,PdfF3UiTests}.cs`, Beleg `reports/t0223-office/`.

### Auffälligkeiten SYS-10
- Abgleichtests mit LibreOffice bestehen leer, wenn LibreOffice fehlt (`TextDocTests.cs:147`, `TextProTests.cs:173`, `:193`, `SheetFilesTests.cs:129`, `SlideTests.cs:117`). LibreOffice wird laut `OfficeConvert.cs:3` nicht mehr mitgeliefert – der Fremdprogramm-Nachweis läuft im Normalfall also nie.
- Doku≠Code: `StudioKnowledge.cs:371` nennt als Werkzeuge „libreoffice, qpdf, tesseract“, `CapabilityCatalog.cs:21` schreibt „LibreOffice rechnet die Datei nicht nach“; die Programme arbeiten ohne LibreOffice und qpdf (`OfficeConvert.cs:3`, `MainWindow.Pdf.cs:16`).
- Zwei XLSX-Schreiber nebeneinander: `SheetFiles.WriteXlsx` (voll) und `Ooxml.NewXlsx` (einfach, von der KI-Aktion benutzt, `SheetPromptAdapter.cs:46`).
- Katalog führt für `text` 48 und für `tabelle` 49 Tests mit Status „PC_VERIFIED“; ein Rohlog-Beleg unter `reports/` liegt nur für Text vor (`reports/t0223-office/text-abgleich/`), für Tabelle und Präsentation: nicht gefunden.

## SYS-09 E-Mails & Termine

Urteilsspalten: post = „E-Mail & Kontakte“, inbox = „Posteingang“, kal = „Kalender“, kont = „Adressbuch“, aufg = „Aufgaben“.

| Nr | Kriterium | post | inbox | kal | kont | aufg | Beleg |
|---|---|---|---|---|---|---|---|
| 1 | Echte Arbeitsfläche | F (reines Formular) | T (3 Spalten Ordner·Liste·Lesen, aber Knopflisten, Klartext) | V (Zeitachse zum Ziehen) | T (Gruppen·Liste·Karte, Bearbeiten per Formular) | V (Tafel mit Ziehen) | src/Trendo.App/MainWindow.Mail.cs:25-61; MainWindow.Pim.cs:167-328; Pim.cs:445 + CalTimeGrid.cs; MainWindow.AddressBook.cs:134-188; Pim.cs:639,680-687 |
| 2 | Werkzeuge real | T (Prüfen, .eml, mailto) | T (Abrufen, Antworten, Weiterleiten, Merken, Regeln nur lokal) | V | V (Doppelte, Nummern, Etiketten-PDF) | V | Mail.cs:33-45; Pim.cs:281-299,329-341; AddressBook.cs:80-91,195 |
| 3 | Neues Projekt/Objekt | T (neuer Entwurf) | T (Neue Nachricht) | V | V | V | Mail.cs:28-32; Pim.cs:187-188; Pim.cs:529-546; AddressBook.cs:78; Pim.cs:602-614 |
| 4 | Echte Dateien importieren | T (.vcf/.eml nur per eingetipptem Pfad) | F (kein .eml/.mbox-Import im Posteingang gefunden) | V (ICS, fremde Regeln über ical.net) | V (vCard, CSV Google/Outlook) | T (nur über ICS-VTODO des Kalenders) | Mail.cs:67-76; Pim.cs:551-552,560-566; AddressBook.cs:80; src/Trendo.Core/Sys/Sys02.cs:405 |
| 5 | Vorhandenes bearbeiten | F (Entwurf nicht wieder ladbar) | T (Entwürfe öffnen; Servermails unveränderlich) | V | V | V | Mail.cs:40; Pim.cs:212; Pim.cs:484-519; AddressBook.cs:147,169-188; Pim.cs:700-720 |
| 6 | Rückgängig | F | F (kein Undo; „Verwerfen“ löscht Entwurf sofort) | V (50 Schritte, Strg+Z) | F (nur Ja/Nein beim Löschen) | V (Strg+Z) | Pim.cs:314; Pim.cs:358,369,422-437; AddressBook.cs:160-163; Pim.cs:576-591,659 |
| 7 | Echte Vorschau | T (EML-Lesekarte, nur Text) | T (nur Text; HTML-Mails werden zu Text, keine Bilder) | V | V (Kontaktkarte, Etikettenbogen) | V | Mail.cs:82-83; src/Trendo.Core/W3/Mail.cs:147 (HtmlToText); Pim.cs:267 |
| 8 | Speichern/Autosave | T (nur beim Klick „Im Mailprogramm öffnen“) | T (Entwurf nur per Knopf; Schreibfeld nur im Speicher) | V (jede Änderung schreibt sofort) | V | V | Mail.cs:40; Pim.cs:311-312, `_sendDraft` Pim.cs:25; Sys02.cs:425-436 (Add nach jeder Änderung) |
| 9 | Wiederherstellung nach Absturz | F | F (ungesicherter Text weg; geladene Mails nicht zwischengespeichert, `Inbox` nur im Speicher Pim.cs:18) | T (Daten bleiben, Undo-Liste nicht) | T | T | Pim.cs:18,25,422 |
| 10 | Export Profi-Format | T (.eml, .vcf) | T (Anhang speichern; kein Export von Mails) | V (.ics mit RRULE/VALARM/VTODO) | V (vCard, CSV, PDF-Etiketten) | T (nur über Kalender-ICS) | Mail.cs:40,78; Pim.cs:276,733; Pim.cs:549-550, Sys02.cs:345-355; AddressBook.cs:81-85,149 |
| 11 | Rundreise geprüft | T (EML schreiben→lesen im Adapter) | F (nichts zum Rundreisen) | V | V | V | MailPromptAdapter.cs:107-110; PimTests „O3/O5: … ICS hin und zurück“; IcsForeignTests „ICS echt: … Python-Referenz“; AddressBookTests.Vcard_roundtrip; AddressBookPlusTests.Csv_roundtrip; TodoBoardUiTests.Core_stage_and_ics |
| 12 | Fachfunktion real | F (sendet nichts, öffnet fremdes Mailprogramm) | T (echtes IMAP über TLS, aber nur INBOX, nur 50 Kopfzeilen, nur lesend; echtes SMTP, aber nur text/plain, kein Anhang, nur AUTH PLAIN, keine Ablage im Server-„Gesendet“) | T (Erinnerung nur solange Trendo läuft; Wiederholung nur täglich/wöchentlich/monatlich/jährlich ohne Ende/Ausnahme; kein CalDAV) | T (kein CardDAV) | T (keine Wiederholung/Erinnerung je Aufgabe gefunden) | Mail.cs:9,27; MailNet.cs:10-49; src/Trendo.Core/W3/MailProtocol.cs:83,90,165,184; Pim.cs:89-90,134,164,305-316; Pim.cs:496-497,524; src/Trendo.App/MainWindow.Sys02.cs:34; Suche „caldav|carddav|pop3“ in src/Trendo.Core ohne Treffer |
| 13 | Claude/KI direkt | T (KI formuliert Text um; Adapter `mail.entwurf` schreibt echte .eml) | B (Adapter `mail.senden` Stufe Blocked, sendet nie; Seiten für Claude als sensibel gesperrt) | T (Claude liest Kalenderstand; kein eigener Adapter) | B (sensibel gesperrt) | nicht geprüft | Mail.cs:46-54; MailPromptAdapter.cs:4-30 (Ready), 33-64 (Blocked); MainWindow.ClaudeBridge.cs:249,264; MainWindow.ClaudeOperate.cs:116-117 |
| 14 | Ende-zu-Ende-Tests | T (W3UiTests „SYS-09 UI: Kontakt anlegen, Entwurf prüfen …“) | NG gegen echten Anbieter: IMAP gegen lokalen Loopback-Server (`ImapStreamOverride`), Senden komplett ersetzt (`SendOverride`); TLS-Weg `MailNet` läuft in keinem Test | V (PimUiTests, CalendarGridUiTests, IcsForeignTests inkl. „echt“-Referenz) | V | V | tests/Trendo.App.Tests/MailProgramUiTests.cs:53,74; PimUiTests.cs:45,55; Teil4BueroUiTests.cs:100; PimTests.Imap_readonly/Smtp_send (Protokoll gegen Stream) |
| 15 | Bedienung | F (keine Kürzel, kein Rechtsklick, kein Drag-and-drop) | T (Tasten N/R/Umschalt+R/F/J/K; kein Rechtsklick, kein Drag-and-drop) | T (Strg+Z, Rechtsklick „Termin löschen“, Ziehen; kein Datei-Drop) | F (nur Enter in der Suche) | T (Strg+Z, Ziehen; kein Rechtsklick) | Pim.cs:300,344,744-760; Pim.cs:369,381,383-389; AddressBook.cs:105; Pim.cs:659; Zählung `DragDrop.|AllowDrop` in Pim/Mail/AddressBook = 0; Vollbild: nicht geprüft |

**Gesamturteil:** TEILWEISE – Kalender, Adressbuch und Aufgaben sind solide lokale Programme; das Mailprogramm ist ein Lese-Fenster mit Klartext-Versand und „E-Mail & Kontakte“ ist ein Formular, das an ein fremdes Mailprogramm abgibt.

**Was fehlt (priorisiert):**
1. Mail: Anhänge senden (SMTP `Compose` baut nur `text/plain`, MailProtocol.cs:184), HTML-Ansicht, mehr als INBOX/50 Kopfzeilen, Ordner des Servers, Lesen/Löschen/Verschieben auf dem Server, Kopie in „Gesendet“ (IMAP APPEND), lokaler Zwischenspeicher.
2. Nachweis gegen einen echten Anbieter (TLS, STARTTLS, App-Passwort) – heute nur Loopback/Fake; moderne Anmeldung (OAuth für Gmail/Outlook) fehlt, nur AUTH PLAIN (MailProtocol.cs:165).
3. Abgleich: CalDAV, CardDAV, POP3 – nicht vorhanden; mehrere Konten nicht vorhanden (ein `konto.json`, Pim.cs:57,71).
4. Seite „post“ auflösen oder ins Mailprogramm überführen (doppelte, schwächere Schreibmaske; Datei nur per Pfad-Eintippen, Mail.cs:67).
5. Kalender: Reihen mit Ende/Ausnahmen/„nur diesen Termin“ nativ bearbeiten (Pim.cs:524), Erinnerung auch bei geschlossenem Trendo, Einladungen (iTIP) – nicht vorhanden.
6. Autosave des Schreibfelds, Rückgängig im Adressbuch/Mail, Rechtsklick-Menüs, Datei-Drop (.eml/.ics/.vcf).

**Abnahmekriterien:**
- Gegen ein echtes Testpostfach (IMAP/SMTP über TLS) werden ≥ 500 Mails in mehreren Ordnern geladen, eine Mail mit 2 Anhängen gesendet und beim Empfänger sowie im Server-Ordner „Gesendet“ wiedergefunden (Beleg unter reports/).
- Eine HTML-Mail mit eingebettetem Bild wird formatiert angezeigt; Als-gelesen/Löschen wirkt auf dem Server und ist nach erneutem Abruf sichtbar.
- Ein Termin, in Trendo geändert, erscheint per CalDAV auf einem zweiten Gerät und umgekehrt; dasselbe für einen Kontakt per CardDAV.
- Trendo wird mitten im Schreiben beendet; nach dem Neustart steht der Text als Entwurf da.
- Eine Reihe „wöchentlich bis 31.12., ohne 24.12.“ lässt sich in Trendo anlegen, ein Einzeltermin verschieben, als ICS sichern und unverändert wieder einlesen.

**Aufwand/Risiko:** groß. Risiko hoch bei Mail (Anbieter-Eigenheiten, OAuth, Sicherheit beim HTML-Anzeigen, Datenverlust bei schreibendem IMAP); mittel bei CalDAV/CardDAV; klein bei Bedienung/Autosave.

**Dateien:** src/Trendo.App/MainWindow.Mail.cs, MainWindow.Pim.cs, MailNet.cs, MainWindow.AddressBook.cs, CalTimeGrid.cs, MainWindow.TaskCards.cs; src/Trendo.Core/W3/Mail.cs, MailProtocol.cs, MailPlus.cs, MailDates.cs, AddressBook.cs, AddressBookPlus.cs; src/Trendo.Core/Sys/Sys02.cs (Kalender/ICS ab Z. 316), IcsForeign.cs; src/Trendo.Core/PromptAction/MailPromptAdapter.cs; Tests: PimTests, MailPlusTests, IcsForeignTests, AddressBook(Plus)Tests, PimUiTests, MailProgramUiTests, CalendarGridUiTests, TodoBoardUiTests, AddressBookUiTests.

---

## SYS-02 Kleine Helfer & Musik

Urteilsspalten: start = „Übersicht“ (alltag-start mit Reitern Dateien, Bilder & Medien, Zeit & Kalender, Kleine Helfer, Notizbuch, Editor, Rechner, Uhr & Wecker, Zettel), werk = „Werkzeuge: Notizen, Rechner, Wecker“, med = „Medien“ (Fotos · Musik & Videos).

| Nr | Kriterium | start | werk | med | Beleg |
|---|---|---|---|---|---|
| 1 | Echte Arbeitsfläche | T (Kachel-Startseite; Reiter „Bilder & Medien“ = Formularzeilen mit Pfadfeldern; Editor/Notizbuch echte Schreibflächen) | T (Notizkarten nur anlegen/löschen, nicht bearbeitbar; Rechner mit Tastenfeld; Wecker = 4 feste Zeiten) | T (Fotogalerie + Player-Leiste; kein eingebetteter Videoplayer) | src/Trendo.App/MainWindow.Helfer.cs:43-53; MainWindow.Sys02.cs:158-221; MainWindow.W1Apps.cs:80-97,141-156; MainWindow.MusicPlayer.cs:109,190-191 |
| 2 | Werkzeuge real | V (Drehen, Verkleinern, Schwärzen, Umbenennen, Umwandeln ausgeführt) | T | T | Sys02UiTests „SYS-02-P06/P10/P18 …“; HelferUiTests N9/N11/N20/N18/N5; W1Apps.cs:104-139 |
| 3 | Neu anlegen | V (Notizbuchseite, Zettel, Wecker) | T (neue Notiz) | T (Album, Wiedergabeliste) | HelferUiTests N5/N18/N20; W1Apps.cs:82; MainWindow.Fotoalbum.cs:89-92; MusicPlayerUiTests „… Liste anlegen und füllen“ |
| 4 | Echte Dateien importieren | T (Editor öffnet Dateien; Bildwerkzeuge über Pfadfelder) | F | T (liest Ordner ein; kein Datei-Drop) | Helfer.cs:85; Sys02.cs:180-193; Fotoalbum.cs:95-101; MusicPlayer.cs:36-41 |
| 5 | Vorhandenes bearbeiten | V (Editor, Notizbuch) | F (Haftnotiz nur löschbar, `TextBlock`) | T (Foto geht an die Bildbearbeitung; Musik-Metadaten nicht bearbeitbar – nicht gefunden) | W1Apps.cs:90-96; Fotoalbum.cs:203 |
| 6 | Rückgängig | T (Umbenennen mit Rückgängig; sonst nur Textfeld-eigenes) | F | T (Papierkorb statt Undo) | Sys02UiTests „… Umbenennen mit Vorschau + Rückgängig“; Fotoalbum.cs:205-217 |
| 7 | Echte Vorschau | T | T (Ergebnis/Verlauf) | T (Foto groß; Ton hörbar nur mit mpv; Video in fremdem Fenster) | Fotoalbum.cs:193,224; MusicPlayer.cs:66,190; src/Trendo.Core/Sys/Sys02.cs:98-99 |
| 8 | Speichern/Autosave | T (Notizbuch speichert beim Schreiben/Strg+S; Editor: nicht geprüft) | T (Notiz sofort gespeichert; Rechner-Verlauf nur im Speicher, `_calcHist`) | T (Alben/Listen gespeichert) | Helfer.cs:701; HelferUiTests N5 „schreiben (speichert)“; W1Apps.cs:70,82 |
| 9 | Wiederherstellung nach Absturz | nicht geprüft (kein Wiederherstellungs-Code in Helfer/Sys02 gefunden) | F (laufender Kurzzeitwecker nur als `DispatcherTimer` im Speicher) | T (Bibliothek wird neu eingelesen; Wiedergabestand weg) | W1Apps.cs:161-177 |
| 10 | Export Profi-Format | T (PNG-Bildschirmfoto, MP4-Aufnahme, MP3/OGG/WAV, PDF aus Notizbuch) | F | F (kein Export von Listen, z. B. M3U – nicht gefunden) | Sys02.cs:186-212,223; Helfer.cs:558 |
| 11 | Rundreise geprüft | T (Editor: gleiche Kodierung zurück) | F | F | HelferUiTests „N9: Editor öffnet ANSI/CRLF … speichert in gleicher Kodierung“ |
| 12 | Fachfunktion real | T – Bildschirmfoto: „Trendo-Fenster“ ist nur ein Abbild des eigenen Fensters (`RenderTargetBitmap`), ganzer Bildschirm nur mit ffmpeg (gdigrab/x11grab); kein Bereichs-/Fensterwahl-Werkzeug; Schwärzen per eingetippten Koordinaten | T – Wecker ohne freie Zeit und ohne Ton (nur Fenster nach vorn); „Wecker“ mit Uhrzeit liegt im Reiter „Uhr & Wecker“ und klingelt nur mit mpv | T – Musik real über mpv-Hintergrundprozess mit JSON-IPC, aber mpv muss nachinstalliert sein und die Steuerung nutzt eine Windows-Pipe (`\\.\pipe\…`) → nur Windows; Video = externes mpv/VLC-Fenster oder Windows-Standardprogramm | src/Trendo.App/MainWindow.axaml.cs:337-344; Sys02.cs:185-193; Trendo.Core/Sys/Sys02.cs:49; W1Apps.cs:147-177; Helfer.cs:387-405; MpvAudio.cs:23,31-32; MusicPlayer.cs:45-56,316,327; Sys02.cs:225-229 |
| 13 | Claude/KI direkt | T | T (Adapter `helfer.rechnen`, Stufe Partial: rechnet wirklich, legt echte Notiz an, schreibt ergebnis.txt) | F (Katalog: „Musik abspielen und Dateien verschieben sind nicht angeschlossen“; Adapter-Warnung „Musik bleibt aus“) | src/Trendo.Core/PromptAction/MeasurePromptAdapter.cs:8-31,322-339; CapabilityCatalog.cs:24 |
| 14 | Ende-zu-Ende-Tests | V für Bild-/Dateiwerkzeuge (echte Dateien) ; Bildschirmfoto ganzer Bildschirm/Aufnahme: NG (kein Test gefunden) | T (Rechner/Wecker in HelferUiTests; Seite „werkzeuge“ selbst: nicht geprüft) | T – Player-UI mit `FakeAudio` ersetzt; echter mpv-Test existiert, überspringt sich aber ohne mpv; realer Lauf: nicht geprüft | Sys02UiTests; tests/Trendo.App.Tests/MusicPlayerUiTests.cs:43; MpvAudioTests.cs:18,21 („mpv (echt)“ / Skip); FotoalbumUiTests „T-0302 …“ |
| 15 | Bedienung | T (Enter/Esc/Strg+S in einzelnen Reitern) | F (nur Enter im Rechner) | T (Fotos: ← → Esc, Strg-Klick, Doppelklick; Musik: keine Medientasten/Leertaste gefunden) | Helfer.cs:165,200,257,701; W1Apps.cs:112; Fotoalbum.cs:169,226; Rechtsklick- und Drag-and-drop-Zählung in W1Apps/MusicPlayer/Fotoalbum/Helfer/Sys02 = 0; Vollbild: nicht geprüft |

**Gesamturteil:** TEILWEISE – viele kleine Helfer funktionieren wirklich, aber das Versprechen „Musik/Video abspielen“ hängt an einem nachzuladenden Fremdprogramm, und die Seite „Werkzeuge“ ist die schwächere Doppelung der Alltag-Reiter.

**Was fehlt (priorisiert):**
1. Eigene, mitgelieferte Tonausgabe (oder mpv fest im Paket) und eingebettete Videowiedergabe im Trendo-Fenster; Wiedergabe plattformneutral statt Windows-Pipe.
2. Echtes Bildschirmfoto-Werkzeug: Bereich/Fenster/Bildschirm wählen ohne ffmpeg, Schwärzen mit der Maus statt Koordinaten.
3. „Werkzeuge“: Haftnotiz bearbeiten, Wecker mit freier Zeit und Ton, Verlauf speichern – oder Seite mit den Alltag-Reitern zusammenlegen (heute zwei Rechner, zwei Wecker, zwei Notizarten).
4. Tastatur (Leertaste/Medientasten), Rechtsklick, Dateien per Drag-and-drop in Player und Galerie.
5. Wiedergabelisten exportieren/importieren (M3U), Titelangaben bearbeiten.
6. KI-Aktion für Medien (abspielen, Liste füllen) – heute ausdrücklich nicht angeschlossen.

**Abnahmekriterien:**
- Auf einem frisch installierten PC ohne Zusatzprogramme spielt ein MP3 hörbar ab, Fortschritt und Titelende stimmen (Test ohne `AudioOverride`, Beleg unter reports/).
- Ein MP4 läuft sichtbar innerhalb des Trendo-Fensters mit Pause, Springen und Vollbild.
- „Bildschirmfoto“ nimmt ohne ffmpeg einen mit der Maus gezogenen Bereich des Desktops auf; die PNG enthält Pixel fremder Fenster.
- Ein Kurzzeitwecker über 7 Minuten lässt sich frei einstellen und meldet sich mit Ton, auch wenn Trendo minimiert ist.

**Aufwand/Risiko:** mittel. Risiko mittel (Audio-/Video-Bibliothek, Lizenz, Plattformunterschiede); gering bei Werkzeuge/Bedienung.

**Dateien:** src/Trendo.App/MainWindow.Sys02.cs, MainWindow.Helfer.cs, MainWindow.W1Apps.cs (ToolsView Z. 74, MediaView Z. 181), MainWindow.MusicPlayer.cs, MpvAudio.cs, MainWindow.Fotoalbum.cs, MainWindow.axaml.cs:337; src/Trendo.Core/Sys/Sys02.cs; src/Trendo.Core/PromptAction/MeasurePromptAdapter.cs. Hinweis: MainWindow.MediaBrowser.cs / MediaPro.cs / ClaudeMedia.cs gehören zu „Freie Bilder & Klänge“, nicht zur Seite „medien“.

---

## STUDIO-LETTER-01 Briefe schreiben

| Nr | Kriterium | Urteil (brief) | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | TEILWEISE – Assistent in 3 Schritten mit Textfeldern links, maßstäbliche DIN-A4-Vorschau rechts; geschrieben wird im Formular, nicht auf der Seite; Brieftext ist ein Klartextfeld (keine Auszeichnung) | src/Trendo.App/MainWindow.W1Office.cs:140,156-158,196-199,229-232; MainWindow.LetterPro.cs:414-437 |
| 2 | Werkzeuge real | VOLLWERTIG – Vorlagen, Fristenrechner, Serienbrief, Unterschrift zeichnen, Rechtschreibung (nur mit LibreOffice-Wörterbuch), Briefart/Pflichtangaben, Postausgang | LetterPro.cs:34,80-95,103-124,191-216,278-291; MainWindow.LetterOffice.cs:30-65; LetterProTests F1–F4; LetterOfficeTests |
| 3 | Neues Projekt | VOLLWERTIG | W1Office.cs:137-138, „Neuer Brief“ W1Office.cs:327 |
| 4 | Echte Dateien importieren | TEILWEISE – CSV für Serienbrief, Bild für Unterschrift/Foto, Eingangspost in Akten; eine fremde DOCX/ODT lässt sich nicht als Brief öffnen | LetterPro.cs:103,200-201; MainWindow.W1Letter.cs:178,216; src/Trendo.Core/Letters.cs:458-462 |
| 5 | Vorhandenes bearbeiten | TEILWEISE – „Weiterschreiben“ nur für Trendos eigene Briefe über die Begleitdatei `.trendo-brief.json`; ohne sie deaktiviert | W1Office.cs:338-347; Letters.cs:458-462 |
| 6 | Rückgängig | TEILWEISE – nur „Alles leeren → Rückgängig“ (Hinweis) und Papierkorb; kein Schritt-Verlauf | W1Office.cs:174-181; W1Letter.cs:356-381 |
| 7 | Echte Vorschau | VOLLWERTIG – Seiten mit Sichtfenster, Falz-/Lochmarken, Folgeseiten, Serienbrief-Blättern | LetterPro.cs:414-437,428-433,509-533; ClaudeLetterUiTests „Brief-Design im Programm …“ |
| 8 | Speichern/Autosave | TEILWEISE – Speichern nur per Knopf ins Archiv (DOCX + Begleitdatei, versioniert); kein Autosave gefunden (Suche „Autosave|AutoSave“ in W1Office/LetterPro/W1Letter ohne Treffer); Entwurf `_letter` liegt nur im Speicher | W1Office.cs:134,265,293 |
| 9 | Wiederherstellung nach Absturz | FEHLT – kein Mechanismus gefunden; ungespeicherter Brief ist weg | W1Office.cs:134 (`_letter ??= NewLetter()`) |
| 10 | Export Profi-Format | VOLLWERTIG – DOCX (eigener Schreiber), PDF nach DIN 5008 ohne LibreOffice (eigener Schreiber), ODT in der Akte, Export mit Ort/Format, E-Mail-Entwurf mit PDF-Anhang | Letters.cs:299; src/Trendo.Core/LetterPdf.cs:15; W1Letter.cs:282-288; W1Office.cs:303,309,313; LetterSendUiTests (3 Tests) |
| 11 | Rundreise geprüft | TEILWEISE – Text wird aus der DOCX zurückgelesen und der Brief über die Begleitdatei wieder geöffnet; PDF auf Gültigkeit geprüft; Öffnen in Word/LibreOffice: nicht geprüft | LetterPromptAdapter.cs:66-71; LetterDesignPdfTests „… jede der 20 Vorlagen ergibt ein gültiges PDF“; LetterF3Tests (Marken in der DOCX) |
| 12 | Fachfunktion real | VOLLWERTIG mit Abstrichen – DIN-5008-Maße (Form A/B) umgesetzt und in DOCX gemessen; Frist mit Wochenend-Verschiebung; Serienbrief erzeugt je Zeile eine DOCX (kein Sammel-PDF/Sammeldruck gefunden) | LetterDinTests „T-0310: DOCX – Form A …“; Letters.cs:160,185,279; LetterPro.cs:124,169-179; LetterProUiTests „F3/F4: Serienbrief aus CSV (ein DOCX je Zeile)“ |
| 13 | Claude/KI direkt | VOLLWERTIG – Adapter `brief.schreiben` (Stufe Partial) schreibt echte DOCX + Begleitdatei und prüft sie; Claude ändert `Brief.json`, Text und Design erscheinen im offenen Brief; lokale KI verbessert Text | LetterPromptAdapter.cs:6-16,55-84; src/Trendo.App/MainWindow.ClaudeLetter.cs:14,119-134; ClaudeLetterUiTests „Claude im Brief-Studio …“; W1Office.cs:245 |
| 14 | Ende-zu-Ende-Tests | TEILWEISE – Kern- und UI-Tests schreiben echte DOCX/PDF; in LetterSendUiTests/ClaudeLetterUiTests sind Mailprogramm/Claude ersetzt (`Override`, LetterSendUiTests.cs:61, ClaudeLetterUiTests.cs:52); echter Druck: nicht geprüft | LetterSendUiTests „Briefe: „Als PDF“ ohne LibreOffice …“; LetterDesignPdfTests |
| 15 | Bedienung | TEILWEISE – Assistent „einfach“ / „Alles auf einer Seite“; keine Tastenkürzel (kein `KeyDown` außer Farbfeld), kein Rechtsklick, kein Drag-and-drop | W1Office.cs:171-172; ClaudeLetter.cs:77; Zählung `ContextMenu`/`DragDrop` in W1Office/LetterPro/W1Letter = 0; Vollbild: nicht geprüft |

**Gesamturteil:** TEILWEISE – fachlich stark (DIN 5008, eigenes PDF/DOCX, Fristen, Serienbrief, Akten), aber als Formular-Assistent ohne Autosave, ohne Schritt-Rückgängig und ohne Öffnen fremder Briefe kein vollwertiges Schreibprogramm.

**Was fehlt (priorisiert):**
1. Autosave und Wiederherstellung des laufenden Briefs.
2. Schreiben direkt auf der Briefseite mit Auszeichnung (fett, Aufzählung, Absatzabstand) statt Klartextfeld.
3. Rückgängig/Wiederholen als Verlauf, Strg+S/Strg+P/Strg+Z.
4. Fremde DOCX/ODT als Brief öffnen (heute nur mit Trendos Begleitdatei).
5. Serienbrief als ein Sammel-PDF bzw. Sammeldruck; Feldliste per Klick einfügen.
6. Rechtschreibung ohne LibreOffice-Abhängigkeit (LetterPro.cs:287).

**Abnahmekriterien:**
- Trendo wird beim Schreiben hart beendet; nach dem Start steht der Brief mit allen Feldern wieder da.
- Die erzeugte PDF (Form A und B) wird ausgemessen: Anschriftfeld, Informationsblock, Betreff und Falzmarken liegen innerhalb ±0,5 mm der DIN-Maße; die DOCX öffnet sich in Word und LibreOffice ohne Reparaturhinweis.
- Ein in Word geschriebener Brief (.docx) lässt sich öffnen, ändern und wieder speichern.
- Serienbrief mit 50 Empfängern ergibt eine PDF mit 50 Briefen in der Reihenfolge der Liste.
- Fett gesetzter Text und eine Aufzählung erscheinen in Vorschau, PDF und DOCX gleich.

**Aufwand/Risiko:** mittel (Autosave/Undo klein; Schreiben auf der Seite mit Auszeichnung und DOCX-Import groß). Risiko mittel: Vorschau, PDF- und DOCX-Schreiber müssen bei Auszeichnung gleich umbrechen.

**Dateien:** src/Trendo.App/MainWindow.W1Office.cs (LetterView Z. 140), MainWindow.LetterPro.cs, MainWindow.LetterOffice.cs, MainWindow.W1Letter.cs, MainWindow.ClaudeLetter.cs, MainWindow.DinBrief.cs (typst-Brief, laut Kopfkommentar SYS-10); src/Trendo.Core/Letters.cs, LetterPdf.cs, LetterCase.cs; src/Trendo.Core/PromptAction/LetterPromptAdapter.cs.

---

## X-11 Neues Projekt starten

| Nr | Kriterium | Urteil (vorhaben) | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | TEILWEISE – Gantt mit Ziehen ist eine echte Fläche; Tafel verschiebt Karten nur über ←/→-Knöpfe; Rest Listen/Felder | src/Trendo.App/GanttCanvas.cs:10; MainWindow.W4Suite.cs:298-320,254-270 (Z. 268-269) |
| 2 | Werkzeuge real | VOLLWERTIG – Balken/Kanten ziehen, Abhängigkeiten mit Nachrücken, kritischer Pfad, Meilensteine | GanttUiTests „T-0308: Gantt – …“; CreationPlanTests „T-0308: Balken, Verschieben mit Nachrücken …“ |
| 3 | Neues Projekt | VOLLWERTIG – Ziel wählen, Ordner + Schritte + Zeitplan entstehen | W4Suite.cs:94-136 (Z. 124) |
| 4 | Echte Dateien importieren | TEILWEISE – Dateien werden ins Vorhaben kopiert; kein Import fremder Pläne (CSV, MS Project, ICS) gefunden | W4Suite.cs:375-394 |
| 5 | Vorhandenes bearbeiten | VOLLWERTIG | W4Suite.cs:48-54,156-163; CreationMappeTests „… Umbenennen verschiebt den Ordner …“ |
| 6 | Rückgängig | TEILWEISE – nur im Zeitplan (Dateistand-Stapel, Knopf); Tafel/Liste ohne | W4Suite.cs:283,293,316-317 |
| 7 | Echte Vorschau | VOLLWERTIG – Übersicht mit Kennzahlen, Fortschrittsring, Mini-Zeitplan | W4Suite.cs:176-234 |
| 8 | Speichern/Autosave | VOLLWERTIG – jede Änderung schreibt sofort in `SCHRITTE.md` im Projektordner | src/Trendo.Core/CreationSuite.cs:14; CreationPlanTests „T-0308: Format – …“ |
| 9 | Wiederherstellung nach Absturz | TEILWEISE – Daten bleiben durch Sofort-Schreiben; kein eigener Absturzschutz, Undo-Stapel nur im Speicher | W4Suite.cs:283 |
| 10 | Export Profi-Format | FEHLT – kein Export des Plans (PDF, CSV, Bild, Projektdatei) gefunden; nur „In den Kalender“ | Suche „Export|\.csv|\.pdf|\.xml“ in W4Suite.cs/GanttCanvas.cs ohne Treffer; W4Suite.cs:34-41,217 |
| 11 | Rundreise geprüft | TEILWEISE – nur das eigene Textformat hin und zurück | CreationPlanTests „Format – altes „@Ende“ bleibt …“ |
| 12 | Fachfunktion real | TEILWEISE – Planung real, aber ohne Zuständige, Aufwand/Ressourcen, Unteraufgaben (`Step` kennt nur Text, Zustand, Beginn/Ende, Meilenstein, Vorgänger) | CreationSuite.cs:10; src/Trendo.Core/CreationPlan.cs:4 |
| 13 | Claude/KI direkt | TEILWEISE – Adapter `projekt.anlegen` (Stufe Partial) legt einen Eintrag im `ProjectStore` an und schreibt eine .txt; er erzeugt KEIN Vorhaben mit Schritten/Zeitplan im Ordner des Programms | src/Trendo.Core/PromptAction/MeasurePromptAdapter.cs:34-57,374-394; W4Suite.cs:15,124; CapabilityCatalog.cs:23 |
| 14 | Ende-zu-Ende-Tests | VOLLWERTIG für das Vorhandene – UI-Tests ohne ersetzte Fremdwerkzeuge (keine nötig) | GanttUiTests; VorhabenDemoUiTests „Vorhaben: „Beispiel ansehen“ …“; CreationPlanTests „Fälligkeiten in den Kalender …“ |
| 15 | Bedienung | TEILWEISE – Maus-Ziehen im Gantt, Zoom Tage/Wochen; keine Tastenkürzel (kein `KeyDown` in W4Suite.cs/GanttCanvas.cs), kein Rechtsklick, kein Datei-Drop | W4Suite.cs:320; Zählungen = 0; Vollbild: nicht geprüft |

**Gesamturteil:** TEILWEISE – ein brauchbarer persönlicher Planer mit echtem Gantt, aber ohne Export, ohne Zuständige/Ressourcen und mit einer KI-Aktion, die am Programm vorbeigeht.

**Was fehlt (priorisiert):**
1. Export: Zeitplan als PDF/Bild, Schritte als CSV/XLSX, Termine als ICS-Datei.
2. KI-Aktion an das Programm anschließen: aus einem Satz ein Vorhaben mit Schritten, Terminen und Abhängigkeiten anlegen (heute nur ProjectStore-Eintrag).
3. Zuständige, Aufwand, Unteraufgaben, Notizen je Schritt.
4. Tafel: Karten ziehen; Rückgängig auch in Liste und Tafel; Tastenkürzel.
5. Import fremder Pläne (CSV, ggf. MS-Project-XML).

**Abnahmekriterien:**
- Aus einem Vorhaben mit 12 Schritten und 3 Abhängigkeiten entsteht eine PDF des Zeitplans und eine CSV; die CSV lässt sich in ein neues Vorhaben einlesen und ergibt denselben Plan.
- Der Satz „Plane meinen Umzug bis 1. März“ über die KI-Leiste ergibt ein Vorhaben, das im Programm unter „Vorhaben“ mit Schritten und Zeitplan erscheint.
- Eine Karte lässt sich mit der Maus von „Offen“ nach „In Arbeit“ ziehen und mit Strg+Z zurücknehmen.
- Ein Schritt mit Zuständigem und Aufwand erscheint so in Liste, Tafel und Export.

**Aufwand/Risiko:** mittel; Risiko gering (reine Eigenlogik, Textformat `SCHRITTE.md` muss abwärtsverträglich erweitert werden).

**Dateien:** src/Trendo.App/MainWindow.W4Suite.cs, GanttCanvas.cs, MainWindow.Projektmappe.cs (Projektmappe; Zuordnung zur Seite „vorhaben“ nicht geprüft), MainWindow.W5Ext.cs:85; src/Trendo.Core/CreationSuite.cs, CreationPlan.cs, CreationMappe.cs; src/Trendo.Core/PromptAction/MeasurePromptAdapter.cs.

---


## Gemeinsame Hülle (gilt für alle acht, einmal belegt)

Alle acht Programme sind über `RegisterProgram(FachTabs, new ProgramSpec(...))` angemeldet: App/MainWindow.Bewerbung.cs:96, App/MainWindow.Karten.cs:35, App/MainWindow.Vertraege.cs:33, App/MainWindow.PdfWerkstatt.cs:62, App/MainWindow.Formulare.cs:45, App/MainWindow.DeckDesign.cs:47, App/MainWindow.DocScan.cs:43, App/MainWindow.KreativStudios4.cs:1238.

- Rückgängig/Wiederholen: Schnappschüsse je Änderung, höchstens 100 – Core/Programs/ProgramDocument.cs:75-99; Knöpfe App/MainWindow.ProgramShell.cs:615-616; Test CT/ProgramDocumentTests.cs:12, AT/ProgramShellUiTests.cs:15.
- Speichern: erst `.tmp`, dann verschieben – ProgramDocument.cs:139-148. Automatisch: 30-Sekunden-Takt und beim Schließen (ProgramShell.cs:83-88), 1,5 s nach der letzten Änderung (ProgramShell.cs:228); Test AT/EinfachBedienenUiTests.cs:78.
- Wiederherstellung nach Absturz: Sicherungsdatei neben der Datei bzw. im Sicherungsordner (ProgramDocument.cs:218-235), Karte „Wiederherstellen“ auf der Startseite (ProgramShell.cs:517-526); Test AT/ProgramShellUiTests.cs:56. Getestet ist der Weg „ungespeichert → Startseite“, kein echter Prozessabbruch (nicht geprüft).
- Versionen: bis 20 Stände, zurückholbar als Bearbeitungsschritt – ProgramDocument.cs:151-200, ProgramShell.cs:593-598; Test AT/ProgramShellUiTests.cs:71.
- Tastenkürzel: nur Strg+S/Z/Y/N/W – ProgramShell.cs:343-352. Bedienstufen Einfach/Erweitert/Profi – ProgramShell.cs:627, 639; Tests AT/ProgramLevelUiTests.cs:17, 49.
- Rechtsklick-Menü und Ziehen-und-Ablegen: in der Hülle und in allen acht Programmdateien kein Treffer für `ContextMenu`, `ContextFlyout`, `AllowDrop`, `DragDrop` (Grep). Vollbild: `WindowState.FullScreen` nur im Einrichtungsablauf (App/MainWindow.axaml.cs:173, App/MainWindow.InstallOverlay.cs:169) – für Programme nicht belegt.
- Claude im Programm: allgemeiner Weg über das Projektdokument (`ReplaceData`, ein Rückgängig-Schritt, Banner „vergleichen/behalten/verwerfen“) – App/MainWindow.ClaudeProgramDoc.cs:13-40, App/MainWindow.ClaudeLive.cs:33-55, ProgramShell.cs:340. Für keines der acht Studios gibt es einen eigenen Test dieses Wegs → je Studio „nicht geprüft“.
- Einschränkung für alle Programme mit Fremddateien: die `.tdoc` speichert nur Pfade, nicht die Dateien selbst (PdfWerkstatt.cs:136, Formulare.cs:121, DocScan `ScanPage.Src` DocScan.cs:84, Bewerbung.cs:229, Vertraege.cs:106). Wird die Quelle verschoben, ist das Projekt unvollständig.

## STUDIO-CV-01 Bewerbung & Lebenslauf

| Nr | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | TEILWEISE | Eingabefelder links (App/MainWindow.Bewerbung.cs:303-375), daneben nachgebaute A4-Vorschau (:384-385); keine Bearbeitung im Blatt |
| 2 | Werkzeuge real | VOLLWERTIG | Prüfen, Stationen, Entwurf, Mappe (Bewerbung.cs:110-128); Test `Cv_editor` AT/BueroStudiosUiTests.cs:51 |
| 3 | Neues Projekt | VOLLWERTIG | drei Vorlagen Bewerbung.cs:102-107 |
| 4 | Echte Dateien importieren | TEILWEISE | nur Foto und Anlagen (Bewerbung.cs:213-231); kein Import eines vorhandenen Lebenslaufs (docs/claude/vorbilder/bewerbung-reactive-resume-europass.md:19) |
| 5 | Vorhandene Projekte bearbeiten | VOLLWERTIG | Hülle „Zuletzt/Öffnen“ ProgramShell.cs:499-512 |
| 6 | Rückgängig | VOLLWERTIG | `d.Undo()` nach Station löschen, AT/BueroStudiosUiTests.cs:69-70 |
| 7 | Echte Vorschau | TEILWEISE | Vorschau ist ein eigener Nachbau aus Steuerelementen, nicht das gerenderte PDF (Bewerbung.cs:384-390) |
| 8 | Speichern/Autosave | VOLLWERTIG | Hülle (siehe oben) |
| 9 | Wiederherstellung | VOLLWERTIG | Hülle; kein studioeigener Test |
| 10 | Export Profi-Formate | VOLLWERTIG | PDF, DOCX, TXT (ATS), Anschreiben-PDF, Mappe-PDF, CSV – Bewerbung.cs:132-137 |
| 11 | Export wieder öffnen | TEILWEISE | DOCX wird zurückgelesen und Text geprüft (CT/BueroTests.cs:107-109); PDF nur Seitenzahl und A4-Größe (CT/BueroTests.cs:97-99), Inhalt des PDF nicht geprüft |
| 12 | Fachfunktion real | VOLLWERTIG | `CvPdf.Write` Core/Buero/Bewerbung.cs:442, Mappe aus Deckblatt+Anschreiben+Lebenslauf+Anlagen Core/Buero/Bewerbung.cs:686; Test `Cv_mappe` CT/BueroTests.cs:138 |
| 13 | Claude/KI | TEILWEISE | Aktion „Lebenslauf prüfen“ liefert nur `lebenslauf.txt` (Core/PromptAction/BueroPromptAdapters.cs:14, 20, 88), Stufe Partial (CT/BueroTests.cs:191). Im Programm schreibt die KI das Anschreiben ins Dokument (Bewerbung.cs:171-198) |
| 14 | Ende-zu-Ende-Tests | TEILWEISE | PDF/DOCX/Mappe echt (PDFsharp); KI im Test ersetzt (`CvAiOverride`, AT/BueroStudiosUiTests.cs:104) – echter KI-Lauf NICHT GETESTET |
| 15 | Bedienung | TEILWEISE | Stufen und Strg-Kürzel aus der Hülle; Akzentfarbe nur sechs Felder (Bewerbung.cs:548); kein Rechtsklick, kein Ziehen-und-Ablegen (Grep ohne Treffer) |

**Gesamturteil:** TEILWEISE – echte Dateien (PDF, Word, Mappe) entstehen und sind getestet, aber die Oberfläche ist ein Formular mit nachgebauter Vorschau und ohne Import.

**Was fehlt**
1. Vorschau aus dem tatsächlich erzeugten PDF statt Nachbau (Abweichungen Vorschau ↔ PDF sind heute nicht prüfbar).
2. Import vorhandener Lebensläufe (DOCX/PDF/JSON Resume).
3. Abschnitte frei anordnen, eigene Abschnitte (Vorbilder-Liste Zeile 18).
4. KI-Aktion, die PDF/DOCX ins Projekt legt statt nur TXT.
5. Inhaltsprüfung des PDF im Test (Text zurücklesen), Test mit echter KI.
6. Anlagen in den Projektordner kopieren statt nur Pfad.

**Abnahmekriterien**
- Die Vorschau zeigt Seite für Seite das gerenderte PDF; Seitenzahl in Vorschau und Export stimmen in einem Test überein.
- Ein vorhandener DOCX-Lebenslauf wird eingelesen und ergibt mindestens Name, Stationen und Ausbildung.
- Ein Test liest den Text des exportierten PDF zurück (Name, eine Station).
- Die KI-Aktion hinterlässt `lebenslauf.pdf` im Projekt und meldet die Seitenzahl.

**Aufwand/Risiko:** mittel / gering (Kern steht; PDF-Vorschau über vorhandenes PDFium möglich).

**Dateien:** App/MainWindow.Bewerbung.cs, Core/Buero/Bewerbung.cs, Core/PromptAction/BueroPromptAdapters.cs, CT/BueroTests.cs, AT/BueroStudiosUiTests.cs

## STUDIO-CARDS-01 Karten & Einladungen

| Nr | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | TEILWEISE | Kartenbild als Canvas, Text anklickbar (App/MainWindow.Karten.cs:163-188), aber Lage nur senkrecht per Regler (Karten.cs:274), waagrecht fest (Karten.cs:188); kein Ziehen, kein Skalieren |
| 2 | Werkzeuge real | VOLLWERTIG | Text, Bild, Prüfen, Gäste, Druck (Karten.cs:50-59); Test `Cards` AT/BueroStudiosUiTests.cs:143 |
| 3 | Neues Projekt | VOLLWERTIG | Vorlagen Karten.cs:41; alle erzeugen eine Karte (AT/BueroStudiosUiTests.cs:46) |
| 4 | Echte Dateien importieren | TEILWEISE | Bild JPG/PNG (Karten.cs:99), Gästeliste CSV (Karten.cs:114-118); keine Schmuckelemente, kein SVG/PDF |
| 5 | Vorhandene Projekte bearbeiten | VOLLWERTIG | Hülle |
| 6 | Rückgängig | VOLLWERTIG | Formatwechsel zurück, AT/BueroStudiosUiTests.cs:182 |
| 7 | Echte Vorschau | TEILWEISE | Canvas-Nachbau mit eingesetzten Platzhaltern (AT/BueroStudiosUiTests.cs:155); nicht das Druck-PDF, Beschnitt/Marken in der Vorschau nicht belegt |
| 8 | Speichern/Autosave | VOLLWERTIG | Hülle |
| 9 | Wiederherstellung | VOLLWERTIG | Hülle; kein studioeigener Test |
| 10 | Export Profi-Formate | TEILWEISE | Druck-PDF mit Beschnitt, Schnitt- und Falzmarken, TrimBox/BleedBox (Core/Buero/Karten.cs:383-384, 459-478). Kein CMYK/PDF-X (Grep `CMYK|PDF/X|OutputIntent` in Karten.cs: 0 Treffer); kein PNG/JPG-Export (Karten.cs:64-66) |
| 11 | Export wieder öffnen | VOLLWERTIG | PDF mit PdfReader geöffnet, Seitengröße, TrimBox und BleedBox gemessen – `Cards_pdf` CT/BueroTests.cs:291-297 |
| 12 | Fachfunktion real | VOLLWERTIG | `KartenPdf.Write`/`WriteSerie` Core/Buero/Karten.cs:367-368; Serienkarten 4 Seiten AT/BueroStudiosUiTests.cs:174 |
| 13 | Claude/KI | TEILWEISE | Aktion heißt „Karte anlegen“ (BueroPromptAdapters.cs:98), erzeugt aber nur `karte.txt` (:104, :146) – kein Kartendokument, kein PDF |
| 14 | Ende-zu-Ende-Tests | VOLLWERTIG | keine ersetzten Werkzeuge; PDF echt geschrieben und gelesen (CT/BueroTests.cs:279-313) |
| 15 | Bedienung | TEILWEISE | Schriftfarbe und Hintergrund als `#RRGGBB`-Textfeld (Karten.cs:267, 283); kein Rechtsklick, kein Ziehen-und-Ablegen |

**Gesamturteil:** TEILWEISE – das Druck-PDF ist echt und sauber geprüft, aber gestaltet wird über Felder und einen Höhenregler statt frei auf der Karte.

**Was fehlt**
1. Freies Verschieben, Skalieren und Drehen von Text und Bild mit der Maus.
2. Farbwähler statt Hex-Eingabe; mehrere Bilder je Seite, Rahmen und Schmuck.
3. Vorschau mit Beschnitt- und Sicherheitsrand, auf Wunsch das echte PDF.
4. KI-Aktion, die eine Karte (tdoc) und das Druck-PDF anlegt.
5. Bild-Export (PNG/JPG) zum Verschicken; Umschläge.
6. Druckerei-Profil (CMYK/PDF-X) – nur falls gewünscht.

**Abnahmekriterien**
- Ein Text lässt sich mit der Maus an eine beliebige Stelle ziehen; die Lage steht danach im PDF an derselben Stelle (Test misst Koordinaten).
- Farbe wird über einen Farbwähler gesetzt, kein Hex-Tippen nötig.
- Die KI-Aktion hinterlässt ein öffenbares Kartendokument und ein PDF mit TrimBox.
- Die Vorschau zeigt Beschnittlinie und Falz.

**Aufwand/Risiko:** groß / mittel (Modell kennt nur Y und Ausrichtung; Umbau von Modell, Canvas und PDF-Zeichnung).

**Dateien:** App/MainWindow.Karten.cs, Core/Buero/Karten.cs, Core/PromptAction/BueroPromptAdapters.cs, CT/BueroTests.cs, AT/BueroStudiosUiTests.cs

## STUDIO-CONTRACTS-01 Verträge-Ordner

| Nr | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | TEILWEISE | Liste mit Ampel (App/MainWindow.Vertraege.cs:181-204) und Eingabefelder (Vertraege.cs:235-255) – Liste + Formular, für eine Vertragsverwaltung üblich |
| 2 | Werkzeuge real | VOLLWERTIG | Befehle Vertraege.cs:47-57; Test `Contracts` AT/BueroStudiosUiTests.cs:189 |
| 3 | Neues Projekt | VOLLWERTIG | Vorlagen Vertraege.cs:41-42 |
| 4 | Echte Dateien importieren | TEILWEISE | Dokument (PDF/Bild) wird nur als Pfad verknüpft (Vertraege.cs:98-107; Vorbilder-Liste vertraege-volders-aboalarm.md:16); kein CSV-Import, kein Auslesen aus dem Dokument |
| 5 | Vorhandene Projekte bearbeiten | VOLLWERTIG | Hülle |
| 6 | Rückgängig | VOLLWERTIG | AT/BueroStudiosUiTests.cs:208, 219 |
| 7 | Echte Vorschau | TEILWEISE | Stand-Text und Kosten live (AT/BueroStudiosUiTests.cs:205-207); keine Vorschau des Kündigungsschreibens im Programm (Übergabe an „Briefe schreiben“, Vertraege.cs:55) |
| 8 | Speichern/Autosave | VOLLWERTIG | Hülle |
| 9 | Wiederherstellung | VOLLWERTIG | Hülle; kein studioeigener Test |
| 10 | Export Profi-Formate | VOLLWERTIG | ICS, CSV, Kündigung als PDF – Vertraege.cs:62-64 |
| 11 | Export wieder öffnen | VOLLWERTIG | ICS mit Ical.Net gelesen, 4 Termine, Datum geprüft (CT/BueroTests.cs:415-421; AT/BueroStudiosUiTests.cs:214); PDF nur Seitenzahl (CT/BueroTests.cs:441) |
| 12 | Fachfunktion real | VOLLWERTIG | Fristrechnung mit Monatsenden (Tests `Contracts_deadline`, `Contracts_end` CT/BueroTests.cs:345, 358) |
| 13 | Claude/KI | TEILWEISE | Aktion „Kündigungsfrist berechnen“ → `vertrag.txt` (BueroPromptAdapters.cs:156, 162, 216); legt keinen Vertrag im Ordner an |
| 14 | Ende-zu-Ende-Tests | VOLLWERTIG | keine ersetzten Werkzeuge (nur „heute“ fest, `ConNowOverride`) |
| 15 | Bedienung | TEILWEISE | Hülle; kein Suchen/Filtern/Sortieren belegt (nicht geprüft), kein Rechtsklick |

**Gesamturteil:** TEILWEISE – rechnet richtig und liefert echte Kalenderdatei und Kündigungs-PDF, bleibt aber eine Liste mit Formular; „Erinnerung im Kalender“ ist nur eine Datei.

**Was fehlt**
1. Erinnerung direkt in den Trendo-Kalender eintragen (heute nur `.ics`-Datei, Vertraege.cs:110-119).
2. Dokumente in den Ordner kopieren und im Programm ansehen.
3. Vertragsdaten aus PDF/Foto vorschlagen (Texterkennung ist im Haus vorhanden).
4. Suchen, Filtern, Sortieren; Import aus CSV.
5. KI-Aktion, die den Vertrag anlegt statt nur Text zu schreiben.

**Abnahmekriterien**
- Nach „Erinnerungen“ steht der Termin im Kalender von „E-Mails & Termine“ (Test liest ihn dort).
- Ein angehängtes Dokument liegt im Projektordner und öffnet sich per Klick, auch wenn das Original gelöscht wurde.
- Die KI-Aktion fügt dem offenen Ordner einen Vertrag hinzu (rückgängig machbar).

**Aufwand/Risiko:** mittel / gering.

**Dateien:** App/MainWindow.Vertraege.cs, Core/Buero/Vertraege.cs, Core/PromptAction/BueroPromptAdapters.cs, CT/BueroTests.cs

## STUDIO-PDFTOOLS-01 PDF-Werkstatt

| Nr | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | VOLLWERTIG | Werkzeugleiste links (App/MainWindow.PdfWerkstatt.cs:105), Seitenbilder aus PDFium mit Strg/Umschalt-Auswahl (:531-537, :559), Schwärzen per Maus auf der Seite (:572-587) |
| 2 | Werkzeuge real | VOLLWERTIG | 12 Werkzeuge (PdfWerkstatt.cs:29-43); Tests PW-01 bis PW-10 CT/PdfWerkstattTests.cs:59-196 |
| 3 | Neues Projekt | VOLLWERTIG | Vorlagen je Werkzeug PdfWerkstatt.cs:67-76 |
| 4 | Echte Dateien importieren | VOLLWERTIG | PDF, PNG, JPG über Dateiauswahl (PdfWerkstatt.cs:81); geschützte/defekte PDF wird abgefangen (:133, :548) |
| 5 | Vorhandene Projekte bearbeiten | TEILWEISE | Hülle; Projekt enthält nur Pfade (:136) – nach Verschieben der Quellen leer; Passwort bewusst nur im Speicher (:51) |
| 6 | Rückgängig | VOLLWERTIG | Liste/Seitenfolge über `d.Edit` (:136); Ergebnisdateien bleiben, Original nie überschrieben (Test AT/PdfWerkstattUiTests.cs:77) |
| 7 | Echte Vorschau | VOLLWERTIG | echte Seitenbilder `PdfView.Render` (:559) |
| 8 | Speichern/Autosave | VOLLWERTIG | Hülle |
| 9 | Wiederherstellung | VOLLWERTIG | Hülle; kein studioeigener Test |
| 10 | Export Profi-Formate | TEILWEISE | PDF und Vergleich als TXT (:100-103); kein PDF/A, keine Texterkennung im Stapel (Vorbilder-Liste buero-pdf24-pdfsam.md:20) |
| 11 | Export wieder öffnen | VOLLWERTIG | Ergebnisse mit PdfReader und PDFium zurückgelesen (CT/PdfWerkstattTests.cs:48-57, AT/PdfWerkstattUiTests.cs:141, 163-166); „Ergebnis öffnet in Trendo PDF“ AT/PdfWerkstattUiTests.cs:77 |
| 12 | Fachfunktion real | VOLLWERTIG | Schwärzen: Seite wird als Bild neu aufgebaut, Text ist nicht mehr in der Datei (Core/Sys/PdfWerkstatt.cs:408-447; Test PW-09 CT/PdfWerkstattTests.cs:179-194, UI AT/PdfWerkstattUiTests.cs:146-166). Verkleinern: Bilder werden neu gerechnet und als JPEG gespeichert, Größe gemessen (PdfWerkstatt.cs:274-331; CT/PdfWerkstattTests.cs:97-122; AT/PdfWerkstattUiTests.cs:137-139) |
| 13 | Claude/KI | TEILWEISE | Aktion schreibt nur `pdf-auftrag.txt` (Core/PromptAction/BueroPdfPromptAdapters.cs:19, 90); Ausführen ausdrücklich gesperrt: „die KI ändert keine PDFs selbst“ (:92) |
| 14 | Ende-zu-Ende-Tests | VOLLWERTIG | PDFsharp, PDFium und SkiaSharp echt, keine Ersatz-Werkzeuge in den Tests (AT/PdfWerkstattUiTests.cs:124-166) |
| 15 | Bedienung | TEILWEISE | Mehrfachauswahl mit Strg/Umschalt; Seiten ordnen nur über Knöpfe „Nach vorn/hinten“ (:91-92); kein Ziehen-und-Ablegen (Vorbilder-Liste Zeile 21), kein Rechtsklick |

**Gesamturteil:** VOLLWERTIG – echtes Stapel-Werkzeug mit geprüfter Rundreise; Schwärzen entfernt wirklich, Verkleinern komprimiert wirklich.

**Was fehlt**
1. KI führt den Auftrag aus (Datei rein → neue Datei raus) statt nur Text zu schreiben.
2. Schwärzen, das nur die Stelle entfernt und den Rest der Seite als Text lässt (heute ganze Seite Bild mit 150 dpi, PdfWerkstatt.cs:408; Vorbilder-Liste Zeile 19).
3. Dateien und Seiten per Ziehen-und-Ablegen.
4. PDF/A, Texterkennung im Stapel.
5. Prüfung, dass geschwärzte Datei auch keine alten Metadaten/Anhänge mitnimmt (nicht geprüft).

**Abnahmekriterien**
- Die KI-Aktion „füge a.pdf und b.pdf zusammen“ hinterlässt eine PDF mit der Summe der Seiten im Projekt.
- Nach dem Schwärzen einer Stelle ist der übrige Text der Seite weiter suchbar, der geschwärzte nicht.
- Seiten lassen sich mit der Maus umsortieren; die Ergebnisdatei hat die neue Folge.

**Aufwand/Risiko:** klein–mittel / mittel (stellenweises Schwärzen im Inhaltsstrom ist heikel; lieber Texterkennung nach dem Bild als Zwischenlösung).

**Dateien:** App/MainWindow.PdfWerkstatt.cs, Core/Sys/PdfWerkstatt.cs, Core/Sys/PdfTools.cs, Core/PromptAction/BueroPdfPromptAdapters.cs, CT/PdfWerkstattTests.cs, AT/PdfWerkstattUiTests.cs

## STUDIO-FORMS-01 Formulare ausfüllen

| Nr | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | VOLLWERTIG | gerenderte PDF-Seite (App/MainWindow.Formulare.cs:266) mit Feldern direkt auf der Seite (Test AT/FormulareUiTests.cs:64-65) und Klick-Werkzeugen (Formulare.cs:33-41, 351) |
| 2 | Werkzeuge real | TEILWEISE | Ausfüllen, Text, Datum, Haken, Kreuz, Unterschrift funktionieren (Tests FO-02 bis FO-05); frei Gesetztes ist danach nicht verschiebbar (`IsHitTestVisible = false`, Formulare.cs:348), nur löschbar (:397-398) |
| 3 | Neues Projekt | VOLLWERTIG | „Formular öffnen“, „Beispiel-Antrag“ Formulare.cs:52-55 |
| 4 | Echte Dateien importieren | TEILWEISE | beliebige PDF öffnen (Formulare.cs:58, 121); AcroForm wird wirklich gelesen (Core/Sys/PdfFormular.cs:103). Kein XFA (Grep `XFA` in Core/Sys: 0 Treffer); echte Behördenformulare NICHT GETESTET |
| 5 | Vorhandene Projekte bearbeiten | TEILWEISE | Hülle; Projekt hält nur den Pfad der PDF (:121) |
| 6 | Rückgängig | VOLLWERTIG | Werte und Gesetztes über `d.Edit` (Formulare.cs:62, 199) |
| 7 | Echte Vorschau | VOLLWERTIG | Seitenbild aus PDFium, Pflichtfelder markiert (AT/FormulareUiTests.cs:39) |
| 8 | Speichern/Autosave | VOLLWERTIG | Hülle; sensible Angaben (IBAN) nicht im Dokument (Test FO-06 UI AT/FormulareUiTests.cs:88) |
| 9 | Wiederherstellung | VOLLWERTIG | Hülle; kein studioeigener Test |
| 10 | Export Profi-Formate | VOLLWERTIG | ausgefüllte PDF (Felder bleiben) und geglättete PDF – Formulare.cs:72-75; PdfFormular.cs:139-171 |
| 11 | Export wieder öffnen | VOLLWERTIG | Felder der Ergebnisdatei zurückgelesen (CT/FormulareTests.cs:63-65), Knopfzustände geprüft (:67-72), geglättet: keine Felder, Text per PDFium lesbar (AT/FormulareUiTests.cs:77-79) |
| 12 | Fachfunktion real | VOLLWERTIG | Text, Ankreuzfeld, Auswahl, Optionsfeld gefüllt (CT/FormulareTests.cs:54-76); Original bleibt unverändert (AT/FormulareUiTests.cs:81) |
| 13 | Claude/KI | TEILWEISE | Aktion ordnet nur Feldnamen zu und schreibt `formular-zuordnung.txt` (BueroPdfPromptAdapters.cs:107, 138); füllt nichts aus (:140) |
| 14 | Ende-zu-Ende-Tests | TEILWEISE | echt, aber ausschließlich an Formularen, die Trendo selbst mit `AddFields` gebaut hat (CT/FormulareTests.cs:19-37) |
| 15 | Bedienung | TEILWEISE | Hülle; kein Tab-Sprung von Feld zu Feld belegt (nicht geprüft), kein Rechtsklick, kein Ziehen-und-Ablegen |

**Gesamturteil:** TEILWEISE – echtes Lesen, Füllen und Glätten von PDF-Formularfeldern mit geprüfter Rundreise, aber nur an eigenen Testformularen belegt.

**Was fehlt**
1. Tests mit echten fremden Formularen (frei verfügbare Behörden-PDFs) inkl. Kammfeldern, Mehrzeilern, Datumsformaten.
2. XFA-Formulare erkennen und klar melden (heute nicht behandelt).
3. Frei gesetzten Text und Unterschrift verschieben und in der Größe ändern.
4. Sichtbares Aussehen der Werte sicherstellen: bei nicht geglätteter Datei wird `/NeedAppearances` gesetzt (PdfFormular.cs:159) – ob jeder Betrachter und Druck die Werte zeigt, ist nicht geprüft.
5. KI füllt das offene Formular aus „Meinen Angaben“ wirklich aus (mit Vorschau), statt nur einer Zuordnungsliste.
6. Digitale Signatur ist nicht vorgesehen (Signaturfelder werden übersprungen, PdfFormular.cs:260) – als Grenze nennen.

**Abnahmekriterien**
- Drei frei verfügbare amtliche PDF-Formulare werden geöffnet, gefüllt, gespeichert und die Werte aus der Ergebnisdatei zurückgelesen.
- Ein XFA-Formular führt zu einer verständlichen Meldung statt leerer Feldliste.
- Ein gesetzter Text lässt sich mit der Maus verschieben; die neue Lage steht im PDF.
- Eine nicht geglättete Ergebnisdatei zeigt in PDFium gerendert die Werte (Bildvergleich oder Textauszug).

**Aufwand/Risiko:** mittel / mittel (fremde Formulare sind uneinheitlich; PDFsharp-Grenzen).

**Dateien:** App/MainWindow.Formulare.cs, Core/Sys/PdfFormular.cs, Core/PromptAction/BueroPdfPromptAdapters.cs, CT/FormulareTests.cs, AT/FormulareUiTests.cs

## STUDIO-DECKDESIGN-01 Präsentationen gestalten

| Nr | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | TEILWEISE | links ein Textfeld mit der Gliederung (App/MainWindow.DeckDesign.cs:222-223), in der Mitte gerenderte Folie (:237-238); auf der Folie selbst lässt sich nichts ändern – dafür Übergabe an „Trendo Präsentation“ (:76) |
| 2 | Werkzeuge real | VOLLWERTIG | Gestalten, Design wechseln, Kontrast reparieren, Folienart (DeckDesign.cs:62-71); Tests DD-03, DD-04, DD-08 AT/DeckDesignUiTests.cs:52-104 |
| 3 | Neues Projekt | VOLLWERTIG | vier Vorlagen DeckDesign.cs:53-58 |
| 4 | Echte Dateien importieren | TEILWEISE | Bild nur über Textzeile „Bild: datei.png“ (Core/Office/DeckDesign.cs:132); Gliederung nur aus der gerade offenen Trendo-Präsentation (DeckDesign.cs:188-192), keine PPTX-/Word-Datei direkt |
| 5 | Vorhandene Projekte bearbeiten | VOLLWERTIG | Hülle |
| 6 | Rückgängig | VOLLWERTIG | Gliederung über `d.Set` (DeckDesign.cs:225) |
| 7 | Echte Vorschau | VOLLWERTIG | `SlideView` rendert das erzeugte Folien-Dokument (DeckDesign.cs:238) |
| 8 | Speichern/Autosave | VOLLWERTIG | Hülle |
| 9 | Wiederherstellung | VOLLWERTIG | Hülle; kein studioeigener Test |
| 10 | Export Profi-Formate | VOLLWERTIG | PPTX, PDF, Handzettel-PDF, Plan-TXT – DeckDesign.cs:81-84 |
| 11 | Export wieder öffnen | TEILWEISE | PPTX wird nur mit dem eigenen Leser zurückgelesen (Folienzahl, Titel, Notizen – CT/DeckDesignTests.cs:107-111); keine Prüfung gegen PowerPoint/LibreOffice oder einen OpenXML-Prüfer (Grep `OpenXmlValidator` in src/tests: 0 Treffer); PDF nur Dateigröße > 1000 Bytes (:113) |
| 12 | Fachfunktion real | VOLLWERTIG | Folienart je Inhalt, 6-Punkte-Regel, Schriftgröße, Zeitplan (Tests DD-02 bis DD-06 CT/DeckDesignTests.cs:24-86) |
| 13 | Claude/KI | VOLLWERTIG | Aktion schreibt eine echte `praesentation.pptx` plus Folienplan ins Projekt und liest sie zur Kontrolle zurück (Core/PromptAction/DeckScanPromptAdapters.cs:43-51); Stufe dennoch Partial (CT/DeckDesignTests.cs:135) |
| 14 | Ende-zu-Ende-Tests | TEILWEISE | Export echt; „Mit der KI entwerfen“ nur mit ersetzter KI (`DeckAiOverride`, DeckDesign.cs:23; Test DD-10 AT/DeckDesignUiTests.cs:123) – echter KI-Lauf NICHT GETESTET |
| 15 | Bedienung | TEILWEISE | Farben als Hex-Textfeld (DeckDesign.cs:284); kein Vorführen im Programm, kein Rechtsklick, kein Ziehen-und-Ablegen |

**Gesamturteil:** TEILWEISE – erzeugt echte, gestaltete PPTX/PDF aus Stichpunkten (auch über die KI-Aktion), ist aber ein Generator mit Textfeld und kein Folien-Editor.

**Was fehlt**
1. Text und Bild direkt auf der Folie ändern (oder klare Zwei-Wege-Kopplung mit „Trendo Präsentation“, ohne dass ein erneutes „Gestalten“ Handarbeit überschreibt – Verhalten nicht geprüft).
2. PPTX-Prüfung gegen ein fremdes Programm bzw. einen Formatprüfer.
3. Bilder per Dateiauswahl/Bildsuche statt Textzeile; Logo/Markenvorlage (Vorbilder-Liste Zeilen 17, 19).
4. PPTX/DOCX als Quelle der Gliederung einlesen.
5. Farbwähler; Test mit echter lokaler KI.

**Abnahmekriterien**
- Eine erzeugte PPTX öffnet in LibreOffice Impress ohne Reparaturmeldung (Beleg unter `reports/`).
- Ein Klick auf einen Folientext erlaubt die Änderung; die Gliederung links zieht nach.
- Ein Bild lässt sich über „Bild …“ wählen und erscheint in PPTX und PDF.
- „Mit der KI entwerfen“ ist mit der echten lokalen KI einmal belegt.

**Aufwand/Risiko:** mittel–groß / mittel (Rückweg Folie → Gliederung ist der schwierige Teil).

**Dateien:** App/MainWindow.DeckDesign.cs, Core/Office/DeckDesign.cs, Core/Office/SlideFiles.cs, Core/Office/SlideRead.cs, Core/PromptAction/DeckScanPromptAdapters.cs, CT/DeckDesignTests.cs, AT/DeckDesignUiTests.cs

## STUDIO-DOCSCAN-01 Dokumente scannen

| Nr | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | VOLLWERTIG | Original mit vier ziehbaren Eckpunkten (App/MainWindow.DocScan.cs:500-520; Test SC-09 AT/DocScanUiTests.cs:91), Ergebnisbild daneben, Seitenleiste |
| 2 | Werkzeuge real | TEILWEISE | Kanten finden, Drehen, Ordnen, Modi funktionieren (DocScan.cs:59-67, 78); Knopf „Scanner …“ zeigt nur einen Hinweistext (DocScan.cs:71) |
| 3 | Neues Projekt | VOLLWERTIG | „Neuer Scan“, Beispiel DocScan.cs:50-51 |
| 4 | Echte Dateien importieren | TEILWEISE | Fotos und Ordner (DocScan.cs:59-60; Test AT/DocScanUiTests.cs:122-125). `.heic` steht in der Liste (DocScan.cs:35), gelesen wird mit `SKBitmap.Decode` (:131) – ob HEIC damit geht, ist nicht geprüft. Kein Scanner, keine Kamera (DocScan.cs:21) |
| 5 | Vorhandene Projekte bearbeiten | TEILWEISE | Hülle; Seiten verweisen auf die Fotopfade (`Src`, DocScan.cs:84) |
| 6 | Rückgängig | VOLLWERTIG | Hülle; Seitenänderungen im Dokument (Test SC-06 AT/DocScanUiTests.cs:67) |
| 7 | Echte Vorschau | VOLLWERTIG | Ergebnis ist das tatsächlich verarbeitete Bild (`DocScan.Process` Core/Office/DocScan.cs:255) |
| 8 | Speichern/Autosave | VOLLWERTIG | Hülle |
| 9 | Wiederherstellung | VOLLWERTIG | Hülle; kein studioeigener Test |
| 10 | Export Profi-Formate | TEILWEISE | mehrseitige PDF und PNG 300 dpi (DocScan.cs:75-76); durchsuchbare PDF nur mit eingerichtetem Tesseract (DocScan.cs:283-287); kein PDF/A, kein TIFF |
| 11 | Export wieder öffnen | TEILWEISE | PDF-Seitenformat laut Test SC-07 geprüft (CT/DocScanTests.cs:92); Testkörper nicht im Einzelnen gelesen – Inhalt der Seiten nicht geprüft |
| 12 | Fachfunktion real | TEILWEISE | Blatt finden und perspektivisch entzerren ist echt gerechnet (Core/Office/DocScan.cs:57, 118; Tests SC-01 bis SC-03 CT/DocScanTests.cs:15-48) – aber nur an selbst erzeugten Kunstfotos (`TestPhoto`, DocScan.cs:317; AT/DocScanUiTests.cs:16). „Scannen“ im Wortsinn (Gerät) gibt es nicht |
| 13 | Claude/KI | TEILWEISE | Aktion schreibt nur `scan-auftrag.txt` (DeckScanPromptAdapters.cs:68, 91); „ein Scanner wird nicht angesteuert“ (:93) |
| 14 | Ende-zu-Ende-Tests | NICHT GETESTET | Texterkennung im Test durch `FakeTesseract` ersetzt, der nur eine leere PDF-Seite erzeugt (AT/DocScanUiTests.cs:131-147); kein Test mit echtem Foto; kein Beleg unter `reports/` gefunden |
| 15 | Bedienung | TEILWEISE | Eckpunkte mit der Maus, Modi in der Werkzeugleiste; kein Ziehen-und-Ablegen von Fotos, kein Rechtsklick |

**Gesamturteil:** TEILWEISE – echtes Entzerren und PDF aus Fotos, aber kein Scanner, Texterkennung unbelegt und nur künstliche Testbilder.

**Was fehlt**
1. Scanner-Anbindung (WIA/TWAIN) – oder den Knopf entfernen und das Programm ehrlich „Fotos zu Scans“ nennen.
2. Belegter Lauf mit echtem Tesseract (Text aus der Ergebnis-PDF zurücklesen).
3. Tests mit echten Handyfotos (Schatten, heller Tisch, Knicke) – die Kantenfindung ist nur an dunklem Kunst-Hintergrund belegt.
4. HEIC wirklich lesen oder aus der Liste nehmen.
5. KI-Aktion, die aus übergebenen Fotos die PDF erzeugt.
6. Fotos per Ziehen-und-Ablegen; Kamera.

**Abnahmekriterien**
- Fünf echte Fotos (im Repo als Testdaten) werden erkannt; die Ecken weichen höchstens 2 % der Bildbreite von der Handmarkierung ab.
- Mit eingerichtetem Tesseract enthält die Ergebnis-PDF den erwarteten Text (per PDFium zurückgelesen), Beleg unter `reports/`.
- Ein HEIC-Foto wird geladen oder mit klarer Meldung abgelehnt (Test).
- Kein Knopf im Menüband, der nur einen Hinweis zeigt.

**Aufwand/Risiko:** mittel–groß / mittel–hoch (Scanner-Treiber nur unter Windows, gerätabhängig; echte Fotos decken Schwächen der Kantenfindung auf).

**Dateien:** App/MainWindow.DocScan.cs, Core/Office/DocScan.cs, Core/Sys/PdfSignOcr.cs, Core/PromptAction/DeckScanPromptAdapters.cs, CT/DocScanTests.cs, AT/DocScanUiTests.cs

## STUDIO-DAYPLAN-01 Tagesplaner

| Nr | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | FEHLT | links Aufgaben- und Terminliste mit Textfeldern (App/MainWindow.KreativStudios4.cs:1279-1320), rechts der berechnete Plan als Liste mit Haken (:1323-1360); keine Zeitleiste, kein Ziehen von Blöcken |
| 2 | Werkzeuge real | VOLLWERTIG | Tag planen, Termine/Aufgaben holen, auf morgen (KreativStudios4.cs:1253-1259); Test AT/KreativStudios4UiTests.cs:218 |
| 3 | Neues Projekt | VOLLWERTIG | „Leerer Tag“, „Beispiel: Bürotag“ KreativStudios4.cs:1245-1247 |
| 4 | Echte Dateien importieren | TEILWEISE | Termine und Aufgaben aus dem Trendo-Kalender (KreativStudios4.cs:1173-1200); keine `.ics`-Datei einlesen |
| 5 | Vorhandene Projekte bearbeiten | VOLLWERTIG | Hülle |
| 6 | Rückgängig | VOLLWERTIG | Änderungen über `d.Edit` (KreativStudios4.cs:1186) |
| 7 | Echte Vorschau | TEILWEISE | Plan als Liste; keine Tagesansicht mit Uhrzeit-Raster |
| 8 | Speichern/Autosave | VOLLWERTIG | Hülle |
| 9 | Wiederherstellung | VOLLWERTIG | Hülle; kein studioeigener Test |
| 10 | Export Profi-Formate | VOLLWERTIG | `.ics` und `.txt` – KreativStudios4.cs:1264-1265 |
| 11 | Export wieder öffnen | VOLLWERTIG | ICS von Ical.Net und vom eigenen Kalender gelesen (CT/KreativKits4Tests.cs:519) |
| 12 | Fachfunktion real | VOLLWERTIG | Planer `DayPlanner` (Core/Kreativ/KreativKits4.cs:1181); 300 Zufallstage ohne Überlappung (CT/KreativKits4Tests.cs:482), Grenzfälle (:445) |
| 13 | Claude/KI | TEILWEISE | Aktion „Tag planen“ → nur TXT (Core/PromptAction/KreativPromptAdapters4.cs:139-146), „Trägt nichts in den Kalender ein“ (:135) |
| 14 | Ende-zu-Ende-Tests | VOLLWERTIG | keine externen Werkzeuge; nur „heute“ fest (`DayNowOverride`, KreativStudios4.cs:1107) |
| 15 | Bedienung | TEILWEISE | Hülle; Eingabe als Freitextzeile „Titel, 90 min, hoch, bis 20.11.“ (KreativStudios4.cs:1282); kein Ziehen, kein Rechtsklick |

**Gesamturteil:** TEILWEISE – der Planer rechnet nachweislich richtig und exportiert eine lesbare Kalenderdatei, die Oberfläche ist aber Liste plus Textfeld statt eines Tageskalenders.

**Was fehlt**
1. Tagesansicht mit Uhrzeit-Raster; Blöcke mit der Maus verschieben und verlängern, Plan rechnet nach.
2. Plan direkt in den Trendo-Kalender übernehmen (mit Rückfrage), nicht nur als Datei.
3. Blöcke festhalten („nicht verschieben“), mehrere Tage/Woche.
4. `.ics`-Datei einlesen.
5. KI-Aktion, die den Plan im Programm anlegt.

**Abnahmekriterien**
- Ein Block lässt sich in der Tagesansicht auf eine andere Uhrzeit ziehen; kein Termin wird überlappt (Test).
- „In den Kalender“ legt die Blöcke im Trendo-Kalender an; ein zweiter Klick erzeugt keine Doppelten.
- Eine fremde `.ics` mit drei Terminen erscheint als feste Termine des Tages.

**Aufwand/Risiko:** mittel / gering (Rechenkern steht und ist gut getestet).

**Dateien:** App/MainWindow.KreativStudios4.cs (1100-1382), Core/Kreativ/KreativKits4.cs (ab 1181), Core/PromptAction/KreativPromptAdapters4.cs (135 ff.), CT/KreativKits4Tests.cs (422-560), AT/KreativStudios4UiTests.cs:218


## SPEC-11 Geld & Rechnungen

Programm `finanzen` („Geld“). Registriert als einfacher Reiter, nicht als Programm: `App/MainWindow.W5Finance.cs:15` (`RegisterTab`), `docs/programme/katalog.json:3806` mit `"Shell": false`.

| Nr | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | TEILWEISE | Eine lange Seite aus Karten und Eingabefeldern (`W5Finance.cs:23-80`); direkt bearbeitbar sind nur die Buchungszeilen (Kategorie-Auswahl, „Ändern“) `MainWindow.Haushaltsbuch.cs:91-111`, Ring-Diagramm filtert per Klick `:57-59` |
| 2 | Werkzeuge funktionsfähig | VOLLWERTIG | Buchung eintragen `W5Finance.cs:32-41`, Budget `W5Money.cs:123`, Regeln `Haushaltsbuch.cs:216-222`, Storno als Gegenbuchung `Core/Finance.cs:156-165`; Tests `HaushaltsbuchUiTests`, `W5FinanceUiTests` |
| 3 | Neues Projekt | FEHLT | Es gibt genau ein Buch je Datenordner: `Core/Finance.cs:137` (`finanzen/buchungen.json`); kein Mandant, kein zweites Konto (auch `docs/claude/vorbilder/geld-gnucash-homebank.md:17` „✗ Mehrere Konten“) |
| 4 | Echte Dateien importieren | TEILWEISE | Nur Bank-CSV: `Core/Finance.cs:45-67`, Vorschau vor Übernahme `Haushaltsbuch.cs:145-183`. MT940, CAMT.053, OFX, QIF: kein Treffer im Quelltext (Grep über `src`). Pfad wird getippt, kein Dateidialog: `W5Finance.cs:43-44` |
| 5 | Vorhandenes bearbeiten | VOLLWERTIG | Buchung ändern `Haushaltsbuch.cs:126-142`, `Ledger.Replace` `Core/Finance.cs:150` |
| 6 | Rückgängig | TEILWEISE | Eigener Stapel nur für Haushaltsbuch-Änderungen, max. 30, nur im Arbeitsspeicher: `Haushaltsbuch.cs:14, 21-27, 38`. „Neue Buchung“, „Rechnung erstellen“, Budget setzen laufen nicht darüber (`W5Finance.cs:32-41, 60-73`). Kein Wiederholen |
| 7 | Echte Vorschau | TEILWEISE | Diagramme und Kennzahlen live (`W5Money.cs:103-110`); Rechnung hat keine Vorschau vor dem Schreiben, sie wird sofort gespeichert und gebucht (`W5Finance.cs:64-70`) |
| 8 | Speichern/Autosave | TEILWEISE | Jede Änderung schreibt sofort die JSON-Datei (`Core/Finance.cs:140-141`), aber direkt mit `File.WriteAllText` ohne Zwischendatei und ohne Versionen; die Versionen der Hülle (`MainWindow.ProgramShell.cs:592`) gelten hier nicht |
| 9 | Wiederherstellung nach Absturz | NICHT GETESTET | Kein Sicherungs- oder Wiederherstellungsweg für `buchungen.json` gefunden; beschädigte Datei wirft in `Ledger.All()` (`Finance.cs:139`, kein `try`) |
| 10 | Export in Profi-Formaten | TEILWEISE | CSV `Finance.cs:80-87`; Steuerberater-ZIP (CSV + Summen + HTML-Rechnungen) `Core/FinanceExtra.cs:60-79`; XRechnung CII 2.3 `Core/ERechnung.cs:76-102`. Rechnung selbst nur HTML (`Finance.cs:116-132`, `:173`). Kein PDF, kein ZUGFeRD-PDF, kein DATEV-Format, kein ELSTER/EÜR-Formular (kein Treffer im Quelltext) |
| 11 | Rundreise geprüft | TEILWEISE | CSV Export→Import: `W5FinanceTests.Sums` („Export-Import ohne Verlust“); XRechnung wird nach dem Schreiben wieder eingelesen `W5ERechnung.cs:31`, `ERechnungTests` (Hin- und Rückweg). Keine Prüfung gegen den offiziellen KoSIT-Validator; `reports/alle-studios/erechnung-e2e.md` belegt nur die eigenen Tests |
| 12 | Fachfunktion real | TEILWEISE | Real: Netto/MwSt, Monats-/Jahressummen `Finance.cs:89-100`, fortlaufende Nummer `:102-107`, Budgetwarnung, Abo-Erkennung, IBAN-Prüfung `ERechnung.cs:53-65`. Fehlt: Kredit/Tilgungsplan (nur Text im Studio-Satz `App/StudioCatalog.cs:35`; `Core/PromptAction/StudioKnowledge.cs:401` sagt selbst „das Programm hat dafür keine eigene Funktion“), Steuer nur als Übersicht |
| 13 | Claude/KI arbeitet direkt | TEILWEISE | `InvoicePromptAdapter` (Stufe Partial) schreibt eine HTML-Rechnung, bucht nicht: `Core/PromptAction/BusinessPromptAdapter.cs:8-32`; `MoneyPayAdapter` Blocked `:35-60`. Seite „finanzen“ ist für Claude zustimmungspflichtig: `App/MainWindow.ClaudeBridge.cs:249`. Kredit und Sparplan sind reine Chat-Antworten (`StudioKnowledge.cs:400-401`) |
| 14 | Ende-zu-Ende-Tests | TEILWEISE | Rein lokal, nichts ersetzt: `W5FinanceTests`, `HouseholdBookTests`, `ERechnungTests`, `W5FinanceUiTests`, `HaushaltsbuchUiTests`, `ERechnungUiTests`. Kein Test mit echtem Kontoauszug einer Bank, keine externe Validierung der XRechnung |
| 15 | Bedienung | TEILWEISE | Nur Enter in Suchfeld (`Haushaltsbuch.cs:85`). Kein Rechtsklick, kein Drag-and-drop (0 Treffer `ContextMenu|DragDrop` in den Dateien), keine Tastenkürzel der Hülle (`ProgramShell.cs:348-350` greift nur für Programme). Einfach/Profi, Vollbild: nicht geprüft |

**Gesamturteil:** TEILWEISE – ein brauchbares Haushaltsbuch mit echter Rechnungs- und XRechnungs-Ausgabe, aber als Formularseite ohne Programm-Hülle und ohne die im Namen versprochenen Teile Kredite und Steuern.

**Was fehlt**
1. Rechnung als PDF (und ZUGFeRD-PDF) mit allen Pflichtangaben nach § 14 UStG: die normale Rechnung übergibt keine Steuernummer (`W5Finance.cs:64-65`; Ausgabe nur wenn `TaxId` gesetzt `Finance.cs:130`), keine Bankverbindung, kein Zahlungsziel, nur eine Position, Leistungsdatum fest gleich Rechnungsdatum (`Finance.cs:123`).
2. Umzug in die Programm-Hülle: Dokument, Rückgängig/Wiederholen für alles, Versionen, Absturz-Sicherung.
3. Bankimport MT940 und CAMT.053, Dateidialog statt getipptem Pfad, mehrere Konten.
4. Kredit-Rechner mit Tilgungsplan (Annuität, Sondertilgung) – steht im Studio-Satz, existiert nicht.
5. Steuer-Ausgabe: EÜR nach Anlage-EÜR-Zeilen, USt-Voranmeldung, DATEV-Buchungsstapel (CSV im DATEV-Format).
6. Eingehende E-Rechnungen lesen (`ERechnung.Read` wird nur zur Selbstkontrolle benutzt: einziger Aufruf `W5ERechnung.cs:31`).

**Abnahmekriterien**
- Eine Rechnung mit drei Positionen entsteht als PDF, enthält Steuernummer oder USt-IdNr., Leistungsdatum, Zahlungsziel und Bankverbindung; die eingebettete oder beiliegende XRechnung besteht den KoSIT-Validator (Beleg unter `reports/`).
- Ein echter MT940- und ein CAMT.053-Auszug werden eingelesen; Anzahl und Summe stimmen mit dem Auszug überein.
- Kredit 10.000 €, 5 %, 60 Monate ergibt einen Tilgungsplan, dessen Restschuld im letzten Monat 0,00 € ist.
- Nach erzwungenem Prozessende mitten in einer Buchung ist `buchungen.json` lesbar und der letzte Stand vorhanden.
- Strg+Z nimmt auch „Neue Buchung“ und „Rechnung erstellen“ zurück.

**Aufwand/Risiko:** groß. Risiko hoch bei Steuer- und Rechnungsrecht (Pflichtangaben, GoBD: Rechnungen als überschreibbare HTML-Dateien, Nummern werden aus Dateinamen im Ordner abgeleitet `Finance.cs:172` – gelöschte Datei gibt die Nummer wieder frei).

**Dateien:** `App/MainWindow.W5Finance.cs`, `App/MainWindow.W5Money.cs`, `App/MainWindow.Haushaltsbuch.cs`, `App/MainWindow.W5ERechnung.cs`, `App/MainWindow.W5SmallBiz.cs`, `Core/Finance.cs`, `Core/FinanceExtra.cs`, `Core/HouseholdBook.cs`, `Core/ERechnung.cs`, `Core/PromptAction/BusinessPromptAdapter.cs`, Tests `tests/Trendo.Core.Tests/W5FinanceTests.cs`, `ERechnungTests.cs`, `HouseholdBookTests.cs`, `tests/Trendo.App.Tests/W5FinanceUiTests.cs`, `HaushaltsbuchUiTests.cs`, `ERechnungUiTests.cs`.

---

## STUDIO-ONLINE-BUSINESS-01 Online verkaufen

Programm `verkaufen` mit drei Unterreitern (Kasse, Online & Artikel, Auswertung) und zweiter Reiter `business` („Geschäftsidee“): `App/MainWindow.W5Business.cs:17-18`, `App/MainWindow.Kasse.cs:32-38`. Kein Programm der Hülle (`katalog.json:3894`, `"Shell": false`). Die Tabelle bewertet das Studio gesamt; Unterschiede je Teil stehen in der Belegspalte.

| Nr | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | TEILWEISE | Kasse: Artikel-Kacheln und Warenkorb mit +/− `Kasse.cs:73-106` – fachgerecht. Online & Artikel: Formularzeilen und Listen `W5Sell.cs:159-262`. Geschäftsidee: Formular mit Zahlenfeldern `W5Business.cs:33-47` |
| 2 | Werkzeuge funktionsfähig | VOLLWERTIG | Kassieren `Core/CashRegister.cs:47-70`, Rabatt `:38-45`, Storno `:73-85`, Scanner-Eingabe `Kasse.cs:71`, Bestellung/Lieferung/Zahlung/Erstattung `W5Sell.cs:175-217`; Tests `CashRegisterTests`, `SalesStatsTests`, `KasseUiTests` |
| 3 | Neues Projekt | TEILWEISE | „+ Neue Idee“ legt eine Geschäftsidee an, an der Shop und Kasse hängen: `W5Business.cs:30`, `Kasse.cs:13` (`BizIdea.Id`). Keine Vorlagen |
| 4 | Echte Dateien importieren | FEHLT | Kein Import für Artikel (CSV), Kunden oder Bestellungen; VK-11 „Bestellungen aus fremden Online-Shops lesen“ steht im Katalog als „offen“ (`katalog.json`, Funktionen VK-11) |
| 5 | Vorhandenes bearbeiten | TEILWEISE | Bestellungen durchlaufen Zustände (`W5Sell.cs:175-217`); Artikel und Kunden lassen sich auf dieser Seite nur anlegen, nicht ändern oder löschen (`W5Sell.cs:232-262` enthält nur „anlegen“) |
| 6 | Rückgängig | FEHLT | Kein Rückgängig; Korrektur nur über Storno (`Kasse.cs:160-167`) und Erstattung (`W5Sell.cs:200-215`). Fachlich für eine Kasse richtig, für Artikel/Kunden fehlt es |
| 7 | Echte Vorschau | TEILWEISE | Webshop-Vorschau als Karten in der Seite `W5Sell.cs:243-250`; Bon hat keine Vorschau vor dem Speichern (`Kasse.cs:146`) |
| 8 | Speichern/Autosave | TEILWEISE | Verkäufe, Bestand, Bewegungen werden sofort geschrieben (`CashRegister.cs:65-68`). Der Warenkorb liegt nur im Arbeitsspeicher (`Kasse.cs:25`). Kassieren schreibt vier Dateien nacheinander ohne Transaktion (`CashRegister.cs:65-68`) |
| 9 | Wiederherstellung nach Absturz | NICHT GETESTET | Kein Test und kein Mechanismus für einen Abbruch zwischen Bestandsänderung und Bon (`CashRegister.cs:64-68`) gefunden. Sicherung als ZIP gibt es im Nachbar-Reiter „Start & Übergabe“ (`App/MainWindow.W5Launch.cs:64`, anderes Studio-Kürzel BUS-030) |
| 10 | Export in Profi-Formaten | TEILWEISE | Bon als PDF 80 mm `CashRegister.cs:97-125`, Tagesabschluss PDF A4 `:128-147` (PdfSharp, echt), Shop als eine HTML-Datei `W5Sell.cs:258-262`, Bauplan Markdown `W5Business.cs:58`. Kein DSFinV-K/Kassenexport, kein Bon-Druck, kein Artikel-CSV. TSE ist im Katalog als „bewusst nicht“ entschieden (VK-12) und wird nicht als Lücke gewertet; Hinweis steht auf der Seite `Kasse.cs:183` |
| 11 | Rundreise geprüft | NICHT GETESTET | PDF wird nur auf Gültigkeit geprüft (`CashRegisterTests.Cancel_day_pdf`); kein Wiedereinlesen. HTML-Shop wird nicht wieder geöffnet |
| 12 | Fachfunktion real | TEILWEISE | Kasse real (Bestand, MwSt, Einnahme in „Geld“, fortlaufende Bon-Nummer `CashRegister.cs:62-63`). „Online verkaufen“ ist keine Verkaufsfunktion: die erzeugte Seite sagt selbst „Vorschau – Bestellung und Bezahlung sind hier nicht eingerichtet“ (`Core/FinanceExtra.cs:306-321`). Kartenzahlung ist nur ein Merkmal am Bon, kein Terminal (`CashRegister.cs:50, 63`). Geschäftsidee: Bewertung wird gerechnet (`W5Business.cs:61-78`), Gründung ist eine Abhakliste (`:82-93`) |
| 13 | Claude/KI arbeitet direkt | TEILWEISE | Einziger Adapter `BusinessPlanAdapter` (Partial) schreibt einen Markdown-Bauplan: `BusinessPromptAdapter.cs:63-83`. Katalog-Grenze: „Kein Shop, keine Zahlung“ `Core/PromptAction/CapabilityCatalog.cs:33`. Für Kasse und Shop gibt es keinen Adapter; `KasseRenderCart`/`KasseCheckout` sind öffentlich „für Tests und die KI“ (`Kasse.cs:29-30, 48-49`), eine Anbindung daran habe ich nicht gefunden (nicht geprüft über `ClaudeBridge` hinaus) |
| 14 | Ende-zu-Ende-Tests | TEILWEISE | Lokal echt: `CashRegisterTests`, `SalesStatsTests`, `KasseUiTests`, `Teil4BusinessUiTests`, `W5BusinessTests`. Der Stripe-Shop auf dieser Seite (`W5Sell.cs:263`) nur gegen Fake (`PodShopUiTests`, `PodWebOverride`) |
| 15 | Bedienung | TEILWEISE | Enter für Scanner/Suche und Rabatt (`Kasse.cs:71, 117`), Schnellbeträge (`:133-135`). Kein Rechtsklick, kein Drag-and-drop (0 Treffer), keine Kürzel der Hülle. Einfach/Profi, Vollbild: nicht geprüft |

**Gesamturteil:** TEILWEISE – die Kasse ist ein echtes kleines Kassenbuch mit PDF-Bon, der namensgebende Online-Verkauf ist eine statische Vorschau-Seite, und „planen, gründen“ ist Formular plus Text.

**Was fehlt**
1. Echter Online-Shop als Projekt: mehrere Seiten, Produktbilder, Warenkorb, Bestellung, Pflichtseiten – heute nur eine HTML-Datei ohne Bilder und ohne Kauf (`FinanceExtra.cs:306-321`). Der vorhandene Stripe/Printful-Shop (`Core/Pod/PodShop.cs:79-115`) deckt nur POD-Produkte.
2. Artikel und Kunden ändern/löschen, Artikelbilder, Varianten, CSV-Import und -Export.
3. Umzug in die Programm-Hülle mit Rückgängig für Stammdaten und Absturz-Sicherung; Kassieren als eine unteilbare Schreibaktion.
4. Bon drucken (Bondrucker) und Kassen-Datenexport; Kartenzahlung bleibt reines Merkmal.
5. Geschäftsidee: Finanzplan (Liquidität über 12 Monate), Businessplan als PDF/Dokument statt nur Markdown.
6. Bestellungen aus fremden Shops lesen (VK-11, offen).

**Abnahmekriterien**
- „Als Webseite speichern“ erzeugt einen Ordner mit Startseite, Produktseiten mit Bild, Impressum/AGB/Widerruf/Datenschutz; ein HTML-Validator meldet keine Fehler, die Seite ist bei 375 px Breite bedienbar.
- Ein Testkauf im Stripe-Testmodus erscheint als Bestellung in Trendo (Beleg mit echtem Testkonto unter `reports/`).
- Ein Artikel lässt sich umbenennen und im Preis ändern; alte Bons zeigen weiter den alten Preis.
- Prozessende während „Kassieren“ hinterlässt entweder Bon + Bestand + Einnahme oder nichts davon.
- 200 Artikel aus einer CSV sind in unter 5 Sekunden angelegt, doppelte Nummern werden gemeldet.

**Aufwand/Risiko:** groß. Risiko: Rechtstexte und Zahlungsabwicklung (nur mit eigenem Konto der Person), Kassenrecht – die Kasse darf weiter nicht als zertifizierte Kasse auftreten.

**Dateien:** `App/MainWindow.W5Business.cs`, `App/MainWindow.W5Sell.cs`, `App/MainWindow.Kasse.cs`, `App/MainWindow.KasseStats.cs`, `Core/CashRegister.cs`, `Core/FinanceExtra.cs` (Klasse `Shop`, `FoundingSteps`), `Core/BusinessLaunch.cs`, `Core/PromptAction/BusinessPromptAdapter.cs`, `docs/spec/kataloge/STUDIO-ONLINE-BUSINESS-01_KATALOG.md`, Tests `tests/Trendo.Core.Tests/CashRegisterTests.cs`, `SalesStatsTests.cs`, `W5BusinessTests.cs`, `tests/Trendo.App.Tests/KasseUiTests.cs`, `Teil4BusinessUiTests.cs`.

---

## STUDIO-POD-01 Print on Demand

Programm `pod-programm`, läuft in der Programm-Hülle: `App/MainWindow.PodStudio.cs:41-61` (`RegisterProgram`, `ProgramSpec`), `katalog.json:4043` mit `"Shell": true`. Katalog-Status dort: „IN_PROGRESS / Test NOT_RUN“.

| Nr | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Echte Arbeitsfläche | VOLLWERTIG | Zeichenfläche in Druckgröße mit Auswählen, Verschieben, Skalieren, Drehen, Einrasten, Zoom und Schwenken: `App/MainWindow.PodDesigner.cs:151-222`; Ebenenliste links `:318-340`, Eigenschaften rechts `:343-411` |
| 2 | Werkzeuge funktionsfähig | TEILWEISE | Text, Rechteck, Ellipse, Bild (`Core/Pod/PodDesign.cs:36-41`), Gruppen, Reihenfolge, Schatten, Umriss. Keine Bézier-Pfade, kein Freistellen, keine Effekte, 8 feste Schriften (`PodDesigner.cs:32`); Test `PodDesignUiTests.Edit_with_mouse` |
| 3 | Neues Projekt | VOLLWERTIG | Vorlagen T-Shirt, Tasse, Poster: `PodStudio.cs:46-51`; Startseite der Hülle `MainWindow.ProgramShell.cs:15` |
| 4 | Echte Dateien importieren | TEILWEISE | PNG/JPEG über Dateidialog: `PodDesigner.cs:326`, `PodStudio.cs:93`. Kein SVG, kein PSD/PDF. Bilder werden als absoluter Pfad gemerkt, nicht ins Dokument übernommen (`PodDesign.cs:10-11` Feld `Src`; fehlt die Datei, zeigt die Fläche „Bild fehlt“ `PodDesigner.cs:55-56`) |
| 5 | Vorhandenes bearbeiten | VOLLWERTIG | Dokument `.tdoc` wird geöffnet und weiterbearbeitet (Hülle); Inhalt unter `design` im Dokument `PodDesign.cs:31-32` |
| 6 | Rückgängig | VOLLWERTIG | Jede Änderung läuft über `d.Edit` der Hülle: `PodDesigner.cs:35`, `:213`; Strg+Z/Y `ProgramShell.cs:349-350`, Knöpfe `:615-616` |
| 7 | Echte Vorschau | TEILWEISE | Lokale Produktvorschau ist eine gezeichnete Umrissform, kein Foto: `PodStudio.cs:112-146` (Kommentar „vereinfachte Vorschau“). Echte Mockups nur über Printful |
| 8 | Speichern/Autosave | VOLLWERTIG | Autosave der Hülle alle 30 s, 1,5 s nach Änderung und beim Schließen: `ProgramShell.cs:83-88, 228, 245-247`; Versionen `:592` |
| 9 | Wiederherstellung nach Absturz | TEILWEISE | „Wiederherstellen“ auf der Startseite der Hülle `ProgramShell.cs:520-526`. Für POD nicht eigens getestet (kein Treffer in den POD-Tests) |
| 10 | Export in Profi-Formaten | TEILWEISE | PNG mit Transparenz in 300 DPI `PodDesigner.cs:79-93`, SVG `PodDesign.cs:116-135`, Verkaufstext Markdown `PodStudio.cs:56`. Kein PDF, kein CMYK/Farbprofil, keine anbietergenauen Druckmaße (Richtwerte: `Core/Pod/PodStudio.cs:7, 22-29`) |
| 11 | Rundreise geprüft | TEILWEISE | Exportiertes PNG wird als Motiv übernommen und besteht die eigene Druckprüfung: `PodDesignUiTests.Use_as_motif`. SVG wird nicht wieder eingelesen |
| 12 | Fachfunktion real | TEILWEISE | Druckprüfung DPI/Größe/Transparenz real `Core/Pod/PodStudio.cs:33-65`, Preisrechner `:72-90`, Markenwarnung aus 40-Wörter-Liste `:97-109`. Mockup-Erzeugung real nur über Printful und braucht zusätzlich ein Netlify-Konto als öffentliche Ablage: `App/MainWindow.PodPrintful.cs:111-113, 146-174` |
| 13 | Claude/KI arbeitet direkt | TEILWEISE | Einziger Adapter `PodPriceAdapter` (Partial) schreibt `preisrechnung.txt`: `Core/PromptAction/StudyPromptAdapter.cs:184-214`. Kein Adapter, der Elemente auf die Fläche setzt. Kittl-Anbindung nur als Schalter, damit Claude die Kittl-Werkzeuge nutzen darf (`PodDesigner.cs:419-421, 495-523`) |
| 14 | Ende-zu-Ende-Tests | BLOCKIERT | Printful, Printify, Stripe, Netlify: ausschließlich Fake-HTTP – `PodPrintfulUiTests.cs:16, 47` (`PodWebOverride`, sogar `PodPngOverride`), `PodPrintifyUiTests.cs:17, 45`, `PodShopUiTests.cs:14, 41`, Kern `PrintfulTests`, `PrintifyTests`, `PodShopTests`. Kittl: `KittlCliOverride` `PodDesignUiTests.cs:115`. Kein Beleg eines echten Laufs unter `reports/` (Grep ohne Treffer). Ohne eigene Konten nicht prüfbar. Lokaler Teil echt getestet: `PodDesignTests`, `PodStudioTests`, `PodDesignUiTests` |
| 15 | Bedienung | TEILWEISE | Viele Tastenkürzel: Entf, Strg+G, Strg+C/X/V, Pfeile, Klammern für Reihenfolge, +/− Zoom: `PodDesigner.cs:269-296`; Mausrad-Zoom, mittlere Taste schwenkt `:155, 217-221`. Kein Rechtsklick-Menü, kein Drag-and-drop von Bildern auf die Fläche (0 Treffer `ContextMenu|DragDrop`). Einfach/Profi, Vollbild: nicht geprüft |

**Gesamturteil:** TEILWEISE – ein echter, einfacher Design-Editor in der Programm-Hülle mit realer Druckprüfung; der Weg zum verkauften Produkt hängt an vier Fremdkonten und ist nie gegen die echten Dienste belegt.

**Was fehlt**
1. Ein echter Lauf gegen Printful, Printify, Stripe (Testmodus) und Netlify mit Beleg – heute alles Fake.
2. Fotoreale Produktvorschau ohne Fremdkonto (heute Umrisszeichnung); Mockup über Printful verlangt Printful **und** Netlify.
3. Bilder ins Dokument übernehmen statt absoluter Pfade; SVG-Import; Drag-and-drop auf die Fläche; Rechtsklick-Menü.
4. Anbietergenaue Druckbereiche je Artikel statt fünf Richtwerten; Druckprüfung gegen die Maße des gewählten Printful-/Printify-Produkts.
5. Gelato (in der Planung genannt, `docs/claude/FILM_POD_PLAN_2026-11-08.md:31`; im Quelltext kein Treffer).
6. KI-Aktion, die den Entwurf wirklich baut (Text/Bild auf die Fläche), statt nur den Preis zu rechnen.

**Abnahmekriterien**
- Mit einem echten Printful-Testkonto entstehen aus einem Entwurf mindestens zwei Mockup-Bilder; Protokoll und Bilder liegen unter `reports/`.
- Ein Kauf im Stripe-Testmodus erscheint als „Testzahlung“ und wird nachweislich nicht an Printful gegeben (heute nur mit Fake geprüft: `PodShopUiTests.Test_payment_and_restart`).
- Ein Dokument mit Bild lässt sich nach dem Verschieben in einen anderen Ordner oder auf einen anderen PC vollständig öffnen.
- Ein Bild aus dem Explorer lässt sich auf die Fläche ziehen; Rechtsklick auf ein Element bietet Kopieren, Löschen, Reihenfolge.
- Die Druckprüfung nennt für ein gewähltes Printful-Produkt dessen echte Pixelmaße.

**Aufwand/Risiko:** mittel. Risiko: Kosten und Rechtliches bei echten Bestellungen (Freigabe-Klick ist vorhanden: `App/MainWindow.PodShop.cs:141-152`, `:222`), Abhängigkeit von vier Schnittstellen, Stripe-Geheimschlüssel im Programm (über `_core.Secrets`, `PodShop.cs:43`). Stripe ist nicht auf den Testmodus beschränkt – ein Live-Schlüssel wird angenommen (`PodShop.cs:45`); das ist eine Entscheidung, die der Nutzer kennen sollte.

**Dateien:** `App/MainWindow.PodStudio.cs`, `App/MainWindow.PodDesigner.cs`, `App/MainWindow.PodPrintful.cs`, `App/MainWindow.PodPrintify.cs`, `App/MainWindow.PodShop.cs`, `Core/Pod/PodStudio.cs`, `Core/Pod/PodDesign.cs`, `Core/Pod/Printful.cs`, `Core/Pod/Printify.cs`, `Core/Pod/PodShop.cs`, `Core/Pod/KittlConnect.cs`, `Core/PromptAction/StudyPromptAdapter.cs:183-214`, `App/MainWindow.ProgramShell.cs`, `Core/Programs/ProgramDocument.cs`, Tests `tests/Trendo.App.Tests/PodDesignUiTests.cs`, `PodPrintfulUiTests.cs`, `PodPrintifyUiTests.cs`, `PodShopUiTests.cs`, `tests/Trendo.Core.Tests/PodDesignTests.cs`, `PodStudioTests.cs`, `PrintfulTests.cs`, `PrintifyTests.cs`, `PodShopTests.cs`.

---


## Auffälligkeiten der Gruppe (Fassaden, tote Knöpfe, Doku≠Code, Fehler)

### Büropaket SYS-10
Siehe Abschnitt „Auffälligkeiten SYS-10“ oben.

### E-Mail, Helfer, Briefe, Vorhaben

1. **Doku ≠ Code (Mail-Versand):** `CapabilityCatalog.cs:22` („Versand ist nicht angeschlossen. Trendo schickt selbst keine E-Mail.“), `MailPromptAdapter.cs:32,58` („Trendo hat kein Postfach“) und der Kopfkommentar `MainWindow.Mail.cs:9` widersprechen dem Posteingang, der über `MailNet.SendAsync` wirklich per SMTP sendet (`MainWindow.Pim.cs:131`). Die KI-Aktion `mail.senden` bleibt deshalb Blocked, obwohl der Sendeweg existiert.
2. **Anhänge gehen beim echten Senden verloren:** `MailDraft.Attachments` existiert (`src/Trendo.Core/W3/Mail.cs:11`) und wird für den .eml-Entwurf als multipart geschrieben (`Mail.cs:67-76`), aber `SmtpSession.Compose` baut immer nur `text/plain` (`MailProtocol.cs:184`); das Schreibfeld im Posteingang hat gar kein Anhang-Feld (`Pim.cs:305-316`).
3. **Zwei Mail-Programme im selben Studio:** „E-Mail & Kontakte“ (`post`, Formular, gibt an fremdes Mailprogramm ab, `Mail.cs:27,40`) neben „Posteingang“ (eigenes Senden). „E-Mail schreiben“ im Adressbuch führt zur schwächeren Seite (`AddressBook.cs:146` → `Show("post")`).
4. **Mail-Tests nie gegen echten Server:** IMAP nur über `ImapStreamOverride` auf Loopback (`MailProgramUiTests.cs:53`, `PimUiTests.cs:45`, `Teil4BueroUiTests.cs:100`), Senden nur über `SendOverride` (`MailProgramUiTests.cs:74`, `PimUiTests.cs:55`). Der TLS-/STARTTLS-Code in `MailNet.cs:10-32` wird von keinem gefundenen Test ausgeführt. Katalog-Status trotzdem „IMPLEMENTED / Test PC_VERIFIED“.
5. **Posteingang zeigt höchstens 50 Nachrichten** und nur INBOX (`Pim.cs:89-90`, `MailProtocol.cs:83,90`); „Gemerkt“ merkt nur die UID in den Einstellungen (`Pim.cs:288`) – bei Kontowechsel passen die UIDs nicht mehr (Folge nicht geprüft).
6. **Doku ≠ Code (Brief-PDF):** `LetterPromptAdapter.cs:73-74` und `CapabilityCatalog.cs:20` behaupten „PDF nur mit LibreOffice“; das Programm schreibt PDF selbst ohne LibreOffice (`W1Letter.cs:286-288`, `LetterPdf.cs:15`). Die KI-Aktion liefert deshalb unnötig kein PDF.
7. **Katalog-Name ≠ Ort der Funktion:** Das Bildschirmfoto liegt nicht auf der Seite „medien“, sondern im Reiter „alltag-bilder“ (`MainWindow.Sys02.cs:29,185`); „Trendo-Fenster aufnehmen“ rendert nur das eigene Fenster (`MainWindow.axaml.cs:337-344`) – kein Bildschirmfoto im üblichen Sinn.
8. **„Wecker“ auf der Seite Werkzeuge ohne Ton und ohne freie Zeit** (`W1Apps.cs:147-177`); doppelte Funktionen zu den Alltag-Reitern (Rechner `W1Apps.cs:104` vs. Helfer N11, Notizen vs. Notizbuch/Zettel, Wecker vs. „Uhr & Wecker“).
9. **Musik nur unter Windows und nur mit mpv:** Steuerung über `\\.\pipe\…` (`MpvAudio.cs:31-32`); ohne mpv nur der Hinweis „Player einrichten“ (`MusicPlayer.cs:66,316`). Der echte mpv-Test überspringt sich, wenn mpv fehlt (`MpvAudioTests.cs:21`).
10. **KI-Aktion X-11 geht am Programm vorbei:** `projekt.anlegen` schreibt in den `ProjectStore` (`MeasurePromptAdapter.cs:380-394`), das Programm „Vorhaben“ arbeitet in `WorkFolder("Vorhaben")` mit `SCHRITTE.md` (`W4Suite.cs:15,124`).
11. **Katalog-Rauschen:** `docs/programme/katalog.json` führt bei „post“ Formate wie `.gltf/.mp4/.webm` und Abhängigkeiten FFmpeg/VLC/mpv, bei „vorhaben“ leere `Functions` und als Tests fremde SYS-09/SYS-10-Tests; bei kalender/kontakte/aufgaben/posteingang dieselbe Funktionsliste EM-01 … – die Einträge sind offenbar automatisch und nicht je Programm belastbar.
12. **Keine toten Knöpfe gefunden** in den gelesenen Abschnitten; unsichtbare Hilfsknöpfe `letter-cat-*` (Höhe 0, nicht anklickbar, nur für Tests/KI) in `W1Office.cs:144-155`.


### Sieben neue Büro-Studios und Tagesplaner

**Knöpfe ohne Funktion / Fassaden**
- „Scanner …“ im Menüband von „Dokumente scannen“ setzt nur einen Meldungstext („noch nicht angeschlossen“) – App/MainWindow.DocScan.cs:71. Der Test prüft genau diesen Hinweis als Soll (AT/DocScanUiTests.cs:137). Programmname und Studio-Satz versprechen Scannen; real sind es Fotos.
- Texterkennung in „Dokumente scannen“: einziger Test ersetzt Tesseract durch eine Attrappe, die eine leere PDF-Seite schreibt (AT/DocScanUiTests.cs:139-147). Der echte Weg ist unbelegt.

**Nur Text statt Produkt (KI-Aktionen)**
- Sieben von acht KI-Aktionen liefern ausschließlich eine TXT-Datei, alle Stufe Partial: `lebenslauf.txt` (BueroPromptAdapters.cs:88), `karte.txt` (:146), `vertrag.txt` (:216), `pdf-auftrag.txt` (BueroPdfPromptAdapters.cs:90), `formular-zuordnung.txt` (:138), `scan-auftrag.txt` (DeckScanPromptAdapters.cs:91), Tagesplan-TXT (KreativPromptAdapters4.cs:146). Einzige Ausnahme: Präsentationen gestalten schreibt eine echte PPTX (DeckScanPromptAdapters.cs:43-51).
- Bezeichnung passt nicht zum Ergebnis: Aktion „Karte anlegen“ (BueroPromptAdapters.cs:98) legt keine Karte an, sondern eine Textdatei.
- PDF-Werkstatt und Formulare sperren das Ausführen durch die KI ausdrücklich (BueroPdfPromptAdapters.cs:92, 140) – die KI kann dort nur beschreiben.

**Doku ≠ Code**
- Studio-Satz Verträge: „Erinnerung im Kalender“ (App/StudioCatalog.cs:29) – tatsächlich entsteht eine `.ics`-Datei, die die Person selbst per Doppelklick übernimmt (App/MainWindow.Vertraege.cs:110-119).
- Studio-Satz Scannen: „Dokumente scannen“ (StudioCatalog.cs:25) – kein Gerät angebunden (DocScan.cs:21, 71).
- Die Vorbilder-Listen unter `docs/claude/vorbilder/` (bewerbung…, buero-pdf24…, karten-canva…, praesentation-gestalten…, scannen-lens…, vertraege-volders…) nennen dieselben Lücken wie der Code – dort keine Abweichung gefunden.

**Mögliche Fehler (Verdacht, nicht ausgeführt)**
- `.heic` in der Dateiliste (DocScan.cs:35), gelesen wird über `SKBitmap.Decode` (:131). Ob das HEIC kann, ist nicht geprüft; schlägt es fehl, kommt die Meldung „lässt sich nicht als Bild lesen“.
- Ausgefüllte, nicht geglättete Formulare verlassen sich auf `/NeedAppearances = true` (Core/Sys/PdfFormular.cs:159). Ob Werte in jedem Betrachter und im Druck erscheinen, ist nicht geprüft.

**Testlücken**
- Wiederherstellung, Versionen, Autosave sind nur an der Hülle allgemein getestet (AT/ProgramShellUiTests.cs:56, 71), nicht je Studio.
- Claudes direkter Weg ins Dokument (App/MainWindow.ClaudeProgramDoc.cs) ist für keines der acht Studios eigens getestet.
- `reports/audit/ohne-wirkung.tsv` (Stand 09.11.) enthält keine Zeile zu diesen acht Programmen – die Klick-Prüfung ist älter als die Studios (T-0671/T-0672 vom 15.11.), also keine Aussage.
- Formulare: nur selbst gebaute Testformulare (CT/FormulareTests.cs:19-37). Scannen: nur Kunstfotos (Core/Office/DocScan.cs:317). PPTX: nur eigener Leser (CT/DeckDesignTests.cs:107-111).

**Formular statt Editor**
- Bewerbung (Bewerbung.cs:303-375), Verträge (Vertraege.cs:235-255) und Tagesplaner (KreativStudios4.cs:1279-1360) sind Eingabemasken mit Ergebnisansicht. Karten hat eine Zeichenfläche, auf der man aber nur auswählt, nicht gestaltet (Karten.cs:185-188, 274). Präsentationen gestalten ist ein Textfeld mit Vorschau (DeckDesign.cs:222-238).
- Echte Arbeitsflächen mit direkter Bearbeitung: PDF-Werkstatt (PdfWerkstatt.cs:531-587), Formulare (Formulare.cs:266, 351), Scannen (DocScan.cs:500-520).


### Business & Geld

**Doku ≠ Code**
- `Core/PromptAction/CapabilityCatalog.cs:34` (POD): „Es ist kein Anbieter angeschlossen. Trendo stellt nichts online und bestellt nichts.“ – im Programm sind Printful, Printify, Netlify und Stripe verdrahtet, inklusive Shop online stellen (`App/MainWindow.PodShop.cs:88-97`) und kostenpflichtiger Freigabe (`:141-152`). Dieselbe veraltete Aussage in `StudyPromptAdapter.cs:183, 213`, `StudioKnowledge.cs:436-437` („von Hand“, „Nichts wird bestellt oder eingestellt“), im Kopfkommentar `App/MainWindow.PodStudio.cs:16-17` und im Hilfetext `PodStudio.cs:60` („Trendo stellt nichts online“). Claude bekommt damit falsches Fachwissen über das Studio.
- `katalog.json` (POD, ab Zeile 4043): Werkzeugliste nennt nur drei Exporte, Status „IN_PROGRESS / Test NOT_RUN“; Designer, Printful, Printify, Kittl fehlen in der Beschreibung.
- `App/StudioCatalog.cs:35` und `StudioKnowledge.cs:396`: „Kredite und Steuern im Blick“ – es gibt keinen Kredit-Rechner; `StudioKnowledge.cs:400-401` räumt das selbst ein. Versprechen im Studio-Satz ohne Funktion.
- `StudioKnowledge.cs:407` nennt für SPEC-11 das Format `.pdf`; Rechnungen entstehen nur als HTML (`Core/Finance.cs:173`). `katalog.json:3855-3864` nennt für `finanzen` kein PDF und kein XML, obwohl XRechnung-XML geschrieben wird.
- `CapabilityCatalog.cs:35` (SPEC-11): „Zahlung und Buchung sind nicht angeschlossen“ – der Knopf „Rechnung erstellen“ bucht sehr wohl eine Einnahme (`W5Finance.cs:68`); gemeint ist nur die KI-Aktion.
- `katalog.json:3833-3839` listet für `finanzen` das Werkzeug „Datei wählen …“; auf der Geld-Seite ist der Kontoauszug ein getippter Pfad (`W5Finance.cs:43`). Herkunft des Katalogeintrags nicht geprüft.

**Fassaden / Scheinfunktionen**
- „Als Webseite speichern“ unter „Online verkaufen“ erzeugt eine Seite ohne Kaufmöglichkeit (`Core/FinanceExtra.cs:306-321`). Die Seite sagt das ehrlich, der Studio-Name verspricht mehr.
- Zahlart „Karte“ in der Kasse ist nur ein Vermerk (`Core/CashRegister.cs:50, 63`), kein Kartenterminal.
- „Gründung Schritt für Schritt“ ist eine Abhakliste (`W5Business.cs:82-93`, `FinanceExtra.cs:324-336`).
- Lokale POD-Produktvorschau ist eine Umrisszeichnung (`PodStudio.cs:119-133`).

**Fehler und Risiken im Code**
- Normale Rechnung ohne Steuernummer/USt-IdNr.: `W5Finance.cs:64-65` übergibt kein `TaxId`; ebenso Rechnung aus Bestellung `Core/FinanceExtra.cs:260`. Pflichtangabe nach § 14 Abs. 4 UStG fehlt damit in jeder HTML-Rechnung (nur die XRechnung prüft sie: `Core/ERechnung.cs:46`).
- Rechnungsnummern werden aus den vorhandenen Dateinamen abgeleitet (`Core/Finance.cs:172`, `:102-107`): wird die letzte Rechnungsdatei gelöscht, wird ihre Nummer neu vergeben.
- `W5Finance.cs:60-73` und `W5ERechnung.cs:28-32`: erst wird die Rechnungsdatei geschrieben, dann gebucht; schlägt das Buchen fehl, existiert die Rechnung ohne Buchung (kein gemeinsamer Abschluss).
- `Core/Finance.cs:139`: `Ledger.All()` ohne Fehlerbehandlung – eine beschädigte `buchungen.json` lässt die ganze Geld-Seite scheitern (bei Budgets und POD-Listen ist das abgefangen: `FinanceExtra.cs:130`, `Core/Pod/PodShop.cs:31-32`).
- `Core/CashRegister.cs:64, 79`: Bestand wird mit `(int)Math.Ceiling(Qty)` geändert, die Lagerbewegung aber mit der genauen Menge gebucht (`:66`) – bei Teilmengen (z. B. 0,5) laufen Bestand und Bewegungen auseinander.
- `Core/CashRegister.cs:62`: Bon-Nummer = Anzahl der heutigen Bons + 1; kein Schutz, falls ein Bon-Eintrag fehlt oder zwei Fenster gleichzeitig kassieren (nicht getestet).
- `App/MainWindow.PodPrintful.cs:47` in den Tests: `PodPngOverride` ersetzt sogar die Druckdatei durch drei Bytes – der Weg „echter Entwurf → Upload → Mockup“ ist auch lokal nie als Ganzes gelaufen.
- POD-Bilder als absolute Pfade im Dokument (`Core/Pod/PodDesign.cs:10-11`); `PodUseAsMotif` legt die Motivdatei im Datenordner ab, nicht neben dem Dokument (`PodDesigner.cs:97`).

**Nicht als Lücke gewertet (entschieden)**
- Zertifizierte Kasse mit TSE: im Funktionskatalog „bewusst nicht“ (VK-12), Hinweis auf der Seite `Kasse.cs:183` und im Tagesabschluss `CashRegister.cs:145`.
