#!/usr/bin/env python3
"""Erzeugt requirements.yaml aus den Spezifikations-CSVs (docs/spec). Nur Statuswerte aus der Quelle; nichts wird als gebaut markiert."""
import csv, pathlib, yaml
R = pathlib.Path(__file__).resolve().parent.parent
S = R / "docs" / "spec"
def rows(p): return list(csv.DictReader(open(p, encoding="utf-8")))
mods = [{"id": r["requirement_id"], "name": r["module_name"],
         "approved_expansion_functions": r["approved_expansion_functions"],
         "approval_status": r["status"],
         "implementation_status": "NOT_STARTED", "test_status": "NOT_RUN"}
        for r in rows(S / "MODULE_REGISTER.csv")]
bus = [{"id": r["requirement_id"], "module": "STUDIO-ONLINE-BUSINESS-01", "mode": r["mode"], "title": r["title"],
        "approval_status": r["approval_status"], "implementation_status": r["implementation_status"],
        "test_status": r["test_status"]} for r in rows(S / "studios/BUSINESS_REQUIREMENTS_30.csv")]
let = [{"id": r["requirement_id"], "module": "STUDIO-LETTER-01", "title": r["feature_name"],
        "approval_status": r["status"], "implementation_status": r["implementation_status"],
        "test_status": r["test_status"]} for r in rows(S / "studios/LETTER_REQUIREMENTS_25.csv")]
tests = []
for f, key in [("QA_MATRIX_GROK", "GROK_QA_MATRIX.csv"), ("BUSINESS", "BUSINESS_QA_MATRIX.csv"), ("LETTER", "LETTER_QA_MATRIX.csv")]:
    for r in rows(S / "qa" / key):
        tests.append({"id": r["test_id"], "catalog": key, "status": r.get("status") or r.get("initial_status")})
doc = {"schema_version": 1, "source": "Trendo OS Grok Production Package v2.1 (2026-10-21)",
       "note": "APPROVED_SPEC bedeutet genehmigte Spezifikation, NICHT gebaut. Alle Tests NOT_RUN bis echte Logs existieren.",
       "status_model": ["PROPOSED", "APPROVED_SPEC", "PLANNED", "IN_PROGRESS", "IMPLEMENTED", "TESTED", "ACCEPTED", "RELEASED", "FAILED", "BLOCKED"],
       "editions": ["windows-app", "linux-usb-ssd", "pi4-plus-windows"],
       "modules": mods, "business_requirements": bus, "letter_requirements": let, "tests": tests}
hdr = "# AUTOMATISCH ERZEUGT durch scripts/generate_requirements.py – nicht von Hand Status hochsetzen.\n"
(R / "requirements.yaml").write_text(hdr + yaml.safe_dump(doc, allow_unicode=True, sort_keys=False, width=120), encoding="utf-8")
print(f"requirements.yaml: {len(mods)} Module, {len(bus)} BUS, {len(let)} LETTER, {len(tests)} Tests")
