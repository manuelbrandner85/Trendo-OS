using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Trendo.Core;

/// <summary>SYS-06 Network File Sharing: eine Freigabe = ein Ordner, nur lesen, geheimer Link, läuft automatisch ab.
/// Diese Klasse enthält nur die Regeln (kein Netzwerk); der kleine Server liegt in der App.</summary>
public sealed class FolderShare
{
    public string Folder { get; }
    public string Token { get; }
    public DateTimeOffset Expires { get; }
    public int Downloads { get; private set; }
    private readonly TimeProvider _time;

    public FolderShare(string folder, TimeSpan duration, TimeProvider? time = null)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromHours(24)) throw new ArgumentOutOfRangeException(nameof(duration), "Höchstens 24 Stunden.");
        _time = time ?? TimeProvider.System;
        Folder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
        Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        Expires = _time.GetLocalNow() + duration;
    }

    public bool Active => _time.GetLocalNow() < Expires;

    /// <summary>Prüft Token und Pfad. Liefert den echten Pfad oder null (falsches Token, abgelaufen, außerhalb, versteckt).</summary>
    public string? Resolve(string urlPath)
    {
        if (!Active) return null;
        var p = Uri.UnescapeDataString(urlPath.Split('?')[0]).TrimStart('/');
        if (!p.StartsWith(Token)) return null;
        var rel = p[Token.Length..].TrimStart('/');
        if (rel.Split('/', '\\').Any(s => s == ".." || s.StartsWith('.'))) return null;
        var full = Path.GetFullPath(Path.Combine(Folder, rel.Replace('/', Path.DirectorySeparatorChar)));
        if (full != Folder && !full.StartsWith(Folder + Path.DirectorySeparatorChar)) return null;
        return File.Exists(full) || Directory.Exists(full) ? full : null;
    }

    public void CountDownload() => Downloads++;

    public string ListingHtml(string dir)
    {
        var rel = Path.GetRelativePath(Folder, dir).Replace('\\', '/'); if (rel == ".") rel = "";
        var sb = new StringBuilder($"<!doctype html><meta charset=utf-8><meta name=viewport content='width=device-width'><title>Trendo Freigabe</title><body style='font-family:sans-serif;background:#0b1730;color:#e6edf7;padding:16px'><h2>📁 {WebUtility.HtmlEncode(Path.GetFileName(Folder))}{(rel == "" ? "" : " / " + WebUtility.HtmlEncode(rel))}</h2><p>Nur lesen · gültig bis {Expires:HH:mm} Uhr</p><ul>");
        string Href(string name) => "/" + Token + "/" + string.Join('/', (rel == "" ? name : rel + "/" + name).Split('/').Select(Uri.EscapeDataString));
        if (rel != "") sb.Append($"<li><a style='color:#3ba7ff' href='/{Token}/{string.Join('/', rel.Split('/').SkipLast(1).Select(Uri.EscapeDataString))}'>⬆ zurück</a></li>");
        foreach (var d in Directory.GetDirectories(dir).Where(x => !Path.GetFileName(x).StartsWith('.')).Order()) sb.Append($"<li>📁 <a style='color:#3ba7ff' href='{Href(Path.GetFileName(d))}'>{WebUtility.HtmlEncode(Path.GetFileName(d))}</a></li>");
        foreach (var f in Directory.GetFiles(dir).Where(x => !Path.GetFileName(x).StartsWith('.')).Order()) sb.Append($"<li>📄 <a style='color:#3ba7ff' href='{Href(Path.GetFileName(f))}'>{WebUtility.HtmlEncode(Path.GetFileName(f))}</a> ({new FileInfo(f).Length / 1024} KB)</li>");
        return sb.Append("</ul></body>").ToString();
    }

    public static string ContentType(string f) => Path.GetExtension(f).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf", ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".txt" or ".md" or ".csv" => "text/plain; charset=utf-8",
        ".mp4" => "video/mp4", ".mp3" => "audio/mpeg", ".html" or ".htm" => "text/plain; charset=utf-8", _ => "application/octet-stream",
    };

    /// <summary>Nur private Heimnetz-Adressen (10/8, 172.16/12, 192.168/16) – nie ins offene Internet.</summary>
    public static bool IsPrivateLan(IPAddress a)
    {
        if (a.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        var b = a.GetAddressBytes();
        return b[0] == 10 || b[0] == 172 && b[1] is >= 16 and <= 31 || b[0] == 192 && b[1] == 168;
    }
}
