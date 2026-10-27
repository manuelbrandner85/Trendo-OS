using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Handy-Zugang (Vorgabe 27.10.2026, „viel einfacher, auch unterwegs“): kleiner Webserver nur im Heimnetz und im Tailscale-Netz.
/// Das Handy öffnet die Seite per QR-Code im normalen Browser – ohne App und ohne Entwicklermodus. Es kann Fotos und Dateien an den PC
/// schicken, Text und Links übergeben und Dateien aus „Zum Handy“ laden. Nichts öffentlich, kein Port im Router, jedes Gerät mit eigenem Schlüssel.</summary>
public sealed class PhoneLinkServer : IAsyncDisposable
{
    public const string Cookie = "trendo_handy";
    public const long MaxUpload = 2L << 30;
    readonly WebApplication _app;
    public IReadOnlyList<IPAddress> Addresses { get; }
    public int Port { get; }

    PhoneLinkServer(WebApplication app, IReadOnlyList<IPAddress> addresses, int port) { _app = app; Addresses = addresses; Port = port; }

    /// <summary>Startet auf den angegebenen Adressen (nur Heimnetz/Tailscale/Loopback). <paramref name="onFile"/> und <paramref name="onText"/>
    /// melden Empfangenes an Trendo (Benachrichtigung, Protokoll).</summary>
    public static async Task<PhoneLinkServer> StartAsync(PhoneAccess link, IReadOnlyList<IPAddress> addresses, int port, string inbox, string outbox, AuditLog audit,
        Action<string, string> onFile, Action<string, string> onText)
    {
        var bind = addresses.Where(PhoneAccess.AllowedAddress).Distinct().ToList();
        if (bind.Count == 0) throw new InvalidOperationException("Kein Heimnetz gefunden. Ist der PC mit dem WLAN oder LAN verbunden?");
        var b = WebApplication.CreateSlimBuilder();
        b.Logging.ClearProviders();
        b.WebHost.ConfigureKestrel(k => { foreach (var ip in bind) k.Listen(ip, port); k.Limits.MaxRequestBodySize = MaxUpload; });
        var app = b.Build();
        var hosts = bind.Select(a => a.ToString()).ToHashSet();

        // Schutz: nur Heimnetz/Tailscale, Host-Kopf muss eine eigene Adresse sein (gegen DNS-Rebinding), Antworten nicht zwischenspeichern
        app.Use(async (ctx, next) =>
        {
            var ip = ctx.Connection.RemoteIpAddress;
            if (ip is null || !PhoneAccess.AllowedAddress(ip)) { ctx.Response.StatusCode = 403; return; }
            if (!hosts.Contains(ctx.Request.Host.Host)) { ctx.Response.StatusCode = 421; return; }
            ctx.Response.Headers["Cache-Control"] = "no-store";
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
            ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src 'self' data:";
            await next();
        });

        PhoneAccess.Device? Who(HttpContext c) => link.Verify(c.Request.Cookies[Cookie]);

        app.MapGet("/p/{secret}", (string secret) =>
            link.CurrentOffer() is { } o && o.Secret == secret
                ? Results.Content(PhoneLinkPages.Pair(secret), "text/html; charset=utf-8")
                : Results.Content(PhoneLinkPages.Message("Dieser QR-Code ist abgelaufen", "Öffne in Trendo „Handy verbinden“ und scanne den neuen Code."), "text/html; charset=utf-8", Encoding.UTF8, 410));

        app.MapPost("/p/{secret}", async (HttpContext c, string secret) =>
        {
            var form = await c.Request.ReadFormAsync();
            if (link.Redeem(secret, form["name"].ToString()) is not { } r)
                return Results.Content(PhoneLinkPages.Message("Verbinden hat nicht geklappt", "Der Code ist abgelaufen oder schon benutzt. Scanne den neuen Code in Trendo."), "text/html; charset=utf-8", Encoding.UTF8, 410);
            c.Response.Cookies.Append(Cookie, r.Token, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromDays(365), IsEssential = true });
            audit.Append("handy", "phonelink.paired", $"{r.Device.Name} ({c.Connection.RemoteIpAddress})");
            onText(r.Device.Name, "\u0001verbunden");
            return Results.Redirect("/");
        });

        app.MapGet("/", (HttpContext c) => Who(c) is { } d
            ? Results.Content(PhoneLinkPages.App(d.Name, Environment.MachineName), "text/html; charset=utf-8")
            : Results.Content(PhoneLinkPages.Message("Noch nicht verbunden", "Öffne am PC in Trendo „Handy verbinden“ und scanne den QR-Code mit der Kamera."), "text/html; charset=utf-8", Encoding.UTF8, 401));

