using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>N5/N9/N11/N18/N20: Notizbuch, Editor, Rechner, Zettel, Uhr – Oberfläche.</summary>
public class N5N20_HelferUi
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    static void CtrlS(Control c) { c.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = KeyModifiers.Control }); Pump(); }

    [AvaloniaFact(DisplayName = "N9: Editor öffnet ANSI/CRLF, sucht, ersetzt, speichert in gleicher Kodierung; Kodierung umstellbar")]
    public void Editor()
    {
        var (w, _, _) = Ui.Open();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var f = Path.Combine(w.Core.DataRoot, "liste.txt");
        File.WriteAllBytes(f, Encoding.GetEncoding(1252).GetBytes("Äpfel\r\nBirnen\r\nÄpfel"));
        w.EditorOpen(f); Pump();
        Assert.Equal("ed-tab-0", w.TryFind<Button>("ed-tab-")!.Name);
        var tb = w.Find<TextBox>("ed-text");
        Assert.Equal("Äpfel\r\nBirnen\r\nÄpfel", tb.Text);
        Assert.Contains("ANSI", w.Find<TextBlock>("ed-status").Text);
        Assert.Equal("1\n2\n3", w.Find<TextBlock>("ed-lines").Text);
        w.Find<TextBox>("ed-find").Text = "birnen";
        w.Find<Button>("ed-find-next").Press(); Pump();
        Assert.Equal("Birnen", w.Find<TextBox>("ed-text").SelectedText);
        w.Find<TextBox>("ed-find").Text = "Äpfel"; w.Find<TextBox>("ed-replace").Text = "Kirschen";
        w.Find<Button>("ed-replace-all").Press(); Pump();
        Assert.Equal("Kirschen\r\nBirnen\r\nKirschen", w.Find<TextBox>("ed-text").Text);
        Assert.Contains("•", (string)w.Find<Button>("ed-tab-0").Content!);
        w.Find<Button>("ed-save").Press(); Pump();
        Assert.Equal(Encoding.GetEncoding(1252).GetBytes("Kirschen\r\nBirnen\r\nKirschen"), File.ReadAllBytes(f));
        // auf UTF-8 + LF umstellen
        w.Find<ComboBox>("ed-enc").SelectedItem = "UTF-8"; Pump();
        w.Find<ComboBox>("ed-eol").SelectedItem = "LF"; Pump();
        w.Find<Button>("ed-save").Press(); Pump();
        Assert.Equal(Encoding.UTF8.GetBytes("Kirschen\nBirnen\nKirschen"), File.ReadAllBytes(f));
        // Neuer Reiter; Schließen mit ungespeicherten Änderungen fragt erst nach
        w.Find<Button>("ed-new").Press(); Pump();
        Assert.Equal(2, w.EdDocs.Count);
        w.Find<TextBox>("ed-text").Text = "Entwurf"; Pump();
        w.Find<Button>("ed-close-1").Press(); Pump();
        Assert.Equal(2, w.EdDocs.Count);
        w.Find<Button>("ed-close-1").Press(); Pump();
        Assert.Single(w.EdDocs);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "N11: Rechner – Tasten, wissenschaftlich, Programmierer, Grafik, Datum, Verlauf")]
    public void Rechner()
    {
        var (w, _, _) = Ui.Open();
        w.Show("rechner"); Pump();
        foreach (var k in new[] { "7", "mal", "6" }) w.Find<Button>("rc-key-" + k).Press();
        w.Find<Button>("rc-key-gleich").Press(); Pump();
        Assert.Equal("= 42", w.Find<TextBlock>("rc-result").Text);
        Assert.Equal("7×6 = 42", w.CalcHistory[0]);
        w.Find<Button>("rc-mode-wissenschaftlich").Press(); Pump();
        Assert.NotNull(w.TryFind<Button>("rc-key-sin"));
        w.Find<TextBox>("rc-input").Text = "";
        w.Find<Button>("rc-key-sin").Press(); w.Find<Button>("rc-key-3").Press(); w.Find<Button>("rc-key-0").Press(); w.Find<Button>("rc-key-zu").Press();
        w.Find<Button>("rc-key-gleich").Press(); Pump();
        Assert.Equal("= 0,5", w.Find<TextBlock>("rc-result").Text);
        w.Find<Button>("rc-mode-programmierer").Press(); Pump();
        w.Find<TextBox>("rc-prog-a").Text = "255"; Pump();
        Assert.Equal("FF", w.Find<TextBlock>("rc-hex").Text);
        Assert.Equal("1111 1111", w.Find<TextBlock>("rc-bin").Text);
        w.Find<TextBox>("rc-prog-b").Text = "15"; w.Find<Button>("rc-op-and").Press(); Pump();
        Assert.Equal("15", w.Find<TextBox>("rc-prog-a").Text);
        w.Find<Button>("rc-mode-grafik").Press(); Pump();
        Assert.Equal(2, w.Find<GraphView>("rc-graph").Series.Count);
        w.Find<TextBox>("rc-graph-expr").Text = "x^2"; w.Find<Button>("rc-graph-draw").Press(); Pump();
        Assert.Single(w.Find<GraphView>("rc-graph").Series);
        w.Find<Button>("rc-mode-datum").Press(); Pump();
        w.Find<TextBox>("rc-date-a").Text = "26.10.2026"; w.Find<TextBox>("rc-date-b").Text = "02.11.2026";
        w.Find<Button>("rc-date-diff").Press(); Pump();
        Assert.Contains("7 Tage", w.Find<TextBlock>("rc-date-out").Text);
        Assert.Contains("5 Werktage", w.Find<TextBlock>("rc-date-out").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "N20: Wecker stellen und klingeln (Benachrichtigung mit Schlummern), Timer läuft ab, Stoppuhr-Runden, Weltuhr")]
    public void Uhr()
    {
        var (w, _, _) = Ui.Open();
        w.Show("uhr"); Pump();
        w.Find<TextBox>("uhr-al-time").Text = "06:30"; w.Find<TextBox>("uhr-al-label").Text = "Aufstehen";
        w.Find<ComboBox>("uhr-al-days").SelectedIndex = 1;
        w.Find<Button>("uhr-al-add").Press(); Pump();
        Assert.Equal("Aufstehen", Assert.Single(w.Wecker.All()).Label);
        Assert.NotNull(w.TryFind<ToggleSwitch>("uhr-al-on-0"));
        var due = w.CheckAlarms(DateTime.Today.AddHours(6).AddMinutes(30).AddSeconds(3)); Pump();
        Assert.Single(due);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Aufstehen"));
        Assert.NotNull(w.TryFind<Button>("toast-action"));
        Assert.Empty(w.CheckAlarms(DateTime.Today.AddHours(6).AddMinutes(31)));
        w.Find<Button>("uhr-tm-5").Press(); Pump();
        Assert.True(w.TimerRunning);
        Assert.InRange(w.TimerLeft(DateTime.Now).TotalSeconds, 290, 300);
        w.Find<Button>("uhr-tm-stop").Press(); Pump();
        Assert.False(w.TimerRunning);
        w.Find<Button>("uhr-sw-start").Press(); w.Find<Button>("uhr-sw-lap").Press(); Pump();
        Assert.NotEmpty(w.Find<StackPanel>("uhr-laps").Children);
        w.Find<Button>("uhr-sw-reset").Press(); Pump();
        w.Find<ComboBox>("uhr-wc-pick").SelectedItem = "Sydney"; w.Find<Button>("uhr-wc-add").Press(); Pump();
        Assert.Contains("Sydney", w.Weltuhr.Cities());
        w.Close();
    }

    [AvaloniaFact(DisplayName = "N18: Zettel anlegen, Farbe wählen, auf der Startseite anheften und lösen")]
    public void Zettel()
    {
        var (w, _, _) = Ui.Open();
        w.Show("zettel"); Pump();
        w.Find<Button>("zt-add").Press(); Pump();
        w.Find<TextBox>("zt-text-0").Text = "Arzt anrufen"; Pump();
        w.Find<Button>("zt-color-0-Blau").Press(); Pump();
        Assert.Equal(("Arzt anrufen", "Blau"), (w.Zettel.All()[0].Text, w.Zettel.All()[0].Color));
        w.Show("start"); Pump();
        var desk = w.Find<WrapPanel>("desk-zettel");
        Assert.True(desk.IsVisible); Assert.Single(desk.Children);
        w.Show("zettel"); Pump();
        w.Find<CheckBox>("zt-pin-0").IsChecked = false; Pump();
        w.Show("start"); Pump();
        Assert.False(w.Find<WrapPanel>("desk-zettel").IsVisible);
        w.Show("zettel"); Pump();
        w.Find<Button>("zt-del-0").Press(); Pump();
        Assert.Empty(w.Zettel.All());
        w.Close();
    }

    [AvaloniaFact(DisplayName = "N5: Notizbuch – Abschnitt und Seite anlegen, schreiben (speichert), Schlagworte, Suche, der KI als Wissen geben")]
    public void Notizbuch()
    {
        var (w, _, _) = Ui.Open();
        w.Show("notizbuch"); Pump();
        Assert.Equal("Allgemein", (string)w.Find<Button>("nb-sec-0").Content!);
        w.Find<TextBox>("nb-new-sec").Text = "Haushalt"; w.Find<Button>("nb-add-sec").Press(); Pump();
        w.Find<TextBox>("nb-new-page").Text = "Einkauf"; w.Find<Button>("nb-add-page").Press(); Pump();
        var text = w.Find<TextBox>("nb-text");
        text.Text = "Milch, Brot #einkauf"; CtrlS(text);
        Assert.Equal("Milch, Brot #einkauf", w.Notizbuch.Read("Haushalt", "Einkauf"));
        Assert.NotEmpty(w.Find<WrapPanel>("nb-tags").Children);
        w.Find<TextBox>("nb-search").Text = "milch"; w.Find<Button>("nb-search-go").Press(); Pump();
        Assert.Contains("Einkauf", (string)w.Find<Button>("nb-hit-0").Content!);
        w.Find<Button>("nb-to-ki").Press(); Pump();
        Assert.Contains(w.Wissen.All(), k => k.Title == "Einkauf" && k.Text.Contains("Milch"));
        w.Find<Button>("nb-del").Press(); Pump();
        Assert.Empty(w.Notizbuch.Pages("Haushalt"));
        w.Close();
    }
}
