using System.IO.Compression;
using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// T-0313: GitHub-Programme ausgebaut – Trefferliste zeigt Windows-Programm/Quellcode, Updates mit einem Klick, abgebrochenen Download fortsetzen,
/// optionale Anmeldung (Schlüssel im sicheren Speicher, nur an api.github.com). Nachgebautes GitHub, kein Netz.
[Collection("GitHubNetz")]
public class GitHubPlus_Ui
{
    sealed class Fake(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> f) : HttpMessageHandler
    {
        public readonly List<HttpRequestMessage> Calls = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { lock (Calls) Calls.Add(r); return f(r, ct); }
    }
    static HttpResponseMessage Json(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
    static OssRepo Repo(string n) => new(n, "Werkzeug", "https://github.com/" + n, "https://github.com/" + n + ".git", 5, "MIT", "C#", DateTimeOffset.Now);
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string Rel(string repo, string tag, long size) => $$"""{"tag_name":"{{tag}}","assets":[{"name":"w-windows-x64.zip","browser_download_url":"https://github.com/{{repo}}/releases/download/{{tag}}/w-windows-x64.zip","size":{{size}}}]}""";
    static byte[] Zip(string tag, int fill = 0)
    {
        var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            using (var s = new StreamWriter(z.CreateEntry("w/w.exe").Open())) s.Write("MZ" + tag);
            if (fill > 0) { using var f = z.CreateEntry("w/daten.bin", CompressionLevel.NoCompression).Open(); var b = new byte[fill]; new Random(2).NextBytes(b); f.Write(b); }
        }
        return ms.ToArray();
    }

