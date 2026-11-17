"""T-0719: Gegenprobe der Exporte von Karten & Touren (GPX, KML) und Tagesplaner (ICS) ausserhalb von Trendo.

Nur Python-Standardbibliothek (xml.etree, datetime) - gpxpy und icalendar sind auf diesem PC nicht installiert
und wurden nicht installiert. Geprueft wird gegen die Regeln aus dem GPX-1.1-Schema (topografix.com/GPX/1/1/gpx.xsd:
Namensraum, Pflichtattribute, xsd:sequence-Reihenfolge, Wertebereiche) und RFC 5545 (CRLF, Faltung <= 75 Byte,
BEGIN/END, Pflichtfelder). Aufruf: python -I pruefe_gpx_kml_ics.py <datei> ...
"""
import sys
import xml.etree.ElementTree as ET
from datetime import datetime

GPX = "{http://www.topografix.com/GPX/1/1}"
KML = "{http://www.opengis.net/kml/2.2}"
WPT_ORDER = ["ele", "time", "magvar", "geoidheight", "name", "cmt", "desc", "src", "link", "sym", "type", "fix", "sat",
             "hdop", "vdop", "pdop", "ageofdgpsdata", "dgpsid", "extensions"]
RTE_ORDER = ["name", "cmt", "desc", "src", "link", "number", "type", "extensions", "rtept"]
TRK_ORDER = ["name", "cmt", "desc", "src", "link", "number", "type", "extensions", "trkseg"]
GPX_ORDER = ["metadata", "wpt", "rte", "trk", "extensions"]


def local(e):
    return e.tag.split("}")[-1]


def in_order(children, order, where, errors):
    last = -1
    for c in children:
        n = local(c)
        if n not in order:
            errors.append(f"{where}: unerwartetes Element <{n}>")
            continue
        i = order.index(n)
        if i < last:
            errors.append(f"{where}: <{n}> steht nach <{order[last]}> (GPX-1.1-Reihenfolge verletzt)")
        last = max(last, i)


def point(p, where, errors):
    try:
        lat, lon = float(p.get("lat")), float(p.get("lon"))
        if not (-90 <= lat <= 90 and -180 <= lon < 180):
            errors.append(f"{where}: Koordinate ausserhalb des Bereichs {lat},{lon}")
    except (TypeError, ValueError):
        errors.append(f"{where}: lat/lon fehlt oder keine Zahl")
    in_order(list(p), WPT_ORDER, where, errors)
    for c in p:
        if local(c) == "ele":
            float(c.text)
        if local(c) == "time":
            datetime.fromisoformat(c.text.replace("Z", "+00:00"))


def check_gpx(path):
    errors = []
    root = ET.parse(path).getroot()
    if root.tag != GPX + "gpx":
        errors.append(f"Wurzel ist {root.tag}, erwartet {GPX}gpx")
    if root.get("version") != "1.1":
        errors.append("version ist nicht 1.1")
    if not root.get("creator"):
        errors.append("creator fehlt (Pflicht)")
    in_order(list(root), GPX_ORDER, "gpx", errors)
    n = {"wpt": 0, "rte": 0, "trk": 0, "pkt": 0}
    for i, w in enumerate(root.findall(GPX + "wpt")):
        point(w, f"wpt[{i}]", errors); n["wpt"] += 1
    for i, r in enumerate(root.findall(GPX + "rte")):
        in_order(list(r), RTE_ORDER, f"rte[{i}]", errors); n["rte"] += 1
        for j, p in enumerate(r.findall(GPX + "rtept")):
            point(p, f"rte[{i}]/rtept[{j}]", errors); n["pkt"] += 1
    for i, t in enumerate(root.findall(GPX + "trk")):
        in_order(list(t), TRK_ORDER, f"trk[{i}]", errors); n["trk"] += 1
        for s in t.findall(GPX + "trkseg"):
            for j, p in enumerate(s.findall(GPX + "trkpt")):
                point(p, f"trk[{i}]/trkpt[{j}]", errors); n["pkt"] += 1
    names = [e.text for e in root.iter() if local(e) == "name"]
    return errors, f"{n['wpt']} Wegpunkte, {n['rte']} Routen, {n['trk']} Spuren, {n['pkt']} Punkte, Namen: {names}"


def check_kml(path):
    errors = []
    root = ET.parse(path).getroot()
    if root.tag != KML + "kml":
        errors.append(f"Wurzel ist {root.tag}")
    marks = root.findall(f".//{KML}Placemark")
    for m in marks:
        for co in m.iter(KML + "coordinates"):
            for t in co.text.split():
                parts = [float(x) for x in t.split(",")]
                if len(parts) not in (2, 3) or not (-180 <= parts[0] <= 180 and -90 <= parts[1] <= 90):
                    errors.append(f"Koordinate ungueltig: {t}")
    return errors, f"{len(marks)} Placemarks: {[m.findtext(KML + 'name') for m in marks]}"


def check_ics(path):
    errors = []
    raw = open(path, "rb").read()
    if b"\n" in raw.replace(b"\r\n", b""):
        errors.append("Zeilenende ohne CR (RFC 5545 verlangt CRLF)")
    lines = raw.split(b"\r\n")
    for i, l in enumerate(lines):
        if len(l) > 75:
            errors.append(f"Zeile {i + 1} hat {len(l)} Byte (> 75, nicht gefaltet)")
    text = raw.decode("utf-8").replace("\r\n ", "").replace("\r\n\t", "")
    stack, events, cur = [], [], None
    for l in text.split("\r\n"):
        if not l:
            continue
        k, _, v = l.partition(":")
        name = k.split(";")[0]
        if name == "BEGIN":
            stack.append(v)
            if v == "VEVENT":
                cur = {}
        elif name == "END":
            if not stack or stack.pop() != v:
                errors.append(f"END:{v} passt nicht")
            if v == "VEVENT":
                events.append(cur); cur = None
        elif cur is not None:
            cur[name] = v
    if stack:
        errors.append(f"nicht geschlossen: {stack}")
    if "VERSION:2.0" not in text or "PRODID:" not in text:
        errors.append("VERSION:2.0 oder PRODID fehlt")
    out = []
    for e in events:
        for req in ("UID", "DTSTAMP", "DTSTART"):
            if req not in e:
                errors.append(f"VEVENT ohne {req}")
        s = datetime.strptime(e["DTSTART"], "%Y%m%dT%H%M%S")
        en = datetime.strptime(e["DTEND"], "%Y%m%dT%H%M%S")
        if en <= s:
            errors.append(f"DTEND nicht nach DTSTART: {e.get('SUMMARY')}")
        out.append(f"{s:%d.%m.%Y %H:%M}-{en:%H:%M} {e.get('SUMMARY')} [{e.get('CATEGORIES')}]")
    return errors, f"{len(events)} Termine: {out}"


def main():
    bad = 0
    for p in sys.argv[1:]:
        kind = p.rsplit(".", 1)[-1].lower()
        errs, info = {"gpx": check_gpx, "kml": check_kml, "ics": check_ics}[kind](p)
        name = p.replace("\\", "/").rsplit("/", 1)[-1]
        print(f"{'OK    ' if not errs else 'FEHLER'} {name}: {info}")
        for e in errs:
            print("       - " + e)
        bad += len(errs)
    print(f"Ergebnis: {'alle Pruefungen bestanden' if bad == 0 else str(bad) + ' Fehler'}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
