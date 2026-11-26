using System.Numerics;

namespace Trendo.Core.Wissen;

/// <summary>T-0781 Nachbesserung (Gegenprüfung): Gleichungen vollständig und ehrlich lösen.
/// <list type="bullet">
/// <item>Polynom- und Bruchgleichungen (Variable im Nenner): Definitionsmenge, Hauptnenner, Ausschluss; Nullstellen über die Cauchy-Schranke und
/// Vorzeichenwechsel zwischen den Nullstellen der Ableitung sicher eingegrenzt (exakt mit Brüchen); unabhängige Probe: Anzahl der reellen Nullstellen mit der Sturm-Kette.</item>
/// <item>Umkehrgleichungen mit allen Fällen: Betrag und gerade Potenzen (±), sin/cos/tan mit allgemeiner Lösung (k ∈ ℤ), Definitionsbereich von ln, √, eˣ.
/// Unabhängige Probe: Einsetzen und eine eigene Nullstellensuche im Bereich (Vorzeichenwechsel und Berührpunkte).</item>
/// <item>Bestimmte Integrale über Polstellen: Lücken erkennen, uneigentliches Integral per Grenzwert, sonst „existiert nicht“.</item>
/// </list>
/// Kann der Kern eine Aufgabe nicht sicher vollständig lösen, ist die Probe gelb (unsicher) – nie eine falsche Antwort mit grüner Probe.</summary>
public static partial class MatheKern
{
    /// <summary>Höchster Grad einer Polynomgleichung (darüber wird ehrlich abgelehnt).</summary>
    public const int MaxGrad = 64;

    // =====================================================================================
    // Hilfen für Polynome als Koeffizientenlisten (aufsteigend)
    // =====================================================================================

    static Bruch[] Kuerze(Bruch[] k)
    {
        var n = k.Length; while (n > 1 && k[n - 1].IstNull) n--;
        return n == k.Length ? k : k[..n];
    }

    static int GradVon(Bruch[] k) { var g = Kuerze(k).Length - 1; return g == 0 && k[0].IstNull ? -1 : g; }

    static Bruch[] Abl(Bruch[] k) => k.Length <= 1 ? [Bruch.Null] : [.. Enumerable.Range(1, k.Length - 1).Select(i => k[i] * new Bruch(i))];

    static Bruch Wert(Bruch[] k, Bruch x)
    {
        var acc = Bruch.Null;
        for (var i = k.Length - 1; i >= 0; i--) acc = acc * x + k[i];
        return acc;
    }

    /// <summary>Polynomdivision a = q·b + r (exakt).</summary>
    static (Bruch[] Q, Bruch[] R) Teile(Bruch[] a, Bruch[] b)
    {
        a = Kuerze(a); b = Kuerze(b);
        var r = a.ToArray(); var db = b.Length - 1;
        if (r.Length - 1 < db) return ([Bruch.Null], r);
        var q = new Bruch[r.Length - db]; for (var i = 0; i < q.Length; i++) q[i] = Bruch.Null;
        for (var i = r.Length - 1; i >= db; i--)
        {
            Rechenzeit.Pruefe();
            if (r[i].IstNull) continue;
            var f = r[i] / b[db]; q[i - db] = f;
            for (var j = 0; j <= db; j++) r[i - db + j] -= f * b[j];
        }
        return (q, db == 0 ? [Bruch.Null] : Kuerze(r[..db]));
    }

    static Bruch[] Normiert(Bruch[] k) { k = Kuerze(k); var l = k[^1]; return l.IstNull ? k : [.. k.Select(c => c / l)]; }

    static Bruch[] Ggt(Bruch[] a, Bruch[] b)
    {
        a = Kuerze(a); b = Kuerze(b);
        while (!(b.Length == 1 && b[0].IstNull))
        {
            var (_, r) = Teile(a, b);
            a = b; b = r.Length == 0 ? [Bruch.Null] : Normiert(r);
        }
        return Normiert(a);
    }

    /// <summary>Quadratfreier Anteil (jede Nullstelle einfach): p / ggT(p, p').</summary>
    static Bruch[] Quadratfrei(Bruch[] k)
    {
        k = Kuerze(k);
        if (k.Length <= 2) return k;
        var g = Ggt(k, Abl(k));
        return g.Length <= 1 ? k : Teile(k, g).Q;
    }

    /// <summary>Anzahl verschiedener reeller Nullstellen mit der Sturm-Kette (exakt, auf der ganzen Zahlengeraden) – unabhängig von der Suche.</summary>
    public static int SturmAnzahl(Bruch[] k)
    {
        var p = Kuerze(k);
        if (p.Length <= 1) return 0;
        var kette = new List<Bruch[]> { p, Abl(p) };
        while (true)
        {
            Rechenzeit.Pruefe();
            var (_, r) = Teile(kette[^2], kette[^1]);
            r = Kuerze(r);
            if (r.Length == 1 && r[0].IstNull) break;
            // −Rest, mit dem Betrag des Leitkoeffizienten normiert (Vorzeichen bleibt)
            var l = r[^1].Betrag;
            kette.Add([.. r.Select(c => -c / l)]);
            if (kette.Count > 200) break;
        }
        int Wechsel(bool plusUnendlich)
        {
            var vz = kette.Select(q => { var g = q.Length - 1; var s = q[^1].Vorzeichen; return plusUnendlich || g % 2 == 0 ? s : -s; }).Where(s => s != 0).ToList();
            return vz.Zip(vz.Skip(1)).Count(t => t.First != t.Second);
        }
        return Wechsel(false) - Wechsel(true);
    }

    /// <summary>Double exakt als Bruch.</summary>
    static Bruch AusDouble(double d)
    {
        if (d == 0) return Bruch.Null;
        var bits = BitConverter.DoubleToInt64Bits(d);
        var neg = bits < 0; var exp = (int)((bits >> 52) & 0x7FF); var mant = bits & 0xFFFFFFFFFFFFFL;
        if (exp == 0) exp++; else mant |= 1L << 52;
        exp -= 1075;
        BigInteger z = mant; if (neg) z = -z;
        return exp >= 0 ? new Bruch(z * BigInteger.Pow(2, exp), 1) : new Bruch(z, BigInteger.Pow(2, -exp));
    }

    /// <summary>Alle verschiedenen reellen Nullstellen (Näherung, sortiert). Sicher eingegrenzt: Cauchy-Schranke, Nullstellen der Ableitung als Trenner,
    /// Vorzeichen exakt mit Brüchen, dann Intervallhalbierung. Mehrfache Nullstellen über den quadratfreien Anteil.</summary>
    public static List<double> ReelleNullstellen(Bruch[] k)
    {
        var q = Quadratfrei(k);
        return Isoliere(q, 0);
    }

