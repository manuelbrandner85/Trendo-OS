using System.Numerics;

namespace Trendo.Core.Wissen;

/// <summary>T-0781: Auswerten (Gleitkomma und exakt), Vereinfachen und Polynome mit Bruch-Koeffizienten.</summary>
public static class Auswertung
{
    /// <summary>Wert mit Gleitkomma (Bogenmaß). Fehlende Variablen werfen eine verständliche Meldung.</summary>
    public static double Wert(Term t, IReadOnlyDictionary<string, double>? werte = null) => t switch
    {
        Zahl z => z.Wert.AlsDouble(),
        Konst k => k.Wert,
        Var v => werte is not null && werte.TryGetValue(v.Name, out var d) ? d : throw new ArgumentException($"Für „{v.Name}“ fehlt ein Wert."),
        Summe s => s.Glieder.Sum(g => Wert(g, werte)),
        Produkt p => p.Faktoren.Aggregate(1.0, (a, f) => a * Wert(f, werte)),
        Potenz w => Pow(Wert(w.Basis, werte), w.Exponent, werte),
        Fkt f => f.Name switch
        {
            "sin" => Math.Sin(Wert(f.Argument, werte)), "cos" => Math.Cos(Wert(f.Argument, werte)), "tan" => Math.Tan(Wert(f.Argument, werte)),
            "asin" => Math.Asin(Wert(f.Argument, werte)), "acos" => Math.Acos(Wert(f.Argument, werte)), "atan" => Math.Atan(Wert(f.Argument, werte)),
            "ln" => Math.Log(Wert(f.Argument, werte)), "log" => Math.Log10(Wert(f.Argument, werte)), "abs" => Math.Abs(Wert(f.Argument, werte)),
            _ => double.NaN,
        },
        _ => double.NaN,
    };

    static double Pow(double b, Term e, IReadOnlyDictionary<string, double>? werte)
    {
        // ungerade Wurzeln aus negativen Zahlen (∛(−8) = −2) wie in der Schule
        // T-0781: auch „1/3“ aus der Eingabe (Bruch-Gruppe, noch kein einzelner Zahl-Knoten)
        Bruch? ez = e is Zahl z0 ? z0.Wert : b < 0 && !e.Variablen().Any() ? Exakt(e) : null;
        if (b < 0 && ez is { } zw && !zw.IstGanz && zw.N % 2 == 1)
        {
            var r = Math.Pow(-b, zw.AlsDouble());
            return zw.Z % 2 == 0 ? r : -r;
        }
        return Math.Pow(b, Wert(e, werte));
    }

    /// <summary>Exakter Wert als Bruch, wenn das ohne Näherung geht (sonst null) – z. B. für die Probe durch Einsetzen.</summary>
    public static Bruch? Exakt(Term t, IReadOnlyDictionary<string, Bruch>? werte = null)
    {
        try { return E(t, werte); }
        catch (Exception e) when (e is DivideByZeroException or OverflowException) { return null; }
    }

    static Bruch? E(Term t, IReadOnlyDictionary<string, Bruch>? werte)
    {
        switch (t)
        {
            case Zahl z: return z.Wert;
            case Var v: return werte is not null && werte.TryGetValue(v.Name, out var b) ? b : null;
            case Summe s:
                {
                    var r = Bruch.Null;
                    foreach (var g in s.Glieder) { if (E(g, werte) is not { } x) return null; r += x; }
                    return r;
                }
            case Produkt p:
                {
                    var r = Bruch.Eins;
                    foreach (var g in p.Faktoren) { if (E(g, werte) is not { } x) return null; r *= x; }
                    return r;
                }
            case Potenz w:
                {
                    if (E(w.Basis, werte) is not { } bb || E(w.Exponent, werte) is not { } ee) return null;
                    if (ee.IstGanz) return bb.Hoch((int)ee.Z);
                    if (ee.N > 12 || BigInteger.Abs(ee.Z) > 64) return null;
                    if (bb.Wurzel((int)ee.N) is not { } root) return null;
                    return root.Hoch((int)ee.Z);
                }
            case Fkt f when f.Name == "abs": return E(f.Argument, werte) is { } a ? a.Betrag : null;
            case Fkt f when f.Name is "sin" or "tan" or "asin" or "atan": return E(f.Argument, werte) is { IstNull: true } ? Bruch.Null : null;
            case Fkt f when f.Name == "cos": return E(f.Argument, werte) is { IstNull: true } ? Bruch.Eins : null;
            case Fkt f when f.Name is "ln" or "log": return E(f.Argument, werte) is { } eins && eins == Bruch.Eins ? Bruch.Null : f.Name == "log" && E(f.Argument, werte) is { } a10 && Zehnerpotenz(a10) is { } n ? new Bruch(n) : null;
            default: return null;
        }
    }

