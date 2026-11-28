using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0785: Blender-Verbindung mit Ersatz-MCP-Brücke (spricht das Protokoll des Blender-Lab-Add-ons: JSON + Null-Byte über TCP) und nachgebildetem
/// Blender-Prozess. ERSETZT: Blender selbst und das Add-on (die Statusdatei des Startskripts schreibt der Test). Der Echt-Test mit dem installierten Blender
/// steht in <see cref="BlenderLinkEchtTests"/>.</summary>
public class BlenderLinkTests
{
    /// <summary>Ersatz für die MCP-Brücke im Blender-Add-on.</summary>
    internal sealed class FakeAddon : IDisposable
    {
        readonly TcpListener _l;
        readonly ConcurrentBag<Socket> _clients = [];
        readonly CancellationTokenSource _cts = new();
        public int Pid { get; }
        public volatile string File = "";
        public int Requests, Sleeps;

        public FakeAddon(int port, int pid, string file = "")
        {
            Pid = pid; File = file; _l = new TcpListener(IPAddress.Loopback, port);
            _l.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _l.Start(); _ = Task.Run(Loop);
        }

        async Task Loop()
        {
            while (!_cts.IsCancellationRequested)
            {
                Socket s;
                try { s = await _l.AcceptSocketAsync(_cts.Token); } catch (Exception) { return; }
                _clients.Add(s); _ = Task.Run(() => Serve(s));
            }
        }

        async Task Serve(Socket s)
        {
            try
            {
                var buf = new List<byte>(); var chunk = new byte[4096];
                while (!buf.Contains(0)) { var n = await s.ReceiveAsync(chunk, _cts.Token); if (n == 0) return; buf.AddRange(chunk.Take(n)); }
                var req = JsonNode.Parse(Encoding.UTF8.GetString([.. buf.TakeWhile(b => b != 0)]))!;
                Interlocked.Increment(ref Requests);
                var code = (string)req["code"]!;
                JsonObject res;
                if (code == BlenderBridge.PingCode) res = new() { ["status"] = "ok", ["result"] = new JsonObject { ["pid"] = Pid, ["datei"] = File, ["geaendert"] = false } };
                else if (code.StartsWith("SLEEP:")) { Interlocked.Increment(ref Sleeps); await Task.Delay(int.Parse(code[6..]), _cts.Token); res = new() { ["status"] = "ok", ["result"] = new JsonObject { ["fertig"] = true, ["pid"] = Pid } }; }
                else if (code.Contains("open_mainfile")) { var a = code.IndexOf("filepath=") + 9; var path = JsonNode.Parse(code[a..code.IndexOf(")\n", a)])!.GetValue<string>(); File = path; res = new() { ["status"] = "ok", ["result"] = new JsonObject { ["offen"] = true, ["datei"] = path } }; }
                else res = new() { ["status"] = "ok", ["result"] = new JsonObject { ["echo"] = code, ["pid"] = Pid } };
                await s.SendAsync(Encoding.UTF8.GetBytes(res.ToJsonString() + "\0"));
                s.Shutdown(SocketShutdown.Both);
            }
            catch (Exception) { }
            finally { s.Dispose(); }
        }

        /// <summary>Port zu, offene Verbindungen hart getrennt.</summary>
        public void Dispose()
        {
            _cts.Cancel(); _l.Stop();
            foreach (var c in _clients) { try { c.LingerState = new LingerOption(true, 0); c.Close(); } catch (Exception) { } }
        }
    }

    internal sealed class FakeProc(int id) : IBlenderProc
    {
        public int Id { get; } = id;
        public volatile bool Exited;
        public int Code = -1;
        public bool HasExited => Exited;
        public int? ExitCode => Exited ? Code : null;
    }

    /// <summary>Nachgebildeter Startweg: „Blender“ meldet seine Brücke nach kurzer Zeit auf dem Port (abschaltbar).</summary>
    internal sealed class FakeHost(Func<int> port) : IBlenderHost
    {
        int _next = 41000;
        public readonly List<(FakeProc P, IReadOnlyList<string> Args)> Started = [];
        public readonly Dictionary<int, FakeAddon> Addons = [];
        public List<int> Foreign { get; } = [];
        public bool AutoBridge { get; set; } = true;

