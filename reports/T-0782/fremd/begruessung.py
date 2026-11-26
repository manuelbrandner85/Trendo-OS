# Trendo – Programmieren lernen: Begrüßung
# Aufgabe: Schreibe eine Funktion `gruss(name)`, die den Text `Hallo, <name>!` zurückgibt.

def gruss(name):
    return f'Hallo, {name}!'


# ---- Tests der Aufgabe ----
import unittest

class Tests(unittest.TestCase):
    def test_lea(self):
        self.assertEqual(gruss("Lea"), "Hallo, Lea!")
    def test_tom(self):
        self.assertEqual(gruss("Tom"), "Hallo, Tom!")
    def test_gibt_zurueck(self):
        self.assertIsInstance(gruss("Ali"), str)


if __name__ == "__main__":
    unittest.main(verbosity=2)
