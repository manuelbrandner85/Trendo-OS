using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Trendo.Core.Sys;

namespace Trendo.Core.Buero;

// ===================== STUDIO-CONTRACTS-01 Verträge-Ordner (T-0671) =====================
// Clean-Room nach dem Bedienkonzept von Vertragsverwaltungen (Volders, Aboalarm, Finanzguru): Verträge erfassen, Kündigungsfrist berechnen,
// Fristen-Ampel, Erinnerungen als Kalenderdatei, Kosten im Überblick, Kündigungsschreiben als Brief-Entwurf. Es wird nichts versendet.

public sealed class Vertrag
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Anbieter { get; set; } = "";
    /// <summary>Anschrift des Anbieters (für das Kündigungsschreiben), mehrzeilig.</summary>
    public string Anschrift { get; set; } = "";
    public string Kategorie { get; set; } = "Sonstiges";
    public string Kundennummer { get; set; } = "";
    /// <summary>Vertragsbeginn als JJJJ-MM-TT.</summary>
    public string Beginn { get; set; } = "";
    /// <summary>Mindestlaufzeit in Monaten; 0 = unbefristet (jederzeit mit Frist kündbar).</summary>
    public int LaufzeitMonate { get; set; } = 24;
    /// <summary>Verlängerung nach Ablauf in Monaten; 0 = endet von selbst. Bei unbefristeten Verträgen ohne Bedeutung.</summary>
    public int VerlaengerungMonate { get; set; } = 1;
    public int FristWert { get; set; } = 1;
    /// <summary>tage, wochen oder monate.</summary>
    public string FristEinheit { get; set; } = "monate";
    /// <summary>Unbefristet: Kündigung nur zum Monatsende (häufig bei Abos und Mietverträgen).</summary>
    public bool ZumMonatsende { get; set; } = true;
    public decimal Kosten { get; set; }
    /// <summary>monat, quartal, halbjahr, jahr oder einmalig.</summary>
    public string Intervall { get; set; } = "monat";
    public List<string> Dokumente { get; set; } = [];
    /// <summary>Gekündigt zum (JJJJ-MM-TT); leer = läuft.</summary>
    public string GekuendigtZum { get; set; } = "";
    public string Notiz { get; set; } = "";
}

public sealed class VertragsOrdner
{
    public List<Vertrag> Vertraege { get; set; } = [];
    /// <summary>Erinnerung so viele Tage vor dem letzten Kündigungstag.</summary>
    public int VorlaufTage { get; set; } = 14;

    static readonly JsonSerializerOptions O = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public JsonObject ToJson() => JsonSerializer.SerializeToNode(this, O)!.AsObject();
    public static VertragsOrdner FromJson(JsonNode? n)
    {
        if (n is not JsonObject o) return new();
        try { return o.Deserialize<VertragsOrdner>(O) ?? new(); } catch (JsonException) { return new(); }
    }
}

/// <summary>Stand eines Vertrags an einem Tag: nächstes mögliches Ende, letzter Tag für den Zugang der Kündigung, Ampel.</summary>
public sealed record VertragsStand(DateTime? Ende, DateTime? KuendigenBis, int? TageBis, string Ampel, string Text);

