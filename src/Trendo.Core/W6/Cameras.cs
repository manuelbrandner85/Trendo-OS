using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Trendo.Core;

public enum CameraKind { Webcam, Rtsp, Onvif }

/// <summary>Eine Webcam, eine RTSP-Adresse oder eine per ONVIF gefundene Netzwerkkamera.</summary>
public sealed record CameraDevice(string Id, string Name, CameraKind Kind, string Source)
{
    public bool HasPicture => Kind == CameraKind.Webcam
        ? Source.Length > 0
        : Source.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) || Source.StartsWith("rtsps://", StringComparison.OrdinalIgnoreCase);

    public string KindLabel => Kind switch
    {
        CameraKind.Webcam => "Webcam",
        CameraKind.Rtsp => "IP-Kamera",
        _ => "ONVIF",
    };
}

public interface IWebcamProbe
{
    IReadOnlyList<CameraDevice> List();
}

public interface IFrameSource
{
    Task<byte[]> GrabAsync(CameraDevice camera, CancellationToken ct);
}

public interface IOnvifTransport
{
    Task<IReadOnlyList<string>> ProbeAsync(CancellationToken ct);
    Task<string> PostAsync(string url, string soapAction, string body, CancellationToken ct);
}

/// <summary>CORE-09: Webcams aus dem System, RTSP-Adressen und ONVIF-Antworten.</summary>
public static class WebcamScan
{
    public static IReadOnlyList<CameraDevice> ReadVideo4Linux(string sysClass)
    {
        if (!Directory.Exists(sysClass)) return [];
        var list = new List<CameraDevice>();
        foreach (var dir in Directory.GetDirectories(sysClass).OrderBy(d => d, StringComparer.Ordinal))
        {
            var node = Path.GetFileName(dir);
            if (!node.StartsWith("video", StringComparison.Ordinal)) continue;
            string name;
            try { name = File.ReadAllText(Path.Combine(dir, "name")).Trim(); }
            catch (IOException) { name = node; }
            if (name.Contains("metadata", StringComparison.OrdinalIgnoreCase)) continue;
            var dev = "/dev/" + node;
            list.Add(new CameraDevice(dev, name.Length == 0 ? dev : name, CameraKind.Webcam, dev));
        }
        return list;
    }

    /// <summary>Ausgabe von ffmpeg -list_devices (DirectShow). Nur Videogeräte, keine Mikrofone.</summary>
    public static IReadOnlyList<CameraDevice> FromDshow(string text)
    {
        var list = new List<CameraDevice>();
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (!line.Contains("(video)", StringComparison.OrdinalIgnoreCase)) continue;
            var q = Regex.Match(line, "\"([^\"]+)\"");
            if (!q.Success) continue;
            var name = q.Groups[1].Value.Trim();
            if (name.Length == 0) continue;
            list.Add(new CameraDevice("dshow:" + name, name, CameraKind.Webcam, name));
        }
        return list;
    }

    public static CameraDevice FromRtsp(string url, string? name = null)
    {
        var t = url.Trim();
        if (!Uri.TryCreate(t, UriKind.Absolute, out var u) || u.Scheme is not ("rtsp" or "rtsps"))
            throw new ArgumentException("Die Adresse muss mit rtsp:// beginnen, zum Beispiel rtsp://192.168.1.20:554/stream.");
        if (string.IsNullOrEmpty(u.Host)) throw new ArgumentException("In der Adresse fehlt der Rechnername.");
        var label = string.IsNullOrWhiteSpace(name) ? "IP-Kamera " + u.Host : name.Trim();
        return new CameraDevice("rtsp:" + u.AbsoluteUri, label, CameraKind.Rtsp, u.AbsoluteUri);
    }
}

public sealed class SystemWebcamProbe : IWebcamProbe
{
    public IReadOnlyList<CameraDevice> List()
    {
        var sys = WebcamScan.ReadVideo4Linux("/sys/class/video4linux");
        if (sys.Count > 0 || !OperatingSystem.IsWindows()) return sys;
        var ff = Cli.Find("ffmpeg");
        if (ff is null) return [];
        var r = Cli.Run(ff, CameraFfmpeg.DshowListArgs(), timeoutMs: 8000);
        return WebcamScan.FromDshow(r.Err + "\n" + r.Out);
    }
}

