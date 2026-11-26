# Bericht T-0781: Mathe-Helfer und Naturwissenschaft (Wissen & Lernen)

**Auftrag:** `tasks/T-0781-mathe-naturwiss.md` (Freigabe 26.11.2026, Etappe 0.9.113) · **Branch:** `claude/T-0781-mathe-naturwiss` (Basis main 0.9.112 = 184fcbbfc) · eigene Kopie `%TEMP%\trendo-auftrag-T-0781`.

## Nachbesserung nach der Gegenprüfung („nicht freigabefähig“, fachlich falsche Ergebnisse)

Grundregel umgesetzt: Kann der Kern eine Aufgabe nicht sicher vollständig lösen, ist die Probe **gelb/unsicher** (`Probe.Unsicher`, `Loesung.ProbeOk = false`, Ergebniszeile „(unsicher – siehe Probe)“, Liste „?“, PDF „Probe unsicher:“) – keine falsche Antwort mit grüner Probe. Keine Probe benutzt denselben Suchweg wie die Lösung. Neu: `src/Trendo.Core/Wissen/MatheGleichung.cs`.

| Nr. | Befund | Behebung | Regressionstest (rot auf 5f38f9b4c → grün) |
|---|---|---|---|
| 1 HOCH | Variable im Nenner: `1/x=2` u. a. falsch, 30 leere Schritte | Bruchgleichungen über Zähler/Nenner (`Rational`): Definitionsmenge, Hauptnenner, Polynom lösen, Nennernullstellen ausschließen; Umformen ohne leere Schritte, Abbruch statt Endlosschleife; `x/x = 1` → „jede Zahl außer x = 0“, `x²/x = 0` und `1/x = 0` → keine Lösung | Core `T0781GegenprobeTests.Nenner` (5 Fälle), `Definitionsmenge` (3) |
| 2 HOCH | Verlorene Lösungen | Umkehren mit Fallunterscheidung: Betrag und gerade Potenzen (±), `(ln x)² = 4` → e⁻², e²; sin/cos/tan mit allgemeiner Lösung (`π/6 + 2kπ oder 5π/6 + 2kπ (k ∈ ℤ)`), `sin(2x)` mit Periode π; |c| > 1 → keine Lösung. Probe: Einsetzen (k = −1…2) und **eigene Nullstellensuche** von links − rechts (Abtasten, Polstellen ausgesondert, Berührpunkte) – Abweichung = gelb | `Alle_loesungen` (4), `Trig` (4 + sin x = 2) |
| 3 HOCH | Nullstellen nur in [−1000, 1000] mit Schritt 0,1 | Quadratfreier Anteil, Cauchy-Schranke, Nullstellen der Ableitung als Trenner, Vorzeichen **exakt mit Brüchen**, Intervallhalbierung; Grad > 64 ehrlich abgelehnt bzw. sauber gelöst; **Probe: Sturm-Kette** zählt die verschiedenen reellen Nullstellen unabhängig (Grad > 30: gelb) | `Nullstellen` (x³ − 2000000001, x⁴ − 4,0001x² + 4,0002 mit 4 Lösungen, x⁵ − x − 1, x^720720 − 1 → ±1) |
| 4 HOCH | ∫ über Polstellen falsch | Lücken im Intervall (Nenner-, ln-, Wurzelstellen) erkennen, uneigentliches Integral per Grenzwert von F, sonst „existiert nicht“; Probe ohne Stammfunktion: adaptive Simpson-Regel mit ausgesparten Lücken bzw. Wachstumstest | `Uneigentlich` (6: ∫₋₁¹1/x², 1/x, ∫₀¹1/(x−0,5), ∫₋₂⁰1/(x+1) existieren nicht; ∫₀¹1/√x = 2; ∫₀¹ln x = −1) |
| 5 HOCH | ΔT in °C | Temperaturdifferenz ohne Nullpunktverschiebung (`InSi/Aus(…, differenz)` für Größen mit Δ); „30 °C − 20 °C = 10 K“, „20 °C + 5 K“ | `DeltaT` (Q = 83 720 J) |
| 6 HOCH | 3e5, 6,022e23, 1,6e-19 | Termleser liest Zehnerpotenzen; `2e`, `e^2`, `3·e` bleiben die Konstante e | `Zehnerpotenz` |
| 7 HOCH | Einfrieren | `Bruch.ISqrt/IRoot` mit Newton; `Bruch.Hoch` begrenzt die Bits (200 000); große Potenzen klar abgelehnt; `Rechenzeit` (3 s, je Thread) in allen langen Schleifen (Teiler, Eingrenzung, Sturm, Wurzelzerlegung); Oberfläche rechnet im Hintergrund (¼ s sofort, sonst „Rechnet …“, Neuzeichnen nach Ende) | Core `Kein_einfrieren` (4), `Isqrt`; UI `Gegenpruefung_einfrieren_und_gelb` |
| 8 HOCH | PDF-Hochzahlen | Folgen zusammenhängend: 10⁻¹⁹ → 10^(-19), xⁿ⁻¹ → x^(n-1), SO₄²⁻ → SO4^(2-); Beleg neu erzeugt (`reports/T-0781/fremdprogramm/`) | `Pdf_hochzahlen`, `MatheKernTests.Pdf_zeichen` |
| M | Linse f = g | „b = ∞ – Bild im Unendlichen“, Teilen durch 0 sonst „nicht bestimmbar“ mit gelber Probe | `Linse` |
| M | Sperre gegen fertige Ergebnisse | beide Seiten vorher vereinfacht (`x + 0 = 5`, `1x = 5`, `2x/2 = 5`) | UI `Gegenpruefung_ki_sperren` |
| M | Pfeile ⇌/⟶ im Faktor-Filter | ergänzt | UI `Gegenpruefung_ki_sperren` |
| M | Löschen/Kopieren für die KI | `ProgramCmd.OhneKi` (mh-loeschen, mh-kopie, mh-hoch, mh-runter, mh-beispiel, nw-loeschen) | UI `Gegenpruefung_ki_sperren` |
| N | Definitionsbereich bei `x/x`, `x²/x` | Schritt „Definitionsbereich … x ≠ 0“ | `Niedrig` |
| N | falsch rote Proben `x⁰`, `(−8)^(1/3)` | Variablen der Probe aus dem Term; ungerade Wurzel durch Potenzieren gegengerechnet; zu große Zahlen gelb statt rot | `Niedrig` |
| N | `Cu2+` ohne `^`, mL, mT, Lu/Lr | Ladung am Ende (einatomig: Ziffern = Ladung; mehratomig: zwei Ziffern → letzte ist Ladung, NH4+ bleibt NH₄⁺); Liter und Tesla mit Vorsätzen; Lu und Lr in Gruppe 3 (La–Yb, Ac–No in der Fußzeile) | `Niedrig_natur`, `NaturKernTests.Neu2024` |

