using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core.Shell;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;
namespace Trendo.App.Tests;

/// <summary>T-0201: echte Dateiauswahl und Drag &amp; Drop für alle Pfadfelder.</summary>
public class T0201_Dateiauswahl_Ui
{
    [AvaloniaFact(DisplayName = "T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“")]
    public void EveryPathFieldHasPicker()
    {
        var (w, _, _) = Ui.Open();
        var missing = new List<string>();
        var fehler = new List<string>();
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        void Scan(string key)
        {
            w.Show(key); Dispatcher.UIThread.RunJobs();
            var root = w.ActivePageRoot;
            if (root is null) { fehler.Add(key + " ohne Seite"); return; }
            if (root.Name == "error-page") { fehler.Add(key); return; }
            foreach (var box in root.GetLogicalDescendants().OfType<TextBox>())
            {
                var hint = box.PlaceholderText ?? "";
                var kind = PathFields.Classify(box.Name, hint);
                if (kind == PathFields.Kind.None) continue;
                if (string.IsNullOrEmpty(box.Name)) { missing.Add($"{key}: Feld ohne Namen ({hint})"); continue; }
                void Need(string suffix, string label)
                {
                    var b = root.GetLogicalDescendants().OfType<Button>().FirstOrDefault(x => x.Name == box.Name + suffix);
                    if (b is null || !Equals(b.Content, label)) missing.Add($"{key}: {box.Name} ({hint}) ohne „{label}“");
                    else gesehen.Add(box.Name!);
                }
                if (kind is PathFields.Kind.File or PathFields.Kind.FileOrFolder) Need("-pick", "Datei wählen …");
                if (kind is PathFields.Kind.Folder or PathFields.Kind.FileOrFolder) Need("-pickdir", "Ordner wählen …");
            }
        }
        foreach (var key in w.PageKeys.OrderBy(k => k, StringComparer.Ordinal).ToList()) Scan(key);
        w.Show("ablaeufe"); w.Find<Button>("wf-new").Press(); Scan("ablaeufe");
        w.Show("produktion"); w.Find<Button>("fusion-recipe-video-pack").Press(); Scan("produktion");
        w.Show("baukasten"); w.Find<Button>("builder-add-img").Press(); Scan("baukasten");
        w.Show("forensik");
        w.Find<TextBox>("for-case-title").Text = "Beweis";
        w.Find<Button>("for-case-new").Press();
        Scan("forensik");
        w.Show("nachbau");
        w.Find<TextBox>("clean-name").Text = "Probe";
        w.Find<Button>("clean-create").Press();
        Scan("nachbau");
        w.ShareAddressOverride = System.Net.IPAddress.Loopback;
        Scan("teilen");
        typeof(MainWindow).GetProperty(nameof(MainWindow.Phones))!.SetValue(w, new[] { new Trendo.Core.PhoneDevice("test", "device", "Probe") });
        Scan("handy");
        foreach (var name in new[] { "wissen-folder", "ext-path", "sec-folder", "for-file", "for-add-file-0", "contact-file", "share-folder", "clean-theirs", "wfe-at", "wfe-from", "wfe-to", "pdf-merge-files", "pkg-att", "pkg-main", "phone-file-0", "settings-file", "hash-file", "zip-src", "wiz-folder" })
            if (!gesehen.Contains(name)) missing.Add("erwartet, aber nie mit Auswahl gesehen: " + name);
        if (!gesehen.Any(n => n.StartsWith("builder-url-", StringComparison.Ordinal)))
            missing.Add("erwartet, aber nie mit Auswahl gesehen: builder-url- (Bild im Baukasten)");
        Assert.True(fehler.Count == 0, "Seiten mit Fehler statt Inhalt:\n" + string.Join("\n", fehler));
        Assert.True(missing.Count == 0, "Pfadfelder ohne Auswahl:\n" + string.Join("\n", missing));
        Assert.True(gesehen.Count >= 36, $"nur {gesehen.Count} Felder gefunden");
        w.Close();
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
        w.Show("ki-wissen"); Settle();
        Assert.True(w.DropPaths(V<TextBox>(w, "wissen-folder"), [a]));
        Assert.Equal(a, V<TextBox>(w, "wissen-folder").Text);
        w.Show("doku-schreiben"); Settle();
        Assert.True(w.DropPaths(V<TextBox>(w, "pkg-att"), [b]));
        Assert.Equal(b, V<TextBox>(w, "pkg-att").Text);
    }
}
