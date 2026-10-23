"""Run with Blender background --python. Creates REAL demo .blend and glb if Blender exists."""
import bpy,sys,argparse,json,pathlib
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
parser=argparse.ArgumentParser();parser.add_argument('--output',required=True);a=parser.parse_args(args)
out=pathlib.Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,location=(0,0,1))
ob=bpy.context.object;ob.name='Trendo_SmokeSphere'
mat=bpy.data.materials.new('Trendo_SmokePBR');mat.use_nodes=True
node=mat.node_tree.nodes.get('Principled BSDF')
if node:node.inputs['Base Color'].default_value=(0.08,0.25,0.7,1.0);node.inputs['Metallic'].default_value=0.25;node.inputs['Roughness'].default_value=0.35
ob.data.materials.append(mat)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'trendo_test.blend'))
bpy.ops.export_scene.gltf(filepath=str(out/'trendo_test.glb'),export_format='GLB',use_selection=False)
report={'status':'PRODUCED_NOT_UNREAL_TESTED','objects':len([o for o in bpy.data.objects if o.type=='MESH']),'blend':str(out/'trendo_test.blend'),'glb':str(out/'trendo_test.glb'),'blender_version':bpy.app.version_string}
(out/'asset_manifest.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
