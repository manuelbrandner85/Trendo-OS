using System.Globalization;

namespace Trendo.Core.PromptAction;

/// <summary>STUDIO-LETTER-01 über Letters.WriteDocx. PDF nur, wenn LibreOffice wirklich da ist.</summary>
public sealed class LetterPromptAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-LETTER-01";
    public string ActionId => "brief.schreiben";
    public string Label => "Brief schreiben";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Brief_wird_geschrieben_und_wieder_gelesen";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["absender", "strasse", "ort", "empfaenger", "betreff", "text"];
    public IReadOnlyList<string> OutputKinds => ["docx"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = PromptFields.Read(prompt);
        foreach (var kv in known) fields[kv.Key] = kv.Value;
        return new ActionDraft(ActionId, fields);
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var draft = Draft(parameters);
        var list = new List<ClarificationQuestion>();
        void Need(string field, string value, string label)
        {
            if (value.Length == 0)
                list.Add(new(field, field, label + " fehlt. Schreib es als „" + label + ": …“.", true));
        }
        Need("absender", draft.SenderName, "Absender");
        Need("strasse", draft.SenderStreet, "Strasse");
        Need("ort", draft.SenderCity, "Ort");
        Need("empfaenger", draft.Recipient, "Empfaenger");
        Need("betreff", draft.Subject, "Betreff");
        Need("text", draft.Body, "Text");
        return list;
    }

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ValidationFinding>();
        var raw = Field(parameters, "datum");
        if (raw.Length > 0 && !TryDate(raw, out _))
            list.Add(new("datum", "Das Datum verstehe ich nicht. Zum Beispiel 2026-10-22.", true));
        foreach (var issue in Letters.Validate(Draft(parameters)))
            if (issue.Blocking)
                list.Add(new(PromptFields.Norm(issue.Field), issue.Message, true));
        return list;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var draft = Draft(call.Parameters);
        var stem = Letters.FileStem(draft);
        var relative = "briefe/v" + call.Version + "/" + stem + ".docx";
        var full = call.Projects.ResolveInProject(call.ProjectId, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        Letters.WriteDocx(draft, full);
        var jsonRel = "briefe/v" + call.Version + "/" + stem + ".trendo-brief.json";
        var json = call.Projects.ResolveInProject(call.ProjectId, jsonRel);
        File.WriteAllText(json, System.Text.Json.JsonSerializer.Serialize(draft));
        var text = Letters.ReadDocxText(full);
        var bodyLine = draft.Body.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var textOk = text.Contains(draft.Subject, StringComparison.Ordinal) && bodyLine.Length > 0 && text.Contains(bodyLine, StringComparison.Ordinal);
        var loaded = LetterArchive.Load(full);
        var reopenOk = loaded is not null && loaded.Subject == draft.Subject && loaded.Body == draft.Body;
        var warnings = Letters.Validate(draft).Where(i => !i.Blocking).Select(i => i.Message).ToList();
        if (!LibreOffice())
            warnings.Add("PDF ist hier nicht möglich. LibreOffice (soffice) fehlt. Die Word-Datei liegt im Projekt.");
        return new AdapterOutcome(true,
            [new ProducedFile(relative, "docx", true), new ProducedFile(jsonRel, "json", true)],
            [
                new QaResult("brief.text", textOk, textOk ? "Betreff und Text stehen in der Word-Datei." : "Der Text in der Word-Datei stimmt nicht mit dem Auftrag überein.", null, draft.Subject, "Letters.ReadDocxText"),
                new QaResult("brief.wieder", reopenOk, reopenOk ? "Der Brief lässt sich wieder öffnen." : "Die Begleitdatei passt nicht zum Brief.", null, relative, "LetterArchive.Load")
            ],
            text.Trim(), warnings, [], null);
    }

    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Ein Brief wird nicht verschickt."], null);

    static LetterDraft Draft(IReadOnlyDictionary<string, string> parameters)
    {
        var draft = new LetterDraft
        {
            SenderName = Field(parameters, "absender", "name"),
            SenderStreet = Field(parameters, "strasse"),
            SenderCity = Field(parameters, "ort"),
            Recipient = Field(parameters, "empfaenger", "an"),
            Subject = Field(parameters, "betreff"),
            Body = Field(parameters, "text", "inhalt"),
            Place = Field(parameters, "briefort"),
            Reference = Field(parameters, "zeichen", "aktenzeichen"),
            Attachments = Field(parameters, "anlagen"),
        };
        var salute = Field(parameters, "anrede");
        if (salute.Length > 0) draft.Salutation = salute;
        var closing = Field(parameters, "gruss");
        if (closing.Length > 0) draft.Closing = closing;
        var art = Field(parameters, "art", "kategorie");
        if (art.Length > 0)
        {
            var subject = draft.Subject;
            var body = draft.Body;
            Letters.ApplyTemplate(draft, art);
            if (subject.Length > 0) draft.Subject = subject;
            if (body.Length > 0) draft.Body = body;
        }
        if (TryDate(Field(parameters, "datum"), out var date)) draft.Date = date;
        return draft;
    }

    static bool TryDate(string raw, out DateTime date)
    {
        date = default;
        if (raw.Length == 0) return false;
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || DateTime.TryParse(raw, CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out date);
    }

    static string Field(IReadOnlyDictionary<string, string> parameters, params string[] keys)
    {
        foreach (var key in keys)
            if (parameters.TryGetValue(key, out var value) && value.Trim().Length > 0)
                return value.Trim();
        return "";
    }

    static bool LibreOffice()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            if (File.Exists(Path.Combine(dir, "soffice")) || File.Exists(Path.Combine(dir, "soffice.exe")))
                return true;
        return false;
    }
}
