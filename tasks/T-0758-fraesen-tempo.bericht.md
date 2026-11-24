# Bericht T-0758: Fräsen aus DXF – Laufzeit und Abbruch

**Auftrag:** [ALT]-Folgeaufgaben aus 21 Prüfrunden von T-0747 (Laufzeit von `CncCam.Generate`), freigegeben am 23.11.2026.
**Branch/Arbeitskopie:** `claude/T-0758-fraesen-tempo`, eigene Kopie von `main` (Release 0.9.108, 24bb4f70e) in `%TEMP%\trendo-auftrag-T-0758`; Vergleichsstand `main` als eigener Arbeitsbaum.
**Geändert:** nur `src/Trendo.Core/W6/CncCam.cs` (Fräsbahn-/Gravur-Erzeugung, Streckenraster `SegGrid`, Abbruch). Neu: `tests/Trendo.Core.Tests/CncCamTempoTests.cs`, `reports/T-0758/`. Kontur-Logik (`CadContours`, T-0759) und DXF-Lesen (T-0760) nicht angefasst.

## Zuerst gemessen: wo die Zeit liegt
Mit Zeitstempeln je Phase in `Generate` (nur im Messstand, nicht im Commit) und `dotnet-trace` (Stichproben je Methode) auf den Prüferfällen. Ergebnis (0.9.108):

| Fall | langsame Phase | Ursache |
|---|---|---|
| Platte + schräge Linien von Rand zu Rand (p_diag, q_diag, r2_diag, p1_diag_gravur) | Aufbau des Hindernisrasters (`BuildGrid` → `SegGrid.Add`) | `SegGrid.Add` trug jede Strecke in **jede Zelle ihres Rahmens** ein: eine schräge 2000-mm-Linie bei 3-mm-Zellen ≈ 445 000 Zellen (100 Linien: 28 s, 240 MB). Dasselbe im Versatz (`polyGrid`, `rawGrid`), in `Unreached` und bei den Rahmen. |
| viele zu kleine Löcher (q_diag 5000 Kreise r 0,5 bei Fräser 6) | Versatz (`OffsetLoops`) | Der rohe Versatz nach innen eines zu kleinen Lochs ist ein Knäuel, dessen Strecken sich alle gegenseitig schneiden: quadratisch viele Schnittpunkte und Stücke je Loch (≈ 17 ms je Loch), Ergebnis trotzdem leer. |
| schräge Kante mit 3000–10 000 kollinearen Punkten + deckungsgleiche Linie | Raster-Aufbau und -Abfragen | wie oben (lange schräge Strecken), dazu Abfragerechtecke über die ganze Platte: Millionen Zellen je Abfrage. |
| Kamm 50 000 / Zickzack 100 000 (eine Kontur, 200 000 Punkte) | Selbstschnitt-Prüfung, Versatz-Schnitte, Glätten | Rasterzelle aus der mittleren Streckenlänge (1000 mm) → 2000 Strecken je Zelle; Punkt-in-Kontur über alle 200 000 Strecken je gültigem Stück; Douglas-Peucker quadratisch (jeder Punkt bleibt, Bereiche schrumpfen um 1). |
| riesig neben 30 000 winzigen, Streifen + 20 000 winzige (q5, q6) | Art je Kontur, Bahnprüfung, Reihenfolge | Rahmenraster mit groben Zellen (30 000 Rahmen in wenigen Zellen); Punkt-in-Kontur über die ganze Riesenkontur; Reihenfolge: eine Zelle mit 30 000 Startpunkten je Schritt durchsucht. |
| gleicher Rahmen / Eimer (b1, e1) | Doppelte Konturen | Vorfilter nur über die linke untere Ecke: alle Paare mit gleicher Ecke. |
| große G-Code-Texte (q4: 310 MB) | G-Code schreiben | Zahlenformat je Koordinate, nur ein Kern. |
| Abbruch | Phasen ohne Abbruchpunkt | Raster-Aufbau (eine `Add` dauerte Sekunden), Glätten, Selbstschnitt, Deckungsprüfung, Kopie/Rahmen der Konturen, G-Code-Schleife. |

