# Trendo OS – Grok Bot / Grok Build Orchestrierung und Blender/Unreal-Bridges

## A. Klare Aufgaben- und Vertrauensgrenzen

```text
USER (Freigabe und Hardwarezugang)
  |-- Grok Bot: Planungs-/Projektleiter (Cloud; geteilter Computer pro Konto)
  |      |-- NUR genehmigte/entschärfte Anforderungsdaten, Tasklisten, anonymisierte Statusberichte
  |      +-- Übergabeweg A: explizit genehmigte lokale Befehle (nur bei tatsächlich unterstütztem Zugriff)
  |      +-- Übergabeweg B: TASK-*.md in Nutzerübergabeordner, Nutzer startet Auftrag selbst
  |
  +-- Windows-PC: Grok Build (lokaler Code-Agent, autorisierter Projektordner)
          |-- Git branch + Source + Tests + QA Evidence
          |-- Trendo Local Approval Broker (NOCH ZU IMPLEMENTIEREN)
          |-- Blender adapter: Blender Python / geprüfter MCP-Prozess
          |-- Unreal adapter: UE5 Editor Python/Scripting / geprüfter MCP-Prozess
          +-- Native Windows / Linux / Pi OS-Adapter
```

**Heute keine voraussetzbare vollautomatische direkte Integration Grok Bot ↔ Grok Build.** Grok Bot kann nach Freigabe lokale Befehle auf geeigneten Systemen anfordern. Ein dauerhafter sicherer Auftrags-/Ergebnis-Broker muss erst implementiert, mit Auth/Scope/Audit getestet und ausdrücklich freigegeben werden. Anfangs die manuelle Datei-Übergabe verwenden.

## B. Mindestformat Grok Bot → Grok Build
Je Auftrag: `task_id`, `project_id`, `requirement_ids`, `approved_scope_paths`, `objective`, `constraints`, `required_tool_versions`, `allow_network`, `allow_mcp`, `human_approval_required`, `expected_outputs`, `acceptance_tests`, `resource_budget`, `expiry`, `rollback_plan`. Eingangsvalidierung, Schema-Versionierung, Replay- und Duplicate-Schutz.

## C. MCP-Vorprüfung (gilt für Blender und Unreal separat)
1. Toolinstallation, Herkunft/Lizenz und kompatible Plattform nachweisen.
2. MCP-Server-Quellcode/Server-Binary auf Eignung und Berechtigung prüfen; nur ausdrücklich autorisierte Provider.
3. Start des lokalen Servers im eingeschränkten Benutzerkontext; keine feste öffentliche Bind-Adresse, kein vordefiniertes Admin-Token.
4. Grok Build Konfiguration via `.grok/config.toml` oder Nutzerkonfiguration erst nach konkreter Pfad-/Programmprüfung aktivieren. `grok inspect` und `grok mcp doctor` soweit unterstützt ausführen.
5. Tool-Liste prüfen (`tools/list`) sowie Test einer risikolosen Lesefunktion; protokollieren.
6. Authorisierter Minimalauftrag in eigener Testprojektdatei; Fehlerbehandlung prüfen.
7. Erst nach bestandener Rechte- und Integrationstestmatrix andere Aktionen erlauben.

**Konfigurationsvorlage** in `.grok/config.toml` bleibt OHNE aktive frei erfundene Servereinträge. Die Existenz von `mcp_servers.blender` ist NICHT belegt.

## D. Blender-Adapter-Vertrag (Mindesttests)
`check_version`, `open_project_readonly`, `inspect_scene`, `create_mesh_from_approved_spec`, `save_blend`, `render_preview`, `validate_mesh`, `export_asset`. Grok Build darf diese gewünschten Funktionen nicht als MCP Toolnamen behaupten, bevor ein realer Adapter sie bereitstellt. Blender lässt sich alternativ über genehmigte Python-Skripte und Blender Batch/GUI betreiben.
- Tests: Mesh-Topologie/Normals, Maßstab/Ausrichtung, UV/UDIM sofern erforderlich, Materialien/Texturen, Rig/Animation sofern vorhanden; tatsächliches .blend reopen, Bildrender, Exportformat und Unity-/Unreal-Kompatibilität kontrollieren.
- Vor Änderungen bestehendes Projekt sicher versionieren; Produktions-Mesh nicht aufgrund eines einzelnen Referenzbildes als „perfekt“ deklarieren.

## E. Unreal Engine 5-Adapter-Vertrag (Mindesttests)
`check_ue_version`, `open_uproject`, `inspect_plugins`, `import_asset`, `validate_assets`, `run_editor_tests`, `save_map`, `build_project` sind BEISPIEL-Fähigkeitsnamen, keine zugesicherten MCP-Tools. Reale Implementierung über **freigegebene** UE-Python-/Editor-Scripting-/Automationsschnittstellen oder geprüfte MCP-Bridge.
- UE5-Editor-Lizenz/EULA, Engine-Version, GPU/VRAM, Projektplugins, Dateipfade vorab prüfen.
- Asset-Handoff: Einheiten, Achsensystem, Pivot, Materialien, Skelette, LOD, Animationen, Naming und Texturreferenzen; Importbeweise via Editor/Logs; PIE/Smoke Tests tatsächlich ausführen.
- Keine ungetesteten automatischen Änderungen am fremden Game-Repo. Eigene Spiele AION 2026 und GENESIS bleiben separat von Trendo OS.

## F. Grok Bot Cloud und Retention
Grok Bot teilt innerhalb eines Kontos Files, Browsercookies, gespeicherte Logins. Projekte sind deshalb lokal getrennt und Bot bekommt nur entschärfte Taskmetadaten. Weder Bot-Snapshot-Löschung noch 120h-Löschung in einem fremden Cloudprodukt darf ungeprüft garantiert werden; forensische Beweiskopien GAR NICHT dorthin hochladen. Sessiondaten nach minimaler Notwendigkeit handhaben, Export nur nach Freigabe.

## G. Status-Dashboard
`NOT_CONNECTED`, `PENDING_APPROVAL`, `READY_VERIFIED`, `RUNNING`, `FAILED`, `BLOCKED`. Jeder Agenten-Status kommt aus tatsächlicher Grok-/Tool-Session oder einem überprüften Job-Event. Kein fingierter Live-Agent.