    static List<double> Isoliere(Bruch[] q, int tiefe)
    {
        q = Kuerze(q);
        var g = q.Length - 1;
        if (g <= 0) return [];
        if (g == 1) return [(-q[0] / q[1]).AlsDouble()];
        if (tiefe > MaxGrad) throw new OverflowException("Zu tiefe Rechnung.");
        var schranke = 1 + Enumerable.Range(0, g).Max(i => (q[i] / q[g]).Betrag.AlsDouble());
        var trenner = Isoliere(Abl(q), tiefe + 1).Where(c => c > -schranke && c < schranke).Distinct().Order().ToList();
        var punkte = new List<double> { -schranke }; punkte.AddRange(trenner); punkte.Add(schranke);
        var roots = new List<double>();
        for (var i = 0; i + 1 < punkte.Count; i++)
        {
            Rechenzeit.Pruefe();
            Bruch l = AusDouble(punkte[i]), r = AusDouble(punkte[i + 1]);
            int sl = Wert(q, l).Vorzeichen, sr = Wert(q, r).Vorzeichen;
            if (sl == 0) { roots.Add(punkte[i]); continue; }
            if (sr == 0 || sl * sr > 0) continue;
            for (var it = 0; it < 200; it++)
            {
                var m = (l + r) / new Bruch(2);
                var sm = Wert(q, m).Vorzeichen;
                if (sm == 0) { l = r = m; break; }
                if (sm == sl) l = m; else r = m;
                if ((r - l).AlsDouble() <= 1e-15 * Math.Max(1, Math.Abs(l.AlsDouble()))) break;
                Rechenzeit.Pruefe();
            }
            roots.Add(((l + r) / new Bruch(2)).AlsDouble());
        }
        if (Wert(q, AusDouble(schranke)).IstNull) roots.Add(schranke);
        return [.. roots.Distinct().Order()];
    }

    /// <summary>Rationale Nullstelle p/q (p teilt a₀, q teilt aₙ) oder null; Teiler bis zur Wurzel gesucht, mit Zeitgrenze.</summary>
    static Bruch? RationaleNullstelle(Bruch[] k)
    {
        if (k[0].IstNull) return Bruch.Null;
        var nenner = k.Select(c => c.N).Aggregate(Bruch.Kgv);
        var ganz = k.Select(c => (c * new Bruch(nenner, 1)).Z).ToArray();
        var p0 = BigInteger.Abs(ganz[0]); var pn = BigInteger.Abs(ganz[^1]);
        if (p0 > 1_000_000_000_000 || pn > 1_000_000_000_000) return null; // ehrlich: dann sucht die Nullstellen-Eingrenzung weiter
        var tp = Teiler(p0); var tq = Teiler(pn);
        if ((long)tp.Count * tq.Count > 200_000) return null;
        foreach (var p in tp)
            foreach (var q in tq)
                foreach (var s in new[] { 1, -1 })
                {
                    Rechenzeit.Pruefe();
                    var r = new Bruch(s * p, q);
                    if (Polynom.Horner(k, r).Restwert.IstNull) return r;
                }
        return null;
    }

    static List<BigInteger> Teiler(BigInteger n)
    {
        var klein = new List<BigInteger>(); var gross = new List<BigInteger>();
        for (BigInteger d = 1; d * d <= n; d++)
        {
            if (d % 4096 == 0) Rechenzeit.Pruefe();
            if (n % d != 0) continue;
            klein.Add(d); if (d * d != n) gross.Add(n / d);
        }
        gross.Reverse(); klein.AddRange(gross);
        return klein;
    }

    // =====================================================================================
    // Gleichung: Einstieg
    // =====================================================================================

    static Loesung Gleichung(string eingabe, string? variable)
    {
        var teile = eingabe.Split('=');
        if (teile.Length != 2) throw new FormatException(teile.Length < 2 ? "In einer Gleichung steht ein „=“." : "Bitte nur ein „=“ – mehrere Gleichungen mit „;“ trennen (Gleichungssystem).");
        var links = TermLeser.Lies(teile[0]); var rechts = TermLeser.Lies(teile[1]);
        var alle = links.Variablen().Union(rechts.Variablen()).ToList();
        var x = variable is { Length: > 0 } v0 ? v0 : alle.Count == 1 ? alle[0] : alle.Contains("x") ? "x" : alle.Count == 0 ? throw new ArgumentException("In der Gleichung kommt keine Variable vor.") : alle[0];
        if (alle.Any(v => v != x)) throw new ArgumentException($"In der Gleichung kommen mehrere Variablen vor ({string.Join(", ", alle)}) – für mehrere Gleichungen nimm „Gleichungssystem“.");
        var aufgabe = $"Löse nach {x}: {A(links)} = {A(rechts)}";
        var differenz = Term.Plus(links, Term.Minus(rechts));
        var poly = Polynom.Aus(differenz);
        if (poly is not null) return GleichungPolynom(eingabe, aufgabe, links, rechts, x, poly, null);
        if (Rational(differenz) is { } rat && !rat.N.IstKonstant(out _)) return GleichungPolynom(eingabe, aufgabe, links, rechts, x, rat.Z, rat.N);
        return GleichungUmformen(eingabe, aufgabe, links, rechts, x);
    }

    /// <summary>Term als Bruch zweier Polynome (Zähler, Nenner) oder null (Funktionen, Wurzeln).</summary>
    static (Polynom Z, Polynom N)? Rational(Term t)
    {
        Rechenzeit.Pruefe();
        switch (t)
        {
            case Zahl or Var: return Polynom.Aus(t) is { } p ? (p, Polynom.Konstante(Bruch.Eins)) : null;
            case Summe s:
                {
                    (Polynom Z, Polynom N) acc = (Polynom.Konstante(Bruch.Null), Polynom.Konstante(Bruch.Eins));
                    foreach (var g in s.Glieder)
                    {
                        if (Rational(g) is not { } r) return null;
                        acc = acc.N.Gleich(r.N) ? (acc.Z + r.Z, acc.N) : (acc.Z * r.N + r.Z * acc.N, acc.N * r.N);
                    }
                    return acc;
                }
            case Produkt pr:
                {
                    (Polynom Z, Polynom N) acc = (Polynom.Konstante(Bruch.Eins), Polynom.Konstante(Bruch.Eins));
                    foreach (var g in pr.Faktoren) { if (Rational(g) is not { } r) return null; acc = (acc.Z * r.Z, acc.N * r.N); }
                    return acc;
                }
            case Potenz w when w.Exponent is Zahl e && e.Wert.IstGanz && BigInteger.Abs(e.Wert.Z) <= MaxGrad:
                {
                    if (Rational(w.Basis) is not { } r) return null;
                    var n = (int)e.Wert.Z;
                    return n >= 0 ? (r.Z.Hoch(n), r.N.Hoch(n)) : (r.N.Hoch(-n), r.Z.Hoch(-n));
                }
            default: return Polynom.Aus(t) is { } q ? (q, Polynom.Konstante(Bruch.Eins)) : null;
        }
    }

    // =====================================================================================
    // Polynom- und Bruchgleichungen
    // =====================================================================================

    sealed record Wurzel1(string Text, Bruch? Exakt, WurzelZahl? Wurzel, double Naeherung);

