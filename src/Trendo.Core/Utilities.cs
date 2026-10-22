using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>X-09: Was belegt gerade Arbeitsspeicher? Nur Lesen – Trendo beendet keine fremden Programme.</summary>
public sealed record ProcInfo(string Name, int Count, long Bytes, string? Studio);

public static class ResourceMonitor
{
    public static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ollama"] = "KI", ["ollama app"] = "KI", ["blender"] = "3D Studio", ["UnrealEditor"] = "Game Studio", ["soffice"] = "Office", ["soffice.bin"] = "Office",
        ["mpv"] = "Medien", ["kdenlive"] = "Medien", ["TrendoOS"] = "Trendo OS", ["Code"] = "Entwickler", ["SumatraPDF"] = "PDF",
    };

    public static IReadOnlyList<ProcInfo> Group(IEnumerable<(string Name, long Bytes)> procs, int top = 12) =>
        procs.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ProcInfo(g.Key, g.Count(), g.Sum(x => x.Bytes), Known.GetValueOrDefault(g.Key)))
            .OrderByDescending(p => p.Bytes).Take(top).ToList();

    public static IReadOnlyList<ProcInfo> Snapshot(int top = 12)
    {
        var list = new List<(string, long)>();
        foreach (var p in Process.GetProcesses()) { try { list.Add((p.ProcessName, p.WorkingSet64)); } catch { } finally { p.Dispose(); } }
        return Group(list, top);
    }

    public static string Size(long b) => b >= 1L << 30 ? $"{b / (double)(1L << 30):0.0} GB" : $"{b / (double)(1L << 20):0} MB";
}

/// <summary>SYS-02: Notizen (notizen.json im Benutzerdatenordner).</summary>
public sealed record Note(string Id, string Text, DateTime Changed);

public sealed class NoteStore(string dataRoot)
{
    private string File_ => Path.Combine(dataRoot, "notizen.json");
    public List<Note> All() { try { return File.Exists(File_) ? JsonSerializer.Deserialize<List<Note>>(File.ReadAllText(File_)) ?? [] : []; } catch (JsonException) { return []; } }
    private void Save(List<Note> l) { var t = File_ + ".tmp"; File.WriteAllText(t, JsonSerializer.Serialize(l)); File.Move(t, File_, true); }
    public Note Add(string text) { var l = All(); var n = new Note(Guid.NewGuid().ToString("N")[..8], text.Trim(), DateTime.Now); l.Insert(0, n); Save(l); return n; }
    public void Update(string id, string text) { var l = All(); var i = l.FindIndex(n => n.Id == id); if (i >= 0) { l[i] = l[i] with { Text = text.Trim(), Changed = DateTime.Now }; Save(l); } }
    public void Delete(string id) { var l = All(); l.RemoveAll(n => n.Id == id); Save(l); }
}

/// <summary>SYS-02: Taschenrechner – + − × ÷ ^ %, Klammern, Komma oder Punkt. Kein eval, eigener Parser.</summary>
public static class Calc
{
    public static double Eval(string expr)
    {
        var s = expr.Replace(" ", "").Replace("×", "*").Replace("÷", "/").Replace("−", "-").Replace(',', '.');
        var i = 0; var v = Sum(); if (i != s.Length) throw new FormatException($"Unerwartetes Zeichen „{s[i]}“");
        return v;
        double Sum() { var x = Prod(); while (i < s.Length && s[i] is '+' or '-') { var o = s[i++]; var y = Prod(); x = o == '+' ? x + y : x - y; } return x; }
        double Prod() { var x = Pow(); while (i < s.Length && s[i] is '*' or '/') { var o = s[i++]; var y = Pow(); if (o == '/' && y == 0) throw new DivideByZeroException("Teilen durch 0 geht nicht"); x = o == '*' ? x * y : x / y; } return x; }
        double Pow() { var x = Unary(); if (i < s.Length && s[i] == '^') { i++; x = Math.Pow(x, Pow()); } return x; }
        double Unary() { if (i < s.Length && s[i] == '-') { i++; return -Unary(); } if (i < s.Length && s[i] == '+') { i++; return Unary(); } return Atom(); }
        double Atom()
        {
            double x;
            if (i < s.Length && s[i] == '(') { i++; x = Sum(); if (i >= s.Length || s[i] != ')') throw new FormatException("Klammer nicht geschlossen"); i++; }
            else
            {
                var st = i; while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                if (st == i) throw new FormatException("Zahl erwartet");
                x = double.Parse(s[st..i], CultureInfo.InvariantCulture);
            }
            if (i < s.Length && s[i] == '%') { i++; x /= 100; }
            return x;
        }
    }
    public static string Format(double v) => Math.Round(v, 10).ToString("#,##0.##########", CultureInfo.GetCultureInfo("de-DE"));

    /// <summary>Zahl aus einem Eingabefeld. Leere oder falsche Eingabe wird eine verständliche Meldung, kein Absturz.</summary>
    public static double Zahl(string? text, string feld)
    {
        var s = (text ?? "").Trim().Trim('€', '$', ' ').Trim();
        if (s.Length == 0) throw new ArgumentException($"Bitte {feld} ausfüllen.");
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.GetCultureInfo("de-DE"), out var v)) return v;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
        throw new ArgumentException($"„{s}“ ist keine Zahl. Bitte {feld} als Zahl eintragen.");
    }
}

/// <summary>SYS-02 Medien / SYS-10 Office / SYS-03 Downloads: Dateien eines Ordners nach Art, neueste zuerst.</summary>
public static class FileShelf
{
    public static readonly string[] Media = [".mp3", ".m4a", ".flac", ".wav", ".ogg", ".mp4", ".mkv", ".mov", ".avi", ".webm"];
    public static readonly string[] Office = [".docx", ".doc", ".odt", ".xlsx", ".xls", ".ods", ".pptx", ".odp", ".pdf", ".txt", ".md", ".csv", ".rtf"];

    public static IReadOnlyList<FileInfo> Recent(IEnumerable<string> folders, string[]? ext, int max = 40, int depth = 2)
    {
        var res = new List<FileInfo>();
        foreach (var f in folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var q = new Queue<(string, int)>(); q.Enqueue((f, 0));
            while (q.Count > 0 && res.Count < 5000)
            {
                var (d, lv) = q.Dequeue();
                try
                {
                    foreach (var file in Directory.EnumerateFiles(d))
                        if (ext is null || ext.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) res.Add(new FileInfo(file));
                    if (lv < depth) foreach (var s in Directory.EnumerateDirectories(d)) if (!Path.GetFileName(s).StartsWith('.')) q.Enqueue((s, lv + 1));
                }
                catch (UnauthorizedAccessException) { } catch (IOException) { }
            }
        }
        return res.Where(f => !f.Name.StartsWith("~$")).OrderByDescending(f => f.LastWriteTimeUtc).Take(max).ToList();
    }

    public static string Kind(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".docx" or ".doc" or ".odt" or ".rtf" => "Text", ".xlsx" or ".xls" or ".ods" or ".csv" => "Tabelle", ".pptx" or ".odp" => "Präsentation", ".pdf" => "PDF",
        ".mp3" or ".m4a" or ".flac" or ".wav" or ".ogg" => "Musik", ".mp4" or ".mkv" or ".mov" or ".avi" or ".webm" => "Video",
        ".exe" or ".msi" => "Programm", ".zip" or ".7z" or ".rar" => "Archiv", ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" => "Bild", _ => "Datei",
    };
}
