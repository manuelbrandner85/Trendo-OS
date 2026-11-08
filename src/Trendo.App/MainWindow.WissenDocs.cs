using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 8 – CORE-10: „Word, PDF, PowerPoint, Excel einlesen“ in „Wissen merken“ – markitdown (MIT) wandelt in Markdown um,
/// jede Datei wird eine Wissensseite (Quelle „Datei: name“, schon vorhandene werden übersprungen).</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Umwandlung nachbilden (Eingabe, Ausgabe.md) – schreibt die Ausgabe selbst.</summary>
    public static Func<string, string, Cli.Result>? DocsRunOverride { get; set; }
    static readonly KiLibraryModel DocsModel = new("dokumente-markitdown", KiKind.Wissen, KiRuntime.Dokumente, "Dokumente umwandeln (markitdown)", "", 0.15, 2, "MIT",
        "Word, PDF, PowerPoint, Excel ins Wissen übernehmen", "In „Wissen merken“ unter „Dateien einlesen“.", []);
    bool DocsReady => DocsRunOverride is not null || KiStore.RuntimeReady(KiRuntime.Dokumente);

    /// <summary>Dokumente umwandeln und als Wissensseiten ablegen (öffentlich für Tests). Rückgabe: (übernommen, übersprungen, Fehler).</summary>
    public async Task<(int Added, int Skipped, int Failed)> WissenImportDocs(string path)
    {
        path = path.Trim().Trim('"');
        var files = DocMarkdown.Files(path);
        if (files.Count == 0) { _wissenMsg = "Dort liegen keine Word-, PDF-, PowerPoint-, Excel- oder HTML-Dateien."; Show("ki-wissen"); return (0, 0, 0); }
        int a = 0, s = 0, f = 0; string? lastErr = null;
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-md-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(tmp);
        try
        {
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i]; _wissenMsg = $"Wandle um … {i + 1} von {files.Count}: {Path.GetFileName(file)}"; Show("ki-wissen");
                var outMd = Path.Combine(tmp, i + ".md");
                var r = DocsRunOverride is { } o ? o(file, outMd) : await KiPyRun(KiRuntime.Dokumente, DocMarkdown.Args(file, outMd), 300_000);
                if (!r.Ok || !File.Exists(outMd)) { f++; lastErr = $"{Path.GetFileName(file)}: {Short(r.Err.Length > 0 ? r.Err : r.Out)}"; continue; }
                try
                {
                    var res = DocMarkdown.Parse(r.Out); var text = (await File.ReadAllTextAsync(outMd)).Trim(); var source = "Datei: " + Path.GetFileName(file);
                    if (Wissen.All().Any(k => k.Source == source && k.Text == text)) { s++; continue; }
                    Wissen.Add(DocMarkdown.TitleFor(file, res), text, source, "dokument"); a++;
                }
                catch (Exception e) when (e is FormatException or System.Text.Json.JsonException or IOException) { f++; lastErr = $"{Path.GetFileName(file)}: {e.Message}"; }
            }
        }
        finally { try { Directory.Delete(tmp, true); } catch (IOException) { } }
        _core.Audit.Append(Principal.LocalOwner.Name, "wissen.dokumente", $"{a} neu, {s} schon da, {f} Fehler");
        _wissenMsg = $"{a} Dokument(e) ins Wissen übernommen" + (s > 0 ? $", {s} schon vorhanden" : "") + (f > 0 ? $", {f} nicht lesbar (zuletzt {lastErr})" : "") + ".";
        Show("ki-wissen"); return (a, s, f);
    }

    async Task WissenDocsSetup()
    {
        _wissenMsg = "Dokument-Umwandlung wird eingerichtet (einmalig, ca. 150 MB) …"; Show("ki-wissen");
        try { await KiPyInstall(DocsModel, KiPyApps.For(KiRuntime.Dokumente)!); _wissenMsg = "✓ Word, PDF, PowerPoint und Excel lassen sich jetzt einlesen."; }
        catch (Exception e) when (e is InvalidOperationException or IOException or HttpRequestException or OperationCanceledException) { _wissenMsg = "Einrichten ging nicht: " + e.Message; }
        Show("ki-wissen");
    }

    Control WissenDocsButton(TextBox folder) => DocsReady
        ? Act("wissen-docs", "Word, PDF, PowerPoint, Excel einlesen", () => _ = WissenImportDocs(folder.Text ?? ""))
        : Act("wissen-docs-setup", "Word/PDF einlesen einrichten (markitdown)", () => _ = WissenDocsSetup());
}
