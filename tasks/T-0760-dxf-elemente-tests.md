# T-0760: DXF-Elemente lesen statt melden, lastrobuste Zeittests

**Ziel:** INSERT, SPLINE und MLINE im DXF-Leser wirklich lesen (Fräsen, Füllflächen, Wohnungsplaner); REGION und die „ezdxf-Linse“ nur, wenn sicher – sonst ehrlich gemeldet. Sechs Zeittests lastrobust machen, ohne Grenzen zu lockern.
**Branch:** claude/T-0760-dxf-elemente-tests (Basis main, Release 0.9.108, 24bb4f70e)
**Dateien/Bereich:** `src/Trendo.Core/Cad/CadDrawing.cs` (Leser), neu `src/Trendo.Core/Cad/CadDxfElements.cs`, `src/Trendo.Core/Arch/PlanDxfImport.cs` (nur SPLINE wie ELLIPSE behandeln), Tests unter `tests/Trendo.Core.Tests/`, Belege `reports/T-0760/`.
**Fertig, wenn:**
- [x] INSERT: Einfügepunkt, Skalierung, Drehung, Spiegelung, Feld, verschachtelt mit Tiefengrenze 16, Zyklenschutz, Arbeitsgrenze
- [x] SPLINE: Kontroll-/Fitpunkte, Knoten, Gewichte (NURBS), Sehnenfehler ≤ 0,001 mm je Einheit, geschlossen/periodisch nur wenn eindeutig
- [x] MLINE: Linien aus Gehrung × Parameter, gegen MLINESTYLE-Abstände geprüft, Enden und Gehrungen nach Stil
- [x] REGION und Linse: geprüft, bleiben gemeldet (Begründung im Bericht)
- [x] Sicherheitsregeln aus T-0747 nach dem Auflösen (Tests)
- [x] Regressionslauf über alle Prüferfälle gegen main, jede Abweichung begründet
- [x] Fremdbeleg mit echtem Fremdprogramm (FreeCAD 1.1.4) – Import und Rundreise
- [x] Zeittests lastrobust, Nachweis rot bei künstlich verlangsamtem Code
**Nicht tun:** CncCam-Laufzeit (T-0758), Konturen/Schutzbereich (T-0759); kein Volltest (macht die Hauptsitzung).
**Am Ende:** Branch pushen, Bericht `tasks/T-0760-dxf-elemente-tests.bericht.md`.
