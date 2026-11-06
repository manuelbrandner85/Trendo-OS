# Beleg S1-01 – Trendo-Spiel als Godot-Projekt, echter Lauf (06.11.2026)

- Godot: 4.7.2.stable.official.ed1daf0bf (offizielles Paket von GitHub, SHA-512 geprüft, Signatur gültig)
- Projekt: aus EngineTargets.CreateGodotProject-Inhalten (2 Level: Wiese, Burg)

| Schritt | Befehl | Exitcode | Meldung |
|---|---|---|---|
| Import | --headless --path . --import | 0 | – |
| Level-Prüfung | --script res://tests/check_level.gd | 0 | TRENDO-OK 2 Level |
| Spiel 300 Bilder | --quit-after 300 | 0 | keine Fehler/Warnungen |
| Spiel-Prüfung | --script res://tests/play_check.gd | 0 | TRENDO-OK Spiel läuft, Figur steht |
| Gegenprobe ohne Boden | --script res://tests/play_check.gd | 1 | TRENDO-FEHLER Figur steht nicht auf dem Boden |

SHA-256 der Skripte:
- trendo_level.gd: da285822b879c324d9aad255a8ed7bee3d32467d98fd2db12d7125cb67d6c9d8
- trendo_player.gd: f55e5000242723f48b5d85194177f609a2779f7b2adf91af8f0dcdbcb8dfb07e
- tests/check_level.gd: 1b902363d7b4e26ceea4b276d88f70927710ff6df833010836d591f4cd6fac55
- tests/play_check.gd: 948f2b363e2fbf97cda8a9dd6ba902e40b277dc16c90419ac944ac245682a93b

Nicht geprüft: Steuerung von Hand, Fenster-Darstellung.
