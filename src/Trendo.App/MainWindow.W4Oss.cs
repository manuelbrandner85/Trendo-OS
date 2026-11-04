using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Net.Http.Headers;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-06: Offene Projekte von GitHub. Wunsch 27.10.2026: Auswählen = laden, einrichten und sofort starten; ausführliche Beschreibung
/// (README von GitHub, auf Wunsch von der KI auf Deutsch erklärt); alles wieder entfernbar.</summary>
public partial class MainWindow
{
    private IReadOnlyList<OssRepo> _ossHits = [];
    private string _ossQuery = "";
    private readonly Dictionary<string, (string? Intro, List<string>? Features)> _ossReadme = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _ossGerman = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _ossLoading = new(StringComparer.OrdinalIgnoreCase);
    public string OssFolder => WorkFolder("Open Source");
    public Func<string, Task<IReadOnlyList<OssRepo>>>? OssSearchOverride { get; set; }
    /// <summary>Für Tests: README-Text statt GitHub.</summary>
    public Func<string, Task<string>>? OssReadmeOverride { get; set; }
    private static readonly HttpClient OssHttp = new() { Timeout = TimeSpan.FromSeconds(20) };

    private void InitW4Oss() { LoadGitHubToken(); InitW4OssTab(); }
    private void InitW4OssTab() => RegisterTab(KreativTabs, "oss", "Open Source", "X-06", OssView, "github", "open source", "quellcode", "lizenz", "programm installieren");

    public async Task OssSearch(string q)
    {
        _ossQuery = q;
        if (_core.Offline) { Say("Trendo ist offline. Die Suche braucht Internet.", "oss"); return; }
        try { _ossHits = OssSearchOverride is not null ? await OssSearchOverride(q) : await OssSearchHttp(OssHttp, q); _w45Msg = _ossHits.Count == 0 ? "Nichts gefunden." : ""; }
        catch (Exception ex) { _ossHits = []; _w45Msg = "Suche hat nicht geklappt: " + ex.Message; }
        // Masterprompt 131: gleich zeigen, ob es ein fertiges Windows-Programm gibt (ohne Anmeldung nur die ersten 6 – schont das GitHub-Limit)
        if (_ossHits.Count > 0 && (OssSearchOverride is null || GitHubHandlerOverride is not null)) _ = OssCheckAvailability(_ossHits.Take(GitHubToken is null ? 6 : 20).Select(h => h.FullName));
        _core.Audit.Append(Principal.LocalOwner.Name, "oss.search", q);
        Show("oss");
    }

    /// <summary>README holen und daraus Einleitung und Funktionen lesen (einmal je Projekt).</summary>
    public async Task OssLoadReadme(string repo)
    {
        if (_ossReadme.ContainsKey(repo) || !_ossLoading.Add(repo)) return;
        try
        {
            string md;
            if (OssReadmeOverride is { } o) md = await o(repo);
            else { using var res = await GitHubGet(GitHubApps.ReadmeUrl(repo), "application/vnd.github.raw"); md = await res.Content.ReadAsStringAsync(); }
            _ossReadme[repo] = (GitHubApps.ReadmeIntro(md), GitHubApps.ReadmeFeatures(md));
            _ossReadmeText[repo] = md.Length > 6000 ? md[..6000] : md;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException) { _ossReadme[repo] = (null, null); }
        finally { _ossLoading.Remove(repo); }
        if (CurrentView is "oss" or "components") Show(CurrentView);
    }
    private readonly Dictionary<string, string> _ossReadmeText = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Von der KI auf Deutsch erklären lassen: was, wofür, für wen (mit KI-Fortschritt).</summary>
    public async Task OssExplain(OssRepo r)
    {
        await OssLoadReadme(r.FullName);
        var md = _ossReadmeText.GetValueOrDefault(r.FullName) ?? r.Description;
        using var kiLabel = KiActivityHub.Label("GitHub-Programm erklären");
        var a = await Intel.AskAsync(new KiRequest($"Erkläre auf Deutsch in 4 bis 6 einfachen Sätzen, was das Programm „{r.FullName}“ ist, wofür man es im Alltag nutzt und für wen es geeignet ist. Keine Werbung, keine Erfindungen – nur, was in der Beschreibung steht.\n\nBeschreibung:\n{r.Description}\n\nREADME:\n{md}",
            "Du erklärst Programme für Menschen ohne Technikwissen. Antworte nur mit dem Erklärtext.", PreferredModel: KiModelName, UseKnowledge: false));
        if (a.Ok) _ossGerman[r.FullName] = a.Text.Trim();
        else _w45Msg = "Die KI konnte es gerade nicht erklären: " + a.Text;
        Show("oss");
    }

