#:project ../../src/Trendo.Core/Trendo.Core.csproj
#:property PublishAot=false
// T-0765 Beleg: Einkaufslisten-App aus der Baukasten-Vorlage im ECHTEN Browser (vorhandenes Chrome, headless, eigenes Profil im Temp-Ordner, ohne Netz).
// Aufruf (im Repo): dotnet run reports/T-0765/browser-beleg.cs -- <Ausgabeordner für Rohlog/Bilder>
// Ablauf: Vorlage → Publish + ZIP (wie „Exportieren (ZIP)“ im Baukasten: SitePage.Publish + ZipFile.CreateFromDirectory) → ZIP entpacken →
// Chrome 1: Seite öffnen, Knopf zur zweiten Seite klicken, leeres Formular (Pflichtfeld) absenden, zwei Einträge tippen und speichern, zurück zur Liste →
// Chrome schließen (Browser.close, sonst nur der eigene Prozess per PID) → Chrome 2 (gleiches Profil): Einträge, Anzahl, Summe noch da, einen entfernen →
// Rundreise: ZIP wieder öffnen (SitePage.FromExport) = gleiche App. Alle Netzanfragen werden mitgeschrieben – erlaubt sind nur file://-Adressen.
using System.Diagnostics;
using System.IO.Compression;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Trendo.Core;

