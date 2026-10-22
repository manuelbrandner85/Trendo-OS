using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

public class W5_Bus_Eintraege_Ui
{
    static string Text(Window w) => string.Join("\n", w.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

    [AvaloniaFact(DisplayName = "T-0208: Leere Pflichtfelder melden sich, Einträge erscheinen, ändern und löschen")]
    public void Anlegen_aendern_loeschen()
    {
        var (w, _, root) = Ui.Open();
        w.Show("biz-betrieb");
        w.Find<Button>("op-t-add").Press();
        Assert.Contains("Tarif", Text(w));
        Assert.Equal(2, w.Find<TextBox>("op-t-name").BorderThickness.Left);
        w.Find<TextBox>("op-t-name").Text = "Basis"; w.Find<TextBox>("op-t-price").Text = "12"; w.Find<Button>("op-t-add").Press();
        Assert.Contains("Basis", w.Find<TextBlock>("op-t-row-0").Text);
        w.Find<Button>("op-t-edit-0").Press();
        Assert.Equal("Basis", w.Find<TextBox>("op-t-name").Text);
        w.Find<TextBox>("op-t-name").Text = "Plus"; w.Find<Button>("op-t-add").Press();
        Assert.Equal("Plus", w.BizTiers.All()[0].Name);
        w.Find<Button>("op-t-del-0").Press();
        Assert.Empty(w.BizTiers.All());

        w.Find<TextBox>("op-c-name").Text = "Anna"; w.Find<TextBox>("op-c-next").Text = DateTime.Today.ToString("dd.MM.yyyy"); w.Find<Button>("op-c-add").Press();
        w.Find<Button>("op-c-edit-0").Press();
        w.Find<TextBox>("op-c-name").Text = "Bea"; w.Find<Button>("op-c-add").Press();
        Assert.Contains("Bea", w.Find<TextBlock>("op-due").Text);
        w.Find<Button>("op-c-del-0").Press();
        Assert.Empty(w.BizContacts.All());

        w.Find<Button>("op-s-import").Press();
        Assert.Contains("CSV", Text(w));
        var csv = Path.Combine(root, "zahlen.csv"); File.WriteAllText(csv, "01.10.2026;10;2;1\n");
        w.BizFileDialog = _ => Task.FromResult<string?>(csv);
        w.Find<TextBox>("op-s-csv").Text = "";
        w.Find<Button>("op-s-import").Press();
        Assert.Contains("1 Tag übernommen", Text(w));
        Assert.Single(w.BizStats.All());
        w.Find<TextBox>("op-s-csv").Text = "02.10.2026;8;1;1";
        w.Find<Button>("op-s-import").Press();
        Assert.Equal(2, w.BizStats.All().Count);

        w.Show("biz-marke");
        w.Find<Button>("br-seo-check").Press();
        Assert.Contains("Suchwort", Text(w));
        w.Find<TextBox>("br-color").Text = "blau"; w.Find<Button>("br-palette").Press();
        Assert.Contains("#RRGGBB", Text(w));
        w.Find<TextBox>("br-p-what").Text = "antwortet"; w.Find<Button>("br-p-add").Press();
        Assert.Single(w.ProcessSteps.All());
        w.Find<Button>("br-p-del-0").Press();
        Assert.Empty(w.ProcessSteps.All());

        w.Show("biz-markt");
        w.Find<Button>("mk-c-add").Press();
        Assert.Contains("Mitbewerber", Text(w));
        w.Find<TextBox>("mk-p-text").Text = "Kein Termin"; w.Find<Button>("mk-p-add").Press();
        w.Find<Button>("mk-p-del-0").Press();
        Assert.Empty(w.Problems.All());
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0208: Jeder sichtbare Knopf auf Betrieb, Marke und Markt wirkt", Timeout = 180000)]
    public void Jeder_Knopf_wirkt()
    {
        var (w, l, root) = Ui.Open();
        var fehl = Probe(w, l, root, "biz-betrieb", "biz-marke", "biz-markt");
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

    static void Pump() { for (var i = 0; i < 6; i++) Dispatcher.UIThread.RunJobs(); }
}
