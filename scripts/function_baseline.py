#!/usr/bin/env python3
"""Funktions-Baseline: erfasst aus dem Quellcode jede benannte Funktion der Oberfläche.

Erfasst werden Befehle, Exporte, Werkzeuge, Knöpfe, Felder und Auswahllisten mit festem Namen.
Zur Laufzeit zusammengesetzte Namen (zum Beispiel je Level ein Knopf) sind nicht enthalten und werden gezählt.
Aufruf: python scripts/function_baseline.py  schreibt docs/STUDIO_REBUILD_4_0/baseline/funktionen.csv
"""
import csv
import glob
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "docs", "STUDIO_REBUILD_4_0", "baseline", "funktionen.csv")
COLUMNS = ["funktions_id", "art", "name", "bezeichnung", "studios", "aufrufstelle", "eingabe", "ausgabe", "abhaengigkeiten", "bedienbarkeit", "teststatus"]

N = r'"([a-z0-9][a-z0-9-]*)"'
T = r'"((?:[^"\\]|\\.)*)"'
PATTERNS = [
    ("befehl", re.compile(r"new ProgramCmd\(" + N + r",\s*" + T)),
    ("export", re.compile(r"new ProgramExport\(" + N + r",\s*" + T + r",\s*" + T)),
    ("werkzeug", re.compile(r"new ProgramTool\(" + N + r',\s*"[^"]*",\s*' + T)),
    ("knopf", re.compile(r"\b(?:Act|RBtn|Btn|B)\(" + N + r",\s*" + T)),
    ("knopf", re.compile(r"new Button \{ Name = " + N + r",\s*Content = " + T)),
    ("feld", re.compile(r"new TextBox \{ Name = " + N + r"()")),
    ("feld", re.compile(r"\bField\(" + N + r",\s*" + T)),
    ("auswahl", re.compile(r"new ComboBox \{ Name = " + N + r"()")),
]
DYNAMIC = re.compile(r'Name = \$"|\bB\(\$"|\bAct\(\$"')


def read(path):
    with io.open(path, encoding="utf-8") as f:
        return f.read()


def studio_files():
    mapping = {}
    for spec in glob.glob(os.path.join(ROOT, "docs", "STUDIO_REBUILD_4_0", "studios", "*", "SPEZIFIKATION.md")):
        studio = os.path.basename(os.path.dirname(spec))
        section = read(spec).split("## Quellcode")[-1]
        for name in re.findall(r"`([^`]+\.cs)`", section):
            mapping.setdefault(name.replace("\\", "/"), set()).add(studio)
    return mapping


def tested_names():
    names = set()
    for path in glob.glob(os.path.join(ROOT, "tests", "**", "*.cs"), recursive=True):
        for s in re.findall(r'"([a-z0-9][a-z0-9-]*)"', read(path)):
            names.add(s)
            if s.startswith("prog-cmd-"):
                names.add(s[len("prog-cmd-"):])
    return names


def scan():
    mapping, tested = studio_files(), tested_names()
    rows, dynamic = {}, 0
    for path in sorted(glob.glob(os.path.join(ROOT, "src", "Trendo.App", "**", "*.cs"), recursive=True)):
        if os.sep + "obj" + os.sep in path or os.sep + "bin" + os.sep in path:
            continue
        rel = os.path.relpath(path, ROOT).replace(os.sep, "/")
        text = read(path)
        dynamic += len(DYNAMIC.findall(text))
        for kind, rx in PATTERNS:
            for m in rx.finditer(text):
                name, label = m.group(1), m.group(2)
                key = (kind, name)
                if key in rows:
                    continue
                line = text.count("\n", 0, m.start()) + 1
                output = "Datei " + m.group(3) if kind == "export" else ""
                rows[key] = {
                    "funktions_id": f"FN-{kind}-{name}", "art": kind, "name": name, "bezeichnung": label,
                    "studios": " ".join(sorted(mapping.get(rel, []))) or "gemeinsam oder nicht zugeordnet",
                    "aufrufstelle": f"{rel}:{line}", "eingabe": "Klick" if kind in ("knopf", "befehl", "werkzeug") else "Wert" if kind in ("feld", "auswahl") else "offenes Dokument",
                    "ausgabe": output or "offen, nicht statisch ableitbar", "abhaengigkeiten": "offen, nicht statisch ableitbar",
                    "bedienbarkeit": "über Befehle und Claude erreichbar" if kind in ("befehl", "export", "werkzeug") else "auf der Seite",
                    "teststatus": "Test nennt den Namen" if name in tested else "kein Test nennt den Namen",
                }
    return sorted(rows.values(), key=lambda r: r["funktions_id"]), dynamic


def main():
    rows, dynamic = scan()
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with io.open(OUT, "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, COLUMNS, lineterminator="\n")
        w.writeheader()
        w.writerows(rows)
    with_test = sum(1 for r in rows if r["teststatus"].startswith("Test"))
    assigned = sum(1 for r in rows if not r["studios"].startswith("gemeinsam"))
    sys.stdout.write(f"{len(rows)} Funktionen, {with_test} mit Test, {assigned} einem Studio zugeordnet, {dynamic} Stellen mit Namen zur Laufzeit\n")


if __name__ == "__main__":
    main()
