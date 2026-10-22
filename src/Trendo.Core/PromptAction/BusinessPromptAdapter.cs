using System.Globalization;
using System.Net;
using System.Text;

namespace Trendo.Core.PromptAction;

/// <summary>SPEC-11: Rechnung oder Angebot als HTML. Decimal, nicht gebucht, nichts bezahlt.</summary>
public sealed class InvoicePromptAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-11";
    public string ActionId => "rechnung.schreiben";
    public string Label => "Rechnung oder Angebot";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Rechnung_rechnet_und_bucht_nicht";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["verkaeufer", "kaeufer", "position"];
    public IReadOnlyList<string> OutputKinds => ["html"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = MoneyFields.Read(prompt, known);
        return MoneyFields.WantsPay(fields) ? null : new ActionDraft(ActionId, fields);
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => MoneyFields.MissingInvoice(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => MoneyFields.ValidateInvoice(parameters);
    public AdapterOutcome Produce(AdapterCall call) => MoneyFields.WriteInvoice(call);
    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Es wurde nichts gebucht und nichts bezahlt."], null);
}

/// <summary>Zahlung und Buchung bleiben aus. Auch „Zahlen: ja“ und eine Freigabe im Text buchen nichts.</summary>
public sealed class MoneyPayAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-11";
    public string ActionId => "geld.zahlen";
    public string Label => "Zahlung";
    public SideEffectClass Effect => SideEffectClass.Payment;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Blocked;
    public string Evidence => "PromptActionTests.Geld_zahlt_nichts";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["betrag"];
    public IReadOnlyList<string> OutputKinds => [];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = MoneyFields.Read(prompt, known);
        return MoneyFields.WantsPay(fields) ? new ActionDraft(ActionId, fields) : null;
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];

    public AdapterOutcome Produce(AdapterCall call) =>
        new(false, [], [], null, ["Es wurde nichts bezahlt und nichts gebucht."], ["Zahlung ist nicht angeschlossen."], null);

    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Es wurde nichts bezahlt und nichts gebucht."], null);
}

/// <summary>STUDIO-ONLINE-BUSINESS-01: Bauplan aus den vorhandenen Schätzwerten. Keine Buchung.</summary>
public sealed class BusinessPlanAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-ONLINE-BUSINESS-01";
    public string ActionId => "geschaeft.bewerten";
    public string Label => "Geschäftsidee bewerten";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Geschaeft_bewertet_ohne_buchung";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["name", "preis", "kosten", "menge"];
    public IReadOnlyList<string> OutputKinds => ["md"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) =>
        new(ActionId, MoneyFields.Read(prompt, known));

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => MoneyFields.MissingPlan(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => MoneyFields.ValidatePlan(parameters);
    public AdapterOutcome Produce(AdapterCall call) => MoneyFields.WritePlan(call);
    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Es wurde nichts verkauft und nichts bezahlt."], null);
}

