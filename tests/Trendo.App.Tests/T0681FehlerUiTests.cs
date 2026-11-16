using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0681 (B1b): Sicherung ehrlich angezeigt, Fehlerstand der Abläufe, Podcast-Merker, Rückfrage im App-Center, Code-Editor im Hintergrund.</summary>
public class T0681_Ui
{
    static async Task Until(Func<bool> cond, int seconds = 60)
    {
        var sw = Stopwatch.StartNew();
        while (!cond()) { Dispatcher.UIThread.RunJobs(); if (sw.Elapsed > TimeSpan.FromSeconds(seconds)) break; await Task.Delay(40); }
        Dispatcher.UIThread.RunJobs();
    }

    static bool Lebt(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    [AvaloniaFact(DisplayName = "T-0681 Sicherung UI: gesperrte Datei → „Unvollständig“ in Meldung und Liste; beschädigte Sicherung steht als solche da und wird beim Wiederherstellen übersprungen")]
    public void Sicherung_zeigt_unvollstaendig_und_beschaedigt()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            File.WriteAllText(Path.Combine(root, "notiz.txt"), "v1");
            var belegt = Path.Combine(root, "belegt.dat"); File.WriteAllText(belegt, "x");
            w.Show("sicherung");
            using (new FileStream(belegt, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) w.Find<Button>("btn-backup-now").Press();
            var msg = w.Find<TextBlock>("backup-msg").Text!;
            Assert.StartsWith("Unvollständig gesichert", msg); Assert.Contains("belegt.dat", msg);
            Assert.Contains("unvollständig: 1 Datei(en) nicht gesichert", w.Find<TextBlock>("backup-row-0").Text);
            // eine beschädigte Sicherung (halbe Datei) dazulegen: neuester Name, also oben in der Liste
            var halb = Path.Combine(w.BackupStore.Folder, "trendo-sicherung-29991231-235959.zip"); File.WriteAllBytes(halb, [0x50, 0x4B, 0x03, 0x04, 9, 9, 9]);
            File.WriteAllText(Path.Combine(root, "notiz.txt"), "v2");
            w.Show("sicherung");
            Assert.Contains("beschädigt", w.Find<TextBlock>("backup-row-0").Text);
            var vorher = w.BackupStore.List().Count;
            w.Find<Button>("backup-restore-0").Press();
            Assert.Contains("nicht lesbar", w.Find<TextBlock>("backup-restore-plan-0").Text);
            w.Find<Button>("backup-restore-yes-0").Press();
            var nach = w.Find<TextBlock>("backup-msg").Text!;
            Assert.Contains("übersprungen", nach); Assert.Contains("nichts geändert", nach);
            Assert.Equal("v2", File.ReadAllText(Path.Combine(root, "notiz.txt")));
            Assert.Equal(vorher, w.BackupStore.List().Count);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0681 Abläufe UI: fehlgeschlagener Lauf steht mit Fehlerstand am Ablauf; beschädigte Ablaufdatei wird auf der Seite gemeldet")]
    public async Task Ablaeufe_zeigen_Fehlerstand()
    {
        var (w, _, root) = Ui.Open();
        var home = Path.Combine(root, "home"); Directory.CreateDirectory(home);
        MainWindow.WorkflowHome = home;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "ablaeufe")); File.WriteAllText(Path.Combine(root, "ablaeufe", "ablaeufe.json"), "[{\"Name\":\"halb");
            w.Show("ablaeufe");
            Assert.Contains("beschädigt", w.Find<TextBlock>("wf-load-problem").Text);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "ablaeufe"), "ablaeufe.json.beschaedigt-*"));
            File.WriteAllText(Path.Combine(home, "ziel"), "Datei statt Ordner"); Directory.CreateDirectory(Path.Combine(home, "quelle")); File.WriteAllText(Path.Combine(home, "quelle", "a.txt"), "x");
            var wf = w.Workflows.Add(new Workflow { Name = "Kopie", Trigger = TriggerKind.Intervall, EveryMinutes = 60, Steps = [new() { Kind = StepKind.Kopieren, From = Path.Combine(home, "quelle"), To = Path.Combine(home, "ziel"), Pattern = "*.txt" }] });
            await w.WfTickAsync(); // der Zeitplan versucht es – und scheitert
            w.Show("ablaeufe");
            var fail = w.Find<TextBlock>("wf-fail-0").Text!;
            Assert.Contains("Letzter Lauf fehlgeschlagen (1 Versuch", fail); Assert.Contains("nächsten Takt", fail);
            Assert.Null(wf.LastRun);
            File.Delete(Path.Combine(home, "ziel"));
            await w.WfTickAsync(); // nächster Takt: erneut versucht, jetzt klappt es
            w.Show("ablaeufe");
            Assert.True(File.Exists(Path.Combine(home, "ziel", "a.txt")));
            Assert.Null(w.TryFind<TextBlock>("wf-fail-0")); Assert.NotNull(wf.LastRun);
        }
        finally { MainWindow.WorkflowHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0681 Podcast: fehlt FFmpeg, nennt die Meldung den Einrichten-Weg – und „Lautheit angleichen“ geht danach weiter (Merker nicht hängen geblieben)")]
    public async Task Podcast_Merker_bleibt_nicht_haengen()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            const string K = "podcast-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-interview")); Settle();
            var mp3 = Path.Combine(root, "folge.mp3"); File.WriteAllBytes(mp3, new byte[2048]);
            w.ProgramActive(K)!.Set("audio", mp3, "Aufnahme");
            w.PodcFfmpegPath = ""; // FFmpeg fehlt
            for (var i = 0; i < 2; i++) // früher: beim zweiten Klick „wird schon angeglichen“ – für immer
            {
                w.Show(K); Settle(); w.Find<Button>("prog-cmd-podc-loud").Press(); Settle();
                Assert.Contains("FFmpeg fehlt", w.ProgramMessage(K)); Assert.Contains("Zusatzprogramme", w.ProgramMessage(K)); Assert.Contains("Einrichten", w.ProgramMessage(K));
            }
            // FFmpeg „eingerichtet“ (hier: ein Programm, das sich nicht starten lässt) – der Knopf arbeitet, statt „schon angeglichen“ zu melden
            var kaputt = Path.Combine(root, "ffmpeg-kaputt.exe"); File.WriteAllText(kaputt, "kein Programm");
            w.PodcFfmpegPath = kaputt;
            for (var i = 0; i < 2; i++)
            {
                w.Show(K); Settle(); w.Find<Button>("prog-cmd-podc-loud").Press();
                await Until(() => !w.ProgramMessage(K).Contains("wird angeglichen"), 20);
                Assert.DoesNotContain("schon angeglichen", w.ProgramMessage(K)); Assert.DoesNotContain("FFmpeg fehlt", w.ProgramMessage(K));
                Assert.NotEqual("", w.ProgramMessage(K));
            }
        }
        finally { w.Close(); }
    }

    static Button DlgButton(Window dlg, string name) => dlg.GetLogicalDescendants().OfType<Button>().First(b => b.Name == name);

    [AvaloniaFact(DisplayName = "T-0681 App-Center: Installieren fragt in einem eigenen Fenster nach (was, woher, Größe) – ohne Bestätigung passiert nichts, Ablehnen steht im Protokoll, erst „Installieren“ installiert")]
    public async Task AppCenter_fragt_vor_dem_Installieren()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var pkg = new FakePkg(); w.AppBackendOverride = pkg;
            w.Show("appcenter"); await w.AppSearchAsync("vlc"); w.Show("appcenter");
            w.Find<Button>("appc-hit-install-0").Press(); W2Ui.Settle();
            // Rückfrage ist offen – es ist noch nichts passiert
            var dlg = Assert.Single(w.OwnedWindows);
            Assert.Empty(pkg.Done);
            var text = dlg.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Name == "appc-confirm-text").Text!;
            Assert.Contains("VLC", text); Assert.Contains("VideoLAN.VLC", text); Assert.Contains("Woher: Test-Paketverwaltung", text); Assert.Contains("Größe:", text);
            var no = DlgButton(dlg, "appc-confirm-no"); Assert.True(no.IsDefault); Assert.True(no.IsCancel);
            no.Press(); W2Ui.Settle();
            Assert.Empty(pkg.Done); Assert.Empty(w.OwnedWindows);
            Assert.Contains("nichts geändert", w.Find<TextBlock>("appc-msg").Text);
            var log = w.Core.Audit.ReadAll();
            Assert.Contains(log, a => a.Action == "approval.denied"); Assert.Contains(log, a => a.Action == "app.abgelehnt" && a.Detail.Contains("install VideoLAN.VLC"));
            Assert.DoesNotContain(log, a => a.Action is "approval.approved" or "app.done");
            Assert.Empty(w.Core.Approvals.Pending());
            // Fenster schließen (X) lehnt ebenfalls ab
            w.Find<Button>("appc-hit-install-0").Press(); W2Ui.Settle();
            Assert.Single(w.OwnedWindows).Close(); W2Ui.Settle();
            Assert.Empty(pkg.Done);
            // erst das ausdrückliche Ja installiert
            w.Find<Button>("appc-hit-install-0").Press(); W2Ui.Settle();
            DlgButton(Assert.Single(w.OwnedWindows), "appc-confirm-yes").Press(); W2Ui.Settle();
            Assert.Equal(["i VideoLAN.VLC"], pkg.Done);
            Assert.Contains("VLC: installiert", w.Find<TextBlock>("appc-msg").Text);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "app.done");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0681 App-Center: auch Entfernen und Aktualisieren fragen vorher; die Rückfrage nennt Programm und Vorgang; Nein ändert nichts")]
    public async Task AppCenter_fragt_vor_dem_Entfernen()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var pkg = new FakePkg(); w.AppBackendOverride = pkg;
            var gefragt = new List<MainWindow.AppChange>(); var antwort = false;
            w.AppConfirm = c => { gefragt.Add(c); return Task.FromResult(antwort); };
            w.Show("appcenter"); await w.AppLoadAsync(); w.Show("appcenter");
            w.Find<Button>("appc-inst-uninstall-0").Press(); W2Ui.Settle();
            w.Find<Button>("appc-upd-upgrade-0").Press(); W2Ui.Settle();
            Assert.Empty(pkg.Done); Assert.Equal(2, gefragt.Count);
            Assert.Equal("uninstall", gefragt[0].Action); Assert.Contains("entfernen?", gefragt[0].Title); Assert.Contains("Git.Git", gefragt[0].Text); Assert.Equal("Entfernen", gefragt[0].YesLabel);
            Assert.Equal("upgrade", gefragt[1].Action); Assert.Contains("2.45 → 2.47", gefragt[1].Text);
            Assert.NotNull(w.TryFind<Button>("appc-inst-uninstall-0")); // steht weiter in der Liste
            Assert.Equal(2, w.Core.Audit.ReadAll().Count(a => a.Action == "app.abgelehnt"));
            antwort = true;
            w.Find<Button>("appc-inst-uninstall-0").Press(); W2Ui.Settle();
            Assert.Equal(["x Git.Git"], pkg.Done);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0681 Code-Editor: „Ausführen“ läuft im Hintergrund – Klick kehrt sofort zurück, Ausgabe erscheint laufend, die Seite bleibt bedienbar, „Abbrechen“ beendet den eigenen Kindprozess")]
    public async Task CodeEditor_laeuft_im_Hintergrund_und_bricht_ab()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var proj = Path.Combine(root, "projekt"); Directory.CreateDirectory(proj); File.WriteAllText(Path.Combine(proj, "main.py"), "print('Hallo')\n");
        try
        {
            w.CodeOpenFolder(proj);
            w.CodeRunOverride = OperatingSystem.IsWindows()
                ? ("powershell", ["-NoProfile", "-Command", "Write-Output 'Zeile eins'; Start-Sleep -Seconds 300"], proj)
                : ("sh", ["-c", "echo 'Zeile eins'; sleep 300"], proj);
            w.Show("code"); Settle();
            Assert.Null(w.TryFind<Button>("ce-stop"));
            var sw = Stopwatch.StartNew();
            RealClick(w, V<Button>(w, "ce-run")); Settle();
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"Der Klick hat die Oberfläche {sw.Elapsed.TotalSeconds:0.0} s blockiert."); // früher: bis zu 120 s
            Assert.True(w.CodeRunning); Assert.False(w.CodeRunTask!.IsCompleted);
            Assert.Contains("läuft", V<SelectableTextBlock>(w, "ce-output").Text);
            // Ausgabe kommt, während das Programm noch läuft
            await Until(() => w.TryFind<SelectableTextBlock>("ce-output")?.Text?.Contains("Zeile eins") == true);
            Assert.Contains("Zeile eins", V<SelectableTextBlock>(w, "ce-output").Text);
            Assert.True(w.CodeRunning); var pid = w.CodeRunPid; Assert.True(pid > 0); Assert.True(Lebt(pid));
            // Oberfläche bleibt bedienbar: andere Knöpfe reagieren, ein zweiter Start ist gesperrt
            RealClick(w, V<Button>(w, "ce-panel-suche")); Settle(); Assert.NotNull(w.TryFind<TextBox>("ce-search"));
            RealClick(w, V<Button>(w, "ce-panel-ausgabe")); Settle();
            Assert.Contains("Zeile eins", V<SelectableTextBlock>(w, "ce-output").Text);
            Assert.False(V<Button>(w, "ce-run").IsEnabled); Assert.False(V<Button>(w, "ce-test").IsEnabled);
            // Abbrechen beendet den Kindprozess
            RealClick(w, V<Button>(w, "ce-stop")); Settle();
            await Until(() => !w.CodeRunning, 30);
            Assert.False(w.CodeRunning);
            await w.CodeRunTask!.WaitAsync(TimeSpan.FromSeconds(10));
            for (var i = 0; i < 100 && Lebt(pid); i++) await Task.Delay(50);
            Assert.False(Lebt(pid), "Der gestartete Prozess läuft nach „Abbrechen“ weiter.");
            Settle();
            var aus = V<SelectableTextBlock>(w, "ce-output").Text!;
            Assert.StartsWith("■ Abgebrochen", aus); Assert.Contains("Zeile eins", aus);
            Assert.Null(w.TryFind<Button>("ce-stop")); Assert.True(V<Button>(w, "ce-run").IsEnabled);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "code.run" && a.Detail.Contains("abgebrochen"));
            // ein kurzer Lauf endet von allein mit seiner Ausgabe
            w.CodeRunOverride = OperatingSystem.IsWindows() ? ("cmd", ["/c", "echo fertig-681"], proj) : ("sh", ["-c", "echo fertig-681"], proj);
            RealClick(w, V<Button>(w, "ce-run")); Settle();
            await Until(() => !w.CodeRunning, 30);
            Settle();
            var kurz = V<SelectableTextBlock>(w, "ce-output").Text!;
            Assert.StartsWith("✓ Fertig (Exit 0)", kurz); Assert.Contains("fertig-681", kurz);
        }
        finally { w.CodeStop(); w.Close(); }
    }
}
