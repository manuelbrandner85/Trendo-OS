#!/usr/bin/env python3
"""Prüft requirements.yaml gegen MODULE_REGISTER und Requirement-CSVs. Exit 1 bei Fehlern."""
import csv, pathlib, sys, yaml
R = pathlib.Path(__file__).resolve().parent.parent; S = R / "docs" / "spec"; err = []
def ids(p, k="requirement_id"): return [r[k] for r in csv.DictReader(open(p, encoding="utf-8"))]
try: d = yaml.safe_load(open(R / "requirements.yaml", encoding="utf-8"))
except Exception as e: print("FAIL: YAML nicht lesbar:", e); sys.exit(1)
print("OK  requirements.yaml parst")
allowed = set(d["status_model"]) | {"REJECTED_UPGRADE_BASE_PRESERVED", "NOT_STARTED", "NOT_RUN", "TESTED_BOX", "PC_VERIFIED"}
for sec, src, n in [("modules", S/"MODULE_REGISTER.csv", 54), ("business_requirements", S/"studios/BUSINESS_REQUIREMENTS_30.csv", 30),
                    ("letter_requirements", S/"studios/LETTER_REQUIREMENTS_25.csv", 25)]:
    y = [m["id"] for m in d[sec]]; c = ids(src)
    if y != c: err.append(f"{sec}: IDs weichen von {src.name} ab")
    if len(set(y)) != len(y): err.append(f"{sec}: doppelte IDs")
    if len(y) != n: err.append(f"{sec}: erwartet {n}, gefunden {len(y)}")
    for m in d[sec]:
        for k in ("approval_status", "implementation_status", "test_status"):
            if m[k] not in allowed: err.append(f"{m['id']}: unbekannter Status {m[k]}")
        if m["implementation_status"] != "NOT_STARTED" or m["test_status"] != "NOT_RUN":
            ev = m.get("evidence")
            if ev and (R / ev).is_file(): continue  # Fortschritt nur mit Nachweis-Datei im Repo
            err.append(f"{m['id']}: Fortschritt ohne Nachweis ({m['implementation_status']}/{m['test_status']})")
    print(f"OK  {sec}: {len(y)} IDs geprüft" if not any(e.startswith(sec) for e in err) else f"FAIL {sec}")
# Verbesserungen 13–97: IDs wie Quelle, Entscheidung wie decisions.yaml, Abgelehntes/Offenes nie begonnen, Fortschritt nur mit Nachweis
imps = d.get("improvements", []); src = ids(S / "IMPROVEMENTS.csv")
if [m["id"] for m in imps] != src or [m["id"] for m in imps] != [f"IMP-{n}" for n in range(13, 98)]: err.append("improvements: IDs weichen von IMPROVEMENTS.csv bzw. IMP-13…97 ab")
dec = {}; cur = None
for line in open(S / "decisions.yaml", encoding="utf-8"):
    s = line.strip()
    if s.startswith("- id:"): cur = s[5:].strip()
    elif cur and s.startswith("decision:"): dec[cur] = s[9:].strip()
for m in imps + d.get("improvement_functions", []):
    for k in ("implementation_status", "test_status"):
        if m[k] not in allowed: err.append(f"{m['id']}: unbekannter Status {m[k]}")
    parent = m.get("improvement", m["id"])
    if "decision" in m and dec.get(m["id"]) != m["decision"]: err.append(f"{m['id']}: Entscheidung {m['decision']} ≠ decisions.yaml {dec.get(m['id'])}")
    if dec.get(parent) in ("REJECTED", "UNDECIDED") and m["implementation_status"] != "NOT_STARTED": err.append(f"{m['id']}: abgelehnt/offen, aber begonnen")
    if (m["implementation_status"] != "NOT_STARTED" or m["test_status"] != "NOT_RUN") and not (m.get("evidence") and (R / m["evidence"]).is_file()):
        err.append(f"{m['id']}: Fortschritt ohne Nachweis")
fn = d.get("improvement_functions", [])
if [m["id"] for m in fn] != ids(S / "IMPROVEMENT_FUNCTIONS.csv"): err.append("improvement_functions: IDs weichen von IMPROVEMENT_FUNCTIONS.csv ab")
if not any(e.startswith(("improvement", "IMP-")) for e in err): print(f"OK  improvements: {len(imps)} Verbesserungen, {len(fn)} Einzelfunktionen geprüft")
rej = [m["id"] for m in d["modules"] if m["approval_status"] != "APPROVED_SPEC"]
if rej != ["SPEC-07"]: err.append(f"Abgelehnt erwartet nur SPEC-07, gefunden {rej}")
else: print("OK  SPEC-07 bleibt REJECTED_UPGRADE_BASE_PRESERVED, alle anderen APPROVED_SPEC")
bad = [t["id"] for t in d["tests"] if t["status"] != "NOT_RUN"]
if bad: err.append(f"Tests nicht NOT_RUN: {bad}")
else: print(f"OK  {len(d['tests'])} Tests stehen auf NOT_RUN")
ph = {x["id"]: x for x in d.get("phases", [])}
if ph.get("P1", {}).get("status") == "ACCEPTED":
    if not ph["P1"].get("accepted_by") or not ph["P1"].get("open_items"): err.append("P1 ACCEPTED ohne accepted_by/open_items")
    else: print(f"OK  P1 ACCEPTED durch {ph['P1']['accepted_by']}, {len(ph['P1']['open_items'])} offene Punkte dokumentiert")
for x in ph.values():
    if x["status"] not in allowed: err.append(f"Phase {x['id']}: unbekannter Status")
for e in err: print("FAIL", e)
print("ERGEBNIS:", "FEHLER" if err else "BESTANDEN (nur Spezifikationskonsistenz, kein Software-Test)")
sys.exit(1 if err else 0)
