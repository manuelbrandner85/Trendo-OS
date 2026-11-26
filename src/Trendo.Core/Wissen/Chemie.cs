using System.Globalization;
using System.Numerics;
using System.Text;

namespace Trendo.Core.Wissen;

/// <summary>Eine Summenformel: Anzahl je Element (in der Reihenfolge des ersten Auftretens) und Ladung.</summary>
public sealed record Formel(string Text, IReadOnlyList<(string Symbol, int Anzahl)> Atome, int Ladung)
{
    public int Anzahl(string s) => Atome.Where(a => a.Symbol == s).Sum(a => a.Anzahl);
}

/// <summary>Molmasse: Zeilen der Rechnung (Element, Anzahl, Atommasse, Beitrag, Massenanteil) und Summe in g/mol.</summary>
public sealed record MolmasseErgebnis(Formel Formel, IReadOnlyList<(Element E, int Anzahl, double Beitrag, double Anteil)> Zeilen, double Summe, bool Geschaetzt);

/// <summary>Ausgeglichene Reaktion: Koeffizienten links/rechts, Lösungsweg, Atombilanz als Probe.</summary>
public sealed record Reaktion(string Eingabe, IReadOnlyList<(int K, Formel F)> Links, IReadOnlyList<(int K, Formel F)> Rechts, IReadOnlyList<Schritt> Schritte, IReadOnlyList<Probe> Proben)
{
    public string Gleichung => Seite(Links) + " → " + Seite(Rechts);
    static string Seite(IReadOnlyList<(int K, Formel F)> s) => string.Join(" + ", s.Select(x => (x.K == 1 ? "" : x.K + " ") + Chemie.Schoen(x.F.Text)));
    public bool ProbeOk => Proben.Count > 0 && Proben.All(p => p.Ok);
}

