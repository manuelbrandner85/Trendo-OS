using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Business;

// ===================== STUDIO-INVOICE-01 Rechnungen & Angebote (T-0675) =====================
// Clean-Room nach dem Bedienkonzept von sevDesk, lexoffice und Invoice Ninja (nur Vorbild, kein Code): Angebot → Auftragsbestätigung → Rechnung,
// Kunden- und Artikelstamm, Umsatzsteuer je Position (19/7/0 %, Kleinunternehmer § 19 UStG), lückenlose Nummernkreise, Pflichtangaben nach § 14 Abs. 4 UStG,
// PDF und XRechnung (vorhandener ERechnung-Kern mit ZUGFeRD-csharp), Status offen/bezahlt/überfällig. Nutzt dieselben Daten wie „Geld & Rechnungen“ (SPEC-11):
// Rechnungsnummern RE-JJJJ-NNN im selben Kreis, Zahlungseingänge im selben Haushaltsbuch (Ledger). Keine Steuerberatung.

/// <summary>JSON-Hilfe für die Business-Studios (camelCase, Fehler = leeres Objekt).</summary>
public static class BizJson
{
    static readonly JsonSerializerOptions O = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static JsonObject To<T>(T value) => JsonSerializer.SerializeToNode(value, O)!.AsObject();
    public static T From<T>(JsonNode? n) where T : new()
    {
        if (n is not JsonObject o) return new();
        try { return o.Deserialize<T>(O) ?? new(); } catch (JsonException) { return new(); }
    }
    public static T Clone<T>(T value) where T : new() => From<T>(To(value));

    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public static string Euro(decimal d) => d.ToString("N2", De) + " €";
    public static string Num(decimal d, string fmt = "0.##") => d.ToString(fmt, De);
    public static DateOnly? Datum(string? s) =>
        DateOnly.TryParseExact((s ?? "").Trim(), ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy"], De, DateTimeStyles.None, out var d) ? d : null;
    public static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string Text(DateOnly d) => d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    public static string TextOf(string iso) => Datum(iso) is { } d ? Text(d) : iso;
    /// <summary>Betrag aus deutscher oder englischer Schreibweise („1.234,56 €“, „1234.56“). Fehler = FormatException.</summary>
    public static decimal Betrag(string s) => Finance.ParseAmount(s);
    public static bool TryBetrag(string s, out decimal v)
    {
        v = 0; if (string.IsNullOrWhiteSpace(s)) return false;
        try { v = Finance.ParseAmount(s); return true; } catch (FormatException) { return false; } catch (OverflowException) { return false; }
    }
    public static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    public static string NewId(string p) => p + Guid.NewGuid().ToString("N")[..8];
}

public sealed class Firma
{
    public string Name { get; set; } = "";
    public string Strasse { get; set; } = "";
    public string PlzOrt { get; set; } = "";
    public string Email { get; set; } = "";
    public string Telefon { get; set; } = "";
    public string SteuerNr { get; set; } = "";
    public string UstId { get; set; } = "";
    public string Iban { get; set; } = "";
    public string Bank { get; set; } = "";
    public bool Kleinunternehmer { get; set; }
    public int Zahlungsziel { get; set; } = 14;
    public string Anschrift => string.Join("\n", new[] { Name, Strasse, PlzOrt }.Select(x => x.Trim()).Where(x => x.Length > 0));
}

public sealed class Kunde
{
    public string Id { get; set; } = "";
    public string Nummer { get; set; } = "";
    public string Name { get; set; } = "";
    public string Zusatz { get; set; } = "";
    public string Strasse { get; set; } = "";
    public string PlzOrt { get; set; } = "";
    public string Email { get; set; } = "";
    public string UstId { get; set; } = "";
    /// <summary>Unternehmer (B2B) oder Verbraucher – wichtig für Verzugszinsen und 40-€-Pauschale im Mahnwesen.</summary>
    public bool Unternehmer { get; set; } = true;
    /// <summary>Käuferreferenz für die E-Rechnung (bei Behörden die Leitweg-ID).</summary>
    public string Referenz { get; set; } = "";
    public string Anschrift => string.Join("\n", new[] { Name, Zusatz, Strasse, PlzOrt }.Select(x => x.Trim()).Where(x => x.Length > 0));
}

public sealed class Artikel
{
    public string Id { get; set; } = "";
    public string Nummer { get; set; } = "";
    public string Name { get; set; } = "";
    public string Einheit { get; set; } = "Stück";
    public decimal Preis { get; set; }
    public decimal Ust { get; set; } = 19;
}

public sealed class Position
{
    public string Text { get; set; } = "";
    public decimal Menge { get; set; } = 1;
    public string Einheit { get; set; } = "Stück";
    /// <summary>Einzelpreis netto.</summary>
    public decimal Preis { get; set; }
    /// <summary>Rabatt in Prozent auf die Position.</summary>
    public decimal Rabatt { get; set; }
    public decimal Ust { get; set; } = 19;
}

/// <summary>Ein Beleg der Kette: Angebot, Auftragsbestätigung, Rechnung oder Stornorechnung. Ohne Nummer = Entwurf (änderbar).</summary>
public sealed class Vorgang
{
    public string Id { get; set; } = "";
    /// <summary>angebot, auftrag, rechnung oder storno.</summary>
    public string Art { get; set; } = "rechnung";
    public string Nummer { get; set; } = "";
    public string Datum { get; set; } = "";
    /// <summary>Leistungsdatum oder -zeitraum (Pflichtangabe § 14 Abs. 4 Nr. 6 UStG), z. B. „03.11.2026“ oder „November 2026“.</summary>
    public string Leistung { get; set; } = "";
    public string KundeId { get; set; } = "";
    /// <summary>Anschrift wie beim Festschreiben (bleibt, auch wenn der Kundenstamm sich ändert).</summary>
    public string KundeAnschrift { get; set; } = "";
    public string KundeNummer { get; set; } = "";
    public bool KundeUnternehmer { get; set; } = true;
    public List<Position> Positionen { get; set; } = [];
    public string Kopftext { get; set; } = "";
    public string Fusstext { get; set; } = "";
    public int ZahlungszielTage { get; set; } = 14;
    /// <summary>Angebot: gültig so viele Tage.</summary>
    public int GueltigTage { get; set; } = 30;
    /// <summary>Hinweis bei 0 % ohne Kleinunternehmer (z. B. Steuerbefreiung) – Pflicht nach § 14 Abs. 4 Nr. 8 UStG.</summary>
    public string Befreiung { get; set; } = "";
    public bool Kleinunternehmer { get; set; }
    public string BezahltAm { get; set; } = "";
    public decimal Bezahlt { get; set; }
    public bool Storniert { get; set; }
    public bool Angenommen { get; set; }
    /// <summary>Vorgänger in der Kette (Nummer des Angebots bzw. der Rechnung beim Storno).</summary>
    public string Herkunft { get; set; } = "";
    public bool Gebucht { get; set; }
    public bool Entwurf => Nummer.Length == 0;
}

public sealed class RechnungsBuch
{
    public Firma Firma { get; set; } = new();
    public List<Kunde> Kunden { get; set; } = [];
    public List<Artikel> Artikel { get; set; } = [];
    public List<Vorgang> Vorgaenge { get; set; } = [];
    public JsonObject ToJson() => BizJson.To(this);
    public static RechnungsBuch FromJson(JsonNode? n) => BizJson.From<RechnungsBuch>(n);
}

public sealed record SatzSumme(decimal Satz, decimal Netto, decimal Steuer);
public sealed record Summen(decimal Netto, decimal Steuer, decimal Brutto, IReadOnlyList<SatzSumme> JeSatz);
public sealed record Pflichtangabe(string Id, string Text, bool Ok, string Hinweis);
public sealed record BelegStatus(string Id, string Text);

public static partial class Rechnungen
{
    public static readonly (string Id, string Name, string Prefix)[] Arten = [("angebot", "Angebot", "AN"), ("auftrag", "Auftragsbestätigung", "AB"), ("rechnung", "Rechnung", "RE"), ("storno", "Stornorechnung", "SR")];
    public static readonly string[] Einheiten = ["Stück", "Std.", "Tag", "Pauschal", "Monat", "km", "kg", "m", "m²", "Liter"];
    /// <summary>Erlaubte Umsatzsteuersätze: § 12 Abs. 1 und 2 UStG (Werte aus <see cref="LegalRates"/>), 0 % nur mit Befreiungshinweis.</summary>
    public static readonly decimal[] Saetze = [LegalRates.VatStandard, LegalRates.VatReduced, 0m];
    /// <summary>Kleinbetragsrechnung bis 250 € brutto (§ 33 UStDV, seit 01.01.2017): Name/Anschrift des Empfängers, Nummer und Steuernummer entfallen.</summary>
    public const decimal Kleinbetrag = 250m;
    public const string Quelle = "§ 14 Abs. 4 UStG, § 33 UStDV, § 12 UStG, § 19 UStG – gesetze-im-internet.de (Stand 15.11.2026). Keine Steuerberatung.";
    public const string KleinunternehmerHinweis = "Gemäß § 19 UStG wird keine Umsatzsteuer berechnet.";

