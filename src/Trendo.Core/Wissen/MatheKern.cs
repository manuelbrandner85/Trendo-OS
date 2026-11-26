using System.Globalization;
using System.Numerics;
using System.Text;

namespace Trendo.Core.Wissen;

/// <summary>T-0781 Mathe-Helfer: Art der Aufgabe.</summary>
public enum MatheArt { Automatisch, Vereinfachen, Ausmultiplizieren, Faktorisieren, Gleichung, Gleichungssystem, Bruchrechnen, Ableiten, Integrieren }

/// <summary>Ein Schritt des Lösungswegs: was gemacht wird (Text) und die Rechnung (Formel).</summary>
public sealed record Schritt(string Text, string Formel);

/// <summary>Probe (Einsetzen oder Gegenrechnung): bestanden? und was gerechnet wurde.</summary>
public sealed record Probe(bool Ok, string Text)
{
    /// <summary>T-0781 (Gegenprüfung): die Probe konnte die Vollständigkeit oder Richtigkeit nicht sicher bestätigen – gelb anzeigen, nie als „bestanden“ zählen.</summary>
    public bool Unsicher { get; init; }
    public static Probe Gelb(string text) => new(false, "⚠ " + text) { Unsicher = true };
}

/// <summary>Aufgabe an den Rechenkern.</summary>
public sealed record MatheAuftrag(MatheArt Art, string Eingabe, string? Variable = null, string? Von = null, string? Bis = null);

/// <summary>Ergebnis mit Lösungsweg. <see cref="Graph"/>: Funktionen für den Funktionsgraph (Eingabeform, mit „;“ getrennt), sonst leer.</summary>
public sealed record Loesung(MatheArt Art, string Eingabe, string Aufgabe, IReadOnlyList<Schritt> Schritte, string Ergebnis, string ErgebnisLatex, IReadOnlyList<Probe> Proben, string Graph, string Variable)
{
    public bool ProbeOk => Proben.Count > 0 && Proben.All(p => p.Ok && !p.Unsicher);
    /// <summary>Mindestens eine Probe ist unsicher (gelb) und keine ist fehlgeschlagen.</summary>
    public bool Unsicher => Proben.Any(p => p.Unsicher) && !Proben.Any(p => !p.Ok && !p.Unsicher);
}

/// <summary>Eigener, geprüfter Rechenkern des Mathe-Helfers: Terme vereinfachen, ausmultiplizieren und faktorisieren, lineare und quadratische Gleichungen
/// (auch höheren Grades mit rationalen Nullstellen), lineare Gleichungssysteme (Gauß), Bruchrechnen, Ableitungen und einfache Integrale – jeweils mit
/// Lösungsweg Schritt für Schritt und einer Probe, die unabhängig vom Lösungsweg rechnet (Einsetzen, Ausmultiplizieren, Gegenrechnung mit dem Rechner).
/// Exakt mit Brüchen; Gleitkomma nur für Näherungen und die numerische Gegenprobe.</summary>
public static partial class MatheKern
{
    public static string ArtName(MatheArt a) => a switch
    {
        MatheArt.Automatisch => "Automatisch", MatheArt.Vereinfachen => "Vereinfachen", MatheArt.Ausmultiplizieren => "Ausmultiplizieren", MatheArt.Faktorisieren => "Faktorisieren",
        MatheArt.Gleichung => "Gleichung lösen", MatheArt.Gleichungssystem => "Gleichungssystem", MatheArt.Bruchrechnen => "Bruchrechnen", MatheArt.Ableiten => "Ableiten", MatheArt.Integrieren => "Integrieren",
        _ => a.ToString(),
    };

    public static readonly IReadOnlyDictionary<string, MatheArt> ArtVonId = new Dictionary<string, MatheArt>(StringComparer.OrdinalIgnoreCase)
    {
        ["auto"] = MatheArt.Automatisch, ["vereinfachen"] = MatheArt.Vereinfachen, ["ausmultiplizieren"] = MatheArt.Ausmultiplizieren, ["faktorisieren"] = MatheArt.Faktorisieren,
        ["gleichung"] = MatheArt.Gleichung, ["system"] = MatheArt.Gleichungssystem, ["bruch"] = MatheArt.Bruchrechnen, ["ableiten"] = MatheArt.Ableiten, ["integrieren"] = MatheArt.Integrieren,
    };
    public static string IdVon(MatheArt a) => ArtVonId.First(kv => kv.Value == a).Key;

    static string A(Term t) => TermText.Anzeige(t);
    static Term V(Term t) => Vereinfacher.Vereinfache(t);

    /// <summary>Welche Art passt zur Eingabe?</summary>
    public static MatheArt Erkenne(string eingabe)
    {
        var s = Vorbereiten(eingabe);
        if (s.Contains('∫')) return MatheArt.Integrieren;
        var teile = Gleichungen(s);
        if (teile.Count > 1) return MatheArt.Gleichungssystem;
        if (s.Contains('=')) return MatheArt.Gleichung;
        var t = TermLeser.Lies(s);
        if (!t.Variablen().Any()) return MatheArt.Bruchrechnen;
        return HatKlammerProdukt(t) ? MatheArt.Ausmultiplizieren : MatheArt.Vereinfachen;
    }

    static bool HatKlammerProdukt(Term t) => t switch
    {
        Produkt p => p.Faktoren.Any(f => f is Summe || f is Potenz { Basis: Summe }) || p.Faktoren.Any(HatKlammerProdukt),
        Potenz w => w.Basis is Summe && w.Exponent is Zahl { Wert.IstGanz: true } || HatKlammerProdukt(w.Basis),
        Summe s => s.Glieder.Any(HatKlammerProdukt),
        _ => false,
    };

    static string Vorbereiten(string s) => s.Trim().Replace("≔", "=").Replace("==", "=");

