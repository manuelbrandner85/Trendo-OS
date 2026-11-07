# Beleg – Echte Kollision, Überfahren mit Schaden, Menschen auf dem Boden, echter Lauf mit UE 5.8 (07.11.2026)

Anlass (Nutzer): Wagen und Menschen liefen durch Objekte; Kollision soll echt sein mit entsprechendem Schaden; Menschen sollen auf dem Boden stehen und nicht im Boden versinken.

## Ursachen (gemessen, vorher)
- Baum: die Kollisionshülle umschloss die ganze Krone – die Figur prallte 6,6 m vor dem Stamm ab (unsichtbare Wand).
- Kleine Bäume, Sträucher bis 2,4 m, einige Felsen und der liegende Stamm: Figur und Wagen gingen hindurch (eine grobe Hülle je Modell).
- Figuren (Menschen) und Tiere hatten keinen festen Körper: Figur und Wagen gingen hindurch.
- Versinken: Die übertragenen Geh- und Laufbewegungen hielten die Wurzel auf der Grundhaltung fest (force_root_lock + REF_POSE). Nach dem Übertragen trägt die Wurzel aber die Beckenhöhe (94 cm) – gehende Menschen sackten bis 112 cm in den Boden; stehende nicht (Ruhe-Bewegung ohne Sperre).

## Umbau
- Welt-Bau: Baum = Zylinder um den aus dem Modell gemessenen Stamm (13–19 cm, im Spiel je nach Größe bis gut 40 cm); Felsen/Holz/hohe Sträucher = Zerlegung in 2–8 konvexe Teile; Gras, Farn, kleine Sträucher durchlaufbar.
- Menschen-Bau: Wurzel auf den ersten Bildstand festhalten (ANIM_FIRST_FRAME) – Beckenhöhe bleibt, kein Vorwärtsrutschen.
- Figuren bekommen einen festen Körper (Kapsel, Kanal Pawn).
- Überfahren ab 6 km/h: Körper bekommt die Wagengeschwindigkeit, fällt mit der MetaHuman-Sturzphysik (gemeinsame Funktion TrendoRagdollBody), Blut und Lache; Tiere sterben. Der Wagen verliert etwas Tempo und nimmt Schaden.
- Aufprall an Hindernissen (nicht Gelände): Tempoverlust > 10 km/h in einem Bild; Schaden aus der Wucht, ab 60 % schwächerer Motor, 100 % Motor aus; Hinweis im Spiel.
- Abgetrenntes Glied: entsteht über dem Gelände und wird aufgefangen, falls es durchrutscht. Tiere: Pose immer gerechnet.