var outDir = Path.GetFullPath(args.Length > 0 ? args[0] : "reports/T-0765");
Directory.CreateDirectory(outDir);
var work = Path.Combine(Path.GetTempPath(), "trendo-t0765-beleg-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(work);
var log = new StringBuilder();
string Clean(string s) => s.Replace(work, "<temp>").Replace(work.Replace('\\', '/'), "<temp>").Replace(Environment.UserName, "<user>");
void Log(string s) { var line = $"{DateTime.Now:HH:mm:ss.fff} {Clean(s)}"; Console.WriteLine(line); log.AppendLine(line); }
var fails = 0;
void Check(bool ok, string what) { Log((ok ? "OK    " : "FEHLER ") + what); if (!ok) fails++; }

var chrome = new[] { @"C:\Program Files\Google\Chrome\Application\chrome.exe", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", @"C:\Program Files\Microsoft\Edge\Application\msedge.exe" }.FirstOrDefault(File.Exists);
if (chrome is null) { Log("BLOCKIERT: kein Chrome/Edge gefunden"); return 2; }
Log("Browser: " + chrome + " " + FileVersionInfo.GetVersionInfo(chrome).ProductVersion);

// 1) App aus der Vorlage exportieren – derselbe Weg wie im Baukasten
var page = SitePage.Template("einkaufsliste")!;
var fertig = Path.Combine(work, "fertig");
page.Publish(fertig, Path.Combine(work, "baukasten"));
var zip = Path.Combine(work, "Einkaufsliste.zip");
ZipFile.CreateFromDirectory(fertig, zip);
var ext = Path.Combine(work, "entpackt"); ZipFile.ExtractToDirectory(zip, ext);
var index = Path.Combine(ext, "index.html");
var url = new Uri(index).AbsoluteUri;
Log("ZIP: " + zip + " (" + new FileInfo(zip).Length + " Bytes), entpackt nach " + ext);
var profile = Path.Combine(work, "profil");

var requests = new List<string>();
async Task<int> Session(string name, Func<Cdp, Task> body)
{
    Directory.CreateDirectory(profile);
    var psi = new ProcessStartInfo(chrome) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
    foreach (var a in new[] { "--headless=new", "--user-data-dir=" + profile, "--remote-debugging-port=0", "--no-first-run", "--no-default-browser-check", "--disable-background-networking",
        "--disable-component-update", "--disable-sync", "--proxy-server=http://127.0.0.1:9", "--proxy-bypass-list=<-loopback>", "--host-resolver-rules=MAP * ~NOTFOUND, EXCLUDE 127.0.0.1",
        "--window-size=420,900", "about:blank" }) psi.ArgumentList.Add(a);
    var portFile = Path.Combine(profile, "DevToolsActivePort");
    if (File.Exists(portFile)) File.Delete(portFile); // bleibt vom vorigen Lauf liegen – sonst alter Port
    using var p = Process.Start(psi)!;
    _ = p.StandardOutput.ReadToEndAsync(); _ = p.StandardError.ReadToEndAsync();
    Log($"[{name}] Chrome gestartet, PID {p.Id}, Profil {profile}");
    Cdp? cdp = null;
    try
    {
        var sw = Stopwatch.StartNew();
        string[] lines = [];
        while (sw.Elapsed < TimeSpan.FromSeconds(30)) { try { if (File.Exists(portFile)) { lines = File.ReadAllLines(portFile); if (lines.Length >= 2) break; } } catch (IOException) { } await Task.Delay(100); }
        if (lines.Length < 2) throw new TimeoutException("Chrome meldet keinen DevTools-Port");
        var port = int.Parse(lines[0]);
        using var http = new HttpClient();
        var list = JsonNode.Parse(await http.GetStringAsync($"http://127.0.0.1:{port}/json/list"))!.AsArray();
        var ws = list.First(t => t!["type"]!.GetValue<string>() == "page")!["webSocketDebuggerUrl"]!.GetValue<string>();
        cdp = await Cdp.Connect(ws, port, lines[1]);
        cdp.OnEvent = (m, prm) => { if (m == "Network.requestWillBeSent") { var u = prm["request"]!["url"]!.GetValue<string>(); lock (requests) requests.Add(u); Log($"[{name}] Netz: {u}"); } };
        await cdp.Call("Network.enable"); await cdp.Call("Page.enable"); await cdp.Call("Runtime.enable");
        await body(cdp);
    }
    finally
    {
        if (cdp is not null) try { await cdp.CallBrowser("Browser.close"); } catch (Exception e) { Log($"[{name}] Browser.close: {e.Message}"); }
        if (!p.WaitForExit(15000)) { Log($"[{name}] Chrome nicht beendet – nur der eigene Prozess {p.Id} wird beendet"); p.Kill(true); p.WaitForExit(); }
        Log($"[{name}] Chrome beendet (Exit {p.ExitCode})");
    }
    return p.Id;
}

async Task Nav(Cdp c, string u) { await c.Call("Page.navigate", new JsonObject { ["url"] = u }); await WaitFor(c, "document.readyState === 'complete' && document.documentElement.getAttribute('data-app') === 'bereit'"); }
async Task<JsonNode?> Eval(Cdp c, string js) => (await c.Call("Runtime.evaluate", new JsonObject { ["expression"] = js, ["returnByValue"] = true }))?["result"]?["value"];
async Task WaitFor(Cdp c, string js) { var sw = Stopwatch.StartNew(); while (sw.Elapsed < TimeSpan.FromSeconds(15)) { if ((await Eval(c, js))?.GetValue<bool>() == true) return; await Task.Delay(100); } throw new TimeoutException(js); }
async Task Click(Cdp c, string selector)
{
    var r = (await Eval(c, $"(function(){{var e=document.querySelector({JsonSerializer.Serialize(selector)}); if(!e) return null; e.scrollIntoView({{block:'center'}}); var b=e.getBoundingClientRect(); return [b.left+b.width/2, b.top+b.height/2];}})()"))?.AsArray()
        ?? throw new InvalidOperationException("nicht gefunden: " + selector);
    double x = r[0]!.GetValue<double>(), y = r[1]!.GetValue<double>();
    foreach (var t in new[] { "mousePressed", "mouseReleased" })
        await c.Call("Input.dispatchMouseEvent", new JsonObject { ["type"] = t, ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1 });
    Log($"Klick auf {selector} bei ({x:0},{y:0})");
    await Task.Delay(150);
}
async Task Type(Cdp c, string selector, string text) { await Click(c, selector); await c.Call("Input.insertText", new JsonObject { ["text"] = text }); Log($"Getippt in {selector}: „{text}“"); }
async Task Shot(Cdp c, string file) { var b = (await c.Call("Page.captureScreenshot", new JsonObject { ["format"] = "png" }))!["data"]!.GetValue<string>(); File.WriteAllBytes(Path.Combine(outDir, file), Convert.FromBase64String(b)); Log("Bild: " + file); }
string Visible = "Array.prototype.slice.call(document.querySelectorAll('section[data-seite]')).filter(function(s){return !s.hidden}).map(function(s){return s.getAttribute('data-seite')}).join(',')";
string Items = "Array.prototype.slice.call(document.querySelectorAll('#seite-liste ul.t-liste li')).map(function(l){return l.querySelector('span')?l.querySelector('span').textContent:'(leer) '+l.textContent}).join(' | ')";
string Stats = "Array.prototype.slice.call(document.querySelectorAll('#seite-liste .t-wert')).map(function(p){return p.textContent}).join(' | ')";

try
{
    var pid1 = await Session("Lauf 1", async c =>
    {
        await Nav(c, url);
        Check((await Eval(c, Visible))?.GetValue<string>() == "liste", "Start: nur Seite „Liste“ sichtbar → " + await Eval(c, Visible));
        Log("Liste: " + await Eval(c, Items)); Log("Auswertung: " + await Eval(c, Stats));
        Check((await Eval(c, Stats))?.GetValue<string>() == "Noch zu kaufen 0 | Summe in € 0", "Anzahl und Summe 0");
        await Click(c, "#seite-liste a.btn[href='#seite-neu']");
        await WaitFor(c, "location.hash === '#seite-neu'");
        Check((await Eval(c, Visible))?.GetValue<string>() == "neu", "Knopf „Etwas hinzufügen“ führt zur zweiten Seite → " + await Eval(c, Visible));
        await Click(c, "#seite-neu form button[type=submit]");
        var msg = (await Eval(c, "document.querySelector('#seite-neu .t-fehler').textContent"))?.GetValue<string>();
        Check(msg == "Bitte ausfüllen: Artikel.", "Pflichtfeld: leeres Formular wird abgelehnt → „" + msg + "“");
        Check((await Eval(c, "Object.keys(localStorage).length"))?.GetValue<int>() == 0, "nichts gespeichert");
        await Type(c, "#seite-neu input[name=artikel]", "Milch"); await Type(c, "#seite-neu input[name=menge]", "2"); await Type(c, "#seite-neu input[name=preis]", "1.29");
        await Click(c, "#seite-neu form button[type=submit]");
        await Type(c, "#seite-neu input[name=artikel]", "Brot"); await Type(c, "#seite-neu input[name=preis]", "2.79");
        await Click(c, "#seite-neu form button[type=submit]");
        msg = (await Eval(c, "document.querySelector('#seite-neu .t-fehler').textContent"))?.GetValue<string>();
        Check(msg == "Gespeichert.", "Formular meldet „" + msg + "“");
        await Click(c, "nav.t-nav a[href='#seite-liste']");
        await WaitFor(c, "location.hash === '#seite-liste'");
        Check((await Eval(c, Visible))?.GetValue<string>() == "liste", "Navigation zurück zur Liste");
        var items = (await Eval(c, Items))?.GetValue<string>(); Log("Liste: " + items);
        Check(items == "Milch · Menge: 2 · Preis (€): 1,29 | Brot · Preis (€): 2,79", "zwei Einträge in der Liste");
        Check((await Eval(c, Stats))?.GetValue<string>() == "Noch zu kaufen 2 | Summe in € 4,08", "Anzahl 2, Summe 4,08 → " + await Eval(c, Stats));
        Log("localStorage: " + await Eval(c, "JSON.stringify(Object.keys(localStorage))"));
        await Shot(c, "lauf1-liste.png");
    });
    var pid2 = await Session("Lauf 2 (Neustart, gleiches Profil)", async c =>
    {
        await Nav(c, url);
        var items = (await Eval(c, Items))?.GetValue<string>(); Log("Liste nach Neustart: " + items);
        Check(items == "Milch · Menge: 2 · Preis (€): 1,29 | Brot · Preis (€): 2,79", "Einträge nach Neustart des Browsers noch da");
        Check((await Eval(c, Stats))?.GetValue<string>() == "Noch zu kaufen 2 | Summe in € 4,08", "Auswertung nach Neustart");
        await Shot(c, "lauf2-nach-neustart.png");
        await Click(c, "#seite-liste ul.t-liste li:first-child button.t-weg");
        Check((await Eval(c, Items))?.GetValue<string>() == "Brot · Preis (€): 2,79", "„Entfernen“ löscht den Eintrag");
        Check((await Eval(c, Stats))?.GetValue<string>() == "Noch zu kaufen 1 | Summe in € 2,79", "Auswertung danach");
        await Click(c, "#seite-liste a.btn[href='#seite-neu']");
        await Shot(c, "lauf2-formular.png");
    });
    Check(pid1 != pid2, $"zwei getrennte Browser-Prozesse (PID {pid1}, {pid2})");
    var foreign = requests.Where(u => !u.StartsWith("file:", StringComparison.Ordinal) && !u.StartsWith("data:", StringComparison.Ordinal) && u != "about:blank").ToList();
    Check(foreign.Count == 0, $"keine Netzanfrage außer file:// ({requests.Count} Anfragen gesamt" + (foreign.Count > 0 ? ": " + string.Join(", ", foreign) : "") + ")");

    // Rundreise
    var back = SitePage.FromExport(zip);
    Check(back.ToJson() == page.ToJson(), "Rundreise: exportierte ZIP wieder geöffnet = gleiche App (" + back.Pages.Count + " Seiten, " + back.Collections.Count + " Sammlung)");

    var html = File.ReadAllText(index);
    Check(!html.Contains("<script src", StringComparison.OrdinalIgnoreCase) && !html.Contains("<link", StringComparison.OrdinalIgnoreCase), "keine externen Skripte oder Stylesheets in index.html");

    File.Copy(zip, Path.Combine(outDir, "Einkaufsliste.zip"), true);
    File.Copy(index, Path.Combine(outDir, "index.html"), true);
}
catch (Exception e) { Log("FEHLER Ablauf: " + e); fails++; }

Log(fails == 0 ? "ERGEBNIS: BESTANDEN" : $"ERGEBNIS: {fails} FEHLER");
File.WriteAllText(Path.Combine(outDir, "rohlog.txt"), log.ToString());
var sums = new StringBuilder();
foreach (var f in Directory.GetFiles(outDir).Where(f => !f.EndsWith("SHA256SUMS.txt", StringComparison.Ordinal) && !f.EndsWith(".cs", StringComparison.Ordinal)).OrderBy(f => f, StringComparer.Ordinal))
    sums.AppendLine($"{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant()}  {Path.GetFileName(f)}");
File.WriteAllText(Path.Combine(outDir, "SHA256SUMS.txt"), sums.ToString());
Console.Write(sums);
try { Directory.Delete(work, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
return fails == 0 ? 0 : 1;

sealed class Cdp
{
    readonly ClientWebSocket _ws = new();
    int _id;
    readonly System.Collections.Concurrent.ConcurrentDictionary<int, TaskCompletionSource<JsonNode?>> _wait = new();
    public Action<string, JsonNode>? OnEvent;
    string _browserWs = "";
    ClientWebSocket? _bws;
    public static async Task<Cdp> Connect(string url, int port, string browserPath)
    {
        var c = new Cdp { _browserWs = $"ws://127.0.0.1:{port}{browserPath}" };
        await c._ws.ConnectAsync(new Uri(url), CancellationToken.None);
        _ = c.Loop();
        return c;
    }
    async Task Loop()
    {
        var buf = new byte[1 << 20];
        try
        {
            while (_ws.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream(); WebSocketReceiveResult r;
                do { r = await _ws.ReceiveAsync(buf, CancellationToken.None); ms.Write(buf, 0, r.Count); } while (!r.EndOfMessage);
                if (r.MessageType == WebSocketMessageType.Close) break;
                var msg = JsonNode.Parse(ms.ToArray())!;
                if (msg["id"] is { } id && _wait.TryRemove(id.GetValue<int>(), out var t))
                {
                    if (msg["error"] is { } err) t.SetException(new InvalidOperationException(err.ToJsonString())); else t.SetResult(msg["result"]);
                }
                else if (msg["method"] is { } m) OnEvent?.Invoke(m.GetValue<string>(), msg["params"] ?? new JsonObject());
            }
        }
        catch (WebSocketException) { }
        foreach (var t in _wait.Values) t.TrySetException(new IOException("Verbindung zu: Browser beendet"));
    }
    public async Task<JsonNode?> Call(string method, JsonObject? prm = null)
    {
        var id = ++_id; var t = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously); _wait[id] = t;
        var body = new JsonObject { ["id"] = id, ["method"] = method, ["params"] = prm ?? new JsonObject() };
        await _ws.SendAsync(Encoding.UTF8.GetBytes(body.ToJsonString()), WebSocketMessageType.Text, true, CancellationToken.None);
        return await t.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
    public async Task CallBrowser(string method)
    {
        _bws = new ClientWebSocket(); await _bws.ConnectAsync(new Uri(_browserWs), CancellationToken.None);
        await _bws.SendAsync(Encoding.UTF8.GetBytes(new JsonObject { ["id"] = 1, ["method"] = method }.ToJsonString()), WebSocketMessageType.Text, true, CancellationToken.None);
        try { await _bws.ReceiveAsync(new byte[4096], new CancellationTokenSource(5000).Token); } catch (Exception e) when (e is WebSocketException or OperationCanceledException) { }
    }
}
