using System.Globalization;
using System.Text;

namespace Trendo.Core.Media;

/// <summary>Paket 01 – Studio 3, S3-17 (07.11.2026): Abspann-Vorlage – Abschnitte (Rolle → Namen), Bewegung über editierbare Keyframes
/// (Zeit, Lage, Deckkraft), Ausgabe als Videoclip mit FFmpeg. Die Schrift wird vor der Freigabe auf ihre Lizenz geprüft.</summary>
public static class Credits
{
    public sealed record Section(string Role, IReadOnlyList<string> Names);

    /// <param name="Y">Lage der Textoberkante als Anteil der Bildhöhe (1 = unten außerhalb, 0 = oben, negativ = oben hinaus).</param>
    public sealed record Keyframe(double Time, double Y, double Opacity = 1);

    public sealed record Template(string Title, IReadOnlyList<Section> Sections, double Seconds, IReadOnlyList<Keyframe> Keys, string Font, int Width = 1280, int Height = 720);

    /// <summary>Rollender Abspann: von unten herein, oben hinaus, am Anfang und Ende kurz eingeblendet.</summary>
    public static Template Rolling(string title, IReadOnlyList<Section> sections, double seconds, string font, int w = 1280, int h = 720) =>
        new(title, sections, seconds, [new(0, 1.0, 0), new(0.5, 0.9, 1), new(seconds - 0.5, -0.6, 1), new(seconds, -0.7, 0)], font, w, h);

    public static Template Validate(Template t)
    {
        if (!(t.Seconds is >= 1 and <= 600)) throw new ArgumentException("Abspann: 1 bis 600 Sekunden.");
        if (t.Keys.Count < 2) throw new ArgumentException("Mindestens zwei Keyframes (Anfang und Ende).");
        for (var i = 0; i < t.Keys.Count; i++)
        {
            var k = t.Keys[i];
            if (k.Time < 0 || k.Time > t.Seconds + 1e-9) throw new ArgumentException($"Keyframe {i + 1}: Zeit {k.Time:0.##} s liegt außerhalb des Abspanns.");
            if (i > 0 && k.Time <= t.Keys[i - 1].Time) throw new ArgumentException($"Keyframe {i + 1}: Zeiten müssen aufsteigen.");
            if (!(k.Opacity is >= 0 and <= 1) || !double.IsFinite(k.Y) || k.Y is < -5 or > 5) throw new ArgumentException($"Keyframe {i + 1}: Lage oder Deckkraft ungültig.");
        }
        if (t.Sections.Count == 0 || t.Sections.All(s => s.Names.Count == 0)) throw new ArgumentException("Der Abspann ist leer.");
        return t;
    }

    /// <summary>Wert zwischen den Keyframes (linear) – auch für die Vorschau.</summary>
    public static (double Y, double Opacity) At(IReadOnlyList<Keyframe> keys, double t)
    {
        if (t <= keys[0].Time) return (keys[0].Y, keys[0].Opacity);
        for (var i = 1; i < keys.Count; i++)
            if (t <= keys[i].Time) { var a = keys[i - 1]; var b = keys[i]; var f = (t - a.Time) / (b.Time - a.Time); return (a.Y + (b.Y - a.Y) * f, a.Opacity + (b.Opacity - a.Opacity) * f); }
        return (keys[^1].Y, keys[^1].Opacity);
    }

    static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>FFmpeg-Ausdruck für eine Größe über die Zeit (stückweise linear).</summary>
    public static string Expr(IReadOnlyList<Keyframe> keys, Func<Keyframe, double> v)
    {
        var e = F(v(keys[^1]));
        for (var i = keys.Count - 1; i >= 1; i--)
        {
            var a = keys[i - 1]; var b = keys[i];
            e = $"if(lt(t\\,{F(b.Time)})\\,{F(v(a))}+({F(v(b))}-{F(v(a))})*(t-{F(a.Time)})/{F(b.Time - a.Time)}\\,{e})";
        }
        return $"if(lt(t\\,{F(keys[0].Time)})\\,{F(v(keys[0]))}\\,{e})";
    }

    /// <summary>Text des Abspanns: Titel, dann je Abschnitt Rolle und Namen, Leerzeilen dazwischen.</summary>
    public static string Text(Template t)
    {
        var sb = new StringBuilder(t.Title.Trim()).Append("\n\n");
        foreach (var s in t.Sections.Where(s => s.Names.Count > 0)) { sb.Append(s.Role.Trim().ToUpperInvariant()).Append('\n'); foreach (var n in s.Names) sb.Append(n.Trim()).Append('\n'); sb.Append('\n'); }
        return sb.ToString().TrimEnd() + "\n";
    }