## Messung (Kollision je Art, Figur-Kapsel und Wagen-Box gegen die Form des Hindernisses)
- grass_medium_01_tall_b_LOD0 Figur=geht durch Wagen=fährt durch, 54 cm hoch
- grass_medium_01_small_b_LOD0 Figur=geht durch Wagen=fährt durch, 18 cm hoch
- grass_medium_01_large_b_LOD0 Figur=geht durch Wagen=fährt durch, 31 cm hoch
- grass_medium_01_mid_b_LOD0 Figur=geht durch Wagen=fährt durch, 31 cm hoch
- grass_medium_01_mid_c_LOD0 Figur=geht durch Wagen=fährt durch, 17 cm hoch
- grass_medium_01_large_c_LOD0 Figur=geht durch Wagen=fährt durch, 21 cm hoch
- grass_medium_01_tiny_f_LOD0 Figur=geht durch Wagen=fährt durch, 9 cm hoch
- grass_medium_01_tiny_c_LOD0 Figur=geht durch Wagen=fährt durch, 11 cm hoch
- grass_medium_01_tiny_a_LOD0 Figur=geht durch Wagen=fährt durch, 19 cm hoch
- grass_medium_01_tiny_b_LOD0 Figur=geht durch Wagen=fährt durch, 12 cm hoch
- grass_medium_01_tall_a_LOD0 Figur=geht durch Wagen=fährt durch, 43 cm hoch
- grass_medium_01_tiny_d_LOD0 Figur=geht durch Wagen=fährt durch, 5 cm hoch
- grass_medium_01_large_a_LOD0 Figur=geht durch Wagen=fährt durch, 28 cm hoch
- grass_medium_01_small_a_LOD0 Figur=geht durch Wagen=fährt durch, 27 cm hoch
- grass_medium_01_mid_a_LOD0 Figur=geht durch Wagen=fährt durch, 36 cm hoch
- grass_medium_01_tall_c_LOD0 Figur=geht durch Wagen=fährt durch, 34 cm hoch
- grass_medium_01_tiny_e_LOD0 Figur=geht durch Wagen=fährt durch, 9 cm hoch
- fern_02_b Figur=geht durch Wagen=fährt durch, 36 cm hoch
- fern_02_c Figur=geht durch Wagen=fährt durch, 39 cm hoch
- fern_02_a Figur=geht durch Wagen=fährt durch, 43 cm hoch
- fern_02_d Figur=geht durch Wagen=fährt durch, 28 cm hoch
- shrub_02_d Figur=prallt ab 270 cm vor der Mitte Wagen=prallt ab 325 cm vor der Mitte, 132 cm hoch
- shrub_04_2k Figur=geht durch Wagen=fährt durch, 39 cm hoch
- shrub_02_c Figur=prallt ab 104 cm vor der Mitte Wagen=prallt ab 203 cm vor der Mitte, 206 cm hoch
- shrub_02_a Figur=prallt ab 113 cm vor der Mitte Wagen=prallt ab 175 cm vor der Mitte, 173 cm hoch
- shrub_02_b Figur=prallt ab 130 cm vor der Mitte Wagen=prallt ab 281 cm vor der Mitte, 241 cm hoch
- pine_sapling_small_c Figur=prallt ab 45 cm vor der Mitte Wagen=prallt ab 95 cm vor der Mitte, 118 cm hoch
- pine_sapling_small_b Figur=prallt ab 46 cm vor der Mitte Wagen=prallt ab 97 cm vor der Mitte, 135 cm hoch
- pine_sapling_small_a Figur=prallt ab 60 cm vor der Mitte Wagen=prallt ab 110 cm vor der Mitte, 232 cm hoch
- tree_small_02_2k Figur=prallt ab 155 cm vor der Mitte Wagen=prallt ab 195 cm vor der Mitte, 739 cm hoch
- rock_moss_set_01_rock03 Figur=prallt ab 195 cm vor der Mitte Wagen=prallt ab 254 cm vor der Mitte, 143 cm hoch
- boulder_01_2k Figur=prallt ab 151 cm vor der Mitte Wagen=prallt ab 201 cm vor der Mitte, 114 cm hoch
- rock_moss_set_01_rock06 Figur=prallt ab 281 cm vor der Mitte Wagen=prallt ab 338 cm vor der Mitte, 223 cm hoch
- rock_moss_set_01_rock01 Figur=prallt ab 102 cm vor der Mitte Wagen=prallt ab 188 cm vor der Mitte, 96 cm hoch
- rock_moss_set_01_rock04 Figur=prallt ab 120 cm vor der Mitte Wagen=prallt ab 173 cm vor der Mitte, 141 cm hoch
- rock_moss_set_01_rock05 Figur=prallt ab 165 cm vor der Mitte Wagen=prallt ab 227 cm vor der Mitte, 191 cm hoch
- rock_moss_set_01_rock02 Figur=prallt ab 153 cm vor der Mitte Wagen=prallt ab 225 cm vor der Mitte, 116 cm hoch
- tree_stump_01_2k Figur=prallt ab 69 cm vor der Mitte Wagen=prallt ab 136 cm vor der Mitte, 59 cm hoch
- dead_tree_trunk_2k Figur=prallt ab 80 cm vor der Mitte Wagen=prallt ab 246 cm vor der Mitte, 37 cm hoch
- Objekt o Figur=geht durch Wagen=fährt durch
- Objekt G Figur=geht durch Wagen=fährt durch
- Mensch (Figur) Figur=prallt ab Wagen=prallt ab
- Objekt Z Figur=geht durch Wagen=fährt durch
- Gegner Figur=prallt ab Wagen=prallt ab
- Tier DeerDoe Figur=prallt ab Wagen=prallt ab