Rust: **nicht eingesetzt.** Die Zeit lag in Algorithmen (Vollbelegung, quadratische Schleifen), nicht in der Rechengeschwindigkeit von C#; nach den Änderungen ist kein Fall mehr durch Rechenleistung begrenzt, für den Rust messbar mehr brächte. Eine zweite Sprache hätte den Vergleich „G-Code byte-gleich“ zusätzlich erschwert.

## Was geändert wurde (Verhalten gleich, nur schneller)
Grundsatz: Jede Änderung liefert **dieselben Treffer in derselben Reihenfolge** bzw. dasselbe Ergebnis wie vorher – nachgewiesen durch Vergleich mit der alten Umsetzung (Tests) und den G-Code-Vergleich über alle Prüferfälle.

1. **`SegGrid` ohne Vollbelegung** (Punkt 5 des Auftrags). Eine Strecke gilt weiter als „in jeder Zelle ihres Rahmens“, wird aber nur bis 64 Zellen fein eingetragen; größere Rahmen kommen in ein grobes Raster mit eigener Stufe je Richtung (höchstens 2 × 2 Einträge). Abfragen liefern exakt dieselben Strecken in derselben Reihenfolge wie vorher (Zelle für Zelle, je Zelle nach Nummer; grobe Strecken an ihrer ersten Zelle einsortiert). Sehr große Abfragerechtecke (mehr Zellen als 2 × Strecken) gehen die Strecken direkt durch – gleiche Treffer, gleiche Reihenfolge. Statt reiner DDA (nur Zellen entlang der Strecke) wurde dieser Weg gewählt, weil DDA die Kandidaten und damit Zählwerte wie das Prüfbudget von „U vor Kerbe“ (`fileWork`) und die Meldungsreihenfolge verändert hätte.
2. **Feineres Prüfraster mit Bezugszelle (`RefGrid`)** für Prüfungen, deren Ergebnis nur von Strecken abhängt, die eine Bedingung erfüllen: Hindernisprüfung der Bahnen (`Nearest`), Selbstschnitt, Schnitte der rohen Versatzlinie, Gültigkeit der Versatzstücke, Engstellen (`Unreached`), Rahmen für die Art je Kontur. Gesucht wird nur dann feiner, wenn das bisherige Raster überladen ist (> 8 Strecken je Zelle); gezählt wird eine Strecke nur, wenn auch das bisherige Raster sie geliefert hätte; Reihenfolge wie vorher, wo sie zählt (Schnittpunkte, nächstes Hindernis bei Gleichstand).
3. **Zu kleine Löcher**: Nach innen kann ein gültiger Bahnpunkt nur in der Kontur und mindestens |Versatz| − 1e-7 von allen Strecken liegen; der Abstand zum Rand ist höchstens die halbe kleinere Rahmenseite. Ist die kleiner, ist das Ergebnis leer wie bisher – jetzt ohne die rohe Versatzlinie zu bauen.
4. **Punkt-in-Kontur über waagrechte Streifen** (`StripIndex`): gleiche Formel und gleiche Eckenreihenfolge wie `CadContour.Contains`/`Inside`, nur die Strecken in Höhe des Punkts. Im Versatz, wo der Punkt nachweislich mindestens den Fräserradius von allen Strecken entfernt ist, darf der günstigere Strahl gewählt werden (Ergebnis dort in Gleitkomma genau und richtungsunabhängig).
5. **Douglas-Peucker**: gleiche Punkte und gleiche Abweichung; der weiteste Punkt eines Bereichs wird über einen Rahmenbaum gesucht (Teilbäume, die sicher kleiner sind, übersprungen; Gleichstand: erster Punkt wie vorher), sehr lange Bereiche in Stücken parallel mit Zusammenführung in Reihenfolge.
6. **Doppelt gezeichnete Konturen**: Vorfilter über alle vier Rahmenwerte statt nur die Ecke – dieselben Kandidaten, die die Rahmenprüfung bestehen.
7. **Reihenfolge vieler Bahnen**: Zellen mit mehr als 64 Startpunkten werden fein unterteilt; gesucht wird das kleinste (Abstand², Nummer) – genau das, was die Schleife über die Zelle lieferte.
8. **G-Code** je Bahn in Blöcken zu 512 auf allen Kernen geschrieben und in Reihenfolge angehängt (Text, Steg-Hinweise und erster Fehler wie vorher).
9. **Verketten der Versatzstücke**: „erstes unbenutztes Stück“ je Startpunkt über einen Zeiger, nahe Startpunkte (< 1e-7) über ein Raster statt Durchsuchen aller Stücke (Kamm innen 50 000: 92 s → 33 s) – gleiche Wahl.
10. **Hindernisprüfung (`Nearest`)**: kleinster Abstand je Strecke ohne Trefferliste und Sortierung; bei Gleichstand gewinnt, was das bisherige Raster zuerst geliefert hätte.
11. **Abbruch**: Abbruchpunkte in Kopie/Rahmen der Konturen, Deckungsprüfung, Selbstschnitt, Glätten, Art je Kontur (je Kontur), Schnitte, Ketten, Reihenfolge, G-Code-Schleife.

