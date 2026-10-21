#!/usr/bin/env python3
"""Erzeugt docs/BUILD_PLAN.md und src/Trendo.App/Assets/buildplan.json aus requirements.yaml (Status kommt immer aus requirements.yaml)."""
import json, yaml, pathlib
R = pathlib.Path(__file__).resolve().parent.parent
d = yaml.safe_load(open(R / "requirements.yaml", encoding="utf-8"))
W0 = yaml.safe_load(open(R / "docs" / "wave0.yaml", encoding="utf-8"))
W0B = yaml.safe_load(open(R / "docs" / "wave0b.yaml", encoding="utf-8"))
WAVES = [
 ("W0", "Setup & Ersteinrichtung", "von Manuel genehmigt: Assistent, Basispaket, Studio-Pakete, KI nach Hardware, Updates & Sicherung, Systemcheck",
  [x["id"] for x in W0]),
 ("W0b", "Look & Bedienung", "von Manuel genehmigt: einfache Sprache, Anpassen, Hintergründe, Animationen, Hilfe, Designs",
  [x["id"] for x in W0B]),
 ("W1", "Fundament & Alltag", "Desktop, Barrierefreiheit, Dateien, Office, Briefe, Browser – das tägliche Arbeiten zuerst",
  ["SYS-13", "CORE-01", "SYS-12", "X-09", "SYS-11", "SYS-02", "SYS-10", "STUDIO-LETTER-01", "SYS-03"]),
 ("W2", "KI & Automatisierung", "lokale KI, Agenten, Wissen, Prompts, Workflows",
  ["CORE-02", "X-05", "CORE-03", "CORE-10", "CORE-12", "SYS-05"]),
 ("W3", "System, Sicherheit & Kommunikation", "Schutz, Wiederherstellung, Apps, Geräte, Netzwerk, Mail",
  ["CORE-11", "X-02", "SYS-04", "SYS-07", "SYS-06", "SYS-09", "SPEC-12", "SYS-08"]),
 ("W4", "Kreativ & Entwicklung", "3D, Spiele, Software, Medien, Open Source, Assets, No-Code",
  ["CORE-07", "X-06", "CORE-08", "CORE-05", "CORE-06", "X-07", "X-03", "X-11"]),
 ("W5", "Business", "Online-Business-Studio, Finanzen, Studio-Fusion, Erweiterungen",
  ["STUDIO-ONLINE-BUSINESS-01", "SPEC-11", "X-01", "X-04", "X-10"]),
 ("W6", "Fach-Studios", "Spezial-Studios auf Basis der fertigen Kernbausteine",
  ["SPEC-01", "SPEC-02", "SPEC-03", "SPEC-04", "SPEC-05", "SPEC-06", "SPEC-08", "SPEC-09", "SPEC-10", "SPEC-13", "SPEC-14", "SPEC-15", "SPEC-16", "CORE-09"]),
 ("W7", "Geräte & Betriebsmodi", "Pi/PC/Android-Verbund, Mobile Bridge, Triple Mode",
  ["CORE-04", "X-08", "SYS-01"]),
]
mods = {m["id"]: m for m in d["modules"]} | {x["id"]: x for x in W0 + W0B}
approved = [m for m in d["modules"] if m["approval_status"] == "APPROVED_SPEC"]
planned = [i for w in WAVES[2:] for i in w[3]]
assert sorted(planned) == sorted(m["id"] for m in approved), set(planned) ^ {m["id"] for m in approved}
reqs = {"STUDIO-LETTER-01": d["letter_requirements"], "STUDIO-ONLINE-BUSINESS-01": d["business_requirements"]}
def accept(mid):
    a = [f"Modul im Studio-Layout erreichbar (max. 2 Navigationsebenen, deutsche Beschriftung)",
         "Kernfunktion mit echten Daten nutzbar, kein Platzhalter", "Automatische Tests (Core/UI) grün",
         "Auf dem Windows-PC verifiziert (Screenshot + Log + SHA-256 unter reports/)", "Status in requirements.yaml ehrlich gesetzt"]
    if mid in reqs: a.append(f"Alle {len(reqs[mid])} Teilanforderungen einzeln geprüft")
    return a
md = ["# Trendo OS – Bauplan (alle genehmigten Module)", "",
      "Automatisch erzeugt aus `requirements.yaml` mit `scripts/build_plan.py`. Der Status kommt ausschließlich aus requirements.yaml.",
      f"Umfang: Welle 0 ({len(W0)} Setup-Punkte, Status in docs/wave0.yaml) + {len(approved)} genehmigte Module (SPEC-07 abgelehnt), {len(d['business_requirements'])} BUS- und {len(d['letter_requirements'])} LETTER-Anforderungen.", "",
      "UI-Regeln: einheitliche Studio-Vorlage (Kopf · Werkzeugleiste · Arbeitsfläche · Seitenpanel), höchstens 2 Navigationsebenen, deutsche Beschriftung, nur echte Daten, nicht Gebautes ist als „Noch nicht gebaut“ markiert.", "",
      "Statusmodell: APPROVED_SPEC → PLANNED → IN_PROGRESS → IMPLEMENTED → TESTED → ACCEPTED.", ""]
out = []
for wid, name, why, ids in WAVES:
    md += [f"## {wid} – {name}", "", why, "", "| Reihenfolge | Modul | Name | Umsetzung | Test |", "|---|---|---|---|---|"]
    for n, i in enumerate(ids, 1):
        m = mods[i]; md.append(f"| {n} | {i} | {m['name']} | {m['implementation_status']} | {m['test_status']} |")
        out.append({"wave": wid, "waveName": name, "order": n, "id": i, "name": m["name"], "impl": m["implementation_status"], "test": m["test_status"], "local": i.startswith("W0"),
                    "subs": [{"id": r["id"], "title": r["title"], "impl": r["implementation_status"], "test": r["test_status"]} for r in reqs.get(i, [])]})
    md.append("")
    md.append("Abnahmekriterien je Modul:"); md += [f"- {x}" for x in accept(ids[0])[:5]]; md.append("")
    for i in ids:
        if i in reqs:
            md += [f"### {i}: Teilanforderungen", "", "| ID | Titel | Umsetzung | Test |", "|---|---|---|---|"]
            md += [f"| {r['id']} | {r['title']} | {r['implementation_status']} | {r['test_status']} |" for r in reqs[i]]; md.append("")
(R / "docs" / "BUILD_PLAN.md").write_text("\n".join(md) + "\n", encoding="utf-8")
(R / "docs" / "buildplan.json").write_text(json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
print(len(out), "Module,", sum(len(x["subs"]) for x in out), "Teilanforderungen")
