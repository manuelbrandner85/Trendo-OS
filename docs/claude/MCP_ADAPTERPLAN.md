# MCP-Adapter: belastbarer Stand

- `.mcp.json` startet **nur** `trendo-readonly`, einen im Paket befindlichen, standardbibliothekbasierten Python-stdio-Server. Er hat lesende Werkzeuge `trendo_capabilities`, `trendo_git_status`, `trendo_jobs` und keine Schreib- oder Ausführungswerkzeuge.
- Die operative Blender-Integration läuft über `tools/blender/smoke_asset.py` und `tools/blender/inspect_asset.py`, welche reale Blender-Dateien erzeugen/prüfen, **wenn Blender lokal installiert ist**. Es wird kein nicht existenter Blender-MCP als bereits verbunden deklariert.
- Die Unreal-Integration ist `tools/unreal/validate_project.py` als realer CLI-Adapter mit expliziten Engine-/Projektpfaden. Nur mit installiertem Editor testen.
- Für optionale Blender-/Unreal-MCP-Server zuerst vorhandene vertrauenswürdige Version, unterstützte API, Lizenz und Rechte prüfen. Danach eine _separate_ lokale `.mcp.json`-Erweiterung nach Nutzerfreigabe, keine unbestätigten Installationskommandos oder Netzwerkfreigaben.
- `trendo-readonly` bietet **keine** OS-Sandbox. Es führt keine Produktiv- oder Maschinenaktionen aus.