Rot-Lauf: `reports/T-0781/nachbesserung/rot-vorher.txt` – alle 30 Fälle der neuen Core-Klasse rot gegen 5f38f9b4c. **Ehrlich:** `Kein_einfrieren` meldete auf dem alten Stand 3 Fälle als bestanden (dort warf der alte Stand schon eine Überlaufmeldung); der Einfrier-Befund des Prüfers betraf „Vereinfachen“ von `(10^4000)^4000` über 15 s – jetzt sofort abgelehnt. Die zwei neuen UI-Tests nutzen die neue Eigenschaft `Unsicher` bzw. die Sperrliste; auf dem alten Stand übersetzt die Testdatei nicht (`reports/T-0781/nachbesserung/rot-vorher-app.txt`).
Gezielte Läufe: Core `rohlog-core-tests.txt`: 313 bestanden, 1 übersprungen (PubChem-Abgleich ohne CSV). App `rohlog-app-tests.txt`: 377 gesamt, 373 bestanden, 4 übersprungen (Echtläufe ohne Variable), 0 rot – darin T0781Wissen_Ui, StudioBilder und 25 Querschnittsklassen; `StartMenu_StudioFenster` nicht erneut gelaufen (rot bis zum requirements.yaml-Eintrag, unverändert). Merge-Hinweis: `ProgramKiAction.Hinweis` unverändert (T-0779 bringt `bool MakesFile`).

