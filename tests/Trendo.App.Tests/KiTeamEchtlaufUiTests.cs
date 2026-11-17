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

/// <summary>T-0741 ECHTLAUF (nichts ersetzt außer der Fensterdarstellung – headless): Team „Text schreiben &amp; prüfen“ mit dem echten lokalen Ollama-Modell über die Oberfläche –
/// starten, Fortschritt je Rolle beobachten, mitten im Lauf „Stopp“, „Zwischenstand speichern“, „Team-Lauf fortsetzen“ bis fertig.
/// Läuft nur mit TRENDO_TEAM_ECHT=&lt;Belegordner&gt;; TRENDO_TEAM_MODELL wählt das Modell (Standard qwen3:14b). Sonst übersprungen.
/// Der Beleg (Ablauf, Stand, Übergabe-Dateien, Agenten-Protokolle, SHA-256) wird immer geschrieben – auch wenn der Lauf scheitert.</summary>
public class KiTeamEchtlauf_Ui
{
    const string Goal = "Schreibe eine kurze Einladung (drei bis fünf Sätze) zum Nachbarschafts-Sommerfest am Samstag um 15 Uhr im Innenhof. Der Texter legt die Einladung als Datei einladung.md im Projekt an.";

    [AvaloniaFact(DisplayName = "T-0741 ECHT Ollama: KI-Team über die Oberfläche – Stopp, Zwischenstand, Fortsetzen (nur mit TRENDO_TEAM_ECHT)")]
    public void Real_ollama_team_stop_resume()
    {
        if (Environment.GetEnvironmentVariable("TRENDO_TEAM_ECHT") is not { Length: > 0 } d) return; // ohne Belegordner kein Echtlauf
        var dir = Directory.CreateDirectory(d).FullName;
        var model = Environment.GetEnvironmentVariable("TRENDO_TEAM_MODELL") is { Length: > 0 } m ? m : "qwen3:14b";
        var (w, _, _) = Ui.Open();
        var log = new StringBuilder(); var sw = Stopwatch.StartNew(); string? lastSeen = null; string? snapshot = null;
        const char Lf = '\n', Cr = '\r';
        void Note(string s) => log.Append($"[{sw.Elapsed.TotalSeconds,7:F1} s] {s}").Append(Lf);
        string OneLine(string s) => s.Replace(Cr, ' ').Replace(Lf, ' ');
        string Clean(string s) => s.Replace(Environment.NewLine, Lf.ToString()).Replace(Environment.UserName, "<user>");
        void Watch()
        {
            if (w.TeamLive is not { } st) return;
            string now; lock (st) now = string.Join(" | ", st.Roles.Select(r => $"{r.Name}: {AgentCenter.StatusText(r.Status)}{(r.Note.Length > 0 ? " – " + (r.Note.Length > 90 ? r.Note[..90] + " …" : r.Note) : "")}"));
            if (now != lastSeen) { lastSeen = now; Note("Fortschritt: " + OneLine(now)); }
        }
        bool WaitFor(Func<bool> done, int ms) { var t = Stopwatch.StartNew(); while (!done() && t.ElapsedMilliseconds < ms) { Thread.Sleep(50); Settle(); Watch(); } Settle(); Watch(); return done(); }
        void Tap(string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); Settle(); Note("Klick: " + name); }
        string Screen() => string.Join(" | ", Enumerable.Range(0, 3).Select(i => w.TryFind<TextBlock>($"team-role-{i}")?.Text));
        void WriteBeleg()
        {
            File.WriteAllText(Path.Combine(dir, "team-ablauf.log"), Clean(log.ToString()));
            if (w.TeamLive is { } t0 && w.Agents.TeamRun(t0.Id) is { } fin)
            {
                File.WriteAllText(Path.Combine(dir, "team-stand.json"), Clean(System.Text.Json.JsonSerializer.Serialize(fin, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })) + Lf);
                var root = w.Core.Projects.ProjectFilesRoot(fin.ProjectId);
                if (Directory.Exists(Path.Combine(root, "team", fin.Run)))
                    foreach (var f in Directory.GetFiles(Path.Combine(root, "team", fin.Run), "*.md")) File.WriteAllText(Path.Combine(dir, "uebergabe-" + Path.GetFileName(f)), Clean(File.ReadAllText(f)));
                if (File.Exists(Path.Combine(root, "einladung.md"))) File.WriteAllText(Path.Combine(dir, "einladung.md"), Clean(File.ReadAllText(Path.Combine(root, "einladung.md"))));
            }
            if (snapshot is not null) File.WriteAllText(Path.Combine(dir, "team-zwischenstand-nach-stopp.md"), Clean(snapshot));
            var runs = new StringBuilder();
            foreach (var ar in w.Agents.Runs(null, 50).Where(x => x.ProjectId == "sommerfest").OrderBy(x => x.Started))
            {
                runs.Append($"## {ar.AgentName} · Lauf {ar.Id} · {ar.State} · {ar.Steps} Schritte · KI {ar.Backend} ({ar.Model}) · {(ar.Finished - ar.Started)?.TotalSeconds:F1} s").Append(Lf).Append(Lf);
                foreach (var e in ar.Log) runs.Append($"- {e.At:HH:mm:ss} · {e.Step} · {e.Kind}: {OneLine(e.Text)}").Append(Lf);
                runs.Append(Lf);
            }
            File.WriteAllText(Path.Combine(dir, "agenten-protokolle.md"), Clean(runs.ToString()));
            var sums = new StringBuilder();
            foreach (var f in Directory.GetFiles(dir).Where(f => !f.EndsWith("SHA256.txt") && !f.EndsWith(".gitattributes") && !Path.GetFileName(f).StartsWith("erstversuch")).Order(StringComparer.Ordinal))
                sums.Append($"{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant()}  {Path.GetFileName(f)}").Append(Lf);
            File.WriteAllText(Path.Combine(dir, "SHA256.txt"), sums.ToString());
        }
        try
        {
            w.KiBackendsOverride = () => [new OllamaBackend()];
            w.TeamModelOverride = model;
            Note($"Echtes Ollama 127.0.0.1:11434, Modell {model}. Fenster headless, Bedienung über echte Klicks.");
            w.Core.Projects.Create("sommerfest", "Sommerfest");
            w.Show("ki-rollen"); Settle();
            w.Find<ComboBox>("agent-project").SelectedIndex = 1 + w.Core.Projects.List().ToList().FindIndex(p => p.Id == "sommerfest"); Settle();
            w.Find<TextBox>("agent-goal").Text = Goal;
            Tap("team-text");
            // Stopp, sobald der Planer fertig ist und der Texter arbeitet (mitten im Lauf)
            var reached = WaitFor(() => w.TeamLive is { } s && (s.Roles[0].Status == TeamRoleStatus.Fertig && s.Roles[1].Status == TeamRoleStatus.Arbeitet || s.State != AgentRunState.Laeuft && !w.AgentRunning), 15 * 60_000);
            Note("Meldung: " + w.Find<TextBlock>("agent-msg").Text);
            Assert.True(reached && w.AgentRunning, "Texter hat nicht angefangen: " + w.Find<TextBlock>("agent-msg").Text);
            Note("Bildschirm vor Stopp: " + Screen());
            Tap("agent-stop");
            Assert.True(WaitFor(() => !w.AgentRunning, 5 * 60_000), "Stopp hat nicht gegriffen");
            Note("Meldung: " + w.Find<TextBlock>("agent-msg").Text);
            Note("Bildschirm nach Stopp: " + Screen());
            var stopped = w.Agents.TeamRun(w.TeamLive!.Id)!;
            Assert.Equal(AgentRunState.Gestoppt, stopped.State);
            Assert.Equal(TeamRoleStatus.Fertig, stopped.Roles[0].Status);
            Assert.NotEqual(TeamRoleStatus.Fertig, stopped.Roles[2].Status);
            Tap("team-snapshot");
            Note("Meldung: " + w.Find<TextBlock>("agent-msg").Text);
            snapshot = w.Core.Projects.ReadFile("sommerfest", $"team/{stopped.Run}/zwischenstand.md");
            Tap("team-resume");
            Assert.True(WaitFor(() => !w.AgentRunning && w.Find<TextBlock>("agent-msg").Text?.StartsWith("Team") == true && w.Find<TextBlock>("agent-msg").Text?.Contains("fortgesetzt") != true, 30 * 60_000), "Fortsetzen nicht beendet");
            Note("Meldung: " + w.Find<TextBlock>("agent-msg").Text);
            Note("Bildschirm am Ende: " + Screen());
            var end = w.Agents.TeamRun(stopped.Id)!;
            Note($"Endstand: {end.State} · Rückgaben {end.Returns} · Rollen: " + string.Join(", ", end.Roles.Select(r => $"{r.Name} {AgentCenter.StatusText(r.Status)} (Runden {r.Rounds}, Schritte {r.Steps})")));
            Assert.Equal(AgentRunState.Fertig, end.State);
            Assert.All(end.Roles, r => Assert.Equal(TeamRoleStatus.Fertig, r.Status));
            Assert.Equal(1, end.Roles[0].Rounds); // Planer lief nach dem Fortsetzen nicht noch einmal
        }
        catch (Exception ex) { Note("FEHLGESCHLAGEN: " + OneLine(ex.Message)); throw; }
        finally { try { WriteBeleg(); } catch (IOException) { } w.KiBackendsOverride = null; w.TeamModelOverride = null; w.Close(); }
    }
}
