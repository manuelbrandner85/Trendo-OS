# Beleg – Weitere Tierarten passend zur Landschaft, echter Lauf mit UE 5.8 (07.11.2026)

## Quelle und Lizenz
- ANIMAL VARIETY PACK von PROTOFACTOR INC, dauerhaft kostenlos (Unreal Engine Permanent Collection), Fab-Lizenz privat und professionell 0 €: https://www.fab.com/listings/2dd7964c-a601-4264-a53d-465dcae1644c
- Geholt über das Fab-Fenster im Unreal-Editor des Testprojekts (Fassung 5.5, läuft in 5.8); der Launcher bot nur ein Projekt des Nutzers an – dort wurde nichts hinzugefügt.
- Zwischenspeicher TrendoWerkzeuge/assets/tiere-pakete: neue Spiele bekommen das Paket beim Bauen ohne neuen Download. Namensnennung in lizenzen.txt im Spiel.

## Was gebaut ist
- Landschaft des Spiels aus der Spielbibel (Savanne, Hof oder Wald; ausdrücklich gewählt gewinnt) in level.json „biome“.
- Lebensraum je Tierart: Hyäne → Savanne; Rothirsch, Hirschkuh, Fuchs, Wolf → Wald; Hausschwein → Hof. Im Spiel erscheinen nur passende Tiere – Wolfsrudel zu dritt, Fuchs allein, Hirsche und Schweine zu zweit.
- Bewegungen aus dem Paket (Stehen, Umschauen, Grasen/Schnüffeln, Gehen, Laufen, Sterben, Angriff) in ihrer echten Länge; die Krähe bleibt weg (Vogel – das Tier läuft am Boden).
- Das Paket kommt auf die Kochliste, damit die Tiere auch im fertigen Spiel da sind.

## Messung (Testspiel „Waldläufer“ → Landschaft Wald)
- 8 Tiere gesetzt: Hirschkuh, Rothirsch, Fuchs, Wolf; keine Hyäne, kein Schwein (passen nicht in den Wald).
- Hirschkuh geht 17 m umher; Figur auf 5 m → flieht auf 11,2 m mit 21 km/h (rennt).
- Fehler auf dem Weg, behoben: Der Lebensraum-Schlüssel „hyen“ passte nicht auf den Ordnernamen „Hyaene“ – die Hyäne erschien zuerst im Wald.

Bild: unreal-waldtiere.png (Hirschkühe im Testspiel)

## Ehrlich offen
- Die Landschaft des Testspiels sieht trockener aus als ein Wald (Bodenmaterial und Pflanzen aus dem Welt-Bau) – die Tierauswahl folgt der Spielbibel, die Bodengestaltung je Landschaft ist ein eigener Schritt.
- Vögel (Krähe) fliegen nicht; Tiere greifen nicht an.

## SHA-256
- unreal-waldtiere.png: 5dd6e7ee5f4e0b93fb989a6fd6abbc200e87448eced7e3178beadaf67af2d963

