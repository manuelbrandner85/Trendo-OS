using System.Net;
using System.Net.Http.Headers;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>SYS-03: alles mit Netzverkehr (QA-019: nicht im Core). Downloads mit Fortsetzen, Seiten laden, Kurzlinks auflösen, DNS-Test.</summary>
public static class WebTools
{
    static HttpClient Client(bool redirects = true) => new(new HttpClientHandler { AllowAutoRedirect = redirects }) { Timeout = TimeSpan.FromMinutes(30) };

    /// <summary>Lädt nach „target“. Liegt eine .part-Datei derselben Adresse da, wird per Range fortgesetzt.</summary>
    public static async Task<long> DownloadAsync(string url, string target, IProgress<(long Done, long Total)>? progress = null, CancellationToken ct = default)
    {
        var u = new Uri(url); if (u.Scheme is not ("http" or "https")) throw new ArgumentException("Nur http- und https-Adressen.");
        var from = Web.ResumeFrom(target, url);
        if (from == 0 && File.Exists(Web.PartFile(target))) File.Delete(Web.PartFile(target));
        using var http = Client();
        using var req = new HttpRequestMessage(HttpMethod.Get, u);
        var st = Web.LoadState(target);
        if (from > 0) { req.Headers.Range = new RangeHeaderValue(from, null); if (st?.ETag is { } et && EntityTagHeaderValue.TryParse(et, out var tag)) req.Headers.IfRange = new RangeConditionHeaderValue(tag); }
        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var resumed = resp.StatusCode == HttpStatusCode.PartialContent;
        if (!resumed) from = 0;
        var total = resumed ? resp.Content.Headers.ContentRange?.Length ?? -1 : resp.Content.Headers.ContentLength ?? -1;
        Web.SaveState(new(url, target, total, resp.Headers.ETag?.ToString()));
        await using (var fs = new FileStream(Web.PartFile(target), resumed ? FileMode.Append : FileMode.Create, FileAccess.Write))
        await using (var s = await resp.Content.ReadAsStreamAsync(ct))
        {
            var buf = new byte[81920]; long done = from; int n;
            while ((n = await s.ReadAsync(buf, ct)) > 0) { await fs.WriteAsync(buf.AsMemory(0, n), ct); done += n; progress?.Report((done, total)); }
        }
        Web.Finish(target); return new FileInfo(target).Length;
    }

    public static async Task<string> GetHtmlAsync(Uri u, CancellationToken ct = default)
    {
        using var http = Client(); http.Timeout = TimeSpan.FromSeconds(20);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (TrendoOS Lesemodus)");
        return await http.GetStringAsync(u, ct);
    }

    /// <summary>Kurzlink auflösen: nur Kopfzeilen, keine Weiterleitung folgen – nur auf ausdrücklichen Wunsch.</summary>
    public static async Task<Uri?> ResolveShortAsync(Uri u)
    {
        using var http = Client(redirects: false); http.Timeout = TimeSpan.FromSeconds(10);
        using var r = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, u));
        return r.Headers.Location is { } l ? (l.IsAbsoluteUri ? l : new Uri(u, l)) : null;
    }

    public static async Task<(bool Dns, bool Web)> NetworkCheckAsync()
    {
        bool dns, web;
        try { dns = (await Dns.GetHostAddressesAsync("example.org")).Length > 0; } catch (Exception) { dns = false; }
        try { using var http = Client(); http.Timeout = TimeSpan.FromSeconds(6); using var r = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, "https://example.org/")); web = true; } catch (Exception) { web = false; }
        return (dns, web);
    }
}
