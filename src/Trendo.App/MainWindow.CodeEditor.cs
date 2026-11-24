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
/// unten Ausgabe, Tests, Fehler, Terminal, Paket, Suche in allen Dateien und Git. Strg+S speichert.
/// T-0688: Bauen, Ausführen, Testen (je Test, Klick springt zur Stelle) und Paket bauen mit den echten Werkzeugen (DevBuild), Terminal im Projektordner,
/// Entwürfe ungespeicherter Dateien im Datenordner (CodeDrafts) mit Wiederherstellen nach einem Absturz.</summary>
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

    private void InitCodeEditor()
    {
        Closed += (_, _) => CodeStop(); // kein verwaistes Programm, wenn Trendo geschlossen wird
        RegisterTab(KreativTabs, CEK, "Code-Editor", "CORE-07", CodeEditorView, "code-editor", "code bearbeiten", "quelltext", "editor", "programmieren", "vs code", "vscodium", "syntax");
    }

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
            CodeProject.WriteText(f.Path, f.Doc.Text); f.Saved = f.Doc.Text; CodeDrafts.Delete(_core.DataRoot, f.Path, ProgLocks);
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

    // T-0681: „Ausführen“ und „Testen“ laufen im Hintergrund – die Oberfläche bleibt bedienbar, die Ausgabe kommt laufend, „Abbrechen“ beendet das Programm.
    // T-0688: dazu „Bauen“, „Paket bauen“ und das Terminal – alle über denselben Hintergrund-Lauf, immer nur einer zur Zeit.
    CancellationTokenSource? _ceRunCts; Task? _ceRunTask; int _ceRunPid; SelectableTextBlock? _ceOutBox, _ceTermBox;
    string _ceRunWhat = "run"; string _ceTerm = ""; DevBuild.TestRun? _ceTests; List<DevBuild.Problem> _ceProblems = []; DevBuild.Package? _cePkg;
    /// <summary>Läuft gerade ein Programm oder Test aus dem Code-Editor?</summary>
    public bool CodeRunning => _ceRunCts is not null;
    /// <summary>Der laufende bzw. letzte Lauf (für Tests und zum Abwarten).</summary>
    public Task? CodeRunTask => _ceRunTask;
    /// <summary>Prozessnummer des zuletzt gestarteten Programms (0 = noch keins).</summary>
    public int CodeRunPid => _ceRunPid;
    /// <summary>Tests: dieses Kommando starten statt des erkannten – auch im abgeschotteten Testmodus, der sonst nichts startet.</summary>
    public (string Exe, string[] Args, string Cwd)? CodeRunOverride { get; set; }
    /// <summary>Tests (T-0688): im abgeschotteten Testmodus trotzdem die echten Werkzeuge (dotnet, python, node, Terminal) starten.</summary>
    public bool CodeRealToolsInTests { get; set; }
    /// <summary>Ergebnis des letzten Testlaufs (je Test), Fehlerliste des letzten Laufs, letztes Paket, Ausgabe und Terminal.</summary>
    public DevBuild.TestRun? CodeTests => _ceTests;
    public IReadOnlyList<DevBuild.Problem> CodeProblems => _ceProblems;
    public DevBuild.Package? CodePackage => _cePkg;
    public string CodeOutput => _ceOut;
    public string CodeTerminalText => _ceTerm;
    const int CeOutMax = 200_000;

    bool CodeMayStart(out string why)
    {
        why = "";
        if (!DarfAusfuehren("ausfuehren", out why)) return false; // T-0764 (H): Ausführen, Testen, Bauen, Paket – im Kinderprofil nie (vor dem Testmodus geprüft)
        if (_ceRunCts is not null) throw new InvalidOperationException("Es läuft schon ein Programm. Warte, bis es fertig ist – oder klick auf „Abbrechen“.");
        if (_opt.Isolated && CodeRunOverride is null && !CodeRealToolsInTests) { why = "Testmodus: Trendo startet keine Programme."; return false; }
        return true;
    }

    /// <summary>Erkanntes Projekt im geöffneten Ordner.</summary>
    public DevBuild.Project CodeProjectInfo => DevBuild.Detect(_ceRoot ?? throw new InvalidOperationException("Erst einen Projektordner öffnen."));

    void CodeRun(bool tests) { if (tests) CodeTest(); else CodeRunProject(); }

    /// <summary>„Ausführen“: Python-, JavaScript- oder PowerShell-Datei direkt, sonst das ganze Projekt (dotnet run, main.py, index.js, Web-App über ihren Server).</summary>
    public void CodeRunProject()
    {
        var root = _ceRoot ?? throw new InvalidOperationException("Erst einen Projektordner öffnen.");
        if (!CodeMayStart(out var why)) { CodeSave(all: true); _ceTab = "ausgabe"; _ceOut = why; return; }
        CodeSave(all: true); _ceTab = "ausgabe";
        DevBuild.Cmd cmd;
        if (CodeRunOverride is { } forced) cmd = new(forced.Exe, forced.Args, forced.Cwd);
        else
        {
            var p = DevBuild.Detect(root);
            var ext = Path.GetExtension(CodeActiveFile ?? "").ToLowerInvariant();
            if (p.Kind != DevBuild.Kind.Web && ext is ".py" or ".js" or ".mjs" or ".ps1" && CodeProject.RunCommand(root, CodeActiveFile!) is { } fc)
            {
                if (ext == ".py" && DevBuild.Missing("python") is { } m) { _ceOut = "✗ " + m; return; }
                if (ext is ".js" or ".mjs" && DevBuild.Missing("node") is { } mn) { _ceOut = "✗ " + mn; return; }
                cmd = new(fc.Exe, fc.Args, fc.Cwd, ext == ".py" ? new Dictionary<string, string> { ["PYTHONIOENCODING"] = "utf-8" } : null, ext == ".py");
            }
            else
            {
                var plan = DevBuild.Run(p);
                if (plan.Note == "browser")
                {
                    var index = Path.Combine(root, "index.html");
                    if (!_opt.Isolated) Launch(index);
                    _ceOut = $"„{Path.GetFileName(index)}“ im Browser geöffnet. (Mit Node startet „Ausführen“ den kleinen Server des Projekts.)"; return;
                }
                if (plan.Cmd is not { } pc) { _ceOut = (plan.Blocked is not null ? "✗ " + plan.Blocked : plan.Note) ?? ""; return; }
                cmd = pc;
            }
        }
        CodeStartCmd("run", cmd, 120000, "Programm läuft …");
    }

    /// <summary>„Bauen“: .NET übersetzt, Python/Node prüfen die Schreibweise. Fehler landen in der Fehlerliste.</summary>
    public void CodeBuild()
    {
        var root = _ceRoot ?? throw new InvalidOperationException("Erst einen Projektordner öffnen.");
        CodeSave(all: true); _ceTab = "ausgabe";
        if (!CodeMayStart(out var why)) { _ceOut = why; return; }
        var plan = DevBuild.Build(DevBuild.Detect(root));
        if (plan.Cmd is not { } cmd) { _ceOut = (plan.Blocked is not null ? "✗ " + plan.Blocked : plan.Note) ?? ""; return; }
        CodeStartCmd("build", cmd, 600000, "Wird gebaut …");
    }

    string? _ceResults;
    /// <summary>„Testen“: echter Testlauf (dotnet test, python -m unittest/pytest, node --test), danach je Test bestanden/fehlgeschlagen.</summary>
    public void CodeTest()
    {
        var root = _ceRoot ?? throw new InvalidOperationException("Erst einen Projektordner öffnen.");
        CodeSave(all: true); _ceTab = "ausgabe";
        if (!CodeMayStart(out var why)) { _ceOut = why; return; }
        DevBuild.Cmd cmd;
        if (CodeRunOverride is { } forced) { cmd = new(forced.Exe, forced.Args, forced.Cwd); _ceResults = null; }
        else
        {
            _ceResults = Path.Combine(_core.DataRoot, "code-editor", "testlaeufe", Guid.NewGuid().ToString("N")[..12]);
            var plan = DevBuild.Test(DevBuild.Detect(root), _ceResults);
            if (plan.Cmd is not { } pc) { _ceOut = "✗ Exit -1\n" + (plan.Blocked ?? plan.Note ?? "Für dieses Projekt kennt Trendo keinen Test."); return; }
            Directory.CreateDirectory(_ceResults); cmd = pc;
        }
        CodeStartCmd("test", cmd, 600000, "Test läuft …");
    }

    /// <summary>„Paket bauen“: eigenständiges Programm bzw. ZIP mit Startskript, danach Startprobe aus einem leeren Ordner.</summary>
    public void CodePublish()
    {
        var root = _ceRoot ?? throw new InvalidOperationException("Erst einen Projektordner öffnen.");
        CodeSave(all: true); _ceTab = "ausgabe";
        if (!CodeMayStart(out var why)) { _ceOut = why; return; }
        var p = DevBuild.Detect(root);
        CodeStart("publish", "Paket wird gebaut … (beim ersten Mal lädt .NET noch Bestandteile nach, das dauert)", async (line, ct) =>
        {
            var pkg = await DevBuild.PublishAsync(p, line, ct);
            _ui.Post(() => _cePkg = pkg);
            return new Cli.Result(pkg.Ok && pkg.ProbeOk ? 0 : pkg.Message == "Abgebrochen." ? Cli.Cancelled : 1, "", pkg.Ok ? "" : pkg.Message);
        });
    }

    /// <summary>Terminal: ein Befehl in der Eingabeaufforderung (Windows) bzw. sh, Arbeitsordner = Projekt. Trendo filtert nichts, protokolliert aber jeden Start.</summary>
    public void CodeTerminal(string command)
    {
        var root = _ceRoot ?? throw new InvalidOperationException("Erst einen Projektordner öffnen.");
        command = command.Trim();
        if (command.Length == 0) return;
        _ceTab = "terminal";
        if (!DarfAusfuehren("terminal", out var notAllowed)) { _ceTerm += notAllowed + "\n"; return; } // T-0764 (H)
        if (_ceRunCts is not null) throw new InvalidOperationException("Es läuft schon ein Programm. Warte, bis es fertig ist – oder klick auf „Abbrechen“.");
        _ceTerm += $"{Path.GetFileName(root)}> {command}\n";
        if (_opt.Isolated && !CodeRealToolsInTests) { _ceTerm += "Testmodus: Trendo startet keine Programme.\n"; return; }
        _core.Audit.Append(CurrentPrincipal.Name, "code.terminal", $"{Path.GetFileName(root)}: {(command.Length > 200 ? command[..200] + " …" : command)}");
        DevBuild.Cmd cmd;
        if (OperatingSystem.IsWindows())
        {
            // cmd versteht die Maskierung von Anführungszeichen in Programm-Argumenten nicht – der Befehl kommt deshalb unverändert in eine Skriptdatei
            var script = Path.Combine(_core.DataRoot, "code-editor", "terminal.cmd"); Directory.CreateDirectory(Path.GetDirectoryName(script)!);
            File.WriteAllText(script, $"@echo off\r\nchcp 65001 >nul\r\ncd /d \"{root}\"\r\n{command}\r\n", new System.Text.UTF8Encoding(false));
            cmd = new DevBuild.Cmd("cmd", ["/d", "/c", script], root, new Dictionary<string, string> { ["PYTHONIOENCODING"] = "utf-8" }, true);
        }
        else cmd = new DevBuild.Cmd("sh", ["-c", command], root);
        CodeStartCmd("terminal", cmd, 1800000, "");
    }

    void CodeStartCmd(string what, DevBuild.Cmd cmd, int timeoutMs, string head)
    {
        var env = cmd.Env is null ? null : new Dictionary<string, string>(cmd.Env);
        CodeStart(what, head, (line, ct) =>
        {
            if (what != "terminal") line("$ " + cmd.Display);
            return Cli.RunStreamAsync(cmd.Exe, cmd.Args, cmd.Cwd, line, ct, timeoutMs, pid => _ceRunPid = pid, env, cmd.Utf8 ? new System.Text.UTF8Encoding(false) : null);
        });
    }

    void CodeStart(string what, string head, Func<Action<string>, CancellationToken, Task<Cli.Result>> work)
    {
        var root = _ceRoot!;
        var cts = _ceRunCts = new CancellationTokenSource(); _ceRunPid = 0; _ceRunWhat = what;
        if (what != "terminal") { _ceOut = head + " („Abbrechen“ beendet es.)\n"; if (what is "test" or "build") _ceProblems = []; if (what == "test") _ceTests = null; if (what == "publish") _cePkg = null; }
        _ceRunTask = CodeRunInBackground(what, root, work, cts);
    }

    async Task CodeRunInBackground(string what, string root, Func<Action<string>, CancellationToken, Task<Cli.Result>> work, CancellationTokenSource cts)
    {
        var term = what == "terminal";
        var streamed = new System.Text.StringBuilder(); // was schon laufend angezeigt wurde – bleibt auch nach „Abbrechen“ stehen
        // Ausgabe laufend: Zeilen kommen aus dem Hintergrund und werden in die Ausgabe geschrieben, ohne die Seite neu zu bauen
        void Line(string l) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (streamed.Length < CeOutMax) streamed.Append(l).Append('\n');
            if (!ReferenceEquals(_ceRunCts, cts)) return;
            if (term) { _ceTerm += l + "\n"; if (_ceTerm.Length > CeOutMax) _ceTerm = "… (Anfang gekürzt)\n" + _ceTerm[^(CeOutMax / 2)..]; if (_ceTermBox is { } tb) tb.Text = _ceTerm; return; }
            _ceOut += l + "\n"; if (_ceOut.Length > CeOutMax) _ceOut = "… (Anfang gekürzt)\n" + _ceOut[^(CeOutMax / 2)..];
            if (_ceOutBox is { } box) box.Text = _ceOut;
            // Web-App: sobald der Server bereit ist, die Seite im Browser zeigen
            if (what == "run" && !_opt.Isolated && System.Text.RegularExpressions.Regex.Match(l, @"^Bereit: (http://127\.0\.0\.1:\d+)") is { Success: true } m) Launch(m.Groups[1].Value);
        });
        Cli.Result r;
        try { r = await Task.Run(() => work(Line, cts.Token)); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or OperationCanceledException or UnauthorizedAccessException) { r = new(-1, "", ex.Message); }
        await _ui.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background); // ausstehende Zeilen zuerst
        // ab hier wieder im Oberflächen-Thread
        var cancelled = r.Exit == Cli.Cancelled || cts.IsCancellationRequested; // „Abbrechen“ kann auch als Ausnahme ankommen
        _ceRunCts = null; cts.Dispose();
        var action = what switch { "test" => "code.test", "build" => "code.build", "publish" => "code.publish", "terminal" => "code.terminal.ende", _ => "code.run" };
        _core.Audit.Append(Principal.LocalOwner.Name, action, $"{Path.GetFileName(root)} exit={r.Exit}{(cancelled ? " abgebrochen" : "")}");
        if (term) { _ceTerm += (r.Err.Length > 0 && !cancelled ? r.Err.TrimEnd() + "\n" : "") + (cancelled ? "■ Abgebrochen.\n" : $"(Exit {r.Exit})\n"); if (CurrentView == CEK) Show(CEK); return; }
        var body = (r.Out + "\n" + r.Err).Trim();
        // Abgebrochen: das Ergebnis des Prozesses ist dann oft leer – die schon laufend gezeigten Zeilen dürfen nicht verschwinden
        if (cancelled && streamed.Length > 0 && !r.Out.Contains(streamed.ToString().Trim(), StringComparison.Ordinal)) body = (streamed.ToString() + (r.Err.Length > 0 && r.Err != r.Out ? "\n" + r.Err : "")).Trim();
        if (body.Length > CeOutMax) body = "… (Anfang gekürzt)\n" + body[^(CeOutMax / 2)..];
        if (what == "publish") body = _ceOut.Trim() + (r.Err.Length > 0 ? "\n" + r.Err : "");
        if (what is "build" or "test" or "run") _ceProblems = DevBuild.Problems(body, root);
        string head;
        if (what == "test" && !cancelled)
        {
            var p = DevBuild.Detect(root);
            _ceTests = DevBuild.ReadResults(p, _ceResults, r.Out + "\n" + r.Err);
            if (_ceResults is { } rd) try { Directory.Delete(rd, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            head = _ceTests.Cases.Count == 0 ? (r.Ok ? "✓ Fertig (Exit 0)" : $"✗ Exit {r.Exit}") : r.Ok && _ceTests.Failed == 0 ? $"✓ Tests bestanden: {_ceTests.Summary}" : $"✗ Tests nicht bestanden: {_ceTests.Summary} (Exit {r.Exit})";
            if (_ceTests.Cases.Count > 0) _ceTab = "tests";
        }
        else if (what == "publish" && _cePkg is { } pkg)
        {
            head = pkg.Ok && pkg.ProbeOk ? $"✓ Paket fertig ({DevBuild.Size(pkg.ZipBytes)} ZIP) – Startprobe bestanden" : pkg.Ok ? "✗ Paket fertig, aber die Startprobe ist nicht gelungen" : cancelled ? "■ Abgebrochen – das Verpacken wurde beendet." : "✗ " + pkg.Message;
            if (pkg.Ok) _ceTab = "paket";
        }
        else head = r.Ok ? "✓ Fertig (Exit 0)" : cancelled ? "■ Abgebrochen – das Programm wurde beendet." : $"✗ Exit {r.Exit}";
        if (cancelled && what != "publish") head = "■ Abgebrochen – das Programm wurde beendet.";
        if (what == "build" && !cancelled) head = r.Ok ? $"✓ Gebaut (Exit 0){(_ceProblems.Count > 0 ? $" – {_ceProblems.Count} Hinweise" : "")}" : $"✗ Bauen fehlgeschlagen (Exit {r.Exit}) – {_ceProblems.Count(x => x.Error)} Fehler";
        if (!r.Ok && !cancelled && _ceProblems.Any(x => x.Error) && what != "test") _ceTab = "fehler";
        _ceOut = head + "\n" + body;
        if (CurrentView == CEK) Show(CEK);
    }

    /// <summary>„Abbrechen“: beendet das laufende Programm samt Unterprozessen.</summary>
    public void CodeStop()
    {
        if (_ceRunCts is not { } cts) return;
        if (_ceRunWhat == "terminal") _ceTerm += "Wird abgebrochen …\n"; else _ceOut += "Wird abgebrochen …\n";
        try { cts.Cancel(); } catch (ObjectDisposedException) { }
    }

    /// <summary>Zur Stelle einer Meldung springen (nur Dateien im Projekt).</summary>
    public void CodeJump(string file, int line)
    {
        if (_ceRoot is null || !Trendo.Core.Kreativ.CodeAiKit.InsideNoLinks(_ceRoot, file) || !File.Exists(file)) throw new InvalidOperationException("Die Stelle liegt nicht im geöffneten Projekt.");
        CodeOpenFile(file); Show(CEK); CodeGoto(line);
    }

    // ───────────── Entwürfe: Wiederherstellen nach Absturz (T-0688) ─────────────

    Avalonia.Threading.DispatcherTimer? _ceDraftTimer;
    /// <summary>Wartezeit nach dem letzten Tastendruck, bis der Entwurf geschrieben wird.</summary>
    public static TimeSpan CodeDraftDelay { get; set; } = TimeSpan.FromSeconds(2);
    string CeAutoSaveFlag => Path.Combine(_core.DataRoot, "code-editor", "automatisch-speichern");
    /// <summary>„Automatisch speichern“: schreibt nach jeder Pause direkt in die Dateien statt nur einen Entwurf.</summary>
    public bool CodeAutoSave
    {
        get => File.Exists(CeAutoSaveFlag);
        set { if (value) { Directory.CreateDirectory(Path.GetDirectoryName(CeAutoSaveFlag)!); File.WriteAllText(CeAutoSaveFlag, "ja"); } else if (File.Exists(CeAutoSaveFlag)) File.Delete(CeAutoSaveFlag); }
    }

    void CodeDraftSoon()
    {
        _ceDraftTimer ??= new Avalonia.Threading.DispatcherTimer(CodeDraftDelay, Avalonia.Threading.DispatcherPriority.Background, (_, _) => { _ceDraftTimer!.Stop(); CodeDraftFlush(); });
        _ceDraftTimer.Stop(); _ceDraftTimer.Interval = CodeDraftDelay; _ceDraftTimer.Start();
    }

    /// <summary>Entwürfe aller geänderten Dateien jetzt schreiben (bzw. mit „Automatisch speichern“ die Dateien selbst).</summary>
    public void CodeDraftFlush()
    {
        if (_ceRoot is null) return;
        if (CodeAutoSave && _ceFiles.Any(f => f.Dirty)) { try { CodeSave(all: true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } return; }
        // T-0726: mit der Sperre der Programm-Hülle – ein zweites Trendo-Fenster überschreibt oder löscht den Entwurf dieses Fensters nicht
        var busy = new List<string>();
        foreach (var f in _ceFiles)
            try
            {
                if (!f.Dirty) CodeDrafts.Delete(_core.DataRoot, f.Path, ProgLocks);
                else if (CodeDrafts.Save(_core.DataRoot, _ceRoot, f.Path, f.Doc.Text, CodeDrafts.ShaOf(f.Path), locks: ProgLocks) is not null) busy.Add(Path.GetFileName(f.Path));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        CodeDraftBusy = busy;
        if (busy.Count > 0) _ceOut = $"Achtung: {string.Join(", ", busy)} ist auch in einem anderen Trendo-Fenster geändert – dessen Sicherung bleibt erhalten, hier mit Strg+S speichern.";
    }

    /// <summary>T-0726: Dateien, deren Entwurf gerade ein anderes Trendo-Fenster hält (letzter Durchlauf).</summary>
    public IReadOnlyList<string> CodeDraftBusy { get; private set; } = [];

    /// <summary>Entwürfe von früher (z. B. nach einem Absturz), die noch nicht offen sind.</summary>
    public List<CodeDrafts.Draft> CodePendingDrafts() => CodeDrafts.All(_core.DataRoot, ProgLocks).Where(d => _ceFiles.All(f => !string.Equals(f.Path, d.File, StringComparison.OrdinalIgnoreCase))).ToList();

    /// <summary>Entwürfe wiederherstellen: Projekt und Dateien öffnen, Text des Entwurfs einsetzen (ungespeichert, ●).</summary>
    public int CodeRestoreDrafts()
    {
        var n = 0; var changed = new List<string>();
        foreach (var d in CodePendingDrafts())
        {
            if (!Directory.Exists(d.Root)) continue;
            if (_ceRoot is null || !string.Equals(_ceRoot, d.Root, StringComparison.OrdinalIgnoreCase)) { if (_ceRoot is not null) continue; CodeOpenFolder(d.Root); }
            if (!File.Exists(d.File)) { Directory.CreateDirectory(Path.GetDirectoryName(d.File)!); File.WriteAllText(d.File, ""); }
            CodeOpenFile(d.File);
            var f = _ceFiles[_ceActive]; f.Doc.Text = d.Text; n++;
            if (d.DiskSha.Length > 0 && CodeDrafts.ShaOf(d.File) != d.DiskSha) changed.Add(Path.GetFileName(d.File));
        }
        _ceOut = n == 0 ? "Nichts wiederherzustellen." : $"{n} Dateien mit dem letzten ungespeicherten Stand wiederhergestellt – mit Strg+S speichern." + (changed.Count > 0 ? $" Achtung: {string.Join(", ", changed)} wurde inzwischen auch anders geändert – vor dem Speichern prüfen." : "");
        _core.Audit.Append(Principal.LocalOwner.Name, "code.wiederherstellen", n.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return n;
    }

    public void CodeDiscardDrafts() { foreach (var d in CodePendingDrafts()) CodeDrafts.Delete(_core.DataRoot, d.File, ProgLocks); _ceOut = "Alte Entwürfe verworfen."; }

    /// <summary>Tastenkürzel: F5 Ausführen, Umschalt+F5 Abbrechen, Strg+Umschalt+B Bauen, Strg+Umschalt+T Testen.</summary>
    void CodeKeys(KeyEventArgs e)
    {
        var ctrlShift = e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        Action? a = e.Key switch
        {
            Key.F5 when e.KeyModifiers.HasFlag(KeyModifiers.Shift) => CodeStop,
            Key.F5 when !CodeRunning => CodeRunProject,
            Key.B when ctrlShift && !CodeRunning => CodeBuild,
            Key.T when ctrlShift && !CodeRunning => CodeTest,
            _ => null,
        };
        if (a is null) return;
        e.Handled = true; Try(CEK, () => { a(); Show(CEK); });
    }

    /// <summary>Neues Projekt aus einer Vorlage im Ordner des Software-Studios anlegen und gleich öffnen.</summary>
    public string CodeNewProject(string templateId, string name)
    {
        var dir = DevStudio.Create(DevFolder, templateId, string.IsNullOrWhiteSpace(name) ? "Mein Projekt" : name);
        if (Cli.Find("git") is not null) DevStudio.GitInit(dir);
        _core.Audit.Append(Principal.LocalOwner.Name, "dev.create", templateId);
        CodeOpenFolder(dir);
        _ceOut = $"Projekt „{Path.GetFileName(dir)}“ angelegt ({DevStudio.Templates.First(t => t.Id == templateId).Name}). „Ausführen“ startet es, „Testen“ prüft es, „Paket bauen“ macht ein fertiges Programm daraus.";
        return dir;
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
        ed.TextChanged += (_, _) => { CodeTitleRefresh(); CodeDraftSoon(); };
        ed.TextArea.Caret.PositionChanged += (_, _) => { if (_cePos is { } p) p.Text = $"Zeile {ed.TextArea.Caret.Line}, Spalte {ed.TextArea.Caret.Column}"; };
        ed.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.S) { e.Handled = true; Try(CEK, () => { CodeSave(e.KeyModifiers.HasFlag(KeyModifiers.Shift)); Show(CEK); }); }
            else CodeKeys(e);
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
            if (primary) { b.Background = Accent; b.Foreground = Brushes.Black; }
            ToolTip.SetTip(b, tip); b.Click += (_, _) => Try(CEK, () => { a(); Show(CEK); }); return b;
        }
        // Werkzeugleiste
        var bar = new WrapPanel { Name = "ce-toolbar", Margin = new Thickness(0, 0, 0, 8) };
        var folderBtn = new Button { Name = "ce-open-folder", Content = "Ordner öffnen …", FontSize = 12.5, Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(7), Padding = new Thickness(10, 5) };
        if (_ceRoot is null) { folderBtn.Background = Accent; folderBtn.Foreground = Brushes.Black; } // im Leerzustand ist das der erste Schritt
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
        // T-0708 (V-14): Speichern · Rückgängig · Wiederholen am gemeinsamen Platz links oben (Rückgängig des Editors: je Datei, wie in VS Code)
        var ceDoc = _ceActive >= 0 && _ceActive < _ceFiles.Count ? _ceFiles[_ceActive].Doc : null;
        var ceUndo = Btn("ce-undo", "Rückgängig", "Strg+Z", () => ceDoc?.UndoStack.Undo()); ceUndo.IsEnabled = ceDoc?.UndoStack.CanUndo == true;
        var ceRedo = Btn("ce-redo", "Wiederholen", "Strg+Y", () => ceDoc?.UndoStack.Redo()); ceRedo.IsEnabled = ceDoc?.UndoStack.CanRedo == true;
        if (ceDoc is not null) ceDoc.UndoStack.PropertyChanged += (_, _) => { ceUndo.IsEnabled = ceDoc.UndoStack.CanUndo; ceRedo.IsEnabled = ceDoc.UndoStack.CanRedo; };
        bar.Children.Insert(0, DocActions(Btn("ce-save", "Speichern", "Strg+S", () => CodeSave()), ceUndo, ceRedo));
        bar.Children.Add(Btn("ce-save-all", "Alle speichern", "Strg+Umschalt+S", () => CodeSave(true)));
        var runBtn = Btn("ce-run", "▶ Ausführen", "F5 – Python-, JavaScript- oder PowerShell-Datei starten, sonst das ganze Projekt (dotnet run, main.py, Web-App)", () => CodeRun(false), _ceRoot is not null);
        var buildBtn = Btn("ce-build", "Bauen", "Strg+Umschalt+B – .NET übersetzen, Python/Node auf Schreibfehler prüfen", CodeBuild);
        var testBtn = Btn("ce-test", "✓ Testen", "Strg+Umschalt+T – echte Tests (dotnet test, python -m unittest, node --test), Ergebnis je Test", () => CodeRun(true));
        var pkgBtn = Btn("ce-publish", "Paket bauen", "Fertiges Programm: .NET eigenständig als eine Datei, Python/Node als ZIP mit Startskript, Web-App als Ordner und ZIP – danach eine Startprobe", CodePublish);
        bar.Children.Add(runBtn); bar.Children.Add(buildBtn); bar.Children.Add(testBtn); bar.Children.Add(pkgBtn);
        if (CodeRunning)
        {
            runBtn.IsEnabled = testBtn.IsEnabled = buildBtn.IsEnabled = pkgBtn.IsEnabled = false;
            var stop = Btn("ce-stop", "■ Abbrechen", "Das laufende Programm beenden", CodeStop);
            stop.Background = Brush.Parse("#7F1D1D"); stop.Foreground = Brushes.White; bar.Children.Add(stop);
        }
        bar.Children.Add(Btn("ce-find", "Suchen", "In der Datei: Strg+F. Hier: im ganzen Projekt", () => _ceTab = "suche"));
        bar.Children.Add(Btn("ce-git", "Git", "Änderungen ansehen und als Stand speichern", () => _ceTab = "git"));
        var auto = new CheckBox { Name = "ce-autosave", Content = "Automatisch speichern", IsChecked = CodeAutoSave, FontSize = 12, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(auto, "An: jede Pause beim Tippen speichert die Dateien. Aus: Trendo merkt sich ungespeicherte Änderungen trotzdem und bietet sie nach einem Absturz an.");
        auto.IsCheckedChanged += (_, _) => Try(CEK, () => CodeAutoSave = auto.IsChecked == true);
        bar.Children.Add(auto);

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
                var b = new Button { Name = $"ce-node-{i++}", Content = new TextBlock { Text = $"{icon}  {e.Name}" } /* Text statt Zeichenkette: „_“ ist sonst ein Tastenkürzel */, Background = active ? Brush.Parse("#2A2410") : Brushes.Transparent, Foreground = active ? gold : open ? Text : Brush.Parse("#B8C0CC"),
                    BorderThickness = new Thickness(0), FontSize = 12.5, Padding = new Thickness(6 + e.Depth * 14, 3, 6, 3), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(5) };
                ToolTip.SetTip(b, Path.GetRelativePath(_ceRoot, e.Path));
                b.Click += (_, _) => Try(CEK, () => { if (en.IsDir) { if (!_ceCollapsed.Remove(en.Path)) _ceCollapsed.Add(en.Path); } else CodeOpenFile(en.Path); Show(CEK); });
                var menu = new ContextMenu();
                if (!en.IsDir) { var mo = new MenuItem { Header = "Öffnen" }; mo.Click += (_, _) => Try(CEK, () => { CodeOpenFile(en.Path); Show(CEK); }); menu.Items.Add(mo); }
                var mf = new MenuItem { Header = "Im Ordner zeigen" }; mf.Click += (_, _) => Launch(en.IsDir ? en.Path : Path.GetDirectoryName(en.Path)!); menu.Items.Add(mf);
                var mt = new MenuItem { Header = "Terminal hier" }; mt.Click += (_, _) => { _ceTab = "terminal"; Show(CEK); }; menu.Items.Add(mt);
                b.ContextMenu = menu;
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
            Child = new ScrollViewer { Content = new StackPanel { Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 6, Children = {
                new TextBlock { Text = "</>", FontSize = _ceRoot is null ? 30 : 46, Foreground = gold, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = _ceRoot is null ? "Neues Projekt anlegen – oder oben einen Ordner öffnen" : "Datei links wählen", Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center },
                CodeNewProjectBox() } } } };
        var mid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        mid.Children.Add(new ScrollViewer { Content = tabs, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Grid.SetRow(center, 1); mid.Children.Add(center);

        // Unten: Ausgabe / Suche / Git
        var bottomTabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        var testLbl = _ceTests is { Cases.Count: > 0 } tr ? $"Tests ({tr.Passed}✓ {tr.Failed}✗)" : "Tests";
        var errLbl = _ceProblems.Count > 0 ? $"Fehler ({_ceProblems.Count(x => x.Error)})" : "Fehler";
        foreach (var (id, lbl) in new[] { ("ausgabe", "Ausgabe"), ("tests", testLbl), ("fehler", errLbl), ("terminal", "Terminal"), ("paket", "Paket"), ("suche", "Suche im Projekt"), ("git", "Git") })
        {
            var on = _ceTab == id; var ii = id;
            var b = new Button { Name = $"ce-panel-{id}", Content = lbl, FontSize = 12, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 4, 0), Background = on ? Brush.Parse("#2A2410") : Brushes.Transparent, Foreground = on ? gold : Muted, BorderThickness = new Thickness(0) };
            b.Click += (_, _) => { _ceTab = ii; Show(CEK); }; bottomTabs.Children.Add(b);
        }
        _ceOutBox = null; _ceTermBox = null;
        Control body = _ceTab switch { "suche" => CodeSearchPanel(), "git" => CodeGitPanel(), "tests" => CodeTestsPanel(), "fehler" => CodeProblemsPanel(), "terminal" => CodeTerminalPanel(), "paket" => CodePackagePanel(), _ => _ceOutBox = new SelectableTextBlock { Name = "ce-output", Text = _ceOut.Length == 0 ? "Hier erscheinen Ausgaben von „Ausführen“ und „Testen“." : _ceOut,
            FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"), FontSize = 12, Foreground = _ceOut.StartsWith('✗') ? Brush.Parse("#FCA5A5") : Brush.Parse("#C9D1D9"), TextWrapping = TextWrapping.Wrap } };
        var bottom = new Border { Name = "ce-bottom", Height = 220, Background = panelBg, BorderBrush = edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 6), Margin = new Thickness(0, 8, 0, 0),
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
        if (CodePendingDrafts() is { Count: > 0 } drafts)
        {
            var yes = Btn("ce-restore-yes", "Wiederherstellen", "Öffnet die Dateien mit dem ungespeicherten Stand", () => CodeRestoreDrafts(), true);
            var no = Btn("ce-restore-no", "Verwerfen", "Die alten Entwürfe löschen (die Dateien selbst bleiben, wie sie sind)", CodeDiscardDrafts);
            head.Children.Insert(0, new Border { Name = "ce-restore", Background = Brush.Parse("#2A2410"), BorderBrush = gold, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 0, 8),
                Child = new DockPanel { Children = { new StackPanel { [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right, Orientation = Orientation.Horizontal, Children = { yes, no } },
                    new TextBlock { Text = $"Ungespeicherte Änderungen von vorher gefunden: {string.Join(", ", drafts.Take(4).Select(d => Path.GetFileName(d.File)))}{(drafts.Count > 4 ? " …" : "")} (Stand {drafts[0].SavedUtc.ToLocalTime():dd.MM. HH:mm}).", Foreground = Text, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap } } } });
            folderBtn.ClearValue(Button.BackgroundProperty); folderBtn.ClearValue(Button.ForegroundProperty); // genau eine goldene Hauptaktion
            runBtn.ClearValue(Button.BackgroundProperty); runBtn.ClearValue(Button.ForegroundProperty);
        }
        return FitPage("Code-Editor", head, work);
    }

    Control CodeNewProjectBox()
    {
        if (_ceRoot is not null) return new Panel();
        var name = new TextBox { Name = "ce-np-name", PlaceholderText = "Name, z. B. Mein Rechner", Width = 200, FontSize = 12.5, Margin = new Thickness(0, 0, 6, 6) };
        var tpl = new ComboBox { Name = "ce-np-template", ItemsSource = DevStudio.Templates.Select(t => t.Name).ToList(), SelectedIndex = 0, Width = 300, FontSize = 12.5, Margin = new Thickness(0, 0, 6, 6) };
        var make = new Button { Name = "ce-np-create", Content = "Projekt anlegen", FontSize = 12.5, Padding = new Thickness(12, 5), CornerRadius = new CornerRadius(7) };
        make.Click += (_, _) => Try(CEK, () => { CodeNewProject(DevStudio.Templates[Math.Max(0, tpl.SelectedIndex)].Id, name.Text ?? ""); Show(CEK); });
        return new StackPanel { Spacing = 6, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, Children = { new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Children = { tpl, name, make } },
            new TextBlock { Text = "Jede Vorlage bringt eine kleine echte Funktion und Tests mit.", Foreground = Muted, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center } } };
    }

    Button CeRow(string name, string text, IBrush fg, Action? click)
    {
        var b = new Button { Name = name, Content = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis }, FontSize = 11.5, FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 1), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Foreground = fg };
        if (click is not null) b.Click += (_, _) => Try(CEK, click);
        return b;
    }

    Control CodeTestsPanel()
    {
        var sp = new StackPanel { Name = "ce-tests", Spacing = 1 };
        if (_ceTests is not { } t) { sp.Children.Add(P(CodeRunning && _ceRunWhat == "test" ? "Tests laufen …" : "Noch kein Testlauf. „✓ Testen“ startet die Tests des Projekts.", Muted, 12)); return sp; }
        sp.Children.Add(P(t.Summary, t.Failed == 0 && t.Cases.Count > 0 ? Brush.Parse("#4ADE80") : Brush.Parse("#FCA5A5"), 12));
        var i = 0;
        foreach (var c in t.Cases.Take(400))
        {
            var cc = c; var where = c.File is { } f && _ceRoot is not null ? $"  ({Path.GetRelativePath(_ceRoot, f)}:{c.Line})" : "";
            var msg = c.Message.Replace('\n', ' ').Replace('\r', ' ');
            var text = (c.Passed ? "✓ " : c.Skipped ? "– " : "✗ ") + c.Name + (c.Passed ? "" : (msg.Length > 0 ? " — " + (msg.Length > 140 ? msg[..140] + " …" : msg) : "") + where);
            sp.Children.Add(CeRow($"ce-test-{i++}", text, c.Passed ? Brush.Parse("#9FD8B0") : c.Skipped ? Muted : Brush.Parse("#FCA5A5"), c.File is { } ff ? () => CodeJump(ff, cc.Line) : null));
        }
        return sp;
    }

    Control CodeProblemsPanel()
    {
        var sp = new StackPanel { Name = "ce-problems", Spacing = 1 };
        if (_ceProblems.Count == 0) { sp.Children.Add(P("Keine Fehler. Fehler aus „Bauen“, „Testen“ und „Ausführen“ erscheinen hier – ein Klick springt zur Stelle.", Muted, 12)); return sp; }
        var i = 0;
        foreach (var pr in _ceProblems.Take(300))
        {
            var pp = pr; var rel = _ceRoot is not null && Trendo.Core.Kreativ.CodeAiKit.InsideNoLinks(_ceRoot, pr.File) ? Path.GetRelativePath(_ceRoot, pr.File) : Path.GetFileName(pr.File);
            sp.Children.Add(CeRow($"ce-problem-{i++}", $"{(pr.Error ? "✗" : "⚠")} {rel}:{pr.Line}:{pr.Column}  {pr.Code}  {pr.Message}", pr.Error ? Brush.Parse("#FCA5A5") : Brush.Parse("#E3B341"), () => CodeJump(pp.File, pp.Line)));
        }
        return sp;
    }

    Control CodeTerminalPanel()
    {
        var sp = new StackPanel { Spacing = 4 };
        if (_ceRoot is null) { sp.Children.Add(P("Erst einen Projektordner öffnen.", Muted, 12)); return sp; }
        sp.Children.Add(P($"Befehle laufen im Projektordner „{Path.GetFileName(_ceRoot)}“ ({(OperatingSystem.IsWindows() ? "Eingabeaufforderung" : "sh")}). Jeder Start wird im Protokoll vermerkt. „■ Abbrechen“ beendet den Befehl.", Muted, 11));
        var input = new TextBox { Name = "ce-term-input", PlaceholderText = "Befehl eingeben und Enter, z. B. dotnet --info oder python --version", FontSize = 12.5, FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"), IsEnabled = !CodeRunning };
        input.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; var c = input.Text ?? ""; Try(CEK, () => { CodeTerminal(c); Show(CEK); }); } };
        sp.Children.Add(input);
        sp.Children.Add(_ceTermBox = new SelectableTextBlock { Name = "ce-term-out", Text = _ceTerm, FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"), FontSize = 12, Foreground = Brush.Parse("#C9D1D9"), TextWrapping = TextWrapping.Wrap });
        return sp;
    }

    Control CodePackagePanel()
    {
        var sp = new StackPanel { Name = "ce-package", Spacing = 3 };
        if (_cePkg is not { } k) { sp.Children.Add(P(CodeRunning && _ceRunWhat == "publish" ? "Paket wird gebaut …" : "Noch kein Paket. „Paket bauen“ macht aus dem Projekt ein fertiges Programm und startet es einmal zur Probe.", Muted, 12)); return sp; }
        if (!k.Ok) { sp.Children.Add(P("✗ " + k.Message, Brush.Parse("#FCA5A5"), 12)); return sp; }
        sp.Children.Add(P(k.Kind, Text, 12.5));
        sp.Children.Add(P($"Ordner: {k.Folder}  ·  {DevBuild.Size(k.Bytes)}", Muted, 11.5));
        sp.Children.Add(P($"ZIP: {k.Zip}  ·  {DevBuild.Size(k.ZipBytes)}  ·  SHA-256 {k.Sha256}", Muted, 11.5));
        sp.Children.Add(P((k.ProbeOk ? "✓ Startprobe: " : "✗ Startprobe: ") + k.Probe, k.ProbeOk ? Brush.Parse("#4ADE80") : Brush.Parse("#FCA5A5"), 12));
        var open = new Button { Name = "ce-package-open", Content = "Ordner öffnen", FontSize = 12, Margin = new Thickness(0, 4, 0, 0) };
        open.Click += (_, _) => Launch(k.Folder);
        sp.Children.Add(open);
        return sp;
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
                var b = new Button { Name = $"ce-hit-{i++}", Content = new TextBlock { Text = $"{Path.GetRelativePath(_ceRoot, h.File)}:{h.Line}   {(h.Text.Length > 110 ? h.Text[..110] + " …" : h.Text)}" }, FontSize = 11.5, FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
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
