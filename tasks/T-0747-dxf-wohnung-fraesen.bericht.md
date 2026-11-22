# Bericht T-0747: DXF-Import – Wohnung planen und Fräsen

**Auftrag:** Restliste d7 Rang 22 (SPEC-01 Wohnung planen: DXF/IFC-Import fehlt) und Rang 25 (SPEC-14 Fräsen: kein CAM aus DXF/SVG).
**Branch/Arbeitskopie:** `claude/T-0747-dxf-wohnung-fraesen`, eigene Kopie von `main` (0.9.105) in `%TEMP%\trendo-auftrag-T-0747`.

## Gemeinsamer Leser (genau einer)
Vorhanden war `CadDxf` in `src/Trendo.Core/Cad/CadDrawing.cs` (T-0691, Trendo CAD) – LINE, LWPOLYLINE, POLYLINE/VERTEX, ARC, CIRCLE, TEXT/MTEXT, Ebenen.
Er wurde erweitert, kein zweiter Leser:
- `$INSUNITS` wird gelesen, alle Maße werden in mm umgerechnet (`ReadResult.Units`, `MmPerUnit`, `UnitName`; ohne Angabe = mm wie bisher). Damit liest auch Trendo CAD Zoll-/cm-/m-Dateien jetzt im richtigen Maßstab.
- Text der Datei: UTF-8, sonst wie bisher Latin-1; `\U+00B2`, `%%c/%%d/%%p` in Texten.
- Neu daneben: `CadContours` (lose Linien/Bögen zu geschlossenen Zügen verketten) – von Grundriss (Räume) und Fräsen (Konturen) gemeinsam benutzt; `CadSvg` (SVG → dieselbe `CadDrawing`, mm über width/height + viewBox, transform, rect mit Rundung, circle/ellipse, path über `ImageVector.ParseSubpaths`).
- `ImageVector.ParsePath` nutzt jetzt `ParseSubpaths` (Teilpfade getrennt, Bogen A echt abgetastet, S/T gespiegelt, Q quadratisch) – bestehender Test `ImageVectorTests` grün.

## 1. SPEC-01 Wohnung planen – DXF in den Plan
**Vorher:** Plan konnte DXF nur schreiben (`PlanDrawing.Dxf`), kein Import (audit-3).
**Nachher:** `PlanDxfImport.Import` (Kern) + Menüband „Einlesen → DXF einlesen …“ (Stufe Erweitert/Profi), Ziehen und Ablegen einer .dxf (oder eines Vorlage-Bilds) auf die Zeichenfläche. Ein Rückgängig-Schritt.
- Doppellinien (5–60 cm) → Wand mit dieser Stärke, Achse in der Mitte; Einzellinien → Wand mit der links eingestellten Wandstärke; ARC/Kreis/Ausbuchtung → Bogenwände (vorhandenes `AddArcWall`).
- Querstriche an Wandenden fallen weg, Ecken werden auf den Achsschnittpunkt gezogen (nächster Schnitt).
- Lücke zwischen zwei Wandstücken auf einer Linie (40–400 cm) → Tür, wenn ein Schwenkbogen passt (inkl. Anschlagseite/Richtung), sonst Fenster.
- Geschlossene Umrisse auf Raum-Ebenen → Räume, Name aus dem Text darin, Raumart aus dem Namen; übrige Texte → Beschriftungen; Flächenangaben „m²“ rechnet Trendo selbst.
- Ebenen: Wand-Ebenen automatisch (Wand/Wall/Mauer …), Möbel/Maße/Öffnungen werden nicht zu Wänden; die Meldung nennt, was nicht übernommen wurde (auch DIMENSION, INSERT …).
- Maßstab aus `$INSUNITS`, y gespiegelt, leerer Plan beginnt bei 0/0, sonst 1 m rechts neben dem vorhandenen Plan.
**Beleg:** `T0747_DxfWohnungFraesen.Plan_dxf_roundtrip` (Trendo-Plan → DXF → Import: gleiche 5 Wände mit Stärken, Tür 90 + Fenster 120 an gleicher Stelle, Raum „Küche“; zweite Runde identisch), `Foreign_dxf_into_plan` (selbst erzeugte fremde DXF in mm), UI-Test `T0747_DxfUi.Plan_import_edit_export` (Klick → Import → Rückgängig/Wiederholen → Wandstärke in den Eigenschaften ändern → Export DXF → wieder gelesen). Fremdleser-Prüfung: `reports/T-0747/pruefe_t0747.py` liest `wohnung-bearbeitet.dxf` selbst (Aufbau, $INSUNITS 5, 8×6 m, Innenwand 17,5 cm, Türbogen, Raumnamen, m²) – BESTANDEN.
**IFC-Import:** OFFEN. Vorhanden sind IFC-Export und IFC-Info über IfcOpenShell (nur nach Einrichten, auf diesem PC nicht eingerichtet) und eine IFC-3D-Ansicht; ein Weg IFC → Wände gibt es in den vorhandenen Bausteinen nicht. Nicht gebaut.

## 2. SPEC-14 Fräsen – CAM aus DXF/SVG
**Vorher:** CNC-Werkstatt mit G-Code-Editor, Simulation und Generatoren für Rechteck/Tasche/Lochkreis; keine Konturen aus Zeichnungen (audit-3).
**Nachher:** `CncCam` (Kern) + Menüband „Erzeugen → Aus DXF/SVG …“ und „Bahnen neu“, Rechtsklick-Menü in der Vorschau, Ziehen und Ablegen (.dxf/.svg → Bahnen, .nc/.gcode → laden), Eigenschaften „Fräsart“ und „Haltestege (Anzahl/Breite/Höhe)“ – jede Änderung ein Rückgängig-Schritt, Bahnen werden sofort neu berechnet.
- Außenkontur: Versatz um den Fräserradius nach außen, Außenecken rund (Sehnen tangential – nie näher als der Radius), Gleichlauf im Uhrzeigersinn; Innenkontur nach innen, gegen den Uhrzeigersinn; offene Linien auf der Linie (Gravur, hin und zurück je Stufe).
- Automatik: äußerster Umriss außen, Löcher innen; Löcher zuerst, Umriss zuletzt. Schleifen an engen Stellen werden entfernt; käme die Bahn der Kontur näher als der Radius, wird die Kontur ausgelassen und gemeldet (kein Anschneiden). Zu kleine Löcher werden gemeldet.
  **Berichtigung (Gegenprüfung):** „kein Anschneiden“ stimmte so nicht. Geprüft wurde nur gegen die *eigene* Kontur und nur Punkt gegen Strecke: eine Bahn konnte durch ein Nachbarteil (3 mm Abstand, Fräser Ø 6) oder durch eine Insel im Loch fahren, eine Achterform wurde gefräst, und an konvexen Bögen kam die Bahn dem echten Bogen um bis zu 0,01 mm (Sehnentoleranz) näher als der Radius. Das Prüfskript hat das mit Toleranz 0,02 und abgetasteten Punkten nicht bemerkt. Behoben unter „Gegenprüfung behoben“.
- Zustellung in Stufen bis zur Tiefe, Haltestege gleichmäßig auf Außenkonturen im letzten Bereich, M3 mit Drehzahl aus Werkstoff/Fräser, Teil auf den Nullpunkt geschoben.
- G-Code bleibt im vorhandenen Dokument (Editor, Simulation, Prüfbericht, Export „G-Code (.nc)“); die Vorschau zeigt die Konturen dünn unter den Bahnen.
**Beleg:** `Cam_from_dxf` (Zoll-DXF: Reihenfolge, Abstand Bahn–Kontur = 2,00 mm, Drehsinn, Stufen −2/−4/−6 aus dem vorhandenen `CncSim`, 3 Stege nur außen), `Offset_exact`, `Cam_modes`, `Svg_contours`, `CncJob_keeps_contours`, UI-Test `T0747_DxfUi.Cnc_import_tabs_export` (Klick → SVG → Bahnen → Stege eintippen → Rückgängig → Fräsart → Export .nc → kaputte Datei gibt Meldung). Prüfskript liest beide .nc gegen die Original-Zeichnung (eigener DXF-/SVG-Leser): Bahnen im Rohteil, kein G0 ins Material, kleinster Abstand Fräsermitte–Kontur 1,991 / 1,999 mm bei Radius 2 (Toleranz 0,02 – Abtastung des Prüfers), Versatz-Treffer je Kontur – BESTANDEN.
**Keine Maschine angesteuert.**

## Fremdwerkzeuge
Vorher geprüft, nichts installiert: `ezdxf` nicht installiert, LibreCAD/QCAD nicht vorhanden, Blender 5.2 ohne DXF-Erweiterung, kein G-Code-Viewer. Deshalb **eigenes Python-Prüfskript** (`reports/T-0747/pruefe_t0747.py`, nur Standardbibliothek, kein Trendo-Code). Das ist kein Nachweis „öffnet sich in LibreCAD/QCAD“ – NICHT GETESTET.

## Geänderte Dateien
- Kern: `src/Trendo.Core/Cad/CadDrawing.cs`, `Cad/CadContours.cs` (neu), `Cad/CadSvg.cs` (neu), `Arch/PlanDxfImport.cs` (neu), `W6/CncCam.cs` (neu), `W6/CncSim.cs` (CncJob: Konturen, CAM-Werte), `Media/ImageVector.cs`
- App: `src/Trendo.App/MainWindow.DxfImport.cs` (neu), `MainWindow.Spec01.cs`, `MainWindow.Spec14.cs`, `CncPathView.cs`
- Tests: `tests/Trendo.Core.Tests/DxfWohnungFraesenTests.cs` (neu, 12), `tests/Trendo.App.Tests/DxfWohnungFraesenUiTests.cs` (neu, 2), `tests/fixtures/t0747/` (selbst erzeugt: `make_fixtures.py`, `wohnung-fremd.dxf`, `teil-fremd.dxf`, `teil-fremd.svg`)
- Belege: `reports/T-0747/` (Prüfskript, Belegdateien, Rohlogs, `SHA256SUMS.txt`), `.gitattributes` (`reports/T-0747/** -text`), `tasks/T-0747-*.md`

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler.
- Kern gezielt (T0747, T0266_CncSim, T0273_Arc, T0257_FloorPlanPlus, T0256_FloorPlan, IfcExchangeTests, ImageVectorTests, MeshEditTests, T0280, RealismToolsTests, SPEC14_*, SPEC01_*, PromptAction*): **175 gesamt, 174 bestanden, 1 übersprungen (vorbestehend), 0 Fehler** – Rohlog `reports/T-0747/T-0747-kerntests.log`.
- App gezielt: T0747_DxfUi 2/2, SPEC14_Ui 2/2, SPEC01_Ui 6/6, SPEC01_UiArc 1/1, Bildbearbeitung_Ui 5/5, Studio3DMeshEditUiTests 5/5, PlanPhoto_Ui 4/4, UebersichtEinfach_Ui 2/2, DeadButtonTests 83/83 – Rohlog `reports/T-0747/T-0747-apptests.log`.
  - Erster Lauf: UebersichtEinfach_Ui meldete das neue Pfadfeld „cnc-drawing-path“ als in „Einfach“ fehlend → Feld entfernt (Einlesen läuft über Menüband „Mehr Werkzeuge“/Strg+K, Rechtsklick und Ziehen); danach grün. Test nicht geändert.
- Prüfskript: `python -I reports/T-0747/pruefe_t0747.py reports/T-0747 tests/fixtures/t0747` → BESTANDEN, Exit 0 – `reports/T-0747/T-0747-pruefskript.log`.
- Rot-Grün: die neuen Tests lassen sich gegen `main` nicht bauen (APIs fehlen); fachlich schlägt z. B. `Dxf_units_to_mm` auf dem alten Leser fehl (Zoll wurde als mm gelesen).
- Oberfläche im Bild geprüft (`tools/Trendo.Render`, Stufe Profi, beide Programme leer): neue Knöpfe „DXF einlesen …“, „Aus DXF/SVG …“, „Bahnen neu“ im Menüband, Dunkel-Gold, eine goldene Hauptaktion je Ansicht unverändert.
- Kein Volltest (macht die Hauptsitzung).

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis über die Oberfläche: **VOLLWERTIG** für beide Wege (leerer Grundriss → DXF → Wände/Türen/Räume; leeres CNC-Programm → DXF/SVG → G-Code), UI-Tests vom Klick bis zur Datei.
2. Fremddatei importieren, bearbeiten, exportieren, Export im Fremdprogramm: **TEILWEISE** – Import/Bearbeiten/Export echt; „Fremdprogramm“ nur als eigenes Python-Prüfskript, weil kein Fremd-CAD vorhanden ist (LibreCAD/QCAD/ezdxf: BLOCKIERT ohne Installation). Fremddateien selbst erzeugt.
3. Prozessabbruch ≤ 30 s: **TEILWEISE (unverändert)** – Import läuft als normale Dokument-Änderung durch die vorhandene Programm-Hülle mit Wiederherstellung; in diesem Paket nicht eigens mit Abbruch geprüft.
4. KI führt Fachaufgabe aus: **NICHT GETESTET / nicht Teil des Pakets**.
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** (`T0747_DxfUi`, 2 Tests).
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen und Ablegen: **TEILWEISE** – Ziehen und Ablegen in beiden Programmen, Rechtsklick-Menü in der CNC-Vorschau, Strg+Z für jeden Import; Rechtsklick im Grundriss und eigene Tastenkürzel für „Einlesen“ fehlen; Ziehen und Ablegen selbst nicht automatisch getestet (Headless-Drag nicht nachgebildet).

## Was NICHT geprüft wurde
- Öffnen der Exporte in LibreCAD, QCAD, AutoCAD oder einem G-Code-Viewer (nicht vorhanden, nichts installiert).
- Echte fremde Architekten-DXF (Blöcke/INSERT, Schraffuren, SPLINE, ELLIPSE werden gezählt, nicht übernommen), binäre DXF.
- Ziehen und Ablegen per Maus (nur Code-Weg), Prozessabbruch während des Imports.
- Echte Fräsmaschine (verboten), Werkzeugwechsel, Taschen-/Ausräumstrategien aus Konturen.

## Offene Punkte
- IFC → Plan (braucht IfcOpenShell-Leseweg, nicht vorhanden).
- DXF-Blöcke (INSERT) und SPLINE/ELLIPSE in Leser und Import.
- CAM: Taschen aus geschlossenen Konturen, Rampen-/Helixeintauchen, G2/G3-Ausgabe statt kurzer G1-Stücke.
- Grundriss: Rechtsklick-Menü und Tastenkürzel für „DXF einlesen“, Auswahl der Wand-Ebenen in der Oberfläche (Kern kann es: `PlanDxfOptions.WallLayers`).

**Status:** umgesetzt und gezielt getestet; Merge/Release nicht Teil des Pakets.

## Gegenprüfung behoben (T-0747-fix)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix`, Zweig `claude/T-0747-dxf-wohnung-fraesen` (Commits obendrauf). Keine Maschine angesteuert.
Rot-Probe: neue Kerntests gegen den alten Stand (nur fehlende Namen als Platzhalter ergänzt, ohne Verhalten) – **11 von 12 rot** (`reports/T-0747/T-0747-fix-rotprobe-kern.log`; grün war nur Rauschen 0,05 mm/500 Punkte).

**1. Nachbarkonturen (MITTEL) – behoben.** `CncCam.Generate` prüft jede Bahn Strecke für Strecke gegen *alle* geschlossenen Konturen (Rasterindex `SegGrid`), Mindestabstand Radius − 0,001 mm. Verstöße werden weggelassen: „Kontur 1 ausgelassen: zu nah an Kontur 2 – die Fräsbahn käme ihr auf 0 mm nahe …“. Selbst schneidende Konturen (Acht, auch Kreuzung im Eckpunkt) werden abgelehnt: „… kreuzt sich selbst …“. Bahnen auf der Linie (Gravur) werden gegen alle *anderen* geschlossenen Konturen geprüft.
Tests: `T0747_FixGegenpruefung.Neighbours_too_close`, `Island_near_hole_edge`, `Figure_eight_rejected`, `Random_shapes_never_gouge` (40 Zufallsformen, Fräser 1–10 mm, außen und innen). Der Abstand wird im Test **unabhängig** über den G-Code gemessen (eigener G-Code-Leser, eigener Strecke-zu-Strecke-Abstand, kein Trendo-Code). Rot auf altem Stand: alle drei Fälle.
Messwerte: zwei Teile 3 mm / Insel 2 mm / Acht → keine Bahn an den betroffenen Konturen, kleinster Abstand der übrigen Schnitte 3,0000 mm (Radius 3); Zufallsformen: schlechtester Wert Radius − 0,00007 mm (Rundung im G-Code).
Beleg: `reports/T-0747/fix-faelle.dxf` (alle Fälle als LINE-Ketten) → `fix-faelle.nc` + `fix-faelle-hinweise.txt`; Prüfskript misst exakt Strecke zu Strecke gegen alle Konturen: **2,9999 mm bei Radius 3**, gefräst nur Platte, Hantel-Platte, Hantel-Kammern, Schlitzteil, Rauschkreis.

**2. Engstellen (MITTEL) – behoben.** Der Versatz wählt nicht mehr „die größere Schleife“. Neu (`OffsetLoops`): rohe Versatzlinie (Strecken verschoben, Außenecken als Bogen, Innenecken als Verbinder über den Eckpunkt wie bei Clipper), an allen Selbstschnitten geteilt; ein Stück bleibt nur, wenn es genau im Abstand zu seiner eigenen Strecke/Ecke liegt, keiner anderen Stelle näher kommt und auf der richtigen Seite liegt; die Stücke werden wieder zu Schleifen verbunden. Die Hantel ergibt so **zwei** Kammern. Danach wird jede Kontur abgetastet: Stellen, die weiter als Radius + ½ Radius (mind. 0,2 mm) von jeder Bahn liegen, werden mit Lage gemeldet: „Kontur 2: Bereich enger als der Fräser Ø 6 mm – dort bleibt Material stehen (bei X 60 Y 28,5; bei X 60 Y 31,5) …“. Normale 90°-Innenecken melden nichts. Leere Ergebnisse nennen immer den Grund („Innenkontur ist zu klein …“, „der Versatz nach außen ergab keine Bahn – Kontur ohne Fläche/entartet“) statt still zu überspringen.
Tests: `Dumbbell_hole` (2 Innenbahnen + Meldung mit Lage im Steg), `Slot_outside_and_empty_reason` (Umriss gefräst, Meldung „bei X 30 …“, Loch 4 mm mit Grund, Rechteck mit Loch ohne Meldung). Rot auf altem Stand: beide.

**3. Verrauschte Konturen (MITTEL) – behoben.** Ursache war die Schleifenwahl (und eine Abbruchgrenze von 2000 Schleifen), nicht fehlende Glättung. Mit dem Beschneiden aus 2 werden verrauschte Kreise gefräst. Zusätzlich Douglas-Peucker vor dem Versatz (`SimplifyTol` 0,001 mm, deutlich unter Radius/10); die tatsächliche Abweichung wird dem Versatz zugeschlagen, der Abstand zur *Original*kontur bleibt ≥ Radius. Bewusst kleiner als die vorgeschlagenen 0,01–0,02 mm: das Glätten wird für die Sicherheit nicht gebraucht, und jede Glätt-Abweichung vergrößert das Teil um genau diesen Betrag.
Tests: `Noisy_circle` (Rauschen 0,05 und 0,5 mm × 500 und 2000 Punkte, je außen und innen) – Bahn vorhanden, unabhängiger Abstand ≥ Radius − 0,0011, mittlerer Abstand ≤ Radius + 2·Rauschen + 0,2. Rot auf altem Stand: 3 von 4 („enger als der Fräser“).
Messwerte außen (kleinster Abstand, unabhängig über den G-Code / Rechenzeit): 0,05/500 3,0000 mm 26 ms · 0,5/500 3,0003 mm 50 ms · 0,05/2000 3,0000 mm 386 ms · 0,5/2000 3,0009 mm 935 ms; innen kleinster Wert 2,9999 mm (Rundung im G-Code) – Radius 3.
Dabei gefunden und mit behoben: Bögen und Kreise wurden mit Sehnen *auf* dem Kreis (0,01 mm Toleranz) angenähert – an konvexen Bögen kam die Bahn dem echten Bogen bis 0,01 mm zu nah (Prüfskript jetzt exakt: vorher 1,9903 mm bei Radius 2). Fürs Fräsen werden Bögen jetzt mit 0,001 mm zerlegt (`CncCam.ContourChord`), SVG-Bögen/-Rundungen feiner (0,625° statt 2,5°, Kurven 96 statt 24 Stücke). Neu gemessen: 1,9990 mm (DXF in Zoll), 1,9998 mm (SVG) bei Radius 2.
  **Berichtigung (zweite Prüfung):** „0,001 mm“ galt nur bis R ≈ 420 mm (Obergrenze 1440 Stücke je Kreis; gemessen R 5000 → −0,0100, R 20000 → −0,0277), SVG-Bézier mit 96 Stücken kam auf −0,0205 mm. Behoben unter „Zweite Prüfung behoben“.

**4. Große DXF (NIEDRIG) – behoben.** Kettenbildung (`CadContours.Chain`) mit Rasterindex der Enden; Wandpaare, Querstriche, Zusammenführen auf einer Linie und Ecken in `PlanDxfImport` mit Rasterindex – Ergebnis unverändert (gleiche Reihenfolge wie das frühere „immer wieder von vorn suchen“; Test prüft 168 Wände/4832 Öffnungen wie vorher). Obergrenze 50 000 Elemente mit Meldung („… zu viele …“) für Grundriss und Fräsen. Abbrechen (`CancellationToken`) im Leser, Import und CAM. In der Oberfläche werden Dateien ab 512 KB im Hintergrund gelesen; Karte „… wird eingelesen …“ mit „Abbrechen“, Ergebnis als **ein** Rückgängig-Schritt, Abbrechen ändert nichts.
Messwerte (10 000 Linien, Kern): Grundriss-Import **24 678 ms → 1 798–1 952 ms**, Konturen 440 ms → 38–79 ms (je nach Lauf). Oberfläche (12 000 Linien, Headless-Test): Klick bis Oberfläche wieder frei **344 ms** (vorher lief alles im UI-Thread), fertig nach 4,2 s.
Tests: `Big_dxf_fast` (Zeit, Obergrenze, Abbrechen), UI `T0747_FixUi.Big_dxf_background_and_cancel` (Grundriss im Hintergrund, Karte, Rückgängig, Abbrechen; CNC-Abbrechen). Rot auf altem Stand: Zeitgrenze (24,7 s).

