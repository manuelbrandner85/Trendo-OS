# Beleg – abtrennbare Körperteile bei brutalen Spielen, echter Lauf mit UE 5.8 (07.11.2026)

Beim Bau der Menschen schneidet Unreals Geometrie-Skript aus dem MetaHuman des Gegners vier Glieder heraus (Unterarme mit Hand, Unterschenkel mit Fuß): Punkte nach ihrem stärksten Knochen,
Schnittkante geschlossen, Drehpunkt am Gelenk, Materialien übernommen, Physik-Kollision. Kleidung wird mitgeschnitten, wo sie das Glied bedeckt (Hosenbeine; das T-Shirt ist kurzärmelig).

Im Spiel (nur Gewalt-Stufe „brutal“): beim tödlichen Hieb wird ein Glied am Körper und an der Kleidung ausgeblendet, das ausgeschnittene Stück fliegt an genau der Stelle als Physikteil in Schlagrichtung weg, der Körper fällt als Ragdoll; Blutspritzer am Trefferort, nach dem Sturz eine Blutlache unter dem Körper und eine kleinere am Stumpf. Blut liegt nur auf dem Gelände (Treffer-Strahl nur auf Gelände, der Körper nimmt keine Boden-Flecken an).

## Messung
- Bau: 6 abtrennbare Stücke (4 Körper, 2 Hosenbeine), Schnitt je Glied unter 1 s.
- Kampf-Probe: abgetrennt=calf_l mit Hosenbein (frühere Läufe lowerarm_l, lowerarm_r, calf_r), Ragdoll liegt (Becken 20 cm über dem Boden), 9 Blutflecken.
- Behobener Fehler: vorher traf der Blut-Strahl den liegenden Körper, die ganze Figur wurde rot eingefärbt und am Boden war kein Blut; jetzt Blutlache sichtbar, Kleidung unverändert.

Bild: unreal-glieder.png (Kampfort von schräg oben: Blutlache, liegender Gegner, abgetrennter Unterschenkel daneben)

## Ehrlich offen
- Die Blutflecken sind noch einheitlich dunkelrot (kein Eintrocknen, kein Versickern im Boden); ein Wagen-Lauf im selben Prüflauf endete bei 1 km/h nach 26,5 m (Aufprall, nicht Teil dieser Änderung).
- Kopf/Rumpf werden nicht abgetrennt; Schnittfläche hat noch Hautmaterial statt Wunde.

## SHA-256
- unreal-glieder.png: d53e3b3fe0455f346501bc671d54ae1b8b4b6e8790458f4525e6d256368a6f8a

## Rohlog-Auszüge
```
Result: Succeeded
LogTemp: Display: TRENDO-FIGUR MetaHuman=ja (BP_Waldlaeuferin_C) Mensch=ja
LogTrendo: Display: TRENDO-BODEN Gegner geht=ja Abstand Füße–Boden=2.2 cm
LogTrendo: Display: TRENDO-GEGNER alarmiert=ja Abstand vorher=1100 nachher=3585 Leben 3→2 verfolgt und angegriffen
LogTemp: Display: TRENDO-GLIED abgetrennt=calf_l Kleidung=ja
LogTrendo: Display: TRENDO-KAMPF Körper: Becken Z=20, Gegner Z=115, Simulation=ja, Physik-Körper=22
LogTrendo: Display: TRENDO-KAMPF Gegner besiegt=ja Ragdoll=ja Becken über Boden=20 cm (liegt) Blut=9 abgetrennt=1 Gewalt=brutal
LogTrendo: Display: TRENDO-WAGEN eingestiegen
LogTrendo: Display: TRENDO-WAGEN gesteuert=ja Klasse=TrendoCar Tempo=1 km/h Strecke=26.5 m Raddrehung=337° (Reifen drehen sich) Gang=1
LogTrendo: Display: TRENDO-WAGEN ausgestiegen, Figur=sichtbar
LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=8 Gegner=0 Natur=125952 Figur=ja Boden=ja Z=92
LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```
