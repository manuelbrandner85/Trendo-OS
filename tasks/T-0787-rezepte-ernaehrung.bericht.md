# Bericht T-0787: Rezepte & Ernährung (Etappe 0.9.115 „Wissen & Lernen E“)

**Auftrag:** `tasks/T-0787-rezepte-ernaehrung.md` (Freigabe 26.11.2026) · **Branch:** `claude/T-0787-rezepte-ernaehrung` (Basis main 9bc135aaa = 0.9.114) · eigene Kopie in `%TEMP%\trendo-auftrag-T-0787`.

## Dritte Prüfrunde (Stand b6ffe500c)
Rot-Lauf auf b6ffe500c (`reports/T-0787/runde3/rot-runde3.txt`): Core `T0787_Runde3` **5 von 5 rot**, App `T0787Runde3_Ui` **2 von 2 rot**; auf dem neuen Stand grün.

| Nr. | Befund | Behebung | Test |
|---|---|---|---|
| M1 | Fehlalarme der Wortfamilien bei Kochsätzen | **Aufbau zweistufig statt längerer Liste.** (a) *Sperre* (KiCheck, KI-Aktion, Adapter): Wirk-Verb (heilt, lindert, bekämpft, wirkt/hilft gegen/bei, schützt vor, gut für/gegen, senkt, stärkt, fördert, beugt … vor …) + Krankheits-/Körperbegriff aus fester Liste (Krebs, Erkältung, Entzündung, Blutdruck, Blutzucker, Cholesterin, Verdauung, Immunsystem, Herz, Knochen, Kopfschmerzen, Husten, Müdigkeit …) mit höchstens fünf Wörtern dazwischen; Körperbegriff + „-senkend/-hemmend/-fördernd …“; feste Begriffe (Detox, entgiftet, Superfood, entzündungshemmend, antioxidativ …); Abnehmen nur als Diät („zum Abnehmen geeignet“, „hilft beim Abnehmen“ – nicht „vom Herd“, „des Deckels“). (b) *Hinweis* („Bitte prüfen“, keine Ablehnung): gesund/gesünder/gesündere, Heiltee, lindernd, „senkt das Risiko“, medizinisch, Diät … „gesunde Portion“, „Heilwasser“, „Gesundheit!“ bleiben frei. Ehrlich in Hilfe und Code: Wortlisten, nicht vollständig | Core `Fehlalarme_frei` (20 Kochsätze aus allen Runden: weder Sperre noch Hinweis), `Versprechen_gesperrt` (23 Sätze gesperrt, 7 weiche nicht gesperrt, KiCheck), Neu `Hinweise`; `T0787_Runde2.Versprechen_wortfamilien` jetzt „gesperrt ODER Hinweis“ (so verlangt) |
| M1 Nebenbefund | Der Test der Zeitgrenze aus Runde 2 setzte eine globale Zeitgrenze – parallel laufende Tests konnten dadurch „Zeitgrenze“ melden (hier beobachtet) | Zeitgrenze fest; für den Test als Argument (`Gesundheitsversprechen(text, zeit)`, `KiCheck(…, zeit)`) | `Zeitgrenze_ehrlich` (Prüfung und KiCheck) |
| M2 | „1.500-2.000 g“ = 2 bis 1500 | Zahlen am Anfang einheitlich über `NormZahl` – erste und zweite Zahl eines Bereichs nach derselben Regel | Core `Bereich` |
| M3 | englische Einheiten/Zutaten | Einheiten cup, tbsp, tsp, fl oz, pint, quart (US-Maße, Volumen mit Dichte), oz, lb (Masse); `InMetrisch` rechnet sie um; 96 englische Namen (`NaehrwertTabelle.Englisch`) auf dieselben USDA-Zeilen; Import meldet „… englische Zutatennamen nicht zugeordnet – Nährwerte fehlen (…)“ | Core `Englisch`, Neu `Hinweise` (alle Verweise gültig) |
| N4 | „1,000 g“, „1.500 TL“, „1 Dose weiße Bohnen“ | Komma + drei Ziffern nur vor g/ml/mg als englisches Tausenderkomma (1000, Hinweis „Menge prüfen“), sonst Dezimalkomma („1,500 TL“ = 1,5 TL); „1.500 TL“ mit Hinweis; Dose: Dosenware, deren Name ein Wort der Zutat enthält (Bohnen → Kidneybohnen aus der Dose – SR Legacy hat keine weißen Bohnen in der Auswahl) | Core `Kleinigkeiten` |
| N5 | Fehler beim Ablegen mehrerer Dateien | `RzImportCore`: Lese- und Übernahmefehler einheitlich gesammelt, Abschluss „1 von 3 Dateien eingelesen (… Rezepte). „kaputt.json“: …“ | UI `Mehrere_dateien_fehler_gesammelt` |
| N6 | Sperre erst nach dem Dialog | Sperre greift vor dem Dateidialog; zweiter Klick öffnet keinen zweiten Dialog | UI `Sperre_vor_dialog` |

