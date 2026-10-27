using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Wunsch 27.10.2026 („das Betriebssystem auf dem Handy voll nutzen“, „wie eine Handy-App“): Trendo als App auf dem Handy.
/// Die Programme rechnet der PC in Handy-Größe (Hochformat, Breite des Handys) – kein verkleinerter PC-Bildschirm. Das Handy zeigt das Bild,
/// tippt, schreibt, wählt und wischt darin. Nur für Handys, die der PC-Nutzer dafür freigegeben hat.</summary>
public interface IPhoneRemote
{
    IReadOnlyList<PhoneApp> Apps();
    /// <summary>App/Studio auf dem Handy öffnen; liefert den Titel.</summary>
    Task<string?> OpenAsync(string id);
    /// <summary>Zurück; null = wieder auf dem Handy-Startbildschirm.</summary>
    Task<string?> BackAsync();
    /// <summary>Bild der Handy-Ansicht (<paramref name="desktop"/>: der ganze PC-Bildschirm). Null, wenn gerade nichts offen ist.</summary>
    Task<PhoneFrame?> ScreenAsync(int width, int height, double scale, bool desktop);
    Task<PhoneHit> TapAsync(double fx, double fy, bool desktop);
    Task SetTextAsync(string text);
    Task ChooseAsync(int index);
    Task KeyAsync(string key);
    Task ScrollAsync(double fx, double fy, double dy, bool desktop);
    byte[]? Icon();
    /// <summary>Symbol einer App – dieselbe Kachel wie auf dem Trendo-Desktop (PNG).</summary>
    Task<byte[]?> AppIconAsync(string id);
    void Started(string device);
}

