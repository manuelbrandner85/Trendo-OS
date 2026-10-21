using System.Net;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>X-03: Baukasten ohne Programmieren – Seite aus Bausteinen zusammenklicken, als JSON speichern, als HTML veröffentlichen.</summary>
public sealed record Block(string Kind, string Text = "", string Url = "");

public sealed class SitePage
{
    public string Title { get; set; } = "Meine Seite";
    public string Accent { get; set; } = "#2F86F6";
    public List<Block> Blocks { get; set; } = [];

    public static readonly (string Kind, string Label)[] Kinds =
        [("h1", "Überschrift"), ("p", "Text"), ("img", "Bild"), ("button", "Knopf mit Link"), ("list", "Liste"), ("contact", "Kontakt per E-Mail"), ("hr", "Trennlinie")];

    public void Move(int i, int delta)
    {
        var j = i + delta;
        if (i < 0 || j < 0 || i >= Blocks.Count || j >= Blocks.Count) return;
        (Blocks[i], Blocks[j]) = (Blocks[j], Blocks[i]);
    }

    public static string SafeUrl(string u)
    {
        u = u.Trim();
        return Uri.TryCreate(u, UriKind.Absolute, out var x) && x.Scheme is "https" or "http" or "mailto" ? x.AbsoluteUri
            : !u.Contains(':') && FreeAssets.SafeRel(u) ? u : "#";
    }

    public string ToHtml()
    {
        string E(string s) => WebUtility.HtmlEncode(s);
        var acc = System.Text.RegularExpressions.Regex.IsMatch(Accent, "^#[0-9A-Fa-f]{6}$") ? Accent : "#2F86F6";
        var sb = new StringBuilder();
        sb.Append($"<!doctype html>\n<html lang=\"de\">\n<head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>{E(Title)}</title>\n");
        sb.Append($"<style>body{{font-family:system-ui,sans-serif;max-width:760px;margin:0 auto;padding:2rem 1rem;line-height:1.55;color:#1b2333}}h1{{color:{acc}}}a.btn{{display:inline-block;background:{acc};color:#fff;padding:.6rem 1.2rem;border-radius:8px;text-decoration:none}}img{{max-width:100%;border-radius:10px}}hr{{border:0;border-top:1px solid #dde}}</style>\n</head>\n<body>\n");
        foreach (var b in Blocks)
            sb.Append(b.Kind switch
            {
                "h1" => $"<h1>{E(b.Text)}</h1>\n",
                "p" => $"<p>{E(b.Text).Replace("\n", "<br>")}</p>\n",
                "img" => $"<p><img src=\"{E(SafeUrl(b.Url))}\" alt=\"{E(b.Text)}\"></p>\n",
                "button" => $"<p><a class=\"btn\" href=\"{E(SafeUrl(b.Url))}\">{E(b.Text)}</a></p>\n",
                "list" => "<ul>\n" + string.Concat(b.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(l => $"<li>{E(l)}</li>\n")) + "</ul>\n",
                "contact" => $"<p><a class=\"btn\" href=\"mailto:{E(Uri.EscapeDataString(b.Url.Trim()).Replace("%40", "@"))}\">{E(b.Text.Length > 0 ? b.Text : "Schreib mir")}</a></p>\n",
                "hr" => "<hr>\n",
                _ => "",
            });
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    public void Save(string file) => File.WriteAllText(file, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    public static SitePage Load(string file) => JsonSerializer.Deserialize<SitePage>(File.ReadAllText(file)) ?? new();

    /// <summary>Schreibt index.html (und kopiert lokale Bilder mit) in den Ausgabeordner.</summary>
    public string Publish(string outDir, string? assetDir = null)
    {
        Directory.CreateDirectory(outDir);
        if (assetDir is not null)
            foreach (var b in Blocks.Where(b => b.Kind == "img" && !b.Url.Contains(':') && FreeAssets.SafeRel(b.Url)))
            {
                var src = Path.Combine(assetDir, b.Url); var dst = Path.Combine(outDir, b.Url);
                if (File.Exists(src)) { Directory.CreateDirectory(Path.GetDirectoryName(dst)!); File.Copy(src, dst, true); }
            }
        var p = Path.Combine(outDir, "index.html"); File.WriteAllText(p, ToHtml(), new UTF8Encoding(false)); return p;
    }
}