        public IBlenderProc Start(string exe, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env)
        {
            var p = new FakeProc(Interlocked.Increment(ref _next));
            lock (Started) Started.Add((p, args));
            var file = args.Count > 0 && args[0].EndsWith(".blend") ? args[0] : "";
            if (AutoBridge) _ = Task.Run(async () => { await Task.Delay(150); Bridge(p, file); });
            return p;
        }

        public void Bridge(FakeProc p, string file = "") { if (!p.Exited) lock (Addons) Addons[p.Id] = new FakeAddon(port(), p.Id, file); }
        public IReadOnlyList<int> BlenderPids() { lock (Started) return [.. Foreign, .. Started.Where(s => !s.P.Exited).Select(s => s.P.Id)]; }
        /// <summary>Absturz (Code −1) bzw. sauberes Schließen (Code 0).</summary>
        public void End(FakeProc p, int code = -1) { p.Code = code; p.Exited = true; lock (Addons) if (Addons.Remove(p.Id, out var a)) a.Dispose(); }
        public FakeProc Last { get { lock (Started) return Started[^1].P; } }
        public FakeAddon AddonOf(FakeProc p) { lock (Addons) return Addons[p.Id]; }
    }

    static int FreePort() => BlenderBridge.FreePort();

    sealed class Rig : IDisposable
    {
        public readonly string Dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-blink-" + Guid.NewGuid().ToString("N"))).FullName;
        public readonly FakeHost Host;
        public readonly BlenderLink Link;
        public readonly List<string> Log = [];
        public string Blend => Path.Combine(Dir, "projekt.blend");
        public string State => Path.Combine(Dir, "zustand");
        public Rig(bool separate = false, int? port = null)
        {
            BlenderLink? link = null;
            Host = new FakeHost(() => link!.Port);
            var exe = Path.Combine(Dir, "blender.exe"); File.WriteAllText(exe, "");
            File.WriteAllText(Blend, "original");
            Link = link = new BlenderLink(exe, Host, port ?? FreePort(), separateInstance: separate, log: (k, t) => { lock (Log) Log.Add(k + ": " + t); }, stateDir: Path.Combine(Dir, "zustand"))
            { Heartbeat = TimeSpan.FromMilliseconds(100), PingTimeout = TimeSpan.FromMilliseconds(400), StartTimeout = TimeSpan.FromSeconds(8), Backoff = _ => TimeSpan.FromMilliseconds(50) };
        }
        public bool Logged(string kind) { lock (Log) return Log.Any(l => l.StartsWith(kind + ":")); }
        public string LastScript => File.ReadAllText(Directory.EnumerateFiles(State, "bruecke-*.py").OrderBy(File.GetLastWriteTimeUtc).Last());
        public void Dispose()
        {
            Link.Dispose();
            lock (Host.Addons) foreach (var a in Host.Addons.Values) a.Dispose();
            try { Directory.Delete(Dir, true); } catch (IOException) { }
        }
        /// <summary>Sicherung so anlegen, wie das Startskript es tut (Begleitdatei mit Quelle, Start, PID).</summary>
        public (string Rec, string Meta) Recovery(string source, int pid, string? start, string content = "gesichert")
        {
            var (rec, meta) = BlenderBridge.RecoveryFor(source);
            File.WriteAllText(rec, content);
            File.WriteAllText(meta, new JsonObject { ["quelle"] = Path.GetFullPath(source), ["start"] = start, ["pid"] = pid }.ToJsonString());
            return (rec, meta);
        }
        public void WriteStatus(bool? ok, string grund, int? port = null) =>
            File.WriteAllText(Link.CurrentStatusFile!, new JsonObject { ["ok"] = ok, ["grund"] = grund, ["port"] = port ?? Link.Port, ["datei"] = "" }.ToJsonString());
    }

    static async Task Until(Func<bool> ok, int ms = 10000)
    {
        for (var t = 0; t < ms && !ok(); t += 25) await Task.Delay(25);
        Assert.True(ok(), "Bedingung nicht erreicht");
    }

