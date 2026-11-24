# T-0758 – Messtabelle Fräsbahn-Erzeugung alt/neu

Gemessen am 23.11.2026 mit `harness --gen` (nur `CncCam.Generate`, Konturen vorher gelesen; Einstellungen aus `.set.json` der Prüferfälle, sonst Fräser 6 mm), Grenze 150 s, Fälle nacheinander. **Alt** = main 0.9.108 (24bb4f70e), **neu** = Zweig claude/T-0758-fraesen-tempo (32ecd7545). Während der Messung liefen zeitweise Vergleichsläufe im Hintergrund – Einzelwerte schwanken bis etwa ±40 % (b1 lag in Wiederholungen alt wie neu zwischen 20 s und über 150 s, f_viele alt 8,8–52 s, neu 11–30 s). Prüfwert G-Code/Meldungen: erste 16 Hex-Zeichen von SHA-256; „alt ohne Ergebnis“: alt erreichte die Zeitgrenze, neu liefert ein Ergebnis (Gleichheit dann über den Vergleichslauf mit 120-s-Grenze bzw. Einzelvergleich, siehe vergleich.md). „InvalidOperationException“: es blieb keine fräsbare Kontur (gleiche Meldung).

| Fall | alt (0.9.108) | neu | Faktor | Speicher alt / neu (MB) | G-Code + Meldungen |
|---|---:|---:|---:|---:|---|
| p_diag_20_l_0_h_t6 | 6.97 s | 0.17 s | 42× | 130 / 54 | gleich |
| p_diag_50_l_0_h_t6 | 20.68 s | 0.17 s | 121× | 176 / 54 | gleich |
| p_diag_100_l_0_h_t6 | 40.06 s | 0.19 s | 208× | 241 / 54 | gleich |
| p_diag_200_l_0_h_t6 | 105.26 s | 0.23 s | 454× | 311 / 55 | gleich |
| p_diag_1000_l_0_h_t1 | > 150 s (Zeitgrenze) | 0.41 s | > 362× | – / 66 | alt ohne Ergebnis |
| p_diag_500_l_2000_h_t6 | > 150 s (Zeitgrenze) | 1.70 s | > 88× | – / 100 | alt ohne Ergebnis |
| p_diag_2000_l_2000_h_t6 | > 150 s (Zeitgrenze) | 2.19 s | > 68× | – / 108 | alt ohne Ergebnis |
| p1_diag_gravur_200_10000 | 147.74 s | 1.69 s | 87× | 8293 / 246 | gleich |
| p1_diag_gravur_1000_10000 | > 150 s (Zeitgrenze) | 2.79 s | > 54× | – / 285 | alt ohne Ergebnis |
| p1b_diag_lw50_500_10000 | 129.99 s | 2.68 s | 49× | 10640 / 255 | gleich |
| q_diag_500_5000_seg50 | 33.25 s | 1.20 s | 28× | 3063 / 128 | gleich |
| r2_diag_1000_loch25x4000 | 44.58 s | 1.95 s | 23× | 535 / 117 | gleich |
| s_diagonal_kollinear_3000 | 23.86 s | 0.19 s | 126× | 281 / 62 | gleich |
| s_diagonal_kollinear_10000 | 24.04 s | 0.28 s | 86× | 221 / 73 | gleich |
| f_viele_o_3000 | 8.82 s | 11.12 s | 1× | 324 / 340 | gleich |
| k2s_neben_60_60_4.0 | 6.38 s | 6.64 s | 1× | 351 / 371 | gleich |
| k2_lochblech_200_200 | > 150 s (Zeitgrenze) | 5.35 s | > 28× | 11637 / 737 | alt ohne Ergebnis |
| n2_kamm_50000_zaehne_20_versteckt | 115.98 s | 26.51 s | 4× | 524 / 617 | gleich |
| n8_zickzack_formgleich_100000 | 3.98 s | 1.03 s | 4× | 178 / 182 | gleich |
| k1_kamm_innen_50000_1 | 107.23 s | 28.90 s | 4× | 1234 / 1332 | gleich |
| q4_kreise_2000_r1000_gleich | 22.96 s | 28.93 s | 1× | 2552 / 3779 | gleich |
| q5_riesig_neben_30000_winzig | > 150 s (Zeitgrenze) | 12.44 s | > 12× | 803 / 1471 | alt ohne Ergebnis |
| q6_streifen_400_winzig_20000 | > 150 s (Zeitgrenze) | 4.70 s | > 32× | – / 252 | alt ohne Ergebnis |
| b1_gleicher_rahmen_5000_konturen_5000_schraffuren | 20.94 s (InvalidOperationException) | 34.27 s (InvalidOperationException) |  | 359 / 387 | – |
| b2_gleicher_rahmen_versteckt_20000x20000 | > 150 s (Zeitgrenze) | > 150 s (Zeitgrenze) |  | – / 234 | – |
| e1_eimer_kreise_20000x20000 | 6.17 s | 6.54 s | 1× | 292 / 292 | gleich |
| e0_kontrolle_verteilt_100000x100000 | > 150 s (Zeitgrenze) | > 150 s (Zeitgrenze) |  | – / – | – |
| z1_zickzack_senkrecht_100000_versteckt_2000 | > 150 s (Zeitgrenze) | > 150 s (Zeitgrenze) |  | – / 302 | – |
| g2_50000_kreise_d1 | 25.59 s | 19.06 s | 1× | 1299 / 1305 | gleich |
| x3a_stoss2000_q20000 | > 150 s (Zeitgrenze) | 1.46 s (InvalidOperationException) |  | 1455 / 204 | – |
| x6c_g10000_q50000 | > 150 s (Zeitgrenze) | 10.40 s (InvalidOperationException) |  | 1024 / 386 | – |
| k4_viele_c_500 | 2.95 s | 3.17 s | 1× | 610 / 608 | gleich |

