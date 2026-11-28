# Bericht T-0788: Neues Studio „Stammbaum“ (Etappe 0.9.115 „Wissen & Lernen E“)

**Auftrag:** `tasks/T-0788-stammbaum.md` · **Branch:** `claude/T-0788-stammbaum` (Basis main 0.9.114 = 9bc135aaa) · eigene Kopie in `%TEMP%\trendo-auftrag-T-0788`.

## Was war → was ist
| Punkt | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| Studio Stammbaum | fehlte (`docs/audit/2026-11-26-neubewertung/fehlende-studios.md`, Wissen & Lernen Rang 10: „kein GEDCOM – neuer Kern“) | Studio `STUDIO-FAMILY-01`, Programm `stammbaum-programm` in der Hülle: Personenliste links (Suche), Baum in der Mitte, Personenzeile oben, Eigenschaften rechts, Prüfung unten | `T0788Stammbaum_Ui` (5 Tests + Echtlauf) |
| Personen, Familien, Ereignisse | – | Person, Vater, Mutter, Partner, Kind (Familie wird mitgeführt); Ereignisse Geburt, Taufe, Tod, Begräbnis, Beruf, Wohnort … (25 Arten) und Familienereignisse (Heirat, Scheidung …); Notizen | `Click_to_files`, `Lines`, `Lines_parent_first` |
| Daten | – | `GenDate`: GEDCOM 5.5.1 (genau, ABT/CAL/EST, BEF, AFT, BET…AND, FROM…TO, INT, Text, B.C., Doppeljahr); deutsche Eingabe („12.03.1850“, „um 1850“, „zwischen 1850 und 1860“, „44 v. Chr.“); Rohtext bleibt erhalten; unlesbare Eingabe mit Beispielen abgelehnt | `Dates` (14), `Input` (14), `Input_rejected` |
| Orte, Quellen | – | Ortsansicht (Zahl der Ereignisse, Umbenennen überall, Koordinaten N/E), Quellen (Titel, Verfasser, Veröffentlichung) an Person oder Ereignis mit Fundstelle | `Mouse_and_keys` (Ort umbenannt), `Read_foreign`, `Report` |
| Baumansicht | – | Vorfahren (Ahnentafel, 2–6 Generationen, „+ Vater/+ Mutter“ anklickbar) und Nachkommen (Partner im Kasten); Mausrad-Zoom um die Mausstelle, Ziehen, + / − / 0, Pfeile, Klick wählt, Doppelklick/„In die Mitte“, Rechtsklick-Menü, Entf; Einpassen hält die Person in der Mitte im Bild (nie kleiner als 60 %) | `Layouts`, `Mouse_and_keys`, `Click_to_files` (Klick auf „+ Mutter“) |
| GEDCOM | – | `Gedcom.Read/Write`: CONC/CONT, Kennungen bleiben, fehlerhafte Zeilen mit Nummer gemeldet; Export immer UTF-8, Kopf GEDC 5.5.1 + SUBM, Zeilen ≤ 255 Byte, CONC nie an Leerzeichen; **unbekannte Tags und Datensätze bleiben erhalten** (an Person, Name, Ereignis, Ort, Quellenangabe, FAMC/FAMS/CHIL, Kopf, eigene Datensätze); zweiter Export zeichengleich | `Roundtrip_lossless`, `Long_values`, `Broken_lines`, `Import_gedcom` |
| Zeichensätze | – | UTF-8 (±BOM), UTF-16, Windows-1252/ANSI, DOS 850, Macintosh; **ANSEL wird erkannt und umgerechnet** (Akzente vor dem Buchstaben → Unicode NFC, ß, Ł, Ø …), unbekannte Zeichen gezählt und in der Meldung genannt; falsche Angaben („ASCII“ bei UTF-8) erkannt | `Ansel`, `Encodings`, `Import_gedcom` (ANSEL per Ziehen) |
| Prüfungen | – | Fehler: Geburt nach Tod, Taufe vor Geburt, Begräbnis vor Tod, Elternteil jünger als Kind, Geburt nach Tod der Mutter / > 1 Jahr nach Tod des Vaters, Heirat vor eigener Geburt / nach eigenem Tod, eigener Vorfahre. Hinweise: Mutter < 12 / > 55, Vater < 13 / > 80, Heirat < 13, Alter > 120, Ereignis > 1 Jahr nach Tod, unlesbare/nicht genormte Daten, leere Familie. Ungefähre Daten mit ±10 Jahren – keine Fehlalarme | `Checks_find_contradictions`, `Checks_no_false_alarm`, `Click_to_files` (unten sichtbar, Rückgängig) |
| Bericht/PDF | – | Personen- und Familienbericht (Lebensdaten, Eltern, Partner mit Heirat, Kinder, nummerierte Quellen mit Fundstelle, Notizen, Prüfung, Quellen- und Ortsverzeichnis) als PDF, DOCX, ODT über die gemeinsamen Textformate; Baum als SVG, PNG, PDF; Personenliste CSV | `Report`, `Click_to_files`, Fremdnachlauf |
| Lebende schützen | – | Haken „Lebende Personen in Ausgaben ausblenden“ (Standard an) für GEDCOM, Bericht, Baum-Bild und CSV: Name „Lebende Person“ + Nachname, keine Daten/Notizen/Quellen, Familienereignisse lebender Paare entfallen, Verknüpfungen bleiben. Lebend = kein Tod/Begräbnis und jünger als 110 Jahre, sonst Schätzung über Eltern/Kinder/Partner/Heirat; ohne Anhalt geschützt | `Living_hidden`, `Click_to_files` |
| KI-Fachaktion | – | `stammbaum.personen` über `ProgramSpec.KiActions` (T-0774): Vorschau ändert nichts, „Ja, behalten“ fügt ein, ein Rückgängig-Schritt. Gesperrt: `sources`, `hideLiving` (`KiLocked`); `KiCheck = FamilyKit.KiProblem`: vorhandene Personen, Familien (nur neue Kinder erlaubt), Quellen und der Schutz dürfen sich nicht ändern, neue Daten müssen lesbar sein. Menüband-Befehle `NoKi` | `Ki_check` (Core), `Ki_persons` (UI, KI ersetzt), **echt** `Real_local_ki` (qwen3:14b) |
| Auftrag außerhalb des Studios | – | `StammbaumGedcomAdapter` („Personen als GEDCOM“): Zeilen → geprüfte GEDCOM-Datei im Projekt, kein Seiteneffekt | `T0788StammbaumTests.Adapter` |
| Gemeinsam (klein) | `TimelineKit.ToSvg/ToPdf` schrieben fest „Zeitleiste“ als Titel | optionaler Titel (Standard „Zeitleiste“, Zeitleisten unverändert) – der Stammbaum nutzt dieselben Zeichenbausteine für Bildschirm, SVG, PNG und PDF | `Layouts`, `WissenStudiosTests` (39) grün |

