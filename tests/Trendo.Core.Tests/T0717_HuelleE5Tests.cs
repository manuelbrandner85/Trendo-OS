using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0717 (B6 „Programm-Hülle für alle“, Etappe E5 KI &amp; Wissen): Rückgängig der Hülle für sofort speichernde Programme (<see cref="ItemUndo"/>),
/// Entwurf „neuer Ablauf“ an der Notsicherung, atomares Schreiben von Recherche-Markierungen, Forensik-Fällen, Projekt-JSON, Produktion; echte Abbruchtests.</summary>
public class T0717_HuelleE5Tests(ITestOutputHelper output)
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0717-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0717 ItemUndo: Rückgängig und Wiederholen über mehrere Elemente liefern genau die zu übernehmenden Stände – auch Wiederholen nach mehreren Schritten zurück")]
    public void Item_undo_redo_many_items()
    {
        var u = new ItemUndo("LINK-AUFGABEN", "aufgaben");
        var store = new Dictionary<string, string?> { ["a"] = "a0", ["b"] = "b0" };
        void Change(string k, string? v) { var before = store.GetValueOrDefault(k); store[k] = v; u.Record("ändern " + k, k, before is null ? null : JsonValue.Create(before), v is null ? null : JsonValue.Create(v)); }
        void Apply(IReadOnlyList<(string Key, JsonNode? Value)>? l) { Assert.NotNull(l); foreach (var (k, v) in l!) store[k] = v?.GetValue<string>(); }
        string S() => string.Join(",", store.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + (kv.Value ?? "-")));

        Change("a", "a1"); Change("b", "b1"); Change("c", "c1"); Change("a", null); // neu angelegt (c) und gelöscht (a) zählen mit
        var states = new[] { "a=a0,b=b0", "a=a1,b=b0", "a=a1,b=b1", "a=a1,b=b1,c=c1", "a=-,b=b1,c=c1" };
        Assert.Equal(states[4], S());
        for (var i = 3; i >= 0; i--) { Apply(u.Undo()); Assert.Equal(states[i], S().Replace(",c=-", "")); }
        Assert.Null(u.Undo()); Assert.False(u.CanUndo);
        for (var i = 1; i <= 4; i++) { Apply(u.Redo()); Assert.Equal(states[i], S().Replace(",c=-", "")); } // Wiederholen bringt jeden Zwischenstand
        Assert.Null(u.Redo());
        Assert.False(u.Link.Dirty); // alles ist schon gespeichert – keine Notsicherung
        Assert.Null(u.Link.Autosave(Path.Combine(Tmp(), "sicherung")));

        // ein anderes Element, das außerhalb geändert wurde, bleibt beim Rückgängig unberührt
        Apply(u.Undo()); store["x"] = "fremd"; Apply(u.Undo()); Assert.Equal("fremd", store["x"]);
        // nach Rückgängig eine neue Änderung: Wiederholen ist leer, Rückgängig nimmt genau sie zurück
        Change("b", "b9"); Assert.False(u.CanRedo); Apply(u.Undo()); Assert.Equal("b1", store["b"]);
    }

    [Fact(DisplayName = "T-0717 ItemUndo: 100 Schritte wie die Hülle (vorher Aufgaben/Vorhaben ohne Grenze, Kalender 50, Abläufe 50)")]
    public void Item_undo_depth_100()
    {
        var u = new ItemUndo("LINK-KALENDER", "kalender"); var v = 0;
        for (var i = 1; i <= 120; i++) { u.Record("Schritt", "t", JsonValue.Create(v), JsonValue.Create(i)); v = i; }
        var n = 0; while (u.Undo() is { } l) { n++; v = l.Single().Value!.GetValue<int>(); }
        Assert.Equal(ProgramDocument.MaxUndo, n); Assert.Equal(20, v);
    }

    /// <summary>Gemeinsamer Ablauf: Hilfsprogramm hart beenden, danach prüfen.</summary>
    void KillLoop(string mode, Func<string, int> read, int rounds = 10)
    {
        var dir = Tmp(); var rnd = new Random(717); int during = 0, worst = 0;
        try
        {
            for (var round = 1; round <= 3 * rounds && (round <= rounds || during < rounds / 2); round++) // unter Last weiter abbrechen, bis genug mitten im Schreiben lagen (wie T0708)
            {
                if (ProgramCrashTests.RunAndKill(mode, dir, 2, rnd)) during++;
                var (started, done, _) = ProgramCrashTests.ReadLog(dir);
                var n = read(dir);
                Assert.True(n >= done, $"Runde {round}: gespeichert ist Stand {n}, fertig war {done}");
                Assert.True(n <= started, $"Runde {round}: Stand {n} > begonnen {started}");
                worst = Math.Max(worst, started - n);
            }
            Assert.True(worst <= 1, "Mehr als der eine laufende Schritt ging verloren: " + worst);
            Assert.True(during >= rounds / 2, $"Nur {during} von {rounds} Abbrüchen fielen in einen laufenden Schreibvorgang – der Test taugt so nicht.");
            output.WriteLine($"{mode}: {rounds} harte Abbrüche, {during} mitten im Schreiben, höchstens {worst} Schritt verloren.");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0717 Absturz: Abläufe – neuer Ablauf an der Notsicherung der Hülle (mit Sperre), 10-mal hart beendet, Sicherung öffnet sich jedes Mal, höchstens der laufende Schritt fehlt")]
    public void Kill_while_autosaving_workflow_draft() => KillLoop("link-ablauf", dir =>
    {
        var bak = Path.Combine(dir, "sicherung");
        Assert.Empty(ProgramDocument.OrphanScan(bak, "LINK-ABLAEUFE").Broken); // nie eine halbe Sicherung
        using var locks = new ProgramLocks(Path.Combine(dir, "sperren"));
        var link = new ProgramLink("LINK-ABLAEUFE", "ablaeufe-neu") { Locks = locks };
        var pending = link.Pending(bak); // die Sperre des abgestürzten Prozesses gilt als verwaist
        Assert.Single(pending);
        var st = link.Restore(pending[0].File, bak)!;
        Assert.Equal(40_000, JsonNode.Parse(st["steps"]!.GetValue<string>())!.AsArray().Count); // Schritte vollständig
        return int.Parse(st["name"]!.GetValue<string>()[7..], System.Globalization.CultureInfo.InvariantCulture);
    });

    [Fact(DisplayName = "T-0717 Absturz: Produktion – Tafel wird sofort gespeichert (atomar), 10-mal hart beendet, die Tafel ist jedes Mal vollständig lesbar")]
    public void Kill_while_saving_board() => KillLoop("tafel", dir =>
    {
        var store = new ProjectStore(Path.Combine(dir, "projekte"), new AuditLog(Path.Combine(dir, "audit-test.jsonl")));
        var f = store.ResolveInProject("hofladen", BigBoards.File);
        System.Text.Json.JsonDocument.Parse(File.ReadAllText(f)).Dispose(); // gültiges JSON – vorher konnte eine halbe Datei bleiben (Trendo zeigte dann eine leere Tafel)
        var b = BigBoards.Load(store, "hofladen");
        Assert.Equal(4000, b.Cards.Count);
        Assert.StartsWith("Stand ", b.Goal);
        return int.Parse(b.Goal[6..], System.Globalization.CultureInfo.InvariantCulture);
    });

    [Fact(DisplayName = "T-0717 Atomar: Recherche-Markierungen, Forensik-Fall und Bericht, Projekt-JSON und Projektdateien, Produktion; Forensik-Frist bleibt höchstens 120 h; alte Dateien bleiben lesbar")]
    public void E5_stores_atomic_and_old_files()
    {
        var dir = Tmp();
        try
        {
            var audit = new AuditLog(Path.Combine(dir, "audit.jsonl"));
            // Recherche: alte markierungen.json (Format vor T-0717) lesbar, Markieren/Entfernen schreibt atomar
            var desk = new ResearchDesk(dir, audit);
            var marks = Path.Combine(dir, "tmp", "recherche-sitzung", "markierungen.json");
            File.WriteAllText(marks, "[{\"Id\":\"ab12cd34\",\"Url\":\"https://example.org/a\",\"Title\":\"A\",\"Start\":0,\"Length\":5,\"Text\":\"Hallo\",\"Color\":\"gelb\",\"Note\":\"\",\"At\":\"2026-11-10T09:30:00+01:00\"}]");
            Assert.Equal("Hallo", Assert.Single(desk.Highlights()).Text);
            desk.AddHighlight("https://example.org/a", "A", "Hallo Welt, schöner Tag", 6, 4, "grün", "wichtig");
            Assert.Equal(2, desk.Highlights().Count);
            desk.RemoveHighlight("ab12cd34"); Assert.Equal("Welt", Assert.Single(desk.Highlights()).Text);

            // Forensik: Fall, Beweis, Bericht – atomar; Frist unverändert und nie über 120 h; keine Zwischendatei bleibt
            var now = new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.FromHours(1));
            var time = new FixedTime(now);
            var lab = new ForensicsLab(dir, audit, time);
            Assert.Throws<ArgumentOutOfRangeException>(() => lab.Create("zu lang", ForensicsLab.MaxLife + TimeSpan.FromMinutes(1)));
            var c = lab.Create("Betrugs-Mail März");
            Assert.Equal(now + ForensicsLab.MaxLife, c.Expires);
            var src = Path.Combine(dir, "mail.eml"); File.WriteAllText(src, "From: test@example.org\r\nSubject: Gewinn\r\n\r\nText");
            var it = lab.Add(c, src);
            var copy = Path.Combine(dir, "forensik", c.Id, "beweise", it.Name);
            Assert.True(File.GetAttributes(copy).HasFlag(FileAttributes.ReadOnly));
            Assert.Equal(ForensicsLab.Sha256(src), it.Sha256);
            var rep = lab.Report(c); Assert.Contains("Betrugs-Mail März", File.ReadAllText(rep));
            var again = Assert.Single(new ForensicsLab(dir, audit, time).Cases());
            Assert.Equal(c.Expires, again.Expires); Assert.Single(again.Items); // Speichern verlängert die Frist nicht
            Assert.Empty(Directory.GetFiles(Path.Combine(dir, "forensik"), "*.tmp", SearchOption.AllDirectories));
            Assert.Empty(Directory.GetFiles(Path.Combine(dir, "forensik"), "*.teil", SearchOption.AllDirectories));
            time.Now = now + ForensicsLab.MaxLife; // nach 120 h: Fall wird vollständig gelöscht (Regel aus CLAUDE.md unverändert)
            Assert.Equal(1, lab.PurgeExpired()); Assert.Empty(lab.Cases());

            // Forensik: fall.json im Format vor T-0717 (File.WriteAllText, gleiche Felder) bleibt lesbar
            var old = Path.Combine(dir, "forensik", "20261110-093000-ab12"); Directory.CreateDirectory(Path.Combine(old, "beweise"));
            File.WriteAllText(Path.Combine(old, "fall.json"), "{\"Id\":\"20261110-093000-ab12\",\"Title\":\"Alt\",\"Created\":\"2026-11-15T09:30:00+01:00\",\"Expires\":\"2026-11-20T09:30:00+01:00\",\"Items\":[]}");
            Assert.Equal("Alt", Assert.Single(new ForensicsLab(dir, audit, new FixedTime(now)).Cases()).Title);

            // Projekte: project.json im alten Format lesbar; Anlegen, Datei, Notiz atomar; Tafel der Produktion
            var projects = new ProjectStore(Path.Combine(dir, "projekte"), audit);
            Directory.CreateDirectory(Path.Combine(dir, "projekte", "alt", "files"));
            File.WriteAllText(Path.Combine(dir, "projekte", "alt", "project.json"), "{\"Id\":\"alt\",\"Name\":\"Altes Projekt\",\"CreatedAt\":\"2026-11-01T08:00:00+01:00\"}");
            Assert.Equal("Altes Projekt", Assert.Single(projects.List()).Name);
            var (p, _) = BigBoards.Create(projects, "Hofladen online", "Bis Weihnachten verkaufen");
            var board = BigBoards.Load(projects, p.Id); board.Cards.Add(new BoardCard { Title = "Fotos" }); BigBoards.Save(projects, p.Id, board);
            Assert.Equal("Fotos", Assert.Single(BigBoards.Load(projects, p.Id).Cards).Title);
            projects.WriteBytes(p.Id, "bild.bin", [1, 2, 3]); Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(projects.ResolveInProject(p.Id, "bild.bin")));
            Assert.Empty(Directory.GetFiles(dir, "*.tmp", SearchOption.AllDirectories));
        }
        finally
        {
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, true);
        }

        // Gegenprobe im Quelltext: an diesen Speicherorten kein direktes File.Write* mehr (vor T-0717 so – nach einem Abbruch konnte eine halbe Datei bleiben)
        var root = new DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !File.Exists(Path.Combine(root.FullName, "Trendo.sln"))) root = root.Parent;
        Assert.NotNull(root);
        string Src(string rel) => File.ReadAllText(Path.Combine(root!.FullName, "src", rel));
        foreach (var (file, bad) in new[]
        {
            ("Trendo.Core/W3/ResearchMarks.cs", "File.WriteAllText(MarksFile"),
            ("Trendo.Core/W3/Research.cs", "File.WriteAllText(f, sb"),
            ("Trendo.Core/W3/Forensics.cs", "File.WriteAllText(Meta"),
            ("Trendo.Core/W3/Forensics.cs", "File.WriteAllLines(f"),
            ("Trendo.Core/ProjectStore.cs", "File.WriteAllText("),
            ("Trendo.Core/ProjectStore.cs", "File.WriteAllBytes("),
            ("Trendo.Core/ProjectFiles.cs", "File.WriteAllText("),
            ("Trendo.Core/Fusion.cs", "File.WriteAllText("),
            ("Trendo.Core/W7/Fusion.cs", "File.WriteAllText(NodesFile"),
            ("Trendo.Core/W2/Agents.cs", "File.WriteAllText(CustomFile"),
            ("Trendo.App/MainWindow.Spec06.cs", "File.WriteAllText(f, Learn"),
            ("Trendo.App/MainWindow.ChatWindow.cs", "File.WriteAllText(f, ChatStore"),
        })
            Assert.False(System.Text.RegularExpressions.Regex.IsMatch(Src(file), @"(?<!Atomic)" + System.Text.RegularExpressions.Regex.Escape(bad)), $"{file}: noch „{bad}“");
    }

    sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("t", TimeSpan.FromHours(1), "t", "t");
    }
}
