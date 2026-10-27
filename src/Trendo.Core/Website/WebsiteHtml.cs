using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Schreibt die echte, bearbeitbare Seite. Bibliotheken kommen aus dem Programm, nicht aus dem Netz.</summary>
public static class WebsiteHtml
{
    static readonly UTF8Encoding Utf8 = new(false);

    public static void Write(string dir, WebsiteContent c)
    {
        c.Sections ??= [];
        c.Frames ??= [];
        foreach (var s in c.Sections) s.Items ??= [];
        c.Style ??= WebsiteStyle.Klar;
        Directory.CreateDirectory(Path.Combine(dir, "assets", "vendor"));
        File.WriteAllText(Path.Combine(dir, "stil.css"), Css(c), Utf8);
        File.WriteAllText(Path.Combine(dir, "index.html"), Index(dir, c), Utf8);
        File.WriteAllText(Path.Combine(dir, "impressum.html"), Simple(c, "Impressum", "Pflichtangaben fehlen noch. Trendo erfindet keinen Namen und keine Adresse.", ImpressumBody(c)), Utf8);
        if (c.Kind == "blog") WriteArticles(dir, c);
        WebsiteAssets.Copy("trendo-scroll.js", Path.Combine(dir, "assets", "vendor", "trendo-scroll.js"));
        WebsiteAssets.Copy("seite.js", Path.Combine(dir, "seite.js"));
        WebsiteAssets.Copy("LIZENZEN.txt", Path.Combine(dir, "assets", "vendor", "LIZENZEN.txt"));
        if (NeedsThree(c))
        {
            WebsiteAssets.Copy("trendo-three.js", Path.Combine(dir, "assets", "vendor", "trendo-three.js"));
            WebsiteAssets.Copy("THREE-LICENSE.txt", Path.Combine(dir, "assets", "vendor", "THREE-LICENSE.txt"));
        }
        if (c.PixelStreaming) WebsiteUnreal.WritePixelDoc(dir);
        if (c.Vite) WriteVite(dir, c);
        File.WriteAllText(Path.Combine(dir, ".gitignore"), "node_modules/\ndist/\n.vorschau/\n", Utf8);
        WebsiteJson.Write(Path.Combine(dir, "inhalt.json"), c);
        WebsiteJson.Write(Path.Combine(dir, "stil.json"), c.Style);
        File.WriteAllText(Path.Combine(dir, "README.md"), Readme(c), Utf8);
        File.WriteAllText(Path.Combine(dir, "PROJEKT.md"), Projekt(c), Utf8);
    }

    public static bool NeedsThree(WebsiteContent c) => c.Technique == "drei" || !string.IsNullOrWhiteSpace(c.ModelFile);