## Befunde unterwegs → behoben
| Befund (Beleg) | Behebung | Test |
|---|---|---|
| Erster Echtlauf (`reports/T-0788/ki/echtlauf-1-befund.md`): qwen3:14b nannte Hans Muster zuerst als Vater, danach in eigener Zeile mit Daten – die Daten fehlten im Dokument (Person galt schon als „vorhanden“) | `AddLines` unterscheidet Personen, die im selben Aufruf neu angelegt wurden; deren Angaben werden ergänzt. Vorhandene Personen bleiben unverändert | `Lines_parent_first`; zweiter Echtlauf bestanden |
| LibreOffice hing 300 s beim Einlesen des Bericht-DOCX (`fremd`-Lauf 1, Exit −2) – eingegrenzt: ein DOCX, das **mit einer Tabelle endet**, bringt LibreOffice zum Hängen (Varianten „nur Tabelle“ hängen, „Tabelle + Absatz“ 1 s) | Bericht endet immer mit einem Absatz (Stand, Zahl der Personen) | `Report` (letzter Block ist ein Absatz), Fremdnachlauf grün |
| Arbeitsfläche überdeckte bei kleinem Fenster den Bereich „Prüfung“ (Mindesthöhe) und der Baum war zu klein | Mindesthöhe entfernt, Einpassen mit Untergrenze 60 % und Person in der Mitte | Bild geprüft, `Mouse_and_keys` (Trefferprüfung mit `InputHitTest`) |
| Calc macht aus „12. März 1850“ eine Datumszelle (12.03.50) | so belassen (Verhalten von Calc), Prüfung im Beleg angepasst und kommentiert | Fremdnachlauf |

