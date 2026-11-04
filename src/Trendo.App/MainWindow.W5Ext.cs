using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-04: Erweiterungen (nur Daten: Vorlagen, Schrittlisten, Links, Textbausteine).</summary>
public partial class MainWindow
{
    public ExtensionStore Exts => new(_core.DataRoot, _core.Audit, _core.Approvals); // T-0241: Installieren/Entfernen nur mit Freigabe
    private string? _extShow;
    /// <summary>T-0241: offene Rückfrage (Titel, Angaben, Knopftext, Aktion nach „Ja“).</summary>
    private (string Title, string Details, string Yes, Func<Task<string>> Run)? _extAsk;

    /// <summary>Freigabe für eine Erweiterungsaktion anlegen und erteilen (der Klick auf „Ja“ ist die Zustimmung). Not-Stopp → Ausnahme.</summary>
    string ExtApprove(string action, string description, string target)
    {
        var req = _core.Approvals.Request(Principal.LocalOwner, action, description, target);
        _core.Approvals.Approve(req.Id, Principal.LocalOwner);
        return req.Id;
    }

    /// <summary>Angaben aus der Beschreibung einer Erweiterung (Ordner oder ZIP) – für die Rückfrage, ohne etwas zu installieren.</summary>
    static string ExtPeek(string path)
    {
        try
        {
            ExtManifest? m = null;
            if (Directory.Exists(path)) m = Extensions.Validate(path).M;
            else if (File.Exists(path) && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var z = System.IO.Compression.ZipFile.OpenRead(path);
                var mf = z.Entries.FirstOrDefault(x => x.FullName == Extensions.ManifestFile || x.FullName.EndsWith("/" + Extensions.ManifestFile));
                if (mf is not null) m = System.Text.Json.JsonSerializer.Deserialize<ExtManifest>(new StreamReader(mf.Open()).ReadToEnd(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            return m is null ? $"„{Path.GetFileName(path)}“ – Beschreibung nicht lesbar; Trendo prüft beim Installieren." : $"„{m.Name}“ {m.Version} von {m.Author} · Art: {m.Kind} · Lizenz {m.License}";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException) { return $"„{Path.GetFileName(path)}“ – {e.Message}"; }
    }

    /// <summary>Für Tests: offene Rückfrage beantworten.</summary>
    public async Task ExtAnswer(bool yes)
    {
        if (_extAsk is not { } a) return;
        _extAsk = null;
        if (!yes) { Say("Abgebrochen – nichts verändert.", "erweiterungen"); return; }
        string msg;
        try { msg = await a.Run(); }
        catch (ApprovalException e) { msg = "Nicht ausgeführt: " + e.Message; }
        Say(msg, "erweiterungen");
    }

    /// <summary>Erweiterung entfernen – nach Rückfrage (Erweiterungsseite) bzw. bestätigtem Entfernen (Hinzufügen-Zentrale).</summary>
    public string ExtRemoveApproved(string id)
    {
        try { return Exts.Remove(id, ExtApprove(ExtensionStore.RemoveAction, $"Erweiterung {id} entfernen", id)) is { } err ? "Nicht entfernt: " + err : "Entfernt."; }
        catch (ApprovalException e) { return "Nicht entfernt: " + e.Message; }
    }

    private void InitW5Ext() => RegisterTab(BusinessTabs, "erweiterungen", "Erweiterungen", "X-04", ExtView, "erweiterung", "plugin", "add-on", "vorlage", "marktplatz");

    /// <summary>Erst fragen (mit Name, Version, Herkunft, Prüfsumme), dann mit Freigabe installieren.</summary>
    public void ExtInstall(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) { Say("Nicht installiert: Ordner oder ZIP nicht gefunden.", "erweiterungen"); return; }
        var hash = ExtensionStore.SourceHash(path);
        _extAsk = ("Erweiterung installieren?", $"{ExtPeek(path)}\nQuelle: {path}\nPrüfsumme {hash[..Math.Min(12, hash.Length)]}… · enthält nur Daten (Vorlagen, Listen, Links), keine Programme.", "Ja, installieren",
            () =>
            {
                var (e, err) = Exts.Install(path, ExtApprove(ExtensionStore.InstallAction, "Erweiterung installieren: " + Path.GetFileName(path), hash));
                return Task.FromResult(e is not null ? $"„{e.Manifest.Name}“ {e.Manifest.Version} installiert." : "Nicht installiert: " + string.Join(" ", err));
            });
        Show("erweiterungen");
    }

    public void ExtUse(InstalledExt e)
    {
        switch (e.Manifest.Kind)
        {
            case "seite":
                var tmp = Path.Combine(Path.GetTempPath(), "trendo-seite-" + Guid.NewGuid().ToString("N") + ".json"); File.WriteAllText(tmp, Exts.EntryText(e));
                _page = SitePage.Load(tmp); File.Delete(tmp); PersistBuilder(); Show("baukasten"); break;
            case "vorhaben":
                var dir = Path.Combine(SuiteFolder, DevStudio.SafeName(e.Manifest.Name));
                if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, CreationSuite.ListFile), Exts.EntryText(e)); }
                _suiteOpen = dir; Show("vorhaben"); break;
            default: _extShow = e.Manifest.Id; Show("erweiterungen"); break;
        }
    }

    /// <summary>Erweiterungen wie in VS Code (Clean-Room): links die Liste (installiert, mit An/Aus), rechts die Detailseite der gewählten
    /// Erweiterung mit Herkunft, Prüfsumme und Inhalt; Installieren und Katalog oben links.</summary>
    private Control ExtView()
    {
        var all = Exts.All();
        var left = new StackPanel { Spacing = 8 };
        if (_extAsk is { } ask)
            left.Children.Add(Card(new StackPanel { Name = "ext-ask", Spacing = 6, Children = {
                new TextBlock { Text = ask.Title, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = ask.Details, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                new WrapPanel { Children = { Act("ext-ask-yes", ask.Yes, () => _ = ExtAnswer(true), true), Act("ext-ask-no", "Abbrechen", () => _ = ExtAnswer(false)) } } } }));
        var path = new TextBox { Name = "ext-path", PlaceholderText = "Ordner oder ZIP der Erweiterung" };
        left.Children.Add(new StackPanel { Spacing = 6, Children = { path, new WrapPanel { Children = { Act("ext-install", "Installieren", () => ExtInstall(path.Text?.Trim('"', ' ') ?? ""), true),
            new Border { Width = 6 }, Act("ext-sample", "Beispiel ausprobieren", () => ExtInstall(ExtensionSamples.WriteSample(Path.Combine(_core.DataRoot, "beispiele")))) } } } });
        left.Children.Add(new Expander { Name = "ext-catalog", Header = new TextBlock { Text = "Katalog (geprüfte Erweiterungen)", Foreground = Text, FontSize = 12.5 }, Content = ExtCatalogCard(), HorizontalAlignment = HorizontalAlignment.Stretch });
        left.Children.Add(H($"Installiert ({all.Count})", 13));
        if (all.Count == 0) left.Children.Add(P("Noch keine Erweiterungen installiert. „Beispiel ausprobieren“ zeigt, wie es geht.", Muted, 12));
        var sel = all.FirstOrDefault(e => e.Manifest.Id == _extShow) ?? all.FirstOrDefault();
        var i = 0;
        foreach (var e in all)
        {
            var x = e; var on = e == sel;
            var toggle = new ToggleSwitch { Name = $"ext-on-{i}", IsChecked = e.Enabled, OnContent = "", OffContent = "", VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(toggle, e.Enabled ? "Abschalten" : "Einschalten");
            toggle.IsCheckedChanged += (_, _) => { Exts.SetEnabled(x.Manifest.Id, toggle.IsChecked == true); Show("erweiterungen"); };
            var item = new Button { Name = $"ext-item-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 6), CornerRadius = new CornerRadius(8),
                Background = on ? Brush.Parse("#26D4A84B") : Brushes.Transparent, BorderThickness = new Thickness(0),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,10,*,Auto"), Children = {
                    new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(8), Background = Brush.Parse("#33D4A84B"), Child = new TextBlock { Text = e.Manifest.Name.Length > 0 ? e.Manifest.Name[..1].ToUpperInvariant() : "?", Foreground = GoldText(), FontSize = 17, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } },
                    Pos(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = e.Manifest.Name, Foreground = e.Enabled ? Text : Muted, FontSize = 13, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = $"{e.Manifest.Author} · {e.Manifest.Version}", Foreground = Muted, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis } } }, 2),
                    Pos(toggle, 3) } } };
            item.Click += (_, _) => { _extShow = x.Manifest.Id; Show("erweiterungen"); };
            Hover(item); left.Children.Add(item);
            i++;
        }

        // rechts: Detailseite
        Control right;
        if (sel is null)
            right = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Children = {
                new TextBlock { Text = "🧩", FontSize = 52, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "Trendo erweitern", Foreground = Text, FontSize = 20, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
                P("Erweiterungen bringen neue Vorlagen, Schrittlisten, Links und Textbausteine. Sie enthalten nie Programme – das prüft Trendo bei jeder Installation.", Muted, 12.5) } };
        else
        {
            var x = sel; var idx = all.ToList().IndexOf(sel);
            var use = Act($"ext-use-{idx}", x.Manifest.Kind switch { "seite" => "Im Baukasten öffnen", "vorhaben" => "Als Vorhaben anlegen", _ => "Anzeigen" }, () => ExtUse(x), true); use.IsEnabled = x.Enabled;
            var remove = Act($"ext-remove-{idx}", "Entfernen", () => { _extAsk = ("Erweiterung entfernen?", $"„{x.Manifest.Name}“ {x.Manifest.Version} – ihre Dateien werden gelöscht. Damit angelegte Vorhaben und Seiten bleiben.", "Ja, entfernen", () => Task.FromResult(ExtRemoveApproved(x.Manifest.Id))); Show("erweiterungen"); });
            var detail = new StackPanel { Name = "ext-detail", Spacing = 10 };
            detail.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = {
                new Border { Width = 84, Height = 84, CornerRadius = new CornerRadius(16), Background = Brush.Parse("#33D4A84B"), Child = new TextBlock { Text = x.Manifest.Name.Length > 0 ? x.Manifest.Name[..1].ToUpperInvariant() : "?", Foreground = GoldText(), FontSize = 40, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } },
                new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2, Children = {
                    new TextBlock { Text = x.Manifest.Name, Foreground = Text, FontSize = 24, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = $"{x.Manifest.Author} · Version {x.Manifest.Version} · Lizenz {x.Manifest.License} · {(x.Enabled ? "eingeschaltet" : "ausgeschaltet")}", Foreground = Muted, FontSize = 12 },
                    new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = { use, new Border { Width = 6 }, remove } } } } } });
            detail.Children.Add(P(x.Manifest.Description, Text, 13));
            detail.Children.Add(new TextBlock { Text = $"Geprüft: Prüfsumme (SHA-256) {x.Sha256[..16]}… · Art: {x.Manifest.Kind} · enthält keine Programme", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            detail.Children.Add(H("Inhalt", 14));
            if (x.Manifest.Kind == "links")
            {
                try
                {
                    var links = new WrapPanel();
                    foreach (var l in System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(Exts.EntryText(x)) ?? [])
                        if (Uri.TryCreate(l.Value, UriKind.Absolute, out var u) && u.Scheme == "https") { var url = u.AbsoluteUri; links.Children.Add(Act("ext-link-" + links.Children.Count, l.Key, () => Launch(url))); }
                    detail.Children.Add(links);
                }
                catch (System.Text.Json.JsonException) { detail.Children.Add(P("Linkliste ist beschädigt.", Brush.Parse("#F97316"))); }
            }
            else detail.Children.Add(new Border { Background = Brush.Parse("#0B1424"), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 10), Child = new SelectableTextBlock { Name = "ext-text", Text = Exts.EntryText(x), Foreground = Text, TextWrapping = TextWrapping.Wrap } });
            right = new ScrollViewer { Content = detail };
        }
        var grid = new Grid { Name = "ext-manager", ColumnDefinitions = new ColumnDefinitions("340,14,*"), Children = { Card(new ScrollViewer { Content = left }), Pos(Card(right), 2) } };
        return BusinessPage("Erweiterungen", grid);
    }
}
