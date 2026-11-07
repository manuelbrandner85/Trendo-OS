# Beleg – Gegner-KI und fahrbarer Wagen (Chaos-Physik) im Unreal-Spiel aus Trendo, echter Lauf mit UE 5.8 (07.11.2026)

Ablauf: Kompilieren (Build.bat), Unreal-Tests, Startlauf -game -trendocheck -trendoshot mit zwei Proben im Prüflauf.

1. Gegner-Probe: Die Spielfigur wird 11 m vor den Gegner in sein Blickfeld gestellt. Nach 3 s: Gegner alarmiert, ist hingelaufen und hat angegriffen (Leben 3 → 2; der Treffer setzt die Figur an den Speicherpunkt zurück).
   Gegner-KI in der Open World: Streife um den Startpunkt, Sehen mit Blickfeld (~145°) und freier Sicht (Sichtstrahl), Hören aus der Nähe, Verfolgen, Schlag aus der Nähe mit Pause, Suchen am letzten Ort, Rückzug des angeschlagenen Bosses, folgt dem Gelände, schaut in Gehrichtung, echte Geh-/Lauf-Bewegung.
2. Wagen-Probe: Einsteigen – das Spiel steuert jetzt den Sportwagen aus Epics Fahrzeug-Beispiel (BP_ModularVehicleSimplifiedSkeletalSetup, Chaos Modular Vehicle: Gas, Bremse, Lenkung, Federung, Reifen). Bild aus der Fahrzeugkamera; danach Aussteigen, Figur wieder sichtbar.
   Steuerung: Einsteigen mit E (Controller B) neben dem Wagen, Fahren mit der Steuerung des Wagens (W/S bzw. Trigger, A/D), Aussteigen mit E/B.
3. Ergebnis: TRENDO-OK, Unreal-Tests 3 von 3.

Bilder: unreal-gegner.png (nach dem Angriff, Leben 2), unreal-wagen.png (Fahrzeugkamera im Wagen)

## Reality Check (ehrlich)
- Glaubwürdig: Wagen mit echter Fahrphysik steht mit Rädern auf dem Gelände, Licht und Schatten stimmen; Gegner reagiert wie ein Mensch (bemerkt, läuft hin, schlägt zu).
- Noch nicht perfekt: Der Gegner hat keine Schlag-Animation (Treffer ohne sichtbaren Hieb), umgeht Hindernisse nicht per Navigationsnetz; Sträucher haben keine Kollision (der Wagen steht in einem Busch); die Figur sitzt unsichtbar im Wagen.

## SHA-256
- unreal-gegner.png: cc140fd4ac2e8357751948cc9c78aa74903779fa000799ce06e9b6d82cf7cada
- unreal-wagen.png: 76d8fd82c7804cd3f3dd9ae372d7aa0c4ad4ae9fe704f1fb35cd9cea6818aae1
- Source/Waldlaeufer/TrendoActors.cpp: 4659d47088530a645285f229fa12183e89877dcc75953543770902da3172c9af
- Source/Waldlaeufer/TrendoGameMode.cpp: 90ea9d0f12da3562ab915e2a2333ae0b9fec31d789a2cb05bac0a615a892f713

## Rohlog-Auszüge
```
Result: Succeeded
[2026.11.07-14.43.27:705][589]LogAutomationController: Display: Test Completed. Result={Success} Name={Daten} Path={Trendo.Daten}
[2026.11.07-14.43.27:729][592]LogAutomationController: Display: Test Completed. Result={Success} Name={Regeln} Path={Trendo.Regeln}
[2026.11.07-14.43.27:754][595]LogAutomationController: Display: Test Completed. Result={Success} Name={Spielstand} Path={Trendo.Spielstand}
[2026.11.07-14.43.40:869][  0]LogTrendo: Display: TRENDO-MODELLE 9 Spielobjekt-Modelle, Mensch=ja, Wagen=ja
[2026.11.07-14.43.43:024][  0]LogTrendo: Display: TRENDO-NATUR 125952 Pflanzen/Felsen in 39 Arten
[2026.11.07-14.43.43:476][  0]LogTemp: Display: TRENDO-FIGUR MetaHuman=ja Mensch=ja
[2026.11.07-14.44.05:882][629]LogTrendo: Display: TRENDO-GEGNER alarmiert=ja Abstand vorher=1100 nachher=3566 Leben 3→2 verfolgt und angegriffen
[2026.11.07-14.44.07:388][722]LogTrendo: Display: TRENDO-WAGEN eingestiegen
[2026.11.07-14.44.09:888][166]LogTrendo: Display: TRENDO-WAGEN gesteuert=ja Klasse=BP_ModularVehicleSimplifiedSkeletalSetup_C
[2026.11.07-14.44.11:410][422]LogTrendo: Display: TRENDO-WAGEN ausgestiegen, Figur=sichtbar
[2026.11.07-14.44.12:899][706]LogTrendo: Display: TRENDO-SPIEL Welt=openworld Level=0 Objekte=8 Gegner=1 Natur=125952 Figur=ja Boden=ja Z=92
[2026.11.07-14.44.12:899][706]LogTrendo: Display: TRENDO-OBJEKT o Modell=ja brass_vase_02_2k
[2026.11.07-14.44.12:899][706]LogTrendo: Display: TRENDO-OBJEKT o Modell=ja brass_vase_02_2k
[2026.11.07-14.44.12:899][706]LogTrendo: Display: TRENDO-OBJEKT G Modell=ja food_apple_01_2k
[2026.11.07-14.44.12:899][706]LogTrendo: Display: TRENDO-OBJEKT N Modell=ja -
[2026.11.07-14.44.12:899][706]LogTrendo: Display: TRENDO-OBJEKT V Modell=ja -
[2026.11.07-14.44.12:899][706]LogTrendo: Display: TRENDO-OBJEKT Z Modell=ja treasure_chest_2k
[2026.11.07-14.44.12:899][706]LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
```

Nicht geprüft: Fahren von Hand (Lenken, Bremsen, Kurven), Kampf von Hand, gepacktes Spiel, Bildrate.