**5. Bahnen bei geänderten Werten (NIEDRIG) – behoben.** `CncJob` merkt sich zu jedem G-Code aus der Zeichnung einen Stempel (Werte + Prüfsumme). Fräser-Ø, Tiefe, Zustellung, Vorschub, Eintauchen geändert → Bahnen werden sofort neu gerechnet (zusammen ein Rückgängig-Schritt). Geht das nicht (z. B. Fräser größer als alles), wird nur der Wert übernommen, der G-Code gilt als veraltet: Hinweis in den Eigenschaften, Export „G-Code (.nc)“ gesperrt („erst „Bahnen neu“ drücken“). Von Hand geänderter oder geladener G-Code gilt nicht als CAM-Code und wird nie umgerechnet.
Tests: `Stale_cam` (Kern, rot auf altem Stand), UI `T0747_FixUi.Cnc_values_recompute_or_lock_export`.

**6. Kleinigkeiten (NIEDRIG) – behoben.** DXF-Rückfall Windows-1252 statt Latin-1 (über den vorhandenen `TextFileCodec`; „€“, „–“ kommen richtig an). DXF-Sonderzeichen (%%c, \U+…) nur noch in fremden Dateien: Trendos eigene DXF tragen am Anfang den Kommentar `999 / Trendo OS` (CAD und Grundriss) und bleiben beim Wiedereinlesen wörtlich. Kaputte SVG-Pfade („M0 0 L10“) → `CadError` „Pfad „teil“ in der SVG-Datei ist kaputt: Der SVG-Pfad ist unvollständig: nach „L“ fehlt eine Zahl am Ende.“ statt `ArgumentOutOfRangeException`.
Test: `Encoding_escapes_broken_svg` (rot auf altem Stand).

**Belege geändert (nötig, weil sich die Ausgaben geändert haben):** `teil-fremd-dxf.nc`, `teil-fremd-svg.nc` (feinere Bögen), `wohnung-bearbeitet.dxf` (Kennung `999 Trendo OS`), `pruefe_t0747.py` (exakte Strecke-zu-Strecke-Prüfung, Fall 3); neu `fix-faelle.*` und die Rohlogs `T-0747-fix-*.log`; `SHA256SUMS.txt` neu gerechnet. Die alten Logs `T-0747-*.log` bleiben als Stand vor der Gegenprüfung.

**Tests (gezielt, kein Volltest):** `dotnet build Trendo.sln -c Release` 0 Fehler. Kern (T0747_DxfWohnungFraesen, T0747_FixGegenpruefung, T0266_CncSim, T0273_Arc, T0256_FloorPlan, T0257_FloorPlanPlus, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests): **84 gesamt, 83 bestanden, 1 übersprungen (vorbestehend: „IFC echt“ ohne TRENDO_IFC_PYTHON), 0 Fehler** – `reports/T-0747/T-0747-fix-kerntests.log`. App: T0747_DxfUi 2/2, T0747_FixUi 2/2, SPEC01_Ui 6/6, SPEC01_UiArc 1/1, SPEC14_Ui 2/2, Bildbearbeitung_Ui 5/5, PlanPhoto_Ui 4/4, N23N24_CadPrintUi 3/3, UebersichtEinfach_Ui 2/2, DeadButtonTests 83/83 – **110/110** – `reports/T-0747/T-0747-fix-apptests.log`. Prüfskript BESTANDEN – `T-0747-fix-pruefskript.log`. Sichtprüfung `tools/Trendo.Render` (CNC-Werkstatt, Profi, leer): Aussehen unverändert, Dunkel-Gold, eine goldene Hauptaktion. UI-Rot-Probe nicht möglich (die UI-Tests brauchen die neuen Schnittstellen); die Kern-Rot-Probe deckt 1–6 ab.

**Was offen bleibt / nicht geprüft:**
- Anzeige sehr großer G-Codes ist langsam (vorbestehend, auch beim Laden einer .nc): im Headless-Test 5 000 Zeilen 7,3 s, 20 000 Zeilen 53 s. Eine CNC-Zeichnung mit 12 000 Linien ergibt 72 000 Zeilen G-Code – das Einlesen selbst läuft im Hintergrund (1,1 s), die Anzeige danach friert ein. Braucht einen virtualisierten Editor (eigenes Paket).
- Offene Konturen wurden ohne Hinweis auf der Linie bis zur vollen Tiefe gefräst; nicht lesbare Elemente fehlten still; Konturen in einer abgelehnten Acht wurden auf der falschen Seite gefräst – von der zweiten Prüfung gefunden, erledigt in „Zweite Prüfung behoben“.
- Taschen mit Inseln werden nicht gemeinsam ausgeräumt: Loch und zu nahe Insel werden beide ausgelassen und gemeldet (sicher, aber kein Ergebnis).
- ~~SVG-Bézierkurven … DXF-Bögen/Kreise sind auf 0,001 mm begrenzt.~~ Diese Aussage war zu stark (siehe Berichtigung oben); erledigt in „Zweite Prüfung behoben“.
- Ziehen und Ablegen per Maus und echte Fremd-CAD-Programme weiterhin nicht geprüft (keine installiert).

## Zweite Prüfung behoben (T-0747-fix2)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix2`, Commit obendrauf auf `claude/T-0747-dxf-wohnung-fraesen`. Keine Maschine angesteuert.
**Prüfprogramm des Prüfers** (Python/numpy, wahre Geometrie, Strecke zu Strecke, liest den G-Code; nur als Kopie benutzt, nicht im Repo): alle Fälle über `CncJob.ImportDrawing` neu erzeugt und geprüft.

| Fallsatz | Fälle | mit G-Code | Befunde vorher | Befunde jetzt | schlechtester Abstand − Radius |
|---|---|---|---|---|---|
| Hauptsatz | 143 | 123 | 30 | **0** | −0,0010 mm |
| x1–x3 (Achter) | 3 | 2 | 3 (Fräsermitte im Material) | **0** | 0,0000 mm |
| große Radien R 300 … 20 000 | 16 | 16 | 3 (schlechtester −0,0277) | **0** | −0,0010 mm |

Die 20 Fälle ohne G-Code enden mit einer Meldung (Acht im Umriss, Teile zu nah) – nichts wird still ausgelassen.
Rot-Probe: die neuen Kerntests `T0747_Fix2ZweitePruefung` gegen den Stand 1855224e7 – **9 von 9 rot** (`reports/T-0747/T-0747-fix2-rotprobe-kern.log`).

**1. Konturen in einer abgelehnten Acht (MITTEL) – behoben.** Liegt eine Kontur in einer sich selbst kreuzenden Kontur (gerade/ungerade-Regel) oder berührt/schneidet sie diese, ist ihre Deutung unsicher: sie wird ausgelassen und gemeldet („Kontur 3 ausgelassen: liegt in oder an Kontur 2, die sich selbst kreuzt …“). Konturen außerhalb bleiben unverändert. Test `Contours_inside_rejected_eight` (x1, x2, x3). Prüfprogramm: „IM MATERIAL“ 28 → 0.

**2. Offene Konturen (MITTEL) – behoben.** Verketten schließt Spalten bis 0,1 mm (`CncCam.ChainTol`; der Spalt wird überbrückt, kein Punkt wird verschoben). Bleibt eine Kontur offen, wird sie in den Fräsarten Automatisch/Außen/Innen ausgelassen und gemeldet („offene Kontur (Enden 1 mm auseinander) – auf der Linie gefräst würde ein Teil um den Fräserradius kleiner …“); auf der Linie nur in der Fräsart „Auf der Linie (Gravur)“. Der vorhandene Test `Cam_modes` wurde dafür angepasst (Gravur jetzt ausdrücklich im Modus „auf“, „auto“ meldet die offene Linie). Test `Open_contours` (0,05 mm Spalt → geschlossen, unabhängiger Abstand ≥ 2,998; 1 mm Spalt → Meldung). Dabei gefunden und behoben: die größere Toleranz hatte beim Schließen echte Bogenpunkte nahe dem Anfang entfernt (Prüfprogramm „g_kleine_radien“ −0,0038) – jetzt werden nur doppelte Punkte (≤ 1 µm) entfernt.

**3. Nicht lesbare Elemente (MITTEL) – behoben (Meldung).** Der CNC-Import meldet „Nicht gelesen: 2× SPLINE (Freiformkurve), 1× ELLIPSE, 1× Blockverweis (INSERT) – diese Teile fehlen in den Fräsbahnen.“, nicht gefräste Texte, ausgeblendete Ebenen mit Anzahl und eine Einheiten-Umrechnung. ELLIPSE/SPLINE abtasten und INSERT auflösen wurde **nicht** gebaut (der Leser ist mit Trendo CAD und Grundriss geteilt; das wäre ein eigenes Paket). Test `Skipped_elements_reported`.

**4. Bogen- und Kurventoleranz (MITTEL) – behoben.** `CadContours.Segments` rechnet die Stückzahl aus der Pfeilhöhe ohne feste Obergrenze (Sicherheitsgrenze: Sehnen nicht kürzer als 0,05 mm, höchstens 4 Mio.). SVG: Bézierkurven werden halbiert, bis die Kontrollpunkte höchstens 0,001 mm (umgerechnet in Pfad-Einheiten) von der Sehne liegen; Bögen, Rundungen und Ellipsen aus der Pfeilhöhe (größter Krümmungsradius a²/b). Tests `Big_radius_true_circle` (R 1000/8000/20000: 2,99895/2,99894/2,99894 mm zum wahren Kreis bei Radius 3, 38–294 ms) und `Long_bezier_true_curve` (Bogen über 3000 mm: 2,99996 mm zur wahren Kurve). Prüfprogramm: schlechtester Wert aller 162 Fälle −0,0010 mm (vorher −0,0277).
Berichtigt: Die frühere Aussage „Bögen auf 0,001 mm“ galt nur bis R ≈ 420 mm; ehrlich gilt jetzt: Abweichung der Kontur vom wahren Bogen ≤ 0,001 mm, Bahn ≥ Radius − 0,001 mm zur Kontur, also ≥ Radius − 0,002 mm zur wahren Geometrie (gemessen −0,0010).

**5. Laufzeit (NIEDRIG) – behoben.** Einlesen (jede Dateigröße), „Bahnen neu“, Fräsart, Haltestege und Änderungen an Fräser-Ø/Tiefe/Zustellung/Vorschub rechnen im Hintergrund mit Karte und „Abbrechen“; übernommen wird ein Rückgängig-Schritt. Abbrechen wirkt auch in `OffsetLoops`, Engstellen-Suche und Abstandsprüfung. Beschleunigt: die teure Seiten-Prüfung (Punkt in Kontur) läuft nur noch für Stücke, die alle anderen Prüfungen bestehen, und die Rasterabfrage bricht beim ersten Treffer ab. Gemessen (Prüfprogramm): Zickzack 5000 Punkte 7 483 → 1 797–2 479 ms, Rauschen 20 000 Punkte 19 092 → 1 608–2 047 ms; Test `Zigzag_speed_and_cancel` (eigener Zickzack 4 742 → ~2 000 ms, Abbrechen nach 100 ms wirkt nach ~130 ms statt 3 787 ms). Die UI-Tests warten jetzt auf das Ergebnis im Hintergrund (Hilfsfunktion, keine Prüfung entfernt).

**6. Kurze Engstellen (NIEDRIG) – behoben.** Messpunkte alle r/8 (0,02–0,25 mm), auch ein einzelner Messpunkt außer Reichweite wird gemeldet. Test `Short_narrow_spot_reported` (spitze Kerbe 0,2 mm). Prüfprogramm: „STILLE LÜCKE“ 5 → 0.

**7. Einheiten in Trendo CAD (Info) – erledigt.** Die Meldung nach dem DXF-Import nennt „Maße in mm umgerechnet aus Zoll (×25,4)“; der CNC-Import meldet die Umrechnung ebenfalls. UI-Test `T0747_FixUi.Cad_import_shows_unit_conversion`.

**Belege:** `teil-fremd-svg.nc` und `fix-faelle-hinweise.txt` neu erzeugt (feinere Abtastung, mehr gemeldete Engstellen), Rohlogs `T-0747-fix2-*.log`, `SHA256SUMS.txt` neu. Prüfskript `pruefe_t0747.py`: BESTANDEN (exakt 1,9990 / 1,9993 / 2,9999 mm).

**Tests (gezielt, kein Volltest):** siehe Abgabe; Rohlogs `T-0747-fix2-kerntests.log`, `T-0747-fix2-apptests.log`.

**Weiterhin offen:** ELLIPSE/SPLINE/INSERT werden gemeldet, aber nicht gefräst · langsame Anzeige sehr großer G-Codes im Editor (vorbestehend) · Taschen mit Inseln nicht gemeinsam ausgeräumt · Laufzeit bei sehr dichten Konturen bleibt im Sekundenbereich (läuft aber im Hintergrund, abbrechbar).

**Testzahlen T-0747-fix2:** Build 0 Fehler. Kern (T0747 alle drei Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests): **93 gesamt, 92 bestanden, 1 übersprungen (vorbestehend „IFC echt“), 0 Fehler**. App: T0747_DxfUi 2/2, T0747_FixUi 3/3, SPEC01_Ui 6/6, SPEC01_UiArc 1/1, SPEC14_Ui 2/2, Bildbearbeitung_Ui 5/5, PlanPhoto_Ui 4/4, N23N24_CadPrintUi 3/3, UebersichtEinfach_Ui 2/2, DeadButtonTests 83/83 – **111/111**. Im ersten Lauf schlug `T0747_FixUi.Big_dxf_background_and_cancel` fehl, weil die neue Abbrechen-Meldung „Abgebrochen“ großschrieb; Meldung auf „Vorgang abgebrochen – nichts geändert …“ geändert (Test unverändert), Wiederholung grün (steht im Rohlog). Prüfskript BESTANDEN. Prüfprogramm des Prüfers: 0 Befunde in 162 Fällen.

## Dritte Prüfung behoben (T-0747-fix3)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix3`. Vorher `origin/main` (0.9.106, 2c9ea226f) eingeführt; einziger Konflikt `.gitattributes`, beide Zeilen behalten. Gebaut und getestet auf diesem Stand. Keine Maschine angesteuert.
**Prüfprogramme beider Prüfer** (nur als Kopie benutzt, nicht im Repo), alle Fälle über `CncJob.ImportDrawing` neu erzeugt:

| Fallgruppe | Fälle | mit G-Code | Befunde | schlechtester Abstand − Radius |
|---|---|---|---|---|
| zweite Prüfung, Hauptsatz | 143 | 123 | 0 (vorher 0) | −0,0010 mm |
| zweite Prüfung, x1–x3 | 3 | 2 | 0 | 0,0000 mm |
| zweite Prüfung, große Radien | 16 | 16 | 0 | −0,0010 mm |
| dritte Prüfung, Hauptsatz (p3) | 37 | 31 | **0** (vorher 2: o3, o5) | −0,0043 mm (Erklärung unten) |
| dritte Prüfung, Spalt mitten / Limaçon (p3b) | 7 | 7 | **0** (vorher 2: o6) | 0,0000 mm |
| dritte Prüfung, Haltestege (p3c) | 9 | 9 | **0**; Anhebungen = Stege in 9/9 (vorher still falsch) | −0,0001 mm |
| dritte Prüfung, Ecke diagonal (p3d) | 2 | 1 | 0 | 0,0000 mm |

Haltestege (s12, 9 Fälle): Anhebungen im Durchgang auf voller Tiefe = Anzahl Stege in allen 9 Fällen. Gravur-Abdeckung (g9): jede wahre Kontur höchstens 0,0011 mm von der Gravurbahn.
Zu den Werten unter −0,002 mm (nur in der Gruppe „dritte Prüfung“, innerhalb der Prüfer-Toleranz 0,01): u6 (Zoll) −0,0024 und v8 (SVG-Bogen gedreht) −0,0043 entstehen in der *Wahrheit* des Fallgenerators – u6 tastet den Kreis in Zoll mit 0,001 Zoll (= 0,0254 mm) Pfeilhöhe ab, v8 lässt den Endpunkt des Bogens weg. Gegen die wahre Geometrie liegt Trendo dort bei ≥ Radius − 0,001 (Bogentoleranz 0,001 mm). Die Bahn gegen Trendos eigene Kontur ist nie näher als Radius − 0,001 mm.

**Rot-Probe:** neue Kerntests `T0747_Fix3DrittePruefung` gegen a9a31b924 – 11 von 11 rot; UI-Tests `T0747_Fix3RaceUi` gegen a9a31b924 – 4 von 4 rot (`reports/T-0747/T-0747-fix3-rotprobe.log`). Danach wurde im Test M2 nur die Mehrdeutigkeits-Geometrie geschärft (die Enden zweier Linien lagen so, dass sie sich zu Recht selbst schlossen).

**M1 Spalt mitten in der Kette – behoben.** `CadContours.Chain` neu: 1. nur exakt passende Enden (≤ 1 µm) verketten, 2. eine Kette mit eigenen Enden ≤ 0,1 mm auseinander schließen, 3. offene Ketten über den kürzesten Spalt verbinden. Beide Endpunkte bleiben, der Spalt ist eine eigene kurze Strecke – die wahre Geometrie wird nie verschoben. Test `Gap_in_the_middle_keeps_both_ends` (o6 mit 0,05/0,09 mm: Kontur enthält (50/0) und (50,05/0), Abstand 3,0000 bei Radius 3). Prüfer o6: vorher −0,05/−0,09 → jetzt 0 Befunde.

**M2/N1 Kandidatenwahl – behoben.** Exakt passende Enden zuerst, sonst der nächste Kandidat nach echtem Abstand (≤ 0,1 + 1e-9, nicht mehr je Achse); geschlossene Ketten werden nie mit anderen verbunden; liegt ein anderes Ende höchstens Abstand + 0,05 mm entfernt, wird nicht überbrückt und gemeldet („Mehrdeutiger Spalt bei X … Y …: … nicht verbunden.“ – erscheint beim CNC-Import unter den Hinweisen). Test `Chaining_nearest_exact_first` (o4, o7, o5, Diagonalspalt 0,113 mm bleibt offen, Spalt 0,1 bei krummen Koordinaten schließt, Mehrdeutigkeit).

**M3 Haltestege – behoben.** `Loop` erkennt Steggrenzen genau auf einem Bahnpunkt (jede Strecke deckt [Anfang, Ende) ab) und zählt die Anhebungen; stimmt die Zahl nicht mit den Stegen überein, gibt es eine Fehlermeldung statt still falschem G-Code. Test `Tabs_on_path_points` (Fräser 2/3/6 mm, Quadrat 20 mm und Seite = Bogenlänge, 2 und 4 Stege: Anhebungen = Stege = Absenkungen).

**M4 Hintergrund-Ergebnis gegen aktuellen Stand – behoben.** Bahnberechnung und CNC-Import merken sich den Dokumentstand beim Start und übernehmen das Ergebnis nur auf genau diesen Stand; sonst wird es verworfen und gemeldet („… wurde verworfen. Bei Bedarf „Bahnen neu“ drücken.“). Die Konturen gehören jetzt mit in den Stempel (`CncJob.ContoursHash`) – G-Code einer anderen Zeichnung gilt immer als veraltet und ist für den Export gesperrt. Hinweis: Dokumente, die mit dem Stand von T-0747-fix2 gestempelt wurden, gelten dadurch einmal als veraltet, bis „Bahnen neu“ gedrückt wird. Tests `T0747_Fix3RaceUi.R2_undo_during_background`, `R3_hand_edit_during_background`.

**M5 offene Konturen als Hindernis – behoben.** Bei der Abstandsprüfung zählen alle Konturen, auch offene und ausgelassene. Test `Open_contour_is_obstacle` (o3: Nachbarteil wird ausgelassen und gemeldet; mit Abstand wird es gefräst, ≥ Radius − 0,002 zum offenen Umriss).

**N2 Limaçon – behoben.** „In der kreuzenden Kontur“ heißt jetzt gerade/ungerade **oder** Windungszahl ≠ 0. Test `Limacon_and_missing_units`. **N3 – behoben.** Eine zweite Wertänderung während der Rechnung wird gemerkt und danach als ein Schritt nachgerechnet, ohne Ausnahme (Test `R1_second_change_is_kept`; Abbrechen verwirft auch die gemerkten Änderungen). **N4 – behoben.** DXF ohne $INSUNITS: „Einheit nicht angegeben – als mm gelesen“ beim CNC-Import und in Trendo CAD (Tests `Limacon_and_missing_units`, `Cad_missing_units`).

**Tests:** siehe Testzahlen unten; Rohlogs `T-0747-fix3-kerntests.log`, `T-0747-fix3-apptests.log`, `T-0747-fix3-pruefskript.log`. Belege der früheren Runden bleiben unverändert (die Ausgaben sind gleich geblieben).

**Weiterhin offen:** ELLIPSE/SPLINE/INSERT nur gemeldet · Anzeige sehr großer G-Codes langsam (vorbestehend) · Taschen mit Inseln nicht gemeinsam ausgeräumt · Mehrdeutige Spalten werden nicht automatisch aufgelöst, nur gemeldet.

**Testzahlen T-0747-fix3 (auf main 0.9.106 + Zweig):** Build 0 Fehler. Kern (T0747 alle vier Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests): **104 gesamt, 103 bestanden, 1 übersprungen (vorbestehend „IFC echt“), 0 Fehler**. App: T0747_DxfUi 2/2, T0747_FixUi 3/3, T0747_Fix3RaceUi 4/4, SPEC01_Ui 6/6, SPEC01_UiArc 1/1, SPEC14_Ui 2/2, Bildbearbeitung_Ui 5/5, PlanPhoto_Ui 4/4, N23N24_CadPrintUi 3/3, UebersichtEinfach_Ui 2/2, DeadButtonTests 83/83 – **115/115**. Prüfskript BESTANDEN. Prüfprogramme beider Prüfer: 0 Befunde in 217 Fällen.

