using System.Net.Http.Headers;
using System.Net.Sockets;
using Trendo.Core;
using Trendo.Core.PromptAction;

namespace Trendo.App;

/// <summary>GET und Loopback-Prüfung für den Auftrag. Der Kern bleibt ohne diese Bibliotheken.</summary>
public static class ReadOnlyWeb
{
    public static Task<(string Type, byte[] Bytes, Uri Final)> GetAsync(Uri start, CancellationToken ct = default) =>
        FetchAsync(start, files: true, ct);

    public static Task<(string Type, byte[] Bytes, Uri Final)> GetPageAsync(Uri start, CancellationToken ct = default) =>
        FetchAsync(start, files: false, ct);

    public static async Task<(string Type, byte[] Bytes, Uri Final)> FetchAsync(Uri start, bool files, CancellationToken ct = default)
    {
        // Sicherheitsreview B1: erst die tatsächlich aufgelöste Adresse prüfen – ein öffentlicher Name kann auf das Heimnetz zeigen (DNS-Rebinding)
        var handler = new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false, MaxResponseHeadersLength = 64, ConnectCallback = ConnectPublicOnly };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Trendo-Lesemodus)");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(files ? "*/*" : "text/html"));
        var u = start;
        for (var hop = 0; hop < 4; hop++)
        {
            using var r = await http.GetAsync(u, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)r.StatusCode is >= 300 and < 400 && r.Headers.Location is { } loc)
            {
                var next = new Uri(u, loc);
                var verdict = ResearchDesk.Classify(next.ToString());
                if (verdict.Kind != UrlKind.Lesemodus) throw new InvalidOperationException("Weiterleitung blockiert: " + verdict.Reason);
                u = next;
                continue;
            }
            r.EnsureSuccessStatusCode();
            var type = r.Content.Headers.ContentType?.MediaType ?? "";
            if (!files && type is not ("text/html" or "text/plain" or "application/xhtml+xml"))
                throw new InvalidOperationException("Das ist ein Download, keine Seite. Im Lesemodus werden keine Dateien geladen.");
            if (r.Content.Headers.ContentLength > ResearchDesk.MaxBytes) throw new InvalidOperationException("Die Antwort ist zu groß.");
            await using var stream = await r.Content.ReadAsStreamAsync(ct);
            var buf = new byte[ResearchDesk.MaxBytes];
            var n = 0;
            int k;
            while (n < buf.Length && (k = await stream.ReadAsync(buf.AsMemory(n), ct)) > 0) n += k;
            if (n == buf.Length)
            {
                var extra = new byte[1];
                if (await stream.ReadAsync(extra, ct) > 0) throw new InvalidOperationException("Die Antwort ist zu groß.");
            }
            return (type, buf[..n], u);
        }
        throw new InvalidOperationException("Zu viele Weiterleitungen.");
    }

    /// <summary>Verbindet nur zu öffentlichen Adressen; Loopback, Heimnetz, Link-Local, ULA, CGNAT und mapped IPv4 werden abgelehnt.</summary>
    internal static async ValueTask<Stream> ConnectPublicOnly(SocketsHttpConnectionContext ctx, CancellationToken ct)
    {
        var host = ctx.DnsEndPoint.Host;
        var addresses = System.Net.IPAddress.TryParse(host.Trim('[', ']'), out var literal) ? [literal] : await System.Net.Dns.GetHostAddressesAsync(host, ct);
        var allowed = addresses.Where(a => !ResearchDesk.IsInternal(a)).ToList();
        if (allowed.Count == 0) throw new HttpRequestException("Geräte im eigenen Netz werden im Lesemodus nicht abgerufen.");
        Exception? last = null;
        foreach (var ip in allowed)
        {
            var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try { await socket.ConnectAsync(new System.Net.IPEndPoint(ip, ctx.DnsEndPoint.Port), ct); return new NetworkStream(socket, ownsSocket: true); }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException) { socket.Dispose(); last = ex; if (ct.IsCancellationRequested) throw; }
        }
        throw new HttpRequestException("Keine Verbindung möglich.", last);
    }

    public static async Task<IReadOnlyList<(string Name, bool Open)>> ProbeLoopback(IReadOnlyList<KiSourceInfo> sources, CancellationToken ct)
    {
        var list = new List<(string Name, bool Open)>();
        foreach (var source in sources)
        {
            if (!IntelligencePromptModel.Loopback(source.BaseUrl) || !Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var uri) || uri is null)
            {
                list.Add((source.Name, false));
                continue;
            }
            using var tcp = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(300);
            try
            {
                await tcp.ConnectAsync(uri.Host, uri.Port, timeout.Token);
                list.Add((source.Name, tcp.Connected));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                list.Add((source.Name, false));
            }
            catch (Exception ex) when (ex is SocketException or TimeoutException or IOException)
            {
                list.Add((source.Name, false));
            }
        }
        return list;
    }
}
