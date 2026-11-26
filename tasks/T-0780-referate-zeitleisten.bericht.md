# Bericht T-0780: Referate & Hausarbeiten, Zeitleisten (Etappe 0.9.113 „Wissen & Lernen“)

**Auftrag:** `tasks/T-0780-referate-zeitleisten.md` (Freigabe 26.11.2026) · **Branch:** `claude/T-0780-referate-zeitleisten` (Basis main 0.9.112 = 184fcbbfc) · eigene Kopie in `%TEMP%\trendo-auftrag-T-0780`.

## Nachbesserung nach der 2. Gegenprüfung
Rot-Lauf gegen b256e1859: `reports/T-0780/rot-gegenpruefung-2.txt` (Core-Tests übersetzen nicht – `CitationHits` fehlt; App: `Bridge_every_step_checked` und `Fake_png_does_not_break` rot, die übrigen 4 Tests der Klasse grün wie vorher).

| Nr. | Befund | Behebung | Regressionstest |
|---|---|---|---|
| H1 | Claude-Brücke umging Quellen-Sperre und Zitatprüfung über `befehl`, `feld_setzen`, `klicken` | Prüfung am Ende **jedes** Claude-Schritts (`BridgeUi`): Stand aller offenen Dokumente von Programmen mit `KiLocked`/`KiCheck` vorher merken, nach dem Schritt Felder übernehmen (Fokus weg, notfalls neu zeichnen), dann gesperrte Felder (jede Tiefe) vergleichen und `KiCheck` ausführen; bei Verstoß `Rollback` auf den Stand davor (Daten und Rückgängig-Liste), Claude bekommt „Zurückgenommen: …“, Protokoll `claude.zurueckgerollt`. `feld_setzen` fokussiert das Feld vorher (wie getippt). Gilt für alle Programme mit KiLocked/KiCheck | `T0780Gegenpruefung_Ui.Bridge_every_step_checked`: Quelle per `befehl ref-src-buch` und per `klicken ref-srcadd-buch` → zurückgerollt; erfundener Beleg per `feld_setzen ref-sec-text` → zurückgerollt; Gegenprobe mit `[@q-buch]` übernommen; Scannen `feld_setzen scan-folder-box` → abgelehnt, Ordner unverändert |
| M1 | zu viele Fehlalarme | Gesperrt nur: Klammer mit Jahr plus Komma, Seitenangabe oder „vgl.“, „(Name o. J.)“, „(n.d.)“, „(Name [2021])“, „vgl. Name, 2021, S. 4“ ohne Klammer, Ort/Verlag-Angaben; Jahre vor 1500 und „v. Chr.“. Nur Hinweis (in „Formvorgaben prüfen“, keine Ablehnung): erzählend „Name (2005)“, „In Deutschland (2023)“, „(Olympia 2024)“, „(erfunden 2021)“, Fußnotenzeile „1) … 1990“ | Testtabelle `T0780GegenpruefungTests.Blocked` (17 Fälle), `Hint_only` (8), `No_false_alarm` (9 Gegenproben) |
| M2 | unechtes PNG mit gültigem Kopf brach Import/Export ab | Dekodieren in `Reencode`, `Thumb`, PNG-Export, Zeichenfläche und PDF abgesichert; beim Import verworfen und gemeldet | `T0780Gegenpruefung_Ui.Fake_png_does_not_break` (Import, PNG, Zeichnen, PDF) |
| M3 | 2020a/b nur nach erster Person | Gruppe = ganze Personenliste | `Year_suffix_full_list` |
| niedrig | KiCheck beanstandete schon vorhandene Belege der Person | Nur neue Verweise/Belege im geänderten Abschnitt zählen | `Only_new_text_checked` |

Ehrlich: `scan-folder-box` ist im Programm ein Pfadfeld, das nur über „Ordner wählen …“ geändert wird (schreibgeschützt) – `feld_setzen` wird dort schon vorher abgelehnt; der Schutz am Schritt-Ende greift für jeden anderen Weg (geprüft mit `befehl`/`klicken` an der Quellen-Sperre).
**Tests dieser Runde:** Build 0 Fehler. Core (u. a. PromptAction, CiteFormats, KreativKits4, WissenStudios, T0780Gegenpruefung, Deck/Slide/Docx/Odt/Text/Office, Assist, Fremdnachlauf echt): **360 bestanden, 5 übersprungen, 0 Fehler**. App 17 Klassen inkl. ClaudeBridge, ClaudeLive, ClaudeLiveSync, ClaudeLiveRegie, ClaudeRunFix, ClaudePrograms, Assist*, T0774, DocScan, WissenStudios, T0780Gegenpruefung: **205 gesamt, 0 fehlgeschlagen, 3 übersprungen** (`reports/T-0780/app-klassen-3.txt`).

## Nachbesserung nach der Gegenprüfung („nicht freigabefähig“)
Rot-Lauf: `reports/T-0780/rot-gegenpruefung.txt` – die neuen Core-Tests übersetzen auf 11c40362d nicht (fehlende Prüfungen), die 4 App-Tests übersetzen dort und schlagen alle fehl. Endstand grün (Zahlen unten).