Tests: Core (T0787*, PromptAction*, QA015*) **125 gesamt, 123 bestanden, 2 übersprungen** (`reports/T-0787/runde3/rohlog-core.txt`); App (T0787Rezepte_Ui, T0787_Rot, T0787Gegenpruefung_Ui, T0787Runde2_Ui, T0787Runde3_Ui, StudioFolds_Ui, PromptBarTests, UebersichtEinfach_Ui) **28 gesamt, 0 Fehler, 1 übersprungen** (`rohlog-app.txt`). Nicht wiederholt: Fremd- und Echtlauf.

## Zweite Prüfrunde (Stand 3496aede4)
Rot-Lauf: `T0787_Runde2` (nur Schnittstellen von 3496aede4) dort **6 von 6 rot** (`reports/T-0787/runde2/rot-runde2.txt`), auf dem neuen Stand grün. Für R2-2, R2-6 (Einlesen im Hintergrund, Sperre, geschlossenes Buch) und R2-7 (Zeitgrenze) gibt es keinen Rot-Lauf auf altem Stand: die Tests brauchen die neuen Schnittstellen `RzImportFilesAsync` bzw. `VersprechenZeit` und übersetzen dort nicht.

| Nr. | Befund | Behebung | Test |
|---|---|---|---|
| M1 | „0.250 kg“ = 250 kg; „1.500 kg“ mehrdeutig; Hinweis fiel bei Einlesen/KI still weg | **Regel (in Hilfe und Anforderungen dokumentiert):** „1.000,5“ = deutsche Schreibweise (eindeutig); führende 0 („0.250“) = Dezimalpunkt; Punkt + drei Ziffern vor kg, l, Pfund, dl, Tasse, Becher = Dezimalpunkt (1,5 kg); sonst (g, ml, Stück, ohne Einheit) = Tausenderpunkt (1500 g). Punkt + drei Ziffern kommt immer mit Hinweis (außer „0.…“). Wege ohne Rückfrage (JSON-LD-Import, KI-Aktion) nutzen `LiesMitPruefvermerk`: die Zutat bekommt „Menge prüfen: …“ in ihren Hinweis, Import meldet „… mehrdeutig“, die KI-Meldung nennt es | Core `Punktregel`, `Import_meldet_mehrdeutig`, Neu `Pruefvermerk`; UI `Ki_mehrdeutig_markiert` |
| M2 | Schließen während des Lesens; Doppelklick | `RzImportFilesAsync`: Sperre (kein zweites Einlesen, ehrliche Meldung); nach dem Lesen: Ist das Buch noch offen? Sonst nichts übernehmen, Meldung „… geschlossen … nichts übernommen“, Eintrag im Protokoll | UI `Geschlossen_waehrend_lesen`, `Sperre_gegen_doppelt` |
| M3 | Lücken bei Versprechen | Wortfamilien: heilen (auch Heiltee/Heil-Tee, „heilt“ außer „heilt nicht“), Krebs/Krankheit, Schutz vor, senken (auch Risiko, „Blutdruck senkend“), entzündungshemmend, antioxidativ, stärkt Knochen/Herz/…, gut für/gegen, wirkt gegen, fördert Verdauung/…, gesund*, zum/beim Abnehmen, Schlankmacher, Superfood, lindern* … **Ehrlich:** Wortlisten, nicht vollständig – so in Hilfe und Code-Kommentar | Core `Versprechen_wortfamilien` (auch „Der Teig heilt nicht“, „Den Deckel abnehmen“, „Das Ei bleibt heil“, Fehlalarme aus Runde 1) |
| M4 | „1.000,5 g“, „1.250,75 ml“ | deutsche Schreibweise eindeutig gelesen, ohne Hinweis | Core `Deutsche_schreibweise` |
| N5 | Duplikat-Umbenennung konnte ein echtes späteres Rezept umbenennen | alle echten Kennungen bleiben reserviert; nur Duplikat/leere bekommt eine freie; Plan zeigt weiter auf das erste | Core `Duplikat_bekommt_freie_kennung` |
| N6 | Ablegen las im UI-Thread | Ablegen nimmt `RzImportFilesAsync` (gleicher Weg wie „Rezept öffnen …“) | gleicher Code-Weg wie UI-Tests oben; das Ablege-Ereignis selbst nicht eigens simuliert |
| N7 | Zeitüberschreitung der Regex | abgefangen → Hinweis „… brach an der Zeitgrenze ab“, KiCheck und KI-Aktion lehnen ab | Neu `Zeitgrenze_ehrlich` |
| N8 | Kleinigkeiten | „10 x 10 x 10 g“ (bis drei Faktoren), „3 x Eier“, „-1 g“ gemeldet (mit Leerzeichen „- 2“ bleibt Aufzählung), „1 Dose Bohnen“ = Kidneybohnen (Dose), Bund/Dose/Pck. + Gramm über Packungsgewicht zusammengelegt, Zeiten beim Einlesen höchstens 7 Tage | Core `Kleinigkeiten` |

