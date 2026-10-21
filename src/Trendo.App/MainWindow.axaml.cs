using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Schlanke Trendo-Oberfläche (P1). Dunkles Premium-Theme nach DESIGN_BRIEF; zeigt nur echte Zustände.</summary>
public sealed class MainWindow : Window
{
    private static readonly IBrush Bg = Brush.Parse("#0B1220");
    private static readonly IBrush Panel = Brush.Parse("#121B2E");
    private static readonly IBrush Accent = Brush.Parse("#3BA7FF");
    private static readonly IBrush Muted = Brush.Parse("#8A9BB8");
    private static readonly IBrush Text = Brush.Parse("#E8EEF8");

    private readonly TrendoCore _core;
    private readonly AppOptions _opt;
    private readonly ContentControl _main = new();
    private readonly TextBlock _status = new() { Foreground = Muted, FontSize = 12 };
    private IReadOnlyList<StudioEntry> _studios = [];

    public MainWindow() : this(new AppOptions(null, null, 0)) { }

    public MainWindow(AppOptions opt)
    {
        _opt = opt;
        _core = new TrendoCore(opt.DataRoot ?? TrendoCore.DefaultDataRoot());
        Title = "Trendo OS – Windows-Vorschau (P1)";
        Width = 1280; Height = 800; Background = Bg;

        var req = TrendoCore.FindRequirements(AppContext.BaseDirectory);
        _studios = req is null ? [] : _core.Studios.Load(req, new DefaultToolProbe());

        var nav = new StackPanel { Spacing = 4, Margin = new Thickness(12) };
        nav.Children.Add(new TextBlock { Text = "Trendo OS", FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Text, Margin = new Thickness(4, 4, 4, 16) });
        foreach (var (label, view) in new (string, Func<Control>)[]
                 { ("Start", StartView), ("Alle Studios", StudiosView), ("Projekte", ProjectsView), ("Freigaben", ApprovalsView), ("System & Sicherheit", SystemView) })
        {
            var b = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Foreground = Text, Padding = new Thickness(12, 8) };
            b.Click += (_, _) => _main.Content = view();
            nav.Children.Add(b);
        }
        var root = new DockPanel();
        var left = new Border { Background = Panel, Width = 230, Child = nav };
        DockPanel.SetDock(left, Dock.Left);
        var bottom = new Border { Background = Panel, Padding = new Thickness(12, 6), Child = _status };
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(left); root.Children.Add(bottom);
        root.Children.Add(new ScrollViewer { Content = _main, Padding = new Thickness(24) });
        Content = root;
        _main.Content = StartView();
        _status.Text = $"Core {_core.Version} · Secrets: {_core.Secrets.BackendName} · Audit-Kette: {(_core.Audit.Verify() ? "intakt" : "FEHLER")} · Trendo-KI: Nicht verbunden";

