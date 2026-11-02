using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Befund 02.11.2026 (echter Lauf im Spiele-Studio): Claude meldete „fertig“, der Prozess blieb aber offen (mitgestartete MCP-Werkzeuge) – Trendo wartete ewig;
/// und Claude baute eigene Dateien statt im Programm-Dokument zu arbeiten. Beides ist hier abgesichert. Die Prozess-Tests nutzen cmd.exe, nie das echte Claude.
[Collection("Claude")]
public class ClaudeRunFix_Ui
{
    static Task<ClaudeCode.Status> St() => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-lauf-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static ProcessStartInfo Cmd(string dir, string command) { var psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true, WorkingDirectory = dir }; psi.ArgumentList.Add("/d"); psi.ArgumentList.Add("/c"); psi.ArgumentList.Add(command); return psi; }

    [Fact(DisplayName = "Claude-Lauf: „result“ gemeldet, Prozess bleibt offen (wie mit MCP-Werkzeugen) – Trendo ist trotzdem gleich fertig und räumt den Prozess auf; Mitschrift im Projekt")]
    public async Task Result_ends_run_even_if_process_stays()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dir = Temp(); var lines = new List<string>();
        try
        {
            var sw = Stopwatch.StartNew();
            var script = Path.Combine(dir, "lauf.cmd"); // wie Claude mit MCP-Werkzeugen: meldet „result“, bleibt dann offen
            File.WriteAllText(script, "@echo {\"type\":\"result\",\"subtype\":\"success\",\"result\":\"ok\"}\r\n@ping -n 60 127.0.0.1 >nul\r\n");
            var code = await MainWindow.ClaudeRunProcess(Cmd(dir, script), dir, lines.Add, CancellationToken.None);
            Assert.Equal(0, code); Assert.True(sw.Elapsed < TimeSpan.FromSeconds(25), $"dauerte {sw.Elapsed}"); // nicht 60 s warten
            Assert.Contains(lines, l => l.Contains("\"result\""));
            Assert.Contains("\"type\":\"result\"", File.ReadAllText(Path.Combine(dir, ".trendo", "letzter-lauf.jsonl")));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "Claude-Lauf: meldet zu lange nichts – Wächter beendet ihn mit verständlichem Hinweis")]
    public async Task Silence_watchdog()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dir = Temp(); var lines = new List<string>(); var before = MainWindow.ClaudeSilenceLimit;
        try
        {
            MainWindow.ClaudeSilenceLimit = TimeSpan.FromSeconds(2);
            var sw = Stopwatch.StartNew();
            var code = await MainWindow.ClaudeRunProcess(Cmd(dir, "ping -n 60 127.0.0.1 >nul"), dir, lines.Add, CancellationToken.None);
            Assert.Equal(-1, code); Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"dauerte {sw.Elapsed}");
            Assert.Contains(lines, l => l.Contains("trendo_error") && l.Contains("nichts mehr gemeldet"));
        }
        finally { MainWindow.ClaudeSilenceLimit = before; try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [AvaloniaFact(DisplayName = "Am Programm vorbei gearbeitet (nur eigene Dateien, Studio-Dokument unverändert) – Trendo schickt Claude zurück ins Programm-Dokument; neue Projekte übernehmen keine bloß geöffnete Vorlage")]
    public async Task Back_into_program()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<IReadOnlyList<string>>();
        MainWindow.ClaudeStatusOverride = St;
        MainWindow.ClaudeRunOverride = (args, dir, line, _) =>
        {
            calls.Add(args);
            line(J(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
            if (calls.Count == 1) File.WriteAllText(Path.Combine(dir, "index.html"), ClaudeChat_Ui.Page("<canvas></canvas>")); // am Programm vorbei
            else
            {
                var tdoc = Directory.GetFiles(dir, "*.tdoc").Single(); var o = JsonNode.Parse(File.ReadAllText(tdoc))!.AsObject();
                o["data"]!.AsObject()["title"] = "Frontlinie"; File.WriteAllText(tdoc, o.ToJsonString());
            }
            line(J(new { type = "result", subtype = "success", is_error = false, result = "Fertig.", session_id = "s1", num_turns = 1 }));
            return Task.FromResult(0);
        };
        try
        {
            await Run(w.ClaudeCheck(true));
            var page = MainWindow.Programs.Keys.First(k => MainWindow.Programs[k].Studio == "CORE-06");
            w.Show(page); var template = w.ProgramNew(page, "topdown"); w.Show(page); // nur geöffnete Vorlage
            w.ClaudeDockToggle("CORE-06"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Baue ein Action-Spiel wie ein Militär-Shooter"));
            var doc = w.ProgramActive(page)!;
            Assert.NotSame(template, doc); Assert.Equal(w.ClaudeChatCurrent()!.Title, doc.Title); // eigenes Dokument, nicht die Vorlage
            Assert.Contains("WICHTIG – du arbeitest im Programm", calls[0][calls[0].ToList().IndexOf("--append-system-prompt") + 1]);
            Assert.True(calls.Count >= 2); Assert.Contains("nicht im Programm gearbeitet", calls[1][1]); // zurück ins Programm
            Assert.Equal("Frontlinie", (string?)w.ProgramActive(page)!.Data["title"]);
        }
        finally { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }
}
