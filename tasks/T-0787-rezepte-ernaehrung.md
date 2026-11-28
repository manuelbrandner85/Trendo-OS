# T-0787: Wissen & Lernen – „Rezepte & Ernährung“ (Etappe 0.9.115)

**Ziel:** Neues Studio der Kategorie Wissen & Lernen mit echtem Fachkern (Rezepte, Portionen, Nährwerte, Wochenplan, Einkaufsliste, schema.org-Rezept, Druck), freigegeben vom Nutzer am 26.11.2026.
**Branch:** claude/T-0787-rezepte-ernaehrung (Basis main 9bc135aaa = 0.9.114)
**Dateien/Bereich:** neu `src/Trendo.Core/Wissen/Rezepte.cs`, `src/Trendo.Core/Wissen/NaehrwertTabelle.Daten.cs` (erzeugt), `src/Trendo.Core/PromptAction/RezeptPromptAdapters.cs`, `src/Trendo.App/MainWindow.Rezepte.cs`, `tools/rezepte/*`, Bild `STUDIO-RECIPE-01.jpg`; kleine Einträge am Listenende in StudioCatalog, StudioIcons, CapabilityCatalog, StudioProfiles, StudioTeams, StudioKnowledge, PromptChips, IntelligencePromptModel, W6 (Init), `tools/studio_bilder.py`, `docs/design/STUDIOBILDER.json`, Fähigkeitsmatrix; eine Zeile `TRENDO_RZ_MODE` in `tools/Trendo.Render/Program.cs` (Bildprüfung der Ansichten).
**Fertig, wenn:**
- [x] Rezepte mit Zutaten (zeilenweise, Menge/Einheit/Zutat erkannt), Mengen und Schritten
- [x] Portionen umrechnen (Ansicht und Übernehmen) mit Einheiten (Brüche, g↔kg, TL→EL, Löffel → g/ml)
- [x] Nährwerte je Portion aus frei lizenzierter Tabelle (USDA FoodData Central SR Legacy, CC0 1.0), als Richtwert gekennzeichnet, ohne Gesundheitsversprechen
- [x] Wochenplan und zusammengefasste Einkaufsliste
- [x] Import/Export schema.org/Recipe (JSON-LD, Rezeptseite HTML) – ein Fremdprogramm (Chrome) liest das JSON-LD
- [x] Druck/PDF (Rezept, Kochbuch, Woche) und DOCX/ODT, Einkaufsliste TXT/CSV
- [x] KI-Fachaktion `rezept.anlegen` über `ProgramSpec.KiActions`; Echtlauf qwen3:14b mit Rohlog unter `reports/T-0787/`
- [x] gezielte Testklassen grün; Rot-Grün-Beleg
**Nicht tun:** `requirements.yaml` nicht ändern (Vorschlag im Bericht), QA-015 bleibt bei 91, kein Merge, kein Release, kein Push nach main, keine Daten ohne klare Lizenz, kein Claude-Echtlauf.
**Am Ende:** Branch pushen, Bericht `tasks/T-0787-rezepte-ernaehrung.bericht.md`.
