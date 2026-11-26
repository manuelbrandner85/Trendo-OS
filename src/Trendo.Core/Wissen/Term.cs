using System.Globalization;
using System.Numerics;
using System.Text;

namespace Trendo.Core.Wissen;

/// <summary>T-0781 Mathe-Helfer: Rechenausdruck als Baum (eigener Rechenkern, kein fremdes CAS). Zahlen sind exakte <see cref="Bruch"/>e;
/// Minus ist „·(−1)“, Teilen ist „·(…)^(−1)“, Wurzel ist „^(1/2)“, e^u ist eine Potenz der Konstanten e.</summary>
public abstract class Term
{
    /// <summary>Eindeutige Schreibweise für Vergleiche (gleiche Terme = gleicher Schlüssel).</summary>
    public abstract string Schluessel { get; }
    public override string ToString() => TermText.Anzeige(this);

    public static readonly Term Null = new Zahl(Bruch.Null), Eins = new Zahl(Bruch.Eins), MinusEins = new Zahl(Bruch.MinusEins);
    public static Term N(Bruch b) => new Zahl(b);
    public static Term Plus(params Term[] t) => new Summe(t);
    public static Term Mal(params Term[] t) => new Produkt(t);
    public static Term Minus(Term a) => a is Zahl z ? new Zahl(-z.Wert) : new Produkt([MinusEins, a]);
    public static Term Durch(Term a, Term b) => new Produkt([a, new Potenz(b, MinusEins)]);
    public static Term Hoch(Term b, Term e) => new Potenz(b, e);
    public static Term Hoch(Term b, Bruch e) => new Potenz(b, new Zahl(e));

    public bool IstZahl(out Bruch b) { if (this is Zahl z) { b = z.Wert; return true; } b = default; return false; }
    public bool IstZahl() => this is Zahl;

    /// <summary>Kommt die Variable vor?</summary>
    public bool Enthaelt(string v) => this switch
    {
        Var x => x.Name == v,
        Summe s => s.Glieder.Any(t => t.Enthaelt(v)),
        Produkt p => p.Faktoren.Any(t => t.Enthaelt(v)),
        Potenz w => w.Basis.Enthaelt(v) || w.Exponent.Enthaelt(v),
        Fkt f => f.Argument.Enthaelt(v),
        _ => false,
    };

    public IEnumerable<string> Variablen()
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        void Walk(Term t)
        {
            switch (t)
            {
                case Var v: set.Add(v.Name); break;
                case Summe s: foreach (var g in s.Glieder) Walk(g); break;
                case Produkt p: foreach (var g in p.Faktoren) Walk(g); break;
                case Potenz w: Walk(w.Basis); Walk(w.Exponent); break;
                case Fkt f: Walk(f.Argument); break;
            }
        }
        Walk(this); return set;
    }

    /// <summary>Variable durch einen Term ersetzen.</summary>
    public Term Ersetze(string v, Term durch) => this switch
    {
        Var x when x.Name == v => durch,
        Summe s => new Summe([.. s.Glieder.Select(t => t.Ersetze(v, durch))]),
        Produkt p => new Produkt([.. p.Faktoren.Select(t => t.Ersetze(v, durch))]),
        Potenz w => new Potenz(w.Basis.Ersetze(v, durch), w.Exponent.Ersetze(v, durch)),
        Fkt f => new Fkt(f.Name, f.Argument.Ersetze(v, durch)),
        _ => this,
    };

    /// <summary>Anzahl Knoten (Schutz gegen ausufernde Ausdrücke).</summary>
    public int Groesse => this switch
    {
        Summe s => 1 + s.Glieder.Sum(t => t.Groesse),
        Produkt p => 1 + p.Faktoren.Sum(t => t.Groesse),
        Potenz w => 1 + w.Basis.Groesse + w.Exponent.Groesse,
        Fkt f => 1 + f.Argument.Groesse,
        _ => 1,
    };
}

public sealed class Zahl(Bruch wert) : Term
{
    public Bruch Wert { get; } = wert;
    public override string Schluessel => "#" + Wert.Eingabe();
}

