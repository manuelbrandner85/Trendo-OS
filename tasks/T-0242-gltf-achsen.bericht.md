# Bericht T-0242 – glTF-Achsen

- **Status:** IMPLEMENTED, automatisierte Tests grün, Gegenprobe in echtem Blender 5.2.3 LTS bestanden. Unreal-Import nicht geprüft.

## Änderung
- `src/Trendo.Core/MeshIO.cs`: `ToGltf` (x, z, −y) beim Schreiben von Positionen und Normalen, `FromGltf` (x, −z, y) beim Lesen von GLB/glTF. Drehung um X (Determinante +1), Umlaufsinn bleibt.
- `tests/Trendo.Core.Tests/Scene3DTests.cs`: neuer Test `Gltf_axes` (Quader 10×20×40 cm → im GLB 0,1/0,4/0,2 m, Rundreise unverändert). Optional `TRENDO_GLTF_OUT` legt die Datei für die Blender-Gegenprobe ab.
- Version 0.9.17, `installer/neuerungen/0.9.17.md`.

## Nachweise
- Volltest: Api 15/15, Core 848 bestanden/1 übersprungen, App 434 bestanden/3 übersprungen, Paket-Tests 8/8 OK – `reports/T-0242/volltest.log`, SHA-256 `5ce022fe696d2ad8bf00fc51c3e20c9ecfb933ca628ba0ae58be92adae807b42`
- Blender 5.2.3 LTS (`--background --factory-startup`, Import per `bpy.ops.import_scene.gltf`): Breite X 0,1 m, Tiefe Y 0,2 m, Höhe Z 0,4 m – `reports/T-0242/blender-gegenprobe.log`, SHA-256 `a584d597fbf30a042b0deab38c5ed95e236befbb509bb77d52b3cde45cdb6dcb`; Skript `blender-pruefen.py`, Datei `quader.glb`

## Folgen / Risiken
- GLB-Dateien, die mit älteren Trendo-Versionen gespeichert wurden, liegen nun beim erneuten Öffnen in Trendo gekippt (sie waren ohne Achsenwechsel geschrieben). Neu speichern behebt es.
- OBJ/STL/FBX unverändert (eigene Konventionen, nicht Teil dieses Auftrags).

## Nicht geprüft
Unreal-Import, Web-Vorschau im Browser, Linux/Pi.