static class MoneyFields
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    public static Dictionary<string, string> Read(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = PromptFields.Read(prompt);
        foreach (var kv in known) fields[kv.Key] = kv.Value;
        return fields;
    }

    public static bool WantsPay(IReadOnlyDictionary<string, string> fields) =>
        Yes(fields, "zahlen") || Yes(fields, "ueberweisen") || Yes(fields, "buchen");

    public static IReadOnlyList<ClarificationQuestion> MissingInvoice(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        void Need(string field, string label)
        {
            if (Text(parameters, field).Length == 0)
                list.Add(new(field, field, label + " fehlt. Schreib es als „" + label + ": …“.", true));
        }
        Need("verkaeufer", "Verkaeufer");
        if (Text(parameters, "kaeufer").Length == 0 && Text(parameters, "kunde").Length == 0)
            list.Add(new("kaeufer", "kaeufer", "Kaeufer fehlt. Schreib es als „Kaeufer: …“.", true));
        if (Text(parameters, "position").Length == 0 && Text(parameters, "leistung").Length == 0)
            list.Add(new("position", "position", "Position fehlt. Zum Beispiel „Position: Beratung | 2 | 10“.", true));
        if (!Yes(parameters, "kleinunternehmer") && Text(parameters, "mwst").Length == 0)
            list.Add(new("mwst", "mwst", "MwSt fehlt. Zum Beispiel „MwSt: 19“ oder „Kleinunternehmer: ja“.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> ValidateInvoice(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ValidationFinding>();
        if (!TryBuild(parameters, out _, out var error))
            list.Add(new(error.Field, error.Message, true));
        return list;
    }

    public static AdapterOutcome WriteInvoice(AdapterCall call)
    {
        if (!TryBuild(call.Parameters, out var built, out var error))
            return new(false, [], [], null, [], [error.Message], null);
        var invoice = built.Invoice;
        var heading = built.Heading;
        var relative = "finanzen/v" + call.Version + "/" + invoice.Number + ".html";
        var full = call.Projects.ResolveInProject(call.ProjectId, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var html = Finance.InvoiceHtml(invoice, heading);
        File.WriteAllText(full, html);
        var (net, vat, gross) = Finance.Totals(invoice);
        var read = File.ReadAllText(full);
        var money = Euro(gross);
        var ok = read.Contains("<h2>" + heading + " " + WebUtility.HtmlEncode(invoice.Number) + "</h2>", StringComparison.Ordinal)
            && read.Contains(WebUtility.HtmlEncode(invoice.Seller), StringComparison.Ordinal)
            && read.Contains(WebUtility.HtmlEncode(invoice.Buyer), StringComparison.Ordinal)
            && read.Contains(money, StringComparison.Ordinal)
            && invoice.Lines.All(l => read.Contains(WebUtility.HtmlEncode(l.Text), StringComparison.Ordinal))
            && (!invoice.SmallBusiness || read.Contains("§ 19 UStG", StringComparison.Ordinal));
        var preview = Preview(invoice, heading, net, vat, gross);
        return new(true, [new ProducedFile(relative, "html", true)],
            [new QaResult("rechnung.summe", ok, ok ? "Netto, MwSt und Gesamt stehen in der HTML-Datei." : "Die HTML-Datei stimmt nicht mit der Rechnung überein.", "EUR", money, "Finance.Totals")],
            preview,
            ["Nicht gebucht. Es wurde nichts bezahlt. Keine Steuerberatung."], [], null);
    }

    public static IReadOnlyList<ClarificationQuestion> MissingPlan(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        void Need(string field, string label)
        {
            if (Text(parameters, field).Length == 0)
                list.Add(new(field, field, label + " fehlt. Schreib es als „" + label + ": …“.", true));
        }
        Need("name", "Name");
        Need("problem", "Problem");
        Need("zielgruppe", "Zielgruppe");
        Need("loesung", "Loesung");
        Need("modell", "Modell");
        Need("preis", "Preis");
        Need("kosten", "Kosten");
        Need("fix", "Fix");
        Need("menge", "Menge");
        Need("stunden", "Stunden");
        Need("startgeld", "Startgeld");
        Need("mitbewerber", "Mitbewerber");
        Need("koennen", "Koennen");
        return list;
    }

    public static IReadOnlyList<ValidationFinding> ValidatePlan(IReadOnlyDictionary<string, string> parameters)
    {
        if (!TryPlan(parameters, out _, out var error))
            return [new(error.Field, error.Message, true)];
        return [];
    }

    public static AdapterOutcome WritePlan(AdapterCall call)
    {
        if (!TryPlan(call.Parameters, out var idea, out var error))
            return new(false, [], [], null, [], [error.Message], null);
        var text = BusinessStudio.Blueprint(idea).Replace("\r\n", "\n");
        var relative = "geschaeft/v" + call.Version + "/" + DevStudio.SafeName(idea.Name) + ".md";
        var full = call.Projects.ResolveInProject(call.ProjectId, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
        var read = File.ReadAllText(full).Replace("\r\n", "\n");
        var verdict = BusinessStudio.Evaluate(idea);
        var ok = read == text && read.Contains(verdict.Decision, StringComparison.Ordinal) && read.Contains(idea.Name, StringComparison.Ordinal);
        var profit = BusinessStudio.ProfitMonth(idea).ToString("N2", De) + " €";
        var preview = idea.Name + ": " + verdict.Decision + " (" + verdict.Score + "/100). Gewinn im Monat " + profit + ". Nicht gebucht.";
        return new(true, [new ProducedFile(relative, "md", true)],
            [new QaResult("geschaeft.bauplan", ok, ok ? "Der Bauplan entspricht der Bewertung aus deinen Zahlen." : "Der Bauplan stimmt nicht mit der Bewertung überein.", "EUR", profit, "BusinessStudio.Blueprint")],
            preview,
            ["Schätzung aus deinen Zahlen. Keine Buchung, keine Zahlung, keine Rechtsberatung."], [], null);
    }

    static string Preview(Invoice invoice, string heading, decimal net, decimal vat, decimal gross)
    {
        var sb = new StringBuilder();
        sb.Append(heading).Append(' ').Append(invoice.Number).Append('\n');
        sb.Append("Von ").Append(invoice.Seller.Replace("\n", ", ")).Append('\n');
        sb.Append("An ").Append(invoice.Buyer.Replace("\n", ", ")).Append('\n');
        foreach (var line in invoice.Lines)
        {
            var sum = Math.Round(line.Qty * line.UnitNet, 2);
            sb.Append(line.Text).Append(": ").Append(line.Qty.ToString("0.##", De)).Append(" × ").Append(Euro(line.UnitNet)).Append(" = ").Append(Euro(sum)).Append('\n');
        }
        sb.Append("Netto ").Append(Euro(net)).Append('\n');
        if (invoice.SmallBusiness) sb.Append("Keine MwSt (§ 19 UStG)\n");
        else sb.Append("MwSt ").Append(invoice.VatRate.ToString("0.##", De)).Append(" %: ").Append(Euro(vat)).Append('\n');
        sb.Append("Gesamt ").Append(Euro(gross)).Append('\n');
        sb.Append("Nicht gebucht. Es wurde nichts bezahlt.");
        return sb.ToString();
    }

    static bool TryBuild(IReadOnlyDictionary<string, string> parameters, out Built built, out FieldError error)
    {
        built = default;
        error = default;
        var art = Text(parameters, "art");
        string heading;
        if (art.Length == 0 || art.Equals("Rechnung", StringComparison.OrdinalIgnoreCase)) heading = "Rechnung";
        else if (art.Equals("Angebot", StringComparison.OrdinalIgnoreCase)) heading = "Angebot";
        else { error = new("art", "Art ist „Rechnung“ oder „Angebot“."); return false; }

        var seller = Text(parameters, "verkaeufer");
        var buyer = Text(parameters, "kaeufer");
        if (buyer.Length == 0) buyer = Text(parameters, "kunde");
        if (!TryLines(parameters, out var lines, out error)) return false;
        if (lines.Count == 0) { error = new("position", "Mindestens eine Position. Zum Beispiel „Position: Beratung | 2 | 10“."); return false; }

        var small = Yes(parameters, "kleinunternehmer");
        decimal vat = 0;
        if (!small)
        {
            if (!TryMoney(Text(parameters, "mwst"), out vat) || vat < 0 || vat > 100)
            {
                error = new("mwst", "MwSt verstehe ich nicht. Zum Beispiel 19.");
                return false;
            }
        }

        var dateText = Text(parameters, "datum");
        DateOnly date;
        if (dateText.Length == 0) date = DateOnly.FromDateTime(DateTime.Today);
        else if (!DateOnly.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            && !DateOnly.TryParse(dateText, De, DateTimeStyles.None, out date))
        {
            error = new("datum", "Das Datum verstehe ich nicht. Zum Beispiel 2026-10-22.");
            return false;
        }

        var number = Text(parameters, "nummer");
        if (number.Length == 0)
        {
            number = Finance.NextInvoiceNumber([], date);
            if (heading == "Angebot") number = "AN-" + number["RE-".Length..];
        }
        else if (number != Path.GetFileName(number) || number.Contains("..", StringComparison.Ordinal))
        {
            error = new("nummer", "Die Nummer darf kein Ordnername sein.");
            return false;
        }

        var tax = Text(parameters, "steuernummer");
        built = new(new Invoice(number, date, seller, buyer, lines, vat, small, tax), heading);
        return true;
    }

    static bool TryLines(IReadOnlyDictionary<string, string> parameters, out List<InvoiceLine> lines, out FieldError error)
    {
        lines = [];
        error = default;
        var block = Text(parameters, "position");
        if (block.Length > 0)
        {
            foreach (var raw in block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = raw.Split('|');
                if (parts.Length < 3) parts = raw.Split(';');
                if (parts.Length < 3) { error = new("position", "Position so schreiben: „Beratung | 2 | 10“."); return false; }
                if (!TryMoney(parts[1], out var qty) || qty <= 0) { error = new("position", "Die Menge muss größer als 0 sein."); return false; }
                if (!TryMoney(parts[2], out var price) || price < 0) { error = new("position", "Den Preis verstehe ich nicht."); return false; }
                var label = parts[0].Trim();
                if (label.Length == 0) { error = new("position", "Die Leistung fehlt."); return false; }
                lines.Add(new(label, qty, price));
            }
            return true;
        }
        if (Text(parameters, "leistung").Length == 0 && Text(parameters, "menge").Length == 0 && Text(parameters, "preis").Length == 0)
            return true;
        if (!TryMoney(Text(parameters, "menge"), out var oneQty) || oneQty <= 0) { error = new("menge", "Die Menge muss größer als 0 sein."); return false; }
        if (!TryMoney(Text(parameters, "preis"), out var onePrice) || onePrice < 0) { error = new("preis", "Den Preis verstehe ich nicht."); return false; }
        var one = Text(parameters, "leistung");
        if (one.Length == 0) { error = new("leistung", "Die Leistung fehlt."); return false; }
        lines.Add(new(one, oneQty, onePrice));
        return true;
    }

    static bool TryPlan(IReadOnlyDictionary<string, string> parameters, out BusinessIdea idea, out FieldError error)
    {
        idea = new BusinessIdea();
        error = default;
        if (!TryMoney(Text(parameters, "preis"), out var price) || price < 0) { error = new("preis", "Den Preis verstehe ich nicht."); return false; }
        if (!TryMoney(Text(parameters, "kosten"), out var cost) || cost < 0) { error = new("kosten", "Die Kosten je Verkauf verstehe ich nicht."); return false; }
        if (!TryMoney(Text(parameters, "fix"), out var fix) || fix < 0) { error = new("fix", "Die festen Kosten verstehe ich nicht."); return false; }
        if (!TryMoney(Text(parameters, "menge"), out var qty) || qty < 0) { error = new("menge", "Die Menge verstehe ich nicht."); return false; }
        if (!TryMoney(Text(parameters, "stunden"), out var hours) || hours <= 0) { error = new("stunden", "Die Stunden pro Woche müssen größer als 0 sein."); return false; }
        if (!TryMoney(Text(parameters, "startgeld"), out var capital) || capital < 0) { error = new("startgeld", "Das Startgeld verstehe ich nicht."); return false; }
        if (!int.TryParse(Text(parameters, "mitbewerber"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rivals) || rivals < 0)
        {
            error = new("mitbewerber", "Mitbewerber ist eine ganze Zahl ab 0.");
            return false;
        }
        if (!int.TryParse(Text(parameters, "koennen"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var skill) || skill is < 1 or > 5)
        {
            error = new("koennen", "Koennen ist eine Zahl von 1 bis 5.");
            return false;
        }
        var model = ModelId(Text(parameters, "modell"));
        if (model is null) { error = new("modell", "Modell kenne ich nicht. Zum Beispiel Abo, Dienstleistung oder Shop."); return false; }
        idea.Name = Text(parameters, "name");
        idea.Problem = Text(parameters, "problem");
        idea.Audience = Text(parameters, "zielgruppe");
        idea.Solution = Text(parameters, "loesung");
        idea.ModelId = model;
        idea.Price = (double)price;
        idea.VariableCost = (double)cost;
        idea.FixedCostMonth = (double)fix;
        idea.CustomersMonth = (double)qty;
        idea.HoursWeek = (double)hours;
        idea.Capital = (double)capital;
        idea.Competitors = rivals;
        idea.Skill = skill;
        return true;
    }

    static string? ModelId(string raw)
    {
        var n = PromptFields.Norm(raw);
        var hits = BusinessStudio.Models.Where(m =>
            string.Equals(m.Id, n, StringComparison.OrdinalIgnoreCase)
            || string.Equals(PromptFields.Norm(m.Name), n, StringComparison.OrdinalIgnoreCase)
            || m.Name.Contains(raw.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        return hits.Count == 1 ? hits[0].Id : null;
    }

    static bool TryMoney(string raw, out decimal value)
    {
        value = 0;
        if (raw.Trim().Length == 0) return false;
        try { value = Finance.ParseAmount(raw); return true; }
        catch (FormatException) { return false; }
        catch (OverflowException) { return false; }
    }

    static bool Yes(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && value.Trim().Equals("ja", StringComparison.OrdinalIgnoreCase);

    static string Text(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value.Trim() : "";

    static string Euro(decimal value) => value.ToString("N2", De) + " €";

    readonly record struct Built(Invoice Invoice, string Heading);
    readonly record struct FieldError(string Field, string Message);
}
