#!/usr/bin/env python3
"""Local Trendo job registry, minimal worktree helper and opt-in Claude CLI launcher."""
import argparse, datetime, json, os, pathlib, re, shutil, sqlite3, subprocess, sys, uuid
ROOT=pathlib.Path(__file__).resolve().parents[1]
LOCAL=ROOT/'.trendo-local'
DB=LOCAL/'jobs.sqlite3'
VALID={'QUEUED','READY','RUNNING','FAILED','BLOCKED','SUCCEEDED','INTERRUPTED','CANCELLED'}
MAX_RUNNING=3  # T-0249 (V110/V111): höchstens 3 Aufträge gleichzeitig
TRANS={'QUEUED':{'READY','CANCELLED','BLOCKED'},'READY':{'RUNNING','CANCELLED','BLOCKED'},'RUNNING':{'SUCCEEDED','FAILED','INTERRUPTED','BLOCKED'},'INTERRUPTED':{'READY','CANCELLED','BLOCKED'},'FAILED':{'READY','CANCELLED'},'BLOCKED':{'READY','CANCELLED'},'SUCCEEDED':set(),'CANCELLED':set()}

def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def bootstrap():
 LOCAL.mkdir(parents=True,exist_ok=True)
 info=ROOT/'.git/info/exclude'
 if info.is_file():
  prev=info.read_text(encoding='utf-8',errors='replace')
  if '.trendo-local/' not in prev:
   with info.open('a',encoding='utf-8') as f:f.write('\n# Trendo local state (GODMODE 2)\n.trendo-local/\n')

def connect():
 bootstrap(); c=sqlite3.connect(DB,timeout=10,isolation_level=None)
 c.row_factory=sqlite3.Row;c.execute('PRAGMA journal_mode=WAL');c.execute('PRAGMA busy_timeout=10000')
 c.execute('CREATE TABLE IF NOT EXISTS jobs(id TEXT PRIMARY KEY,title TEXT NOT NULL,prompt TEXT NOT NULL,status TEXT NOT NULL,created TEXT NOT NULL,updated TEXT NOT NULL,attempt INTEGER NOT NULL DEFAULT 0,branch TEXT,worktree TEXT,last_exit INTEGER)')
 c.execute('CREATE TABLE IF NOT EXISTS transitions(job_id TEXT,at TEXT,old TEXT,new TEXT,detail TEXT)')
 cols={r[1] for r in c.execute('PRAGMA table_info(jobs)')}
 if 'paths' not in cols:c.execute('ALTER TABLE jobs ADD COLUMN paths TEXT')  # T-0249: erlaubte Dateien/Ordner (JSON-Liste)
 if 'pid' not in cols:c.execute('ALTER TABLE jobs ADD COLUMN pid INTEGER')
 return c

SECRET=re.compile(r'(?i)(password|passwort|token|secret|api[_-]?key)(\s*[=:]\s*)[^\s&"\']+')
def mask(text):
 """T-0249: Geheimnisse in Ereignissen unkenntlich machen."""
 return SECRET.sub(lambda m:m.group(1)+m.group(2)+'***',str(text))

def alive(pid):
 """Läuft der Prozess noch? Unter Windows nie os.kill (das beendet ihn), sondern nur nachfragen."""
 if not pid:return False
 if os.name=='nt':
  import ctypes
  k=ctypes.windll.kernel32;h=k.OpenProcess(0x1000,False,int(pid))
  if not h:return False
  code=ctypes.c_ulong();ok=k.GetExitCodeProcess(h,ctypes.byref(code));k.CloseHandle(h)
  return bool(ok) and code.value==259
 try:os.kill(int(pid),0);return True
 except OSError:return False

def recover(c):
 """T-0249: RUNNING-Aufträge, deren Prozess nicht mehr läuft (Absturz, Neustart), auf INTERRUPTED setzen."""
 n=0
 for row in c.execute("SELECT id,pid FROM jobs WHERE status='RUNNING'").fetchall():
  if not alive(row['pid']):transition(c,row['id'],'INTERRUPTED','Prozess läuft nicht mehr (beim Start erkannt)');n+=1
 return n

def paths_of(j):
 try:return [p.strip().replace('\\','/').rstrip('/') for p in json.loads(j.get('paths') or '[]') if str(p).strip()]
 except ValueError:return []

def overlaps(a,b):
 return any(x==y or x.startswith(y+'/') or y.startswith(x+'/') for x in a for y in b)

