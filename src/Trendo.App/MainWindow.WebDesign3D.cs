using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Bibliothek;

namespace Trendo.App;

/// <summary>Z3 im Studio „Webseite bauen“: 3D-Modelle ins Projekt holen (auch .blend, .fbx, .obj … über Blender nach GLB), Dateien an den Editor geben, Standbild aus der Ansicht speichern.</summary>
public partial class MainWindow
{
    bool _wd3dBusy;
    /// <summary>Für Tests: Umwandlung nach GLB ersetzen (sonst Blender über ModelConverter).</summary>
    public static Func<string, string, Task>? WebDesignGlbConvertOverride { get; set; }

    /// <summary>Modell aus einem anderen Format über Blender nach GLB wandeln, prüfen und mit Herkunft und Lizenz in die Medien legen.</summary>
    public async Task WebDesignModelImport(string path, string titel, string herkunft, string lizenz, string urheber = "", string quelle = "")
    {
        if (_wd3dBusy) return;
        path = path.Trim().Trim('"');
        if (WebDesignImport.LocalPathError(path) is { } lerr || !File.Exists(path)) { _wdNote = WebDesignImport.LocalPathError(path) ?? "Diese Datei gibt es nicht."; Show("dev"); return; }
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!ModelConverter.Extensions.Contains(ext)) { _wdNote = "Dieses 3D-Format kennt Trendo nicht. Möglich: " + string.Join(", ", ModelConverter.Extensions); Show("dev"); return; }
        _wd3dBusy = true;
        var temp = Path.Combine(Path.GetTempPath(), "trendo-glb-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try
        {
            var glb = path;
            if (ext != ".glb")
            {
                _wdNote = "Blender wandelt das Modell in GLB um …"; Show("dev");
                glb = Path.Combine(temp, Path.GetFileNameWithoutExtension(path) + ".glb");
                if (WebDesignGlbConvertOverride is { } o) await o(path, glb); else await ModelConverter.ToGlb(path, glb);
            }
            if (WebDesignModel.CheckGlb(glb) is { } gerr) { _wdNote = "Nicht übernommen: " + gerr; return; }
            var (a, err) = WebDesignAssets.Import(WebDesignDir, glb, "modell", titel.Trim().Length > 0 ? titel : Path.GetFileNameWithoutExtension(path), herkunft.Trim().Length > 0 ? herkunft : (ext == ".blend" ? "eigenes Blender-Modell" : "eigene Datei"), lizenz, urheber, quelle);
            _wdNote = err ?? $"Modell „{a!.Titel}“ liegt unter {a.Datei}" + (ext != ".glb" ? " (aus Blender umgewandelt)." : ".") + (string.IsNullOrWhiteSpace(lizenz) ? " Bitte noch die Lizenz eintragen." : "");
            _core.Audit.Append(Principal.LocalOwner.Name, "web.visuell.modell", err ?? a!.Datei);
            if (err is null) WebDesignReload(WebDesignCurrent());
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or TimeoutException or TaskCanceledException) { _wdNote = "Umwandlung abgebrochen: " + e.Message; }
        finally { _wd3dBusy = false; try { Directory.Delete(temp, true); } catch (IOException) { } Show("dev"); }
    }

    /// <summary>Dateien der 3D-Bausteine an den Editor geben (der Editor läuft aus dem Programmordner und darf Projektdateien nicht selbst lesen).</summary>
    void WebDesignPush3DFiles(string json)
    {
        if (_wdView is null || JsonNode.Parse(json) is not JsonObject doc) return;
        foreach (var rel in WebDesignModel.Blocks(doc).SelectMany(WebDesignModel.Files).Distinct())
        {
            var file = Path.Combine(WebDesignDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file) || new FileInfo(file).Length > WebDesignModel.MaxBytes) continue;
            _wdView.InvokeScript(WebDesignModel.FileScript(rel, File.ReadAllBytes(file)));
        }
    }

    /// <summary>Standbild aus der 3D-Ansicht (PNG aus dem Editor) als Medium speichern und im Baustein eintragen – für Geräte ohne 3D.</summary>
    public void WebDesignStill(string? data)
    {
        try
        {
            var o = JsonNode.Parse(data ?? "{}") as JsonObject;
            var id = o?["id"]?.ToString() ?? ""; var url = o?["bild"]?.ToString() ?? "";
            const string prefix = "data:image/png;base64,";
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Za-z0-9_-]{1,80}$") || !url.StartsWith(prefix, StringComparison.Ordinal) || url.Length > 30_000_000) { _wdNote = "Das Standbild ließ sich nicht übernehmen."; return; }
            var bytes = Convert.FromBase64String(url[prefix.Length..]);
            using (var codec = SkiaSharp.SKCodec.Create(new MemoryStream(bytes)))
                if (codec is null || codec.EncodedFormat != SkiaSharp.SKEncodedImageFormat.Png || codec.Info.Width > 4096 || codec.Info.Height > 4096) { _wdNote = "Das Standbild hat ein unerwartetes Format."; return; }
            var temp = Path.Combine(Path.GetTempPath(), "trendo-standbild-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
            var file = Path.Combine(temp, "standbild-" + id.ToLowerInvariant().Replace('_', '-') + ".png"); File.WriteAllBytes(file, bytes);
            var (a, err) = WebDesignAssets.Import(WebDesignDir, file, "bild", "Standbild 3D " + id, "Standbild aus dem 3D-Baustein", "eigenes Werk");
            try { Directory.Delete(temp, true); } catch (IOException) { }
            if (a is null) { _wdNote = err ?? "Das Standbild ließ sich nicht speichern."; return; }
            WebDesignHumanAction(new JsonObject { ["aktionen"] = new JsonArray(new JsonObject { ["aktion"] = "element_aendern", ["id"] = id, ["props"] = new JsonObject { ["standbild"] = a.Datei } }) }.ToJsonString());
            _wdNote = $"Standbild gespeichert ({a.Datei}). Geräte ohne 3D zeigen es statt der Szene.";
        }
        catch (FormatException) { _wdNote = "Das Standbild ließ sich nicht lesen."; }
        catch (System.Text.Json.JsonException) { _wdNote = "Das Standbild ließ sich nicht lesen."; }
        finally { Avalonia.Threading.Dispatcher.UIThread.Post(() => Show("dev")); }
    }
}
