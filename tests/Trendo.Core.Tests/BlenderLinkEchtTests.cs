using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0785 ECHT: installiertes Blender mit dem echten MCP-Add-on (Blender Lab). Läuft nur mit TRENDO_ECHT_BLENDER=&lt;blender.exe&gt; und
/// TRENDO_ECHT_MCP_ADDON=&lt;Ordner des Add-ons „mcp“&gt; – das Add-on wird nur in ein eigenes Temp-Profil KOPIERT, das Profil der Person bleibt unberührt.
/// Eigene Instanz: eigener Port (nicht 9876), eigenes Profil (BLENDER_USER_RESOURCES). Beendet wird nur der selbst gestartete Prozess (PID geprüft).
/// Rohlog nach TRENDO_ECHT_LOG (Pfade mit Benutzernamen ersetzt).</summary>
public class BlenderLinkEchtTests
{
    public sealed class EchtFactAttribute : FactAttribute
    {
        public EchtFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("TRENDO_ECHT_BLENDER") is not { Length: > 0 } b || !File.Exists(b)) Skip = "Übersprungen – TRENDO_ECHT_BLENDER (blender.exe) nicht gesetzt.";
            else if (Environment.GetEnvironmentVariable("TRENDO_ECHT_MCP_ADDON") is not { Length: > 0 } a || !File.Exists(Path.Combine(a, "mcp_to_blender_server.py"))) Skip = "Übersprungen – TRENDO_ECHT_MCP_ADDON (Ordner des MCP-Add-ons) nicht gesetzt.";
        }
    }

    static int FreePort() { var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); var p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p; }

    static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(from).Where(d => Path.GetFileName(d) != "__pycache__")) Copy(d, Path.Combine(to, Path.GetFileName(d)));
    }

    /// <summary>Den vorhandenen Blender-MCP-Server der Person (nur lesen: python -I -B, keine .pyc) per stdio ansprechen: Werkzeugliste und ein Aufruf.</summary>
    static async Task McpServerCheck(string startPy, int port, string blenderExe, Action<string> L)
    {
        var py = Path.Combine(Path.GetDirectoryName(startPy)!, "python", "python.exe");
        var psi = new ProcessStartInfo(py) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-I", "-B", startPy }) psi.ArgumentList.Add(a);
        psi.Environment[BlenderBridge.PortVariable] = port.ToString(); psi.Environment["BLENDER_PATH"] = blenderExe;
        using var p = Process.Start(psi)!;
        _ = p.StandardError.ReadToEndAsync();
        async Task<System.Text.Json.Nodes.JsonNode?> Call(int? id, string method, object? prm = null)
        {
            var o = new System.Text.Json.Nodes.JsonObject { ["jsonrpc"] = "2.0", ["method"] = method };
            if (id is { } i) o["id"] = i;
            if (prm is not null) o["params"] = System.Text.Json.JsonSerializer.SerializeToNode(prm);
            await p.StandardInput.WriteLineAsync(o.ToJsonString()); await p.StandardInput.FlushAsync();
            if (id is null) return null;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            while (await p.StandardOutput.ReadLineAsync(cts.Token) is { } line)
                if (System.Text.Json.Nodes.JsonNode.Parse(line) is { } n && (int?)n["id"] == id) return n;
            return null;
        }
        try
        {
            await Call(1, "initialize", new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "trendo-test", version = "1" } });
            await Call(null, "notifications/initialized");
            var tools = (await Call(2, "tools/list"))!["result"]!["tools"]!.AsArray().Select(t => (string)t!["name"]!).ToList();
            L($"MCP-Server (stdio) Werkzeuge: {tools.Count} – {string.Join(", ", tools)}");
            Assert.Contains("execute_blender_code", tools); Assert.Contains("render_viewport_to_path", tools); Assert.Contains("get_objects_summary", tools);
            var call = await Call(3, "tools/call", new { name = "get_objects_summary", arguments = new { } });
            var text = call!["result"]!.ToJsonString();
            L("MCP-Server get_objects_summary: " + (text.Length > 600 ? text[..600] + " …" : text));
            Assert.Contains("TrendoWuerfel", text);
        }
        finally { try { p.StandardInput.Close(); if (!p.WaitForExit(5000)) p.Kill(); } catch (InvalidOperationException) { } }
    }

    [EchtFact(DisplayName = "T-0785 ECHT: Blender startet sichtbar mit MCP, Würfel, Prozess beendet, automatischer Neustart, Szene wieder da")]
    public async Task Echt()
    {
        var exe = Environment.GetEnvironmentVariable("TRENDO_ECHT_BLENDER")!;
        var addon = Environment.GetEnvironmentVariable("TRENDO_ECHT_MCP_ADDON")!;
        var logFile = Environment.GetEnvironmentVariable("TRENDO_ECHT_LOG");
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-T-0785-echt-" + Guid.NewGuid().ToString("N")[..8])).FullName;
        var profile = Path.Combine(root, "profil"); var project = Directory.CreateDirectory(Path.Combine(root, "projekt")).FullName;
        Copy(addon, Path.Combine(profile, "extensions", "user_default", "mcp"));
        var log = new List<string>();
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string Clean(string s) => s.Replace(user.Replace(@"\", @"\\"), @"C:\\Users\\<user>", StringComparison.OrdinalIgnoreCase).Replace(user, @"C:\Users\<user>", StringComparison.OrdinalIgnoreCase).Replace(user.Replace('\\', '/'), "C:/Users/<user>", StringComparison.OrdinalIgnoreCase)
            .Replace(Environment.MachineName, "<pc>", StringComparison.OrdinalIgnoreCase);
        void L(string s) { lock (log) log.Add($"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {Clean(s)}"); }
        var before = Process.GetProcessesByName("blender").Select(p => p.Id).ToHashSet();
        var port = FreePort();
        L($"Blender: {exe} ({FileVersionInfo.GetVersionInfo(exe).ProductVersion})");
        L($"vorher offene Blender-Prozesse (fremd, werden nie beendet): {before.Count}");
        L($"eigene Instanz: Port {port}, Profil {profile}, Projekt {project}");
        var env = new Dictionary<string, string> { ["BLENDER_USER_RESOURCES"] = profile };
        var link = new BlenderLink(exe, new ProcessBlenderHost(), port, env, separateInstance: true, log: (k, t) => L($"[protokoll] {k}: {t}"))
        { Heartbeat = TimeSpan.FromSeconds(2), StartTimeout = TimeSpan.FromMinutes(3) };
        link.Changed += s => L($"[status] {s.State}: {s.Text}");
        int? own = null; var killed = new List<int>();
        try
        {
            var st = await link.EnsureAsync(project);
            Assert.Equal(BlenderLinkState.Verbunden, st.State); Assert.True(st.Own);
            own = link.OwnPid!.Value; Assert.DoesNotContain(own.Value, before);
            var proc = Process.GetProcessById(own.Value);
            for (var i = 0; i < 60 && proc.MainWindowHandle == IntPtr.Zero; i++) { await Task.Delay(500); proc.Refresh(); }
            L($"sichtbar: PID {own} Fenster={(proc.MainWindowHandle != IntPtr.Zero ? "ja" : "nein")} Titel=\"{proc.MainWindowTitle}\"");
            Assert.NotEqual(IntPtr.Zero, proc.MainWindowHandle);
            using var keep = link.Keep();

            var made = await link.RunAsync("Würfel anlegen", "import bpy, bmesh\nme=bpy.data.meshes.new('TrendoWuerfel'); bm=bmesh.new(); bmesh.ops.create_cube(bm, size=2.0); bm.to_mesh(me); bm.free()\nob=bpy.data.objects.new('TrendoWuerfel', me); bpy.context.scene.collection.objects.link(ob); ob.location=(0,0,1)\nresult={'objekte':[o.name for o in bpy.data.objects],'datei':bpy.data.filepath}");
            L("Würfel: " + made.ToJsonString());
            Assert.Equal("ok", (string?)made["status"]);
            Assert.Contains("TrendoWuerfel", made["result"]!["objekte"]!.ToJsonString());

            if (Environment.GetEnvironmentVariable("TRENDO_ECHT_MCP_SERVER") is { Length: > 0 } srv && File.Exists(srv))
                await McpServerCheck(srv, port, exe, L); // der Weg, den Claude Code nimmt: MCP-Server (stdio) → Brücke im von Trendo gestarteten Blender

            var rec = Path.Combine(project, BlenderLink.RecoveryName);
            for (var i = 0; i < 90 && !File.Exists(rec); i++) await Task.Delay(500); // Sicherungskopie spätestens nach 30 s
            L($"Wiederherstellungskopie vorhanden: {File.Exists(rec)} ({(File.Exists(rec) ? new FileInfo(rec).Length : 0)} Bytes)");
            Assert.True(File.Exists(rec));

            var first = own.Value;
            Assert.DoesNotContain(first, before);
            Process.GetProcessById(first).Kill(); killed.Add(first); // nur der selbst gestartete Prozess
            L($"eigenen Prozess beendet: PID {first}");
            for (var i = 0; i < 360 && !(link.Starts >= 2 && link.Status.State == BlenderLinkState.Verbunden); i++) await Task.Delay(500);
            Assert.Equal(BlenderLinkState.Verbunden, link.Status.State); Assert.Equal(2, link.Starts);
            own = link.OwnPid; Assert.NotEqual(first, own); Assert.DoesNotContain(own!.Value, before);
            L($"neu gestartet: PID {own}");

            var scene = await link.RunAsync("Szene abfragen", "import bpy\nresult={'objekte':sorted(o.name for o in bpy.data.objects),'datei':bpy.data.filepath,'geaendert':bool(bpy.data.is_dirty)}", repeatable: true);
            L("Szene nach Neustart: " + scene.ToJsonString());
            Assert.Contains("TrendoWuerfel", scene["result"]!["objekte"]!.ToJsonString());
            Assert.EndsWith("szene.blend", (string?)scene["result"]!["datei"]);
            L("ERGEBNIS: BESTANDEN");
        }
        catch (Exception e) { L("ERGEBNIS: FEHLER " + e.GetType().Name + ": " + e.Message); throw; }
        finally
        {
            link.Dispose();
            if (link.OwnPid is { } p && !before.Contains(p)) { try { Process.GetProcessById(p).Kill(); killed.Add(p); L($"Aufräumen: eigenen Prozess beendet (PID {p})"); } catch (ArgumentException) { } }
            var after = Process.GetProcessesByName("blender").Select(x => x.Id).ToHashSet();
            L($"von diesem Test beendet: nur eigene PIDs [{string.Join(",", killed)}] (keine davon war vorher offen: {!killed.Any(before.Contains)})");
            L($"fremde Blender-Prozesse vorher [{string.Join(",", before)}], noch offen [{string.Join(",", before.Where(after.Contains))}], von selbst beendet [{string.Join(",", before.Where(x => !after.Contains(x)))}]");
            if (logFile is { Length: > 0 }) File.WriteAllLines(logFile, log);
            await Task.Delay(1500);
            try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
