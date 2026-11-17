"""T-0725 Echtlauf übrige Fachstudios: führt die Testklasse T0725_Echtlauf_Ui (echte Fremdprogramme) aus und legt Belege unter reports/T-0725/ ab.

    python tools/echtlauf/fachstudios/t0725.py            (vorher: dotnet build Trendo.sln -c Release)

Ergebnis: reports/T-0725/echtlauf.log (Rohausgabe des Testlaufs), je Studio ein Unterordner mit Protokoll (*.log) und den echten Dateien
(Fremddatei von LibreOffice, Trendo-Ergebnis, Gegenprobe durch LibreOffice, qpdf, Ical.Net), dazu SHA256SUMS über alle Dateien. Benutzer- und Rechnernamen werden
in Textdateien ersetzt (C:\\Users\\<user>, <pc>); Binärdateien, die den Benutzernamen enthalten, werden nicht abgelegt (im Log vermerkt).
"""
from __future__ import annotations

import atexit
import hashlib
import os
import shutil
import subprocess
import sys
import tempfile
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "echtlauf"))
from echtlauf import sanitize  # noqa: E402  (gleiche Ersetzung wie die übrigen Echtläufe)

OUT = ROOT / "reports" / "T-0725"
EXE = ROOT / "tests" / "Trendo.App.Tests" / "bin" / "Release" / "net10.0" / "Trendo.App.Tests.exe"
TEXT = {".log", ".txt", ".ics", ".json", ".md", ".csv"}


def main() -> int:
    if not EXE.exists():
        print("Bitte zuerst bauen: dotnet build Trendo.sln -c Release")
        return 2
    raw = Path(tempfile.mkdtemp(prefix="trendo-t0725-roh-"))
    atexit.register(shutil.rmtree, raw, True)  # T-0729: Rohordner auch bei Fehlern wegräumen
    env = dict(os.environ, TRENDO_BELEG_DIR=str(raw))
    started = datetime.now()
    p = subprocess.run([str(EXE), "-class", "Trendo.App.Tests.T0725_Echtlauf_Ui", "-reporter", "verbose"], cwd=ROOT, env=env, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if OUT.exists():
        shutil.rmtree(OUT)
    OUT.mkdir(parents=True)
    user = Path(os.environ.get("USERPROFILE") or str(Path.home())).name
    notes: list[str] = []
    for f in sorted(raw.rglob("*")):
        if not f.is_file():
            continue
        rel = f.relative_to(raw)
        target = OUT / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        if f.suffix.lower() in TEXT:
            target.write_text(sanitize(f.read_text(encoding="utf-8", errors="replace")), encoding="utf-8")
            continue
        data = f.read_bytes()
        if user and len(user) >= 3 and (user.encode("utf-8").lower() in data.lower() or user.encode("utf-16-le").lower() in data.lower()):
            notes.append(f"nicht abgelegt (enthält den Benutzernamen): {rel.as_posix()}")
            continue
        target.write_bytes(data)
    head = [
        "# T-0725 Echtlauf übrige Fachstudios (T0725_Echtlauf_Ui)",
        f"Start: {started:%Y-%m-%dT%H:%M:%S}  Ende: {datetime.now():%Y-%m-%dT%H:%M:%S}",
        "$ Trendo.App.Tests.exe -class Trendo.App.Tests.T0725_Echtlauf_Ui -reporter verbose",
        f"Exit {p.returncode}",
        *notes,
        "",
    ]
    (OUT / "echtlauf.log").write_text(sanitize("\n".join(head) + p.stdout + ("\n[stderr]\n" + p.stderr if p.stderr.strip() else "")), encoding="utf-8")
    sums = [f"{hashlib.sha256(x.read_bytes()).hexdigest()}  {x.relative_to(OUT).as_posix()}" for x in sorted(OUT.rglob("*")) if x.is_file() and x.name != "SHA256SUMS"]
    (OUT / "SHA256SUMS").write_bytes(("\n".join(sums) + "\n").encode("utf-8"))  # LF, damit „sha256sum -c SHA256SUMS“ direkt prüft
    shutil.rmtree(raw, ignore_errors=True)
    print(f"Exit {p.returncode} – {len(sums)} Dateien unter {OUT.relative_to(ROOT).as_posix()}")
    print("\n".join(l for l in p.stdout.splitlines() if "Total:" in l or "[FAIL]" in l or "[SKIP]" in l))
    return p.returncode


if __name__ == "__main__":
    raise SystemExit(main())
