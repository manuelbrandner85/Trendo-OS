namespace Trendo.Core.Sys;

/// <summary>Masterprompt 131: Speicherplatz als Kachelkarte (wie WinDirStat) – Ordner einlesen (Hintergrund, abbrechbar, ohne Verknüpfungen zu folgen),
/// Kacheln nach Größe (Squarified-Verfahren), Dateiarten, große Dateien. Löschen gehört nicht hierher: das Programm verschiebt nur nach „Ja“ in den Papierkorb.</summary>
public static class DiskMap
{
    public sealed class Node(string path, string name, bool isDir)
    {
        public string Path { get; } = path;
        public string Name { get; } = name;
        public bool IsDir { get; } = isDir;
        public long Bytes { get; set; }
        public int Files { get; set; }
        public List<Node> Children { get; } = [];
        public Node? Parent { get; set; }
    }

    public sealed record Rect(double X, double Y, double W, double H);

    /// <summary>Ordner einlesen. Unterhalb von <paramref name="maxDepth"/> wird nur noch zusammengezählt; nicht lesbare Ordner zählen 0.</summary>
    public static Node Scan(string root, int maxDepth = 8, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var top = new Node(root, System.IO.Path.GetFileName(root.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : root, true);
        var count = 0;
        void Walk(Node node, int depth)
        {
            ct.ThrowIfCancellationRequested();
            IEnumerable<FileSystemInfo> items;
            try { items = new DirectoryInfo(node.Path).EnumerateFileSystemInfos("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Offline }).ToList(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return; }
            foreach (var i in items)
            {
                if (i is FileInfo f)
                {
                    long len; try { len = f.Length; } catch (IOException) { continue; }
                    node.Bytes += len; node.Files++;
                    if (depth < maxDepth) node.Children.Add(new Node(f.FullName, f.Name, false) { Bytes = len, Files = 1, Parent = node });
                    if (++count % 500 == 0) progress?.Report(count);
                }
                else if (i is DirectoryInfo d)
                {
                    var c = new Node(d.FullName, d.Name, true) { Parent = node };
                    Walk(c, depth + 1);
                    node.Bytes += c.Bytes; node.Files += c.Files;
                    if (depth < maxDepth) node.Children.Add(c);
                }
            }
            node.Children.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
        }
        Walk(top, 0); progress?.Report(count);
        return top;
    }

    /// <summary>Squarified-Treemap (Bruls, Huizing, van Wijk): Flächen proportional zum Gewicht, möglichst quadratisch. Reihenfolge wie übergeben (größte zuerst).</summary>
    public static List<Rect> Squarify(IReadOnlyList<double> weights, Rect area)
    {
        var res = new Rect[weights.Count];
        var total = weights.Sum(); if (total <= 0 || area.W <= 0 || area.H <= 0) return [.. res.Select(_ => new Rect(area.X, area.Y, 0, 0))];
        var scale = area.W * area.H / total;
        var items = weights.Select((w, i) => (i, a: Math.Max(0, w) * scale)).ToList();
        double x = area.X, y = area.Y, w0 = area.W, h0 = area.H; var pos = 0;
        while (pos < items.Count)
        {
            var side = Math.Min(w0, h0); var row = new List<(int i, double a)>(); var best = double.MaxValue;
            while (pos < items.Count)
            {
                var test = row.Append(items[pos]).ToList(); var worst = Worst(test, side);
                if (row.Count > 0 && worst > best) break;
                row = test; best = worst; pos++;
            }
            var sum = row.Sum(r => r.a);
            if (w0 >= h0)
            {
                var colW = h0 <= 0 ? 0 : sum / h0; var yy = y;
                foreach (var (i, a) in row) { var hh = colW <= 0 ? 0 : a / colW; res[i] = new Rect(x, yy, colW, hh); yy += hh; }
                x += colW; w0 -= colW;
            }
            else
            {
                var rowH = w0 <= 0 ? 0 : sum / w0; var xx = x;
                foreach (var (i, a) in row) { var ww = rowH <= 0 ? 0 : a / rowH; res[i] = new Rect(xx, y, ww, rowH); xx += ww; }
                y += rowH; h0 -= rowH;
            }
        }
        return [.. res];
        static double Worst(List<(int i, double a)> row, double side)
        {
            var s = row.Sum(r => r.a); if (s <= 0 || side <= 0) return double.MaxValue;
            var mx = row.Max(r => r.a); var mn = row.Min(r => r.a); var s2 = side * side;
            return Math.Max(s2 * mx / (s * s), s * s / (s2 * Math.Max(mn, 1e-9)));
        }
    }

    public static string Kind(string name) => System.IO.Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".mp4" or ".mkv" or ".mov" or ".avi" or ".webm" or ".m4v" => "Video",
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".heic" or ".webp" or ".bmp" or ".raw" or ".cr2" or ".nef" or ".psd" => "Bild",
        ".mp3" or ".wav" or ".flac" or ".m4a" or ".ogg" or ".aac" => "Musik",
        ".pdf" or ".docx" or ".doc" or ".xlsx" or ".odt" or ".ods" or ".pptx" or ".txt" or ".md" or ".tdoc" => "Dokument",
        ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".iso" or ".img" => "Archiv",
        ".exe" or ".msi" or ".dll" or ".appx" or ".msix" => "Programm",
        _ => "Sonstiges",
    };

    /// <summary>Größte Dateien unter einem Knoten (für „Große Dateien“).</summary>
    public static List<Node> Largest(Node root, int max = 20)
    {
        var all = new List<Node>(); void W(Node n) { foreach (var c in n.Children) { if (c.IsDir) W(c); else all.Add(c); } }
        W(root); return all.OrderByDescending(n => n.Bytes).Take(max).ToList();
    }

    /// <summary>Knoten aus dem Baum entfernen (nach dem Papierkorb), Größen der Eltern nachziehen.</summary>
    public static void Remove(Node n)
    {
        if (n.Parent is not { } p) return;
        p.Children.Remove(n);
        for (var a = p; a is not null; a = a.Parent) { a.Bytes -= n.Bytes; a.Files -= n.Files; }
    }
}