Unverändert: Schutzbereich, U vor Kerbe (inklusive Prüfbudget – die Zählwerte sind gleich, weil das Raster dieselben Kandidaten liefert), Linien-Stöße, alle Meldungen.

## Messtabelle alt/neu
Vollständig in `reports/T-0758/messtabelle.md` (32 Fälle, nur `CncCam.Generate`, Grenze 150 s, Hintergrundlast ±40 %). Auszug:

| Fall (Auftragspunkt) | 0.9.108 | T-0758 | G-Code |
|---|---:|---:|---|
| p_diag 20/50/100/200 Linien (1) | 7 / 21 / 40 / 105 s | 0,17 / 0,17 / 0,19 / 0,23 s | gleich |
| p_diag 500 Linien + 2000 Löcher / 2000 + 2000 (1) | > 150 s / > 150 s | 1,7 s / 2,2 s | alt ohne Ergebnis |
| p1_diag_gravur_200 / _1000 (1) | 148 s / > 150 s | 1,7 s / 2,8 s | gleich / alt ohne Ergebnis |
| q_diag_500_5000, r2_diag_1000 (1, 2) | 33 s / 45 s | 1,2 s / 2,0 s | gleich |
| s_diagonal_kollinear 3000 / 10 000 (6) | 24 s / 24 s | 0,19 s / 0,28 s | gleich |
| k2_lochblech_200 (2) | > 150 s | 5,4 s | alt ohne Ergebnis |
| Kamm 50 000 / Kamm innen 50 000 / n8 (3) | 116 s / 107 s / 4,0 s | 26,5 s / 28,9 s / 1,0 s | gleich |
| q5 riesig + 30 000 winzig / q6 Streifen + 20 000 (3) | > 150 s / > 150 s | 12,4 s / 4,7 s | alt ohne Ergebnis |
| x3a / x6c Stoß (5) | > 150 s / > 150 s | 1,5 s / 10,4 s | alt ohne Ergebnis (neu: „keine fräsbare Kontur“ wie bei 0.9.108 mit mehr Zeit) |
| g2 50 000 Kreise, q4, e1, k2s, k4, f_viele, b1 | 6–26 s | 3–29 s | gleich, im Rahmen der Messschwankung (Zeit liegt im Versatz vieler großer Kreise bzw. im G-Code-Umfang) |
| b2 gleicher Rahmen 20 000, e0 100 000, z1 Zickzack 100 000 | > 150 s | > 150 s | beide ohne Ergebnis – offen |

Abbruch (Nachlauf nach Abbruch-Anforderung, `--cancel` nach 0,3/1/3 s): neu in allen 32 Fällen höchstens **0,59 s** (z1), meist unter 0,1 s. 0.9.108: g2 50 000 Kreise 14,5 s, Kamm 50 000 11,5 s, p_diag_500 **197 s**, q4 2,1 s, q6 2,6 s.

