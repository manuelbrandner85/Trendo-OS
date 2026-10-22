using System.Text.Json;

namespace Trendo.Core.PromptAction;

/// <summary>CORE-06: spielbare Browser-Vorlage. Der Editor wird nicht gestartet.</summary>
public sealed class GameBrowserAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-06";
    public string ActionId => "spiel.browser";
    public string Label => "Browser-Spiel anlegen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Spiel_schreibt_browser_datei";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["vorlage", "name"];
    public IReadOnlyList<string> OutputKinds => ["html", "js", "md"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = GameFields.Read(prompt, known);
        if (GameFields.WantsLaunch(fields) || GameFields.IsUnreal(fields)) return null;
        return new ActionDraft(ActionId, fields);
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => GameFields.MissingBrowser(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => GameFields.ValidateBrowser(parameters);
    public AdapterOutcome Produce(AdapterCall call) => GameFields.WriteBrowser(call);
    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Unreal wurde nicht gestartet."], null);
}

/// <summary>Leere Unreal-Projektdatei. Der Editor startet nicht.</summary>
public sealed class GameUnrealAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-06";
    public string ActionId => "spiel.unreal";
    public string Label => "Unreal-Projekt anlegen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Spiel_schreibt_uprojekt";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["name", "version"];
    public IReadOnlyList<string> OutputKinds => ["uproject"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = GameFields.Read(prompt, known);
        if (GameFields.WantsLaunch(fields) || !GameFields.IsUnreal(fields)) return null;
        return new ActionDraft(ActionId, fields);
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => GameFields.MissingUnreal(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => GameFields.ValidateUnreal(parameters);
    public AdapterOutcome Produce(AdapterCall call) => GameFields.WriteUnreal(call);
    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Der Unreal-Editor wurde nicht gestartet."], null);
}

/// <summary>Starten bleibt aus. Auch „Starten: ja“ öffnet weder Browser noch Editor.</summary>
public sealed class GameLaunchAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-06";
    public string ActionId => "spiel.starten";
    public string Label => "Spiel starten";
    public SideEffectClass Effect => SideEffectClass.Machine;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Blocked;
    public string Evidence => "PromptActionTests.Spiel_startet_nichts";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => [];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = GameFields.Read(prompt, known);
        return GameFields.WantsLaunch(fields) ? new ActionDraft(ActionId, fields) : null;
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) =>
        new(false, [], [], null, ["Es wurde nichts gestartet."], ["Starten ist nicht angeschlossen."], null);
    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Es wurde nichts gestartet."], null);
}

static class GameFields
{
    public static Dictionary<string, string> Read(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = PromptFields.Read(prompt);
        foreach (var kv in known) fields[kv.Key] = kv.Value;
        return fields;
    }

    public static bool WantsLaunch(IReadOnlyDictionary<string, string> fields) => Yes(fields, "starten");

    public static bool IsUnreal(IReadOnlyDictionary<string, string> fields) =>
        Template(Text(fields, "vorlage")) == "unreal";

