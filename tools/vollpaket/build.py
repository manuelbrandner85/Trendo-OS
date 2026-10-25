"""Baut den Ordner „Programme“ des Trendo-Vollpakets (T-0222).

  python tools/vollpaket/build.py [--nur id,id] [--ziel artifacts/vollpaket/out]

Schritte je Programm: laden (nur offizielle Quelle aus programme.json) -> Prüfsumme gegen die Herstellerangabe ->
entpacken -> einen einzelnen Oberordner einebnen -> Hauptprogramm vorhanden? -> als <id>.zip packen.
Am Ende: Programme/manifest.json (liest Trendo beim Einrichten) und SHA256SUMS.
Arbeitsordner artifacts/vollpaket/ (nie committet). Nichts wird installiert, nichts außerhalb des Repos geändert.
"""
from __future__ import annotations

import argparse, hashlib, json, os, shutil, subprocess, sys, time, urllib.request, zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "artifacts" / "vollpaket"
DL, STAGE, BIN = WORK / "download", WORK / "staging", WORK / "bin"
SEVENZR = ("https://github.com/ip7z/7zip/releases/download/26.05/7zr.exe", "9bf582b894dfa665f852d7e9112b02290a1c303c2d95a21fcdc41ffcb03143b7")
JUNK = {"$PLUGINSDIR", "$TEMP", "[NSIS].nsi", "Uninstall.exe", "uninstall.exe", "$R0"}


