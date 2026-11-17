# Bericht T-0705: Büro-Etappe ausgebaut – Klick bis Datei, Echtlauf mit Fremdprogrammen, Absturzschutz, Ziehen und Rechtsklick

**Auftrag:** `tasks/T-0705-buero-ausbau.md` · Freigabe im Chat 17.11.2026: „Büro-Etappe mit aufnehmen und ausbauen“.
**Branch:** `claude/T-0705-buero-ausbau` (Basis `claude/T-0685-b1-integration` @ f82933c40) · Arbeitskopie: eigener Temp-Ordner, nach dem Push gelöscht.
**Ausgangslage (T-0702):** Kriterien 1, 2, 4 NICHT GETESTET, 3, 5, 6 TEILWEISE; Rechtsklick/Ziehen nicht geprüft, kein Beleg unter `reports/`.

## 1. Klick bis Datei – je Studio vom leeren Zustand über den ersten Schritt (Stufe „Einfach“)
Neue Klasse `BueroAusbau_Ui` (echte Mausklicks über `RealClick`, Tippen über `KeyTextInput` + Tab; ersetzt ist nur die Dateiauswahl des Systems, sie liefert echte Dateien):

| Studio | Weg | Prüfung der Datei |
|---|---|---|
| Bewerbung | „Leerer Lebenslauf“ → „Namen eintragen“ (Fokus im Namensfeld) → Name/Beruf getippt → Mehr → Export PDF, Word | PDFium-Text bzw. DOCX-Text enthält den Namen |
| Karten | „Geburtstag“ → „Text anpassen“ (Eigenschaften klappen auf) → Text getippt → Druck-PDF | Text auf der Karte im PDF |
| Verträge | „Leerer Ordner“ → „Ersten Vertrag eintragen“ → Name, Anbieter, Nummer, Beginn getippt → Erinnerungen (.ics), Kündigung als PDF | ICS mit VEVENT + VALARM; PDF enthält Kundennummer und Anbieter |
| PDF-Werkstatt | „PDFs zusammenfügen“ → „Dateien hinzufügen …“ → „Ausführen“ | 3 Seiten in der richtigen Reihenfolge |
| Formulare | „Formular öffnen“ → „Formular öffnen …“ → zwei Felder getippt → „Ausgefüllt speichern“, „Fertig (geglättet)“ | Feldwerte gelesen; geglättet: keine Felder mehr, Wert im Seitentext |
| Präsentation gestalten | „Leere Gliederung“ → „Stichpunkte schreiben“ (Platzhalter markiert) → getippt → „Gestalten“ → PPTX | 2 Folien, Titel stimmt |
| Scannen | „Neuer Scan“ → „Fotos hinzufügen …“ → „Als PDF ablegen“ | PDF mit 1 Seite |

## 2. Fremddatei-Rundreise mit echten Werkzeugen (Belege `reports/T-0705/`)
Werkzeuge auf diesem PC geprüft (nichts installiert): LibreOffice 26.2.4.2 (Trendo-Werkzeugordner, inkl. eigenem Python/UNO), qpdf 12.4.2, Tesseract 5.4.0 (deu/eng), PDFium (vorhandene Bibliothek), pdftotext (Poppler aus Git für Windows, nur wenn im PATH). Klasse `BueroEchtlauf_Ui`, Sammler `python tools/echtlauf/buero/t0705.py` → `reports/T-0705/echtlauf.log` (Rohausgabe, Exit 0, 6/6 bestanden), je Studio `*.log` (jeder Befehl mit Exit-Code und Ausgabe, jede Datei mit SHA-256) und die echten Dateien, `SHA256SUMS` über 32 Dateien (`sha256sum -c SHA256SUMS`: alle OK; `.gitattributes`: `reports/T-0705/** -text`); Benutzerpfade ersetzt (`C:\Users\<user>`), Prüf-Grep leer.