    static Loesung GleichungPolynom(string eingabe, string aufgabe, Term links, Term rechts, string x, Polynom zaehler, Polynom? nenner)
    {
        var schritte = new List<Schritt>();
        var k = zaehler.Koeffizienten(x) ?? throw new ArgumentException("Unerwartete Variable.");
        k = Kuerze(k);
        var grad = GradVon(k);
        if (grad > MaxGrad) throw new ArgumentException($"Die Gleichung hat Grad {grad} – der Mathe-Helfer löst Polynomgleichungen bis Grad {MaxGrad}. Bei höherem Grad könnte er die Vollständigkeit nicht sicher prüfen.");
        var ausgeschlossen = new List<Wurzel1>();
        Bruch[]? nk = null;
        if (nenner is not null)
        {
            nk = Kuerze(nenner.Koeffizienten(x) ?? throw new ArgumentException("Unerwartete Variable."));
            if (GradVon(nk) > MaxGrad) throw new ArgumentException($"Der Nenner hat einen zu hohen Grad (über {MaxGrad}).");
            var nl = new List<(string Text, Bruch? Exakt, WurzelZahl? Wurzel, double Naeherung)>();
            Nullstellen(nk, x, [], nl);
            ausgeschlossen = [.. nl.Select(l => new Wurzel1(l.Text, l.Exakt, l.Wurzel, l.Naeherung)).GroupBy(l => l.Text).Select(g => g.First()).OrderBy(l => l.Naeherung)];
            schritte.Add(new Schritt("Definitionsmenge: kein Nenner darf 0 sein", ausgeschlossen.Count == 0 ? $"D = ℝ (der Nenner {A(nenner.AlsTerm())} wird nie 0)" : $"D = ℝ \\ {{{string.Join("; ", ausgeschlossen.Select(a => a.Text))}}}"));
            schritte.Add(new Schritt($"Beide Seiten mit dem Hauptnenner {A(nenner.AlsTerm())} malnehmen, alles auf eine Seite bringen", $"{A(links)} = {A(rechts)}  ⇔  {A(zaehler.AlsTerm())} = 0"));
        }
        else
        {
            var normal = zaehler.AlsTerm();
            if (!(rechts is Zahl { Wert.IstNull: true } && A(links) == A(normal)))
                schritte.Add(new Schritt("Alles auf eine Seite bringen, ausmultiplizieren und zusammenfassen", $"{A(links)} = {A(rechts)}  ⇔  {A(normal)} = 0"));
        }
        string Ausser() => ausgeschlossen.Count == 0 ? "" : " außer " + string.Join(", ", ausgeschlossen.Select(a => $"{x} = {a.Text}"));
        if (grad <= 0)
        {
            var allg = grad < 0;
            schritte.Add(new Schritt(allg ? "Die Gleichung stimmt immer (0 = 0) – jede Zahl der Definitionsmenge ist eine Lösung." : $"Die Gleichung ist ein Widerspruch ({k[0]} = 0) – es gibt keine Lösung.", allg ? "0 = 0" : $"{k[0]} = 0"));
            var erg = allg ? (ausgeschlossen.Count == 0 ? "L = ℝ (jede Zahl)" : $"L = ℝ \\ {{{string.Join("; ", ausgeschlossen.Select(a => a.Text))}}} (jede Zahl{Ausser()})") : "L = { } (keine Lösung)";
            var probe0 = new List<Probe>();
            if (allg)
            {
                // Probe an drei Stellen, die nicht ausgeschlossen sind (exakt)
                var stellen = new[] { 2, 3, 7, -5, 11 }.Select(v => new Bruch(v)).Where(v => !ausgeschlossen.Any(a => a.Exakt == v)).Take(3).ToList();
                var ok = stellen.All(v => Auswertung.Exakt(links, new Dictionary<string, Bruch> { [x] = v }) is { } lw && lw == Auswertung.Exakt(rechts, new Dictionary<string, Bruch> { [x] = v }));
                probe0.Add(new Probe(ok, $"{(ok ? "✓" : "✗")} Einsetzen von {string.Join(", ", stellen.Select(v => $"{x} = {v}"))}: beide Seiten jeweils gleich (exakt)."));
            }
            else probe0.Add(new Probe(true, "✓ Ein Widerspruch (Zahl ≠ 0) kann von keinem Wert erfüllt werden."));
            return new Loesung(MatheArt.Gleichung, eingabe, aufgabe, schritte, erg, erg.Replace("ℝ", "\\mathbb{R}").Replace("{ }", "\\{\\}"), probe0, TermText.Eingabe(links) + ";" + TermText.Eingabe(rechts), x);
        }
        var lsg = new List<(string Text, Bruch? Exakt, WurzelZahl? Wurzel, double Naeherung)>();
        Nullstellen(k, x, schritte, lsg);
        var gefunden = lsg.GroupBy(l => l.Text).Select(g => g.First()).OrderBy(l => l.Naeherung).ToList();
        // Definitionsmenge: Nullstellen des Nenners ausschließen
        var eindeutig = new List<(string Text, Bruch? Exakt, WurzelZahl? Wurzel, double Naeherung)>();
        foreach (var l in gefunden)
        {
            var aus = nk is not null && (l.Exakt is { } e ? Wert(nk, e).IstNull : l.Wurzel is { } wz ? wz.Einsetzen(nk).IstNull : Math.Abs(Wert(nk, AusDouble(l.Naeherung)).AlsDouble()) < 1e-9);
            if (aus) schritte.Add(new Schritt($"{x} = {l.Text} gehört nicht zur Definitionsmenge (der Nenner wäre 0) und fällt weg", $"{x} ≠ {l.Text}"));
            else eindeutig.Add(l);
        }
        var proben = new List<Probe>();
        foreach (var l in eindeutig)
        {
            if (l.Exakt is { } e)
            {
                var w = new Dictionary<string, Bruch> { [x] = e };
                var lw = Auswertung.Exakt(links, w); var rw = Auswertung.Exakt(rechts, w);
                var ok = lw is not null && lw == rw;
                proben.Add(new Probe(ok, $"{(ok ? "✓" : "✗")} {x} = {e} einsetzen: links {lw?.ToString() ?? "—"}, rechts {rw?.ToString() ?? "—"}{(ok ? " – stimmt (exakt)." : " – stimmt NICHT.")}"));
            }
            else if (l.Wurzel is { } wz)
            {
                var null0 = wz.Einsetzen(k);
                double lw = Auswertung.Wert(links, new Dictionary<string, double> { [x] = wz.AlsDouble() }), rw = Auswertung.Wert(rechts, new Dictionary<string, double> { [x] = wz.AlsDouble() });
                var zahl = double.IsFinite(lw) && double.IsFinite(rw);
                var ok = null0.IstNull && (!zahl || Math.Abs(lw - rw) <= 1e-7 * Math.Max(1, Math.Abs(lw)));
                proben.Add(new Probe(ok, $"{(ok ? "✓" : "✗")} {x} = {wz} einsetzen: exakt mit Wurzeln gerechnet ergibt der Zähler {(null0.IstNull ? "0" : null0.ToString())}" + (zahl ? $"; als Zahl links {Zahlwort(lw)}, rechts {Zahlwort(rw)}." : " (für Gleitkomma zu groß – nur exakt geprüft).")));
            }
            else
            {
                double lw = Auswertung.Wert(links, new Dictionary<string, double> { [x] = l.Naeherung }), rw = Auswertung.Wert(rechts, new Dictionary<string, double> { [x] = l.Naeherung });
                var ok = Math.Abs(lw - rw) <= 1e-6 * Math.Max(1, Math.Max(Math.Abs(lw), Math.Abs(rw)));
                proben.Add(new Probe(ok, $"{(ok ? "✓" : "✗")} {x} ≈ {l.Text.TrimStart('≈', ' ')} einsetzen (Näherung): links {Zahlwort(lw)}, rechts {Zahlwort(rw)}."));
            }
        }
        // Unabhängige Probe der Vollständigkeit: Zahl der verschiedenen reellen Nullstellen mit der Sturm-Kette (nicht mit der Suche)
        if (grad > 30) proben.Add(Probe.Gelb($"Grad {grad}: die Sturm-Kette wird erst bis Grad 30 gerechnet – ob alle Lösungen gefunden sind, ist unsicher."));
        else
        {
            var n = SturmAnzahl(k);
            if (n == gefunden.Count) proben.Add(new Probe(true, $"✓ Unabhängige Zählung mit der Sturm-Kette: genau {n} verschiedene reelle Nullstelle(n){(nenner is null ? "" : " des Zählers")} – alle gefunden."));
            else proben.Add(Probe.Gelb($"Die Sturm-Kette zählt {n} verschiedene reelle Nullstelle(n), gefunden wurden {gefunden.Count} – das Ergebnis ist unsicher."));
        }
        var unsicher = proben.Any(p => p.Unsicher);
        var ergebnis = eindeutig.Count == 0 ? "L = { } (keine reelle Lösung)"
            : eindeutig.Count == 1 ? $"{x} = {eindeutig[0].Text}" : string.Join(";  ", eindeutig.Select((l, i) => $"{x}{Index(i + 1)} = {l.Text}"));
        if (unsicher) ergebnis += " (unsicher – siehe Probe)";
        var latex = eindeutig.Count == 0 ? "\\mathbb{L} = \\{\\}" : string.Join(",\\quad ", eindeutig.Select((l, i) => $"{x}_{{{i + 1}}} = " + (l.Exakt is { } e ? TermText.Latex(new Zahl(e)) : l.Wurzel is { } wz ? TermText.Latex(wz.AlsTerm()) : l.Text.Replace("≈", "\\approx").Replace(",", "{,}"))));
        return new Loesung(MatheArt.Gleichung, eingabe, aufgabe, schritte, ergebnis, latex, proben, TermText.Eingabe(links) + ";" + TermText.Eingabe(rechts), x);
    }

