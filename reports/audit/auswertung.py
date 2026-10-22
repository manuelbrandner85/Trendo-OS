import csv,collections,re,sys
rows=[r for r in csv.reader(open('result.tsv'),delimiter='\t') if len(r)>=5][1:]
seen={}
for r in rows:
    page,name,label,en,eff=r[:5]; det=r[5] if len(r)>5 else ''
    if re.match(r'(win-(min|max|close)|.*-tab-|side-|quick-|dock-|top-|start-)',name): continue
    key=name if not name.startswith('#') else page+name
    if key in seen: continue
    seen[key]=(page,label,eff,det)
def cls(eff,det):
    if eff in('DISABLED','SKIPPED_RISKY','NOT_REFOUND'): return eff
    if eff=='NONE': return 'DEAD'
    if eff=='CRASH': return 'CRASH'
    if re.search(r'fehlt|nicht installiert|Einrichten|Zusatzprogramm',det): return 'NEEDS_TOOL'
    if eff in('UI','FILE,UI') and det=='' : return 'UI_ONLY'
    return 'WORKS'
c=collections.Counter(); bypage=collections.defaultdict(collections.Counter)
dead=[]
for k,(p,l,e,d) in seen.items():
    x=cls(e,d); c[x]+=1; bypage[p][x]+=1
    if x in('DEAD','CRASH'): dead.append((p,k,l,e,d))
print(len(seen),c)
open('dead.tsv','w').write('\n'.join('\t'.join(t) for t in dead))
open('bypage.tsv','w').write('\n'.join(f"{p}\t"+' '.join(f'{a}={b}' for a,b in v.items()) for p,v in sorted(bypage.items())))
