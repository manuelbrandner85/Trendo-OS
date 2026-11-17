using System.Net;
using System.Net.Sockets;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Echter, kleiner WebDAV-Server auf 127.0.0.1 für den Test (HttpListener, echte HTTP-Anfragen über TCP): MKCOL, PUT mit Basic-Anmeldung,
/// GET ohne Anmeldung (damit Chrome die hochgeladene Seite öffnen kann). Kein vollständiger WebDAV-Server (kein PROPFIND/LOCK) – genau das, was
/// Trendo zum Hochladen braucht. Es geht nichts ins Internet.</summary>
public sealed class LocalWebDav : IDisposable
{
    readonly HttpListener _l = new();
    readonly string _user, _pass;
    public string Root { get; } = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-dav-" + Guid.NewGuid().ToString("N"))).FullName;
    public string Origin { get; }
    public List<string> Log { get; } = [];
    public LocalWebDav(string user, string pass)
    {
        _user = user; _pass = pass;
        var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        Origin = $"http://127.0.0.1:{port}";
        _l.Prefixes.Add(Origin + "/"); _l.Start();
        _ = Task.Run(Loop);
    }
    async Task Loop()
    {
        while (_l.IsListening)
        {
            HttpListenerContext c;
            try { c = await _l.GetContextAsync(); } catch { return; }
            try { Answer(c); } catch { try { c.Response.Abort(); } catch { } }
        }
    }
    void Answer(HttpListenerContext c)
    {
        var rel = Uri.UnescapeDataString(c.Request.Url!.AbsolutePath).TrimStart('/');
        var full = Path.GetFullPath(Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar)));
        int status;
        if (!full.StartsWith(Root, StringComparison.Ordinal) || rel.Contains("..")) status = 403;
        else if (c.Request.HttpMethod is "GET" or "HEAD")
        {
            if (Directory.Exists(full)) full = Path.Combine(full, "index.html");
            if (!File.Exists(full)) status = 404;
            else
            {
                var b = File.ReadAllBytes(full);
                c.Response.ContentType = Path.GetExtension(full) switch { ".html" => "text/html; charset=utf-8", ".css" => "text/css", ".js" or ".mjs" => "text/javascript", ".png" => "image/png", ".svg" => "image/svg+xml", ".json" => "application/json", ".xml" => "application/xml", _ => "application/octet-stream" };
                c.Response.ContentLength64 = b.Length;
                if (c.Request.HttpMethod == "GET") c.Response.OutputStream.Write(b);
                status = 200;
            }
        }
        else if (c.Request.Headers["Authorization"] != "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(_user + ":" + _pass))) status = 401;
        else if (c.Request.HttpMethod == "MKCOL") { status = Directory.Exists(full) ? 405 : 201; Directory.CreateDirectory(full); }
        else if (c.Request.HttpMethod == "PUT")
        {
            if (!Directory.Exists(Path.GetDirectoryName(full))) status = 409;
            else { using (var fs = File.Create(full)) c.Request.InputStream.CopyTo(fs); status = 201; }
        }
        else status = 405;
        lock (Log) Log.Add($"{c.Request.HttpMethod} /{rel} → {status}");
        c.Response.StatusCode = status; c.Response.Close();
    }
    public void Dispose() { try { _l.Stop(); _l.Close(); } catch { } try { Directory.Delete(Root, true); } catch { } }
}

