# Beleg – physikalischer Sturz (Ragdoll) und Wagen mit Automatik, echter Lauf mit UE 5.8 (07.11.2026)

Ursache des Durchfallens: Trendo schaltet beim Aufbau die Kollision des ganzen MetaHuman-Teils ab (damit er nicht mit der Gegner-Kapsel kollidiert) – das galt auch für die Physik-Körper. Beim Sturz wird sie jetzt wieder eingeschaltet.

## Standard im Spiel
- Besiegte Gegner fallen als Ragdoll (Physik-Hülle des Engine-Mannequins, gleiche Körperknochen wie MetaHuman; CCD gegen Durchrutschen), mit Stoß in Schlagrichtung; Sturz-Animation nur als Rückfall.
- Wagen (Chaos Vehicles) schaltet selbst: im Prüflauf nach 5 s Vollgas im 2. Gang.

## Messung (Prüflauf, Gewalt-Stufe brutal)
- Kampf: Gegner besiegt, Ragdoll mit 22 Physik-Körpern, Becken 20 cm über dem Boden (liegt), 3 Blutflecken.
- Wagen: 55 km/h, 43,5 m in 5 s, Räder drehen sich (235°), Gang 2.
- Gegner zuvor: geht auf dem Boden (2,2 cm), verfolgt und greift an; Unreal-Tests 3/3, TRENDO-OK.

Bild: unreal-ragdoll-wagen.png (liegender Gegner nach dem Sturz)

## SHA-256
- unreal-ragdoll-wagen.png: 613b0b3c535b57eae38cd6aa9b6c65387c9aeaf495f77dad6437826d87c8686c

## Rohlog-Auszüge
```
Result: Succeeded
LogTemp: Display: TRENDO-FIGUR MetaHuman=ja (BP_Waldlaeuferin_C) Mensch=ja
LogTrendo: Display: TRENDO-BODEN Gegner geht=ja Abstand Füße–Boden=2.2 cm
LogTrendo: Display: TRENDO-GEGNER alarmiert=ja Abstand vorher=1100 nachher=3587 Leben 3→2 verfolgt und angegriffen
LogTrendo: Display: TRENDO-KAMPF Körper: Becken Z=20, Gegner Z=115, Simulation=ja, Physik-Körper=22
LogTrendo: Display: TRENDO-KAMPF Gegner besiegt=ja Ragdoll=ja Becken über Boden=20 cm (liegt) Blut=3 Gewalt=brutal
LogTrendo: Display: TRENDO-WAGEN eingestiegen
LogTrendo: Display: TRENDO-WAGEN gesteuert=ja Klasse=TrendoCar Tempo=55 km/h Strecke=43.5 m Raddrehung=235° (Reifen drehen sich) Gang=2
LogTrendo: Display: TRENDO-WAGEN ausgestiegen, Figur=sichtbar
LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=8 Gegner=0 Natur=125952 Figur=ja Boden=ja Z=92
LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```
