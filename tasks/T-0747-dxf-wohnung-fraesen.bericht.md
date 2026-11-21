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