Offen (alt wie neu über der Grenze): b2_gleicher_rahmen_versteckt (Doppel-Prüfung: 20 000 fast gleiche Umrisse, jedes Paar besteht die Vorprüfung), e0_kontrolle_verteilt_100000 und z1_zickzack_senkrecht_100000 – siehe Bericht.

## Abbruch der Fräsbahn-Erzeugung (Nachlauf nach der Abbruch-Anforderung)

`harness --cancel`: Abbruch nach 300, 1000 und 3000 ms, gemessen bis zur Ausnahme; angegeben der größte Nachlauf. Alt nur für fünf Fälle gemessen (je Fall bis zu mehreren Minuten).

| Fall | Nachlauf alt (ms) | Nachlauf neu (ms) |
|---|---:|---:|
| p_diag_20_l_0_h_t6 | – | 0 (fertig vor Abbruch: 216 ms) |
| p_diag_50_l_0_h_t6 | – | 0 (fertig vor Abbruch: 285 ms) |
| p_diag_100_l_0_h_t6 | – | 0 (fertig vor Abbruch: 265 ms) |
| p_diag_200_l_0_h_t6 | – | 31 |
| p_diag_1000_l_0_h_t1 | – | 15 |
| p_diag_500_l_2000_h_t6 | 197 423 (nach 1000 ms; dann Messgrenze) | 36 |
| p_diag_2000_l_2000_h_t6 | – | 9 |
| p1_diag_gravur_200_10000 | – | 13 |
| p1_diag_gravur_1000_10000 | – | 13 |
| p1b_diag_lw50_500_10000 | – | 18 |
| q_diag_500_5000_seg50 | – | 90 |
| r2_diag_1000_loch25x4000 | – | 8 |
| s_diagonal_kollinear_3000 | – | 11 |
| s_diagonal_kollinear_10000 | – | 63 |
| f_viele_o_3000 | – | 41 |
| k2s_neben_60_60_4.0 | – | 26 |
| k2_lochblech_200_200 | – | 100 |
| n2_kamm_50000_zaehne_20_versteckt | 11505 | 34 |
| n8_zickzack_formgleich_100000 | – | 37 |
| k1_kamm_innen_50000_1 | – | 65 |
| q4_kreise_2000_r1000_gleich | 2108 | 48 |
| q5_riesig_neben_30000_winzig | – | 10 |
| q6_streifen_400_winzig_20000 | 2582 | 7 |
| b1_gleicher_rahmen_5000_konturen_5000_schraffuren | – | 6 |
| b2_gleicher_rahmen_versteckt_20000x20000 | – | 7 |
| e1_eimer_kreise_20000x20000 | – | 5 |
| e0_kontrolle_verteilt_100000x100000 | – | 105 |
| z1_zickzack_senkrecht_100000_versteckt_2000 | – | 590 |
| g2_50000_kreise_d1 | 14457 | 31 |
| x3a_stoss2000_q20000 | – | Zeitgrenze exit 127 |
| x6c_g10000_q50000 | – | 5 |
| k4_viele_c_500 | – | 8 |

Alt zusätzlich: p_diag_500_l_2000_h_t6 – Abbruch nach 1000 ms griff erst nach 197 s (rohlogs/cancel_alt.log). x3a: neu fertig nach 1,5 s mit „keine fräsbare Kontur“ (das Messprogramm brach bei dieser Ausnahme ab, kein Abbruch nötig).
