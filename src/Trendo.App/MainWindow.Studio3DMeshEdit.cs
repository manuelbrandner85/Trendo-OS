using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0691: Bearbeitungsmodus von „3D-Modelle erstellen“ – wie in Blender zwischen Objekt und Bearbeiten umschalten (Tab),
/// Punkte, Kanten oder Flächen wählen (1/2/3, Umschalt erweitert), Auswahl verschieben/drehen/skalieren (G/R/S bzw. Anfasser),
/// extrudieren (E), Fläche füllen (F), Kante teilen, verschmelzen (M), Normalen neu berechnen (Umschalt+N), löschen (Entf).
/// Jede Änderung ist ein Schritt im Rückgängig des Studios. Grundformen werden beim Umschalten in ein bearbeitbares Netz umgewandelt.</summary>
public partial class MainWindow
{
    /// <summary>Netze mit mehr Flächen bearbeitet Trendo nicht direkt (die Ansicht zeichnet jeden Punkt); Weg: in Blender bearbeiten.</summary>
    public const int MeshEditMaxFaces = 60_000;

    public bool MeshEditing { get; private set; }
    /// <summary>„punkt“, „kante“ oder „flaeche“.</summary>
    public string MeshSelectMode { get; private set; } = "punkt";
    public HashSet<int> MeshSelVerts { get; } = [];
    public HashSet<(int A, int B)> MeshSelEdges { get; } = [];
    public HashSet<int> MeshSelFaces { get; } = [];
    private double _meshExtrude = 2, _meshMergeDist = 0.01;
    (string Id, float[] Pos, int[] Idx, EditMesh Mesh)? _meshCache;

    /// <summary>Netz des gewählten Teils (lokal); null, wenn nicht im Bearbeitungsmodus.</summary>
    public EditMesh? MeshCurrent
    {
        get
        {
            if (!MeshEditing || Selected is not { Shape: "mesh", Positions: { } pos, Indices: { } idx } p) return null;
            if (_meshCache is { } c && c.Id == p.Id && ReferenceEquals(c.Pos, pos) && ReferenceEquals(c.Idx, idx)) return c.Mesh;
            var m = EditMesh.FromPart(p); _meshCache = (p.Id, pos, idx, m); return m;
        }
    }

    void MeshEditReset() { MeshEditing = false; MeshClearSel(); _meshCache = null; }
    void MeshClearSel() { MeshSelVerts.Clear(); MeshSelEdges.Clear(); MeshSelFaces.Clear(); }

    /// <summary>Nach Rückgängig/Wiederholen: Auswahl auf gültige Nummern kürzen.</summary>
    void MeshEditAfterSwap()
    {
        _meshCache = null;
        if (!MeshEditing) return;
        if (MeshCurrent is not { } m) { MeshEditing = Selected is { Shape: "mesh" }; MeshClearSel(); return; }
        MeshSelVerts.RemoveWhere(v => v >= m.V.Count); MeshSelFaces.RemoveWhere(f => f >= m.Faces);
        MeshSelEdges.RemoveWhere(e => e.A >= m.V.Count || e.B >= m.V.Count);
    }

    /// <summary>Nach einem Absturz: gesicherte Szene übernehmen (bitte speichern).</summary>
    void Restore3D(Scene3D s)
    {
        if (_scene.Parts.Count > 0) Push();
        _scene = s; _sceneEpoch++; _selId = s.Parts.FirstOrDefault()?.Id; MeshEditReset(); _pngKey = "";
        Scene3DDirty = true;
        _core.Audit.Append(Principal.LocalOwner.Name, "3d.recover", s.Name);
        Say($"„{s.Name}“ wiederhergestellt – bitte speichern.", "3d");
    }

