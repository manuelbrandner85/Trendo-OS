# T-0749: Testbildsatz fuer den Fotoscan. Aufruf:
#   blender -b --factory-startup --python t0749_bilder.py -- <ausgabeordner> [anzahl]
# Ein texturierter Testkoerper (Affe auf Sockel, prozedurale Farbflecken), Kameras auf zwei Ringen.
import bpy, math, sys, os

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out = argv[0] if argv else os.path.abspath("bilder")
count = int(argv[1]) if len(argv) > 1 else 48
os.makedirs(out, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

def textured(name, seed):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.8
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 9.0
    noise.inputs["Detail"].default_value = 12.0
    noise.noise_dimensions = "4D"
    noise.inputs["W"].default_value = seed
    vor = nt.nodes.new("ShaderNodeTexVoronoi")
    vor.inputs["Scale"].default_value = 25.0
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.05, 0.04, 0.03, 1)
    ramp.color_ramp.elements[1].color = (0.95, 0.85, 0.6, 1)
    ramp.color_ramp.interpolation = "CONSTANT"
    for pos, col in ((0.35, (0.8, 0.15, 0.1, 1)), (0.5, (0.1, 0.5, 0.9, 1)), (0.62, (0.9, 0.9, 0.2, 1))):
        e = ramp.color_ramp.elements.new(pos); e.color = col
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.inputs["Factor"].default_value = 0.25
    nt.links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], mix.inputs["A"])
    nt.links.new(vor.outputs["Color"], mix.inputs["B"])
    nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
    return m

bpy.ops.mesh.primitive_monkey_add(size=1.4, location=(0, 0, 0.75))
monkey = bpy.context.active_object
mod = monkey.modifiers.new("sub", "SUBSURF"); mod.levels = 2; mod.render_levels = 2
bpy.ops.object.shade_smooth()
monkey.data.materials.append(textured("Haut", 1.3))

bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, -0.1))
base = bpy.context.active_object
base.scale = (1.6, 1.6, 0.3)
base.data.materials.append(textured("Sockel", 7.7))

world = bpy.data.worlds.new("Welt"); scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.02, 0.02, 0.02, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
sun = bpy.data.lights.new("Sonne", "SUN"); sun.energy = 3.0
so = bpy.data.objects.new("Sonne", sun); scene.collection.objects.link(so)
so.rotation_euler = (math.radians(40), 0, math.radians(30))
fill = bpy.data.lights.new("Licht", "AREA"); fill.energy = 400; fill.size = 5
fo = bpy.data.objects.new("Licht", fill); scene.collection.objects.link(fo)
fo.location = (-3, -3, 4); fo.rotation_euler = (math.radians(50), 0, math.radians(-45))

cam = bpy.data.cameras.new("Kamera"); cam.lens = 35
co = bpy.data.objects.new("Kamera", cam); scene.collection.objects.link(co); scene.camera = co
target = bpy.data.objects.new("Ziel", None); scene.collection.objects.link(target); target.location = (0, 0, 0.5)
tc = co.constraints.new("TRACK_TO"); tc.target = target; tc.track_axis = "TRACK_NEGATIVE_Z"; tc.up_axis = "UP_Y"

scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 64
scene.cycles.use_denoising = True
scene.render.resolution_x = 960
scene.render.resolution_y = 720
scene.render.image_settings.file_format = "JPEG"
scene.render.image_settings.quality = 92

rings = [(4.2, 0.9), (4.0, 2.6)]
per = count // len(rings)
n = 0
for r, z in rings:
    for i in range(per):
        a = 2 * math.pi * i / per + (0.3 if z > 2 else 0)
        co.location = (r * math.cos(a), r * math.sin(a), z)
        scene.render.filepath = os.path.join(out, f"bild_{n:03d}.jpg")
        bpy.ops.render.render(write_still=True)
        n += 1
print(f"T0749_RENDER_FERTIG {n} Bilder in {out}")
