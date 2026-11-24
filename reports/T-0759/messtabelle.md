# T-0759: Laufzeit und Abbruch

Rechner mit 16 Kernen, Windows 11. Während der Messungen liefen auf dem Rechner zeitweise Testläufe anderer Sitzungen; Einzelwerte schwanken deshalb
bis etwa Faktor 2. Belastbar sind nur die abwechselnd (758, neu, 758, neu …) gemessenen Paare.

## Gesamtlauf aller 1 063 Prüfer-Altfälle (erster Lauf, nacheinander je Satz main → 758 → neu)

| Stand | Summe Fräsbahn-Erzeugung |
|---|---|
| main (0.9.108) | 2 293 s (davon 12 Fälle an der Zeitgrenze) |
| 758 (cf3a5ba1b) | 116,6 s |
| neu (erster Stand) | 143,6 s (+23 % gegenüber 758) |

Rohwerte je Fall: `rohlogs/zeiten_main_758_neu_lauf1.txt` (Spalten: Satz, Fall, ms main, ms 758, ms neu). Der Endstand wurde noch einmal ganz gerechnet
(`rohlogs/zeiten_neu_endstand_unter_last.txt`), dabei lief aber ein Testlauf einer anderen Sitzung mit; die Summe ist deshalb kein Vergleichswert.

## Auffällige Fälle, abwechselnd gemessen

| Fall | 758 | neu | Befund |
|---|---|---|---|
| e2_kante_4000 (pruef20) | 19,0 / 17,8 / 37,9 s | 17,3 / 17,7 s | kein Unterschied – der Wert 12,7 → 22,3 s im Gesamtlauf war Last, nicht Code |
| p1_diag_gravur_1000_10000 (pruef19) | 1,8 / 2,0 / 1,9 s (ruhig), 3,5 / 3,3 s (unter Last) | 5,6 / 5,8 s (ruhig), 7,6 / 7,6 s (unter Last) | echt langsamer, etwa +3,7 s |

Ursache p1_diag_gravur_1000 (Zeitstempel je Abschnitt, nur im Messstand): Verbund-Analyse der offenen Linien (`AnalyzeOpen`) ≈ 1,5 s und Bereichsprüfung
(`Enclosures`) ≈ 2,5 s. Die 1000 Linien laufen alle durch einen Punkt (Plattenmitte), das sind rund 500 000 Kreuzungen, dazu 10 000 Kreise, die die
Linien berühren. Beides ist echte Arbeit der neuen Regeln, kein doppelter Aufruf und kein fehlendes Raster (beide Abschnitte arbeiten mit Raster und
Budget). Eine günstige Abkürzung, die das Ergebnis sicher gleich lässt, gibt es nicht; mit 5000 Linien greift die Höchstzahl der Berührstellen (1 Mio.)
und der Fall läuft wie in 758 (2,7 s beide). Laut Koordinator angenommen (weiterhin weit schneller als main mit 122 s).

r2_diag_3000_loch25x4000: 758 4,4 s, neu 7,2 s (unter Last) – Mehraufwand der neuen Hüllprüfung für die 25 Löcher mit je 4000 Punkten.

## Abbruch (Pflicht vor dem Push): neu gegen 758, Endstand

Messprogramm `--abbruch`: Import mit Fräsbahn-Erzeugung starten, nach der Wartezeit abbrechen, Nachlauf bis zur `OperationCanceledException` messen
(Fräser 6 mm). Rohwerte: `rohlogs/abbruch_758_neu_endstand.txt`.

| Fall | Abbruch nach | 758 Nachlauf | neu Nachlauf |
|---|---|---|---|
| p1_diag_gravur_5000_10000 | 0,3 / 1 / 2 / 4 s | 36 / 38 / 16 / 5 ms | 14 / 12 / 24 / 2 ms |
| p1_diag_gravur_1000_10000 | 0,3 / 1 / 2 / 4 s | 8 / 64 / 4 ms / fertig | 36 / 11 / 19 / 31 ms |
| r2_diag_3000_loch25x4000 | 0,3 / 1 / 2 / 4 / 8 s | 34 / 20 / 807 / 107 ms / fertig | 8 / 31 / 1 / 35 / 17 ms |

Höchster Nachlauf neu: 36 ms (Ziel unter 2 s, nicht langsamer als 758). Der gemeldete Rückschritt (p1_diag_gravur_5000: 274 s statt 125 s auf main)
ist im Endstand (Zweig auf Basis cf3a5ba1b) nicht mehr nachzustellen: Der ganze Import ohne Abbruch dauert 6,0 s (758: 6,2 s), der Abbruch wirkt in höchstens 24 ms.
Zusätzlich grün: `T0758_*.Cancel_quickly` (Kamm 20 000, 20 000 Kreise, 400 Diagonalen mit 1500 Löchern: Nachlauf unter 2 s).

## T-0759-fix1

e2_kante_3000 (pruef20), abwechselnd gemessen (758 / fix1): 13,6 / 14,9 s, 13,6 / 13,5 s, 12,4 / 11,6 s – kein messbarer Unterschied; die
gemeldeten 9,5 → 10,2 s liegen in der Schwankung des Rechners. Gesamtlauf aller 1 103 Fälle (nacheinander je Satz 758 → fix1, unter fremder Last):
758 160,0 s, fix1 155,5 s (`rohlogs/fix1_zeiten_758_fix1.txt`). Abbruchpfade sind unverändert (neue Prüfungen haben Budgets und Abbruchpunkte).
