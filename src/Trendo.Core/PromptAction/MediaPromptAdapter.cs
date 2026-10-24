namespace Trendo.Core.PromptAction;

/// <summary>CORE-08: echte Umwandlung mit ffmpeg. Ohne ffmpeg keine Datei.</summary>
public sealed class MediaConvertAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-08";
    public string ActionId => "medien.umwandeln";
    public string Label => "Medium umwandeln";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Medien_wandelt_bild_mit_ffmpeg";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["datei", "aufgabe"];
    public IReadOnlyList<string> OutputKinds => ["jpg", "mp4", "mp3", "gif"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = MediaFields.Read(prompt, known);
        return MediaFields.WantsUpload(fields) ? null : new ActionDraft(ActionId, fields);
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => MediaFields.Missing(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => MediaFields.Validate(parameters);
    public AdapterOutcome Produce(AdapterCall call) => MediaFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Es wurde nichts hochgeladen."], null);
}

/// <summary>Hochladen bleibt aus.</summary>
public sealed class MediaUploadAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-08";
    public string ActionId => "medien.hochladen";
    public string Label => "Hochladen";
    public SideEffectClass Effect => SideEffectClass.Publish;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Blocked;
    public string Evidence => "PromptActionTests.Medien_laedt_nichts_hoch";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => [];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = MediaFields.Read(prompt, known);
        return MediaFields.WantsUpload(fields) ? new ActionDraft(ActionId, fields) : null;
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) =>
        new(false, [], [], null, ["Es wurde nichts hochgeladen."], ["Hochladen ist nicht angeschlossen."], null);
    public AdapterOutcome SideEffect(AdapterCall call) =>
        new(false, [], [], null, [], ["Es wurde nichts hochgeladen."], null);
}

static class MediaFields
{
    public static Dictionary<string, string> Read(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = PromptFields.Read(prompt);
        foreach (var kv in known) fields[kv.Key] = kv.Value;
        return fields;
    }

    public static bool WantsUpload(IReadOnlyDictionary<string, string> fields) => Yes(fields, "hochladen");

    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (Text(parameters, "datei").Length == 0)
            list.Add(new("datei", "datei", "Datei fehlt. Schreib den Projektpfad als „Datei: …“.", true));
        if (Text(parameters, "aufgabe").Length == 0)
            list.Add(new("aufgabe", "aufgabe", "Aufgabe fehlt. Zum Beispiel „Aufgabe: Bild“.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ValidationFinding>();
        if (Text(parameters, "aufgabe").Length > 0 && JobId(Text(parameters, "aufgabe")) is null)
            list.Add(new("aufgabe", "Die Aufgabe kenne ich nicht. Zum Beispiel Bild, MP3 oder MP4.", true));
        var file = Text(parameters, "datei");
        if (file.Length > 0 && (Path.IsPathRooted(file) || file.Contains("..", StringComparison.Ordinal)))
            list.Add(new("datei", "Die Datei muss im Projekt liegen.", true));
        return list;
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        var ffmpeg = Cli.Find("ffmpeg");
        if (ffmpeg is null)
            return new(false, [], [], null, ["ffmpeg fehlt. Es wurde nichts umgewandelt."], ["ffmpeg ist nicht installiert."], null);
        var jobId = JobId(Text(call.Parameters, "aufgabe"));
        var sourceRel = Text(call.Parameters, "datei").Replace('\\', '/');
        if (jobId is null || sourceRel.Length == 0)
            return new(false, [], [], null, [], ["Aufgabe oder Datei fehlt."], null);
        string source;
        try { source = call.Projects.ResolveInProject(call.ProjectId, sourceRel); }
        catch (ProjectIsolationException ex) { return new(false, [], [], null, [], [ex.Message], null); }
        if (new FileInfo(source).LinkTarget is not null)
            return new(false, [], [], null, [], ["Die Datei ist ein Verweis nach außen und wurde nicht gelesen."], null);
        if (!File.Exists(source))
            return new(true, [], [], null, [], ["Die Datei liegt nicht im Projekt: " + sourceRel], null);
        var job = MediaStudio.Jobs.First(j => j.Id == jobId);
        var copyRel = "medien/v" + call.Version + "/" + Path.GetFileName(source);
        var copy = call.Projects.ResolveInProject(call.ProjectId, copyRel);
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(source, copy, true);
        var (result, output) = MediaStudio.Run(ffmpeg, copy, jobId);
        if (!result.Ok || output.Length == 0 || !File.Exists(output))
        {
            var detail = result.Err.Length > 0 ? result.Err.Trim() : "ffmpeg hat keine Datei geschrieben.";
            return new(true, [], [new QaResult("medien.ffmpeg", false, detail, null, sourceRel, "ffmpeg")], null, [], [detail], null);
        }
        var root = call.Projects.ProjectFilesRoot(call.ProjectId);
        var rel = Path.GetRelativePath(root, output).Replace('\\', '/');
        if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel))
            return new(false, [], [], null, [], ["Die Ausgabedatei liegt außerhalb des Projekts."], null);
        var bytes = File.ReadAllBytes(output);
        var headerOk = job.Ext == ".jpg" ? bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8 : bytes.Length > 0;
        var preview = job.Label + "\n" + Path.GetFileName(output) + "\n" + bytes.Length + " Bytes. Nichts hochgeladen.";
        return new(true,
            [new ProducedFile(copyRel, Path.GetExtension(copy).TrimStart('.'), true), new ProducedFile(rel, job.Ext.TrimStart('.'), true)],
            [new QaResult("medien.datei", headerOk && bytes.Length > 0, headerOk ? "ffmpeg hat eine lesbare Datei geschrieben." : "Die Ausgabedatei hat nicht das erwartete Format.", null, Path.GetFileName(output), "ffmpeg " + job.Id)],
            preview, ["Nichts hochgeladen."], [], null);
    }

    static string? JobId(string raw)
    {
        var n = PromptFields.Norm(raw);
        return n switch
        {
            "image" or "bild" or "bild-verkleinern" => "image",
            "thumb" or "vorschaubild" => "thumb",
            "mp3" or "ton" => "mp3",
            "mp4" => "mp4",
            "small" or "kleiner" => "small",
            "gif" => "gif",
            "trim" or "ausschnitt" => "trim",
            _ => null
        };
    }

    static bool Yes(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && value.Trim().Equals("ja", StringComparison.OrdinalIgnoreCase);

    static string Text(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value.Trim() : "";
}
