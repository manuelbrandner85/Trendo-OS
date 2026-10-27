using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Net.Http.Headers;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-06: Offene Projekte von GitHub.</summary>
public partial class MainWindow
{
    private IReadOnlyList<OssRepo> _ossHits = [];
    private string _ossQuery = "";
    public string OssFolder => WorkFolder("Open Source");
    public Func<string, Task<IReadOnlyList<OssRepo>>>? OssSearchOverride { get; set; }
    private static readonly HttpClient OssHttp = new() { Timeout = TimeSpan.FromSeconds(20) };

    private void InitW4Oss() => RegisterTab(KreativTabs, "oss", "Open Source", "X-06", OssView, "github", "open source", "quellcode", "lizenz");

    public async Task OssSearch(string q)
    {
        _ossQuery = q;
        if (_core.Offline) { Say("Trendo ist offline. Die Suche braucht Internet.", "oss"); return; }
        try { _ossHits = OssSearchOverride is not null ? await OssSearchOverride(q) : await OssSearchHttp(OssHttp, q); _w45Msg = _ossHits.Count == 0 ? "Nichts gefunden." : ""; }
        catch (Exception ex) { _ossHits = []; _w45Msg = "Suche hat nicht geklappt: " + ex.Message; }
        _core.Audit.Append(Principal.LocalOwner.Name, "oss.search", q);
        Show("oss");
    }

    private Control OssView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Finde freie Projekte auf GitHub. Trendo zeigt dir zu jedem Projekt, was du damit machen darfst.") } };
        var q = new TextBox { Name = "oss-query", Text = _ossQuery, PlaceholderText = "z. B. markdown editor", Width = 300 };
        sp.Children.Add(new WrapPanel { Children = { q, Act("oss-search", "Suchen", () => _ = OssSearch(q.Text ?? ""), true) } });
        if (_ossHits.Count == 0 && _ossQuery.Length == 0)
            sp.Children.Add(EmptyState("oss-empty", "🐙", "Freie Programme von GitHub", "Suche nach einem Thema – Trendo zeigt Sterne, Lizenz und eine verständliche Beschreibung. Fertige Windows-Programme installierst du mit einem Klick.",
                Chip("oss-ex-0", "Notizen", () => _ = OssSearch("notes app")), Chip("oss-ex-1", "Bildbearbeitung", () => _ = OssSearch("image editor")),
                Chip("oss-ex-2", "Passwort-Manager", () => _ = OssSearch("password manager")), Chip("oss-ex-3", "Musik", () => _ = OssSearch("music player")),
                Chip("oss-ex-4", "PDF", () => _ = OssSearch("pdf tool")), Chip("oss-ex-5", "Spiele", () => _ = OssSearch("open source game"))));
        var i = 0;
        foreach (var r in _ossHits)
        {
            var repo = r; var kind = OpenSource.Classify(r.LicenseId);
            var get = Act($"oss-get-{i}", "Herunterladen", () =>
            {
                var res = OpenSource.Fetch(repo.CloneUrl, OssFolder, repo.FullName, repo.LicenseId);
                _core.Audit.Append(Principal.LocalOwner.Name, "oss.fetch", $"{repo.FullName} exit={res.Exit}");
                Say(res.Ok ? $"„{repo.FullName}“ liegt jetzt im Ordner „Open Source“, mit Herkunftsdatei." : "Herunterladen hat nicht geklappt: " + Short(res.Err), "oss");
            });
            get.IsEnabled = kind != OpenSource.LicenseKind.Unklar;
            // Vorgabe 2026-10-26: fertiges Windows-Programm direkt auf Trendo OS installieren (Klick = Zustimmung, Lizenz steht daneben)
            var install = Act($"oss-install-{i}", "Zustimmen & installieren", () => _ = GitHubInstall(repo), true);
            install.IsEnabled = kind != OpenSource.LicenseKind.Unklar && !_depBusy;
            var guide = ProgramGuide.ForGitHub(r.FullName, r.Description, r.LicenseId, r.Language, r.Stars, null, "das passende Windows-Programm", null);
            sp.Children.Add(Card(new StackPanel { Spacing = 4, Children = {
                Row(new StackPanel { Children = {
                    new TextBlock { Text = $"{r.FullName}  ★ {r.Stars:N0}", Foreground = Text, FontSize = 12.5 },
                    P(r.Description, null, 11), P(OpenSource.Explain(r.LicenseId), kind == OpenSource.LicenseKind.Unklar ? Brush.Parse("#FACC15") : null, 10.5) } },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { Act($"oss-web-{i}", "Ansehen", () => Launch(repo.Url)), get, install } }),
                GuideBox(guide, $"oss-guide-{i}") } }));
            i++;
        }
        var local = Directory.GetDirectories(OssFolder);
        if (local.Length > 0) sp.Children.Add(P($"Schon geholt: {string.Join(", ", local.Select(Path.GetFileName))}", null, 11));
        return KreativPage("Open Source", sp);
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
