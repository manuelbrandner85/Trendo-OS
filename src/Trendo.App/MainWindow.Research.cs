using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SYS-08: Recherche im Lesemodus – nur lesen, nie Formulare, Downloads oder Anmeldung. .onion nur über den offiziellen Tor Browser.</summary>
public partial class MainWindow
{
    private ResearchDesk? _desk;
    public ResearchDesk Desk => _desk ??= new ResearchDesk(_core.DataRoot, _core.Audit);
    public Func<Uri, Task<(string Type, string Body, Uri Final)>>? ReaderFetchOverride { get; set; }
    public ReaderPage? ReaderShown { get; private set; }
    private string _rsMsg = "", _rsUrl = "", _rsTrans = "";

    private void InitSys08()
    {
        _showMap["recherche"] = ResearchView; SysTabs.Add(("recherche", "Recherche (Lesemodus)"));
        StudioPages["SYS-08"] = "recherche";
        ExtraPages.Add(("Recherche im Lesemodus", "recherche", ["recherche", "dark web", "deep web", "tor", "onion", "lesen", "quelle"]));
        Desk.EndSession(); // flüchtig: jede App-Sitzung beginnt leer
    }

    private static async Task<(string, string, Uri)> FetchReadOnly(Uri u)
    {
        var (type, bytes, final) = await ReadOnlyWeb.GetPageAsync(u);
        return (type, System.Text.Encoding.UTF8.GetString(bytes), final);
    }

    public async Task ReadUrlAsync(string input)
    {
        _rsUrl = input; _rsTrans = "";
        var v = ResearchDesk.Classify(input);
        if (v.Kind != UrlKind.Lesemodus) { _rsMsg = v.Reason; ReaderShown = null; return; }
        if (_core.Offline) { _rsMsg = "Offline-Modus: Trendo geht nicht ins Internet."; return; }
        try
        {
            var (type, body, final) = await (ReaderFetchOverride ?? FetchReadOnly)(v.Uri!);
            ReaderShown = type == "text/plain" ? new ReaderPage(final.Host, body, []) : ResearchDesk.ToReader(body, final);
            Desk.Record(final, ReaderShown, body);
            _rsMsg = $"Gelesen: {final.Host} · {ReaderShown.Text.Length} Zeichen · nichts wurde gespeichert außer in dieser Sitzung.";
        }
        catch (Exception ex) { ReaderShown = null; _rsMsg = "Konnte die Seite nicht lesen: " + ex.Message; }
    }

    private Control ResearchView()
    {
        var sp = new StackPanel { Spacing = 6, Children = { P("Lesemodus: Trendo holt nur den Text einer Seite. Keine Anmeldung, keine Formulare, keine Downloads, keine Käufe. Seiten mit .onion öffnest du nur im offiziellen Tor Browser.") } };
        var url = new TextBox { Name = "rs-url", PlaceholderText = "Adresse, z. B. de.wikipedia.org/wiki/Tor_(Netzwerk)", Text = _rsUrl, Width = 420 };
        var go = new Button { Name = "rs-read", Content = "Lesen", Background = Accent, IsEnabled = !_core.Offline };
        go.Click += async (_, _) => { go.IsEnabled = false; await ReadUrlAsync(url.Text ?? ""); Show("recherche"); };
        sp.Children.Add(new WrapPanel { Children = { MSp(url), MSp(go) } });
        var v = _rsUrl == "" ? null : ResearchDesk.Classify(_rsUrl);
        if (v?.Kind == UrlKind.NurTorBrowser)
        {
            var tb = new Button { Name = "rs-tor", Content = "Tor Browser holen (offizielle Seite)" };
            tb.Click += (_, _) => Launch("https://www.torproject.org/de/download/");
            sp.Children.Add(tb);
        }
        sp.Children.Add(new TextBlock { Name = "rs-msg", Text = _rsMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        if (ReaderShown is { } p)
        {
            var tr = new Button { Name = "rs-translate", Content = "Auf Deutsch (KI auf diesem PC)" };
            tr.Click += async (_, _) => { tr.IsEnabled = false; using var kiLabel = Trendo.Core.KiActivityHub.Label("Recherche"); var a = await Intel.AskAsync(new KiRequest("Übersetze ins Deutsche. Gib nur die Übersetzung aus:\n\n" + (p.Text.Length > 4000 ? p.Text[..4000] : p.Text), PreferredModel: KiModelName, UseKnowledge: false)); _rsTrans = a.Text; Show("recherche"); };
            var body = new StackPanel { Spacing = 4, Children = { H(p.Title, 15), tr } };
            if (_rsTrans != "") body.Children.Add(new SelectableTextBlock { Name = "rs-trans", Text = _rsTrans, Foreground = Text, TextWrapping = TextWrapping.Wrap });
            body.Children.Add(new SelectableTextBlock { Name = "rs-text", Text = p.Text.Length > 20000 ? p.Text[..20000] + "\n…" : p.Text, Foreground = Text, TextWrapping = TextWrapping.Wrap, FontSize = 12.5 });
            if (p.Links.Count > 0)
            {
                body.Children.Add(H("Links auf der Seite", 13)); var i = 0;
                foreach (var (t, h) in p.Links.Take(25))
                {
                    var b = new Button { Name = $"rs-link-{i++}", Content = t.Length > 70 ? t[..70] + "…" : t, Padding = new Thickness(6, 1), FontSize = 11 };
                    ToolTip.SetTip(b, h); b.Click += async (_, _) => { await ReadUrlAsync(h); Show("recherche"); };
                    body.Children.Add(b);
                }
            }
            sp.Children.Add(WCard(body));
        }
        sp.Children.Add(H("Gute Startpunkte", 14));
        var wp = new WrapPanel();
        foreach (var (n, u, why) in ResearchDesk.Sources) { var b = new Button { Content = n }; ToolTip.SetTip(b, why); b.Click += async (_, _) => { await ReadUrlAsync(u); Show("recherche"); }; wp.Children.Add(MSp(b)); }
        sp.Children.Add(wp);
        sp.Children.Add(H("Diese Sitzung", 14));
        var srcs = Desk.SessionSources();
        sp.Children.Add(P(srcs.Count == 0 ? "Noch nichts gelesen." : string.Join("\n", srcs.Select(s => $"{s.At:HH:mm} {s.Title} – {s.Url}")), null, 11));
        var note = new TextBox { Name = "rs-note", PlaceholderText = "Notizen zur Recherche", Text = Desk.Note(), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 50 };
        var save = new Button { Name = "rs-export", Content = "Als Bericht speichern" };
        save.Click += (_, _) => { Desk.SaveNote(note.Text ?? ""); var f = Desk.ExportReport(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Trendo Recherche")); _rsMsg = "Gespeichert: " + f; Show("recherche"); };
        var end = new Button { Name = "rs-end", Content = "Sitzung beenden (alles löschen)" };
        end.Click += (_, _) => { Desk.EndSession(); ReaderShown = null; _rsUrl = ""; _rsTrans = ""; _rsMsg = "Sitzung beendet. Quellen und Notizen sind gelöscht."; Show("recherche"); };
        sp.Children.Add(note); sp.Children.Add(new WrapPanel { Children = { MSp(save), MSp(end) } });
        return SysPage("Recherche (Lesemodus)", sp);
    }
}
