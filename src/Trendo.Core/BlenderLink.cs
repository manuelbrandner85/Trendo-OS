using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>T-0785: Protokoll des Blender-MCP-Add-ons (Blender Lab, Erweiterung „mcp“). Der MCP-Server, den Claude Code startet (stdio, z. B. blmcp),
/// öffnet für JEDEN Werkzeugaufruf eine neue TCP-Verbindung zu diesem Add-on in Blender (Standard 127.0.0.1:9876, Umgebung <c>BLENDER_MCP_PORT</c>) und
/// schickt <c>{"type":"execute","code":…,"strict_json":…}</c> mit einem Null-Byte am Ende; die Antwort ist JSON mit <c>status</c>/<c>result</c>, ebenfalls mit
/// Null-Byte. Das Add-on bearbeitet Anfragen im Hauptfaden von Blender – während eines langen Renders antwortet es erst danach, der Port nimmt aber weiter an.</summary>
public static class BlenderBridge
{
    public const int DefaultPort = 9876;
    public const string PortVariable = "BLENDER_MCP_PORT";

    /// <summary>Kurzer Lebenszeichen-Code: welche Blender-Instanz antwortet (PID), welche Datei offen ist, ungespeicherte Änderungen.</summary>
    public const string PingCode = "import bpy,os\nresult={'pid':os.getpid(),'datei':bpy.data.filepath,'version':bpy.app.version_string,'geaendert':bool(bpy.data.is_dirty)}";

    /// <summary>Nimmt jemand auf dem Port an? (Betriebssystem-Ebene – klappt auch, wenn Blender gerade lange rechnet.)</summary>
    public static async Task<bool> ListeningAsync(int port, TimeSpan timeout, CancellationToken ct = default)
    {
        using var c = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(timeout);
        try { await c.ConnectAsync("127.0.0.1", port, cts.Token); return true; }
        catch (Exception e) when (e is SocketException or OperationCanceledException or IOException) { if (ct.IsCancellationRequested) throw; return false; }
    }