/// <summary>Konstante π oder e.</summary>
public sealed class Konst(string name) : Term
{
    public string Name { get; } = name;
    public static readonly Konst Pi = new("pi"), E = new("e");
    public double Wert => Name == "pi" ? Math.PI : Math.E;
    public override string Schluessel => "$" + Name;
}

public sealed class Var(string name) : Term
{
    public string Name { get; } = name;
    public override string Schluessel => Name;
}

public sealed class Summe(IReadOnlyList<Term> glieder) : Term
{
    public IReadOnlyList<Term> Glieder { get; } = glieder;
    public override string Schluessel => "(+ " + string.Join(" ", Glieder.Select(g => g.Schluessel)) + ")";
}

public sealed class Produkt(IReadOnlyList<Term> faktoren) : Term
{
    public IReadOnlyList<Term> Faktoren { get; } = faktoren;
    public override string Schluessel => "(* " + string.Join(" ", Faktoren.Select(g => g.Schluessel)) + ")";
}

public sealed class Potenz(Term basis, Term exponent) : Term
{
    public Term Basis { get; } = basis;
    public Term Exponent { get; } = exponent;
    public override string Schluessel => "(^ " + Basis.Schluessel + " " + Exponent.Schluessel + ")";
}

/// <summary>Funktion: sin, cos, tan, asin, acos, atan, ln, log (Zehnerlogarithmus), abs.</summary>
public sealed class Fkt(string name, Term argument) : Term
{
    public string Name { get; } = name;
    public Term Argument { get; } = argument;
    public static readonly string[] Namen = ["sin", "cos", "tan", "asin", "acos", "atan", "ln", "log", "abs"];
    public override string Schluessel => "(" + Name + " " + Argument.Schluessel + ")";
}

/// <summary>Eigener Leser für Rechenausdrücke in deutscher Schreibweise: Komma oder Punkt, ·, ×, ÷, :, ², ³, √, π, |x|, Malzeichen darf fehlen („2x“, „3(x+1)“).
/// Mehrbuchstabige Wörter ohne Funktion sind Produkte einzelner Variablen („xy“ = x·y); Namen mit Unterstrich („m_1“, „E_kin“) und „Δ“ + Buchstabe sind eine Variable.</summary>
public static class TermLeser
{
    static readonly Dictionary<string, string> Funktionen = new(StringComparer.Ordinal)
    {
        ["sin"] = "sin", ["cos"] = "cos", ["tan"] = "tan", ["asin"] = "asin", ["arcsin"] = "asin", ["acos"] = "acos", ["arccos"] = "acos", ["atan"] = "atan", ["arctan"] = "atan",
        ["ln"] = "ln", ["log"] = "log", ["lg"] = "log", ["abs"] = "abs", ["betrag"] = "abs", ["exp"] = "exp", ["sqrt"] = "sqrt", ["wurzel"] = "sqrt",
    };

    /// <summary>T-0781 (2. Prüfrunde): Hinweise zu mehrdeutigen Eingaben (z. B. „2e+3“ als Zehnerpotenz gelesen), gesammelt seit <see cref="HinweiseLeeren"/> (je Thread).</summary>
    [ThreadStatic] static List<string>? _hinweise;
    public static IReadOnlyList<string> Hinweise => _hinweise ?? [];
    public static void HinweiseLeeren() => _hinweise = [];
    static void Hinweis(string h) { _hinweise ??= []; if (!_hinweise.Contains(h)) _hinweise.Add(h); }

    public static Term Lies(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new FormatException("Bitte eine Rechnung eingeben.");
        if (text.Length > 2000) throw new FormatException("Die Eingabe ist zu lang (höchstens 2000 Zeichen).");
        var p = new Leser(Normal(text));
        var t = p.Ausdruck();
        p.Ende();
        return t;
    }

    static string Normal(string s) => s.Replace('−', '-').Replace('–', '-').Replace('×', '*').Replace('·', '*').Replace('⋅', '*').Replace('÷', '/').Replace(' ', ' ')
        .Replace("**", "^").Replace('[', '(').Replace(']', ')').Replace('{', '(').Replace('}', ')');

    sealed class Leser(string s)
    {
        int i;
        int tiefe;
        void Leer() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        char Sieh() { Leer(); return i < s.Length ? s[i] : '\0'; }

