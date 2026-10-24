using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0218: „Alle Studios“ nach Referenzbild 2 – Eingabezeile, Bereichs-Chips, Bildkarten je Studio (lokal mit KI erzeugt),
/// Systemmodule, aktuelle Projekte und rechts KI, Geräte, KI-Team und GitHub. Nur echte Zustände.</summary>
public partial class MainWindow
{
    private double? _lastCpu, _lastRam;
    private static readonly Dictionary<string, Bitmap?> StudioPictures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Kartenbild aus Assets/studios/&lt;ID&gt;.jpg (Herkunft docs/design/STUDIOBILDER.json); fehlt es, bleibt die Farbfläche.</summary>
    internal static Bitmap? StudioPicture(string id)
    {
        lock (StudioPictures)
        {
            if (StudioPictures.TryGetValue(id, out var cached)) return cached;
            Bitmap? bmp = null;
            try { using var st = AssetLoader.Open(new Uri($"avares://TrendoOS/Assets/studios/{id}.jpg")); bmp = Bitmap.DecodeToWidth(st, 384); }
            catch (Exception ex) when (ex is FileNotFoundException or IOException or ArgumentException or InvalidOperationException) { bmp = null; }
            StudioPictures[id] = bmp;
            return bmp;
        }
    }

    private Control StudiosView()
    {
        var list = _studios.Where(StudioHit).ToList();
        var main = new StackPanel { Spacing = 12 };

        // Eingabezeile: Enter sucht in Studios, Seiten und Dateien
        var ask = new TextBox { Name = "studios-ask", PlaceholderText = "Was möchtest du heute erstellen?", Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Text, FontSize = 19, VerticalContentAlignment = VerticalAlignment.Center };
        void Go() { var q = ask.Text?.Trim() ?? ""; if (q.Length == 0) return; _query = q; Show("suche"); }
        ask.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Go(); } };
        var goBtn = new Button { Name = "studios-ask-go", Width = 38, Height = 38, CornerRadius = new CornerRadius(19), Padding = new Thickness(0), Background = Brush.Parse("#332F86F6"), BorderBrush = Accent, BorderThickness = new Thickness(1), Content = Ico(Mdi.ChevronRight, 20, Text), VerticalAlignment = VerticalAlignment.Center };
        Avalonia.Automation.AutomationProperties.SetName(goBtn, "Suchen"); ToolTip.SetTip(goBtn, "Suchen");
        goBtn.Click += (_, _) => { if (string.IsNullOrWhiteSpace(ask.Text)) ask.Focus(); else Go(); };
        var orb = new Border { Width = 46, Height = 46, CornerRadius = new CornerRadius(23), Background = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#E6F4FF"), 0), new GradientStop(Color.Parse("#3BA7FF"), 0.35), new GradientStop(Color.Parse("#0B2A6B"), 1) } },
            BoxShadow = BoxShadows.Parse("0 0 22 2 #883BA7FF"), VerticalAlignment = VerticalAlignment.Center };
        var askRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), Children = {
            orb, Pos(new TextBlock { Text = "Trendo –", Foreground = Text, FontSize = 21, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 8, 0) }, 1), Pos(ask, 2), Pos(goBtn, 3) } };
        main.Children.Add(new Border { Name = "studios-ask-bar", CornerRadius = new CornerRadius(34), Padding = new Thickness(10, 8, 12, 8), Background = Brush.Parse(C("#B30A1530", "#E6FFFFFF")), BorderBrush = Brush.Parse("#995AA9FF"), BorderThickness = new Thickness(1.5),
            BoxShadow = BoxShadows.Parse("0 0 26 0 #553BA7FF"), MaxWidth = 860, HorizontalAlignment = HorizontalAlignment.Stretch, Child = askRow });

        // Bereichs-Chips (Filter)
        var filters = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        void ChipFilter(string? area, string label)
        {
            var on = string.IsNullOrWhiteSpace(area) ? string.IsNullOrWhiteSpace(_studioFilter) : string.Equals(_studioFilter, area, StringComparison.OrdinalIgnoreCase);
            var b = new Button { Name = "studio-filter-" + StudioFilterId(area), Content = label, Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(14, 5), CornerRadius = new CornerRadius(14), FontSize = 12.5, Foreground = Text,
                Background = on ? Accent : Brush.Parse(C("#66101F3D", "#CCFFFFFF")), BorderBrush = Brush.Parse("#665AA9FF"), BorderThickness = new Thickness(on ? 0 : 1) };
            b.Click += (_, _) => { _studioFilter = string.IsNullOrWhiteSpace(area) ? null : area; _studioQuery = ""; Show("studios"); };
            filters.Children.Add(b);
        }
        ChipFilter(null, "Alle");
        foreach (var area in StudioCatalog.Areas) ChipFilter(area, area);
        main.Children.Add(filters);

        // Kopf „Studios“ mit Suche
        var search = new TextBox { Name = "txt-studio-search", Text = _studioQuery, PlaceholderText = "Studio suchen", Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Text, FontSize = 12.5, MinWidth = 180 };
        search.TextChanged += (_, _) =>
        {
            if (_studioRebuild > 0) return;
            var next = search.Text ?? "";
            if (next == _studioQuery) return;
            _studioQuery = next;
            _studioRebuild++;
            try { Show("studios"); }
            finally { _studioRebuild--; }
        };
        var count = new TextBlock { Name = "studios-count", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 2, 0, 0),
            Text = _studios.Count == 0 ? "Die Studio-Liste wurde nicht gefunden." : string.IsNullOrWhiteSpace(_studioFilter) && _studioQuery.Trim().Length == 0 ? $"{list.Count} Studios – alle kreativen und technischen Studios auf einen Blick" : $"{list.Count} Studios in dieser Ansicht" };
        if (_studios.Count == 0) count.Foreground = Brushes.OrangeRed;
        var searchBox = new Border { CornerRadius = new CornerRadius(10), BorderBrush = Edge, BorderThickness = new Thickness(1), Background = Brush.Parse(C("#55202C48", "#CCFFFFFF")), Padding = new Thickness(10, 4),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Ico(Mdi.Magnify, 14, Muted), search } } };
        main.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), Children = {
            Ico(Mdi.ViewGridOutline, 20, Text), Pos(new TextBlock { Text = "Studios", Foreground = Text, FontSize = 19, FontWeight = FontWeight.SemiBold, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }, 1),
            Pos(count, 2), Pos(searchBox, 3) } });

        // Bildkarten, nach Bereichen gruppiert
        var groups = list.GroupBy(s => StudioCatalog.For(s.Id)?.Area ?? "Weitere")
            .OrderBy(g => Array.IndexOf(StudioCatalog.Areas, g.Key) is var k && k < 0 ? 99 : k);
        foreach (var g in groups)
        {
            var head = new TextBlock { Name = "studio-area-" + g.Key, Text = g.Key, Foreground = Muted, FontSize = 12, FontWeight = FontWeight.SemiBold, Margin = new Thickness(2, 4, 0, 0) };
            var wrap = new WrapPanel();
            foreach (var st in g) wrap.Children.Add(StudioCard(st));
            main.Children.Add(new StackPanel { Spacing = 4, Children = { head, wrap } });
        }
        if (list.Count == 0 && _studios.Count > 0) main.Children.Add(P("Kein Studio passt zu dieser Suche.", Text));

        main.Children.Add(SystemModulesRow());
        main.Children.Add(RecentProjectsRow());

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,290") };
        body.Children.Add(main);
        var side = StudiosSide(); side.Margin = new Thickness(16, 0, 0, 0); Grid.SetColumn(side, 1); body.Children.Add(side);
        return Page("Alle Studios", body);
    }

    /// <summary>Bildkarte: KI-Bild, Studio-Symbol, Name und Satz; Klick öffnet das Studio im eigenen Fenster.</summary>
    private Button StudioCard(StudioEntry st)
    {
        var info = StudioCatalog.For(st.Id);
        var name = info?.Name ?? st.Name;
        var si = StudioIcons.For(st.Id);
        var layers = new Grid { Height = 112, ClipToBounds = true };
        if (StudioPicture(st.Id) is { } pic)
            layers.Children.Add(new Image { Source = pic, Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        else
            layers.Children.Add(new Border { Background = si is null ? Grad("#1B3150", "#060D1A") : Grad(si.C1, si.C2), Opacity = 0.55 });
        layers.Children.Add(new Border { Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#00050B18"), 0.25), new GradientStop(Color.Parse("#E6050B18"), 1) } } });
        var chip = si is null ? new Border { Name = "studio-icon-" + st.Id, Width = 26, Height = 26 } : Chip(si.Path, si.C1, si.C2, 26, 0.6);
        chip.Name = "studio-icon-" + st.Id; chip.HorizontalAlignment = HorizontalAlignment.Left; chip.VerticalAlignment = VerticalAlignment.Top; chip.Margin = new Thickness(8);
        chip.BoxShadow = BoxShadows.Parse("0 2 8 0 #88000000");
        layers.Children.Add(chip);
        var badge = Badge(st); badge.HorizontalAlignment = HorizontalAlignment.Right; badge.VerticalAlignment = VerticalAlignment.Top; badge.Margin = new Thickness(0, 8, 8, 0);
        layers.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 6, 0), CornerRadius = new CornerRadius(6), Padding = new Thickness(5, 1),
            Background = Brush.Parse("#99050B18"), IsVisible = badge.IsVisible, Child = badge });
        badge.Margin = new Thickness(0);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(9, 0, 9, 7), Spacing = 1, Children = {
            new TextBlock { Text = name, Foreground = Brushes.White, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
            new TextBlock { Text = info?.Sentence ?? "", Foreground = Brush.Parse("#C9D6EA"), FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis } } };
        layers.Children.Add(text);
        var card = new Button { Name = "studio-card-" + st.Id, Width = 190, Padding = new Thickness(0), Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(10), ClipToBounds = true,
            Background = TileBg, BorderBrush = Brush.Parse("#4D5AA9FF"), BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = new Border { CornerRadius = new CornerRadius(9), ClipToBounds = true, Child = layers } };
        Avalonia.Automation.AutomationProperties.SetName(card, name);
        ToolTip.SetTip(card, info is null ? name : $"{name} – {info.Sentence}");
        var sid = st.Id;
        card.Click += async (_, _) => await OpenStudio(sid, name);
        Hover(card);
        return card;
    }

    /// <summary>Kernfunktionen von Trendo als eigene Reihe (öffnen die echten Studios).</summary>
    private Control SystemModulesRow()
    {
        var wrap = new WrapPanel();
        foreach (var id in new[] { "CORE-02", "CORE-03", "SYS-05", "CORE-10", "X-06", "X-05" })
        {
            var info = StudioCatalog.For(id); if (info is null) continue;
            var icon = StudioIcons.For(id) is { } si ? Chip(si.Path, si.C1, si.C2, 34, 0.6) : Chip(Mdi.Apps, "#42A5F5", "#1565C0", 34, 0.6);
            var b = new Button { Name = "sysmod-" + id, Width = 228, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(10), Background = Brush.Parse(C("#99101F3D", "#E6FFFFFF")), BorderBrush = Edge, BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { icon, Pos(new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = {
                    new TextBlock { Text = info.Name, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = info.Sentence, Foreground = Muted, FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis } } }, 1) } } };
            Avalonia.Automation.AutomationProperties.SetName(b, info.Name);
            var sid = id; var nm = info.Name;
            b.Click += async (_, _) => await OpenStudio(sid, nm);
            Hover(b); wrap.Children.Add(b);
        }
        return new StackPanel { Spacing = 6, Children = { Section2(Mdi.Apps, "Systemmodule", "Kernfunktionen von Trendo"), wrap } };
    }

    private Control RecentProjectsRow()
    {
        var wrap = new WrapPanel();
        var all = SmallOutline("studios-projects-all", "Alle Projekte anzeigen"); all.Click += (_, _) => Show("projects");
        var list = _core.Projects.List().OrderByDescending(p => p.CreatedAt).Take(5).ToList();
        if (list.Count == 0)
        {
            var first = new Button { Name = "studios-first-project", Content = "＋ Erstes Projekt anlegen", Padding = new Thickness(12, 6), Background = Accent, CornerRadius = new CornerRadius(8) };
            first.Click += (_, _) => Show("projects");
            wrap.Children.Add(new StackPanel { Spacing = 6, Children = { P("Noch keine Projekte.", Text), first } });
        }
        foreach (var p in list)
        {
            var b = new Button { Name = "studios-project-" + p.Id, Width = 190, Height = 78, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(10), Background = Brush.Parse(C("#99101F3D", "#E6FFFFFF")), BorderBrush = Edge, BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top,
                Content = new StackPanel { Spacing = 3, Children = { new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Chip(Mdi.Folder, "#42A5F5", "#1565C0", 22), new TextBlock { Text = p.Name, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 130 } } },
                    new TextBlock { Text = $"Angelegt {p.CreatedAt:dd.MM.yyyy}", Foreground = Muted, FontSize = 10.5 } } } };
            Avalonia.Automation.AutomationProperties.SetName(b, p.Name);
            b.Click += (_, _) => Show("projects");
            wrap.Children.Add(b);
        }
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { Section2(Mdi.Folder, "Aktuelle Projekte", "Zuletzt angelegt"), Pos(all, 1) } };
        return new StackPanel { Spacing = 6, Children = { head, wrap } };
    }

    private static Control Section2(string icon, string title, string sub) => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = {
        Ico(icon, 18, Text), new TextBlock { Text = title, Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
        new TextBlock { Text = sub, Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center } } };

    /// <summary>Rechte Spalte der Übersicht: KI, Geräte, KI-Team, GitHub – mit echten Zuständen.</summary>
    private Control StudiosSide()
    {
        var sp = new StackPanel { Spacing = 10 };
        Border Card(string name, string icon, string title, string? sub, Control body, Button? more = null)
        {
            var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { Ico(icon, 17, Accent),
                Pos(new StackPanel { Margin = new Thickness(8, 0, 0, 0), Children = { new TextBlock { Text = title, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold } } }, 1) } };
            if (sub is not null) ((StackPanel)head.Children[1]).Children.Add(new TextBlock { Text = sub, Foreground = Muted, FontSize = 10.5 });
            if (more is not null) head.Children.Add(Pos(more, 2));
            var b = new Border { Name = name, Background = Glass, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10), Child = new StackPanel { Spacing = 8, Children = { head, body } } };
            return b;
        }
        Control Row(Control left, string title, string sub, string state, string stateColor) => new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { left,
            Pos(new StackPanel { Margin = new Thickness(10, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = title, Foreground = Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis }, new TextBlock { Text = sub, Foreground = Muted, FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis } } }, 1),
            Pos(new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 1), Background = Brush.Parse("#22" + stateColor.TrimStart('#')), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = state, Foreground = Brush.Parse(stateColor), FontSize = 10.5 } }, 2) } };

        // KI: lokal und kostenlos; bezahlte Dienste sind optional
        var kiMore = SmallOutline("studios-ki-open", "Öffnen"); kiMore.Click += (_, _) => Show("ki");
        var ki = new StackPanel { Spacing = 8, Children = {
            Row(Chip(Mdi.Brain, "#5C9DFF", "#283593", 30), "Lokale KI", _kiConnected ? KiModelName : "Kostenlos auf diesem PC", _kiConnected ? "Bereit" : "Nicht verbunden", _kiConnected ? "#4ADE80" : "#93A4C3"),
            Row(Chip(Mdi.CloudOutline, "#90A4AE", "#455A64", 30), "Online-KI", "Optional, eigener Schlüssel", "Optional", "#93A4C3"),
            Row(Chip(Mdi.ShieldCheck, "#66BB6A", "#1B5E20", 30), "Kosten", "Keine bezahlte KI nötig", "0 €", "#4ADE80") } };
        sp.Children.Add(Card("studios-side-ki", Mdi.Brain, "KI", "Kostenlose KI für jede Aufgabe", ki, kiMore));

        // Geräte: dieser PC mit echten Messwerten, weitere Geräte über „PC, Pi & Handy verbinden“
        var devMore = SmallOutline("studios-devices-open", "Alle anzeigen"); devMore.Click += (_, _) => Show("geraete");
        string Pct(double? v) => v is { } x ? $"{x:0} %" : "n. v.";
        var dev = new StackPanel { Spacing = 8, Children = {
            Row(Chip(Mdi.Monitor, "#42A5F5", "#1565C0", 30), "Dieser PC", $"CPU {Pct(_lastCpu)} · RAM {Pct(_lastRam)}", "Online", "#4ADE80") } };
        var link = new Button { Name = "studios-devices-connect", Content = "Raspberry Pi oder Handy verbinden …", Padding = new Thickness(8, 3), FontSize = 11, Background = Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6) };
        link.Click += async (_, _) => await OpenStudio("CORE-04", StudioCatalog.NameOf("CORE-04", "PC, Pi & Handy verbinden"));
        dev.Children.Add(link);
        sp.Children.Add(Card("studios-side-devices", Mdi.Monitor, "Geräte", null, dev, devMore));

        // KI-Team: die echten Rollen des Agenten-Centers
        var teamMore = SmallOutline("studios-team-open", "Alle anzeigen"); teamMore.Click += (_, _) => Show("ki-rollen");
        var team = new StackPanel { Spacing = 6 };
        foreach (var r in AgentCenter.Roles.Take(5))
            team.Children.Add(Row(Chip(Mdi.RobotHappy, "#7FD3FF", "#2F86F6", 26), r.Name, r.Does, "Bereit", "#93A4C3"));
        sp.Children.Add(Card("studios-side-team", Mdi.RobotHappy, "KI-Team", $"{AgentCenter.Roles.Count} Rollen", team, teamMore));

        // GitHub & Open Source
        var gh = new Button { Name = "studios-github-open", Content = "Freie Projekte auf GitHub finden", Padding = new Thickness(10, 5), Background = Accent, CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        gh.Click += async (_, _) => await OpenStudio("X-06", StudioCatalog.NameOf("X-06", "Projekte von GitHub holen"));
        sp.Children.Add(Card("studios-side-github", Mdi.CodeTags, "GitHub & Open Source", "Nur lesen, nichts wird automatisch installiert", gh));
        return sp;
    }
}