def check_start(c,j):
 """T-0249: Start nur mit Pfadliste, höchstens MAX_RUNNING gleichzeitig, nie zwei Aufträge auf denselben Dateien."""
 mine=paths_of(j)
 if not mine:return 'BLOCKED','Pfadliste fehlt – bitte mit --paths die erlaubten Dateien/Ordner angeben'
 running=[dict(r) for r in c.execute("SELECT * FROM jobs WHERE status='RUNNING' AND id<>?",(j['id'],)).fetchall()]
 if len(running)>=MAX_RUNNING:return 'WAIT',f'Es laufen schon {len(running)} Aufträge (höchstens {MAX_RUNNING})'
 for r in running:
  if overlaps(mine,paths_of(r)):return 'WAIT','Gleiche Dateien wie laufender Auftrag '+r['id']
 return 'OK',''

def event(kind,job='',detail=''):
 bootstrap()
 with (LOCAL/'events.jsonl').open('a',encoding='utf-8') as f:
  f.write(json.dumps({'at':now(),'kind':kind,'job':job,'detail':mask(detail)[:180]},ensure_ascii=False)+'\n')

def get(c,jid):
 row=c.execute('SELECT * FROM jobs WHERE id=?',(jid,)).fetchone()
 if not row:raise ValueError('Unbekannte Job-ID')
 return dict(row)

def transition(c,jid,new,detail=''):
 if new not in VALID:raise ValueError('Ungültiger Status')
 c.execute('BEGIN IMMEDIATE')
 try:
  old=get(c,jid)['status']
  if new not in TRANS.get(old,set()):raise ValueError(f'Unerlaubter Übergang {old} -> {new}')
  at=now();c.execute('UPDATE jobs SET status=?,updated=? WHERE id=?',(new,at,jid))
  c.execute('INSERT INTO transitions VALUES(?,?,?,?,?)',(jid,at,old,new,mask(detail)[:300]));c.execute('COMMIT')
 except Exception:c.execute('ROLLBACK');raise
 event('job-transition',jid,old+' -> '+new)

def doctor():
 print('Trendo GODMODE 2.0 – lokale Voraussetzungen')
 for binary in ['python','git','dotnet','claude','blender']:
  print(f'  {binary}: {shutil.which(binary) or "NICHT_GEFUNDEN"}')
 print('  Repository:',ROOT)
 print('  Trendo.sln:',(ROOT/'Trendo.sln').exists(),' (erst nach Kopieren in bestehendes Repo erwartet)')
 for k in ['TRENDO_UNREAL_EDITOR','TRENDO_UPROJECT']:
  print(f'  {k}: '+('GESETZT' if os.environ.get(k) else 'NICHT_GESETZT'))

def worktree(c,jid):
 j=get(c,jid)
 if j['worktree'] and pathlib.Path(j['worktree']).is_dir():return j['worktree']
 if not (ROOT/'.git').exists():raise RuntimeError('Worktree nur im tatsächlichen Git-Klon möglich')
 if not shutil.which('git'):raise RuntimeError('git fehlt')
 slug=re.sub(r'[^a-z0-9]+','-',j['title'].lower()).strip('-')[:30] or 'task'
 branch='claude/'+jid+'-'+slug
 folder=ROOT.parent/(ROOT.name+'-worktrees')/jid
 folder.parent.mkdir(exist_ok=True,parents=True)
 if folder.exists():raise RuntimeError('Worktree-Ziel existiert bereits')
 subprocess.run(['git','-C',str(ROOT),'worktree','add','-b',branch,str(folder),'HEAD'],check=True)
 c.execute('UPDATE jobs SET branch=?,worktree=?,updated=? WHERE id=?',(branch,str(folder),now(),jid))
 event('worktree-created',jid,branch)
 return str(folder)

