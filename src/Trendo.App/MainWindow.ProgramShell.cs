using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0231 V2/V3/V6/V9: Vorlage eines Studio-Programms – Startseite (Neu · Vorlagen · Zuletzt · Wiederherstellen), Reiter für mehrere Dokumente,
/// Menüband (Datei, Bearbeiten, Studio-Gruppen, Export), Arbeitsfläche, Eigenschaften rechts, Statusleiste, Tastenkürzel, automatisches Sichern.</summary>
public sealed record ProgramTemplate(string Id, string Name, string Hint, Func<JsonObject> Create);
public sealed record ProgramCmd(string Name, string Label, Action<ProgramDocument> Run, bool Primary = false, string? Tip = null);
public sealed record ProgramExport(string Name, string Label, string Extension, Func<ProgramDocument, string, string> Write);

public sealed class ProgramSpec(string key, string studio, string title)
{
    public string Key { get; } = key;
    public string Studio { get; } = studio;
    public string Title { get; } = title;
    public string Sentence { get; init; } = "";
    public string Folder { get; init; } = title;
    public List<ProgramTemplate> Templates { get; init; } = [];
    public List<(string Group, List<ProgramCmd> Cmds)> Ribbon { get; init; } = [];
    public List<ProgramExport> Exports { get; init; } = [];
    public required Func<ProgramDocument, Control> Workspace { get; init; }
    public Func<ProgramDocument, Control>? Properties { get; init; }
    public Func<ProgramDocument, string>? Status { get; init; }
    public string Help { get; init; } = "";
}

public partial class MainWindow
{
    public static readonly Dictionary<string, ProgramSpec> Programs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ProgramDocument>> _progDocs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _progActive = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _progMsg = new(StringComparer.OrdinalIgnoreCase);
    private DispatcherTimer? _progAutosave;
    public ProgramRecent ProgRecent => new(_core.DataRoot);
    string ProgBackup => Path.Combine(_core.DataRoot, "programme", "sicherung");

    /// <summary>Studio-Programm anmelden: Seite, Reiter und Suchbegriffe wie bei jeder Studio-Seite.</summary>
    private void RegisterProgram(List<(string, string, string)> tabs, ProgramSpec spec, params string[] words)
    {
        Programs[spec.Key] = spec;
        RegisterTab(tabs, spec.Key, spec.Title, spec.Studio, () => ProgramView(spec), words);
        if (_progAutosave is null)
        {
            _progAutosave = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _progAutosave.Tick += (_, _) => ProgramAutosaveAll();
            _progAutosave.Start();
        }
    }

    // ---------- Öffentliche Wege (Tests, KI, Abnahme V12) ----------
    public List<ProgramDocument> ProgramDocs(string key) => _progDocs.TryGetValue(key, out var l) ? l : _progDocs[key] = [];
    public ProgramDocument? ProgramActive(string key) { var l = ProgramDocs(key); return l.Count == 0 ? null : l[Math.Clamp(_progActive.GetValueOrDefault(key), 0, l.Count - 1)]; }
    public string ProgramMessage(string key) => _progMsg.GetValueOrDefault(key, "");

    public ProgramDocument ProgramNew(string key, string? templateId = null)
    {
        var spec = Programs[key];
        var t = spec.Templates.FirstOrDefault(x => x.Id == templateId) ?? spec.Templates.FirstOrDefault();
        var d = new ProgramDocument(spec.Studio, t is null || t.Id == "leer" ? "Unbenannt" : t.Name, t?.Create() ?? []);
        var l = ProgramDocs(key); l.Add(d); _progActive[key] = l.Count - 1;
        _progMsg[key] = t is null || t.Id == "leer" ? "Neues Dokument." : $"Vorlage „{t.Name}“ – einfach anpassen.";
        return d;
    }

