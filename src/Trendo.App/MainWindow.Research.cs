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
    private string _rsMsg = "", _rsUrl = "", _rsTrans = "", _rsShownUrl = "", _rsStyle = "einfach";
    public string ReaderUrl => _rsShownUrl;
    SelectableTextBlock? _rsTextBlock;
    private bool _rsWholePage; private string _rsRaw = ""; private Uri? _rsFinal;

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
            ReaderShown = type == "text/plain" ? new ReaderPage(final.Host, body, []) : ResearchDesk.ToReader(body, final, !_rsWholePage);
            _rsRaw = type == "text/plain" ? "" : body; _rsFinal = final;
            Desk.Record(final, ReaderShown, body); _rsShownUrl = final.ToString();
            _rsMsg = $"Gelesen: {final.Host} · {ReaderShown.Text.Length} Zeichen · nichts wurde gespeichert außer in dieser Sitzung.";
        }
        catch (Exception ex) { ReaderShown = null; _rsMsg = "Konnte die Seite nicht lesen: " + Trendo.Core.FriendlyError.Say(ex); }
    }

    /// <summary>Zwischen „nur der Artikel“ und „ganze Seite“ wechseln, ohne die Seite neu zu laden (öffentlich für Tests).</summary>
    public void ReaderToggleWholePage()
    {
        _rsWholePage = !_rsWholePage;
        if (_rsRaw.Length > 0 && _rsFinal is not null) ReaderShown = ResearchDesk.ToReader(_rsRaw, _rsFinal, !_rsWholePage);
    }

    private WrapPanel ReaderBar(Button translate)
    {
        var bar = new WrapPanel { Children = { MSp(translate) } };
        if (_rsRaw.Length == 0) return bar;
        var toggle = new Button { Name = "rs-whole", Content = _rsWholePage ? "Nur den Artikel zeigen" : "Ganze Seite zeigen" };
        toggle.Click += (_, _) => { ReaderToggleWholePage(); Show("recherche"); };
        bar.Children.Add(MSp(toggle));
        if (!_rsWholePage && ReaderShown is { IsArticle: false }) bar.Children.Add(new TextBlock { Name = "rs-noarticle", Text = "Kein einzelner Artikel erkannt – die ganze Seite wird gezeigt.", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
        return bar;
    }

    /// <summary>Recherche als Lese-Browser (Clean-Room): oben Adresse, links Startpunkte und Quellen der Sitzung, Mitte der Lesebereich,
    /// rechts Markierungen, Zitate und Notizen. Nur Text, keine Anmeldung, keine Formulare, keine Downloads.</summary>
    private Control ResearchView()
    {
        var top = new StackPanel { Spacing = 6 };
        var url = new TextBox { Name = "rs-url", PlaceholderText = "Adresse, z. B. de.wikipedia.org/wiki/Tor_(Netzwerk)", Text = _rsUrl };
        var go = new Button { Name = "rs-read", Content = "Lesen", Background = Accent, IsEnabled = !_core.Offline, Margin = new Thickness(8, 0, 0, 0) };
        go.Click += async (_, _) => { go.IsEnabled = false; await ReadUrlAsync(url.Text ?? ""); Show("recherche"); };
        url.KeyDown += async (_, e) => { if (e.Key == Avalonia.Input.Key.Enter && go.IsEnabled) { go.IsEnabled = false; await ReadUrlAsync(url.Text ?? ""); Show("recherche"); } };
        top.Children.Add(new DockPanel { Children = { Dk(go, Avalonia.Controls.Dock.Right), url } });
        top.Children.Add(P("Lesemodus: Trendo holt nur den Text einer Seite. Keine Anmeldung, keine Formulare, keine Downloads, keine Käufe. Seiten mit .onion öffnest du nur im offiziellen Tor Browser.", Muted, 11.5));
        var v = _rsUrl == "" ? null : ResearchDesk.Classify(_rsUrl);
        if (v?.Kind == UrlKind.NurTorBrowser)
        {
            var tb = new Button { Name = "rs-tor", Content = "Tor Browser holen (offizielle Seite)" };
            tb.Click += (_, _) => Launch("https://www.torproject.org/de/download/");
            top.Children.Add(tb);
        }
        top.Children.Add(new TextBlock { Name = "rs-msg", Text = _rsMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap, IsVisible = _rsMsg.Length > 0 });

        // links: Startpunkte und Quellen
        var left = new StackPanel { Spacing = 3 };
        left.Children.Add(H("Gute Startpunkte", 13));
        foreach (var (n, u, why) in ResearchDesk.Sources)
        {
            var b = new Button { Content = n, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(8, 4) };
            ToolTip.SetTip(b, why); b.Click += async (_, _) => { await ReadUrlAsync(u); Show("recherche"); }; Hover(b); left.Children.Add(b);
        }
        left.Children.Add(new Border { Height = 8 });
        left.Children.Add(H("Diese Sitzung", 13));
        var srcs = Desk.SessionSources();
        if (srcs.Count == 0) left.Children.Add(P("Noch nichts gelesen.", Muted, 11.5));
        var si = 0;
        foreach (var src in srcs)
        {
            var su = src.Url;
            var b = new Button { Name = $"rs-src-{si++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = su == _rsShownUrl ? Brush.Parse("#26D4A84B") : Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(8, 4),
                Content = new StackPanel { Children = { new TextBlock { Text = src.Title, Foreground = Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis }, new TextBlock { Text = $"{src.At:HH:mm} · {su}", Foreground = Muted, FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis } } } };
            b.Click += async (_, _) => { await ReadUrlAsync(su); Show("recherche"); }; Hover(b); left.Children.Add(b);
        }

        // Mitte: Lesebereich
        Control center;
        if (ReaderShown is { } p)
        {
            var tr = new Button { Name = "rs-translate", Content = "Auf Deutsch (KI auf diesem PC)" };
            tr.Click += async (_, _) => { tr.IsEnabled = false; using var kiLabel = Trendo.Core.KiActivityHub.Label("Recherche"); var a = await Intel.AskAsync(new KiRequest("Übersetze ins Deutsche. Gib nur die Übersetzung aus:\n\n" + (p.Text.Length > 4000 ? p.Text[..4000] : p.Text), PreferredModel: KiModelName, UseKnowledge: false)); _rsTrans = a.Text; Show("recherche"); };
            var body = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = p.Title, Foreground = Text, FontSize = 22, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap }, ReaderBar(tr) } };
            if (_rsTrans != "") body.Children.Add(new SelectableTextBlock { Name = "rs-trans", Text = _rsTrans, Foreground = Text, TextWrapping = TextWrapping.Wrap });
            body.Children.Add(ResearchMarkBar(p));
            var shownText = p.Text.Length > 20000 ? p.Text[..20000] : p.Text;
            var marks = Desk.Highlights(_rsShownUrl);
            var reader = new SelectableTextBlock { Name = "rs-text", Foreground = Text, TextWrapping = TextWrapping.Wrap, FontSize = 15, LineHeight = 24, SelectionBrush = Brush.Parse("#66C9A227"), MaxWidth = 760 };
            if (marks.Count == 0 && !shownText.Contains("## ")) reader.Text = shownText + (p.Text.Length > 20000 ? "\n…" : "");
            else reader.Inlines = ReaderInlines(shownText, marks);
            _rsTextBlock = reader;
            body.Children.Add(reader);
            if (p.Links.Count > 0)
            {
                body.Children.Add(H("Links auf der Seite", 13)); var i = 0;
                var links = new WrapPanel();
                foreach (var (t, h) in p.Links.Take(25))
                {
                    var b = new Button { Name = $"rs-link-{i++}", Content = t.Length > 70 ? t[..70] + "…" : t, Padding = new Thickness(6, 1), FontSize = 11, Margin = new Thickness(0, 0, 4, 4) };
                    ToolTip.SetTip(b, h); b.Click += async (_, _) => { await ReadUrlAsync(h); Show("recherche"); };
                    links.Children.Add(b);
                }
                body.Children.Add(links);
            }
            center = new ScrollViewer { Content = new Border { Padding = new Thickness(26, 18), Child = body } };
        }
        else center = new StackPanel { Name = "rs-empty", Spacing = 10, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Children = {
            new TextBlock { Text = "📖", FontSize = 52, HorizontalAlignment = HorizontalAlignment.Center },
            new TextBlock { Text = "Ruhig lesen, markieren, zitieren", Foreground = Text, FontSize = 20, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
            P("Oben eine Adresse eingeben oder links einen Startpunkt wählen. Markierte Stellen werden rechts zu Zitaten mit Quelle.", Muted, 12.5) } };

        // rechts: Markierungen, Zitate, Notizen
        var right = new StackPanel { Spacing = 8 };
        if (ReaderShown is not null) right.Children.Add(ResearchMarksSide());
        right.Children.Add(ResearchQuotes());
        var note = new TextBox { Name = "rs-note", PlaceholderText = "Notizen zur Recherche", Text = Desk.Note(), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90 };
        var save = new Button { Name = "rs-export", Content = "Als Bericht speichern" };
        save.Click += (_, _) => { Desk.SaveNote(note.Text ?? ""); var f = Desk.ExportReport(ResearchReportFolder ?? Path.Combine(UserFolder(Environment.SpecialFolder.MyDocuments), "Trendo Recherche"), _rsStyle); _rsMsg = "Gespeichert: " + f; Show("recherche"); };
        var end = new Button { Name = "rs-end", Content = "Sitzung beenden (alles löschen)" };
        end.Click += (_, _) => { Desk.EndSession(); ReaderShown = null; _rsUrl = ""; _rsTrans = ""; _rsMsg = "Sitzung beendet. Quellen, Notizen und Markierungen sind gelöscht."; Show("recherche"); };
        right.Children.Add(H("Notizen", 13)); right.Children.Add(note); right.Children.Add(new WrapPanel { Children = { MSp(save), MSp(end) } });

        var browser = new Grid { Name = "rs-browser", ColumnDefinitions = new ColumnDefinitions("250,12,*,12,330"), Children = {
            Card(new ScrollViewer { Content = left }),
            Pos(new Border { Background = Brush.Parse("#0B1424"), CornerRadius = new CornerRadius(12), MinHeight = 460, Child = center }, 2),
            Pos(Card(new ScrollViewer { Content = right }), 4) } };
        return SysPage("Recherche (Lesemodus)", new StackPanel { Spacing = 8, Children = { Card(top), browser } });
    }
}
