using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-04: Erweiterungen (nur Daten: Vorlagen, Schrittlisten, Links, Textbausteine).</summary>
public partial class MainWindow
{
    public ExtensionStore Exts => new(_core.DataRoot, _core.Audit);
    private string? _extShow;

    private void InitW5Ext() => RegisterTab(BusinessTabs, "erweiterungen", "Erweiterungen", "X-04", ExtView, "erweiterung", "plugin", "add-on", "vorlage", "marktplatz");

    public void ExtInstall(string path)
    {
        var (e, err) = Exts.Install(path);
        Say(e is not null ? $"„{e.Manifest.Name}“ {e.Manifest.Version} installiert." : "Nicht installiert: " + string.Join(" ", err), "erweiterungen");
    }

    public void ExtUse(InstalledExt e)
    {
        switch (e.Manifest.Kind)
        {
            case "seite":
                var tmp = Path.Combine(Path.GetTempPath(), "trendo-seite-" + Guid.NewGuid().ToString("N") + ".json"); File.WriteAllText(tmp, Exts.EntryText(e));
                _page = SitePage.Load(tmp); File.Delete(tmp); _page.Save(BuilderFile); Show("baukasten"); break;
            case "vorhaben":
                var dir = Path.Combine(SuiteFolder, DevStudio.SafeName(e.Manifest.Name));
                if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, CreationSuite.ListFile), Exts.EntryText(e)); }
                _suiteOpen = dir; Show("vorhaben"); break;
            default: _extShow = e.Manifest.Id; Show("erweiterungen"); break;
        }
    }

    private Control ExtView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Erweiterungen bringen neue Vorlagen, Schrittlisten, Links und Textbausteine. Sie enthalten nie Programme – das prüft Trendo bei jeder Installation.") } };
        var path = new TextBox { Name = "ext-path", PlaceholderText = "Ordner oder ZIP der Erweiterung", Width = 340 };
        sp.Children.Add(Card(new WrapPanel { Children = { path, Act("ext-install", "Installieren", () => ExtInstall(path.Text?.Trim('"', ' ') ?? ""), true),
            Act("ext-sample", "Beispiel ausprobieren", () => ExtInstall(ExtensionSamples.WriteSample(Path.Combine(_core.DataRoot, "beispiele")))) } }));
        sp.Children.Add(P("Einen Online-Katalog gibt es noch nicht. Erweiterungen kommen als Ordner oder ZIP.", null, 10.5));
        var all = Exts.All();
        if (all.Count == 0) sp.Children.Add(P("Noch keine Erweiterungen installiert."));
        var i = 0;
        foreach (var e in all)
        {
            var x = e;
            var on = new ToggleSwitch { Name = $"ext-on-{i}", IsChecked = e.Enabled, OnContent = "an", OffContent = "aus" };
            on.IsCheckedChanged += (_, _) => { Exts.SetEnabled(x.Manifest.Id, on.IsChecked == true); Show("erweiterungen"); };
            var use = Act($"ext-use-{i}", x.Manifest.Kind switch { "seite" => "Im Baukasten öffnen", "vorhaben" => "Als Vorhaben anlegen", _ => "Anzeigen" }, () => ExtUse(x)); use.IsEnabled = e.Enabled;
            var body = new StackPanel { Children = { new TextBlock { Text = $"{e.Manifest.Name} {e.Manifest.Version}", Foreground = Text, FontSize = 12.5 },
                P($"{e.Manifest.Description} · von {e.Manifest.Author} · Lizenz {e.Manifest.License} · Prüfsumme {e.Sha256[..12]}…", null, 10.5) } };
            if (_extShow == e.Manifest.Id)
            {
                if (e.Manifest.Kind == "links")
                {
                    try
                    {
                        foreach (var l in System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(Exts.EntryText(e)) ?? [])
                            if (Uri.TryCreate(l.Value, UriKind.Absolute, out var u) && u.Scheme == "https") { var url = u.AbsoluteUri; body.Children.Add(Act("ext-link-" + body.Children.Count, l.Key, () => Launch(url))); }
                    }
                    catch (System.Text.Json.JsonException) { body.Children.Add(P("Linkliste ist beschädigt.", Brush.Parse("#F97316"))); }
                }
                else body.Children.Add(new SelectableTextBlock { Name = "ext-text", Text = Exts.EntryText(e), Foreground = Text, TextWrapping = TextWrapping.Wrap });
            }
            sp.Children.Add(Card(Row(body, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { on, use, Act($"ext-remove-{i}", "Entfernen", () => { Exts.Remove(x.Manifest.Id); Show("erweiterungen"); }) } })));
            i++;
        }
        return BusinessPage("Erweiterungen", sp);
    }
}
