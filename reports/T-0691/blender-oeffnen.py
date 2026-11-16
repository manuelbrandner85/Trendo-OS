# T-0691: Prüfskript – öffnet die von Trendo exportierten Dateien in einem eigenen Blender-Hintergrundprozess (blender -b) und zählt Geometrie.
import bpy, sys, os
d = sys.argv[sys.argv.index("--") + 1]
for name, op in [("affe.gltf", "gltf"), ("affe.glb", "gltf"), ("affe.obj", "obj"), ("affe.stl", "stl")]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    f = os.path.join(d, name)
    if op == "gltf": bpy.ops.import_scene.gltf(filepath=f)
    elif op == "obj": bpy.ops.wm.obj_import(filepath=f)
    else: bpy.ops.wm.stl_import(filepath=f)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
    pts = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
    ext = [max(p[i] for p in pts) - min(p[i] for p in pts) for i in range(3)]
    print(f"BELEG {name}: Objekte={len(meshes)} Dreiecke={tris} Ausdehnung X/Y/Z={ext[0]:.4f}/{ext[1]:.4f}/{ext[2]:.4f} (Blender-Einheiten)")
print("BELEG Blender", bpy.app.version_string)
