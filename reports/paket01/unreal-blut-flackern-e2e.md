# Beleg – Blut trocknet ein, Gegner flackern in der Ferne nicht mehr, echter Lauf mit UE 5.8 (07.11.2026)

## Blut trocknet und zieht ein
- Jeder Blutfleck merkt sich seine Entstehung (Material-Parameter „Start“). Das Material rechnet das Alter aus der Spielzeit: frisch tiefrot, nass glänzend (Rauheit 0,16);
  nach etwa 3 Minuten rotbraun, matt (Rauheit 0,8), weniger Glanz, Deckkraft auf 72 % (in den Boden gezogen). Flecken bleiben 10 Minuten liegen.
- Prüflauf mit Zeitraffer: alle Flecken um 5 Minuten gealtert, gleicher Blick wie das Kampfbild.

Bilder: unreal-blut-frisch.png (direkt nach dem Kampf), unreal-blut-eingetrocknet.png (Zeitraffer +5 min)

## Flackern in der Ferne (Messung statt Annahme)
- Neue Probe TRENDO-FLACKERN: jeder laufende Gegner in über 25 m Entfernung, Kopf-Knochen im Körperraum Bild für Bild; gezählt werden Bilder ohne Posen-Update und Sprünge über 12 cm.
- Erster Messwert: 11 Sprünge bis 61 cm auf 1081 Bilder. Ursachen, einzeln protokolliert und behoben:
  1. Streife folgte einem wandernden Zielpunkt und blieb bei 40 cm Nähe stehen → ständiges Stop-and-go (Stehen/Gehen alle 0,36 s). Jetzt Wegpunkte, davor sanft abbremsen, 2–4 s stehen und umschauen.
  2. Stehen und Gehen waren zwei Animationen, hart getauscht. Jetzt je Mensch eine Bewegungs-Mischung Stehen (0) → Gehen (231 cm/s) → Laufen (500 cm/s), Tempo weich nachgeführt; dazu Mindestdauer und Fall-Verzögerung für Sprung/Fall.
  3. Die Messung selbst verglich nach einer Pause mit einem veralteten Wert – jetzt verworfen.
- Endmessung: 645 Bilder, 1 Sprung (28 cm, im ersten Spielbild beim Laden), Posen-Update in 97 % der Bilder → „ruhig“.

## Ehrlich offen
- Der eine Sprung im ersten Bild (Figur setzt nach dem Erscheinen auf) ist nicht behoben, fällt in den Ladebeginn.
- Gemessen wurde ein Gegner (der Prüfwelt); Bildfolge als Video nicht aufgenommen.

## SHA-256
- unreal-blut-frisch.png: 662b5b9d63b7f097c566f1f04fd96a2c3b609a6656e994c6295b83cad4754378
- unreal-blut-eingetrocknet.png: afec833e2ff6fb6970af8c787d69e215ea14ab9d04e8aa3b9139b115bfe67d00

## Rohlog-Auszüge – Blut
```
LogTrendo: Display: TRENDO-KAMPF Gegner besiegt=ja Ragdoll=ja Becken über Boden=20 cm (liegt) Blut=9 abgetrennt=1 Gewalt=brutal
LogTrendo: Display: TRENDO-BLUT Zeitraffer +5 min: 9 Flecken gealtert
```

## Rohlog-Auszüge – Flackern (Endlauf)
```
Result: Succeeded
LogTemp: Display: TRENDO-FIGUR MetaHuman=ja (BP_Waldlaeuferin_C) Mensch=ja
LogTrendo: Display: TRENDO-FLACKERN Sprung 28 cm: TrendoEnemy_0 Abstand 51 m Tempo 150 Animation MH_BS_MM_Bewegung (vorher MH_BS_MM_Bewegung) Boden=ja Zeit 0.64 Kopf V(X=-1.31, Y=8.71, Z=130.84) (vorher V(X=-1.86, Y=9.06, Z=158.91)) Becken V(X=0.86, Y=-0.60, Z=49.97) Körper-Drehung R(Y=-106.07) Pos 0.1
LogTrendo: Display: TRENDO-BODEN Gegner geht=ja Abstand Füße–Boden=2.2 cm
LogTrendo: Display: TRENDO-GEGNER alarmiert=ja Abstand vorher=1100 nachher=5728 Leben 3→2 verfolgt und angegriffen
LogTrendo: Display: TRENDO-FLACKERN Gegner in der Ferne bis 55 m: 645 Bilder, ohne Posen-Update 18 (3%), Sprünge über 12 cm 1, größter Sprung je Bild 28.1 cm (ruhig)
LogTemp: Display: TRENDO-GLIED abgetrennt=lowerarm_r Kleidung=nein
LogTrendo: Display: TRENDO-KAMPF Körper: Becken Z=19, Gegner Z=115, Simulation=ja, Physik-Körper=22
LogTrendo: Display: TRENDO-KAMPF Gegner besiegt=ja Ragdoll=ja Becken über Boden=19 cm (liegt) Blut=9 abgetrennt=1 Gewalt=brutal
LogTrendo: Display: TRENDO-GLIED liegt 7 cm über dem Boden, 1.4 m vom Körper
LogTrendo: Display: TRENDO-BLUT Zeitraffer +5 min: 9 Flecken gealtert
LogTrendo: Display: TRENDO-WAGEN freie Bahn 49 m, Lenkziel -45° zur Fahrtrichtung
LogTrendo: Display: TRENDO-WAGEN eingestiegen
LogTrendo: Display: TRENDO-WAGEN gesteuert=ja Klasse=TrendoCar Tempo=26 km/h Strecke=42.5 m Raddrehung=407° (Reifen drehen sich) Gang=2 Neigung max=12°
LogTrendo: Display: TRENDO-WAGEN ausgestiegen, Figur=sichtbar
LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=8 Gegner=0 Natur=125952 Figur=ja Boden=ja Z=92
LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```
