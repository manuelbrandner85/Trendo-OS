using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Business;

// ===================== Rechnungen & Angebote im Programm „Geld“ (SPEC-11, T-0694) =====================
// Herkunft: Kern aus dem unfertigen Zweig sicherung/T-0675-business-a (dort als eigenes Studio STUDIO-INVOICE-01 geplant) übernommen und für das eine
// vorhandene Programm „Geld“ angepasst: Speicherung absturzsicher (AtomicFile), Steuersätze als einstellbare Tabelle, ZUGFeRD-PDF, Zahlungsabgleich.
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
    /// <summary>Käuferreferenz dieser Rechnung (Bestellnummer, bei Behörden die Leitweg-ID) – für die E-Rechnung.</summary>
    public string Referenz { get; set; } = "";
    public string KundeEmail { get; set; } = "";
    /// <summary>Zahlungseingänge (Datum, Betrag, Herkunft z. B. „Kontoauszug“) – daraus ergibt sich „bezahlt“.</summary>
    public List<Zahlungseingang> Zahlungen { get; set; } = [];
    public bool Gebucht { get; set; }
    public bool Entwurf => Nummer.Length == 0;
}

public sealed class Zahlungseingang
{
    public string Datum { get; set; } = "";
    public decimal Betrag { get; set; }
    public string Hinweis { get; set; } = "";
}

public sealed class RechnungsBuch
{
    public Firma Firma { get; set; } = new();
    public List<Kunde> Kunden { get; set; } = [];
    public List<Artikel> Artikel { get; set; } = [];
    public List<Vorgang> Vorgaenge { get; set; } = [];
    /// <summary>T-0694: Umsatzsteuersätze als einstellbare Tabelle mit Stand-Datum (nichts fest verdrahtet).</summary>
    public Steuersaetze Steuer { get; set; } = new();
    public JsonObject ToJson() => BizJson.To(this);
    public static RechnungsBuch FromJson(JsonNode? n) => BizJson.From<RechnungsBuch>(n);
}

/// <summary>T-0694: Umsatzsteuersätze, wie sie in Rechnungen wählbar sind – einstellbar, mit dem Datum, an dem sie zuletzt geprüft wurden.
/// Vorgabe aus <see cref="LegalRates"/> (§ 12 Abs. 1 und 2 UStG). Keine Steuerberatung.</summary>
public sealed class Steuersaetze
{
    public List<decimal> Saetze { get; set; } = [LegalRates.VatStandard, LegalRates.VatReduced, 0m];
    /// <summary>Stand (TT.MM.JJJJ): wann die Sätze zuletzt geprüft wurden.</summary>
    public string Stand { get; set; } = LegalRates.Checked;
    public string Quelle { get; set; } = "§ 12 UStG – gesetze-im-internet.de";
    public List<decimal> Erlaubt() => Saetze.Count > 0 ? [.. Saetze.Distinct().OrderByDescending(x => x)] : [LegalRates.VatStandard, LegalRates.VatReduced, 0m];
    /// <summary>„19; 7; 0“ lesen. Fehler = FormatException mit klarem Satz.</summary>
    public static List<decimal> Lesen(string text)
    {
        var res = new List<decimal>();
        foreach (var part in (text ?? "").Replace("%", "").Split([';', ' ', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!BizJson.TryBetrag(part.TrimEnd('%'), out var v) || v < 0 || v > 100) throw new FormatException($"„{part}“ ist kein Steuersatz. Bitte so schreiben: 19; 7; 0");
            if (!res.Contains(v)) res.Add(v);
        }
        if (res.Count == 0) throw new FormatException("Bitte mindestens einen Steuersatz eintragen, z. B. 19; 7; 0");
        return res;
    }
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
        Add("absender", "Name und Anschrift deines Unternehmens", f.Name.Trim().Length > 0 && f.PlzOrt.Trim().Length > 0, "Unter „Meine Firma“ Name, Straße und PLZ Ort eintragen.");
        if (!klein) Add("empfaenger", "Name und Anschrift des Kunden", v.KundeAnschrift.Split('\n').Count(x => x.Trim().Length > 0) >= 2, "Beim Kunden Name und Anschrift eintragen (mindestens zwei Zeilen).");
        if (!klein) Add("steuernr", "Steuernummer oder USt-IdNr.", f.SteuerNr.Trim().Length > 0 || f.UstId.Trim().Length > 0, "Unter „Meine Firma“ Steuernummer oder USt-IdNr. eintragen.");
        Add("datum", "Ausstellungsdatum", BizJson.Datum(v.Datum) is not null, "Datum als TT.MM.JJJJ eintragen.");
        if (!klein) Add("nummer", "Fortlaufende Nummer", true, "");
        Add("positionen", "Menge und Bezeichnung jeder Leistung", v.Positionen.Count > 0 && v.Positionen.All(p => p.Text.Trim().Length > 0 && p.Menge != 0), "Jede Position braucht eine Bezeichnung und eine Menge.");
        Add("leistung", "Zeitpunkt der Leistung (Leistungsdatum oder -zeitraum)", v.Leistung.Trim().Length > 0 || v.Art != "rechnung", "Leistungsdatum eintragen – oder „entspricht Rechnungsdatum“.");
        var erlaubt = b.Steuer.Erlaubt();
        Add("saetze", $"Steuersatz je Position zulässig ({string.Join(", ", erlaubt.Select(x => BizJson.Num(x) + " %"))})", v.Positionen.All(p => erlaubt.Contains(p.Ust)), $"Nur {string.Join(", ", erlaubt.Select(x => BizJson.Num(x)))} % verwenden – die Sätze stehen unter „Meine Firma“.");
        if (v.Kleinunternehmer) Add("p19", "Hinweis auf § 19 UStG (Kleinunternehmer)", true, "");
        else if (v.Positionen.Any(p => p.Ust == 0)) Add("befreiung", "Hinweis auf die Steuerbefreiung bei 0 %", v.Befreiung.Trim().Length > 0, "Bei 0 % den Grund angeben (z. B. „Steuerfreie Leistung nach § 4 Nr. … UStG“).");
        Add("betrag", "Entgelt und Steuerbetrag je Satz", s.JeSatz.Count > 0, "Positionen mit Preis eintragen.");
        if (v.Positionen.Any(p => p.Preis < 0 || p.Rabatt is < 0 or > 100) && v.Art != "storno") r.Add(new("werte", "Preise und Rabatte gültig", false, "Preis nicht negativ, Rabatt zwischen 0 und 100 %."));
        if (klein) r.Add(new("klein", $"Kleinbetragsrechnung bis {Kleinbetrag:0} € (§ 33 UStDV) – Kunde, Nummer und Steuernummer nicht vorgeschrieben", true, ""));
        return r;
    }

