#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""T-0687: Testschichten sichtbar machen. Liest den Quelltext aller Tests und die Liste der Ersatz-Haken (*Override in src/) und schreibt
reports/echtlauf/testschichten.md: je Programm, welche Tests nur Trendos eigenen Code prüfen, welche ein Werkzeug ersetzen, welche ein echtes
Fremdwerkzeug brauchen (und ohne es übersprungen werden), welche nur auf Anforderung laufen und welche fest übersprungen sind.

Aufruf: python tools/echtlauf/testschichten.py [--ziel reports/echtlauf/testschichten.md]
Rein statisch (kein Testlauf). Was auf diesem PC wirklich lief, steht im Beleg der Echtlauf-Station (reports/echtlauf/<datum>/).
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]

HOOK_RX = re.compile(r"\bpublic\s+[^;={}\n]*?\b(\w+Override)\s*\{")
NAME_RX = re.compile(r'DisplayName\s*=\s*"((?:[^"\\]|\\.)*)"|\[(?:Avalonia)?(?:Fact|Theory)\("((?:[^"\\]|\\.)*)"')
ATTR_RX = re.compile(r"\[(\w*(?:Fact|Theory))\b")
ASSIGN_RX = re.compile(r"\b(\w+Override)\s*=\s*(?!null\b|=)")
# Fremdwerkzeug im Test: eigene Attribute, die ohne das Programm überspringen, oder Suche nach dem Programm im Test
TOOL_ATTRS = {"NeedsFact", "BlenderFact", "FfmpegFact", "AssimpFact", "TypstFact", "MeltFact", "IfcFact", "Open3dFact", "PmFact", "VecFact", "ValidatorFact",
              "TransformFact", "SewingFact", "AxeFact", "ResticFact", "YrFact", "YoloxFact", "PytiledFact", "ReferenzFact", "IconFact", "MdFact", "ToolFact",
              "PciFact", "RulesFact", "NeedsLanFact"}
OS_ATTRS = {"WinFact", "LinuxFact"}
TOOL_RX = re.compile(r'Cli\.Find\("|EchtWerkzeuge\.|NeedsFactAttribute\.Tool\(|FindBrowser\(|ModelConverter\.FindBlender|PdfBrowser\(|Cli\.Run\("(?:node|python|git|ffmpeg|dotnet)"')
ENV_RX = re.compile(r'GetEnvironmentVariable\("(TRENDO_[A-Z0-9_]+)"\)')
SKIP_RX = re.compile(r'Skip\s*=\s*"')
REAL_SKIP_RX = re.compile(r"Ueberspringen\.Weil|Assert\.Skip(?:Unless|When)?\(")

LAYERS = ["intern", "ersetzt", "echt", "anforderung", "fest-uebersprungen"]
LABEL = {"intern": "nur Trendos Code", "ersetzt": "Werkzeug ersetzt (*Override)", "echt": "echtes Fremdwerkzeug (sonst übersprungen)",
         "anforderung": "nur auf Anforderung (Umgebungsvariable)", "fest-uebersprungen": "fest übersprungen"}


def sources(root: Path):
    for f in sorted(root.rglob("*.cs")):
        if any(p in ("obj", "bin") for p in f.relative_to(root).parts):
            continue
        yield f


def hooks() -> dict[str, str]:
    out = {}
    for f in sources(REPO / "src"):
        t = f.read_text(encoding="utf-8", errors="replace")
        for m in HOOK_RX.finditer(t):
            line = t.count("\n", 0, m.start()) + 1
            out.setdefault(m.group(1), f"{f.relative_to(REPO).as_posix()}:{line}")
    return out


