"""Funktionsschutz: Jede in der Baseline erfasste Funktion muss im Quellcode weiter vorhanden sein."""
import csv
import glob
import io
import os
import unittest

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BASELINE = os.path.join(ROOT, "docs", "STUDIO_REBUILD_4_0", "baseline", "funktionen.csv")


class FunktionsBaseline(unittest.TestCase):
    def test_keine_funktion_verschwunden(self):
        with io.open(BASELINE, encoding="utf-8", newline="") as f:
            rows = list(csv.DictReader(f))
        self.assertGreater(len(rows), 1000)
        source = []
        for path in glob.glob(os.path.join(ROOT, "src", "Trendo.App", "**", "*.cs"), recursive=True):
            if os.sep + "obj" + os.sep in path or os.sep + "bin" + os.sep in path:
                continue
            with io.open(path, encoding="utf-8") as f:
                source.append(f.read())
        text = "\n".join(source)
        missing = [r["funktions_id"] for r in rows if '"' + r["name"] + '"' not in text]
        self.assertEqual([], missing, "Diese Funktionen fehlen. Wurde eine bewusst gestrichen, muss das der Nutzer entschieden haben.")

    def test_pflichtspalten(self):
        with io.open(BASELINE, encoding="utf-8", newline="") as f:
            rows = list(csv.DictReader(f))
        for r in rows:
            for column in ("funktions_id", "aufrufstelle", "eingabe", "ausgabe", "abhaengigkeiten", "bedienbarkeit", "teststatus"):
                self.assertTrue(r[column], f"{r['funktions_id']}: {column} fehlt")
        self.assertEqual(len(rows), len({r["funktions_id"] for r in rows}))


if __name__ == "__main__":
    unittest.main()