        public void Ende()
        {
            Leer();
            if (i < s.Length) throw new FormatException(s[i] == ')' ? "Eine schließende Klammer „)“ ist zu viel." : $"Unerwartetes Zeichen „{s[i]}“ an Stelle {i + 1}.");
        }

        public Term Ausdruck()
        {
            if (++tiefe > 200) throw new FormatException("Zu viele verschachtelte Klammern.");
            var glieder = new List<Term> { Produkt() };
            while (true)
            {
                var c = Sieh();
                if (c == '+') { i++; glieder.Add(Produkt()); }
                else if (c == '-') { i++; glieder.Add(Term.Minus(Produkt())); }
                else break;
            }
            tiefe--;
            return glieder.Count == 1 ? glieder[0] : new Summe(glieder);
        }

        Term Produkt()
        {
            var f = new List<Term> { Vorzeichen() };
            while (true)
            {
                var c = Sieh();
                if (c is '*') { i++; f.Add(Vorzeichen()); }
                else if (c is '/' or ':')
                {
                    i++; var nenner = Vorzeichen();
                    // „3/4“ aus zwei ganzen Zahlen bleibt als Bruch zusammen (für das Bruchrechnen: kein Teilen-Schritt, aber Kürzen)
                    if (f[^1] is Zahl { Wert.IstGanz: true } zz && nenner is Zahl { Wert.IstGanz: true } nz && c == '/') f[^1] = new Produkt([zz, new Potenz(nz, Term.MinusEins)]);
                    else f.Add(new Potenz(nenner, Term.MinusEins));
                }
                else if (char.IsLetterOrDigit(c) || c is '(' or '√' or 'π') f.Add(Hoch()); // Malzeichen darf fehlen (kein Vorzeichen: „2 -3“ bleibt eine Differenz)
                else break;
            }
            return f.Count == 1 ? f[0] : new Produkt(f);
        }

        Term Vorzeichen()
        {
            var c = Sieh();
            if (c == '-') { i++; return Term.Minus(Vorzeichen()); }
            if (c == '+') { i++; return Vorzeichen(); }
            return Hoch();
        }

        Term Hoch()
        {
            var b = Nach(Atom());
            if (Sieh() == '^') { i++; var e = Vorzeichen(); return new Potenz(b, e); }
            return b;
        }

        Term Nach(Term t)
        {
            while (i < s.Length)
            {
                var c = s[i];
                if (c is '²' or '³' or '⁴') { i++; t = new Potenz(t, new Zahl(c == '²' ? 2 : c == '³' ? 3 : 4)); }
                else if (c == '!') { i++; t = Fakultaet(t); }
                else break;
            }
            return t;
        }

        static Term Fakultaet(Term t)
        {
            if (t is not Zahl z || !z.Wert.IstGanz || z.Wert.Vorzeichen < 0 || z.Wert.Z > 400) throw new FormatException("Fakultät „!“ nur für ganze Zahlen von 0 bis 400.");
            BigInteger f = 1; for (var k = 2; k <= (int)z.Wert.Z; k++) f *= k;
            return new Zahl(new Bruch(f, 1));
        }

