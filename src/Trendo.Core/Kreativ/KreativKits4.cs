using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core.Kreativ;

/// <summary>Code mit KI: Projektart erkennen, passenden Testbefehl vorschlagen, Schnappschuss der Projektdateien (SHA-256)
/// und Zeilen-Diff mit eigenem LCS. Nutzt den Projektbaum des vorhandenen Code-Editors (<see cref="CodeProject"/>).</summary>
public static class CodeAiKit
{
    public sealed record Kind(string Id, string Label, string Marker);
    public sealed record Command(string Exe, IReadOnlyList<string> Args)
    {
        public string Display => string.Join(" ", new[] { Exe }.Concat(Args).Select(a => a.Contains(' ') ? "\"" + a + "\"" : a));
    }

    static string Py => OperatingSystem.IsWindows() ? "python" : "python3";

    static string? First(string dir, params string[] patterns)
    {
        foreach (var p in patterns)
        {
            try { if (Directory.GetFiles(dir, p).Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault() is { } f) return Path.GetFileName(f); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return null;
    }

    static string ReadSmall(string file)
    {
        try { var fi = new FileInfo(file); return fi.Exists && fi.Length < 1_000_000 ? File.ReadAllText(file) : ""; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; }
    }

    /// <summary>Projektarten im Ordner (oberste Ebene), in dieser Reihenfolge: .NET, npm, Python, Cargo, Go.</summary>
    public static IReadOnlyList<Kind> DetectProject(string root)
    {
        if (!KreativSafe.LocalDir(root)) return [];
        var list = new List<Kind>();
        if (First(root, "*.sln", "*.slnx", "*.csproj", "*.fsproj", "*.vbproj") is { } d) list.Add(new("dotnet", ".NET", d));
        if (File.Exists(Path.Combine(root, "package.json"))) list.Add(new("npm", "JavaScript (npm)", "package.json"));
        var py = First(root, "pyproject.toml", "setup.py", "setup.cfg", "pytest.ini", "requirements.txt", "tox.ini", "test_*.py", "*_test.py")
                 ?? (Directory.Exists(Path.Combine(root, "tests")) && First(Path.Combine(root, "tests"), "test_*.py") is { } t ? "tests/" + t : null);
        if (py is not null) list.Add(new("python", "Python", py));
        if (File.Exists(Path.Combine(root, "Cargo.toml"))) list.Add(new("cargo", "Rust (Cargo)", "Cargo.toml"));
        if (File.Exists(Path.Combine(root, "go.mod"))) list.Add(new("go", "Go", "go.mod"));
        return list;
    }

    /// <summary>Testbefehl je Projektart. npm nur mit eigenem „test“-Skript (sonst Node-Testläufer), Python mit pytest, wenn es eingerichtet ist.</summary>
    public static Command? TestCommand(string root, string kind)
    {
        if (!KreativSafe.LocalDir(root)) return null;
        switch (kind)
        {
            case "dotnet":
                {
                    var sln = SafeFiles(root, "*.sln").Concat(SafeFiles(root, "*.slnx")).Order(StringComparer.OrdinalIgnoreCase).ToList();
                    var proj = SafeFiles(root, "*.csproj").Concat(SafeFiles(root, "*.fsproj")).Concat(SafeFiles(root, "*.vbproj")).ToList();
                    if (sln.Count == 0 && proj.Count == 0) return null;
                    // mehrere Lösungen oder Projekte: dotnet bräuchte eine Wahl – die erste Lösung (sonst das erste Projekt) nehmen
                    if (sln.Count > 1 || (sln.Count == 0 && proj.Count > 1) || (sln.Count == 1 && proj.Count > 0))
                        return new("dotnet", ["test", Path.GetFileName(sln.Count > 0 ? sln[0] : proj.Order(StringComparer.OrdinalIgnoreCase).First()), "--nologo"]);
                    return new("dotnet", ["test", "--nologo"]);
                }
            case "npm":
                {
                    var pkg = Path.Combine(root, "package.json");
                    if (!File.Exists(pkg)) return null;
                    try
                    {
                        using var doc = JsonDocument.Parse(ReadSmall(pkg));
                        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("scripts", out var s) && s.ValueKind == JsonValueKind.Object
                            && s.TryGetProperty("test", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() is { Length: > 0 } cmd && !cmd.Contains("no test specified", StringComparison.OrdinalIgnoreCase))
                            return new("npm", ["test"]);
                    }
                    catch (JsonException) { }
                    return new("node", ["--test"]);
                }
            case "python":
                {
                    var pytest = File.Exists(Path.Combine(root, "pytest.ini")) || File.Exists(Path.Combine(root, "conftest.py"))
                        || ReadSmall(Path.Combine(root, "pyproject.toml")).Contains("[tool.pytest", StringComparison.Ordinal)
                        || ReadSmall(Path.Combine(root, "setup.cfg")).Contains("[tool:pytest]", StringComparison.Ordinal)
                        || Regex.IsMatch(ReadSmall(Path.Combine(root, "requirements.txt")) + "\n" + ReadSmall(Path.Combine(root, "requirements-dev.txt")), @"(?im)^\s*pytest\b");
                    if (pytest) return new(Py, ["-m", "pytest", "-q"]);
                    var inTests = SafeFiles(root, "test_*.py").Count == 0 && Directory.Exists(Path.Combine(root, "tests"));
                    return new(Py, inTests ? ["-m", "unittest", "discover", "-s", "tests", "-q"] : ["-m", "unittest", "discover", "-q"]);
                }
            case "cargo": return File.Exists(Path.Combine(root, "Cargo.toml")) ? new("cargo", ["test"]) : null;
            case "go": return File.Exists(Path.Combine(root, "go.mod")) ? new("go", ["test", "./..."]) : null;
            default: return null;
        }
    }

    static List<string> SafeFiles(string dir, string pattern)
    {
        try { return [.. Directory.GetFiles(dir, pattern)]; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }
    }

    // ---------- Schnappschuss ----------

    public sealed record SnapFile(string Path, string Sha, long Size, string? Text);
    public sealed record Snapshot(string Root, DateTime TakenUtc, List<SnapFile> Files);
    public enum ChangeKind { Neu, Geaendert, Geloescht }
    public sealed record Change(string Path, ChangeKind Kind);

    /// <summary>Textinhalt wird bis zu dieser Größe je Datei mitgespeichert (für den Diff).</summary>
    public const long MaxTextBytes = 512 * 1024;
    public const long MaxTextTotal = 40L * 1024 * 1024;
    public const int MaxFiles = 20000;

    static bool Binary(byte[] b) => Array.IndexOf(b, (byte)0, 0, Math.Min(b.Length, 8000)) >= 0;
    static StringComparer PathCmp => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>Liegt <paramref name="path"/> im Projektordner, ohne dass ein Ordner (oder die Datei) dazwischen eine Verknüpfung ist?
    /// Verknüpfte Ordner könnten sonst aus dem Projekt hinausführen.</summary>
    public static bool InsideNoLinks(string root, string path)
    {
        var r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var f = Path.GetFullPath(path);
        if (!f.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            if (File.Exists(f) && new FileInfo(f).LinkTarget is not null) return false;
            for (var dir = Path.GetDirectoryName(f); dir is not null && dir.Length > r.Length; dir = Path.GetDirectoryName(dir))
                if (new DirectoryInfo(dir).LinkTarget is not null) return false;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Dateien über dieser Größe werden nicht gelesen, sondern über Größe und Änderungszeit verglichen (große Daten im Projekt bremsen sonst).</summary>
    public const long MaxHashBytes = 64L * 1024 * 1024;

    /// <summary>Prüfsumme jeder Datei im Projekt (ohne Bau-, Paket- und Git-Ordner wie im Code-Editor), Text kleiner Dateien für den späteren Vergleich.</summary>
    public static Snapshot Take(string root, DateTime? nowUtc = null)
    {
        if (!KreativSafe.LocalDir(root)) throw new ArgumentException("Wähle einen Projektordner auf diesem Rechner.");
        var full = Path.GetFullPath(root);
        var files = new List<SnapFile>(); long text = 0;
        var linked = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        bool UnderLink(string file)
        {
            for (var dir = Path.GetDirectoryName(file); dir is not null && dir.Length > full.TrimEnd(Path.DirectorySeparatorChar).Length; dir = Path.GetDirectoryName(dir))
            {
                if (!linked.TryGetValue(dir, out var l)) linked[dir] = l = new DirectoryInfo(dir).LinkTarget is not null;
                if (l) return true;
            }
            return false;
        }
        foreach (var e in CodeProject.Tree(full, null, MaxFiles * 3).Where(e => !e.IsDir).Take(MaxFiles))
        {
            try
            {
                var fi = new FileInfo(e.Path);
                if (fi.LinkTarget is not null || UnderLink(e.Path)) continue; // Verknüpfungen nicht folgen
                string sha; string? body = null;
                if (fi.Length <= MaxTextBytes && text + fi.Length <= MaxTextTotal)
                {
                    var bytes = File.ReadAllBytes(e.Path);
                    sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
                    if (!Binary(bytes)) { body = new UTF8Encoding(false).GetString(bytes).TrimStart('\uFEFF'); text += bytes.Length; }
                }
                else if (fi.Length > MaxHashBytes) sha = $"groesse:{fi.Length}:{fi.LastWriteTimeUtc.Ticks}";
                else { using var fs = File.OpenRead(e.Path); sha = Convert.ToHexStringLower(SHA256.HashData(fs)); }
                files.Add(new(Path.GetRelativePath(full, e.Path).Replace('\\', '/'), sha, fi.Length, body));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return new(full, nowUtc ?? DateTime.UtcNow, files);
    }

    public static List<Change> Changed(Snapshot before, Snapshot after)
    {
        var a = new Dictionary<string, SnapFile>(PathCmp); foreach (var f in before.Files) a[f.Path] = f;
        var b = new Dictionary<string, SnapFile>(PathCmp); foreach (var f in after.Files) b[f.Path] = f;
        var res = new List<Change>();
        foreach (var (p, f) in b)
            if (!a.TryGetValue(p, out var old)) res.Add(new(p, ChangeKind.Neu));
            else if (old.Sha != f.Sha) res.Add(new(p, ChangeKind.Geaendert));
        foreach (var p in a.Keys) if (!b.ContainsKey(p)) res.Add(new(p, ChangeKind.Geloescht));
        return res.OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string Save(Snapshot s) => JsonSerializer.Serialize(s);
    public static Snapshot? Load(string json)
    {
        try { return JsonSerializer.Deserialize<Snapshot>(json) is { Files: not null, Root: not null } s ? s : null; }
        catch (JsonException) { return null; }
    }

    // ---------- Zeilen-Diff (LCS) ----------

    public enum Op { Gleich, Weg, Neu }
    /// <summary>Zeile im Vergleich; <paramref name="Old"/>/<paramref name="New"/> sind Zeilennummern ab 1, 0 = gibt es auf dieser Seite nicht.</summary>
    public sealed record Line(Op Op, string Text, int Old, int New);

    static string[] Lines(string? s)
    {
        if (string.IsNullOrEmpty(s)) return [];
        var t = s.Replace("\r\n", "\n").Replace('\r', '\n');
        if (t.EndsWith('\n')) t = t[..^1];
        return t.Split('\n');
    }

    /// <summary>Zeilen-Diff über die längste gemeinsame Teilfolge. Gleicher Anfang und gleiches Ende werden vorher abgetrennt;
    /// ist der Rest größer als <paramref name="maxCells"/> Felder, gilt der ganze Rest als ersetzt (ehrlich statt langsam).</summary>
    public static IReadOnlyList<Line> Diff(string? before, string? after, long maxCells = 4_000_000)
    {
        var x = Lines(before); var y = Lines(after);
        var res = new List<Line>();
        int pre = 0; while (pre < x.Length && pre < y.Length && x[pre] == y[pre]) pre++;
        int suf = 0; while (suf < x.Length - pre && suf < y.Length - pre && x[x.Length - 1 - suf] == y[y.Length - 1 - suf]) suf++;
        for (var i = 0; i < pre; i++) res.Add(new(Op.Gleich, x[i], i + 1, i + 1));
        int n = x.Length - pre - suf, m = y.Length - pre - suf;
        if ((long)(n + 1) * (m + 1) > maxCells)
        {
            for (var i = 0; i < n; i++) res.Add(new(Op.Weg, x[pre + i], pre + i + 1, 0));
            for (var j = 0; j < m; j++) res.Add(new(Op.Neu, y[pre + j], 0, pre + j + 1));
        }
        else
        {
            var w = m + 1; var L = new int[(n + 1) * w];
            for (var i = n - 1; i >= 0; i--)
                for (var j = m - 1; j >= 0; j--)
                    L[i * w + j] = x[pre + i] == y[pre + j] ? L[(i + 1) * w + j + 1] + 1 : Math.Max(L[(i + 1) * w + j], L[i * w + j + 1]);
            int a = 0, b = 0;
            while (a < n || b < m)
            {
                if (a < n && b < m && x[pre + a] == y[pre + b]) { res.Add(new(Op.Gleich, x[pre + a], pre + a + 1, pre + b + 1)); a++; b++; }
                else if (b >= m || (a < n && L[(a + 1) * w + b] >= L[a * w + b + 1])) { res.Add(new(Op.Weg, x[pre + a], pre + a + 1, 0)); a++; }
                else { res.Add(new(Op.Neu, y[pre + b], 0, pre + b + 1)); b++; }
            }
        }
        for (var k = 0; k < suf; k++) res.Add(new(Op.Gleich, x[x.Length - suf + k], x.Length - suf + k + 1, y.Length - suf + k + 1));
        return res;
    }

    public static (int Added, int Removed) Count(IReadOnlyList<Line> diff) => (diff.Count(l => l.Op == Op.Neu), diff.Count(l => l.Op == Op.Weg));

    /// <summary>Einheitliches Diff-Format („unified“, wie git diff) mit <paramref name="context"/> Zeilen Umfeld; leer, wenn nichts anders ist.</summary>
    /// <param name="added">neue Datei: „--- /dev/null“; <paramref name="removed"/>: gelöschte Datei: „+++ /dev/null“ (so nimmt git apply es an).</param>
    public static string Unified(IReadOnlyList<Line> diff, string path, int context = 3, bool added = false, bool removed = false)
    {
        var changes = Enumerable.Range(0, diff.Count).Where(i => diff[i].Op != Op.Gleich).ToList();
        if (changes.Count == 0) return "";
        // Zeilen vor jedem Eintrag (alt/neu), für die Kopfzeilen der Abschnitte
        var oldBefore = new int[diff.Count + 1]; var newBefore = new int[diff.Count + 1];
        for (var i = 0; i < diff.Count; i++)
        {
            oldBefore[i + 1] = oldBefore[i] + (diff[i].Op != Op.Neu ? 1 : 0);
            newBefore[i + 1] = newBefore[i] + (diff[i].Op != Op.Weg ? 1 : 0);
        }
        var sb = new StringBuilder($"--- {(added ? "/dev/null" : "a/" + path)}\n+++ {(removed ? "/dev/null" : "b/" + path)}\n");
        var k = 0;
        while (k < changes.Count)
        {
            var from = Math.Max(0, changes[k] - context); var to = Math.Min(diff.Count - 1, changes[k] + context);
            while (k + 1 < changes.Count && changes[k + 1] - context <= to + 1) { k++; to = Math.Min(diff.Count - 1, changes[k] + context); }
            int ol = 0, nl = 0;
            for (var i = from; i <= to; i++) { if (diff[i].Op != Op.Neu) ol++; if (diff[i].Op != Op.Weg) nl++; }
            var os = ol == 0 ? oldBefore[from] : oldBefore[from] + 1; var ns = nl == 0 ? newBefore[from] : newBefore[from] + 1;
            sb.Append(CultureInfo.InvariantCulture, $"@@ -{os},{ol} +{ns},{nl} @@\n");
            for (var i = from; i <= to; i++) sb.Append(diff[i].Op switch { Op.Neu => '+', Op.Weg => '-', _ => ' ' }).Append(diff[i].Text).Append('\n');
            k++;
        }
        return sb.ToString();
    }
}

/// <summary>Bilder erkennen ohne Internet: Format aus den ersten Bytes, EXIF (Aufnahmedatum, Kamera, Belichtung), Helligkeit,
/// Schärfe (Varianz des Laplace-Filters) und Hauptfarben (k-means mit festem Startwert). Arbeitet auf RGBA-Bytes, damit es ohne Grafikbibliothek testbar ist.</summary>
public static class VisionKit
{
    public const string Ok = "in Ordnung", TooDark = "zu dunkel", TooBright = "zu hell";
    public const string SharpOk = "scharf", Blurry = "unscharf", Flat = "ohne Struktur", Tiny = "zu klein";
    public const double DefaultSharpLimit = 100;

    public static string Format(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return "PNG";
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return "JPEG";
        if (h.Length >= 4 && h[0] == 'G' && h[1] == 'I' && h[2] == 'F' && h[3] == '8') return "GIF";
        if (h.Length >= 2 && h[0] == 'B' && h[1] == 'M') return "BMP";
        if (h.Length >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return "WebP";
        if (h.Length >= 4 && ((h[0] == 'I' && h[1] == 'I' && h[2] == 42 && h[3] == 0) || (h[0] == 'M' && h[1] == 'M' && h[2] == 0 && h[3] == 42))) return "TIFF";
        if (h.Length >= 12 && h[4] == 'f' && h[5] == 't' && h[6] == 'y' && h[7] == 'p')
        {
            var brand = Encoding.ASCII.GetString(h.Slice(8, 4));
            if (brand is "heic" or "heix" or "mif1" or "msf1" or "hevc") return "HEIC";
            if (brand is "avif" or "avis") return "AVIF";
        }
        return "unbekannt";
    }

    // ---------- EXIF ----------

    public sealed record ExifInfo(DateTime? Taken, string Make, string Model, string Exposure, string FNumber, int? Iso, string Focal, int Orientation)
    {
        public string Camera
        {
            get
            {
                var make = Make.Trim(); var model = Model.Trim();
                if (model.Length == 0) return make;
                return make.Length > 0 && !model.StartsWith(make.Split(' ')[0], StringComparison.OrdinalIgnoreCase) ? make + " " + model : model;
            }
        }
    }

    /// <summary>EXIF aus JPEG (APP1 „Exif“), TIFF oder PNG (eXIf); null, wenn es keine gibt. Fremde Dateien: jede Länge wird geprüft.</summary>
    public static ExifInfo? Exif(byte[] f)
    {
        if (f is null || f.Length < 12) return null;
        var fmt = Format(f);
        if (fmt == "TIFF") return Tiff(f);
        if (fmt == "PNG")
        {
            for (var p = 8; p + 12 <= f.Length;)
            {
                var len = (int)Math.Min(int.MaxValue, (uint)(f[p] << 24 | f[p + 1] << 16 | f[p + 2] << 8 | f[p + 3]));
                if (len < 0 || p + 12L + len > f.Length) return null;
                if (f[p + 4] == 'e' && f[p + 5] == 'X' && f[p + 6] == 'I' && f[p + 7] == 'f') return Tiff(f.AsSpan(p + 8, len));
                if (f[p + 4] == 'I' && f[p + 5] == 'E' && f[p + 6] == 'N' && f[p + 7] == 'D') return null;
                p += 12 + len;
            }
            return null;
        }
        if (fmt != "JPEG") return null;
        for (var p = 2; p + 4 <= f.Length;)
        {
            if (f[p] != 0xFF) return null;
            var marker = f[p + 1];
            if (marker == 0xFF) { p++; continue; } // Füllbyte
            if (marker is 0xD8 or 0x01 || (marker >= 0xD0 && marker <= 0xD7)) { p += 2; continue; }
            if (marker is 0xDA or 0xD9) return null;
            var len = f[p + 2] << 8 | f[p + 3];
            if (len < 2 || p + 2 + len > f.Length) return null;
            if (marker == 0xE1 && len >= 8 && f[p + 4] == 'E' && f[p + 5] == 'x' && f[p + 6] == 'i' && f[p + 7] == 'f' && f[p + 8] == 0 && f[p + 9] == 0)
                return Tiff(f.AsSpan(p + 10, len - 8));
            p += 2 + len;
        }
        return null;
    }

    static ExifInfo? Tiff(ReadOnlySpan<byte> t)
    {
        if (t.Length < 8) return null;
        bool le;
        if (t[0] == 'I' && t[1] == 'I') le = true; else if (t[0] == 'M' && t[1] == 'M') le = false; else return null;
        var buf = t.ToArray();
        int U16(int o) => o < 0 || o + 2 > buf.Length ? -1 : le ? buf[o] | buf[o + 1] << 8 : buf[o] << 8 | buf[o + 1];
        long U32(int o) => o < 0 || o + 4 > buf.Length ? -1 : le ? (uint)(buf[o] | buf[o + 1] << 8 | buf[o + 2] << 16 | buf[o + 3] << 24) : (uint)(buf[o] << 24 | buf[o + 1] << 16 | buf[o + 2] << 8 | buf[o + 3]);
        if (U16(2) != 42) return null;
        var tags = new Dictionary<int, (int Type, long Count, int ValueAt)>();
        void Ifd(long at, int depth)
        {
            if (at < 8 || at + 2 > buf.Length || depth > 2) return;
            var n = U16((int)at); if (n <= 0 || n > 512) return;
            for (var i = 0; i < n; i++)
            {
                var e = (int)at + 2 + i * 12; if (e + 12 > buf.Length) return;
                int tag = U16(e), type = U16(e + 2); var count = U32(e + 4);
                var size = type switch { 1 or 2 or 6 or 7 => 1, 3 or 8 => 2, 4 or 9 or 11 => 4, 5 or 10 or 12 => 8, _ => 0 };
                if (size == 0 || count < 0 || count > 65535) continue;
                var valueAt = size * count <= 4 ? e + 8 : (int)Math.Min(int.MaxValue, U32(e + 8));
                if (valueAt < 0 || valueAt + size * count > buf.Length) continue;
                tags.TryAdd(tag, (type, count, valueAt));
                if (tag == 0x8769 && type is 4 or 13) Ifd(U32(e + 8), depth + 1);
            }
        }
        Ifd(U32(4), 0);
        if (tags.Count == 0) return null;
        string Ascii(int tag) => tags.TryGetValue(tag, out var v) && v.Type == 2 ? Encoding.UTF8.GetString(buf, v.ValueAt, (int)v.Count).Split('\0')[0].Trim() : "";
        (long N, long D)? Rat(int tag) => tags.TryGetValue(tag, out var v) && v.Type is 5 or 10 ? (U32(v.ValueAt), U32(v.ValueAt + 4)) : null;
        int? Int(int tag) => tags.TryGetValue(tag, out var v) ? v.Type == 3 ? U16(v.ValueAt) : v.Type == 4 ? (int)Math.Min(int.MaxValue, U32(v.ValueAt)) : null : null;
        DateTime? Date(int tag) => DateTime.TryParseExact(Ascii(tag), "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        var exp = Rat(0x829A) is { D: > 0 } e1 ? e1.N >= e1.D ? (e1.N / (double)e1.D).ToString("0.#", CultureInfo.InvariantCulture) + " s" : $"1/{Math.Round(e1.D / (double)Math.Max(1, e1.N))} s" : "";
        var fnum = Rat(0x829D) is { D: > 0 } f1 ? "f/" + (f1.N / (double)f1.D).ToString("0.#", CultureInfo.InvariantCulture) : "";
        var focal = Rat(0x920A) is { D: > 0 } f2 ? (f2.N / (double)f2.D).ToString("0.#", CultureInfo.InvariantCulture) + " mm" : "";
        return new(Date(0x9003) ?? Date(0x9004) ?? Date(0x0132), Ascii(0x010F), Ascii(0x0110), exp, fnum, Int(0x8827), focal, Int(0x0112) ?? 1);
    }

    // ---------- Pixel ----------

    static void Check(byte[] rgba, int w, int h)
    {
        if (w <= 0 || h <= 0 || rgba is null || rgba.Length < (long)w * h * 4) throw new ArgumentException("Die Bilddaten passen nicht zur Größe.");
    }

    /// <summary>Helligkeit je Bildpunkt (0–255, Gewichte nach Rec. 601).</summary>
    public static byte[] Gray(byte[] rgba, int w, int h)
    {
        Check(rgba, w, h);
        var g = new byte[w * h];
        for (int i = 0, p = 0; i < g.Length; i++, p += 4) g[i] = (byte)((77 * rgba[p] + 150 * rgba[p + 1] + 29 * rgba[p + 2] + 128) >> 8);
        return g;
    }

    public sealed record Light(double Mean, double DarkShare, double BrightShare, double Contrast, string Verdict);

    /// <summary>Mittlere Helligkeit, Anteil fast schwarzer (&lt; 32) und fast weißer (&gt; 235) Bildpunkte, Kontrast (Standardabweichung).</summary>
    public static Light Exposure(byte[] rgba, int w, int h)
    {
        var g = Gray(rgba, w, h);
        double sum = 0, sq = 0; int dark = 0, bright = 0;
        foreach (var v in g) { sum += v; sq += (double)v * v; if (v < 32) dark++; else if (v > 235) bright++; }
        var mean = sum / g.Length; var sd = Math.Sqrt(Math.Max(0, sq / g.Length - mean * mean));
        double ds = (double)dark / g.Length, bs = (double)bright / g.Length;
        var verdict = mean < 60 || ds > 0.6 ? TooDark : mean > 200 || bs > 0.6 ? TooBright : Ok;
        return new(Math.Round(mean, 1), Math.Round(ds, 3), Math.Round(bs, 3), Math.Round(sd, 1), verdict);
    }

    public sealed record Sharpness(double Variance, string Verdict);

    /// <summary>Schärfe als Varianz des Laplace-Filters (4er-Nachbarschaft) auf der Helligkeit. Unter <paramref name="limit"/> gilt das Bild als unscharf;
    /// ein Bild ohne Struktur (fast einfarbig) lässt sich nicht beurteilen.</summary>
    public static Sharpness Sharp(byte[] rgba, int w, int h, double limit = DefaultSharpLimit)
    {
        Check(rgba, w, h);
        if (w < 3 || h < 3) return new(0, Tiny);
        var g = Gray(rgba, w, h);
        double sum = 0, sq = 0, gs = 0, gq = 0; var n = 0;
        foreach (var v in g) { gs += v; gq += (double)v * v; }
        var gm = gs / g.Length; var gsd = Math.Sqrt(Math.Max(0, gq / g.Length - gm * gm));
        for (var y = 1; y < h - 1; y++)
            for (var x = 1; x < w - 1; x++)
            {
                var i = y * w + x;
                double lap = 4 * g[i] - g[i - 1] - g[i + 1] - g[i - w] - g[i + w];
                sum += lap; sq += lap * lap; n++;
            }
        var mean = sum / n; var variance = Math.Max(0, sq / n - mean * mean);
        var verdict = gsd < 4 ? Flat : variance < limit ? Blurry : SharpOk;
        return new(Math.Round(variance, 1), verdict);
    }

    public sealed record ColorShare(string Hex, double Share);

    /// <summary>Hauptfarben mit k-means: Auswahl der Bildpunkte in festen Abständen, Startpunkte nach k-means++ mit festem Startwert – gleiches Bild, gleiches Ergebnis.
    /// Durchsichtige Bildpunkte zählen nicht. Sortiert nach Anteil.</summary>
    public static IReadOnlyList<ColorShare> MainColors(byte[] rgba, int w, int h, int k = 5, int seed = 42, int maxSamples = 20000, int iterations = 15)
    {
        Check(rgba, w, h);
        var total = w * h; var step = Math.Max(1, total / Math.Max(1, maxSamples));
        var pts = new List<(double R, double G, double B)>();
        for (var i = 0; i < total; i += step) { var p = i * 4; if (rgba[p + 3] >= 128) pts.Add((rgba[p], rgba[p + 1], rgba[p + 2])); }
        if (pts.Count == 0 || k < 1) return [];
        static double D((double R, double G, double B) a, (double R, double G, double B) b) => (a.R - b.R) * (a.R - b.R) + (a.G - b.G) * (a.G - b.G) + (a.B - b.B) * (a.B - b.B);
        var rnd = new Random(seed);
        var centers = new List<(double R, double G, double B)> { pts[rnd.Next(pts.Count)] };
        var dist = new double[pts.Count];
        while (centers.Count < k)
        {
            double sumD = 0;
            for (var i = 0; i < pts.Count; i++) { var best = double.MaxValue; foreach (var c in centers) best = Math.Min(best, D(pts[i], c)); dist[i] = best; sumD += best; }
            if (sumD <= 0) break; // weniger verschiedene Farben als k
            var r = rnd.NextDouble() * sumD; var pick = pts.Count - 1;
            for (var i = 0; i < pts.Count; i++) { r -= dist[i]; if (r <= 0) { pick = i; break; } }
            centers.Add(pts[pick]);
        }
        var assign = new int[pts.Count];
        for (var it = 0; it < iterations; it++)
        {
            var changed = false;
            for (var i = 0; i < pts.Count; i++)
            {
                var best = 0; var bd = double.MaxValue;
                for (var c = 0; c < centers.Count; c++) { var dd = D(pts[i], centers[c]); if (dd < bd) { bd = dd; best = c; } }
                if (assign[i] != best || it == 0) { changed |= assign[i] != best; assign[i] = best; }
            }
            var acc = new (double R, double G, double B, int N)[centers.Count];
            for (var i = 0; i < pts.Count; i++) { var a = acc[assign[i]]; acc[assign[i]] = (a.R + pts[i].R, a.G + pts[i].G, a.B + pts[i].B, a.N + 1); }
            for (var c = 0; c < centers.Count; c++) if (acc[c].N > 0) centers[c] = (acc[c].R / acc[c].N, acc[c].G / acc[c].N, acc[c].B / acc[c].N);
            if (!changed && it > 0) break;
        }
        var counts = new int[centers.Count]; foreach (var a in assign) counts[a]++;
        return Enumerable.Range(0, centers.Count).Where(c => counts[c] > 0)
            .Select(c => new ColorShare($"#{(int)Math.Round(centers[c].R):X2}{(int)Math.Round(centers[c].G):X2}{(int)Math.Round(centers[c].B):X2}", Math.Round((double)counts[c] / pts.Count, 3)))
            .GroupBy(c => c.Hex).Select(g => new ColorShare(g.Key, Math.Round(g.Sum(x => x.Share), 3)))
            .OrderByDescending(c => c.Share).ThenBy(c => c.Hex, StringComparer.Ordinal).ToList();
    }

    // ---------- Bericht ----------

    public sealed record Report(string File, string Format, int Width, int Height, long Bytes, ExifInfo? Exif, Light Light, Sharpness Sharp, IReadOnlyList<ColorShare> Colors, string Text = "");

    public static Report Analyze(string file, byte[] head, long bytes, ExifInfo? exif, byte[] rgba, int w, int h, int width, int height, double sharpLimit = DefaultSharpLimit) =>
        new(file, Format(head), width, height, bytes, exif, Exposure(rgba, w, h), Sharp(rgba, w, h, sharpLimit), MainColors(rgba, w, h));

    public static bool Problem(Report r) => r.Sharp.Verdict == Blurry || r.Light.Verdict != Ok;

    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    static string Kb(long b) => (b / 1024.0).ToString("N0", De) + " KB";

    public static string Markdown(IReadOnlyList<Report> list, string title)
    {
        var sb = new StringBuilder($"# {title}\n\n");
        var bad = list.Where(Problem).ToList();
        if (list.Count > 1) sb.Append(CultureInfo.InvariantCulture, $"{list.Count} Bilder geprüft, {bad.Count} auffällig ({list.Count(r => r.Sharp.Verdict == Blurry)} unscharf, {list.Count(r => r.Light.Verdict == TooDark)} zu dunkel, {list.Count(r => r.Light.Verdict == TooBright)} zu hell).\n\n");
        foreach (var r in list)
        {
            sb.Append($"## {Path.GetFileName(r.File)}\n\n");
            sb.Append($"- Format: {r.Format}, {r.Width} × {r.Height} Bildpunkte, {Kb(r.Bytes)}\n");
            if (r.Exif is { } e)
            {
                if (e.Taken is { } t) sb.Append($"- Aufgenommen: {t.ToString("dd.MM.yyyy HH:mm", De)}\n");
                if (e.Camera.Length > 0) sb.Append($"- Kamera: {e.Camera}\n");
                var set = string.Join(", ", new[] { e.Exposure, e.FNumber, e.Iso is { } iso ? "ISO " + iso : "", e.Focal }.Where(s => s.Length > 0));
                if (set.Length > 0) sb.Append($"- Einstellung: {set}\n");
            }
            sb.Append($"- Helligkeit: {r.Light.Mean.ToString("0", De)} von 255 – {r.Light.Verdict}\n");
            sb.Append($"- Schärfe: {r.Sharp.Variance.ToString("0", De)} – {r.Sharp.Verdict}\n");
            if (r.Colors.Count > 0) sb.Append("- Hauptfarben: ").Append(string.Join(", ", r.Colors.Select(c => $"{c.Hex} ({(c.Share * 100).ToString("0", De)} %)"))).Append('\n');
            if (r.Text.Trim().Length > 0) sb.Append("\nText im Bild:\n\n> ").Append(r.Text.Trim().Replace("\n", "\n> ")).Append('\n');
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>CSV mit Semikolon (öffnet in deutschen Tabellenprogrammen richtig); Felder, die wie eine Formel beginnen, bekommen ein Hochkomma.</summary>
    public static string Csv(IReadOnlyList<Report> list)
    {
        static string C(string s)
        {
            if (s.Length > 0 && "=+-@\t\r".Contains(s[0])) s = "'" + s;
            return s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
        var sb = new StringBuilder("Datei;Format;Breite;Höhe;Größe (KB);Aufnahme;Kamera;Helligkeit;Belichtung;Schärfe;Urteil Schärfe;Hauptfarben\r\n");
        foreach (var r in list)
            sb.Append(string.Join(";", new[]
            {
                C(Path.GetFileName(r.File)), C(r.Format), r.Width.ToString(CultureInfo.InvariantCulture), r.Height.ToString(CultureInfo.InvariantCulture),
                Math.Round(r.Bytes / 1024.0).ToString(CultureInfo.InvariantCulture), r.Exif?.Taken?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "", C(r.Exif?.Camera ?? ""),
                r.Light.Mean.ToString("0", CultureInfo.InvariantCulture), C(r.Light.Verdict), r.Sharp.Variance.ToString("0", CultureInfo.InvariantCulture), C(r.Sharp.Verdict),
                C(string.Join(" ", r.Colors.Select(c => c.Hex))),
            })).Append("\r\n");
        return sb.ToString();
    }
}

/// <summary>Recherche mit Quellen: Quellenverwaltung mit Zitaten je Quelle, Zitierstile APA 7, Harvard und eine DIN-ISO-690-nahe Form,
/// Literaturverzeichnis, BibTeX hin und zurück, Doppelte erkennen, fehlende Pflichtfelder je Stil.
/// Ergänzt das Recherche-Studio (SYS-08), dessen Sitzungsquellen sich übernehmen lassen.</summary>
public static partial class CiteKit
{
    public sealed record Source
    {
        public string Id { get; init; } = "";
        /// <summary>buch, artikel oder webseite.</summary>
        public string Kind { get; init; } = "buch";
        /// <summary>Personen mit Semikolon getrennt, je „Nachname, Vorname“; eine Einrichtung ohne Komma.</summary>
        public string Authors { get; init; } = "";
        public string Title { get; init; } = "";
        public string Year { get; init; } = "";
        public string Publisher { get; init; } = "";
        public string Place { get; init; } = "";
        public string Edition { get; init; } = "";
        /// <summary>Zeitschrift (Artikel) bzw. Name der Webseite.</summary>
        public string Journal { get; init; } = "";
        public string Volume { get; init; } = "";
        public string Issue { get; init; } = "";
        public string Pages { get; init; } = "";
        public string Url { get; init; } = "";
        public string Doi { get; init; } = "";
        /// <summary>Abrufdatum JJJJ-MM-TT.</summary>
        public string Accessed { get; init; } = "";
    }

    public sealed record Quote(string Id, string SourceId, string Text, string Page, string Note = "");
    public sealed record Person(string Last, string First);

    public static readonly IReadOnlyList<(string Id, string Label)> Kinds = [("buch", "Buch"), ("artikel", "Artikel"), ("webseite", "Webseite")];
    public static readonly IReadOnlyList<(string Id, string Label)> Styles = [("apa", "APA 7"), ("harvard", "Harvard"), ("din", "DIN ISO 690 (nah)")];
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    static readonly HashSet<string> Particles = new(StringComparer.OrdinalIgnoreCase) { "von", "van", "de", "der", "den", "da", "di", "du", "le", "la", "zu", "vom", "zum", "ten", "ter" };

    [GeneratedRegex(@"\s*;\s*")]
    private static partial Regex SemiRx();
    [GeneratedRegex(@"\s+(?:and|und|&)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex AndRx();

    /// <summary>Trennt nur außerhalb von {…}: „{Bundesamt für Bau und Raum}“ bleibt ein Name.</summary>
    static List<string> SplitTop(string s, Regex sep)
    {
        var mask = new StringBuilder(s); var depth = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '{') depth++;
            else if (s[i] == '}') depth = Math.Max(0, depth - 1);
            else if (depth > 0) mask[i] = 'x';
        }
        var res = new List<string>(); var from = 0;
        foreach (Match m in sep.Matches(mask.ToString())) { res.Add(s[from..m.Index]); from = m.Index + m.Length; }
        res.Add(s[from..]);
        return res;
    }

    /// <summary>Personen aus „Muster, Erika; Beispiel, Max“ (oder „Erika Muster und Max Beispiel“); eine Einrichtung steht in {…}.</summary>
    public static IReadOnlyList<Person> People(string? authors)
    {
        var res = new List<Person>(); var a = (authors ?? "").Trim();
        var parts0 = a.Contains(';') ? SplitTop(a, SemiRx()) : SplitTop(a, AndRx());
        foreach (var raw in parts0.Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            if (raw.StartsWith('{') && raw.EndsWith('}')) { res.Add(new(raw[1..^1].Trim(), "")); continue; }
            if (raw.Contains(','))
            {
                var i = raw.IndexOf(','); res.Add(new(raw[..i].Trim(), raw[(i + 1)..].Trim()));
                continue;
            }
            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) { res.Add(new(parts[0], "")); continue; }
            var lastStart = parts.Length - 1;
            while (lastStart > 1 && Particles.Contains(parts[lastStart - 1])) lastStart--;
            res.Add(new(string.Join(' ', parts[lastStart..]), string.Join(' ', parts[..lastStart])));
        }
        return res;
    }

    /// <summary>„Erika Maria“ → „E. M.“, „Hans-Peter“ → „H.-P.“; schon gekürzte Vornamen bleiben.</summary>
    public static string Initials(string first) =>
        string.Join(" ", (first ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w =>
            string.Join("-", w.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(p => char.IsLetter(p[0]) ? char.ToUpper(p[0], De) + "." : p))));

    static string ApaName(Person p) => p.First.Length == 0 ? p.Last : $"{p.Last}, {Initials(p.First)}";
    static string DinFirst(Person p) => p.First.Length == 0 ? p.Last : $"{p.Last.ToUpper(De)}, {p.First}";
    static string DinOther(Person p) => p.First.Length == 0 ? p.Last : $"{p.First} {p.Last.ToUpper(De)}";

    static string AuthorsApa(IReadOnlyList<Person> ps)
    {
        var n = ps.Select(ApaName).ToList();
        return n.Count switch
        {
            0 => "",
            1 => n[0],
            2 => $"{n[0]}, & {n[1]}",
            <= 20 => string.Join(", ", n.Take(n.Count - 1)) + ", & " + n[^1],
            _ => string.Join(", ", n.Take(19)) + ", … " + n[^1],
        };
    }

    static string AuthorsHarvard(IReadOnlyList<Person> ps)
    {
        var n = ps.Select(ApaName).ToList();
        return n.Count switch { 0 => "", 1 => n[0], 2 => $"{n[0]} und {n[1]}", 3 => $"{n[0]}, {n[1]} und {n[2]}", _ => $"{n[0]} et al." };
    }

    static string AuthorsDin(IReadOnlyList<Person> ps) => ps.Count switch
    {
        0 => "",
        1 => DinFirst(ps[0]),
        2 => $"{DinFirst(ps[0])} und {DinOther(ps[1])}",
        3 => $"{DinFirst(ps[0])}, {DinOther(ps[1])} und {DinOther(ps[2])}",
        _ => $"{DinFirst(ps[0])}, u. a.",
    };

    static string YearOf(Source s) => s.Year.Trim().Length > 0 ? s.Year.Trim() : "o. J.";
    static string End(string s) { s = s.TrimEnd(); return s.Length == 0 || ".?!".Contains(s[^1]) ? s : s + "."; }
    static string EndIt(string title) { var t = title.Trim(); return ".?!".Contains(t.Length > 0 ? t[^1] : ' ') ? $"*{t}*" : $"*{t}*."; }

    /// <summary>Seitenbereich mit Halbgeviertstrich.</summary>
    public static string PageRange(string p) => Regex.Replace((p ?? "").Trim(), @"\s*(?:--|-|–|—)\s*", "–");

    public static string NormDoi(string? doi)
    {
        var d = (doi ?? "").Trim();
        d = Regex.Replace(d, @"^(?:https?://(?:dx\.)?doi\.org/|doi:\s*)", "", RegexOptions.IgnoreCase);
        return d.ToLowerInvariant();
    }

    public static bool ValidDoi(string? doi) => Regex.IsMatch(NormDoi(doi), @"^10\.\d{4,9}/\S+$");

    static string Edition(string e)
    {
        e = (e ?? "").Trim();
        if (e.Length == 0 || e == "1") return "";
        return int.TryParse(e.TrimEnd('.'), out var n) ? $"{n}. Aufl." : e;
    }

    public static DateTime? Date(string? s) =>
        DateTime.TryParseExact((s ?? "").Trim(), ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy"], De, DateTimeStyles.None, out var d) ? d : null;
    static string Long(DateTime d) => d.ToString("d. MMMM yyyy", De);
    static string DoiUrl(string doi) => "https://doi.org/" + NormDoi(doi);

    /// <summary>Eintrag fürs Literaturverzeichnis als Markdown (kursiv mit *…*).</summary>
    public static string Format(Source s, string style)
    {
        var ps = People(s.Authors); var y = YearOf(s); var t = s.Title.Trim();
        var doi = s.Doi.Trim().Length > 0 ? NormDoi(s.Doi) : "";
        var acc = Date(s.Accessed);
        var parts = new List<string>();
        switch (style)
        {
            case "harvard":
                {
                    var head = ps.Count > 0 ? $"{AuthorsHarvard(ps)} ({y})" : "";
                    if (s.Kind == "artikel")
                    {
                        var title = $"„{t}“";
                        parts.Add(head.Length > 0 ? $"{head} {title}," : $"{title} ({y}),");
                        var vol = s.Volume.Trim() + (s.Issue.Trim().Length > 0 ? $"({s.Issue.Trim()})" : "");
                        var tail = string.Join(", ", new[] { $"*{s.Journal.Trim()}*", vol, s.Pages.Trim().Length > 0 ? "S. " + PageRange(s.Pages) : "" }.Where(x => x.Length > 0 && x != "**"));
                        parts.Add(End(tail));
                        if (doi.Length > 0) parts.Add($"doi: {doi}.");
                    }
                    else if (s.Kind == "webseite")
                    {
                        parts.Add(head.Length > 0 ? $"{head} {EndIt(t)}" : $"{EndIt(t).TrimEnd('.')} ({y}).");
                        if (s.Url.Trim().Length > 0) parts.Add($"Verfügbar unter: {s.Url.Trim()}" + (acc is { } a ? $" (Zugriff: {Long(a)})." : "."));
                    }
                    else
                    {
                        parts.Add(head.Length > 0 ? $"{head} {EndIt(t)}" : $"{EndIt(t).TrimEnd('.')} ({y}).");
                        if (Edition(s.Edition) is { Length: > 0 } ed) parts.Add(ed);
                        var pub = s.Place.Trim().Length > 0 && s.Publisher.Trim().Length > 0 ? $"{s.Place.Trim()}: {s.Publisher.Trim()}" : s.Publisher.Trim() + s.Place.Trim();
                        if (pub.Length > 0) parts.Add(End(pub));
                        if (doi.Length > 0) parts.Add($"doi: {doi}.");
                    }
                    break;
                }
            case "din":
                {
                    var head = ps.Count > 0 ? $"{AuthorsDin(ps)}, {y}." : "";
                    if (s.Kind == "artikel")
                    {
                        parts.Add(head.Length > 0 ? $"{head} {End(t)}" : $"{End(t).TrimEnd('.')}, {y}.");
                        if (s.Journal.Trim().Length > 0) parts.Add($"*{s.Journal.Trim()}*.");
                        var vol = s.Volume.Trim() + (s.Issue.Trim().Length > 0 ? $"({s.Issue.Trim()})" : "");
                        var tail = string.Join(", ", new[] { vol, s.Pages.Trim().Length > 0 ? "S. " + PageRange(s.Pages) : "" }.Where(x => x.Length > 0));
                        if (tail.Length > 0) parts.Add(End(tail));
                        if (doi.Length > 0) parts.Add($"DOI: {doi}.");
                    }
                    else if (s.Kind == "webseite")
                    {
                        parts.Add(head.Length > 0 ? $"{head} *{t}* [online]." : $"*{t}* [online], {y}.");
                        if (s.Journal.Trim().Length > 0) parts.Add(End(s.Journal.Trim()));
                        if (acc is { } a) parts.Add($"[Zugriff am: {Long(a)}].");
                        if (s.Url.Trim().Length > 0) parts.Add($"Verfügbar unter: {s.Url.Trim()}");
                    }
                    else
                    {
                        parts.Add(head.Length > 0 ? $"{head} {EndIt(t)}" : $"{EndIt(t).TrimEnd('.')}, {y}.");
                        if (Edition(s.Edition) is { Length: > 0 } ed) parts.Add(ed);
                        var pub = s.Place.Trim().Length > 0 && s.Publisher.Trim().Length > 0 ? $"{s.Place.Trim()}: {s.Publisher.Trim()}" : s.Publisher.Trim() + s.Place.Trim();
                        if (pub.Length > 0) parts.Add(End(pub));
                        if (doi.Length > 0) parts.Add($"DOI: {doi}.");
                    }
                    break;
                }
            default: // APA 7
                {
                    var head = ps.Count > 0 ? $"{AuthorsApa(ps)} ({y})." : "";
                    if (s.Kind == "artikel")
                    {
                        parts.Add(head.Length > 0 ? $"{head} {End(t)}" : $"{End(t)} ({y}).");
                        var vol = (s.Volume.Trim().Length > 0 ? $"*{s.Volume.Trim()}*" : "") + (s.Issue.Trim().Length > 0 ? $"({s.Issue.Trim()})" : "");
                        var tail = string.Join(", ", new[] { s.Journal.Trim().Length > 0 ? $"*{s.Journal.Trim()}*" : "", vol, PageRange(s.Pages) }.Where(x => x.Length > 0));
                        if (tail.Length > 0) parts.Add(End(tail));
                        if (doi.Length > 0) parts.Add(DoiUrl(doi)); else if (s.Url.Trim().Length > 0) parts.Add(s.Url.Trim());
                    }
                    else if (s.Kind == "webseite")
                    {
                        parts.Add(head.Length > 0 ? $"{head} {EndIt(t)}" : $"{EndIt(t)} ({y}).");
                        if (s.Journal.Trim().Length > 0) parts.Add(End(s.Journal.Trim()));
                        if (s.Url.Trim().Length > 0) parts.Add(acc is { } a ? $"Abgerufen am {Long(a)}, von {s.Url.Trim()}" : s.Url.Trim());
                    }
                    else
                    {
                        var ed = Edition(s.Edition);
                        var title = ed.Length > 0 ? $"*{t}* ({ed})." : EndIt(t);
                        parts.Add(head.Length > 0 ? $"{head} {title}" : $"{title} ({y}).");
                        if (s.Publisher.Trim().Length > 0) parts.Add(End(s.Publisher.Trim()));
                        if (doi.Length > 0) parts.Add(DoiUrl(doi));
                    }
                    break;
                }
        }
        return string.Join(" ", parts.Where(p => p.Length > 0)).Replace("**", "");
    }

    /// <summary>Ohne Markdown-Sternchen (für Text und Zwischenablage).</summary>
    public static string Plain(string md) => (md ?? "").Replace("*", "");

    /// <summary>Kurzbeleg im Text, z. B. (Muster &amp; Beispiel, 2024, S. 12).</summary>
    public static string InText(Source s, string style, string page = "")
    {
        var ps = People(s.Authors); var y = YearOf(s);
        var pg = page.Trim().Length > 0 ? ", S. " + PageRange(page) : "";
        var shortTitle = KreativSafe.Cut(s.Title.Trim(), 40);
        string names = style switch
        {
            "din" => ps.Count switch { 0 => shortTitle, 1 => ps[0].Last.ToUpper(De), 2 => $"{ps[0].Last.ToUpper(De)} und {ps[1].Last.ToUpper(De)}", _ => $"{ps[0].Last.ToUpper(De)} u. a." },
            "harvard" => ps.Count switch { 0 => shortTitle, 1 => ps[0].Last, 2 => $"{ps[0].Last} und {ps[1].Last}", 3 => $"{ps[0].Last}, {ps[1].Last} und {ps[2].Last}", _ => $"{ps[0].Last} et al." },
            _ => ps.Count switch { 0 => shortTitle, 1 => ps[0].Last, 2 => $"{ps[0].Last} & {ps[1].Last}", _ => $"{ps[0].Last} et al." },
        };
        return style is "din" or "harvard" ? $"({names} {y}{pg})" : $"({names}, {y}{pg})";
    }

    public static string QuoteLine(Quote q, Source s, string style) => $"„{q.Text.Trim()}“ {InText(s, style, q.Page)}";

    static string SortKey(Source s) { var ps = People(s.Authors); return (ps.Count > 0 ? ps[0].Last + " " + ps[0].First : s.Title).Trim(); }

    /// <summary>Literaturverzeichnis: nach erster Person (sonst Titel), dann Jahr und Titel – deutsche Sortierung ohne Groß/Klein.</summary>
    public static List<Source> Sorted(IEnumerable<Source> sources)
    {
        var cmp = StringComparer.Create(De, CompareOptions.IgnoreCase);
        return sources.OrderBy(SortKey, cmp).ThenBy(s => s.Year, cmp).ThenBy(s => s.Title, cmp).ToList();
    }

    public static List<string> Bibliography(IEnumerable<Source> sources, string style) => Sorted(sources).Select(s => Format(s, style)).ToList();

    public static string Markdown(IEnumerable<Source> sources, IEnumerable<Quote> quotes, string style, string title = "Literaturverzeichnis")
    {
        var list = Sorted(sources); var label = Styles.FirstOrDefault(x => x.Id == style).Label ?? "APA 7";
        var sb = new StringBuilder($"# {title}\n\nZitierstil: {label}\n\n");
        foreach (var s in list) sb.Append("- ").Append(Format(s, style)).Append('\n');
        var qs = quotes.ToList();
        if (qs.Count > 0)
        {
            sb.Append("\n## Zitate\n");
            foreach (var s in list)
            {
                var mine = qs.Where(q => q.SourceId == s.Id).ToList();
                if (mine.Count == 0) continue;
                sb.Append($"\n### {s.Title.Trim()}\n\n");
                foreach (var q in mine) sb.Append("> ").Append(QuoteLine(q, s, style)).Append('\n').Append(q.Note.Trim().Length > 0 ? $"\n{q.Note.Trim()}\n" : "").Append('\n');
            }
        }
        return sb.ToString();
    }

    /// <summary>Was für den Stil fehlt oder nicht stimmt (Feldnamen wie in der Oberfläche).</summary>
    public static List<string> Missing(Source s, string style)
    {
        var m = new List<string>();
        void Need(bool ok, string label) { if (!ok) m.Add(label); }
        bool Has(string v) => v.Trim().Length > 0;
        Need(Has(s.Title), "Titel");
        switch (s.Kind)
        {
            case "artikel":
                Need(Has(s.Authors), "Autorin oder Autor"); Need(Has(s.Year), "Jahr"); Need(Has(s.Journal), "Zeitschrift"); Need(Has(s.Pages), "Seiten");
                if (style == "apa") Need(Has(s.Volume), "Jahrgang (Band)");
                break;
            case "webseite":
                Need(Has(s.Url), "Adresse (URL)");
                if (style != "apa") Need(Has(s.Accessed), "Abrufdatum");
                break;
            default:
                Need(Has(s.Authors) || style == "apa", "Autorin oder Autor"); Need(Has(s.Year), "Jahr"); Need(Has(s.Publisher), "Verlag");
                if (style != "apa") Need(Has(s.Place), "Ort");
                break;
        }
        if (Has(s.Year) && !Regex.IsMatch(s.Year.Trim(), @"^(\d{4}[a-z]?|o\. ?J\.)$")) m.Add("Jahr als vierstellige Zahl");
        if (Has(s.Accessed) && Date(s.Accessed) is null) m.Add("Abrufdatum als TT.MM.JJJJ");
        if (Has(s.Doi) && !ValidDoi(s.Doi)) m.Add("DOI im Format 10.xxxx/…");
        if (Has(s.Url) && !Regex.IsMatch(s.Url.Trim(), @"^https?://\S+$", RegexOptions.IgnoreCase)) m.Add("Adresse mit https://");
        return m;
    }

    // ---------- Doppelte ----------

    static readonly HashSet<string> Tracking = new(StringComparer.OrdinalIgnoreCase) { "fbclid", "gclid", "mc_cid", "mc_eid", "ref" };

    public static string NormUrl(string? url)
    {
        var u = (url ?? "").Trim();
        if (u.Length == 0) return "";
        if (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https")) return u.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant(); if (host.StartsWith("www.", StringComparison.Ordinal)) host = host[4..];
        var q = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => { var k = p.Split('=')[0]; return !k.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) && !Tracking.Contains(k); }).Order(StringComparer.Ordinal).ToList();
        var path = uri.AbsolutePath.TrimEnd('/');
        return $"{host}{(uri.IsDefaultPort ? "" : ":" + uri.Port)}{path}{(q.Count > 0 ? "?" + string.Join("&", q) : "")}";
    }

    public static string NormTitle(string? title)
    {
        var t = (title ?? "").ToLower(De).Replace("ß", "ss").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in t)
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            else sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    /// <summary>Gruppen gleicher Quellen: gleiche DOI, gleiche Adresse (ohne www, Schrägstrich am Ende, Anker und Werbe-Parameter) oder gleicher Titel bei gleichem (oder fehlendem) Jahr.</summary>
    public static List<List<Source>> Duplicates(IReadOnlyList<Source> sources)
    {
        var parent = Enumerable.Range(0, sources.Count).ToArray();
        int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
        void Join(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[Math.Max(a, b)] = Math.Min(a, b); }
        // Schlüssel einmal je Quelle; DOI und Adresse über Gruppen, Titel nur innerhalb gleicher Titel (Jahr prüfen)
        var doi = sources.Select(s => NormDoi(s.Doi)).ToList(); var url = sources.Select(s => NormUrl(s.Url)).ToList(); var title = sources.Select(s => NormTitle(s.Title)).ToList();
        foreach (var g in Enumerable.Range(0, sources.Count).Where(i => doi[i].Length > 0).GroupBy(i => doi[i])) foreach (var i in g.Skip(1)) Join(g.First(), i);
        foreach (var g in Enumerable.Range(0, sources.Count).Where(i => url[i].Length > 0).GroupBy(i => url[i])) foreach (var i in g.Skip(1)) Join(g.First(), i);
        foreach (var g in Enumerable.Range(0, sources.Count).Where(i => title[i].Length > 3).GroupBy(i => title[i]))
        {
            var idx = g.ToList();
            for (var x = 0; x < idx.Count; x++)
                for (var y = x + 1; y < idx.Count; y++)
                {
                    string ya = sources[idx[x]].Year.Trim(), yb = sources[idx[y]].Year.Trim();
                    if (ya.Length == 0 || yb.Length == 0 || ya == yb) Join(idx[x], idx[y]);
                }
        }
        return Enumerable.Range(0, sources.Count).GroupBy(Find).Where(g => g.Count() > 1).Select(g => g.Select(i => sources[i]).ToList()).ToList();
    }

    // ---------- BibTeX ----------

    static string BibEsc(string v)
    {
        var sb = new StringBuilder();
        foreach (var c in v ?? "")
            sb.Append(c switch
            {
                '\\' => "\\textbackslash{}", '{' => "\\{", '}' => "\\}", '&' => "\\&", '%' => "\\%", '$' => "\\$", '#' => "\\#", '_' => "\\_",
                '\r' => "", '\n' => " ", _ => c.ToString(),
            });
        return sb.ToString();
    }

    static string Ascii(string s)
    {
        var t = (s ?? "").Replace("ß", "ss").Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue").Normalize(NormalizationForm.FormD);
        return new string(t.Where(c => c < 128 && char.IsLetterOrDigit(c)).ToArray());
    }

    static string KeyFor(Source s, HashSet<string> used)
    {
        var ps = People(s.Authors);
        var who = Ascii(ps.Count > 0 ? ps[0].Last : "").ToLowerInvariant();
        var word = Ascii(s.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(w => w.Length > 3) ?? s.Title).ToLowerInvariant();
        var year = new string(s.Year.Where(char.IsDigit).ToArray());
        var key = (who + year + word); if (key.Length == 0) key = "quelle";
        key = KreativSafe.Cut(key, 40);
        var k = key; for (var i = 0; !used.Add(k); i++) k = key + (char)('a' + i % 26) + (i >= 26 ? (i / 26).ToString(CultureInfo.InvariantCulture) : "");
        return k;
    }

    static string BibAuthors(string authors) =>
        string.Join(" and ", People(authors).Select(p => p.First.Length == 0 ? "{" + BibEsc(p.Last) + "}" : BibEsc(p.Last) + ", " + BibEsc(p.First)));

    public static string BibTex(IEnumerable<Source> sources)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var sb = new StringBuilder();
        foreach (var s in sources)
        {
            var type = s.Kind switch { "artikel" => "article", "webseite" => "online", _ => "book" };
            sb.Append('@').Append(type).Append('{').Append(KeyFor(s, used)).Append(",\n");
            void F(string name, string value, bool raw = false) { if (value.Trim().Length > 0) sb.Append("  ").Append(name).Append(" = {").Append(raw ? value.Trim() : BibEsc(value.Trim())).Append("},\n"); }
            F("author", BibAuthors(s.Authors), raw: true);
            F("title", s.Title); F("year", s.Year); F("publisher", s.Publisher); F("address", s.Place); F("edition", s.Edition);
            F(s.Kind == "webseite" ? "organization" : "journal", s.Journal); F("volume", s.Volume); F("number", s.Issue);
            // DOI und Adresse sind in BibLaTeX wörtliche Felder: nicht maskieren, nur Klammern und Umbrüche entfernen
            static string Verbatim(string v) => new((v ?? "").Where(c => c is not ('{' or '}' or '\r' or '\n')).ToArray());
            F("pages", PageRange(s.Pages).Replace("–", "--")); F("doi", s.Doi.Trim().Length > 0 ? Verbatim(NormDoi(s.Doi)) : "", raw: true);
            F("url", Verbatim(s.Url), raw: true); F("urldate", Date(s.Accessed) is { } d ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : s.Accessed);
            sb.Append("}\n\n");
        }
        return sb.ToString();
    }

    public const int MaxBibChars = 5_000_000;
    static readonly Dictionary<string, string> Months = new(StringComparer.OrdinalIgnoreCase) { ["jan"] = "1", ["feb"] = "2", ["mar"] = "3", ["apr"] = "4", ["may"] = "5", ["jun"] = "6", ["jul"] = "7", ["aug"] = "8", ["sep"] = "9", ["oct"] = "10", ["nov"] = "11", ["dec"] = "12" };

    /// <summary>Liest BibTeX/BibLaTeX robust: Werte in {…}, "…", Zahlen und @string-Kürzel mit #, LaTeX-Akzente werden zu Umlauten.
    /// Kaputte Einträge werden übersprungen und gemeldet, der Rest wird gelesen.</summary>
    public static (List<Source> Sources, List<string> Warnings) ParseBibTex(string text)
    {
        var res = new List<Source>(); var warn = new List<string>();
        text ??= "";
        if (text.Length > MaxBibChars) { warn.Add("Die Datei ist zu groß (höchstens 5 MB)."); return (res, warn); }
        var macros = new Dictionary<string, string>(Months, StringComparer.OrdinalIgnoreCase);
        var pos = 0; var n = 0;
        while (res.Count < 5000)
        {
            var at = text.IndexOf('@', pos); if (at < 0) break;
            pos = at + 1;
            var tStart = pos; while (pos < text.Length && (char.IsLetter(text[pos]))) pos++;
            var type = text[tStart..pos].ToLowerInvariant();
            if (type.Length == 0) continue;
            while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
            if (pos >= text.Length || (text[pos] != '{' && text[pos] != '(')) continue;
            var close = text[pos] == '{' ? '}' : ')';
            var bodyStart = pos + 1;
            if (type is "comment" or "preamble") { var after = SkipBalanced(text, pos); pos = after < 0 ? text.Length : after; continue; }
            if (type == "string")
            {
                try { var (name, value, after) = ReadField(text, bodyStart, macros); macros[name] = value; pos = SkipTo(text, after, close) + 1; }
                catch (FormatException e) { warn.Add("Kürzel (@string) übersprungen: " + e.Message); var next = text.IndexOf("\n@", bodyStart, StringComparison.Ordinal); pos = next < 0 ? text.Length : next + 1; }
                continue;
            }
            n++;
            try
            {
                pos = bodyStart;
                var keyEnd = text.IndexOfAny([',', close], pos);
                if (keyEnd < 0) throw new FormatException("Eintrag endet nicht.");
                var key = text[pos..keyEnd].Trim();
                pos = keyEnd;
                var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                while (true)
                {
                    while (pos < text.Length && (char.IsWhiteSpace(text[pos]) || text[pos] == ',')) pos++;
                    if (pos >= text.Length) throw new FormatException("Eintrag endet nicht.");
                    if (text[pos] == close) { pos++; break; }
                    if (text[pos] == '@') throw new FormatException("Klammer fehlt.");
                    var (name, value, after) = ReadField(text, pos, macros);
                    fields[name] = value; pos = after;
                }
                res.Add(FromBib(type, key, fields, res.Count));
            }
            catch (FormatException e)
            {
                warn.Add($"Eintrag {n} übersprungen: {e.Message}");
                var next = text.IndexOf("\n@", Math.Min(text.Length, bodyStart), StringComparison.Ordinal);
                pos = next < 0 ? text.Length : next + 1;
            }
        }
        if (res.Count >= 5000 && text.IndexOf('@', pos) >= 0) warn.Add("Es werden höchstens 5000 Einträge gelesen – der Rest der Datei wurde nicht übernommen.");
        return (res, warn);
    }

    static int SkipBalanced(string t, int open)
    {
        var close = t[open] == '{' ? '}' : ')'; var openC = t[open]; var depth = 0;
        for (var i = open; i < t.Length; i++)
        {
            if (t[i] == '\\') { i++; continue; }
            if (t[i] == openC) depth++;
            else if (t[i] == close && --depth == 0) return i + 1;
        }
        return -1; // nicht geschlossen
    }

    static int SkipTo(string t, int from, char c)
    {
        while (from < t.Length && (char.IsWhiteSpace(t[from]) || t[from] == ',')) from++;
        if (from >= t.Length || t[from] != c) throw new FormatException("Klammer fehlt.");
        return from;
    }

    /// <summary>name = wert [# wert …]; gibt den rohen Wert (Klammern noch drin) zurück.</summary>
    static (string Name, string Value, int After) ReadField(string t, int pos, Dictionary<string, string> macros)
    {
        var ns = pos; while (pos < t.Length && (char.IsLetterOrDigit(t[pos]) || t[pos] is '_' or '-' or ':' or '.')) pos++;
        var name = t[ns..pos].Trim().ToLowerInvariant();
        if (name.Length == 0) throw new FormatException("Feldname fehlt.");
        while (pos < t.Length && char.IsWhiteSpace(t[pos])) pos++;
        if (pos >= t.Length || t[pos] != '=') throw new FormatException($"„=“ fehlt nach „{name}“.");
        pos++;
        var value = new StringBuilder();
        while (true)
        {
            while (pos < t.Length && char.IsWhiteSpace(t[pos])) pos++;
            if (pos >= t.Length) throw new FormatException("Wert fehlt.");
            if (t[pos] == '{')
            {
                var end = SkipBalanced(t, pos); if (end < 0) throw new FormatException("Geschweifte Klammer nicht geschlossen.");
                value.Append(t, pos + 1, end - pos - 2); pos = end;
            }
            else if (t[pos] == '"')
            {
                var depth = 0; var i = pos + 1;
                for (; i < t.Length; i++)
                {
                    if (t[i] == '\\') { i++; continue; }
                    if (t[i] == '{') depth++; else if (t[i] == '}') depth--;
                    else if (t[i] == '"' && depth == 0) break;
                }
                if (i >= t.Length) throw new FormatException("Anführungszeichen nicht geschlossen.");
                value.Append(t, pos + 1, i - pos - 1); pos = i + 1;
            }
            else
            {
                var s = pos; while (pos < t.Length && (char.IsLetterOrDigit(t[pos]) || t[pos] is '_' or '-' or ':' or '.')) pos++;
                if (pos == s) throw new FormatException($"Wert von „{name}“ unlesbar.");
                var word = t[s..pos];
                value.Append(macros.TryGetValue(word, out var mv) ? mv : word);
            }
            while (pos < t.Length && char.IsWhiteSpace(t[pos])) pos++;
            if (pos < t.Length && t[pos] == '#') { pos++; continue; }
            return (name, value.ToString(), pos);
        }
    }

    static readonly Dictionary<char, char> Accents = new()
    {
        ['"'] = '\u0308', ['\''] = '\u0301', ['`'] = '\u0300', ['^'] = '\u0302', ['~'] = '\u0303', ['='] = '\u0304', ['.'] = '\u0307',
        ['u'] = '\u0306', ['v'] = '\u030C', ['H'] = '\u030B', ['c'] = '\u0327', ['k'] = '\u0328', ['r'] = '\u030A',
    };

    [GeneratedRegex(@"\\([""'`^~=.])\s*(?:\{\s*([A-Za-z]|\\i|\\j)\s*\}|([A-Za-z]|\\i|\\j))")]
    private static partial Regex SymAccentRx();
    [GeneratedRegex(@"\\([uvHckr])(?:\s*\{\s*([A-Za-z])\s*\}|\s+([A-Za-z]))")]
    private static partial Regex LetterAccentRx();
    // Steuerwort endet mit {} oder einem Leerzeichen, das TeX verschluckt
    [GeneratedRegex(@"\\(ss|ae|AE|oe|OE|aa|AA|o|O|l|L)(?![A-Za-z])(?:\{\}|\s)?")]
    private static partial Regex LigatureRx();
    [GeneratedRegex(@"\\(?:url|textit|textbf|emph|textsc|texttt|mkbibquote|enquote)\s*\{([^{}]*)\}")]
    private static partial Regex CommandRx();

    /// <summary>LaTeX-Schreibweisen zu Unicode: \"a → ä, {\ss} → ß, \&amp; → &amp;, -- → –, Klammern weg.</summary>
    public static string Unlatex(string v)
    {
        var s = v ?? "";
        s = s.Replace("\\textbackslash{}", "\u0001").Replace("\\textbackslash", "\u0001");
        for (var i = 0; i < 3; i++) s = CommandRx().Replace(s, "$1");
        s = SymAccentRx().Replace(s, m => { var l = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value; l = l switch { "\\i" => "i", "\\j" => "j", _ => l }; return (l + Accents[m.Groups[1].Value[0]]).Normalize(NormalizationForm.FormC); });
        s = LetterAccentRx().Replace(s, m => ((m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value) + Accents[m.Groups[1].Value[0]]).Normalize(NormalizationForm.FormC));
        s = LigatureRx().Replace(s, m => m.Groups[1].Value switch { "ss" => "ß", "ae" => "æ", "AE" => "Æ", "oe" => "œ", "OE" => "Œ", "aa" => "å", "AA" => "Å", "o" => "ø", "O" => "Ø", "l" => "ł", _ => "Ł" });
        s = s.Replace("\\{", "\u0002").Replace("\\}", "\u0003");
        s = Regex.Replace(s, @"\\([&%$#_])", "$1");
        s = s.Replace("---", "—").Replace("--", "–").Replace("~", " ").Replace("{", "").Replace("}", "");
        s = s.Replace("\u0001", "\\").Replace("\u0002", "{").Replace("\u0003", "}");
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    /// <summary>„and“ nur außerhalb von Klammern trennt Personen; {Einrichtung} bleibt ein Name.</summary>
    [GeneratedRegex(@"\s+and\s+", RegexOptions.IgnoreCase)]
    private static partial Regex BibAndRx();

    static string AuthorsFromBib(string raw) =>
        string.Join("; ", SplitTop(raw, BibAndRx()).Select(x => x.Trim()).Where(x => x.Length > 0).Select(x =>
        {
            if (x.StartsWith('{') && SkipBalanced(x, 0) == x.Length) return "{" + Unlatex(x) + "}"; // Einrichtung
            var u = Unlatex(x);
            if (u.Contains(',')) return u;
            var p = People(u); return p.Count == 1 && p[0].First.Length > 0 ? $"{p[0].Last}, {p[0].First}" : u;
        }));

    static Source FromBib(string type, string key, Dictionary<string, string> f, int index)
    {
        string G(params string[] names) { foreach (var n in names) if (f.TryGetValue(n, out var v) && v.Trim().Length > 0) return Unlatex(v); return ""; }
        // wörtliche Felder: keine LaTeX-Umwandlung (Adressen dürfen ~ und -- enthalten)
        string Raw(string name) => f.TryGetValue(name, out var v) ? Regex.Replace(v.Replace("{", "").Replace("}", ""), @"\s+", "") : "";
        var url = Raw("url"); if (url.Length == 0 && G("howpublished") is { } hp && Regex.IsMatch(hp, @"^https?://", RegexOptions.IgnoreCase)) url = hp;
        var kind = type switch
        {
            "article" or "inproceedings" or "conference" => "artikel",
            "online" or "www" or "webpage" or "electronic" => "webseite",
            "misc" => url.Length > 0 ? "webseite" : "buch",
            _ => "buch",
        };
        var year = G("year"); if (year.Length == 0 && Regex.Match(G("date"), @"\d{4}") is { Success: true } ym) year = ym.Value;
        var authors = f.TryGetValue("author", out var a) && a.Trim().Length > 0 ? AuthorsFromBib(a) : f.TryGetValue("editor", out var e) ? AuthorsFromBib(e) : "";
        return new Source
        {
            Id = "q" + (index + 1).ToString(CultureInfo.InvariantCulture) + "-" + Ascii(key).ToLowerInvariant(),
            Kind = kind, Authors = authors, Title = G("title"), Year = year,
            Publisher = kind == "webseite" ? G("publisher") : G("publisher", "institution", "school", "organization"),
            Place = G("address", "location"), Edition = G("edition"),
            Journal = kind == "webseite" ? G("organization", "journaltitle", "journal") : G("journal", "journaltitle", "booktitle"),
            Volume = G("volume"), Issue = G("number", "issue"), Pages = PageRange(G("pages")),
            Url = url, Doi = Raw("doi").Length > 0 ? NormDoi(Raw("doi")) : "", Accessed = Raw("urldate"),
        };
    }
}

/// <summary>Tagesplaner: Aufgaben mit Dauer, Wichtigkeit und Frist um feste Termine herum in Zeitblöcke legen – zwischen Arbeitsbeginn und -ende,
/// mit Pausen nach längerer Arbeit; was nicht passt, kommt auf die Liste für morgen. Ausgabe als Kalenderdatei (RFC 5545) und als Text.</summary>
public static partial class DayPlanner
{
    public sealed record Item(string Id, string Title, int Minutes, int Importance = 2, DateTime? Due = null, bool Done = false);
    public sealed record Appointment(string Id, string Title, DateTime Start, DateTime End);
    public sealed record Block(DateTime Start, DateTime End, string Title, string Kind, string? ItemId = null);
    /// <param name="WorkBeforeBreak">Minuten Arbeit am Stück, bevor eine Pause eingeplant wird.</param>
    /// <param name="BreakMinutes">Länge der Pause; 0 = keine Pausen.</param>
    public sealed record Options(TimeSpan DayStart, TimeSpan DayEnd, int WorkBeforeBreak = 90, int BreakMinutes = 10);
    public sealed record Later(Item Item, string Why);
    public sealed record Plan(DateTime Day, IReadOnlyList<Block> Blocks, IReadOnlyList<Later> Tomorrow);

    public const string Work = "aufgabe", Fixed = "termin", Pause = "pause";
    public static readonly IReadOnlyList<(int Id, string Label)> Levels = [(1, "hoch"), (2, "mittel"), (3, "niedrig")];
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    public static string Level(int importance) => Levels.FirstOrDefault(l => l.Id == importance).Label ?? "mittel";

    /// <summary>Reihenfolge: früheste Frist zuerst (ohne Frist zuletzt), dann Wichtigkeit, dann Eingabereihenfolge.</summary>
    public static List<Item> Order(IEnumerable<Item> items) =>
        items.Select((x, i) => (x, i)).Where(t => !t.x.Done).OrderBy(t => t.x.Due?.Date ?? DateTime.MaxValue).ThenBy(t => Math.Clamp(t.x.Importance, 1, 3)).ThenBy(t => t.i).Select(t => t.x).ToList();

    /// <summary>Plant einen Tag. Termine werden zu belegten Zeiten (überlappende zusammengefasst), Aufgaben werden am Stück in die freien Lücken gelegt –
    /// die wichtigste passende zuerst; passt sie nicht, füllt eine kleinere die Lücke. Nach <see cref="Options.WorkBeforeBreak"/> Minuten Arbeit kommt eine Pause;
    /// ein Termin oder eine Lücke ab Pausenlänge gilt als Unterbrechung. Kein Block reicht über einen Termin oder über das Tagesende.</summary>
    /// <param name="notBefore">Für „heute“: nicht vor dieser Uhrzeit beginnen (auf 5 Minuten aufgerundet).</param>
    public static Plan Make(DateTime day, IEnumerable<Item> items, IEnumerable<Appointment> appointments, Options o, DateTime? notBefore = null)
    {
        day = day.Date;
        if (o.DayStart < TimeSpan.Zero || o.DayEnd > TimeSpan.FromHours(24) || o.DayEnd <= o.DayStart) throw new ArgumentException("Das Arbeitsende muss nach dem Arbeitsbeginn liegen (am selben Tag).");
        if (o.WorkBeforeBreak < 15) throw new ArgumentException("Pause frühestens nach 15 Minuten Arbeit.");
        if (o.BreakMinutes is < 0 or > 120) throw new ArgumentException("Die Pause darf höchstens 120 Minuten lang sein.");
        int ws = (int)o.DayStart.TotalMinutes, we = (int)o.DayEnd.TotalMinutes;
        if (notBefore is { } nb)
        {
            if (nb.Date > day) ws = we;
            else if (nb.Date == day) ws = Math.Max(ws, (int)Math.Ceiling(nb.TimeOfDay.TotalMinutes / 5) * 5);
        }
        ws = Math.Min(ws, we);

        var blocks = new List<Block>();
        var busy = new List<(int S, int E)>();
        foreach (var a in appointments.Where(a => a.End > a.Start && a.Start < day.AddDays(1) && a.End > day).OrderBy(a => a.Start))
        {
            var s = a.Start < day ? day : a.Start; var e = a.End > day.AddDays(1) ? day.AddDays(1) : a.End;
            blocks.Add(new(s, e, a.Title, Fixed, a.Id));
            busy.Add(((int)Math.Floor((s - day).TotalMinutes), (int)Math.Ceiling((e - day).TotalMinutes)));
        }
        // belegte Zeiten zusammenfassen
        var merged = new List<(int S, int E)>();
        foreach (var b in busy.OrderBy(b => b.S))
            if (merged.Count > 0 && b.S <= merged[^1].E) merged[^1] = (merged[^1].S, Math.Max(merged[^1].E, b.E));
            else merged.Add(b);
        // freie Lücken im Arbeitsfenster
        var free = new List<(int S, int E)>(); var c0 = ws;
        foreach (var b in merged)
        {
            if (b.E <= c0) continue;
            if (b.S >= we) break;
            if (b.S > c0) free.Add((c0, Math.Min(b.S, we)));
            c0 = Math.Max(c0, b.E);
        }
        if (c0 < we) free.Add((c0, we));

        var remaining = Order(items);
        var later = new List<Later>();
        foreach (var bad in remaining.Where(x => x.Minutes <= 0 || x.Minutes > 24 * 60).ToList()) { later.Add(new(bad, "Dauer fehlt oder ist zu lang")); remaining.Remove(bad); }
        int run = 0, cursor, lastWork = int.MinValue / 2;
        foreach (var (s, e) in free)
        {
            if (o.BreakMinutes == 0 || s - lastWork >= o.BreakMinutes) run = 0; // Termin oder Lücke seit der letzten Arbeit als Unterbrechung
            cursor = s;
            while (true)
            {
                Item? pick = null; var needBreak = false;
                foreach (var it in remaining)
                {
                    var nbk = o.BreakMinutes > 0 && run > 0 && run + it.Minutes > o.WorkBeforeBreak;
                    if (cursor + (nbk ? o.BreakMinutes : 0) + it.Minutes <= e) { pick = it; needBreak = nbk; break; }
                }
                if (pick is null) break;
                if (needBreak)
                {
                    blocks.Add(new(day.AddMinutes(cursor), day.AddMinutes(cursor + o.BreakMinutes), "Pause", Pause));
                    cursor += o.BreakMinutes; run = 0;
                }
                blocks.Add(new(day.AddMinutes(cursor), day.AddMinutes(cursor + pick.Minutes), pick.Title, Work, pick.Id));
                cursor += pick.Minutes; run += pick.Minutes; lastWork = cursor;
                remaining.Remove(pick);
            }
        }
        var longest = free.Select(f => f.E - f.S).DefaultIfEmpty(0).Max();
        foreach (var it in remaining) later.Add(new(it, it.Minutes > longest ? "länger als jede freie Zeit heute" : "passt heute nicht mehr"));
        return new(day, blocks.OrderBy(b => b.Start).ThenBy(b => b.Kind == Fixed ? 0 : 1).ToList(), later);
    }

    /// <summary>Für „auf morgen schieben“: was noch offen ist, in der bisherigen Reihenfolge.</summary>
    public static List<Item> Carry(IEnumerable<Item> items) => items.Where(i => !i.Done).ToList();

    public static (int Planned, int Breaks, int Free) Summary(Plan p, Options o)
    {
        var planned = (int)p.Blocks.Where(b => b.Kind == Work).Sum(b => (b.End - b.Start).TotalMinutes);
        var breaks = (int)p.Blocks.Where(b => b.Kind == Pause).Sum(b => (b.End - b.Start).TotalMinutes);
        var ws = p.Day + o.DayStart; var we = p.Day + o.DayEnd;
        var fixedIn = p.Blocks.Where(b => b.Kind == Fixed).Select(b => (S: b.Start < ws ? ws : b.Start, E: b.End > we ? we : b.End)).Where(x => x.E > x.S).OrderBy(x => x.S).ToList();
        var busyMin = 0.0; var end = ws;
        foreach (var (s, e) in fixedIn) { var from = s > end ? s : end; if (e > from) { busyMin += (e - from).TotalMinutes; end = e; } }
        return (planned, breaks, Math.Max(0, (int)((we - ws).TotalMinutes - busyMin) - planned - breaks));
    }

    // ---------- Ausgabe ----------

    public static string Text(Plan p, Options o)
    {
        var sb = new StringBuilder($"Tagesplan für {p.Day.ToString("dddd, d. MMMM yyyy", De)}\n");
        sb.Append($"Arbeitszeit {Hm(o.DayStart)}–{Hm(o.DayEnd)}\n\n");
        foreach (var b in p.Blocks)
            sb.Append($"{b.Start:HH\\:mm}–{b.End:HH\\:mm}  ").Append(b.Kind switch { Fixed => "Termin: " + b.Title, Pause => "Pause", _ => b.Title }).Append('\n');
        if (p.Tomorrow.Count > 0)
        {
            sb.Append("\nMorgen\n");
            foreach (var l in p.Tomorrow) sb.Append($"- {l.Item.Title} ({l.Item.Minutes} min, {Level(l.Item.Importance)}) – {l.Why}\n");
        }
        var (pl, br, fr) = Summary(p, o);
        sb.Append($"\nGeplant {pl} min, Pausen {br} min, frei {fr} min\n");
        return sb.ToString();
    }

    static string Hm(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}";

    /// <summary>Text für eine iCalendar-Eigenschaft: \ ; , und Zeilenumbrüche maskieren (RFC 5545, 3.3.11).</summary>
    public static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n");

    /// <summary>Zeilen über 75 Byte (UTF-8) falten: Umbruch, dann ein Leerzeichen (RFC 5545, 3.1). Mehrbyte-Zeichen werden nicht zerschnitten.</summary>
    public static string Fold(string line)
    {
        var sb = new StringBuilder(); var used = 0; var limit = 75;
        foreach (var r in line.EnumerateRunes())
        {
            var n = r.Utf8SequenceLength;
            if (used + n > limit) { sb.Append("\r\n "); used = 1; limit = 75; }
            sb.Append(r.ToString()); used += n;
        }
        return sb.ToString();
    }

    /// <summary>Kalenderdatei mit Aufgaben- und Pausenblöcken (und auf Wunsch den Terminen), Zeiten als lokale Uhrzeit („floating“), CRLF, gefaltet.</summary>
    public static string Ics(Plan p, DateTime stampUtc, bool withAppointments = false)
    {
        var lines = new List<string> { "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//Tagesplaner//DE", "CALSCALE:GREGORIAN", "METHOD:PUBLISH" };
        var i = 0;
        foreach (var b in p.Blocks.Where(b => withAppointments || b.Kind != Fixed))
        {
            i++;
            var uid = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{b.Start:O}|{b.End:O}|{b.Kind}|{b.Title}|{b.ItemId}")))[..20];
            lines.Add("BEGIN:VEVENT");
            lines.Add($"UID:{uid}-{i}@tagesplaner");
            lines.Add("DTSTAMP:" + stampUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
            lines.Add("DTSTART:" + b.Start.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture));
            lines.Add("DTEND:" + b.End.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture));
            lines.Add("SUMMARY:" + Escape(b.Kind == Pause ? "Pause" : b.Title));
            lines.Add("CATEGORIES:" + (b.Kind switch { Fixed => "Termin", Pause => "Pause", _ => "Aufgabe" }));
            lines.Add("TRANSP:" + (b.Kind == Pause ? "TRANSPARENT" : "OPAQUE"));
            lines.Add("END:VEVENT");
        }
        lines.Add("END:VCALENDAR");
        return string.Concat(lines.Select(l => Fold(l) + "\r\n"));
    }

    // ---------- Eingabe aus Text ----------

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*(h|std|stunden?|min|minuten|m)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DurRx();
    [GeneratedRegex(@"\bbis\s+(?:zum\s+)?(\d{1,2})\.(\d{1,2})\.(\d{2,4})?", RegexOptions.IgnoreCase)]
    private static partial Regex DueRx();

    /// <summary>Eine Aufgabe je Zeile, z. B. „Angebot schreiben, 90 min, hoch, bis 20.11.“. Ohne Dauer 30 Minuten, ohne Wichtigkeit mittel.</summary>
    public static List<Item> ParseItems(string text, DateTime today)
    {
        var res = new List<Item>();
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim().TrimStart('-', '*', '•').Trim();
            if (line.Length == 0) continue;
            var min = 30;
            if (DurRx().Match(line) is { Success: true } m)
            {
                var v = double.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
                min = (int)Math.Round(m.Groups[2].Value.StartsWith('h') || m.Groups[2].Value.StartsWith("st", StringComparison.OrdinalIgnoreCase) ? v * 60 : v);
                line = line.Remove(m.Index, m.Length);
            }
            DateTime? due = null;
            if (DueRx().Match(line) is { Success: true } d)
            {
                var y = d.Groups[3].Success ? int.Parse(d.Groups[3].Value, CultureInfo.InvariantCulture) : today.Year; if (y < 100) y += 2000;
                if (DateTime.TryParseExact($"{d.Groups[1].Value}.{d.Groups[2].Value}.{y}", "d.M.yyyy", De, DateTimeStyles.None, out var dd)) { if (!d.Groups[3].Success && dd < today.AddDays(-30)) dd = dd.AddYears(1); due = dd; }
                line = line.Remove(d.Index, d.Length);
            }
            var imp = Regex.IsMatch(line, @"\b(hoch|wichtig|dringend)\b", RegexOptions.IgnoreCase) ? 1 : Regex.IsMatch(line, @"\b(niedrig|unwichtig|irgendwann)\b", RegexOptions.IgnoreCase) ? 3 : 2;
            line = Regex.Replace(line, @"\b(hoch|wichtig|dringend|mittel|niedrig|unwichtig|irgendwann)\b", "", RegexOptions.IgnoreCase);
            var title = Regex.Replace(line, @"[\s,;]+", " ").Trim(' ', ',', ';', '–', '-');
            if (title.Length == 0) continue;
            res.Add(new($"t{res.Count + 1}", KreativSafe.Cut(title, 200), Math.Clamp(min, 5, 24 * 60), imp, due));
        }
        return res;
    }

    /// <summary>„8“, „08:30“, „8.30“, „8h30“ → Uhrzeit; 24:00 ist das Tagesende.</summary>
    public static TimeSpan? ParseTime(string? s)
    {
        var m = Regex.Match((s ?? "").Trim(), @"^(\d{1,2})(?:[:.h](\d{2}))?$");
        if (!m.Success) return null;
        var h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); var mi = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        if (h > 24 || mi > 59 || (h == 24 && mi > 0)) return null;
        return new TimeSpan(h, mi, 0);
    }
}