    public static string Name(string art) => Arten.FirstOrDefault(a => a.Id == art).Name ?? "Rechnung";
    public static string Prefix(string art) => Arten.FirstOrDefault(a => a.Id == art).Prefix ?? "RE";

    public static decimal PosNetto(Position p) => Math.Round(p.Menge * p.Preis * (1 - p.Rabatt / 100m), 2, MidpointRounding.AwayFromZero);

    /// <summary>Summen je Steuersatz (Steuer je Satz gerundet, wie in Buchhaltungsprogrammen üblich). Kleinunternehmer: keine Steuer.</summary>
    public static Summen Summe(Vorgang v)
    {
        var groups = v.Positionen.GroupBy(p => v.Kleinunternehmer ? 0m : p.Ust).OrderByDescending(g => g.Key)
            .Select(g => { var n = g.Sum(PosNetto); return new SatzSumme(g.Key, n, Math.Round(n * g.Key / 100m, 2, MidpointRounding.AwayFromZero)); }).ToList();
        var net = groups.Sum(g => g.Netto); var tax = groups.Sum(g => g.Steuer);
        return new(net, tax, net + tax, groups);
    }

    public static DateOnly? Faellig(Vorgang v) => BizJson.Datum(v.Datum) is { } d && v.Art == "rechnung" ? d.AddDays(Math.Max(0, v.ZahlungszielTage)) : null;
    public static decimal Offen(Vorgang v) => v.Art == "rechnung" && !v.Storniert ? Math.Max(0, Summe(v).Brutto - v.Bezahlt) : 0;

