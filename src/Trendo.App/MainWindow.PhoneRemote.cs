using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Wunsch 27.10.2026: Trendo auf dem Handy voll nutzen – „nicht nur ein kleines Fenster, sondern wie eine Handy-App“.
/// Der PC baut die Programme auf einer eigenen, unsichtbaren Handy-Fläche in Handy-Größe auf (Hochformat, Breite des Handys) und schickt
/// das Bild; das Handy tippt, schreibt, wählt und wischt darin. Alles läuft auf dem PC. Zusätzlich gibt es den ganzen PC-Bildschirm.
/// Nur für Handys, die der PC-Nutzer dafür freigegeben hat.</summary>
public partial class MainWindow
{
    TextBox? _remoteFocus;
    ComboBox? _remoteCombo;
    readonly Dictionary<string, DateTime> _remoteToast = [];
    /// <summary>Was zuletzt vom Handy angetippt wurde (Name des Elements, für Protokoll und Tests).</summary>
    public string? PhoneLastHit { get; private set; }

    // Handy-Fläche: liegt außerhalb des sichtbaren Bereichs, wird nur für das Handy gezeichnet
    Canvas? _phoneStage;
    Border? _phoneHolder, _phoneSurface;
    LayoutTransformControl? _phoneScaler;
    double _phoneScale = 1;
    bool _phoneNeedsFit;
    string? _phonePage;
    readonly List<string> _phoneHistory = [];
    DateTime _phoneRouteUntil;
    /// <summary>Seite, die gerade auf dem Handy offen ist (null = Handy-Startbildschirm).</summary>
    public string? PhonePage => _phonePage;

