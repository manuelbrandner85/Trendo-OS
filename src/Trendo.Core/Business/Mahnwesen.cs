using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Business;

// ===================== STUDIO-DUNNING-01 Mahnungen (T-0675) =====================
// Clean-Room nach dem Mahnwesen von lexoffice und sevDesk: überfällige Rechnungen erkennen (aus „Rechnungen & Angebote“ oder von Hand),
// Zahlungserinnerung → 1./2./3. Mahnung mit Fristen, Verzugszinsen nach § 288 BGB tagesgenau über den einstellbaren Basiszinssatz (§ 247 BGB),
// 40-€-Pauschale bei Unternehmern (§ 288 Abs. 5 BGB), Brief über den vorhandenen Brief-Kern (LetterDraft/LetterPdf), Verlauf je Rechnung.
// Es wird nichts versendet. Keine Rechtsberatung.

public sealed class BasisZins
{
    /// <summary>Gültig ab (JJJJ-MM-TT).</summary>
    public string Ab { get; set; } = "";
    /// <summary>Basiszinssatz in Prozent (kann negativ sein).</summary>
    public decimal Satz { get; set; }
}

public sealed class MahnStufe
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Neue Zahlungsfrist in Tagen ab Briefdatum.</summary>
    public int FristTage { get; set; } = 10;
    /// <summary>Mahngebühr (nur tatsächliche Kosten wie Porto und Papier; bei Unternehmern gilt stattdessen die 40-€-Pauschale).</summary>
    public decimal Gebuehr { get; set; }
    /// <summary>Verzugszinsen in diesem Brief ausweisen.</summary>
    public bool Zinsen { get; set; } = true;
}

public sealed class MahnEinstellungen
{
    public List<BasisZins> Basiszinsen { get; set; } = [.. Mahnwesen.BasiszinsTabelle.Select(x => new BasisZins { Ab = x.Ab, Satz = x.Satz })];
    public string BasiszinsQuelle { get; set; } = Mahnwesen.BasiszinsQuelle;
    public string BasiszinsStand { get; set; } = Mahnwesen.BasiszinsStand;
    /// <summary>§ 288 Abs. 1 Satz 2 BGB: 5 Prozentpunkte über dem Basiszinssatz.</summary>
    public decimal AufschlagVerbraucher { get; set; } = 5;
    /// <summary>§ 288 Abs. 2 BGB: 9 Prozentpunkte, wenn kein Verbraucher beteiligt ist.</summary>
    public decimal AufschlagUnternehmer { get; set; } = 9;
    /// <summary>§ 288 Abs. 5 BGB: Pauschale 40 € gegenüber Schuldnern, die keine Verbraucher sind.</summary>
    public decimal PauschaleUnternehmer { get; set; } = 40;
    /// <summary>Zinstage-Basis (365 = taggenau auf das Kalenderjahr).</summary>
    public int Zinstage { get; set; } = 365;
    public List<MahnStufe> Stufen { get; set; } =
    [
        new() { Id = "erinnerung", Name = "Zahlungserinnerung", FristTage = 7, Gebuehr = 0, Zinsen = false },
        new() { Id = "mahnung1", Name = "1. Mahnung", FristTage = 10, Gebuehr = 0, Zinsen = true },
        new() { Id = "mahnung2", Name = "2. Mahnung", FristTage = 7, Gebuehr = 2.50m, Zinsen = true },
        new() { Id = "mahnung3", Name = "3. Mahnung (letzte)", FristTage = 7, Gebuehr = 2.50m, Zinsen = true },
    ];
}

public sealed class MahnEintrag
{
    public int Stufe { get; set; }
    public string Name { get; set; } = "";
    public string Datum { get; set; } = "";
    public string FristBis { get; set; } = "";
    public decimal Offen { get; set; }
    public decimal Zinsen { get; set; }
    public decimal Gebuehr { get; set; }
    public decimal Pauschale { get; set; }
    public decimal Summe { get; set; }
    public string Datei { get; set; } = "";
}

/// <summary>Eine offene Forderung (Rechnung) mit Mahnverlauf.</summary>
public sealed class Forderung
{
    public string Id { get; set; } = "";
    public string Nummer { get; set; } = "";
    public string Anschrift { get; set; } = "";
    public bool Unternehmer { get; set; } = true;
    /// <summary>Offener Betrag (brutto) der Rechnung.</summary>
    public decimal Betrag { get; set; }
    public string Rechnungsdatum { get; set; } = "";
    public string Faellig { get; set; } = "";
    /// <summary>rechnungen (aus dem Studio „Rechnungen &amp; Angebote“) oder manuell.</summary>
    public string Quelle { get; set; } = "manuell";
    public string BezahltAm { get; set; } = "";
    public string Notiz { get; set; } = "";
    public List<MahnEintrag> Verlauf { get; set; } = [];
    public string KundeName => Anschrift.Split('\n').FirstOrDefault()?.Trim() ?? "";
}

