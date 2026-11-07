# Beleg – fotoreale Open World in Unreal aus Trendo, echter Lauf mit UE 5.8 (07.11.2026)

Ablauf über den Trendo-Code (UnrealNature.Http/Fetch/CopyTo, UnrealGame.Write, UnrealGame.UseWeltMap), Unreal-Schritte über die offiziellen Kommandozeilen:

1. Freie Fotomaterialien und Modelle von Poly Haven (CC0) über die offizielle API laden: 73 Dateien, jede Größe geprüft, Lizenzdatei geschrieben.
2. Kompilieren (Build.bat) – Result: Succeeded.
3. Welt einrichten (UnrealEditor-Cmd -ExecutePythonScript=Trendo/trendo_welt.py -nullrhi): Wiesen- und Waldboden mit Fernmischung, eigenes Blatt-Material (zweiseitig, Durchleuchtung, Alpha-Karten), eigenes Oberflächenmaterial, Nanite mit Flächenerhalt für Pflanzen, Karte /Game/Trendo/Welt aus der Open-World-Vorlage.
4. Unreal-Tests: 3 von 3 bestanden.
5. Startlauf (-game -trendocheck -trendoshot): TRENDO-OK – TRENDO-SPIEL Welt=openworld Level=0 Objekte=7 Gegner=1 Natur=125952 Figur=ja Boden=ja Z=92

Bild: unreal-natur-start.png

## Reality Check (ehrlich)
- Glaubwürdig: Boden mit Gras, Blättern und Steinen ohne sichtbare Kachelung, belaubte Bäume, Farne und Gräser mit Durchleuchtung, bemooste Felsen, Kontaktschatten, Dunst in der Ferne, Sonne seitlich hinter der Kamera.
- Noch nicht fotoreal: Spielfigur, Münzen und Gegner sind Grundformen (Zylinder, Kugeln) – nächster Schritt echte Modelle und Figur mit Animation. Ferne Berge der Engine-Vorlage ohne Bewuchs.
- Gefunden und behoben: Nanite ließ das Laub verschwinden (jetzt Flächenerhalt), Poly-Haven-Blätter haben eine eigene Alpha-Karte (wird mitgeladen), glTF-Materialien des Engine-Plugins hatten keine Nutzung für Instanzen (eigene Projektmaterialien), Prüfstart nahm echte Eingaben an (jetzt gesperrt), Controller-Sticks mit Totzone.

## SHA-256
- unreal-natur-start.png: 0741bfacdd4c2070dfc22ade79715cb0a29a2499f204a322333a5e2e26fd4e41
- Trendo/trendo_welt.py: 1315d2ec510db924065f6ff55d302ac2832216d392ea20f13a344e984d44b6fc
- Source/Waldlaeufer/TrendoGameMode.cpp: 152944033bee69b27e89197e1f80ad259500ae431b1d46c78e631f8fa1b22c7c

## Rohlog-Auszüge
```
Result: Succeeded
[2026.11.07-03.31.45:547][289]LogPython: TRENDO-WELT Boden-Texturen: 8
[2026.11.07-03.31.45:547][289]LogPython: TRENDO-WELT Bodenmaterial: /Game/Trendo/Natur/M_TrendoBoden.M_TrendoBoden aus leafy_grass, forest_ground_04
[2026.11.07-03.31.45:547][289]LogPython: TRENDO-WELT grass_medium_01: 17 Modell(e), 1 Blatt- und 0 Oberflächen-Material(ien)
[2026.11.07-03.31.45:547][289]LogPython: TRENDO-WELT fern_02: 4 Modell(e), 1 Blatt- und 0 Oberflächen-Material(ien)
[2026.11.07-03.31.45:547][289]LogPython: TRENDO-WELT shrub_02: 4 Modell(e), 1 Blatt- und 0 Oberflächen-Material(ien)
[2026.11.07-03.31.45:547][289]LogPython: TRENDO-WELT shrub_04: 1 Modell(e), 1 Blatt- und 0 Oberflächen-Material(ien)
[2026.11.07-03.31.45:547][289]LogPython: TRENDO-WELT pine_sapling_small: 3 Modell(e), 1 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-03.31.45:547][289]LogPython: TRENDO-WELT tree_small_02: 1 Modell(e), 1 Blatt- und 2 Oberflächen-Material(ien)
[2026.11.07-03.31.45:547][289]LogPython: TRENDO-WELT tree_stump_01: 1 Modell(e), 0 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-03.31.45:547][289]LogPython: TRENDO-WELT dead_tree_trunk: 1 Modell(e), 0 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-03.31.45:547][289]LogPython: TRENDO-WELT rock_moss_set_01: 6 Modell(e), 0 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-03.31.45:547][289]LogPython: TRENDO-WELT boulder_01: 1 Modell(e), 0 Blatt- und 1 Oberflächen-Material(ien)
[2026.11.07-03.31.45:547][289]LogPython: TRENDO-WELT Karte /Game/Trendo/Welt: Bodenmaterial auf 65 Gelände-Teil(e)
[2026.11.07-03.31.45:547][289]LogPython: TRENDO-WELT-OK 39 Pflanzen/Felsen-Modelle
[2026.11.07-03.32.32:312][594]LogAutomationController: Display: Test Completed. Result={Success} Name={Daten} Path={Trendo.Daten}
[2026.11.07-03.32.32:328][596]LogAutomationController: Display: Test Completed. Result={Success} Name={Regeln} Path={Trendo.Regeln}
[2026.11.07-03.32.32:353][599]LogAutomationController: Display: Test Completed. Result={Success} Name={Spielstand} Path={Trendo.Spielstand}
[2026.11.07-03.33.13:173][  0]LogTrendo: Display: TRENDO-NATUR 125952 Pflanzen/Felsen in 39 Arten
[2026.11.07-03.33.13:175][  0]LogLoad: Took 1.053224 seconds to LoadMap(/Game/Trendo/Welt)
[2026.11.07-03.33.26:942][853]LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=7 Gegner=1 Natur=125952 Figur=ja Boden=ja Z=92
[2026.11.07-03.33.26:942][853]LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```

Nicht geprüft: Spielen von Hand, Bildrate auf schwächeren PCs, gepacktes Windows-Spiel (BuildCookRun), Klick in der Trendo-Oberfläche (dort mit Testersatz geprüft: GameUnreal_Ui).