        Term Atom()
        {
            var c = Sieh();
            if (c == '\0') throw new FormatException("Die Rechnung hört mitten drin auf – es fehlt noch etwas.");
            if (char.IsAsciiDigit(c) || c is '.' or ',')
            {
                var st = i;
                while (i < s.Length && (char.IsAsciiDigit(s[i]) || (s[i] is '.' or ',') && i + 1 < s.Length && char.IsAsciiDigit(s[i + 1]))) i++;
                var txt = s[st..i];
                if (txt.Count(ch => ch is '.' or ',') > 1) throw new FormatException($"„{txt}“ ist keine Zahl.");
                // T-0781 (Gegenprüfung): Zehnerpotenz „3e5“, „6,022e23“, „1,6e-19“ – nur wenn nach e/E Ziffern folgen; „2e“, „2e^x“, „3·e“ bleiben die Konstante e
                if (i + 1 < s.Length && s[i] is 'e' or 'E')
                {
                    var j = i + 1;
                    if (j < s.Length && s[j] is '+' or '-') j++;
                    var ziffern = j;
                    while (j < s.Length && char.IsAsciiDigit(s[j])) j++;
                    if (j > ziffern && (j >= s.Length || !char.IsLetter(s[j])))
                    {
                        if (j - ziffern > 6) throw new FormatException("Die Zehnerpotenz ist zu groß.");
                        var exp = int.Parse(s[(i + 1)..j], System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture);
                        if (Math.Abs(exp) > 10_000) throw new FormatException("Die Zehnerpotenz ist zu groß (höchstens 10⁴).");
                        // Mit Vorzeichen direkt nach e („2e+3“, „2e-1“) ist es eine Zehnerpotenz – für die Konstante e: „2e + 3“ mit Leerzeichen oder „2·e − 1“
                        if (s[i + 1] is '+' or '-') Hinweis($"„{s[st..j]}“ wurde als Zehnerpotenz {txt}·10^{exp} gelesen. Gemeint war „{txt}·e {s[i + 1]} {s[ziffern..j]}“? Dann mit Leerzeichen schreiben: {txt}e {s[i + 1]} {s[ziffern..j]}.");
                        i = j;
                        return new Zahl(Bruch.Lies(txt) * new Bruch(10).Hoch(exp));
                    }
                    // „2e3x“ ist mehrdeutig (2·10³·x oder 2·e·3x) – nicht raten
                    if (j > ziffern && j < s.Length && char.IsLetter(s[j]))
                        throw new FormatException($"„{s[st..(j + 1)]}“ ist mehrdeutig: Zehnerpotenz ({txt}·10^{s[(i + 1)..j]}·{s[j]}) oder die Zahl e ({txt}·e·{s[(i + 1)..j]}{s[j]})? Bitte mit Malzeichen schreiben, z. B. {txt}e{s[(i + 1)..j]}·{s[j]} oder {txt}·e·{s[(i + 1)..j]}{s[j]}.");
                }
                return new Zahl(Bruch.Lies(txt));
            }
            if (c == '(')
            {
                i++; var t = Ausdruck();
                if (Sieh() != ')') throw new FormatException("Eine Klammer „(“ wird nicht geschlossen.");
                i++; return t;
            }
            if (c == '|')
            {
                i++; var t = Ausdruck();
                if (Sieh() != '|') throw new FormatException("Der Betrag „|…|“ wird nicht geschlossen.");
                i++; return new Fkt("abs", t);
            }
            if (c == '√')
            {
                i++;
                var arg = Sieh() == '(' ? Atom() : Nach(Atom());
                return new Potenz(arg, new Zahl(new Bruch(1, 2)));
            }
            if (c == 'π') { i++; return Konst.Pi; }
            if (c == 'Δ' && i + 1 < s.Length && char.IsLetter(s[i + 1])) { i += 2; return new Var("Δ" + s[i - 1]); }
            if (char.IsLetter(c))
            {
                var st = i;
                while (i < s.Length && char.IsLetter(s[i])) i++;
                var wort = s[st..i];
                // Name mit Index: m_1, E_kin – eine Variable
                if (i < s.Length && s[i] == '_' && wort.Length == 1 || i < s.Length && s[i] == '_' && wort.Length > 1 && !Funktionen.ContainsKey(wort))
                {
                    i++; var st2 = i;
                    while (i < s.Length && char.IsLetterOrDigit(s[i])) i++;
                    if (i == st2) throw new FormatException("Nach „_“ fehlt der Index (z. B. m_1).");
                    return new Var(wort + "_" + s[st2..i]);
                }
                if (Funktionen.TryGetValue(wort, out var fn)) return Funktion(fn, wort);
                if (wort is "pi" or "Pi") return Konst.Pi;
                // „xy“ = x·y: nur den ersten Buchstaben nehmen, der Rest wird danach gelesen (so gilt „xy²“ = x·y²); „sinx“ = sin(x)
                var fkt = Funktionen.Keys.Where(n => wort.StartsWith(n, StringComparison.Ordinal)).OrderByDescending(n => n.Length).FirstOrDefault();
                if (fkt is not null) { i = st + fkt.Length; return Funktion(Funktionen[fkt], fkt); }
                if (wort.StartsWith("pi", StringComparison.Ordinal)) { i = st + 2; return Konst.Pi; }
                i = st + 1;
                return wort[0] == 'e' ? Konst.E : new Var(wort[0].ToString());
            }
            throw new FormatException($"Unerwartetes Zeichen „{c}“ an Stelle {i + 1}.");
        }

