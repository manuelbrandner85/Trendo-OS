using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0267: Programm „Sendeablauf“ über den echten Eingabeweg – Ablauf eintippen, in der Tabelle ändern, sortieren, On-Air-Uhr, Weitergabe, Rechner.</summary>
public class SPEC15_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? Try<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static void Key1(MainWindow w, Key k, PhysicalKey ph) { w.KeyPress(k, RawInputModifiers.None, ph, null); w.KeyRelease(k, RawInputModifiers.None, ph, null); Settle(); }
    static void Retype(MainWindow w, string name, string text, bool enter = true) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); RealClick(w, tb); tb.Focus(); tb.SelectAll(); Settle(); Key1(w, Key.Back, PhysicalKey.Backspace); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } if (enter) Key1(w, Key.Enter, PhysicalKey.Enter); }
    static void CloseToasts(MainWindow w) { while (Try<Button>(w, "toast-close") is { } c) RealClick(w, c); }

    [AvaloniaFact(DisplayName = "SPEC-15 UI: Programmpunkte eintippen (Enter), Dauer und Titel in der Tabelle ändern, verschieben, entfernen; Sendeplatz zu kurz wird rot")]
    public void Type_rundown()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-15"]); Settle();
        Assert.Null(Try<Button>(w, "bc-add-run")); // kein Formular mehr
        Retype(w, "bc-show", "Abendmagazin"); Retype(w, "bc-slot", "10");
        foreach (var (t, d) in new[] { ("Begrüßung", "1:30"), ("Gast", "6:00"), ("Musik", "4:00") })
        {
            Retype(w, "bc-new-title", t, false); Retype(w, "bc-new-dur", d);
        }
        Assert.Equal(["Begrüßung", "Gast", "Musik"], w.Broadcast.Plan.Segments.Select(s => s.Title));
        Assert.Equal(690, w.Broadcast.Total.TotalSeconds);
        Assert.Contains("1:30 zu lang", Get<TextBlock>(w, "bc-total").Text);
        Retype(w, "bc-dur-1", "4:30"); Assert.Equal(270, w.Broadcast.Plan.Segments[1].Seconds);
        Assert.Contains("frei", Get<TextBlock>(w, "bc-total").Text);
        Retype(w, "bc-note-1", "Kamera 2"); Retype(w, "bc-title-1", "Gast: Erika Muster");
        Assert.Equal("Gast: Erika Muster", w.Broadcast.Plan.Segments[1].Title); Assert.Equal("Kamera 2", w.Broadcast.Plan.Segments[1].Note);
        Press(w, "bc-down-0"); Assert.Equal("Begrüßung", w.Broadcast.Plan.Segments[1].Title);
        Assert.Equal(w.Broadcast.Plan.Start.AddSeconds(270).ToString("HH:mm:ss"), Get<TextBlock>(w, "bc-row-1").Text); // Beginn rechnet mit
        Press(w, "bc-del-2"); Assert.Equal(2, w.Broadcast.Plan.Segments.Count);
        Retype(w, "bc-dur-0", "abc"); Assert.StartsWith("⚠", Get<TextBlock>(w, "bc-msg").Text); Assert.Equal(270, w.Broadcast.Plan.Segments[0].Seconds);
        Retype(w, "bc-start", "morgen"); Assert.Contains("Beginn bitte", Get<TextBlock>(w, "bc-msg").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-15 UI: On-Air-Uhr zeigt laufenden Punkt mit Countdown und Nächstes; Weitergabe CSV/Kapitel/Karten; Rechner sofort")]
    public void OnAir_export_calcs()
    {
        var (w, _, _) = Ui.Open();
        var start = new DateTime(2026, 10, 31, 20, 0, 0);
        w.Broadcast.Plan.Start = start; w.Broadcast.Add("Begrüßung", 60); w.Broadcast.Add("Nachrichten", 300); w.Broadcast.Add("Wetter", 120);
        w.BcClockOverride = start.AddSeconds(100);
        w.Show("sendung"); Settle();
        Press(w, "bc-mode-onair");
        Assert.Equal("2. Nachrichten", Get<TextBlock>(w, "bc-now-title").Text);
        Assert.Equal("4:20", Get<TextBlock>(w, "bc-now-left").Text);
        Assert.Equal("Danach: Wetter", Get<TextBlock>(w, "bc-now-next").Text);
        w.BcClockOverride = start.AddSeconds(475); w.BcTick();
        Assert.Equal("0:05", Get<TextBlock>(w, "bc-now-left").Text); Assert.Contains("Ende der Sendung", Get<TextBlock>(w, "bc-now-next").Text);
        w.BcClockOverride = start.AddSeconds(-90); w.BcTick(); Assert.Equal("Noch nicht auf Sendung", Get<TextBlock>(w, "bc-now-title").Text);
        Press(w, "bc-mode-ablauf");
        Press(w, "bc-export"); var csv = Get<TextBlock>(w, "bc-msg").Text!["Gespeichert: ".Length..]; Assert.Contains("Nachrichten", File.ReadAllText(csv));
        CloseToasts(w);
        Press(w, "bc-chapters"); Assert.Contains("Kapitelmarken", Get<TextBlock>(w, "bc-msg").Text);
        Press(w, "bc-cards"); Assert.True(File.Exists(Get<TextBlock>(w, "bc-msg").Text!["Gespeichert: ".Length..]));
        CloseToasts(w);
        Assert.Contains("Mbit/s", Get<SelectableTextBlock>(w, "bc-rate-out").Text);
        Retype(w, "bc-speak-t", "eins zwei drei vier fünf sechs sieben acht neun zehn", false); Assert.StartsWith("10 Wörter", Get<SelectableTextBlock>(w, "bc-speak-out").Text);
        Assert.Contains("crop=", Get<SelectableTextBlock>(w, "bc-crop-out").Text);
        Assert.Contains("16:9", Get<SelectableTextBlock>(w, "bc-aspect-out").Text);
        Assert.Contains("Timecode", Get<SelectableTextBlock>(w, "bc-tc-out").Text);
        w.Close();
    }
}