public sealed class MahnAkte
{
    public Firma Absender { get; set; } = new();
    public MahnEinstellungen Einstellungen { get; set; } = new();
    public List<Forderung> Forderungen { get; set; } = [];
    public JsonObject ToJson() => BizJson.To(this);
    public static MahnAkte FromJson(JsonNode? n) => BizJson.From<MahnAkte>(n);
}

public sealed record ZinsAbschnitt(DateOnly Von, DateOnly Bis, int Tage, decimal Satz, decimal Zinsen);
public sealed record MahnStand(string Id, string Text, int? NaechsteStufe);

public static partial class Mahnwesen
{
    /// <summary>Basiszinssatz nach § 247 BGB, veröffentlicht von der Deutschen Bundesbank (abgerufen 16.11.2026). Neuere Werte im Studio eintragen.</summary>
    public static readonly (string Ab, decimal Satz)[] BasiszinsTabelle =
    [
        ("2022-01-01", -0.88m), ("2022-07-01", -0.88m), ("2023-01-01", 1.62m), ("2023-07-01", 3.12m), ("2024-01-01", 3.62m),
        ("2024-07-01", 3.37m), ("2025-01-01", 2.27m), ("2025-07-01", 1.27m), ("2026-01-01", 1.27m), ("2026-07-01", 1.52m),
    ];
    public const string BasiszinsQuelle = "Deutsche Bundesbank – Basiszinssatz nach § 247 BGB (bundesbank.de › Basiszinssatz)";
    public const string BasiszinsStand = "16.11.2026";
    public const string Hinweis = "Verzugszinsen nach § 288 BGB, Verzug nach § 286 BGB, Pauschale § 288 Abs. 5 BGB. Keine Rechtsberatung – Trendo versendet nichts.";

    public static decimal Basis(MahnEinstellungen e, DateOnly tag)
    {
        var hit = e.Basiszinsen.Select(b => (Ab: BizJson.Datum(b.Ab), b.Satz)).Where(b => b.Ab is { } a && a <= tag).OrderBy(b => b.Ab).LastOrDefault();
        if (hit.Ab is null) throw new InvalidOperationException($"Für den {BizJson.Text(tag)} ist kein Basiszinssatz eingetragen – unter „Einstellungen“ den Wert der Bundesbank nachtragen.");
        return hit.Satz;
    }

    public static decimal Zinssatz(MahnEinstellungen e, DateOnly tag, bool unternehmer) => Basis(e, tag) + (unternehmer ? e.AufschlagUnternehmer : e.AufschlagVerbraucher);

    /// <summary>Verzugszinsen tagesgenau: von <paramref name="ab"/> bis einschließlich <paramref name="bis"/>, abschnittsweise bei jedem Wechsel des Basiszinssatzes.
    /// Zinsen = Betrag × Satz × Tage / Zinstage (kaufmännisch auf Cent gerundet). Negative Sätze ergeben 0 € (Zinsen werden nie negativ).</summary>
    public static (decimal Zinsen, List<ZinsAbschnitt> Abschnitte) Verzugszinsen(MahnEinstellungen e, decimal betrag, DateOnly ab, DateOnly bis, bool unternehmer)
    {
        var parts = new List<ZinsAbschnitt>();
        if (bis < ab || betrag <= 0) return (0, parts);
        if (e.Zinstage is not (360 or 365)) throw new ArgumentException("Zinstage: 360 oder 365.");
        var changes = e.Basiszinsen.Select(b => BizJson.Datum(b.Ab)).OfType<DateOnly>().Where(d => d > ab && d <= bis).Distinct().OrderBy(d => d).ToList();
        var start = ab; decimal total = 0;
        foreach (var next in changes.Append(bis.AddDays(1)))
        {
            var end = next.AddDays(-1); var days = end.DayNumber - start.DayNumber + 1;
            var rate = Math.Max(0, Zinssatz(e, start, unternehmer));
            var z = betrag * rate / 100m * days / e.Zinstage;
            parts.Add(new(start, end, days, rate, Math.Round(z, 2, MidpointRounding.AwayFromZero))); total += z;
            start = next;
        }
        return (Math.Round(total, 2, MidpointRounding.AwayFromZero), parts);
    }

