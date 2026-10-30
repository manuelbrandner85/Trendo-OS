import bpy, sys, json
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=sys.argv[-1])
o=[x for x in bpy.context.scene.objects if x.type=='MESH'][0]
d=o.dimensions; mx=max(v.co.z for v in o.data.vertices); mn=min(v.co.z for v in o.data.vertices)
w=o.matrix_world
zs=[(w @ v.co).z for v in o.data.vertices]; ys=[(w @ v.co).y for v in o.data.vertices]; xs=[(w @ v.co).x for v in o.data.vertices]
print("ERGEBNIS", json.dumps({"breite_x":round(max(xs)-min(xs),4),"tiefe_y":round(max(ys)-min(ys),4),"hoehe_z":round(max(zs)-min(zs),4),"blender":bpy.app.version_string}))