## Was war → was ist
| Punkt | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| Mathe-Helfer | nur Rechner mit Funktionsgraph (`MainWindow.Helfer.cs:28,268`), keine Lösungswege (`fehlende-studios.md`, Rang 3) | Studio `STUDIO-MATH-01`, Programm `mathe-helfer-programm`: eigener Rechenkern `src/Trendo.Core/Wissen/` (Bruch, Term + Leser, Polynom, WurzelZahl, MatheKern, Rechenblatt) – vereinfachen, ausmultiplizieren (binomische Formeln), faktorisieren (Ausklammern, Nullstellen, Horner), lineare/quadratische/höhere Gleichungen (p-q-Formel, Lösungen mit √ exakt), Umkehrgleichungen (√, aˣ, ln, sin), Gleichungssysteme (Gauß mit Brüchen), Bruchrechnen (kgV, Kürzen, Kehrwert), Ableiten (alle Grundregeln), Integrieren (Grundintegrale, lineare Substitution, partielle Integration, bestimmte Integrale); Arbeitsfläche wie ein CAS-Blatt mit Graph (GraphView, jetzt in Gold) | Core `MatheKernTests` (81 Fälle, jede Lösung zusätzlich vom Test nachgerechnet, 200 Zufallsgleichungen), UI `T0781Wissen_Ui` |
| Probe | – | jede Lösung mit Probe, unabhängig vom Lösungsweg: exaktes Einsetzen (auch in ℚ(√m)), Ausmultiplizieren der Faktoren, Gegenrechnung mit dem Taschenrechner `Calc`, Differenzenquotient, Ableiten der Stammfunktion, Simpson-Regel | Core `MatheKernTests` |
| Naturwissenschaft | nur `wissenschaft` (Statistik) | Studio `STUDIO-SCIENCE-01`, Programm `naturwissenschaft-programm`: Periodensystem (118 Elemente, eigene Tabelle nach IUPAC/CIAAW 2021 + Änderungen 2024, abgeglichen mit PubChem), Molmasse mit Rechenweg, Reaktionen ausgleichen (Nullraum exakt, Ionen/Redox, Probe Atom/Ladung/Masse), Einheiten mit Dimensionsprüfung, Formelsammlung (27 Formeln, jede Umstellung gegengerechnet), Unsicherheit über `StatsPlus.Propagate` (SPEC-05), Laborheft | Core `NaturKernTests` (47), Abgleich `reports/T-0781/elemente-abgleich.txt` |
| KI-Fachaktion | – | `mathe.rechnung` / `natur.rechnung` über `ProgramSpec.KiActions` (T-0774): KI nennt nur die Aufgabe, der Kern rechnet; Vorschau, „Ja, behalten“, ein Rückgängig-Schritt; fertige Ergebnisse („x = 5“) und KI-Faktoren werden abgelehnt; `KiLocked` sperrt die Rechnungen gegen „dokument.setzen“; Menübefehle „Rechnen/Ins Laborheft“ für die KI gesperrt (Befund Echtlauf 3) | UI `Mathe_ki_textaufgabe`, `Mathe_ki_rechnet_nie`, `Natur_ki_textaufgabe`, `Eingebunden`; **Echtlauf qwen3:14b bestanden** `reports/T-0781/echtlauf-lokale-ki.md` |
| Export | – | Mathe: PDF, LaTeX, Markdown; Natur: PDF, CSV, Markdown, Periodensystem-CSV; PDF-Zeichen ohne Arial-Glyphe werden ersetzt (Befund pdftotext) | UI `Mathe_klick_bis_datei`, `Natur_klick_bis_datei`, Core `Pdf_zeichen`; Fremdprogramm-Beleg |
| KI-Auftrag ohne Dokument | – | Adapter `mathe.loesen`, `natur.rechnen` (Markdown vom Kern) | Core `WissenAdapterTests` |
| Gemeinsame Teile | – | `ProgramKiAction.Hinweis` (Vorschau-Text für Fachaktionen ohne Datei), `GraphView.Farben` | – |

**Bibliothek/Rust:** AngouriMath (MIT, 2.9.0 vom 24.11.2026) geprüft und verworfen – liefert keine Lösungsschritte; eigener Kern ohne neue Abhängigkeit. UnitsNet verworfen (schon früher, keine deutschen Namen; Unsicherheiten fehlen). Details in `docs/nachbau/*/anforderungen.md`.

## Vorbilder (Clean-Room, nur Bedienung)
| Studio | Vorbilder (Quellen und Datum in der Anforderungsliste) |
|---|---|
| Mathe-Helfer | GeoGebra CAS (Handbuch, Lizenz Stand 11/2025), Photomath (Google-Hilfe, Common Sense Media), Symbolab (Produktseite, Test 2026), Wolfram\|Alpha Step-by-step; Microsoft Math Solver eingestellt 07.07.2025 |
| Naturwissenschaft | Ptable (Stand 03.10.2024), ChemDoodle 2D (v12.12.1), PhET „Balancing Chemical Equations“ (Lehrerhandreichung, Lizenzwechsel 29.03.2026), WebQC Balancer |