    /// <summary>Status: entwurf, offen, ueberfaellig, teilbezahlt, bezahlt, storniert; Angebote: offen, angenommen, abgelaufen.</summary>
    public static BelegStatus Status(Vorgang v, DateOnly heute)
    {
        if (v.Entwurf) return new("entwurf", "Entwurf");
        if (v.Storniert) return new("storniert", "Storniert");
        if (v.Art == "angebot")
        {
            if (v.Angenommen) return new("angenommen", "Angenommen");
            return BizJson.Datum(v.Datum) is { } d && heute > d.AddDays(v.GueltigTage) ? new("abgelaufen", "Abgelaufen") : new("offen", "Offen");
        }
        if (v.Art is "auftrag" or "storno") return new("fertig", v.Art == "storno" ? "Storno" : "Bestätigt");
        var offen = Offen(v);
        if (offen <= 0) return new("bezahlt", "Bezahlt" + (v.BezahltAm.Length > 0 ? " am " + BizJson.TextOf(v.BezahltAm) : ""));
        var due = Faellig(v);
        if (due is { } f && heute > f) return new("ueberfaellig", $"Überfällig seit {heute.DayNumber - f.DayNumber} Tagen" + (v.Bezahlt > 0 ? $" (noch {BizJson.Euro(offen)})" : ""));
        if (v.Bezahlt > 0) return new("teilbezahlt", $"Teilweise bezahlt – noch {BizJson.Euro(offen)}");
        return new("offen", due is { } ff ? $"Offen – fällig am {BizJson.Text(ff)}" : "Offen");
    }

