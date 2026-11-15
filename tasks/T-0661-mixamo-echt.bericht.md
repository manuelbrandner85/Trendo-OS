# T-0661 Bericht: Mixamo mit echter Datei geprüft

**Auftrag:** Die offenen Punkte aus T-0660 mit einer echten Mixamo-Datei prüfen (M2 → M4 → M5/M6), Fehler beheben. Weitergeleitet von der Sitzung Spiele-Studio; der Nutzer hat sich selbst bei Mixamo angemeldet und die Datei geladen.
**Branch:** `claude/T-0661-mixamo-echt`, von `origin/main` ed2c70c9b; vor dem Merge mit `origin/main` c83876263 (0.9.98 der Web-Sitzung) zusammengeführt.
**Echte Datei:** „Walking Left Turn.fbx“ aus dem Download-Ordner der Person (FBX binär, With Skin, 30 Bilder/s, 1,87 MB). Sie liegt nicht im Repo und nicht in Testdaten; Bilder und Protokoll nur lokal.

## Ergebnis je Punkt

- **M2 Übernahme:** erkannt (Mixamo, Mesh und Kurven), Animation, Aktion „Gehen“, Quelle und Lizenz gesetzt, zweite Übernahme abgelehnt. Ohne Änderung in Ordnung.
- **M5 Zuordnung:** „Gehen“ automatisch belegt. In Ordnung.
- **M6 Lizenzwächter:** Asset-Paket, Browser-Paket (Datei als „.bin“) und ZIP-Weitergabe abgebrochen, fertiger Build erlaubt, aufbereitete FBX weiter erkannt. In Ordnung.
- **M4 Blender:** 66/66 Knochen, Maßstab 0,01 → Meter, 1,79 m; auf der Stelle 0 cm Abweichung (Weg 1,68 m entfernt), Wurzelbewegung erhält den Weg; Bilder: Pose und Drehung wie Original, Füße am Boden. In Ordnung.
- **M4 Godot 4.7:** GLB importiert, „gehen“ 1,23 s, 66 Knochen, 1,81 m. In Ordnung.
- **M4 Unreal 5.8:** fünf Fehler, behoben:
  1. FUZZY-Kettenzuordnung (Drehknochen am Schlüsselbein, Wurzel am rechten Fuß) → exakte Namen plus Fuß-Ziele.
  2. `duplicate_and_retarget` (veraltet) lieferte leere Animation, gemeldet als „übertragen“ → `run_batch_retarget`, gezählt wird nur Inhalt.
  3. Beim Ersetzen blieb die alte Animation → alte nach „Consolidate“ gelöscht, neue verschoben und geprüft.
  4. FBX in Metern → Wurzel mit Maßstab 100, Hüfte bei 1,0 → Unreal-FBX in Zentimetern.
  5. Becken der Engine-Figur am Boden (Wurzelbewegungs-Operation) → auf der Stelle aus, mit Wurzelbewegung Knochen gesetzt; Beckenhöhe wird in jedem Lauf geprüft.
  Ergebnis: Engine-Figur läuft im Bild mit (Becken 95 cm, 36 Bilder, 89 Spuren); mit Wurzelbewegung 1,51 m Weg und Drehung.

## Geänderte Dateien

`src/Trendo.Core/Programs/MixamoBlender.cs`, `MixamoUnreal.cs`, `src/Trendo.App/MainWindow.ProgGameMixamo.cs`, `tests/Trendo.Core.Tests/MixamoTests.cs`, `docs/nachbau/spiele-ki/mixamo.md`, `installer/neuerungen/0.9.99.md`, `src/Trendo.App/Trendo.App.csproj`.

## Testergebnis

Volltest auf be9448657 (`dotnet test Trendo.sln -c Release`, `python -m unittest discover -s tests/claude_handoff`):

| Projekt | Fehler | Erfolgreich | Übersprungen | Gesamt | Dauer |
|---|---|---|---|---|---|
| Trendo.Api.Tests | 0 | 16 | 0 | 16 | 2 s |
| Trendo.Core.Tests | 0 | 2555 | 5 | 2560 | 1 m 44 s |
| Trendo.App.Tests | 1 | 1308 | 6 | 1315 | 48 m 28 s |
| tests/claude_handoff | 0 | – | – | OK | – |

Der eine Fehler ist der Leistungstest „Pilot Text Leistung“ (Formatwechsel 1521 ms statt unter 1500 ms) bei gleichzeitig laufenden anderen Sitzungen; einzeln dreimal hintereinander grün. Nicht von T-0661. Im vorigen Volltest wackelte ebenso der Prozesstest „Job-Objekt“ (zählt fremde PING-Prozesse mit), einzeln dreimal grün. SHA-256 des Protokolls (nur lokal): `E4AA961FF1E0B8A3F3683BD472624AF2FD994D744C364F39837B1EF6A382DD0C`.

## Offen

- Eigene Figur aus dem Mixamo-Auto-Rigger nicht mit echten Daten geprüft.
- Drehknochen und Mittelhandknochen der Engine-Figur ohne eigene Quelle.
- Die MetaHuman-Übertragung im Spiele-Bau (UnrealGame.cs) nutzt ebenfalls `duplicate_and_retarget`; vermutlich gleicher Fehler (leere Animation), nicht untersucht.
