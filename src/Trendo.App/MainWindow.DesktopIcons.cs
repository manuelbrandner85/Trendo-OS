using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Wunsch 27.10.2026: Der Desktop ist standardmäßig aufgeräumt – keine Symbole. Studios, Programme und Systemmodule legt man sich selbst
/// auf den Desktop (Rechtsklick „Symbol hinzufügen“, im Startmenü „Auf den Desktop“) und nimmt sie wieder weg. Die Symbole sehen aus wie bisher
/// (Bildkacheln der Studios, Module mit ihrem Farbsymbol). Gleiches Aussehen nutzt die Handy-App.</summary>
public partial class MainWindow
{
    /// <summary>Kernfunktionen von Trendo, die man wie Apps auf den Desktop legen kann (Schlüssel „page:…“).</summary>
    internal static readonly (string Key, string Title, string Sub, string Icon, string C1, string C2)[] SystemModules =
    [
        ("ki", "Trendo KI", "Verstehen, planen, erstellen", Mdi.StarFourPoints, "#8FD8FF", "#2F86F6"),
        ("ki-rollen", "Agenten-Zentrale", "12 Rollen, Teams, Aufgaben", Mdi.RobotHappy, "#9CF5A8", "#22B35E"),
        ("ablaeufe", "Abläufe", "Schritte automatisch ausführen", Mdi.Tools, "#FFD39A", "#F08A3C"),
        ("ki-wissen", "Wissen & Gedächtnis", "Eigene Dokumente, Kontext", Mdi.Brain, "#C7A6FF", "#7C4DFF"),
        ("components", "Programme", "Vollpaket, ohne Download", Mdi.Apps, "#7FD3FF", "#3F6BFF"),
        ("ki-quellen", "Freie Quellen", "Kostenlose Modelle & Assets", Mdi.Magnify, "#8FB8FF", "#3D5BFF"),
        ("dev", "Code & GitHub", "Projekte, Versionen", Mdi.CodeTags, "#ECEFF1", "#90A4AE"),
        ("approvals", "Freigaben", "Du entscheidest vorher", Mdi.ShieldCheck, "#9CF5A8", "#15803D"),
        ("ressourcen", "Ressourcen", "Leistung gezielt verteilen", Mdi.ChartLine, "#FF8A80", "#E53935"),
        ("handy", "Handy-Verbindung", "Dateien, Bildschirm, Trendo am Handy", Mdi.Cellphone, "#80DEEA", "#00838F"),
    ];

    string _deskIconQuery = "";

    void InitDesktopIcons()
    {
        _showMap["desktop-symbole"] = DesktopIconsView;
        if (!ExtraPages.Any(p => p.Key == "desktop-symbole")) ExtraPages.Add(("Desktop-Symbole", "desktop-symbole", ["desktop", "symbol", "symbole", "verknüpfung", "kachel", "aufräumen"]));
    }

    public bool OnDesktop(string id) => _settings.DesktopIcons.Contains(id);

    /// <summary>Symbol auf den Desktop legen oder wegnehmen (Studio-ID oder „page:Schlüssel“).</summary>
    public void SetOnDesktop(string id, bool on)
    {
        if (on && !_settings.DesktopIcons.Contains(id)) _settings.DesktopIcons.Add(id);
        if (!on) _settings.DesktopIcons.Remove(id);
        SaveSettings();
        if (CurrentView == "start" || _deskHost.Children.Count > 0) { _deskHost.Children.Clear(); _deskHost.Children.Add(SafeView("start")); }
    }

    (string Title, string Sub)? DeskIconInfo(string id)
    {
        if (id.StartsWith("app:", StringComparison.Ordinal))
            return WebApps.Get(id[4..]) is { } wa && WebAppInstalled(wa.Id) ? (wa.Name, "App") : null;
        if (id.StartsWith("page:", StringComparison.Ordinal))
            return SystemModules.FirstOrDefault(m => "page:" + m.Key == id) is { Title: not null } m ? (m.Title, m.Sub) : null;
        if (FeaturedStudios.FirstOrDefault(f => f.Id == id) is { Id: not null } f) return (StudioCatalog.NameOf(f.Id, f.Title), f.Sub);
        return StudioCatalog.For(id) is { } info ? (info.Name, info.Sentence) : _studios.FirstOrDefault(s => s.Id == id) is { } s ? (s.Name, "") : null;
    }