    /// <summary>Alle reellen Nullstellen eines Polynoms mit Lösungsweg: x ausklammern, lineare/quadratische Formel, rationale Nullstellen + Polynomdivision, Rest eingegrenzt.</summary>
    static void Nullstellen(Bruch[] k, string x, List<Schritt> schritte, List<(string Text, Bruch? Exakt, WurzelZahl? Wurzel, double Naeherung)> lsg)
    {
        k = Kuerze(k);
        var grad = k.Length - 1;
        if (grad <= 0) return;
        var m = 0; while (k[m].IstNull) m++;
        var arbeit = k[m..];
        if (m > 0)
        {
            var innen = Polynom.AusKoeffizienten(x, arbeit);
            if (arbeit.Length > 1)
                schritte.Add(new Schritt($"{x}{(m > 1 ? Hoch(m) : "")} ausklammern und den Satz vom Nullprodukt nutzen: ein Produkt ist 0, wenn ein Faktor 0 ist", $"{x}{(m > 1 ? Hoch(m) : "")}·({A(innen.AlsTerm())}) = 0  ⇒  {x} = 0 oder {A(innen.AlsTerm())} = 0"));
            else schritte.Add(new Schritt("Ein Produkt ist 0, wenn ein Faktor 0 ist", $"{x} = 0"));
            lsg.Add(("0", Bruch.Null, null, 0));
        }
        LoeseUnivariat(arbeit, x, schritte, lsg);
    }

    // =====================================================================================
    // Umkehrgleichungen mit allen Fällen
    // =====================================================================================

    /// <summary>Lösungskandidat x = Wert + k·(PeriodePi·π), k ∈ ℤ (ohne Periode: genau ein Wert).</summary>
    sealed record Kand(Term Wert, Bruch? PeriodePi);

    sealed class KeineLoesung(string grund) : Exception(grund);

    static Loesung GleichungUmformen(string eingabe, string aufgabe, Term links, Term rechts, string x)
    {
        var schritte = new List<Schritt>();
        Term seite = links, wert = rechts;
        if (!links.Enthaelt(x)) (seite, wert) = (rechts, links);
        if (wert.Enthaelt(x) || Vorkommen(seite, x) != 1)
            throw new ArgumentException("Diese Gleichung kann der Mathe-Helfer nicht Schritt für Schritt lösen: Die Variable kommt in Funktionen oder Wurzeln mehrfach vor. Lösbar sind Polynom- und Bruchgleichungen und Gleichungen, in denen die Variable nur einmal vorkommt.");
        var kands = Loese(seite, V(wert), x, schritte, "", 0);
        // Einsetzen (unabhängig vom Umformen): Scheinlösungen verwerfen
        var proben = new List<Probe>();
        var gute = new List<Kand>();
        double H(double v) { try { return Auswertung.Wert(links, new Dictionary<string, double> { [x] = v }) - Auswertung.Wert(rechts, new Dictionary<string, double> { [x] = v }); } catch (ArgumentException) { return double.NaN; } }
        double Massstab(double v) { try { return Math.Max(1, Math.Abs(Auswertung.Wert(rechts, new Dictionary<string, double> { [x] = v }))); } catch (ArgumentException) { return 1; } }
        foreach (var kd in kands)
        {
            var w = Auswertung.Wert(kd.Wert);
            var ks = kd.PeriodePi is null ? new[] { 0 } : [-1, 0, 1, 2];
            var werte = ks.Select(kk => w + kk * (kd.PeriodePi?.AlsDouble() ?? 0) * Math.PI).ToList();
            var ok = werte.All(v => double.IsFinite(v) && Math.Abs(H(v)) <= 1e-9 * Massstab(v));
            if (!ok)
            {
                schritte.Add(new Schritt("Scheinlösung verworfen: die Probe stimmt nicht", $"{x} = {A(kd.Wert)}"));
                continue;
            }
            gute.Add(kd);
            proben.Add(new Probe(true, kd.PeriodePi is null
                ? $"✓ {x} = {A(kd.Wert)} ≈ {Zahlwort(Math.Round(w, 8))} einsetzen: links {Zahlwort(Math.Round(H(w) + Auswertung.Wert(rechts, new Dictionary<string, double> { [x] = w }), 8))}, rechts {Zahlwort(Math.Round(Auswertung.Wert(rechts, new Dictionary<string, double> { [x] = w }), 8))}."
                : $"✓ {x} = {A(kd.Wert)} + {PeriodeText(kd.PeriodePi!.Value)} für k = −1, 0, 1, 2 eingesetzt: beide Seiten gleich."));
        }
        // Vollständigkeit unabhängig: eigene Nullstellensuche von links − rechts im Bereich
        proben.Add(Vollstaendigkeit(H, gute, x));
        var unsicher = proben.Any(p => p.Unsicher);
        string ergebnis;
        if (gute.Count == 0) ergebnis = "L = { } (keine Lösung)";
        else
        {
            var teile = new List<string>();
            var einzel = gute.Where(g => g.PeriodePi is null).ToList();
            for (var i = 0; i < einzel.Count; i++)
            {
                var w = einzel[i].Wert; var ex = Auswertung.Exakt(w);
                var t = ex is { } e ? e.ToString() : $"{A(w)} ≈ {Zahlwort(Math.Round(Auswertung.Wert(w), 8))}";
                teile.Add($"{x}{(einzel.Count + gute.Count(g => g.PeriodePi is not null) > 1 ? Index(i + 1) : "")} = {t}");
            }
            foreach (var p in gute.Where(g => g.PeriodePi is not null)) teile.Add(p.Wert is Zahl { Wert.IstNull: true } ? $"{x} = {PeriodeText(p.PeriodePi!.Value)}" : $"{x} = {A(p.Wert)} + {PeriodeText(p.PeriodePi!.Value)}");
            ergebnis = string.Join(gute.Any(g => g.PeriodePi is not null) ? " oder " : ";  ", teile) + (gute.Any(g => g.PeriodePi is not null) ? " (k ∈ ℤ)" : "");
        }
        if (unsicher) ergebnis += " (unsicher – siehe Probe)";
        var latex = gute.Count == 0 ? "\\mathbb{L} = \\{\\}" : string.Join(",\\quad ", gute.Select(g => $"{x} = {TermText.Latex(g.Wert)}" + (g.PeriodePi is { } pp ? $" + {PeriodeText(pp).Replace("π", "\\pi")}" : "")));
        return new Loesung(MatheArt.Gleichung, eingabe, aufgabe, schritte, ergebnis, latex, proben, TermText.Eingabe(links) + ";" + TermText.Eingabe(rechts), x);
    }

