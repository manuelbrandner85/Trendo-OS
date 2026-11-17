#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""T-0687 Echtlauf-Station: ein Befehl, der auf diesem PC jedes externe Werkzeug so findet, wie Trendo es findet, und je Werkzeug
einen echten kleinen Fachlauf über Trendos eigenen Code fährt.

Aufruf (im Repository, nach `dotnet build Trendo.sln -c Release`):
    python tools/echtlauf/echtlauf.py                 # alle Werkzeuge, Beleg unter reports/echtlauf/<datum>/
    python tools/echtlauf/echtlauf.py --nur godot ffmpeg
    python tools/echtlauf/echtlauf.py --langsam       # zusätzlich Unity (EditMode-Tests) und Unreal (C++-Spiel kompilieren, testen, starten; dauert)
    python tools/echtlauf/echtlauf.py --bauen         # vorher Trendo.sln und den Helfer bauen

Ergebnis je Werkzeug: ECHT-GRÜN (alle Schritte liefen mit dem echten Werkzeug und bestanden), ROT (ein Schritt schlug fehl) oder
BLOCKIERT (Werkzeug fehlt, Voraussetzung fehlt oder Schritt wurde übersprungen – mit Grund). Es wird nichts installiert, nichts geladen,
nie angemeldet; gestartet werden nur eigene Prozesse.

Bausteine:
- Suchen und eigene Fachläufe: tools/echtlauf/Trendo.Echtlauf (C#, nutzt Trendo.Core/Trendo.App: DependencyManager, EngineTargets, ClaudeCode,
  KiLibraryStore, AceStepClient, Archive7z …).
- Vorhandene Echt-Tests werden eingebunden statt neu gebaut (Core über `dotnet test`, App über den xUnit-Runner), Ergebnis aus der TRX-Datei.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import re
import socket
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
HELPER_DIR = REPO / "tools" / "echtlauf" / "Trendo.Echtlauf"
HELPER = HELPER_DIR / "bin" / "Release" / "net10.0" / "Trendo.Echtlauf.dll"
CORE_TESTS = REPO / "tests" / "Trendo.Core.Tests"
APP_TESTS_EXE = REPO / "tests" / "Trendo.App.Tests" / "bin" / "Release" / "net10.0" / ("Trendo.App.Tests.exe" if os.name == "nt" else "Trendo.App.Tests")

GRUEN, ROT, BLOCK = "ECHT-GRÜN", "ROT", "BLOCKIERT"

# Werkzeug → Schritte. ("helfer", name) = eigener Fachlauf im Helfer; ("core"|"app", Teil des DisplayName) = vorhandener Echt-Test;
# ("core-attr", Attributname) = alle Core-Tests mit diesem Echtwerkzeug-Attribut; ("blockiert", Grund) = bewusst nicht automatisiert.
PLAN: list[tuple[str, str, list[tuple[str, str]]]] = [
    ("ffmpeg", "Videoschnitt-Export mit echtem FFmpeg", [("core", "Videoschnitt-Export mit echtem FFmpeg"), ("core", "Echter Export mit FFmpeg (Software-Encoder)")]),
    ("node", "Browser-Spiele: erzeugtes JavaScript mit node --check", [("core", "CORE-06: Alle Browser-Spiele werden angelegt"), ("core", "CORE-06: Gebautes Spiel")]),
    ("git", "Software-Studio: Git-Stand anlegen und speichern", [("core", "CORE-07: Git-Stand anlegen")]),
    ("python", "Python-Vorlage und alle KI-Python-Umgebungen", [("helfer", "python"), ("core", "CORE-07: Python- und .NET-Vorlage")]),
    ("chrome", "Webseite als PDF und Editor-Bündel in echtem Chrome", [("app", "SYS-03-P14: Seite als PDF"), ("app", "K1 und K2: Das echte Editor-Bündel in echtem Chrome")]),
    ("blender", "glTF/OBJ/STL/FBX über Blender, Kreatur mit Rig, Rundlauf FBX/GLB", [("core", "CORE-05: Echtes Blender"), ("core", "Kreatur-Baukasten: echter Blender"), ("core", "3D S2-17: echter Rundlauf")]),
    ("unreal", "Unreal: C++-Spiel anlegen, kompilieren, Unreal-Tests, Startlauf", [("helfer-langsam", "unreal")]),
    ("unity", "Unity-Projekt anlegen, EditMode-Tests", [("helfer-langsam", "unity")]),
    ("godot", "Godot-Projekt anlegen, importieren, prüfen, starten, Gegenprobe", [("helfer", "godot")]),
    ("libreoffice", "LibreOffice öffnet Trendos DOCX/XLSX/PPTX und wandelt nach PDF", [
        ("core", "LibreOffice öffnet ein von Trendo erzeugtes DOCX"), ("core", "DOCX real mit LibreOffice zu PDF und ODT"),
        ("core", "Tabelle C1: Abgleich mit LibreOffice Calc"), ("core", "T-0229 P6: Abgleich mit LibreOffice Impress"),
        ("core", "T-0230: Abgleich mit LibreOffice Writer"), ("core", "Seitenzahl und A4 eines echten LibreOffice-PDFs")]),
    ("tesseract", "Tesseract liest einen von Trendo gerenderten Brief", [("core", "LETTER-012: Echte Tesseract-Texterkennung"), ("core", "Scan → durchsuchbares PDF mit Tesseract")]),
    ("sdcli", "Bild-KI erzeugt ein Bild (PNG geprüft)", [("helfer", "sdcli")]),
    ("acestep", "Musik-KI erzeugt 10 s Lied", [("helfer", "acestep")]),
    ("ollama", "Lokale KI beantwortet eine Rechenfrage", [("helfer", "ollama")]),
    ("claude", "Claude Code beantwortet eine Frage über das Abo (nur wenn angemeldet)", [("helfer", "claude")]),
    ("7zip", "7-Zip packt, Trendos Entpacker packt aus, byte-gleich", [("helfer", "7zip")]),
    ("weitere", "Weitere Echtwerkzeuge der Testsuite (assimp, typst, melt, IfcOpenShell, Open3D, glTF-Prüfer …)", [
        ("core-attr", a) for a in ["AssimpFact", "TypstFact", "MeltFact", "IfcFact", "Open3dFact", "PmFact", "VecFact", "ValidatorFact", "TransformFact",
                                   "SewingFact", "AxeFact", "ResticFact", "YrFact", "YoloxFact", "PytiledFact", "ReferenzFact", "IconFact", "MdFact", "ToolFact"]]),
]

