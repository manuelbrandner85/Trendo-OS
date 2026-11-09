#!/usr/bin/env python3
"""Erzeugt die Studio-Funktionsmatrix aus dem Repository (read-only, schreibt nur die Matrix).

Stufen je Studio:
  1 nur spezifiziert    Umsetzung nicht begonnen
  2 teilweise           in Arbeit, offene Katalogfunktionen oder Ampel rot
  3 funktionsfähig      gebaut, aber ohne belegten Testlauf oder ohne zugeordnete Tests
  4 getestet            gebaut, Testbeleg und zugeordnete Tests vorhanden
  5 abgenommen          ACCEPTED oder RELEASED mit Abnahmeprotokoll
"""
import glob
import io
import os
import re
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASE = os.path.join(ROOT, "docs", "STUDIO_REBUILD_4_0")


def read(path):
    with io.open(path, encoding="utf-8") as f:
        return f.read()


def inventory():
    rows = []
    for line in read(os.path.join(BASE, "TRENDO_STUDIO_INVENTORY_4.0.md")).splitlines():
        m = re.match(r"\| \[([A-Z0-9-]+)\]\(studios/", line)
        if not m:
            continue
        c = [x.strip() for x in line.strip("|").split("|")]
        rows.append({"id": m.group(1), "name": c[1], "area": c[2], "programs": c[3], "funcs": c[4], "open": c[5],
                     "tests": c[6], "spec": c[7], "impl": c[8], "test": c[9], "light": c[10], "risk": c[12]})
    return rows


def programs_of(studio):
    path = os.path.join(BASE, "studios", studio, "SPEZIFIKATION.md")
    return re.findall(r"^- \*\*(.+?)\*\* \(`([^`]+)`\)", read(path), re.M)


def evidence():
    text = read(os.path.join(ROOT, "requirements.yaml"))
    part = text[text.index("\nmodules:"):text.index("\nbusiness_requirements:")]
    found = {}
    for item in re.split(r"\n- id: ", part)[1:]:
        m = re.search(r"\n\s*evidence: *(.*)", item)
        found[item.split("\n")[0].strip()] = m.group(1).strip() if m else ""
    return found


def capability():
    text = read(os.path.join(ROOT, "src", "Trendo.Core", "PromptAction", "CapabilityCatalog.cs"))
    out = {}
    for m in re.finditer(r'P\("([^"]+)", "[^"]*", "[^"]*", "[^"]*", "([^"]*)", "[^"]*", "[^"]*", "([^"]*)"', text):
        out[m.group(1)] = (m.group(2), m.group(3))
    return out


def document_studios():
    found = set()
    for path in glob.glob(os.path.join(ROOT, "src", "Trendo.App", "*.cs")):
        found.update(re.findall(r'new ProgramSpec\([^,]+, "([A-Z0-9-]+)"', read(path)))
    return found


def level(r):
    if r["impl"] == "NOT_STARTED":
        return 1, "Umsetzung nicht begonnen" + (", Erweiterung abgelehnt" if r["spec"].startswith("REJECTED") else "")
    if r["impl"] in ("ACCEPTED", "RELEASED"):
        return 5, "Abnahme eingetragen"
    if r["impl"] == "IN_PROGRESS":
        return 2, "laut requirements.yaml in Arbeit"
    if r["open"] not in ("0", "") or r["light"] == "rot":
        return 2, f"{r['open']} Katalogfunktionen offen" if r["open"] != "0" else "Ampel rot"
    if r["test"] != "PC_VERIFIED" or not r["tests"].isdigit() or int(r["tests"]) == 0:
        return 3, "gebaut, kein belegter Testlauf"
    return 4, "gebaut und getestet, keine Abnahme"


NAMES = {1: "nur spezifiziert", 2: "teilweise", 3: "funktionsfähig", 4: "getestet", 5: "abgenommen"}


def main():
    rows = inventory()
    ev, cap, docs = evidence(), capability(), document_studios()
    counts = Counter(level(r)[0] for r in rows)
    same = Counter(r["tests"] for r in rows if r["tests"].isdigit())
    shared = sorted(n for n, c in same.items() if c >= 4)
    out = ["# Trendo Studio-Funktionsmatrix", "",
           "> Aus dem Repository erzeugt mit `scripts/studio_matrix.py`. Keine Einschätzung ohne Beleg.", "",
           f"{len(rows)} Studios, " + ", ".join(f"{counts[k]} {NAMES[k]}" for k in sorted(NAMES)) + ".", "",
           "## Was die Stufen bedeuten", "",
           "| Stufe | Bedeutung |", "|---|---|",
           "| 1 nur spezifiziert | Umsetzung nicht begonnen |",
           "| 2 teilweise | in Arbeit, offene Katalogfunktionen oder Ampel rot |",
           "| 3 funktionsfähig | gebaut, aber kein belegter Testlauf oder keine zugeordneten Tests |",
           "| 4 getestet | gebaut, Testbeleg und zugeordnete Tests vorhanden, keine Abnahme |",
           "| 5 abgenommen | ACCEPTED oder RELEASED mit Abnahmeprotokoll |", "",
           "## Grenzen dieser Matrix", "",
           "- Kein Studio ist abgenommen, weil kein Abnahmeprotokoll existiert.",
           f"- Die Testzahlen sind grob: Die Werte {', '.join(map(str, shared))} kommen bei mindestens vier Studios gleich vor. Das sind gemeinsame Tests, keine eigenen je Studio.",
           "- Die Belege in `requirements.yaml` stammen teils aus alten Versionen (zum Beispiel `reports/pc-0.9.2`).",
           "- Die Ampel prüft, ob etwas vorhanden ist, nicht wie gut es ist.",
           "- Die Spalte „Grenze der Auftragsausführung“ stammt aus `CapabilityCatalog` und beschreibt den Prompt-Weg, nicht das Programm selbst.",
           "- Einen Nutzertest mit Anfängern gab es bisher nicht.", "",
           "## Claude-Zugang", "",
           "- **voll:** Dokumentprogramm. Claude erreicht alle Befehle, Vorlagen, Exporte und jede Stelle der Daten.",
           "- **Seite:** Claude liest die Seite, füllt Felder, wählt und klickt. Interne Daten ohne sichtbares Element erreicht es nicht.", "",
           "## Matrix", "",
           "| Studio | Name | Bereich | Unterprogramme | Stufe | Grund | Spez | Umsetzung | Test | Katalog (offen) | Tests | Beleg | Claude | Grenze der Auftragsausführung |",
           "|---|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for r in rows:
        n, why = level(r)
        subs = ", ".join(f"{a} (`{b}`)" for a, b in programs_of(r["id"])) or "–"
        limit = cap.get(r["id"], ("", "nicht im Katalog"))[1].replace("|", "/")
        access = "voll" if r["id"] in docs else ("–" if n == 1 else "Seite")
        out.append(f"| {r['id']} | {r['name']} | {r['area']} | {subs} | {n} {NAMES[n]} | {why} | {r['spec']} | {r['impl']} | {r['test']} | {r['funcs']} ({r['open']}) | {r['tests']} | {ev.get(r['id'], '') or '–'} | {access} | {limit} |")
    with io.open(os.path.join(BASE, "TRENDO_STUDIO_FUNKTIONSMATRIX.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out) + "\n")
    print({NAMES[k]: counts[k] for k in sorted(NAMES)}, "voll:", sum(1 for r in rows if r["id"] in docs))


if __name__ == "__main__":
    main()
