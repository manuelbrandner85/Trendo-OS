# T-0759: G-Code-Vergleich über alle Prüfer-Altfälle

Grundlage: 39 Fallsätze aus den Prüfrunden 9–21 von T-0747 (Liste in `ordner.txt`), 1 063 Fälle. Gerechnet mit demselben Messprogramm (`rohlogs/harness_Program.cs`) für
`main` (0.9.108, 24bb4f70e), `758` (cf3a5ba1b, Basis dieses Zweigs) und `neu` (dieser Zweig). Verglichen wird der G-Code byte-genau; die Prüfprogramme
der Prüfer (`gen_p8.py check`, `check20.py`) liefen auf 758 und neu.

Kurzform in den Tabellen: `a2i2` = 2 Außen-, 2 Innenbahnen; leer = kein G-Code (Fehler oder Zeitüberschreitung); `-B` = Bahnen auf der Linie.

## Endstand neu gegen 758: 13 Fälle verschieden, alle anderen 1 050 byte-gleich

| Satz | Fall | 758 | neu | Begründung |
|---|---|---|---|---|
| pruef15 casesr15 | r_rahmen_0_spalt0p0 | kein G-Code | a2i2 | Zeichnungsrahmen mit Schriftfeld erkannt und nicht gefräst (`DropFrame`); vorher blieb wegen des Rahmen-Linienverbunds nichts übrig. Prüfer: ok |
| pruef15 casesr15 | r_rahmen_RAHMEN_spalt0p0 | kein G-Code | a2i2 | wie oben (Rahmen auf Ebene RAHMEN). Prüfer: ok |
| pruef15 casesr15 | r_rahmen_RAHMEN_spalt0p05 | kein G-Code | a2i2 | wie oben, Rahmen mit 0,05-mm-Spalt. Prüfer: ok |
| pruef16 cases16 | d1_bem_lochabstand_gap0p0 | a1 | a1i2 | Bemaßung auf Ebene BEM ist keine Kante (Baum aus Hilfslinien, kein Hindernis): die zwei Bohrungen werden gefräst. 758: „Loch nicht als Loch gefräst“. Prüfer: ok |
| pruef16 cases16 | d1_bem_lochabstand_gap0p625 | a1 | a1i2 | wie d1. Prüfer: ok |
| pruef16 cases16 | d2_bem_breite_aussen_gap0p625 | i1 | a1i1 | Bemaßung außen an der Platte macht die Platte nicht mehr „zu nah“: Außenkontur wird gefräst |
| pruef16 cases16 | d3_lineplatte_bem_gap0p625 | i1 | a1i1 | wie d2 |
| pruef16 cases16 | m1_mittelkreuz_4linien | a1 | a1i1 | Mittelkreuz auf Mittellinien-Ebene (Baum) ist keine Kante: Bohrung wird gefräst. Prüfer: ok |
| pruef16 cases16 | m2_mittellinie_ueber_rand_stoss | i1 | a1i2 | Mittellinie über den Rand: Platte und Bohrung gefräst. Prüfer: ok (758: Befund) |
| pruef18 cases18 | b1_bruecke_zwei_platten | a1i2 | i2 | Zwei Linien zwischen zwei Platten schließen einen Bereich ein (neue Regel c): beide Platten ausgelassen. 758 hatte den Kreis in der Brücke **als Außenkontur** gefräst (Prüfer-Befund „LOCH AUSSEN GEFRÄST“) – das war der Fehler. Prüfer: ok |
| pruef18 cases18 | x1_u_ueberstand_kreuzend | a1i1 | i1 | U aus sich kreuzenden Überständen vor einer Kerbe: Platte ausgelassen; 758 fräste das Loch im U außen. Prüfer: ok |
| pruef18 rueck_sehne | s7_sehne_andere_ebene | a1i1 | i1 | Sehne vor der Kerbe auf anderer Ebene zählt jetzt wie auf derselben Ebene: Platte ausgelassen, Bohrung bleibt |
| — | (r2_diag_3000_loch25x4000) | a1i25 | a1i25 | im ersten Lauf verschieden (neu: a1, 25 Löcher ausgelassen), im Endstand **gleich** – siehe unten |

Prüfprogramme: neu hat keinen Befund, den 758 nicht hat. Weggefallen sind die Befunde von 758 in pruef15 (3 von 3 → 0), pruef16 (5 von 11 → 1) und pruef18 (2 von 15 → 0).
Der verbleibende Befund in pruef16 ist u1_u_stoss_loch_in_kerbe (in main, 758 und neu gleich): Der Kreis liegt in der Kerbe außerhalb des U-Umrisses und ist dort ein eigenes Teil – im Bericht T-0747 so begründet und angenommen; die Erwartung „notout“ des Prüfers weicht davon ab. Daneben geänderte Meldungstexte ohne G-Code-Unterschied (pruef14, pruef16, pruef18): Konturen, die vorher als
„offene Kontur ausgelassen“ gemeldet wurden, heißen jetzt „schließen zusammen mit Kontur … einen Bereich ein“.

