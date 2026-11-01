using System.Security.Cryptography;

namespace Trendo.Core;

/// <summary>Prüfbericht einer Fotoserie für den Scan: Anzahl, Größe, kleinste/größte Auflösung (nur JPEG und PNG werden gelesen), doppelte Fotos, Hinweise.</summary>
public sealed record PhotoSetReport(IReadOnlyList<string> Files, long Bytes, (int W, int H)? Smallest, (int W, int H)? Largest, int Duplicates, IReadOnlyList<string> Warnings)
{
    public int Count => Files.Count;
    public bool Ready => Count >= 3;
}

/// <summary>3D-Scan als Scan-Arbeitsplatz (Masterprompt 131, Runde 7): Fotos vor der langen Rechnung prüfen – damit niemand eine Stunde wartet, um dann „zu wenige Fotos“ zu lesen.</summary>
public static class PhotoSet
{
    /// <summary>Breite und Höhe aus dem Dateikopf (PNG: IHDR, JPEG: erster SOF-Block); null, wenn nicht lesbar.</summary>
    public static (int W, int H)? ImageSize(string path)
    {
        try
        {
            using var f = File.OpenRead(path); var head = new byte[24];
            if (f.Read(head, 0, 24) < 24) return null;
            if (head[0] == 0x89 && head[1] == 'P' && head[2] == 'N' && head[3] == 'G')
                return ((head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19], (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23]);
            if (head[0] != 0xFF || head[1] != 0xD8) return null;
            f.Position = 2;
            for (var guard = 0; guard < 500; guard++)
            {
                int b; do b = f.ReadByte(); while (b == 0xFF);
                if (b < 0) return null;
                var marker = b; var hi = f.ReadByte(); var lo = f.ReadByte(); if (lo < 0) return null;
                var len = (hi << 8) | lo;
                if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                {
                    var buf = new byte[5]; if (f.Read(buf, 0, 5) < 5) return null;
                    return ((buf[3] << 8) | buf[4], (buf[1] << 8) | buf[2]);
                }
                if (marker is 0xD9 or 0xDA) return null;
                f.Position += len - 2;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return null;
    }

    public static PhotoSetReport Check(string folder)
    {
        if (!Directory.Exists(folder)) return new([], 0, null, null, 0, ["Diesen Ordner gibt es nicht."]);
        var files = PhotoScan.ListImages(folder);
        var sizes = files.Select(ImageSize).Where(s => s is not null).Select(s => s!.Value).ToList();
        var bytes = files.Sum(f => new FileInfo(f).Length);
        var dup = files.GroupBy(f => new FileInfo(f).Length).Where(g => g.Count() > 1)
            .Sum(g => g.GroupBy(f => { using var s = File.OpenRead(f); return Convert.ToHexString(SHA256.HashData(s)); }).Sum(h => h.Count() - 1));
        (int, int)? small = sizes.Count == 0 ? null : sizes.MinBy(s => (long)s.W * s.H), large = sizes.Count == 0 ? null : sizes.MaxBy(s => (long)s.W * s.H);
        var w = new List<string>();
        if (files.Count < 3) w.Add(PhotoScan.TooFew(files.Count));
        else if (files.Count < 20) w.Add($"{files.Count} Fotos gehen, aber 20 bis 80 Fotos rundherum (mit viel Überlappung) ergeben ein deutlich besseres Modell.");
        if (files.Count > 400) w.Add($"{files.Count} Fotos – die Rechnung dauert sehr lange. 80 bis 200 reichen meist.");
        if (small is { } sm && Math.Max(sm.Item1, sm.Item2) < 1000) w.Add($"Manche Fotos sind klein ({sm.Item1} × {sm.Item2} Pixel) – Details gehen verloren. Besser mindestens 2000 Pixel Breite.");
        if (sizes.Select(s => (Math.Max(s.W, s.H), Math.Min(s.W, s.H))).Distinct().Count() > 1) w.Add("Die Fotos haben verschiedene Größen – am besten alle mit derselben Kamera und Einstellung aufnehmen.");
        if (dup > 0) w.Add($"{dup} doppelte Fotos – sie bringen nichts Neues und können die Rechnung stören.");
        return new(files, bytes, small, large, dup, w);
    }
}
