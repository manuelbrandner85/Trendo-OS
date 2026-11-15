using System.Globalization;
using System.Text;
using Trendo.Core.Business;

namespace Trendo.Core.PromptAction;

/// <summary>STUDIO-INVOICE-01 Rechnungen &amp; Angebote: Entwurf mit Positionen (Menge, Einheit, Preis, USt, Rabatt), Summen je Steuersatz und Pflichtangaben-Prüfung
/// nach § 14 Abs. 4 UStG. Vergibt keine Nummer, bucht nichts, versendet nichts – festgeschrieben wird im Studio.</summary>
public sealed class RechnungEntwurfAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-INVOICE-01";
    public string ActionId => "rechnungen.entwurf";
    public string Label => "Rechnung oder Angebot entwerfen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "BusinessGeldTests.Rechnung_Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["position"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    /// <summary>„Beratung | 6 | Std. | 85 | 19 | 10“ (Text | Menge | Einheit | Preis netto | USt % | Rabatt %); Einheit, USt und Rabatt dürfen fehlen.</summary>
    public static List<Position> Positionen(string block)
    {
        var res = new List<Position>();
        foreach (var raw in (block ?? "").Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = raw.Split('|').Select(x => x.Trim()).ToArray();
            if (p.Length < 3) throw new FormatException($"Position „{raw}“ bitte als „Leistung | Menge | Einheit | Preis | USt | Rabatt“, z. B. „Beratung | 6 | Std. | 85 | 19“.");
            var hasUnit = p.Length >= 4 && !BizJson.TryBetrag(p[2], out _);
            var off = hasUnit ? 1 : 0;
            if (!BizJson.TryBetrag(p[1], out var menge) || menge == 0) throw new FormatException($"„{raw}“: Die Menge fehlt oder ist 0.");
            if (!BizJson.TryBetrag(p[2 + off], out var preis) || preis < 0) throw new FormatException($"„{raw}“: Den Preis verstehe ich nicht.");
            decimal ust = 19, rab = 0;
            if (p.Length > 3 + off && !BizJson.TryBetrag(p[3 + off].TrimEnd('%', ' '), out ust)) throw new FormatException($"„{raw}“: USt bitte 19, 7 oder 0.");
            if (p.Length > 4 + off && !BizJson.TryBetrag(p[4 + off].TrimEnd('%', ' '), out rab)) throw new FormatException($"„{raw}“: Rabatt in Prozent.");
            res.Add(new Position { Text = p[0], Menge = menge, Einheit = hasUnit ? p[2] : "Stück", Preis = preis, Ust = ust, Rabatt = rab });
        }
        return res;
    }

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "position") ? [] : [new("position", "position", "Was soll auf die Rechnung? Schreib z. B. „Position: Beratung | 6 | Std. | 85 | 19“ – dazu „Kunde: …“ und „Verkaeufer: …“.", true)];

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "position")) try { if (Positionen(FieldKit.Text(parameters, "position")).Any(p => !Rechnungen.Saetze.Contains(p.Ust))) f.Add(new("position", "USt nur 19, 7 oder 0 %.", true)); } catch (FormatException e) { f.Add(new("position", e.Message, true)); }
        if (FieldKit.Has(parameters, "art") && !Rechnungen.Arten.Any(a => a.Id == FieldKit.Text(parameters, "art").ToLowerInvariant() && a.Id != "storno")) f.Add(new("art", "Art: rechnung, angebot oder auftrag.", true));
        if (FieldKit.Has(parameters, "datum") && BizJson.Datum(FieldKit.Text(parameters, "datum")) is null) f.Add(new("datum", "Datum als TT.MM.JJJJ.", true));
        return f;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters;
        var heute = BizJson.Datum(FieldKit.Text(p, "datum")) ?? DateOnly.FromDateTime(DateTime.Today);
        static (string Name, string Tail) Split(string block) { var l = block.Replace(", ", "\n").Split('\n'); return (l[0].Trim(), string.Join("\n", l.Skip(1).Select(x => x.Trim()))); }
        var (sn, sr) = Split(FieldKit.Text(p, "verkaeufer", "absender"));
        var lines = sr.Split('\n');
        var b = new RechnungsBuch { Firma = new Firma { Name = sn, Strasse = lines.FirstOrDefault() ?? "", PlzOrt = lines.Skip(1).FirstOrDefault() ?? "", SteuerNr = FieldKit.Text(p, "steuernummer", "steuernr"), UstId = FieldKit.Text(p, "ustid", "ust-idnr"), Kleinunternehmer = FieldKit.Yes(p, "kleinunternehmer") } };
        var art = FieldKit.Has(p, "art") ? FieldKit.Text(p, "art").ToLowerInvariant() : "rechnung";
        var v = Rechnungen.Neu(b, art, heute);
        v.KundeAnschrift = FieldKit.Text(p, "kunde", "kaeufer").Replace(", ", "\n");
        v.Positionen = Positionen(FieldKit.Text(p, "position"));
        if (FieldKit.Has(p, "leistung")) v.Leistung = FieldKit.Text(p, "leistung");
        v.Befreiung = FieldKit.Text(p, "befreiung");
        var s = Rechnungen.Summe(v); var check = Rechnungen.Pruefen(b, v);
        var sb = new StringBuilder();
        sb.Append($"{Rechnungen.Name(art)} (Entwurf, ohne Nummer) vom {BizJson.Text(heute)}\n");
        sb.Append("An: ").Append(v.KundeAnschrift.Replace("\n", ", ")).Append('\n');
        var i = 0;
        foreach (var x in v.Positionen)
            sb.Append($"{++i}. {x.Text}: {BizJson.Num(x.Menge)} {x.Einheit} × {BizJson.Euro(x.Preis)}" + (x.Rabatt != 0 ? $" − {BizJson.Num(x.Rabatt)} %" : "") + (v.Kleinunternehmer ? "" : $" ({BizJson.Num(x.Ust)} % USt)") + $" = {BizJson.Euro(Rechnungen.PosNetto(x))}\n");
        sb.Append($"Summe netto {BizJson.Euro(s.Netto)}\n");
        foreach (var g in s.JeSatz.Where(_ => !v.Kleinunternehmer)) sb.Append($"USt {BizJson.Num(g.Satz)} % auf {BizJson.Euro(g.Netto)}: {BizJson.Euro(g.Steuer)}\n");
        if (v.Kleinunternehmer) sb.Append(Rechnungen.KleinunternehmerHinweis).Append('\n');
        sb.Append($"Gesamtbetrag {BizJson.Euro(s.Brutto)}\n\nPflichtangaben (§ 14 Abs. 4 UStG):\n");
        foreach (var c in check) sb.Append(c.Ok ? "✓ " : "✗ ").Append(c.Text).Append(c.Ok ? "" : " – " + c.Hinweis).Append('\n');
        sb.Append("\nNummer wird erst beim Festschreiben im Studio vergeben (lückenlos). Nicht gebucht, nichts versendet. Keine Steuerberatung.\n");
        var qa = new QaResult(ActionId, s.JeSatz.Count > 0, $"{v.Positionen.Count} Positionen, {check.Count(c => !c.Ok)} fehlende Pflichtangaben.", "EUR", BizJson.Euro(s.Brutto), "Rechnungen");
        return FieldKit.Save(call, FieldKit.Slot("rechnung", call.Version, "entwurf.txt"), sb.ToString(), $"{Rechnungen.Name(art)}: {BizJson.Euro(s.Brutto)}", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Festgeschrieben, gebucht und verschickt wird im Studio von dir – die KI legt nur den Entwurf an.");
}

