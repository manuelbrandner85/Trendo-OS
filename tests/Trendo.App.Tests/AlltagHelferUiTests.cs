using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using SkiaSharp;
using Xunit;

namespace Trendo.App.Tests;

public class AlltagHelfer_Ui
{
    static string Text(Window w) => string.Join("\n", w.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

    [AvaloniaFact(DisplayName = "T-0209: Geld umrechnen und Pipette stürzen bei leerem Feld nicht ab")]
    public void Leere_Felder_ohne_Absturz()
    {
        var (w, _, root) = Ui.Open();
        w.Show("alltag-helfer");
        w.Find<Button>("cur-run").Press();
        Assert.Contains("Betrag", Text(w));
        w.Find<TextBox>("cur-amount").Text = "abc";
        w.Find<TextBox>("cur-rate").Text = "1,08";
        w.Find<Button>("cur-run").Press();
        Assert.Contains("keine Zahl", Text(w));
        w.Find<TextBox>("cur-amount").Text = "10";
        w.Find<TextBox>("cur-rate").Text = "1,08";
        w.Find<Button>("cur-run").Press();
        Assert.Contains("10,8", w.Find<TextBlock>("unit-result").Text);

        w.Find<Button>("color-copy").Press();
        Assert.Contains("Farbe", Text(w));
        w.Find<Button>("color-pick").Press();
        Assert.Contains("Pipette", Text(w));

        var png = Path.Combine(root, "pipette.png");
        using (var b = new SKBitmap(8, 8))
        using (var c = new SKCanvas(b)) { c.Clear(new SKColor(47, 134, 246)); using var img = SKImage.FromBitmap(b); using var data = img.Encode(SKEncodedImageFormat.Png, 100); File.WriteAllBytes(png, data.ToArray()); }
        w.Show("alltag-helfer");
        w.Find<TextBox>("color-file").Text = png;
        w.Find<TextBox>("color-xy").Text = "";
        w.Find<Button>("color-pick").Press();
        Assert.Contains("x,y", Text(w));
        w.Find<TextBox>("color-file").Text = png;
        w.Find<TextBox>("color-xy").Text = "1,1";
        w.Find<Button>("color-pick").Press();
        Assert.Contains("#2F86F6", Text(w));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0209: Papierkorb und Speicherplatz melden sich, Ordnerwahl per Dialog")]
    public void Papierkorb_und_Ordner()
    {
        var (w, _, root) = Ui.Open();
        var dir = Path.Combine(root, "platz"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "a.txt"), "hallo");
        w.UseFolderDialog(_ => Task.FromResult<string?>(dir));
        w.Show("alltag-dateien");
        w.Find<Button>("trash-show").Press();
        var t = Text(w);
        Assert.True(t.Contains("leer") || t.Contains("nicht verfügbar") || t.Contains("Einträge"), t);
        w.Find<Button>("trash-empty").Press();
        Assert.Contains("Leeren", Text(w));
        w.Find<Button>("du-run").Press();
        Assert.Contains("Speicherplatz", Text(w));
        w.Close();
    }
}