## Anforderungen erfüllt / teilweise / fehlt
- **Mathe-Helfer** (`docs/nachbau/mathe-helfer/anforderungen.md`, MH-01…MH-20): erfüllt 17; teilweise 3 – MH-07 (höhere Grade ohne rationale Nullstelle nur als Näherung), MH-08 (Umkehrgleichungen nur bei einmal vorkommender Variable), MH-12 (keine allgemeine Substitution/Partialbrüche). Ziehen und Ablegen ist gebaut (`BueroFileDrop`), aber nur über „Aufgabenblatt öffnen …“ getestet.
- **Naturwissenschaft** (`docs/nachbau/naturwissenschaft/anforderungen.md`, NW-01…NW-18): erfüllt 16; fehlt 2 – NW-17 Strukturformeln/3D/Simulationen, NW-18 Isotope/Temperaturregler (nicht im Auftrag).

## requirements.yaml-Vorschlag
`requirements.yaml` ist für Agenten gesperrt und **nicht geändert**. Vorschlag (nach dem Block `STUDIO-FORMS-01` einfügen):
```yaml
- id: STUDIO-MATH-01
  name: Mathe-Helfer
  approved_expansion_functions: '0'
  approval_status: APPROVED_SPEC
  implementation_status: IN_PROGRESS
  test_status: NOT_RUN
  evidence: Freigabe im Chat 09.11.2026 (79 neue Studios), Etappe Wissen & Lernen 26.11.2026 (T-0781)
- id: STUDIO-SCIENCE-01
  name: Naturwissenschaft
  approved_expansion_functions: '0'
  approval_status: APPROVED_SPEC
  implementation_status: IN_PROGRESS
  test_status: NOT_RUN
  evidence: Freigabe im Chat 09.11.2026 (79 neue Studios), Etappe Wissen & Lernen 26.11.2026 (T-0781)
```
**Folge, solange der Eintrag fehlt:** Die Studios stehen nicht in der Übersicht/Startmenü (Liste kommt aus requirements.yaml), und `StartMenu_StudioFenster.Jedes_Studio` ist **rot**. Mit genau diesem Vorschlag (nur in der Testkopie unter `bin/`, nicht im Repo) lief die Klasse grün: `reports/T-0781/rohlog-startmenue-mit-requirements-vorschlag.txt` (5/5).

## Tests (gezielt, kein Volltest)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core (`reports/T-0781/rohlog-core-tests.txt`): MatheKernTests, NaturKernTests, WissenAdapterTests, PromptActionTests, T0777StilleTests, KreativKits*, BueroTests – **279 bestanden, 1 übersprungen** (PubChem-Abgleich ohne CSV; mit CSV bestanden: `rohlog-elemente-abgleich.txt`). `docs/improvement-42/studio-capability-matrix.md` mit `TRENDO_UPDATE_MATRIX=1` neu erzeugt; Zählungen 107→109 Adapter, 78→80 „partial“, 83→85 Studios, 61→63 in der Übersicht angepasst.
- App (`reports/T-0781/rohlog-app-tests.txt`): T0781Wissen_Ui, StudioBilder und 26 Querschnittsklassen (u. a. ProgramHeadDesign_Ui „eine goldene Aktion“, DesignPaket3/4, ClaudeBridge, AssistChain, DeadButtonTests, StudiosAlle, StudioIcons, StudioTeams, T0774KiDokument, Helfer) – **380 gesamt, 375 bestanden, 4 übersprungen (Echtläufe ohne Variable), 1 rot: `StartMenu_StudioFenster.Jedes_Studio`** (nur wegen fehlendem requirements.yaml-Eintrag, siehe oben).
- Rot-Grün: neue Core-Testklassen gegen 184fcbbfc übersetzen nicht (`reports/T-0781/rot-alter-stand.txt`); UI-Tests brauchen die neuen Programme. Der Regressionstest für Befund „Ins Laborheft für die KI“ wurde nach dem Befund geschrieben (die alten Befehlsnamen sind umbenannt, ein Rot-Lauf mit den alten Namen wurde nicht gemacht).