## Vierte Prüfung behoben (T-0747-fix4)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix4`, Commit obendrauf auf 300f50d92. Keine Maschine angesteuert.
**Prüfprogramme aller drei Prüfer** (nur als Kopie benutzt bzw. Fallgenerator `gen_p4.py` ausgeführt, nichts im Repo), alle Fälle über `CncJob.ImportDrawing`:

| Fallgruppe | Fälle | mit G-Code | Befunde | schlechtester Abstand − Radius |
|---|---|---|---|---|
| Prüfer 2 (Haupt, x1–x3, große Radien) | 162 | 141 | 0 | −0,0010 mm |
| Prüfer 3 (p3, p3b, p3c, p3d) | 55 | 48 | 0; Haltestege 9/9 richtig | −0,0043 mm (Wahrheit des Generators, siehe dritte Prüfung) |
| Prüfer 4 (v1–v15) | 72 | 58 | **1** (vorher 9) – siehe unten | −0,0010 mm |

Haltestege in Prüfer 4 (v9/v10/v11): 109 Außenbahnen geprüft, Anhebungen = Stege bzw. „zu kurz“ gemeldet, 0 Abweichungen.
**Der eine verbleibende Befund ist bewusst so:** `v2_platte_doppelt_fast_gleich_d3` – die Platte ist zweimal gezeichnet, die zweite Kopie 0,0005 mm versetzt. Trendo fasst beide als „doppelt gezeichnet“ zusammen (Auftrag H2: „nahezu deckungsgleich … zusammenfassen und melden“) und fräst den Kreis als Loch (Fräsermitte 1,5 mm innen, Test `Duplicate_outlines`). Die Wahrheitsdatei des Generators enthält aber beide Platten als eigene Konturen; das Prüfprogramm zählt gerade/ungerade über alle wahren Umrisse und hält dadurch die Lochfläche für Material („IM MATERIAL“). Nach Fräser-Logik ist das Loch richtig; würde Trendo dem Prüfprogramm folgen, wäre das Loch um den Fräser-Ø zu groß (genau der Fehler aus H2). Vor dem Fix fiel der Fall nicht auf, weil das Loch damals falsch (außen) gefräst wurde.
**Clearance (Hinweis v8):** Dokumentiert und gemessen gilt: Bahn ≥ Radius − 0,001 mm zu Trendos Kontur; Kontur ≤ 0,001 mm vom wahren Bogen, also ≥ Radius − 0,002 mm zur wahren Geometrie. Gemessen über alle 289 Fälle: −0,0010 mm. Die −0,0043 (v8) und −0,0024 (u6) aus Prüfer 3 stammen aus dessen Wahrheitsdateien (v8 lässt den Bogen-Endpunkt weg, u6 tastet mit 0,001 Zoll ab) und widersprechen dem Wert nicht.

**Rot-Probe gegen 300f50d92:** Kerntests `T0747_Fix4ViertePruefung` 5 von 5 rot, UI-Tests `T0747_Fix4RaceUi` 3 von 3 rot (nach Schärfen von P2a: auch B hat eine Zeichnung, sonst fiel die falsche Zuordnung nicht auf) – `reports/T-0747/T-0747-fix4-rotprobe.log`.

**H1 DXF-Normale – behoben.** Der gemeinsame Leser `CadDxf.Parse` wertet 210/220/230 für CIRCLE, ARC, LWPOLYLINE und 2D-POLYLINE aus: Normale (0,0,−1) → x gespiegelt, Bögen 180−a1 … 180−a0, Ausbuchtungen umgekehrt. Schräge Normalen werden nicht gelesen und gemeldet („CIRCLE nicht in der Zeichenebene“). Gilt damit auch für Trendo CAD und den Grundriss (Test `Ocs_in_floor_plan`). Test `Ocs_normal_minus_z` (v14-Fälle: Kreis bei −50/20, gespiegelte Polylinie als Loch, Rundrechteck aus Linien und gespiegelten Bögen schließt sich und hat die richtige Fläche).

**H2 doppelte Konturen – behoben.** Deckungsgleiche geschlossene Konturen (jeder Punkt und jede Stückmitte ≤ 0,001 mm vom anderen Zug, in beide Richtungen; Richtung und Startecke egal) werden vor der Tiefenbestimmung zusammengefasst und gemeldet („Kontur 2: doppelt gezeichnet (deckungsgleich mit Kontur 1) – einmal verwendet.“). Teilweise überlappende Konturen berühren sich und werden wie bisher als „zu nah“ abgelehnt und gemeldet. Test `Duplicate_outlines` (LWPOLYLINE doppelt, 8 LINE, umgedreht, andere Startecke, 0,0005 mm versetzt, doppeltes Loch, teilweise Überlappung).

**M1 Loch in einem ausgelassenen offenen Umriss – behoben.** Konturen in oder an einem ausgelassenen offenen Umriss (geschlossene Annäherung: Enden verbunden) gelten als unsicher und werden ausgelassen und gemeldet. Test `Hole_inside_open_outline` (v5b).

**M2 Warteschlange an das Dokument gebunden – behoben.** Gemerkte Änderungen tragen ihr Dokument; nachgerechnet wird nur dort, und nur wenn es noch offen ist (sonst verfallen sie). Ergebnisse aus dem Hintergrund werden nur in noch offene Dokumente übernommen (auch Grundriss). Tests `T0747_Fix4RaceUi.P2a_close_doc`, `P2b_switch_tab`.

**N1 – behoben.** Gefrorene Ebenen (Flag 70 Bit 1) gelten wie ausgeschaltete als unsichtbar: nicht gefräst, gemeldet („Ausgeblendete Ebenen nicht gefräst: HILFE (1)“). **N2 – behoben.** Bögen unter 0,00001° und Linien der Länge 0 sind keine eigenen Konturen. Test `Frozen_layer_and_tiny_pieces`. **N4 – behoben.** Eine zweite Datei während eines Einlesens wird gemerkt und danach in ihr Dokument eingelesen (Test `Second_file_is_queued`).

**Tests:** siehe Testzahlen unten; Rohlogs `T-0747-fix4-*.log`. Belege der früheren Runden unverändert (gleiche Ausgaben).

**Weiterhin offen:** ELLIPSE/SPLINE/INSERT nur gemeldet · Anzeige sehr großer G-Codes langsam (vorbestehend) · Taschen mit Inseln nicht gemeinsam · TEXT mit Normale −Z wird nicht gespiegelt (Texte werden ohnehin nicht gefräst) · der oben erklärte Prüfer-4-Fall bleibt bewusst „rot“.

**Testzahlen T-0747-fix4:** Build 0 Fehler. Kern (alle fünf T0747-Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests): **109 gesamt, 108 bestanden, 1 übersprungen (vorbestehend „IFC echt“), 0 Fehler**. App: T0747_DxfUi 2/2, T0747_FixUi 3/3, T0747_Fix3RaceUi 4/4, T0747_Fix4RaceUi 3/3, SPEC01_Ui 6/6, SPEC01_UiArc 1/1, SPEC14_Ui 2/2, Bildbearbeitung_Ui 5/5, PlanPhoto_Ui 4/4, N23N24_CadPrintUi 3/3, UebersichtEinfach_Ui 2/2, DeadButtonTests 83/83 – **118/118**. Prüfskript BESTANDEN. Prüfprogramme: 289 Fälle, 1 Befund (oben erklärt).

## Fünfte Prüfung behoben (T-0747-fix5)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix5`, Commit obendrauf auf a86f84d55. Keine Maschine angesteuert.
**Prüfprogramme aller Prüfer** (nur als Kopie benutzt bzw. Fallgeneratoren `gen_p4.py`, `gen_p5.py`, `gen_p5_spline.py` und `check_p5_stege.py` ausgeführt, nichts im Repo), alle Fälle über `CncJob.ImportDrawing`:

| Fallgruppe | Fälle | mit G-Code | Befunde | schlechtester Abstand − Radius | Rechenzeit gesamt (längster Fall) |
|---|---|---|---|---|---|
| Prüfer 2 (Haupt, x1–x3, große Radien) | 162 | 141 | 1 – Wahrheitsdatei, siehe unten | −0,0010 mm | 10,2 s (`l_zickzack_5000` 1,5 s) |
| Prüfer 3 (p3, p3b, p3c, p3d) | 55 | 48 | 0 | −0,0043 mm (Wahrheit des Generators, siehe dritte Prüfung) | 1,2 s |
| Prüfer 4 (v1–v15) | 72 | 58 | 1 – `v2`, wie in der vierten Prüfung erklärt | −0,0010 mm | 0,8 s |
| Prüfer 5 (b, f, g, h, k, l, n, q, t, u, z) | 80 | 74 | 4 – alle Wahrheitsdatei bzw. Prüfprogramm, siehe unten; Haltestege 4/4 richtig | −0,0010 mm | 12,7 s (`g2_50000_kreise` 10,6 s, `g1_50000_linien` 1,5 s, `g3_polylinie_50000_punkte` 0,3 s) |
| Prüfer 5 Spline-Polylinien | 8 | 8 | 0 | −0,0001 mm | 0,1 s |

**Die sechs verbleibenden Befunde – keiner ist eine falsche Bahn:**
- `v2_platte_doppelt_fast_gleich_d3` (Prüfer 4) und `k1_kreis_in_kreis_0_0005_d3` (Prüfer 5): gleiche Ursache. Zwei Konturen liegen 0,0005 mm auseinander; Trendo fasst sie als „doppelt gezeichnet“ zusammen (Toleranz 0,001 mm, gemeldet) und fräst ein Loch. Die Wahrheitsdatei führt beide als eigene Umrisse, das Prüfprogramm zählt gerade/ungerade und hält die Lochfläche deshalb für Material. Die Fälle `k1_…_0_0011`, `_0_002`, `_0_01`, `_0_5` (über der Toleranz) sind ohne Befund.
- `p_spline_block_d6` (Prüfer 2): Die ELLIPSE darin wird jetzt gelesen (M2) und als Loch gefräst. Die Wahrheitsdatei stammt aus der Zeit, als ELLIPSE nicht gelesen wurde, und kennt nur die Platte – daher „IM MATERIAL“ genau auf der Ellipse.
- `n8_ellipse_ocs_als_loch_d3` (Prüfer 5): Laut DXF-Referenz stehen Mittelpunkt (10/20) und Hauptachse (11/21) der ELLIPSE **in Weltkoordinaten**, anders als bei CIRCLE/ARC. Die Normale (0,0,−1) dreht nur die Nebenachse, der Mittelpunkt bleibt bei X 60. Die Wahrheitsdatei spiegelt den Mittelpunkt wie bei einem Kreis nach X −60 und erwartet dort ein Loch. Trendo folgt der Referenz (Test `Ellipse_is_sampled`, Normale −Z).
- `f6_radius_negativ_und_null_d3` (Prüfer 5): CIRCLE mit Radius −10 bzw. 0 wird nach Auftrag N4 nicht geraten, sondern verworfen und gemeldet („Nicht gelesen: … 1× CIRCLE mit Radius ≤ 0“). Die Wahrheitsdatei nimmt ein Loch mit |r| an (dort selbst mit Fragezeichen versehen). Weil die Meldung nicht mit „Kontur n“ beginnt, zählt das Prüfprogramm eine stille Lücke.
- `g2_50000_kreise_d1` (Prüfer 5): Die Kreise stehen absichtlich nicht in der Wahrheitsdatei (laut Generator prüft sie ein eigenes `check_p5.py` analytisch, das nicht mitgeliefert wurde). `check.py` hält deshalb alle 49 999 Löcher für Material. Eigene Nachrechnung über den G-Code (Skript nur im Arbeitsordner): 49 999 von 49 999 Löchern gefräst, kleinster Abstand Fräsermitte – Lochkante 0,5004 mm (Radius 0,5), Umriss 0,5000 mm, 0 Punkte im Material.

**Rot-Probe gegen a86f84d55:** Kerntests `T0747_Fix5FuenftePruefung` 7 von 7 rot (für das Übersetzen nur Platzhalter für die neuen Schnittstellen `CadDxf.Box`, `ReadResult.Unread` und `CamSettings.Unread`) – `reports/T-0747/T-0747-fix5-rotprobe.log`.

**M1 3D-Polylinie mit Normale – behoben.** Bei POLYLINE mit Flag 8 (3D-Polylinie) oder 16/64 (Netz) wird die Normale nicht ausgewertet: Diese Punkte stehen in Weltkoordinaten wie bei LINE. Eine schräge Normale macht sie daher nicht mehr „nicht in der Zeichenebene“. Netze werden wie bisher nicht gelesen und gemeldet. Bei 3D-Polylinien werden Ausbuchtungen (42) nicht verwendet. Test `Polyline3d_ignores_normal` (z2-Fall: Platte mit Normale −Z bleibt bei X 0…100, Kreis als Loch).

**M2 Umriss nicht lesbar – behoben, Entscheidung:**
- **ELLIPSE wird abgetastet** (genaue Mathematik, sicher): volle Ellipse als geschlossene Polylinie, Teilstück offen, Schrittweite aus der Krümmung so gewählt, dass die Pfeilhöhe ≤ 0,001 mm ist (gemessen 0,00044 mm). Bei Normale −Z dreht sich die Nebenachse.
- **INSERT wird nicht aufgelöst.** Ein falsch platzierter Block (Basispunkt, Drehung, Skalierung, Spiegelung, Felder, Verschachtelung) ergäbe falschen G-Code. Stattdessen rechnet Trendo die **Ausdehnung** jedes Blockverweises vorsichtig aus: Rahmen des Blockinhalts mit Basispunkt, Skalierung, Drehung, MINSERT-Feld, Verschachtelung bis Tiefe 16 und Normale −Z. Rekursiver Block → „unbekannt, überall“, fehlender Block → leer.
- Das Gleiche gilt für **SPLINE und Elemente mit schräger Normale**.
- Jede gelesene Kontur, die ganz in der Ausdehnung eines nicht gelesenen Elements liegt, wird **ausgelassen und gemeldet**: „Kontur n ausgelassen: liegt in nicht gelesenem Element Blockverweis „PLATTE“ – ob sie Loch oder Teil ist (die Seite), lässt sich nicht sicher sagen.“ (b2: Löcher nicht mehr außen gefräst.)
- Liegt umgekehrt ein nicht gelesenes Element in einer Kontur, wird diese gefräst, aber gemeldet: „… enthält nicht gelesenes Element … – dort wird nichts gefräst.“ (b4)
- HATCH und SOLID werden wie bisher nur gemeldet, ohne Auswirkung auf die Seite.
- Die Ausdehnungen werden mit dem Auftrag gespeichert (`unread`) und gehen in die Prüfsumme der Konturen ein.
- Tests `Insert_outline_makes_inside_unsure`, `Insert_extent_transform`, `Oblique_outline_makes_inside_unsure`, `Ellipse_is_sampled` (n7: Ellipse als Platte mit Löchern).

**N1 viele Konturen – behoben.**
- Doppel-Prüfung mit Vorfilter: nur Paare mit fast gleichem Rahmen (Raster über die linke untere Ecke) und fast gleicher Fläche werden genau verglichen.
- Die Tiefe wird über ein Raster der Rahmen bestimmt.
- Versatz, Engstellen und Abstandsprüfung laufen je Kontur parallel; jeder Thread hat einen eigenen Merker, die Meldungen bleiben in Konturreihenfolge.
- Die Reihenfolge vieler Bahnen wird über eine Nachbarsuche im Raster bestimmt.
- Höchstzahl Elemente 200 000 (vorher 50 000).
- Gemessen: 50 001 Kreise 18,8 s im Test (`Many_circles_fast`, vorher geschätzt 45 min), im Prüferfall `g2` 10,6 s. Läuft im Hintergrund wie bisher, Abbrechen nach 0,2 s wirkt in unter 3 s.

**N2 – behoben.** Zahlen NaN/∞ oder Koordinaten ≥ 1e9 mm (nach Einheit) verwerfen nur das betroffene Element und werden gemeldet („CIRCLE mit ungültiger Koordinate“), der Rest wird gelesen. Das gilt auch für POLYLINE-Punkte.
**N3 – behoben.** 999-Kommentare werden überall entfernt, auch zwischen `$INSUNITS` und dem Wert.
**N4 – behoben.** CIRCLE/ARC mit Radius ≤ 0 werden verworfen und gemeldet.
**N5 – behoben.** Bei PEDIT-Splines (Flag 4) werden Rahmenpunkte (Vertex-Flag 16) übersprungen; nur die Kurvenpunkte zählen (z5 und Spline-Fälle ohne Befund).
**N6 – behoben.** NaN verwirft nur das Element (siehe N2).
Test zu N2–N6: `Robust_reader`.

**Tests:** siehe Testzahlen unten; Rohlogs `T-0747-fix5-*.log`.

**Weiterhin offen:**
- INSERT und SPLINE werden nicht aufgelöst; Konturen in ihrer Ausdehnung werden ausgelassen und gemeldet (bewusst).
- Die Anzeige sehr großer G-Codes ist langsam (vorbestehend).
- Taschen mit Inseln werden nicht gemeinsam gefräst.
- TEXT mit Normale −Z wird nicht gespiegelt.
- Die oben erklärten sechs Prüfer-Fälle bleiben bewusst „rot“.

**Testzahlen T-0747-fix5:** Build 0 Fehler. Kern (alle sechs T0747-Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests): **116 gesamt, 115 bestanden, 1 übersprungen (vorbestehend „IFC echt“), 0 Fehler**. App: T0747_DxfUi 2/2, T0747_FixUi 3/3, T0747_Fix3RaceUi 4/4, T0747_Fix4RaceUi 3/3, SPEC01_Ui 6/6, SPEC01_UiArc 1/1, SPEC14_Ui 2/2, Bildbearbeitung_Ui 5/5, PlanPhoto_Ui 4/4, N23N24_CadPrintUi 3/3, UebersichtEinfach_Ui 2/2, DeadButtonTests 83/83 – **118/118**. Prüfskript BESTANDEN. Prüfprogramme: 377 Fälle, 6 Befunde (oben erklärt, keine falsche Bahn). Belege der früheren Runden unverändert (gleiche Ausgaben).

## Sechste Prüfung behoben (T-0747-fix6)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix6`, Commit obendrauf auf 7b253bbad. Keine Maschine angesteuert.
**Prüfprogramme aller Prüfer** (nur als Kopie benutzt bzw. Fallgeneratoren ausgeführt, darunter `gen_p6.py gen` und `gen_p6.py check`; nichts im Repo), alle Fälle über `CncJob.ImportDrawing`:

| Fallgruppe | Fälle | mit G-Code | Befunde | schlechtester Abstand − Radius | Rechenzeit gesamt (längster Fall) |
|---|---|---|---|---|---|
| Prüfer 2 | 162 | 141 | 1 (p_spline_block, erklärt) | −0,0010 mm | 10,4 s (`l_zickzack_5000` 1,6 s) |
| Prüfer 3 | 55 | 48 | 0 | −0,0043 mm (Wahrheit des Generators, erklärt) | 1,3 s |
| Prüfer 4 | 72 | 58 | 1 (v2, erklärt) | −0,0010 mm | 1,2 s |
| Prüfer 5 | 80 | 72 (f4, f7 jetzt bewusst ohne) | 4 (f6, g2, k1_0_0005, n8 – erklärt); Haltestege 4/4 richtig | −0,0010 mm | 12,9 s (`g2_50000_kreise` 10,5 s) |
| Prüfer 5 Spline | 8 | 8 | 0 | −0,0001 mm | 0,1 s |
| Prüfer 6 (i, e, x, d) | 30 | 9 | 0 (`gen_p6.py check`); `check.py` je Fall: 0 (d1 mit 958 Meldungen und alle Lücken gemeldet) | −0,0010 mm (e7) | 1,9 s (`d1_viele_konturen` 1,8 s) |

Ellipsen (Prüfer 6, `gen_p6.py check`): Pfeilhöhe höchstens 0,00100 mm, Punkt-Abweichung ≤ 9e-12 mm, Teilellipsen an richtiger Stelle und Richtung (Endpunkte ≤ 2e-14 mm). Bei i1–i16 und x1–x5 werden alle Löcher im wahren Umriss ausgelassen und gemeldet. Determinismus d1: unverändert (vom Prüfer bestätigt, Meldungen in Konturreihenfolge).

**Bewertung:** Keine neuen Befunde. Übrig sind nur die sechs in der fünften Prüfung erklärten Fälle (v2, k1_0_0005, p_spline_block, n8, f6, g2), alle Fehler der Wahrheitsdatei bzw. des Prüfprogramms, vom Prüfer bestätigt.
**Absichtlich ohne G-Code:** f4, f7 (Prüfer 5; Kreis mit NaN bzw. 1e300) sowie i1–i16 und x1–x5 (Prüfer 6). Dort steckt der Umriss in einem nicht gelesenen Element, dessen Ausdehnung unbekannt ist bzw. alle Löcher enthält. Trendo lässt alles aus und meldet es, statt Löcher als Teile außen zu fräsen.

**Rot-Probe gegen 7b253bbad:** `T0747_Fix6SechstePruefung` 7 von 7 rot, ohne Platzhalter übersetzbar. `Ellipse_extreme_parameters` hängt auf dem alten Stand: Abbruch nach 30 s mit `--blame-hang-timeout`. Log: `reports/T-0747/T-0747-fix6-rotprobe.log`.

**M-1 Ausdehnung vorsichtig – behoben.** Ein gemeinsamer Rechner `ShapeBox` ersetzt die alten Rahmen aus Einzelpunkten.
- **Bögen und Ausbuchtungen:** Für jede Ausbuchtung (beliebig groß, auch 3 und −3) werden Mittelpunkt und Radius des Bogens berechnet. Endpunkte und alle Achsen-Scheitel im Bogenbereich gehen in den Rahmen ein; das gilt für LWPOLYLINE, alte POLYLINE im Block (i9) und ARC.
- **Normale:** Bei alten POLYLINE wirkt die Normale aus dem Kopf (i10). Normale −Z wird gespiegelt. Schräge Normalen werden über die DXF-Regel „beliebige Achse“ umgerechnet; Kreise und Bögen gehen dann als Raumkreis ein.
- **ELLIPSE:** Rahmen der vollen Ellipse aus Haupt- und Nebenachse.
- **SPLINE:** konvexe Hülle der Kontrollpunkte. Ein SPLINE nur mit Fit-Punkten (i16) oder mit Gewicht ≤ 0 gilt als „unbekannt, überall“.
- **HATCH:** Randpunkte. Dazu kommt ein Zuschlag aus Bogenradien und Ellipsenachsen sowie für Ausbuchtungen höchstens max(1, |b|) · halbe Diagonale (sicherer Zuschlag).
- Tests: `Block_extent_bulges_and_normal` (Ausbuchtung 0,3 / 1 / 1,5 / 3 / −3 / 12 gegen fein abgetastete wahre Ausdehnung; alte POLYLINE mit Ausbuchtung und mit Normale −Z; Loch außerhalb des Punkt-Rahmens wird ausgelassen) und `Unknown_extents`.