    /// <summary>Verzug ab dem Tag nach dem Zahlungsziel (kalendermäßig bestimmt, § 286 Abs. 2 Nr. 1 BGB).</summary>
    public static DateOnly? VerzugAb(Forderung f) => BizJson.Datum(f.Faellig)?.AddDays(1);

    public static MahnStand Stand(MahnAkte a, Forderung f, DateOnly heute)
    {
        if (f.BezahltAm.Length > 0) return new("bezahlt", "Bezahlt am " + BizJson.TextOf(f.BezahltAm), null);
        if (BizJson.Datum(f.Faellig) is not { } due) return new("fehler", "Fälligkeitsdatum fehlt.", null);
        if (heute <= due) return new("nicht-faellig", $"Noch nicht fällig (Zahlungsziel {BizJson.Text(due)})", null);
        var tage = heute.DayNumber - due.DayNumber;
        var n = f.Verlauf.Count;
        if (n > 0 && BizJson.Datum(f.Verlauf[^1].FristBis) is { } frist && heute <= frist)
            return new("frist", $"{f.Verlauf[^1].Name} vom {BizJson.TextOf(f.Verlauf[^1].Datum)} – Frist läuft bis {BizJson.Text(frist)}", null);
        if (n >= a.Einstellungen.Stufen.Count) return new("ende", $"Alle Mahnstufen ausgeschöpft ({tage} Tage überfällig) – gerichtliches Mahnverfahren prüfen. Trendo stellt keinen Antrag.", null);
        return new("bereit", $"{tage} Tage überfällig – nächster Schritt: {a.Einstellungen.Stufen[n].Name}", n);
    }

    /// <summary>Mahnung der nächsten Stufe berechnen (ohne sie zu speichern): Zinsen bis heute, Gebühr, einmalige Pauschale, Gesamtforderung inklusive früherer Gebühren.</summary>
    public static MahnEintrag Berechnen(MahnAkte a, Forderung f, DateOnly heute)
    {
        var st = Stand(a, f, heute);
        if (st.NaechsteStufe is not { } i) throw new InvalidOperationException(st.Text);
        var e = a.Einstellungen; var s = e.Stufen[i];
        if (s.FristTage is < 1 or > 90) throw new ArgumentException($"Frist für „{s.Name}“: 1 bis 90 Tage.");
        decimal z = 0;
        if (s.Zinsen && VerzugAb(f) is { } ab) z = Verzugszinsen(e, f.Betrag, ab, heute, f.Unternehmer).Zinsen;
        var pauschale = f.Unternehmer && s.Zinsen && f.Verlauf.All(v => v.Pauschale == 0) ? e.PauschaleUnternehmer : 0;
        var fee = f.Unternehmer ? 0 : s.Gebuehr; // § 288 Abs. 5 Satz 3 BGB: Pauschale wird auf Kosten angerechnet – keine zusätzliche Mahngebühr
        var before = f.Verlauf.Sum(v => v.Gebuehr + v.Pauschale);
        return new MahnEintrag
        {
            Stufe = i, Name = s.Name, Datum = BizJson.Iso(heute), FristBis = BizJson.Iso(heute.AddDays(s.FristTage)), Offen = f.Betrag, Zinsen = z, Gebuehr = fee, Pauschale = pauschale,
            Summe = f.Betrag + z + fee + pauschale + before,
        };
    }

    public static MahnEintrag Mahnen(MahnAkte a, Forderung f, DateOnly heute)
    {
        var m = Berechnen(a, f, heute); f.Verlauf.Add(m); return m;
    }

    [GeneratedRegex(@"\b(Frau|Herrn?)[ \t]+((?:Dr\.[ \t]+)?[A-ZÄÖÜ][\p{L}\-]+(?:[ \t]+[A-ZÄÖÜ][\p{L}\-]+)?)")] private static partial Regex PersonRx();
    /// <summary>Anrede aus der Anschrift („z. Hd. Frau Beispiel“ → „Sehr geehrte Frau Beispiel,“), sonst „Sehr geehrte Damen und Herren,“.</summary>
    public static string Anrede(string anschrift)
    {
        var m = PersonRx().Match(anschrift ?? "");
        if (!m.Success) return "Sehr geehrte Damen und Herren,";
        var name = m.Groups[2].Value.Split(' ').Last();
        return m.Groups[1].Value == "Frau" ? $"Sehr geehrte Frau {name}," : $"Sehr geehrter Herr {name},";
    }