    public static string Css(WebsiteContent c)
    {
        var s = c.Style;
        string H(string v, string fallback) => Regex.IsMatch(v ?? "", "^#[0-9A-Fa-f]{6}$") ? v! : fallback;
        var sb = new StringBuilder();
        sb.Append(":root {\n");
        sb.Append("  --font: ").Append(s.Font).Append(";\n");
        sb.Append("  --background: ").Append(H(s.Background, "#f5f5f7")).Append(";\n");
        sb.Append("  --surface: ").Append(H(s.Surface, "#ffffff")).Append(";\n");
        sb.Append("  --text: ").Append(H(s.Text, "#1d1d1f")).Append(";\n");
        sb.Append("  --muted: ").Append(H(s.Muted, "#3a3a3c")).Append(";\n");
        sb.Append("  --accent: ").Append(H(s.Accent, "#0b4f9c")).Append(";\n");
        sb.Append("  --accent-text: ").Append(H(s.AccentText, "#ffffff")).Append(";\n");
        sb.Append("  --radius: ").Append(Math.Clamp(s.Radius, 0, 40)).Append("px;\n}\n");
        sb.Append("""
*, *::before, *::after { box-sizing: border-box; }
html { scroll-behavior: smooth; }
body { margin: 0; overflow-x: hidden; font-family: var(--font); font-size: 1.125rem; line-height: 1.5; background: var(--background); color: var(--text); }
img, video, canvas { max-width: 100%; }
a { color: var(--accent); }
[hidden] { display: none !important; }
.sprung { position: absolute; left: 1rem; top: -4rem; background: var(--accent); color: var(--accent-text); padding: 0.6rem 1rem; z-index: 6; }
.sprung:focus { top: 1rem; }
.kopf, .fuss, .schmal { width: min(1120px, calc(100% - 2rem)); margin: 0 auto; }
.kopf { display: flex; justify-content: space-between; gap: 1rem; align-items: center; padding: 1rem 0; }
.kopf nav { display: flex; flex-wrap: wrap; gap: 0.35rem; }
.kopf a, .fuss a { text-decoration: none; padding: 0.55rem 0.75rem; min-height: 44px; display: inline-flex; align-items: center; }
.marke { font-weight: 600; margin: 0; }
h1 { font-size: clamp(2.4rem, 6vw, 4.6rem); line-height: 1.05; letter-spacing: -0.03em; margin: 0 0 0.35em; }
h2 { font-size: clamp(1.6rem, 3vw, 2.2rem); line-height: 1.15; margin: 0 0 0.4em; }
h3 { margin: 0 0 0.3em; font-size: 1.2rem; }
.held, .kapitel { position: relative; min-height: 100vh; display: flex; align-items: flex-end; }
.held .text, .kapitel .text { position: relative; z-index: 2; max-width: 40rem; padding-bottom: 8vh; }
.kicker { letter-spacing: 0.14em; text-transform: uppercase; font-size: 0.78rem; margin: 0 0 0.8rem; }
.lead { font-size: 1.25rem; color: var(--muted); }
.akzentlinie { width: 4.5rem; height: 4px; background: var(--accent); border: 0; margin: 0 0 1rem; }
.abschnitt { padding: 4rem 0; }
.raster { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 1rem; }
.karte { background: var(--surface); color: var(--text); border-radius: var(--radius); padding: 1.1rem 1.15rem 1.2rem; box-shadow: 0 10px 30px rgba(0,0,0,0.06); transition: transform 0.2s ease, box-shadow 0.2s ease; }
.karte:hover, .zeigen { transform: translateY(-4px); box-shadow: 0 12px 24px rgba(0,0,0,0.16); }
button, .knopf { min-height: 44px; min-width: 44px; padding: 0.7rem 1.15rem; font: inherit; font-size: 1rem; border: 0; border-radius: var(--radius); background: var(--accent); color: var(--accent-text); cursor: pointer; }
button:focus-visible, a:focus-visible, input:focus-visible, textarea:focus-visible { outline: 3px solid var(--accent); outline-offset: 3px; }
label { display: block; margin: 0.8rem 0 0.3rem; }
input, textarea { width: min(32rem, 100%); font: inherit; font-size: 1rem; color: var(--text); background: var(--surface); border: 1px solid var(--muted); border-radius: 8px; padding: 0.7rem 0.8rem; min-height: 44px; }
textarea { min-height: 8rem; }
.preis { font-weight: 600; }
.fuss { padding: 2rem 0 3rem; color: var(--muted); }
.fortschritt-spur { position: fixed; left: 0; top: 0; bottom: 0; width: 6px; background: rgba(127,127,127,0.35); z-index: 4; }
.fortschritt { height: 100%; width: 100%; background: var(--accent); transform: scaleY(0.08); transform-origin: top; }
.schicht { position: absolute; border-radius: 50%; z-index: 0; }
.schicht.weit { width: min(46vw, 520px); height: min(46vw, 520px); background: var(--accent); opacity: 0.22; top: 8vh; right: -4vw; }
.schicht.nah { width: min(28vw, 320px); height: min(28vw, 320px); background: var(--text); opacity: 0.1; bottom: 6vh; left: -3vw; }
.buehne-voll { position: absolute; inset: 0; z-index: 0; }
.buehne-voll canvas, .buehne-voll video, .buehne-voll .folge { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; display: block; }
.held.dunkel .text, .held.dunkel h1, .held.dunkel p, .held.dunkel .kicker { color: #f4f1ea; }
.held.dunkel .lead { color: #e4e0d8; }
.film-ersatz { position: absolute; inset: 0; display: grid; place-items: center; background: #142033; color: #f4f1ea; text-align: center; padding: 2rem; }
.spalte { display: flex; flex-wrap: wrap; gap: 0.6rem; margin-top: 1rem; }
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after { animation: none !important; transition: none !important; scroll-behavior: auto !important; }
}
@media (max-width: 700px) {
  .kopf { flex-direction: column; align-items: flex-start; }
  .held, .kapitel { padding-top: 5.5rem; min-height: 100vh; }
  .raster { grid-template-columns: 1fr; }
  .held .text, .kapitel .text { padding-bottom: 2.5rem; }
}
""");
        return sb.ToString();
    }

