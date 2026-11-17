using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0688: Software-Studio vom Klick bis zur echten Datei. Die Läufe mit „echt“ im Namen starten die echten Werkzeuge dieses PCs
/// (python, dotnet, Eingabeaufforderung) über <see cref="MainWindow.CodeRealToolsInTests"/> und werden übersprungen, wenn ein Werkzeug fehlt.
/// Claude ist in diesen Tests ERSETZT (<see cref="MainWindow.ClaudeRunOverride"/>) – der Echtlauf steht in reports/T-0688.</summary>
public class T0688_SoftwareStudio_Ui
{
    static string Py => OperatingSystem.IsWindows() ? "python" : "python3";

    static async Task Until(Func<bool> cond, int seconds = 120)
    {
        var sw = Stopwatch.StartNew();
        while (!cond()) { Dispatcher.UIThread.RunJobs(); if (sw.Elapsed > TimeSpan.FromSeconds(seconds)) break; await Task.Delay(40); }
        Dispatcher.UIThread.RunJobs();
    }

    static (MainWindow W, string Root) OpenAt(string root)
    {
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher(), offline: true);
        var w = new MainWindow(core, new AppOptions(root, null, 0, true, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Erweitert);
        w.Width = 1600; w.Height = 1000; w.Show(); Dispatcher.UIThread.RunJobs();
        return (w, root);
    }

