# Bericht T-0780: Referate & Hausarbeiten, Zeitleisten (Etappe 0.9.113 „Wissen & Lernen“)

**Auftrag:** `tasks/T-0780-referate-zeitleisten.md` (Freigabe 26.11.2026) · **Branch:** `claude/T-0780-referate-zeitleisten` (Basis main 0.9.112 = 184fcbbfc) · eigene Kopie in `%TEMP%\trendo-auftrag-T-0780`.

## Was war → was ist
| Punkt | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| Referate & Hausarbeiten | fehlte (`docs/audit/2026-11-26-neubewertung/fehlende-studios.md`, Wissen & Lernen Rang 2: nur Teile in `quellen-programm`, `text`, `praesentation`) | Studio `STUDIO-REFERAT-01`, Programm `referate-programm` in der Hülle: Kette Thema → Gliederung → Text → Literatur → Folien; links die Gliederung (Ziehen, Rechtsklick), Mitte der Schritt, rechts Formvorgaben. Zitate als Verweis `[@kennung, S. n]` → Kurzbeleg nach APA 7/Harvard/DIN ISO 690 (nah, `CiteKit`), Literaturverzeichnis von selbst. Ausgabe DOCX, ODT, PDF (Deckblatt, Inhaltsverzeichnis, Seitenzahlen), PPTX und ODP (Folien mit Sprechernotizen), Markdown, BibTeX. Quellen aus „Recherche mit Quellen“ (offen oder .tdoc), RIS/BibTeX (auch per Ablegen) | `WissenStudios_Ui` (6 Referate-Tests), `WissenStudiosTests` (Gliederung, Zitate, Ausgaben, Folien), Fremdnachlauf LibreOffice |
| Zeitleisten | fehlte (Rang 6, „neuer Kern“) | Studio `STUDIO-TIMELINE-01`, Programm `zeitleisten-programm`: Ereignisse und Zeiträume in Spuren, Daten auch v. Chr. (ohne Jahr 0), ungefähr („ca.“, „um“), Jahrzehnt/Jahrhundert/Jahrtausend; Zeichenfläche mit Mausrad-Zoom, Verschieben, Eintrag in der Zeit ziehen, Doppelklick legt an, Entf, Rechtsklick, Bilder (verkleinert eingebettet). Ausgabe SVG, PNG, PDF; CSV/JSON ein/aus, TimelineJS-Tabelle und -JSON einlesbar | `WissenStudios_Ui` (4 Zeitleisten-Tests), `WissenStudiosTests` (Daten, Zeilen, CSV/JSON, Layout, SVG/PDF), Fremdnachlauf Inkscape/LibreOffice |
| ODP schreiben | Trendo las ODP nur (`SlideFiles.Save` kennt nur PPTX/PDF) | `SlideFiles.WriteOdp` (neu, `SlideOdp.cs`): Folien, Hintergrund, Textfelder, Aufzählung zwei Ebenen, Formen, Bilder, Notizen (Notizseite im Master, damit Impress sie als Notizen erkennt) | `WissenStudiosTests.Slides`, Fremdnachlauf Impress (ODP → PPTX → Trendo liest Notizen) |
| KI-Fachaktionen | – | `referat.gliederung`, `referat.abschnitt`, `zeitleiste.ereignisse` über `ProgramSpec.KiActions` (T-0774): Vorschau ändert nichts, „Ja, behalten“ schreibt ins offene Dokument, genau ein Rückgängig-Schritt. Keine erfundenen Quellen: Verweis ohne Quelle, freier Kurzbeleg „(Name, Jahr)“ ohne passende Quelle und neue Quellen per `dokument.setzen` (`KiLocked = ["sources"]`) werden abgelehnt; Ausgabe mit Verweis ohne Quelle ist gesperrt | `Referat_ki_outline`, `Referat_ki_section_no_invented_sources`, `Timeline_ki_events` (KI ersetzt); **echt:** `Real_local_ki` mit qwen3:14b |

