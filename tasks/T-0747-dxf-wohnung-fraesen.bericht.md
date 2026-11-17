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
