#!/usr/bin/env python3
"""Claude Code hook (stdin JSON). Best-effort policy + privacy-safe real event capture.
Not an OS sandbox: native Claude permissions and Trendo server gates remain authoritative.
"""
import json, os, sys, re, datetime, pathlib

def denial(reason):
 print(json.dumps({'hookSpecificOutput': {'hookEventName':'PreToolUse','permissionDecision':'deny','permissionDecisionReason':reason}},ensure_ascii=False))
 return 0

def safe_event(obj):
 # No commands, file contents, secrets or raw tool arguments retained.
 return {'at':datetime.datetime.now(datetime.timezone.utc).isoformat(),
         'kind':sys.argv[1] if len(sys.argv)>1 else 'unknown',
         'tool':str(obj.get('tool_name',''))[:70],
         'session':str(obj.get('session_id',''))[:90],
         'agent':str(obj.get('agent_id') or obj.get('agent_type') or '')[:90],
         'job':re.sub('[^A-Za-z0-9_-]','',os.environ.get('TRENDO_JOB_ID',''))[:60],
         'failed':bool(obj.get('is_error',False))}

def main():
 try:
  obj=json.loads(sys.stdin.read(3_000_000) or '{}')
  if not isinstance(obj,dict):raise ValueError('JSON-Objekt erforderlich')
 except Exception:
  if len(sys.argv)>1 and sys.argv[1]=='pre':return denial('Sicherheits-Hook: unlesbarer Aufruf blockiert')
  return 0
 kind=sys.argv[1] if len(sys.argv)>1 else ''
 if kind=='pre':
  tool=str(obj.get('tool_name',''))
  ti=obj.get('tool_input') or {}
  if not isinstance(ti,dict):return denial('Unbekannte Toolparameter')
  if tool in ('Bash','PowerShell'):
   cmd=str(ti.get('command') or '')
   # Defense in depth. Enforce native permissions, OS controls and independent release approval too.
   patterns=[r'\bgit\s+(?:push|merge|rebase|reset\s+--hard|clean\s+-[a-z]*f|branch\s+-D)\b',
             r'\bgh\s+(?:pr\s+merge|release\s+create)\b',r'\b(?:rm\s+-rf|Remove-Item\b[^\n]*-Recurse)\b',
             r'\b(?:format(?:-volume)?|diskpart|reg\s+(?:add|delete)|bcdedit|Set-ExecutionPolicy)\b',
             r'\b(?:curl|wget|Invoke-WebRequest)\b[^\n]*\|\s*(?:bash|sh|iex|Invoke-Expression)\b',
             r'\b(?:shutdown|Restart-Computer|Stop-Computer)\b',
             r'\b(?:chmod\s+777|sudo\s+|Start-Process\b[^\n]*-Verb\s+RunAs)\b']
   if any(re.search(x,cmd,re.I) for x in patterns):return denial('Riskanter System-/Git-/Netzwerkbefehl: nur manuell nach separater Freigabe')
   if re.search(r'\bgit\s+(?:commit|checkout|switch)\b',cmd,re.I):
    # Do not create any new trust by auto-allowing. Native permission flow applies.
    pass
  if tool in ('Write','Edit','MultiEdit','NotebookEdit'):
   path=str(ti.get('file_path') or ti.get('notebook_path') or '')
   if not path:return denial('Dateipfad nicht angegeben')
   # Disallow edits to authoritative registers and installed hook safeguards.
   normalized='/'+path.replace('\\','/').lower().strip().lstrip('/')
   blocked=['/agents.md','/docs/spec/decisions.yaml','/requirements.yaml',
            '/.claude/settings.json','/.mcp.json','/.claude/hooks/trendo_hook.py']
   if any(normalized.endswith(x) for x in blocked):return denial('Geschützte Projekt-/Sicherheitsdatei: gesonderte geprüfte Migration erforderlich')
   if '/.git/' in normalized or normalized.endswith('/.git'):return denial('Direktes Schreiben in .git verboten')
   if re.search(r'\.(env|pem|p12|pfx|key)$',normalized):return denial('Secrets-Datei darf nicht über diesen Agenten erstellt/überschrieben werden')
   if '..' in pathlib.PurePath(normalized).parts:return denial('Relative Pfadflucht blockiert')
   root=os.environ.get('CLAUDE_PROJECT_DIR')
   if root and os.path.isabs(path):
    # For Claude-native worktrees outside main checkout, allow only explicitly
    # registered worktrees under a sibling <repo>-worktrees directory.
    dest=pathlib.Path(path).resolve(strict=False)
    base=pathlib.Path(root).resolve()
    sibling=base.parent/(base.name+'-worktrees')
    if not (dest.is_relative_to(base) or dest.is_relative_to(sibling.resolve(strict=False))):
     return denial('Dateizugriff außerhalb Projekt/Worktree blockiert')
 try:
  root=pathlib.Path(os.environ.get('CLAUDE_PROJECT_DIR') or os.getcwd())
  local=root/'.trendo-local'
  local.mkdir(parents=True,exist_ok=True)
  with (local/'events.jsonl').open('a',encoding='utf-8') as fh:
   fh.write(json.dumps(safe_event(obj),ensure_ascii=False)+'\n')
 except Exception:
  # Observational failures don't rewrite tool outcomes. For pre checks, no security claim.
  pass
 return 0
if __name__=='__main__':sys.exit(main())
