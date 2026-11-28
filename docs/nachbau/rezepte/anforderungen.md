# Rezepte & Ernährung – Anforderungen (Clean-Room)

Freigabe: 79 neue Studios am 09.11.2026, Etappe 0.9.115 „Wissen & Lernen E“ am 26.11.2026 (T-0787). Kategorie: Wissen & Lernen. Id `STUDIO-RECIPE-01`, Programm `rezepte-programm`.
Eigene Worte, kein fremder Code, keine fremden Texte, Symbole oder Grafiken. Keine Downloads oder Testversionen von Programmen – nur öffentliche Doku, Produktseiten und Fachvergleiche.

## Vorbilder (nur Bedienkonzept)

| Vorbild | Was es gut macht | Lizenz / Zugang | Quelle (abgerufen 28.11.2026) |
|---|---|---|---|
| Paprika Recipe Manager | Rezeptbuch offline, Wochen-/Monatsplan, Einkaufsliste fasst gleiche Zutaten zusammen und sortiert nach Abteilung („aisle“), Nährwerte aus USDA-Daten berechnet | proprietär | paprikaapp.com (Funktionsseite); cooklang.org „Best Recipe Management Software in 2026“ (18.06.2026, aktualisiert 19.11.2026) |
| Mealie | Rezepte mit Zutaten-Erkennung (Menge, Einheit, Zutat), Essensplan mit Mahlzeiten (Frühstück, Mittag, Abend, Snack), Einkaufsliste aus Rezepten, Import über schema.org-Rezepte von Webseiten | AGPL-3.0 (github.com/mealie-recipes/mealie) – nur Clean-Room | docs.mealie.io (FAQ „Ingredient parser“, Features); cooklang.org Vergleich Mealie/Paprika/KitchenOwl (2026) |
| Tandoor Recipes | Lebensmittel mit Eigenschaften (Nährwerte) – je Rezept automatisch berechnet, Einheitenumrechnung, Plan → Einkaufsliste, Import aus 20+ Formaten | Lizenz im Repo ohne SPDX-Kennung (GitHub: NOASSERTION; laut Vergleich AGPL mit Zusatzklausel) – nur Clean-Room | newreleases.io (Tandoor 1.5.0: unit conversion, food properties), cooklang.org „Tandoor vs Mealie vs KitchenOwl“ |
| AnyList / Cooklang (CookCLI) | Rezept skalieren, Einkaufsliste aus mehreren Rezepten | AnyList proprietär; cooklang-rs MIT | cooklang.org Vergleich (2026); github.com/cooklang/cooklang-rs (MIT) |
| schema.org „Recipe“ + Google-Doku für Rezepte | offenes Austauschformat: `recipeIngredient`, `recipeInstructions` (HowToStep/HowToSection), `recipeYield`, ISO-8601-Zeiten, `nutrition` (NutritionInformation) | schema.org CC BY-SA 3.0 (nur Spezifikation gelesen, nichts kopiert) | schema.org/Recipe; developers.google.com Rezept-Daten |

Offene Alternativen: Mealie (AGPL-3.0), KitchenOwl (AGPL-3.0), Tandoor (Lizenz unklar, nicht verwendet), Cooklang (MIT), grocy (MIT, Vorrat). **Kein Code übernommen** – nur Verhalten.