Zeittests im Repo (Grenzen fest, alle auf 0.9.108 rot – `rohlogs/rot_auf_main.log`): 200 Linien < 3 s (alt 65,6 s), 2500 zu kleine Löcher < 3 s (alt 26,4 s), Selbstschnitt Kamm 50 000 < 4 s (alt 7,3 s), Abbruch < 2 s (alt 2,8 s Nachlauf beim Kamm 20 000), sieben Vergleichsfälle zusammen < 20 s (alt über 10 min: Gravur 438 s, kollineare Kante 152 s).

## G-Code-Vergleich über alle Altfälle
`reports/T-0758/vergleich.md`: alle Zeichnungen aus 161 Fallordnern der Prüfrunden 2–21 (Weg der Oberfläche `CncJob.ImportDrawing`, Grenze 120 s), main 0.9.108 gegen Endstand 32ecd7545. **2807 Fälle: 2735 byte-gleich (G-Code, alle Meldungen in Reihenfolge, Konturen, Erfolg/Fehler), 72 abweichend – ausnahmslos Fälle, in denen 0.9.108 die 120-s-Grenze erreichte und der neue Stand fertig wurde** (60 mit G-Code, 12 mit der Meldung „Es blieb keine fräsbare Kontur …“). Kein anderer Unterschied, keine Rundungsunterschiede, kein Fall, in dem nur der neue Stand die Grenze erreicht. Nicht verglichen: 2 Fälle (k5_dicht_500_60000 hängt in beiden Ständen beim Lesen/Verketten; pf1 = Kopie eines verglichenen Falls). Zusätzlich sieben eigene Vergleichsfälle mit festen Prüfwerten aus 0.9.108 im Test `Same_gcode_as_before`.

## Tests
- Neu `T0758_CncCamTempo` (8 Tests): Streckenraster gegen die alte Umsetzung (gleiche Treffer, gleiche Reihenfolge, Any bricht an gleicher Stelle ab; 3 Zellgrößen × 2000 Abfragen), lange Strecke belegt keine Feinzellen, Douglas-Peucker gegen die alte Umsetzung (gleiche Punkte und Abweichung, auch Kamm, Rauschen, 150 000-Punkte-Kreis), Selbstschnitt gegen die alte Umsetzung (300 Kämme mit Berührungen knapp unter/über 1e-7), G-Code byte-gleich in sieben Fällen (Prüfwerte aus 0.9.108) unter 20 s, 200 Linien < 3 s, 2500 zu kleine Löcher < 3 s, Abbruch < 2 s. Alle 8 grün; die Zeit-/Abbruch-Tests auf 0.9.108 rot (Rot-Grün).
- Core gezielt (Endstand): `dotnet test --filter T0747|T0758|T0266|MeshEditTests` → 143 Tests, 142 bestanden, 1 Fehler: `T-0747-fix20 Edge_lines_fast` (oben 15,5 s > 12 s) im Gesamtlauf unter Last; einzeln wiederholt grün (oben 8,4 s und 6,7 s), auf main unter gleicher Last rot (13,3 s). Die Zeit dieses Tests liegt vor allem im Lesen/Verketten. Ein früherer Lauf (vor den letzten zwei Commits) hatte 135/135 grün.
- App gezielt: SPEC14_Ui 2/2, T0747_DxfUi 2/2, T0747_Fix3RaceUi 4/4, T0747_Fix4RaceUi 3/3, T0747_FixUi 3/3, Studio3DMeshEditUiTests 5/5 – alle grün (`rohlogs/app_tests.log`).
- Build `dotnet build Trendo.sln -c Release`: 0 Fehler. Kein Volltest (macht die Hauptsitzung).

