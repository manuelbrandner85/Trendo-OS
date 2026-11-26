using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Trendo.Core.Programs;

namespace Trendo.Core.Lernen;

/// <summary>T-0782 (Gegenprüfung M6–M8, N5): Antwortprüfung, Schutz vor verratenen Lösungen, Zählung von Versuchen außerhalb der Rückgängig-Liste,
/// und die Sicht der KI auf das Dokument (ohne Lösungen).</summary>
public static partial class Nachhilfe
{
    /// <param name="Zaehlt">false = Eingabe war leer, unlesbar oder im falschen Format – das ist kein Versuch.</param>
    public sealed record Pruefung(bool Richtig, string Rueckmeldung, bool Zaehlt = true)
    {
        public void Deconstruct(out bool richtig, out string rueckmeldung) { richtig = Richtig; rueckmeldung = Rueckmeldung; }
    }

    [GeneratedRegex(@"^[\p{L}]\s*=\s*")] private static partial Regex Assignment();
    [GeneratedRegex(@"^(.*?\d)\s*([\p{L}²³]+)$")] private static partial Regex WithUnit();
    [GeneratedRegex(@"\d\s*[eE]\s*[+-]?\d")] private static partial Regex Exponent();
    [GeneratedRegex(@"^(-?\d+)\s*/\s*(-?\d+)$")] private static partial Regex FractionLoose();
    [GeneratedRegex(@"^(-?\d+)\s*(?:,\s*)?(?:r|rest)\s*(\d+)$")] private static partial Regex RestForm();
    [GeneratedRegex(@"[\p{L}\p{N}]")] private static partial Regex AnyWordChar();

    /// <summary>Minuszeichen, geschützte Leerzeichen und Malpunkte vereinheitlichen.</summary>
    static string Zeichen(string s) => s.Replace('−', '-').Replace('–', '-').Replace('‒', '-').Replace(' ', ' ').Replace(' ', ' ').Replace(' ', ' ');

    /// <summary>Zahl lesen (Komma oder Punkt als Dezimalzeichen, Vorzeichen, keine Tausenderpunkte, keine e-Schreibweise). Überlauf → false.</summary>
    public static bool ZahlLesen(string s, out decimal v)
    {
        v = 0;
        s = Zeichen(s).Trim().Replace(" ", "");
        if (s.Length == 0 || s.Length > 40 || Exponent().IsMatch(s) || s.Count(c => c is ',' or '.') > 1) return false;
        return decimal.TryParse(s.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out v);
    }

    static bool Bruch(string s, out long z, out long n)
    {
        z = n = 0;
        var m = FractionLoose().Match(Zeichen(s).Trim());
        if (!m.Success || !long.TryParse(m.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out z) || !long.TryParse(m.Groups[2].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out n)) return false;
        if (n < 0) { z = -z; n = -n; } // -3/-4 = 3/4, 3/-4 = -3/4
        return true;
    }

