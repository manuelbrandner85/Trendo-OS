using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>
/// CORE-07, Seite „dev“: aus einer Beschreibung wird ein bearbeitbarer Ordner.
/// Anschluss für den gemeinsamen Prompt-Kern (Verbesserung 42/43), der auf diesem Stand noch nicht im Repo liegt.
/// </summary>
public static class WebsiteStudio
{
    public const string StudioId = "CORE-07";
    public const string PageId = "dev";
    public const string ActionBuild = "website.build";
    public const string ActionRevise = "website.revise";
    public const string ActionCheck = "website.check";
    public const string ActionPack = "website.pack";

    public static async Task<WebsiteBuildResult> BuildAsync(string parent, WebsiteBrief brief, WebsiteAi? ai = null, WebsiteBuildContext? ctx = null, CancellationToken ct = default)
    {
        ctx ??= new WebsiteBuildContext();
        var answer = ctx.AiAnswer;
        if (ai is not null && string.IsNullOrWhiteSpace(answer))
        {
            try { answer = await ai(WebsitePlan.PromptForAi(brief), ct); }
            catch (OperationCanceledException) { answer = null; }
            catch (Exception) { answer = null; }
        }
        return Build(parent, brief, ctx with { AiAnswer = answer });
    }

    public static WebsiteBuildResult Build(string parent, WebsiteBrief brief, WebsiteBuildContext? ctx = null)
    {
        ctx ??= new WebsiteBuildContext();
        var content = WebsitePlan.Compose(brief, ctx.AiAnswer);
        var folder = DevStudio.SafeName(content.Name);
        var dir = Path.Combine(parent, folder);
        if (Directory.Exists(dir)) throw new IOException($"Den Ordner „{folder}“ gibt es schon.");
        Directory.CreateDirectory(dir);
        try
        {
            var model = WebsiteModels.Place(dir, brief.ModelPath, ctx.Blender, brief.ExampleCube);
            content.ModelFile = model.Relative;
            content.ModelNote = model.Note;
            content.ExampleModel = model.Example;
            var media = WebsiteMedia.Place(dir, brief.MediaPath, content.Style.Accent);
            content.VideoFile = media.Video;
            content.PosterFile = media.Poster;
            content.Frames = media.Frames.ToList();
            content.MediaNote = media.Note;
            content.UnrealNote = WebsiteUnreal.Describe(ctx.Engines ?? GameStudio.FindEngines());
            WebsiteHtml.Write(dir, content);
            var quality = WebsiteQuality.Check(dir);
            WebsiteQuality.Save(dir, quality);
            var msg = "„" + content.Name + "“ liegt im Ordner. " + content.TechniqueReason
                + (content.CopyFromAi ? " Texte mit lokaler KI ergänzt." : " Texte aus der Beschreibung, ohne KI.");
            if (!string.IsNullOrWhiteSpace(content.ModelNote)) msg += " " + content.ModelNote;
            if (!string.IsNullOrWhiteSpace(brief.MediaPath)) msg += " " + content.MediaNote;
            return new(dir, content, quality, msg.Trim());
        }
        catch
        {
            try { Directory.Delete(dir, true); } catch { /* Aufräumen darf den ursprünglichen Fehler nicht verdecken */ }
            throw;
        }
    }

    public static WebsiteContent Load(string dir)
    {
        var content = WebsiteJson.Read<WebsiteContent>(Path.Combine(dir, "inhalt.json"));
        content.Sections ??= [];
        content.Frames ??= [];
        foreach (var section in content.Sections) section.Items ??= [];
        content.Style ??= WebsiteStyle.Klar;
        return content;
    }