    static string Index(string dir, WebsiteContent c)
    {
        var b64 = ModelBase64(dir, c);
        var url = b64 is null && !string.IsNullOrWhiteSpace(c.ModelFile) ? c.ModelFile! : "";
        var dark = c.Technique is "drei" or "partikel" or "video";
        var sb = new StringBuilder();
        sb.Append(Head(c, c.Headline, c.Description, c.Technique, c.ModelMotion, c.Style.Accent, dark ? "#10141c" : c.Style.Background, url));
        if (c.Technique == "geschichte")
            sb.Append("<div class=\"fortschritt-spur\" aria-hidden=\"true\"><div class=\"fortschritt\" data-fortschritt></div></div>\n");
        sb.Append("<header class=\"kopf\"><p class=\"marke\">").Append(E(c.Name)).Append("</p><nav aria-label=\"Bereiche\">");
        sb.Append("<a href=\"#start\">Anfang</a>");
        foreach (var s in c.Sections) sb.Append("<a href=\"#").Append(s.Id).Append("\">").Append(E(s.Title)).Append("</a>");
        sb.Append("<a href=\"impressum.html\">Impressum</a></nav></header>\n<main id=\"inhalt\">\n");
        sb.Append(Hero(c, b64));
        foreach (var s in c.Sections) sb.Append(Section(c, s));
        if (NeedsThree(c) && c.Technique != "drei") sb.Append(ModelSection(c, b64));
        if (c.Frames.Count > 0 && c.Technique != "video") sb.Append(FrameBlock(c, "abschnitt schmal"));
        if (c.PixelStreaming)
            sb.Append("<section class=\"abschnitt schmal\" id=\"pixel\"><h2>Pixel Streaming</h2><p>Pixel Streaming ist nur eine Option und hier nicht verbunden. Dafür brauchst du einen eigenen Server. Trendo startet keinen.</p><p>Die Notiz dazu liegt in PIXEL-STREAMING.md im Projektordner.</p></section>\n");
        sb.Append("</main>\n<footer class=\"fuss\"><p><a href=\"impressum.html\">Impressum</a> · <a href=\"assets/vendor/LIZENZEN.txt\">Lizenzen</a></p></footer>\n");
        if (!string.IsNullOrEmpty(b64)) sb.Append("<script id=\"modell-daten\" type=\"text/plain\">").Append(b64).Append("</script>\n");
        sb.Append(Scripts(c));
        sb.Append("</body></html>\n");
        return sb.ToString();
    }

