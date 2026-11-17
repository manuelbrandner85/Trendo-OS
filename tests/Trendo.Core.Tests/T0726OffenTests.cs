using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trendo.Core;
using Trendo.Core.Karten;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0726 (offene Probleme, Gruppe A) – Kern: Prüfprotokoll mit Schlüssel (HMAC), Code-Entwürfe mit Fenstersperre, GitHub-Upload-Beschreibung,
/// Zustimmung zu Kartenkacheln, Lernkarte zurückholen.</summary>
public class T0726Offen
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0726-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    /// <summary>Dauerhafter Speicher wie die Anmeldeinformationsverwaltung, aber nur im Test (nichts landet im echten Tresor).</summary>
    sealed class Tresor : ISecretStore
    {
        readonly Dictionary<string, string> _d = new();
        public string BackendName => "Test-Tresor (dauerhaft)";
        public void Set(string key, string value) => _d[key] = value;
        public string? Get(string key) => _d.TryGetValue(key, out var v) ? v : null;
        public bool Delete(string key) => _d.Remove(key);
    }

    static string Hash(long seq, DateTimeOffset at, string actor, string action, string detail, string prev) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{seq}|{at:O}|{actor}|{action}|{detail}|{prev}")));

    /// <summary>Wie ein Angreifer, der nur den Datenordner beschreiben kann: Eintrag mit korrekter Hash-Kette anhängen (ohne Schlüssel).</summary>
    static void Forge(string file, string action, string detail = "x")
    {
        var last = JsonSerializer.Deserialize<AuditEntry>(File.ReadLines(file).Last(l => l.Length > 0))!;
        var at = DateTimeOffset.Now; var seq = last.Seq + 1;
        var e = new AuditEntry(seq, at, "owner", action, detail, last.Hash, Hash(seq, at, "owner", action, detail, last.Hash));
        File.AppendAllText(file, JsonSerializer.Serialize(e) + "\n");
    }

    [Fact(DisplayName = "T-0726 A3: Prüfprotokoll mit Schlüssel – ohne Schlüssel angehängter Eintrag (korrekte Hash-Kette) wird erkannt; Neustart mit demselben Tresor prüft weiter")]
    public void Audit_hmac_detects_forged_append()
    {
        var dir = Tmp();
        try
        {
            var tresor = new Tresor();
            var core = new TrendoCore(dir, tresor, new FakeLauncher());
            core.Audit.Append("owner", "test.eins", "a"); core.Audit.Append("owner", "test.zwei", "b");
            Assert.True(core.Audit.Keyed);
            Assert.True(core.Audit.Verify());
            var lines = File.ReadAllLines(core.Audit.FilePath);
            Assert.All(lines, l => Assert.Contains("\"Mac\":", l));
            Assert.DoesNotContain(Convert.ToBase64String(AuditKey.LoadOrCreate(tresor)!.Key), File.ReadAllText(core.Audit.FilePath)); // Schlüssel nie im Protokoll
            Forge(core.Audit.FilePath, "approval.approved");
            Assert.False(core.Audit.Verify());
            Assert.Equal(lines.Length + 1, core.Audit.FirstBroken());
            // Neustart (gleicher Tresor): der Abschnitt ohne Signatur wird mit einem signierten Eintrag sichtbar festgehalten (statt für immer „beschädigt“)
            // und bleibt als „ohne Signatur“ erkennbar; das Freigabe-Tor traut ihm nicht (Trusted)
            var again = new TrendoCore(dir, tresor, new FakeLauncher()).Audit;
            Assert.True(again.Verify());
            var c = again.Check();
            Assert.Equal([(long)lines.Length + 1], c.Unsigned); Assert.Equal(1, c.Acknowledged);
            Assert.Contains(again.ReadAll(), e => e.Action == AuditLog.UnsignedAck && e.Detail.StartsWith($"{lines.Length + 1}-") && e.Mac is not null);
            Assert.DoesNotContain(again.Trusted(), e => e.Action == "approval.approved");
            // eine Signatur fälschen (Kid des echten Schlüssels, falscher HMAC) wird nie festgehalten: bleibt gebrochen, auch nach Neustart
            var last = again.ReadAll()[^1]; var at = DateTimeOffset.Now; var h = Hash(last.Seq + 1, at, "owner", "x", "y", last.Hash);
            File.AppendAllText(again.FilePath, JsonSerializer.Serialize(new AuditEntry(last.Seq + 1, at, "owner", "x", "y", last.Hash, h, new string('A', 64), last.Kid)) + "\n");
            Assert.Equal(last.Seq + 1, new TrendoCore(dir, tresor, new FakeLauncher()).Audit.FirstBroken());
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0726 A3: ganze Kette neu berechnet (Eintrag in der Mitte geändert) → erkannt; fremde Schlüsselkennung → erkannt")]
    public void Audit_hmac_detects_rewritten_chain()
    {
        var dir = Tmp();
        try
        {
            var core = new TrendoCore(dir, new Tresor(), new FakeLauncher());
            for (var i = 0; i < 5; i++) core.Audit.Append("owner", "test", "n" + i);
            var f = core.Audit.FilePath;
            var all = core.Audit.ReadAll().ToList();
            // Eintrag 3 ändern und alle Hashes danach neu berechnen – die Kette stimmt wieder, die Signaturen nicht
            var prev = all[1].Hash; var outLines = new List<string> { JsonSerializer.Serialize(all[0]), JsonSerializer.Serialize(all[1]) };
            for (var i = 2; i < all.Count; i++)
            {
                var e = all[i]; var detail = i == 2 ? "geändert" : e.Detail;
                var h = Hash(e.Seq, e.At, e.Actor, e.Action, detail, prev);
                outLines.Add(JsonSerializer.Serialize(e with { Detail = detail, PrevHash = prev, Hash = h })); prev = h;
            }
            File.WriteAllText(f, string.Join("\n", outLines) + "\n");
            Assert.Equal(3, core.Audit.FirstBroken());
            // fremde Schlüsselkennung mit erfundenem HMAC
            File.WriteAllText(f, string.Join("\n", all.Select(e => JsonSerializer.Serialize(e))) + "\n");
            Assert.True(core.Audit.Verify());
            var last = all[^1]; var at = DateTimeOffset.Now; var h2 = Hash(last.Seq + 1, at, "owner", "x", "y", last.Hash);
            File.AppendAllText(f, JsonSerializer.Serialize(new AuditEntry(last.Seq + 1, at, "owner", "x", "y", last.Hash, h2, new string('A', 64), "0000000000000000")) + "\n");
            Assert.False(core.Audit.Verify());
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0726 A3: alte Protokolle ohne Schlüssel bleiben gültig und lesbar; ab der Schlüssel-Anlage wird signiert; ohne dauerhaften Tresor wie bisher (nur Hash-Kette)")]
    public void Audit_old_logs_stay_valid()
    {
        var dir = Tmp();
        try
        {
            var path = Path.Combine(dir, "audit", "audit.jsonl");
            var old = new AuditLog(path); // wie ein Protokoll aus der Zeit vor T-0726
            old.Append("core", "core.start", "alt"); old.Append("owner", "test", "alt");
            Thread.Sleep(20);
            var core = new TrendoCore(dir, new Tresor(), new FakeLauncher());
            core.Audit.Append("owner", "test", "neu");
            Assert.True(core.Audit.Verify());
            var all = core.Audit.ReadAll();
            Assert.Null(all[0].Mac); Assert.Null(all[1].Mac);
            Assert.All(all.Skip(2), e => Assert.NotNull(e.Mac));
            Assert.DoesNotContain("\"Mac\":null", File.ReadLines(path).First()); // altes Format unverändert geschrieben

            var mem = Tmp();
            try
            {
                var c2 = new TrendoCore(mem, new InMemorySecretStore(), new FakeLauncher());
                Assert.False(c2.Audit.Keyed);
                Assert.True(c2.Audit.Verify());
                Assert.All(c2.Audit.ReadAll(), e => Assert.Null(e.Mac));
            }
            finally { try { Directory.Delete(mem, true); } catch (IOException) { } }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0726 A3: Freigabe-Tor übernimmt nach dem Neustart nur signiert angefragte Anfragen – eine ohne Schlüssel eingeschleuste Anfrage (Protokoll + offen.json) nie")]
    public void Gate_rejects_restore_from_forged_log()
    {
        var dir = Tmp();
        try
        {
            var tresor = new Tresor();
            var core = new TrendoCore(dir, tresor, new FakeLauncher());
            var r = core.Approvals.Request(Principal.LocalOwner, Integrations.OpenUrl, "Seite öffnen", "https://example.org/", resumable: true);
            // eingeschleust: Anfrage im Protokoll (Hash-Kette korrekt, ohne Schlüssel) und in der Ablage
            var target = "https://boese.example/";
            Forge(core.Audit.FilePath, "approval.requested", $"FAKE0001 {Integrations.OpenUrl} {target}");
            var store = Path.Combine(dir, "freigaben", "offen.json");
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(store))!;
            node["Requests"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject { ["Id"] = "FAKE0001", ["Action"] = Integrations.OpenUrl, ["Target"] = target, ["ExpiresAt"] = DateTimeOffset.Now.AddMinutes(5) });
            File.WriteAllText(store, node.ToJsonString());
            var pending = new TrendoCore(dir, tresor, new FakeLauncher()).Approvals.Pending();
            Assert.Contains(pending, p => p.Id == r.Id); // echte, signierte Anfrage
            Assert.DoesNotContain(pending, p => p.Id == "FAKE0001");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0726 A4: schreibgeschützte Sperrdatei der Ablage – Trendo startet trotzdem (Neustart-Schutz fällt nur aus, im Protokoll)")]
    public void Gate_starts_with_readonly_lock()
    {
        var dir = Tmp();
        var sperre = Path.Combine(dir, "freigaben", "offen.json.sperre");
        try
        {
            var c = new TrendoCore(dir, new InMemorySecretStore(), new FakeLauncher());
            c.Approvals.Request(Principal.LocalOwner, Integrations.OpenUrl, "Seite öffnen", "https://example.org/", resumable: true);
            File.SetAttributes(sperre, FileAttributes.ReadOnly);
            var again = new TrendoCore(dir, new InMemorySecretStore(), new FakeLauncher()); // darf nicht werfen
            again.Approvals.Request(Principal.LocalOwner, Integrations.OpenUrl, "Seite öffnen", "https://example.org/2", resumable: true);
            Assert.Contains(again.Audit.ReadAll(), e => e.Action == "approval.store.failed");
        }
        finally { try { File.SetAttributes(sperre, FileAttributes.Normal); Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0726 A2: Code-Entwürfe mit Fenstersperre – zweites Fenster überschreibt/löscht den Entwurf nicht und bietet ihn nicht an; nach Absturz wird er angeboten und übernommen")]
    public void Code_drafts_locked_per_window()
    {
        var dir = Tmp();
        try
        {
            var proj = Path.Combine(dir, "projekt"); Directory.CreateDirectory(proj);
            var file = Path.Combine(proj, "main.py"); File.WriteAllText(file, "print(1)\n");
            var data = Path.Combine(dir, "daten");
            var sperren = Path.Combine(data, "programme", "sperren");
            using var a = new ProgramLocks(sperren); using var b = new ProgramLocks(sperren);
            Assert.Null(CodeDrafts.Save(data, proj, file, "print(2)\n", CodeDrafts.ShaOf(file), locks: a)); // Fenster A tippt
            // Fenster B hat dieselbe Datei unverändert offen – sein Takt darf A's Entwurf nicht löschen
            CodeDrafts.Delete(data, file, b);
            Assert.Equal("print(2)\n", CodeDrafts.Load(data, file)!.Text);
            // B bietet A's Entwurf nicht als „nach einem Absturz“ an
            Assert.Empty(CodeDrafts.All(data, b));
            // B tippt auch – A's Entwurf bleibt, B erfährt, wer ihn hält
            var holder = CodeDrafts.Save(data, proj, file, "print(3)\n", CodeDrafts.ShaOf(file), locks: b);
            Assert.NotNull(holder); Assert.Equal(Environment.ProcessId, holder!.Pid);
            Assert.Equal("print(2)\n", CodeDrafts.Load(data, file)!.Text);
            // A stürzt ab: Sperre verwaist → B bekommt den Entwurf angeboten und kann übernehmen
            a.Abandon();
            Assert.Equal("print(2)\n", Assert.Single(CodeDrafts.All(data, b)).Text);
            Assert.Null(CodeDrafts.Save(data, proj, file, "print(4)\n", CodeDrafts.ShaOf(file), locks: b));
            Assert.Equal("print(4)\n", CodeDrafts.Load(data, file)!.Text);
            CodeDrafts.Delete(data, file, b);
            Assert.Null(CodeDrafts.Load(data, file));
            // ohne Sperre wie bisher
            CodeDrafts.Save(data, proj, file, "x", "");
            Assert.Single(CodeDrafts.All(data));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0726 A2 (zweiter Prozess): Entwurf eines laufenden anderen Prozesses wird nicht angeboten, nach dessen Ende schon")]
    public void Code_drafts_locked_across_processes()
    {
        var dir = Tmp();
        try
        {
            var proj = Path.Combine(dir, "projekt"); Directory.CreateDirectory(proj);
            var file = Path.Combine(proj, "main.py"); File.WriteAllText(file, "print(1)\n");
            var data = Path.Combine(dir, "daten");
            CodeDrafts.Save(data, proj, file, "print(2)\n", CodeDrafts.ShaOf(file));
            var draftFile = Directory.GetFiles(Path.Combine(data, "code-editor", "entwuerfe"), "*.json").Single();
            File.WriteAllText(Path.Combine(data, "ziel.txt"), draftFile);
            using var here = new ProgramLocks(Path.Combine(data, "programme", "sperren"));
            using var p = System.Diagnostics.Process.Start(ProgramCrashTests.Probe("sperre-entwurf", data))!; // zweites „Fenster“ in einem eigenen Prozess hält den Entwurf
            try
            {
                for (var i = 0; i < 300 && !Directory.GetFiles(data, "sperre-*.txt").Any(); i++) Thread.Sleep(50);
                Assert.Equal("gesperrt", File.ReadAllText(Directory.GetFiles(data, "sperre-*.txt").Single()));
                Assert.Empty(CodeDrafts.All(data, here)); // läuft noch: nicht anbieten
                CodeDrafts.Delete(data, file, here); Assert.NotNull(CodeDrafts.Load(data, file)); // und nicht löschen
                p.Kill(); p.WaitForExit();
                Assert.Equal("print(2)\n", Assert.Single(CodeDrafts.All(data, here)).Text); // abgestürzt: anbieten
            }
            finally { if (!p.HasExited) p.Kill(); }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0726 A5: GitHub-Upload – Ziel bindet Repo, Sichtbarkeit und Dateien; .git, .trendo, node_modules, .env, *.key nicht in der Liste; Änderung → anderes Ziel")]
    public void GitHub_upload_description()
    {
        var dir = Tmp();
        try
        {
            File.WriteAllText(Path.Combine(dir, "index.html"), "<h1>Hallo</h1>");
            foreach (var d in new[] { ".git", ".trendo", "node_modules" }) { Directory.CreateDirectory(Path.Combine(dir, d)); File.WriteAllText(Path.Combine(dir, d, "x.txt"), "x"); }
            File.WriteAllText(Path.Combine(dir, ".env"), "GEHEIM=1"); File.WriteAllText(Path.Combine(dir, "server.key"), "k");
            var a = GitHubLink.DescribeUpload(dir, "erika-muster", "seite", true, "Seite");
            Assert.Equal(1, a.Files);
            Assert.StartsWith("github.com/erika-muster/seite#privat#", a.Target);
            Assert.Contains("index.html", a.Preview); Assert.DoesNotContain(".env", a.Preview); Assert.DoesNotContain("server.key", a.Preview);
            Assert.Contains("Privat", a.Text);
            Assert.Contains("eigenen Git-Verlauf", a.Text); // vorhandenes .git: früher gespeicherte Stände gehen mit – ehrlich in der Rückfrage
            File.WriteAllText(Path.Combine(dir, ".trendo-notiz"), "n"); // nur Ordner .trendo sind ausgenommen, eine Datei mit ähnlichem Namen lädt git hoch
            Assert.Contains(".trendo-notiz", GitHubLink.DescribeUpload(dir, "erika-muster", "seite", true, "Seite").Preview);
            File.Delete(Path.Combine(dir, ".trendo-notiz"));
            Directory.Delete(Path.Combine(dir, ".git"), true);
            Assert.DoesNotContain("Git-Verlauf", GitHubLink.DescribeUpload(dir, "erika-muster", "seite", true, "Seite").Text);
            var pub = GitHubLink.DescribeUpload(dir, "erika-muster", "seite", false, "Seite");
            Assert.NotEqual(a.Target, pub.Target); Assert.Contains("Öffentlich", pub.Text);
            File.AppendAllText(Path.Combine(dir, "index.html"), "!");
            Assert.NotEqual(a.Target, GitHubLink.DescribeUpload(dir, "erika-muster", "seite", true, "Seite").Target);
            Assert.Throws<ArgumentException>(() => GitHubLink.DescribeUpload(dir, "erika-muster", "a b", true, "x"));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0726 A6: Zustimmung zu Kartenkacheln – fehlt/unlesbar = nein, Erteilen speichert die Freigabe-Id atomar, Zurücknehmen löscht")]
    public void Tile_consent_store()
    {
        var dir = Tmp();
        try
        {
            Assert.False(TileConsent.Allowed(dir));
            TileConsent.Grant(dir, "abc123");
            Assert.True(TileConsent.Allowed(dir));
            Assert.Contains("abc123", File.ReadAllText(Path.Combine(dir, "karten", "internet-kacheln.json")));
            File.WriteAllText(Path.Combine(dir, "karten", "internet-kacheln.json"), "{kaputt");
            Assert.False(TileConsent.Allowed(dir));
            TileConsent.Grant(dir, "x"); TileConsent.Revoke(dir);
            Assert.False(TileConsent.Allowed(dir));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0726 A7: gelöschte Lernkarte samt Lernstand an alter Stelle zurück; doppeltes Zurückholen legt nichts doppelt an")]
    public void Learn_remove_restore()
    {
        var dir = Tmp();
        try
        {
            var box = new LearnBox(dir);
            box.Add("A", "eins", "1"); var c = box.Add("A", "zwei", "2"); box.Add("A", "drei", "3");
            box.Answer(c.Id, Grade.Falsch);
            var before = JsonSerializer.Serialize(c);
            var gone = box.Remove(c.Id)!.Value;
            Assert.Equal(1, gone.Index); Assert.Equal(2, new LearnBox(dir).Store.Cards.Count);
            box.Restore(gone.Card, gone.Index); box.Restore(gone.Card, gone.Index);
            var again = new LearnBox(dir).Store.Cards;
            Assert.Equal(["eins", "zwei", "drei"], again.Select(x => x.Front));
            Assert.Equal(before, JsonSerializer.Serialize(again[1]));
            Assert.Null(box.Remove("gibt-es-nicht"));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
