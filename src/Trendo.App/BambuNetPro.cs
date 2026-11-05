using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Trendo.Core.Print3D;

namespace Trendo.App;

/// <summary>Bambu Lab im Heimnetz (05.11.2026): Drucker finden (ihre Meldungen auf Port 2021 mithören), eigene Adressen für den Netz-Vergleich,
/// Zeitraffer-Videos von der Speicherkarte holen (FTPS). Nur lesend bzw. auf Knopfdruck – nichts läuft von selbst.</summary>
public static class BambuNetPro
{
    /// <summary>Eine Weile auf Meldungen der Drucker hören (sie melden sich etwa alle paar Sekunden) und alle gefundenen liefern.</summary>
    public static async Task<List<BambuFound>> DiscoverAsync(TimeSpan listen, CancellationToken ct = default)
    {
        var found = new Dictionary<string, BambuFound>();
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, BambuDiscovery.Port));
        try { udp.JoinMulticastGroup(IPAddress.Parse("239.255.255.250")); } catch (SocketException) { } // manche Netze erlauben kein Multicast – Broadcast reicht dann
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct); stop.CancelAfter(listen);
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var r = await udp.ReceiveAsync(stop.Token);
                if (BambuDiscovery.Parse(Encoding.UTF8.GetString(r.Buffer)) is { } f && Trendo.Core.PhoneAccess.AllowedAddress(IPAddress.Parse(f.Ip))) found[f.Serial] = f;
            }
        }
        catch (OperationCanceledException) { }
        return [.. found.Values];
    }

    /// <summary>Eigene IPv4-Adressen dieses PCs (WLAN und Kabel), um zu prüfen, ob der Drucker im selben Netz ist.</summary>
    public static List<string> OwnIps() =>
        [.. NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses).Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork).Select(a => a.Address.ToString())];

    /// <summary>Ist dieser PC gerade über WLAN verbunden?</summary>
    public static bool PcOnWifi() => NetworkInterface.GetAllNetworkInterfaces().Any(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

    /// <summary>Zeitraffer-Videos auf der Speicherkarte auflisten und das neueste herunterladen (FTPS, Port 990).</summary>
    public static async Task<string?> FetchNewestTimelapse(string host, string accessCode, string targetDir, Func<string?> knownThumb, Action<string> rememberThumb, CancellationToken ct = default)
    {
        await using var ctrl = await BambuNet.OpenTls(host, 990, knownThumb, rememberThumb, ct);
        var r = new StreamReader(ctrl, Encoding.ASCII); var w = new StreamWriter(ctrl, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        async Task<string> Reply() { string? line; do { line = await r.ReadLineAsync(ct) ?? throw new IOException("Drucker hat die Verbindung beendet."); } while (line.Length < 4 || line[3] == '-'); return line; }
        async Task<string> Cmd(string c, params string[] ok) { await w.WriteLineAsync(c); var rep = await Reply(); if (!ok.Any(o => rep.StartsWith(o, StringComparison.Ordinal))) throw new IOException($"Drucker antwortet auf {c.Split(' ')[0]}: {rep}"); return rep; }
        async Task<Stream> Pasv()
        {
            var pasv = await Cmd("PASV", "227");
            var n = System.Text.RegularExpressions.Regex.Match(pasv, @"\((\d+),(\d+),(\d+),(\d+),(\d+),(\d+)\)").Groups.Values.Skip(1).Select(g => int.Parse(g.Value)).ToArray();
            return await BambuNet.OpenTls(host, n[4] * 256 + n[5], knownThumb, _ => { }, ct);
        }
        await Reply();
        await Cmd("USER bblp", "331", "230"); await Cmd($"PASS {accessCode}", "230");
        await Cmd("PBSZ 0", "200"); await Cmd("PROT P", "200"); await Cmd("TYPE I", "200");
        string listing;
        await using (var data = await Pasv())
        {
            await w.WriteLineAsync("NLST /timelapse"); var rep = await Reply(); if (!rep.StartsWith('1')) return null;
            using var sr = new StreamReader(data, Encoding.UTF8); listing = await sr.ReadToEndAsync(ct);
        }
        await Reply();
        var newest = listing.Split('\n').Select(x => x.Trim()).Where(x => x.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".avi", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x, StringComparer.Ordinal).FirstOrDefault();
        if (newest is null) return null;
        var remote = newest.StartsWith('/') ? newest : "/timelapse/" + Path.GetFileName(newest);
        Directory.CreateDirectory(targetDir);
        var local = Path.Combine(targetDir, Path.GetFileName(remote));
        await using (var data = await Pasv())
        {
            await w.WriteLineAsync($"RETR {remote}"); var rep = await Reply(); if (!rep.StartsWith('1')) throw new IOException("Herunterladen abgelehnt: " + rep);
            await using var f = File.Create(local); await data.CopyToAsync(f, ct);
        }
        await Reply();
        try { await w.WriteLineAsync("QUIT"); } catch (IOException) { }
        return local;
    }
}