    void InitPhoneSurface(Grid root)
    {
        _phoneSurface = new Border { Name = "phone-surface", Background = Brush.Parse("#0A1428"), ClipToBounds = true };
        _phoneSurface.Width = 412; _phoneSurface.Height = 800;
        // Schärfe über eine Layout-Vergrößerung statt über die DPI: Avalonia zeichnet eingebettete Ebenen (Schatten, Ausschnitte) bei hoher DPI
        // sonst doppelt vergrößert – Befund 27.10.2026 („am Handy ist alles zu groß“).
        _phoneScaler = new LayoutTransformControl { Child = _phoneSurface };
        // Schrift wächst beim Vergrößern exakt mit (ohne Hinting würde knapper Text sonst ein paar Prozent breiter und abgeschnitten)
        TextOptions.SetTextHintingMode(_phoneScaler, TextHintingMode.None);
        _phoneHolder = new Border { Child = _phoneScaler };
        Canvas.SetLeft(_phoneHolder, -30000); Canvas.SetTop(_phoneHolder, 0);
        _phoneStage = new Canvas { IsHitTestVisible = false, Children = { _phoneHolder } };
        Grid.SetRowSpan(_phoneStage, Math.Max(1, root.RowDefinitions.Count)); Grid.SetColumnSpan(_phoneStage, Math.Max(1, root.ColumnDefinitions.Count));
        root.Children.Add(_phoneStage);
        // Ein echter Klick am PC beendet die Umleitung aufs Handy sofort
        AddHandler(PointerPressedEvent, (_, _) => _phoneRouteUntil = default, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>Geht diese Seite aufs Handy? Ja, wenn das Handy sie gerade (oder vor wenigen Sekunden) ausgelöst hat
    /// oder sie dort offen ist und am PC kein Fenster sie zeigt (z. B. Aktualisieren nach dem Laden).</summary>
    bool PhoneTakes(string key) =>
        _phoneSurface is not null && (DateTime.UtcNow < _phoneRouteUntil || (key == _phonePage && !Windows.Stack.Any(w => w.Page == key && !w.Minimized)));

    void PresentPhone(string key, bool push = true)
    {
        if (_phoneSurface is null) return;
        if (key == "start") { _phonePage = null; _phoneHistory.Clear(); _phoneSurface.Child = null; return; }
        CurrentView = key;
        if (push && (_phoneHistory.Count == 0 || _phoneHistory[^1] != key)) _phoneHistory.Add(key);
        if (_phoneHistory.Count > 30) _phoneHistory.RemoveAt(0);
        _phonePage = key;
        var content = SafeView(key);
        // Wie ein Fenster am PC: feste Fläche (Handy-Breite × Handy-Höhe), die Seite rollt selbst. Befund 27.10.2026: in einem unendlich hohen
        // Rollbereich blähten sich Seiten mit anpassender Vorschau (z. B. Brief als DIN-A4-Blatt) riesig auf.
        // Handy-Layout: senkrecht rollen, Breite fest (Inhalt bricht um). Spalten und zu breite Leisten richtet PhoneFit beim ersten Bild um.
        _phoneSurface.Child = new ScrollViewer
        {
            Name = "phone-scroll", HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(6, 6, 6, 12), Child = content },
        };
        _phoneNeedsFit = true;
        AfterShow(key);
    }

    /// <summary>Für Tests: Seite direkt auf der Handy-Fläche öffnen.</summary>
    public void PresentPhoneForTests(string key) => PresentPhone(key);

    /// <summary>Apps für das Handy: alle Studios und Programme aus dem Startmenü, mit Bereich und Symbol.</summary>
    public IReadOnlyList<PhoneApp> PhoneApps() =>
        SystemModules.Select(m => new PhoneApp("page:" + m.Key, m.Title, "Trendo", m.Icon, _settings.DesktopIcons.Contains("page:" + m.Key)))
            .Concat(MenuStudios().Select(s => (s.Id, s.Name, Area: StudioCatalog.For(s.Id)?.Area ?? "Weitere"))
                .Select(s => new PhoneApp(s.Id, s.Name, s.Area, AreaIcon(s.Area), _settings.DesktopIcons.Contains(s.Id)))).ToList();

    readonly Dictionary<string, byte[]> _phoneIcons = [];
    /// <summary>App-Symbol fürs Handy: dieselbe Kachel wie auf dem Trendo-Desktop, quadratisch mit runden Ecken, scharf für Handy-Bildschirme.</summary>
    public byte[]? PhoneAppIcon(string id)
    {
        if (_phoneIcons.TryGetValue(id, out var cached)) return cached;
        if (DeskIconInfo(id) is null && !PhoneApps().Any(a => a.Id == id)) return null;
        var tile = new Border { Width = 64, Height = 64, CornerRadius = new CornerRadius(15), ClipToBounds = true, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = DeskIconFace(id, false, 30) };
        tile.Measure(new Size(64, 64)); tile.Arrange(new Rect(0, 0, 64, 64));
        return _phoneIcons[id] = Png(tile, new Size(64, 64), 3);
    }

    /// <summary>App auf dem Handy öffnen (wie im Startmenü, nur auf der Handy-Fläche).</summary>
    public async Task<string?> PhoneOpen(string id)
    {
        if (PhoneApps().FirstOrDefault(a => a.Id == id) is not { } app) return null;
        _phoneHistory.Clear();
        _phoneRouteUntil = DateTime.UtcNow.AddSeconds(6);
        if (id.StartsWith("page:", StringComparison.Ordinal)) { PresentPhone(id[5..]); return app.Name; }
        var before = _phonePage;
        await OpenStudio(id, app.Name);
        if (_phonePage is null || _phonePage == before) PresentPhone(StudioLaunchPage(id));
        return _phonePage is { } p ? PageTitle(p) : app.Name;
    }

    public string? PhoneBack()
    {
        if (_phoneHistory.Count > 0) _phoneHistory.RemoveAt(_phoneHistory.Count - 1);
        if (_phoneHistory.Count == 0) { PresentPhone("start"); return null; }
        PresentPhone(_phoneHistory[^1], push: false);
        return PageTitle(_phoneHistory[^1]);
    }

    /// <summary>Handy-Fläche auf die Größe des Handys bringen (CSS-Pixel des Handys = Avalonia-Einheiten).</summary>
    public void PhoneViewport(int width, int height)
    {
        if (_phoneSurface is null || (_phoneSurface.Width == width && _phoneSurface.Height == height)) return;
        var widthChanged = _phoneSurface.Width != width;
        _phoneSurface.Width = width; _phoneSurface.Height = height;
        if (widthChanged && _phonePage is { } p) PresentPhone(p, push: false); // andere Handy-Breite: Seite neu aufbauen und neu umbrechen
    }

    /// <summary>Handy-Umbruch (Wunsch 27.10.2026: „responsive für jedes Handy“): PC-Seiten sind für breite Fenster gebaut. Auf dem Handy werden
    /// Spalten-Layouts untereinander gestapelt und zu breite Knopfleisten in mehrere Zeilen umgebrochen – nur auf der Handy-Fläche, am PC bleibt alles.</summary>
    void PhoneFit(double width)
    {
        if (_phoneSurface?.Child is not ScrollViewer sv || sv.Content is not Border pad || pad.Child is not Control root) return;
        // Eingebettete Bereiche rollen auf dem Handy nicht waagerecht – Leisten brechen stattdessen um
        foreach (var inner in root.GetSelfAndVisualDescendants().OfType<ScrollViewer>()) inner.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        for (var round = 0; round < 8; round++)
        {
            _phoneSurface.Measure(new Size(width, _phoneSurface.Height)); _phoneSurface.Arrange(new Rect(0, 0, width, _phoneSurface.Height));
            var changed = false;
            foreach (var g in root.GetSelfAndVisualDescendants().OfType<Grid>().ToList())
                if (StackColumns(g, width)) changed = true;
            foreach (var st in root.GetSelfAndVisualDescendants().OfType<StackPanel>().ToList())
                if (WrapRow(st, width)) changed = true;
            foreach (var c in root.GetSelfAndVisualDescendants().OfType<Control>().ToList())
                if (FitControl(c, width)) changed = true;
            // Kachel-Raster (4–6 Spalten am PC): auf dem Handy so viele Spalten, wie ordentlich hineinpassen
            foreach (var u in root.GetSelfAndVisualDescendants().OfType<Avalonia.Controls.Primitives.UniformGrid>().ToList())
            {
                var max = Math.Max(1, (int)(Math.Max(u.Bounds.Width, 1) / 110));
                if (u.Columns > max) { u.Columns = max; changed = true; }
            }
            foreach (var t in root.GetSelfAndVisualDescendants().OfType<TextBlock>().ToList())
                if (FitText(t)) changed = true;
            if (!changed) break;
        }
        _phoneSurface.Measure(new Size(width, _phoneSurface.Height)); _phoneSurface.Arrange(new Rect(0, 0, width, _phoneSurface.Height));
    }

    /// <summary>Einzelne Elemente, die über den Handy-Rand ragen: feste Breiten schmaler, Texte und Knopftexte umbrechen.</summary>
    bool FitControl(Control c, double width)
    {
        if (_phoneSurface is null || !c.IsEffectivelyVisible || c.TranslatePoint(new Point(c.Bounds.Width, 0), _phoneSurface) is not { } right) return false;
        if (c.TranslatePoint(new Point(0, 0), _phoneSurface) is not { } left || left.X >= width) return false; // liegt ganz außerhalb (z. B. ausgeblendete Spalte)
        var wider = c.Parent is Control par && par.Bounds.Width > 0 && c.Bounds.Width > par.Bounds.Width + 1; // zu breit für den Behälter (auch zentriert, links hinaus)
        if (right.X <= width + 1 && left.X >= -1 && !wider) return false;
        // Platz: bis zum Handy-Rand, höchstens so breit wie der Behälter (zentrierte, zu breite Felder beginnen sonst links außerhalb)
        var room = Math.Max(80, Math.Min(width - Math.Max(0, left.X) - 8, (c.Parent as Control)?.Bounds.Width is > 0 and var pw ? pw - c.Margin.Left - c.Margin.Right : width));
        switch (c)
        {
            case Button { Content: string text } b:
                b.Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
                b.MaxWidth = room; b.Width = double.NaN; b.MinWidth = 0;
                return true;
            case TextBlock t when t.TextWrapping == TextWrapping.NoWrap && t.TextTrimming == TextTrimming.None:
                t.TextWrapping = TextWrapping.Wrap; t.MaxWidth = room;
                return true;
            case TextBlock t when t.MaxWidth > room:
                t.MaxWidth = room; return true;
            case ComboBox when room < Math.Min(c.Width, 140): // Auswahl nicht zu schmal drücken – sie rutscht besser in die nächste Zeile
                c.Margin = new Thickness(0, c.Margin.Top, c.Margin.Right, c.Margin.Bottom);
                return false;
            case TextBox or ComboBox or Border or Avalonia.Controls.Panel or ContentControl when (c.Width > room || c.MinWidth > room) && c.Parent is not Avalonia.Controls.Presenters.ScrollContentPresenter:
                if (c.Width > room) c.Width = room;
                if (c.MinWidth > room) c.MinWidth = room;
                c.MaxWidth = Math.Min(c.MaxWidth, room);
                return true;
        }
        return false;
    }

    /// <summary>Breite, die ein Text ohne Umbruch bräuchte.</summary>
    public static double NaturalWidth(TextBlock t)
    {
        if (string.IsNullOrEmpty(t.Text)) return 0;
        var probe = new TextBlock { Text = t.Text, FontSize = t.FontSize, FontWeight = t.FontWeight, FontFamily = t.FontFamily, FontStyle = t.FontStyle, Padding = t.Padding, LetterSpacing = t.LetterSpacing };
        probe.Measure(Size.Infinity);
        return probe.DesiredSize.Width;
    }

    static bool Overflows(TextBlock t) =>
        t.IsEffectivelyVisible && t.TextWrapping == TextWrapping.NoWrap && t.TextTrimming == TextTrimming.None && t.Bounds.Width > 0 && NaturalWidth(t) > t.Bounds.Width + 2;

    /// <summary>Text, der nicht in sein Feld passt (abgeschnitten oder über den Nachbarn), bricht um – bei Knopftexten der ganze Knopf.</summary>
    bool FitText(TextBlock t)
    {
        if (!Overflows(t) || t.GetVisualAncestors().TakeWhile(a => a != _phoneSurface).Any(a => a is Canvas or Viewbox or TextBox)) return false;
        var single = !(t.Text ?? "").Trim().Contains(' ');
        if (single) // ein einzelnes Wort kann nicht umbrechen: Schrift etwas kleiner (auch Knopftext)
        {
            var k = Math.Max(9, Math.Floor(t.FontSize * (t.Bounds.Width - 1) / NaturalWidth(t) * 10) / 10);
            if (t.TemplatedParent is not null && t.FindAncestorOfType<ContentControl>() is { } owner) owner.FontSize = k; else t.FontSize = k;
            return true;
        }
        if (t.FindAncestorOfType<Button>() is { Content: string text } b && t.Text == text)
        {
            b.Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
            b.Width = double.NaN; b.MinWidth = 0;
            return true;
        }
        if (t.TemplatedParent is not null) return false;
        t.TextWrapping = TextWrapping.Wrap;
        t.InvalidateMeasure();
        return true;
    }

    /// <summary>Prüfung „sauber auf dem Handy“: sichtbare Elemente, die über den rechten Rand ragen (für Tests und das Protokoll).</summary>
    public List<string> PhoneOverflow(double width)
    {
        var bad = new List<string>();
        if (_phoneSurface?.Child is not ScrollViewer sv || sv.Content is not Control root) return bad;
        foreach (var c in root.GetSelfAndVisualDescendants().OfType<Control>())
        {
            if (!c.IsEffectivelyVisible || c.Bounds.Width < 1 || c.Opacity <= 0) continue;
            if (c.GetVisualAncestors().OfType<ScrollViewer>().Any(x => x.Name != "phone-scroll" && x.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled)) continue;
            if (c is Image or Shape || c.GetVisualAncestors().TakeWhile(a => a != _phoneSurface).Any(a => a is Canvas or Viewbox)) continue; // Bilder und Zeichnungen dürfen beschnitten sein
            if (c is TextBlock ot && ot.GetVisualAncestors().All(a => a is not TextBox) && Overflows(ot))
                bad.Add($"Text „{ot.Text}“ passt nicht in sein Feld ({NaturalWidth(ot):0} statt {ot.Bounds.Width:0} px)");
            else if (c.TranslatePoint(new Point(c.Bounds.Width, 0), _phoneSurface) is { } r && r.X > width + 2 && c.TranslatePoint(default, _phoneSurface) is { } l && l.X < width)
                bad.Add($"{c.GetType().Name} {c.Name} {(c is TextBlock tb ? "„" + tb.Text + "“" : c is Button { Content: string bt } ? "„" + bt + "“" : "")} ragt {r.X - width:0} px hinaus");
        }
        return bad;
    }

    static bool Simple(Control c) => c is TextBlock or Button or Image or TextBox or ComboBox or CheckBox or Shape or Avalonia.Controls.Primitives.ToggleButton or ProgressBar or Slider
        || (c is Border b && b.Child is null or TextBlock or Image or Shape);

    /// <summary>Grid mit mehreren Spalten nebeneinander (Menü | Inhalt | Vorschau) → untereinander. Kleine Zeilen (Text + Knopf) bleiben.</summary>
    bool StackColumns(Grid g, double width)
    {
        if (g.ColumnDefinitions.Count < 2 || g.RowDefinitions.Count > 1) return false;
        var kids = g.Children.Where(c => c.IsVisible).ToList();
        if (kids.Count < 2 || kids.Any(c => Grid.GetColumnSpan(c) > 1 || Grid.GetRowSpan(c) > 1)) return false;
        // Zeile, die über den Handy-Rand ragt (z. B. Eingabefeld + Knopf, Schritt-Leiste): immer untereinander
        bool Beyond(Control c) => _phoneSurface is not null && c.TranslatePoint(new Point(c.Bounds.Width, 0), _phoneSurface) is { } r && r.X > width + 1;
        var overflow = Beyond(g) || kids.Any(Beyond);
        if (!overflow && (g.Bounds.Width < width * 0.5 || kids.Count(c => !Simple(c)) < 2)) return false;
        var cols = kids.Select(Grid.GetColumn).Distinct().Count();
        if (cols < 2) return false;
        if (g.Parent is not Avalonia.Controls.Panel and not Decorator and not ContentControl) return false;
        // Kinder in eine senkrechte Liste umziehen (das alte Grid bleibt unbenutzt zurück – setzt die Seite dort später Spalten, stört das nicht)
        var order = g.Children.OrderBy(Grid.GetColumn).ToList();
        var col = new StackPanel { Name = g.Name, Margin = g.Margin, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = g.VerticalAlignment };
        Grid.SetRow(col, Grid.GetRow(g)); Grid.SetColumn(col, Grid.GetColumn(g)); Grid.SetRowSpan(col, Grid.GetRowSpan(g)); Grid.SetColumnSpan(col, Grid.GetColumnSpan(g));
        DockPanel.SetDock(col, DockPanel.GetDock(g));
        g.Children.Clear();
        foreach (var k in order)
        {
            k.Width = double.NaN; k.MaxWidth = double.PositiveInfinity; k.MinWidth = 0; k.HorizontalAlignment = HorizontalAlignment.Stretch;
            col.Children.Add(k);
        }
        switch (g.Parent)
        {
            case Avalonia.Controls.Panel p: p.Children[p.Children.IndexOf(g)] = col; break;
            case Decorator d: d.Child = col; break;
            case ContentControl cc: cc.Content = col; break;
        }
        return true;
    }

    /// <summary>Waagerechte Leiste, die über den Handy-Rand ragt (Knöpfe, Schritte) → WrapPanel, das in mehrere Zeilen umbricht.</summary>
    bool WrapRow(StackPanel st, double width)
    {
        if (st.Orientation != Orientation.Horizontal || st.Children.Count < 2 || st.Parent is not Avalonia.Controls.Panel and not Decorator and not ContentControl) return false;
        // Die Leiste selbst ist oft schmal genug – ihr Inhalt ragt hinaus: rechte Kante des letzten sichtbaren Elements zählt
        if (_phoneSurface is null) return false;
        var far = st.Children.Where(c => c.IsVisible).Select(c => c.TranslatePoint(new Point(c.Bounds.Width, 0), _phoneSurface)?.X ?? 0).DefaultIfEmpty(0).Max();
        if (far <= width + 1) return false;
        var wrap = new WrapPanel { Name = st.Name, Margin = st.Margin, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = st.VerticalAlignment };
        Grid.SetRow(wrap, Grid.GetRow(st)); Grid.SetColumn(wrap, Grid.GetColumn(st)); Grid.SetRowSpan(wrap, Grid.GetRowSpan(st)); Grid.SetColumnSpan(wrap, Grid.GetColumnSpan(st));
        DockPanel.SetDock(wrap, DockPanel.GetDock(st));
        var kids = st.Children.ToList();
        st.Children.Clear();
        foreach (var k in kids)
        {
            if (k.Margin == default) k.Margin = new Thickness(0, 0, Math.Max(4, st.Spacing), 6);
            wrap.Children.Add(k);
        }
        switch (st.Parent)
        {
            case Avalonia.Controls.Panel p: var i = p.Children.IndexOf(st); p.Children[i] = wrap; break;
            case Decorator d: d.Child = wrap; break;
            case ContentControl cc: cc.Content = wrap; break;
        }
        return true;
    }

    static byte[] Png(Visual v, Size size, double scale)
    {
        var px = new PixelSize(Math.Max(1, (int)(size.Width * scale)), Math.Max(1, (int)(size.Height * scale)));
        using var rtb = new RenderTargetBitmap(px, new Vector(96 * scale, 96 * scale));
        rtb.Render(v);
        using var ms = new MemoryStream();
        rtb.Save(ms, new PngBitmapEncoderOptions());
        return ms.ToArray();
    }

    /// <summary>Bild für das Handy: die Handy-Fläche in Handy-Größe – oder (<paramref name="desktop"/>) der ganze PC-Bildschirm.</summary>
    public PhoneFrame? PhoneScreen(int width, int height, double scale, bool desktop)
    {
        if (desktop)
        {
            var size = Bounds.Size;
            if (size.Width < 10 || size.Height < 10) return null;
            return new PhoneFrame(Png(this, size, Math.Min(scale, width * scale / size.Width)), "PC-Bildschirm");
        }
        if (_phoneSurface?.Child is null || _phonePage is null) return null;
        PhoneViewport(width, height);
        if (_phoneNeedsFit) { _phoneNeedsFit = false; PhoneFit(width); }
        if (_phoneScaler is not null && Math.Abs(_phoneScale - scale) > 0.01) { _phoneScale = scale; _phoneScaler.LayoutTransform = new ScaleTransform(scale, scale); }
        var sz = new Size(width * _phoneScale, height * _phoneScale);
        _phoneScaler!.Measure(sz); _phoneScaler.Arrange(new Rect(sz));
        return new PhoneFrame(Png(_phoneScaler, sz, 1), PageTitle(_phonePage));
    }

    /// <summary>Kompatibel: PC-Bildschirm als PNG.</summary>
    public byte[]? PhoneScreen(int maxWidth) => PhoneScreen(maxWidth, 0, 1, true)?.Png;

    /// <summary>Was liegt an diesem Punkt ganz oben? Eigene Prüfung über die Bildschirmgeometrie – funktioniert auch auf der Handy-Fläche außerhalb des Fensters.</summary>
    static Visual? HitDeep(Visual root, Point p)
    {
        Visual? best = null;
        void Walk(Visual v)
        {
            if (!v.IsVisible || v.Opacity <= 0 || (v is InputElement ie && !ie.IsHitTestVisible)) return;
            if (root.TranslatePoint(p, v) is not { } q) return;
            var inside = new Rect(v.Bounds.Size).Contains(q);
            if (!inside && v.ClipToBounds) return;
            if (inside) best = v;
            foreach (var c in v.GetVisualChildren().OrderBy(c => c.ZIndex)) Walk(c);
        }
        Walk(root);
        return best;
    }

    PhoneHit Act(Visual hit)
    {
        PhoneLastHit = hit.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(c => c.Name is { Length: > 0 } n && !n.StartsWith("PART_", StringComparison.Ordinal))?.Name;
        foreach (var v in hit.GetSelfAndVisualAncestors())
        {
            switch (v)
            {
                case TextBox tb when tb.IsEffectivelyEnabled:
                    tb.Focus(); _remoteFocus = tb; tb.CaretIndex = tb.Text?.Length ?? 0;
                    return new PhoneHit("Eingabefeld", tb.Text ?? "", Multiline: tb.AcceptsReturn);
                case ToggleButton t when t.IsEffectivelyEnabled:
                    t.IsChecked = !(t.IsChecked ?? false);
                    t.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    return new PhoneHit("Schalter");
                case Button b when b.IsEffectivelyEnabled:
                    b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if (b.Command is { } cmd && cmd.CanExecute(b.CommandParameter)) cmd.Execute(b.CommandParameter);
                    return new PhoneHit("Knopf");
                case ComboBox cb when cb.IsEffectivelyEnabled:
                    _remoteCombo = cb;
                    var items = cb.Items.Cast<object?>().Select(i => i is ContentControl c ? c.Content?.ToString() ?? "" : i?.ToString() ?? "").ToList();
                    return new PhoneHit("Auswahl", Choices: items, Selected: cb.SelectedIndex);
                case ListBoxItem li:
                    li.IsSelected = true;
                    return new PhoneHit("Eintrag");
                case TabItem ti:
                    ti.IsSelected = true;
                    return new PhoneHit("Reiter");
            }
        }
        return new PhoneHit("nichts");
    }

    /// <summary>Antippen (Anteile 0…1). Auf der Handy-Fläche öffnen Knöpfe ihr Ziel auf dem Handy.</summary>
    public PhoneHit PhoneTapAt(double fx, double fy, bool desktop)
    {
        if (desktop || _phoneSurface is null || _phonePage is null)
        {
            var p = new Point(fx * Bounds.Width, fy * Bounds.Height);
            return (this.InputHitTest(p) as Visual ?? HitDeep(this, p)) is { } hit ? Act(hit) : new PhoneHit("nichts");
        }
        var s = _phoneSurface.Bounds.Size;
        _phoneRouteUntil = DateTime.UtcNow.AddSeconds(4);
        return HitDeep(_phoneSurface, new Point(fx * s.Width, fy * s.Height)) is { } h ? Act(h) : new PhoneHit("nichts");
    }

    /// <summary>Kompatibel: Antippen am PC-Bildschirm, liefert die Art des Treffers.</summary>
    public string PhoneTap(double fx, double fy) => PhoneTapAt(fx, fy, true).Kind;

    TextBox? RemoteBox() => _remoteFocus is { } tb && tb.IsEffectivelyVisible && TopLevel.GetTopLevel(tb) is not null ? tb : FocusManager?.GetFocusedElement() as TextBox;

    /// <summary>Text des angetippten Felds ersetzen (Handy-Tastatur) – mit „\u0001“ davor wird angehängt.</summary>
    public void PhoneSetText(string text)
    {
        if (RemoteBox() is not { } tb || tb.IsReadOnly) return;
        if (text.StartsWith('\u0001')) { PhoneType(text[1..]); return; }
        tb.Text = text; tb.CaretIndex = text.Length;
    }

    /// <summary>Text ins zuletzt angetippte Eingabefeld schreiben (an der Schreibmarke).</summary>
    public void PhoneType(string text)
    {
        if (RemoteBox() is not { } tb || tb.IsReadOnly) return;
        var t = tb.Text ?? ""; var c = Math.Clamp(tb.CaretIndex, 0, t.Length);
        tb.Text = t.Insert(c, text); tb.CaretIndex = c + text.Length;
    }

    public void PhoneChoose(int index)
    {
        if (_remoteCombo is { } cb && index >= 0 && index < cb.ItemCount) { _phoneRouteUntil = DateTime.UtcNow.AddSeconds(4); cb.SelectedIndex = index; }
    }

    /// <summary>Sondertasten vom Handy: Löschen, Enter, Tab (nächstes Feld), Esc.</summary>
    public void PhoneKey(string key)
    {
        var tb = RemoteBox();
        _phoneRouteUntil = _phonePage is not null ? DateTime.UtcNow.AddSeconds(4) : default;
        switch (key)
        {
            case "back" when tb is { IsReadOnly: false }:
                var t = tb.Text ?? ""; var c = Math.Clamp(tb.CaretIndex, 0, t.Length);
                if (c > 0) { tb.Text = t.Remove(c - 1, 1); tb.CaretIndex = c - 1; }
                break;
            case "enter" when tb is { AcceptsReturn: true, IsReadOnly: false }:
                PhoneType("\n");
                break;
            case "enter":
                ((Control?)tb ?? this).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                break;
            case "tab" when tb is not null:
                var boxes = this.GetVisualDescendants().OfType<TextBox>().Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled).ToList();
                if (boxes.IndexOf(tb) is var i and >= 0 && i + 1 < boxes.Count) { boxes[i + 1].Focus(); _remoteFocus = boxes[i + 1]; }
                break;
            case "esc":
                ((Control?)tb ?? this).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
                break;
        }
    }

