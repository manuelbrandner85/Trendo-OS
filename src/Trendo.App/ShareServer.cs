using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SYS-06: kleiner Nur-Lese-Webserver für eine Ordnerfreigabe im Heimnetz. Lauscht nur auf der privaten Adresse, beantwortet nur GET.</summary>
public sealed class ShareServer : IAsyncDisposable
{
    private readonly TcpListener _l; private readonly CancellationTokenSource _cts = new(); private readonly Task _loop; private readonly AuditLog _audit;
    public FolderShare Share { get; }
    public IPAddress Address { get; }
    public int Port => ((IPEndPoint)_l.LocalEndpoint).Port;
    public string Url => $"http://{Address}:{Port}/{Share.Token}/";

    public ShareServer(FolderShare share, IPAddress address, AuditLog audit, int port = 0)
    {
        if (!FolderShare.IsPrivateLan(address) && !IPAddress.IsLoopback(address)) throw new InvalidOperationException("Nur im Heimnetz erlaubt.");
        Share = share; Address = address; _audit = audit;
        _l = new TcpListener(address, port); _l.Start();
        _loop = Task.Run(Loop);
    }

    public static IReadOnlyList<(string Name, IPAddress Ip)> LanAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses.Where(u => FolderShare.IsPrivateLan(u.Address)).Select(u => (n.Name, u.Address))).ToList();

    private async Task Loop()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient c;
            try { c = await _l.AcceptTcpClientAsync(_cts.Token); } catch { break; }
            _ = Task.Run(() => Handle(c));
        }
    }

    private async Task Handle(TcpClient c)
    {
        using (c)
        {
            try
            {
                c.ReceiveTimeout = 5000;
                var s = c.GetStream();
                var buf = new byte[8192]; var n = await s.ReadAsync(buf).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                var first = Encoding.ASCII.GetString(buf, 0, n).Split("\r\n")[0].Split(' ');
                if (first.Length < 2 || first[0] != "GET") { await Send(s, 405, "text/plain", "Nur lesen."u8.ToArray()); return; }
                var path = Share.Resolve(first[1]);
                if (path is null) { await Send(s, Share.Active ? 404 : 410, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(Share.Active ? "Nicht gefunden." : "Diese Freigabe ist abgelaufen.")); return; }
                if (Directory.Exists(path)) { await Send(s, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(Share.ListingHtml(path))); return; }
                Share.CountDownload();
                _audit.Append("freigabe", "share.download", $"{Path.GetFileName(path)} von {((IPEndPoint)c.Client.RemoteEndPoint!).Address}");
                await using var fs = File.OpenRead(path);
                var head = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: {FolderShare.ContentType(path)}\r\nContent-Length: {fs.Length}\r\nContent-Disposition: attachment; filename*=UTF-8''{Uri.EscapeDataString(Path.GetFileName(path))}\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n");
                await s.WriteAsync(head); await fs.CopyToAsync(s);
            }
            catch { }
        }
    }

    private static async Task Send(NetworkStream s, int code, string type, byte[] body)
    {
        var h = Encoding.ASCII.GetBytes($"HTTP/1.1 {code} {(code == 200 ? "OK" : "Fehler")}\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nX-Content-Type-Options: nosniff\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
        await s.WriteAsync(h); await s.WriteAsync(body);
    }

    public async ValueTask DisposeAsync() { _cts.Cancel(); _l.Stop(); try { await _loop.WaitAsync(TimeSpan.FromSeconds(2)); } catch { } }
}