## Befunde aus dem Echtlauf → behoben (gemeinsame Bausteine, klein)
| Befund (Rohlog) | Behebung | Test |
|---|---|---|
| qwen3:14b rief statt der Fachaktion die Menüband-Befehle „Ereignis“/„Zeitraum“ mit eigenen Angaben → vier leere „Neues Ereignis 2026“ | `ProgramCmd.NoKi` (neu, Standard aus): Befehl wird der KI nicht angeboten; alle Befehle der beiden Studios außer „Stil“ so markiert | `Timeline_ki_events`, `Referat_ki_outline` (Befehle stehen nicht in der KI-Anweisung) |
| Nach dem Abschnitt hängte die KI „Formvorgaben prüfen“ an → Abbruch (eine Fachaktion als letzter Schritt) | wie oben (Prüfen nicht für die KI); Hinweis „genau eine Aktion“ in `KiHint` | Echtlauf bestanden |
| qwen2.5:7b/qwen3 lieferten „ereignisse“ als Liste von Texten → Rückfrage statt Plan | `AssistPlanner.FromKi`: Liste von Texten für einen Text-Wert = Zeilen | `WissenStudiosTests.Planner_list_and_long_text` |
| Text-Werte aus KI-Plänen fest auf < 2000 Zeichen begrenzt (längere Abschnitte gingen still verloren) | `AssistParam.Max` (Standard 2000, unverändert für alle anderen); Abschnitt 60 000, Gliederung 20 000, Ereignisse 50 000 | dito |
| KI nannte den Abschnitt mit seiner Kennung („s4“) bzw. die Spur mit ihrer Kennung („t-allg“) | Abschnitt und Spur auch über die Kennung gefunden | Echtlauf; `WissenStudiosTests.Lines` (Spur) |
| Vorschau sagte „Die neue Datei entsteht erst bei „Ja, behalten““ – falsch für Fachaktionen ohne Datei | `ProgramKiAction.OnApplyNote` (optional) | `Referat_ki_outline` |

## Echtlauf mit lokaler KI (nichts installiert, Claude nicht beteiligt)
Ollama 127.0.0.1:11434, qwen3:14b (automatische Wahl nach T-0703), Bedienung über die Oberfläche. Beleg `reports/T-0780/ki/` (Rohlog, Protokoll, DOCX, SVG). Ergebnis **bestanden** (1/1, 864 s):
- Gliederung „Bienen …“: 5 Kapitel mit 15 Unterpunkten ins Dokument, ein Rückgängig-Schritt, Rückgängig stellt her.
- Abschnitt „Mulchen“ im Beispiel: Text mit zwei Verweisen auf die vorhandene Quelle `q-buch`, keine unbekannte Quelle; DOCX ausgegeben.
- Zeitleiste aus Fließtext: 44 v. Chr., 800, 1618–1648 (Zeitraum), ca. 1450 – richtig gelesen, ein Rückgängig-Schritt, SVG ausgegeben.
- **Ehrlich:** (1) Der Grafikspeicher ist mit anderen Programmen geteilt; qwen3:14b lief gemessen mit etwa 5 Token/s, die erste Antwort brauchte bis 463 s. Für den Echtlauf ist die Wartezeit der Kette im Test auf 8 min gesetzt (Produkt: 90 s unverändert). Mit 90 s fällt Trendo auf kleinere Modelle zurück – qwen2.5:0.5b lieferte dann keinen brauchbaren Plan. (2) Die KI schrieb „bis zu 50 %“ mit Verweis auf `q-buch` – die Quelle ist echt, ob sie die Zahl belegt, prüft Trendo nicht (das kann die Person mit „Mit Claude prüfen“ in „Recherche mit Quellen“). (3) Fünf Läufe davor scheiterten an den oben behobenen Befunden; die Spur-Kennung wurde nach dem letzten Echtlauf behoben (dort entstand noch eine zusätzliche Spur „t-allg“), nur mit Ersatz-KI und Core-Test nachgeprüft.