    // ---------- Öffentliche Wege (Oberfläche, Tests, KI) ----------
    /// <summary>Zwischen Objektmodus und Bearbeitungsmodus wechseln. Eine Grundform wird dabei (als ein Rückgängig-Schritt) zum Netz.</summary>
    public void Studio3DEditMode(bool on)
    {
        if (!on) { MeshEditReset(); Say("Objektmodus.", "3d"); return; }
        if (Selected is not { } p) { Say("Wähle zuerst ein Teil, dann „Bearbeiten“ (Tab).", "3d"); return; }
        var cur = EditMesh.FromPart(p);
        if (cur.Faces == 0) { Say("Dieses Teil hat keine Fläche zum Bearbeiten.", "3d"); return; }
        if (cur.Faces > MeshEditMaxFaces) { Say($"{p.Name} hat {cur.Faces:N0} Flächen – zum Bearbeiten hier höchstens {MeshEditMaxFaces:N0}. Größere Netze in Blender bearbeiten („In Blender öffnen“).", "3d"); return; }
        var converted = p.Shape != "mesh";
        if (converted) { Push(); cur.WriteTo(p); }
        MeshEditing = true; MeshClearSel(); _meshCache = null; if (_gizmo3d.Length == 0) _gizmo3d = "move";
        Say(converted ? $"{p.Name} ist jetzt ein bearbeitbares Netz ({cur.Stats()}). Rückgängig macht es wieder zur Grundform."
            : $"Bearbeiten: {p.Name} · {cur.Stats()}. Klick wählt, Umschalt+Klick erweitert, 1/2/3 Punkte/Kanten/Flächen.", "3d");
    }

    public void Studio3DSelectMode(string mode)
    {
        if (mode is not ("punkt" or "kante" or "flaeche")) return;
        // Auswahl mitnehmen wie in Blender: Flächen → ihre Punkte, Punkte → Kanten/Flächen, die ganz gewählt sind
        var m = MeshCurrent; var verts = m is null ? [] : SelectedVertsOf(m);
        MeshSelectMode = mode; MeshClearSel();
        if (m is not null && verts.Count > 0)
        {
            if (mode == "punkt") MeshSelVerts.UnionWith(verts);
            else if (mode == "kante") MeshSelEdges.UnionWith(m.Edges().Where(e => verts.Contains(e.A) && verts.Contains(e.B)));
            else MeshSelFaces.UnionWith(Enumerable.Range(0, m.Faces).Where(f => verts.Contains(m.T[f * 3]) && verts.Contains(m.T[f * 3 + 1]) && verts.Contains(m.T[f * 3 + 2])));
        }
        Say(mode switch { "punkt" => "Auswahl: Punkte.", "kante" => "Auswahl: Kanten.", _ => "Auswahl: Flächen." }, "3d");
    }

    /// <summary>Element wählen (a = Punkt/Fläche oder Kantenanfang, b = Kantenende); add = Auswahl erweitern bzw. abwählen; a &lt; 0 ohne add leert die Auswahl.</summary>
    public void Studio3DMeshSelect(string kind, int a, int b = -1, bool add = false)
    {
        if (MeshCurrent is null) return;
        if (!add) MeshClearSel();
        if (a < 0) { Show("3d"); return; }
        switch (kind)
        {
            case "punkt": if (!MeshSelVerts.Add(a)) MeshSelVerts.Remove(a); break;
            case "kante": var e = EditMesh.Key(a, b); if (!MeshSelEdges.Add(e)) MeshSelEdges.Remove(e); break;
            default: if (!MeshSelFaces.Add(a)) MeshSelFaces.Remove(a); break;
        }
        Show("3d");
    }

    public void Studio3DMeshSelectAll(bool all = true)
    {
        if (MeshCurrent is not { } m) return;
        MeshClearSel();
        if (all)
        {
            if (MeshSelectMode == "punkt") MeshSelVerts.UnionWith(Enumerable.Range(0, m.V.Count));
            else if (MeshSelectMode == "kante") MeshSelEdges.UnionWith(m.Edges());
            else MeshSelFaces.UnionWith(Enumerable.Range(0, m.Faces));
        }
        Show("3d");
    }

