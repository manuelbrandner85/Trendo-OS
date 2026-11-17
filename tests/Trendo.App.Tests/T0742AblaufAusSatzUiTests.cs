using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0742 (SYS-05): Über die Oberfläche wird aus einem Satz ein echter Ablauf in der vorhandenen Engine – sichtbar im Ablauf-Editor, änderbar,
/// rückgängig machbar, aus bis zur Bestätigung; die Windows-Aufgabe gibt es nur nach Zustimmung und mit einem Klick wieder weg.
/// Hier sind KI und schtasks ersetzt (Ersatz-KI, aufgezeichnete schtasks-Aufrufe). Echt: <see cref="T0742_Echtlauf_Ui"/>.</summary>
public class T0742_Ui
{
    sealed class FixedKi(Func<string, string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9";
        public readonly List<string> Systems = [];
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Systems.Add(messages[0].Content); return Task.FromResult(reply(messages[^1].Content)); }
    }

    static async Task Until(Func<bool> ok, int ms = 10000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }

    const string Json = """
        {"name": "Tagesnotiz", "ausloeser": {"art": "taeglich", "uhrzeit": "07:00", "wochentage": ["mo","di","mi","do","fr"]},
         "schritte": [{"art": "ordner_anlegen", "nach": "~/Notizen"}, {"art": "text_schreiben", "nach": "~/Notizen/notiz-{datum}.txt", "text": "Notizen für {datum}"}]}
        """;

    [AvaloniaFact(DisplayName = "T-0742 UI: Satz → KI → echter Ablauf im Editor (aus), ändern, Rückgängig/Wiederholen, Einschalten nur nach Ja, Lauf schreibt Datei")]
    public async Task Satz_wird_Ablauf_im_Editor()
    {
        var (w, _, root) = Ui.Open();
        var home = Path.Combine(root, "heim"); Directory.CreateDirectory(home); MainWindow.WorkflowHome = home;
        var ki = new FixedKi(_ => "Hier ist der Ablauf:\n```json\n" + Json + "\n```");
        w.KiBackendsOverride = () => [ki];
        w.WfTasks.Run = a => (1, ""); // schtasks: keine Aufgabe vorhanden
        try
        {
            w.Show("ablaeufe"); Settle();
            Assert.Equal(Avalonia.Media.Brushes.White, w.Find<Button>("wf-ki-anlegen").Foreground); // Leerzustand: der Satz ist die goldene Hauptaktion
            w.Find<TextBox>("wf-ki-prompt").Text = "Jeden Werktag um 7 Uhr eine Tagesnotiz im Ordner Notizen anlegen";
            RealClick(w, w.Find<Button>("wf-ki-anlegen"));
            await Until(() => !w.WfKiBusy && w.Workflows.All().Count == 1);
            Assert.Contains("Ablauf-Engine", ki.Systems.Single());
            var wf = w.Workflows.All().Single();
            Assert.False(wf.Enabled);
            Assert.Equal("Jeden Werktag um 7 Uhr eine Tagesnotiz im Ordner Notizen anlegen", wf.Origin);
            Assert.Equal(5, wf.Days.Count);
            w.Show("ablaeufe"); Settle();
            // im Ablauf-Editor: Auslöser mit Bedingung und beide Schritte als Bausteine
            Assert.Contains("nur Mo, Di, Mi, Do, Fr um 07:00", w.GetLogicalTexts());
            Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("1. Ordner anlegen"));
            Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("2. Textdatei schreiben"));
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Entwurf der KI – aus"));
            Assert.Contains("nur, solange Trendo offen", w.Find<TextBlock>("wf-zeitplan-text").Text);
            // ändern im Editor: Bedingung auf Freitag
            w.Find<TextBox>("wf-edit-days").Text = "Fr"; w.Find<Button>("wf-edit-save").Press(); Settle();
            Assert.Equal([DayOfWeek.Friday], w.Workflows.Get(wf.Id)!.Days);
            // Rückgängig: erst die Änderung, dann das Anlegen selbst
            w.Find<Button>("wf-undo").Press(); Settle();
            Assert.Equal(5, w.Workflows.Get(wf.Id)!.Days.Count);
            w.Find<Button>("wf-undo").Press(); Settle();
            Assert.Empty(w.Workflows.All());
            w.Find<Button>("wf-redo").Press(); Settle(); // Wiederholen legt ihn wieder an – weiterhin aus
            var again = Assert.Single(w.Workflows.All());
            Assert.Equal(wf.Id, again.Id); Assert.False(again.Enabled); Assert.Equal(2, again.Steps.Count); Assert.NotNull(again.Origin);
            // Einschalten fragt nach: „Nein“ → bleibt aus
            w.FreigabeConfirm = _ => Task.FromResult(false);
            w.Find<Button>("wf-toggle-0").Press(); await Until(() => w.LastWirkung?.Action == "workflow.enable");
            Assert.False(again.Enabled);
            Assert.Contains("Jeden Werktag", w.LastWirkung!.Text);
            w.FreigabeConfirm = _ => Task.FromResult(true);
            w.Show("ablaeufe"); Settle();
            w.Find<Button>("wf-toggle-0").Press(); await Until(() => again.Enabled);
            // läuft wirklich: Ausführen legt Ordner und Datei an
            w.Show("ablaeufe"); Settle();
            w.Find<Button>("wf-run-0").Press(); await Until(() => File.Exists(Path.Combine(home, "Notizen", $"notiz-{DateTime.Now:yyyy-MM-dd}.txt")));
            Assert.StartsWith("Notizen für ", File.ReadAllText(Path.Combine(home, "Notizen", $"notiz-{DateTime.Now:yyyy-MM-dd}.txt")));
        }
        finally { w.KiBackendsOverride = null; MainWindow.WorkflowHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0742 UI: Unbrauchbare KI-Antwort legt nichts an und sagt warum")]
    public async Task Unsichere_Antwort_legt_nichts_an()
    {
        var (w, _, root) = Ui.Open();
        var home = Path.Combine(root, "heim"); Directory.CreateDirectory(home); MainWindow.WorkflowHome = home;
        w.KiBackendsOverride = () => [new FixedKi(_ => """{"name":"x","ausloeser":{"art":"manuell"},"schritte":[{"art":"ordner_anlegen","nach":"C:/Windows/x"}]}""")];
        w.WfTasks.Run = a => (1, "");
        try
        {
            w.Show("ablaeufe"); Settle();
            await w.WfFromSentenceAsync("Leg einen Ordner in Windows an");
            Assert.Empty(w.Workflows.All());
            Assert.Contains("es wurde nichts angelegt", w.Find<TextBlock>("wf-msg").Text);
            Assert.Contains("außerhalb", w.Find<TextBlock>("wf-msg").Text);
        }
        finally { w.KiBackendsOverride = null; MainWindow.WorkflowHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0742 UI: Windows-Aufgabe nur nach Ja, sichtbar, ein Klick entfernt sie; Löschen des Ablaufs entfernt sie mit; „neue Datei“ zeigt deutlich „nur bei offenem Trendo“")]
    public async Task Windows_Aufgabe_nur_mit_Zustimmung()
    {
        if (!WorkflowTaskPlan.Supported) return; // Linux: dort gibt es keine Aufgabenplanung (Hinweis prüft der Kern-Test)
        var (w, _, root) = Ui.Open();
        var home = Path.Combine(root, "heim"); Directory.CreateDirectory(home); MainWindow.WorkflowHome = home;
        var exe = Path.Combine(root, "TrendoOS.exe"); File.WriteAllText(exe, ""); MainWindow.WfTaskExe = exe;
        var calls = new List<string>(); var exists = false;
        w.WfTasks.Run = a => { calls.Add(a[0]); if (a[0] == "/Create") exists = true; if (a[0] == "/Delete") exists = false; return (a[0] == "/Query" && !exists ? 1 : 0, ""); };
        try
        {
            w.Show("ablaeufe"); w.Find<Button>("wf-tpl-notiz").Press(); Settle(); // täglich 08:00
            Assert.Equal("⚠ Läuft im Moment nur, solange Trendo offen ist.", w.Find<TextBlock>("wf-zeitplan-text").Text);
            w.FreigabeConfirm = _ => Task.FromResult(false);
            w.Find<Button>("wf-task-create").Press(); await Until(() => w.LastWirkung?.Action == "workflow.windows-aufgabe");
            Assert.DoesNotContain("/Create", calls); // ohne Ja keine Aufgabe
            Assert.Contains("ohne Administratorrechte", w.LastWirkung!.Text);
            Assert.DoesNotContain(Environment.UserName, w.LastWirkung!.Preview!);
            w.FreigabeConfirm = _ => Task.FromResult(true);
            w.Show("ablaeufe"); Settle();
            w.Find<Button>("wf-task-create").Press(); await Until(() => calls.Contains("/Create"));
            w.Show("ablaeufe"); Settle();
            var wf = w.Workflows.All().Single();
            Assert.StartsWith("✓ Läuft auch, wenn Trendo geschlossen ist", w.Find<TextBlock>("wf-zeitplan-text").Text);
            Assert.Contains(WorkflowTaskPlan.TaskName(w.Core.DataRoot, wf.Id), w.Find<TextBlock>("wf-zeitplan-text").Text);
            // Zeitplan ändern → als veraltet sichtbar
            w.WfChange(wf, x => x.At = "09:15", "Start geändert."); Settle();
            Assert.Contains("Zeitplan hat sich geändert", w.Find<TextBlock>("wf-zeitplan-text").Text);
            // ein Klick entfernt sie
            w.Find<Button>("wf-task-remove").Press(); Settle();
            Assert.Contains("/Delete", calls); Assert.False(exists); Assert.Null(wf.WinTask);
            // Löschen des Ablaufs nimmt die Aufgabe mit
            w.Show("ablaeufe"); Settle(); w.Find<Button>("wf-task-create").Press(); await Until(() => exists);
            w.Show("ablaeufe"); Settle(); calls.Clear();
            w.Find<Button>("wf-del-0").Press(); Settle();
            Assert.Contains("/Delete", calls); Assert.False(exists); Assert.Empty(w.Workflows.All());
            // „neue Datei“: deutlich nur bei offenem Trendo, kein Knopf zum Anlegen
            w.Find<Button>("wf-tpl-pdf").Press(); Settle();
            Assert.StartsWith("⏸ Läuft nur, solange Trendo offen ist.", w.Find<TextBlock>("wf-zeitplan-text").Text);
            Assert.Null(w.TryFind<Button>("wf-task-create"));
        }
        finally { MainWindow.WfTaskExe = null; MainWindow.WorkflowHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); w.Close(); }
    }
}

/// <summary>T-0742 Gegenprüfung: Start durch die Windows-Aufgabe und Aufräumen (schtasks ersetzt; es wird keine echte Windows-Aufgabe angelegt).</summary>
public class T0742_Gegenpruefung_Ui
{
    [AvaloniaFact(DisplayName = "T-0742 Gegenprüfung: „läuft gerade schon“ ist für die Windows-Aufgabe kein Fehler – Rückgabe 0, neutral protokolliert")]
    public void Laeuft_schon_ist_kein_Fehler()
    {
        var (w, _, root) = Ui.Open();
        var home = Path.Combine(root, "heim"); Directory.CreateDirectory(home); MainWindow.WorkflowHome = home;
        try
        {
            var wf = w.Workflows.Add(new Workflow { Name = "Notiz", Trigger = TriggerKind.Intervall, EveryMinutes = 60,
                Steps = [new() { Kind = StepKind.TextSchreiben, To = Path.Combine(home, "n-{zeit}.txt"), Text = "x" }] });
            var lease = Path.Combine(w.Core.DataRoot, "ablaeufe", "laeuft-" + wf.Id + ".lock"); File.WriteAllText(lease, ""); // ein anderer Prozess führt ihn gerade aus
            var o = new StringWriter();
            Assert.Equal(0, MainWindow.RunWorkflowHeadless(w.Core, wf.Id, o));
            Assert.Contains("läuft gerade schon", o.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Fehler", o.ToString());
            Assert.Empty(Directory.GetFiles(home, "n-*.txt"));
        }
        finally { MainWindow.WorkflowHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0742 Gegenprüfung: Start durch eine Aufgabe mit fehlendem Datenordner legt ihn nicht an und entfernt die eigene Aufgabe; ungültige Id wird abgelehnt")]
    public void Fehlender_Datenordner_wird_nicht_angelegt()
    {
        if (!WorkflowTaskPlan.Supported) return;
        var root = Path.Combine(Path.GetTempPath(), "trendo-t0742-weg-" + Guid.NewGuid().ToString("N"));
        var calls = new List<string>();
        var plan = new WorkflowTaskPlan { Run = a => { calls.Add(string.Join(" ", a)); return (0, ""); } };
        Assert.Equal(0, MainWindow.RunFromTask("abc123", root, TextWriter.Null, plan));
        Assert.False(Directory.Exists(root));
        Assert.Equal([$"/Delete /TN {WorkflowTaskPlan.TaskName(root, "abc123")} /F"], calls);
        Assert.Equal(2, MainWindow.RunFromTask("abc 123", root, TextWriter.Null, plan));
        Assert.False(Directory.Exists(root));
    }

    [AvaloniaFact(DisplayName = "T-0742 Gegenprüfung: Gibt es den Ablauf nicht mehr, entfernt die Aufgabe sich selbst; im offenen Trendo zeigt ein Ablauf mit KI-Schritt „nur bei offenem Trendo“ und bietet das Entfernen an")]
    public void Ablauf_weg_oder_KI_Schritt()
    {
        if (!WorkflowTaskPlan.Supported) return;
        var (w, _, root) = Ui.Open();
        var home = Path.Combine(root, "heim"); Directory.CreateDirectory(home); MainWindow.WorkflowHome = home;
        var calls = new List<string>(); var exists = true;
        w.WfTasks.Run = a => { calls.Add(a[0]); if (a[0] == "/Delete") exists = false; return (a[0] == "/Query" && !exists ? 1 : 0, ""); };
        try
        {
            var plan = new WorkflowTaskPlan { Run = a => { calls.Add(string.Join(" ", a)); return (0, ""); } };
            Assert.Equal(0, MainWindow.RunWorkflowHeadless(w.Core, "weg12345", TextWriter.Null, plan));
            Assert.Contains($"/Delete /TN {WorkflowTaskPlan.TaskName(w.Core.DataRoot, "weg12345")} /F", calls);
            calls.Clear();
            w.Show("ablaeufe"); w.Find<Button>("wf-tpl-notiz").Press(); Settle();
            var wf = w.Workflows.All().Single(); w.Workflows.NoteWinTask(wf, WorkflowTaskPlan.ScheduleKey(wf));
            w.WfChange(wf, x => x.Steps.Add(new WfStep { Kind = StepKind.KiZusammenfassen, From = home }), "Baustein ergänzt."); Settle();
            w.Show("ablaeufe"); Settle();
            Assert.StartsWith("⏸ Läuft nur, solange Trendo offen ist.", w.Find<TextBlock>("wf-zeitplan-text").Text);
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("führt diesen Ablauf aber nicht mehr aus"));
            w.Find<Button>("wf-task-remove").Press(); Settle();
            Assert.Contains("/Delete", calls); Assert.Null(w.Workflows.Get(wf.Id)!.WinTask);
            Assert.Null(w.TryFind<Button>("wf-task-remove"));
        }
        finally { MainWindow.WorkflowHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0742 Gegenprüfung ECHT (zwei Prozesse, ohne schtasks): Das gebaute TrendoOS.exe mit --ablauf-faellig wartet auf die Sperre und schreibt nur seinen Laufstand – das Pausieren des offenen Trendo bleibt")]
    public void Zwei_Prozesse_Sperre_echt()
    {
        var repo = AppContext.BaseDirectory; while (repo is not null && !File.Exists(Path.Combine(repo, "Trendo.sln"))) repo = Path.GetDirectoryName(repo);
        var exe = repo is null ? "" : Path.Combine(repo, "src", "Trendo.App", "bin", "Release", "net10.0", OperatingSystem.IsWindows() ? "TrendoOS.exe" : "TrendoOS");
        if (!File.Exists(exe)) return; // nicht gebaut: dieser Nachweis braucht das gebaute Programm
        var (w, _, root) = Ui.Open();
        var home = Path.Combine(root, "heim"); Directory.CreateDirectory(home); MainWindow.WorkflowHome = home;
        try
        {
            var wf = w.Workflows.Add(new Workflow { Name = "Zwei Prozesse", Trigger = TriggerKind.Intervall, EveryMinutes = 60,
                Steps = [new() { Kind = StepKind.TextSchreiben, To = Path.Combine(home, "lauf-{zeit}.txt"), Text = "x" }] });
            var file = Path.Combine(w.Core.DataRoot, "ablaeufe", "ablaeufe.json");
            Process p;
            using (var hold = new FileStream(file + ".sperre", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) // das „offene Trendo“ hält die Sperre
            {
                var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var a in new[] { WorkflowTaskPlan.Switch, wf.Id, "--data-root", w.Core.DataRoot }) psi.ArgumentList.Add(a);
                p = Process.Start(psi)!;
                var sw = Stopwatch.StartNew();
                while (Directory.GetFiles(home, "lauf-*.txt").Length == 0 && sw.ElapsedMilliseconds < 60_000 && !p.HasExited) Thread.Sleep(50);
                Assert.Single(Directory.GetFiles(home, "lauf-*.txt")); // der zweite Prozess hat den Schritt ausgeführt …
                Thread.Sleep(700);
                Assert.False(p.HasExited); // … und wartet jetzt auf die Sperre, um seinen Laufstand einzutragen
                var list = System.Text.Json.JsonSerializer.Deserialize<List<Workflow>>(File.ReadAllText(file))!;
                list.Single(x => x.Id == wf.Id).Enabled = false; // währenddessen speichert das offene Trendo: pausiert
                File.WriteAllText(file, System.Text.Json.JsonSerializer.Serialize(list));
            }
            Assert.True(p.WaitForExit(60_000));
            var output = p.StandardOutput.ReadToEnd();
            Assert.True(p.ExitCode == 0, output + p.StandardError.ReadToEnd());
            var disk = System.Text.Json.JsonSerializer.Deserialize<List<Workflow>>(File.ReadAllText(file))!.Single(x => x.Id == wf.Id);
            Assert.False(disk.Enabled); // Pausieren bleibt
            Assert.NotNull(disk.LastRun); Assert.Equal(0, disk.FailCount); // Laufstand der Aufgabe ist eingetragen
            Assert.Contains("ok:", output);
        }
        finally { MainWindow.WorkflowHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0742 Gegenprüfung: Lässt sich die Windows-Aufgabe beim Löschen nicht entfernen, bleibt der Ablauf mit Hinweis bestehen")]
    public void Loeschen_ohne_entfernte_Aufgabe_behaelt_Ablauf()
    {
        if (!WorkflowTaskPlan.Supported) return;
        var (w, _, root) = Ui.Open();
        var home = Path.Combine(root, "heim"); Directory.CreateDirectory(home); MainWindow.WorkflowHome = home;
        w.WfTasks.Run = a => a[0] == "/Delete" ? (1, "Zugriff verweigert") : (0, ""); // Aufgabe da, Entfernen scheitert
        try
        {
            w.Show("ablaeufe"); w.Find<Button>("wf-tpl-notiz").Press(); Settle();
            var wf = w.Workflows.All().Single(); w.Workflows.NoteWinTask(wf, WorkflowTaskPlan.ScheduleKey(wf));
            w.Show("ablaeufe"); Settle();
            w.Find<Button>("wf-del-0").Press(); Settle();
            Assert.NotNull(w.Workflows.Get(wf.Id));
            Assert.Contains("nicht entfernen", w.Find<TextBlock>("wf-msg").Text);
        }
        finally { MainWindow.WorkflowHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); w.Close(); }
    }
}

/// <summary>T-0742 ECHTLAUF (ersetzt ist nur die Fensterdarstellung und die Antwort „Ja“ im Rückfrage-Dialog): Satz über die Oberfläche → echtes Claude Code
/// (eigenes Abo, schon angemeldet) → Ablauf in der Engine → Einschalten → echte Windows-Aufgabe per schtasks.exe (eigenes Konto, ohne Admin) →
/// die Aufgabe startet das gebaute TrendoOS.exe mit „--ablauf-faellig“ (ohne Fenster) → der Ablauf erzeugt die Datei → Aufgabe per Klick entfernt.
/// Nur mit TRENDO_T0742_ECHT=&lt;Belegordner&gt;; sonst übersprungen (kein Netz/Abo in normalen Läufen).</summary>
public class T0742_Echtlauf_Ui
{
    const string Satz = "Schreib mir jeden Tag um 0:01 Uhr im Ordner Berichte eine Textdatei mit dem heutigen Datum im Namen, Inhalt: Tagesbericht – Trendo war hier.";

    static string? Beleg() => Environment.GetEnvironmentVariable("TRENDO_T0742_ECHT") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;
    static void WaitFor(Func<bool> done, int ms) { var sw = Stopwatch.StartNew(); while (!done() && sw.ElapsedMilliseconds < ms) { Thread.Sleep(100); Settle(); } Settle(); }
    static (int Exit, string Out) Schtasks(params string[] args)
    {
        var psi = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = WorkflowTaskPlan.Oem(), StandardErrorEncoding = WorkflowTaskPlan.Oem() };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!; var o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd(); p.WaitForExit(); return (p.ExitCode, o);
    }
    /// <summary>Beleg ohne persönliche Angaben: Testordner, Benutzername, Rechnername und Konto-SID ersetzt, Zeilenenden einheitlich LF.</summary>
    static string Clean(string s, string root) => System.Text.RegularExpressions.Regex.Replace(
        s.Replace("\r\n", "\n").Replace(root, "<testordner>").Replace(root.Replace("\\", "\\\\"), "<testordner>").Replace(Environment.UserName, "<user>").Replace(Environment.MachineName, "<pc>"),
        @"S-1-5-21-[\d-]+", "<sid>");

    [AvaloniaFact(DisplayName = "T-0742 ECHT: Satz → Claude → Ablauf → Windows-Aufgabe startet TrendoOS.exe ohne Fenster → Datei entsteht (nur mit TRENDO_T0742_ECHT)")]
    public void Echtlauf()
    {
        if (Beleg() is not { } dir) return;
        var log = new StringBuilder();
        void L(string s) => log.Append($"{DateTime.Now:HH:mm:ss} {s}\n");
        var claude = Environment.GetEnvironmentVariable("TRENDO_CLAUDE_EXE") is { Length: > 0 } e ? e : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.exe");
        if (!File.Exists(claude)) { File.WriteAllText(Path.Combine(dir, "BLOCKIERT.txt"), "Claude Code nicht gefunden.\n"); return; }
        var repo = AppContext.BaseDirectory; while (repo is not null && !File.Exists(Path.Combine(repo, "Trendo.sln"))) repo = Path.GetDirectoryName(repo.TrimEnd(Path.DirectorySeparatorChar));
        var exe = Path.Combine(repo!, "src", "Trendo.App", "bin", "Release", "net10.0", "TrendoOS.exe");
        if (!File.Exists(exe)) { File.WriteAllText(Path.Combine(dir, "BLOCKIERT.txt"), "TrendoOS.exe nicht gebaut (dotnet build Trendo.sln -c Release).\n"); return; }
        var (w, _, root) = Ui.Open();
        var home = Path.Combine(root, "heim"); Directory.CreateDirectory(home); MainWindow.WorkflowHome = home; MainWindow.WfTaskExe = exe;
        var oldRun = MainWindow.ClaudeRunOverride; var oldStatus = MainWindow.ClaudeStatusOverride;
        Workflow? wf = null;
        try
        {
            // wie AgentEchtlauf_Ui: echtes Claude Code mit der vorhandenen Anmeldung, gleiche Argumente wie in Trendo, ohne API-Variablen
            MainWindow.ClaudeStatusOverride = async () =>
            {
                var psi = new ProcessStartInfo(claude) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, StandardOutputEncoding = Encoding.UTF8 };
                psi.ArgumentList.Add("auth"); psi.ArgumentList.Add("status"); psi.ArgumentList.Add("--json");
                using var p = Process.Start(psi)!; var json = await p.StandardOutput.ReadToEndAsync(); await p.WaitForExitAsync();
                return ClaudeCode.ParseStatus(claude, json);
            };
            MainWindow.ClaudeRunOverride = (args, d, line, ct) =>
            {
                var psi = new ProcessStartInfo(claude) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = d };
                foreach (var a in args) psi.ArgumentList.Add(a);
                foreach (var v in ClaudeCode.ApiVariables) psi.Environment.Remove(v);
                psi.Environment.Remove(ClaudeCode.TokenVariable);
                return MainWindow.ClaudeRunProcess(psi, d, line, ct);
            };
            var st = w.ClaudeCheck(fresh: true); WaitFor(() => st.IsCompleted, 30_000);
            if (!st.Result.Ready) { File.WriteAllText(Path.Combine(dir, "BLOCKIERT.txt"), "Claude Code ist nicht über ein Abo angemeldet – Trendo meldet nie selbst an.\n"); return; }
            w.KiBackendsOverride = () => [new ClaudeKiBackend(w, () => "")];

            // 1. Satz über die Oberfläche
            w.Show("ablaeufe"); Settle();
            w.Find<TextBox>("wf-ki-prompt").Text = Satz; L("Satz eingegeben: " + Satz);
            RealClick(w, w.Find<Button>("wf-ki-anlegen"));
            WaitFor(() => !w.WfKiBusy, 5 * 60_000);
            var msg = w.Find<TextBlock>("wf-msg").Text ?? ""; L("Meldung: " + msg);
            wf = w.Workflows.All().SingleOrDefault();
            Assert.True(wf is not null, msg);
            File.WriteAllText(Path.Combine(dir, "1-ablauf-von-der-ki.json"), Clean(System.Text.Json.JsonSerializer.Serialize(wf, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), root) + "\n");
            Assert.False(wf!.Enabled); Assert.Equal(TriggerKind.Taeglich, wf.Trigger);
            L($"Ablauf angelegt: {wf.Name} · {wf.Trigger} {wf.At} · Schritte: {string.Join(", ", wf.Steps.Select(s => WorkflowEngine.StepLabel(s.Kind)))} · aus");

            // 2. Einschalten (Rückfrage: „Ja“ der Person ersetzt)
            w.Show("ablaeufe"); Settle();
            w.Find<Button>("wf-toggle-0").Press(); WaitFor(() => wf.Enabled, 30_000);
            Assert.True(wf.Enabled); L("Eingeschaltet nach Rückfrage: " + w.LastWirkung?.Title);

            // 3. Windows-Aufgabe über die Oberfläche anlegen – echtes schtasks.exe
            w.Show("ablaeufe"); Settle();
            w.Find<Button>("wf-task-create").Press(); WaitFor(() => wf.WinTask is not null || (w.Find<TextBlock>("wf-msg").Text ?? "").Contains("nicht"), 30_000);
            L("Meldung: " + w.Find<TextBlock>("wf-msg").Text);
            Assert.NotNull(wf.WinTask);
            var name = WorkflowTaskPlan.TaskName(w.Core.DataRoot, wf.Id);
            var q = Schtasks("/Query", "/TN", name, "/V", "/FO", "LIST"); Assert.Equal(0, q.Exit);
            File.WriteAllText(Path.Combine(dir, "2-windows-aufgabe-abfrage.txt"), Clean(q.Out, root));
            File.WriteAllText(Path.Combine(dir, "3-windows-aufgabe.xml"), Clean(Schtasks("/Query", "/TN", name, "/XML").Out, root));
            w.Show("ablaeufe"); Settle(); L("Anzeige: " + w.Find<TextBlock>("wf-zeitplan-text").Text);
            Assert.StartsWith("✓", w.Find<TextBlock>("wf-zeitplan-text").Text);

            // 4. Trendo-Fenster ist für den Ablauf nicht nötig: die Windows-Aufgabe startet TrendoOS.exe selbst (jetzt sofort statt um 0:01)
            var berichte = Directory.GetFiles(home, "*", SearchOption.AllDirectories);
            Assert.Empty(berichte);
            var run = Schtasks("/Run", "/TN", name); L($"schtasks /Run → {run.Exit}: {run.Out.Trim()}"); Assert.Equal(0, run.Exit);
            var started = DateTime.Now;
            string[] made = [];
            WaitFor(() => (made = Directory.Exists(home) ? Directory.GetFiles(home, "*.txt", SearchOption.AllDirectories) : []).Length > 0, 90_000);
            L($"Datei nach {(DateTime.Now - started).TotalSeconds:F1} s: {string.Join(", ", made.Select(f => Path.GetRelativePath(home, f)))}");
            var file = Assert.Single(made);
            File.WriteAllText(Path.Combine(dir, "4-erzeugte-datei.txt"), File.ReadAllText(file).Replace("\r\n", "\n"));
            WaitFor(() => File.Exists(Path.Combine(w.Core.DataRoot, "ablaeufe", "windows-aufgabe.log")), 20_000);
            File.WriteAllText(Path.Combine(dir, "5-windows-aufgabe.log"), Clean(File.ReadAllText(Path.Combine(w.Core.DataRoot, "ablaeufe", "windows-aufgabe.log")), root));
            var hist = File.ReadAllLines(Path.Combine(w.Core.DataRoot, "ablaeufe", "protokoll.jsonl")).Where(l => l.Contains(wf.Id)).ToList();
            File.WriteAllText(Path.Combine(dir, "6-ablauf-protokoll.jsonl"), Clean(string.Join("\n", hist), root) + "\n");
            Assert.Contains("ok:", File.ReadAllText(Path.Combine(dir, "5-windows-aufgabe.log")));

            // 5. Ein Klick entfernt die Aufgabe
            w.Show("ablaeufe"); Settle();
            w.Find<Button>("wf-task-remove").Press(); Settle();
            var gone = Schtasks("/Query", "/TN", name); L($"Nach „Windows-Aufgabe entfernen“: schtasks /Query → {gone.Exit}"); Assert.NotEqual(0, gone.Exit);
            L("Fertig.");
        }
        finally
        {
            if (wf is not null && Schtasks("/Query", "/TN", WorkflowTaskPlan.TaskName(w.Core.DataRoot, wf.Id)).Exit == 0) Schtasks("/Delete", "/TN", WorkflowTaskPlan.TaskName(w.Core.DataRoot, wf.Id), "/F"); // nie liegen lassen
            File.WriteAllText(Path.Combine(dir, "0-ablauf.log"), Clean(log.ToString(), root));
            var sums = new StringBuilder();
            foreach (var f in Directory.GetFiles(dir).Where(f => !f.EndsWith("SHA256.txt")).Order()) sums.Append($"{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant()}  {Path.GetFileName(f)}\n");
            File.WriteAllText(Path.Combine(dir, "SHA256.txt"), sums.ToString());
            MainWindow.ClaudeRunOverride = oldRun; MainWindow.ClaudeStatusOverride = oldStatus; MainWindow.WfTaskExe = null;
            MainWindow.WorkflowHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); w.KiBackendsOverride = null; w.Close();
        }
    }
}