    /// <summary>Zeilen mit Satz wie im Kino: Titel groß, Rolle klein und grau, Namen mittelgroß; Abstand in Bildhöhen-Anteilen.</summary>
    public static List<(string Text, double Size, string Color, double Offset)> Layout(Template t)
    {
        var h = (double)t.Height; double big = h / 14, role = h / 30, name = h / 20, y = 0;
        var l = new List<(string, double, string, double)> { (t.Title.Trim(), big, "white", 0) }; y += big * 2;
        foreach (var s in t.Sections.Where(s => s.Names.Count > 0))
        {
            l.Add((s.Role.Trim().ToUpperInvariant(), role, "0xB8BCC6", y)); y += role * 1.6;
            foreach (var n in s.Names) { l.Add((n.Trim(), name, "white", y)); y += name * 1.35; }
            y += name;
        }
        return [.. l.Select(x => (x.Item1, Math.Round(x.Item2), x.Item3, x.Item4 / h))];
    }

    /// <summary>FFmpeg-Aufruf: schwarzer Grund, jede Zeile für sich zentriert, Lage und Deckkraft aus den Keyframes. <paramref name="textFile"/> (Textfassung) wird vorher geschrieben.</summary>
    public static List<string> Args(Template t, string textFile, string output)
    {
        Validate(t);
        var font = t.Font.Replace('\\', '/').Replace(":", "\\:");
        var y = Expr(t.Keys, k => k.Y); var alpha = Expr(t.Keys, k => k.Opacity);
        var vf = string.Join(",", Layout(t).Select(x => $"drawtext=fontfile='{font}':text='{VideoEdit.DrawText(x.Text)}':fontcolor={x.Color}:fontsize={F(x.Size)}:x=(w-text_w)/2:y=h*({y}+{F(x.Offset)}):alpha={alpha}"));
        return ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", $"color=c=black:s={t.Width}x{t.Height}:r=25:d={F(t.Seconds)}", "-f", "lavfi", "-i", $"anullsrc=r=48000:cl=stereo",
            "-vf", vf, "-t", F(t.Seconds), "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", output];
    }

    // ───────────── Schrift-Lizenz ─────────────

    public sealed record FontCheck(string Light, string Text, string License, int FsType);

    /// <summary>Schrift prüfen (nur lesen): Lizenztext aus der Name-Tabelle (ID 13/14) und Einbettungsrecht (OS/2 fsType).
    /// Rot: Einbetten verboten. Gelb: keine Lizenzangabe – vor Veröffentlichung prüfen. Grün: freie Lizenz (OFL, Apache, …) oder Systemschrift zum Rendern.</summary>
    public static FontCheck CheckFont(string path)
    {
        if (!File.Exists(path)) return new("rot", "Schrift fehlt: " + Path.GetFileName(path), "", 0);
        byte[] b; try { b = File.ReadAllBytes(path); } catch (IOException e) { return new("rot", "Schrift nicht lesbar: " + e.Message, "", 0); }
        if (b.Length < 12) return new("rot", "Keine Schriftdatei.", "", 0);
        int U16(int p) => p + 1 < b.Length ? b[p] << 8 | b[p + 1] : 0; long U32(int p) => p + 3 < b.Length ? (long)b[p] << 24 | (long)b[p + 1] << 16 | (long)b[p + 2] << 8 | b[p + 3] : 0;
        var tag = Encoding.ASCII.GetString(b, 0, 4);
        if (tag != "\0\u0001\0\0" && tag != "OTTO" && tag != "true") return new("rot", "Keine TrueType/OpenType-Schrift.", "", 0);
        int n = U16(4); int os2 = -1, name = -1;
        for (var i = 0; i < n; i++) { var r = 12 + i * 16; var tt = Encoding.ASCII.GetString(b, r, 4); var off = (int)U32(r + 8); if (tt == "OS/2") os2 = off; if (tt == "name") name = off; }
        var fsType = os2 >= 0 ? U16(os2 + 8) : 0;
        var license = "";
        if (name >= 0)
        {
            int count = U16(name + 2), strOff = name + U16(name + 4);
            for (var i = 0; i < count; i++)
            {
                var r = name + 6 + i * 12; int platform = U16(r), id = U16(r + 6), len = U16(r + 8), off = U16(r + 10);
                if (id is not (13 or 14) || strOff + off + len > b.Length) continue;
                var s = platform is 0 or 3 ? Encoding.BigEndianUnicode.GetString(b, strOff + off, len) : Encoding.Latin1.GetString(b, strOff + off, len);
                if (s.Length > license.Length) license = s;
            }
        }
        var lic = license.Trim();
        if ((fsType & 0x000F) == 0x0002) return new("rot", "Die Schrift verbietet das Einbetten (Restricted License) – nicht für Veröffentlichungen verwenden.", lic, fsType);
        if (System.Text.RegularExpressions.Regex.IsMatch(lic, "SIL Open Font|OFL|Apache License|Ubuntu Font Licence|public domain|CC0|Bitstream Vera|DejaVu", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return new("gruen", "Freie Schriftlizenz: " + (lic.Length > 80 ? lic[..80] + " …" : lic), lic, fsType);
        if (path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), StringComparison.OrdinalIgnoreCase) && fsType != 0x0002)
            return new("gruen", "Systemschrift – zum Rendern ins Video erlaubt (wird nicht weitergegeben).", lic, fsType);
        return new("gelb", "Keine Lizenzangabe in der Schrift – vor der Veröffentlichung Lizenz prüfen.", lic, fsType);
    }
}