def run(c,jid,execute=False):
 j=get(c,jid)
 if j['status'] not in {'READY','INTERRUPTED'}:
  raise ValueError('Job erst auf READY setzen (oder INTERRUPTED bewusst erneut starten)')
 if not execute:
  print('VORSCHAU: Dieser Auftrag wird NICHT gestartet. Benutze --execute für bewussten lokalen Start.');print('Job:',jid,'Titel:',j['title']);return
 if not shutil.which('claude'):raise RuntimeError('Claude Code CLI fehlt; keine Ausführung')
 verdict,why=check_start(c,j)
 if verdict=='BLOCKED':transition(c,jid,'BLOCKED',why);raise RuntimeError(why)
 if verdict=='WAIT':raise RuntimeError(why+' – Auftrag bleibt '+j['status'])
 if j['status']=='INTERRUPTED':transition(c,jid,'READY','Expliziter Wiederanlauf')
 # use the current folder only if on a non-main branch. Worktree should contain installed Claude package.
 cwd=worktree(c,jid)
 if not (pathlib.Path(cwd)/'CLAUDE.md').is_file():
  transition(c,jid,'BLOCKED','Claude-Übergabepaket noch nicht in Git-Worktree vorhanden; lokal integrieren und branch committen')
  raise RuntimeError('Worktree enthält noch keine CLAUDE.md: Paket zuerst in eigenen Branch committen')
 transition(c,jid,'RUNNING','Tatsächlicher Claude Code CLI-Aufruf')
 c.execute('UPDATE jobs SET attempt=attempt+1 WHERE id=?',(jid,))
 env=os.environ.copy();env['TRENDO_JOB_ID']=jid
 logfile=LOCAL/'run-logs';logfile.mkdir(parents=True,exist_ok=True)
 path=logfile/(jid+'-attempt-'+str(get(c,jid)['attempt'])+'.log')
 prompt=('Arbeite strikt nach CLAUDE.md und aktuellen Freigaben. Verwende isolierte Agenten nach Bedarf. '
         'Keine externen Aktionen, kein Push/Merge/Release. Aufgabe:\n'+j['prompt'])
 try:
  with path.open('w',encoding='utf-8') as fp:
   pr=subprocess.Popen(['claude','--print',prompt],cwd=cwd,env=env,stdout=fp,stderr=subprocess.STDOUT)
   c.execute('UPDATE jobs SET pid=? WHERE id=?',(pr.pid,jid))
   code=pr.wait()
  c.execute('UPDATE jobs SET last_exit=?,pid=NULL WHERE id=?',(code,jid))
  transition(c,jid,'SUCCEEDED' if code==0 else 'FAILED','Prozess beendet (Exitcode '+str(code)+'); Qualität noch NICHT extern abgenommen')
  print('CLI Exit:',code,'Privates lokales Log:',path)
 except KeyboardInterrupt:
  transition(c,jid,'INTERRUPTED','Manuell unterbrochen');raise
 except Exception as e:
  transition(c,jid,'INTERRUPTED','Prozessstart/-überwachung unterbrochen');raise


def main():
 parser=argparse.ArgumentParser(description='Trendo lokale Job-Verwaltung; kein Hintergrund-Daemon')
 sub=parser.add_subparsers(dest='action',required=True)
 sub.add_parser('bootstrap');sub.add_parser('doctor');job=sub.add_parser('job');jsub=job.add_subparsers(dest='jaction',required=True)
 create=jsub.add_parser('create');create.add_argument('--title',required=True);create.add_argument('--prompt',required=True);create.add_argument('--paths',default='',help='Erlaubte Dateien/Ordner, durch Komma getrennt')
 jsub.add_parser('recover')
 jsub.add_parser('list');status=jsub.add_parser('status');status.add_argument('id')
 setp=jsub.add_parser('set');setp.add_argument('id');setp.add_argument('new',choices=['READY','BLOCKED','CANCELLED']);setp.add_argument('--reason',default='Manuelle Statusänderung')
 wt=jsub.add_parser('worktree');wt.add_argument('id')
 ru=jsub.add_parser('run');ru.add_argument('id');ru.add_argument('--execute',action='store_true')
 args=parser.parse_args()
 if args.action=='bootstrap':bootstrap();print('Lokalen Speicher angelegt');return
 if args.action=='doctor':doctor();return
 c=connect()
 if args.jaction!='recover' and (n:=recover(c)):print(n,'abgebrochene(r) Auftrag/Aufträge auf INTERRUPTED gesetzt')
 if args.jaction=='recover':print(recover(c),'auf INTERRUPTED gesetzt');return
 if args.jaction=='create':
  jid='T-'+uuid.uuid4().hex[:10];at=now();paths=[p.strip() for p in args.paths.split(',') if p.strip()]
  c.execute('INSERT INTO jobs(id,title,prompt,status,created,updated,paths) VALUES(?,?,?,?,?,?,?)',(jid,args.title[:120],args.prompt,'QUEUED',at,at,json.dumps(paths)))
  event('job-created',jid,args.title);print(jid,'QUEUED')
 elif args.jaction=='list':
  for row in c.execute('SELECT id,title,status,attempt FROM jobs ORDER BY created DESC LIMIT 100'):
   print(row['id'],row['status'],row['attempt'],row['title'])
 elif args.jaction=='status':
  x=get(c,args.id);x['prompt']='[aus Datenschutzgründen verborgen]';print(json.dumps(x,indent=2,ensure_ascii=False))
 elif args.jaction=='set':transition(c,args.id,args.new,args.reason);print(args.new)
 elif args.jaction=='worktree':print(worktree(c,args.id))
 elif args.jaction=='run':run(c,args.id,args.execute)
if __name__=='__main__':
 try:main()
 except (ValueError,RuntimeError,subprocess.CalledProcessError) as e:
  print('FEHLER:',e,file=sys.stderr);sys.exit(2)