**Nährwert-Tabelle:** USDA FoodData Central, *SR Legacy* (April 2018), **gemeinfrei, CC0 1.0 Universal** (Angabe auf fdc.nal.usda.gov; Bitte um Quellenangabe wird erfüllt). Geladen am 28.11.2026 von
`fdc.nal.usda.gov/fdc-datasets/FoodData_Central_sr_legacy_food_csv_2018-04.zip` (6 074 592 Bytes, SHA-256 `b80817294b8850530aaedf2e515c02593b1824f763a0ff356e5c2081643e6fd0`).
160 Lebensmittel, je Zeile FDC-Nummer und Originalbeschreibung (`tools/rezepte/herkunft-usda-sr.tsv`); erzeugt mit `tools/rezepte/naehrwerte_gen.py` → `src/Trendo.Core/Wissen/NaehrwertTabelle.Daten.cs`.
Deutsche Namen, Suchwörter und Ladenabteilungen sind eigene Zuordnung. Salz = Natrium × 2,5.
**Nicht verwendet:** Bundeslebensmittelschlüssel 4.0 (seit 16.12.2025 kostenfrei, „ohne Lizenzbarrieren“ – aber ohne benannte Lizenz und ohne Regel zur Weitergabe; blsdb.de/faq, abgerufen 28.11.2026), Open Food Facts (ODbL, Teilen-unter-gleichen-Bedingungen für die Datenbank).

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Stand |
|---|---|---|---|---|---|
| RZ-01 | Rezept anlegen | Name, Beschreibung, Portionen (+ Wort wie „Stück“), Vorbereitungs-/Garzeit, Kategorie, Stichwörter, Notizen, Quelle; Liste links mit Suche, Rechtsklick (Plan, Kopieren, Verschieben, Löschen), Ziehen zum Umsortieren | UI `Klick_bis_datei`, `Import_und_bedienung` | Paprika, Mealie | erfüllt (Ziehen nur über den gemeinsamen Baustein `BueroReorder`, nicht eigens getestet) |
| RZ-02 | Zutaten wie im Kochbuch | eine Zutat je Zeile; erkannt werden Menge (1,5 · 1/2 · ½ · 1 ½ · 2–3), Einheit (g, kg, ml, l, EL, TL, Tasse, Prise, Msp., Stück, Zehe, Dose, Pck., Bund, Stange …), Name, Hinweis nach Komma, „nach Geschmack“, Zwischenüberschrift „Für den Teig:“ | Core `Zutaten_lesen` | Mealie-Parser (Verhalten) | erfüllt |
| RZ-03 | Schritte | ein Schritt je Zeile, Nummern „1.“ werden entfernt; Druck nummeriert | UI `Klick_bis_datei` | alle | erfüllt |
| RZ-04 | Portionen umrechnen | „–/+“ zeigt Mengen für n Portionen (nur Ansicht), „So ins Rezept übernehmen“ rechnet um (ein Rückgängig-Schritt); halbe/doppelte Menge; Brüche bei Löffeln und Stück, passende Einheit (1000 ml → 1 l, 3 TL → 1 EL) | Core `Portionen_und_einheiten`, UI `Klick_bis_datei` | AnyList, Cooklang | erfüllt |
| RZ-05 | Einheiten umrechnen | Löffel/Tasse/Becher → Gramm (feste Zutat über Dichte aus USDA-Portionen) bzw. Milliliter (Flüssigkeit); Festlegung 1 EL = 15 ml, 1 TL = 5 ml, 1 Tasse = 250 ml | Core `Portionen_und_einheiten` | Tandoor | erfüllt |
| RZ-06 | Nährwerte je Portion | Energie (kcal/kJ), Fett, gesättigte Fettsäuren, Kohlenhydrate, Zucker, Ballaststoffe, Eiweiß, Salz je Portion; Grammbestimmung über Masse, Dichte, Stückgewicht, Packung; nicht erfasste Zutaten und Zutaten ohne Menge werden genannt | Core `Naehrwerte` (Rechnung gegen Tabellenwerte nachgerechnet), UI `Klick_bis_datei` | Paprika, Tandoor | erfüllt |
| RZ-07 | Richtwert, keine Versprechen | überall „Richtwerte“ mit Quelle und Lizenz; keine Ernährungs- oder Gesundheitsberatung; Heil-/Gesundheitsversprechen werden in KI-Text abgelehnt und im eigenen Text als Hinweis gemeldet | Core `Keine_versprechen`, UI `Ki_rezept` | Auftrag | erfüllt |
| RZ-08 | Zuordnung von Hand | Klick/Rechtsklick auf „gerechnet als“: bessere Treffer aus der Tabelle oder „Nicht mitrechnen“; bleibt beim Ändern anderer Zeilen erhalten | UI `Import_und_bedienung` | Tandoor | erfüllt |
| RZ-09 | Wochenplan | sieben Tage ab Montag, Mahlzeit, Portionen je Eintrag; Woche vor/zurück; Rechtsklick: anderer Tag, ±1 Portion, entfernen; gelöschtes Rezept fällt aus dem Plan | UI `Woche_und_einkauf`, Core `Einkaufsliste` | Mealie, Paprika | erfüllt |
| RZ-10 | Einkaufsliste zusammengefasst | aus dem Plan; gleiche Zutaten über alle Rezepte addiert (g mit g, ml mit ml, Stück; gemischt über Dichte), nach Abteilung im Laden; „für: …“; abhaken; eigene Einträge | Core `Einkaufsliste`, UI `Woche_und_einkauf` | Paprika („combines“, „aisle“) | erfüllt |
| RZ-11 | Import schema.org | JSON-LD-Datei oder gespeicherte Rezeptseite (HTML): `@graph`, `@type` als Liste, HowToSection, `recipeYield` als Liste/Text, ISO-Dauer; Datei bleibt unverändert; kaputtes JSON verständlich gemeldet; auch per Ziehen auf die Arbeitsfläche | Core `JsonLd_hin_und_zurueck`, UI `Import_und_bedienung` | Mealie/Tandoor-Import, Google-Doku | erfüllt (Ziehen über `BueroFileDrop`, nicht eigens getestet) |
| RZ-12 | Export schema.org | JSON-LD (`Recipe`, ein Rezept als Objekt, mehrere als `@graph`) mit `nutrition` je Portion als Richtwert; Rezeptseite HTML mit eingebettetem JSON-LD | Core `JsonLd_hin_und_zurueck`; **echt**: Chrome liest das JSON-LD (Fremdnachlauf) | schema.org | erfüllt (Import in Mealie/Tandoor selbst nicht geprüft – nicht vorhanden) |
| RZ-13 | Druck/PDF | Rezept (in den angezeigten Portionen), Kochbuch mit Inhaltsverzeichnis, Wochenplan mit Einkaufsliste; DOCX/ODT; Einkaufsliste als TXT/CSV (Formel-Schutz) | Core `Druck_dateien`, UI `Klick_bis_datei`, `Woche_und_einkauf`; **echt**: LibreOffice, pdftotext, qpdf | Paprika (Druck) | erfüllt |
| RZ-14 | Rezept aus Text | eingefügter Text → Name, Portionen, Zutaten, Schritte | Core `Aus_text`, UI `Woche_und_einkauf` | Paprika („copied text“) | erfüllt |
| RZ-15 | KI-Fachaktion | `rezept.anlegen` über `ProgramSpec.KiActions`: Vorschau → „Ja, behalten“ → ein Rückgängig-Schritt; Plan und Liste gesperrt; Nährwerte rechnet der Kern | UI `Ki_rezept`; **Echtlauf** qwen3:14b `Real_local_ki` | Auftrag | erfüllt |
| RZ-16 | Einfach/Profi, Kürzel | Einfach: Neues Rezept, Öffnen, Portionen übernehmen, Auf den Plan; Profi: alles; Strg+Z/Strg+Y der Hülle | UI `Import_und_bedienung`, `Klick_bis_datei` | Trendo-Regel | teilweise (Menüleisten-Kürzel nicht einzeln getestet) |

