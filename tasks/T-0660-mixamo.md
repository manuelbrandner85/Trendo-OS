# T-0660: Mixamo im Spiele-Studio (M1–M6)

**Ziel:** Mixamo-Anbindung im Programm „Spiele bauen“, Freigabe des Nutzers am 14.11.2026 („Ja alle“), weitergeleitet von der Sitzung Spiele-Studio.
**Branch:** claude/T-0660-mixamo (von origin/main 92b46dd0a)
**Dateien/Bereich:** `src/Trendo.Core/Programs/Mixamo.cs`, `MixamoGuard.cs`, `MixamoBlender.cs`, `MixamoUnreal.cs` (neu), `GameRelease.cs` und `GameStudio.cs` (Lizenzwächter), `src/Trendo.App/MainWindow.ProgGameMixamo.cs` (neu), `MainWindow.ProgGame.cs`, `MainWindow.W4Game.cs`, Tests, `tests/fixtures/mixamo/testrig.py`, `docs/nachbau/spiele-ki/mixamo.md`.
**Fertig, wenn:**
- [ ] M1: Knopf „Mixamo öffnen“ zeigt mixamo.com im eingebauten Browser mit eigenem Profil; Anmeldung macht die Person, Trendo speichert keine Anmeldedaten
- [ ] M2: neue Mixamo-FBX im Download-Ordner werden von selbst erkannt (Inhalt „mixamorig“) und mit Quelle Mixamo und Lizenz „Mixamo-Bedingungen, nur im fertigen Spiel“ übernommen
- [ ] M3: eigene Figur als FBX für den Auto-Rigger, Ergebnis kommt über M2 zurück
- [ ] M4: Blender gleicht Knochennamen an, setzt Maßstab, auf der Stelle oder Wurzelbewegung, überträgt auf die Spielfigur; weiter nach Unreal (IK Retargeter) und Godot (glTF); Reality Check
- [ ] M5: Zuordnung zu Stehen, Gehen, Laufen, Springen, Fallen, Getroffen mit Suchbegriffen
- [ ] M6: Lizenzwächter bricht Export, Asset-Paket und Weitergabe mit Mixamo-Rohdateien ab; fertiger Build erlaubt
- [ ] gezielte Tests, QA, Volltest grün
**Nicht tun:** inoffizielle Schnittstelle oder automatischer Download; Adobe-Anmeldedaten oder Token speichern; Mixamo-Dateien mitliefern; fremden Code übernehmen.
**Am Ende:** bei Grün nach main mergen und pushen (Dauerfreigabe „Immer nach main“), Bericht `tasks/T-0660-mixamo.bericht.md`. Release nur mit OK.
