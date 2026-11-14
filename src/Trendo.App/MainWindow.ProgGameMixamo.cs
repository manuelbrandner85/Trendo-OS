using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Trendo.Core;
using Trendo.Core.Bibliothek;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0660 (Freigabe 14.11.2026, M1–M6): Mixamo im Programm „Spiele bauen“ unter „Figuren“. mixamo.com im eingebauten Browser (Anmeldung macht die Person,
/// Trendo speichert nichts davon), neue Mixamo-FBX aus dem Download-Ordner werden von selbst übernommen, die eigene Figur geht als FBX zum Auto-Rigger,
/// Blender bereitet die Animationen auf, Unreal und Godot bekommen das Ergebnis. Lizenz: nur im fertigen Spiel (siehe MixamoGuard).</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Blender-Aufbereitung (Figur, Aktion → Datei, Zielordner, Bewegungsart) statt echtem Blender.</summary>
    public Func<string, IReadOnlyDictionary<string, string>, string, string, MixamoBlender.PrepReport>? MixamoPrepOverride { get; set; }
    /// <summary>Für Tests: Auto-Rigger-Vorbereitung (Quelle, Ziel) statt echtem Blender.</summary>
    public Func<string, string, MixamoBlender.RiggerReport>? MixamoRiggerOverride { get; set; }

    bool _mxWebOpen, _mxBusy; string _mxMsg = ""; string _mxMotion = MixamoBlender.InPlace;
    DateTime _mxSince = DateTime.Now.AddDays(-1); // Doppelte verhindert Known (Name und Größe), nicht die Zeit
    NativeWebView? _mxWeb; FileSystemWatcher? _mxWatch; Avalonia.Threading.DispatcherTimer? _mxTimer; bool _mxScanning;

    string MixamoDir(ProgramDocument d, string sub) => Path.Combine(GameAssetDir(d), sub);
    string MixamoProfile => Path.Combine(_core.DataRoot, "web-mixamo");

    string? BlenderExePath() => GameEnginesSeen().FirstOrDefault(e => e.Id == "blender" && e.Installed)?.Exe;

    // ───────────── M1: eingebauter Browser ─────────────

    NativeWebView MixamoWeb()
    {
        if (_mxWeb is not null) return _mxWeb;
        var profile = MixamoProfile;
        if (File.Exists(profile + ".loeschen")) { try { Directory.Delete(profile, true); File.Delete(profile + ".loeschen"); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
        var web = new NativeWebView { Name = "game-mixamo-web", Height = 620 };
        // eigenes Profil: die Anmeldung bleibt in Trendo, Trendo liest keine Anmeldedaten; „Abmelden“ löscht es
        web.EnvironmentRequested += (_, e) => { if (e is WindowsWebView2EnvironmentRequestedEventArgs w) { Directory.CreateDirectory(profile); w.UserDataFolder = profile; } };
        web.NavigationStarted += (_, e) =>
        {
            // im Fenster bleiben nur Mixamo und die Anmeldung; alles andere öffnet der normale Browser
            if (e.Request is { } u && !Mixamo.Inside(u)) { e.Cancel = true; if (u.Scheme == "https") Launch(u.AbsoluteUri); }
        };
        web.NewWindowRequested += (_, e) =>
        {
            if (e.Request is { } u && Mixamo.Inside(u)) { e.Handled = false; return; } // Adobe-Anmeldefenster in Trendo lassen
            if (e.Request is { } l && l.Scheme == "https") Launch(l.AbsoluteUri);
            e.Handled = true;
        };
        return _mxWeb = web;
    }

    void MixamoGo(string url)
    {
        _mxWebOpen = true; MixamoWatch();
        if (!_opt.Isolated) MixamoWeb().Navigate(new Uri(url));
        Show(GK);
    }

    /// <summary>Abmelden: Browser schließen und das Mixamo-Profil (Sitzung, Cookies) löschen; klappt das nicht sofort, beim nächsten Öffnen.</summary>
    public void MixamoSignOut()
    {
        _mxWebOpen = false; _mxWeb = null; Show(GK);
        var profile = MixamoProfile;
        try { if (Directory.Exists(profile)) Directory.Delete(profile, true); _mxMsg = "✓ Abgemeldet – die Mixamo-Sitzung ist gelöscht."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try { File.WriteAllText(profile + ".loeschen", ""); } catch (IOException) { }
            _mxMsg = "Abgemeldet – die Sitzung wird beim nächsten Öffnen gelöscht (sie ist gerade noch in Benutzung).";
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "game.mixamo.abmelden", "");
        Show(GK);
    }

    // ───────────── M2: Download-Ordner beobachten ─────────────

    void MixamoWatch()
    {
        if (_mxWatch is not null || _opt.Isolated || !Directory.Exists(BibDownloads)) return;
        try
        {
            _mxWatch = new FileSystemWatcher(BibDownloads) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size, EnableRaisingEvents = true };
            _mxWatch.Filters.Add("*.fbx"); _mxWatch.Filters.Add("*.zip");
            void Seen(object? _, FileSystemEventArgs __) => _ui.Post(MixamoSoon);
            _mxWatch.Created += Seen; _mxWatch.Renamed += (s, e) => Seen(s, e); _mxWatch.Changed += Seen;
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { _mxWatch = null; }
    }

    /// <summary>Viele Ereignisse beim Schreiben einer Datei: gesammelt, erst 2 s nach dem letzten wird geprüft.</summary>
    void MixamoSoon()
    {
        _mxTimer ??= new Avalonia.Threading.DispatcherTimer(TimeSpan.FromSeconds(2), Avalonia.Threading.DispatcherPriority.Background, (_, _) => { _mxTimer!.Stop(); if (ProgramActive(GK) is { } d) _ = MixamoScan(d); });
        _mxTimer.Stop(); _mxTimer.Start();
    }

    /// <summary>Neue Mixamo-Dateien (FBX und Pakete als ZIP) aus dem Download-Ordner übernehmen, auch per Knopf. Prüfen, Entpacken und Kopieren laufen im
    /// Hintergrund; übernommen wird nur, was fertig geschrieben ist (Größe stabil, Datei frei). Liefert die übernommenen Dateinamen.</summary>
    public async Task<List<string>> MixamoScan(ProgramDocument d)
    {
        var taken = new List<string>();
        if (_mxScanning) return taken;
        _mxScanning = true;
        try
        {
            var seenZips = d.Meta["mixamoZips"] as JsonArray ?? [];
            var candidates = DownloadPickup.NewFiles(BibDownloads, _mxSince, [".fbx", ".zip"])
                .Where(f => Path.GetExtension(f).Equals(".zip", StringComparison.OrdinalIgnoreCase) ? !seenZips.Any(z => (string?)z == ZipKey(f)) : !Mixamo.Known(d.Data, f)).ToList();
            if (candidates.Count == 0) return taken;
            var dir = MixamoDir(d, "mixamo");
            var known = Mixamo.Assets(d.Data).Select(a => Path.GetFileName((string?)a["file"] ?? "")).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var (ready, zips) = await Task.Run(() => MixamoPrepare(candidates, dir, known));
            foreach (var (file, info, origin) in ready)
                try
                {
                    d.Edit("Mixamo: " + Path.GetFileName(file), x => { if (Mixamo.Take(x, dir, file, info) is { } e) { e["origin"] = origin; e["originBytes"] = info.Bytes; } });
                    taken.Add(Path.GetFileName(file)); _core.Audit.Append(Principal.LocalOwner.Name, "game.mixamo.uebernommen", Path.GetFileName(file));
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { _mxMsg = "⚠ " + ex.Message; }
            if (zips.Count > 0) d.EditMeta(m => { var a = m["mixamoZips"] as JsonArray ?? []; foreach (var z in zips) a.Add(z); m["mixamoZips"] = a; });
            if (taken.Count > 0) { _mxMsg = $"✓ Übernommen: {string.Join(", ", taken)} (Quelle Mixamo, nur im fertigen Spiel)."; GameRefresh(); }
        }
        finally { _mxScanning = false; }
        return taken;
    }

    static string ZipKey(string f) { try { return Path.GetFileName(f) + "|" + new FileInfo(f).Length; } catch (IOException) { return Path.GetFileName(f); } }

    /// <summary>Hintergrund: fertig geschriebene Dateien finden, Mixamo erkennen, ZIP-Pakete entpacken, in den Asset-Ordner kopieren (eigener Name bei Gleichnamigen).</summary>
    static (List<(string File, Mixamo.Info Info, string Origin)> Ready, List<string> Zips) MixamoPrepare(List<string> files, string dir, HashSet<string> taken)
    {
        var size = files.ToDictionary(f => f, f => { try { return new FileInfo(f).Length; } catch (IOException) { return -1L; } });
        Thread.Sleep(700);
        var ready = new List<(string, Mixamo.Info, string)>(); var zips = new List<string>();
        foreach (var f in files)
        {
            try
            {
                if (new FileInfo(f).Length != size[f] || size[f] <= 0) continue; // wird noch geschrieben
                using (File.Open(f, FileMode.Open, FileAccess.Read, FileShare.None)) { } // niemand schreibt mehr
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            try
            {
                var sources = new List<string>();
                if (Path.GetExtension(f).Equals(".zip", StringComparison.OrdinalIgnoreCase)) { sources.AddRange(Mixamo.FromZip(f, Path.Combine(Path.GetTempPath(), "trendo-mixamo-zip-" + Guid.NewGuid().ToString("N")))); zips.Add(ZipKey(f)); }
                else sources.Add(f);
                foreach (var src in sources)
                {
                    var info = Mixamo.Detect(src) with { Bytes = new FileInfo(src).Length }; if (!info.IsMixamo) continue;
                    Directory.CreateDirectory(dir);
                    var name = Path.GetFileName(src); var target = Path.Combine(dir, name);
                    for (var i = 2; File.Exists(target) || taken.Contains(Path.GetFileName(target)); i++) target = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(name)}-{i}{Path.GetExtension(name)}");
                    File.Copy(src, target); taken.Add(Path.GetFileName(target));
                    ready.Add((target, info, Path.GetFileName(src)));
                }
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { }
        }
        return (ready, zips);
    }

    // ───────────── M3: eigene Figur zum Auto-Rigger ─────────────

    public async Task MixamoRigger(ProgramDocument d, string source)
    {
        if (_mxBusy) return;
        var out1 = Path.Combine(MixamoDir(d, "mixamo-upload"), DevStudio.SafeName(d.Text("title", "Figur")) + "-fuer-mixamo.fbx");
        _mxBusy = true; _mxMsg = "Blender bereitet die Figur für den Auto-Rigger vor …"; GameRefresh();
        try
        {
            var bl = MixamoRiggerOverride is null ? BlenderExePath() ?? throw new InvalidOperationException("Blender ist nicht installiert – kostenlos von blender.org.") : "";
            var r = await Task.Run(() => MixamoRiggerOverride is { } o ? o(source, out1) : MixamoBlender.Rigger(bl, source, out1));
            _mxMsg = $"✓ {Path.GetFileName(r.File)} ist bereit ({r.Height:0.00} m, {r.Triangles} Dreiecke). Auf Mixamo „Upload Character“ wählen und diese Datei hochladen; das Ergebnis lädst du herunter, Trendo übernimmt es von selbst.";
            if (!_opt.Isolated) Launch(Path.GetDirectoryName(r.File)!);
            MixamoGo(Mixamo.Url);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException) { _mxMsg = "⚠ " + e.Message; }
        finally { _mxBusy = false; GameRefresh(); }
    }

    async Task MixamoPickFigure(ProgramDocument d)
    {
        var got = PickOverride is { } fake ? await fake(new PickRequest("game-mixamo-rigger", false, false, ["glb", "gltf", "obj", "fbx", "blend"]))
            : (await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Eigene Figur (ohne Skelett) für Mixamo", FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("3D-Figur") { Patterns = ["*.glb", "*.gltf", "*.obj", "*.fbx", "*.blend"] }] }))
                .Select(f => Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(f)).OfType<string>().ToArray();
        if (got is { Length: > 0 }) await MixamoRigger(d, got[0]);
    }

    // ───────────── M4: Blender, dann Unreal und Godot ─────────────

    public async Task MixamoPrep(ProgramDocument d)
    {
        if (_mxBusy) return;
        var anims = Mixamo.Assignments(d.Data).Where(kv => File.Exists(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value);
        var figure = d.Text("mixamoFigure"); if (!File.Exists(figure)) figure = "";
        var outDir = MixamoDir(d, "mixamo-fertig"); var motion = _mxMotion;
        _mxBusy = true; _mxMsg = "Blender bereitet die Animationen auf (Namen, Maßstab, " + (motion == MixamoBlender.InPlace ? "auf der Stelle" : "Wurzelbewegung") + (figure.Length > 0 ? ", Übertragen auf die Figur" : "") + ") …"; GameRefresh();
        try
        {
            var bl = MixamoPrepOverride is null ? BlenderExePath() ?? throw new InvalidOperationException("Blender ist nicht installiert – kostenlos von blender.org.") : "";
            var r = await Task.Run(() => MixamoPrepOverride is { } o ? o(figure, anims, outDir, motion) : MixamoBlender.Prep(bl, figure, anims, outDir, motion));
            var warn = MixamoBlender.Check(r);
            d.Edit("Mixamo aufbereitet", x => x["mixamoPrepared"] = new JsonObject { ["dir"] = outDir, ["motion"] = motion, ["glb"] = r.Glb, ["fbx"] = new JsonArray([.. r.Animations.Select(a => (JsonNode)a.Fbx)]), ["ok"] = warn.Count == 0 });
            _core.Audit.Append(Principal.LocalOwner.Name, "game.mixamo.blender", $"{r.Animations.Count} {motion}");
            _mxMsg = (warn.Count == 0 ? "✓ " : "⚠ ") + $"{r.Animations.Count} Animation(en) aufbereitet" + (r.FigureHeight > 0 ? $", Figur {r.FigureHeight:0.00} m" : "") + ": " +
                     string.Join(", ", r.Animations.Select(a => $"{a.Action} {a.Frames} Bilder, {a.Matched}/{a.Bones} Knochen" + (motion == MixamoBlender.InPlace ? $", Weg {a.Travel:0.0} m → auf der Stelle" : ""))) +
                     (warn.Count > 0 ? ". Prüfen: " + string.Join(" ", warn) : ".");
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException) { _mxMsg = "⚠ " + e.Message; }
        finally { _mxBusy = false; GameRefresh(); }
    }

    static List<string> MixamoFbx(ProgramDocument d) => [.. (d.Data["mixamoPrepared"]?["fbx"] as JsonArray ?? []).Select(n => n?.GetValue<string>() ?? "").Where(File.Exists)];

    public async Task MixamoToUnreal(ProgramDocument d)
    {
        var built = d.Text("built");
        var uproject = built.Length > 0 && Directory.Exists(built) ? Directory.GetFiles(built, "*.uproject").FirstOrDefault() : null;
        if (uproject is null) { _mxMsg = "⚠ Erst „Bauen“ – dann holt Trendo die Animationen ins Unreal-Projekt."; GameRefresh(); return; }
        if (UnrealEngineDir() is not { } engine) { _mxMsg = "⚠ Unreal Engine 5 fehlt – kostenlos über den Epic Games Launcher."; GameRefresh(); return; }
        var files = MixamoFbx(d); if (files.Count == 0) { _mxMsg = "⚠ Erst „Mit Blender aufbereiten“."; GameRefresh(); return; }
        if (_mxBusy || UnrealBusy) return;
        _mxBusy = true; _mxMsg = "Unreal importiert die Animationen und überträgt sie per IK Retargeter auf die Engine-Figur … einige Minuten."; GameRefresh();
        try
        {
            var log = Path.Combine(built, "Trendo-Pruefung", "mixamo.txt"); Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            var n = MixamoUnreal.Stage(built, files);
            var (exit, text) = await Task.Run(() => UnrealRun(UnrealGame.EditorCmd(engine), MixamoUnreal.Args(uproject, log), log, 1_800_000));
            var (ok, imported, retargeted, err) = MixamoUnreal.Parse(text);
            if (ok) MixamoUnreal.Unstage(built); // importiert: die Rohdateien bleiben nicht lose im Projekt liegen
            _core.Audit.Append(Principal.LocalOwner.Name, "game.mixamo.unreal", ok ? $"{imported}/{retargeted}" : "fehler");
            _mxMsg = ok ? $"✓ Unreal: Figur {MixamoUnreal.HeightCm(text)} cm, {imported} von {n} Animation(en) importiert, {retargeted} auf die Engine-Figur übertragen (Ordner Trendo/Mixamo im Projekt)." : $"⚠ Unreal (Exitcode {exit}): {err}";
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { _mxMsg = "⚠ " + e.Message; }
        finally { _mxBusy = false; GameRefresh(); }
    }

    public void MixamoToGodot(ProgramDocument d)
    {
        var dir = d.Text("godotProject");
        var glb = d.Data["mixamoPrepared"]?["glb"]?.GetValue<string>() ?? "";
        if (dir.Length == 0 || !Directory.Exists(dir)) throw new InvalidOperationException("Erst „Als Godot-Projekt“ unter „Bauen“.");
        if (!File.Exists(glb)) throw new InvalidOperationException("Erst „Mit Blender aufbereiten“.");
        var target = Path.Combine(dir, "assets", "mixamo"); Directory.CreateDirectory(target);
        File.Copy(glb, Path.Combine(target, "mixamo.glb"), true);
        // Godot braucht die Datei im Projekt; damit sie nicht über Git weitergegeben wird, schließt eine .gitignore sie aus
        File.WriteAllText(Path.Combine(target, ".gitignore"), "# Mixamo-Dateien nur im fertigen Spiel, nicht weitergeben (Mixamo-Bedingungen)\n*.glb\n*.fbx\n");
        _mxMsg = "✓ mixamo.glb liegt in assets/mixamo – Godot importiert sie beim Öffnen (Animationen: " + string.Join(", ", Mixamo.Assignments(d.Data).Keys) + ").";
    }

    // ───────────── Karte ─────────────

    Control GameMixamoCard(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "game-mixamo", Spacing = 6 };
        sp.Children.Add(H("Figuren und Animationen von Mixamo", 14));
        sp.Children.Add(P("Kostenlos mit eigenem Adobe-Konto. In deinem Spiel erlaubt, auch verkauft – die Dateien selbst dürfen aber nicht weitergegeben werden. Trendo übernimmt sie und passt auf.", Muted, 12));
        sp.Children.Add(P(Mixamo.Summary(d.Data), Text, 12));
        sp.Children.Add(new WrapPanel { Children = {
            Act("game-mixamo-open", _mxWebOpen ? "Mixamo schließen" : "Mixamo öffnen", () => { if (_mxWebOpen) { _mxWebOpen = false; Show(GK); } else MixamoGo(Mixamo.Url); }, true), new Border { Width = 6 },
            Act("game-mixamo-scan", "Neue Downloads übernehmen", async () => { var t = await MixamoScan(d); if (t.Count == 0) { _mxMsg = "Keine neue, fertig geladene Mixamo-Datei im Download-Ordner."; Show(GK); } }), new Border { Width = 6 },
            Busy3(Act("game-mixamo-rigger", "Eigene Figur für den Auto-Rigger", () => _ = MixamoPickFigure(d))) } });
        if (_mxWebOpen)
        {
            Control view = _opt.Isolated ? new TextBlock { Name = "game-mixamo-web", Text = "Testmodus: mixamo.com wird nicht geladen.", Foreground = Muted, Margin = new Thickness(8) } : MixamoWeb();
            if (view.Parent is Panel old) old.Children.Remove(view); else if (view.Parent is Decorator dec) dec.Child = null;
            sp.Children.Add(new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, BorderBrush = Edge, BorderThickness = new Thickness(1), Child = view });
            sp.Children.Add(P("Anmelden machst du selbst. Lade Figuren als FBX herunter – Trendo übernimmt neue Dateien aus dem Download-Ordner von selbst.", Muted, 11.5));
        }
        if (_mxWebOpen || Directory.Exists(MixamoProfile)) sp.Children.Add(Act("game-mixamo-signout", "Bei Mixamo abmelden (Sitzung löschen)", MixamoSignOut));

        // Übernommene Dateien: Art änderbar (Figur oder Animation)
        var all = Mixamo.Assets(d.Data);
        if (all.Count > 0)
        {
            var list = new StackPanel { Name = "game-mixamo-files", Spacing = 2 };
            for (var i = 0; i < all.Count; i++)
            {
                var file = all[i]["file"]!.GetValue<string>(); var kind = all[i]["kind"]?.GetValue<string>() ?? "";
                var other = kind == "figur" ? "animation" : "figur";
                var b = Act($"game-mixamo-kind-{i}", kind == "figur" ? "ist Animation" : "ist Figur", () => ProgramRun(GK, () => d.Edit("Mixamo-Art", x => Mixamo.SetKind(x, file, other))));
                b.FontSize = 11; b.Padding = new Thickness(6, 2);
                list.Children.Add(new DockPanel { Children = { Dk(b, Avalonia.Controls.Dock.Right), new TextBlock { Text = $"{Path.GetFileName(file)} – {(kind == "figur" ? "Figur" : "Animation")}", Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } });
            }
            sp.Children.Add(list);
        }

        // M5: Aktionen zuordnen
        var anims = Mixamo.Assets(d.Data).Where(a => a["kind"]?.GetValue<string>() == "animation").Select(a => a["file"]!.GetValue<string>()).ToList();
        var figs = Mixamo.Assets(d.Data).Where(a => a["kind"]?.GetValue<string>() == "figur").Select(a => a["file"]!.GetValue<string>()).ToList();
        var map = Mixamo.Assignments(d.Data);
        sp.Children.Add(H("Spielaktionen", 13));
        if (figs.Count > 0)
        {
            var cur = d.Text("mixamoFigure");
            var pick = new ComboBox { Name = "game-mixamo-figure", ItemsSource = figs.Select(Path.GetFileName).ToList(), SelectedIndex = Math.Max(0, figs.IndexOf(cur)), MinWidth = 220 };
            pick.SelectionChanged += (_, _) => { var f = figs[Math.Max(0, pick.SelectedIndex)]; if (f != d.Text("mixamoFigure")) ProgramRun(GK, () => d.Set("mixamoFigure", f, "Mixamo-Figur")); };
            sp.Children.Add(Row(P("Figur", Muted, 11.5), pick));
        }
        foreach (var a in Mixamo.Actions)
        {
            var id = a.Id;
            var items = new List<string> { "–" }; items.AddRange(anims.Select(Path.GetFileName)!);
            var box = new ComboBox { Name = $"game-mixamo-act-{id}", ItemsSource = items, SelectedIndex = map.TryGetValue(id, out var f) ? Math.Max(0, anims.IndexOf(f) + 1) : 0, MinWidth = 200 };
            box.SelectionChanged += (_, _) => { var i = box.SelectedIndex; var want = i <= 0 ? null : anims[i - 1]; if (want != Mixamo.Assignments(d.Data).GetValueOrDefault(id)) ProgramRun(GK, () => d.Edit("Aktion " + a.Label, x => Mixamo.Assign(x, id, want))); };
            var row = new WrapPanel { Children = { new TextBlock { Text = a.Label, Width = 90, Foreground = Text, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center }, box, new Border { Width = 8 } } };
            foreach (var (q, k) in a.Search.Select((q, k) => (q, k)))
            {
                var b = Act($"game-mixamo-act-{id}-s{k}", "🔎 " + q, () => MixamoGo(Mixamo.SearchUrl(q))); b.FontSize = 11; b.Padding = new Thickness(6, 2); b.Margin = new Thickness(0, 0, 4, 0);
                row.Children.Add(b);
            }
            sp.Children.Add(row);
        }

        // M4: Blender, Unreal, Godot
        sp.Children.Add(H("Aufbereiten", 13));
        var inPlace = new RadioButton { Name = "game-mixamo-inplace", GroupName = "mx-motion", Content = "Auf der Stelle (das Spiel bewegt die Figur)", IsChecked = _mxMotion == MixamoBlender.InPlace };
        var root = new RadioButton { Name = "game-mixamo-root", GroupName = "mx-motion", Content = "Wurzelbewegung (die Animation bewegt die Figur)", IsChecked = _mxMotion == MixamoBlender.RootMotion };
        inPlace.IsCheckedChanged += (_, _) => { if (inPlace.IsChecked == true) _mxMotion = MixamoBlender.InPlace; };
        root.IsCheckedChanged += (_, _) => { if (root.IsChecked == true) _mxMotion = MixamoBlender.RootMotion; };
        sp.Children.Add(new WrapPanel { Children = { inPlace, new Border { Width = 12 }, root } });
        var prepared = d.Data["mixamoPrepared"] is JsonObject;
        sp.Children.Add(new WrapPanel { Children = {
            Busy3(Act("game-mixamo-prep", _mxBusy ? "Arbeitet …" : "Mit Blender aufbereiten", () => _ = MixamoPrep(d), true)), new Border { Width = 6 },
            Busy3(Act("game-mixamo-unreal", "Nach Unreal (IK Retargeter)", () => _ = MixamoToUnreal(d)), prepared), new Border { Width = 6 },
            Busy3(Act("game-mixamo-godot", "Nach Godot (glTF)", () => ProgramRun(GK, () => MixamoToGodot(d))), prepared) } });
        if (_mxMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "game-mixamo-msg", Text = _mxMsg, Foreground = _mxMsg.StartsWith('⚠') ? Brush.Parse("#F5A524") : Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        return new Border { Padding = new Thickness(12, 10), Margin = new Thickness(0, 0, 0, 10), CornerRadius = new CornerRadius(12), Background = Brush.Parse(C("#14FFFFFF", "#08000000")), BorderBrush = Edge, BorderThickness = new Thickness(1), Child = sp };
    }

    Button Busy3(Button b, bool enabled = true) { b.IsEnabled = enabled && !_mxBusy; return b; }
}