public static class CameraFfmpeg
{
    public static string[] DshowListArgs() => ["-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy"];

    public static IReadOnlyList<string> InputArgs(CameraDevice cam, bool windows)
    {
        if (cam.Source.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) || cam.Source.StartsWith("rtsps://", StringComparison.OrdinalIgnoreCase))
            return ["-rtsp_transport", "tcp", "-i", cam.Source];
        if (!cam.HasPicture)
            throw new InvalidOperationException("Diese Kamera hat keine Bildadresse. Trag eine rtsp://-Adresse ein.");
        if (cam.Source.StartsWith("/dev/", StringComparison.Ordinal))
            return ["-f", "v4l2", "-i", cam.Source];
        _ = windows;
        return ["-f", "dshow", "-i", "video=" + cam.Source];
    }

    public static string[] SnapshotArgs(CameraDevice cam, string outfile, bool windows)
    {
        var a = new List<string> { "-hide_banner", "-loglevel", "error", "-y" };
        a.AddRange(InputArgs(cam, windows));
        a.AddRange(["-frames:v", "1", "-q:v", "3", outfile]);
        return a.ToArray();
    }

    public static string[] RecordArgs(CameraDevice cam, string outfile, bool windows)
    {
        var a = new List<string> { "-hide_banner", "-loglevel", "error", "-y" };
        a.AddRange(InputArgs(cam, windows));
        a.AddRange(["-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p", "-movflags", "+frag_keyframe+empty_moov", outfile]);
        return a.ToArray();
    }
}

public sealed class FfmpegFrameSource(Func<string?> ffmpeg, Func<string, IReadOnlyList<string>, int, Cli.Result>? run = null) : IFrameSource
{
    public async Task<byte[]> GrabAsync(CameraDevice camera, CancellationToken ct)
    {
        var ff = ffmpeg() ?? throw new InvalidOperationException("Dafür fehlt FFmpeg. Mit „FFmpeg einrichten“ holst du es.");
        var file = Path.Combine(Path.GetTempPath(), "trendo-cam-" + Guid.NewGuid().ToString("N") + ".jpg");
        var args = CameraFfmpeg.SnapshotArgs(camera, file, OperatingSystem.IsWindows());
        var exec = run ?? ((exe, a, ms) => Cli.Run(exe, a, timeoutMs: ms));
        Cli.Result r;
        try { r = await Task.Run(() => exec(ff, args, 12000), ct); }
        catch (OperationCanceledException) { TryDelete(file); throw; }
        try
        {
            if (!r.Ok || !File.Exists(file) || new FileInfo(file).Length < 32)
            {
                var why = string.IsNullOrWhiteSpace(r.Err) ? "Die Kamera hat kein Bild geliefert." : "Die Kamera hat kein Bild geliefert. " + OneLine(r.Err);
                throw new InvalidOperationException(why);
            }
            return await File.ReadAllBytesAsync(file, ct);
        }
        finally { TryDelete(file); }
    }

    static void TryDelete(string file) { try { if (File.Exists(file)) File.Delete(file); } catch (IOException) { } }
    static string OneLine(string s) { s = s.Replace('\r', ' ').Replace('\n', ' ').Trim(); return s.Length > 180 ? s[..180] : s; }
}

/// <summary>Startet ffmpeg und beendet die Aufnahme, wenn Stop aufgerufen wird.</summary>
public sealed class CameraRecorder : IDisposable
{
    private Process? _p;
    public string Output { get; private set; } = "";
    public bool Running => _p is { HasExited: false };

    public void Start(string ffmpeg, IReadOnlyList<string> args, string output)
    {
        if (Running) throw new InvalidOperationException("Es läuft schon eine Aufnahme.");
        var dir = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var psi = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        _p = Process.Start(psi) ?? throw new IOException("Die Aufnahme ließ sich nicht starten.");
        Output = output;
        _ = _p.StandardError.ReadToEndAsync();
        _ = _p.StandardOutput.ReadToEndAsync();
    }

    public void Stop()
    {
        if (_p is null) return;
        try { if (!_p.HasExited) { _p.StandardInput.WriteLine("q"); _p.StandardInput.Flush(); } } catch (IOException) { }
        if (!_p.HasExited && !_p.WaitForExit(4000)) { try { _p.Kill(true); } catch (System.ComponentModel.Win32Exception) { } catch (InvalidOperationException) { } }
        _p.Dispose();
        _p = null;
    }

