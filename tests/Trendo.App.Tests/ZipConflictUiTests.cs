using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0243: Entpacken in der Oberfläche fragt, wenn es Dateien schon gibt – nichts wird still überschrieben.</summary>
public class ZipConflictUiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "T-0243 UI: zweites Entpacken zeigt die Rückfrage; „Beide behalten“ legt eine Kopie an, „Abbrechen“ ändert nichts")]
    public void Unpack_asks()
    {
        var (w, _, root) = Ui.Open();
        var d = Path.Combine(root, "zip"); var src = Path.Combine(d, "fotos"); Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "bild.txt"), "neu");
        var zip = Archives.Pack([src], Path.Combine(d, "fotos.zip"));
        w.Show("alltag-dateien"); Pump();
        w.Find<TextBox>("zip-file").Text = zip;
        w.Find<Button>("zip-unpack").Press(); Pump();
        var target = Path.Combine(d, "fotos", "fotos", "bild.txt");
        Assert.True(File.Exists(target));
        File.WriteAllText(target, "meins");
        w.Find<Button>("zip-unpack").Press(); Pump();
        Assert.NotNull(w.TryFind<Button>("zip-conflict-keep"));
        Assert.Equal("meins", File.ReadAllText(target));
        w.Find<Button>("zip-conflict-cancel").Press(); Pump();
        Assert.Null(w.TryFind<Button>("zip-conflict-keep"));
        Assert.Equal("meins", File.ReadAllText(target));
        w.Find<Button>("zip-unpack").Press(); Pump();
        w.Find<Button>("zip-conflict-keep").Press(); Pump();
        Assert.Equal("meins", File.ReadAllText(target));
        Assert.Equal("neu", File.ReadAllText(Path.Combine(d, "fotos", "fotos", "bild (2).txt")));
        w.Close();
    }
}
