using Trendo.Core;
using Xunit;

/// <summary>T-0688: Software-Studio baut, testet und paketiert wirklich. Die Lauf-Tests nutzen die echten Werkzeuge dieses PCs (dotnet, python, node)
/// und werden ehrlich übersprungen, wenn eins fehlt. Die Auswerte-Tests prüfen die Leser mit echten Ausgaben der Werkzeuge.</summary>
public class T0688_DevBuild
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-dev688-" + Guid.NewGuid().ToString("N")[..10]); Directory.CreateDirectory(d); return d; }
    static string Py => OperatingSystem.IsWindows() ? "python" : "python3";

    [Fact(DisplayName = "T-0688: jede Vorlage hat eine echte Funktion und einen echten Test, Projektart wird erkannt")]
    public void Vorlagen_haben_Funktion_und_Test()
    {
        var root = Tmp();
        Assert.Contains(DevStudio.Templates, t => t.Id == "dotnet-desktop");
        foreach (var t in DevStudio.Templates)
        {
            var dir = DevStudio.Create(root, t.Id, "Probe " + t.Id);
            var p = DevBuild.Detect(dir);
            Assert.NotNull(p.Tests); // jede Vorlage bringt Tests mit
            switch (t.Id)
            {
                case "dotnet-console" or "dotnet-desktop":
                    Assert.Equal(DevBuild.Kind.DotNet, p.Kind); Assert.Equal(t.Id == "dotnet-desktop", p.Gui);
                    Assert.EndsWith(".Tests.csproj", p.Tests); Assert.True(File.Exists(Path.Combine(dir, "tests", Path.GetFileName(p.Tests))));
                    Assert.DoesNotContain("Hallo", File.ReadAllText(Path.Combine(dir, t.Id == "dotnet-desktop" ? "Umrechner.cs" : "Rechner.cs")));
                    break;
                case "python": Assert.Equal(DevBuild.Kind.Python, p.Kind); Assert.Equal("unittest", p.Tests); Assert.Contains("def median", File.ReadAllText(Path.Combine(dir, "rechner.py"))); break;
                case "web": Assert.Equal(DevBuild.Kind.Web, p.Kind); Assert.True(File.Exists(Path.Combine(dir, "server.js"))); Assert.Contains("Logik.dazu", File.ReadAllText(Path.Combine(dir, "app.js"))); break;
                case "node": Assert.Equal(DevBuild.Kind.Node, p.Kind); Assert.Contains("export function zaehle", File.ReadAllText(Path.Combine(dir, "index.js"))); break;
            }
        }
        // .NET: „Testen“ ist ein echter Testlauf, nicht nur Bauen
        if (Cli.Find("dotnet") is not null) Assert.Equal("test", DevStudio.TestCommand(Path.Combine(root, "Probe-dotnet-console"))!.Value.Args[0]);
    }

    [Fact(DisplayName = "T-0688: fehlt ein Werkzeug, gibt es keinen Befehl, sondern BLOCKIERT mit Einrichten-Weg")]
    public void Fehlendes_Werkzeug_blockiert()
    {
        var root = Tmp();
        var cs = DevBuild.Detect(DevStudio.Create(root, "dotnet-console", "a"));
        var py = DevBuild.Detect(DevStudio.Create(root, "python", "b"));
        DevBuild.HideToolsForTests = new HashSet<string> { "dotnet", Py, "node" };
        try
        {
            foreach (var plan in new[] { DevBuild.Build(cs), DevBuild.Run(cs), DevBuild.Test(cs), DevBuild.Test(py), DevBuild.Run(py) })
            {
                Assert.Null(plan.Cmd);
                Assert.StartsWith("BLOCKIERT", plan.Blocked);
                Assert.Contains("Einrichten", plan.Blocked);
            }
            Assert.False(DevStudio.RunTests(cs.Root).Ok);
            Assert.StartsWith("BLOCKIERT", DevStudio.RunTests(cs.Root).Err);
            var pkg = DevBuild.PublishAsync(cs, _ => { }, CancellationToken.None).GetAwaiter().GetResult();
            Assert.False(pkg.Ok); Assert.StartsWith("BLOCKIERT", pkg.Message);
        }
        finally { DevBuild.HideToolsForTests = null; }
    }

    [Fact(DisplayName = "T-0688: Fehlerliste liest dotnet-, Python- und Node-Meldungen mit Datei und Zeile")]
    public void Fehlerliste()
    {
        var root = Tmp();
        var cs = Path.Combine(root, "Program.cs");
        var outText = $"  {cs}(3,9): error CS0103: Der Name \"x\" ist im aktuellen Kontext nicht vorhanden. [{root}\\a.csproj]\n  {cs}(3,9): error CS0103: Der Name \"x\" ist im aktuellen Kontext nicht vorhanden. [{root}\\a.csproj]\n" +
            $"{cs}(7,1): warning CS8321: Die lokale Funktion \"f\" ist deklariert, wird aber nie verwendet.\n" +
            $"Traceback (most recent call last):\n  File \"{Path.Combine(root, "main.py")}\", line 12, in <module>\n    teile(1, 0)\nZeroDivisionError: division by zero\n";
        var list = DevBuild.Problems(outText, root);
        Assert.Equal(3, list.Count); // doppelte Meldung nur einmal
        var e0 = list.Single(p => p.Code == "CS0103"); Assert.Equal((cs, 3, 9, true), (e0.File, e0.Line, e0.Column, e0.Error));
        Assert.Contains(list, p => p.File.EndsWith("main.py") && p.Line == 12 && p.Message.StartsWith("ZeroDivisionError"));
        Assert.Contains(list, p => !p.Error && p.Code == "CS8321" && p.Line == 7);
    }

    [Fact(DisplayName = "T-0688: unittest-Ausgabe → je Test bestanden/fehlgeschlagen mit Datei und Zeile")]
    public void Unittest_lesen()
    {
        var root = Tmp(); var f = Path.Combine(root, "test_rechner.py");
        var output = "test_a (test_rechner.RechnerTest.test_a) ... ok\ntest_b (test_rechner.RechnerTest.test_b) ... FAIL\n\n" +
            "======================================================================\nFAIL: test_b (test_rechner.RechnerTest.test_b)\n----------------------------------------------------------------------\n" +
            $"Traceback (most recent call last):\n  File \"{f}\", line 9, in test_b\n    self.assertEqual(1, 2)\nAssertionError: 1 != 2\n\n----------------------------------------------------------------------\nRan 2 tests in 0.001s\n\nFAILED (failures=1)\n";
        var r = DevBuild.ParseUnittest(output, root);
        Assert.Equal((1, 1), (r.Passed, r.Failed));
        var bad = r.Cases.Single(c => !c.Passed);
        Assert.Equal((f, 9), (bad.File, bad.Line)); Assert.Equal("AssertionError: 1 != 2", bad.Message);
    }

    [NeedsFact("python", DisplayName = "T-0688: Python-Vorlage – Bauen, Testen (je Test), Ausführen, Paket mit Startprobe aus leerem Ordner (echtes Python)")]
    public async Task Python_ganz()
    {
        var p = DevBuild.Detect(DevStudio.Create(Tmp(), "python", "Zahlen"));
        var b = DevBuild.Build(p).Cmd!; Assert.True(Cli.Run(b.Exe, b.Args, b.Cwd, 60000, b.Env).Ok);
        var t = DevBuild.Test(p).Cmd!; var tr = Cli.Run(t.Exe, t.Args, t.Cwd, 60000, t.Env);
        Assert.True(tr.Ok, tr.Err);
        var run = DevBuild.ReadResults(p, null, tr.Out + tr.Err);
        Assert.Equal(6, run.Passed); Assert.Equal(0, run.Failed);
        // ein Fehler wird als einzelner Test mit Datei/Zeile gemeldet
        File.WriteAllText(Path.Combine(p.Root, "rechner.py"), File.ReadAllText(Path.Combine(p.Root, "rechner.py")).Replace("return sum(zahlen)", "return sum(zahlen) + 1"));
        tr = Cli.Run(t.Exe, t.Args, t.Cwd, 60000, t.Env);
        run = DevBuild.ReadResults(p, null, tr.Out + tr.Err);
        Assert.False(tr.Ok); Assert.Equal(1, run.Failed);
        Assert.EndsWith("test_rechner.py", run.Cases[0].File); Assert.True(run.Cases[0].Line > 0);
        File.WriteAllText(Path.Combine(p.Root, "rechner.py"), File.ReadAllText(Path.Combine(p.Root, "rechner.py")).Replace("return sum(zahlen) + 1", "return sum(zahlen)"));
        var r = DevBuild.Run(p).Cmd!; var rr = Cli.Run(r.Exe, r.Args, r.Cwd, 60000, r.Env);
        Assert.Contains("Mittelwert: 3.33", rr.Out);
        var pkg = await DevBuild.PublishAsync(p, _ => { }, CancellationToken.None);
        Assert.True(pkg.Ok, pkg.Message); Assert.True(pkg.ProbeOk, pkg.Probe);
        Assert.True(File.Exists(pkg.Zip)); Assert.Equal(64, pkg.Sha256.Length); Assert.Contains("Mittelwert", pkg.Probe);
        Assert.False(File.Exists(Path.Combine(pkg.Folder, ".gitignore")));
    }

    [NeedsFact("node", DisplayName = "T-0688: Web-App – node --test je Test (TAP), Paket als Ordner+ZIP, Startprobe über den Server des Projekts (echtes Node)")]
    public async Task Web_ganz()
    {
        var p = DevBuild.Detect(DevStudio.Create(Tmp(), "web", "Einkauf"));
        var res = Path.Combine(p.Root, ".trendo", "tests"); Directory.CreateDirectory(res);
        var t = DevBuild.Test(p, res).Cmd!; var tr = Cli.Run(t.Exe, t.Args, t.Cwd, 60000);
        Assert.True(tr.Ok, tr.Err + tr.Out);
        var run = DevBuild.ReadResults(p, res, tr.Out);
        Assert.Equal(4, run.Passed); Assert.Equal(0, run.Failed);
        var pkg = await DevBuild.PublishAsync(p, _ => { }, CancellationToken.None);
        Assert.True(pkg.Ok, pkg.Message); Assert.True(pkg.ProbeOk, pkg.Probe); Assert.Contains("200", pkg.Probe);
        Assert.False(File.Exists(Path.Combine(pkg.Folder, "server.js"))); Assert.True(File.Exists(Path.Combine(pkg.Folder, "logik.js")));
    }

    [NeedsFact("dotnet", DisplayName = "T-0688: .NET-Konsole – dotnet test je Test (TRX), Fehler mit Datei/Zeile, publish self-contained single-file, Startprobe (echtes dotnet)")]
    public async Task DotNet_ganz()
    {
        if (!DevBuild.HasDotNetSdk()) Trendo.Core.Tests.Ueberspringen.Weil("kein .NET SDK auf diesem PC.");
        var p = DevBuild.Detect(DevStudio.Create(Tmp(), "dotnet-console", "Rechner"));
        var res = Path.Combine(p.Root, ".trendo", "tests");
        var t = DevBuild.Test(p, res).Cmd!; var tr = Cli.Run(t.Exe, t.Args, t.Cwd, 600000, t.Env);
        Assert.True(tr.Ok, tr.Out + tr.Err);
        var run = DevBuild.ReadResults(p, res, tr.Out);
        Assert.Equal(5, run.Passed); Assert.Equal(0, run.Failed);
        // fehlgeschlagener Test → Name, Meldung, Datei und Zeile im Testprojekt
        var testFile = Path.Combine(p.Root, "tests", "RechnerTests.cs");
        File.WriteAllText(testFile, File.ReadAllText(testFile).Replace("Assert.Equal(10m,", "Assert.Equal(11m,"));
        Directory.Delete(res, true);
        tr = Cli.Run(t.Exe, t.Args, t.Cwd, 600000, t.Env);
        run = DevBuild.ReadResults(p, res, tr.Out);
        Assert.False(tr.Ok); Assert.Equal(1, run.Failed); Assert.Equal(4, run.Passed);
        var bad = run.Cases.First(c => !c.Passed);
        Assert.Contains("Summe", bad.Name); Assert.Equal(testFile, bad.File, ignoreCase: true); Assert.True(bad.Line > 0);
        File.WriteAllText(testFile, File.ReadAllText(testFile).Replace("Assert.Equal(11m,", "Assert.Equal(10m,"));
        var pkg = await DevBuild.PublishAsync(p, _ => { }, CancellationToken.None);
        Assert.True(pkg.Ok, pkg.Message); Assert.True(pkg.ProbeOk, pkg.Probe);
        Assert.Contains("Summe: 10", pkg.Probe);
        Assert.Single(Directory.GetFiles(pkg.Folder, OperatingSystem.IsWindows() ? "*.exe" : "Rechner")); // ein einziges Programm
        Assert.True(pkg.Bytes > 10_000_000); // enthält die .NET-Laufzeit
    }
}