## Endstand neu gegen main: dieselben 12 Fälle wie oben plus 12 Leistungsfälle

In 12 Fällen aus pruef19 (cases19, 19b, 19c) liefert `main` nach 120–345 s keinen G-Code (Zeitgrenze des Messprogramms), 758 und neu liefern ihn
(byte-gleich zwischen 758 und neu; r2_diag_3000 im Endstand eigens nachgeprüft). Das ist der Gewinn aus T-0758, nicht aus T-0759.

## r2_diag_3000_loch25x4000: Löcher bleiben (Entscheidung des Koordinators, Punkt 1)

Im ersten Lauf ließ neu alle 25 Löcher aus. Ursache: Die 3000 Diagonalen liegen 0,1 mm auseinander, ihre Enden verketten sich (Abstand ≤ 0,1 mm), der Verbund gilt
als „schließt für sich einen Bereich ein“. Der Schutz (`Protect`) ließ dann alles aus, dessen Rahmen ganz im **Rahmen** des Verbunds liegt (Regel 1) – die Löcher liegen
im Rahmen (0…980 mm), aber nicht in der Hülle der Linien (x + y ≤ 980, Löcher bei x + y ≥ 1 038). Bei 1000 Linien (0,3 mm Abstand) verketten sich die Enden nicht,
deshalb ging der Fall durch.

Begründung, warum die Löcher sicher Löcher bleiben: Alles, was die Linien eines Verbunds **allein** einschließen, liegt in ihrer konvexen Hülle. Eine geschlossene
Kontur, die die Hülle (samt 0,1 mm Kettentoleranz) weder berührt noch schneidet, kann in keinem solchen Bereich liegen; ihre Lesart (Loch oder Teil) hängt nur von
den geschlossenen Konturen um sie ab. Bereiche aus Linien **und** geschlossenen Konturen prüft Regel c) getrennt. Umgesetzt: `OutsideHull` als Ausnahme im Schutz der
Verbünde ohne Stoß; jede Ecke außerhalb der Hülle, keine Strecke ≤ 0,1 mm an einer Hüllkante, Arbeitsbudget 20 Mio., sonst vorsichtig auslassen.
Test: `T0759_FraesenSonderfaelle.Dense_engraving_keeps_holes_outside_hull` (rot ohne die Ausnahme, grün mit; Gegenproben „Bohrung zwischen den Linien“ und „#“).

## Zusätzlich gefunden: Linien, die Löcher durchqueren (T-0758-Tests)

Die Klassen `T0758_*` liefen im ersten Stand nicht und zeigten danach zwei Fehler: `diagonal_60_loecher_50` (Gravurlinien von Rand zu Rand **durch** Bohrungen) ergab
„keine fräsbare Kontur“, weil zwei Linien durch dieselben zwei Löcher als „Brücke zwischen zwei Konturen“ zählten. Eine Linie, die ein Loch ganz durchquert, liegt dort
in Luft und trennt nichts; Regel c) zählt solche Anschlüsse jetzt nicht mehr (Rand X – Stück in X, das in Luft liegt – Rand X). Ein Überstand, der in X endet, und ein
Durchgang durch Material (Brücke, deren Linien durch die Platten laufen) zählen weiter. Ergebnis: G-Code wieder byte-gleich wie 0.9.108; Test
`Lines_through_holes` (rot ohne, grün mit; Gegenfälle Langloch und Brücke durch Platten).

## Gegenprüfung behoben (T-0759-fix1): Lauf gegen 758 über 1 103 Fälle

Zusätzlich zu den 39 Sätzen laufen die 40 Gegenfälle der Prüferin (`pruef1-T-0759/cases`, Prüfskript `chk.py`). Rohwerte: `rohlogs/fix1_*`.
Gegen 758 (cf3a5ba1b) verschieden: die 9 Fälle aus pruef16/pruef18 wie oben (unverändert begründet) und 11 Gegenfälle der Prüferin:

