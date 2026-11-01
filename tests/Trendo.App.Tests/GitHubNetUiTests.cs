using System.IO.Compression;
using System.Net;
using System.Text;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// T-0312: GitHub-Programme laden ohne Hängen – Zeitlimit beim Suchen, Limit von GitHub erklärt, Projekt ohne Windows-Programm schließt die Anzeige,
/// kaputte Antwort beendet sauber, Download zeigt echten Prozentwert, stockender Download wird gemeldet. Alles mit nachgebautem GitHub (kein Netz).
[Collection("GitHubNetz")]
public class GitHubNet_Ui
{
    sealed class FakeGitHub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> f) : HttpMessageHandler
    {
        public readonly List<string> Calls = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { lock (Calls) Calls.Add(r.RequestUri!.ToString()); return f(r, ct); }
    }
    static HttpResponseMessage Json(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
    static OssRepo Repo(string name) => new(name, "Werkzeug", "https://github.com/" + name, "https://github.com/" + name + ".git", 5, "MIT", "C#", DateTimeOffset.Now);

    /// <summary>Datenstrom, der langsam liefert (damit der Prozentwert sichtbar steigt) oder nach einem Teil stehen bleibt.</summary>
    sealed class SlowStream(byte[] data, int chunk, int delayMs, int stallAfter = int.MaxValue) : Stream
    {
        int _pos;
        public override async ValueTask<int> ReadAsync(Memory<byte> buf, CancellationToken ct = default)
        {
            if (_pos >= stallAfter) await Task.Delay(Timeout.Infinite, ct);
            await Task.Delay(delayMs, ct);
            var n = Math.Min(Math.Min(chunk, buf.Length), data.Length - _pos); data.AsMemory(_pos, n).CopyTo(buf); _pos += n; return n;
        }
        public override int Read(byte[] b, int o, int c) => ReadAsync(b.AsMemory(o, c)).AsTask().GetAwaiter().GetResult();
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false; public override long Length => data.Length;
        public override long Position { get => _pos; set => throw new NotSupportedException(); }
        public override void Flush() { } public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException(); public override void SetLength(long v) => throw new NotSupportedException(); public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
    }

    static async Task Run(MainWindow w, Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }

    [AvaloniaFact(DisplayName = "T-0312: Hängende GitHub-Antwort → nach dem Zeitlimit klare Meldung, Anzeige beendet, nächster Versuch möglich; Sekundenzähler beim Suchen")]
    public async Task Hanging_api_times_out()
    {
        var (w, _, _) = Ui.Open();
        var fake = new FakeGitHub(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Json("{}"); });
        MainWindow.GitHubHandlerOverride = fake; MainWindow.GitHubApiTimeout = TimeSpan.FromSeconds(2);
        try
        {
            w.Show("oss"); var t = w.GitHubInstall(Repo("haengt/werkzeug"), start: false);
            await Task.Delay(1300); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Matches(@"suchen … \d+ s", w.SetupOverlayStepText);
            await Run(w, t);
            Assert.Contains("GitHub antwortet nicht", w.SetupOverlayStepText); Assert.True(w.SetupFinishShown);
            Assert.Equal(2, fake.Calls.Count); // ein zweiter Versuch, dann Schluss
        }
        finally { MainWindow.GitHubHandlerOverride = null; MainWindow.GitHubApiTimeout = TimeSpan.FromSeconds(20); }
    }

    [AvaloniaFact(DisplayName = "T-0312: Aufgebrauchtes GitHub-Limit → Meldung mit Uhrzeit; Projekt ohne Windows-Programm und kaputte Antwort beenden die Anzeige statt bei 0 % zu hängen")]
    public async Task Limit_none_broken()
    {
        var (w, _, _) = Ui.Open();
        var reset = DateTimeOffset.Now.AddMinutes(17);
        var fake = new FakeGitHub((r, _) =>
        {
            var u = r.RequestUri!.AbsolutePath;
            if (u.Contains("/limit/")) { var m = new HttpResponseMessage(HttpStatusCode.Forbidden); m.Headers.Add("X-RateLimit-Remaining", "0"); m.Headers.Add("X-RateLimit-Reset", reset.ToUnixTimeSeconds().ToString()); return Task.FromResult(m); }
            if (u.Contains("/nurcode/")) return Task.FromResult(u.EndsWith("/latest") ? Json("""{"tag_name":"v1","assets":[{"name":"quelle.tar.gz","browser_download_url":"https://github.com/x/y/q.tar.gz","size":5}]}""") : Json("[]"));
            return Task.FromResult(u.EndsWith("/latest") ? Json("""{"tag_name":null,"assets":[{"size":null},{"name":"x"}]}""") : Json("""[{"assets":[{"browser_download_url":7}]}]"""));
        });
        MainWindow.GitHubHandlerOverride = fake;
        try
        {
            w.Show("oss");
            await Run(w, w.GitHubInstall(Repo("limit/werkzeug"), start: false));
            Assert.Contains($"Ab {reset:HH:mm} Uhr geht es wieder", w.SetupOverlayStepText); Assert.True(w.SetupFinishShown);
            await Run(w, w.GitHubInstall(Repo("nurcode/werkzeug"), start: false));
            Assert.Contains("kein fertiges Windows-Programm", w.SetupOverlayStepText); Assert.True(w.SetupFinishShown);
            await Run(w, w.GitHubInstall(Repo("kaputt/werkzeug"), start: false));
            Assert.True(w.SetupFinishShown); Assert.DoesNotContain("suchen …", w.SetupOverlayStepText);
            // Zwischenspeicher: „neueste Version“ wird nicht erneut abgefragt
            var before = fake.Calls.Count(c => c.Contains("nurcode") && c.EndsWith("/latest"));
            await Run(w, w.GitHubInstall(Repo("nurcode/werkzeug"), start: false));
            Assert.Equal(before, fake.Calls.Count(c => c.Contains("nurcode") && c.EndsWith("/latest")));
        }
        finally { MainWindow.GitHubHandlerOverride = null; }
    }

    [AvaloniaFact(DisplayName = "T-0312: Download zeigt steigenden Prozentwert bis zur fertigen Einrichtung; stockender Download wird nach dem Zeitlimit gemeldet")]
    public async Task Download_progress_and_stall()
    {
        var (w, _, root) = Ui.Open();
        var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            using (var s = new StreamWriter(z.CreateEntry("werkzeug/werkzeug.exe").Open())) s.Write("MZ");
            var fill = z.CreateEntry("werkzeug/daten.bin", CompressionLevel.NoCompression); using var fs = fill.Open(); var rnd = new Random(1); var b = new byte[3 << 20]; rnd.NextBytes(b); fs.Write(b);
        }
        var zip = ms.ToArray();
        var stallNext = false;
        var fake = new FakeGitHub((r, _) =>
        {
            var u = r.RequestUri!.ToString();
            if (u.EndsWith("/releases/latest")) return Task.FromResult(Json($$"""{"tag_name":"v3.1","assets":[{"name":"werkzeug-windows-x64.zip","browser_download_url":"https://github.com/gut/werkzeug/releases/download/v3.1/werkzeug-windows-x64.zip","size":{{zip.Length}}}]}"""));
            var c = new StreamContent(new SlowStream(zip, 256 << 10, 40, stallNext ? 512 << 10 : int.MaxValue)); c.Headers.ContentLength = zip.Length;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = c });
        });
        MainWindow.GitHubHandlerOverride = fake;
        try
        {
            w.Show("oss");
            var t = w.GitHubInstall(Repo("gut/werkzeug"), start: false); var seen = new List<int>();
            while (!t.IsCompleted)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(30);
                if (System.Text.RegularExpressions.Regex.Match(w.SetupOverlayPercentText, @"^(\d+)\s?%$") is { Success: true } m) seen.Add(int.Parse(m.Groups[1].Value));
            }
            Assert.NotNull(await t);
            Assert.Contains(seen, p => p > 15 && p < 80); Assert.True(seen.Distinct().Count() >= 3, string.Join(",", seen.Distinct())); // steigt sichtbar
            Assert.True(File.Exists(Path.Combine(root, "werkzeuge", "gh-gut-werkzeug", "werkzeug", "werkzeug.exe")));
            // stockt
            stallNext = true; MainWindow.GitHubStallTimeout = TimeSpan.FromSeconds(2);
            w.Find<Avalonia.Controls.Button>("setup-overlay-done").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            GitHubApps.Uninstall(Path.Combine(root, "werkzeuge"), "gh-gut-werkzeug", w.Core.Audit);
            await Run(w, w.GitHubInstall(Repo("gut/werkzeug"), start: false));
            Assert.Contains("Download stockt", w.SetupOverlayStepText);
        }
        finally { MainWindow.GitHubHandlerOverride = null; MainWindow.GitHubStallTimeout = TimeSpan.FromSeconds(45); }
    }
}
