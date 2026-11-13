using System.Security.Cryptography;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>Bootfähiges Trendo-OS-Abbild für USB-Stick/SSD (Wunsch 04.11.2026). Seit 13.11.2026 vom Windows-Kundenkanal getrennt: GitHub baut es
/// nur noch auf Anforderung und legt es im eigenen Vor-Release „trendo-os-abbild“ ab (nie „latest“). Es kommt in Teilen
/// (unter 2 GB, je mit SHA-256) samt <c>TrendoOS-Abbild.json</c> an. Hier: Beschreibung lesen, Teile prüfen und zusammensetzen,
/// roh auf ein Ziel schreiben und zur Kontrolle zurücklesen. Das Ziel ist ein <see cref="Stream"/> – in Tests eine Datei (virtuelle Festplatte).</summary>
public static class TrendoImage
{
    public const string ManifestName = "TrendoOS-Abbild.json";
    public const string ImageTag = "trendo-os-abbild";
    public const string LatestBase = "https://github.com/manuelbrandner85/Trendo-OS/releases/download/" + ImageTag + "/";
    public const int Sector = 512;

    public sealed record Part(string File, string Sha256, long Bytes);
    public sealed record Manifest(int Schema, string Version, long Bytes, string Sha256, double MinGb, IReadOnlyList<Part> Parts);

    /// <summary>Beschreibung lesen; ungültig (fehlende Teile, Summen falsch, unsichere Dateinamen) = null.</summary>
    public static Manifest? Parse(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            var r = d.RootElement;
            if (r.GetProperty("schema").GetInt32() != 1) return null;
            var parts = r.GetProperty("parts").EnumerateArray().Select(p => new Part(p.GetProperty("file").GetString() ?? "", (p.GetProperty("sha256").GetString() ?? "").ToLowerInvariant(), p.GetProperty("bytes").GetInt64())).ToList();
            var m = new Manifest(1, r.GetProperty("version").GetString() ?? "", r.GetProperty("bytes").GetInt64(), (r.GetProperty("sha256").GetString() ?? "").ToLowerInvariant(),
                r.TryGetProperty("min_gb", out var g) ? g.GetDouble() : 32, parts);
            if (parts.Count == 0 || m.Bytes <= 0 || parts.Sum(p => p.Bytes) != m.Bytes || !IsSha(m.Sha256)) return null;
            if (parts.Any(p => !IsSha(p.Sha256) || p.Bytes <= 0 || !SafeName(p.File))) return null;
            return m;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { return null; }
    }

    static bool IsSha(string s) => s.Length == 64 && s.All(Uri.IsHexDigit);
    static bool SafeName(string f) => f.Length is > 0 and < 100 && f.StartsWith("TrendoOS-Abbild.", StringComparison.Ordinal) && f.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');

    public static async Task<string> Sha256Async(Stream s, long bytes, Action<long>? progress = null, CancellationToken ct = default)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buf = new byte[4 << 20]; long done = 0;
        while (done < bytes)
        {
            var n = await s.ReadAsync(buf.AsMemory(0, (int)Math.Min(buf.Length, bytes - done)), ct);
            if (n == 0) break;
            h.AppendData(buf, 0, n); done += n; progress?.Invoke(done);
        }
        if (done != bytes) throw new IOException($"Nur {done} von {bytes} Bytes lesbar.");
        return Convert.ToHexStringLower(h.GetHashAndReset());
    }

    /// <summary>Teile aus <paramref name="dir"/> prüfen und zu <paramref name="output"/> zusammensetzen; prüft danach das ganze Abbild.
    /// Fehler = Text (deutsch), sonst null.</summary>
    public static async Task<string?> JoinAsync(Manifest m, string dir, string output, Action<long>? progress = null, CancellationToken ct = default)
    {
        foreach (var p in m.Parts)
        {
            var f = Path.Combine(dir, p.File);
            if (!File.Exists(f) || new FileInfo(f).Length != p.Bytes) return $"Teil {p.File} fehlt oder ist unvollständig.";
            await using var s = File.OpenRead(f);
            if (await Sha256Async(s, p.Bytes, null, ct) != p.Sha256) return $"Teil {p.File} ist beschädigt (Prüfsumme falsch).";
        }
        var tmp = output + ".laden";
        await using (var dst = File.Create(tmp))
        {
            long done = 0;
            foreach (var p in m.Parts)
            {
                await using var s = File.OpenRead(Path.Combine(dir, p.File));
                var buf = new byte[4 << 20]; int n;
                while ((n = await s.ReadAsync(buf, ct)) > 0) { await dst.WriteAsync(buf.AsMemory(0, n), ct); done += n; progress?.Invoke(done); }
            }
        }
        await using (var chk = File.OpenRead(tmp))
            if (await Sha256Async(chk, m.Bytes, null, ct) != m.Sha256) { File.Delete(tmp); return "Das zusammengesetzte Abbild ist beschädigt (Prüfsumme falsch)."; }
        File.Move(tmp, output, true);
        return null;
    }

    /// <summary>Abbild roh auf das Ziel schreiben (Blöcke immer ein Vielfaches der Sektorgröße, letzter Block mit Nullen aufgefüllt),
    /// danach zurücklesen und mit der Prüfsumme vergleichen. Fehler = Text, sonst null.</summary>
    public static async Task<string?> WriteAndVerifyAsync(Stream image, Stream target, long bytes, string sha256, Action<string, long>? progress = null, CancellationToken ct = default)
    {
        const int Block = 4 << 20; // 4 MiB, Vielfaches von 512 und 4096
        var buf = new byte[Block]; long done = 0;
        target.Position = 0;
        while (done < bytes)
        {
            var want = (int)Math.Min(Block, bytes - done); var got = 0;
            while (got < want) { var n = await image.ReadAsync(buf.AsMemory(got, want - got), ct); if (n == 0) return "Das Abbild endet zu früh."; got += n; }
            var len = (got + Sector - 1) / Sector * Sector;
            if (len > got) Array.Clear(buf, got, len - got);
            await target.WriteAsync(buf.AsMemory(0, len), ct);
            done += got; progress?.Invoke("schreiben", done);
        }
        await target.FlushAsync(ct);
        target.Position = 0;
        var back = await Sha256Async(target, bytes, d => progress?.Invoke("prüfen", d), ct);
        return back == sha256 ? null : "Kontrolle fehlgeschlagen: auf dem Laufwerk steht nicht genau das Abbild. Bitte anderes Laufwerk oder erneut versuchen.";
    }

    /// <summary>Boot-Taste je Hersteller (für die Anleitung nach dem Schreiben).</summary>
    public static readonly IReadOnlyList<(string Maker, string Key)> BootKeys =
    [
        ("Acer", "F12"), ("ASUS", "F8 oder Esc"), ("Dell", "F12"), ("Fujitsu", "F12"), ("Gigabyte", "F12"), ("HP", "F9 oder Esc"),
        ("Lenovo", "F12 oder Fn+F12"), ("MSI", "F11"), ("Medion", "F10"), ("Samsung", "F2 oder Esc"), ("Toshiba / Dynabook", "F12"), ("Apple (Intel)", "Alt/Option gedrückt halten"),
    ];
}
