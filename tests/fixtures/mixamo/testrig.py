# Testfigur mit Knochennamen wie Mixamo (eigene Arbeit, keine Mixamo-Datei): Blender ohne Fenster
#   blender -b --factory-startup --python testrig.py -- <ordner>
# schreibt figur.fbx (Präfix mixamorig1:, T-Haltung, mit Mesh), walking.fbx (Präfix mixamorig:, läuft 2 m nach vorn, mit Mesh)
# und modell.glb (nur Mesh, für den Auto-Rigger). Maße wie ein Mensch (1,75 m), Skelett im Zentimeter-Maßstab wie bei Mixamo.
import bpy, sys, os, math
from mathutils import Vector

out = sys.argv[sys.argv.index("--") + 1]
os.makedirs(out, exist_ok=True)

BONES = [  # Name, Eltern, Kopf, Schwanz (Meter)
    ("Hips", None, (0, 0, 0.95), (0, 0, 1.05)),
    ("Spine", "Hips", (0, 0, 1.05), (0, 0, 1.18)),
    ("Spine1", "Spine", (0, 0, 1.18), (0, 0, 1.30)),
    ("Spine2", "Spine1", (0, 0, 1.30), (0, 0, 1.42)),
    ("Neck", "Spine2", (0, 0, 1.42), (0, 0, 1.52)),
    ("Head", "Neck", (0, 0, 1.52), (0, 0, 1.75)),
    ("LeftUpLeg", "Hips", (0.1, 0, 0.92), (0.1, 0, 0.5)),
    ("LeftLeg", "LeftUpLeg", (0.1, 0, 0.5), (0.1, 0, 0.08)),
    ("LeftFoot", "LeftLeg", (0.1, 0, 0.08), (0.1, -0.15, 0.02)),
    ("RightUpLeg", "Hips", (-0.1, 0, 0.92), (-0.1, 0, 0.5)),
    ("RightLeg", "RightUpLeg", (-0.1, 0, 0.5), (-0.1, 0, 0.08)),
    ("RightFoot", "RightLeg", (-0.1, 0, 0.08), (-0.1, -0.15, 0.02)),
    ("LeftArm", "Spine2", (0.2, 0, 1.4), (0.48, 0, 1.4)),
    ("LeftForeArm", "LeftArm", (0.48, 0, 1.4), (0.74, 0, 1.4)),
    ("RightArm", "Spine2", (-0.2, 0, 1.4), (-0.48, 0, 1.4)),
    ("RightForeArm", "RightArm", (-0.48, 0, 1.4), (-0.74, 0, 1.4)),
]


def build(prefix, walk):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    arm_data = bpy.data.armatures.new("Armature")
    arm = bpy.data.objects.new("Armature", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for name, parent, head, tail in BONES:
        b = arm_data.edit_bones.new(prefix + name)
        b.head = Vector(head) * 100; b.tail = Vector(tail) * 100  # Zentimeter wie bei Mixamo
        if parent: b.parent = arm_data.edit_bones[prefix + parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    arm.scale = (0.01, 0.01, 0.01)
    # Körper: einfache Glieder als Mesh, an die Knochen gebunden
    meshes = []
    for name, parent, head, tail in BONES:
        h, t = Vector(head), Vector(tail); mid = (h + t) / 2; ln = max((t - h).length, 0.05)
        bpy.ops.mesh.primitive_cylinder_add(radius=0.06 if "Leg" in name or "Hips" in name or "Spine" in name else 0.04, depth=ln, location=mid)
        o = bpy.context.active_object; o.name = "Teil_" + name
        d = (t - h).normalized(); o.rotation_euler = Vector((0, 0, 1)).rotation_difference(d).to_euler()
        vg = o.vertex_groups.new(name=prefix + name); vg.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
        meshes.append(o)
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes: o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]; bpy.ops.object.join()
    body = bpy.context.active_object; body.name = "Koerper"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    md = body.modifiers.new("Armature", "ARMATURE"); md.object = arm; body.parent = arm
    body.matrix_parent_inverse = arm.matrix_world.inverted()
    if walk:
        sc = bpy.context.scene; sc.frame_start = 1; sc.frame_end = 31; sc.render.fps = 30
        arm.animation_data_create(); act = bpy.data.actions.new("mixamo.com"); arm.animation_data.action = act
        hips = arm.pose.bones[prefix + "Hips"]
        for f in range(1, 32):
            p = (f - 1) / 30.0
            # Hüfte wandert 2 m nach vorn (−Y) und federt leicht
            pos = arm.matrix_world.inverted() @ Vector((0, -2.0 * p, 0.95 + 0.03 * math.sin(p * math.pi * 4)))  # Lage im Skelett (Zentimeter)
            hips.matrix = __import__("mathutils").Matrix.Translation(pos) @ hips.bone.matrix_local.to_3x3().to_4x4()
            hips.keyframe_insert("location", frame=f)
            hips.rotation_mode = "QUATERNION"; hips.keyframe_insert("rotation_quaternion", frame=f)
            for side, sign in (("Left", 1), ("Right", -1)):
                ul = arm.pose.bones[prefix + side + "UpLeg"]; ul.rotation_mode = "XYZ"
                ul.rotation_euler = (sign * 0.5 * math.sin(p * math.pi * 4), 0, 0); ul.keyframe_insert("rotation_euler", frame=f)
                fa = arm.pose.bones[prefix + side + "Arm"]; fa.rotation_mode = "XYZ"
                fa.rotation_euler = (0, 0, -sign * 0.4 * math.sin(p * math.pi * 4)); fa.keyframe_insert("rotation_euler", frame=f)
            bpy.context.scene.frame_set(f)
    return arm, body


def export(path, arm, body, anim):
    bpy.ops.object.select_all(action="DESELECT"); arm.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, bake_anim=anim, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, apply_unit_scale=True)


arm, body = build("mixamorig1:", False); export(os.path.join(out, "figur.fbx"), arm, body, False)
arm, body = build("mixamorig:", True); export(os.path.join(out, "walking.fbx"), arm, body, True)
arm, body = build("mixamorig:", False)
bpy.ops.object.select_all(action="DESELECT"); body.select_set(True); bpy.context.view_layer.objects.active = body
body.modifiers.clear(); body.parent = None
bpy.ops.export_scene.gltf(filepath=os.path.join(out, "modell.glb"), export_format="GLB", use_selection=True)
print("TESTRIG ok")
