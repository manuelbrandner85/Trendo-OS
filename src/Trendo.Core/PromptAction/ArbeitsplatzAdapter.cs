namespace Trendo.Core.PromptAction;

/// <summary>Schreibt ein Arbeitsblatt in den Projektordner. Kein Versand, keine Zahlung, keine Veröffentlichung, kein Gerät.</summary>
public sealed class ArbeitsplatzBlattAdapter : IStudioActionAdapter
{
    public const string ActionIdValue = "arbeitsplatz.blatt";
    public const string BoundaryHint = "Lokale Notiz im Projekt. Nichts wurde gesendet, bezahlt, veröffentlicht oder an ein Gerät geschickt.";
    public const string NewlineToken = "«NL»";

    public string StudioId => "X-11";
    public string ActionId => ActionIdValue;
    public string Label => "Arbeitsblatt schreiben";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "OneWorkspaceTests.Blatt_wird_Datei_und_besteht_QA";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["titel", "text"];
    public IReadOnlyList<string> OutputKinds => ["md"];

    public static string Encode(string? text) => (text ?? "").Replace("\r\n", "\n").Replace("\n", NewlineToken);
    public static string Decode(string? text) => (text ?? "").Replace(NewlineToken, "\n");

    public static string Prompt(string titel, string text) => "Titel: " + OneLine(titel) + "\nText: " + Encode(text);

    public static string Document(string titel, string text)
    {
        var body = Decode(text).Trim();
        return "# Arbeitsblatt\nStudio: Neues Projekt starten\nTitel: " + OneLine(titel) + "\nStand: lokal\n\nText:\n"
            + body + "\n\n---\nHinweis: " + BoundaryHint + "\n";
    }

    public static string BodyOf(string document)
    {
        const string start = "Text:\n";
        var i = document.IndexOf(start, StringComparison.Ordinal);
        if (i < 0) return document;
        var from = i + start.Length;
        var end = document.IndexOf("\n---\n", from, StringComparison.Ordinal);
        var slice = end < 0 ? document[from..] : document[from..end];
        return slice.TrimEnd('\n');
    }

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = PromptFields.Read(prompt);
        foreach (var kv in known) fields[kv.Key] = kv.Value;
        if (!fields.ContainsKey("titel") && !fields.ContainsKey("text") && !prompt.Contains("Arbeitsblatt", StringComparison.OrdinalIgnoreCase))
            return null;
        return new ActionDraft(ActionId, fields);
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (string.IsNullOrWhiteSpace(parameters.GetValueOrDefault("titel")))
            list.Add(new("titel", "titel", "Wie soll das Arbeitsblatt heißen?", true));
        if (string.IsNullOrWhiteSpace(Decode(parameters.GetValueOrDefault("text"))))
            list.Add(new("text", "text", "Was soll auf dem Arbeitsblatt stehen?", true));
        return list;
    }

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var titel = OneLine(parameters.GetValueOrDefault("titel") ?? "");
        var text = Decode(parameters.GetValueOrDefault("text"));
        if (text.Contains("???", StringComparison.Ordinal))
            return [new("unklar", "Der Text ist nicht eindeutig. Bitte klar schreiben.", true)];
        if (titel.Length > 80)
            return [new("titel", "Der Titel ist zu lang.", true)];
        if (text.Length > 8000)
            return [new("text", "Der Text ist zu lang.", true)];
        return [];
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var rel = "arbeitsplatz/blatt-v" + call.Version + ".md";
        var doc = Document(call.Parameters.GetValueOrDefault("titel") ?? "", call.Parameters.GetValueOrDefault("text") ?? "");
        call.Projects.WriteFile(call.ProjectId, rel, doc);
        var read = call.Projects.ReadFile(call.ProjectId, rel);
        var same = read == doc;
        var hint = read.Contains(BoundaryHint, StringComparison.Ordinal);
        IReadOnlyList<QaResult> qa =
        [
            new("blatt.gelesen", same, same ? "Die Datei wurde gelesen und stimmt mit dem Geschriebenen überein." : "Die Datei weicht ab.", null, null, "ProjectStore"),
            new("blatt.grenze", hint, hint ? "Der Hinweis steht in der Datei: nichts wurde gesendet, bezahlt oder veröffentlicht." : "Der Hinweis fehlt.", null, null, "ArbeitsplatzBlattAdapter")
        ];
        var preview = doc.Length > 500 ? doc[..500] : doc;
        return new AdapterOutcome(true, [new ProducedFile(rel, "md", true)], qa, preview, [], [], null);
    }

    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Diese Aktion hat keine Wirkung nach außen."], null);

    private static string OneLine(string? value)
    {
        var s = (value ?? "").Replace("\r\n", "\n").Replace('\n', ' ').Trim();
        return s.Length <= 80 ? s : s[..80];
    }
}
