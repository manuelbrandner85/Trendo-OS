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
# Verbesserungen 13–97 (Entscheidung aus decisions.yaml/Freigabe-Delta) und ihre Einzelfunktionen, soweit Funktionslisten vorliegen
imps = [{"id": r["requirement_id"], "title": r["title"], "approved_functions": int(r["approved_functions"]), "decision": r["decision"],
         "implementation_status": r["implementation_status"], "test_status": r["test_status"],
         **({"evidence": r["evidence"]} if r["evidence"] else {}), **({"note": r["note"]} if r["note"] else {})}
        for r in rows(S / "IMPROVEMENTS.csv")]
funcs = [{"id": r["requirement_id"], "improvement": r["improvement"], "title": r["title"], "requirement": r["requirement"],
          "implementation_status": r["implementation_status"], "test_status": r["test_status"],
          **({"evidence": r["evidence"]} if r["evidence"] else {}), **({"note": r["note"]} if r["note"] else {})}
         for r in rows(S / "IMPROVEMENT_FUNCTIONS.csv")]
# Fortschritt mit Nachweis (evidence) aus der bestehenden Datei übernehmen, damit Neugenerieren nichts zurücksetzt
try:
    old = yaml.safe_load(open(R / "requirements.yaml", encoding="utf-8")) or {}
    keep = {m["id"]: m for sec in ("modules", "business_requirements", "letter_requirements", "improvements", "improvement_functions") for m in old.get(sec, []) if m.get("evidence")}
except FileNotFoundError: keep = {}
for m in mods + bus + let + imps + funcs:
    if m["id"] in keep: m.update({k: keep[m["id"]][k] for k in ("implementation_status", "test_status", "evidence")})
tests = []
for f, key in [("BUSINESS", "BUSINESS_QA_MATRIX.csv"), ("LETTER", "LETTER_QA_MATRIX.csv")]:
    for r in rows(S / "qa" / key):
        tests.append({"id": r["test_id"], "catalog": key, "status": r.get("status") or r.get("initial_status")})
# Grundsätze (von Hand gepflegt, hier im Generator, damit Neugenerieren sie nicht löscht)
packages = [
 {
  "id": "PAKET-44",
  "title": "Fachprogramme nur bei Bedarf (Universal Professional Studio Fusion)",
  "source": "MASTER_BIS_94.md Zeile 86; 10 Funktionen von Manuel am 2026-10-23 freigegeben; Umsetzung aus PR #271",
  "approval_status": "APPROVED_SPEC",
  "functions": [
   {
    "id": "P44-01",
    "name": "Programmkatalog",
    "for_user": "Zeigt, welche Fachprogramme TRENDO kennt.",
    "acceptance": "Die Seite „Externe Programme“ listet alle Programme aus dem Katalog.",
    "approval_status": "APPROVED_SPEC"
   },
   {
    "id": "P44-02",
    "name": "Nicht vorladen",
    "for_user": "Programme starten nicht beim Hochfahren.",
    "acceptance": "Nach dem Start steht überall „Nicht geladen“, kein Programm läuft im Hintergrund.",
    "approval_status": "APPROVED_SPEC"
   },
   {
    "id": "P44-03",
    "name": "Erkennen",
    "for_user": "Findet ein schon installiertes Programm.",
    "acceptance": "Ein installiertes Programm wird als „gefunden, nicht geprüft“ angezeigt.",
    "approval_status": "APPROVED_SPEC"
   },
   {
    "id": "P44-04",
    "name": "Prüfen auf Knopfdruck",
    "for_user": "Kurzer Test, ob das Programm funktioniert.",
    "acceptance": "Nur „Verbindung prüfen“ startet die Prüfung; Ergebnis ist bestanden oder fehlgeschlagen.",
    "approval_status": "APPROVED_SPEC"
   },
   {
    "id": "P44-05",
    "name": "Öffnen nur mit Freigabe",
    "for_user": "Programm öffnet erst nach bestandener Prüfung und Freigabe.",
    "acceptance": "Ohne Prüfung und Freigabe öffnet nichts.",
    "approval_status": "APPROVED_SPEC"
   },
   {
    "id": "P44-06",
    "name": "Alle Wege über dieselbe Prüfung",
    "for_user": "Auch alte Knöpfe sowie 3D- und Spielestudio nutzen dieselbe Prüfung.",
    "acceptance": "„Starten“, „Einrichten“ und die Wege aus #251/#253 öffnen nichts ohne Prüfung.",
    "approval_status": "APPROVED_SPEC"
   },
   {
    "id": "P44-07",
    "name": "Kein automatischer Download",
    "for_user": "Es wird nie heimlich oder automatisch etwas heruntergeladen.",
    "acceptance": "Offline, beim Start und bei fehlgeschlagener Prüfung wird nichts geladen; Download nur nach ausdrücklicher Zustimmung (siehe P44-08).",
    "approval_status": "APPROVED_SPEC"
   },
   {
    "id": "P44-08",
    "name": "Fehlendes Programm per Knopf installieren",
    "for_user": "Installiert ein fehlendes Programm per Knopf mit Rückfrage aus offizieller kostenloser Quelle.",
    "acceptance": "Nach Klick erscheint eine Rückfrage mit Quelle, Lizenz und Größe; erst nach Ja wird aus der offiziellen kostenlosen Quelle (z. B. winget) installiert und das Programm danach erkannt.",
    "approval_status": "APPROVED_SPEC"
   },
   {
    "id": "P44-09",
    "name": "Geheimnisse schützen",
    "for_user": "Schlüssel nur bei Bedarf aus dem Secret Store, nie im Protokoll.",
    "acceptance": "Ein Test zeigt, dass kein geheimer Wert im Protokoll steht.",
    "approval_status": "APPROVED_SPEC"
   },
   {
    "id": "P44-10",
    "name": "Echter Start auf dem PC",
    "for_user": "Blender, Unreal und LibreOffice laufen auf Windows wirklich.",
    "acceptance": "Je ein dokumentierter Lauf auf Manuels PC mit Beleg.",
    "approval_status": "APPROVED_SPEC"
   }
  ]
 }
]

