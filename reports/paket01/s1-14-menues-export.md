# Beleg S1-14 – Spielmenüs im Unity- und Godot-Export (07.11.2026)

Startmenü (Fortsetzen, Spielen, Einstellungen, Steuerung), Pause (Esc/P/Start), Einstellungen gespeichert (Lautstärke, hoher Kontrast, große Schrift, ruhige Effekte, Karte), Karte, Controller. Im Menü stehen Eingaben, Gegner und Regeln still; die Physik läuft weiter.

| Lauf | Exitcode | Ergebnis |
|---|---|---|
| Unity 6000.6.8f1 EditMode | 0 | result="Passed" total="5" passed="5" failed="0" |
| Unity 6000.6.8f1 PlayMode (u. a. Startmenue_Einstellungen_Pause, Ohne_Menue_sofort_spielen_und_Fortsetzen_mit_Spielstand) | 0 | result="Passed" total="8" passed="8" failed="0" |
| Unity Windows-Build | 0 | TRENDO-BUILD Succeeded 95952166 Bytes |
| Godot 4.7.2 check_level.gd | 0 | TRENDO-OK 2 Level |
| Godot 4.7.2 play_check.gd | 0 | TRENDO-OK Spiel läuft, Figur steht |
| Godot 4.7.2 save_check.gd | 0 | TRENDO-OK Spielstand speichern, fortsetzen und prüfen |
| Godot 4.7.2 gameplay_check.gd | 0 | TRENDO-OK Gameplay: Inventar, Figur, Aufgabe, Schwert, Boss, Fahrzeug, Doppelsprung |
| Godot 4.7.2 menu_check.gd | 0 | TRENDO-OK Menü: Start, Einstellungen gespeichert, Pause, Weiter |
| Godot 4.7.2 Spiel 300 Bilder | 0 | keine Fehler |

Dabei gefunden und behoben: Godot brach mit „Typ aus Variant abgeleitet“ ab (Warnung als Fehler) – Menütitel hat jetzt einen festen Typ. Unity schrieb in Hintergrundläufen keine Ergebnisse; geprüft wurde im Vordergrund.

## SHA-256
- EditMode-ergebnis.xml: 5daecec975de2abc02835f581a2aeada73f5b4c3cd1fdded7a0629c4a8aa5697
- PlayMode-ergebnis.xml: c78673d87400f7e8a79550da830a242dfd488e68433b4e00cf73fa838ebc4948
- TrendoMenu.cs: aab638370a928c52706b9fbd8425a476fe4a84417546a1e00e873112d2325859
- TrendoMenuTests.cs: 5c075166940a6cc6094f1fd68a038f7b6fce6ab0e1fc642398d4c0d341122a64
- trendo_level.gd: bb18bba2f115a8625c1ee6620153332df70f1200cc7eef6fd815d2482ae53c43
- menu_check.gd: 32b1f6882c0dd21a1a13e2ae3a219bb4a1bd0d77a0e467c0de7c69f4fbd3798a

Nicht geprüft: Controller (kein Gamepad angeschlossen), Bedienung der Menüs von Hand im Unity-/Godot-Fenster.
