using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Trendo.Core.Print3D;

namespace Trendo.App;

/// <summary>N24: Netzwerkteil der Bambu-Lab-Anbindung (TLS, FTPS, Kamera). Liegt in der App, weil der Kern ohne Netz auskommen muss (QA-019).</summary>
public static class BambuNet
{
    /// <summary>TLS-Verbindung mit Fingerabdruck-Prüfung (erstes Mal merken, danach vergleichen).</summary>
    public static async Task<Stream> OpenTls(string host, int port, Func<string?> knownThumb, Action<string> rememberThumb, CancellationToken ct)
    {
        var tcp = new TcpClient(); await tcp.ConnectAsync(host, port, ct);
        var ssl = new SslStream(tcp.GetStream(), false, (_, cert, _, _) =>
        {
            if (cert is null) return false;
            var thumb = new X509Certificate2(cert).GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256);
            var known = knownThumb();
            if (known is null) { rememberThumb(thumb); return true; }
            return string.Equals(known, thumb, StringComparison.OrdinalIgnoreCase);
        });
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, ct);
        return ssl;
    }

}

/// <summary>Datei auf die Speicherkarte des Druckers laden (FTPS mit implizitem TLS, Port 990, passiver Modus).</summary>
public static class BambuFtp
{
    public static async Task UploadAsync(string host, string accessCode, string localFile, Func<string?> knownThumb, Action<string> rememberThumb, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        await using var ctrl = await BambuNet.OpenTls(host, 990, knownThumb, rememberThumb, ct);
        var r = new StreamReader(ctrl, Encoding.ASCII); var w = new StreamWriter(ctrl, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        async Task<string> Reply() { string? line; var last = ""; do { line = await r.ReadLineAsync(ct) ?? throw new IOException("Drucker hat die Verbindung beendet."); last = line; } while (line.Length < 4 || line[3] == '-'); return last; }
        async Task<string> Cmd(string c, params string[] ok) { await w.WriteLineAsync(c); var rep = await Reply(); if (!ok.Any(o => rep.StartsWith(o, StringComparison.Ordinal))) throw new IOException($"Drucker antwortet auf {c.Split(' ')[0]}: {rep}"); return rep; }
        await Reply();
        await Cmd("USER bblp", "331", "230");
        await Cmd($"PASS {accessCode}", "230");
        await Cmd("PBSZ 0", "200"); await Cmd("PROT P", "200"); await Cmd("TYPE I", "200");
        var pasv = await Cmd("PASV", "227");
        var nums = System.Text.RegularExpressions.Regex.Match(pasv, @"\((\d+),(\d+),(\d+),(\d+),(\d+),(\d+)\)").Groups.Values.Skip(1).Select(g => int.Parse(g.Value)).ToArray();
        var port = nums[4] * 256 + nums[5];
        await w.WriteLineAsync($"STOR {Path.GetFileName(localFile)}");
        await using (var data = await BambuNet.OpenTls(host, port, knownThumb, _ => { }, ct))
        {
            var rep = await Reply(); if (!rep.StartsWith('1')) throw new IOException("Hochladen abgelehnt: " + rep);
            await using var f = File.OpenRead(localFile); var buf = new byte[81920]; long done = 0; int n;
            while ((n = await f.ReadAsync(buf, ct)) > 0) { await data.WriteAsync(buf.AsMemory(0, n), ct); done += n; progress?.Report((double)done / Math.Max(1, f.Length)); }
        }
        var fin = await Reply(); if (!fin.StartsWith("226", StringComparison.Ordinal)) throw new IOException("Hochladen nicht bestätigt: " + fin);
        try { await w.WriteLineAsync("QUIT"); } catch (IOException) { }
    }
}


/// <summary>Kamerabild der Drucker A1 und P1 (JPEG über TLS, Port 6000).</summary>
public static class BambuCameraNet
{
    public static async Task<byte[]> SnapshotAsync(string host, string accessCode, Func<string?> knownThumb, Action<string> rememberThumb, CancellationToken ct = default)
    {
        await using var s = await BambuNet.OpenTls(host, 6000, knownThumb, rememberThumb, ct);
        await s.WriteAsync(BambuCamera.AuthPacket(accessCode), ct);
        var head = new byte[16]; await ReadExact(s, head, ct);
        var size = BitConverter.ToInt32(head, 0); if (size is <= 0 or > 10_000_000) throw new IOException("Unerwartete Antwort der Kamera.");
        var img = new byte[size]; await ReadExact(s, img, ct);
        if (img.Length < 4 || img[0] != 0xFF || img[1] != 0xD8) throw new IOException("Die Kamera hat kein Bild geschickt.");
        return img;
    }

    static async Task ReadExact(Stream s, byte[] buf, CancellationToken ct) { var got = 0; while (got < buf.Length) { var n = await s.ReadAsync(buf.AsMemory(got), ct); if (n == 0) throw new IOException("Kamera-Verbindung beendet."); got += n; } }

}