principles = [{
    "id": "GRUNDSATZ-01",
    "title": "Betriebssystem für jeden: jedes Fachprogramm und Tool ist installierbar",
    "source": "Manuel (Wortlaut 2026-10-23), mit den Abnahmekriterien im Chat bestätigt am 2026-10-25",
    "wording": "Trendo OS soll ein Betriebssystem für jeden werden, also alles ist an den Nutzer anpassbar. "
               "Fachprogramme oder Tools im besten Fall schon bei der Installation von Trendo OS mitinstallieren "
               "und ansonsten innerhalb von Trendo OS installierbar. Es darf kein Programm oder Tool geben, wo dies nicht möglich ist.",
    "approval_status": "APPROVED_SPEC",
    "note": "Eintrag und Abnahmekriterien von Manuel am 2026-10-25 bestätigt. APPROVED_SPEC heißt nicht umgesetzt.",
    "acceptance_criteria": [
        "AK-1: Jedes Programm oder Tool, das ein Studio braucht, steht im Abhängigkeitskatalog (src/Trendo.Core/Dependencies.cs). Ein Test schlägt fehl, wenn ein Studio ein Programm nutzt, das dort fehlt.",
        "AK-2: Für jedes Programm ist festgelegt, wie es zum Nutzer kommt: im Setup enthalten, beim Setup zur Auswahl angeboten oder später in Trendo OS mit einem Klick installierbar.",
        "AK-3: Kein Programm hat den Zustand ‚nicht installierbar‘. Wo der Hersteller keine automatische Installation erlaubt (z. B. Konto nötig), führt Trendo OS den Nutzer Schritt für Schritt zur offiziellen Installation und erkennt das Programm danach selbst.",
        "AK-4: Installiert wird nur nach Zustimmung des Nutzers, nur aus offiziellen Quellen (winget oder Herstellerseite) und mit Eintrag im Audit-Log. Kein heimlicher Auto-Download.",
        "AK-5: Lizenz, Kosten und Hürden (z. B. Konto-Pflicht) werden vor der Installation in leicht verständlichem Deutsch angezeigt.",
        "AK-6: Nach der Installation prüft Trendo OS, ob das Programm wirklich startet, und zeigt das Ergebnis an.",
        "AK-7: Der Nutzer kann jedes optionale Programm wieder entfernen oder abwählen (alles ist anpassbar).",
        "AK-8: Nachweis auf einem echten Windows-PC: Installation aus Trendo OS heraus mit Log für mindestens ein Programm je Liefer-Art.",
    ],    "amendments": [{
        "date": "2026-10-25",
        "source": "Manuel im Chat (Auswahl „Mitliefern + nachbauen“, Reihenfolge „Office zuerst“, Programmliste T-0222 „alles gut“)",
        "wording": "Studio-Programme werden nicht heruntergeladen, sondern mit Trendo OS mitinstalliert (Vollpaket, ohne Internet, ohne Admin). Parallel baut Trendo eigene Programme nach, zuerst Office (Text, Tabelle, PDF); ein eigenes Programm ersetzt das Original im Studio, sobald es reicht. Nur Programme, deren Lizenz die Weitergabe verbietet (Unreal, Chrome, Claude Code, Qwen2.5 3B), bleiben verlinkt; Meshroom und Tor vorerst ebenfalls.",
        "task": "tasks/T-0222-programme-mitliefern.md",
    }],
}]
doc = {"schema_version": 1, "source": "Trendo OS Production Package v2.1 (2026-10-21)",
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
       "principles": principles, "packages": packages, "modules": mods, "business_requirements": bus, "letter_requirements": let,
       "improvements": imps, "improvement_functions": funcs, "tests": tests}
hdr = "# AUTOMATISCH ERZEUGT durch scripts/generate_requirements.py – nicht von Hand Status hochsetzen.\n"
(R / "requirements.yaml").write_text(hdr + yaml.safe_dump(doc, allow_unicode=True, sort_keys=False, width=120), encoding="utf-8")
print(f"requirements.yaml: {len(mods)} Module, {len(bus)} BUS, {len(let)} LETTER, {len(imps)} Verbesserungen, {len(funcs)} Einzelfunktionen, {len(tests)} Tests")