    public ProgramDocument ProgramOpen(string key, string path)
    {
        var spec = Programs[key]; var l = ProgramDocs(key);
        var have = l.FindIndex(x => x.Path is not null && string.Equals(Path.GetFullPath(x.Path), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
        if (have >= 0) { _progActive[key] = have; return l[have]; }
        var d = ProgramDocument.Load(path, spec.Studio);
        l.Add(d); _progActive[key] = l.Count - 1; ProgRecent.Add(spec.Studio, path);
        _progMsg[key] = "Geöffnet: " + Path.GetFileName(path);
        _core.Audit.Append(Principal.LocalOwner.Name, "program.open", $"{spec.Studio} {Path.GetFileName(path)}");
        return d;
    }

    /// <summary>Speichern; ohne Ort in den Studio-Ordner der Projekte (vorhandene Dateien werden nicht überschrieben).</summary>
    public string ProgramSave(string key, string? path = null)
    {
        var spec = Programs[key]; var d = ProgramActive(key) ?? throw new InvalidOperationException("Es ist kein Dokument offen.");
        var target = path ?? d.Path;
        if (target is null)
        {
            var dir = WorkFolder(spec.Folder); var stem = DevStudio.SafeName(d.Title); target = Path.Combine(dir, stem + ProgramDocument.Extension);
            for (var n = 2; File.Exists(target); n++) target = Path.Combine(dir, $"{stem} ({n}){ProgramDocument.Extension}");
        }
        d.Save(target); ProgRecent.Add(spec.Studio, d.Path!);
        _core.Audit.Append(Principal.LocalOwner.Name, "program.save", $"{spec.Studio} {Path.GetFileName(d.Path)}");
        _progMsg[key] = "Gespeichert: " + d.Path; return d.Path!;
    }

    public void ProgramClose(string key, int index)
    {
        var l = ProgramDocs(key); if (index < 0 || index >= l.Count) return;
        l.RemoveAt(index); _progActive[key] = Math.Max(0, Math.Min(_progActive.GetValueOrDefault(key), l.Count - 1));
    }

    public void ProgramAutosaveAll()
    {
        foreach (var l in _progDocs.Values) foreach (var d in l) try { d.Autosave(ProgBackup); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Eigenschaftsfelder übernehmen mit Enter (bei einzeiligen Feldern) oder beim Verlassen.</summary>
    private static void OnEnter(TextBox t, Action commit) => t.KeyDown += (_, e) => { if (e.Key == Key.Enter && !t.AcceptsReturn) { commit(); e.Handled = true; } };

    /// <summary>Befehl ausführen: Fehler werden zur Meldung, danach Seite neu zeichnen.</summary>
    private void ProgramRun(string key, Action a)
    {
        try { a(); }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException or FormatException or KeyNotFoundException) { _progMsg[key] = ex.Message; }
        Show(key);
    }

    // ---------- Oberfläche ----------
    private Control ProgramView(ProgramSpec spec)
    {
        var key = spec.Key;
        var icon = StudioIcons.For(spec.Studio);
        var (c1, c2) = icon is null ? ("#7FD3FF", "#2F6BF6") : (icon.C1, icon.C2);
        var docs = ProgramDocs(key); var d = ProgramActive(key);

        // V6: Kopfband in der Farbwelt des Studios
        var title = d is null ? null : new TextBox { Name = "prog-title", Text = d.Title, FontSize = 15, FontWeight = FontWeight.SemiBold, MinWidth = 260, Background = Brush.Parse("#26FFFFFF"), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
        if (title is not null) title.LostFocus += (_, _) => { if (d is not null && !string.IsNullOrWhiteSpace(title.Text) && title.Text.Trim() != d.Title) { d.Title = title.Text.Trim(); d.Edit("Titel", _ => { }); Show(key); } };
        var band = new Border
        {
            Name = "prog-band", CornerRadius = new CornerRadius(16), Padding = new Thickness(16, 12), BoxShadow = CardShadow,
            Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse(c2), 0), new GradientStop(Color.Parse(c1), 1) } },
            BorderBrush = Brush.Parse("#55FFFFFF"), BorderThickness = new Thickness(1, 1, 1, 0),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
                new Border { Width = 46, Height = 46, CornerRadius = new CornerRadius(12), Background = Brush.Parse("#33FFFFFF"), Child = icon is null ? null : Ico(icon.Path, 26, Brushes.White) },
                Pos(new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = {
                    new TextBlock { Text = spec.Title, Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeight.Bold },
                    new TextBlock { Text = spec.Sentence, Foreground = Brush.Parse("#E6FFFFFF"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis } } }, 1),
                Pos(title is null ? new Panel() : new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = {
                    title, new TextBlock { Name = "prog-dirty", Text = d!.Dirty ? "● geändert" : d.Path is null ? "nicht gespeichert" : "✓ gespeichert", Foreground = Brushes.White, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } }, 2) } },
        };

