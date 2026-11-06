# Claude Code – Masterauftrag Paket 01 (Studios 1–4)

Du entwickelst ausschließlich das **BESTEHENDE** Trendo OS weiter: `manuelbrandner85/Trendo-OS`, .NET + Avalonia. Dieses ZIP ist ein **Nutzer-Entscheidungspaket**, keine Behauptung einer vorhandenen Funktion. Es ergänzt `CLAUDE.md`, `requirements.yaml`, `docs/spec/`, `docs/spec/decisions.yaml`, `docs/claude/AUSBAU_PLAN_2026-11-05.md` und die tatsächliche lokale Codebasis; Regeln mit stärkeren Schutzanforderungen haben Vorrang. Konflikte als `decision-delta` melden, nicht stillschweigend überschreiben.

## Start – zwingend READ ONLY
1. Aktuellen Git-HEAD, Branch, Dirty-Working-Tree, offene Aufträge und parallele Claude-Sitzungen feststellen; `CLAUDE.md`, `README.md`, `.claude/skills/trendo-audit/SKILL.md`, `StudioCatalog.cs`, die vier Studio-Quellbereiche und ihre Tests lesen.
2. `/trendo-audit`, dann `/trendo-plan` verwenden, tatsächliche Capabilities, installierte Engines, Unity-Lizenz/Zugriff, freie Hardware-Ressourcen und vorhandene Trendo-Services ermitteln. Kein Installer, keine externen Zugriffe ohne Freigabe.
3. Für **jede** Anforderung aus `decisions/APPROVALS.csv`: `APPROVED_SPEC`, vorhandene Codepfade, fehlende Funktionen, Tests, Abhängigkeiten und konkrete Dateien im Capability-Delta dokumentieren. Unterschiede zur Ist-Implementierung werden mit Nachweisen behandelt.
4. REA-Regel aus `03_GLOBALE_REGELN_REA_UNITY.md` respektieren. Native Nachentwicklung vor unnötiger Drittsoftware, keine proprietären Quellcodes/Assets kopieren. Unity als reale Engine-Option samt C# und CLI/Build-/Test-Adapter einplanen; wo nur Austauschformate relevant sind, nur einen geprüften Unity-Zieladapter vorsehen.
5. Nur nach konkreter Taskplanung in eigenen Branch/Worktree implementieren. Vorher lokale Tests/Baseline; bestehende Kernsysteme wiederverwenden. Keine zweite Workflow-Engine, Studio-Registry, QA, KI-Core oder Projektverwaltung.

## Technischer Auftrag
- `CORE-06`: kontrollierte Projektvorlagen für **Unreal, Unity, Godot**, eigener Spiel-Editor/Preview, Gameplay-, Asset- und Export-Workflows. Unreal Python ausschließlich für Editorautomatisierung; Unity C# für Gameplay/Editor-Skripte; Godot GDScript/C# wo passend. **Kein Unreal/Unity/Blender-Status `CONNECTED` ohne wirklichen lokal getesteten Prozess bzw. Adapter.**
- `CORE-05`: hochwertige, native Trendo-Arbeitsfläche + verifizierbare Blender-Pipeline für Meshes, Rigging, UV, Animation, Rendering, Scan und Export. Unity-Importprofil für FBX/glTF/USD nur je tatsächlich nachgewiesener Importunterstützung/Plugin-Konfiguration.
- `CORE-08` Video: non-destruktive Timeline, Effekte, Compositing, Filmsequenzen, Export, mit realen Outputs. Unity-Kamera-/Timeline-/Cinemachine-Integration, wenn Projekt tatsächlich Unity nutzt; sonst **nicht** als Voraussetzung.
- `STUDIO-MUSIK-01` + `CORE-08` Audio: DAW, lokale Musik-KI, Plugins, Mixing, Mastering, Film-/Game-Audio, Unity-/Unreal-/Godot-Eventadapter, sofern Zielengine vorhanden.

## Arbeitsregeln
- Eine bereits vorhandene Funktion erweitern, keine unnötigen Duplikate und keine extra Studios erzeugen.
- In allen vier Studios: native bedienbare Arbeitsfläche statt Formularsimulation; `Einfach`/`Profi`, Drag-drop, Vollbild, Rechtsklick, Autosave, Undo/Redo, echter Preview, nachvollziehbare Capability-Indikatoren.
- 20 vorhandene Claude-Agenten (`.claude/agents`); Master Director koordiniert 1–3 nach Bedarf und niemals in denselben Dateien. Kein zusätzlicher 21. Agent.
- `ProjectStore`, `StudioCatalog/StudioRegistry`, `ApprovalGate`, `Rbac/AuditLog`, bestehendes Job-System, QA und KI-Kern prüfen und nachweislich wiederverwenden; keine erfundenen Interfaces.
- Keine bezahlten APIs als stillschweigende Pflicht. REA-Clean-Room nur zulässige Funktionalität rekonstruieren; Marken/UI/Code/Texte/Assets nicht unrechtmäßig kopieren.
- Keine unautorisierten Installationen, Treiber, Systemänderungen, Exporte nach draußen, Push, Merge, Release, Upload oder veröffentlichte Builds. Vor jeder Aktion mit Außenwirkung explizite Freigabe.

## Priorität
1. Game-Vertical-Slice: Spiel anlegen, editieren, speichern, Vorschau, Export, automatisierter Test.
2. Unity-Adapter: Nachweis einer echten vorhandenen Installation; `Editor`-CLI/`-batchmode`, C#-Build, EditMode/PlayMode-Test, Fail-Closed bei fehlender Lizenz/Editor. Unreal/Godot analog.
3. Getesteter Austausch CORE-05 ⇄ CORE-06: `.blend` → Zielasset → referenzierte Szene; Zielimport für Unity/Unreal/Godot ohne verlustfreies Versprechen.
4. Film/Audio-Kette: Timeline → Film-Cue → Audio-Stems → Videoexport, mit realen Eingabe-/Ausgabedateien.
5. Restliche Features in kleinen getrennten Arbeiten nach `planning/ROADMAP.md`.

## Erfolgskriterium
Implementierung ≠ Test: Eine Funktion gilt erst als TESTED mit Rohlog, Exitcode, überprüfbaren Output-Dateien und SHA-256. Kein ungetesteter „AAA+“-Anspruch und keine generischen Demo-Videos. Fehler und offene Punkte sichtbar lassen. Dokumentiere **was nicht getestet wurde**.

## Ergebnis jeder Sitzung
Audit-HEAD; Task-ID/Scope/Agent; Änderungen und Diffs; Build/Testbefehle; echte Testresultate/Exitcodes; Outputs/Hashes; Sicherheits-/Lizenz-/Performance-Befunde; aktualisierte Projektwissen-Datei; offene Punkte; Nächster Task. Keine Magie und kein stillschweigendes Weiterbauen bei Konflikten.
