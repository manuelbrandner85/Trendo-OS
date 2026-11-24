using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-04: Erweiterungen – Daten (Vorlagen, Schrittlisten, Links, Textbausteine) und seit T-0766 Funktions-Erweiterungen in der WebAssembly-Sandbox
/// (Befehl im Studio „Projekte“, Rechte in eigenem Schritt freigeben und entziehen, Werkstatt zum Selbstbauen).</summary>
public partial class MainWindow
{
    public ExtensionStore Exts => new(_core.DataRoot, _core.Audit, _core.Approvals); // T-0241: Installieren/Entfernen nur mit Freigabe
    private string? _extShow;
    /// <summary>T-0241: offene Rückfrage (Titel, Angaben, Knopftext, Aktion nach „Ja“).</summary>
    private (string Title, string Details, string Yes, Func<Task<string>> Run)? _extAsk;
    /// <summary>T-0766 (Befund N5): beim Update einer Funktions-Erweiterung mit Daten – Haken „Datenordner behalten“ (null = nicht gezeigt, Vorgabe aus).</summary>
    private bool? _extKeepData;
    /// <summary>T-0766 (Befund N6): geladene Datei aus einem Netz-Katalog, bis die Rückfrage beantwortet ist.</summary>
    private string? _extPendingFile;

    /// <summary>Freigabe für eine Erweiterungsaktion anlegen und erteilen (der Klick auf „Ja“ ist die Zustimmung). Not-Stopp → Ausnahme.</summary>
    /// B4 (T-0706): Aufruf nur aus dem „Ja“ der Rückfrage-Karte (_extAsk); freigeben darf nur die angemeldete erwachsene Person (Kern prüft die Rolle).
    string ExtApprove(string action, string description, string target) =>
        FreigabeAusRueckfrage(new Wirkung(action, target, description, description, "Ja")) ?? throw new ApprovalException(CanApprove ? "Nicht freigegeben." : NurErwachsene);