    HashSet<int> SelectedVertsOf(EditMesh m)
    {
        var set = new HashSet<int>(MeshSelVerts.Where(v => v < m.V.Count));
        foreach (var (a, b) in MeshSelEdges) { set.Add(a); set.Add(b); }
        foreach (var f in MeshSelFaces) if (f < m.Faces) { set.Add(m.T[f * 3]); set.Add(m.T[f * 3 + 1]); set.Add(m.T[f * 3 + 2]); }
        set.RemoveWhere(v => v >= m.V.Count);
        return set;
    }

    /// <summary>Gewählte Flächen; in den anderen Arten die Flächen, deren drei Punkte alle gewählt sind.</summary>
    List<int> SelectedFacesOf(EditMesh m)
    {
        if (MeshSelectMode == "flaeche") return [.. MeshSelFaces.Where(f => f < m.Faces)];
        var v = SelectedVertsOf(m);
        return [.. Enumerable.Range(0, m.Faces).Where(f => v.Contains(m.T[f * 3]) && v.Contains(m.T[f * 3 + 1]) && v.Contains(m.T[f * 3 + 2]))];
    }

    /// <summary>Werkzeug am Netz ausführen: auf einer Kopie rechnen; erst wenn es klappt, ein Rückgängig-Schritt und zurück ins Teil.</summary>
    bool MeshRun(Func<EditMesh, Part3D, string> op)
    {
        if (MeshCurrent is not { } cur || Selected is not { } p) { Say("Zuerst ein Teil wählen und „Bearbeiten“ (Tab).", "3d"); return false; }
        var work = new EditMesh(); work.V.AddRange(cur.V); work.T.AddRange(cur.T);
        string msg;
        try { msg = op(work, p); }
        catch (MeshEditError e) { Say(e.Message, "3d"); return false; }
        if (work.Faces == 0) { Say("Danach bliebe keine Fläche übrig – das Teil lieber im Objektmodus löschen (Entf).", "3d"); return false; }
        Push(); work.WriteTo(p); _meshCache = (p.Id, p.Positions!, p.Indices!, work);
        Say(msg + " · " + work.Stats(), "3d");
        return true;
    }

    Vec3 SelCenterLocal(EditMesh m) => m.Centroid(SelectedVertsOf(m));

    /// <summary>Auswahl in der Szene verschieben (cm).</summary>
    public bool Studio3DMeshMove(Vec3 world) => MeshRun((m, p) =>
    {
        var v = SelectedVertsOf(m); if (v.Count == 0) throw new MeshEditError("Zuerst Punkte, Kanten oder Flächen wählen.");
        m.Translate(v, EditMesh.DirToLocal(world, p));
        return $"{v.Count} Punkte verschoben ({Part3D.Num(world.X)} | {Part3D.Num(world.Y)} | {Part3D.Num(world.Z)} cm)";
    });

    /// <summary>Auswahl um eine Szenenachse durch ihre Mitte drehen (Grad).</summary>
    public bool Studio3DMeshRotate(char axis, double degrees) => MeshRun((m, p) =>
    {
        var v = SelectedVertsOf(m); if (v.Count == 0) throw new MeshEditError("Zuerst Punkte, Kanten oder Flächen wählen.");
        m.Rotate(v, EditMesh.DirToLocal(Axis(axis), p), degrees, m.Centroid(v));
        return $"{v.Count} Punkte um {char.ToUpperInvariant(axis)} um {Part3D.Num(degrees)}° gedreht";
    });

    /// <summary>Auswahl skalieren – gleichmäßig (axis = ' ') oder entlang einer Szenenachse.</summary>
    public bool Studio3DMeshScale(double factor, char axis = ' ') => MeshRun((m, p) =>
    {
        var v = SelectedVertsOf(m); if (v.Count == 0) throw new MeshEditError("Zuerst Punkte, Kanten oder Flächen wählen.");
        if (factor <= 0) throw new MeshEditError("Der Faktor muss größer als 0 sein.");
        if (axis == ' ') m.Scale(v, factor, m.Centroid(v)); else m.ScaleAlong(v, EditMesh.DirToLocal(Axis(axis), p), factor, m.Centroid(v));
        return $"{v.Count} Punkte skaliert × {Part3D.Num(factor)}";
    });

