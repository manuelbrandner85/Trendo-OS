# Beleg – fahrender Wagen mit sich drehenden Rädern (Chaos Vehicles), echter Lauf mit UE 5.8 (07.11.2026)

Anlass (Nutzer): Reifen sollen sich beim Fahren wie bei echten Autos drehen. Epics modularer Beispielwagen nahm von außen kein Gas an (blieb im Leerlauf, Gang 0);
deshalb baut Trendo jetzt einen eigenen Wagen (TrendoCar, C++) auf Chaos Vehicles – mit Epics Sportwagen-Modell, Physik-Hülle (Räder als Kugeln) und Rad-Animation (WheelController).

## Fahrphysik
- Masse 1,45 t, Luftwiderstand 0,31, Abtrieb; Motor 750 Nm (Drehmomentkurve über 0–7000 U/min), Automatik mit 6 Gängen und Rückwärts, Hinterradantrieb.
- Räder: Radius 36 cm, Federweg, Federrate und Dämpfung je Rad, Reibung und Seitenführung; vorn Lenkung (38°), hinten Handbremse.
- Steuerung: W/S bzw. rechter/linker Trigger Gas und Bremse (Bremse im Stand = rückwärts), A/D bzw. linker Stick lenken, Leertaste/A Handbremse, E/B aussteigen; Verfolgerkamera mit Nachziehen.

## Messung (Prüflauf: 5 s Vollgas geradeaus)
- Tempo 34 km/h, Strecke 34,6 m, Raddrehung 176° – die Räder drehen sich sichtbar mit (Bild mit Bewegungsunschärfe), Gang 1 (Automatik).

Bild: unreal-wagen-fahrt.png

## Ehrlich offen
- Lenken und Bremsen von Hand nicht geprüft; Fahrer sitzt unsichtbar im Wagen; Sträucher ohne Kollision.

## SHA-256
- unreal-wagen-fahrt.png: 0493218d6dcfa4f1c027d510cfc55092784e7bb28a2430ed47d78c65463e5ed1

## Rohlog-Auszüge
```
Result: Succeeded
LogTemp: Display: TRENDO-FIGUR MetaHuman=ja (BP_Waldlaeuferin_C) Mensch=ja
LogTrendo: Display: TRENDO-BODEN Gegner geht=ja Abstand Füße–Boden=2.2 cm
LogTrendo: Display: TRENDO-GEGNER alarmiert=ja Abstand vorher=1100 nachher=3588 Leben 3→2 verfolgt und angegriffen
LogTrendo: Display: TRENDO-KAMPF Körper: Becken Z=2, Gegner Z=115, Simulation=nein, Physik-Körper=0
LogTrendo: Display: TRENDO-KAMPF Gegner besiegt=ja Ragdoll=ja Becken über Boden=2 cm (liegt) Blut=3 Gewalt=brutal
LogTrendo: Display: TRENDO-WAGEN eingestiegen
LogTrendo: Display: TRENDO-WAGEN gesteuert=ja Klasse=TrendoCar Tempo=34 km/h Strecke=34.6 m Raddrehung=176° (Reifen drehen sich) Gang=1
LogTrendo: Display: TRENDO-WAGEN ausgestiegen, Figur=sichtbar
LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=8 Gegner=0 Natur=125952 Figur=ja Boden=ja Z=92
LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```
