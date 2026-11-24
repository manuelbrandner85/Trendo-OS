# T-0760 – Bericht: DXF-Elemente lesen statt melden, lastrobuste Zeittests

**Zweig:** `claude/T-0760-dxf-elemente-tests` (Basis main 24bb4f70e, Release 0.9.108) · freigegeben vom Nutzer am 23.11.2026

## Geänderte Dateien
- `src/Trendo.Core/Cad/CadDrawing.cs` – Leser: Elemente einer Blockdefinition werden aufgehoben; Lesen eines Elements als lokale Funktion `Entity` (für ENTITIES und Blöcke gleich); `Insert` löst Verweise auf; `Place` bildet ab; SPLINE/MLINE-Fälle; `CadDxf` ist jetzt `partial`; Ausdehnung je Block nur einmal berechnet (`boxMemo`).
- `src/Trendo.Core/Cad/CadDxfElements.cs` (neu) – Abbildung `Xf`, `MapEntity`, `ReadSpline` (NURBS), `ReadMline`, `MlineStyles`.
- `src/Trendo.Core/Arch/PlanDxfImport.cs` – SPLINE wird im Grundriss wie die Ellipse nicht in Wandstücke zerlegt, sondern gemeldet.
- Tests: neu `tests/Trendo.Core.Tests/DxfElementeT0760Tests.cs` (11 Tests), neu `tests/Trendo.Core.Tests/Lastmessung.cs`; angepasst `DxfWohnungFraesenFix5Tests.cs`, `DxfWohnungFraesenFix6Tests.cs` (Verhalten hat sich gewollt geändert), `DxfWohnungFraesenFix2/11/16/20Tests.cs`, `TempDirTests.cs` (Zeitmessung).
- `.gitattributes` (`reports/T-0760/** -text`), `reports/T-0760/**`, `tasks/T-0760-*`.

## (1) DXF-Elemente
| Element | vorher | jetzt | Beleg |
|---|---|---|---|
| INSERT | „nicht gelesen“, Ausdehnung als unsicherer Bereich | aufgelöst: Einfügepunkt, Basispunkt, Faktoren 41/42 (auch negativ), Drehung, Normale −Z, Feld 70/71/44/45, verschachtelt (Tiefe ≤ 16), Zyklus erkannt, Arbeitsgrenze 8 Mio. Gruppen. Ebene 0 erbt die Ebene des Verweises; gefrorene Ebene / 60 = 1 → Inhalt unsichtbar (nicht gefräst, gemeldet); ausgeschaltete Ebene wie AutoCAD. Ähnlichkeit: Kreise/Bögen/Ausbuchtungen bleiben exakt; ungleiche Faktoren: Ellipse fein abgetastet (≤ 0,001 mm). Gemeldet bleiben: Block fehlt, Xref, schräge Normale, unsinnige Faktoren, zu groß, zu tief, Zyklus – mit vorsichtiger Ausdehnung | `T0760_DxfElemente.Insert_*`, FreeCAD-Vergleich |
| SPLINE | gemeldet | NURBS nach de Boor (Kontrollpunkte, Knoten, Gewichte), adaptiv je Knotenstück bis Sehnenfehler ≤ 0,001 mm in mm (Zoll/cm/m berücksichtigt). Geschlossen nur, wenn sich die Enden treffen. Gemeldet (nicht geraten): nur Fitpunkte bei Grad ≥ 2, Fitpunkte neben der Kurve, geschlossen ohne passende Enden, fallende Knoten, Gewicht ≤ 0, widersprüchliche Anzahlen | `Spline_nurbs_circle` (Punkte exakt auf r 40, Pfeilhöhe ≤ 0,001 mm in mm und Zoll), `Spline_not_guessed`, echte Pro/ENGINEER-Dateien, FreeCAD-Rundreise |
| MLINE | gemeldet | je Linie ein Zug: Scheitel + Gehrung × Parameter (wie AutoCAD), streng geprüft (Gruppenfolge, Richtung = Richtung zum nächsten Scheitel, normiert, Gehrung nicht parallel) und gegen die MLINESTYLE-Abstände × Maßstab; Enden (Linie, äußere/innere Bögen) und Gehrungslinien nach Stil-Flags. Gemeldet: Unterbrechungen, Abstände passen nicht zum Stil, schräge Bogenenden | `Mline_walls` (auch Wohnungsplaner: Wände), `Mline_not_guessed` |
| REGION | gemeldet | **bleibt gemeldet.** Inhalt ist ACIS: bis AutoCAD 2010 verschlüsselter SAT-Text mit eigener Topologie (Körper/Fläche/Schleife/Kante, Kurven teils als prozedurale B-Splines), ab 2013 binär (SAB) im Abschnitt ACDSDATA. Ohne Geometrie-Kern nicht sicher lesbar – nicht geraten | `Region_and_lens_stay_reported` |
| „ezdxf-Linse“ | ohne Form gemeldet | **bleibt gemeldet.** Gruppe 73 = 0 sagt in beiden Lesarten „im Uhrzeigersinn“, die Winkel liegen in beiden in [0°, 360°); dieselben Bytes sind eine gültige Linse (Komplementwinkel, AutoCAD/ezdxf) und eine gültige „Erdnuss“ (wörtlich) – beide Ränder schließen sich. Die Datei entscheidet es nicht. (ezdxf ist auf dem PC nicht installiert; Testdateien selbst erzeugt) | `Region_and_lens_stay_reported`, bestehend `Cw_literal_lens_vs_peanut` |