    [AvaloniaFact(DisplayName = "T-0313: Trefferliste zeigt „Windows-Programm · Version · Größe“ bzw. „Nur Quellcode“; Installieren nur, wo es ein Windows-Programm gibt")]
    public async Task Availability_in_hits()
    {
        var (w, _, _) = Ui.Open();
        var fake = new Fake((r, _) =>
        {
            var u = r.RequestUri!.AbsolutePath;
            if (u.Contains("/avail-win/")) return Task.FromResult(Json(Rel("avail-win/a", "v3.1", 47_000_000)));
            if (u.Contains("/avail-src/")) return Task.FromResult(u.EndsWith("latest") ? Json("""{"tag_name":"v2","assets":[{"name":"src.tar.gz","browser_download_url":"https://github.com/x/y/s.tar.gz","size":9}]}""") : Json("[]"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        MainWindow.GitHubHandlerOverride = fake;
        try
        {
            w.OssSearchOverride = _ => Task.FromResult<IReadOnlyList<OssRepo>>([Repo("avail-win/a"), Repo("avail-src/b"), Repo("avail-none/c")]);
            w.Show("oss"); await w.OssSearch("werkzeug");
            for (var i = 0; i < 100 && w.OssAvailability.Count < 3; i++) { await Task.Delay(20); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
            Assert.Equal("Windows-Programm · v3.1 · 45 MB", w.OssAvailability["avail-win/a"].Text);
            Assert.StartsWith("Nur Quellcode", w.OssAvailability["avail-src/b"].Text); Assert.StartsWith("Noch keine Veröffentlichung", w.OssAvailability["avail-none/c"].Text);
            w.Show("oss"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal("Windows-Programm · v3.1 · 45 MB", ((TextBlock)w.Find<Border>("oss-avail-0").Child!).Text);
            Assert.True(w.Find<Button>("oss-install-0").IsEnabled); Assert.Null(w.TryFind<Button>("oss-install-1")); Assert.Null(w.TryFind<Button>("oss-install-2")); // nur Quellcode: statt ausgegrautem Knopf der Weg über den Quellcode (T-0314)
        }
        finally { MainWindow.GitHubHandlerOverride = null; }
    }

    [AvaloniaFact(DisplayName = "T-0313: Update – neue Version wird erkannt (Versionsvergleich), „Aktualisieren“ richtet sie ein, danach kein Update mehr")]
    public async Task Update_installed()
    {
        var (w, _, root) = Ui.Open();
        var tag = "v1.0"; byte[] Data() => Zip(tag);
        var fake = new Fake((r, _) => Task.FromResult(r.RequestUri!.AbsolutePath.EndsWith("/latest") ? Json(Rel("upd/werkzeug", tag, Data().Length)) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Data()) }));
        MainWindow.GitHubHandlerOverride = fake;
        try
        {
            w.Show("oss"); Assert.NotNull(await Ret(w.GitHubInstall(Repo("upd/werkzeug"), start: false)));
            var e = GitHubApps.List(Path.Combine(root, "werkzeuge")).Single(); Assert.Equal("v1.0", e.Version);
            await Run(w.GitHubCheckUpdates()); Assert.Empty(w.GitHubUpdates);
            tag = "v1.10"; await Run(w.GitHubCheckUpdates()); Assert.Equal("v1.10", w.GitHubUpdates[e.Id]);
            w.Show("components"); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.NotNull(w.Find<Button>("btn-gh-update-" + e.Id));
            await Run(w.GitHubUpdate(e));
            Assert.Equal("v1.10", GitHubApps.List(Path.Combine(root, "werkzeuge")).Single().Version); Assert.Empty(w.GitHubUpdates);
            Assert.True(GitHubApps.IsNewer("v1.9", "v1.10")); Assert.False(GitHubApps.IsNewer("2.0", "v2.0.0")); Assert.True(GitHubApps.IsNewer("nightly-a", "nightly-b"));
        }
        finally { MainWindow.GitHubHandlerOverride = null; }
    }
    static async Task<T> Ret<T>(Task<T> t) { await Run(t); return await t; }

    [AvaloniaFact(DisplayName = "T-0313: Abgebrochener Download – angefangene Datei bleibt, nächster Versuch lädt mit Range ab der Stelle weiter (206) und richtet ein")]
    public async Task Resume_download()
    {
        var (w, _, root) = Ui.Open();
        var data = Zip("v5", 3 << 20); var cut = 1 << 20; var first = true; long servedSecond = 0;
        var fake = new Fake(async (r, ct) =>
        {
            if (r.RequestUri!.AbsolutePath.EndsWith("/latest")) return Json(Rel("resume/werkzeug", "v5", data.Length));
            if (first)
            {
                first = false; var s = new MemoryStream(); s.Write(data, 0, cut); s.Position = 0;
                var c = new StreamContent(new StallAfter(s)); c.Headers.ContentLength = data.Length; return new HttpResponseMessage(HttpStatusCode.OK) { Content = c };
            }
            var from = r.Headers.Range?.Ranges.First().From ?? 0; servedSecond = data.Length - from;
            var part = new ByteArrayContent(data, (int)from, (int)(data.Length - from)); part.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(from, data.Length - 1, data.Length);
            return new HttpResponseMessage(from > 0 ? HttpStatusCode.PartialContent : HttpStatusCode.OK) { Content = part };
        });
        MainWindow.GitHubHandlerOverride = fake; MainWindow.GitHubStallTimeout = TimeSpan.FromSeconds(1);
        try
        {
            w.Show("oss");
            Assert.Null(await Ret(w.GitHubInstall(Repo("resume/werkzeug"), start: false)));
            Assert.Contains("stockt", w.SetupOverlayStepText);
            var parts = Directory.GetFiles(Path.Combine(Path.GetTempPath(), "trendo-gh-teile"), "*.part"); Assert.Contains(parts, p => new FileInfo(p).Length == cut);
            w.Find<Button>("setup-overlay-done").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.NotNull(await Ret(w.GitHubInstall(Repo("resume/werkzeug"), start: false)));
            Assert.Equal(cut, fake.Calls.Last().Headers.Range!.Ranges.First().From); Assert.Equal(data.Length - cut, servedSecond);
            Assert.True(File.Exists(Path.Combine(root, "werkzeuge", "gh-resume-werkzeug", "w", "w.exe")));
        }
        finally { MainWindow.GitHubHandlerOverride = null; MainWindow.GitHubStallTimeout = TimeSpan.FromSeconds(45); }
    }
    sealed class StallAfter(Stream inner) : Stream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> b, CancellationToken ct = default) { var n = await inner.ReadAsync(b, ct); if (n == 0) await Task.Delay(Timeout.Infinite, ct); return n; }
        public override int Read(byte[] b, int o, int c) => ReadAsync(b.AsMemory(o, c)).AsTask().GetAwaiter().GetResult();
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false; public override long Length => inner.Length; public override long Position { get => inner.Position; set { } }
        public override void Flush() { } public override long Seek(long o, SeekOrigin s) => 0; public override void SetLength(long v) { } public override void Write(byte[] b, int o, int c) { }
    }

    [AvaloniaFact(DisplayName = "T-0313: GitHub-Anmeldung – Schlüssel wird geprüft und im sicheren Speicher abgelegt, geht nur an api.github.com, Abmelden löscht ihn; falscher Schlüssel abgewiesen")]
    public async Task Token()
    {
        var (w, _, _) = Ui.Open();
        const string Good = "github_pat_ABCDEFGHIJKLMNOPQRSTUV0123456789";
        var fake = new Fake((r, _) =>
        {
            var auth = r.Headers.Authorization?.Parameter;
            if (r.RequestUri!.AbsolutePath == "/rate_limit") return Task.FromResult(auth == Good ? Json("""{"resources":{"core":{"limit":5000,"remaining":4999}}}""") : new HttpResponseMessage(HttpStatusCode.Unauthorized));
            if (r.RequestUri.AbsolutePath.EndsWith("/latest")) return Task.FromResult(Json(Rel("tok/werkzeug", "v1", Zip("v1").Length)));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Zip("v1")) });
        });
        MainWindow.GitHubHandlerOverride = fake;
        try
        {
            w.Show("oss");
            Assert.False(await Ret(w.GitHubSetToken("github_pat_FALSCH_0000000000000000000"))); Assert.Null(w.Core.Secrets.Get(MainWindow.GitHubTokenKey)); Assert.Null(MainWindow.GitHubToken);
            Assert.True(await Ret(w.GitHubSetToken(Good)));
            Assert.Equal(Good, w.Core.Secrets.Get(MainWindow.GitHubTokenKey)); Assert.Contains("5000", w.Find<TextBlock>("oss-token-msg").Text);
            lock (fake.Calls) fake.Calls.Clear(); // nur die Abrufe nach der Anmeldung prüfen
            await Run(w.GitHubInstall(Repo("tok/werkzeug"), start: false));
            Assert.All(fake.Calls.Where(c => c.RequestUri!.Host == "api.github.com"), c => Assert.Equal(Good, c.Headers.Authorization?.Parameter));
            Assert.All(fake.Calls.Where(c => c.RequestUri!.Host != "api.github.com"), c => Assert.Null(c.Headers.Authorization));
            Assert.Contains(fake.Calls, c => c.RequestUri!.Host == "github.com");
            w.GitHubRemoveToken(); Assert.Null(w.Core.Secrets.Get(MainWindow.GitHubTokenKey)); Assert.Null(MainWindow.GitHubToken);
        }
        finally { MainWindow.GitHubHandlerOverride = null; MainWindow.GitHubToken = null; }
    }
}