| Fall (pruef1-T-0759) | 758 | 671475769 | fix1 | Begründung |
|---|---|---|---|---|
| a7_mittelkreuz_ueber_rand | kein G-Code | a1i1 | a1i1 | Mittelkreuz auf MITTE über den Rand ist keine Kante |
| f1_platte_ebene_rahmen_eckausschnitt | i2 | **a2 (Löcher außen)** | kein G-Code | Rahmen vermutet (Ebene „Rahmen“), aber kein Text und nicht unterteilt: Konturen darin ausgelassen, Meldung „Rahmen oder Platte nicht eindeutig“ |
| f1c_platte_ebene_frame_linien | i2 | **a2 (Löcher außen)** | kein G-Code | wie f1 (Ebene „FRAME“, 4 Einzellinien) |
| f2_a4_platte_ebene0_eck180 | i2 | **a2 (Löcher außen)** | kein G-Code | wie f1 (Normgröße A4 + 180-mm-Feld reicht allein nicht) |
| f2b_a3_platte_heftrand | i2 | **a2 (Löcher außen)** | kein G-Code | wie f1 (A3 mit Heftrand) |
| f4_rahmen_mit_innenplatte_loch | a1i1, **Loch außen** | a1i1 (Rahmen erkannt, Loch innen) | kein G-Code | wie f1 – Rahmen ohne Text/Unterteilung ist nicht zweifelsfrei; 758 fräste das Loch der Innenplatte außen |
| o1_hash_schliesst_c_tasche | a1, **Loch außen** | kein G-Code | kein G-Code | „#“ schließt mit der C-Tasche einen Bereich ein (Regel c, schon 671475769) |
| o2_hash_spalt005 | a1, **Loch außen** | kein G-Code | kein G-Code | wie o1 mit 0,05-mm-Spalt |
| o3_hash_mit_bulgeplatte | a2, **Loch außen** | kein G-Code | kein G-Code | wie o1 |
| t2_zwei_linien_durch_loch_bruecke | a2, **Insel außen** | a2, **Insel außen** | a1 (Insel ausgelassen) | zwei Linien quer über ein Loch mit Insel können einen Steg bilden – neu erkannt (fix1) |
| t6_freies_teil_mit_loch_linie | a1, **Insel außen** | a1, **Insel außen** | kein G-Code | wie t2 |

f1b (Ebene „Rahmen-Teil 1“) und f3 (ohne Hinweis) sind in 758 und fix1 gleich (Platte, Löcher innen). Der Name zählt nur noch als ganzes Wort.

**Rotlauf auf 671475769** (Prüfskript der Prüferin, `rohlogs/fix1_rotlauf_pruefin_758_671475769_fix1.txt`): 671475769 hat 8 Fälle mit Loch außen
(f1, f1b, f1c, f2, f2b, a5, t2, t6) und verliert in 7 Fällen die ganze Platte (a1 ACHSE/CENTER/Mitte/BEM/Achse 1, a2 Dimension 3mm, a3).
fix1: **kein Fall mit Loch außen**, alle a1/a2/a3-Platten samt Löchern gefräst.

Prüfprogramme über alle Sätze: 758 80 Befunde, fix1 74. Kein neuer Befund. Weggefallen sind 4 in pruef16, 2 in pruef18 und 6 Fälle mit Loch außen bei der Prüferin.
Die übrigen Befunde sind in beiden gleich (nur andere Laufzeit im Text). r_rahmen (pruef15) bleibt wie in 758 ohne G-Code, jetzt mit der Meldung
„Rahmen oder Platte nicht eindeutig“ (die Prüferdateien haben keinen Text im Schriftfeld).

## Zweitprüfung behoben (T-0759-fix2): Zeichnungsrahmen wird nie mehr entfernt

Lauf gegen 758 über 1 109 Fälle (39 Sätze, 40 Gegenfälle aus `pruef1-T-0759`, 6 aus `pruef2-T-0759/cases4`). Rohwerte: `rohlogs/fix2_*`.
Unterschiede: die 9 begründeten aus pruef16/pruef18, die 11 aus der Gegenprüfung (Tabelle oben, dort jetzt alle Rahmen-Fälle „kein G-Code“) und:

| Fall (pruef2-T-0759/cases4) | 758 | f3d703507 (laut Prüfung) | fix2 | Begründung |
|---|---|---|---|---|
| g1_platte_rahmen_gravurtext_eckfeld | i2 | **Löcher außen** (Rahmen „erkannt“) | kein G-Code | Rahmen-Verdacht: nichts gefräst, Meldung mit Rat |
| g2_platte_rahmen_text_ebene0 | i2 | **Löcher außen** | kein G-Code | wie g1 (Text auf Ebene „Gravur“) |
| g3_a4_ebene0_text_mtext | i2 | **Löcher außen** | kein G-Code | Verdacht nur über die Blattgröße: Rat ohne „Ebene umbenennen“ |
| g4_rahmen_linien_frame_text | i2 | **Löcher außen** | kein G-Code | wie g1 (FRAME aus Einzellinien, „1:1“) |
| g5_platte_rahmen_text_ausserhalb_feld | i2 | nicht gemessen | kein G-Code | Rahmen-Verdacht (Ebene „Rahmen“ mit Eckfeld) |
| g6_echter_rahmen_scheibe | a1i2 | nicht gemessen | kein G-Code | echter Rahmen: wie r_rahmen nichts gefräst; Weg: Rahmen-Ebene ausblenden |

Prüfskript der Prüferin über alle Gegenfälle: 758 7 Fälle mit Loch außen, fix2 **0**. Gegenüber 758 verlieren nur Fälle mit Rahmen-Verdacht
(f1, f1c, f2, f2b, f4, g1–g6) ihren G-Code, dazu wie schon in fix1 die Fälle, in denen 758 ein Loch außen fräste (o1–o3, t6) und t2 (Insel ausgelassen).
Prüfprogramme: 758 80 Befunde, fix2 74, kein neuer. Rotlauf der neuen Tests auf f3d703507: 3 rot (`rohlogs/fix2_rotlauf_f3d7035.txt`), auf fix2 alle grün.