**M-2 alle verworfenen Umrisse – behoben.** Jedes nicht gelesene oder verworfene Element, das allein einen Umriss bilden kann, kommt mit seiner Ausdehnung in dieselbe Auslass-Logik:
- CIRCLE mit Radius < 0: Ausdehnung |r|.
- Geschlossene LWPOLYLINE/POLYLINE mit NaN oder ∞, unbekannte Elemente, MLINE, REGION, 3DSOLID, BODY, SURFACE, MESH, WIPEOUT: Ausdehnung „unbekannt, überall“.
- HATCH, SOLID, TRACE und 3DFACE als Füllfläche.
- Konturen darin werden ausgelassen und deutlich gemeldet. Bei unbekannter Ausdehnung lautet die Meldung: „… hat keine bestimmbare Ausdehnung – es könnte diese Kontur umschließen …“.
- Ausnahmen:
  - Linien, Bögen und offene Züge bilden allein keinen Umriss. Fehlen sie, bleibt eine offene Kontur, und die wird schon ausgelassen und gemeldet.
  - Eine Füllfläche, deren Rechteck sich mit einer gelesenen Kontur deckt (± 0,01 mm), hat ihren Rand gezeichnet und ändert nichts (h1 bleibt richtig).
  - Ein Kreis mit Radius 0 ist kein Umriss.
- Test `Unreadable_outlines_make_inside_unsure` (x1–x5, h1, Radius 0).

**M-3 ELLIPSE ohne Endlosschleife – behoben.** Die Parameter werden per Rest normalisiert, ohne Schleife, die von der Eingabe abhängt. Beträge über 1e6 werden verworfen, gemeldet („ELLIPSE mit unsinnigem Parameter“) und mit ihrer Ausdehnung als nicht gelesen geführt. Test `Ellipse_extreme_parameters`: 1e17 und −1e300 in 3 ms; 4π + 0,3 … 4π + 2,5 ergibt dieselbe Teilellipse wie 0,3 … 2,5.

**N-1 – behoben.** Die Ausdehnung von Blockverweisen wird erst am Ende berechnet, damit BLOCKS auch nach ENTITIES stehen dürfen (i14). Ein Xref-Block (Flag 4 oder 8) gilt als „unbekannt, überall“ (i15).

**N-2 – behoben.** Der Grundriss übernimmt Ellipsen nicht als Wand und meldet sie („Nicht lesbar: 1× ELLIPSE (nicht als Wand übernommen)“), statt dass sie still in kurze Stücke zerfallen. Test `Floor_plan_reports_ellipse`.

**N-3 – behoben.**
- Neue Schrittweite aus |r''(t)| ≤ max(a, b): Pfeilhöhe ≤ h²/8 · max(a, b) ≤ 0,001 mm, unabhängig vom Verhältnis.
- Die Scheitel sind immer Stützpunkte. Sonst würden bei sehr flachen Ellipsen zwei Punkte beidseits der Spitze beim Verketten zusammengefasst, und die Spitze würde abgeschnitten (gemessen vorher 0,0011 mm).
- Punktzahlen: flache Ellipse 0,001 jetzt 704 Punkte (vorher 275 747), 1500/800 mit 0,02: 2 900 Punkte.
- Höchstens 200 000 Punkte je Ellipse und 2 000 000 je Datei; darüber wird die Ellipse gemeldet und als nicht gelesen mit Ausdehnung geführt.

**N-4 – behoben.**
- Verhältnis > 1 wird richtig abgetastet (die Schrittweite nimmt die größere Halbachse), e7 ohne Befund.
- Die Prüfsumme der Konturen schreibt die Ausdehnungen kulturunabhängig (Test `Contours_hash_invariant`).
- Test zu N-3/N-4: `Ellipse_points_ratio_limit` (Pfeilhöhe auch nach dem Verketten ≤ 0,001 mm).

**Tests:** siehe Testzahlen unten; Rohlogs `T-0747-fix6-*.log`.

**Weiterhin offen:**
- INSERT, SPLINE, MLINE und REGION werden nicht aufgelöst; Konturen in ihrer Ausdehnung werden ausgelassen und gemeldet (bewusst).
- Die Anzeige sehr großer G-Codes ist langsam (vorbestehend).
- Taschen mit Inseln werden nicht gemeinsam gefräst.
- TEXT mit Normale −Z wird nicht gespiegelt.
- Die sechs erklärten Prüfer-Fälle bleiben „rot“.

**Testzahlen T-0747-fix6:** Build 0 Fehler. Kern (alle sieben T0747-Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests): **123 gesamt, 122 bestanden, 1 übersprungen (vorbestehend „IFC echt“), 0 Fehler**. App: T0747_DxfUi 2/2, T0747_FixUi 3/3, T0747_Fix3RaceUi 4/4, T0747_Fix4RaceUi 3/3, SPEC01_Ui 6/6, SPEC01_UiArc 1/1, SPEC14_Ui 2/2, Bildbearbeitung_Ui 5/5, PlanPhoto_Ui 4/4, N23N24_CadPrintUi 3/3, UebersichtEinfach_Ui 2/2, DeadButtonTests 83/83 – **118/118**. Prüfskript BESTANDEN. Prüfprogramme: 407 Fälle, keine neuen Befunde (sechs erklärte). Belege der früheren Runden unverändert (gleiche Ausgaben).

## Siebte Prüfung behoben (T-0747-fix7)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix7`, Commit obendrauf auf a49d5c572. Keine Maschine angesteuert.
**Prüfprogramme aller Prüfer** (nur als Kopie benutzt bzw. Fallgeneratoren ausgeführt, darunter `gen_p7.py gen/check`; nichts im Repo), alle Fälle über `CncJob.ImportDrawing`:

| Fallgruppe | Fälle | mit G-Code | Befunde | Rechenzeit gesamt |
|---|---|---|---|---|
| Prüfer 2 | 162 | 141 | 1 (p_spline_block, erklärt) | 10,3 s |
| Prüfer 3 | 55 | 48 | 0 | 1,2 s |
| Prüfer 4 | 72 | 58 | 1 (v2, erklärt) | 1,1 s |
| Prüfer 5 | 80 | 72 | 4 (f6, g2, k1_0_0005, n8 – erklärt); Haltestege 4/4 richtig | 13,8 s (g2 50 000 Kreise 11,6 s) |
| Prüfer 5 Spline | 8 | 8 | 0 | 0,2 s |
| Prüfer 6 (`gen_p6.py check`) | 30 | 9 | 0 | 1,9 s |
| Prüfer 7 (`gen_p7.py check`) | 24 | 12 | 0 | 0,2 s |

**Prüfer 7 im Einzelnen:**
- c1–c11 liefern G-Code mit beiden Löchern innen gefräst: c1 deckungsgleiche Schraffur weit vom Nullpunkt, c2 kleine Schraffur in der Platte, WIPEOUT im Türblock, OLE2FRAME/Proxy/SHAPE, Ansichtsfenster/XLINE, Bemaßungsblock.
- b1 und b3 sind sicher (alles ausgelassen und gemeldet). b2 fräst die Platte und lässt das Loch in der nicht gezeichneten Insel aus.
- a1–a8 (Blöcke mit Ausbuchtungen, Normalen, SPLINE mit Gewichten): alle Löcher im wahren Umriss ausgelassen.

Abstände wie in der sechsten Prüfung (schlechtester −0,0010 mm).

**Rot-Probe gegen a49d5c572:** `T0747_Fix7SiebtePruefung` 3 von 3 rot – `reports/T-0747/T-0747-fix7-rotprobe.log`.

**M-1 Schraffur-Rahmen – behoben.** `HatchLoops` liest den Randpfad-Abschnitt (91 … 97) Schleife für Schleife:
- Polylinien-Ränder mit Ausbuchtungen, Linien, Bögen (im Uhrzeigersinn gespeichert: vorsichtig als Vollkreis), Ellipsen (volle Ellipse) und Splines (Kontroll- und Fit-Punkte; Gewicht ≤ 0 → unbekannt).
- Erhebungspunkt und Saatpunkte zählen nicht mehr.
- Ist der Abschnitt nicht wie erwartet aufgebaut, gilt die Schraffur als „unbekannt, überall“.
- Test `Hatch_box_from_boundary_only` (c1, c2, Rand aus Linie + Bogen).

**N-1 Füllflächen-Ausnahme nur bei gleicher Form – behoben.**
- Jede Randschleife ist ein eigener Eintrag; Inseln werden also einzeln behandelt.
- Eine Schleife fällt nur weg, wenn sie formgleich mit einer gezeichneten, sichtbaren, geschlossenen Kontur ist. Formgleich heißt: jeder Punkt und jede Stückmitte beider Züge höchstens 0,01 mm vom anderen, Richtung und Startecke egal, auch bei einem Rand aus einzelnen LINE.
- Der Vergleich passiert im Leser `CadDxf.Parse` über die verketteten Konturen. Der bisherige Rechteck-Vergleich in `CncCam` ist entfernt.
- Schraffuren auf ausgeblendeten Ebenen zählen nicht.
- SOLID/TRACE/3DFACE werden als Viereck verglichen.
- Test `Fill_exception_needs_same_shape` (b1, b2, b3, Rand aus LINE mit anderer Startecke, ausgeblendete Ebene).

**N-2 WIPEOUT, OLE2FRAME, Proxy, SHAPE – behoben.**
- WIPEOUT ist eine Füllfläche mit Ausdehnung: Bildrahmen aus Einfügepunkt + u·U + v·V mit ½ Pixel Reserve; der Ausschnitt liegt darin.
- OLE2FRAME: Ausdehnung aus den zwei Ecken.
- ACAD_PROXY_ENTITY ohne Ersatzgrafik (92 = 0, kein 310) zeichnet nichts; mit Ersatzgrafik bleibt sie „unbekannt, überall“.
- SHAPE (Symbol aus einer Formdatei) zählt wie Text.
- Test `Wipeout_ole_proxy_shape`.

**Weiterhin offen:**
- INSERT, SPLINE, MLINE und REGION werden nicht aufgelöst.
- Schraffur-Bögen im Uhrzeigersinn gehen nur als Vollkreis in den Rahmen ein und nicht in den Formvergleich; solche Schraffuren gelten also nie als „schon gezeichnet“ (vorsichtig).
- Die sechs erklärten Prüfer-Fälle bleiben „rot“.

**Testzahlen T-0747-fix7:** Build 0 Fehler. Kern (alle acht T0747-Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests): **126 gesamt, 125 bestanden, 1 übersprungen (vorbestehend „IFC echt“), 0 Fehler**. App: T0747_DxfUi 2/2, T0747_FixUi 3/3, T0747_Fix3RaceUi 4/4, T0747_Fix4RaceUi 3/3, SPEC01_Ui 6/6, SPEC01_UiArc 1/1, SPEC14_Ui 2/2, Bildbearbeitung_Ui 5/5, PlanPhoto_Ui 4/4, N23N24_CadPrintUi 3/3, UebersichtEinfach_Ui 2/2, DeadButtonTests 83/83 – **118/118**. Prüfskript BESTANDEN. Prüfprogramme: 431 Fälle, keine neuen Befunde (sechs erklärte). Belege der früheren Runden unverändert.

## Achte Prüfung behoben (T-0747-fix8)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix8`, Commit obendrauf auf d3aa0f251. Keine Maschine angesteuert.
**Prüfprogramme aller Prüfer** (nur als Kopie benutzt bzw. Fallgeneratoren und Prüfteile ausgeführt, nichts im Repo), alle Fälle über `CncJob.ImportDrawing`:

| Fallgruppe | Fälle | mit G-Code | Befunde | Rechenzeit gesamt |
|---|---|---|---|---|
| Prüfer 2 | 162 | 141 | 1 (p_spline_block, erklärt) | 12,1 s |
| Prüfer 3 | 55 | 48 | 0 | 1,5 s |
| Prüfer 4 | 72 | 58 | 1 (v2, erklärt) | 1,5 s |
| Prüfer 5 | 80 | 72 | 4 (f6, g2, k1_0_0005, n8 – erklärt); Haltestege 4/4 richtig | 17,3 s (g2 50 000 Kreise 14,5 s; Rechner nebenbei ausgelastet) |
| Prüfer 5 Spline | 8 | 8 | 0 | 0,6 s |
| Prüfer 6 (`gen_p6.py check`) | 30 | 9 | 0 | 2,7 s |
| Prüfer 7 (`gen_p7.py check`) | 24 | 12 | 0 | 0,4 s |
| Prüfer 8 Schraffur (`gen_p8.py check`, cases) | 78 | 14 | **0** (vorher 16) | 0,4 s |
| Prüfer 8 Schraffur (cases2) | 9 | 5 | **0** (vorher 4) | 0,2 s |

Keine neuen Befunde, nur die sechs erklärten. Alle Fälle mit kaputter Schraffur (b01–b19, f02, g06, g07) werden ausgelassen und gemeldet. Alle Fälle, die G-Code verlangen („need“/„mill“, u. a. c01, c02, c04, c05, c16, g01–g05), liefern ihn mit den Löchern innen.

**Rot-Probe gegen d3aa0f251:** `T0747_Fix8AchtePruefung` 5 von 5 rot. Für das Übersetzen gab es nur Platzhalter für `KindLabel`, `ReadResult.Redundant` und `Box.ToSafe`. Log: `reports/T-0747/T-0747-fix8-rotprobe.log`.

**H-1 Schleifenzahl 91 – behoben.** `HatchLoops` gibt jetzt `null` (= Ausdehnung unbekannt, überall) zurück, wenn:
- 91 fehlt, negativ, nicht ganzzahlig, NaN oder Text ist;
- 91 = 0 ist, aber Randdaten folgen;
- nach der letzten Schleife noch Randgruppen stehen (91 zu klein).

Eine leere Liste gibt es nur bei 91 = 0 ohne Randdaten. Das gilt auch für Schraffuren im Block (f02).

**H-2 Kantenzahl 93 – behoben.**
- Nach jeder Schleife darf nur 97/330 folgen, danach die nächste Schleife (92) bzw. das Abschnittsende; sonst `null`.
- Kantenrand: 93 ≥ 1. Polylinienrand: mindestens 3 Punkte, oder 2 Punkte mit Ausbuchtung (Kreis aus zwei Halbkreisen).
- Alle Zahlen müssen ganzzahlig sein und dürfen nicht größer sein als die vorhandenen Daten.
- Eine Schleife mit leerem oder NaN-Rahmen wird nie mehr verworfen, sondern gilt als „überall“.

**H-3 NaN/∞ – behoben.**
- `SaneNumbers` prüft jetzt alle Gleitkomma-Gruppen (10–59, 110–149, 210–239, 460–469, 1010–1059) auf NaN/∞; Winkel (50–59) mit Betrag über 1e6 gelten als ungültig.
- Zentral: `Box.With` macht aus einem nicht endlichen Punkt „überall“, `Box.Union` macht aus NaN „überall“, `ToSafe()` vor Skalierung und in `CncCam`.
- Gesucht, wo sonst NaN in einen Rahmen gelangen konnte: In Ellipsen-, Kreis- und Blockrahmen wurden Rahmen direkt gebaut. Sie gehen jetzt alle über `Union`/`ToSafe`.

**M-1 Spline-Kante mit „97 0“ – behoben.** Nach Kontrollpunkten und Gewichten wird 97 immer als Fit-Zahl gelesen (auch 0), danach optional 12/22 und 13/23 (c16, g04).

**M-2 WIPEOUT-Ausschnitt – behoben.** Die Ausschnitt-Punkte 14/24 werden mit ½ Pixel Versatz über U/V nach Weltkoordinaten umgerechnet und mit dem Rahmen vereinigt (e02).

**Niedrig – behoben.**
- **Formvergleich:**
  - Ellipsenkanten werden abgetastet.
  - Bögen und Ellipsen mit 73 = 0 werden mit negativem Bogen abgetastet. Ihr Rahmen ist vorsichtig der Vollkreis bzw. die volle Ellipse.
  - Spline-Kanten mit Grad 1 oder mit Kontrollpunkten auf einer Geraden (geklemmte Knoten) gelten als Zug.
  - Toleranz dokumentiert: **je Achse ± 0,01 mm (Chebyshev)**, wie beim Rahmenvergleich. 0,009 mm rundum gilt noch als gleich (c05), 0,011 mm nicht (c06, c07). Rein euklidisch wäre die Ecke bei c05 0,0127 mm.
- **Ausgeblendete Ebenen:** Füllflächen dort zählen nur nicht, wo sie formgleich mit einer gezeichneten Kontur sind oder ganz in einer liegen; außerhalb gelten sie als nicht gelesener Umriss (c15). Groß-/Kleinschreibung der Ebene ist egal.
- **Zeittest N1:** lastrobust gemacht. Grenze 120 s, Messwert im Log (einzeln etwa 20 s); Abbrechen < 10 s (einzeln unter 1 s).
- **Meldungen:**
  - Eine Bezeichnungstabelle `CadDxf.KindLabel` für Grundriss, Trendo CAD und Fräsen: Schraffur, Abdeckfläche, Bemaßung, Mehrfachlinie usw.
  - Im Grundriss heißt es jetzt „Nicht übernommen“.
  - Ganz formgleiche Schraffuren zählt `ReadResult.Redundant`, sie erscheinen beim Fräsen nicht mehr unter „fehlen in den Fräsbahnen“.
- **Warnung CS0649:** `Seg.T` entfernt.

**Tests:** `T0747_Fix8AchtePruefung`:
- `Hatch_counts_strict`
- `Nan_never_in_box`
- `Spline_fit_zero_and_shapes`
- `Wipeout_clip_outside_frame`
- `Labels_and_redundant_fill`

Rohlogs: `T-0747-fix8-*.log`.

**Weiterhin offen:**
- INSERT, SPLINE, MLINE und REGION werden nicht aufgelöst.
- Die sechs erklärten Prüfer-Fälle bleiben „rot“.

**Testzahlen T-0747-fix8:** Build 0 Fehler, CS0649 in PlanDxfImport weg. Kern (alle neun T0747-Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests): **131 gesamt, 130 bestanden, 1 übersprungen (vorbestehend „IFC echt“), 0 Fehler**. App: T0747_DxfUi 2/2, T0747_FixUi 3/3, T0747_Fix3RaceUi 4/4, T0747_Fix4RaceUi 3/3, SPEC01_Ui 6/6, SPEC01_UiArc 1/1, SPEC14_Ui 2/2, Bildbearbeitung_Ui 5/5, PlanPhoto_Ui 4/4, N23N24_CadPrintUi 3/3, UebersichtEinfach_Ui 2/2, DeadButtonTests 83/83 – **118/118**. Prüfskript BESTANDEN. Prüfprogramme: 518 Fälle, keine neuen Befunde (sechs erklärte). Belege der früheren Runden unverändert.

## Neunte Prüfung behoben (T-0747-fix9)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix9`, Commit obendrauf auf 07675b8f3. Keine Maschine angesteuert.
**Prüfprogramme aller Prüfer** (nur als Kopie benutzt bzw. Fallgeneratoren und Prüfteile ausgeführt; Harness-Kopie des Prüfers 9 mit `--lesen`, `--abbruchlesen`, `--abbruch`; nichts im Repo), alle Fälle über `CncJob.ImportDrawing`:

| Fallgruppe | Fälle | mit G-Code | Befunde | Rechenzeit gesamt |
|---|---|---|---|---|
| Prüfer 2 | 162 | 141 | 1 (p_spline_block, erklärt) | 12,4 s |
| Prüfer 3 | 55 | 48 | 0 | 1,7 s |
| Prüfer 4 | 72 | 58 | 1 (v2, erklärt) | 1,1 s |
| Prüfer 5 | 80 | 72 | 4 (f6, g2, k1_0_0005, n8 – erklärt); Haltestege 4/4 richtig | 15,8 s (g2 50 000 Kreise 13,2 s) |
| Prüfer 5 Spline | 8 | 8 | 0 | 0,2 s |
| Prüfer 6 | 30 | 9 | 0 | 2,4 s |
| Prüfer 7 | 24 | 12 | 0 | 0,2 s |
| Prüfer 8 Schraffur (cases, cases2) | 87 | 19 | 0 | 1,1 s |
| Prüfer 9 Schraffur (cases9) | 152 | 32 | **0**; Schraffur-Meldungen 4/4 wie erwartet | 1,2 s |
| Prüfer 9 Toleranz (tol) | 5 | 5 | 0 (r + 0,002 … 0,0099 formgleich) | 0,2 s |
| Prüfer 9 Riesenkreise (big, big3) | 3 | 3 | 0 | 3,2 s |

Keine neuen Befunde, nur die sechs erklärten.

**Leistung (Harness des Prüfers 9):**

| Fall | Lesen | Fräsbahnen gesamt | Abbrechen |
|---|---|---|---|
| perf_n8000_hatch (8000 Schraffur-Schleifen + 8000 Kreise) | 0,37 s (vorher 67,9 s) | 2,1 s (ohne Schraffur 1,6 s) | Lesen nach 0,2 s: 2 ms danach; Fräsbahn nach 0,05/0,2/0,6 s: ≤ 16 ms danach |
| perf_n4000_hatch_aus | 0,35 s | 1,1 s | – |
| Riesenkreis r 300 000 mit gleicher Schraffur | – | 0,77 s (vorher 54 s je Paar) | Lesen nach 0,2 s: 3 ms danach |
| 3 Riesenkreise | – | 1,9 s | – |

**Zusatzfälle des Regressionsprüfers** (selbst nachgebaut, Koordinaten auf 0,001 mm gerundet, damit die Dateien unter 64 MB bleiben; Harness `--lesen`):

