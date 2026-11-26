# T-0772 – Belege (Fräsen- und DXF-Restpunkte aus 0.9.109)

Alt = main 542dc1ce7 (Release 0.9.110), neu = Zweig claude/T-0772-fraesen-rest. Gleicher Mess-Harness (`werkzeuge/harness_Program.cs`, Weg `CadDxf.Read` → `CncCam.Contours` → `CncCam.Generate` bzw. `CncJob.ImportDrawing`), alt und neu abwechselnd (`werkzeuge/messen.sh`). Prüfwerte: erste 16 Hex-Zeichen von SHA-256 über Konturen (K), Verkettungs-Hinweise (N), G-Code (G) und Meldungen (W).

**Wichtig:** Die Originaldateien der Prüfrunden (k5_dicht, b2, e0, z1, p1 aus T-0747) liegen nicht mehr auf dem PC. Sie wurden nach Namen und Meldungen in `reports/T-0758` nachgebaut (`werkzeuge/erzeuge772.py`); die Zahlen gelten für diese Nachbauten. Während der Messung lief auf dem PC eine fremde Testsitzung mit voller Last – Einzelwerte schwanken, deshalb Median aus 3 abwechselnden Läufen, wo es zeitlich ging.

| Fall | alt | neu | Ergebnis gleich |
|---|---:|---:|---|
| k5b_dicht_2000_3000 (Konturen) | 5,7 s (Median 5,66/5,67/8,21) | 1,4 s (1,45/1,29/1,41) | K und N gleich |
| k5b_dicht_2000_30000 (Konturen) | > 400 s abgebrochen (früherer Einzellauf 290 s) | 51 s (70,7/50,6/51,3) | K und N gleich wie früherer alter Lauf (`vergleich/alt_k5b_2000_30000_50_phasen.log`) |
| k5b_dicht_500_60000 | nicht abgewartet | 97 s, dann Budget: Meldung „weitere kleine Spalten werden nicht mehr geschlossen“ | – (neu: begrenzt) |
| Abbruch k5b 60000 nach 3 s | Nachlauf 374 ms | 94 ms | – |
| b2 2000 (Generate) | 13,6 s (13,99/12,85/13,57) | 9,6 s (10,32/9,57/9,18) | G gleich |
| b2 20000 | > 300 s abgebrochen | 256 s fertig | – (alt ohne Ergebnis) |
| e0 10000 | 5,7 s | 6,1 s | G gleich (kein Gewinn) |
| e0 100000 | OutOfMemoryException nach 56 s (Absturz) | klare Meldung „G-Code würde zu groß“ nach 39 s | – |
| z1 10000 | 3,47 s | 3,29 s | G gleich (kein Gewinn) |
| z1 100000 (je 1 Lauf) | 61 s | 50 s | G gleich (im Rahmen der Last) |
| p1_diag_gravur_1000_10000 | 6,8 s (6,80/6,99/6,73) | 2,9 s (2,98/2,80/2,87) | G und W gleich |
| p1_diag_gravur_200_10000 | 3,5 s (3,78/3,51/3,26) | 2,5 s (2,59/2,54/2,53) | G und W gleich |

Ergebnisgleichheit zusätzlich:
- `vergleich/`: 400 Zufallszeichnungen (`werkzeuge/korpus772.py`: Platten, Löcher, lange Gravurlinien, Linien-Rechtecke mit gleich großen Spalten, fast gleiche Umrisse, dichte Linien) über `CncJob.ImportDrawing`: **400 von 400 gleich** (G-Code und Meldungen bzw. Fehlermeldung).
- Test `T0772VerkettenTests.Gleiches_ergebnis_wie_bisher`: 2400 Fälle gegen die unveränderte Kopie des Verkettens aus 0.9.110 (`tests/Trendo.Core.Tests/CadContoursAlt.cs`), Ketten, Reihenfolge und Meldungen zeichengleich.
- Bestehende Fräs-Testklassen T0747/T0758/T0759/T0760/T0266 und App-Klassen grün (`tests/`).

Punkt 4: `punkt4/` (Werkzeugprüfung, Begründung). Punkt 5: `punkt5/` (Rot auf main, Grün auf dem Zweig).

Rust: nicht eingesetzt. Die Zeit ging in quadratischen Algorithmen verloren (jede Verkettungsrunde alles neu, jedes Paar fast gleicher Umrisse mit zwei Rastern, lange Linien als Kandidat in jeder Abfrage) – das hilft keine andere Sprache.

Prüfsummen: `SHA256SUMS.txt`.
