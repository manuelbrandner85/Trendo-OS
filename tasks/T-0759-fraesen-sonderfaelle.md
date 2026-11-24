# T-0759: Fräsen aus DXF – Sonderfälle (Brücke, kreuzende Überstände, zwei Ebenen, Bemaßung, Zeichnungsrahmen)

**Ziel:** Dort Fräsbahnen erzeugen, wo es eindeutig sicher ist; sonst weiter auslassen und melden. Kein Loch und kein Kreis darf je als Außenkontur gefräst werden.
**Branch:** claude/T-0759-fraesen-sonderfaelle (Basis main, Release 0.9.108, 24bb4f70e)
**Dateien/Bereich:** `src/Trendo.Core/W6/CncCam.cs` (Schutzbereich, Teil b, Hindernisse), neu `src/Trendo.Core/W6/CncCamSonderfaelle.cs`; Tests `tests/Trendo.Core.Tests/FraesenSonderfaelleT0759Tests.cs`; Belege `reports/T-0759/`.
**Fertig, wenn:**
- [x] b1_bruecke_zwei_platten, x1_u_ueberstand_kreuzend, s7 (zwei Ebenen), d1/m1/m2 (Bemaßung/Mittellinie), casesr15 (Rahmen) je mit Rot-Grün-Test
- [x] Regressionslauf aller Prüfer-Altfälle (Runden 9–21) gegen main, jede G-Code-Abweichung begründet
- [x] eigene Gegenfälle (Kreis in Kerbe nur durch Bemaßung geschlossen, Rahmen der eigentlich Platte ist, Brücke die einen Bereich einschließt, „#“, Lochkreis)
- [x] Leistung nicht schlechter (Messung in `reports/T-0759/`)
**Nicht tun:** DXF-Leser (T-0760: INSERT/SPLINE/MLINE/REGION), Laufzeit der Fräsbahn-/Gravur-Erzeugung und Abbruch (T-0758). Kein Merge, kein Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0759-fraesen-sonderfaelle.bericht.md`.
