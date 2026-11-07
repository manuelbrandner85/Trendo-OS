# Beleg – Anatomischer Sturz, ruhiger Kopf, realistisches Blut, dichter Wald, echter Lauf mit UE 5.8 (07.11.2026)

Anlass (Nutzer): Brutalität und Umgebung realistischer; der Kopf des getöteten Mannes zappelte merkwürdig, der Fall war nicht anatomisch korrekt.

## Ursachen und Umbau
- Fallen: Bisher fiel der MetaHuman mit der Physik-Hülle der Engine-Übungspuppe (PA_Mannequin) – Gelenkgrenzen passten nicht zum Körper. Jetzt die MetaHuman-eigene Hülle (PHYS_<Name>, 21 Körper), Mannequin nur als Rückfall.
- Kopf-Zappeln: Gesicht, Kleidung und Haare liefen mit eigener Animation gegen den fallenden Körper. Jetzt folgen sie ihm Knochen für Knochen (Leader-Pose); Kopf und Nacken stark gedämpft, alle Glieder bewusstlos-gedämpft, die Physik schläft ein, sobald er liegt.
- Blut: Spurenbild statt runder Kleckse – Spritzer in Schlagrichtung (je weiter, desto länglicher), auch bei Treffern ohne Tod; Lache unter dem Oberkörper wächst über 8 s; am Stumpf 6 Blutstöße, jeder schwächer.
- Umgebung: Pflanzendichte je Landschaft – Wald mit 2,6× Bäumen, 2,5× Farnen, 2× Unterholz und Totholz (164 059 Pflanzen und Felsen); Savanne offen, Hof viel Wiese.
- Wagen im Wald: Liegt die freie Bahn hinter ihm, setzt der Prüf-Fahrer zurück (höchstens etwa 18 km/h) statt zwischen Bäumen zu wenden.

## Messung
- Körper liegt (Becken 30 cm über dem Boden), Physik-Körper 21; Kopf nach dem Liegen 0,3 cm Bewegung in 0,6 s (ruhig) – vorher sichtbares Zappeln.
- 48 Blutflecken (vorher 9), nach Zeitraffer +5 min 54 gealtert.
- Wagen: zurückgesetzt 17,9 m, 18 km/h, Räder drehen sich.

Bild: unreal-sturz-blut.png (Nahaufnahme: Toter auf dem Bauch, Spurenbild, Lache, abgetrennter Unterarm)

## Ehrlich offen
- Der Boden des Wald-Spiels wirkt noch trocken (gleiche zwei Bodentexturen für jede Landschaft) – nächster Schritt: Boden je Landschaft.
- Blut fließt nicht dem Gelände nach; keine Spritzer an Wänden oder Kleidung.

## SHA-256
- unreal-sturz-blut.png: bbe9dc475a65eace3bd4d7d248a4839d9d095422df8f8bb834477602cd83ec77

## Rohlog-Auszüge
```
Result: Succeeded
LogTrendo: Display: TRENDO-NATUR 164059 Pflanzen/Felsen in 39 Arten, Landschaft wald
LogTrendo: Display: TRENDO-TIER 8 Tiere gesetzt (Landschaft wald: DeerDoe, DeerStag, Fox, Wolf), 1 verschiedene Felle
LogTemp: Display: TRENDO-FIGUR MetaHuman=ja (BP_Waldlaeuferin_C) Mensch=ja
LogTrendo: Display: TRENDO-FLACKERN Start-Sprung 22 cm Zeit 0.68 Kopf V(X=-1.55, Y=8.87, Z=145.41) (vorher V(X=-1.92, Y=9.18, Z=166.99)) Becken V(X=1.21, Y=-0.86, Z=72.31) Boden=ja Fallen=nein Z=115 Animation MH_BS_MM_Bewegung
LogTrendo: Display: TRENDO-FLACKERN Start-Sprung 27 cm Zeit 0.85 Kopf V(X=0.78, Y=11.16, Z=118.57) (vorher V(X=-1.55, Y=8.87, Z=145.41)) Becken V(X=0.83, Y=-0.53, Z=45.65) Boden=ja Fallen=nein Z=115 Animation MH_BS_MM_Bewegung
LogTrendo: Display: TRENDO-WEG Wegnetz=ja Pfade=30 davon mit Umweg=0
LogTrendo: Display: TRENDO-WEG Hindernis rock_moss_set_01_rock06: gerade versperrt, Pfad 23.0 m statt 21.0 m mit 7 Punkten, führt drumherum
LogTrendo: Display: TRENDO-BODEN Gegner geht=ja Abstand Füße–Boden=2.2 cm
LogTrendo: Display: TRENDO-GEGNER alarmiert=ja Abstand vorher=1100 nachher=288 Leben 3→3 verfolgt
LogTrendo: Display: TRENDO-FLACKERN Gegner in der Ferne bis 45 m: 943 Bilder, ohne Posen-Update 0 (0%), Sprünge über 12 cm 0 (dazu 2 beim Erscheinen), größter Sprung je Bild 27.0 cm (ruhig)
LogTrendo: Display: TRENDO-TIER vorher: 8 Tiere, DeerDoe geht, walk, gelaufen 12 m
LogTrendo: Display: TRENDO-TIER DeerDoe flieht=ja Abstand 5.0 → 13.6 m Bewegung=run Tempo 18 km/h Fluchten=1
LogTemp: Display: TRENDO-GLIED abgetrennt=lowerarm_r Kleidung=nein
LogTrendo: Display: TRENDO-KAMPF Körper: Becken Z=30, Gegner Z=115, Simulation=ja, Physik-Körper=21
LogTrendo: Display: TRENDO-KAMPF Gegner besiegt=ja Ragdoll=ja Becken über Boden=30 cm (liegt) Blut=48 abgetrennt=1 Gewalt=brutal
LogTrendo: Display: TRENDO-KAMPF Kopf nach dem Liegen: 0.3 cm Bewegung in 0,6 s (ruhig)
LogTrendo: Display: TRENDO-GLIED liegt -4 cm über dem Boden, 1.1 m vom Körper
LogTrendo: Display: TRENDO-BLUT Zeitraffer +5 min: 54 Flecken gealtert
LogTrendo: Display: TRENDO-WAGEN freie Bahn 60 m, Lenkziel -180° zur Fahrtrichtung
LogTrendo: Display: TRENDO-WAGEN eingestiegen
LogTrendo: Display: TRENDO-WAGEN gesteuert=ja Klasse=TrendoCar Tempo=18 km/h Strecke=17.9 m Raddrehung=333° (Reifen drehen sich) Gang=-1 Neigung max=9°
LogTrendo: Display: TRENDO-WAGEN ausgestiegen, Figur=sichtbar
LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=8 Gegner=0 Natur=164059 Figur=ja Boden=ja Z=92
LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```
