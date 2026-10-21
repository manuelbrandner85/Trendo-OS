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
