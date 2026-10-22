using System.Net;
using System.Text;
using System.Text.Json;
using Trendo.Core;

namespace Trendo.Companion;

/// <summary>
/// Kopfloser Trendo-Begleiter für Linux/Raspberry Pi. Feste Aufgabenliste, keine Shell-Befehle von außen.
/// Standardbindung ist nur 127.0.0.1. Alle Schnittstellen (http://+:Port/) nur, wenn loopbackOnly false ist –
/// das setzt die Kommandozeile ausschließlich bei „serve --lan“ oder der gespeicherten Einstellung „netz an“.
/// </summary>
public sealed class CompanionServer : IDisposable
{
    public static string Version => typeof(CompanionServer).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    private readonly HttpListener _http = new();
    private readonly string _stateFile;
    private readonly string _sysRoot;
    private readonly IProcessRunner _run;
    private readonly Func<DateTime> _now;
    private readonly object _lock = new();
    private readonly Action<string> _log;
    public int Port { get; }
    /// <summary>true: nur dieser Rechner. false: alle Schnittstellen, bewusst freigegeben.</summary>
    public bool LoopbackOnly { get; }
    public string ListenPrefix { get; }

    /// <summary>--local gewinnt. Sonst Netz nur mit --lan oder gespeicherter Freigabe.</summary>
    public static bool ShouldListenOnLan(bool lanSwitch, bool localSwitch, bool savedSetting)
        => !localSwitch && (lanSwitch || savedSetting);

    public static string ClientKey(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return ip.ToString();
    }

    public CompanionServer(int port, string stateFile, string sysRoot = "/", IProcessRunner? run = null, Func<DateTime>? now = null, bool loopbackOnly = true, Action<string>? log = null)
    {
        Port = port; _stateFile = stateFile; _sysRoot = sysRoot; _run = run ?? new RealProcessRunner(); _now = now ?? (() => DateTime.Now); _log = log ?? (_ => { });
        LoopbackOnly = loopbackOnly;
        ListenPrefix = loopbackOnly ? $"http://127.0.0.1:{port}/" : $"http://+:{port}/";
        _http.Prefixes.Add(ListenPrefix);
    }

    public CompanionState State() { lock (_lock) return CompanionState.Load(_stateFile); }

    public string? Tesseract() => new[] { "/usr/bin/tesseract", "/usr/local/bin/tesseract" }.FirstOrDefault(File.Exists);

    public List<string> Caps()
    {
        var c = new List<string> { "sysinfo" };
        if (Tesseract() is not null) c.Add("ocr");
        if (LinuxSysInfo.GpioAvailable(_sysRoot)) c.Add("sensor");
        if (File.Exists("/usr/bin/ffmpeg")) c.Add("video");
        return c;
    }

    public void Start() { _http.Start(); _ = Task.Run(Loop); }

