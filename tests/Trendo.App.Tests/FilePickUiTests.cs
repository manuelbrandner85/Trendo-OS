using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;
namespace Trendo.App.Tests;

/// <summary>T-0201: echte Dateiauswahl und Drag &amp; Drop für alle Pfadfelder.</summary>
public class T0201_Dateiauswahl_Ui
{
    [AvaloniaFact(DisplayName = "T-0201: jedes Datei-/Ordnerfeld auf jeder Seite hat „Datei wählen …“ bzw. „Ordner wählen …“")]
    public void EveryPathFieldHasPicker()
    {
        var (w, _, _) = Ui.Open();
        var seen = new HashSet<string>(); var missing = new List<string>();
        foreach (var k in w.PageKeys.OrderBy(x => x).ToList())
        {
            w.Show(k); Dispatcher.UIThread.RunJobs();
            foreach (var t in w.GetLogicalDescendants().OfType<TextBox>().Where(t => t.Name is { } n && System.Text.RegularExpressions.Regex.IsMatch(n, @"-(file\d?|files|folder|path)$") && n != "bm-folder"))
            {
                seen.Add(t.Name!);
                var row = t.Parent as StackPanel;
                if (row?.Children.OfType<Button>().Any(b => b.Name == t.Name + "-pick" || b.Name == t.Name + "-pickdir") != true) missing.Add($"{k}: {t.Name}");
            }
        }
        Assert.Empty(missing);
        Assert.True(seen.Count >= 36, $"nur {seen.Count} Felder gefunden");
    }

    [AvaloniaFact(DisplayName = "T-0201: Mausklick auf „Datei wählen …“ füllt das Feld, die Datei steht danach unter „Zuletzt“; Eintippen geht weiter")]
    public void ClickPicksAndRemembers()
    {
        var (w, _, root) = Ui.Open();
        var f = Path.Combine(root, "messung.csv"); File.WriteAllText(f, "a;b\n1;2\n");
        MainWindow.PickRequest? asked = null;
        w.PickOverride = r => { asked = r; return Task.FromResult<string[]?>([f]); };
        w.Show("wissenschaft"); Settle();
        var tb = V<TextBox>(w, "sci-t-file");
        Assert.False(V<Button>(w, "sci-t-file-pick").GetVisualParent()!.GetVisualChildren().OfType<Button>().First(b => b.Name == "sci-t-file-recent").IsVisible);
        var pick = V<Button>(w, "sci-t-file-pick"); pick.BringIntoView(); Settle();
        RealClick(w, pick);
        Assert.Equal(f, tb.Text);
        Assert.NotNull(asked); Assert.False(asked!.Folder); Assert.Contains("csv", asked.Extensions);
        Assert.True(w.GetLogicalDescendants().OfType<Button>().First(b => b.Name == "sci-desc-file-recent").IsVisible);
        tb.Text = "/eigener/pfad.csv"; Assert.Equal("/eigener/pfad.csv", tb.Text);
    }

    [AvaloniaFact(DisplayName = "T-0201: Ordnerfeld öffnet den Ordner-Dialog")]
    public void FolderPick()
    {
        var (w, _, root) = Ui.Open();
        bool? folder = null; w.PickOverride = r => { folder = r.Folder; return Task.FromResult<string[]?>([root]); };
        w.Show("alltag-dateien"); Settle();
        RealClick(w, V<Button>(w, "dup-folder-pickdir"));
        Assert.True(folder); Assert.Equal(root, V<TextBox>(w, "dup-folder").Text);
    }

    [AvaloniaFact(DisplayName = "T-0201: Datei aufs Feld oder aufs Studio ziehen füllt das passende Feld")]
    public void DropFills()
    {
        var (w, _, root) = Ui.Open();
        var a = Path.Combine(root, "a.csv"); var b = Path.Combine(root, "b.csv"); File.WriteAllText(a, ""); File.WriteAllText(b, "");
        w.Show("wissenschaft"); Settle();
        var fit = V<TextBox>(w, "sci-fit-file");
        Assert.True(w.DropPaths(fit, [a])); Assert.Equal(a, fit.Text);
        var desc = V<TextBox>(w, "sci-desc-file");
        Assert.True(w.DropPaths(w.ActivePageRoot!, [b]));
        Assert.Equal(b, w.PathFieldsIn(w.ActivePageRoot!).First().Text);
        w.Show("alltag-dateien"); Settle();
        Assert.True(w.DropPaths(V<TextBox>(w, "dup-folder"), [a]));
        Assert.Equal(root, V<TextBox>(w, "dup-folder").Text); // Datei auf Ordnerfeld → ihr Ordner
    }
}
