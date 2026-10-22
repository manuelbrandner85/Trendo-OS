using System.Net;
using System.Net.Sockets;
using System.Text;
using Trendo.Core;

namespace Trendo.App;

/// <summary>WS-Discovery im lokalen Netz und ONVIF-Media per HTTP. Der Kern bleibt ohne Netz-Bibliothek.</summary>
public sealed class OnvifUdp : IOnvifTransport
{
    public TimeSpan Wait { get; init; } = TimeSpan.FromSeconds(2);

    public async Task<IReadOnlyList<string>> ProbeAsync(CancellationToken ct)
    {
        using var udp = new UdpClient();
        try { udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2); } catch (SocketException) { }
        var payload = Encoding.UTF8.GetBytes(OnvifXml.Probe());
        await udp.SendAsync(payload, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 3702), ct);
        var list = new List<string>();
        var end = DateTime.UtcNow + Wait;
        while (DateTime.UtcNow < end && !ct.IsCancellationRequested)
        {
            var ms = (int)Math.Max(1, (end - DateTime.UtcNow).TotalMilliseconds);
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
            wait.CancelAfter(ms);
            try
            {
                var r = await udp.ReceiveAsync(wait.Token);
                list.Add(Encoding.UTF8.GetString(r.Buffer));
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) { break; }
        }
        return list;
    }

    public async Task<string> PostAsync(string url, string soapAction, string body, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        using var msg = new HttpRequestMessage(HttpMethod.Post, url);
        msg.Content = new StringContent(body, Encoding.UTF8, "application/soap+xml");
        msg.Headers.TryAddWithoutValidation("SOAPAction", "\"" + soapAction + "\"");
        using var resp = await http.SendAsync(msg, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }
}

/// <summary>Echte Verbindung zu einem Gerät, das Befehlszeilen per TCP annimmt (Arm, ESP32, Brücke).</summary>
public sealed class TcpMotionLink : IMotionLink
{
    private readonly TcpClient _tcp;
    private readonly Stream _stream;
    public bool IsOpen => _tcp.Connected;
    public string Endpoint { get; }

    private TcpMotionLink(TcpClient tcp, string endpoint)
    {
        _tcp = tcp;
        _stream = tcp.GetStream();
        Endpoint = endpoint;
    }

    public static TcpMotionLink Open(string endpoint, int timeoutMs = 2000)
    {
        if (!MotionEndpoint.TryParse(endpoint, out var host, out var port))
            throw new ArgumentException("Die Adresse muss Rechner und Port enthalten, zum Beispiel 192.168.1.50:9000.");
        var tcp = new TcpClient();
        try
        {
            var connect = tcp.ConnectAsync(host, port);
            if (!connect.Wait(timeoutMs)) { tcp.Dispose(); throw new IOException($"Keine Antwort von {host}:{port}."); }
            connect.GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is SocketException or IOException or AggregateException)
        {
            tcp.Dispose();
            var why = ex is IOException io && io.Message.StartsWith("Keine Antwort", StringComparison.Ordinal) ? io.Message : $"Keine Verbindung zu {host}:{port}.";
            throw new IOException(why);
        }
        return new TcpMotionLink(tcp, $"{host}:{port}");
    }

    public void Send(string line)
    {
        if (!IsOpen) throw new IOException("Das Gerät ist nicht mehr verbunden.");
        var bytes = Encoding.ASCII.GetBytes(line.TrimEnd('\r', '\n') + "\n");
        _stream.Write(bytes);
        _stream.Flush();
    }

    public void Dispose()
    {
        _stream.Dispose();
        _tcp.Dispose();
    }
}
