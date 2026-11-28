using System.Globalization;
using System.Text;

namespace Trendo.Core.Business;

// ===================== Kredit-Rechner für „Geld“ (SPEC-11, T-0694) =====================
// Annuitätendarlehen mit monatlicher Rate: Tilgungsplan, Sondertilgungen, Zinsbindung, Effektivzins-Näherung.
// Formeln (Finanzmathematik, Lehrbuchwissen): Monatszins i = Sollzins p. a. / 12; Rate = K · i / (1 − (1 + i)^−n).
// Der Effektivzins wird als interner Zinsfuß der Zahlungsreihe bestimmt und aufs Jahr hochgerechnet ((1 + i_m)^12 − 1) – eine Näherung an die
// Preisangabenverordnung, KEIN verbindlicher effektiver Jahreszins einer Bank. Keine Finanzberatung.

/// <summary>Eine Zeile des Tilgungsplans (Monat ab 1).</summary>
public sealed record KreditRate(int Monat, DateOnly Datum, decimal Rate, decimal Zins, decimal Tilgung, decimal Sonder, decimal Rest);

public sealed record KreditPlan(decimal Betrag, decimal SollzinsPa, decimal Monatsrate, IReadOnlyList<KreditRate> Raten, decimal Gebuehr)
{
    public decimal ZinsenGesamt => Raten.Sum(r => r.Zins);
    public decimal SonderGesamt => Raten.Sum(r => r.Sonder);
    public decimal Gezahlt => Raten.Sum(r => r.Rate + r.Sonder) + Gebuehr;
    public decimal Restschuld => Raten.Count == 0 ? Betrag : Raten[^1].Rest;
    public int Monate => Raten.Count;
    public string Laufzeit => Monate % 12 == 0 ? $"{Monate / 12} Jahre" : Monate > 12 ? $"{Monate / 12} Jahre und {Monate % 12} Monate" : $"{Monate} Monate";
    /// <summary>Effektivzins-Näherung in Prozent p. a.: interner Zinsfuß aus Auszahlung (Betrag − Gebühr), allen Zahlungen und der Restschuld am Ende.</summary>
    public decimal EffektivPa => Kredit.Effektiv(Betrag - Gebuehr, [.. Raten.Select(r => r.Rate + r.Sonder)], Restschuld);
    /// <summary>Summen je Kalenderjahr: (Jahr, Zinsen, Tilgung inkl. Sondertilgung, Restschuld am Jahresende).</summary>
    public IReadOnlyList<(int Jahr, decimal Zins, decimal Tilgung, decimal Schuld)> Jahre =>
        [.. Raten.GroupBy(r => r.Datum.Year).Select(g => (g.Key, g.Sum(r => r.Zins), g.Sum(r => r.Tilgung + r.Sonder), g.Last().Rest))];
}

