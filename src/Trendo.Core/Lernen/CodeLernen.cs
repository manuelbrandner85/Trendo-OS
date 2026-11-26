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
    /// <summary>T-0782 (Gegenprüfung M5): ein Testfall als Daten. <paramref name="Aufruf"/> läuft im abgeschotteten Python; <paramref name="Erwartet"/>
    /// (JSON) bleibt außerhalb – verglichen wird in Trendo, nie im Lerncode. <paramref name="Art"/>: „gleich“, „fast“ (Kommazahl) oder „typ“ (Name des Typs).</summary>
    public sealed record Fall(string Name, string Aufruf, string Erwartet, string Art = "gleich");

    /// <param name="Faelle">Die Tests der Aufgabe als Daten (siehe <see cref="Fall"/>).</param>
    public sealed record Aufgabe(string Id, string Konzept, string Titel, string Anleitung, string Start, IReadOnlyList<Fall> Faelle, IReadOnlyList<string> Hinweise, string? Braucht = null)
    {
        /// <summary>Dieselben Tests als unittest-Code (für den Export – läuft in jedem Python).</summary>
        public string Tests => UnittestCode(this);
    }

    public sealed record TestErgebnis(string Name, string Status, string Meldung, string Ausgabe);
    /// <param name="Status">„pass“, „fail“, „error“ (Syntax-/Laufzeitfehler vor den Tests), „timeout“ oder „limit“ (Ausgabe/Dateien zu groß).</param>
    public sealed record Bericht(string Status, IReadOnlyList<TestErgebnis> Tests, string Meldung, double Sekunden)
    {
        public int Bestanden => Tests.Count(t => t.Status == "pass");
    }

    public static readonly IReadOnlyList<(string Id, string Name)> Konzepte =
    [
        ("ausgabe", "Funktionen und Rückgabe"), ("zahlen", "Rechnen"), ("bedingungen", "Bedingungen"), ("schleifen", "Schleifen"), ("listen", "Listen"), ("texte", "Texte"), ("woerterbuecher", "Wörterbücher"),
    ];

    static Fall F(string name, string aufruf, string erwartet, string art = "gleich") => new(name, aufruf, erwartet, art);

    public static readonly IReadOnlyList<Aufgabe> Track =
    [
        new("gruss", "ausgabe", "Begrüßung", "Schreibe eine Funktion `gruss(name)`, die den Text `Hallo, <name>!` zurückgibt.\n\nBeispiel: `gruss(\"Lea\")` gibt `\"Hallo, Lea!\"` zurück.",
            "def gruss(name):\n    # Gib hier den Begrüßungstext zurück\n    pass\n",
            [F("lea", "gruss(\"Lea\")", "\"Hallo, Lea!\""), F("tom", "gruss(\"Tom\")", "\"Hallo, Tom!\""), F("gibt_text_zurueck", "gruss(\"Ali\")", "\"str\"", "typ")],
            ["Eine Funktion gibt etwas mit `return` zurück – `print` zeigt es nur an.", "Texte kannst du mit `+` verbinden oder mit einem f-String: `f\"Hallo, {name}!\"`."]),
        new("summe", "zahlen", "Zwei Zahlen addieren", "Schreibe `summe(a, b)`, die die Summe der beiden Zahlen zurückgibt.",
            "def summe(a, b):\n    pass\n",
            [F("klein", "summe(2, 3)", "5"), F("negativ", "summe(-4, 1)", "-3"), F("komma", "summe(0.1, 0.2)", "0.3", "fast")],
            ["`return a + b` – mehr braucht es nicht.", "Achte darauf, dass `return` eingerückt in der Funktion steht."], "gruss"),
        new("gerade", "bedingungen", "Gerade oder ungerade", "Schreibe `ist_gerade(n)`, die `True` zurückgibt, wenn `n` gerade ist, sonst `False`.",
            "def ist_gerade(n):\n    pass\n",
            [F("vier", "ist_gerade(4)", "true"), F("sieben", "ist_gerade(7)", "false"), F("null", "ist_gerade(0)", "true"), F("negativ", "ist_gerade(-3)", "false")],
            ["Der Rest-Operator `%` gibt den Rest einer Division: `7 % 2` ist 1.", "Eine Zahl ist gerade, wenn beim Teilen durch 2 kein Rest bleibt."], "summe"),
        new("note", "bedingungen", "Schulnote aus Punkten", "Schreibe `note(punkte)` für einen Test mit 100 Punkten: ab 90 → 1, ab 75 → 2, ab 60 → 3, ab 45 → 4, ab 25 → 5, sonst 6.",
            "def note(punkte):\n    pass\n",
            [.. new[] { (100, 1), (90, 1), (89, 2), (75, 2), (60, 3), (45, 4), (25, 5), (24, 6), (0, 6) }.Select(x => F($"punkte_{x.Item1}", $"note({x.Item1})", x.Item2.ToString(CultureInfo.InvariantCulture)))],
            ["Prüfe von oben nach unten mit `if`, `elif` und `else`.", "Bei `if punkte >= 90:` ist 90 selbst schon dabei."], "gerade"),
        new("zaehlen", "schleifen", "Bis n zählen", "Schreibe `bis(n)`, die eine Liste der Zahlen von 1 bis n zurückgibt. `bis(3)` → `[1, 2, 3]`, `bis(0)` → `[]`.",
            "def bis(n):\n    ergebnis = []\n    # Schleife hier\n    return ergebnis\n",
            [F("drei", "bis(3)", "[1, 2, 3]"), F("null", "bis(0)", "[]"), F("zehn_lang", "len(bis(10))", "10")],
            ["`for i in range(1, n + 1):` läuft von 1 bis n.", "Mit `ergebnis.append(i)` hängst du eine Zahl an die Liste an."], "gerade"),
        new("klatschen", "schleifen", "Klatsch-Spiel", "Schreibe `klatsch(n)`: Ist n durch 3 teilbar, gib `\"Klatsch\"` zurück; durch 5 → `\"Stampf\"`; durch beide → `\"KlatschStampf\"`; sonst die Zahl als Text.",
            "def klatsch(n):\n    pass\n",
            [F("eins", "klatsch(1)", "\"1\""), F("drei", "klatsch(9)", "\"Klatsch\""), F("fuenf", "klatsch(10)", "\"Stampf\""), F("beide", "klatsch(30)", "\"KlatschStampf\"")],
            ["Prüfe den Fall „durch beide teilbar“ zuerst.", "`str(n)` macht aus der Zahl einen Text."], "zaehlen"),
        new("umdrehen", "listen", "Liste umdrehen", "Schreibe `umgedreht(liste)`, die eine neue Liste in umgekehrter Reihenfolge zurückgibt – ohne die alte zu verändern.",
            "def umgedreht(liste):\n    pass\n",
            [F("zahlen", "umgedreht([1, 2, 3])", "[3, 2, 1]"), F("leer", "umgedreht([])", "[]"), F("alt_bleibt", "(lambda a: (umgedreht(a), a)[1])([1, 2])", "[1, 2]")],
            ["Eine Liste lässt sich mit einem Ausschnitt umdrehen – schau dir die Schreibweise `liste[::…]` an.", "`liste.reverse()` verändert die alte Liste – das soll hier nicht passieren."], "zaehlen"),
        new("palindrom", "texte", "Palindrom", "Schreibe `ist_palindrom(wort)`: `True`, wenn das Wort vorwärts und rückwärts gleich ist – ohne auf Groß- und Kleinschreibung zu achten („Anna“).",
            "def ist_palindrom(wort):\n    pass\n",
            [F("anna", "ist_palindrom(\"Anna\")", "true"), F("otto", "ist_palindrom(\"otto\")", "true"), F("haus", "ist_palindrom(\"Haus\")", "false")],
            ["`wort.lower()` macht alles klein.", "Vergleiche das Wort mit seiner umgedrehten Fassung."], "umdrehen"),
        new("woerter", "woerterbuecher", "Wörter zählen", "Schreibe `zaehle_woerter(text)`, die ein Wörterbuch zurückgibt: jedes Wort (klein geschrieben) → wie oft es vorkommt. Wörter sind durch Leerzeichen getrennt.",
            "def zaehle_woerter(text):\n    pass\n",
            [F("einfach", "zaehle_woerter(\"ich bin ich\")", "{\"ich\": 2, \"bin\": 1}"), F("gross", "zaehle_woerter(\"Ball ball\")", "{\"ball\": 2}"), F("leer", "zaehle_woerter(\"\")", "{}")],
            ["`text.split()` zerlegt den Text in Wörter.", "Mit `zaehler[wort] = zaehler.get(wort, 0) + 1` zählst du hoch."], "palindrom"),
    ];

    /// <summary>JSON-Wert als Python-Literal (true → True, null → None …) für den unittest-Export.</summary>
    static string PyLiteral(JsonNode? n) => n switch
    {
        null => "None",
        JsonArray a => "[" + string.Join(", ", a.Select(PyLiteral)) + "]",
        JsonObject o => "{" + string.Join(", ", o.Select(kv => JsonSerializer.Serialize(kv.Key) + ": " + PyLiteral(kv.Value))) + "}",
        JsonValue v when v.GetValueKind() == JsonValueKind.True => "True",
        JsonValue v when v.GetValueKind() == JsonValueKind.False => "False",
        _ => n.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
    };

    static string UnittestCode(Aufgabe a)
    {
        var sb = new StringBuilder("import unittest\nfrom loesung import *\n\n\nclass Tests(unittest.TestCase):\n");
        foreach (var f in a.Faelle)
        {
            var exp = JsonNode.Parse(f.Erwartet);
            sb.Append($"    def test_{f.Name}(self):\n");
            sb.Append(f.Art switch
            {
                "fast" => $"        self.assertAlmostEqual({f.Aufruf}, {PyLiteral(exp)})\n",
                "typ" => $"        self.assertEqual(type({f.Aufruf}).__name__, {PyLiteral(exp)})\n",
                _ => $"        self.assertEqual({f.Aufruf}, {PyLiteral(exp)})\n",
            });
        }
        return sb.ToString();
    }

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

    /// <summary>Marke vor jeder Ergebniszeile des Laufs.</summary>
    public const string Marke = "@@T0782@@";

    /// <summary>T-0782 (Gegenprüfung M5): Lauf-Hilfe im Container. Sie ruft nur die Aufrufe der Testfälle auf („faelle.json“ enthält KEINE erwarteten Werte)
    /// und schreibt je Fall den zurückgegebenen Wert (als JSON) über eine vorab gesicherte Kopie der Standardausgabe. Verglichen wird danach in Trendo.
    /// Ehrlich: Der Lerncode läuft im selben Python-Prozess und kann diese Zeilen fälschen – aber nur mit Werten, die er selbst ausrechnet; die erwarteten
    /// Werte kennt er nicht. Eine gefälschte „bestanden“-Datei wie früher („ergebnis.json“) gibt es nicht mehr.</summary>
    public const string Runner = """
import io, json, os, sys, traceback
sys.path.insert(0, os.getcwd())
_kanal = os.fdopen(os.dup(1), "w", encoding="utf-8")
_marke = "@@T0782@@"
_dumps = json.dumps
def _sende(o):
    _kanal.write(_marke + _dumps(o, ensure_ascii=False) + "\n")
    _kanal.flush()
with open("faelle.json", encoding="utf-8") as _f:
    _faelle = json.load(_f)
_alt = sys.stdout
sys.stdout = io.StringIO()
try:
    import loesung
    _ns = dict(vars(loesung))
except BaseException as e:
    sys.stdout = _alt
    _sende({"fehler": "".join(traceback.format_exception_only(type(e), e)).strip()[-1500:]})
    raise SystemExit(0)
for _fall in _faelle:
    _puffer = io.StringIO()
    sys.stdout = _puffer
    try:
        _wert = eval(_fall["aufruf"], _ns)
        sys.stdout = _alt
        try:
            _sende({"name": _fall["name"], "wert": _wert, "typ": type(_wert).__name__, "ausgabe": _puffer.getvalue()[:500]})
        except (TypeError, ValueError):
            _sende({"name": _fall["name"], "wert": None, "repr": repr(_wert)[:300], "typ": type(_wert).__name__, "ausgabe": _puffer.getvalue()[:500]})
    except BaseException as e:
        sys.stdout = _alt
        _sende({"name": _fall["name"], "ausnahme": "".join(traceback.format_exception_only(type(e), e)).strip()[-800:], "ausgabe": _puffer.getvalue()[:500]})
_sende({"ende": True})
""";

    /// <summary>Echte Tests der Aufgabe gegen <paramref name="code"/> im abgeschotteten Lauf ausführen. <paramref name="arbeit"/> wird angelegt und danach gelöscht.</summary>
    public static Bericht Testen(Aufgabe a, string code, string python, string arbeit, int timeoutMs = 10_000) =>
        Testen(a, code, python, arbeit, new LernSandbox.Limits(timeoutMs));

    public static Bericht Testen(Aufgabe a, string code, string python, string arbeit, LernSandbox.Limits lim)
    {
        Directory.CreateDirectory(arbeit);
        try
        {
            File.WriteAllText(Path.Combine(arbeit, "loesung.py"), code, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(arbeit, "faelle.json"), new JsonArray([.. a.Faelle.Select(f => (JsonNode)new JsonObject { ["name"] = f.Name, ["aufruf"] = f.Aufruf })]).ToJsonString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(arbeit, "lauf.py"), Runner, new UTF8Encoding(false));
            var r = LernSandbox.Run(python, ["-I", "-B", "-X", "utf8", "lauf.py"], arbeit, lim);
            return Auswerten(a, r, arbeit);
        }
        finally { try { Directory.Delete(arbeit, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
    }

    [GeneratedRegex(@"File ""[^""]*[\\/]([^\\/""]+)""")] private static partial Regex FilePaths();

    /// <summary>Pfade in Meldungen kürzen (nur Dateiname) – keine Ordnernamen des Rechners in Anzeige oder Belegen.</summary>
    public static string Kurz(string s) => FilePaths().Replace(s, "File \"$1\"");

    /// <summary>Meldung kürzen (M5): höchstens <paramref name="max"/> Zeichen, ohne Steuerzeichen.</summary>
    public static string Knapp(string? s, int max = 300)
    {
        s = new string((s ?? "").Where(c => c is '\n' or '\t' || !char.IsControl(c)).ToArray());
        return s.Length > max ? s[..max] + " …" : s;
    }

    static string Zeige(JsonNode? n) => n is null ? "None" : Knapp(PyLiteral(n), 120);

    /// <summary>Zahlen gleich behandeln (5 und 5.0), sonst Struktur vergleichen.</summary>
    public static bool Gleich(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a is JsonValue va && b is JsonValue vb)
        {
            var ka = va.GetValueKind(); var kb = vb.GetValueKind();
            if (ka == JsonValueKind.Number && kb == JsonValueKind.Number) return va.GetValue<double>() == vb.GetValue<double>();
            return ka == kb && va.ToJsonString() == vb.ToJsonString();
        }
        if (a is JsonArray xa && b is JsonArray xb) return xa.Count == xb.Count && xa.Zip(xb).All(p => Gleich(p.First, p.Second));
        if (a is JsonObject oa && b is JsonObject ob) return oa.Count == ob.Count && oa.All(kv => ob.TryGetPropertyValue(kv.Key, out var v) && Gleich(kv.Value, v));
        return false;
    }

    /// <summary>Ergebnis eines Laufs bewerten – die erwarteten Werte stehen nur hier. Kaputte Zeilen werden übergangen (M5), Meldungen gekürzt.</summary>
    public static Bericht Auswerten(Aufgabe a, LernSandbox.Result r, string arbeit = "")
    {
        string Clean(string s) => Knapp(arbeit.Length > 0 ? Kurz(s).Replace(arbeit, ".") : Kurz(s), 800);
        if (r.TimedOut) return new("timeout", [], $"Zeitgrenze überschritten ({r.Seconds:0.#} s) – läuft eine Schleife endlos?", r.Seconds);
        if (r.OutputLimit) return new("limit", [], "Abgebrochen: zu viel Ausgabe oder zu große Dateien – schreibt der Code in einer Schleife ohne Ende?", r.Seconds);
        if (r.MemoryLimit) return new("error", [], "Speichergrenze erreicht – legt der Code riesige Listen an?", r.Seconds);
        var zeilen = new Dictionary<string, JsonObject>(StringComparer.Ordinal); string? fehler = null; var ende = false;
        foreach (var line in r.Out.Split('\n'))
        {
            var at = line.IndexOf(Marke, StringComparison.Ordinal); if (at < 0) continue;
            JsonObject? o;
            try { o = JsonNode.Parse(line[(at + Marke.Length)..].Trim()) as JsonObject; }
            catch (JsonException) { continue; } // M5: kaputte Zeile → übergehen
            if (o is null) continue;
            if (o["fehler"] is JsonValue fv && fv.GetValueKind() == JsonValueKind.String) fehler ??= fv.GetValue<string>();
            else if (o["ende"] is not null) ende = true;
            else if (o["name"] is JsonValue nv && nv.GetValueKind() == JsonValueKind.String && !zeilen.ContainsKey(nv.GetValue<string>())) zeilen[nv.GetValue<string>()] = o;
        }
        if (fehler is not null) return new("error", [], fehler.Contains("MemoryError", StringComparison.Ordinal) ? "Speichergrenze erreicht – legt der Code riesige Listen an?" : Freundlich(Clean(fehler)), r.Seconds); // N2
        if (zeilen.Count == 0 && !ende) return new("error", [], Clean(("Der Testlauf hat kein Ergebnis geliefert. " + r.Text + "\n" + r.Err).Trim()), r.Seconds);
        var tests = new List<TestErgebnis>();
        foreach (var f in a.Faelle)
        {
            string Txt(JsonObject o, string k) => o[k] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : "";
            if (!zeilen.TryGetValue(f.Name, out var o)) { tests.Add(new(f.Name, "error", "Keine Rückmeldung für diesen Test (der Lauf endete vorher).", "")); continue; }
            var ausgabe = Knapp(Txt(o, "ausgabe"), 500);
            if (Txt(o, "ausnahme") is { Length: > 0 } ex) { tests.Add(new(f.Name, "error", Freundlich(Clean(ex)), ausgabe)); continue; }
            var soll = JsonNode.Parse(f.Erwartet);
            var ist = o["wert"];
            bool ok;
            string meldung;
            try
            {
                switch (f.Art)
                {
                    case "typ":
                        ok = Txt(o, "typ") == soll!.GetValue<string>();
                        meldung = ok ? "" : $"Erwartet wurde ein Wert vom Typ {soll!.GetValue<string>()}, bekommen: {Knapp(Txt(o, "typ"), 40)}.";
                        break;
                    case "fast":
                        ok = ist is JsonValue iv && iv.GetValueKind() == JsonValueKind.Number && Math.Abs(iv.GetValue<double>() - soll!.GetValue<double>()) < 1e-7;
                        meldung = ok ? "" : $"{f.Aufruf} ergab {Zeige(ist)}, erwartet etwa {Zeige(soll)}.";
                        break;
                    default:
                        ok = Gleich(ist, soll);
                        meldung = ok ? "" : $"{f.Aufruf} ergab {(o["repr"] is JsonValue rv ? Knapp(rv.ToString(), 120) : Zeige(ist))}, erwartet {Zeige(soll)}.";
                        break;
                }
            }
            catch (Exception e) when (e is InvalidOperationException or FormatException or JsonException) { ok = false; meldung = "Der Wert ließ sich nicht prüfen."; }
            tests.Add(new(f.Name, ok ? "pass" : "fail", Knapp(meldung, 400), ausgabe));
        }
        var status = tests.All(t => t.Status == "pass") ? "pass" : "fail";
        return new(status, tests, "", r.Seconds);
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

    /// <summary>Frage an die KI zur Aufgabe. T-0782 (Gegenprüfung M2): Programmausgabe und Testmeldungen (enthalten Werte, die der Lerncode erzeugt hat – z. B.
    /// Rechner- oder Benutzername) gehen nur mit <paramref name="mitAusgabe"/> mit – das setzt Trendo nur für die KI auf diesem PC; sonst nur Testnamen und Status.</summary>
    public static string KiFrage(ProgramDocument d, string frage, bool mitAusgabe = false)
    {
        var a = Aktuell(d); var l = Letzter(d);
        var sb = new StringBuilder($"Aufgabe „{a.Titel}“ (Konzept: {KonzeptName(a.Konzept)}):\n{a.Anleitung}\n\nAktueller Code der lernenden Person:\n{Code(d)}\n");
        if (l is not null)
        {
            sb.Append($"\nLetzter Testlauf: {l.Status}, {l.Bestanden}/{l.Tests.Count} bestanden. Nicht bestanden: {string.Join(", ", l.Tests.Where(t => t.Status != "pass").Select(t => t.Name))}.");
            if (mitAusgabe) sb.Append(" " + Knapp(l.Meldung, 300) + " " + string.Join(" ", l.Tests.Where(t => t.Status != "pass").Take(3).Select(t => $"{t.Name}: {Knapp(t.Meldung, 200)}")));
            sb.Append('\n');
        }
        sb.Append($"\nFrage: {frage}");
        return sb.ToString();
    }

    /// <summary>Lösung als Python-Datei mit den Tests darunter – läuft in jedem Python mit „python datei.py“.</summary>
    public static string Export(ProgramDocument d)
    {
        var a = Aktuell(d);
        var tests = a.Tests.Replace("from loesung import *\n", "");
        return $"# Programmieren lernen: {a.Titel}\n# Aufgabe: {a.Anleitung.Split('\n')[0]}\n\n{Code(d).TrimEnd()}\n\n\n# ---- Tests der Aufgabe ----\n{tests.TrimEnd()}\n\n\nif __name__ == \"__main__\":\n    unittest.main(verbosity=2)\n";
    }

    /// <summary>Eigene Python-Datei als Code der aktuellen Aufgabe einlesen (Tests aus einer exportierten Datei werden abgeschnitten).</summary>
    public static string ImportText(string text)
    {
        if (text.Length > 100_000) throw new InvalidDataException("Die Datei ist zu groß (höchstens 100 000 Zeichen).");
        text = text.Replace("\r\n", "\n").TrimStart('﻿');
        var cut = text.IndexOf("# ---- Tests der Aufgabe ----", StringComparison.Ordinal);
        if (cut >= 0) text = text[..cut];
        var lines = text.Split('\n').SkipWhile(l => l.StartsWith("# Programmieren lernen", StringComparison.Ordinal) || l.StartsWith("# Trendo – Programmieren lernen", StringComparison.Ordinal) || l.StartsWith("# Aufgabe:", StringComparison.Ordinal)).ToList();
        return string.Join("\n", lines).Trim('\n') + "\n";
    }
}