public sealed record PhoneApp(string Id, string Name, string Area, string Icon, bool Home = false);
public sealed record PhoneFrame(byte[] Png, string Title);
/// <summary>Was ein Fingertipp getroffen hat: Knopf, Eingabefeld (mit Text), Auswahl (mit Möglichkeiten) …</summary>
public sealed record PhoneHit(string Kind, string? Text = null, IReadOnlyList<string>? Choices = null, int Selected = -1, bool Multiline = false);

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
        Action<string, string> onFile, Action<string, string> onText, IPhoneRemote? remote = null)
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
            ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src 'self' data: blob:";
            await next();
        });

        PhoneAccess.Device? Who(HttpContext c) => link.Verify(c.Request.Cookies[Cookie]);
        // Dauerhaft verbunden: bei jedem Öffnen gilt der Schlüssel im Handy wieder ein volles Jahr
        bool Keep(HttpContext c)
        {
            c.Response.Cookies.Append(Cookie, c.Request.Cookies[Cookie]!, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromDays(365), IsEssential = true });
            return true;
        }

        app.MapGet("/p/{secret}", (HttpContext c, string secret) =>
        {
            if (c.Connection.RemoteIpAddress is { } from) link.MarkReached(from); // Assistent: „Handy hat den PC erreicht“
            return link.CurrentOffer() is { } o && o.Secret == secret
                ? Results.Content(PhoneLinkPages.Pair(secret), "text/html; charset=utf-8")
                : Results.Content(PhoneLinkPages.Message("Dieser QR-Code ist abgelaufen", "Öffne in Trendo „Handy verbinden“ und scanne den neuen Code."), "text/html; charset=utf-8", Encoding.UTF8, 410);
        });

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

        var away = bind.FirstOrDefault(PhoneAccess.IsTailscale);
        app.MapGet("/", (HttpContext c) => Who(c) is { } d && Keep(c)
            ? Results.Content(PhoneLinkPages.App(d.Name, Environment.MachineName, remote is not null && d.Remote, away is not null && !PhoneAccess.IsTailscale(c.Connection.LocalIpAddress ?? IPAddress.Loopback)), "text/html; charset=utf-8")
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

        // Unterwegs-Zugang: das verbundene Handy bekommt einen Link auf die Tailscale-Adresse (eigener Schlüssel dort)
        app.MapPost("/api/away", (HttpContext c) =>
        {
            if (Who(c) is not { } d) return Results.Unauthorized();
            if (away is null) return Results.NotFound();
            audit.Append("handy", "phonelink.away", d.Name);
            return Results.Ok(new { url = $"http://{away}:{port}/t/{link.NewTransfer(d.Id)}" });
        });
        app.MapGet("/t/{code}", (HttpContext c, string code) =>
        {
            if (link.RedeemTransfer(code) is not { } r)
                return Results.Content(PhoneLinkPages.Message("Link abgelaufen", "Öffne Trendo zuhause im WLAN und tippe noch einmal auf „Unterwegs-Zugang einrichten“."), "text/html; charset=utf-8", Encoding.UTF8, 410);
            c.Response.Cookies.Append(Cookie, r.Token, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromDays(365), IsEssential = true });
            audit.Append("handy", "phonelink.away.ok", r.Device.Name);
            return Results.Redirect("/?unterwegs=1");
        });

        // Trendo als Handy-App – nur mit Erlaubnis am PC
        IResult? Denied(HttpContext c, out PhoneAccess.Device? d)
        {
            d = Who(c);
            if (d is null) return Results.Unauthorized();
            if (remote is null || !d.Remote) return Results.Content("Am PC in Trendo unter „Handy“ erlauben, dass dieses Handy Trendo bedienen darf.", "text/plain; charset=utf-8", Encoding.UTF8, 403);
            return null;
        }
        static double Num(string? v) => double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) && double.IsFinite(x) ? x : -1;
        static bool Desk(HttpContext c) => c.Request.Query["pc"] == "1";
        app.MapGet("/fern", () => Results.Redirect("/app"));
        app.MapGet("/app", (HttpContext c) =>
        {
            if (Denied(c, out var d) is { } no) return no is Microsoft.AspNetCore.Http.HttpResults.UnauthorizedHttpResult ? Results.Redirect("/") : no;
            remote!.Started(d!.Name);
            audit.Append("handy", "phonelink.remote.open", d.Name);
            return Results.Content(PhoneLinkPages.MobileApp(d.Name), "text/html; charset=utf-8");
        });
        app.MapGet("/manifest.webmanifest", () => Results.Text(PhoneLinkPages.Manifest, "application/manifest+json; charset=utf-8"));
        app.MapGet("/icon.png", () => remote?.Icon() is { } png ? Results.File(png, "image/png") : Results.NotFound());
        app.MapGet("/api/icon/{id}", async (HttpContext c, string id) =>
        {
            if (Denied(c, out _) is { } no) return no;
            if (!remote!.Apps().Any(a => a.Id == id)) return Results.NotFound();
            c.Response.Headers.CacheControl = "private, max-age=86400";
            return await remote.AppIconAsync(id) is { } png ? Results.File(png, "image/png") : Results.NotFound();
        });
        app.MapGet("/api/apps", (HttpContext c) => Denied(c, out _) is { } no ? no : Results.Ok(remote!.Apps()));
        app.MapPost("/api/open", async (HttpContext c) =>
        {
            if (Denied(c, out _) is { } no) return no;
            var id = (await c.Request.ReadFormAsync())["id"].ToString();
            if (!remote!.Apps().Any(a => a.Id == id)) return Results.NotFound();
            return Results.Ok(new { title = await remote.OpenAsync(id) });
        });
        app.MapPost("/api/back", async (HttpContext c) => Denied(c, out _) is { } no ? no : Results.Ok(new { title = await remote!.BackAsync() }));
        app.MapGet("/api/screen", async (HttpContext c) =>
        {
            if (Denied(c, out _) is { } no) return no;
            var w = (int)Math.Clamp(Num(c.Request.Query["w"]), 280, 1600);
            var h = (int)Math.Clamp(Num(c.Request.Query["h"]), 300, 2400);
            var sc = Math.Clamp(Num(c.Request.Query["s"]), 1, 2.5);
            if (await remote!.ScreenAsync(w, h, sc, Desk(c)) is not { } f) return Results.NoContent();
            var tag = "\"" + Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData(f.Png)) + "\"";
            c.Response.Headers["X-Trendo-Title"] = Uri.EscapeDataString(f.Title);
            c.Response.Headers.ETag = tag;
            if (c.Request.Headers.IfNoneMatch == tag) return Results.StatusCode(304); // unverändert: nichts übertragen
            return Results.File(f.Png, "image/png");
        });
        app.MapPost("/api/tap", async (HttpContext c) =>
        {
            if (Denied(c, out _) is { } no) return no;
            var f = await c.Request.ReadFormAsync();
            double x = Num(f["x"]), y = Num(f["y"]);
            if (x is < 0 or > 1 || y is < 0 or > 1) return Results.BadRequest();
            return Results.Ok(await remote!.TapAsync(x, y, Desk(c)));
        });
        app.MapPost("/api/settext", async (HttpContext c) =>
        {
            if (Denied(c, out _) is { } no) return no;
            var t = (await c.Request.ReadFormAsync())["text"].ToString();
            if (t.Length > 100_000) return Results.BadRequest();
            await remote!.SetTextAsync(t); return Results.Ok();
        });
        app.MapPost("/api/type", async (HttpContext c) =>
        {
            if (Denied(c, out _) is { } no) return no;
            var t = (await c.Request.ReadFormAsync())["text"].ToString();
            if (t.Length is 0 or > 5000) return Results.BadRequest();
            await remote!.SetTextAsync("\u0001" + t); return Results.Ok(); // anhängen
        });
        app.MapPost("/api/choose", async (HttpContext c) =>
        {
            if (Denied(c, out _) is { } no) return no;
            var i = (int)Num((await c.Request.ReadFormAsync())["i"]);
            if (i < 0) return Results.BadRequest();
            await remote!.ChooseAsync(i); return Results.Ok();
        });
        app.MapPost("/api/key", async (HttpContext c) =>
        {
            if (Denied(c, out _) is { } no) return no;
            var k = (await c.Request.ReadFormAsync())["key"].ToString();
            if (k is not ("enter" or "back" or "esc" or "tab")) return Results.BadRequest();
            await remote!.KeyAsync(k); return Results.Ok();
        });
        app.MapPost("/api/scroll", async (HttpContext c) =>
        {
            if (Denied(c, out _) is { } no) return no;
            var f = await c.Request.ReadFormAsync();
            double x = Num(f["x"]), y = Num(f["y"]);
            var dy = double.TryParse(f["dy"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? Math.Clamp(v, -3, 3) : 0;
            if (x is < 0 or > 1 || y is < 0 or > 1) return Results.BadRequest();
            await remote!.ScrollAsync(x, y, dy, Desk(c)); return Results.Ok();
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

    public const string Manifest = """
        {"name":"Trendo","short_name":"Trendo","start_url":"/app","scope":"/","display":"standalone","orientation":"portrait",
         "background_color":"#050B18","theme_color":"#0A1428","icons":[{"src":"/icon.png","sizes":"512x512","type":"image/png","purpose":"any maskable"}]}
        """;

    /// <summary>Trendo als Handy-App: Startseite, alle Apps, offene App bildschirmfüllend, Leiste unten. Fühlt sich an wie eine echte App –
    /// zum Startbildschirm hinzugefügt sogar ohne Browserleisten.</summary>
    public static string MobileApp(string device) => MobileHtml.Replace("{{DEVICE}}", E(device));

    const string MobileHtml = """
<!doctype html><html lang="de"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no,viewport-fit=cover">
<meta name="theme-color" content="#0A1428"><meta name="mobile-web-app-capable" content="yes"><meta name="apple-mobile-web-app-capable" content="yes">
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent"><meta name="apple-mobile-web-app-title" content="Trendo">
<link rel="manifest" href="/manifest.webmanifest"><link rel="icon" href="/icon.png"><link rel="apple-touch-icon" href="/icon.png"><title>Trendo</title>
<style>
*{box-sizing:border-box;-webkit-tap-highlight-color:transparent}html,body{margin:0;height:100%;overflow:hidden;background:#050B18;color:#E6EDF7;font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;overscroll-behavior:none}
#top{position:fixed;left:0;right:0;top:0;height:calc(52px + env(safe-area-inset-top));padding:env(safe-area-inset-top) 10px 0;display:flex;align-items:center;gap:8px;
background:linear-gradient(180deg,#0E1B3A,#0A1428);border-bottom:1px solid #F5C45133;z-index:5}
#back{width:40px;height:40px;border:0;border-radius:12px;background:#ffffff14;color:#F5C451;font-size:24px;display:none}
#title{font-size:18px;font-weight:650;flex:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
#dot{width:9px;height:9px;border-radius:50%;background:#4ADE80;box-shadow:0 0 8px #4ADE80}
#main{position:fixed;left:0;right:0;top:calc(52px + env(safe-area-inset-top));bottom:calc(64px + env(safe-area-inset-bottom));overflow:hidden}
.pane{position:absolute;inset:0;overflow-y:auto;-webkit-overflow-scrolling:touch;padding:16px 14px 24px}
#view{padding:0;overflow:hidden;display:none;background:#0A1428}#view img{display:block;width:100%;height:100%;touch-action:none;user-select:none;-webkit-user-select:none}
#tabs{position:fixed;left:0;right:0;bottom:0;height:calc(64px + env(safe-area-inset-bottom));padding-bottom:env(safe-area-inset-bottom);display:flex;
background:linear-gradient(0deg,#050B18,#0E1B3A);border-top:1px solid #F5C45133;z-index:5}
#tabs button{flex:1;border:0;background:none;color:#9FB1CF;font-size:11px;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:3px}
#tabs button.on{color:#F5C451}#tabs svg{width:24px;height:24px;fill:currentColor}
h2{font-size:15px;color:#9FB1CF;font-weight:600;margin:18px 4px 8px}.hello{font-size:26px;font-weight:700;margin:4px 4px 2px;background:linear-gradient(90deg,#FFF1B8,#F5C451,#3B82F6);-webkit-background-clip:text;background-clip:text;color:transparent}
.sub{color:#9FB1CF;margin:0 4px 6px;font-size:14px}
input.search{width:100%;padding:13px 16px;border-radius:14px;border:1px solid #29436F;background:#0E1B3A;color:#E6EDF7;font-size:16px;margin-top:10px}
.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:14px 8px}
.app{display:flex;flex-direction:column;align-items:center;gap:6px;background:none;border:0;color:#E6EDF7;font-size:11.5px;text-align:center;line-height:1.2;padding:0}
.ico{width:58px;height:58px;border-radius:17px;display:flex;align-items:center;justify-content:center;
background:radial-gradient(120% 120% at 30% 15%,#2B4C86 0%,#0E1B3A 70%);border:1px solid #F5C45155;box-shadow:0 6px 16px #0008,inset 0 1px 0 #ffffff22}
.ico svg{width:30px;height:30px;fill:url(#gold)}.ico img{width:100%;height:100%;border-radius:inherit;display:block}.ico{overflow:hidden;padding:0}.app:active .ico{transform:scale(.92)}
.chips{display:flex;gap:8px;overflow-x:auto;padding:2px 2px 6px}.chip{flex:none;border:1px solid #29436F;background:#0E1B3A;color:#E6EDF7;border-radius:20px;padding:8px 13px;font-size:13px}
.chip.on{background:#2F86F6;border-color:#2F86F6}
.row{display:flex;align-items:center;gap:12px;padding:10px 6px;border-bottom:1px solid #ffffff10;background:none;border-left:0;border-right:0;border-top:0;color:#E6EDF7;width:100%;text-align:left;font-size:15px}
.row .ico{width:42px;height:42px;border-radius:12px}.row .ico svg{width:22px;height:22px}
#sheet{position:fixed;left:0;right:0;bottom:0;background:#0E1B3A;border-radius:20px 20px 0 0;border-top:1px solid #F5C45155;padding:14px 14px calc(14px + env(safe-area-inset-bottom));
transform:translateY(110%);transition:transform .2s;z-index:9;max-height:70%;overflow:auto;box-shadow:0 -10px 30px #000a}#sheet.open{transform:none}
#sheet textarea,#sheet input{width:100%;font-size:17px;padding:12px;border-radius:12px;border:1px solid #29436F;background:#081226;color:#E6EDF7}
.btn{display:block;width:100%;border:0;border-radius:12px;padding:14px;font-size:16px;font-weight:600;background:#2F86F6;color:#fff;margin-top:10px}
.sec{background:#1A2B4D}.opt{display:block;width:100%;text-align:left;border:0;border-bottom:1px solid #ffffff10;background:none;color:#E6EDF7;font-size:16px;padding:14px 6px}.opt.on{color:#F5C451}
.clock{font-size:54px;font-weight:200;letter-spacing:1px;margin:6px 4px 0;color:#E6EDF7;text-shadow:0 2px 18px #3B82F655}
.empty{color:#9FB1CF;text-align:center;margin:40px 10px;line-height:1.5;font-size:14px}.empty .btn{max-width:240px;margin:16px auto 0}
.app.wiggle .ico{animation:wig .25s infinite alternate}@keyframes wig{from{transform:rotate(-2deg)}to{transform:rotate(2deg)}}
#busy{position:fixed;top:calc(56px + env(safe-area-inset-top));left:50%;transform:translateX(-50%);background:#0E1B3Aee;border:1px solid #29436F;border-radius:20px;padding:6px 14px;font-size:13px;display:none;z-index:8}
</style></head><body>
<svg width="0" height="0" style="position:absolute"><defs><linearGradient id="gold" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#FFF1B8"/><stop offset=".5" stop-color="#F5C451"/><stop offset="1" stop-color="#3B82F6"/></linearGradient></defs></svg>
<div id="top"><button id="back" aria-label="Zurück">‹</button><div id="title">Trendo</div><div id="dot" title="verbunden"></div></div>
<div id="main">
 <div class="pane" id="home"><div class="clock" id="clock">--:--</div><div class="hello" id="hello">Hallo!</div><div class="sub">{{DEVICE}} · verbunden mit deinem PC</div>
  <input class="search" id="q" type="search" placeholder="Apps suchen …"><div class="grid" id="homeApps" style="margin-top:18px"></div>
  <div id="homeEmpty" class="empty">Dein Startbildschirm ist aufgeräumt.<br>Unter <b>Apps</b> eine App <b>lange gedrückt halten</b> → „Zum Startbildschirm“.<button class="btn sec" id="goApps">Apps ansehen</button></div></div>
 <div class="pane" id="apps" style="display:none"><input class="search" id="q2" type="search" placeholder="Suchen …"><div id="list"></div></div>
 <div class="pane" id="view"><img id="img" alt="Trendo"></div>
</div>
<div id="busy">Lädt …</div>
<div id="sheet"></div>
<div id="tabs">
 <button id="tHome" class="on"><svg viewBox="0 0 24 24"><path d="M10,20V14H14V20H19V12H22L12,3L2,12H5V20H10Z"/></svg>Start</button>
 <button id="tApps"><svg viewBox="0 0 24 24"><path d="M16,20H20V16H16M16,14H20V10H16M10,8H14V4H10M16,8H20V4H16M10,14H14V10H10M4,14H8V10H4M4,20H8V16H4M10,20H14V16H10M4,8H8V4H4V8Z"/></svg>Apps</button>
 <button id="tOpen"><svg viewBox="0 0 24 24"><path d="M4,4H20A2,2 0 0,1 22,6V18A2,2 0 0,1 20,20H4A2,2 0 0,1 2,18V6A2,2 0 0,1 4,4M4,8V18H20V8H4Z"/></svg>Offen</button>
 <button id="tPc"><svg viewBox="0 0 24 24"><path d="M21,16H3V4H21M21,2H3C1.89,2 1,2.89 1,4V16A2,2 0 0,0 3,18H10V20H8V22H16V20H14V18H21A2,2 0 0,0 23,16V4C23,2.89 22.1,2 21,2Z"/></svg>PC</button>
 <button id="tFiles"><svg viewBox="0 0 24 24"><path d="M9,16V10H5L12,3L19,10H15V16H9M5,20V18H19V20H5Z"/></svg>Senden</button>
</div>
<script>
var $=function(i){return document.getElementById(i)},apps=[],area=null,mode='home',pc=false,busy=false,etag=null,url=null,title='Trendo',timer=null,y0=null,x0=null,t0=0,hit=null,deb=null;
var recent=JSON.parse(localStorage.getItem('trendoRecent')||'[]'),homeIds=JSON.parse(localStorage.getItem('trendoHome')||'null'),press=null,pressed=false;
function saveHome(){try{localStorage.setItem('trendoHome',JSON.stringify(homeIds));}catch(e){}}
function post(p,d){var f=new FormData();for(var k in d)f.append(k,d[k]);return fetch(p+(pc&&p.indexOf('/api/')==0&&p!='/api/open'&&p!='/api/back'?'?pc=1':''),{method:'POST',body:f});}
function icon(a){return '<div class="ico"><img src="/api/icon/'+encodeURIComponent(a.id)+'" alt="" loading="lazy"></div>';}
function tile(a){return '<button class="app" data-id="'+a.id+'">'+icon(a)+'<span>'+esc(a.name)+'</span></button>';}
function esc(s){return String(s).replace(/[&<>"]/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c];});}
function hello(){var h=new Date().getHours();$('hello').textContent=h<11?'Guten Morgen!':h<18?'Hallo!':'Guten Abend!';}
function renderHome(){var q=$('q').value.toLowerCase();
 var list=q?apps.filter(function(a){return a.name.toLowerCase().indexOf(q)>=0;}):(homeIds||[]).map(function(id){return apps.find(function(a){return a.id==id;});}).filter(Boolean);
 $('homeApps').innerHTML=list.map(tile).join('');$('homeEmpty').style.display=!q&&!list.length?'block':'none';}
function tick(){var d=new Date();$('clock').textContent=('0'+d.getHours()).slice(-2)+':'+('0'+d.getMinutes()).slice(-2);}
function renderList(){var q=$('q2').value.toLowerCase();$('list').innerHTML=apps.filter(function(a){return !q||a.name.toLowerCase().indexOf(q)>=0||a.area.toLowerCase().indexOf(q)>=0;})
 .sort(function(a,b){return a.name.localeCompare(b.name,'de');}).map(function(a){return '<button class="row app" data-id="'+a.id+'" style="flex-direction:row">'+icon(a)+'<div><div>'+esc(a.name)+'</div><div style="color:#9FB1CF;font-size:12px">'+esc(a.area)+'</div></div></button>';}).join('');}
function show(m){mode=m;['home','apps','view'].forEach(function(p){$(p).style.display=(p==m||(m=='pc'&&p=='view'))?'block':'none';});
 ['tHome','tApps','tOpen','tPc'].forEach(function(t){$(t).classList.remove('on');});$({home:'tHome',apps:'tApps',view:'tOpen',pc:'tPc'}[m]).classList.add('on');
 pc=m=='pc';$('back').style.display=m=='view'?'block':'none';$('title').textContent=m=='home'?'Trendo':m=='apps'?'Alle Apps':m=='pc'?'PC-Bildschirm':title;
 etag=null;clearInterval(timer);if(m=='view'||m=='pc'){load();timer=setInterval(load,600);}}
function size(){var v=$('view');return {w:Math.round(v.clientWidth),h:Math.round(v.clientHeight),s:Math.min(2.5,Math.max(1,window.devicePixelRatio||1)).toFixed(2)};}
function load(){if(busy||document.hidden)return;busy=true;var z=size();
 fetch('/api/screen?w='+z.w+'&h='+z.h+'&s='+z.s+(pc?'&pc=1':''),{headers:etag?{'If-None-Match':etag}:{}}).then(function(r){
  if(r.status==204){busy=false;if(!pc){show('home');}return null;}if(r.status==304){busy=false;return null;}if(!r.ok)throw r.status;
  etag=r.headers.get('ETag');var t=r.headers.get('X-Trendo-Title');if(t&&!pc){title=decodeURIComponent(t);$('title').textContent=title;}return r.blob();})
 .then(function(b){if(!b)return;var u=URL.createObjectURL(b);var i=$('img');i.onload=function(){if(url)URL.revokeObjectURL(url);url=u;};i.src=u;busy=false;$('dot').style.background='#4ADE80';})
 .catch(function(e){busy=false;$('dot').style.background=e==403?'#FACC15':'#EF4444';});}
function refresh(ms){etag=null;setTimeout(load,ms||120);}
function open(id){$('busy').style.display='block';recent=[id].concat(recent.filter(function(x){return x!=id;})).slice(0,12);try{localStorage.setItem('trendoRecent',JSON.stringify(recent));}catch(e){}
 post('/api/open',{id:id}).then(function(r){return r.json();}).then(function(j){title=j.title||'Trendo';$('busy').style.display='none';show('view');}).catch(function(){$('busy').style.display='none';});}
document.addEventListener('click',function(e){if(pressed){pressed=false;e.preventDefault();return;}var b=e.target.closest('[data-id]');if(b){open(b.getAttribute('data-id'));return;}});
document.addEventListener('pointerdown',function(e){var b=e.target.closest('[data-id]');if(!b)return;clearTimeout(press);press=setTimeout(function(){pressed=true;if(navigator.vibrate)navigator.vibrate(20);b.classList.add('wiggle');homeSheet(b.getAttribute('data-id'),b);},480);});
['pointerup','pointercancel','pointermove'].forEach(function(t){document.addEventListener(t,function(e){if(t!='pointermove'||Math.abs(e.movementY)>4||Math.abs(e.movementX)>4)clearTimeout(press);});});
document.addEventListener('contextmenu',function(e){if(e.target.closest('[data-id]'))e.preventDefault();});
function homeSheet(id,b){var a=apps.find(function(x){return x.id==id;});if(!a)return;homeIds=homeIds||[];var on=homeIds.indexOf(id)>=0;
 $('sheet').innerHTML='<div style="display:flex;align-items:center;gap:12px;margin-bottom:6px">'+icon(a)+'<b>'+esc(a.name)+'</b></div>'+
  '<button class="btn" id="hs">'+(on?'Vom Startbildschirm entfernen':'Zum Startbildschirm')+'</button><button class="btn sec" id="ho">Öffnen</button><button class="btn sec" id="hc">Abbrechen</button>';
 $('sheet').classList.add('open');var done=function(){$('sheet').classList.remove('open');b.classList.remove('wiggle');};
 $('hs').onclick=function(){if(on)homeIds=homeIds.filter(function(x){return x!=id;});else homeIds.push(id);saveHome();renderHome();done();};
 $('ho').onclick=function(){done();open(id);};$('hc').onclick=done;}
$('goApps').onclick=function(){renderList();show('apps');};
$('q').oninput=renderHome;$('q2').oninput=renderList;
$('tHome').onclick=function(){show('home');};$('tApps').onclick=function(){renderList();show('apps');};$('tOpen').onclick=function(){show('view');};$('tPc').onclick=function(){show('pc');};
$('tFiles').onclick=function(){location.href='/';};
$('back').onclick=function(){post('/api/back',{}).then(function(r){return r.json();}).then(function(j){if(!j.title)show('home');else{title=j.title;$('title').textContent=title;refresh();}});};
function pos(e){var r=$('img').getBoundingClientRect();return {x:Math.min(1,Math.max(0,(e.clientX-r.left)/r.width)).toFixed(4),y:Math.min(1,Math.max(0,(e.clientY-r.top)/r.height)).toFixed(4)};}
var img=$('img');
img.addEventListener('pointerdown',function(e){y0=e.clientY;x0=e.clientX;t0=Date.now();});
img.addEventListener('pointerup',function(e){if(y0===null)return;var dy=e.clientY-y0,p=pos(e);
 if(Math.abs(dy)>18){var q=pos({clientX:x0,clientY:y0});var v=-dy/img.getBoundingClientRect().height*(Date.now()-t0<250?2.2:1.2);post('/api/scroll',{x:q.x,y:q.y,dy:v.toFixed(3)}).then(function(){refresh(60);});}
 else{if(navigator.vibrate)navigator.vibrate(8);post('/api/tap',p).then(function(r){return r.json();}).then(function(h){hit=h;if(h.kind=='Eingabefeld')edit(h);else if(h.kind=='Auswahl')choose(h);refresh(150);setTimeout(load,700);});}
 y0=null;});
function closeSheet(){$('sheet').classList.remove('open');$('sheet').innerHTML='';refresh();}
function edit(h){var multi=h.multiline;$('sheet').innerHTML=(multi?'<textarea id="ed" rows="6"></textarea>':'<input id="ed" type="text" enterkeyhint="go">')+
 '<button class="btn" id="ok">Fertig</button>';var ed=$('ed');ed.value=h.text||'';$('sheet').classList.add('open');setTimeout(function(){ed.focus();},60);
 ed.oninput=function(){clearTimeout(deb);deb=setTimeout(function(){post('/api/settext',{text:ed.value}).then(function(){refresh(50);});},220);};
 ed.onkeydown=function(e){if(e.key==='Enter'&&!multi){e.preventDefault();post('/api/settext',{text:ed.value}).then(function(){return post('/api/key',{key:'enter'});}).then(closeSheet);}};
 $('ok').onclick=function(){post('/api/settext',{text:ed.value}).then(closeSheet);};}
function choose(h){$('sheet').innerHTML=(h.choices||[]).map(function(c,i){return '<button class="opt'+(i==h.selected?' on':'')+'" data-i="'+i+'">'+(i==h.selected?'✓ ':'')+esc(c)+'</button>';}).join('')+'<button class="btn sec" id="ok">Abbrechen</button>';
 $('sheet').classList.add('open');$('ok').onclick=closeSheet;$('sheet').querySelectorAll('[data-i]').forEach(function(b){b.onclick=function(){post('/api/choose',{i:b.getAttribute('data-i')}).then(closeSheet);};});}
document.addEventListener('visibilitychange',function(){if(!document.hidden)refresh();});
document.addEventListener('touchstart',function f(){var d=document.documentElement;if(d.requestFullscreen&&!window.matchMedia('(display-mode: standalone)').matches)d.requestFullscreen().catch(function(){});document.removeEventListener('touchstart',f);},{passive:true});
hello();tick();setInterval(tick,10000);fetch('/api/apps').then(function(r){return r.json();}).then(function(a){apps=a;if(!homeIds)homeIds=a.filter(function(x){return x.home;}).map(function(x){return x.id;});renderHome();renderList();});
</script></body></html>
""";

    public static string App(string device, string pc, bool remote = false, bool canAway = false) => Head +
        $"<h1>Verbunden mit {E(pc)}</h1><p class=\"ok\">✓ {E(device)} ist mit Trendo verbunden.</p>" +
        (remote ? "<div class=\"card\"><b>Trendo bedienen</b><p>Trendo als App auf dem Handy: alle Programme und Studios, in Handy-Größe, mit dem Finger bedienen.</p><a class=\"btn\" id=\"fern\" href=\"/app\">Trendo öffnen</a></div>"
                : "<div class=\"card\"><b>Trendo bedienen</b><p>Möchtest du Trendo komplett vom Handy aus bedienen? Am PC unter „Handy“ bei diesem Handy „Trendo bedienen erlauben“ antippen.</p></div>") +
        (canAway ? "<div class=\"card\"><b>Auch unterwegs</b><p>Einmal hier antippen, solange du zuhause bist. Danach erreichst du den PC überall, wenn am Handy Tailscale an ist. Speichere die neue Seite als Lesezeichen oder auf dem Startbildschirm.</p>" +
                   "<button class=\"sec\" id=\"away\" onclick=\"fetch('/api/away',{method:'POST'}).then(function(r){return r.json();}).then(function(j){location.href=j.url;}).catch(function(){document.getElementById('away').textContent='Hat nicht geklappt – ist Tailscale am Handy an?';});\">Unterwegs-Zugang einrichten</button></div>" : "") +
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
