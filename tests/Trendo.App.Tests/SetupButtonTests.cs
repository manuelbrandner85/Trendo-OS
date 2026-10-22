using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

public class W0_Einrichtung_Ui
{
    static string AllText(Window w) => string.Join("\n", w.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

    [AvaloniaFact(DisplayName = "T-0210: Auswahl sichtbar, Design sofort, Ordnerdialog, Reparieren fragt vor dem Installieren")]
    public async Task Auswahl_und_reparieren()
    {
        var (w, _, root) = Ui.Open();
        var runner = new FakeRunner(); w.Deps.Runner = runner;

        w.Show("einrichtung-2");
        Assert.Contains("Aussehen: Dunkel Premium", w.Find<TextBlock>("wiz-choice").Text);
        Assert.Contains("Pakete:", w.Find<TextBlock>("wiz-choice").Text);
        Assert.StartsWith("✓", w.Find<RadioButton>("wiz-theme-dunkel").Content as string);
        w.Find<RadioButton>("wiz-theme-hell").IsChecked = true;
        Assert.Contains("Aussehen: Hell", w.Find<TextBlock>("wiz-choice").Text);
        Assert.StartsWith("✓", w.Find<RadioButton>("wiz-theme-hell").Content as string);
        Assert.Equal(2, w.Find<RadioButton>("wiz-theme-hell").BorderThickness.Left);
        Assert.Equal(Color.Parse("#0F1B2E"), Assert.IsAssignableFrom<ISolidColorBrush>(w.Find<TextBlock>("wiz-choice").Foreground).Color);

        w.Show("einrichtung-3");
        Assert.Contains("Büro & Briefe", w.Find<TextBlock>("wiz-choice").Text);
        w.Find<CheckBox>("wiz-pack-office").IsChecked = false;
        Assert.DoesNotContain("Büro", w.Find<TextBlock>("wiz-choice").Text);
        Assert.StartsWith("✓", PackName(w, "wiz-pack-ki"));
        w.Find<CheckBox>("wiz-pack-office").IsChecked = true;

        w.Show("einrichtung-1");
        var dir = Path.Combine(root, "MeinOrdner"); Directory.CreateDirectory(dir);
        w.WizFolderDialog = _ => Task.FromResult<string?>(dir);
        w.Find<Button>("wiz-folder-pick").Press();
        Assert.Equal(dir, w.Find<TextBox>("wiz-folder").Text);
        Assert.Contains("gewählt", w.Find<TextBlock>("wiz-folder-msg").Text ?? "");
        w.WizFolderDialog = _ => Task.FromResult<string?>(null);
        w.Find<Button>("wiz-folder-pick").Press();
        Assert.Contains("Kein Ordner", w.Find<TextBlock>("wiz-folder-msg").Text ?? "");

        w.Show("systemcheck");
        for (var i = 0; i < 100 && w.TryFind<Button>("fix-sumatra") is null; i++) { await Task.Delay(30); Dispatcher.UIThread.RunJobs(); }
        foreach (var id in new[] { "sumatra", "mpv", "7zip", "ollama", "model", "libreoffice" })
        {
            var b = w.TryFind<Button>("fix-" + id);
            Assert.NotNull(b);
            b!.Press();
            for (var i = 0; i < 80 && !((w.TryFind<TextBlock>("fix-status")?.Text ?? "").Contains("repariert") || (w.TryFind<TextBlock>("fix-status")?.Text ?? "").Contains("fehlt")); i++)
            { await Task.Delay(25); Dispatcher.UIThread.RunJobs(); }
            var status = w.Find<TextBlock>("fix-status").Text ?? "";
            Assert.True(status.Contains("repariert") || status.Contains("fehlt – Installieren?"), status);
        }
        Assert.DoesNotContain(runner.Calls, c => c.Contains("winget", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(w.TryFind<Button>("fix-install-libreoffice"));
        w.Find<Button>("fix-install-libreoffice").Press();
        for (var i = 0; i < 80 && !runner.Calls.Any(c => c.Contains("winget", StringComparison.OrdinalIgnoreCase)); i++)
        { await Task.Delay(25); Dispatcher.UIThread.RunJobs(); }
        Assert.Contains(runner.Calls, c => c.Contains("winget", StringComparison.OrdinalIgnoreCase) && c.Contains("LibreOffice"));

        w.Show("heilung");
        w.Find<Button>("heal-run").Press();
        var heal = AllText(w);
        Assert.True(heal.Contains("Prüfung") || heal.Contains("geprüft"), heal);
        w.Close();
    }

    static string PackName(MainWindow w, string name)
    {
        var box = w.Find<CheckBox>(name);
        return box.GetLogicalDescendants().OfType<TextBlock>().First().Text ?? "";
    }

    [AvaloniaFact(DisplayName = "T-0210: Jeder sichtbare Knopf in Einrichtung, Systemcheck und Selbstheilung wirkt", Timeout = 180000)]
    public async Task Jeder_Knopf_wirkt()
    {
        var (w, l, root) = Ui.Open();
        w.Deps.Runner = new FakeRunner();
        w.Show("systemcheck");
        for (var i = 0; i < 100 && w.TryFind<Button>("fix-sumatra") is null; i++) { await Task.Delay(30); Dispatcher.UIThread.RunJobs(); }
        Assert.NotNull(w.TryFind<Button>("fix-sumatra"));
        var fehl = Probe(w, l, root, "einrichtung", "einrichtung-1", "systemcheck", "heilung");
        w.Close();
        Assert.True(fehl.Count == 0, "Knopf ohne Wirkung oder Absturz:\n" + string.Join("\n", fehl));
    }

    static List<string> Probe(MainWindow w, FakeLauncher launcher, string root, params string[] pages)
    {
        var failures = new List<string>();
        foreach (var page in pages)
        {
            w.Show(page); Pump();
            var body = Body(w, page);
            if (body is null) { failures.Add(page + ": Seiteninhalt nicht gefunden"); continue; }
            var buttons = body.GetLogicalDescendants().OfType<Button>().Where(b => b.IsVisible && b.IsEnabled).ToList();
            if (buttons.Count == 0) { failures.Add(page + ": kein sichtbarer aktiver Knopf"); continue; }
            var seen = new HashSet<string>();
            foreach (var spec in buttons)
            {
                var name = spec.Name ?? "";
                var label = spec.Content as string ?? string.Join(" ", spec.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
                if (!seen.Add(name.Length > 0 ? name : label)) continue;
                if (name.EndsWith("-tab-" + page, StringComparison.Ordinal)) continue;
                if (name == "sys-tab-" + page) continue;
                if (name.StartsWith("fix-install", StringComparison.Ordinal)) continue;
                if (label.Contains("installieren", StringComparison.OrdinalIgnoreCase)) continue;
                w.Show(page); Pump();
                body = Body(w, page);
                if (body is null) { failures.Add($"{page}\t{name}\tSeite fehlt"); continue; }
                var b = name.Length > 0
                    ? body.GetLogicalDescendants().OfType<Button>().FirstOrDefault(x => x.Name == name && x.IsVisible && x.IsEnabled)
                    : body.GetLogicalDescendants().OfType<Button>().FirstOrDefault(x => string.IsNullOrEmpty(x.Name) && (x.Content as string) == label && x.IsVisible && x.IsEnabled);
                if (b is null) continue;
                var t0 = Texts(body); var v0 = w.CurrentView; var l0 = launcher.Opened.Count; var f0 = Stamp(root);
                try
                {
                    b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
                    var now = Body(w, page) ?? body;
                    var eff = w.CurrentView != v0 || launcher.Opened.Count > l0 || Stamp(root) != f0 || Texts(now) != t0;
                    if (!eff) failures.Add($"{page}\t{name}\t{label}\tohne Wirkung");
                }
                catch (Exception ex) { failures.Add($"{page}\t{name}\t{label}\tAbsturz {ex.GetType().Name}: {ex.Message}"); }
            }
        }
        return failures;
    }

    static ContentControl? Body(MainWindow w, string page) =>
        w.GetLogicalDescendants().OfType<ContentControl>().FirstOrDefault(c => c.Name == "win-body-" + w.WindowGroupOf(page));

    static string Texts(Control root)
    {
        var sb = new StringBuilder();
        foreach (var c in root.GetLogicalDescendants())
        {
            if (c is TextBlock t) sb.Append(t.Text).Append('|');
            else if (c is SelectableTextBlock s) sb.Append(s.Text).Append('|');
            else if (c is TextBox x) sb.Append(x.Text).Append('|');
            else if (c is Button btn && btn.Content is string label) sb.Append(label).Append('|');
        }
        return sb.ToString();
    }

    static long Stamp(string root)
    {
        if (!Directory.Exists(root)) return 0;
        long n = 0;
        foreach (var f in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(f).Contains("audit", StringComparison.OrdinalIgnoreCase)) continue;
            try { n += File.GetLastWriteTimeUtc(f).Ticks % 1000003 + 1; } catch (IOException) { }
        }
        return n;
    }

    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }
}
