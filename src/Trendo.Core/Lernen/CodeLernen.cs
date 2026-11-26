using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Trendo.Core.Programs;

namespace Trendo.Core.Lernen;

/// <summary>T-0782 (STUDIO-CODELEARN-01 „Programmieren lernen“): Lernpfad (Track) Python mit Konzepten, Aufgaben mit echten Tests,
/// gestufte Hinweise und Fortschritt. Bewertet wird nur durch die echten Tests im abgeschotteten Lauf (<see cref="LernSandbox"/>) – nie durch die KI.
/// Ergebnisform angelehnt an die öffentlich beschriebene Exercism-Schnittstelle (Status pass/fail/error, Ergebnis je Test, Ausgabe gekürzt); eigener Code und eigene Aufgaben.
/// Anforderungen und Prüfungen: docs/nachbau/programmieren-lernen/anforderungen.md.</summary>
public static partial class CodeLernen
{
    /// <param name="Tests">Python-Testcode (unittest), importiert die Lösung als Modul „loesung“.</param>
    public sealed record Aufgabe(string Id, string Konzept, string Titel, string Anleitung, string Start, string Tests, IReadOnlyList<string> Hinweise, string? Braucht = null);

    public sealed record TestErgebnis(string Name, string Status, string Meldung, string Ausgabe);
    /// <param name="Status">„pass“, „fail“, „error“ (Syntax-/Laufzeitfehler vor den Tests) oder „timeout“.</param>
    public sealed record Bericht(string Status, IReadOnlyList<TestErgebnis> Tests, string Meldung, double Sekunden)
    {
        public int Bestanden => Tests.Count(t => t.Status == "pass");
    }

    public static readonly IReadOnlyList<(string Id, string Name)> Konzepte =
    [
        ("ausgabe", "Funktionen und Rückgabe"), ("zahlen", "Rechnen"), ("bedingungen", "Bedingungen"), ("schleifen", "Schleifen"), ("listen", "Listen"), ("texte", "Texte"), ("woerterbuecher", "Wörterbücher"),
    ];

    const string T = "import unittest\nfrom loesung import *\n\n";

