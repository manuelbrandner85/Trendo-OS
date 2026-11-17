"""T-0740: unabhängige Prüfung der Belege mit Python (eigener CSV-/Text-Leser, nicht Trendo-Code).

Aufruf: python reports/T-0740/pruefe.py reports/T-0740
Liest die von Trendo erzeugten Dateien unter lauf/ und prüft Aufbau und Inhalt.
"""
import csv
import io
import json
import sys
from pathlib import Path

base = Path(sys.argv[1])
lauf = base / "lauf"
ok = True


def check(cond, text):
    global ok
    print(("OK    " if cond else "FEHLER") + " " + text)
    ok = ok and cond


# 1) Vorhersagen je Zeile (Ausgabe der CSV-Anwendung)
raw = (lauf / "ausgabe-neue-texte-vorhersagen.csv").read_bytes()
check(raw.startswith(b"\xef\xbb\xbf"), "Vorhersagen-CSV beginnt mit UTF-8-BOM")
check(b"\r" not in raw, "Vorhersagen-CSV: einheitlich LF")
rows = list(csv.reader(io.StringIO(raw.decode("utf-8-sig")), delimiter=";"))
check(rows[0] == ["nr", "text", "erwartet", "Vorhersage", "Sicherheit", "Bekannte Wörter"], "Kopfzeile: " + ";".join(rows[0]))
data = rows[1:]
check(len(data) == 30, f"{len(data)} Datenzeilen (erwartet 30)")
right = sum(1 for r in data if r[2] == r[3])
print(f"      Treffer auf ungesehenen Texten: {right} von {len(data)} ({right * 100 // len(data)} %)")
check(right >= 24, "mindestens 24 von 30 richtig")
check(all(r[4].endswith(" %") and 0 <= int(r[4][:-2]) <= 100 for r in data), "jede Zeile hat eine Sicherheit 0–100 %")
wrong = [f"{r[0]}: „{r[1]}“ erwartet {r[2]}, erkannt {r[3]} ({r[4]})" for r in data if r[2] != r[3]]
for w in wrong:
    print("      daneben: " + w)

# 2) Vorhersagen je Datei (Ordner)
raw = (lauf / "ausgabe-ordner-vorhersagen.csv").read_bytes()
rows = list(csv.reader(io.StringIO(raw.decode("utf-8-sig")), delimiter=";"))
check(rows[0][:3] == ["Datei", "Vorhersage", "Sicherheit"], "Ordner-Kopfzeile: " + ";".join(rows[0]))
got = {r[0]: r[1] for r in rows[1:]}
want = {"rechnung-strom.txt": "Rechnung", "mahnung.txt": "Rechnung", "einladung-besprechung.txt": "Termin", "arzttermin.md": "Termin", "newsletter.txt": "Werbung", "gewinnspiel.txt": "Werbung"}
check(got == want, "je Datei die erwartete Kategorie: " + ", ".join(f"{k}={v}" for k, v in sorted(got.items())))

# 3) Vorlage als Markdown mit Kopfzeilen
md = (lauf / "vorlage-export.md").read_text(encoding="utf-8")
head, body = md.split("\n---\n", 1)
meta = dict(line.split(": ", 1) for line in head.splitlines()[1:])
check(meta.get("format") == "trendo-ki-anweisung" and meta.get("name") == "Brief formulieren" and meta.get("bereich") == "Briefe", "Markdown-Kopf: " + json.dumps(meta, ensure_ascii=False))
check("{{Empfänger}}" in body and "{{Ton|sachlich und freundlich}}" in body, "Anweisungstext mit Lücken unverändert")

# 4) Sammlung als JSON
j = json.loads((lauf / "alle-vorlagen.json").read_text(encoding="utf-8"))
check(j.get("format") == "trendo-ki-anweisungen" and len(j["vorlagen"]) >= 6, f"JSON-Sammlung mit {len(j['vorlagen'])} Vorlagen")
brief = [v for v in j["vorlagen"] if v["name"] == "Brief formulieren"]
check(len(brief) == 1 and "2 kurze Absätze" in brief[0]["text"] and brief[0]["version"] == 2, "„Brief formulieren“ nach Ersetzen: Version 2 mit geändertem Text")

print("GESAMT: " + ("BESTANDEN" if ok else "FEHLER"))
sys.exit(0 if ok else 1)
