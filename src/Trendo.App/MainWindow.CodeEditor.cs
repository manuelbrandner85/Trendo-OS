using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Code-Editor – selbst nachgebaut statt VSCodium (Nachbau-Plan, freigegeben 04.11.2026).
/// Links der Projektbaum, oben Reiter je Datei, in der Mitte der Editor (AvaloniaEdit, MIT: Zeilennummern, Farben je Sprache, Suchen mit Strg+F),
/// unten Ausgabe, Suche in allen Dateien und Git. Strg+S speichert.</summary>
public partial class MainWindow
{
    const string CEK = "code";
    sealed class CeFile(string path, TextDocument doc, string saved) { public string Path = path; public TextDocument Doc = doc; public string Saved = saved; public bool Dirty => Doc.Text != Saved; }
    readonly List<CeFile> _ceFiles = []; int _ceActive = -1; string? _ceRoot; string _ceOut = ""; string _ceTab = "ausgabe"; string _ceQuery = "";
    readonly HashSet<string> _ceCollapsed = new(StringComparer.OrdinalIgnoreCase);
    TextEditor? _ceEditor; static bool _ceDarkDone; readonly List<TextBlock> _ceTabLabels = []; TextBlock? _cePos;

    public string? CodeRoot => _ceRoot;
    public IReadOnlyList<string> CodeOpenFiles => _ceFiles.Select(f => f.Path).ToList();
    public string? CodeActiveFile => _ceActive >= 0 && _ceActive < _ceFiles.Count ? _ceFiles[_ceActive].Path : null;
    public TextEditor? CodeEditorForTests => _ceEditor;
    public bool CodeDirty(string path) => _ceFiles.FirstOrDefault(f => f.Path == path)?.Dirty ?? false;

    private void InitCodeEditor() => RegisterTab(KreativTabs, CEK, "Code-Editor", "CORE-07", CodeEditorView, "code-editor", "code bearbeiten", "quelltext", "editor", "programmieren", "vs code", "vscodium", "syntax");

    /// <summary>Projektordner öffnen (öffentlich für Tests, die Software-Seite und die KI).</summary>
    public void CodeOpenFolder(string dir)
    {
        if (!Directory.Exists(dir)) throw new DirectoryNotFoundException("Ordner nicht gefunden: " + dir);
        _ceRoot = Path.GetFullPath(dir); _ceCollapsed.Clear();
        // Unterordner beim Öffnen zugeklappt – übersichtlich auch bei großen Projekten
        foreach (var d in CodeProject.Tree(_ceRoot).Where(e => e.IsDir && e.Depth >= 0)) _ceCollapsed.Add(d.Path);
        var first = new[] { "README.md", "Program.cs", "main.py", "index.html", "index.js", "app.py" }.Select(n => Path.Combine(_ceRoot, n)).FirstOrDefault(File.Exists);
        if (first is not null && _ceFiles.All(f => f.Path != first)) CodeOpenFile(first);
        _ceOut = $"Projekt „{Path.GetFileName(_ceRoot)}“ geöffnet.";
    }

    public void CodeOpenFile(string file)
    {
        file = Path.GetFullPath(file);
        var i = _ceFiles.FindIndex(f => string.Equals(f.Path, file, StringComparison.OrdinalIgnoreCase));
        if (i < 0) { var text = CodeProject.ReadText(file); _ceFiles.Add(new CeFile(file, new TextDocument(text) { FileName = file }, text)); i = _ceFiles.Count - 1; }
        _ceActive = i;
        _ceRoot ??= Path.GetDirectoryName(file);
    }

    public void CodeSave(bool all = false)
    {
        foreach (var f in all ? _ceFiles : _ceFiles.Skip(Math.Max(0, _ceActive)).Take(_ceActive >= 0 ? 1 : 0))
        {
            if (!f.Dirty) continue;
            CodeProject.WriteText(f.Path, f.Doc.Text); f.Saved = f.Doc.Text;
            _core.Audit.Append(Principal.LocalOwner.Name, "code.save", Path.GetFileName(f.Path));
        }
        _ceOut = all ? "Alle Dateien gespeichert." : CodeActiveFile is { } a ? $"„{Path.GetFileName(a)}“ gespeichert." : "Keine Datei offen.";
    }