ANSI = re.compile(r"\x1b\[[0-9;?]*[A-Za-z]")


def sanitize(text: str) -> str:
    """Benutzer- und Rechnernamen aus Rohlogs entfernen (Regel: C:\\Users\\<user>, <pc>)."""
    text = ANSI.sub("", text)
    home = os.environ.get("USERPROFILE") or str(Path.home())
    user = Path(home).name
    variants = {home, home.replace("\\", "/"), home.replace("\\", "\\\\")}
    if re.match(r"^[A-Za-z]:", home):
        variants.add("/" + home[0].lower() + home[2:].replace("\\", "/"))
    for v in sorted(variants, key=len, reverse=True):
        text = re.sub(re.escape(v), lambda _m: "C:\\Users\\<user>", text, flags=re.IGNORECASE)
    if user and len(user) >= 3:
        text = re.sub(r"(?i)(users[\\/])" + re.escape(user) + r"\b", lambda m: m.group(1) + "<user>", text)
        # T-0730: auch URL-kodiert (%5CUsers%5C<name>) und frei stehend (z. B. „LicenseClient-<name>“, „User: <name>“)
        text = re.sub(r"(?i)(?:(?<![A-Za-z0-9])|(?<=%5C)|(?<=%2F))" + re.escape(user) + r"(?![A-Za-z0-9])", "<user>", text)
    pc = os.environ.get("COMPUTERNAME") or socket.gethostname()
    if pc and len(pc) >= 3:
        text = re.sub(r"\b" + re.escape(pc) + r"\b", "<pc>", text, flags=re.IGNORECASE)
    # T-0730: Maschinen- und Login-IDs (z. B. Unreal „MachineId=…“, „loginid="…"“)
    text = re.sub(r"(?i)\b((?:MachineId|LoginId)\s*[=:]\s*\"?)[0-9a-f]{16,}", lambda m: m.group(1) + "<id>", text)
    return text


def run(cmd: list[str], cwd: Path | None = None, timeout: int = 3600, env: dict | None = None) -> tuple[int, str, float]:
    t0 = time.monotonic()
    try:
        p = subprocess.run(cmd, cwd=cwd, capture_output=True, timeout=timeout, env=env)
        out = (p.stdout or b"").decode("utf-8", "replace") + (p.stderr or b"").decode("utf-8", "replace")
        return p.returncode, out, time.monotonic() - t0
    except subprocess.TimeoutExpired as e:
        out = ((e.stdout or b"") + (e.stderr or b"")).decode("utf-8", "replace")
        return -2, out + f"\nZeitüberschreitung nach {timeout} s", time.monotonic() - t0


