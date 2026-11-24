# T-0758: Fräsen aus DXF – Laufzeit und Abbruch

**Ziel:** Die [ALT]-Folgeaufgaben aus 21 Prüfrunden von T-0747 zur Laufzeit von `CncCam.Generate` schließen: erst die echte Ursache messen, dann Algorithmus und Raster verbessern – bei unverändertem G-Code und unveränderten Sicherheitsregeln. Freigegeben vom Nutzer am 23.11.2026.
**Branch:** claude/T-0758-fraesen-tempo (Basis main, Release 0.9.108, 24bb4f70e)
**Dateien/Bereich:** `src/Trendo.Core/W6/CncCam.cs` (Fräsbahn- und Gravur-Erzeugung, `SegGrid`, Abbruch), Tests `tests/Trendo.Core.Tests/CncCamTempoTests.cs`, Belege `reports/T-0758/`. Nicht: Kontur-Logik in `CadContours` (T-0759), DXF-Lesen (T-0760).
**Fertig, wenn:**
- [x] Ursache je bekanntem Fall gemessen (Phase und Algorithmus), Messtabelle alt/neu unter `reports/T-0758/`.
- [x] Schräge Linien von Rand zu Rand, viele zu kleine Löcher, Kamm, kollineare Kante, viele Rahmen in wenigen Zellen: deutlich schneller.
- [x] Abbruch der Fräsbahn-Erzeugung in unter 2 s.
- [x] G-Code und Meldungen gleich wie main über alle Altfälle der Prüfer (Runde 2–21); jede Abweichung erklärt.
- [x] Zeittests mit fester Grenze, bestehende Tests unverändert; betroffene Testklassen grün.
**Nicht tun:** Verhalten ändern (Schutzbereich, U vor Kerbe, Linien-Stöße, Meldungen), Kontur-Logik umbauen, Rust ohne messbaren Vorteil, Merge/Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0758-fraesen-tempo.bericht.md`.
