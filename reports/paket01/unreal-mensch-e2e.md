# Beleg – echter Mensch (MetaHuman) mit Kleidung als Spielfigur, echter Lauf mit UE 5.8 (07.11.2026)

Ablauf: Trendo-Skript Trendo/trendo_mensch.py (aus UnrealGame.MenschScript) über UnrealEditor-Cmd mit -RenderOffscreen, danach Startlauf -game -trendocheck -trendoshot.

1. MetaHuman aus der Epic-Vorlage (MetaHuman Creator-Kerndaten), Gesichts-Rig und 4K-Hauttexturen aus Epics MetaHuman-Dienst (Anmeldung kommt automatisch aus dem Epic Games Launcher).
2. Kleidung: kostenloses Outfit „MetaOutfits – Casual Wear – Female“ (Fab, Wizark Studios, Standard-Lizenz von Fab) als .mhpkg aus Trendo/Kleidung importiert und als einzige Kleidung angezogen.
3. Zusammenbau (Körper, Gesicht, Kleidung, Haare, Augenbrauen, Wimpern) und Bewegungen der Engine-Figur per IK-Retargeting auf das MetaHuman-Skelett (Stehen, Gehen, Laufen, Springen, Fallen).
4. Startlauf: TRENDO-OK, Figur: MetaHuman.

Bilder: unreal-mensch-start.png, unreal-mensch-objekte.png

## Reality Check (ehrlich)
- Glaubwürdig: echte Haut, Haare als Strähnen, Gesicht und Proportionen eines Menschen; Hose, Oberteil und Schuhe statt Unterwäsche; natürlicher Stand und Gang; Säbel in der Faust.
- Noch nicht perfekt: Freizeitkleidung statt Wanderkleidung (das einzige passende kostenlose Komplett-Outfit); Outfit laut Fab für UE 5.6–5.7 gebaut, in 5.8 importiert; Übergänge Stehen/Gehen/Springen schalten hart um; Gegner und Figuren sind noch Gliederpuppen.

## SHA-256
- unreal-mensch-start.png: 1f952f79e7ba7523f02c3bafcf99c5ef433809406627adf6d87673361445597d
- unreal-mensch-objekte.png: 5602f6cc72353a4a796070420eb327a4e66d7cd7c11bffe4e65f2d5d4862ac6d
- Trendo/trendo_mensch.py: ec9f760189bba27f1941669531e6dac487118b05995c41520f8c11a4bba62cb1

## Rohlog-Auszüge
```
[2026.11.07-14.08.01:838][  2]LogPython: TRENDO-MENSCH Rig und 4K-Texturen aus dem MetaHuman-Dienst
[2026.11.07-14.08.25:700][  2]LogPython: TRENDO-MENSCH Kleidungs-Parameter: 
[2026.11.07-14.08.25:700][  2]LogPython: TRENDO-MENSCH Kleidung: WI_Casual_F_Outfit, 0 Farben angepasst
[2026.11.07-14.10.03:732][  2]LogPython: TRENDO-MENSCH Hinweis: MetaHumanCharacterEditorSubsystem: /Game/Trendo/Mensch/Gemeinsam/Face/CR_MetaHuman_HeadMovement_IK_Proc : Cannot break link Multiply.Result.Z -> RerouteNode_1.Value in package /Game/Trendo/Mensch/Gemeinsam/Face/CR_MetaHuman_HeadMovement_IK_Proc |   RuntimeError: MetaHumanCharacterEditorSubsystem: /Gam
[2026.11.07-14.10.18:619][  2]LogPython: TRENDO-MENSCH gebaut: 3 Körperteile, 3 Haar-Arten, 39 Texturen
[2026.11.07-14.10.18:663][  2]LogPython: TRENDO-MENSCH Hinweis (ARKit-Zuordnung) – Schritt wiederholt
[2026.11.07-14.10.22:101][  2]LogPython: TRENDO-MENSCH Bewegungen übertragen: Stehen, Gehen, Laufen, Springen, Fallen
[2026.11.07-14.10.22:101][  2]LogPython: TRENDO-MENSCH-OK 10
[2026.11.07-14.10.45:452][  0]LogTrendo: Display: TRENDO-MODELLE 9 Spielobjekt-Modelle, Mensch=ja, Wagen=ja
[2026.11.07-14.10.46:084][  0]LogTrendo: Display: TRENDO-NATUR 125952 Pflanzen/Felsen in 39 Arten
[2026.11.07-14.10.47:503][  0]LogTemp: Display: TRENDO-FIGUR MetaHuman=ja Mensch=ja
[2026.11.07-14.10.47:510][  0]LogLoad: Took 3.512523 seconds to LoadMap(/Game/Trendo/Welt)
[2026.11.07-14.11.06:963][258]LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=7 Gegner=1 Natur=125952 Figur=ja Boden=ja Z=92
[2026.11.07-14.11.06:963][258]LogTrendo: Display: TRENDO-OBJEKT o Modell=ja brass_vase_02_2k
[2026.11.07-14.11.06:963][258]LogTrendo: Display: TRENDO-OBJEKT o Modell=ja brass_vase_02_2k
[2026.11.07-14.11.06:963][258]LogTrendo: Display: TRENDO-OBJEKT G Modell=ja food_apple_01_2k
[2026.11.07-14.11.06:963][258]LogTrendo: Display: TRENDO-OBJEKT N Modell=ja SKM_Manny_Simple
[2026.11.07-14.11.06:963][258]LogTrendo: Display: TRENDO-OBJEKT V Modell=ja SKM_SportsCar
[2026.11.07-14.11.06:963][258]LogTrendo: Display: TRENDO-OBJEKT Z Modell=ja treasure_chest_2k
[2026.11.07-14.11.06:963][258]LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```

Nicht geprüft: Spielen von Hand, Klick in der Trendo-Oberfläche (dort mit Testersatz geprüft: GameUnreal_Ui), gepacktes Windows-Spiel mit MetaHuman, Bildrate.