    /// <summary>Pflichtangaben einer Rechnung nach § 14 Abs. 4 UStG (Kleinbetragsrechnung § 33 UStDV bis 250 € brutto). Für Angebote nur Hinweise.</summary>
    public static List<Pflichtangabe> Pruefen(RechnungsBuch b, Vorgang v)
    {
        var f = b.Firma; var s = Summe(v); var klein = v.Art == "rechnung" && s.Brutto <= Kleinbetrag && s.Brutto > 0;
        var r = new List<Pflichtangabe>();
        void Add(string id, string text, bool ok, string hint) => r.Add(new(id, text, ok, ok ? "" : hint));
        Add("absender", "Name und Anschrift deines Unternehmens", f.Name.Trim().Length > 0 && f.PlzOrt.Trim().Length > 0, "Unter „Firma“ Name, Straße und PLZ Ort eintragen.");
        if (!klein) Add("empfaenger", "Name und Anschrift des Kunden", v.KundeAnschrift.Split('\n').Count(x => x.Trim().Length > 0) >= 2, "Kunde mit Anschrift wählen oder eintragen.");
        if (!klein) Add("steuernr", "Steuernummer oder USt-IdNr.", f.SteuerNr.Trim().Length > 0 || f.UstId.Trim().Length > 0, "Unter „Firma“ Steuernummer oder USt-IdNr. eintragen.");
        Add("datum", "Ausstellungsdatum", BizJson.Datum(v.Datum) is not null, "Datum als TT.MM.JJJJ eintragen.");
        if (!klein) Add("nummer", "Fortlaufende Nummer", true, "");
        Add("positionen", "Menge und Bezeichnung jeder Leistung", v.Positionen.Count > 0 && v.Positionen.All(p => p.Text.Trim().Length > 0 && p.Menge != 0), "Jede Position braucht eine Bezeichnung und eine Menge.");
        Add("leistung", "Zeitpunkt der Leistung (Leistungsdatum oder -zeitraum)", v.Leistung.Trim().Length > 0 || v.Art != "rechnung", "Leistungsdatum eintragen – oder „entspricht Rechnungsdatum“.");
        Add("saetze", "Steuersatz je Position zulässig (19 %, 7 % oder 0 %)", v.Positionen.All(p => Saetze.Contains(p.Ust)), "Nur 19, 7 oder 0 % verwenden.");
        if (v.Kleinunternehmer) Add("p19", "Hinweis auf § 19 UStG (Kleinunternehmer)", true, "");
        else if (v.Positionen.Any(p => p.Ust == 0)) Add("befreiung", "Hinweis auf die Steuerbefreiung bei 0 %", v.Befreiung.Trim().Length > 0, "Bei 0 % den Grund angeben (z. B. „Steuerfreie Leistung nach § 4 Nr. … UStG“).");
        Add("betrag", "Entgelt und Steuerbetrag je Satz", s.JeSatz.Count > 0, "Positionen mit Preis eintragen.");
        if (v.Positionen.Any(p => p.Preis < 0 || p.Rabatt is < 0 or > 100) && v.Art != "storno") r.Add(new("werte", "Preise und Rabatte gültig", false, "Preis nicht negativ, Rabatt zwischen 0 und 100 %."));
        if (klein) r.Add(new("klein", $"Kleinbetragsrechnung bis {Kleinbetrag:0} € (§ 33 UStDV) – Kunde, Nummer und Steuernummer nicht vorgeschrieben", true, ""));
        return r;
    }

    public static bool Vollstaendig(RechnungsBuch b, Vorgang v) => Pruefen(b, v).All(p => p.Ok);

