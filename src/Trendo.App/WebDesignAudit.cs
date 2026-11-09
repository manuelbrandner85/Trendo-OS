using Trendo.Core;

namespace Trendo.App;

public sealed record WebAuditResult(IReadOnlyList<WebsiteFinding> Findings, IReadOnlyList<string> Images, string Note);

/// <summary>A6: Prüft die exportierte Seite so, wie Besucher sie bekommen: in drei Breiten in einem echten Browser, dazu Konsole, defekte Verweise, Barrierefreiheit (axe-core) und Qualität auf der fertig gezeichneten Seite.
/// Die Prüfung arbeitet in einem eigenen Ordner; die gespeicherte Seite bleibt unberührt.</summary>
public static class WebDesignAudit
{
    public const string WorkDir = ".trendo-pruefung";

    public static async Task<WebAuditResult> RunAsync(string json, string projectDir, string editorDir, string? node, string axeDir, Func<string, IReadOnlyList<string>, int, Cli.Result>? axeRun = null, CancellationToken ct = default)
    {
        var root = Path.Combine(projectDir, WorkDir); var site = Path.Combine(root, "seite");
        if (Directory.Exists(site)) Directory.Delete(site, true);
        Directory.CreateDirectory(root);
        var findings = new List<WebsiteFinding>(WebDesignCheck.CheckData(json, projectDir));
        var notes = new List<string>(); var images = new List<string>();
        try
        {
            var (dir, err) = WebDesign.ExportContent(json, projectDir, site, editorDir);
            if (dir is null) { findings.Add(new("Links", "Fehler", "Der Export ging nicht: " + err, "export")); return new(WebDesignCheck.Distinct(findings), images, "Der Export ging nicht."); }
            if (WebsitePreview.FindBrowser() is null)
            {
                notes.Add("Kein Chrome oder Edge: Bilder, Konsole und die Prüfung der gezeichneten Seite entfielen.");
                return new(WebDesignCheck.Distinct(findings), images, string.Join(" ", notes));
            }
            string? dom = null;
            foreach (var (w, h, name) in WebDesignCheck.Widths)
            {
                var errors = new List<string>(); var png = Path.Combine(root, name + ".png"); var domFile = Path.Combine(root, name + ".html");
                try
                {
                    var shot = await WebsitePreview.CaptureAsync(site, w, h, png, ct: ct, errors: errors, domPath: domFile);
                    if (shot.Ok) images.Add(png); else notes.Add(shot.Note);
                    if (File.Exists(domFile)) dom = domFile;
                }
                catch (Exception e) when (e is InvalidOperationException or TimeoutException or OperationCanceledException or IOException or System.Net.WebSockets.WebSocketException) { notes.Add($"Bild für {name} ging nicht: {e.Message}"); }
                foreach (var t in errors) findings.Add(new(t.StartsWith("Waagerechtes", StringComparison.Ordinal) ? "Handy" : "Konsole", "Fehler", t, t.StartsWith("Datei nicht gefunden", StringComparison.Ordinal) ? "missing" : "console"));
            }
            if (dom is null) { notes.Add("Die gezeichnete Seite ließ sich nicht auslesen."); return new(WebDesignCheck.Distinct(findings), images, string.Join(" ", notes)); }
            File.Copy(dom, Path.Combine(site, "index.html"), true);
            findings.AddRange(WebsiteQuality.Check(site).Findings.Where(f => f.Level != "Ok" && !WebDesignCheck.TemplateOnlyCodes.Contains(f.Code)));
            if (axeRun is not null || (node is not null && AxeCheck.Ready(axeDir)))
            {
                var (r, msg) = AxeCheck.Run(node ?? "node", axeDir, site, run: axeRun);
                if (r is not null) findings.AddRange(r.Findings); else notes.Add(msg);
            }
            else notes.Add("axe-core ist noch nicht eingerichtet; die genaue Barrierefreiheitsprüfung entfiel.");
            return new(WebDesignCheck.Distinct(findings), images, string.Join(" ", notes));
        }
        finally
        {
            foreach (var (_, _, name) in WebDesignCheck.Widths) try { File.Delete(Path.Combine(root, name + ".html")); } catch (IOException) { }
            try { if (Directory.Exists(site)) Directory.Delete(site, true); } catch (IOException) { /* Browser hält die Datei noch; der nächste Lauf räumt auf */ } catch (UnauthorizedAccessException) { }
        }
    }
}
