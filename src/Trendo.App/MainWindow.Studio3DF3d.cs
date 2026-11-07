using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 1 – CORE-05: Vorschaubilder jeder 3D-Datei im 3D-Ordner mit f3d (BSD-3). Einrichtung nur nach Zustimmung (Abhängigkeitskatalog, Prüfsumme).</summary>
public partial class MainWindow
{
    string _f3dMsg = ""; string? _f3dLast;
    /// <summary>Für Tests: f3d-Lauf nachbilden (Programm, Argumente, Zeitlimit).</summary>
    public static Func<string, IReadOnlyList<string>, int, Cli.Result>? F3dRunOverride { get; set; }
    /// <summary>Für Tests: f3d-Programm vorgeben.</summary>
    public static string? F3dExeOverride { get; set; }
    public string F3dMessage => _f3dMsg;
    string? F3dExe => F3dExeOverride is { } o ? (o.Length == 0 ? null : o) : _deps.ResolvePath(DependencyCatalog.Get("f3d")); // Test: "" = nicht eingerichtet

    /// <summary>Vorschaubild einer 3D-Datei in <c>3D/vorschau/&lt;name&gt;.png</c> (öffentlich für Tests).</summary>
    public void F3dPreview(string file)
    {
        var outFile = Path.Combine(Folder3D, "vorschau", Path.GetFileNameWithoutExtension(file) + ".png");
        var (ok, msg) = Model3DPreview.Render(F3dExe ?? "", file, outFile, 768, run: F3dRunOverride);
        _f3dMsg = ok ? $"Vorschaubild: {Path.GetFileName(outFile)}" : msg; _f3dLast = ok ? outFile : _f3dLast;
        _core.Audit.Append(Principal.LocalOwner.Name, "3d.f3d.vorschau", ok ? Path.GetFileName(file) : "fehler");
        Show("3d");
    }

    async Task SetupF3d()
    {
        var comp = DependencyCatalog.Get("f3d");
        var req = _core.Approvals.Request(Principal.LocalOwner, "deps.install", "f3d (Vorschaubilder für 3D-Dateien) einrichten", comp.Id);
        _core.Approvals.Approve(req.Id, Principal.LocalOwner);
        if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner);
        _f3dMsg = "f3d wird eingerichtet (offizieller Download, ~66 MB, Prüfsumme wird geprüft) …"; Show("3d");
        var ok = !_opt.Isolated && await Task.Run(() => _deps.InstallAsync(Principal.LocalOwner, comp));
        _f3dMsg = ok ? "f3d ist eingerichtet." : _opt.Isolated ? "Testmodus: nichts installiert." : "f3d konnte nicht eingerichtet werden – unter Zusatzprogramme erneut versuchen.";
        Show("3d");
    }

    Control F3dCard()
    {
        var sp = new StackPanel { Spacing = 6, Name = "3d-f3d" };
        sp.Children.Add(H("Vorschaubilder (f3d)", 13));
        var files = Directory.Exists(Folder3D) ? Directory.EnumerateFiles(Folder3D, "*.*", SearchOption.AllDirectories).Where(Model3DPreview.Supports).OrderByDescending(File.GetLastWriteTimeUtc).Take(8).ToList() : [];
        if (F3dExe is null)
        {
            sp.Children.Add(P("Erzeugt ein sauberes Bild jeder 3D-Datei (glTF, OBJ, STL, FBX, USD …) – für Übersicht, Galerie und Webseite. Kostenlos, BSD-3.", null, 11));
            sp.Children.Add(Act("3d-f3d-setup", "f3d einrichten", () => _ = SetupF3d()));
        }
        else if (files.Count == 0) sp.Children.Add(P("Noch keine 3D-Datei im 3D-Ordner – zuerst exportieren (GLB/OBJ/STL).", null, 11));
        else
        {
            var i = 0;
            foreach (var f in files) { var file = f; sp.Children.Add(Act($"3d-f3d-{i++}", "🖼 " + Path.GetFileName(file), () => F3dPreview(file))); }
        }
        if (_f3dMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "3d-f3d-msg", Text = _f3dMsg, FontSize = 11, Foreground = Muted, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        if (_f3dLast is { } png && File.Exists(png))
            try { using var fs = File.OpenRead(png); sp.Children.Add(new Image { Name = "3d-f3d-image", Source = new Bitmap(fs), MaxHeight = 220 }); }
            catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NullReferenceException) { sp.Children.Add(P("Bild nicht lesbar.", null, 11)); }
        return sp;
    }
}
