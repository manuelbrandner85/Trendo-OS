#!/usr/bin/env python3
"""SYS-13-P22: erzeugt THIRD_PARTY_NUGET.md aus den project.assets.json der ausgelieferten Projekte (src/)."""
import glob, json, os, re, xml.etree.ElementTree as ET
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
nuget = os.environ.get("NUGET_PACKAGES") or os.path.expanduser("~/.nuget/packages")
pk = {}
for a in glob.glob(os.path.join(root, "src", "*", "obj", "project.assets.json")):
    for name, lib in json.load(open(a))["libraries"].items():
        if lib["type"] == "package":
            i, v = name.split("/"); pk.setdefault(i.lower(), (i, v))
def lic(i, v):
    p = os.path.join(nuget, i.lower(), v.lower(), i.lower() + ".nuspec")
    if not os.path.exists(p): return "unbekannt (Paket nicht im Cache)"
    md = next(e for e in ET.parse(p).getroot() if e.tag.endswith("metadata"))
    l = next((e for e in md if e.tag.endswith("}license") or e.tag == "license"), None)
    u = next((e.text for e in md if e.tag.endswith("licenseUrl")), None)
    if l is not None: return ("Lizenzdatei im Paket: " + l.text) if l.get("type") == "file" else l.text
    if u:
        if re.search("mit", u, re.I): return f"MIT ({u})"
        if re.search("apache", u, re.I): return f"Apache-2.0 ({u})"
        return "siehe " + u
    return "unbekannt"
rows = "".join(f"| {i} | {v} | {lic(i, v).replace('|', '/')} |\n" for _, (i, v) in sorted(pk.items()))
open(os.path.join(root, "THIRD_PARTY_NUGET.md"), "w").write("# NuGet-Bibliotheken und Lizenzen\n\nAutomatisch erzeugt aus project.assets.json (alle Projekte, auch indirekte Pakete).\n\n| Paket | Version | Lizenz |\n|---|---|---|\n" + rows)
print(len(pk), "Pakete")