**Sicherheitsregeln nach dem Auflösen** (`Insert_safety_after_expansion`, `Insert_nested_layers_visibility`, Regression): nicht lesbares im Block (REGION) macht die Teile darin unsicher; Schraffur im (gedrehten) Block formgleich zum Rand fällt weg; Linien-Stöße aus Blöcken werden verkettet; ausgeblendete/unsichtbare Blockinhalte werden nie gefräst. In den echten Dateien greifen Schutzbereich und „liegt an offener Kontur“ jetzt auch an den gelesenen Splines (siehe unten).

### Regressionslauf (Pflicht)
1710 verschiedene DXF (SHA-256-eindeutig, aus 2655 Dateien unter `pruef*-T-0747-*`), main gegen Zweig, verglichen: Geometrie (Prüfsumme aller Elemente, Ebenen, Sichtbarkeit), Nicht-gelesen, Ausdehnungen, formgleiche/ausgeblendete Füllflächen, Einheiten.
- **gleich: 1664 · anders nur wo vorher „nicht gelesen“ stand: 46 (INSERT/„Element in Blockdefinition“ 38, SPLINE 6, MLINE 1) · unerklärt: 0** (`reports/T-0760/regression/lesen-je-datei.tsv`).
- Fräsen ist eine Funktion des Leseergebnisses (CncCam unverändert) – bei gleichem Lesen gleiche Fräsbahnen. Für die 46 geänderten Dateien Fräsen und Grundriss vorher/nachher (`fraesen-grundriss-geaenderte.tsv`):
  - 26 Fälle, in denen vorher alles ausgelassen wurde, werden jetzt gefräst; 15 bleiben ganz ausgelassen (gemeldet); 5 wurden schon vorher teilweise gefräst (z. B. `altcases_p6_i2_minsert_3x2_gedreht`: 6 Teile, `p5_b2_platte_im_block_loecher_los`: Platte + 2 Löcher).
  - weiter ausgelassen und gemeldet (richtig): Schraffur nur im Block ohne gezeichneten Rand (`c_block_insert_negativ*`, `f01–f04`), SOLID im Block ohne Rand (`a6`), Xref (`i15`), SPLINE nur Fitpunkte (`i16`), Gewicht 0 (`a8_spline_gewicht_null`), selbstkreuzender Zug aus dem Block (`a7`), MLINE des Prüfers mit widersprüchlichen Richtungen (`x3_umriss_mline`).
  - `p6_d1_viele_konturen_meldungen`: ein Loch weniger – die jetzt gelesene Kontur aus Block „P“ liegt 0 mm an Kontur 1071; Schutzbereich greift (beide ausgelassen, gemeldet).