    static bool Rest(string s, out long q, out long r)
    {
        q = r = 0; s = Zeichen(s).Trim().ToLowerInvariant();
        var m = RestForm().Match(s);
        if (m.Success) return long.TryParse(m.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out q) && long.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out r);
        return long.TryParse(s.Replace(" ", ""), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out q); // „4“ = 4 Rest 0
    }

    /// <summary>Soll-Antwort vereinheitlichen (beim Anlegen und Einlesen): Minus, Leerzeichen im Bruch, Vorzeichen im Nenner.</summary>
    public static string NormSoll(string a)
    {
        a = Zeichen(a).Trim();
        if (Bruch(a, out var z, out var n) && n != 0) return $"{z}/{n}";
        if (ZahlLesen(a, out var v)) return v.ToString(CultureInfo.InvariantCulture);
        if (Rest(a, out var q, out var r) && RestForm().IsMatch(a.ToLowerInvariant())) return r == 0 ? $"{q}" : $"{q} R {r}";
        return a;
    }

    static string ArtVon(IReadOnlyList<string> antworten) =>
        antworten.All(a => ZahlLesen(a, out _)) ? "zahl" : antworten.All(a => Bruch(a, out _, out var n) && n != 0) ? "bruch" : "text";

    /// <summary>Prüft eine Eingabe gegen die erlaubten Antworten. Format-Probleme (leer, keine Zahl, e-Schreibweise, Dezimalzahl statt Bruch, falsche Einheit)
    /// zählen nicht als Versuch; „fast richtig“ (Bruch nicht ganz gekürzt) zählt als falsch mit Hinweis.</summary>
    public static Pruefung Vergleiche(Item i, string eingabe)
    {
        var roh = Zeichen(eingabe ?? "").Trim();
        if (roh.Length == 0) return new(false, "Schreib zuerst eine Antwort.", false);
        if (!AnyWordChar().IsMatch(roh)) return new(false, "Das ist noch keine Antwort – schreib eine Zahl oder ein Wort.", false);
        try
        {
            switch (i.Art)
            {
                case "zahl":
                {
                    var e = Assignment().Replace(roh, "").Trim().TrimEnd('.');
                    if (Exponent().IsMatch(e)) return new(false, "Bitte schreib die Zahl ohne e-Schreibweise, z. B. 21.", false);
                    if (WithUnit().Match(e) is { Success: true } mu)
                    {
                        var unit = mu.Groups[2].Value;
                        if (i.Einheit is { } soll && string.Equals(unit, soll, StringComparison.OrdinalIgnoreCase)) e = mu.Groups[1].Value;
                        else if (i.Einheit is { } s2) return new(false, $"Die Einheit passt nicht – gefragt ist {s2}.", false);
                        else return new(false, "Hier ist nur eine Zahl gefragt, ohne Einheit.", false);
                    }
                    if (!ZahlLesen(e, out var v)) return new(false, "Hier ist eine Zahl gefragt.", false);
                    return i.Antworten.Any(a => ZahlLesen(a, out var w) && Math.Abs(v - w) < 0.000001m) ? new(true, "Richtig!") : new(false, "Noch nicht richtig.");
                }
                case "bruch":
                {
                    if (!Bruch(roh, out var z, out var n))
                        return ZahlLesen(roh, out _) ? new(false, "Gefragt ist ein Bruch wie 3/4 – keine Kommazahl.", false) : new(false, "Schreib den Bruch als Zähler/Nenner, z. B. 3/4.", false);
                    if (n == 0) return new(false, "Durch null kann man nicht teilen – der Nenner darf nicht 0 sein.", false);
                    if (!Bruch(i.Antworten[0], out var sz, out var sn) || sn == 0) return new(false, "Diese Übung hat keine gültige Lösung.", false);
                    if (z == sz && n == sn) return new(true, "Richtig!");
                    if ((decimal)z * sn == (decimal)sz * n) return new(false, "Der Wert stimmt, aber du kannst noch weiter kürzen.");
                    return new(false, "Noch nicht richtig.");
                }
                case "rest":
                {
                    if (!Rest(roh, out var q, out var r)) return new(false, "Schreib das Ergebnis wie „3 R 2“ (oder nur die Zahl, wenn kein Rest bleibt).", false);
                    return i.Antworten.Any(a => Rest(a, out var aq, out var ar) && aq == q && ar == r) ? new(true, "Richtig!") : new(false, "Noch nicht richtig.");
                }
                default:
                {
                    var e = Norm(roh);
                    string Loose(string s) => Fold(Norm(s));
                    var hit = i.Antworten.FirstOrDefault(a => Loose(a) == Loose(e));
                    if (hit is null) return new(false, "Noch nicht richtig.");
                    // N5: Großschreibung – richtig, aber mit Hinweis, wenn die Musterantwort anders geschrieben ist (z. B. Nomen groß)
                    var exact = i.Antworten.Any(a => string.Equals(Spaces().Replace(a.Trim(), " "), Spaces().Replace(roh.Trim().Trim('.', '!'), " "), StringComparison.Ordinal)
                                                     || string.Equals(FoldCase(a), FoldCase(roh), StringComparison.Ordinal));
                    return exact || !hit.Any(char.IsUpper) && !roh.Any(char.IsUpper) ? new(true, "Richtig!") : new(true, $"Richtig! Achte aber auf die Groß- und Kleinschreibung: „{hit}“.");
                }
            }
        }
        catch (Exception ex) when (ex is OverflowException or FormatException) { return new(false, "Diese Eingabe kann Trendo nicht lesen – bitte kürzer oder einfacher schreiben.", false); }
    }

    static string FoldCase(string s) => Spaces().Replace(s.Trim().Trim('.', '!'), " ").Replace("ae", "ä").Replace("oe", "ö").Replace("ue", "ü");

    /// <summary>Kleinbuchstaben, Umlaute als ae/oe/ue/ss (so gilt „baeume“ wie „Bäume“).</summary>
    public static string Fold(string s) => s.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");

    // ───────────── Lösungen in Texten erkennen (M7) ─────────────

    static readonly Lazy<List<(string Wort, int Zahl)>> Zahlwoerter = new(() =>
    {
        string[] e = ["null", "eins", "zwei", "drei", "vier", "fuenf", "sechs", "sieben", "acht", "neun", "zehn", "elf", "zwoelf", "dreizehn", "vierzehn", "fuenfzehn", "sechzehn", "siebzehn", "achtzehn", "neunzehn"];
        string[] z = ["", "", "zwanzig", "dreissig", "vierzig", "fuenfzig", "sechzig", "siebzig", "achtzig", "neunzig"];
        string[] pre = ["", "ein", "zwei", "drei", "vier", "fuenf", "sechs", "sieben", "acht", "neun"];
        var l = new List<(string, int)>();
        for (var n = 0; n < 20; n++) l.Add((e[n], n));
        l.Add(("ein", 1)); l.Add(("eine", 1));
        for (var t = 2; t < 10; t++) { l.Add((z[t], t * 10)); for (var u = 1; u < 10; u++) l.Add((pre[u] + "und" + z[t], t * 10 + u)); }
        l.Add(("hundert", 100)); l.Add(("einhundert", 100)); l.Add(("tausend", 1000));
        return [.. l.OrderByDescending(x => x.Item1.Length)];
    });

    [GeneratedRegex(@"(?<=\b\p{L})[\s\-\._,*/·]+(?=\p{L}\b)")] private static partial Regex LetterGaps();
    [GeneratedRegex(@"-?\d+(?:\.\d+)?(?:\s*[-+*/]\s*-?\d+(?:\.\d+)?)+")] private static partial Regex Expression();
    [GeneratedRegex(@"-?\d+(?:\.\d+)?")] private static partial Regex Number();

    /// <summary>Text für den Vergleich vereinheitlichen: klein, Umlaute gefaltet, Minus, Dezimalkomma → Punkt, Malpunkt → *, „:“ zwischen Zahlen → /.</summary>
    static string Flach(string s)
    {
        s = Fold(Zeichen(OhneLatex(s)));
        s = Regex.Replace(s, @"(?<=\d),(?=\d)", ".");
        s = s.Replace('·', '*').Replace('×', '*').Replace('÷', '/');
        s = Regex.Replace(s, @"(?<=\d)\s*:\s*(?=\d)", " / ");
        return Spaces().Replace(s, " ");
    }

    static string Zahlwort(string s)
    {
        foreach (var (w, n) in Zahlwoerter.Value) s = Regex.Replace(s, @"(?<![\p{L}])" + w + @"(?![\p{L}])", n.ToString(CultureInfo.InvariantCulture));
        return s;
    }

    /// <summary>Einfacher Rechner für „20 + 1“, „3 * 7 - 0“ (Punkt vor Strich, links nach rechts). Null bei Fehlern.</summary>
    public static decimal? Rechne(string expr)
    {
        try
        {
            var toks = Regex.Matches(expr.Replace(" ", ""), @"(?<=^|[-+*/])-?\d+(?:\.\d+)?|[-+*/]").Select(m => m.Value).ToList();
            if (toks.Count == 0 || toks.Count % 2 == 0) return null;
            var terms = new List<decimal>(); var ops = new List<char>();
            decimal cur = decimal.Parse(toks[0], CultureInfo.InvariantCulture);
            for (var k = 1; k < toks.Count; k += 2)
            {
                var op = toks[k][0]; var v = decimal.Parse(toks[k + 1], CultureInfo.InvariantCulture);
                if (op == '*') cur *= v; else if (op == '/') { if (v == 0) return null; cur /= v; }
                else { terms.Add(cur); ops.Add(op); cur = v; }
            }
            terms.Add(cur);
            var res = terms[0];
            for (var k = 0; k < ops.Count; k++) res = ops[k] == '+' ? res + terms[k + 1] : res - terms[k + 1];
            return res;
        }
        catch (Exception e) when (e is OverflowException or FormatException or DivideByZeroException) { return null; }
    }

    /// <summary>Enthält ein Text die Lösung? Erkennt auch Schreibvarianten: Rest („3 Rest 2“), Bruch mit Leerzeichen, Zahlwörter („einundzwanzig“),
    /// Unicode-Minus, Punkt/Komma, gesperrte Buchstaben („w-e-n-t“), ae statt ä und Rechnungen, die die Lösung ergeben („20 + 1“).</summary>
    public static bool VerraetLoesung(string text, IEnumerable<string> antworten)
    {
        var flach = Flach(text);
        var mitZahlen = Zahlwort(flach);
        var gesperrt = LetterGaps().Replace(flach, "");
        foreach (var a0 in antworten.Select(a => NormSoll(a ?? "")).Where(a => a.Length > 0))
        {
            var a = Flach(a0);
            if (Bruch(a0, out var z, out var n) && n != 0)
            {
                foreach (var t in new[] { mitZahlen })
                    foreach (Match m in Regex.Matches(t, @"-?\d+\s*/\s*-?\d+"))
                        if (Bruch(m.Value, out var tz, out var tn) && tz == z && tn == n) return true;
                continue;
            }
            if (Rest(a0, out var q, out var r) && RestForm().IsMatch(a0.ToLowerInvariant()))
            {
                if (Regex.IsMatch(mitZahlen, $@"(?<!\d){q}\s*(?:,\s*)?(?:r|rest)\s*{r}(?!\d)")) return true;
                continue;
            }
            if (ZahlLesen(a0, out var v))
            {
                foreach (Match m in Expression().Matches(mitZahlen)) if (Rechne(m.Value) is { } res && res == v) return true;
                foreach (Match m in Number().Matches(mitZahlen)) if (decimal.TryParse(m.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var w) && w == v) return true;
                continue;
            }
            var pattern = @"(?<![\p{L}\p{N}])" + Regex.Escape(a) + @"(?![\p{L}\p{N}])";
            if (Regex.IsMatch(flach, pattern) || Regex.IsMatch(gesperrt, pattern)) return true;
        }
        return false;
    }

    // ───────────── Versuche außerhalb der Rückgängig-Liste (M6) ─────────────

    /// <summary>Kennung einer Übung (ältere Dokumente ohne Kennung: aus Thema und Frage).</summary>
    static string Uid(JsonObject u) => u["uid"].AsText() is { Length: > 0 } id ? id : "f" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes((u["thema"].AsText() ?? "") + "|" + (u["frage"].AsText() ?? ""))))[..12];

    static JsonObject MetaFor(ProgramDocument d, JsonObject u, bool create)
    {
        var all = d.Meta["nachhilfe"] as JsonObject;
        if (all is null) { if (!create) return []; d.EditMeta(m => m["nachhilfe"] = new JsonObject()); all = (JsonObject)d.Meta["nachhilfe"]!; }
        var id = Uid(u);
        if (all[id] is JsonObject o) return o;
        if (!create) return [];
        var n = new JsonObject { ["versuche"] = 0, ["gesehen"] = false };
        d.EditMeta(_ => { all[id] = n; while (all.Count > 500) all.Remove(all.First().Key); });
        return n;
    }

    /// <summary>Versuche dieser Übung – zählt auch, was mit Strg+Z zurückgenommen wurde (steht nicht in der Rückgängig-Liste).</summary>
    public static int VersucheEcht(ProgramDocument d) => Uebung(d) is { } u ? Math.Max((int)ProgramDocument.ToNumber(MetaFor(d, u, false)["versuche"]), (int)ProgramDocument.ToNumber(u["versuche"])) : 0;
    /// <summary>Wurde der Lösungsweg dieser Übung schon gezeigt – auch wenn das danach rückgängig gemacht wurde?</summary>
    public static bool LoesungGesehen(ProgramDocument d) => Uebung(d) is { } u && (MetaFor(d, u, false)["gesehen"].AsBool() || u["loesungGezeigt"].AsBool());

    // ───────────── Sicht der KI (M6) ─────────────

    /// <summary>Was die KI-Kette vom Dokument sieht: ohne Antworten, Lösungswege, Hinweise, Eingaben und Verlauf.</summary>
    public static JsonObject KiSicht(JsonObject data)
    {
        var c = data.DeepClone().AsObject();
        if (c["uebung"] is JsonObject u) foreach (var k in new[] { "antworten", "erklaerung", "hinweise", "eingaben", "uid" }) u.Remove(k);
        if (c["eigene"] is JsonArray e) foreach (var o in e.OfType<JsonObject>()) foreach (var k in new[] { "antworten", "erklaerung", "hinweise" }) o.Remove(k);
        c.Remove("verlauf");
        return c;
    }
}
