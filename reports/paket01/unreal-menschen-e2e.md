# Beleg – Spielfigur, Gegner und Figur als echte Menschen (MetaHuman) mit Kleidung, echter Lauf mit UE 5.8 (07.11.2026)

Ablauf: Trendo-Skript Trendo/trendo_mensch.py (UnrealGame.MenschScript) über UnrealEditor-Cmd mit -RenderOffscreen, danach Startlauf -game -trendocheck -trendoshot.

1. Drei MetaHumans aus Epic-Vorlagen (MetaHuman Creator-Kerndaten): Waldläuferin (Vorlage Ada), Gegner (Victor), Figur (Walter). Gesichts-Rig und 4K-Hauttexturen aus Epics MetaHuman-Dienst (Anmeldung kommt aus dem Epic Games Launcher).
2. Kleidung: kostenlose Outfits „MetaOutfits – Casual Wear – Female/Male“ (Fab, Wizark Studios, Standard-Lizenz von Fab), aus Trendos Asset-Ordner assets/kleidung ins Projekt; Frau und Männer bekommen das passende Outfit, Epics Grundkleidung wird ersetzt.
3. Je Figur Zusammenbau (Körper, Gesicht, Kleidung, Haare, Augenbrauen, Wimpern) und Bewegungen per IK-Retargeting auf das eigene Skelett (Stehen, Gehen, Laufen, Springen, Fallen).
4. Im Spiel: Spielfigur = Waldläuferin, Gegner und Boss = Victor (Boss 25 % größer), sprechende Figur = Walter; fehlt eine Figur, bleibt dort die Gliederpuppe.
5. Startlauf: TRENDO-OK, Spielfigur: MetaHuman.

Bilder: unreal-menschen-start.png, unreal-menschen-objekte.png (vorn die Waldläuferin, hinten Walter, Victor und der Sportwagen)

## Reality Check (ehrlich)
- Glaubwürdig: drei unterschiedliche echte Menschen (Alter, Haare, Statur), Kleidung statt Unterwäsche, natürlicher Stand, Schatten und Bodenkontakt.
- Noch nicht perfekt: Freizeitkleidung statt Wander-/Kampfkleidung (einzige kostenlose Komplett-Outfits), Übergänge Stehen/Gehen/Springen schalten hart um, Gegner tragen keine Waffe.

## SHA-256
- unreal-menschen-start.png: 83c336c2c866a6a919b2529d4aa4e288e1ce8146d9359c89edf9a7adff1a3ba0
- unreal-menschen-objekte.png: 14557367224ba5e73884c6cf2afde636b4617e9d791428c5569513bda59b03cf

## Rohlog-Auszüge
```
[2026.11.07-14.25.06:004][  2]LogPython: TRENDO-MENSCH Kleidung: 2 Outfit(s) – WI_Casual_F_Outfit, WI_Casual_M_Outfit
[2026.11.07-14.27.17:212][  2]LogPython: TRENDO-MENSCH Hinweis: MetaHumanCharacterEditorSubsystem: /Game/Trendo/Mensch/Gemeinsam/Face/CR_MetaHuman_HeadMovement_IK_Proc : Cannot break link Multiply.Result.Z -> RerouteNode_1.Value in package /Game/Trendo/Mensch/Gemeinsam/Face/CR_MetaHuman_HeadMovement_IK_Proc |   RuntimeError: MetaHumanCharacterEditorSubsystem: /Gam
[2026.11.07-14.27.32:406][  2]LogPython: TRENDO-MENSCH Hinweis (ARKit-Zuordnung) – Schritt wiederholt
[2026.11.07-14.27.35:868][  2]LogPython: TRENDO-MENSCH Waldlaeuferin (Ada) gebaut: 3 Körperteile, 3 Haar-Arten, Kleidung WI_Casual_F_Outfit, Bewegungen übertragen
[2026.11.07-14.30.38:462][  2]LogPython: TRENDO-MENSCH Gegner (Victor) gebaut: 3 Körperteile, 5 Haar-Arten, Kleidung WI_Casual_M_Outfit, Bewegungen übertragen
[2026.11.07-14.33.59:331][  2]LogPython: TRENDO-MENSCH Figur (Walter) gebaut: 3 Körperteile, 5 Haar-Arten, Kleidung WI_Casual_M_Outfit, Bewegungen übertragen
[2026.11.07-14.33.59:332][  2]LogPython: TRENDO-MENSCH-OK 3 Waldlaeuferin,Gegner,Figur
[2026.11.07-14.25.06:004][  2]LogPython: TRENDO-MENSCH Kleidung: 2 Outfit(s) – WI_Casual_F_Outfit, WI_Casual_M_Outfit
[2026.11.07-14.27.17:212][  2]LogPython: TRENDO-MENSCH Hinweis: MetaHumanCharacterEditorSubsystem: /Game/Trendo/Mensch/Gemeinsam/Face/CR_MetaHuman_HeadMovement_IK_Proc : Cannot break link Multiply.Result.Z -> RerouteNode_1.Value in package /Game/Trendo/Mensch/Gemeinsam/Face/CR_MetaHuman_HeadMovement_IK_Proc |   RuntimeError: MetaHumanCharacterEditorSubsystem: /Gam
[2026.11.07-14.27.32:406][  2]LogPython: TRENDO-MENSCH Hinweis (ARKit-Zuordnung) – Schritt wiederholt
[2026.11.07-14.27.35:868][  2]LogPython: TRENDO-MENSCH Waldlaeuferin (Ada) gebaut: 3 Körperteile, 3 Haar-Arten, Kleidung WI_Casual_F_Outfit, Bewegungen übertragen
[2026.11.07-14.30.38:462][  2]LogPython: TRENDO-MENSCH Gegner (Victor) gebaut: 3 Körperteile, 5 Haar-Arten, Kleidung WI_Casual_M_Outfit, Bewegungen übertragen
[2026.11.07-14.33.59:331][  2]LogPython: TRENDO-MENSCH Figur (Walter) gebaut: 3 Körperteile, 5 Haar-Arten, Kleidung WI_Casual_M_Outfit, Bewegungen übertragen
[2026.11.07-14.33.59:332][  2]LogPython: TRENDO-MENSCH-OK 3 Waldlaeuferin,Gegner,Figur
[2026.11.07-14.34.52:833][  0]LogTemp: Display: TRENDO-FIGUR MetaHuman=ja Mensch=ja
[2026.11.07-14.35.12:624][182]LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=7 Gegner=1 Natur=125952 Figur=ja Boden=ja Z=92
[2026.11.07-14.35.12:624][182]LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```

Nicht geprüft: Spielen von Hand, Klick in der Trendo-Oberfläche (dort mit Testersatz geprüft: GameUnreal_Ui), gepacktes Windows-Spiel mit MetaHumans, Bildrate.
