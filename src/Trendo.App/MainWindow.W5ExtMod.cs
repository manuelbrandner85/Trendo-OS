using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0766 (X-04): Funktions-Erweiterungen in der Oberfläche.
/// · Erweiterungsseite: Rechte verständlich anzeigen, in eigenem Schritt freigeben (Rückfrage + ApprovalGate), jederzeit entziehen; Befehle anzeigen.
/// · Studio „Projekte“: Knopf je freigegebenem Befehl, Ergebnis als neue Datei im offenen Projekt, „Rückgängig“.
/// · Werkstatt: eigene Erweiterung aus Vorlage, Manifest-Editor, Quelltext, Bauen (Wasmtime), ZIP ausgeben, installieren, aktualisieren.</summary>
public partial class MainWindow
{
    readonly ExtensionResultUndo _extUndo = new();
    string? _extWsDir, _extWsZip; string _extWsMsg = ""; bool _extWsOpen;

    static Control ExtButtons(Button use, Button remove, Button? extra)
    {
        var w = new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = { use, new Border { Width = 6 }, remove } };
        if (extra is not null) { w.Children.Add(new Border { Width = 6 }); w.Children.Add(extra); }
        return w;
    }

    /// <summary>Rückfrage „Rechte freigeben?“ – eigener Schritt nach dem Installieren, nie im selben Klick.</summary>
    public void ExtAskGrant(InstalledExt e)
    {
        _extShow = e.Manifest.Id;
        _extAsk = ($"Rechte für „{e.Manifest.Name}“ freigeben?",
            "Die Erweiterung darf danach:\n" + string.Join("\n", (e.Manifest.Permissions ?? []).Select(r => "· " + ExtensionRights.Explain(r)))
            + "\nNicht erlaubt, egal was sie versucht: Internet, andere Dateien auf dem PC, länger als 15 Sekunden oder mehr als 64 MB Speicher. Entziehen geht jederzeit.",
            "Ja, diese Rechte erlauben",
            () => Task.FromResult(ExtGrantApproved(e.Manifest.Id)));
        Show("erweiterungen");
    }

    /// <summary>Rechte mit Freigabe (Ziel: Kennung@Prüfsumme) – nur aus dem „Ja“ der Rückfrage.</summary>
    public string ExtGrantApproved(string id)
    {
        if (Exts.Get(id) is not { } e) return "Erweiterung nicht gefunden.";
        try
        {
            var err = Exts.GrantRights(id, ExtApprove(ExtensionStore.GrantAction, $"Rechte für Erweiterung {e.Manifest.Name} freigeben", ExtensionStore.GrantTarget(e)));
            return err is null ? $"Rechte freigegeben. Der Knopf „{e.Manifest.Commands?.FirstOrDefault()?.Label}“ steht jetzt im Studio „Projekte“." : "Nicht freigegeben: " + err;
        }
        catch (ApprovalException x) { return "Nicht freigegeben: " + Trendo.Core.FriendlyError.Say(x); }
    }

    void ExtModuleDetail(StackPanel detail, InstalledExt x, IReadOnlyList<string> granted)
    {
        detail.Children.Add(H("Rechte", 14));
        var rights = new StackPanel { Name = "ext-rights", Spacing = 3 };
        foreach (var r in x.Manifest.Permissions ?? [])
        {
            var on = granted.Contains(r);
            rights.Children.Add(new TextBlock { Text = $"{(on ? "✓" : "○")}  {ExtensionRights.Explain(r)}{(on ? "" : " – noch nicht freigegeben")}", Foreground = on ? Text : Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        }
        rights.Children.Add(new TextBlock { Text = "✕  Internet, andere Dateien auf dem PC, Trendo-Einstellungen – gibt es für Erweiterungen nicht", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        detail.Children.Add(rights);
        detail.Children.Add(H("Befehle", 14));
        foreach (var c in x.Manifest.Commands ?? [])
            detail.Children.Add(P($"„{c.Label}“ – nimmt {string.Join(", ", c.Input)} aus dem offenen Projekt und legt eine neue {c.Output}-Datei daneben. Knopf im Studio „Projekte“ rechts unter „Mit Erweiterung“.", Text, 12));
        var src = Path.Combine(x.Dir, ExtensionWorkshop.SourceFile);
        if (File.Exists(src))
        {
            detail.Children.Add(H("Quelltext (zum Nachlesen)", 14));
            detail.Children.Add(new Border { Background = Brush.Parse("#0B1424"), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 10), MaxHeight = 320,
                Child = new ScrollViewer { Content = new SelectableTextBlock { Name = "ext-source", Text = File.ReadAllText(src), FontFamily = Mono, FontSize = 11, Foreground = Text } } });
        }
    }

    // ── Studio „Projekte“ ──

    /// <summary>Befehl einer Erweiterung auf eine Projektdatei anwenden (öffentlich für Tests). Läuft außerhalb des Oberflächen-Threads; Not-Stopp bricht ab.</summary>
    public async Task<string> ExtRunInProject(string extId, string commandId, string inputRel)
    {
        var pid = _projSel ?? throw new InvalidOperationException("Erst ein Projekt wählen.");
        var store = Exts; var projects = _core.Projects;
        var r = await Task.Run(() => store.RunCommand(extId, commandId, projects, pid, inputRel));
        _extUndo.Record(pid, r);
        _projMsg = r.Message;
        return r.Message;
    }

    public string ExtUndoLast() { _projMsg = _extUndo.Undo(_core.Projects); return _projMsg; }

    void ExtProjectCommands(StackPanel right, string pid)
    {
        var ready = Exts.ReadyCommands().ToList();
        if (ready.Count == 0 && !_extUndo.CanUndo) return;
        right.Children.Add(H("Mit Erweiterung", 13));
        var files = _core.Projects.Files(pid).Where(f => !f.IsDir).ToList();
        var i = 0;
        foreach (var (e, c) in ready)
        {
            var k = i++;
            var match = files.Where(f => c.Input.Contains(Path.GetExtension(f.Name).ToLowerInvariant())).Select(f => f.Relative).ToList();
            if (match.Count == 0) { right.Children.Add(P($"„{c.Label}“ ({e.Manifest.Name}): keine {string.Join("/", c.Input)}-Datei im Projekt.", Muted, 11.5)); continue; }
            var pick = new ComboBox { Name = $"ext-cmd-file-{k}", ItemsSource = match, SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
            var run = new Button { Name = $"ext-cmd-run-{k}", Content = c.Label, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
            ToolTip.SetTip(run, $"{e.Manifest.Name} {e.Manifest.Version} – läuft in der Sandbox, legt eine neue {c.Output}-Datei an");
            run.Click += async (_, _) => { run.IsEnabled = false; await ExtRunInProject(e.Manifest.Id, c.Id, pick.SelectedItem as string ?? match[0]); Show("projects"); };
            right.Children.Add(pick); right.Children.Add(run);
        }
        if (_extUndo.CanUndo)
        {
            var undo = new Button { Name = "ext-cmd-undo", Content = $"Rückgängig: {_extUndo.Next}", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
            undo.Click += (_, _) => { ExtUndoLast(); Show("projects"); };
            right.Children.Add(undo);
        }
    }

    // ── Werkstatt ──

    public string? ExtWorkshopDir => _extWsDir;
    public string? ExtWorkshopZip => _extWsZip;

    public string ExtWorkshopNew(string name)
    {
        _extWsDir = ExtensionWorkshop.Create(_core.DataRoot, name); _extWsOpen = true; _extWsZip = null;
        _extWsMsg = $"Vorlage „{name.Trim()}“ angelegt – Beschreibung und Quelltext anpassen, dann „Bauen und prüfen“.";
        return _extWsDir;
    }

    public void ExtWorkshopSave(ExtManifest m, string? source)
    {
        if (_extWsDir is not { } d) return;
        ExtensionWorkshop.Save(d, m);
        if (source is not null) ExtensionWorkshop.SaveSource(d, source);
    }

    public List<string> ExtWorkshopBuild()
    {
        if (_extWsDir is not { } d) return ["Erst eine Erweiterung anlegen."];
        var err = ExtensionWorkshop.Build(d);
        _extWsMsg = err.Count == 0 ? "Gebaut und geprüft – nur erlaubte Sandbox-Funktionen, Prüfsumme eingetragen." : "Noch nicht fertig: " + string.Join(" ", err);
        return err;
    }

    public string? ExtWorkshopExport()
    {
        if (_extWsDir is not { } d) return null;
        var (zip, err) = ExtensionWorkshop.Export(d, ExtensionWorkshop.ExportDir(_core.DataRoot));
        _extWsZip = zip;
        _extWsMsg = zip is null ? "Nicht ausgegeben: " + string.Join(" ", err) : $"ZIP liegt unter {zip} (Prüfsumme {ExtensionCatalog.FileSha256(zip)[..12]}…). Weitergeben oder hier installieren.";
        return zip;
    }

    private Control ExtWorkshopCard()
    {
        const string V = "erweiterungen";
        var sp = new StackPanel { Name = "ext-ws", Spacing = 6 };
        var list = ExtensionWorkshop.List(_core.DataRoot);
        if (_extWsDir is null || !Directory.Exists(_extWsDir)) _extWsDir = list.FirstOrDefault();
        var newName = new TextBox { Name = "ext-ws-name", PlaceholderText = "Name der neuen Erweiterung", FontSize = 12 };
        sp.Children.Add(new DockPanel { Children = { Dk(Act("ext-ws-new", "Aus Vorlage anlegen", () => Try(V, () => { ExtWorkshopNew(newName.Text ?? ""); Show(V); })), Avalonia.Controls.Dock.Right), newName } });
        if (list.Count > 1)
        {
            var pick = new ComboBox { Name = "ext-ws-pick", ItemsSource = list.Select(Path.GetFileName).ToList(), SelectedItem = Path.GetFileName(_extWsDir), FontSize = 12 };
            pick.SelectionChanged += (_, _) => { if (pick.SelectedItem is string n && Path.Combine(ExtensionWorkshop.Root(_core.DataRoot), n) is var d && d != _extWsDir) { _extWsDir = d; _extWsZip = null; _extWsMsg = ""; Show(V); } };
            sp.Children.Add(pick);
        }
        if (_extWsDir is null)
        {
            sp.Children.Add(P("Eine eigene Funktions-Erweiterung entsteht aus der Vorlage „CSV als Tabelle“: Beschreibung und Rechte festlegen, Quelltext (WebAssembly-Text) anpassen, bauen, als ZIP ausgeben. Trendo baut mit der eingebauten Wasmtime-Bibliothek – nichts muss installiert werden.", Muted, 11.5));
            return Card(sp);
        }
        ExtManifest m;
        try { m = ExtensionWorkshop.Load(_extWsDir); }
        catch (Exception x) when (x is IOException or System.Text.Json.JsonException or InvalidDataException) { sp.Children.Add(P("Beschreibung nicht lesbar: " + x.Message, Brush.Parse("#F97316"), 11.5)); return Card(sp); }
        var cmd = m.Commands?.FirstOrDefault() ?? new ExtCommand("umwandeln", "Umwandeln", [".csv"], ".md");
        TextBox Field(string name, string label, string? text) { var t = new TextBox { Name = name, Text = text, FontSize = 12 }; sp.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 11 }); sp.Children.Add(t); return t; }
        var fName = Field("ext-ws-f-name", "Name", m.Name);
        sp.Children.Add(new TextBlock { Text = $"Kennung {m.Id} · Autor {m.Author} · Lizenz {m.License}", Foreground = Muted, FontSize = 11 });
        var fVer = Field("ext-ws-f-version", "Version (zum Aktualisieren erhöhen, z. B. 1.1.0)", m.Version);
        var fDesc = Field("ext-ws-f-desc", "Beschreibung", m.Description);
        var fCmd = Field("ext-ws-f-cmd", "Name des Knopfs im Studio „Projekte“", cmd.Label);
        var fIn = Field("ext-ws-f-in", "Nimmt Dateien mit Endung (mit Komma getrennt)", string.Join(", ", cmd.Input));
        sp.Children.Add(new TextBlock { Text = "Ergebnis-Endung", Foreground = Muted, FontSize = 11 });
        var fOut = new ComboBox { Name = "ext-ws-f-out", ItemsSource = Extensions.OutputExt, SelectedItem = Extensions.OutputExt.Contains(cmd.Output) ? cmd.Output : ".md", FontSize = 12 };
        sp.Children.Add(fOut);
        sp.Children.Add(new TextBlock { Text = "Rechte, die die Erweiterung erbittet", Foreground = Muted, FontSize = 11 });
        var checks = new List<(string R, CheckBox C)>();
        foreach (var r in ExtensionRights.All)
        {
            var c = new CheckBox { Name = "ext-ws-right-" + r, IsChecked = (m.Permissions ?? []).Contains(r), Content = new TextBlock { Text = ExtensionRights.Explain(r), FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Foreground = Text } };
            checks.Add((r, c)); sp.Children.Add(c);
        }
        sp.Children.Add(new TextBlock { Text = "Quelltext (WebAssembly-Text, WAT) – liest stdin, schreibt das Ergebnis nach stdout", Foreground = Muted, FontSize = 11 });
        var src = new TextBox { Name = "ext-ws-source", Text = ExtensionWorkshop.LoadSource(_extWsDir), AcceptsReturn = true, FontFamily = Mono, FontSize = 11, Height = 240, TextWrapping = TextWrapping.NoWrap };
        sp.Children.Add(src);
        var dir = _extWsDir;
        ExtManifest Collect() => ExtensionWorkshop.Load(dir) with // frisch von der Platte: Prüfsumme und Einstieg aus dem letzten Bau bleiben
        {
            Name = (fName.Text ?? "").Trim(), Version = (fVer.Text ?? "").Trim(), Description = (fDesc.Text ?? "").Trim(),
            Permissions = [.. checks.Where(x => x.C.IsChecked == true).Select(x => x.R)],
            Commands = [cmd with { Label = (fCmd.Text ?? "").Trim(), Input = [.. (fIn.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.StartsWith('.') ? x.ToLowerInvariant() : "." + x.ToLowerInvariant())], Output = fOut.SelectedItem as string ?? ".md" }],
        };
        void SaveNow() { if (_extWsDir == dir) ExtWorkshopSave(Collect(), src.Text ?? ""); }
        // Kriterium 3: Getipptes nach der Tipp-Pause sichern – ein Absturz verliert höchstens die letzten Sekunden
        SaveOnPause(src, SaveNow); foreach (var t in new[] { fName, fVer, fDesc, fCmd, fIn }) SaveOnPause(t, SaveNow);
        var bar = new WrapPanel();
        bar.Children.Add(Act("ext-ws-build", "Bauen und prüfen", () => Try(V, () => { SaveNow(); ExtWorkshopBuild(); Show(V); })));
        bar.Children.Add(Act("ext-ws-export", "Als ZIP ausgeben", () => Try(V, () => { SaveNow(); if (ExtWorkshopBuild().Count == 0) ExtWorkshopExport(); Show(V); })));
        if (_extWsZip is { } zip && File.Exists(zip)) bar.Children.Add(Act("ext-ws-install", "Diese ZIP installieren …", () => ExtInstall(zip)));
        sp.Children.Add(bar);
        if (_extWsMsg.Length > 0) { var msg = P(_extWsMsg, Text, 11.5); msg.Name = "ext-ws-msg"; sp.Children.Add(msg); }
        return Card(sp);
    }
}