# ───────────── Tests im Quelltext finden ─────────────
NAME_RX = re.compile(r'DisplayName\s*=\s*"((?:[^"\\]|\\.)*)"|\[(?:Avalonia)?(?:Fact|Theory)\("((?:[^"\\]|\\.)*)"')
METHOD_RX = re.compile(r"public\s+(?:async\s+)?(?:static\s+)?[\w<>\[\],? ]+\s+(\w+)\s*\(")
CLASS_RX = re.compile(r"^\s*public\s+(?:sealed\s+|static\s+|partial\s+)*class\s+(\w+)", re.M)


def _ns(text: str) -> str:
    m = re.search(r"^namespace\s+([\w.]+)\s*;", text, re.M)
    return m.group(1) if m else ""


def _class_at(text: str, pos: int) -> str | None:
    last = None
    for m in CLASS_RX.finditer(text):
        if m.start() > pos:
            break
        if not m.group(1).endswith("Attribute"):  # eingebettete Attributklassen (z. B. BlenderFactAttribute) sind keine Testklassen
            last = m.group(1)
    return last


def find_tests(project: str, part: str) -> list[tuple[str, str]]:
    """(vollständiger Methodenname, Anzeigename) aller Tests, deren Anzeigename `part` enthält."""
    root = REPO / "tests" / ("Trendo.Core.Tests" if project == "core" else "Trendo.App.Tests")
    hits = []
    for f in sorted(root.rglob("*.cs")):
        if "\\obj\\" in str(f) or "/obj/" in str(f) or "\\bin\\" in str(f) or "/bin/" in str(f):
            continue
        t = f.read_text(encoding="utf-8", errors="replace")
        for m in NAME_RX.finditer(t):
            name = m.group(1) or m.group(2) or ""
            if part not in name:
                continue
            mm = METHOD_RX.search(t, m.end())
            cls = _class_at(t, m.start())
            if mm and cls:
                hits.append(((_ns(t) + "." if _ns(t) else "") + f"{cls}.{mm.group(1)}", name))
    return hits


def find_attr_tests(attr: str) -> list[tuple[str, str]]:
    hits = []
    for f in sorted(CORE_TESTS.rglob("*.cs")):
        if any(x in f.parts for x in ("obj", "bin")):
            continue
        t = f.read_text(encoding="utf-8", errors="replace")
        for m in re.finditer(r"\[" + attr + r"\b(?:\(([^\]]*)\))?\]", t):
            dn = re.search(r'DisplayName\s*=\s*"((?:[^"\\]|\\.)*)"', m.group(1) or "")
            mm = METHOD_RX.search(t, m.end())
            cls = _class_at(t, m.start())
            if mm and cls:
                hits.append(((_ns(t) + "." if _ns(t) else "") + f"{cls}.{mm.group(1)}", dn.group(1) if dn else mm.group(1)))
    return hits


def parse_trx(path: Path) -> list[dict]:
    res = []
    if not path.exists():
        return res
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    root = ET.parse(path).getroot()
    for r in root.iter("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult"):
        msg = r.find(".//t:Output/t:ErrorInfo/t:Message", ns)
        so = r.find(".//t:Output/t:StdOut", ns)
        reason = "".join(x.text or "" for x in r.findall(".//t:Output/t:ReasonText", ns)) if r.find(".//t:Output", ns) is not None else ""
        res.append({"name": r.get("testName"), "outcome": r.get("outcome"), "dauer": r.get("duration"),
                    "meldung": ((msg.text if msg is not None else "") or reason or (so.text if so is not None else "") or "").strip()[:2000]})
    return res


def run_tests(kind: str, methods: list[str], work: Path, tag: str) -> tuple[list[dict], str, float]:
    trx = work / f"{tag}.trx"
    if trx.exists():
        trx.unlink()
    if kind == "core":
        flt = "|".join("FullyQualifiedName=" + m for m in methods)
        cmd = ["dotnet", "test", str(CORE_TESTS), "-c", "Release", "--no-build", "--filter", flt, "--logger", f"trx;LogFileName={trx}"]
        code, out, sec = run(cmd, cwd=REPO, timeout=7200)
    else:
        cmd = [str(APP_TESTS_EXE)]
        for m in methods:
            cmd += ["-method", m]
        cmd += ["-trx", str(trx)]
        code, out, sec = run(cmd, cwd=APP_TESTS_EXE.parent, timeout=7200)
    return parse_trx(trx), f"$ {' '.join(c if ' ' not in c else repr(c) for c in cmd)}\nExit {code}\n{out}", sec