- Echte Dateien (`echte-dateien/echte-dxf.tsv`): 11 Zeichnungen eines Stahlsockels, geschrieben von **Pro/ENGINEER** – 22 bis 110 SPLINE je Datei alle gelesen (Fitpunkte liegen jeweils auf der Kurve), bis zu 36 499 Blockelemente aufgelöst, nichts mehr „nicht gelesen“ außer SOLID/Bemaßung/Hinweislinie. In `extension-rod` (innen 5 → 1) und `upright-post` (6 → 4) werden Löcher, die vorher trotz „enthält nicht gelesenes Element SPLINE“ gefräst wurden, jetzt ausgelassen, weil sie an den gelesenen, offenen Spline-Linien der Ansicht liegen (Regel „liegt in oder an offener Kontur“) – vorsichtiger, gemeldet.

### Fremdbeleg (Pflicht) – FreeCAD 1.1.4 (`freecadcmd`, vorhanden, nichts installiert)
- **Import:** Test-DXF (AC1015) mit 5 Blockverweisen (30° gedreht, Faktor 1,5, gespiegelt −1, verschachtelt 0,5/−20° mit innerem 15°) und 2 SPLINE (rationaler NURBS-Kreis, kubisch 6 Punkte) – FreeCADs eigener Importer (`Import.readDXF`) gegen Trendo: **größter Abstand Trendo → FreeCAD 0,000079 mm**, FreeCAD → Trendo ≤ 0,00099 mm (Splines, Trendos Sehnenfehler) (`vergleich-basis0.txt`).
- Mit Basispunkt ≠ 0 weicht FreeCAD ab: es ignoriert den Basispunkt des Blocks (Rahmen um (10/10) verschoben, `lagen-basis10.txt`). Trendo folgt der DXF-Regel (Einfügepunkt ↔ Basispunkt); die bestehenden Prüfer-Erwartungen (`Insert_extent_transform`) verlangen das ebenso.
- **Rundreise:** FreeCAD erzeugt B-Spline (offen, 5 Punkte interpoliert), periodischen geschlossenen B-Spline und Kreis und schreibt mit `Import.writeDXFObject` (AC1014, SPLINE) → Trendo liest alles (nichts „nicht gelesen“, geschlossener Spline geschlossen) → **größter Abstand 0,000003 mm / 0,000994 mm** (`vergleich-rundreise.txt`, Export reproduzierbar).
- MLINE: kein Fremdprogramm mit MLINE auf dem PC geprüft → **BLOCKIERT** (Voraussetzung: AutoCAD/BricsCAD/ODA o. ä.); belegt nur mit eigenen Dateien nach DXF-Referenz.

## (2) Lastrobuste Zeittests
**Gewählt: Grenze relativ zu einer Vergleichsmessung im selben Lauf** (`tests/Trendo.Core.Tests/Lastmessung.cs`). Unmittelbar vor und nach der gemessenen Arbeit läuft eine feste Ganzzahl-Rechnung – einfädig bzw. über `Parallel.For` wie der geprüfte Code; Faktor = kleinere der beiden / ungestörter Bezugswert dieses PCs (55 ms / 86 ms), mindestens 1. Geprüft wird `Dauer < Grenze × Faktor`; Messwert, Faktor und Grenze stehen im Protokoll.
- Warum nicht nicht-parallele Sammlung: Fix16/Fix20 lagen schon in der nicht parallelen Sammlung und fielen trotzdem – die Last kommt aus anderen Prozessen (andere Testprojekte, parallele Sitzungen).
- Warum nicht CPU-Zeit: übersähe Verlangsamung durch Warten (Sperren, Schlafen) und summiert bei paralleler Arbeit alle Kerne.
- Speicher (`Circles_and_huge_bulges`, 1175 MB im Volltest): `GC.GetTotalAllocatedBytes` zählte den ganzen Prozess; jetzt `GetAllocatedBytesForCurrentThread` (CadDxf.Parse rechnet einfädig): 218 MB, Grenze 1000 MB unverändert.
- `TempDirTests.Delete_retries_locked_file`: Freigabe der Sperre auf eigenem Faden statt `Task.Run`/`Task.Delay` im (im Volltest belegten) Thread-Pool; Versuche 30 × 100 ms unverändert.

