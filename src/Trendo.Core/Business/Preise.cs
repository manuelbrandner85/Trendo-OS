using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Trendo.Core.Business;

// ===================== STUDIO-PRICING-01 Preise kalkulieren (T-0675) =====================
// Clean-Room nach Kalkulations-Vorlagen (IHK/Handwerk) und Shop-Margenrechnern: Zuschlagskalkulation, Rückwärtsrechnung vom Wunschpreis,
// Stundensatz für Selbständige, Marge/Aufschlag, Break-Even mit Diagramm, Plattformgebühren (einstellbare Tabellen mit Stand), Preisstaffeln,
// Übergabe als Artikel an „Rechnungen & Angebote“. Alles sind Rechenhilfen – keine Steuer- oder Unternehmensberatung.

public sealed class Kalkulation
{
    public string Name { get; set; } = "Produkt";
    public string Einheit { get; set; } = "Stück";
    public decimal Material { get; set; }
    /// <summary>Materialgemeinkosten in % auf das Material (Lager, Einkauf).</summary>
    public decimal MaterialGk { get; set; }
    public decimal Stunden { get; set; }
    public decimal Stundensatz { get; set; }
    /// <summary>Fertigungsgemeinkosten in % auf den Lohn (Maschinen, Werkstatt).</summary>
    public decimal FertigungGk { get; set; }
    /// <summary>Verwaltungs- und Vertriebsgemeinkosten in % auf die Herstellkosten.</summary>
    public decimal VerwaltungGk { get; set; }
    /// <summary>Gewinnzuschlag in % auf die Selbstkosten.</summary>
    public decimal Gewinn { get; set; } = 20;
    public decimal Ust { get; set; } = 19;
    public bool Kleinunternehmer { get; set; }
}

public sealed class StundensatzEingabe
{
    /// <summary>Was im Monat zum Leben übrig bleiben soll (nach Steuern und Vorsorge).</summary>
    public decimal WunschNetto { get; set; } = 3000;
    /// <summary>Kranken- und Pflegeversicherung, Rente und weitere Vorsorge im Monat.</summary>
    public decimal Vorsorge { get; set; } = 900;
    /// <summary>Geschätzter Anteil der Einkommensteuer am Gewinn in % (eigene Schätzung).</summary>
    public decimal SteuerProzent { get; set; } = 25;
    /// <summary>Betriebskosten im Monat (Miete, Software, Versicherung, Auto …).</summary>
    public decimal Kosten { get; set; } = 600;
    public int Urlaub { get; set; } = 30;
    public int Feiertage { get; set; } = 10;
    public int Krankheit { get; set; } = 10;
    public int Fortbildung { get; set; } = 5;
    public decimal Wochenstunden { get; set; } = 40;
    /// <summary>Anteil der Arbeitszeit, der bezahlt wird (Rest: Akquise, Büro, Buchhaltung).</summary>
    public decimal Auslastung { get; set; } = 60;
}

public sealed class Plattform
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Prozent vom Verkaufspreis (brutto inkl. Versand), z. B. Transaktions- und Zahlungsgebühr zusammen.</summary>
    public decimal Prozent { get; set; }
    /// <summary>Fester Betrag je Verkauf.</summary>
    public decimal JeVerkauf { get; set; }
    /// <summary>Einstellgebühr je Angebot (je verkauftem Stück angesetzt).</summary>
    public decimal JeAngebot { get; set; }
    /// <summary>Monatliche Grundgebühr (verteilt auf die Verkäufe im Monat).</summary>
    public decimal Monatlich { get; set; }
    /// <summary>Mindestgebühr je Verkauf (z. B. Verkaufsgebühr).</summary>
    public decimal Mindest { get; set; }
    public string Quelle { get; set; } = "";
    public string Stand { get; set; } = "";
}

public sealed class Staffel
{
    public int AbMenge { get; set; } = 1;
    public decimal Rabatt { get; set; }
}

