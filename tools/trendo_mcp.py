#!/usr/bin/env python3
"""Minimal stdio MCP server: strictly read-only, stdlib, newline JSON-RPC."""
import sys,json,pathlib,shutil,subprocess,sqlite3
ROOT=pathlib.Path(__file__).resolve().parents[1]
TOOLS=[
 {'name':'trendo_capabilities','description':'Read-only availability probe for local software and Trendo root','inputSchema':{'type':'object','properties':{},'additionalProperties':False}},
 {'name':'trendo_git_status','description':'Read-only repo branch and git status (no secrets/content)','inputSchema':{'type':'object','properties':{},'additionalProperties':False}},
 {'name':'trendo_jobs','description':'Read-only local job states (no private prompts)','inputSchema':{'type':'object','properties':{},'additionalProperties':False}},
]
def call(name):
 if name=='trendo_capabilities':return {'tools':{k:bool(shutil.which(k)) for k in ['python','git','dotnet','claude','blender']},'root_repo':(ROOT/'Trendo.sln').exists()}
 if name=='trendo_git_status':
  if not (ROOT/'.git').exists():return {'status':'not-in-git-repository'}
  p=subprocess.run(['git','-C',str(ROOT),'status','--short','--branch'],capture_output=True,text=True,timeout=7)
  return {'exit':p.returncode,'lines':p.stdout.splitlines()[:30]}
 if name=='trendo_jobs':
  db=ROOT/'.trendo-local/jobs.sqlite3'
  if not db.is_file():return []
  with sqlite3.connect('file:'+str(db)+'?mode=ro',uri=True) as c:
   return [{'id':r[0],'title':r[1],'status':r[2]} for r in c.execute('SELECT id,title,status FROM jobs ORDER BY created DESC LIMIT 50')]
 raise ValueError('Unknown MCP tool')
def response(req):
 method=req.get('method');rid=req.get('id')
 if method=='notifications/initialized' or (rid is None and method and method.startswith('notifications/')):return None
 if method=='initialize':
  version=req.get('params',{}).get('protocolVersion','2025-06-18')
  return {'jsonrpc':'2.0','id':rid,'result':{'protocolVersion':version,'capabilities':{'tools':{'listChanged':False}},'serverInfo':{'name':'trendo-readonly','version':'2.0.0'}}}
 if method=='tools/list':return {'jsonrpc':'2.0','id':rid,'result':{'tools':TOOLS}}
 if method=='tools/call':
  name=req.get('params',{}).get('name','')
  try:
   val=call(name);err=False
  except Exception as e:
   val={'error':str(e)};err=True
  return {'jsonrpc':'2.0','id':rid,'result':{'content':[{'type':'text','text':json.dumps(val,ensure_ascii=False)}],'isError':err}}
 if method=='ping':return {'jsonrpc':'2.0','id':rid,'result':{}}
 return {'jsonrpc':'2.0','id':rid,'error':{'code':-32601,'message':'Method not found'}}
if __name__=='__main__':
 for line in sys.stdin:
  try:
   req=json.loads(line);res=response(req)
   if res is not None:print(json.dumps(res,ensure_ascii=False),flush=True)
  except Exception as e:
   print(json.dumps({'jsonrpc':'2.0','id':None,'error':{'code':-32700,'message':'Parse/dispatch error'}}),flush=True)