## Echtlauf mit lokaler KI (nichts installiert, Claude nicht beteiligt)
Ollama aus `%LOCALAPPDATA%\TrendoWerkzeuge\ollama` mit Trendos Modellordner selbst gestartet und danach beendet, Modell qwen3:14b (automatische Wahl). Bedienung über die Oberfläche (Vorlage, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“). Beleg `reports/T-0788/ki/echtlauf-lokale-ki.md` + `stammbaum-mit-ki.ged`: **bestanden** (Plan nach 19–20 s). Ergebnis: Erika Muster (15. August 1952, Neustadt), Eltern Hans Muster (1921 Musterstadt – 1999) und Marie Sommer (um 1925 Talheim), Familie mit Kind; ein Rückgängig-Schritt stellt das Dokument wieder her.
**Ehrlich:** Die KI ließ die Heirat (1950 in Neustadt) und das Geschlecht von Erika weg – Trendo ergänzt nichts selbst; die Person sieht die fehlende Heirat im Baum und kann sie eintragen. Der Sterbeort von Hans fehlt in der Anzeige des Belegs nur, weil das Protokoll ihn nicht ausgibt (im GEDCOM-Beleg steht er).

## Fremdprogramme (Abnahme 2)
Test `T0788_Fremdnachlauf` (2/2 bestanden, echte Werkzeuge, nichts ersetzt), Belege `reports/T-0788/fremd/` mit Rohlog und SHA-256, Eingang `fremd-eingang.ged` (GEDCOM in der Form verbreiteter Genealogie-Programme, eigene neutrale Daten mit Umlauten, _UID, _FREL, CHAN, QUAY, PEDI, NOTE-/REPO-/OBJE-Datensätzen):
- **Bericht:** Trendo-DOCX und -ODT → LibreOffice (Kopie aus `T0776_LoKopie`, eigenes Profil) liest alle 9 Prüfstellen (Umlaute, Fundstelle, Partner/Heirat/Kinder, Prüfung, Quelle, Ort) und setzt PDF; pdftotext liest Trendos Bericht-PDF.
- **Baum:** Trendo-SVG → Inkscape findet alle Texte (Anzahl = Trendo), speichert einfaches SVG mit „Jürgen Größer“ …, rendert PNG 1200 px; pdftotext liest das Baum-PDF.
- **Personenliste:** CSV → LibreOffice Calc (ODS und zurück) mit allen Zeilen.
- **GEDCOM in einem Genealogie-Programm: BLOCKIERT** – auf diesem PC liegt keines (Gramps, RootsMagic o. ä. nicht vorhanden, nichts installiert). Der Export ist mit Trendos eigenem Leser (Rundreise inhaltsgleich, zweiter Export zeichengleich) und den Formatregeln (≤ 255 Byte, CONC/CONT, Kopf) geprüft. Voraussetzung für den Nachweis: ein vorhandenes Genealogie-Programm (z. B. Gramps) oder eine Freigabe zum Herunterladen.