| Fall | Lesen jetzt | gemeldet auf d3aa0f251 |
|---|---|---|
| h5: 10 000 Kreiszüge (130 Punkte) in nicht formgleicher Schraffur | 2,3 s | 150 s |
| h2: 10 000 formgleiche Paare (130 Punkte) | 3,4 s | 98 s |
| h2b: 1 300 Paare mit 1 000 Punkten | 3,3 s | 215 s |
| CIRCLE r 100 000 + Schraffur-Bogen 0…360 | 0,2 s | 56 s |
| h4: 100 fast gleiche Schraffuren gegen 100 Züge (1 000 Punkte) | 2,6 s | 802 s |

Abbrechen beim Lesen:
- h4: 43 ms bzw. 1 ms nach dem Abbruch.
- h2 (58 MB): 0,7–0,9 s nach dem Abbruch. Das Zerlegen des Textes in Zeilen läuft vor der ersten Prüfung.

Test `Fill_matching_worst_cases`:
- Kreis r 100 000: 0,22 s.
- 100 × 100 fast gleich: 2,6 s.
- Abbrechen: 0,36 s.

Test `Fill_matching_fast_and_cancellable`:
- 8000 Schleifen + 8000 Kreise: 0,43 s.
- 8000 formgleiche Paare: 0,45 s.
- Riesenkreis: 0,37 s.
- Abbrechen nach 0,1 s: 0,14 s.
- Grenzen lastrobust: 60 s bzw. 10 s.

**Rot-Probe gegen 07675b8f3:** `T0747_Fix9NeuntePruefung` 5 von 5 rot. Nur `ReadResult.HiddenFills` brauchte einen Platzhalter. Der Leistungstest lief dort 2 min, der Ergänzungstest wurde nach 2 min Stillstand abgebrochen. Log: `reports/T-0747/T-0747-fix9-rotprobe.log`.

**H-1 Randdaten hinter dem Abschnittsende – behoben, Grundsatz umgesetzt.** `HatchLoops` liest den HATCH-Körper als vollständige, strenge Grammatik:
- **Vorspann:** ohne Randgruppen (92/93/72/73/94–97).
- **Randabschnitt:** 91 ganzzahlig ≥ 0, dann genau so viele Schleifen.
  - Jede Schleife beginnt mit 92.
  - Polylinienrand: 72/73/93, dann n × 10/20 (42 nur bei gesetztem Ausbuchtungs-Flag).
  - Kantenrand: 93 ≥ 1 Kanten, jede mit ihren festen Gruppen. Linie 10/20/11/21; Bogen 10/20/40/50/51[/73]; Ellipse 10/20/11/21/40/50/51[/73]; Spline 94/73/74/95/96, Knoten, Kontrollpunkte [42], 97 Fit-Punkte, optional 12/22 und 13/23.
  - Danach 97 und genau so viele 330.
- **Nachspann:** im ganzen Rest nur erlaubte Gruppen. Das sind 75/76/52/41/77/78/53/43/44/45/46/79/49/47/98/10/20/30/63/421/102, 450–470 und 1000–1071.
- Jede Abweichung ergibt `null` (überall unsicher). Das gilt auch für ein zweites 91 und für Randdaten nach 75/76 bzw. nach den Saatpunkten.

**H-2 ausgeblendete Schraffur – behoben (echte Enthaltenseinsprüfung).**
- Eine Schleife auf ausgeblendeter Ebene zählt nur dann nicht, wenn ihre abgetastete Form ganz in einer gezeichneten Kontur liegt. Bedingungen: ein Punkt innen, und kein Stück der Schleife näher als 0,002 mm am Konturrand (Streckenraster). Dann kann sie die Kontur nicht verlassen.
- Bei der U-Platte überquert die Schraffur den Rand der Aussparung. Sie bleibt deshalb, und der Kreis darin wird ausgelassen.
- Ohne bestimmbare Form bleibt der Eintrag.

**H-3 assoziative Schraffur – behoben.** 330-Kennungen (z. B. „2A5“, „FF“) werden nur als Gruppe übersprungen und nicht als Zahl gelesen; Zusatzdaten 1000–1071 sind im Nachspann erlaubt.

**H-4 Leistung – behoben.**
- Rahmen je Kontur einmal vorberechnet; Vorfilter über ein Raster nach der linken unteren Ecke.
- Formvergleich über ein Streckenraster statt jeder gegen jeden, mit frühem Abbruch und Punktgrenze 400 000 (darüber: nicht formgleich, sicher).
- Feine Konturen (Bögen auf 0,0001 mm) werden nur für Elemente in Kandidaten-Rahmen neu gebaut.
- Abbruchprüfung in Formvergleich, Enthaltenseinsprüfung und Füllflächen-Schleife.

**N-1 – behoben.**
- `Redundant` zählt nur echte Formgleichheit. Eine nur enthaltene, ausgeblendete Schraffur wird weiter als „Nicht gelesen: 1× Schraffur“ gemeldet.
- Neu: „Füllflächen auf ausgeblendeten Ebenen … : 1× Schraffur auf Ebene AUS“ (`ReadResult.HiddenFills`).

**N-2 Toleranz – behoben.**
- Schraffur-Bögen und -Ellipsen werden auf 0,00001 mm abgetastet, die Vergleichskonturen auf 0,0001 mm.
- Verglichen wird mit 0,01 mm je Achse plus diesem Zerlegungsfehler (0,0102 mm).
- Ergebnis: Kreis r + 0,0099 ist formgleich, r + 0,011 nicht (tol-Fälle 0,002 … 0,0099 alle formgleich).

**N-3 – behoben.** Grad-1- und kollineare Spline-Kanten gelten nur bei nicht fallenden Knoten (Anzahl = Kontrollpunkte + Grad + 1, Enden geklemmt) als Zug.

**N-4 – behoben.** Ein Rand mit Stücken der Länge 0 mit Ausbuchtung oder lauter gleichen Punkten ist nicht bestimmbar und gilt als „überall unsicher“, mit Meldung statt Punkt-Rahmen.

**Tests:** `T0747_Fix9NeuntePruefung`:
- `Hatch_grammar`
- `Hidden_fill_real_containment`
- `Fill_matching_fast_and_cancellable`
- `Tolerance_knots_degenerate`
- `Fill_matching_worst_cases`

Rohlogs: `T-0747-fix9-*.log`.

**Weiterhin offen:**
- INSERT, SPLINE, MLINE und REGION werden nicht aufgelöst.
- Die sechs erklärten Prüfer-Fälle bleiben „rot“.

**Testzahlen T-0747-fix9:** Build 0 Fehler. Kern (alle zehn T0747-Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests): **136 gesamt, 135 bestanden, 1 übersprungen (vorbestehend „IFC echt“), 0 Fehler**. App: T0747_DxfUi 2/2, T0747_FixUi 3/3, T0747_Fix3RaceUi 4/4, T0747_Fix4RaceUi 3/3, SPEC01_Ui 6/6, SPEC01_UiArc 1/1, SPEC14_Ui 2/2, Bildbearbeitung_Ui 5/5, PlanPhoto_Ui 4/4, N23N24_CadPrintUi 3/3, UebersichtEinfach_Ui 2/2, DeadButtonTests 83/83 – **118/118**. Prüfskript BESTANDEN. Prüfprogramme: 678 Fälle, keine neuen Befunde (sechs erklärte). Belege der früheren Runden unverändert.

## Zehnte Prüfung behoben (T-0747-fix10)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix10`, Commit obendrauf auf b1dc530fa. Keine Maschine angesteuert.
**Prüfprogramme aller Prüfer** (nur als Kopie benutzt bzw. Fallgeneratoren und Prüfteile ausgeführt; Harness-Kopie des Prüfers 10 mit `--perf`, `--lesen`, `--abbruchlesen`, `--fix`; nichts im Repo):

| Fallgruppe | Fälle | mit G-Code | Befunde | Rechenzeit gesamt |
|---|---|---|---|---|
| Prüfer 2 | 162 | 141 | 1 (p_spline_block, erklärt) | 10,2 s |
| Prüfer 3 | 55 | 48 | 0 | 1,3 s |
| Prüfer 4 | 72 | 58 | 1 (v2, erklärt) | 0,8 s |
| Prüfer 5 | 80 | 72 | 4 (f6, g2, k1_0_0005, n8 – erklärt); Haltestege 4/4 richtig | 12,6 s (g2 50 000 Kreise 10,6 s) |
| Prüfer 5 Spline | 8 | 8 | 0 | 0,1 s |
| Prüfer 6 | 30 | 9 | 0 | 1,9 s |
| Prüfer 7 | 24 | 12 | 0 | 0,2 s |
| Prüfer 8 Schraffur | 87 | 19 | 0 | 0,4 s |
| Prüfer 9 (cases9, tol, big, big3) | 160 | 40 | 0; Schraffur-Meldungen 4/4 wie erwartet | 2,8 s |
| Prüfer 10 Schraffur (cases10, 10b, 10c, 10d) | 66 | 17 | **0** (auf b1dc530fa: 8 – a_runde_ecken_kantenrand_cw, c_bogen_r_0, c_ellipse_ratio_0, c_normale_schraeg, b2_cw_ergaenzungsbogen_aus, i_insel_kontur_in_schraffur, j_spitze_0_0019, j_spitze_m0_001) | 0,7 s |

Keine neuen Befunde, nur die sechs erklärten.

**Leistung (Harness des Prüfers 10, `--lesen` = Einlesen inklusive Füllflächen-Vergleich):**

| Fall | vorher (b1dc530fa, laut Prüfung) | jetzt |
|---|---|---|
| q2: Kontur 300 000 Punkte + 200 ausgeblendete Schraffuren | 16 s | 0,50 s |
| q2: Kontur 300 000 Punkte + 2000 ausgeblendete Schraffuren | > 120 s, 110 GB alloziert | 0,69 s (gesamt mit Fräsbahn 1,6 s, 0,85 GB) |
| q2: Kontur 30 000 Punkte + 20 000 ausgeblendete | – | 1,3 s |
| q7: Sägezahn 362 054 Punkte + 50 ausgeblendete | > 120 s | 0,73 s |
| q7: Sägezahn formgleich | 16 s | 1,1 s |
| q7: Abbrechen beim Lesen | 5,6 s | 3–124 ms nach dem Abbruch |
| q4: 2000 Kreise r 1000 formgleich | – | 1,0 s (zwischendurch 20,8 s, siehe Vollkreis-Vergleich) |
| q3: 3 × 300 000 formgleich | – | 2,1 s |
| q9: 20 000 Schleifen | – | 3,3 s |
| pruef9 perf_n8000_hatch / perf_n4000_hatch_aus | 0,37 / 0,35 s | 0,36 / 0,32 s |

Belegdateien (`--fix`): alle 12 Ausgaben (4 G-Codes der Referenzdateien, Job-G-Codes, Hinweise) sind hash-gleich mit dem Vorstand.

**Rot-Probe gegen b1dc530fa:** `T0747_Fix10ZehntePruefung` 6 von 6 rot. Nur `ReadResult.HiddenFillKinds` brauchte einen Platzhalter. Der Leistungstest lief dort 1 min 47 s. Log: `reports/T-0747/T-0747-fix10-rotprobe.log`.

**HOCH 1 Bögen/Ellipsen im Uhrzeigersinn – behoben.**
- HATCH-Kanten mit 73 = 0 werden wie bei AutoCAD/ezdxf gelesen. Bei Bögen gilt: echter Start 360° − a0, Bogen −((a1 − a0) mod 360°, bei 0 volle 360°). Ellipsenparameter gelten ebenso.
- Ezdxf ist hier nicht installiert. Geprüft habe ich deshalb gegen die Fälle des Prüfers (gebaut wie ezdxf `ArcEdge`), nicht gegen eine echte ezdxf-Datei.
- Ältere Prüferfälle (p8 a02/a03) schreiben Uhrzeigerbögen wörtlich bzw. Ellipsenparameter im Bogenmaß. Deshalb ist der Rahmen vorsichtig: bei Bögen im Uhrzeigersinn der Vollkreis, bei Ellipsenkanten die volle Ellipse.
- Die abgetastete Form dient bei solchen Schleifen nur dem Formvergleich („mehrdeutig“). Für das Weglassen ausgeblendeter Schraffuren wird sie nicht verwendet. Die Kandidaten für den Formvergleich kommen aus dem Rahmen der Form.
- **Zusammenhang:** Ende einer Kante = Anfang der nächsten (je Achse ± tol), sonst keine Form.
- **Ungültig:** Bogen-Radius ≤ 0, Ellipsen-Verhältnis ≤ 0 oder > 1. Eine HATCH mit schräger Normale gilt als „überall unsicher“.
- Tests: `Cw_arc_edges_complement` (runde Ecken im/gegen den Uhrzeigersinn formgleich, G-Code mit Löchern innen), `Cw_arc_into_notch` (b2), `Connectivity_and_invalid`.

**HOCH 2 Leistung – behoben.**
- Raster und Punkt-im-Zug-Index (waagrechte Streifen, Größe an die Kontur angepasst) werden je grober Kontur einmal und erst bei Bedarf gebaut, ebenso das Raster je feiner Vergleichskontur.
- Vollkreise vergleiche ich analytisch mit gezeichneten Kreisen: |Δx| + |Δr| und |Δy| + |Δr| ≤ Toleranz genügt.
- Schraffur-Bögen werden auf tol/100 abgetastet; der Zerlegungsfehler ist eingerechnet.
- Test `Hidden_fills_in_huge_contour`: 300 000 Punkte + 2000 ausgeblendet 0,7 s; Sägezahn 0,46 s; Grenze 10 s, einmal wiederholt.

**MITTEL Abbruch – behoben.** `Grid()` und der Index prüfen den Token alle 1024 Strecken. Lange Strecken werden in Stücke von höchstens einer Zelle geteilt, damit eine schräge Strecke nicht sehr viele Zellen belegt.

**Ausgeblendete Schraffur (Prüfer-10-Fälle i_insel, j_spitze) – mit behoben.**
- Weggelassen wird sie, wenn die dicht abgetastete Form (Abstand ≤ 0,01 mm) innen liegt oder höchstens 0,01 mm vom Konturrand. Das ist die gleiche Toleranz wie beim Formvergleich: Spitzen bis 0,01 mm am oder über dem Rand sind erlaubt.
- Zusätzlich darf in der Schraffur keine gezeichnete Kontur liegen, die selbst wieder etwas enthält (z. B. Loch mit Teil). Sonst änderte ein Umriss der Schraffur die Rolle des Inneren.
- Test `Hidden_fill_nested_and_tolerance`.

**App-Test – behoben.**
- `Studio3DMeshEditUiTests.Cad_dxf_import_extrude_export_recover` erwartet jetzt „1× Bemaßung“.
- Gesucht habe ich nach englischen Typnamen in Meldungen: Füllflächen hießen in den Meldungen „Füllfläche HATCH“. Jetzt heißen sie wie in der Tabelle `KindLabel` (Schraffur, Abdeckfläche …); die sechs Testerwartungen sind angepasst.

**NIEDRIG – behoben.**
- Ausgeblendete Füllflächen werden einmal gemeldet, bei ihrer Ebene und mitgezählt: „Ausgeblendete Ebenen nicht gefräst: AUS (2, davon 1× Schraffur)“. Unter „Nicht gelesen“ erscheinen sie nicht mehr (`HiddenFillKinds`, `HiddenFillLayers`; Test `Hidden_fill_note_once`).
- Fix9-Zeittests: Grenze 10 s mit einer Wiederholung. Abbrechen lastrobust: ein schon abgebrochener Token wirft sofort; der Abbruch kommt nach einem Drittel der gemessenen Lesezeit, sonst mit 1 ms wiederholt.
- Kommentar zu 100 × 100 fast gleich korrigiert.

**Tests:** `T0747_Fix10ZehntePruefung`:
- `Cw_arc_edges_complement`
- `Cw_arc_into_notch`
- `Connectivity_and_invalid`
- `Hidden_fill_nested_and_tolerance`
- `Hidden_fills_in_huge_contour`
- `Hidden_fill_note_once`

Rohlogs: `T-0747-fix10-*.log`.

**Weiterhin offen (nicht Fix10, Vorstand):**
- CAM-Hänger in `CncCam.Generate` bei q5/q5b (riesige Kontur neben vielen winzigen) und q6/q6b (Streifen mit winzigen Konturen): Abbruch durch die 120-s-Grenze des Harness.
- q4/q4b: 243 ausgelassene Kreise („Versatz ergab keine Bahn“), CAM 41 s.
- INSERT, SPLINE, MLINE und REGION werden nicht aufgelöst.
- Die sechs erklärten Prüfer-Fälle bleiben „rot“.

**Testzahlen T-0747-fix10:** Build 0 Fehler. Kern (alle elf T0747-Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests): **142 gesamt, 141 bestanden, 1 übersprungen (vorbestehend „IFC echt“), 0 Fehler**. App: T0747_DxfUi 2/2, T0747_FixUi 3/3, T0747_Fix3RaceUi 4/4, T0747_Fix4RaceUi 3/3, SPEC01_Ui 6/6, SPEC01_UiArc 1/1, SPEC14_Ui 2/2, Bildbearbeitung_Ui 5/5, PlanPhoto_Ui 4/4, N23N24_CadPrintUi 3/3, UebersichtEinfach_Ui 2/2, DeadButtonTests 83/83, Studio3DMeshEditUiTests 5/5 – **123/123**. Prüfskript BESTANDEN. Prüfprogramme: 744 Fälle, keine neuen Befunde (sechs erklärte). Belege der früheren Runden unverändert.

## Elfte Prüfung behoben (T-0747-fix11)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix11`, Commit obendrauf auf f6b58a065. Keine Maschine angesteuert.

**Ursachenklasse statt Einzelfälle.** Alle Befunde dieser Runde kommen aus zwei Klassen:
1. Das Weglassen ausgeblendeter Schraffuren war zu großzügig (HOCH A) bzw. hatte kein Arbeitsbudget (HOCH B, MITTEL C).
2. Schleifen über die Datenmenge liefen ohne Abbruch oder Grenze (MITTEL D, E).

Entscheidung (einfacher und sicher): Eine ausgeblendete Schraffur wird nur noch im **eindeutigen Fall** verworfen, sonst bleibt sie stehen und wird gemeldet. Eindeutig heißt:
- Die Schleife ist nicht mehrdeutig (keine Uhrzeiger-/Ellipsenkanten) und hat höchstens 20 000 Punkte.
- Unter den 4 kleinsten Konturen, deren Rahmen sie enthalten, ist eine, in der alle Eckpunkte innen oder ≤ 0,01 mm am Rand liegen. Stücke, die den Rand kreuzen, liegen nur in der Toleranz außen; geprüft wird über einen Kanten-Schnitttest, nicht durch dichtes Abtasten.
- Keine andere gezeichnete Kontur, deren Rahmen in der Schraffur liegt, enthält selbst wieder etwas.
- Über einem harten Arbeitsbudget (30 Mio. Schritte) wird nichts mehr verworfen.

Jede Schleife über die Datenmenge prüft den Abbruch.

**Prüfprogramme aller Prüfer** (nur als Kopie benutzt bzw. Fallgeneratoren und Prüfteile ausgeführt; Harness-Kopie des Prüfers 11; nichts im Repo). Die Einheiten-Fälle (cases11b, cez1/5/6) wurden mit in mm umgerechneten Erwartungen geprüft, weil der G-Code immer in mm ist:

| Fallgruppe | Fälle | mit G-Code | Befunde | Rechenzeit gesamt |
|---|---|---|---|---|
| Prüfer 2 | 162 | 141 | 1 (p_spline_block, erklärt) | 10,8 s |
| Prüfer 3 | 55 | 48 | 0 | 1,6 s |
| Prüfer 4 | 72 | 58 | 1 (v2, erklärt) | 1,1 s |
| Prüfer 5 | 80 | 72 | 4 (f6, g2, k1_0_0005, n8 – erklärt); Haltestege 4/4 richtig | 13,4 s (g2 50 000 Kreise 10,9 s) |
| Prüfer 5 Spline | 8 | 8 | 0 | 0,1 s |
| Prüfer 6 | 30 | 9 | 0 | 2,0 s |
| Prüfer 7 | 24 | 12 | 0 | 0,3 s |
| Prüfer 8 | 87 | 19 | 0 | 0,4 s |
| Prüfer 9 (cases9, tol, big, big3) | 160 | 40 | 0 | 2,7 s |
| Prüfer 10 | 66 | 17 | 0 | 0,7 s |
| Prüfer 11 (cases11, 11b, 11c) | 80 | 22 | 1 (g_aus_ueber_rand_0p011, sicher, siehe offen); im Lauf des Prüfers auf f6b58a065: 16 | 0,6 s |
| Prüfer 11 ezdxf (casesez, cez1/5/6) | 44 | 40 | 4 (e_linse_cw_ezdxf_linse_gezeichnet in allen vier Einheiten-Varianten, bewusst, siehe MITTEL F) | 3,5 s |

Keine neuen Befunde außer den sechs erklärten und den zwei bewusst sicheren (kein Loch wird außen gefräst).

**Leistung (Harness des Prüfers 11; Lesen = Einlesen inklusive Füllflächen-Vergleich):**

| Fall | vorher (f6b58a065, laut Prüfung) | jetzt Lesen | jetzt gesamt mit Fräsbahn |
|---|---|---|---|
| n1 Cache 200 × 5000 / 25 × 50 000 | – | 1,5 / 1,7 s | 3,5 / 3,0 s |
| n2 Kamm 50 000 Zähne + 20 versteckt | > 900 s | 0,39 s | 97 s (CAM, offen) |
| n2 Kamm 100 000 Zähne + 50 versteckt | – | 0,54 s | > 120 s (CAM, offen) |
| n3 60 000 + 60 000 Kreise | Abbruch-Nachlauf 10,2 s | 1,7 s, Abbruch 0,1–0,3 s danach | 16 s |
| n3b Vollkreis-Kanten 5000 | – | 0,7 s | 4,5 s |
| n4 / n4b r 1e7 (bis 1000) | 2,1 GB bzw. Speicherüberlauf | ≤ 0,06 s | ≤ 0,25 s |
| n5 500 große versteckt | 33 s, 38 GB | 0,12 s | 0,19 s |
| n6 40 000 Löcher + 2000 versteckt | > 120 s | 1,0 s | – |
| n7 fast gleich | – | 1,4 s | 1,9 s |
| n8 Zickzack 100 000 formgleich | – | 6,5 s | 43,8 s (CAM, offen) |
| q2 / q7 / q3 / q9 (zehnte Prüfung) | 0,5–3,3 s | 0,57–1,6 s | 0,9–5,5 s |
| pruef9 perf_n8000_hatch | 0,36 s | 0,42 s | 3,5 s |