    static string Hero(WebsiteContent c, string? b64)
    {
        var sb = new StringBuilder();
        var dark = c.Technique is "drei" or "partikel" or "video";
        var kapitel = c.Technique == "geschichte" ? " kapitel\" data-kapitel=\"" : "";
        sb.Append("<section class=\"held").Append(dark ? " dunkel" : "").Append(kapitel).Append("\" id=\"start\">\n");
        switch (c.Technique)
        {
            case "parallax":
                sb.Append("<div class=\"schicht weit\" data-parallax=\"0.18\"></div><div class=\"schicht nah\" data-parallax=\"-0.22\"></div>\n");
                break;
            case "drei":
                sb.Append("<div class=\"buehne-voll\">");
                sb.Append(Canvas());
                sb.Append("</div>\n");
                break;
            case "partikel":
                sb.Append("<div class=\"buehne-voll\"><canvas id=\"shader\" aria-hidden=\"true\"></canvas><canvas id=\"partikel\" aria-hidden=\"true\"></canvas></div>\n");
                break;
            case "video":
                sb.Append("<div class=\"buehne-voll\">");
                if (!string.IsNullOrWhiteSpace(c.VideoFile))
                {
                    var poster = string.IsNullOrWhiteSpace(c.PosterFile) ? "" : " poster=\"" + A(c.PosterFile!) + "\"";
                    var type = c.VideoFile!.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ? "video/webm" : "video/mp4";
                    sb.Append("<video class=\"film\" autoplay muted loop playsinline").Append(poster).Append(" aria-hidden=\"true\"><source src=\"").Append(A(c.VideoFile)).Append("\" type=\"").Append(type).Append("\"></video>\n");
                }
                else if (c.Frames.Count > 0) sb.Append(FrameVisual(c, inHero: true));
                else sb.Append("<div class=\"film-ersatz\"><p>Noch kein Video hinterlegt. Lege eine mp4-Datei ab, dann läuft sie hier.</p></div>\n");
                sb.Append("</div>\n");
                break;
        }
        sb.Append("<div class=\"schmal text\"><hr class=\"akzentlinie\"><p class=\"kicker\">").Append(E(Kicker(c))).Append("</p><h1>").Append(E(c.Headline)).Append("</h1><p class=\"lead\">").Append(E(c.Subline)).Append("</p>");
        if (c.Technique == "geschichte") sb.Append("<p>Scrollen führt durch die Kapitel.</p>");
        if (c.Technique == "drei") sb.Append(TurnButtons());
        if (c.Technique == "mikro") sb.Append(MicroCards(c));
        if (!string.IsNullOrWhiteSpace(c.ModelNote) && c.Technique == "drei") sb.Append("<p id=\"modell-hinweis\">").Append(E(c.ModelNote)).Append("</p>");
        sb.Append("</div></section>\n");
        if (c.Technique == "video" && c.Frames.Count > 0 && !string.IsNullOrWhiteSpace(c.VideoFile))
            sb.Append("<section class=\"abschnitt schmal\" id=\"folge\"><h2>Bildfolge</h2><p>").Append(E(c.MediaNote)).Append("</p>").Append(FrameVisual(c, false)).Append("</section>\n");
        return sb.ToString();
    }

    static string Section(WebsiteContent c, WebsiteSection s)
    {
        var sb = new StringBuilder();
        var extra = c.Technique == "geschichte" ? " kapitel\" data-kapitel=\"" : "";
        sb.Append("<section class=\"abschnitt schmal").Append(extra).Append("\" id=\"").Append(s.Id).Append("\"><div class=\"text\"><h2>").Append(E(s.Title)).Append("</h2><p>").Append(E(s.Text)).Append("</p>");
        if (s.Id == "kontakt") sb.Append(Form());
        else if (s.Items.Count > 0)
        {
            sb.Append("<div class=\"raster\">");
            foreach (var item in s.Items)
            {
                sb.Append("<article class=\"karte\"><h3>");
                if (!string.IsNullOrWhiteSpace(item.Href)) sb.Append("<a href=\"").Append(A(item.Href!)).Append("\">").Append(E(item.Title)).Append("</a>");
                else sb.Append(E(item.Title));
                sb.Append("</h3><p>").Append(E(item.Text)).Append("</p>");
                if (!string.IsNullOrWhiteSpace(item.Price)) sb.Append("<p class=\"preis\">").Append(E(item.Price)).Append("</p>");
                if (c.Kind == "shop" && s.Id == "angebot") sb.Append("<button type=\"button\" data-waren>In den Korb</button>");
                sb.Append("</article>");
            }
            sb.Append("</div>");
            if (c.Kind == "shop" && s.Id == "angebot") sb.Append("<p id=\"korb\">Im Korb: 0 (Beispiel, es wird nichts bestellt)</p>");
        }
        sb.Append("</div></section>\n");
        return sb.ToString();
    }

