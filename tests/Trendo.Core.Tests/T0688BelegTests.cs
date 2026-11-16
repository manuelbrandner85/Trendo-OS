using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Xunit;

/// <summary>T-0688: Belege für reports/T-0688 – nur aktiv mit TRENDO_BELEG_T0688=&lt;Zielordner&gt;, sonst übersprungen (der Volltest wird nicht länger).
/// Je Vorlage mit den echten Werkzeugen dieses PCs: anlegen → bauen → testen → paketieren → Paket aus leerem Ordner starten; Rohlog und SHA-256.
/// Der Claude-Echtlauf braucht zusätzlich TRENDO_CLAUDE_ECHT=1 und ein angemeldetes Claude Code (Abo); er nutzt dieselben Argumente wie Trendo.</summary>
public class T0688_Beleg
{
    static string? Ziel => Environment.GetEnvironmentVariable("TRENDO_BELEG_T0688") is { Length: > 0 } d ? d : null;

    /// <summary>Keine Benutzer- oder Rechnernamen in Belegen.</summary>
    static string Clean(string s)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        s = s.Replace(home, @"C:\Users\<user>", StringComparison.OrdinalIgnoreCase).Replace(home.Replace('\\', '/'), "C:/Users/<user>", StringComparison.OrdinalIgnoreCase);
        if (Environment.UserName.Length > 2) s = s.Replace(Environment.UserName, "<user>", StringComparison.OrdinalIgnoreCase);
        if (Environment.MachineName.Length > 2) s = s.Replace(Environment.MachineName, "<pc>", StringComparison.OrdinalIgnoreCase);
        return s;
    }

    static string Sha(string file) { using var f = File.OpenRead(file); return Convert.ToHexStringLower(SHA256.HashData(f)); }

    [Fact(DisplayName = "T-0688 Beleg: jede Vorlage anlegen → bauen → testen → paketieren → Paket starten (echte Werkzeuge, nur mit TRENDO_BELEG_T0688)")]
    public async Task Vorlagen_Beleg()
    {
        if (Ziel is not { } ziel) { Trendo.Core.Tests.Ueberspringen.Weil("nur für den Beleg (TRENDO_BELEG_T0688 setzen)."); return; }
        Directory.CreateDirectory(ziel);
        var arbeit = Path.Combine(Path.GetTempPath(), "trendo-beleg-0688-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(arbeit);
        var summary = new StringBuilder("| Vorlage | Bauen | Testen | Paket | Größe ZIP | SHA-256 ZIP | Startprobe | Rohlog SHA-256 |\n|---|---|---|---|---|---|---|---|\n");
        var versions = new StringBuilder();
        foreach (var (tool, args) in new[] { ("dotnet", "--version"), ("python", "--version"), ("node", "--version") })
            versions.Append($"- {tool} {args}: {(Cli.Find(tool) is null ? "fehlt" : Cli.Run(tool, [args], null, 30000).Out.Trim() + Cli.Run(tool, [args], null, 30000).Err.Trim())}\n");
        var allOk = true;
        foreach (var t in DevStudio.Templates)
        {
            var log = new StringBuilder();
            void L(string l) { lock (log) log.AppendLine(l); }
            L($"# T-0688 Beleg: Vorlage {t.Id} ({t.Name}) – {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            var dir = DevStudio.Create(arbeit, t.Id, "Beleg " + t.Id);
            var p = DevBuild.Detect(dir);
            L($"Projekt angelegt: {dir}");
            L($"Erkannt: {p.Label}, Programm: {p.Main}, Tests: {p.Tests}");
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) L($"  Datei: {Path.GetRelativePath(dir, f)} ({new FileInfo(f).Length} Bytes, SHA-256 {Sha(f)})");
            string Step(string name, DevBuild.Plan plan, int ms, string? results = null)
            {
                L($"\n## {name}");
                if (plan.Cmd is not { } c) { L(plan.Blocked ?? plan.Note ?? "–"); return plan.Blocked is not null ? "BLOCKIERT" : "entfällt"; }
                L("$ " + c.Display);
                var sw = Stopwatch.StartNew();
                var r = Cli.Run(c.Exe, c.Args, c.Cwd, ms, c.Env, c.Utf8 ? new UTF8Encoding(false) : null);
                L(r.Out.TrimEnd()); if (r.Err.Trim().Length > 0) L("[stderr]\n" + r.Err.TrimEnd());
                L($"Exit {r.Exit} nach {sw.Elapsed.TotalSeconds:0.0} s");
                if (results is not null)
                {
                    var tr = DevBuild.ReadResults(p, results, r.Out + "\n" + r.Err);
                    foreach (var c2 in tr.Cases) L($"  {(c2.Passed ? "BESTANDEN" : c2.Skipped ? "ÜBERSPRUNGEN" : "FEHLGESCHLAGEN")}: {c2.Name}");
                    L("Auswertung: " + tr.Summary);
                    return r.Ok && tr.Failed == 0 && tr.Passed > 0 ? $"✓ {tr.Passed} bestanden" : $"✗ {tr.Summary}";
                }
                return r.Ok ? "✓" : $"✗ Exit {r.Exit}";
            }
            var build = Step("Bauen", DevBuild.Build(p), 600000);
            var res = Path.Combine(arbeit, "ergebnisse-" + t.Id); Directory.CreateDirectory(res);
            var test = Step("Testen", DevBuild.Test(p, res), 600000, res);
            L("\n## Paket bauen und starten");
            var pkg = await DevBuild.PublishAsync(p, L, CancellationToken.None);
            L($"Ergebnis: {pkg.Message} · Art: {pkg.Kind} · Ordner {pkg.Folder} ({pkg.Bytes} Bytes) · ZIP {pkg.Zip} ({pkg.ZipBytes} Bytes) · SHA-256 {pkg.Sha256}");
            L($"Startprobe: {(pkg.ProbeOk ? "BESTANDEN" : "NICHT BESTANDEN")} – {pkg.Probe}");
            var logFile = Path.Combine(ziel, $"vorlage-{t.Id}.log");
            File.WriteAllText(logFile, Clean(log.ToString()), new UTF8Encoding(false));
            summary.Append($"| {t.Name} | {build} | {test} | {(pkg.Ok ? "✓ " + pkg.Kind : "✗ " + pkg.Message)} | {(pkg.Ok ? DevBuild.Size(pkg.ZipBytes) : "–")} | `{(pkg.Ok ? pkg.Sha256 : "–")}` | {(pkg.ProbeOk ? "✓ " : "✗ ")}{Clean(pkg.Probe).Replace("|", "/")} | `{Sha(logFile)}` |\n");
            allOk &= pkg.ProbeOk && test.StartsWith('✓') && !build.StartsWith('✗');
        }
        File.WriteAllText(Path.Combine(ziel, "vorlagen-zusammenfassung.md"), Clean($"# T-0688 – Vorlagen mit echten Werkzeugen\n\nLauf: {DateTime.Now:yyyy-MM-dd HH:mm}, Rechner: Windows {Environment.OSVersion.Version}, {DevBuild.Rid()}\n\n{versions}\n{summary}\nRohlogs: `vorlage-<id>.log` in diesem Ordner. Die Pakete selbst liegen nicht im Repo (Größe); ihre Prüfsummen stehen oben.\n"), new UTF8Encoding(false));
        Assert.True(allOk, "Nicht jede Vorlage lief vollständig durch – siehe Zusammenfassung.");
    }

    [Fact(DisplayName = "T-0688 Beleg: „Mit Claude ändern“ mit dem echten Claude Code im Projektordner (nur mit TRENDO_CLAUDE_ECHT=1 und Abo-Anmeldung)")]
    public async Task Claude_Echtlauf()
    {
        if (Ziel is not { } ziel || Environment.GetEnvironmentVariable("TRENDO_CLAUDE_ECHT") != "1") { Trendo.Core.Tests.Ueberspringen.Weil("nur für den Beleg (TRENDO_BELEG_T0688 und TRENDO_CLAUDE_ECHT=1)."); return; }
        var exe = Cli.Find("claude") ?? throw new InvalidOperationException("Claude Code fehlt.");
        ProcessStartInfo Psi(IEnumerable<string> a, string cwd)
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = cwd };
            foreach (var x in a) psi.ArgumentList.Add(x);
            foreach (var v in ClaudeCode.ApiVariables) psi.Environment.Remove(v); // nur das Abo, nie ein API-Schlüssel (wie Trendo)
            return psi;
        }
        // Anmeldestand über denselben Weg wie Trendo (claude auth status --json) – nie anmelden
        using (var sp = Process.Start(Psi(["auth", "status", "--json"], Path.GetTempPath()))!)
        {
            var json = await sp.StandardOutput.ReadToEndAsync(); await sp.WaitForExitAsync();
            var st = ClaudeCode.ParseStatus(exe, json);
            if (!st.Ready) { File.WriteAllText(Path.Combine(ziel, "claude-echtlauf.md"), "# Claude-Echtlauf\n\nNICHT GETESTET: Claude Code ist auf diesem PC nicht mit dem Abo angemeldet.\n"); Trendo.Core.Tests.Ueberspringen.Weil("Claude nicht angemeldet."); return; }
        }
        var dir = DevStudio.Create(Path.Combine(Path.GetTempPath(), "trendo-beleg-0688-claude-" + Guid.NewGuid().ToString("N")[..8]), "python", "Claude Beleg");
        var store = Path.Combine(Path.GetTempPath(), "trendo-beleg-0688-sicherung-" + Guid.NewGuid().ToString("N")[..8]);
        var before = CodeAiKit.Take(dir); var backup = FolderSnapshot.Take(dir, store);
        var prompt = "Wunsch: Die Funktion median in rechner.py soll bei einer leeren Liste die Meldung \"Die Liste ist leer.\" ausgeben statt der bisherigen Meldung.\nGeöffnet ist die Datei „rechner.py“.\n"
            + "Ändere die Dateien direkt in diesem Projektordner (Werkzeuge Edit oder Write). Ändere nur, was für den Wunsch nötig ist, halte den Stil des Projekts ein und führe keine Programme aus. Fasse am Ende in höchstens zwei Sätzen auf Deutsch zusammen, was du geändert hast.";
        var args = ClaudeCode.Args(prompt, ClaudeCode.Phase.Chat, null, "Du arbeitest im Studio „Code mit KI“ von Trendo OS. Arbeitsordner ist der Projektordner der Person.", null, ClaudeCode.Level.Sicher);
        var log = new StringBuilder($"# T-0688 Claude-Echtlauf {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nArbeitsordner (= Projektordner): {dir}\nArgumente: {string.Join(" ", args.Select(a => a.Length > 80 ? a[..80] + "…" : a))}\n\n");
        var sw = Stopwatch.StartNew(); var say = new StringBuilder(); bool? ok = null;
        using (var p = Process.Start(Psi(args, dir))!)
        {
            p.StandardInput.Close();
            while (await p.StandardOutput.ReadLineAsync() is { } line)
                foreach (var ev in ClaudeCode.Parse(line, dir))
                    switch (ev)
                    {
                        case ClaudeCode.Say s: say.AppendLine(s.Text); break;
                        case ClaudeCode.Tool tl: log.AppendLine($"Werkzeug: {tl.Name} {tl.File}"); break;
                        case ClaudeCode.Done dn: ok = dn.Ok; log.AppendLine($"Fertig: ok={dn.Ok}, Runden {dn.Turns}"); break;
                    }
            await p.WaitForExitAsync();
            log.AppendLine($"Exit {p.ExitCode} nach {sw.Elapsed.TotalSeconds:0} s");
        }
        log.AppendLine("\nAntwort von Claude:\n" + say.ToString().Trim());
        var changes = CodeAiKit.Changed(before, CodeAiKit.Take(dir));
        log.AppendLine($"\nGeänderte Dateien laut Trendo-Vergleich: {string.Join(", ", changes.Select(c => $"{c.Path} ({c.Kind})"))}");
        foreach (var c in changes.Where(c => c.Kind == CodeAiKit.ChangeKind.Geaendert))
            log.AppendLine($"\n```diff\n{CodeAiKit.Unified(CodeAiKit.Diff(before.Files.First(f => f.Path == c.Path).Text, File.ReadAllText(Path.Combine(dir, c.Path))), c.Path)}```");
        // gleicher Test wie im Projekt: läuft er noch?
        var t = DevBuild.Test(DevBuild.Detect(dir)).Cmd!; var tr = Cli.Run(t.Exe, t.Args, t.Cwd, 60000, t.Env);
        log.AppendLine($"\nTests nach der Änderung: Exit {tr.Exit} – {DevBuild.ParseUnittest(tr.Out + tr.Err, dir).Summary}");
        var changedText = File.ReadAllText(Path.Combine(dir, "rechner.py"));
        // Zurücknehmen über die Sicherung (wie „Alles zurücknehmen“)
        backup.Restore();
        var after = CodeAiKit.Changed(before, CodeAiKit.Take(dir));
        log.AppendLine($"Nach „Alles zurücknehmen“: {after.Count} Abweichungen vom Stand vor Claude.");
        File.WriteAllText(Path.Combine(ziel, "claude-echtlauf.log"), Clean(log.ToString()), new UTF8Encoding(false));
        Assert.True(ok, "Claude meldete keinen erfolgreichen Abschluss.");
        Assert.Contains("Die Liste ist leer.", changedText);
        Assert.Contains(changes, c => c.Path == "rechner.py");
        Assert.Empty(after);
    }
}
