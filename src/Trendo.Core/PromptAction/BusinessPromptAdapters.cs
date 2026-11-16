using System.Globalization;
using System.Text;
using Trendo.Core.Business;

namespace Trendo.Core.PromptAction;

/// <summary>STUDIO-SOCIAL-01 Social Media planen: Beitrag für mehrere Netzwerke vorbereiten – Text je Netzwerk mit Zeichenprüfung, Hashtags, Zeit aus der Warteschlange,
/// Bildformate. Veröffentlicht wird nichts; der Plan ist eine CSV-Datei.</summary>
public sealed class SocialPlanAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-SOCIAL-01";
    public string ActionId => "social.plan";
    public string Label => "Beitrag planen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "BusinessStudiosTests.Social_adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["text"];
    public IReadOnlyList<string> OutputKinds => ["csv"];

    static List<string> Nets(IReadOnlyDictionary<string, string> p)
    {
        var raw = FieldKit.Text(p, "netzwerke", "netzwerk");
        if (raw.Length == 0) return ["instagram", "facebook", "linkedin"];
        return [.. raw.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries).Select(n => n.Trim().ToLowerInvariant()).Select(n => n == "twitter" ? "x" : n).Distinct()];
    }

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "text") ? [] : [new("text", "text", "Was soll im Beitrag stehen? Schreib z. B. „Text: Ab Samstag gibt es wieder Zimtschnecken“.", true)];

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        var bad = Nets(parameters).Where(n => SocialPlan.Networks.All(x => x.Id != n)).ToList();
        if (bad.Count > 0) f.Add(new("netzwerke", "Unbekannt: " + string.Join(", ", bad) + ". Möglich: " + string.Join(", ", SocialPlan.Networks.Select(n => n.Id)) + ".", true));
        if (FieldKit.Has(parameters, "zeit") && !DateTime.TryParse(FieldKit.Text(parameters, "zeit"), CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out _))
            f.Add(new("zeit", "Zeit wie „16.11.2026 09:00“.", true));
        return f;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters; var nets = Nets(p);
        var when = DateTime.TryParse(FieldKit.Text(p, "zeit"), CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out var t) ? t : SocialPlan.NextSlot([], SocialPlan.DefaultSlots, DateTime.Now);
        var post = new SocialPost("p1", when, FieldKit.Text(p, "text"), SocialPlan.Tags(FieldKit.Text(p, "hashtags")), nets, FieldKit.Text(p, "bild"));
        var checks = SocialPlan.Check(post);
        var sb = new StringBuilder(SocialPlan.ToCsv([post]));
        var errors = checks.Where(c => c.Error).ToList();
        var qa = new QaResult(ActionId, errors.Count == 0, errors.Count == 0 ? $"{nets.Count} Netzwerke geprüft, Zeichen passen." : string.Join(" ", errors.Select(e => e.Network + ": " + e.Text)), null, string.Join(",", nets), "SocialPlan");
        var preview = string.Join("\n", nets.Select(n => $"{SocialPlan.Network(n).Name}: {SocialPlan.Length(SocialPlan.Compose(post, n), n)}/{SocialPlan.Network(n).MaxChars} Zeichen"));
        return FieldKit.Save(call, FieldKit.Slot("social", call.Version, "redaktionsplan.csv"), sb.ToString(), preview, qa, [.. checks.Where(c => !c.Error).Select(c => c.Network + ": " + c.Text)]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Trendo veröffentlicht nichts – kopiere den Text zur geplanten Zeit ins Netzwerk.");
}

