using System.Globalization;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace Trendo.Core.Office;

/// <summary>T-0701: Zellaussehen in XLSX lesen und schreiben – Schrift (fett, kursiv, Unterstreichung, durchgestrichen, Farbe, Größe, Schriftart),
/// Füllfarbe, Rahmen je Seite mit Linienart und Farbe, Ausrichtung, Umbruch, Einzug, Drehung, Zahlenformat-Codes.
/// Farben aus dem Design der Mappe (theme + tint) und der alten Excel-Palette (indexed) werden in feste Farben umgerechnet – sie sehen danach gleich aus.</summary>
static class SheetStyles
{
    static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>Die Standardpalette von Excel (indexed 0–63).</summary>
    static readonly string[] Palette =
    [
        "000000", "FFFFFF", "FF0000", "00FF00", "0000FF", "FFFF00", "FF00FF", "00FFFF", "000000", "FFFFFF", "FF0000", "00FF00", "0000FF", "FFFF00", "FF00FF", "00FFFF",
        "800000", "008000", "000080", "808000", "800080", "008080", "C0C0C0", "808080", "9999FF", "993366", "FFFFCC", "CCFFFF", "660066", "FF8080", "0066CC", "CCCCFF",
        "000080", "FF00FF", "FFFF00", "00FFFF", "800080", "800000", "008080", "0000FF", "00CCFF", "CCFFFF", "CCFFCC", "FFFF99", "99CCFF", "FF99CC", "CC99FF", "FFCC99",
        "3366FF", "33CCCC", "99CC00", "FFCC00", "FF9900", "FF6600", "666699", "969696", "003366", "339966", "003300", "333300", "993300", "993366", "333399", "333333",
    ];

    /// <summary>Office-Standarddesign, wenn die Mappe keines mitbringt (Reihenfolge wie Excels theme-Nummern: lt1, dk1, lt2, dk2, accent1–6, hlink, folHlink).</summary>
    static readonly string[] OfficeTheme = ["FFFFFF", "000000", "E7E6E6", "44546A", "4472C4", "ED7D31", "A5A5A5", "FFC000", "5B9BD5", "70AD47", "0563C1", "954F72"];

    /// <summary>Farben des Designs in Excels Nummerierung (0 = lt1, 1 = dk1, 2 = lt2, 3 = dk2, 4–9 = accent1–6, 10 = hlink, 11 = folHlink).</summary>
    public static string[] ThemeColors(XDocument? theme)
    {
        var scheme = theme?.Root?.Descendants(A + "clrScheme").FirstOrDefault();
        if (scheme is null) return OfficeTheme;
        string? C(string n)
        {
            var e = scheme.Element(A + n)?.Elements().FirstOrDefault();
            var v = (string?)e?.Attribute("lastClr") ?? (string?)e?.Attribute("val");
            return v is { Length: 6 } ? v.ToUpperInvariant() : null;
        }
        var order = new[] { "lt1", "dk1", "lt2", "dk2", "accent1", "accent2", "accent3", "accent4", "accent5", "accent6", "hlink", "folHlink" };
        return order.Select((n, i) => C(n) ?? OfficeTheme[i]).ToArray();
    }

    /// <summary>Farbe eines &lt;color&gt;/&lt;fgColor&gt;-Elements als #RRGGBB; null bei „automatisch“ oder unbekannt.</summary>
    public static string? Color(XElement? c, string[] theme)
    {
        if (c is null || (string?)c.Attribute("auto") is "1" or "true") return null;
        string? hex = null;
        if ((string?)c.Attribute("rgb") is { Length: >= 6 } rgb) hex = rgb[^6..].ToUpperInvariant();
        else if (int.TryParse((string?)c.Attribute("theme"), out var t) && t >= 0 && t < theme.Length) hex = theme[t];
        else if (int.TryParse((string?)c.Attribute("indexed"), out var ix) && ix >= 0 && ix < Palette.Length) hex = Palette[ix];
        if (hex is null) return null;
        if (double.TryParse((string?)c.Attribute("tint"), NumberStyles.Float, CultureInfo.InvariantCulture, out var tint) && tint != 0) hex = Tint(hex, tint);
        return "#" + hex;
    }

