using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>C5: Programme sichtbar mit dem Projekt starten (Startweg nachgebildet); Unity und Unbekanntes werden nicht gestartet.</summary>
public class StudioMcp_Ui
{
    [AvaloniaFact(DisplayName = "C5: Godot startet im Editor mit dem Projektordner; Unity und fremde Namen werden abgewiesen")]
    public void Launch()
    {
        var (w, _, _) = Ui.Open(); var calls = new List<(string Exe, IReadOnlyList<string> Args)>();
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-c5-" + Guid.NewGuid().ToString("N"))).FullName;
        MainWindow.StudioLaunchOverride = (e, a) => calls.Add((e, a));
        try
        {
            File.WriteAllText(Path.Combine(d, "project.godot"), "");
            Assert.Null(w.StudioLaunchProgram("godot", d));
            Assert.Equal(["--editor", "--path", d], Assert.Single(calls).Args);
            Assert.Contains("nicht automatisch", w.StudioLaunchProgram("unity", d));
            Assert.Contains("nicht automatisch", w.StudioLaunchProgram("cmd", d));
            Assert.Single(calls);
        }
        finally { MainWindow.StudioLaunchOverride = null; Directory.Delete(d, true); }
    }
}

/// <summary>T-0785: Blender startet von selbst über die gehaltene Verbindung (Ersatz-Brücke und nachgebildeter Prozess – Blender selbst ist ERSETZT;
/// der Echt-Test steht in Trendo.Core.Tests.BlenderLinkEchtTests).</summary>
public class BlenderLink_Ui
{
    static BlenderLink_Ui() => Trendo.Core.BlenderBridge.Wire ??= new LoopbackBlenderWire(); // QA-019: der App-Transport

