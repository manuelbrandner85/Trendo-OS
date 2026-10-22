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
# Fortschritt mit Nachweis (evidence) aus der bestehenden Datei übernehmen, damit Neugenerieren nichts zurücksetzt
try:
    old = yaml.safe_load(open(R / "requirements.yaml", encoding="utf-8")) or {}
    keep = {m["id"]: m for sec in ("modules", "business_requirements", "letter_requirements") for m in old.get(sec, []) if m.get("evidence")}
except FileNotFoundError: keep = {}
for m in mods + bus + let:
    if m["id"] in keep: m.update({k: keep[m["id"]][k] for k in ("implementation_status", "test_status", "evidence")})
tests = []
for f, key in [("QA_MATRIX_GROK", "GROK_QA_MATRIX.csv"), ("BUSINESS", "BUSINESS_QA_MATRIX.csv"), ("LETTER", "LETTER_QA_MATRIX.csv")]:
    for r in rows(S / "qa" / key):
        tests.append({"id": r["test_id"], "catalog": key, "status": r.get("status") or r.get("initial_status")})
# Grundsätze (von Hand gepflegt, hier im Generator, damit Neugenerieren sie nicht löscht)
principles = [{
    "id": "GRUNDSATZ-01",
    "title": "Betriebssystem für jeden: jedes Fachprogramm und Tool ist installierbar",
    "source": "Manuel, übermittelt von TRENDO OS MASTER (2026-10-23)",
    "wording": "Trendo OS soll ein Betriebssystem für jeden werden, also alles ist an den Nutzer anpassbar. "
               "Fachprogramme oder Tools im besten Fall schon bei der Installation von Trendo OS mitinstallieren "
               "und ansonsten innerhalb von Trendo OS installierbar. Es darf kein Programm oder Tool geben, wo dies nicht möglich ist.",
    "approval_status": "APPROVED_SPEC",
    "note": "Wortlaut von Manuel übermittelt; Manuel muss den Eintrag und die Abnahmekriterien noch bestätigen.",
    "acceptance_criteria_proposed": [
        "AK-1: Jedes Programm oder Tool, das ein Studio braucht, steht im Abhängigkeitskatalog (src/Trendo.Core/Dependencies.cs). Ein Test schlägt fehl, wenn ein Studio ein Programm nutzt, das dort fehlt.",
        "AK-2: Für jedes Programm ist festgelegt, wie es zum Nutzer kommt: im Setup enthalten, beim Setup zur Auswahl angeboten oder später in Trendo OS mit einem Klick installierbar.",
        "AK-3: Kein Programm hat den Zustand ‚nicht installierbar‘. Wo der Hersteller keine automatische Installation erlaubt (z. B. Konto nötig), führt Trendo OS den Nutzer Schritt für Schritt zur offiziellen Installation und erkennt das Programm danach selbst.",
        "AK-4: Installiert wird nur nach Zustimmung des Nutzers, nur aus offiziellen Quellen (winget oder Herstellerseite) und mit Eintrag im Audit-Log. Kein heimlicher Auto-Download.",
        "AK-5: Lizenz, Kosten und Hürden (z. B. Konto-Pflicht) werden vor der Installation in leicht verständlichem Deutsch angezeigt.",
        "AK-6: Nach der Installation prüft Trendo OS, ob das Programm wirklich startet, und zeigt das Ergebnis an.",
        "AK-7: Der Nutzer kann jedes optionale Programm wieder entfernen oder abwählen (alles ist anpassbar).",
        "AK-8: Nachweis auf einem echten Windows-PC: Installation aus Trendo OS heraus mit Log für mindestens ein Programm je Liefer-Art.",
    ],
}]
doc = {"schema_version": 1, "source": "Trendo OS Grok Production Package v2.1 (2026-10-21)",
       "note": "APPROVED_SPEC bedeutet genehmigte Spezifikation, NICHT gebaut. Alle Tests NOT_RUN bis echte Logs existieren.",
       "status_model": ["PROPOSED", "APPROVED_SPEC", "PLANNED", "IN_PROGRESS", "IMPLEMENTED", "TESTED", "ACCEPTED", "RELEASED", "FAILED", "BLOCKED"],
       "editions": ["windows-app", "linux-usb-ssd", "pi4-plus-windows"],
       "phases": [
           {"id": "P0", "name": "Repository & Requirements Lock", "status": "ACCEPTED", "note": "Spezifikation importiert, ADRs, Inventur (PR #1, #2)"},
           {"id": "P1", "name": "Windows Kern-Vertical-Slice", "status": "ACCEPTED",
            "accepted_by": "Manuel", "accepted_at": "2026-10-21T14:57+02:00", "evidence": "reports/P1_TESTBERICHT.md",
            "open_items": ["QA-017 Narrator/Kontrast/Multi-Monitor NOT_RUN", "QA-019 realer Offline-Lauf NOT_RUN", "Code-Signatur fehlt"]},
           {"id": "P2", "name": "Linux USB/SSD Boot & Ready-to-Run Essentials", "status": "IN_PROGRESS"},
           {"id": "P3", "name": "Pi/Windows Bridge", "status": "APPROVED_SPEC"},
           {"id": "P4", "name": "Funktionsfamilien", "status": "APPROVED_SPEC"},
           {"id": "P5", "name": "Security, Integration, Recovery", "status": "APPROVED_SPEC"},
           {"id": "P6", "name": "Release Candidates", "status": "APPROVED_SPEC"}],
       "principles": principles, "modules": mods, "business_requirements": bus, "letter_requirements": let, "tests": tests}
hdr ="# AUTOMATISCH ERZEUGT durch scripts/generate_requirements.py – nicht von Hand Status hochsetzen.\n"
(R / "requirements.yaml").write_text(hdr + yaml.safe_dump(doc, allow_unicode=True, sort_keys=False, width=120), encoding="utf-8")
print(f"requirements.yaml: {len(mods)} Module, {len(bus)} BUS, {len(let)} LETTER, {len(tests)} Tests")
