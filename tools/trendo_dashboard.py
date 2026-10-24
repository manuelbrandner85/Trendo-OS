#!/usr/bin/env python3
"""Read-only local dashboard; never controls Claude or host processes."""
import json, pathlib, http.server, urllib.parse, sqlite3
ROOT=pathlib.Path(__file__).resolve().parents[1]
LOCAL=ROOT/'.trendo-local'
PAGE=r"""<!DOCTYPE html><html lang="de"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Trendo GODMODE – Status</title><style>
:root{color-scheme:dark}body{font:15px system-ui,Segoe UI,sans-serif;background:#0b1322;color:#e5edf7;max-width:1100px;margin:0 auto;padding:24px}h1{font-size:28px}p{color:#a8b8cc}.panel{background:#152137;border:1px solid #2c415d;border-radius:14px;padding:18px;margin:18px 0}table{width:100%;border-collapse:collapse}th,td{border-bottom:1px solid #2d4059;text-align:left;padding:9px;font-size:13px}th{color:#96aec8}.dot{color:#80b9ee}small{color:#a8b8cc}code{color:#9ac7fa}</style></head><body><h1>Trendo <span class="dot">Agent Command Center</span></h1><p>Lokale, ausschließlich lesende Ansicht. Nur tatsächlich erfasste Hook- und Job-Ereignisse. Kein simuliertes Live-Tracking.</p><div class="panel"><h2>Persistente Jobs</h2><table><thead><tr><th>ID</th><th>Titel</th><th>Status</th><th>Versuche</th></tr></thead><tbody id="jobs"></tbody></table></div><div class="panel"><h2>Letzte Ereignisse</h2><table><thead><tr><th>Zeit (UTC)</th><th>Art</th><th>Tool</th><th>Agent / Job</th></tr></thead><tbody id="events"></tbody></table></div><small id="updated">Noch keine Daten</small><script>
function cell(tr,s){let d=document.createElement('td');d.textContent=String(s??'');tr.appendChild(d)}function table(id,rows,keys){let b=document.getElementById(id);b.replaceChildren();for(let r of rows){let tr=document.createElement('tr');for(let k of keys)cell(tr,r[k]);b.appendChild(tr)}}async function refresh(){try{let r=await fetch('/api/state',{cache:'no-store'});if(!r.ok)throw Error(r.status);let d=await r.json();table('jobs',d.jobs,['id','title','status','attempt']);table('events',d.events,['at','kind','tool','who']);document.getElementById('updated').textContent='Daten gelesen: '+new Date().toLocaleTimeString('de-DE')}catch(e){document.getElementById('updated').textContent='Kein Zugriff auf lokale Daten: '+e.message}}refresh();setInterval(refresh,2500)
</script></body></html>"""
class Handler(http.server.BaseHTTPRequestHandler):
 def log_message(self,fmt,*args):pass
 def do_GET(self):
  route=urllib.parse.urlparse(self.path).path
  if route=='/':
   raw=PAGE.encode('utf-8');mime='text/html; charset=utf-8'
  elif route=='/api/state':
   jobs=[]
   path=LOCAL/'jobs.sqlite3'
   if path.is_file():
    try:
     with sqlite3.connect('file:'+str(path)+'?mode=ro',uri=True,timeout=2) as c:
      jobs=[dict(zip(['id','title','status','attempt'],r)) for r in c.execute('SELECT id,title,status,attempt FROM jobs ORDER BY updated DESC LIMIT 100')]
    except (sqlite3.Error,OSError):pass
   events=[]
   ev=LOCAL/'events.jsonl'
   if ev.is_file():
    # Max 2 MiB tail; no raw commands are recorded by hooks.
    with ev.open('rb') as f:
     f.seek(0,2);end=f.tell();f.seek(max(0,end-2_000_000));data=f.read().decode('utf-8','replace')
    for line in data.splitlines()[-150:]:
     try:
      d=json.loads(line);events.append({'at':d.get('at',''),'kind':d.get('kind',''),'tool':d.get('tool',''),'who':d.get('agent') or d.get('job','')})
     except json.JSONDecodeError:pass
   raw=json.dumps({'jobs':jobs,'events':events[::-1]},ensure_ascii=False).encode();mime='application/json; charset=utf-8'
  else:self.send_error(404);return
  self.send_response(200);self.send_header('Content-Type',mime);self.send_header('Cache-Control','no-store');self.send_header('X-Content-Type-Options','nosniff');self.send_header('Content-Security-Policy',"default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; connect-src 'self'");self.send_header('Content-Length',str(len(raw)));self.end_headers();self.wfile.write(raw)
if __name__=='__main__':
 print('Nur lokal: http://127.0.0.1:8765 | Strg+C beendet');http.server.ThreadingHTTPServer(('127.0.0.1',8765),Handler).serve_forever()