def judge(results: list[dict]) -> str:
    if not results:
        return ROT  # kein Ergebnis: der Test lief gar nicht – Fehler der Station oder des Testlaufs, nie still grün oder blockiert
    oc = [r["outcome"] for r in results]
    if any(o in ("Failed", "Error", "Timeout", "Aborted") for o in oc):
        return ROT
    if all(o in ("NotExecuted", "Skipped", "Inconclusive") for o in oc):
        return BLOCK
    return GRUEN if all(o == "Passed" for o in oc) else GRUEN + " (teilweise übersprungen)"


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description="Trendo Echtlauf-Station")
    ap.add_argument("--nur", nargs="*", help="nur diese Werkzeuge (Kennungen aus der Liste)")
    ap.add_argument("--langsam", action="store_true", help="auch langsame Läufe (Unity, Unreal)")
    ap.add_argument("--bauen", action="store_true", help="vorher Trendo.sln und den Helfer bauen")
    ap.add_argument("--ziel", help="Ausgabeordner (Standard reports/echtlauf/<datum>)")
    a = ap.parse_args()

    out = Path(a.ziel) if a.ziel else REPO / "reports" / "echtlauf" / dt.date.today().isoformat()
    out.mkdir(parents=True, exist_ok=True)
    work = Path(os.environ.get("TEMP", "/tmp")) / f"trendo-echtlauf-{os.getpid()}"
    work.mkdir(parents=True, exist_ok=True)

    if a.bauen:
        for cmd in (["dotnet", "build", str(REPO / "Trendo.sln"), "-c", "Release"], ["dotnet", "build", str(HELPER_DIR), "-c", "Release"]):
            code, txt, _ = run(cmd, cwd=REPO)
            if code != 0:
                print(sanitize(txt[-3000:]))
                return 2
    elif not HELPER.exists():
        code, txt, _ = run(["dotnet", "build", str(HELPER_DIR), "-c", "Release"], cwd=REPO)
        if code != 0:
            print(sanitize(txt[-3000:]))
            return 2

    found_json = work / "werkzeuge.json"
    code, txt, _ = run(["dotnet", str(HELPER), "finden", str(found_json)], cwd=REPO, timeout=600)
    if code != 0:
        print(sanitize(txt))
        return 2
    found = {x["id"]: x for x in json.loads(found_json.read_text(encoding="utf-8"))}
    (out / "werkzeuge.json").write_text(sanitize(json.dumps(list(found.values()), ensure_ascii=False, indent=2)) + "\n", encoding="utf-8")

    summary = []
    for tid, title, steps in PLAN:
        if a.nur and tid not in a.nur:
            continue
        f = found.get(tid, {})
        log = [f"# Echtlauf {tid} – {title}", f"Datum: {dt.datetime.now().isoformat(timespec='seconds')}",
               f"Fundort: {f.get('pfad') or 'nicht gefunden'}", f"Suchweg: {f.get('suchweg', '–')}", f"Version: {f.get('version') or '–'}", ""]
        t0 = time.monotonic()
        verdicts, details = [], []
        if tid in found and not f.get("pfad"):
            verdicts.append(BLOCK)
            details.append(f"{f.get('name', tid)} fehlt auf diesem PC (Suchweg: {f.get('suchweg')}).")
        else:
            for kind, arg in steps:
                if kind == "blockiert":
                    verdicts.append(BLOCK); details.append(arg); log.append("BLOCKIERT: " + arg)
                elif kind in ("helfer", "helfer-langsam"):
                    if kind == "helfer-langsam" and not a.langsam:
                        verdicts.append(BLOCK); details.append("langsamer Lauf, nur mit --langsam")
                        log.append("BLOCKIERT: langsamer Lauf, nur mit --langsam")
                        continue
                    code, txt, sec = run(["dotnet", str(HELPER), "lauf", arg, str(work / tid)], cwd=REPO, timeout=7200)
                    log.append(f"## Fachlauf (Helfer: {arg}) – Exit {code}, {sec:.1f} s"); log.append(txt)
                    v = GRUEN if code == 0 else BLOCK if code == 3 else ROT
                    verdicts.append(v)
                    first = next((l for l in txt.splitlines() if l.startswith("BLOCKIERT:")), None)
                    said = [l.split(" ", 1)[1] for l in txt.splitlines() if re.match(r"^\d\d:\d\d:\d\d \S", l) and not l[9:].startswith("$") and "Exit" not in l[9:16]]
                    details.append(first[len("BLOCKIERT: "):] if first else f"Fachlauf {arg}: {v}" + (f" – {said[-1][:200]}" if said else ""))
                else:
                    tests = find_attr_tests(arg) if kind == "core-attr" else find_tests(kind, arg)
                    if not tests:
                        verdicts.append(ROT); details.append(f"Test „{arg}“ im Quelltext nicht gefunden – Station anpassen")
                        log.append(f"FEHLER: kein Test mit „{arg}“ gefunden"); continue
                    proj = "core" if kind == "core-attr" else kind
                    res, txt, sec = run_tests(proj, sorted({m for m, _ in tests}), work, f"{tid}-{len(verdicts)}")
                    log.append(f"## Tests ({proj}): „{arg}“ – {len(res)} Ergebnis(se), {sec:.1f} s")
                    for r in res:
                        log.append(f"- {r['outcome']}: {r['name']}" + (f"\n  {r['meldung']}" if r["meldung"] and r["outcome"] != "Passed" else ""))
                    log.append(txt[-6000:])
                    v = judge(res)
                    verdicts.append(v)
                    skipped = [r for r in res if r["outcome"] in ("NotExecuted", "Skipped")]
                    details.append(f"{arg}: {sum(r['outcome'] == 'Passed' for r in res)} bestanden, {sum(r['outcome'] == 'Failed' for r in res)} fehlgeschlagen, {len(skipped)} übersprungen"
                                   + (f" ({skipped[0]['meldung'][:160]})" if skipped and skipped[0]["meldung"] else ""))
        sec = time.monotonic() - t0
        if any(v == ROT for v in verdicts):
            final = ROT
        elif verdicts and all(v == BLOCK for v in verdicts):
            final = BLOCK
        elif any(v == BLOCK or "teilweise" in v for v in verdicts):
            final = GRUEN + " (teilweise)"
        else:
            final = GRUEN
        log += ["", f"ERGEBNIS: {final} ({sec:.1f} s)"] + [f"- {d}" for d in details]
        (out / f"{tid}.log").write_text(sanitize("\n".join(log)) + "\n", encoding="utf-8")
        summary.append({"werkzeug": tid, "fachlauf": title, "version": f.get("version"), "ergebnis": final, "dauer_s": round(sec, 1), "details": [sanitize(d) for d in details]})
        print(f"{tid:12} {final:28} {sec:7.1f} s")

    if a.nur and (out / "ergebnis.json").exists():  # Teil-Lauf: übrige Werkzeuge aus dem vorhandenen Beleg übernehmen
        done = {s["werkzeug"] for s in summary}
        old = [s for s in json.loads((out / "ergebnis.json").read_text(encoding="utf-8")) if s["werkzeug"] not in done]
        order = [t for t, _, _ in PLAN]
        summary = sorted(old + summary, key=lambda s: order.index(s["werkzeug"]) if s["werkzeug"] in order else 99)
    (out / "ergebnis.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    md = [f"# Echtlauf-Station – {out.name}", "", "Erzeugt von `python tools/echtlauf/echtlauf.py`. Je Werkzeug ein echter Fachlauf über Trendos Code; Rohlog in `<werkzeug>.log`.",
          "ECHT-GRÜN = mit dem echten Werkzeug gelaufen und bestanden; „(teilweise)“ = ein Teil wurde übersprungen oder ist bewusst nicht automatisiert (siehe Spalte Hinweise); ROT = fehlgeschlagen; BLOCKIERT = Werkzeug oder Voraussetzung fehlt.", "",
          "| Werkzeug | Fachlauf | Version | Ergebnis | Dauer | Hinweise |", "|---|---|---|---|---|---|"]
    for s in summary:
        md.append(f"| {s['werkzeug']} | {s['fachlauf']} | {sanitize(s['version'] or '–')} | {s['ergebnis']} | {s['dauer_s']} s | {'<br>'.join(d.replace('|', '/') for d in s['details'])} |")
    (out / "ERGEBNIS.md").write_text("\n".join(md) + "\n", encoding="utf-8")
    sums = [f"{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.name}" for p in sorted(out.iterdir()) if p.is_file() and p.name != "SHA256SUMS"]
    (out / "SHA256SUMS").write_text("\n".join(sums) + "\n", encoding="utf-8")
    print(f"Beleg: {out.relative_to(REPO) if out.is_relative_to(REPO) else out}")
    return 1 if any(s["ergebnis"] == ROT for s in summary) else 0


if __name__ == "__main__":
    sys.exit(main())