        app.MapPost("/api/upload", async (HttpContext c) =>
        {
            if (Who(c) is not { } d) return Results.Unauthorized();
            var form = await c.Request.ReadFormAsync();
            Directory.CreateDirectory(inbox);
            var saved = new List<string>();
            foreach (var f in form.Files)
            {
                var target = SafeTarget(inbox, f.FileName);
                await using (var s = File.Create(target)) await f.CopyToAsync(s);
                saved.Add(Path.GetFileName(target));
                audit.Append("handy", "phonelink.file", $"{d.Name}: {Path.GetFileName(target)} ({f.Length} B)");
                onFile(d.Name, target);
            }
            return Results.Ok(new { saved });
        });

        app.MapPost("/api/text", async (HttpContext c) =>
        {
            if (Who(c) is not { } d) return Results.Unauthorized();
            var form = await c.Request.ReadFormAsync();
            var text = form["text"].ToString();
            if (text.Length is 0 or > 20_000) return Results.BadRequest();
            audit.Append("handy", "phonelink.text", $"{d.Name}: {text.Length} Zeichen");
            onText(d.Name, text);
            return Results.Ok();
        });

        app.MapGet("/api/files", (HttpContext c) =>
        {
            if (Who(c) is null) return Results.Unauthorized();
            Directory.CreateDirectory(outbox);
            return Results.Ok(new DirectoryInfo(outbox).EnumerateFiles().Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
                .OrderByDescending(f => f.LastWriteTimeUtc).Take(200).Select(f => new { name = f.Name, size = f.Length }));
        });

        app.MapGet("/f/{name}", (HttpContext c, string name) =>
        {
            if (Who(c) is not { } d) return Results.Unauthorized();
            var path = Path.Combine(outbox, Path.GetFileName(name));
            if (Path.GetFileName(name) != name || !File.Exists(path)) return Results.NotFound();
            audit.Append("handy", "phonelink.download", $"{d.Name}: {name}");
            return Results.File(path, "application/octet-stream", name);
        });

        await app.StartAsync();
        audit.Append("handy", "phonelink.start", string.Join(", ", bind) + ":" + port);
        return new PhoneLinkServer(app, bind, port);
    }

    /// <summary>Dateiname vom Handy: nur der Name, ohne Pfad; gleiche Namen bekommen eine Nummer.</summary>
    internal static string SafeTarget(string dir, string fileName)
    {
        var name = Path.GetFileName(fileName ?? "");
        name = new string(name.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray()).Trim(' ', '.');
        if (name.Length == 0) name = $"vom-handy-{DateTime.Now:yyyyMMdd-HHmmss}";
        var target = Path.Combine(dir, name);
        for (var i = 2; File.Exists(target); i++) target = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(name)} ({i}){Path.GetExtension(name)}");
        return target;
    }

    public async ValueTask DisposeAsync() { await _app.StopAsync(); await _app.DisposeAsync(); }
}

/// <summary>Die Seiten, die das Handy sieht: schlicht, groß, ohne fremde Dateien aus dem Netz.</summary>
static class PhoneLinkPages
{
    const string Head = "<!doctype html><html lang=\"de\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta name=\"theme-color\" content=\"#0A1428\"><title>Trendo</title><style>" +
        "body{margin:0;font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;background:#050B18;color:#E6EDF7}main{max-width:520px;margin:0 auto;padding:22px 16px 40px}" +
        "h1{font-size:24px;margin:8px 0 4px}p{color:#9FB1CF;line-height:1.45}.card{background:#0E1B3A;border:1px solid #29436F;border-radius:16px;padding:16px;margin:14px 0}" +
        "button,.btn,input[type=submit]{display:block;width:100%;box-sizing:border-box;border:0;border-radius:12px;padding:16px;font-size:17px;font-weight:600;background:#2F86F6;color:#fff;margin-top:10px;text-align:center;text-decoration:none}" +
        ".sec{background:#1A2B4D}input[type=text],textarea{width:100%;box-sizing:border-box;border-radius:10px;border:1px solid #29436F;background:#081226;color:#E6EDF7;padding:12px;font-size:16px}" +
        "li{list-style:none;border-bottom:1px solid #1A2B4D;padding:10px 0}li a{color:#7CB8FF;text-decoration:none;font-weight:600}ul{padding:0;margin:0}.ok{color:#4ADE80}.logo{font-size:30px}" +
        "</style></head><body><main><div class=\"logo\">◆ Trendo</div>";
    const string Foot = "</main></body></html>";
    static string E(string s) => WebUtility.HtmlEncode(s);