    public void Dispose() => Stop();
}

public static class OnvifXml
{
    public const string GetProfilesAction = "http://www.onvif.org/ver10/media/wsdl/GetProfiles";
    public const string GetStreamUriAction = "http://www.onvif.org/ver10/media/wsdl/GetStreamUri";

    public static string Probe()
    {
        var id = Guid.NewGuid();
        return $"""
        <?xml version="1.0" encoding="utf-8"?>
        <e:Envelope xmlns:e="http://www.w3.org/2003/05/soap-envelope" xmlns:w="http://schemas.xmlsoap.org/ws/2004/08/addressing" xmlns:d="http://schemas.xmlsoap.org/ws/2005/04/discovery" xmlns:dn="http://www.onvif.org/ver10/network/wsdl">
          <e:Header>
            <w:MessageID>uuid:{id}</w:MessageID>
            <w:To e:mustUnderstand="true">urn:schemas-xmlsoap-org:ws:2005:04:discovery</w:To>
            <w:Action e:mustUnderstand="true">http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</w:Action>
          </e:Header>
          <e:Body>
            <d:Probe><d:Types>dn:NetworkVideoTransmitter</d:Types></d:Probe>
          </e:Body>
        </e:Envelope>
        """;
    }

    public static IReadOnlyList<string> XAddrs(string xml)
    {
        var found = new List<string>();
        foreach (Match m in Regex.Matches(xml, @"XAddrs[^>]*>([^<]+)", RegexOptions.IgnoreCase))
            foreach (var part in m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (part.StartsWith("http", StringComparison.OrdinalIgnoreCase)) found.Add(part.Trim());
        return found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string GetProfilesBody() => """
        <?xml version="1.0" encoding="utf-8"?>
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:trt="http://www.onvif.org/ver10/media/wsdl">
          <s:Body><trt:GetProfiles/></s:Body>
        </s:Envelope>
        """;

    public static string? ProfileToken(string xml)
    {
        var m = Regex.Match(xml, @"Profiles\b[^>]*\btoken\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    public static string GetStreamUriBody(string token)
    {
        var safe = token.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        return $"""
        <?xml version="1.0" encoding="utf-8"?>
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:trt="http://www.onvif.org/ver10/media/wsdl" xmlns:tt="http://www.onvif.org/ver10/schema">
          <s:Body>
            <trt:GetStreamUri>
              <trt:StreamSetup>
                <tt:Stream>RTP-Unicast</tt:Stream>
                <tt:Transport><tt:Protocol>RTSP</tt:Protocol></tt:Transport>
              </trt:StreamSetup>
              <trt:ProfileToken>{safe}</trt:ProfileToken>
            </trt:GetStreamUri>
          </s:Body>
        </s:Envelope>
        """;
    }

    public static string? StreamUri(string xml)
    {
        var m = Regex.Match(xml, @"rtsps?://[^\s<""']+", RegexOptions.IgnoreCase);
        return m.Success ? m.Value.Trim() : null;
    }
}

public static class OnvifCameras
{
    public static async Task<IReadOnlyList<CameraDevice>> DiscoverAsync(IOnvifTransport net, CancellationToken ct = default)
    {
        var replies = await net.ProbeAsync(ct);
        var list = new List<CameraDevice>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var xml in replies)
        {
            foreach (var addr in OnvifXml.XAddrs(xml))
            {
                if (!seen.Add(addr)) continue;
                string? rtsp = null;
                try
                {
                    var profiles = await net.PostAsync(addr, OnvifXml.GetProfilesAction, OnvifXml.GetProfilesBody(), ct);
                    var token = OnvifXml.ProfileToken(profiles);
                    if (token is not null)
                    {
                        var stream = await net.PostAsync(addr, OnvifXml.GetStreamUriAction, OnvifXml.GetStreamUriBody(token), ct);
                        rtsp = OnvifXml.StreamUri(stream);
                    }
                }
                catch (Exception)
                {
                    rtsp = null;
                }
                var host = HostOf(rtsp ?? addr);
                list.Add(new CameraDevice("onvif:" + addr, "Netzwerk-Kamera " + host, CameraKind.Onvif, rtsp ?? ""));
            }
        }
        return list;
    }

    static string HostOf(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Host.Length > 0) return u.Host;
        return url;
    }
}
