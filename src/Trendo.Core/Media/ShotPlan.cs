using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 3, S3-01/S3-03 (07.11.2026): Drehbuch → Szenen und Shotliste mit Codes (SZ03-E02), Szenenlogik, Prüfung der Reihenfolge
/// (Widersprüche werden gemeldet, nie still umsortiert) und Storyboard-Tafeln für die Zeitleiste.
/// Schreibweise: Szenenkopf „3 INNEN. KÜCHE – TAG“ (Nummer optional, auch INT./EXT./AUSSEN), Einstellungen als „- Totale: Beschreibung (4 s)“.</summary>
public static class ShotPlan
{
    public sealed record Shot(string Id, string Type, string Description, double Seconds);
    public sealed record Scene(string Code, int? Number, string Place, string Location, string Time, IReadOnlyList<Shot> Shots);
    public sealed record Plan(IReadOnlyList<Scene> Scenes, IReadOnlyList<string> Problems)
    {
        public double Seconds => Scenes.Sum(s => s.Shots.Sum(x => x.Seconds));
        public IEnumerable<Shot> AllShots => Scenes.SelectMany(s => s.Shots);
    }

    public static readonly string[] Types = ["Supertotale", "Totale", "Halbtotale", "Amerikanisch", "Halbnah", "Nah", "Groß", "Detail", "Over-Shoulder", "POV", "Fahrt", "Schwenk"];

    static readonly Regex Head = new(@"^\s*(?:(\d+)\s*[.)]?\s+)?(INT|EXT|INNEN|AUSSEN|AUSSEN/INNEN|INNEN/AUSSEN)\.?\s+(.+?)(?:\s+[–-]\s+(.+))?\s*$", RegexOptions.IgnoreCase);
    static readonly Regex ShotLine = new(@"^\s*[-•*]\s*([^:]{2,30}):\s*(.+?)\s*(?:\((\d+(?:[.,]\d+)?)\s*s\))?\s*$");

    public static Plan Parse(string script)
    {
        var scenes = new List<(int? Num, string Place, string Loc, string Time, List<(string Type, string Desc, double? Sec)> Shots)>();
        var problems = new List<string>(); var lineNo = 0;
        foreach (var raw in (script ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            lineNo++; var l = raw.TrimEnd(); if (l.Trim().Length == 0) continue;
            if (Head.Match(l) is { Success: true } h)
            {
                var place = h.Groups[2].Value.ToUpperInvariant() switch { "INT" or "INNEN" => "Innen", "EXT" or "AUSSEN" => "Außen", _ => "Innen/Außen" };
                scenes.Add((h.Groups[1].Success ? int.Parse(h.Groups[1].Value, CultureInfo.InvariantCulture) : null, place, h.Groups[3].Value.Trim(), h.Groups[4].Success ? h.Groups[4].Value.Trim() : "", []));
                continue;
            }
            if (ShotLine.Match(l) is { Success: true } s)
            {
                if (scenes.Count == 0) { problems.Add($"Zeile {lineNo}: Einstellung „{s.Groups[1].Value.Trim()}“ steht vor dem ersten Szenenkopf."); continue; }
                double? sec = s.Groups[3].Success ? double.Parse(s.Groups[3].Value.Replace(',', '.'), CultureInfo.InvariantCulture) : null;
                var type = s.Groups[1].Value.Trim();
                if (!Types.Any(t => t.Equals(type, StringComparison.OrdinalIgnoreCase))) problems.Add($"Zeile {lineNo}: Einstellungsgröße „{type}“ unbekannt – übernommen, bitte prüfen.");
                scenes[^1].Shots.Add((type, s.Groups[2].Value.Trim(), sec));
            }
        }
        // Reihenfolge prüfen – melden, nicht umsortieren
        var nums = scenes.Where(x => x.Num is not null).Select(x => x.Num!.Value).ToList();
        for (var i = 1; i < nums.Count; i++) if (nums[i] <= nums[i - 1]) problems.Add($"Reihenfolge: Szene {nums[i]} steht nach Szene {nums[i - 1]} – bitte prüfen (Trendo sortiert nicht still um).");
        foreach (var d in nums.GroupBy(n => n).Where(g => g.Count() > 1)) problems.Add($"Szenennummer {d.Key} kommt {d.Count()}× vor.");
        for (var i = 0; i < scenes.Count; i++) if (scenes[i].Shots.Count == 0) problems.Add($"Szene {(scenes[i].Num?.ToString() ?? (i + 1).ToString())} „{scenes[i].Loc}“ hat keine Einstellung.");
        var result = new List<Scene>();
        for (var i = 0; i < scenes.Count; i++)
        {
            var sc = scenes[i]; var code = $"SZ{(sc.Num ?? i + 1):00}";
            result.Add(new Scene(code, sc.Num, sc.Place, sc.Loc, sc.Time, [.. sc.Shots.Select((x, k) => new Shot($"{code}-E{k + 1:00}", x.Type, x.Desc, Math.Clamp(x.Sec ?? 3, 0.5, 600)))]));
        }
        if (result.Count == 0) problems.Add("Kein Szenenkopf gefunden – Schreibweise z. B. „1 INNEN. KÜCHE – TAG“.");
        return new Plan(result, problems);
    }

    /// <summary>Shotliste als CSV (Semikolon, UTF-8 mit BOM für Tabellenprogramme).</summary>
    public static string Csv(Plan p)
    {
        var sb = new StringBuilder("ID;Szene;Innen/Außen;Ort;Tageszeit;Einstellung;Beschreibung;Dauer (s)\n");
        foreach (var s in p.Scenes) foreach (var x in s.Shots)
            sb.Append($"{x.Id};{s.Code};{s.Place};{s.Location.Replace(';', ',')};{s.Time};{x.Type};{x.Description.Replace(';', ',')};{x.Seconds.ToString("0.#", CultureInfo.InvariantCulture)}\n");
        return sb.ToString();
    }

    /// <summary>Storyboard-Tafel (Klappe) als kurzer Clip: Code, Einstellung, Ort und Beschreibung auf dunklem Grund, so lang wie geplant.</summary>
    public static List<string> SlateArgs(Scene s, Shot x, string font, string output, int w = 1280, int h = 720)
    {
        var f = font.Replace('\\', '/').Replace(":", "\\:");
        string T(string t) => VideoEdit.DrawText(t.Length > 70 ? t[..70] + "…" : t);
        var vf = string.Join(",",
            $"drawtext=fontfile='{f}':text='{T(x.Id)}':fontcolor=0xF5B83D:fontsize={h / 9}:x=(w-text_w)/2:y=h*0.22",
            $"drawtext=fontfile='{f}':text='{T(x.Type + " · " + s.Place + ". " + s.Location + (s.Time.Length > 0 ? " – " + s.Time : ""))}':fontcolor=white:fontsize={h / 20}:x=(w-text_w)/2:y=h*0.45",
            $"drawtext=fontfile='{f}':text='{T(x.Description)}':fontcolor=0xB8BCC6:fontsize={h / 26}:x=(w-text_w)/2:y=h*0.6");
        return ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", $"color=c=0x151A22:s={w}x{h}:r=25:d={x.Seconds.ToString("0.###", CultureInfo.InvariantCulture)}",
            "-vf", vf, "-c:v", "libx264", "-pix_fmt", "yuv420p", output];
    }
}