    static Vec3 Axis(char a) => char.ToLowerInvariant(a) switch { 'x' => new(1, 0, 0), 'y' => new(0, 1, 0), _ => new(0, 0, 1) };

    public bool Studio3DMeshExtrude(double? distance = null)
    {
        var d = distance ?? _meshExtrude; List<int>? moved = null;
        var ok = MeshRun((m, _) => { var f = SelectedFacesOf(m); if (f.Count == 0) throw new MeshEditError("Zum Extrudieren Flächen wählen (Taste 3), dann E."); moved = m.Extrude(f, d); return $"{f.Count} Flächen um {Part3D.Num(d)} cm extrudiert"; });
        if (ok && moved is not null) { MeshSelectMode = "flaeche"; MeshClearSel(); MeshSelFaces.UnionWith(moved); }
        return ok;
    }

    public bool Studio3DMeshDelete()
    {
        var ok = MeshRun((m, _) => MeshSelectMode switch
        {
            "punkt" when MeshSelVerts.Count > 0 => $"{MeshSelVerts.Count} Punkte gelöscht ({m.DeleteVerts(MeshSelVerts)} Flächen weg)",
            "kante" when MeshSelEdges.Count > 0 => $"{MeshSelEdges.Count} Kanten gelöscht ({m.DeleteEdges(MeshSelEdges)} Flächen weg)",
            "flaeche" when MeshSelFaces.Count > 0 => $"{m.DeleteFaces(MeshSelFaces)} Flächen gelöscht",
            _ => throw new MeshEditError("Nichts gewählt zum Löschen."),
        });
        if (ok) MeshClearSel();
        return ok;
    }

    public bool Studio3DMeshFill()
    {
        List<int>? added = null;
        var ok = MeshRun((m, _) => { added = m.FillFace([.. SelectedVertsOf(m)]); return "Neue Fläche"; });
        if (ok && added is not null) { MeshSelectMode = "flaeche"; MeshClearSel(); MeshSelFaces.UnionWith(added); }
        return ok;
    }

    public bool Studio3DMeshSplit()
    {
        var edges = MeshSelEdges.ToList(); var news = new List<int>();
        var ok = MeshRun((m, _) =>
        {
            if (MeshSelectMode != "kante" || edges.Count == 0) throw new MeshEditError("Zum Teilen Kanten wählen (Taste 2).");
            foreach (var (a, b) in edges) news.Add(m.SplitEdge(a, b));
            return $"{edges.Count} Kanten geteilt";
        });
        if (ok) { MeshSelectMode = "punkt"; MeshClearSel(); MeshSelVerts.UnionWith(news); }
        return ok;
    }

    public bool Studio3DMeshMerge()
    {
        var keep = -1;
        var ok = MeshRun((m, _) => { var v = SelectedVertsOf(m); keep = m.MergeVerts(v); return $"{v.Count} Punkte in der Mitte verschmolzen"; });
        if (ok) { MeshSelectMode = "punkt"; MeshClearSel(); if (keep >= 0) MeshSelVerts.Add(keep); }
        return ok;
    }

    public bool Studio3DMeshMergeByDistance(double? distance = null)
    {
        var d = distance ?? _meshMergeDist;
        var ok = MeshRun((m, _) => { var n = m.MergeByDistance(d); if (n == 0) throw new MeshEditError($"Keine Punkte näher als {Part3D.Num(d)} cm."); return $"{n} doppelte Punkte verschmolzen"; });
        if (ok) MeshClearSel();
        return ok;
    }

    public bool Studio3DMeshNormals() => MeshRun((m, _) => { var n = m.RecalcNormals(); return n == 0 ? "Normalen geprüft – alle zeigen schon nach außen" : $"Normalen neu berechnet: {n} Flächen umgedreht"; });