def tests():
    """(Projekt, Datei, Anzeigename, Attribut, Abschnitt, Kopf der Datei) je Test."""
    for proj in ("Trendo.Core.Tests", "Trendo.App.Tests", "Trendo.Api.Tests"):
        root = REPO / "tests" / proj
        for f in sources(root):
            t = f.read_text(encoding="utf-8", errors="replace")
            ms = list(NAME_RX.finditer(t))
            if not ms:
                continue
            head = t[: ms[0].start()]
            for i, m in enumerate(ms):
                # Abschnitt: vom Attribut dieses Tests bis zum nächsten Test
                start = t.rfind("[", 0, m.start())
                end = ms[i + 1].start() if i + 1 < len(ms) else len(t)
                attr_m = ATTR_RX.search(t, start, m.end())
                yield proj, f.relative_to(REPO).as_posix(), (m.group(1) or m.group(2) or ""), attr_m.group(1) if attr_m else "", t[start:end], head


def classify(attr: str, body: str, head: str) -> tuple[str, list[str], list[str]]:
    hooks_used = sorted({h for h in ASSIGN_RX.findall(body + "\n" + head)})
    envs = sorted(set(ENV_RX.findall(body)))
    first_line = body.split("\n", 1)[0]
    if SKIP_RX.search(first_line) and "SkipUnless" not in first_line and "SkipWhen" not in first_line:
        return "fest-uebersprungen", hooks_used, envs
    if envs and REAL_SKIP_RX.search(body):
        return "anforderung", hooks_used, envs
    if attr in TOOL_ATTRS or TOOL_RX.search(body):
        return ("ersetzt" if hooks_used and attr not in TOOL_ATTRS else "echt"), hooks_used, envs
    if hooks_used:
        return "ersetzt", hooks_used, envs
    return "intern", hooks_used, envs


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser()
    ap.add_argument("--ziel", default=str(REPO / "reports" / "echtlauf" / "testschichten.md"))
    a = ap.parse_args()

    hk = hooks()
    cat = json.loads((REPO / "docs" / "programme" / "katalog.json").read_text(encoding="utf-8"))
    by_name = defaultdict(list)
    for e in cat:
        for n in e.get("Tests", []):
            by_name[n].append(e["Id"])
    progname = {e["Id"]: (e["Name"], e["Studio"]) for e in cat}

    rows = []
    hook_use = Counter()
    for proj, f, name, attr, body, head in tests():
        layer, used, envs = classify(attr, body, head)
        for h in used:
            hook_use[h] += 1
        rows.append({"projekt": proj, "datei": f, "name": name, "schicht": layer, "haken": used, "env": envs, "attr": attr,
                     "os": attr in OS_ATTRS, "programme": by_name.get(name, [])})

    total = Counter(r["schicht"] for r in rows)
    per_prog = defaultdict(Counter)
    prog_hooks = defaultdict(Counter)
    for r in rows:
        for p in r["programme"] or ["(ohne Programmzuordnung)"]:
            per_prog[p][r["schicht"]] += 1
            for h in r["haken"]:
                prog_hooks[p][h] += 1

    L = ["# Testschichten – was „getestet“ wirklich heißt", "",
         "Erzeugt von `python tools/echtlauf/testschichten.py` aus dem Quelltext der Tests (keine Handarbeit, kein Testlauf). "
         "Was auf diesem PC wirklich mit dem Fremdwerkzeug lief, belegt die Echtlauf-Station unter `reports/echtlauf/<datum>/`.", "",
         "## Schichten", "",
         "| Schicht | Bedeutung | Tests |", "|---|---|---|",
         f"| intern | prüft nur Trendos eigenen Code (Rechnen, Dateiformate, Oberfläche ohne Fremdprogramm) | {total['intern']} |",
         f"| ersetzt | mindestens ein Ersatz-Haken (`*Override`) ist gesetzt – das eigentliche Werkzeug (Claude, FFmpeg-Lauf, Netz, Installation …) läuft **nicht**; kein Ende-zu-Ende-Nachweis | {total['ersetzt']} |",
         f"| echt | braucht ein echtes Fremdwerkzeug (Attribut wie `NeedsFact`/`BlenderFact` oder Suche im Test); fehlt es, wird der Test sichtbar übersprungen | {total['echt']} |",
         f"| anforderung | läuft nur mit gesetzter Umgebungsvariable (z. B. `TRENDO_PC_LAUF`, `TRENDO_ACE_LIVE`), sonst übersprungen | {total['anforderung']} |",
         f"| fest-uebersprungen | `Skip = \"…\"` ohne Bedingung | {total['fest-uebersprungen']} |",
         f"| **gesamt** | | **{len(rows)}** |", "",
         f"Ersatz-Haken im Code: **{len(hk)}** (`public … *Override {{ get; set; }}` in `src/`); in Tests gesetzt: {sum(1 for h in hk if hook_use[h])}; "
         f"Zuweisungen in Tests (je Test gezählt): {sum(hook_use.values())}.", "",
         "Grenzen der Auswertung: Ein Test zählt als „ersetzt“, wenn in seinem Abschnitt oder im Kopf der Testdatei ein Haken gesetzt wird (Hilfsmethoden "
         "zwischen zwei Tests werden dem vorherigen Test zugeschlagen). „echt“ heißt: der Test benutzt das Werkzeug, wenn es da ist – ob es auf einem "
         "bestimmten PC lief, steht nur im Lauf-Beleg. Die Zuordnung zu Programmen kommt aus `docs/programme/katalog.json` (Feld `Tests`).", "",
         "## Je Programm", "",
         "| Programm | Studio | Tests | intern | ersetzt | echt | Anforderung | fest übersprungen | häufigste Haken |", "|---|---|---|---|---|---|---|---|---|"]
    def key(p):
        return (progname.get(p, ("~", "~"))[1], progname.get(p, ("~", p))[0])
    for p in sorted(per_prog, key=key):
        c = per_prog[p]; n = sum(c.values())
        name, studio = progname.get(p, (p, "–"))
        top = ", ".join(f"`{h}` ×{k}" for h, k in prog_hooks[p].most_common(3)) or "–"
        L.append(f"| {name} (`{p}`) | {studio} | {n} | {c['intern']} | {c['ersetzt']} | {c['echt']} | {c['anforderung']} | {c['fest-uebersprungen']} | {top} |")
    L += ["", "## Programme ohne einen einzigen Test der Schicht „echt“", ""]
    none_real = [p for p in sorted(per_prog, key=key) if p in progname and per_prog[p]["echt"] == 0]
    L.append(", ".join(f"{progname[p][0]} (`{p}`)" for p in none_real) or "–")
    L += ["", "## Ersatz-Haken nach Nutzung in Tests", "", "| Haken | Ort | Tests, die ihn setzen |", "|---|---|---|"]
    for h, where in sorted(hk.items(), key=lambda x: (-hook_use[x[0]], x[0])):
        L.append(f"| `{h}` | `{where}` | {hook_use[h]} |")
    L += ["", "## Tests der Schichten „echt“, „anforderung“ und „fest-uebersprungen“", "", "| Schicht | Test | Datei | Bedingung |", "|---|---|---|---|"]
    for r in sorted((r for r in rows if r["schicht"] in ("echt", "anforderung", "fest-uebersprungen")), key=lambda r: (LAYERS.index(r["schicht"]), r["datei"], r["name"])):
        cond = ", ".join(r["env"]) if r["env"] else (r["attr"] if r["attr"] in TOOL_ATTRS else "Suche im Test")
        L.append(f"| {r['schicht']} | {r['name'].replace('|', '/')} | `{r['datei']}` | {cond} |")
    Path(a.ziel).parent.mkdir(parents=True, exist_ok=True)
    Path(a.ziel).write_text("\n".join(L) + "\n", encoding="utf-8")
    print(f"{len(rows)} Tests, {len(hk)} Haken → {a.ziel}")
    print(" ".join(f"{k}={total[k]}" for k in LAYERS))
    return 0


if __name__ == "__main__":
    sys.exit(main())
