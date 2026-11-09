using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Trendo.App;
using Trendo.Core.Pod;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Print on Demand, Design-Editor (docs/nachbau/kittl/anforderungen.md).</summary>
public class PodDesign_Ui
{
    const string K = "pod-programm";

    static (MainWindow W, Trendo.Core.Programs.ProgramDocument D, string Root) Open()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-tshirt")); Settle();
        return (w, w.ProgramActive(K)!, root);
    }
    static Point OnBoard(MainWindow w, PodItem i, double x = 0.5, double y = 0.5) => w.Find<Canvas>("pod-board").TranslatePoint(new Point(i.X + i.W * x, i.Y + i.H * y), w)!.Value;
    static List<PodItem> Items(Trendo.Core.Programs.ProgramDocument d) => PodDesign.Items(d.Data);
    static void Drag(MainWindow w, Point a, Point b, RawInputModifiers m = RawInputModifiers.None) { w.MouseDown(a, MouseButton.Left, m); for (var i = 1; i <= 5; i++) { w.MouseMove(a + (b - a) * (i / 5.0), m | RawInputModifiers.LeftMouseButton); Settle(); } w.MouseUp(b, MouseButton.Left, m); Settle(); }

    [AvaloniaFact(DisplayName = "K-01 bis K-03, K-09, K-10: Elemente hinzufügen, mit der Maus verschieben und skalieren, Text ändern, Ebene ausblenden, Rückgängig")]
    public void Edit_with_mouse()
    {
        var (w, d, _) = Open();
        RealClick(w, w.Find<Button>("pod-add-rect")); Settle();
        var r = Items(d).Single(); Assert.Contains(r.Id, w.PodSelected);

        Drag(w, OnBoard(w, r), OnBoard(w, r) + new Vector(120, 60), RawInputModifiers.Control); // Strg: ohne Einrasten
        var m = Items(d).Single(); Assert.True(m.X > r.X + 100, $"X {m.X} Treffer {(w.InputHitTest(OnBoard(w, r)) as Control)?.GetType().Name}/{(w.InputHitTest(OnBoard(w, r)) as Control)?.Name} Punkt {OnBoard(w, r)} Ansicht {w.Find<Border>("pod-view").Bounds} Zoom {w.PodZoom}"); Assert.True(m.Y > r.Y + 50);
        Assert.True(d.CanUndo);

        Drag(w, OnBoard(w, m, 1, 1), OnBoard(w, m, 1, 1) + new Vector(55, 55));
        var s = Items(d).Single(); Assert.True(s.W > m.W + 50, $"W {s.W}"); Assert.Equal(m.X, s.X, 3);

        RealClick(w, w.Find<Button>("pod-add-text")); Settle();
        var t = Items(d)[1]; Assert.Equal("text", t.Kind);
        var box = w.Find<TextBox>("pod-prop-text"); box.Focus(); box.Text = "Waldliebe"; w.Find<Button>("pod-add-text").Focus(); Settle();
        Assert.Equal("Waldliebe", Items(d)[1].Text);

        w.Find<Button>("pod-hide-" + t.Id).BringIntoView(); Settle();
        RealClick(w, w.Find<Button>("pod-hide-" + t.Id)); Settle();
        Assert.True(Items(d)[1].Hidden);
        Assert.Single(w.Find<Canvas>("pod-board").Children);
        Assert.True(d.Undo());
        Assert.False(Items(d)[1].Hidden);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "K-05 bis K-08: Tasten ordnen, gruppieren, kopieren, zoomen, Raster und Lineale")]
    public void Keys()
    {
        var (w, d, _) = Open();
        w.PodAdd(d, "rect"); Settle(); var a = Items(d)[0].Id;
        w.PodAdd(d, "ellipse"); Settle(); var b = Items(d)[1].Id;
        w.PodSelected.Clear(); w.PodSelected.Add(a);
        Assert.True(w.PodKey_(d, Key.OemCloseBrackets, KeyModifiers.None)); Assert.Equal(a, Items(d)[1].Id);
        w.PodSelected.Clear(); w.PodSelected.Add(a); w.PodSelected.Add(b);
        w.PodKey_(d, Key.G, KeyModifiers.Control);
        Assert.Equal(Items(d)[0].Group, Items(d)[1].Group); Assert.NotEqual("", Items(d)[0].Group);
        w.PodKey_(d, Key.C, KeyModifiers.Control); w.PodKey_(d, Key.V, KeyModifiers.Control);
        Assert.Equal(4, Items(d).Count);
        var z = w.PodZoom; w.PodKey_(d, Key.OemPlus, KeyModifiers.None); Assert.True(w.PodZoom > z);
        w.PodKey_(d, Key.D2, KeyModifiers.Shift); Assert.True(w.PodZoom > 0);
        w.PodKey_(d, Key.OemQuotes, KeyModifiers.Control); Assert.True(w.PodGrid);
        w.PodKey_(d, Key.R, KeyModifiers.Shift); Assert.True(w.PodRulers);
        w.PodKey_(d, Key.Delete, KeyModifiers.None); Assert.Equal(2, Items(d).Count);
        Assert.False(w.PodKey_(d, Key.F7, KeyModifiers.None));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "K-11: Farbe in die Palette, von dort anwenden; ungültige Farbe ändert nichts")]
    public void Palette()
    {
        var (w, d, _) = Open();
        w.PodAdd(d, "rect"); Settle();
        var fill = w.Find<TextBox>("pod-prop-fill"); fill.Focus(); fill.Text = "#123456"; w.Find<Button>("pod-color-keep").Focus(); Settle();
        Assert.Equal("#123456", Items(d)[0].Fill);
        w.Find<Button>("pod-color-keep").BringIntoView(); Settle();
        RealClick(w, w.Find<Button>("pod-color-keep")); Settle();
        w.PodAdd(d, "ellipse"); Settle();
        var f2 = w.Find<TextBox>("pod-prop-fill"); f2.Focus(); f2.Text = "rot"; w.Find<Button>("pod-color-keep").Focus(); Settle();
        Assert.NotEqual("rot", Items(d)[1].Fill);
        w.Find<Button>("pod-color-0").BringIntoView(); Settle();
        RealClick(w, w.Find<Button>("pod-color-0")); Settle();
        Assert.Equal("#123456", Items(d)[1].Fill);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "K-12, K-13: Entwurf wird PNG in Druckgröße mit Transparenz, wird Motiv und besteht die Druckprüfung; leerer Entwurf wird abgelehnt")]
    public void Use_as_motif()
    {
        var (w, d, root) = Open();
        Assert.Throws<InvalidOperationException>(() => w.PodExportPng(d, Path.Combine(root, "leer.png")));
        w.PodAdd(d, "rect"); Settle();
        RealClick(w, w.Find<Button>("pod-use")); Settle();
        Assert.True(File.Exists(d.Text("motif")));
        Assert.NotNull(w.TryFind<StackPanel>("pod-check")); // weiter im Schritt „Druck prüfen“
        w.Close();
    }

    [AvaloniaFact(DisplayName = "K-14: Kittl ist von Haus aus aus, Knopf schaltet um und nennt die Anmeldung")]
    public async Task Kittl_login_flow()
    {
        var (w, _, _) = Open();
        var calls = new List<string>();
        var state = "  Status: ! Needs authentication";
        w.KittlCliOverride = a => { calls.Add(string.Join(' ', a)); return new(0, a[1] == "get" ? state : "", ""); };
        var console = new List<string>();
        w.KittlConsoleOverride = a => { console.Add(string.Join(' ', a)); state = "  Status: √ Connected"; return true; };
        w.PodKittlSet(true);
        Assert.Equal(KittlConnect.State.AnmeldungNoetig, await w.PodKittlCheck()); Settle();
        Assert.Equal("Anmeldung nötig", w.Find<TextBlock>("pod-kittl-state").Text);
        Assert.Null(w.TryFind<Button>("pod-kittl-logout"));

        Assert.True(await w.PodKittlLogin()); Settle();
        Assert.Contains("mcp login trendo-kittl", console);
        Assert.Contains(calls, c => c.StartsWith("mcp add --transport http trendo-kittl", StringComparison.Ordinal));
        Assert.StartsWith("✓", w.Find<TextBlock>("pod-kittl-state").Text);
        Assert.Null(w.TryFind<Button>("pod-kittl-login"));

        state = "  Status: ! Needs authentication";
        await w.PodKittlLogout(); Settle();
        Assert.Contains("mcp logout trendo-kittl", calls);
        Assert.NotNull(w.TryFind<Button>("pod-kittl-login"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "POD-Design: Bricht die Anmeldung ab, bleibt der Knopf und ein Hinweis erklärt es")]
    public async Task Kittl_login_aborted()
    {
        var (w, _, _) = Open();
        w.KittlCliOverride = a => new(0, a[1] == "get" ? "  Status: ! Needs authentication" : "", "");
        w.KittlConsoleOverride = _ => false;
        w.PodKittlSet(true);
        Assert.False(await w.PodKittlLogin()); Settle();
        Assert.NotNull(w.TryFind<Button>("pod-kittl-login"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "POD-Design: Kittl-Schalter schaltet die Anbindung ein und aus und bietet die Anmeldung an")]
    public void Kittl()
    {
        var (w, _, _) = Open();
        Assert.False(w.PodKittlOn);
        w.Find<Button>("pod-kittl-toggle").BringIntoView(); Settle();
        RealClick(w, w.Find<Button>("pod-kittl-toggle")); Settle();
        Assert.True(w.PodKittlOn);
        Assert.NotNull(w.TryFind<Button>("pod-kittl-login"));
        w.Find<Button>("pod-kittl-toggle").BringIntoView(); Settle();
        RealClick(w, w.Find<Button>("pod-kittl-toggle")); Settle();
        Assert.False(w.PodKittlOn);
        w.Close();
    }
}
