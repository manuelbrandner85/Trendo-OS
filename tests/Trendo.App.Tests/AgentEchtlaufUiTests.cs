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

/// <summary>T-0698 ECHTLAUF (nichts ersetzt außer der Fensterdarstellung): ein Agent aus „KI-Agenten bauen“ läuft mit dem echten lokalen Ollama-Modell
/// bzw. mit dem echten Claude Code über das eigene Abo. Nur mit gesetzter Umgebungsvariable TRENDO_AGENT_ECHT=&lt;Belegordner&gt;; sonst übersprungen.
/// TRENDO_AGENT_MODELL wählt das Ollama-Modell (Standard qwen3:14b). Der Claude-Lauf braucht ein bereits angemeldetes Claude Code – Trendo meldet nie an.</summary>
public class AgentEchtlauf_Ui
{
    const string K = "agenten-bauen-programm";
    const string Goal = "Lege die Datei einkauf.md mit genau drei Einkaufsartikeln als Liste an (je Zeile ein echter Artikel, z. B. „- Milch“). Lies die Datei danach mit datei.lesen und ergänze einen vierten Artikel, indem du die ganze Datei neu schreibst. Melde dich dann fertig.";

    static string? Beleg() => Environment.GetEnvironmentVariable("TRENDO_AGENT_ECHT") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;
    static void WaitFor(Func<bool> done, int ms) { var sw = Stopwatch.StartNew(); while (!done() && sw.ElapsedMilliseconds < ms) { Thread.Sleep(50); Settle(); } Settle(); }

    static AgentRun RunViaUi(MainWindow w, string name, string model, int minutes)
    {
        w.ProgramNew(K, "leer"); w.Show(K); Settle();
        var d = w.ProgramActive(K)!;
        d.Set("name", name, "Name"); d.Set("goal", Goal, "Ziel"); d.Set("model", model, "Modell"); d.Set("maxsteps", "8", "Schritte"); d.Set("maxmin", minutes.ToString(), "Minuten");
        w.Show(K); Settle();
        RealClick(w, w.Find<Button>("prog-cmd-agent-start")); Settle();
        WaitFor(() => w.AgentTask0?.IsCompleted == true, (minutes + 1) * 60_000);
        Assert.True(w.AgentTask0!.IsCompleted, "Lauf nicht beendet");
        return w.Agents.Runs(name, 1).Single();
    }