    /// <summary>Wischen: die Liste/Seite unter dem Finger scrollen (<paramref name="dy"/> in Bildschirmhöhen, positiv = nach unten).</summary>
    public void PhoneScroll(double fx, double fy, double dy, bool desktop = true)
    {
        Visual? hit;
        if (desktop || _phoneSurface is null || _phonePage is null) { var p = new Point(fx * Bounds.Width, fy * Bounds.Height); hit = this.InputHitTest(p) as Visual ?? HitDeep(this, p); }
        else { var s = _phoneSurface.Bounds.Size; hit = HitDeep(_phoneSurface, new Point(fx * s.Width, fy * s.Height)); }
        var sv = hit?.GetSelfAndVisualAncestors().OfType<ScrollViewer>().FirstOrDefault(s => s.Extent.Height > s.Viewport.Height);
        if (sv is null) return;
        sv.Offset = new Vector(sv.Offset.X, Math.Clamp(sv.Offset.Y + dy * sv.Viewport.Height, 0, Math.Max(0, sv.Extent.Height - sv.Viewport.Height)));
    }

    static byte[]? _phoneIcon;
    static byte[]? PhoneIcon()
    {
        if (_phoneIcon is not null) return _phoneIcon;
        try { using var s = AssetLoader.Open(new Uri("avares://TrendoOS/Assets/logo/trendo-512.png")); using var ms = new MemoryStream(); s.CopyTo(ms); return _phoneIcon = ms.ToArray(); }
        catch (Exception e) when (e is FileNotFoundException or IOException or InvalidOperationException) { return null; }
    }