    /// <summary>Kunden in den Vorgang übernehmen (Anschrift, Nummer, Art) – nur bei Entwürfen.</summary>
    public static void KundeSetzen(Vorgang v, Kunde k)
    {
        if (!v.Entwurf) throw new InvalidOperationException("Ein festgeschriebener Beleg lässt sich nicht mehr ändern – bei Fehlern stornieren.");
        v.KundeId = k.Id; v.KundeAnschrift = k.Anschrift; v.KundeNummer = k.Nummer; v.KundeUnternehmer = k.Unternehmer;
    }

    public static Position AusArtikel(Artikel a, decimal menge = 1) => new() { Text = a.Name, Einheit = a.Einheit, Preis = a.Preis, Ust = a.Ust, Menge = menge };

    public static Vorgang Neu(RechnungsBuch b, string art, DateOnly heute)
    {
        if (!Arten.Any(a => a.Id == art) || art == "storno") throw new ArgumentException("Art: angebot, auftrag oder rechnung.");
        return new Vorgang
        {
            Id = BizJson.NewId("b"), Art = art, Datum = BizJson.Iso(heute), ZahlungszielTage = b.Firma.Zahlungsziel, Kleinunternehmer = b.Firma.Kleinunternehmer,
            Leistung = art == "rechnung" ? BizJson.Text(heute) : "",
            Kopftext = art switch { "angebot" => "vielen Dank für Ihre Anfrage. Gerne bieten wir Ihnen an:", "auftrag" => "vielen Dank für Ihren Auftrag, den wir hiermit wie folgt bestätigen:", _ => "vielen Dank für Ihren Auftrag. Wir berechnen Ihnen:" },
        };
    }

    /// <summary>Folgebeleg in der Kette (Angebot → Auftragsbestätigung → Rechnung) als neuer Entwurf mit denselben Positionen. Das Angebot gilt danach als angenommen.</summary>
    public static Vorgang Weiter(RechnungsBuch b, Vorgang von, string art, DateOnly heute)
    {
        if (von.Entwurf) throw new InvalidOperationException($"Erst „{Name(von.Art)}“ festschreiben, dann weiterführen.");
        if (von.Storniert) throw new InvalidOperationException("Ein stornierter Beleg lässt sich nicht weiterführen.");
        if (!(von.Art == "angebot" && art is "auftrag" or "rechnung") && !(von.Art == "auftrag" && art == "rechnung")) throw new InvalidOperationException("Weiter geht es nur vom Angebot zur Auftragsbestätigung oder Rechnung und von der Auftragsbestätigung zur Rechnung.");
        var n = Neu(b, art, heute);
        n.KundeId = von.KundeId; n.KundeAnschrift = von.KundeAnschrift; n.KundeNummer = von.KundeNummer; n.KundeUnternehmer = von.KundeUnternehmer;
        n.Positionen = [.. von.Positionen.Select(BizJson.Clone)]; n.Befreiung = von.Befreiung; n.Herkunft = von.Nummer;
        n.Kopftext = (art == "rechnung" ? "wie vereinbart" : "vielen Dank für Ihren Auftrag") + $" ({Name(von.Art)} {von.Nummer} vom {BizJson.TextOf(von.Datum)}) berechnen wir Ihnen:";
        if (art == "auftrag") n.Kopftext = $"vielen Dank für Ihren Auftrag zu unserem {Name(von.Art)} {von.Nummer} vom {BizJson.TextOf(von.Datum)}. Wir bestätigen:";
        if (von.Art == "angebot") von.Angenommen = true;
        return n;
    }

    /// <summary>Festschreiben: Pflichtangaben prüfen, nächste lückenlose Nummer vergeben. Danach ist der Beleg unveränderlich (GoBD-Gedanke).</summary>
    public static string Festschreiben(RechnungsBuch b, Vorgang v, Nummernkreis kreis)
    {
        if (!v.Entwurf) throw new InvalidOperationException($"Der Beleg hat schon die Nummer {v.Nummer}.");
        var miss = Pruefen(b, v).Where(p => !p.Ok).ToList();
        if (miss.Count > 0) throw new InvalidOperationException("Noch nicht vollständig: " + string.Join(" ", miss.Select(m => m.Hinweis)));
        var d = BizJson.Datum(v.Datum)!.Value;
        if (v.Kleinunternehmer) foreach (var p in v.Positionen) p.Ust = 0;
        v.Nummer = kreis.Vergeben(v.Art, d);
        kreis.Ablegen(v);
        return v.Nummer;
    }

