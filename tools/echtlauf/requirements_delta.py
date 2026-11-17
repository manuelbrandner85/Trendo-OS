#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""T-0687: Vorschlag (nur Vorschlag!) für Statuswechsel in requirements.yaml. Ändert requirements.yaml NICHT.

Für jedes Modul mit test_status PC_VERIFIED oder TESTED:
- Datum des Belegs (letzter Commit der Belegdatei) gegen das Datum der letzten Änderung am Code des Studios
  (Quelldateien der Programme laut docs/programme/katalog.json) – Beleg älter → Rückstufung vorschlagen.
- Studio aus der Liste entfernt (StudioCatalog.Removed) → kein gültiger Prüfstand.
- Programme des Studios ohne einen Test der Schicht „echt“ (siehe reports/echtlauf/testschichten.md) → Hinweis.
Für Module mit NOT_RUN: Hochstufung nur vorschlagen, wenn ein frischer Echtlauf-Beleg (reports/echtlauf/<datum>/) das Werkzeug des Studios ECHT-GRÜN zeigt.

Aufruf: python tools/echtlauf/requirements_delta.py [--echtlauf reports/echtlauf/<datum>] [--ziel <datei.md>]
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).parent))
import testschichten  # noqa: E402

# Werkzeug der Echtlauf-Station → Studios, deren Kernfunktion davon abhängt
TOOL_STUDIOS = {"ffmpeg": ["CORE-08"], "godot": ["CORE-06"], "blender": ["CORE-05"], "libreoffice": ["SYS-10"], "tesseract": ["STUDIO-LETTER-01"],
                "sdcli": ["STUDIO-IMAGEAI-01"], "acestep": ["STUDIO-MUSIK-01"], "ollama": ["CORE-02"], "claude": [], "chrome": ["CORE-07"]}


def git_date(paths: list[str]) -> str | None:
    paths = [p for p in paths if (REPO / p).exists()]
    if not paths:
        return None
    r = subprocess.run(["git", "log", "-1", "--format=%cs", "--", *paths], cwd=REPO, capture_output=True, text=True, encoding="utf-8")
    return r.stdout.strip() or None