    public bool Studio3DMeshFlip() => MeshRun((m, _) => { var f = SelectedFacesOf(m); if (f.Count == 0) throw new MeshEditError("Zum Umdrehen Flächen wählen."); m.FlipFaces(f); return $"{f.Count} Flächen umgedreht"; });

    /// <summary>Weltpunkte, Kanten und Auswahl in die Ansicht geben (nur im Bearbeitungsmodus).</summary>
    void MeshEditToView(CadView vp)
    {
        if (MeshCurrent is not { } m || Selected is not { } p) return;
        vp.EditVerts = m.V.Select(v => EditMesh.ToWorld(v, p)).ToList();
        vp.EditTris = m.T; vp.EditEdges = m.Edges(); vp.EditMode = MeshSelectMode;
        vp.EditSelVerts = MeshSelVerts; vp.EditSelEdges = MeshSelEdges; vp.EditSelFaces = MeshSelFaces;
        vp.ElementClicked = (kind, a, b, add) => { SaveCam(vp); Studio3DMeshSelect(kind, a, b, add); };
        vp.PartClicked = null; vp.PartMoved = null;
        var sel = SelectedVertsOf(m);
        if (sel.Count > 0)
        {
            vp.GizmoMode = _gizmo3d.Length > 0 ? _gizmo3d : "move"; vp.GizmoAt = EditMesh.ToWorld(m.Centroid(sel), p);
            vp.GizmoDragged = (ax, mode, amount) =>
            {
                SaveCam(vp);
                if (mode == "move") Studio3DMeshMove(Axis(ax) * amount);
                else if (mode == "rotate") Studio3DMeshRotate(ax, amount);
                else Studio3DMeshScale(amount, ax);
                Show("3d");
            };
        }
        else { vp.GizmoMode = ""; vp.GizmoAt = null; vp.GizmoDragged = null; }
    }