/// <summary>STUDIO-DUNNING-01 Mahnungen: Verzugszinsen nach § 288 BGB tagesgenau (Basiszinssatz-Tabelle mit Stand) und 40-€-Pauschale bei Unternehmern. Versendet nichts.</summary>
public sealed class MahnZinsAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-DUNNING-01";
    public string ActionId => "mahnung.zinsen";
    public string Label => "Verzugszinsen berechnen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "BusinessGeldTests.Mahnung_Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["betrag", "faellig"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var q = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "betrag")) q.Add(new("betrag", "betrag", "Wie hoch ist die offene Rechnung? Schreib z. B. „Betrag: 1190“.", true));
        if (!FieldKit.Has(parameters, "faellig")) q.Add(new("faellig", "faellig", "Wann war sie fällig? Schreib z. B. „Faellig: 15.10.2026“, dazu „Kunde: Unternehmer“ oder „Verbraucher“.", true));
        return q;
    }
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "betrag") && (!BizJson.TryBetrag(FieldKit.Text(parameters, "betrag"), out var b) || b <= 0)) f.Add(new("betrag", "Betrag als Zahl über 0, z. B. 1190,00.", true));
        foreach (var k in new[] { "faellig", "stichtag" })
            if (FieldKit.Has(parameters, k) && BizJson.Datum(FieldKit.Text(parameters, k)) is null) f.Add(new(k, "Datum als TT.MM.JJJJ.", true));
        return f;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters; var e = new MahnEinstellungen();
        var betrag = BizJson.Betrag(FieldKit.Text(p, "betrag"));
        var due = BizJson.Datum(FieldKit.Text(p, "faellig"))!.Value; var heute = BizJson.Datum(FieldKit.Text(p, "stichtag")) ?? DateOnly.FromDateTime(DateTime.Today);
        var b2b = !FieldKit.Text(p, "kunde", "schuldner").ToLowerInvariant().StartsWith("verbrauch");
        var sb = new StringBuilder();
        sb.Append($"Offen: {BizJson.Euro(betrag)}, fällig am {BizJson.Text(due)}, Stichtag {BizJson.Text(heute)}, Schuldner: {(b2b ? "Unternehmer" : "Verbraucher")}\n");
        QaResult qa;
        if (heute <= due) { sb.Append("Noch nicht im Verzug – Zinsen erst ab dem Tag nach dem Zahlungsziel.\n"); qa = new(ActionId, true, "nicht im Verzug", "EUR", "0", "Mahnwesen"); }
        else
        {
            var (z, parts) = Mahnwesen.Verzugszinsen(e, betrag, due.AddDays(1), heute, b2b);
            sb.Append($"Verzug seit {BizJson.Text(due.AddDays(1))} ({heute.DayNumber - due.DayNumber} Tage)\n");
            foreach (var a in parts) sb.Append($"{BizJson.Text(a.Von)}–{BizJson.Text(a.Bis)}: {a.Tage} Tage × {BizJson.Num(a.Satz, "0.00")} % = {BizJson.Euro(a.Zinsen)}\n");
            sb.Append($"Verzugszinsen: {BizJson.Euro(z)}\n");
            if (b2b) sb.Append($"Pauschale § 288 Abs. 5 BGB: {BizJson.Euro(e.PauschaleUnternehmer)}\n");
            sb.Append($"Gesamt: {BizJson.Euro(betrag + z + (b2b ? e.PauschaleUnternehmer : 0))}\n");
            qa = new(ActionId, parts.Count > 0, $"{parts.Count} Zinsabschnitte", "EUR", BizJson.Euro(z), "Mahnwesen");
        }
        sb.Append($"\nBasiszinssatz: {e.BasiszinsQuelle}, Stand {e.BasiszinsStand}. {Mahnwesen.Hinweis}\n");
        return FieldKit.Save(call, FieldKit.Slot("mahnung", call.Version, "zinsen.txt"), sb.ToString(), qa.Inputs ?? "", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Gemahnt wird von dir – Trendo schreibt den Brief, versendet aber nichts.");
}

