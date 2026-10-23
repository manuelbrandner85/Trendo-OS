import unittest, pathlib, json, subprocess, sys, tempfile, importlib.util, os
ROOT=pathlib.Path(__file__).resolve().parents[2]
def mod(name, rel):
 spec=importlib.util.spec_from_file_location(name,ROOT/rel);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
class AgentConfigurationTests(unittest.TestCase):
 def test_agent_count_and_frontmatter(self):
  agents=sorted((ROOT/'.claude/agents').glob('*.md'))
  self.assertEqual(20,len(agents))
  names=[]
  for f in agents:
   txt=f.read_text(encoding='utf-8');self.assertTrue(txt.startswith('---\n'))
   name=next(s.split(': ',1)[1] for s in txt.splitlines() if s.startswith('name: '));names.append(name)
   self.assertIn('description:',txt);self.assertIn('model:',txt)
   if int(f.name[:2]) not in {1,2,3,18,19,20}:self.assertIn('isolation: worktree',txt)
  self.assertEqual(len(names),len(set(names)))
 def test_skills_and_settings(self):
  self.assertGreaterEqual(len(list((ROOT/'.claude/skills').glob('*/SKILL.md'))),12)
  settings=json.loads((ROOT/'.claude/settings.json').read_text(encoding='utf-8'))
  self.assertIn('PreToolUse',settings['hooks']);self.assertIn('SubagentStart',settings['hooks'])
  self.assertIn('trendo-readonly',json.loads((ROOT/'.mcp.json').read_text(encoding='utf-8'))['mcpServers'])
 def test_migration_rules(self):
  # M1: Grok-Regeln bleiben über den Import aktiv; M3: harte Sperren unabhängig vom Hook.
  self.assertIn('\n@AGENTS.md\n',(ROOT/'CLAUDE.md').read_text(encoding='utf-8'))
  self.assertTrue((ROOT/'docs/claude/MIGRATION_GROK_CLAUDE.md').is_file())
  perms=json.loads((ROOT/'.claude/settings.json').read_text(encoding='utf-8'))['permissions']
  for rule in ['Bash(git push * main)','PowerShell(git push *--force*)','Bash(*installer/release.sh*)',
               'PowerShell(Remove-Item *-Recurse*)','Edit(/AGENTS.md)','Edit(/docs/spec/decisions.yaml)']:
   self.assertIn(rule,perms['deny'])
  self.assertIn('Bash(git push *)',perms['ask'])
  ignored=(ROOT/'.gitignore').read_text(encoding='utf-8').splitlines()
  for p in ['.trendo-local/','.claude/worktrees/','.claude/settings.local.json']:self.assertIn(p,ignored)
 def test_decisions(self):
  txt=(ROOT/'docs/claude/TRENDO_GODMODE_ENTSCHEIDUNGEN_2026-10-23.md').read_text(encoding='utf-8')
  for s in ['42–85','86','87–96','97','120-Stunden']:self.assertIn(s,txt)
class HooksTests(unittest.TestCase):
 def test_denials_and_safe(self):
  script=ROOT/'.claude/hooks/trendo_hook.py'
  with tempfile.TemporaryDirectory() as d:
   env=os.environ.copy();env['CLAUDE_PROJECT_DIR']=d
   def call(tool,values):
    return subprocess.run([sys.executable,str(script),'pre'],input=json.dumps({'tool_name':tool,'tool_input':values,'session_id':'unit-test'}),text=True,capture_output=True,env=env)
   a=call('Bash',{'command':'git push origin main'});self.assertEqual(0,a.returncode);self.assertIn('"deny"',a.stdout)
   b=call('PowerShell',{'command':'Remove-Item C:\\ -Recurse'});self.assertIn('"deny"',b.stdout)
   c=call('Write',{'file_path':'AGENTS.md','content':'oops'});self.assertIn('"deny"',c.stdout)
   d2=call('Write',{'file_path':str(pathlib.Path(d)/'src/feature.cs'),'content':'ok'});self.assertEqual('',d2.stdout)
   events=(pathlib.Path(d)/'.trendo-local/events.jsonl').read_text(encoding='utf-8')
   self.assertNotIn('feature.cs',events);self.assertNotIn('git push',events)
class DatabaseTests(unittest.TestCase):
 def test_transactions_survive_new_connection(self):
  t=mod('trendo_ops_test','tools/trendo_ops.py')
  with tempfile.TemporaryDirectory() as d:
   old=(t.ROOT,t.LOCAL,t.DB)
   t.ROOT=pathlib.Path(d);t.LOCAL=t.ROOT/'.trendo-local';t.DB=t.LOCAL/'jobs.sqlite3'
   conns=[]
   try:
    c=t.connect();conns.append(c);j='T-unittest';at=t.now()
    c.execute('INSERT INTO jobs(id,title,prompt,status,created,updated) VALUES(?,?,?,?,?,?)',(j,'Test','private','QUEUED',at,at))
    t.transition(c,j,'READY');t.transition(c,j,'RUNNING');t.transition(c,j,'INTERRUPTED')
    c2=t.connect();conns.append(c2)
    self.assertEqual('INTERRUPTED',t.get(c2,j)['status'])
    with self.assertRaises(ValueError):t.transition(c,j,'SUCCEEDED')
   finally:
    # Windows: offene SQLite-Handles verhindern das Löschen des Temp-Ordners.
    for x in conns:x.close()
    t.ROOT,t.LOCAL,t.DB=old
class MCPTests(unittest.TestCase):
 def test_protocol(self):
  p=ROOT/'tools/trendo_mcp.py'
  reqs=[{'jsonrpc':'2.0','id':1,'method':'initialize','params':{'protocolVersion':'2025-06-18'}}, {'jsonrpc':'2.0','id':2,'method':'tools/list'}, {'jsonrpc':'2.0','id':3,'method':'tools/call','params':{'name':'trendo_capabilities','arguments':{}}}]
  cp=subprocess.run([sys.executable,str(p)],input='\n'.join(map(json.dumps,reqs))+'\n',capture_output=True,text=True,timeout=10)
  self.assertEqual(0,cp.returncode,cp.stderr)
  res=[json.loads(x) for x in cp.stdout.splitlines()]
  self.assertEqual(3,len(res));self.assertEqual('trendo-readonly',res[0]['result']['serverInfo']['name'])
  self.assertEqual(3,len(res[1]['result']['tools']))
  self.assertEqual(False,res[2]['result']['isError'])
if __name__=='__main__':unittest.main()