Tests: Core (T0787*, PromptAction*, QA015*) **119 gesamt, 117 bestanden, 2 übersprungen** (`reports/T-0787/runde2/rohlog-core.txt`); App (T0787Rezepte_Ui, T0787_Rot, T0787Gegenpruefung_Ui, T0787Runde2_Ui, StudioFolds_Ui, PromptBarTests, UebersichtEinfach_Ui) **26 gesamt, 0 Fehler, 1 übersprungen** (`rohlog-app.txt`). Kopfzeile der Hülle unverändert (wird separat behandelt). Nicht wiederholt: Fremd- und Echtlauf.

## Nachbesserung nach der Gegenprüfung (Stand f3d579a88)
Rot-Lauf: `T0787_Gegenpruefung` (nur alte Schnittstellen) auf f3d579a88: **8 von 8 rot** (`reports/T-0787/gegenpruefung/rot-gegenpruefung.txt`; G2 dort 1 min 25 s für 300 KB), auf dem neuen Stand grün. V10 war laut Gegenprüfung rot.

| Nr. | Befund | Behebung | Test (rot → grün) |
|---|---|---|---|
| H1 | V10 rot: in „Einfach“ fehlten `prog-level-*` und sechs Ausgaben | Ersatzwege wie bei allen Programmen der Hülle: Stufenwahl über „Mehr Werkzeuge“ (`prog-tools-more`), „Ansicht ▸ Bedienstufe“ und „Mehr ▸ Bedienstufe“; die Ausgaben über „Datei ▸ Exportieren“ (jede Stufe alle Formate, T-0710). **Einfrier-Datei verlängert** (`Daten/einfach-vorher-ausgeblendet.txt`, 12 Zeilen mit Begründung) – genau wie T-0780 (Referate/Zeitleisten) und T-0784 (Quiz, Kurse, Mathe, Natur, Nachhilfe, Programmieren lernen) für ihre neuen Studios; ein globaler Präfix in `Replaced` hätte `prog-export-docx` usw. für alle Programme freigestellt und den Test geschwächt | `UebersichtEinfach_Ui` komplett grün; **neu** `T0787Gegenpruefung_Ui.Einfach_ersatzwege`: in „Einfach“ „Mehr Werkzeuge“ geklickt → Stufe Profi; „Ansicht ▸ Bedienstufe ▸ Erweitert“ → Stufe gewechselt; alle sechs Ausgaben über die Menüleiste → jede schreibt ihre Datei |
| H2 | ReDoS: `<script…ld+json…>(.*?)</script>` quadratisch bei offenen Marken, im UI-Thread | Linearer Scan mit `IndexOf` (`ScriptBloecke`, höchstens 200 Blöcke, offene Marke → Meldung „nicht geschlossen“); `Plain` ohne Regex für Marken, linear, je Text höchstens 20 000 Zeichen; „Rezept öffnen …“ liest und wertet im Hintergrund aus (`Task.Run`), übernommen wird im UI-Thread (ein Rückgängig-Schritt) | Core G2 300 KB < 5 s (alt: 85 s, rot); Neu: 10 MB offene Marken + 5 MB „<“-Flut in Name/Zutat/Schritten < 20 s (gemessen ≈ 1 s); UI: 20-MB-Datei über „Rezept öffnen …“, Einlesen lief im Hintergrund, Meldung „nicht geschlossen“, nichts angelegt |
| M3 | doppelte Kennungen → ArgumentException (Einkaufsliste, WochenDruck, KiCheck) | `Rezepte()` macht Kennungen eindeutig und **stabil**: doppelte → `<id>-2`, `-3`; leere → `r-<Stelle>` (vorher bei jedem Lesen zufällig neu); `KiCheck` lehnt doppelte Kennungen der KI ab | Core G3 |
| M4 | „ca./etwa 200 g“ ohne Menge; „1.000 g“ = 1 g | „ca./etwa/circa/ungefähr/rund“ vor der Zahl: Menge gilt, „ca.“ in den Hinweis; Punkt mit genau drei Ziffern = Tausenderpunkt; mehrdeutiges wird gemeldet (`RezeptKit.Lies` → Hinweis, in der Oberfläche als ⚠-Meldung beim Übernehmen der Zutaten und bei „Rezept aus Text“) | Core G4/G7, Neu `Hinweise`; UI `Versprechen_in_zutaten_und_hinweis` |
| M5 | Versprechen nur in Name/Beschreibung/Schritten; Teilstring-Treffer | `AllerText` (auch Zutaten, Hinweise, Notizen, Stichwörter) in `KiCheck`, KI-Aktion und `Pruefe`; Wendungen als Regex mit Buchstaben-Grenzen; neu u. a. „senkt Blutzucker“, „entzündungshemmend“, „blutzuckersenkend“, „lindert“, „beugt … vor“ | Core G5 (keine Fehlalarme bei „hilft beim Frischhalten“, „verbeugt“); UI: „1 TL Kurkuma, heilt Entzündungen“ aus der Fachaktion abgelehnt |
| N6 | AusText: int.Parse-Überlauf | `Portionen()` mit TryParse, Grenze 1…500, Hinweis statt Ausnahme | Core G6, Neu `Hinweise` |
| N7 | „–2 Eier“, „2 x 200 g“, „1e5 g“, 1E+20, „0 g“ | Gedankenstrich als Aufzählung; „2 x 200 g“ = 400 g; Exponent-Schreibweise → keine Menge + Hinweis; Menge außerhalb 0 < m ≤ 1 000 000 schon beim Lesen verworfen (mit Hinweis) – Speichern/Neuladen ändert nichts mehr | Core G4/G7 (auch Rundreise über JSON) |
| N8 | „1 Dose Tomaten“ → frische Tomate; Stück und Gramm getrennt | Bei Einheit „Dose“ Dosenware zuerst (`*-dose`, Kidney-, Kichererbsen, Kokosmilch, Mais, Oliven); Stück und Gramm derselben Zutat mit bekanntem Stückgewicht in Gramm zusammengelegt | Core G8 |
| N9 | MaxRezepte nur je Block; Zeiten unbegrenzt | Grenze über alle Blöcke (mit Hinweis); Zeiten beim Einlesen auf 0…100 000 min, `Minuten()` ohne Überlauf | Core G9 |
| N10 | Fett-Prozent nicht wie USDA | „Vollmilch (3,25 %)“, „Fettarme Milch (1 %)“, „Schlagsahne (36 %)“, „Sahne (31 %)“ (vorher „Kochsahne (leicht)“), „Naturjoghurt (3,3 %)“; alte Bezeichnungen bleiben als Suchwörter. Geändert in Generator, Daten und Herkunft gleich (die USDA-CSV liegt nicht mehr auf dem PC – Generator nicht erneut ausgeführt; Werte unverändert) | Core G10 |

