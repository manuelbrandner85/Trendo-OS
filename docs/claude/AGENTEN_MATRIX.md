# 20 spezialisierte Claude-Code-Agenten

| ID | Agent | Verantwortung | Übergabe / Grenzen |
|---|---|---|---|
| 01 | `01-architecture-lead` | Trendo Architektur- und Abhängigkeitsanalyse. Nur für Aufgaben im Fachbereich ARCHITEKTUR delegieren. | read-only review |
| 02 | `02-requirements-decisions` | Anforderungen, Freigaben und Entscheidungssperren. Nur für Aufgaben im Fachbereich REQUIREMENTS delegieren. | read-only review |
| 03 | `03-security-permissions` | Sicherheit, Datenschutz und Freigabe-Gates. Nur für Aufgaben im Fachbereich SECURITY delegieren. | read-only review |
| 04 | `04-ux-avalonia` | Premium UI/UX und barrierefreies Avalonia-Frontend. Nur für Aufgaben im Fachbereich UI/UX delegieren. | isolierter Worktree; Diff + Tests |
| 05 | `05-core-api` | Core Backend, API und vertragstreue Services. Nur für Aufgaben im Fachbereich CORE/API delegieren. | isolierter Worktree; Diff + Tests |
| 06 | `06-data-memory` | Persistenz, Projektisolation und Entscheidungsregister. Nur für Aufgaben im Fachbereich DATA delegieren. | isolierter Worktree; Diff + Tests |
| 07 | `07-ai-local-models` | Trendo KI-Core, lokale Modelle und Provider-Adapter. Nur für Aufgaben im Fachbereich AI delegieren. | isolierter Worktree; Diff + Tests |
| 08 | `08-agent-workflow` | Trendo interne Agenten und Workflow-Orchestrierung. Nur für Aufgaben im Fachbereich AGENTS/WORKFLOW delegieren. | isolierter Worktree; Diff + Tests |
| 09 | `09-one-workspace-studios` | ONE Workspace, StudioCatalog und Prompt-to-Action. Nur für Aufgaben im Fachbereich STUDIO delegieren. | isolierter Worktree; Diff + Tests |
| 10 | `10-office-documents` | Office, Dokumente, PDF und Veröffentlichungs-Workflows. Nur für Aufgaben im Fachbereich OFFICE delegieren. | isolierter Worktree; Diff + Tests |
| 11 | `11-web-business-commerce` | Webentwicklung, Business und E-Commerce. Nur für Aufgaben im Fachbereich WEB/BUSINESS delegieren. | isolierter Worktree; Diff + Tests |
| 12 | `12-blender-assets` | Blender, 3D-Modelle und Materialien. Nur für Aufgaben im Fachbereich BLENDER/3D delegieren. | isolierter Worktree; Diff + Tests |
| 13 | `13-unreal-games` | Unreal Engine, Spiele und Open-World-Integration. Nur für Aufgaben im Fachbereich UNREAL/GAMES delegieren. | isolierter Worktree; Diff + Tests |
| 14 | `14-media-vfx` | Film, Audio, Animation, VFX und Broadcast. Nur für Aufgaben im Fachbereich MEDIA delegieren. | isolierter Worktree; Diff + Tests |
| 15 | `15-device-fusion` | PC, Raspberry Pi, Android und Remote-Jobs. Nur für Aufgaben im Fachbereich DEVICE FUSION delegieren. | isolierter Worktree; Diff + Tests |
| 16 | `16-linux-boot` | Linux USB/SSD Edition, Boot und Recovery. Nur für Aufgaben im Fachbereich LINUX delegieren. | isolierter Worktree; Diff + Tests |
| 17 | `17-windows-release` | Windows Installer, Editionen und Release-Artefakte. Nur für Aufgaben im Fachbereich INSTALLER delegieren. | isolierter Worktree; Diff + Tests |
| 18 | `18-performance-reliability` | CPU/GPU/RAM, Performance und Stabilität. Nur für Aufgaben im Fachbereich PERFORMANCE delegieren. | read-only review |
| 19 | `19-qa-verification` | QA, Regression, Testmatrix und Evidenz. Nur für Aufgaben im Fachbereich QA delegieren. | read-only review |
| 20 | `20-release-compliance` | Release-Review, Sicherheit, Doku und Handover. Nur für Aufgaben im Fachbereich RELEASE delegieren. | read-only review |

**Orchestrierung:** Haupt-Chat = Master Director (kein zusätzlicher 21. Subagent); 20 Rollen auf Abruf, max. drei Implementierungsagenten parallel. Reines Nachdenken/Review kann unabhängig erfolgen, aber Implementierungen dürfen sich nicht auf gleichen Dateipfaden überschneiden. Der Owner verantwortet externe Freigaben. Fehlerhafte Agentenarbeit ist nicht durch bloßen Agentenstatus repariert.
