# Bestehende 20 Claude-Code-Agenten – keine neue Agenten-Suite

Prüfe `.claude/agents/`, niemals Zahl oder Namen erfinden. Zuordnung:
- `01-architecture-lead`: Systemzuschnitt und Adapterverträge (read-only).
- `02-requirements-decisions`: JA/NEIN-Ledger und Konflikte (read-only).
- `03-security-permissions`: SideEffects, Rechte, Secrets (read-only).
- `04-ux-avalonia`: native UI/Viewport/Timeline.
- `05-core-api`: APIs und Codec-/Engine-/CLI-Verträge.
- `06-data-memory`: Projektstruktur, Sessions, Assets und Migration.
- `07-ai-local-models`: lokale AI-Modelle, Consent, VRAM-Budget.
- `08-agent-workflow`: bestehende Job-/Agenten-Orchestrierung.
- `09-one-workspace-studios`: Workspaces im vorhandenen Studio-Katalog.
- `11-web-business-commerce`: hier nur fachliche Web-Export-Verträge, kein Web-Studio-Ausbau.
- `12-blender-assets`: 3D, Meshes, Rig, Texturen und Export.
- `13-unreal-games`: Engine-Adapter für Unreal, Unity, Godot; evtl. Fachaufgabe intern aufteilen, aber kein zusätzlicher permanenter Agent.
- `14-media-vfx`: Film-, Musik-, Ton- und Audio-Pipelines.
- `18-performance-reliability`: GPU/CPU/RAM und Stabilität (Review).
- `19-qa-verification`: unabhängige QA und Test-Evidenz (Review).
- `20-release-compliance`: finale Lizenz-/Release-Prüfung (Review).

Höchstens 1–3 parallele Implementierungsagenten mit eigenen Dateien/Worktrees. Agenten brauchen keine separate Claude-API; vorhandenen Claude-Code-Zugang benutzen. Für externe Tools standardisierte, testbare Adapter und MCP nur wenn der Server existiert. `.mcp.json` garantiert keine funktionierende Blender-/Unreal-/Unity-Verbindung. README und Sicherheitsrichtlinien im aktuellen Repo gelten.