**Befund nebenbei (nicht behoben, gemeinsamer Code):** Bei 1600 px Breite ist die Kopfzeile der Programm-Hülle in „Einfach“ nach „Mehr Werkzeuge“ voll – „Professionell“ ragt rechts hinaus (gemessen x = 1473, Breite 176). Dasselbe bei `referate-programm`: dort trifft schon der echte Klick auf „Mehr Werkzeuge“ den Fensterrand. Darum klickt `Einfach_ersatzwege` die Stufe über das Klick-Ereignis; „Mehr Werkzeuge“ selbst ist echt geklickt.
Tests nach der Nachbesserung: Core (T0787*, PromptAction*, QA015*) **111 gesamt, 109 bestanden, 2 übersprungen** (Fremdläufe ohne Variable) – `reports/T-0787/gegenpruefung/rohlog-core.txt`; App (T0787Rezepte_Ui, T0787_Rot, T0787Gegenpruefung_Ui, StudioFolds_Ui, PromptBarTests, UebersichtEinfach_Ui) **23 gesamt, 0 Fehler, 1 übersprungen** (Echtlauf ohne Variable) – `rohlog-app.txt`. Nicht wiederholt: Fremdlauf (Chrome/LibreOffice) und Echtlauf qwen3:14b – der Kern der Ausgabe ist unverändert, nur Einlesen, Mengen-Parser, Prüfungen und fünf Namen.