def modules() -> list[dict]:
    out, cur, in_mod = [], None, False
    for line in (REPO / "requirements.yaml").read_text(encoding="utf-8").splitlines():
        if line.startswith("modules:"):
            in_mod = True; continue
        if in_mod and re.match(r"^\S", line) and not line.startswith("- "):
            in_mod = False
        if not in_mod:
            continue
        m = re.match(r"^- id: (\S+)", line)
        if m:
            cur = {"id": m.group(1)}; out.append(cur); continue
        m = re.match(r"^  (\w+): (.*)$", line)
        if cur is not None and m:
            cur.setdefault(m.group(1), m.group(2).strip().strip("'\""))
    return out


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser()
    ap.add_argument("--echtlauf", help="Beleg-Ordner der Echtlauf-Station")
    ap.add_argument("--ziel", default=str(REPO / "docs" / "audit" / "2026-11-16-profi-audit" / "requirements-delta.md"))
    a = ap.parse_args()

    cat = json.loads((REPO / "docs" / "programme" / "katalog.json").read_text(encoding="utf-8"))
    removed = set(re.search(r"Removed = new HashSet<string>\([^)]*\) \{([^}]*)\}", (REPO / "src" / "Trendo.App" / "StudioCatalog.cs").read_text(encoding="utf-8")).group(1).replace('"', "").replace(" ", "").split(","))
    by_studio: dict[str, list[dict]] = {}
    for e in cat:
        by_studio.setdefault(e["Studio"], []).append(e)

    # Testschichten je Programm
    layers: dict[str, dict[str, int]] = {}
    by_name: dict[str, list[str]] = {}
    for e in cat:
        for n in e.get("Tests", []):
            by_name.setdefault(n, []).append(e["Id"])
    for _proj, _f, name, attr, body, head in testschichten.tests():
        layer, _h, _e = testschichten.classify(attr, body, head)
        for p in by_name.get(name, []):
            layers.setdefault(p, {}).setdefault(layer, 0)
            layers[p][layer] += 1

    run = None
    if a.echtlauf:
        d = Path(a.echtlauf)
        run = {x["werkzeug"]: x for x in json.loads((d / "ergebnis.json").read_text(encoding="utf-8"))}
        run_rel = d.relative_to(REPO).as_posix() if d.is_absolute() and d.is_relative_to(REPO) else d.as_posix()

    down, keep, up, info = [], [], [], []
    for m in modules():
        sid, ts, impl, ev = m["id"], m.get("test_status", ""), m.get("implementation_status", ""), m.get("evidence", "")
        progs = by_studio.get(sid, [])
        src = sorted({f for p in progs for f in p.get("SourceFiles", [])})
        code_date = git_date(src)
        ev_paths = [x for x in re.findall(r"(reports/[^\s,;)]+)", ev)]
        ev_date = git_date(ev_paths)
        real = sum(layers.get(p["Id"], {}).get("echt", 0) for p in progs)
        repl = sum(layers.get(p["Id"], {}).get("ersetzt", 0) for p in progs)
        if ts in ("PC_VERIFIED", "TESTED"):
            reasons = []
            if sid in removed:
                reasons.append("Studio ist aus der Liste entfernt (StudioCatalog.Removed) – nicht mehr angeboten")
            if not ev_paths:
                reasons.append("kein Belegpfad unter reports/")
            elif ev_date and code_date and ev_date < code_date:
                reasons.append(f"Beleg vom {ev_date}, Code des Studios zuletzt geändert am {code_date}")
            if progs and real == 0:
                reasons.append(f"kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: {repl})")
            row = (sid, m.get("name", ""), f"{impl} / {ts}", ev, "; ".join(reasons))
            (down if reasons and (sid in removed or (ev_date and code_date and ev_date < code_date) or not ev_paths) else keep if not reasons else info).append(row)
        elif run:
            for tool, studios in TOOL_STUDIOS.items():
                if sid in studios and tool in run and run[tool]["ergebnis"].startswith("ECHT-GRÜN"):
                    up.append((sid, m.get("name", ""), f"{impl} / {ts}", f"{run_rel}/{tool}.log", run[tool]["ergebnis"]))

    L = ["# Vorschlag: Statuswechsel in requirements.yaml (T-0687)", "",
         "**Nur ein Vorschlag – `requirements.yaml` wurde nicht geändert.** Die Entscheidung trifft der Nutzer. "
         "Erzeugt mit `python tools/echtlauf/requirements_delta.py" + (f" --echtlauf {run_rel}" if run else "") + "`.", "",
         "Regel (CLAUDE.md): `TESTED`/`PC_VERIFIED` nur mit Rohlog + SHA-256 unter `reports/`. Ein Beleg, der älter ist als die letzte Änderung am Code "
         "des Studios, deckt den heutigen Stand nicht. Ein Test mit ersetztem Werkzeug (`*Override`) ist kein Ende-zu-Ende-Nachweis.", "",
         "Datum „Code geändert“ = letzter Commit an den Quelldateien der Programme des Studios laut `docs/programme/katalog.json`; "
         "Datum „Beleg“ = letzter Commit der Belegdatei.", "",
         f"## Zurückstufen vorgeschlagen ({len(down)})", "",
         "Vorschlag: `test_status` → `NOT_RUN` (bzw. bei entfernten Studios Eintrag als entfernt kennzeichnen), bis ein neuer Beleg vorliegt.", "",
         "| Modul | Name | heute | Beleg | Grund |", "|---|---|---|---|---|"]
    L += [f"| {r[0]} | {r[1]} | {r[2]} | `{r[3]}` | {r[4]} |" for r in down]
    L += ["", f"## Beibehalten, aber mit Hinweis ({len(info)})", "", "Beleg ist nicht älter als der Code, aber die Programme haben keinen Test mit echtem Werkzeug.", "",
          "| Modul | Name | heute | Beleg | Hinweis |", "|---|---|---|---|---|"]
    L += [f"| {r[0]} | {r[1]} | {r[2]} | `{r[3]}` | {r[4]} |" for r in info]
    L += ["", f"## Beibehalten ohne Befund ({len(keep)})", "", ", ".join(r[0] for r in keep) or "–", ""]
    L += [f"## Hochstufen denkbar ({len(up)})", "",
          "Nur wo die Echtlauf-Station das Werkzeug des Studios heute ECHT-GRÜN belegt hat (Rohlog + SHA256SUMS im Beleg-Ordner). "
          "Vorschlag: `test_status` → `PC_VERIFIED` für den belegten Fachlauf – `implementation_status` bleibt, wie er ist.", "",
          "| Modul | Name | heute | Beleg | Ergebnis |", "|---|---|---|---|---|"]
    L += [f"| {r[0]} | {r[1]} | {r[2]} | `{r[3]}` | {r[4]} |" for r in up]
    L += ["", "## Weitere Befunde (von Hand geprüft, 16.11.2026)", "",
          "- **Kaputte Umlaute:** `requirements.yaml` Zeilen 538, 540 und 545 auf dem Zweig `claude/T-0674-buero-etappe` (Audit-Stand) enthalten "
          "„Arbeiten & BÃ¼ro“, „â€žJa, weiterâ€œ“ und „Formulare ausfÃ¼llen“ (UTF-8 doppelt kodiert). Auf dem Integrationszweig `claude/T-0685-b1-integration` "
          "sind diese Zeilen noch nicht vorhanden – beim Zusammenführen der Büro-Etappe reparieren („Büro“, „„Ja, weiter““, „ausfüllen“).",
          "- **IMP-42 (Prompt-to-Action)** steht auf `IN_PROGRESS / NOT_RUN`, obwohl `PromptActionTests` (88 Tests) am 16.11.2026 grün liefen. "
          "Hochstufen auf `TESTED` erst mit Rohlog + SHA-256 unter `reports/` (z. B. TRX-Datei des Laufs); diese Station legt dafür keinen Beleg an.",
          "- **CORE-06** (Spiele) bleibt in der Zurückstufungsliste, obwohl Godot, Unity und Node heute ECHT-GRÜN liefen: Unreal (Kernversprechen des Studios) "
          "ist nicht automatisch belegt. Teil-Hochstufung nur, wenn der Nutzer den Godot-/Unity-Beleg für ausreichend hält.",
          "", "## Grenzen dieses Vorschlags", "",
          "- „Code geändert“ zählt jeden Commit an den Quelldateien – auch rein optische. Der Nutzer entscheidet je Eintrag, ob die Änderung den Beleg entwertet.",
          "- „kein Test mit echtem Fremdwerkzeug“ beruht auf der Zuordnung Test → Programm im Programmkatalog. Tests, deren Anzeigename weder die Studio-Kennung "
          "noch die Seite nennt (z. B. „LETTER-012: Echte Tesseract-Texterkennung“ für STUDIO-LETTER-01), fehlen dort – der Hinweis kann deshalb zu streng sein.",
          "- Der Echtlauf-Beleg zeigt einen Fachlauf je Werkzeug, nicht jede Funktion des Studios."]
    Path(a.ziel).parent.mkdir(parents=True, exist_ok=True)
    Path(a.ziel).write_text("\n".join(L) + "\n", encoding="utf-8")
    print(f"zurück {len(down)}, Hinweis {len(info)}, ohne Befund {len(keep)}, hoch {len(up)} → {a.ziel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