    public static readonly IReadOnlyList<Aufgabe> Track =
    [
        new("gruss", "ausgabe", "Begrüßung", "Schreibe eine Funktion `gruss(name)`, die den Text `Hallo, <name>!` zurückgibt.\n\nBeispiel: `gruss(\"Lea\")` gibt `\"Hallo, Lea!\"` zurück.",
            "def gruss(name):\n    # Gib hier den Begrüßungstext zurück\n    pass\n",
            T + "class Tests(unittest.TestCase):\n    def test_lea(self):\n        self.assertEqual(gruss(\"Lea\"), \"Hallo, Lea!\")\n    def test_tom(self):\n        self.assertEqual(gruss(\"Tom\"), \"Hallo, Tom!\")\n    def test_gibt_zurueck(self):\n        self.assertIsInstance(gruss(\"Ali\"), str)\n",
            ["Eine Funktion gibt etwas mit `return` zurück – `print` zeigt es nur an.", "Texte kannst du mit `+` verbinden oder mit einem f-String: `f\"Hallo, {name}!\"`."]),
        new("summe", "zahlen", "Zwei Zahlen addieren", "Schreibe `summe(a, b)`, die die Summe der beiden Zahlen zurückgibt.",
            "def summe(a, b):\n    pass\n",
            T + "class Tests(unittest.TestCase):\n    def test_klein(self):\n        self.assertEqual(summe(2, 3), 5)\n    def test_negativ(self):\n        self.assertEqual(summe(-4, 1), -3)\n    def test_komma(self):\n        self.assertAlmostEqual(summe(0.1, 0.2), 0.3)\n",
            ["`return a + b` – mehr braucht es nicht.", "Achte darauf, dass `return` eingerückt in der Funktion steht."], "gruss"),
        new("gerade", "bedingungen", "Gerade oder ungerade", "Schreibe `ist_gerade(n)`, die `True` zurückgibt, wenn `n` gerade ist, sonst `False`.",
            "def ist_gerade(n):\n    pass\n",
            T + "class Tests(unittest.TestCase):\n    def test_vier(self):\n        self.assertTrue(ist_gerade(4))\n    def test_sieben(self):\n        self.assertFalse(ist_gerade(7))\n    def test_null(self):\n        self.assertTrue(ist_gerade(0))\n    def test_negativ(self):\n        self.assertFalse(ist_gerade(-3))\n",
            ["Der Rest-Operator `%` gibt den Rest einer Division: `7 % 2` ist 1.", "Eine Zahl ist gerade, wenn beim Teilen durch 2 kein Rest bleibt."], "summe"),
        new("note", "bedingungen", "Schulnote aus Punkten", "Schreibe `note(punkte)` für einen Test mit 100 Punkten: ab 90 → 1, ab 75 → 2, ab 60 → 3, ab 45 → 4, ab 25 → 5, sonst 6.",
            "def note(punkte):\n    pass\n",
            T + "class Tests(unittest.TestCase):\n    def test_grenzen(self):\n        for p, n in [(100, 1), (90, 1), (89, 2), (75, 2), (60, 3), (45, 4), (25, 5), (24, 6), (0, 6)]:\n            with self.subTest(punkte=p):\n                self.assertEqual(note(p), n)\n",
            ["Prüfe von oben nach unten mit `if`, `elif` und `else`.", "Bei `if punkte >= 90:` ist 90 selbst schon dabei."], "gerade"),
        new("zaehlen", "schleifen", "Bis n zählen", "Schreibe `bis(n)`, die eine Liste der Zahlen von 1 bis n zurückgibt. `bis(3)` → `[1, 2, 3]`, `bis(0)` → `[]`.",
            "def bis(n):\n    ergebnis = []\n    # Schleife hier\n    return ergebnis\n",
            T + "class Tests(unittest.TestCase):\n    def test_drei(self):\n        self.assertEqual(bis(3), [1, 2, 3])\n    def test_null(self):\n        self.assertEqual(bis(0), [])\n    def test_zehn(self):\n        self.assertEqual(len(bis(10)), 10)\n",
            ["`for i in range(1, n + 1):` läuft von 1 bis n.", "Mit `ergebnis.append(i)` hängst du eine Zahl an die Liste an."], "gerade"),
        new("klatschen", "schleifen", "Klatsch-Spiel", "Schreibe `klatsch(n)`: Ist n durch 3 teilbar, gib `\"Klatsch\"` zurück; durch 5 → `\"Stampf\"`; durch beide → `\"KlatschStampf\"`; sonst die Zahl als Text.",
            "def klatsch(n):\n    pass\n",
            T + "class Tests(unittest.TestCase):\n    def test_eins(self):\n        self.assertEqual(klatsch(1), \"1\")\n    def test_drei(self):\n        self.assertEqual(klatsch(9), \"Klatsch\")\n    def test_fuenf(self):\n        self.assertEqual(klatsch(10), \"Stampf\")\n    def test_beide(self):\n        self.assertEqual(klatsch(30), \"KlatschStampf\")\n",
            ["Prüfe den Fall „durch beide teilbar“ zuerst.", "`str(n)` macht aus der Zahl einen Text."], "zaehlen"),
        new("umdrehen", "listen", "Liste umdrehen", "Schreibe `umgedreht(liste)`, die eine neue Liste in umgekehrter Reihenfolge zurückgibt – ohne die alte zu verändern.",
            "def umgedreht(liste):\n    pass\n",
            T + "class Tests(unittest.TestCase):\n    def test_zahlen(self):\n        self.assertEqual(umgedreht([1, 2, 3]), [3, 2, 1])\n    def test_leer(self):\n        self.assertEqual(umgedreht([]), [])\n    def test_alt_bleibt(self):\n        a = [1, 2]\n        umgedreht(a)\n        self.assertEqual(a, [1, 2])\n",
            ["Eine Liste lässt sich mit einem Ausschnitt umdrehen – schau dir die Schreibweise `liste[::…]` an.", "`liste.reverse()` verändert die alte Liste – das soll hier nicht passieren."], "zaehlen"),
        new("palindrom", "texte", "Palindrom", "Schreibe `ist_palindrom(wort)`: `True`, wenn das Wort vorwärts und rückwärts gleich ist – ohne auf Groß- und Kleinschreibung zu achten („Anna“).",
            "def ist_palindrom(wort):\n    pass\n",
            T + "class Tests(unittest.TestCase):\n    def test_anna(self):\n        self.assertTrue(ist_palindrom(\"Anna\"))\n    def test_otto(self):\n        self.assertTrue(ist_palindrom(\"otto\"))\n    def test_haus(self):\n        self.assertFalse(ist_palindrom(\"Haus\"))\n",
            ["`wort.lower()` macht alles klein.", "Vergleiche das Wort mit seiner umgedrehten Fassung."], "umdrehen"),
        new("woerter", "woerterbuecher", "Wörter zählen", "Schreibe `zaehle_woerter(text)`, die ein Wörterbuch zurückgibt: jedes Wort (klein geschrieben) → wie oft es vorkommt. Wörter sind durch Leerzeichen getrennt.",
            "def zaehle_woerter(text):\n    pass\n",
            T + "class Tests(unittest.TestCase):\n    def test_einfach(self):\n        self.assertEqual(zaehle_woerter(\"ich bin ich\"), {\"ich\": 2, \"bin\": 1})\n    def test_gross(self):\n        self.assertEqual(zaehle_woerter(\"Ball ball\"), {\"ball\": 2})\n    def test_leer(self):\n        self.assertEqual(zaehle_woerter(\"\"), {})\n",
            ["`text.split()` zerlegt den Text in Wörter.", "Mit `zaehler[wort] = zaehler.get(wort, 0) + 1` zählst du hoch."], "palindrom"),
    ];