    static int? Zehnerpotenz(Bruch a)
    {
        if (a.Vorzeichen <= 0) return null;
        for (var n = -30; n <= 30; n++) if (new Bruch(10).Hoch(n) == a) return n;
        return null;
    }
}

/// <summary>Vereinfachen: Zahlen zusammenrechnen, gleiche Glieder und gleiche Basen zusammenfassen, Hoch 0/1 und Mal 0/1 entfernen.
/// Klammern werden hier NICHT ausmultipliziert (dafür <see cref="Polynom"/>).</summary>
public static class Vereinfacher
{
    public static Term Vereinfache(Term t)
    {
        for (var k = 0; k < 6; k++)
        {
            var n = Einmal(t);
            if (n.Schluessel == t.Schluessel) return n;
            t = n;
        }
        return t;
    }

    static Term Einmal(Term t)
    {
        Rechenzeit.Pruefe();
        return EinmalOhne(t);
    }

    static Term EinmalOhne(Term t) => t switch
    {
        Summe s => Sum(s.Glieder.Select(Einmal)),
        Produkt p => Prod(p.Faktoren.Select(Einmal)),
        Potenz w => Pot(Einmal(w.Basis), Einmal(w.Exponent)),
        Fkt f => Fn(f.Name, Einmal(f.Argument)),
        _ => t,
    };

    static Term Fn(string name, Term a)
    {
        if (a is Zahl z)
        {
            if (z.Wert.IstNull && name is "sin" or "tan" or "asin" or "atan") return Term.Null;
            if (z.Wert.IstNull && name == "cos") return Term.Eins;
            if (z.Wert == Bruch.Eins && name is "ln" or "log") return Term.Null;
            if (name == "abs") return new Zahl(z.Wert.Betrag);
            // T-0781: Hauptwerte der Umkehrfunktionen an den bekannten Stellen exakt (für allgemeine Lösungen wie π/6 + 2kπ)
            Bruch? pi = name switch
            {
                "asin" => z.Wert == Bruch.Eins ? new Bruch(1, 2) : z.Wert == Bruch.MinusEins ? new Bruch(-1, 2) : z.Wert == new Bruch(1, 2) ? new Bruch(1, 6) : z.Wert == new Bruch(-1, 2) ? new Bruch(-1, 6) : null,
                "acos" => z.Wert == Bruch.Eins ? Bruch.Null : z.Wert == Bruch.MinusEins ? Bruch.Eins : z.Wert.IstNull ? new Bruch(1, 2) : z.Wert == new Bruch(1, 2) ? new Bruch(1, 3) : z.Wert == new Bruch(-1, 2) ? new Bruch(2, 3) : null,
                "atan" => z.Wert == Bruch.Eins ? new Bruch(1, 4) : z.Wert == Bruch.MinusEins ? new Bruch(-1, 4) : null,
                _ => null,
            };
            if (pi is { } p) return p.IstNull ? Term.Null : p == Bruch.Eins ? Konst.Pi : new Produkt([new Zahl(p), Konst.Pi]);
        }
        if (name is "sin" or "cos" && PiVielfaches(a) is { } k && (k * 2).IstGanz)
        {
            // sin(kπ), cos(kπ) für halbe Vielfache exakt: Viertel der Umdrehung zählen
            var viertel = (int)(((k * 2).Z % 4 + 4) % 4);
            return new Zahl(name == "sin" ? viertel switch { 1 => 1, 3 => -1, _ => 0 } : viertel switch { 0 => 1, 2 => -1, _ => 0 });
        }
        if (name == "ln" && a is Konst { Name: "e" }) return Term.Eins;
        if (name == "ln" && a is Potenz w && w.Basis is Konst { Name: "e" }) return w.Exponent;
        if (name == "abs" && a is Fkt { Name: "abs" }) return a;
        return new Fkt(name, a);
    }