## Echtlauf lokale KI
Ollama, qwen3:14b, über die Oberfläche: Mathe „Rechteck 3 cm länger als breit, 40 cm²“ → `mathe.rechnung {gleichung, x*(x+3) = 40}` → Kern: x₁ = −8, x₂ = 5, Probe exakt; Natur „molare Masse Glucose“ → `natur.rechnung {molmasse, C6H12O6}` → 180,156 g/mol; je ein Rückgängig-Schritt, Rückgängig stellt den alten Stand her. **Ehrlich:** drei frühere Versuche schlugen fehl (ungültiges JSON von qwen3; fehlendes Protokoll; GPU durch andere Sitzungen belegt → Ausweichen auf mistral:7b, das bei Natur den Menübefehl wählte → Fehler behoben). Alle Versuche liegen unter `reports/T-0781/`. Kein Claude-Echtlauf (Auftrag).

## Abnahmekriterien
1. Aus leerer Vorlage echtes Ergebnis über die Oberfläche: **VOLLWERTIG** (beide: `Mathe_klick_bis_datei`, `Natur_klick_bis_datei`).
2. Fremddatei rein/raus: **VOLLWERTIG** für Mathe-Helfer (Aufgabenblatt aus LibreOffice → einlesen → bearbeiten → PDF/MD/TeX; PDF öffnet in LibreOffice Draw und pdftotext), **TEILWEISE** für Naturwissenschaft (Export öffnet in LibreOffice/pdftotext; kein Import einer Fremddatei – Messreihen-Import liegt im Programm „Messdaten & Auswertung“). LaTeX nicht übersetzt (kein TeX auf dem PC).
3. Abbruch ≤ 30 s: **VOLLWERTIG** für Mathe-Helfer (`Mathe_absturz`: 20-s-Takt, Absturz, Wiederherstellen), Naturwissenschaft nutzt dieselbe Hülle – **NICHT GETESTET** einzeln.
4. KI im offenen Dokument, rückgängig: **VOLLWERTIG** (beide, Echtlauf qwen3:14b).
5. Test vom Klick bis zur Datei: **VOLLWERTIG** (beide).
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **TEILWEISE** – Bedienstufen (LaTeX nur Profi, Menüband je Stufe, Bild geprüft), Enter/Entf/↑↓, Rechtsklick-Menüs getestet; Ziehen und Ablegen gebaut, aber nicht im Test ausgelöst.

## Nicht geprüft / offene Punkte
- requirements.yaml-Eintrag (gesperrt) – erst danach erscheinen die Studios in Übersicht und Startmenü; `StartMenu_StudioFenster` bis dahin rot.
- Kein Volltest (macht die Hauptsitzung). Claude nicht beteiligt.
- Ziehen und Ablegen nicht im Test ausgelöst; Naturwissenschaft ohne eigenen Absturztest.
- KI-Planung mit kleinen Modellen unzuverlässig (Versuche 1–3); Modelle/Grafikkarte werden von parallelen Sitzungen geteilt.
- `AssistPlanner.FromKi` lehnt leicht kaputtes JSON ab (Versuch 1) – gemeinsamer Code, nicht angefasst.
- Studio-Bilder lokal mit ComfyUI + FLUX.2 klein erzeugt (`tools/studio_bilder.py --ids STUDIO-MATH-01 STUDIO-SCIENCE-01`); ComfyUI danach beendet.

## Geänderte Dateien
- neu: `src/Trendo.Core/Wissen/{Bruch,Term,Algebra,MatheKern,Rechenblatt,Elemente,Chemie,Groessen,Formelsammlung}.cs`, `src/Trendo.Core/PromptAction/WissenPromptAdapters.cs`, `src/Trendo.App/MainWindow.MatheHelfer.cs`, `src/Trendo.App/MainWindow.Naturwissenschaft.cs`, `src/Trendo.App/Assets/studios/STUDIO-MATH-01.jpg`, `STUDIO-SCIENCE-01.jpg`, `tests/Trendo.Core.Tests/{MatheKernTests,NaturKernTests,WissenAdapterTests}.cs`, `tests/Trendo.App.Tests/T0781WissenUiTests.cs`, `docs/nachbau/mathe-helfer/anforderungen.md`, `docs/nachbau/naturwissenschaft/anforderungen.md`, `reports/T-0781/**`, `tasks/T-0781-*`
- geändert (klein, am Ende der Listen): `src/Trendo.App/StudioCatalog.cs`, `StudioIcons.cs`, `MainWindow.PromptChips.cs`, `MainWindow.W6.cs` (Init), `GraphView.cs` (Farben), `MainWindow.ProgramShell.cs` (`ProgramKiAction.Hinweis`), `MainWindow.Assist.cs` (Hinweis in der Vorschau), `src/Trendo.Core/PromptAction/CapabilityCatalog.cs`, `IntelligencePromptModel.cs`, `StudioKnowledge.cs`, `src/Trendo.Core/W2/StudioProfiles.cs`, `StudioTeams.cs`, `tools/studio_bilder.py`, `docs/improvement-42/studio-capability-matrix.md`, `.gitattributes`, Zählungen in `tests/Trendo.Core.Tests/PromptActionTests.cs`, `tests/Trendo.App.Tests/PromptBarTests.cs`, `StudioFoldsUiTests.cs`

