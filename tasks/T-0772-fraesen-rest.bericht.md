# T-0772 – Bericht: Fräsen- und DXF-Restpunkte aus 0.9.109

**Auftrag:** `tasks/T-0772-fraesen-rest.md` · **Branch:** `claude/T-0772-fraesen-rest` (Basis main 542dc1ce7 = 0.9.110) · Belege `reports/T-0772/` (BELEG.md, Rohlogs, SHA256SUMS.txt)

Vorweg: Die Prüfdateien der Prüfrunden (k5_dicht, b2, e0, z1, p1) liegen nicht mehr auf dem PC. Sie sind nach Namen und Meldungen aus `reports/T-0758` nachgebaut (`reports/T-0772/werkzeuge/erzeuge772.py`). Alle Zahlen gelten für diese Nachbauten; gemessen unter Fremdlast (parallele Testsitzung), daher Median aus 3 abwechselnden Läufen alt/neu.

## Ergebnis je Punkt

| Punkt | Vorher (Beleg) | Ursache | Jetzt | Test / Beleg |
|---|---|---|---|---|
| 1 k5_dicht hängt | 30 000 dichte Linien: Konturen > 400 s (früher 290 s), 60 000 nicht abgewartet | Spalten-Überbrücken (`CadContours.Chain`, Schritt 3): jede Runde alle Enden, Paare, Sortierung und Mehrdeutigkeit neu – eine Runde je Verbindung → quadratisch | Paare und Zustand einmal, nach jeder Verbindung nur in der Nähe neu bewertet (`CadContoursBridge.cs`). Gleich weite Paare: die Reihenfolge hing bisher vom .NET-Sortierverfahren ab – dafür wird List.Sort genau nachgebildet, aber nur so weit wie nötig (`IntroPrefix`, beim ersten Gebrauch gegen List.Sort geprüft, sonst echtes List.Sort). Arbeitsgrenze 2 Mrd. Schritte → klare Meldung, keine weiteren Spalten. 3000 Linien 5,7 → 1,4 s, 30 000 Linien → 51 s, 60 000 → 97 s mit Meldung; Abbruch 94 ms | `T0772VerkettenTests` (2400 Fälle zeichengleich zur Kopie aus 0.9.110 `CadContoursAlt.cs`; k5 3000 gleich und ≥ 5× schneller, Median aus 3, relativ; 10 000 fertig; Budget + Abbruch), `messung/punkt1-rohlog.txt` |
| 2 b2 / e0 / z1 > 150 s | b2 20 000 > 300 s, b2 2000 13,6 s; e0 100 000 Absturz OutOfMemory; z1 100 000 61 s | b2: Doppelprüfung prüft jedes Paar fast gleicher Umrisse mit zwei Rastern (quadratisch). e0 100 000: G-Code größer als eine .NET-Zeichenkette. z1/e0 sonst: Versatz und Abstandsprüfung je Bahn, linear, kein Algorithmusfehler gefunden | b2: Vorauswahl über einen seltenen Prüfpunkt (Kerbspitze) in einem gemeinsamen Raster – nur Konturen, die ihm nahekommen, können deckungsgleich sein; übrige Prüfung unverändert. 2000: 9,6 s; 20 000: fertig in 256 s (Rest: jeder der 20 000 überlappenden Umrisse liegt bei allen anderen, Abstandsprüfung bleibt quadratisch). e0 100 000: klare Meldung „G-Code würde zu groß“ statt Absturz (39 s). z1/e0 10 000: gleich schnell | `T0772FraesenTests.Doppel_unter_vielen_fast_gleichen`, `messung/punkt2-rohlog.txt`, G-Code-Prüfwerte gleich |
| 3 Gravur p1 5,7 s | p1 1000 Linien + 10 000 Löcher 6,8 s, 200 Linien 3,5 s | Abstandsprüfung der Bahnen: lange schräge Linien liegen im groben Raster und waren bei jeder Abfrage Kandidat (Rahmen über die ganze Platte); lange Abfragen gingen alle Strecken durch | Lange Linien zusätzlich entlang ihres Verlaufs eingetragen, lange Abfragen stückweise, und „nichts Fremdes im Rahmen der Bahn“ als schnelle Absage – Auswertung reihenfolgeunabhängig, gleiche Treffer. 6,8 → 2,9 s, 3,5 → 2,5 s | `T0772FraesenTests.Lange_gravurlinie_abstand`, `messung/punkt3-rohlog.txt` |
| 4 REGION / ezdxf-Linse | nur gemeldet | REGION = ACIS (SAT verschlüsselt bis 2010, SAB binär ab 2013); Linse: Datei ist in beiden Lesarten gültig | **bleibt gemeldet**, Begründung `punkt4/REGION-und-Linse.md`. Python 3.13 vorhanden, **ezdxf nicht** (nichts installiert), FreeCAD 1.1 vorhanden (schreibt keine REGION). REGION: BLOCKIERT mangels echter Dateien/Gegenprüfung; Linse: fehlt keine Software, sondern eine Information in der Datei | `punkt4/werkzeuge.txt` |
| 5 `/shell:powershell` nicht rot | auf main nicht rot | `:` war keine Grenze vor dem Programmnamen | `:` zählt als Grenze; Fund weiter nur mit verschlüsseltem Befehl danach; `--shell`-Ausnahme unverändert | `T0772GefahrenTests`: Rot auf main (`punkt5/rot-auf-main-542dc1ce7.log`, 1 Fehler), grün auf dem Zweig (`punkt5/gruen-core.log`: T0757*/T0755*/T0754*/OsBehavior/W7 u. a. 87 bestanden, 4 übersprungen (Linux), 0 Fehler) |