/// <summary>T-0718 (Audit C4) in der Oberfläche: Veröffentlichen aus „Webseite bauen“ (Web Design Studio) und dem Baukasten nur über das Freigabe-Tor –
/// echter Dialog mit Wirkung, Ziel, Dateien, Größe; Abbrechen = nichts passiert; Ja = genau eine Ausführung im Kern; Kinderprofil darf nicht;
/// Rückmeldung mit Ort, Dateien, Prüfergebnis bzw. „Was tun“. Der Server-Weg lädt echt per HTTP an einen Server auf 127.0.0.1.</summary>
public class T0718WebVeroeffentlichen_Ui
{
    internal const string Page = """{"root":{"props":{"title":"Start","description":"Kurz"}},"content":[{"type":"Ueberschrift","props":{"id":"h","text":"Willkommen","level":"h2"}},{"type":"Text","props":{"id":"t","text":"Absatz","align":"left"}}],"zones":{}}""";
    internal static async Task Settle(int rounds = 10) { for (var i = 0; i < rounds; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); } Dispatcher.UIThread.RunJobs(); }
    internal static async Task Until(Func<bool> ok, int ms = 15000) { for (var t = 0; t < ms && !ok(); t += 20) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Dispatcher.UIThread.RunJobs(); }
    static T Dlg<T>(Window dlg, string name) where T : Control => dlg.GetLogicalDescendants().OfType<T>().First(b => b.Name == name);
    internal static string Temp(string n) => Path.Combine(Path.GetTempPath(), "trendo-t0718-" + n + "-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Web-Design-Seite anlegen, exportieren, prüfen – wie eine Person über „Webseite bauen“.</summary>
    internal static async Task<MainWindow> WebSite(MainWindow w)
    {
        w.WebDesignMessage("{\"art\":\"speichern\",\"daten\":" + Page + "}"); await Settle();
        Assert.Null(WebDesignExport.ExportSite(w.WebDesignDir, MainWindow.WebDesignEditorDir).Error);
        w.Show("dev"); await Settle();
        w.Find<Button>("web-pub-pruefen").Press(); await Settle();
        Assert.Contains("Prüfung bestanden", w.WebDesignPublishNote);
        return w;
    }
    internal static void EditorDir() => MainWindow.WebDesignEditorDirOverride = Path.Combine(WebDesignKi_Ui.Repo(), "src", "Trendo.App", "Assets", "webdesign");

    [AvaloniaFact(DisplayName = "T-0718 UI Ordner: echter Dialog nennt Wirkung, Ziel, Dateien, Größe; Abbrechen = nichts im Ziel; Ja = genau eine Ausführung, Rückmeldung mit Ort, Dateien und Prüfung")]
    public async Task Ordner_mit_echtem_Dialog()
    {
        EditorDir();
        var dest = Temp("ziel");
        MainWindow.FolderPickOverride = _ => dest;
        var (w, _, _) = Ui.Open();
        try
        {
            w.FreigabeConfirm = null; // echter Dialog
            await WebSite(w);
            w.Find<Button>("web-pub-ordner").Press(); await Settle();
            var dlg = Assert.Single(w.OwnedWindows);
            var text = Dlg<TextBlock>(dlg, "freigabe-text").Text!;
            foreach (var t in new[] { "Wirkung:", "Ziel: " + dest, "Dateien:", "Größe:", "nichts ins Internet" }) Assert.Contains(t, text);
            Assert.Contains("index.html", Dlg<SelectableTextBlock>(dlg, "freigabe-vorschau").Text);
            var no = Dlg<Button>(dlg, "freigabe-nein"); Assert.True(no.IsDefault);
            no.Press(); await Settle();
            Assert.Empty(w.OwnedWindows);
            Assert.False(Directory.Exists(dest));
            Assert.Contains("Abgebrochen", w.WebDesignPublishNote);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "approval.denied");
            Assert.DoesNotContain(w.Core.Audit.ReadAll(), a => a.Action == "execution.start");

            w.Find<Button>("web-pub-ordner").Press(); await Settle();
            Dlg<Button>(Assert.Single(w.OwnedWindows), "freigabe-ja").Press(); await Until(() => w.WebDesignPublishResult is not null);
            var r = w.WebDesignPublishResult!;
            Assert.True(r.Ok, r.Text);
            Assert.True(File.Exists(Path.Combine(dest, "index.html")));
            Assert.Equal(WebDesignPublish.Files(dest).Count, r.Dateien);
            Assert.StartsWith("Veröffentlicht in " + dest, w.WebDesignPublishNote);
            Assert.Contains("Prüfung:", w.WebDesignPublishNote); Assert.Contains("alles gefunden", w.WebDesignPublishNote);
            Assert.NotNull(w.TryFind<Button>("web-pub-oeffnen"));
            var id = w.LastFreigabe!.Id;
            Assert.Single(w.Core.Audit.ReadAll(), a => a.Action == "execution.start" && a.Detail.StartsWith($"{id} {WebPublishing.ActionOrdner} {dest}#"));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "web.veroeffentlicht" && a.Detail.StartsWith("ordner "));
            Assert.Throws<ApprovalException>(() => w.Core.Approvals.Execute(id, WebPublishing.ActionOrdner, w.LastFreigabe.Target, () => 0)); // verbraucht
        }
        finally { MainWindow.FolderPickOverride = null; MainWindow.WebDesignEditorDirOverride = null; w.Close(); try { Directory.Delete(dest, true); } catch { } }
    }

    [AvaloniaFact(DisplayName = "T-0718 UI Kinderprofil: keine Rückfrage, nichts veröffentlicht, Hinweis „Erwachsener“, im Protokoll abgelehnt")]
    public async Task Kinderprofil_veroeffentlicht_nicht()
    {
        EditorDir();
        var dest = Temp("kind");
        MainWindow.FolderPickOverride = _ => dest;
        var root = Temp("ui");
        Directory.CreateDirectory(root);
        new UserSettings { Child = true, Name = "Kind", SetupDone = true }.Save(root);
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher(), offline: false);
        var w = new MainWindow(core, new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Erweitert);
        var asked = 0; w.FreigabeConfirm = _ => { asked++; return Task.FromResult(true); };
        w.Show(); Dispatcher.UIThread.RunJobs();
        try
        {
            await WebSite(w);
            w.Find<Button>("web-pub-ordner").Press(); await Settle();
            Assert.Equal(0, asked); Assert.Empty(w.OwnedWindows);
            Assert.False(Directory.Exists(dest));
            Assert.Contains("Erwachsenen", w.WebDesignPublishNote);
            Assert.Contains(core.Audit.ReadAll(), a => a.Action == "freigabe.nicht.erlaubt" && a.Detail.StartsWith(WebPublishing.ActionOrdner));
            Assert.DoesNotContain(core.Audit.ReadAll(), a => a.Action is "approval.approved" or "execution.start");
        }
        finally { MainWindow.FolderPickOverride = null; MainWindow.WebDesignEditorDirOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0718 UI eigener Server: echter Upload per HTTP an WebDAV auf 127.0.0.1 nach Ja – Dateien liegen byte-gleich auf dem Server, Nachprüfung, Anmeldung nur im geschützten Speicher; falsches Passwort → Meldung mit „Was tun“")]
    public async Task Server_echter_Upload_auf_127_0_0_1()
    {
        EditorDir();
        using var dav = new LocalWebDav("web", "Probe-Pass-123");
        Directory.CreateDirectory(Path.Combine(dav.Root, "site"));
        var (w, _, root) = Ui.Open();
        try
        {
            await WebSite(w);
            w.Find<TextBox>("web-pub-server-adresse").Text = dav.Origin + "/site/";
            w.Find<TextBox>("web-pub-server-benutzer").Text = "web";
            w.Find<TextBox>("web-pub-server-passwort").Text = "falsch";
            w.Find<Button>("web-pub-server").Press(); await Until(() => w.WebDesignPublishResult is not null);
            Assert.False(w.WebDesignPublishResult!.Ok);
            Assert.Contains("Benutzername oder Passwort abgelehnt", w.WebDesignPublishNote); Assert.Contains("Was tun:", w.WebDesignPublishNote);
            Assert.False(File.Exists(Path.Combine(dav.Root, "site", "index.html")));

            w.Find<Button>("web-pub-pruefen").Press(); await Settle();
            w.Find<TextBox>("web-pub-server-passwort").Text = "Probe-Pass-123";
            var asked = 0; w.FreigabeConfirm = q => { asked++; Assert.Contains("Ziel: " + dav.Origin + "/site/", q.Text); Assert.Contains("Heimnetz", q.Text); return Task.FromResult(true); };
            w.Find<Button>("web-pub-server").Press(); await Until(() => w.WebDesignPublishResult is not null);
            var r = w.WebDesignPublishResult; Assert.True(r is not null, $"asked={asked} note={w.WebDesignPublishNote} log={string.Join(";", dav.Log)}"); r = r!;
            Assert.True(r.Ok, r.Text); Assert.Equal(1, asked);
            var src = Path.Combine(w.WebDesignDir, WebDesign.ExportDir);
            var files = WebDesignPublish.Files(src);
            Assert.Equal(files.Count, r.Dateien);
            foreach (var f in files) Assert.Equal(File.ReadAllBytes(Path.Combine(src, f.Rel)), File.ReadAllBytes(Path.Combine(dav.Root, "site", f.Rel)));
            Assert.StartsWith($"Hochgeladen zu {dav.Origin}/site/", w.WebDesignPublishNote);
            Assert.Contains($"Nachprüfung: {files.Count} von {files.Count} Dateien", w.WebDesignPublishNote);
            Assert.Contains(dav.Log, l => l.StartsWith("PUT /site/index.html → 201"));
            // Anmeldung nur im geschützten Speicher, nie im Klartext in Trendos Daten
            Assert.Equal("Probe-Pass-123", new WebDavLink(w.Core.Secrets).Saved(new Uri(dav.Origin + "/site/")).Password);
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(f => Path.GetExtension(f) is ".json" or ".txt" or ".log" or ".jsonl" or ".yaml" or ""))
                Assert.DoesNotContain("Probe-Pass-123", File.ReadAllText(file));
            // je Ja genau eine Ausführung (Fehlversuch mit falschem Passwort + gelungener Lauf), die letzte mit der letzten Freigabe
            Assert.Equal(2, w.Core.Audit.ReadAll().Count(a => a.Action == "execution.start" && a.Detail.Contains(WebPublishing.ActionServer) && a.Detail.Contains("#")));
            Assert.Single(w.Core.Audit.ReadAll(), a => a.Action == "execution.start" && a.Detail.StartsWith(w.LastFreigabe!.Id + " " + WebPublishing.ActionServer));
        }
        finally { MainWindow.WebDesignEditorDirOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0718 UI Testmodus: Server-Weg lädt nie ins Internet; Netlify ohne eigenes Konto: BLOCKIERT mit „Was tun“, keine Anfrage im Tor")]
    public async Task Nichts_ins_Internet_und_Netlify_blockiert()
    {
        EditorDir();
        var (w, _, _) = Ui.Open();
        try
        {
            await WebSite(w);
            w.WebDesignServerAddress = "https://mein-server.example/site/";
            await w.WebDesignPublishToServer(); await Settle();
            Assert.Contains("nur an diesen PC", w.WebDesignPublishNote);
            Assert.Null(w.TryFind<Button>("web-pub-hochladen"));
            await w.WebDesignPublishUpload(); await Settle();
            Assert.Contains("Netlify ist nicht verbunden", w.WebDesignPublishNote); Assert.Contains("Was tun:", w.WebDesignPublishNote);
            Assert.DoesNotContain(w.Core.Audit.ReadAll(), a => a.Action == "approval.requested");
        }
        finally { MainWindow.WebDesignEditorDirOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0718 UI ZIP: nur nach Ja im Tor; Paket enthält genau die gezeigten Dateien; Rückmeldung nennt „nichts hochgeladen“")]
    public async Task Zip_ueber_das_Tor()
    {
        EditorDir();
        var (w, _, _) = Ui.Open();
        try
        {
            await WebSite(w);
            w.FreigabeConfirm = _ => Task.FromResult(false);
            w.Find<Button>("web-pub-paket").Press(); await Settle();
            Assert.False(Directory.Exists(Path.Combine(w.WebDesignDir, "pakete")) && Directory.GetFiles(Path.Combine(w.WebDesignDir, "pakete")).Length > 0);
            w.FreigabeConfirm = _ => Task.FromResult(true);
            w.Find<Button>("web-pub-paket").Press(); await Until(() => w.WebDesignPublishResult is not null);
            Assert.True(w.WebDesignPublishResult!.Ok, w.WebDesignPublishNote);
            Assert.Contains("nichts hochgeladen", w.WebDesignPublishNote);
            using var z = System.IO.Compression.ZipFile.OpenRead(w.WebDesignPublishResult.Wo);
            Assert.Equal(WebDesignPublish.Files(Path.Combine(w.WebDesignDir, WebDesign.ExportDir)).Count, z.Entries.Count);
        }
        finally { MainWindow.WebDesignEditorDirOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0718 UI Baukasten: „Veröffentlichen …“ fragt im echten Dialog; nach Ja liegt die Seite im Zielordner, Prüfung grün")]
    public async Task Baukasten_ueber_das_Tor()
    {
        var dest = Temp("bau");
        MainWindow.FolderPickOverride = _ => dest;
        var (w, _, _) = Ui.Open();
        try
        {
            w.FreigabeConfirm = null;
            w.Show("baukasten"); await Settle();
            w.BuilderInsert("h1", 0); w.BuilderInsert("p", 1); await Settle();
            w.Find<Button>("builder-veroeffentlichen").Press(); await Settle();
            var dlg = Assert.Single(w.OwnedWindows);
            Assert.Contains("Ziel: " + dest, Dlg<TextBlock>(dlg, "freigabe-text").Text);
            Dlg<Button>(dlg, "freigabe-ja").Press(); await Until(() => w.BuilderPublishResult is not null);
            Assert.True(w.BuilderPublishResult!.Ok, w.BuilderPublishResult.Text);
            Assert.Contains("Überschrift", WebUtility.HtmlDecode(File.ReadAllText(Path.Combine(dest, "index.html"))));
            Assert.Single(w.Core.Audit.ReadAll(), a => a.Action == "execution.start" && a.Detail.Contains(WebPublishing.ActionOrdner));
        }
        finally { MainWindow.FolderPickOverride = null; w.Close(); try { Directory.Delete(dest, true); } catch { } }
    }
}

/// <summary>T-0718 Beleg in echtem Chrome (übersprungen ohne Chrome/Edge): (a) in einen Ordner veröffentlicht, vom lokalen Server ausgeliefert, in Chrome geöffnet –
/// Bildschirmfoto, keine Skriptfehler, keine fehlenden Dateien; (b) per WebDAV auf einen Server auf 127.0.0.1 hochgeladen und dort unter seiner Adresse in Chrome geöffnet.
/// Mit TRENDO_T0718_BELEG=&lt;Ordner&gt; werden Bilder und Rohlog dort abgelegt.</summary>
public class T0718WebVeroeffentlichen_Browser
{
    static void Beleg(string? dir, string line) { if (dir is null) return; File.AppendAllText(Path.Combine(dir, "rohlog.txt"), $"{DateTime.Now:HH:mm:ss} {line}\n"); }

    [AvaloniaFact(DisplayName = "T-0718 echter Chrome: Ordner und WebDAV-Server auf 127.0.0.1 – veröffentlichte Seite lädt ohne Fehler, alle lokalen Links/Dateien per HTTP erreichbar, Bildschirmfoto (übersprungen ohne Chrome/Edge)")]
    public async Task Echter_Chrome_Beleg()
    {
        if (WebsitePreview.FindBrowser() is null) { Assert.Skip("Übersprungen – Chrome oder Edge fehlt auf diesem PC."); return; }
        var beleg = Environment.GetEnvironmentVariable("TRENDO_T0718_BELEG") is { Length: > 0 } b ? Directory.CreateDirectory(b).FullName : null;
        T0718WebVeroeffentlichen_Ui.EditorDir();
        var dest = T0718WebVeroeffentlichen_Ui.Temp("chrome");
        MainWindow.FolderPickOverride = _ => dest;
        using var dav = new LocalWebDav("web", "Probe-Pass-123");
        Directory.CreateDirectory(Path.Combine(dav.Root, "site"));
        var (w, _, _) = Ui.Open();
        var shots = beleg ?? T0718WebVeroeffentlichen_Ui.Temp("shots");
        Directory.CreateDirectory(shots);
        try
        {
            await T0718WebVeroeffentlichen_Ui.WebSite(w);
            Beleg(beleg, "Chrome: " + Path.GetFileName(WebsitePreview.FindBrowser()));
            // (a) Ordner
            w.Find<Button>("web-pub-ordner").Press(); await T0718WebVeroeffentlichen_Ui.Until(() => w.WebDesignPublishResult is not null);
            Assert.True(w.WebDesignPublishResult!.Ok, w.WebDesignPublishNote);
            Beleg(beleg, "(a) Rückmeldung: " + w.WebDesignPublishNote.Replace(dest, "<zielordner>").Replace("\n", " | "));
            var errA = new List<string>();
            var shotA = await WebsitePreview.CaptureAsync(dest, 1280, 900, Path.Combine(shots, "a-ordner-chrome.png"), errors: errA);
            Assert.True(shotA.Ok, shotA.Note);
            Assert.Empty(errA);
            using (var host = WebsiteHost.Start(dest))
            {
                var n = await HttpLinks(host.Origin + "/", dest, beleg, "(a)");
                Assert.True(n > 0);
            }
            Beleg(beleg, $"(a) Chrome-Fehler: {errA.Count}; Bild a-ordner-chrome.png ({new FileInfo(Path.Combine(shots, "a-ordner-chrome.png")).Length} Bytes)");

            // (b) WebDAV-Server auf 127.0.0.1
            w.Find<Button>("web-pub-pruefen").Press(); await T0718WebVeroeffentlichen_Ui.Settle();
            w.Find<TextBox>("web-pub-server-adresse").Text = dav.Origin + "/site/";
            w.Find<TextBox>("web-pub-server-benutzer").Text = "web";
            w.Find<TextBox>("web-pub-server-passwort").Text = "Probe-Pass-123";
            var before = w.WebDesignPublishResult;
            w.Find<Button>("web-pub-server").Press(); await T0718WebVeroeffentlichen_Ui.Until(() => !ReferenceEquals(w.WebDesignPublishResult, before));
            Assert.True(w.WebDesignPublishResult!.Ok, w.WebDesignPublishNote);
            Beleg(beleg, "(b) Rückmeldung: " + w.WebDesignPublishNote.Replace("\n", " | ").Replace(dav.Origin, "http://127.0.0.1:<port>"));
            lock (dav.Log) foreach (var l in dav.Log) Beleg(beleg, "(b) Server: " + l);
            var errB = new List<string>();
            var shotB = await WebsitePreview.CaptureUrlAsync(dav.Origin + "/site/index.html", 1280, 900, Path.Combine(shots, "b-webdav-chrome.png"), errB);
            Assert.True(shotB.Ok, shotB.Note);
            Assert.Empty(errB);
            var nb = await HttpLinks(dav.Origin + "/site/", Path.Combine(dav.Root, "site"), beleg, "(b)");
            Assert.True(nb > 0);
            Beleg(beleg, $"(b) Chrome-Fehler: {errB.Count}; Bild b-webdav-chrome.png ({new FileInfo(Path.Combine(shots, "b-webdav-chrome.png")).Length} Bytes)");
        }
        finally { MainWindow.FolderPickOverride = null; MainWindow.WebDesignEditorDirOverride = null; w.Close(); try { Directory.Delete(dest, true); } catch { } if (beleg is null) try { Directory.Delete(shots, true); } catch { } }
    }

    /// <summary>Jeden lokalen Verweis jeder HTML-/CSS-Datei über HTTP abrufen – muss 200 liefern.</summary>
    static async Task<int> HttpLinks(string baseUrl, string dir, string? beleg, string tag)
    {
        using var http = new HttpClient();
        var n = 0;
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Where(f => f.EndsWith(".html") || f.EndsWith(".css")))
        {
            var rel = Path.GetRelativePath(dir, f).Replace('\\', '/');
            if (rel.Split('/').Any(p => p.StartsWith('.'))) continue;
            var page = new Uri(new Uri(baseUrl), rel);
            foreach (var v in WebPublishing.Refs(File.ReadAllText(f), f.EndsWith(".css")).Where(WebPublishing.IsLocal).Distinct())
            {
                var u = new Uri(page, v.Split('#')[0]);
                using var resp = await http.GetAsync(u);
                Assert.True(resp.StatusCode == HttpStatusCode.OK, $"{rel}: {v} → {(int)resp.StatusCode}");
                n++;
            }
        }
        Beleg(beleg, $"{tag} HTTP-Linkprüfung: {n} lokale Verweise abgerufen, alle 200");
        return n;
    }
}