## Was war → was ist
| Punkt | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| Studio | fehlte (`docs/audit/2026-11-26-neubewertung/fehlende-studios.md` Z. 117: „Rezept“ im Code nur Studio-Ablauf in `Fusion.cs`) | Programm `rezepte-programm` (STUDIO-RECIPE-01) in der Programm-Hülle: Rezeptbuch mit drei Ansichten (Rezepte · Wochenplan · Einkaufsliste), Nährwerte rechts | App `T0787Rezepte_Ui` (4 Fälle), Rot-Grün `T0787_Rot` |
| Rezepte | – | Name, Beschreibung, Portionen (+ Wort), Zeiten, Kategorie, Stichwörter, Notizen, Quelle; Zutaten zeilenweise wie im Kochbuch, Trendo erkennt Menge (½, 1/2, 1,5, 2–3), Einheit (g, kg, ml, l, EL, TL, Tasse, Becher, Prise, Msp., Stück, Zehe, Dose, Pck., Bund, Stange …), Zutat, Hinweis, Zwischenüberschrift; Schritte zeilenweise; Rezept aus eingefügtem Text | Core `Zutaten_lesen`, `Aus_text`; App `Klick_bis_datei`, `Woche_und_einkauf` |
| Portionen und Einheiten | – | „–/+“ zeigt Mengen für n Portionen (nur Ansicht, Tabelle mit Gramm/kcal), „So ins Rezept übernehmen“ (ein Rückgängig-Schritt), halbe/doppelte Menge, passende Einheit (1000 ml → 1 l, 3 TL → 1 EL), Löffel/Tasse → g (über Dichte) bzw. ml | Core `Portionen_und_einheiten`; App `Klick_bis_datei` |
| Nährwerte | – | je Portion: kcal/kJ, Fett, gesättigte FS, KH, Zucker, Ballaststoffe, Eiweiß, Salz – **Richtwerte** mit Quelle und Lizenz, Fehlendes und „ohne Menge“ genannt; Zuordnung per Klick/Rechtsklick änderbar | Core `Naehrwerte` (gegen Tabellenwerte nachgerechnet); App `Klick_bis_datei`, `Import_und_bedienung` |
| Nährwert-Tabelle | – | **USDA FoodData Central, SR Legacy (April 2018), CC0 1.0** – 160 Lebensmittel mit FDC-Nummer; Generator `tools/rezepte/naehrwerte_gen.py`, Herkunft je Zeile `tools/rezepte/herkunft-usda-sr.tsv`; Download 28.11.2026, 6 074 592 Bytes, SHA-256 `b80817294b8850530aaedf2e515c02593b1824f763a0ff356e5c2081643e6fd0` | Generator erneut laufen lassen → gleiche Datei (SHA-256 `a4480c1a…`) |
| Keine Gesundheitsversprechen | – | Hinweis „Richtwerte – keine Ernährungsberatung“ in Ansicht, Druck, JSON-LD; Heil-/Gesundheitsversprechen in KI-Text abgelehnt (`RezeptKit.KiCheck`), im eigenen Text als Hinweis | Core `Keine_versprechen`; App `Ki_rezept` |
| Wochenplan | – | Montag–Sonntag, Mahlzeit, Portionen; Woche vor/zurück; Rechtsklick: anderer Tag, ±1 Portion, entfernen | App `Woche_und_einkauf` |
| Einkaufsliste | – | aus dem Plan, gleiche Zutaten über alle Rezepte zusammengefasst (g/ml/Stück, gemischt über Dichte), nach Abteilung im Laden, „für: …“, abhaken, eigene Einträge; TXT, CSV (Formel-Schutz) | Core `Einkaufsliste`; App `Woche_und_einkauf` |
| schema.org/Recipe | – | Export JSON-LD (`Recipe`, mehrere als `@graph`, `nutrition` als Richtwert) und Rezeptseite HTML mit eingebettetem JSON-LD; Import aus JSON-LD oder gespeicherter Rezeptseite (`@graph`, `@type`-Liste, HowToSection, Ertrag als Liste, ISO-Dauer), Datei unverändert; Ziehen auf die Arbeitsfläche | Core `JsonLd_hin_und_zurueck`; App `Import_und_bedienung`; **echt**: Chrome |
| Druck | – | Rezept (in angezeigter Portionenzahl), Kochbuch mit Inhaltsverzeichnis, Wochenplan + Einkaufsliste als PDF; Rezept als DOCX/ODT | Core `Druck_dateien`; App (PDFium-Text); **echt**: LibreOffice, pdftotext, qpdf |
| KI-Fachaktion | – | `rezept.anlegen` über `ProgramSpec.KiActions` (`MakesFile = false`): Vorschau → „Ja, behalten“ → ein Rückgängig-Schritt; `KiLocked = plan, liste`; Nährwerte rechnet der Kern | App `Ki_rezept` (KI ersetzt); **Echtlauf qwen3:14b** |

