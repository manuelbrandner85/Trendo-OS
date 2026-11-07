# Beleg – echte Modelle statt Grundformen im Unreal-Spiel aus Trendo, echter Lauf mit UE 5.8 (07.11.2026)

Ablauf über den Trendo-Code (UnrealNature.Fetch/CopyTo mit Spielobjekt-Modellen, UnrealGame.Write mit den Engine-Beispiel-Plugins), Unreal-Schritte über die offiziellen Kommandozeilen:

1. Poly-Haven-Abruf (CC0, offizielle API): Plan mit 121 Dateien; alle schon im Zwischenspeicher mit passender Größe (0 neu geladen), Lizenzdatei geschrieben.
2. Kompilieren (Build.bat) – Result: Succeeded.
3. Welt einrichten: zusätzlich 9 Spielobjekte als Fotomodelle in echter Größe, mehrteilige Modelle (Truhe mit Deckel, Griffen, Schloss) über die Unreal-glTF-Einstellungen zu einem Modell zusammengefasst, eigene Materialien, Nanite, Hindernis mit Kollisionsbox.
4. Unreal-Tests: 3 von 3 bestanden.
5. Startlauf (-game -trendocheck -trendoshot): TRENDO-OK; zwei Bilder: Start und Nahaufnahme vor den ersten Spielobjekten.

Bilder: unreal-modelle-start.png, unreal-modelle-objekte.png

## Was jetzt echt ist
- Spielfigur, Gegner und Figur N: Epic-Mannequin (Engine-Plugin MoverExamples, Teil jeder Unreal-Installation) mit Skelett; Gehen und Laufen fließend nach Tempo (Blendspace 0–500 cm/s), schneller läuft die Lauf-Animation schneller (keine rutschenden Füße), Sprung, Fallen, Atmen im Stand. Gegner wenden mit Abbremsen und schauen in Gehrichtung; Boss 25 % größer. Matte Farben statt Chrom (Figur oliv, Gegner rotbraun, Boss fast schwarz).
- Waffe: Säbel (Poly Haven) in der rechten Faust, Klinge schräg nach vorn unten, folgt der Hand; Wurfwaffe: Beil, das sich im Flug überschlägt.
- Fahrzeug: Sportwagen (Engine-Plugin ChaosModularVehicleExamples); im Wagen sitzt die Figur unsichtbar am Steuer, der Wagen dreht träger als ein Mensch.
- Fundstück o: Messingvase 21×53 cm (eine echte Taschenuhr mit 6 cm war im Gras unsichtbar), Gegenstand „Apfel“: Apfel 10 cm, Ziel: Schatztruhe 96 cm, Schlüssel: Messing-Öllampe, Speicherpunkt: Holzlaterne, Hindernis: Holzkiste, Feder: alter Reifen.
- Fehlt ein Modell (anderes Spiel ohne Welt-Einrichtung), nimmt der Code die bisherige Grundform – das Spiel bleibt spielbar.

## Reality Check (ehrlich)
- Glaubwürdig: Säbel, Vase, Apfel, Truhe, Wagen in echter Größe mit Fotomaterialien, Schatten und Bodenkontakt; Figuren mit echter Bewegung statt gleitender Formen.
- Noch nicht fotoreal: Die Figur ist die Epic-Gliederpuppe, kein echter Mensch. Ein echter Mensch braucht MetaHuman (kostenlos, aber mit Epic-Anmeldung durch den Nutzer). Übergänge zwischen Stehen, Gehen und Springen schalten hart um (kein Überblenden, kein Motion Matching, keine Fuß-IK). Säbel ohne Schlag-Animation. Wagen ohne Chaos-Fahrphysik (fährt wie die Figur). Tiere fehlen noch.

## SHA-256
- unreal-modelle-start.png: c9c2e07ee967798510ae671d28dee1741ca22f72c4ea2a1b1895b5e88e0c1235
- unreal-modelle-objekte.png: e642f2fb64f08ce47b506fbc615d4951f8d7c04521ec503d0c9a5ca48830d32e
- Trendo/trendo_welt.py: cdf91d5786078f75b10dec65579d11181a444b6bf2e813c153010f8c0413ccb7
- Source/Waldlaeufer/TrendoActors.cpp: 982b87e8505abec8d6218be030c34cec86ff86159378601acb590b53bff80e83
- Source/Waldlaeufer/TrendoCharacter.cpp: 6a0f954529f37d98ad9ce14cfe9e4b5cbfebe2c031077700d5a9a0eb07364fc2
- Source/Waldlaeufer/TrendoGameMode.cpp: 3c9bebff6238db91464a0aeb8db5a5e9bb82c3a892287c42c8f140d58d95e614