    /// <summary>a = k·π? Dann k.</summary>
    static Bruch? PiVielfaches(Term a) => a switch
    {
        Konst { Name: "pi" } => Bruch.Eins,
        Zahl { Wert.IstNull: true } => Bruch.Null,
        Produkt { Faktoren: [Zahl z, Konst { Name: "pi" }] } => z.Wert,
        _ => null,
    };

    static Term Pot(Term b, Term e)
    {
        if (e is Zahl ez)
        {
            if (ez.Wert.IstNull) return Term.Eins;
            if (ez.Wert == Bruch.Eins) return b;
            if (b is Zahl bz)
            {
                if (ez.Wert.IstGanz)
                {
                    // Gegenprüfung T-0781: große Potenzen nicht still stehen lassen (Probe rot) und nicht endlos rechnen – ausrechnen oder klar ablehnen
                    if (bz.Wert.IstNull && ez.Wert.Vorzeichen < 0) throw new DivideByZeroException("Teilen durch 0 geht nicht.");
                    if (bz.Wert.IstNull || bz.Wert == Bruch.Eins) return new Zahl(bz.Wert);
                    if (bz.Wert == Bruch.MinusEins) return new Zahl(ez.Wert.Z.IsEven ? Bruch.Eins : Bruch.MinusEins);
                    if (BigInteger.Abs(ez.Wert.Z) > 100_000) throw new OverflowException("Die Zahl wird zu groß – das rechnet der Mathe-Helfer nicht aus.");
                    return new Zahl(bz.Wert.Hoch((int)ez.Wert.Z));
                }
                if (ez.Wert.N <= 12 && bz.Wert.Wurzel((int)ez.Wert.N) is { } r && BigInteger.Abs(ez.Wert.Z) <= 512) return new Zahl(r.Hoch((int)ez.Wert.Z));
            }
            if (b is Potenz bw && ez.Wert.IstGanz) return Pot(bw.Basis, Prod([bw.Exponent, e]));
            if (b is Potenz bw2 && bw2.Exponent is Zahl ie && ie.Wert.IstGanz && ie.Wert.Z % 2 != 0) return Pot(bw2.Basis, new Zahl(ie.Wert * ez.Wert)); // (x³)^(1/3) = x
            if (b is Produkt bp && ez.Wert.IstGanz) return Prod(bp.Faktoren.Select(f => Pot(f, e)));
        }
        if (b is Zahl one && one.Wert == Bruch.Eins) return Term.Eins;
        if (b is Zahl zero && zero.Wert.IstNull && e is Zahl pos && pos.Wert.Vorzeichen > 0) return Term.Null;
        if (b is Konst { Name: "e" } && e is Fkt { Name: "ln" } lf) return lf.Argument;
        return new Potenz(b, e);
    }

    /// <summary>Glied = Zahl · Rest (Rest ohne Zahlfaktor).</summary>
    public static (Bruch Koeff, Term? Teil) Zerlege(Term t)
    {
        if (t is Zahl z) return (z.Wert, null);
        if (t is Produkt p && p.Faktoren.Count > 0 && p.Faktoren[0] is Zahl c)
            return (c.Wert, p.Faktoren.Count == 2 ? p.Faktoren[1] : new Produkt([.. p.Faktoren.Skip(1)]));
        return (Bruch.Eins, t);
    }

