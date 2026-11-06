# Native Cross-Studio-Pipelines 1–4

## A. Blender → Unity/Unreal/Godot → Testspiel
1. `CORE-05`: editierbare Mesh-, Material-, Rig- und Animationsquellen in `.blend` + autorisierte Assets.
2. Standard-Exports nur nach Format-/Lizenz-/Versionprüfung; geänderte Materialien/Rigs kennzeichnen.
3. `CORE-06`: gewählte Engine prüft Import, Pivots, Achsen, Units, UV, Animator, Scene-Referenzen; Unity ist gleichberechtigtes Ziel.
4. Trendo QA: Preview/Editor-Log + ggf. Testlevel und Hash. Ohne gewählte/verbundene Engine nur exportierbares Asset, kein behauptetes Spiel.

## B. Blender/Unreal/Unity → Film/VFX
`CORE-05` baut Set/Charakter; `CORE-08` importiert Bildsequenzen mit Timecode/Framerate/Color Space; virtuelle Kamera via Unreal oder Unity nur wenn vorhanden, danach Compositing, Sync, Export. Render-Workflows besitzen Cache und Wiederaufnahme.

## C. Trendo Musik/Audio → Games + Film
`STUDIO-MUSIK-01` liefert editierbare Mix-/MIDI-Projekte + WAV/Stems, LUFS-Ziele und Marker; `CORE-06` ordnet Audio Events zu (Unity/Unreal/Godot); `CORE-08` synchronisiert Film-Cues per Timecode. Keine proprietären Musikrechte voraussetzen.

## D. Claude-Agenten und Jobs
Ein vorhandener `ProjectStore`-basierter Auftrag, agentenspezifische Worktree-Diffs, saubere Übergabe, Fachadapter pro Engine/Studio, Freigabe für Exporte außerhalb der Projektisolation. Nie zwei Agenten an derselben Datei. Claude-Chat schreibt nicht direkt in Renderer, sondern validiert Capability und ruft den existierenden Job-Kern.