| Studio | Fremddatei (von LibreOffice erzeugt) | in Trendo | Prüfung durch Fremdprogramm |
|---|---|---|---|
| PDF-Werkstatt | 3-seitiges PDF (Writer) | per **Ablegen** geholt, Seite 3 per **Ziehen** nach vorn, **Rechtsklick** drehen/löschen, Ausführen | qpdf `--check` ok, `--show-npages` 2, `/Rotate 90` in qpdf-JSON; PDFium-Reihenfolge; LibreOffice öffnet → PNG; Original-SHA unverändert |
| Formulare | PDF mit echten AcroForm-Feldern (Writer-Formular über UNO, `tools/echtlauf/buero/lo_fremd.py`) | erster Schritt, Felder getippt, Ankreuzfeld geklickt, speichern + geglättet | qpdf-JSON: `Nachname=u:Muster`, `Wohnort=u:Musterstadt`, `Zustimmung=/Yes`; geglättet keine Felder mehr, Text auf der Seite; LibreOffice öffnet beide |
| Scannen | Seitenbild (PNG) eines Briefs | per **Ablegen**, „Durchsuchbare PDF“ mit **echtem Tesseract** | qpdf ok, 1 Seite; Textebene: „Stadtwerke Musterstadt Rechnung Nummer 4711 Betrag 128,40 Euro“; LibreOffice öffnet |
| Präsentation | PowerPoint (Impress über UNO: 3 Folien, Punkte, Notiz) | per **Ablegen** übernommen (Titel, Punkte, Sprechernotiz), Folie per **Ziehen** umsortiert, PPTX exportiert | LibreOffice öffnet das Trendo-PPTX → PDF, qpdf: 3 Seiten, neue Reihenfolge im Text |
| Bewerbung | Zeugnis-PDF (Writer) | per **Ablegen** als Anlage, Export Word/PDF/Mappe | LibreOffice öffnet DOCX → PDF mit Namen; qpdf ok für Lebenslauf und Mappe; Zeugnis ist letzte Seite der Mappe |
| Karten, Verträge | – (keine Fremddatei) | Druck-PDF, Kündigung per Rechtsklick, ICS | qpdf ok, LibreOffice öffnet beide PDFs; ICS mit VALARM nur protokolliert (kein Kalenderprogramm auf diesem PC) |

**Befund qpdf:** Alle PDFs, die Trendo mit PDFsharp 6.2.4 schreibt (Lebenslauf, Karte, Formular-Ausgabe, Kündigung …), liefern bei `qpdf --check` Exit 3 mit nur der Warnung „input stream is complete but output may still be valid“ (Flate-Strom ohne Abschluss). Struktur, Seiten und Inhalt sind gültig; der Test lässt **nur genau diese** Warnung zu und protokolliert sie. Ursache liegt im Kompressor der PDF-Bibliothek, nicht in den Büro-Studios → offener Punkt.

## 3. Absturzschutz
`BueroAusbau_Ui.Crash_all_seven`: in allen sieben Studios eine Änderung per Klick, dann der feste Sicherungstakt (`ProgramAutosaveEvery` = 20 s, geprüft ≤ 20 s), Fenster „stirbt“ ohne Speichern (Sperren bleiben liegen wie nach einem beendeten Prozess). Das neue Fenster zeigt je Studio „Wiederherstellen“ und bringt jede Änderung zurück. Genutzt: Hülle/`AtomicFile`/Wiederherstellen-Karte, keine eigene Logik.
**Gefundener Fehler (rot → grün):** „Anderes Design“ in *Präsentation gestalten* warf intern („Knoten hat schon ein Elternteil“: ein `JsonNode` für fünf Eigenschaften). Das Design wechselte trotzdem, aber **ohne Rückgängig-Schritt und ohne „geändert“** – nach einem Absturz war die Änderung weg. Fix in `DdSetTheme` (eigener Wert je Eigenschaft). Der Absturztest war auf dem alten Stand rot.