    public static bool Vollstaendig(RechnungsBuch b, Vorgang v) => Pruefen(b, v).All(p => p.Ok);

    /// <summary>T-0694: Was genau fehlt – ein Satz je fehlender Pflichtangabe (leer = vollständig).</summary>
    public static List<string> Fehlend(RechnungsBuch b, Vorgang v) => [.. Pruefen(b, v).Where(p => !p.Ok).Select(p => $"{p.Text} fehlt: {p.Hinweis}")];

    /// <summary>Kunden in den Vorgang übernehmen (Anschrift, Nummer, Art) – nur bei Entwürfen.</summary>
    public static void KundeSetzen(Vorgang v, Kunde k)
    {
        if (!v.Entwurf) throw new InvalidOperationException("Ein festgeschriebener Beleg lässt sich nicht mehr ändern – bei Fehlern stornieren.");
        v.KundeId = k.Id; v.KundeAnschrift = k.Anschrift; v.KundeNummer = k.Nummer; v.KundeUnternehmer = k.Unternehmer;
    }

    public static Position AusArtikel(Artikel a, decimal menge = 1) => new() { Text = a.Name, Einheit = a.Einheit, Preis = a.Preis, Ust = a.Ust, Menge = menge };

    public static Vorgang Neu(RechnungsBuch b, string art, DateOnly heute)
    {
        art = (art ?? "").Trim().ToLowerInvariant();
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
        var offen = Offen(v);
        if (betrag > offen) throw new ArgumentException($"Offen sind nur {BizJson.Euro(offen)} – mehr kann nicht zugeordnet werden.");
        v.Bezahlt += betrag; v.BezahltAm = BizJson.Iso(am);
    }

