# Beleg – abtrennbare Körperteile bei brutalen Spielen, echter Lauf mit UE 5.8 (07.11.2026)

Beim Bau der Menschen schneidet Unreals Geometrie-Skript aus dem MetaHuman des Gegners vier Glieder heraus (Unterarme mit Hand, Unterschenkel mit Fuß): Punkte nach ihrem stärksten Knochen,
Schnittkante geschlossen, Drehpunkt am Gelenk, Materialien übernommen, Physik-Kollision. Kleidung wird mitgeschnitten, wo sie das Glied bedeckt (Hosenbeine; das T-Shirt ist kurzärmelig).

Im Spiel (nur Gewalt-Stufe „brutal“): beim tödlichen Hieb wird ein Glied am Körper und an der Kleidung ausgeblendet, das ausgeschnittene Stück fliegt an genau der Stelle als Physikteil in Schlagrichtung weg, Blut am Stumpf; der Körper fällt als Ragdoll.

## Messung
- Bau: 6 abtrennbare Stücke (4 Körper, 2 Hosenbeine), Schnitt je Glied unter 1 s.
- Kampf-Probe: abgetrennt=lowerarm_l (vorher in einem Lauf calf_r mit Hosenbein), Ragdoll liegt (Becken 20 cm über dem Boden).

Bild: unreal-glieder.png (Kampfort von schräg oben)

## Ehrlich offen
- Im Bild ist das abgetrennte Stück kaum zu sehen (fliegt weg) und das Blut zu klein – Blutlache unter dem Körper, sichtbare Wunde am Stumpf und Landeplatz des Glieds werden verbessert.
- Kopf/Rumpf werden nicht abgetrennt; Schnittfläche hat noch Hautmaterial statt Wunde.

## SHA-256
- unreal-glieder.png: 20e5819a743667c6c298b1ca5cf133c43a5aea0dbf31e954b177463e9413a999

## Rohlog-Auszüge
```
Result: Succeeded
LogTemp: Display: TRENDO-FIGUR MetaHuman=ja (BP_Waldlaeuferin_C) Mensch=ja
LogTrendo: Display: TRENDO-BODEN Gegner geht=ja Abstand Füße–Boden=2.2 cm
LogTrendo: Display: TRENDO-GEGNER alarmiert=ja Abstand vorher=1100 nachher=3586 Leben 3→2 verfolgt und angegriffen
LogTemp: Display: TRENDO-GLIED abgetrennt=lowerarm_l Kleidung=nein
LogTrendo: Display: TRENDO-KAMPF Körper: Becken Z=20, Gegner Z=115, Simulation=ja, Physik-Körper=22
LogTrendo: Display: TRENDO-KAMPF Gegner besiegt=ja Ragdoll=ja Becken über Boden=20 cm (liegt) Blut=3 abgetrennt=1 Gewalt=brutal
LogTrendo: Display: TRENDO-WAGEN eingestiegen
LogTrendo: Display: TRENDO-WAGEN gesteuert=ja Klasse=TrendoCar Tempo=59 km/h Strecke=41.5 m Raddrehung=206° (Reifen drehen sich) Gang=2
LogTrendo: Display: TRENDO-WAGEN ausgestiegen, Figur=sichtbar
LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=8 Gegner=0 Natur=125952 Figur=ja Boden=ja Z=92
LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```
