using System.Text;

namespace Trendo.Core;

/// <summary>X-01: Studio-Fusion – Abläufe über mehrere Studios. Ein Rezept ist eine Kette echter Schritte; Trendo zeigt erst den Plan, führt nach Freigabe aus und protokolliert jeden Schritt.</summary>
public sealed record FusionStep(string Studio, string Label, Func<FusionContext, string> Run);
public sealed record Recipe(string Id, string Label, string Help, string[] Needs, IReadOnlyList<FusionStep> Steps);
public sealed record StepResult(string Label, bool Ok, string Detail);

public sealed class FusionContext
{
    public required string WorkDir { get; init; }
    public Dictionary<string, string> Input { get; init; } = new();
    public Dictionary<string, string> Out { get; } = new();
    public string? Ffmpeg { get; init; }
    public string? Blender { get; init; }
    public string In(string k) => Input.TryGetValue(k, out var v) ? v : "";
}

public static class Fusion
{
    public static readonly IReadOnlyList<Recipe> Recipes =
    [
        new("idea-site", "Geschäftsidee → Bauplan → Webseite", "Aus deiner Idee entstehen Bauplan und eine fertige Angebotsseite.", ["idea"],
        [
            new("Business", "Idee bewerten und Bauplan schreiben", c =>
            {
                var i = new BusinessIdea { Name = c.In("name"), Problem = c.In("problem"), Audience = c.In("audience"), Solution = c.In("solution"), Price = double.TryParse(c.In("price").Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 0, HoursWeek = 10, CustomersMonth = 10 };
                var f = Path.Combine(c.WorkDir, "Bauplan.md"); File.WriteAllText(f, BusinessStudio.Blueprint(i)); c.Out["verdict"] = BusinessStudio.Evaluate(i).Decision; c.Out["blueprint"] = f;
                return $"Bewertung: {c.Out["verdict"]}";
            }),
            new("Baukasten", "Angebotsseite bauen", c =>
            {
                var pg = new SitePage { Title = c.In("name"), Blocks = [new("h1", c.In("name")), new("p", c.In("problem")), new("p", c.In("solution")),
                    new("list", $"Für: {c.In("audience")}\nPreis: {c.In("price")} €"), new("contact", "Jetzt vormerken", c.In("email"))] };
                pg.Save(Path.Combine(c.WorkDir, "seite.json")); c.Out["site"] = pg.Publish(Path.Combine(c.WorkDir, "webseite")); return "index.html geschrieben";
            }),
            new("Vorhaben", "Schrittliste zum Start", c => { var d = CreationSuite.Create(c.WorkDir, "web", "Start"); c.Out["steps"] = d; return CreationSuite.Steps(d).Count + " Schritte"; }),
        ]),
        new("video-pack", "Video → Upload-Paket", "Aus einem Video werden MP4, Vorschaubild, Ton und GIF.", ["ffmpeg", "file"],
        [
            new("Medien", "In MP4 umwandeln", c => Media(c, "mp4")),
            new("Medien", "Vorschaubild", c => Media(c, "thumb")),
            new("Medien", "Ton als MP3", c => Media(c, "mp3")),
            new("Medien", "GIF für soziale Netze", c => Media(c, "gif")),
        ]),
        new("logo-print", "3D-Schriftzug → Druckdatei", "Aus einem Wort wird ein 3D-Modell, eine Vorschau und eine STL-Datei für den 3D-Drucker.", ["blender", "text"],
        [
            new("3D", "3D-Schrift anlegen", c => { var (r, o) = Studio3D.Create(c.Blender!, c.WorkDir, "text", "Logo", ".blend", "#2F86F6", c.In("text")); if (!r.Ok) throw new InvalidOperationException(r.Err); c.Out["blend"] = o; return Path.GetFileName(o); }),
            new("3D", "Vorschau rendern", c => { var (r, o) = Studio3D.Preview(c.Blender!, c.Out["blend"]); if (!r.Ok) throw new InvalidOperationException(r.Err); c.Out["png"] = o; return Path.GetFileName(o); }),
            new("3D", "Als STL speichern", c => { var (r, o) = Studio3D.Convert(c.Blender!, c.Out["blend"], ".stl"); if (!r.Ok) throw new InvalidOperationException(r.Err); c.Out["stl"] = o; return Path.GetFileName(o); }),
        ]),
    ];

    static string Media(FusionContext c, string job)
    {
        var (r, o) = MediaStudio.Run(c.Ffmpeg!, c.In("file"), job);
        if (!r.Ok) throw new InvalidOperationException(r.Err.Trim());
        c.Out[job] = o; return Path.GetFileName(o);
    }

    public static string Plan(Recipe r) => string.Join("\n", r.Steps.Select((s, i) => $"{i + 1}. {s.Studio}: {s.Label}"));

    /// <summary>Was fehlt, bevor das Rezept laufen kann (leer = alles bereit).</summary>
    public static IReadOnlyList<string> Missing(Recipe r, FusionContext c)
    {
        var m = new List<string>();
        foreach (var n in r.Needs)
            switch (n)
            {
                case "ffmpeg" when c.Ffmpeg is null: m.Add("FFmpeg ist nicht installiert."); break;
                case "blender" when c.Blender is null: m.Add("Blender ist nicht installiert."); break;
                case "file" when !File.Exists(c.In("file")): m.Add("Wähle eine Videodatei."); break;
                case "text" when string.IsNullOrWhiteSpace(c.In("text")): m.Add("Gib ein Wort ein."); break;
                case "idea" when string.IsNullOrWhiteSpace(c.In("name")) || string.IsNullOrWhiteSpace(c.In("problem")): m.Add("Gib Name und Problem der Idee ein."); break;
            }
        return m;
    }

    /// <summary>Führt alle Schritte nacheinander aus und hält beim ersten Fehler an. Schreibt ein Protokoll in den Arbeitsordner.</summary>
    public static IReadOnlyList<StepResult> Run(Recipe r, FusionContext c, AuditLog? audit = null)
    {
        Directory.CreateDirectory(c.WorkDir);
        var res = new List<StepResult>();
        foreach (var s in r.Steps)
        {
            try { res.Add(new(s.Label, true, s.Run(c))); }
            catch (Exception ex) { res.Add(new(s.Label, false, ex.Message)); }
            audit?.Append("fusion", "fusion.step", $"{r.Id}: {s.Label} ok={res[^1].Ok}");
            if (!res[^1].Ok) break;
        }
        var sb = new StringBuilder($"Ablauf: {r.Label}\nZeit: {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}\n\n");
        foreach (var x in res) sb.AppendLine($"{(x.Ok ? "OK  " : "FEHLER")} {x.Label}: {x.Detail}");
        if (res.Count < r.Steps.Count) sb.AppendLine($"\nAngehalten. {r.Steps.Count - res.Count} Schritte nicht ausgeführt.");
        File.WriteAllText(Path.Combine(c.WorkDir, "PROTOKOLL.txt"), sb.ToString());
        return res;
    }
}
