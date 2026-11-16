using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Trendo.Core.Office;

namespace Trendo.Core.Business;

// ===================== STUDIO-MARKET-01 Marktanalyse (T-0677) =====================
// Clean-Room nach dem Bedienkonzept von Markt-Dashboards und Strategie-Werkzeugen (Statista-Dashboards, SWOT-/Canvas-Werkzeuge, Google Trends als Idee):
// Wettbewerber-Tabelle, SWOT, Personas, Marktgröße TAM/SAM/SOM mit nachvollziehbarer Rechnung, Preis-Positionierungskarte, Quelle je Zahl.
// Keine erfundenen Zahlen: jede Zahl hat eine Quelle oder ist ausdrücklich eine eigene Annahme – sonst wird sie als „Quelle fehlt“ markiert.

public sealed class MarktQuelle
{
    public string Id { get; set; } = "";
    public string Titel { get; set; } = "";
    public string Herausgeber { get; set; } = "";
    public string Url { get; set; } = "";
    /// <summary>Abgerufen am (JJJJ-MM-TT) bzw. Erscheinungsdatum.</summary>
    public string Datum { get; set; } = "";
    /// <summary>Statistik, Studie, Webseite, Eigene Erhebung, KI-Vorschlag.</summary>
    public string Art { get; set; } = "Webseite";
    /// <summary>KI-Vorschläge gelten erst als Quelle, wenn die Person sie geöffnet und bestätigt hat.</summary>
    public bool Geprueft { get; set; } = true;
}

/// <summary>Eine Zahl mit Herkunft: Quelle (Id) oder ausdrücklich eigene Annahme.</summary>
public sealed class Kennzahl
{
    public double? Wert { get; set; }
    public string QuelleId { get; set; } = "";
    public bool Annahme { get; set; }
    public string Notiz { get; set; } = "";
    public Kennzahl() { }
    public Kennzahl(double? wert, string quelle = "", bool annahme = false) { Wert = wert; QuelleId = quelle; Annahme = annahme; }
}

public sealed class Wettbewerber
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public Kennzahl Preis { get; set; } = new();
    public string Angebot { get; set; } = "";
    public string Staerken { get; set; } = "";
    public string Schwaechen { get; set; } = "";
    /// <summary>Leistung/Qualität 1–10 – eigene Einschätzung (für die Positionierungskarte).</summary>
    public int Leistung { get; set; } = 5;
    public string Notiz { get; set; } = "";
}

public sealed class SwotPunkt
{
    public string Text { get; set; } = "";
    public string QuelleId { get; set; } = "";
}

public sealed class Swot
{
    public List<SwotPunkt> Staerken { get; set; } = [];
    public List<SwotPunkt> Schwaechen { get; set; } = [];
    public List<SwotPunkt> Chancen { get; set; } = [];
    public List<SwotPunkt> Risiken { get; set; } = [];
    public List<SwotPunkt> Feld(string id) => id switch { "staerken" => Staerken, "schwaechen" => Schwaechen, "chancen" => Chancen, "risiken" => Risiken, _ => throw new ArgumentException("SWOT-Feld: staerken, schwaechen, chancen oder risiken.") };
}

public sealed class Persona
{
    public string Id { get; set; } = "";
    /// <summary>Rollenname, keine echte Person (z. B. „Die Gelegenheitskäuferin“).</summary>
    public string Name { get; set; } = "";
    public string Alter { get; set; } = "";
    public string Lebenslage { get; set; } = "";
    public string Ziele { get; set; } = "";
    public string Probleme { get; set; } = "";
    public string Kanaele { get; set; } = "";
    public string Zahlungsbereitschaft { get; set; } = "";
    /// <summary>Worauf die Persona beruht (Interviews, Umfrage, Annahme).</summary>
    public string Grundlage { get; set; } = "Annahme";
    public string QuelleId { get; set; } = "";
}

public sealed class Marktgroesse
{
    /// <summary>Anzahl möglicher Kundinnen und Kunden im Gesamtmarkt.</summary>
    public Kennzahl Kunden { get; set; } = new();
    /// <summary>Umsatz je Kunde und Jahr (Preis × Käufe pro Jahr).</summary>
    public Kennzahl UmsatzJeKunde { get; set; } = new();
    /// <summary>Anteil, den du mit deinem Angebot und Vertrieb überhaupt erreichen kannst (%).</summary>
    public Kennzahl SamAnteil { get; set; } = new();
    /// <summary>Realistisch erreichbarer Marktanteil in 3 Jahren (%).</summary>
    public Kennzahl SomAnteil { get; set; } = new();
}

