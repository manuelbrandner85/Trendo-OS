using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0785: Blender-Verbindung mit Ersatz-MCP-Brücke (spricht das Protokoll des Blender-Lab-Add-ons: JSON + Null-Byte über TCP) und nachgebildetem
/// Blender-Prozess. ERSETZT: Blender selbst und das Add-on. Der Echt-Test mit dem installierten Blender steht in <see cref="BlenderLinkEchtTests"/>.</summary>
public class BlenderLinkTests
{
    /// <summary>Ersatz für die MCP-Brücke im Blender-Add-on.</summary>
    internal sealed class FakeAddon : IDisposable
    {
        readonly TcpListener _l;
        readonly ConcurrentBag<Socket> _clients = [];
        readonly CancellationTokenSource _cts = new();
        public int Pid { get; }
        public int Requests;
        public int Sleeps;

        public FakeAddon(int port, int pid)
        {
            Pid = pid; _l = new TcpListener(IPAddress.Loopback, port);
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
                if (code == BlenderBridge.PingCode) res = new() { ["status"] = "ok", ["result"] = new JsonObject { ["pid"] = Pid, ["datei"] = "", ["geaendert"] = false } };
                else if (code.StartsWith("SLEEP:")) { Interlocked.Increment(ref Sleeps); await Task.Delay(int.Parse(code[6..]), _cts.Token); res = new() { ["status"] = "ok", ["result"] = new JsonObject { ["fertig"] = true, ["pid"] = Pid } }; }
                else res = new() { ["status"] = "ok", ["result"] = new JsonObject { ["echo"] = code, ["pid"] = Pid } };
                await s.SendAsync(Encoding.UTF8.GetBytes(res.ToJsonString() + "\0"));
                s.Shutdown(SocketShutdown.Both);
            }
            catch (Exception) { }
            finally { s.Dispose(); }
        }