**Status:** umgesetzt und gezielt getestet; requirements.yaml-Eintrag, Merge und Volltest durch die Hauptsitzung.

## 2. Prüfrunde (Nachbesserung 2)
Jeder Punkt hat einen Regressionstest; Rot-Läufe gegen 8a5e74012 in `reports/T-0781/nachbesserung2/`.

| Punkt | Änderung | Test |
|---|---|---|
| HOCH N1 Simpson | `Simpson`: `Rechenzeit.Pruefe()` in jeder Stufe, höchstens 200 000 Funktionswerte, Abbruch bei NaN/∞; vorab Abtastung (400 Punkte): ist f auf einem Stück nicht reell (√x, ln x, 1/√x auf [−1, 1]), lautet das Ergebnis ehrlich „… ist nicht definiert auf dem ganzen Intervall“ mit Gegenprobe; reelle Kubikwurzel (`x^(1/3)`, `x^(−1/3)` für x < 0) über `Auswertung.Pow`, Lücke bei 0 als Grenzwert → ∫₋₁¹ = 0 | Core `T0781Runde2Tests.Nicht_definiert` (3 Fälle), `.Kubikwurzel` (2 Fälle, Hälften 3/4 und 3/2) – vorher: Testlauf hing (Abbruch nach 600 s) |
| HOCH N2 °C-Differenz | `Groessen.Ausrechnen`: °C − °C bleibt beim Umrechnen eine Differenz (10 °C, 18 °F) | `.Differenz_in_celsius` – vorher „−263,15 °C“ |
| MITTEL Hintergrund | `MhRechnen`: kurzer Versuch mit 0,1 s Kern-Zeitgrenze direkt, sonst `async` im Hintergrund (kein `task.Wait`); Stand je Dokument (`_mhGen`), veraltete Ergebnisse verworfen; geschlossenes Dokument wird nicht angefasst; neu Getipptes bleibt stehen; Ausnahmen im Rückruf werden zur Meldung; keine Zwangs-Navigation. Anzeige: höchstens 0,2 s direkt je Sekunde, Rest im Hintergrund. Aufgabenblatt (`MhImportAsync`) rechnet im Hintergrund (je Zeile 3 s, zusammen 30 s). KI-Fachaktion rechnet mit der 3-s-Grenze des Kerns und merkt das Ergebnis | App `T0781Wissen_Ui`: `Spaet_ueberschreibt_getipptes_nicht`, `Veraltetes_ergebnis_verworfen`, `Geschlossenes_dokument_unberuehrt`, `Ausnahme_im_hintergrund`, `Import_ohne_blockieren` (Testnaht `MhKernFuerTests` stellt langsames Rechnen nach) |
| MITTEL e-Schreibweise | Zehnerpotenz nur, wenn Ziffern/±Ziffern direkt folgen und kein Buchstabe ohne Operator danach; `2e + 3` = 2·e + 3; `2e+3` = 2000 mit Schritt „Hinweis zur Schreibweise“; `2e3x` → Meldung „mehrdeutig“ | `.E_schreibweise` |
| Umbenennung | `ProgramCmd.NoKi`, `ProgramKiAction.OnApplyNote` (string?, null = alter Standardtext) – Definitionen zeichengleich mit T-0780 | bestehende App-Tests (`Eingebunden`, Sperren) grün |
| NIEDRIG | `x^(1/3) = −2` → x = −8 (Probe ✓); deutsche Meldung „Die Hochzahl ist zu groß (höchstens 1000).“; `Rechenzeit.Pruefe()` auch in Vereinfachen (`Einmal`, Polynom-Multiplikation), Ableiten, Integrieren | `.Niedrig` |