## Messung (Füße auf dem Boden, vier tiefste sichtbare Knochen gegen Gelände/Felsen)
- Spielfigur tiefster Punkt 4.7 cm über dem Boden (auf dem Boden): ball_r 32.3 littletoe_02_l 8.6 ball_l 4.7 bigtoe_02_r 32.7, Kapsel-Unterkante 32.5 cm, Körper 0.0 cm unter der Kapsel, Bewegung MOVE_Walking
- Mensch (Figur) tiefster Punkt 0.1 cm über dem Boden (auf dem Boden): ball_r 0.1 ball_l 0.3 littletoe_02_l 1.9 littletoe_02_r 1.9
- Gegner tiefster Punkt 8.6 cm über dem Boden (auf dem Boden): littletoe_02_l 8.6 littletoe_01_l 8.7 ringtoe_02_l 9.2 ball_l 9.2, Kapsel-Unterkante 2.1 cm, Körper -0.0 cm unter der Kapsel, Bewegung MOVE_Walking
- Tier DeerDoe tiefster Punkt 2.0 cm über dem Boden (auf dem Boden): root 2.0 DeerDoe_-R-Finger0 9.6 DeerDoe_-L-Foot 11.0 DeerDoe_-L-Finger0 13.4, Kapsel-Unterkante 2.0 cm, Körper -0.0 cm unter der Kapsel, Bewegung MOVE_Walking
- Tier DeerDoe tiefster Punkt -200.0 cm über dem Boden (steckt im Boden): root -117.8 DeerDoe_-R-Finger0 -200.0 DeerDoe_-L-Finger0 -122.9 DeerDoe_-R-Foot -60.7, Kapsel-Unterkante 2.1 cm, Körper -0.0 cm unter der Kapsel, Bewegung MOVE_Walking
- Tier DeerStag tiefster Punkt 1.9 cm über dem Boden (auf dem Boden): root 1.9 STAG_-R-Finger0 10.1 STAG_-L-Finger0 10.4 STAG_-L-Foot 12.1, Kapsel-Unterkante 1.9 cm, Körper -0.0 cm unter der Kapsel, Bewegung MOVE_Walking
- Tier DeerStag tiefster Punkt 2.1 cm über dem Boden (auf dem Boden): root 2.1 STAG_-R-Finger0 10.2 STAG_-L-Finger0 10.6 STAG_-L-Foot 12.3, Kapsel-Unterkante 2.1 cm, Körper -0.0 cm unter der Kapsel, Bewegung MOVE_Walking
- Toter (liegt) tiefster Punkt -22.5 cm über dem Boden (steckt im Boden): lowerarm_out_r -22.5 lowerarm_bck_r -21.2 pinky_01_half_r -19.7 pinky_02_half_r -19.5, Kapsel-Unterkante 2.1 cm, Körper -28.8 cm unter der Kapsel, Bewegung MOVE_None
- Toter liegt auf LandscapeStreamingProxy

## Messung (Aufprall)
- Probe: Mensch 6 m und Baum 12 m vor dem fahrenden Wagen (20 km/h)
- Mensch überfahren mit 25 km/h, Körper fällt=ja (Physik), Wagen-Schaden 2 %
- Bilanz: 0 Aufprall(e), stärkster 0 km/h, Wagen-Schaden 2 %, überfahren 1

Bilder: unreal-kollision-fels.png (Spielfigur steht auf einem Felsen, Rehe dahinter), unreal-kollision-gegner.png (Figur im Wald)

## Ehrlich offen
- Baum-Aufprall des Wagens nicht gemessen: der Prüf-Wagen bleibt im Testgelände bei etwa 11 m an einer Geländestelle stehen und erreicht den gesetzten Baum nicht (Schaden-Logik ist eingebaut, aber ungeprüft).
- Zwei Reh-Messungen zeigen Werte bis −200 cm an Hilfsknochen; im Bild stehen die Rehe auf dem Boden – Ursache nicht geklärt.
- Kopf des Toten: 0,0–0,8 cm in drei Läufen, im letzten 2,3 cm (knapp über der Ruhe-Grenze 2 cm). Hand des Toten bis 28 cm unter der Geländeoberfläche in einem Lauf.
- Keine sichtbare Verformung der Karosserie (Schaden wirkt auf Motor und Hinweis).

## SHA-256
- unreal-kollision-fels.png: 8a7377b5c107cea3c4df2dd025d31d4fcb2f3e6cbc4efa191829eba26f77fe01
- unreal-kollision-gegner.png: dfa4201afb9cb600fb651daf70f524085ea7d5b3d57a0e01c0d43d5fb3c63c77