/// <summary>STUDIO-NEWSLETTER-01: Newsletter-Entwurf als E-Mail-taugliches HTML mit Pflichtprüfung (Impressum, Abmeldelink). Kein Versand.</summary>
public sealed class NewsletterDraftAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-NEWSLETTER-01";
    public string ActionId => "newsletter.entwurf";
    public string Label => "Newsletter entwerfen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "BusinessStudiosTests.Newsletter_adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["betreff", "text"];
    public IReadOnlyList<string> OutputKinds => ["html"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var q = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "betreff")) q.Add(new("betreff", "betreff", "Wie lautet der Betreff? Z. B. „Betreff: Neuigkeiten im November“.", true));
        if (!FieldKit.Has(parameters, "text")) q.Add(new("text", "text", "Was soll drinstehen? Z. B. „Text: Wir haben neue Öffnungszeiten …“.", true));
        return q;
    }
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "link") && !FieldKit.Text(parameters, "link").StartsWith("https://", StringComparison.OrdinalIgnoreCase)) f.Add(new("link", "Link mit https:// angeben.", true));
        if (FieldKit.Has(parameters, "absender") && !NewsletterKit.ValidEmail(FieldKit.Text(parameters, "absender"))) f.Add(new("absender", "Absender als E-Mail-Adresse.", true));
        return f;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters;
        var blocks = new List<NlBlock> { new("b1", "kopf", Title: FieldKit.Text(p, "titel", "betreff")), new("b2", "text", Text: FieldKit.Text(p, "text")) };
        if (FieldKit.Has(p, "link")) blocks.Add(new("b3", "knopf", Label: FieldKit.Has(p, "knopf") ? FieldKit.Text(p, "knopf") : "Mehr erfahren", Url: FieldKit.Text(p, "link")));
        blocks.Add(new("b9", "fuss", Text: FieldKit.Text(p, "impressum")));
        var doc = new NlDoc(FieldKit.Text(p, "betreff"), FieldKit.Text(p, "vorschau"), FieldKit.Has(p, "absender") ? FieldKit.Text(p, "absender") : "newsletter@example.org", "#B45309", blocks);
        var html = NewsletterKit.Html(doc); var checks = NewsletterKit.Check(doc);
        var errors = checks.Where(c => c.Error).ToList();
        var qa = new QaResult(ActionId, html.Contains("width=\"600\"") && html.Contains("abbestellen"), errors.Count == 0 ? "Pflichtangaben vollständig." : $"{errors.Count} Pflichtpunkte offen – im Studio ergänzen.", null, doc.Subject, "NewsletterKit");
        return FieldKit.Save(call, FieldKit.Slot("newsletter", call.Version, "newsletter.html"), html, $"Betreff „{doc.Subject}“, {blocks.Count} Bausteine", qa, [.. checks.Select(c => (c.Error ? "Pflicht: " : "") + c.Text)]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Trendo verschickt keine Newsletter – den Entwurf öffnest du in deinem E-Mail-Programm oder Versanddienst.");
}

/// <summary>STUDIO-FLYER-01: Druckvorgabe für Flyer oder Visitenkarte – Endformat, Datenformat mit Beschnitt, Sicherheitsabstand, Inhalt und Prüfliste.</summary>
public sealed class FlyerSpecAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-FLYER-01";
    public string ActionId => "flyer.entwurf";
    public string Label => "Flyer anlegen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "BusinessStudiosTests.Flyer_adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["titel"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "titel") ? [] : [new("titel", "titel", "Was ist die Überschrift? Z. B. „Titel: Sommerfest am 20. Juni“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "format") && FlyerKit.Formats.All(f => f.Id != FieldKit.Text(parameters, "format").ToLowerInvariant())
            ? [new("format", "Format: " + string.Join(", ", FlyerKit.Formats.Select(f => f.Id)) + ".", true)] : [];

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters; var fmt = FlyerKit.Format(FieldKit.Has(p, "format") ? FieldKit.Text(p, "format").ToLowerInvariant() : "a5");
        var doc = new FlyerDoc(fmt.Id, "#FFFFFF", "#FFFFFF", []); var (dw, dh) = FlyerKit.DataSize(doc);
        var sb = new StringBuilder();
        sb.Append("Flyer: ").Append(FieldKit.Text(p, "titel")).Append('\n');
        sb.Append(CultureInfo.InvariantCulture, $"Endformat: {fmt.Name}\nDatenformat mit 3 mm Beschnitt: {dw:0.#} × {dh:0.#} mm\nSicherheitsabstand für Text: {fmt.SafeMm:0.#} mm vom Schnitt\n");
        if (fmt.Panels > 1) sb.Append("Falzlinien bei ").Append(string.Join(" und ", FlyerKit.Folds(fmt).Select(x => x.ToString("0.#", CultureInfo.InvariantCulture) + " mm"))).Append('\n');
        if (FieldKit.Has(p, "text")) sb.Append("\nText:\n").Append(FieldKit.Text(p, "text")).Append('\n');
        if (FieldKit.Has(p, "qr")) sb.Append("\nQR-Code: ").Append(FieldKit.Text(p, "qr")).Append(" (mindestens 15 × 15 mm)\n");
        sb.Append("\nVor dem Druck prüfen:\n- Bilder mit 300 dpi in Endgröße\n- Hintergründe 3 mm in den Beschnitt ziehen\n- Kein Text im Randbereich\n- Druck-PDF mit Schnittmarken aus dem Studio\n");
        var qr = !FieldKit.Has(p, "qr") || QrOk(FieldKit.Text(p, "qr"));
        var qa = new QaResult(ActionId, qr, qr ? "Formatangaben berechnet." : "Der QR-Inhalt ist zu lang für einen kleinen Code.", "mm", fmt.Id, "FlyerKit");
        return FieldKit.Save(call, FieldKit.Slot("flyer", call.Version, "druckvorgabe.txt"), sb.ToString(), $"{fmt.Name}, Datenformat {dw:0.#} × {dh:0.#} mm", qa);
    }
    static bool QrOk(string t) { try { QrMatrix.Encode(t); return true; } catch (InvalidOperationException) { return false; } }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Trendo bestellt keinen Druck – das Druck-PDF lädst du selbst bei deiner Druckerei hoch.");
}

