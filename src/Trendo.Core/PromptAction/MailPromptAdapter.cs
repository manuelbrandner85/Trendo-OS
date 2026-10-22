namespace Trendo.Core.PromptAction;

/// <summary>SYS-09: Entwurf als .eml. Trendo verschickt selbst nichts.</summary>
public sealed class MailDraftAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-09";
    public string ActionId => "mail.entwurf";
    public string Label => "E-Mail-Entwurf";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Ready;
    public string Evidence => "PromptActionTests.Mail_entwurf_wird_gelesen";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["an", "betreff", "text"];
    public IReadOnlyList<string> OutputKinds => ["eml"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = MailFields.Read(prompt, known);
        if (MailFields.WantsSend(fields)) return null;
        return new ActionDraft(ActionId, fields);
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => MailFields.Missing(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => MailFields.Validate(parameters);

    public AdapterOutcome Produce(AdapterCall call) => MailFields.Write(call, "entwurf.eml");

    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Es wurde nichts gesendet."], null);
}

/// <summary>Versand bleibt blockiert, bis ein echtes Postfach angeschlossen ist. Auch „Senden: ja“ schickt nichts.</summary>
public sealed class MailSendAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-09";
    public string ActionId => "mail.senden";
    public string Label => "E-Mail senden";
    public SideEffectClass Effect => SideEffectClass.Message;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Blocked;
    public string Evidence => "PromptActionTests.Mail_senden_schickt_nichts";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["an", "betreff", "text"];
    public IReadOnlyList<string> OutputKinds => ["eml"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = MailFields.Read(prompt, known);
        return MailFields.WantsSend(fields) ? new ActionDraft(ActionId, fields) : null;
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => MailFields.Missing(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => MailFields.Validate(parameters);

    public AdapterOutcome Produce(AdapterCall call)
    {
        var written = MailFields.Write(call, "nicht-gesendet.eml");
        var warnings = written.Warnings.ToList();
        warnings.Add("Es wurde nichts gesendet. Trendo hat kein Postfach und schickt selbst keine E-Mail.");
        return written with { Warnings = warnings };
    }

    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Es wurde nichts gesendet."], null);
}

static class MailFields
{
    public static Dictionary<string, string> Read(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = PromptFields.Read(prompt);
        foreach (var kv in known) fields[kv.Key] = kv.Value;
        return fields;
    }

    public static bool WantsSend(IReadOnlyDictionary<string, string> fields) =>
        fields.TryGetValue("senden", out var value) && value.Trim().Equals("ja", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        void Need(string field, string label)
        {
            if (!parameters.TryGetValue(field, out var value) || value.Trim().Length == 0)
                list.Add(new(field, field, label + " fehlt.", true));
        }
        Need("an", "Empfänger");
        Need("betreff", "Betreff");
        Need("text", "Text");
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var draft = Draft(parameters);
        return MailCenter.Check(draft)
            .Where(m => m.Contains("gültige", StringComparison.Ordinal) || m.Contains("fehlt", StringComparison.Ordinal))
            .Select(m => new ValidationFinding("an", m, true))
            .ToList();
    }

    public static AdapterOutcome Write(AdapterCall call, string fileName)
    {
        var draft = Draft(call.Parameters);
        var relative = "post/v" + call.Version + "/" + fileName;
        var full = call.Projects.ResolveInProject(call.ProjectId, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var raw = MailCenter.ToEml(draft, DateTimeOffset.Now);
        File.WriteAllText(full, raw);
        var parsed = MailCenter.ParseEml(raw);
        var ok = parsed.To.Contains(draft.To, StringComparison.Ordinal) && parsed.Subject == draft.Subject && parsed.Body.Contains(draft.Body, StringComparison.Ordinal);
        var warnings = MailCenter.Check(draft).Where(m => !m.Contains("gültige", StringComparison.Ordinal) && !m.Contains("fehlt", StringComparison.Ordinal)).ToList();
        return new(true, [new ProducedFile(relative, "eml", true)],
            [new QaResult("mail.entwurf", ok, ok ? "Empfänger, Betreff und Text stehen in der EML-Datei." : "Die EML-Datei stimmt nicht mit dem Auftrag überein.", null, draft.Subject, "MailCenter.ParseEml")],
            parsed.Body, warnings, [], null);
    }

    static MailDraft Draft(IReadOnlyDictionary<string, string> parameters) => new(
        parameters.GetValueOrDefault("an")?.Trim() ?? "",
        parameters.GetValueOrDefault("betreff")?.Trim() ?? "",
        parameters.GetValueOrDefault("text")?.Trim() ?? "",
        parameters.GetValueOrDefault("kopie")?.Trim() ?? "");
}