    static Term Sum(IEnumerable<Term> glieder)
    {
        var flat = new List<Term>();
        foreach (var g in glieder) if (g is Summe s) flat.AddRange(s.Glieder); else flat.Add(g);
        var konst = Bruch.Null;
        var gruppen = new List<(string Key, Term Teil, Bruch Koeff)>();
        foreach (var g in flat)
        {
            var (c, rest) = Zerlege(g);
            if (rest is null) { konst += c; continue; }
            var i = gruppen.FindIndex(x => x.Key == rest.Schluessel);
            if (i < 0) gruppen.Add((rest.Schluessel, rest, c)); else gruppen[i] = (gruppen[i].Key, gruppen[i].Teil, gruppen[i].Koeff + c);
        }
        var res = gruppen.Where(x => !x.Koeff.IstNull).OrderByDescending(x => Grad(x.Teil)).ThenBy(x => Ordnung(x.Teil), StringComparer.Ordinal)
            .Select(x => x.Koeff == Bruch.Eins ? x.Teil : Prod([new Zahl(x.Koeff), x.Teil])).ToList();
        if (!konst.IstNull) res.Add(new Zahl(konst));
        return res.Count switch { 0 => Term.Null, 1 => res[0], _ => new Summe(res) };
    }

    static Term Prod(IEnumerable<Term> faktoren)
    {
        var flat = new List<Term>();
        foreach (var f in faktoren) if (f is Produkt p) flat.AddRange(p.Faktoren); else flat.Add(f);
        var koeff = Bruch.Eins;
        var basen = new List<(string Key, Term Basis, List<Term> Exp)>();
        foreach (var f in flat)
        {
            if (f is Zahl z) { koeff *= z.Wert; continue; }
            Term b = f, e = Term.Eins;
            if (f is Potenz w) { b = w.Basis; e = w.Exponent; }
            // Zahlenbasen mit Bruch-Exponent (√2, 2^(1/3)) bleiben eigene Faktoren; Zahl^(−1) ist ein Kehrwert
            if (b is Zahl bz && e is Zahl ez && ez.Wert.IstGanz) { koeff *= bz.Wert.Hoch((int)ez.Wert.Z); continue; }
            var i = basen.FindIndex(x => x.Key == b.Schluessel);
            if (i < 0) basen.Add((b.Schluessel, b, [e])); else basen[i].Exp.Add(e);
        }
        if (koeff.IstNull) return Term.Null;
        var rest = new List<Term>();
        foreach (var (_, b, ex) in basen)
        {
            var e = ex.Count == 1 ? ex[0] : Sum(ex);
            var p = Pot(b, e);
            if (p is Zahl pz) koeff *= pz.Wert;
            else if (p is Produkt pp) foreach (var x in pp.Faktoren) { if (x is Zahl xz) koeff *= xz.Wert; else rest.Add(x); }
            else rest.Add(p);
        }
        rest = [.. rest.OrderBy(Ordnung, StringComparer.Ordinal)];
        if (koeff.IstNull) return Term.Null;
        if (rest.Count == 0) return new Zahl(koeff);
        if (koeff == Bruch.Eins) return rest.Count == 1 ? rest[0] : new Produkt(rest);
        return new Produkt([new Zahl(koeff), .. rest]);
    }

    /// <summary>Sortierung: Variablen vor Funktionen, Nenner zuletzt.</summary>
    static string Ordnung(Term t) => t switch
    {
        Var v => "1" + v.Name,
        Potenz w when w.Exponent is Zahl e && e.Wert.Vorzeichen < 0 => "9" + w.Basis.Schluessel,
        Potenz w => (w.Basis is Var ? "1" : "5") + w.Basis.Schluessel,
        Konst => "0",
        Fkt f => "6" + f.Name,
        Summe => "4",
        _ => "7" + t.Schluessel,
    };