        Term Funktion(string fn, string wort)
        {
            Term arg;
            if (Sieh() is '(' or '|') arg = Atom();
            else if (Sieh() is var c && (char.IsLetterOrDigit(c) || c is 'π')) arg = Nach(Atom()); // „sin x“, „ln 2“
            else throw new FormatException($"Nach „{wort}“ fehlt der Wert in Klammern, z. B. {wort}(x).");
            return fn switch
            {
                "sqrt" => new Potenz(arg, new Zahl(new Bruch(1, 2))),
                "exp" => new Potenz(Konst.E, arg),
                _ => new Fkt(fn, arg),
            };
        }
    }
}

/// <summary>Schreibweisen eines Terms: Anzeige (deutsch, mit ², √, ·), Eingabe (für den Rechner und den Funktionsgraph) und LaTeX.</summary>
public static class TermText
{
    const int PSumme = 1, PProdukt = 2, PPotenz = 3, PAtom = 4;
    static readonly string Hochzahlen = "⁰¹²³⁴⁵⁶⁷⁸⁹";

    /// <summary>Ist der Term „negativ geschrieben“ (−3, −2x, −x²)?</summary>
    public static bool Negativ(Term t) => t is Zahl z && z.Wert.Vorzeichen < 0 || t is Produkt p && p.Faktoren.Count > 0 && p.Faktoren[0] is Zahl c && c.Wert.Vorzeichen < 0;

    public static Term Negiert(Term t) => t switch
    {
        Zahl z => new Zahl(-z.Wert),
        Produkt p when p.Faktoren[0] is Zahl c && c.Wert == Bruch.MinusEins => p.Faktoren.Count == 2 ? p.Faktoren[1] : new Produkt([.. p.Faktoren.Skip(1)]),
        Produkt p when p.Faktoren[0] is Zahl c => new Produkt([new Zahl(-c.Wert), .. p.Faktoren.Skip(1)]),
        _ => Term.Minus(t),
    };

    // ---------------- Anzeige ----------------
    public static string Anzeige(Term t) => A(t, 0);

    static string Klammer(string s, bool k) => k ? "(" + s + ")" : s;
    static int Rang(Term t) => t switch
    {
        Summe => PSumme,
        Produkt p => p.Faktoren.Count == 1 ? Rang(p.Faktoren[0]) : PProdukt,
        Zahl z => z.Wert.Vorzeichen < 0 || !z.Wert.IstGanz ? PProdukt : PAtom,
        Potenz w when w.Exponent is Zahl e && e.Wert.Vorzeichen < 0 => PProdukt,
        Potenz => PPotenz,
        _ => PAtom,
    };

    static string A(Term t, int umgebung)
    {
        var s = t switch
        {
            Zahl z => z.Wert.ToString(),
            Konst k => k.Name == "pi" ? "π" : "e",
            Var v => v.Name,
            Summe sm => ASumme(sm),
            Produkt p => AProdukt(p.Faktoren),
            Potenz w when w.Exponent is Zahl e && e.Wert.Vorzeichen < 0 => AProdukt([w]),
            Potenz w => APotenz(w),
            Fkt f when f.Name == "abs" => "|" + A(f.Argument, 0) + "|",
            Fkt f when f.Argument is Fkt { Name: "abs" } => f.Name + A(f.Argument, 0),
            Fkt f => f.Name + "(" + A(f.Argument, 0) + ")",
            _ => "?",
        };
        return Klammer(s, Rang(t) < umgebung || umgebung == PPotenz + 1 && Rang(t) < PAtom);
    }

    static string ASumme(Summe s)
    {
        var sb = new StringBuilder();
        for (var k = 0; k < s.Glieder.Count; k++)
        {
            var g = s.Glieder[k];
            if (k == 0) { sb.Append(A(g, PSumme)); continue; }
            if (Negativ(g)) sb.Append(" − ").Append(A(Negiert(g), PProdukt)); else sb.Append(" + ").Append(A(g, PProdukt));
        }
        return sb.ToString();
    }

