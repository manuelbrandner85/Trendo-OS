"""Run Blender on a real .blend: --background FILE.blend --python this.py -- --report QA.json"""
import bpy,sys,argparse,pathlib,json
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
parser=argparse.ArgumentParser();parser.add_argument('--report',required=True);a=parser.parse_args(args)
meshes=[];total=0;issues=[]
for obj in bpy.data.objects:
 if obj.type!='MESH':continue
 mesh=obj.data;tris=sum(max(0,len(poly.vertices)-2) for poly in mesh.polygons);total+=tris
 uv=len(mesh.uv_layers)>0;mat=len(mesh.materials)>0
 edges=sum(1 for edge in mesh.edges if edge.use_edge_sharp) # diagnostic, not manifold certification
 if not uv:issues.append(obj.name+': missing UV map')
 if not mat:issues.append(obj.name+': missing material')
 if any(abs(float(v)-1)>0.001 for v in obj.scale):issues.append(obj.name+': non-unit object scale')
 meshes.append({'name':obj.name,'vertices':len(mesh.vertices),'triangles':tris,'uv':uv,'materials':len(mesh.materials),'scale':list(obj.scale),'sharp_edges':edges})
if not meshes:issues.append('no mesh objects')
report={'status':'PASS' if not issues else 'REVIEW','triangles':total,'blender_version':bpy.app.version_string,'meshes':meshes,'issues':issues,'note':'Topologie/Bildvergleich/UE-Import nicht allein durch diesen Test validiert'}
path=pathlib.Path(a.report).resolve();path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(report,ensure_ascii=False))
