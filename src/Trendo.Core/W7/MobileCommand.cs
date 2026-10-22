using System.Net;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>Kleine Web-Seite fürs Handy im Heimnetz: einmaliger Code, QR auf dem PC, Not-Stopp und Freigaben. Führt nichts selbst aus.</summary>
public sealed class MobileCommandServer : IDisposable
{
    public static readonly Principal Handy = new("gekoppeltes-handy", Role.Owner);
    private readonly TrendoCore _core;
    private readonly bool _loopbackOnly;
    private readonly Func<DateTime> _now;
    private readonly string _stateFile;
    private readonly object _lock = new();
    private HttpListener? _http;
    private string? _code;
    private DateTime _codeExpires;
    private int _failed;

    public int Port { get; }
    public string PublicHost { get; private set; } = "127.0.0.1";
    public string? LanHinweis { get; private set; }
    public string? Code { get { lock (_lock) return _code; } }
    public string PageUrl => $"http://{PublicHost}:{Port}/";
    public string QrText { get { var c = Code; return c is null ? PageUrl : PageUrl + "#code=" + c; } }
    public bool IsRunning => _http?.IsListening == true;

    public MobileCommandServer(TrendoCore core, int port, bool loopbackOnly = true, Func<DateTime>? now = null)
    {
        _core = core;
        Port = port;
        _loopbackOnly = loopbackOnly;
        _now = now ?? (() => DateTime.Now);
        _stateFile = Path.Combine(core.DataRoot, "handy", "kopplung.json");
    }

