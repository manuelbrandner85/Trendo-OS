using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0735 (SYS-08): Suche mit Trefferliste über öffentliche Suchdienste ohne Konto (Wikipedia-Schnittstelle, DuckDuckGo-HTML).
/// Abruf nur über den vorhandenen Lese-Weg <see cref="ReadOnlyWeb"/> (öffentliche Adressen, keine Formulare, höchstens 2 MB);
/// jeder Treffer zeigt seine Herkunft (Dienst, Suchadresse, Uhrzeit). Treffer öffnen sich im Lesemodus.</summary>
public partial class MainWindow
{
    /// <summary>Tests: ersetzt den Abruf der Suche. Standard ist der echte Lese-Weg.</summary>
    public Func<Uri, Task<(string Type, byte[] Bytes, Uri Final)>>? ResearchSearchFetchOverride { get; set; }
    private List<WebSearchHit> _rsHits = [];
    private string _rsEngine = "wikipedia", _rsQuery = "";
    public IReadOnlyList<WebSearchHit> ResearchHits => _rsHits;

    public async Task ResearchSearchAsync(string engine, string query)
    {
        _rsEngine = ResearchSearch.Engines.Any(e => e.Id == engine) ? engine : "wikipedia"; _rsQuery = query ?? "";
        if (_core.Offline) { _rsMsg = "Offline-Modus: Trendo geht nicht ins Internet."; return; }
        Uri url;
        try { url = ResearchSearch.QueryUrl(_rsEngine, _rsQuery); }
        catch (ArgumentException ex) { _rsMsg = ex.Message; return; }
        try
        {
            var (_, bytes, _) = await (ResearchSearchFetchOverride ?? (u => ReadOnlyWeb.GetAsync(u)))(url);
            var at = DateTimeOffset.Now;
            var hits = ResearchSearch.Parse(_rsEngine, System.Text.Encoding.UTF8.GetString(bytes), url, at);
            Desk.RecordSearch(_rsEngine, _rsQuery, url, bytes, hits);
            _rsHits = hits.ToList(); ReaderShown = null;
            _rsMsg = hits.Count == 0 ? $"Keine Treffer bei {ResearchSearch.EngineName(_rsEngine)}. Versuch ein anderes Wort." : $"{hits.Count} Treffer bei {ResearchSearch.EngineName(_rsEngine)} · {at:HH:mm}";
        }
        catch (Exception ex) { _rsMsg = "Suche hat nicht geklappt: " + Trendo.Core.FriendlyError.Say(ex); }
    }

