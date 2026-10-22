using System.IO.Compression;
using Trendo.Core;
using Xunit;

public class WebsiteStudioTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-web-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "Webseite: jede Art wird ein echter Ordner, ohne Netz und ohne Upload")]
    public void Kinds_are_real_folders()
    {
        var root = Tmp();
        foreach (var (id, _) in WebsiteKinds.All)
        {
            var built = WebsiteStudio.Build(root, new WebsiteBrief("Eine kleine Seite über " + id + " für den Test im Studio.", "Art " + id, Kind: id, Technique: "mikro"));
            Assert.True(File.Exists(Path.Combine(built.Directory, "index.html")));
            Assert.True(File.Exists(Path.Combine(built.Directory, "stil.css")));
            Assert.True(File.Exists(Path.Combine(built.Directory, "seite.js")));
            Assert.True(File.Exists(Path.Combine(built.Directory, "inhalt.json")));
            var html = File.ReadAllText(Path.Combine(built.Directory, "index.html"));
            Assert.DoesNotContain("src=\"http", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("href=\"http", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("lang=\"de\"", html);
            Assert.True(built.Quality.Score >= 90, built.Quality.Plain());
        }
        var shop = File.ReadAllText(Path.Combine(root, "Art-shop", "index.html"));
        Assert.Contains("In den Korb", shop);
        Assert.Contains("nichts bestellt", shop);
        Assert.True(File.Exists(Path.Combine(root, "Art-blog", "artikel-1.html")));
        Assert.Contains("Beispielteam", File.ReadAllText(Path.Combine(root, "Art-firma", "index.html")));
        Assert.Contains("Arbeit eins", File.ReadAllText(Path.Combine(root, "Art-portfolio", "index.html")));
    }

    [Fact(DisplayName = "Webseite: Technik wird erkannt oder gewählt, Bibliotheken liegen lokal")]
    public void Techniques_and_local_libraries()
    {
        var root = Tmp();
        var auto = WebsiteStudio.Build(root, new WebsiteBrief("Ein Blog mit Parallax über das Gärtnern.", "Gartenblog"));
        Assert.Equal("blog", auto.Content.Kind);
        Assert.Equal("parallax", auto.Content.Technique);
        Assert.Contains("data-parallax", File.ReadAllText(Path.Combine(auto.Directory, "index.html")));
        Assert.False(auto.Content.CopyFromAi);

        foreach (var (id, _, _) in WebsiteTechniques.All)
        {
            var built = WebsiteStudio.Build(root, WebsiteStudio.Example(id));
            var html = File.ReadAllText(Path.Combine(built.Directory, "index.html"));
            Assert.Contains("data-technik=\"" + id + "\"", html);
            Assert.True(File.Exists(Path.Combine(built.Directory, "assets", "vendor", "trendo-scroll.js")));
            Assert.Contains("GSAP", File.ReadAllText(Path.Combine(built.Directory, "assets", "vendor", "LIZENZEN.txt")));
            Assert.DoesNotContain("cdn", html, StringComparison.OrdinalIgnoreCase);
            Assert.True(built.Quality.Score >= 90, id + "\n" + built.Quality.Plain());
            switch (id)
            {
                case "geschichte":
                    Assert.Contains("data-kapitel", html);
                    Assert.Contains("data-fortschritt", html);
                    break;
                case "parallax":
                    Assert.Contains("data-parallax", html);
                    break;
                case "drei":
                    Assert.Contains("id=\"buehne\"", html);
                    Assert.True(File.Exists(Path.Combine(built.Directory, "assets", "modell.glb")));
                    Assert.True(File.Exists(Path.Combine(built.Directory, "assets", "vendor", "trendo-three.js")));
                    Assert.Contains("MIT", File.ReadAllText(Path.Combine(built.Directory, "assets", "vendor", "THREE-LICENSE.txt")));
                    Assert.Contains("Beispielwürfel", html);
                    var info = XrInspector.Inspect(Path.Combine(built.Directory, "assets", "modell.glb"));
                    Assert.True(info.Triangles >= 12);
                    break;
                case "partikel":
                    Assert.Contains("id=\"partikel\"", html);
                    Assert.Contains("id=\"shader\"", html);
                    break;
                case "video":
                    Assert.Contains("Noch kein Video hinterlegt", html);
                    break;
                case "mikro":
                    Assert.Contains("zeigen", html);
                    Assert.Contains("Beim Zeigen", html);
                    break;
            }
        }
    }

    [Fact(DisplayName = "Webseite: Stil per Klick, Änderung, Vite optional, Ordner wird nicht überschrieben")]
    public void Style_revise_vite_and_guard()
    {
        var root = Tmp();
        var built = WebsiteStudio.Build(root, new WebsiteBrief("Landingpage für ein Café.", "Café Nord", Kind: "landing", Technique: "mikro", Vite: true));
        Assert.True(File.Exists(Path.Combine(built.Directory, "package.json")));
        Assert.True(File.Exists(Path.Combine(built.Directory, "vite.config.js")));
        Assert.False(Directory.Exists(Path.Combine(built.Directory, "node_modules")));
        Assert.Throws<IOException>(() => WebsiteStudio.Build(root, new WebsiteBrief("Noch einmal.", "Café Nord")));

        var styled = WebsiteStudio.ApplyStyle(built.Directory, WebsiteStyle.Luxus);
        Assert.Contains("#14110e", File.ReadAllText(Path.Combine(built.Directory, "stil.css")));
        Assert.Equal("luxus", styled.Content.Style.Id);
        foreach (var preset in WebsiteStyle.Presets)
        {
            Assert.True(WebsiteColor.Contrast(preset.Text, preset.Background) >= 4.5, preset.Id);
            Assert.True(WebsiteColor.Contrast(preset.Muted, preset.Background) >= 4.5, preset.Id + " leise");
            Assert.True(WebsiteColor.Contrast(preset.AccentText, preset.Accent) >= 4.5, preset.Id + " knopf");
        }

        var revised = WebsiteStudio.Revise(built.Directory, "Überschrift in Stille am See");
        Assert.Equal("Stille am See", revised.Content.Headline);
        Assert.Contains("Stille am See", File.ReadAllText(Path.Combine(built.Directory, "index.html")));
        Assert.Contains("#14110e", File.ReadAllText(Path.Combine(built.Directory, "stil.css")));
    }

    [Fact(DisplayName = "Webseite: KI-Text nur wenn er harmloses JSON ist")]
    public async Task Ai_json_is_data()
    {
        var root = Tmp();
        var good = await WebsiteStudio.BuildAsync(root, new WebsiteBrief("Eine Landingpage für eine Bücherei.", "Bücherei"), (_, _) => Task.FromResult<string?>("{\"headline\":\"Leise Regale\",\"subline\":\"Bücher borgen, ohne Eile.\",\"sections\":[{\"id\":\"nutzen\",\"title\":\"Ruhig lesen\",\"text\":\"Ein Sessel und eine Lampe.\"}]}"));
        Assert.True(good.Content.CopyFromAi);
        Assert.Contains("Leise Regale", File.ReadAllText(Path.Combine(good.Directory, "index.html")));
        Assert.Contains("Ruhig lesen", File.ReadAllText(Path.Combine(good.Directory, "index.html")));

        var bad = await WebsiteStudio.BuildAsync(root, new WebsiteBrief("Eine Landingpage für eine Bücherei.", "Bücherei Zwei"), (_, _) => Task.FromResult<string?>("{\"headline\":\"<script>alert(1)</script>\",\"subline\":\"https://cdn.example/x.js\",\"sections\":[{\"id\":\"nutzen\",\"text\":\"<script src=https://cdn.example/a.js></script>\"}]}"));
        var html = File.ReadAllText(Path.Combine(bad.Directory, "index.html"));
        Assert.DoesNotContain("<script>alert", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cdn.example", html, StringComparison.OrdinalIgnoreCase);
        Assert.False(bad.Content.CopyFromAi);
    }

    [Fact(DisplayName = "Webseite: GLB bleibt im Ordner, Blender fehlt ehrlich, fremde Pfade nicht mitnehmen")]
    public void Model_blender_and_path_guard()
    {
        var root = Tmp();
        var glb = Path.Combine(root, "wurf.glb");
        File.WriteAllBytes(glb, WebsiteGlb.Cube());
        var built = WebsiteStudio.Build(root, new WebsiteBrief("Produktseite mit Modell.", "Würfel", Kind: "shop", Technique: "mikro", ModelPath: glb, ModelMotion: "scroll"));
        Assert.True(File.Exists(Path.Combine(built.Directory, "assets", "modell.glb")));
        Assert.Contains("data-bewegung=\"scroll\"", File.ReadAllText(Path.Combine(built.Directory, "index.html")));
        Assert.Contains("id=\"buehne\"", File.ReadAllText(Path.Combine(built.Directory, "index.html")));

        var blend = Path.Combine(root, "x.blend");
        File.WriteAllText(blend, "kein blender");
        var missed = WebsiteStudio.Build(root, new WebsiteBrief("Modell aus Blender.", "Ohne Blender", ModelPath: blend, Technique: "drei"));
        Assert.Contains("Blender ist nicht installiert", missed.Content.ModelNote);
        Assert.False(File.Exists(Path.Combine(missed.Directory, "assets", "modell.glb")));

        var pack = Path.Combine(root, "paket");
        Directory.CreateDirectory(pack);
        File.WriteAllText(Path.Combine(root, "geheim.txt"), "nein");
        File.WriteAllText(Path.Combine(pack, "ok.bin"), "1234");
        File.WriteAllText(Path.Combine(pack, "modell.gltf"), "{\"buffers\":[{\"uri\":\"ok.bin\"},{\"uri\":\"../geheim.txt\"},{\"uri\":\"https://example.test/a.bin\"}]}");
        var assets = Path.Combine(root, "ziel");
        Directory.CreateDirectory(assets);
        WebsiteModels.CopyCompanions(Path.Combine(pack, "modell.gltf"), assets);
        Assert.True(File.Exists(Path.Combine(assets, "ok.bin")));
        Assert.False(File.Exists(Path.Combine(assets, "geheim.txt")));
        Assert.DoesNotContain(Directory.GetFiles(assets, "*", SearchOption.AllDirectories), f => f.Contains("example", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "Webseite: Unreal-Status ehrlich, Bildfolge und Pixel Streaming ohne Server")]
    public void Unreal_media_and_pixel()
    {
        Assert.Contains("nicht installiert", WebsiteUnreal.Describe([]));
        Assert.Contains("keinen Render", WebsiteUnreal.Describe([new GameStudio.Engine("5.4", "/kein/pfad")]));
        var root = Tmp();
        var frames = Path.Combine(root, "bilder");
        Directory.CreateDirectory(frames);
        File.WriteAllBytes(Path.Combine(frames, "b.png"), TinyPng());
        File.WriteAllBytes(Path.Combine(frames, "a.png"), TinyPng());
        var built = WebsiteStudio.Build(root, new WebsiteBrief("Scroll mit Bildern.", "Folge", Technique: "video", MediaPath: frames, PixelStreaming: true), new WebsiteBuildContext(Engines: []));
        Assert.Contains("nicht installiert", built.Content.UnrealNote);
        Assert.Equal(2, built.Content.Frames.Count);
        Assert.True(File.Exists(Path.Combine(built.Directory, "assets", "frames", "0001.png")));
        var html = File.ReadAllText(Path.Combine(built.Directory, "index.html"));
        Assert.Contains("bildfolge", html);
        Assert.Contains("nicht verbunden", html);
        var doc = File.ReadAllText(Path.Combine(built.Directory, "PIXEL-STREAMING.md"));
        Assert.Contains("startet diesen Server nicht", doc);
        Assert.DoesNotContain("http", doc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Webseite: Prüfung mit Note, Jetzt beheben, Paket nur nach Freigabe")]
    public void Quality_fix_and_pack()
    {
        var root = Tmp();
        var built = WebsiteStudio.Build(root, WebsiteStudio.Example("mikro"));
        var htmlPath = Path.Combine(built.Directory, "index.html");
        var html = File.ReadAllText(htmlPath).Replace("name=\"viewport\"", "name=\"weg\"", StringComparison.Ordinal);
        html = html.Replace("</body>", "<a href=\"fehlt.html\">Kaputt</a><script src=\"https://cdn.example/a.js\"></script></body>", StringComparison.Ordinal);
        File.WriteAllText(htmlPath, html);
        var css = File.ReadAllText(Path.Combine(built.Directory, "stil.css")).Replace("--text: #1d1d1f;", "--text: #d0d0d0;");
        File.WriteAllText(Path.Combine(built.Directory, "stil.css"), css);
        var before = WebsiteQuality.Check(built.Directory);
        Assert.True(before.Score < 90, before.Plain());
        Assert.Contains(before.Findings, f => f.Code == "viewport");
        Assert.Contains(before.Findings, f => f.Code == "missing");
        Assert.Contains(before.Findings, f => f.Code == "external-script");
        var after = WebsiteQuality.Fix(built.Directory);
        var fixedHtml = File.ReadAllText(htmlPath);
        Assert.Contains("name=\"viewport\"", fixedHtml);
        Assert.DoesNotContain("cdn.example", fixedHtml);
        Assert.Contains("fehlt.html", fixedHtml);
        Assert.True(after.Score > before.Score, after.Plain());
        Assert.Contains(after.Findings, f => f.Code == "missing");
        Assert.DoesNotContain(after.Findings, f => f.Code == "external-script");
        Assert.True(WebsiteColor.Contrast(CssVar(built.Directory, "text"), CssVar(built.Directory, "background")) >= 4.5);

        var audit = new AuditLog(Path.Combine(root, "audit.jsonl"));
        var gate = new ApprovalGate(audit);
        Assert.Throws<ApprovalException>(() => WebsiteStudio.Pack(gate, "fehlt", built.Directory));
        var pending = WebsiteStudio.RequestPack(gate, Principal.LocalOwner, built.Directory);
        Assert.Throws<ApprovalException>(() => WebsiteStudio.Pack(gate, pending.Id, built.Directory));
        Assert.False(Directory.GetFiles(root, "*.zip").Any());
        gate.Approve(pending.Id, Principal.LocalOwner);
        var zip = WebsiteStudio.Pack(gate, pending.Id, built.Directory);
        using var archive = ZipFile.OpenRead(zip);
        Assert.Contains(archive.Entries, e => e.FullName.EndsWith("index.html", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(archive.Entries, e => e.Name == "NICHT-HOCHGELADEN.txt");
    }

    static string CssVar(string dir, string name)
    {
        var css = File.ReadAllText(Path.Combine(dir, "stil.css"));
        var marker = "--" + name + ":";
        var i = css.IndexOf(marker, StringComparison.Ordinal);
        return css[(i + marker.Length)..].Trim().Split(';', 2)[0].Trim();
    }

    static byte[] TinyPng() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
}