Tests: Core gezielt (T0781*, MatheKern, NaturKern, WissenAdapter, PromptAction, T0777Stille, KreativKits, Buero) 322 Tests: 321 bestanden, 1 übersprungen (PubChem-Abgleich); App `T0781Wissen_Ui` 16 bestanden, 2 übersprungen (FREMD, ECHT); App-Querklassen (26 Klassen, u. a. AssistChain, ClaudePrograms, DeadButton, PromptBar, StudioFolds, StudiosAlle) 364: 362 bestanden, 2 übersprungen. Kein Volltest.
Ehrlich: Der Export (PDF/LaTeX/MD) rechnet noch synchron je Rechnung, aber mit der 3-s-Grenze des Kerns; meist liegen die Ergebnisse schon im Zwischenspeicher.

## 3. Prüfrunde (Nachbesserung 3)
Rot-Läufe gegen 0ab418a92 in `reports/T-0781/nachbesserung3/`.

| Punkt | Änderung | Test |
|---|---|---|
| HOCH langsame Konvergenz | `Grenzwert` in `BestimmtesIntegral`: für c·x^a an der Stelle 0 die exakte Regel (konvergent genau für a > −1, sonst divergent); sonst F an p ± 10⁻² … 10⁻¹², Verhältnis aufeinanderfolgender Differenzen: < 0,9 und gleichmäßig → konvergent mit Aitken-Extrapolation, ≥ 0,98 → divergent, dazwischen „(unsicher – siehe Probe)“ mit gelber Probe. Gegenprobe (`ProbeUneigentlich`) rechnet nur mit f: Zehnerstücke zur Lücke hin, ausgesparter Rest aus drei Summen hochgerechnet | Core `T0781Runde3Tests.Konvergent` (∫x^(−2/3) −1..1 = 6, 0..1 = 3, ∫x^(−3/4) = 4, ∫x^(−0.9) = 10, ∫5x^(−3/4) = 20), `.Extrapolation_ohne_potenzregel` (∫x^(−2/3) + x^(1/3) = 15/4) – vorher alle „existiert nicht“; `.Divergent` (1/x, 1/x², x^(−1), x^(−1.5)) als Schutz, schon vorher grün |
| MITTEL schnelle Folge | Verworfen wird nur eine ältere Fassung derselben Zeile (gleiches Dokument, gleiche Art, gleicher Text) – mit Meldung „Ältere Rechnung verworfen …“; eine andere ältere Rechnung wird angehängt | App `Veraltetes_ergebnis_verworfen` (jetzt: beide verschiedenen Rechnungen kommen an), `Gleiche_fassung_verworfen_mit_meldung` (Reihenfolge per zweitem Tor festgelegt) – beide vorher rot |
| NIEDRIG schmale Lücke am Rand | Abtastung zusätzlich dicht an den Rändern (10⁻³ … 10⁻⁹ der Länge); `Grenzwert` meldet „undefiniert“, wenn f neben der Stelle keinen reellen Wert hat → „nicht überall definiert“ statt „divergiert“ | `.Schmale_luecke_am_rand` (√x ab −0,0001, ln x ab −10⁻⁶, √(1−x) bis 1,000001) – vorher „divergiert“ |

Tests: Core gezielt 335 Tests: 334 bestanden, 1 übersprungen (PubChem-Abgleich); App `T0781Wissen_Ui` 19: 17 bestanden, 2 übersprungen (FREMD, ECHT); App-Querklassen (26 Klassen) 364: 362 bestanden, 2 übersprungen. Kein Volltest.

## ENTWICKLUNGSBERICHT
- Projekt: Trendo OS / Sprachen: C# (.NET 10, Avalonia) / Rust eingesetzt: NEIN / Rust-Komponenten: keine
- Grund der Sprachwahl: Rechenkern läuft im .NET-Prozess, Schulaufgaben in Millisekunden (200 Zufallsgleichungen + 81 weitere Core-Fälle in unter 1 s); Rust brächte FFI und zweite Toolchain ohne messbaren Vorteil
- Durchgeführte Tests: siehe oben (Core 279/280, App 375/380, Echtlauf, Fremdprogramm) / Gemessene Verbesserungen: – / Offene Probleme: siehe oben