        Opened += OnOpened;
        Closing += (_, _) => _core.Shutdown();
    }

    private async void OnOpened(object? s, EventArgs e)
    {
        if (_opt.ScreenshotPath is { } path)
        {
            await Task.Delay(1500);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var size = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
                using var bmp = new RenderTargetBitmap(size);
                bmp.Render(this);
                bmp.Save(path);
            });
            _core.Audit.Append("app", "app.screenshot", Path.GetFileName(path));
        }
        if (_opt.AutoExitSeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(_opt.AutoExitSeconds));
            Close();
        }
    }

    private static TextBlock H(string t) => new() { Text = t, FontSize = 26, FontWeight = FontWeight.SemiBold, Foreground = Text, Margin = new Thickness(0, 0, 0, 12) };
    private static TextBlock P(string t, IBrush? c = null) => new() { Text = t, Foreground = c ?? Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2) };
    private static Border Card(Control c) => new() { Background = Panel, CornerRadius = new CornerRadius(10), Padding = new Thickness(16), Margin = new Thickness(0, 0, 12, 12), Child = c };

    private Control StartView()
    {
        var sp = new StackPanel();
        sp.Children.Add(H("Willkommen bei Trendo OS"));
        sp.Children.Add(P("Dies ist der erste Windows-Vertical-Slice (P1). Alle Studios sind genehmigte Spezifikation – angezeigt wird nur der echte Status."));
        var counts = _studios.GroupBy(x => x.Capability).ToDictionary(g => g.Key, g => g.Count());
        var wrap = new WrapPanel();
        foreach (var c in Enum.GetValues<Capability>())
            wrap.Children.Add(Card(new StackPanel { Width = 150, Children = {
                new TextBlock { Text = counts.GetValueOrDefault(c).ToString(), FontSize = 30, Foreground = Accent },
                P(StudioRegistry.GermanLabel(c)) } }));
        sp.Children.Add(wrap);
        sp.Children.Add(Card(new StackPanel { Children = { new TextBlock { Text = "Trendo KI-Assistent", Foreground = Text, FontWeight = FontWeight.SemiBold }, P("Nicht verbunden – in P1 nicht enthalten. Kein vorgetäuschter Online-Status.") } }));
        return sp;
    }

    private Control StudiosView()
    {
        var sp = new StackPanel();
        sp.Children.Add(H("Alle Studios"));
        if (_studios.Count == 0) sp.Children.Add(P("requirements.yaml nicht gefunden – Status unbekannt.", Brushes.OrangeRed));
        var wrap = new WrapPanel();
        foreach (var st in _studios)
        {
            var col = st.Capability switch { Capability.Ready => Brushes.LimeGreen, Capability.Blocked => Brushes.OrangeRed, Capability.Rejected => Brushes.Gray, Capability.NotConnected => Brushes.Gold, _ => Muted };
            wrap.Children.Add(Card(new StackPanel { Width = 230, Children = {
                new TextBlock { Text = st.Name, Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                P($"{st.Id} · {st.ApprovalStatus}"),
                new TextBlock { Text = StudioRegistry.GermanLabel(st.Capability), Foreground = col, Margin = new Thickness(0, 6, 0, 0) },
                P(st.Reason) } }));
        }
        sp.Children.Add(wrap);
        return sp;
    }

    private Control ProjectsView()
    {
        var sp = new StackPanel();
        sp.Children.Add(H("Projekte"));
        var id = new TextBox { PlaceholderText = "projekt-id (a-z, 0-9, -)", Width = 260 };
        var create = new Button { Content = "Projekt anlegen" };
        var msg = P("");
        create.Click += (_, _) =>
        {
            try { _core.Projects.Create(id.Text ?? "", id.Text ?? ""); _main.Content = ProjectsView(); }
            catch (Exception ex) { msg.Text = ex.Message; }
        };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { id, create } });
        sp.Children.Add(msg);
        foreach (var p in _core.Projects.List())
        {
            var open = new Button { Content = "Ordner im Explorer öffnen (Freigabe nötig)" };
            open.Click += (_, _) => { _core.Integrations.PlanOpenProjectFolder(p.Id); _main.Content = ApprovalsView(); };
            sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { new TextBlock { Text = p.Name, Foreground = Text }, P($"Angelegt {p.CreatedAt:dd.MM.yyyy HH:mm}"), open } }));
        }
        var url = new Button { Content = "Trendo-Repository im Browser öffnen (Freigabe nötig)" };
        url.Click += (_, _) => { _core.Integrations.PlanOpenUrl("https://github.com/manuelbrandner85/Trendo-OS"); _main.Content = ApprovalsView(); };
        sp.Children.Add(url);
        return sp;
    }

    private Control ApprovalsView()
    {
        var sp = new StackPanel();
        sp.Children.Add(H("Freigaben"));
        var pending = _core.Approvals.Pending();
        if (pending.Count == 0) sp.Children.Add(P("Keine offenen Freigaben."));
        foreach (var r in pending)
        {
            var yes = new Button { Content = "Freigeben und ausführen", Background = Accent };
            var no = new Button { Content = "Ablehnen" };
            yes.Click += (_, _) => { _core.Approvals.Approve(r.Id); _core.Integrations.Execute(r); _main.Content = ApprovalsView(); };
            no.Click += (_, _) => { _core.Approvals.Deny(r.Id); _main.Content = ApprovalsView(); };
            sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = r.Description, Foreground = Text },
                P($"Aktion {r.Action} · gültig bis {r.ExpiresAt:HH:mm}"),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { yes, no } } } }));
        }
        return sp;
    }

    private Control SystemView()
    {
        var sp = new StackPanel();
        sp.Children.Add(H("System & Sicherheit"));
        sp.Children.Add(P($"Datenordner: lokal im Benutzerprofil (TrendoOS)"));
        sp.Children.Add(P($"Secrets-Backend: {_core.Secrets.BackendName}"));
        sp.Children.Add(P($"Audit-Log: {_core.Audit.ReadAll().Count} Einträge, Hash-Kette {(_core.Audit.Verify() ? "intakt" : "BESCHÄDIGT")}"));
        foreach (var e in _core.Audit.ReadAll().TakeLast(15).Reverse())
            sp.Children.Add(P($"{e.At:HH:mm:ss}  {e.Actor}  {e.Action}  {e.Detail}"));
        return sp;
    }
}