/// <summary>STUDIO-LANDING-01: Verkaufsseite als statisches HTML mit Held, Nutzen, Preis, FAQ und Kaufen-Knopf (externer Link). Kein Bezahlen in Trendo.</summary>
public sealed class LandingPageAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-LANDING-01";
    public string ActionId => "verkaufsseite.entwurf";
    public string Label => "Verkaufsseite entwerfen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "BusinessStudiosTests.Landing_adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["produkt", "preis"];
    public IReadOnlyList<string> OutputKinds => ["html"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var q = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "produkt")) q.Add(new("produkt", "produkt", "Was verkaufst du? Z. B. „Produkt: Handgemachte Seife“.", true));
        if (!FieldKit.Has(parameters, "preis")) q.Add(new("preis", "preis", "Was kostet es? Z. B. „Preis: 8,90 €“.", true));
        return q;
    }
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "link") && !LandingKit.SafeUrl(FieldKit.Text(parameters, "link")) ? [new("link", "Kauf-Link mit https://.", true)] : [];

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters; var name = FieldKit.Text(p, "produkt");
        var nutzen = string.Join("\n", FieldKit.Text(p, "nutzen").Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var blocks = new List<LpBlock> { new("h", "held", name, FieldKit.Text(p, "beschreibung"), Label: "Jetzt kaufen") };
        if (nutzen.Length > 0) blocks.Add(new("n", "nutzen", "Darum lohnt es sich", Items: nutzen));
        blocks.Add(new("p", "preis", "Preis", Price: FieldKit.Text(p, "preis"), Label: "Jetzt kaufen"));
        var doc = new LpDoc(name, FieldKit.Text(p, "beschreibung"), "#B45309", FieldKit.Text(p, "link"), FieldKit.Text(p, "impressum"), FieldKit.Text(p, "datenschutz"), blocks);
        var html = LandingKit.Index(doc); var checks = LandingKit.Check(doc);
        var qa = new QaResult(ActionId, html.Contains("impressum.html") && !html.Contains("<script", StringComparison.OrdinalIgnoreCase), "Seite ohne Skripte, mit Impressum- und Datenschutz-Link.", null, name, "LandingKit");
        return FieldKit.Save(call, FieldKit.Slot("verkaufsseite", call.Version, "index.html"), html, $"{name} – {blocks.Count} Bausteine", qa, [.. checks.Select(c => (c.Error ? "Pflicht: " : "") + c.Text)]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("In Trendo wird nicht bezahlt – der Kaufen-Knopf führt zu deinem externen Bezahllink.");
}