    /// <summary>Freier Port ohne TcpListener. Der Core darf System.Net.Sockets nicht referenzieren.</summary>
    public static int FreePort()
    {
        for (var i = 0; i < 20; i++)
        {
            var port = System.Security.Cryptography.RandomNumberGenerator.GetInt32(20000, 45000);
            var h = new HttpListener();
            h.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                h.Start();
                h.Stop();
                h.Close();
                return port;
            }
            catch (HttpListenerException)
            {
                try { h.Close(); } catch (ObjectDisposedException) { }
            }
        }
        throw new InvalidOperationException("Kein freier Port für die Handy-Seite.");
    }

    public static IReadOnlyList<string> LanIpv4()
    {
        var list = new List<string>();
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                foreach (var u in ni.GetIPProperties().UnicastAddresses)
                {
                    var s = u.Address.ToString();
                    if (s.Contains(':')) continue;
                    if (FusionStore.IsLanAddress(s)) list.Add(s);
                }
            }
        }
        catch (System.Net.NetworkInformation.NetworkInformationException) { }
        return list;
    }

    public string NewCode()
    {
        lock (_lock)
        {
            _code = RandomNumber();
            _codeExpires = _now().AddMinutes(10);
            _failed = 0;
            return _code;
        }
    }

    public void Start()
    {
        if (_http?.IsListening == true) return;
        HttpListener Listen(IEnumerable<string> prefixes)
        {
            var h = new HttpListener();
            foreach (var p in prefixes) h.Prefixes.Add(p);
            h.Start();
            return h;
        }
        var loop = new[] { $"http://127.0.0.1:{Port}/" };
        try
        {
            if (_loopbackOnly) _http = Listen(loop);
            else
            {
                var lan = LanIpv4().FirstOrDefault();
                if (lan is null)
                {
                    _http = Listen(loop);
                    LanHinweis = "Kein Heimnetz gefunden. Die Seite ist nur auf diesem PC erreichbar.";
                }
                else
                {
                    try
                    {
                        _http = Listen([loop[0], $"http://{lan}:{Port}/"]);
                        PublicHost = lan;
                    }
                    catch (HttpListenerException)
                    {
                        _http = Listen(loop);
                        PublicHost = "127.0.0.1";
                        LanHinweis = "Im Heimnetz hat die Seite nicht gestartet. Auf diesem PC geht sie.";
                    }
                }
            }
        }
        catch (HttpListenerException ex)
        {
            throw new InvalidOperationException("Die Handy-Seite konnte nicht starten. " + ex.Message);
        }
        _ = Task.Run(Loop);
    }

    private async Task Loop()
    {
        var http = _http;
        if (http is null) return;
        while (http.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await http.GetContextAsync(); }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }
            _ = Task.Run(() => Handle(ctx));
        }
    }

    private async Task Handle(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;
        try
        {
            var ip = req.RemoteEndPoint?.Address;
            if (ip is null || !Allowed(ip) || (_loopbackOnly && !IPAddress.IsLoopback(ip)))
            {
                await Send(res, 403, new { error = "nur Heimnetz" });
                return;
            }
            var path = req.Url!.AbsolutePath.TrimEnd('/');
            if (path.Length == 0) path = "/";
            if (req.HttpMethod == "GET" && path is "/" or "/index.html")
            {
                await Html(res);
                return;
            }
            if (req.HttpMethod == "GET" && path == "/api/health")
            {
                await Send(res, 200, new { app = "trendo-handy" });
                return;
            }
            if (req.HttpMethod == "POST" && path == "/api/koppeln")
            {
                var body = await Read(req, 4096);
                var code = body is null ? "" : Pick(body, "code");
                var (token, err) = TryPair(code);
                if (token is null) { await Send(res, 403, new { error = err }); return; }
                _core.Audit.Append(Handy.Name, "handy.pair", "gekoppelt");
                await Send(res, 200, new { token });
                return;
            }
            if (!Authorized(req.Headers["Authorization"]))
            {
                await Send(res, 401, new { error = "nicht gekoppelt" });
                return;
            }
            if (req.HttpMethod == "GET" && path == "/api/zustand")
            {
                await Send(res, 200, State());
                return;
            }
            if (req.HttpMethod == "POST" && path == "/api/notstopp")
            {
                _core.NotStopp.StopAll(_core.Audit, Handy.Name, "Handy");
                await Send(res, 200, new { notstopp = true });
                return;
            }
            if (req.HttpMethod == "POST" && path == "/api/notstopp/aufheben")
            {
                _core.NotStopp.Resume(_core.Audit, Handy.Name);
                await Send(res, 200, new { notstopp = _core.NotStopp.IsStopped });
                return;
            }
            if (req.HttpMethod == "POST" && path == "/api/entkoppeln")
            {
                Unpair(req.Headers["Authorization"]);
                await Send(res, 200, new { ok = true });
                return;
            }
            if (req.HttpMethod == "POST" && path.StartsWith("/api/freigaben/", StringComparison.Ordinal))
            {
                var rest = path["/api/freigaben/".Length..];
                var slash = rest.IndexOf('/');
                if (slash <= 0) { await Send(res, 404, new { error = "unbekannt" }); return; }
                var id = rest[..slash];
                var act = rest[(slash + 1)..];
                if (!IsId(id)) { await Send(res, 400, new { error = "Kennung ungültig" }); return; }
                var pending = _core.Approvals.Pending().FirstOrDefault(r => r.Id == id);
                if (pending is null) { await Send(res, 404, new { error = "Keine offene Freigabe." }); return; }
                if (act == "annehmen")
                {
                    _core.Approvals.Approve(id, Handy);
                    await Send(res, 200, new { angenommen = true, ausgefuehrt = false, hinweis = "Angenommen. Auf dem PC auf „Jetzt ausführen“ tippen." });
                    return;
                }
                if (act == "ablehnen")
                {
                    _core.Approvals.Deny(id, Handy);
                    await Send(res, 200, new { abgelehnt = true });
                    return;
                }
            }
            await Send(res, 404, new { error = "unbekannt" });
        }
        catch (Exception e) when (e is IOException or HttpListenerException or JsonException or ApprovalException or UnauthorizedAccessException)
        {
            try { await Send(res, 400, new { error = e.Message }); } catch { }
        }
    }

    private object State() => new
    {
        notstopp = _core.NotStopp.IsStopped,
        seit = _core.NotStopp.Since,
        freigaben = _core.Approvals.Pending().Select(r => new
        {
            id = r.Id,
            text = r.Description,
            vorschau = r.Preview.Take(12).Select(l => l.Length > 240 ? l[..240] : l).ToArray(),
            bis = r.ExpiresAt,
        }).ToArray(),
    };

    private (string? Token, string Error) TryPair(string code)
    {
        lock (_lock)
        {
            var state = Load();
            var now = _now();
            var given = (code ?? "").Trim();
            if (_code is null || now > _codeExpires) return (null, "Der Code ist abgelaufen. Auf dem PC einen neuen erzeugen.");
            var ok = given.Length == 6 && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(_code), Encoding.ASCII.GetBytes(given));
            if (!ok)
            {
                if (++_failed >= 5) _code = null;
                return (null, "Code falsch.");
            }
            var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            state.Tokens.Add(CompanionState.Sha(token));
            _code = null;
            _failed = 0;
            Save(state);
            return (token, "");
        }
    }

    private bool Authorized(string? header)
    {
        if (header is null || !header.StartsWith("Bearer ", StringComparison.Ordinal)) return false;
        var hash = Encoding.ASCII.GetBytes(CompanionState.Sha(header[7..].Trim()));
        lock (_lock)
        {
            var state = Load();
            return state.Tokens.Any(t => System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(t), hash));
        }
    }

    private void Unpair(string? header)
    {
        if (header is null || !header.StartsWith("Bearer ", StringComparison.Ordinal)) return;
        var hash = CompanionState.Sha(header[7..].Trim());
        lock (_lock)
        {
            var state = Load();
            state.Tokens.RemoveAll(t => t == hash);
            Save(state);
        }
    }

    private static bool Allowed(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return FusionStore.IsLanAddress(ip.ToString());
    }

    private static bool IsId(string id) => id.Length is >= 8 and <= 64 && id.All(char.IsAsciiLetterOrDigit);

    private static string RandomNumber() => System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private sealed class PairFile { public List<string> Tokens { get; set; } = []; }

    private PairFile Load()
    {
        try
        {
            return File.Exists(_stateFile) ? JsonSerializer.Deserialize<PairFile>(File.ReadAllText(_stateFile)) ?? new() : new();
        }
        catch (JsonException) { return new(); }
    }

    private void Save(PairFile state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_stateFile)!);
        var tmp = _stateFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(tmp, _stateFile, true);
    }

    private static string Pick(string json, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";
        }
        catch (JsonException) { return ""; }
    }

    private static async Task<string?> Read(HttpListenerRequest r, int max)
    {
        if (r.ContentLength64 > max) return null;
        using var sr = new StreamReader(r.InputStream, Encoding.UTF8);
        var buf = new char[max + 1];
        var n = await sr.ReadBlockAsync(buf, 0, buf.Length);
        if (n > max) return null;
        return new string(buf, 0, n);
    }

    private static async Task Send(HttpListenerResponse r, int code, object body)
    {
        r.StatusCode = code;
        r.ContentType = "application/json; charset=utf-8";
        var b = JsonSerializer.SerializeToUtf8Bytes(body, body.GetType(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        r.ContentLength64 = b.Length;
        await r.OutputStream.WriteAsync(b);
        r.Close();
    }

    private static async Task Html(HttpListenerResponse r)
    {
        r.StatusCode = 200;
        r.ContentType = "text/html; charset=utf-8";
        var b = Encoding.UTF8.GetBytes(Page);
        r.ContentLength64 = b.Length;
        await r.OutputStream.WriteAsync(b);
        r.Close();
    }

    private const string Page = """
<!DOCTYPE html>
<html lang="de">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Trendo auf dem Handy</title>
<style>
body{font-family:sans-serif;margin:0;background:#07101c;color:#e8eef8}
main{max-width:28rem;margin:0 auto;padding:1rem}
button,input{font:inherit}
button{border:0;border-radius:10px;padding:.7rem 1rem;margin:.35rem .35rem 0 0}
.rot{background:#b91c1c;color:#fff;width:100%}
.blau{background:#1d4ed8;color:#fff}
input{padding:.6rem;width:100%;box-sizing:border-box;margin:.4rem 0}
.karte{background:#122033;border-radius:12px;padding:.8rem;margin:.6rem 0}
p{line-height:1.4}
</style>
</head>
<body>
<main>
<h1>Trendo auf dem Handy</h1>
<p id="hinweis">Kopplungscode vom PC eingeben. Der Code gilt zehn Minuten und nur einmal.</p>
<form id="kopplung">
<label>Code<input id="code" inputmode="numeric" autocomplete="one-time-code" maxlength="6"></label>
<button class="blau" type="submit">Koppeln</button>
</form>
<section id="app" hidden>
<p id="lage"></p>
<button class="rot" id="stopp" type="button">Not-Stopp</button>
<div id="liste"></div>
</section>
<script>
const key="trendo-handy";
function token(){return sessionStorage.getItem(key)||""}
async function api(path,opt){
  const r=await fetch(path,Object.assign({headers:{"Authorization":"Bearer "+token(),"Content-Type":"application/json"}},opt||{}));
  const t=await r.text();
  let j={};
  try{j=t?JSON.parse(t):{}}catch(e){j={error:t}}
  if(!r.ok) throw new Error(j.error||("Fehler "+r.status));
  return j;
}
function showApp(on){
  document.getElementById("kopplung").hidden=on;
  document.getElementById("app").hidden=!on;
}
async function laden(){
  const z=await api("/api/zustand");
  document.getElementById("lage").textContent=z.notstopp?"Not-Stopp ist aktiv. Es läuft nichts mehr automatisch.":"Automatik läuft. Not-Stopp hält sie sofort an.";
  document.getElementById("stopp").textContent=z.notstopp?"Automatik wieder erlauben":"Not-Stopp";
  const liste=document.getElementById("liste");
  liste.replaceChildren();
  const rows=z.freigaben||[];
  if(rows.length===0){const p=document.createElement("p");p.textContent="Keine offenen Freigaben.";liste.appendChild(p);}
  for(const f of rows){
    const k=document.createElement("div");k.className="karte";
    const h=document.createElement("strong");h.textContent=f.text||"";k.appendChild(h);
    for(const v of (f.vorschau||[])){const p=document.createElement("p");p.textContent=v;k.appendChild(p);}
    const a=document.createElement("button");a.className="blau";a.type="button";a.textContent="Annehmen";
    a.onclick=async()=>{await api("/api/freigaben/"+encodeURIComponent(f.id)+"/annehmen",{method:"POST"});await laden();};
    const n=document.createElement("button");n.type="button";n.textContent="Ablehnen";
    n.onclick=async()=>{await api("/api/freigaben/"+encodeURIComponent(f.id)+"/ablehnen",{method:"POST"});await laden();};
    k.append(a,n);liste.appendChild(k);
  }
}
document.getElementById("kopplung").onsubmit=async(e)=>{
  e.preventDefault();
  const code=document.getElementById("code").value.trim();
  const r=await fetch("/api/koppeln",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({code})});
  const j=await r.json();
  if(!r.ok){document.getElementById("hinweis").textContent=j.error||"Kopplung hat nicht geklappt.";return;}
  sessionStorage.setItem(key,j.token);
  showApp(true);
  try{await laden();}catch(err){document.getElementById("hinweis").textContent=err.message;}
};
document.getElementById("stopp").onclick=async()=>{
  const z=await api("/api/zustand");
  await api(z.notstopp?"/api/notstopp/aufheben":"/api/notstopp",{method:"POST"});
  await laden();
};
const hash=(location.hash.match(/code=(\d{6})/)||[])[1];
if(hash) document.getElementById("code").value=hash;
if(token()){showApp(true);laden().catch(()=>showApp(false));}
</script>
</main>
</body>
</html>
""";

    public void Dispose()
    {
        try { _http?.Stop(); _http?.Close(); } catch (ObjectDisposedException) { }
    }
}
