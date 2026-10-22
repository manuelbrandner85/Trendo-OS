using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using IO = System.IO;
using Trendo.Core;
using Trendo.Core.PromptAction;
using Trendo.Core.Rebuild;

namespace Trendo.App;

/// <summary>Programm benennen oder beschreiben, Plan abhaken, bauen, in Trendo öffnen, Setup-Datei speichern.</summary>
public partial class MainWindow
{
    private RebuildJob? _job;
    private bool _rebuildBusy;
    private string _rebuildLive = "";
    private string? _setupDir;
    private LoadedLogic? _loaded;
    public RebuildPorts? RebuildOverride { get; set; }
    public ISetupLauncher SetupLauncher { get; set; } = new WindowsSetupLauncher();
    public bool RebuildBusy => _rebuildBusy;

    private T? Named<T>(string name) where T : Control =>
        this.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);

    public void EnsureMyPrograms()
    {
        foreach (var program in MyPrograms.Load(_core.DataRoot))
        {
            if (string.IsNullOrWhiteSpace(program.PageKey)) continue;
            var key = program.PageKey;
            _showMap[key] = () => ProgramView(key);
        }
    }

    partial void AfterPromptResult(string studio, StudioExecutionResult result)
    {
        if (!string.Equals(studio, "X-10", StringComparison.OrdinalIgnoreCase) || _promptProject is null) return;
        try
        {
            var root = _core.Projects.ResolveInProject(_promptProject, "nachbau");
            var job = RebuildEngine.TryLoad(root);
            if (job is null) return;
            _job = job;
            _cleanOpen = root;
            if (job.Built)
            {
                MyPrograms.Upsert(_core.DataRoot, job.Blueprint.OwnName, root, job.Blueprint.NameReason);
                EnsureMyPrograms();
            }
        }
        catch (Exception) { return; }
        if (string.Equals(CurrentView, "nachbau", StringComparison.OrdinalIgnoreCase)) Show("nachbau");
    }

    private RebuildPorts Ports()
    {
        if (RebuildOverride is not null) return RebuildOverride;
        return new RebuildPorts
        {
            Web = new WikipediaResearch(_core.Offline),
            Author = _opt.Isolated ? new NoAuthor() : new LocalAuthor(Intel),
            Build = new DotnetRunner(),
            Packer = new VelopackPacker(new DotnetRunner())
        };
    }

    private void RunRebuild(string live, Action work)
    {
        if (_rebuildBusy) return;
        _rebuildBusy = true;
        _rebuildLive = live;
        Show("nachbau");
        Task.Run(() =>
        {
            var remember = false;
            try
            {
                work();
                remember = _job?.Built == true;
            }
            catch (Exception ex)
            {
                _rebuildLive = ex.Message;
                if (_job is not null)
                {
                    _job.Log.Add(ex.Message);
                    try { RebuildEngine.Save(_job); } catch (Exception) { }
                }
            }
            finally
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _rebuildBusy = false;
                    if (remember && _job is not null)
                    {
                        MyPrograms.Upsert(_core.DataRoot, _job.Blueprint.OwnName, _job.Root, _job.Blueprint.NameReason);
                        EnsureMyPrograms();
                    }
                    Show("nachbau");
                });
            }
        });
    }

    private void RebuildSection(StackPanel sp)
    {
        if (_cleanOpen is not null && (_job is null || !string.Equals(_job.Root, _cleanOpen, StringComparison.OrdinalIgnoreCase)))
            _job = RebuildEngine.TryLoad(_cleanOpen);

        var prompt = new TextBox
        {
            Name = "rebuild-prompt",
            PlaceholderText = "z. B. ein Notizprogramm wie Notepad++ oder so etwas wie Paint",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 64,
            FontSize = 14
        };
        var web = new ToggleSwitch { Name = "rebuild-web", IsChecked = false, OnContent = "an", OffContent = "aus" };
        Avalonia.Automation.AutomationProperties.SetName(web, "Im Internet recherchieren");
        if (_core.Offline)
        {
            web.IsEnabled = false;
            web.IsChecked = false;
            ToolTip.SetTip(web, "Offline: Recherche bleibt aus.");
        }
        var plan = new Button { Name = "rebuild-plan", Content = "Plan erstellen", MinHeight = 36, Background = Accent, Foreground = Brushes.White, IsEnabled = !_rebuildBusy };
        plan.Click += (_, _) =>
        {
            if (_rebuildBusy) return;
            var text = prompt.Text?.Trim() ?? "";
            if (text.Length == 0) text = Named<TextBox>("clean-original")?.Text?.Trim() ?? "";
            if (text.Length == 0) { Say("Beschreibe zuerst das Programm.", "nachbau"); return; }
            var research = web.IsChecked == true;
            RunRebuild("Der Plan wird erstellt …", () =>
            {
                var (name, _) = OwnName.Choose(text, null);
                var dir = IO.Path.Combine(CleanFolder, DevStudio.SafeName(name));
                if (!IO.Directory.Exists(dir))
                {
                    try { CleanRoom.Create(CleanFolder, name, text); } catch (IOException) { }
                }
                _job = RebuildEngine.Plan(dir, text, research, Ports());
                _cleanOpen = dir;
                _rebuildLive = "";
            });
        };
        var ask = new StackPanel { Spacing = 8, Children = {
            H("Programm beschreiben", 16),
            P("Eine Cloud-KI wird nicht benutzt. Die lokale KI hilft nur, wenn sie auf diesem PC läuft."),
            prompt,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { web, new TextBlock { Text = "Im Internet recherchieren", Foreground = Text, VerticalAlignment = VerticalAlignment.Center } } },
            plan,
            P(_core.Offline ? "Offline: Recherche bleibt aus." : "Die Recherche ist aus, bis du den Schalter setzt. Es wird nichts aus dem Internet ausgeführt.", null, 11)
        } };
        if (_rebuildLive.Length > 0) ask.Children.Add(new TextBlock { Name = "rebuild-live", Text = _rebuildLive, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(Card(ask));

        if (_job is null) return;
        var job = _job;
        var head = new StackPanel { Spacing = 4, Children = {
            H(job.Blueprint.OwnName, 16),
            P(job.Blueprint.NameReason, Text, 12)
        } };
        head.Children[1].Name = "rebuild-reason";
        if (job.Sources.Count == 0) head.Children.Add(P("Keine Quellen. Der Plan kommt aus deiner Beschreibung.", null, 11));
        for (var i = 0; i < job.Sources.Count; i++)
        {
            var src = job.Sources[i];
            head.Children.Add(new TextBlock
            {
                Name = "rebuild-source-" + i,
                Text = src.Title + "\n" + src.Url + "\n" + src.Chars + " Zeichen, SHA-256 " + src.Sha256,
                Foreground = Muted,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
        }
        foreach (var feature in job.Blueprint.Features)
        {
            var box = new CheckBox
            {
                Name = "rebuild-feat-" + feature.Id,
                IsChecked = feature.On,
                Content = PrioText.Of(feature.Priority) + " · " + feature.Title,
                IsEnabled = !_rebuildBusy
            };
            ToolTip.SetTip(box, feature.Detail);
            head.Children.Add(box);
        }
        var go = new Button { Name = "rebuild-go", Content = "Los", MinHeight = 36, Background = Accent, Foreground = Brushes.White, IsEnabled = !_rebuildBusy };
        go.Click += (_, _) =>
        {
            if (_rebuildBusy || _job is null) return;
            var ticks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var feature in _job.Blueprint.Features)
            {
                var box = Named<CheckBox>("rebuild-feat-" + feature.Id);
                ticks[feature.Id] = box?.IsChecked == true;
            }
            RunRebuild("Es wird gebaut …", () =>
            {
                _job = RebuildEngine.ApplyTicks(_job!, ticks);
                _job = RebuildEngine.Build(_job, Ports());
                _rebuildLive = "";
            });
        };
        head.Children.Add(go);
        var preview = new StackPanel { Spacing = 2, Children = { H("Vorschau", 14), P(job.Blueprint.OwnName, Text, 13) } };
        foreach (var feature in job.Blueprint.Features.Where(f => f.On))
            preview.Children.Add(P("• " + feature.Title, null, 12));
        preview.Children.Add(P("Knöpfe: " + job.Blueprint.ButtonHeight + " Pixel, Schrift " + job.Blueprint.FontSize + ".", null, 11));
        head.Children.Add(new Border { Name = "rebuild-preview", Background = Brush.Parse("#22000000"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Child = preview });
        var log = new TextBlock { Name = "rebuild-log", Text = string.Join("\n", job.Log.TakeLast(12)), Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap };
        head.Children.Add(log);
        sp.Children.Add(Card(head));

        var open = new Button { Name = "rebuild-open", Content = "In Trendo öffnen", MinHeight = 36, IsEnabled = !_rebuildBusy };
        open.Click += (_, _) =>
        {
            if (_job is null) return;
            var program = MyPrograms.Upsert(_core.DataRoot, _job.Blueprint.OwnName, _job.Root, _job.Blueprint.NameReason);
            EnsureMyPrograms();
            Show(program.PageKey);
        };
        var folder = new TextBox { Name = "rebuild-setup-dir", PlaceholderText = "Ordner für die Setup-Datei", Text = _setupDir ?? "", MinWidth = 220 };
        var pick = new Button { Name = "rebuild-setup-pick", Content = "Ordner wählen", MinHeight = 36 };
        pick.Click += async (_, _) =>
        {
            try
            {
                var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Ordner für die Setup-Datei", AllowMultiple = false });
                var path = picked.FirstOrDefault()?.TryGetLocalPath();
                if (!string.IsNullOrWhiteSpace(path)) { _setupDir = path; Show("nachbau"); }
            }
            catch (Exception)
            {
                Say("Den Ordner bitte in das Feld schreiben.", "nachbau");
            }
        };
        var setup = new Button { Name = "rebuild-setup", Content = "Als Setup-Datei erstellen", MinHeight = 36, IsEnabled = !_rebuildBusy && job.Built };
        setup.Click += (_, _) =>
        {
            if (_rebuildBusy || _job is null) return;
            var dest = Named<TextBox>("rebuild-setup-dir")?.Text?.Trim() ?? "";
            if (dest.Length == 0) { Say("Wähle zuerst einen Ordner für die Setup-Datei.", "nachbau"); return; }
            RunRebuild("Die Setup-Datei wird erstellt …", () =>
            {
                _setupDir = dest;
                _job = RebuildEngine.Pack(_job!, dest, Ports(), true);
                _rebuildLive = "";
            });
        };
        var signed = new TextBlock { Name = "rebuild-unsigned", Text = "Die Setup-Datei ist nicht signiert. Windows fragt beim Installieren selbst nach. Trendo installiert sie nicht von allein.", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap };
        if (!string.IsNullOrWhiteSpace(job.SetupNote)) signed.Text = job.SetupNote;
        var installOk = new CheckBox { Name = "rebuild-install-ok", Content = "Ich will diese Datei jetzt installieren" };
        var install = new Button { Name = "rebuild-install", Content = "Installieren", MinHeight = 36 };
        install.Click += (_, _) =>
        {
            var confirmed = Named<CheckBox>("rebuild-install-ok")?.IsChecked == true;
            var path = _job?.SetupPath ?? "";
            var block = InstallGate.Block(confirmed, OperatingSystem.IsWindows(), IO.File.Exists(path));
            if (block is not null) { Say(block, "nachbau"); return; }
            try
            {
                SetupLauncher.Start(path);
                Say("Windows fragt jetzt selbst nach. Die Datei ist nicht signiert.", "nachbau");
            }
            catch (Exception ex) { Say("Die Installation wurde nicht gestartet: " + ex.Message, "nachbau"); }
        };
        var use = new StackPanel { Spacing = 8, Children = {
            H("Benutzen", 16),
            new WrapPanel { Orientation = Orientation.Horizontal },
            signed,
            new WrapPanel { Children = { installOk, install } }
        } };
        ((WrapPanel)use.Children[1]).Children.Add(open);
        ((WrapPanel)use.Children[1]).Children.Add(folder);
        ((WrapPanel)use.Children[1]).Children.Add(pick);
        ((WrapPanel)use.Children[1]).Children.Add(setup);
        sp.Children.Add(Card(use));

        var change = new TextBox { Name = "rebuild-change", PlaceholderText = "z. B. mach die Knöpfe größer", MinHeight = 36 };
        var revise = new Button { Name = "rebuild-revise", Content = "Ändern", MinHeight = 36, IsEnabled = !_rebuildBusy };
        revise.Click += (_, _) =>
        {
            if (_rebuildBusy || _job is null) return;
            var text = Named<TextBox>("rebuild-change")?.Text ?? "";
            var again = _job.Built;
            RunRebuild("Die Änderung wird übernommen …", () =>
            {
                _job = RebuildEngine.Revise(_job!, text, Ports(), again);
                _rebuildLive = "";
            });
        };
        var undo = new Button { Name = "rebuild-undo", Content = "Rückgängig", MinHeight = 36, IsEnabled = !_rebuildBusy };
        undo.Click += (_, _) =>
        {
            if (_rebuildBusy || _job is null) return;
            _job = RebuildEngine.Undo(_job);
            _rebuildLive = "";
            Show("nachbau");
        };
        var redo = new Button { Name = "rebuild-redo", Content = "Wiederholen", MinHeight = 36, IsEnabled = !_rebuildBusy };
        redo.Click += (_, _) =>
        {
            if (_rebuildBusy || _job is null) return;
            _job = RebuildEngine.Redo(_job);
            _rebuildLive = "";
            Show("nachbau");
        };
        var version = new TextBlock { Name = "rebuild-version", Text = "Fassung " + job.Version + (job.Latest > job.Version ? " von " + job.Latest : ""), Foreground = Text, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(Card(new StackPanel { Spacing = 8, Children = {
            H("Nachbessern", 16),
            P("Eine Änderung trifft nur die genannte Stelle. Jede Fassung lässt sich zurücknehmen."),
            change,
            new WrapPanel { Children = { revise, undo, redo, version } }
        } }));
    }

    private Control ProgramView(string page)
    {
        var program = MyPrograms.FindByPage(_core.DataRoot, page);
        if (program is null) return Page("Programm", P("Dieses Programm ist nicht mehr eingetragen."));
        var job = RebuildEngine.TryLoad(program.ProjectDir);
        if (job is null) return Page(program.Name, P("Der Plan zu diesem Programm fehlt."));
        LoadedLogic? logic = null;
        string banner;
        if (job.Built)
        {
            logic = RebuildHost.TryLoad(ProjectWriter.KernDll(job.Root));
            banner = logic is null
                ? "Die Bibliothek ließ sich nicht laden. Die Ansicht folgt dem bestätigten Plan."
                : "Läuft mit der gebauten Bibliothek RebuildKern.dll.";
        }
        else banner = "Die Ansicht folgt dem bestätigten Plan. Diese Fassung ist noch nicht gebaut.";
        if (_loaded is not null && !ReferenceEquals(_loaded, logic)) _loaded.Release();
        _loaded = logic;
        var bp = job.Blueprint;
        var height = logic?.ButtonHeight ?? bp.ButtonHeight;
        var font = logic?.FontSize ?? bp.FontSize;
        var dark = logic?.Dark ?? bp.Dark;
        var title = logic is { Title.Length: > 0 } ? logic.Title : bp.OwnName;
        var kind = logic is { Kind.Length: > 0 } ? logic.Kind : bp.Kind;
        bool Has(string id) => logic?.Has(id) ?? bp.Features.Any(f => f.On && string.Equals(f.Id, id, StringComparison.Ordinal));
        var dir = IO.Path.Combine(job.Root, "daten");
        IO.Directory.CreateDirectory(dir);
        string Load() => logic is not null ? logic.Load(dir) : IO.File.Exists(IO.Path.Combine(dir, "zettel.txt")) ? IO.File.ReadAllText(IO.Path.Combine(dir, "zettel.txt")) : "";
        void Save(string text)
        {
            if (logic is not null) logic.Save(dir, text);
            else { IO.Directory.CreateDirectory(dir); IO.File.WriteAllText(IO.Path.Combine(dir, "zettel.txt"), text ?? ""); }
        }
        int Count(string text, string term) => logic?.Count(text, term) ?? CountLocal(text, term);

        var fg = Brush.Parse(dark ? "#EEF3FB" : "#1B2433");
        var muted = Brush.Parse(dark ? "#9DB0CF" : "#5C6B80");
        var bg = Brush.Parse(dark ? "#101826" : "#F4F7FB");
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(new Border { Name = "prog-banner", Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Child = P(banner, Text, 12) });
        body.Children.Add(P(bp.NameReason, muted, 12));
        var host = new Border { Name = "prog-host", Background = bg, CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Child = kind == "zeichnen" ? PaintHost(fg, muted, height, Has) : kind == "notizen" ? NotesHost(fg, height, font, Has, Load, Save, Count) : GeneralHost(fg, height, font, Has, Load, Save, logic) };
        body.Children.Add(host);
        return Page(title, body);
    }

    private Control NotesHost(IBrush fg, int height, int font, Func<string, bool> has, Func<string> load, Action<string> save, Func<string, string, int> count)
    {
        var box = new TextBox { Name = "prog-text", AcceptsReturn = true, MinHeight = 180, TextWrapping = TextWrapping.Wrap, FontSize = font, Text = has("speichern") ? load() : "" };
        var row = new WrapPanel();
        var status = new TextBlock { Name = "prog-status", Foreground = fg, TextWrapping = TextWrapping.Wrap };
        if (has("speichern"))
        {
            var b = ActionButton("Speichern", height, font);
            b.Click += (_, _) => { save(box.Text ?? ""); status.Text = "Gespeichert."; };
            row.Children.Add(b);
            var o = ActionButton("Öffnen", height, font);
            o.Click += (_, _) => { box.Text = load(); status.Text = "Geladen."; };
            row.Children.Add(o);
        }
        if (has("suchen"))
        {
            var q = new TextBox { Name = "prog-query", PlaceholderText = "Suchwort", Width = 140, MinHeight = height, FontSize = font };
            var b = ActionButton("Suchen", height, font);
            b.Click += (_, _) => status.Text = count(box.Text ?? "", q.Text ?? "") + " Treffer";
            row.Children.Add(q);
            row.Children.Add(b);
        }
        if (has("schrift"))
        {
            var up = ActionButton("Größer", height, font);
            up.Click += (_, _) => box.FontSize = Math.Clamp(box.FontSize + 2, 12, 36);
            row.Children.Add(up);
        }
        var panel = new StackPanel { Spacing = 8 };
        if (has("schreiben") || has("speichern")) panel.Children.Add(box);
        if (has("zeilen"))
        {
            var lines = new TextBlock { Foreground = fg };
            void Update() => lines.Text = "Zeilen: " + (box.Text ?? "").Replace("\r\n", "\n").Split('\n').Length;
            box.TextChanged += (_, _) => Update();
            Update();
            panel.Children.Add(lines);
        }
        panel.Children.Add(row);
        panel.Children.Add(status);
        return panel;
    }

    private Control PaintHost(IBrush fg, IBrush muted, int height, Func<string, bool> has)
    {
        var canvas = new Canvas { Name = "prog-canvas", Width = 520, Height = 220, Background = Brush.Parse("#0B1220") };
        var row = new WrapPanel();
        if (has("farben")) row.Children.Add(ActionButton("Blau", height, 14));
        if (has("leeren"))
        {
            var clear = ActionButton("Leeren", height, 14);
            clear.Click += (_, _) => canvas.Children.Clear();
            row.Children.Add(clear);
        }
        canvas.PointerPressed += (_, e) =>
        {
            var p = e.GetPosition(canvas);
            var dot = new Ellipse { Width = 8, Height = 8, Fill = Brush.Parse("#7DD3FC") };
            Canvas.SetLeft(dot, p.X);
            Canvas.SetTop(dot, p.Y);
            canvas.Children.Add(dot);
        };
        return new StackPanel { Spacing = 8, Children = { canvas, row, new TextBlock { Text = "Die Fläche gehört zu dieser App.", Foreground = muted, TextWrapping = TextWrapping.Wrap } } };
    }

    private Control GeneralHost(IBrush fg, int height, int font, Func<string, bool> has, Func<string> load, Action<string> save, LoadedLogic? logic)
    {
        var box = new TextBox { Name = "prog-text", PlaceholderText = "Dein Text", MinHeight = height, FontSize = font, Text = has("speichern") ? load() : "" };
        var status = new TextBlock { Name = "prog-status", Foreground = fg, TextWrapping = TextWrapping.Wrap };
        var row = new WrapPanel();
        if (has("gruss"))
        {
            var b = ActionButton("Zeigen", height, font);
            b.Click += (_, _) => status.Text = logic is not null ? logic.Greet(box.Text) : string.IsNullOrWhiteSpace(box.Text) ? "Hallo." : "Hallo " + box.Text.Trim() + ".";
            row.Children.Add(b);
        }
        if (has("speichern"))
        {
            var b = ActionButton("Speichern", height, font);
            b.Click += (_, _) => { save(box.Text ?? ""); status.Text = "Gespeichert."; };
            row.Children.Add(b);
        }
        return new StackPanel { Spacing = 8, Children = { box, row, status } };
    }

    private Button ActionButton(string label, int height, int font) => new()
    {
        Name = "prog-action",
        Content = label,
        MinHeight = height,
        MinWidth = 96,
        Margin = new Thickness(0, 0, 8, 8),
        Background = Accent,
        Foreground = Brushes.White,
        FontSize = font
    };

    private static int CountLocal(string? text, string? term)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term)) return 0;
        var n = 0;
        var i = 0;
        while ((i = text.IndexOf(term, i, StringComparison.OrdinalIgnoreCase)) >= 0) { n++; i += term.Length; }
        return n;
    }
}

sealed class LocalAuthor(IntelligenceCore core) : ILocalAuthor
{
    public bool Available => true;
    public string? Ask(string system, string user)
    {
        try
        {
            var answer = core.AskAsync(new KiRequest(user, system, UseKnowledge: false)).ConfigureAwait(false).GetAwaiter().GetResult();
            return answer.Ok ? answer.Text : null;
        }
        catch (Exception) { return null; }
    }
}