    static string AProdukt(IReadOnlyList<Term> faktoren)
    {
        var oben = new List<Term>(); var unten = new List<Term>(); var koeff = Bruch.Eins;
        foreach (var f in faktoren)
        {
            if (f is Zahl z) koeff *= z.Wert;
            else if (f is Potenz w && w.Exponent is Zahl e && e.Wert.Vorzeichen < 0)
            {
                unten.Add(-e.Wert == Bruch.Eins ? w.Basis : new Potenz(w.Basis, new Zahl(-e.Wert)));
            }
            else oben.Add(f);
        }
        var neg = koeff.Vorzeichen < 0; koeff = koeff.Betrag;
        if (!koeff.IstGanz) { unten.Insert(0, new Zahl(new Bruch(koeff.N, 1))); koeff = new Bruch(koeff.Z, 1); }
        var sb = new StringBuilder();
        if (koeff != Bruch.Eins || oben.Count == 0) sb.Append(koeff.ToString());
        for (var k = 0; k < oben.Count; k++)
        {
            var f = oben[k];
            var txt = A(f, PProdukt + 1);
            if (sb.Length > 0)
            {
                var vorher = k == 0 ? null : oben[k - 1];
                var dicht = (vorher is null || vorher is Var vv && vv.Name.Length == 1 || vorher is Potenz pv && pv.Basis is Var pb && pb.Name.Length == 1)
                            && (f is Var fv && fv.Name.Length == 1 || f is Potenz fp && fp.Basis is Var fb && fb.Name.Length == 1 && !(fp.Exponent is Zahl fe && fe.Wert == new Bruch(1, 2)) || txt.StartsWith('('));
                if (!dicht) sb.Append('·');
            }
            sb.Append(txt);
        }
        var o = sb.ToString();
        if (unten.Count > 0)
        {
            var u = unten.Count == 1 ? A(unten[0], PAtom) : "(" + AProdukt(unten) + ")";
            if (unten.Count == 1 && Rang(unten[0]) < PAtom && !u.StartsWith('(')) u = "(" + u + ")";
            if (oben.Count > 0 && Rang(oben.Count == 1 && koeff == Bruch.Eins ? oben[0] : new Produkt(oben)) < PProdukt) o = "(" + o + ")";
            o = o + "/" + u;
        }
        return (neg ? "−" : "") + o;
    }

    static string APotenz(Potenz w)
    {
        if (w.Exponent is Zahl e && e.Wert == new Bruch(1, 2))
        {
            var inner = A(w.Basis, 0);
            return w.Basis is Var or Zahl { Wert.IstGanz: true, Wert.Vorzeichen: >= 0 } ? "√" + inner : "√(" + inner + ")";
        }
        var b = A(w.Basis, PPotenz + 1);
        if (w.Exponent is Zahl z && z.Wert.IstGanz && z.Wert.Vorzeichen >= 0 && z.Wert.Z < 10)
            return b + Hochzahlen[(int)z.Wert.Z];
        var ex = A(w.Exponent, PAtom);
        if (w.Exponent is not (Var or Zahl { Wert.IstGanz: true, Wert.Vorzeichen: >= 0 }) && !ex.StartsWith('(')) ex = "(" + ex + ")";
        return b + "^" + ex;
    }

    // ---------------- Eingabe (Rechner, Graph) ----------------
    /// <summary>Schreibweise für den Rechner (<see cref="Calc"/>) und zum erneuten Einlesen: nur * / ^, Klammern um alles Zusammengesetzte.</summary>
    public static string Eingabe(Term t) => t switch
    {
        Zahl z => z.Wert.IstGanz && z.Wert.Vorzeichen >= 0 ? z.Wert.Eingabe() : "(" + z.Wert.Eingabe() + ")",
        Konst k => k.Name == "pi" ? "pi" : "e",
        Var v => v.Name,
        Summe s => "(" + string.Join("+", s.Glieder.Select(Eingabe)) + ")",
        Produkt p => "(" + string.Join("*", p.Faktoren.Select(Eingabe)) + ")",
        Potenz w when w.Exponent is Zahl e && e.Wert == new Bruch(1, 2) => "sqrt(" + Eingabe(w.Basis) + ")",
        Potenz w when w.Basis is Konst { Name: "e" } => "exp(" + Eingabe(w.Exponent) + ")",
        Potenz w => "(" + Eingabe(w.Basis) + ")^(" + Eingabe(w.Exponent) + ")",
        Fkt f => f.Name + "(" + Eingabe(f.Argument) + ")",
        _ => "?",
    };