    /// <summary>Am PC sichtbar machen, dass ein Handy gerade bedient (höchstens alle 10 Minuten ein Hinweis je Handy).</summary>
    void PhoneRemoteStarted(string device)
    {
        if (_remoteToast.TryGetValue(device, out var last) && DateTime.UtcNow - last < TimeSpan.FromMinutes(10)) return;
        _remoteToast[device] = DateTime.UtcNow;
        Toast($"📱 {device} nutzt Trendo gerade auf dem Handy.", ("Sperren", new Action(() =>
        {
            if (PhoneAccess.Devices().FirstOrDefault(d => d.Name == device) is { } d) PhoneSetRemote(d.Id, d.Name, false);
            if (CurrentView == "handy") Show("handy");
        })));
    }

    /// <summary>Brücke vom Handy-Server (eigener Thread) in die Oberfläche (UI-Thread).</summary>
    sealed class PhoneRemoteBridge(MainWindow w) : IPhoneRemote
    {
        public IReadOnlyList<PhoneApp> Apps() => Dispatcher.UIThread.Invoke(w.PhoneApps);
        public async Task<string?> OpenAsync(string id) => await Dispatcher.UIThread.InvokeAsync(() => w.PhoneOpen(id));
        public async Task<string?> BackAsync() => await Dispatcher.UIThread.InvokeAsync(w.PhoneBack);
        public async Task<PhoneFrame?> ScreenAsync(int width, int height, double scale, bool desktop) => await Dispatcher.UIThread.InvokeAsync(() => w.PhoneScreen(width, height, scale, desktop));
        public async Task<PhoneHit> TapAsync(double fx, double fy, bool desktop) => await Dispatcher.UIThread.InvokeAsync(() => w.PhoneTapAt(fx, fy, desktop));
        public async Task SetTextAsync(string text) => await Dispatcher.UIThread.InvokeAsync(() => w.PhoneSetText(text));
        public async Task ChooseAsync(int index) => await Dispatcher.UIThread.InvokeAsync(() => w.PhoneChoose(index));
        public async Task KeyAsync(string key) => await Dispatcher.UIThread.InvokeAsync(() => w.PhoneKey(key));
        public async Task ScrollAsync(double fx, double fy, double dy, bool desktop) => await Dispatcher.UIThread.InvokeAsync(() => w.PhoneScroll(fx, fy, dy, desktop));
        public byte[]? Icon() => PhoneIcon();
        public async Task<byte[]?> AppIconAsync(string id) => await Dispatcher.UIThread.InvokeAsync(() => w.PhoneAppIcon(id));
        public void Started(string device) => Dispatcher.UIThread.Post(() => w.PhoneRemoteStarted(device));
    }
}