    /// <summary>Aufhellen/Abdunkeln wie Excel: Helligkeit im HSL-Farbraum.</summary>
    static string Tint(string hex, double tint)
    {
        double r = Convert.ToInt32(hex[..2], 16) / 255.0, g = Convert.ToInt32(hex[2..4], 16) / 255.0, b = Convert.ToInt32(hex[4..6], 16) / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), l = (max + min) / 2, h = 0, s = 0;
        if (max != min)
        {
            var d = max - min; s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4; h /= 6;
        }
        l = tint < 0 ? l * (1 + tint) : l * (1 - tint) + tint;
        double Hue(double p, double q, double t) { if (t < 0) t += 1; if (t > 1) t -= 1; return t < 1 / 6.0 ? p + (q - p) * 6 * t : t < 0.5 ? q : t < 2 / 3.0 ? p + (q - p) * (2 / 3.0 - t) * 6 : p; }
        if (s == 0) r = g = b = l;
        else { var q = l < 0.5 ? l * (1 + s) : l + s - l * s; var p = 2 * l - q; r = Hue(p, q, h + 1 / 3.0); g = Hue(p, q, h); b = Hue(p, q, h - 1 / 3.0); }
        static string X(double v) => ((int)Math.Round(Math.Clamp(v, 0, 1) * 255)).ToString("X2", CultureInfo.InvariantCulture);
        return X(r) + X(g) + X(b);
    }

    static bool On(XElement? e) => e is not null && (string?)e.Attribute("val") is null or "1" or "true";

    /// <summary>Aussehen eines Zellformats (cellXfs/xf), wie Trendo es in der Zelle ablegt.</summary>
    public sealed record Look(NumFmt Fmt, string? Code, bool Bold, bool Italic, string? Underline, bool Strike, string? FontColor, double? FontSize, string? FontName,
        string? Fill, bool Border, CellBorders? Borders, string? HAlign, string? VAlign, bool Wrap, int Indent, int Rotation, bool Shrink, bool Unlocked)
    {
        public static readonly Look None = new(NumFmt.Standard, null, false, false, null, false, null, null, null, null, false, null, null, null, false, 0, 0, false, false);
        public bool Any => this != None;
        public void Apply(Cell c)
        {
            c.Format = Fmt; c.NumCode = Code; c.Bold = Bold; c.Italic = Italic; c.Underline = Underline; c.Strike = Strike; c.FontColor = FontColor; c.FontSize = FontSize; c.FontName = FontName;
            c.Fill = Fill; c.Border = Border; c.Borders = Borders; c.HAlign = HAlign; c.VAlign = VAlign; c.Wrap = Wrap; c.Indent = Indent; c.Rotation = Rotation; c.Shrink = Shrink; c.Unlocked = Unlocked;
        }
    }

    /// <summary>Alle Zellformate einer styles.xml lesen; die erste Schrift wird Grundschrift der Mappe.</summary>
    public static List<Look> ReadLooks(XDocument? styles, string[] theme, SheetDoc doc, Func<int, string?, (NumFmt, string?)> numFmt)
    {
        var root = styles?.Root; if (root is null) return [];
        var codes = new Dictionary<int, string?>();
        foreach (var n in root.Element(S + "numFmts")?.Elements(S + "numFmt") ?? []) if (int.TryParse((string?)n.Attribute("numFmtId"), out var id)) codes[id] = (string?)n.Attribute("formatCode");
        var fontEls = root.Element(S + "fonts")?.Elements(S + "font").ToList() ?? [];
        static double? Sz(XElement f) => double.TryParse((string?)f.Element(S + "sz")?.Attribute("val"), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        if (fontEls.Count > 0)
        {
            if (Sz(fontEls[0]) is { } s0) doc.FontSize = s0;
            if ((string?)fontEls[0].Element(S + "name")?.Attribute("val") is { Length: > 0 } n0) doc.FontName = n0;
        }
        var fonts = fontEls.Select(f =>
        {
            var u = f.Element(S + "u"); var uv = u is null ? null : (string?)u.Attribute("val") ?? "single";
            var col = Color(f.Element(S + "color"), theme);
            var size = Sz(f); var name = (string?)f.Element(S + "name")?.Attribute("val");
            return (Bold: On(f.Element(S + "b")), Italic: On(f.Element(S + "i")), Underline: uv is "none" ? null : uv, Strike: On(f.Element(S + "strike")),
                Color: col is "#000000" ? null : col, Size: size is { } sz && sz != doc.FontSize ? size : null, Name: name is { Length: > 0 } && name != doc.FontName ? name : null);
        }).ToList();
        var fills = root.Element(S + "fills")?.Elements(S + "fill").Select(f => f.Element(S + "patternFill") is { } p && (string?)p.Attribute("patternType") == "solid"
            ? Color(p.Element(S + "fgColor"), theme) ?? Color(p.Element(S + "bgColor"), theme) : null).ToList() ?? [];
        var borders = root.Element(S + "borders")?.Elements(S + "border").Select(b =>
        {
            BorderLine? Side(string n, string alt)
            {
                var e = b.Element(S + n) ?? b.Element(S + alt);
                return (string?)e?.Attribute("style") is { Length: > 0 } st && st != "none" ? new BorderLine(st, Color(e!.Element(S + "color"), theme) is { } c && c != "#000000" ? c : null) : null;
            }
            var cb = new CellBorders(Side("left", "start"), Side("right", "end"), Side("top", "top"), Side("bottom", "bottom"));
            var thinAll = new[] { cb.Left, cb.Right, cb.Top, cb.Bottom }.All(x => x is { Style: "thin", Color: null });
            return (Border: thinAll, Borders: thinAll || !cb.Any ? null : cb);
        }).ToList() ?? [];
        return root.Element(S + "cellXfs")?.Elements(S + "xf").Select(x =>
        {
            int I(string a) => int.TryParse((string?)x.Attribute(a), out var v) ? v : 0;
            var nf = I("numFmtId"); var (fmt, code) = numFmt(nf, codes.GetValueOrDefault(nf));
            var f = fonts.ElementAtOrDefault(I("fontId")); var bd = borders.ElementAtOrDefault(I("borderId"));
            var al = x.Element(S + "alignment");
            string? H(string a, params string[] dflt) => (string?)al?.Attribute(a) is { Length: > 0 } v && !dflt.Contains(v) ? v : null;
            int N(string a) => int.TryParse((string?)al?.Attribute(a), out var v) ? v : 0;
            bool B(string a) => (string?)al?.Attribute(a) is "1" or "true";
            return new Look(fmt, code, f.Bold, f.Italic, f.Underline, f.Strike, f.Color, f.Size, f.Name, fills.ElementAtOrDefault(I("fillId")), bd.Border, bd.Borders,
                H("horizontal", "general"), H("vertical", "bottom"), B("wrapText"), N("indent"), N("textRotation"), B("shrinkToFit"),
                (string?)x.Element(S + "protection")?.Attribute("locked") is "0" or "false");
        }).ToList() ?? [];
    }

    // ---------------- Schreiben ----------------
    public readonly record struct XfKey(NumFmt Fmt, string? Code, int Font, string? Fill, int Border, string? HAlign, string? VAlign, bool Wrap, int Indent, int Rotation, bool Shrink, bool Unlocked);
    readonly record struct FontKey(bool Bold, bool Italic, string? Underline, bool Strike, string? Color, double? Size, string? Name);

    /// <summary>Sammelt beim Schreiben Schriften, Füllungen, Rahmen und Zahlenformate und vergibt die Nummern.</summary>
    public sealed class Writer(SheetDoc doc)
    {
        readonly List<FontKey> _fonts = [new(false, false, null, false, null, null, null), new(true, false, null, false, null, null, null)];
        readonly List<string> _fills = [];
        readonly List<CellBorders?> _borders = [null, new(new("thin"), new("thin"), new("thin"), new("thin"))];
        readonly List<XfKey> _xfs = [new(NumFmt.Standard, null, 0, null, 0, null, null, false, 0, 0, false, false)];
        readonly List<string> _codes = [];

        int Font(Cell c)
        {
            var k = new FontKey(c.Bold, c.Italic, c.Underline, c.Strike, c.FontColor?.ToUpperInvariant(), c.FontSize, c.FontName);
            var i = _fonts.IndexOf(k); if (i >= 0) return i; _fonts.Add(k); return _fonts.Count - 1;
        }

        int Border(Cell c)
        {
            if (c.Borders is not { Any: true } b) return c.Border ? 1 : 0;
            var i = _borders.IndexOf(b); if (i >= 0) return i; _borders.Add(b); return _borders.Count - 1;
        }

        /// <summary>Nummer des Zellformats (s="…") für eine Zelle.</summary>
        public int Style(Cell c)
        {
            if (!c.HasLook) return 0;
            var fill = c.Fill?.ToUpperInvariant();
            if (fill is not null && !_fills.Contains(fill)) _fills.Add(fill);
            if (c.NumCode is { } code && SheetNumberFormat.BuiltinId(code) is null && !_codes.Contains(code)) _codes.Add(code);
            var k = new XfKey(c.Format, c.NumCode, Font(c), fill, Border(c), c.HAlign, c.VAlign, c.Wrap, c.Indent, c.Rotation, c.Shrink, c.Unlocked);
            var i = _xfs.IndexOf(k); if (i >= 0) return i; _xfs.Add(k); return _xfs.Count - 1;
        }

        /// <summary>Eigene Codes ab 170 – 164 bis 166 belegen Trendos Euro- und Datumsformate.</summary>
        int NumId(XfKey k) => k.Code is { } code ? SheetNumberFormat.BuiltinId(code) ?? 170 + _codes.IndexOf(code) : NumFmtIdOf(k.Fmt);

        static int NumFmtIdOf(NumFmt f) => f switch
        {
            NumFmt.Ganzzahl => 3, NumFmt.Zahl => 4, NumFmt.Prozent => 10, NumFmt.Text => 49, NumFmt.Euro => 164, NumFmt.Datum => 165, NumFmt.DatumZeit => 166, _ => 0,
        };

        public static string TrendoCode(NumFmt f) => f switch
        {
            NumFmt.Euro => "#,##0.00\\ \"€\"", NumFmt.Datum => "DD.MM.YYYY", NumFmt.DatumZeit => "DD.MM.YYYY HH:MM", _ => "",
        };

        static string Inv(double d) => d.ToString("R", CultureInfo.InvariantCulture);
        static string Col(string? hex) => hex is null ? "<color auto=\"1\"/>" : $"<color rgb=\"FF{hex.TrimStart('#').ToUpperInvariant()}\"/>";

        public string StylesXml(List<string> dxfFills)
        {
            var st = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"{S}\">");
            var own = _xfs.Where(k => k.Code is null).Select(k => k.Fmt).Distinct().Where(f => NumFmtIdOf(f) >= 164).ToList();
            var count = own.Count + _codes.Count;
            if (count > 0)
            {
                st.Append($"<numFmts count=\"{count}\">");
                foreach (var f in own) st.Append($"<numFmt numFmtId=\"{NumFmtIdOf(f)}\" formatCode=\"{SecurityElement.Escape(TrendoCode(f))}\"/>");
                for (var i = 0; i < _codes.Count; i++) st.Append($"<numFmt numFmtId=\"{170 + i}\" formatCode=\"{SecurityElement.Escape(_codes[i])}\"/>");
                st.Append("</numFmts>");
            }
            st.Append($"<fonts count=\"{_fonts.Count}\">");
            foreach (var f in _fonts)
                st.Append("<font>").Append(f.Bold ? "<b/>" : "").Append(f.Italic ? "<i/>" : "").Append(f.Strike ? "<strike/>" : "")
                  .Append(f.Underline is { } u ? (u == "single" ? "<u/>" : $"<u val=\"{SecurityElement.Escape(u)}\"/>") : "")
                  .Append($"<sz val=\"{Inv(f.Size ?? doc.FontSize)}\"/>").Append(f.Color is { } c ? Col(c) : "")
                  .Append($"<name val=\"{SecurityElement.Escape(f.Name ?? doc.FontName)}\"/></font>");
            st.Append("</fonts>");
            st.Append($"<fills count=\"{2 + _fills.Count}\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>");
            foreach (var f in _fills) st.Append($"<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF{f.TrimStart('#')[^6..]}\"/><bgColor indexed=\"64\"/></patternFill></fill>");
            st.Append($"</fills><borders count=\"{_borders.Count}\">");
            foreach (var b in _borders)
            {
                string Side(string n, BorderLine? l) => l is null ? $"<{n}/>" : $"<{n} style=\"{SecurityElement.Escape(l.Style)}\">{Col(l.Color)}</{n}>";
                st.Append("<border>").Append(Side("left", b?.Left)).Append(Side("right", b?.Right)).Append(Side("top", b?.Top)).Append(Side("bottom", b?.Bottom)).Append("<diagonal/></border>");
            }
            st.Append("</borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
            st.Append($"<cellXfs count=\"{_xfs.Count}\">");
            foreach (var k in _xfs)
            {
                var num = NumId(k); var fillId = k.Fill is null ? 0 : 2 + _fills.IndexOf(k.Fill);
                var align = k.HAlign is not null || k.VAlign is not null || k.Wrap || k.Indent != 0 || k.Rotation != 0 || k.Shrink;
                st.Append($"<xf numFmtId=\"{num}\" fontId=\"{k.Font}\" fillId=\"{fillId}\" borderId=\"{k.Border}\" xfId=\"0\"{(num != 0 ? " applyNumberFormat=\"1\"" : "")}{(k.Font != 0 ? " applyFont=\"1\"" : "")}{(fillId != 0 ? " applyFill=\"1\"" : "")}{(k.Border != 0 ? " applyBorder=\"1\"" : "")}{(align ? " applyAlignment=\"1\"" : "")}{(k.Unlocked ? " applyProtection=\"1\"" : "")}");
                if (!align && !k.Unlocked) { st.Append("/>"); continue; }
                st.Append('>');
                if (align)
                    st.Append("<alignment").Append(k.HAlign is { } h ? $" horizontal=\"{SecurityElement.Escape(h)}\"" : "").Append(k.VAlign is { } v ? $" vertical=\"{SecurityElement.Escape(v)}\"" : "")
                      .Append(k.Rotation != 0 ? $" textRotation=\"{k.Rotation}\"" : "").Append(k.Wrap ? " wrapText=\"1\"" : "").Append(k.Indent != 0 ? $" indent=\"{k.Indent}\"" : "").Append(k.Shrink ? " shrinkToFit=\"1\"" : "").Append("/>");
                if (k.Unlocked) st.Append("<protection locked=\"0\"/>");
                st.Append("</xf>");
            }
            st.Append("</cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
            if (dxfFills.Count > 0) st.Append($"<dxfs count=\"{dxfFills.Count}\">{string.Concat(dxfFills.Select(f => $"<dxf><fill><patternFill><bgColor rgb=\"{(f.TrimStart('#').Length == 6 ? "FF" : "")}{f.TrimStart('#')}\"/></patternFill></fill></dxf>"))}</dxfs>");
            st.Append("</styleSheet>");
            return st.ToString();
        }
    }
}
