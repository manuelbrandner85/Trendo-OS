using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 1 – CORE-05: 3D-Dateien mit assimp (BSD-3) nach GLB umwandeln – in einem eigenen Trendo-Prozess (<c>--assimp-convert</c>),
/// damit eine kaputte oder bösartige Datei nur diesen Prozess trifft. Einrichtung nur nach Zustimmung (Abhängigkeitskatalog, Prüfsumme).</summary>
public partial class MainWindow
{
    string _assimpMsg = ""; string? _assimpLast;
    /// <summary>Für Tests: Umwandlungs-Prozess nachbilden und DLL vorgeben ("" = nicht eingerichtet).</summary>
    public static Func<string, IReadOnlyList<string>, int, Cli.Result>? AssimpRunOverride { get; set; }
    public static string? AssimpDllOverride { get; set; }
    public string AssimpMessage => _assimpMsg;
    string? AssimpDll => AssimpDllOverride is { } o ? (o.Length == 0 ? null : o) : _deps.ResolvePath(DependencyCatalog.Get("assimp"));

    /// <summary>3D-Datei → GLB im Ordner „3D/umgewandelt“ (öffentlich für Tests).</summary>
    public void AssimpToGlb(string file)
    {
        var outFile = Path.Combine(Folder3D, "umgewandelt", Path.GetFileNameWithoutExtension(file) + ".glb");
        var exe = Environment.ProcessPath ?? "TrendoOS";
        var (ok, msg) = AssimpConvert.Convert(exe, AssimpDll ?? "", file, outFile, run: AssimpRunOverride ?? (_opt.Isolated ? (_, _, _) => new Cli.Result(-1, "", "Testmodus: assimp wird nicht gestartet.") : null));
        _assimpMsg = ok ? $"✓ Umgewandelt: {Path.GetFileName(outFile)} (im Ordner 3D/umgewandelt)" : "⚠ " + msg; _assimpLast = ok ? outFile : _assimpLast;
        _core.Audit.Append(Principal.LocalOwner.Name, "3d.assimp.umwandeln", ok ? Path.GetFileName(file) : "fehler");
        Show("3d");
    }

    async Task SetupAssimp()
    {
        var comp = DependencyCatalog.Get("assimp");
        var req = _core.Approvals.Request(Principal.LocalOwner, "deps.install", "assimp (3D-Dateien umwandeln) einrichten", comp.Id);
        _core.Approvals.Approve(req.Id, Principal.LocalOwner);
        if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner);
        _assimpMsg = "assimp wird eingerichtet (offizieller Download, ~18 MB, Prüfsumme wird geprüft) …"; Show("3d");
        var ok = !_opt.Isolated && await Task.Run(() => _deps.InstallAsync(Principal.LocalOwner, comp));
        _assimpMsg = ok ? "assimp ist eingerichtet." : _opt.Isolated ? "Testmodus: nichts installiert." : "assimp konnte nicht eingerichtet werden – unter Zusatzprogramme erneut versuchen.";
        Show("3d");
    }

    Control AssimpCard()
    {
        var sp = new StackPanel { Spacing = 6, Name = "3d-assimp" };
        sp.Children.Add(H("In GLB umwandeln (assimp)", 13));
        if (AssimpDll is null)
        {
            sp.Children.Add(P("Macht aus FBX, DAE, 3DS, OBJ, PLY, STL … eine GLB-Datei für Web, Spiele und AR. Kostenlos, BSD-3, ~18 MB. Läuft getrennt von Trendo.", null, 11));
            sp.Children.Add(Act("3d-assimp-setup", "assimp einrichten", () => _ = SetupAssimp()));
        }
        else
        {
            var files = Directory.Exists(Folder3D) ? Directory.EnumerateFiles(Folder3D, "*.*", SearchOption.AllDirectories)
                .Where(f => AssimpConvert.Inputs.Contains(Path.GetExtension(f).ToLowerInvariant()) && !f.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) && !f.Contains(Path.DirectorySeparatorChar + "umgewandelt" + Path.DirectorySeparatorChar))
                .OrderByDescending(File.GetLastWriteTimeUtc).Take(8).ToList() : [];
            if (files.Count == 0) sp.Children.Add(P("Keine umwandelbare 3D-Datei im 3D-Ordner (FBX, DAE, 3DS, OBJ, PLY, STL …).", null, 11));
            var i = 0;
            foreach (var f in files) { var file = f; sp.Children.Add(Act($"3d-assimp-{i++}", "GLB: " + Path.GetFileName(file), () => AssimpToGlb(file))); }
        }
        if (_assimpMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "3d-assimp-msg", Text = _assimpMsg, FontSize = 11, Foreground = Muted, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        return sp;
    }
}