## Rohlog-Auszüge
```
Result: Succeeded
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT Boden-Texturen: 8
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT Bodenmaterial: /Game/Trendo/Natur/M_TrendoBoden.M_TrendoBoden aus leafy_grass, forest_ground_04
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT grass_medium_01: 17 Modell(e), 1 Blatt- und 0 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT fern_02: 4 Modell(e), 1 Blatt- und 0 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT shrub_02: 4 Modell(e), 1 Blatt- und 0 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT shrub_04: 1 Modell(e), 1 Blatt- und 0 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT pine_sapling_small: 3 Modell(e), 1 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT tree_small_02: 1 Modell(e), 1 Blatt- und 2 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT tree_stump_01: 1 Modell(e), 0 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT dead_tree_trunk: 1 Modell(e), 0 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT rock_moss_set_01: 6 Modell(e), 0 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT boulder_01: 1 Modell(e), 0 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT brass_vase_02: 1 Modell(e), 0 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT food_apple_01: 1 Modell(e), 0 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT treasure_chest: 1 Modell(e), 0 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT brass_diya_lantern: 1 Modell(e), 0 Blatt- und 3 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT wooden_lantern_01: 1 Modell(e), 0 Blatt- und 2 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT wooden_crate_02: 1 Modell(e), 0 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT old_tyre: 1 Modell(e), 0 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT wooden_handle_saber: 1 Modell(e), 0 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT hatchet: 1 Modell(e), 0 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT Karte /Game/Trendo/Welt: Bodenmaterial auf 65 Gelände-Teil(e)
[2026.11.07-11.27.17:607][  2]LogPython: TRENDO-WELT-OK 39 Pflanzen/Felsen-Modelle, 9 Spielobjekt-Modelle
[2026.11.07-11.27.40:220][590]LogAutomationController: Display: Test Completed. Result={Success} Name={Daten} Path={Trendo.Daten}
[2026.11.07-11.27.40:236][592]LogAutomationController: Display: Test Completed. Result={Success} Name={Regeln} Path={Trendo.Regeln}
[2026.11.07-11.27.40:261][595]LogAutomationController: Display: Test Completed. Result={Success} Name={Spielstand} Path={Trendo.Spielstand}
[2026.11.07-11.29.26:191][  0]LogTrendo: Display: TRENDO-MODELLE 9 Spielobjekt-Modelle, Mensch=ja, Wagen=ja
[2026.11.07-11.29.26:587][  0]LogTrendo: Display: TRENDO-NATUR 125952 Pflanzen/Felsen in 39 Arten
[2026.11.07-11.29.26:601][  0]LogLoad: Took 1.789173 seconds to LoadMap(/Game/Trendo/Welt)
[2026.11.07-11.29.46:279][  9]LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=7 Gegner=1 Natur=125952 Figur=ja Boden=ja Z=92
[2026.11.07-11.29.46:279][  9]LogTrendo: Display: TRENDO-OBJEKT o Modell=ja brass_vase_02_2k
[2026.11.07-11.29.46:279][  9]LogTrendo: Display: TRENDO-OBJEKT o Modell=ja brass_vase_02_2k
[2026.11.07-11.29.46:279][  9]LogTrendo: Display: TRENDO-OBJEKT G Modell=ja food_apple_01_2k
[2026.11.07-11.29.46:279][  9]LogTrendo: Display: TRENDO-OBJEKT N Modell=ja SKM_Manny_Simple
[2026.11.07-11.29.46:279][  9]LogTrendo: Display: TRENDO-OBJEKT V Modell=ja SKM_SportsCar
[2026.11.07-11.29.46:279][  9]LogTrendo: Display: TRENDO-OBJEKT Z Modell=ja treasure_chest_2k
[2026.11.07-11.29.46:279][  9]LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```

Nicht geprüft: Spielen von Hand (Laufen, Kampf, Einsteigen), Bildrate auf schwächeren PCs, gepacktes Windows-Spiel (BuildCookRun) mit den Engine-Beispiel-Plugins, Klick in der Trendo-Oberfläche (dort mit Testersatz geprüft: GameUnreal_Ui).