    /// <summary>T-0694: Zahlungseingang zuordnen – merkt Datum, Betrag und Herkunft am Beleg und liefert die Buchungen fürs Haushaltsbuch (je Steuersatz anteilig).</summary>
    public static List<Entry> ZahlungZuordnen(Vorgang v, decimal betrag, DateOnly am, string hinweis = "")
    {
        Zahlung(v, betrag, am);
        v.Zahlungen.Add(new Zahlungseingang { Datum = BizJson.Iso(am), Betrag = betrag, Hinweis = hinweis.Trim() });
        return Buchungen(v, betrag, am);
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
    /// <summary>Käufer: aus dem Kundenstamm, sonst aus der Anschrift des Belegs (Name = erste Zeile, Straße = die Zeile vor „PLZ Ort“).</summary>
    public static ERechnungPartei Kaeufer(Vorgang v, Kunde? k)
    {
        if (k is not null && k.Name.Trim().Length > 0 && k.PlzOrt.Trim().Length > 0) return ERechnung.Parse(string.Join("\n", k.Name, k.Strasse, k.PlzOrt), k.UstId, "", k.Email);
        var lines = v.KundeAnschrift.Replace("\r", "").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        var zi = lines.FindIndex(1, l => ERechnung.Parse("x\n" + l).Zip.Length > 0);
        if (zi < 1) return ERechnung.Parse(v.KundeAnschrift, k?.UstId ?? "", "", k?.Email is { Length: > 0 } m0 ? m0 : v.KundeEmail);
        return ERechnung.Parse(string.Join("\n", lines[0], zi > 1 ? lines[zi - 1] : "", lines[zi]), k?.UstId ?? "", "", k?.Email is { Length: > 0 } m ? m : v.KundeEmail);
    }

    static (Kunde? Kunde, List<ERechnungZeile> Zeilen, string Referenz) ERechnungTeile(RechnungsBuch b, Vorgang v)
    {
        var k = b.Kunden.FirstOrDefault(x => x.Id == v.KundeId && x.Id.Length > 0);
        var lines = v.Positionen.Select(p => new ERechnungZeile(p.Text, p.Menge, p.Einheit, Math.Round(p.Preis * (1 - p.Rabatt / 100m), 4), PosNetto(p), v.Kleinunternehmer ? 0 : p.Ust)).ToList();
        var reference = k?.Referenz.Trim() is { Length: > 0 } r ? r : v.Referenz.Trim().Length > 0 ? v.Referenz.Trim() : v.KundeNummer.Trim().Length > 0 ? v.KundeNummer : v.Herkunft;
        return (k, lines, reference);
    }

    /// <summary>T-0694: Was für die E-Rechnung noch fehlt (leer = kann geschrieben werden). XRechnung ist strenger als ZUGFeRD.</summary>
    public static IReadOnlyList<string> ERechnungFehlend(RechnungsBuch b, Vorgang v, bool xrechnung)
    {
        if (v.Art != "rechnung" || v.Entwurf) return ["E-Rechnung gibt es für festgeschriebene Rechnungen."];
        var (k, lines, reference) = ERechnungTeile(b, v);
        return ERechnung.CheckDetailed(v.Nummer, Verkaeufer(b.Firma), Kaeufer(v, k), b.Firma.Iban, reference, lines, v.Kleinunternehmer, v.Befreiung, xrechnung);
    }

    /// <summary>E-Rechnung über den vorhandenen ERechnung-Kern – mit Steuersatz, Einheit und Rabatt je Position.
    /// <paramref name="xrechnung"/> = true: XRechnung (CII 2.3); false: Profil EN 16931 für das ZUGFeRD-PDF.</summary>
    public static byte[] ERechnungXml(RechnungsBuch b, Vorgang v, bool xrechnung)
    {
        if (v.Art != "rechnung" || v.Entwurf) throw new InvalidOperationException("E-Rechnung gibt es für festgeschriebene Rechnungen.");
        var (k, lines, reference) = ERechnungTeile(b, v);
        return ERechnung.WriteDetailed(v.Nummer, BizJson.Datum(v.Datum)!.Value, BizJson.Datum(v.Leistung), Verkaeufer(b.Firma), Kaeufer(v, k), b.Firma.Iban, reference, lines,
            v.Kleinunternehmer, v.ZahlungszielTage, v.Befreiung, xrechnung);
    }

    /// <summary>XRechnung (CII 2.3).</summary>
    public static byte[] XRechnung(RechnungsBuch b, Vorgang v) => ERechnungXml(b, v, xrechnung: true);

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

    /// <summary>T-0694: Entwurf aus einer Beschreibung – Zeilen „Kunde: Name, Straße, PLZ Ort“, „Position: Leistung | Menge | Einheit | Preis netto | USt % | Rabatt %“
    /// (Einheit, USt und Rabatt dürfen fehlen), „Leistung: …“, „Referenz: …“, „E-Mail: …“, „Art: Angebot“. Ein Artikel aus dem Stamm darf statt einer Position genannt werden
    /// („Position: Beratung | 6“ nimmt Preis, Einheit und USt des Artikels). Fehler = FormatException mit klarem Satz.</summary>
    public static Vorgang AusText(RechnungsBuch b, string text, DateOnly heute)
    {
        var fields = new List<(string Key, string Value)>();
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var i = raw.IndexOf(':'); if (i <= 0) continue;
            fields.Add((raw[..i].Trim().ToLowerInvariant().Replace("ä", "ae").Replace("ü", "ue").Replace("ö", "oe"), raw[(i + 1)..].Trim()));
        }
        string Get(params string[] keys) => fields.FirstOrDefault(f => keys.Contains(f.Key)).Value ?? "";
        var art = Get("art").ToLowerInvariant(); art = art.StartsWith("angebot", StringComparison.Ordinal) ? "angebot" : art.StartsWith("auftrag", StringComparison.Ordinal) ? "auftrag" : "rechnung";
        var v = Neu(b, art, heute);
        var kunde = Get("kunde", "kaeufer", "empfaenger");
        var k = b.Kunden.FirstOrDefault(x => x.Name.Length > 0 && string.Equals(x.Name, kunde.Split(',')[0].Trim(), StringComparison.OrdinalIgnoreCase));
        if (k is not null) KundeSetzen(v, k); else v.KundeAnschrift = string.Join("\n", kunde.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        v.KundeEmail = Get("e-mail", "email", "mail"); v.Referenz = Get("referenz", "bestellnummer", "leitweg-id");
        if (Get("leistung", "leistungsdatum", "zeitraum") is { Length: > 0 } l) v.Leistung = l;
        if (BizJson.Datum(Get("datum")) is { } d) { v.Datum = BizJson.Iso(d); if (Get("leistung", "leistungsdatum", "zeitraum").Length == 0 && art == "rechnung") v.Leistung = BizJson.Text(d); }
        foreach (var (key, val) in fields.Where(f => f.Key is "position" or "pos" or "posten"))
        {
            var p = val.Split('|').Select(x => x.Trim()).ToArray();
            var a = b.Artikel.FirstOrDefault(x => string.Equals(x.Name, p[0], StringComparison.OrdinalIgnoreCase));
            if (p.Length < 2 || p.Length < 3 && a is null) throw new FormatException($"Position „{val}“ bitte so: „Leistung | Menge | Einheit | Preis | USt“, z. B. „Beratung | 6 | Std. | 85 | 19“.");
            if (!BizJson.TryBetrag(p[1], out var menge) || menge == 0) throw new FormatException($"„{val}“: Die Menge fehlt oder ist 0.");
            var hasUnit = p.Length >= 4 && !BizJson.TryBetrag(p[2], out _); var off = hasUnit ? 1 : 0;
            var pos = a is not null && p.Length < 3 ? AusArtikel(a, menge) : new Position { Text = p[0], Menge = menge, Einheit = hasUnit ? p[2] : a?.Einheit ?? "Stück", Ust = a?.Ust ?? b.Steuer.Erlaubt().First() };
            if (p.Length > 2 + off) { if (!BizJson.TryBetrag(p[2 + off], out var preis) || preis < 0) throw new FormatException($"„{val}“: Den Preis verstehe ich nicht."); pos.Preis = preis; }
            if (p.Length > 3 + off) { if (!BizJson.TryBetrag(p[3 + off].TrimEnd('%', ' '), out var ust)) throw new FormatException($"„{val}“: USt bitte als Zahl, z. B. 19."); pos.Ust = ust; }
            if (p.Length > 4 + off) { if (!BizJson.TryBetrag(p[4 + off].TrimEnd('%', ' '), out var rab) || rab is < 0 or > 100) throw new FormatException($"„{val}“: Rabatt in Prozent zwischen 0 und 100."); pos.Rabatt = rab; }
            if (v.Kleinunternehmer) pos.Ust = 0;
            v.Positionen.Add(pos);
        }
        if (v.Positionen.Count == 0) throw new FormatException("Es fehlt mindestens eine Zeile „Position: Leistung | Menge | Einheit | Preis | USt“.");
        if (Get("befreiung") is { Length: > 0 } bf) v.Befreiung = bf;
        return v;
    }

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
        AtomicFile.WriteAllText(Datei, JsonSerializer.Serialize(all));
        return $"{p}-{date.Year}-{n:000}";
    }

