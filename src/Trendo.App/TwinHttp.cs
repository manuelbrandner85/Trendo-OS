using System.Net;
using System.Net.Sockets;
using System.Text;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Gegenprüfung T-0750: HTTP-Abruf einer Quelle des Anlagen-Monitors – liegt in der App, wie <see cref="AceStepClient"/> (QA-019: der Kern hat keine Netz-Abhängigkeit).
/// Nur GET, höchstens 64 KB, 5 s, keine Weiterleitung, kein Proxy. Schutz vor DNS-Rebinding: Trendo löst den Namen beim Verbindungsaufbau selbst auf,
/// lässt nur private, Loopback- oder Link-local-Adressen zu und verbindet sich genau mit einer davon – zeigt ein Name (auch *.local oder *.fritz.box) nach außen, wird nicht verbunden.</summary>
public static class TwinHttp
{
    public const int MaxBody = 65_536;

    /// <summary>HttpClient ohne Weiterleitungen und ohne Proxy. <paramref name="resolve"/> ersetzt die Namensauflösung (nur für Tests; Standard: DNS des Systems).</summary>
    public static HttpClient Create(Func<string, CancellationToken, Task<IPAddress[]>>? resolve = null)
    {
        resolve ??= (host, ct) => Dns.GetHostAddressesAsync(host, ct);
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(5), PooledConnectionLifetime = TimeSpan.FromMinutes(1),
            ConnectCallback = async (ctx, ct) =>
            {
                var host = ctx.DnsEndPoint.Host.Trim('[', ']');
                var all = IPAddress.TryParse(host, out var lit) ? [lit] : await resolve(host, ct);
                if (all.Length == 0) throw new IOException($"Der Name „{host}“ ist im eigenen Netz nicht bekannt.");
                if (all.Any(a => !TwinFeed.IsLocalAddress(a))) throw new IOException($"„{host}“ zeigt auf eine Adresse außerhalb des eigenen Netzes – Trendo verbindet sich nicht.");
                Exception? last = null;
                foreach (var ip in all)
                {
                    var sock = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await sock.ConnectAsync(new IPEndPoint(ip, ctx.DnsEndPoint.Port), ct);
                        // Nachkontrolle: tatsächlich verbundene Gegenstelle
                        if (sock.RemoteEndPoint is not IPEndPoint ep || !TwinFeed.IsLocalAddress(ep.Address)) throw new IOException("Die Gegenstelle liegt nicht im eigenen Netz.");
                        return new NetworkStream(sock, ownsSocket: true);
                    }
                    catch (Exception e) when (e is SocketException or IOException) { sock.Dispose(); last = e; }
                }
                throw new IOException($"Keine Verbindung zu „{host}“.", last);
            },
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>Antworttext einer Adresse holen (GET, 5 s, höchstens 64 KB, keine Weiterleitung).</summary>
    public static async Task<string> GetAsync(HttpClient http, Uri u, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(TimeSpan.FromSeconds(5));
        using var resp = await http.GetAsync(u, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        if ((int)resp.StatusCode is >= 300 and < 400) throw new IOException("Die Adresse leitet weiter – Trendo folgt keiner Weiterleitung.");
        resp.EnsureSuccessStatusCode();
        await using var st = await resp.Content.ReadAsStreamAsync(cts.Token);
        var buf = new byte[MaxBody]; var n = 0; int k;
        while (n < buf.Length && (k = await st.ReadAsync(buf.AsMemory(n), cts.Token)) > 0) n += k;
        return Encoding.UTF8.GetString(buf, 0, n);
    }

    /// <summary>Eine freigegebene HTTP-Quelle einmal lesen (Prüfung von Freigabe und Adresse im Kern, <see cref="TwinFeed.FetchHttpAsync"/>).</summary>
    public static Task<double> FetchAsync(HttpClient http, TwinSource s, CancellationToken ct = default) =>
        TwinFeed.FetchHttpAsync(s, (u, c) => GetAsync(http, u, c), ct);
}
