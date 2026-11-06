# Beleg S2-17 – Blender-Rundlauf „Für Engine“ (06.11.2026)

- Blender: Blender 5.2.3 LTS (Windows, ohne Fenster: --background --factory-startup --python-exit-code 1)
- Quelle: figur.blend (2 Teile, 2 Materialien, Skelett mit 2 Knochen, Aktion „Winken“, Formschlüssel „Laecheln“)
- Skript: Studio3DEngines.Script, SHA-256 aeaff72cca96562eedeca819b8f0d7a0750d44bc8835a766d8b61679ce8e9634
- Ergebnis: alle vier Ziele Exitcode 0, Rundlauf ohne Verlust (Teile, Dreiecke, Materialien, UV, Knochen, Animationen, Formschlüssel, Maße)
- Nicht geprüft: Import in Unity/Unreal/Godot selbst (Unity nicht installiert, Godot nicht gefunden, Unreal-Import nicht automatisiert)

| Ziel | Exitcode | Datei | SHA-256 |
|---|---|---|---|
| unity | 0 | figur-unity.fbx | 6894a45b1ed9cee01970f93ec5dc1b6d11c303b74f25aac36e0b2a8dbe838303 |
| unreal | 0 | figur-unreal.fbx | f114627834bfd5b65e141323391439adf92b2137cbd299e2849316e1c4af0f29 |
| godot | 0 | figur-godot.glb | ceb2b7ab70fe345cafb910bd1e56bceaa332a92688a204a49d1316eac58d8da3 |
| web | 0 | figur-web.glb | ceb2b7ab70fe345cafb910bd1e56bceaa332a92688a204a49d1316eac58d8da3 |

## Berichte (Pfade neutralisiert)

```json
{"target": "unity", "file": "<temp>/figur-unity.fbx", "source": {"meshes": 2, "vertices": 490, "triangles": 972, "materials": 2, "meshesWithUv": 2, "bones": 2, "animations": 1, "shapeKeys": 1, "sizeX": 1.0, "sizeY": 0.6, "sizeZ": 2.7}, "result": {"meshes": 2, "vertices": 490, "triangles": 972, "materials": 2, "meshesWithUv": 2, "bones": 2, "animations": 2, "shapeKeys": 1, "sizeX": 1.0, "sizeY": 0.6, "sizeZ": 2.7}}
```

```json
{"target": "unreal", "file": "<temp>/figur-unreal.fbx", "source": {"meshes": 2, "vertices": 490, "triangles": 972, "materials": 2, "meshesWithUv": 2, "bones": 2, "animations": 1, "shapeKeys": 1, "sizeX": 1.0, "sizeY": 0.6, "sizeZ": 2.7}, "result": {"meshes": 2, "vertices": 490, "triangles": 972, "materials": 2, "meshesWithUv": 2, "bones": 2, "animations": 2, "shapeKeys": 1, "sizeX": 1.0, "sizeY": 0.6, "sizeZ": 2.7}}
```

```json
{"target": "godot", "file": "<temp>/figur-godot.glb", "source": {"meshes": 2, "vertices": 490, "triangles": 972, "materials": 2, "meshesWithUv": 2, "bones": 2, "animations": 1, "shapeKeys": 1, "sizeX": 1.0, "sizeY": 0.6, "sizeZ": 2.7}, "result": {"meshes": 2, "vertices": 2008, "triangles": 972, "materials": 2, "meshesWithUv": 2, "bones": 2, "animations": 1, "shapeKeys": 1, "sizeX": 1.0, "sizeY": 0.6, "sizeZ": 2.7}}
```

```json
{"target": "web", "file": "<temp>/figur-web.glb", "source": {"meshes": 2, "vertices": 490, "triangles": 972, "materials": 2, "meshesWithUv": 2, "bones": 2, "animations": 1, "shapeKeys": 1, "sizeX": 1.0, "sizeY": 0.6, "sizeZ": 2.7}, "result": {"meshes": 2, "vertices": 2008, "triangles": 972, "materials": 2, "meshesWithUv": 2, "bones": 2, "animations": 1, "shapeKeys": 1, "sizeX": 1.0, "sizeY": 0.6, "sizeZ": 2.7}}
```

## Rohlog (Ende je Lauf)

### unity
```
TRENDO_ENGINE_EXPORT ok
Blender 5.2.3 LTS (hash e13b728c6880 built 2026-10-22 01:37:16)

Blender quit
```

### unreal
```
TRENDO_ENGINE_EXPORT ok
Blender 5.2.3 LTS (hash e13b728c6880 built 2026-10-22 01:37:16)

Blender quit
```

### godot
```
TRENDO_ENGINE_EXPORT ok
Blender 5.2.3 LTS (hash e13b728c6880 built 2026-10-22 01:37:16)

Blender quit
```

### web
```
TRENDO_ENGINE_EXPORT ok
Blender 5.2.3 LTS (hash e13b728c6880 built 2026-10-22 01:37:16)

Blender quit
```
