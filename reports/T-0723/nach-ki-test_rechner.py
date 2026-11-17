import unittest

from rechner import addiere, teile, multipliziere


class RechnerTest(unittest.TestCase):
    def test_addiere(self):
        self.assertEqual(addiere(2, 3), 5)

    def test_teile(self):
        self.assertEqual(teile(9, 3), 3)
        with self.assertRaises(ValueError):
            teile(1, 0)

    def test_multipliziere(self):
        self.assertEqual(multipliziere(2, 3), 6)


if __name__ == "__main__":
    unittest.main()