    /// <summary>Festgeschriebenen Beleg unveränderlich ablegen (JSON). Rückgängig im Studio löscht ihn nicht – so bleibt der Kreis lückenlos.</summary>
    public string Ablegen(Vorgang v)
    {
        if (v.Entwurf) throw new InvalidOperationException("Entwürfe werden nicht abgelegt.");
        Directory.CreateDirectory(Archiv);
        var f = Path.Combine(Archiv, v.Nummer + ".json");
        AtomicFile.WriteAllText(f, BizJson.To(v).ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return f;
    }

    public List<Vorgang> Abgelegt() =>
        Directory.Exists(Archiv) ? [.. Directory.GetFiles(Archiv, "*.json").Select(f => { try { return BizJson.From<Vorgang>(JsonNode.Parse(File.ReadAllText(f))); } catch (JsonException) { return null; } }).OfType<Vorgang>().Where(v => !v.Entwurf)] : [];
}

/// <summary>T-0694: Das Rechnungsbuch von „Geld“ (Meine Firma, Kunden, Artikel, Belege samt Entwürfen) als eine Datei unter finanzen/rechnungsbuch.json.
/// Geschrieben wird absturzsicher (<see cref="AtomicFile"/>), die vorige Fassung bleibt als .bak daneben; eine beschädigte Datei wird nicht still geleert,
/// sondern beiseitegelegt und aus der .bak-Fassung wiederhergestellt (<see cref="Wiederhergestellt"/> sagt es der Oberfläche).</summary>
public sealed class RechnungsBuchStore(string dataRoot)
{
    public string Datei => Path.Combine(dataRoot, "finanzen", "rechnungsbuch.json");
    public string? Wiederhergestellt { get; private set; }