public static class Kredit
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public const int MaxMonate = 12 * 60;
    public const string Hinweis = "Rechnung mit festem Sollzins und monatlicher Rate. Der Effektivzins ist eine Näherung (interner Zinsfuß) und kein Angebot einer Bank. Keine Finanzberatung.";

    static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>Monatsrate eines Annuitätendarlehens, das in <paramref name="monate"/> Monaten vollständig getilgt ist.</summary>
    public static decimal Rate(decimal betrag, decimal sollzinsPa, int monate)
    {
        if (betrag <= 0) throw new ArgumentException("Der Kreditbetrag muss größer als 0 sein.");
        if (monate is < 1 or > MaxMonate) throw new ArgumentException($"Die Laufzeit muss zwischen 1 und {MaxMonate} Monaten liegen.");
        if (sollzinsPa is < 0 or > 50) throw new ArgumentException("Der Sollzins muss zwischen 0 und 50 % liegen.");
        if (sollzinsPa == 0) return R2(betrag / monate);
        var i = (double)sollzinsPa / 1200.0;
        return R2((decimal)((double)betrag * i / (1 - Math.Pow(1 + i, -monate))));
    }

    /// <summary>Monatsrate aus anfänglicher Tilgung in Prozent p. a. (wie bei Baufinanzierungen üblich): (Zins + Tilgung) · Betrag / 12.</summary>
    public static decimal RateAusTilgung(decimal betrag, decimal sollzinsPa, decimal tilgungPa) => R2(betrag * (sollzinsPa + tilgungPa) / 1200m);

    /// <summary>Tilgungsplan. Entweder <paramref name="monate"/> (dann wird die Rate berechnet) oder <paramref name="rate"/> (dann ergibt sich die Laufzeit) angeben.
    /// <paramref name="sonderJaehrlich"/>: Sondertilgung jeweils nach 12, 24 … Monaten; <paramref name="sonder"/>: einzelne Sondertilgungen (Monat → Betrag).
    /// <paramref name="zinsbindungMonate"/>: nur so viele Monate rechnen – der Rest ist die Restschuld am Ende der Zinsbindung.</summary>
    public static KreditPlan Plan(decimal betrag, decimal sollzinsPa, int? monate = null, decimal? rate = null, DateOnly? start = null,
        decimal sonderJaehrlich = 0, IReadOnlyDictionary<int, decimal>? sonder = null, int? zinsbindungMonate = null, decimal gebuehr = 0)
    {
        if (betrag <= 0) throw new ArgumentException("Der Kreditbetrag muss größer als 0 sein.");
        if (sollzinsPa is < 0 or > 50) throw new ArgumentException("Der Sollzins muss zwischen 0 und 50 % liegen.");
        if (monate is null == rate is null) throw new ArgumentException("Bitte entweder die Laufzeit oder die Monatsrate angeben.");
        if (sonderJaehrlich < 0 || sonder?.Values.Any(v => v < 0) == true || gebuehr < 0) throw new ArgumentException("Sondertilgung und Gebühr dürfen nicht negativ sein.");
        if (gebuehr >= betrag) throw new ArgumentException("Die Gebühr muss kleiner als der Kreditbetrag sein.");
        var m = rate ?? Rate(betrag, sollzinsPa, monate!.Value);
        var first = R2(betrag * sollzinsPa / 1200m);
        if (m <= first) throw new ArgumentException($"Die Rate von {m.ToString("N2", De)} € deckt nicht einmal die Zinsen des ersten Monats ({first.ToString("N2", De)} €) – der Kredit würde nie getilgt.");
        var s0 = start ?? DateOnly.FromDateTime(DateTime.Today);
        var rows = new List<KreditRate>(); var rest = betrag;
        var limit = Math.Min(MaxMonate, zinsbindungMonate ?? MaxMonate);
        for (var k = 1; rest > 0 && k <= limit; k++)
        {
            var zins = R2(rest * sollzinsPa / 1200m);
            var tilg = Math.Min(rest, m - zins);
            // letzte planmäßige Rate bei fester Laufzeit: Rundungsrest mitnehmen, damit die Restschuld genau 0,00 € wird
            if (monate is { } n && k == n && zinsbindungMonate is null) tilg = rest;
            rest -= tilg;
            var extra = (sonder?.GetValueOrDefault(k) ?? 0) + (sonderJaehrlich > 0 && k % 12 == 0 ? sonderJaehrlich : 0);
            extra = Math.Min(extra, rest); rest -= extra;
            rows.Add(new KreditRate(k, s0.AddMonths(k), zins + tilg, zins, tilg, extra, rest));
        }
        return new KreditPlan(betrag, sollzinsPa, m, rows, gebuehr);
    }

    /// <summary>Interner Zinsfuß der Zahlungsreihe (monatlich, nachschüssig), aufs Jahr hochgerechnet – in Prozent, zwei Nachkommastellen.
    /// Gesucht ist i mit: Auszahlung = Σ Zahlung_k / (1 + i)^k + Restschuld / (1 + i)^n. Lösung durch Intervallhalbierung (monoton fallend in i).</summary>
    public static decimal Effektiv(decimal auszahlung, IReadOnlyList<decimal> zahlungen, decimal restschuld = 0)
    {
        if (auszahlung <= 0 || zahlungen.Count == 0) return 0;
        double Barwert(double i)
        {
            double sum = 0, q = 1;
            foreach (var z in zahlungen) { q *= 1 + i; sum += (double)z / q; }
            return sum + (double)restschuld / q;
        }
        if (Barwert(0) <= (double)auszahlung) return 0; // es wird nicht mehr zurückgezahlt als ausgezahlt
        double lo = 0, hi = 1;
        for (var k = 0; k < 200; k++) { var mid = (lo + hi) / 2; if (Barwert(mid) > (double)auszahlung) lo = mid; else hi = mid; }
        return Math.Round((decimal)((Math.Pow(1 + (lo + hi) / 2, 12) - 1) * 100), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Tilgungsplan als CSV (Semikolon, deutsches Zahlenformat) – öffnet sich in Excel, Calc und Trendo Tabelle.</summary>
    public static string Csv(KreditPlan p)
    {
        var sb = new StringBuilder("Monat;Datum;Rate;Zinsen;Tilgung;Sondertilgung;Restschuld\r\n");
        foreach (var r in p.Raten)
            sb.Append(string.Join(";", r.Monat, r.Datum.ToString("dd.MM.yyyy", De), r.Rate.ToString("0.00", De), r.Zins.ToString("0.00", De), r.Tilgung.ToString("0.00", De), r.Sonder.ToString("0.00", De), r.Rest.ToString("0.00", De))).Append("\r\n");
        sb.Append($"Summe;;{p.Raten.Sum(r => r.Rate).ToString("0.00", De)};{p.ZinsenGesamt.ToString("0.00", De)};{p.Raten.Sum(r => r.Tilgung).ToString("0.00", De)};{p.SonderGesamt.ToString("0.00", De)};{p.Restschuld.ToString("0.00", De)}\r\n");
        return sb.ToString();
    }

    /// <summary>Tilgungsplan als PDF (Kennzahlen oben, dann die Tabelle mit Seitenumbruch).</summary>
    public static int Pdf(KreditPlan p, string path)
    {
        using var pdf = new BizPdf("Tilgungsplan", "") /* T-0784: Trendo ist nicht der Autor */ { Footer = Hinweis };
        pdf.Write("Tilgungsplan", 16, true, pdf.Accent); pdf.Y += 2;
        pdf.Write($"Kreditbetrag {BizJson.Euro(p.Betrag)} · Sollzins {BizJson.Num(p.SollzinsPa, "0.###")} % p. a. · Monatsrate {BizJson.Euro(p.Monatsrate)} · Laufzeit {p.Laufzeit}", 10);
        pdf.Write($"Zinsen gesamt {BizJson.Euro(p.ZinsenGesamt)} · Sondertilgungen {BizJson.Euro(p.SonderGesamt)} · insgesamt gezahlt {BizJson.Euro(p.Gezahlt)} · Restschuld am Ende {BizJson.Euro(p.Restschuld)} · Effektivzins etwa {BizJson.Num(p.EffektivPa, "0.00")} % p. a.", 10);
        pdf.Y += 3;
        pdf.Table(["Monat", "Datum", "Rate", "Zinsen", "Tilgung", "Sondertilgung", "Restschuld"], [16, 24, 26, 26, 26, 26, 26], "rlrrrrr",
            p.Raten.Select(r => new[] { r.Monat.ToString(De), r.Datum.ToString("dd.MM.yyyy", De), BizJson.Euro(r.Rate), BizJson.Euro(r.Zins), BizJson.Euro(r.Tilgung), r.Sonder == 0 ? "" : BizJson.Euro(r.Sonder), BizJson.Euro(r.Rest) }), 8.5);
        return pdf.Save(path);
    }
}
