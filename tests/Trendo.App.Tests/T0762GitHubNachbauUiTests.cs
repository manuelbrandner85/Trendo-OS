using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0762: X-06 „Projekte von GitHub holen“ (Installer-Ampel mit eigener Bestätigung, Quellcode im Code-Editor mit Bauanleitung, Entfernen ohne Rest)
/// und X-10 „Programme nachbauen“ (Anforderungsliste, Abgleich mit echtem Testlauf, Vergleich nebeneinander). Dazu der PC-Lauf mit echtem GitHub (nur mit TRENDO_PC_LAUF=1).</summary>
public class T0762_GitHubNachbau_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    static async Task Until(Func<bool> ok, int ms = 120_000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!ok() && sw.ElapsedMilliseconds < ms) { await Task.Delay(50); Pump(); }
        Pump();
    }
    static OssRepo Repo(string full, string lic = "MIT") => new(full, "Ein freies Programm", "https://github.com/" + full, "https://github.com/" + full + ".git", 10, lic, "C#", DateTimeOffset.UtcNow);
    /// <summary>Textfeld wie eine Person bearbeiten: hineinklicken, tippen, mit Enter bestätigen.</summary>
    static void Type(MainWindow w, string name, string text)
    {
        var tb = w.Find<TextBox>(name); tb.Focus(); tb.Text = text;
        tb.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Pump();
    }

    [AvaloniaFact(DisplayName = "T-0762 X-06 (UI, Signatur ersetzt, Installer-Lauf ersetzt): gültig signierter Installer = Grün ohne Häkchen, aber eigener Klick; Datei ist bis zum Start gesperrt; „Nicht ausführen“ verwirft; Knopf gehört zu genau seiner Karte")]
    public async Task Green_installer_own_click_and_tamper()
    {
        var (w, l, root) = Ui.Open();
        var inst = Path.Combine(root, "Tool-Setup-1.0.exe"); File.WriteAllBytes(inst, [.. "MZ"u8.ToArray(), .. Encoding.ASCII.GetBytes("Inno Setup")]);
        var programs = Path.Combine(root, "local", "Programs"); Directory.CreateDirectory(programs);
        w.GitHubInstallRootsOverride = [programs];
        w.GitHubSignatureOverride = _ => new SignatureInfo(SigState.Gueltig, "Beispiel GmbH", "Gültig signiert (in der Datei).");
        w.GitHubFetchOverride = _ => Task.FromResult((new GitHubApps.Release("v1.0", "v1.0", [new GitHubApps.Asset("Tool-1.0.exe", "https://github.com/x", 10, null)]), (string?)inst));
        var runs = 0;
        w.GitHubInstallerRunOverride = (_, _) => { runs++; var d = Path.Combine(programs, "tool"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "tool.exe"), new string('x', 400)); return Task.CompletedTask; };
        Assert.Null(await w.GitHubInstall(Repo("beispiel/tool"))); Pump();
        Assert.Equal(0, runs);
        w.Show("oss"); Pump();
        Assert.Contains("Grün", w.Find<TextBlock>("oss-installer-ampel").Text);
        Assert.Null(w.TryFind<CheckBox>("oss-installer-risk")); // Grün braucht kein Häkchen …
        Assert.True(w.Find<Button>("oss-installer-run").IsEnabled); // … aber einen eigenen Klick
        // B5: die geprüfte Datei ist bis zum Start gesperrt – austauschen oder ändern geht nicht
        Assert.Throws<IOException>(() => File.AppendAllText(inst, "verändert"));
        Assert.Throws<IOException>(() => File.Move(inst, inst + ".alt"));
        // B6: ein zweiter Installer ersetzt die Karte – der Knopf der alten Karte führt nichts aus
        var first = w.GitHubPendingInstaller!;
        Assert.Null(await w.GitHubInstall(Repo("beispiel/tool"))); Pump();
        Assert.NotSame(first, w.GitHubPendingInstaller);
        Assert.Null(await w.GitHubConfirmInstaller(first)); Pump();
        Assert.Equal(0, runs); Assert.NotNull(w.GitHubPendingInstaller);
        w.GitHubCancelInstaller(); Pump();
        Assert.Null(w.GitHubPendingInstaller);
        File.AppendAllText(inst, ""); // Sperre nach „Nicht ausführen“ wieder frei
        // erneut, dann „Nicht ausführen“
        Assert.Null(await w.GitHubInstall(Repo("beispiel/tool"))); Pump();
        w.Show("oss"); Pump();
        w.Find<Button>("oss-installer-cancel").Press(); Pump();
        Assert.Null(w.GitHubPendingInstaller); Assert.Equal(0, runs);
        // erneut, jetzt ausführen
        Assert.Null(await w.GitHubInstall(Repo("beispiel/tool"))); Pump();
        var exe = await w.GitHubConfirmInstaller(); Pump(); await w.GitHubStartCheckTask; Pump();
        Assert.Equal(1, runs); Assert.EndsWith("tool.exe", exe);
        // Z1b: auch nach dem Installer (und auch bei Grün) ist der erste Start ein eigener Schritt – Karte ohne Häkchen, eigener Klick
        Assert.Empty(l.Opened);
        Assert.Equal(GitHubApps.Ampel.Gruen, w.GitHubPendingStart!.Check.Ampel); Assert.False(w.GitHubPendingStart!.Check.NeedsRiskConsent);
        w.Show("oss"); Pump();
        Assert.Null(w.TryFind<CheckBox>("oss-firststart-risk"));
        w.Find<Button>("oss-firststart-run").Press(); Pump();
        Assert.Contains(l.Opened, o => o.EndsWith("tool.exe"));
        var acts = w.Core.Audit.ReadAll().Select(a => a.Action).ToList();
        Assert.Contains("github.installer.held", acts); Assert.Contains("github.installer.consent", acts); Assert.Contains("github.installer.declined", acts);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0762 X-06 (UI): geholtes Projekt mit einem Klick im Code-Editor, Bauanleitung als Knopf, echter dotnet-Bau (übersprungen ohne SDK); Entfernen ohne Rest")]
    public async Task Fetched_project_in_code_editor()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var dir = Path.Combine(w.OssFolder, "beispiel_hallo"); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Hallo.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(dir, "Program.cs"), "System.Console.WriteLine(\"Hallo\");\n");
        File.WriteAllText(OpenSource.OriginFile(dir), "Quelle: https://github.com/beispiel/hallo.git\nLizenz: MIT\n");
        w.Show("oss"); Pump();
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Bauen: dotnet build"));
        w.Find<Button>("oss-local-code-0").Press(); Pump();
        Assert.Equal("code", w.CurrentView);
        Assert.Equal(Path.GetFullPath(dir), w.CodeRoot);
        var hint = w.Find<Button>("ce-hint-0");
        Assert.Equal("▶ dotnet build \"Hallo.csproj\"", hint.Content);
        Assert.Equal("⚠ " + OpenSource.BuildWarning, w.Find<TextBlock>("ce-hints-warn").Text); // B1: fest und ehrlich
        if (DevBuild.HasDotNetSdk())
        {
            Assert.True(hint.IsEnabled);
            w.CodeRealToolsInTests = true;
            hint.Press(); Pump();
            // B1: erster Lauf nur nach eigener Bestätigung – der Klick auf den Vorschlag allein startet nichts
            Assert.False(w.CodeRunning); Assert.DoesNotContain("dotnet build", w.CodeTerminalText);
            Assert.NotNull(w.TryFind<Border>("ce-hint-ask"));
            w.Find<Button>("ce-hint-cancel").Press(); Pump();
            Assert.Null(w.CodeHintPending); Assert.False(OpenSource.BuildApproved(dir));
            w.Find<Button>("ce-hint-0").Press(); Pump();
            w.Find<Button>("ce-hint-confirm").Press(); Pump();
            Assert.True(OpenSource.BuildApproved(dir));
            await Until(() => !w.CodeRunning, 300_000);
            Assert.Contains("(Exit 0)", w.CodeTerminalText);
            Assert.True(Directory.EnumerateFiles(Path.Combine(dir, "bin"), "Hallo.dll", SearchOption.AllDirectories).Any());
        }
        w.Show("oss"); Pump();
        w.Find<Button>("oss-local-del-0").Press(); Pump();
        w.Find<Button>("oss-local-del-0").Press(); Pump();
        Assert.False(Directory.Exists(dir)); Assert.False(File.Exists(OpenSource.OriginFile(dir)));
        Assert.Empty(Directory.GetFileSystemEntries(w.OssFolder));
        Assert.Null(w.CodeRoot); // war im Code-Editor offen – vorher geschlossen
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0762 X-10 (UI): Anforderungen in der Tabelle anlegen und bearbeiten, Rückgängig, Test verknüpfen, Abgleich mit echtem Python-Testlauf, Export .md, Treffer nebeneinander")]
    public async Task Requirements_and_side_by_side()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.Show("nachbau");
        w.Find<TextBox>("clean-name").Text = "Notizen";
        w.Find<Button>("clean-create").Press();
        var d = Path.Combine(w.CleanFolder, "Notizen");
        w.Find<Button>("clean-req-add").Press(); w.Find<Button>("clean-req-add").Press();
        Assert.Equal(2, CleanRoom.LoadRequirements(d).Count);
        Type(w, "clean-req-fn-0", "Notiz speichern");
        Type(w, "clean-req-src-0", "Handbuch Kapitel 2");
        Type(w, "clean-req-test-0", "2-Umsetzung/test_notiz.py#test_speichern");
        Type(w, "clean-req-fn-1", "Notiz löschen");
        Type(w, "clean-req-test-1", "2-Umsetzung/test_notiz.py#test_loeschen");
        var saved = CleanRoom.LoadRequirements(d);
        Assert.Equal("Notiz speichern", saved[0].Function); Assert.Equal("Handbuch Kapitel 2", saved[0].Source);
        Assert.Equal(DateTimeOffset.Now.ToString("yyyy-MM-dd"), saved[0].Date);
        // Rückgängig nimmt die letzte Änderung zurück (gespeichert)
        Type(w, "clean-req-fn-1", "falsch");
        w.Find<Button>("clean-req-undo").Press(); Pump();
        Assert.Equal("Notiz löschen", CleanRoom.LoadRequirements(d)[1].Function);
        Assert.Equal("Notiz löschen", w.Find<TextBox>("clean-req-fn-1").Text);
        // eigener Code mit Tests in „2-Umsetzung“
        var impl = Path.Combine(d, "2-Umsetzung");
        File.WriteAllText(Path.Combine(impl, "notiz.py"), "def speichern(t):\n    return t.strip()\n\ndef loeschen(liste, i):\n    return liste\n");
        File.WriteAllText(Path.Combine(impl, "test_notiz.py"), "import unittest\nimport notiz\n\nclass T(unittest.TestCase):\n    def test_speichern(self):\n        self.assertEqual(notiz.speichern(' a '), 'a')\n\n    def test_loeschen(self):\n        self.assertEqual(notiz.loeschen([1, 2], 0), [2])\n");
        w.CleanRealToolsInTests = true;
        await w.CleanReqReconcile(); Pump();
        var rec = CleanRoom.LoadRequirements(d);
        if (DevBuild.Missing("python") is null)
        {
            Assert.Equal(CleanRoom.StatusDone, rec[0].Status); Assert.Contains("bestanden", rec[0].Note);
            Assert.Equal(CleanRoom.StatusOpen, rec[1].Status); Assert.Contains("fehlgeschlagen", rec[1].Note);
            Assert.Contains("1 erfüllt", w.Find<TextBlock>("clean-req-summary").Text);
        }
        Assert.All(rec, r => Assert.NotEqual("", r.Link)); // jede Anforderung hat einen verknüpften Test
        // Verknüpfte Datei im Code-Editor
        w.Find<Button>("clean-req-code-0").Press(); Pump();
        Assert.Equal("code", w.CurrentView); Assert.EndsWith("test_notiz.py", w.CodeActiveFile);
        w.Show("nachbau"); Pump();
        // Export
        w.Find<Button>("clean-req-export").Press(); Pump();
        var md = File.ReadAllText(Path.Combine(d, "3-Pruefung", "ANFORDERUNGEN.md"));
        Assert.Contains("| A-001 | Notiz speichern |", md); Assert.Contains("Handbuch Kapitel 2", md);
        // Ähnlichkeitsprüfung: Treffer nebeneinander mit markierten Zeilen
        var orig = Path.Combine(root, "vergleich"); Directory.CreateDirectory(orig);
        File.WriteAllText(Path.Combine(orig, "fremd.py"), "# fremd\ndef save(t):\n    return t.strip()\n\ndef remove(items, i):\n    return items\n");
        w.CleanCheck(orig); Pump();
        Assert.NotNull(w.TryFind<StackPanel>("clean-side"));
        var side = w.CleanSideBySide!.Value;
        Assert.EndsWith("notiz.py", side.Ours); Assert.EndsWith("fremd.py", side.Theirs);
        Assert.Contains(2, side.OursLines); Assert.Contains(3, side.TheirsLines);
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Dein Code ·")); Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Vergleichstext ·"));
        var report = File.ReadAllText(Path.Combine(d, "3-Pruefung", "PRUEFBERICHT.md"));
        Assert.Contains("Herkunft: eigener Code „2-Umsetzung/notiz.py“, Vergleichstext „vergleich/fremd.py“", report);
        Assert.Contains("Handbuch Kapitel 2 | " + DateTimeOffset.Now.ToString("yyyy-MM-dd"), report);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0762 B2 (UI): unsigniertes ZIP-Programm startet nach „Installieren & starten“ NICHT – Ampel Rot, Start erst nach Häkchen und eigenem Klick, danach ohne Rückfrage; geänderte Datei fragt erneut")]
    public async Task B2_unsigned_program_first_start()
    {
        var (w, l, root) = Ui.Open();
        var zip = Path.Combine(root, "dl.zip");
        using (var z = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create)) using (var s = new StreamWriter(z.CreateEntry("b/b.exe").Open())) s.Write("MZ unsigniert");
        w.GitHubFetchOverride = _ => Task.FromResult<(GitHubApps.Release, string?)>((new GitHubApps.Release("v2.0", "", [new("b-windows-x64.zip", "https://github.com/a/b/releases/download/v2.0/b-windows-x64.zip", new FileInfo(zip).Length, null)]), zip));
        Assert.NotNull(await w.GitHubInstall(Repo("a/b"))); Pump(); await w.GitHubStartCheckTask; Pump();
        Assert.Empty(l.Opened); // „Installieren & starten“ startet Unsigniertes nicht
        var pend = w.GitHubPendingStart!;
        Assert.Equal(GitHubApps.Ampel.Rot, pend.Check.Ampel); // echte Authenticode-Prüfung
        if (w.SetupFinishShown) { w.Find<Button>("setup-overlay-done").Press(); Pump(); }
        w.Show("oss"); Pump();
        Assert.Contains("Rot", w.Find<TextBlock>("oss-firststart-ampel").Text);
        Assert.False(w.Find<Button>("oss-firststart-run").IsEnabled);
        Assert.False(w.GitHubConfirmStart()); Assert.Empty(l.Opened);
        w.Find<CheckBox>("oss-firststart-risk").IsChecked = true; Pump();
        Assert.Empty(l.Opened); // Häkchen allein startet nichts
        w.Find<Button>("oss-firststart-run").Press(); Pump();
        var exe = Assert.Single(l.Opened); Assert.EndsWith("b.exe", exe);
        var entry = Assert.Single(GitHubApps.List(w.ToolsRootForTests));
        Assert.NotNull(entry.StartApproved);
        // erlaubt: nächster Start ohne Rückfrage
        w.GitHubLaunch(entry); await w.GitHubStartCheckTask; Pump(); Assert.Equal(2, l.Opened.Count); Assert.Null(w.GitHubPendingStart);
        // Programmdatei geändert: erneut fragen – N6: Start von „Programme“ springt zur Karte „Erster Start“
        File.AppendAllText(exe, "neu");
        w.Show("components"); Pump();
        w.Find<Button>("btn-gh-start-" + entry.Id).Press(); Pump(); await w.GitHubStartCheckTask; Pump();
        Assert.Equal("oss", w.CurrentView); Assert.NotNull(w.TryFind<Border>("oss-firststart-card"));
        Assert.NotNull(w.GitHubPendingStart); Assert.Equal(2, l.Opened.Count);
        w.GitHubCancelStart();
        // N2: Erlaubnis gilt für den Ordner – eine neue Skript-/DLL-Datei daneben fragt erneut; Startdatei signiert, DLL nicht → Gelb
        w.GitHubAcceptStartRisk(false);
        w.GitHubLaunch(entry); await w.GitHubStartCheckTask; Pump(); w.GitHubAcceptStartRisk(true); Assert.True(w.GitHubConfirmStart()); Assert.Equal(3, l.Opened.Count);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(exe)!, "helfer.dll"), "MZ");
        w.GitHubLaunch(entry); await w.GitHubStartCheckTask; Pump(); Assert.Equal(3, l.Opened.Count); w.GitHubCancelStart();
        w.GitHubSignatureOverride = f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? new SignatureInfo(SigState.Gueltig, "Beispiel GmbH", "Gültig signiert (in der Datei).") : new SignatureInfo(SigState.Unsigniert, null, "Die Datei ist nicht signiert.");
        w.GitHubLaunch(entry); await w.GitHubStartCheckTask; Pump();
        Assert.Equal(GitHubApps.Ampel.Gelb, w.GitHubPendingStart!.Check.Ampel); Assert.Contains("helfer.dll", w.GitHubPendingStart!.Check.Text);
        w.GitHubCancelStart();
        // alles gültig signiert (Signatur ersetzt): Z1b – trotzdem eigener Schritt, Grün ohne Häkchen; Text nennt ehrlich, was eine Signatur nicht abdeckt
        w.GitHubSignatureOverride = _ => new SignatureInfo(SigState.Gueltig, "Beispiel GmbH", "Gültig signiert (in der Datei).");
        w.GitHubLaunch(entry); await w.GitHubStartCheckTask; Pump(); Assert.Equal(3, l.Opened.Count);
        Assert.Equal(GitHubApps.Ampel.Gruen, w.GitHubPendingStart!.Check.Ampel);
        Assert.Contains("Daten- und Skriptdateien kann eine Signatur nicht abdecken", w.GitHubPendingStart!.Check.Text);
        Assert.True(w.GitHubConfirmStart()); Assert.Equal(4, l.Opened.Count);
        // Z1a: Erlaubnis gilt für ALLE Dateien des Ordners – auch eine geänderte Textdatei fragt erneut
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(exe)!, "liesmich.txt"), "neu");
        w.GitHubLaunch(entry); await w.GitHubStartCheckTask; Pump(); Assert.Equal(4, l.Opened.Count); Assert.NotNull(w.GitHubPendingStart); w.GitHubCancelStart();
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0762 B3 (UI): Abgleich sperrt den Knopf, zeigt „Abbrechen“, schreibt nach Projektwechsel in die richtige Liste; Abbrechen beendet den Lauf (übersprungen ohne Python)")]
    public async Task B3_reconcile_lock_switch_cancel()
    {
        if (DevBuild.Missing("python") is not null) { Assert.Skip("Übersprungen – Python fehlt."); return; }
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var a = CleanRoom.Create(w.CleanFolder, "Erstes", "x"); var b = CleanRoom.Create(w.CleanFolder, "Zweites", "y");
        File.WriteAllText(Path.Combine(a, "2-Umsetzung", "test_a.py"), "import time, unittest\nclass T(unittest.TestCase):\n    def test_a(self):\n        time.sleep(3)\n");
        w.CleanOpen(a); w.Show("nachbau"); Pump();
        var r = w.CleanReqAdd("A"); w.CleanReq.Update(r.Id, x => x with { Link = "2-Umsetzung/test_a.py#test_a" });
        w.CleanRealToolsInTests = true;
        var t = w.CleanReqReconcile(); Pump();
        Assert.True(w.CleanReconcileRunning);
        Assert.False(w.Find<Button>("clean-req-reconcile").IsEnabled);
        Assert.True(w.Find<Button>("clean-req-cancel").IsVisible);
        Assert.Contains(w.GetLogicalTexts(), s => s.Contains("führt Code aus „2-Umsetzung“ mit deinen Rechten aus"));
        w.CleanOpen(b); w.Show("nachbau"); Pump(); // Projekt wechseln, während der Test läuft
        await t; Pump();
        Assert.Equal(CleanRoom.StatusDone, Assert.Single(CleanRoom.LoadRequirements(a)).Status);
        Assert.False(File.Exists(CleanRoom.RequirementsFile(b))); // nichts in die falsche Liste
        // Abbrechen
        File.WriteAllText(Path.Combine(a, "2-Umsetzung", "test_a.py"), "import time, unittest\nclass T(unittest.TestCase):\n    def test_a(self):\n        time.sleep(120)\n");
        w.CleanOpen(a); w.Show("nachbau"); Pump();
        w.CleanReq.Update(r.Id, x => x with { Status = CleanRoom.StatusOpen, Note = "" });
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var t2 = w.CleanReqReconcile(); Pump();
        await Task.Delay(1500); Pump();
        w.Find<Button>("clean-req-cancel").Press(); Pump();
        await t2; Pump();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(60));
        Assert.False(w.CleanReconcileRunning);
        Assert.Equal(CleanRoom.StatusOpen, Assert.Single(CleanRoom.LoadRequirements(a)).Status); // unverändert
        Assert.Contains("abgebrochen", w.Find<TextBlock>("clean-req-summary").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0762 N3a (UI): Ausführen/Bauen im Code-Editor fragen bei einem nicht freigegebenen Projekt aus „Open Source“ zuerst nach – auch ohne den Weg über „Im Code-Editor“; echter Python-Bau nach Bestätigung")]
    public async Task N3a_code_editor_asks_for_oss()
    {
        if (DevBuild.Missing("python") is not null) { Assert.Skip("Übersprungen – Python fehlt."); return; }
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var dir = Path.Combine(w.OssFolder, "beispiel_py"); Directory.CreateDirectory(Path.Combine(dir, "pkg"));
        var marker = Path.Combine(dir, "GELAUFEN.txt");
        File.WriteAllText(Path.Combine(dir, "main.py"), $"open(r'{marker}', 'w').write('x')\n");
        File.WriteAllText(Path.Combine(dir, "pkg", "modul.py"), "def f():\n    return 1\n");
        File.WriteAllText(OpenSource.OriginFile(dir), "Quelle: https://github.com/beispiel/py.git\n");
        w.CodeRealToolsInTests = true;
        w.CodeOpenFolder(dir); w.Show("code"); Pump(); // direkt im Code-Editor geöffnet, nicht über „Im Code-Editor“
        w.CodeRunProject(); w.Show("code"); Pump();
        Assert.False(w.CodeRunning); Assert.False(File.Exists(marker));
        Assert.Equal("Ausführen", w.CodeHintPending); Assert.NotNull(w.TryFind<Border>("ce-hint-ask"));
        w.Find<Button>("ce-hint-cancel").Press(); Pump();
        w.CodeBuild(); w.Show("code"); Pump();
        Assert.Equal("Bauen", w.CodeHintPending); Assert.False(w.CodeRunning);
        w.Find<Button>("ce-hint-confirm").Press(); Pump();
        Assert.True(OpenSource.BuildApproved(dir));
        await Until(() => !w.CodeRunning, 120_000);
        Assert.Contains("Gebaut (Exit 0)", w.CodeOutput); Assert.False(File.Exists(marker)); // Bauen prüft nur, führt main.py nicht aus
        w.CodeBuild(); Pump(); Assert.Null(w.CodeHintPending); // freigegeben: keine zweite Rückfrage
        await Until(() => !w.CodeRunning, 120_000);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0762 N5 (UI): Abgleich A → B → A – Bearbeitungen in A während des Laufs bleiben erhalten, nur der Status kommt hinzu (übersprungen ohne Python)")]
    public async Task N5_switch_back_keeps_edits()
    {
        if (DevBuild.Missing("python") is not null) { Assert.Skip("Übersprungen – Python fehlt."); return; }
        var (w, _, _) = Ui.Open();
        var a = CleanRoom.Create(w.CleanFolder, "Erstes", "x"); var b = CleanRoom.Create(w.CleanFolder, "Zweites", "y");
        File.WriteAllText(Path.Combine(a, "2-Umsetzung", "test_a.py"), "import time, unittest\nclass T(unittest.TestCase):\n    def test_a(self):\n        time.sleep(3)\n");
        w.CleanOpen(a); w.Show("nachbau"); Pump();
        var r = w.CleanReqAdd("alt"); w.CleanReq.Update(r.Id, x => x with { Link = "2-Umsetzung/test_a.py#test_a" });
        w.CleanRealToolsInTests = true;
        var t = w.CleanReqReconcile(); Pump();
        w.CleanOpen(b); w.Show("nachbau"); Pump();
        w.CleanOpen(a); w.Show("nachbau"); Pump();
        w.CleanReq.Update(r.Id, x => x with { Function = "während des Laufs geändert", Source = "Handbuch S. 3" });
        var extra = w.CleanReqAdd("neu dazu");
        await t; Pump();
        var list = CleanRoom.LoadRequirements(a);
        Assert.Equal(2, list.Count);
        Assert.Equal("während des Laufs geändert", list[0].Function); Assert.Equal("Handbuch S. 3", list[0].Source);
        Assert.Equal(CleanRoom.StatusDone, list[0].Status);
        Assert.Equal(extra.Id, list[1].Id);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0762 B4 (UI): beschädigte Anforderungsliste wird angezeigt statt still überschrieben")]
    public void B4_broken_list_shown()
    {
        var (w, _, _) = Ui.Open();
        var a = CleanRoom.Create(w.CleanFolder, "Kaputt", "x");
        File.WriteAllText(CleanRoom.RequirementsFile(a), "[{kaputt");
        w.CleanOpen(a); w.Show("nachbau"); Pump();
        Assert.Contains("beschädigt", w.Find<TextBlock>("clean-req-problem").Text);
        w.Find<Button>("clean-req-add").Press(); Pump();
        Assert.Single(Directory.GetFiles(Path.Combine(a, "1-Beschreibung"), "anforderungen.defekt-*.json"));
        Assert.Contains("gesichert", w.Find<TextBlock>("clean-req-problem").Text);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0762 Z3a/Z3b (UI, Installer-Lauf ersetzt): KI-Installer vom Hersteller erst nach Ampel (echte Prüfung: Rot) und eigener Rückfrage, Nein führt nichts aus; erster Start des Herstellerprogramms über die Karte")]
    public async Task Z3_vendor_installer_and_start()
    {
        var (w, l, root) = Ui.Open();
        w.LocalAppDataDir = Path.Combine(root, "local");
        var setup = Path.Combine(root, "LM-Studio-Setup.exe"); File.WriteAllText(setup, "MZ");
        w.KiDownloadOverride = (_, _) => Task.FromResult<(string, string?)>((setup, null));
        var runs = 0;
        w.KiInstallerRunOverride = _ => { runs++; Assert.Throws<IOException>(() => File.AppendAllText(setup, "x")); var d = Path.Combine(w.LocalAppDataDir, "Programs", "LM Studio"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "LM Studio.exe"), "x"); return Task.CompletedTask; };
        MainWindow.Wirkung? asked = null;
        w.FreigabeConfirm = q => { asked = q; return Task.FromResult(false); };
        Assert.False(await w.KiSourceInstall("lmstudio")); Pump();
        Assert.Equal(0, runs); // Nein: Installer läuft nicht
        Assert.NotNull(asked); Assert.Contains("● Rot", asked!.Text); Assert.Contains("SHA-256", asked.Text); Assert.Equal("Trotzdem ausführen", asked.YesLabel);
        if (w.SetupFinishShown) { w.Find<Button>("setup-overlay-done").Press(); Pump(); }
        File.WriteAllText(setup, "MZ"); // der abgelehnte Installer wurde gelöscht – neu „laden“
        w.FreigabeConfirm = q => { asked = q; return Task.FromResult(true); };
        Assert.True(await w.KiSourceInstall("lmstudio")); Pump();
        Assert.Equal(1, runs); // die Datei war während des Laufs gesperrt (Prüfung im Ersatz-Lauf)
        if (w.SetupFinishShown) { w.Find<Button>("setup-overlay-done").Press(); Pump(); }
        // Z3a: Start des Herstellerprogramms – erst Karte, dann eigener Klick; danach ohne Rückfrage
        Assert.False(w.KiSourceStart("lmstudio")); Pump(); await w.GitHubStartCheckTask; Pump();
        Assert.Empty(l.Opened); Assert.NotNull(w.GitHubPendingStart); Assert.Equal("oss", w.CurrentView);
        w.GitHubAcceptStartRisk(true); Assert.True(w.GitHubConfirmStart()); Pump();
        Assert.Single(l.Opened, o => o.EndsWith("LM Studio.exe"));
        w.KiSourceStart("lmstudio"); await w.GitHubStartCheckTask; Pump(); Assert.Equal(2, l.Opened.Count);
        Assert.NotNull(GitHubApps.ExternalApproval(w.ToolsRootForTests, "ki:lmstudio"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0762 L1 (UI, Signatur ersetzt und künstlich verlangsamt): „Starten“ kehrt sofort zurück, Prüfung läuft im Hintergrund mit Fortschritt und „Abbrechen“; nach Erlaubnis nur Index + ausführbare Inhalte")]
    public async Task L1_click_returns_immediately()
    {
        var (w, l, root) = Ui.Open();
        var zip = Path.Combine(root, "dl.zip");
        using (var z = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
        {
            using (var s = new StreamWriter(z.CreateEntry("b/b.exe").Open())) s.Write("MZ");
            for (var i = 0; i < 60; i++) using (var s = new StreamWriter(z.CreateEntry($"b/lib{i}.dll").Open())) s.Write("MZ" + i);
            for (var i = 0; i < 400; i++) using (var s = new StreamWriter(z.CreateEntry($"b/daten/d{i}.dat").Open())) s.Write(new string('x', 2000));
        }
        w.GitHubFetchOverride = _ => Task.FromResult<(GitHubApps.Release, string?)>((new GitHubApps.Release("v2.0", "", [new("b-windows-x64.zip", "https://github.com/a/b/releases/download/v2.0/b-windows-x64.zip", new FileInfo(zip).Length, null)]), zip));
        Assert.NotNull(await w.GitHubInstall(Repo("a/b"), start: false)); Pump();
        if (w.SetupFinishShown) { w.Find<Button>("setup-overlay-done").Press(); Pump(); }
        var entry = Assert.Single(GitHubApps.List(w.ToolsRootForTests));
        w.GitHubSignatureOverride = _ => { Thread.Sleep(25); return new SignatureInfo(SigState.Gueltig, "Beispiel GmbH", "Gültig signiert (in der Datei)."); }; // 61 × 25 ms ≈ 1,5 s
        w.Show("oss"); Pump();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        w.GitHubLaunch(entry);
        var click = sw.ElapsedMilliseconds;
        Assert.True(click < 300, $"Klick dauerte {click} ms");
        Assert.True(w.GitHubStartChecking);
        w.Show("oss"); Pump();
        Assert.NotNull(w.TryFind<Border>("oss-check-card")); // Fortschritt mit Abbrechen, Oberfläche bedienbar
        w.Show("components"); Pump(); w.Show("oss"); Pump();
        w.Find<Button>("oss-check-cancel").Press(); Pump();
        await w.GitHubStartCheckTask; Pump();
        Assert.Null(w.GitHubPendingStart); Assert.Empty(l.Opened); Assert.False(w.GitHubStartChecking);
        File.AppendAllText(GitHubApps.ExePath(w.ToolsRootForTests, entry), ""); // Sperren nach dem Abbrechen frei
        // erneut, bis zur Karte (Grün), bestätigen
        w.GitHubLaunch(entry); await w.GitHubStartCheckTask; Pump();
        Assert.Equal(GitHubApps.Ampel.Gruen, w.GitHubPendingStart!.Check.Ampel);
        Assert.True(w.GitHubConfirmStart()); Assert.Single(l.Opened);
        // erlaubt: schneller Weg ohne Signaturprüfung (Signatur-Ersatz würde sonst wieder 1,5 s brauchen)
        var calls = 0; w.GitHubSignatureOverride = _ => { calls++; return new SignatureInfo(SigState.Gueltig, "Beispiel GmbH", "ok"); };
        sw.Restart(); w.GitHubLaunch(entry); Assert.True(sw.ElapsedMilliseconds < 300);
        await w.GitHubStartCheckTask; Pump();
        Assert.Equal(2, l.Opened.Count); Assert.Equal(0, calls);
        // Index: geänderte Datendatei (nicht gelesen, nur Verzeichnisdaten) fragt erneut
        File.WriteAllText(Path.Combine(w.ToolsRootForTests, entry.Id, "b", "daten", "d0.dat"), "anders");
        w.GitHubLaunch(entry); await w.GitHubStartCheckTask; Pump();
        Assert.Equal(2, l.Opened.Count); Assert.NotNull(w.GitHubPendingStart); w.GitHubCancelStart();
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0762 L2 (UI): mit Verknüpfung im Programmordner wird keine Erlaubnis gespeichert – jeder Start fragt (übersprungen ohne Windows)")]
    public async Task L2_links_always_ask()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Nur Windows."); return; }
        var (w, l, root) = Ui.Open();
        var zip = Path.Combine(root, "dl.zip");
        using (var z = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create)) using (var s = new StreamWriter(z.CreateEntry("b/b.exe").Open())) s.Write("MZ");
        w.GitHubFetchOverride = _ => Task.FromResult<(GitHubApps.Release, string?)>((new GitHubApps.Release("v2.0", "", [new("b-windows-x64.zip", "https://github.com/a/b/releases/download/v2.0/b-windows-x64.zip", new FileInfo(zip).Length, null)]), zip));
        Assert.NotNull(await w.GitHubInstall(Repo("a/b"), start: false)); Pump();
        var entry = Assert.Single(GitHubApps.List(w.ToolsRootForTests));
        var outside = Path.Combine(root, "draussen"); Directory.CreateDirectory(outside);
        Assert.True(Cli.Run("cmd", ["/d", "/c", "mklink", "/J", Path.Combine(w.ToolsRootForTests, entry.Id, "b", "plugins"), outside]).Ok);
        w.GitHubSignatureOverride = _ => new SignatureInfo(SigState.Gueltig, "Beispiel GmbH", "ok");
        w.GitHubLaunch(entry); await w.GitHubStartCheckTask; Pump();
        Assert.Equal(GitHubApps.Ampel.Gelb, w.GitHubPendingStart!.Check.Ampel); Assert.Contains("nicht gespeichert", w.GitHubPendingStart!.Check.Text);
        w.GitHubAcceptStartRisk(true); Assert.True(w.GitHubConfirmStart()); Assert.Single(l.Opened);
        Assert.Null(Assert.Single(GitHubApps.List(w.ToolsRootForTests)).StartApproved);
        w.GitHubLaunch(entry); await w.GitHubStartCheckTask; Pump();
        Assert.NotNull(w.GitHubPendingStart); Assert.Single(l.Opened); // fragt wieder
        w.GitHubCancelStart();
        Directory.Delete(Path.Combine(w.ToolsRootForTests, entry.Id, "b", "plugins"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0762 P1 (UI): Abbruch nach dem Ende der Hintergrund-Prüfung – weder Karte noch Start (auch bei erlaubtem Programm); Sperren frei")]
    public async Task P1_cancel_after_background()
    {
        var (w, l, root) = Ui.Open();
        var zip = Path.Combine(root, "dl.zip");
        using (var z = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create)) using (var s = new StreamWriter(z.CreateEntry("b/b.exe").Open())) s.Write("MZ");
        w.GitHubFetchOverride = _ => Task.FromResult<(GitHubApps.Release, string?)>((new GitHubApps.Release("v2.0", "", [new("b-windows-x64.zip", "https://github.com/a/b/releases/download/v2.0/b-windows-x64.zip", new FileInfo(zip).Length, null)]), zip));
        Assert.NotNull(await w.GitHubInstall(Repo("a/b"), start: false)); Pump();
        var entry = Assert.Single(GitHubApps.List(w.ToolsRootForTests));
        var exe = GitHubApps.ExePath(w.ToolsRootForTests, entry);
        w.GitHubSignatureOverride = _ => new SignatureInfo(SigState.Gueltig, "Beispiel GmbH", "ok");
        // noch nicht erlaubt: Abbruch nach der Prüfung → keine Karte
        var hits = 0; w.GitHubCheckAfterBackground = () => { hits++; w.GitHubCancelCheck(); };
        w.GitHubLaunch(entry); await w.GitHubStartCheckTask; Pump();
        Assert.Equal(1, hits); Assert.Null(w.GitHubPendingStart); Assert.Empty(l.Opened);
        File.AppendAllText(exe, ""); // Sperren frei
        // erlauben (normaler Weg), dann erlaubter Start mit Abbruch → kein Start
        w.GitHubCheckAfterBackground = null;
        w.GitHubLaunch(entry); await w.GitHubStartCheckTask; Pump();
        Assert.True(w.GitHubConfirmStart()); Assert.Single(l.Opened);
        w.GitHubCheckAfterBackground = () => w.GitHubCancelCheck();
        w.GitHubLaunch(entry); await w.GitHubStartCheckTask; Pump();
        Assert.Single(l.Opened); Assert.Null(w.GitHubPendingStart);
        File.AppendAllText(exe, "");
        // ohne Abbruch startet der erlaubte Weg wieder
        w.GitHubCheckAfterBackground = null;
        File.WriteAllText(exe, "MZ"); w.GitHubLaunch(entry); await w.GitHubStartCheckTask; Pump(); // Inhalt gleich, Zeit neu → Index anders: Karte
        Assert.NotNull(w.GitHubPendingStart); w.GitHubCancelStart();
        w.Close();
    }

    // ───────────── PC-Lauf mit echtem GitHub ─────────────

    static string Clean(string s)
    {
        foreach (var (p, r) in new[] { (Path.GetTempPath().TrimEnd('\\', '/'), "%TEMP%"), (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"C:\Users\<user>") })
            if (p.Length > 3) s = s.Replace(p, r, StringComparison.OrdinalIgnoreCase);
        return s.Replace(Environment.UserName, "<user>", StringComparison.OrdinalIgnoreCase).Replace(Environment.MachineName, "<pc>", StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact(DisplayName = "T-0762 X-06 PC-Lauf: Suche → freies Windows-Programm (ripgrep) mit Prüfsumme holen → starten → per PID beenden → Entfernen ohne Rest; falsche Prüfsumme abgewiesen; Quellcode holen → Code-Editor → bauen → entfernen (nur mit TRENDO_PC_LAUF=1)")]
    public async Task Pc_run_real_github()
    {
        if (Environment.GetEnvironmentVariable("TRENDO_PC_LAUF") != "1") { Assert.Skip("Übersprungen – PC-Lauf nur mit TRENDO_PC_LAUF=1 (braucht Internet, öffentliche GitHub-API ohne Anmeldung, git)."); return; }
        var log = new StringBuilder();
        void L(string s) => log.AppendLine($"[{DateTime.Now:HH:mm:ss}] {Clean(s)}");
        var (w, l, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            L($"T-0762 PC-Lauf X-06 · {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz} · Windows {Environment.OSVersion.Version} · ohne GitHub-Anmeldung (MainWindow.GitHubToken = {(MainWindow.GitHubToken is null ? "keiner" : "gesetzt")})");
            L($"Trendo-Datenordner (isoliert): {root}; Werkzeugordner: {w.ToolsRootForTests}");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            // 1. Suche (öffentliche Such-API) – über die Oberfläche
            var hits = await MainWindow.OssSearchHttp(http, "ripgrep");
            var rg = hits.First(h => h.FullName.Equals("BurntSushi/ripgrep", StringComparison.OrdinalIgnoreCase));
            L($"Suche „ripgrep“: {hits.Count} Treffer, gewählt {rg.FullName} · Lizenz {rg.LicenseId} ({OpenSource.Classify(rg.LicenseId)}) · {rg.Stars} Sterne");
            w.OssSearchOverride = _ => Task.FromResult<IReadOnlyList<OssRepo>>([rg]);
            await w.OssSearch("ripgrep"); Pump();
            Assert.NotNull(w.TryFind<Button>("oss-install-0"));
            // 2. Holen mit Prüfsumme (echter Weg der App: neueste Veröffentlichung, Auswahl, Download, SHA-256 gegen GitHub-Angabe, Entpacken)
            var exe = await w.GitHubInstall(rg, start: false); Pump();
            Assert.NotNull(exe);
            var entry = Assert.Single(GitHubApps.List(w.ToolsRootForTests));
            var relJson = await (await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, GitHubApps.LatestReleaseUrl(rg.FullName)) { Headers = { { "User-Agent", "TrendoOS" } } })).Content.ReadAsStringAsync();
            var rel = GitHubApps.ParseRelease(relJson);
            var asset = rel.Assets.Single(a => a.Name == entry.Asset);
            L($"Eingerichtet: {entry.Repo} {entry.Version} · Datei {entry.Asset} ({asset.Size} Bytes) · SHA-256 geprüft {entry.Sha256} · GitHub-Angabe {asset.Sha256} · gleich: {entry.Sha256 == asset.Sha256}");
            Assert.Equal(asset.Sha256, entry.Sha256);
            Assert.StartsWith(Path.GetFullPath(w.ToolsRootForTests), Path.GetFullPath(exe!), StringComparison.OrdinalIgnoreCase);
            L($"Programm: {exe} ({new FileInfo(exe!).Length} Bytes) · Signatur: {Authenticode.Check(exe!).State}");
            // 3. Starten über den echten Weg der App (N4): „Starten“ → Tor GitHubLaunch → Karte „Erster Start“ (Ampel) → Häkchen → eigener Klick.
            //    Das Testfenster hat einen Test-Starter: er nimmt den Pfad entgegen, den Trendo starten würde; diese Datei startet der Lauf als eigenen Prozess.
            w.Show("components"); Pump();
            w.Find<Button>("btn-gh-start-" + entry.Id).Press(); Pump(); await w.GitHubStartCheckTask; Pump();
            var held = w.GitHubPendingStart;
            Assert.NotNull(held); Assert.Empty(l.Opened); Assert.Equal("oss", w.CurrentView);
            L($"„Starten“ unter Programme: nicht gestartet, Karte „Erster Start“ – Ampel {held!.Check.Ampel} ({held.Check.Title}), Ordner-Prüfsumme {held.Check.Sha256}");
            w.Find<CheckBox>("oss-firststart-risk").IsChecked = true; Pump();
            Assert.Empty(l.Opened);
            w.Find<Button>("oss-firststart-run").Press(); Pump();
            var launched = Assert.Single(l.Opened);
            Assert.Equal(Path.GetFullPath(exe!), Path.GetFullPath(launched));
            Assert.NotNull(Assert.Single(GitHubApps.List(w.ToolsRootForTests)).StartApproved);
            L($"Häkchen gesetzt, „Trotzdem starten“ geklickt: Trendo übergibt {launched} an den Starter; Erlaubnis zur Ordner-Prüfsumme gespeichert");
            var psi = new System.Diagnostics.ProcessStartInfo(launched) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, CreateNoWindow = true };
            psi.ArgumentList.Add("--version");
            using (var v = System.Diagnostics.Process.Start(psi)!) { var ver = (await v.StandardOutput.ReadToEndAsync()).Split('\n')[0].Trim(); await v.WaitForExitAsync(); L($"Start 1 (--version): PID {v.Id}, Exit {v.ExitCode}, Ausgabe „{ver}“"); Assert.Equal(0, v.ExitCode); Assert.StartsWith("ripgrep", ver); }
            psi.ArgumentList.Clear(); psi.ArgumentList.Add("suchwort"); // liest von der Standardeingabe und wartet
            using (var p = System.Diagnostics.Process.Start(psi)!)
            {
                var pid = p.Id; await Task.Delay(2000);
                L($"Start 2 (wartet auf Eingabe): PID {pid}, läuft: {!p.HasExited}");
                Assert.False(p.HasExited);
                System.Diagnostics.Process.GetProcessById(pid).Kill(entireProcessTree: true); await p.WaitForExitAsync();
                L($"PID {pid} beendet: {p.HasExited} (Exit {p.ExitCode})");
                Assert.True(p.HasExited);
            }
            // 4. Falsche Prüfsumme wird abgewiesen – echte Datei, falsche Angabe, über den Weg der App
            var dl = Path.Combine(root, "dl"); Directory.CreateDirectory(dl);
            var file = Path.Combine(dl, asset.Name);
            await File.WriteAllBytesAsync(file, await http.GetByteArrayAsync(asset.Url));
            var wrong = asset with { Sha256 = new string('0', 64) };
            w.GitHubFetchOverride = _ => Task.FromResult((rel with { Assets = [wrong] }, (string?)file));
            var before = Directory.GetDirectories(w.ToolsRootForTests).Select(Path.GetFileName).ToList();
            Assert.Null(await w.GitHubInstall(rg with { FullName = "BurntSushi/ripgrep" }, start: false)); Pump();
            w.GitHubFetchOverride = null;
            Assert.Equal(before, Directory.GetDirectories(w.ToolsRootForTests).Select(Path.GetFileName).ToList());
            Assert.Equal(entry.Sha256, Assert.Single(GitHubApps.List(w.ToolsRootForTests)).Sha256); // alter Stand unverändert
            L($"Falsche Prüfsumme (0000…): abgewiesen – Werkzeugordner unverändert ({string.Join(", ", before)}), Eintrag unverändert");
            File.Delete(file);
            if (w.SetupFinishShown) { w.Find<Button>("setup-overlay-done").Press(); Pump(); }
            // 5. Entfernen über die Oberfläche (mit Rückfrage)
            w.Show("components"); Pump();
            w.Find<Button>("btn-gh-remove-" + entry.Id).Press(); Pump();
            w.Find<Button>("btn-gh-remove-" + entry.Id).Press(); Pump();
            Assert.Empty(GitHubApps.List(w.ToolsRootForTests));
            Assert.False(Directory.Exists(Path.Combine(w.ToolsRootForTests, entry.Id)));
            var rest = Directory.GetFileSystemEntries(w.ToolsRootForTests).Select(Path.GetFileName).Where(n => n!.StartsWith("gh-", StringComparison.Ordinal)).ToList();
            Assert.Empty(rest);
            L($"Entfernt über „Entfernen“ → „Wirklich entfernen?“: Ordner {entry.Id} weg: {!Directory.Exists(Path.Combine(w.ToolsRootForTests, entry.Id))}, Liste leer, kein gh-* übrig. Werkzeugordner enthält: [{string.Join(", ", Directory.GetFileSystemEntries(w.ToolsRootForTests).Select(Path.GetFileName))}]");
            // 6. Quellcode holen → Code-Editor → bauen → entfernen
            var src = (await MainWindow.OssSearchHttp(http, "itsdangerous")).First(h => h.FullName.Equals("pallets/itsdangerous", StringComparison.OrdinalIgnoreCase));
            L($"Suche „itsdangerous“: gewählt {src.FullName} · Lizenz {src.LicenseId} ({OpenSource.Classify(src.LicenseId)})");
            w.OssSearchOverride = _ => Task.FromResult<IReadOnlyList<OssRepo>>([src]);
            await w.OssSearch("itsdangerous"); Pump();
            w.Find<Button>("oss-get-0").Press(); Pump();
            var proj = Directory.GetDirectories(w.OssFolder).Single();
            var head = Cli.Run("git", ["-C", proj, "log", "-1", "--format=%H %cs"], null, 30000).Out.Trim();
            L($"Quellcode geholt (git clone --depth 1): {Path.GetFileName(proj)} · Stand {head} · Herkunftsdatei: {File.Exists(OpenSource.OriginFile(proj))}");
            w.Find<Button>("oss-local-code-0").Press(); Pump();
            Assert.Equal("code", w.CurrentView); Assert.Equal(Path.GetFullPath(proj), w.CodeRoot);
            L("Im Code-Editor geöffnet. Erkannte Bauanleitung: " + string.Join(" · ", w.CodeBuildHints.Select(h => $"{h.Command} [{(h.Available ? "Werkzeug da" : h.Tool + " fehlt")}]")));
            var idx = w.CodeBuildHints.ToList().FindIndex(h => h.Available);
            Assert.True(idx >= 0, "Kein Bauwerkzeug vorhanden.");
            w.CodeRealToolsInTests = true;
            w.Find<Button>($"ce-hint-{idx}").Press(); Pump();
            Assert.False(w.CodeRunning); // B1: erster Lauf je Projekt nur nach eigener Bestätigung
            L($"Bauanleitung gewählt – Trendo fragt zuerst: „{OpenSource.BuildWarning}“ → „Ja, im Projekt ausführen“");
            w.Find<Button>("ce-hint-confirm").Press();
            await Until(() => !w.CodeRunning, 600_000);
            L("Terminal des Code-Editors:\n" + w.CodeTerminalText.TrimEnd());
            Assert.Contains("(Exit 0)", w.CodeTerminalText);
            w.Show("oss"); Pump();
            w.Find<Button>("oss-local-del-0").Press(); Pump(); w.Find<Button>("oss-local-del-0").Press(); Pump();
            Assert.Empty(Directory.GetFileSystemEntries(w.OssFolder));
            L($"Geholtes Projekt entfernt: Ordner „Open Source“ ist leer ({Directory.GetFileSystemEntries(w.OssFolder).Length} Einträge, auch keine Herkunftsdatei).");
            L("Ergebnis: BESTANDEN");
        }
        catch (Exception ex) { L("FEHLER: " + ex); throw; }
        finally
        {
            w.Close();
            var dir = Path.Combine(WebDesignKi_Ui.Repo(), "reports", "T-0762"); Directory.CreateDirectory(dir);
            var raw = Path.Combine(dir, "x06-echtlauf.log"); File.WriteAllText(raw, log.ToString());
            var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(raw)));
            File.WriteAllText(Path.Combine(dir, "x06-echtlauf.sha256"), $"{sha}  x06-echtlauf.log\n");
        }
    }
}
