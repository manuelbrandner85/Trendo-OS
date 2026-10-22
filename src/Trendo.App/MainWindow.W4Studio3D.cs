using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-05: 3D-Studio. Formen, Maße und Vorschau laufen in Trendo. Blender nur zum Weiterbearbeiten.</summary>
public partial class MainWindow
{
    public string Folder3D => WorkFolder("3D");
    public string? BlenderPath => ToolPath("blender") is { } p && File.Exists(p) ? p : Cli.Find("blender");
    /// <summary>Für Tests: liefert Dateien statt des System-Dialogs.</summary>
    public Func<Task<IReadOnlyList<string>>>? Pick3DFiles { get; set; }
    /// <summary>Für Tests: ersetzt die lokale KI.</summary>
    public Func<string, Task<string>>? Ki3DOverride { get; set; }
    public bool Ki3DForceReady { get; set; }

    private Scene3D _szene = new();
    private int _ansicht = 5;
    private string? _letzteDatei;
    private bool _blenderFragen;
    private bool _blenderDanachOeffnen;
    private bool _importMm;
    private List<ScenePart>? _vorschlag;
    private static readonly CultureInfo De3 = new("de-DE");

    private void InitW4Studio3D() => RegisterTab(KreativTabs, "3d", "3D", "CORE-05", Studio3DView, "blender", "3d", "modell", "stl", "gltf", "rendern", "würfel", "kugel");

    public void OpenSaved3D(string projectId)
    {
        try
        {
            var json = _core.Projects.ReadFile(projectId, "3d/szene.json");
            _szene = Scene3D.FromJson(json);
            _szene.ProjektId = projectId;
            _vorschlag = null;
            Say($"Modell aus dem Projekt „{projectId}“ geöffnet.", "3d");
        }
        catch (Exception ex) { Say("Das Projekt hat kein 3D-Modell: " + ex.Message, "3d"); }
    }