## 4. Rechtsklick und Ziehen/Ablegen (gemeinsame Hilfen `MainWindow.BueroAusbau.cs`: `BueroReorder`, `BueroFileDrop`, `BueroMenu`)
- **PDF-Werkstatt:** Seiten per Maus umsortieren (gewählte Seiten wandern gemeinsam, `PwMoveTo`, rückgängig machbar), Rechtsklick: drehen links/rechts, an den Anfang/ans Ende, Seite löschen; PDFs/Bilder auf die Fläche **und auf die Karte „erster Schritt“** ziehen.
- **Präsentation gestalten:** Folie in der Leiste ziehen = Abschnitt der Gliederung verschieben (Kern `DeckDesign.MoveSection`/`SectionOfSlide`/`RemoveSection`, jede Zeile bleibt erhalten, aufgeteilte Folien bleiben zusammen); Rechtsklick: nach vorn/hinten, Folienart automatisch, Folie entfernen; .pptx/.odp/.txt/.md ablegen oder neu „Präsentation öffnen …“ (Erweitert) übernimmt Titel, Punkte, Notizen.
- **Scannen:** Seiten ziehen, Rechtsklick (drehen, Kanten finden, ganzes Bild, vor/zurück, entfernen), Fotos ablegen.
- **Formulare:** PDF ablegen = öffnen. **Verträge:** Rechtsklick an der Zeile (Kündigung schreiben/als PDF, gekündigt, Dokument anhängen, löschen), Dokument auf eine Zeile ziehen = an diesen Vertrag hängen. **Bewerbung:** Zeugnis/Zertifikat ablegen = Anlage (ein Foto im Lebenslauf = Bewerbungsfoto). **Karten:** Foto ablegen = Bild der Seite, CSV = Gästeliste.
- Hinweis im ersten Schritt nennt das Ziehen („… oder zieh sie hierher“).
Tests über echte Fenster-Ereignisse im Headless-Fenster: `MouseDown/MouseMove/MouseUp` (Ziehen), Rechtsklick öffnet das Menü (`IsOpen` geprüft), `DragEnter/DragOver/Drop` mit Avalonias eigener Datei-Klasse (Treffer an der Ablegestelle geprüft). Unpassende Datei (.txt in der PDF-Werkstatt) wird mit Hinweis abgelehnt.

## 5. Fachfunktionen geprüft
PDF-Werkstatt: drehen/löschen/zusammenführen/teilen waren da (jetzt zusätzlich per Maus). Formulare: AcroForm ausfüllen und flach speichern waren da – jetzt mit fremdem LibreOffice-Formular und qpdf belegt. Verträge: Kündigungsfrist-Erinnerung war als ICS mit VALARM da (belegt; Kalender-Import nicht prüfbar). Präsentation: Notizen (`> `) und Designs (Folien-Master-Ersatz) waren da; neu: vorhandene PPTX/ODP übernehmen, Folien umsortieren/entfernen, Design-Fehler behoben.

