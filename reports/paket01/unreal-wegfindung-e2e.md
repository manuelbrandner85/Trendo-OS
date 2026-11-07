# Beleg – Wegfindung um Hindernisse, echter Lauf mit UE 5.8 (07.11.2026)

## Was gebaut ist
- Wegnetz (Recast-NavMesh) entsteht zur Laufzeit rund um jeden Gegner (30 m Radius, abgebaut ab 50 m) – auch große offene Welten bleiben schnell. Grenzen 1 km × 1 km legt der Welt-Bau an.
- Gegner (Streife, Verfolgung, Suche am letzten Ort, Rückzug des Bosses) suchen alle 0,4 s oder wenn das Ziel wandert einen Pfad und gehen ihn Punkt für Punkt ab; das bisherige Ausweichen bleibt als Rückfall.
- Projekt: Modul NavigationSystem, DefaultEngine.ini RuntimeGeneration=Dynamic und Erzeugung nur um die Gegner.

## Messung
- Wegnetz=ja, 33 Pfade im Prüflauf, 4 davon mit Umweg.
- Hindernis-Probe: Felsblock (boulder_01_2k) versperrt den geraden Weg zu einem Punkt 4,5 m dahinter – Pfad 23,8 m statt 23,2 m mit 5 Punkten, führt drumherum (kein Teilstück durch den Fels).
- Gegner verfolgt weiterhin (11 m → 2,2 m), Füße 2,1 cm über dem Boden; Flackern ruhig (0 Sprünge, 3 beim Erscheinen getrennt gezählt).

## Prüf-Aufbau (offen gelegt)
- Vor der Wagen-Probe werden eingefrorene Gegner im Umkreis von 10 m um 15 m zur Seite gestellt: Ein für die Probe eingefrorener Charakter ist für die Fahrphysik eine unbewegliche Wand (ein Lauf blieb so nach 2,6 m stehen).

## Ehrlich offen
- Überfahren von Gegnern (Wagen stößt Figur um) ist nicht gebaut. Fliegende/schwimmende Wesen nutzen kein Wegnetz.

## Rohlog-Auszüge
```
Result: Succeeded
LogTemp: Display: TRENDO-FIGUR MetaHuman=ja (BP_Waldlaeuferin_C) Mensch=ja
LogTrendo: Display: TRENDO-WEG Wegnetz=ja Pfade=33 davon mit Umweg=4
LogTrendo: Display: TRENDO-WEG Hindernis boulder_01_2k: gerade versperrt, Pfad 23.8 m statt 23.2 m mit 5 Punkten, führt drumherum
LogTrendo: Display: TRENDO-BODEN Gegner geht=ja Abstand Füße–Boden=2.1 cm
LogTrendo: Display: TRENDO-GEGNER alarmiert=ja Abstand vorher=1100 nachher=221 Leben 3→3 verfolgt
LogTrendo: Display: TRENDO-FLACKERN Gegner in der Ferne bis 52 m: 630 Bilder, ohne Posen-Update 19 (3%), Sprünge über 12 cm 0 (dazu 3 beim Erscheinen), größter Sprung je Bild 23.4 cm (ruhig)
LogTemp: Display: TRENDO-GLIED abgetrennt=calf_l Kleidung=ja
LogTrendo: Display: TRENDO-KAMPF Körper: Becken Z=20, Gegner Z=115, Simulation=ja, Physik-Körper=22
LogTrendo: Display: TRENDO-KAMPF Gegner besiegt=ja Ragdoll=ja Becken über Boden=20 cm (liegt) Blut=9 abgetrennt=1 Gewalt=brutal
LogTrendo: Display: TRENDO-GLIED liegt 2 cm über dem Boden, 1.4 m vom Körper
LogTrendo: Display: TRENDO-BLUT Zeitraffer +5 min: 9 Flecken gealtert
LogTrendo: Display: TRENDO-WAGEN freie Bahn 49 m, Lenkziel -45° zur Fahrtrichtung
LogTrendo: Display: TRENDO-WAGEN eingestiegen
LogTrendo: Display: TRENDO-WAGEN gesteuert=ja Klasse=TrendoCar Tempo=26 km/h Strecke=42.6 m Raddrehung=303° (Reifen drehen sich) Gang=2 Neigung max=13°
LogTrendo: Display: TRENDO-WAGEN ausgestiegen, Figur=sichtbar
LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=8 Gegner=0 Natur=125952 Figur=ja Boden=ja Z=92
LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```