    static void Write(string dir, string prefix, MainWindow w, AgentRun r, string note)
    {
        var def = w.Agents.Definition(r.AgentId)!;
        var file = w.Core.Projects.ResolveInProject(def.ProjectId!, "einkauf.md");
        var result = w.Core.Projects.ResolveInProject(def.ProjectId!, r.ResultFile!);
        var sb = new StringBuilder();
        sb.AppendLine($"# {prefix}: echter Agentenlauf (T-0698)").AppendLine().AppendLine(note).AppendLine();
        sb.AppendLine($"Zustand: {r.State} · Schritte: {r.Steps} · KI: {r.Backend} ({r.Model}) · Dauer: {(r.Finished - r.Started)?.TotalSeconds:F1} s");
        sb.AppendLine($"Geschriebene Dateien: {string.Join(", ", r.FilesWritten)}").AppendLine();
        sb.AppendLine("## Protokoll").AppendLine();
        foreach (var e in r.Log) sb.AppendLine($"- {e.At:HH:mm:ss} · {e.Step} · {e.Kind}: {e.Text.Replace("\n", " ")}");
        if (File.Exists(file)) { sb.AppendLine().AppendLine("## einkauf.md").AppendLine().AppendLine("```").AppendLine(File.ReadAllText(file)).AppendLine("```"); File.Copy(file, Path.Combine(dir, prefix + "-einkauf.md"), true); }
        File.Copy(result, Path.Combine(dir, prefix + "-ergebnis.md"), true);
        File.WriteAllText(Path.Combine(dir, prefix + "-lauf.md"), sb.ToString().Replace(Environment.UserName, "<user>"));
        File.WriteAllText(Path.Combine(dir, prefix + "-lauf.json"), System.Text.Json.JsonSerializer.Serialize(r, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }).Replace(Environment.UserName, "<user>"));
        var sums = new StringBuilder();
        foreach (var f in Directory.GetFiles(dir, prefix + "-*").Order()) sums.AppendLine($"{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant()}  {Path.GetFileName(f)}");
        File.WriteAllText(Path.Combine(dir, prefix + "-SHA256.txt"), sums.ToString());
    }

    [AvaloniaFact(DisplayName = "T-0698 ECHT Ollama: Agent mit lokalem Modell legt Datei an, liest sie, ergänzt sie (nur mit TRENDO_AGENT_ECHT)")]
    public void Real_ollama()
    {
        if (Beleg() is not { } dir) return; // ohne Belegordner kein Echtlauf (kein Netz/Modell in normalen Läufen)
        var model = Environment.GetEnvironmentVariable("TRENDO_AGENT_MODELL") is { Length: > 0 } m ? m : "qwen3:14b";
        var (w, _, _) = Ui.Open();
        try
        {
            w.KiBackendsOverride = () => [new OllamaBackend()];
            var r = RunViaUi(w, "einkauf-ollama", "lokal:" + model, 15);
            Write(dir, "ollama", w, r, $"Echtes Ollama auf 127.0.0.1:11434, Modell {model}. Bedienung über die Oberfläche (Knopf „Agent starten“), Fenster headless.");
            Assert.Equal(AgentRunState.Fertig, r.State);
            var def = w.Agents.Definition("einkauf-ollama")!;
            var lines = w.Core.Projects.ReadFile(def.ProjectId!, "einkauf.md").Split('\n').Count(l => l.TrimStart().StartsWith('-'));
            Assert.True(lines >= 4, $"nur {lines} Artikel");
            Assert.Contains(r.Log, e => e.Kind == "werkzeug" && e.Text.StartsWith("datei.lesen"));
        }
        finally { w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0698 ECHT Claude: Agent mit Claude über das eigene Abo (nur mit TRENDO_AGENT_ECHT und angemeldetem Claude Code)")]
    public void Real_claude()
    {
        if (Beleg() is not { } dir) return;
        var exe = Environment.GetEnvironmentVariable("TRENDO_CLAUDE_EXE") is { Length: > 0 } e ? e : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.exe");
        if (!File.Exists(exe)) { File.WriteAllText(Path.Combine(dir, "claude-BLOCKIERT.txt"), "Claude Code nicht gefunden."); return; }
        var (w, _, _) = Ui.Open();
        var oldRun = MainWindow.ClaudeRunOverride; var oldStatus = MainWindow.ClaudeStatusOverride;
        try
        {
            // Die Testumgebung startet Claude sonst nie (eigener Datenordner ohne Anmeldung). Hier: echtes Claude Code mit der vorhandenen Anmeldung der Person,
            // gleiche Argumente wie in Trendo (ClaudeCode.Args, Frage-Modus ohne Schreibwerkzeuge), keine API-Variablen.
            MainWindow.ClaudeStatusOverride = async () => // echter Anmeldestand („claude auth status“), ohne anzumelden
            {
                var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, StandardOutputEncoding = Encoding.UTF8 };
                psi.ArgumentList.Add("auth"); psi.ArgumentList.Add("status"); psi.ArgumentList.Add("--json");
                using var p = Process.Start(psi)!; var json = await p.StandardOutput.ReadToEndAsync(); await p.WaitForExitAsync();
                return ClaudeCode.ParseStatus(exe, json);
            };
            MainWindow.ClaudeRunOverride = (args, d, line, ct) =>
            {
                var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = d };
                foreach (var a in args) psi.ArgumentList.Add(a);
                foreach (var v in ClaudeCode.ApiVariables) psi.Environment.Remove(v);
                psi.Environment.Remove(ClaudeCode.TokenVariable);
                return MainWindow.ClaudeRunProcess(psi, d, line, ct);
            };
            var st = w.ClaudeCheck(fresh: true); WaitFor(() => st.IsCompleted, 30_000); // zwischengespeicherten Test-Stand („nicht installiert“) verwerfen
            if (!st.Result.Ready) { File.WriteAllText(Path.Combine(dir, "claude-BLOCKIERT.txt"), "Claude Code ist nicht über ein Abo angemeldet – Trendo meldet nie selbst an. " + st.Result.Text); return; }
            w.KiBackendsOverride = () => [new ClaudeKiBackend(w, () => "")];
            var r = RunViaUi(w, "einkauf-claude", "claude", 15);
            Write(dir, "claude", w, r, "Echtes Claude Code (Abo, bereits angemeldet), Frage-Modus. Die Werkzeuge führt Trendo aus, nicht Claude.");
            Assert.Equal(AgentRunState.Fertig, r.State);
            var def = w.Agents.Definition("einkauf-claude")!;
            Assert.True(w.Core.Projects.ReadFile(def.ProjectId!, "einkauf.md").Split('\n').Count(l => l.TrimStart().StartsWith('-')) >= 4);
        }
        finally { MainWindow.ClaudeRunOverride = oldRun; MainWindow.ClaudeStatusOverride = oldStatus; w.KiBackendsOverride = null; w.Close(); }
    }
}
