using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Trendo.Core;
using Trendo.Core.ConsolePort;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Spiele-Bibliothek im Konsolen-Studio: Karte wählen, bauen oder holen, spielen, ändern. Bestehende kommerzielle Spiele stehen nicht im Katalog.</summary>
public partial class MainWindow
{
    string _libKind = "";
    static readonly Dictionary<string, Bitmap?> LibraryCovers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Für Tests: Programmstart nachbilden (Programm, Argumente).</summary>
    public Func<string, IReadOnlyList<string>, bool>? LibStartOverride { get; set; }
    /// <summary>Für Tests: Downloads nachbilden.</summary>
    public IWebFetch? LibFetchOverride { get; set; }
    /// <summary>Für Tests: Entpacken nachbilden (Archiv, Ziel).</summary>
    public Func<string, string, bool>? LibExtractOverride { get; set; }
    /// <summary>Für Tests: Godot vorgeben.</summary>
    public string? LibGodotOverride { get; set; }

    static readonly HttpFetch LibHttp = new(new HttpClient { Timeout = TimeSpan.FromMinutes(5) });

    async Task<string?> LibDownload(string url, string sha256, string dest)
    {
        try { return await GameLibrary.DownloadAsync(LibFetchOverride ?? LibHttp, url, sha256, dest); }
        catch (HttpRequestException) { return "Der Download ist fehlgeschlagen. Ist das Internet erreichbar?"; }
    }