Ergebnisgleichheit gesamt: 400 Zufallszeichnungen über `CncJob.ImportDrawing` alt gegen neu: 400 gleich (`vergleich/ergebnis.txt`); alle Messfälle mit Ergebnis in beiden Ständen: gleiche Prüfwerte.

Rust: **nicht eingesetzt** – die Messung zeigte Algorithmusfehler (quadratisch), keine Rechenleistung als Engpass.

## Tests (gezielt, kein Volltest)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core `T0772*`: 8 bestanden (`tests/gruen-T0772.log`).
- Core T0747/T0758/T0759/T0760/T0266 (alle Fräs-/DXF-Klassen): 151, davon 150 bestanden, 1 Fehler: `T0747_FixGegenpruefung.Big_dxf_fast` (Grundriss-Import 14,2 s > 8 s unter Fremdlast). Einzeln wiederholt: bestanden (4,7 s). Der Grundriss-Import ruft den geänderten Code in diesem Test nicht auf (Ebene WAND, keine Raumkonturen); alt gegen neu im Wechsel 1,8/4,8 s gegen 5,2/4,6 s (`tests/grundriss-zeit-alt-neu.log`) – Lastschwankung, kein Rückschritt erkennbar, aber unter Last nicht sauber zu trennen.
- Core CncCam/CncSim/DxfElemente/FraesenSonderfaelle/T0757/T0755/T0754/OsBehavior/W7/NoFixedUser/Cad: 180, 176 bestanden, 4 übersprungen, 0 Fehler (`tests/gruen-core-betroffen.log`).
- App `SPEC14_Ui`, `T0747_DxfUi`, `T0747_Fix3RaceUi`, `T0747_Fix4RaceUi`, `T0747_FixUi`: 14/14 (`tests/gruen-app.log`).

## Korrekturrunde nach Gegenprüfung (fix1)

| Befund | Ursache | Lösung | Beleg |
|---|---|---|---|
| HOCH 1: Bahnstrecke der Länge 0 (Null-Gravur neben Loch, auch drei gleiche Punkte) fand kein Hindernis und wurde gefräst | `RefGrid.Along`: Stückzahl `Ceiling(0 / step)` = 0 → keine Abfrage | Mindestens ein Stück; nicht endliche oder riesige Rechtecke (NaN, ±∞, > 1e12) laufen über den bisherigen Weg (`Plain`), ebenso die schnelle Absage in `Nearest` und die Doppel-Vorauswahl. Beim Durchsehen zusätzlich gefunden: im neuen Überbrücken galten NaN-Abstände als „nah“ (`dist > lim` statt `!(dist <= lim)`) – Paar und Schließen jetzt wie bisher | `T0772Fix1Tests` (Länge 0 mit 2 und 3 Punkten, 1e-12, 1e-9, freie Null-Gravur): grün auf main, **rot auf 8e84f2d44** (2 Fehler), grün jetzt (`fix1/`). Zufallsvergleich `Generate` mit entarteten Strecken (Länge 0, doppelte Punkte, 1e-12, 1e-9) über 2000 Fälle: main gegen neu 2000 gleich, main gegen 8e84f2d44 99 abweichend (`fix1/zufall-*.tsv`). `T0772VerkettenTests` um Länge 0, doppelte Punkte, 1e-12 und NaN erweitert – fand den NaN-Fehler, jetzt 2400 gleich |
| MITTEL 2: Belege ohne `-text` | `.gitattributes` | `reports/T-0772/** -text`, Belege byte-genau neu eingecheckt, SHA256SUMS neu und gegen `git show HEAD:<datei>` geprüft | `SHA256SUMS.txt` |
| MITTEL 3: `tests/gruen-core.log` war ein Zwischenstand mit 3 Fehlern | – | umbenannt in `tests/zwischenstand-vor-endstand-core.log`; echter Lauf des Endstands daneben `tests/gruen-core-endstand.log`, `tests/gruen-app-endstand.log` | siehe unten |
| NIEDRIG 5: einzelne riesige Bahn | `sb.ToString()` im Parallelteil | Längenprüfung vor `ToString`, Speicher-/Grenzfehler dort → Meldung „G-Code würde zu groß“ | Code (`GcodeTooBig`), nicht mit echter Riesenbahn getestet |
| NIEDRIG 6 / 4 | – | Beschreibung `CadContoursAlt.cs` und Messung gleich weiter Spalten ergänzt | oben |

