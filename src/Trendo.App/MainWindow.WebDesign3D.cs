using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Bibliothek;

namespace Trendo.App;

/// <summary>Z3 im Studio „Webseite bauen“: 3D-Modelle ins Projekt holen (auch .blend, .fbx, .obj … über Blender nach GLB), Dateien an den Editor geben, Standbild aus der Ansicht speichern.</summary>
public partial class MainWindow
{
    bool _wd3dBusy;
    /// <summary>Schon an den Editor gegebene 3D-Dateien (Pfad, Größe, Zeit): jede geht nur einmal hinein, nicht bei jeder Änderung neu.</summary>
    readonly HashSet<string> _wd3dSent = [];
    /// <summary>Für Tests: Umwandlung nach GLB ersetzen (sonst Blender über ModelConverter).</summary>
    public static Func<string, string, Task>? WebDesignGlbConvertOverride { get; set; }

    /// <summary>Modell aus einem anderen Format über Blender nach GLB wandeln, prüfen und mit Herkunft und Lizenz in die Medien legen.</summary>
    public async Task WebDesignModelImport(string path, string titel, string herkunft, string lizenz, string urheber = "", string quelle = "")
    {
        if (_wd3dBusy) { _wdNote = "Es wird gerade ein Modell umgewandelt. Bitte warten, bis es fertig ist."; Show("dev"); return; }
        path = path.Trim().Trim('"');
        if (WebDesignImport.LocalPathError(path) is { } lerr || !File.Exists(path)) { _wdNote = WebDesignImport.LocalPathError(path) ?? "Diese Datei gibt es nicht."; Show("dev"); return; }
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!ModelConverter.Extensions.Contains(ext)) { _wdNote = "Dieses 3D-Format kennt Trendo nicht. Möglich: " + string.Join(", ", ModelConverter.Extensions); Show("dev"); return; }
        if (new FileInfo(path).Length > ModelConverter.MaxInputBytes) { _wdNote = "Die Datei ist zu groß (über 1 GB). Bitte in Blender verkleinern."; Show("dev"); return; }
        _wd3dBusy = true;
        var temp = Path.Combine(Path.GetTempPath(), "trendo-glb-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try
        {
            var glb = path;
            if (ext != ".glb")
            {
                _wdNote = "Blender wandelt das Modell in GLB um …"; Show("dev");
                glb = Path.Combine(temp, Path.GetFileNameWithoutExtension(path) + ".glb");
                if (WebDesignGlbConvertOverride is { } o) await o(path, glb); else await ModelConverter.ToGlb(path, glb, onlyLocalTextures: true);
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
    /// <summary>Für Tests: Skripte, die sonst an den Editor (WebView) gingen; im Testmodus gibt es kein WebView.</summary>
    public static Action<string>? WebDesignScriptSink { get; set; }

    void WebDesignPush3DFiles(string json)
    {
        if (_wdView is null && WebDesignScriptSink is null) return;
        JsonNode? parsed;
        try { parsed = JsonNode.Parse(json); } catch (System.Text.Json.JsonException) { return; }
        if (parsed is not JsonObject doc) return;
        foreach (var rel in WebDesignModel.Blocks(doc).SelectMany(WebDesignModel.Files).Distinct())
        {
            var file = Path.Combine(WebDesignDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!WebDesignCheck.IsLocalFile(WebDesignDir, rel) || !File.Exists(file) || new FileInfo(file).Length > WebDesignModel.MaxBytes) continue; // keine Verknüpfung aus dem Projekt heraus
            var fi = new FileInfo(file);
            if (!_wd3dSent.Add($"{rel}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}")) continue;
            var script = WebDesignModel.FileScript(rel, File.ReadAllBytes(file));
            if (_wdView is not null) _wdView.InvokeScript(script); else WebDesignScriptSink?.Invoke(script);
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
            _wdNote = "";
            WebDesignHumanAction(new JsonObject { ["aktionen"] = new JsonArray(new JsonObject { ["aktion"] = "element_aendern", ["id"] = id, ["props"] = new JsonObject { ["standbild"] = a.Datei } }) }.ToJsonString());
            _wdNote = _wdNote.Length > 0 ? $"Standbild gespeichert ({a.Datei}), aber nicht eingetragen: {_wdNote}" : $"Standbild gespeichert ({a.Datei}). Geräte ohne 3D zeigen es statt der Szene.";
        }
        catch (FormatException) { _wdNote = "Das Standbild ließ sich nicht lesen."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _wdNote = "Das Standbild ließ sich nicht speichern (kein Schreibrecht im Projekt)."; }
        catch (System.Text.Json.JsonException) { _wdNote = "Das Standbild ließ sich nicht lesen."; }
        finally { Avalonia.Threading.Dispatcher.UIThread.Post(() => Show("dev")); }
    }
}
