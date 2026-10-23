# Blender–Unreal fachlicher Produktionsworkflow

1. **Capabilities**: `where blender`, `where UnrealEditor-Cmd.exe`, `python tools/trendo_ops.py doctor`; keine Blender/UE-Installation aus einem Prompt heraus.
2. **Designauftrag**: echte Referenzen oder freigegebene textliche Specs, Asset-ID, Maße (Meter), Blickwinkel, Modellklasse, Stil, PBR, Polybudget, LOD, Animation.
3. **Blender-Produktion**: bevorzugt `bpy` über kontrollierten lokalen Hintergrundprozess; `.blend` im Projekt, Materialien, UV, reale Meshes; via `smoke_asset.py` nur einen Test-Asset erzeugen.
4. **QA Blender**: `inspect_asset.py` berichtet Meshzahlen, Dreiecke, UVs, Materialien, Nonmanifold-Kanten, Skalen, Quellpfad. Fehler im Agenten-/QA-Report dokumentieren.
5. **Handoff**: echtes glTF/FBX mit Units/Axis, Materials, Asset-Manifest JSON mit SHA256 und Lizenzstatus. Keine Fake-Dateien und keine Bindung von fremden Bildern ohne Rechte.
6. **UE Integration**: installierten Unreal-Editor, Projektpfad und Zielpipeline prüfen; Assetimport nur über vorhandene unterstützte Adapter, Logs und Screenshots/automatisierte Szenentests dokumentieren.
7. **Performance**: Unreal-Testlauf auf Zielhardware, LOD/Material/Shader-Budgets; kein automatisches AAA+-Versprechen.
8. **Endstatus**: `VERIFIED` nur wenn Blender- und UE-Artefakte tatsächlich validiert wurden, sonst `PRODUCED`/`BLOCKED`/`UNSUPPORTED`.

### Lokaler Smoke-Test (ausdrücklich vom Nutzer ausführen)

```powershell
blender --background --python tools/blender/smoke_asset.py -- --output .trendo-local/smoke
blender --background .trendo-local/smoke/trendo_test.blend --python tools/blender/inspect_asset.py -- --report .trendo-local/smoke/qa.json
python tools/unreal/validate_project.py --editor "C:\...\UnrealEditor-Cmd.exe" --uproject "C:\...\MyGame.uproject" --dry-run
```

Smoke-Test ist kein echtes AAA+-Produktionsmodell, sondern überprüft den Mechanismus mit echten Dateien.