Einbindung (am Listenende, klein): `StudioCatalog` (Wissen & Lernen), `StudioIcons` (eigener Pfad Kochtopf), `CapabilityCatalog` + Adapter `RezeptJsonLdAdapter` (`rezept.jsonld`), `StudioProfiles`, `StudioTeams` (Küchen-Team), `StudioKnowledge`, Startideen (`PromptChips`, je ≥ 8 Wörter, damit die KI zuerst gefragt wird), `InitRezepte()` in `MainWindow.W6.cs`, Bild `STUDIO-RECIPE-01.jpg` (lokal ComfyUI + FLUX.2 klein 4B, Prüfsummen ok; danach ComfyUI beendet), `docs/design/STUDIOBILDER.json`, Fähigkeitsmatrix (`TRENDO_UPDATE_MATRIX=1`), eine Zeile `TRENDO_RZ_MODE` in `tools/Trendo.Render/Program.cs` (Bildprüfung Wochenplan/Einkaufsliste).
Zählungen angepasst (nicht geschwächt): Studios 91 → 92 (`PromptBarTests`), Übersicht-Katalog 69 → 70 (`StudioFolds_Ui.Plan_is_consistent`), Adapter 115 → 116, Teilweise 86 → 87 (`PromptActionTests`).

## Vorbilder (Details in `docs/nachbau/rezepte/anforderungen.md`)
| Vorbild | Stärken (Bedienkonzept) | Lizenz | Quelle (28.11.2026) |
|---|---|---|---|
| Paprika | Plan, Einkaufsliste fasst zusammen und sortiert nach Abteilung, Nährwerte aus USDA | proprietär | paprikaapp.com; cooklang.org „Best Recipe Management Software in 2026“ (aktualisiert 19.11.2026) |
| Mealie | Zutaten-Erkennung, Mahlzeiten im Plan, Einkaufsliste, schema.org-Import | AGPL-3.0 – Clean-Room | docs.mealie.io; cooklang.org-Vergleich 2026 |
| Tandoor | Nährwerte je Rezept aus Lebensmittel-Eigenschaften, Einheitenumrechnung | Repo ohne SPDX (NOASSERTION) – nicht verwendet | newreleases.io (Tandoor 1.5.0) |
| AnyList / Cooklang | Skalieren, Einkaufsliste aus mehreren Rezepten | proprietär / MIT | cooklang.org; github.com/cooklang/cooklang-rs |
| schema.org Recipe | offenes Austauschformat | CC BY-SA 3.0 (nur gelesen) | schema.org/Recipe |
Nicht verwendet: BLS 4.0 (kostenfrei, aber **ohne benannte Lizenz** – blsdb.de/faq), Open Food Facts (ODbL).

## Anforderungen (RZ-01 … RZ-16)
Erfüllt 14; **teilweise**: RZ-01 und RZ-11 (Ziehen zum Umsortieren bzw. Datei-Ablegen nur über die gemeinsamen Bausteine `BueroReorder`/`BueroFileDrop`, nicht eigens getestet), RZ-16 (Menüleisten-Kürzel nicht einzeln getestet). RZ-12: Import in Mealie/Tandoor selbst nicht geprüft (nicht vorhanden).

## Echtlauf mit lokaler KI (nichts installiert)
Ollama aus `%LOCALAPPDATA%\TrendoWerkzeuge\ollama` (eigener Start, danach beendet), automatische Wahl **qwen3:14b**. Bedienung über die Oberfläche. Beleg `reports/T-0787/echtlauf-lokale-ki.md`, Rohlog `rohlog-echtlauf.txt` (1/1 bestanden, Exit 0).
Rezepttext „Kartoffelsuppe für 4 Personen …“ → Plan `rezept.anlegen` (21 s) → Vorschau unverändert → „Ja, behalten“ → 6 Zutaten (alle zugeordnet), 4 Schritte, ≈ 305 kcal je Portion (Kern), ein Rückgängig-Schritt → Rückgängig: Buch wie vorher.
**Befund:** „Gemüsebrühe“ rechnet mit „Hühnerbrühe“ (SR Legacy hat keine passende Gemüsebrühe als fertige Brühe) – in der Tabelle sichtbar, per Rechtsklick änderbar.
Erster Anlauf scheiterte nur an `TRENDO_ASSIST_MODELL=qwen3:14b` („unbekanntes Modell“ in `KiModel.Select`) – ohne die Variable wählt Trendo dasselbe Modell selbst.