    public void CodeClose(int i)
    {
        if (i < 0 || i >= _ceFiles.Count) return;
        if (_ceFiles[i].Dirty) throw new InvalidOperationException($"„{Path.GetFileName(_ceFiles[i].Path)}“ hat ungespeicherte Änderungen – erst speichern (Strg+S) oder „Verwerfen“.");
        _ceFiles.RemoveAt(i); _ceActive = Math.Min(_ceActive, _ceFiles.Count - 1);
    }

    public void CodeNewFile(string relative)
    {
        var root = _ceRoot ?? throw new InvalidOperationException("Erst einen Projektordner öffnen.");
        var full = CodeProject.Inside(root, relative);
        if (File.Exists(full)) throw new InvalidOperationException($"„{relative}“ gibt es schon.");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllText(full, "");
        _ceCollapsed.Remove(Path.GetDirectoryName(full)!);
        CodeOpenFile(full); _ceOut = $"„{relative}“ angelegt.";
    }

    public List<CodeProject.Hit> CodeSearch(string q) { _ceQuery = q; _ceTab = "suche"; return _ceRoot is null ? [] : CodeProject.Search(_ceRoot, q); }

    void CodeRun(bool tests)
    {
        var root = _ceRoot ?? throw new InvalidOperationException("Erst einen Projektordner öffnen.");
        CodeSave(all: true);
        if (_opt.Isolated) { _ceOut = "Testmodus: Trendo startet keine Programme."; _ceTab = "ausgabe"; return; }
        Cli.Result r;
        if (tests) r = DevStudio.RunTests(root);
        else
        {
            var cmd = CodeProject.RunCommand(root, CodeActiveFile ?? root) ?? throw new InvalidOperationException("Für diese Datei kennt Trendo kein „Ausführen“ (Python, JavaScript, PowerShell oder ein .NET-Projekt).");
            r = Cli.Run(cmd.Exe, cmd.Args, cmd.Cwd, 120000);
        }
        _core.Audit.Append(Principal.LocalOwner.Name, tests ? "code.test" : "code.run", $"{Path.GetFileName(root)} exit={r.Exit}");
        _ceOut = (r.Ok ? "Fertig (Exit 0)\n" : $"Exit {r.Exit}\n") + (r.Out + "\n" + r.Err).Trim();
        _ceTab = "ausgabe";
    }

    /// <summary>Farben der mitgelieferten Sprachen für dunklen Hintergrund (Original ist für helle Flächen gedacht).</summary>
    static void CodeDarkColors()
    {
        if (_ceDarkDone) return; _ceDarkDone = true;
        foreach (var def in HighlightingManager.Instance.HighlightingDefinitions.ToList())
        {
            IEnumerable<HighlightingColor> colors;
            try { colors = def.NamedHighlightingColors.ToList(); } catch (HighlightingDefinitionInvalidException) { continue; } // eine mitgelieferte Sprache lädt nicht – überspringen
            foreach (var c in colors)
            {
                var n = c.Name.ToLowerInvariant();
                var hex = n.Contains("comment") ? "#6A9955" : n.Contains("string") || n.Contains("char") || n.Contains("attributevalue") ? "#CE9178" : n.Contains("number") || n.Contains("digit") ? "#B5CEA8"
                    : n.Contains("keyword") || n.Contains("modifier") || n.Contains("access") || n.Contains("visibility") ? "#569CD6" : n.Contains("type") || n.Contains("class") ? "#4EC9B0"
                    : n.Contains("method") || n.Contains("function") ? "#DCDCAA" : n.Contains("control") || n.Contains("goto") || n.Contains("return") || n.Contains("loop") ? "#C586C0"
                    : n.Contains("tag") || n.Contains("element") ? "#569CD6" : n.Contains("attribute") ? "#9CDCFE" : n.Contains("preprocessor") ? "#9B9B9B" : n.Contains("heading") ? "#F5B83D" : "#D4D4D4";
                c.Foreground = new SimpleHighlightingBrush(Color.Parse(hex)); c.Background = null;
            }
        }
    }