public sealed class Marktmappe
{
    public string Thema { get; set; } = "";
    public string Region { get; set; } = "Deutschland";
    public string Waehrung { get; set; } = "€";
    public double EigenerPreis { get; set; }
    public int EigeneLeistung { get; set; } = 6;
    public string EigenerName { get; set; } = "Unser Angebot";
    public List<MarktQuelle> Quellen { get; set; } = [];
    public List<Wettbewerber> Wettbewerber { get; set; } = [];
    public Swot Swot { get; set; } = new();
    public List<Persona> Personas { get; set; } = [];
    public Marktgroesse Groesse { get; set; } = new();
    static readonly JsonSerializerOptions O = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public JsonObject ToJson() => JsonSerializer.SerializeToNode(this, O)!.AsObject();
    public static Marktmappe FromJson(JsonNode? n)
    {
        if (n is not JsonObject o) return new();
        try { return o.Deserialize<Marktmappe>(O) ?? new(); } catch (JsonException) { return new(); }
    }
}

public sealed record GroessenRechnung(double? Tam, double? Sam, double? Som, IReadOnlyList<string> Zeilen, IReadOnlyList<string> Fehlt);
public sealed record Kartenpunkt(string Name, double Preis, int Leistung, double X, double Y, bool Eigen);
public sealed record Positionierung(IReadOnlyList<Kartenpunkt> Punkte, double PreisMin, double PreisMax, IReadOnlyList<string> Luecken, string Lage);
public sealed record QuellenBefund(string Wo, string Text, bool Fehler);

