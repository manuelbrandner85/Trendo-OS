# Trendo Zusammenspiel der Studios 4.0

> Entwurf. Es entsteht kein neues System. Grundlage: `docs/ALL_STUDIOS_GITHUB_GODMODE/STUDIO_FUSION_ARCHITECTURE.md`.

## Vorhandene gemeinsame Kernsysteme

| Aufgabe | Klasse |
|---|---|
| Projekte | `ProjectStore`, `ProjectFiles` |
| Rechte und Freigaben | `ApprovalGate`, `Rbac` |
| Protokoll | `AuditLog` |
| KI | `IntelligenceCore`, `ClaudeTools` |
| KI-Teams | `AgentCenter` |
| Abläufe | `WorkflowEngine` (`W2/Workflows.cs`) |
| Grafikkarten-Warteschlange | `GpuQueue` |
| Prüfkette je Studio | `StudioWorkflow`, `RunStore` |
| Einrichtung fremder Programme | `DependencyCatalog` |

## Geforderte Ketten

| Kette | Heute | Offen |
|---|---|---|
| Webdesign → Bild → 3D → Website | Teile einzeln | Übergabe als gemeinsamer Projektbestand |
| 3D → Animation → Spiele → Unreal | Blender- und Unreal-Anbindung vorhanden | durchgehender Ablauf mit Stand je Schritt |
| Architektur → CAD → 3D → Render | IFC und Render einzeln vorhanden | Übergabe zwischen den Programmen |
| Film → Audio → VFX → Export | Drehbuch, Schnitt, Audio vorhanden | KI-Clips, Übergabe an den Schnitt; VFX ist kein Studio |
| Print on Demand → Online verkaufen | getrennt | Übergabe des Verkaufstextes |

## Regel

Jede Übergabe läuft über Dateien im gemeinsamen Projekt und über `ProjectStore`. Jede Kette bekommt einen Test, der wirklich von Anfang bis Ende läuft.