def sha256(p: Path) -> str:
    h = hashlib.sha256()
    with p.open("rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""): h.update(b)
    return h.hexdigest()


def fetch(url: str, dest: Path, expected: str | None) -> str:
    if not dest.exists():
        dest.parent.mkdir(parents=True, exist_ok=True)
        tmp = dest.with_suffix(dest.suffix + ".part")
        print(f"  laden {url}", flush=True)
        req = urllib.request.Request(url, headers={"User-Agent": "TrendoOS-Vollpaket/1"})
        with urllib.request.urlopen(req, timeout=120) as r, tmp.open("wb") as f: shutil.copyfileobj(r, f, 1 << 20)
        tmp.replace(dest)
    got = sha256(dest)
    if expected and got != expected.lower():
        dest.unlink()
        raise SystemExit(f"Prüfsumme falsch: {dest.name} – erwartet {expected}, bekommen {got}. Datei gelöscht.")
    return got


def seven_zip() -> Path:
    exe = BIN / "7z.exe"
    if exe.exists(): return exe
    zr = BIN / "7zr.exe"; fetch(SEVENZR[0], zr, SEVENZR[1])
    inst = DL / "7z2605-x64.exe"; fetch("https://github.com/ip7z/7zip/releases/download/26.05/7z2605-x64.exe", inst, "fd99e86f57d8f429ef5ed045bf98315af962e2309b0f1ba87a147a44c71647d0")
    subprocess.run([str(zr), "x", "-y", f"-o{BIN}", str(inst), "7z.exe", "7z.dll"], check=True, stdout=subprocess.DEVNULL)
    return exe


def unpack(src: Path, kind: str, dest: Path) -> None:
    if dest.exists(): shutil.rmtree(dest)
    dest.mkdir(parents=True)
    if kind == "zip":
        with zipfile.ZipFile(src) as z: z.extractall(dest)
    else:  # 7z, sfx (7z-Selbstentpacker), nsis – alles kann 7-Zip öffnen, ohne das Programm auszuführen
        subprocess.run([str(seven_zip()), "x", "-y", f"-o{dest}", str(src)], check=True, stdout=subprocess.DEVNULL)
    for j in JUNK:
        p = dest / j
        if p.is_dir(): shutil.rmtree(p)
        elif p.exists(): p.unlink()
    kids = list(dest.iterdir())
    if len(kids) == 1 and kids[0].is_dir():  # einzelnen Oberordner einebnen
        inner = kids[0]
        for c in inner.iterdir(): shutil.move(str(c), dest / c.name)
        inner.rmdir()


def pull_model(tag: str, ollama_dir: Path, dest: Path) -> None:
    if dest.exists(): shutil.rmtree(dest)
    dest.mkdir(parents=True)
    env = dict(os.environ, OLLAMA_MODELS=str(dest), OLLAMA_HOST="127.0.0.1:11499")
    srv = subprocess.Popen([str(ollama_dir / "ollama.exe"), "serve"], env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        for _ in range(60):
            if subprocess.run([str(ollama_dir / "ollama.exe"), "list"], env=env, capture_output=True).returncode == 0: break
            time.sleep(1)
        subprocess.run([str(ollama_dir / "ollama.exe"), "pull", tag], env=env, check=True)
    finally:
        srv.terminate(); srv.wait(30)  # nur der eben gestartete eigene Prozess


def pack(src: Path, zpath: Path) -> None:
    zpath.parent.mkdir(parents=True, exist_ok=True)
    tmp = zpath.with_suffix(".zip.part")
    with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True) as z:
        for f in sorted(src.rglob("*")):
            if f.is_file(): z.write(f, f.relative_to(src).as_posix())
    tmp.replace(zpath)


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--nur", default="")
    ap.add_argument("--ziel", default=str(WORK / "out"))
    a = ap.parse_args()
    spec = json.loads((Path(__file__).parent / "programme.json").read_text(encoding="utf-8"))
    only = {s for s in a.nur.split(",") if s}
    out = Path(a.ziel) / "Programme"
    manifest_path = out / "manifest.json"
    old = {p["id"]: p for p in json.loads(manifest_path.read_text(encoding="utf-8"))["programs"]} if manifest_path.exists() else {}
    entries = []
    for p in spec["programs"]:
        pid = p["id"]
        if only and pid not in only:
            z = out / f"{pid}.zip"
            if pid in old: entries.append(old[pid])
            elif z.exists():  # früher gebautes Paket übernehmen, ohne neu zu packen
                entries.append({"id": pid, "name": p["name"], "version": p["version"], "license": p["license"], "file": z.name,
                                "sha256": sha256(z), "bytes": z.stat().st_size, "exe": p["exe"], "source_url": p["url"],
                                "source_sha256": p["sha256"], "source_sha256_from_vendor": p["sha256"] is not None})
            continue
        print(f"[{pid}] {p['name']} {p['version']}", flush=True)
        stage = STAGE / pid
        if p["kind"] == "ollama-pull":
            src_sha = None
            pull_model(p["version"], STAGE / "ollama", stage)
        else:
            src = DL / Path(p["url"].split("?")[0]).name
            src_sha = fetch(p["url"], src, p["sha256"])
            unpack(src, p["kind"], stage)
        for old_name, new_name in (p.get("rename") or {}).items(): (stage / old_name).rename(stage / new_name)
        for x in p.get("extras") or []:  # Zusatzdateien aus derselben offiziellen Quelle, z. B. deutsche Texterkennung
            target = stage / x["path"]
            x_sha = fetch(x["url"], DL / f"{pid}-{Path(x['path']).name}", x["sha256"])
            target.parent.mkdir(parents=True, exist_ok=True); shutil.copyfile(DL / f"{pid}-{Path(x['path']).name}", target)
            print(f"  + {x['path']} sha256={x_sha}", flush=True)
        if not (stage / p["exe"]).exists(): raise SystemExit(f"[{pid}] Hauptprogramm fehlt nach dem Entpacken: {p['exe']}")
        z = out / f"{pid}.zip"
        pack(stage, z)
        entries.append({"id": pid, "name": p["name"], "version": p["version"], "license": p["license"], "file": z.name,
                        "sha256": sha256(z), "bytes": z.stat().st_size, "exe": p["exe"], "source_url": p["url"],
                        "source_sha256": src_sha, "source_sha256_from_vendor": p["sha256"] is not None})
        print(f"  ok {z.stat().st_size // 1048576} MB", flush=True)
    order = [p["id"] for p in spec["programs"]]
    entries.sort(key=lambda e: order.index(e["id"]))
    manifest_path.write_text(json.dumps({"schema": 1, "built": time.strftime("%Y-%m-%d"), "programs": entries}, ensure_ascii=False, indent=2), encoding="utf-8")
    (out / "SHA256SUMS").write_text("".join(f"{e['sha256']}  {e['file']}\n" for e in entries) + f"{sha256(manifest_path)}  manifest.json\n", encoding="utf-8")
    total = sum(e["bytes"] for e in entries)
    print(f"Fertig: {len(entries)} Programme, {total / (1 << 30):.2f} GB in {out}")


if __name__ == "__main__":
    sys.exit(main())