public static partial class Marktanalyse
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public static readonly string[] Arten = ["Statistik", "Studie", "Webseite", "Eigene Erhebung", "KI-Vorschlag"];
    public static readonly (string Id, string Name)[] SwotFelder = [("staerken", "Stärken"), ("schwaechen", "Schwächen"), ("chancen", "Chancen"), ("risiken", "Risiken")];

    public static string Zahl(double v, int stellen = 0) => v.ToString(stellen == 0 ? "#,##0" : "#,##0." + new string('0', stellen), De);

    /// <summary>Nummer der Quelle in der Liste („[Q2]“), leer wenn es sie nicht gibt.</summary>
    public static string Marke(Marktmappe m, string quelleId)
    {
        var i = m.Quellen.FindIndex(q => q.Id == quelleId);
        return i < 0 ? "" : $"[Q{i + 1}]";
    }

    /// <summary>Herkunft einer Zahl in Worten: „[Q1]“, „eigene Annahme“, „[Q2, ungeprüft]“ oder „⚠ Quelle fehlt“.</summary>
    public static string Herkunft(Marktmappe m, Kennzahl k)
    {
        if (k.QuelleId.Length > 0 && m.Quellen.FirstOrDefault(q => q.Id == k.QuelleId) is { } q) return q.Geprueft ? Marke(m, q.Id) : Marke(m, q.Id).TrimEnd(']') + ", ungeprüft]";
        if (k.Annahme) return "eigene Annahme";
        return "⚠ Quelle fehlt";
    }

    public static bool Belegt(Marktmappe m, Kennzahl k) => k.Annahme || (k.QuelleId.Length > 0 && m.Quellen.Any(q => q.Id == k.QuelleId && q.Geprueft));

    // ---------- Marktgröße ----------

    /// <summary>TAM = Kunden × Umsatz je Kunde; SAM = TAM × erreichbarer Anteil; SOM = SAM × realistischer Marktanteil. Jede Zeile nennt die Herkunft.</summary>
    public static GroessenRechnung Groesse(Marktmappe m)
    {
        var g = m.Groesse; var z = new List<string>(); var fehlt = new List<string>(); var w = m.Waehrung;
        void Need(Kennzahl k, string name) { if (k.Wert is null) fehlt.Add(name + ": Zahl fehlt"); else if (!Belegt(m, k)) fehlt.Add(name + ": Quelle fehlt (oder als eigene Annahme kennzeichnen)"); }
        Need(g.Kunden, "Kunden im Gesamtmarkt"); Need(g.UmsatzJeKunde, "Umsatz je Kunde und Jahr"); Need(g.SamAnteil, "Erreichbarer Anteil (SAM)"); Need(g.SomAnteil, "Realistischer Marktanteil (SOM)");
        double? tam = null, sam = null, som = null;
        if (g.Kunden.Wert is { } k && g.UmsatzJeKunde.Wert is { } u)
        {
            tam = k * u;
            z.Add($"TAM (Gesamtmarkt) = {Zahl(k)} Kunden {Herkunft(m, g.Kunden)} × {Zahl(u, 2)} {w} je Kunde und Jahr {Herkunft(m, g.UmsatzJeKunde)} = {Zahl(tam.Value)} {w} im Jahr");
        }
        else z.Add("TAM (Gesamtmarkt) = Kunden × Umsatz je Kunde – noch nicht berechenbar.");
        if (tam is { } t && g.SamAnteil.Wert is { } sa)
        {
            sam = t * Math.Clamp(sa, 0, 100) / 100;
            z.Add($"SAM (erreichbarer Markt) = {Zahl(t)} {w} × {Zahl(sa, 1)} % {Herkunft(m, g.SamAnteil)} = {Zahl(sam.Value)} {w} im Jahr");
        }
        else z.Add("SAM (erreichbarer Markt) = TAM × erreichbarer Anteil – noch nicht berechenbar.");
        if (sam is { } s && g.SomAnteil.Wert is { } so)
        {
            som = s * Math.Clamp(so, 0, 100) / 100;
            z.Add($"SOM (realistisch erreichbar) = {Zahl(s)} {w} × {Zahl(so, 1)} % {Herkunft(m, g.SomAnteil)} = {Zahl(som.Value)} {w} im Jahr");
            if (m.EigenerPreis > 0) z.Add($"Das sind etwa {Zahl(som.Value / m.EigenerPreis)} Verkäufe zu {Zahl(m.EigenerPreis, 2)} {w} im Jahr.");
        }
        else z.Add("SOM (realistisch erreichbar) = SAM × Marktanteil – noch nicht berechenbar.");
        if (g.SomAnteil.Wert is > 10) fehlt.Add("Marktanteil über 10 % ist für einen Neuling sehr ehrgeizig – Annahme begründen.");
        return new(tam, sam, som, z, fehlt);
    }

    // ---------- Positionierung ----------

    /// <summary>Preis-Leistungs-Karte: x = Preis (links günstig), y = Leistung 1–10 (eigene Einschätzung). Lücken = Viertel ohne Anbieter.</summary>
    public static Positionierung Karte(Marktmappe m)
    {
        var pts = m.Wettbewerber.Where(c => c.Preis.Wert is > 0).Select(c => (c.Name, P: c.Preis.Wert!.Value, L: Math.Clamp(c.Leistung, 1, 10), Eigen: false)).ToList();
        if (m.EigenerPreis > 0) pts.Add((m.EigenerName.Length > 0 ? m.EigenerName : "Unser Angebot", m.EigenerPreis, Math.Clamp(m.EigeneLeistung, 1, 10), true));
        if (pts.Count == 0) return new([], 0, 0, [], "Noch keine Preise – bei den Wettbewerbern Preise mit Quelle eintragen.");
        double lo = pts.Min(p => p.P), hi = pts.Max(p => p.P); if (hi - lo < 1e-9) { lo *= 0.8; hi = hi * 1.2 + 1; }
        var pad = (hi - lo) * 0.08; lo = Math.Max(0, lo - pad); hi += pad;
        var list = pts.Select(p => new Kartenpunkt(p.Name, p.P, p.L, (p.P - lo) / (hi - lo), (p.L - 1) / 9.0, p.Eigen)).ToList();
        var mid = (lo + hi) / 2; var luecken = new List<string>();
        foreach (var (name, left, top) in new[] { ("günstig und stark", true, true), ("teuer und stark", false, true), ("günstig und einfach", true, false), ("teuer und einfach", false, false) })
            if (!list.Any(p => !p.Eigen && (p.Preis < mid) == left && (p.Leistung >= 5.5) == top)) luecken.Add(name);
        var comps = m.Wettbewerber.Where(c => c.Preis.Wert is > 0).Select(c => new Competitor(c.Name, c.Url, c.Preis.Wert!.Value, c.Staerken, c.Schwaechen, c.Angebot)).ToList();
        var lage = BusinessMarket.Prices(comps, m.EigenerPreis) is { } pp ? $"Preise der Wettbewerber {Zahl(pp.Min, 2)} – {Zahl(pp.Max, 2)} {m.Waehrung} (Mitte {Zahl(pp.Median, 2)} {m.Waehrung}). {pp.Where}" : "Noch keine Wettbewerberpreise.";
        return new(list, lo, hi, luecken.Where(l => l != "teuer und einfach").ToList(), lage);
    }

    static string E(string s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>Positionierungskarte als SVG (für Bericht und Webseite).</summary>
    public static string KarteSvg(Marktmappe m, int w = 800, int h = 560)
    {
        var k = Karte(m); var sb = new StringBuilder(); const int L = 70, R = 30, T = 30, B = 60; double pw = w - L - R, ph = h - T - B;
        sb.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w}\" height=\"{h}\" viewBox=\"0 0 {w} {h}\" font-family=\"Segoe UI, Arial, sans-serif\">\n<rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{L}\" y=\"{T}\" width=\"{pw}\" height=\"{ph}\" fill=\"#F8FAFC\" stroke=\"#CBD5E1\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{L + pw / 2}\" y1=\"{T}\" x2=\"{L + pw / 2}\" y2=\"{T + ph}\" stroke=\"#CBD5E1\" stroke-dasharray=\"4 4\"/><line x1=\"{L}\" y1=\"{T + ph / 2}\" x2=\"{L + pw}\" y2=\"{T + ph / 2}\" stroke=\"#CBD5E1\" stroke-dasharray=\"4 4\"/>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{L + pw / 2}\" y=\"{h - 18}\" text-anchor=\"middle\" font-size=\"14\" fill=\"#334155\">Preis ({E(m.Waehrung)}) → {Zahl(k.PreisMin, 2)} … {Zahl(k.PreisMax, 2)}</text>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"20\" y=\"{T + ph / 2}\" text-anchor=\"middle\" font-size=\"14\" fill=\"#334155\" transform=\"rotate(-90 20 {T + ph / 2})\">Leistung (eigene Einschätzung) →</text>\n");
        foreach (var p in k.Punkte)
        {
            double x = L + p.X * pw, y = T + (1 - p.Y) * ph;
            sb.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{x:0.#}\" cy=\"{y:0.#}\" r=\"{(p.Eigen ? 9 : 7)}\" fill=\"{(p.Eigen ? "#C9A227" : "#2563EB")}\" stroke=\"#FFFFFF\" stroke-width=\"2\"/>\n");
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{x + 12:0.#}\" y=\"{y + 4:0.#}\" font-size=\"13\" fill=\"#0F172A\"{(p.Eigen ? " font-weight=\"bold\"" : "")}>{E(p.Name)}</text>\n");
        }
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    // ---------- Quellen-Prüfung ----------

    [GeneratedRegex(@"\d+([.,]\d+)?\s*(%|€|eur|euro|mio|millionen|mrd|milliarden|tsd|prozent)", RegexOptions.IgnoreCase)]
    private static partial Regex NumberClaimRx();

    /// <summary>Jede Zahl braucht eine Herkunft. Meldet fehlende Quellen, ungeprüfte KI-Vorschläge und alte Quellen.</summary>
    public static List<QuellenBefund> Pruefen(Marktmappe m, DateTime heute)
    {
        var f = new List<QuellenBefund>();
        foreach (var c in m.Wettbewerber)
            if (c.Preis.Wert is not null && !Belegt(m, c.Preis)) f.Add(new($"Wettbewerber „{c.Name}“", "Preis ohne Quelle – Preisliste/Webseite als Quelle eintragen.", true));
        foreach (var x in Groesse(m).Fehlt) f.Add(new("Marktgröße", x, !x.StartsWith("Marktanteil über", StringComparison.Ordinal)));
        foreach (var (id, name) in SwotFelder)
            foreach (var p in m.Swot.Feld(id))
                if (NumberClaimRx().IsMatch(p.Text) && (p.QuelleId.Length == 0 || m.Quellen.All(q => q.Id != p.QuelleId))) f.Add(new($"SWOT {name}", $"„{Cut(p.Text, 50)}“ nennt eine Zahl ohne Quelle.", true));
        foreach (var p in m.Personas)
            if (p.Grundlage.Trim().Length == 0 || p.Grundlage.Equals("Annahme", StringComparison.OrdinalIgnoreCase)) f.Add(new($"Persona „{p.Name}“", "beruht auf Annahmen – mit Gesprächen oder einer Umfrage bestätigen.", false));
        for (var i = 0; i < m.Quellen.Count; i++)
        {
            var q = m.Quellen[i];
            if (!q.Geprueft) f.Add(new($"Quelle Q{i + 1}", $"„{Cut(q.Titel, 50)}“ ist ein KI-Vorschlag – Seite öffnen, prüfen und bestätigen.", true));
            if (q.Url.Trim().Length == 0 && q.Herausgeber.Trim().Length == 0) f.Add(new($"Quelle Q{i + 1}", "ohne Adresse und ohne Herausgeber – nicht nachprüfbar.", true));
            if (DateTime.TryParseExact(q.Datum, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && d < heute.AddYears(-3)) f.Add(new($"Quelle Q{i + 1}", $"ist von {d:yyyy} – älter als drei Jahre.", false));
        }
        return f;
    }

    static string Cut(string s, int n) => (s ?? "").Length <= n ? s ?? "" : s![..n].TrimEnd() + "…";

    // ---------- Recherche-Wege ----------

    /// <summary>Gelesene Seiten aus „Sicher recherchieren“ als Quellen (Webseite, Abrufdatum). Bekannte Adressen werden übersprungen.</summary>
    public static int AusRecherche(Marktmappe m, IEnumerable<(string Titel, string Url, DateTimeOffset Am)> seiten)
    {
        var known = m.Quellen.Select(q => q.Url.Trim().TrimEnd('/').ToLowerInvariant()).ToHashSet(); var n = 0;
        foreach (var (titel, url, am) in seiten)
        {
            var key = url.Trim().TrimEnd('/').ToLowerInvariant(); if (key.Length == 0 || !known.Add(key)) continue;
            var host = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host.Replace("www.", "") : "";
            m.Quellen.Add(new MarktQuelle { Id = NeueId("q"), Titel = titel.Length > 0 ? titel : host, Herausgeber = host, Url = url, Datum = am.LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Art = "Webseite" });
            n++;
        }
        return n;
    }

    /// <summary>Mitbewerber aus der Seite „Markt“ (Geschäftsidee) übernehmen – Preise ohne Quelle werden markiert, nicht verworfen.</summary>
    public static int AusGeschaeftsidee(Marktmappe m, IEnumerable<Competitor> list)
    {
        var have = m.Wettbewerber.Select(c => c.Name.Trim().ToLowerInvariant()).ToHashSet(); var n = 0;
        foreach (var c in list)
        {
            if (!have.Add(c.Name.Trim().ToLowerInvariant())) continue;
            m.Wettbewerber.Add(new Wettbewerber { Id = NeueId("w"), Name = c.Name, Url = c.Url, Preis = new Kennzahl(c.Price > 0 ? c.Price : null), Angebot = c.Description, Staerken = c.Strengths, Schwaechen = c.Weaknesses });
            n++;
        }
        return n;
    }

    public static string NeueId(string p) => p + Guid.NewGuid().ToString("N")[..8];

    /// <summary>Auftrag an die KI: Wettbewerber recherchieren – streng im Zeilenformat und nur mit Quelladresse je Preis.</summary>
    public static string RecherchePrompt(Marktmappe m) =>
        $"Recherchiere bis zu 8 Wettbewerber für: „{m.Thema}“ in {m.Region}. Antworte NUR mit Zeilen im Format\n" +
        "Name | Webseite | Preis in Euro (nur Zahl, leer wenn unbekannt) | Adresse der Seite, auf der der Preis steht | Angebot in wenigen Wörtern\n" +
        "Erfinde nichts: Wenn du einen Preis nicht mit einer echten Adresse belegen kannst, lass Preis und Adresse leer. Keine Marktgrößen schätzen.";

    /// <summary>KI-Antwort einlesen: Wettbewerber und ihre Quellen kommen als „KI-Vorschlag, ungeprüft“ dazu. Ein Preis ohne Adresse wird nicht übernommen.</summary>
    public static (int Neu, int PreiseVerworfen) KiEinlesen(Marktmappe m, string antwort, DateTime heute)
    {
        int neu = 0, weg = 0; var have = m.Wettbewerber.Select(c => c.Name.Trim().ToLowerInvariant()).ToHashSet();
        foreach (var raw in (antwort ?? "").Replace("\r", "").Split('\n'))
        {
            var cols = raw.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToList();
            if (cols.Count < 2 || cols[0].Length == 0 || cols[0].StartsWith("Name", StringComparison.OrdinalIgnoreCase) || cols[0].All(ch => ch is '-' or ':' or ' ')) continue;
            if (!have.Add(cols[0].ToLowerInvariant())) continue;
            string Col(int i) => i < cols.Count ? cols[i] : "";
            var w = new Wettbewerber { Id = NeueId("w"), Name = Cut(cols[0], 80), Url = Web(Col(1)), Angebot = Cut(Col(4), 200), Notiz = "von der KI vorgeschlagen – prüfen" };
            var preisText = Regex.Replace(Col(2), @"[^\d,.]", "");
            var url = Web(Col(3));
            if (preisText.Length > 0 && double.TryParse(preisText.Replace(".", "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var p) && p > 0)
            {
                if (url.Length > 0)
                {
                    var q = new MarktQuelle { Id = NeueId("q"), Titel = $"Preisseite {w.Name}", Herausgeber = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : "", Url = url, Datum = heute.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Art = "KI-Vorschlag", Geprueft = false };
                    m.Quellen.Add(q); w.Preis = new Kennzahl(p, q.Id);
                }
                else weg++;
            }
            m.Wettbewerber.Add(w); neu++;
        }
        return (neu, weg);
    }

    static string Web(string s) => Uri.TryCreate((s ?? "").Trim(), UriKind.Absolute, out var u) && u.Scheme is "http" or "https" ? u.ToString() : "";

    // ---------- Ausgaben ----------

    static string Preis(Marktmappe m, Wettbewerber c) => c.Preis.Wert is { } p ? $"{Zahl(p, 2)} {m.Waehrung} {Herkunft(m, c.Preis)}" : "–";

    /// <summary>Bericht als Markdown: Überblick, Wettbewerber, Positionierung, SWOT, Personas, Marktgröße mit Rechnung, offene Punkte, Quellenverzeichnis.</summary>
    public static string Markdown(Marktmappe m, DateTime heute, string? karteDatei = null)
    {
        var sb = new StringBuilder(); var g = Groesse(m); var k = Karte(m);
        sb.Append("# Marktanalyse: ").Append(m.Thema.Length > 0 ? m.Thema : "ohne Thema").Append("\n\n");
        sb.Append("Region: ").Append(m.Region).Append(" · Stand: ").Append(heute.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)).Append("\n\n");
        sb.Append("## Wettbewerber\n\n| Name | Preis | Angebot | Stärken | Schwächen | Webseite |\n|---|---|---|---|---|---|\n");
        foreach (var c in m.Wettbewerber) sb.Append($"| {Md(c.Name)} | {Md(Preis(m, c))} | {Md(c.Angebot)} | {Md(c.Staerken)} | {Md(c.Schwaechen)} | {Md(c.Url)} |\n");
        sb.Append("\n## Positionierung\n\n").Append(k.Lage).Append('\n');
        if (karteDatei is not null) sb.Append("\n![Preis-Leistungs-Karte](").Append(karteDatei).Append(")\n");
        if (k.Luecken.Count > 0) sb.Append("\nFreie Felder: ").Append(string.Join(", ", k.Luecken)).Append(" (Leistung = eigene Einschätzung).\n");
        sb.Append("\n## SWOT\n\n");
        foreach (var (id, name) in SwotFelder)
        {
            sb.Append("**").Append(name).Append("**\n\n");
            var l = m.Swot.Feld(id); if (l.Count == 0) sb.Append("- (noch leer)\n");
            foreach (var p in l) sb.Append("- ").Append(p.Text).Append(p.QuelleId.Length > 0 ? " " + Marke(m, p.QuelleId) : "").Append('\n');
            sb.Append('\n');
        }
        if (m.Personas.Count > 0)
        {
            sb.Append("## Zielgruppen (Personas)\n\n");
            foreach (var p in m.Personas)
                sb.Append("### ").Append(p.Name).Append("\n\n- Alter: ").Append(p.Alter).Append("\n- Lebenslage: ").Append(p.Lebenslage).Append("\n- Ziele: ").Append(p.Ziele)
                  .Append("\n- Probleme: ").Append(p.Probleme).Append("\n- Erreichbar über: ").Append(p.Kanaele).Append("\n- Zahlungsbereitschaft: ").Append(p.Zahlungsbereitschaft)
                  .Append("\n- Grundlage: ").Append(p.Grundlage).Append(p.QuelleId.Length > 0 ? " " + Marke(m, p.QuelleId) : "").Append("\n\n");
        }
        sb.Append("## Marktgröße (TAM, SAM, SOM)\n\n");
        foreach (var z in g.Zeilen) sb.Append("- ").Append(z).Append('\n');
        var pruef = Pruefen(m, heute);
        if (pruef.Count > 0) { sb.Append("\n## Offene Punkte\n\n"); foreach (var p in pruef) sb.Append("- ").Append(p.Fehler ? "⚠ " : "").Append(p.Wo).Append(": ").Append(p.Text).Append('\n'); }
        sb.Append("\n## Quellen\n\n");
        if (m.Quellen.Count == 0) sb.Append("Noch keine Quellen.\n");
        for (var i = 0; i < m.Quellen.Count; i++)
        {
            var q = m.Quellen[i];
            sb.Append($"[Q{i + 1}] ").Append(q.Herausgeber.Length > 0 ? q.Herausgeber + ": " : "").Append(q.Titel).Append(q.Url.Length > 0 ? " – " + q.Url : "")
              .Append(q.Datum.Length > 0 ? $" (abgerufen {q.Datum})" : "").Append(" · ").Append(q.Art).Append(q.Geprueft ? "" : " · ungeprüft").Append('\n');
        }
        sb.Append("\n_Zahlen ohne Quelle sind markiert. Leistung auf der Karte ist eine eigene Einschätzung._\n");
        return sb.ToString();
    }

    static string Md(string s) => (s ?? "").Replace("|", "/").Replace("\n", " ");

    /// <summary>Bericht als Trendo-Text-Dokument (für den PDF-Export über <see cref="TextPdf"/>); <paramref name="karte"/> = PNG der Karte.</summary>
    public static TextDoc Dokument(Marktmappe m, DateTime heute, byte[]? karte = null)
    {
        var d = new TextDoc { Blocks = [], Title = "Marktanalyse " + m.Thema };
        d.Page.PageNumbers = true; d.Page.Footer = "Marktanalyse · Stand " + heute.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture); d.Page.Design = "modern";
        void Para(string t, ParaStyle s = ParaStyle.Text) => d.Blocks.Add(new Para(t, s));
        Para("Marktanalyse: " + (m.Thema.Length > 0 ? m.Thema : "ohne Thema"), ParaStyle.Title);
        Para($"Region {m.Region} · Stand {heute:dd.MM.yyyy}", ParaStyle.Subtitle);
        Para("Wettbewerber", ParaStyle.Heading1);
        var t = new Table { Rows = [["Name", "Preis", "Angebot", "Stärken", "Schwächen"]] };
        foreach (var c in m.Wettbewerber) t.Rows.Add([c.Name, Preis(m, c), c.Angebot, c.Staerken, c.Schwaechen]);
        d.Blocks.Add(t);
        var k = Karte(m);
        Para("Positionierung", ParaStyle.Heading1); Para(k.Lage);
        if (karte is { Length: > 0 }) d.Blocks.Add(new Picture { Data = karte, Ext = "png", WidthCm = 15, HeightCm = 10.5, Caption = "Preis-Leistungs-Karte (Leistung = eigene Einschätzung)" });
        if (k.Luecken.Count > 0) Para("Freie Felder: " + string.Join(", ", k.Luecken));
        Para("SWOT", ParaStyle.Heading1);
        foreach (var (id, name) in SwotFelder)
        {
            Para(name, ParaStyle.Heading2);
            foreach (var p in m.Swot.Feld(id)) d.Blocks.Add(new Para(p.Text + (p.QuelleId.Length > 0 ? " " + Marke(m, p.QuelleId) : "")) { List = ListKind.Bullet });
        }
        if (m.Personas.Count > 0)
        {
            Para("Zielgruppen", ParaStyle.Heading1);
            foreach (var p in m.Personas) { Para(p.Name, ParaStyle.Heading2); Para($"{p.Alter} · {p.Lebenslage}. Ziele: {p.Ziele}. Probleme: {p.Probleme}. Erreichbar über: {p.Kanaele}. Zahlt: {p.Zahlungsbereitschaft}. Grundlage: {p.Grundlage}."); }
        }
        Para("Marktgröße", ParaStyle.Heading1);
        foreach (var z in Groesse(m).Zeilen) d.Blocks.Add(new Para(z) { List = ListKind.Bullet });
        var pr = Pruefen(m, heute);
        if (pr.Count > 0) { Para("Offene Punkte", ParaStyle.Heading1); foreach (var p in pr) d.Blocks.Add(new Para((p.Fehler ? "⚠ " : "") + p.Wo + ": " + p.Text) { List = ListKind.Bullet }); }
        Para("Quellen", ParaStyle.Heading1);
        for (var i = 0; i < m.Quellen.Count; i++) { var q = m.Quellen[i]; Para($"[Q{i + 1}] {(q.Herausgeber.Length > 0 ? q.Herausgeber + ": " : "")}{q.Titel}{(q.Url.Length > 0 ? " – " + q.Url : "")}{(q.Datum.Length > 0 ? $" (abgerufen {q.Datum})" : "")} · {q.Art}{(q.Geprueft ? "" : " · ungeprüft")}"); }
        return d;
    }

    /// <summary>Gliederung für „Trendo Präsentation“ (eine Zeile = Folie, „  - “ = Punkt).</summary>
    public static string Gliederung(Marktmappe m)
    {
        var sb = new StringBuilder(); var g = Groesse(m); var k = Karte(m);
        sb.Append("Marktanalyse: ").Append(m.Thema.Length > 0 ? m.Thema : "ohne Thema").Append("\n  - ").Append(m.Region).Append('\n');
        sb.Append("Wettbewerber\n"); foreach (var c in m.Wettbewerber.Take(6)) sb.Append("  - ").Append(c.Name).Append(": ").Append(Preis(m, c)).Append('\n');
        sb.Append("Positionierung\n  - ").Append(k.Lage).Append('\n'); if (k.Luecken.Count > 0) sb.Append("  - Freie Felder: ").Append(string.Join(", ", k.Luecken)).Append('\n');
        foreach (var (id, name) in SwotFelder) { sb.Append("SWOT – ").Append(name).Append('\n'); var l = m.Swot.Feld(id); if (l.Count == 0) sb.Append("  - (noch leer)\n"); foreach (var p in l.Take(5)) sb.Append("  - ").Append(p.Text).Append(p.QuelleId.Length > 0 ? " " + Marke(m, p.QuelleId) : "").Append('\n'); }
        foreach (var p in m.Personas.Take(3)) sb.Append("Zielgruppe: ").Append(p.Name).Append("\n  - ").Append(p.Ziele).Append("\n  - Probleme: ").Append(p.Probleme).Append('\n');
        sb.Append("Marktgröße\n"); foreach (var z in g.Zeilen) sb.Append("  - ").Append(z).Append('\n');
        sb.Append("Quellen\n"); for (var i = 0; i < m.Quellen.Count && i < 8; i++) sb.Append($"  - [Q{i + 1}] ").Append(m.Quellen[i].Herausgeber.Length > 0 ? m.Quellen[i].Herausgeber : m.Quellen[i].Titel).Append('\n');
        return sb.ToString();
    }

    public static string Csv(Marktmappe m)
    {
        static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("Name;Preis;Preisquelle;Angebot;Stärken;Schwächen;Leistung (eigene Einschätzung);Webseite\n");
        foreach (var c in m.Wettbewerber)
            sb.Append(Q(c.Name)).Append(';').Append(c.Preis.Wert is { } p ? p.ToString("0.00", De) : "").Append(';').Append(Q(c.Preis.Wert is null ? "" : Herkunft(m, c.Preis))).Append(';')
              .Append(Q(c.Angebot)).Append(';').Append(Q(c.Staerken)).Append(';').Append(Q(c.Schwaechen)).Append(';').Append(c.Leistung).Append(';').Append(Q(c.Url)).Append('\n');
        return sb.ToString();
    }

    public static Marktmappe Beispiel(DateTime heute)
    {
        var q1 = new MarktQuelle { Id = "q1", Titel = "Haushalte in Deutschland (Beispielwert)", Herausgeber = "Statistikamt (Beispiel)", Url = "https://example.org/haushalte", Datum = heute.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Art = "Statistik" };
        var q2 = new MarktQuelle { Id = "q2", Titel = "Preisliste Anbieter A", Herausgeber = "example.com", Url = "https://example.com/preise", Datum = heute.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Art = "Webseite" };
        return new Marktmappe
        {
            Thema = "Kräuter-Abo für die Fensterbank", Region = "Deutschland", EigenerPreis = 14.9, EigeneLeistung = 8, EigenerName = "Unser Kräuter-Abo",
            Quellen = [q1, q2],
            Wettbewerber =
            [
                new() { Id = "w1", Name = "Anbieter A", Url = "https://example.com", Preis = new Kennzahl(19.9, "q2"), Angebot = "Kräuterbox monatlich", Staerken = "große Auswahl", Schwaechen = "teurer Versand", Leistung = 7 },
                new() { Id = "w2", Name = "Anbieter B", Url = "https://example.net", Preis = new Kennzahl(9.9), Angebot = "Samen-Set", Staerken = "günstig", Schwaechen = "keine Anleitung", Leistung = 4 },
            ],
            Swot = new Swot
            {
                Staerken = [new() { Text = "Frische Pflanzen statt Samen" }], Schwaechen = [new() { Text = "Kleines Team, wenig Bekanntheit" }],
                Chancen = [new() { Text = "Wachsendes Interesse an Selbstversorgung" }], Risiken = [new() { Text = "Versand lebender Pflanzen im Winter" }],
            },
            Personas = [new() { Id = "p1", Name = "Die Stadtgärtnerin", Alter = "25–40", Lebenslage = "Wohnung ohne Garten", Ziele = "frische Kräuter ohne Aufwand", Probleme = "Pflanzen gehen ein", Kanaele = "Instagram, Pinterest", Zahlungsbereitschaft = "10–20 € im Monat", Grundlage = "Annahme" }],
            Groesse = new Marktgroesse { Kunden = new Kennzahl(41_000_000, "q1"), UmsatzJeKunde = new Kennzahl(60, annahme: true), SamAnteil = new Kennzahl(5, annahme: true), SomAnteil = new Kennzahl(1, annahme: true) },
        };
    }
}
