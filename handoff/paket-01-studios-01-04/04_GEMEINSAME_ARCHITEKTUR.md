# Architektur: Ein Trendo, vier Facharbeitsplätze, gemeinsame Produktionsdienste

## Kein zweiter Kern
Avalonia UI (`Trendo.App`) → Studio-Workspaces → fachliche `Trendo.Core`-Modelle → geprüfte Adapter/CLI/SDKs → `ProjectStore`/Job-System → `ApprovalGate` + RBAC/Audit → QA → Artefakt/Preview. Bestehende Implementierungen im tatsächlichen HEAD lokalisieren und erweitern; diese Dokumentnamen sind eine gewünschte Schnittstellenform, keine behaupteten existierenden C#-Interfaces.

## Drei separate Artefakttypen
- `ProjectArtifact`: user-editable source (`.blend`, `.unity`, `.uproject`, `.godot`, native Trendo-Dokumente, Medien-Timelines), Referenz auf Version und Hash.
- `PreviewArtifact`: bildschirmtaugliches Render-/Preview-Ergebnis mit Settings.
- `ReleaseArtifact`: exportierbares, versionsgebundenes Paket. Erst nach ApprovalGate an öffentliche Ziele ausliefern.

## Gemeinsame schematische Metadaten
`studio_id`, `project_id`, `task_id`, `engine_id` (optional), `engine_version`, `input_paths`, `output_paths`, `settings`, `capabilities`, `permissions`, `approval_id`, `logs`, `sha256`, `status`, `license_provenance`, `time_utc`.

## Native Oberfläche
Leinwand/Timeline/3D-Viewport, Objektliste/Tracks, Eigenschaften rechts, Dateibrowser, Ressourcen-Bibliothek, Tastaturkürzel, andockbare Ansichten, Drag/Drop, Undo/Redo, Autosave, Recovery; UI in Deutsch, verständlich für `Einfach`, tiefe Funktionen für `Profi`. Alle Aktionen müssen reale Daten verändern, nicht nur Statuslabels.

## Austauschformate und kein Verlustversprechen
- 3D: `.blend` (Quelle), GLB/glTF, FBX (via geprüfte Pipeline), USD, Texturen und Animationen je Importfunktion; Material-/Rig-/Blendshape-Differenzen dokumentieren.
- Film: Projekt-/Timeline-Quelle; ggf. OTIO, EDL, FCPXML, WAV und Bildsequenzen nach tatsächlichem Codecsupport.
- Musik: native Session, WAV/FLAC, MIDI, Stems, Metadaten, ggf. MP3/OGG wenn Codec verfügbar.
- Games: je Engine native Projekte und Build-Artefakte. Nicht konvertieren, wenn Informationen nicht übertragbar sind.

## Hardware- und Feature-Flags
Jede Integration fragt `installed`, `licensed`, `versionCompatible`, `permissionGranted`, `healthy`, `tested` ab. Bei fehlenden Voraussetzungen klare Fehler ohne Systemänderung. GPU-intensive lokale KI/Render-Tasks müssen Speicherbudget, Temperatur und freien VRAM messen. Pi 4 koordiniert, Windows-PC rendert; fallback auf CPU nur wenn sinnvoll.

## Sicherheit
Nutzerprojekt-Isolation, Pfadvalidierung, Prozesse nur mit Allowlist, Timeout und Ressourcengrenzen, keine willkürlichen Systembefehle über Prompts, restriktive Datei-/Netzwerkrechte, Jobs abbrechbar, Windows-Installationen extern nur autorisiert.