public sealed class Preisblatt
{
    public Kalkulation Kalkulation { get; set; } = new();
    public StundensatzEingabe Stundensatz { get; set; } = new();
    /// <summary>Wunschpreis für die Rückwärtsrechnung (brutto, wie im Laden).</summary>
    public decimal Wunschpreis { get; set; }
    public decimal Fixkosten { get; set; } = 1500;
    public List<Plattform> Plattformen { get; set; } = [.. PreisKit.StandardPlattformen.Select(BizJson.Clone)];
    public string Plattform { get; set; } = "etsy";
    public decimal Versand { get; set; }
    public int VerkaeufeMonat { get; set; } = 30;
    public List<Staffel> Staffeln { get; set; } = [new() { AbMenge = 1 }, new() { AbMenge = 10, Rabatt = 5 }, new() { AbMenge = 50, Rabatt = 10 }];
    public JsonObject ToJson() => BizJson.To(this);
    public static Preisblatt FromJson(JsonNode? n) => BizJson.From<Preisblatt>(n);
}

public sealed record Kalkulationsschema(decimal Material, decimal MaterialGk, decimal Lohn, decimal FertigungGk, decimal Herstellkosten, decimal VerwaltungGk,
    decimal Selbstkosten, decimal Gewinn, decimal Netto, decimal Ust, decimal Brutto, decimal Aufschlag, decimal Marge);
public sealed record Rueckwaerts(decimal Netto, decimal Selbstkosten, decimal Gewinn, decimal GewinnProzent, decimal Marge, decimal MaxSelbstkosten, decimal MaxMaterial, bool Lohnt);
public sealed record StundensatzErgebnis(int Arbeitstage, decimal Jahresstunden, decimal Fakturierbar, decimal GewinnVorSteuerJahr, decimal UmsatzJahr, decimal Satz, decimal Tagessatz);
public sealed record BreakEvenErgebnis(decimal Deckungsbeitrag, int Menge, decimal Umsatz, List<(int Menge, decimal Umsatz, decimal Kosten)> Punkte);
public sealed record PlattformErgebnis(string Name, decimal Brutto, decimal Gebuehren, decimal UstAbfuehren, decimal Auszahlung, decimal Selbstkosten, decimal GewinnJeStueck, decimal Marge, decimal GewinnMonat);
public sealed record StaffelErgebnis(int AbMenge, decimal Rabatt, decimal Netto, decimal Brutto, decimal GewinnJeStueck, decimal Marge, bool UnterSelbstkosten);

public static class PreisKit
{
    const string Stand = "16.11.2026";
    /// <summary>Beispielwerte zum Start – jede Plattform ändert ihre Gebühren; vor dem Rechnen auf der Gebührenseite des Anbieters prüfen und hier anpassen.</summary>
    public static readonly Plattform[] StandardPlattformen =
    [
        new() { Id = "etsy", Name = "Etsy", Prozent = 10.5m, JeVerkauf = 0.30m, JeAngebot = 0.18m, Quelle = "Etsy-Gebührenseite (Transaktion 6,5 % + Zahlung DE 4 % + 0,30 €; Einstellgebühr 0,20 US-$ ≈ 0,18 €) – bitte prüfen", Stand = Stand },
        new() { Id = "amazon", Name = "Amazon (Professional)", Prozent = 15m, Mindest = 0.30m, Monatlich = 39m, Quelle = "Amazon Seller Central (Verkaufsgebühr je nach Kategorie 7–15 %, Tarif Professional 39 € netto/Monat) – bitte prüfen", Stand = Stand },
        new() { Id = "shop", Name = "Eigener Shop (Zahlungsanbieter)", Prozent = 2.49m, JeVerkauf = 0.35m, Monatlich = 0m, Quelle = "Beispiel: übliche Händlergebühr eines Zahlungsanbieters – mit deinem Vertrag abgleichen", Stand = Stand },
        new() { Id = "markt", Name = "Markt / direkt (ohne Gebühr)", Quelle = "keine Gebühr", Stand = Stand },
    ];

