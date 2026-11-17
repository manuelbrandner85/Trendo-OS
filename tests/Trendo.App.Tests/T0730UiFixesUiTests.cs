using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0730 (Gruppe D, Oberfläche): Umbenennen im Tagesplaner-Block nimmt die Pfeiltasten nicht mehr dem Block weg,
/// Gruppieren mit nur einer Form wirft keine Ausnahme mehr. Echte Tasten- und Mausereignisse im Headless-Fenster.</summary>
[Collection("Claude")]
public class T0730UiFixes_Ui
{
    const string DK = "tagesplaner-programm";

    static T V<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Max(MainWindow w, string key) { if (w.Windows.Active is { Maximized: false } win) { w.Windows.ToggleMaximize(win.Id); w.Show(key); } Settle(); w.UpdateLayout(); Settle(); }
    static void Click(MainWindow w, Point p) { w.MouseMove(p); w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle(); }
    static void Key_(TopLevel w, Key k, PhysicalKey pk, RawInputModifiers mod = RawInputModifiers.None) { w.KeyPress(k, mod, pk, null); w.KeyRelease(k, mod, pk, null); Settle(); }
    static JsonObject Plan(ProgramDocument d, int i) => (JsonObject)d.List("plan")[i]!;

    [AvaloniaFact(DisplayName = "T-0730 Tagesplaner: beim Umbenennen im Block verschieben Pfeil hoch/runter den Block nicht, Entf/F2 wirken nur im Textfeld, Enter übernimmt den Namen")]
    public void Umbenennen_mit_Pfeiltasten_haelt_Block()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        MainWindow.DayNowOverride = () => new DateTime(2026, 11, 16, 7, 0, 0);
        try
        {
            w.Show(DK); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); Max(w, DK);
            var d = w.ProgramActive(DK)!;
            RealClick(w, V<Button>(w, "prog-first-step")); Settle();
            var box = V<TextBox>(w, "dp-blk-edit"); Assert.True(box.IsFocused, "Name im Block hat keinen Fokus");
            box.Text = "Angebot"; Key_(w, Key.Enter, PhysicalKey.Enter);
            Assert.Equal("2026-11-16T08:00:00", (string?)Plan(d, 0)["s"]);

            // Block wählen, F2 → Umbenennen im Block
            var tl = V<DayTimeline>(w, "dp-timeline"); var r = tl.BlockRect("p:0");
            Click(w, tl.TranslatePoint(new Point(r.Center.X - 40, r.Y + 8), w)!.Value); Assert.Equal("p:0", w.DaySel);
            Key_(w, Key.F2, PhysicalKey.F2);
            Assert.Equal("p:0", w.DayEditing);
            box = V<TextBox>(w, "dp-blk-edit"); Assert.True(box.IsFocused, "Name im Block hat nach F2 keinen Fokus");
            var hist = d.History.Count;

            // Tasten im Textfeld: Pfeile, Entf, F2 – der Block bleibt, wo er ist, und bleibt im Plan
            Key_(w, Key.Up, PhysicalKey.ArrowUp); Key_(w, Key.Up, PhysicalKey.ArrowUp);
            Key_(w, Key.Down, PhysicalKey.ArrowDown);
            Key_(w, Key.Delete, PhysicalKey.Delete);
            Key_(w, Key.F2, PhysicalKey.F2);
            Assert.Equal("2026-11-16T08:00:00", (string?)Plan(d, 0)["s"]); Assert.Equal("2026-11-16T08:30:00", (string?)Plan(d, 0)["e"]);
            Assert.Single(d.List("plan"));
            Assert.Equal(hist, d.History.Count);
            Assert.Equal("p:0", w.DayEditing);

            V<TextBox>(w, "dp-blk-edit").Text = "Angebot fertig"; Key_(w, Key.Enter, PhysicalKey.Enter);
            Assert.Null(w.DayEditing);
            Assert.Equal("Angebot fertig", (string?)((JsonObject)d.List("tasks")[0]!)["title"]);
            Assert.Equal("2026-11-16T08:00:00", (string?)Plan(d, 0)["s"]);

            // Gegenprobe: ohne Textfeld verschiebt Pfeil runter den gewählten Block weiterhin um 5 Minuten
            tl = V<DayTimeline>(w, "dp-timeline"); r = tl.BlockRect("p:0");
            Click(w, tl.TranslatePoint(new Point(r.Center.X - 40, r.Y + 8), w)!.Value); Assert.Equal("p:0", w.DaySel);
            if (w.DayEditing is not null) Key_(w, Key.Escape, PhysicalKey.Escape); // zweiter Klick auf den gewählten Block startet wieder das Umbenennen (wie im Explorer) – mit Esc beenden
            var fokus = w.FocusManager?.GetFocusedElement(); Assert.True(fokus is DayTimeline, "Fokus: " + fokus?.GetType().Name + " " + (fokus as Avalonia.Controls.Control)?.Name);
            Key_(w, Key.Down, PhysicalKey.ArrowDown);
            Assert.Equal("2026-11-16T08:05:00", (string?)Plan(d, 0)["s"]);
        }
        finally { MainWindow.DayNowOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0730 Vektor: Strg+G und „Gruppieren“ mit nur einer Form werfen nicht, ändern nichts und geben einen Hinweis; zwei Formen werden gruppiert")]
    public void Gruppieren_mit_einer_Form_wirft_nicht()
    {
        var doc = new VDoc { Shapes = [new VShape { Id = "r1", X = 20, Y = 20 }, new VShape { Id = "r2", Kind = "ellipse", X = 200, Y = 40 }] };
        var st = new VectorEditorState(); st.Selected.Add("r1");
        var commits = new List<string>();
        var ed = new VectorEditor(doc, st, (_, label) => commits.Add(label), "t0730-vec");
        var win = new Window { Width = 900, Height = 600, Content = ed };
        win.Show();
        try
        {
            Settle();
            var surface = win.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "t0730-vec-surface");
            surface.Focus(); Settle();
            Key_(win, Key.G, PhysicalKey.G, RawInputModifiers.Control); // echtes Kürzel Strg+G
            Assert.Empty(commits);
            Assert.Contains("mindestens zwei Formen", ed.LastHint);
            Assert.All(ed.Doc.Shapes, s => Assert.Equal("", s.Group));

            var ex = Record.Exception(() => ed.Act("group")); // Weg über Menü/Knopf
            Assert.Null(ex); Assert.Empty(commits);

            st.Selected.Add("r2");
            ed.Act("group");
            Assert.Equal(["Gruppiert"], commits);
        }
        finally { win.Close(); }
    }
}
