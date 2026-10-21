#!/usr/bin/env python3
"""Legt je Modul ein GitHub-Issue an bzw. aktualisiert Labels (wave, studio, status) aus docs/buildplan.json. Idempotent (Titel beginnt mit Modul-ID)."""
import json, subprocess, pathlib
R = pathlib.Path(__file__).resolve().parent.parent
plan = json.load(open(R / "docs" / "buildplan.json", encoding="utf-8"))
def gh(*a): return subprocess.run(["gh", *a], capture_output=True, text=True, cwd=R)
GROUP = {"W0": "Setup", "W0B": "Look", "CORE": "Kern", "SYS": "System", "SPEC": "Fach-Studio", "X": "Erweiterung", "STUDIO": "Studio"}
def status(m): return "fertig" if m["test"] == "PASS" else {"IN_PROGRESS": "in-arbeit", "IMPLEMENTED": "gebaut", "PLANNED": "geplant"}.get(m["impl"], "nicht-gebaut")
labels = {f"welle:{m['wave']}" for m in plan} | {f"studio:{GROUP[m['id'].split('-')[0]]}" for m in plan} | {f"status:{s}" for s in ["nicht-gebaut", "geplant", "in-arbeit", "gebaut", "fertig"]}
for l in sorted(labels): gh("label", "create", l, "--force", "--color", {"welle": "1D76DB", "studio": "5319E7", "status": "FBCA04"}[l.split(":")[0]])
existing = {i["title"].split(" ")[0]: i for i in json.loads(gh("issue", "list", "--state", "all", "--limit", "200", "--json", "number,title,labels").stdout or "[]")}
for m in plan:
    want = [f"welle:{m['wave']}", f"studio:{GROUP[m['id'].split('-')[0]]}", f"status:{status(m)}"]
    if m["id"] in existing:
        i = existing[m["id"]]; old = [l["name"] for l in i["labels"] if l["name"].startswith("status:") and l["name"] not in want]
        gh("issue", "edit", str(i["number"]), "--add-label", ",".join(want), *(["--remove-label", ",".join(old)] if old else []))
        if status(m) == "fertig": gh("issue", "close", str(i["number"]))
        continue
    body = [f"**Welle {m['wave']} – {m['waveName']}**, Reihenfolge {m['order']}", "", f"Status (aus requirements.yaml): Umsetzung `{m['impl']}`, Test `{m['test']}`", "",
            "Abnahme:", "- [ ] Im Studio-Layout erreichbar (max. 2 Navigationsebenen, deutsch)", "- [ ] Kernfunktion mit echten Daten, kein Platzhalter",
            "- [ ] Automatische Tests grün", "- [ ] Auf dem Windows-PC verifiziert (Screenshot, Log, SHA-256 unter reports/)", "- [ ] requirements.yaml ehrlich aktualisiert"]
    if m["subs"]: body += ["", "Teilanforderungen:"] + [f"- [ ] {s['id']} {s['title']}" for s in m["subs"]]
    body += ["", "Plan: docs/BUILD_PLAN.md"]
    r = gh("issue", "create", "--title", f"{m['id']} {m['name']}", "--body", "\n".join(body), "--label", ",".join(want))
    print(m["id"], r.stdout.strip() or r.stderr.strip())
