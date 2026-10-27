using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    string? _phonePage;
    readonly List<string> _phoneHistory = [];
    DateTime _phoneRouteUntil;
    /// <summary>Seite, die gerade auf dem Handy offen ist (null = Handy-Startbildschirm).</summary>
    public string? PhonePage => _phonePage;

    void InitPhoneSurface(Grid root)
    {
        _phoneSurface = new Border { Name = "phone-surface", Background = Brush.Parse("#0A1428"), ClipToBounds = true };
        _phoneHolder = new Border { Width = 412, Height = 800, Child = _phoneSurface };
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
        if (content is ScrollViewer own) own.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _phoneSurface.Child = content is ScrollViewer ? content : new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(10, 8), Child = content },
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, // Handy-Breite: Inhalt bricht um wie in einer App
        };
        AfterShow(key);
    }

    /// <summary>Für Tests: Seite direkt auf der Handy-Fläche öffnen.</summary>
    public void PresentPhoneForTests(string key) => PresentPhone(key);

    /// <summary>Apps für das Handy: alle Studios und Programme aus dem Startmenü, mit Bereich und Symbol.</summary>
    public IReadOnlyList<PhoneApp> PhoneApps() =>
        MenuStudios().Select(s => (s.Id, s.Name, Area: StudioCatalog.For(s.Id)?.Area ?? "Weitere"))
            .Select(s => new PhoneApp(s.Id, s.Name, s.Area, AreaIcon(s.Area))).ToList();

    /// <summary>App auf dem Handy öffnen (wie im Startmenü, nur auf der Handy-Fläche).</summary>
    public async Task<string?> PhoneOpen(string id)
    {
        if (PhoneApps().FirstOrDefault(a => a.Id == id) is not { } app) return null;
        _phoneHistory.Clear();
        _phoneRouteUntil = DateTime.UtcNow.AddSeconds(6);
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
        if (_phoneHolder is null || (_phoneHolder.Width == width && _phoneHolder.Height == height)) return;
        _phoneHolder.Width = width; _phoneHolder.Height = height;
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
        _phoneSurface.Measure(new Size(width, height)); _phoneSurface.Arrange(new Rect(0, 0, width, height));
        return new PhoneFrame(Png(_phoneSurface, new Size(width, height), scale), PageTitle(_phonePage));
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
        public void Started(string device) => Dispatcher.UIThread.Post(() => w.PhoneRemoteStarted(device));
    }
}