/// <summary>STUDIO-RECEIPTS-01 Belege &amp; Spesen: Datum, Betrag, Händler und USt aus Belegtext erkennen, Kategorie und Ablagename vorschlagen. Bucht nichts.</summary>
public sealed class BelegErkennenAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-RECEIPTS-01";
    public string ActionId => "beleg.erkennen";
    public string Label => "Beleg auslesen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "BusinessGeldTests.Beleg_Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["text"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "text") ? [] : [new("text", "text", "Füge den Text des Belegs ein: „Text:“ und darunter die Zeilen vom Kassenbon oder der Rechnung.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "text") && FieldKit.Text(parameters, "text").Length < 8 ? [new("text", "Der Text ist zu kurz für einen Beleg.", true)] : [];

    public AdapterOutcome Produce(AdapterCall call)
    {
        var heute = BizJson.Datum(FieldKit.Text(call.Parameters, "stichtag")) ?? DateOnly.FromDateTime(DateTime.Today);
        var e = BelegKit.Erkennen(FieldKit.Text(call.Parameters, "text"), null, heute);
        var b = BelegKit.Uebernehmen(new Beleg(), e, "Text");
        var sb = new StringBuilder();
        sb.Append("Datum: ").Append(e.Datum is { } d ? BizJson.Text(d) : "–").Append('\n');
        sb.Append("Händler: ").Append(e.Haendler.Length > 0 ? e.Haendler : "–").Append('\n');
        sb.Append("Betrag: ").Append(e.Betrag is { } a ? BizJson.Euro(a) : "–").Append('\n');
        sb.Append("USt: ").Append(e.UstSatz is { } r ? BizJson.Num(r) + " %" : "–").Append(e.Ust is { } u ? " = " + BizJson.Euro(u) : "").Append('\n');
        var konto = BelegKit.Standard.FirstOrDefault(k => k.Name == e.Kategorie)?.Konto ?? "";
        sb.Append("Kategorie: ").Append(e.Kategorie).Append(konto.Length > 0 ? $" (Konto-Vorschlag SKR03 {konto})" : "").Append('\n');
        sb.Append("Ablagename: ").Append(BelegKit.Ablagename(b, ".pdf")).Append('\n');
        foreach (var h in e.Hinweise) sb.Append("◐ ").Append(h).Append('\n');
        sb.Append("\nNicht gebucht. Werte bitte im Studio prüfen. Keine Steuerberatung.\n");
        var qa = new QaResult(ActionId, e.Betrag is not null && e.Datum is not null, $"{e.Hinweise.Count} Hinweise", "EUR", e.Betrag is { } x ? BizJson.Euro(x) : "", "BelegKit");
        return FieldKit.Save(call, FieldKit.Slot("beleg", call.Version, "beleg.txt"), sb.ToString(), $"{e.Haendler} {(e.Betrag is { } y ? BizJson.Euro(y) : "")}".Trim(), qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Gebucht und erstattet wird von dir – Trendo liest den Beleg nur aus.");
}

/// <summary>STUDIO-PRICING-01 Preise kalkulieren: Zuschlagskalkulation vom Material bis zum Bruttopreis, optional Rückwärtsrechnung vom Wunschpreis.</summary>
public sealed class PreisKalkulationAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-PRICING-01";
    public string ActionId => "preis.kalkulieren";
    public string Label => "Preis kalkulieren";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "BusinessGeldTests.Preis_Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["material"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    static readonly string[] Nums = ["material", "materialgk", "stunden", "stundensatz", "fertigungsgk", "gemeinkosten", "gewinn", "ust", "wunschpreis"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "material") ? [] : [new("material", "material", "Was kostet das Material je Stück? Schreib z. B. „Material: 12“, dazu „Stunden: 1,5“, „Stundensatz: 45“ und „Gewinn: 20“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        foreach (var k in Nums) if (FieldKit.Has(parameters, k) && !BizJson.TryBetrag(FieldKit.Text(parameters, k).TrimEnd('%', '€', ' '), out _)) f.Add(new(k, $"„{k}“ als Zahl, z. B. 12,50.", true));
        return f;
    }

    static decimal N(IReadOnlyDictionary<string, string> p, string k, decimal d) => BizJson.TryBetrag(FieldKit.Text(p, k).TrimEnd('%', '€', ' '), out var v) ? v : d;

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters;
        var k = new Kalkulation { Name = FieldKit.Has(p, "name") ? FieldKit.Text(p, "name") : "Produkt", Material = N(p, "material", 0), MaterialGk = N(p, "materialgk", 0), Stunden = N(p, "stunden", 0),
            Stundensatz = N(p, "stundensatz", 0), FertigungGk = N(p, "fertigungsgk", 0), VerwaltungGk = N(p, "gemeinkosten", 0), Gewinn = N(p, "gewinn", 20), Ust = N(p, "ust", 19), Kleinunternehmer = FieldKit.Yes(p, "kleinunternehmer") };
        var text = PreisKit.Bericht(new Preisblatt { Kalkulation = k });
        if (FieldKit.Has(p, "wunschpreis"))
        {
            var r = PreisKit.Rueckwaerts(k, N(p, "wunschpreis", 0));
            text += $"\nRückwärts vom Wunschpreis {BizJson.Euro(N(p, "wunschpreis", 0))} brutto: netto {BizJson.Euro(r.Netto)}, Gewinn {BizJson.Euro(r.Gewinn)} ({BizJson.Num(r.GewinnProzent)} % auf die Selbstkosten)." +
                    $" Für {BizJson.Num(k.Gewinn)} % Gewinn dürften die Selbstkosten höchstens {BizJson.Euro(r.MaxSelbstkosten)} sein (Material höchstens {BizJson.Euro(r.MaxMaterial)}).\n";
        }
        text += "\nRechenhilfe – keine Steuer- oder Unternehmensberatung.\n";
        var s = PreisKit.Rechnen(k);
        var qa = new QaResult(ActionId, s.Netto >= s.Selbstkosten || k.Gewinn < 0, $"Selbstkosten {BizJson.Euro(s.Selbstkosten)}, netto {BizJson.Euro(s.Netto)}", "EUR", BizJson.Euro(s.Brutto), "PreisKit");
        return FieldKit.Save(call, FieldKit.Slot("preis", call.Version, "kalkulation.txt"), text, $"{k.Name}: {BizJson.Euro(s.Brutto)} brutto", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Preise setzt du selbst – Trendo rechnet nur.");
}