    public RechnungsBuch Laden()
    {
        var (text, note) = SichereDatei.Lesen(Datei);
        Wiederhergestellt = note;
        if (text is null) return new RechnungsBuch();
        return RechnungsBuch.FromJson(JsonNode.Parse(text));
    }

    public void Speichern(RechnungsBuch b) => SichereDatei.Schreiben(Datei, b.ToJson().ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    public string Roh() => File.Exists(Datei) ? File.ReadAllText(Datei) : "";
    /// <summary>Für Rückgängig: genau diesen früheren Stand zurückschreiben (leer = Datei entfernen).</summary>
    public void RohSetzen(string json) { if (json.Length == 0) { if (File.Exists(Datei)) File.Delete(Datei); } else SichereDatei.Schreiben(Datei, json); }
}

/// <summary>T-0694: JSON-Datei absturzsicher schreiben und nach einem Schaden wiederherstellen. Schreiben: erst die bisherige gültige Fassung als .bak sichern,
/// dann atomar ersetzen. Lesen: ist die Datei kein gültiges JSON (abgebrochener Schreibvorgang eines anderen Programms, Datenträgerfehler), wird sie als
/// „…beschaedigt-JJJJMMTT-HHMMSS…“ beiseitegelegt und die .bak-Fassung zurückgeholt.</summary>
public static class SichereDatei
{
    static bool Gueltig(string text) { try { JsonNode.Parse(text); return text.Trim().Length > 0; } catch (JsonException) { return false; } }

    public static void Schreiben(string path, string json)
    {
        try { if (File.Exists(path) && Gueltig(File.ReadAllText(path))) File.Copy(path, path + ".bak", true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* Sicherung ist Zusatz – das Schreiben selbst bleibt atomar */ }
        AtomicFile.WriteAllText(path, json);
    }

    /// <summary>Text der Datei (null = es gibt sie nicht) und ein Hinweis, falls aus der Sicherung wiederhergestellt wurde.</summary>
    public static (string? Text, string? Hinweis) Lesen(string path)
    {
        if (!File.Exists(path)) return File.Exists(path + ".bak") && Gueltig(File.ReadAllText(path + ".bak")) ? Zurueck(path, null) : (null, null);
        var text = File.ReadAllText(path);
        if (Gueltig(text)) return (text, null);
        var aside = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + ".beschaedigt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + Path.GetExtension(path));
        try { File.Move(path, aside, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { aside = ""; }
        if (File.Exists(path + ".bak") && Gueltig(File.ReadAllText(path + ".bak"))) return Zurueck(path, aside);
        return (null, $"„{Path.GetFileName(path)}“ war beschädigt und es gab keine Sicherung. Die beschädigte Datei liegt als „{Path.GetFileName(aside)}“ daneben.");
    }

    static (string?, string?) Zurueck(string path, string? aside)
    {
        var text = File.ReadAllText(path + ".bak");
        AtomicFile.WriteAllText(path, text);
        return (text, aside is null ? $"„{Path.GetFileName(path)}“ fehlte – der letzte gesicherte Stand wurde zurückgeholt."
            : $"„{Path.GetFileName(path)}“ war beschädigt – der letzte gesicherte Stand wurde zurückgeholt." + (aside.Length > 0 ? $" Die beschädigte Datei liegt als „{Path.GetFileName(aside)}“ daneben." : ""));
    }
}
