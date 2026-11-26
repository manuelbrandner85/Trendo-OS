# Bericht T-0781: Mathe-Helfer und Naturwissenschaft (Wissen & Lernen)

**Auftrag:** `tasks/T-0781-mathe-naturwiss.md` (Freigabe 26.11.2026, Etappe 0.9.113) · **Branch:** `claude/T-0781-mathe-naturwiss` (Basis main 0.9.112 = 184fcbbfc) · eigene Kopie `%TEMP%\trendo-auftrag-T-0781`.

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

## ENTWICKLUNGSBERICHT
- Projekt: Trendo OS / Sprachen: C# (.NET 10, Avalonia) / Rust eingesetzt: NEIN / Rust-Komponenten: keine
- Grund der Sprachwahl: Rechenkern läuft im .NET-Prozess, Schulaufgaben in Millisekunden (200 Zufallsgleichungen + 81 weitere Core-Fälle in unter 1 s); Rust brächte FFI und zweite Toolchain ohne messbaren Vorteil
- Durchgeführte Tests: siehe oben (Core 279/280, App 375/380, Echtlauf, Fremdprogramm) / Gemessene Verbesserungen: – / Offene Probleme: siehe oben