Endstand-Tests: Core T0772*, T0747*, T0754*, T0755*, T0757*, T0758*, T0759*, T0760*, T0266, NoFixedUser: **232 bestanden, 0 Fehler** (`tests/gruen-core-endstand.log`; auch `Big_dxf_fast` und der Papierkorb-Test T-0754 grün). App `SPEC14_Ui`, `T0747_DxfUi`: 4/4 (`tests/gruen-app-endstand.log`).

## Abnahmekriterien
1 Leere Vorlage → Ergebnis: NICHT GETESTET (nicht Gegenstand). 2 Fremddatei → Export im Fremdprogramm: NICHT GETESTET (nur eigene Nachbau-Dateien; Original-Prüfdateien fehlen). 3 Abbruch ≤ 30 s Verlust: TEILWEISE (Abbruch beim Einlesen jetzt < 0,1 s; Wiederherstellung nicht berührt). 4 KI: NICHT GETESTET. 5 Klick bis Datei: TEILWEISE (Weg `CncJob.ImportDrawing` wie die Oberfläche, nicht über die Oberfläche). 6 Bedienung: nicht berührt.

## Nicht geprüft
- Originaldateien der Prüfrunden (fehlen) – nur Nachbauten.
- Ruhige Messung ohne Fremdlast; Pi/Linux.
- k5 mit 60 000 Linien gegen alt (alt nicht abgewartet).
- Volltest (macht die Hauptsitzung).

## Offene Punkte
- b2 20 000: Abstandsprüfung und Tiefe bei 20 000 übereinanderliegenden Umrissen bleiben quadratisch (≈ 4 min).
- z1 100 000 / e0 100 000: kein Algorithmusgewinn gefunden; e0 100 000 bricht jetzt mit Meldung ab.
- Viele exakt gleich lange Spalten (Rasterzeichnungen) laufen über die genaue Runde (Nachbildung der Sortierung, nur so weit nötig). Messung der Gegenprüfung: 2000 Rechtecke aus Linien mit gleich großen Spalten 19,8 s → 1,0–1,5 s; ab etwa 15 000 solchen Rechtecken greift die Arbeitsgrenze (Meldung, keine weiteren Spalten).
- REGION: echte Dateien und ein Gegenprüf-Programm nötig; ezdxf-Rundreise braucht `pip install ezdxf` durch den Nutzer.

## Berührte Dateien
- `src/Trendo.Core/Cad/CadContours.cs` (partial, Schritt 3 ausgelagert), neu `src/Trendo.Core/Cad/CadContoursBridge.cs`
- `src/Trendo.Core/W6/CncCam.cs` (DupSelector, Nearest-Absage, RefGrid.AnyNear/MinNear/AnyInRect/Along, SegGrid.AnyMixed/BigList/CellCount, MaxGcodeChars)
- `src/Trendo.Core/Sys/Gefahren.cs` (eine Regex)
- neu `tests/Trendo.Core.Tests/T0772VerkettenTests.cs`, `T0772FraesenTests.cs`, `T0772GefahrenTests.cs`, `T0772Fix1Tests.cs`, `CadContoursAlt.cs` (Vergleich: `Chain` samt Hilfsfunktionen `Junctions`, `Dist`, `DupTol` unverändert aus 0.9.110 kopiert, die übrigen Teile von `CadContours` entfernt)
- `installer/neuerungen/0.9.111.md` (neu, nur dieser Abschnitt), `tasks/T-0772-*`, `reports/T-0772/`

**Status:** umgesetzt und gezielt getestet; Punkte 1, 3, 5 erledigt; Punkt 2 teilweise; Punkt 4 begründet gemeldet.
