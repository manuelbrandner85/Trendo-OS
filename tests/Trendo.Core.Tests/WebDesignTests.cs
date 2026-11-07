using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Web Design Studio: eine Datenquelle je Projekt, sicheres Speichern mit Sicherung, Wiederherstellung, Export als offline lauffähige Seite. Je 5 positive und 5 negative Prüfungen.</summary>
public class WebDesignTests
{
    static string Dir() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-webdesign-" + Guid.NewGuid().ToString("N"))).FullName;
    const string Page = """{"root":{"props":{"title":"Bäckerei Muster","description":"Frisches Brot"}},"content":[{"type":"Kopfbereich","props":{"id":"k1","title":"Willkommen","href":"#sortiment"}},{"type":"Szene3D","props":{"id":"s1","form":"ring"}}],"zones":{}}""";

    /// <summary>Echtes Editor-Bündel aus dem Repo (src/Trendo.App/Assets/webdesign).</summary>
    static string EditorDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "src", "Trendo.App", "Assets", "webdesign"))) return Path.Combine(d.FullName, "src", "Trendo.App", "Assets", "webdesign");
        throw new DirectoryNotFoundException("Editor-Bündel nicht gefunden");
    }

    [Fact(DisplayName = "Web Design +1: Speichern und Laden ergeben dieselbe Seite")]
    public void Save_load_roundtrip()
    {
        var d = Dir(); try { Assert.Null(WebDesign.Save(d, Page)); Assert.Equal("Willkommen", JsonNode.Parse(WebDesign.Load(d))!["content"]![0]!["props"]!["title"]!.ToString()); }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Web Design +2: zweites Speichern hält die vorige Fassung als Sicherung")]
    public void Backup_kept()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, Page); WebDesign.Save(d, Page.Replace("Willkommen", "Hallo"));
            Assert.Contains("Willkommen", File.ReadAllText(Path.Combine(d, WebDesign.BackupFile))); Assert.Contains("Hallo", File.ReadAllText(Path.Combine(d, WebDesign.PageFile)));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Web Design +3: beschädigte Seitendatei → letzte gute Fassung wird wiederhergestellt")]
    public void Recover_from_backup()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, Page); WebDesign.Save(d, Page); File.WriteAllText(Path.Combine(d, WebDesign.PageFile), "{kaputt");
            Assert.Contains("Willkommen", WebDesign.Load(d));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Web Design +4: Export legt index.html, seite.js und das echte Bündel ab – offline, ohne Modul-Skripte")]
    public void Export_offline_site()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, Page);
            var (outDir, err) = WebDesign.Export(d, EditorDir()); Assert.Null(err);
            var html = File.ReadAllText(Path.Combine(outDir!, "index.html"));
            Assert.Contains("<title>Bäckerei Muster</title>", html); Assert.Contains("<script src=\"seite.js\"></script>", html); Assert.DoesNotContain("type=\"module\"", html); Assert.DoesNotContain("http", html);
            Assert.StartsWith("window.TRENDO_SEITE = {", File.ReadAllText(Path.Combine(outDir!, "seite.js")));
            Assert.True(new FileInfo(Path.Combine(outDir!, "assets", "editor.js")).Length > 500_000); Assert.True(File.Exists(Path.Combine(outDir!, "assets", "style.css")));
            Assert.Contains("@puckeditor/core", File.ReadAllText(Path.Combine(outDir!, "assets", "LIZENZEN.txt"))); // Lizenzvermerk reist mit der Seite
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Web Design +5: Nachrichten des Editors werden erkannt (auch als JSON-Text), Leerseite ist gültig")]
    public void Messages_and_empty()
    {
        Assert.Equal("bereit", WebDesign.ParseMessage("""{"art":"bereit"}""")!.Kind);
        var m = WebDesign.ParseMessage("\"{\\\"art\\\":\\\"speichern\\\",\\\"daten\\\":{\\\"root\\\":{}}}\""); Assert.Equal("speichern", m!.Kind); Assert.Contains("root", m.Data);
        Assert.Null(WebDesign.Validate(WebDesign.Empty("Test")).Error);
    }

    [Fact(DisplayName = "Web Design −1: kein JSON, fehlendes root/content oder Baustein ohne Typ wird abgelehnt, nichts geschrieben")]
    public void Reject_invalid()
    {
        var d = Dir(); try
        {
            Assert.NotNull(WebDesign.Save(d, "kein json")); Assert.NotNull(WebDesign.Save(d, """{"content":[]}""")); Assert.NotNull(WebDesign.Save(d, """{"root":{},"content":[{"props":{}}]}"""));
            Assert.False(File.Exists(Path.Combine(d, WebDesign.PageFile)));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Web Design −2: gefährliche Links (javascript:, data:, vbscript:) werden entschärft")]
    public void Strip_dangerous_links()
    {
        var (ok, _) = WebDesign.Validate(Page.Replace("#sortiment", "JavaScript:alert(1)").Replace("\"form\":\"ring\"", "\"src\":\"data:text/html,x\""));
        Assert.DoesNotContain("alert", ok!, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("data:text", ok!);
    }

    [Fact(DisplayName = "Web Design −3: Text mit </script> kann das Skript der fertigen Seite nicht beenden")]
    public void No_script_breakout()
    {
        var d = Dir(); try
        {
            WebDesign.Save(d, Page.Replace("Willkommen", "</script><script>alert(1)</script>"));
            var (outDir, _) = WebDesign.Export(d, EditorDir());
            Assert.DoesNotContain("</script>", File.ReadAllText(Path.Combine(outDir!, "seite.js")));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Web Design −4: Export ohne Editor-Bündel meldet den Fehler statt eine kaputte Seite zu schreiben")]
    public void Export_without_bundle()
    {
        var d = Dir(); try { WebDesign.Save(d, Page); var (outDir, err) = WebDesign.Export(d, Path.Combine(d, "fehlt")); Assert.Null(outDir); Assert.Contains("Editor-Bündel fehlt", err); }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Web Design −5: zu große Seite und unbekannte Nachrichten werden abgewiesen; ohne Datei gibt es eine leere Seite")]
    public void Limits_and_defaults()
    {
        Assert.Contains("zu groß", WebDesign.Validate("{\"root\":{},\"content\":[],\"x\":\"" + new string('a', WebDesign.MaxBytes) + "\"}").Error);
        Assert.Null(WebDesign.ParseMessage("kaputt")); Assert.Null(WebDesign.ParseMessage("""{"x":1}"""));
        var d = Dir(); try { Assert.Empty(JsonNode.Parse(WebDesign.Load(d))!["content"]!.AsArray()); } finally { Directory.Delete(d, true); }
    }
}
