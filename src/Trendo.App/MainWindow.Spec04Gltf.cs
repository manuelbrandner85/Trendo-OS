using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 5 – SPEC-04: Khronos glTF-Validator (offizielle Prüfung) und glTF-Transform (Packen für Web &amp; Brille) im VR-Studio.
/// Einrichten nur nach Klick: Validator über den Abhängigkeitskatalog (Zip mit Prüfsumme), glTF-Transform per npm (feste Fassung, ohne Installationsskripte).</summary>
public partial class MainWindow
{
    string _gltfMsg = ""; GltfTools.Report? _gltfReport;
    public string GltfMessage => _gltfMsg;
    public GltfTools.Report? GltfLastReport => _gltfReport;
    /// <summary>Für Tests: Programmläufe nachbilden, Programme vorgeben ("" = nicht eingerichtet).</summary>
    public static Func<string, IReadOnlyList<string>, int, Cli.Result>? GltfRunOverride { get; set; }
    public static string? GltfValidatorOverride { get; set; }
    public static (string Node, string CliJs)? GltfTransformOverride { get; set; }
    string GltfTransformDir => Path.Combine(_deps.ToolsRoot, "gltf-transform");
    string? GltfValidator => GltfValidatorOverride is { } o ? (o.Length == 0 ? null : o) : _deps.ResolvePath(DependencyCatalog.Get("gltf-validator"));
    (string Node, string CliJs)? GltfTransform => GltfTransformOverride ?? (Cli.Find("node") is { } n && File.Exists(GltfTools.CliJs(GltfTransformDir)) ? (n, GltfTools.CliJs(GltfTransformDir)) : null);

    /// <summary>Offizielle Khronos-Prüfung des geöffneten Modells (öffentlich für Tests).</summary>
    public void XrValidate()
    {
        var (rep, msg) = GltfTools.Validate(GltfValidator ?? "", XrFile, run: GltfRunOverride);
        _gltfReport = rep; _gltfMsg = msg;
        _core.Audit.Append(Principal.LocalOwner.Name, "xr.gltf.pruefen", rep is null ? "fehler" : $"{rep.Errors} Fehler");
        Show("xr");
    }

    /// <summary>Für Web &amp; Brille packen: neue Datei „…-web.glb“, das Original bleibt; danach gleich wieder geprüft, wenn der Validator da ist.</summary>
    public void XrPackWeb()
    {
        if (GltfTransform is not { } t) { _gltfMsg = "glTF-Transform ist noch nicht eingerichtet."; Show("xr"); return; }
        var output = Path.Combine(Path.GetDirectoryName(XrFile) ?? WorkFolder("Modell-Prüfer"), Path.GetFileNameWithoutExtension(XrFile) + "-web.glb");
        var (ok, msg) = GltfTools.Pack(t.Node, t.CliJs, XrFile, output, run: GltfRunOverride);
        _core.Audit.Append(Principal.LocalOwner.Name, "xr.gltf.packen", ok ? Path.GetFileName(output) : "fehler");
        if (!ok) { _gltfMsg = "" + msg; Show("xr"); return; }
        var original = Path.GetFileName(XrFile); XrFile = output;
        var check = GltfValidator is { } v ? GltfTools.Validate(v, output, run: GltfRunOverride) : (null, "");
        _gltfReport = check.Report;
        _gltfMsg = $"Gepackt: {Path.GetFileName(output)} ({msg}) – {original} bleibt unverändert. Für Blender und Unreal das Original verwenden." + (check.Report is { } r ? " " + r.Text : "");
        Show("xr");
    }

    async Task XrGltfSetup(string which)
    {
        if (which == "validator")
        {
            var comp = DependencyCatalog.Get("gltf-validator");
            var req = _core.Approvals.Request(Principal.LocalOwner, "deps.install", "glTF-Validator (Khronos) einrichten", comp.Id); _core.Approvals.Approve(req.Id, Principal.LocalOwner);
            if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner);
            _gltfMsg = "glTF-Validator wird eingerichtet (offizieller Download, ~2 MB, Prüfsumme wird geprüft) …"; Show("xr");
            var ok = !_opt.Isolated && await Task.Run(() => _deps.InstallAsync(Principal.LocalOwner, comp));
            _gltfMsg = ok ? "glTF-Validator ist eingerichtet." : _opt.Isolated ? "Testmodus: nichts installiert." : "glTF-Validator konnte nicht eingerichtet werden.";
        }
        else
        {
            var npm = Cli.Find("npm") ?? Cli.Find("npm.cmd");
            if (_opt.Isolated) _gltfMsg = "Testmodus: nichts installiert.";
            else if (npm is null) _gltfMsg = "Node.js mit npm fehlt – zuerst unter Programme „Node.js“ einrichten.";
            else
            {
                _gltfMsg = "glTF-Transform wird aus der offiziellen npm-Quelle geladen (feste Fassung, ohne Installationsskripte) …"; Show("xr");
                Directory.CreateDirectory(GltfTransformDir);
                var r = await Task.Run(() => Cli.Run(npm, GltfTools.InstallArgs(GltfTransformDir), GltfTransformDir, 600_000));
                var ok = r.Ok && File.Exists(GltfTools.CliJs(GltfTransformDir));
                _core.Audit.Append(Principal.LocalOwner.Name, "deps.npm", "@gltf-transform/cli@" + GltfTools.TransformVersion + (ok ? "" : " (fehlgeschlagen)"));
                _gltfMsg = ok ? "glTF-Transform ist eingerichtet." : "Einrichten ging nicht: " + Short(r.Err.Length > 0 ? r.Err : r.Out);
            }
        }
        Show("xr");
    }

    Control XrGltfCard()
    {
        var sp = new StackPanel { Name = "xr-gltf", Spacing = 4 };
        sp.Children.Add(H("Prüfen & für Web packen", 12));
        if (!GltfTools.IsGltf(XrFile)) { sp.Children.Add(P("Nur für .glb und .gltf.", Muted, 11)); return sp; }
        var row = new WrapPanel();
        row.Children.Add(GltfValidator is null ? Act("xr-gltf-validator-setup", "Khronos-Prüfer einrichten", () => _ = XrGltfSetup("validator")) : Act("xr-gltf-validate", "Offiziell prüfen", XrValidate));
        row.Children.Add(new Border { Width = 4 });
        row.Children.Add(GltfTransform is null ? Act("xr-gltf-transform-setup", "glTF-Transform einrichten", () => _ = XrGltfSetup("transform")) : Act("xr-gltf-pack", "Für Web & Brille packen", XrPackWeb, true));
        sp.Children.Add(row);
        sp.Children.Add(P("Packen: kleinere Datei (meshopt, WebP) für Web-Seiten und Brillen. Blender und Unreal lesen das nicht – dafür das Original nehmen.", Muted, 10.5));
        if (_gltfMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "xr-gltf-msg", Text = _gltfMsg, FontSize = 11, Foreground = Muted, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        return sp;
    }
}
