# T-0749: Fremdprogramm-Gegenprobe. Blender importiert die Exporte von Trendo (OBJ, STL, GLB),
# meldet Punkte, Dreiecke und Ausmasse und rendert eine Ansicht des Scans als Sichtpruefung.
#   blender -b --factory-startup --python t0749_pruefen.py -- <ordner mit T-0749-scan.*> <bild.png>
import bpy, sys, os, math, json

argv = sys.argv[sys.argv.index("--") + 1:]
folder, shot = argv[0], argv[1]
result = {}
for ext in ("obj", "stl", "glb"):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path = os.path.join(folder, "T-0749-scan." + ext)
    if ext == "obj":
        bpy.ops.wm.obj_import(filepath=path)
    elif ext == "stl":
        bpy.ops.wm.stl_import(filepath=path)
    else:
        bpy.ops.import_scene.gltf(filepath=path)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    verts = sum(len(o.data.vertices) for o in meshes)
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
    dims = [round(max(o.dimensions[i] for o in meshes), 3) for i in range(3)] if meshes else []
    result[ext] = {"objekte": len(meshes), "punkte": verts, "dreiecke": tris, "ausmass": dims}
    print(f"T0749_IMPORT {ext}: objekte={len(meshes)} punkte={verts} dreiecke={tris} ausmass={dims}")

# Sichtpruefung: GLB ist noch geladen
scene = bpy.context.scene
obj = [o for o in scene.objects if o.type == "MESH"][0]
mat = bpy.data.materials.new("Grau"); mat.diffuse_color = (0.7, 0.7, 0.7, 1)
obj.data.materials.clear(); obj.data.materials.append(mat)
cam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera")); scene.collection.objects.link(cam); scene.camera = cam
c = sum((obj.matrix_world @ v.co for v in obj.data.vertices), obj.location * 0) / max(1, len(obj.data.vertices))
r = max(obj.dimensions) * 1.6
cam.location = (c.x + r * 0.6, c.y - r * 0.9, c.z + r * 0.5)
d = c - cam.location
cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
sun = bpy.data.objects.new("Sonne", bpy.data.lights.new("Sonne", "SUN")); scene.collection.objects.link(sun)
sun.rotation_euler = (math.radians(45), 0, math.radians(30))
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x, scene.render.resolution_y = 800, 600
scene.render.filepath = shot
bpy.ops.render.render(write_still=True)
print("T0749_PRUEFUNG " + json.dumps(result))