    /// <summary>Grad eines Glieds (Summe der ganzzahligen Exponenten seiner Variablen) – für die Reihenfolge „höchste Potenz zuerst“.</summary>
    public static int Grad(Term t) => t switch
    {
        Var => 1,
        Potenz w when w.Basis is Var && w.Exponent is Zahl e && e.Wert.IstGanz => (int)e.Wert.Z,
        Produkt p => p.Faktoren.Sum(Grad),
        _ => 0,
    };
}

/// <summary>Polynom in einer oder mehreren Variablen mit exakten Koeffizienten. Monome als „x^2*y“.</summary>
public sealed class Polynom
{
    /// <summary>Monom-Schlüssel → (Exponenten je Variable, Koeffizient).</summary>
    readonly Dictionary<string, (SortedDictionary<string, int> Exp, Bruch C)> _m = [];

    public IEnumerable<(IReadOnlyDictionary<string, int> Exp, Bruch C)> Glieder => _m.Values.Select(v => ((IReadOnlyDictionary<string, int>)v.Exp, v.C));
    public bool IstNull => _m.Count == 0;
    public int Anzahl => _m.Count;

    static string Key(SortedDictionary<string, int> e) => string.Join("*", e.Select(kv => kv.Key + "^" + kv.Value));

    public static Polynom Konstante(Bruch c) { var p = new Polynom(); p.Add([], c); return p; }
    public static Polynom Variable(string v) { var p = new Polynom(); p.Add(new SortedDictionary<string, int>(StringComparer.Ordinal) { [v] = 1 }, Bruch.Eins); return p; }

    void Add(SortedDictionary<string, int> e, Bruch c)
    {
        if (c.IstNull) return;
        var k = Key(e);
        if (_m.TryGetValue(k, out var have)) { var s = have.C + c; if (s.IstNull) _m.Remove(k); else _m[k] = (have.Exp, s); }
        else _m[k] = (new SortedDictionary<string, int>(e, StringComparer.Ordinal), c);
    }

    public static Polynom operator +(Polynom a, Polynom b) { var r = a.Kopie(); foreach (var (e, c) in b._m.Values) r.Add(e, c); return r; }
    public static Polynom operator -(Polynom a, Polynom b) => a + b * Konstante(Bruch.MinusEins);
    public static Polynom operator *(Polynom a, Polynom b)
    {
        var r = new Polynom();
        foreach (var (ea, ca) in a._m.Values)
            foreach (var (eb, cb) in b._m.Values)
            {
                if ((r._m.Count & 255) == 0) Rechenzeit.Pruefe();
                var e = new SortedDictionary<string, int>(ea, StringComparer.Ordinal);
                foreach (var (v, n) in eb) e[v] = e.GetValueOrDefault(v) + n;
                r.Add(e, ca * cb);
            }
        if (r._m.Count > 5000) throw new OverflowException("Das Ergebnis wird zu groß (mehr als 5000 Glieder).");
        return r;
    }
    public Polynom Hoch(int n)
    {
        if (n < 0) throw new ArgumentException("negativer Exponent");
        var r = Konstante(Bruch.Eins); var b = this;
        while (n > 0) { if ((n & 1) == 1) r *= b; n >>= 1; if (n > 0) b *= b; }
        return r;
    }
    public Polynom Kopie() { var r = new Polynom(); foreach (var (e, c) in _m.Values) r.Add(e, c); return r; }
    public bool Gleich(Polynom o) => (this - o).IstNull;

    public IReadOnlyList<string> Variablen => _m.Values.SelectMany(v => v.Exp.Keys).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
    public int Grad(string v) => _m.Count == 0 ? 0 : _m.Values.Max(m => m.Exp.GetValueOrDefault(v));
    public int Gesamtgrad => _m.Count == 0 ? 0 : _m.Values.Max(m => m.Exp.Values.Sum());
    public bool IstKonstant(out Bruch c) { c = _m.Count == 0 ? Bruch.Null : _m.Values.First().C; return _m.Count == 0 || _m.Count == 1 && _m.Values.First().Exp.Count == 0; }

