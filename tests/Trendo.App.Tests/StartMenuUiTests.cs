using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

/// <summary>Startmenü über den echten Eingabeweg: Mausklick an der Bildschirmstelle bzw. echte Tastendrücke.</summary>
public class StartMenu_Ui
{
    internal static void RealClick(Window w, Control c)
    {
        Settle(); w.UpdateLayout(); Settle();
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w) ?? throw new InvalidOperationException("nicht sichtbar: " + c.Name);
        var hit = w.InputHitTest(p) as Visual;
        Assert.True(hit is not null && (hit == c || hit.GetVisualAncestors().Contains(c)), $"{c.Name}: an der Stelle liegt {hit?.GetType().Name} {(hit as Control)?.Name} statt des Knopfs; Kette: " + string.Join(" < ", (hit?.GetVisualAncestors() ?? []).OfType<Control>().Take(12).Select(a => a.GetType().Name + ":" + a.Name)) + $" p={p} k={w.ClientSize}");
        w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle();
    }
    internal static void Settle() { for (var i = 0; i < 5; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); } }
    internal static T V<T>(Window w, string name) where T : Control => w.GetVisualDescendants().OfType<T>().First(c => c.Name == name && c.IsEffectivelyVisible);

    [AvaloniaFact(DisplayName = "Startmenü: Klick auf Startknopf öffnet, Klick daneben und Esc schließen, Windows-Taste schaltet um")]
    public void OpenClose()
    {
        var (w, _, _) = Ui.Open(); w.Show("start"); Settle();
        var sb = V<Button>(w, "start-button");
        var pos = sb.TranslatePoint(default, w)!.Value; Assert.True(pos.X < w.Bounds.Width / 2, "Startknopf soll links sitzen");
        RealClick(w, sb); Assert.True(w.StartMenuOpen);
        w.Key(PhysicalKey.Escape); Assert.False(w.StartMenuOpen);
        w.Key(PhysicalKey.MetaLeft); Assert.True(w.StartMenuOpen);
        w.Key(PhysicalKey.MetaLeft); Assert.False(w.StartMenuOpen);
        RealClick(w, sb); Assert.True(w.StartMenuOpen);
        var free = new Point(w.Bounds.Width - 40, 120); w.MouseDown(free, MouseButton.Left); w.MouseUp(free, MouseButton.Left); Settle();
        Assert.False(w.StartMenuOpen);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Startmenü: Suche findet Studio/Seite, Ergebnis per Mausklick öffnet")]
    public void Search()
    {
        var (w, _, _) = Ui.Open(); w.Show("start"); Settle();
        RealClick(w, V<Button>(w, "start-button"));
        V<TextBox>(w, "startmenu-search").Text = "Einstellungen"; Settle();
        RealClick(w, V<Button>(w, "sm-hit-0"));
        Assert.False(w.StartMenuOpen); Assert.Equal("settings", w.CurrentView);
        RealClick(w, V<Button>(w, "start-button")); V<TextBox>(w, "startmenu-search").Text = "xyzgibtsnicht"; Settle();
        Assert.Null(w.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "sm-hit-0"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Startmenü: Studio aus A–Z anheften, angeheftet öffnen, lösen; Zuletzt benutzt")]
    public void PinRecent()
    {
        var (w, _, root) = Ui.Open(); w.Show("start"); Settle();
        RealClick(w, V<Button>(w, "start-button"));
        var letters = w.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name?.StartsWith("sm-letter-") == true).Select(t => t.Text).ToList();
        Assert.Equal(letters.OrderBy(x => x, StringComparer.Create(new System.Globalization.CultureInfo("de-DE"), true)), letters);
        var office = w.Studios.First(s => s.Id == "SYS-10");
        var toggle = V<Button>(w, "sm-pintoggle-SYS-10"); toggle.BringIntoView(); Settle(); RealClick(w, toggle);
        Assert.Contains("SYS-10", Trendo.Core.UserSettings.Load(root).PinnedStudios);
        var pin = V<Button>(w, "sm-pin-SYS-10"); pin.BringIntoView(); Settle(); RealClick(w, pin);
        Assert.Contains(w.CurrentView, new[] { "office", "studio-deps" }); Assert.False(w.StartMenuOpen);
        w.Show("settings");
        RealClick(w, V<Button>(w, "start-button"));
        Assert.NotNull(w.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "sm-recent-settings"));
        Assert.Equal("settings", Trendo.Core.UserSettings.Load(root).RecentPages[0]);
        RealClick(w, V<Button>(w, "sm-unpin-SYS-10"));
        Assert.DoesNotContain("SYS-10", Trendo.Core.UserSettings.Load(root).PinnedStudios);
        w.Close();
    }
}