## Rot-Grün
Neue Testdateien gegen main 9bc135aaa (eigener Worktree, danach entfernt): übersetzen nicht (Core 18 Fehler, App 1 Fehler – Typen fehlen), `reports/T-0788/rot-alter-stand.txt`. Regressionstest `Lines_parent_first`: Befund des ersten Echtlaufs (`echtlauf-1-befund.md`) ist der Rot-Beleg. Endstand grün (Zahlen unten).

## Tests (gezielt, kein Volltest)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core: `T0788StammbaumTests` 44/44; `T0788_Fremdnachlauf` 2/2 echt; berührte Klassen (PromptAction, StudioTeams, NoFixedUser, VorbildAnalysen, T0777, WissenStudios, StudioKnowledge, Tests inkl. QA-015, Assist, T0788StammbaumTests): **202 bestanden, 0 Fehler** (`reports/T-0788/core-klassen.txt`).
- App (20 Klassen: T0788Stammbaum_Ui, PromptBarTests, StudioFolds_Ui, StudioIconTests, StudiosAlle_Ui, StudioTeams_Ui, StartMenu_StudioFenster, ClaudeEverywhere_Ui, EinfachBedienen_Ui, StudiosTechnikWissen_Ui, T0728_Bedienung_Ui, T0755_Ui, WissenStudios_Ui, Sys13_Qualitaet_Ui, StudioBilder, UebersichtEinfach_Ui, AssistChain_Ui, Assist_Ui, ClaudePrograms_Ui, T0779QuizKurse_Ui): **Total 251, Failed 3, Errors 0, Skipped 4** (`reports/T-0788/app-klassen.txt`). Die 3 Fehlschläge hängen nur am fehlenden requirements.yaml-Eintrag: `StudioFolds_Ui.Overview_and_search` („70 Studios“ erwartet, ohne Eintrag 69), `Sys13_Qualitaet_Ui.Modules` (STUDIO-FAMILY-01 ohne Modul), `StartMenu_StudioFenster.Jedes_Studio`. Übersprungen: Echtläufe ohne Schalter. `T0788Stammbaum_Ui.Real_local_ki` gesondert mit `TRENDO_T0788_ECHT` bestanden.
- **QA-015** (`tests/Trendo.Core.Tests/Tests.cs`) bleibt bei 91 und ist **jetzt noch grün**, weil es die Liste aus requirements.yaml zählt; sobald der Eintrag unten übernommen ist, wird er (mit T-0787 zusammen) rot und muss vom Sammel-Helfer angepasst werden.
- Angepasste Zähler (Katalog-Stand mit diesem Studio): `PromptActionTests` 116 Adapter / 87 teilweise, `PromptBarTests` 92 Studios, `StudioFolds_Ui` 70; `docs/improvement-42/studio-capability-matrix.md` mit `TRENDO_UPDATE_MATRIX=1` neu erzeugt; `einfach-vorher-ausgeblendet.txt` um 16 Stammbaum-Zeilen mit Begründung ergänzt (Ersatzweg „Mehr Werkzeuge“). **Zusammenführung mit T-0787:** diese Zähler steigen dort ebenfalls – beim Sammeln jeweils +1 je Studio.

## Bild geprüft
Trendo.Render (eigener `TRENDO_RENDER_ROOT`): Profi mit Beispiel, Einfach mit Beispiel, Profi leer – Dunkel-Gold, eine goldene Hauptaktion („Person“ bzw. „Erste Person“ im Leerzustand), Baum lesbar, Auswahl gold umrandet, Prüfung unten. Studio-Bild mit ComfyUI + FLUX.2 klein 4B lokal erzeugt (`tools/studio_bilder.py`, Prüfsummen ok, eigene ComfyUI auf 127.0.0.1:8189 mit eigener Datenbank, weil die Standard-Datenbank von einer anderen Sitzung belegt war; danach beendet) und angesehen.

