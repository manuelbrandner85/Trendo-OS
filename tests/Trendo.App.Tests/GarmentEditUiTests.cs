using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0745 (SPEC-08): Schnittteile auf der Arbeitsfläche frei bearbeiten – mit Maus, Tastatur und Rechtsklick, bis in die Produktionsdateien und die Anprobe.</summary>
public class GarmentEdit_Ui
{
    const string Key = "mode-programm";

    static (MainWindow W, ProgramDocument D) Open(int level = UserLevel.Profi)
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1200;
        w.ProgramSetLevel(level);
        w.Show(Key); Settle();
        w.Find<Button>("prog-tpl-tshirt").Press(); Settle();
        return (w, w.ProgramActive(Key)!);
    }

    static GarmentPatternEditor Ed(MainWindow w) => w.GarmentEditor ?? throw new InvalidOperationException("Kein Schnitt-Editor");

    /// <summary>Bildschirmpunkt eines Schnittpunkts (cm) im Fenster.</summary>
    static Point At(MainWindow w, int part, double x, double y)
    {
        var ed = Ed(w); ed.BringIntoView(); Settle();
        return Ed(w).TranslatePoint(Ed(w).MapPoint(part, x, y), w)!.Value;
    }

    static void Click(MainWindow w, Point p, MouseButton b = MouseButton.Left) { w.MouseDown(p, b); w.MouseUp(p, b); Settle(); }
    static void Key1(MainWindow w, Avalonia.Input.Key k, PhysicalKey ph, RawInputModifiers m = RawInputModifiers.None) { w.KeyPress(k, m, ph, null); w.KeyRelease(k, m, ph, null); Settle(); }

    static List<PatNode> Nodes(MainWindow w) => Ed(w).Nodes(0);

    [AvaloniaFact(DisplayName = "T-0745: Teil anklicken, Saumecke ziehen, Doppelklick setzt Punkt, K macht eine Kurve, Entf löscht, Strg+Z nimmt je Schritt zurück – Maße und Nahtzugabe folgen")]
    public void Mouse_and_keys()
    {
        var (w, d) = Open();
        try
        {
            var g0 = GarmentBlocks.Build("tshirt", new BodyMeasures(180, 100, 86, 102, false));
            var front0 = g0.Parts[0];
            // 1. Teil anklicken
            Click(w, At(w, 0, 0, front0.Outline.Max(q => q.Y) / 2));
            Assert.Equal(0, Ed(w).State.Part);
            Assert.Contains("Vorderteil", w.Find<TextBlock>("garment-measure-part").Text);
            var n0 = Nodes(w); var hem = n0.FindIndex(q => q.Edge == "hem");
            // 2. Saumecke links 3 cm nach unten ziehen
            var from = At(w, 0, n0[hem].X, n0[hem].Y); var to = At(w, 0, n0[hem].X, n0[hem].Y - 3);
            w.MouseDown(from, MouseButton.Left); for (var i = 1; i <= 5; i++) { w.MouseMove(from + (to - from) * (i / 5.0)); Settle(); } w.MouseUp(to, MouseButton.Left); Settle();
            Assert.Equal("Punkt verschoben", d.UndoLabel);
            var front1 = w.GarmentEditor!.Garment.Parts[0];
            Assert.Equal(front0.Edge("sideL").Length + 3, front1.Edge("sideL").Length, 0); // ≈ 3 cm länger (Mausraster)
            Assert.True(GarmentProduction.WithAllowance(front1).Min(q => q.Y) < GarmentProduction.WithAllowance(front0).Min(q => q.Y) - 2.5); // Saumzugabe folgt
            Assert.Contains("(bearbeitet)", w.Find<TextBlock>("garment-measure-part").Text);
            Assert.Contains("Seitennaht links", w.Find<TextBlock>("garment-measure-seams").Text); // Nahtvergleich zeigt den Unterschied
            Assert.Contains("⚠", w.Find<TextBlock>("garment-measure-seams").Text);

            // 3. Doppelklick auf die Mitte des Saums setzt einen Punkt
            var count = Nodes(w).Count;
            var mid = At(w, 0, 0, front1.Edge("hem").Points[0].Y + (front1.Edge("hem").Points[^1].Y - front1.Edge("hem").Points[0].Y) / 2);
            w.MouseDown(mid, MouseButton.Left); w.MouseUp(mid, MouseButton.Left); w.MouseDown(mid, MouseButton.Left); w.MouseUp(mid, MouseButton.Left); Settle();
            Assert.Equal(count + 1, Nodes(w).Count);
            Assert.Equal("Punkt eingefügt", d.UndoLabel);
            var added = Ed(w).State.Node; Assert.True(added >= 0); Assert.Null(Nodes(w)[added].Edge);

            // 4. Pfeiltaste: 1 cm (Umschalt) nach unten → Saum wird spitz
            Key1(w, Avalonia.Input.Key.Down, PhysicalKey.ArrowDown, RawInputModifiers.Shift);
            Assert.Equal(Nodes(w)[Ed(w).State.Node].Y, front1.Edge("hem").Points[0].Y - 1 + (front1.Edge("hem").Points[^1].Y - front1.Edge("hem").Points[0].Y) / 2, 1);

            // 5. K: Kante nach dem Punkt wird Kurve
            Key1(w, Avalonia.Input.Key.K, PhysicalKey.K);
            Assert.Equal("Kante als Kurve", d.UndoLabel);
            Assert.True(GarmentEdit.IsCurve(Nodes(w), Ed(w).State.Seg));

            // 6. Rechtsklick auf den Punkt öffnet das Menü mit allen Befehlen
            var sel = Nodes(w)[added];
            w.MouseDown(At(w, 0, sel.X, sel.Y), MouseButton.Right); w.MouseUp(At(w, 0, sel.X, sel.Y), MouseButton.Right); Settle();
            var menu = Ed(w).ContextMenu;
            Assert.NotNull(menu);
            var names = menu!.Items.OfType<MenuItem>().Select(i => i.Name).ToList();
            Assert.Contains("garment-edit-menu-delete", names); Assert.Contains("garment-edit-menu-insert", names); Assert.Contains("garment-edit-menu-reset", names);
            Assert.True(menu.Items.OfType<MenuItem>().First(i => i.Name == "garment-edit-menu-delete").IsEnabled);

            // 7. Entf löscht den Punkt
            Assert.Equal(added, Ed(w).State.Node);
            Ed(w).Focus(); Key1(w, Avalonia.Input.Key.Delete, PhysicalKey.Delete);
            Assert.Equal("Punkt gelöscht", d.UndoLabel); Assert.Equal(count, Nodes(w).Count);

            // 8. Strg+Z je Schritt: löschen, Kurve, Pfeil, einfügen, ziehen
            foreach (var label in new[] { "Punkt gelöscht", "Kante als Kurve", "Punkt verschoben", "Punkt eingefügt" })
            {
                Assert.Equal(label, d.UndoLabel);
                Ed(w).Focus(); Key1(w, Avalonia.Input.Key.Z, PhysicalKey.Z, RawInputModifiers.Control);
            }
            Assert.Equal("Punkt verschoben", d.UndoLabel);
            Assert.Equal(count, Nodes(w).Count);
            Ed(w).Focus(); Key1(w, Avalonia.Input.Key.Z, PhysicalKey.Z, RawInputModifiers.Control);
            Assert.Empty(GarmentEdit.Edited(d.Data[GarmentEdit.Field] as JsonObject, "tshirt"));
            Assert.Equal(front0.Area, w.GarmentEditor!.Garment.Parts[0].Area, 6);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0745: Eckpunkt mit Naht lässt sich nicht löschen (Hinweis statt Absturz); Teil zurücksetzen holt die Vorlage; Profi setzt Punkte genau per Zahl")]
    public void Corner_reset_and_exact()
    {
        var (w, d) = Open();
        try
        {
            var front0 = Ed(w).Garment.Parts[0];
            Click(w, At(w, 0, 0, front0.Outline.Max(q => q.Y) / 2));
            var sh = Nodes(w).FindIndex(q => q.Edge == "shR");
            Click(w, At(w, 0, Nodes(w)[sh].X, Nodes(w)[sh].Y));
            Assert.Equal(sh, Ed(w).State.Node);
            w.Find<Button>("garment-edit-delete").Press(); Settle();
            Assert.Contains("Schulter rechts", w.Find<TextBlock>("garment-edit-hint").Text);
            Assert.Null(d.Data[GarmentEdit.Field]);
            // genau setzen: Schulterpunkt 1,5 cm höher
            var y = Nodes(w)[sh].Y + 1.5;
            w.Find<TextBox>("garment-node-y").Text = y.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
            w.Find<Button>("garment-node-set").Press(); Settle();
            Assert.Equal("Punkt gesetzt", d.UndoLabel);
            Assert.Equal(y, Nodes(w)[sh].Y, 2);
            Assert.True(w.Find<Button>("garment-edit-reset").IsEnabled);
            w.Find<Button>("garment-edit-reset").Press(); Settle();
            Assert.Equal("Teil zurückgesetzt", d.UndoLabel);
            Assert.Null(d.Data[GarmentEdit.Field]);
            Assert.Equal(front0.Area, Ed(w).Garment.Parts[0].Area, 6);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0745: Einfach bearbeitet mit Maus und Rechtsklick ohne Leiste, zeigt Maße; Profi zeigt Leiste, Nahtvergleich und Zahlenfelder")]
    public void Levels()
    {
        var (w, _) = Open(UserLevel.Einfach);
        try
        {
            Assert.Null(w.TryFind<Button>("garment-edit-insert")); // Einfach: keine Leiste, alles mit Maus und Rechtsklick
            Click(w, At(w, 0, 0, Ed(w).Garment.Parts[0].Outline.Max(q => q.Y) / 2));
            var n = Nodes(w); Click(w, At(w, 0, n[0].X, n[0].Y));
            Assert.Equal(0, Ed(w).State.Node);
            Assert.Contains(Ed(w).ContextMenu!.Items.OfType<MenuItem>(), i => i.Name == "garment-edit-menu-insert" && i.IsEnabled); // Ersatzweg für die Leiste
            // auch in Einfach: Punkt ziehen wirkt
            var from = At(w, 0, n[0].X, n[0].Y); var to = from + new Point(0, 12);
            w.MouseDown(from, MouseButton.Left); for (var i = 1; i <= 4; i++) { w.MouseMove(from + (to - from) * (i / 4.0)); Settle(); } w.MouseUp(to, MouseButton.Left); Settle();
            Assert.Equal("Punkt verschoben", w.ProgramActive(Key)!.UndoLabel);
            Assert.Null(w.TryFind<TextBox>("garment-node-x")); Assert.Null(w.TryFind<TextBlock>("garment-measure-seams"));
            Assert.NotNull(w.TryFind<TextBlock>("garment-measure-edges"));
            // Gegenprüfung T-0745 (6): in „Einfach“ den Rechtsklick-Eintrag wirklich auslösen – Rechtsklick auf die längste Kante, „Punkt hier einfügen“
            var nn = Nodes(w);
            static double Len(List<PatPt> p) => Enumerable.Range(1, p.Count - 1).Sum(k => Math.Sqrt(Math.Pow(p[k].X - p[k - 1].X, 2) + Math.Pow(p[k].Y - p[k - 1].Y, 2)));
            var longest = Enumerable.Range(0, nn.Count).MaxBy(s => Len(GarmentEdit.SegmentPoints(nn, s)));
            var sp = GarmentEdit.SegmentPoints(nn, longest); var midPt = sp[sp.Count / 2];
            if (sp.Count == 2) midPt = new PatPt((sp[0].X + sp[1].X) / 2, (sp[0].Y + sp[1].Y) / 2);
            var at = At(w, 0, midPt.X, midPt.Y);
            w.MouseMove(at); w.MouseDown(at, MouseButton.Right); w.MouseUp(at, MouseButton.Right); Settle();
            var menu = Ed(w).ContextMenu!; Assert.True(menu.IsOpen, "Rechtsklick-Menü ist nicht offen");
            var insert = menu.Items.OfType<MenuItem>().Single(i => i.Name == "garment-edit-menu-insert"); Assert.True(insert.IsEnabled);
            insert.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Settle(); menu.Close(); Settle();
            Assert.Equal("Punkt eingefügt", w.ProgramActive(Key)!.UndoLabel);
            Assert.Equal(nn.Count + 1, Nodes(w).Count);
            w.ProgramSetLevel(UserLevel.Profi); w.Show(Key); Settle();
            Assert.NotNull(w.TryFind<TextBox>("garment-node-x")); Assert.NotNull(w.TryFind<TextBlock>("garment-measure-seams"));
            Assert.NotNull(w.TryFind<Button>("garment-edit-insert"));
        }
        finally { w.Close(); }
    }

    static Control? Focused(MainWindow w) => w.FocusManager?.GetFocusedElement() as Control;

    [AvaloniaFact(DisplayName = "Gegenprüfung T-0745 (2): keine Tastatur-Falle – ohne Auswahl wandert Tab aus der Fläche; nach Enter im Zahlenfeld bleibt der Fokus im Feld")]
    public void Keyboard_no_trap()
    {
        var (w, d) = Open();
        try
        {
            Ed(w).BringIntoView(); Settle(); Ed(w).Focus(); Settle();
            Assert.Same(Ed(w), Focused(w)); Assert.Equal(-1, Ed(w).State.Part);
            Key1(w, Avalonia.Input.Key.Tab, PhysicalKey.Tab);
            Assert.NotNull(Focused(w)); Assert.NotSame(Ed(w), Focused(w));
            Ed(w).Focus(); Settle();
            Key1(w, Avalonia.Input.Key.Escape, PhysicalKey.Escape); // Esc ohne Auswahl tut nichts und bleibt frei für die Hülle
            Key1(w, Avalonia.Input.Key.Down, PhysicalKey.ArrowDown);
            Assert.Null(d.Data[GarmentEdit.Field]);

            var front0 = Ed(w).Garment.Parts[0];
            Click(w, At(w, 0, 0, front0.Outline.Max(q => q.Y) / 2));
            var sh = Nodes(w).FindIndex(q => q.Edge == "shR");
            Click(w, At(w, 0, Nodes(w)[sh].X, Nodes(w)[sh].Y));
            Assert.Equal(sh, Ed(w).State.Node);
            var box = w.Find<TextBox>("garment-node-y"); box.BringIntoView(); box.Focus(); Settle();
            var y = Nodes(w)[sh].Y + 1;
            box.Text = y.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
            Key1(w, Avalonia.Input.Key.Enter, PhysicalKey.Enter);
            Assert.Equal("Punkt gesetzt", d.UndoLabel);
            Assert.Equal(y, Nodes(w)[sh].Y, 2);
            Assert.Equal("garment-node-y", Focused(w)?.Name); // das neu aufgebaute Feld hat den Fokus, nicht die Fläche
            Assert.NotSame(box, Focused(w));
            // noch ein Enter: wieder im Feld, die Fläche holt den Fokus nicht
            Key1(w, Avalonia.Input.Key.Enter, PhysicalKey.Enter);
            Assert.Equal("garment-node-y", Focused(w)?.Name);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Gegenprüfung T-0745 (7): Pfeil-Schritte am selben Punkt innerhalb 1 s sind EIN Rückgängig-Schritt; Pause oder andere Änderung trennt")]
    public void Arrow_steps_grouped()
    {
        var (w, d) = Open();
        try
        {
            var t = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
            var front0 = Ed(w).Garment.Parts[0];
            Click(w, At(w, 0, 0, front0.Outline.Max(q => q.Y) / 2));
            var hem = Nodes(w).FindIndex(q => q.Edge == "hem");
            Click(w, At(w, 0, Nodes(w)[hem].X, Nodes(w)[hem].Y));
            Assert.Equal(hem, Ed(w).State.Node);
            Ed(w).State.Clock = () => t;
            var y0 = Nodes(w)[hem].Y; var h0 = d.History.Count;
            void Down() { Ed(w).Focus(); Key1(w, Avalonia.Input.Key.Down, PhysicalKey.ArrowDown, RawInputModifiers.Shift); }
            void Undo() { Ed(w).Focus(); Key1(w, Avalonia.Input.Key.Z, PhysicalKey.Z, RawInputModifiers.Control); }
            // drei Schritte kurz hintereinander → ein Schritt
            for (var k = 0; k < 3; k++) { Down(); t += TimeSpan.FromMilliseconds(300); }
            Assert.Equal(y0 - 3, Nodes(w)[hem].Y, 3);
            Assert.Equal(h0 + 1, d.History.Count);
            Assert.Equal("Punkt verschoben", d.UndoLabel);
            Undo();
            Assert.Equal(y0, Nodes(w)[hem].Y, 3); Assert.Null(d.Data[GarmentEdit.Field]);
            // mit Pause über 1 s: zwei Schritte
            Down(); t += TimeSpan.FromSeconds(2); Down();
            Assert.Equal(h0 + 2, d.History.Count);
            Undo(); Assert.Equal(y0 - 1, Nodes(w)[hem].Y, 3);
            Undo(); Assert.Equal(y0, Nodes(w)[hem].Y, 3);
            // andere Änderung dazwischen (K: Kante als Kurve) trennt, auch ohne Pause
            Down(); Ed(w).Focus(); Key1(w, Avalonia.Input.Key.K, PhysicalKey.K); Down();
            Assert.Equal(["Punkt verschoben", "Kante als Kurve", "Punkt verschoben"], d.History.Take(3));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Gegenprüfung T-0745 (1, 4): Ecke genau auf die Nachbarecke gesetzt wird mit Erklärung abgelehnt; Export nennt sich überschneidende Teile")]
    public void Reject_and_export_hint()
    {
        var (w, d) = Open();
        try
        {
            var front0 = Ed(w).Garment.Parts[0];
            Click(w, At(w, 0, 0, front0.Outline.Max(q => q.Y) / 2));
            var n = Nodes(w); var hem = n.FindIndex(q => q.Edge == "hem"); var side = n.FindIndex(q => q.Edge == "sideR");
            Click(w, At(w, 0, n[hem].X, n[hem].Y));
            Assert.Equal(hem, Ed(w).State.Node);
            var de = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
            w.Find<TextBox>("garment-node-x").Text = n[side].X.ToString("0.####", de); w.Find<TextBox>("garment-node-y").Text = n[side].Y.ToString("0.####", de);
            w.Find<Button>("garment-node-set").Press(); Settle();
            Assert.Null(d.Data[GarmentEdit.Field]); // nichts übernommen
            Assert.Contains(Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(w.ActivePageRoot!).OfType<TextBlock>(), t => (t.Text ?? "").Contains("keine Länge mehr", StringComparison.Ordinal));
            // sich überschneidender Umriss: Hinweis auf der Fläche und beim Export
            var crossed = GarmentEdit.MoveNode(n, hem, front0.Edge("hem").Length / 2 + 10, 30);
            d.Set(GarmentEdit.Field, GarmentEdit.With(null, "tshirt", front0.Name, crossed), "Probe"); w.Show(Key); Settle();
            Assert.NotNull(w.GarmentExport(d, ".dxf"));
            Assert.Contains("überschneidet sich", w.GarmentMessage);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0745: Klick bis Datei – bearbeiteter Saum landet in DXF, SVG, PDF, Datenblatt und im Anprobe-Auftrag")]
    public async Task Click_to_file()
    {
        var (w, d) = Open();
        try
        {
            var before = new Dictionary<string, string>();
            // Stand vorher ohne Export über die Oberfläche (dessen Meldung läge über der Fläche)
            before[".dxf"] = GarmentProduction.Dxf(Ed(w).Garment); before[".svg"] = GarmentProduction.Svg(Ed(w).Garment, d.Text("name", "Entwurf"));
            var front0 = Ed(w).Garment.Parts[0];
            Click(w, At(w, 0, 0, front0.Outline.Max(q => q.Y) / 2));
            var n = Nodes(w); var hem = n.FindIndex(q => q.Edge == "hem"); var side = n.FindIndex(q => q.Edge == "sideR");
            // beide Saumecken 5 cm tiefer (genau, über die Pfeiltaste mit Umschalt)
            foreach (var i in new[] { hem, side })
            {
                Click(w, At(w, 0, Nodes(w)[i].X, Nodes(w)[i].Y));
                Assert.True(i == Ed(w).State.Node, $"Punkt {i} erwartet, gewählt {Ed(w).State.Node}, Teil {Ed(w).State.Part}: {w.Find<TextBlock>("garment-edit-hint").Text}");
                for (var k = 0; k < 5; k++) { Ed(w).Focus(); Key1(w, Avalonia.Input.Key.Down, PhysicalKey.ArrowDown, RawInputModifiers.Shift); }
            }
            var front = Ed(w).Garment.Parts[0];
            Assert.Equal(front0.Edge("sideR").Length + 5, front.Edge("sideR").Length, 2);
            foreach (var ext in new[] { ".dxf", ".svg", ".pdf", ".md" })
            {
                var f = w.GarmentExport(d, ext); Assert.NotNull(f); Assert.True(new FileInfo(f!).Length > 100);
                if (before.TryGetValue(ext, out var old)) Assert.NotEqual(old, File.ReadAllText(f!));
            }
            var dxf = File.ReadAllText(w.GarmentExport(d, ".dxf")!);
            Assert.Equal(GarmentProduction.Dxf(Ed(w).Garment), dxf);
            Assert.Contains("Frei bearbeitet:** Vorderteil", File.ReadAllText(w.GarmentExport(d, ".md")!));
            Assert.Contains("näht die frei bearbeiteten Teile", w.Find<TextBlock>("garment-tryon-edited").Text);
            JsonObject? job = null;
            w.GarmentRunOverride = f => { job = JsonNode.Parse(File.ReadAllText(f))!.AsObject(); return Task.FromResult<string?>("ersetzt"); };
            await w.GarmentTryOn(d); Settle();
            var hemPts = job!["parts"]![0]!["edges"]!.AsArray().First(e => (string)e!["name"]! == "hem")!["pts"]!.AsArray();
            Assert.Equal(front.Edge("hem").Points[0].Y, (double)hemPts[0]![1]!, 2);
            Assert.Equal(front0.Edge("hem").Points[0].Y - 5, (double)hemPts[0]![1]!, 2);
            // Beleg T-0745: echte Dateien aus diesem Klick-Ablauf für die Prüfung mit Fremdwerkzeugen (nur auf Wunsch)
            if (Environment.GetEnvironmentVariable("TRENDO_T0745_OUT") is { Length: > 0 } outDir)
            {
                Directory.CreateDirectory(outDir);
                foreach (var ext in new[] { ".dxf", ".svg", ".pdf" }) File.Copy(w.GarmentExport(d, ext)!, Path.Combine(outDir, "schnitt-bearbeitet" + ext), true);
                File.WriteAllText(Path.Combine(outDir, "anprobe-auftrag.json"), job.ToJsonString());
                File.WriteAllText(Path.Combine(outDir, "dokument-felder.json"), new JsonObject { [GarmentEdit.Field] = d.Data[GarmentEdit.Field]!.DeepClone() }.ToJsonString());
                var g = Ed(w).Garment;
                var expect = new JsonObject
                {
                    ["parts"] = new JsonArray([.. g.Parts.Select(p => (JsonNode)new JsonObject
                    {
                        ["name"] = p.Name, ["area_cm2"] = Math.Round(p.Area, 3), ["perimeter_cm"] = Math.Round(p.Edges.Sum(e => e.Length), 3),
                        ["cut_w_cm"] = Math.Round(GarmentProduction.WithAllowance(p).Max(q => q.X) - GarmentProduction.WithAllowance(p).Min(q => q.X), 3),
                        ["cut_h_cm"] = Math.Round(GarmentProduction.WithAllowance(p).Max(q => q.Y) - GarmentProduction.WithAllowance(p).Min(q => q.Y), 3),
                    })]),
                    ["template_front"] = new JsonObject { ["area_cm2"] = Math.Round(front0.Area, 3), ["perimeter_cm"] = Math.Round(front0.Edges.Sum(e => e.Length), 3), ["cut_h_cm"] = Math.Round(GarmentProduction.WithAllowance(front0).Max(q => q.Y) - GarmentProduction.WithAllowance(front0).Min(q => q.Y), 3) },
                };
                File.WriteAllText(Path.Combine(outDir, "erwartet.json"), expect.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
        }
        finally { w.Close(); }
    }
}