        /// <summary>Absturz: Port zu, offene Verbindungen hart getrennt.</summary>
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
        public bool HasExited => Exited;
        public int? ExitCode => Exited ? -1 : null;
    }

    /// <summary>Nachgebildeter Startweg: „Blender“ meldet seine Brücke nach kurzer Zeit auf dem Port.</summary>
    internal sealed class FakeHost(int port) : IBlenderHost
    {
        int _next = 41000;
        public readonly List<(FakeProc P, IReadOnlyList<string> Args)> Started = [];
        public readonly Dictionary<int, FakeAddon> Addons = [];
        public List<int> Foreign { get; } = [];
        public int StartDelayMs { get; set; } = 150;

        public IBlenderProc Start(string exe, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env)
        {
            var p = new FakeProc(Interlocked.Increment(ref _next));
            lock (Started) Started.Add((p, args));
            _ = Task.Run(async () => { await Task.Delay(StartDelayMs); if (!p.Exited) lock (Addons) Addons[p.Id] = new FakeAddon(port, p.Id); });
            return p;
        }

        public IReadOnlyList<int> BlenderPids() { lock (Started) return [.. Foreign, .. Started.Where(s => !s.P.Exited).Select(s => s.P.Id)]; }

        public void Kill(FakeProc p) { p.Exited = true; lock (Addons) if (Addons.Remove(p.Id, out var a)) a.Dispose(); }
        public FakeProc Last { get { lock (Started) return Started[^1].P; } }
        public FakeAddon AddonOf(FakeProc p) { lock (Addons) return Addons[p.Id]; }
    }

    static int FreePort() { var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); var p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p; }

    sealed class Rig : IDisposable
    {
        public readonly string Dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-blink-" + Guid.NewGuid().ToString("N"))).FullName;
        public readonly int Port = FreePort();
        public readonly FakeHost Host;
        public readonly BlenderLink Link;
        public readonly List<string> Log = [];
        public Rig(bool separate = false)
        {
            Host = new FakeHost(Port);
            var exe = Path.Combine(Dir, "blender.exe"); File.WriteAllText(exe, "");
            File.WriteAllText(Path.Combine(Dir, "projekt.blend"), "x");
            Link = new BlenderLink(exe, Host, Port, separateInstance: separate, log: (k, t) => { lock (Log) Log.Add(k + ": " + t); })
            { Heartbeat = TimeSpan.FromMilliseconds(100), PingTimeout = TimeSpan.FromMilliseconds(400), StartTimeout = TimeSpan.FromSeconds(8), Backoff = _ => TimeSpan.FromMilliseconds(50) };
        }
        public bool Logged(string kind) { lock (Log) return Log.Any(l => l.StartsWith(kind + ":")); }
        public void Dispose()
        {
            Link.Dispose();
            lock (Host.Addons) foreach (var a in Host.Addons.Values) a.Dispose();
            try { Directory.Delete(Dir, true); } catch (IOException) { }
        }
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
        Assert.Equal(Path.Combine(r.Dir, "projekt.blend"), args[0]); // mit Projekt
        Assert.Equal("--python-expr", args[1]); Assert.DoesNotContain("--background", args); Assert.DoesNotContain("-b", args); // sichtbar, Brücke per geprüftem Skript
        Assert.Contains($"PORT = {r.Port}", File.ReadAllText(Path.Combine(r.Dir, ".trendo-blender-bruecke.py")));
        Assert.True(all[0].Own); Assert.Equal(r.Host.Last.Id, r.Link.OwnPid);
        Assert.True(r.Logged("start")); Assert.True(r.Logged("verbunden"));
    }

    [Fact(DisplayName = "T-0785: schon offenes, verbundenes Blender wird weitergenutzt – kein zweites")]
    public async Task Reuse()
    {
        using var r = new Rig();
        using var open = new FakeAddon(r.Port, 777); r.Host.Foreign.Add(777);
        var s = await r.Link.EnsureAsync(r.Dir);
        Assert.Equal(BlenderLinkState.Verbunden, s.State); Assert.False(s.Own); Assert.Equal(777, s.Pid);
        Assert.Empty(r.Host.Started);
    }

    [Fact(DisplayName = "T-0785: fremdes Blender ohne Brücke – klarer Grund, kein zweites Blender, nichts beendet")]
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
    }

    [Fact(DisplayName = "T-0785: Prozess beendet → automatischer Neustart mit demselben Projekt, Verbindung wieder da, alles im Protokoll")]
    public async Task Crash_restart()
    {
        using var r = new Rig();
        await r.Link.EnsureAsync(r.Dir);
        using var keep = r.Link.Keep();
        var first = r.Host.Last;
        r.Host.Kill(first);
        await Until(() => r.Link.Starts == 2 && r.Link.Status.State == BlenderLinkState.Verbunden);
        Assert.NotEqual(first.Id, r.Link.OwnPid);
        Assert.Equal(Path.Combine(r.Dir, "projekt.blend"), r.Host.Started[1].Args[0]); // Projekt wieder geöffnet
        Assert.True(r.Logged("abbruch")); Assert.True(r.Logged("neustart")); Assert.True(r.Logged("wieder-verbunden"));
        Assert.Equal(2, r.Host.Started.Count);
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
        lock (r.Host.Addons) r.Host.Addons[p.Id] = new FakeAddon(r.Port, p.Id);
        await Until(() => r.Link.Status.State == BlenderLinkState.Verbunden);
        Assert.Single(r.Host.Started); Assert.True(r.Logged("abbruch")); Assert.True(r.Logged("wieder-verbunden"));
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
        r.Host.Kill(r.Host.Last);
        var res = await job.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(true, (bool?)res["result"]!["fertig"]); Assert.True(r.Logged("fortgesetzt"));
        Assert.Equal("fertig (nach Neuverbinden)", r.Link.Jobs[^1].State);

        var a2 = r.Host.AddonOf(r.Host.Last); var before = a2.Sleeps;
        var job2 = r.Link.RunAsync("Code", "SLEEP:4000", repeatable: false);
        await Until(() => a2.Sleeps == before + 1);
        r.Host.Kill(r.Host.Last);
        var e = await Assert.ThrowsAsync<BlenderLinkException>(() => job2);
        Assert.True(e.Interrupted); Assert.Contains("unterbrochen", e.Message);
        Assert.Equal("unterbrochen", r.Link.Jobs[^1].State);
        await Until(() => r.Link.Status.State == BlenderLinkState.Verbunden); // Verbindung kommt trotzdem wieder
    }

    [Fact(DisplayName = "T-0785: wiederholte Abstürze – Neustarts mit Grenze, dann „getrennt“ mit Grund; „Neu verbinden“ startet wieder")]
    public async Task Restart_budget()
    {
        using var r = new Rig();
        r.Link.MaxRestarts = 2;
        await r.Link.EnsureAsync(r.Dir);
        using var keep = r.Link.Keep();
        for (var i = 2; i <= 3; i++) { r.Host.Kill(r.Host.Last); var n = i; await Until(() => r.Link.Starts == n && r.Link.Status.State == BlenderLinkState.Verbunden); }
        r.Host.Kill(r.Host.Last);
        await Until(() => r.Link.Status.State == BlenderLinkState.Getrennt);
        Assert.Contains("angehalten", r.Link.Status.Text); Assert.Equal(3, r.Link.Starts);
        Assert.Equal(BlenderLinkState.Verbunden, (await r.Link.ReconnectAsync()).State);
        Assert.Equal(4, r.Link.Starts);
    }

    [Fact(DisplayName = "T-0785: Start-Skript schaltet die Brücke auf dem Port ein, ohne Einstellungen zu speichern; Projekt- und Wiederherstellungswahl")]
    public void Script_and_project()
    {
        var s = BlenderBridge.StartScript(9911, Path.Combine(Path.GetTempPath(), "st.json"), Path.Combine(Path.GetTempPath(), "rec.blend"));
        Assert.Contains("PORT = 9911", s); Assert.Contains("addon_enable", s); Assert.Contains("srv.start('127.0.0.1', PORT)", s);
        Assert.Contains("copy=True", s); Assert.Contains("return 30.0", s); // Sicherungskopie alle 30 s
        Assert.DoesNotContain("save_userpref", s); Assert.DoesNotContain("quit", s);
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-blink-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var (a0, save0, _) = BlenderLink.ProjectArgs(d); Assert.Empty(a0); Assert.Equal(Path.Combine(d, "szene.blend"), save0);
            var blend = Path.Combine(d, "haus.blend"); File.WriteAllText(blend, "1"); File.SetLastWriteTimeUtc(blend, DateTime.UtcNow.AddMinutes(-5));
            Assert.Equal([blend], BlenderLink.ProjectArgs(d).Args);
            var rec = Path.Combine(d, BlenderLink.RecoveryName); File.WriteAllText(rec, "2");
            var (a1, _, restore) = BlenderLink.ProjectArgs(d); Assert.Equal([rec], a1); Assert.Equal(blend, restore); // jüngere Sicherung → wiederherstellen
            Assert.Equal([blend], StudioMcp.LaunchArgs("blender", d)); // Punkt-Dateien zählen nicht als Projekt
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0785: Projekt-MCP bekommt den Port der Verbindung; voller Werkzeugumfang des Blender-Servers")]
    public void Mcp_port_and_tools()
    {
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-blink-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var user = JsonNode.Parse("""{"mcpServers":{"blender":{"command":"python.exe","args":["-I","start.py"],"env":{"BLENDER_PATH":"b.exe"}}}}""")!.AsObject();
            StudioMcp.EnsureProjectConfig(d, "CORE-05", userConfig: user, blenderPort: 9911);
            var file = Path.Combine(d, ".mcp.json");
            var env = JsonNode.Parse(File.ReadAllText(file))!["mcpServers"]!["trendo-blender"]!["env"]!;
            Assert.Equal("9911", (string?)env["BLENDER_MCP_PORT"]); Assert.Equal("b.exe", (string?)env["BLENDER_PATH"]);
            Assert.Null(user["mcpServers"]!["blender"]!["env"]!["BLENDER_MCP_PORT"]); // Einstellung der Person unverändert
            Assert.True(StudioMcp.HasServer(file, "trendo-blender")); Assert.False(StudioMcp.HasServer(file, "trendo-unreal"));
            Assert.Equal("mcp__trendo-blender", StudioMcp.BlenderAllowedTools);
        }
        finally { Directory.Delete(d, true); }
    }
}