    /// <summary>Storno: festgeschriebene Rechnung wird durch eine Stornorechnung mit eigener Nummer und negativen Mengen aufgehoben – nichts wird gelöscht.</summary>
    public static Vorgang Stornieren(RechnungsBuch b, Vorgang v, DateOnly heute, Nummernkreis kreis)
    {
        if (v.Art != "rechnung" || v.Entwurf) throw new InvalidOperationException("Stornieren geht nur bei festgeschriebenen Rechnungen. Einen Entwurf kannst du einfach löschen.");
        if (v.Storniert) throw new InvalidOperationException("Diese Rechnung ist schon storniert.");
        var s = new Vorgang
        {
            Id = BizJson.NewId("b"), Art = "storno", Datum = BizJson.Iso(heute), Leistung = v.Leistung, KundeId = v.KundeId, KundeAnschrift = v.KundeAnschrift, KundeNummer = v.KundeNummer, KundeUnternehmer = v.KundeUnternehmer,
            Positionen = [.. v.Positionen.Select(p => { var c = BizJson.Clone(p); c.Menge = -c.Menge; return c; })], Kleinunternehmer = v.Kleinunternehmer, Befreiung = v.Befreiung, Herkunft = v.Nummer,
            Kopftext = $"hiermit stornieren wir die Rechnung {v.Nummer} vom {BizJson.TextOf(v.Datum)} vollständig.",
        };
        s.Nummer = kreis.Vergeben("storno", heute); v.Storniert = true;
        kreis.Ablegen(s); kreis.Ablegen(v);
        return s;
    }

    /// <summary>Zahlung erfassen (auch Teilzahlung). Rückgabe: Status danach.</summary>
    public static void Zahlung(Vorgang v, decimal betrag, DateOnly am)
    {
        if (v.Art != "rechnung" || v.Entwurf) throw new InvalidOperationException("Zahlungen gibt es nur bei festgeschriebenen Rechnungen.");
        if (v.Storniert) throw new InvalidOperationException("Die Rechnung ist storniert.");
        if (betrag <= 0) throw new ArgumentException("Der Betrag muss größer als 0 sein.");
        v.Bezahlt = Math.Min(Summe(v).Brutto, v.Bezahlt + betrag); v.BezahltAm = BizJson.Iso(am);
    }

    /// <summary>Buchungen für das Haushaltsbuch von „Geld &amp; Rechnungen“ – je Steuersatz eine Einnahme (Brutto, Satz), bei Teilzahlung anteilig.</summary>
    public static List<Entry> Buchungen(Vorgang v, decimal betrag, DateOnly am)
    {
        var s = Summe(v); if (s.Brutto == 0) return [];
        var res = new List<Entry>(); var rest = betrag;
        for (var i = 0; i < s.JeSatz.Count; i++)
        {
            var g = s.JeSatz[i]; var share = i == s.JeSatz.Count - 1 ? rest : Math.Round(betrag * (g.Netto + g.Steuer) / s.Brutto, 2);
            rest -= share;
            res.Add(new Entry(am, $"Rechnung {v.Nummer}" + (s.JeSatz.Count > 1 ? $" ({Num(g.Satz)} %)" : ""), share, "Einnahmen", g.Satz));
        }
        return res;
    }
    static string Num(decimal d) => BizJson.Num(d);