    /// <summary>Mahnbrief als Entwurf für den vorhandenen Brief-Kern (LetterDraft → LetterPdf oder „Briefe schreiben“). Wird nicht versendet.</summary>
    public static LetterDraft Brief(MahnAkte a, Forderung f, MahnEintrag m, LetterDraft? absender = null)
    {
        var e = a.Einstellungen; var fa = a.Absender;
        var sb = new StringBuilder();
        var rd = BizJson.TextOf(f.Rechnungsdatum); var due = BizJson.TextOf(f.Faellig);
        if (m.Stufe == 0)
            sb.Append($"sicher ist es Ihrer Aufmerksamkeit entgangen: Unsere Rechnung {f.Nummer} vom {rd} war am {due} fällig. Bis heute konnten wir keinen Zahlungseingang feststellen.\n\n");
        else
            sb.Append($"leider haben wir zu unserer Rechnung {f.Nummer} vom {rd}, fällig am {due}, trotz " + (f.Verlauf.Count > 1 || m.Stufe > 1 ? "unserer bisherigen Schreiben" : "unserer Zahlungserinnerung") + " noch keine Zahlung erhalten. Sie befinden sich seit dem " + BizJson.Text(VerzugAb(f)!.Value) + " in Verzug.\n\n");
        sb.Append($"Offener Rechnungsbetrag: {BizJson.Euro(m.Offen)}\n");
        if (m.Zinsen > 0) sb.Append($"Verzugszinsen bis {BizJson.TextOf(m.Datum)} ({(f.Unternehmer ? e.AufschlagUnternehmer : e.AufschlagVerbraucher):0} Prozentpunkte über dem Basiszinssatz, § 288 BGB): {BizJson.Euro(m.Zinsen)}\n");
        var earlier = f.Verlauf.Where(x => x != m).Sum(x => x.Gebuehr + x.Pauschale);
        if (earlier > 0) sb.Append($"Kosten aus früheren Schreiben: {BizJson.Euro(earlier)}\n");
        if (m.Pauschale > 0) sb.Append($"Verzugspauschale nach § 288 Abs. 5 BGB: {BizJson.Euro(m.Pauschale)}\n");
        if (m.Gebuehr > 0) sb.Append($"Mahngebühr: {BizJson.Euro(m.Gebuehr)}\n");
        sb.Append($"Zu zahlen: {BizJson.Euro(m.Summe)}\n\n");
        sb.Append($"Bitte überweisen Sie den Betrag bis zum {BizJson.TextOf(m.FristBis)}" + (fa.Iban.Trim().Length > 0 ? $" auf das Konto IBAN {fa.Iban.Trim()}" : "") + $" unter Angabe der Rechnungsnummer {f.Nummer}.");
        if (m.Stufe == 0) sb.Append(" Sollten Sie die Zahlung bereits veranlasst haben, betrachten Sie dieses Schreiben bitte als gegenstandslos.");
        if (m.Stufe >= e.Stufen.Count - 1) sb.Append("\n\nDies ist unsere letzte Mahnung. Geht die Zahlung nicht fristgerecht ein, werden wir ohne weitere Ankündigung das gerichtliche Mahnverfahren einleiten.");
        var d = absender ?? new LetterDraft();
        d.Category = "mahnung"; d.Recipient = f.Anschrift; d.Date = BizJson.Datum(m.Datum)!.Value.ToDateTime(TimeOnly.MinValue);
        d.Subject = $"{m.Name} – Rechnung {f.Nummer} vom {rd}"; d.Reference = f.Nummer; d.Salutation = Anrede(f.Anschrift); d.Body = sb.ToString();
        if (fa.Name.Trim().Length > 0) { d.SenderName = fa.Name.Trim(); d.SenderStreet = fa.Strasse.Trim(); d.SenderCity = fa.PlzOrt.Trim(); if (fa.Email.Trim().Length > 0) d.SenderContact = fa.Email.Trim(); }
        return d;
    }