    static string PeriodeText(Bruch c) => c == Bruch.Eins ? "kπ" : c.IstGanz ? $"{c}kπ" : c.Z.IsOne ? $"kπ/{c.N}" : $"{c.Z}kπ/{c.N}";

    /// <summary>Eigene Nullstellensuche von h = links − rechts (Abtasten, Vorzeichenwechsel ohne Polstellen, Berührpunkte) – vergleicht mit den gefundenen Lösungen.</summary>
    static Probe Vollstaendigkeit(Func<double, double> h, List<Kand> loes, string x)
    {
        var periodisch = loes.Any(l => l.PeriodePi is not null);
        var einzel = loes.Where(l => l.PeriodePi is null).Select(l => Auswertung.Wert(l.Wert)).ToList();
        double lo = -10, hi = 10;
        if (!periodisch && einzel.Count > 0) { lo = Math.Max(-1e6, Math.Min(lo, einzel.Min() - 1)); hi = Math.Min(1e6, Math.Max(hi, einzel.Max() + 1)); }
        var erwartet = new List<double>(einzel.Where(v => v > lo && v < hi));
        foreach (var p in loes.Where(l => l.PeriodePi is not null))
        {
            var w = Auswertung.Wert(p.Wert); var per = p.PeriodePi!.Value.AlsDouble() * Math.PI;
            for (var k = (int)Math.Floor((lo - w) / per) - 1; w + k * per < hi; k++) if (w + k * per > lo) erwartet.Add(w + k * per);
        }
        erwartet = [.. erwartet.Order()];
        const int n = 40000;
        var schritt = (hi - lo) / n;
        var gefunden = new List<double>();
        double Abs(double v) { var r = h(v); return double.IsFinite(r) ? Math.Abs(r) : double.PositiveInfinity; }
        double prevX = lo, prev = h(lo);
        for (var i = 1; i <= n; i++)
        {
            if (i % 2000 == 0) Rechenzeit.Pruefe();
            var xv = lo + i * schritt; var hv = h(xv);
            if (double.IsFinite(prev) && double.IsFinite(hv))
            {
                if (hv == 0) gefunden.Add(xv);
                else if (prev * hv < 0)
                {
                    double a = prevX, b = xv, fa = prev;
                    for (var it = 0; it < 100; it++) { var m = (a + b) / 2; var fm = h(m); if (!double.IsFinite(fm)) break; if (fa * fm <= 0) b = m; else { a = m; fa = fm; } }
                    var r = (a + b) / 2;
                    if (Abs(r) < 1e-6 * Math.Max(1, Math.Abs(r))) gefunden.Add(r); // sonst Polstelle (Vorzeichenwechsel ohne Nullstelle)
                }
            }
            // Berührpunkt: kleines lokales Minimum von |h|
            if (i >= 2)
            {
                double xa = lo + (i - 2) * schritt, xb = lo + (i - 1) * schritt;
                if (Abs(xb) < 1e-3 && Abs(xb) <= Abs(xa) && Abs(xb) <= Abs(xv))
                {
                    double a = xa, b = xv;
                    for (var it = 0; it < 100; it++) { var m1 = a + (b - a) / 3; var m2 = b - (b - a) / 3; if (Abs(m1) < Abs(m2)) b = m2; else a = m1; }
                    var r = (a + b) / 2;
                    if (Abs(r) < 1e-9) gefunden.Add(r);
                }
            }
            prevX = xv; prev = hv;
        }
        var tol = 1e-5 * Math.Max(1, hi - lo);
        var eindeutig = new List<double>();
        foreach (var g in gefunden.Order()) if (eindeutig.Count == 0 || g - eindeutig[^1] > tol) eindeutig.Add(g);
        var passt = eindeutig.Count == erwartet.Count && eindeutig.Zip(erwartet).All(t => Math.Abs(t.First - t.Second) <= Math.Max(tol, 1e-4 * Math.Abs(t.Second)));
        var bereich = $"{Zahlwort(lo)} … {Zahlwort(hi)}";
        return passt
            ? new Probe(true, $"✓ Unabhängige Suche (Abtasten von links − rechts im Bereich {bereich}): {eindeutig.Count} Nullstelle(n) – genau die angegebenen Lösungen.")
            : Probe.Gelb($"Die unabhängige Suche im Bereich {bereich} findet {eindeutig.Count} Nullstelle(n), erwartet waren {erwartet.Count} – ob die Lösung vollständig ist, ist unsicher.");
    }

    static int Vorkommen(Term t, string x) => t switch
    {
        Var v => v.Name == x ? 1 : 0,
        Summe s => s.Glieder.Sum(g => Vorkommen(g, x)),
        Produkt p => p.Faktoren.Sum(g => Vorkommen(g, x)),
        Potenz w => Vorkommen(w.Basis, x) + Vorkommen(w.Exponent, x),
        Fkt f => Vorkommen(f.Argument, x),
        _ => 0,
    };

    static double Zahlwert(Term t) { try { return Auswertung.Wert(V(t)); } catch (ArgumentException) { return double.NaN; } }