    /// <summary>Rechnungsausgangsbuch als CSV.</summary>
    public static string Csv(RechnungsBuch b, DateOnly heute)
    {
        var sb = new StringBuilder("Art;Nummer;Datum;Kunde;Netto;USt;Brutto;Offen;Status\r\n");
        var de = CultureInfo.GetCultureInfo("de-DE");
        foreach (var v in b.Vorgaenge.Where(v => !v.Entwurf).OrderBy(v => v.Datum).ThenBy(v => v.Nummer))
        {
            var s = Summe(v);
            sb.Append(string.Join(";", BizJson.Csv(Name(v.Art)), BizJson.Csv(v.Nummer), BizJson.TextOf(v.Datum), BizJson.Csv(v.KundeAnschrift.Split('\n')[0]),
                s.Netto.ToString("0.00", de), s.Steuer.ToString("0.00", de), s.Brutto.ToString("0.00", de), Offen(v).ToString("0.00", de), BizJson.Csv(Status(v, heute).Text))).Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>Für die E-Rechnung: Partei aus Firma bzw. Kunde (vorhandener ERechnung-Kern).</summary>
    public static ERechnungPartei Verkaeufer(Firma f) => ERechnung.Parse(f.Anschrift, f.UstId, f.SteuerNr, f.Email);
    public static ERechnungPartei Kaeufer(Vorgang v, Kunde? k) => ERechnung.Parse(v.KundeAnschrift, k?.UstId ?? "", "", k?.Email ?? "");

    /// <summary>XRechnung (CII 2.3) über den vorhandenen ERechnung-Kern – mit Steuersatz, Einheit und Rabatt je Position.</summary>
    public static byte[] XRechnung(RechnungsBuch b, Vorgang v)
    {
        if (v.Art != "rechnung" || v.Entwurf) throw new InvalidOperationException("E-Rechnung gibt es für festgeschriebene Rechnungen.");
        var k = b.Kunden.FirstOrDefault(x => x.Id == v.KundeId);
        var lines = v.Positionen.Select(p => new ERechnungZeile(p.Text, p.Menge, p.Einheit, Math.Round(p.Preis * (1 - p.Rabatt / 100m), 4), PosNetto(p), v.Kleinunternehmer ? 0 : p.Ust)).ToList();
        var reference = k?.Referenz.Trim() is { Length: > 0 } r ? r : v.KundeNummer.Trim().Length > 0 ? v.KundeNummer : v.Herkunft;
        return ERechnung.WriteDetailed(v.Nummer, BizJson.Datum(v.Datum)!.Value, BizJson.Datum(v.Leistung), Verkaeufer(b.Firma), Kaeufer(v, k), b.Firma.Iban, reference, lines,
            v.Kleinunternehmer, v.ZahlungszielTage, v.Befreiung);
    }

    /// <summary>Lückenprüfung eines Nummernkreises: fehlende laufende Nummern je Jahr (z. B. RE-2026-004 fehlt).</summary>
    public static List<string> Luecken(IEnumerable<string> nummern, string prefix)
    {
        var res = new List<string>();
        foreach (var g in nummern.Select(n => NrRx().Match(n)).Where(m => m.Success && m.Groups[1].Value == prefix).GroupBy(m => m.Groups[2].Value))
        {
            var have = g.Select(m => int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture)).ToHashSet();
            for (var i = 1; i < have.Max(); i++) if (!have.Contains(i)) res.Add($"{prefix}-{g.Key}-{i:000}");
        }
        return res;
    }
    [GeneratedRegex(@"^([A-Z]{2})-(\d{4})-(\d{3,})$")] internal static partial Regex NrRx();

    /// <summary>Beispiel mit neutralen Daten (Erika Muster, Beispiel GmbH).</summary>
    public static RechnungsBuch Beispiel(DateOnly heute)
    {
        var b = new RechnungsBuch
        {
            Firma = new() { Name = "Erika Muster Webdesign", Strasse = "Musterstraße 1", PlzOrt = "12345 Musterstadt", Email = "rechnung@example.org", Telefon = "0123 456789", SteuerNr = "12/345/67890", Iban = "DE89370400440532013000", Bank = "Beispielbank", Zahlungsziel = 14 },
            Kunden =
            [
                new() { Id = "k1", Nummer = "K-1001", Name = "Beispiel GmbH", Zusatz = "z. Hd. Frau Beispiel", Strasse = "Hauptstraße 5", PlzOrt = "54321 Beispielhausen", Email = "einkauf@example.com", Unternehmer = true, Referenz = "BEST-2026-17" },
                new() { Id = "k2", Nummer = "K-1002", Name = "Max Mustermann", Strasse = "Gartenweg 3", PlzOrt = "12345 Musterstadt", Email = "max@example.net", Unternehmer = false },
            ],
            Artikel =
            [
                new() { Id = "a1", Nummer = "A-01", Name = "Beratung", Einheit = "Std.", Preis = 85m, Ust = 19 },
                new() { Id = "a2", Nummer = "A-02", Name = "Webseite Grundpaket", Einheit = "Pauschal", Preis = 1200m, Ust = 19 },
                new() { Id = "a3", Nummer = "A-03", Name = "Fachbuch „Gutes Webdesign“", Einheit = "Stück", Preis = 28.04m, Ust = 7 },
            ],
        };
        var v = Neu(b, "rechnung", heute); KundeSetzen(v, b.Kunden[0]);
        v.Positionen = [AusArtikel(b.Artikel[1]), AusArtikel(b.Artikel[0], 6), AusArtikel(b.Artikel[2], 2)];
        v.Positionen[1].Rabatt = 10;
        b.Vorgaenge.Add(v);
        return b;
    }
}

/// <summary>Lückenloser Nummernkreis je Belegart und Jahr (AN-, AB-, RE-, SR-JJJJ-NNN). Der Stand liegt zentral unter finanzen/nummernkreise.json;
/// Rechnungsnummern aus „Geld &amp; Rechnungen“ (RE-*.html) und das Archiv (RE-*.json) zählen mit – eine Nummer wird nie doppelt vergeben.</summary>
public sealed class Nummernkreis(string dataRoot)
{
    public string Datei => Path.Combine(dataRoot, "finanzen", "nummernkreise.json");
    public string Archiv => Path.Combine(dataRoot, "finanzen", "rechnungen");