    /// <summary>Tasten im Bearbeitungsmodus. Rückgabe: erledigt.</summary>
    bool MeshEditKey(KeyEventArgs e)
    {
        var mods = e.KeyModifiers;
        if (e.Key == Key.Tab && mods == KeyModifiers.None) { Studio3DEditMode(!MeshEditing); return true; }
        if (!MeshEditing) return false;
        switch (e.Key)
        {
            case Key.D1 or Key.NumPad1 when mods == KeyModifiers.None: Studio3DSelectMode("punkt"); return true;
            case Key.D2 or Key.NumPad2 when mods == KeyModifiers.None: Studio3DSelectMode("kante"); return true;
            case Key.D3 or Key.NumPad3 when mods == KeyModifiers.None: Studio3DSelectMode("flaeche"); return true;
            case Key.A when mods == KeyModifiers.None: Studio3DMeshSelectAll(); return true;
            case Key.A when mods == KeyModifiers.Alt: Studio3DMeshSelectAll(false); return true;
            case Key.E when mods == KeyModifiers.None: Studio3DMeshExtrude(); return true;
            case Key.F when mods == KeyModifiers.None: Studio3DMeshFill(); return true;
            case Key.M when mods == KeyModifiers.None: Studio3DMeshMerge(); return true;
            case Key.N when mods == KeyModifiers.Shift: Studio3DMeshNormals(); return true;
            case Key.Delete or Key.X when mods == KeyModifiers.None: Studio3DMeshDelete(); return true;
            case Key.Escape: MeshClearSel(); return true;
        }
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.PageUp or Key.PageDown)
        {
            var step = mods.HasFlag(KeyModifiers.Shift) ? 10 : 1;
            var by = e.Key switch { Key.Left => new Vec3(-step, 0, 0), Key.Right => new Vec3(step, 0, 0), Key.Up => new Vec3(0, step, 0), Key.Down => new Vec3(0, -step, 0), Key.PageUp => new Vec3(0, 0, step), _ => new Vec3(0, 0, -step) };
            Studio3DMeshMove(by); return true;
        }
        return false;
    }

    /// <summary>Rechtsklick-Menü der 3D-Fläche – im Bearbeitungsmodus die Netzwerkzeuge, sonst die Teil-Befehle.</summary>
    ContextMenu Menu3D(CadView vp)
    {
        var menu = new ContextMenu { Name = "3d-context" };
        void Item(string name, string header, string? key, Action a, bool enabled = true)
        {
            var mi = new MenuItem { Name = name, Header = header, InputGesture = key is null ? null : KeyGesture.Parse(key), IsEnabled = enabled };
            mi.Click += (_, _) => { SaveCam(vp); a(); Show("3d"); };
            menu.Items.Add(mi);
        }
        void Sep() => menu.Items.Add(new Separator());
        var any = Selected is not null;
        if (MeshEditing)
        {
            Item("3d-ctx-mode-punkt", "Punkte wählen", "D1", () => Studio3DSelectMode("punkt"));
            Item("3d-ctx-mode-kante", "Kanten wählen", "D2", () => Studio3DSelectMode("kante"));
            Item("3d-ctx-mode-flaeche", "Flächen wählen", "D3", () => Studio3DSelectMode("flaeche"));
            Item("3d-ctx-all", "Alles auswählen", "A", () => Studio3DMeshSelectAll());
            Sep();
            Item("3d-ctx-extrude", "Extrudieren", "E", () => Studio3DMeshExtrude());
            Item("3d-ctx-fill", "Fläche füllen (3–4 Punkte)", "F", () => Studio3DMeshFill());
            Item("3d-ctx-split", "Kante teilen", null, () => Studio3DMeshSplit());
            Item("3d-ctx-merge", "Punkte verschmelzen (Mitte)", "M", () => Studio3DMeshMerge());
            Item("3d-ctx-merge-dist", "Doppelte Punkte verschmelzen", null, () => Studio3DMeshMergeByDistance());
            Item("3d-ctx-normals", "Normalen neu berechnen", "Shift+N", () => Studio3DMeshNormals());
            Item("3d-ctx-flip", "Flächen umdrehen", null, () => Studio3DMeshFlip());
            Item("3d-ctx-delete", "Auswahl löschen", "Delete", () => Studio3DMeshDelete());
            Sep();
            Item("3d-ctx-object", "Zurück zum Objektmodus", "Tab", () => Studio3DEditMode(false));
        }
        else
        {
            Item("3d-ctx-edit", "Netz bearbeiten (Punkte, Kanten, Flächen)", "Tab", () => Studio3DEditMode(true), any);
            Item("3d-ctx-dup", "Verdoppeln", "Ctrl+D", Studio3DDuplicate, any);
            Item("3d-ctx-floor", "Auf den Boden", null, Studio3DDrop, any);
            Item("3d-ctx-remove", "Teil löschen", "Delete", Studio3DDeleteSelected, any);
        }
        Sep();
        Item("3d-ctx-undo", "Rückgängig", "Ctrl+Z", UndoScene, _undo3d.Count > 0);
        Item("3d-ctx-redo", "Wiederholen", "Ctrl+Y", RedoScene, _redo3d.Count > 0);
        return menu;
    }

    /// <summary>Werkzeugkarte rechts im Bearbeitungsmodus (sonst leer und unsichtbar).</summary>
    Control MeshEditCard()
    {
        if (!MeshEditing || MeshCurrent is not { } m) return new Border { Name = "3d-mesh-card", IsVisible = false };
        var de = CultureInfo.GetCultureInfo("de-DE");
        Button B(string name, string label, string tip, Action a, bool on = false)
        {
            var b = Act(name, label, () => { a(); Show("3d"); }); b.Padding = new Thickness(8, 3); b.FontSize = 12; b.Margin = new Thickness(0, 0, 4, 4); ToolTip.SetTip(b, tip);
            if (on) { b.Background = Accent; b.Foreground = Brushes.White; }
            return b;
        }
        var dist = new TextBox { Name = "3d-mesh-extrude-amt", Text = _meshExtrude.ToString("0.##", de), Width = 64 };
        dist.LostFocus += (_, _) => { var v = Read(dist.Text); if (!double.IsNaN(v) && Math.Abs(v) > 1e-6) _meshExtrude = v; };
        var md = new TextBox { Name = "3d-mesh-merge-amt", Text = _meshMergeDist.ToString("0.###", de), Width = 64 };
        md.LostFocus += (_, _) => { var v = Read(md.Text); if (!double.IsNaN(v) && v > 0) _meshMergeDist = v; };
        var sel = SelectedVertsOf(m);
        var info = sel.Count == 0 ? "Nichts gewählt – klicke auf das Netz." : MeshSelectMode switch
        {
            "kante" => $"{MeshSelEdges.Count} Kanten gewählt",
            "flaeche" => $"{MeshSelFaces.Count} Flächen gewählt",
            _ => $"{MeshSelVerts.Count} Punkte gewählt",
        };
        return Card(new StackPanel { Name = "3d-mesh-card", Spacing = 6, Children = {
            H("Netz bearbeiten", 14),
            new TextBlock { Name = "3d-mesh-stats", Text = m.Stats() + (m.OpenEdges() > 0 ? $" · {m.OpenEdges()} offene Kanten" : " · geschlossen"), Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap },
            new WrapPanel { Children = {
                B("3d-mesh-mode-punkt", "Punkte", "Punkte wählen (Taste 1)", () => Studio3DSelectMode("punkt"), MeshSelectMode == "punkt"),
                B("3d-mesh-mode-kante", "Kanten", "Kanten wählen (Taste 2)", () => Studio3DSelectMode("kante"), MeshSelectMode == "kante"),
                B("3d-mesh-mode-flaeche", "Flächen", "Flächen wählen (Taste 3)", () => Studio3DSelectMode("flaeche"), MeshSelectMode == "flaeche"),
                B("3d-mesh-all", "Alles", "Alles auswählen (A)", () => Studio3DMeshSelectAll()) } },
            new TextBlock { Name = "3d-mesh-sel", Text = info, Foreground = Text, FontSize = 12 },
            new WrapPanel { Children = { Labeled("Abstand cm", dist), B("3d-mesh-extrude", "Extrudieren", "Gewählte Flächen herausziehen (E)", () => Studio3DMeshExtrude()) } },
            new WrapPanel { Children = {
                B("3d-mesh-fill", "Fläche füllen", "Aus 3 oder 4 gewählten Punkten eine Fläche (F)", () => Studio3DMeshFill()),
                B("3d-mesh-split", "Kante teilen", "Gewählte Kanten in der Mitte teilen", () => Studio3DMeshSplit()),
                B("3d-mesh-merge", "Verschmelzen", "Gewählte Punkte in ihrer Mitte zusammenführen (M)", () => Studio3DMeshMerge()),
                B("3d-mesh-delete", "Löschen", "Gewählte Punkte, Kanten oder Flächen löschen (Entf)", () => Studio3DMeshDelete()) } },
            new WrapPanel { Children = {
                Labeled("Abstand cm", md), B("3d-mesh-merge-dist", "Doppelte verschmelzen", "Punkte näher als der Abstand zusammenführen", () => Studio3DMeshMergeByDistance()) } },
            new WrapPanel { Children = {
                B("3d-mesh-normals", "Normalen neu berechnen", "Alle Flächen nach außen richten (Umschalt+N)", () => Studio3DMeshNormals()),
                B("3d-mesh-flip", "Umdrehen", "Gewählte Flächen umdrehen", () => Studio3DMeshFlip()) } },
            new TextBlock { Text = "Tasten: Tab Objekt/Bearbeiten · 1/2/3 Punkte/Kanten/Flächen · G/R/S Verschieben/Drehen/Skalieren mit den Anfassern · Pfeile 1 cm (Umschalt 10 cm) · E Extrudieren · F Fläche · M Verschmelzen · Entf Löschen · Strg+Z/Strg+Y", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap },
        } });
    }
}