    /// <summary>Umkehren Schicht für Schicht; verzweigt bei Betrag, geraden Potenzen und Winkelfunktionen. Gibt alle Kandidaten zurück (leer = keine Lösung).</summary>
    static List<Kand> Loese(Term seite, Term wert, string x, List<Schritt> s, string fall, int tiefe)
    {
        if (tiefe > 6) throw new ArgumentException("Zu viele Fallunterscheidungen – diese Gleichung löst der Mathe-Helfer nicht Schritt für Schritt.");
        string F(string t) => fall.Length == 0 ? t : $"{fall}: {t}";
        void Schritt(string text, Term neuSeite, Term neuWert) => s.Add(new Schritt(F(text), $"{A(neuSeite)} = {A(V(neuWert))}"));
        List<Kand> Keine(string grund) { s.Add(new Schritt(F(grund), "keine Lösung")); return []; }
        List<Kand> Faelle(Term u, Term w1, Term w2, string text)
        {
            s.Add(new Schritt(F(text), $"{A(u)} = {A(V(w1))} oder {A(u)} = {A(V(w2))}"));
            if (Zahlwert(w1) is var a && Zahlwert(w2) is var b && Math.Abs(a - b) < 1e-15 * Math.Max(1, Math.Abs(a))) return Loese(u, V(w1), x, s, fall, tiefe + 1);
            return [.. Loese(u, V(w1), x, s, (fall.Length > 0 ? fall + ", " : "") + "Fall 1", tiefe + 1), .. Loese(u, V(w2), x, s, (fall.Length > 0 ? fall + ", " : "") + "Fall 2", tiefe + 1)];
        }
        for (var guard = 0; guard < 40; guard++)
        {
            Rechenzeit.Pruefe();
            wert = V(wert);
            if (seite is Var) return [new Kand(wert, null)];
            var c = Zahlwert(wert);
            switch (seite)
            {
                case Summe sm:
                    {
                        var mit = sm.Glieder.First(g => g.Enthaelt(x)); var ohne = sm.Glieder.Where(g => !g.Enthaelt(x)).ToList();
                        var o = ohne.Count == 1 ? ohne[0] : new Summe(ohne);
                        seite = mit; wert = Term.Plus(wert, Term.Minus(o));
                        Schritt($"{(TermText.Negativ(o) ? "+ " + A(TermText.Negiert(o)) : "− " + A(o))} auf beiden Seiten", seite, wert);
                        continue;
                    }
                case Produkt p:
                    {
                        var mit = p.Faktoren.First(g => g.Enthaelt(x)); var ohne = p.Faktoren.Where(g => !g.Enthaelt(x)).ToList();
                        var o = V(ohne.Count == 1 ? ohne[0] : new Produkt(ohne));
                        seite = mit; wert = Term.Durch(wert, o);
                        Schritt($"Beide Seiten durch {A(o)} teilen", seite, wert);
                        continue;
                    }
                case Potenz w when w.Exponent is Zahl ez && !w.Exponent.Enthaelt(x):
                    {
                        var e = ez.Wert;
                        if (e.Vorzeichen < 0)
                        {
                            if (c == 0) return Keine($"{A(seite)} = 0 hat keine Lösung (ein Kehrwert ist nie 0)");
                            seite = e == Bruch.MinusEins ? w.Basis : new Potenz(w.Basis, new Zahl(-e)); wert = Term.Durch(Term.Eins, wert);
                            Schritt("Kehrwert auf beiden Seiten", seite, wert);
                            continue;
                        }
                        if (e.IstGanz)
                        {
                            var n = (int)e.Z;
                            if (n % 2 == 0)
                            {
                                if (c < 0) return Keine($"Eine gerade Potenz ist nie negativ ({A(wert)} < 0)");
                                if (c == 0) { seite = w.Basis; wert = Term.Null; Schritt($"{n}. Wurzel: nur 0 hoch {n} ergibt 0", seite, wert); continue; }
                                var r = V(Term.Hoch(wert, new Bruch(1, n)));
                                return Faelle(w.Basis, r, Term.Minus(r), $"{n}. Wurzel ziehen – positive und negative Lösung (±)");
                            }
                            seite = w.Basis; wert = c < 0 ? Term.Minus(Term.Hoch(Term.Minus(wert), new Bruch(1, n))) : Term.Hoch(wert, new Bruch(1, n));
                            Schritt($"{n}. Wurzel ziehen (ungerade Hochzahl: genau eine reelle Lösung)", seite, wert);
                            continue;
                        }
                        // Bruch-Exponent p/q: Wurzel auflösen
                        var q = (int)e.N; var pz = (int)e.Z;
                        if (q % 2 == 0 && c < 0) return Keine($"Eine Wurzel ist nie negativ ({A(wert)} < 0)");
                        seite = pz == 1 ? w.Basis : new Potenz(w.Basis, new Zahl(pz)); wert = Term.Hoch(wert, new Bruch(q, 1));
                        Schritt($"Beide Seiten hoch {q} (Wurzel auflösen)", seite, wert);
                        continue;
                    }
                case Potenz w when !w.Basis.Enthaelt(x):
                    {
                        var b = Zahlwert(w.Basis);
                        if (!(b > 0) || b == 1) throw new ArgumentException($"Mit der Basis {A(w.Basis)} lässt sich die Gleichung nicht durch Logarithmieren lösen (die Basis muss positiv und ≠ 1 sein).");
                        if (!(c > 0)) return Keine($"{A(w.Basis)} hoch eine Zahl ist immer positiv – {A(wert)} wird nie erreicht");
                        seite = w.Exponent;
                        wert = w.Basis is Konst { Name: "e" } ? new Fkt("ln", wert) : Term.Durch(new Fkt("ln", wert), new Fkt("ln", w.Basis));
                        Schritt(w.Basis is Konst { Name: "e" } ? "Natürlichen Logarithmus auf beiden Seiten (ln(eᵘ) = u)" : "Logarithmieren: u = ln(rechts)/ln(Basis)", seite, wert);
                        continue;
                    }
                case Fkt f:
                    {
                        var u = f.Argument;
                        switch (f.Name)
                        {
                            case "ln": seite = u; wert = Term.Hoch(Konst.E, wert); Schritt("e hoch beide Seiten (Umkehrung von ln)", seite, wert); continue;
                            case "log": seite = u; wert = Term.Hoch(new Zahl(10), wert); Schritt("10 hoch beide Seiten (Umkehrung von lg)", seite, wert); continue;
                            case "abs":
                                if (c < 0) return Keine($"Ein Betrag ist nie negativ ({A(wert)} < 0)");
                                if (c == 0) { seite = u; wert = Term.Null; Schritt("Betrag ist 0 genau dann, wenn der Inhalt 0 ist", seite, wert); continue; }
                                return Faelle(u, wert, Term.Minus(wert), "Betrag auflösen: der Inhalt ist die Zahl oder ihre Gegenzahl");
                            case "asin": if (c < -Math.PI / 2 || c > Math.PI / 2) return Keine("arcsin liefert nur Werte von −π/2 bis π/2"); seite = u; wert = new Fkt("sin", wert); Schritt("sin auf beiden Seiten", seite, wert); continue;
                            case "acos": if (c < 0 || c > Math.PI) return Keine("arccos liefert nur Werte von 0 bis π"); seite = u; wert = new Fkt("cos", wert); Schritt("cos auf beiden Seiten", seite, wert); continue;
                            case "atan": if (c <= -Math.PI / 2 || c >= Math.PI / 2) return Keine("arctan liefert nur Werte zwischen −π/2 und π/2"); seite = u; wert = new Fkt("tan", wert); Schritt("tan auf beiden Seiten", seite, wert); continue;
                            case "sin" or "cos" or "tan": return Winkel(f.Name, u, wert, c, x, s, fall);
                        }
                        break;
                    }
            }
            throw new ArgumentException("Diese Gleichung kann der Mathe-Helfer nicht Schritt für Schritt umformen.");
        }
        throw new ArgumentException("Die Umformung kommt nicht zum Ziel – der Mathe-Helfer bricht ab, statt weiter zu raten.");
    }