    private Control OssView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Finde freie Programme auf GitHub. Ein Klick auf „Installieren & starten“ lädt das Programm von der offiziellen Projektseite, richtet es ein und startet es – ohne Admin. Alles lässt sich unter „Programme“ wieder entfernen.") } };
        var q = new TextBox { Name = "oss-query", Text = _ossQuery, PlaceholderText = "z. B. markdown editor", Width = 300 };
        q.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) _ = OssSearch(q.Text ?? ""); };
        sp.Children.Add(new WrapPanel { Children = { q, Act("oss-search", "Suchen", () => _ = OssSearch(q.Text ?? ""), true) } });
        if (_ossHits.Count == 0 && _ossQuery.Length == 0)
            sp.Children.Add(EmptyState("oss-empty", "🐙", "Freie Programme von GitHub", "Links eine Kategorie wählen oder oben ein Thema suchen – Trendo zeigt Sterne, Lizenz und eine verständliche Beschreibung. Fertige Windows-Programme installierst und startest du mit einem Klick."));
        var installed = GitHubApps.List(_deps.ToolsRoot);
        var i = 0;
        foreach (var r in _ossHits)
        {
            var repo = r; var kind = OpenSource.Classify(r.LicenseId);
            var have = installed.FirstOrDefault(x => string.Equals(x.Repo, r.FullName, StringComparison.OrdinalIgnoreCase));
            var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            void Add(Control c) { c.Margin = new Thickness(0, 0, 6, 4); buttons.Children.Add(c); }
            if (have is not null && File.Exists(GitHubApps.ExePath(_deps.ToolsRoot, have)))
            {
                Add(Act($"oss-start-{i}", "Starten", () => Launch(GitHubApps.ExePath(_deps.ToolsRoot, have)), true));
                Add(Act($"oss-manage-{i}", "Verwalten / Entfernen", () => Show("components")));
            }
            else
            {
                var install = Act($"oss-install-{i}", "Installieren & starten", () => _ = GitHubInstall(repo), true);
                var onlySource = _ossAvail.TryGetValue(r.FullName, out var av) && av.Kind != "windows";
                install.IsEnabled = kind != OpenSource.LicenseKind.Unklar && !_depBusy && !onlySource;
                if (onlySource) ToolTip.SetTip(install, "Dieses Projekt hat kein fertiges Windows-Programm – mit „Quellcode holen“ bekommst du den Code.");
                if (!install.IsEnabled) ToolTip.SetTip(install, kind == OpenSource.LicenseKind.Unklar ? "Die Lizenz ist unklar – Trendo installiert es deshalb nicht." : "Gerade wird etwas eingerichtet.");
                if (!onlySource) Add(install);
                SourceButtons(r, i, kind, Add); // T-0314: aus dem Quellcode einrichten oder Alternativen
            }
            var get = Act($"oss-get-{i}", "Quellcode holen", () =>
            {
                var res = OpenSource.Fetch(repo.CloneUrl, OssFolder, repo.FullName, repo.LicenseId);
                _core.Audit.Append(Principal.LocalOwner.Name, "oss.fetch", $"{repo.FullName} exit={res.Exit}");
                Say(res.Ok ? $"„{repo.FullName}“ liegt jetzt im Ordner „Open Source“, mit Herkunftsdatei. Entfernen kannst du es unten auf dieser Seite." : "Herunterladen hat nicht geklappt: " + Short(res.Err), "oss");
            });
            get.IsEnabled = kind != OpenSource.LicenseKind.Unklar;
            Add(get);
            Add(Act($"oss-web-{i}", "Ansehen", () => Launch(repo.Url)));
            var rd = _ossReadme.GetValueOrDefault(r.FullName);
            var guide = ProgramGuide.ForGitHub(r.FullName, r.Description, r.LicenseId, r.Language, r.Stars, null, "das passende Windows-Programm", null, rd.Intro, rd.Features, _ossGerman.GetValueOrDefault(r.FullName));
            var box = GuideBox(guide, $"oss-guide-{i}", open: rd.Intro is not null || rd.Features is { Count: > 0 }); // README geladen (nach dem Aufklappen): offen bleiben
            if (box is Expander ex) ex.Expanding += (_, _) => _ = OssLoadReadme(repo.FullName);
            var explain = Act($"oss-explain-{i}", _ossGerman.ContainsKey(r.FullName) ? "Neu erklären" : "Auf Deutsch erklären (KI)", () => _ = OssExplain(repo));
            explain.IsEnabled = _kiConnected || _opt.Isolated;
            if (!explain.IsEnabled) ToolTip.SetTip(explain, "Dafür muss die lokale KI laufen (KI-Quellen).");
            sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = {
                new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                    new StackPanel { Spacing = 2, Children = {
                        new TextBlock { Text = r.FullName.Split('/')[1], Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold },
                        new TextBlock { Text = $"{r.FullName}  ·  ★ {r.Stars:N0}{(r.Language.Length > 0 ? "  ·  " + r.Language : "")}{(have is not null ? "  ·  ✓ eingerichtet" : "")}", Foreground = Muted, FontSize = 11.5 },
                        P(r.Description, Text, 12.5), OssAvailBadge(r, i), SourceBadge(r, i),
                        P(OpenSource.Explain(r.LicenseId), kind == OpenSource.LicenseKind.Unklar ? Brush.Parse("#FACC15") : null, 11) } },
                    Pos(buttons, 1) } },
                _ossGerman.TryGetValue(r.FullName, out var de) ? new Border { Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(10), Background = Brush.Parse("#1AC9A227"), Child = new StackPanel { Spacing = 3, Children = { new TextBlock { Text = "Auf Deutsch erklärt (KI)", Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold }, P(de, Text, 12) } } } : new Panel(),
                new WrapPanel { Children = { box, explain } } } }));
            i++;
        }
        // ── App-Store-Rahmen (Umbau 04.11.2026, wie Flathub/Microsoft Store): Kategorien links, Treffer in der Mitte, Eigenes rechts ──
        var cats = new StackPanel { Name = "oss-cats", Spacing = 3 };
        cats.Children.Add(new TextBlock { Text = "Kategorien", Foreground = Muted, FontSize = 11.5, Margin = new Thickness(4, 0, 0, 4) });
        foreach (var (n, label, query) in new[] { ("oss-ex-0", "📝 Notizen", "notes app"), ("oss-ex-1", "🖼 Bildbearbeitung", "image editor"), ("oss-ex-2", "🔐 Passwort-Manager", "password manager"),
                     ("oss-ex-3", "🎵 Musik", "music player"), ("oss-ex-4", "📕 PDF", "pdf tool"), ("oss-ex-5", "🎮 Spiele", "open source game"), ("oss-ex-6", "🎬 Video", "video editor"),
                     ("oss-ex-7", "📊 Büro", "office suite"), ("oss-ex-8", "💻 Entwickeln", "code editor"), ("oss-ex-9", "🛡 Sicherheit", "privacy tool"), ("oss-ex-10", "🧊 3D", "3d modeling"), ("oss-ex-11", "📚 Lernen", "flashcards") })
        {
            var qq = query; var on = string.Equals(_ossQuery, query, StringComparison.OrdinalIgnoreCase);
            var b = new Button { Name = n, Content = label, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(8),
                Background = on ? Brush.Parse("#2A2410") : Brushes.Transparent, Foreground = on ? Brush.Parse("#F5B83D") : Text, BorderThickness = new Thickness(0), FontSize = 12.5 };
            b.Click += (_, _) => _ = OssSearch(qq);
            cats.Children.Add(b);
        }
        var side = new StackPanel { Name = "oss-side", Spacing = 8 };
        side.Children.Add(H("Bei dir eingerichtet", 13));
        if (installed.Count == 0) side.Children.Add(P("Noch nichts – „Installieren & starten“ bei einem Treffer.", Muted, 11.5));
        var ii = 0;
        foreach (var app in installed.Take(12))
        {
            var a0 = app; var exe = GitHubApps.ExePath(_deps.ToolsRoot, a0);
            var run = new Button { Name = $"oss-mine-{ii++}", Content = "▶ " + a0.Repo.Split('/').Last(), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, FontSize = 12, IsEnabled = File.Exists(exe) };
            run.Click += (_, _) => Launch(exe);
            side.Children.Add(run);
        }
        if (installed.Count > 0) side.Children.Add(Act("oss-manage-all", "Verwalten / Entfernen", () => Show("components")));
        // Geholte Projekte (Quellcode) – einzeln entfernbar
        var local = Directory.GetDirectories(OssFolder).OrderBy(Path.GetFileName).ToList();
        if (local.Count > 0)
        {
            side.Children.Add(H("Geholte Projekte (Quellcode)", 14));
            var k = 0;
            foreach (var d in local)
            {
                var dir = d;
                side.Children.Add(Card(Row(P(Path.GetFileName(dir), Text, 12.5), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                    Act($"oss-local-open-{k}", "Ordner", () => Launch(dir)),
                    OssRemoveButton(dir, $"oss-local-del-{k}") } })));
                k++;
            }
        }
        side.Children.Add(GitHubTokenBox());
        Border Pane(Control c, string name) => new() { Name = name, Child = new ScrollViewer { Content = c }, Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(10), BoxShadow = CardShadow };
        var store = new Grid { Name = "oss-store", ColumnDefinitions = new ColumnDefinitions("200,*,260"), ColumnSpacing = 10 };
        store.Children.Add(Pane(cats, "oss-left")); store.Children.Add(Pos(Pane(sp, "oss-center"), 1)); store.Children.Add(Pos(Pane(side, "oss-right"), 2));
        return FitKreativPage("Open Source", new Panel(), store);

    }

    /// <summary>Geholtes Projekt entfernen – mit Rückfrage im Knopf; löscht nur Ordner direkt im Ordner „Open Source“.</summary>
    private Button OssRemoveButton(string dir, string name)
    {
        var key = "oss:" + dir; var armed = _confirmRemove.Contains(key);
        var b = new Button { Name = name, Content = armed ? "Wirklich entfernen?" : "Entfernen", Background = armed ? Brush.Parse("#B91C1C") : Brushes.Transparent };
        b.Click += (_, _) =>
        {
            if (!_confirmRemove.Remove(key)) { _confirmRemove.Add(key); Show("oss"); return; }
            var full = Path.GetFullPath(dir);
            if (!string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(OssFolder), StringComparison.OrdinalIgnoreCase)) { Say("Nur Projekte im Ordner „Open Source“ lassen sich hier entfernen.", "oss"); return; }
            try
            {
                foreach (var f in new DirectoryInfo(full).EnumerateFiles("*", SearchOption.AllDirectories)) if (f.IsReadOnly) f.IsReadOnly = false; // Git legt schreibgeschützte Dateien an
                Directory.Delete(full, true);
                _core.Audit.Append(Principal.LocalOwner.Name, "oss.remove", Path.GetFileName(full));
                Say($"„{Path.GetFileName(full)}“ wurde entfernt.", "oss");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say("Entfernen ging nicht – ist ein Programm darin noch geöffnet? " + ex.Message, "oss"); }
        };
        return b;
    }

    public static async Task<IReadOnlyList<OssRepo>> OssSearchHttp(HttpClient http, string query, string? language = null, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, OpenSource.SearchUrl(query, language));
        req.Headers.UserAgent.Add(new ProductInfoHeaderValue("TrendoOS", "1.0"));
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var res = await http.SendAsync(req, ct);
        if ((int)res.StatusCode == 403) throw new InvalidOperationException("GitHub bremst gerade. Versuch es in einer Minute noch mal.");
        res.EnsureSuccessStatusCode();
        return OpenSource.Parse(await res.Content.ReadAsStringAsync(ct));
    }
}
