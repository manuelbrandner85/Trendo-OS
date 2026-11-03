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
/// <summary><paramref name="Level"/>: ab welcher Bedienstufe das Werkzeug erscheint (0 Einfach, 1 Erweitert, 2 Profi – siehe <see cref="UserLevel"/>).</summary>
public sealed record ProgramCmd(string Name, string Label, Action<ProgramDocument> Run, bool Primary = false, string? Tip = null, int Level = 0);
public sealed record ProgramExport(string Name, string Label, string Extension, Func<ProgramDocument, string, string> Write);
/// <summary>Paket F (A1 Profi-Bauplan): ein Werkzeug der Werkzeugleiste links – Zeichen, Name, Hinweis; <paramref name="Active"/> markiert das gewählte.</summary>
public sealed record ProgramTool(string Name, string Icon, string Label, string Tip, Action<ProgramDocument> Select, Func<bool> Active);
/// <summary>Paket F (A1): Bereich unten (Zeitleiste, Ebenen, Messwerte …).</summary>
public sealed record ProgramPanel(string Title, Func<ProgramDocument, Control> Build, double Height = 180);

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
    /// <summary>Paket F (A1): Werkzeugleiste links (wie in Profi-Programmen) – einklappbar.</summary>
    public List<ProgramTool> Tools { get; init; } = [];
    /// <summary>Paket F (A1): Bereich unten – einklappbar.</summary>
    public ProgramPanel? Bottom { get; init; }
    public Func<ProgramDocument, string>? Status { get; init; }
    public string Help { get; init; } = "";
    /// <summary>T-0256: Arbeitsfläche füllt die Fensterhöhe (Zeichenprogramme) statt mitzuscrollen.</summary>
    public bool FillHeight { get; init; }
    /// <summary>Masterprompt 131: Bedienstufe je Menüband-Gruppe (fehlt = Einfach); Befehle können zusätzlich eine eigene Stufe haben.</summary>
    public Dictionary<string, int> GroupLevels { get; init; } = [];
    public Dictionary<string, int> CmdLevels { get; init; } = [];
    /// <summary>Arbeitsfläche aus Karten und Listen (statt einer Zeichenfläche): bei voller Fensterhöhe senkrecht scrollbar.</summary>
    public bool ScrollWorkspace { get; init; }
    public int LevelOf(ProgramCmd c) => Math.Max(c.Level, CmdLevels.GetValueOrDefault(c.Name));
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
        OfficeAutosave(); // T-0254: Text, Tabelle, Präsentation
    }

    /// <summary>Eigenschaftsfelder übernehmen mit Enter (bei einzeiligen Feldern) oder beim Verlassen.</summary>
    /// <summary>T-0273: Ein Eingabefeld, das nach dem Neuzeichnen schon aus der Seite gelöst ist, übernimmt beim Fokusverlust nichts mehr –
    /// sonst schreibt es nach Enter + „Rückgängig“ den alten Wert erneut zurück. Klassen-Handler laufen vor den Feld-Handlern.</summary>
    static readonly bool StaleFocusGuard = RegisterStaleFocusGuard();
    static bool RegisterStaleFocusGuard()
    {
        InputElement.LostFocusEvent.AddClassHandler<TextBox>((t, e) => { if (TopLevel.GetTopLevel(t) is null) e.Handled = true; });
        return true;
    }

    private static void OnEnter(TextBox t, Action commit) => t.KeyDown += (_, e) => { if (e.Key == Key.Enter && !t.AcceptsReturn) { commit(); e.Handled = true; } };

    /// <summary>Befehl ausführen: Fehler werden zur Meldung, danach Seite neu zeichnen.</summary>
    private void ProgramRun(string key, Action a)
    {
        if (AssistQuiet > 0) { a(); return; } // T-0315: Vorschau-Lauf des Assistenten – kein Neuzeichnen, Fehler gehen an den Assistenten
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
                    spec.FillHeight && d is not null && _progMsg.GetValueOrDefault(key) is { Length: > 0 } bm
                        ? new SelectableTextBlock { Name = "prog-msg", Text = "➜ " + bm, Foreground = Brushes.White, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, MaxHeight = 40 }
                        : new TextBlock { Text = spec.Sentence, Foreground = Brush.Parse("#E6FFFFFF"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis } } }, 1),
                Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = {
                    ClaudeBand(key), // Claude in der Kopfzeile jedes Programms – nimmt keine Zeile im Menüband weg
                    title is null ? LevelSwitch(key) : new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = {
                    LevelSwitch(key), title, new TextBlock { Name = "prog-dirty", Text = d!.Dirty ? "● geändert" : d.Path is null ? "nicht gespeichert" : "✓ gespeichert", Foreground = Brushes.White, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } } } }, 2) } },
        };

        Panel root;
        if (spec.FillHeight && d is not null)
        {
            // Kopf (Band, Meldung, Reiter, Menüband) fest, darunter Arbeitsfläche + Eigenschaften in voller Höhe, Statusleiste unten
            var (top, grid, status) = ProgramWorkParts(spec, d, docs);
            band.Padding = new Thickness(14, 8);
            var head = new StackPanel { Spacing = 6, Children = { band, top } };
            if (ClaudeChangeBanner(key, d) is { } cb) head.Children.Insert(1, cb); // Claude hat geändert: vergleichen, behalten, verwerfen
            if (ClaudeProgramExtra(key) is { } cx) head.Children.Insert(1, cx); // z. B. das spielbare Spiel, das Claude gebaut hat
            // DockPanel: Kopf und Statusleiste bekommen die echte Breite (Menüband bricht richtig um), die Arbeitsfläche den Rest
            head.Margin = new Thickness(0, 0, 0, 8); status.Margin = new Thickness(0, 8, 0, 0);
            DockPanel.SetDock(head, Avalonia.Controls.Dock.Top); DockPanel.SetDock(status, Avalonia.Controls.Dock.Bottom);
            root = new DockPanel { Name = "prog-root", LastChildFill = true, Children = { head, status, grid } };
        }
        else
        {
            Control body = d is null ? ProgramStart(spec) : ProgramWork(spec, d, docs);
            root = new StackPanel { Name = "prog-root", Spacing = 10, Children = { band, body } };
            if (_progMsg.GetValueOrDefault(key) is { Length: > 0 } msg) root.Children.Insert(1, ResultCard(msg));
            if (ClaudeChangeBanner(key, d) is { } cb) root.Children.Insert(1, cb); // Claude hat geändert: vergleichen, behalten, verwerfen
            if (ClaudeProgramExtra(key) is { } cx) root.Children.Insert(1, cx); // z. B. das spielbare Spiel, das Claude gebaut hat
        }
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
        return spec.FillHeight && d is not null ? FitPage(spec.Title, new Panel(), root, pageHead: false) : Page(spec.Title, root);
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
        var (top, grid, status) = ProgramWorkParts(spec, d, docs);
        var inner = (StackPanel)((StackPanel)top).Children[1]; inner.Children.Add(grid); inner.Children.Add(status);
        return top;
    }

    private (Control Top, Control Grid, Control Status) ProgramWorkParts(ProgramSpec spec, ProgramDocument d, List<ProgramDocument> docs)
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
        // Zeichenprogramme: „Speichern unter …“ klappt das Feld auf, statt Platz im Menüband zu belegen
        List<Control> SaveAsControls()
        {
            var go = RBtn("prog-saveas", "Speichern unter", () => ProgramRun(key, () => ProgramSave(key, Need(saveAs, "Ordner und Namen"))));
            if (!spec.FillHeight) return [saveAs, go];
            var open = RBtn("prog-saveas-open", "Speichern unter …", () => { });
            go.Margin = new Thickness(0, 6, 0, 0);
            open.Flyout = new Flyout { Content = new StackPanel { Width = 380, Spacing = 4, Children = { P("Ordner und Dateiname", null, 12), saveAs, go } } };
            return [open];
        }
        var groups = new List<(string, List<Control>)>
        {
            ("Datei", [
                RBtn("prog-new", "Neu", () => ProgramRun(key, () => ProgramNew(key))),
                RBtn("prog-save", "Speichern", () => ProgramRun(key, () => ProgramSave(key)), true),
                .. SaveAsControls(),
                RBtn("prog-close", "Schließen", () => ProgramRun(key, () => { if (d.Dirty) d.Autosave(ProgBackup); ProgramClose(key, _progActive.GetValueOrDefault(key)); })),
            ]),
            ("Bearbeiten", [
                RBtn("prog-undo", "↶ Rückgängig", () => ProgramRun(key, () => { if (!d.Undo()) throw new InvalidOperationException("Es gibt nichts rückgängig zu machen."); }), tip: d.UndoLabel is { } u ? "Rückgängig: " + u : "Nichts rückgängig zu machen"),
                RBtn("prog-redo", "↷ Wiederholen", () => ProgramRun(key, () => { if (!d.Redo()) throw new InvalidOperationException("Es gibt nichts zu wiederholen."); })),
            ]),
        };
        var level = ProgramLevel;
        foreach (var (g, cmds) in spec.Ribbon)
        {
            if (!UserLevel.Shows(level, spec.GroupLevels.GetValueOrDefault(g))) continue;
            var shown = cmds.Where(c => UserLevel.Shows(level, spec.LevelOf(c))).Select(c => (Control)RBtn("prog-cmd-" + c.Name, c.Label, () => ProgramRun(key, () => c.Run(d)), c.Primary, c.Tip)).ToList();
            if (shown.Count > 0) groups.Add((g, shown));
        }
        if (spec.Exports.Count > 0)
            groups.Add(("Exportieren", spec.Exports.Take(level == UserLevel.Einfach ? 1 : int.MaxValue).Select(x => (Control)RBtn("prog-export-" + x.Name, x.Label, () => ProgramRun(key, () =>
            {
                var file = Path.Combine(WorkFolder(spec.Folder), DevStudio.SafeName(d.Title) + x.Extension);
                var res = x.Write(d, file);
                _core.Audit.Append(Principal.LocalOwner.Name, "program.export", $"{spec.Studio} {Path.GetFileName(res)}");
                _progMsg[key] = "Exportiert: " + res; NotifyFileResult(_progMsg[key]);
            }))).ToList()));
        if (level == UserLevel.Profi) groups.Add(("Profi", ProTools(spec, d)));
        if (spec.Help.Length > 0) groups.Add(("Hilfe", [RBtn("prog-help", "? Hilfe", () => Toast(spec.Help))]));
        var ribbon = new WrapPanel { Name = "prog-ribbon" };
        foreach (var (g, items) in groups)
        {
            var row = new WrapPanel(); foreach (var it in items) { it.Margin = new Thickness(0, 0, 4, 4); it.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(it); }
            if (spec.FillHeight)
            {
                var grp = new Border { Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(6, 4, 6, 0), CornerRadius = new CornerRadius(10), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = row };
                ToolTip.SetTip(grp, g); ribbon.Children.Add(grp);
            }
            else
            ribbon.Children.Add(new Border { Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10, 6, 10, 4), CornerRadius = new CornerRadius(12), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1),
                Child = new StackPanel { Spacing = 2, Children = { row, new TextBlock { Text = g, Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } } });
        }

        // Arbeitsfläche und Eigenschaften
        var work = new Border { Name = "prog-workspace", ClipToBounds = true, CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Padding = new Thickness(spec.FillHeight ? 8 : 12), MinHeight = spec.FillHeight ? 220 : 420, Child = spec.FillHeight && spec.ScrollWorkspace ? new ScrollViewer { Name = "prog-workspace-scroll", Content = spec.Workspace(d), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled } : spec.Workspace(d) };
        // Paket F (A1 Profi-Bauplan): Werkzeuge links · große Mitte · Eigenschaften rechts · Bereich unten – jeder Rand einklappbar
        bool Hidden(string part) => _settings.HiddenPanels.Contains(key + ":" + part);
        var showTools = spec.Tools.Count > 0 && !Hidden("werkzeuge"); var showProps = spec.Properties is not null && !Hidden("eigenschaften"); var showBottom = spec.Bottom is not null && !Hidden("unten");
        Control center = work;
        if (spec.Bottom is { } bottom && showBottom)
        {
            var bp = new Border { Name = "prog-bottom", Height = bottom.Height, CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Padding = new Thickness(10, 6),
                Child = new DockPanel { Children = { Dk(new TextBlock { Text = bottom.Title, Foreground = Muted, FontSize = 12, FontWeight = FontWeight.SemiBold, Margin = new Thickness(2, 0, 0, 4) }, Avalonia.Controls.Dock.Top), bottom.Build(d) } } };
            var g2 = new Grid { RowDefinitions = new RowDefinitions("*,10,Auto"), Children = { work } }; Grid.SetRow(bp, 2); g2.Children.Add(bp); center = g2;
        }
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions((showTools ? "Auto,10," : "") + (showProps ? "*,12,300" : "*")) };
        var col = 0;
        if (showTools)
        {
            var rail = new StackPanel { Name = "prog-tools", Spacing = 4 };
            foreach (var t in spec.Tools)
            {
                var on = t.Active(); var tt = t;
                var b = new Button { Name = "prog-cmd-" + t.Name, Content = t.Icon, Width = 40, Height = 40, Padding = new Thickness(0), FontSize = 17, CornerRadius = new CornerRadius(10),
                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                    Background = on ? Accent : Brushes.Transparent, Foreground = on ? Brushes.White : Text, BorderBrush = on ? Brushes.Transparent : Edge, BorderThickness = new Thickness(1) };
                ToolTip.SetTip(b, $"{t.Label} – {t.Tip}"); Avalonia.Automation.AutomationProperties.SetName(b, t.Label);
                b.Click += (_, _) => ProgramRun(key, () => tt.Select(d));
                rail.Children.Add(b);
            }
            grid.Children.Add(new Border { Name = "prog-tools-panel", CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Padding = new Thickness(6), Child = rail });
            col = 2;
        }
        grid.Children.Add(Pos(center, col));
        if (spec.Properties is { } props && showProps)
            grid.Children.Add(Pos(new Border { Name = "prog-properties", CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Padding = new Thickness(12),
                Child = spec.FillHeight ? new ScrollViewer { Content = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 8, 0), Children = { H("Eigenschaften", 13), props(d) } } } : new StackPanel { Spacing = 6, Children = { H("Eigenschaften", 13), props(d) } } }, col + 2));

        // Ränder ein- und ausklappen (gemerkt je Programm)
        var toggles = new StackPanel { Name = "prog-panel-toggles", Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(10, 0, 0, 0) };
        void Toggle(string part, string icon, string label, bool on)
        {
            var b = new Button { Name = "prog-toggle-" + part, Content = icon, FontSize = 12, Padding = new Thickness(7, 1), Background = on ? Brush.Parse("#22FFFFFF") : Brushes.Transparent, Foreground = on ? Text : Muted, BorderThickness = new Thickness(0) };
            ToolTip.SetTip(b, (on ? "Einklappen: " : "Ausklappen: ") + label);
            b.Click += (_, _) => { var id = key + ":" + part; if (!_settings.HiddenPanels.Remove(id)) _settings.HiddenPanels.Add(id); SaveSettings(); Show(key); };
            toggles.Children.Add(b);
        }
        if (spec.Tools.Count > 0) Toggle("werkzeuge", "◧", "Werkzeuge", showTools);
        if (spec.Bottom is not null) Toggle("unten", "⬓", spec.Bottom.Title, showBottom);
        if (spec.Properties is not null) Toggle("eigenschaften", "◨", "Eigenschaften", showProps);

        // Statusleiste
        var status = new Border { Name = "prog-status", CornerRadius = new CornerRadius(10), Background = Brush.Parse(C("#331A1A1A", "#14000000")), Padding = new Thickness(12, 5),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Children = {
                new TextBlock { Name = "prog-status-text", Text = spec.Status?.Invoke(d) ?? "", Foreground = Muted, FontSize = 12 },
                Pos(new TextBlock { Text = (d.Path is null ? "Noch nicht gespeichert" : Path.GetFileName(d.Path)) + (d.Dirty ? " · geändert, wird automatisch gesichert" : "") + $" · {d.Changed:HH:mm}", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, 1),
                Pos(toggles, 2) } } };
        return (new StackPanel { Spacing = 0, Children = { tabs, new StackPanel { Spacing = 8, Children = { ribbon } } } }, grid, status);

        Button RBtn(string name, string label, Action go, bool primary = false, string? tip = null)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12.5, Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8), Foreground = primary ? Brushes.White : Text,
                Background = primary ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(primary ? 0 : 1) };
            if (tip is not null) ToolTip.SetTip(b, tip);
            b.Click += (_, _) => go(); return b;
        }
    }

    // ---------- Masterprompt 131: Bedienstufen ----------
    public int ProgramLevel => UserLevel.Of(_settings.Level);

    /// <summary>Bedienstufe wechseln – nur die Werkzeugauswahl ändert sich, Dokumente bleiben unberührt.</summary>
    public void ProgramSetLevel(int level, string? reopen = null)
    {
        _settings.Level = UserLevel.Id(level); SaveSettings();
        _core.Audit.Append(Principal.LocalOwner.Name, "program.level", _settings.Level);
        if (reopen is not null) Show(reopen);
    }

    private Control LevelSwitch(string key)
    {
        var bar = new StackPanel { Name = "prog-level", Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < 3; i++)
        {
            var lv = i; var on = ProgramLevel == lv;
            var b = new Button { Name = "prog-level-" + UserLevel.Ids[i], Content = UserLevel.Names[i], FontSize = 11.5, Padding = new Thickness(9, 3), Margin = new Thickness(0),
                CornerRadius = i == 0 ? new CornerRadius(8, 0, 0, 8) : i == 2 ? new CornerRadius(0, 8, 8, 0) : new CornerRadius(0),
                Background = on ? Brushes.White : Brush.Parse("#26FFFFFF"), Foreground = on ? Brush.Parse("#0B1424") : Brushes.White, BorderThickness = new Thickness(0) };
            ToolTip.SetTip(b, lv switch
            {
                UserLevel.Einfach => "Einfach: nur das Wichtigste, große Schritte",
                UserLevel.Erweitert => "Erweitert: alle Fachwerkzeuge",
                _ => "Professionell: zusätzlich Befehlssuche, Verlauf und Projektdaten",
            });
            b.Click += (_, _) => ProgramSetLevel(lv, key);
            bar.Children.Add(b);
        }
        return bar;
    }

    /// <summary>Profi-Werkzeuge: alle Befehle durchsuchen (auch die der Stufe verborgenen), Verlauf mit Sprung zurück, Projektdaten direkt bearbeiten.</summary>
    private List<Control> ProTools(ProgramSpec spec, ProgramDocument d)
    {
        var key = spec.Key;
        Button Tool(string name, string label, string tip, Control content)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12.5, Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1),
                Flyout = new Flyout { Content = content } };
            ToolTip.SetTip(b, tip); return b;
        }
        // Befehlssuche
        var all = spec.Ribbon.SelectMany(g => g.Cmds.Select(c => (g.Group, Cmd: c))).ToList();
        var q = new TextBox { Name = "prog-palette-q", PlaceholderText = "Befehl suchen …", Width = 300 };
        var hits = new StackPanel { Name = "prog-palette-list", Spacing = 2 };
        void Fill()
        {
            hits.Children.Clear(); var t = (q.Text ?? "").Trim(); var k = 0;
            foreach (var (g, c) in all.Where(x => t.Length == 0 || x.Cmd.Label.Contains(t, StringComparison.OrdinalIgnoreCase) || x.Group.Contains(t, StringComparison.OrdinalIgnoreCase) || (x.Cmd.Tip ?? "").Contains(t, StringComparison.OrdinalIgnoreCase)).Take(12))
            {
                var cmd = c;
                var b = new Button { Name = $"prog-palette-{k++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, Padding = new Thickness(8, 4),
                    Content = new TextBlock { Text = $"{cmd.Label}  ·  {g}", Foreground = Text, FontSize = 12.5 } };
                b.Click += (_, _) => ProgramRun(key, () => cmd.Run(d));
                hits.Children.Add(b);
            }
            if (k == 0) hits.Children.Add(P("Kein Befehl gefunden.", null, 12));
        }
        q.TextChanged += (_, _) => Fill(); Fill();

        // Verlauf
        var hist = new StackPanel { Name = "prog-history-list", Spacing = 2 };
        var steps = d.History;
        if (steps.Count == 0) hist.Children.Add(P("Noch keine Änderung.", null, 12));
        for (var i = 0; i < Math.Min(steps.Count, 25); i++)
        {
            var back = i + 1;
            var b = new Button { Name = $"prog-history-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, Padding = new Thickness(8, 3),
                Content = new TextBlock { Text = steps[i], Foreground = Text, FontSize = 12.5 } };
            ToolTip.SetTip(b, back == 1 ? "Diesen Schritt rückgängig machen" : $"Bis vor diesen Schritt zurück ({back} Schritte)");
            b.Click += (_, _) => ProgramRun(key, () => { for (var n = 0; n < back; n++) d.Undo(); _progMsg[key] = $"{back} Schritt(e) zurück – „Wiederholen“ holt sie zurück."; });
            hist.Children.Add(b);
        }

        // Projektdaten
        var json = new TextBox { Name = "prog-data-text", Text = d.Data.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), AcceptsReturn = true, FontFamily = new FontFamily("Consolas, Cascadia Mono, monospace"), FontSize = 12, Width = 560, Height = 380, TextWrapping = TextWrapping.NoWrap };
        var apply = new Button { Name = "prog-data-apply", Content = "Übernehmen", Background = Accent, Foreground = Brushes.White, Padding = new Thickness(12, 6) };
        apply.Click += (_, _) => ProgramRun(key, () => { d.ReplaceData(json.Text ?? ""); _progMsg[key] = "Projektdaten übernommen – „Rückgängig“ stellt den alten Stand her."; });

        return [
            Tool("prog-palette", "Befehle suchen", "Alle Befehle des Programms durchsuchen und ausführen", new StackPanel { Spacing = 6, Children = { q, hits } }),
            Tool("prog-history", $"Verlauf ({steps.Count})", "Änderungen ansehen und zu einem früheren Stand zurückspringen", new ScrollViewer { MaxHeight = 360, Width = 320, Content = hist }),
            Tool("prog-data", "{ } Projektdaten", "Die gespeicherten Daten des Dokuments direkt bearbeiten", new StackPanel { Spacing = 6, Children = { P("Genau das, was gespeichert wird. Übernehmen ist ein Schritt im Verlauf.", null, 12), json, apply } }),
        ];
    }
}