    [Fact(DisplayName = "T-0785: Blender startet einmal sichtbar mit Projekt und Brücke – fünf gleichzeitige Anfragen, kein doppelter Start")]
    public async Task Start_once()
    {
        using var r = new Rig();
        var all = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => r.Link.EnsureAsync(r.Dir)));
        Assert.All(all, s => Assert.Equal(BlenderLinkState.Verbunden, s.State));
        Assert.Single(r.Host.Started); Assert.Equal(1, r.Link.Starts);
        var args = r.Host.Started[0].Args;
        Assert.Equal(r.Blend, args[0]); Assert.Equal("--python-expr", args[1]); Assert.DoesNotContain("--background", args);
        Assert.DoesNotContain("--online-mode", args); // M1: Online-Freigabe nie selbst setzen
        Assert.True(all[0].Own); Assert.Equal(r.Host.Last.Id, r.Link.OwnPid);
        Assert.Contains("offen: projekt.blend", all[0].Text);
        Assert.False(Directory.EnumerateFiles(r.Dir, "*.py").Any()); // Skript liegt in Trendos Zustandsordner, nicht im Projekt
    }

    [Fact(DisplayName = "T-0785 M4: schon offenes, verbundenes Blender wird weitergenutzt – kein zweites; die offene Datei wird angezeigt")]
    public async Task Reuse()
    {
        using var r = new Rig();
        var other = Path.Combine(Path.GetTempPath(), "fremd-" + Guid.NewGuid().ToString("N") + ".blend");
        using var open = new FakeAddon(r.Link.Port, 777, other); r.Host.Foreign.Add(777);
        var s = await r.Link.EnsureAsync(r.Dir);
        Assert.Equal(BlenderLinkState.Verbunden, s.State); Assert.False(s.Own); Assert.Equal(777, s.Pid);
        Assert.Contains("deinem offenen Blender", s.Text); Assert.Contains(Path.GetFileName(other), s.Text);
        Assert.False(r.Link.OpenFileInProject); // gehört nicht zum Projekt → die App fragt
        Assert.Empty(r.Host.Started);
        open.File = r.Blend; await r.Link.EnsureAsync(r.Dir); Assert.True(r.Link.OpenFileInProject);
    }

    [Fact(DisplayName = "T-0785: fremdes Blender ohne Brücke – klarer Grund, kein zweites Blender")]
    public async Task Foreign_without_bridge()
    {
        using var r = new Rig();
        r.Host.Foreign.Add(777);
        var s = await r.Link.EnsureAsync(r.Dir);
        Assert.Equal(BlenderLinkState.Getrennt, s.State); Assert.Contains("schon offen", s.Text);
        Assert.Empty(r.Host.Started);
    }

    [Fact(DisplayName = "T-0785: eigene Instanz (eigener Port, eigenes Profil) startet neben einem fremden Blender")]
    public async Task Separate_instance()
    {
        using var r = new Rig(separate: true);
        r.Host.Foreign.Add(777);
        Assert.Equal(BlenderLinkState.Verbunden, (await r.Link.EnsureAsync(r.Dir)).State);
        Assert.Single(r.Host.Started);
        Assert.Contains("ENABLE_ADDON = True", r.LastScript); // eigenes Profil: ohne Rückfrage
    }

    [Fact(DisplayName = "T-0785: Absturz → automatischer Neustart mit derselben Datei, Verbindung wieder da, alles im Protokoll")]
    public async Task Crash_restart()
    {
        using var r = new Rig();
        await r.Link.EnsureAsync(r.Dir);
        using var keep = r.Link.Keep();
        r.Host.End(r.Host.Last);
        await Until(() => r.Link.Starts == 2 && r.Link.Status.State == BlenderLinkState.Verbunden);
        Assert.Equal(r.Blend, r.Host.Started[1].Args[0]);
        Assert.True(r.Logged("abbruch")); Assert.True(r.Logged("neustart")); Assert.True(r.Logged("wieder-verbunden"));
        Assert.Equal("original", File.ReadAllText(r.Blend));
    }

    [Fact(DisplayName = "T-0785 H1a: Absturz mit Sicherung – Sicherung öffnet als EIGENE Datei mit Hinweis, Original bleibt unverändert, Sicherung weg")]
    public async Task H1a_restore_own_file()
    {
        using var r = new Rig();
        await r.Link.EnsureAsync(r.Dir);
        using var keep = r.Link.Keep();
        var p = r.Host.Last;
        var (rec, meta) = r.Recovery(r.Blend, p.Id, r.Link.CurrentStart);
        r.Host.End(p, -1073741819); // Zugriffsverletzung
        await Until(() => r.Link.Starts == 2 && r.Link.Status.State == BlenderLinkState.Verbunden);
        var restored = r.Link.RestoredFile!;
        Assert.Equal(restored, r.Host.Started[1].Args[0]);
        Assert.StartsWith(Path.Combine(r.Dir, "projekt-wiederhergestellt-"), restored);
        Assert.Equal("gesichert", File.ReadAllText(restored));
        Assert.Equal("original", File.ReadAllText(r.Blend)); // nie über die Datei der Person gespeichert
        Assert.False(File.Exists(rec)); Assert.False(File.Exists(meta));
        Assert.Contains("EIGENE Datei", r.LastScript); Assert.Contains("Speichern unter", r.LastScript);
        Assert.True(r.Logged("wiederhergestellt"));
    }

    [Fact(DisplayName = "T-0785 H1b/H2: sauber geschlossen (Code 0) – kein Neustart, „von dir geschlossen“, eigene Sicherung gelöscht; nur „Neu verbinden“ startet (N3)")]
    public async Task H1b_clean_close()
    {
        using var r = new Rig();
        await r.Link.EnsureAsync(r.Dir);
        using var keep = r.Link.Keep();
        var p = r.Host.Last;
        var (rec, meta) = r.Recovery(r.Blend, p.Id, r.Link.CurrentStart);
        r.Host.End(p, 0);
        await Until(() => r.Link.Status.State == BlenderLinkState.Geschlossen);
        await Task.Delay(500);
        Assert.Equal(1, r.Link.Starts); Assert.Contains("geschlossen", r.Link.Status.Text);
        Assert.False(File.Exists(rec)); Assert.False(File.Exists(meta)); Assert.Equal("original", File.ReadAllText(r.Blend));
        Assert.True(r.Logged("geschlossen"));
        Assert.Equal(BlenderLinkState.Geschlossen, (await r.Link.EnsureAsync()).State); Assert.Equal(1, r.Link.Starts);
        Assert.Equal(BlenderLinkState.Verbunden, (await r.Link.ReconnectAsync()).State); Assert.Equal(2, r.Link.Starts);
    }

    [Fact(DisplayName = "T-0785 H1: Sicherung mit fremder PID, anderer Startzeit oder anderer Quelle wird nie übernommen")]
    public async Task H1_wrong_meta_not_restored()
    {
        using var r = new Rig();
        await r.Link.EnsureAsync(r.Dir);
        using var keep = r.Link.Keep();
        var p = r.Host.Last; var start = r.Link.CurrentStart;
        var (rec, _) = r.Recovery(r.Blend, p.Id + 1, start); // andere PID
        r.Host.End(p);
        await Until(() => r.Link.Starts == 2 && r.Link.Status.State == BlenderLinkState.Verbunden);
        Assert.Null(r.Link.RestoredFile); Assert.Equal(r.Blend, r.Host.Started[1].Args[0]); Assert.True(File.Exists(rec));
        Assert.Null(BlenderLink.TakeRecovery(r.Blend, p.Id + 1, "andere-startzeit"));
        Assert.Null(BlenderLink.TakeRecovery(Path.Combine(r.Dir, "anders.blend"), p.Id + 1, start));
        Assert.Equal("original", File.ReadAllText(r.Blend));
    }

    [Fact(DisplayName = "T-0785 H1 gleicher Ordner: zwei .blend nebeneinander – Sicherungen getrennt, nach Dateiwechsel wird nur die neue Datei wiederhergestellt")]
    public async Task H1_same_folder()
    {
        using var r = new Rig();
        var b = Path.Combine(r.Dir, "zweite.blend"); File.WriteAllText(b, "zweite");
        Assert.NotEqual(BlenderBridge.RecoveryFor(r.Blend).Blend, BlenderBridge.RecoveryFor(b).Blend);
        File.SetLastWriteTimeUtc(b, DateTime.UtcNow.AddMinutes(-5));
        await r.Link.EnsureAsync(r.Dir);
        using var keep = r.Link.Keep();
        Assert.Null(await r.Link.OpenFileAsync(b)); // Datei gewechselt
        var p = r.Host.Last;
        var (recA, _) = r.Recovery(r.Blend, p.Id, r.Link.CurrentStart, "A-gesichert");
        var (recB, _) = r.Recovery(b, p.Id, r.Link.CurrentStart, "B-gesichert");
        r.Host.End(p);
        await Until(() => r.Link.Starts == 2 && r.Link.Status.State == BlenderLinkState.Verbunden);
        Assert.StartsWith(Path.Combine(r.Dir, "zweite-wiederhergestellt-"), r.Link.RestoredFile);
        Assert.Equal("B-gesichert", File.ReadAllText(r.Link.RestoredFile!));
        Assert.Equal("original", File.ReadAllText(r.Blend)); Assert.Equal("zweite", File.ReadAllText(b));
        Assert.True(File.Exists(recA)); Assert.False(File.Exists(recB)); // A bleibt unangetastet
        Assert.Contains("'.trendo-wiederherstellung-' + os.path.splitext(n)[0]", BlenderBridge.StartScript(1, "s", "c", false, "t")); // dieselbe Benennung je Quelle
    }

    [Fact(DisplayName = "T-0785 H2: das verbundene Blender der Person wird geschlossen – „von dir geschlossen“, Trendo startet keins")]
    public async Task H2_foreign_closed()
    {
        using var r = new Rig();
        var open = new FakeAddon(r.Link.Port, 777); r.Host.Foreign.Add(777);
        await r.Link.EnsureAsync(r.Dir);
        using var keep = r.Link.Keep();
        open.Dispose(); r.Host.Foreign.Clear();
        await Until(() => r.Link.Status.State == BlenderLinkState.Geschlossen);
        await Task.Delay(400);
        Assert.Empty(r.Host.Started); Assert.True(r.Logged("geschlossen"));
    }

    [Fact(DisplayName = "T-0785: Verbindung getrennt (Prozess lebt) → „verbindet neu“, dann wieder verbunden ohne neuen Prozess")]
    public async Task Drop_reconnect()
    {
        using var r = new Rig();
        await r.Link.EnsureAsync(r.Dir);
        using var keep = r.Link.Keep();
        var p = r.Host.Last; var states = new ConcurrentBag<BlenderLinkState>(); r.Link.Changed += s => states.Add(s.State);
        lock (r.Host.Addons) { r.Host.Addons[p.Id].Dispose(); r.Host.Addons.Remove(p.Id); }
        await Until(() => states.Contains(BlenderLinkState.VerbindetNeu));
        r.Host.Bridge(p);
        await Until(() => r.Link.Status.State == BlenderLinkState.Verbunden);
        Assert.Single(r.Host.Started); Assert.True(r.Logged("abbruch")); Assert.True(r.Logged("wieder-verbunden"));
    }

    [Fact(DisplayName = "T-0785 N1: solange Claude arbeitet (Quiet), schickt der Herzschlag keine Kennungsabfragen")]
    public async Task N1_quiet_no_pings()
    {
        using var r = new Rig();
        await r.Link.EnsureAsync(r.Dir);
        using var keep = r.Link.Keep();
        var p = r.Host.Last;
        FakeAddon a;
        using (r.Link.Quiet())
        {
            lock (r.Host.Addons) { r.Host.Addons[p.Id].Dispose(); r.Host.Addons.Remove(p.Id); }
            await Until(() => r.Link.Status.State == BlenderLinkState.VerbindetNeu);
            r.Host.Bridge(p); a = r.Host.AddonOf(p);
            await Until(() => r.Link.Status.State == BlenderLinkState.Arbeitet);
            await Task.Delay(600);
            Assert.Equal(0, a.Requests);
        }
        await Until(() => r.Link.Status.State == BlenderLinkState.Verbunden); // danach wieder mit Kennungsabfrage
        Assert.True(a.Requests > 0);
    }

    [Fact(DisplayName = "T-0785: langer Lauf (3 s bei 0,4 s Lebenszeichen-Grenze) bricht nicht ab und löst keinen Neustart aus")]
    public async Task Long_run()
    {
        using var r = new Rig();
        await r.Link.EnsureAsync(r.Dir);
        using var keep = r.Link.Keep();
        var states = new ConcurrentBag<BlenderLinkState>(); r.Link.Changed += s => states.Add(s.State);
        var res = await r.Link.RunAsync("Rendern", "SLEEP:3000");
        Assert.Equal(true, (bool?)res["result"]!["fertig"]);
        Assert.Equal(1, r.Link.Starts); Assert.False(r.Logged("abbruch"));
        Assert.DoesNotContain(BlenderLinkState.VerbindetNeu, states); Assert.DoesNotContain(BlenderLinkState.Getrennt, states);
        Assert.Contains(BlenderLinkState.Arbeitet, states);
        Assert.Equal("fertig", Assert.Single(r.Link.Jobs).State);
    }

    [Fact(DisplayName = "T-0785: Absturz mitten im Auftrag – wiederholbarer Auftrag läuft nach dem Neustart weiter, anderer wird klar gemeldet")]
    public async Task Job_interrupted()
    {
        using var r = new Rig();
        await r.Link.EnsureAsync(r.Dir);
        using var keep = r.Link.Keep();
        var a1 = r.Host.AddonOf(r.Host.Last);
        var job = r.Link.RunAsync("Export", "SLEEP:4000", repeatable: true);
        await Until(() => a1.Sleeps == 1);
        r.Host.End(r.Host.Last);
        var res = await job.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(true, (bool?)res["result"]!["fertig"]); Assert.True(r.Logged("fortgesetzt"));
        Assert.Equal("fertig (nach Neuverbinden)", r.Link.Jobs[^1].State);

        var a2 = r.Host.AddonOf(r.Host.Last); var before = a2.Sleeps;
        var job2 = r.Link.RunAsync("Code", "SLEEP:4000", repeatable: false);
        await Until(() => a2.Sleeps == before + 1);
        r.Host.End(r.Host.Last);
        var e = await Assert.ThrowsAsync<BlenderLinkException>(() => job2);
        Assert.True(e.Interrupted); Assert.Contains("unterbrochen", e.Message);
        Assert.Equal("unterbrochen", r.Link.Jobs[^1].State);
        await Until(() => r.Link.Status.State == BlenderLinkState.Verbunden);
    }

    [Fact(DisplayName = "T-0785 N3: wiederholte Abstürze – Neustarts mit Grenze, dann „getrennt“; nur „Neu verbinden“ startet wieder")]
    public async Task Restart_budget()
    {
        using var r = new Rig();
        r.Link.MaxRestarts = 2;
        await r.Link.EnsureAsync(r.Dir);
        using var keep = r.Link.Keep();
        for (var i = 2; i <= 3; i++) { r.Host.End(r.Host.Last); var n = i; await Until(() => r.Link.Starts == n && r.Link.Status.State == BlenderLinkState.Verbunden); }
        r.Host.End(r.Host.Last);
        await Until(() => r.Link.Status.State == BlenderLinkState.Getrennt);
        Assert.Contains("angehalten", r.Link.Status.Text); Assert.Equal(3, r.Link.Starts);
        await r.Link.EnsureAsync(); Assert.Equal(3, r.Link.Starts); // ein normaler Bedarf hebt die Sperre nicht auf
        Assert.Equal(BlenderLinkState.Verbunden, (await r.Link.ReconnectAsync()).State);
        Assert.Equal(4, r.Link.Starts);
    }

    [Fact(DisplayName = "T-0785 M1: Online-Freigabe aus – Brücke startet nicht, klare Erklärung; gibt die Person frei, verbindet Trendo von selbst")]
    public async Task M1_online_off()
    {
        using var r = new Rig();
        r.Host.AutoBridge = false;
        var ensure = r.Link.EnsureAsync(r.Dir);
        await Until(() => r.Link.CurrentStatusFile is not null);
        r.WriteStatus(false, "online-aus");
        var s = await ensure;
        Assert.Equal(BlenderLinkState.Getrennt, s.State); Assert.Equal(BlenderLink.OnlineText, s.Text);
        using var keep = r.Link.Keep();
        await Task.Delay(400); Assert.Equal(BlenderLink.OnlineText, r.Link.Status.Text); Assert.Equal(1, r.Link.Starts); // Grund bleibt, kein Neustart
        r.WriteStatus(true, "gestartet"); r.Host.Bridge(r.Host.Last);
        await Until(() => r.Link.Status.State == BlenderLinkState.Verbunden);
        var script = BlenderBridge.StartScript(1, "s", "c", false, "t");
        Assert.Contains("if not bpy.app.online_access:", script); Assert.Contains("userpref_show(section='SYSTEM')", script);
        Assert.DoesNotContain("online_access =", script); Assert.DoesNotContain("use_online_access", script);
    }

    [Theory(DisplayName = "T-0785 M2: MCP-Add-on aus – erst fragen; Ja schreibt die Zustimmung und verbindet, Nein erklärt und schaltet nichts ein")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task M2_addon_consent(bool yes)
    {
        using var r = new Rig();
        r.Host.AutoBridge = false; var asked = 0;
        r.Link.AskAddonConsent = () => { Interlocked.Increment(ref asked); return Task.FromResult(yes); };
        var ensure = r.Link.EnsureAsync(r.Dir);
        await Until(() => r.Link.CurrentStatusFile is not null);
        Assert.Contains("ENABLE_ADDON = False", r.LastScript);
        r.WriteStatus(false, "addon-aus");
        if (yes)
        {
            await Until(() => File.Exists(r.Link.CurrentConsentFile!));
            r.WriteStatus(true, "gestartet"); r.Host.Bridge(r.Host.Last);
            Assert.Equal(BlenderLinkState.Verbunden, (await ensure).State); Assert.True(r.Link.AddonConsent);
        }
        else
        {
            var s = await ensure;
            Assert.Equal(BlenderLink.AddonNoText, s.Text); Assert.False(File.Exists(r.Link.CurrentConsentFile!)); Assert.False(r.Link.AddonConsent);
        }
        Assert.Equal(1, asked);
        Assert.Contains("if not (ENABLE_ADDON or os.path.exists(CONSENT)):", BlenderBridge.StartScript(1, "s", "c", false, "t"));
    }

    [Fact(DisplayName = "T-0785 M3: Port belegt (keine Blender-Brücke) → eigener freier Port; laufende Brücke auf anderem Port wird übernommen, nicht umgesetzt")]
    public async Task M3_port()
    {
        var busy = new TcpListener(IPAddress.Loopback, 0); busy.Start(); // ein anderes Programm, das annimmt, aber kein Blender ist
        var taken = ((IPEndPoint)busy.LocalEndpoint).Port;
        try
        {
            using var r = new Rig(port: taken);
            var s = await r.Link.EnsureAsync(r.Dir);
            Assert.Equal(BlenderLinkState.Verbunden, s.State); Assert.NotEqual(taken, r.Link.Port); Assert.True(r.Logged("port"));
            Assert.Contains($"PORT = {r.Link.Port}", r.LastScript);
        }
        finally { busy.Stop(); }

        using var r2 = new Rig();
        r2.Host.AutoBridge = false;
        var other = FreePort();
        var ensure = r2.Link.EnsureAsync(r2.Dir);
        await Until(() => r2.Link.CurrentStatusFile is not null);
        using var bridge = new FakeAddon(other, r2.Host.Last.Id);
        r2.WriteStatus(true, "laeuft", other);
        Assert.Equal(BlenderLinkState.Verbunden, (await ensure).State); Assert.Equal(other, r2.Link.Port);
        Assert.DoesNotContain("srv.stop()", BlenderBridge.StartScript(1, "s", "c", false, "t")); // eine laufende Brücke wird nie beendet oder umgesetzt
    }

    [Fact(DisplayName = "T-0785 M3: nach einem Absturz ist inzwischen ein fremdes Blender offen – kein zweites starten")]
    public async Task M3_restart_foreign()
    {
        using var r = new Rig();
        await r.Link.EnsureAsync(r.Dir);
        using var keep = r.Link.Keep();
        r.Host.Foreign.Add(777);
        r.Host.End(r.Host.Last);
        await Until(() => r.Link.Status.State == BlenderLinkState.Getrennt);
        Assert.Contains("anderes Blender offen", r.Link.Status.Text); Assert.Equal(1, r.Link.Starts);
    }

    [Fact(DisplayName = "T-0785: Projektwahl ohne Punkt-Dateien; Startskript speichert keine Einstellungen, räumt Sicherung beim Speichern/Schließen weg")]
    public void Script_and_project()
    {
        var s = BlenderBridge.StartScript(9911, Path.Combine(Path.GetTempPath(), "st.json"), Path.Combine(Path.GetTempPath(), "ok"), false, "t");
        Assert.Contains("PORT = 9911", s); Assert.Contains("srv.start('127.0.0.1', PORT)", s); Assert.Contains("copy=True", s); Assert.Contains("return 30.0", s);
        Assert.Contains("save_post.append(_saved)", s); Assert.Contains("atexit.register(_exit)", s);
        Assert.DoesNotContain("save_userpref", s); Assert.DoesNotContain("quit", s); Assert.DoesNotContain("RESTORE_TO", s);
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-blink-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var (a0, save0) = BlenderLink.ProjectArgs(d); Assert.Empty(a0); Assert.Equal(Path.Combine(d, "szene.blend"), save0);
            var blend = Path.Combine(d, "haus.blend"); File.WriteAllText(blend, "1"); File.SetLastWriteTimeUtc(blend, DateTime.UtcNow.AddMinutes(-5));
            File.WriteAllText(BlenderBridge.RecoveryFor(blend).Blend, "2");
            Assert.Equal([blend], BlenderLink.ProjectArgs(d).Args); Assert.Equal([blend], StudioMcp.LaunchArgs("blender", d));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0785 H3: Freigabeliste – lesende Werkzeuge einzeln, execute_blender_code* nicht vorab erlaubt; Port in der .mcp.json")]
    public void H3_allowed_tools_and_port()
    {
        var list = StudioMcp.BlenderAllowedTools.Split(' ');
        Assert.Equal(24, list.Length);
        Assert.All(list, t => Assert.StartsWith("mcp__trendo-blender__", t));
        Assert.DoesNotContain(list, t => t.Contains("execute"));
        Assert.DoesNotContain("mcp__trendo-blender", list); // nie der ganze Server
        Assert.Contains("mcp__trendo-blender__get_objects_summary", list); Assert.Contains("mcp__trendo-blender__render_viewport_to_path", list);
        Assert.Contains("NICHT", StudioMcp.BlenderRule(false)); Assert.Contains("save_mainfile", StudioMcp.BlenderRule(true));
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-blink-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var user = JsonNode.Parse("""{"mcpServers":{"blender":{"command":"python.exe","args":["-I","start.py"],"env":{"BLENDER_PATH":"b.exe"}}}}""")!.AsObject();
            StudioMcp.EnsureProjectConfig(d, "CORE-05", userConfig: user, blenderPort: 9911);
            var file = Path.Combine(d, ".mcp.json");
            var env = JsonNode.Parse(File.ReadAllText(file))!["mcpServers"]!["trendo-blender"]!["env"]!;
            Assert.Equal("9911", (string?)env["BLENDER_MCP_PORT"]); Assert.Equal("b.exe", (string?)env["BLENDER_PATH"]);
            Assert.Null(user["mcpServers"]!["blender"]!["env"]!["BLENDER_MCP_PORT"]);
            Assert.True(StudioMcp.HasServer(file, "trendo-blender")); Assert.False(StudioMcp.HasServer(file, "trendo-unreal"));
        }
        finally { Directory.Delete(d, true); }
    }
}