    /// <summary>sin/cos/tan(u) = c mit allen Lösungen: u = β + 2kπ (und zweiter Ast), tan mit kπ; u muss linear sein (a·x + b).</summary>
    static List<Kand> Winkel(string fn, Term u, Term wert, double c, string x, List<Schritt> s, string fall)
    {
        string F(string t) => fall.Length == 0 ? t : $"{fall}: {t}";
        if (fn != "tan" && (c < -1 || c > 1)) { s.Add(new Schritt(F($"{fn} nimmt nur Werte von −1 bis 1 an ({A(wert)} liegt außerhalb)"), "keine Lösung")); return []; }
        if (Linear(u, x) is not { } lin) throw new ArgumentException($"Bei {fn} löst der Mathe-Helfer nur, wenn im Argument die Variable linear steht (z. B. {fn}(2x + 1)) – sonst wären nicht alle Lösungen sicher.");
        var pi = (Term)Konst.Pi;
        List<Term> basen; Bruch periode;
        switch (fn)
        {
            case "sin": { var b = V(new Fkt("asin", wert)); basen = [b, V(Term.Plus(pi, Term.Minus(b)))]; periode = new Bruch(2); s.Add(new Schritt(F("sin ist periodisch (2π) und nimmt jeden Wert zweimal je Periode an: u = arcsin(c) + 2kπ oder u = π − arcsin(c) + 2kπ"), $"{A(u)} = {A(basen[0])} + 2kπ oder {A(u)} = {A(basen[1])} + 2kπ")); break; }
            case "cos": { var b = V(new Fkt("acos", wert)); basen = [b, V(Term.Minus(b))]; periode = new Bruch(2); s.Add(new Schritt(F("cos ist periodisch (2π) und symmetrisch: u = ±arccos(c) + 2kπ"), $"{A(u)} = ±{A(b)} + 2kπ")); break; }
            default: { var b = V(new Fkt("atan", wert)); basen = [b]; periode = Bruch.Eins; s.Add(new Schritt(F("tan ist periodisch mit π: u = arctan(c) + kπ"), $"{A(u)} = {A(b)} + kπ")); break; }
        }
        // gleiche Äste zusammenfassen (z. B. cos u = 1: +0 und −0)
        var verschieden = new List<Term>();
        foreach (var b in basen)
        {
            var bw = Zahlwert(b);
            if (verschieden.Any(v => Math.Abs(Math.IEEERemainder(Zahlwert(v) - bw, periode.AlsDouble() * Math.PI)) < 1e-12)) continue;
            verschieden.Add(b);
        }
        var (a, bb) = lin;
        var res = new List<Kand>();
        foreach (var b in verschieden)
        {
            var w = V(Term.Durch(Term.Plus(b, Term.Minus(new Zahl(bb))), new Zahl(a)));
            res.Add(new Kand(w, periode / a.Betrag));
        }
        if (!(a == Bruch.Eins && bb.IstNull)) s.Add(new Schritt(F($"Nach {x} auflösen (Periode durch {a.Betrag} teilen)"), string.Join(" oder ", res.Select(r => $"{x} = {A(r.Wert)} + {PeriodeText(r.PeriodePi!.Value)}"))));
        return res;
    }

    // =====================================================================================
    // Bestimmte Integrale – auch über Lücken und Polstellen
    // =====================================================================================

    /// <summary>Stellen in [a, b], an denen f nicht definiert ist: Nullstellen von Nennern, ln/lg-Argumente ≤ 0 als Rand, Wurzeln negativer Zahlen.</summary>
    static List<double> Luecken(Term f, string x, double a, double b)
    {
        var res = new List<double>();
        void Basis(Term t)
        {
            if (Polynom.Aus(t) is { } p && p.Koeffizienten(x) is { } k && GradVon(k) >= 1)
            {
                foreach (var r in ReelleNullstellen(k)) if (r >= a - 1e-12 && r <= b + 1e-12) res.Add(Math.Clamp(r, a, b));
                foreach (var r in new[] { a, b }) if (Wert(k, AusDouble(r)).IstNull) res.Add(r);
            }
        }
        void Walk(Term t)
        {
            switch (t)
            {
                case Summe s: foreach (var g in s.Glieder) Walk(g); break;
                case Produkt p: foreach (var g in p.Faktoren) Walk(g); break;
                case Potenz w:
                    Walk(w.Basis); Walk(w.Exponent);
                    if (w.Exponent is Zahl e && (e.Wert.Vorzeichen < 0 || !e.Wert.IstGanz)) Basis(w.Basis);
                    break;
                case Fkt fk:
                    Walk(fk.Argument);
                    if (fk.Name is "ln" or "log" or "tan") Basis(fk.Name == "tan" ? new Fkt("cos", fk.Argument) : fk.Argument);
                    break;
            }
        }
        Walk(f);
        return [.. res.Distinct().Order()];
    }