## Prüfungen (gezielt, kein Volltest)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core: DeckDesignTests (neu `Move_sections`), BueroTests, DocScanTests, FormulareTests, PdfWerkstattTests, PromptActionTests, VorbildAnalysenTests, NoFixedUser → **150/150 bestanden**.
- App (`Trendo.App.Tests.exe -class …`): BueroAusbau_Ui 12/12, BueroEchtlauf_Ui 6/6 (echte Werkzeuge), BueroStudios_Ui 5/5, PdfWerkstatt_Ui 8/8, DeckDesign_Ui 5/5, DocScan_Ui 4/4, Formulare_Ui 4/4, UebersichtEinfach_Ui 2/2, ProgramHeadDesign_Ui 6/6, ProgramCrashUiTests 6/6, PromptBarTests 6/6, StudioFolds_Ui 3/3, W0b_Look 3/3, EveryButton_Alltag 1/1 (EveryButton_AllPages: 1 übersprungen – eigene Bedingung des Tests, nicht von T-0705).
- Rot-Grün: `BueroAusbau_Ui` auf altem `src/` → 5 von 12 rot (Ziehen/Ablegen/Rechtsklick ×4, Absturz wegen des Design-Fehlers); die 7 Klick-bis-Datei-Tests waren schon grün (sie belegen vorhandene Wege, neu ist der Nachweis).
- Funktions-Baseline `python -m unittest discover -s tests/claude_handoff`: 11/11 OK.
- Bilder (Render 1600 × 1000): Präsentation gestalten (Professionell, „Präsentation öffnen …“ in „Weitergeben“, eine goldene Hauptaktion), PDF-Werkstatt (Einfach, erster Schritt mittig, Dunkel-Gold).

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis über die Oberfläche: **VOLLWERTIG** (alle sieben, Klicktests).
2. Fremddatei importieren/bearbeiten/exportieren, im Fremdprogramm geöffnet: **VOLLWERTIG** für PDF-Werkstatt, Formulare, Scannen, Präsentation, Bewerbung; **TEILWEISE** für Karten (nur Export im Fremdprogramm, keine Fremddatei importiert) und Verträge (Kündigungs-PDF ja, ICS nicht in einem Kalender geöffnet).
3. Prozessabbruch ≤ 30 s: **TEILWEISE** – alle sieben über „Fenster stirbt ohne Speichern“ + Sicherungstakt 20 s nachgewiesen; der echte Prozessabbruch mitten im Schreiben ist nur allgemein für die Hülle im Kern-Test `ProgramCrashTests` geprüft, nicht je Büro-Studio.
4. Claude/KI-Fachaufgabe: **NICHT GETESTET** (nicht Teil dieses Pakets).
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG**.
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: **TEILWEISE** – Ziehen/Ablegen in allen sieben, Rechtsklick in PDF-Werkstatt, Präsentation, Scannen, Verträge (getestet); Einfach unverändert grün; **keine neuen Tastenkürzel** und kein Kürzel-Test für die Büro-Studios.

## Was NICHT geprüft wurde
Volltest der drei Testprojekte; Api-Tests; echter Prozess-Kill je Büro-Studio; Kalender-Import der ICS; Karten mit fremdem Bild/CSV aus einem Fremdprogramm; KI-Aufgaben; Rechtsklick in Bewerbung/Karten/Formulare (dort nur Ablegen); Ziehen in einem echten (nicht Headless-)Fenster mit dem Windows-Explorer.

## Offene Punkte
- PDFsharp-Flate-Warnung bei qpdf (Exit 3, nur „input stream is complete …“) für alle Trendo-PDFs – Bibliothek/Kompressor, außerhalb dieses Pakets.
- Für die Hülle (Design-Paket 3, nicht angefasst): Die Karte „erster Schritt“ nimmt Ablegen nur an, weil `BueroFileDrop` sie nach dem Einhängen findet (Name `prog-first`). Ein Haken `ProgramSpec.Drop` in der Hülle wäre sauberer und für alle Programme nutzbar. In „Einfach“ liegen die Exporte hinter „Mehr“ – die Klicktests öffnen dafür „Mehr“.
- Tastenkürzel für Seiten/Folien (z. B. Entf, Strg+Pfeil) fehlen in den Büro-Studios.

## Berührte Dateien (für die Zusammenführung)
`src/Trendo.App/MainWindow.BueroAusbau.cs` (neu), `src/Trendo.App/MainWindow.{Bewerbung,Karten,Vertraege,PdfWerkstatt,Formulare,DeckDesign,DocScan}.cs`, `src/Trendo.Core/Office/DeckDesign.cs`, `tests/Trendo.App.Tests/BueroAusbauUiTests.cs` (neu), `tests/Trendo.App.Tests/BueroEchtlaufUiTests.cs` (neu), `tests/Trendo.Core.Tests/DeckDesignTests.cs`, `tools/echtlauf/buero/lo_fremd.py` (neu), `tools/echtlauf/buero/t0705.py` (neu), `reports/T-0705/**` (neu), `tasks/T-0705-buero-ausbau.md`, `tasks/T-0705-buero-ausbau.bericht.md`, `.gitattributes` (eine Zeile `reports/T-0705/** -text`).

## Status
Umgesetzt, gezielte Tests grün, Echtlauf 6/6 mit echten Werkzeugen; gepusht nur auf `claude/T-0705-buero-ausbau`.
