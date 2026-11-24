using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0762: X-06 (Installer-Ampel, Bauanleitung, Entfernen ohne Rest) und X-10 (Anforderungsliste, Abgleich, Vergleich nebeneinander, Bericht mit Herkunft).</summary>
public class T0762GitHubNachbauTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0762-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0762 X-06: Installer-Ampel – gültig = Grün ohne Häkchen, nicht prüfbar = Gelb, unsigniert/ungültig = Rot mit Risiko-Bestätigung, fehlend = gesperrt")]
    public void Installer_ampel()
    {
        var d = Tmp(); var f = Path.Combine(d, "setup.exe"); File.WriteAllBytes(f, [(byte)'M', (byte)'Z', 1, 2, 3]);
        var g = GitHubApps.CheckInstaller(f, GitHubApps.InstallerKind.Inno, _ => new SignatureInfo(SigState.Gueltig, "Beispiel GmbH", "Gültig signiert (in der Datei)."));
        Assert.Equal(GitHubApps.Ampel.Gruen, g.Ampel); Assert.False(g.NeedsRiskConsent); Assert.Contains("Beispiel GmbH", g.Title); Assert.Contains("nicht, dass sie harmlos ist", g.Text);
        Assert.Equal(64, g.Sha256.Length);
        var y = GitHubApps.CheckInstaller(f, GitHubApps.InstallerKind.Inno, _ => new SignatureInfo(SigState.NichtPruefbar, null, "Nicht prüfbar."));
        Assert.Equal(GitHubApps.Ampel.Gelb, y.Ampel); Assert.True(y.NeedsRiskConsent);
        var bad = GitHubApps.CheckInstaller(f, GitHubApps.InstallerKind.Inno, _ => new SignatureInfo(SigState.Ungueltig, "X", Authenticode.Explain(0x80096010)));
        Assert.Equal(GitHubApps.Ampel.Rot, bad.Ampel); Assert.True(bad.NeedsRiskConsent); Assert.Contains("verändert", bad.Text);
        // echte Windows-Prüfung: selbst geschriebene Datei ist nicht signiert → Rot
        var real = GitHubApps.CheckInstaller(f, GitHubApps.InstallerKind.Squirrel);
        if (OperatingSystem.IsWindows()) Assert.Equal(GitHubApps.Ampel.Rot, real.Ampel);
        File.Delete(f);
        Assert.True(GitHubApps.CheckInstaller(f, GitHubApps.InstallerKind.Nsis).Blocked);
        Directory.Delete(d, true);
    }

    public sealed class NotepadFactAttribute : FactAttribute { public NotepadFactAttribute() { if (!OperatingSystem.IsWindows() || !File.Exists(Path.Combine(Environment.SystemDirectory, "notepad.exe"))) Skip = "Übersprungen – nur unter Windows mit notepad.exe."; } }
    public sealed class PythonFactAttribute : FactAttribute { public PythonFactAttribute() { if (DevBuild.Missing("python") is not null) Skip = "Übersprungen – Python fehlt."; } }

    [NotepadFact(DisplayName = "T-0762 X-06: echte Authenticode-Prüfung einer Windows-Programmdatei ergibt Grün (nur Windows)")]
    public void Installer_real_signed()
    {
        var f = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        var c = GitHubApps.CheckInstaller(f, GitHubApps.InstallerKind.Nsis);
        Assert.Equal(GitHubApps.Ampel.Gruen, c.Ampel);
        Assert.False(c.NeedsRiskConsent);
    }

    [Fact(DisplayName = "T-0762 X-06: Bauanleitung erkannt (dotnet, npm mit Skripten, cargo, go, Python, CMake) – Werkzeug fehlt wird markiert, vorhandene zuerst")]
    public void Build_hints()
    {
        var d = Tmp();
        File.WriteAllText(Path.Combine(d, "App.sln"), "");
        File.WriteAllText(Path.Combine(d, "package.json"), """{"scripts":{"build":"vite build","test":"vitest"}}""");
        File.WriteAllText(Path.Combine(d, "Cargo.toml"), "[package]");
        File.WriteAllText(Path.Combine(d, "go.mod"), "module x");
        File.WriteAllText(Path.Combine(d, "requirements.txt"), "requests");
        File.WriteAllText(Path.Combine(d, "CMakeLists.txt"), "project(x)");
        var h = OpenSource.BuildHints(d, t => t is "dotnet" or "npm");
        var cmds = h.Select(x => x.Command).ToList();
        Assert.Contains("dotnet build \"App.sln\"", cmds);
        Assert.Contains("npm install", cmds); Assert.Contains("npm run build", cmds); Assert.Contains("npm test", cmds);
        Assert.Contains("cargo build --release", cmds); Assert.Contains("go build ./...", cmds); Assert.Contains("python -I -m compileall -q .", cmds);
        Assert.Contains("cmake -B build && cmake --build build", cmds);
        Assert.True(h.TakeWhile(x => x.Available).Count() == h.Count(x => x.Available)); // vorhandene zuerst
        Assert.False(h.First(x => x.Tool == "cargo").Available);
        Assert.Empty(OpenSource.BuildHints(Tmp()));
        var only = Tmp(); Directory.CreateDirectory(Path.Combine(only, "src")); File.WriteAllText(Path.Combine(only, "src", "Tool.csproj"), "<Project/>");
        Assert.Equal("dotnet build \"src" + Path.DirectorySeparatorChar + "Tool.csproj\"", OpenSource.BuildHints(only, _ => true).Single().Command);
    }

    [Fact(DisplayName = "T-0762 X-06: Entfernen löscht Ordner (auch schreibgeschützte Git-Dateien) und Herkunftsdatei – kein Rest; nur Ordner direkt im Open-Source-Ordner")]
    public void Remove_leaves_nothing()
    {
        var parent = Tmp(); var p = Path.Combine(parent, "o_r"); Directory.CreateDirectory(Path.Combine(p, ".git", "objects"));
        var ro = Path.Combine(p, ".git", "objects", "ab"); File.WriteAllText(ro, "x"); File.SetAttributes(ro, FileAttributes.ReadOnly);
        File.WriteAllText(OpenSource.OriginFile(p), "Quelle: https://github.com/o/r.git");
        Assert.Throws<InvalidOperationException>(() => OpenSource.Remove(parent, Path.Combine(p, ".git")));
        OpenSource.Remove(parent, p);
        Assert.Empty(Directory.GetFileSystemEntries(parent));
        Directory.Delete(parent);
    }

    static string NewProject(out string root)
    {
        root = Tmp();
        return CleanRoom.Create(root, "Notizen", "ein bekannter Notizblock");
    }

    [Fact(DisplayName = "T-0762 X-10: Anforderungsliste – Anlegen mit Herkunft und Datum, Bearbeiten, Rückgängig, sicher gespeichert, Export als Markdown-Tabelle")]
    public void Requirements_doc()
    {
        var dir = NewProject(out var root);
        var doc = new CleanRoom.RequirementsDoc(dir);
        var a = doc.Add("Notiz speichern", "Handbuch Kapitel 2");
        var b = doc.Add("Notiz suchen");
        Assert.Equal("A-001", a.Id); Assert.Equal("A-002", b.Id);
        Assert.Equal(DateTimeOffset.Now.ToString("yyyy-MM-dd"), a.Date);
        doc.Update(a.Id, r => r with { Expected = "Datei liegt danach im Ordner", Stage = "Muss" });
        Assert.Equal("Datei liegt danach im Ordner", new CleanRoom.RequirementsDoc(dir).Items[0].Expected); // sofort gespeichert
        doc.Remove(b.Id);
        Assert.Single(CleanRoom.LoadRequirements(dir));
        Assert.True(doc.Undo()); Assert.Equal(2, CleanRoom.LoadRequirements(dir).Count); // Löschen zurück
        Assert.True(doc.Undo()); Assert.Equal("", CleanRoom.LoadRequirements(dir)[0].Expected); // Bearbeiten zurück
        var md = File.ReadAllText(CleanRoom.ExportRequirements(dir));
        Assert.Contains("| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle (Herkunft) | Datum | Stufe | Status |", md);
        Assert.Contains("| A-001 | Notiz speichern |", md); Assert.Contains("Handbuch Kapitel 2", md);
        // beschädigte Datei: leere Liste statt Absturz
        File.WriteAllText(CleanRoom.RequirementsFile(dir), "{kaputt");
        Assert.Empty(CleanRoom.LoadRequirements(dir));
        Directory.Delete(root, true);
    }

    [Fact(DisplayName = "T-0762 X-10: Abgleich – kein Test = offen, Datei ohne Testname = teilweise, Test bestanden = erfüllt, fehlgeschlagen = offen, Pfad außerhalb = offen")]
    public void Reconcile_rules()
    {
        var dir = NewProject(out var root);
        File.WriteAllText(Path.Combine(dir, "2-Umsetzung", "test_notiz.py"), "def test_speichern(): pass\ndef test_suchen(): pass\n");
        Requirement R(string id, string link) => new(id, "f", "", "", "q", "2026-11-24", "Muss", CleanRoom.StatusOpen, link);
        var run = new DevBuild.TestRun([new("test_notiz.Tests.test_speichern", true, false, "", null, 0), new("test_notiz.Tests.test_suchen", false, false, "AssertionError", null, 0)]);
        Assert.Equal(CleanRoom.StatusOpen, CleanRoom.Reconcile(dir, R("A-1", ""), run).Status);
        Assert.Equal(CleanRoom.StatusPartly, CleanRoom.Reconcile(dir, R("A-2", "2-Umsetzung/test_notiz.py"), run).Status);
        Assert.Equal(CleanRoom.StatusDone, CleanRoom.Reconcile(dir, R("A-3", "2-Umsetzung/test_notiz.py#test_speichern"), run).Status);
        var failed = CleanRoom.Reconcile(dir, R("A-4", "2-Umsetzung/test_notiz.py#test_suchen"), run);
        Assert.Equal(CleanRoom.StatusOpen, failed.Status); Assert.Contains("AssertionError", failed.Reason);
        Assert.Equal(CleanRoom.StatusPartly, CleanRoom.Reconcile(dir, R("A-5", "2-Umsetzung/test_notiz.py#test_speichern"), null).Status); // noch nicht gelaufen
        Assert.Equal(CleanRoom.StatusOpen, CleanRoom.Reconcile(dir, R("A-6", "../../geheim.txt"), run).Status);
        Assert.Equal(CleanRoom.StatusOpen, CleanRoom.Reconcile(dir, R("A-7", "2-Umsetzung/fehlt.py#x"), run).Status);
        Directory.Delete(root, true);
    }

    [PythonFact(DisplayName = "T-0762 X-10: Abgleich mit echtem Python-Testlauf in „2-Umsetzung“ (übersprungen ohne Python)")]
    public void Reconcile_real_python()
    {
        var dir = NewProject(out var root);
        File.WriteAllText(Path.Combine(dir, "2-Umsetzung", "notiz.py"), "def speichern(t):\n    return t.strip()\n");
        File.WriteAllText(Path.Combine(dir, "2-Umsetzung", "test_notiz.py"), "import unittest\nimport notiz\nclass T(unittest.TestCase):\n    def test_speichern(self):\n        self.assertEqual(notiz.speichern(' a '), 'a')\n    def test_kaputt(self):\n        self.assertEqual(1, 2)\n");
        var doc = new CleanRoom.RequirementsDoc(dir);
        var a = doc.Add("Speichern"); var b = doc.Add("Absichtlich offen");
        doc.Update(a.Id, r => r with { Link = "2-Umsetzung/test_notiz.py#test_speichern" });
        doc.Update(b.Id, r => r with { Link = "2-Umsetzung/test_notiz.py#test_kaputt" });
        var (run, log) = CleanRoom.RunTests(dir);
        Assert.NotNull(run); Assert.Contains("Exit", log);
        var res = CleanRoom.ReconcileAll(dir, doc.Items, run);
        Assert.Equal(CleanRoom.StatusDone, res[0].Status);
        Assert.Equal(CleanRoom.StatusOpen, res[1].Status);
        Directory.Delete(root, true);
    }

    [Fact(DisplayName = "T-0762 X-10: Vergleich nebeneinander markiert die gleichen Zeilen trotz Umbenennen, eigener Code bleibt unmarkiert")]
    public void Match_lines()
    {
        var theirs = "// Kopf\nusing System;\n\npublic static decimal Total(List<Order> orders) {\n    decimal sum = 0;\n    foreach (var o in orders) { if (o.Paid && !o.Cancelled) { sum += o.Price * o.Qty; } }\n    return sum;\n}\n";
        var ours = "/* eigene\n   Version */\npublic static decimal Gesamt(List<Auftrag> liste) {\n    decimal s = 0;\n    foreach (var a in liste) { if (a.Bezahlt && !a.Storniert) { s += a.Preis * a.Menge; } }\n    return s;\n}\n";
        var (o, t) = CleanRoom.MatchLines(ours, theirs);
        Assert.Contains(5, o); Assert.Contains(6, t); // die foreach-Zeile beidseits (Zeilennummern trotz Blockkommentar richtig)
        Assert.DoesNotContain(1, o); Assert.DoesNotContain(2, t);
        var (o2, _) = CleanRoom.MatchLines("def gesamt(x):\n    return sum(x)\n", theirs);
        Assert.Empty(o2);
    }

    [Fact(DisplayName = "T-0762 X-10: Prüfbericht nennt Herkunft und Datum jedes Eintrags (Anforderungen, Tagebuch, Ähnlichkeitstreffer)")]
    public void Report_has_origin_and_date()
    {
        var dir = NewProject(out var root);
        var doc = new CleanRoom.RequirementsDoc(dir); doc.Add("Speichern", "Handbuch S. 12");
        var orig = Path.Combine(root, "vergleich"); Directory.CreateDirectory(orig);
        var code = "class A { void M() { int x = 1; x++; if (x > 2) { x--; } return; } }";
        File.WriteAllText(Path.Combine(orig, "a.cs"), code); File.WriteAllText(Path.Combine(dir, "2-Umsetzung", "b.cs"), code.Replace("x", "y"));
        var sims = CleanRoom.Compare(Path.Combine(dir, "2-Umsetzung"), orig);
        Assert.NotEmpty(sims);
        var text = File.ReadAllText(CleanRoom.Report(dir, sims, orig));
        var today = DateTimeOffset.Now.ToString("yyyy-MM-dd");
        Assert.Contains("Herkunft: eigener Code „2-Umsetzung/b.cs“, Vergleichstext „vergleich/a.cs“ · Datum: " + today, text);
        Assert.Contains("| A-001 | Speichern |", text); Assert.Contains("Handbuch S. 12 | " + today, text);
        foreach (var line in text.Split('\n').Where(l => l.StartsWith("- ") && l.Contains('[')))
        { Assert.Contains("Herkunft: Tagebuch", line); Assert.Contains("Datum:", line); }
        Assert.DoesNotContain(root, text); // nur Ordnernamen, keine vollen Pfade
        Directory.Delete(root, true);
    }

    // ───────────── Nachbesserung nach Gegenprüfung (B1–B8) ─────────────

    public sealed class WindowsPythonFactAttribute : FactAttribute { public WindowsPythonFactAttribute() { if (!OperatingSystem.IsWindows() || DevBuild.Missing("python") is not null) Skip = "Übersprungen – nur Windows mit Python."; } }
    public sealed class WindowsFactAttribute : FactAttribute { public WindowsFactAttribute() { if (!OperatingSystem.IsWindows()) Skip = "Übersprungen – nur Windows."; } }

    static Cli.Result RunLikeTerminal(string dir, string command)
    {
        var script = Path.Combine(Path.GetTempPath(), "trendo-t0762-term-" + Guid.NewGuid().ToString("N")[..8] + ".cmd");
        File.WriteAllText(script, $"@echo off\r\ncd /d \"{dir}\"\r\n{command}\r\n");
        try { return Cli.Run("cmd", ["/d", "/c", script], dir, 120_000, new Dictionary<string, string> { ["NoDefaultCurrentDirectoryInExePath"] = "1" }); }
        finally { File.Delete(script); }
    }

    [WindowsPythonFact(DisplayName = "T-0762 B1: Bauanleitung „python -I -m compileall“ führt eine projekteigene compileall.py bzw. python.bat NICHT aus (ohne -I schon – Rot-Grün)")]
    public void B1_project_module_not_run()
    {
        var d = Tmp();
        File.WriteAllText(Path.Combine(d, "pyproject.toml"), "[project]\nname='x'\n");
        var marker = Path.Combine(d, "GEKAPERT.txt");
        File.WriteAllText(Path.Combine(d, "compileall.py"), $"open(r'{marker}', 'w').write('x')\n");
        File.WriteAllText(Path.Combine(d, "python.bat"), $"@echo x> \"{marker}\"\r\n");
        var cmd = OpenSource.BuildHints(d).First(h => h.Tool == "python").Command;
        Assert.Equal("python -I -m compileall -q .", cmd);
        var r = RunLikeTerminal(d, cmd);
        Assert.False(File.Exists(marker), "Projekt-Code lief: " + r.Out + r.Err);
        Assert.Equal(0, r.Exit);
        // Gegenprobe: der alte Befehl ohne -I lädt die compileall.py aus dem Projekt
        RunLikeTerminal(d, "python -m compileall -q .");
        Assert.True(File.Exists(marker));
        Directory.Delete(d, true);
    }

    [Fact(DisplayName = "T-0762 B1: Tooltips ehrlich (npm-Installationsskripte, MSBuild-Ziele, Makefile, gradlew aus dem Projekt); erster Bau-Lauf je Projekt wird in der Herkunftsdatei vermerkt")]
    public void B1_honest_hints_and_approval()
    {
        var parent = Tmp(); var d = Path.Combine(parent, "o_r"); Directory.CreateDirectory(d);
        File.WriteAllText(Path.Combine(d, "App.sln"), ""); File.WriteAllText(Path.Combine(d, "package.json"), "{}"); File.WriteAllText(Path.Combine(d, "Makefile"), "all:"); File.WriteAllText(Path.Combine(d, "gradlew.bat"), "");
        var h = OpenSource.BuildHints(d, _ => true);
        Assert.Contains("postinstall", h.Single(x => x.Command == "npm install").Why);
        Assert.Contains("MSBuild", h.Single(x => x.Tool == "dotnet").Why);
        Assert.Contains("beliebige Befehle", h.Single(x => x.Tool == "make").Why);
        Assert.Contains("Skript aus dem Projekt", h.Single(x => x.Tool == "gradlew").Why);
        Assert.Equal("Bauen führt Code aus diesem fremden Projekt mit deinen Rechten aus.", OpenSource.BuildWarning);
        File.WriteAllText(OpenSource.OriginFile(d), "Quelle: x\n");
        Assert.False(OpenSource.BuildApproved(d));
        OpenSource.ApproveBuild(d);
        Assert.True(OpenSource.BuildApproved(d));
        OpenSource.Remove(parent, d);
        Assert.Empty(Directory.GetFileSystemEntries(parent));
    }

    [WindowsFact(DisplayName = "T-0762 B7: Entfernen folgt keiner Junction – Ziel außerhalb bleibt samt Schreibschutz erhalten")]
    public void B7_junction_not_followed()
    {
        var parent = Tmp(); var outside = Tmp();
        var keep = Path.Combine(outside, "wichtig.txt"); File.WriteAllText(keep, "bleibt"); File.SetAttributes(keep, FileAttributes.ReadOnly);
        var p = Path.Combine(parent, "o_r"); Directory.CreateDirectory(Path.Combine(p, "sub"));
        var link = Path.Combine(p, "sub", "verweis");
        var r = Cli.Run("cmd", ["/d", "/c", "mklink", "/J", link, outside]);
        Assert.True(r.Ok && Directory.Exists(link), r.Out + r.Err);
        OpenSource.Remove(parent, p);
        Assert.False(Directory.Exists(p));
        Assert.True(File.Exists(keep));
        Assert.True(File.GetAttributes(keep).HasFlag(FileAttributes.ReadOnly)); // Schreibschutz im Ziel nicht angefasst
        File.SetAttributes(keep, FileAttributes.Normal); Directory.Delete(outside, true); Directory.Delete(parent, true);
    }

    [Fact(DisplayName = "T-0762 B4: beschädigte anforderungen.json wird gemeldet und vor dem ersten Speichern als anforderungen.defekt-…json gesichert")]
    public void B4_broken_file_backed_up()
    {
        var dir = NewProject(out var root);
        File.WriteAllText(CleanRoom.RequirementsFile(dir), "{kaputt – von Hand bearbeitet");
        var doc = new CleanRoom.RequirementsDoc(dir);
        Assert.NotNull(doc.Problem); Assert.Contains("beschädigt", doc.Problem);
        Assert.Empty(doc.Items);
        doc.Add("Neu");
        var copy = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(CleanRoom.RequirementsFile(dir))!, "anforderungen.defekt-*.json"));
        Assert.Equal("{kaputt – von Hand bearbeitet", File.ReadAllText(copy));
        Assert.Contains(Path.GetFileName(copy), doc.Problem);
        Assert.Single(CleanRoom.LoadRequirements(dir));
        Directory.Delete(root, true);
    }

    [Fact(DisplayName = "T-0762 B8: Abgleich prüft Testname UND Datei/Modul – gleicher Testname in anderer Datei zählt nicht")]
    public void B8_same_name_other_file()
    {
        var dir = NewProject(out var root);
        var impl = Path.Combine(dir, "2-Umsetzung");
        File.WriteAllText(Path.Combine(impl, "test_a.py"), "def test_x(): pass\n"); File.WriteAllText(Path.Combine(impl, "test_b.py"), "def test_x(): pass\n");
        var run = new DevBuild.TestRun([new("test_a.T.test_x", true, false, "", null, 0), new("test_b.T.test_x", false, false, "kaputt", null, 0)]);
        Requirement R(string link) => new("A-1", "f", "", "", "q", "2026-11-24", "Muss", CleanRoom.StatusOpen, link);
        Assert.Equal(CleanRoom.StatusDone, CleanRoom.Reconcile(dir, R("2-Umsetzung/test_a.py#test_x"), run).Status);
        Assert.Equal(CleanRoom.StatusOpen, CleanRoom.Reconcile(dir, R("2-Umsetzung/test_b.py#test_x"), run).Status);
        var onlyA = new DevBuild.TestRun([new("test_a.T.test_x", true, false, "", null, 0)]);
        Assert.Equal(CleanRoom.StatusPartly, CleanRoom.Reconcile(dir, R("2-Umsetzung/test_b.py#test_x"), onlyA).Status); // nicht gelaufen ≠ erfüllt
        // Ergebnisdatei nennt die Datei direkt (z. B. dotnet TRX): zählt
        var byFile = new DevBuild.TestRun([new("Ns.Klasse.test_x", true, false, "", Path.Combine(impl, "test_b.py"), 3)]);
        Assert.Equal(CleanRoom.StatusDone, CleanRoom.Reconcile(dir, R("2-Umsetzung/test_b.py#test_x"), byFile).Status);
        Directory.Delete(root, true);
    }

    [PythonFact(DisplayName = "T-0762 B3: Testlauf des Abgleichs lässt sich abbrechen – Prozess wird beendet, kein Ergebnis")]
    public async Task B3_cancel_test_run()
    {
        var dir = NewProject(out var root);
        File.WriteAllText(Path.Combine(dir, "2-Umsetzung", "test_lang.py"), "import time, unittest\nclass T(unittest.TestCase):\n    def test_lang(self):\n        time.sleep(120)\n");
        using var cts = new CancellationTokenSource();
        var pid = 0; var sw = System.Diagnostics.Stopwatch.StartNew();
        var t = CleanRoom.RunTestsAsync(dir, cts.Token, onStart: p => pid = p);
        while (pid == 0 && sw.Elapsed < TimeSpan.FromSeconds(30)) await Task.Delay(50);
        await Task.Delay(800); cts.Cancel();
        var (run, log) = await t;
        Assert.Null(run); Assert.Contains("Abgebrochen", log);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(60));
        Assert.Throws<ArgumentException>(() => System.Diagnostics.Process.GetProcessById(pid));
        Directory.Delete(root, true);
    }

    [WindowsFact(DisplayName = "T-0762 B5: gesperrte Programmdatei lässt sich nicht austauschen, aber starten; Prüfsumme aus dem gesperrten Datenstrom")]
    public void B5_locked_file()
    {
        var d = Tmp(); var exe = Path.Combine(d, "where.exe"); File.Copy(Path.Combine(Environment.SystemDirectory, "where.exe"), exe);
        using (var lk = GitHubApps.OpenLocked(exe))
        {
            var sha = GitHubApps.Sha256Of(lk);
            Assert.Throws<IOException>(() => File.AppendAllText(exe, "x"));
            Assert.Throws<IOException>(() => File.Move(exe, exe + ".alt"));
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "/?") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true })!;
            p.StandardOutput.ReadToEnd(); p.WaitForExit();
            Assert.Equal(sha, GitHubApps.CheckProgram(exe, locked: lk).Sha256);
        }
        Directory.Delete(d, true);
    }

    [Fact(DisplayName = "T-0762 B2: Start-Erlaubnis wird zur Prüfsumme gemerkt; alte Liste ohne Feld bleibt lesbar")]
    public void B2_approve_start()
    {
        var tools = Tmp();
        File.WriteAllText(Path.Combine(tools, GitHubApps.ListName), """[{"id":"gh-a-b","repo":"a/b","version":"v1","exe":"b.exe","license":"MIT","asset":"b.zip","sha256":"00","description":"","installed":"2026-11-01T10:00:00+01:00"}]""");
        var e = Assert.Single(GitHubApps.List(tools)); Assert.Null(e.StartApproved);
        Assert.True(GitHubApps.ApproveStart(tools, "gh-a-b", "abc"));
        Assert.Equal("abc", Assert.Single(GitHubApps.List(tools)).StartApproved);
        Assert.False(GitHubApps.ApproveStart(tools, "gh-x-y", "abc"));
        Directory.Delete(tools, true);
    }
}
