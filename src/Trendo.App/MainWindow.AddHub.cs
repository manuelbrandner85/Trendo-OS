using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Wunsch 27.10.2026 (A1–A4): Installieren leicht finden. Ein Ort „Hinzufügen“ für Programme, KI, GitHub und Erweiterungen mit einer
/// Suche über alles (A1), ein „Hinzufügen …“-Knopf in jedem Studio, das etwas braucht (A2), Treffer in der Trendo-Suche (A3) und
/// „Meine Installationen“ mit Aktualisieren und Entfernen (A4). Installiert wird über die bestehenden, geprüften Wege – nur nach Zustimmung.</summary>
public partial class MainWindow
{
    /// <summary>Ein Eintrag der Zentrale. <paramref name="Install"/> null = nur öffnen (eigene Seite), <paramref name="Remove"/>/<paramref name="Update"/> für „Meine Installationen“.</summary>
    public sealed record AddItem(string Id, string Tab, string Name, string What, bool Installed, Func<Task>? Install, Func<Task>? Open, Func<Task>? Remove = null, Func<Task>? Update = null, string? License = null);

    string _hubTab = "alles", _hubQuery = "", _hubMsg = "", _hubAsk = "";
    IReadOnlyList<string> _hubOllama = [];
    DateTime _hubOllamaAt;
    public static readonly (string Id, string Label)[] HubTabs = [("alles", "Alles"), ("programme", "Programme"), ("ki", "KI"), ("github", "GitHub"), ("erweiterungen", "Erweiterungen"), ("meine", "Meine Installationen")];

    void InitAddHub()
    {
        _showMap["hinzufuegen"] = AddHubView;
        if (!ExtraPages.Any(p => p.Key == "hinzufuegen")) ExtraPages.Add(("Hinzufügen", "hinzufuegen", ["installieren", "herunterladen", "hinzufügen", "programm", "app", "ki", "modell", "github", "erweiterung", "plugin", "download", "deinstallieren", "entfernen", "aktualisieren", "update", "meine installationen"]));
    }

    /// <summary>A2: Hub für ein Studio öffnen – zeigt, was es braucht.</summary>
    public void OpenAddHubFor(string studioId)
    {
        var need = StudioTools.Required.TryGetValue(studioId, out var n) ? n : [];
        _hubTab = "alles";
        _hubQuery = need.Length > 0 ? DependencyCatalog.Get(need[0]).Name : StudioCatalog.NameOf(studioId, "");
        Show("hinzufuegen");
    }

    public void OpenAddHub(string tab = "alles", string query = "") { _hubTab = tab; _hubQuery = query; Show("hinzufuegen"); }