    /// <summary>Suchzeile oben: Dienst wählen, Begriff eingeben, „Suchen“ (Hauptaktion der Ansicht).</summary>
    private Control ResearchSearchBar()
    {
        var engine = new ComboBox { Name = "rs-engine", ItemsSource = ResearchSearch.Engines.Select(e => e.Name).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(ResearchSearch.Engines, e => e.Id == _rsEngine)), Width = 210, Margin = new Thickness(0, 0, 8, 0) };
        ToolTip.SetTip(engine, "Öffentliche Suche ohne Konto. Die Herkunft steht an jedem Treffer.");
        var q = new TextBox { Name = "rs-query", PlaceholderText = "Suchen, z. B. Phishing erkennen", Text = _rsQuery };
        var go = new Button { Name = "rs-search", Content = "Suchen", Background = Accent, IsEnabled = !_core.Offline, Margin = new Thickness(8, 0, 0, 0) };
        async Task Run() { go.IsEnabled = false; await ResearchSearchAsync(ResearchSearch.Engines[Math.Max(0, engine.SelectedIndex)].Id, q.Text ?? ""); Show("recherche"); }
        go.Click += async (_, _) => await Run();
        q.KeyDown += async (_, e) => { if (e.Key == Avalonia.Input.Key.Enter && go.IsEnabled) await Run(); };
        return new DockPanel { Children = { Dk(engine, Avalonia.Controls.Dock.Left), Dk(go, Avalonia.Controls.Dock.Right), q } };
    }

    /// <summary>Trefferliste in der Mitte: Titel, Auszug, Adresse und Herkunft je Treffer; Klick liest die Seite im Lesemodus.</summary>
    private Control ResearchHitsView()
    {
        var sp = new StackPanel { Name = "rs-hits", Spacing = 10 };
        var first = _rsHits[0];
        sp.Children.Add(new TextBlock { Text = $"Treffer für „{_rsQuery}“", Foreground = Text, FontSize = 20, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(new SelectableTextBlock { Name = "rs-hits-origin", Text = $"Herkunft: {first.Engine} · {first.At:dd.MM.yyyy HH:mm} · {first.QueryUrl}", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap });
        var i = 0;
        foreach (var h in _rsHits)
        {
            var hu = h.Url;
            var b = new Button { Name = $"rs-hit-{i++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(8, 6),
                Content = new StackPanel { Spacing = 2, Children = {
                    new TextBlock { Text = h.Title, Foreground = Brush.Parse("#E8C766"), FontSize = 15, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = hu, Foreground = Muted, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = h.Snippet, Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, IsVisible = h.Snippet.Length > 0 },
                    new TextBlock { Text = "Quelle: " + h.Engine, Foreground = Muted, FontSize = 10.5 } } } };
            ToolTip.SetTip(b, "Im Lesemodus öffnen – nur Text, keine Formulare, keine Downloads");
            b.Click += async (_, _) => { await ReadUrlAsync(hu); Show("recherche"); }; Hover(b);
            sp.Children.Add(b);
        }
        return new ScrollViewer { Content = new Border { Padding = new Thickness(26, 18), Child = sp } };
    }

    /// <summary>Kompakte Trefferliste links, solange eine Seite offen ist.</summary>
    private void ResearchHitsSide(StackPanel left)
    {
        if (_rsHits.Count == 0) return;
        left.Children.Add(H("Treffer", 13));
        left.Children.Add(P($"{_rsHits[0].Engine} · „{_rsQuery}“", Muted, 10.5));
        var i = 0;
        foreach (var h in _rsHits.Take(10))
        {
            var hu = h.Url;
            var b = new Button { Name = $"rs-side-hit-{i++}", Content = new TextBlock { Text = h.Title, Foreground = Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis }, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = hu == _rsShownUrl ? Brush.Parse("#26D4A84B") : Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(8, 3) };
            ToolTip.SetTip(b, hu); b.Click += async (_, _) => { await ReadUrlAsync(hu); Show("recherche"); }; Hover(b); left.Children.Add(b);
        }
        var back = new Button { Name = "rs-hits-back", Content = "Alle Treffer zeigen", Padding = new Thickness(8, 3), Margin = new Thickness(0, 2, 0, 0) };
        back.Click += (_, _) => { ReaderShown = null; Show("recherche"); };
        left.Children.Add(back);
        left.Children.Add(new Border { Height = 8 });
    }

    /// <summary>Schalter „Sammlung behalten“: aus = flüchtig wie bisher, an = bleibt über einen Neustart.</summary>
    private Control ResearchKeepToggle()
    {
        var keep = new CheckBox { Name = "rs-keep", Content = "Sammlung behalten (auch nach Neustart)", IsChecked = Desk.Keep, Foreground = Text, FontSize = 12 };
        ToolTip.SetTip(keep, "Aus: jede Sitzung beginnt leer. An: Quellen, Suchen, Markierungen und Notizen bleiben auf diesem PC.");
        keep.IsCheckedChanged += (_, _) => { Desk.SetKeep(keep.IsChecked == true); _rsMsg = Desk.Keep ? "Die Sammlung bleibt jetzt auch nach einem Neustart." : "Die Sammlung ist wieder flüchtig – beim nächsten Start ist sie leer."; Show("recherche"); };
        return keep;
    }
}