## Fremdprogramme (Abnahme 2)
Test `T0780_Fremdnachlauf` (3/3 bestanden, echte Werkzeuge, nichts ersetzt), Belege `reports/T-0780/fremd/` mit Rohlog:
- **Referat:** fremde RIS-Datei (Zotero-Form) → Trendo-DOCX/ODT → LibreOffice (Kopie aus T0776_LoKopie, eigenes Profil) liest Deckblatt, „Inhaltsverzeichnis“, Kapitel, Kurzbelege (DIN), Literaturverzeichnis; LibreOffice-PDF ≥ 4 Seiten, pdftotext liest „Seite 2 von N“.
- **Folien:** PPTX und ODP → Impress-PDF mit einer Seite je Folie und allen Titeln; Impress speichert als ODP/PPTX, Trendo liest Titel und Notizen wieder.
- **Zeitleiste:** fremde TimelineJS-Tabelle → SVG: Inkscape 1.4 findet alle Texte (Anzahl = Trendo), exportiert einfaches SVG mit „753 v. Chr.“, „15.03.44 v. Chr.“ und PNG 1400 px; Trendo-PDF per pdftotext lesbar; Trendo-CSV in LibreOffice Calc (ODS und zurück) mit allen Zeilen.

## Rot-Grün
Neue Testdateien gegen main 184fcbbfc (eigener Worktree, danach entfernt): übersetzen nicht (Core 23, App 3 Fehler – Typen fehlen), `reports/T-0780/rot-alter-stand.txt`. Endstand grün (Zahlen unten).

## Tests (gezielt, kein Volltest)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core (Filter über die berührten Klassen: PromptActionTests, StudioTeamsTests, VorbildAnalysenTests, T0777*, KreativKits4Tests, CiteFormatsTests, WissenStudiosTests, DeckDesign, Slide*, NoFixedUser, Evidence, DecisionRegister, Assist*, T0223, PdfWerkstatt, DocScan): **236 bestanden, 2 übersprungen, 0 Fehler**. `T0780_Fremdnachlauf` 3/3 (echt), `WissenStudiosTests` 39/39.
- App (34 berührte Klassen, Rohlog `reports/T-0780/app-klassen.txt`): **Total 405, Failed 3, Errors 1, Skipped 3.** Die 3 Fehlschläge hängen nur am fehlenden requirements.yaml-Eintrag (`StartMenu_StudioFenster.Jedes_Studio`, `StudioFolds_Ui` „63 Studios“ ↔ 61, `Sys13_Qualitaet_Ui.Modules`). „Errors 1“ meldet der Läufer ohne Testnamen und ohne Meldung (im Rohlog nur „Waiting 10 seconds for foreground threads to exit“) – Ursache nicht geklärt; in den beiden Läufen davor mit denselben Klassen stand „Errors 0“. Übersprungen: die beiden Echtläufe ohne Variable und T0774-Echtlauf. Gesondert: `WissenStudios_Ui` 11/11 inkl. Echtlauf (mit `TRENDO_T0780_ECHT`).
- `docs/improvement-42/studio-capability-matrix.md` mit `TRENDO_UPDATE_MATRIX=1` neu erzeugt.

## Bild geprüft
Trendo.Render (eigener `TRENDO_RENDER_ROOT`): Referate (Profi, Beispiel) und Zeitleisten (Einfach und Profi, Beispiel) angesehen – Dunkel-Gold, eine goldene Hauptaktion (Abschnitt bzw. Ereignis), erster Schritt im Leerzustand, Achse „500 v. Chr. … Chr. Geb. … 1500“, Beschriftungen ohne Überdeckung, am rechten Rand nach links gesetzt. Studio-Bilder mit ComfyUI + FLUX.2 klein lokal erzeugt (`tools/studio_bilder.py`, ComfyUI danach beendet) und angesehen.

