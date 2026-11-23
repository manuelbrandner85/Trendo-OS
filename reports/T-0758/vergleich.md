# T-0758 – G-Code-Vergleich über alle Altfälle der Prüfer

Alle Zeichnungen (DXF/SVG) aus den Fallordnern der Prüfrunden 2–21 von T-0747 (161 Ordner, Liste in `ordner.txt`) wurden mit dem Weg der Oberfläche (`CncJob.ImportDrawing`: Lesen, Konturen, Fräsbahnen; Einstellungen aus `.set.json`; Abbruch nach 120 s) zweimal gerechnet: mit main 0.9.108 (24bb4f70e) und mit dem Endstand dieses Zweigs (32ecd7545). Verglichen wurden G-Code byte-genau, alle Meldungen (Text und Reihenfolge), Erfolg/Fehler und die gelesenen Konturen (`rohlogs/vergleich_cmp.py`).

**Ergebnis: 2807 Fälle, 2735 byte-gleich, 72 abweichend – alle 72, weil 0.9.108 die 120-s-Grenze erreichte (OperationCanceledException), der neue Stand aber fertig wurde.** Kein Fall, in dem der neue Stand anderen G-Code oder andere Meldungen liefert als 0.9.108 mit Ergebnis; kein Fall, in dem nur der neue Stand die Grenze erreicht. Keine Rundungsunterschiede.

Nicht verglichen (2 Fälle): `pruef21-T-0747-leistung/c21e/k5_dicht_500_60000` – beide Stände hingen über eine Stunde **beim Lesen/Verketten** (Abbruch dort greift nicht; außerhalb von `Generate`, siehe Bericht „Offene Punkte“); `pruef19-T-0747-hatchgram/pf1/p_diag_500_l_2000_h_t6` – Lauf abgebrochen, dieselbe Datei liegt in `perf19` und ist dort verglichen (alt: Grenze, neu: fertig).

Zusätzlich: `r1_kreise_fastgleich_50000` erreichte in einem früheren Vergleichslauf unter Last neu die Grenze (alt 127 s fertig); einzeln gerechnet liefert der neue Stand in 71–78 s denselben G-Code (SHA-256 4b6d4a2af9d885d9…) – im Endlauf gleich.

## 60 Fälle: alt ohne Ergebnis (Grenze), neu mit G-Code

Zeiten: Gesamtweg (Lesen + Konturen + Fräsbahnen) im Vergleichslauf, 4 Läufe parallel; alt-Zeiten über 120 s = Nachlauf bis der Abbruch griff.