    TextEditor CodeEditor()
    {
        if (_ceEditor is not null) { Detach(_ceEditor); return _ceEditor; }
        CodeDarkColors();
        var ed = new TextEditor { Name = "ce-editor", ShowLineNumbers = true, FontFamily = new FontFamily("Cascadia Code, Consolas, Menlo, monospace"), FontSize = 14,
            Background = Brush.Parse("#0E1117"), Foreground = Brush.Parse("#D4D4D4"), LineNumbersForeground = Brush.Parse("#5A6475"), Padding = new Thickness(6, 4),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        ed.Options.ConvertTabsToSpaces = true; ed.Options.IndentationSize = 4; ed.Options.HighlightCurrentLine = true; ed.Options.EnableHyperlinks = false;
        ed.TextArea.SelectionBrush = Brush.Parse("#5533A1F5"); ed.TextArea.TextView.CurrentLineBackground = Brush.Parse("#161B24"); ed.TextArea.TextView.CurrentLineBorder = new Pen(Brushes.Transparent);
        ed.TextChanged += (_, _) => CodeTitleRefresh();
        ed.TextArea.Caret.PositionChanged += (_, _) => { if (_cePos is { } p) p.Text = $"Zeile {ed.TextArea.Caret.Line}, Spalte {ed.TextArea.Caret.Column}"; };
        ed.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.S) { e.Handled = true; Try(CEK, () => { CodeSave(e.KeyModifiers.HasFlag(KeyModifiers.Shift)); Show(CEK); }); }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        return _ceEditor = ed;
    }

    // Punkt am Reiter (ungespeichert) ohne die ganze Seite neu zu zeichnen
    void CodeTitleRefresh()
    {
        for (var i = 0; i < _ceFiles.Count && i < _ceTabLabels.Count; i++)
            _ceTabLabels[i].Text = Path.GetFileName(_ceFiles[i].Path) + (_ceFiles[i].Dirty ? " ●" : "");
    }