## Fremdprogramme (Beleg `reports/T-0787/`, Rohlog `rohlog-core-fremd.txt`, 2/2 bestanden)
- **Google Chrome** (headless, eigenes Profil): öffnet die Rezeptseite, `JSON.parse` des Browsers liest das eingebettete JSON-LD: 2 Rezepte vom Typ `Recipe`, 11 Zutaten, Energie je Portion `411 kcal|289 kcal` = Werte von Trendo. Ehrlich: Die Prüfschrift steht in der Seite selbst (`#pruefen`), Chrome führt sie aus – ein unabhängiger schema.org-Validator (z. B. Google Rich Results) wurde nicht benutzt (würde Daten ins Internet senden).
- **LibreOffice** (nur aus der Kopie `T0776_LoKopie`, eigenes Profil): öffnet Rezept-DOCX und -ODT (12 Prüfstellen), Calc öffnet die CSV-Einkaufsliste und schreibt ein PDF.
- **pdftotext** liest Rezept-, Kochbuch- und Wochen-PDF; **qpdf** `--check`: lesbar, keine Fehler (Warnungen zu PDFsharp-Flate-Strömen wie in T-0779).

## Rot-Grün
`T0787_Rot` (nur Namen/Kennungen) im eigenen Worktree auf 9bc135aaa: **rot** („rezepte-programm fehlt“, `reports/T-0787/rot-alter-stand.txt`), auf dem neuen Stand grün. Übrige neue Tests nutzen neue Typen und übersetzen auf dem alten Stand nicht.

## Tests (gezielt, kein Volltest)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core (T0787, PromptActionTests, StudioKnowledge, Assist, NoFixedUser, Tests): **115 gesamt, 113 bestanden, 0 Fehler, 2 übersprungen** (Fremdläufe ohne Variable) – `reports/T-0787/rohlog-core-tests.txt`; QA015_StudioStatus 4/4 (`rohlog-core-qa015.txt`); Fremdlauf mit `TRENDO_T0787_FREMD` 10/10 (`rohlog-core-fremd.txt`).
- App (T0787Rezepte_Ui, T0787_Rot, AssistChain_Ui, ClaudeEverywhere_Ui, DeadButtonTests, ProgramCatalog_Ui, PromptBarTests, StartMenu_Ui, StudioBilder, StudioClickTests, StudioFolds_Ui, StudioIconTests, StudioOrder_Ui, StudioTeams_Ui, StudiosAlle_Ui, StudiosTechnikWissen_Ui, T0728_Bedienung_Ui, T0774KiDokument_Ui): **313 gesamt, 2 Fehler, 1 Errors, 3 übersprungen** (`rohlog-app-tests.txt`). Die 2 Fehler waren veraltete Zahlen/der fehlende Katalogeintrag (`StudioFolds_Ui` 69 → 70, `ProgramCatalog_Ui` „fehlt: rezepte-programm“) – behoben, Nachlauf beider Klassen **4/4 grün** (`rohlog-app-nachlauf.txt`). Das 1 „Error“ ist ein Aufräumfehler der Avalonia-Headless-Plattform („calling thread cannot access this object“) bei `StudioFolds_Ui`; im Nachlauf nicht wieder aufgetreten, nicht aus diesem Paket.
- Echtlauf `Real_local_ki` mit `TRENDO_T0787_ECHT`: 1/1 (`rohlog-echtlauf.txt`).
- Bilder mit Trendo.Render (`reports/T-0787/bilder/`): Profi mit Beispiel (Rezepte, Wochenplan, Einkaufsliste), Einfach leer – Dunkel-Gold, eine goldene Hauptaktion („Neues Rezept“ bzw. im Leerzustand „Erstes Rezept anlegen“), erster Schritt sichtbar. Befund behoben: „1 Stange Lauch“ – „Stange“ ist jetzt Einheit.

## Abnahmekriterien
1. Aus leerer Vorlage echtes Ergebnis: **VOLLWERTIG** – leeres Buch → erster Schritt → Rezept → PDF/JSON-LD/HTML/DOCX (`Klick_bis_datei`), Plan → Einkaufsliste → PDF/CSV/TXT (`Woche_und_einkauf`).
2. Fremddatei rein/raus, Export im Fremdprogramm: **TEILWEISE** – Import: Rezeptseite mit JSON-LD nach Google-Doku-Struktur (selbst nachgebaut, keine fremde Seite geladen); Export öffnet echt in Chrome, LibreOffice, pdftotext, qpdf. Nicht geprüft: Import in Mealie/Tandoor (nicht vorhanden), echte Seite eines Rezeptportals.
3. Abbruch ≤ 30 s: **TEILWEISE / NICHT GETESTET** – Programm-Hülle (Autosicherung 20 s, Wiederherstellung); alle Exporte schreiben erst daneben und ersetzen dann (`RzAtomic`, `AtomicFile`); kein eigener Absturztest.
4. KI im offenen Dokument, rückgängig: **VOLLWERTIG** – Echtlauf qwen3:14b, ein Rückgängig-Schritt.
5. Test vom Klick bis zur Datei: **VOLLWERTIG**.
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **TEILWEISE** – Einfach/Profi (Bild + Test), Rechtsklick (Rezeptliste, Zutat-Zuordnung, Planeinträge) getestet; Ziehen (Umsortieren, Datei ablegen) eingebaut über gemeinsame Bausteine, nicht eigens getestet; Kürzel der Hülle (Strg+Z) nur über Knöpfe getestet.