        Control body = d is null ? ProgramStart(spec) : ProgramWork(spec, d, docs);
        var root = new StackPanel { Name = "prog-root", Spacing = 10, Children = { band, body } };
        if (_progMsg.GetValueOrDefault(key) is { Length: > 0 } msg) root.Children.Insert(1, ResultCard(msg));
        root.KeyDown += (_, e) =>
        {
            if (e.KeyModifiers != KeyModifiers.Control) return;
            switch (e.Key)
            {
                case Key.S when d is not null: ProgramRun(key, () => ProgramSave(key)); e.Handled = true; break;
                case Key.Z when d is not null: ProgramRun(key, () => d.Undo()); e.Handled = true; break;
                case Key.Y when d is not null: ProgramRun(key, () => d.Redo()); e.Handled = true; break;
                case Key.N: ProgramRun(key, () => ProgramNew(key)); e.Handled = true; break;
                case Key.W when d is not null: ProgramRun(key, () => ProgramClose(key, _progActive.GetValueOrDefault(key))); e.Handled = true; break;
            }
        };
        return Page(spec.Title, root);
    }

    /// <summary>G1/V2: Startseite des Programms.</summary>
    private Control ProgramStart(ProgramSpec spec)
    {
        var key = spec.Key;
        var tpl = new WrapPanel { Name = "prog-templates" };
        foreach (var t in spec.Templates)
        {
            var tt = t;
            var b = new Button { Name = "prog-tpl-" + t.Id, Width = 210, MinHeight = 96, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(14, 12), CornerRadius = new CornerRadius(14),
                Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top,
                Content = new StackPanel { Spacing = 4, Children = {
                    new TextBlock { Text = t.Id == "leer" ? "＋ " + t.Name : t.Name, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = t.Hint, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap } } } };
            b.Click += (_, _) => ProgramRun(key, () => ProgramNew(key, tt.Id));
            tpl.Children.Add(b);
        }
        var recent = new StackPanel { Name = "prog-recent", Spacing = 3 };
        var i = 0;
        foreach (var f in ProgRecent.Get(spec.Studio))
        {
            var path = f;
            var b = new Button { Name = $"prog-recent-{i++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Padding = new Thickness(8, 5),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = Path.GetFileNameWithoutExtension(f), Foreground = Text, FontSize = 13 }, Pos(P(File.GetLastWriteTime(f).ToString("dd.MM.yyyy HH:mm", De), null, 11), 1) } } };
            ToolTip.SetTip(b, f);
            b.Click += (_, _) => ProgramRun(key, () => ProgramOpen(key, path));
            recent.Children.Add(b);
        }
        if (i == 0) recent.Children.Add(P("Noch nichts gespeichert.", null, 12));
        var open = Field("prog-open-path", "Projektdatei öffnen (" + ProgramDocument.Extension + ")", 320);
        recent.Children.Add(Line(open, Act("prog-open", "Öffnen", () => ProgramRun(key, () => { var p = Need(open, "die Datei"); if (!File.Exists(p)) throw new FileNotFoundException("Diese Datei gibt es nicht."); ProgramOpen(key, p); }))));
        var sp = new StackPanel { Spacing = 10, Children = {
            Card(new StackPanel { Spacing = 8, Children = { H("Neu", 15), tpl } }),
            Card(new StackPanel { Spacing = 6, Children = { H("Zuletzt", 15), recent } }) } };
        var orphans = ProgramDocument.Orphans(ProgBackup, spec.Studio);
        if (orphans.Count > 0)
        {
            var rs = new StackPanel { Spacing = 4, Children = { H("Wiederherstellen", 15), P("Diese Dokumente wurden nicht gespeichert, aber automatisch gesichert.", null, 12) } };
            var k = 0;
            foreach (var o in orphans.Take(5))
            {
                var file = o;
                rs.Children.Add(Row(P($"{ProgramDocument.Load(file).Title} · gesichert {File.GetLastWriteTime(file):dd.MM. HH:mm}", Text, 12.5), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                    Act($"prog-restore-{k}", "Wiederherstellen", () => ProgramRun(key, () => { var d = ProgramDocument.Restore(file); File.Delete(file); var l = ProgramDocs(key); l.Add(d); _progActive[key] = l.Count - 1; _progMsg[key] = "Wiederhergestellt – bitte speichern."; }), true),
                    Act($"prog-discard-{k}", "Verwerfen", () => ProgramRun(key, () => File.Delete(file))) } }));
                k++;
            }
            sp.Children.Insert(0, Card(rs));
        }
        return sp;
    }

    /// <summary>V2/V9: Reiter, Menüband, Arbeitsfläche, Eigenschaften, Statusleiste.</summary>
    private Control ProgramWork(ProgramSpec spec, ProgramDocument d, List<ProgramDocument> docs)
    {
        var key = spec.Key;
        // V9: Reiter der offenen Dokumente
        var tabs = new WrapPanel { Name = "prog-tabs" };
        for (var k = 0; k < docs.Count; k++)
        {
            var idx = k; var doc = docs[k]; var on = ReferenceEquals(doc, d);
            var t = new Button { Name = $"prog-tab-{k}", Content = doc.Title + (doc.Dirty ? " •" : ""), FontSize = 12.5, Padding = new Thickness(12, 5), Margin = new Thickness(0, 0, 2, 0), CornerRadius = new CornerRadius(10, 10, 0, 0),
                Background = on ? TileBg : Brushes.Transparent, Foreground = on ? Text : Muted, BorderBrush = on ? EdgeLit : Edge, BorderThickness = new Thickness(1, 1, 1, 0) };
            t.Click += (_, _) => { _progActive[key] = idx; Show(key); };
            var x = new Button { Name = $"prog-tab-close-{k}", Content = "×", Padding = new Thickness(6, 1), Margin = new Thickness(0, 0, 8, 0), Background = Brushes.Transparent, FontSize = 12 };
            ToolTip.SetTip(x, doc.Dirty ? "Schließen (Änderungen sind automatisch gesichert)" : "Schließen");
            x.Click += (_, _) => ProgramRun(key, () => { if (doc.Dirty) doc.Autosave(ProgBackup); ProgramClose(key, idx); });
            tabs.Children.Add(t); tabs.Children.Add(x);
        }
        var plus = new Button { Name = "prog-tab-new", Content = "＋", Padding = new Thickness(10, 3), Background = Brushes.Transparent };
        ToolTip.SetTip(plus, "Neues Dokument (Strg+N)"); plus.Click += (_, _) => ProgramRun(key, () => ProgramNew(key));
        tabs.Children.Add(plus);

        // Menüband
        var saveAs = Field("prog-saveas-path", "Speichern unter: Ordner\\Name", 240);
        var groups = new List<(string, List<Control>)>
        {
            ("Datei", [
                RBtn("prog-new", "Neu", () => ProgramRun(key, () => ProgramNew(key))),
                RBtn("prog-save", "Speichern", () => ProgramRun(key, () => ProgramSave(key)), true),
                saveAs, RBtn("prog-saveas", "Speichern unter", () => ProgramRun(key, () => ProgramSave(key, Need(saveAs, "Ordner und Namen")))),
                RBtn("prog-close", "Schließen", () => ProgramRun(key, () => { if (d.Dirty) d.Autosave(ProgBackup); ProgramClose(key, _progActive.GetValueOrDefault(key)); })),
            ]),
            ("Bearbeiten", [
                RBtn("prog-undo", "↶ Rückgängig", () => ProgramRun(key, () => { if (!d.Undo()) throw new InvalidOperationException("Es gibt nichts rückgängig zu machen."); }), tip: d.UndoLabel is { } u ? "Rückgängig: " + u : "Nichts rückgängig zu machen"),
                RBtn("prog-redo", "↷ Wiederholen", () => ProgramRun(key, () => { if (!d.Redo()) throw new InvalidOperationException("Es gibt nichts zu wiederholen."); })),
            ]),
        };
        foreach (var (g, cmds) in spec.Ribbon)
            groups.Add((g, cmds.Select(c => (Control)RBtn("prog-cmd-" + c.Name, c.Label, () => ProgramRun(key, () => c.Run(d)), c.Primary, c.Tip)).ToList()));
        if (spec.Exports.Count > 0)
            groups.Add(("Exportieren", spec.Exports.Select(x => (Control)RBtn("prog-export-" + x.Name, x.Label, () => ProgramRun(key, () =>
            {
                var file = Path.Combine(WorkFolder(spec.Folder), DevStudio.SafeName(d.Title) + x.Extension);
                var res = x.Write(d, file);
                _core.Audit.Append(Principal.LocalOwner.Name, "program.export", $"{spec.Studio} {Path.GetFileName(res)}");
                _progMsg[key] = "Exportiert: " + res; NotifyFileResult(_progMsg[key]);
            }))).ToList()));
        if (spec.Help.Length > 0) groups.Add(("Hilfe", [RBtn("prog-help", "? Hilfe", () => Toast(spec.Help))]));
        var ribbon = new WrapPanel { Name = "prog-ribbon" };
        foreach (var (g, items) in groups)
        {
            var row = new WrapPanel(); foreach (var it in items) { it.Margin = new Thickness(0, 0, 4, 4); it.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(it); }
            ribbon.Children.Add(new Border { Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10, 6, 10, 4), CornerRadius = new CornerRadius(12), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1),
                Child = new StackPanel { Spacing = 2, Children = { row, new TextBlock { Text = g, Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } } });
        }

        // Arbeitsfläche und Eigenschaften
        var work = new Border { Name = "prog-workspace", CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Padding = new Thickness(12), MinHeight = 420, Child = spec.Workspace(d) };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(spec.Properties is null ? "*" : "*,12,300"), Children = { work } };
        if (spec.Properties is { } props)
            grid.Children.Add(Pos(new Border { Name = "prog-properties", CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Padding = new Thickness(12),
                Child = new StackPanel { Spacing = 6, Children = { H("Eigenschaften", 13), props(d) } } }, 2));

        // Statusleiste
        var status = new Border { Name = "prog-status", CornerRadius = new CornerRadius(10), Background = Brush.Parse(C("#33081329", "#14000000")), Padding = new Thickness(12, 5),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new TextBlock { Name = "prog-status-text", Text = spec.Status?.Invoke(d) ?? "", Foreground = Muted, FontSize = 12 },
                Pos(new TextBlock { Text = (d.Path is null ? "Noch nicht gespeichert" : Path.GetFileName(d.Path)) + (d.Dirty ? " · geändert, wird automatisch gesichert" : "") + $" · {d.Changed:HH:mm}", Foreground = Muted, FontSize = 12 }, 1) } } };
        return new StackPanel { Spacing = 0, Children = { tabs, new StackPanel { Spacing = 8, Children = { ribbon, grid, status } } } };

        Button RBtn(string name, string label, Action go, bool primary = false, string? tip = null)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12.5, Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8), Foreground = primary ? Brushes.White : Text,
                Background = primary ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(primary ? 0 : 1) };
            if (tip is not null) ToolTip.SetTip(b, tip);
            b.Click += (_, _) => go(); return b;
        }
    }
}
