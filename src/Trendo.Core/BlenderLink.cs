using System.Diagnostics;
using System.Net;
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
    /// <summary>Zeitgrenze fürs Verbinden (länger als die ~2 s, nach denen Windows einen geschlossenen Port meldet).</summary>
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Kennungsabfrage: welche Blender-Instanz antwortet (PID), welche Datei offen ist, ungespeicherte Änderungen.</summary>
    public const string PingCode = "import bpy,os\nresult={'pid':os.getpid(),'datei':bpy.data.filepath,'version':bpy.app.version_string,'geaendert':bool(bpy.data.is_dirty)}";

    /// <summary>Nimmt jemand auf dem Port an? (Betriebssystem-Ebene – klappt auch, wenn Blender gerade lange rechnet.)</summary>
    public static async Task<bool> ListeningAsync(int port, TimeSpan timeout, CancellationToken ct = default)
    {
        using var c = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(timeout);
        try { await c.ConnectAsync("127.0.0.1", port, cts.Token); return true; }
        catch (Exception e) when (e is SocketException or OperationCanceledException or IOException) { if (ct.IsCancellationRequested) throw; return false; }
    }

    /// <summary>Freier Port auf 127.0.0.1 (für eine eigene Brücke, wenn der gewünschte belegt ist).</summary>
    public static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0); l.Start();
        try { return ((IPEndPoint)l.LocalEndpoint).Port; } finally { l.Stop(); }
    }

    /// <summary>Code in Blender ausführen. <paramref name="timeout"/> gilt für die ganze Anfrage (lange Läufe: Stunden). Wirft <see cref="BlenderBridgeException"/>.</summary>
    public static async Task<JsonObject> ExecuteAsync(int port, string code, TimeSpan timeout, CancellationToken ct = default, bool strictJson = true)
    {
        using var c = new TcpClient { NoDelay = true };
        // Verbinden hat eine eigene, längere Zeitgrenze: Windows meldet „abgewiesen“ auf 127.0.0.1 erst nach etwa 2 s – das darf nicht als „beschäftigt“ gelten.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(timeout > ConnectTimeout ? timeout : ConnectTimeout);
        try { await c.ConnectAsync("127.0.0.1", port, cts.Token); cts.CancelAfter(timeout); }
        catch (SocketException e) { throw new BlenderBridgeException(BlenderBridgeError.Refused, $"Keine Verbindung zu Blender auf Port {port} ({e.SocketErrorCode})."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new BlenderBridgeException(BlenderBridgeError.ConnectTimeout, $"Blender auf Port {port} nimmt gerade nicht an (Warteschlange voll oder beschäftigt)."); }
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

    /// <summary>Name der Sicherungskopie einer Datei – je Quelldatei eigen, im Ordner der Quelle (mehrere .blend in einem Ordner überschreiben sich nie).</summary>
    public static (string Blend, string Meta) RecoveryFor(string source)
    {
        var full = Path.GetFullPath(source);
        var b = Path.Combine(Path.GetDirectoryName(full)!, ".trendo-wiederherstellung-" + Path.GetFileNameWithoutExtension(full));
        return (b + ".blend", b + ".json");
    }

    /// <summary>Python-Code für den sichtbaren Start (über <see cref="BlenderScript"/> mit SHA-256-Prüfung).
    /// <list type="bullet">
    /// <item>Online-Freigabe von Blender wird respektiert: ist sie aus, startet die Brücke nicht; Blender zeigt die Einstellungen (System) – freigeben muss die Person selbst.</item>
    /// <item>MCP-Add-on: eingeschaltet wird es nur mit <paramref name="enableAddon"/> oder sobald die Zustimmungsdatei da ist (Trendo fragt die Person).</item>
    /// <item>Läuft die Brücke schon (Auto-Start des Add-ons), wird sie mit ihrem Port übernommen, nie beendet oder umgesetzt.</item>
    /// <item>Sicherung alle 30 s bei Änderungen, je Quelldatei mit Begleitdatei (Quelle, Start, PID). Speichert die Person oder schließt sie Blender sauber,
    /// fliegt die Sicherung weg. Die Datei der Person wird nie überschrieben.</item>
    /// </list></summary>
    public static string StartScript(int port, string statusFile, string consentFile, bool enableAddon, string start, string? saveAs = null, string? hint = null, bool recovery = true)
    {
        static string Lit(string? p) => p is null ? "None" : JsonSerializer.Serialize(p);
        static string Path_(string? p) => p is null ? "None" : JsonSerializer.Serialize(Path.GetFullPath(p));
        return $$"""
            import bpy, os, json, importlib, addon_utils, atexit, time
            PORT = {{port}}
            STATUS = {{Path_(statusFile)}}
            CONSENT = {{Path_(consentFile)}}
            ENABLE_ADDON = {{(enableAddon ? "True" : "False")}}
            START = {{Lit(start)}}
            SAVE_AS = {{Path_(saveAs)}}
            HINT = {{Lit(hint)}}
            RECOVERY = {{(recovery ? "True" : "False")}}
            _st = {'ok': None, 'grund': 'startet', 'pid': os.getpid(), 'port': PORT, 'start': START, 'datei': '', 'sicherung': ''}
            _changed = [False]
            _saving = [False]

            def _write():
                try:
                    tmp = STATUS + '.tmp'
                    with open(tmp, 'w', encoding='utf-8') as f:
                        json.dump(_st, f)
                    os.replace(tmp, STATUS)
                except OSError:
                    pass

            def _status(ok, grund, **kw):
                _st.update(ok=ok, grund=grund, **kw)
                _write()

            def _rec_paths(src):
                d, n = os.path.split(os.path.abspath(src))
                base = os.path.join(d, '.trendo-wiederherstellung-' + os.path.splitext(n)[0])
                return base + '.blend', base + '.json'

            def _drop_rec(src):
                if not src:
                    return
                for p in _rec_paths(src):
                    try:
                        os.remove(p)
                    except OSError:
                        pass
                _st['sicherung'] = ''
                _write()

            def _mcp_names():
                return [n for n in bpy.context.preferences.addons.keys() if n.startswith('bl_ext.') and n.endswith('.mcp')]

            def _mcp_found():
                return [m.__name__ for m in addon_utils.modules() if m.__name__.startswith('bl_ext.') and m.__name__.endswith('.mcp')]

            def _try_start():
                if not bpy.app.online_access:
                    if _st['grund'] != 'online-aus':
                        _status(False, 'online-aus')
                        try:
                            bpy.ops.screen.userpref_show(section='SYSTEM')
                        except Exception as ex:
                            print('TRENDO-EINSTELLUNGEN', ex)
                    return 2.0
                names = _mcp_names()
                if not names:
                    found = _mcp_found()
                    if not found:
                        if _st['grund'] != 'addon-fehlt':
                            _status(False, 'addon-fehlt')
                        return 5.0
                    if not (ENABLE_ADDON or os.path.exists(CONSENT)):
                        if _st['grund'] != 'addon-aus':
                            _status(False, 'addon-aus')
                        return 1.0
                    try:
                        bpy.ops.preferences.addon_enable(module=found[0])
                    except Exception as ex:
                        _status(False, 'addon-fehler: ' + str(ex))
                        return None
                    names = _mcp_names()
                    if not names:
                        _status(False, 'addon-fehler')
                        return None
                srv = importlib.import_module(names[0] + '.mcp_to_blender_server')
                ei = importlib.import_module(names[0] + '.execute_interactive')
                if srv.is_running():
                    try:
                        cur = srv._state.sock.getsockname()[1]
                    except Exception:
                        cur = PORT
                    _status(True, 'laeuft', port=cur)
                    return None
                try:
                    srv.start('127.0.0.1', PORT)
                except Exception as ex:
                    _status(False, 'port-belegt: ' + str(ex))
                    return None
                if not bpy.app.timers.is_registered(ei.run):
                    bpy.app.timers.register(ei.run, first_interval=srv.TIMER_INTERVAL_ACTIVE, persistent=True)
                _status(True, 'gestartet', port=PORT)
                return None

            def _hint():
                try:
                    wm = bpy.context.window_manager
                    def draw(self, ctx):
                        for line in HINT.split('\n'):
                            self.layout.label(text=line)
                    with bpy.context.temp_override(window=wm.windows[0]):
                        wm.popup_menu(draw, title='Wiederhergestellt nach Absturz', icon='INFO')
                except Exception as ex:
                    print('TRENDO-HINWEIS', ex)
                return None

            def _start():
                try:
                    if SAVE_AS and not bpy.data.filepath and not os.path.exists(SAVE_AS):
                        bpy.ops.wm.save_as_mainfile(filepath=SAVE_AS, check_existing=True)
                except Exception as ex:
                    print('TRENDO-PROJEKT', ex)
                _st['datei'] = bpy.data.filepath
                _write()
                if HINT:
                    bpy.app.timers.register(_hint, first_interval=1.5)
                bpy.app.timers.register(_try_start, first_interval=0.1, persistent=True)
                return None

            @bpy.app.handlers.persistent
            def _mark(*_a):
                _changed[0] = True

            @bpy.app.handlers.persistent
            def _saved(*_a):
                if _saving[0]:
                    return
                path = _a[0] if _a and isinstance(_a[0], str) else ''
                if path and bpy.data.filepath and os.path.normcase(os.path.abspath(path)) != os.path.normcase(os.path.abspath(bpy.data.filepath)):
                    return
                _changed[0] = False
                old = _st.get('datei')
                if old and os.path.normcase(os.path.abspath(old)) != os.path.normcase(os.path.abspath(bpy.data.filepath)):
                    _drop_rec(old)
                _drop_rec(bpy.data.filepath)
                _st['datei'] = bpy.data.filepath
                _write()

            @bpy.app.handlers.persistent
            def _loaded(*_a):
                old = _st.get('datei')
                if old and os.path.abspath(old) != os.path.abspath(bpy.data.filepath or old + '?'):
                    _drop_rec(old)
                _changed[0] = False
                _st['datei'] = bpy.data.filepath
                _write()

            def _save_recovery():
                src = bpy.data.filepath
                if RECOVERY and _changed[0] and src:
                    rec, meta = _rec_paths(src)
                    try:
                        _saving[0] = True
                        bpy.ops.wm.save_as_mainfile(filepath=rec, copy=True, check_existing=False)
                        with open(meta, 'w', encoding='utf-8') as f:
                            json.dump({'quelle': os.path.abspath(src), 'start': START, 'pid': os.getpid(), 'zeit': time.time()}, f)
                        _changed[0] = False
                        _st['sicherung'] = rec
                        _st['datei'] = src
                        _write()
                    except Exception as ex:
                        print('TRENDO-WIEDERHERSTELLUNG', ex)
                    finally:
                        _saving[0] = False
                return 30.0

            def _exit():
                _drop_rec(_st.get('datei'))
                _st.update(ok=False, grund='beendet')
                _write()

            atexit.register(_exit)
            bpy.app.handlers.depsgraph_update_post.append(_mark)
            bpy.app.handlers.save_post.append(_saved)
            bpy.app.handlers.load_post.append(_loaded)
            bpy.app.timers.register(_start, first_interval=0.2, persistent=True)
            if RECOVERY:
                bpy.app.timers.register(_save_recovery, first_interval=30.0, persistent=True)
            """;
    }
}

public enum BlenderBridgeError { Refused, Timeout, ConnectTimeout, Broken, Invalid }

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

public enum BlenderLinkState { Aus, Startet, Verbunden, Arbeitet, VerbindetNeu, Getrennt, Geschlossen }

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
        BlenderLinkState.Geschlossen => "Blender von dir geschlossen",
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
    /// <summary>0 = sauber beendet (Fenster geschlossen), sonst Absturz bzw. hart beendet.</summary>
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

    /// <summary>Zuletzt gestarteter Prozess (das gehaltene Objekt – z. B. für den Echt-Test).</summary>
    public Process? Last { get; private set; }

    /// <summary>Startet blender.exe direkt (nicht blender-launcher.exe, der sofort endet) – so kennt Trendo die echte PID. Ohne Konsolenfenster;
    /// Blenders eigenes Fenster ist ganz normal sichtbar.</summary>
    public IBlenderProc Start(string exe, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env)
    {
        var psi = ShellProcessLauncher.ForProgram(exe, args);
        foreach (var (k, v) in env) psi.Environment[k] = v;
        var p = Process.Start(psi) ?? throw new InvalidOperationException("Blender ließ sich nicht starten.");
        Last = p;
        return new Proc(p);
    }

    /// <summary>N9: nur Blender mit Fenster zählt als „offen“ – Hintergrundläufe (<c>--background</c>, ohne Fenster) nicht.</summary>
    public IReadOnlyList<int> BlenderPids()
    {
        var all = Process.GetProcessesByName("blender");
        try { return all.Where(Interactive).Select(p => p.Id).ToList(); } finally { foreach (var p in all) p.Dispose(); }
    }

    static bool Interactive(Process p) { try { return p.MainWindowHandle != IntPtr.Zero; } catch (InvalidOperationException) { return false; } }
}

