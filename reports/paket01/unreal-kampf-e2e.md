# Beleg – Gegner als echte Charaktere (Boden, Kollision, Laufen), Kampf mit Sturz und Blut, echter Lauf mit UE 5.8 (07.11.2026)

Anlass (Nutzer): nicht durch Objekte gehen; Gegner fielen teils durch den Boden, liefen unecht und flackerten in der Ferne; Kampf physikalisch, bei brutalen Spielen Blut.

## Was geändert ist
- Gegner sind echte Unreal-Charaktere: Kollisionskapsel, Schwerkraft, Bodenkontakt, Stufen, Hangwinkel, Ausweichen (RVO) über die Charakter-Bewegung, gesteuert von einem KI-Controller; sie gehen nicht mehr durch Bäume, Felsen oder Kisten (bleiben sie hängen, weichen sie seitlich aus).
- Laufen aus der echten Geschwindigkeit (Gehen/Laufen auf dem eigenen Skelett) – Füße passen zum Tempo.
- Kein Ruckeln/Flackern durch gedrosselte Animation in der Ferne: Update-Rate-Optimierung für Menschen aus, Knochen immer aktuell. Vorher traf der Bodenstrahl auch den eigenen Körper (Höhe sprang) – entfällt mit der Kapsel.
- Kampf: Treffer stoßen den Gegner physikalisch zurück (kurz benommen); besiegte Gegner fallen mit echter Sturz-Animation (auf das eigene Skelett übertragen) zu Boden und bleiben liegen.
- Gewalt-Stufe in der Spielbibel (keine | normal | brutal, aus der Zielgruppe gewählt): keine = Gegner verschwinden, kein Blut; normal/brutal = Sturz und Blut am Boden (eigenes Decal-Material, dunkel und matt).

## Messungen (Prüflauf, Gewalt-Stufe brutal)
- Bodenkontakt: Gegner geht auf dem Boden, Abstand Füße–Boden 2,2 cm.
- Gegner-Probe: alarmiert, verfolgt, angegriffen (Leben 3 → 2).
- Kampf-Probe: Boss besiegt, Becken 2 cm über dem Boden (liegt), 3 Blutflecken.

Bild: unreal-kampf.png (Ort des Kampfs: Blut am Boden, der liegende Boss wird teils von der Figur Walter verdeckt)

## Ehrlich offen
- Ragdoll-Physik am MetaHuman-Körper fiel im Prüflauf ohne Kollision durch das Gelände (gemessen bis 18 m) – daher Sturz-Animation; Ursache wird weiter untersucht.
- Abtrennbare Körperteile (nur bei „brutal“) sind noch nicht gebaut – geplant über ausgeschnittene Gliedmaßen aus dem MetaHuman beim Bau.
- Ausweichen ohne Navigationsnetz (seitlich ausweichen, kein Pfad um große Hindernisse).
- Wagen: fährt, beschleunigt im Prüflauf aber nur langsam (1–6 m in 5 s); Raddrehung noch nicht messbar nachgewiesen.
- Flackern in der Ferne: Ursache behoben (siehe oben), aber noch nicht mit einer Bildfolge gemessen.

## SHA-256
- unreal-kampf.png: 56a9369f839a69d9f6323062bd31e0eed3d8943660d88ce8c859ea87b83d0404

## Rohlog-Auszüge
```
LogPython: TRENDO-MENSCH Waldlaeuferin (Ada) gebaut: 3 Körperteile, 3 Haar-Arten, Kleidung WI_Casual_F_Outfit, Bewegungen übertragen
LogPython: TRENDO-MENSCH Gegner (Victor) gebaut: 3 Körperteile, 5 Haar-Arten, Kleidung WI_Casual_M_Outfit, Bewegungen übertragen
LogPython: TRENDO-MENSCH Figur (Walter) gebaut: 3 Körperteile, 5 Haar-Arten, Kleidung WI_Casual_M_Outfit, Bewegungen übertragen
LogPython: TRENDO-MENSCH-OK 3 Waldlaeuferin,Gegner,Figur
Result: Succeeded
LogTemp: Display: TRENDO-FIGUR MetaHuman=ja (BP_Waldlaeuferin_C) Mensch=ja
LogTrendo: Display: TRENDO-BODEN Gegner geht=ja Abstand Füße–Boden=2.2 cm
LogTrendo: Display: TRENDO-GEGNER alarmiert=ja Abstand vorher=1100 nachher=3591 Leben 3→2 verfolgt und angegriffen
LogTrendo: Display: TRENDO-KAMPF Körper: Becken Z=2, Gegner Z=115, Simulation=nein, Physik-Körper=0
LogTrendo: Display: TRENDO-KAMPF Gegner besiegt=ja Ragdoll=ja Becken über Boden=2 cm (liegt) Blut=3 Gewalt=brutal
LogTrendo: Display: TRENDO-WAGEN eingestiegen
LogTrendo: Display: TRENDO-WAGEN gesteuert=ja Klasse=BP_ModularVehicleSimplifiedSkeletalSetup_C Tempo=1 km/h Strecke=1.2 m Raddrehung=0° (Reifen stehen)
LogTrendo: Display: TRENDO-WAGEN ausgestiegen, Figur=sichtbar
LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=8 Gegner=0 Natur=125952 Figur=ja Boden=ja Z=92
LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```
