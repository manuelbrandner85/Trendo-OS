using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Welle 1: SYS-03 Browser & Downloads.</summary>
public partial class MainWindow
{
    private Control DownloadsView()
    {
        var sp = new StackPanel { Spacing = 5 };
        var browsers = Browsers.Find();
        var url = new TextBox { Name = "web-url", PlaceholderText = "Adresse oder Suchbegriff, z. B. tagesschau.de", Width = 420, HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = !_core.Offline };
        var msg = P(_core.Offline ? "Offline-Modus: Internet ist ausgeschaltet." : ""); msg.Name = "web-msg";
        var row = new WrapPanel();
        void Go(string? exe)
        {
            var u = Browsers.Normalize(url.Text ?? "");
            if (u is null) { msg.Text = "Diese Adresse öffnet Trendo nicht (nur http und https)."; return; }
            if (exe is null) Launch(u.AbsoluteUri); else _core.Integrations.LaunchDirect(Principal.LocalOwner, exe, _core.Audit, u.AbsoluteUri);
            msg.Text = $"Geöffnet: {u.Host}";
        }
        url.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Go(null); };
        var std = new Button { Name = "web-open", Content = "Im Standardbrowser öffnen", Background = Accent, Margin = new Thickness(0, 0, 6, 6), IsEnabled = !_core.Offline };
        std.Click += (_, _) => Go(null); row.Children.Add(std);
        foreach (var b in browsers)
        {
            var btn = new Button { Name = "web-" + b.Id, Content = b.Name, Margin = new Thickness(0, 0, 6, 6), IsEnabled = !_core.Offline };
            btn.Click += (_, _) => Go(b.Path); row.Children.Add(btn);
        }
        sp.Children.Add(new StackPanel { Spacing = 6, Children = { H("Internet", 15), url, row, msg,
            P(browsers.Count == 0 ? "Kein bekannter Browser gefunden – der Windows-Standard wird benutzt." : $"Gefunden: {string.Join(", ", browsers.Select(b => b.Name))}. Suchbegriffe gehen an DuckDuckGo (ohne Tracking).", null, 11) } });

        var dl = Browsers.DownloadsFolder();
        var files = FileShelf.Recent([dl], null, 30, depth: 0);
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H("Downloads", 15) } };
        var openDir = new Button { Name = "dl-folder", Content = "Ordner öffnen", FontSize = 11 }; openDir.Click += (_, _) => Launch(dl);
        head.Children.Add(Pos(openDir, 1));
        sp.Children.Add(head);
        var total = files.Sum(f => f.Length);
        sp.Children.Add(P(files.Count == 0 ? "Der Download-Ordner ist leer." : $"Neueste {files.Count} Dateien, zusammen {ResourceMonitor.Size(total)}. Trendo löscht nichts von selbst.", null, 11));
        var i = 0;
        foreach (var f in files)
        {
            var r = FileRow(f, i++, "dl");
            if (Browsers.Unfinished(f.Name)) sp.Children.Add(new StackPanel { Children = { r, P("   ⏳ noch nicht fertig heruntergeladen", Brush.Parse("#FACC15"), 10.5) } });
            else if (Browsers.Risky(f.Name)) sp.Children.Add(new StackPanel { Children = { r, P("   ⚠ Programmdatei – nur öffnen, wenn du der Quelle vertraust", Brush.Parse("#FACC15"), 10.5) } });
            else sp.Children.Add(r);
        }
        return AppsPage("Browser & Downloads", sp);
    }
}