Belege (`reports/T-0760/zeittests/`):
- ungestört: Faktor 1,00 bei den CncCam-Messungen → Grenzen wirken unverändert (z. B. „oben“ 9149 ms < 12 000, „2000 X-Stöße“ 5786 ms < 10 000) – alle 6 grün.
- künstliche Last (16 Rechenprozesse): **alter Stand rot** (Fix20 „oben“ 15 819 ms > 12 000), **neuer Stand grün** (Faktoren 1,9–6,5; z. B. „oben“ 14 905 ms < 12 000 × 2,82).
- **künstlich verlangsamter Code** (vorübergehend, nicht im Commit): 15 s Rechnen am Anfang von `CncCam.Generate`/`CadDxf.Parse` → 4/4 rot (Faktor 1,00); 15 s Schlafen → 4/4 rot; 100 s Rechnen → Fix5 rot (Abbrechen 100 393 ms > 10 000).
- Grenze der Methode (ehrlich): unter starker Fremdlast überschätzt die Vergleichsarbeit die Verlangsamung teils (Faktor bis 6,5 bei gemessen ~2,7-facher Verlangsamung) – dann fiele ein Rückschritt bis etwa Faktor 2 erst ohne Last auf. Ungestört ist die Prüfung so streng wie vorher.

## Tests (Exitcodes, Zahlen aus der Ausgabe)
- Build `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core gezielt (T0747 alle, T0760, TempDirTests, CadTests, CadMeasureTests, T0256, T0257, T0266, T0748): **221/221 bestanden**, Exit 0 – gelaufen, während parallel der Regressionslauf den Rechner belastete.
- App gezielt (T0747_DxfUi, T0747_FixUi, T0747_Fix3RaceUi, T0747_Fix4RaceUi, GarmentEdit_Ui, Garment_Ui, Studio3DMeshEditUiTests): **30/30**, Exit 0.
- Rot-Grün: die 11 neuen Tests gegen main: **10 rot**, 1 grün (REGION/Linse – Verhalten bleibt absichtlich gleich) (`tests/rotgruen-main.log`).
- Kein Volltest (macht die Hauptsitzung).

## Abnahmekriterien
1. Leere Vorlage → Ergebnis über die Oberfläche: **NICHT GETESTET** (Leser-Paket, keine Oberflächenänderung).
2. Fremddatei importieren/exportieren, Fremdprogramm öffnet: **TEILWEISE** – Import aus FreeCAD-Export und echten Pro/ENGINEER-Dateien belegt; Trendos DXF-Export unverändert (R12, keine Blöcke/Splines), MLINE-Fremdbeleg BLOCKIERT.
3. Prozessabbruch ≤ 30 s: **NICHT GETESTET** (nicht berührt).
4. Claude/KI-Fachaufgabe: **FEHLT** (nicht Teil des Auftrags).
5. Test vom Klick bis zur Datei: **TEILWEISE** – bestehende UI-Tests (T0747_DxfUi …) grün; `CncJob.ImportDrawing` mit aufgelösten Blöcken bis zu den Fräsbahnen getestet (`Insert_nested_layers_visibility`), kein neuer Klick-Test.
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: **NICHT GETESTET** (keine Oberflächenänderung).

## Nicht geprüft / offen
- MLINE in einem Fremdprogramm (keins vorhanden); echte MLINE-Dateien fehlen auf dem PC.
- Echte AutoCAD-Dateien mit periodischen Splines und Feldern (MINSERT) – nur eigene, FreeCAD- und Pro/ENGINEER-Dateien.
- FreeCAD ignoriert beim Import den Blockbasispunkt (Fehler in FreeCAD 1.1.4, nicht in Trendo).
- `2000 X-Stöße` (Fix16) liegt einzeln bei 5,8–10 s nahe der 10-s-Grenze – Laufzeit von CncCam ist Sache von T-0758.
- Bezugswert der Vergleichsarbeit ist an diesem PC gemessen (bei ~35 % Grundlast); auf langsameren Rechnern wächst der Faktor entsprechend.
- Schnittstellen zu T-0758/T-0759: `CncCam.cs` und `CadContours.cs` nicht geändert.

**Status:** umgesetzt, gezielt getestet; Volltest durch die Hauptsitzung offen.
