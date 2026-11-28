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

    [Fact(DisplayName = "T-0785: Claude bekommt alle Werkzeuge des Blender-Servers frei (mit und ohne Trendo-Brücke)")]
    public void Allowed_tools()
    {
        var d = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-blink-ui-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var mcp = Path.Combine(d, ".mcp.json");
            File.WriteAllText(mcp, "{\"mcpServers\":{\"trendo-blender\":{\"command\":\"python.exe\"}}}");
            var a = MainWindow.ClaudeWithMcp(["-p", "x"], mcp).ToList();
            Assert.Equal("mcp__trendo-blender", a[a.IndexOf("--allowedTools") + 1]);
            var b = MainWindow.ClaudeWithMcp(["-p", "x"], mcp, Path.Combine(d, "bruecke.json")).ToList();
            Assert.Equal("mcp__trendo mcp__trendo-blender", b[b.IndexOf("--allowedTools") + 1]);
            File.WriteAllText(mcp, "{\"mcpServers\":{\"trendo-unreal\":{\"command\":\"py.exe\"}}}");
            Assert.DoesNotContain("--allowedTools", MainWindow.ClaudeWithMcp(["-p", "x"], mcp));
        }
        finally { Directory.Delete(d, true); }
    }
}
