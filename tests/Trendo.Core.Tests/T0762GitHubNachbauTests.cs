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

    // ───────────── Zweite Prüfrunde (N1, N2) ─────────────

    [WindowsFact(DisplayName = "T-0762 N1: projektlokale Skripte (gradlew.bat, mvnw.cmd) laufen mit „.\\…“ auch, wenn das Terminal den Projektordner nicht durchsucht; ohne „.\\“ nicht (Befehl nicht gefunden)")]
    public void N1_local_wrappers_resolve()
    {
        var d = Tmp();
        var marker = Path.Combine(d, "gelaufen.txt");
        File.WriteAllText(Path.Combine(d, "gradlew.bat"), $"@echo gradle %*> \"{marker}\"\r\n");
        File.WriteAllText(Path.Combine(d, "build.gradle"), "");
        var g = OpenSource.BuildHints(d, _ => true).Single(h => h.Tool == "gradlew");
        Assert.Equal(@".\gradlew.bat build", g.Command);
        var r = RunLikeTerminal(d, g.Command);
        Assert.Equal(0, r.Exit); Assert.Contains("gradle build", File.ReadAllText(marker));
        File.Delete(marker);
        Assert.NotEqual(0, RunLikeTerminal(d, "gradlew.bat build").Exit); // so stand es vorher da – mit der Schutz-Variable nicht auffindbar („… nicht gefunden“)
        Assert.False(File.Exists(marker));
        // Maven-Wrapper
        var m = Tmp();
        File.WriteAllText(Path.Combine(m, "pom.xml"), "<project/>"); File.WriteAllText(Path.Combine(m, "mvnw.cmd"), $"@echo maven %*> \"{Path.Combine(m, "m.txt")}\"\r\n");
        var mh = OpenSource.BuildHints(m, _ => true).Single(h => h.Tool == "mvnw");
        Assert.Equal(@".\mvnw.cmd package", mh.Command);
        Assert.Equal(0, RunLikeTerminal(m, mh.Command).Exit); Assert.Contains("maven package", File.ReadAllText(Path.Combine(m, "m.txt")));
        // nur „gradlew“ (Unix-Skript) unter Windows: nicht ausführbar → gesperrt
        var u = Tmp(); File.WriteAllText(Path.Combine(u, "gradlew"), "#!/bin/sh");
        Assert.False(OpenSource.BuildHints(u, _ => true).Single(h => h.Tool == "gradlew").Available);
        Directory.Delete(d, true); Directory.Delete(m, true); Directory.Delete(u, true);
    }

    [Fact(DisplayName = "T-0762 N2/Z1/Z2: Grün nur, wenn Startdatei und alle Programme/Bibliotheken signiert sind und es keine nicht signierbaren ausführbaren Inhalte oder Verknüpfungen gibt; Prüfsumme über ALLE Dateien")]
    public void N2_folder_check()
    {
        var d = Tmp(); Directory.CreateDirectory(Path.Combine(d, "resources"));
        var exe = Path.Combine(d, "app.exe"); File.WriteAllText(exe, "MZ");
        File.WriteAllText(Path.Combine(d, "helfer.dll"), "MZ"); File.WriteAllText(Path.Combine(d, "liesmich.txt"), "x");
        SignatureInfo Ok(string _) => new(SigState.Gueltig, "Beispiel GmbH", "ok");
        var allSigned = GitHubApps.CheckProgramFolder(exe, d, Ok);
        Assert.Equal(GitHubApps.Ampel.Gruen, allSigned.Ampel);
        Assert.Contains("Geprüft: Programme und Bibliotheken sind signiert von Beispiel GmbH", allSigned.Text);
        Assert.Contains("Daten- und Skriptdateien kann eine Signatur nicht abdecken", allSigned.Text);
        var onlyExe = GitHubApps.CheckProgramFolder(exe, d, f => f.EndsWith(".exe") ? new SignatureInfo(SigState.Gueltig, "Beispiel GmbH", "ok") : new SignatureInfo(SigState.Unsigniert, null, "nein"));
        Assert.Equal(GitHubApps.Ampel.Gelb, onlyExe.Ampel); Assert.True(onlyExe.NeedsRiskConsent); Assert.Contains("helfer.dll", onlyExe.Text);
        Assert.Equal(GitHubApps.Ampel.Rot, GitHubApps.CheckProgramFolder(exe, d, _ => new SignatureInfo(SigState.Unsigniert, null, "nein")).Ampel);
        // Z1c: nicht signierbare ausführbare Inhalte → Gelb mit Nennung, auch wenn alles Signierbare signiert ist
        foreach (var n in new[] { "resources/app.asar", "start.ahk", "app.exe.config", "python313._pth", "lib.zip", "modul.mjs" })
        {
            var f = Path.Combine(d, n); File.WriteAllText(f, "x");
            var c = GitHubApps.CheckProgramFolder(exe, d, Ok);
            Assert.Equal(GitHubApps.Ampel.Gelb, c.Ampel); Assert.Contains("nicht signierbar/prüfbar", c.Text); Assert.Contains(n, c.Text);
            File.Delete(f);
        }
        Assert.Equal(GitHubApps.Ampel.Gruen, GitHubApps.CheckProgramFolder(exe, d, Ok).Ampel);
        // Z1a: Prüfsumme über ALLE Dateien – auch eine Text-/Datendatei ändert sie
        var h1 = GitHubApps.FolderHashAll(d);
        Assert.Equal(GitHubApps.Fingerprint(d, GitHubApps.ExecSet(d, exe)).Key, allSigned.Sha256); // L1: Ergebnis = Fingerabdruck
        File.WriteAllText(Path.Combine(d, "liesmich.txt"), "geändert");
        var h2 = GitHubApps.FolderHashAll(d); Assert.NotEqual(h1, h2);
        File.WriteAllText(Path.Combine(d, "neu.dat"), "x");
        Assert.NotEqual(h2, GitHubApps.FolderHashAll(d));
        Directory.Delete(d, true);
    }

    [WindowsFact(DisplayName = "T-0762 Z2: Verknüpfung im Programmordner → Gelb mit Nennung; Pfad und Ziel fließen in die Prüfsumme")]
    public void Z2_reparse_point()
    {
        var d = Tmp(); var t1 = Tmp(); var t2 = Tmp();
        var exe = Path.Combine(d, "app.exe"); File.WriteAllText(exe, "MZ");
        var link = Path.Combine(d, "plugins");
        Assert.True(Cli.Run("cmd", ["/d", "/c", "mklink", "/J", link, t1]).Ok);
        SignatureInfo Ok(string _) => new(SigState.Gueltig, "Beispiel GmbH", "ok");
        var c = GitHubApps.CheckProgramFolder(exe, d, Ok);
        Assert.Equal(GitHubApps.Ampel.Gelb, c.Ampel); Assert.Contains("Verknüpfung", c.Text); Assert.Contains("plugins", c.Text);
        var h1 = GitHubApps.FolderHashAll(d);
        Directory.Delete(link);
        Assert.True(Cli.Run("cmd", ["/d", "/c", "mklink", "/J", link, t2]).Ok); // gleicher Name, anderes Ziel
        Assert.NotEqual(h1, GitHubApps.FolderHashAll(d));
        Directory.Delete(link); Directory.Delete(d, true); Directory.Delete(t1, true); Directory.Delete(t2, true);
    }

    [Fact(DisplayName = "T-0762 Z3a: Start-Erlaubnis für Herstellerprogramme (KI) wird getrennt gespeichert")]
    public void Z3_external_approval()
    {
        var tools = Tmp();
        Assert.Null(GitHubApps.ExternalApproval(tools, "ki:lmstudio"));
        GitHubApps.ApproveExternal(tools, "ki:lmstudio", "abc"); GitHubApps.ApproveExternal(tools, "ki:jan", "def");
        Assert.Equal("abc", GitHubApps.ExternalApproval(tools, "ki:lmstudio")); Assert.Equal("def", GitHubApps.ExternalApproval(tools, "ki:jan"));
        Directory.Delete(tools, true);
    }

    // ───────────── Vierte Prüfrunde (L1–L3) ─────────────

    [Fact(DisplayName = "T-0762 L1: Fingerabdruck = Index aller Dateien (ohne Lesen) + SHA-256 nur der ausführbaren Inhalte; Datendatei geändert → Index anders, ausführbare Summe gleich")]
    public void L1_fingerprint()
    {
        var d = Tmp(); var exe = Path.Combine(d, "app.exe"); File.WriteAllText(exe, "MZ"); File.WriteAllText(Path.Combine(d, "a.dll"), "MZ1"); File.WriteAllText(Path.Combine(d, "daten.bin"), "1234");
        var p1 = GitHubApps.Fingerprint(d, GitHubApps.ExecSet(d, exe));
        Assert.Equal(2, p1.ExecSha.Count); Assert.Equal(3, p1.FileCount);
        File.WriteAllText(Path.Combine(d, "daten.bin"), "12345");
        var p2 = GitHubApps.Fingerprint(d, GitHubApps.ExecSet(d, exe));
        Assert.NotEqual(p1.Index, p2.Index); Assert.Equal(p1.ExecHash, p2.ExecHash); Assert.NotEqual(p1.Key, p2.Key);
        File.WriteAllText(Path.Combine(d, "a.dll"), "MZ2");
        Assert.NotEqual(p2.ExecHash, GitHubApps.Fingerprint(d, GitHubApps.ExecSet(d, exe)).ExecHash);
        Assert.Equal(GitHubApps.CheckProgramFolder(exe, d, _ => new SignatureInfo(SigState.Gueltig, "X", "ok")).Sha256, GitHubApps.Fingerprint(d, GitHubApps.ExecSet(d, exe)).Key);
        Directory.Delete(d, true);
    }

    [WindowsFact(DisplayName = "T-0762 L1c: Signaturergebnis je Datei-Inhalt nur im Speicher – zweite Prüfung derselben Dateien ruft Windows nicht erneut auf")]
    public void L1_signature_cache()
    {
        var d = Tmp(); var exe = Path.Combine(d, "where.exe"); File.Copy(Path.Combine(Environment.SystemDirectory, "where.exe"), exe);
        File.WriteAllText(Path.Combine(d, "x.dll"), "MZ-eigene-" + Guid.NewGuid());
        var before = GitHubApps.SignatureCacheCount;
        var print = GitHubApps.Fingerprint(d, GitHubApps.ExecSet(d, exe));
        var c1 = GitHubApps.CheckProgramFolder(exe, d, print);
        Assert.True(GitHubApps.SignatureCacheCount >= before + 1);
        var mid = GitHubApps.SignatureCacheCount;
        var c2 = GitHubApps.CheckProgramFolder(exe, d, print);
        Assert.Equal(mid, GitHubApps.SignatureCacheCount); Assert.Equal(c1.Ampel, c2.Ampel);
        Directory.Delete(d, true);
    }

    [Fact(DisplayName = "T-0762 L3: package.json mit „main“ im Programmordner → Gelb mit Nennung")]
    public void L3_package_main()
    {
        var d = Tmp(); var exe = Path.Combine(d, "app.exe"); File.WriteAllText(exe, "MZ");
        Directory.CreateDirectory(Path.Combine(d, "resources", "app"));
        File.WriteAllText(Path.Combine(d, "resources", "app", "package.json"), """{"name":"x","main":"start.txt"}""");
        var c = GitHubApps.CheckProgramFolder(exe, d, _ => new SignatureInfo(SigState.Gueltig, "X", "ok"));
        Assert.Equal(GitHubApps.Ampel.Gelb, c.Ampel); Assert.Contains("resources/app/package.json", c.Text);
        File.WriteAllText(Path.Combine(d, "resources", "app", "package.json"), """{"name":"x"}""");
        Assert.Equal(GitHubApps.Ampel.Gruen, GitHubApps.CheckProgramFolder(exe, d, _ => new SignatureInfo(SigState.Gueltig, "X", "ok")).Ampel);
        Directory.Delete(d, true);
    }

    public sealed class MessungFactAttribute : FactAttribute { public MessungFactAttribute() { if (Environment.GetEnvironmentVariable("TRENDO_MESSUNG") != "1" || !OperatingSystem.IsWindows()) Skip = "Übersprungen – Messung nur mit TRENDO_MESSUNG=1 (legt bis 1,9 GB Testdaten an)."; } }

    [MessungFact(DisplayName = "T-0762 L1 Messung: alt (FolderHashAll) gegen neu (Fingerabdruck) bei 3 000 Dateien/286 MB und 20 000 Dateien/1,9 GB, erste Prüfung mit echten System-DLLs (warm)")]
    public void L1_measure()
    {
        var log = new System.Text.StringBuilder($"T-0762 L1 Messung · {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz} · {Environment.ProcessorCount} Kerne\n");
        var slower = new List<string>();
        var dlls = Directory.EnumerateFiles(Environment.SystemDirectory, "*.dll").Where(f => { try { var n = new FileInfo(f).Length; return n > 20_000 && n < 600_000; } catch (IOException) { return false; } }).Take(1000).ToList();
        foreach (var (files, mb, ndll) in new[] { (3000, 286, 300), (20000, 1900, 1000) })
        {
            var d = Tmp(); var exe = Path.Combine(d, "app.exe"); File.Copy(Path.Combine(Environment.SystemDirectory, "notepad.exe"), exe); File.SetAttributes(exe, FileAttributes.Normal);
            long dllBytes = 0;
            for (var i = 0; i < ndll && i < dlls.Count; i++) { var t = Path.Combine(d, "bin", (i / 100).ToString(), Path.GetFileName(dlls[i])); Directory.CreateDirectory(Path.GetDirectoryName(t)!); File.Copy(dlls[i], t); File.SetAttributes(t, FileAttributes.Normal); dllBytes += new FileInfo(t).Length; }
            var rest = files - 1 - Math.Min(ndll, dlls.Count); var each = Math.Max(1, (mb * 1024L * 1024L - dllBytes) / rest);
            var buf = new byte[each]; new Random(1).NextBytes(buf);
            for (var i = 0; i < rest; i++) { var t = Path.Combine(d, "daten", (i / 500).ToString(), $"d{i}.dat"); Directory.CreateDirectory(Path.GetDirectoryName(t)!); File.WriteAllBytes(t, buf); }
            long total = Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
            log.AppendLine($"\n## {files} Dateien, {total / 1048576.0:0} MB, davon {Math.Min(ndll, dlls.Count)} echte System-DLLs ({dllBytes / 1048576.0:0} MB)");
            GitHubApps.FolderHashAll(d); // aufwärmen
            var sw = System.Diagnostics.Stopwatch.StartNew(); GitHubApps.FolderHashAll(d); var alt = sw.Elapsed;
            log.AppendLine($"alt – Start mit Erlaubnis (FolderHashAll, liest alle Dateien): {alt.TotalSeconds:0.00} s");
            sw.Restart(); var p = GitHubApps.Fingerprint(d, GitHubApps.ExecSet(d, exe)); var neu = sw.Elapsed;
            log.AppendLine($"neu – Start mit Erlaubnis (Index + SHA-256 der {p.ExecSha.Count} ausführbaren Dateien): {neu.TotalSeconds:0.00} s (im Hintergrund)");
            sw.Restart(); GitHubApps.FolderIndex(d); log.AppendLine($"neu – Bestätigen (nur Index, Verzeichnisdaten): {sw.Elapsed.TotalSeconds:0.000} s");
            sw.Restart(); var c1 = GitHubApps.CheckProgramFolder(exe, d, p); var first = sw.Elapsed;
            log.AppendLine($"neu – erste Prüfung, Signaturen von {p.ExecSha.Count} Dateien (echtes WinVerifyTrust): {first.TotalSeconds:0.00} s → {c1.Ampel}");
            sw.Restart(); GitHubApps.CheckProgramFolder(exe, d, p); log.AppendLine($"neu – zweite Prüfung (Signatur-Zwischenspeicher im Speicher): {sw.Elapsed.TotalSeconds:0.00} s");
            Directory.Delete(d, true);
            if (neu > alt + TimeSpan.FromSeconds(0.5)) slower.Add($"{files}: neu {neu.TotalSeconds:0.00} s > alt {alt.TotalSeconds:0.00} s");
        }
        log.AppendLine("\nKalt (leerer Dateicache) nicht gemessen: den Windows-Dateicache zu leeren braucht Administratorrechte. Alle Werte warm; die Prüfung läuft in der App im Hintergrund.");
        var repo = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(repo, "Trendo.sln"))) repo = Path.GetDirectoryName(repo)!;
        var outDir = Path.Combine(repo, "reports", "T-0762"); Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "l1-messung.txt"), log.ToString().Replace(Environment.UserName, "<user>", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(slower);
    }
}