## Rohlog-Auszüge
```
Result: Succeeded
LogTrendo: Display: TRENDO-NATUR 164059 Pflanzen/Felsen in 39 Arten, Landschaft wald
LogTrendo: Display: TRENDO-TIER 8 Tiere gesetzt (Landschaft wald: DeerDoe, DeerStag, Fox, Wolf), 1 verschiedene Felle
LogTemp: Display: TRENDO-FIGUR MetaHuman=ja (BP_Waldlaeuferin_C) Mensch=ja
LogTrendo: Display: TRENDO-KOLL grass_medium_01_tall_b_LOD0 Figur=geht durch Wagen=fährt durch, 54 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_small_b_LOD0 Figur=geht durch Wagen=fährt durch, 18 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_large_b_LOD0 Figur=geht durch Wagen=fährt durch, 31 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_mid_b_LOD0 Figur=geht durch Wagen=fährt durch, 31 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_mid_c_LOD0 Figur=geht durch Wagen=fährt durch, 17 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_large_c_LOD0 Figur=geht durch Wagen=fährt durch, 21 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_tiny_f_LOD0 Figur=geht durch Wagen=fährt durch, 9 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_tiny_c_LOD0 Figur=geht durch Wagen=fährt durch, 11 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_tiny_a_LOD0 Figur=geht durch Wagen=fährt durch, 19 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_tiny_b_LOD0 Figur=geht durch Wagen=fährt durch, 12 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_tall_a_LOD0 Figur=geht durch Wagen=fährt durch, 43 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_tiny_d_LOD0 Figur=geht durch Wagen=fährt durch, 5 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_large_a_LOD0 Figur=geht durch Wagen=fährt durch, 28 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_small_a_LOD0 Figur=geht durch Wagen=fährt durch, 27 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_mid_a_LOD0 Figur=geht durch Wagen=fährt durch, 36 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_tall_c_LOD0 Figur=geht durch Wagen=fährt durch, 34 cm hoch
LogTrendo: Display: TRENDO-KOLL grass_medium_01_tiny_e_LOD0 Figur=geht durch Wagen=fährt durch, 9 cm hoch
LogTrendo: Display: TRENDO-KOLL fern_02_b Figur=geht durch Wagen=fährt durch, 36 cm hoch
LogTrendo: Display: TRENDO-KOLL fern_02_c Figur=geht durch Wagen=fährt durch, 39 cm hoch
LogTrendo: Display: TRENDO-KOLL fern_02_a Figur=geht durch Wagen=fährt durch, 43 cm hoch
LogTrendo: Display: TRENDO-KOLL fern_02_d Figur=geht durch Wagen=fährt durch, 28 cm hoch
LogTrendo: Display: TRENDO-KOLL shrub_02_d Figur=prallt ab 270 cm vor der Mitte Wagen=prallt ab 325 cm vor der Mitte, 132 cm hoch
LogTrendo: Display: TRENDO-KOLL shrub_04_2k Figur=geht durch Wagen=fährt durch, 39 cm hoch
LogTrendo: Display: TRENDO-KOLL shrub_02_c Figur=prallt ab 104 cm vor der Mitte Wagen=prallt ab 203 cm vor der Mitte, 206 cm hoch
LogTrendo: Display: TRENDO-KOLL shrub_02_a Figur=prallt ab 113 cm vor der Mitte Wagen=prallt ab 175 cm vor der Mitte, 173 cm hoch
LogTrendo: Display: TRENDO-KOLL shrub_02_b Figur=prallt ab 130 cm vor der Mitte Wagen=prallt ab 281 cm vor der Mitte, 241 cm hoch
LogTrendo: Display: TRENDO-KOLL pine_sapling_small_c Figur=prallt ab 45 cm vor der Mitte Wagen=prallt ab 95 cm vor der Mitte, 118 cm hoch
LogTrendo: Display: TRENDO-KOLL pine_sapling_small_b Figur=prallt ab 46 cm vor der Mitte Wagen=prallt ab 97 cm vor der Mitte, 135 cm hoch
LogTrendo: Display: TRENDO-KOLL pine_sapling_small_a Figur=prallt ab 60 cm vor der Mitte Wagen=prallt ab 110 cm vor der Mitte, 232 cm hoch
LogTrendo: Display: TRENDO-KOLL tree_small_02_2k Figur=prallt ab 155 cm vor der Mitte Wagen=prallt ab 195 cm vor der Mitte, 739 cm hoch
LogTrendo: Display: TRENDO-KOLL rock_moss_set_01_rock03 Figur=prallt ab 195 cm vor der Mitte Wagen=prallt ab 254 cm vor der Mitte, 143 cm hoch
LogTrendo: Display: TRENDO-KOLL boulder_01_2k Figur=prallt ab 151 cm vor der Mitte Wagen=prallt ab 201 cm vor der Mitte, 114 cm hoch
LogTrendo: Display: TRENDO-KOLL rock_moss_set_01_rock06 Figur=prallt ab 281 cm vor der Mitte Wagen=prallt ab 338 cm vor der Mitte, 223 cm hoch
LogTrendo: Display: TRENDO-KOLL rock_moss_set_01_rock01 Figur=prallt ab 102 cm vor der Mitte Wagen=prallt ab 188 cm vor der Mitte, 96 cm hoch
LogTrendo: Display: TRENDO-KOLL rock_moss_set_01_rock04 Figur=prallt ab 120 cm vor der Mitte Wagen=prallt ab 173 cm vor der Mitte, 141 cm hoch
LogTrendo: Display: TRENDO-KOLL rock_moss_set_01_rock05 Figur=prallt ab 165 cm vor der Mitte Wagen=prallt ab 227 cm vor der Mitte, 191 cm hoch
LogTrendo: Display: TRENDO-KOLL rock_moss_set_01_rock02 Figur=prallt ab 153 cm vor der Mitte Wagen=prallt ab 225 cm vor der Mitte, 116 cm hoch
LogTrendo: Display: TRENDO-KOLL tree_stump_01_2k Figur=prallt ab 69 cm vor der Mitte Wagen=prallt ab 136 cm vor der Mitte, 59 cm hoch
LogTrendo: Display: TRENDO-KOLL dead_tree_trunk_2k Figur=prallt ab 80 cm vor der Mitte Wagen=prallt ab 246 cm vor der Mitte, 37 cm hoch
LogTrendo: Display: TRENDO-KOLL Objekt o Figur=geht durch Wagen=fährt durch
LogTrendo: Display: TRENDO-KOLL Objekt G Figur=geht durch Wagen=fährt durch
LogTrendo: Display: TRENDO-KOLL Mensch (Figur) Figur=prallt ab Wagen=prallt ab
LogTrendo: Display: TRENDO-KOLL Objekt Z Figur=geht durch Wagen=fährt durch
LogTrendo: Display: TRENDO-KOLL Gegner Figur=prallt ab Wagen=prallt ab
LogTrendo: Display: TRENDO-KOLL Tier DeerDoe Figur=prallt ab Wagen=prallt ab
LogTrendo: Display: TRENDO-FUSS Spielfigur tiefster Punkt 4.7 cm über dem Boden (auf dem Boden): ball_r 32.3 littletoe_02_l 8.6 ball_l 4.7 bigtoe_02_r 32.7, Kapsel-Unterkante 32.5 cm, Körper 0.0 cm unter der Kapsel, Bewegung MOVE_Walking
LogTrendo: Display: TRENDO-FUSS Mensch (Figur) tiefster Punkt 0.1 cm über dem Boden (auf dem Boden): ball_r 0.1 ball_l 0.3 littletoe_02_l 1.9 littletoe_02_r 1.9
LogTrendo: Display: TRENDO-FUSS Gegner tiefster Punkt 8.6 cm über dem Boden (auf dem Boden): littletoe_02_l 8.6 littletoe_01_l 8.7 ringtoe_02_l 9.2 ball_l 9.2, Kapsel-Unterkante 2.1 cm, Körper -0.0 cm unter der Kapsel, Bewegung MOVE_Walking
LogTrendo: Display: TRENDO-FUSS Tier DeerDoe tiefster Punkt 2.0 cm über dem Boden (auf dem Boden): root 2.0 DeerDoe_-R-Finger0 9.6 DeerDoe_-L-Foot 11.0 DeerDoe_-L-Finger0 13.4, Kapsel-Unterkante 2.0 cm, Körper -0.0 cm unter der Kapsel, Bewegung MOVE_Walking
LogTrendo: Display: TRENDO-FUSS Tier DeerDoe tiefster Punkt -200.0 cm über dem Boden (steckt im Boden): root -117.8 DeerDoe_-R-Finger0 -200.0 DeerDoe_-L-Finger0 -122.9 DeerDoe_-R-Foot -60.7, Kapsel-Unterkante 2.1 cm, Körper -0.0 cm unter der Kapsel, Bewegung MOVE_Walking
LogTrendo: Display: TRENDO-FUSS Tier DeerStag tiefster Punkt 1.9 cm über dem Boden (auf dem Boden): root 1.9 STAG_-R-Finger0 10.1 STAG_-L-Finger0 10.4 STAG_-L-Foot 12.1, Kapsel-Unterkante 1.9 cm, Körper -0.0 cm unter der Kapsel, Bewegung MOVE_Walking
LogTrendo: Display: TRENDO-FUSS Tier DeerStag tiefster Punkt 2.1 cm über dem Boden (auf dem Boden): root 2.1 STAG_-R-Finger0 10.2 STAG_-L-Finger0 10.6 STAG_-L-Foot 12.3, Kapsel-Unterkante 2.1 cm, Körper -0.0 cm unter der Kapsel, Bewegung MOVE_Walking
LogTrendo: Display: TRENDO-WEG Wegnetz=ja Pfade=33 davon mit Umweg=7
LogTrendo: Display: TRENDO-WEG Hindernis shrub_02_a: gerade versperrt, Pfad 26.3 m statt 23.1 m mit 9 Punkten, führt drumherum
LogTrendo: Display: TRENDO-BODEN Gegner geht=ja Abstand Füße–Boden=2.2 cm
LogTrendo: Display: TRENDO-GEGNER alarmiert=ja Abstand vorher=1100 nachher=323 Leben 3→3 verfolgt
LogTrendo: Display: TRENDO-FLACKERN Gegner in der Ferne bis 53 m: 509 Bilder, ohne Posen-Update 0 (0%), Sprünge über 12 cm 0 (dazu 0 beim Erscheinen), größter Sprung je Bild 5.8 cm (ruhig)
LogTrendo: Display: TRENDO-TIER vorher: 8 Tiere, DeerDoe geht, walk, gelaufen 22 m
LogTrendo: Display: TRENDO-TIER DeerDoe flieht=ja Abstand 5.0 → 5.4 m Bewegung=idle2 Tempo 0 km/h Fluchten=1
LogTemp: Display: TRENDO-GLIED abgetrennt=calf_l Kleidung=ja
LogTrendo: Display: TRENDO-KAMPF Körper: Becken Z=40, Gegner Z=115, Simulation=ja, Physik-Körper=21
LogTrendo: Display: TRENDO-KAMPF Gegner besiegt=ja Ragdoll=ja Becken über Boden=40 cm (liegt) Blut=48 abgetrennt=1 Gewalt=brutal
LogTrendo: Display: TRENDO-KAMPF Kopf nach dem Liegen: 2.3 cm Bewegung in 0,6 s (zappelt)
LogTrendo: Display: TRENDO-FUSS Toter (liegt) tiefster Punkt -22.5 cm über dem Boden (steckt im Boden): lowerarm_out_r -22.5 lowerarm_bck_r -21.2 pinky_01_half_r -19.7 pinky_02_half_r -19.5, Kapsel-Unterkante 2.1 cm, Körper -28.8 cm unter der Kapsel, Bewegung MOVE_None
LogTrendo: Display: TRENDO-FUSS Toter liegt auf LandscapeStreamingProxy
LogTrendo: Display: TRENDO-GLIED liegt -9 cm über dem Boden, 1.9 m vom Körper
LogTrendo: Display: TRENDO-BLUT Zeitraffer +5 min: 54 Flecken gealtert
LogTrendo: Display: TRENDO-WAGEN freie Bahn 23 m, Lenkziel -45° zur Fahrtrichtung
LogTrendo: Display: TRENDO-WAGEN eingestiegen
LogTrendo: Display: TRENDO-AUFPRALL Probe: Mensch 6 m und Baum 12 m vor dem fahrenden Wagen (20 km/h)
LogTrendo: Display: TRENDO-AUFPRALL Mensch überfahren mit 25 km/h, Körper fällt=ja (Physik), Wagen-Schaden 2 %
LogTrendo: Display: TRENDO-WAGEN gesteuert=ja Klasse=TrendoCar Tempo=0 km/h Strecke=10.7 m Raddrehung=271° (Reifen drehen sich) Gang=1 Neigung max=2°
LogTrendo: Display: TRENDO-AUFPRALL Bilanz: 0 Aufprall(e), stärkster 0 km/h, Wagen-Schaden 2 %, überfahren 1
LogTrendo: Display: TRENDO-WAGEN ausgestiegen, Figur=sichtbar
LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=9 Gegner=0 Natur=164059 Figur=ja Boden=ja Z=92
LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```