    Dictionary<string, int> Load()
    {
        try { return File.Exists(Datei) ? JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(Datei)) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    /// <summary>Höchste schon benutzte laufende Nummer für Präfix und Jahr.</summary>
    public int Stand(string prefix, int year)
    {
        var key = $"{prefix}-{year}"; var max = Load().GetValueOrDefault(key);
        if (Directory.Exists(Archiv))
            foreach (var f in Directory.GetFiles(Archiv, prefix + "-" + year + "-*.*"))
                if (Rechnungen.NrRx().Match(Path.GetFileNameWithoutExtension(f)) is { Success: true } m && int.TryParse(m.Groups[3].Value, out var n)) max = Math.Max(max, n);
        return max;
    }

    public string Vorschau(string art, DateOnly date) { var p = Rechnungen.Prefix(art); return $"{p}-{date.Year}-{Stand(p, date.Year) + 1:000}"; }

    public string Vergeben(string art, DateOnly date)
    {
        var p = Rechnungen.Prefix(art); var n = Stand(p, date.Year) + 1;
        var all = Load(); all[$"{p}-{date.Year}"] = n;
        Directory.CreateDirectory(Path.GetDirectoryName(Datei)!);
        var tmp = Datei + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(all)); SafeFile.Move(tmp, Datei);
        return $"{p}-{date.Year}-{n:000}";
    }

    /// <summary>Festgeschriebenen Beleg unveränderlich ablegen (JSON). Rückgängig im Studio löscht ihn nicht – so bleibt der Kreis lückenlos.</summary>
    public string Ablegen(Vorgang v)
    {
        if (v.Entwurf) throw new InvalidOperationException("Entwürfe werden nicht abgelegt.");
        Directory.CreateDirectory(Archiv);
        var f = Path.Combine(Archiv, v.Nummer + ".json");
        File.WriteAllText(f, BizJson.To(v).ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return f;
    }

    public List<Vorgang> Abgelegt() =>
        Directory.Exists(Archiv) ? [.. Directory.GetFiles(Archiv, "*.json").Select(f => { try { return BizJson.From<Vorgang>(JsonNode.Parse(File.ReadAllText(f))); } catch (JsonException) { return null; } }).OfType<Vorgang>().Where(v => !v.Entwurf)] : [];
}