/// <summary>T-0781 Naturwissenschaft: Summenformeln lesen (Klammern, Kristallwasser „·5H2O“, Ladungen „SO4^2-“), Molmasse mit Rechenweg,
/// Reaktionsgleichungen ausgleichen über das Gleichungssystem der Atombilanzen (exakt mit Brüchen), Probe: jedes Element links = rechts, Ladung links = rechts.</summary>
public static class Chemie
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    public static Formel Lies(string text)
    {
        var t = (text ?? "").Trim().Replace(" ", "");
        if (t.Length == 0) throw new FormatException("Bitte eine Summenformel eingeben, z. B. H2SO4.");
        if (t.Length > 200) throw new FormatException("Die Formel ist zu lang.");
        // Ladung am Ende: ^2-, ^+, {3+}, „+“/„−“ nach ^
        var ladung = 0; string? anzeige = null;
        var dach = t.IndexOfAny(['^', '{']);
        if (dach >= 0)
        {
            var l = t[(dach + 1)..].TrimEnd('}').Replace('−', '-');
            t = t[..dach];
            var zeichen = l.EndsWith('+') ? 1 : l.EndsWith('-') ? -1 : l.StartsWith('+') ? 1 : l.StartsWith('-') ? -1 : 0;
            if (zeichen == 0) throw new FormatException($"Ladung „{l}“ nicht verstanden – schreib z. B. SO4^2- oder Fe^3+.");
            var ziffern = l.Trim('+', '-');
            ladung = zeichen * (ziffern.Length == 0 ? 1 : int.Parse(ziffern, CultureInfo.InvariantCulture));
        }
        if (t is "e" or "e-") return new Formel("e^-", [], -1);
        // T-0781 (Gegenprüfung): Ladung ohne „^“ am Ende („Cu2+“, „Na+“, „NH4+“, „SO42-“). Einatomige Ionen: alle Ziffern davor sind die Ladung (Cu2+ = Cu²⁺);
        // mehratomige: eine einzelne Ziffer gehört zur Formel (NH4+ = NH₄⁺), bei zwei Ziffern ist die letzte die Ladung (SO42- = SO₄²⁻)
        if (dach < 0 && t.Length > 1 && t[^1] is '+' or '-' && !t.Contains('·') && !t.Contains('*'))
        {
            var zeichen = t[^1] == '+' ? 1 : -1; var rumpf = t[..^1];
            var z = rumpf.Length; while (z > 0 && char.IsAsciiDigit(rumpf[z - 1])) z--;
            var ziffern = rumpf[z..]; var vorne = rumpf[..z];
            var einatomig = System.Text.RegularExpressions.Regex.IsMatch(vorne, "^[A-Z][a-z]?$");
            var betrag = 1;
            if (einatomig && ziffern.Length > 0) { betrag = int.Parse(ziffern, CultureInfo.InvariantCulture); rumpf = vorne; }
            else if (!einatomig && ziffern.Length >= 2) { betrag = ziffern[^1] - '0'; rumpf = vorne + ziffern[..^1]; }
            if (betrag is < 1 or > 9) throw new FormatException($"Ladung „{t}“ nicht verstanden – schreib z. B. {vorne}^{betrag}{t[^1]}.");
            t = rumpf; ladung = zeichen * betrag; anzeige = rumpf + "^" + (betrag > 1 ? betrag.ToString(CultureInfo.InvariantCulture) : "") + (zeichen > 0 ? "+" : "-");
        }
        var atome = new Dictionary<string, int>(StringComparer.Ordinal); var reihe = new List<string>();
        foreach (var teil in t.Split(['·', '*', '•', '.'], StringSplitOptions.RemoveEmptyEntries))
        {
            // Kristallwasser „5H2O“: Zahl vorne multipliziert den Teil
            var i = 0; var vorne = 1;
            if (teil.Length > 0 && char.IsAsciiDigit(teil[0])) { while (i < teil.Length && char.IsAsciiDigit(teil[i])) i++; vorne = int.Parse(teil[..i], CultureInfo.InvariantCulture); }
            var gruppe = Gruppe(teil, ref i, 0);
            if (i != teil.Length) throw new FormatException($"Unerwartetes Zeichen „{teil[i]}“ in „{text}“.");
            foreach (var (s, n) in gruppe) { if (!atome.ContainsKey(s)) { atome[s] = 0; reihe.Add(s); } atome[s] += n * vorne; }
        }
        if (reihe.Count == 0) throw new FormatException("In der Formel steht kein Element.");
        return new Formel(anzeige ?? text!.Trim(), [.. reihe.Select(s => (s, atome[s]))], ladung);
    }

    static List<(string, int)> Gruppe(string t, ref int i, int tiefe)
    {
        if (tiefe > 8) throw new FormatException("Zu viele verschachtelte Klammern.");
        var r = new List<(string, int)>();
        while (i < t.Length)
        {
            var c = t[i];
            if (c is '(' or '[')
            {
                i++;
                var inner = Gruppe(t, ref i, tiefe + 1);
                if (i >= t.Length || t[i] is not (')' or ']')) throw new FormatException("Eine Klammer wird nicht geschlossen.");
                i++;
                var n = Zahl(t, ref i);
                r.AddRange(inner.Select(x => (x.Item1, x.Item2 * n)));
                continue;
            }
            if (c is ')' or ']') { if (tiefe == 0) throw new FormatException("Eine schließende Klammer ist zu viel."); break; }
            if (char.IsAsciiLetterUpper(c))
            {
                var st = i++;
                while (i < t.Length && char.IsAsciiLetterLower(t[i])) i++;
                var sym = t[st..i];
                // „Co“ ist Cobalt, „CO“ Kohlenstoffmonoxid – bei unbekanntem zweiten Buchstaben ist es kein Element
                if (Elemente.Symbol(sym) is null)
                {
                    if (sym.Length > 1 && Elemente.Symbol(sym[..1]) is not null) throw new FormatException($"„{sym}“ ist kein Element. Achte auf Groß- und Kleinschreibung (z. B. Co = Cobalt, CO = Kohlenstoff + Sauerstoff).");
                    throw new FormatException($"„{sym}“ ist kein Element.");
                }
                r.Add((sym, Zahl(t, ref i)));
                continue;
            }
            if (char.IsAsciiLetterLower(c)) throw new FormatException($"Elementsymbole beginnen mit einem Großbuchstaben – „{c}“ an Stelle {i + 1}.");
            throw new FormatException($"Unerwartetes Zeichen „{c}“.");
        }
        return r;
    }

    static int Zahl(string t, ref int i)
    {
        var st = i; while (i < t.Length && char.IsAsciiDigit(t[i])) i++;
        if (st == i) return 1;
        var n = int.Parse(t[st..i], CultureInfo.InvariantCulture);
        return n is > 0 and <= 10000 ? n : throw new FormatException("Anzahl 1 bis 10000.");
    }

    /// <summary>Tiefgestellte Ziffern für die Anzeige („H₂SO₄“, „SO₄²⁻“).</summary>
    public static string Schoen(string formel)
    {
        var sb = new StringBuilder(); var dach = false;
        for (var k = 0; k < formel.Length; k++)
        {
            var c = formel[k];
            if (c is '^' or '{') { dach = true; continue; }
            if (c == '}') continue;
            if (dach) { sb.Append(c switch { '+' => '⁺', '-' or '−' => '⁻', >= '0' and <= '9' => "⁰¹²³⁴⁵⁶⁷⁸⁹"[c - '0'], _ => c }); continue; }
            // Ziffern nach einem Buchstaben oder einer Klammer tiefstellen, Ziffern am Anfang (Koeffizient, Kristallwasser) nicht
            var tief = char.IsAsciiDigit(c) && k > 0 && (char.IsLetter(formel[k - 1]) || formel[k - 1] is ')' or ']' || char.IsAsciiDigit(formel[k - 1]) && sb.Length > 0 && "₀₁₂₃₄₅₆₇₈₉".Contains(sb[^1]));
            sb.Append(tief ? "₀₁₂₃₄₅₆₇₈₉"[c - '0'] : c == '*' || c == '.' ? '·' : c);
        }
        return sb.ToString();
    }

    public static MolmasseErgebnis Molmasse(string text)
    {
        var f = Lies(text);
        var summe = f.Atome.Sum(a => a.Anzahl * Elemente.Symbol(a.Symbol)!.Masse);
        var zeilen = f.Atome.Select(a => { var e = Elemente.Symbol(a.Symbol)!; var b = a.Anzahl * e.Masse; return (e, a.Anzahl, b, b / summe); }).ToList();
        return new MolmasseErgebnis(f, zeilen, summe, zeilen.Any(z => z.e.MasseGeschaetzt));
    }

    public static IReadOnlyList<Schritt> MolmasseWeg(MolmasseErgebnis m)
    {
        var s = new List<Schritt> { new("Atome zählen", string.Join(", ", m.Zeilen.Select(z => $"{z.E.Symbol}: {z.Anzahl}"))) };
        s.Add(new Schritt("Anzahl mal Atommasse je Element", string.Join(" + ", m.Zeilen.Select(z => $"{z.Anzahl} · {z.E.MasseText} g/mol"))));
        s.Add(new Schritt("Zusammenzählen", $"M({Schoen(m.Formel.Text)}) = {G(m.Summe)} g/mol"));
        s.Add(new Schritt("Massenanteile", string.Join(", ", m.Zeilen.Select(z => $"{z.E.Symbol} {(z.Anteil * 100).ToString("0.00", De)} %"))));
        return s;
    }

    public static string G(double v) => v.ToString(v >= 1000 ? "#,##0.0" : "0.0##", De);

    // ---------------- Ausgleichen ----------------
    static readonly string[] Pfeile = ["<=>", "⇌", "->", "→", "⟶", "=>", "="];

    public static Reaktion Ausgleichen(string eingabe)
    {
        var text = (eingabe ?? "").Trim();
        var pfeil = Pfeile.FirstOrDefault(p => text.Contains(p, StringComparison.Ordinal)) ?? throw new FormatException("Zwischen Ausgangsstoffen und Produkten fehlt der Pfeil, z. B. H2 + O2 -> H2O.");
        var seiten = text.Split(pfeil);
        if (seiten.Length != 2) throw new FormatException("Bitte genau einen Pfeil.");
        // „+“ trennt Stoffe nur, wenn danach ein neuer Stoff beginnt (Buchstabe/Ziffer); ein „+“ am Ende ist die Ladung (Cu2+)
        List<Formel> Stoffe(string seite) => [.. System.Text.RegularExpressions.Regex.Split(seite.Trim(), @"\s+\+\s+|(?<=[A-Za-z0-9)\]])\+(?=[A-Za-z0-9(\[])")
            .Select(x => x.Trim()).Where(x => x.Length > 0).Select(x => { var i = 0; while (i < x.Length && char.IsAsciiDigit(x[i])) i++; return Lies(x[i..]); })];
        var links = Stoffe(seiten[0]); var rechts = Stoffe(seiten[1]);
        if (links.Count == 0 || rechts.Count == 0) throw new FormatException("Auf beiden Seiten muss mindestens ein Stoff stehen.");
        var alle = links.Concat(rechts).ToList();
        var elemente = alle.SelectMany(f => f.Atome.Select(a => a.Symbol)).Distinct().ToList();
        var mitLadung = alle.Any(f => f.Ladung != 0);
        var zeilen = elemente.Select(e => alle.Select((f, j) => new Bruch((j < links.Count ? 1 : -1) * f.Anzahl(e))).ToArray()).ToList();
        if (mitLadung) zeilen.Add([.. alle.Select((f, j) => new Bruch((j < links.Count ? 1 : -1) * f.Ladung))]);
        var namen = Enumerable.Range(0, alle.Count).Select(j => ((char)('a' + j)).ToString()).ToList();
        var schritte = new List<Schritt>
        {
            new("Vor jeden Stoff einen unbekannten Faktor schreiben", string.Join(" + ", links.Select((f, j) => namen[j] + "·" + Schoen(f.Text))) + " → " + string.Join(" + ", rechts.Select((f, j) => namen[links.Count + j] + "·" + Schoen(f.Text)))),
            new("Atombilanz je Element: links gleich viele Atome wie rechts" + (mitLadung ? ", dazu die Ladung" : ""), string.Join(" | ", elemente.Select(e => $"{e}: {Bilanz(alle, links.Count, namen, f => f.Anzahl(e))}")) + (mitLadung ? $" | Ladung: {Bilanz(alle, links.Count, namen, f => f.Ladung)}" : "")),
        };
        // Nullraum der Bilanzmatrix (exakt)
        var n = alle.Count; var M = zeilen.Select(r => r.ToArray()).ToList();
        var piv = new List<int>(); var zr = 0;
        for (var sp = 0; sp < n && zr < M.Count; sp++)
        {
            var pr = Enumerable.Range(zr, M.Count - zr).FirstOrDefault(r => !M[r][sp].IstNull, -1);
            if (pr < 0) continue;
            (M[pr], M[zr]) = (M[zr], M[pr]);
            var p = M[zr][sp]; for (var j = 0; j < n; j++) M[zr][j] /= p;
            for (var r = 0; r < M.Count; r++) if (r != zr && !M[r][sp].IstNull) { var f = M[r][sp]; for (var j = 0; j < n; j++) M[r][j] -= f * M[zr][j]; }
            piv.Add(sp); zr++;
        }
        var frei = Enumerable.Range(0, n).Where(j => !piv.Contains(j)).ToList();
        if (frei.Count == 0) throw new ArgumentException("Diese Reaktion lässt sich nicht ausgleichen – prüfe die Formeln (stimmen Produkte und Ausgangsstoffe?).");
        if (frei.Count > 1) throw new ArgumentException("Die Gleichung ist mehrdeutig: Es gibt mehrere unabhängige Lösungen (wahrscheinlich zwei Reaktionen in einer). Bitte aufteilen.");
        var vec = new Bruch[n]; vec[frei[0]] = Bruch.Eins;
        for (var i = 0; i < piv.Count; i++) vec[piv[i]] = -M[i][frei[0]];
        schritte.Add(new Schritt($"Gleichungssystem lösen: {namen[frei[0]]} = 1 setzen", string.Join(", ", namen.Select((nm, j) => $"{nm} = {vec[j]}"))));
        if (vec.All(v => v.Vorzeichen <= 0)) for (var j = 0; j < n; j++) vec[j] = -vec[j];
        if (vec.Any(v => v.Vorzeichen <= 0))
            throw new ArgumentException("Diese Reaktion lässt sich nicht mit positiven Faktoren ausgleichen – ein Stoff steht wohl auf der falschen Seite oder fehlt.");
        var kgv = vec.Select(v => v.N).Aggregate(Bruch.Kgv);
        var ganz = vec.Select(v => (v * new Bruch(kgv, 1)).Z).ToArray();
        var ggt = ganz.Aggregate(BigInteger.GreatestCommonDivisor);
        var k = ganz.Select(v => (int)(v / ggt)).ToArray();
        if (kgv > 1 || ggt > 1) schritte.Add(new Schritt($"Mit {kgv} malnehmen{(ggt > 1 ? $" und durch {ggt} teilen" : "")}, damit kleinste ganze Zahlen entstehen", string.Join(", ", namen.Select((nm, j) => $"{nm} = {k[j]}"))));
        var L = links.Select((f, j) => (k[j], f)).ToList(); var R = rechts.Select((f, j) => (k[links.Count + j], f)).ToList();
        var reaktion = new Reaktion(text, L, R, schritte, []);
        schritte.Add(new Schritt("Ausgeglichene Gleichung", reaktion.Gleichung));
        // Probe: Atome zählen (unabhängig vom Lösungsweg)
        var proben = new List<Probe>();
        foreach (var e in elemente)
        {
            var li = L.Sum(x => x.Item1 * x.f.Anzahl(e)); var re = R.Sum(x => x.Item1 * x.f.Anzahl(e));
            proben.Add(new Probe(li == re, $"{(li == re ? "✓" : "✗")} {e}: links {li}, rechts {re}"));
        }
        if (mitLadung)
        {
            var li = L.Sum(x => x.Item1 * x.f.Ladung); var re = R.Sum(x => x.Item1 * x.f.Ladung);
            proben.Add(new Probe(li == re, $"{(li == re ? "✓" : "✗")} Ladung: links {li:+0;−0;0}, rechts {re:+0;−0;0}"));
        }
        if (!L.Any(x => x.f.Atome.Count > 0 && x.f.Atome.All(a => Elemente.Symbol(a.Symbol)!.MasseGeschaetzt)))
        {
            // Massenerhaltung als zweite Probe
            var ml = L.Sum(x => x.Item1 * x.f.Atome.Sum(a => a.Anzahl * Elemente.Symbol(a.Symbol)!.Masse)); var mr = R.Sum(x => x.Item1 * x.f.Atome.Sum(a => a.Anzahl * Elemente.Symbol(a.Symbol)!.Masse));
            var ok = Math.Abs(ml - mr) < 1e-6 * Math.Max(1, ml);
            proben.Add(new Probe(ok, $"{(ok ? "✓" : "✗")} Massenerhaltung: links {G(ml)} g, rechts {G(mr)} g (je Formelumsatz)"));
        }
        return reaktion with { Proben = proben };
    }

    static string Bilanz(List<Formel> alle, int nLinks, List<string> namen, Func<Formel, int> zahl)
    {
        string Seite(IEnumerable<int> idx) { var t = idx.Where(j => zahl(alle[j]) != 0).Select(j => (zahl(alle[j]) == 1 ? "" : zahl(alle[j]) + "") + namen[j]).ToList(); return t.Count == 0 ? "0" : string.Join(" + ", t); }
        return Seite(Enumerable.Range(0, nLinks)) + " = " + Seite(Enumerable.Range(nLinks, alle.Count - nLinks));
    }
}
