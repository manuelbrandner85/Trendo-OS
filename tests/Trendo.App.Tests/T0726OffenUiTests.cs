using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Karten;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0726 (offene Probleme, Gruppe A) – echt über die Oberfläche: Projekt-Notiz und Lernkarten nach der Tipp-Pause gespeichert, Lernkarte löschen mit
/// Rückgängig (samt Lernstand), Live streamen: Text der Zelle mit Fokus nach der Tipp-Pause gespeichert, Kartenkacheln nur nach Zustimmung über das Freigabe-Tor.</summary>
public class T0726Offen_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static bool Has(MainWindow w, string name) { Settle(); return w.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); Settle(); }
    static void Type(MainWindow w, string name, string text) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); Settle(); RealClick(w, Get<TextBox>(w, name)); tb = Get<TextBox>(w, name); tb.Focus(); Settle(); var end = tb.Text?.Length ?? 0; tb.SelectionStart = end; tb.SelectionEnd = end; tb.CaretIndex = end; Settle(); /* zwei Klicks auf dieselbe Stelle kurz nacheinander zählen als Doppelklick und markieren ein Wort (z. B. „:“) – Markierung aufheben, Cursor ans Ende */ foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } }
    /// <summary>Tipp-Pause abwarten und dann bis zu 10 s auf das gespeicherte Ergebnis – im vollen Testlauf unter Last kommt der Speicher-Takt später.</summary>
    static void PauseUntil(Func<bool> ok) { Pause(); var until = DateTime.Now + TimeSpan.FromSeconds(10); while (!ok() && DateTime.Now < until) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } Settle(); }
    static void Pause() { var until = DateTime.Now + MainWindow.LinkTypingDelay + TimeSpan.FromMilliseconds(150); while (DateTime.Now < until) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } Settle(); }
    static async Task Until(Func<bool> ok) { for (var i = 0; i < 300 && !ok(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Settle(); Assert.True(ok()); }

    [AvaloniaFact(DisplayName = "T-0726 A7: Projekt-Notiz – Getipptes ist nach der Tipp-Pause gespeichert, ohne das Feld zu verlassen (Fenster zu → Notiz da)")]
    public void Project_note_saved_on_pause()
    {
        MainWindow.LinkTypingDelay = TimeSpan.FromMilliseconds(300);
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            w.Core.Projects.Create("umzug", "Umzug"); w.ProjectSelected = "umzug";
            w.Show("projects"); Settle();
            Type(w, "proj-note", "Kartons bestellen"); Assert.Equal("Kartons bestellen", Get<TextBox>(w, "proj-note").Text); PauseUntil(() => w.Core.Projects.Note("umzug") == "Kartons bestellen");
            Assert.True(Get<TextBox>(w, "proj-note").IsFocused); // nicht verlassen
            Assert.Equal("Kartons bestellen", w.Core.Projects.Note("umzug"));
            Type(w, "proj-note", ", Helfer fragen"); PauseUntil(() => w.Core.Projects.Note("umzug") == "Kartons bestellen, Helfer fragen");
            w.Close(); // Feld nicht verlassen
            Assert.Equal("Kartons bestellen, Helfer fragen", new ProjectStore(Path.Combine(root, "projects"), new AuditLog(Path.Combine(root, "audit", "audit.jsonl"))).Note("umzug"));
        }
        finally { MainWindow.LinkTypingDelay = TimeSpan.FromSeconds(1); if (w.IsVisible) w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0726 A7: Lernkarte löschen lässt sich rückgängig machen (Knopf und Strg+Z) – mit Lernstand und an alter Stelle; Bearbeiten nach der Tipp-Pause gespeichert")]
    public void Learn_delete_undo_and_edit_on_pause()
    {
        MainWindow.LinkTypingDelay = TimeSpan.FromMilliseconds(300);
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            var a = w.Learn.Add("Englisch", "Baum", "tree"); w.Learn.Add("Englisch", "Haus", "house"); w.Learn.Add("Englisch", "Katze", "cat");
            w.Learn.Answer(a.Id, Grade.Falsch); w.Learn.Answer(a.Id, Grade.Gut); // Lernstand, der beim Zurückholen bleiben muss
            var before = JsonSerializer.Serialize(w.Learn.Store.Cards.First(c => c.Id == a.Id));
            var order = w.Learn.Store.Cards.Select(c => c.Id).ToList();
            w.LearnDeck = "Englisch"; w.LearnMode = "karten"; w.Show("lernen"); Settle();
            var row = Enumerable.Range(0, 3).First(i => Get<TextBox>(w, $"learn-f-{i}").Text == "Baum");
            Press(w, $"learn-del-{row}");
            Assert.DoesNotContain(w.Learn.Store.Cards, c => c.Id == a.Id);
            Press(w, "learn-undo-del");
            Assert.Equal(order, w.Learn.Store.Cards.Select(c => c.Id).ToList()); // alte Stelle
            Assert.Equal(before, JsonSerializer.Serialize(w.Learn.Store.Cards.First(c => c.Id == a.Id))); // samt Lernstand
            Assert.Equal(before, JsonSerializer.Serialize(new LearnBox(root).Store.Cards.First(c => c.Id == a.Id))); // auch auf der Platte
            Assert.False(Has(w, "learn-undo-del"));
            // Strg+Z auf der Seite (außerhalb eines Textfelds)
            row = Enumerable.Range(0, 3).First(i => Get<TextBox>(w, $"learn-f-{i}").Text == "Katze");
            Press(w, $"learn-del-{row}");
            Assert.Equal(2, w.Learn.Store.Cards.Count);
            Get<DockPanel>(w, "learn-root").Focus(); Settle();
            w.KeyPress(Avalonia.Input.Key.Z, Avalonia.Input.RawInputModifiers.Control, Avalonia.Input.PhysicalKey.Z, null); w.KeyRelease(Avalonia.Input.Key.Z, Avalonia.Input.RawInputModifiers.Control, Avalonia.Input.PhysicalKey.Z, null); Settle();
            Assert.Equal(3, w.Learn.Store.Cards.Count);
            // Bearbeiten: nach der Tipp-Pause gespeichert, Feld nicht verlassen
            row = Enumerable.Range(0, 3).First(i => Get<TextBox>(w, $"learn-f-{i}").Text == "Haus");
            Type(w, $"learn-b-{row}", " (das)"); Pause();
            Assert.Equal("house (das)", new LearnBox(root).Store.Cards.First(c => c.Front == "Haus").Back);
        }
        finally { MainWindow.LinkTypingDelay = TimeSpan.FromSeconds(1); if (w.IsVisible) w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0726 A9: Live streamen – Text in der Zelle mit Fokus ist nach der Tipp-Pause gespeichert (Fenster zu ohne Verlassen → da) und ein Rückgängig-Schritt; Unfertiges wartet")]
    public void Broadcast_focused_cell_saved_on_pause()
    {
        MainWindow.LinkTypingDelay = TimeSpan.FromMilliseconds(300);
        var (w, _, root) = Ui.Open(); w.Width = 1700; w.Height = 1100;
        try
        {
            w.Broadcast.Add("Begrüßung", 90, ""); w.Broadcast.Add("Interview", 300, ""); w.Broadcast.Save();
            w.Show("sendung"); Settle();
            Type(w, "bc-note-1", "Kamera 2, Gast"); PauseUntil(() => new BroadcastDesk(root).Plan.Segments[1].Note == "Kamera 2, Gast");
            Assert.True(Get<TextBox>(w, "bc-note-1").IsFocused);
            Assert.Equal("Kamera 2, Gast", new BroadcastDesk(root).Plan.Segments[1].Note);
            // Rückgängig nimmt das Getippte als einen Schritt zurück
            Press(w, "bc-undo");
            Assert.Equal("", new BroadcastDesk(root).Plan.Segments[1].Note);
            Press(w, "bc-redo");
            Assert.Equal("Kamera 2, Gast", new BroadcastDesk(root).Plan.Segments[1].Note);
            // Dauer halb getippt („5:“): nichts Ungültiges gespeichert, kein Fehler – erst vollständig
            var dur = Get<TextBox>(w, "bc-dur-0"); dur.Text = ""; Type(w, "bc-dur-0", "2:"); Pause();
            Assert.Equal(90, new BroadcastDesk(root).Plan.Segments[0].Seconds);
            Type(w, "bc-dur-0", "30"); PauseUntil(() => new BroadcastDesk(root).Plan.Segments[0].Seconds == 150);
            Assert.True(new BroadcastDesk(root).Plan.Segments[0].Seconds == 150, $"gespeichert {new BroadcastDesk(root).Plan.Segments[0].Seconds} s, im Feld „{Get<TextBox>(w, "bc-dur-0").Text}“, Cursor {Get<TextBox>(w, "bc-dur-0").CaretIndex}");
            Assert.Equal(150, new BroadcastDesk(root).Plan.Segments[0].Seconds);
            Type(w, "bc-title-0", " und Vorschau"); PauseUntil(() => new BroadcastDesk(root).Plan.Segments[0].Title == "Begrüßung und Vorschau");
            w.Close(); // Feld nicht verlassen
            Assert.Equal("Begrüßung und Vorschau", new BroadcastDesk(root).Plan.Segments[0].Title);
        }
        finally { MainWindow.LinkTypingDelay = TimeSpan.FromSeconds(1); if (w.IsVisible) w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0726 A6: Kartenkacheln aus dem Internet nur nach Zustimmung über das Freigabe-Tor – Nein lädt nichts, Ja gilt als Einstellung (im Protokoll), ausschaltbar; offline kein Angebot")]
    public async Task Map_tiles_need_consent()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            w.ProgramNew("karten"); w.Show("karten"); Settle();
            Assert.False(TileConsent.Allowed(root));
            Assert.True(Has(w, "mk-tiles-allow"));
            w.FreigabeConfirm = _ => Task.FromResult(false);
            Press(w, "mk-tiles-allow");
            await Until(() => w.LastWirkung?.Action == TileConsent.Action);
            await Until(() => w.Core.Audit.ReadAll().Any(a => a.Action == "freigabe.nein"));
            Assert.False(TileConsent.Allowed(root));
            Assert.Equal(TileConsent.Target, w.LastWirkung!.Target);
            Assert.Contains("IP-Adresse", w.LastWirkung.Text);
            w.FreigabeConfirm = _ => Task.FromResult(true);
            Press(w, "mk-tiles-allow");
            await Until(() => TileConsent.Allowed(root));
            var log = w.Core.Audit.ReadAll();
            Assert.Contains(log, a => a.Action == "execution.start" && a.Detail.Contains(TileConsent.Action));
            Assert.True(Has(w, "mk-tiles-stop")); Assert.False(Has(w, "mk-tiles-allow"));
            Press(w, "mk-tiles-stop");
            Assert.False(TileConsent.Allowed(root));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "karten.kacheln.aus");
        }
        finally { w.Close(); }

        var (o, _, oroot) = Ui.Open(offline: true); o.Width = 1600; o.Height = 1000;
        try
        {
            o.ProgramNew("karten"); o.Show("karten"); Settle();
            Assert.False(Has(o, "mk-tiles-allow"));
            Assert.False(await o.MapTilesAllow());
            Assert.False(TileConsent.Allowed(oroot));
            Assert.DoesNotContain(o.Core.Audit.ReadAll(), a => a.Action == "approval.requested" && a.Detail.Contains(TileConsent.Action));
        }
        finally { o.Close(); }
    }
}

/// <summary>T-0726 A5: GitHub-Upload im Claude-Chat über das Freigabe-Tor (wie Netlify) – nachgebildete gh/git, nie ein echtes Konto.</summary>
[Collection("Claude")]
public class T0726GitHubTor_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }
    static async Task Until(Func<bool> ok) { for (var i = 0; i < 300 && !ok(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Pump(); Assert.True(ok()); }
    static async Task Run(Task t) { while (!t.IsCompleted) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);

    static Func<IReadOnlyList<string>, string, Action<string>, CancellationToken, Task<int>> FakeClaude() => (args, dir, line, _) =>
    {
        line(J(new { type = "system", subtype = "init", session_id = "s-1", model = "claude" }));
        File.WriteAllText(Path.Combine(dir, "index.html"), ClaudeChat_Ui.Page("<h1>Kontakt</h1>"));
        Directory.CreateDirectory(Path.Combine(dir, ".trendo")); File.WriteAllText(Path.Combine(dir, ".trendo", "intern.json"), "{}");
        File.WriteAllText(Path.Combine(dir, ".env"), "GEHEIM=1");
        line(J(new { type = "result", subtype = "success", is_error = false, result = "Fertig", session_id = "s-1", num_turns = 1 }));
        return Task.FromResult(0);
    };

    [AvaloniaFact(DisplayName = "T-0726 A5: „GitHub“ im Claude-Chat fragt im Freigabe-Tor (Repo, Sichtbarkeit, Dateien) – Nein lädt nichts hoch, Ja genau einmal; ändert sich vorher eine Datei, blockiert der Kern")]
    public async Task GitHub_upload_through_gate()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<string[]>(); var loggedIn = false;
        MainWindow.ConnRunOverride = (exe, args, cwd, env, vis) =>
        {
            lock (calls) calls.Add([.. args]);
            if (args is ["auth", "login", ..]) loggedIn = true;
            if (args is ["auth", "status", ..]) return Task.FromResult(loggedIn ? (0, "github.com\n  ✓ Logged in to github.com account erika-muster (keyring)") : (1, "nicht angemeldet"));
            if (args is ["repo", "create", var name, ..]) return Task.FromResult((0, $"https://github.com/erika-muster/{name}"));
            return Task.FromResult((0, ""));
        };
        MainWindow.GhExeOverride = () => @"C:\x\gh.exe";
        MainWindow.ClaudeStatusOverride = () => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe")); MainWindow.ClaudeRunOverride = FakeClaude();
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Pump();
            await Run(w.ClaudeChatSend("Baue eine Kontaktseite"));
            var chat = w.ClaudeChatCurrent()!;
            await MainWindow.ConnRunOverride!(@"C:\x\gh.exe", ["auth", "login", "--web"], "", new Dictionary<string, string>(), true); await Run(w.GitHubCheck());
            void Open() { w.Find<Button>("claude-chat-github").Press(); Pump(); if (w.TryFind<Button>("claude-github-yes") is null) { w.Find<Button>("claude-chat-github").Press(); Pump(); } }

            // Nein im Tor: nichts angelegt, nichts hochgeladen
            w.FreigabeConfirm = _ => Task.FromResult(false);
            Open(); w.Find<Button>("claude-github-yes").Press();
            await Until(() => w.ClaudeChatCurrent()!.Messages[^1].Text.StartsWith("GitHub: Abgebrochen"));
            Assert.DoesNotContain(calls, c => c is ["repo", ..] || c is ["add", ..] || c is ["init", ..]);
            var asked = w.LastWirkung!;
            Assert.Equal(GitHubLink.ActionUpload, asked.Action);
            Assert.StartsWith("github.com/erika-muster/baue-eine-kontaktseite#privat#", asked.Target);
            Assert.Contains("index.html", asked.Preview); Assert.Contains(".gitignore", asked.Preview);
            Assert.DoesNotContain(".trendo", asked.Preview); Assert.DoesNotContain(".env", asked.Preview);

            // Datei ändert sich zwischen Rückfrage und Hochladen: der Kern führt nichts aus
            w.FreigabeConfirm = _ => { File.AppendAllText(Path.Combine(chat.Folder, "index.html"), "<!-- geändert -->"); return Task.FromResult(true); };
            Open(); w.Find<Button>("claude-github-yes").Press();
            await Until(() => w.ClaudeChatCurrent()!.Messages[^1].Text.StartsWith("GitHub: ") && !w.ClaudeChatCurrent()!.Messages[^1].Text.Contains("Abgebrochen"));
            Assert.DoesNotContain(calls, c => c is ["repo", ..]);
            Assert.Null(w.LastGitHubRepo);

            // Ja: genau einmal, über das Tor (Protokoll), privat
            w.FreigabeConfirm = _ => Task.FromResult(true);
            Open(); w.Find<Button>("claude-github-yes").Press();
            await Until(() => w.LastGitHubRepo is not null);
            Assert.Equal("https://github.com/erika-muster/baue-eine-kontaktseite", w.LastGitHubRepo);
            Assert.Contains("--private", Assert.Single(calls, c => c is ["repo", "create", ..]));
            var id = w.LastFreigabe!.Id;
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "execution.start" && a.Detail.StartsWith(id));
            Assert.Throws<ApprovalException>(() => w.Core.Approvals.Execute(id, GitHubLink.ActionUpload, w.LastWirkung!.Target, () => true)); // einmalig
        }
        finally { MainWindow.ConnRunOverride = null; MainWindow.GhExeOverride = null; MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; w.Close(); }
    }
}