## Rohlog-Auszüge
```
Result: Succeeded
LogPython: TRENDO-WELT Blut-Decal: /Game/Trendo/Natur/M_TrendoBlut.M_TrendoBlut
LogPython: TRENDO-WELT-OK 39 Pflanzen/Felsen-Modelle, 9 Spielobjekt-Modelle
Result: Succeeded
LogTrendo: Display: TRENDO-TIER 8 Tiere gesetzt (Landschaft wald: DeerDoe, DeerStag, Fox, Wolf), 1 verschiedene Felle
LogTemp: Display: TRENDO-FIGUR MetaHuman=ja (BP_Waldlaeuferin_C) Mensch=ja
LogTrendo: Display: TRENDO-FLACKERN Start-Sprung 13 cm Zeit 0.63 Kopf V(X=-1.74, Y=8.99, Z=153.71) (vorher V(X=-1.93, Y=9.18, Z=166.99)) Becken V(X=1.35, Y=-0.98, Z=80.66) Boden=ja Fallen=nein Z=115 Animation MH_BS_MM_Bewegung
LogTrendo: Display: TRENDO-FLACKERN Start-Sprung 27 cm Zeit 0.73 Kopf V(X=-1.18, Y=8.91, Z=126.88) (vorher V(X=-1.74, Y=8.99, Z=153.71)) Becken V(X=0.92, Y=-0.64, Z=53.70) Boden=ja Fallen=nein Z=115 Animation MH_BS_MM_Bewegung
LogTrendo: Display: TRENDO-WEG Wegnetz=ja Pfade=38 davon mit Umweg=0
LogTrendo: Display: TRENDO-WEG Hindernis tree_small_02_2k: gerade versperrt, Pfad 24.9 m statt 24.5 m mit 3 Punkten, führt drumherum
LogTrendo: Display: TRENDO-BODEN Gegner geht=ja Abstand Füße–Boden=2.2 cm
LogTrendo: Display: TRENDO-GEGNER alarmiert=ja Abstand vorher=1100 nachher=4721 Leben 3→2 verfolgt und angegriffen
LogTrendo: Display: TRENDO-FLACKERN Gegner in der Ferne bis 55 m: 772 Bilder, ohne Posen-Update 0 (0%), Sprünge über 12 cm 0 (dazu 2 beim Erscheinen), größter Sprung je Bild 26.8 cm (ruhig)
LogTrendo: Display: TRENDO-TIER vorher: 8 Tiere, DeerDoe geht, walk, gelaufen 17 m
LogTrendo: Display: TRENDO-TIER DeerDoe flieht=ja Abstand 5.0 → 11.2 m Bewegung=run Tempo 21 km/h Fluchten=1
LogTemp: Display: TRENDO-GLIED abgetrennt=calf_r Kleidung=ja
LogTrendo: Display: TRENDO-KAMPF Körper: Becken Z=18, Gegner Z=115, Simulation=ja, Physik-Körper=22
LogTrendo: Display: TRENDO-KAMPF Gegner besiegt=ja Ragdoll=ja Becken über Boden=18 cm (liegt) Blut=9 abgetrennt=1 Gewalt=brutal
LogTrendo: Display: TRENDO-GLIED liegt -16770 cm über dem Boden, 0.9 m vom Körper
LogTrendo: Display: TRENDO-BLUT Zeitraffer +5 min: 9 Flecken gealtert
LogTrendo: Display: TRENDO-WAGEN freie Bahn 49 m, Lenkziel -45° zur Fahrtrichtung
LogTrendo: Display: TRENDO-WAGEN eingestiegen
LogTrendo: Display: TRENDO-WAGEN gesteuert=ja Klasse=TrendoCar Tempo=25 km/h Strecke=42.6 m Raddrehung=350° (Reifen drehen sich) Gang=2 Neigung max=14°
LogTrendo: Display: TRENDO-WAGEN ausgestiegen, Figur=sichtbar
LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=8 Gegner=0 Natur=125952 Figur=ja Boden=ja Z=92
LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
LogPython: TRENDO-WELT Tier Hyaene: 12 Bewegungen (atk 0.7 s, bark 2.8 s, die 1.7 s, idle 1.9 s, idle2 1.8 s, roar 2.2 s, run 0.6 s, sit 3.7 s, sleep 0.4 s, smell 2.7 s, up 0.9 s, walk 1.4 s)
LogPython: TRENDO-WELT Tier DeerDoe (Paket): 6 Bewegungen, Lebensraum wald
LogPython: TRENDO-WELT Tier DeerStag (Paket): 7 Bewegungen, Lebensraum wald
LogPython: TRENDO-WELT Tier Fox (Paket): 6 Bewegungen, Lebensraum wald
LogPython: TRENDO-WELT Tier Pig (Paket): 7 Bewegungen, Lebensraum hof
LogPython: TRENDO-WELT Tier Wolf (Paket): 6 Bewegungen, Lebensraum wald
```