    /// <summary>Beschreibung einer Erweiterung (Ordner oder ZIP) lesen, ohne etwas zu installieren; null, wenn nicht lesbar.</summary>
    static ExtManifest? ExtPeekManifest(string path)
    {
        try
        {
            if (Directory.Exists(path)) return Extensions.Validate(path).M;
            if (!File.Exists(path) || !path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return null;
            using var z = System.IO.Compression.ZipFile.OpenRead(path);
            var mf = z.Entries.FirstOrDefault(x => x.FullName == Extensions.ManifestFile || x.FullName.EndsWith("/" + Extensions.ManifestFile));
            return mf is null || mf.Length > 1 << 20 ? null : System.Text.Json.JsonSerializer.Deserialize<ExtManifest>(new StreamReader(mf.Open()).ReadToEnd(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Angaben aus der Beschreibung einer Erweiterung (Ordner oder ZIP) – für die Rückfrage, ohne etwas zu installieren.</summary>
    string ExtPeek(string path)
    {
        try
        {
            var m = ExtPeekManifest(path);
            if (m is null) return $"„{Path.GetFileName(path)}“ – Beschreibung nicht lesbar; Trendo prüft beim Installieren.";
            var head = $"„{m.Name}“ {m.Version} von {m.Author} · Art: {m.Kind} · Lizenz {m.License}";
            // T-0766 (Befund N5): ein Update sagt, welche Fassung es ersetzt
            if (m.Id is not null && FreeAssets.SafeRel(m.Id) && Exts.Get(m.Id) is { } cur)
                head += Extensions.CompareVersion(m.Version, cur.Manifest.Version) < 0 ? $"\nÄlter als die installierte Fassung {cur.Manifest.Version} – wird nicht installiert."
                    : $"\nErsetzt die installierte Fassung {cur.Manifest.Version}." + (cur.Manifest.Kind == Extensions.ModuleKind ? " Ihre Rechte müssen danach neu freigegeben werden." : "");
            // T-0766: Rechte vor der Installation verständlich zeigen – freigegeben werden sie erst danach in einem eigenen Schritt
            return m.Kind != Extensions.ModuleKind ? head + "\nEnthält nur Daten (Vorlagen, Listen, Links), keine Programme."
                : head + "\nFunktions-Erweiterung: läuft nur in der Sandbox – ohne Internet, ohne Zugriff auf andere Dateien, mit Zeit-, Speicher- und Ordnergrenze (15 s, 64 MB, eigener Ordner höchstens 100 MB).\nMöchte diese Rechte (freigeben erst nach dem Installieren, in einem eigenen Schritt):\n"
                    + string.Join("\n", (m.Permissions ?? []).Select(r => "· " + ExtensionRights.Explain(r)));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException) { return $"„{Path.GetFileName(path)}“ – {e.Message}"; }
    }

    /// <summary>Für Tests: offene Rückfrage beantworten.</summary>
    public async Task ExtAnswer(bool yes)
    {
        if (_extAsk is not { } a) return;
        _extAsk = null;
        if (!yes)
        {
            if (_extPendingFile is { } pf && File.Exists(pf)) File.Delete(pf);
            _extPendingFile = null; _extKeepData = null;
            Say("Abgebrochen – nichts verändert.", "erweiterungen"); return;
        }
        string msg;
        try { msg = await a.Run(); }
        catch (ApprovalException e) { msg = "Nicht ausgeführt: " + Trendo.Core.FriendlyError.Say(e); }
        Say(msg, "erweiterungen");
    }

    /// <summary>Erweiterung entfernen – nach Rückfrage (Erweiterungsseite) bzw. bestätigtem Entfernen (Hinzufügen-Zentrale).</summary>
    public string ExtRemoveApproved(string id)
    {
        try { return Exts.Remove(id, ExtApprove(ExtensionStore.RemoveAction, $"Erweiterung {id} entfernen", id)) is { } err ? "Nicht entfernt: " + err : "Entfernt."; }
        catch (ApprovalException e) { return "Nicht entfernt: " + Trendo.Core.FriendlyError.Say(e); }
    }

    private void InitW5Ext() => RegisterTab(BusinessTabs, "erweiterungen", "Erweiterungen", "X-04", ExtView, "erweiterung", "plugin", "add-on", "vorlage", "marktplatz");

    /// <summary>Erst fragen (mit Name, Version, Herkunft, Prüfsumme), dann mit Freigabe installieren.</summary>
    public void ExtInstall(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) { Say("Nicht installiert: Ordner oder ZIP nicht gefunden.", "erweiterungen"); return; }
        var hash = ExtensionStore.SourceHash(path);
        ExtOfferKeepData(path);
        _extAsk = ("Erweiterung installieren?", $"{ExtPeek(path)}\nQuelle: {path}\nPrüfsumme {hash[..Math.Min(12, hash.Length)]}…", "Ja, installieren",
            async () =>
            {
                var approval = ExtApprove(ExtensionStore.InstallAction, "Erweiterung installieren: " + Path.GetFileName(path), hash);
                var keep = _extKeepData == true; _extKeepData = null;
                var store = Exts;
                // T-0766 (Befund M2): Prüfen und Übersetzen nicht auf dem Oberflächen-Thread
                var (e, err) = await Task.Run(() => store.Install(path, approval, keep));
                return ExtInstalledText(e, err, false);
            });
        Show("erweiterungen");
    }

    /// <summary>Befund N5: Haken „Datenordner behalten“ nur anbieten, wenn ein Update Daten der bisherigen Fassung vorfindet.</summary>
    void ExtOfferKeepData(string path)
    {
        _extKeepData = ExtPeekManifest(path) is { Id: { } id } m && FreeAssets.SafeRel(id) && Exts.Get(id) is not null
            && Directory.Exists(Exts.DataDir(id)) && Directory.EnumerateFileSystemEntries(Exts.DataDir(id)).Any() ? false : null;
    }

    /// <summary>Befund N6: Erfolgsmeldung nennt bei Funktions-Erweiterungen den nächsten Schritt (Rechte freigeben).</summary>
    string ExtInstalledText(InstalledExt? e, List<string> err, bool checksum)
    {
        if (e is null) return "Nicht installiert: " + string.Join(" ", err);
        _extShow = e.Manifest.Id;
        var done = $"„{e.Manifest.Name}“ {e.Manifest.Version} installiert{(checksum ? " (Prüfsumme bestätigt)" : "")}.";
        return e.Manifest.Kind == Extensions.ModuleKind ? done + " Noch ohne Rechte – „Rechte prüfen und freigeben“ ist der nächste Schritt." : done;
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
            case Extensions.ModuleKind:
                // T-0766: ohne freigegebene Rechte zuerst die Rechte zeigen, sonst ins Studio „Projekte“, wo der Knopf liegt
                if (Exts.GrantedRights(e).Count == 0) ExtAskGrant(e); else Show("projects");
                break;
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
                ExtKeepDataBox(),
                new WrapPanel { Children = { Act("ext-ask-yes", ask.Yes, () => _ = ExtAnswer(true), true), Act("ext-ask-no", "Abbrechen", () => _ = ExtAnswer(false)) } } } }));
        var path = new TextBox { Name = "ext-path", PlaceholderText = "Ordner oder ZIP der Erweiterung" };
        left.Children.Add(new StackPanel { Spacing = 6, Children = { path, new WrapPanel { Children = { Act("ext-install", "Installieren", () => ExtInstall(path.Text?.Trim('"', ' ') ?? ""), true),
            new Border { Width = 6 }, Act("ext-sample", "Beispiel ausprobieren", () => ExtInstall(ExtensionSamples.WriteSample(Path.Combine(_core.DataRoot, "beispiele")))) } } } });
        left.Children.Add(ExtExpander("ext-werkstatt", "Eigene Erweiterung bauen", ExtWorkshopCard(), _extWsOpen, v => _extWsOpen = v));
        left.Children.Add(ExtExpander("ext-catalog", "Katalog (geprüfte Erweiterungen)", ExtCatalogCard(), false, null));
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
                P("Erweiterungen bringen neue Funktionen – etwa einen Umwandler für Projektdateien – sowie Vorlagen, Schrittlisten, Links und Textbausteine. Funktionen laufen nur in einer abgesicherten Sandbox: ohne Internet, ohne Zugriff auf fremde Dateien, und erst nachdem du ihre Rechte freigegeben hast.", Muted, 12.5) } };
        else
        {
            var x = sel; var idx = all.ToList().IndexOf(sel);
            var module = x.Manifest.Kind == Extensions.ModuleKind;
            var granted = module ? Exts.GrantedRights(x) : [];
            var use = Act($"ext-use-{idx}", x.Manifest.Kind switch { "seite" => "Im Baukasten öffnen", "vorhaben" => "Als Vorhaben anlegen", Extensions.ModuleKind => granted.Count == 0 ? "Rechte prüfen und freigeben" : "Im Studio „Projekte“ benutzen", _ => "Anzeigen" }, () => ExtUse(x), true); use.IsEnabled = x.Enabled;
            var remove = Act($"ext-remove-{idx}", "Entfernen", () => { _extAsk = ("Erweiterung entfernen?", $"„{x.Manifest.Name}“ {x.Manifest.Version} – ihre Dateien{(module ? ", ihre Rechte und ihr eigener Datenordner" : "")} werden gelöscht. Damit angelegte Vorhaben, Seiten und Projektdateien bleiben.", "Ja, entfernen", () => Task.FromResult(ExtRemoveApproved(x.Manifest.Id))); Show("erweiterungen"); });
            var detail = new StackPanel { Name = "ext-detail", Spacing = 10 };
            detail.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = {
                new Border { Width = 84, Height = 84, CornerRadius = new CornerRadius(16), Background = Brush.Parse("#33D4A84B"), Child = new TextBlock { Text = x.Manifest.Name.Length > 0 ? x.Manifest.Name[..1].ToUpperInvariant() : "?", Foreground = GoldText(), FontSize = 40, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } },
                new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2, Children = {
                    new TextBlock { Text = x.Manifest.Name, Foreground = Text, FontSize = 24, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = $"{x.Manifest.Author} · Version {x.Manifest.Version} · Lizenz {x.Manifest.License} · {(x.Enabled ? "eingeschaltet" : "ausgeschaltet")}", Foreground = Muted, FontSize = 12 },
                    ExtButtons(use, remove, module && granted.Count > 0 ? Act($"ext-revoke-{idx}", "Rechte entziehen", () => { Exts.RevokeRights(x.Manifest.Id); Say($"Rechte von „{x.Manifest.Name}“ entzogen – ihr Knopf ist aus dem Studio „Projekte“ verschwunden.", "erweiterungen"); }) : null) } } } });
            detail.Children.Add(P(x.Manifest.Description, Text, 13));
            detail.Children.Add(new TextBlock { Text = module ? $"Geprüft: Prüfsumme (SHA-256) {x.Sha256[..16]}… · Modul {x.Manifest.ModuleSha256?[..16]}… · Funktions-Erweiterung, läuft nur in der Sandbox (WebAssembly)"
                : $"Geprüft: Prüfsumme (SHA-256) {x.Sha256[..16]}… · Art: {x.Manifest.Kind} · enthält keine Programme", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            if (module) ExtModuleDetail(detail, x, granted);
            else detail.Children.Add(H("Inhalt", 14));
            if (module) { }
            else if (x.Manifest.Kind == "links")
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