    public static IReadOnlyList<ClarificationQuestion> MissingBrowser(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (Text(parameters, "vorlage").Length == 0)
            list.Add(new("vorlage", "vorlage", "Vorlage fehlt. Zum Beispiel „Vorlage: Schlange“.", true));
        if (Text(parameters, "name").Length == 0)
            list.Add(new("name", "name", "Name fehlt. Schreib es als „Name: …“.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> ValidateBrowser(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ValidationFinding>();
        var id = Template(Text(parameters, "vorlage"));
        if (id is null || id == "unreal")
            list.Add(new("vorlage", "Die Vorlage kenne ich nicht. Möglich: Schlange, Mauerbrecher oder Hüpfspiel.", true));
        if (BadName(Text(parameters, "name")))
            list.Add(new("name", "Der Name darf kein Ordnerpfad sein.", true));
        return list;
    }

    public static IReadOnlyList<ClarificationQuestion> MissingUnreal(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (Text(parameters, "name").Length == 0)
            list.Add(new("name", "name", "Name fehlt. Schreib es als „Name: …“.", true));
        if (Text(parameters, "version").Length == 0)
            list.Add(new("version", "version", "Version fehlt. Zum Beispiel „Version: 5.4“.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> ValidateUnreal(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ValidationFinding>();
        if (BadName(Text(parameters, "name")))
            list.Add(new("name", "Der Name darf kein Ordnerpfad sein.", true));
        var version = Text(parameters, "version");
        if (version.Length > 0 && version.Any(c => !(char.IsLetterOrDigit(c) || c is '.' or '-')))
            list.Add(new("version", "Die Version darf nur Ziffern, Punkte und Bindestriche haben.", true));
        return list;
    }

    public static AdapterOutcome WriteBrowser(AdapterCall call)
    {
        var id = Template(Text(call.Parameters, "vorlage"));
        var name = Text(call.Parameters, "name");
        if (id is null || id == "unreal" || name.Length == 0)
            return new(false, [], [], null, [], ["Vorlage oder Name fehlt."], null);
        var parentRel = "spiele/v" + call.Version;
        var parent = call.Projects.ResolveInProject(call.ProjectId, parentRel);
        Directory.CreateDirectory(parent);
        string dir;
        try { dir = GameStudio.CreateBrowserGame(parent, id, name); }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return new(false, [], [], null, [], [ex.Message], null);
        }
        var htmlPath = Path.Combine(dir, "index.html");
        var jsPath = Path.Combine(dir, "game.js");
        var html = File.ReadAllText(htmlPath);
        var js = File.ReadAllText(jsPath);
        var title = System.Net.WebUtility.HtmlEncode(name);
        var ok = html.Contains("<title>" + title + "</title>", StringComparison.Ordinal) && js.Contains("Punkte", StringComparison.Ordinal);
        var label = GameStudio.Templates.First(t => t.Id == id).Label;
        var produced = Files(parentRel, dir);
        var preview = label + "\n" + name + "\nIm Browser spielbar. Unreal wurde nicht gestartet.";
        return new(true, produced,
            [new QaResult("spiel.datei", ok, ok ? "Titel und Spiel stehen in den Dateien." : "Die Spieldateien passen nicht zur Vorlage.", null, "index.html", "GameStudio.CreateBrowserGame")],
            preview, ["Unreal wurde nicht gestartet. Es ist kein Editor-Lauf."], [], null);
    }

    public static AdapterOutcome WriteUnreal(AdapterCall call)
    {
        var name = Text(call.Parameters, "name");
        var version = Text(call.Parameters, "version");
        if (name.Length == 0 || version.Length == 0)
            return new(false, [], [], null, [], ["Name oder Version fehlt."], null);
        var parentRel = "spiele/v" + call.Version;
        var parent = call.Projects.ResolveInProject(call.ProjectId, parentRel);
        Directory.CreateDirectory(parent);
        string dir;
        try { dir = GameStudio.CreateUnrealProject(parent, name, version); }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return new(false, [], [], null, [], [ex.Message], null);
        }
        var project = Directory.GetFiles(dir, "*.uproject").Single();
        using var doc = JsonDocument.Parse(File.ReadAllText(project));
        var engine = doc.RootElement.GetProperty("EngineAssociation").GetString();
        var ok = engine == version;
        var rel = parentRel + "/" + Path.GetFileName(dir) + "/" + Path.GetFileName(project);
        var engines = GameStudio.FindEngines();
        var preview = "Unreal-Projekt " + Path.GetFileName(dir) + " für Version " + version + ".\nDer Editor wurde nicht gestartet.";
        var warning = engines.Count == 0
            ? "Keine installierte Unreal-Engine gefunden. Die Projektdatei liegt da, der Editor wurde nicht gestartet."
            : "Der Editor wurde nicht gestartet.";
        return new(true, [new ProducedFile(rel, "uproject", true)],
            [new QaResult("spiel.uproject", ok, ok ? "Die Engine-Version steht in der Projektdatei." : "Die Projektdatei hat eine andere Engine-Version.", null, engine, "GameStudio.CreateUnrealProject")],
            preview, [warning], [], null);
    }

    static List<ProducedFile> Files(string parentRel, string dir)
    {
        var list = new List<ProducedFile>();
        foreach (var file in Directory.EnumerateFiles(dir))
        {
            var rel = parentRel + "/" + Path.GetFileName(dir) + "/" + Path.GetFileName(file);
            list.Add(new(rel, Path.GetExtension(file).TrimStart('.'), true));
        }
        return list;
    }

    static string? Template(string raw)
    {
        var n = PromptFields.Norm(raw);
        return n switch
        {
            "schlange" or "snake" => "snake",
            "mauerbrecher" or "breakout" => "breakout",
            "huepfspiel" or "huepfen" or "jump" => "jump",
            "unreal" => "unreal",
            _ => null
        };
    }

    static bool BadName(string name) =>
        name.Length > 0 && (name.IndexOfAny(['/', '\\']) >= 0 || name.Contains("..", StringComparison.Ordinal));

    static bool Yes(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && value.Trim().Equals("ja", StringComparison.OrdinalIgnoreCase);

    static string Text(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value.Trim() : "";
}
