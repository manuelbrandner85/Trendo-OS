"""Unreal CLI adapter. Only run explicitly with installed UnrealEditor-Cmd and genuine .uproject."""
import argparse,subprocess,pathlib,json,os,sys,datetime
p=argparse.ArgumentParser();p.add_argument('--editor',required=True);p.add_argument('--uproject',required=True);p.add_argument('--report',default='.trendo-local/unreal_report.json');p.add_argument('--dry-run',action='store_true');a=p.parse_args()
editor=pathlib.Path(a.editor).expanduser().resolve();uproject=pathlib.Path(a.uproject).expanduser().resolve()
if not editor.is_file() or not uproject.is_file() or uproject.suffix.lower()!='.uproject':sys.exit('BLOCKED: tatsächlicher Editor/uproject-Pfad fehlt')
cmd=[str(editor),str(uproject),'-unattended','-nop4','-nosplash','-ExecCmds=Automation RunTests Project; Quit']
if a.dry_run:print(json.dumps({'command':cmd,'status':'NOT_RUN'},indent=2));sys.exit(0)
# No internet, publishing or equipment action. Just local automation test.
try:cp=subprocess.run(cmd,capture_output=True,text=True,timeout=1800,check=False)
except subprocess.TimeoutExpired:sys.exit('BLOCKED: Unreal-Test Timeout')
raw=(cp.stdout or '')+'\n'+(cp.stderr or '')
report={'at':datetime.datetime.now(datetime.timezone.utc).isoformat(),'exit_code':cp.returncode,'status':'TEST_EXECUTED_NOT_VERIFIED','engine':str(editor),'uproject':str(uproject),'log_excerpt':raw[-3500:]}
# Even code 0 may mean no tests ran: never mark VERIFIED without a parsed test report.
reportfile=pathlib.Path(a.report);reportfile.parent.mkdir(parents=True,exist_ok=True);reportfile.write_text(json.dumps(report,indent=2),encoding='utf-8');print('UNREAL-AUTOMATION-AUFRUF:',report['status'],'Exit:',cp.returncode,'Report:',reportfile)
sys.exit(cp.returncode)