    static string MicroCards(WebsiteContent c)
    {
        var items = c.Sections.SelectMany(s => s.Items).Take(3).ToList();
        if (items.Count == 0) items.Add(new WebsiteItem { Title = c.Name, Text = c.Subline });
        var sb = new StringBuilder("<div class=\"raster\">");
        for (var i = 0; i < items.Count; i++)
        {
            var show = i == 1 ? " zeigen" : "";
            sb.Append("<article class=\"karte").Append(show).Append("\"><p class=\"marke\">").Append(E(items[i].Title)).Append("</p><p>").Append(E(items[i].Text)).Append("</p>");
            if (i == 1) sb.Append("<p>Beim Zeigen hebt sich die Karte.</p>");
            sb.Append("</article>");
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    static string ModelSection(WebsiteContent c, string? b64)
    {
        var sb = new StringBuilder();
        sb.Append("<section class=\"abschnitt schmal\" id=\"modell\"><h2>Modell</h2><p id=\"modell-hinweis\">").Append(E(string.IsNullOrWhiteSpace(c.ModelNote) ? "Modell in der Seite." : c.ModelNote)).Append("</p>");
        sb.Append("<div class=\"karte\" style=\"position:relative;height:420px;padding:0\">");
        sb.Append("<div class=\"buehne-voll\">").Append(Canvas()).Append("</div>");
        sb.Append("<div class=\"spalte\" style=\"position:relative;z-index:2;padding:1rem\">");
        sb.Append(TurnButtons());
        sb.Append("</div></div></section>\n");
        return sb.ToString();
    }

    static string Canvas() =>
        "<canvas id=\"buehne\" width=\"1280\" height=\"720\" aria-label=\"Dreidimensionale Ansicht. Mit der Maus oder den Knöpfen drehen.\"></canvas>";

    static string TurnButtons() =>
        "<div class=\"spalte\"><button type=\"button\" id=\"dreh-links\">Nach links drehen</button><button type=\"button\" id=\"dreh-rechts\">Nach rechts drehen</button></div>";

    static string FrameBlock(WebsiteContent c, string cls) =>
        "<section class=\"" + cls + "\" id=\"folge\"><h2>Bildfolge</h2><p>" + E(c.MediaNote) + "</p>" + FrameVisual(c, false) + "</section>\n";

    static string FrameVisual(WebsiteContent c, bool inHero)
    {
        var first = c.Frames[0];
        var json = JsonSerializer.Serialize(c.Frames);
        var sb = new StringBuilder();
        if (!inHero) sb.Append("<div class=\"karte\" style=\"position:relative;min-height:280px;padding:0\">");
        sb.Append("<img class=\"folge\" src=\"").Append(A(first)).Append("\" alt=\"Erstes Bild der Folge\">");
        sb.Append("<canvas id=\"bildfolge\" aria-hidden=\"true\"></canvas>");
        sb.Append("<script id=\"bildfolge-daten\" type=\"application/json\">").Append(json).Append("</script>");
        if (!inHero) sb.Append("</div>");
        return sb.ToString();
    }

    static string Form() => """
<form id="kontaktform">
<label for="kf-name">Name</label><input id="kf-name" name="name" autocomplete="name">
<label for="kf-mail">E-Mail</label><input id="kf-mail" name="mail" type="email" autocomplete="email">
<label for="kf-text">Nachricht</label><textarea id="kf-text" name="text"></textarea>
<button type="submit">Nachricht merken</button>
<p id="kontakt-hinweis" hidden>Die Nachricht bleibt in diesem Fenster. Trendo sendet sie nicht.</p>
</form>
""";

    static void WriteArticles(string dir, WebsiteContent c)
    {
        var section = c.Sections.FirstOrDefault(s => s.Id == "artikel");
        if (section is null) return;
        var n = 1;
        foreach (var item in section.Items)
        {
            var file = string.IsNullOrWhiteSpace(item.Href) ? "artikel-" + n + ".html" : item.Href!;
            if (file.IndexOfAny(['/', '\\', ':']) >= 0 || file.Contains("..")) file = "artikel-" + n + ".html";
            item.Href = file;
            var body = "<p>" + E(item.Text) + "</p><p>Dieser Text ist ein Anfang. Ersetze ihn durch deinen Artikel.</p><p><a href=\"index.html#artikel\">Zurück zur Übersicht</a></p>";
            File.WriteAllText(Path.Combine(dir, file), Simple(c, item.Title, item.Text, body), Utf8);
            n++;
        }
    }

    static string Simple(WebsiteContent c, string title, string description, string body)
    {
        var sb = new StringBuilder();
        sb.Append(Head(c, title, WebsitePlan.Describe(title, description), "seite", "keine", c.Style.Accent, c.Style.Background, ""));
        sb.Append("<header class=\"kopf\"><p class=\"marke\"><a href=\"index.html\">").Append(E(c.Name)).Append("</a></p></header>\n<main id=\"inhalt\" class=\"schmal abschnitt\"><h1>").Append(E(title)).Append("</h1>").Append(body).Append("</main>\n<footer class=\"fuss\"><p><a href=\"index.html\">Zur Startseite</a></p></footer>\n</body></html>\n");
        return sb.ToString();
    }

    static string ImpressumBody(WebsiteContent c) =>
        "<p>Für „" + E(c.Name) + "“ fehlen noch die Pflichtangaben.</p><ul><li>Name und Anschrift</li><li>Kontakt</li><li>Falls nötig: Register und Umsatzsteuer-ID</li></ul><p>Trendo setzt hier nichts ein, was du nicht selbst geschrieben hast.</p><p><a href=\"index.html\">Zur Startseite</a></p>";

    static string Head(WebsiteContent c, string title, string description, string technique, string motion, string accent, string stage, string modelUrl)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"de\">\n<head>\n<meta charset=\"utf-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append("<title>").Append(E(title)).Append("</title>\n");
        sb.Append("<meta name=\"description\" content=\"").Append(A(description)).Append("\">\n");
        sb.Append("<meta name=\"theme-color\" content=\"").Append(A(accent)).Append("\">\n");
        sb.Append("<link rel=\"stylesheet\" href=\"stil.css\">\n</head>\n<body data-technik=\"").Append(A(technique)).Append("\" data-bewegung=\"").Append(A(motion)).Append("\" data-farbe=\"").Append(A(accent)).Append("\" data-buehne=\"").Append(A(stage)).Append("\"");
        if (modelUrl.Length > 0) sb.Append(" data-modell-url=\"").Append(A(modelUrl)).Append("\"");
        sb.Append(">\n<a class=\"sprung\" href=\"#inhalt\">Zum Inhalt</a>\n");
        return sb.ToString();
    }

    static string Scripts(WebsiteContent c)
    {
        var sb = new StringBuilder();
        sb.Append("<script src=\"assets/vendor/trendo-scroll.js\"></script>\n");
        if (NeedsThree(c)) sb.Append("<script src=\"assets/vendor/trendo-three.js\"></script>\n");
        sb.Append("<script src=\"seite.js\"></script>\n");
        return sb.ToString();
    }

    static string? ModelBase64(string dir, WebsiteContent c)
    {
        if (string.IsNullOrWhiteSpace(c.ModelFile) || !c.ModelFile.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) return null;
        var path = Path.Combine(dir, c.ModelFile.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return null;
        var bytes = File.ReadAllBytes(path);
        return bytes.Length is 0 or > 1_500_000 ? null : Convert.ToBase64String(bytes);
    }

    static void WriteVite(string dir, WebsiteContent c)
    {
        var name = DevStudio.SafeName(c.Name).ToLowerInvariant();
        File.WriteAllText(Path.Combine(dir, "package.json"), "{\n  \"name\": \"" + name + "\",\n  \"private\": true,\n  \"version\": \"0.1.0\",\n  \"scripts\": {\n    \"dev\": \"vite\",\n    \"build\": \"vite build\",\n    \"preview\": \"vite preview\"\n  },\n  \"devDependencies\": { \"vite\": \"^6.0.0\" }\n}\n", Utf8);
        File.WriteAllText(Path.Combine(dir, "vite.config.js"), "export default { base: './', server: { host: '127.0.0.1' } };\n", Utf8);
    }

    static string Kicker(WebsiteContent c) => c.Technique switch
    {
        "geschichte" => "Kapitel 01",
        "parallax" => "Mit Tiefe",
        "drei" => "Im Raum",
        "partikel" => "Bewegter Grund",
        "video" => "Bewegtes Bild",
        "mikro" => "Feine Bewegung",
        _ => WebsiteKinds.Label(c.Kind)
    };

    static string Readme(WebsiteContent c)
    {
        var vite = c.Vite
            ? "\nVite ist dabei, aber nicht nötig. Mit Node startest du auf Wunsch `npm install` und `npm run dev`. Ohne Node öffnest du index.html.\n"
            : "\nÖffne index.html im Browser. Ein Baukasten mit Vite ist nicht eingeschaltet.\n";
        return "# " + c.Name + "\n\nSeite aus Trendo OS. Dateien zum Bearbeiten: index.html, stil.css, seite.js, inhalt.json, stil.json.\n" + vite + "Veröffentlicht ist sie nicht.\n";
    }

    static string Projekt(WebsiteContent c) =>
        "# " + c.Name + "\n\n"
        + "- Art: " + WebsiteKinds.Label(c.Kind) + "\n"
        + "- Technik: " + WebsiteTechniques.Label(c.Technique) + "\n"
        + "- Grund: " + c.TechniqueReason + "\n"
        + "- Stil: " + c.Style.Label + "\n"
        + "- Texte: " + (c.CopyFromAi ? "mit lokaler KI ergänzt" : "aus der Beschreibung, ohne KI") + "\n"
        + "- Modell: " + (string.IsNullOrWhiteSpace(c.ModelNote) ? "keins" : c.ModelNote) + "\n"
        + "- Medien: " + (string.IsNullOrWhiteSpace(c.MediaNote) ? "keine Datei" : c.MediaNote) + "\n"
        + "- Unreal: " + c.UnrealNote + "\n"
        + "- Pixel Streaming: " + (c.PixelStreaming ? "nur dokumentiert, nicht verbunden" : "nicht gewünscht") + "\n"
        + "- Vite: " + (c.Vite ? "optionale Dateien liegen bei, die Seite läuft auch ohne" : "aus") + "\n"
        + "- Veröffentlichung: nicht geschehen\n";

    static string E(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\'': sb.Append("&#39;"); break;
                default:
                    if (char.IsControl(ch) && ch is not ('\n' or '\t')) break;
                    sb.Append(ch);
                    break;
            }
        }
        return sb.ToString();
    }

    static string A(string? s) => E(s);
}

public static class WebsiteAssets
{
    public static void Copy(string file, string dest)
    {
        var asm = typeof(WebsiteAssets).Assembly;
        var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("." + file, StringComparison.Ordinal) || n.EndsWith(file, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Intern fehlt die Datei " + file + ".");
        using var stream = asm.GetManifestResourceStream(name) ?? throw new InvalidOperationException("Intern fehlt die Datei " + file + ".");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        using var output = File.Create(dest);
        stream.CopyTo(output);
    }
}
