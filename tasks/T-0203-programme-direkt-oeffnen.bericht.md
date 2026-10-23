# T-0203 Bericht – Externe Programme direkt öffnen

- **Gemeinsamer Starter (Core):** `ExternalApps` (Blender, Unreal, VS Code, LibreOffice, OBS) plant Programm + Argumente; `Integrations.LaunchArgs` / `IProcessLauncher.OpenWithArgs` starten ohne Shell-Parsing, jede Aktion im Audit-Log.
  - Blender: `.blend` direkt; `.glb/.gltf/.obj/.stl/.fbx/.ply` werden in eine leere Szene importiert.
  - Unreal: `.uproject`; VS Code: Projektordner; LibreOffice: Dokument; OBS: Szenensammlung (eine Szene je Programmpunkt) wird in den OBS-Ordner gelegt und mit `--collection` geladen.
- **Knöpfe:** Spiele („In Unreal Engine öffnen“ je Unreal-Projekt), Webseite/Software („In VS Code öffnen“ je Projekt), Office („In LibreOffice öffnen“, zuletzt angelegtes Dokument), Sendung & Stream („In OBS Studio öffnen“).
- **Fehlt ein Programm:** „… einrichten …“ → eine Rückfrage (Größe, Lizenz) → „Ja, einrichten“ nutzt die bestehende Programmprüfung/Installation (Freigabe im Approval-Gate). Danach erscheint „In … öffnen“ sofort, ohne Neustart. Unreal: nur Herstellerseite (Epic-Lizenz). Nichts wird ungefragt installiert.
- **Zentrale Liste:** Programme → „Externe Programme“ zeigt alle fünf mit Pfad und „Starten“ bzw. „Einrichten“.
- Abhängigkeitskatalog um **VS Code** und **OBS Studio** ergänzt.
- **3D-Studio nicht angefasst** (wird gerade umgebaut): Der Umbau kann `OpenInButton("blender", () => modell, "3d")` bzw. `OpenInApp("blender", datei)` nutzen.
- **Nebenbefund behoben:** Knöpfe aus `Act(...)` hatten `Background = null` und waren per Maus nur schwer bzw. gar nicht treffbar (Trefferprüfung findet keinen Knopf). Jetzt `Transparent`, sieht gleich aus.
- Tests: `T0203_ExternalApps` (Core, 4), `T0203_Programme_Ui` (5, echte Mausklicks, Fake-Launcher prüft Programmpfad UND Datei).
- Offen: PC-Nachweis mit echten Programmen (Integrator).