    private Control CodeEditorView()
    {
        var gold = Brush.Parse("#F5B83D"); var panelBg = Brush.Parse("#12161E"); var edge = Brush.Parse("#2A3140");
        Button Btn(string name, string label, string tip, Action a, bool primary = false)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12.5, Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(7), Padding = new Thickness(10, 5) };
            if (primary) { b.Background = Accent; b.Foreground = Brushes.White; }
            ToolTip.SetTip(b, tip); b.Click += (_, _) => Try(CEK, () => { a(); Show(CEK); }); return b;
        }
        // Werkzeugleiste
        var bar = new WrapPanel { Name = "ce-toolbar", Margin = new Thickness(0, 0, 0, 8) };
        var folderBtn = new Button { Name = "ce-open-folder", Content = "Ordner öffnen …", FontSize = 12.5, Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(7), Padding = new Thickness(10, 5), Background = Accent, Foreground = Brushes.White };
        folderBtn.Click += async (_, _) =>
        {
            string? dir;
            if (PickOverride is { } fake) dir = (await fake(new PickRequest("code-folder", true, false, []))) is [var f, ..] ? f : null;
            else dir = (await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Projektordner öffnen" })).Select(x => x.TryGetLocalPath()).OfType<string>().FirstOrDefault();
            if (dir is not null) Try(CEK, () => { CodeOpenFolder(dir); Show(CEK); });
        };
        bar.Children.Add(folderBtn);
        var newName = new TextBox { Name = "ce-new-name", PlaceholderText = "Name, z. B. src/hallo.py", Width = 220, FontSize = 12.5, Margin = new Thickness(0, 0, 4, 0) };
        bar.Children.Add(newName);
        bar.Children.Add(Btn("ce-new", "＋ Datei", "Neue Datei im Projekt anlegen", () => CodeNewFile((newName.Text ?? "").Trim() is { Length: > 0 } n ? n : throw new InvalidOperationException("Bitte einen Dateinamen eingeben."))));
        bar.Children.Add(Btn("ce-save", "Speichern", "Strg+S", () => CodeSave()));
        bar.Children.Add(Btn("ce-save-all", "Alle speichern", "Strg+Umschalt+S", () => CodeSave(true)));
        bar.Children.Add(Btn("ce-run", "Ausführen", "Aktive Datei starten (Python, JavaScript, PowerShell) oder dotnet run", () => CodeRun(false), true));
        bar.Children.Add(Btn("ce-test", "Testen", "Tests des Projekts (dotnet build, node --test, unittest)", () => CodeRun(true)));
        bar.Children.Add(Btn("ce-find", "Suchen", "In der Datei: Strg+F. Hier: im ganzen Projekt", () => _ceTab = "suche"));
        bar.Children.Add(Btn("ce-git", "Git", "Änderungen ansehen und als Stand speichern", () => _ceTab = "git"));

        // Links: Projektbaum
        var tree = new StackPanel { Name = "ce-tree", Spacing = 0 };
        if (_ceRoot is null) tree.Children.Add(P("Noch kein Projekt. „Ordner öffnen …“ – oder auf der Software-Seite „Bearbeiten“.", Muted, 12));
        else
        {
            tree.Children.Add(new TextBlock { Text = Path.GetFileName(_ceRoot).ToUpperInvariant(), Foreground = gold, FontSize = 11, FontWeight = FontWeight.SemiBold, Margin = new Thickness(4, 0, 0, 6) });
            var i = 0;
            foreach (var e in CodeProject.Tree(_ceRoot, _ceCollapsed).Take(800))
            {
                var en = e; var open = _ceFiles.Any(f => f.Path == e.Path); var active = CodeActiveFile == e.Path;
                var icon = e.IsDir ? (_ceCollapsed.Contains(e.Path) ? "▸ 📁" : "▾ 📂") : CodeIcon(e.Name);
                var b = new Button { Name = $"ce-node-{i++}", Content = $"{icon}  {e.Name}", Background = active ? Brush.Parse("#2A2410") : Brushes.Transparent, Foreground = active ? gold : open ? Text : Brush.Parse("#B8C0CC"),
                    BorderThickness = new Thickness(0), FontSize = 12.5, Padding = new Thickness(6 + e.Depth * 14, 3, 6, 3), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(5) };
                ToolTip.SetTip(b, Path.GetRelativePath(_ceRoot, e.Path));
                b.Click += (_, _) => Try(CEK, () => { if (en.IsDir) { if (!_ceCollapsed.Remove(en.Path)) _ceCollapsed.Add(en.Path); } else CodeOpenFile(en.Path); Show(CEK); });
                tree.Children.Add(b);
            }
        }
        var left = new Border { Name = "ce-explorer", Width = 250, Background = panelBg, BorderBrush = edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(6, 8), Margin = new Thickness(0, 0, 8, 0),
            Child = new ScrollViewer { Content = tree } };

        // Mitte: Reiter + Editor
        var tabs = new StackPanel { Name = "ce-tabs", Orientation = Orientation.Horizontal }; _ceTabLabels.Clear();
        for (var k = 0; k < _ceFiles.Count; k++)
        {
            var kk = k; var f = _ceFiles[k]; var on = k == _ceActive;
            var tl = new TextBlock { Text = Path.GetFileName(f.Path) + (f.Dirty ? " ●" : ""), VerticalAlignment = VerticalAlignment.Center }; _ceTabLabels.Add(tl);
            var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { tl } };
            var tab = new Button { Name = $"ce-tab-{k}", Content = label, FontSize = 12.5, Padding = new Thickness(12, 6), Margin = new Thickness(0, 0, 2, 0), CornerRadius = new CornerRadius(8, 8, 0, 0),
                Background = on ? Brush.Parse("#0E1117") : Brush.Parse("#1A1F29"), Foreground = on ? Text : Muted, BorderBrush = on ? gold : Brushes.Transparent, BorderThickness = new Thickness(0, 2, 0, 0) };
            tab.Click += (_, _) => { _ceActive = kk; Show(CEK); };
            var close = new Button { Name = $"ce-close-{k}", Content = "×", FontSize = 12, Padding = new Thickness(6, 2), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Muted, VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(close, "Schließen");
            close.Click += (_, _) => Try(CEK, () => { CodeClose(kk); Show(CEK); });
            tabs.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { tab, close } });
        }
        Control center;
        if (CodeActiveFile is { } act)
        {
            var ed = CodeEditor(); var f = _ceFiles[_ceActive];
            if (!ReferenceEquals(ed.Document, f.Doc)) ed.Document = f.Doc;
            var (hl, _) = CodeProject.Language(act);
            try { ed.SyntaxHighlighting = hl is null ? null : HighlightingManager.Instance.GetDefinition(hl); } catch (HighlightingDefinitionInvalidException) { ed.SyntaxHighlighting = null; }
            center = new Border { Child = ed, CornerRadius = new CornerRadius(0, 10, 10, 10), ClipToBounds = true, BorderBrush = edge, BorderThickness = new Thickness(1) };
        }
        else center = new Border { Background = Brush.Parse("#0E1117"), CornerRadius = new CornerRadius(10), BorderBrush = edge, BorderThickness = new Thickness(1),
            Child = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 6, Children = {
                new TextBlock { Text = "</>", FontSize = 46, Foreground = gold, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "Datei links wählen oder einen Ordner öffnen", Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center } } } };
        var mid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        mid.Children.Add(new ScrollViewer { Content = tabs, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Grid.SetRow(center, 1); mid.Children.Add(center);

        // Unten: Ausgabe / Suche / Git
        var bottomTabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        foreach (var (id, lbl) in new[] { ("ausgabe", "Ausgabe"), ("suche", "Suche im Projekt"), ("git", "Git") })
        {
            var on = _ceTab == id; var ii = id;
            var b = new Button { Name = $"ce-panel-{id}", Content = lbl, FontSize = 12, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 4, 0), Background = on ? Brush.Parse("#2A2410") : Brushes.Transparent, Foreground = on ? gold : Muted, BorderThickness = new Thickness(0) };
            b.Click += (_, _) => { _ceTab = ii; Show(CEK); }; bottomTabs.Children.Add(b);
        }
        Control body = _ceTab switch { "suche" => CodeSearchPanel(), "git" => CodeGitPanel(), _ => new SelectableTextBlock { Name = "ce-output", Text = _ceOut.Length == 0 ? "Hier erscheinen Ausgaben von „Ausführen“ und „Testen“." : _ceOut,
            FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"), FontSize = 12, Foreground = _ceOut.StartsWith('✗') ? Brush.Parse("#FCA5A5") : Brush.Parse("#C9D1D9"), TextWrapping = TextWrapping.Wrap } };
        var bottom = new Border { Name = "ce-bottom", Height = 170, Background = panelBg, BorderBrush = edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 6), Margin = new Thickness(0, 8, 0, 0),
            Child = new DockPanel { Children = { bottomTabs, new ScrollViewer { Content = body } } } };
        DockPanel.SetDock(bottomTabs, Avalonia.Controls.Dock.Top);

        // Statusleiste
        var lang = CodeActiveFile is { } af ? CodeProject.Language(af).Label : "–";
        var status = new DockPanel { Margin = new Thickness(2, 6, 2, 0), Children = {
            new TextBlock { Name = "ce-lang", Text = $"{lang} · UTF-8", Foreground = Muted, FontSize = 11.5 },
            (_cePos = new TextBlock { Name = "ce-pos", Text = _ceEditor is { } e2 && CodeActiveFile is not null ? $"Zeile {e2.TextArea.Caret.Line}, Spalte {e2.TextArea.Caret.Column}" : "", Foreground = Muted, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Right }) } };

        var work = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("*,Auto,Auto") };
        work.Children.Add(left); Grid.SetRowSpan(left, 2);
        Grid.SetColumn(mid, 1); work.Children.Add(mid);
        Grid.SetColumn(bottom, 1); Grid.SetRow(bottom, 1); work.Children.Add(bottom);
        Grid.SetColumnSpan(status, 2); Grid.SetRow(status, 2); work.Children.Add(status);
        var head = new StackPanel { Children = { bar } };
        return FitPage("Code-Editor", head, work);
    }

    static string CodeIcon(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".cs" => "🟣", ".py" => "🐍", ".js" or ".mjs" or ".ts" or ".tsx" or ".jsx" => "🟨", ".html" or ".htm" => "🌐", ".css" => "🎨", ".json" => "{ }", ".md" => "📝",
        ".png" or ".jpg" or ".svg" or ".gif" => "🖼", ".csproj" or ".xml" or ".axaml" => "⚙", _ => "📄",
    };

    Control CodeSearchPanel()
    {
        var sp = new StackPanel { Spacing = 4 };
        var q = new TextBox { Name = "ce-search", Text = _ceQuery, PlaceholderText = "Text im ganzen Projekt suchen und Enter", FontSize = 12.5 };
        q.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Try(CEK, () => { CodeSearch(q.Text ?? ""); Show(CEK); }); } };
        sp.Children.Add(q);
        if (_ceRoot is not null && _ceQuery.Length > 0)
        {
            var hits = CodeProject.Search(_ceRoot, _ceQuery);
            sp.Children.Add(P(hits.Count == 0 ? "Nichts gefunden." : $"{hits.Count} Treffer" + (hits.Count >= 300 ? " (die ersten 300)" : ""), Muted, 11.5));
            var i = 0;
            foreach (var h in hits.Take(120))
            {
                var hh = h;
                var b = new Button { Name = $"ce-hit-{i++}", Content = $"{Path.GetRelativePath(_ceRoot, h.File)}:{h.Line}   {(h.Text.Length > 110 ? h.Text[..110] + " …" : h.Text)}", FontSize = 11.5, FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(4, 1), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Foreground = Text };
                b.Click += (_, _) => Try(CEK, () => { CodeOpenFile(hh.File); Show(CEK); CodeGoto(hh.Line); });
                sp.Children.Add(b);
            }
        }
        return sp;
    }

    public void CodeGoto(int line)
    {
        if (_ceEditor is null || _ceEditor.Document is null) return;
        line = Math.Clamp(line, 1, _ceEditor.Document.LineCount);
        _ceEditor.TextArea.Caret.Line = line; _ceEditor.TextArea.Caret.Column = 1; _ceEditor.ScrollToLine(line); _ceEditor.Focus();
    }

    Control CodeGitPanel()
    {
        var sp = new StackPanel { Spacing = 4 };
        if (_ceRoot is null) { sp.Children.Add(P("Erst einen Projektordner öffnen.", Muted, 12)); return sp; }
        if (Cli.Find("git") is null) { sp.Children.Add(P("Git ist nicht eingerichtet – unter Werkzeuge mit einem Klick laden. Ohne Git speichert der Editor ganz normal.", Muted, 12)); return sp; }
        var g = DevStudio.Git(_ceRoot);
        if (!g.IsRepo)
        {
            sp.Children.Add(P("Dieser Ordner hat noch keinen Versionsstand.", Muted, 12));
            var init = new Button { Name = "ce-git-init", Content = "Versionsstand anlegen", FontSize = 12 };
            init.Click += (_, _) => Try(CEK, () => { var r = DevStudio.GitInit(_ceRoot); _ceOut = r.Ok ? "Erster Stand gespeichert." : "Git: " + Short(r.Err + r.Out); Show(CEK); });
            sp.Children.Add(init); return sp;
        }
        sp.Children.Add(P($"Zweig {g.Branch} · {(g.Changed == 0 ? "alles gespeichert" : g.Changed + " geänderte Dateien")} · zuletzt: {g.LastCommit}", Muted, 12));
        foreach (var f in g.Files.Take(30)) sp.Children.Add(new TextBlock { Text = "• " + f, FontSize = 11.5, Foreground = Brush.Parse("#E3B341"), FontFamily = new FontFamily("Cascadia Code, Consolas, monospace") });
        var msg = new TextBox { Name = "ce-commit-msg", PlaceholderText = "Was hast du geändert?", Width = 320, FontSize = 12.5 };
        var commit = new Button { Name = "ce-commit", Content = "Stand speichern", FontSize = 12, Background = Accent, Foreground = Brushes.White, Margin = new Thickness(6, 0, 0, 0) };
        commit.Click += (_, _) => Try(CEK, () => { CodeSave(true); var r = DevStudio.Commit(_ceRoot, msg.Text ?? ""); _ceOut = r.Ok ? "Stand gespeichert." : "Nichts gespeichert. " + Short(r.Err + r.Out); _ceTab = r.Ok ? "git" : "ausgabe"; Show(CEK); });
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { msg, commit } });
        return sp;
    }
}