| Fall | alt | neu |
|---|---:|---:|
| q5_riesig_neben_30000_winzig | 121 s | 16.5 s |
| q5_riesig_neben_5000_winzig | 164 s | 1.8 s |
| q6_streifen_400_winzig_20000 | 235 s | 6.8 s |
| q6_streifen_78_winzig_20000 | 168 s | 4.6 s |
| q5b_riesig_neben_5000_winzig_ohne_hatch | 188 s | 1.8 s |
| q6b_streifen_78_ohne_hatch_ohne_winzig | 286 s | 0.4 s |
| n2_kamm_50000_zaehne_20_versteckt | 177 s | 55.5 s |
| n3_kreise_zelle_60000x60000 | 131 s | 31.7 s |
| n2_kamm_50000_zaehne_20_versteckt | 120 s | 31.4 s |
| n2_kamm_100000_zaehne_50_versteckt | 361 s | 117.0 s |
| n2_kamm_50000_zaehne_20_versteckt | 120 s | 28.7 s |
| k2_kamm_199999_x4_versteckt | 194 s | 64.7 s |
| k2_kamm_199999_x4_versteckt | 149 s | 119.8 s |
| j2_stoss_50000_kreise_50000 | 141 s | 32.4 s |
| j4_stoss_10000_kreise_150000 | 124 s | 116.9 s |
| pruef10-leistung_cases10_q5_riesig_neben_5000_winzig | 236 s | 2.3 s |
| pruef10-leistung_cases10b_q5b_riesig_neben_5000_winzig_ohne_hatch | 215 s | 2.3 s |
| pruef10-leistung_cases10b_q6b_streifen_78_ohne_hatch_ohne_winzig | 193 s | 0.3 s |
| p_diag_1000_l_2000_h_t1 | 12185 s | 3.3 s |
| p_diag_2000_l_2000_h_t6 | 555 s | 1.8 s |
| p_diag_2000_l_5000_h_t6 | 1265 s | 4.1 s |
| p_diag_500_l_2000_h_t6 | 290 s | 1.2 s |
| p_diag_1000_l_0_h_t1 | 8608 s | 0.7 s |
| p_diag_1000_l_0_h_t6 | 340 s | 0.3 s |
| p_diag_2000_l_0_h_t6 | 554 s | 0.3 s |
| p1_diag_gravur_1000_10000 | 139 s | 4.8 s |
| p1_diag_gravur_2000_20000 | 121 s | 6.0 s |
| p1_diag_gravur_200_10000 | 157 s | 0.9 s |
| p1_diag_gravur_5000_10000 | 186 s | 4.4 s |
| p1b_diag_lw50_2000_10000 | 123 s | 4.4 s |
| p1b_diag_lw50_500_10000 | 123 s | 1.4 s |
| q_diag_1000_20000_seg1 | 168 s | 4.1 s |
| q_diag_1000_5000_seg1 | 121 s | 0.5 s |
| q_diag_3000_5000_seg1 | 243 s | 0.8 s |
| r2_diag_1000_loch25x4000 | 129 s | 2.1 s |
| r2_diag_3000_loch25x4000 | 138 s | 3.4 s |
| p1_diag_gravur_1000_10000 | 137 s | 2.9 s |
| p1_diag_gravur_2000_20000 | 164 s | 4.5 s |
| p1_diag_gravur_200_10000 | 121 s | 0.9 s |
| p1_diag_gravur_5000_10000 | 125 s | 4.1 s |
| p1b_diag_lw50_2000_10000 | 126 s | 3.4 s |
| p1b_diag_lw50_500_10000 | 122 s | 1.5 s |
| k5_dicht_200000_100 | 157 s | 63.2 s |
| k5_dicht_200000_30 | 163 s | 43.1 s |
| k5_dicht_200000_300 | 155 s | 50.3 s |
| k5_dicht_20000_0 | 4491 s | 1.9 s |
| k5_dicht_20000_300 | 121 s | 1.6 s |
| k5_dicht_20000_3000 | 120 s | 7.2 s |
| p1b_diag_2000_2500 | 247 s | 11.3 s |
| p1b_diag_5000_2500 | 408 s | 29.8 s |
| k2_lochblech_100_100 | 129 s | 2.1 s |
| k2_lochblech_200_200 | 160 s | 4.9 s |
| k1_kamm_innen_100000_5 | 222 s | 86.2 s |
| k1_kamm_innen_50000_1 | 235 s | 31.8 s |
| k1_kamm_innen_50000_20 | 120 s | 39.2 s |
| k2_lochblech_300_300 | 157 s | 10.3 s |
| k1_kamm_innen_100000_5 | 183 s | 76.2 s |
| k2_lochblech_100_100 | 125 s | 1.4 s |
| k2_lochblech_200_200 | 122 s | 4.2 s |
| k2_lochblech_300_300 | 122 s | 8.7 s |

## 12 Fälle: alt ohne Ergebnis (Grenze), neu mit der Meldung „Es blieb keine fräsbare Kontur …“

| Fall | alt | neu |
|---|---:|---:|
| s1_formvergleich_kamm_2000_C64_F117 | 121 s | 55.7 s |
| y_2000_5000 | 121 s | 1.4 s |
| y_2000_2000 | 121 s | 2.0 s |
| p1_gassen_100 | 209 s | 5.1 s |
| p3_streifen_1500 | 558 s | 25.9 s |
| x3a_stoss2000_q20000 | 172 s | 0.8 s |
| x3b_stoss10000_q20000 | 155 s | 1.1 s |
| x3c_stoss20000_q50000 | 190 s | 4.0 s |
| x6c_g10000_q50000 | 121 s | 10.1 s |
| d_diag_2000 | 636 s | 3.9 s |
| d_diag_500 | 535 s | 2.4 s |
| k5_dicht_400000_300 | 291 s | 42.9 s |
