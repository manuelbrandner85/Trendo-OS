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
- Taschen mit Inseln werden nicht gemeinsam ausgeräumt: Loch und zu nahe Insel werden beide ausgelassen und gemeldet (sicher, aber kein Ergebnis).
- SVG-Bézierkurven: Abweichung der Abtastung (96 Stücke je Kurve) zum echten Kurvenverlauf ist nicht begrenzt berechnet, nur sehr klein; DXF-Bögen/Kreise sind auf 0,001 mm begrenzt.
- Ziehen und Ablegen per Maus und echte Fremd-CAD-Programme weiterhin nicht geprüft (keine installiert).
