using System.Net;
using System.Net.Sockets;
using System.Text;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Gegenprüfung T-0750: HTTP-Quelle des Anlagen-Monitors mit echtem HTTP/1.1-Server auf 127.0.0.1 (aus dem Kern hierher verlegt, QA-019)
/// und Schutz vor DNS-Rebinding: die beim Verbindungsaufbau aufgelöste Adresse muss im eigenen Netz liegen. Die Namensauflösung ist dafür ersetzt (kein echtes DNS).</summary>
public class TwinHttp_Tests
{
    const string Body = "{\"StatusSNS\":{\"DS18B20\":{\"Temperature\":21.7}}}";

    /// <summary>Kleiner echter HTTP/1.1-Server über TCP (HttpListener bräuchte für 127.0.0.1 eine URL-Freigabe vom Admin).</summary>
    sealed class Server : IDisposable
    {
        readonly TcpListener _l = new(IPAddress.Loopback, 0);
        public int Port { get; }
        public int Served;
        public Server()
        {
            _l.Start(); Port = ((IPEndPoint)_l.LocalEndpoint).Port;
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    TcpClient c; try { c = await _l.AcceptTcpClientAsync(); } catch (Exception) { return; }
                    using (c)
                    {
                        var st = c.GetStream(); var buf = new byte[4096]; var n = await st.ReadAsync(buf); var req = Encoding.ASCII.GetString(buf, 0, n);
                        Interlocked.Increment(ref Served);
                        var resp = req.StartsWith("GET /weiter ")
                            ? "HTTP/1.1 302 Found\r\nLocation: http://8.8.8.8/\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                            : $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Body.Length}\r\nConnection: close\r\n\r\n{Body}";
                        await st.WriteAsync(Encoding.ASCII.GetBytes(resp));
                    }
                }
            });
        }
        public void Dispose() => _l.Stop();
    }

    static string Chain(Exception e) { var sb = new StringBuilder(); for (Exception? x = e; x is not null; x = x.InnerException) sb.Append(x.Message).Append(" | "); return sb.ToString(); }

    [Fact(DisplayName = "HTTP-Quelle mit echtem Server auf 127.0.0.1: Wert aus JSON-Feld, ohne Freigabe kein Abruf, keine Weiterleitung")]
    public async Task Http_real_server()
    {
        using var srv = new Server();
        using var http = TwinHttp.Create();
        var s = new TwinSource { Kind = "http", Target = $"http://127.0.0.1:{srv.Port}/cm", Asset = "Aquarium", Sensor = "Wasser", JsonField = "StatusSNS.DS18B20.Temperature" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => TwinHttp.FetchAsync(http, s));
        Assert.Equal(0, srv.Served);
        s.Approved = true;
        Assert.Equal(21.7, await TwinHttp.FetchAsync(http, s));
        s.Target = $"http://127.0.0.1:{srv.Port}/weiter";
        await Assert.ThrowsAsync<IOException>(() => TwinHttp.FetchAsync(http, s));
        s.Target = "http://8.8.8.8/"; await Assert.ThrowsAsync<ArgumentException>(() => TwinHttp.FetchAsync(http, s));
        Assert.Equal(2, srv.Served);
    }

    [Fact(DisplayName = "Gegenprüfung DNS-Rebinding: *.fritz.box/*.local, der auf eine öffentliche Adresse zeigt, wird nicht verbunden; zeigt er ins eigene Netz, klappt es")]
    public async Task Dns_rebinding_refused()
    {
        using var srv = new Server();
        var asked = new List<string>();
        // ersetzte Namensauflösung: „sensor.fritz.box“ zeigt (wie bei DNS-Rebinding) nach außen, „gemischt.local“ teils nach außen, „kessel.local“ auf diesen PC
        Task<IPAddress[]> Resolve(string host, CancellationToken ct)
        {
            asked.Add(host);
            return Task.FromResult(host switch
            {
                "sensor.fritz.box" => new[] { IPAddress.Parse("93.184.216.34") },
                "gemischt.local" => [IPAddress.Loopback, IPAddress.Parse("8.8.8.8")],
                "kessel.local" => [IPAddress.Loopback],
                _ => [],
            });
        }
        using var http = TwinHttp.Create(Resolve);
        var s = new TwinSource { Kind = "http", Target = $"http://sensor.fritz.box:{srv.Port}/cm", Approved = true, Asset = "A", Sensor = "B", JsonField = "StatusSNS.DS18B20.Temperature" };
        Assert.True(TwinFeed.IsLocalUrl(s.Target, out _)); // die Endung allein sähe harmlos aus
        var e = await Assert.ThrowsAnyAsync<Exception>(() => TwinHttp.FetchAsync(http, s));
        Assert.Contains("außerhalb des eigenen Netzes", Chain(e));
        s.Target = $"http://gemischt.local:{srv.Port}/cm";
        e = await Assert.ThrowsAnyAsync<Exception>(() => TwinHttp.FetchAsync(http, s));
        Assert.Contains("außerhalb des eigenen Netzes", Chain(e));
        s.Target = $"http://unbekannt.lan:{srv.Port}/cm";
        e = await Assert.ThrowsAnyAsync<Exception>(() => TwinHttp.FetchAsync(http, s));
        Assert.Contains("nicht bekannt", Chain(e));
        Assert.Equal(0, srv.Served);
        s.Target = $"http://kessel.local:{srv.Port}/cm";
        Assert.Equal(21.7, await TwinHttp.FetchAsync(http, s)); // Name → 127.0.0.1: Trendo verbindet sich genau mit dieser Adresse
        Assert.Equal(1, srv.Served);
        Assert.Equal(["sensor.fritz.box", "gemischt.local", "unbekannt.lan", "kessel.local"], asked);
    }
}
