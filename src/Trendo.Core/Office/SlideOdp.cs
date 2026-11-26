using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace Trendo.Core.Office;

/// <summary>T-0780: Präsentation als ODP (OpenDocument, LibreOffice Impress) schreiben – bisher konnte Trendo ODP nur lesen.
/// Abgedeckt: Foliengröße 16:9, Hintergrund des Designs, Textfelder mit Größe, Fett, Farbe, Ausrichtung und Aufzählung (zwei Ebenen über Tab),
/// Rechtecke und Ellipsen mit Füllung, Bilder (eingebettet) und Sprechernotizen. Tabellen, Diagramme, Medien, Animationen und Übergänge schreibt dieser Weg nicht.</summary>
public static partial class SlideFiles
{
    static string Cm(double v) => v.ToString("0.###", CultureInfo.InvariantCulture) + "cm";
    static string Ox(string s) => SecurityElement.Escape(s) ?? "";
    static string OdpColor(string? c, string fallback)
    {
        var h = (c ?? fallback).TrimStart('#');
        if (h.Length == 8) h = h[2..];
        return "#" + (h.Length == 6 ? h : fallback.TrimStart('#')).ToLowerInvariant();
    }

    public static void WriteOdp(SlideDoc doc, string path)
    {
        var th = doc.Theme;
        var auto = new StringBuilder(); var body = new StringBuilder(); var pics = new List<(string Name, byte[] Data)>();
        var styleNo = 0;
        string TextStyle(double size, bool bold, string color)
        {
            var n = "T" + ++styleNo;
            auto.Append($"<style:style style:name=\"{n}\" style:family=\"text\"><style:text-properties fo:font-size=\"{size.ToString("0.#", CultureInfo.InvariantCulture)}pt\" fo:color=\"{color}\" style:font-name=\"{Ox(th.Font)}\" fo:font-family=\"{Ox(th.Font)}\"{(bold ? " fo:font-weight=\"bold\"" : "")}/></style:style>");
            return n;
        }
        string ParaStyle(Align a)
        {
            var n = "P" + ++styleNo;
            var al = a switch { Align.Center => "center", Align.Right => "end", Align.Justify => "justify", _ => "start" };
            auto.Append($"<style:style style:name=\"{n}\" style:family=\"paragraph\"><style:paragraph-properties fo:text-align=\"{al}\" fo:margin-bottom=\"0.2cm\"/></style:style>");
            return n;
        }
        string Graphic(string? fill, bool textOnly)
        {
            var n = "gr" + ++styleNo;
            auto.Append($"<style:style style:name=\"{n}\" style:family=\"graphic\"><style:graphic-properties draw:stroke=\"none\" " +
                (textOnly || fill is null ? "draw:fill=\"none\"" : $"draw:fill=\"solid\" draw:fill-color=\"{OdpColor(fill, th.Accent)}\"") +
                " draw:textarea-vertical-align=\"top\" draw:auto-grow-height=\"false\" fo:padding-top=\"0.1cm\" fo:padding-bottom=\"0.1cm\" fo:padding-left=\"0.2cm\" fo:padding-right=\"0.2cm\" fo:wrap-option=\"wrap\"/></style:style>");
            return n;
        }
        // Aufzählung
        auto.Append("<style:style style:name=\"prNotes\" style:family=\"presentation\" style:parent-style-name=\"Standard-notes\"><style:graphic-properties draw:fill=\"none\"/></style:style>");
        auto.Append("<text:list-style style:name=\"L1\"><text:list-level-style-bullet text:level=\"1\" text:bullet-char=\"•\"><style:list-level-properties text:space-before=\"0.3cm\" text:min-label-width=\"0.6cm\"/></text:list-level-style-bullet>"
            + "<text:list-level-style-bullet text:level=\"2\" text:bullet-char=\"–\"><style:list-level-properties text:space-before=\"1.2cm\" text:min-label-width=\"0.6cm\"/></text:list-level-style-bullet></text:list-style>");
        var pageStyles = new Dictionary<string, string>();
        string PageStyle(string bg)
        {
            var c = OdpColor(bg, th.Background);
            if (pageStyles.TryGetValue(c, out var have)) return have;
            var n = "dp" + (pageStyles.Count + 1);
            auto.Append($"<style:style style:name=\"{n}\" style:family=\"drawing-page\"><style:drawing-page-properties draw:fill=\"solid\" draw:fill-color=\"{c}\" presentation:background-visible=\"true\" presentation:background-objects-visible=\"true\"/></style:style>");
            pageStyles[c] = n; return n;
        }
        var no = 0;
        foreach (var s in doc.Slides)
        {
            no++;
            body.Append($"<draw:page draw:name=\"Folie{no}\" draw:style-name=\"{PageStyle(s.Background ?? th.Background)}\" draw:master-page-name=\"Standard\">");
            var shapeNo = 0;
            foreach (var sh in s.Shapes)
            {
                shapeNo++;
                var geo = $"svg:x=\"{Cm(sh.X)}\" svg:y=\"{Cm(sh.Y)}\" svg:width=\"{Cm(Math.Max(0.1, sh.W))}\" svg:height=\"{Cm(Math.Max(0.1, sh.H))}\"";
                var isTitle = sh.Role == "title";
                var color = OdpColor(sh.Color, isTitle ? th.Title : th.Text);
                string Paras()
                {
                    var sb = new StringBuilder();
                    var ts = TextStyle(sh.FontSize, sh.Bold, color); var ps = ParaStyle(sh.Align);
                    var lines = sh.Text.Replace("\r\n", "\n").Split('\n');
                    if (sh.Bullets)
                    {
                        sb.Append("<text:list text:style-name=\"L1\">");
                        foreach (var raw in lines)
                        {
                            if (raw.Trim().Length == 0) continue;
                            var deep = raw.StartsWith('\t');
                            var item = $"<text:list-item><text:p text:style-name=\"{ps}\"><text:span text:style-name=\"{ts}\">{Ox(raw.Trim())}</text:span></text:p></text:list-item>";
                            sb.Append(deep ? $"<text:list-item><text:list>{item}</text:list></text:list-item>" : item);
                        }
                        sb.Append("</text:list>");
                    }
                    else foreach (var raw in lines) sb.Append($"<text:p text:style-name=\"{ps}\"><text:span text:style-name=\"{ts}\">{Ox(raw)}</text:span></text:p>");
                    return sb.ToString();
                }
                switch (sh.Kind)
                {
                    case ShapeKind.Text:
                    {
                        var cls = sh.Role switch { "title" => " presentation:class=\"title\"", "sub" => " presentation:class=\"subtitle\"", "body" or "body2" => " presentation:class=\"outline\"", _ => "" };
                        body.Append($"<draw:frame draw:name=\"{Ox(RoleLabel(sh.Role, shapeNo))}\" draw:style-name=\"{Graphic(sh.Fill, sh.Fill is null)}\" {geo}{cls} presentation:user-transformed=\"true\"><draw:text-box>{Paras()}</draw:text-box></draw:frame>");
                        break;
                    }
                    case ShapeKind.Rechteck or ShapeKind.Ellipse or ShapeKind.Pfeil:
                    {
                        var el = sh.Kind == ShapeKind.Ellipse ? "draw:ellipse" : "draw:rect";
                        body.Append($"<{el} draw:name=\"Form{shapeNo}\" draw:style-name=\"{Graphic(sh.Fill ?? th.Accent, false)}\" {geo}>{(sh.Text.Length > 0 ? Paras() : "")}</{el}>");
                        break;
                    }
                    case ShapeKind.Bild when sh.Image is { Length: > 0 } img:
                    {
                        var ext = img.Length > 3 && img[0] == 0xFF && img[1] == 0xD8 ? "jpg" : "png";
                        var name = $"Pictures/bild{pics.Count + 1}.{ext}"; pics.Add((name, img));
                        body.Append($"<draw:frame draw:name=\"Bild{shapeNo}\" draw:style-name=\"{Graphic(null, true)}\" {geo}><draw:image xlink:href=\"{name}\" xlink:type=\"simple\" xlink:show=\"embed\" xlink:actuate=\"onLoad\"/></draw:frame>");
                        break;
                    }
                    default:
                        if (sh.Text.Trim().Length > 0) body.Append($"<draw:frame draw:name=\"Text{shapeNo}\" draw:style-name=\"{Graphic(null, true)}\" {geo}><draw:text-box>{Paras()}</draw:text-box></draw:frame>");
                        break;
                }
            }
            if (s.Notes.Trim().Length > 0)
            {
                body.Append($"<presentation:notes><draw:page-thumbnail draw:layer=\"layout\" svg:width=\"16.932cm\" svg:height=\"9.524cm\" svg:x=\"1.058cm\" svg:y=\"1.905cm\" draw:page-number=\"{no}\" presentation:class=\"page\"/><draw:frame draw:name=\"Notizen\" draw:layer=\"layout\" presentation:class=\"notes\" presentation:style-name=\"prNotes\" svg:x=\"1.905cm\" svg:y=\"12.065cm\" svg:width=\"15.239cm\" svg:height=\"11.429cm\"><draw:text-box>");
                foreach (var line in s.Notes.Replace("\r\n", "\n").Split('\n')) body.Append($"<text:p>{Ox(line)}</text:p>");
                body.Append("</draw:text-box></draw:frame></presentation:notes>");
            }
            body.Append("</draw:page>");
        }
        const string Ns = "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" office:version=\"1.3\"";
        var content = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-content {Ns}><office:automatic-styles>{auto}</office:automatic-styles><office:body><office:presentation>{body}</office:presentation></office:body></office:document-content>";
        var styles = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-styles {Ns}><office:styles>"
            + $"<style:default-style style:family=\"graphic\"><style:text-properties style:font-name=\"{Ox(th.Font)}\" fo:font-family=\"{Ox(th.Font)}\" fo:font-size=\"18pt\" fo:language=\"de\" fo:country=\"DE\"/></style:default-style>"
            + "<style:style style:name=\"Standard-notes\" style:family=\"presentation\"><style:graphic-properties draw:stroke=\"none\" draw:fill=\"none\"/><style:text-properties fo:font-size=\"12pt\"/></style:style>"
            + "</office:styles><office:automatic-styles>"
            + $"<style:page-layout style:name=\"PM1\"><style:page-layout-properties fo:margin-top=\"0cm\" fo:margin-bottom=\"0cm\" fo:margin-left=\"0cm\" fo:margin-right=\"0cm\" fo:page-width=\"{Cm(SlideDoc.WidthCm)}\" fo:page-height=\"{Cm(SlideDoc.HeightCm)}\" style:print-orientation=\"landscape\"/></style:page-layout>"
            + $"<style:page-layout style:name=\"PMN\"><style:page-layout-properties fo:margin-top=\"0cm\" fo:margin-bottom=\"0cm\" fo:margin-left=\"0cm\" fo:margin-right=\"0cm\" fo:page-width=\"21cm\" fo:page-height=\"29.7cm\" style:print-orientation=\"portrait\"/></style:page-layout><style:style style:name=\"Mdp1\" style:family=\"drawing-page\"><style:drawing-page-properties draw:fill=\"solid\" draw:fill-color=\"{OdpColor(th.Background, "#FFFFFF")}\"/></style:style>"
            + "</office:automatic-styles><office:master-styles><style:master-page style:name=\"Standard\" style:page-layout-name=\"PM1\" draw:style-name=\"Mdp1\"><presentation:notes style:page-layout-name=\"PMN\"><draw:page-thumbnail draw:layer=\"layout\" svg:width=\"16.932cm\" svg:height=\"9.524cm\" svg:x=\"1.058cm\" svg:y=\"1.905cm\" presentation:class=\"page\"/><draw:frame draw:layer=\"layout\" presentation:style-name=\"Standard-notes\" svg:width=\"15.239cm\" svg:height=\"11.429cm\" svg:x=\"1.905cm\" svg:y=\"12.065cm\" presentation:class=\"notes\" presentation:placeholder=\"true\"><draw:text-box/></draw:frame></presentation:notes></style:master-page></office:master-styles></office:document-styles>";
        var meta = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-meta {Ns}><office:meta><dc:title>{Ox(doc.Slides.FirstOrDefault()?.Title ?? "")}</dc:title><meta:generator>Trendo OS</meta:generator></office:meta></office:document-meta>";
        var manifest = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.3\"><manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.presentation\"/><manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/><manifest:file-entry manifest:full-path=\"styles.xml\" manifest:media-type=\"text/xml\"/><manifest:file-entry manifest:full-path=\"meta.xml\" manifest:media-type=\"text/xml\"/>");
        foreach (var (n, data) in pics) manifest.Append($"<manifest:file-entry manifest:full-path=\"{n}\" manifest:media-type=\"{(n.EndsWith(".jpg", StringComparison.Ordinal) ? "image/jpeg" : "image/png")}\"/>");
        manifest.Append("</manifest:manifest>");
        var tmp = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using (var z = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            var mt = z.CreateEntry("mimetype", CompressionLevel.NoCompression); // muss zuerst und unkomprimiert stehen
            using (var w = new StreamWriter(mt.Open(), new UTF8Encoding(false))) w.Write("application/vnd.oasis.opendocument.presentation");
            void Add(string name, string text) { using var w = new StreamWriter(z.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false)); w.Write(text); }
            Add("content.xml", content); Add("styles.xml", styles); Add("meta.xml", meta); Add("META-INF/manifest.xml", manifest.ToString());
            foreach (var (n, data) in pics) { using var st = z.CreateEntry(n).Open(); st.Write(data); }
        }
        SafeFile.Move(tmp, path);
    }

    static string RoleLabel(string? role, int no) => role switch { "title" => "Titel", "sub" => "Untertitel", "body" => "Inhalt", "body2" => "Inhalt 2", _ => "Textfeld " + no };
}
