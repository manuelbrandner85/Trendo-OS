#!/usr/bin/env python3
"""Einfache Stückliste (SBOM) der NuGet-Pakete im Produkt, mit Lizenz aus der .nuspec-Datei im lokalen Paketordner.

Aufruf: python scripts/sbom.py   schreibt docs/commercial/sbom-nuget.json und docs/commercial/sbom-nuget.md
Erfasst die Pakete der Produktprojekte (src/), getrennt nach direkt und mittelbar. Testprojekte zählen nicht.
"""
import io
import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "docs", "commercial")
CACHE = os.environ.get("NUGET_PACKAGES") or os.path.join(os.path.expanduser("~"), ".nuget", "packages")


def projects():
    for name in ("Trendo.App", "Trendo.Core", "Trendo.Api", "Trendo.Companion"):
        path = os.path.join(ROOT, "src", name, name + ".csproj")
        if os.path.exists(path):
            yield name, path


def packages(csproj):
    raw = subprocess.run(["dotnet", "list", csproj, "package", "--include-transitive", "--format", "json"], capture_output=True, text=True, encoding="utf-8", check=True).stdout
    data = json.loads(raw)
    for p in data.get("projects", []):
        for fw in p.get("frameworks", []):
            for kind, key in (("direkt", "topLevelPackages"), ("mittelbar", "transitivePackages")):
                for pkg in fw.get(key, []):
                    yield pkg["id"], pkg.get("resolvedVersion") or pkg.get("requestedVersion"), kind


def license_of(pid, version):
    folder = os.path.join(CACHE, pid.lower(), version.lower())
    nuspec = os.path.join(folder, pid.lower() + ".nuspec")
    if not os.path.exists(nuspec):
        return "nicht im Paketordner gefunden"
    text = io.open(nuspec, encoding="utf-8", errors="replace").read()
    m = re.search(r"<license\s+type=\"expression\"[^>]*>([^<]+)</license>", text)
    if m:
        return m.group(1).strip()
    m = re.search(r"<license\s+type=\"file\"[^>]*>([^<]+)</license>", text)
    if m:
        return "Lizenzdatei im Paket: " + m.group(1).strip()
    m = re.search(r"<licenseUrl>([^<]+)</licenseUrl>", text)
    if m:
        return "nur Verweis: " + m.group(1).strip()
    return "keine Angabe im Paket"


def main():
    found = {}
    for name, path in projects():
        for pid, ver, kind in packages(path):
            entry = found.setdefault((pid, ver), {"paket": pid, "version": ver, "art": kind, "projekte": []})
            if kind == "direkt":
                entry["art"] = "direkt"
            if name not in entry["projekte"]:
                entry["projekte"].append(name)
    rows = sorted(found.values(), key=lambda r: (r["art"] != "direkt", r["paket"].lower()))
    for r in rows:
        r["lizenz"] = license_of(r["paket"], r["version"])
    os.makedirs(OUT, exist_ok=True)
    with io.open(os.path.join(OUT, "sbom-nuget.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump({"format": "Trendo SBOM 1", "quelle": "dotnet list package --include-transitive, Lizenz aus .nuspec", "pakete": rows}, f, ensure_ascii=False, indent=1)
    lines = ["# NuGet-Stückliste (SBOM)", "", "Erzeugt mit `scripts/sbom.py` aus den Produktprojekten unter `src/`. Testprojekte sind nicht enthalten.", "",
             "| Paket | Version | Art | Lizenz laut Paket | Projekte |", "|---|---|---|---|---|"]
    for r in rows:
        lines.append(f"| {r['paket']} | {r['version']} | {r['art']} | {r['lizenz']} | {', '.join(r['projekte'])} |")
    with io.open(os.path.join(OUT, "sbom-nuget.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    counts = {}
    for r in rows:
        counts[r["lizenz"]] = counts.get(r["lizenz"], 0) + 1
    sys.stdout.write(f"{len(rows)} Pakete\n")
    for k, v in sorted(counts.items(), key=lambda x: -x[1]):
        sys.stdout.write(f"  {v:3} {k}\n")


if __name__ == "__main__":
    main()
