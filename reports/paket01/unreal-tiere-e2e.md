# Beleg – Tiere in der offenen Welt (Vorschlag 4), echter Lauf mit UE 5.8 (07.11.2026)

## Quelle und Lizenz
- Hyena_A1 von Animated3D auf Fab, kostenlos, Lizenz Creative Commons Attribution 4.0 (CC BY 4.0): https://www.fab.com/listings/48f273ad-defb-436a-9e23-131f1409700e
- Geprüft und verworfen: Animalia-Pakete (135–404 €), Forest Animals (179 €), Rehkitz (kostenlos, aber comicartig). Kaffernbüffel (frei, CC BY) hat keine Geh-Bewegung.
- Geholt über den offiziellen Fab-Download mit der Anmeldung des Nutzers, direkt nach TrendoWerkzeuge/assets/tiere (nicht in den Download-Ordner), mit LIZENZ.txt.
- Namensnennung: Der Welt-Bau schreibt Content/TrendoDaten/lizenzen.txt – sie wird mit dem fertigen Spiel ausgeliefert.

## Was gebaut ist
- Welt-Bau importiert jedes Tier aus Trendo/Tiere: Skelett-Netz, 27 Bewegungen, Fotomaterial aus 2048-px-Texturen (Farbe × Umgebungsverdeckung, Normalen, Fellkarten über Deckkraft-Maske).
- Die FBX hält nach jeder Bewegung die Schlusspose bis 16,5 s: die echte Länge wird gemessen (z. B. Gehen 1,4 s, Laufen 0,6 s, Sterben 1,7 s) und nur diese gespielt.
- Im Spiel (ATrendoAnimal): Gruppe von 3 Tieren abseits des Spielwegs, streift im Wegnetz umher, ruht und schnüffelt, flieht bei unter 9 m vor der Spielfigur (rennt), Tempo und Bewegung passen zusammen; im Schwertkampf treffbar (Sterbe-Bewegung, Blut je Gewaltstufe).
- Keine identischen Kopien: Fell-Varianten A2–A4 (gleiches Modell, ebenfalls kostenlos, CC BY 4.0) – der Welt-Bau baut je Variante ein Material, im Spiel bekommt jedes Tier ein anderes Fell.
- Testspieler-Bot: neuer Prüfpunkt „Tiere“ und Bild „Tiere“.

## Messung
- 3 Tiere gesetzt, 3 verschiedene Felle; das nächste ist vor der Probe 19 m umhergegangen; Figur auf 5 m → Tier flieht auf 17,8 m mit 22 km/h (rennt).
- Fehler auf dem Weg dahin, behoben: Interchange legte für die FBX-Texturverweise 32×32-Platzhalter an und die Netz-Materialzuweisung blieb nicht erhalten (Tiere grau) – jetzt klassischer Textur-Import mit eigenen Namen, Material setzt das Spiel beim Erscheinen. Letzte Ursache für Grau: dem neu erzeugten Material fehlte das Nutzungs-Flag für Skelett-Netze – im Editor setzt Unreal es selbst, im Spiel fiel es auf das Standardmaterial zurück.

Bild: unreal-tiere.png (drei Hyänen mit verschiedenem Fell in der Savanne, Licht über die Schulter; Aufnahme aus 10 m, vor der Flucht)

## Ehrlich offen
- Eine Tierart (Hyäne) in vier Fellen. Weitere freie, realistische und animierte Tiere sind auf Fab selten; der Weg ist allgemein (jeder Unterordner in assets/tiere wird zum Tier).
- Das Fell wirkt aus der Nähe glatter als im Original (keine Fell-Simulation); Tiere greifen nicht an (Fluchttier).

## SHA-256
- unreal-tiere.png: 35c5a8e04fc1c7f3fb3da103e277930fcac6919cd8f52adb27720a28f6939529

