# Beleg S1-15 – Spielstände (06.11.2026)

Ein Format (Format 1, versioniert) für Browser-Spiel, Probespiel, Unity und Godot: Level, Punkte, Leben, Speicherpunkt, Münzen, besiegte Gegner, Spielzeit, Entscheidungen.

| Lauf | Exitcode | Ergebnis |
|---|---|---|
| Trendo GameSavesTests (inkl. Browser-Spiel echt mit Node v24.21.0) | 0 | 6 von 6 bestanden |
| Trendo GameSaves_Ui (Probespiel speichern/fortsetzen, kaputte Datei) | 0 | 1 von 1 bestanden |
| Unity 6000.6.8f1 EditMode (u. a. Spielstand_speichern_und_laden) | 0 | result="Passed" total="4" passed="4" failed="0" |
| Unity 6000.6.8f1 PlayMode (u. a. Spielstand_setzt_im_richtigen_Level_fort) | 0 | result="Passed" total="2" passed="2" failed="0" |
| Godot 4.7.2 save_check.gd | 0 | TRENDO-OK Spielstand speichern, fortsetzen und prüfen |
| Godot 4.7.2 check_level.gd / play_check.gd | 0 / 0 | TRENDO-OK 2 Level / TRENDO-OK Spiel läuft, Figur steht |

Gegenproben (in den Tests): neuere Fassung, kaputte Datei, fehlendes Level werden abgelehnt; unversionierter Stand wird gehoben; Spielende löscht den Stand.

## SHA-256
- EditMode-ergebnis.xml: 919933d5daf73090931f7d5c591caa17d67f41d31c408672d790e66b7aeb18f0
- PlayMode-ergebnis.xml: f873abd52235420a42a9f7c5e47cb5e0acf17a6b0ad05c053aa4eafc3aa4977f
- trendo_level.gd: 055b3d4ac795f75b7cf03376b063f843c94b445c4593b153c0ab65cf6c4199d6
- save_check.gd: 41a7613e0e1fb64b84e13ceadac181c4ce1553893d8aa203d6c571ce5e220903
- TrendoLevelData.cs: 0ba4540692efab55e111496d79fe4e2114b75d8e302b21a73ebf40ac6f7bdbc7
- TrendoLevel.cs: 0fff4c59ef32208437e92b67a6a18a23b5a0a297a78f343d685914cf11239166

Nicht geprüft: Speichern im echten Browser von Hand, Spielstand im gebauten Unity-Spiel von Hand.