    [Fact(DisplayName = "T-0786 QA-019: die App-Umsetzung LoopbackBlenderWire spricht echt über 127.0.0.1 – annehmen, senden, lesen, abgewiesen, freier Port")]
    public async Task Loopback_wire_real()
    {
        var wire = new LoopbackBlenderWire();
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0); l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        try
        {
            var server = Task.Run(async () =>
            {
                while (true) // erste Verbindung (nur Verbinden + Schließen) ohne Anfrage, zweite mit Anfrage
                {
                using var s = await l.AcceptSocketAsync(); var b = new byte[4096]; var got = new List<byte>();
                var closed = false;
                while (!got.Contains(0)) { var n = await s.ReceiveAsync(b); if (n == 0) { closed = true; break; } got.AddRange(b.Take(n)); }
                if (closed) continue;
                await s.SendAsync(System.Text.Encoding.UTF8.GetBytes("{\"status\":\"ok\",\"result\":{\"pid\":4242}}\0"));
                return System.Text.Encoding.UTF8.GetString([.. got.TakeWhile(x => x != 0)]);
                }
            });
            var (state, stream, _) = await wire.ConnectAsync(port, TimeSpan.FromSeconds(5), CancellationToken.None);
            Assert.Equal(Trendo.Core.WireConnect.Ok, state); Assert.NotNull(stream); stream!.Dispose();
            var prev = Trendo.Core.BlenderBridge.Wire; Trendo.Core.BlenderBridge.Wire = wire;
            try
            {
                // zweite Verbindung: der ganze Weg über BlenderBridge mit der App-Umsetzung
                var res = await Trendo.Core.BlenderBridge.ExecuteAsync(port, Trendo.Core.BlenderBridge.PingCode, TimeSpan.FromSeconds(10));
                Assert.Equal(4242, (int)res["result"]!["pid"]!);
            }
            finally { Trendo.Core.BlenderBridge.Wire = prev; }
            Assert.Contains("\"type\":\"execute\"", await server);
        }
        finally { l.Stop(); }
        var free = wire.FreePort(); Assert.InRange(free, 1024, 65535);
        var (refused, none, detail) = await wire.ConnectAsync(free, TimeSpan.FromSeconds(8), CancellationToken.None);
        Assert.Equal(Trendo.Core.WireConnect.Refused, refused); Assert.Null(none); Assert.False(string.IsNullOrEmpty(detail));
    }

    sealed class Proc(int id) : Trendo.Core.IBlenderProc { public int Id => id; public bool HasExited => false; public int? ExitCode => null; }

    sealed class Host(int port) : Trendo.Core.IBlenderHost
    {
        public int Starts;
        public int Port => port;
        public System.Net.Sockets.TcpListener? Bridge;
        public Trendo.Core.IBlenderProc Start(string exe, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env)
        {
            Interlocked.Increment(ref Starts);
            var l = Bridge = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port); l.Start();
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    System.Net.Sockets.Socket s; try { s = await l.AcceptSocketAsync(); } catch (Exception) { return; }
                    _ = Task.Run(async () =>
                    {
                        using var _s = s; var b = new byte[65536]; var got = new List<byte>();
                        while (!got.Contains(0)) { var n = await s.ReceiveAsync(b); if (n == 0) return; got.AddRange(b.Take(n)); }
                        await s.SendAsync(System.Text.Encoding.UTF8.GetBytes("{\"status\":\"ok\",\"result\":{\"pid\":4242}}\0"));
                    });
                }
            });
            return new Proc(4242);
        }
        public IReadOnlyList<int> BlenderPids() => Starts > 0 ? [4242] : [];
    }

    static int FreePort() { var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0); l.Start(); var p = ((System.Net.IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p; }

    [AvaloniaFact(DisplayName = "T-0785: „Blender starten“ nutzt die gehaltene Verbindung – zweimal gefragt, einmal gestartet, Statusfeld „verbunden“, Protokoll")]
    public async Task Start_once_status()
    {
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-blink-ui-" + Guid.NewGuid().ToString("N"))).FullName;
        var exe = Path.Combine(d, "blender.exe"); File.WriteAllText(exe, "");
        var host = new Host(FreePort());
        MainWindow.BlenderLinkOverride = () => new Trendo.Core.BlenderLink(exe, host, host.Port) { Heartbeat = TimeSpan.FromMilliseconds(100) };
        var (w, _, _) = Ui.Open();
        try
        {
            Assert.Null(w.StudioLaunchProgram("blender", d));
            Assert.Null(w.StudioLaunchProgram("blender", d));
            for (var i = 0; i < 200 && !w.BlenderStatusText.StartsWith("Blender verbunden"); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
            Assert.StartsWith("Blender verbunden", w.BlenderStatusText);
            Assert.Equal(1, host.Starts);
            Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "blender.link.start");
            Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "blender.link.verbunden");
        }
        finally { MainWindow.BlenderLinkOverride = null; host.Bridge?.Stop(); w.Close(); try { Directory.Delete(d, true); } catch (IOException) { } }
    }

    [AvaloniaFact(DisplayName = "T-0785: fehlt Blender, kommt eine ehrliche Meldung – kein Start, kein Laden")]
    public async Task Missing()
    {
        MainWindow.BlenderLinkOverride = () => null;
        var (w, _, _) = Ui.Open();
        try
        {
            Assert.Equal(MainWindow.BlenderMissingText, w.StudioLaunchProgram("blender", Path.GetTempPath()));
            var st = await w.BlenderEnsure(Path.GetTempPath());
            Assert.Equal(Trendo.Core.BlenderLinkState.Getrennt, st.State); Assert.Contains("nicht installiert", st.Text); Assert.Contains("Klick", st.Text);
        }
        finally { MainWindow.BlenderLinkOverride = null; w.Close(); }
    }

    [Fact(DisplayName = "T-0785 H3: Claude bekommt die lesenden Blender-Werkzeuge einzeln frei, execute_blender_code nicht vorab (mit und ohne Trendo-Brücke)")]
    public void Allowed_tools()
    {
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-blink-ui-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var mcp = Path.Combine(d, ".mcp.json");
            File.WriteAllText(mcp, "{\"mcpServers\":{\"trendo-blender\":{\"command\":\"python.exe\"}}}");
            var a = MainWindow.ClaudeWithMcp(["-p", "x"], mcp).ToList();
            var allowed = a[a.IndexOf("--allowedTools") + 1];
            Assert.Equal(Trendo.Core.StudioMcp.BlenderAllowedTools, allowed);
            Assert.DoesNotContain("execute_blender_code", allowed); // H3: Python-Ausführung läuft durch die Auto-Prüfung von Claude Code
            Assert.DoesNotContain("mcp__trendo-blender", allowed.Split(' '));
            var b = MainWindow.ClaudeWithMcp(["-p", "x"], mcp, Path.Combine(d, "bruecke.json")).ToList();
            Assert.Equal("mcp__trendo " + Trendo.Core.StudioMcp.BlenderAllowedTools, b[b.IndexOf("--allowedTools") + 1]);
            File.WriteAllText(mcp, "{\"mcpServers\":{\"trendo-unreal\":{\"command\":\"py.exe\"}}}");
            Assert.DoesNotContain("--allowedTools", MainWindow.ClaudeWithMcp(["-p", "x"], mcp));
        }
        finally { Directory.Delete(d, true); }
    }

    /// <summary>Antwortet wie die Brücke im offenen Blender der Person (Kennung mit offener Datei, Datei öffnen).</summary>
    sealed class PersonBlender : IDisposable
    {
        readonly System.Net.Sockets.TcpListener _l;
        public string File; public int Opened;
        public PersonBlender(int port, string file)
        {
            File = file; _l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port); _l.Start();
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    System.Net.Sockets.Socket s; try { s = await _l.AcceptSocketAsync(); } catch (Exception) { return; }
                    _ = Task.Run(async () =>
                    {
                        using var _s = s; var b = new byte[65536]; var got = new List<byte>();
                        while (!got.Contains(0)) { var n = await s.ReceiveAsync(b); if (n == 0) return; got.AddRange(b.Take(n)); }
                        var code = (string)System.Text.Json.Nodes.JsonNode.Parse(System.Text.Encoding.UTF8.GetString([.. got.TakeWhile(x => x != 0)]))!["code"]!;
                        string res;
                        if (code.Contains("open_mainfile")) { Interlocked.Increment(ref Opened); var a = code.IndexOf("filepath=") + 9; File = System.Text.Json.Nodes.JsonNode.Parse(code[a..code.IndexOf(")\n", a)])!.GetValue<string>(); res = "{\"status\":\"ok\",\"result\":{\"offen\":true}}"; }
                        else res = new System.Text.Json.Nodes.JsonObject { ["status"] = "ok", ["result"] = new System.Text.Json.Nodes.JsonObject { ["pid"] = 777, ["datei"] = File, ["geaendert"] = false } }.ToJsonString();
                        await s.SendAsync(System.Text.Encoding.UTF8.GetBytes(res + "\0"));
                    });
                }
            });
        }
        public void Dispose() => _l.Stop();
    }

    sealed class PersonHost : Trendo.Core.IBlenderHost
    {
        public int Starts;
        public Trendo.Core.IBlenderProc Start(string exe, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env) { Starts++; throw new InvalidOperationException("darf nicht starten"); }
        public IReadOnlyList<int> BlenderPids() => [777];
    }

    [AvaloniaTheory(DisplayName = "T-0785 M4: offenes Blender der Person mit fremder Datei – Trendo fragt; „Projekt öffnen“ öffnet es, sonst arbeitet Claude in der Datei ohne Speichern")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task M4_person_file(bool openProject)
    {
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-blink-ui-" + Guid.NewGuid().ToString("N"))).FullName;
        var exe = Path.Combine(d, "blender.exe"); System.IO.File.WriteAllText(exe, "");
        var proj = Directory.CreateDirectory(Path.Combine(d, "projekt")).FullName; var blend = Path.Combine(proj, "haus.blend"); System.IO.File.WriteAllText(blend, "x");
        var port = Trendo.Core.BlenderBridge.FreePort(); var other = Path.Combine(d, "fremd.blend");
        using var person = new PersonBlender(port, other); var host = new PersonHost(); var asked = new List<string>();
        MainWindow.BlenderLinkOverride = () => new Trendo.Core.BlenderLink(exe, host, port);
        MainWindow.BlenderAskOverride = k => { asked.Add(k); return openProject; };
        var (w, _, _) = Ui.Open();
        try
        {
            var steps = new List<Trendo.Core.ClaudeChats.Step>();
            var (keep, maySave) = await w.ClaudeBlenderPrepare(proj, new Avalonia.Controls.StackPanel(), steps, new Avalonia.Controls.StackPanel(), CancellationToken.None);
            using var _k = keep;
            Assert.Equal(["projekt"], asked); Assert.Equal(0, host.Starts);
            Assert.Contains(steps, s => s.Text.Contains("fremd.blend")); // die offene Datei wird gezeigt
            if (openProject) { Assert.Equal(1, person.Opened); Assert.Equal(blend, person.File); Assert.True(maySave); }
            else { Assert.Equal(0, person.Opened); Assert.False(maySave); Assert.Contains(steps, s => s.Text.Contains("soll sie nicht speichern") && s.Text.Contains("technisch sperren")); }
        }
        finally { MainWindow.BlenderLinkOverride = null; MainWindow.BlenderAskOverride = null; w.Close(); try { Directory.Delete(d, true); } catch (IOException) { } }
    }

    [AvaloniaFact(DisplayName = "T-0785 M2: Zustimmung zum MCP-Add-on wird sichtbar erfragt, mit Hinweis „bleibt dauerhaft an“, und gemerkt")]
    public async Task M2_consent_stored()
    {
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-blink-ui-" + Guid.NewGuid().ToString("N"))).FullName;
        var exe = Path.Combine(d, "blender.exe"); System.IO.File.WriteAllText(exe, "");
        var asked = 0;
        MainWindow.BlenderLinkOverride = () => new Trendo.Core.BlenderLink(exe, new PersonHost(), Trendo.Core.BlenderBridge.FreePort());
        MainWindow.BlenderAskOverride = k => { if (k == "addon") asked++; return true; };
        var (w, _, _) = Ui.Open();
        try
        {
            var link = w.BlenderLinkGet()!;
            Assert.False(link.AddonConsent); Assert.Null(w.BlenderAddonConsentStored);
            Assert.True(await link.AskAddonConsent!());
            Assert.Equal(1, asked); Assert.True(w.BlenderAddonConsentStored);
            Assert.Contains("dauerhaft an", MainWindow.BlenderAddonPermanentText);
            Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "blender.link.addon-frage");
        }
        finally { MainWindow.BlenderLinkOverride = null; MainWindow.BlenderAskOverride = null; w.Close(); try { Directory.Delete(d, true); } catch (IOException) { } }
    }

    [AvaloniaFact(DisplayName = "T-0785 N10: „Nein“ zum MCP-Add-on wird gemerkt – keine neue Frage, bis „Neu verbinden“ gedrückt wird")]
    public async Task N10_no_remembered()
    {
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-blink-ui-" + Guid.NewGuid().ToString("N"))).FullName;
        var exe = Path.Combine(d, "blender.exe"); System.IO.File.WriteAllText(exe, "");
        var asked = 0;
        MainWindow.BlenderLinkOverride = () => new Trendo.Core.BlenderLink(exe, new PersonHost(), Trendo.Core.BlenderBridge.FreePort());
        MainWindow.BlenderAskOverride = k => { if (k == "addon") asked++; return false; };
        var (w, _, _) = Ui.Open();
        try
        {
            var link = w.BlenderLinkGet()!;
            Assert.False(await link.AskAddonConsent!()); Assert.Equal(1, asked); Assert.False(w.BlenderAddonConsentStored);
            Assert.False(await link.AskAddonConsent!()); Assert.Equal(1, asked); // gemerkt, keine zweite Frage
            await w.BlenderReconnectClick(); // Knopf „Neu verbinden“
            Assert.False(await link.AskAddonConsent!()); Assert.Equal(2, asked); // fragt wieder
        }
        finally { MainWindow.BlenderLinkOverride = null; MainWindow.BlenderAskOverride = null; w.Close(); try { Directory.Delete(d, true); } catch (IOException) { } }
    }

    [AvaloniaFact(DisplayName = "T-0785 H2: Trendo schließen gibt die Blender-Verbindung frei (kein Neustart beim Herunterfahren)")]
    public void Close_disposes_link()
    {
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-blink-ui-" + Guid.NewGuid().ToString("N"))).FullName;
        var exe = Path.Combine(d, "blender.exe"); System.IO.File.WriteAllText(exe, "");
        MainWindow.BlenderLinkOverride = () => new Trendo.Core.BlenderLink(exe, new PersonHost(), Trendo.Core.BlenderBridge.FreePort());
        var (w, _, _) = Ui.Open();
        try
        {
            var link = w.BlenderLinkGet()!;
            Assert.False(link.IsDisposed);
            w.Close();
            Assert.True(link.IsDisposed);
        }
        finally { MainWindow.BlenderLinkOverride = null; try { Directory.Delete(d, true); } catch (IOException) { } }
    }
}