## Rohlog-Auszüge
```
Result: Succeeded
LogPython: TRENDO-WELT Blut-Decal: /Game/Trendo/Natur/M_TrendoBlut.M_TrendoBlut
LogPython: TRENDO-WELT-OK 39 Pflanzen/Felsen-Modelle, 9 Spielobjekt-Modelle
Result: Succeeded
LogTrendo: Display: TRENDO-TIER 3 Tiere gesetzt, 3 verschiedene Felle
LogTemp: Display: TRENDO-FIGUR MetaHuman=ja (BP_Waldlaeuferin_C) Mensch=ja
LogTrendo: Display: TRENDO-WEG Wegnetz=ja Pfade=24 davon mit Umweg=0
LogTrendo: Display: TRENDO-WEG Hindernis tree_small_02_2k: gerade versperrt, Pfad 24.9 m statt 24.5 m mit 4 Punkten, führt drumherum
LogTrendo: Display: TRENDO-BODEN Gegner geht=ja Abstand Füße–Boden=2.2 cm
LogTrendo: Display: TRENDO-GEGNER alarmiert=ja Abstand vorher=1100 nachher=4074 Leben 3→2 verfolgt und angegriffen
LogTrendo: Display: TRENDO-FLACKERN Gegner in der Ferne bis 52 m: 358 Bilder, ohne Posen-Update 5 (1%), Sprünge über 12 cm 0 (dazu 3 beim Erscheinen), größter Sprung je Bild 27.1 cm (ruhig)
LogTrendo: Display: TRENDO-TIER vorher: 3 Tiere, Hyaene geht, walk, gelaufen 19 m
LogTrendo: Display: TRENDO-TIER Hyaene flieht=ja Abstand 5.0 → 17.8 m Bewegung=run Tempo 22 km/h Fluchten=1
LogTemp: Display: TRENDO-GLIED abgetrennt=calf_r Kleidung=ja
LogTrendo: Display: TRENDO-KAMPF Körper: Becken Z=20, Gegner Z=115, Simulation=ja, Physik-Körper=22
LogTrendo: Display: TRENDO-KAMPF Gegner besiegt=ja Ragdoll=ja Becken über Boden=20 cm (liegt) Blut=9 abgetrennt=1 Gewalt=brutal
LogTrendo: Display: TRENDO-GLIED liegt -16770 cm über dem Boden, 0.8 m vom Körper
LogTrendo: Display: TRENDO-BLUT Zeitraffer +5 min: 9 Flecken gealtert
LogTrendo: Display: TRENDO-WAGEN freie Bahn 49 m, Lenkziel -45° zur Fahrtrichtung
LogTrendo: Display: TRENDO-WAGEN eingestiegen
LogTrendo: Display: TRENDO-WAGEN gesteuert=ja Klasse=TrendoCar Tempo=25 km/h Strecke=42.4 m Raddrehung=232° (Reifen drehen sich) Gang=2 Neigung max=11°
LogTrendo: Display: TRENDO-WAGEN ausgestiegen, Figur=sichtbar
LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=8 Gegner=0 Natur=125952 Figur=ja Boden=ja Z=92
LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
LogPython: TRENDO-WELT Tier Hyaene: Texturen ao=T_Hyaene_ao 2048px, color=T_Hyaene_color 2048px, normal=T_Hyaene_normal 2048px, opacity=T_Hyaene_opacity 2048px
LogPython: TRENDO-WELT Tier Hyaene_A2: Texturen ao=T_Hyaene_A2_ao 2048px, color=T_Hyaene_A2_color 2048px, normal=T_Hyaene_A2_normal 2048px, opacity=T_Hyaene_A2_opacity 2048px
LogPython: TRENDO-WELT Tier Hyaene_A3: Texturen ao=T_Hyaene_A3_ao 1024px, color=T_Hyaene_A3_color 2048px, normal=T_Hyaene_A3_normal 1024px, opacity=T_Hyaene_A3_opacity 2048px
LogPython: TRENDO-WELT Tier Hyaene_A4: Texturen ao=T_Hyaene_A4_ao 2048px, color=T_Hyaene_A4_color 2048px, normal=T_Hyaene_A4_normal 2048px, opacity=T_Hyaene_A4_opacity 2048px
LogPython: TRENDO-WELT Tier Hyaene: 12 Bewegungen (atk 0.7 s, bark 2.8 s, die 1.7 s, idle 1.9 s, idle2 1.8 s, roar 2.2 s, run 0.6 s, sit 3.7 s, sleep 0.4 s, smell 2.7 s, up 0.9 s, walk 1.4 s)
```