    /// <summary>Offene Forderungen aus den Belegen von „Rechnungen &amp; Angebote“ übernehmen (festgeschrieben, nicht storniert, noch offen). Bekannte werden aktualisiert,
    /// bezahlte als bezahlt markiert. Rückgabe: Zahl der neuen.</summary>
    public static int AusRechnungen(MahnAkte a, IEnumerable<Vorgang> belege, Firma? firma = null)
    {
        var n = 0;
        if (firma is not null && a.Absender.Name.Trim().Length == 0) a.Absender = BizJson.Clone(firma);
        foreach (var v in belege.Where(v => v.Art == "rechnung" && !v.Entwurf).GroupBy(v => v.Nummer).Select(g => g.Last()))
        {
            var f = a.Forderungen.FirstOrDefault(x => x.Nummer == v.Nummer);
            var offen = Rechnungen.Offen(v);
            if (f is null)
            {
                if (offen <= 0 || v.Storniert) continue;
                a.Forderungen.Add(f = new Forderung { Id = BizJson.NewId("f"), Nummer = v.Nummer, Quelle = "rechnungen" }); n++;
            }
            f.Anschrift = v.KundeAnschrift; f.Unternehmer = v.KundeUnternehmer; f.Rechnungsdatum = v.Datum; f.Faellig = Rechnungen.Faellig(v) is { } d ? BizJson.Iso(d) : f.Faellig;
            if (v.Storniert) { f.BezahltAm = ""; f.Notiz = "Rechnung storniert"; f.Betrag = 0; }
            else if (offen <= 0) { f.BezahltAm = v.BezahltAm.Length > 0 ? v.BezahltAm : f.BezahltAm.Length > 0 ? f.BezahltAm : BizJson.Iso(DateOnly.FromDateTime(DateTime.Today)); }
            else f.Betrag = offen;
        }
        a.Forderungen.RemoveAll(f => f.Quelle == "rechnungen" && f.Notiz == "Rechnung storniert" && f.Verlauf.Count == 0);
        return n;
    }

    /// <summary>Überfällige, noch unbezahlte Forderungen – älteste zuerst.</summary>
    public static List<Forderung> Ueberfaellig(MahnAkte a, DateOnly heute) =>
        [.. a.Forderungen.Where(f => f.BezahltAm.Length == 0 && BizJson.Datum(f.Faellig) is { } d && heute > d).OrderBy(f => f.Faellig)];

    public static string Csv(MahnAkte a, DateOnly heute)
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        var sb = new StringBuilder("Rechnung;Kunde;Betrag;Fällig;Tage überfällig;Stufe;Letztes Schreiben;Frist bis;Zinsen bis heute;Stand\r\n");
        foreach (var f in a.Forderungen.OrderBy(f => f.Faellig))
        {
            var due = BizJson.Datum(f.Faellig); var tage = due is { } d && heute > d && f.BezahltAm.Length == 0 ? heute.DayNumber - d.DayNumber : 0;
            decimal z = 0; try { if (tage > 0) z = Verzugszinsen(a.Einstellungen, f.Betrag, VerzugAb(f)!.Value, heute, f.Unternehmer).Zinsen; } catch (InvalidOperationException) { }
            var last = f.Verlauf.LastOrDefault();
            sb.Append(string.Join(";", BizJson.Csv(f.Nummer), BizJson.Csv(f.KundeName), f.Betrag.ToString("0.00", de), BizJson.TextOf(f.Faellig), tage, BizJson.Csv(last?.Name ?? ""), last is null ? "" : BizJson.TextOf(last.Datum),
                last is null ? "" : BizJson.TextOf(last.FristBis), z.ToString("0.00", de), BizJson.Csv(Stand(a, f, heute).Text))).Append("\r\n");
        }
        return sb.ToString();
    }

    public static MahnAkte Beispiel(DateOnly heute) => new()
    {
        Absender = new() { Name = "Erika Muster Webdesign", Strasse = "Musterstraße 1", PlzOrt = "12345 Musterstadt", Email = "rechnung@example.org", Iban = "DE89370400440532013000" },
        Forderungen =
        [
            new() { Id = "f1", Nummer = "RE-2026-041", Anschrift = "Beispiel GmbH\nz. Hd. Frau Beispiel\nHauptstraße 5\n54321 Beispielhausen", Unternehmer = true, Betrag = 1190m,
                Rechnungsdatum = BizJson.Iso(heute.AddDays(-45)), Faellig = BizJson.Iso(heute.AddDays(-31)) },
            new() { Id = "f2", Nummer = "RE-2026-047", Anschrift = "Max Mustermann\nGartenweg 3\n12345 Musterstadt", Unternehmer = false, Betrag = 238m,
                Rechnungsdatum = BizJson.Iso(heute.AddDays(-20)), Faellig = BizJson.Iso(heute.AddDays(-6)) },
            new() { Id = "f3", Nummer = "RE-2026-052", Anschrift = "Muster KG\nIndustrieweg 9\n12345 Musterstadt", Unternehmer = true, Betrag = 595m,
                Rechnungsdatum = BizJson.Iso(heute.AddDays(-5)), Faellig = BizJson.Iso(heute.AddDays(9)) },
        ],
    };
}
