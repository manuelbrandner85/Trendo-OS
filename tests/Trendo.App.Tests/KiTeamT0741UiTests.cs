using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0741 (CORE-03 „KI-Team“) über die Oberfläche: Fortschritt je Rolle, Stopp-Knopf, Zwischenstand speichern, Team-Lauf fortsetzen,
/// „im Internet nachlesen“ erst nach „Zustimmen“.
/// ERSETZT: das KI-Modell (<see cref="WaitKi"/>) und der Internet-Abruf (<see cref="MainWindow.AgentWebOverride"/>). Echt: Oberfläche (echte Klicks), Team-Schleife,
/// Agenten-Lauf, Werkzeuge, Projektdateien, Freigabe-Tor. Der Lauf mit echtem lokalem Modell: <see cref="KiTeamEchtlauf_Ui"/>.</summary>
public class KiTeamT0741_Ui
{
    /// <summary>ERSETZTES Modell: antwortet über eine Funktion, die auch warten kann (bis „Stopp“).</summary>
    sealed class WaitKi(Func<IReadOnlyList<KiMessage>, CancellationToken, Task<string>> reply) : IKiBackend
    {
        public string Name => "Ersatz-Modell (Test)";
        public string BaseUrl => "http://127.0.0.1:1";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public async Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> m, CancellationToken ct = default) { await Task.Delay(5, ct); return await reply(m, ct); }
    }

    static string Call(string tool, object args) => new JsonObject { ["gedanke"] = "weiter mit " + tool, ["werkzeug"] = tool, ["argumente"] = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(args)) }.ToJsonString();
    static string Done(string text) => new JsonObject { ["gedanke"] = "fertig", ["fertig"] = text }.ToJsonString();
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); Settle(); }
    static void WaitFor(Func<bool> done, int ms = 10000) { var sw = System.Diagnostics.Stopwatch.StartNew(); while (!done() && sw.ElapsedMilliseconds < ms) { Thread.Sleep(10); Settle(); } Settle(); }
    static string RoleText(MainWindow w, int i) => w.TryFind<TextBlock>($"team-role-{i}")?.Text ?? "";
    static string Role(IReadOnlyList<KiMessage> m) => m[0].Content is var s && s.Contains("„Planer“") ? "planer" : s.Contains("„Texter“") ? "texter" : s.Contains("„Lektor“") ? "lektor" : s.Contains("„Rechercheur“") ? "recherche" : "andere";

    static void StartTeam(MainWindow w, string project, string team, string goal)
    {
        w.Show("ki-rollen"); Settle();
        var pick = w.Find<ComboBox>("agent-project");
        pick.SelectedIndex = 1 + w.Core.Projects.List().ToList().FindIndex(p => p.Id == project); Settle();
        w.Find<TextBox>("agent-goal").Text = goal;
        Tap(w, "team-" + team);
    }

    [AvaloniaFact(DisplayName = "T-0741 UI +1: Fortschritt je Rolle live, Stopp mitten im Lauf, Zwischenstand speichern, Team-Lauf fortsetzen bis fertig")]
    public void Progress_stop_snapshot_resume()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var resumed = false;
            w.KiBackendsOverride = () => [new WaitKi(async (m, ct) =>
            {
                switch (Role(m))
                {
                    case "planer": return Done("1. Begrüßung 2. Programm");
                    case "texter" when m.Count == 2: return Call("datei.schreiben", new { pfad = "einladung.md", inhalt = "Liebe Nachbarn, am Samstag feiern wir." });
                    case "texter" when !resumed: await Task.Delay(Timeout.Infinite, ct); return ""; // arbeitet, bis die Person „Stopp“ drückt
                    case "texter": return Done("Einladung steht in einladung.md.");
                    default: return Done("Geprüft: passt.");
                }
            })];
            w.Core.Projects.Create("fest", "Sommerfest");
            StartTeam(w, "fest", "text", "Einladung zum Sommerfest");
            WaitFor(() => RoleText(w, 1).Contains("arbeitet") && File.Exists(Path.Combine(w.Core.Projects.ProjectFilesRoot("fest"), "einladung.md")));
            Assert.Contains("Planer – fertig", RoleText(w, 0));
            Assert.Contains("Texter – arbeitet", RoleText(w, 1));
            Assert.Contains("Lektor – wartet", RoleText(w, 2));
            Assert.Contains("1. Begrüßung", w.Find<SelectableTextBlock>("team-role-note-0").Text);
            Assert.True(w.AgentRunning);

            Tap(w, "agent-stop");
            WaitFor(() => !w.AgentRunning);
            Assert.StartsWith("Team gestoppt", w.Find<TextBlock>("agent-msg").Text);
            Assert.Contains("Texter – gestoppt", RoleText(w, 1));            Assert.Contains("Lektor – wartet", RoleText(w, 2));
            Assert.Equal("Liebe Nachbarn, am Samstag feiern wir.", w.Core.Projects.ReadFile("fest", "einladung.md")); // Erarbeitetes bleibt

            Tap(w, "team-snapshot");
            var run = w.TeamLive!.Run;
            var snap = w.Core.Projects.ReadFile("fest", $"team/{run}/zwischenstand.md");
            Assert.Contains("Texter – gestoppt", snap); Assert.Contains("1. Begrüßung", snap);
            Assert.StartsWith("Zwischenstand gespeichert", w.Find<TextBlock>("agent-msg").Text);

            resumed = true;
            Tap(w, "team-resume");
            WaitFor(() => !w.AgentRunning && w.Find<TextBlock>("agent-msg").Text?.StartsWith("Team fertig") == true);
            Assert.StartsWith("Team fertig", w.Find<TextBlock>("agent-msg").Text);
            for (var i = 0; i < 3; i++) Assert.Contains("– fertig", RoleText(w, i));
            Assert.Contains("Runde 2", RoleText(w, 1));
            Assert.Null(w.TryFind<Button>("team-resume")); // fertig → nichts mehr fortzusetzen
        }
        finally { w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0741 UI +2: Rechercheur fragt vor dem Internet-Abruf – erst nach „Zustimmen“ wird gelesen, Ergebnis landet in der Übergabe-Datei")]
    public void Web_only_after_approval_click()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var fetched = 0;
            w.AgentWebOverride = (u, ct) => { Interlocked.Increment(ref fetched); return Task.FromResult(("text/html", Encoding.UTF8.GetBytes("<html><head><title>Fest</title></head><body><p>Das Sommerfest ist am 12. Juli.</p></body></html>"), u)); };
            w.KiBackendsOverride = () => [new WaitKi((m, ct) => Task.FromResult(Role(m) == "recherche"
                ? m.Count == 2 ? Call("web.lesen", new { adresse = "https://example.org/sommerfest" }) : Done("Fakten: " + (m[^1].Content.Contains("12. Juli") ? "Fest am 12. Juli" : "keine"))
                : Done("ok")))];
            w.Core.Projects.Create("fest", "Sommerfest");
            StartTeam(w, "fest", "projekt", "Sommerfest planen");
            WaitFor(() => w.TryFind<Border>("team-approve") is not null);
            Assert.Equal(0, fetched); // ohne Zustimmung nichts abgerufen
            Assert.Contains("wartet auf deine Freigabe", w.Find<SelectableTextBlock>("team-role-note-0").Text);
            Tap(w, "team-approve-yes");
            WaitFor(() => !w.AgentRunning && w.Find<TextBlock>("agent-msg").Text?.StartsWith("Team") == true);
            Assert.Equal(1, fetched);
            Assert.StartsWith("Team fertig", w.Find<TextBlock>("agent-msg").Text);
            var st = w.TeamLive!;
            Assert.Equal("Fakten: Fest am 12. Juli", st.Roles[0].Result);
            Assert.Contains("Fest am 12. Juli", w.Core.Projects.ReadFile("fest", st.Roles[0].File!));
        }
        finally { w.KiBackendsOverride = null; w.AgentWebOverride = null; w.Close(); }
    }
}