## Abnahmekriterien (für dieses Paket)
- 1 (aus leerer Vorlage echtes Ergebnis): NICHT BETROFFEN – Leistungsauftrag ohne neue Oberfläche; der Weg der Oberfläche (`CncJob.ImportDrawing`) liefert unverändert dasselbe.
- 2 (Fremddatei → Export, Beleg mit SHA-256): TEILWEISE – 2807 Prüfer-DXF über den Weg der Oberfläche gelesen und gefräst, G-Code byte-gleich zu 0.9.108 (Belege + SHA-256 unter `reports/T-0758/`); ein Fremdprogramm zum Öffnen des G-Codes ist auf diesem PC nicht vorhanden (wie T-0747).
- 3 (Prozessabbruch ≤ 30 s Verlust): NICHT BETROFFEN; der Abbruch der Fräsbahn-Erzeugung wirkt jetzt in unter 0,6 s (vorher bis 197 s).
- 4 (KI-Fachaufgabe): NICHT BETROFFEN.
- 5 (Test bis zur echten Datei): VOLLWERTIG für den Leistungsteil – echte DXF → Konturen → G-Code im Test und im Vergleichslauf.
- 6 (Bedienung): NICHT BETROFFEN (keine Oberflächenänderung, darum auch kein Bildnachweis nötig).

## Was NICHT geprüft wurde
- Kein Volltest (Hauptsitzung). Keine Maschine angesteuert, kein Fremd-G-Code-Betrachter vorhanden.
- Zeiten unter Hintergrundlast gemessen (bis ±40 %); keine Messung auf dem Pi oder einem schwächeren PC.
- Linux/Pi-Edition nicht geprüft (nur .NET-Code, keine plattformabhängigen Teile geändert).
- Der Abbruch beim Lesen und Verketten (`CadDxf.Read`, `CadContours.Build`) wurde nicht geändert (Bereich T-0759/T-0760).

## Offene Punkte
- **Lesen/Verketten ohne wirksamen Abbruch:** `pruef21-T-0747-leistung/c21e/k5_dicht_2000_30000` und `k5_dicht_500_60000` hängen in 0.9.108 **und** im neuen Stand über 15 min bis über eine Stunde, bevor `Generate` überhaupt beginnt (nur Lesen + Konturen: nach 900 s noch ohne Ergebnis). Gehört zu T-0759 (CadContours) bzw. T-0760 (DXF-Lesen) – bitte dort übernehmen.
- **b2_gleicher_rahmen_versteckt_20000/30000:** Doppelprüfung (`Coincident`) für 20 000 fast gleiche Umrisse; jedes Paar besteht Rahmen- und Flächenvorprüfung, je Paar werden zwei Raster gebaut – quadratisch in der Zahl der Umrisse, alt wie neu über 150 s. Ein Versuch (kleine Züge ohne Raster) brachte im Messlauf keinen Durchbruch und wurde nicht übernommen; nötig wäre ein anderer Vorfilter (Formmerkmale), der die Ergebnisgleichheit nachweisbar hält.
- **e0_kontrolle_verteilt_100000, z1_zickzack_senkrecht_100000:** weiterhin über 150 s (alt wie neu); Abbruch neu in 0,1–0,6 s.
- **Kamm 50 000 (26 s):** Rest ist Douglas-Peucker mit 200 000 behaltenen Punkten (jeder Bereich schrumpft um einen Punkt – mit der geforderten Ergebnisgleichheit grundsätzlich quadratisch; jetzt auf allen Kernen).
- Rust: nicht eingesetzt (siehe oben) – keine Folgeaufgabe.

## Geänderte Dateien
- `src/Trendo.Core/W6/CncCam.cs`
- `tests/Trendo.Core.Tests/CncCamTempoTests.cs` (neu)
- `tasks/T-0758-fraesen-tempo.md`, `tasks/T-0758-fraesen-tempo.bericht.md`
- `reports/T-0758/` (Messtabelle, Vergleichsergebnis, Rohlogs, SHA-256)

**Status:** Leistungsteil umgesetzt und belegt (deutliche Beschleunigung, Abbruch < 0,6 s, G-Code unverändert über 2807 Altfälle). Offen: Abbruch beim Lesen/Verketten (T-0759/T-0760), b2/e0/z1.
