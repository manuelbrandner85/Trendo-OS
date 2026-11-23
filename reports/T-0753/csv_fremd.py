"""T-0753: CSV-Exporte mit dem csv-Modul von CPython lesen (RFC 4180, unabhaengig von Trendo) und gegen die Shopify-Spalten pruefen."""
import csv, os, sys, platform

d = sys.argv[1]
SHOPIFY_PRODUCT_REQUIRED = {"Title", "URL handle"}
print(f"Werkzeug: Python {platform.python_version()} csv-Modul")
ok = True
for n in ("artikel.csv", "kunden.csv", "bestellungen.csv"):
    with open(os.path.join(d, n), encoding="utf-8", newline="") as f:
        rows = list(csv.reader(f))
    head, data = rows[0], rows[1:]
    widths = {len(r) for r in data}
    good = widths == {len(head)}
    ok &= good
    print(f"{n}: {len(data)} Datenzeilen, {len(head)} Spalten, alle Zeilen gleich breit: {good}")
    if n == "artikel.csv":
        miss = SHOPIFY_PRODUCT_REQUIRED - set(head)
        print(f"  Shopify-Pflichtspalten vorhanden: {not miss}")
        ok &= not miss
        handles = {r[head.index('URL handle')] for r in data}
        print(f"  Artikel (Handles): {sorted(handles)}; Varianten-Zeilen: {len(data)}")
        for r in data:
            img = r[head.index('Product image URL')]
            if img and not img.startswith("http"):
                exists = os.path.exists(os.path.join(d, img))
                print(f"  Bild {img} liegt daneben: {exists}")
                ok &= exists
print("ERGEBNIS:", "BESTANDEN" if ok else "FEHLER")
