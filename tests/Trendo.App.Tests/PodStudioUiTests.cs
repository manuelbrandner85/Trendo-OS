using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Print on Demand Teil 1 (08.11.2026): P1, P3, P4, P6, P7, P8, P9.</summary>
public class PodStudio_Ui
{
    const string K = "pod-programm";

    static string FakePng(string root, int w, int h, byte colorType)
    {
        byte[] d = [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', (byte)(w >> 24), (byte)(w >> 16), (byte)(w >> 8), (byte)w, (byte)(h >> 24), (byte)(h >> 16), (byte)(h >> 8), (byte)h, 8, colorType, 0, 0, 0, 0, 0, 0, 0];
        var f = Path.Combine(root, $"motiv{w}.png"); File.WriteAllBytes(f, d); return f;
    }

    [AvaloniaFact(DisplayName = "POD: Motiv prüfen, Preis für Wunschgewinn, Markenwarnung, Checkliste, Verkaufstext als Datei")]
    public void Flow()
    {
        var (w, _, root) = Ui.Open();
        Assert.Equal(K, MainWindow.StudioPages["STUDIO-POD-01"]);
        w.Show(K); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-tshirt")); Settle();
        var d = w.ProgramActive(K)!;
        Assert.NotNull(w.TryFind<Canvas>("pod-preview"));

        d.Set("motif", FakePng(root, 800, 1000, 2)); w.Show(K); Settle();
        var check = string.Join(" ", w.Find<StackPanel>("pod-check").Children.OfType<TextBlock>().Select(t => t.Text));
        Assert.Contains("So nicht drucken", check); Assert.Contains("unscharf", check); Assert.Contains("Rechteck", check);
        d.Set("motif", FakePng(root, 3543, 4724, 6)); w.Show(K); Settle();
        Assert.Contains("Druckdatei passt", string.Join(" ", w.Find<StackPanel>("pod-check").Children.OfType<TextBlock>().Select(t => t.Text)));

        RealClick(w, w.Find<Button>("pod-step-preis")); Settle();
        Assert.Equal("7,05 €", w.Find<TextBlock>("pod-profit").Text);
        w.Find<TextBox>("pod-want").Text = "10";
        w.Find<Button>("pod-apply").BringIntoView(); Settle();
        RealClick(w, w.Find<Button>("pod-apply")); Settle();
        var profit = decimal.Parse(w.Find<TextBlock>("pod-profit").Text!.Replace(" €", ""), System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
        Assert.InRange(profit, 10m, 10.02m);

        RealClick(w, w.Find<Button>("pod-step-recht")); Settle();
        Assert.StartsWith("Keine der häufigsten", w.Find<TextBlock>("pod-marks").Text);
        d.Set("motifText", "Team Super Mario"); w.Show(K); Settle();
        Assert.Contains("Super Mario", w.Find<TextBlock>("pod-marks").Text);
        w.Find<CheckBox>("pod-check-gpsr").IsChecked = true; Settle();
        Assert.Contains("gpsr", d.List("checks").Select(x => x!.ToString()));

        RealClick(w, w.Find<Button>("pod-step-text")); Settle();
        d.Set("description", "Weiches Shirt mit Waldmotiv."); d.Set("tags", "Natur, Wald");
        var md = Path.Combine(root, "text.md");
        MainWindow.Programs[K].Exports.Single(x => x.Name == "listing").Write(d, md);
        var txt = File.ReadAllText(md);
        Assert.Contains("Weiches Shirt", txt); Assert.Contains("Schlagwörter: Natur, Wald", txt); Assert.Contains("T-Shirt", txt);
        w.Close();
    }
}
