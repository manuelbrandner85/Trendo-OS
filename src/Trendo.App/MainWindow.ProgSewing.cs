using Avalonia.Controls;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 7 – SPEC-08: echte Schnittmuster nach Körpermaßen mit FreeSewing 4 (MIT) im Programm „Kleidung &amp; Produkte“.
/// Ergebnis: SVG im Maßstab 1:1 (Millimeter) zum Ausdrucken oder Plotten; fehlende Maße kommen aus der nächstliegenden Standardgröße – Trendo nennt sie.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Werkzeugordner und Lauf nachbilden.</summary>
    public static string? SewingDirOverride { get; set; }
    public static Func<string, IReadOnlyList<string>, int, Cli.Result>? SewingRunOverride { get; set; }
    string SewingDir => SewingDirOverride ?? Path.Combine(_deps.ToolsRoot, "freesewing");
    bool SewingReady => SewingDirOverride is not null || Sewing.Ready(SewingDir);
    string _sewMsg = ""; public string SewingMessage => _sewMsg;
    public string? SewingLastSvg { get; private set; }
    static readonly string[] SewingExtras = ["neck", "biceps", "inseam", "shoulderToShoulder", "hpsToWaistBack"];

    /// <summary>Schnittmuster erzeugen (öffentlich für Tests). Rückgabe: SVG-Pfad oder null.</summary>
    public string? SewingDraft(ProgramDocument d, string design, int seamMm = 10)
    {
        var node = SewingRunOverride is not null ? "node" : Cli.Find("node");
        if (node is null) { _sewMsg = "⚠ Node.js fehlt – zuerst unter Programme „Node.js“ einrichten."; Show("mode-programm"); return null; }
        var cm = new Dictionary<string, double> { ["chest"] = d.Number("chest"), ["waist"] = d.Number("waist"), ["hip"] = d.Number("hip") };
        foreach (var k in SewingExtras) if (d.Number("fs-" + k) > 0) cm[k] = d.Number("fs-" + k);
        var label = Sewing.Designs.FirstOrDefault(x => x.Id == design).Label ?? design;
        var svg = Path.Combine(WorkFolder("Mode"), "Schnittmuster", $"{DevStudio.SafeName(d.Text("name", "Entwurf"))}-{design}.svg");
        var (r, msg) = Sewing.Draft(node, SewingDir, design, cm, !d.Flag("women"), seamMm, svg, run: SewingRunOverride);
        _sewMsg = r is null ? "⚠ " + msg : $"✓ {label}: {msg} Gespeichert als {Path.GetFileName(svg)} – im Maßstab 1:1 drucken (100 %, nicht „an Seite anpassen“).";
        SewingLastSvg = r is null ? null : svg;
        _core.Audit.Append(Principal.LocalOwner.Name, "mode.schnitt", r is null ? "fehler" : $"{design} {r.Size}");
        Show("mode-programm"); return SewingLastSvg;
    }

    async Task SewingSetup()
    {
        var npm = Cli.Find("npm") ?? Cli.Find("npm.cmd");
        if (_opt.Isolated) _sewMsg = "Testmodus: nichts installiert.";
        else if (npm is null) _sewMsg = "⚠ Node.js mit npm fehlt – zuerst unter Programme „Node.js“ einrichten.";
        else
        {
            _sewMsg = "FreeSewing wird aus der offiziellen npm-Quelle geladen (feste Fassung, ohne Installationsskripte) …"; Show("mode-programm");
            Directory.CreateDirectory(SewingDir);
            var r = await Task.Run(() => Cli.Run(npm, Sewing.InstallArgs(SewingDir), SewingDir, 600_000));
            var ok = r.Ok && Sewing.Ready(SewingDir);
            _core.Audit.Append(Principal.LocalOwner.Name, "deps.npm", "@freesewing@" + Sewing.Version + (ok ? "" : " (fehlgeschlagen)"));
            _sewMsg = ok ? "✓ FreeSewing ist eingerichtet." : "⚠ Einrichten ging nicht: " + (r.Err.Length > 0 ? r.Err : r.Out).Trim().Split('\n')[0];
        }
        Show("mode-programm");
    }

    Control SewingPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "mode-sew", Spacing = 4 };
        sp.Children.Add(H("Schnittmuster nach Maß (FreeSewing)", 12));
        if (!SewingReady)
        {
            sp.Children.Add(P("Echte Schnittmuster nach deinen Körpermaßen – T-Shirt, Hemd, Hose, Rock und mehr. Kostenlos, MIT, ca. 3 MB.", Muted, 11));
            sp.Children.Add(Act("mode-sew-setup", "FreeSewing einrichten", () => _ = SewingSetup()));
        }
        else
        {
            var design = new ComboBox { Name = "mode-sew-design", ItemsSource = Sewing.Designs.Select(x => x.Label).ToList(), SelectedIndex = 0, Width = 230 };
            var seam = new ComboBox { Name = "mode-sew-seam", ItemsSource = new[] { "ohne Nahtzugabe", "Nahtzugabe 1 cm", "Nahtzugabe 1,5 cm" }, SelectedIndex = 1, Width = 160 };
            var extras = new WrapPanel();
            foreach (var k in SewingExtras)
            {
                var key = k; var t = new TextBox { Name = "mode-sew-" + k, Text = d.Number("fs-" + k) > 0 ? d.Number("fs-" + k).ToString("0.#", De) : "", PlaceholderText = Sewing.Name(k) + " cm", Width = 150, FontSize = 12 };
                void Commit() { if (double.TryParse(t.Text, System.Globalization.NumberStyles.Float, De, out var v) && v > 0 && v != d.Number("fs-" + key)) d.Set("fs-" + key, v, Sewing.Name(key)); }
                t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
                extras.Children.Add(t);
            }
            sp.Children.Add(P("Brust, Taille und Hüfte stehen oben. Weitere Maße sind freiwillig – fehlende ergänzt Trendo aus der passenden Standardgröße und sagt, welche.", Muted, 11));
            sp.Children.Add(extras);
            sp.Children.Add(new WrapPanel { Children = { design, new Border { Width = 4 }, seam, new Border { Width = 4 },
                Act("mode-sew-run", "Schnittmuster erzeugen", () => SewingDraft(d, Sewing.Designs[Math.Max(0, design.SelectedIndex)].Id, seam.SelectedIndex switch { 0 => 0, 2 => 15, _ => 10 }), true) } });
            if (SewingLastSvg is { } f && File.Exists(f)) sp.Children.Add(Act("mode-sew-open", "Schnittmuster öffnen", () => Launch(f)));
        }
        if (_sewMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "mode-sew-msg", Text = _sewMsg, FontSize = 11.5, Foreground = Muted, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        return sp;
    }
}