    static List<string> Names(MainWindow w, string prefix) => w.GetLogicalDescendants().OfType<Control>().Select(c => c.Name ?? "").Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).ToList();

    [AvaloniaFact(DisplayName = "T-0688 Code-Editor (echt, Python): Projekt per Klick anlegen → Testen (je Test) → Fehler anklicken springt zur Zeile → Paket bauen mit Startprobe → Terminal mit Abbrechen")]
    public async Task Python_vom_Klick_bis_zum_Paket()
    {
        if (Cli.Find(Py) is null) { Assert.Skip("Übersprungen – " + "Python fehlt auf diesem PC."); return; }
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.CodeRealToolsInTests = true;
        try
        {
            w.Show("code"); Settle();
            // Leerzustand: erster Schritt sichtbar, Ordner öffnen ist die goldene Hauptaktion
            Assert.NotNull(w.TryFind<Button>("ce-np-create"));
            var tpl = w.Find<ComboBox>("ce-np-template"); tpl.SelectedIndex = DevStudio.Templates.ToList().FindIndex(t => t.Id == "python");
            w.Find<TextBox>("ce-np-name").Text = "Zahlen";
            { var nb = V<Button>(w, "ce-np-create"); nb.BringIntoView(); Settle(); RealClick(w, nb); Settle(); }
            var proj = w.CodeRoot!;
            Assert.True(File.Exists(Path.Combine(proj, "rechner.py")));
            Assert.StartsWith(w.DevFolder, proj);
            // Testen läuft im Hintergrund und zeigt jeden Test
            var sw = Stopwatch.StartNew();
            RealClick(w, V<Button>(w, "ce-test")); Settle();
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), "„Testen“ hat die Oberfläche blockiert.");
            await Until(() => !w.CodeRunning); Settle();
            Assert.Equal((6, 0), (w.CodeTests!.Passed, w.CodeTests.Failed));
            Assert.StartsWith("✓ Tests bestanden", w.CodeOutput);
            Assert.Equal(6, Names(w, "ce-test-").Count(n => n != "ce-test-"));
            // Fehler einbauen → ein Test schlägt fehl → Klick springt in die Testdatei zur Zeile
            var rechner = Path.Combine(proj, "rechner.py");
            File.WriteAllText(rechner, File.ReadAllText(rechner).Replace("return sum(zahlen)", "return sum(zahlen) + 1"));
            RealClick(w, V<Button>(w, "ce-test")); Settle();
            await Until(() => !w.CodeRunning); Settle();
            Assert.Equal(1, w.CodeTests!.Failed);
            Assert.StartsWith("✗ Tests nicht bestanden", w.CodeOutput);
            var bad = w.CodeTests.Cases.First(c => !c.Passed);
            RealClick(w, V<Button>(w, "ce-test-0")); Settle();
            Assert.EndsWith("test_rechner.py", w.CodeActiveFile);
            Assert.Equal(bad.Line, w.CodeEditorForTests!.TextArea.Caret.Line);
            File.WriteAllText(rechner, File.ReadAllText(rechner).Replace("return sum(zahlen) + 1", "return sum(zahlen)"));
            // Paket bauen: ZIP mit Startskript, Startprobe aus einem leeren Ordner
            RealClick(w, V<Button>(w, "ce-publish")); Settle();
            await Until(() => !w.CodeRunning); Settle();
            var pkg = w.CodePackage!;
            Assert.True(pkg.Ok && pkg.ProbeOk, pkg.Message + " " + pkg.Probe);
            Assert.True(File.Exists(pkg.Zip)); Assert.Contains("Mittelwert", pkg.Probe);
            Assert.NotNull(w.TryFind<Button>("ce-package-open"));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "code.publish");
            // Terminal im Projektordner: Ausgabe, Protokoll, Abbrechen
            RealClick(w, V<Button>(w, "ce-panel-terminal")); Settle();
            var input = w.Find<TextBox>("ce-term-input"); input.Text = Py + " -c \"import os; print('ordner=' + os.getcwd())\""; input.Focus();
            w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, ""); Settle();
            await Until(() => !w.CodeRunning); Settle();
            Assert.Contains("ordner=" + proj, w.CodeTerminalText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "code.terminal" && a.Detail.Contains("import os"));
            w.CodeTerminal(Py + " -c \"import time; print('warte', flush=True); time.sleep(300)\""); w.Show("code"); Settle();
            await Until(() => w.CodeTerminalText.Contains("warte"), 30);
            Assert.True(w.CodeRunning);
            RealClick(w, V<Button>(w, "ce-stop")); Settle();
            await Until(() => !w.CodeRunning, 30);
            Assert.Contains("Abgebrochen", w.CodeTerminalText);
        }
        finally { w.CodeStop(); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0688 Code-Editor (echt, .NET): Konsolen-Vorlage per Klick → Bauen → Fehler in der Fehlerliste anklicken springt zur Zeile → Testen mit 5 echten Tests")]
    public async Task DotNet_bauen_Fehlerliste_testen()
    {
        if (!DevBuild.HasDotNetSdk()) { Assert.Skip("Übersprungen – " + "kein .NET SDK auf diesem PC."); return; }
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.CodeRealToolsInTests = true;
        try
        {
            w.Show("code"); Settle();
            w.Find<ComboBox>("ce-np-template").SelectedIndex = DevStudio.Templates.ToList().FindIndex(t => t.Id == "dotnet-console");
            w.Find<TextBox>("ce-np-name").Text = "Rechner";
            { var nb = V<Button>(w, "ce-np-create"); nb.BringIntoView(); Settle(); RealClick(w, nb); Settle(); }
            var proj = w.CodeRoot!;
            // Fehler einbauen und bauen
            var program = Path.Combine(proj, "Program.cs");
            File.WriteAllText(program, File.ReadAllText(program).Replace("return 0;", "return gibtsnicht;"));
            var line = File.ReadAllLines(program).ToList().FindIndex(l => l.Contains("gibtsnicht")) + 1;
            RealClick(w, V<Button>(w, "ce-build")); Settle();
            await Until(() => !w.CodeRunning, 300); Settle();
            Assert.StartsWith("✗ Bauen fehlgeschlagen", w.CodeOutput);
            var err = w.CodeProblems.First(p => p.Error);
            Assert.Equal(("CS0103", line), (err.Code, err.Line));
            Assert.NotNull(w.TryFind<Button>("ce-problem-0")); // Fehlerliste ist offen
            RealClick(w, V<Button>(w, "ce-problem-0")); Settle();
            Assert.EndsWith("Program.cs", w.CodeActiveFile); Assert.Equal(line, w.CodeEditorForTests!.TextArea.Caret.Line);
            // im Editor reparieren (ungespeichert) – Bauen speichert vorher selbst
            var ed = w.CodeEditorForTests!; ed.Document.Text = ed.Document.Text.Replace("return gibtsnicht;", "return 0;"); Settle();
            RealClick(w, V<Button>(w, "ce-build")); Settle();
            await Until(() => !w.CodeRunning, 300); Settle();
            Assert.StartsWith("✓ Gebaut", w.CodeOutput);
            RealClick(w, V<Button>(w, "ce-test")); Settle();
            await Until(() => !w.CodeRunning, 300); Settle();
            Assert.True(w.CodeTests is { Passed: 5, Failed: 0 }, w.CodeOutput);
        }
        finally { w.CodeStop(); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0688 Code-Editor: ungespeicherter Stand überlebt einen Absturz – neues Fenster bietet „Wiederherstellen“ an, die Datei selbst bleibt unverändert")]
    public async Task Entwurf_nach_Absturz()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N"));
        var proj = Path.Combine(root, "projekt"); Directory.CreateDirectory(proj);
        var file = Path.Combine(proj, "main.py"); File.WriteAllText(file, "print('alt')\n");
        var old = MainWindow.CodeDraftDelay; MainWindow.CodeDraftDelay = TimeSpan.FromMilliseconds(150);
        var (w1, _) = OpenAt(root);
        try
        {
            w1.CodeOpenFolder(proj); w1.Show("code"); Settle();
            var ed = w1.CodeEditorForTests!; ed.Document.Insert(ed.Document.TextLength, "print('neu, ungespeichert')\n"); Settle();
            // der Entwurf wird kurz nach dem Tippen von selbst geschrieben (Zeitgeber), nicht erst beim Schließen
            await Until(() => CodeDrafts.Load(root, file) is not null, 15);
            Assert.NotNull(CodeDrafts.Load(root, file));
            Assert.Equal("print('alt')\n", File.ReadAllText(file)); // Quelldatei unangetastet
        }
        finally { MainWindow.CodeDraftDelay = old; }
        // „Absturz“: das Fenster verschwindet ohne Speichern; ein neues Trendo auf demselben Datenordner
        w1.Close();
        var (w2, _) = OpenAt(root);
        try
        {
            w2.Show("code"); Settle();
            Assert.NotNull(w2.TryFind<Border>("ce-restore"));
            RealClick(w2, V<Button>(w2, "ce-restore-yes")); Settle();
            Assert.Equal(file, w2.CodeActiveFile, ignoreCase: true);
            Assert.True(w2.CodeDirty(file));
            Assert.Contains("neu, ungespeichert", w2.CodeEditorForTests!.Document.Text);
            w2.CodeSave(); Assert.Contains("neu, ungespeichert", File.ReadAllText(file));
            Assert.Null(CodeDrafts.Load(root, file)); // nach dem Speichern ist der Entwurf weg
            w2.Show("code"); Settle(); Assert.Null(w2.TryFind<Border>("ce-restore"));
        }
        finally { w2.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0688 Software-Seite: „Testen“ blockiert nicht mehr – öffnet den Code-Editor, Test läuft im Hintergrund; neue Vorlage „Programm mit Fenster“")]
    public async Task Software_Seite_testet_im_Hintergrund()
    {
        if (Cli.Find(Py) is null) { Assert.Skip("Übersprungen – " + "Python fehlt auf diesem PC."); return; }
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.CodeRealToolsInTests = true;
        try
        {
            w.Show("dev"); Settle();
            Assert.Contains("C#-Programm mit Fenster (.NET, Avalonia)", (IEnumerable<string>)w.Find<ComboBox>("dev-template").ItemsSource!);
            w.Find<ComboBox>("dev-template").SelectedIndex = DevStudio.Templates.ToList().FindIndex(t => t.Id == "python");
            w.Find<TextBox>("dev-name").Text = "Seitentest";
            w.Find<Button>("dev-create").Press(); Settle();
            w.Show("dev"); Settle();
            var sw = Stopwatch.StartNew();
            w.Find<Button>("dev-test-0").Press(); Settle();
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"„Testen“ hat {sw.Elapsed.TotalSeconds:0.0} s blockiert."); // früher bis zu 180 s
            Assert.Equal("code", w.CurrentView);
            Assert.EndsWith("Seitentest", w.CodeRoot);
            await Until(() => !w.CodeRunning); Settle();
            Assert.Equal(6, w.CodeTests!.Passed);
        }
        finally { w.CodeStop(); w.Close(); }
    }

    // ───────────── Code mit KI: „Mit Claude ändern“ (Claude ERSETZT) ─────────────

    [AvaloniaFact(DisplayName = "T-0688 Code mit KI: „Mit Claude ändern“ – Claude (ersetzt) läuft im Projektordner und ändert Dateien, Diff je Datei, eine Datei zurücknehmen, Rest übernehmen")]
    public async Task Claude_aendert_im_Projektordner()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var proj = Path.Combine(root, "projekt"); Directory.CreateDirectory(proj);
        var rechner = Path.Combine(proj, "rechner.py"); File.WriteAllText(rechner, "def teile(a, b):\n    return a / b\n");
        File.WriteAllText(Path.Combine(proj, "test_rechner.py"), "import unittest\n");
        string? seenDir = null; IReadOnlyList<string>? seenArgs = null;
        MainWindow.FolderPickOverride = _ => proj;
        // ERSETZT: statt Claude Code ein Ersatz, der wie Claude im Arbeitsordner Dateien ändert und den Abschluss meldet
        MainWindow.ClaudeRunOverride = (args, dir, line, ct) =>
        {
            seenDir = dir; seenArgs = args;
            File.WriteAllText(Path.Combine(dir, "rechner.py"), "def teile(a, b):\n    if b == 0:\n        raise ValueError(\"Durch null kann man nicht teilen.\")\n    return a / b\n");
            File.WriteAllText(Path.Combine(dir, "hinweis.md"), "Neu von Claude\n");
            line("{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"Ich habe die Prüfung auf null ergänzt.\"}]}}");
            line("{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"result\":\"Prüfung ergänzt.\",\"session_id\":\"s1\",\"num_turns\":2}");
            return Task.FromResult(0);
        };
        try
        {
            const string K = "code-ki-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            w.Find<Button>("ca-open-big").Press(); Settle();
            for (var i = 0; i < 100 && w.ProgramActive(K)!.Text("root") != proj; i++) { await Task.Delay(20); Settle(); }
            var d = w.ProgramActive(K)!;
            w.CodeAiOpenFile(d, rechner); Settle();
            d.Set("wish", "Teilen durch null soll eine verständliche Meldung geben", "Wunsch"); Settle();
            w.Find<Button>("prog-cmd-ca-change").Press(); Settle();
            await Until(() => w.CodeAiClaudeTask is { IsCompleted: true }, 30); Settle();
            Assert.True(await w.CodeAiClaudeTask!);
            // Claude lief im Projektordner (nicht im Chat-Ordner), mit Dateirechten nur dort und ohne Programme
            Assert.Equal(proj, seenDir);
            Assert.Contains("acceptEdits", seenArgs!); Assert.Contains("--restricted", seenArgs!);
            Assert.Contains("Durch null", File.ReadAllText(rechner));
            Assert.True(w.CodeAiClaudePending);
            Assert.Contains("2 Dateien", w.ProgramMessage(K));
            // Diff je Datei ist sichtbar, Entscheidung angeboten
            Assert.NotNull(w.TryFind<Button>("ca-claude-accept")); Assert.NotNull(w.TryFind<Button>("ca-claude-revert"));
            Assert.Equal(2, w.Find<WrapPanel>("ca-changes").Children.Count);
            w.Find<WrapPanel>("ca-changes").Children.OfType<Button>().First(b => b.Content?.ToString()?.StartsWith("hinweis.md") == true).Press(); Settle();
            // nur die neue Datei zurücknehmen
            w.Find<Button>("ca-claude-revert-file").Press(); Settle();
            Assert.False(File.Exists(Path.Combine(proj, "hinweis.md")));
            Assert.Contains("Durch null", File.ReadAllText(rechner));
            Assert.True(w.CodeAiClaudePending);
            // Rest übernehmen
            w.Find<Button>("ca-claude-accept").Press(); Settle();
            Assert.False(w.CodeAiClaudePending);
            Assert.Contains("Durch null", File.ReadAllText(rechner));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "code-ki.claude" && a.Detail.Contains("2 Dateien"));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "code-ki.claude.uebernommen");

            // zweiter Auftrag: alles zurücknehmen stellt den Stand vor Claude her
            var before = File.ReadAllText(rechner);
            d.Set("wish", "Noch eine Änderung", "Wunsch"); Settle();
            w.Find<Button>("prog-cmd-ca-change").Press(); Settle();
            await Until(() => w.CodeAiClaudeTask is { IsCompleted: true }, 30); Settle();
            Assert.True(w.CodeAiClaudePending);
            w.Find<Button>("ca-claude-revert").Press(); Settle();
            Assert.Equal(before, File.ReadAllText(rechner)); Assert.False(File.Exists(Path.Combine(proj, "hinweis.md")));
            Assert.False(w.CodeAiClaudePending);
        }
        finally { MainWindow.ClaudeRunOverride = null; MainWindow.FolderPickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0688 Code mit KI: ohne Claude-Anmeldung klare Meldung, keine Datei geändert, keine Sicherung offen")]
    public async Task Claude_ohne_Anmeldung()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var proj = Path.Combine(root, "projekt"); Directory.CreateDirectory(proj);
        var rechner = Path.Combine(proj, "rechner.py"); File.WriteAllText(rechner, "x = 1\n");
        MainWindow.FolderPickOverride = _ => proj;
        try
        {
            const string K = "code-ki-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            w.Find<Button>("ca-open-big").Press(); Settle();
            for (var i = 0; i < 100 && w.ProgramActive(K)!.Text("root") != proj; i++) { await Task.Delay(20); Settle(); }
            var d = w.ProgramActive(K)!;
            d.Set("wish", "Mach was", "Wunsch");
            Assert.False(await w.CodeAiClaudeChange(d)); Settle();
            Assert.Contains("nicht eingerichtet", w.ProgramMessage(K));
            Assert.Contains("nie einen API-Schlüssel", w.ProgramMessage(K));
            Assert.False(w.CodeAiClaudePending);
            Assert.Equal("x = 1\n", File.ReadAllText(rechner));
        }
        finally { MainWindow.FolderPickOverride = null; w.Close(); }
    }
}
