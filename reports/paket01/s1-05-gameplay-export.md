# Beleg S1-05 – Gameplay im Unity- und Godot-Export (06.11.2026)

Fähigkeiten (Doppelsprung, Sprint), Regeln/Aufgaben, Inventar und sprechende Figuren, Kampf (Schwert, Werfen, Boss), Fahrzeuge (Wagen, Ufo) in den erzeugten Projekten.

| Lauf | Exitcode | Ergebnis |
|---|---|---|
| Unity 6000.6.8f1 EditMode (u. a. Gameplay_Daten_werden_gelesen) | 0 | result="Passed" total="5" passed="5" failed="0" |
| Unity 6000.6.8f1 PlayMode (u. a. Gegenstand/Figur/Aufgabe, Schwert/Boss, Wagen, Doppelsprung) | 0 | result="Passed" total="6" passed="6" failed="0" |
| Godot 4.7.2 check_level.gd | 0 | TRENDO-OK 2 Level |
| Godot 4.7.2 play_check.gd | 0 | TRENDO-OK Spiel läuft, Figur steht |
| Godot 4.7.2 save_check.gd | 0 | TRENDO-OK Spielstand speichern, fortsetzen und prüfen |
| Godot 4.7.2 gameplay_check.gd | 0 | TRENDO-OK Gameplay: Inventar, Figur, Aufgabe, Schwert, Boss, Fahrzeug, Doppelsprung |
| Godot 4.7.2 Spiel 300 Bilder | 0 | keine Fehler |

Dabei gefunden und behoben: Beim Levelwechsel benannte Godot die neuen Kacheln um (@Area2D@…), weil die alten erst am Bildende verschwinden – Berührungen ab Level 2 hätten nicht gegriffen. Jetzt werden die alten Kacheln sofort entfernt.

## SHA-256
- EditMode-ergebnis.xml: 732a7f899ffd8502f982268529e1baabe857a7c46567d37e0f42b0d12778146a
- PlayMode-ergebnis.xml: dc62c59c971b90a1e101c6df6696d9bc18daec1a65ac69c3f412a10834d3640b
- TrendoLevel.cs: eb53960adf71c9d33e7fcbe5f5ca5dd9840ed7a2c832e95456d2d272d15fbf56
- TrendoPlayer.cs: a5f0e02c8459fc39c9ec1502a6ad1c937b3d00054943697a36e8ac50d2fe7535
- TrendoGameplayTests.cs: e057e829342427aa938c5fe8dcb7df644d741405205328b33dde8a030720352a
- trendo_level.gd: b5b527d66ce4a3b2e1928b0b1a1bf61fcdebb6da5b45cd1811b4ce370a217e3e
- trendo_player.gd: 347ae7f811644b9685b954282e2df9d2d8c4a7fda2e42d906737a43ae2eb8aa7
- trendo_shot.gd: 4a97d73b9e62c5f2450785e6df7d628a8c6775972f6c24550f7106fec662baf2
- gameplay_check.gd: 1937287ab70569b3bc11fb326a6e4ecb2ba2b36f211726f5b2f70eb713df5409

Nicht geprüft: Waffe „Werfen“ und Ufo-Flug in Unity/Godot automatisch (nur Code und Trendo-Seite geprüft), Spielen von Hand.