/// <summary>T-0785: hält die Verbindung zu einem sichtbaren Blender mit MCP-Brücke. Startet Blender bei Bedarf selbst (mit Projekt und Brücke), nutzt ein
/// schon verbundenes Blender weiter (kein zweites, kein doppelter Start), überwacht die Verbindung (Herzschlag) und startet ein ABGESTÜRZTES eigenes Blender
/// mit Wartezeit neu. Schließt die Person Blender selbst, startet nichts neu („von dir geschlossen“, „Neu verbinden“). Nach einem Absturz öffnet sich die
/// Sicherung als EIGENE Datei mit Hinweis – die Datei der Person wird nie überschrieben. Jeder Abbruch und jedes Neuverbinden geht ins Protokoll.
/// Trendo beendet nie ein Blender.</summary>
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
    string? _procStart, _procStatus, _procConsent, _lastFile, _hold;
    int _leases, _busy, _quiet, _fails, _dupChecked = -1;
    int? _connectedPid;
    Task? _probe;
    DateTimeOffset _lastProbe = DateTimeOffset.MinValue, _lastCrash = DateTimeOffset.MinValue;
    CancellationTokenSource? _watch;
    bool _everConnected, _disposed, _paused;
    BlenderLinkStatus _status;

    /// <param name="separateInstance">Eigene Instanz (eigener Port, eigenes Profil in <paramref name="env"/>): darf neben einem offenen fremden Blender starten
    /// und das MCP-Add-on im eigenen Profil ohne Rückfrage einschalten.</param>
    public BlenderLink(string exe, IBlenderHost? host = null, int port = BlenderBridge.DefaultPort, IReadOnlyDictionary<string, string>? env = null, bool separateInstance = false, Action<string, string>? log = null, string? stateDir = null)
    {
        Exe = exe; _host = host ?? new ProcessBlenderHost(); Port = port; Env = env ?? new Dictionary<string, string>(); SeparateInstance = separateInstance; _log = log;
        StateDir = stateDir ?? Path.Combine(Path.GetTempPath(), "trendo-blender-link");
        _status = new(BlenderLinkState.Aus, "Blender ist noch nicht verbunden.", port, null, false);
    }

    public string Exe { get; }
    /// <summary>Port der Brücke – wechselt, wenn der gewünschte belegt ist oder Blender eine schon laufende Brücke auf einem anderen Port hat.</summary>
    public int Port { get; private set; }
    public IReadOnlyDictionary<string, string> Env { get; }
    public bool SeparateInstance { get; }
    /// <summary>Ordner für Statusdatei, Startskript und Zustimmungsdatei (Trendos eigener Temp-Ordner, nie der Projektordner).</summary>
    public string StateDir { get; }
    public string? ProjectDir { get; private set; }
    /// <summary>Zusätzliche Startargumente (nur für Tests mit eigenem Profil, z. B. „--online-mode“; das Produkt setzt keine).</summary>
    public IReadOnlyList<string> ExtraArgs { get; set; } = [];
    /// <summary>Die Person hat erlaubt, das MCP-Add-on in ihrem Blender einzuschalten (gemerkt von der App).</summary>
    public bool AddonConsent { get; set; }
    /// <summary>Fragt die Person, ob das MCP-Add-on eingeschaltet werden darf (beim ersten Mal). null = nicht fragen, nicht einschalten.</summary>
    public Func<Task<bool>>? AskAddonConsent { get; set; }

    /// <summary>Datei, die in Blender zuletzt offen war (Kennungsabfrage, Statusdatei).</summary>
    public string? OpenFile { get { lock (_lock) return _lastFile; } }
    public bool OpenFileDirty { get; private set; }
    /// <summary>Gehört die offene Datei zum Projekt (liegt im Projektordner)?</summary>
    public bool OpenFileInProject => ProjectDir is { } d && OpenFile is { Length: > 0 } f && Path.GetFullPath(f).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(d)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>Herzschlag: so oft prüft Trendo die Verbindung.</summary>
    public TimeSpan Heartbeat { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>Kennungsabfrage-Antwort: kommt sie nicht, rechnet Blender gerade – das ist KEIN Abbruch, solange der Port annimmt.</summary>
    public TimeSpan PingTimeout { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>So lange darf Blender zum Starten (großes Projekt laden) brauchen.</summary>
    public TimeSpan StartTimeout { get; set; } = TimeSpan.FromMinutes(2);
    /// <summary>Zeitgrenze eines Auftrags (Render, Code) – bewusst lang.</summary>
    public TimeSpan JobTimeout { get; set; } = TimeSpan.FromHours(6);
    public int MaxRestarts { get; set; } = 5;
    public TimeSpan RestartWindow { get; set; } = TimeSpan.FromMinutes(10);
    /// <summary>Wartezeit vor dem n-ten Neustart (1, 2, 4, 8 … höchstens 30 s).</summary>
    public Func<int, TimeSpan> Backoff { get; set; } = n => TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Max(0, n - 1))));
    /// <summary>So oft hintereinander darf der Port fehlen, bevor „getrennt“ gemeldet wird (Blender läuft noch).</summary>
    public int ListenFailures { get; set; } = 3;
    /// <summary>M5: Abstand der Kennungsabfragen im Herzschlag beim eigenen Blender (dazwischen nur: lebt der Prozess?).</summary>
    public TimeSpan ProbeInterval { get; set; } = TimeSpan.FromSeconds(15);
    /// <summary>M5: beim Blender der Person höchstens so oft eine Kennungsabfrage, sonst nur der Prozess.</summary>
    public TimeSpan ForeignProbeInterval { get; set; } = TimeSpan.FromSeconds(45);
    /// <summary>H2: wird Blender innerhalb dieser Zeit erneut beendet, startet Trendo nicht von selbst neu, sondern fragt.</summary>
    public TimeSpan QuickRestartWindow { get; set; } = TimeSpan.FromMinutes(2);
    /// <summary>H2: Frage „Blender schon wieder beendet – neu starten?“. null = nicht neu starten.</summary>
    public Func<Task<bool>>? AskRestart { get; set; }
    public bool IsDisposed => _disposed;
    /// <summary>Höchstzahl gleichzeitig offener Prüfverbindungen des Herzschlags (Prüfung für M5).</summary>
    public int MaxOpenProbes { get; private set; }
    int _openProbes;

    public BlenderLinkStatus Status { get { lock (_lock) return _status; } }
    public event Action<BlenderLinkStatus>? Changed;
    /// <summary>Jede Protokollzeile (Art, Text): abbruch, start, neustart, geschlossen, verbunden, wieder-verbunden, fortgesetzt, wiederhergestellt, getrennt, fehler, auftrag, port.</summary>
    public event Action<string, string>? Logged;
    public int? OwnPid { get { lock (_lock) return _proc is { HasExited: false } p ? p.Id : null; } }
    public IReadOnlyList<string> Events { get { lock (_lock) return [.. _events]; } }
    public IReadOnlyList<BlenderJob> Jobs { get { lock (_lock) return [.. _jobs]; } }
    public int Starts { get; private set; }
    /// <summary>Statusdatei, Zustimmungsdatei und Startzeit des laufenden eigenen Blender (Prüfung und Tests).</summary>
    public string? CurrentStatusFile { get { lock (_lock) return _procStatus; } }
    public string? CurrentConsentFile { get { lock (_lock) return _procConsent; } }
    public string? CurrentStart { get { lock (_lock) return _procStart; } }
    /// <summary>Zuletzt nach einem Absturz wiederhergestellte Datei (eigene Datei neben dem Original).</summary>
    public string? RestoredFile { get; private set; }
    /// <summary>Wie oft der Herzschlag eine Kennungsabfrage geschickt hat (Prüfung: keine Abfragen, solange Claude arbeitet).</summary>
    public int HeartbeatPings { get; private set; }

    void Set(BlenderLinkState s, string text, int? pid = null, bool own = false)
    {
        BlenderLinkStatus st;
        lock (_lock) { if (_status.State == s && _status.Text == text && _status.Port == Port) return; _status = st = new(s, text, Port, pid, own); }
        Changed?.Invoke(st);
    }

    void Log(string kind, string text)
    {
        lock (_lock) { _events.Add($"{DateTimeOffset.Now:HH:mm:ss} {kind}: {text}"); if (_events.Count > 200) _events.RemoveAt(0); }
        _log?.Invoke(kind, text);
        Logged?.Invoke(kind, text);
    }

    /// <summary>Projektdatei: jüngste .blend im Projektordner (ohne Trendos Punkt-Dateien). Ohne .blend wird die Szene als „szene.blend“ im Projekt gespeichert.</summary>
    public static (IReadOnlyList<string> Args, string? SaveAs) ProjectArgs(string? projectDir)
    {
        if (projectDir is null || !Directory.Exists(projectDir)) return ([], null);
        var blend = Directory.EnumerateFiles(projectDir, "*.blend", SearchOption.TopDirectoryOnly)
            .Where(IsProjectBlend).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        return blend is null ? ([], Path.Combine(projectDir, "szene.blend")) : ([blend], null);
    }

    /// <summary>Projektdatei: keine Punkt-Dateien (Trendos Sicherungen), keine „…-wiederhergestellt-…“-Kopien (N8).</summary>
    public static bool IsProjectBlend(string f)
    {
        var n = Path.GetFileName(f);
        return !n.StartsWith('.') && n.EndsWith(".blend", StringComparison.OrdinalIgnoreCase) && !n.Contains("-wiederhergestellt-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>N7: verwaiste Sicherungen (ihr Blender läuft nicht mehr) im Projektordner und im Ordner der zuletzt offenen Datei – zum Öffnen anbieten, nie automatisch.</summary>
    public IReadOnlyList<(string Source, string Meta, DateTime At)> OrphanRecoveries()
    {
        var dirs = new[] { ProjectDir, OpenFile is { } f ? Path.GetDirectoryName(f) : null }.OfType<string>().Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
        var alive = _host.BlenderPids().ToHashSet(); if (OwnPid is { } op) alive.Add(op);
        var list = new List<(string, string, DateTime)>();
        foreach (var d in dirs)
            foreach (var meta in Directory.EnumerateFiles(d, ".trendo-wiederherstellung-*.json"))
                try
                {
                    var m = JsonNode.Parse(File.ReadAllText(meta));
                    if ((string?)m?["quelle"] is not { } src || (int?)m?["pid"] is not { } pid || alive.Contains(pid)) continue;
                    if (!File.Exists(BlenderBridge.RecoveryFor(src).Blend)) continue;
                    list.Add((src, meta, File.GetLastWriteTime(meta)));
                }
                catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException or ArgumentException) { }
        return list;
    }

    /// <summary>N7: eine verwaiste Sicherung als eigene Datei neben dem Original anlegen (nichts überschrieben); danach fliegt die Sicherung weg.</summary>
    public static string? OpenOrphan(string source)
    {
        var (rec, meta) = BlenderBridge.RecoveryFor(source);
        try
        {
            if (!File.Exists(rec)) return null;
            var target = RestoredName(source);
            File.Copy(rec, target, overwrite: false);
            File.Delete(rec); if (File.Exists(meta)) File.Delete(meta);
            return target;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    static string RestoredName(string source)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(source))!; var stem = Path.GetFileNameWithoutExtension(source);
        var target = Path.Combine(dir, $"{stem}-wiederhergestellt-{DateTime.Now:yyyyMMdd-HHmmss}.blend");
        for (var i = 2; File.Exists(target); i++) target = Path.Combine(dir, $"{stem}-wiederhergestellt-{DateTime.Now:yyyyMMdd-HHmmss}-{i}.blend");
        return target;
    }

    async Task<JsonObject?> PingAsync(CancellationToken ct) => (await PingExAsync(ct)).Info;

    /// <summary>Kennungsabfrage; <c>Slow</c>: die Brücke nimmt an, antwortet aber nicht rechtzeitig (Blender rechnet) bzw. die Warteschlange ist voll.</summary>
    async Task<(JsonObject? Info, bool Slow)> PingExAsync(CancellationToken ct)
    {
        try
        {
            var r = await BlenderBridge.ExecuteAsync(Port, BlenderBridge.PingCode, PingTimeout, ct);
            return ((string?)r["status"] == "ok" ? r["result"] as JsonObject ?? new JsonObject() : null, false);
        }
        catch (BlenderBridgeException e) { return (null, e.Kind is BlenderBridgeError.Timeout or BlenderBridgeError.ConnectTimeout); }
    }

    /// <summary>Verbindung herstellen: vorhandenes verbundenes Blender weiternutzen, sonst Blender sichtbar mit dem Projekt und der MCP-Brücke starten.
    /// Gleichzeitige Aufrufe warten aufeinander – es startet nie ein zweites Blender. Nach „von dir geschlossen“ oder zu vielen Abstürzen startet nur
    /// <see cref="ReconnectAsync"/> wieder.</summary>
    public async Task<BlenderLinkStatus> EnsureAsync(string? projectDir = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(ct);
        try
        {
            if (projectDir is not null) ProjectDir = projectDir;
            return await EnsureLockedAsync(ct);
        }
        finally { _gate.Release(); }
    }

    async Task<BlenderLinkStatus> EnsureLockedAsync(CancellationToken ct)
    {
        IBlenderProc? own; lock (_lock) own = _proc;
        var (info, slow) = await PingExAsync(ct);
        if (info is not null)
        {
            if (_dupChecked != Port && await DuplicateCheckLockedAsync(info, own, ct) is { } dup) return dup;
            return Connected(info, own);
        }
        if (_paused) return Status;
        if (own is { HasExited: true }) return await HandleExitLockedAsync(own, ct);
        if (slow && (own is { HasExited: false } || _host.BlenderPids().Count > 0))
        {
            Set(BlenderLinkState.Arbeitet, $"Blender rechnet gerade – die Verbindung auf Port {Port} steht.", own?.Id, own is not null);
            return Status;
        }
        if (own is { HasExited: false }) return await WaitReadyAsync(own, ct); // startet noch oder wartet auf die Person (Online-Freigabe, Add-on)
        if (own is { HasExited: true }) return await HandleExitLockedAsync(own, ct);
        if (_host.BlenderPids().Count > 0 && !SeparateInstance)
        {
            Set(BlenderLinkState.Getrennt, $"Blender ist schon offen, aber ohne MCP-Verbindung auf Port {Port}. Starte in Blender unter Bearbeiten → Einstellungen → Add-ons → MCP den Server – oder schließe Blender, dann startet Trendo es selbst.");
            return Status;
        }
        return await StartLockedAsync(ct, restart: false, file: null, hint: null);
    }

    /// <summary>Eigenes Blender ist weg: sauber geschlossen (Code 0) → kein Neustart; Absturz → Neustart mit Grenze und Wartezeit, Sicherung als eigene Datei.</summary>
    async Task<BlenderLinkStatus> HandleExitLockedAsync(IBlenderProc own, CancellationToken ct)
    {
        lock (_lock) { if (!ReferenceEquals(_proc, own)) return _status; _proc = null; }
        var st = ReadStatus(_procStatus);
        var file = st?.Datei is { Length: > 0 } d ? d : _lastFile;
        if (own.ExitCode == 0)
        {
            if (file is not null) DropRecovery(file, own.Id);
            _paused = true;
            Log("geschlossen", $"Blender (PID {own.Id}) wurde sauber beendet – kein Neustart.");
            Set(BlenderLinkState.Geschlossen, "Du hast Blender geschlossen. Trendo startet es nicht von selbst neu – „Neu verbinden“ öffnet es wieder.");
            return Status;
        }
        Log("abbruch", $"Blender (PID {own.Id}) ist abgestürzt bzw. wurde hart beendet{(own.ExitCode is { } x ? $" (Code {x})" : "")}.");
        var now = DateTimeOffset.Now; int n;
        if (_disposed) return Status;
        if (now - _lastCrash < QuickRestartWindow) // z. B. zweimal „Task beenden“: nicht weiter von selbst neu starten, sondern fragen
        {
            _lastCrash = now;
            var again = AskRestart is { } ask && await ask();
            Log("neustart-frage", again ? "Neu starten: ja." : "Neu starten: nein.");
            if (!again)
            {
                _paused = true;
                Set(BlenderLinkState.Getrennt, "Blender wurde kurz hintereinander zweimal beendet. Trendo startet es nicht weiter von selbst – „Neu verbinden“ startet es.");
                return Status;
            }
        }
        _lastCrash = now;
        lock (_lock) { _restarts.RemoveAll(t => now - t > RestartWindow); _restarts.Add(now); n = _restarts.Count; }
        if (n > MaxRestarts)
        {
            _paused = true;
            Log("getrennt", $"{MaxRestarts} Neustarts in {RestartWindow.TotalMinutes:0} min – automatische Neustarts angehalten.");
            Set(BlenderLinkState.Getrennt, $"Blender ist {n - 1}-mal hintereinander abgestürzt. Automatische Neustarts sind angehalten – „Neu verbinden“ versucht es erneut.");
            return Status;
        }
        var wait = Backoff(n);
        Set(BlenderLinkState.VerbindetNeu, $"Blender ist abgestürzt – starte neu in {wait.TotalSeconds:0} s (Versuch {n} von {MaxRestarts}).");
        await Task.Delay(wait, ct);
        if (_disposed) return Status;
        if (await PingAsync(ct) is { } back) return Connected(back, null);
        if (!SeparateInstance && _host.BlenderPids().Count > 0) // inzwischen ist ein anderes Blender offen: keins dazu starten
        {
            Set(BlenderLinkState.Getrennt, "Blender ist abgestürzt; inzwischen ist ein anderes Blender offen. Trendo startet kein zweites – „Neu verbinden“ nutzt das offene, wenn sein MCP-Server läuft.");
            return Status;
        }
        string? hint = null; string? open = file is not null && File.Exists(file) ? file : null;
        if (file is not null && TakeRecovery(file, own.Id, _procStart) is { } restored)
        {
            open = restored; RestoredFile = restored;
            hint = $"Trendo hat Blender nach einem Absturz neu gestartet.\nDas ist eine EIGENE Datei: {Path.GetFileName(restored)}.\nSpeichern unter „{Path.GetFileName(file)}“ übernimmt die Änderungen – deine Datei ist unverändert.";
            Log("wiederhergestellt", $"Sicherung von {Path.GetFileName(file)} als eigene Datei {Path.GetFileName(restored)} geöffnet; Original unverändert.");
        }
        return await StartLockedAsync(ct, restart: true, file: open, hint: hint);
    }

    /// <summary>Sicherung einer Datei nur dann übernehmen, wenn ihre Begleitdatei genau diese Quelle, diesen Prozess und diese Startzeit nennt.
    /// Sie wird als eigene Datei „…-wiederhergestellt-…“ neben das Original kopiert (nichts überschrieben); die Sicherung fliegt danach weg.</summary>
    public static string? TakeRecovery(string source, int pid, string? start)
    {
        var (rec, meta) = BlenderBridge.RecoveryFor(source);
        try
        {
            if (!File.Exists(rec) || !File.Exists(meta)) return null;
            var m = JsonNode.Parse(File.ReadAllText(meta));
            if (!string.Equals(Path.GetFullPath((string?)m?["quelle"] ?? "?"), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase)) return null;
            if ((int?)m?["pid"] != pid || start is null || (string?)m?["start"] != start) return null;
            var target = RestoredName(source);
            File.Copy(rec, target, overwrite: false);
            File.Delete(rec); File.Delete(meta);
            return target;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException or ArgumentException) { return null; }
    }

    static void DropRecovery(string source, int pid)
    {
        var (rec, meta) = BlenderBridge.RecoveryFor(source);
        try
        {
            if (!File.Exists(meta) || (int?)JsonNode.Parse(File.ReadAllText(meta))?["pid"] != pid) return; // nur die eigene Sicherung
            File.Delete(rec); File.Delete(meta);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException or ArgumentException) { }
    }

    static int? PidOf(JsonObject info) => info["pid"]?.GetValueKind() == JsonValueKind.Number ? info["pid"]!.GetValue<int>() : null;

    /// <summary>M3-Rest: Lauschen zwei Brücken auf demselben Port (SO_REUSEADDR), liefern wiederholte Kennungsabfragen verschiedene PIDs. Dann deutlich
    /// warnen; ist Trendos eigenes Blender dabei, zieht NUR dessen Brücke auf einen freien Port um. Das Blender der Person wird nie umgesetzt.</summary>
    async Task<BlenderLinkStatus?> DuplicateCheckLockedAsync(JsonObject first, IBlenderProc? own, CancellationToken ct)
    {
        var pids = new HashSet<int>(); if (PidOf(first) is { } p0) pids.Add(p0);
        for (var i = 0; i < 4; i++) if (await PingAsync(ct) is { } more && PidOf(more) is { } p) pids.Add(p);
        if (pids.Count <= 1) { _dupChecked = Port; return null; }
        var old = Port;
        Log("doppelt", $"Auf Port {old} antworten zwei Blender-Brücken (PID {string.Join(", ", pids)}).");
        if (own is { HasExited: false } && pids.Contains(own.Id))
        {
            var neu = BlenderBridge.FreePort();
            var code = "import os, bpy, importlib\nresult={'pid':os.getpid(),'umgezogen':False}\nif os.getpid()==OWN:\n n=[k for k in bpy.context.preferences.addons.keys() if k.startswith('bl_ext.') and k.endswith('.mcp')][0]\n srv=importlib.import_module(n+'.mcp_to_blender_server'); ei=importlib.import_module(n+'.execute_interactive')\n def _mv():\n  srv.stop(); srv.start('127.0.0.1', NEU)\n  if not bpy.app.timers.is_registered(ei.run): bpy.app.timers.register(ei.run, first_interval=0.05, persistent=True)\n  return None\n bpy.app.timers.register(_mv, first_interval=0.3)\n result['umgezogen']=True".Replace("OWN", own.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("NEU", neu.ToString(System.Globalization.CultureInfo.InvariantCulture));
            for (var i = 0; i < 12; i++)
            {
                try { if ((bool?)(await BlenderBridge.ExecuteAsync(old, code, PingTimeout, ct))["result"]?["umgezogen"] == true) { Port = neu; break; } }
                catch (BlenderBridgeException) { }
            }
            if (Port == neu)
            {
                for (var i = 0; i < 40; i++) { if (await PingAsync(ct) is { } inf && PidOf(inf) == own.Id) { _dupChecked = Port; Log("port", $"Trendos Blender lauscht jetzt auf dem eigenen Port {neu}."); Connected(inf, own); Set(BlenderLinkState.Verbunden, $"Achtung: Auf Port {old} lauschten zwei Blender-Brücken. Trendos Blender ist jetzt auf dem eigenen Port {neu} – verbunden.", own.Id, true); return Status; } await Task.Delay(250, ct); }
            }
        }
        _dupChecked = -1;
        Set(BlenderLinkState.Getrennt, $"Achtung: Auf Port {old} antworten zwei verschiedene Blender (PID {string.Join(", ", pids)}). Trendo verbindet sich mit keinem – schließe eines oder stelle in einem einen anderen MCP-Port ein.");
        return Status;
    }

    BlenderLinkStatus Connected(JsonObject info, IBlenderProc? own)
    {
        if (PidOf(info) is { } np && _connectedPid is { } cp && np != cp && _host.BlenderPids().Contains(cp) && (own is null || own.Id == cp || own.Id == np))
        {
            Log("doppelt", $"Kennungsabfrage lieferte PID {np} statt {cp} – zwei Brücken auf Port {Port}? Trendo prüft neu.");
            _dupChecked = -1;
        }
        _connectedPid = PidOf(info);
        _fails = 0;
        var pid = info["pid"]?.GetValueKind() == JsonValueKind.Number ? info["pid"]!.GetValue<int>() : (int?)null;
        var isOwn = own is not null && pid == own.Id;
        var file = info["datei"]?.GetValueKind() == JsonValueKind.String ? (string?)info["datei"] : null;
        lock (_lock) { if (file is not null) _lastFile = file.Length > 0 ? file : null; _hold = null; }
        OpenFileDirty = (bool?)(info["geaendert"]?.GetValueKind() is JsonValueKind.True or JsonValueKind.False ? info["geaendert"] : null) ?? false;
        var was = Status.State;
        if (was is BlenderLinkState.VerbindetNeu or BlenderLinkState.Getrennt or BlenderLinkState.Startet || !_everConnected)
            Log(_everConnected ? "wieder-verbunden" : "verbunden", $"Blender {(isOwn ? "von Trendo gestartet" : "schon offen")} (PID {pid?.ToString() ?? "?"}, Port {Port}).");
        _everConnected = true;
        var name = OpenFile is { } f ? Path.GetFileName(f) : "ungespeicherte Szene";
        Set(BlenderLinkState.Verbunden, isOwn ? $"Verbunden mit Blender (von Trendo gestartet, Port {Port}) – offen: {name}." : $"Verbunden mit deinem offenen Blender (Port {Port}) – offen: {name}.", pid, isOwn);
        return Status;
    }

    async Task<BlenderLinkStatus> StartLockedAsync(CancellationToken ct, bool restart, string? file, string? hint)
    {
        if (_disposed) return Status; // Trendo fährt herunter: nichts mehr starten
        if (!File.Exists(Exe)) { Set(BlenderLinkState.Getrennt, "Blender wurde nicht gefunden – „Blender einrichten“ im 3D-Studio richtet es ein."); return Status; }
        if (await BlenderBridge.ListeningAsync(Port, TimeSpan.FromSeconds(1), ct)) // Port belegt, aber keine Blender-Brücke antwortet (Kennungsabfrage oben): eigenen freien Port nehmen
        {
            var old = Port; Port = BlenderBridge.FreePort();
            Log("port", $"Port {old} ist belegt (keine Blender-Brücke) – Trendo nimmt Port {Port}.");
        }
        IReadOnlyList<string> projArgs; string? saveAs = null;
        if (file is not null) projArgs = [file];
        else if (_lastFile is { } lf && File.Exists(lf)) projArgs = [lf];
        else (projArgs, saveAs) = ProjectArgs(ProjectDir);
        Directory.CreateDirectory(StateDir);
        var run = Guid.NewGuid().ToString("N")[..12];
        var status = Path.Combine(StateDir, $"blender-{run}.json"); var consent = Path.Combine(StateDir, $"zustimmung-{run}");
        var start = DateTimeOffset.Now.ToString("O");
        var args = new List<string>(projArgs);
        args.AddRange(ExtraArgs);
        args.AddRange(BlenderScript.Args(Path.Combine(StateDir, $"bruecke-{run}.py"), BlenderBridge.StartScript(Port, status, consent, SeparateInstance || AddonConsent, start, saveAs, hint)));
        Set(BlenderLinkState.Startet, restart ? "Blender wird nach dem Absturz neu gestartet …" : "Blender startet sichtbar mit dem Projekt …");
        IBlenderProc p;
        try { p = _host.Start(Exe, args, Env); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            Log("fehler", "Start fehlgeschlagen: " + e.Message);
            Set(BlenderLinkState.Getrennt, "Blender ließ sich nicht starten: " + e.Message);
            return Status;
        }
        lock (_lock) { _proc = p; _procStart = start; _procStatus = status; _procConsent = consent; _hold = null; }
        Starts++;
        Log(restart ? "neustart" : "start", $"Blender gestartet (PID {p.Id}, Port {Port}{(projArgs.Count > 0 ? ", " + Path.GetFileName(projArgs[0]) : "")}).");
        return await WaitReadyAsync(p, ct);
    }

    sealed record RunStatus(bool? Ok, string Grund, int? Port, string? Datei);

    static RunStatus? ReadStatus(string? file)
    {
        try
        {
            if (file is null || !File.Exists(file)) return null;
            var o = JsonNode.Parse(File.ReadAllText(file));
            return new((bool?)(o?["ok"]?.GetValueKind() is JsonValueKind.True or JsonValueKind.False ? o!["ok"] : null), (string?)o?["grund"] ?? "",
                o?["port"]?.GetValueKind() == JsonValueKind.Number ? o!["port"]!.GetValue<int>() : null, (string?)o?["datei"]);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException or FormatException) { return null; }
    }

    public const string OnlineText = "Blender erlaubt gerade keinen Online-Zugriff – ohne ihn startet die MCP-Brücke nicht. Trendo hat dir die Einstellungen geöffnet: " +
        "unter System → Netzwerk „Online-Zugriff erlauben“ einschalten. Trendo verbindet sich danach von selbst; Trendo schaltet das nicht für dich ein.";
    public const string AddonNoText = "Ohne dein OK schaltet Trendo das MCP-Add-on in deinem Blender nicht ein. Du kannst es in Blender unter Einstellungen → Add-ons → „MCP“ selbst einschalten, dann verbindet Trendo von selbst – oder „Neu verbinden“ fragt noch einmal.";

    async Task<BlenderLinkStatus> WaitReadyAsync(IBlenderProc p, CancellationToken ct)
    {
        var until = DateTimeOffset.Now + StartTimeout;
        var asked = false;
        while (DateTimeOffset.Now < until)
        {
            if (p.HasExited)
            {
                if (p.ExitCode == 0) return await HandleExitLockedAsync(p, ct); // während des Starts geschlossen
                Log("abbruch", $"Blender (PID {p.Id}) hat sich beim Start beendet{(p.ExitCode is { } x ? $", Code {x}" : "")}.");
                lock (_lock) if (ReferenceEquals(_proc, p)) _proc = null;
                Set(BlenderLinkState.Getrennt, "Blender hat sich beim Start sofort wieder beendet.");
                return Status;
            }
            var st = ReadStatus(_procStatus);
            if (st is { Port: { } sp } && sp != Port && st.Ok == true)
            {
                Log("port", $"Blender hat seine MCP-Brücke schon auf Port {sp} – Trendo übernimmt sie (nicht umgesetzt).");
                Port = sp;
            }
            if (st is { Ok: false })
            {
                switch (st.Grund)
                {
                    case "online-aus":
                        Hold(p, OnlineText); return Status;
                    case "addon-fehlt":
                        Hold(p, "In Blender fehlt das MCP-Add-on (Blender Lab „MCP“). Einrichten: Blender → Einstellungen → Erweiterungen → „MCP“ installieren. Trendo lädt nichts selbst."); return Status;
                    case "addon-aus" when !asked:
                        asked = true;
                        if (AskAddonConsent is { } ask && await ask())
                        {
                            AddonConsent = true;
                            try { File.WriteAllText(_procConsent!, "ja"); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
                            Log("zustimmung", "Die Person hat erlaubt, das MCP-Add-on einzuschalten.");
                            until = DateTimeOffset.Now + StartTimeout;
                            break;
                        }
                        Log("zustimmung", "MCP-Add-on nicht eingeschaltet (kein OK).");
                        Hold(p, AddonNoText); return Status;
                    case "addon-aus": break;
                    default:
                        Log("fehler", "MCP-Brücke: " + st.Grund);
                        Hold(p, "Die MCP-Brücke in Blender startet nicht: " + st.Grund); return Status;
                }
            }
            if (await PingAsync(ct) is { } info) return _dupChecked != Port && await DuplicateCheckLockedAsync(info, p, ct) is { } dup ? dup : Connected(info, p);
            await Task.Delay(TimeSpan.FromMilliseconds(400), ct);
        }
        Hold(p, $"Blender ist offen, die MCP-Brücke hat sich aber nach {StartTimeout.TotalSeconds:0} s nicht gemeldet (Port {Port}).");
        Log("fehler", "Brücke meldet sich nicht (Zeitgrenze Start).");
        return Status;
    }

    /// <summary>Blender läuft, die Brücke wartet auf die Person – Grund anzeigen; der Herzschlag verbindet von selbst, sobald sie läuft.</summary>
    void Hold(IBlenderProc p, string why)
    {
        lock (_lock) _hold = why;
        Set(BlenderLinkState.Getrennt, why, p.Id, true);
    }

    /// <summary>„Neu verbinden“ per Knopf: Neustart-Sperre und -Zähler zurücksetzen und verbinden (startet Blender auch nach „von dir geschlossen“).</summary>
    public Task<BlenderLinkStatus> ReconnectAsync(CancellationToken ct = default)
    {
        lock (_lock) _restarts.Clear();
        _paused = false;
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
        return new Lease(() => { lock (_lock) { if (--_leases == 0) { _watch?.Cancel(); _watch = null; } } });
    }

    /// <summary>Während Claude über MCP arbeitet: keine Kennungsabfragen im Herzschlag (kein Stau in der Brücke), nur der Port wird geprüft.</summary>
    public IDisposable Quiet()
    {
        lock (_lock) _quiet++;
        return new Lease(() => { lock (_lock) _quiet--; });
    }

    sealed class Lease(Action release) : IDisposable
    {
        int _done;
        public void Dispose() { if (Interlocked.Exchange(ref _done, 1) == 0) release(); }
    }

    /// <summary>Herzschlag (M5). Die Brücke nimmt Verbindungen nur im Hauptfaden an (Warteschlange 5). Darum: zwischen den Kennungsabfragen nur schauen, ob der
    /// Prozess lebt; keine TCP-Prüfung, solange Claude arbeitet (<see cref="Quiet"/>), ein Auftrag läuft oder die Brücke gerade nicht antwortet; nie mehr als
    /// eine offene Prüfverbindung – eine unbeantwortete bleibt offen, bis Blender antwortet (statt neu zu verbinden). Beim Blender der Person höchstens alle
    /// <see cref="ForeignProbeInterval"/>. Zeitüberschreitung beim Verbinden mit lebendem Prozess heißt „arbeitet“, nicht Abbruch.</summary>
    async Task WatchAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Heartbeat, ct);
                if (_paused || _disposed) continue;
                IBlenderProc? own; int busy, quiet; lock (_lock) { own = _proc; busy = _busy; quiet = _quiet; }
                if (own is { HasExited: true }) { await EnsureAsync(null, ct); continue; } // Absturz → Neustart; sauber geschlossen → kein Neustart
                if (own is not null && ReadStatus(_procStatus) is { Ok: true, Port: { } sp } && sp != Port) { Log("port", $"Brücke läuft jetzt auf Port {sp} – übernommen."); Port = sp; }
                if (own is null)
                {
                    if (!_everConnected) continue;
                    if (_connectedPid is { } fp && !_host.BlenderPids().Contains(fp)) // nur der Prozess, kein TCP
                    {
                        if (Status.State != BlenderLinkState.Geschlossen) Log("geschlossen", "Dein Blender wurde geschlossen – kein Neustart.");
                        Set(BlenderLinkState.Geschlossen, "Dein Blender wurde geschlossen. Trendo startet es nicht von selbst – „Neu verbinden“ öffnet es wieder.");
                        continue;
                    }
                }
                if (busy > 0 || quiet > 0) // ein Auftrag bzw. Claude arbeitet über MCP: nur der Prozess zählt
                {
                    if (busy > 0) Set(BlenderLinkState.Arbeitet, $"Blender arbeitet am Auftrag – die Verbindung auf Port {Port} steht.", own?.Id, own is not null);
                    continue;
                }
                if (_probe is { IsCompleted: false }) continue; // höchstens eine offene Prüfverbindung
                var every = own is null ? ForeignProbeInterval : ProbeInterval;
                if (DateTimeOffset.Now - _lastProbe < every && Status.Usable) continue;
                if (own is null && DateTimeOffset.Now - _lastProbe < ForeignProbeInterval) continue;
                _lastProbe = DateTimeOffset.Now;
                _probe = ProbeAsync(own, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException or InvalidOperationException) { Log("fehler", "Herzschlag: " + e.Message); }
        }
    }

    /// <summary>Eine Prüfverbindung: Kennungsabfrage senden und auf die Antwort warten – solange der Prozess lebt, auch lange (eine Verbindung, kein Stau).</summary>
    async Task ProbeAsync(IBlenderProc? own, CancellationToken ct)
    {
        var n = Interlocked.Increment(ref _openProbes); lock (_lock) MaxOpenProbes = Math.Max(MaxOpenProbes, n);
        try
        {
            HeartbeatPings++;
            using var c = new TcpClient { NoDelay = true };
            using (var cc = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                cc.CancelAfter(BlenderBridge.ConnectTimeout);
                try { await c.ConnectAsync("127.0.0.1", Port, cc.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { Busy(own); return; } // Warteschlange voll / beschäftigt: arbeitet
                catch (SocketException) { Refused(own); return; }
            }
            var s = c.GetStream();
            await s.WriteAsync(Encoding.UTF8.GetBytes(new JsonObject { ["type"] = "execute", ["code"] = BlenderBridge.PingCode, ["strict_json"] = true }.ToJsonString() + "\0"), ct);
            using var alive = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var read = ReadAnswerAsync(s, alive.Token);
            var started = DateTimeOffset.Now;
            while (!read.IsCompleted)
            {
                await Task.WhenAny(read, Task.Delay(250, ct));
                if (read.IsCompleted) break;
                if (DateTimeOffset.Now - started > PingTimeout) Busy(own);
                var gone = own is not null ? own.HasExited : _connectedPid is { } fp && !_host.BlenderPids().Contains(fp);
                if (gone || DateTimeOffset.Now - started > JobTimeout || _disposed) { alive.Cancel(); break; }
            }
            JsonObject? info = null;
            try { var r = await read; info = (string?)r?["status"] == "ok" ? r!["result"] as JsonObject : null; } catch (Exception e) when (e is OperationCanceledException or IOException or JsonException or SocketException) { }
            if (info is not null) Connected(info, own);
        }
        catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException) { Refused(own); }
        catch (OperationCanceledException) { }
        finally { Interlocked.Decrement(ref _openProbes); }
    }

    static async Task<JsonObject?> ReadAnswerAsync(NetworkStream s, CancellationToken ct)
    {
        var buf = new MemoryStream(); var chunk = new byte[65536];
        while (true)
        {
            var n = await s.ReadAsync(chunk, ct);
            if (n == 0) break;
            buf.Write(chunk, 0, n);
            if (Array.IndexOf(chunk, (byte)0, 0, n) >= 0) break;
        }
        var bytes = buf.ToArray(); var end = Array.IndexOf(bytes, (byte)0);
        return end < 0 ? null : JsonNode.Parse(Encoding.UTF8.GetString(bytes, 0, end)) as JsonObject;
    }

    void Busy(IBlenderProc? own)
    {
        if (Status.State is BlenderLinkState.Geschlossen) return;
        Set(BlenderLinkState.Arbeitet, $"Blender rechnet gerade – die Verbindung auf Port {Port} steht.", own?.Id ?? _connectedPid, own is not null);
    }

    void Refused(IBlenderProc? own)
    {
        string? hold; lock (_lock) hold = _hold;
        if (hold is not null) return; // wartet auf die Person (Online-Freigabe, Add-on) – Grund bleibt
        if (Status.State == BlenderLinkState.Arbeitet && _fails == 0) { _fails++; return; } // nach „arbeitet“ kann die Warteschlange voll sein: einmal abwarten
        _fails++;
        if (_fails == 1 || Status.State == BlenderLinkState.Arbeitet) Log("abbruch", $"Verbindung zu Blender auf Port {Port} weg.");
        if (own is null)
        {
            Set(BlenderLinkState.Getrennt, $"Die MCP-Brücke in deinem Blender ist aus (Port {Port}). In Blender unter Einstellungen → Add-ons → MCP den Server starten – Trendo verbindet dann von selbst.");
            return;
        }
        if (_fails < ListenFailures) Set(BlenderLinkState.VerbindetNeu, $"Verbindung zu Blender unterbrochen – verbinde neu (Versuch {_fails}) …", own.Id, true);
        else Set(BlenderLinkState.Getrennt, $"Blender läuft, aber die MCP-Brücke antwortet nicht (Port {Port}). Trendo prüft weiter; „Neu verbinden“ oder in Blender den MCP-Server starten.", own.Id, true);
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
                if (!st.Usable) { job.State = attempt > 0 ? "unterbrochen" : "nicht gestartet"; Log("auftrag", $"„{label}“ nicht ausgeführt: {st.Text}"); throw new BlenderLinkException($"„{label}“ wurde {(attempt > 0 ? "unterbrochen" : "nicht gestartet")}: {st.Text}", attempt > 0); }
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
                        _ = Task.Run(async () => { try { await EnsureAsync(null, CancellationToken.None); } catch (ObjectDisposedException) { } }); // Verbindung trotzdem wiederherstellen
                        throw new BlenderLinkException($"„{label}“ wurde unterbrochen ({e.Message}). Blender wird neu verbunden – bitte den Auftrag danach noch einmal starten.", true);
                    }
                    job.State = "wird wiederholt";
                    await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
                }
                catch (BlenderBridgeException e) when (e.Kind is BlenderBridgeError.Timeout or BlenderBridgeError.ConnectTimeout)
                {
                    job.State = "Zeitgrenze";
                    Log("auftrag", $"„{label}“: {e.Message}");
                    throw new BlenderLinkException($"„{label}“: {e.Message}");
                }
            }
        }
        finally { lock (_lock) _busy--; }
    }

    /// <summary>Datei im verbundenen Blender öffnen – nur, wenn dort nichts Ungespeichertes verloren geht. Danach sichert Blender in den Ordner DIESER Datei.</summary>
    public async Task<string?> OpenFileAsync(string blend, CancellationToken ct = default)
    {
        var code = "import bpy\nif bpy.data.is_dirty:\n result={'offen':False,'grund':'ungespeichert'}\nelse:\n bpy.ops.wm.open_mainfile(filepath=" + JsonSerializer.Serialize(Path.GetFullPath(blend)) + ")\n result={'offen':True,'datei':bpy.data.filepath}";
        var r = await RunAsync("Datei öffnen: " + Path.GetFileName(blend), code, false, ct);
        if ((string?)r["status"] != "ok") return "Blender konnte die Datei nicht öffnen: " + ((string?)r["message"] ?? "");
        if ((bool?)r["result"]?["offen"] != true) return "In Blender sind noch ungespeicherte Änderungen – die Datei wurde nicht geöffnet, damit nichts verloren geht.";
        lock (_lock) _lastFile = Path.GetFullPath(blend);
        ProjectDir = Path.GetDirectoryName(Path.GetFullPath(blend));
        return null;
    }

    public void Dispose()
    {
        lock (_lock) { if (_disposed) return; _disposed = true; _watch?.Cancel(); _watch = null; }
    }
}