    public static Aufgabe? ById(string? id) => Track.FirstOrDefault(a => a.Id == id);
    public static string KonzeptName(string id) => Konzepte.FirstOrDefault(k => k.Id == id).Name ?? id;

    // ───────────────────────── Dokument ─────────────────────────

    public static JsonObject Neu() => new() { ["sprache"] = "python", ["aufgabe"] = Track[0].Id, ["code"] = new JsonObject(), ["stand"] = new JsonObject(), ["kiHinweise"] = new JsonObject() };

    public static Aufgabe Aktuell(ProgramDocument d) => ById(d.Text("aufgabe")) ?? Track[0];
    public static string Code(ProgramDocument d, string? id = null) { var a = ById(id) ?? Aktuell(d); return (d.Data["code"] as JsonObject)?[a.Id].AsText() ?? a.Start; }
    static JsonObject? StandJson(ProgramDocument d, string id) => (d.Data["stand"] as JsonObject)?[id] as JsonObject;
    public static bool Geloest(ProgramDocument d, string id) => StandJson(d, id)?["geloest"].AsBool() == true;
    public static int Versuche(ProgramDocument d, string id) => (int)ProgramDocument.ToNumber(StandJson(d, id)?["versuche"]);
    public static int HinweiseGezeigt(ProgramDocument d, string id) => (int)ProgramDocument.ToNumber(StandJson(d, id)?["hinweise"]);
    public static IReadOnlyList<string> KiHinweise(ProgramDocument d, string id) => [.. ((d.Data["kiHinweise"] as JsonObject)?[id] as JsonArray ?? []).Select(x => x.AsText() ?? "")];

    /// <summary>Freigeschaltet, wenn die vorausgesetzte Aufgabe gelöst ist (wie die Konzept-Freischaltung eines Tracks).</summary>
    public static bool Frei(ProgramDocument d, Aufgabe a) => a.Braucht is null || Geloest(d, a.Braucht);
    public static int Fortschritt(ProgramDocument d) => (int)Math.Round(100.0 * Track.Count(a => Geloest(d, a.Id)) / Track.Count);

    public static void Waehlen(ProgramDocument d, string id)
    {
        var a = ById(id) ?? throw new ArgumentException("Diese Aufgabe gibt es nicht.");
        if (!Frei(d, a)) throw new InvalidOperationException($"„{a.Titel}“ ist noch gesperrt – löse zuerst „{ById(a.Braucht)!.Titel}“.");
        d.Edit("Aufgabe " + a.Titel, x => x["aufgabe"] = a.Id);
    }

    public static void CodeSetzen(ProgramDocument d, string code, string label = "Code geändert")
    {
        if (code.Length > 100_000) throw new ArgumentException("Der Code ist zu lang (höchstens 100 000 Zeichen).");
        var id = Aktuell(d).Id;
        if (Code(d, id) == code) return;
        d.Edit(label, x => ((JsonObject)(x["code"] ??= new JsonObject()))[id] = code);
    }

    public static void Zuruecksetzen(ProgramDocument d) => CodeSetzen(d, Aktuell(d).Start, "Code zurückgesetzt");

