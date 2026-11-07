# Beleg – 3D-Spiel in Unreal aus Trendo, echter Lauf mit UE 5.8 (07.11.2026)

Trendo erzeugt mit GameDesign.BuildUnreal ein C++-Spiel (Figur mit Kamera, Enhanced Input, Gegner/Boss, Münzen, Gegenstände, Figuren, Fahrzeuge, Regeln/Aufgaben, Spielstand, Menü, Anzeige) und kompiliert, testet und startet es über die offiziellen Kommandozeilen.

| Spiel | Kompilieren (Build.bat) | Unreal-Tests (Automation RunTests Trendo) | Startlauf (-game -trendocheck) |
|---|---|---|---|
| Waldlaeufer (AAA+) | Exitcode 0, Result: Succeeded | 3 von 3 bestanden (Daten, Regeln, Spielstand) | Exitcode 0, TRENDO-OK – TRENDO-SPIEL Welt=openworld Level=0 Objekte=7 Gegner=1 Figur=ja Boden=ja Z=92 |
| Burgheld (AA) | Exitcode 0, Result: Succeeded | 3 von 3 bestanden (Daten, Regeln, Spielstand) | Exitcode 0, TRENDO-OK – TRENDO-SPIEL Welt=level Level=0 Objekte=27 Gegner=1 Figur=ja Boden=ja Z=144 |

Bilder aus dem Startlauf: unreal-openworld-start.png (Open World), unreal-level-start.png (3D-Level).

## SHA-256
- unreal-openworld-start.png: d18a80171c2085dc039f5bac4f4378a35e4a434f137848ce23653e50b8e27602
- unreal-level-start.png: 94bf200daad028e1fdeabb3fe564726420d23bb6d89274b3effed3cc56a9e28d
- Source/TrendoGameMode.cpp: 37bb85f81e1f8cafa26245424eb93397f9b9fd716c96017a9e7124ea8be29a94
- Source/TrendoCharacter.cpp: ca27892bdd158fb4ce911d66c1b8c903292e4a723a21367c9b4fb689d9bffd48
- Source/TrendoActors.cpp: c4bba9660811ebbe6a9f30b987826b4733788304f787378b373ada6ed1b7f8b0
- Source/TrendoTypes.cpp: bea13a42cac40926f5a31d56df8ba6bfbdeb50ac2bd0991d4592b9a9aa5f1e92
- Source/TrendoTests.cpp: df494e43fd796d355c6d0dcccbc456e68290af6d3cdfabb8823709eea8f927e9

## Rohlog-Auszüge (Open World)
```
Result: Succeeded
[2026.11.07-00.46.14:414][595]LogAutomationController: Display: Test Completed. Result={Success} Name={Daten} Path={Trendo.Daten}
[2026.11.07-00.46.14:430][597]LogAutomationController: Display: Test Completed. Result={Success} Name={Regeln} Path={Trendo.Regeln}
[2026.11.07-00.46.14:455][600]LogAutomationController: Display: Test Completed. Result={Success} Name={Spielstand} Path={Trendo.Spielstand}
[2026.11.07-00.46.53:189][  0]LogLoad: Took 1.603715 seconds to LoadMap(/Engine/Maps/Templates/OpenWorld)
[2026.11.07-00.47.10:455][  0]LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=7 Gegner=1 Figur=ja Boden=ja Z=92
[2026.11.07-00.47.10:455][  0]LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```

Dabei gefunden und behoben: Projektname mit Umlaut („Waldläufer“) wäre ein ungültiger C++-Modulname – Trendo wandelt jetzt in ASCII um (Waldlaeufer). Spielobjekte lagen in der Open World außerhalb des Blickfelds – sie folgen jetzt der Blickrichtung des Startpunkts.

Ehrlich zum Aussehen (Reality Check): Graubox – Grundformen der Engine, Karogelände der Open-World-Vorlage, helle Belichtung im Level. Nicht fotoreal. Nächste Schritte: Gelände-Material und Vegetation (PCG), Figur aus Blender mit Animation, Belichtung.

Nicht geprüft: Spielen von Hand, Controller, gepacktes Windows-Spiel (BuildCookRun).