    /// <summary>Alles, was sich in Trendo hinzufügen lässt – aus den vorhandenen Katalogen.</summary>
    public List<AddItem> AddItems()
    {
        var items = new List<AddItem>();
        // Programme (Vollpaket und freie Programme mit offiziellem Installer)
        foreach (var c in DependencyCatalog.All.Where(c => c.Id != "model"))
        {
            var comp = c;
            var inc = Vollpaket.Included.TryGetValue(c.Id, out var i) ? i.Package : null;
            items.Add(new(c.Id, "programme", c.Name, c.Purpose, _deps.IsInstalled(c),
                c.Mode is DeliveryMode.AutoUser or DeliveryMode.AutoElevated or DeliveryMode.Included ? async () => { if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner); await InstallOne(comp); } : null,
                () => { Show("components"); return Task.CompletedTask; },
                inc is null ? null : () => { Vollpaket.Uninstall(_deps.ToolsRoot, inc, _core.Audit); return Task.CompletedTask; },
                async () => { if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner); await InstallOne(comp); }, c.License));
        }
        // KI: freie Modelle für die KI auf dem PC
        foreach (var m in KiCatalog.Models)
        {
            var tag = m.Tag; var name = m.Name;
            items.Add(new("ollama:" + tag, "ki", m.Name, $"KI-Modell für den PC · {m.GoodFor} · {m.SizeGb:0.0} GB", _hubOllama.Contains(tag),
                async () =>
                {
                    var req = _core.Approvals.Request(Principal.LocalOwner, "ki.pull", $"KI-Modell {name} laden", tag);
                    _core.Approvals.Approve(req.Id, Principal.LocalOwner);
                    var ok = await _core.Approvals.Execute(req.Id, "ki.pull", tag, () => new OllamaBackend().PullAsync(tag));
                    _hubMsg = ok ? $"{name} ist jetzt da." : $"{name} konnte nicht geladen werden (läuft Ollama, ist Internet da?).";
                    _hubOllamaAt = default;
                },
                () => { Show("ki-quellen"); return Task.CompletedTask; },
                async () => { await new OllamaBackend().DeleteAsync(tag); _hubOllamaAt = default; }, null, m.License));
        }
        // KI-Programme (GitHub/Hersteller)
        foreach (var s in KiCatalog.Sources.Where(s => KiSourceSetups.For(s.Id) is not null))
        {
            var id = s.Id;
            items.Add(new("kisrc:" + id, "ki", s.Name, "KI-Programm · " + s.Hint, KiSourceExe(id) is not null,
                async () => { await KiSourceInstall(id); }, () => { Show("ki-quellen"); return Task.CompletedTask; },
                () => { KiSourceRemove(id); return Task.CompletedTask; }, async () => { await KiSourceInstall(id); }, s.License));
        }
        items.Add(new("page:ki-internet", "ki", "Kostenlose Internet-KIs (OpenRouter)", "Chat, Bilder und Audio verstehen – kostenloses Konto, geht ins Internet", OpenRouterReady, null, () => { Show("ki-internet"); return Task.CompletedTask; }));
        items.Add(new("page:ki-bibliothek", "ki", "KI-Bibliothek", "Bilder, Videos, Sprache, Vorlesen und mehr – kostenlose KI nach Arten", false, null, () => { Show("ki-bibliothek"); return Task.CompletedTask; }));
        // GitHub: installierte Programme (Suche läuft über die GitHub-Seite)
        foreach (var e in GitHubApps.List(_deps.ToolsRoot))
        {
            var entry = e;
            items.Add(new("gh:" + e.Id, "github", e.Repo, $"Von GitHub · Version {e.Version}", true, null,
                () => { if (GitHubApps.ExePath(_deps.ToolsRoot, entry) is { } exe) Launch(exe); return Task.CompletedTask; },
                () => { if (Path.IsPathRooted(entry.Exe) && File.Exists(entry.Exe)) GitHubRunUninstaller(entry.Exe); GitHubApps.Uninstall(_deps.ToolsRoot, entry.Id, _core.Audit); return Task.CompletedTask; },
                async () => { await GitHubInstall(new OssRepo(entry.Repo, "", "https://github.com/" + entry.Repo, "https://github.com/" + entry.Repo + ".git", 0, entry.License, "", DateTimeOffset.Now)); }, entry.License));
        }
        // Erweiterungen
        foreach (var x in Exts.All())
        {
            var xid = x.Manifest.Id;
            items.Add(new("ext:" + xid, "erweiterungen", x.Manifest.Name, $"Erweiterung · Version {x.Manifest.Version}", true, null,
                () => { Show("erweiterungen"); return Task.CompletedTask; }, () => { Exts.Remove(xid); return Task.CompletedTask; }));
        }
        items.Add(new("page:erweiterungen", "erweiterungen", "Erweiterungen hinzufügen", "Vorlagen, Plugins und Add-ons für Trendo", false, null, () => { Show("erweiterungen"); return Task.CompletedTask; }));
        return items;
    }

    static bool Matches(AddItem i, string q) => q.Length == 0 || $"{i.Name} {i.What} {i.Tab}".Contains(q, StringComparison.OrdinalIgnoreCase)
        || q.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w => $"{i.Name} {i.What}".Contains(w, StringComparison.OrdinalIgnoreCase));

    Control AddHubView()
    {
        if (DateTime.UtcNow - _hubOllamaAt > TimeSpan.FromMinutes(1) && !_opt.Isolated)
        {
            _hubOllamaAt = DateTime.UtcNow;
            _ = Task.Run(async () => { var have = await new OllamaBackend().ModelsAsync(); Avalonia.Threading.Dispatcher.UIThread.Post(() => { _hubOllama = have; if (CurrentView == "hinzufuegen") Show("hinzufuegen"); }); });
        }
        var sp = new StackPanel { Spacing = 10 };
        sp.Children.Add(P("Programme, KI, GitHub-Projekte und Erweiterungen – alles an einem Ort. Trendo lädt nur aus offiziellen Quellen und fragt vorher.", Text, 13));
        var search = new TextBox { Name = "hub-search", Text = _hubQuery, PlaceholderText = "Was möchtest du hinzufügen? z. B. Video schneiden, Bild, KI, Musik …", MinWidth = 280 };
        search.TextChanged += (_, _) => { _hubQuery = search.Text ?? ""; FillHub(sp); };
        sp.Children.Add(search);
        var tabs = new WrapPanel { Name = "hub-tabs" };
        foreach (var (id, label) in HubTabs)
        {
            var on = _hubTab == id;
            var t = new Button { Name = "hub-tab-" + id, Content = label, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(14), Background = on ? Accent : Brushes.Transparent, BorderBrush = EdgeLit, BorderThickness = new Thickness(on ? 0 : 1) };
            var tid = id;
            t.Click += (_, _) => { _hubTab = tid; Show("hinzufuegen"); };
            tabs.Children.Add(t);
        }
        sp.Children.Add(tabs);
        if (_hubMsg.Length > 0) sp.Children.Add(P(_hubMsg, Brush.Parse("#4ADE80"), 12.5));
        FillHub(sp);
        return Page("Hinzufügen", sp);
    }

    void FillHub(StackPanel sp)
    {
        var fixedCount = _hubMsg.Length > 0 ? 4 : 3;
        while (sp.Children.Count > fixedCount) sp.Children.RemoveAt(fixedCount);
        var q = _hubQuery.Trim();
        var mine = _hubTab == "meine";
        var all = AddItems().Where(i => (mine ? i.Installed : _hubTab == "alles" || i.Tab == _hubTab) && Matches(i, q)).ToList();
        var list = new StackPanel { Name = "hub-list", Spacing = 6 };
        if (mine) list.Children.Add(P($"Alles, was du in Trendo installiert hast ({all.Count}). Aktualisieren lädt die neueste Version, Entfernen gibt den Platz frei.", null, 12));
        foreach (var grp in all.GroupBy(i => i.Tab))
        {
            list.Children.Add(H(HubTabs.First(t => t.Id == grp.Key).Label, 14));
            foreach (var it in grp.OrderBy(i => i.Installed && !mine).ThenBy(i => i.Name).Take(q.Length > 0 || _hubTab != "alles" ? 60 : 8)) list.Children.Add(HubRow(it, mine));
        }
        // GitHub: Suche nach freien Programmen auf GitHub
        if (!mine && _hubTab is "alles" or "github")
        {
            var gh = new Button { Name = "hub-github-search", Content = q.Length > 0 ? $"🔎 Auf GitHub nach „{q}“ suchen" : "🔎 Freie Programme auf GitHub suchen", IsEnabled = !_core.Offline, Margin = new Thickness(0, 6, 0, 0) };
            gh.Click += async (_, _) => { Show("oss"); if (q.Length > 0) await OssSearch(q); };
            list.Children.Add(gh);
        }
        if (all.Count == 0 && mine) list.Children.Add(P("Noch nichts installiert.", null, 12));
        sp.Children.Add(list);
    }

    Control HubRow(AddItem it, bool mine)
    {
        var key = "hub:" + it.Id;
        var safe = it.Id.Replace(':', '-').Replace('/', '-').Replace('.', '-');
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (it.Installed)
        {
            right.Children.Add(new TextBlock { Text = "✓ installiert", Foreground = Brush.Parse("#4ADE80"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            if (it.Open is { } open) { var o = new Button { Name = "hub-open-" + safe, Content = "Öffnen" }; o.Click += async (_, _) => await open(); right.Children.Add(o); }
            if (mine && it.Update is { } upd)
            {
                var u = new Button { Name = "hub-update-" + safe, Content = "Aktualisieren", IsEnabled = !_core.Offline && !_depBusy };
                u.Click += async (_, _) => { _hubMsg = $"{it.Name} wird aktualisiert …"; Show("hinzufuegen"); await upd(); _hubMsg = $"{it.Name} ist aktuell."; Show("hinzufuegen"); };
                right.Children.Add(u);
            }
            if (it.Remove is { } rem)
                right.Children.Add(ConfirmRemoveButton(key, "hub-remove-" + safe, "hinzufuegen", async () =>
                {
                    try { await rem(); _hubMsg = $"{it.Name} wurde entfernt."; }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { _hubMsg = $"{it.Name} läuft noch oder ist gesperrt – bitte schließen und erneut versuchen."; }
                }));
        }
        else if (it.Install is { } install)
        {
            if (_hubAsk == it.Id)
            {
                var yes = new Button { Name = "hub-yes-" + safe, Content = "Ja, installieren", Background = Accent };
                yes.Click += async (_, _) => { _hubAsk = ""; _hubMsg = $"{it.Name} wird installiert …"; Show("hinzufuegen"); await install(); _hubMsg = $"{it.Name}: fertig."; Show("hinzufuegen"); };
                var no = new Button { Name = "hub-no-" + safe, Content = "Nein" }; no.Click += (_, _) => { _hubAsk = ""; Show("hinzufuegen"); };
                right.Children.Add(new TextBlock { Text = it.License is { } l ? $"Lizenz: {l}" : "Offizielle Quelle", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
                right.Children.Add(yes); right.Children.Add(no);
            }
            else
            {
                var b = new Button { Name = "hub-install-" + safe, Content = "Installieren", Background = Accent, IsEnabled = !_core.Offline && !_depBusy && !_settings.Child };
                if (!b.IsEnabled) ToolTip.SetTip(b, _settings.Child ? "Frag einen Erwachsenen – Installieren geht nur in einem Erwachsenen-Profil." : _core.Offline ? "Im Offline-Modus wird nichts geladen." : "Gerade läuft schon eine Installation.");
                b.Click += (_, _) => { _hubAsk = it.Id; Show("hinzufuegen"); };
                right.Children.Add(b);
            }
        }
        else if (it.Open is { } open2) { var o = new Button { Name = "hub-open-" + safe, Content = "Öffnen" }; o.Click += async (_, _) => await open2(); right.Children.Add(o); }
        return WCard(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
            new StackPanel { Children = { new TextBlock { Text = it.Name, Foreground = Text, FontSize = 13.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap }, P(it.What, null, 11.5) } },
            Pos(right, 1) } });
    }
}