## Abnahmekriterien
| Nr. | Referate & Hausarbeiten | Zeitleisten |
|---|---|---|
| 1 Leere Vorlage → echtes Ergebnis | VOLLWERTIG (`Referat_click_to_files`: Klickweg bis DOCX/ODT/PDF/PPTX/ODP) | VOLLWERTIG (`Timeline_click_to_files`: bis SVG/PNG/PDF/CSV/JSON) |
| 2 Fremddatei ein, Export im Fremdprogramm | VOLLWERTIG (Zotero-RIS → DOCX/ODT/PPTX/ODP in LibreOffice) | VOLLWERTIG (TimelineJS-CSV → SVG in Inkscape, CSV in Calc) |
| 3 Absturz ≤ 30 s | TEILWEISE – über die Programm-Hülle (Autosave 20 s, Notsicherung, Wiederherstellung, AtomicFile der Hülle); kein eigener Absturztest für diese Studios | TEILWEISE – dito |
| 4 KI-Fachaufgabe im offenen Dokument, rückgängig | VOLLWERTIG (Ersatz-KI-Tests + Echtlauf qwen3:14b) | VOLLWERTIG (dito) |
| 5 Test vom Klick bis zur Datei | VOLLWERTIG | VOLLWERTIG |
| 6 Einfach/Profi, Kürzel, Rechtsklick, Ziehen | TEILWEISE – Einfach/Profi, Rechtsklick, Ziehen (Gliederung, Dateien) getestet; Tastenkürzel nur die der Hülle (Strg+Z/S …), keine eigenen | VOLLWERTIG – Einfach/Profi, Rechtsklick, Ziehen (Einträge, Ansicht, Dateien), Tasten +/−/←/→/Entf getestet |

## Anforderungen (Clean-Room, `docs/nachbau/…/anforderungen.md`)
- Referate: R-01 … R-21 erfüllt (Prüfung je Zeile in der Datei), R-18 („In Trendo Text/Präsentation öffnen“) gebaut, aber nicht eigens getestet → TEILWEISE; R-22 Deckblatt ohne Seitenzahl, R-23 Fußnoten-Zitierweise → FEHLT (Stufe 2).
- Zeitleisten: Z-01 … Z-19 erfüllt; Z-20 eigene Kalender/Beziehungen, Z-21 Erzählansicht → FEHLT (Stufe 2).

## Vorbild-Tabellen
In `docs/nachbau/referate/anforderungen.md` (Citavi, Zotero+Textverarbeitung, Scrivener, Notion, TUM-Vergleich) und `docs/nachbau/zeitleisten/anforderungen.md` (TimelineJS, Preceden, Aeon Timeline, TimeToast) – mit Quelle und Datum 26.11.2026. Ehrlich: meist Vergleichsportale und Herstellerseiten; für Preceden/Aeon keine öffentliche Angabe zu Daten v. Chr. gefunden.

## requirements.yaml-Vorschlag
(Datei für Agenten gesperrt – nicht geändert. Ohne diese Einträge fehlen beide Studios in der Übersicht und im Startmenü; siehe „Offen“.)
```yaml
- id: STUDIO-REFERAT-01
  name: Referate & Hausarbeiten
  approved_expansion_functions: '0'
  approval_status: APPROVED_SPEC
  implementation_status: IN_PROGRESS
  test_status: NOT_RUN
  evidence: Freigabe im Chat 26.11.2026 (Etappe 0.9.113 Wissen & Lernen), T-0780
- id: STUDIO-TIMELINE-01
  name: Zeitleisten
  approved_expansion_functions: '0'
  approval_status: APPROVED_SPEC
  implementation_status: IN_PROGRESS
  test_status: NOT_RUN
  evidence: Freigabe im Chat 26.11.2026 (Etappe 0.9.113 Wissen & Lernen), T-0780
```

## Was NICHT geprüft wurde
- Öffnen in Microsoft Word/PowerPoint (nicht vorhanden) – nur LibreOffice.
- Inhaltsverzeichnis mit Seitenzahlen in Word (Feld wird beim Öffnen aktualisiert; LibreOffice zeigt die eingetragenen Überschriften).
- Claude-Echtlauf (Kontingent, laut Auftrag nicht).
- Bild der Studios in der Übersicht (erscheint erst mit dem requirements.yaml-Eintrag).
- Volltest (macht die Hauptsitzung).