    public static string? Hinweis(ProgramDocument d)
    {
        var a = Aktuell(d); var n = HinweiseGezeigt(d, a.Id);
        if (n >= a.Hinweise.Count) return null;
        d.Edit("Hinweis gezeigt", x => Stand(x, a.Id)["hinweise"] = n + 1);
        return a.Hinweise[n];
    }

    static JsonObject Stand(JsonObject x, string id)
    {
        var all = (JsonObject)(x["stand"] ??= new JsonObject());
        return (JsonObject)(all[id] ??= new JsonObject { ["versuche"] = 0, ["geloest"] = false, ["hinweise"] = 0 });
    }

    /// <summary>Testergebnis eintragen (ein Rückgängig-Schritt): Versuch zählen, gelöst merken, letztes Ergebnis für die Anzeige.</summary>
    public static void Eintragen(ProgramDocument d, Bericht b, DateTime? jetzt = null)
    {
        var id = Aktuell(d).Id;
        d.Edit(b.Status == "pass" ? "Alle Tests bestanden" : "Tests ausgeführt", x =>
        {
            var s = Stand(x, id);
            s["versuche"] = (int)ProgramDocument.ToNumber(s["versuche"]) + 1;
            if (b.Status == "pass" && !s["geloest"].AsBool()) { s["geloest"] = true; s["geloestAm"] = (jetzt ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture); }
            s["letzter"] = new JsonObject { ["status"] = b.Status, ["bestanden"] = b.Bestanden, ["gesamt"] = b.Tests.Count, ["meldung"] = b.Meldung,
                ["tests"] = new JsonArray([.. b.Tests.Select(t => (JsonNode)new JsonObject { ["name"] = t.Name, ["status"] = t.Status, ["meldung"] = t.Meldung, ["ausgabe"] = t.Ausgabe })]) };
        });
    }

    public static Bericht? Letzter(ProgramDocument d, string? id = null)
    {
        if (StandJson(d, id ?? Aktuell(d).Id)?["letzter"] is not JsonObject l) return null;
        return new(l["status"].AsText() ?? "", [.. (l["tests"] as JsonArray ?? []).OfType<JsonObject>().Select(t => new TestErgebnis(t["name"].AsText() ?? "", t["status"].AsText() ?? "", t["meldung"].AsText() ?? "", t["ausgabe"].AsText() ?? ""))], l["meldung"].AsText() ?? "", 0);
    }

    // ───────────────────────── Testlauf ─────────────────────────

    /// <summary>Kleiner Testlauf (eigener Code): führt die unittest-Tests aus und schreibt „ergebnis.json“ im Arbeitsordner.
    /// Fängt Syntax- und Importfehler als Status „error“ ab; Ausgabe je Test höchstens 500 Zeichen.</summary>
    public const string Runner = """
import io, json, os, sys, traceback, unittest
sys.path.insert(0, os.getcwd())
erg = {"status": "pass", "message": "", "tests": []}
def schreiben():
    with open("ergebnis.json", "w", encoding="utf-8") as f:
        json.dump(erg, f, ensure_ascii=False)
try:
    import test_aufgabe
except BaseException as e:
    tb = traceback.format_exception_only(type(e), e)
    erg["status"] = "error"
    erg["message"] = "".join(tb).strip()[-1500:]
    schreiben()
    sys.exit(0)
class Sammler(unittest.TestResult):
    def __init__(self):
        super().__init__()
        self.puffer = None
    def startTest(self, test):
        super().startTest(test)
        self.puffer = io.StringIO()
        self._alt = sys.stdout
        sys.stdout = self.puffer
    def stopTest(self, test):
        sys.stdout = self._alt
        super().stopTest(test)
    def eintrag(self, test, status, meldung=""):
        name = test.id().split(".")[-1]
        erg["tests"].append({"name": name, "status": status, "message": meldung[-800:], "output": (self.puffer.getvalue() if self.puffer else "")[:500]})
    def addSuccess(self, test):
        self.eintrag(test, "pass")
    def addFailure(self, test, err):
        self.eintrag(test, "fail", str(err[1]))
    def addError(self, test, err):
        self.eintrag(test, "error", "".join(traceback.format_exception_only(err[0], err[1])).strip())
    def addSubTest(self, test, sub, err):
        if err is not None:
            self.eintrag(sub, "fail" if issubclass(err[0], AssertionError) else "error", str(err[1]))
suite = unittest.defaultTestLoader.loadTestsFromModule(test_aufgabe)
r = Sammler()
suite.run(r)
namen = {}
for t in erg["tests"]:
    namen[t["name"]] = t
erg["tests"] = list(namen.values())
if any(t["status"] == "error" for t in erg["tests"]):
    erg["status"] = "fail"
elif any(t["status"] != "pass" for t in erg["tests"]) or not erg["tests"]:
    erg["status"] = "fail"
schreiben()
""";

