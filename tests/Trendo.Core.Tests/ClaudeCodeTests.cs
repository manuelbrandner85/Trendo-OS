using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0316: Claude Code über das Abo des Nutzers – Anmeldestand ohne persönliche Daten, eingegrenzte Läufe in zwei Phasen, Live-Ausgabe, Sicherung für „alles rückgängig“.
public class ClaudeCodeTests
{
    [Fact(DisplayName = "T-0316: Anmeldestand – nur angemeldet, Anmeldeart und Abo-Stufe; E-Mail und Organisation tauchen nirgends auf")]
    public void Status_without_personal_data()
    {
        var s = ClaudeCode.ParseStatus(@"C:\x\claude.exe", """{"loggedIn":true,"authMethod":"claude.ai","email":"erika@example.org","orgName":"Erika Muster","orgId":"1234","subscriptionType":"max"}""");
        Assert.Equal((true, true, "claude.ai", "max"), (s.Installed, s.LoggedIn, s.Method, s.Plan));
        Assert.Equal("✓ Mit deinem Claude-Abo verbunden (Max)", s.Text);
        Assert.DoesNotContain("erika", s.ToString(), StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("1234", s.ToString());
        Assert.False(ClaudeCode.ParseStatus("c", """{"loggedIn":false}""").LoggedIn);
        Assert.Contains("nicht angemeldet", ClaudeCode.ParseStatus("c", "kaputt").Text);
        Assert.Contains("nicht installiert", ClaudeCode.Status.Missing.Text);
    }

    [Fact(DisplayName = "T-0316: Nur das Abo zählt – Anmeldung über Console/API gilt nicht als verbunden; API-Schlüssel ≠ Abo-Token; API-Variablen werden für Läufe entfernt")]
    public void Subscription_only()
    {
        var api = ClaudeCode.ParseStatus("c", """{"loggedIn":true,"authMethod":"console"}""");
        Assert.True(api.LoggedIn); Assert.False(api.Ready); Assert.Contains("nur über dein Abo", api.Text);
        Assert.True(ClaudeCode.ParseStatus("c", """{"loggedIn":true,"authMethod":"oauth_token"}""").Ready);
        Assert.True(ClaudeCode.LooksLikeAboToken("sk-ant-oat01-" + new string('a', 40)));
        Assert.False(ClaudeCode.LooksLikeAboToken("sk-ant-api03-" + new string('a', 40))); Assert.True(ClaudeCode.LooksLikeApiKey("sk-ant-api03-xyz"));
        Assert.Contains("ANTHROPIC_API_KEY", ClaudeCode.ApiVariables); Assert.Contains("ANTHROPIC_BASE_URL", ClaudeCode.ApiVariables);
    }

    [Fact(DisplayName = "T-0316: Claude Code finden – offizieller Installer im Benutzerordner zuerst, dann npm und PATH")]
    public void Find_exe()
    {
        var env = new Dictionary<string, string> { ["USERPROFILE"] = @"C:\Users\nutzer", ["APPDATA"] = @"C:\Users\nutzer\AppData\Roaming", ["PATH"] = @"C:\Tools;C:\Bin" };
        var c = ClaudeCode.Candidates(k => env.GetValueOrDefault(k)).ToList();
        Assert.Equal(@"C:\Users\nutzer\.local\bin\claude.exe", c[0]);
        Assert.Contains(@"C:\Bin\claude.exe", c);
        Assert.Equal(@"C:\Tools\claude.cmd", ClaudeCode.Find(k => env.GetValueOrDefault(k), f => f == @"C:\Tools\claude.cmd"));
        Assert.Null(ClaudeCode.Find(k => env.GetValueOrDefault(k), _ => false));
    }

    [Fact(DisplayName = "T-0316: Läufe sind eingegrenzt – Plan ohne Datei-Änderungen, Umsetzen nur mit der Plan-Sitzung; immer --restricted und keine fremden Erweiterungen")]
    public void Args_are_restricted()
    {
        var plan = ClaudeCode.Args("Mach die Überschrift blau", ClaudeCode.Phase.Plan, system: "Du bist Webdesignerin.");
        Assert.Equal("Mach die Überschrift blau", plan[1]);
        foreach (var must in new[] { "--restricted", "--strict-mcp-config", "stream-json", "--verbose" }) Assert.Contains(must, plan);
        Assert.Equal("Edit Write NotebookEdit MultiEdit", plan[plan.ToList().IndexOf("--disallowedTools") + 1]);
        Assert.Contains("ändere NICHTS", plan[plan.ToList().IndexOf("--append-system-prompt") + 1]);
        Assert.Contains("Webdesignerin", plan[plan.ToList().IndexOf("--append-system-prompt") + 1]);
        Assert.DoesNotContain("--permission-mode", plan); Assert.DoesNotContain("bypassPermissions", plan);
        Assert.Throws<ArgumentException>(() => ClaudeCode.Args("x", ClaudeCode.Phase.Build));
        var build = ClaudeCode.Args("aber in Dunkelblau", ClaudeCode.Phase.Build, "sitzung-1", model: "opus");
        Assert.Equal(("sitzung-1", "acceptEdits", "opus"), (build[build.ToList().IndexOf("--resume") + 1], build[build.ToList().IndexOf("--permission-mode") + 1], build[build.ToList().IndexOf("--model") + 1]));
        Assert.Contains("Zusatz vom Nutzer: aber in Dunkelblau", build[1]);
        Assert.DoesNotContain("--model", ClaudeCode.Args("x", ClaudeCode.Phase.Plan, model: "opus; rm -rf"));
    }

    [Fact(DisplayName = "T-0316: Volle Claude-Funktionen – ohne Eingrenzung, Auto-Modus beim Umsetzen; Planen und Fragen ändern trotzdem nichts")]
    public void Full_level()
    {
        var build = ClaudeCode.Args("", ClaudeCode.Phase.Build, "s-1", level: ClaudeCode.Level.Voll).ToList();
        Assert.DoesNotContain("--restricted", build); Assert.DoesNotContain("--strict-mcp-config", build);
        Assert.Equal("auto", build[build.IndexOf("--permission-mode") + 1]);
        var plan = ClaudeCode.Args("x", ClaudeCode.Phase.Plan, level: ClaudeCode.Level.Voll).ToList();
        Assert.Equal("Edit Write NotebookEdit MultiEdit", plan[plan.IndexOf("--disallowedTools") + 1]); Assert.DoesNotContain("--permission-mode", plan);
        var ask = ClaudeCode.Args("Was ist ein Rechnungsbetrag?", ClaudeCode.Phase.Ask, system: "Du bist Buchhalter.").ToList();
        Assert.Contains("--restricted", ask); Assert.Contains("Haupt-KI in Trendo OS", ask[ask.IndexOf("--append-system-prompt") + 1]); Assert.Contains("Buchhalter", ask[ask.IndexOf("--append-system-prompt") + 1]);
        Assert.Contains("Edit Write NotebookEdit MultiEdit", ask);
        Assert.Contains("--resume", ClaudeCode.Args("weiter", ClaudeCode.Phase.Ask, "s-2")); // Unterhaltung fortsetzen
    }

    [Fact(DisplayName = "T-0316: Live-Ausgabe – Sitzung, Text, Datei lesen/ändern mit Vorher/Nachher (Pfad relativ), Aufgabenliste, Ergebnis; Unbekanntes wird übergangen")]
    public void Parse_stream()
    {
        var root = @"C:\Projekte\seite";
        List<ClaudeCode.Ev> P(string l) => ClaudeCode.Parse(l, root).ToList();
        Assert.Equal(new ClaudeCode.Init("s-1", "claude-sonnet-5-5"), Assert.Single(P("""{"type":"system","subtype":"init","session_id":"s-1","model":"claude-sonnet-5-5"}""")));
        Assert.Equal("Ich schaue mir die Datei an.", ((ClaudeCode.Say)Assert.Single(P("""{"type":"assistant","message":{"content":[{"type":"text","text":"Ich schaue mir die Datei an."}]}}"""))).Text);
        var read = (ClaudeCode.Tool)Assert.Single(P("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Read","input":{"file_path":"C:\\Projekte\\seite\\css\\stil.css"}}]}}"""));
        Assert.Equal(("liest css/stil.css", false), (read.Text, read.Changes));
        var edit = (ClaudeCode.Tool)Assert.Single(P("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Edit","input":{"file_path":"C:\\Projekte\\seite\\index.html","old_string":"<h1>","new_string":"<h1 style=\"color:#1d4ed8\">"}}]}}"""));
        Assert.Equal(("index.html", "<h1>", "<h1 style=\"color:#1d4ed8\">", true), (edit.File, edit.Before, edit.After, edit.Changes));
        var todo = (ClaudeCode.Todo)Assert.Single(P("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"TodoWrite","input":{"todos":[{"content":"Farbe wählen","status":"completed"},{"content":"Überschrift ändern","status":"in_progress"}]}}]}}"""));
        Assert.Equal([("Farbe wählen", "completed"), ("Überschrift ändern", "in_progress")], todo.Items);
        Assert.False(((ClaudeCode.ToolDone)Assert.Single(P("""{"type":"user","message":{"content":[{"type":"tool_result","content":"ok"}]}}"""))).Error);
        var done = (ClaudeCode.Done)Assert.Single(P("""{"type":"result","subtype":"success","is_error":false,"result":"Fertig.","session_id":"s-1","num_turns":4}"""));
        Assert.Equal((true, "Fertig.", 4), (done.Ok, done.Text, done.Turns));
        Assert.Empty(P("""{"type":"rate_limit_event"}""")); Assert.Empty(P("kein json")); Assert.Empty(P("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"ToolSearch","input":{}}]}}"""));
        Assert.Equal("schreibt geheim.txt", ((ClaudeCode.Tool)P("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Write","input":{"file_path":"D:\\anders\\geheim.txt","content":"x"}}]}}""")[0]).Text); // außerhalb: nur Dateiname
    }

    [Fact(DisplayName = "T-0316: Plan aus der Antwort – nummerierte Schritte, Fettdruck entfernt, Ja/Nein-Rückfrage am Ende erkannt")]
    public void Plan_of_answer()
    {
        var (steps, q) = ClaudeCode.PlanOf("Hier mein Plan:\n1. **Farbe** festlegen\n2) Überschrift in index.html anpassen\n- Vorschau prüfen\n\nSoll das Blau auch für Links gelten?");
        Assert.Equal(["Farbe festlegen", "Überschrift in index.html anpassen", "Vorschau prüfen"], steps);
        Assert.Equal("Soll das Blau auch für Links gelten?", q);
        Assert.Null(ClaudeCode.PlanOf("1. Nur ein Schritt").Question);
    }

    [Fact(DisplayName = "T-0316: Sicherung vor dem Lauf – Änderungen erkennen (neu, geändert, gelöscht) und alles zurückholen; node_modules bleibt außen vor")]
    public void Snapshot_restore()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-snap-" + Guid.NewGuid().ToString("N")); var store = root + "-sicherung";
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "css")); Directory.CreateDirectory(Path.Combine(root, "node_modules", "x"));
            File.WriteAllText(Path.Combine(root, "index.html"), "<h1>Hallo</h1>"); File.WriteAllText(Path.Combine(root, "css", "stil.css"), "h1{}");
            File.WriteAllText(Path.Combine(root, "node_modules", "x", "a.js"), "1");
            var snap = FolderSnapshot.Take(root, store);
            Assert.False(File.Exists(Path.Combine(store, "node_modules", "x", "a.js")));
            File.WriteAllText(Path.Combine(root, "index.html"), "<h1 style='color:blue'>Hallo</h1>"); File.Delete(Path.Combine(root, "css", "stil.css")); File.WriteAllText(Path.Combine(root, "neu.js"), "2");
            Assert.Equal([("css/stil.css", "gelöscht"), ("index.html", "geändert"), ("neu.js", "neu")], snap.Changes());
            Assert.Equal("<h1>Hallo</h1>", snap.Original("index.html"));
            snap.Restore();
            Assert.Empty(snap.Changes());
            Assert.Equal("<h1>Hallo</h1>", File.ReadAllText(Path.Combine(root, "index.html"))); Assert.True(File.Exists(Path.Combine(root, "css", "stil.css"))); Assert.False(File.Exists(Path.Combine(root, "neu.js")));
        }
        finally { foreach (var d in new[] { root, store }) if (Directory.Exists(d)) Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Claude im Studio-Terminal: API-Variablen leer (nur Abo), Abo-Token nur wenn gültig, kein API-Schlüssel; Auftrag als eine Zeile")]
    public void Terminal_environment()
    {
        var e = ClaudeCode.TerminalEnvironment(null);
        Assert.All(ClaudeCode.ApiVariables, v => Assert.Equal("", e[v]));
        Assert.Equal("", e[ClaudeCode.TokenVariable]);
        var tok = "sk-ant-oat01-" + new string('a', 30);
        Assert.Equal(tok, ClaudeCode.TerminalEnvironment(tok)[ClaudeCode.TokenVariable]);
        Assert.Equal("", ClaudeCode.TerminalEnvironment("sk-ant-api03-xyz")[ClaudeCode.TokenVariable]);
        Assert.Equal("Baue eine Seite mit Kontakt und Preisen", ClaudeCode.OneLine("  Baue eine Seite\r\n  mit Kontakt\nund Preisen  "));
    }
}