## Abnahmekriterien
| Nr. | Urteil | Begründung |
|---|---|---|
| 1 Leere Vorlage → echtes Ergebnis | VOLLWERTIG | `Click_to_files`: leere Vorlage → erste Person → Vater → Mutter im Baum → alle acht Ausgaben als Dateien |
| 2 Fremddatei ein, Export im Fremdprogramm | TEILWEISE | Fremd-GEDCOM eingelesen; Bericht (DOCX/ODT) in LibreOffice, Baum (SVG) in Inkscape, CSV in Calc echt geöffnet. GEDCOM-Export in einem Genealogie-Programm BLOCKIERT (keins vorhanden) |
| 3 Absturz ≤ 30 s | TEILWEISE | über die Programm-Hülle (Autosave, Notsicherung, Wiederherstellung, AtomicFile); kein eigener Absturztest für dieses Studio |
| 4 KI-Fachaufgabe im offenen Dokument, rückgängig | VOLLWERTIG | Ersatz-KI-Test + Echtlauf qwen3:14b |
| 5 Test vom Klick bis zur Datei | VOLLWERTIG | `Click_to_files`, `Import_gedcom` |
| 6 Einfach/Profi, Kürzel, Rechtsklick, Ziehen | VOLLWERTIG | `Simple_level`, `Mouse_and_keys` (Mausrad, Ziehen, Rechtsklick-Menü, Entf, Rückgängig), Ablegen einer .ged (`Import_gedcom`); Tasten + / − / 0 / Pfeile gebaut, getestet ist Entf |

## Anforderungen (Clean-Room, `docs/nachbau/stammbaum/anforderungen.md`)
S-01 … S-23 erfüllt (Prüfung je Zeile in der Datei); S-10 Doppelklick gebaut, getestet ist der gleiche Weg über „In die Mitte“ → TEILWEISE. S-24 Ortskarte, S-25 Medien, S-26 weitere Ansichten (Sanduhr/Fächer), S-27 GEDCOM 7/ANSEL schreiben, S-28 mehrere Namen/Duplikate → FEHLT (Stufe 2).

## Vorbild-Tabelle
In `docs/nachbau/stammbaum/anforderungen.md`: Family Tree Maker, RootsMagic, Legacy Family Tree, Gramps (GPL-2.0-or-later, nur Clean-Room), FamilySearch – mit Quelle und Datum 28.11.2026. Ehrlich: meist Vergleichs- und Blogseiten; die genauen Standardgrenzen der RootsMagic-Prüfungen für Elternalter fanden sich nicht öffentlich.

## requirements.yaml-Vorschlag
(Datei für Agenten gesperrt – nicht geändert. Ohne diesen Eintrag fehlt das Studio in der Übersicht und im Startmenü; siehe „Offen“.)
```yaml
- id: STUDIO-FAMILY-01
  name: Stammbaum
  approved_expansion_functions: '0'
  approval_status: APPROVED_SPEC
  implementation_status: IN_PROGRESS
  test_status: NOT_RUN
  evidence: Freigabe im Chat 09.11.2026 (79 neue Studios), Etappe 0.9.115 Wissen & Lernen E 26.11.2026 (T-0788)
```

## Was NICHT geprüft wurde
- GEDCOM-Export in einem fremden Genealogie-Programm (keins vorhanden – BLOCKIERT).
- Echte ANSEL-Datei eines Fremdprogramms: geprüft mit selbst erzeugten ANSEL-Bytes nach der Zeichentabelle (inkl. unbekanntem Zeichen).
- Öffnen in Microsoft Word (nicht vorhanden) – nur LibreOffice.
- Sehr große Dateien (> einige Tausend Personen): Grenze 50 000 Personen, Leistung nicht gemessen; jede Änderung schreibt den ganzen Stammbaum ins Dokument.
- Doppelklick im Baum und Tasten + / − / 0 / Pfeile (gebaut, nicht eigens getestet).
- Claude-Echtlauf (Kontingent, laut Auftrag nicht); Volltest (macht die Hauptsitzung).
- Bild des Studios in der Übersicht (erscheint erst mit dem requirements.yaml-Eintrag).