Abbruch der Fräsbahn bei der CAM-Laufzeit (Harness `--abbruch`):
- Kamm: 1–63 ms Nachlauf in der Lesephase, aber bis 5,5 s mitten in `CncCam.Generate` (offen).
- n8: höchstens 67 ms.

Belege (`--fix`): alle 12 Ausgaben hash-gleich mit dem Vorstand, darunter die 4 Referenz-G-Codes.

**Rot-Probe gegen f6b58a065:** `T0747_Fix11ElftePruefung` 6 von 6 rot (für das Übersetzen nur ein Platzhalter für `CadEntity.Invisible`; der Test D/E lief dort 1 min) – `reports/T-0747/T-0747-fix11-rotprobe.log`.

**HOCH A – behoben.** Als innere Kandidaten gelten alle Konturen, deren Rahmen im Schraffurrahmen ± 0,01 mm liegt; ausgenommen ist nur die bestätigte umschließende Kontur. Ein Punkt-im-Zug-Vorfilter entfällt. Damit bleibt die Schraffur bei Inkreis (gap 0 / 0,005 / 0,009) und Raute mit gleichem Rahmen stehen. Test `Hidden_fill_inner_same_box`.

**HOCH B – behoben.** Der Punkt-im-Zug-Index meldet einen Streifen mit mehr als 200 000 Strecken als „unbekannt“; dann wird nicht verworfen. Jede Abfrage geht ins Arbeitsbudget. Kamm mit 50 000 Zähnen: Lesen 0,39 s (vorher > 900 s).

**MITTEL C – behoben.**
- Enthaltensein über Eckpunkte plus Kanten-Schnitttest statt dichter Abtastung.
- „Enthält etwas“ wird je Kontur einmal berechnet; gleiche ausgeblendete Schleifen werden nur einmal entschieden.
- Ergebnis: n5 mit 500 großen Rechtecken 0,12 s (vorher 33 s, 38 GB); n6 mit 40 000 Löchern + 2000 versteckt 1,0 s (vorher > 120 s).

**MITTEL D – behoben.**
- Vollkreise kommen in Eimer nach (x, y, r) im Toleranzschritt; gleiche Kreise nur einmal; Abbruch alle 1024.
- Der Rahmen-Index läuft über alle vier Grenzen und nimmt gleiche Konturen nur einmal auf; gleiche Schraffur-Schleifen werden einmal verglichen.
- Ergebnis: n3 mit 60 000 + 60 000 liest in 1,7 s, Abbruch 0,1–0,3 s danach (vorher 10,2 s).

**MITTEL E – behoben.**
- Abtastpunkte werden vorher gezählt. Grenze je Schleife 400 000, je Datei 4 000 000; darüber keine Form, der Rahmen bleibt.
- Abbruch in `HatchLoops`.
- Dabei gefunden und behoben (Ursachenklasse):
  - `Ocs.Of` lief je Schleife, `IntSafe(…, 60)` je Füllschleife über den ganzen HATCH-Körper (60 000 Schleifen: 75 s → 0,8 s).
  - Vollkreise werden auch ohne abgetastete Form analytisch verglichen.
- Ergebnis: n4b mit 1000 Ausbuchtungen r 1e7 liest in 0,06 s (vorher Speicherüberlauf).

**MITTEL F – behoben.** Bei Uhrzeiger-Kanten wird auch die wörtliche Lesart verkettet. Hängen beide Lesarten zusammen und ergeben verschiedene Formen, gibt es keine Form, also auch keine Formgleichheit; die Schraffur bleibt. Test `Cw_literal_lens_vs_peanut`.
- Bewusste Folge: Die echte ezdxf-Linse (Prüfer-Fall `e_linse_cw_ezdxf_linse_gezeichnet`) ist geometrisch nicht von der wörtlich geschriebenen zu unterscheiden. Sie liefert deshalb keinen G-Code; das ist die sichere Seite.

**MITTEL G – behoben.** Gezeichnete ARC gehen mit ihrem echten Bogenrahmen (Endpunkte + Scheitel im Winkelbereich) in die Kandidatenwahl ein. Flache Bogenkante r 200 ist formgleich (Test `Arc_box_and_units`).

**MITTEL H – behoben.** Der Formvergleich rechnet in mm: Elemente werden als Kopien skaliert, ebenso Formen und Rahmen. Die 0,05-mm-Untergrenze der Bogenzerlegung gilt damit in mm statt in Zeichnungseinheiten. Runde Ecken in m, cm und mm sind formgleich.

**MITTEL I – behoben.** Abbruch in den Tests lastrobust: ein eigener Thread bricht ab, sobald das Lesen startet (ManualResetEvent), bis zu drei Versuche. Die Ausnahme wird wirklich verlangt (auch in Fix10 `Hidden_fills_in_huge_contour`). Der volle Kernlauf ist zweimal hintereinander grün (siehe Testzahlen).

**NIEDRIG – behoben.**
- Gruppe 60 = 1 (unsichtbar) wird wie eine ausgeblendete Ebene behandelt: nicht gefräst, nicht in den Grundriss übernommen, gemeldet als „(unsichtbar) (n, …)“.
- Formgleiche ausgeblendete Schraffuren zählen bei ihrer Ebene mit.
- Die Ebenenmeldung sagt, wenn ausgeblendete Füllflächen außerhalb gezeichneter Konturen stehen bleiben: „– n Füllfläche(n) außerhalb gezeichneter Konturen: Teile darin werden ausgelassen“.
- Strenger Nachspann: Punkte 10/20 nur als Saatpunkte direkt nach 98 und genau so viele (Fall `c_saatpunkte_mehr_als_98`).
- 2-Punkt-Rand mit Ausbuchtungen verschiedenen Vorzeichens gilt als nicht eindeutig (Fall `c_poly_2pkt_bulge_pm`).
- Test `Invisible_and_notes`.

**Weiterhin offen:**
- CAM-Laufzeit (`CncCam.Generate`, Vorstand):
  - Kamm 50 000 Zähne: Fräsbahnen 97 s, Abbruch greift dort erst nach bis zu 5,5 s.
  - Kamm 100 000: über 120 s.
  - n8 Zickzack 100 000: Fräsbahnen 43 s.
  - q4 / q5 / q6: wie in der zehnten Prüfung.
- `g_aus_ueber_rand_0p011`: Die Schraffur ragt 0,011 mm über den Rand, mehr als die Toleranz von 0,01 mm. Sie bleibt deshalb stehen und das Loch wird nicht gefräst (sichere Seite).
- `e_linse_cw_ezdxf_linse_gezeichnet` (siehe MITTEL F).
- INSERT, SPLINE, MLINE und REGION werden nicht aufgelöst.
- Die sechs erklärten Prüfer-Fälle bleiben „rot“.

**Testzahlen T-0747-fix11:** Build 0 Fehler. Kern (alle zwölf T0747-Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests) **zweimal hintereinander**: je 148 gesamt, 147 bestanden, 1 übersprungen (vorbestehend „IFC echt“), 0 Fehler. App: T0747_DxfUi 2/2, T0747_FixUi 3/3, T0747_Fix3RaceUi 4/4, T0747_Fix4RaceUi 3/3, SPEC01_Ui 6/6, SPEC01_UiArc 1/1, SPEC14_Ui 2/2, Bildbearbeitung_Ui 5/5, PlanPhoto_Ui 4/4, N23N24_CadPrintUi 3/3, UebersichtEinfach_Ui 2/2, DeadButtonTests 83/83, Studio3DMeshEditUiTests 5/5 – **123/123**. Prüfskript BESTANDEN. Prüfprogramme: 868 Fälle.

## Zwölfte Prüfung behoben (T-0747-fix12)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix12`, Commit obendrauf auf 671fd5dc7. Keine Maschine angesteuert.

**HOCH 1 – behoben.** Gleiche Schleifen werden nur noch bei wirklich gleicher Punktfolge zusammengefasst: Schlüssel (Rahmen, Punktzahl, Prüfsumme über alle Punkte), danach Punkt für Punkt (`SequenceEqual`). Dasselbe gilt für die Entscheidungen über ausgeblendete Schraffuren (`decided`) und den Konturen-Index (dort über ein Wörterbuch statt „jeder gegen jeden“ in der Zelle). Tests `Same_first_point_not_same_shape` (T1 bleibt, T2 fällt, in beiden Reihenfolgen) und `Hidden_decided_by_full_shape` (L-Platte).

**HOCH 2 und Tiefe – behoben, neue einfache Regel.** Eine ausgeblendete Schraffur fällt nur noch weg, wenn
- (a) sie in einer gezeichneten Kontur E liegt, die selbst auf gerader Tiefe liegt (Teil, kein Loch), und
- (b) außer E keine gezeichnete Kontur mit einem ihrer Stücke in den Schraffurrahmen ± 0,01 mm hineinreicht (Stückrahmen gegen Schraffurrahmen) – egal, ob diese Kontur Inhalt hat.

Warum dadurch keine Kontur ihre Rolle wechseln kann: Die Rolle (Loch/Teil) einer Kontur hängt nur davon ab, wie viele Umrisse sie umschließen. Nach (b) gibt es in der Fläche der Schraffur keine Linie außer E; keine Kontur liegt darin, schneidet sie oder berührt sie. Jede andere Kontur liegt also ganz außerhalb oder umschließt die Schraffur samt E, ohne sie zu berühren. Weglassen ändert damit an keiner Zählung etwas, und E selbst ist nach (a) ein Teil. Kurz: nur eine leere Schraffur in einem Teil darf wegfallen. Unklare Fälle (Arbeitsbudget erschöpft, mehr als 4096 Kandidaten, Punkt-im-Zug unbekannt) bleiben stehen. Die Prüfung (b) bricht beim zweiten Treffer ab; bei sehr großen Schraffurrahmen wird die Kontur Stück für Stück durchgegangen statt über Milliarden Rasterzellen.

Tests `Hidden_fill_kept_when_touched` (Raute schneidet mit/ohne Loch, Kreis darin leer, Kreis berührt den Rand → bleibt; leer im Teil → fällt) und `Hidden_fill_in_hole_kept` (Schraffur in einem Loch bleibt; Teil im Loch auf Tiefe 2 → fällt).

**G-Code-Kosten der strengeren Regel** (alle auf der sicheren Seite: die Schraffur bleibt, Löcher darin werden nicht gefräst, nie wird etwas außen gefräst):

| Prüfer | Fall | Folge |
|---|---|---|
| 8 | c13_hatch_ausgeblendet, c14_hatch_gefroren | zwei Löcher r 5 unter der ausgeblendeten Schraffur nicht gefräst |
| 9 | d_platte_hatch_aus_ueber_loechern, d_platte_hatch_aus_mit_insel_um_loch | ebenso |
| 10 | j_spitze_0_0019, j_spitze_0_0021, j_spitze_m0_001 | ebenso |
| 11 | g_aus_ueber_rand_0p0019 / 0p0021 / 0p009 / 0p011 | Loch r 5 nicht gefräst |

Die Alltagsfälle der Prüfer 8–12 ohne Löcher unter der Schraffur und alle ezdxf-Fälle außer der Linse bleiben unverändert grün.

**MITTEL Leistung – behoben.**
- Streckenraster: Zellgröße aus dem Median der Stücklängen, lange Stücke geteilt, aber höchstens etwa 1 Mio. Teilstücke je Zug (eine Strecke von 1e8 mm belegte vorher Milliarden Zellen). Darüber wird die Zelle gröber.
- Schrittbudget in `Covered` (10 Mio. Schritte) → darüber „nicht formgleich“, die Schraffur bleibt (sicher).
- Formvergleich mit vollständiger Vorauswahl: Formgleich verlangt, dass jeder Eckpunkt und jede Stückmitte höchstens 0,01 mm von der Kontur liegt. Ein gemeinsames Raster aller Kandidaten-Konturen zeigt an einigen Eckpunkten und Stückmitten, welche Konturen dort vorbeigehen; der Punkt mit den wenigsten gibt die Kandidaten (keine echte Übereinstimmung fällt dabei heraus). Mehr als 256 Kandidaten an jedem Punkt, Such- oder Vergleichsbudget erschöpft → „nicht formgleich“.
- Rahmen der Kandidaten-Flächen nur einmal je Rahmen; Auswahl der Elemente mit Abbruch beim ersten Treffer.

**MITTEL Tests – behoben.** Abbruch mitten im Abgleich: Abbruch nach der halben gemessenen Laufzeit, die Ausnahme wird verlangt; ist ein Lauf ohne Fremdlast vorher fertig, wird mit der halben Zeit dieses Laufs wiederholt (bis zu fünf Versuche). Riesenzelle 100 000 Punkte < 3 s mit Wiederholung (`Big_cell_and_same_box_fast`).

**NIEDRIG – behoben.**
- CS8321 (`UB`) entfernt; in den T-0747-Dateien keine Warnung.
- Meldetext „– N Füllfläche(n) nicht sicher zuzuordnen: Teile darin werden ausgelassen“.
- `CadDxf.Write` schreibt unsichtbare Elemente mit 60 = 1 (Rundreise-Test `Write_keeps_invisible`).
- Zeittests in Fix2 (Zickzack) und Fix (10 000 Linien) unter Fremdlast einmal wiederholt, die bessere Zeit zählt.
- Kreis-Eimer nach r sortieren (optional): nicht gemacht – die Eimer-Fälle e0/e1/e2 lesen 100 000 × 100 000 in 3–4 s.

**Prüfprogramme aller Prüfer** (nur gelesen bzw. Generatoren und Prüfteile als Kopie ausgeführt; Harness-Kopie des Prüfers 12; nichts im Repo; Einheiten-Fälle cases11b, cez1/5/6 mit in mm umgerechneten Erwartungen): **900 Fälle, 23 mit Befund** – alle bekannt und erklärt:

| Fallgruppe | Fälle | Befunde |
|---|---|---|
| Prüfer 2 (cases, 2, 3) | 162 | 1 (p_spline_block, wie bisher) |
| Prüfer 3 | 55 | 0 |
| Prüfer 4 | 72 | 1 (v2, wie bisher) |
| Prüfer 5 / Spline | 80 / 8 | 4 (wie bisher); Haltestege 4/4 richtig |
| Prüfer 6 / 7 | 30 / 24 | 0 |
| Prüfer 8 | 87 | 2 (c13, c14 – Kosten der strengen Regel) |
| Prüfer 9 (cases9, tol, big, big3) | 160 | 2 (d_platte_… – Kosten der strengen Regel) |
| Prüfer 10 | 66 | 3 (j_spitze_… – Kosten der strengen Regel) |
| Prüfer 11 (11, 11b, 11c) | 80 | 4 (g_aus_ueber_rand_… – Kosten der strengen Regel) |
| Prüfer 11 ezdxf (ez, cez1/5/6) | 44 | 4 (e_linse_cw_ezdxf, bewusst) |
| Prüfer 12 (cases12, 12b) | 32 | 2 (v5_platte60_loch_sichtbar, t4_umriss_ausgeblendete_ebene_loch_sichtbar – siehe Verhalten) |

**Verhalten (bewusst, wie besprochen):** Ein ausgeblendeter oder unsichtbarer (60 = 1) Plattenumriss mit sichtbarem Kreis wird wie eine ausgeblendete Ebene behandelt: Der Umriss zählt nicht, der Kreis ist dann ein eigenes Teil und wird außen gefräst.

**Leistung (Harness des Prüfers 12, Lesen = Einlesen inklusive Füllflächen-Vergleich):**

| Fall | vorher* | jetzt Lesen | Ergebnis |
|---|---|---|---|
| b1 gleicher Rahmen 5000 × 5000 | 8,2 s | 1,5 s | 182 nicht gelesen (wie vorher) |
| b1 20 000 × 2000 | 16,0 s | 1,8 s | 73 nicht gelesen (wie vorher) |
| b1 30 000 × 30 000 | > 200 s | 4,9 s | 21 132 nicht gelesen (Budget, sicher) |
| b2 versteckt 20 000 / 30 000 | 21 / 38 s | 1,6 / 1,8 s | alle bleiben (Konturen berühren) |
| d1 Riesenzelle 20 000 / 100 000 / 399 000 | – / 90 s / > 120 s | 1,1 / 1,3 / 2,2 s | nicht formgleich (Budget, sicher) |
| d2 Riesenzelle versteckt 100 000 / 399 000 | – | 0,7 / 1,1 s | bleibt |
| d3 Ornament 100 000, Kante 10 000 / 1 000 000 | – | 1,4 / 1,4 s | formgleich / bleibt (Budget) |
| e0 / e1 / e2 Kreise 100 000 × 100 000 | – | 3,4 / 4,1 / 3,1 s | wie erwartet |
| k1 Budget-Kombi je2 / je6 | – | 2,0 / 4,1 s | bleibt |
| k2 Kamm 199 999 × 4 versteckt | – | 1,2 s | 730 bleiben |
| n5 500 große versteckt | 0,12 s | 0,12 s | – |
| n6 40 000 Löcher + 2000 versteckt | 1,0 s | 1,0 s | – |

\* d1: laut Prüfung auf 671fd5dc7; b1/b2: erster Fix12-Zwischenstand (nur HOCH 1/2 umgesetzt, Formvergleich noch ohne Vorauswahl); n5/n6: Stand fix11.

Abbruch beim Lesen (Harness `--abbruchlesen`): d1 100 000 höchstens 85 ms, b1 30 000 höchstens 242 ms, b2 30 000 höchstens 207 ms, k2 höchstens 270 ms nach dem Abbrechen.

Belege (`--fix`): alle 12 Ausgaben hash-gleich mit dem Vorstand (fix_alt des Prüfers 12).

**Rot-Probe gegen 671fd5dc7:** 8 von 23 rot (alle 6 neuen Fix12-Tests sowie die angepassten Fix9 H-2 und Fix11 „unsichtbar“) – `reports/T-0747/T-0747-fix12-rotprobe.log`.

**Weiterhin offen (nicht in Fix12):**
- CAM-Laufzeit und Abbruch in `CncCam.Generate`: Kamm 50 000 Zähne 96 s, Kamm 100 000 über 120 s; mit Fräsbahn außerdem b1/b2 (80–150 s), e0/e1 100 000 Kreise (59–126 s).
- INSERT, SPLINE, MLINE und REGION werden nicht aufgelöst.
- Die erklärten Prüfer-Fälle bleiben „rot“.

**Testzahlen T-0747-fix12:** Build 0 Fehler, keine Warnung in T-0747-Dateien. Kern (alle dreizehn T0747-Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests) **zweimal hintereinander**: je 154 gesamt, 153 bestanden, 1 übersprungen (vorbestehend „IFC echt“), 0 Fehler. App: 13 Klassen inklusive Studio3DMeshEditUiTests – **123/123**. Prüfskript BESTANDEN.

## Dreizehnte Prüfung behoben (T-0747-fix13)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix13`, Commit obendrauf auf fc2a91ec6. Keine Maschine angesteuert.

**HOCH 1 – behoben (Linien-Stöße).** Beim Verketten für das Fräsen (und für den Füllflächen-Abgleich) werden zuerst Knoten der Linienenden gebildet (gleiche Ebene, ≤ 1 µm). Treffen sich an einem Knoten mehr als zwei verschiedene Stücke, wird die ganze zusammenhängende Gruppe nicht gierig verkettet. Ihre Stücke kommen offen zurück und tragen dieselbe Gruppennummer. Doppelt deckungsgleich gezeichnete Stücke zählen dabei einmal (die doppelte Platte aus 8 LINE bleibt richtig). Liegen an einem Punkt zu viele Enden (über 64 zu prüfen), gilt er vorsichtshalber als Stoß.

Beim Fräsen wird jede geschlossene Kontur im Rahmen einer solchen Gruppe ausgelassen und gemeldet („liegt im Linienverbund …, an dem mehr als zwei Linien zusammenstoßen“). Ein Umriss aus der Gruppe kann nur Konturen in ihrem Rahmen umschließen; Konturen außerhalb behalten ihre Rolle. Der Wohnungsplaner verkettet wie bisher. Test `T_junction_never_hole_outside` mit fünf Reihenfolgen, Ausläufer auf eigener Ebene (z4) und Rechteck ohne Ausläufer als Kontrolle.

**MITTEL 2 – behoben (Punktgruppen streng).** Alle `Pairs()`-Nutzer sind ersetzt:
- Punkte, die genau einmal vorkommen (LINE, SOLID/TRACE, 3DFACE, OLE2FRAME, POLYLINE-Eckpunkt, Mittelpunkt von CIRCLE/ARC): Jeder Code und sein y genau einmal; die Reihenfolge ist nach DXF frei und eindeutig. Fehlt ein Wert oder ist er doppelt, gilt das Element als nicht bestimmbar (überall unsicher).
- Wiederholte Punkte (SPLINE-Kontrollpunkte, WIPEOUT 14/24) und LWPOLYLINE: Jedes x muss direkt von seinem y gefolgt sein, ein y ohne x ist ein Fehler.
- Eine LINE mit fehlender y-Gruppe wird nicht mehr still mit y = 0 gezeichnet, sondern gemeldet.
- SOLID mit weniger als vier Punkten ergibt nicht mehr still eine leere Liste.
- Test `Strict_point_groups`.

**HOCH 3 – behoben (Formvergleich-Budget).**
- Ein gemeinsames Schrittbudget je Datei (100 Mio.) für Raster, Kandidatensuche und alle `Covered`-Aufrufe.
- Vorabtest an 16 gleichmäßig verteilten Punkten (und dem letzten) beider Züge vor dem vollen Vergleich.
- Über dem Budget gilt „nicht formgleich“, die Schraffur bleibt.
- s1-Kämme 2,3–3,4 s statt 77 s bis > 150 s.

