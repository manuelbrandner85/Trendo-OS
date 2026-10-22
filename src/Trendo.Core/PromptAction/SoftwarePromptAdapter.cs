namespace Trendo.Core.PromptAction;

/// <summary>CORE-07: Projektvorlage lokal anlegen. Nichts wird veröffentlicht oder gepusht.</summary>
public sealed class SoftwareScaffoldAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-07";
    public string ActionId => "software.anlegen";
    public string Label => "Projekt anlegen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Software_legt_dateien_an";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["vorlage", "name"];
    public IReadOnlyList<string> OutputKinds => ["html", "py", "cs", "js"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = CodeFields.Read(prompt, known);
        return CodeFields.WantsPublish(fields) ? null : new ActionDraft(ActionId, fields);
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => CodeFields.Missing(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => CodeFields.Validate(parameters);
    public AdapterOutcome Produce(AdapterCall call) => CodeFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Es wurde nichts veröffentlicht."], null);
}

/// <summary>Push, Merge und Veröffentlichung bleiben aus.</summary>
public sealed class SoftwarePublishAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-07";
    public string ActionId => "software.veroeffentlichen";
    public string Label => "Veröffentlichen";
    public SideEffectClass Effect => SideEffectClass.Publish;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Blocked;
    public string Evidence => "PromptActionTests.Software_veroeffentlicht_nichts";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => [];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = CodeFields.Read(prompt, known);
        return CodeFields.WantsPublish(fields) ? new ActionDraft(ActionId, fields) : null;
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) =>
        new(false, [], [], null, ["Es wurde nichts veröffentlicht, gepusht oder zusammengeführt."], ["Veröffentlichen ist nicht angeschlossen."], null);
    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Es wurde nichts veröffentlicht."], null);
}

static class CodeFields
{
    public static Dictionary<string, string> Read(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = PromptFields.Read(prompt);
        foreach (var kv in known) fields[kv.Key] = kv.Value;
        return fields;
    }

    public static bool WantsPublish(IReadOnlyDictionary<string, string> fields) =>
        Yes(fields, "veroeffentlichen") || Yes(fields, "push") || Yes(fields, "deploy");

    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (Text(parameters, "vorlage").Length == 0)
            list.Add(new("vorlage", "vorlage", "Vorlage fehlt. Zum Beispiel „Vorlage: Webseite“.", true));
        if (Text(parameters, "name").Length == 0)
            list.Add(new("name", "name", "Name fehlt. Schreib es als „Name: …“.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ValidationFinding>();
        if (TemplateId(Text(parameters, "vorlage")) is null)
            list.Add(new("vorlage", "Die Vorlage kenne ich nicht. Möglich: Webseite, Python, C# oder Node.", true));
        var name = Text(parameters, "name");
        if (name.Length > 0 && (name.IndexOfAny(['/', '\\']) >= 0 || name.Contains("..", StringComparison.Ordinal)))
            list.Add(new("name", "Der Name darf kein Ordnerpfad sein.", true));
        return list;
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        var template = TemplateId(Text(call.Parameters, "vorlage"));
        var name = Text(call.Parameters, "name");
        if (template is null || name.Length == 0)
            return new(false, [], [], null, [], ["Vorlage oder Name fehlt."], null);
        var parentRel = "software/v" + call.Version;
        var parent = call.Projects.ResolveInProject(call.ProjectId, parentRel);
        Directory.CreateDirectory(parent);
        string dir;
        try { dir = DevStudio.Create(parent, template, name); }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return new(false, [], [], null, [], [ex.Message], null);
        }

        var info = DevStudio.Templates.First(t => t.Id == template);
        var warnings = new List<string> { "Nichts veröffentlicht. Kein Push und kein Deployment." };
        var qa = new List<QaResult>();
        var marker = Marker(template, name);
        var markerFull = Path.Combine(dir, marker.File);
        var markerText = File.ReadAllText(markerFull);
        var filesOk = markerText.Contains(marker.Needle, StringComparison.Ordinal);
        qa.Add(new("software.datei", filesOk, filesOk ? "Die angelegte Datei enthält den Projektnamen." : "Die angelegte Datei passt nicht zur Vorlage.", null, marker.File, "DevStudio.Create"));

        if (Yes(call.Parameters, "testen"))
        {
            var command = DevStudio.TestCommand(dir);
            if (command is null)
                warnings.Add("Für diese Vorlage gibt es keinen automatischen Test. Die Dateien wurden nur gelesen.");
            else
            {
                var result = DevStudio.RunTests(dir, 60000);
                var detail = result.Ok ? "Der Test der Vorlage ist durchgelaufen." : "Der Test der Vorlage ist nicht durchgelaufen. " + Trim(result.Err + result.Out);
                qa.Add(new("software.test", result.Ok, detail, null, command.Value.Exe, "DevStudio.RunTests"));
            }
        }

        if (Yes(call.Parameters, "git"))
        {
            var git = DevStudio.GitInit(dir);
            var state = git.Ok ? DevStudio.Git(dir) : new GitState(false, "", 0, [], "");
            var remote = git.Ok ? Cli.Run("git", ["remote"], dir, 10000).Out.Trim() : "";
            var gitOk = git.Ok && state.IsRepo && remote.Length == 0;
            qa.Add(new("software.git", gitOk, gitOk ? "Lokales Git ohne Remote. Nichts wurde gepusht." : "Git ist lokal nicht angelegt. " + Trim(git.Err), null, state.Branch, "DevStudio.GitInit"));
            if (!gitOk) warnings.Add("Git hat nicht geklappt. Die Dateien liegen trotzdem im Projekt.");
        }

        var produced = new List<ProducedFile>();
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            if (rel.StartsWith(".git/", StringComparison.Ordinal) || rel == ".git") continue;
            produced.Add(new(parentRel + "/" + Path.GetFileName(dir) + "/" + rel, Path.GetExtension(file).TrimStart('.'), true));
        }
        var preview = info.Name + "\n" + name + "\n" + string.Join(", ", produced.Select(f => Path.GetFileName(f.RelativePath))) + "\nNichts veröffentlicht.";
        return new(produced.Count > 0, produced, qa, preview, warnings, [], null);
    }

    static (string File, string Needle) Marker(string template, string name) => template switch
    {
        "web" => ("index.html", "<title>" + System.Net.WebUtility.HtmlEncode(name) + "</title>"),
        "python" => ("main.py", "def gruss"),
        "dotnet-console" => ("Program.cs", "Hallo aus Trendo OS!"),
        _ => ("index.js", "export const gruss")
    };

    static string? TemplateId(string raw)
    {
        var n = PromptFields.Norm(raw);
        return n switch
        {
            "web" or "webseite" or "html" => "web",
            "python" => "python",
            "dotnet" or "dotnet-console" or "csharp" or "cs" => "dotnet-console",
            "node" or "javascript" => "node",
            _ => null
        };
    }

    static bool Yes(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && value.Trim().Equals("ja", StringComparison.OrdinalIgnoreCase);

    static string Text(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value.Trim() : "";

    static string Trim(string value) => value.Length <= 180 ? value.Trim() : value.Trim()[..180];
}