    /// <summary>Koeffizienten nach Potenzen von <paramref name="v"/> (Index = Exponent); nur wenn keine andere Variable vorkommt.</summary>
    public Bruch[]? Koeffizienten(string v)
    {
        if (Variablen.Any(x => x != v)) return null;
        var r = new Bruch[Grad(v) + 1]; for (var k = 0; k < r.Length; k++) r[k] = Bruch.Null;
        foreach (var (e, c) in _m.Values) r[e.GetValueOrDefault(v)] += c;
        return r;
    }

    public static Polynom AusKoeffizienten(string v, IReadOnlyList<Bruch> k)
    {
        var p = new Polynom();
        for (var i = 0; i < k.Count; i++) p.Add(i == 0 ? [] : new SortedDictionary<string, int>(StringComparer.Ordinal) { [v] = i }, k[i]);
        return p;
    }

    public Bruch? Wert(IReadOnlyDictionary<string, Bruch> werte)
    {
        var r = Bruch.Null;
        foreach (var (e, c) in _m.Values)
        {
            var t = c;
            foreach (var (v, n) in e) { if (!werte.TryGetValue(v, out var x)) return null; t *= x.Hoch(n); }
            r += t;
        }
        return r;
    }

    /// <summary>Polynom aus einem Term oder null (Funktionen, Wurzeln, Variablen im Nenner).</summary>
    public static Polynom? Aus(Term t)
    {
        try { return A(t); }
        catch (OverflowException) { return null; }
    }

    static Polynom? A(Term t)
    {
        switch (t)
        {
            case Zahl z: return Konstante(z.Wert);
            case Var v: return Variable(v.Name);
            case Summe s:
                {
                    var r = new Polynom();
                    foreach (var g in s.Glieder) { if (A(g) is not { } p) return null; r += p; }
                    return r;
                }
            case Produkt pr:
                {
                    var r = Konstante(Bruch.Eins);
                    foreach (var g in pr.Faktoren) { if (A(g) is not { } p) return null; r *= p; }
                    return r;
                }
            case Potenz w when w.Exponent is Zahl e && e.Wert.IstGanz:
                {
                    if (A(w.Basis) is not { } b) return null;
                    var n = (int)BigInteger.Max(BigInteger.Min(e.Wert.Z, 10_000), -10_000);
                    if (n >= 0) { if (n > 64 && !b.IstKonstant(out _)) throw new OverflowException("Exponent zu groß."); return b.IstKonstant(out var bc) ? Konstante(bc.Hoch(n)) : b.Hoch(n); }
                    return b.IstKonstant(out var c) && !c.IstNull ? Konstante(c.Hoch(n)) : null;
                }
            case Potenz w when w.Basis is Zahl bz && w.Exponent is Zahl ez && ez.Wert.N <= 12 && bz.Wert.Wurzel((int)ez.Wert.N) is { } root:
                return Konstante(root.Hoch((int)ez.Wert.Z));
            default: return null;
        }
    }

    /// <summary>Als Term, höchste Potenz zuerst.</summary>
    public Term AlsTerm()
    {
        var glieder = _m.Values
            .OrderByDescending(m => m.Exp.Values.Sum())
            .ThenBy(m => string.Join(",", m.Exp.Select(kv => kv.Key + (99 - kv.Value).ToString("00"))), StringComparer.Ordinal)
            .Select(m => MonomTerm(m.Exp, m.C)).ToList();
        return glieder.Count switch { 0 => Term.Null, 1 => glieder[0], _ => new Summe(glieder) };
    }

    static Term MonomTerm(IReadOnlyDictionary<string, int> e, Bruch c)
    {
        var f = new List<Term>();
        if (c != Bruch.Eins || e.Count == 0) f.Add(new Zahl(c));
        foreach (var (v, n) in e) f.Add(n == 1 ? new Var(v) : new Potenz(new Var(v), new Zahl(n)));
        return f.Count == 1 ? f[0] : new Produkt(f);
    }

    public override string ToString() => TermText.Anzeige(AlsTerm());

