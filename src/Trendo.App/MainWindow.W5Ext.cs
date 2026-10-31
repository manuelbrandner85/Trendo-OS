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

    private Control ExtView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Erweiterungen bringen neue Vorlagen, Schrittlisten, Links und Textbausteine. Sie enthalten nie Programme – das prüft Trendo bei jeder Installation.") } };
        if (_extAsk is { } ask)
            sp.Children.Add(Card(new StackPanel { Name = "ext-ask", Spacing = 6, Children = {
                new TextBlock { Text = ask.Title, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = ask.Details, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                new WrapPanel { Children = { Act("ext-ask-yes", ask.Yes, () => _ = ExtAnswer(true), true), Act("ext-ask-no", "Abbrechen", () => _ = ExtAnswer(false)) } } } }));
        var path = new TextBox { Name = "ext-path", PlaceholderText = "Ordner oder ZIP der Erweiterung", Width = 340 };
        sp.Children.Add(Card(new WrapPanel { Children = { path, Act("ext-install", "Installieren", () => ExtInstall(path.Text?.Trim('"', ' ') ?? ""), true),
            Act("ext-sample", "Beispiel ausprobieren", () => ExtInstall(ExtensionSamples.WriteSample(Path.Combine(_core.DataRoot, "beispiele")))) } }));
        sp.Children.Add(ExtCatalogCard());
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
            sp.Children.Add(Card(Row(body, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { on, use, Act($"ext-remove-{i}", "Entfernen", () => { _extAsk = ("Erweiterung entfernen?", $"„{x.Manifest.Name}“ {x.Manifest.Version} – ihre Dateien werden gelöscht. Damit angelegte Vorhaben und Seiten bleiben.", "Ja, entfernen", () => Task.FromResult(ExtRemoveApproved(x.Manifest.Id))); Show("erweiterungen"); }) } })));
            i++;
        }
        return BusinessPage("Erweiterungen", sp);
    }
}
