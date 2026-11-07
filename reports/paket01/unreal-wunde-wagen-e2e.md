# Beleg – Wunde an der Schnittfläche, Wagen kippt nicht mehr in der Kurve, echter Lauf mit UE 5.8 (07.11.2026)

## Wunde statt Haut
- Vorher: Die Schnittkante wurde geschlossen (1 Loch je Glied), trug aber das Hautmaterial. Die neuen Dreiecke belegen freie Nummern gelöschter Dreiecke – ein Zählen nach Nummern fand sie deshalb nicht.
- Jetzt: Nahtkanten verschweißen, Loch schließen, neue Dreiecke per Mengenvergleich finden (43–61 je Glied) und mit eigenem Material belegen.
- Material M_TrendoWunde: frisches Fleischrot und geronnenes Blut im Wechsel (Rauschmuster am Glied, wandert beim Fliegen nicht), Rauheit 0,22–0,45 (feucht glänzend), Glanz 0,5.
- Hosenbeine bleiben offen: man sieht hinein auf die Wunde statt auf einen Stoffdeckel.
- Messung Bau: 6 Stücke, Glied_calf_l_Koerper hat die Materialien MI_Body_Baked_VT und M_TrendoWunde.

Bild: unreal-glied-wunde.png (Nahaufnahme des abgetrennten Unterarms auf der Schnittseite, Blut am Boden)

## Wagen
- Ursache 1: Im Prüflauf fuhr der Wagen gegen einen Baum (1 km/h nach 26,5 m). Jetzt prüft die Probe die freie Bahn (Kugel in Wagenbreite, 16 Richtungen, zuerst geradeaus) und lenkt wie ein Spieler dorthin – Drehen per Teleport ließ den laufenden Wagen kippen und wurde verworfen.
- Ursache 2: Beim Einlenken mit Vollgas kippte der Sportwagen auf zwei Räder – die Seitenhaftung war dreifach (Faktor 3) und der Schwerpunkt mittig in der Physik-Hülle.
  Jetzt Schwerpunkt 35 cm tiefer (flacher Sportwagen), Haftung 2,5: in scharfen Kurven rutscht er, statt zu kippen.
- Messung: Lenkziel −45°, Strecke 42,5 m, Räder drehen sich (284°), Automatik im 2. Gang, größte Schräglage 12° (Karosserieneigung, kein Kippen). Vorher in einem Lauf: kopfüber nach 9,9 m.

Bild: unreal-wagen-kurve.png (aus der Verfolgerkamera, Wagen aufrecht nach der Kurve)

## Ehrlich offen
- Tempo am Ende der Kurvenfahrt 25 km/h (geradeaus vorher 55–60 km/h); Fahrverhalten von Hand am Controller nicht geprüft.
- Die Wunde ist im Bild klein; eine eigene Nahaufnahme mit Tiefenschärfe fehlt. Blutflecken noch ohne Eintrocknen/Versickern.

## SHA-256
- unreal-glied-wunde.png: 56655215404383ceeeab643f22417a59e66444f55ee2fe3442bd9f68ba5b800d
- unreal-wagen-kurve.png: 2fbeee93630ec1071cc5ec418871cdb2c3592fbd880a8edc32f31d4067a32198

## Rohlog-Auszüge
```
LogPython: TRENDO-WUNDE Stuecke=6 Materialien calf_l=MI_Body_Baked_VT,M_TrendoWunde
Result: Succeeded
LogTemp: Display: TRENDO-FIGUR MetaHuman=ja (BP_Waldlaeuferin_C) Mensch=ja
LogTrendo: Display: TRENDO-BODEN Gegner geht=ja Abstand Füße–Boden=2.2 cm
LogTrendo: Display: TRENDO-GEGNER alarmiert=ja Abstand vorher=1100 nachher=3587 Leben 3→2 verfolgt und angegriffen
LogTemp: Display: TRENDO-GLIED abgetrennt=lowerarm_l Kleidung=nein
LogTrendo: Display: TRENDO-KAMPF Körper: Becken Z=20, Gegner Z=115, Simulation=ja, Physik-Körper=22
LogTrendo: Display: TRENDO-KAMPF Gegner besiegt=ja Ragdoll=ja Becken über Boden=20 cm (liegt) Blut=9 abgetrennt=1 Gewalt=brutal
LogTrendo: Display: TRENDO-GLIED liegt 7 cm über dem Boden, 1.4 m vom Körper
LogTrendo: Display: TRENDO-WAGEN freie Bahn 49 m, Lenkziel -45° zur Fahrtrichtung
LogTrendo: Display: TRENDO-WAGEN eingestiegen
LogTrendo: Display: TRENDO-WAGEN gesteuert=ja Klasse=TrendoCar Tempo=25 km/h Strecke=42.5 m Raddrehung=284° (Reifen drehen sich) Gang=2 Neigung max=12°
LogTrendo: Display: TRENDO-WAGEN ausgestiegen, Figur=sichtbar
LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=8 Gegner=0 Natur=125952 Figur=ja Boden=ja Z=92
LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```