    public string Import3D(string path, bool millimeters)
    {
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (!MeshIo.Extensions.Contains(ext)) return "Diese Datei kann Trendo nicht öffnen. Erlaubt sind OBJ, STL, GLB, GLTF, FBX und BLEND.";
            if (ext is ".blend" || (ext == ".fbx" && IsBinaryFbx(path)))
            {
                if (BlenderPath is not { } bl) { _blenderFragen = true; return "Dafür braucht Trendo Blender. Unten kannst du es einrichten. OBJ, STL und GLB gehen auch ohne."; }
                var tmp = Path.Combine(Path.GetTempPath(), "trendo-3d-import-" + Guid.NewGuid().ToString("N") + ".glb");
                try
                {
                    var (r, o) = Studio3D.Convert(bl, path, ".glb", tmp);
                    if (!r.Ok || !File.Exists(o)) return "Blender konnte die Datei nicht lesen. " + Kurz(r.Err.Length > 0 ? r.Err : r.Out);
                    return Laden(MeshIo.ReadGlb(o), "glTF rechnet in Metern. Trendo hat auf Zentimeter umgestellt.");
                }
                finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }
            }
            if (ext is ".glb") return Laden(MeshIo.ReadGlb(path), "glTF rechnet in Metern. Trendo hat auf Zentimeter umgestellt.");
            if (ext is ".gltf") return Laden(MeshIo.ReadGltfFile(path), "glTF rechnet in Metern. Trendo hat auf Zentimeter umgestellt.");
            if (ext is ".stl") return Laden([MeshIo.ReadStl(path, millimeters)], millimeters ? "Maße als Millimeter gelesen und in Zentimeter umgerechnet." : "Maße als Zentimeter gelesen.");
            if (ext is ".fbx") return Laden([MeshIo.ReadFbx(path)], millimeters ? "Maße als Millimeter gelesen." : "Maße als Zentimeter gelesen.");
            var objs = MeshIo.ReadObj(path);
            if (millimeters) foreach (var o in objs) Skaliere(o.Mesh, 0.1);
            return Laden(objs, millimeters ? "Maße als Millimeter gelesen und in Zentimeter umgerechnet." : "Maße als Zentimeter gelesen.");
        }
        catch (Exception ex) { return ex.Message; }
    }

    private string Laden(IReadOnlyList<MeshIo.Loaded> parts, string hinweis)
    {
        if (parts.Count == 0) return "In der Datei steckt kein Körper.";
        var msgs = new List<string>();
        foreach (var p in parts) msgs.Add(_szene.AusDatei(p.Name, p.Mesh, p.Farbe));
        return string.Join(" ", msgs) + " " + hinweis;
    }

    private static void Skaliere(Mesh3D m, double s)
    {
        for (var i = 0; i < m.V.Count; i++) m.V[i] = m.V[i] * s;
    }

    private static bool IsBinaryFbx(string path)
    {
        var buf = new byte[24];
        using var fs = File.OpenRead(path);
        var n = fs.Read(buf, 0, buf.Length);
        return n >= 18 && System.Text.Encoding.ASCII.GetString(buf, 0, 18).Contains("Kaydara FBX Binary");
    }

    private void Schritt(Func<string> job)
    {
        try { Say(job(), "3d"); }
        catch (Exception ex) { Say(ex.Message, "3d"); }
    }

    private static bool Zahl(string? s, out double v) =>
        double.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    private TextBox ZahlFeld(string name, double wert, double width = 72) =>
        new() { Name = name, Text = wert.ToString("0.##", De3), Width = width };

    private void VorschauAktualisieren() { /* Bild entsteht beim Neuzeichnen der Seite. */ }

    private string Export(string ext)
    {
        if (_szene.Teile.Count == 0) return "Es gibt noch kein Objekt. Füge zuerst eine Form hinzu.";
        Mesh3D mesh;
        try { mesh = _szene.Welt(); }
        catch (Exception ex) { return ex.Message; }
        var datei = Path.Combine(Folder3D, DevStudio.SafeName(_szene.Name) + ext);
        try
        {
            if (ext == ".blend" || (ext == ".fbx" && BlenderPath is not null))
            {
                if (BlenderPath is not { } bl)
                {
                    _blenderFragen = true;
                    _blenderDanachOeffnen = false;
                    return "Eine Blender-Datei braucht Blender. Richte es unten ein, danach geht der Export ohne Neustart.";
                }
                var glb = Path.ChangeExtension(datei, ".glb");
                MeshIo.WriteGlb(glb, mesh);
                if (ext == ".fbx")
                {
                    var (r, o) = Studio3D.Convert(bl, glb, ".fbx", datei);
                    if (!r.Ok) return "Blender konnte kein FBX schreiben. " + Kurz(r.Err.Length > 0 ? r.Err : r.Out);
                    datei = o;
                }
                else
                {
                    var (r, o) = Studio3D.Convert(bl, glb, ".blend", datei);
                    if (!r.Ok) return "Blender konnte die Datei nicht anlegen. " + Kurz(r.Err.Length > 0 ? r.Err : r.Out);
                    datei = o;
                }
            }
            else if (ext == ".fbx") MeshIo.WriteFbx(datei, mesh);
            else if (ext == ".obj") MeshIo.WriteObj(datei, mesh);
            else if (ext == ".stl") MeshIo.WriteStl(datei, mesh);
            else if (ext == ".glb") MeshIo.WriteGlb(datei, mesh);
            else return "Unbekanntes Format.";
        }
        catch (Exception ex) { return ex.Message; }
        _letzteDatei = datei;
        var extra = ext == ".stl" ? " STL ist in Millimetern (1 cm = 10 mm)." : ext == ".glb" ? " GLB ist in Metern, wie es für glTF üblich ist." : "";
        return $"Gespeichert: {Path.GetFileName(datei)} im 3D-Ordner.{extra}";
    }

    private string InBlender()
    {
        if (_szene.Teile.Count == 0) return "Es gibt noch kein Objekt. Füge zuerst eine Form hinzu.";
        if (BlenderPath is not { } bl)
        {
            _blenderFragen = true;
            _blenderDanachOeffnen = true;
            return "Blender wurde nicht gefunden. Wenn du zustimmst, richtet Trendo es ein und öffnet danach dieses Objekt.";
        }
        var msg = Export(".blend");
        if (_letzteDatei is null || !msg.StartsWith("Gespeichert", StringComparison.Ordinal)) return msg;
        _core.Integrations.LaunchDirect(Principal.LocalOwner, bl, _core.Audit, _letzteDatei);
        return $"Blender öffnet {Path.GetFileName(_letzteDatei)}.";
    }

    private async Task BlenderJa()
    {
        if (_core.Offline) { Say("Offline-Modus: Installation ist aus.", "3d"); return; }
        var comp = DependencyCatalog.Get("blender");
        var check = await _deps.CheckAsync(comp);
        if (check.Ok && check.Path is not null)
        {
            _blenderFragen = false;
            var weiter = _blenderDanachOeffnen;
            _blenderDanachOeffnen = false;
            Say(weiter ? InBlender() : "Blender ist schon da.", "3d");
            return;
        }
        try
        {
            var req = _core.Approvals.Request(Principal.LocalOwner, "deps.install", $"{comp.Name} für 3D-Modelle erstellen installieren", comp.Id);
            _core.Approvals.Approve(req.Id, Principal.LocalOwner);
            if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner);
            var ok = await InstallOne(comp, refresh: false);
            _blenderFragen = false;
            if (!ok || BlenderPath is null) { _blenderDanachOeffnen = false; Say("Blender konnte nicht eingerichtet werden. Du kannst es später erneut versuchen.", "3d"); return; }
            var weiter = _blenderDanachOeffnen;
            _blenderDanachOeffnen = false;
            Say(weiter ? InBlender() : "Blender ist eingerichtet. Du kannst das Objekt jetzt dort öffnen.", "3d");
        }
        catch (Exception ex) { _blenderFragen = false; Say(ex.Message, "3d"); }
    }

    private void DateiFallen(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).Where(p => !string.IsNullOrEmpty(p)).ToList() ?? [];
        if (files.Count == 0) { Say("Die abgelegte Datei hat keinen Pfad.", "3d"); return; }
        Say(string.Join(" ", files.Select(f => Import3D(f!, _importMm))), "3d");
        e.Handled = true;
    }

    private async Task DateiOeffnen(bool millimeters)
    {
        IReadOnlyList<string> files;
        try
        {
            if (Pick3DFiles is { } pick) files = await pick();
            else
            {
                var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "3D-Datei öffnen",
                    AllowMultiple = true,
                    FileTypeFilter = [new FilePickerFileType("3D-Dateien") { Patterns = ["*.obj", "*.stl", "*.glb", "*.gltf", "*.fbx", "*.blend"] }],
                });
                files = picked.Select(f => f.TryGetLocalPath()).Where(p => !string.IsNullOrEmpty(p)).Cast<string>().ToList();
            }
        }
        catch (Exception ex) { Say("Der Datei-Dialog geht hier nicht: " + ex.Message, "3d"); return; }
        if (files.Count == 0) { Say("Keine Datei ausgewählt.", "3d"); return; }
        var msg = string.Join(" ", files.Select(f => Import3D(f, millimeters)));
        Say(msg, "3d");
    }

    private async Task VorschlagHolen(string satz)
    {
        if (string.IsNullOrWhiteSpace(satz)) { Say("Schreib zuerst, was du bauen willst. Zum Beispiel: ein Tisch 120 × 80 cm mit vier Beinen.", "3d"); return; }
        var antwort = Ki3DOverride is { } over ? await over(Scene3DAi.Auftrag(satz)) : await _ollama.ChatAsync(KiModelName, Scene3DAi.Auftrag(satz));
        if (!Scene3DAi.TryLesen(antwort, out var teile, out var fehler)) { _vorschlag = null; Say(fehler, "3d"); return; }
        _vorschlag = teile;
        Say($"{teile.Count} Teile vorgeschlagen. Du kannst die Zahlen ändern und dann bauen.", "3d");
    }

    private Control Studio3DView()
    {
        var root = new StackPanel { Spacing = 8 };
        root.Children.Add(P("Baue ein Objekt aus Formen, oder lass es die lokale KI vorschlagen. Die Vorschau entsteht hier. Blender brauchst du nur, wenn du dort weiterarbeiten willst."));
        root.Children.Add(BlenderLeiste());
        DragDrop.SetAllowDrop(root, true);
        root.AddHandler(DragDrop.DropEvent, DateiFallen);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("340,16,*") };
        grid.Children.Add(TeileKarte());
        var rechts = VorschauKarte();
        Grid.SetColumn(rechts, 2);
        grid.Children.Add(rechts);
        root.Children.Add(grid);

        if (_szene.Gewaehlt is { } teil) root.Children.Add(Eigenschaften(teil));
        root.Children.Add(Werkzeuge());
        root.Children.Add(Beschreibung());
        root.Children.Add(ImportKarte());
        root.Children.Add(ExportKarte());
        root.Children.Add(ProjektKarte());
        return KreativPage("3D-Studio", root);
    }

    private Control BlenderLeiste()
    {
        if (_blenderFragen)
        {
            var comp = DependencyCatalog.Get("blender");
            var ja = Act("3d-blender-yes", "Ja, installieren", BlenderJa, true);
            if (_core.Offline) { ja.IsEnabled = false; ToolTip.SetTip(ja, "Offline-Modus: Installation ist aus."); }
            var nein = Act("3d-blender-no", "Später", () => { _blenderFragen = false; _blenderDanachOeffnen = false; Say("Blender bleibt ungefragt. Dein Objekt bleibt hier.", "3d"); });
            return Card(new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Name = "3d-blender-ask", Text = "Zum Öffnen in Blender fehlt Blender. Jetzt installieren? Trendo fragt nur dieses eine Mal und prüft vorher, ob es schon da ist.", Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                    P($"Lizenz: {comp.License}. {comp.Reason}"),
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { ja, nein } },
                },
            });
        }
        if (BlenderPath is null)
            return Card(new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    P("Blender ist nicht installiert. Bauen, Ansehen und Speichern als OBJ, STL und GLB gehen auch so."),
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal, Spacing = 8,
                        Children =
                        {
                            Act("3d-open-blender", "In Blender öffnen", () => Schritt(InBlender), true),
                            Act("3d-blender-setup", "Blender einrichten", () => { _blenderFragen = true; _blenderDanachOeffnen = false; Say("Blender wird nur installiert, wenn du zustimmst.", "3d"); }),
                        },
                    },
                },
            });
        return Card(Row(P("Blender ist da. Der Knopf öffnet genau dieses Objekt, nicht eine leere Datei."),
            Act("3d-open-blender", "In Blender öffnen", () => Schritt(InBlender), true)));
    }

    private Control TeileKarte()
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H("Teile", 14));
        var name = new TextBox { Name = "3d-name", Text = _szene.Name, PlaceholderText = "Name des Objekts", Width = 200 };
        var form = new ComboBox { Name = "3d-shape", ItemsSource = Scene3D.Formen.Select(f => f.Label).ToList(), SelectedIndex = 0, Width = 140 };
        var add = Act("3d-add", "Hinzufügen", () =>
        {
            if (!string.IsNullOrWhiteSpace(name.Text)) _szene.Name = name.Text.Trim();
            var id = Scene3D.Formen[Math.Clamp(form.SelectedIndex, 0, Scene3D.Formen.Length - 1)].Id;
            Schritt(() => _szene.Hinzu(id));
        }, true);
        sp.Children.Add(new WrapPanel { Orientation = Orientation.Horizontal, Children = { name, form, add } });
        if (_szene.Teile.Count == 0) sp.Children.Add(new TextBlock { Name = "3d-empty", Text = "Noch kein Teil. Wähle eine Form und klicke auf Hinzufügen.", Foreground = Muted, TextWrapping = TextWrapping.Wrap });
        for (var i = 0; i < _szene.Teile.Count; i++)
        {
            var index = i;
            var t = _szene.Teile[i];
            var mark = i == _szene.Auswahl ? "● " : "";
            var wahl = Act($"3d-part-{i}", mark + t.Name, () => { _szene.Auswahl = index; Say($"„{t.Name}“ ausgewählt.", "3d"); });
            if (i == _szene.Auswahl) wahl.Background = Accent;
            var del = Act($"3d-del-{i}", "Löschen", () => Schritt(() => _szene.Entfernen(index)));
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { wahl, del } });
        }
        sp.Children.Add(Act("3d-undo", "Rückgängig", () => Schritt(() => _szene.Rueckgaengig())));
        sp.Children.Add(new TextBlock { Name = "3d-info", Text = _szene.Info(), Foreground = Text, TextWrapping = TextWrapping.Wrap });
        return Card(sp);
    }

    private Control VorschauKarte()
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H("Vorschau", 14));
        var ansichten = new (int Id, string Name, string Label)[] { (0, "front", "Vorne"), (1, "back", "Hinten"), (2, "left", "Links"), (3, "right", "Rechts"), (4, "top", "Oben"), (5, "persp", "Schräg") };
        var row = new WrapPanel();
        foreach (var (id, n, label) in ansichten)
        {
            var view = id;
            var b = Act("3d-view-" + n, label, () => { _ansicht = view; Say(label + " wird angezeigt.", "3d"); });
            if (_ansicht == id) b.Background = Accent;
            row.Children.Add(b);
        }
        sp.Children.Add(row);
        sp.Children.Add(Act("3d-refresh", "Vorschau neu", () => { VorschauAktualisieren(); Say(_szene.Teile.Count == 0 ? "Noch kein Teil für die Vorschau." : "Vorschau aktualisiert.", "3d"); }));
        if (_szene.Teile.Count == 0) sp.Children.Add(P("Die Vorschau erscheint, sobald ein Teil da ist."));
        else
        {
            try
            {
                var png = MeshIo.RenderPng(_szene.Welt(), _ansicht);
                var img = new Image { Name = "3d-preview-image", Source = new Bitmap(new MemoryStream(png)), MaxWidth = 480, MaxHeight = 360, HorizontalAlignment = HorizontalAlignment.Left };
                sp.Children.Add(img);
            }
            catch (Exception ex) { sp.Children.Add(P("Vorschau fehlgeschlagen: " + ex.Message, Brush.Parse("#FACC15"))); }
        }
        var drop = new Border
        {
            Name = "3d-drop",
            BorderBrush = Edge,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(8),
            Child = P("Du kannst eine Datei auch hierher ziehen: OBJ, STL, GLB, GLTF, FBX oder BLEND."),
        };
        DragDrop.SetAllowDrop(drop, true);
        drop.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = DragDropEffects.Copy; e.Handled = true; });
        drop.AddHandler(DragDrop.DropEvent, DateiFallen);
        sp.Children.Add(drop);
        return Card(sp);
    }

    private Control Eigenschaften(ScenePart t)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H("Ausgewähltes Teil", 14));
        var pname = new TextBox { Name = "3d-part-name", Text = t.Name, Width = 180 };
        sp.Children.Add(Zeile(P("Name"), pname));
        var sx = ZahlFeld("3d-sx", t.Bx); var sy = ZahlFeld("3d-sy", t.By); var sz = ZahlFeld("3d-sz", t.Bz);
        var px = ZahlFeld("3d-px", t.X); var py = ZahlFeld("3d-py", t.Y); var pz = ZahlFeld("3d-pz", t.Z);
        var rx = ZahlFeld("3d-rx", t.Rx); var ry = ZahlFeld("3d-ry", t.Ry); var rz = ZahlFeld("3d-rz", t.Rz);
        var farben = Scene3D.Farben.Select(f => f.Label).ToList();
        var ci = Array.FindIndex(Scene3D.Farben, f => f.Hex.Equals(t.Farbe, StringComparison.OrdinalIgnoreCase));
        var color = new ComboBox { Name = "3d-color-name", ItemsSource = farben, SelectedIndex = Math.Max(0, ci), Width = 110 };
        var hex = new TextBox { Name = "3d-color", Text = t.Farbe, Width = 90 };
        var text = new TextBox { Name = "3d-text", Text = t.Text, PlaceholderText = "Schriftzug", Width = 160, IsVisible = t.Form == "text" };
        sp.Children.Add(P("Größe in Zentimetern (Breite, Tiefe, Höhe), dann Position und Drehung in Grad."));
        sp.Children.Add(Zeile(P("Größe"), sx, sy, sz));
        sp.Children.Add(Zeile(P("Position"), px, py, pz));
        sp.Children.Add(Zeile(P("Drehung"), rx, ry, rz));
        sp.Children.Add(Zeile(P("Farbe"), color, hex, text));
        if (t.Notiz.Count > 0) sp.Children.Add(P(string.Join(" · ", t.Notiz)));
        sp.Children.Add(Act("3d-apply", "Übernehmen", () =>
        {
            if (!Zahl(sx.Text, out var bx) || !Zahl(sy.Text, out var by) || !Zahl(sz.Text, out var bz)
                || !Zahl(px.Text, out var x) || !Zahl(py.Text, out var y) || !Zahl(pz.Text, out var z)
                || !Zahl(rx.Text, out var drx) || !Zahl(ry.Text, out var dry) || !Zahl(rz.Text, out var drz))
            { Say("Bitte nur Zahlen eintragen. Komma und Punkt gehen beide.", "3d"); return; }
            var col = (hex.Text ?? "").Trim();
            if (!col.StartsWith('#')) col = Scene3D.Farben[Math.Clamp(color.SelectedIndex, 0, Scene3D.Farben.Length - 1)].Hex;
            var idx = _szene.Teile.IndexOf(t);
            Schritt(() => _szene.Setzen(idx, pname.Text ?? t.Name, bx, by, bz, x, y, z, drx, dry, drz, col, text.Text));
        }, true));
        return Card(sp);
    }

    private Control Werkzeuge()
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H("Werkzeuge", 14));
        sp.Children.Add(P("Wirken auf das ausgewählte Teil. Rückgängig holt den vorigen Stand."));
        var bevel = ZahlFeld("3d-bevel", 0.4);
        var solid = ZahlFeld("3d-solid", 0.4);
        var hole = ZahlFeld("3d-hole", 1.5);
        var achse = new ComboBox { Name = "3d-hole-axis", ItemsSource = new[] { "Z", "X", "Y" }, SelectedIndex = 0, Width = 70 };
        var spiegel = new ComboBox { Name = "3d-mirror-axis", ItemsSource = new[] { "X", "Y", "Z" }, SelectedIndex = 0, Width = 70 };
        int Idx() => _szene.Auswahl;
        sp.Children.Add(Zeile(P("Kanten abrunden (cm)"), bevel, Act("3d-tool-bevel", "Abrunden", () =>
        {
            if (!Zahl(bevel.Text, out var w)) { Say("Die Abrundung braucht eine Zahl.", "3d"); return; }
            Schritt(() => _szene.Werkzeug(Idx(), "bevel", w, ""));
        })));
        sp.Children.Add(Zeile(Act("3d-tool-smooth", "Glätten", () => Schritt(() => _szene.Werkzeug(Idx(), "smooth", 1, "")))));
        sp.Children.Add(Zeile(spiegel, Act("3d-tool-mirror", "Spiegeln", () => Schritt(() => _szene.Werkzeug(Idx(), "spiegel", 0, (spiegel.SelectedItem as string) ?? "X")))));
        sp.Children.Add(Zeile(P("Aushöhlen, Wand (cm)"), solid, Act("3d-tool-solid", "Wand geben", () =>
        {
            if (!Zahl(solid.Text, out var w)) { Say("Die Wandstärke braucht eine Zahl.", "3d"); return; }
            Schritt(() => _szene.Werkzeug(Idx(), "solid", w, ""));
        })));
        sp.Children.Add(Zeile(P("Loch bohren, Radius (cm)"), hole, achse, Act("3d-tool-hole", "Loch bohren", () =>
        {
            if (!Zahl(hole.Text, out var r)) { Say("Der Radius braucht eine Zahl.", "3d"); return; }
            Schritt(() => _szene.Werkzeug(Idx(), "hole", r, ((achse.SelectedItem as string) ?? "Z").ToLowerInvariant()));
        })));
        return Card(sp);
    }

    private Control Beschreibung()
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H("In Worten beschreiben", 14));
        var bereit = Ki3DForceReady || (_kiConnected && !_opt.Isolated);
        var grund = _opt.Offline
            ? "Offline-Modus: die lokale KI ist aus."
            : "Lokale KI ist nicht verbunden. Unter Zusatzprogramme kannst du Ollama einrichten. Ohne KI bleibt dieser Knopf aus.";
        var satz = new TextBox { Name = "3d-prompt", PlaceholderText = "zum Beispiel: ein Tisch 120 × 80 cm mit vier Beinen", MinWidth = 280, MaxWidth = 520 };
        var knopf = Act("3d-ai", "Vorschlag holen", async () => await VorschlagHolen(satz.Text ?? ""), true);
        knopf.IsEnabled = bereit;
        if (!bereit)
        {
            ToolTip.SetTip(knopf, grund);
            sp.Children.Add(new TextBlock { Name = "3d-ai-reason", Text = grund, Foreground = Brush.Parse("#FACC15"), TextWrapping = TextWrapping.Wrap });
        }
        else sp.Children.Add(new TextBlock { Name = "3d-ai-reason", Text = "Die lokale KI schlägt die Teile vor. Du siehst sie, bevor etwas gebaut wird.", Foreground = Muted, TextWrapping = TextWrapping.Wrap });
        var pruefen = Act("3d-ai-check", "KI prüfen", async () =>
        {
            await RefreshKi();
            Say(_kiConnected || Ki3DForceReady ? "Die lokale KI ist verbunden." : "Die lokale KI ist nicht verbunden.", "3d");
        });
        sp.Children.Add(new WrapPanel { Children = { satz, knopf, pruefen } });
        if (_vorschlag is { Count: > 0 } v)
        {
            sp.Children.Add(new TextBlock { Name = "3d-proposal", Text = "Vorschlag – Zahlen kannst du noch ändern:", Foreground = Text, FontWeight = FontWeight.SemiBold });
            var felder = new List<(TextBox Name, TextBox Bx, TextBox By, TextBox Bz, TextBox X, TextBox Y, TextBox Z, ScenePart Teil)>();
            for (var i = 0; i < v.Count; i++)
            {
                var t = v[i];
                var tn = new TextBox { Name = $"3d-prop-name-{i}", Text = t.Name, Width = 110 };
                var bx = ZahlFeld($"3d-prop-bx-{i}", t.Bx, 64);
                var by = ZahlFeld($"3d-prop-by-{i}", t.By, 64);
                var bz = ZahlFeld($"3d-prop-bz-{i}", t.Bz, 64);
                var x = ZahlFeld($"3d-prop-x-{i}", t.X, 64);
                var y = ZahlFeld($"3d-prop-y-{i}", t.Y, 64);
                var z = ZahlFeld($"3d-prop-z-{i}", t.Z, 64);
                felder.Add((tn, bx, by, bz, x, y, z, t));
                sp.Children.Add(new WrapPanel
                {
                    Children =
                    {
                        P(Scene3D.Formen.First(f => f.Id == t.Form).Label), tn,
                        P("B"), bx, P("T"), by, P("H"), bz, P("Lage"), x, y, z,
                    },
                });
            }
            sp.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8,
                Children =
                {
                    Act("3d-ai-apply", "Vorschlag bauen", () =>
                    {
                        foreach (var f in felder)
                        {
                            if (!Zahl(f.Bx.Text, out var bx) || !Zahl(f.By.Text, out var by) || !Zahl(f.Bz.Text, out var bz)
                                || !Zahl(f.X.Text, out var x) || !Zahl(f.Y.Text, out var y) || !Zahl(f.Z.Text, out var z))
                            { Say("Im Vorschlag steht eine Zahl, die nicht lesbar ist.", "3d"); return; }
                            f.Teil.Name = string.IsNullOrWhiteSpace(f.Name.Text) ? f.Teil.Name : f.Name.Text.Trim();
                            f.Teil.Bx = bx; f.Teil.By = by; f.Teil.Bz = bz; f.Teil.X = x; f.Teil.Y = y; f.Teil.Z = z;
                        }
                        _szene.Ersetzen(felder.Select(f => f.Teil));
                        _vorschlag = null;
                        Say("Objekt aus dem Vorschlag gebaut. Teile kannst du weiter ändern. Rückgängig holt den vorigen Stand.", "3d");
                    }, true),
                    Act("3d-ai-drop", "Verwerfen", () => { _vorschlag = null; Say("Vorschlag verworfen. Es wurde nichts gebaut.", "3d"); }),
                },
            });
        }
        return Card(sp);
    }

    private Control ImportKarte()
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H("Datei öffnen", 14));
        var einheit = new ComboBox { Name = "3d-unit", ItemsSource = new[] { "Millimeter", "Zentimeter" }, SelectedIndex = _importMm ? 0 : 1, Width = 140 };
        einheit.SelectionChanged += (_, _) => _importMm = einheit.SelectedIndex == 0;
        sp.Children.Add(P("OBJ, STL, GLB, GLTF, FBX und BLEND. STL vom Drucker ist meist Millimeter. Was Trendo selbst speichert, ist bei OBJ in Zentimetern. GLB rechnet immer in Metern."));
        sp.Children.Add(Zeile(P("Maße in der Datei"), einheit, Act("3d-open", "Datei öffnen …", async () => await DateiOeffnen(einheit.SelectedIndex == 0), true)));
        return Card(sp);
    }

    private Control ExportKarte()
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H("Speichern", 14));
        sp.Children.Add(P("Die Datei landet im 3D-Ordner. STL ist für den Druck und daher in Millimetern."));
        sp.Children.Add(new WrapPanel
        {
            Children =
            {
                Act("3d-export-obj", "OBJ", () => Schritt(() => Export(".obj"))),
                Act("3d-export-stl", "STL", () => Schritt(() => Export(".stl"))),
                Act("3d-export-glb", "GLB", () => Schritt(() => Export(".glb"))),
                Act("3d-export-fbx", "FBX", () => Schritt(() => Export(".fbx"))),
                Act("3d-export-blend", "BLEND", () => Schritt(() => Export(".blend"))),
                Act("3d-folder", "Ordner zeigen", () => { Launch(Folder3D); Say(_letzteDatei is { } f ? $"Ordner geöffnet. Zuletzt: {Path.GetFileName(f)}" : "Ordner geöffnet.", "3d"); }),
            },
        });
        var dateien = Directory.Exists(Folder3D)
            ? Directory.GetFiles(Folder3D).Where(f => MeshIo.Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()) || f.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase)).OrderByDescending(File.GetLastWriteTime).Take(8).ToList()
            : [];
        if (dateien.Count == 0) sp.Children.Add(P("Noch nichts gespeichert."));
        foreach (var f in dateien)
        {
            var mark = _letzteDatei is not null && string.Equals(f, _letzteDatei, StringComparison.OrdinalIgnoreCase);
            sp.Children.Add(new TextBlock
            {
                Name = mark ? "3d-marked" : null,
                Text = (mark ? "● zuletzt gespeichert: " : "") + Path.GetFileName(f),
                Foreground = mark ? Text : Muted,
                FontWeight = mark ? FontWeight.SemiBold : FontWeight.Normal,
            });
        }
        return Card(sp);
    }

    private Control ProjektKarte()
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H("Im Projekt merken", 14));
        sp.Children.Add(P("Speichert das Objekt im Projektregister. Dort kannst du es später wieder öffnen."));
        var liste = _core.Projects.List().Select(p => p.Id + "  " + p.Name).ToList();
        var combo = new ComboBox { Name = "3d-project", ItemsSource = liste, Width = 240, SelectedIndex = liste.FindIndex(s => s.StartsWith((_szene.ProjektId ?? "") + "  ", StringComparison.Ordinal)) };
        var neu = new TextBox { Name = "3d-project-id", PlaceholderText = "neue Kennung, z. B. tisch", Width = 180 };
        if (liste.Count == 0) sp.Children.Add(P("Noch kein Projekt. Trag eine Kennung ein."));
        string? IdAusCombo()
        {
            if (combo.SelectedItem is not string s) return null;
            var id = s.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }
        sp.Children.Add(new WrapPanel { Children = { combo, neu } });
        sp.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children =
            {
                Act("3d-project-save", "Im Projekt speichern", () => Schritt(() => ProjektSpeichern(neu.Text, IdAusCombo())), true),
                Act("3d-project-load", "Aus Projekt öffnen", () =>
                {
                    var id = IdAusCombo();
                    if (id is null) { Say("Wähle zuerst ein Projekt in der Liste.", "3d"); return; }
                    OpenSaved3D(id);
                }),
            },
        });
        return Card(sp);
    }

    private string ProjektSpeichern(string? neu, string? gewaehlt)
    {
        if (_szene.Teile.Count == 0) return "Es gibt noch kein Objekt zum Speichern.";
        var typed = (neu ?? "").Trim().ToLowerInvariant().Replace('_', '-');
        string id;
        if (typed.Length > 0) id = typed;
        else if (gewaehlt is not null) id = gewaehlt;
        else return "Wähle ein Projekt oder trag eine Kennung ein, zum Beispiel tisch.";
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{1,47}$"))
            return "Die Kennung darf nur Kleinbuchstaben, Zahlen und Bindestrich haben, 2 bis 48 Zeichen.";
        var bekannt = _core.Projects.List().Any(p => p.Id == id);
        if (!bekannt) _core.Projects.Create(id, _szene.Name);
        _szene.ProjektId = id;
        _core.Projects.WriteFile(id, "3d/szene.json", _szene.ToJson());
        _core.Projects.WriteFile(id, "3d/modell.obj", MeshIo.ObjText(_szene.Welt()));
        return bekannt ? $"Im Projekt „{id}“ gespeichert." : $"Projekt „{id}“ angelegt und das Modell darin gespeichert.";
    }

    private static StackPanel Zeile(params Control[] c)
    {
        var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var x in c) s.Children.Add(x);
        return s;
    }

    private static string Kurz(string s)
    {
        s = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return s.Length > 180 ? s[..180] + "…" : s;
    }
}
