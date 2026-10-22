using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-05: 3D-Objekte aus Teilen bauen, ansehen und in Blender öffnen.</summary>
public partial class MainWindow
{
    public string Folder3D => WorkFolder("3D");
    public string? BlenderPath => ToolPath("blender") is { } p && File.Exists(p) ? p : (_opt.Isolated ? null : Cli.Find("blender"));
    public Func<string, Task<string>>? SceneAi { get; set; }
    public byte[]? CurrentPreview => _png3d;
    public Func<Task<string?>>? Pick3DFile { get; set; }

    private Scene3D _scene = new();
    private string? _selId;
    private string _view3d = "front";
    private readonly List<string> _undo3d = [];
    private List<Part3D>? _aiParts;
    private byte[]? _png3d;
    private string _pngKey = "";
    private string? _previewError;
    private string? _last3dFile;
    private bool _ollamaChecked, _ollamaUp;

    private void InitW4Studio3D() => RegisterTab(KreativTabs, "3d", "3D", "CORE-05", Studio3DView, "blender", "3d", "modell", "stl", "gltf", "rendern");

    private Part3D? Selected => _selId is null ? null : _scene.Parts.FirstOrDefault(p => p.Id == _selId);

    private Control Studio3DView()
    {
        if (!_ollamaChecked && SceneAi is null && _deps.OllamaUp is { } probe)
        {
            _ollamaChecked = true;
            _ = Task.Run(async () =>
            {
                var up = false;
                try { up = await probe(); } catch { up = false; }
                if (!up) return;
                Dispatcher.UIThread.Post(() => { _ollamaUp = true; if (CurrentView == "3d") Show("3d"); });
            });
        }
        var aiOn = SceneAi is not null || _ollamaUp;
        try { _png3d = PreviewBytes(); _previewError = null; }
        catch (Exception ex) { _png3d = null; _previewError = "Vorschau nicht möglich: " + ex.Message; }

        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(P("Baue ein Objekt aus Teilen, sieh es hier an und öffne genau dieses Objekt in Blender. Maße sind Zentimeter, Z zeigt nach oben."));
        sp.Children.Add(Card(BlenderRow()));

        var objName = new TextBox { Name = "3d-name", Text = _scene.Name, Width = 200 };
        sp.Children.Add(Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                H("Objekt", 14),
                new WrapPanel { Children = { Labeled("Name", objName), Act("3d-save", "Speichern", () => SaveScene(objName)), Act("3d-new", "Neues Objekt", () => NewScene(objName)), Act("3d-undo", "Rückgängig", UndoScene) } },
                H("Teile", 14),
                PartsList(objName),
                H("Teil", 14),
                PartForm(objName),
            },
        }));
        sp.Children.Add(Card(AiBlock(aiOn, objName)));
        sp.Children.Add(Card(PreviewBlock()));
        sp.Children.Add(Card(FileBlock(objName)));
        sp.Children.Add(Card(SavedBlock()));
        return KreativPage("3D-Modelle erstellen", sp);
    }

    private Control BlenderRow()
    {
        if (BlenderPath is null)
            return new WrapPanel
            {
                Children =
                {
                    P("Blender ist nicht installiert. Bauen, Ansehen und der Export als OBJ, STL und GLB gehen trotzdem.", Brush.Parse("#FACC15")),
                    Act("3d-setup-blender", "Blender einrichten", () => _ = SetupBlender()),
                    Act("3d-open-blender", "In Blender öffnen", () => _ = AskBlender()),
                    Act("3d-folder", "Ordner zeigen", ShowFolder),
                },
            };
        return new WrapPanel
        {
            Children =
            {
                P("Blender ist bereit."),
                Act("3d-open-blender", "In Blender öffnen", () => _ = AskBlender(), true),
                Act("3d-folder", "Ordner zeigen", ShowFolder),
            },
        };
    }

    private Control PartsList(TextBox objName)
    {
        if (_scene.Parts.Count == 0) return P("Noch keine Teile. Unten einen Würfel, eine Kugel oder eine andere Form hinzufügen.");
        var list = new StackPanel { Spacing = 4 };
        for (var i = 0; i < _scene.Parts.Count; i++)
        {
            var part = _scene.Parts[i]; var id = part.Id; var n = i;
            var mark = part.Id == _selId ? "● " : "";
            list.Children.Add(new WrapPanel
            {
                Children =
                {
                    Act($"3d-sel-{n}", mark + part.Line(), () => { Keep(objName); _selId = id; Show("3d"); }),
                    Act($"3d-del-{n}", "Entfernen", () => DeletePart(objName, id)),
                },
            });
        }
        return list;
    }

    private Control PartForm(TextBox objName)
    {
        var sel = Selected;
        var labels = Scene3D.PartShapes.Select(s => s.Label).ToList();
        if (sel?.Shape == "mesh") labels.Add("Datei");
        var shapeIndex = 0;
        if (sel is not null)
        {
            var found = Array.FindIndex(Scene3D.PartShapes, s => s.Id == sel.Shape);
            shapeIndex = found >= 0 ? found : labels.Count - 1;
        }
        var shape = new ComboBox { Name = "3d-shape", ItemsSource = labels, SelectedIndex = shapeIndex, Width = 130 };
        var partName = new TextBox { Name = "3d-part-name", Text = sel?.Name ?? "Teil", Width = 140 };
        var colors = Scene3D.Colors.Select(c => c.Name).ToList();
        var colorIndex = 0;
        if (sel is not null)
        {
            var cf = Array.FindIndex(Scene3D.Colors, c => string.Equals(c.Hex, sel.Color, StringComparison.OrdinalIgnoreCase));
            colorIndex = cf >= 0 ? cf : 0;
        }
        var color = new ComboBox { Name = "3d-color", ItemsSource = colors, SelectedIndex = colorIndex, Width = 120 };
        var sx = NumBox("3d-sx", sel?.Sx ?? 10); var sy = NumBox("3d-sy", sel?.Sy ?? 10); var sz = NumBox("3d-sz", sel?.Sz ?? 10);
        var px = NumBox("3d-px", sel?.X ?? 0); var py = NumBox("3d-py", sel?.Y ?? 0); var pz = NumBox("3d-pz", sel?.Z ?? 0);
        var rx = NumBox("3d-rx", sel?.Rx ?? 0); var ry = NumBox("3d-ry", sel?.Ry ?? 0); var rz = NumBox("3d-rz", sel?.Rz ?? 0);
        var text = new TextBox { Name = "3d-text", Text = sel?.Text ?? "Trendo", Width = 180, IsVisible = shapeIndex == 6 };
        shape.SelectionChanged += (_, _) => text.IsVisible = shape.SelectedIndex == 6;
        var bevel = NumBox("3d-bevel-amt", 0.3);
        var dia = NumBox("3d-hole-dia", 3);
        var axis = new ComboBox { Name = "3d-hole-axis", ItemsSource = new[] { "X", "Y", "Z" }, SelectedIndex = 2, Width = 70 };
        var mirror = new ComboBox { Name = "3d-mirror-axis", ItemsSource = new[] { "X", "Y", "Z" }, SelectedIndex = 0, Width = 70 };
        var shell = NumBox("3d-shell-amt", 0.4);
        var form = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                P("Torus: Breite ist der Außendurchmesser, Tiefe die Ringdicke."),
                new WrapPanel { Children = { Labeled("Form", shape), Labeled("Name", partName), Labeled("Farbe", color) } },
                new WrapPanel { Children = { Labeled("Breite", sx), Labeled("Tiefe", sy), Labeled("Höhe", sz) } },
                new WrapPanel { Children = { Labeled("Position X", px), Labeled("Y", py), Labeled("Z", pz) } },
                new WrapPanel { Children = { Labeled("Drehung X", rx), Labeled("Y", ry), Labeled("Z", rz) } },
                Labeled("Schrift", text),
                new WrapPanel { Children = { Act("3d-add", "Hinzufügen", () => AddPart(objName, shape, partName, color, text, sx, sy, sz, px, py, pz, rx, ry, rz), true), Act("3d-apply", "Übernehmen", () => ApplyPart(objName, shape, partName, color, text, sx, sy, sz, px, py, pz, rx, ry, rz)) } },
                H("Werkzeug", 14),
                new WrapPanel { Children = { Labeled("Abrunden cm", bevel), Act("3d-bevel", "Kanten abrunden", () => Bevel(objName, bevel)) } },
                new WrapPanel { Children = { Labeled("Loch Ø cm", dia), Labeled("Achse", axis), Act("3d-hole", "Loch bohren", () => Hole(objName, dia, axis)) } },
                new WrapPanel { Children = { Act("3d-smooth", "Glätten", () => Smooth(objName)), Labeled("Spiegel", mirror), Act("3d-mirror", "Spiegeln", () => Mirror(objName, mirror)), Labeled("Wand cm", shell), Act("3d-shell", "Aushöhlen", () => Shell(objName, shell)) } },
            },
        };
        return form;
    }

    private Control AiBlock(bool aiOn, TextBox objName)
    {
        var sentence = new TextBox { Name = "3d-ai-text", Width = 420, Text = "" };
        var button = Act("3d-ai", "Teile vorschlagen", () => _ = Propose(objName, sentence));
        button.IsEnabled = aiOn;
        var box = new StackPanel { Spacing = 6, Children = { H("In einem Satz beschreiben", 14), sentence, button } };
        if (!aiOn)
            box.Children.Add(new TextBlock { Name = "3d-ai-reason", Text = "Die lokale KI (Ollama) ist nicht verbunden. Deshalb ist „Teile vorschlagen“ aus.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (_aiParts is { Count: > 0 })
        {
            box.Children.Add(P("Vorschlag, noch nicht gebaut:", Text));
            foreach (var p in _aiParts) box.Children.Add(P(p.Line(), Text));
            box.Children.Add(new WrapPanel { Children = { Act("3d-ai-use", "Übernehmen", () => UseAi(objName), true), Act("3d-ai-drop", "Verwerfen", () => { _aiParts = null; Say("Vorschlag verworfen.", "3d"); }) } });
        }
        return box;
    }

    private Control PreviewBlock()
    {
        var box = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                H("Vorschau", 14),
                new WrapPanel
                {
                    Children =
                    {
                        Act("3d-view-front", "Vorne", () => SetView("front", "Ansicht von vorn.")),
                        Act("3d-view-right", "Rechts", () => SetView("right", "Ansicht von rechts.")),
                        Act("3d-view-top", "Oben", () => SetView("top", "Ansicht von oben.")),
                        Act("3d-view-angle", "Schräg", () => SetView("angle", "Schräge Ansicht.")),
                    },
                },
            },
        };
        if (_previewError is { Length: > 0 }) box.Children.Add(P(_previewError, Brush.Parse("#FACC15")));
        if (_png3d is { Length: > 0 })
            box.Children.Add(new Image { Name = "3d-preview-image", Source = new Bitmap(new MemoryStream(_png3d)), Width = 360, Height = 270, HorizontalAlignment = HorizontalAlignment.Left });
        else if (_previewError is null) box.Children.Add(P("Die Vorschau erscheint, sobald ein Teil da ist."));
        return box;
    }

    private Control FileBlock(TextBox objName)
    {
        var drop = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                H("Datei", 14),
                new WrapPanel
                {
                    Children =
                    {
                        Act("3d-open-file", "Datei öffnen …", () => _ = Browse(objName)),
                        Act("3d-export-obj", "Als OBJ", () => ExportNative(objName, ".obj")),
                        Act("3d-export-stl", "Als STL", () => ExportNative(objName, ".stl")),
                        Act("3d-export-glb", "Als GLB", () => ExportNative(objName, ".glb")),
                        Act("3d-export-blend", "Als Blender-Datei", () => _ = ExportWithBlender(objName, ".blend")),
                        Act("3d-export-fbx", "Als FBX", () => _ = ExportWithBlender(objName, ".fbx")),
                    },
                },
                P("OBJ und die Vorschau sind in Zentimetern, STL in Millimetern, GLB in Metern. Eine Datei kannst du auch hierher ziehen."),
            },
        };
        var host = new Border { Name = "3d-drop", Child = drop, Background = Brushes.Transparent, MinHeight = 40 };
        DragDrop.SetAllowDrop(host, true);
        host.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = DragDropEffects.Copy; e.Handled = true; });
        host.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            Keep(objName);
            var files = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).Where(p => !string.IsNullOrEmpty(p)).ToList() ?? [];
            if (files.Count == 0) { Say("Diese Angabe ist keine Datei.", "3d"); return; }
            foreach (var f in files) ImportPath(f!);
            e.Handled = true;
        });
        return host;
    }

    private Control SavedBlock()
    {
        var box = new StackPanel { Spacing = 4, Children = { H("Gespeicherte Objekte", 14) } };
        var any = false;
        foreach (var p in _core.Projects.List())
        {
            var file = Path.Combine(_core.Projects.ProjectFilesRoot(p.Id), "3d", "szene.json");
            if (!File.Exists(file)) continue;
            any = true;
            var id = p.Id;
            box.Children.Add(new WrapPanel { Children = { P(p.Name, Text), Act("3d-proj-" + id, "Öffnen", () => Open3DProject(id)) } });
        }
        if (!any) box.Children.Add(P("Noch nichts gespeichert. „Speichern“ legt das Objekt im Projektregister ab."));
        return box;
    }

    private void AddPart(TextBox objName, ComboBox shape, TextBox partName, ComboBox color, TextBox text, TextBox sx, TextBox sy, TextBox sz, TextBox px, TextBox py, TextBox pz, TextBox rx, TextBox ry, TextBox rz)
    {
        Keep(objName);
        if (!TryRead(shape, partName, color, text, sx, sy, sz, px, py, pz, rx, ry, rz, out var draft, out var err)) { Say(err, "3d"); return; }
        if (draft.Shape == "mesh") { Say("Eine Datei fügst du über „Datei öffnen …“ hinzu.", "3d"); return; }
        Push();
        _scene.Parts.Add(draft);
        _selId = draft.Id;
        Say($"{draft.Name} ist dabei.", "3d");
    }

    private void ApplyPart(TextBox objName, ComboBox shape, TextBox partName, ComboBox color, TextBox text, TextBox sx, TextBox sy, TextBox sz, TextBox px, TextBox py, TextBox pz, TextBox rx, TextBox ry, TextBox rz)
    {
        Keep(objName);
        var p = Selected;
        if (p is null) { Say("Wähle zuerst ein Teil in der Liste.", "3d"); return; }
        if (!TryRead(shape, partName, color, text, sx, sy, sz, px, py, pz, rx, ry, rz, out var draft, out var err)) { Say(err, "3d"); return; }
        Push();
        p.Name = draft.Name; p.Sx = draft.Sx; p.Sy = draft.Sy; p.Sz = draft.Sz;
        p.X = draft.X; p.Y = draft.Y; p.Z = draft.Z; p.Rx = draft.Rx; p.Ry = draft.Ry; p.Rz = draft.Rz;
        p.Color = draft.Color; p.Text = draft.Text;
        if (draft.Shape != "mesh") { p.Shape = draft.Shape; p.Positions = null; p.Indices = null; }
        Say($"{p.Name} geändert.", "3d");
    }

    private bool TryRead(ComboBox shape, TextBox partName, ComboBox color, TextBox text, TextBox sx, TextBox sy, TextBox sz, TextBox px, TextBox py, TextBox pz, TextBox rx, TextBox ry, TextBox rz, out Part3D part, out string error)
    {
        part = new Part3D(); error = "";
        var w = Read(sx.Text); var d = Read(sy.Text); var h = Read(sz.Text);
        if (double.IsNaN(w) || double.IsNaN(d) || double.IsNaN(h) || w <= 0 || d <= 0 || h <= 0) { error = "Breite, Tiefe und Höhe müssen Zahlen größer als 0 sein."; return false; }
        var id = ShapeId(shape.SelectedIndex);
        part.Shape = id;
        part.Name = string.IsNullOrWhiteSpace(partName.Text) ? Scene3D.ShapeLabel(id) : partName.Text.Trim();
        part.Sx = w; part.Sy = d; part.Sz = h;
        part.X = Or0(px.Text); part.Y = Or0(py.Text); part.Z = Or0(pz.Text);
        part.Rx = Or0(rx.Text); part.Ry = Or0(ry.Text); part.Rz = Or0(rz.Text);
        part.Color = Scene3D.Colors[Math.Clamp(color.SelectedIndex, 0, Scene3D.Colors.Length - 1)].Hex;
        part.Text = text.Text ?? "";
        return true;
    }

    private static string ShapeId(int index) => index >= 0 && index < Scene3D.PartShapes.Length ? Scene3D.PartShapes[index].Id : "mesh";

    private void DeletePart(TextBox objName, string id)
    {
        Keep(objName);
        var p = _scene.Parts.FirstOrDefault(x => x.Id == id);
        if (p is null) { Say("Das Teil gibt es nicht mehr.", "3d"); return; }
        Push();
        _scene.Parts.Remove(p);
        if (_selId == id) _selId = _scene.Parts.LastOrDefault()?.Id;
        Say($"{p.Name} entfernt.", "3d");
    }

    private void Bevel(TextBox objName, TextBox amt) => Edit(objName, p =>
    {
        var n = Read(amt.Text);
        if (double.IsNaN(n) || n <= 0) return "Die Abrundung muss größer als 0 sein.";
        p.Ops.Add(new PartOp { Kind = "bevel", Amount = n });
        return $"Kanten von {p.Name} um {Part3D.Num(n)} cm gerundet.";
    });

    private void Hole(TextBox objName, TextBox dia, ComboBox axis) => Edit(objName, p =>
    {
        var n = Read(dia.Text);
        if (double.IsNaN(n) || n <= 0) return "Der Durchmesser muss größer als 0 sein.";
        var ax = axis.SelectedIndex switch { 0 => "x", 1 => "y", _ => "z" };
        p.Ops.Add(new PartOp { Kind = "hole", Diameter = n, Axis = ax });
        return $"Loch mit {Part3D.Num(n)} cm Durchmesser durch {p.Name}.";
    });

    private void Smooth(TextBox objName) => Edit(objName, p => { p.Ops.Add(new PartOp { Kind = "smooth", Amount = 1 }); return $"{p.Name} geglättet."; });

    private void Mirror(TextBox objName, ComboBox axis) => Edit(objName, p =>
    {
        var ax = axis.SelectedIndex switch { 1 => "y", 2 => "z", _ => "x" };
        p.Ops.Add(new PartOp { Kind = "mirror", Axis = ax });
        return $"{p.Name} über die {ax.ToUpperInvariant()}-Achse gespiegelt.";
    });

    private void Shell(TextBox objName, TextBox amt) => Edit(objName, p =>
    {
        var n = Read(amt.Text);
        if (double.IsNaN(n) || n <= 0) return "Die Wandstärke muss größer als 0 sein.";
        p.Ops.Add(new PartOp { Kind = "shell", Amount = n });
        return $"{p.Name} ausgehöhlt, Wand {Part3D.Num(n)} cm.";
    });

    private void Edit(TextBox objName, Func<Part3D, string> change)
    {
        Keep(objName);
        var p = Selected;
        if (p is null) { Say("Wähle zuerst ein Teil in der Liste.", "3d"); return; }
        Push();
        var msg = change(p);
        if (msg.StartsWith("Die ", StringComparison.Ordinal) || msg.StartsWith("Der ", StringComparison.Ordinal)) { UndoSceneSilent(); Say(msg, "3d"); return; }
        Say(msg, "3d");
    }

    private void UndoSceneSilent()
    {
        if (_undo3d.Count == 0) return;
        _scene = Scene3D.FromJson(_undo3d[^1]);
        _undo3d.RemoveAt(_undo3d.Count - 1);
    }

    private void SaveScene(TextBox objName)
    {
        Keep(objName);
        if (_scene.Parts.Count == 0) { Say("Noch nichts zu speichern. Füge zuerst ein Teil hinzu.", "3d"); return; }
        var id = string.IsNullOrWhiteSpace(_scene.ProjectId) ? Scene3D.Slug(_scene.Name) : _scene.ProjectId!;
        try
        {
            if (_core.Projects.List().All(p => p.Id != id)) _core.Projects.Create(id, _scene.Name);
            _scene.ProjectId = id;
            _core.Projects.WriteFile(id, "3d/szene.json", _scene.ToJson());
        }
        catch (Exception ex) { Say("Speichern nicht möglich: " + ex.Message, "3d"); return; }
        Say($"Gespeichert im Projekt „{_scene.Name}“. Du findest es unter Projekte.", "3d");
    }

    public void Open3DProject(string id)
    {
        try
        {
            _scene = Scene3D.FromJson(_core.Projects.ReadFile(id, "3d/szene.json"));
            _scene.ProjectId = id;
            _selId = _scene.Parts.FirstOrDefault()?.Id;
            _undo3d.Clear();
            _aiParts = null;
            _pngKey = "";
            Say($"Projekt „{_scene.Name}“ geöffnet.", "3d");
        }
        catch (Exception ex) { Say("Das Projekt lässt sich nicht öffnen: " + ex.Message, "3d"); }
    }

    private void NewScene(TextBox objName)
    {
        Keep(objName);
        if (_scene.Parts.Count > 0) Push();
        _scene = new Scene3D();
        _selId = null;
        _aiParts = null;
        _pngKey = "";
        Say("Neues Objekt. Gespeicherte Projekte bleiben erhalten.", "3d");
    }

    private void UndoScene()
    {
        if (_undo3d.Count == 0) { Say("Nichts zum Rückgängigmachen.", "3d"); return; }
        _scene = Scene3D.FromJson(_undo3d[^1]);
        _undo3d.RemoveAt(_undo3d.Count - 1);
        if (_selId is null || _scene.Parts.All(p => p.Id != _selId)) _selId = _scene.Parts.LastOrDefault()?.Id;
        Say("Rückgängig.", "3d");
    }

    private void SetView(string view, string msg) { _view3d = view; Say(msg, "3d"); }

    private async Task Propose(TextBox objName, TextBox sentence)
    {
        Keep(objName);
        var text = sentence.Text?.Trim() ?? "";
        if (text.Length < 3) { Say("Schreib einen Satz, zum Beispiel: ein Tisch, 120 mal 80 cm, mit vier Beinen.", "3d"); return; }
        Say("Die KI liest den Satz …", "3d");
        string raw;
        try { raw = SceneAi is { } local ? await local(text) : await _ollama.ChatAsync(KiModelName, ScenePlan.Prompt(text)); }
        catch (Exception ex) { _aiParts = null; Say("Die lokale KI antwortet nicht: " + ex.Message, "3d"); return; }
        if (!ScenePlan.TryRead(raw, out var parts, out var err)) { _aiParts = null; Say(err, "3d"); return; }
        _aiParts = parts;
        Say("So würde ich das bauen. Schau die Liste an, dann übernimm oder verwirf sie.", "3d");
    }

    private void UseAi(TextBox objName)
    {
        Keep(objName);
        if (_aiParts is not { Count: > 0 }) { Say("Es liegt kein Vorschlag da.", "3d"); return; }
        Push();
        _scene.Parts.AddRange(_aiParts);
        _selId = _aiParts[^1].Id;
        var n = _aiParts.Count;
        _aiParts = null;
        Say($"{n} Teile übernommen. Du kannst sie noch ändern.", "3d");
    }

    private async Task Browse(TextBox objName)
    {
        Keep(objName);
        string? path = null;
        if (Pick3DFile is { } pick) path = await pick();
        else
        {
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "3D-Datei öffnen",
                    AllowMultiple = false,
                    FileTypeFilter = [new FilePickerFileType("3D-Datei") { Patterns = ["*.obj", "*.stl", "*.glb", "*.gltf", "*.fbx", "*.blend"] }],
                });
                path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            }
            catch (Exception ex) { Say("Die Dateiauswahl geht hier nicht: " + ex.Message, "3d"); return; }
        }
        if (string.IsNullOrWhiteSpace(path)) { Say("Keine Datei gewählt.", "3d"); return; }
        ImportPath(path);
    }

    public void Import3DFile(string path) => ImportPath(path);

    private void ImportPath(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!File.Exists(path)) { Say("Die Datei gibt es nicht.", "3d"); return; }
        if (ext is ".fbx" or ".blend") { _ = ImportViaBlender(path); return; }
        if (ext is not ".obj" and not ".stl" and not ".glb" and not ".gltf") { Say("Trendo öffnet OBJ, STL, GLB und glTF. FBX und Blend gehen über Blender.", "3d"); return; }
        try
        {
            var part = Mesh3D.Import(path);
            Push();
            _scene.Parts.Add(part);
            _selId = part.Id;
            var unit = ext == ".stl" ? "STL rechne ich von Millimetern in Zentimeter um." : "Die Zahlen nehme ich als Zentimeter.";
            Say($"Eingelesen: {part.Name}, {part.Indices!.Length / 3} Dreiecke. {unit}", "3d");
        }
        catch (Exception ex) { Say(ex.Message, "3d"); }
    }

    private async Task ImportViaBlender(string path)
    {
        var bl = BlenderPath;
        if (bl is null) { await AskBlender(); return; }
        var (r, obj) = Studio3D.Convert(bl, path, ".obj");
        if (!r.Ok || !File.Exists(obj)) { Say("Blender konnte die Datei nicht lesen: " + Short(r.Err.Length > 0 ? r.Err : r.Out), "3d"); return; }
        ImportPath(obj);
    }

    private void ExportNative(TextBox objName, string ext)
    {
        Keep(objName);
        if (_scene.Parts.Count == 0) { Say("Noch kein Teil. Füge zuerst eines hinzu.", "3d"); return; }
        try
        {
            var mesh = Mesh3D.FromScene(_scene);
            if (mesh.Triangles == 0) { Say("Das Objekt hat keine Fläche.", "3d"); return; }
            var path = Mesh3D.Unique(Path.Combine(Folder3D, DevStudio.SafeName(_scene.Name) + ext));
            if (ext == ".stl") Mesh3D.WriteStl(mesh, path);
            else if (ext == ".glb") Mesh3D.WriteGlb(mesh, path);
            else Mesh3D.WriteObj(mesh, path);
            _last3dFile = path;
            var hint = ext switch { ".stl" => "Maße in Millimetern.", ".glb" => "Maße in Metern.", _ => "Maße in Zentimetern." };
            Say($"Gespeichert: {Path.GetFileName(path)}. {hint}", "3d");
        }
        catch (Exception ex) { Say("Export nicht möglich: " + ex.Message, "3d"); }
    }

    private async Task ExportWithBlender(TextBox objName, string ext)
    {
        Keep(objName);
        if (_scene.Parts.Count == 0) { Say("Noch kein Teil. Füge zuerst eines hinzu.", "3d"); return; }
        var bl = await ReadyBlender();
        if (bl is null) return;
        var spec = _scene.WriteBlenderSpec(Path.Combine(Folder3D, "blender"));
        var blend = Mesh3D.Unique(Path.Combine(Folder3D, DevStudio.SafeName(_scene.Name) + ".blend"));
        string? fbx = ext == ".fbx" ? Mesh3D.Unique(Path.Combine(Folder3D, DevStudio.SafeName(_scene.Name) + ".fbx")) : null;
        var (r, _) = Studio3D.ExportBlend(bl, spec, blend, fbx);
        var wanted = fbx ?? blend;
        if (!r.Ok || !File.Exists(wanted)) { Say("Blender konnte die Datei nicht schreiben: " + Short(r.Err.Length > 0 ? r.Err : r.Out), "3d"); return; }
        _last3dFile = wanted;
        Say($"Gespeichert: {Path.GetFileName(wanted)}.", "3d");
    }

    private async Task SetupBlender()
    {
        if (BlenderPath is { } have && File.Exists(have)) { Say("Blender ist schon eingerichtet.", "3d"); return; }
        var check = await _deps.CheckAsync(DependencyCatalog.Get("blender"));
        if (check.Ok && check.Path is { } found && File.Exists(found)) { Say("Blender ist schon eingerichtet.", "3d"); return; }
        _depStudio = "CORE-05";
        _depStudioTitle = "3D-Modelle erstellen";
        _depChecks = [check];
        _depReturn = "3d";
        _depLog.Text = "Blender fehlt. Trendo fragt einmal, bevor etwas installiert wird.";
        Show("studio-deps");
    }

    private async Task AskBlender()
    {
        if (_scene.Parts.Count == 0) { Say("Noch kein Objekt. Füge zuerst ein Teil hinzu.", "3d"); return; }
        var bl = await ReadyBlender();
        if (bl is null) return;
        var spec = _scene.WriteBlenderSpec(Path.Combine(Folder3D, "blender"));
        var blend = Mesh3D.Unique(Path.Combine(Folder3D, DevStudio.SafeName(_scene.Name) + ".blend"));
        var (r, _) = Studio3D.ExportBlend(bl, spec, blend);
        if (!r.Ok || !File.Exists(blend)) { Say("Blender konnte das Objekt nicht öffnen: " + Short(r.Err.Length > 0 ? r.Err : r.Out), "3d"); return; }
        _last3dFile = blend;
        _core.Integrations.LaunchDirect(Principal.LocalOwner, bl, _core.Audit, blend);
        Say($"Blender öffnet {Path.GetFileName(blend)}.", "3d");
    }

    private async Task<string?> ReadyBlender()
    {
        if (BlenderPath is { } have && File.Exists(have)) return have;
        var check = await _deps.CheckAsync(DependencyCatalog.Get("blender"));
        if (check.Ok && check.Path is { } found && File.Exists(found)) return found;
        _depStudio = "CORE-05";
        _depStudioTitle = "3D-Modelle erstellen";
        _depChecks = [check];
        _depReturn = "3d";
        _depLog.Text = "Blender fehlt. Trendo fragt einmal, bevor etwas installiert wird.";
        Show("studio-deps");
        return null;
    }

    private void ShowFolder()
    {
        var target = _last3dFile is { } f && File.Exists(f) ? Path.GetDirectoryName(f)! : Folder3D;
        if (OperatingSystem.IsWindows() && _last3dFile is { } file && File.Exists(file))
            _core.Integrations.LaunchDirect(Principal.LocalOwner, "explorer.exe", _core.Audit, "/select,\"" + file + "\"");
        else Launch(target);
        Say(_last3dFile is { } name && File.Exists(name) ? $"Ordner geöffnet, Datei: {Path.GetFileName(name)}." : "Ordner geöffnet.", "3d");
    }

    private byte[]? PreviewBytes()
    {
        var key = _view3d + "|" + _scene.ToJson();
        if (key == _pngKey) return _png3d;
        _pngKey = key;
        _png3d = Mesh3D.RenderPng(_scene, _view3d, 360, 270);
        return _png3d;
    }

    private void Push()
    {
        _undo3d.Add(_scene.ToJson());
        if (_undo3d.Count > 30) _undo3d.RemoveAt(0);
        _pngKey = "";
    }

    private void Keep(TextBox name)
    {
        if (!string.IsNullOrWhiteSpace(name.Text)) _scene.Name = name.Text.Trim();
    }

    private static double Read(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return double.NaN;
        return double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;
    }

    private static double Or0(string? s) { var v = Read(s); return double.IsNaN(v) ? 0 : v; }

    private static TextBox NumBox(string name, double value) => new() { Name = name, Text = value.ToString("0.##", CultureInfo.GetCultureInfo("de-DE")), Width = 72 };

    private static StackPanel Labeled(string label, Control box) => new() { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 0, 12, 4), Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, box } };

}