    /// <summary>Echte Tests der Aufgabe gegen <paramref name="code"/> im abgeschotteten Lauf ausführen. <paramref name="arbeit"/> wird angelegt und danach gelöscht.</summary>
    public static Bericht Testen(Aufgabe a, string code, string python, string arbeit, int timeoutMs = 10_000)
    {
        Directory.CreateDirectory(arbeit);
        try
        {
            File.WriteAllText(Path.Combine(arbeit, "loesung.py"), code, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(arbeit, "test_aufgabe.py"), a.Tests, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(arbeit, "lauf.py"), Runner, new UTF8Encoding(false));
            var r = LernSandbox.Run(python, ["-I", "-B", "-X", "utf8", "lauf.py"], arbeit, timeoutMs);
            return Auswerten(r, Path.Combine(arbeit, "ergebnis.json"), arbeit);
        }
        finally { try { Directory.Delete(arbeit, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
    }

    [GeneratedRegex(@"File ""[^""]*[\\/]([^\\/""]+)""")] private static partial Regex FilePaths();

    /// <summary>Pfade in Meldungen kürzen (nur Dateiname) – keine Ordnernamen des Rechners in Anzeige oder Belegen.</summary>
    public static string Kurz(string s) => FilePaths().Replace(s, "File \"$1\"");

    static Bericht Auswerten(LernSandbox.Result r, string json, string arbeit)
    {
        if (r.TimedOut) return new("timeout", [], $"Zeitgrenze überschritten ({r.Seconds:0.#} s) – läuft eine Schleife endlos?", r.Seconds);
        if (r.MemoryLimit) return new("error", [], "Speichergrenze erreicht – legt der Code riesige Listen an?", r.Seconds);
        if (!File.Exists(json)) return new("error", [], Kurz(("Der Testlauf hat kein Ergebnis geschrieben. " + r.Text + "\n" + r.Err).Trim()).Replace(arbeit, "."), r.Seconds);
        using var doc = JsonDocument.Parse(File.ReadAllText(json));
        var root = doc.RootElement;
        var tests = root.GetProperty("tests").EnumerateArray().Select(t => new TestErgebnis(t.GetProperty("name").GetString() ?? "", t.GetProperty("status").GetString() ?? "",
            Kurz(t.GetProperty("message").GetString() ?? "").Replace(arbeit, "."), t.GetProperty("output").GetString() ?? "")).ToList();
        var status = root.GetProperty("status").GetString() ?? "error";
        var msg = Kurz(root.GetProperty("message").GetString() ?? "").Replace(arbeit, ".");
        if (status == "error" && msg.Length == 0) msg = "Fehler beim Laden des Codes.";
        if (status == "error") msg = Freundlich(msg);
        return new(status, tests, msg, r.Seconds);
    }

    /// <summary>Häufige Python-Fehler in einem Satz erklären (die Meldung selbst bleibt dabei).</summary>
    public static string Freundlich(string msg) =>
        msg.Contains("IndentationError") ? "Einrückung stimmt nicht: Zeilen in einer Funktion brauchen gleich viele Leerzeichen am Anfang.\n" + msg
        : msg.Contains("SyntaxError") ? "Schreibfehler im Code (Syntax) – oft fehlt ein Doppelpunkt, eine Klammer oder ein Anführungszeichen.\n" + msg
        : msg.Contains("NameError") ? "Ein Name ist unbekannt – ist er richtig geschrieben und vorher angelegt?\n" + msg
        : msg.Contains("ImportError") || msg.Contains("ModuleNotFoundError") ? "Eine Funktion oder ein Modul fehlt – heißt deine Funktion genau wie in der Aufgabe?\n" + msg
        : msg;

    // ───────────────────────── KI ─────────────────────────

    public const string KiRolle = "Du bist Programmier-Tutorin für Einsteiger. Du gibst Hinweise und stellst Rückfragen, aber du schreibst NIE die fertige Lösung und keinen kompletten Code der Aufgabe. "
        + "Ob der Code stimmt, entscheiden nur die echten Tests – du bewertest nicht. Antworte kurz auf Deutsch, höchstens ein winziges Beispiel, das nicht die Aufgabe löst. Keine Links.";

    /// <summary>Ein KI-Hinweis darf keinen Code-Block und keine Funktionsdefinition der Aufgabe enthalten (sonst wäre er die Lösung).</summary>
    public static string? HinweisProblem(Aufgabe a, string hinweis)
    {
        var h = hinweis.Trim();
        if (h.Length < 5) return "Der Hinweis ist leer.";
        if (h.Length > 600) return "Der Hinweis ist zu lang (höchstens 600 Zeichen).";
        if (h.Contains("```") || Regex.IsMatch(h, @"(^|\n)\s*(def|class)\s+\w+\s*\(", RegexOptions.Multiline)) return "Ein Hinweis enthält keinen fertigen Code – nur einen Denkanstoß.";
        if (h.Split('\n').Count(l => Regex.IsMatch(l, @"^\s{2,}\S") || l.TrimStart().StartsWith("return ", StringComparison.Ordinal)) >= 2) return "Ein Hinweis enthält keinen fertigen Code – nur einen Denkanstoß.";
        if (Nachhilfe.HatLink(h)) return "Hinweise enthalten keine Links.";
        return null;
    }

    public static void KiHinweisAnlegen(ProgramDocument d, string hinweis, string label = "KI-Hinweis")
    {
        var a = Aktuell(d);
        if (HinweisProblem(a, hinweis) is { } p) throw new ArgumentException(p);
        d.Edit(label, x => { var all = (JsonObject)(x["kiHinweise"] ??= new JsonObject()); ((JsonArray)(all[a.Id] ??= new JsonArray())).Add(hinweis.Trim()); });
    }

    public static string KiFrage(ProgramDocument d, string frage)
    {
        var a = Aktuell(d); var l = Letzter(d);
        var sb = new StringBuilder($"Aufgabe „{a.Titel}“ (Konzept: {KonzeptName(a.Konzept)}):\n{a.Anleitung}\n\nAktueller Code der lernenden Person:\n{Code(d)}\n");
        if (l is not null) sb.Append($"\nLetzter Testlauf: {l.Status}, {l.Bestanden}/{l.Tests.Count} bestanden. {l.Meldung} " + string.Join(" ", l.Tests.Where(t => t.Status != "pass").Take(3).Select(t => $"{t.Name}: {t.Meldung}")) + "\n");
        sb.Append($"\nFrage: {frage}");
        return sb.ToString();
    }

    /// <summary>Lösung als Python-Datei mit den Tests darunter – läuft in jedem Python mit „python datei.py“.</summary>
    public static string Export(ProgramDocument d)
    {
        var a = Aktuell(d);
        var tests = a.Tests.Replace("from loesung import *\n", "");
        return $"# Trendo – Programmieren lernen: {a.Titel}\n# Aufgabe: {a.Anleitung.Split('\n')[0]}\n\n{Code(d).TrimEnd()}\n\n\n# ---- Tests der Aufgabe ----\n{tests.TrimEnd()}\n\n\nif __name__ == \"__main__\":\n    unittest.main(verbosity=2)\n";
    }

    /// <summary>Eigene Python-Datei als Code der aktuellen Aufgabe einlesen (Tests aus einer exportierten Datei werden abgeschnitten).</summary>
    public static string ImportText(string text)
    {
        if (text.Length > 100_000) throw new InvalidDataException("Die Datei ist zu groß (höchstens 100 000 Zeichen).");
        text = text.Replace("\r\n", "\n").TrimStart('﻿');
        var cut = text.IndexOf("# ---- Tests der Aufgabe ----", StringComparison.Ordinal);
        if (cut >= 0) text = text[..cut];
        var lines = text.Split('\n').SkipWhile(l => l.StartsWith("# Trendo – Programmieren lernen", StringComparison.Ordinal) || l.StartsWith("# Aufgabe:", StringComparison.Ordinal)).ToList();
        return string.Join("\n", lines).Trim('\n') + "\n";
    }
}