## Nicht geprüft / offene Punkte
- **QA-015** bleibt bei 91 und ist **grün** (zählt `requirements.yaml`); sie wird rot (bzw. braucht 93), sobald die Hauptsitzung STUDIO-RECIPE-01 (und T-0788) in `requirements.yaml` einträgt. Ebenso „69 Studios“ in der Studio-Übersicht (`StudioFolds_Ui.Overview_*`) und das Startmenü-Register erst mit dem Eintrag.
- `docs/programme/katalog.json`: nur der Eintrag `rezepte-programm` angehängt (aus dem Katalog-Erzeuger); `KATALOG.md` und Testzuordnung erzeugt die Hauptsitzung nach dem Zusammenführen neu.
- Kein Claude-Echtlauf (Vorgabe). Keine Mikronährstoffe, Allergene, Preise, Bilder im Rezept, kein Laden von Webadressen.
- Nährwerte roh, ohne Garverluste; Stückgewichte aus USDA-Portionsangaben (z. B. Ei 50 g, Zwiebel 110 g) – Richtwerte.
- Ein Rest des Rot-Worktrees (`%TEMP%\trendo-T0787-rot`) war beim ersten Aufräumen durch einen Build-Prozess gesperrt (siehe Abgabe).

## requirements.yaml-Vorschlag
```yaml
- id: STUDIO-RECIPE-01
  name: Rezepte & Ernährung
  approved_expansion_functions: '0'
  approval_status: APPROVED_SPEC
  implementation_status: IN_PROGRESS
  test_status: NOT_RUN
  evidence: Freigabe im Chat 09.11.2026 (79 neue Studios), 26.11.2026 Etappe 0.9.115 Wissen & Lernen E (T-0787)
```

## Geänderte Dateien
- neu: `src/Trendo.Core/Wissen/Rezepte.cs`, `src/Trendo.Core/Wissen/NaehrwertTabelle.Daten.cs`, `src/Trendo.Core/PromptAction/RezeptPromptAdapters.cs`, `src/Trendo.App/MainWindow.Rezepte.cs`, `src/Trendo.App/Assets/studios/STUDIO-RECIPE-01.jpg`, `tools/rezepte/naehrwerte_gen.py`, `tools/rezepte/herkunft-usda-sr.tsv`, `docs/nachbau/rezepte/anforderungen.md`, `docs/programme/rezepte-programm/*`
- gemeinsam (klein, am Listenende): `src/Trendo.App/StudioCatalog.cs`, `StudioIcons.cs`, `MainWindow.PromptChips.cs`, `MainWindow.W6.cs`, `src/Trendo.Core/PromptAction/CapabilityCatalog.cs`, `IntelligencePromptModel.cs`, `StudioKnowledge.cs`, `src/Trendo.Core/W2/StudioProfiles.cs`, `StudioTeams.cs`, `tools/studio_bilder.py`, `tools/Trendo.Render/Program.cs`, `docs/design/STUDIOBILDER.json`, `docs/improvement-42/studio-capability-matrix.md`, `docs/programme/katalog.json`, `.gitattributes`
- Dritte Prüfrunde: `tests/Trendo.Core.Tests/T0787Runde3Tests.cs`, `tests/Trendo.App.Tests/T0787Runde3UiTests.cs`, `reports/T-0787/runde3/*`
- Zweite Prüfrunde: `tests/Trendo.Core.Tests/T0787Runde2Tests.cs`, `tests/Trendo.App.Tests/T0787Runde2UiTests.cs`, `reports/T-0787/runde2/*`
- Gegenprüfung: `tests/Trendo.Core.Tests/T0787GegenpruefungTests.cs`, `tests/Trendo.App.Tests/T0787GegenpruefungUiTests.cs`, `tests/Trendo.App.Tests/Daten/einfach-vorher-ausgeblendet.txt`, `reports/T-0787/gegenpruefung/*`
- Tests: neu `tests/Trendo.Core.Tests/T0787RezepteTests.cs`, `T0787FremdTests.cs`, `tests/Trendo.App.Tests/T0787RezepteUiTests.cs`, `T0787RotTests.cs`; Zahlen in `PromptBarTests.cs`, `StudioFoldsUiTests.cs`, `PromptActionTests.cs`
- Belege: `reports/T-0787/*` (mit `SHA256.txt`), `tasks/T-0787-rezepte-ernaehrung.md`, dieser Bericht

**Status:** umgesetzt und gezielt getestet; Merge erst nach OK.