## Offen / Risiken
- **requirements.yaml:** bis zum Eintrag schlagen erwartbar fehl: `StudioFolds_Ui.Overview_and_search`, `Sys13_Qualitaet_Ui.Modules`, `StartMenu_StudioFenster.Jedes_Studio`; mit dem Eintrag wird QA-015 rot (91 → 92/93 mit T-0787) – Sammel-Helfer.
- **Gemeinsamer Baustein `DocxFormat`:** ein Dokument, das mit einer Tabelle endet, lässt LibreOffice beim Einlesen hängen (hier im Bericht umgangen, im gemeinsamen Schreiber nicht behoben – betrifft z. B. Trendo Text, wenn ein Dokument mit Tabelle endet). Vorschlag: eigener kleiner Auftrag (DOCX immer mit `<w:p/>` vor `w:sectPr`).
- Die KI übernimmt nur, was sie liefert; ausgelassene Angaben (Heirat im Echtlauf) ergänzt Trendo nicht.
- Gemeinsame Dateien berührt (am Ende der Listen): StudioCatalog, StudioIcons, PromptChips, CapabilityCatalog, StudioKnowledge, StudioProfiles, StudioTeams, IntelligencePromptModel, W6 (Init-Zeile), `tools/studio_bilder.py`, `docs/design/STUDIOBILDER.json`, `.gitattributes`, `docs/improvement-42/studio-capability-matrix.md`, `einfach-vorher-ausgeblendet.txt`, Zähler in PromptActionTests/PromptBarTests/StudioFoldsUiTests; klein `Zeitleiste.cs` (optionaler Titel).

## Berührte Dateien
Neu: `src/Trendo.Core/Wissen/Gedcom.cs`, `GenDate.cs`, `Stammbaum.cs`, `StammbaumAusgabe.cs`, `src/Trendo.Core/PromptAction/StammbaumAdapter.cs`, `src/Trendo.App/MainWindow.Stammbaum.cs`, `src/Trendo.App/FamilyTreeCanvas.cs`, `src/Trendo.App/Assets/studios/STUDIO-FAMILY-01.jpg`, `tests/Trendo.Core.Tests/T0788StammbaumTests.cs`, `tests/Trendo.Core.Tests/T0788FremdnachlaufTests.cs`, `tests/Trendo.App.Tests/T0788StammbaumUiTests.cs`, `docs/nachbau/stammbaum/anforderungen.md`, `reports/T-0788/**`, `tasks/T-0788-*.md`.
Geändert: `src/Trendo.App/StudioCatalog.cs`, `StudioIcons.cs`, `MainWindow.PromptChips.cs`, `MainWindow.W6.cs`, `src/Trendo.Core/PromptAction/CapabilityCatalog.cs`, `IntelligencePromptModel.cs`, `StudioKnowledge.cs`, `src/Trendo.Core/W2/StudioProfiles.cs`, `StudioTeams.cs`, `src/Trendo.Core/Wissen/Zeitleiste.cs`, `tools/studio_bilder.py`, `docs/design/STUDIOBILDER.json`, `docs/improvement-42/studio-capability-matrix.md`, `.gitattributes`, Tests `PromptActionTests.cs`, `PromptBarTests.cs`, `StudioFoldsUiTests.cs`, `Daten/einfach-vorher-ausgeblendet.txt`.

## ENTWICKLUNGSBERICHT
- Projekt: Trendo OS · Sprachen: C# (.NET 10, Avalonia 12), Python (Patch-Hilfe, Bildskript) · Rust eingesetzt: NEIN
- Grund: Projekt ist C#; GEDCOM-Lesen/Schreiben, Prüfungen und Layout sind klein und schnell genug (Core-Tests in < 1 s), keine messbare Engstelle.
- Tests: siehe oben · Gemessene Verbesserungen: keine Leistungsmessung nötig · Offene Probleme: siehe „Offen“.