## Offen / Risiken
- **requirements.yaml:** bis zum Eintrag schlagen erwartbar fehl: `Sys13_Qualitaet_Ui.Modules` (Seiten ohne Modul), `StudioFolds_Ui` Seitenzähler („63 Studios“, ohne Eintrag 61), `StartMenu_StudioFenster.Jedes_Studio`. Die Zählertests sind auf den Stand nach dem Eintrag gesetzt.
- Echtlauf-Wartezeit: mit 90 s und geteiltem Grafikspeicher fällt die KI auf kleine Modelle zurück, die keinen Plan liefern; nach einer KI-Nachfrage blieb die Kette einmal ohne Phase stehen (gemeinsamer Baustein, nicht in diesem Paket behoben).
- Lange KI-Pläne schieben „Ja, behalten“ bei 1100 px Fensterhöhe aus dem Bild (gemeinsame Vorschau; Echtlauf mit 1600 px).
- Gemeinsame Dateien berührt (am Ende der Listen): StudioCatalog, StudioIcons, CapabilityCatalog, StudioProfiles, StudioTeams, StudioKnowledge, PromptChips, IntelligencePromptModel, W6 (Init-Zeile), `tools/studio_bilder.py`, `Daten/einfach-vorher-ausgeblendet.txt`, `.gitattributes`; dazu klein in ProgramShell (`ProgramCmd.NoKi`, `ProgramKiAction.OnApplyNote`), Assist.cs (NoKi-Filter, OnApplyNote) und Core `Programs/Assist.cs` (`AssistParam.Max`, Liste → Zeilen).

## Berührte Dateien
Neu: `src/Trendo.Core/Wissen/Referat.cs`, `src/Trendo.Core/Wissen/Zeitleiste.cs`, `src/Trendo.Core/Office/SlideOdp.cs`, `src/Trendo.Core/PromptAction/WissenPromptAdapters.cs`, `src/Trendo.App/MainWindow.Referate.cs`, `src/Trendo.App/MainWindow.Zeitleisten.cs`, `src/Trendo.App/TimelineCanvas.cs`, `src/Trendo.App/Assets/studios/STUDIO-REFERAT-01.jpg`, `…/STUDIO-TIMELINE-01.jpg`, `tests/Trendo.Core.Tests/WissenStudiosTests.cs`, `tests/Trendo.Core.Tests/T0780FremdnachlaufTests.cs`, `tests/Trendo.App.Tests/WissenStudiosUiTests.cs`, `docs/nachbau/referate/anforderungen.md`, `docs/nachbau/zeitleisten/anforderungen.md`, `reports/T-0780/**`, `tasks/T-0780-*.md`.
Geändert: `src/Trendo.App/StudioCatalog.cs`, `StudioIcons.cs`, `MainWindow.PromptChips.cs`, `MainWindow.W6.cs`, `MainWindow.ProgramShell.cs`, `MainWindow.Assist.cs`, `src/Trendo.Core/PromptAction/CapabilityCatalog.cs`, `IntelligencePromptModel.cs`, `StudioKnowledge.cs`, `src/Trendo.Core/W2/StudioProfiles.cs`, `StudioTeams.cs`, `src/Trendo.Core/Programs/Assist.cs`, `tools/studio_bilder.py`, `docs/improvement-42/studio-capability-matrix.md`, `.gitattributes`, Tests `PromptBarTests.cs`, `StudioFoldsUiTests.cs`, `PromptActionTests.cs`, `Daten/einfach-vorher-ausgeblendet.txt`.

## ENTWICKLUNGSBERICHT
- Projekt: Trendo OS · Sprachen: C# (.NET 10, Avalonia 12), Python (Patch-Hilfen, Bildskript) · Rust eingesetzt: NEIN
- Grund: Projekt ist C#; Fachkerne (Datum, Layout, Formate) sind klein und schnell genug, keine messbare Engstelle.
- Tests: siehe oben · Gemessene Verbesserungen: keine Leistungsmessung nötig · Offene Probleme: siehe „Offen“.