**MITTEL 4/5 – behoben (Rahmen-Index).** Das Rahmen-Raster der ausgeblendeten Schraffuren ist durch einen statischen R-Baum ersetzt: 16 Einträge je Knoten, sortiert nach Mittelpunkt in Streifen. Große Rahmen werden nicht mehr flächig eingetragen. Jeder besuchte Knoten und Eintrag geht ins Arbeitsbudget; die Kandidatensuche zählt mit und bricht bei 4096 ab, statt erst eine Liste zu bauen. Ergebnisse:
- c1 (1000 große + 60 000 winzige): 0,8 s statt 19 s.
- w1 100 000: 4,6 s statt 36 s, Speicher 1,3 GB statt 82 GB angefordert.
- l1 (lange dünne Rahmen): 0,8 s, alle 20 000 versteckten Schraffuren richtig verworfen.

**NIEDRIG 6 – behoben.** Das gemeinsame Raster des Formvergleichs wird erst gebaut, wenn eine Schleife mehr als 16 Kandidaten mit gleichem Rahmen hat. Sonst werden die Kandidaten direkt aus dem Rahmen-Index genommen.
- q9: 1,2 s / 0,5 GB statt 4,5 s / 1,1 GB.
- k1: 2,0 s / 1,8 GB statt 4,2 s / 3,9 GB Spitze.

**MITTEL 7 – behoben.**
- Die Zeittests der Klassen Fix12 und Fix13 laufen in einer nicht parallelen Sammlung; die Grenzen liegen bei 10 s.
- Fix10 `Hidden_fill_nested_and_tolerance` prüft wieder `Assert.Empty(Unread)`, dazu die Gegenprobe d = −0,011 → `Single(Unread)`.
- Der vorgeschriebene Kernfilter ist zweimal grün.

**NIEDRIG 8 – behoben.** Grundriss-Meldung „Ellipse (nicht als Wand übernommen)“ statt „ELLIPSE“. `KindLabel` kennt „Ellipse“.

**Neue Kürzel und Lochrolle (selbst geprüft):** Keine Änderung dieser Runde macht aus einem Loch ein Teil oder umgekehrt.
- Der Linien-Stoß lässt nur mehr Konturen aus.
- Strenge Punktgruppen machen nur mehr Elemente „überall unsicher“.
- Budgets, Vorabtest und die faule Rasterwahl ergeben nur seltener „formgleich“; die direkte Kandidatenliste ist vollständig.
- Der R-Baum liefert dieselben Überschneidungen wie das Raster.
- Im Füllflächen-Abgleich zählen offene Stoß-Gruppen nicht als umschließende Kontur. Eine Schraffur fällt dort also nur weg, wenn ihre umschließende geschlossene Kontur auf gerader Tiefe liegt. Konturen im Stoß-Rahmen lässt das Fräsen ohnehin aus.

**Prüfprogramme aller Prüfer** (nur gelesen bzw. als Kopie ausgeführt; Harness-Kopie des Prüfers 13; Einheiten-Fälle mit mm-Erwartung): **925 Fälle, 23 mit Befund** – dieselben 23 bekannten wie in der zwölften Prüfung; **cases13 (Prüfer 13): 0 von 25**. Prüfer-13-Vakuum-Fälle: Spitze −0,5 / −5 mm bleibt stehen, −0,001 / 0,0019 / 0,0021 fällt weg.

**Leistung (Lesen, Harness des Prüfers 13; alt = fc2a91ec6, eigene Harness-Kopie):**

| Fall | alt | neu | Spitze neu |
|---|---|---|---|
| s1 Kamm 500 C64 F117 | 77,7 s | 2,3 s | 0,3 GB |
| s1 Kamm 2000 C64 F117 / C250 F30 | > 150 s | 2,6 / 3,3 s | 0,5 / 0,7 GB |
| c1 1000 große + 60 000 winzig / c1e | 19,1 / 19,1 s | 0,8 / 0,7 s | 0,7 GB |
| c1f 100 große | 3,5 s | 0,7 s | 0,9 GB |
| c1 4000 + 250 000 | Abbruch Elementgrenze | Elementgrenze nach 2,4 s, 0,9 GB | – |
| w1 20 000 / 100 000 | 2,4 / 35,6 s | 0,7 / 4,6 s | 0,4 GB (vorher 82 GB angefordert) |
| l1 lang dünn | 2,2 s | 0,8 s (alle verworfen) | 0,3 GB |
| q9 20 000 Schleifen | 4,5 s | 1,2 s | 0,5 GB (vorher 1,1 GB) |
| k1 je6 | 4,2 s | 2,0 s | 1,8 GB (vorher 3,9 GB) |
| p2 / m1 / r1 | 4,3 / 1,0 / 6,7 s | 1,3 / 0,9 / 5,4 s | – |
| b1 30 000 × 30 000 | 4,9 s | 3,7 s | 24 301 bleiben (Budget, sicher) |

Abbruch beim Lesen: c1 ≤ 148 ms, s1 ≤ 372 ms, w1 ≤ 288 ms, b1/b2/k2 ≤ 262 ms nach dem Abbrechen. Die übrigen n-, q-, b-, d- und e-Fälle liegen wie in der zwölften Prüfung.

Belege (`--fix`): alle 12 Ausgaben hash-gleich mit `fix_alt` des Prüfers 13.

**Rot-Probe gegen fc2a91ec6:** 4 von 22 rot (die drei Fix13-Tests und Fix6 N-2 „Ellipse“) – `reports/T-0747/T-0747-fix13-rotprobe.log`.

**Weiterhin offen:**
- CAM-Laufzeit und Abbruch in `CncCam.Generate` (Kamm, große Kreiszahlen, b1/b2 mit Fräsbahn).
- Das Raster „Art je Kontur“ in `CncCam` trägt große Rahmen weiter flächig ein (nur Fräsbahn-Phase).
- INSERT, SPLINE, MLINE und REGION werden nicht aufgelöst.
- Die erklärten Prüfer-Fälle bleiben „rot“.

**Testzahlen T-0747-fix13:** Build 0 Fehler, keine neue Warnung in T-0747-Dateien. Kern (alle vierzehn T0747-Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests) **zweimal hintereinander**: je 157 gesamt, 156 bestanden, 1 übersprungen (vorbestehend „IFC echt“), 0 Fehler. App 13 Klassen inklusive Studio3DMeshEditUiTests – **123/123**. Prüfskript BESTANDEN.

## Vierzehnte Prüfung behoben (T-0747-fix14)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix14`, Commit obendrauf auf c9ea0f03f. Keine Maschine angesteuert.

**HOCH 1 – behoben.** Zur Stoß-Gruppe gehört jetzt die ganze Komponente, die über Spalte ≤ 0,1 mm (Verkettungstoleranz) erreichbar ist. Offene Ketten, deren Ende so nah an einem Ende der Gruppe oder einer schon angeschlossenen Kette liegt, werden nicht mehr weiter verkettet, sondern der Gruppe zugeschlagen; berühren sich zwei Gruppen so, werden sie eine. Der Rahmen, in dem beim Fräsen alles ausgelassen wird, umfasst damit Gruppe und angeschlossene Ketten. Der feine Aufbau im Füllflächen-Abgleich erkennt Stöße jetzt wie Grob-Aufbau und Fräsen. j1 mit Spalt 0 / 0,00001 / 0,005 / 0,05: kein Loch mehr außen gefräst (vorher 3,499 mm außen). Test `Junction_with_gap_chains`.

**HOCH 2 – behoben.** Eine POLYLINE mit unvollständigem oder ungültigem Eckpunkt (20 fehlt, 10/20 doppelt, NaN) gilt jetzt unabhängig vom Schließ-Flag überall als unsicher. p1/p2 ohne_20 und doppelt_10: kein G-Code mehr statt „Loch außen“. Test `Open_polyline_incomplete_vertex` mit Kontrolle (vollständiger Eckpunkt → Loch innen).

**MITTEL 3 – behoben.** Die Stoß-Schleife beim Fräsen fragt nur noch die Konturen ab, deren linke Rahmenkante im Gruppenrahmen liegt (nach linker Kante sortiert, Binärsuche), statt alle Konturen je Gruppe. Statt einer „offene Kontur“-Zeile je Linie gibt es eine Sammelmeldung zu allen Linienverbünden. j3 mit 66 000 Stößen: 2,3 s statt 46,0 s. Test `Many_junction_groups_fast` (50 000 Stoßgruppen + 1 Kreis, unter 10 s, weniger als 20 Meldungen).

**MITTEL 4 – behoben.** Die Dichteregel („mehr als 64 Knoten angesehen“) ist ersetzt:
- Knoten werden exakt über Schlüssel in 1-µm-Schritten je Ebene gesucht (Nachbarschlüssel ±1).
- Als Stoß zählt nur echte Mehrdeutigkeit: zwei verschiedene Knoten ≤ 1 µm oder Grad über 2.
- Ein eigenes Arbeitsbudget (20 Mio. Schritte) mit einer Sammelmeldung, falls es erschöpft ist.

Ergebnisse:
- f_loch_r1_1500 und f_kreis_r5_500 bis 12 000: je 2 Bahnen und 0 Meldungen (vorher 1 Bahn und 1500 bis 12 000 Meldungen „offene Kontur“).
- Test `Fine_line_circles`.

**Klein.**
- Der Abbruchteil von Fix13 `Shape_budget_and_big_frames` verlangt jetzt `OperationCanceledException` (eigener Thread, bis zu fünf Versuche).
- `KindLabel` kennt Linie, Polylinie, Bogen und Kreis; der Schlüssel „… mit unvollständigen Punkten“ wird über `KindLabel` gebildet.
- j6 (Grad-1-Stichlinien ab Plattenecken) bleibt bewusst ein Stoß, also auf der sicheren Seite.

**Nicht angefasst:** CAM-Abbruch-Nachlauf ([ALT]). Beim Formvergleich-Budget bleiben im Fall b1 30 000 × 30 000 weiterhin 24 301 Schraffuren stehen (sicher).

**Prüferfälle aller Runden** (Prüfprogramme nur gelesen bzw. als Kopie ausgeführt): **958 Fälle, 26 mit Befund**. Das sind die 23 bekannten und drei aus Prüfung 14, die schon auf c9ea0f03f und fc2a91ec6 genauso ausfallen:
- p2_platte_poly_loch_offene_polyline_normal: Der Kreis liegt in einem Loch der Platte, ist also ein Teil – das wird richtig außen gefräst.
- ez_R2000/R2018_spline_daneben: Eine SPLINE ohne bestimmbare Ausdehnung macht die ganze Zeichnung unsicher; es entsteht kein G-Code.

Prüfung 14 ist damit grün: cases14 0 von 12, casespoly14 1 von 6 (p2_normal, siehe oben).

**G-Code-Vergleich** (412 Fälle, Runden 9–14 und Feinfälle):

Gegen c9ea0f03f: 390 gleich, 22 abweichend, alle beabsichtigt:
- j1 / j2 / j3 / j6 / j7 / j8: jetzt kein Fräsen im Linienverbund samt angeschlossenen Ketten (sicher).
- p1 / p2 mit unvollständigem Eckpunkt: jetzt überall unsicher.
- Feinfälle: jetzt 2 Bahnen statt 1.
- z1–z3: nur Meldungstext (Sammelmeldung).

Gegen fc2a91ec6: 390 gleich, 22 abweichend. Dazu kommen die bewusst sicheren Fälle aus Fix13 (a2–a6 Punktgruppen, z1/z2 Stoß).

**Rot-Probe gegen c9ea0f03f:** die 4 neuen Fix14-Tests rot, die Fix13-Tests grün – `reports/T-0747/T-0747-fix14-rotprobe.log`.

**Testzahlen T-0747-fix14:** Build 0 Fehler, keine neue Warnung in T-0747-Dateien. Kern (alle fünfzehn T0747-Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests) **zweimal hintereinander**: je 161 gesamt, 160 bestanden, 1 übersprungen (vorbestehend), 0 Fehler. App 13 Klassen inklusive Studio3DMeshEditUiTests – **123/123**. Prüfskript BESTANDEN.

## Fünfzehnte Prüfung behoben (T-0747-fix15)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix15`, Commit obendrauf auf e400085fd. Keine Maschine angesteuert.

**HOCH 1 – behoben.** Beim Anschließen über Spalte ≤ 0,1 mm stehen jetzt die Enden der offenen Ketten und die Enden aller Stoß-Stücke im selben Raster. Berühren sich zwei Stoß-Gruppen direkt über einen Spalt (gleiche Ebene), verschmelzen sie zu einer Komponente. Ausgelassen wird im Rahmen bzw. Schutzbereich dieser ganzen Komponente. Der feine Aufbau des Füllflächen-Abgleichs nutzt dieselbe Verkettung.

Ergebnis: gg/ggk mit Spalt 0,001 / 0,05 / 0,0999 und j_direkt mit 0,00001 / 0,005 / 0,05 fräsen kein Loch mehr außen (vorher Loch über der Naht außen gefräst).

**HOCH 2 – behoben.** Jede Stoß-Komponente hat einen Schutzbereich aus ihrem Rahmen und ihrer konvexen Hülle. Ausgelassen und gemeldet wird jede geschlossene Kontur, auf die eines zutrifft:
- ihr Rahmen liegt im Komponentenrahmen,
- ihr erster Punkt liegt in der Hülle,
- sie berührt oder schneidet den Rand der Hülle.

Gesucht wird über nach Kante bzw. Punkt sortierte Listen und ein Streckenraster. Die Hülle enthält jedes Sehnenpolygon, mit dem früher eine offene Kette aus diesen Stücken als openOutline geschützt war. Was in fc2a91ec6 geschützt war, ist es also weiterhin. Eine Platte, die die Komponente nur umschließt (ggplatte), wird weiter gefräst.

Ergebnis: gg_spalt_0p11 und ggk_spalt_0p11 fräsen kein Loch mehr außen.

**MITTEL 3 – behoben.**
- Union-Find mit Pfadkürzung und Größe für die Gruppen, Abbruchprüfung in der Ausbreitung (alle 1024 Schritte) und in den Schlussschleifen.
- u1 Stoßkette 50 000: Konturen 48,6 s → 1,1 s.
- u2 rückwärts: 48,7 s → 1,1 s.
- u5: 8,3 s → 0,7 s.
- u6 20 000 mit einer Schraffur: Lesen 8,8 s → 1,1 s, Konturen 8,0 s → 0,4 s.
- Abbruch-Nachlauf beim Lesen u6: höchstens 242 ms (vorher bis 2,2 s).
- Test `Junction_chain_fast`: unter 10 s, Nachlauf unter 1 s.

**MITTEL 4 – behoben.** Die Enden (Versatz 0,9 × der Verbindungsgrenze 1e-6, paarweise 0,9 bzw. 1,8 ×) werden transitiv gruppiert: Union-Find über alle Paare innerhalb der Verbindungsgrenze, gefunden über Schlüssel in dieser Schrittweite mit Nachbarn ±1. Ein Haufen zählt als Stoß, wenn er breiter als die Verbindungsgrenze ist oder mehr als zwei Enden verschiedener Stücke enthält; deckungsgleich doppelte Stücke zählen einmal. Das Ergebnis hängt nicht von der Reihenfolge ab. Die Verkettung (Exact) findet in nicht mehrdeutigen Haufen genau den einen Partner, verwendet also dieselben Haufen.

Ergebnis:
- jj_* (drei Enden 0,9 µm versetzt): in allen 36 Prüfer-Reihenfolgen gleich, kein Loch außen.
- Test `Near_junction_order_independent` mit allen 120 Reihenfolgen × 6 Versätzen.

**Niedrig – behoben.**
- Die Einzelmeldung „Linien-Stoß bei X …“ entfällt; es bleibt die eine Sammelmeldung je Zeichnung.
- Nicht gelesene Elemente heißen in Meldungen deutsch, z. B. „Kreis mit Radius ≤ 0“, „Polylinie mit ungültiger Koordinate“, „Kreis (nicht in der Zeichenebene)“, „Region“.
- Die Zähl-Schlüssel (Skipped) bleiben, wie sie sind.

**Nicht angefasst:** Zeichnungsrahmen/Schriftfeld (casesr15, 3 Fälle ohne G-Code, sicher; Folgeaufgabe). CAM-Laufzeit bei vielen Kreisen ([ALT]).

**Prüferfälle aller Runden** (nur gelesen bzw. als Kopie ausgeführt): **1056 Fälle, 29 mit Befund**:
- die 26 aus der vierzehnten Prüfung, unverändert,
- die 3 Rahmenfälle casesr15 (kein G-Code, nicht anfassen).

Prüfung 15: cases15 0 von 80, casesgg15 0 von 15. Die rueck-Fälle j_direkt, fast_stoss und stern_70 fräsen kein Loch außen; die Kontrollfälle ctl_platte_line und fein_kreis_line unverändert mit Löchern innen.

**G-Code-Vergleich** (525 Fälle, Runden 9–15):
- Gegen e400085fd: 408 gleich, 117 abweichend.
  - 90 davon nur Meldungstext (deutsche Elementnamen, Sammelmeldung).
  - 27 mit anderem G-Code, alle beabsichtigt und sicher: jj (8), jk an der 1-µm-Grenze (3), gg/ggk (8), ggplatte (4: Loch an der Naht ausgelassen, Platte und fernes Loch bleiben), j_direkt (3), fast_stoss_abc (1).
- Gegen c9ea0f03f: 403 gleich (53 mit G-Code-Unterschied, dazu die aus Fix14).
- Gegen fc2a91ec6: 402 gleich (86 mit G-Code-Unterschied, dazu die aus Fix13/14).

**Rot-Probe gegen e400085fd:** alle 3 neuen Fix15-Tests rot (HOCH 1/2, MITTEL 3, MITTEL 4), Fix14 grün – `reports/T-0747/T-0747-fix15-rotprobe.log`.

**Testzahlen T-0747-fix15:** Build 0 Fehler, keine neue Warnung in T-0747-Dateien. Kern (alle sechzehn T0747-Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests) **zweimal hintereinander**: je 164 gesamt, 163 bestanden, 1 übersprungen (vorbestehend), 0 Fehler. App 13 Klassen inklusive Studio3DMeshEditUiTests – **123/123**. Prüfskript BESTANDEN.

## Sechzehnte Prüfung behoben (T-0747-fix16)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix16`, Commit obendrauf auf d61d0cfb8. Keine Maschine angesteuert.

**MITTEL 1 – behoben (Chain/Build linear).**
- Verschmelzen der Stoß-Komponenten über Spalte ≤ 0,1 mm über Zellen der Größe tol/√2: Zwei Enden in derselben Zelle sind immer ≤ tol, sie werden sofort vereinigt. Zwischen Nachbarzellen (±2) wird nur gesucht, solange ihre Wurzeln verschieden sind, und beim ersten Paar ≤ tol aufgehört. Union-Find mit Pfadkürzung und Größe.
- Eigenes Arbeitsbudget (30 Mio.). Bei Erschöpfung kommen die noch nicht geprüften Zellen zur Stoß-Komponente (sicher), dazu eine Sammelmeldung.
- `Junctions` arbeitet genauso: Schlüssel 1 µm/√2, im Schlüssel sofort vereinigt, Nachbarn ±2 nur bei verschiedener Wurzel. Das ergibt genau die transitive Hülle aller Paare ≤ 1 µm. Das Ergebnis bleibt reihenfolgeunabhängig; die 120-Reihenfolgen-Probe aus Fix15 ist grün.
- Ein einfacher Stern löst keine Budget-Sammelmeldung mehr aus.

**MITTEL 2 – behoben (Schutzbereich schnell).**
- Schon ausgelassene Konturen werden über Sprungzeiger mit Pfadkürzung übersprungen.
- Rahmen-Vortest, dann Punkt in konvexer Hülle in O(log H).
- Konturen am Hüllenrand prüft der Code direkt als offene Kandidaten nach linker Kante. Nur wenn das mehr Arbeit wäre als der Weg entlang der Kanten, wird in Zellschritten entlang der Hüllenkanten gesucht, nicht mehr über den ganzen Kantenrahmen.
- Abbruch alle 1024 Schritte, gemeinsames Budget (100 Mio.). Bei Erschöpfung werden die Konturen im Komponentenrahmen ausgelassen und gemeldet.

**MITTEL 3 – behoben (Platte am Hüllenrand wieder gefräst).** Begründung (wie vom Prüfer vorgeschlagen, geprüft): Jeder Umriss aus Stücken einer Komponente liegt in ihrer konvexen Hülle. Er kann also nur Konturen umschließen, die ganz in der Hülle liegen. Ausgelassen wird jetzt jede geschlossene Kontur,
1. deren Rahmen im Komponentenrahmen liegt,
2. deren erster Punkt in der Hülle liegt (± 1e-6, auch auf dem Rand) – das trifft jede Kontur, die ganz in der Hülle liegt,
3. von deren Eckpunkten einer echt in der Hülle liegt (mehr als 1e-6 vom Rand) – Konturen, die teilweise darin liegen.

Eine Kontur, die die Hülle umschließt oder nur berührt, wird gefräst; ihre Tiefe kann sich durch die Komponente nicht ändern. Ergebnisse:
- t_rand und t_rand_ebene: Platte und Loch wie in e400085fd.
- Gegenbeispiel-Versuch (Test `Plate_touching_hull_is_milled`): ein Quadrat, das den Hüllenrand kreuzt und teilweise darin liegt, bleibt ausgelassen; Platte und Loch werden gefräst.
- Die Fix13/14/15-Fälle (cases13/14/15, gg15, r15, Fix15-Tests) bleiben unverändert sicher: kein Loch außen.

**NIEDRIG – behoben.**
- Neue Zeittests (nicht parallele Sammlung, kleinste von zwei Messungen, Fräser Ø 6 wie in der Prüfung), alle gegen d61d0cfb8 rot:
  - Stern 50 000, Bürste 50 000, Rechteck 8000-fach deckungsgleich, 2000 X-Stöße über 20 000 Quadraten: je unter 10 s.
  - Stoßkette 20 000 mit Schraffur: unter 4 s.
  - Mit Fräser Ø 1 dominiert beim Stern das vorbestehende Hindernis-Raster der Fräsbahn-Prüfung (CAM-Grundlaufzeit, nicht Teil dieser Runde).