    static decimal R(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
    static void NotNegative(Kalkulation k)
    {
        if (new[] { k.Material, k.MaterialGk, k.Stunden, k.Stundensatz, k.FertigungGk, k.VerwaltungGk, k.Ust }.Any(x => x < 0)) throw new ArgumentException("Kosten, Stunden und Zuschläge dürfen nicht negativ sein.");
        if (k.Gewinn <= -100) throw new ArgumentException("Der Gewinnzuschlag muss größer als −100 % sein.");
    }

    /// <summary>Zuschlagskalkulation: Material + MGK, Lohn + FGK = Herstellkosten; + Verwaltung/Vertrieb = Selbstkosten; + Gewinn = Nettopreis; + USt = Bruttopreis.</summary>
    public static Kalkulationsschema Rechnen(Kalkulation k)
    {
        NotNegative(k);
        var mgk = R(k.Material * k.MaterialGk / 100m); var lohn = R(k.Stunden * k.Stundensatz); var fgk = R(lohn * k.FertigungGk / 100m);
        var hk = k.Material + mgk + lohn + fgk; var vv = R(hk * k.VerwaltungGk / 100m); var sk = hk + vv;
        var gew = R(sk * k.Gewinn / 100m); var net = sk + gew;
        var ust = k.Kleinunternehmer ? 0 : R(net * k.Ust / 100m);
        var direct = k.Material + lohn;
        return new(k.Material, mgk, lohn, fgk, hk, vv, sk, gew, net, ust, net + ust, direct > 0 ? R((net / direct - 1) * 100m) : 0, net > 0 ? R(gew / net * 100m) : 0);
    }

    /// <summary>Rückwärts vom Wunschpreis (brutto): Was bleibt als Gewinn? Wie hoch dürfen Selbstkosten bzw. Material höchstens sein, damit der eingestellte Gewinnzuschlag bleibt?</summary>
    public static Rueckwaerts Rueckwaerts(Kalkulation k, decimal wunschBrutto)
    {
        NotNegative(k);
        if (wunschBrutto <= 0) throw new ArgumentException("Bitte einen Wunschpreis über 0 € eingeben.");
        var net = k.Kleinunternehmer ? wunschBrutto : R(wunschBrutto / (1 + k.Ust / 100m));
        var s = Rechnen(k); var gew = net - s.Selbstkosten;
        var maxSk = R(net / (1 + k.Gewinn / 100m));
        // Selbstkosten = (M·(1+mgk) + Lohnteil)·(1+vv) → größtes Material
        var lohnTeil = s.Lohn + s.FertigungGk; var f = 1 + k.VerwaltungGk / 100m; var m = 1 + k.MaterialGk / 100m;
        var maxMat = R((maxSk / f - lohnTeil) / m);
        return new(net, s.Selbstkosten, gew, s.Selbstkosten > 0 ? R(gew / s.Selbstkosten * 100m) : 0, net > 0 ? R(gew / net * 100m) : 0, maxSk, Math.Max(0, maxMat), gew >= 0);
    }

    /// <summary>Stundensatz für Selbständige: nötiger Umsatz = (Wunschnetto + Vorsorge) vor Steuern + Betriebskosten, geteilt durch die bezahlten Stunden im Jahr
    /// (Arbeitstage = 52 × 5 − Urlaub − Feiertage − Krankheit − Fortbildung; Stunden je Tag = Wochenstunden / 5; davon Auslastung %).</summary>
    public static StundensatzErgebnis Stundensatz(StundensatzEingabe e)
    {
        if (e.Wochenstunden is <= 0 or > 80) throw new ArgumentException("Wochenstunden zwischen 1 und 80.");
        if (e.Auslastung is <= 0 or > 100) throw new ArgumentException("Auslastung zwischen 1 und 100 %.");
        if (e.SteuerProzent is < 0 or >= 100) throw new ArgumentException("Steueranteil zwischen 0 und 99 %.");
        if (new[] { e.Urlaub, e.Feiertage, e.Krankheit, e.Fortbildung }.Any(x => x < 0)) throw new ArgumentException("Tage dürfen nicht negativ sein.");
        var tage = 260 - e.Urlaub - e.Feiertage - e.Krankheit - e.Fortbildung;
        if (tage <= 0) throw new ArgumentException("Es bleiben keine Arbeitstage übrig.");
        var std = tage * e.Wochenstunden / 5m; var bez = Math.Round(std * e.Auslastung / 100m, 1);
        var gewinn = R((e.WunschNetto + e.Vorsorge) * 12 / (1 - e.SteuerProzent / 100m));
        var umsatz = gewinn + e.Kosten * 12;
        var satz = R(umsatz / bez);
        return new(tage, std, bez, gewinn, umsatz, satz, R(satz * e.Wochenstunden / 5m * e.Auslastung / 100m));
    }

    /// <summary>Aufschlag (auf den Einkauf) in Marge (am Verkaufspreis) umrechnen und zurück: Marge = A / (100 + A) · 100; Aufschlag = M / (100 − M) · 100.</summary>
    public static decimal MargeAusAufschlag(decimal aufschlag) => aufschlag <= -100 ? throw new ArgumentException("Aufschlag größer als −100 %.") : R(aufschlag / (100 + aufschlag) * 100);
    public static decimal AufschlagAusMarge(decimal marge) => marge >= 100 ? throw new ArgumentException("Die Marge muss unter 100 % liegen.") : R(marge / (100 - marge) * 100);

    /// <summary>Break-Even: Menge = Fixkosten / (Preis − variable Kosten), aufgerundet. Punkte für das Diagramm von 0 bis doppelte Menge.</summary>
    public static BreakEvenErgebnis BreakEven(decimal fix, decimal preisNetto, decimal variabel, int punkte = 11)
    {
        if (fix < 0 || variabel < 0 || preisNetto <= 0) throw new ArgumentException("Fixkosten und variable Kosten ab 0, Preis über 0.");
        var db = preisNetto - variabel;
        if (db <= 0) throw new InvalidOperationException("Der Preis deckt nicht einmal die Kosten je Stück – so gibt es keinen Break-Even.");
        var n = (int)Math.Ceiling(fix / db);
        var max = Math.Max(10, n * 2); var pts = new List<(int, decimal, decimal)>();
        for (var i = 0; i < punkte; i++) { var q = (int)Math.Round((double)max * i / (punkte - 1)); pts.Add((q, R(q * preisNetto), R(fix + q * variabel))); }
        return new(db, n, R(n * preisNetto), pts);
    }

    /// <summary>Was bleibt auf einer Plattform je Verkauf: Gebühren (Prozent vom Bruttopreis inkl. Versand + fest + Einstellgebühr + Grundgebühr/Verkäufe, mindestens Mindestgebühr),
    /// abzuführende Umsatzsteuer, Auszahlung, Gewinn nach Selbstkosten und Versand.</summary>
    public static PlattformErgebnis AufPlattform(Plattform p, decimal brutto, decimal versand, decimal selbstkosten, decimal ust, bool klein, int verkaeufeMonat)
    {
        if (brutto <= 0) throw new ArgumentException("Preis über 0 €.");
        if (verkaeufeMonat <= 0) throw new ArgumentException("Verkäufe im Monat mindestens 1.");
        var basis = brutto + versand;
        var fee = Math.Max(p.Mindest, R(basis * p.Prozent / 100m)) + p.JeVerkauf + p.JeAngebot + R(p.Monatlich / verkaeufeMonat);
        var tax = klein ? 0 : R(basis - basis / (1 + ust / 100m));
        var payout = basis - fee;
        var gewinn = payout - tax - selbstkosten - versand;
        var netto = basis - tax;
        return new(p.Name, basis, fee, tax, payout, selbstkosten, gewinn, netto > 0 ? R(gewinn / netto * 100m) : 0, gewinn * verkaeufeMonat);
    }

    /// <summary>Preisstaffeln: Nettopreis je Stufe mit Rabatt, Gewinn je Stück, Warnung unter Selbstkosten.</summary>
    public static List<StaffelErgebnis> Staffeln(Kalkulation k, IEnumerable<Staffel> st)
    {
        var s = Rechnen(k); var res = new List<StaffelErgebnis>();
        foreach (var x in st.OrderBy(x => x.AbMenge))
        {
            if (x.AbMenge < 1 || x.Rabatt is < 0 or >= 100) throw new ArgumentException("Staffel: ab 1 Stück, Rabatt 0 bis 99 %.");
            var net = R(s.Netto * (1 - x.Rabatt / 100m)); var gross = k.Kleinunternehmer ? net : R(net * (1 + k.Ust / 100m)); var g = net - s.Selbstkosten;
            res.Add(new(x.AbMenge, x.Rabatt, net, gross, g, net > 0 ? R(g / net * 100m) : 0, g < 0));
        }
        return res;
    }

    /// <summary>Kalkulation als Artikel für „Rechnungen &amp; Angebote“ (Nettopreis, Einheit, Steuersatz).</summary>
    public static Artikel AlsArtikel(Kalkulation k, string nummer = "") => new()
    {
        Id = BizJson.NewId("a"), Nummer = nummer, Name = k.Name.Trim().Length > 0 ? k.Name.Trim() : "Artikel", Einheit = k.Einheit, Preis = Rechnen(k).Netto, Ust = k.Kleinunternehmer ? 0 : k.Ust,
    };

    public static string Bericht(Preisblatt b)
    {
        var k = b.Kalkulation; var s = Rechnen(k); var sb = new StringBuilder();
        sb.Append($"Kalkulation: {k.Name}\n");
        sb.Append($"Material {BizJson.Euro(s.Material)} + Materialgemeinkosten {BizJson.Num(k.MaterialGk)} % {BizJson.Euro(s.MaterialGk)}\n");
        sb.Append($"Lohn {BizJson.Num(k.Stunden)} Std. × {BizJson.Euro(k.Stundensatz)} = {BizJson.Euro(s.Lohn)} + Fertigungsgemeinkosten {BizJson.Num(k.FertigungGk)} % {BizJson.Euro(s.FertigungGk)}\n");
        sb.Append($"= Herstellkosten {BizJson.Euro(s.Herstellkosten)}\n+ Verwaltung/Vertrieb {BizJson.Num(k.VerwaltungGk)} % {BizJson.Euro(s.VerwaltungGk)}\n= Selbstkosten {BizJson.Euro(s.Selbstkosten)}\n");
        sb.Append($"+ Gewinn {BizJson.Num(k.Gewinn)} % {BizJson.Euro(s.Gewinn)}\n= Nettopreis {BizJson.Euro(s.Netto)}\n");
        sb.Append(k.Kleinunternehmer ? "Keine Umsatzsteuer (§ 19 UStG)\n" : $"+ USt {BizJson.Num(k.Ust)} % {BizJson.Euro(s.Ust)}\n");
        sb.Append($"= Bruttopreis {BizJson.Euro(s.Brutto)}\nAufschlag auf Material und Lohn {BizJson.Num(s.Aufschlag)} %, Marge {BizJson.Num(s.Marge)} %\n");
        return sb.ToString();
    }

    public static string Csv(Preisblatt b)
    {
        var de = CultureInfo.GetCultureInfo("de-DE"); var s = Rechnen(b.Kalkulation);
        var sb = new StringBuilder("Posten;Betrag\r\n");
        foreach (var (n, v) in new[] { ("Material", s.Material), ("Materialgemeinkosten", s.MaterialGk), ("Lohn", s.Lohn), ("Fertigungsgemeinkosten", s.FertigungGk), ("Herstellkosten", s.Herstellkosten),
            ("Verwaltung/Vertrieb", s.VerwaltungGk), ("Selbstkosten", s.Selbstkosten), ("Gewinn", s.Gewinn), ("Nettopreis", s.Netto), ("Umsatzsteuer", s.Ust), ("Bruttopreis", s.Brutto) })
            sb.Append(BizJson.Csv(n)).Append(';').Append(v.ToString("0.00", de)).Append("\r\n");
        sb.Append("\r\nStaffel ab;Rabatt %;Netto;Brutto;Gewinn je Stück\r\n");
        foreach (var x in Staffeln(b.Kalkulation, b.Staffeln)) sb.Append($"{x.AbMenge};{x.Rabatt.ToString("0.##", de)};{x.Netto.ToString("0.00", de)};{x.Brutto.ToString("0.00", de)};{x.GewinnJeStueck.ToString("0.00", de)}\r\n");
        return sb.ToString();
    }

    public static Preisblatt Beispiel() => new()
    {
        Kalkulation = new() { Name = "Holzschneidebrett Eiche", Einheit = "Stück", Material = 12m, MaterialGk = 10, Stunden = 1.5m, Stundensatz = 45m, FertigungGk = 20, VerwaltungGk = 15, Gewinn = 20, Ust = 19 },
        Wunschpreis = 129m, Fixkosten = 1500m, Versand = 4.90m, VerkaeufeMonat = 30, Plattform = "etsy",
    };
}