    public static string Message(string title, string text) => $"{Head}<h1>{E(title)}</h1><p>{E(text)}</p>{Foot}";

    public static string Pair(string secret) => Head +
        "<h1>Mit dem PC verbinden</h1><p>Dieses Handy wird mit Trendo auf dem PC verbunden. Danach kannst du Fotos und Dateien schicken – zuhause und unterwegs.</p>" +
        $"<form class=\"card\" method=\"post\" action=\"/p/{E(secret)}\"><label>Name für dieses Handy</label><input type=\"text\" name=\"name\" id=\"n\" maxlength=\"40\" value=\"Mein Handy\">" +
        "<input type=\"submit\" value=\"Verbinden\"></form><p>Nur verbinden, wenn du gerade selbst vor deinem PC den Code gescannt hast.</p>" +
        "<script>var u=navigator.userAgent,m=u.match(/;\\s*([^;)]+)\\s+Build/);if(m)document.getElementById('n').value=m[1].slice(0,40);else if(/iPhone/.test(u))document.getElementById('n').value='iPhone';</script>" + Foot;

    public static string App(string device, string pc) => Head +
        $"<h1>Verbunden mit {E(pc)}</h1><p class=\"ok\">✓ {E(device)} ist mit Trendo verbunden.</p>" +
        "<div class=\"card\"><b>Fotos &amp; Dateien an den PC</b><p>Landen am PC im Ordner „Vom Handy“.</p><input type=\"file\" id=\"f\" multiple style=\"display:none\">" +
        "<button onclick=\"document.getElementById('f').click()\">Fotos oder Dateien auswählen</button><p id=\"st\"></p></div>" +
        "<div class=\"card\"><b>Text oder Link an den PC</b><textarea id=\"t\" rows=\"3\" placeholder=\"z. B. eine Adresse oder Notiz\"></textarea><button class=\"sec\" onclick=\"sendText()\">An den PC schicken</button><p id=\"tst\"></p></div>" +
        "<div class=\"card\"><b>Vom PC für dich</b><p>Dateien aus dem Ordner „Zum Handy“ auf dem PC.</p><ul id=\"l\"><li>Wird geladen …</li></ul></div>" +
        "<p>Tipp: Im Browser-Menü „Zum Startbildschirm hinzufügen“ – dann ist Trendo wie eine App auf dem Handy.</p>" +
        "<script>" +
        "var f=document.getElementById('f'),st=document.getElementById('st');" +
        "f.onchange=function(){if(!f.files.length)return;var d=new FormData();for(var i=0;i<f.files.length;i++)d.append('f',f.files[i]);var x=new XMLHttpRequest();x.open('POST','/api/upload');" +
        "x.upload.onprogress=function(e){if(e.lengthComputable)st.textContent='Wird gesendet … '+Math.round(e.loaded*100/e.total)+' %';};" +
        "x.onload=function(){st.textContent=x.status==200?'✓ '+f.files.length+' Datei(en) am PC angekommen.':'Senden hat nicht geklappt ('+x.status+').';f.value='';};x.onerror=function(){st.textContent='Keine Verbindung zum PC.';};x.send(d);};" +
        "function sendText(){var t=document.getElementById('t'),s=document.getElementById('tst');if(!t.value)return;var d=new FormData();d.append('text',t.value);" +
        "fetch('/api/text',{method:'POST',body:d}).then(function(r){s.textContent=r.ok?'✓ Am PC angekommen.':'Hat nicht geklappt.';if(r.ok)t.value='';}).catch(function(){s.textContent='Keine Verbindung zum PC.';});}" +
        "fetch('/api/files').then(function(r){return r.json();}).then(function(a){var l=document.getElementById('l');l.innerHTML='';if(!a.length){l.innerHTML='<li>Noch nichts da.</li>';return;}" +
        "a.forEach(function(x){var li=document.createElement('li'),k=document.createElement('a');k.href='/f/'+encodeURIComponent(x.name);k.textContent=x.name;li.appendChild(k);" +
        "li.appendChild(document.createTextNode(' · '+(x.size>1048576?(x.size/1048576).toFixed(1)+' MB':Math.max(1,Math.round(x.size/1024))+' KB')));l.appendChild(li);});}).catch(function(){});" +
        "</script>" + Foot;
}
