using System.Globalization;
using System.Numerics;

namespace Trendo.Core.Wissen;

/// <summary>T-0781 Mathe-Helfer: exakte rationale Zahl (Zähler/Nenner als <see cref="BigInteger"/>, immer gekürzt, Nenner &gt; 0).
/// Rechnet ohne Gleitkomma – 1/3 + 1/6 ist genau 1/2.</summary>
public readonly struct Bruch : IEquatable<Bruch>, IComparable<Bruch>
{
    public BigInteger Z { get; }
    public BigInteger N { get; }

    public Bruch(BigInteger z, BigInteger n)
    {
        if (n.IsZero) throw new DivideByZeroException("Teilen durch 0 geht nicht.");
        if (n.Sign < 0) { z = -z; n = -n; }
        var g = BigInteger.GreatestCommonDivisor(z, n);
        if (!g.IsZero && !g.IsOne) { z /= g; n /= g; }
        Z = z; N = z.IsZero ? BigInteger.One : n;
    }

    public Bruch(long z) : this(z, 1) { }
    public static readonly Bruch Null = new(0), Eins = new(1), MinusEins = new(-1);
    public static implicit operator Bruch(long v) => new(v);

    public bool IstNull => Z.IsZero;
    public bool IstGanz => N.IsOne;
    public int Vorzeichen => Z.Sign;
    public Bruch Betrag => new(BigInteger.Abs(Z), N);
    public Bruch Kehrwert => new(N, Z);

    public static Bruch operator +(Bruch a, Bruch b) => a.N == b.N ? new(a.Z + b.Z, a.N) : new(a.Z * b.N + b.Z * a.N, a.N * b.N);
    public static Bruch operator -(Bruch a, Bruch b) => a + -b;
    public static Bruch operator -(Bruch a) => new(-a.Z, a.N);
    public static Bruch operator *(Bruch a, Bruch b) => new(a.Z * b.Z, a.N * b.N);
    public static Bruch operator /(Bruch a, Bruch b) => b.IstNull ? throw new DivideByZeroException("Teilen durch 0 geht nicht.") : new(a.Z * b.N, a.N * b.Z);
    public static bool operator ==(Bruch a, Bruch b) => a.Equals(b);
    public static bool operator !=(Bruch a, Bruch b) => !a.Equals(b);
    public static bool operator <(Bruch a, Bruch b) => a.CompareTo(b) < 0;
    public static bool operator >(Bruch a, Bruch b) => a.CompareTo(b) > 0;
    public static bool operator <=(Bruch a, Bruch b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Bruch a, Bruch b) => a.CompareTo(b) >= 0;

    /// <summary>Ganzzahlige Potenz (auch negativ). Zu große Exponenten werden abgelehnt, damit nichts ausufert.</summary>
    public Bruch Hoch(int e)
    {
        if (Math.Abs(e) > 4096) throw new OverflowException("Der Exponent ist zu groß.");
        if (e == 0) return Eins;
        if (e < 0) { if (IstNull) throw new DivideByZeroException("0 hoch eine negative Zahl geht nicht."); return Kehrwert.Hoch(-e); }
        return new(BigInteger.Pow(Z, e), BigInteger.Pow(N, e));
    }

    /// <summary>Exakte Quadratwurzel, wenn Zähler und Nenner Quadratzahlen sind; sonst null.</summary>
    public Bruch? Wurzel()
    {
        if (Z.Sign < 0) return null;
        var a = ISqrt(Z); var b = ISqrt(N);
        return a * a == Z && b * b == N ? new Bruch(a, b) : null;
    }

    /// <summary>Exakte n-te Wurzel (n ≥ 2) oder null.</summary>
    public Bruch? Wurzel(int n)
    {
        if (n == 2) return Wurzel();
        if (Z.Sign < 0 && n % 2 == 0) return null;
        var a = IRoot(BigInteger.Abs(Z), n); var b = IRoot(N, n);
        if (BigInteger.Pow(a, n) != BigInteger.Abs(Z) || BigInteger.Pow(b, n) != N) return null;
        return new Bruch(Z.Sign < 0 ? -a : a, b);
    }

    public static BigInteger ISqrt(BigInteger v)
    {
        if (v.Sign <= 0) return BigInteger.Zero;
        var x = (BigInteger)Math.Sqrt((double)v);
        while (x * x > v) x--;
        while ((x + 1) * (x + 1) <= v) x++;
        return x;
    }

    static BigInteger IRoot(BigInteger v, int n)
    {
        if (v.IsZero) return 0;
        var x = (BigInteger)Math.Round(Math.Pow((double)v, 1.0 / n));
        while (x > 0 && BigInteger.Pow(x, n) > v) x--;
        while (BigInteger.Pow(x + 1, n) <= v) x++;
        return x;
    }

    public static BigInteger Ggt(BigInteger a, BigInteger b) => BigInteger.GreatestCommonDivisor(a, b);
    public static BigInteger Kgv(BigInteger a, BigInteger b) => a.IsZero || b.IsZero ? 0 : BigInteger.Abs(a / Ggt(a, b) * b);

    public double AlsDouble() => (double)Z / (double)N is var d && double.IsFinite(d) ? d : Math.Exp(BigInteger.Log(BigInteger.Abs(Z)) - BigInteger.Log(N)) * Z.Sign;

    public bool Equals(Bruch o) => Z == o.Z && N == o.N;
    public override bool Equals(object? obj) => obj is Bruch b && Equals(b);
    public override int GetHashCode() => HashCode.Combine(Z, N);
    public int CompareTo(Bruch o) => (Z * o.N).CompareTo(o.Z * N);

    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Anzeige: „3“, „−3/4“ (mit echtem Minuszeichen).</summary>
    public override string ToString() => (Z.Sign < 0 ? "−" : "") + BigInteger.Abs(Z).ToString(CultureInfo.InvariantCulture) + (IstGanz ? "" : "/" + N.ToString(CultureInfo.InvariantCulture));
    /// <summary>Für die Eingabezeile und den Rechner: „-3/4“ mit ASCII-Minus.</summary>
    public string Eingabe() => (Z.Sign < 0 ? "-" : "") + BigInteger.Abs(Z).ToString(CultureInfo.InvariantCulture) + (IstGanz ? "" : "/" + N.ToString(CultureInfo.InvariantCulture));

    /// <summary>Gemischte Zahl („2 1/2“) für unechte Brüche, sonst wie <see cref="ToString"/>.</summary>
    public string Gemischt()
    {
        if (IstGanz || BigInteger.Abs(Z) < N) return ToString();
        var ganz = BigInteger.DivRem(BigInteger.Abs(Z), N, out var rest);
        return (Z.Sign < 0 ? "−" : "") + ganz.ToString(CultureInfo.InvariantCulture) + " " + rest.ToString(CultureInfo.InvariantCulture) + "/" + N.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Dezimalzahl mit Komma; endet der Bruch nicht, wird gerundet und „≈“ davorgesetzt.</summary>
    public string Dezimal(int stellen = 10)
    {
        if (IstGanz) return ToString();
        var n = N; while (n % 2 == 0) n /= 2; while (n % 5 == 0) n /= 5;
        var exakt = n.IsOne;
        var faktor = BigInteger.Pow(10, stellen);
        var scaled = BigInteger.DivRem(BigInteger.Abs(Z) * faktor, N, out var rem);
        if (rem * 2 >= N) scaled += 1;
        var s = scaled.ToString(CultureInfo.InvariantCulture).PadLeft(stellen + 1, '0');
        var txt = (s[..^stellen] + "," + s[^stellen..]).TrimEnd('0').TrimEnd(',');
        return (exakt ? "" : "≈ ") + (Z.Sign < 0 ? "−" : "") + txt;
    }

    /// <summary>Zahl aus Text: „12“, „2,5“, „0.125“, „3/4“, „-1,5e3“ – Dezimalzahlen werden exakt als Bruch gelesen.</summary>
    public static Bruch Lies(string text)
    {
        var s = text.Trim().Replace('−', '-').Replace(" ", "");
        if (s.Contains('/'))
        {
            var p = s.Split('/');
            if (p.Length != 2) throw new FormatException($"„{text}“ ist keine Zahl.");
            return Lies(p[0]) / Lies(p[1]);
        }
        s = s.Replace(',', '.');
        var exp = 0;
        var ei = s.IndexOfAny(['e', 'E']);
        if (ei > 0) { if (!int.TryParse(s[(ei + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exp)) throw new FormatException($"„{text}“ ist keine Zahl."); s = s[..ei]; }
        var neg = s.StartsWith('-'); if (neg || s.StartsWith('+')) s = s[1..];
        var dot = s.IndexOf('.');
        var digits = dot < 0 ? s : s[..dot] + s[(dot + 1)..];
        if (digits.Length == 0 || !digits.All(char.IsAsciiDigit) || s.Count(c => c == '.') > 1) throw new FormatException($"„{text}“ ist keine Zahl.");
        var z = BigInteger.Parse(digits, CultureInfo.InvariantCulture);
        var nachkomma = dot < 0 ? 0 : s.Length - dot - 1;
        var b = new Bruch(neg ? -z : z, BigInteger.Pow(10, nachkomma));
        return exp == 0 ? b : b * new Bruch(10).Hoch(exp);
    }

    public static bool TryLies(string text, out Bruch b)
    {
        try { b = Lies(text); return true; }
        catch (Exception e) when (e is FormatException or DivideByZeroException or OverflowException) { b = Null; return false; }
    }

    /// <summary>Näherungsbruch einer Gleitkommazahl (Kettenbruch, Nenner ≤ <paramref name="maxNenner"/>).</summary>
    public static Bruch Naeherung(double v, long maxNenner = 1_000_000)
    {
        if (!double.IsFinite(v)) throw new OverflowException("Keine endliche Zahl.");
        long h0 = 0, h1 = 1, k0 = 1, k1 = 0; var x = v;
        for (var i = 0; i < 40; i++)
        {
            var a = (long)Math.Floor(x);
            var h2 = a * h1 + h0; var k2 = a * k1 + k0;
            if (k2 > maxNenner) break;
            h0 = h1; h1 = h2; k0 = k1; k1 = k2;
            if (Math.Abs(x - a) < 1e-12) break;
            x = 1 / (x - a);
        }
        return new Bruch(h1, k1 == 0 ? 1 : k1);
    }
}
