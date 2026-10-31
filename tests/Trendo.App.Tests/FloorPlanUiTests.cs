using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.Core.Arch;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0256: Grundriss-Programm über den echten Eingabeweg – Werkzeuge anklicken, mit der Maus zeichnen und ziehen, Tasten, Rückgängig, Export.</summary>
public class SPEC01_Ui
{
    const string AK = "architektur";
    static FloorPlan Plan(MainWindow w) => FloorPlan.From(w.ProgramActive(AK)!.Data);

    /// <summary>Weltpunkt (cm) des Plans → Fensterpunkt über die echte Zeichenfläche.</summary>
    static Point At(MainWindow w, double x, double y)
    {
        Settle(); w.UpdateLayout(); Settle();
        var c = Canvas(w); var s = w.ArchState;
        return c.TranslatePoint(new Point((x - s.PanX) * s.Zoom, (y - s.PanY) * s.Zoom), w) ?? throw new InvalidOperationException("Zeichenfläche nicht sichtbar");
    }

    /// <summary>Sichtbares Element mit Namen – nach einem Neuzeichnen kann die abgelöste Seite kurz noch im Baum hängen.</summary>
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }

    /// <summary>Die sichtbare Zeichenfläche (eine abgelöste ältere Seite kann kurz noch im Baum hängen).</summary>
    static FloorPlanView Canvas(MainWindow w) => w.GetVisualDescendants().OfType<FloorPlanView>().First(c => c.Name == "arch-canvas" && c.IsEffectivelyVisible && c.Bounds.Width > 0);

    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }

    static void Drag(MainWindow w, Point a, Point b)
    {
        w.MouseDown(a, MouseButton.Left); Settle();
        w.MouseMove(new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2)); w.MouseMove(b); Settle();
        w.MouseUp(b, MouseButton.Left); Settle();
    }

    static void Type(MainWindow w, string box, string text)
    {
        var t = Get<TextBox>(w, box); t.Focus(); t.Text = text; Settle();
        w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); w.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Settle();
    }

    static void Click(MainWindow w, Point p) { w.MouseMove(p); w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle(); }

    [AvaloniaFact(DisplayName = "SPEC-01 UI: Grundriss – leerer Plan, Raum mit der Maus aufziehen, Tür und Fenster setzen, Möbel stellen und ziehen, Entf, Rückgängig")]
    public void Draw_with_mouse()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-01"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        Assert.NotNull(Canvas(w));
        Assert.Null(w.TryFind<Button>("arch-add")); // kein Formular mehr

        // Raum: Rechteck aufziehen → Raum mit 4 Wänden
        RealClick(w, Get<Button>(w, "arch-tool-raum"));
        Assert.Equal("raum", w.ArchState.Tool);
        w.ArchState.UserView = true; // Ansicht festhalten, damit die Umrechnung stabil bleibt
        Drag(w, At(w, 0, 0), At(w, 400, 300));
        var p = Plan(w);
        var room = Assert.Single(p.Rooms);
        Assert.Equal(4, p.Walls.Count);
        Assert.InRange(room.Area / 10000, 11.5, 12.5); // 4 × 3 m, Fang am 10-cm-Raster

        // Tür und Fenster in die Wände klicken
        RealClick(w, Get<Button>(w, "arch-tool-tuer"));
        Click(w, At(w, 200, room.Points.Max(q => q.Y) + 12));
        RealClick(w, Get<Button>(w, "arch-tool-fenster"));
        Click(w, At(w, 200, room.Points.Min(q => q.Y) - 12));
        p = Plan(w);
        Assert.Single(p.Openings, o => o.Kind == "tuer"); Assert.Single(p.Openings, o => o.Kind == "fenster");
        Assert.Equal(1, p.Figures(p.Rooms[0]).Doors);

        // Möbel aus dem Katalog stellen und mit der Maus verschieben
        RealClick(w, Get<Button>(w, "arch-tool-moebel"));
        Press(w, "arch-item-sessel");
        Click(w, At(w, 100, 100));
        var item = Assert.Single(Plan(w).Items);
        Assert.Equal("sessel", item.Type); Assert.Equal(100, item.X, 0); Assert.Equal(100, item.Y, 0);
        RealClick(w, Get<Button>(w, "arch-tool-auswahl"));
        Drag(w, At(w, 100, 100), At(w, 200, 150));
        item = Plan(w).Items[0];
        Assert.InRange(item.X, 195, 205); Assert.InRange(item.Y, 145, 155);
        Assert.Equal(item.Id, w.ArchState.Selected);

        // Eigenschaften: Drehen per Knopf, Löschen per Entf, Rückgängig per Knopf
        Press(w, "arch-rotate");
        Assert.Equal(90, Plan(w).Items[0].Rot);
        Canvas(w).Focus(); Settle();
        w.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null); w.KeyRelease(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null); Settle();
        Assert.Empty(Plan(w).Items);
        RealClick(w, Get<Button>(w, "prog-undo"));
        Assert.Single(Plan(w).Items);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-01 UI: Grundriss – Vorlage, Raum auswählen und umbenennen, Boden wählen, Wandlänge eintippen, Plan prüfen, 3D, Exporte")]
    public void Properties_check_export()
    {
        var (w, l, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-01"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-wohnung"));
        Assert.Contains("Wohnfläche", Get<TextBlock>(w, "prog-status-text").Text);

        // Raum über die Projektliste auswählen, umbenennen, Boden auf Fliesen
        Press(w, "arch-room-2");
        Type(w, "arch-p-name", "Wohnküche");
        Assert.Contains(Plan(w).Rooms, r => r.Name == "Wohnküche");
        Get<ComboBox>(w, "arch-p-floor").SelectedIndex = Array.IndexOf(PlanDrawing.Floors, "fliesen"); Settle();
        Assert.Equal("fliesen", Plan(w).Rooms.First(r => r.Name == "Wohnküche").Floor);
        Assert.Contains("Fliesen 30 × 60", string.Join(" ", w.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)));

        // Wand anklicken und Länge eintippen: angeschlossene Wände gehen mit
        var wall = Plan(w).Walls.OrderBy(x => x.Y1).ThenBy(x => x.X1).First(x => Math.Abs(x.Y1 - x.Y2) < 0.1);
        RealClick(w, Get<Button>(w, "arch-tool-auswahl"));
        w.ArchState.UserView = true;
        Click(w, At(w, Math.Min(wall.X1, wall.X2) + 40, wall.Y1)); // neben Fenstern/Türen (die hätten beim Klick Vorrang)
        Assert.Equal(wall.Id, w.ArchState.Selected);
        Type(w, "arch-p-length", "600");
        Assert.Equal(600, Plan(w).WallOf(wall.Id)!.Length, 1);

        // Prüfen: Treffer anklicken wählt das Element aus
        RealClick(w, Get<Button>(w, "prog-cmd-arch-check"));
        Assert.Contains("Prüfung", string.Join(" ", w.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)));
        Press(w, "arch-issue-0");
        Assert.NotNull(w.ArchState.Selected);

        // 3D und zurück
        RealClick(w, Get<Button>(w, "prog-cmd-arch-2d3d"));
        Assert.True(w.Arch3D); Assert.NotNull(Get<CadView>(w, "arch-3d"));
        RealClick(w, Get<Button>(w, "prog-cmd-arch-2d3d"));
        Assert.False(w.Arch3D);

        // Exporte landen als Dateien
        foreach (var x in new[] { "pdf", "svg", "dxf", "raumbuch", "material" })
        {
            RealClick(w, Get<Button>(w, "prog-export-" + x));
            Assert.Contains("Exportiert:", w.ProgramMessage(AK));
            var file = w.ProgramMessage(AK)["Exportiert: ".Length..].Trim();
            Assert.True(File.Exists(file) && new FileInfo(file).Length > 100, x + ": " + file);
        }
        // Speichern als Trendo-Projekt und wieder öffnen
        RealClick(w, Get<Button>(w, "prog-save"));
        var saved = w.ProgramActive(AK)!.Path!;
        w.ProgramClose(AK, 0); w.ProgramOpen(AK, saved); w.Show(AK); Settle();
        Assert.Contains(Plan(w).Rooms, r => r.Name == "Wohnküche");
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-01 UI: Wand-Werkzeug – Wandzug Punkt für Punkt, Länge tippen + Enter, Esc beendet")]
    public void Wall_chain()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-01"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        RealClick(w, Get<Button>(w, "arch-tool-wand"));
        w.ArchState.UserView = true;
        Click(w, At(w, 0, 0));
        Click(w, At(w, 300, 4)); // nahe waagrecht → rastet waagrecht ein
        var p = Plan(w); var first = Assert.Single(p.Walls);
        Assert.Equal(0, first.Y2, 1); Assert.Equal(300, first.X2, 1);
        // Richtung nach unten zeigen, 2,5 m tippen, Enter
        w.MouseMove(At(w, 300, 100)); Settle();
        Canvas(w).Focus(); Settle();
        foreach (var (k, ph) in new[] { (Key.D2, PhysicalKey.Digit2), (Key.OemComma, PhysicalKey.Comma), (Key.D5, PhysicalKey.Digit5) })
        { w.KeyPress(k, RawInputModifiers.None, ph, null); w.KeyRelease(k, RawInputModifiers.None, ph, null); }
        w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); w.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Settle();
        p = Plan(w);
        Assert.Equal(2, p.Walls.Count);
        var second = p.Walls[1];
        Assert.Equal(250, second.Length, 1); Assert.Equal(300, second.X2, 1); Assert.Equal(250, second.Y2, 1);
        Canvas(w).Focus(); Settle();
        w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); w.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Settle();
        Assert.Null(w.ArchState.Chain);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-01 UI Ausbau: Material, Tapete, Heizung, Licht, Treppe und Wohnfläche direkt in den Eigenschaften")]
    public void Plus()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-01"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-wohnung"));
        string Texts() => string.Join(" ", w.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text));
        Assert.Contains("Wohnfläche (WoFlV)", Texts());
        Press(w, "arch-room-0");
        Get<ComboBox>(w, "arch-p-finish").SelectedIndex = Array.IndexOf(PlanDrawing.Finishes, "tapete"); Settle();
        var t = Texts();
        Assert.Contains("Rollen", t); Assert.Contains("Heizleistung", t); Assert.Contains("LED à 806 lm", t); Assert.Contains("Pakete à 2,2 m²", t);
        RealClick(w, Get<Button>(w, "arch-tool-moebel"));
        Get<Button>(w, "arch-item-treppe").BringIntoView(); Settle(); // Katalog nach unten scrollen
        RealClick(w, Get<Button>(w, "arch-item-treppe"));
        w.ArchState.UserView = true;
        Click(w, At(w, 250, 250));
        Assert.Contains("Steigungen", Texts());
        w.Close();
    }

    static void Key1(MainWindow w, Key k, PhysicalKey ph) { w.KeyPress(k, RawInputModifiers.None, ph, null); w.KeyRelease(k, RawInputModifiers.None, ph, null); Settle(); }

    [AvaloniaFact(DisplayName = "T-0257 UI: Doppelklick erkennt Raum, Raum Punkt für Punkt (Enter), Bemaßen mit drei Klicks, Text setzen und ändern")]
    public void Detect_polygon_dimension_text()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-01"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-leer"));
        w.ArchEdit("Wände", pl => { pl.AddWall(0, 0, 500, 0); pl.AddWall(500, 0, 500, 400); pl.AddWall(500, 400, 0, 400); pl.AddWall(0, 400, 0, 0); });
        RealClick(w, Get<Button>(w, "arch-tool-raum"));
        w.ArchState.UserView = true;
        // echter Doppelklick in die umschlossene Fläche
        var mid = At(w, 250, 200);
        w.MouseMove(mid); w.MouseDown(mid, MouseButton.Left); w.MouseUp(mid, MouseButton.Left); w.MouseDown(mid, MouseButton.Left); w.MouseUp(mid, MouseButton.Left); Settle();
        var r = Assert.Single(Plan(w).Rooms);
        Assert.InRange(r.Area / 10000, 17.6, 18.2); // lichte Fläche 4,76 × 3,76 m = 17,90 m² (Wandachsen 5 × 4 m, Stärke 24 cm)

        // Raum Punkt für Punkt außerhalb: 4 Klicks, Enter
        foreach (var (x, y) in new[] { (700.0, 0.0), (1000.0, 0.0), (1000.0, 200.0), (700.0, 200.0) }) Click(w, At(w, x, y));
        Canvas(w).Focus(); Settle(); Key1(w, Key.Enter, PhysicalKey.Enter);
        Assert.Equal(2, Plan(w).Rooms.Count);
        Assert.Equal(6.0, Plan(w).Rooms[1].Area / 10000, 1);

        // Bemaßen: Anfang, Ende, Abstand
        RealClick(w, Get<Button>(w, "arch-tool-mass"));
        Click(w, At(w, 0, 0)); Click(w, At(w, 500, 0)); Click(w, At(w, 250, -80));
        var dim = Assert.Single(Plan(w).Dimensions);
        Assert.Equal(500, dim.Length, 0); Assert.InRange(dim.Offset, -90, -70);

        // Text setzen und rechts ändern
        RealClick(w, Get<Button>(w, "arch-tool-text"));
        Click(w, At(w, 250, 300));
        Assert.Single(Plan(w).Labels);
        Type(w, "arch-p-text", "Hier kommt das Sofa hin");
        Assert.Equal("Hier kommt das Sofa hin", Plan(w).Labels[0].Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0257 UI: Geschoss anlegen und wechseln, Vorlage-Bild laden und Maßstab über zwei Klicks festlegen, OBJ-Export")]
    public void Levels_background_obj()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        w.Show(MainWindow.StudioPages["SPEC-01"]); Settle();
        RealClick(w, Get<Button>(w, "prog-tpl-bad"));
        RealClick(w, Get<Button>(w, "arch-level-add"));
        Assert.Equal(2, FloorPlan.Levels(w.ProgramActive(AK)!.Data).Count);
        Assert.Equal("Obergeschoss", Plan(w).LevelName); Assert.Empty(Plan(w).Rooms);
        RealClick(w, Get<Button>(w, "arch-level-0"));
        Assert.Equal("Erdgeschoss", Plan(w).LevelName); Assert.NotEmpty(Plan(w).Rooms);
        RealClick(w, Get<Button>(w, "arch-level-1"));

        // Vorlage-Bild: 400 × 300 Pixel, darin eine 200 Pixel lange Strecke = 5 m
        var png = Img.Png(root, 400, 300, (100, 100, 299, 110));
        Get<TextBox>(w, "arch-bg-path").BringIntoView(); Get<TextBox>(w, "arch-bg-path").Text = png; Settle();
        Press(w, "arch-bg-load");
        var bg = Plan(w).Background!; Assert.Equal(400, bg.PxW);
        Press(w, "arch-cal");
        Assert.Equal("kalib", w.ArchState.Tool);
        w.ArchState.UserView = true;
        double Wx(int px) => bg.X + px * bg.CmPerPx; double Wy(int px) => bg.Y + px * bg.CmPerPx;
        Click(w, At(w, Wx(100), Wy(105))); Click(w, At(w, Wx(300), Wy(105)));
        Type(w, "arch-p-callen", "500");
        Assert.Equal(2.5, Plan(w).Background!.CmPerPx, 1);
        Assert.Equal("auswahl", w.ArchState.Tool);

        RealClick(w, Get<Button>(w, "prog-export-obj"));
        var file = w.ProgramMessage(AK)["Exportiert: ".Length..].Trim();
        Assert.True(File.Exists(file) && File.Exists(Path.ChangeExtension(file, ".mtl")));
        Assert.Contains("usemtl", File.ReadAllText(file));
        w.Close();
    }
}