    static List<string> Gleichungen(string s) => [.. s.Split([';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(x => x.Length > 0)];

    /// <summary>Rechnen. Wirft <see cref="FormatException"/> oder <see cref="ArgumentException"/> mit einem Satz für die Person, wenn die Aufgabe nicht geht.</summary>
    /// <param name="zeit">Zeitgrenze (Standard 3 s); die Oberfläche versucht es zuerst mit 0,1 s und rechnet sonst im Hintergrund weiter.</param>
    public static Loesung Rechne(MatheAuftrag a, TimeSpan? zeit = null)
    {
        using var grenze = Rechenzeit.Grenze(zeit ?? Rechenzeit.Standard); // Gegenprüfung T-0781: nie einfrieren
        var eingabe = Vorbereiten(a.Eingabe ?? "");
        if (eingabe.Length == 0) throw new FormatException("Bitte eine Aufgabe eingeben.");
        TermLeser.HinweiseLeeren();
        var art = a.Art == MatheArt.Automatisch ? Erkenne(eingabe) : a.Art;
        var l = art switch
        {
            MatheArt.Vereinfachen => Vereinfachen(eingabe, false),
            MatheArt.Ausmultiplizieren => Vereinfachen(eingabe, true),
            MatheArt.Faktorisieren => Faktorisieren(eingabe, a.Variable),
            MatheArt.Gleichung => Gleichung(eingabe, a.Variable),
            MatheArt.Gleichungssystem => System(eingabe),
            MatheArt.Bruchrechnen => Bruchrechnen(eingabe),
            MatheArt.Ableiten => Ableiten(eingabe, a.Variable),
            MatheArt.Integrieren => Integrieren(eingabe, a.Variable, a.Von, a.Bis),
            _ => throw new ArgumentException("Unbekannte Aufgabenart."),
        };
        // T-0781 (2. Prüfrunde): mehrdeutige Schreibweisen als erster Schritt zeigen
        return TermLeser.Hinweise.Count == 0 ? l : l with { Schritte = [.. TermLeser.Hinweise.Select(h => new Schritt("Hinweis zur Schreibweise", h)), .. l.Schritte] };
    }

    // =====================================================================================
    // Vereinfachen / Ausmultiplizieren
    // =====================================================================================

    static Loesung Vereinfachen(string eingabe, bool ausmultiplizieren)
    {
        if (eingabe.Contains('=')) throw new FormatException("Das ist eine Gleichung – wähle „Gleichung lösen“.");
        var t = TermLeser.Lies(eingabe);
        var art = ausmultiplizieren ? MatheArt.Ausmultiplizieren : MatheArt.Vereinfachen;
        var schritte = new List<Schritt>();
        var poly = Polynom.Aus(t);
        Term ergebnis;
        if (poly is not null)
        {
            // Klammern auflösen: jedes Glied für sich, noch ohne Zusammenfassen
            var glieder = t is Summe s ? s.Glieder : [t];
            var roh = new List<Term>(); var regeln = new List<string>();
            foreach (var g in glieder) roh.AddRange(Aufloesen(g, regeln));
            var rohTerm = roh.Count == 1 ? roh[0] : new Summe(roh);
            if (rohTerm.Schluessel != t.Schluessel && regeln.Count > 0)
                schritte.Add(new Schritt("Klammern auflösen" + (regeln.Count > 0 ? " (" + string.Join(", ", regeln.Distinct()) + ")" : ""), A(t) + " = " + A(rohTerm)));
            ergebnis = poly.AlsTerm();
            if (ergebnis.Schluessel != rohTerm.Schluessel)
            {
                var gruppen = GleicheGlieder(roh);
                schritte.Add(new Schritt("Gleichartige Glieder zusammenfassen" + (gruppen.Length > 0 ? ": " + gruppen : ""), A(rohTerm) + " = " + A(ergebnis)));
            }
        }
        else
        {
            ergebnis = V(t);
            if (ergebnis.Schluessel != t.Schluessel) schritte.Add(new Schritt("Zahlen ausrechnen, gleiche Glieder und gleiche Basen zusammenfassen (Potenzgesetze)", A(t) + " = " + A(ergebnis)));
        }
        if (schritte.Count == 0) schritte.Add(new Schritt("Der Term lässt sich nicht weiter vereinfachen.", A(t)));
        // T-0781 (Gegenprüfung): Wer kürzt, verliert Definitionslücken – x/x = 1 gilt nur für x ≠ 0
        var luecken = NennerNull(t).Where(l => !NennerNull(ergebnis).Contains(l)).ToList();
        if (luecken.Count > 0) schritte.Add(new Schritt("Definitionsbereich: Der Ausgangsterm ist an diesen Stellen nicht definiert – die Vereinfachung gilt nur dort, wo der Nenner nicht 0 ist", string.Join(", ", luecken)));
        var proben = new List<Probe> { ProbeGleichwertig(t, ergebnis) };
        var vars = ergebnis.Variablen().ToList();
        return new Loesung(art, eingabe, (ausmultiplizieren ? "Multipliziere aus: " : "Vereinfache: ") + A(t), schritte, A(ergebnis), TermText.Latex(ergebnis), proben,
            vars.Count == 1 ? TermText.Eingabe(t) + ";" + TermText.Eingabe(ergebnis) : "", vars.FirstOrDefault() ?? "x");
    }

    /// <summary>Ein Glied in einzelne Monome zerlegen (ohne Zusammenfassen), Regeln merken (binomische Formeln, Distributivgesetz).</summary>
    static List<Term> Aufloesen(Term g, List<string> regeln)
    {
        switch (g)
        {
            case Summe s: return [.. s.Glieder.SelectMany(x => Aufloesen(x, regeln))];
            case Potenz w when w.Basis is Summe bs && bs.Glieder.Count == 2 && w.Exponent is Zahl { Wert: var e } && e == new Bruch(2):
                {
                    var (a, b) = (bs.Glieder[0], bs.Glieder[1]);
                    var minus = TermText.Negativ(b);
                    regeln.Add(minus ? "2. binomische Formel (a − b)² = a² − 2ab + b²" : "1. binomische Formel (a + b)² = a² + 2ab + b²");
                    var bb = minus ? TermText.Negiert(b) : b;
                    var mitte = V(Term.Mal(new Zahl(minus ? -2 : 2), a, bb));
                    return [.. new[] { V(Term.Hoch(a, 2)), mitte, V(Term.Hoch(bb, 2)) }.SelectMany(x => Aufloesen(x, regeln)).Select(V)];
                }
            case Produkt p when p.Faktoren.Count == 2 && p.Faktoren[0] is Summe s1 && p.Faktoren[1] is Summe s2 && s1.Glieder.Count == 2 && s2.Glieder.Count == 2
                                && s1.Glieder[0].Schluessel == s2.Glieder[0].Schluessel && V(Term.Plus(s1.Glieder[1], s2.Glieder[1])) is Zahl { Wert.IstNull: true }:
                {
                    regeln.Add("3. binomische Formel (a + b)(a − b) = a² − b²");
                    var a = s1.Glieder[0]; var b = TermText.Negativ(s1.Glieder[1]) ? s2.Glieder[1] : s1.Glieder[1];
                    return [V(Term.Hoch(a, 2)), V(Term.Minus(Term.Hoch(b, 2)))];
                }
        }
        var poly = Polynom.Aus(g);
        if (poly is null) return [g];
        var hatKlammer = HatKlammerProdukt(g);
        if (!hatKlammer) return [V(g)];
        // Distributivgesetz: Faktor für Faktor ausmultiplizieren, Monome einzeln behalten
        regeln.Add("jedes Glied mit jedem multiplizieren");
        var faktoren = g is Produkt pp ? pp.Faktoren : [g];
        List<Term> acc = [Term.Eins];
        foreach (var f in faktoren)
        {
            List<Term> teil = f switch
            {
                Summe fs => [.. fs.Glieder],
                Potenz fw when fw.Basis is Summe && fw.Exponent is Zahl { Wert.IstGanz: true } fz => [.. (Polynom.Aus(fw)?.AlsTerm() is Summe ps ? ps.Glieder : [Polynom.Aus(fw)!.AlsTerm()])],
                _ => [f],
            };
            acc = [.. acc.SelectMany(x => teil.Select(y => V(Term.Mal(x, y))))];
            if (acc.Count > 400) return [poly.AlsTerm()];
        }
        return acc;
    }

    static string GleicheGlieder(List<Term> roh)
    {
        var groups = roh.Select(Vereinfacher.Zerlege).GroupBy(x => x.Teil?.Schluessel ?? "#").Where(g => g.Count() > 1).ToList();
        var parts = new List<string>();
        foreach (var g in groups.Take(4))
        {
            var sum = g.Aggregate(Bruch.Null, (s, x) => s + x.Koeff);
            var rest = g.First().Teil;
            parts.Add(string.Join(" + ", g.Select(x => rest is null ? x.Koeff.ToString() : A(V(Term.Mal(new Zahl(x.Koeff), rest))))).Replace("+ −", "− ") + " = " + (rest is null ? sum.ToString() : A(V(Term.Mal(new Zahl(sum), rest)))));
        }
        return string.Join("; ", parts);
    }

    /// <summary>Probe für Umformungen: Polynome exakt vergleichen, sonst an Probestellen einsetzen.</summary>
    static Probe ProbeGleichwertig(Term vorher, Term nachher)
    {
        if (Polynom.Aus(vorher) is { } p1 && Polynom.Aus(nachher) is { } p2)
        {
            // unabhängig vom Lösungsweg: an drei Stellen exakt einsetzen
            var vars = p1.Variablen.Union(p2.Variablen).Union(vorher.Variablen()).Union(nachher.Variablen()).Distinct().ToList(); // x⁰: Variable bleibt im Ausgangsterm
            var stellen = new[] { new Bruch(2), new Bruch(-3, 2), new Bruch(7, 3) };
            var ok = true; var txt = new List<string>();
            foreach (var (st, k) in stellen.Select((s, k) => (s, k)))
            {
                var werte = vars.Select((v, i) => (v, stellen[(k + i) % stellen.Length] + new Bruch(i))).ToDictionary(x => x.v, x => x.Item2);
                var a = Auswertung.Exakt(vorher, werte); var b = Auswertung.Exakt(nachher, werte);
                ok &= a is not null && a == b;
                if (k == 0) txt.Add($"Einsetzen {string.Join(", ", werte.Select(w => w.Key + " = " + w.Value))}: vorher {a}, nachher {b}");
            }
            return new Probe(ok, (ok ? "✓ Gleichwertig – " : "✗ Nicht gleichwertig – ") + string.Join("; ", txt) + " (dazu zwei weitere Stellen, exakt mit Brüchen).");
        }
        return ProbeNumerisch(vorher, nachher, "Einsetzen an Probestellen");
    }

    /// <summary>Stellen, an denen ein Nenner 0 wird („x ≠ 0“, „x ≠ 3/2“) – nur für lineare Nenner exakt, sonst „Nenner ≠ 0“.</summary>
    static List<string> NennerNull(Term t)
    {
        var res = new List<string>();
        void Walk(Term u)
        {
            switch (u)
            {
                case Summe s: foreach (var g in s.Glieder) Walk(g); break;
                case Produkt p: foreach (var g in p.Faktoren) Walk(g); break;
                case Potenz w:
                    Walk(w.Basis);
                    if (w.Exponent is Zahl e && e.Wert.Vorzeichen < 0 && w.Basis.Variablen().ToList() is [var v] && Polynom.Aus(w.Basis)?.Koeffizienten(v) is { } k)
                    {
                        if (k.Length == 2 && !k[1].IstNull) res.Add($"{v} ≠ {-k[0] / k[1]}");
                        else if (k.Length > 2) res.Add($"{A(w.Basis)} ≠ 0");
                    }
                    break;
                case Fkt f: Walk(f.Argument); break;
            }
        }
        Walk(t);
        return [.. res.Distinct()];
    }

    static Probe ProbeNumerisch(Term vorher, Term nachher, string was)
    {
        var vars = vorher.Variablen().Union(nachher.Variablen()).ToList();
        var punkte = new[] { 0.7, 1.3, 2.1, -0.6, 3.7 };
        var geprueft = 0; var ok = true; var bsp = "";
        foreach (var x in punkte)
        {
            var w = vars.Select((v, i) => (v, x + 0.37 * i)).ToDictionary(t => t.v, t => t.Item2);
            double a, b;
            try { a = Auswertung.Wert(vorher, w); b = Auswertung.Wert(nachher, w); } catch (ArgumentException) { continue; }
            if (!double.IsFinite(a) || !double.IsFinite(b)) continue;
            geprueft++;
            var gut = Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Abs(a));
            ok &= gut;
            if (bsp.Length == 0) bsp = $"{string.Join(", ", w.Select(kv => kv.Key + " = " + Zahlwort(kv.Value)))}: {Zahlwort(a)} und {Zahlwort(b)}";
        }
        if (geprueft < 2) return new Probe(false, "✗ Die Probe ließ sich an keiner Stelle ausrechnen.");
        return new Probe(ok, (ok ? "✓ " : "✗ ") + $"{was} ({geprueft} Stellen, Gleitkomma): {bsp}");
    }

    public static string Zahlwort(double v) => double.IsFinite(v) ? Math.Round(v, 10).ToString("0.##########", CultureInfo.GetCultureInfo("de-DE")).Replace('-', '−') : "—";

    // =====================================================================================
    // Faktorisieren
    // =====================================================================================

    static Loesung Faktorisieren(string eingabe, string? variable)
    {
        if (eingabe.Contains('=')) throw new FormatException("Faktorisieren geht nur mit einem Term, nicht mit einer Gleichung.");
        var t = TermLeser.Lies(eingabe);
        var p = Polynom.Aus(t) ?? throw new ArgumentException("Faktorisieren geht nur bei Polynomen (ohne Wurzeln, Funktionen oder Variablen im Nenner).");
        if (p.IstNull) throw new ArgumentException("Der Term ist 0.");
        var schritte = new List<Schritt>();
        var normal = p.AlsTerm();
        if (normal.Schluessel != t.Schluessel) schritte.Add(new Schritt("Ausmultiplizieren und ordnen", A(t) + " = " + A(normal)));
        var faktoren = new List<(Polynom P, int Mal)>();

        // 1. Gemeinsamen Faktor ausklammern (Zahl und Variablen)
        var glieder = p.Glieder.ToList();
        var zg = glieder.Select(g => BigInteger.Abs(g.C.Z)).Aggregate(BigInteger.GreatestCommonDivisor);
        var nk = glieder.Select(g => g.C.N).Aggregate(Bruch.Kgv);
        var fuehrend = PolyFuehrend(p);
        var c = new Bruch(zg, nk) * (fuehrend.Vorzeichen < 0 ? Bruch.MinusEins : Bruch.Eins);
        var vars = p.Variablen;
        var gemeinsam = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var v in vars) { var m = glieder.Min(g => g.Exp.GetValueOrDefault(v)); if (m > 0) gemeinsam[v] = m; }
        var aussen = Polynom.Konstante(c);
        foreach (var (v, n) in gemeinsam) aussen *= Polynom.Variable(v).Hoch(n);
        var rest = Teile(p, c, gemeinsam);
        if (!(c == Bruch.Eins && gemeinsam.Count == 0))
            schritte.Add(new Schritt("Gemeinsamen Faktor ausklammern", A(normal) + " = " + A(aussen.AlsTerm()) + "·(" + A(rest.AlsTerm()) + ")"));

        var x = variable is { Length: > 0 } vv ? vv : rest.Variablen.Count == 1 ? rest.Variablen[0] : null;
        var weiter = new List<(Polynom P, int Mal)>();
        if (x is not null && rest.Koeffizienten(x) is { } k && k.Length >= 3)
            ZerlegeUnivariat(k, x, weiter, schritte);
        else if (rest.Variablen.Count >= 1 && rest.Anzahl == 2 && Quadratdifferenz(rest) is { } qd)
        {
            schritte.Add(new Schritt("3. binomische Formel: a² − b² = (a + b)(a − b)", A(rest.AlsTerm()) + " = (" + A(qd.Plus.AlsTerm()) + ")(" + A(qd.Minus.AlsTerm()) + ")"));
            weiter.Add((qd.Plus, 1)); weiter.Add((qd.Minus, 1));
        }
        else weiter.Add((rest, 1));

        var produkt = FaktorTerm(aussen, weiter);
        var ergebnisText = FaktorAnzeige(aussen, weiter);
        if (schritte.Count == 0 || weiter.Count == 1 && c == Bruch.Eins && gemeinsam.Count == 0) schritte.Add(new Schritt("Über den rationalen Zahlen lässt sich der Term nicht weiter zerlegen.", A(normal)));
        else schritte.Add(new Schritt("Ergebnis", A(normal) + " = " + ergebnisText));

        // Probe: Faktoren wieder ausmultiplizieren – muss genau das Polynom ergeben
        var zurueck = aussen; foreach (var (f, n) in weiter) zurueck *= f.Hoch(n);
        var ok = zurueck.Gleich(p);
        var probe = new Probe(ok, (ok ? "✓ " : "✗ ") + "Ausmultipliziert ergibt " + ergebnisText + " wieder " + A(zurueck.AlsTerm()) + (ok ? " – genau der Ausgangsterm." : " – das ist NICHT der Ausgangsterm."));
        var gv = p.Variablen;
        return new Loesung(MatheArt.Faktorisieren, eingabe, "Faktorisiere: " + A(t), schritte, ergebnisText, TermText.Latex(produkt), [probe],
            gv.Count == 1 ? TermText.Eingabe(normal) : "", gv.FirstOrDefault() ?? "x");
    }

    static Bruch PolyFuehrend(Polynom p) => p.Glieder.OrderByDescending(g => g.Exp.Values.Sum()).ThenBy(g => string.Join(",", g.Exp.Keys), StringComparer.Ordinal).First().C;

    static Polynom Teile(Polynom p, Bruch c, SortedDictionary<string, int> gemeinsam)
    {
        var r = new Polynom();
        foreach (var (e, k) in p.Glieder)
        {
            var m = Polynom.Konstante(k / c);
            foreach (var (v, n) in e) { var rn = n - gemeinsam.GetValueOrDefault(v); if (rn > 0) m *= Polynom.Variable(v).Hoch(rn); }
            r += m;
        }
        return r;
    }

    static (Polynom Plus, Polynom Minus)? Quadratdifferenz(Polynom p)
    {
        var g = p.Glieder.ToList();
        if (g.Count != 2) return null;
        var pos = g.FirstOrDefault(x => x.C.Vorzeichen > 0); var neg = g.FirstOrDefault(x => x.C.Vorzeichen < 0);
        if (pos.Exp is null || neg.Exp is null) return null;
        Polynom? W((IReadOnlyDictionary<string, int> Exp, Bruch C) m)
        {
            if (m.C.Betrag.Wurzel() is not { } r || m.Exp.Values.Any(n => n % 2 != 0)) return null;
            var q = Polynom.Konstante(r); foreach (var (v, n) in m.Exp) q *= Polynom.Variable(v).Hoch(n / 2); return q;
        }
        if (W(pos) is not { } a || W(neg) is not { } b) return null;
        return (a + b, a - b);
    }

    /// <summary>Univariates Polynom (Koeffizienten aufsteigend, ohne gemeinsamen Faktor) zerlegen: binomische Formeln, Nullstellen, Polynomdivision.</summary>
    static void ZerlegeUnivariat(Bruch[] k, string x, List<(Polynom P, int Mal)> faktoren, List<Schritt> schritte)
    {
        var rest = k.ToArray();
        var guard = 0;
        while (rest.Length - 1 > 2 && guard++ < 20)
        {
            if (RationaleNullstelle(rest) is not { } r) break;
            var (q, _) = Polynom.Horner(rest, r);
            var lin = Polynom.AusKoeffizienten(x, [-r, Bruch.Eins]);
            schritte.Add(new Schritt($"Nullstelle {x} = {r} durch Probieren der Teiler finden und Polynomdivision durch ({A(lin.AlsTerm())})",
                $"({A(Polynom.AusKoeffizienten(x, rest).AlsTerm())}) : ({A(lin.AlsTerm())}) = {A(Polynom.AusKoeffizienten(x, q).AlsTerm())}"));
            Hinzu(faktoren, lin);
            rest = q;
        }
        if (rest.Length - 1 == 2)
        {
            Bruch c = rest[0], b = rest[1], a = rest[2];
            var qp = Polynom.AusKoeffizienten(x, rest);
            // binomische Formeln
            if (a.Wurzel() is { } wa && c.Wurzel() is { } wc && !c.IstNull && (b == 2 * wa * wc || b == -2 * wa * wc))
            {
                var minus = b.Vorzeichen < 0;
                var lin = Polynom.AusKoeffizienten(x, [minus ? -wc : wc, wa]);
                schritte.Add(new Schritt(minus ? "2. binomische Formel rückwärts: a² − 2ab + b² = (a − b)²" : "1. binomische Formel rückwärts: a² + 2ab + b² = (a + b)²", A(qp.AlsTerm()) + " = (" + A(lin.AlsTerm()) + ")²"));
                Hinzu(faktoren, lin); Hinzu(faktoren, lin); return;
            }
            if (b.IstNull && a.Vorzeichen > 0 && c.Vorzeichen < 0 && a.Wurzel() is { } sa && (-c).Wurzel() is { } sc)
            {
                var p1 = Polynom.AusKoeffizienten(x, [sc, sa]); var p2 = Polynom.AusKoeffizienten(x, [-sc, sa]);
                schritte.Add(new Schritt("3. binomische Formel rückwärts: a² − b² = (a + b)(a − b)", A(qp.AlsTerm()) + " = (" + A(p1.AlsTerm()) + ")(" + A(p2.AlsTerm()) + ")"));
                Hinzu(faktoren, p1); Hinzu(faktoren, p2); return;
            }
            var D = b * b - 4 * a * c;
            if (D.Vorzeichen >= 0 && D.Wurzel() is { } wd)
            {
                var x1 = (-b + wd) / (2 * a); var x2 = (-b - wd) / (2 * a);
                schritte.Add(new Schritt($"Nullstellen mit der Mitternachtsformel: D = b² − 4ac = {D}, {x}₁ = {x1}, {x}₂ = {x2}", $"{A(qp.AlsTerm())} = {(a == Bruch.Eins ? "" : a.ToString())}({A(Polynom.AusKoeffizienten(x, [-x1, Bruch.Eins]).AlsTerm())})({A(Polynom.AusKoeffizienten(x, [-x2, Bruch.Eins]).AlsTerm())})"));
                // Bruch-Nullstellen so verteilen, dass ganzzahlige Faktoren entstehen: a(x − x1)(x − x2) = (n1·x − z1)(n2·x − z2)·a/(n1·n2)
                var f1 = Polynom.AusKoeffizienten(x, [new Bruch(-x1.Z, 1), new Bruch(x1.N, 1)]);
                var f2 = Polynom.AusKoeffizienten(x, [new Bruch(-x2.Z, 1), new Bruch(x2.N, 1)]);
                var faktor = a / new Bruch(x1.N * x2.N, 1);
                if (faktor != Bruch.Eins) faktoren.Add((Polynom.Konstante(faktor), 1));
                Hinzu(faktoren, f1); Hinzu(faktoren, f2); return;
            }
            schritte.Add(new Schritt(D.Vorzeichen < 0 ? $"Keine reellen Nullstellen (D = b² − 4ac = {D} < 0) – der quadratische Faktor bleibt." : $"Die Nullstellen sind nicht rational (D = {D}) – der quadratische Faktor bleibt über den rationalen Zahlen stehen.", A(qp.AlsTerm())));
            Hinzu(faktoren, qp); return;
        }
        Hinzu(faktoren, Polynom.AusKoeffizienten(x, rest));
    }

    static void Hinzu(List<(Polynom P, int Mal)> f, Polynom p)
    {
        var i = f.FindIndex(x => x.P.Gleich(p));
        if (i >= 0) f[i] = (f[i].P, f[i].Mal + 1); else f.Add((p, 1));
    }

    static Term FaktorTerm(Polynom aussen, List<(Polynom P, int Mal)> f)
    {
        var parts = new List<Term> { aussen.AlsTerm() };
        foreach (var (p, n) in f) parts.Add(n == 1 ? p.AlsTerm() : new Potenz(p.AlsTerm(), new Zahl(n)));
        return new Produkt(parts);
    }

    static string FaktorAnzeige(Polynom aussen, List<(Polynom P, int Mal)> f)
    {
        var konst = Bruch.Eins; var teile = new List<string>();
        var a = aussen.AlsTerm();
        foreach (var (p, n) in f)
        {
            if (p.IstKonstant(out var pc)) { konst *= pc.Hoch(n); continue; }
            var s = A(p.AlsTerm());
            var atom = p.Anzahl == 1;
            teile.Add((atom ? s : "(" + s + ")") + (n == 1 ? "" : n == 2 ? "²" : n == 3 ? "³" : "^" + n));
        }
        var vorne = V(Term.Mal(new Zahl(konst), a));
        var v = A(vorne);
        if (v == "1") v = ""; else if (v == "−1") v = "−"; else if (vorne is Summe) v = "(" + v + ")";
        if (v.Length == 0 && teile.Count == 1 && teile[0].StartsWith('(') && teile[0].EndsWith(')')) teile[0] = teile[0][1..^1]; // nicht zerlegbar: ohne Klammer
        else if (teile.Count > 0 && v.Length > 0 && vorne is not Zahl && !teile[0].StartsWith('(')) v += "·";
        return v + string.Join("", teile) is { Length: > 0 } r ? r : "1";
    }

    // =====================================================================================
    // Gleichungen in einer Variablen
    // =====================================================================================

    static string Hoch(int n) => n switch { 2 => "²", 3 => "³", _ => "^" + n };
    static string Index(int n) => string.Concat(n.ToString(CultureInfo.InvariantCulture).Select(c => "₀₁₂₃₄₅₆₇₈₉"[c - '0']));

    static void LoeseUnivariat(Bruch[] k, string x, List<Schritt> schritte, List<(string Text, Bruch? Exakt, WurzelZahl? Wurzel, double Naeherung)> lsg)
    {
        var grad = k.Length - 1;
        if (grad == 0) return;
        if (grad == 1)
        {
            Bruch a = k[1], b = k[0];
            if (!b.IstNull) schritte.Add(new Schritt($"{(b.Vorzeichen > 0 ? "−" + b : "+" + (-b))} auf beiden Seiten", $"{A(Polynom.AusKoeffizienten(x, [Bruch.Null, a]).AlsTerm())} = {-b}"));
            var x0 = -b / a;
            static string K(Bruch v) => v.Vorzeichen < 0 || !v.IstGanz ? "(" + v + ")" : v.ToString();
            if (a != Bruch.Eins) schritte.Add(new Schritt($"Beide Seiten durch {(a.Vorzeichen < 0 ? "(" + a + ")" : a.ToString())} teilen", $"{x} = {K(-b)} : {K(a)} = {x0}"));
            lsg.Add((x0.ToString(), x0, null, x0.AlsDouble()));
            return;
        }
        if (grad == 2)
        {
            Bruch a = k[2], b = k[1], c = k[0];
            if (b.IstNull)
            {
                var r = -c / a;
                schritte.Add(new Schritt("Nach x² umstellen (kein x-Glied)", $"{x}² = {r}"));
                if (r.Vorzeichen < 0) { schritte.Add(new Schritt("Ein Quadrat ist nie negativ – keine reelle Lösung.", $"{x}² = {r} < 0")); return; }
                Wurzeln(Bruch.Null, Bruch.Eins, r, x, schritte, lsg, $"Wurzel ziehen: {x} = ±√({r})");
                return;
            }
            var p = b / a; var q = c / a;
            if (a != Bruch.Eins) schritte.Add(new Schritt($"Normalform: durch {a} teilen", $"{A(Polynom.AusKoeffizienten(x, [q, p, Bruch.Eins]).AlsTerm())} = 0"));
            var halb = p / 2; var D = halb * halb - q;
            schritte.Add(new Schritt($"p-q-Formel mit p = {p} und q = {q}: {x} = −p/2 ± √((p/2)² − q)", $"{x} = {-halb} ± √(({halb})² − ({q})) = {-halb} ± √({D})"));
            if (D.Vorzeichen < 0) { schritte.Add(new Schritt($"Unter der Wurzel steht {D} < 0 – es gibt keine reelle Lösung.", $"D = {D}")); return; }
            if (D.IstNull) { schritte.Add(new Schritt("Unter der Wurzel steht 0 – es gibt genau eine (doppelte) Lösung.", $"{x} = {-halb}")); lsg.Add(((-halb).ToString(), -halb, null, (-halb).AlsDouble())); return; }
            Wurzeln(-halb, Bruch.Eins, D, x, schritte, lsg, null);
            return;
        }
        // höherer Grad: rationale Nullstellen und Polynomdivision, dann quadratischer Rest
        var rest = k.ToArray(); var guard = 0;
        while (rest.Length - 1 > 2 && guard++ < 20)
        {
            if (RationaleNullstelle(rest) is not { } r) break;
            var (qk, _) = Polynom.Horner(rest, r);
            schritte.Add(new Schritt($"Nullstelle {x} = {r} durch Probieren der Teiler des absoluten Glieds finden, dann Polynomdivision durch ({A(Polynom.AusKoeffizienten(x, [-r, Bruch.Eins]).AlsTerm())})",
                $"({A(Polynom.AusKoeffizienten(x, rest).AlsTerm())}) : ({A(Polynom.AusKoeffizienten(x, [-r, Bruch.Eins]).AlsTerm())}) = {A(Polynom.AusKoeffizienten(x, qk).AlsTerm())}"));
            lsg.Add((r.ToString(), r, null, r.AlsDouble()));
            rest = qk;
        }
        if (rest.Length - 1 <= 2) { LoeseUnivariat(rest, x, schritte, lsg); return; }
        var num = ReelleNullstellen(rest);
        schritte.Add(new Schritt($"Keine weitere rationale Nullstelle – die übrigen Lösungen werden näherungsweise bestimmt (Vorzeichenwechsel und Intervallhalbierung).", $"{A(Polynom.AusKoeffizienten(x, rest).AlsTerm())} = 0"));
        foreach (var n in num) lsg.Add(("≈ " + Zahlwort(Math.Round(n, 8)), null, null, n));
    }

    static void Wurzeln(Bruch mitte, Bruch faktor, Bruch D, string x, List<Schritt> schritte, List<(string Text, Bruch? Exakt, WurzelZahl? Wurzel, double Naeherung)> lsg, string? text)
    {
        if (D.Wurzel() is { } w)
        {
            var x1 = mitte + faktor * w; var x2 = mitte - faktor * w;
            schritte.Add(new Schritt((text ?? $"√({D}) = {w}") + " – zwei Lösungen", $"{x}₁ = {mitte} + {w} = {x1};  {x}₂ = {mitte} − {w} = {x2}".Replace("0 + ", "").Replace("0 − ", "−")));
            lsg.Add((x1.ToString(), x1, null, x1.AlsDouble())); lsg.Add((x2.ToString(), x2, null, x2.AlsDouble()));
            return;
        }
        var (f, m) = WurzelZahl.Wurzel(D);
        var z1 = new WurzelZahl(mitte, faktor * f, m); var z2 = new WurzelZahl(mitte, -faktor * f, m);
        schritte.Add(new Schritt((text ?? $"√({D}) lässt sich nicht ganz ziehen") + $" – teilweise Wurzel ziehen: √({D}) = {(f == Bruch.Eins ? "" : f.ToString())}√{m}", $"{x}₁ = {z1} ≈ {Zahlwort(Math.Round(z1.AlsDouble(), 6))};  {x}₂ = {z2} ≈ {Zahlwort(Math.Round(z2.AlsDouble(), 6))}"));
        lsg.Add((z1.ToString(), null, z1, z1.AlsDouble())); lsg.Add((z2.ToString(), null, z2, z2.AlsDouble()));
    }

    // =====================================================================================
    // Lineare Gleichungssysteme (Gauß)
    // =====================================================================================

    static readonly string[] Roemisch = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X"];

    static Loesung System(string eingabe)
    {
        var gl = Gleichungen(eingabe);
        if (gl.Count < 2) throw new FormatException("Ein Gleichungssystem hat mindestens zwei Gleichungen – trenne sie mit „;“ oder einer neuen Zeile.");
        if (gl.Count > 10) throw new FormatException("Höchstens 10 Gleichungen.");
        var paare = new List<(Term L, Term R)>();
        foreach (var g in gl)
        {
            var t = g.Split('=');
            if (t.Length != 2) throw new FormatException($"„{g}“ ist keine Gleichung mit genau einem „=“.");
            paare.Add((TermLeser.Lies(t[0]), TermLeser.Lies(t[1])));
        }
        var vars = paare.SelectMany(p => p.L.Variablen().Union(p.R.Variablen())).Distinct().OrderBy(v => v, StringComparer.Ordinal).ToList();
        if (vars.Count == 0) throw new ArgumentException("Im System kommt keine Variable vor.");
        if (vars.Count > 10) throw new ArgumentException("Höchstens 10 Variablen.");
        int n = vars.Count, m = paare.Count;
        var M = new Bruch[m, n + 1];
        for (var i = 0; i < m; i++)
        {
            var p = Polynom.Aus(Term.Plus(paare[i].L, Term.Minus(paare[i].R))) ?? throw new ArgumentException($"Gleichung {Roemisch[i]} ist nicht linear.");
            if (p.Gesamtgrad > 1) throw new ArgumentException($"Gleichung {Roemisch[i]} ist nicht linear (Variablen dürfen nur hoch 1 und nicht miteinander malgenommen vorkommen).");
            for (var j = 0; j <= n; j++) M[i, j] = Bruch.Null;
            foreach (var (e, c) in p.Glieder)
            {
                if (e.Count == 0) M[i, n] = -c;
                else M[i, vars.IndexOf(e.Keys.First())] += c;
            }
        }
        var schritte = new List<Schritt> { new("Jede Gleichung ordnen: Variablen links, Zahl rechts", SystemText(M, vars)) };
        // Gauß-Jordan mit Brüchen
        var pivots = new List<int>(); var zeile = 0;
        for (var sp = 0; sp < n && zeile < m; sp++)
        {
            var pr = Enumerable.Range(zeile, m - zeile).FirstOrDefault(r => !M[r, sp].IstNull, -1);
            if (pr < 0) continue;
            if (pr != zeile) { Tausche(M, pr, zeile, n); schritte.Add(new Schritt($"Gleichungen {Roemisch[pr]} und {Roemisch[zeile]} tauschen", SystemText(M, vars))); }
            var piv = M[zeile, sp];
            if (piv != Bruch.Eins)
            {
                for (var j = 0; j <= n; j++) M[zeile, j] /= piv;
                schritte.Add(new Schritt($"Gleichung {Roemisch[zeile]} durch {piv} teilen, damit vor {vars[sp]} eine 1 steht", SystemText(M, vars)));
            }
            var ops = new List<string>();
            for (var r = 0; r < m; r++)
            {
                if (r == zeile || M[r, sp].IstNull) continue;
                var f = M[r, sp];
                for (var j = 0; j <= n; j++) M[r, j] -= f * M[zeile, j];
                ops.Add($"{Roemisch[r]} {(f.Vorzeichen > 0 ? "−" : "+")} {(f.Betrag == Bruch.Eins ? "" : f.Betrag + "·")}{Roemisch[zeile]}");
            }
            if (ops.Count > 0) schritte.Add(new Schritt($"{vars[sp]} aus den anderen Gleichungen entfernen (Additionsverfahren): {string.Join(", ", ops)}", SystemText(M, vars)));
            pivots.Add(sp); zeile++;
        }
        // Widerspruch?
        for (var r = zeile; r < m; r++)
            if (!M[r, n].IstNull)
            {
                schritte.Add(new Schritt($"Gleichung {Roemisch[r]} lautet 0 = {M[r, n]} – ein Widerspruch. Das System hat keine Lösung.", $"0 = {M[r, n]}"));
                var probe = new Probe(true, "✓ Ein Widerspruch (0 = Zahl ≠ 0) kann von keinem Wertepaar erfüllt werden.");
                return new Loesung(MatheArt.Gleichungssystem, eingabe, "Löse das Gleichungssystem", schritte, "L = { } (keine Lösung)", "\\mathbb{L} = \\{\\}", [probe], "", vars[0]);
            }
        var frei = Enumerable.Range(0, n).Where(j => !pivots.Contains(j)).ToList();
        var loes = new Dictionary<string, Term>();
        var parNamen = new[] { "t", "s", "r", "u", "w" }.Where(nm => !vars.Contains(nm)).ToList();
        for (var k2 = 0; k2 < frei.Count; k2++) loes[vars[frei[k2]]] = new Var(parNamen[k2 % parNamen.Count]);
        for (var i = 0; i < pivots.Count; i++)
        {
            var glieder = new List<Term> { new Zahl(M[i, n]) };
            foreach (var j in frei) if (!M[i, j].IstNull) glieder.Add(Term.Mal(new Zahl(-M[i, j]), loes[vars[j]]));
            loes[vars[pivots[i]]] = V(new Summe(glieder));
        }
        var ergebnis = string.Join(";  ", vars.Select(v => $"{v} = {A(loes[v])}"));
        if (frei.Count > 0) ergebnis = "Unendlich viele Lösungen: " + ergebnis + $" ({string.Join(", ", frei.Select((j, k2) => parNamen[k2 % parNamen.Count]))} beliebig)";
        schritte.Add(new Schritt(frei.Count == 0 ? "Lösung ablesen" : $"Es bleiben freie Variablen ({string.Join(", ", frei.Select(j => vars[j]))}) – die Lösung hängt von einem Parameter ab", ergebnis));
        // Probe: Lösung in JEDE ursprüngliche Gleichung einsetzen (exakt; bei Parametern mit zwei Beispielwerten)
        var proben = new List<Probe>();
        var beispiele = frei.Count == 0 ? [new Dictionary<string, Bruch>()] : new[] { new Bruch(0), new Bruch(3, 2) }.Select(b => frei.Select((_, k2) => parNamen[k2 % parNamen.Count]).ToDictionary(p => p, _ => b)).ToList();
        for (var i = 0; i < paare.Count; i++)
        {
            var ok = true; var txt = "";
            foreach (var bsp in beispiele)
            {
                var werte = vars.ToDictionary(v => v, v => Auswertung.Exakt(loes[v], bsp) ?? Bruch.Null);
                var lw = Auswertung.Exakt(paare[i].L, werte); var rw = Auswertung.Exakt(paare[i].R, werte);
                ok &= lw is not null && lw == rw;
                if (txt.Length == 0) txt = $"links {lw}, rechts {rw}";
            }
            proben.Add(new Probe(ok, $"{(ok ? "✓" : "✗")} Gleichung {Roemisch[i]} ({A(paare[i].L)} = {A(paare[i].R)}) mit der Lösung: {txt}{(frei.Count > 0 ? " (für zwei Parameterwerte)" : "")}."));
        }
        var latex = string.Join(",\\quad ", vars.Select(v => $"{v} = {TermText.Latex(loes[v])}"));
        return new Loesung(MatheArt.Gleichungssystem, eingabe, "Löse das Gleichungssystem", schritte, ergebnis, latex, proben, "", vars[0]);
    }

    static void Tausche(Bruch[,] M, int a, int b, int n) { for (var j = 0; j <= n; j++) (M[a, j], M[b, j]) = (M[b, j], M[a, j]); }

    static string SystemText(Bruch[,] M, List<string> vars)
    {
        var sb = new StringBuilder(); var n = vars.Count;
        for (var i = 0; i < M.GetLength(0); i++)
        {
            var glieder = new List<Term>();
            for (var j = 0; j < n; j++) if (!M[i, j].IstNull) glieder.Add(V(Term.Mal(new Zahl(M[i, j]), new Var(vars[j]))));
            var l = glieder.Count == 0 ? "0" : A(glieder.Count == 1 ? glieder[0] : new Summe(glieder));
            if (i > 0) sb.Append(" | ");
            sb.Append($"{Roemisch[i]}: {l} = {M[i, n]}");
        }
        return sb.ToString();
    }

    // =====================================================================================
    // Bruchrechnen
    // =====================================================================================

    static Loesung Bruchrechnen(string eingabe)
    {
        if (eingabe.Contains('=')) throw new FormatException("Beim Bruchrechnen steht kein „=“ – gib nur die Rechnung ein, z. B. 3/4 + 5/6.");
        var t = TermLeser.Lies(eingabe);
        if (t.Variablen().Any() || Hat(t, x => x is Konst or Fkt)) throw new ArgumentException("Bruchrechnen geht nur mit Zahlen (ohne Variablen und Funktionen) – für Terme nimm „Vereinfachen“.");
        var schritte = new List<Schritt>();
        var ergebnis = BruchWert(t, schritte, out var irr);
        if (irr) throw new ArgumentException("Das Ergebnis ist keine Bruchzahl (z. B. eine Wurzel, die nicht aufgeht) – nimm „Vereinfachen“.");
        var anzeige = ergebnis.ToString();
        var gemischt = ergebnis.Gemischt();
        var dez = ergebnis.Dezimal();
        var ergebnisText = anzeige + (gemischt != anzeige ? $" = {gemischt}" : "") + (ergebnis.IstGanz ? "" : $" {(dez.StartsWith('≈') ? dez : "= " + dez)}");
        if (schritte.Count == 0) schritte.Add(new Schritt("Die Zahl ist schon vollständig gekürzt.", anzeige));
        // Gegenrechnung mit dem eingebauten Rechner (eigener Gleitkomma-Leser, unabhängig von der Bruchrechnung)
        Probe probe;
        try
        {
            var calc = Calc.Eval(eingabe.Replace(':', '/').Replace('·', '*'), false, null);
            var erwartet = ergebnis.AlsDouble();
            if (!double.IsFinite(calc) && !double.IsFinite(erwartet)) probe = Probe.Gelb("Die Zahl ist für die Gegenrechnung mit Gleitkomma zu groß – nur exakt gerechnet.");
            else if (double.IsNaN(calc))
            {
                // T-0781 (Gegenprüfung): ungerade Wurzeln negativer Zahlen ((−8)^(1/3)) kann der Taschenrechner nicht – Gegenprobe durch Potenzieren
                var zurueck = Calc.Eval("(" + erwartet.ToString(global::System.Globalization.CultureInfo.InvariantCulture) + ")^3", false, null);
                var ok3 = eingabe.Replace(" ", "").Contains("^(1/3)") && Math.Abs(zurueck - Calc.Eval(eingabe.Replace(" ", "").Replace("^(1/3)", ""), false, null)) <= 1e-9 * Math.Max(1, Math.Abs(zurueck));
                probe = ok3 ? new Probe(true, $"✓ Gegenrechnung: ({Zahlwort(erwartet)})³ = {Zahlwort(zurueck)} – das ist die Zahl unter der dritten Wurzel.") : Probe.Gelb("Der Taschenrechner kann diese Wurzel aus einer negativen Zahl nicht nachrechnen.");
            }
            else
            {
                var ok = Math.Abs(calc - erwartet) <= 1e-9 * Math.Max(1, Math.Abs(calc));
                probe = new Probe(ok, $"{(ok ? "✓" : "✗")} Gegenrechnung mit dem Taschenrechner (Gleitkomma): {Zahlwort(calc)} – Bruch {anzeige} ergibt {Zahlwort(erwartet)}.");
            }
        }
        catch (Exception e) when (e is FormatException or DivideByZeroException or OverflowException)
        {
            probe = Probe.Gelb("Der Taschenrechner konnte die Eingabe nicht nachrechnen (" + e.Message + ") – nur exakt gerechnet.");
        }
        return new Loesung(MatheArt.Bruchrechnen, eingabe, "Berechne: " + A(t), schritte, ergebnisText, TermText.Latex(new Zahl(ergebnis)), [probe], "", "x");
    }

    static bool Hat(Term t, Func<Term, bool> p) => p(t) || t switch
    {
        Summe s => s.Glieder.Any(g => Hat(g, p)), Produkt pr => pr.Faktoren.Any(g => Hat(g, p)), Potenz w => Hat(w.Basis, p) || Hat(w.Exponent, p), Fkt f => Hat(f.Argument, p), _ => false,
    };

    static Bruch BruchWert(Term t, List<Schritt> s, out bool irrational)
    {
        irrational = false;
        switch (t)
        {
            case Zahl z: return z.Wert;
            case Summe sm:
                {
                    var acc = BruchWert(sm.Glieder[0], s, out var i0); irrational |= i0;
                    foreach (var g in sm.Glieder.Skip(1))
                    {
                        var neg = TermText.Negativ(g) && g is not Zahl;
                        var b = BruchWert(g, s, out var ig); irrational |= ig;
                        acc = Addiere(acc, b, s);
                    }
                    return acc;
                }
            case Produkt lit when lit.Faktoren.Count == 2 && lit.Faktoren[0] is Zahl { Wert.IstGanz: true } za && lit.Faktoren[1] is Potenz { Basis: Zahl { Wert.IstGanz: true } zb, Exponent: Zahl { Wert: var le } } && le == Bruch.MinusEins:
                {
                    // Bruch aus der Eingabe („12/18“): nur kürzen
                    if (zb.Wert.IstNull) throw new DivideByZeroException("Teilen durch 0 geht nicht.");
                    var b = za.Wert / zb.Wert; var g = BigInteger.GreatestCommonDivisor(za.Wert.Z, zb.Wert.Z);
                    if (g > 1) s.Add(new Schritt($"Kürzen: Zähler und Nenner durch {g} teilen", $"{new BruchRoh(za.Wert.Z, zb.Wert.Z)} = {b}"));
                    return b;
                }
            case Produkt neg when neg.Faktoren.Count == 2 && neg.Faktoren[0] is Zahl { Wert: var m1 } && m1 == Bruch.MinusEins:
                {
                    var b = BruchWert(neg.Faktoren[1], s, out var ineg); irrational |= ineg; return -b;
                }
            case Produkt p:
                {
                    var acc = Bruch.Eins; var erst = true;
                    foreach (var f in p.Faktoren)
                    {
                        if (f is Potenz w && w.Exponent is Zahl { Wert: var e } && e == Bruch.MinusEins)
                        {
                            var d = BruchWert(w.Basis, s, out var id); irrational |= id;
                            if (d.IstNull) throw new DivideByZeroException("Teilen durch 0 geht nicht.");
                            if (erst) { acc = d.Kehrwert; erst = false; continue; }
                            var vorher = acc; acc = vorher * d.Kehrwert;
                            s.Add(new Schritt("Durch einen Bruch teilen = mit dem Kehrwert malnehmen", $"{K(vorher)} : {K(d)} = {K(vorher)} · {K(d.Kehrwert)} = {MalText(vorher, d.Kehrwert)}"));
                            continue;
                        }
                        var v = BruchWert(f, s, out var iv); irrational |= iv;
                        if (erst) { acc = v; erst = false; continue; }
                        if (f is Zahl { Wert: var fz } && fz == Bruch.MinusEins && p.Faktoren.Count == 2) { acc = -acc; continue; } // reines Vorzeichen
                        var alt = acc; acc = alt * v;
                        s.Add(new Schritt("Brüche malnehmen: Zähler mal Zähler, Nenner mal Nenner – dann kürzen", $"{K(alt)} · {K(v)} = {MalText(alt, v)}"));
                    }
                    return acc;
                }
            case Potenz w:
                {
                    var b = BruchWert(w.Basis, s, out var ib); var e = BruchWert(w.Exponent, s, out var ie); irrational |= ib || ie;
                    if (e.IstGanz)
                    {
                        if (BigInteger.Abs(e.Z) > 64) throw new OverflowException("Der Exponent ist zu groß.");
                        var r = b.Hoch((int)e.Z);
                        s.Add(new Schritt(e.Vorzeichen < 0 ? "Negative Hochzahl: Kehrwert nehmen und potenzieren" : "Zähler und Nenner einzeln potenzieren", $"({b})^{e} = {r}"));
                        return r;
                    }
                    if (e.N <= 12 && b.Wurzel((int)e.N) is { } root)
                    {
                        var r = root.Hoch((int)e.Z);
                        s.Add(new Schritt(e == new Bruch(1, 2) ? "Wurzel aus Zähler und Nenner ziehen" : $"{e.N}. Wurzel ziehen und potenzieren", $"({b})^({e}) = {r}"));
                        return r;
                    }
                    irrational = true; return Bruch.Null;
                }
            default: irrational = true; return Bruch.Null;
        }
    }

    static string K(Bruch b) => b.Vorzeichen < 0 ? "(" + b + ")" : b.ToString();

    static string MalText(Bruch a, Bruch b)
    {
        var z = a.Z * b.Z; var n = a.N * b.N; var g = BigInteger.GreatestCommonDivisor(z, n);
        var roh = $"({a.Z}·{b.Z})/({a.N}·{b.N})".Replace("-", "−");
        if (a.IstGanz && b.IstGanz) return (a * b).ToString();
        return roh + " = " + new BruchRoh(z, n) + (g > 1 ? $" = {a * b} (gekürzt mit {g})" : "");
    }

    /// <summary>Ungekürzter Bruch nur für die Anzeige.</summary>
    readonly struct BruchRoh(BigInteger z, BigInteger n)
    {
        public override string ToString() => (z.Sign * n.Sign < 0 ? "−" : "") + BigInteger.Abs(z) + (BigInteger.Abs(n).IsOne ? "" : "/" + BigInteger.Abs(n));
    }

    static Bruch Addiere(Bruch a, Bruch b, List<Schritt> s)
    {
        var r = a + b;
        var op = b.Vorzeichen < 0 ? "−" : "+";
        var bb = b.Betrag;
        if (a.IstGanz && b.IstGanz) { s.Add(new Schritt(b.Vorzeichen < 0 ? "Subtrahieren" : "Addieren", $"{a} {op} {bb} = {r}")); return r; }
        if (a.N == b.N)
        {
            var zs = a.Z + b.Z;
            s.Add(new Schritt("Gleiche Nenner: Zähler " + (b.Vorzeichen < 0 ? "subtrahieren" : "addieren") + ", Nenner bleibt" + (BigInteger.GreatestCommonDivisor(zs, a.N) > 1 ? ", dann kürzen" : ""),
                $"{a} {op} {bb} = {new BruchRoh(zs, a.N)}" + (new BruchRoh(zs, a.N).ToString() != r.ToString() ? $" = {r}" : "")));
            return r;
        }
        var hn = Bruch.Kgv(a.N, b.N);
        var za = a.Z * (hn / a.N); var zb = BigInteger.Abs(b.Z) * (hn / b.N);
        var summe = a.Z * (hn / a.N) + b.Z * (hn / b.N);
        var g = BigInteger.GreatestCommonDivisor(summe, hn);
        s.Add(new Schritt($"Hauptnenner finden: kgV({a.N}, {b.N}) = {hn}; beide Brüche darauf erweitern", $"{a} {op} {bb} = {new BruchRoh(za, hn)} {op} {new BruchRoh(zb, hn)}"));
        s.Add(new Schritt("Zähler " + (b.Vorzeichen < 0 ? "subtrahieren" : "addieren") + (g > 1 ? $" und mit {g} kürzen" : ""), $"= {new BruchRoh(summe, hn)}" + (g > 1 ? $" = {r}" : "")));
        return r;
    }

    // =====================================================================================
    // Ableiten
    // =====================================================================================

    static (Term F, string X) Funktion(string eingabe, string? variable)
    {
        var s = eingabe.Trim();
        // „f(x) = …“, „y = …“ erlauben
        var gl = s.IndexOf('=');
        if (gl > 0)
        {
            var kopf = s[..gl].Trim();
            if (kopf.Length <= 8 && (kopf.Contains('(') || kopf.Length == 1)) s = s[(gl + 1)..];
            else throw new FormatException("Zum Ableiten nur die Funktion eingeben, z. B. x² · sin(x) oder f(x) = x² · sin(x).");
            if (kopf.Contains('(') && variable is null) { var a = kopf.IndexOf('(') + 1; var b = kopf.IndexOf(')'); if (b > a) variable = kopf[a..b].Trim(); }
        }
        s = s.Replace("∫", "").Trim();
        if (s.EndsWith("dx", StringComparison.Ordinal) || variable is { Length: > 0 } && s.EndsWith("d" + variable, StringComparison.Ordinal)) s = s[..^(1 + (variable ?? "x").Length)].Trim();
        var f = TermLeser.Lies(s);
        var vars = f.Variablen().ToList();
        var x = variable is { Length: > 0 } v ? v : vars.Contains("x") ? "x" : vars.Count == 1 ? vars[0] : vars.Count == 0 ? "x" : throw new ArgumentException($"Nach welcher Variablen? Es kommen {string.Join(", ", vars)} vor – trage die Variable rechts ein.");
        return (f, x);
    }

    static Loesung Ableiten(string eingabe, string? variable)
    {
        var (f, x) = Funktion(eingabe, variable);
        var schritte = new List<Schritt>();
        var roh = Abl(f, x, schritte, 0);
        var erg = V(roh);
        if (Polynom.Aus(erg) is { } pe) erg = pe.AlsTerm();
        if (schritte.Count == 0 || A(erg) != schritte[^1].Formel.Split(" = ").Last()) schritte.Add(new Schritt("Zusammenfassen", $"f'({x}) = {A(erg)}"));
        var proben = new List<Probe> { ProbeAbleitung(f, erg, x) };
        return new Loesung(MatheArt.Ableiten, eingabe, $"Leite ab: f({x}) = {A(f)}", schritte, $"f'({x}) = {A(erg)}", $"f'({x}) = {TermText.Latex(erg)}", proben,
            f.Variablen().All(v => v == x) ? TermText.Eingabe(f).Replace("(" + x + ")", "(x)") + ";" + TermText.Eingabe(erg) : "", x);
    }

    /// <summary>Gegenrechnung: Differenzenquotient (f(x+h) − f(x−h))/2h an Probestellen – unabhängig von den Ableitungsregeln.</summary>
    static Probe ProbeAbleitung(Term f, Term df, string x)
    {
        var punkte = new[] { 0.7, 1.3, 2.1, -0.6, 3.7, 0.35 };
        var ok = true; var n = 0; var bsp = "";
        foreach (var p in punkte)
        {
            double W(double v) { try { return Auswertung.Wert(f, new Dictionary<string, double> { [x] = v }); } catch (ArgumentException) { return double.NaN; } }
            const double h = 1e-5;
            var num = (W(p + h) - W(p - h)) / (2 * h);
            double sym;
            try { sym = Auswertung.Wert(df, new Dictionary<string, double> { [x] = p }); } catch (ArgumentException) { continue; }
            if (!double.IsFinite(num) || !double.IsFinite(sym) || Math.Abs(num) > 1e7) continue;
            n++;
            var gut = Math.Abs(num - sym) <= 1e-4 * Math.Max(1, Math.Abs(sym));
            ok &= gut;
            if (bsp.Length == 0) bsp = $"bei {x} = {Zahlwort(p)}: Differenzenquotient {Zahlwort(Math.Round(num, 6))}, Ableitung {Zahlwort(Math.Round(sym, 6))}";
        }
        if (n < 2) return new Probe(false, "✗ Die Gegenprobe ließ sich an keiner Stelle ausrechnen.");
        return new Probe(ok, $"{(ok ? "✓" : "✗")} Gegenrechnung mit dem Differenzenquotienten an {n} Stellen – {bsp}.");
    }

    static Term Abl(Term t, string x, List<Schritt> s, int tiefe)
    {
        Rechenzeit.Pruefe();
        if (!t.Enthaelt(x)) return Term.Null;
        var idx = s.Count; var merken = tiefe <= 3 && s.Count < 16;
        if (merken) s.Add(new Schritt("", ""));
        string regel; Term roh;
        switch (t)
        {
            case Var: regel = $"Ableitung von {x}"; roh = Term.Eins; break;
            case Summe sm:
                regel = "Summenregel: jedes Glied einzeln ableiten";
                roh = new Summe([.. sm.Glieder.Select(g => Abl(g, x, s, tiefe + 1))]); break;
            case Produkt p:
                {
                    var konst = p.Faktoren.Where(g => !g.Enthaelt(x)).ToList(); var var_ = p.Faktoren.Where(g => g.Enthaelt(x)).ToList();
                    if (konst.Count > 0)
                    {
                        var c = konst.Count == 1 ? konst[0] : new Produkt(konst); var u = var_.Count == 1 ? var_[0] : new Produkt(var_);
                        regel = $"Faktorregel: der Faktor {A(V(c))} bleibt stehen";
                        roh = Term.Mal(c, Abl(u, x, s, tiefe + 1)); break;
                    }
                    var nenner = var_.Where(g => g is Potenz { Exponent: Zahl { Wert: var e } } && e == Bruch.MinusEins).ToList();
                    if (nenner.Count == 1 && var_.Count >= 2)
                    {
                        var v = ((Potenz)nenner[0]).Basis; var zaehler = var_.Where(g => !ReferenceEquals(g, nenner[0])).ToList(); var u = zaehler.Count == 1 ? zaehler[0] : new Produkt(zaehler);
                        regel = $"Quotientenregel: (u/v)' = (u'·v − u·v')/v² mit u = {A(V(u))}, v = {A(V(v))}";
                        var du = Abl(u, x, s, tiefe + 1); var dv = Abl(v, x, s, tiefe + 1);
                        roh = Term.Durch(Term.Plus(Term.Mal(du, v), Term.Minus(Term.Mal(u, dv))), Term.Hoch(v, 2)); break;
                    }
                    {
                        var u = var_[0]; var v = var_.Count == 2 ? var_[1] : new Produkt([.. var_.Skip(1)]);
                        regel = $"Produktregel: (u·v)' = u'·v + u·v' mit u = {A(V(u))}, v = {A(V(v))}";
                        var du = Abl(u, x, s, tiefe + 1); var dv = Abl(v, x, s, tiefe + 1);
                        roh = Term.Plus(Term.Mal(du, v), Term.Mal(u, dv)); break;
                    }
                }
            case Potenz w when !w.Exponent.Enthaelt(x):
                {
                    var n = w.Exponent; var innen = w.Basis;
                    var nMinus1 = V(Term.Plus(n, Term.MinusEins));
                    var wurzel = n is Zahl { Wert: var nw } && nw == new Bruch(1, 2);
                    if (innen is Var)
                    {
                        regel = wurzel ? $"Wurzelregel: (√{x})' = 1/(2√{x})" : $"Potenzregel: ({x}ⁿ)' = n·{x}ⁿ⁻¹ mit n = {A(n)}";
                        roh = Term.Mal(n, Term.Hoch(innen, nMinus1));
                    }
                    else
                    {
                        regel = $"Kettenregel: äußere Funktion u{(wurzel ? "^(1/2) = √u" : "^" + A(n))}, innere Funktion u = {A(V(innen))} – äußere Ableitung mal innere Ableitung";
                        roh = Term.Mal(n, Term.Hoch(innen, nMinus1), Abl(innen, x, s, tiefe + 1));
                    }
                    break;
                }
            case Potenz w when !w.Basis.Enthaelt(x):
                {
                    var innen = w.Exponent;
                    var e = w.Basis is Konst { Name: "e" };
                    var lnA = e ? (Term)Term.Eins : new Fkt("ln", w.Basis);
                    regel = (e ? "e-Funktion: (eᵘ)' = eᵘ·u'" : $"Exponentialfunktion: (aᵘ)' = aᵘ·ln(a)·u' mit a = {A(w.Basis)}") + (innen is Var ? "" : $" (Kettenregel, innere Funktion u = {A(V(innen))})");
                    roh = Term.Mal(w, lnA, Abl(innen, x, s, tiefe + 1));
                    break;
                }
            case Potenz w:
                {
                    // u^v mit u und v abhängig: u^v = e^(v·ln u)
                    regel = $"Umschreiben: {A(w)} = e^({A(w.Exponent)}·ln({A(w.Basis)})), dann Kettenregel und Produktregel";
                    var inner = Term.Mal(w.Exponent, new Fkt("ln", w.Basis));
                    roh = Term.Mal(w, Abl(inner, x, s, tiefe + 1));
                    break;
                }
            case Fkt f:
                {
                    var u = f.Argument; var du = u is Var ? Term.Eins : Abl(u, x, s, tiefe + 1);
                    var kette = u is Var ? "" : $" (Kettenregel, innere Funktion u = {A(V(u))}, u' = {A(V(du))})";
                    (regel, var aussen) = f.Name switch
                    {
                        "sin" => ("(sin u)' = cos(u)·u'", (Term)new Fkt("cos", u)),
                        "cos" => ("(cos u)' = −sin(u)·u'", Term.Minus(new Fkt("sin", u))),
                        "tan" => ("(tan u)' = 1/cos²(u)·u'", Term.Hoch(new Fkt("cos", u), -2)),
                        "ln" => ("(ln u)' = u'/u", Term.Hoch(u, -1)),
                        "log" => ("(lg u)' = u'/(u·ln 10)", Term.Durch(Term.Eins, Term.Mal(u, new Fkt("ln", new Zahl(10))))),
                        "asin" => ("(arcsin u)' = u'/√(1 − u²)", Term.Hoch(Term.Plus(Term.Eins, Term.Minus(Term.Hoch(u, 2))), new Bruch(-1, 2))),
                        "acos" => ("(arccos u)' = −u'/√(1 − u²)", Term.Minus(Term.Hoch(Term.Plus(Term.Eins, Term.Minus(Term.Hoch(u, 2))), new Bruch(-1, 2)))),
                        "atan" => ("(arctan u)' = u'/(1 + u²)", Term.Hoch(Term.Plus(Term.Eins, Term.Hoch(u, 2)), -1)),
                        "abs" => ("(|u|)' = u/|u|·u' (für u ≠ 0)", Term.Durch(u, new Fkt("abs", u))),
                        _ => throw new ArgumentException("Unbekannte Funktion."),
                    };
                    regel += kette;
                    roh = Term.Mal(aussen, du);
                    break;
                }
            default: throw new ArgumentException("Diesen Ausdruck kann der Mathe-Helfer nicht ableiten.");
        }
        if (merken)
        {
            var kurz = V(roh);
            if (Polynom.Aus(kurz) is { } pk) kurz = pk.AlsTerm();
            s[idx] = new Schritt(regel, $"({A(t)})' = {A(kurz)}");
        }
        return roh;
    }

    // =====================================================================================
    // Integrieren
    // =====================================================================================

    sealed class NichtIntegrierbar(string m) : Exception(m);

    static Loesung Integrieren(string eingabe, string? variable, string? von, string? bis)
    {
        var (f, x) = Funktion(eingabe, variable);
        var schritte = new List<Schritt>();
        Term F;
        try { F = V(Int(V(f), x, schritte, 0)); }
        catch (NichtIntegrierbar e) { throw new ArgumentException(e.Message); }
        if (!HatKlammerProdukt(F) && Polynom.Aus(F) is { } pf) F = pf.AlsTerm(); // (2x + 1)⁴/8 bleibt so stehen, x³/3 + x²/2 wird geordnet
        var fa = f is Summe ? "(" + A(f) + ")" : A(f);
        schritte.Add(new Schritt("Stammfunktion (C ist eine beliebige Konstante)", $"∫ {fa} d{x} = {A(F)} + C"));
        var proben = new List<Probe> { ProbeStammfunktion(f, F, x) };
        var ergebnis = $"∫ {fa} d{x} = {A(F)} + C";
        var latex = $"\\int {TermText.Latex(f)}\\,d{x} = {TermText.Latex(F)} + C";
        var aufgabe = $"Integriere: ∫ {fa} d{x}";
        if (von is { Length: > 0 } && bis is { Length: > 0 })
            (aufgabe, ergebnis, latex) = BestimmtesIntegral(f, F, x, fa, von, bis, schritte, proben);
        return new Loesung(MatheArt.Integrieren, eingabe, aufgabe, schritte, ergebnis, latex, proben,
            f.Variablen().All(v => v == x) ? TermText.Eingabe(f) + ";" + TermText.Eingabe(F) : "", x);
    }

    static Probe ProbeStammfunktion(Term f, Term F, string x)
    {
        // Gegenrechnung: F'(x) per Differenzenquotient muss f(x) ergeben
        var punkte = new[] { 0.7, 1.3, 2.1, -0.6, 3.7, 0.35 };
        var ok = true; var n = 0; var bsp = "";
        foreach (var p in punkte)
        {
            double W(double v) { try { return Auswertung.Wert(F, new Dictionary<string, double> { [x] = v }); } catch (ArgumentException) { return double.NaN; } }
            const double h = 1e-5;
            var num = (W(p + h) - W(p - h)) / (2 * h);
            var fw = Auswertung.Wert(f, new Dictionary<string, double> { [x] = p });
            if (!double.IsFinite(num) || !double.IsFinite(fw)) continue;
            n++;
            var gut = Math.Abs(num - fw) <= 1e-4 * Math.Max(1, Math.Abs(fw));
            ok &= gut;
            if (bsp.Length == 0) bsp = $"bei {x} = {Zahlwort(p)}: F' ≈ {Zahlwort(Math.Round(num, 6))}, f = {Zahlwort(Math.Round(fw, 6))}";
        }
        if (n < 2) return new Probe(false, "✗ Die Probe ließ sich an keiner Stelle ausrechnen.");
        return new Probe(ok, $"{(ok ? "✓" : "✗")} Probe durch Ableiten: F' stimmt an {n} Stellen mit f überein – {bsp}.");
    }

    static Probe ProbeSimpson(Term f, string x, double a, double b, double wert)
    {
        const int n = 2000; var h = (b - a) / n; var sum = 0.0;
        for (var i = 0; i <= n; i++)
        {
            var w = Auswertung.Wert(f, new Dictionary<string, double> { [x] = a + i * h });
            if (!double.IsFinite(w)) return new Probe(false, "✗ Die Funktion ist zwischen den Grenzen nicht überall definiert – das bestimmte Integral ist fraglich.");
            sum += w * (i == 0 || i == n ? 1 : i % 2 == 1 ? 4 : 2);
        }
        var simpson = sum * h / 3;
        var ok = Math.Abs(simpson - wert) <= 1e-6 * Math.Max(1, Math.Abs(wert));
        return new Probe(ok, $"{(ok ? "✓" : "✗")} Gegenrechnung mit der Simpson-Regel (2000 Streifen): {Zahlwort(Math.Round(simpson, 8))}.");
    }

    /// <summary>Linearer Term a·x + b? Dann (a, b).</summary>
    static (Bruch A, Bruch B)? Linear(Term t, string x)
    {
        if (Polynom.Aus(t) is not { } p || p.Koeffizienten(x) is not { } k || k.Length > 2) return null;
        return k.Length == 2 && !k[1].IstNull ? (k[1], k[0]) : null;
    }

    static Term Int(Term t, string x, List<Schritt> s, int tiefe)
    {
        Rechenzeit.Pruefe();
        if (tiefe > 12) throw new NichtIntegrierbar("Das Integral wird zu verschachtelt.");
        void Merke(string regel, Term ergebnis) { if (s.Count < 18) s.Add(new Schritt(regel, $"∫ {A(t)} d{x} = {A(V(ergebnis))}")); }
        if (!t.Enthaelt(x)) { var r0 = Term.Mal(t, new Var(x)); Merke("Konstante: ∫ c dx = c·x", r0); return r0; }
        switch (t)
        {
            case Summe sm:
                {
                    if (s.Count < 18) s.Add(new Schritt("Summenregel: jedes Glied einzeln integrieren", $"∫ ({A(t)}) d{x} = " + string.Join(" + ", sm.Glieder.Select(g => $"∫ {A(g)} d{x}")).Replace("+ ∫ −", "− ∫ ")));
                    return new Summe([.. sm.Glieder.Select(g => Int(g, x, s, tiefe + 1))]);
                }
            case Produkt p:
                {
                    var konst = p.Faktoren.Where(g => !g.Enthaelt(x)).ToList(); var var_ = p.Faktoren.Where(g => g.Enthaelt(x)).ToList();
                    if (konst.Count > 0)
                    {
                        var c = V(konst.Count == 1 ? konst[0] : new Produkt(konst)); var u = var_.Count == 1 ? var_[0] : new Produkt(var_);
                        if (s.Count < 18) s.Add(new Schritt($"Faktorregel: der Faktor {A(c)} kommt vor das Integral", $"∫ {A(t)} d{x} = {A(c)} · ∫ {A(u)} d{x}"));
                        return Term.Mal(c, Int(u, x, s, tiefe + 1));
                    }
                    if (Polynom.Aus(t) is { } poly)
                    {
                        var ausm = poly.AlsTerm();
                        if (s.Count < 18) s.Add(new Schritt("Erst ausmultiplizieren", $"{A(t)} = {A(ausm)}"));
                        return Int(ausm, x, s, tiefe + 1);
                    }
                    // Partielle Integration: Polynom · (e^(ax+b) | sin(ax+b) | cos(ax+b))
                    var polyF = var_.Where(g => Polynom.Aus(g) is not null).ToList(); var rest = var_.Where(g => Polynom.Aus(g) is null).ToList();
                    if (polyF.Count >= 1 && rest.Count == 1 && Partiell(rest[0], x))
                    {
                        var u = V(polyF.Count == 1 ? polyF[0] : new Produkt(polyF)); var dv = rest[0];
                        var vInt = V(IntOhne(dv, x));
                        var du = V(Polynom.Aus(Abl(u, x, [], 99)) is { } pd ? pd.AlsTerm() : Abl(u, x, [], 99));
                        if (s.Count < 18) s.Add(new Schritt($"Partielle Integration: ∫ u·v' dx = u·v − ∫ u'·v dx mit u = {A(u)}, v' = {A(dv)}, also u' = {A(du)}, v = {A(vInt)}",
                            $"∫ {A(t)} d{x} = {A(V(Term.Mal(u, vInt)))} − ∫ {A(V(Term.Mal(du, vInt)))} d{x}"));
                        return Term.Plus(Term.Mal(u, vInt), Term.Minus(Int(V(Term.Mal(du, vInt)), x, s, tiefe + 1)));
                    }
                    if (var_.Count == 1) return Int(var_[0], x, s, tiefe + 1);
                    throw new NichtIntegrierbar($"∫ {A(t)} d{x} kann der Mathe-Helfer nicht Schritt für Schritt lösen. Möglich sind Polynome, Potenzen und Wurzeln von linearen Termen, e-Funktion, sin, cos, 1/x und Polynom mal e-Funktion, sin oder cos (partielle Integration).");
                }
        }
        var r = IntOhne(t, x, s);
        return r;
    }

    static bool Partiell(Term g, string x) => g switch
    {
        Potenz w when w.Basis is Konst { Name: "e" } => Linear(w.Exponent, x) is not null,
        Fkt f when f.Name is "sin" or "cos" => Linear(f.Argument, x) is not null,
        _ => false,
    };

    /// <summary>Grundintegrale (Potenzregel, lineare Substitution, e-Funktion, sin, cos, ln).</summary>
    static Term IntOhne(Term t, string x, List<Schritt>? s = null)
    {
        void Merke(string regel, Term ergebnis) { if (s is not null && s.Count < 18) s.Add(new Schritt(regel, $"∫ {A(t)} d{x} = {A(V(ergebnis))}")); }
        Term r;
        switch (t)
        {
            case Var: r = Term.Mal(new Zahl(new Bruch(1, 2)), Term.Hoch(t, 2)); Merke($"Potenzregel: ∫ {x}ⁿ d{x} = {x}ⁿ⁺¹/(n + 1) mit n = 1", r); return r;
            case Potenz w when !w.Exponent.Enthaelt(x) && Linear(w.Basis, x) is { } lin:
                {
                    var n = w.Exponent;
                    var minusEins = V(Term.Plus(n, Term.Eins)) is Zahl { Wert.IstNull: true };
                    var lineareSub = !(lin.A == Bruch.Eins && lin.B.IstNull);
                    var zusatz = lineareSub ? $" – lineare Substitution: durch die innere Ableitung {lin.A} teilen" : "";
                    if (minusEins)
                    {
                        r = Term.Mal(new Zahl(lin.A.Kehrwert), new Fkt("ln", new Fkt("abs", w.Basis)));
                        Merke($"∫ 1/u du = ln|u|" + zusatz, r); return r;
                    }
                    var np1 = V(Term.Plus(n, Term.Eins));
                    r = Term.Mal(Term.Durch(Term.Eins, Term.Mal(new Zahl(lin.A), np1)), Term.Hoch(w.Basis, np1));
                    Merke($"Potenzregel: ∫ uⁿ du = uⁿ⁺¹/(n + 1) mit n = {A(n)}" + zusatz, r); return r;
                }
            case Potenz w when w.Basis is Konst { Name: "e" } && Linear(w.Exponent, x) is { } lin:
                r = Term.Mal(new Zahl(lin.A.Kehrwert), w); Merke("e-Funktion: ∫ eᵘ du = eᵘ" + (lin.A == Bruch.Eins ? "" : $" – durch die innere Ableitung {lin.A} teilen"), r); return r;
            case Potenz w when w.Basis is Zahl bz && bz.Wert.Vorzeichen > 0 && bz.Wert != Bruch.Eins && Linear(w.Exponent, x) is { } lin:
                r = Term.Durch(w, Term.Mal(new Zahl(lin.A), new Fkt("ln", w.Basis))); Merke($"Exponentialfunktion: ∫ aᵘ du = aᵘ/ln(a)", r); return r;
            case Fkt f when f.Name == "sin" && Linear(f.Argument, x) is { } lin:
                r = Term.Mal(new Zahl(-lin.A.Kehrwert), new Fkt("cos", f.Argument)); Merke("∫ sin(u) du = −cos(u)" + (lin.A == Bruch.Eins ? "" : $" – durch {lin.A} teilen"), r); return r;
            case Fkt f when f.Name == "cos" && Linear(f.Argument, x) is { } lin:
                r = Term.Mal(new Zahl(lin.A.Kehrwert), new Fkt("sin", f.Argument)); Merke("∫ cos(u) du = sin(u)" + (lin.A == Bruch.Eins ? "" : $" – durch {lin.A} teilen"), r); return r;
            case Fkt f when f.Name == "ln" && f.Argument is Var:
                r = Term.Plus(Term.Mal(f.Argument, f), Term.Minus(f.Argument)); Merke($"Bekannte Stammfunktion (partielle Integration mit 1·ln {x}): ∫ ln({x}) d{x} = {x}·ln({x}) − {x}", r); return r;
        }
        throw new NichtIntegrierbar($"∫ {A(t)} d{x} kann der Mathe-Helfer nicht Schritt für Schritt lösen. Möglich sind Polynome, Potenzen und Wurzeln von linearen Termen, e-Funktion, sin, cos, 1/x und Polynom mal e-Funktion, sin oder cos (partielle Integration).");
    }
}