    /// <summary>T-0206: Zeigt, was <see cref="Revise"/> ändern würde – auf einer Kopie, das Projekt bleibt unverändert.</summary>
    public static (IReadOnlyList<string> Removed, IReadOnlyList<string> Added) PreviewRevise(string dir, string instruction)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-web-vorschau-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(dir, f);
                if (rel.StartsWith("node_modules", StringComparison.OrdinalIgnoreCase)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(tmp, rel))!);
                File.Copy(f, Path.Combine(tmp, rel));
            }
            Revise(tmp, instruction);
            var removed = new List<string>(); var added = new List<string>();
            foreach (var f in Directory.EnumerateFiles(tmp, "*.*", SearchOption.AllDirectories).Where(f => Path.GetExtension(f) is ".html" or ".css" or ".js"))
            {
                var orig = Path.Combine(dir, Path.GetRelativePath(tmp, f));
                var before = File.Exists(orig) ? File.ReadAllLines(orig).Select(l => l.Trim()).Where(l => l.Length > 0).ToHashSet() : [];
                var after = File.ReadAllLines(f).Select(l => l.Trim()).Where(l => l.Length > 0).ToHashSet();
                removed.AddRange(before.Except(after)); added.AddRange(after.Except(before));
            }
            return (removed, added);
        }
        finally { try { Directory.Delete(tmp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    public static WebsiteBuildResult Revise(string dir, string instruction)
    {
        var text = instruction.Trim();
        if (text.Length == 0) throw new ArgumentException("Schreib, was sich ändern soll.");
        var content = Load(dir);
        var head = Regex.Match(text, "(?i)überschrift\\s*(?:in|zu|:|auf)\\s*[„\"']?(.*?)[“\"']?\\s*$");
        var sub = Regex.Match(text, "(?i)^untertitel\\s*:\\s*(.+)$");
        var color = Regex.Match(text, "(?i)(?:farbe|akzent)\\s*:?\\s*(#[0-9A-Fa-f]{6})");
        if (head.Success && WebsitePlan.Clean(head.Groups[1].Value) is { Length: >= 2 } line)
            content.Headline = line;
        else if (sub.Success)
            content.Subline = WebsitePlan.Clean(sub.Groups[1].Value);
        else if (color.Success)
            content.Style = content.Style with { Id = "eigen", Label = "Eigen", Accent = color.Groups[1].Value };
        else
            content.Sections.Add(new WebsiteSection { Id = "nachtrag-" + (content.Sections.Count + 1), Title = "Ergänzung", Text = WebsitePlan.Clean(text) });
        content.Description = WebsitePlan.Describe(content.Headline, content.Subline);
        WebsiteHtml.Write(dir, content);
        var quality = WebsiteQuality.Check(dir);
        WebsiteQuality.Save(dir, quality);
        return new(dir, content, quality, "Änderung übernommen. Die erzeugten Dateien wurden neu geschrieben.");
    }

    public static WebsiteBuildResult ApplyStyle(string dir, WebsiteStyle style)
    {
        var content = Load(dir);
        var id = string.IsNullOrWhiteSpace(style.Id) ? "eigen" : style.Id;
        var label = string.IsNullOrWhiteSpace(style.Label) ? "Eigen" : style.Label;
        content.Style = style with { Id = id, Label = label, Font = WebsitePlan.SanitizeFont(style.Font), Radius = Math.Clamp(style.Radius, 0, 40) };
        WebsiteHtml.Write(dir, content);
        var quality = WebsiteQuality.Check(dir);
        WebsiteQuality.Save(dir, quality);
        return new(dir, content, quality, "Stil „" + content.Style.Label + "“ ist in der Seite.");
    }

    public static ApprovalRequest RequestPack(ApprovalGate gate, Principal who, string projectDir) =>
        gate.Request(who, ActionPack, "Webseiten-Paket nur auf diesem PC speichern. Es wird nichts hochgeladen.", Path.GetFullPath(projectDir));

    public static string Pack(ApprovalGate gate, string approvalId, string projectDir)
    {
        var full = Path.GetFullPath(projectDir);
        if (!File.Exists(Path.Combine(full, "index.html"))) throw new InvalidOperationException("In diesem Ordner liegt keine Webseite.");
        var parent = Path.GetDirectoryName(full) ?? full;
        var zip = Path.Combine(parent, Path.GetFileName(full) + "-paket.zip");
        for (var n = 2; File.Exists(zip); n++) zip = Path.Combine(parent, Path.GetFileName(full) + "-paket-" + n + ".zip");
        return gate.Execute(approvalId, ActionPack, full, () =>
        {
            File.WriteAllText(Path.Combine(full, "NICHT-HOCHGELADEN.txt"), "Trendo hat diese Seite nicht ins Internet gestellt.\nDas Paket liegt nur auf diesem PC.\n");
            ZipFile.CreateFromDirectory(full, zip);
            return zip;
        });
    }

    public static WebsiteBrief Example(string technique, string? media = null, string? model = null, bool cube = false) => technique switch
    {
        "geschichte" => new("Eine ruhige Landingpage für eine Berghütte, die Stille vermietet.", "Berghütte Stille", Kind: "landing", Technique: "geschichte", StyleId: "klar"),
        "parallax" => new("Portfolio einer Fotografin mit Tiefe beim Scrollen.", "Lichtbild", Kind: "portfolio", Technique: "parallax", StyleId: "verspielt"),
        "drei" => new("Eine Lampe, die sich im Raum ansehen lässt.", "Lampe Nord", Kind: "shop", Technique: "drei", StyleId: "technik", ModelPath: model, ExampleCube: cube || model is null),
        "partikel" => new("Festliche Seite mit Partikeln für ein kleines Kino.", "Kinosaal", Kind: "landing", Technique: "partikel", StyleId: "luxus"),
        "video" => new("Firmenauftritt mit bewegtem Hintergrund für ein Atelier.", "Atelier Feld", Kind: "firma", Technique: "video", StyleId: "luxus", MediaPath: media),
        "mikro" => new("Klare Seite mit feinen Bewegungen für eine Praxis.", "Praxis Nord", Kind: "landing", Technique: "mikro", StyleId: "klar"),
        _ => throw new ArgumentException("Unbekannte Technik: " + technique)
    };
}

/// <summary>Lokale KI, freiwillig. Liefert null, wenn keine antwortet. Der Text ist Inhalt, keine Anweisung.</summary>
public delegate Task<string?> WebsiteAi(string prompt, CancellationToken ct);