    /// <summary>Aussehen eines Symbols wie auf dem Desktop: Studios als Bildkachel (Bild, Verlauf), Module mit Farbsymbol.</summary>
    Control DeskIconFace(string id, bool label, double iconSize = 30)
    {
        if (id.StartsWith("app:", StringComparison.Ordinal) && WebApps.Get(id[4..]) is { } wa) return WebAppFace(wa, iconSize * 1.3);
        if (id.StartsWith("page:", StringComparison.Ordinal) && SystemModules.FirstOrDefault(m => "page:" + m.Key == id) is { Key: not null } m)
            return new Border { Background = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#2B4C86"), 0), new GradientStop(Color.Parse("#0E1B3A"), 1) } },
                Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 4, Children = { Chip(m.Icon, m.C1, m.C2, iconSize * 1.4, 0.58) } } };
        var face = new Grid { ClipToBounds = true };
        if (StudioPicture(id) is { } pic) face.Children.Add(new Image { Source = pic, Stretch = Stretch.UniformToFill });
        else
        {
            face.Children.Add(new Border { Background = Grad("#1A3A6B", "#0A1A36") });
            if (IconFor(id, iconSize) is { } ico) { ico.HorizontalAlignment = HorizontalAlignment.Center; ico.VerticalAlignment = VerticalAlignment.Center; face.Children.Add(ico); }
        }
        face.Children.Add(new Border { Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0.35, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#00040A18"), 0), new GradientStop(Color.Parse(label ? "#E6040A18" : "#66040A18"), 1) } } });
        return face;
    }

    /// <summary>Die Symbole auf dem Desktop – oder, wenn keine da sind, ein dezenter Hinweis.</summary>
    Control DesktopIconsPanel()
    {
        var ids = _settings.DesktopIcons.Where(i => DeskIconInfo(i) is not null).ToList();
        if (ids.Count == 0)
        {
            var add = SmallOutline("desk-add-hint", "Symbol hinzufügen");
            add.Click += (_, _) => Show("desktop-symbole");
            return new Border { Name = "desk-empty", Margin = new Thickness(0, 6), HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.85,
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = {
                    new TextBlock { Text = "Dein Desktop ist aufgeräumt. Symbole legst du dir selbst hierher – Rechtsklick oder im Startmenü „Auf den Desktop“.", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center },
                    add } } };
        }
        var wrap = new WrapPanel { Name = "desk-icons" };
        foreach (var id in ids)
        {
            var (title, sub) = DeskIconInfo(id)!.Value;
            var module = id.StartsWith("page:", StringComparison.Ordinal);
            var face = new Grid { Children = { DeskIconFace(id, true) } };
            face.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(8, 0, 6, 6), Children = {
                new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 11.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = sub, Foreground = Brush.Parse("#C9D6EA"), FontSize = 9.5, TextTrimming = TextTrimming.CharacterEllipsis } } });
            var webApp = id.StartsWith("app:", StringComparison.Ordinal);
            var t = new Button { Name = module ? "module-" + id[5..] : webApp ? "app-" + id[4..] : "tile-" + id, Width = 150, Height = 84, Margin = new Thickness(4), Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, CornerRadius = new CornerRadius(10), ClipToBounds = true,
                Background = Brushes.Transparent, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Content = face };
            Avalonia.Automation.AutomationProperties.SetName(t, title); ToolTip.SetTip(t, sub.Length > 0 ? $"{title} – {sub}" : title);
            var sid = id;
            t.Click += async (_, _) => { if (sid.StartsWith("page:", StringComparison.Ordinal)) Show(sid[5..]); else if (sid.StartsWith("app:", StringComparison.Ordinal)) OpenWebApp(sid[4..]); else await OpenStudio(sid, title); };
            var rm = new MenuItem { Name = "desk-remove-" + id.Replace(':', '-'), Header = "Vom Desktop entfernen" };
            rm.Click += (_, _) => SetOnDesktop(sid, false);
            t.ContextMenu = new ContextMenu { Items = { rm } };
            Hover(t); wrap.Children.Add(t);
        }
        var panel = Glass2(new StackPanel { Children = { wrap } }, 10);
        panel.Name = "widget-desktop-icons";
        return panel;
    }

    /// <summary>Seite „Desktop-Symbole“: alles, was auf den Desktop kann, mit Knopf zum Hinzufügen/Entfernen.</summary>
    Control DesktopIconsView()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(H("Desktop-Symbole", 18));
        sp.Children.Add(P("Lege dir die Studios und Programme auf den Desktop, die du oft brauchst. Alles andere findest du jederzeit im Startmenü.", Text, 13));
        var search = new TextBox { Name = "desk-icon-search", Text = _deskIconQuery, PlaceholderText = "Suchen …", MaxWidth = 360, HorizontalAlignment = HorizontalAlignment.Left };
        search.TextChanged += (_, _) => { _deskIconQuery = search.Text ?? ""; FillDeskIconList(sp); };
        sp.Children.Add(search);
        FillDeskIconList(sp);
        return sp;
    }

    void FillDeskIconList(StackPanel sp)
    {
        while (sp.Children.Count > 3) sp.Children.RemoveAt(3);
        var q = _deskIconQuery.Trim();
        var all = SystemModules.Select(m => ("page:" + m.Key, m.Title, "Trendo"))
            .Concat(MenuStudios().Select(s => (s.Id, s.Name, StudioCatalog.For(s.Id)?.Area ?? "Weitere")))
            .Where(x => q.Length == 0 || x.Item2.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Item3.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var grp in all.GroupBy(x => x.Item3))
        {
            sp.Children.Add(H(grp.Key, 14));
            var wrap = new WrapPanel();
            foreach (var (id, title, _) in grp)
            {
                var on = OnDesktop(id);
                var face = new Border { Width = 46, Height = 46, CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = DeskIconFace(id, false, 22) };
                var b = new Button { Name = "desk-toggle-" + id.Replace(':', '-'), Width = 250, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(8, 6), HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Background = on ? Brush.Parse("#262F86F6") : Brushes.Transparent, CornerRadius = new CornerRadius(10),
                    Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { face,
                        Pos(new TextBlock { Text = title, Foreground = Text, FontSize = 12.5, Margin = new Thickness(10, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }, 1),
                        Pos(new TextBlock { Text = on ? "✓ auf dem Desktop" : "＋ hinzufügen", Foreground = on ? Brush.Parse("#4ADE80") : Accent, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, 2) } } };
                var sid = id;
                b.Click += (_, _) => { SetOnDesktop(sid, !OnDesktop(sid)); Show("desktop-symbole"); };
                wrap.Children.Add(b);
            }
            sp.Children.Add(wrap);
        }
    }
}
