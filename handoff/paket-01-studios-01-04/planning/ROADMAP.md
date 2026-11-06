# Roadmap – Paket 01 (kein Full-Rewrite)

| Phase | Liefereinheit | Freigabekriterium | Abnahme |
|---|---|---|---|
| P0 | read-only Audit, Git HEAD, Capability-Delta und REA-Make/Integrate-Entscheide | Änderungsvorschläge als Tasks, keine unautorisierte Mutation | Pfad-/API-/Testnachweise |
| P1 | Bestehende Game-/Audio-/3D-/Video-Workspaces absichern | native Arbeitsflächen speichern/öffnen/Undo | reale Dateien, Tests |
| P2 | Unity zusätzlich zu Unreal/Godot als Engine-Ziel | lokaler Versions-/Lizenz-/Editorcheck; CLI und Tests | EditMode+PlayMode und Build-Log oder BLOCKED |
| P3 | Blender→Unity/Unreal/Godot Asset-Verifikation | Import/Export und Diffbericht | Modell+Material+Animation Test |
| P4 | Video-Timeline + Compositing + Ton-/Musik-Cues | editierbarer Projektstand und reproduzierbarer Export | Zeit/Farb-/Ton-Sync |
| P5 | Agenten-Workflow für game/film/audio mit Jobstatus | echte Prozessorgebnisse; QA ohne Fake-States | Log, Hash, Wiederaufnahme |
| P6 | Spezifische Funktionen aus 78 Punkten in kleinen Reihen | pro Feature nachgewiesene Fähigkeiten | Positiv/Negativtest je Punkt |
| P7 | Paketweiter Regressionstest, Dokumentation, Freigabevorlage | kein Release ohne explizites OK | unabhängiges QA-Review |

Gesamtumfang kann nicht in einem Arbeitsschritt erledigt werden; Aufgaben sollen vertikale, komplett prüfbare Slices sein, nicht eine Sammlung kosmetischer Buttons.