| Nr. | Befund | Behebung | Regressionstest |
|---|---|---|---|
| H1+H2 | Zitatprüfung nur in `referat.abschnitt`, schmales Muster; `dokument.setzen`, Gliederung und Claude-Brücke ungeprüft | Eine gemeinsame Prüfung `ReferatKit.KiTextProblem`/`KiCheck` (vorher/nachher): geänderte Abschnitte (Überschrift, Text, Folienpunkte) dürfen nur Verweise `[@kennung]` auf vorhandene Quellen enthalten, freie Kurzbelege werden abgelehnt, die Quellenliste ändert keine KI. Neu `ProgramSpec.KiCheck` – greift im Assistenten nach jedem Vorschau-Schritt (Fachaktion, `dokument.setzen`, Befehle) und in der Claude-Brücke vor dem Übernehmen. Muster: „(vgl. Name 2021)“, „(Name 2021: 5)“, „(Name 2021, 5)“, „(Name, 2021, p. 3)“, „Name (2021)“, „(Name, 2021; Ander, 2019)“, „(van Name, 2021)“, „(A, B & C, 2021)“, „et al.“, Fußnotenzeile mit Jahr, „Ort: Verlag, Jahr“, „Ort Jahr, S. n“. Kein Fehlalarm bei „im Jahr 2021“, „(Stand 2021)“, „(Juli 2021)“, „die Französische Revolution (1789)“, „dem Mauerfall (1989)“. Seitenangabe außerhalb der bekannten Seiten einer Quelle → Warnung in „Formvorgaben prüfen“ | Core `T0780GegenpruefungTests.Free_citations_found` (11 Fälle), `No_false_alarm` (7), `Ki_check_after_change`, `Page_plausibility`; App `T0780Gegenpruefung_Ui.Bridge_and_assistant_check_citations` (Brücke und Assistent) |
| H3 | `KiLocked` galt in der Claude-Brücke nicht | `dokument_setzen` lehnt gesperrte Felder ab – gilt für alle Programme. Abgegrenzter Aufruf `ProgramAssistTarget.LockedFieldFor` → `Session.LockedField`, zeichengleich aus T-0779 übernommen (jedes Pfadsegment und jeder Schlüssel im geschriebenen Wert); der Assistent nutzt dieselbe Funktion | `T0780Gegenpruefung_Ui.Bridge_respects_locked_fields` (Referate `sources` als Pfad, mit Leerzeichen und als Schlüssel im Wert; Scannen `folder`); ClaudeBridge*/ClaudeLive* mitgelaufen |
| H4 | Farben ungeprüft aus Dokument/CSV/Import, Absturz beim Zeichnen | `TimelineKit.ValidColor`/`SafeColor` (nur `#RRGGBB`/`#AARRGGBB`) beim Lesen (FromJson, FromCsv, Import) und beim Zeichnen (Layout, Zeichenfläche, PNG) | `Colors_sanitized` (kein `script`/`onload` im SVG, ToPdf ohne Abbruch, CSV); App `Canvas_bad_color_no_crash` |
| H5 | „50er v. Chr.“ wurde „59er v. Chr.“; verworfene Einträge still | Schreiben passend zum Lesen (auch „0er“ und „0er v. Chr.“), `Timeline.Problems` meldet ausgelassene Einträge, unlesbares Ende, Farben, Bilder – steht in der Statusleiste und nach dem Einlesen | `Roundtrip_all_kinds` (11 Arten, ToString und Kurzform), `Roundtrip_decades` (990 v. Chr. … 2020), `From_json_reports` |
| M1 | Bilder aus JSON/Dokument ungeprüft | Kopfprüfung PNG/JPEG + Höchstgröße beim Lesen; beim Einlesen in der App dekodiert, verkleinert und als JPEG neu kodiert (`TimelineCanvas.Reencode`), sonst weggelassen und gemeldet | `Images_checked`; App `Import_reencodes_images` |
| M2 | Steuerzeichen ergaben ungültiges XML | Gemeinsamer Helfer `Office.XmlSafe` in DOCX, ODT, PPTX, ODP und SVG | `Control_chars_valid_xml` (alle fünf Formate mit XML-Parser) |
| M3 | `[@müller2020]`, `[ @neu]` nicht erkannt | Kennungen mit Buchstaben aller Sprachen, Punkt/Doppelpunkt, Leerzeichen nach „[“; unbekannte blockieren | `Citation_ids_unicode` |
| M4 | 2020a/2020b fehlte | `ReferatKit.ForOutput`: gleiche Erstautorin im gleichen Jahr → a/b nach Titel, in Kurzbeleg und Verzeichnis | `Year_suffix` |
| niedrig | „o. J..“, „*“ im Titel, ISO „-0044“, „0er“, Tab in ODP, Liste→Zeilen | „o. J.“ ohne Doppelpunkt (CiteKit), „*“ wird „∗“, ISO-Jahre astronomisch (−0044 = 45 v. Chr., 0000 = 1 v. Chr.; „-44“ bleibt 44 v. Chr. wie bei TimelineJS), „0er“ = 1…9, `<text:tab/>`, Liste→Zeilen nur bei `AssistParam.Multiline` | `Small_points`, `Iso_years`, `Roundtrip_all_kinds` |
| niedrig | Deckblatt ohne Seitenzahl | **nicht umgesetzt** – braucht eine eigene erste Seite in DOCX, ODT und PDF (gemeinsame Formate); als Grenze dokumentiert (R-22) | – |

**Tests der Nachbesserung (gezielt):** Build 0 Fehler. Core (Filter u. a. PromptAction, CiteFormats, KreativKits4, WissenStudios, T0780Gegenpruefung, Deck/Slide/Docx/Odt/Text/Office/Import, Assist, T0223, PdfWerkstatt, DocScan, Buero): **487 bestanden, 5 übersprungen, 0 Fehler** (`reports/T-0780/core-klassen.txt`). `T0780_Fremdnachlauf` 3/3 echt. App 26 Klassen (u. a. ClaudeBridge*, ClaudeLive*, ClaudePrograms, ClaudeRunFix, Assist*, T0774, WissenStudios, T0780Gegenpruefung, StudiosAlle, UebersichtEinfach, StudioBilder, StudioIcon): **328 gesamt, 0 fehlgeschlagen, 3 übersprungen** (`reports/T-0780/app-klassen.txt`); nach der Übernahme von `LockedField` nochmals die Brücken-, Assistenten- und T-0780-Klassen (`reports/T-0780/app-klassen-lockedfield.txt`).

Für die Zusammenführung: `OnApplyNote` (string?) und `ProgramCmd.NoKi` bleiben unverändert (gemeinsamer Standard); Adapter-Datei heißt jetzt `src/Trendo.Core/PromptAction/ReferatZeitleisteAdapters.cs`. Der Fremdnachlauf-Test räumt seinen Arbeitsordner (`%TEMP%\trendo-t0780-work`) und ohne `TRENDO_BELEG_DIR` auch `%TEMP%\trendo-t0780-beleg` selbst wieder ab (geprüft: beide nach dem Lauf nicht mehr da). Fremdnachlauf mit dem Endstand neu gelaufen (3/3), Belege unter `reports/T-0780/fremd/` neu.
**Ehrlich:** Die Kurzbeleg-Erkennung arbeitet mit Mustern. Ein Ereignis mit Jahreszahl in Klammern direkt nach einem Namen ohne Artikel („Gutenberg (1450)“) gilt als Beleg und wird bei KI-Texten abgelehnt; ein Zitat nach Artikel („der Müller (2020)“) wird nicht erkannt. Der Echtlauf mit lokaler KI wurde nach diesen Änderungen nicht wiederholt (Laufzeit, s. unten); die Ersatz-KI-Tests decken die neuen Wege ab.

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
Neu: `src/Trendo.Core/Wissen/Referat.cs`, `src/Trendo.Core/Wissen/Zeitleiste.cs`, `src/Trendo.Core/Office/SlideOdp.cs`, `src/Trendo.Core/PromptAction/ReferatZeitleisteAdapters.cs`, `src/Trendo.App/MainWindow.Referate.cs`, `src/Trendo.App/MainWindow.Zeitleisten.cs`, `src/Trendo.App/TimelineCanvas.cs`, `src/Trendo.App/Assets/studios/STUDIO-REFERAT-01.jpg`, `…/STUDIO-TIMELINE-01.jpg`, `tests/Trendo.Core.Tests/WissenStudiosTests.cs`, `tests/Trendo.Core.Tests/T0780FremdnachlaufTests.cs`, `tests/Trendo.App.Tests/WissenStudiosUiTests.cs`, `docs/nachbau/referate/anforderungen.md`, `docs/nachbau/zeitleisten/anforderungen.md`, `reports/T-0780/**`, `tasks/T-0780-*.md`.
Geändert: `src/Trendo.App/StudioCatalog.cs`, `StudioIcons.cs`, `MainWindow.PromptChips.cs`, `MainWindow.W6.cs`, `MainWindow.ProgramShell.cs`, `MainWindow.Assist.cs`, `src/Trendo.Core/PromptAction/CapabilityCatalog.cs`, `IntelligencePromptModel.cs`, `StudioKnowledge.cs`, `src/Trendo.Core/W2/StudioProfiles.cs`, `StudioTeams.cs`, `src/Trendo.Core/Programs/Assist.cs`, `tools/studio_bilder.py`, `docs/improvement-42/studio-capability-matrix.md`, `.gitattributes`, Tests `PromptBarTests.cs`, `StudioFoldsUiTests.cs`, `PromptActionTests.cs`, `Daten/einfach-vorher-ausgeblendet.txt`.

## ENTWICKLUNGSBERICHT
- Projekt: Trendo OS · Sprachen: C# (.NET 10, Avalonia 12), Python (Patch-Hilfen, Bildskript) · Rust eingesetzt: NEIN
- Grund: Projekt ist C#; Fachkerne (Datum, Layout, Formate) sind klein und schnell genug, keine messbare Engstelle.
- Tests: siehe oben · Gemessene Verbesserungen: keine Leistungsmessung nötig · Offene Probleme: siehe „Offen“.