    /// <summary>Code in Blender ausführen. <paramref name="timeout"/> gilt für die ganze Anfrage (lange Läufe: Stunden). Wirft <see cref="BlenderBridgeException"/>.</summary>
    public static async Task<JsonObject> ExecuteAsync(int port, string code, TimeSpan timeout, CancellationToken ct = default, bool strictJson = true)
    {
        using var c = new TcpClient { NoDelay = true };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(timeout);
        try { await c.ConnectAsync("127.0.0.1", port, cts.Token); }
        catch (SocketException e) { throw new BlenderBridgeException(BlenderBridgeError.Refused, $"Keine Verbindung zu Blender auf Port {port} ({e.SocketErrorCode})."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new BlenderBridgeException(BlenderBridgeError.Timeout, $"Blender auf Port {port} nimmt nicht an."); }
        var req = new JsonObject { ["type"] = "execute", ["code"] = code, ["strict_json"] = strictJson }.ToJsonString() + "\0";
        var buf = new MemoryStream();
        try
        {
            var s = c.GetStream();
            await s.WriteAsync(Encoding.UTF8.GetBytes(req), cts.Token);
            var chunk = new byte[65536];
            while (true)
            {
                var n = await s.ReadAsync(chunk, cts.Token);
                if (n == 0) break;
                buf.Write(chunk, 0, n);
                if (Array.IndexOf(chunk, (byte)0, 0, n) >= 0) break;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new BlenderBridgeException(BlenderBridgeError.Timeout, $"Zeitgrenze erreicht ({timeout.TotalMinutes:0.#} min) – Blender rechnet noch."); }
        catch (IOException e) { throw new BlenderBridgeException(BlenderBridgeError.Broken, "Verbindung zu Blender abgerissen: " + (e.InnerException?.Message ?? e.Message)); }
        catch (SocketException e) { throw new BlenderBridgeException(BlenderBridgeError.Broken, "Verbindung zu Blender abgerissen: " + e.SocketErrorCode); }
        var bytes = buf.ToArray(); var end = Array.IndexOf(bytes, (byte)0);
        if (end < 0) throw new BlenderBridgeException(BlenderBridgeError.Broken, bytes.Length == 0 ? "Blender hat die Verbindung ohne Antwort geschlossen." : "Antwort von Blender unvollständig.");
        try { return JsonNode.Parse(Encoding.UTF8.GetString(bytes, 0, end)) as JsonObject ?? throw new JsonException(); }
        catch (JsonException) { throw new BlenderBridgeException(BlenderBridgeError.Invalid, "Antwort von Blender ist kein gültiges JSON."); }
    }

    /// <summary>Python-Code für den sichtbaren Start (über <see cref="BlenderScript"/> mit SHA-256-Prüfung): MCP-Add-on einschalten, falls nötig, und seine
    /// Brücke auf <paramref name="port"/> starten – ohne die Einstellungen des Add-ons zu ändern. Fehlt das Add-on, steht „addon-fehlt“ in der Statusdatei.
    /// <paramref name="saveAs"/>: Projekt hatte noch keine .blend – die Szene wird dort gespeichert. <paramref name="restoreTo"/>: Blender wurde mit der
    /// Wiederherstellungsdatei gestartet – sie wird unter dem Projektnamen gespeichert. <paramref name="recovery"/>: alle 30 s eine Sicherungskopie, wenn sich
    /// etwas geändert hat (höchstens 30 s Verlust bei einem Absturz).</summary>
    public static string StartScript(int port, string statusFile, string? recovery = null, string? saveAs = null, string? restoreTo = null)
    {
        static string Lit(string? p) => p is null ? "None" : JsonSerializer.Serialize(Path.GetFullPath(p));
        return $$"""
            import bpy, os, json, importlib, addon_utils
            PORT = {{port}}
            STATUS = {{Lit(statusFile)}}
            RECOVERY = {{Lit(recovery)}}
            SAVE_AS = {{Lit(saveAs)}}
            RESTORE_TO = {{Lit(restoreTo)}}
            _changed = [False]

            def _status(ok, grund):
                try:
                    with open(STATUS, 'w', encoding='utf-8') as f:
                        json.dump({'ok': ok, 'grund': grund, 'pid': os.getpid(), 'port': PORT}, f)
                except OSError:
                    pass

            def _mcp_names():
                return [n for n in bpy.context.preferences.addons.keys() if n.startswith('bl_ext.') and n.endswith('.mcp')]

            def _mcp_module():
                names = _mcp_names()
                if not names:
                    for m in addon_utils.modules():
                        n = m.__name__
                        if n.startswith('bl_ext.') and n.endswith('.mcp'):
                            try:
                                bpy.ops.preferences.addon_enable(module=n)
                            except Exception as ex:
                                print('TRENDO-MCP', n, ex)
                            break
                    names = _mcp_names()
                return names[0] if names else None

            def _start():
                try:
                    if RESTORE_TO:
                        bpy.ops.wm.save_as_mainfile(filepath=RESTORE_TO, check_existing=False)
                    elif SAVE_AS and not bpy.data.filepath:
                        bpy.ops.wm.save_as_mainfile(filepath=SAVE_AS, check_existing=False)
                except Exception as ex:
                    print('TRENDO-PROJEKT', ex)
                name = _mcp_module()
                if name is None:
                    _status(False, 'addon-fehlt')
                    return None
                srv = importlib.import_module(name + '.mcp_to_blender_server')
                ei = importlib.import_module(name + '.execute_interactive')
                if srv.is_running():
                    try:
                        cur = srv._state.sock.getsockname()[1]
                    except Exception:
                        cur = None
                    if cur == PORT:
                        _status(True, 'laeuft')
                        return None
                    srv.stop()
                try:
                    srv.start('127.0.0.1', PORT)
                except Exception as ex:
                    _status(False, 'port-belegt: ' + str(ex))
                    return None
                if not bpy.app.timers.is_registered(ei.run):
                    bpy.app.timers.register(ei.run, first_interval=srv.TIMER_INTERVAL_ACTIVE, persistent=True)
                _status(True, 'gestartet')
                return None

            @bpy.app.handlers.persistent
            def _mark(*_args):
                _changed[0] = True

            def _save_recovery():
                if RECOVERY and _changed[0] and bpy.data.filepath:
                    try:
                        bpy.ops.wm.save_as_mainfile(filepath=RECOVERY, copy=True, check_existing=False)
                        _changed[0] = False
                    except Exception as ex:
                        print('TRENDO-WIEDERHERSTELLUNG', ex)
                return 30.0

            bpy.app.handlers.depsgraph_update_post.append(_mark)
            bpy.app.timers.register(_start, first_interval=0.2, persistent=True)
            if RECOVERY:
                bpy.app.timers.register(_save_recovery, first_interval=30.0, persistent=True)
            """;
    }
}

public enum BlenderBridgeError { Refused, Timeout, Broken, Invalid }

public sealed class BlenderBridgeException(BlenderBridgeError kind, string message) : Exception(message)
{
    public BlenderBridgeError Kind { get; } = kind;
    /// <summary>Die Verbindung ist weg (Blender beendet, Brücke aus) – im Gegensatz zu einer Zeitgrenze, bei der Blender noch rechnet.</summary>
    public bool Lost => Kind is BlenderBridgeError.Refused or BlenderBridgeError.Broken;
}

/// <summary>Ein Auftrag konnte wegen der Verbindung nicht fertig werden – wird immer gemeldet, nie still verworfen.</summary>
public sealed class BlenderLinkException(string message, bool interrupted = false) : Exception(message)
{
    public bool Interrupted { get; } = interrupted;
}

public enum BlenderLinkState { Aus, Startet, Verbunden, Arbeitet, VerbindetNeu, Getrennt }

public sealed record BlenderLinkStatus(BlenderLinkState State, string Text, int Port, int? Pid, bool Own)
{
    /// <summary>Kurz für das Statusfeld.</summary>
    public string Label => State switch
    {
        BlenderLinkState.Verbunden => "Blender verbunden",
        BlenderLinkState.Arbeitet => "Blender arbeitet – Verbindung steht",
        BlenderLinkState.Startet => "Blender startet …",
        BlenderLinkState.VerbindetNeu => "Blender verbindet neu …",
        BlenderLinkState.Getrennt => "Blender getrennt",
        _ => "Blender aus",
    };
    public bool Usable => State is BlenderLinkState.Verbunden or BlenderLinkState.Arbeitet;
}

public sealed record BlenderJob(string Label, DateTimeOffset Started)
{
    public string State { get; internal set; } = "läuft";
}

/// <summary>Ein von Trendo gestarteter Blender-Prozess (für Tests nachbildbar).</summary>
public interface IBlenderProc
{
    int Id { get; }
    bool HasExited { get; }
    int? ExitCode { get; }
}

/// <summary>Startweg und Prozessliste – echt über <see cref="ProcessBlenderHost"/>, in Tests nachgebildet.</summary>
public interface IBlenderHost
{
    IBlenderProc Start(string exe, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env);
    /// <summary>PIDs aller laufenden Blender-Prozesse (auch fremde).</summary>
    IReadOnlyList<int> BlenderPids();
}

public sealed class ProcessBlenderHost : IBlenderHost
{
    sealed class Proc(Process p) : IBlenderProc
    {
        public int Id { get; } = p.Id;
        public bool HasExited { get { try { return p.HasExited; } catch (InvalidOperationException) { return true; } } }
        public int? ExitCode { get { try { return p.HasExited ? p.ExitCode : null; } catch (InvalidOperationException) { return null; } } }
    }

    /// <summary>Startet blender.exe direkt (nicht blender-launcher.exe, der sofort endet) – so kennt Trendo die echte PID. Ohne Konsolenfenster;
    /// Blenders eigenes Fenster ist ganz normal sichtbar.</summary>
    public IBlenderProc Start(string exe, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env)
    {
        var psi = ShellProcessLauncher.ForProgram(exe, args);
        foreach (var (k, v) in env) psi.Environment[k] = v;
        return new Proc(Process.Start(psi) ?? throw new InvalidOperationException("Blender ließ sich nicht starten."));
    }

    public IReadOnlyList<int> BlenderPids()
    {
        var all = Process.GetProcessesByName("blender");
        try { return all.Select(p => p.Id).ToList(); } finally { foreach (var p in all) p.Dispose(); }
    }
}

/// <summary>T-0785: hält die Verbindung zu einem sichtbaren Blender mit MCP-Brücke. Startet Blender bei Bedarf selbst (mit Projekt und eingeschalteter
/// Brücke), nutzt ein schon verbundenes Blender weiter (kein zweites, kein doppelter Start), überwacht die Verbindung (Herzschlag), startet ein abgestürztes
/// oder geschlossenes eigenes Blender mit Wartezeit neu und öffnet das Projekt wieder. Jeder Abbruch und jedes Neuverbinden geht ins Protokoll.
/// Fremde Blender-Prozesse werden nie beendet – Trendo beendet überhaupt kein Blender.</summary>
public sealed class BlenderLink : IDisposable
{
    readonly IBlenderHost _host;
    readonly Action<string, string>? _log;
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly object _lock = new();
    readonly List<DateTimeOffset> _restarts = [];
    readonly List<string> _events = [];
    readonly List<BlenderJob> _jobs = [];
    IBlenderProc? _proc;
    int _leases, _busy;
    CancellationTokenSource? _watch;
    bool _everConnected, _disposed, _paused;
    BlenderLinkStatus _status;

    /// <param name="separateInstance">Eigene Instanz (eigener Port, eigenes Profil in <paramref name="env"/>): darf neben einem offenen fremden Blender starten.</param>
    public BlenderLink(string exe, IBlenderHost? host = null, int port = BlenderBridge.DefaultPort, IReadOnlyDictionary<string, string>? env = null, bool separateInstance = false, Action<string, string>? log = null)
    {
        Exe = exe; _host = host ?? new ProcessBlenderHost(); Port = port; Env = env ?? new Dictionary<string, string>(); SeparateInstance = separateInstance; _log = log;
        _status = new(BlenderLinkState.Aus, "Blender ist noch nicht verbunden.", port, null, false);
    }

    public string Exe { get; }
    public int Port { get; }
    public IReadOnlyDictionary<string, string> Env { get; }
    public bool SeparateInstance { get; }
    public string? ProjectDir { get; private set; }

    /// <summary>Herzschlag: so oft prüft Trendo die Verbindung.</summary>
    public TimeSpan Heartbeat { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>Lebenszeichen-Antwort: kommt sie nicht, rechnet Blender gerade – das ist KEIN Abbruch, solange der Port annimmt.</summary>
    public TimeSpan PingTimeout { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>So lange darf Blender zum Starten (großes Projekt laden) brauchen.</summary>
    public TimeSpan StartTimeout { get; set; } = TimeSpan.FromMinutes(2);
    /// <summary>Zeitgrenze eines Auftrags (Render, Code) – bewusst lang.</summary>
    public TimeSpan JobTimeout { get; set; } = TimeSpan.FromHours(6);
    public int MaxRestarts { get; set; } = 5;
    public TimeSpan RestartWindow { get; set; } = TimeSpan.FromMinutes(10);
    /// <summary>Wartezeit vor dem n-ten Neustart (1, 2, 4, 8 … höchstens 30 s).</summary>
    public Func<int, TimeSpan> Backoff { get; set; } = n => TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Max(0, n - 1))));
    /// <summary>Port-Prüfung im Herzschlag: so oft hintereinander darf der Port fehlen, bevor „getrennt“ gemeldet wird (Blender läuft noch).</summary>
    public int ListenFailures { get; set; } = 3;

    public BlenderLinkStatus Status { get { lock (_lock) return _status; } }
    public event Action<BlenderLinkStatus>? Changed;
    public int? OwnPid { get { lock (_lock) return _proc is { HasExited: false } p ? p.Id : null; } }
    public IReadOnlyList<string> Events { get { lock (_lock) return [.. _events]; } }
    public IReadOnlyList<BlenderJob> Jobs { get { lock (_lock) return [.. _jobs]; } }
    public int Starts { get; private set; }

    void Set(BlenderLinkState s, string text, int? pid = null, bool own = false)
    {
        BlenderLinkStatus st;
        lock (_lock) { if (_status.State == s && _status.Text == text) return; _status = st = new(s, text, Port, pid, own); }
        Changed?.Invoke(st);
    }

    void Log(string kind, string text)
    {
        lock (_lock) { _events.Add($"{DateTimeOffset.Now:HH:mm:ss} {kind}: {text}"); if (_events.Count > 200) _events.RemoveAt(0); }
        _log?.Invoke(kind, text);
        Logged?.Invoke(kind, text);
    }

    /// <summary>Jede Protokollzeile (Art, Text): abbruch, start, neustart, verbunden, wieder-verbunden, fortgesetzt, getrennt, fehler, auftrag.</summary>
    public event Action<string, string>? Logged;

    string StatusFile => Path.Combine(ProjectDir ?? Path.GetTempPath(), ".trendo-blender-status.json");
    public const string RecoveryName = ".trendo-blender-wiederherstellung.blend";

    /// <summary>Projektdatei: jüngste .blend im Projektordner (ohne Trendos eigene Punkt-Dateien). Ist die Wiederherstellungskopie jünger, wird sie
    /// geöffnet und unter dem Projektnamen gespeichert. Ohne .blend wird die Szene als „szene.blend“ im Projekt gespeichert.</summary>
    public static (IReadOnlyList<string> Args, string? SaveAs, string? RestoreTo) ProjectArgs(string? projectDir)
    {
        if (projectDir is null || !Directory.Exists(projectDir)) return ([], null, null);
        var blend = Directory.EnumerateFiles(projectDir, "*.blend", SearchOption.TopDirectoryOnly)
            .Where(f => !Path.GetFileName(f).StartsWith('.') && f.EndsWith(".blend", StringComparison.OrdinalIgnoreCase)).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        var rec = Path.Combine(projectDir, RecoveryName);
        if (blend is null) return File.Exists(rec) ? ([rec], null, Path.Combine(projectDir, "szene.blend")) : ([], Path.Combine(projectDir, "szene.blend"), null);
        if (File.Exists(rec) && File.GetLastWriteTimeUtc(rec) > File.GetLastWriteTimeUtc(blend)) return ([rec], null, blend);
        return ([blend], null, null);
    }

    async Task<JsonObject?> PingAsync(CancellationToken ct)
    {
        try
        {
            var r = await BlenderBridge.ExecuteAsync(Port, BlenderBridge.PingCode, PingTimeout, ct);
            return (string?)r["status"] == "ok" ? r["result"] as JsonObject ?? new JsonObject() : null;
        }
        catch (BlenderBridgeException) { return null; }
    }

    /// <summary>Verbindung herstellen: vorhandenes verbundenes Blender weiternutzen, sonst Blender sichtbar mit dem Projekt und der MCP-Brücke starten.
    /// Gleichzeitige Aufrufe warten aufeinander – es startet nie ein zweites Blender.</summary>
    public async Task<BlenderLinkStatus> EnsureAsync(string? projectDir = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(ct);
        try
        {
            if (projectDir is not null) ProjectDir = projectDir;
            _paused = false;
            return await EnsureLockedAsync(ct);
        }
        finally { _gate.Release(); }
    }

    async Task<BlenderLinkStatus> EnsureLockedAsync(CancellationToken ct)
    {
        IBlenderProc? own; lock (_lock) own = _proc;
        if (await PingAsync(ct) is { } info) return Connected(info, own);
        if (await BlenderBridge.ListeningAsync(Port, TimeSpan.FromSeconds(1), ct))
        {
            Set(BlenderLinkState.Arbeitet, $"Blender rechnet gerade – die Verbindung auf Port {Port} steht.", own?.Id, own is not null);
            return Status;
        }
        if (own is { HasExited: false }) return await WaitReadyAsync(own, ct); // startet noch
        if (own is { HasExited: true })
        {
            Log("abbruch", $"Blender (PID {own.Id}) wurde beendet{(own.ExitCode is { } x ? $", Code {x}" : "")}.");
            lock (_lock) _proc = null;
            var now = DateTimeOffset.Now;
            int n;
            lock (_lock) { _restarts.RemoveAll(t => now - t > RestartWindow); _restarts.Add(now); n = _restarts.Count; }
            if (n > MaxRestarts)
            {
                _paused = true;
                Log("getrennt", $"{MaxRestarts} Neustarts in {RestartWindow.TotalMinutes:0} min – automatische Neustarts angehalten.");
                Set(BlenderLinkState.Getrennt, $"Blender wurde {n - 1}-mal hintereinander beendet. Automatische Neustarts sind angehalten – „Neu verbinden“ versucht es erneut.");
                return Status;
            }
            var wait = Backoff(n);
            Set(BlenderLinkState.VerbindetNeu, $"Blender wurde beendet – starte neu in {wait.TotalSeconds:0} s (Versuch {n} von {MaxRestarts}).");
            await Task.Delay(wait, ct);
            if (await PingAsync(ct) is { } again) return Connected(again, null); // inzwischen von selbst wieder da
            return await StartLockedAsync(ct, restart: true);
        }
        var foreign = _host.BlenderPids();
        if (foreign.Count > 0 && !SeparateInstance)
        {
            Set(BlenderLinkState.Getrennt, $"Blender ist schon offen, aber ohne MCP-Verbindung auf Port {Port}. Starte in Blender unter Bearbeiten → Einstellungen → Add-ons → MCP den Server – oder schließe Blender, dann startet Trendo es selbst.");
            return Status;
        }
        return await StartLockedAsync(ct, restart: false);
    }

    BlenderLinkStatus Connected(JsonObject info, IBlenderProc? own)
    {
        var pid = info["pid"]?.GetValueKind() == JsonValueKind.Number ? info["pid"]!.GetValue<int>() : (int?)null;
        var isOwn = own is not null && pid == own.Id;
        var was = Status.State;
        if (was is BlenderLinkState.VerbindetNeu or BlenderLinkState.Getrennt or BlenderLinkState.Startet || !_everConnected)
            Log(_everConnected ? "wieder-verbunden" : "verbunden", $"Blender {(isOwn ? "von Trendo gestartet" : "schon offen")} (PID {pid?.ToString() ?? "?"}, Port {Port}).");
        _everConnected = true;
        Set(BlenderLinkState.Verbunden, isOwn ? $"Verbunden mit Blender (von Trendo gestartet, Port {Port})." : $"Verbunden mit deinem offenen Blender (Port {Port}).", pid, isOwn);
        return Status;
    }

    async Task<BlenderLinkStatus> StartLockedAsync(CancellationToken ct, bool restart)
    {
        if (!File.Exists(Exe)) { Set(BlenderLinkState.Getrennt, "Blender wurde nicht gefunden – „Blender einrichten“ im 3D-Studio richtet es ein."); return Status; }
        var (projArgs, saveAs, restoreTo) = ProjectArgs(ProjectDir);
        var dir = ProjectDir ?? Path.GetTempPath();
        try { if (File.Exists(StatusFile)) File.Delete(StatusFile); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        var script = Path.Combine(dir, ".trendo-blender-bruecke.py");
        var args = new List<string>(projArgs);
        args.AddRange(BlenderScript.Args(script, BlenderBridge.StartScript(Port, StatusFile, ProjectDir is null ? null : Path.Combine(dir, RecoveryName), saveAs, restoreTo)));
        Set(BlenderLinkState.Startet, restart ? "Blender wird neu gestartet und öffnet das Projekt wieder …" : "Blender startet sichtbar mit dem Projekt …");
        IBlenderProc p;
        try { p = _host.Start(Exe, args, Env); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            Log("fehler", "Start fehlgeschlagen: " + e.Message);
            Set(BlenderLinkState.Getrennt, "Blender ließ sich nicht starten: " + e.Message);
            return Status;
        }
        lock (_lock) _proc = p;
        Starts++;
        Log(restart ? "neustart" : "start", $"Blender gestartet (PID {p.Id}, Port {Port}{(projArgs.Count > 0 ? ", " + Path.GetFileName(projArgs[0]) : "")}{(restoreTo is not null ? ", aus der Wiederherstellung" : "")}).");
        return await WaitReadyAsync(p, ct);
    }

    async Task<BlenderLinkStatus> WaitReadyAsync(IBlenderProc p, CancellationToken ct)
    {
        var until = DateTimeOffset.Now + StartTimeout;
        while (DateTimeOffset.Now < until)
        {
            if (p.HasExited)
            {
                Log("abbruch", $"Blender (PID {p.Id}) hat sich beim Start beendet{(p.ExitCode is { } x ? $", Code {x}" : "")}.");
                Set(BlenderLinkState.Getrennt, "Blender hat sich beim Start sofort wieder beendet.");
                return Status;
            }
            if (ReadStatusFile() is { ok: false } bad)
            {
                var why = bad.grund == "addon-fehlt"
                    ? "In Blender fehlt das MCP-Add-on (Blender Lab „MCP“). Einrichten: Blender → Einstellungen → Erweiterungen → „MCP“ installieren. Trendo lädt nichts selbst."
                    : "Die MCP-Brücke in Blender startet nicht: " + bad.grund;
                Log("fehler", why);
                Set(BlenderLinkState.Getrennt, why, p.Id, true);
                return Status;
            }
            if (await PingAsync(ct) is { } info) return Connected(info, p);
            await Task.Delay(TimeSpan.FromMilliseconds(400), ct);
        }
        Set(BlenderLinkState.Getrennt, $"Blender ist offen, die MCP-Brücke hat sich aber nach {StartTimeout.TotalSeconds:0} s nicht gemeldet (Port {Port}).", p.Id, true);
        Log("fehler", "Brücke meldet sich nicht (Zeitgrenze Start).");
        return Status;
    }

    (bool ok, string grund)? ReadStatusFile()
    {
        try
        {
            if (!File.Exists(StatusFile)) return null;
            var o = JsonNode.Parse(File.ReadAllText(StatusFile));
            return ((bool?)o?["ok"] ?? false, (string?)o?["grund"] ?? "");
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException) { return null; }
    }

    /// <summary>„Neu verbinden“ per Knopf: Neustart-Zähler zurücksetzen und verbinden.</summary>
    public Task<BlenderLinkStatus> ReconnectAsync(CancellationToken ct = default)
    {
        lock (_lock) _restarts.Clear();
        Log("neu-verbinden", "Neu verbinden angefordert.");
        return EnsureAsync(null, ct);
    }

    /// <summary>Solange jemand die Verbindung braucht (Studio, Claude-Lauf), läuft der Herzschlag. Dispose gibt sie frei – Blender bleibt offen.</summary>
    public IDisposable Keep()
    {
        lock (_lock)
        {
            if (_leases++ == 0 && !_disposed) { _watch = new CancellationTokenSource(); var t = _watch.Token; _ = Task.Run(() => WatchAsync(t)); }
        }
        return new Lease(this);
    }

    sealed class Lease(BlenderLink l) : IDisposable
    {
        int _done;
        public void Dispose() { if (Interlocked.Exchange(ref _done, 1) == 0) l.Release(); }
    }

    void Release()
    {
        lock (_lock) { if (--_leases == 0) { _watch?.Cancel(); _watch = null; } }
    }

    async Task WatchAsync(CancellationToken ct)
    {
        var fails = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Heartbeat, ct);
                if (_paused) continue;
                IBlenderProc? own; int busy; lock (_lock) { own = _proc; busy = _busy; }
                if (own is { HasExited: true }) { fails = 0; await EnsureAsync(null, ct); continue; } // Absturz oder geschlossen: neu starten
                if (await BlenderBridge.ListeningAsync(Port, TimeSpan.FromSeconds(2), ct))
                {
                    fails = 0;
                    if (busy > 0) Set(BlenderLinkState.Arbeitet, $"Blender arbeitet am Auftrag – die Verbindung auf Port {Port} steht.", own?.Id, own is not null);
                    else if (!Status.Usable || Status.State == BlenderLinkState.Arbeitet)
                    {
                        if (!await _gate.WaitAsync(0, ct)) continue; // ein Start oder Auftrag läuft gerade
                        try { if (await PingAsync(ct) is { } info) Connected(info, own); else Set(BlenderLinkState.Arbeitet, $"Blender rechnet gerade – die Verbindung auf Port {Port} steht.", own?.Id, own is not null); }
                        finally { _gate.Release(); }
                    }
                    continue;
                }
                if (!_everConnected && own is null) continue; // nie verbunden gewesen: nichts zu überwachen
                fails++;
                if (fails == 1) Log("abbruch", $"Verbindung zu Blender auf Port {Port} weg.");
                if (own is null && _host.BlenderPids().Count == 0 || own is null && SeparateInstance) { fails = 0; Set(BlenderLinkState.VerbindetNeu, "Blender ist nicht mehr offen – starte es neu …"); await EnsureAsync(null, ct); continue; }
                if (fails < ListenFailures) Set(BlenderLinkState.VerbindetNeu, $"Verbindung zu Blender unterbrochen – verbinde neu (Versuch {fails}) …", own?.Id, own is not null);
                else Set(BlenderLinkState.Getrennt, $"Blender läuft, aber die MCP-Brücke antwortet nicht (Port {Port}). Trendo prüft weiter; „Neu verbinden“ oder in Blender den MCP-Server starten.", own?.Id, own is not null);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException or InvalidOperationException) { Log("fehler", "Herzschlag: " + e.Message); }
        }
    }

    /// <summary>Auftrag in Blender ausführen (Code wie über MCP). Reißt die Verbindung ab, wird neu verbunden; <paramref name="repeatable"/> Aufträge
    /// laufen danach noch einmal, andere werden klar als unterbrochen gemeldet. Lange Läufe brechen nicht ab (Zeitgrenze <see cref="JobTimeout"/>).</summary>
    public async Task<JsonObject> RunAsync(string label, string code, bool repeatable = false, CancellationToken ct = default)
    {
        var job = new BlenderJob(label, DateTimeOffset.Now);
        lock (_lock) { _jobs.Add(job); if (_jobs.Count > 50) _jobs.RemoveAt(0); _busy++; }
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                var st = await EnsureAsync(null, ct);
                if (!st.Usable) { job.State = "nicht gestartet"; Log("auftrag", $"„{label}“ nicht gestartet: {st.Text}"); throw new BlenderLinkException($"„{label}“ wurde nicht gestartet: {st.Text}"); }
                try
                {
                    var r = await BlenderBridge.ExecuteAsync(Port, code, JobTimeout, ct, strictJson: false);
                    job.State = attempt > 0 ? "fertig (nach Neuverbinden)" : "fertig";
                    if (attempt > 0) Log("fortgesetzt", $"„{label}“ nach dem Neuverbinden fertig.");
                    return r;
                }
                catch (BlenderBridgeException e) when (e.Lost)
                {
                    Log("abbruch", $"Auftrag „{label}“ unterbrochen: {e.Message}");
                    if (!repeatable || attempt > 0)
                    {
                        job.State = "unterbrochen";
                        _ = Task.Run(() => EnsureAsync(null, CancellationToken.None)); // Verbindung trotzdem wiederherstellen
                        throw new BlenderLinkException($"„{label}“ wurde unterbrochen ({e.Message}). Blender wird neu verbunden – bitte den Auftrag danach noch einmal starten.", true);
                    }
                    job.State = "wird wiederholt";
                    await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
                }
                catch (BlenderBridgeException e) when (e.Kind == BlenderBridgeError.Timeout)
                {
                    job.State = "Zeitgrenze";
                    Log("auftrag", $"„{label}“: {e.Message}");
                    throw new BlenderLinkException($"„{label}“: {e.Message}");
                }
            }
        }
        finally { lock (_lock) _busy--; }
    }

    /// <summary>Datei im verbundenen Blender öffnen – nur, wenn dort nichts Ungespeichertes verloren geht.</summary>
    public async Task<string?> OpenFileAsync(string blend, CancellationToken ct = default)
    {
        var code = "import bpy\nif bpy.data.is_dirty:\n result={'offen':False,'grund':'ungespeichert'}\nelse:\n bpy.ops.wm.open_mainfile(filepath=" + JsonSerializer.Serialize(Path.GetFullPath(blend)) + ")\n result={'offen':True}";
        var r = await RunAsync("Datei öffnen: " + Path.GetFileName(blend), code, false, ct);
        if ((string?)r["status"] != "ok") return "Blender konnte die Datei nicht öffnen: " + ((string?)r["message"] ?? "");
        return (bool?)r["result"]?["offen"] == true ? null : "In Blender sind noch ungespeicherte Änderungen – die Datei wurde nicht geöffnet, damit nichts verloren geht.";
    }

    public void Dispose()
    {
        lock (_lock) { if (_disposed) return; _disposed = true; _watch?.Cancel(); _watch = null; }
    }
}