- Zähl-Schlüssel und Meldungstexte einheitlich deutsch: Kreis/Bogen mit Radius ≤ 0, Polylinie/Linie/Kreis mit ungültiger Koordinate, „… nicht in der Zeichenebene“, Ellipse ohne Ausdehnung / mit unsinnigem Parameter / zu groß zum Abtasten, 3D-Netz (Polylinie). Die betroffenen Tests sind nur im Wortlaut angepasst.

**Messwerte** (`--perf` mit Harness der Prüfer, Fräser Ø 6; Zeit für Lesen + Konturen + Fräsbahnen):

| Fall | e400085fd | d61d0cfb8 | neu |
|---|---|---|---|
| rechteck_dup_3000 / 8000 | 0,30 / 0,45 s | 3,5 / 22,8 s | 0,31 / 0,43 s |
| stern_30000 / v1_stern_50000 | 0,85 / 1,2 s | 17,9 / 46,3 s | 0,89 / 1,3 s |
| v2_buerste_50000 | 1,7 s | 49,1 s | 0,98 s |
| buendel_10000 | 0,36 s | 7,5 s | 0,36 s |
| v5_stoss_dicht_30000 | 1,1 s | 31,1 s | 1,1 s |
| v4_x_stoesse_2000_quadrate_20000 | 3,4 s | 169 s | 3,2–4,3 s (zwei Läufe: 4,3 / 3,4 gegen 3,4 / 3,4) |
| v4_x_stoesse_5000_quadrate_40000 | 8,9 s | > 240 s | 8,3 s |
| v3_huelle_100000_kreise_50000 | 4,3 s | 20,2 s | 4,4 s |
| y_2000_2000 | 30,8 s | 47,3 s | 31,6 s |
| u6 Stoßkette 20 000 + Schraffur | 17,0 s | 1,9 s | 1,9 s |
| u1 Stoßkette 50 000 | 49,9 s | 2,4 s | 2,1 s |

**Prüferfälle aller Runden** (Prüfprogramme nur gelesen bzw. als Kopie ausgeführt): **1069 Fälle, 34 mit Befund**. Das sind die 29 aus der fünfzehnten Prüfung und 5 aus cases16, die in den Läufen der Prüfer auf d61d0cfb8 und den älteren Ständen (alt12, alt14) genauso ausfallen:
- d1_bem_lochabstand_gap0p0/0p625, m1_mittelkreuz, m2_mittellinie: Loch nicht gefräst, sicher. Das sind Bemaßungs- und Mittellinien – Folgeaufgabe.
- u1_u_stoss_loch_in_kerbe: Der Kreis liegt in der Kerbe außerhalb des U-Umrisses. Er ist dort ein eigenes Teil und wird richtig außen gefräst; der Ausläufer beginnt mitten auf einer Linie und ist kein Stoß.

cases16b: 0 von 2. Die rueck-Sammlungen (faelle16, rand, meld) haben keine Erwartungsdateien; sie wurden über den G-Code-Vergleich bewertet.

**G-Code-Vergleich** (553 Fälle):
- Gegen d61d0cfb8: 450 gleich, 103 abweichend. Davon haben nur t_rand und t_rand_ebene anderen G-Code (Platte wieder gefräst); die übrigen 101 unterscheiden sich nur im Meldungstext (deutsche Zähl-Schlüssel).
- Gegen e400085fd: 407 gleich, 146 abweichend, davon 27 mit anderem G-Code – dieselben beabsichtigten aus Fix15.
- Hinweis: Die Vergleichsausgabe brach bisher auf der Konsole beim Zeichen „≤“ ab, jetzt läuft sie mit UTF-8. Die Summenzeile (Fälle/gleich/abweichend) war schon vorher vollständig.

**Rot-Probe gegen d61d0cfb8:** beide neuen Fix16-Tests rot, Fix15-Tests grün – `reports/T-0747/T-0747-fix16-rotprobe.log`.

**Testzahlen T-0747-fix16:** Build 0 Fehler, keine neue Warnung in T-0747-Dateien. Kern (alle siebzehn T0747-Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests) **zweimal hintereinander**: je 166 gesamt, 165 bestanden, 1 übersprungen (vorbestehend), 0 Fehler. App 13 Klassen inklusive Studio3DMeshEditUiTests – **123/123**. Prüfskript BESTANDEN.

**Weiterhin offen (Folgeaufgabe):** CAM-Grundlaufzeit (Nearest/Fräsbahn, Hindernis-Raster bei kleinem Fräser), Bemaßungs-Hilfslinien „zu nah“ an Bohrungen, Zeichnungsrahmen mit Schriftfeld.

## Siebzehnte Prüfung behoben (T-0747-fix17)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix17`, Commit obendrauf auf 840d09292. Keine Maschine angesteuert.

**MITTEL 1 – behoben (kein globales Budget mehr).** Der Schutzbereich ist jetzt eine gemeinsame Prüfroutine mit Budget je Verbund (20 Mio. Hüllenprüfungen); gezählt werden nur die teuren Punkt-in-Hülle-Tests.
- Gesucht wird über ein Raster aller Eckpunkte geschlossener Konturen, je Verbund nur in den Zellen seines Rahmens. Einträge schon ausgelassener Konturen werden dabei entfernt. Damit hat jede Regel auch eine y-Überlappung.
- Ist das Budget eines Verbunds erschöpft, werden nur Konturen ausgelassen, deren Rahmen den Verbundrahmen wirklich überlappt (`Lower(minX − maxW)` + Sprungzeiger), mit eigener Meldung „Prüfaufwand zu groß – … vorsichtshalber ausgelassen“.
- b2_budget_5000_q10000 und alle b-/b2-Fälle: Nachbarplatte und Loch werden wieder gefräst (Bahnen wie d61d0cfb8).
- Test `Neighbour_plate_survives_many_groups`.

**MITTEL 2 – behoben (keine G×N-Schleifen).**
- Die Ausweichschleife beginnt bei `Lower(minX − maxW)` und überspringt ausgelassene Konturen.
- Eine Zickzack-Kontur, die viele Hüllen ohne Eckpunkt darin kreuzt, wird je Verbund nur noch mit ihren Eckpunkten in dessen Rahmenzellen geprüft.
- Punkt-in-Kontur für große Konturen über einen Streifen-Index, waagrecht oder senkrecht – je nachdem, was besser verteilt (Zickzack mit hohen Zähnen).

**HOCH – behoben (Altcode, U vor einer Kerbe).** Neu erkannt wird: Ein offener Linienzug oder Stoß-Verbund stößt mit mindestens zwei Enden (≤ 0,1 mm, gleiche Ebene) an dieselbe geschlossene Kontur C und liegt teilweise außerhalb von C. Dann schließt er mit dem Rand von C einen Bereich ein; in welcher Lesart der Umriss gemeint ist, bleibt offen.
- Ausgelassen und gemeldet werden C selbst (ihr Rand wäre dort kein Außenrand) und alles in der Hülle aus Linienzug ∪ C, das nicht ganz in C liegt.
- Konturen ganz in C behalten in beiden Lesarten ihre Tiefe und werden weiter gefräst – im Prüffall das Loch (30,50).
- Ein Linienzug ganz in C (Gravur von Rand zu Rand) erzeugt keinen neuen Bereich und ändert nichts.
- Linien auf anderen Ebenen (Bemaßung) zählen nicht als Anschluss. Bemaßungen liegen üblich auf eigener Ebene, und die Anschlussregel auf andere Ebenen auszudehnen, würde jede Hilfslinie am Plattenrand zum Problem machen.
- k1 mit und ohne Stummel, Ende auf einer Kante statt einer Ecke, 0,05 mm Spalt: Loch in der Kerbe nie außen, Loch in der Platte bleibt. Test `U_in_front_of_notch`, mit Kontrollen Gravur-Linie und Bemaßungsebene.
- **G-Code-Verlust durch diese Regel:** von den bisherigen Prüferfällen (Runden 9–16) verliert keiner G-Code. Im Vergleich gegen 840d09292 ändert sich der G-Code nur bei den beiden k1-Fällen; 19 Fälle unterscheiden sich nur im Meldungstext.

**NIEDRIG.**
- (a) Chain- und Junctions-Budget: Bei Erschöpfung werden auch die ±2-Nachbarzellen ungeprüfter Zellen der Stoß-Komponente zugeschlagen (bzw. als unsicher markiert), weil ein Zellenpaar nur von einer Seite aus geprüft wird.
- (b) Der Zeittest „2000 X-Stöße“ (Grenze 10 s) liegt neu bei 3,2–4 s, d61d0cfb8 bei 169 s; ein Prüffall mit noch mehr Abstand wäre der neue Nachbarplatten-Test (neu 1,5 s).
- (c) Konturen, die eine Hülle ohne Eckpunkt darin kreuzen, werden gefräst. „Zu nah an Kontur N … 0 mm“ trifft dann zu: Die Fräsbahn käme den Linien des Verbunds (Hindernisse) zu nah. Die Meldung bleibt deshalb so.
- (d) `BuildGrid` prüft den Abbruch alle 4096 Strecken.

**Prüfskript des Prüfers zu u1_u_stoss_loch_in_kerbe:** Unser Prüfskript `reports/T-0747/pruefe_t0747.py` nutzt die Prüferdatei nicht. Die Erwartung dort bleibt Sache des Prüfers; das Fräsen ist sicher.

**Messwerte** (`--perf`, Fräser aus der Falldatei):

| Fall | d61d0cfb8 | 840d09292 | neu |
|---|---|---|---|
| b_budget_0 / 1000 / 3000 / 5000 / 20000 | 4,5 / 4,6 / 5,2 / 5,8 / 10,1 s | 4,1 / 5,5 / 6,2 / 6,9 / 11,7 s | 4,5 / 4,4 / 4,7 / 4,6 / 5,1 s |
| b2_budget_300 / 600 (40 401 Quadrate) | 4,8 / 4,6 s | 4,9 / 5,1 s | 4,3 / 4,7 s |
| b2_budget_2000 / 5000 / 10000 (10 000) | 2,5 / 2,8 / 3,1 s | 3,1 / 3,7 / 4,1 s (Platte fehlt) | 2,3 / 2,3 / 2,4 s |
| b2_budget_5000 / 20000 (2500) | 1,6 / 2,3 s | 2,1 / 3,1 s | 1,4 / 1,8 s |
| g1_gitter_100 / 200 | 1,8 / 5,2 s | 1,7 / 4,4 s | 1,9 / 5,5 s |
| g2_sterne_2000 / 5000 | 0,86 / 1,8 s | 0,85 / 1,8 s | 0,91 / 1,8 s |
| g3_lange_T_5000 / 20000 | 4,5 / 8,1 s | 5,3 / 9,2 s | 3,6 / 3,5 s |
| w1a / w1b / w1c / w1d | 1,2 / 8,0 / 1,6 / 1,6 s | 1,2 / 13,8 / 2,1 / 2,6 s | 1,2 / 8,0 / 2,1 / 1,7 s |
| w1e / w1f / w1g | 4,0 / 11,7 / 11,5 s | 15,9 / 21,4 / 16,0 s | 4,5 / 12,2 / 12,1 s |
| w2a / w2b Kettenbudget | > 180 s / – | 1,3 / 3,4 s | 1,4 / 3,5 s |
| w3a Stoßbudget / w4 lange Linien | 3,6 / 7,9 s | 0,55 / 7,8 s | 0,58 / 8,1 s |
| Prüfer 16: v1 Stern 5000 / 20 000 / 50 000 | 0,8 / 10,5 / 47,9 s | 0,33 / 0,70 / 1,3 s | 0,35 / 0,68 / 1,3 s |
| v2 Bürste 20 000 / 50 000 | 9,4 / 50,4 s | 0,51 / 0,95 s | 0,52 / 0,98 s |
| v3 Hülle 20 000 / 100 000 | 4,0 / 20,2 s | 2,3 / 4,4 s | 2,9 / 5,3 s |
| v4 X-Stöße 2000 / 5000 | 169 / > 180 s | 3,2 / 8,3 s | 3,9 / 10,3 s |
| v5 Stoß dicht 10 000 / 30 000 | 8,7 / 31,5 s | 0,57 / 1,2 s | 0,59 / 1,2 s |

Alle Fälle liegen höchstens bei d61d0cfb8 + 20 %, die meisten darunter. Die Prüfer-16-Fälle v3 und v4 sind gegenüber 840d09292 um bis zu 25 % langsamer; das kostet der Aufbau des Eckpunkt-Rasters. Sie bleiben weit unter d61d0cfb8.

**Prüferfälle aller Runden** (Prüfprogramme nur gelesen bzw. als Kopie ausgeführt): **1345 Fälle, 34 mit Befund**, dieselben wie in der sechzehnten Prüfung (u1 und die Bemaßungs-/Mittellinienfälle; Rahmenfälle r15). Prüfung 17: cases17 0 von 10, cases17b 0 von 2 (vorher 2 – k1 behoben), cases17c 0 von 264. budget17, budget17b, cases17s und rueck/faelle haben keine Erwartungsdateien; sie wurden über Bahnen und den G-Code-Vergleich bewertet.

**G-Code-Vergleich** (844 Fälle):
- Gegen 840d09292: 823 gleich, 21 abweichend. Nur k1 (2) mit anderem G-Code; 19 nur mit zusätzlicher Meldungszeile.
- Gegen d61d0cfb8: 677 gleich, 167 abweichend, davon 4 mit anderem G-Code: t_rand, t_rand_ebene (Fix16) und k1 (2).

**Rot-Probe gegen 840d09292:** beide neuen Fix17-Tests rot, Fix16-Tests grün – `reports/T-0747/T-0747-fix17-rotprobe.log`.

**Testzahlen T-0747-fix17:** Build 0 Fehler, keine neue Warnung in T-0747-Dateien. Kern (alle achtzehn T0747-Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests) **zweimal hintereinander**: je 168 gesamt, 167 bestanden, 1 übersprungen (vorbestehend), 0 Fehler. App 13 Klassen inklusive Studio3DMeshEditUiTests – **123/123**. Prüfskript BESTANDEN.

**Weiterhin offen (Folgeaufgabe):** CAM-Grundlaufzeit (Nearest/Hindernisraster), Bemaßungs-„zu nah“ (d1/m1/m2), Zeichnungsrahmen mit Schriftfeld, n3_kreise_zelle.

## Achtzehnte Prüfung behoben (T-0747-fix18)
Arbeitskopie `%TEMP%\trendo-auftrag-T-0747-fix18`, Commit obendrauf auf 5925d5345. Keine Maschine angesteuert.

**HOCH – behoben (Sehne über der Kerbe).** „Teilweise außerhalb von C“ wird jetzt je Strecke geprüft: Jede Strecke des Linienzugs wird an ihren Schnitten mit dem Rand von C geteilt. Jedes Teilstück liegt dann ganz innen, ganz außen oder auf dem Rand; von jedem wird die Mitte getestet (außen = nicht in C und weiter als 0,1 mm vom Rand).
- Damit lösen auch eine einzelne LINE oder offene 2-Punkt-Polylinie über die Kerbe (s1, s2), 0,05 mm vor der Kante (s4), die Sehne bei x 95 (s9) und die 3-Punkt-Polylinie mit allen Punkten auf dem Rand (s10) die Regel aus.
- Der Kreis in der Kerbe wird nie mehr außen gefräst; das Loch in der Platte bleibt.
- Eine Gravurlinie ganz in der Platte löst weiterhin nicht aus.
- s7 (Sehne auf anderer Ebene) bleibt wie bisher ohne Anschluss: Linien über zwei Ebenen sind Folgeaufgabe.
- Test `Chord_closing_notch`.
- **G-Code-Verlust:** keiner. Gegen 5925d5345 ändert sich der G-Code nur in den 9 Sehnen-Fällen (s1–s4 aus cases18, s1/s2/s4/s9/s10 aus sehne).

**MITTEL – behoben (Linienzüge an derselben C gemeinsam).**
- Alle Linienzüge an derselben Kontur C werden zusammen behandelt. Die Kandidaten – offene Konturen, deren Rahmen den gemeinsamen Rahmen überlappt und die nicht ganz in C liegen – werden je C einmal bestimmt.
- Je Linienzug werden nur noch diese Kandidaten gegen die Hülle aus Hülle(C) ∪ Linienzug geprüft; die vielen Konturen ganz in C kommen gar nicht mehr in die Prüfung.
- Dabei gefunden: Die ältere Regel für offene Umrisse berechnete den Konturrahmen je Paar neu (LINQ). Die Rahmen werden jetzt einmal berechnet.
- Ergebnisse:

| Fall | 840d09292 | 5925d5345 | neu |
|---|---|---|---|
| x1c (5000 U / 5000 Löcher) | 20,8 s | 88,9 s | 2,9 s |
| x1e | 29,9 s | 104 s | 3,7 s |
| p2_u_100_2000 | 10,6 s | 43,8 s | 2,3 s |

- Test `Many_u_on_one_plate_fast` (Platte mit 5000 Löchern und 5000 U, Fräser Ø 6): 7,4 s, Grenze 10 s.

**NIEDRIG/MITTEL – behoben (Gesamtgrenze je Datei).** Für Schutzbereich und Teil b gibt es eine gemeinsame Grenze je Datei (150 Mio. Schritte). Bei Erschöpfung folgt die bestehende sichere Folge: Konturen mit Rahmen-Überlappung auslassen und mit „Prüfaufwand zu groß“ melden. In den Sammlungen wird die Grenze nicht erreicht. p3_streifen_1000 liegt neu bei 61 s (840d09292: 64 s, 5925d5345: 85 s); die Zeit steckt dort in der Fräsbahn-Phase.

**NIEDRIG optional – erledigt.** `Junction_cases_fast` misst bis zu dreimal mit kurzer Pause; die kleinste Messung zählt, die Grenze von 10 s bleibt.

**Messwerte** (`--perf`, Fräser aus der Falldatei; „kein G“ = kein G-Code, alle Konturen sicher ausgelassen – in allen drei Ständen gleich):

| Fall | 840d09292 | 5925d5345 | neu |
|---|---|---|---|
| x1a / x1b / x1c / x1d | 2,2 / 4,7 / 20,8 / 1,4 s | 3,0 / 14,8 / 88,9 / 1,4 s | 1,9 / 1,4 / 2,9 / 1,7 s |
| x1e / x1f | 29,9 / 10,5 s | 104 / 25,3 s | 3,7 / 4,2 s |
| x2a | 4,9 s | 14,7 s | 1,4 s |
| x3a (kein G) | 167 s | 167 s | 170 s |
| x4a / x4b (kein G) | 51,2 / 1,9 s | 59,4 / 1,9 s | 4,4 / 1,9 s |
| x5a (kein G) | 3,4 s | 3,6 s | 3,7 s |
| x6a / x6b / x6c (kein G) | 1,7 / 3,5 / 173 s | 2,1 / 4,3 / 196 s | 2,1 / 4,3 / 180 s |
| p1_gassen_30 / 60 / 100 (kein G) | 0,6 / 10,9 / 106 s | 0,6 / 10,6 / 102 s | 0,6 / 10,6 / 101 s |
| p2_u_200 / 1000 / 3000 | 0,6 / 2,6 / 0,8 s | 1,4 / 8,2 / 0,8 s | 0,4 / 0,9 / 0,8 s |
| p2_u_60_2000 / 100_2000 | 4,3 / 10,6 s | 15,0 / 43,8 s | 0,9 / 2,3 s |
| p3_streifen_500 / 1000 | 4,7 / 63,8 s | 5,8 / 84,9 s | 5,9 / 61,4 s |
| d_diag_500 (kein G) | 146 s | 152 s | 149 s |
| v3 Hülle 20 000 / 100 000 | 2,4 / 4,4 s | 2,9 / 5,3 s | 2,8 / 5,4 s |
| v4 X-Stöße 2000 / 5000 | 3,5 / 8,9 s | 3,9 / 10,3 s | 4,0 / 10,3 s |

x3b/x3c, p3_streifen_1500 und d_diag_2000 brechen in allen drei Ständen schon beim Lesen an der Datei- bzw. Elementgrenze ab.

Alle U-Fälle (Teil b) liegen deutlich unter 840d09292. Bei v3 sowie x6a/x6b und p3_streifen_500 (+15 bis +26 %) bleibt der Fix17-Aufbau des Eckpunkt-Rasters gegenüber 840d09292 sichtbar. Ein Versuch, kleine Konturen nur einmal je Rahmenzelle einzutragen, war langsamer (v4 5000: 15 s) und wurde verworfen.

**Prüferfälle aller Runden** (Prüfprogramme nur gelesen bzw. als Kopie ausgeführt): **1360 Fälle, 36 mit Befund** – die 34 aus der siebzehnten Prüfung und aus cases18 b1_bruecke_zwei_platten und x1_u_ueberstand_kreuzend. Beide sind ausdrücklich nicht Teil dieser Runde (Brücke zwischen zwei Platten, U mit sich kreuzenden Überständen). Die sehne-, diag- und faelle-Sammlungen haben keine Erwartungsdateien; sie wurden über den G-Code-Vergleich bewertet.

**G-Code-Vergleich** (874 Fälle):
- Gegen 5925d5345: 864 gleich, 10 abweichend. Davon 9 mit anderem G-Code (die Sehnen-Fälle, beabsichtigt) und 1 nur mit anderem Meldungstext.
- Gegen 840d09292: 840 gleich, 34 abweichend.

**Rot-Probe gegen 5925d5345:** beide neuen Fix18-Tests rot, Fix17 grün – `reports/T-0747/T-0747-fix18-rotprobe.log`.

**Testzahlen T-0747-fix18:** Build 0 Fehler, keine neue Warnung in T-0747-Dateien. Kern (alle neunzehn T0747-Klassen, T0266_CncSim, T0273_Arc, T0256, T0257, IfcExchangeTests, ImageVectorTests, SPEC01_*, SPEC14_*, CadTests, CadMeasureTests, Studio3DCadCrashTests) **zweimal hintereinander**: je 170 gesamt, 169 bestanden, 1 übersprungen (vorbestehend), 0 Fehler. App 13 Klassen inklusive Studio3DMeshEditUiTests – **123/123**. Prüfskript BESTANDEN.

**Weiterhin offen (Folgeaufgabe [ALT]):** Brücke zwischen zwei Platten, U mit sich kreuzenden Überständen, U bzw. Sehne über zwei Ebenen, `SegGrid.Add` füllt den ganzen Streckenrahmen, Big_dxf_fast ohne Wiederholung, CAM-Grundlaufzeit.