    private async Task Loop()
    {
        while (_http.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _http.GetContextAsync(); } catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }
            _ = Task.Run(() => Handle(ctx));
        }
    }

    /// <summary>Nur private Netze und Loopback dürfen überhaupt fragen.</summary>
    public static bool AllowedRemote(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return FusionStore.IsLanAddress(ip.ToString());
    }

    private async Task Handle(HttpListenerContext ctx)
    {
        var req = ctx.Request; var res = ctx.Response;
        try
        {
            if (!AllowedRemote(req.RemoteEndPoint.Address)) { await Send(res, 403, new { error = "nur Heimnetz" }); return; }
            var path = req.Url!.AbsolutePath.TrimEnd('/');
            _log($"{req.HttpMethod} {path} von {req.RemoteEndPoint.Address}");
            switch (req.HttpMethod, path)
            {
                case ("GET", "/health"):
                    var s0 = State();
                    await Send(res, 200, new CompanionHealth("trendo-companion", Version, s0.Name, LinuxSysInfo.Kind(LinuxSysInfo.Model(_sysRoot)))); return;
                case ("POST", "/pair"):
                    var pr = await Read<PairRequest>(req, 4096);
                    var ip = ClientKey(req.RemoteEndPoint.Address);
                    string? token; string err; CompanionState st; PairDecision gate; int? retry = null;
                    lock (_lock)
                    {
                        st = CompanionState.Load(_stateFile);
                        var now = _now();
                        gate = st.BeginPair(ip, now);
                        if (gate.Gate == PairGate.Ok && pr is not null)
                        {
                            (token, err) = st.TryPair(pr.Code, pr.PcName, now);
                            if (token is null)
                            {
                                st.NotePairFailure(ip, now);
                                var (locked, sec) = st.FreshLock(ip, now);
                                if (locked is not null) { err = locked; retry = sec; }
                            }
                            else st.NotePairSuccess(ip);
                        }
                        else { token = null; err = gate.Error; }
                        st.Save(_stateFile);
                    }
                    if (gate.Gate == PairGate.RateLimited) { _log($"Rate-Limit {ip}"); await Send(res, 429, new { error = gate.Error }, gate.RetryAfterSeconds); return; }
                    if (gate.Gate == PairGate.Locked) { _log($"Sperre {ip}"); await Send(res, 403, new { error = gate.Error }, gate.RetryAfterSeconds); return; }
                    if (pr is null) { await Send(res, 400, new { error = "Anfrage ungültig" }); return; }
                    if (token is null) { await Send(res, 403, new { error = err }, retry); return; }
                    _log($"gekoppelt mit {pr.PcName}");
                    await Send(res, 200, new PairResponse(token, st.Name, LinuxSysInfo.Kind(LinuxSysInfo.Model(_sysRoot)), Caps(), Version)); return;
            }
            var state = State();
            var authErr = state.AuthError(req.Headers["Authorization"], _now());
            if (authErr is not null) { await Send(res, 401, new { error = authErr }); return; }
            switch (req.HttpMethod, path)
            {
                case ("GET", "/info"):
                    var model = LinuxSysInfo.Model(_sysRoot); var (tot, free) = LinuxSysInfo.Mem(_sysRoot);
                    await Send(res, 200, new CompanionInfo(state.Name, LinuxSysInfo.Kind(model), model, System.Runtime.InteropServices.RuntimeInformation.OSDescription, Environment.ProcessorCount, tot, free, LinuxSysInfo.TempC(_sysRoot), LinuxSysInfo.Uptime(_sysRoot), Caps())); return;
                case ("POST", "/task/ocr"):
                    var tess = Tesseract();
                    if (tess is null) { await Send(res, 409, new { error = "Tesseract fehlt auf diesem Gerät" }); return; }
                    if (req.ContentLength64 is <= 0 or > 10_000_000) { await Send(res, 413, new { error = "Bild fehlt oder > 10 MB" }); return; }
                    var dir = Path.Combine(Path.GetTempPath(), "trendo-comp-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
                    try
                    {
                        var img = Path.Combine(dir, "bild.png");
                        await using (var f = File.Create(img)) await req.InputStream.CopyToAsync(f);
                        var lang = req.QueryString["lang"] is "eng" ? "eng" : "deu";
                        var r = await Ocr.RunAsync(_run, tess, img, default, null, lang);
                        if (!r.Ok) { await Send(res, 500, new { error = r.Message }); return; }
                        await Send(res, 200, new OcrResult(r.Text, r.Confidence, r.Unreadable));
                    }
                    finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
                    return;
                case ("POST", "/unpair"):
                    lock (_lock) { var s = CompanionState.Load(_stateFile); s.Unpair(req.Headers["Authorization"]!); s.Save(_stateFile); }
                    await Send(res, 200, new { ok = true }); return;
                default:
                    await Send(res, 404, new { error = "unbekannt" }); return;
            }
        }
        catch (Exception e) when (e is IOException or HttpListenerException or JsonException) { try { await Send(res, 400, new { error = "Fehler" }); } catch { } }
    }

    private static async Task<T?> Read<T>(HttpListenerRequest r, int max)
    {
        if (r.ContentLength64 > max) return default;
        using var sr = new StreamReader(r.InputStream, Encoding.UTF8);
        var buf = new char[max + 1]; var n = await sr.ReadBlockAsync(buf, 0, buf.Length);
        if (n > max) return default;
        try { return JsonSerializer.Deserialize<T>(new string(buf, 0, n), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch (JsonException) { return default; }
    }

    private static async Task Send(HttpListenerResponse r, int code, object body, int? retryAfterSeconds = null)
    {
        r.StatusCode = code; r.ContentType = "application/json; charset=utf-8";
        if (retryAfterSeconds is int sec) r.Headers["Retry-After"] = sec.ToString();
        var b = JsonSerializer.SerializeToUtf8Bytes(body, body.GetType());
        r.ContentLength64 = b.Length; await r.OutputStream.WriteAsync(b); r.Close();
    }

    public void Dispose()
    {
        try { if (_http.IsListening) _http.Stop(); _http.Close(); }
        catch (Exception e) when (e is ObjectDisposedException or HttpListenerException) { }
    }
}