    // ---------------- LaTeX ----------------
    public static string Latex(Term t) => L(t, 0);

    static string L(Term t, int umgebung)
    {
        var s = t switch
        {
            Zahl z => z.Wert.IstGanz ? z.Wert.Eingabe() : (z.Wert.Vorzeichen < 0 ? "-" : "") + $"\\frac{{{BigInteger.Abs(z.Wert.Z)}}}{{{z.Wert.N}}}",
            Konst k => k.Name == "pi" ? "\\pi" : "e",
            Var v => v.Name.Contains('_') ? v.Name[..v.Name.IndexOf('_')] + "_{" + v.Name[(v.Name.IndexOf('_') + 1)..] + "}" : v.Name.Replace("Δ", "\\Delta "),
            Summe sm => LSumme(sm),
            Produkt p => LProdukt(p.Faktoren),
            Potenz w when w.Exponent is Zahl e && e.Wert.Vorzeichen < 0 => LProdukt([w]),
            Potenz w when w.Exponent is Zahl e && e.Wert == new Bruch(1, 2) => "\\sqrt{" + L(w.Basis, 0) + "}",
            Potenz w => L(w.Basis, PPotenz + 1) + "^{" + L(w.Exponent, 0) + "}",
            Fkt f when f.Name == "abs" => "\\left|" + L(f.Argument, 0) + "\\right|",
            Fkt f => (f.Name switch { "asin" => "\\arcsin", "acos" => "\\arccos", "atan" => "\\arctan", "log" => "\\lg", _ => "\\" + f.Name }) + "\\left(" + L(f.Argument, 0) + "\\right)",
            _ => "?",
        };
        var k2 = Rang(t) < umgebung || umgebung == PPotenz + 1 && Rang(t) < PAtom;
        return k2 ? "\\left(" + s + "\\right)" : s;
    }

    static string LSumme(Summe s)
    {
        var sb = new StringBuilder();
        for (var k = 0; k < s.Glieder.Count; k++)
        {
            var g = s.Glieder[k];
            if (k == 0) { sb.Append(L(g, PSumme)); continue; }
            if (Negativ(g)) sb.Append(" - ").Append(L(Negiert(g), PProdukt)); else sb.Append(" + ").Append(L(g, PProdukt));
        }
        return sb.ToString();
    }

    static string LProdukt(IReadOnlyList<Term> faktoren)
    {
        var oben = new List<Term>(); var unten = new List<Term>(); var koeff = Bruch.Eins;
        foreach (var f in faktoren)
        {
            if (f is Zahl z) koeff *= z.Wert;
            else if (f is Potenz w && w.Exponent is Zahl e && e.Wert.Vorzeichen < 0) unten.Add(-e.Wert == Bruch.Eins ? w.Basis : new Potenz(w.Basis, new Zahl(-e.Wert)));
            else oben.Add(f);
        }
        var neg = koeff.Vorzeichen < 0; koeff = koeff.Betrag;
        var zO = new Bruch(koeff.Z, 1); var zU = new Bruch(koeff.N, 1);
        string Teil(List<Term> fs, Bruch c)
        {
            var parts = new List<string>();
            if (c != Bruch.Eins || fs.Count == 0) parts.Add(c.Eingabe());
            parts.AddRange(fs.Select(f => L(f, PProdukt + 1)));
            return string.Join(" \\cdot ", parts);
        }
        var o = Teil(oben, zO);
        var r = unten.Count == 0 && zU == Bruch.Eins ? o : "\\frac{" + o + "}{" + Teil(unten, zU) + "}";
        return (neg ? "-" : "") + r;
    }
}
