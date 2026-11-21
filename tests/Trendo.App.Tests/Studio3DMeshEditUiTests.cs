using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Cad;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0691: 3D-Studio und CAD – Netzbearbeitung in der Ansicht (Maus, Tasten, Rechtsklick), Wiederholen, Wiederherstellung nach Neustart,
/// Import fremder Dateien und Export bis zur echten Datei.</summary>
public class Studio3DMeshEditUiTests
{
    static string Fixture(string name)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent;
        return Path.Combine(d!.FullName, "tests", "fixtures", "t0691", name);
    }
    static CadView Vp(MainWindow w) { Settle(); return V<CadView>(w, "3d-viewport"); }
    static void Key1(MainWindow w, Key k, PhysicalKey ph, RawInputModifiers m = RawInputModifiers.None) { Vp(w).Focus(); w.KeyPress(k, m, ph, null); w.KeyRelease(k, m, ph, null); Settle(); }
    static void Click(MainWindow w, Point inView, bool shift = false)
    {
        var p = Vp(w).TranslatePoint(inView, w)!.Value; var m = shift ? RawInputModifiers.Shift : RawInputModifiers.None;
        w.MouseDown(p, MouseButton.Left, m); w.MouseUp(p, MouseButton.Left, m); Settle();
    }
    /// <summary>Knopf in den sichtbaren Bereich rollen (Seitenleisten scrollen), dann echt klicken.</summary>
    static void Seen(MainWindow w, Control c) { c.BringIntoView(); Settle(); RealClick(w, c); }
    static string Msg(MainWindow w) => ((TextBlock)w.Find<Border>("w45-msg").Child!).Text ?? "";

    [AvaloniaFact(DisplayName = "T-0691 3D: Würfel einsetzen → Tab (Bearbeiten) → Taste 3 → Fläche anklicken → E extrudiert → Strg+Z / Strg+Y → als glTF exportiert, Datei wieder lesbar")]
    public void Edit_extrude_undo_redo_export()
    {
        var (w, _, _) = Ui.Open();
        w.Height = 1200; w.Width = 1600; // größere Arbeitsfläche (bei 900 px Höhe bleiben der 3D-Ansicht nur rund 100 px – siehe Bericht)
        w.Show("3d"); Settle();
        RealClick(w, V<Button>(w, "3d-new-cube"));
        Assert.Equal("cube", w.Studio3DParts[0].Shape);
        Key1(w, Key.Tab, PhysicalKey.Tab);
        Assert.True(w.MeshEditing); Assert.Equal("mesh", w.Studio3DParts[0].Shape);
        Assert.Equal(8, w.MeshCurrent!.V.Count); Assert.Equal(12, w.MeshCurrent.Faces);
        Assert.Contains("8 Punkte", V<TextBlock>(w, "3d-mesh-stats").Text);
        Key1(w, Key.D3, PhysicalKey.Digit3); Assert.Equal("flaeche", w.MeshSelectMode);
        // eine sichtbare Fläche mit der Maus treffen: Mitte eines Dreiecks, das die Ansicht dort meldet
        var vp = Vp(w); vp.InvalidateVisual(); Settle(); vp = Vp(w);
        var m = w.MeshCurrent!; Point? target = null; var face = -1;
        for (var f = 0; f < m.Faces && target is null; f++)
        {
            var a = vp.EditScreenOf(m.T[f * 3])!.Value; var b = vp.EditScreenOf(m.T[f * 3 + 1])!.Value; var c = vp.EditScreenOf(m.T[f * 3 + 2])!.Value;
            var mid = new Point((a.X + b.X + c.X) / 3, (a.Y + b.Y + c.Y) / 3);
            if (vp.EditPick(mid).A == f) { target = mid; face = f; }
        }
        Assert.NotNull(target);
        Assert.NotNull(Vp(w).ElementClicked);
        var hit = w.InputHitTest(Vp(w).TranslatePoint(target!.Value, w)!.Value) as Control;
        Assert.True(ReferenceEquals(hit, Vp(w)), "Treffer: " + hit?.GetType().Name + " " + hit?.Name + " Ziel " + target + " Ansicht " + Vp(w).Bounds + " Fenster " + w.ClientSize + " in Fenster " + Vp(w).TranslatePoint(target!.Value, w));
        Click(w, target!.Value);
        Assert.Equal([face], w.MeshSelFaces);
        Key1(w, Key.E, PhysicalKey.E);
        Assert.Equal(18, w.MeshCurrent!.Faces); Assert.Equal(0, w.MeshCurrent.OpenEdges());
        Assert.Contains("extrudiert", Msg(w));
        var extruded = w.Studio3DScene.ToJson();
        Key1(w, Key.Z, PhysicalKey.Z, RawInputModifiers.Control); Assert.Equal(12, w.MeshCurrent!.Faces);
        Key1(w, Key.Y, PhysicalKey.Y, RawInputModifiers.Control); Assert.Equal(18, w.MeshCurrent!.Faces);
        Assert.Equal(extruded, w.Studio3DScene.ToJson());
        // Rechtsklick-Menü der 3D-Fläche bietet die Netzwerkzeuge
        var menu = Vp(w).ContextMenu!;
        var names = menu.Items.OfType<MenuItem>().Select(i => i.Name).ToList();
        foreach (var n in new[] { "3d-ctx-extrude", "3d-ctx-fill", "3d-ctx-split", "3d-ctx-merge", "3d-ctx-normals", "3d-ctx-delete", "3d-ctx-undo", "3d-ctx-redo", "3d-ctx-object" }) Assert.Contains(n, names);
        // Entf löscht die gewählten Flächen, Tab zurück in den Objektmodus
        Key1(w, Key.Delete, PhysicalKey.Delete); Assert.Equal(17, w.MeshCurrent!.Faces);
        Key1(w, Key.Tab, PhysicalKey.Tab); Assert.False(w.MeshEditing);
        // Export bis zur echten Datei: glTF, wieder einlesen
        RealClick(w, V<Button>(w, "3d-export-gltf"));
        var file = Directory.GetFiles(w.Folder3D, "*.gltf").Single();
        var back = Mesh3D.ImportParts(file, "cm");
        Assert.Equal(17, back.Sum(p => p.Indices!.Length / 3));
    }

    [AvaloniaFact(DisplayName = "T-0691 3D: Punkte-Modus – Punkt anklicken, Umschalt+Klick erweitert, Pfeil hoch verschiebt nur die Auswahl; Fläche füllen und Normalen über die Knöpfe")]
    public void Vertex_select_and_move()
    {
        var (w, _, _) = Ui.Open();
        w.Height = 1200; w.Width = 1600; // größere Arbeitsfläche (bei 900 px Höhe bleiben der 3D-Ansicht nur rund 100 px – siehe Bericht)
        w.Show("3d"); Settle();
        RealClick(w, V<Button>(w, "3d-new-cube"));
        RealClick(w, V<Button>(w, "3d-mode-edit")); Assert.True(w.MeshEditing);
        var vp = Vp(w); var m = w.MeshCurrent!;
        // zwei Punkte, die die Ansicht vorn zeigt
        var picks = Enumerable.Range(0, m.V.Count).Where(i => vp.EditPick(vp.EditScreenOf(i)!.Value).A == i).Take(2).ToList();
        Assert.Equal(2, picks.Count);
        Click(w, vp.EditScreenOf(picks[0])!.Value); Click(w, Vp(w).EditScreenOf(picks[1])!.Value, shift: true);
        Assert.Equal(picks.ToHashSet(), w.MeshSelVerts);
        var before = picks.Select(i => w.MeshCurrent!.V[i]).ToList();
        var others = Enumerable.Range(0, m.V.Count).Except(picks).Select(i => w.MeshCurrent!.V[i]).ToList();
        Key1(w, Key.Up, PhysicalKey.ArrowUp); // +1 cm in Y (Szene)
        var after = w.MeshCurrent!;
        Assert.Contains(after.V, v => before.Any(b => Math.Abs(v.Y - b.Y - 1) < 1e-6 && Math.Abs(v.X - b.X) < 1e-6));
        Assert.All(others, o => Assert.Contains(after.V, v => (v - o).Len < 1e-6));
        // Fläche löschen und mit „Fläche füllen“ wieder schließen
        w.Studio3DSelectMode("flaeche"); w.Studio3DMeshSelectAll(); Settle();
        Assert.Equal(12, w.MeshSelFaces.Count);
        w.Studio3DMeshSelect("flaeche", 0); w.Studio3DMeshDelete(); Assert.Equal(11, w.MeshCurrent!.Faces);
        Assert.Equal(3, w.MeshCurrent.OpenEdges());
        w.Studio3DSelectMode("punkt");
        var rim = OpenVerts(w.MeshCurrent);
        foreach (var v in rim) w.Studio3DMeshSelect("punkt", v, add: true);
        Assert.True(w.MeshEditing, "Bearbeitungsmodus aus"); Settle(); Assert.True(w.GetVisualDescendants().OfType<Button>().Any(b => b.Name == "3d-mesh-fill"), "Knopf fehlt: " + Msg(w));
        Seen(w, V<Button>(w, "3d-mesh-fill"));
        Assert.Equal(12, w.MeshCurrent!.Faces); Assert.Equal(0, w.MeshCurrent.OpenEdges());
        Seen(w, V<Button>(w, "3d-mesh-normals"));
        Assert.True(w.MeshCurrent!.Volume() > 0);
    }

    static List<int> OpenVerts(EditMesh m)
    {
        var count = new Dictionary<(int, int), int>();
        for (var t = 0; t < m.T.Count; t += 3) for (var k = 0; k < 3; k++) { var e = EditMesh.Key(m.T[t + k], m.T[t + (k + 1) % 3]); count[e] = count.GetValueOrDefault(e) + 1; }
        return count.Where(c => c.Value == 1).SelectMany(c => new[] { c.Key.Item1, c.Key.Item2 }).Distinct().ToList();
    }

    [AvaloniaFact(DisplayName = "T-0691 3D: Fremde Blender-Datei einlesen (wie Ziehen auf die Fläche), Punkt verschieben, Wiederherstellung nach Neustart bietet die ungespeicherte Szene vollständig an")]
    public void Import_edit_recover()
    {
        var (w, _, root) = Ui.Open();
        w.Show("3d"); Settle();
        var import = w.Import3DFileAsync(Fixture("affe-blender.glb")); while (!import.IsCompleted) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); import.GetAwaiter().GetResult(); Settle();
        Assert.Single(w.Studio3DParts);
        w.Studio3DEditMode(true); Settle();
        w.Studio3DMeshSelect("punkt", 0); Assert.True(w.Studio3DMeshMove(new Vec3(0, 0, 5)));
        Assert.True(w.Scene3DDirty);
        var json = w.Studio3DScene.ToJson();
        w.ProgramAutosaveAll(); // läuft in der App alle 20 s und beim Schließen (T-0714: Notsicherung der Hülle)
        Assert.True(w.OfficeBackupPending("3d"));
        w.Close();
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
        var w2 = new MainWindow(core, new AppOptions(root, null, 0, Isolated: true), new Probe("browser")); w2.Show(); Settle();
        w2.Show("3d"); Settle();
        RealClick(w2, V<Button>(w2, "office-restore-3d-0"));
        Assert.Equal(json, w2.Studio3DScene.ToJson()); // vollständig, Punkt für Punkt
        Assert.True(w2.Scene3DDirty); Assert.Empty(w2.OfficeRecoveries("3d"));
        w2.Close();
    }

    // ---------- CAD ----------
    [AvaloniaFact(DisplayName = "T-0691 CAD: Fremd-DXF einlesen → Ebenen sichtbar → Kontur als Körper hochziehen → als DXF speichern; Strg+Z nimmt zurück; Wiederherstellung nach Neustart")]
    public void Cad_dxf_import_extrude_export_recover()
    {
        var (w, _, root) = Ui.Open();
        w.CadNew(); w.Show("cad"); Settle();
        var msg = w.CadImport(Fixture("fremd-halteplatte.dxf")); w.Show("cad"); Settle();
        Assert.Contains("1× Bemaßung", msg); // Bemaßung ehrlich gemeldet (T-0747-fix10: deutscher Wortlaut aus KindLabel)
        Assert.Contains("Elemente auf", V<TextBlock>(w, "cad-drawing-summary").Text);
        Assert.True(w.CadCanvas!.Wires.Count > 50); // Linien der Zeichnung in der Ansicht
        Assert.Contains(w.CadCanvas.Labels, l => l.Text == "Halteplatte A");
        // Ebene „Hilfslinien“ ist in der Datei aus – ihre Linien fehlen in der Ansicht
        Assert.DoesNotContain(w.CadCanvas.Wires, x => Math.Abs(x.A.Y - 25) < 1e-9 && Math.Abs(x.B.Y - 25) < 1e-9 && Math.Abs(x.B.X - x.A.X) > 70);
        var konturIdx = w.Cad.Drawing!.Layers.FindIndex(l => l.Name == "Kontur");
        Seen(w, V<Button>(w, $"cad-layer-extrude-{konturIdx}"));
        var body = w.Cad.Bodies[^1]; Assert.Equal("Kontur", body.Name);
        var vol = w.Cad.BuildBody(body).Volume();
        Assert.InRange(vol, 6 * 3978.0, 6 * 3979.5); // 80 × 50 mit Viertelkreis R10 an einer Ecke, 6 mm hoch
        // Bohrungen-Ebene abziehen: zweite Kontur-Gruppe auf eigener Ebene → eigenes Teil, Kreise als Skizzen
        var bores = w.CadExtrudeLayer("Bohrungen", 6)!; Assert.Equal(2, bores.Features.Count);
        // DXF speichern und unabhängig wieder lesen
        var f = w.CadExportDxf();
        var (back, skipped) = CadDxf.Read(f);
        Assert.Empty(skipped);
        Assert.Contains(back.Layers, l => l.Name == "Umriss"); Assert.Contains(back.Layers, l => l.Name == "Kontur");
        Assert.Equal(w.Cad.Drawing.Entities.Count(e => e.Kind == "kreis"), back.Entities.Count(e => e.Kind == "kreis"));
        // Strg+Z in der Ansicht
        var bodies = w.Cad.Bodies.Count;
        w.CadCanvas!.Focus(); w.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null); w.KeyRelease(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null); Settle();
        Assert.Equal(bodies - 1, w.Cad.Bodies.Count);
        Assert.NotNull(w.CadCanvas!.ContextMenu); Assert.Contains(w.CadCanvas.ContextMenu!.Items.OfType<MenuItem>(), i => i.Name == "cad-ctx-import");
        // Absturz: ungespeichertes Bauteil wird gesichert und nach Neustart angeboten
        Assert.True(w.CadDirty);
        var json = w.Cad.ToJson();
        w.ProgramAutosaveAll(); Assert.True(w.OfficeBackupPending("cad"));
        w.Close();
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher());
        var w2 = new MainWindow(core, new AppOptions(root, null, 0, Isolated: true), new Probe("browser")); w2.Show(); Settle();
        w2.Show("cad"); Settle();
        RealClick(w2, V<Button>(w2, "office-restore-cad-0"));
        Assert.Equal(json, w2.Cad.ToJson()); Assert.True(w2.CadDirty); Assert.Null(w2.CadPath);
        // Speichern räumt die Sicherung weg
        w2.ProgramAutosaveAll(); Assert.True(w2.OfficeBackupPending("cad"));
        w2.CadSave(Path.Combine(root, "halter.tcad")); Assert.False(w2.OfficeBackupPending("cad"));
        w2.Close();
    }

    [AvaloniaFact(DisplayName = "T-0691 CAD: STL (von Blender) und facettiertes STEP als Referenzkörper; echtes B-Rep-STEP wird ehrlich abgelehnt; STEP-Export heißt „facettiert“")]
    public void Cad_reference_bodies()
    {
        var (w, _, root) = Ui.Open();
        w.CadNew(); w.Show("cad"); Settle();
        w.CadImport(Fixture("affe-blender.stl")); w.Show("cad"); Settle();
        Assert.Single(w.Cad.References);
        Assert.Contains(w.CadCanvas!.Parts, p => p.Color == "#9CA3AF");
        w.CadAdd(CadOp.Quader);
        var step = w.CadExportAs("step"); Assert.True(File.Exists(step));
        var bad = Path.Combine(root, "echt.step");
        File.WriteAllText(bad, "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=ADVANCED_FACE('',(#2),#3,.T.);\nENDSEC;\nEND-ISO-10303-21;\n");
        var ex = Assert.Throws<CadError>(() => w.CadImport(bad)); Assert.Contains("facettierte", ex.Message);
        w.CadImport(step); Assert.Equal(2, w.Cad.References.Count);
        w.Show("cad"); Settle();
        Assert.Contains(V<Button>(w, "cad-file").ContextMenu!.Items.OfType<MenuItem>(), i => i.Name == "cad-m-step" && (i.Header as string ?? "").Contains("facettiert"));
        Assert.Contains(V<Button>(w, "cad-file").ContextMenu!.Items.OfType<MenuItem>(), i => i.Name == "cad-m-import");
    }
}