## Regeln (Prüfrunden T-0787)

- **Punkt in Mengen:** „1.000,5“ = 1000,5 (deutsch, eindeutig); „0.250“ = 0,25 (führende 0 ist nie Tausenderpunkt); „1.500 kg/l/Pfund/dl/Tasse/Becher“ = 1,5; „1.500 g/ml/Stück/…“ und ohne Einheit = 1500. Punkt mit drei Ziffern meldet Trendo immer; beim Einlesen und aus der KI steht „Menge prüfen: …“ im Hinweis der Zutat.
- **Gesundheitsaussagen, zweistufig (3. Prüfrunde):** (a) Sperre nur bei eindeutigen Aussagen – Wirk-Verb + Krankheits-/Körperbegriff aus fester Liste, Körperbegriff + „-senkend/-hemmend …“, feste Begriffe (Detox, Superfood, entzündungshemmend, antioxidativ), Abnehmen als Diät; (b) weiche Wörter („gesund“, „gesündere Variante“, Heiltee) nur „Bitte prüfen“. Kochsätze („hilft gegen das Ausflocken“, „vom Herd abnehmen“) bleiben frei. Wortlisten – **nicht vollständig**.
- **Zahlen:** dieselbe Regel für beide Zahlen eines Bereichs („1.500-2.000 g“); Komma + drei Ziffern vor g/ml = englisches Tausenderkomma (mit Hinweis), sonst Dezimalkomma.
- **Englisch:** cup, tbsp, tsp, fl oz, pint, quart, oz, lb; häufige englische Zutatennamen auf dieselben USDA-Zeilen; Nicht-Zugeordnetes meldet der Import.
- **Einlesen:** im Hintergrund, eine Datei nach der anderen, kein zweites Einlesen gleichzeitig; ist das Buch inzwischen geschlossen, wird nichts übernommen. Zeiten höchstens 7 Tage.

## Nicht gebaut (bewusst)

- Rezept direkt von einer Webadresse laden (Netzabruf, fremde Inhalte) – stattdessen gespeicherte Seite oder JSON-LD-Datei.
- Mikronährstoffe, Allergene, Preise, Vorrat; Bilder im Rezept; Monatsansicht; geteilte Listen.
- Cooklang- und Paprika-Format (nur schema.org als verbreitetes offenes Format).