    internal static Bitmap? LibraryCover(string id)
    {
        lock (LibraryCovers)
        {
            if (LibraryCovers.TryGetValue(id, out var cached)) return cached;
            Bitmap? bmp = null;
            try { using var st = AssetLoader.Open(new Uri($"avares://TrendoOS/Assets/bibliothek/{id}.jpg")); bmp = Bitmap.DecodeToWidth(st, 384); }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or ArgumentException or NotSupportedException or IOException) { }
            LibraryCovers[id] = bmp;
            return bmp;
        }
    }

    string LibHomebrewDir => Path.Combine(_core.DataRoot, "konsolenspiele", "homebrew");
    string LibMgbaDir => Path.Combine(_core.DataRoot, "konsolenspiele", "mgba");
    static GameLibrary.Entry? LibEntry(ProgramDocument d) => d.Text("lib") == GameLibrary.WishId ? GameLibrary.Wish(d.Text("wish")) : GameLibrary.Find(d.Text("lib"));
    string LibProject(ProgramDocument d, GameLibrary.Entry e) => GameLibrary.ProjectDir(ConsoleProject(d), e);

    Control LibraryView(ProgramDocument d)
    {
        var e = LibEntry(d);
        return e is null ? LibraryGrid(d) : LibraryDetail(d, e);
    }

    Control LibraryGrid(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "lib-view", Spacing = 10, Margin = new Thickness(4, 0, 14, 4) };
        sp.Children.Add(H("Was möchtest du spielen?", 13));
        var wish = new TextBox { Name = "lib-wish", Text = d.Text("wish"), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 64, MaxLength = GameLibrary.MaxWish, PlaceholderText = "Zum Beispiel: große Stadt erkunden, Autos fahren, Aufträge und eine Fahndungsstufe" };
        sp.Children.Add(wish);
        var go = new Button { Name = "lib-wish-go", Content = "Spiel daraus erfinden", Padding = new Thickness(14, 6) };
        go.Classes.Add("accent");
        go.Click += (_, _) => LibWishPick(d, wish.Text);
        sp.Children.Add(go);
        sp.Children.Add(P("Claude übernimmt aus deinem Wunsch Genre und Spielprinzip. Namen, Figuren, Karten und Grafik bestehender Spiele werden nicht nachgebaut, alles wird neu erfunden.", Muted, 11));

        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var (id, label) in new[] { ("", "Alle"), ("Eigen", "Eigene Spiele"), ("Frei", "Freie Spiele"), ("Homebrew", "Homebrew") })
        {
            var c = new Button { Name = "lib-kind-" + (id.Length == 0 ? "alle" : id), Content = label, Padding = new Thickness(12, 4) };
            if (_libKind == id) c.Classes.Add("accent");
            c.Click += (_, _) => { _libKind = id; Show(ConsoleKey); };
            chips.Children.Add(c);
        }
        sp.Children.Add(chips);

        var grid = new WrapPanel { Name = "lib-grid" };
        foreach (var en in GameLibrary.Entries.Where(x => _libKind.Length == 0 || x.Kind.ToString() == _libKind)) grid.Children.Add(LibraryCard(d, en));
        sp.Children.Add(grid);
        return sp;
    }

    Control LibraryCard(ProgramDocument d, GameLibrary.Entry e)
    {
        var face = new Panel { Width = 158, Height = 222, Background = Brush.Parse("#1D2230") };
        if (LibraryCover(e.Id) is { } pic) face.Children.Add(new Image { Source = pic, Stretch = Stretch.UniformToFill });
        var card = new Button { Name = "lib-card-" + e.Id, Width = 174, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(8), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        card.Content = new StackPanel { Spacing = 3, Children = { face, new TextBlock { Text = e.Title, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }, new TextBlock { Text = e.Genre, Foreground = Muted, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis }, new TextBlock { Text = GameLibrary.KindLabel(e.Kind), Foreground = Muted, FontSize = 10 } } };
        card.Click += (_, _) => ProgramRun(ConsoleKey, () => d.Set("lib", e.Id, "Spiel gewählt"));
        return card;
    }

    void LibWishPick(ProgramDocument d, string? text)
    {
        if (GameLibrary.CheckWish(text) is { } problem) { _progMsg[ConsoleKey] = problem; Show(ConsoleKey); return; }
        ProgramRun(ConsoleKey, () => { d.Set("wish", text!.Trim(), "Wunsch"); d.Set("lib", GameLibrary.WishId, "Spiel gewählt"); });
    }

    Control LibraryDetail(ProgramDocument d, GameLibrary.Entry e)
    {
        var info = new StackPanel { Spacing = 6, MaxWidth = 620 };
        var back = new Button { Name = "lib-back", Content = "Zurück zur Bibliothek", Padding = new Thickness(10, 4) };
        back.Click += (_, _) => ProgramRun(ConsoleKey, () => d.Set("lib", "", "Bibliothek"));
        info.Children.Add(back);
        info.Children.Add(new TextBlock { Name = "lib-title", Text = e.Title, Foreground = Text, FontSize = 20, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        info.Children.Add(P($"{GameLibrary.KindLabel(e.Kind)} · {e.Genre} · {e.Style}", Muted, 12));
        info.Children.Add(new TextBlock { Name = "lib-description", Text = e.Description, Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        foreach (var f in e.Features) info.Children.Add(P("• " + f, Muted, 12));
        info.Children.Add(P("Lizenz: " + e.License + (e.Author is null ? "" : " · von " + e.Author), Muted, 11));

        if (e.Kind == GameLibrary.Kind.Homebrew) LibHomebrewActions(info, e);
        else LibGameActions(info, d, e);

        var row = new DockPanel { Name = "lib-detail", Margin = new Thickness(4, 0, 14, 4) };
        var cover = new Panel { Width = 240, Height = 336, Background = Brush.Parse("#1D2230"), Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Top };
        if (LibraryCover(e.Id) is { } pic) cover.Children.Add(new Image { Source = pic, Stretch = Stretch.UniformToFill });
        row.Children.Add(Dk(cover, Avalonia.Controls.Dock.Left));
        row.Children.Add(info);
        return row;
    }

    void LibGameActions(StackPanel info, ProgramDocument d, GameLibrary.Entry e)
    {
        var project = LibProject(d, e);
        var built = GameLibrary.IsBuilt(project);
        info.Children.Add(new TextBlock { Name = "lib-state", Text = built ? "✓ Projekt vorhanden" : "Noch nicht auf diesem PC", Foreground = Muted, FontSize = 12 });
        var main = new Button { Name = e.Kind == GameLibrary.Kind.Frei ? "lib-fetch" : "lib-build", Content = e.Kind == GameLibrary.Kind.Frei ? (built ? "Neu holen" : "Spiel holen") : (built ? "Weiterbauen lassen" : "Spiel bauen lassen"), Padding = new Thickness(14, 6) };
        if (!built) main.Classes.Add("accent");
        main.Click += async (_, _) => { if (e.Kind == GameLibrary.Kind.Frei) await LibFetch(d, e); else await LibSend(GameLibrary.BuildPrompt(e, project), project); };
        info.Children.Add(main);
        if (!built) return;

        var play = new Button { Name = "lib-play", Content = "Spielen", Padding = new Thickness(14, 6) };
        play.Classes.Add("accent");
        play.Click += (_, _) => LibPlayGodot(project);
        info.Children.Add(play);

        if (LibGodot() is null)
        {
            info.Children.Add(P("Godot 4 wurde nicht gefunden. Wähle einmal die Datei Godot_v4…, dann merkt sich Trendo den Ort.", Muted, 11));
            var pickGodot = new Button { Name = "lib-godot-pick", Content = "Godot wählen", Padding = new Thickness(12, 5) };
            pickGodot.Click += async (_, _) => { if (await PickMediaFile("lib-godot", "Godot 4", [OperatingSystem.IsWindows() ? "Godot_v4*.exe" : "Godot_v4*"]) is { } f) LibSetGodot(f); };
            info.Children.Add(pickGodot);
        }

        info.Children.Add(H("Ändern, umbauen, erweitern", 13));
        var change = new TextBox { Name = "lib-change", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 56, MaxLength = GameLibrary.MaxWish, PlaceholderText = "Zum Beispiel: ein zweiter Stadtteil und Regen in der Nacht" };
        info.Children.Add(change);
        var changeGo = new Button { Name = "lib-change-go", Content = "Änderung bauen lassen", Padding = new Thickness(12, 5) };
        changeGo.Click += async (_, _) =>
        {
            if (GameLibrary.CheckWish(change.Text) is { } problem) { _progMsg[ConsoleKey] = problem; Show(ConsoleKey); return; }
            await LibSend(GameLibrary.ChangePrompt(e, change.Text!, project), project);
        };
        info.Children.Add(changeGo);

        info.Children.Add(H("Für andere Geräte", 13));
        var targets = new WrapPanel();
        foreach (var t in GameLibrary.Targets)
        {
            var b = new Button { Name = "lib-export-" + t.Id, Content = t.Label, Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 6, 6) };
            b.Click += async (_, _) => await LibSend(GameLibrary.ExportPrompt(e, t.Id, project), project);
            targets.Children.Add(b);
        }
        info.Children.Add(targets);
    }

    void LibHomebrewActions(StackPanel info, GameLibrary.Entry e)
    {
        info.Children.Add(P($"Beim Klick lädt Trendo dieses frei verteilte Spiel von {new Uri(e.Url!).Host} und, falls nötig, den Emulator mGBA (MPL-2.0, etwa 14 MB). Beide werden über feste Prüfsummen kontrolliert.", Muted, 11));
        var play = new Button { Name = "lib-homebrew-play", Content = "Holen und spielen", Padding = new Thickness(14, 6) };
        play.Classes.Add("accent");
        play.Click += async (_, _) => await LibPlayHomebrew(e);
        info.Children.Add(play);
    }

    /// <summary>Auftrag an Claude im Studio geben. false: geht gerade nicht, der Grund steht im Hinweis.</summary>
    public async Task<bool> LibSend(string prompt, string project)
    {
        LastConsolePrompt = prompt;
        Directory.CreateDirectory(project);
        _core.Audit.Append(Principal.LocalOwner.Name, "bibliothek.claude", prompt.Split('\n')[0]);
        if (ConsoleSendOverride is { } fake) return fake(prompt);
        if (await ClaudeFromStudioBar("STUDIO-CONSOLE-01", prompt, t => _progMsg[ConsoleKey] = t)) return true;
        _progMsg[ConsoleKey] = "Dafür muss Claude verbunden sein. Das richtest du unter „Mit Claude verbinden“ ein."; Show(ConsoleKey);
        return false;
    }

    /// <summary>Holt ein freies Spiel in fester Fassung.</summary>
    public async Task<bool> LibFetch(ProgramDocument d, GameLibrary.Entry e)
    {
        var root = Path.Combine(ConsoleProject(d), e.Id);
        if (Directory.Exists(root)) LibDeleteTree(root);
        Directory.CreateDirectory(root);
        foreach (var args in GameLibrary.FetchSteps(e))
        {
            var ok = ConsoleGitOverride is { } fake ? fake(args, root) : await Task.Run(() => Cli.Find("git") is { } git && Cli.Run(git, args, root, 300000).Ok);
            if (ok) continue;
            _progMsg[ConsoleKey] = "Das Spiel ließ sich nicht laden. Ist Git installiert und das Internet erreichbar?"; Show(ConsoleKey); return false;
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "deps.git", e.Id + " " + GameLibrary.DemoCommit[..8]);
        Show(ConsoleKey);
        return true;
    }

    /// <summary>Git legt Dateien schreibgeschützt ab; ohne das Aufheben scheitert das Löschen unter Windows.</summary>
    static void LibDeleteTree(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(dir, true);
    }

    string LibGodotFile => Path.Combine(_core.DataRoot, "konsolenspiele", "godot-pfad.txt");

    /// <summary>Merkt sich eine selbst gewählte Godot-Datei. false: Das ist keine Godot-4-Datei, der Grund steht im Hinweis.</summary>
    public bool LibSetGodot(string path)
    {
        if (!File.Exists(path) || EngineTargets.GodotVersionFromName(Path.GetFileName(path)) is null)
        {
            _progMsg[ConsoleKey] = "Das ist kein Godot-4-Programm. Wähle die Datei, die mit Godot_v4 beginnt."; Show(ConsoleKey);
            return false;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(LibGodotFile)!);
        File.WriteAllText(LibGodotFile, path);
        _core.Audit.Append(Principal.LocalOwner.Name, "bibliothek.godot", Path.GetFileName(path));
        Show(ConsoleKey);
        return true;
    }

    string? LibGodot()
    {
        if (LibGodotOverride is not null) return LibGodotOverride;
        if (File.Exists(LibGodotFile) && File.ReadAllText(LibGodotFile).Trim() is { Length: > 0 } chosen && File.Exists(chosen)) return chosen;
        if (_opt.Isolated) return null;
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        IEnumerable<string> Files(string dir) => Directory.Exists(dir) ? Directory.EnumerateFiles(dir, "Godot_v4*", SearchOption.AllDirectories) : [];
        var found = EngineTargets.FindGodot([Path.Combine(_deps.ToolsRoot, "godot"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Godot"), Path.Combine(local, "Programs", "Godot"), Path.Combine(pf, "Godot")], Files);
        return found.Count > 0 ? found[0].Exe : Cli.Find("godot");
    }

    bool LibStart(string exe, IReadOnlyList<string> args, string cwd)
    {
        if (LibStartOverride is { } fake) return fake(exe, args);
        try
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = cwd };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            return p is not null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
    }

    /// <summary>Startet ein Godot-Projekt in einem eigenen Fenster.</summary>
    public bool LibPlayGodot(string project)
    {
        if (LibGodot() is not { } godot) { _progMsg[ConsoleKey] = "Zum Spielen fehlt Godot 4. Wähle es mit „Godot wählen“ oder richte es unter Programme ein."; Show(ConsoleKey); return false; }
        _core.Audit.Append(Principal.LocalOwner.Name, "bibliothek.start", Path.GetFileName(project));
        if (LibStart(godot, GameLibrary.PlayArgs(project), project)) return true;
        _progMsg[ConsoleKey] = "Godot ließ sich nicht starten."; Show(ConsoleKey);
        return false;
    }

    async Task<string?> LibEnsureMgba()
    {
        var exe = Path.Combine(LibMgbaDir, GameLibrary.MgbaExe.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(exe)) return exe;
        if (!_opt.Isolated && Cli.Find("mgba-qt") is { } system) return system;
        if (!OperatingSystem.IsWindows()) { _progMsg[ConsoleKey] = "Installiere mGBA über die Paketverwaltung deines Systems, dann klappt das Spielen."; return null; }
        var archive = Path.Combine(LibMgbaDir, "mgba.7z");
        if (await LibDownload(GameLibrary.MgbaUrl, GameLibrary.MgbaSha256, archive) is { } problem) { _progMsg[ConsoleKey] = "mGBA: " + problem; return null; }
        _core.Audit.Append(Principal.LocalOwner.Name, "deps.install", "mgba 0.10.5");
        bool extracted;
        if (LibExtractOverride is { } fake) extracted = fake(archive, LibMgbaDir);
        else
        {
            var seven = (await _deps.CheckAsync(DependencyCatalog.Get("7zip"))).Path;
            extracted = seven is not null && await Task.Run(() => Cli.Run(seven, ["x", "-y", "-o" + LibMgbaDir, archive], LibMgbaDir, 180000).Ok);
        }
        File.Delete(archive);
        if (extracted && File.Exists(exe)) return exe;
        _progMsg[ConsoleKey] = "mGBA ließ sich nicht entpacken. Zum Entpacken wird 7-Zip gebraucht.";
        return null;
    }

    /// <summary>Lädt ein Homebrew-Spiel mit Prüfsumme und startet es im Emulator.</summary>
    public async Task<bool> LibPlayHomebrew(GameLibrary.Entry e)
    {
        var rom = Path.Combine(LibHomebrewDir, GameLibrary.RomFile(e));
        if (!File.Exists(rom) || !string.Equals(ExtensionCatalog.FileSha256(rom), e.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            if (await LibDownload(e.Url!, e.Sha256!, rom) is { } problem) { _progMsg[ConsoleKey] = problem; Show(ConsoleKey); return false; }
            _core.Audit.Append(Principal.LocalOwner.Name, "bibliothek.homebrew", e.Id);
        }
        if (await LibEnsureMgba() is not { } mgba) { Show(ConsoleKey); return false; }
        if (LibStart(mgba, [rom], LibHomebrewDir)) return true;
        _progMsg[ConsoleKey] = "mGBA ließ sich nicht starten."; Show(ConsoleKey);
        return false;
    }
}