public static class Vertraege
{
    public static readonly string[] Kategorien = ["Handy & Internet", "Strom & Gas", "Versicherung", "Streaming & Abos", "Fitness & Verein", "Wohnen & Miete", "Auto & Mobilität", "Finanzen", "Sonstiges"];
    public static readonly (string Id, string Name, int Monate)[] Intervalle = [("monat", "monatlich", 1), ("quartal", "vierteljährlich", 3), ("halbjahr", "halbjährlich", 6), ("jahr", "jährlich", 12), ("einmalig", "einmalig", 0)];
    public static readonly (string Id, string Name)[] Einheiten = [("tage", "Tage"), ("wochen", "Wochen"), ("monate", "Monate")];
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    public static DateTime? Datum(string s) =>
        DateTime.TryParseExact((s ?? "").Trim(), ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy"], De, DateTimeStyles.None, out var d) ? d.Date : null;
    public static string Iso(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string Text(DateTime d) => d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>Letzter Tag, an dem die Kündigung beim Anbieter eingehen muss, damit der Vertrag zum <paramref name="ende"/> endet.
    /// Die Frist wird vom Ende rückwärts gerechnet: Monate über den Folgetag (Ende 30.06., 3 Monate → 31.03.; Ende 28.02., 1 Monat → 31.01.), Wochen und Tage genau.</summary>
    public static DateTime KuendigenBis(DateTime ende, int wert, string einheit)
    {
        if (wert < 0) throw new ArgumentException("Die Kündigungsfrist kann nicht negativ sein.");
        return einheit switch
        {
            "tage" => ende.AddDays(-wert),
            "wochen" => ende.AddDays(-7 * wert),
            "monate" => ende.AddDays(1).AddMonths(-wert).AddDays(-1),
            _ => throw new ArgumentException("Fristeinheit: tage, wochen oder monate."),
        };
    }

    static DateTime MonthEnd(DateTime d) => new DateTime(d.Year, d.Month, 1).AddMonths(1).AddDays(-1);

    /// <summary>Nächstes Vertragsende, zu dem man heute noch kündigen kann. Mindestlaufzeit endet am Tag vor dem Jahrestag des Beginns (Beginn 15.03.2025, 24 Monate → 14.03.2027);
    /// danach je Verlängerung weiter. Unbefristet: frühestes Ende ab heute plus Frist (auf Wunsch zum Monatsende). Null, wenn der Vertrag ohne Kündigung endet und schon vorbei ist.</summary>
    public static DateTime? NaechstesEnde(Vertrag v, DateTime heute)
    {
        heute = heute.Date;
        var beginn = Datum(v.Beginn) ?? throw new FormatException($"„{v.Name}“: Beginn bitte als Datum, z. B. 2025-03-15 oder 15.03.2025.");
        if (v.FristWert < 0 || v.LaufzeitMonate < 0 || v.VerlaengerungMonate < 0) throw new ArgumentException($"„{v.Name}“: Laufzeit, Verlängerung und Frist dürfen nicht negativ sein.");
        if (v.LaufzeitMonate == 0)
        {
            var start = heute < beginn ? beginn : heute;
            // frühestes Ende E mit KuendigenBis(E) >= heute
            var e = v.FristEinheit switch { "tage" => start.AddDays(v.FristWert), "wochen" => start.AddDays(7 * v.FristWert), _ => start.AddMonths(v.FristWert) };
            if (v.ZumMonatsende) { e = MonthEnd(e); while (KuendigenBis(e, v.FristWert, v.FristEinheit) < heute) e = MonthEnd(e.AddDays(1)); }
            else while (KuendigenBis(e, v.FristWert, v.FristEinheit) < heute) e = e.AddDays(1);
            return e;
        }
        var ende = beginn.AddMonths(v.LaufzeitMonate).AddDays(-1);
        if (v.VerlaengerungMonate == 0) return ende >= heute ? ende : null;
        var n = 0;
        while (KuendigenBis(ende, v.FristWert, v.FristEinheit) < heute)
        {
            n++; ende = beginn.AddMonths(v.LaufzeitMonate + n * v.VerlaengerungMonate).AddDays(-1);
            if (n > 2400) throw new InvalidOperationException("Die Laufzeit ist zu lang für eine Berechnung.");
        }
        return ende;
    }

    /// <summary>Stand mit Ampel: rot = höchstens 14 Tage bis zum letzten Kündigungstag, gelb = höchstens 60 Tage, grün = mehr Zeit, grau = gekündigt oder abgelaufen.</summary>
    public static VertragsStand Stand(Vertrag v, DateTime heute)
    {
        heute = heute.Date;
        if (Datum(v.GekuendigtZum) is { } gz) return new(gz, null, null, "grau", gz < heute ? $"Beendet am {Text(gz)}" : $"Gekündigt zum {Text(gz)}");
        DateTime? ende;
        try { ende = NaechstesEnde(v, heute); }
        catch (Exception e) when (e is FormatException or ArgumentException) { return new(null, null, null, "grau", e.Message); }
        if (ende is null) return new(null, null, null, "grau", "Abgelaufen (endet ohne Kündigung)");
        if (v.LaufzeitMonate > 0 && v.VerlaengerungMonate == 0) return new(ende, null, (int)(ende.Value - heute).TotalDays, "grau", $"Endet von selbst am {Text(ende.Value)}");
        var bis = KuendigenBis(ende.Value, v.FristWert, v.FristEinheit); var tage = (int)(bis - heute).TotalDays;
        var ampel = tage <= 14 ? "rot" : tage <= 60 ? "gelb" : "gruen";
        var when = tage == 0 ? "heute" : tage == 1 ? "morgen" : $"in {tage} Tagen";
        return new(ende, bis, tage, ampel, $"Spätestens kündigen bis {Text(bis)} ({when}) – dann endet er am {Text(ende.Value)}");
    }

    /// <summary>Kosten auf einen Monat umgerechnet (einmalige Kosten zählen nicht).</summary>
    public static decimal ProMonat(Vertrag v) => Intervalle.FirstOrDefault(i => i.Id == v.Intervall).Monate is var m and > 0 ? Math.Round(v.Kosten / m, 2) : 0m;
    public static decimal ProJahr(Vertrag v) => Intervalle.FirstOrDefault(i => i.Id == v.Intervall).Monate is var m and > 0 ? Math.Round(v.Kosten * 12m / m, 2) : 0m;

    /// <summary>Laufende Verträge (nicht gekündigt oder Ende noch nicht erreicht).</summary>
    public static bool Laeuft(Vertrag v, DateTime heute) => Datum(v.GekuendigtZum) is not { } g || g >= heute.Date;

    public sealed record KostenUebersicht(decimal Monat, decimal Jahr, List<(string Kategorie, decimal Monat, decimal Jahr, int Anzahl)> Kategorien, decimal Gespart);
    /// <summary>Kosten pro Monat und Jahr, nach Kategorie (teuerste zuerst). <see cref="KostenUebersicht.Gespart"/> = Jahreskosten gekündigter Verträge.</summary>
    public static KostenUebersicht Kosten(IEnumerable<Vertrag> list, DateTime heute)
    {
        var l = list.ToList(); var run = l.Where(v => Laeuft(v, heute)).ToList();
        var cats = run.GroupBy(v => v.Kategorie.Trim().Length > 0 ? v.Kategorie.Trim() : "Sonstiges")
            .Select(g => (g.Key, g.Sum(ProMonat), g.Sum(ProJahr), g.Count())).OrderByDescending(x => x.Item3).ToList();
        return new(run.Sum(ProMonat), run.Sum(ProJahr), cats, l.Where(v => Datum(v.GekuendigtZum) is not null).Sum(ProJahr));
    }

    /// <summary>Erinnerungen als Kalenderdatei (vorhandener ICS-Weg des Kalenders): je Vertrag ein ganztägiger Termin am letzten Kündigungstag mit Erinnerung <paramref name="vorlaufTage"/> vorher.</summary>
    public static string Ics(IEnumerable<Vertrag> list, DateTime heute, int vorlaufTage = 14)
    {
        if (vorlaufTage is < 0 or > 365) throw new ArgumentException("Die Erinnerung muss zwischen 0 und 365 Tagen vorher liegen.");
        var events = new List<CalEvent>();
        foreach (var v in list)
        {
            var s = Stand(v, heute);
            if (s.KuendigenBis is not { } bis || s.Ende is not { } ende) continue;
            var notes = $"Vertrag: {v.Name} bei {v.Anbieter}" + (v.Kundennummer.Trim().Length > 0 ? $", Kundennummer {v.Kundennummer.Trim()}" : "") +
                        $"\nDie Kündigung muss bis {Text(bis)} beim Anbieter sein – dann endet der Vertrag am {Text(ende)}.";
            events.Add(new CalEvent($"vertrag-{(v.Id.Length > 0 ? v.Id : Guid.NewGuid().ToString("N")[..8])}-{bis:yyyyMMdd}@vertraege", bis, bis.AddDays(1),
                $"Kündigen bis heute: {v.Name}", "", notes, AllDay: true, RemindMin: vorlaufTage * 24 * 60));
        }
        if (events.Count == 0) throw new InvalidOperationException("Kein laufender Vertrag mit Kündigungsfrist – nichts zu erinnern.");
        return Sys.Ics.Export(events);
    }

    /// <summary>Kündigungsschreiben als Brief-Entwurf (Vorlage „Kündigung“ des Brief-Kerns), zum berechneten Ende und hilfsweise zum nächstmöglichen Zeitpunkt.
    /// Wird nicht versendet – der Entwurf geht an „Briefe schreiben“ oder wird als PDF gespeichert.</summary>
    public static LetterDraft Kuendigung(Vertrag v, DateTime heute, LetterDraft? absender = null)
    {
        var t = LetterKit.TemplateById("kuendigung") ?? throw new InvalidOperationException("Die Brief-Vorlage „Kündigung“ fehlt.");
        var s = Stand(v, heute);
        var nr = v.Kundennummer.Trim().Length > 0 ? v.Kundennummer.Trim() : "[Vertragsnummer]";
        var zum = s.Ende is { } e && s.Ampel != "grau" ? $"fristgerecht zum {Text(e)}, hilfsweise zum nächstmöglichen Zeitpunkt" : "fristgerecht zum nächstmöglichen Zeitpunkt";
        var body = t.Body.Replace("fristgerecht zum nächstmöglichen Zeitpunkt", zum);
        body += "\n\nBitte senden Sie mir keine Angebote zur Vertragsverlängerung und widerrufen Sie eine erteilte Einzugsermächtigung zum Vertragsende.";
        var recipient = string.Join("\n", new[] { v.Anbieter.Trim(), v.Anschrift.Trim() }.Where(x => x.Length > 0));
        var d = new LetterDraft
        {
            Category = "kuendigung", Recipient = recipient.Length > 0 ? recipient : "[Anbieter und Anschrift]", Date = heute.Date,
            Subject = t.Subject.Replace("[Vertragsnummer]", nr) + (v.Name.Trim().Length > 0 ? $" ({v.Name.Trim()})" : ""), Reference = v.Kundennummer.Trim(), Body = body,
        };
        if (absender is not null) { d.SenderName = absender.SenderName; d.SenderStreet = absender.SenderStreet; d.SenderCity = absender.SenderCity; d.SenderContact = absender.SenderContact; d.Place = absender.Place; }
        return d;
    }

    /// <summary>Übersicht als CSV (Excel, Tabellen).</summary>
    public static string Csv(IEnumerable<Vertrag> list, DateTime heute)
    {
        static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("Name;Anbieter;Kategorie;Kundennummer;Beginn;Kosten;Intervall;Pro Monat;Nächstes Ende;Kündigen bis;Ampel\r\n");
        foreach (var v in list)
        {
            var s = Stand(v, heute);
            sb.Append(string.Join(";", Q(v.Name), Q(v.Anbieter), Q(v.Kategorie), Q(v.Kundennummer), Q(v.Beginn), v.Kosten.ToString("0.00", De), Q(Intervalle.FirstOrDefault(i => i.Id == v.Intervall).Name ?? v.Intervall),
                ProMonat(v).ToString("0.00", De), s.Ende is { } e ? Text(e) : "", s.KuendigenBis is { } b ? Text(b) : "", s.Ampel)).Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>Beispielordner mit neutralen Anbietern.</summary>
    public static VertragsOrdner Beispiel(DateTime heute) => new()
    {
        Vertraege =
        [
            new() { Id = "v1", Name = "Handyvertrag", Anbieter = "Beispiel Mobilfunk GmbH", Anschrift = "Postfach 1000\n12345 Musterstadt", Kategorie = "Handy & Internet", Kundennummer = "KD-123456",
                Beginn = Iso(heute.AddMonths(-23).AddDays(20)), LaufzeitMonate = 24, VerlaengerungMonate = 1, FristWert = 1, FristEinheit = "monate", Kosten = 29.99m, Intervall = "monat" },
            new() { Id = "v2", Name = "Hausratversicherung", Anbieter = "Muster Versicherung AG", Anschrift = "Versicherungsplatz 1\n54321 Beispielhausen", Kategorie = "Versicherung", Kundennummer = "HR-778899",
                Beginn = Iso(new DateTime(heute.Year - 2, 1, 1)), LaufzeitMonate = 12, VerlaengerungMonate = 12, FristWert = 3, FristEinheit = "monate", Kosten = 84m, Intervall = "jahr" },
            new() { Id = "v3", Name = "Streaming-Abo", Anbieter = "Beispiel Stream", Kategorie = "Streaming & Abos", Beginn = Iso(heute.AddMonths(-8)), LaufzeitMonate = 0, FristWert = 0, FristEinheit = "tage",
                ZumMonatsende = true, Kosten = 13.99m, Intervall = "monat" },
            new() { Id = "v4", Name = "Fitnessstudio", Anbieter = "Muster Fitness", Anschrift = "Sportweg 3\n12345 Musterstadt", Kategorie = "Fitness & Verein", Kundennummer = "M-4711",
                Beginn = Iso(heute.AddMonths(-5)), LaufzeitMonate = 12, VerlaengerungMonate = 1, FristWert = 4, FristEinheit = "wochen", Kosten = 39.90m, Intervall = "monat" },
        ],
    };
}
