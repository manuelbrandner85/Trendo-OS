using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Wunsch 27.10.2026: „Felder, in die man Ordner oder Dateien eingeben muss, sollen sich direkt öffnen – nicht den Pfad eingeben“.</summary>
public class PathPickAuditTests
{
    static readonly string[] Hints = ["ordner", "datei", "pfad", "verzeichnis", @":\"];

    [AvaloniaFact(DisplayName = "Pfadfelder: auf keiner Seite muss man einen Ordner- oder Dateipfad eintippen – ein Klick öffnet die Auswahl")]
    public void No_typed_paths()
    {
        var (w, _, _) = Ui.Open();
        var bad = new List<string>();
        foreach (var key in w.AllPageKeys())
        {
            w.Show(key); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            if (w.ActivePageRootForTests is not { } root) continue;
            var known = w.PathFieldsIn(root);
            foreach (var tb in root.GetLogicalDescendants().OfType<TextBox>())
            {
                var h = ((tb.PlaceholderText ?? "") + " " + (tb.Name ?? "")).ToLowerInvariant();
                if (!Hints.Any(h.Contains) || h.Contains("http") || h.Contains("prüfsumme") || h.Contains("muster") || h.Contains("name") || h.Contains("parameter") || tb.Name == "bm-folder") continue; // Lesezeichen-Ordner und Startparameter sind keine Pfade
                if (!known.Contains(tb)) bad.Add($"{key}: {tb.Name} „{tb.PlaceholderText}“");
                else if (!tb.IsReadOnly) bad.Add($"{key}: {tb.Name} ist noch zum Eintippen");
            }
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad.Distinct()));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Pfadfelder: Klick ins Feld öffnet die Ordnerauswahl, die Wahl landet im Feld")]
    public async Task Click_opens_picker()
    {
        var (w, _, root) = Ui.Open();
        var ziel = Path.Combine(root, "Meine Projekte"); Directory.CreateDirectory(ziel);
        MainWindow.PickRequest? frage = null;
        w.PickOverride = r => { frage = r; return Task.FromResult<string[]?>([ziel]); };
        w.Show("einrichtung");
        for (var i = 0; i < 9 && w.TryFind<TextBox>("wiz-folder") is null; i++) w.Find<Button>("wiz-next").Press();
        var tb = w.Find<TextBox>("wiz-folder");
        Assert.True(tb.IsReadOnly);
        tb.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
        for (var i = 0; i < 20 && tb.Text != ziel; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        Assert.True(frage?.Folder);
        Assert.Equal(ziel, tb.Text);
        w.Close();
    }
}