    /// <summary>Polynomdivision durch (x − r) nach Horner: Quotient (Koeffizienten), Rest.</summary>
    public static (Bruch[] Quotient, Bruch Restwert) Horner(IReadOnlyList<Bruch> k, Bruch r)
    {
        var n = k.Count - 1; var q = new Bruch[Math.Max(n, 0)]; var acc = Bruch.Null;
        for (var i = n; i >= 0; i--)
        {
            acc = acc * r + k[i];
            if (i > 0) q[i - 1] = acc;
        }
        return (q, acc);
    }
}

/// <summary>Zahl der Form a + b·√m (m quadratfrei) – exakte Probe für Lösungen mit Wurzel (p-q-Formel).</summary>
public readonly struct WurzelZahl(Bruch a, Bruch b, BigInteger m)
{
    public Bruch A { get; } = a;
    public Bruch B { get; } = b;
    public BigInteger M { get; } = m;
    public static WurzelZahl operator +(WurzelZahl x, WurzelZahl y) => new(x.A + y.A, x.B + y.B, x.M.IsOne ? y.M : x.M);
    public static WurzelZahl operator *(WurzelZahl x, WurzelZahl y) { var m = x.M.IsOne ? y.M : x.M; return new(x.A * y.A + x.B * y.B * new Bruch(m, 1), x.A * y.B + x.B * y.A, m); }
    public bool IstNull => A.IstNull && B.IstNull;
    public double AlsDouble() => A.AlsDouble() + B.AlsDouble() * Math.Sqrt((double)M);

    /// <summary>Polynom (Koeffizienten aufsteigend) an dieser Stelle exakt auswerten (Horner in ℚ(√m)).</summary>
    public WurzelZahl Einsetzen(IReadOnlyList<Bruch> k)
    {
        var acc = new WurzelZahl(Bruch.Null, Bruch.Null, M);
        for (var i = k.Count - 1; i >= 0; i--) acc = acc * this + new WurzelZahl(k[i], Bruch.Null, M);
        return acc;
    }

    /// <summary>√(r) für einen positiven Bruch: k·√m mit quadratfreiem m (√(8/9) = 2/3·√2).</summary>
    public static (Bruch Faktor, BigInteger M) Wurzel(Bruch r)
    {
        if (r.Vorzeichen < 0) throw new ArgumentException("Wurzel aus einer negativen Zahl");
        // √(z/n) = √(z·n)/n
        var zn = r.Z * r.N; BigInteger aussen = 1, innen = 1, rest = zn;
        for (BigInteger p = 2; p * p <= rest && p < 100_000; p++)
        {
            if (p % 1024 == 0) Rechenzeit.Pruefe();
            while (rest % (p * p) == 0) { rest /= p * p; aussen *= p; }
            if (rest % p == 0) { rest /= p; innen *= p; }
        }
        innen *= rest;
        return (new Bruch(aussen, r.N), innen);
    }

    /// <summary>Anzeige wie im Heft: „(−3 + √5)/2“, „1 − 2√3“.</summary>
    public override string ToString()
    {
        if (B.IstNull || M.IsOne) return (A + B).ToString();
        var d = Bruch.Kgv(A.N, B.N); var a = A * new Bruch(d, 1); var b = B * new Bruch(d, 1);
        var w = "√" + M;
        var bt = b.Betrag == Bruch.Eins ? w : b.Betrag + w;
        string inner = a.IstNull ? (b.Vorzeichen < 0 ? "−" : "") + bt : a + (b.Vorzeichen < 0 ? " − " : " + ") + bt;
        return d.IsOne ? inner : (a.IstNull ? inner : "(" + inner + ")") + "/" + d;
    }

    /// <summary>Als Term (für Graph und LaTeX).</summary>
    public Term AlsTerm()
    {
        if (B.IstNull || M.IsOne) return new Zahl(A + B);
        var w = Vereinfacher.Vereinfache(Term.Mal(new Zahl(B), new Potenz(new Zahl(new Bruch(M, 1)), new Zahl(new Bruch(1, 2)))));
        return A.IstNull ? w : new Summe([new Zahl(A), w]);
    }
}