    static (string Aufgabe, string Ergebnis, string Latex) BestimmtesIntegral(Term f, Term F, string x, string fa, string von, string bis, List<Schritt> schritte, List<Probe> proben)
    {
        var aT = V(TermLeser.Lies(von)); var bT = V(TermLeser.Lies(bis));
        if (aT.Variablen().Any() || bT.Variablen().Any()) throw new FormatException("Die Grenzen müssen Zahlen sein.");
        double a = Auswertung.Wert(aT), b = Auswertung.Wert(bT);
        if (!double.IsFinite(a) || !double.IsFinite(b)) throw new FormatException("Die Grenzen müssen endliche Zahlen sein.");
        var aufgabe = $"Berechne: ∫ von {A(aT)} bis {A(bT)} {fa} d{x}";
        var kopf = $"∫ von {A(aT)} bis {A(bT)} {fa} d{x}";
        double Fw(double v) { try { return Auswertung.Wert(F, new Dictionary<string, double> { [x] = v }); } catch (ArgumentException) { return double.NaN; } }
        double fw(double v) { try { return Auswertung.Wert(f, new Dictionary<string, double> { [x] = v }); } catch (ArgumentException) { return double.NaN; } }
        var (lo, hi) = a <= b ? (a, b) : (b, a);
        var luecken = Luecken(f, x, lo, hi);
        // Ränder, an denen f nicht endlich ist (z. B. ln x bei 0), zählen auch
        foreach (var r in new[] { lo, hi }) if (!double.IsFinite(fw(r)) && !luecken.Contains(r)) luecken.Add(r);
        luecken = [.. luecken.Distinct().Order()];
        if (luecken.Count == 0)
        {
            var Fa = V(F.Ersetze(x, aT)); var Fb = V(F.Ersetze(x, bT));
            var exa = Auswertung.Exakt(Fa); var exb = Auswertung.Exakt(Fb);
            double wa = Auswertung.Wert(Fa), wb = Auswertung.Wert(Fb), wert = wb - wa;
            if (!double.IsFinite(wert)) throw new ArgumentException("Das Integral lässt sich in diesen Grenzen nicht ausrechnen.");
            var exText = exa is { } ea && exb is { } eb ? (eb - ea).ToString() : null;
            var wertText = exText is not null ? exText + (exText.Contains('/') ? " ≈ " + Zahlwort(Math.Round(wert, 8)) : "") : A(V(Term.Plus(Fb, Term.Minus(Fa)))) + " ≈ " + Zahlwort(Math.Round(wert, 8));
            schritte.Add(new Schritt($"Hauptsatz: Grenzen einsetzen, F({A(bT)}) − F({A(aT)})", $"[{A(F)}] von {A(aT)} bis {A(bT)} = {A(Fb)} − ({A(Fa)}) = {wertText}"));
            proben.Add(ProbeSimpson(f, x, a, b, wert));
            var latex = $"\\int_{{{TermText.Latex(aT)}}}^{{{TermText.Latex(bT)}}} {TermText.Latex(f)}\\,d{x} = {(exText is not null && exa is { } la && exb is { } lb ? TermText.Latex(new Zahl(lb - la)) : Zahlwort(Math.Round(wert, 8)).Replace(",", "{,}"))}";
            return (aufgabe, $"{kopf} = {wertText}", latex);
        }
        // Uneigentliches Integral: an jeder Lücke Grenzwert von F
        var luText = string.Join(", ", luecken.Select(l => $"{x} = {Zahlwort(Math.Round(l, 10))}"));
        schritte.Add(new Schritt($"Achtung: Die Funktion ist im Intervall nicht überall definiert ({luText}) – uneigentliches Integral: an diesen Stellen den Grenzwert von F bilden", $"F({x}) = {A(F)}"));
        double? Grenzwert(double p, int seite) // seite +1: von rechts, −1: von links
        {
            var werte = new[] { 1e-4, 1e-6, 1e-8, 1e-10 }.Select(eps => Fw(p + seite * eps * Math.Max(1, Math.Abs(p)))).ToList();
            if (werte.Any(v => !double.IsFinite(v))) return null;
            var d1 = Math.Abs(werte[1] - werte[0]); var d2 = Math.Abs(werte[2] - werte[1]); var d3 = Math.Abs(werte[3] - werte[2]);
            var skala = Math.Max(1, Math.Abs(werte[3]));
            // konvergent: die Abstände schrumpfen deutlich (Faktor 2 je Schritt) und werden klein; sonst (z. B. ln|x|: gleich große Schritte) divergent
            if (!(d3 <= 1e-3 * skala && d3 <= 0.5 * d2 + 1e-13 && d2 <= 0.5 * d1 + 1e-13)) return null;
            var direkt = Fw(p);
            var grenz = double.IsFinite(direkt) && Math.Abs(direkt - werte[3]) <= 10 * d3 + 1e-9 * skala ? direkt : werte[3];
            var glatt = Bruch.Naeherung(grenz, 1000).AlsDouble(); // Rundungsrest des Grenzwerts (z. B. −2·10⁻⁹ statt 0) glätten
            return Math.Abs(glatt - grenz) <= 10 * d3 + 1e-9 ? glatt : grenz;
        }
        var punkte = new List<double> { lo }; punkte.AddRange(luecken.Where(l => l > lo && l < hi)); punkte.Add(hi);
        var summe = 0.0; var divergent = new List<double>();
        for (var i = 0; i + 1 < punkte.Count; i++)
        {
            double l = punkte[i], r = punkte[i + 1];
            double? Fl = luecken.Contains(l) ? Grenzwert(l, +1) : Fw(l);
            double? Fr = luecken.Contains(r) ? Grenzwert(r, -1) : Fw(r);
            if (Fl is null || !double.IsFinite(Fl.Value)) divergent.Add(l);
            if (Fr is null || !double.IsFinite(Fr.Value)) divergent.Add(r);
            if (Fl is { } fl && Fr is { } fr && double.IsFinite(fl) && double.IsFinite(fr))
            {
                summe += fr - fl;
                schritte.Add(new Schritt($"Teilstück {Zahlwort(l)} … {Zahlwort(r)}{(luecken.Contains(l) || luecken.Contains(r) ? " (mit Grenzwert)" : "")}", $"F({Zahlwort(r)}) − F({Zahlwort(l)}) = {Zahlwort(Math.Round(fr, 10))} − ({Zahlwort(Math.Round(fl, 10))}) = {Zahlwort(Math.Round(fr - fl, 10))}"));
            }
        }
        if (a > b) summe = -summe;
        if (divergent.Count > 0)
        {
            var bei = string.Join(", ", divergent.Distinct().Select(d => $"{x} = {Zahlwort(Math.Round(d, 10))}"));
            schritte.Add(new Schritt($"Der Grenzwert von F existiert nicht (F wächst über alle Grenzen bei {bei}) – das Integral divergiert", "existiert nicht"));
            proben.Add(ProbeDivergenz(fw, divergent.Distinct().ToList(), lo, hi));
            return (aufgabe, $"{kopf} existiert nicht (das Integral divergiert bei {bei})", $"\\int_{{{TermText.Latex(aT)}}}^{{{TermText.Latex(bT)}}} {TermText.Latex(f)}\\,d{x} \\text{{ existiert nicht}}");
        }
        var nahe = Bruch.Naeherung(summe, 1000);
        var text = Math.Abs(nahe.AlsDouble() - summe) < 1e-7 * Math.Max(1, Math.Abs(summe)) ? nahe + (nahe.IstGanz ? "" : " ≈ " + Zahlwort(Math.Round(summe, 8))) : "≈ " + Zahlwort(Math.Round(summe, 8));
        schritte.Add(new Schritt("Teilstücke addieren", $"{kopf} = {text}"));
        proben.Add(ProbeUneigentlich(fw, punkte, luecken, a > b ? -summe : summe));
        return (aufgabe, $"{kopf} = {text}", $"\\int_{{{TermText.Latex(aT)}}}^{{{TermText.Latex(bT)}}} {TermText.Latex(f)}\\,d{x} = {Zahlwort(Math.Round(summe, 8)).Replace(",", "{,}")}");
    }

    /// <summary>Adaptive Simpson-Regel (für die Gegenrechnung, rechnet nur mit f).</summary>
    static double Simpson(Func<double, double> f, double a, double b, double eps, int tiefe)
    {
        double S(double l, double r) { var m = (l + r) / 2; return (r - l) / 6 * (f(l) + 4 * f(m) + f(r)); }
        double Rek(double l, double r, double ganz, double e, int t)
        {
            var m = (l + r) / 2; double sl = S(l, m), sr = S(m, r);
            if (t <= 0 || Math.Abs(sl + sr - ganz) <= 15 * e) return sl + sr + (sl + sr - ganz) / 15;
            return Rek(l, m, sl, e / 2, t - 1) + Rek(m, r, sr, e / 2, t - 1);
        }
        return Rek(a, b, S(a, b), eps, tiefe);
    }

    static Probe ProbeUneigentlich(Func<double, double> f, List<double> punkte, List<double> luecken, double wert)
    {
        // Gegenrechnung mit f allein: an jeder Lücke ein kleines Stück aussparen (δ = 10⁻⁸ der Länge), adaptive Simpson-Regel
        var sum = 0.0;
        for (var i = 0; i + 1 < punkte.Count; i++)
        {
            double l = punkte[i], r = punkte[i + 1], d = 1e-8 * (r - l);
            var ll = luecken.Contains(l) ? l + d : l; var rr = luecken.Contains(r) ? r - d : r;
            sum += Simpson(f, ll, rr, 1e-10, 50);
        }
        var ok = double.IsFinite(sum) && Math.Abs(sum - wert) <= 1e-3 * Math.Max(1, Math.Abs(wert));
        return ok ? new Probe(true, $"✓ Gegenrechnung ohne Stammfunktion (adaptive Simpson-Regel, Lücken ausgespart): {Zahlwort(Math.Round(sum, 6))}.")
                  : Probe.Gelb($"Die Gegenrechnung ohne Stammfunktion ergibt {Zahlwort(Math.Round(sum, 6))} – der Grenzwert ist unsicher.");
    }

    static Probe ProbeDivergenz(Func<double, double> f, List<double> stellen, double lo, double hi)
    {
        // Gegenrechnung: ∫|f| von der Lücke weg wächst, wenn man näher heranrückt
        foreach (var p in stellen)
        {
            var seite = p < hi ? 1 : -1; var weite = Math.Min(0.5, (hi - lo) / 2);
            double Teil(double d) => Math.Abs(Simpson(v => Math.Abs(f(v)), seite > 0 ? p + d : p - weite, seite > 0 ? p + weite : p - d, 1e-8, 40));
            var a1 = Teil(1e-3); var a2 = Teil(1e-9);
            if (double.IsFinite(a2) && a2 < 2 * a1) return Probe.Gelb("Die Gegenrechnung zeigt kein klares Wachstum an der Lücke – ob das Integral divergiert, ist unsicher.");
        }
        return new Probe(true, "✓ Gegenrechnung ohne Stammfunktion: rückt man näher an die Lücke heran, wächst die Fläche immer weiter – das Integral existiert nicht.");
    }
}
