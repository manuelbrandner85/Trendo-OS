using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Wissen;

/// <summary>T-0788 (Stammbaum): eine Zeile einer GEDCOM-Datei mit ihren Unterzeilen. Fortsetzungen (CONT/CONC) sind beim Lesen schon zusammengesetzt –
/// CONT als Zeilenumbruch „\n“. Unbekannte Strukturen bleiben so als Baum erhalten und werden beim Schreiben unverändert wieder ausgegeben.</summary>
public sealed class GedNode
{
    public string Tag { get; set; } = "";
    /// <summary>Kennung mit @ wie in der Datei („@I1@“), nur bei Datensätzen der Ebene 0.</summary>
    public string? Xref { get; set; }
    public string Value { get; set; } = "";
    public List<GedNode> Kids { get; set; } = [];

    public GedNode() { }
    public GedNode(string tag, string value = "", string? xref = null) { Tag = tag; Value = value; Xref = xref; }

    public GedNode? Kid(string tag) => Kids.FirstOrDefault(k => k.Tag == tag);
    public bool IsPointer => Value.Length > 2 && Value[0] == '@' && Value[^1] == '@' && !Value.Contains(' ') && Value[1] != '#';
    public GedNode Copy() => new() { Tag = Tag, Xref = Xref, Value = Value, Kids = [.. Kids.Select(k => k.Copy())] };

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["t"] = Tag };
        if (Xref is { Length: > 0 }) o["x"] = Xref;
        if (Value.Length > 0) o["v"] = Value;
        if (Kids.Count > 0) o["k"] = new JsonArray([.. Kids.Select(k => (JsonNode)k.ToJson())]);
        return o;
    }

    public static GedNode FromJson(JsonNode? n, int depth = 0)
    {
        var g = new GedNode();
        if (n is not JsonObject o || depth > 60) return g;
        g.Tag = Gedcom.SafeTag(o["t"]?.GetValueKind() == System.Text.Json.JsonValueKind.String ? o["t"]!.GetValue<string>() : "");
        g.Xref = o["x"]?.GetValueKind() == System.Text.Json.JsonValueKind.String ? o["x"]!.GetValue<string>() : null;
        g.Value = o["v"]?.GetValueKind() == System.Text.Json.JsonValueKind.String ? o["v"]!.GetValue<string>() : "";
        if (o["k"] is JsonArray a) foreach (var k in a) g.Kids.Add(FromJson(k, depth + 1));
        return g;
    }

    public static JsonArray ListJson(IEnumerable<GedNode> nodes) => new([.. nodes.Select(n => (JsonNode)n.ToJson())]);
    public static List<GedNode> ListFrom(JsonNode? n) => n is JsonArray a ? [.. a.Select(x => FromJson(x))] : [];
}

/// <summary>Ergebnis des Einlesens: Datensätze, erkannte Zeichenkodierung (ehrlich benannt), Hinweise, Zahl nicht umrechenbarer Zeichen.</summary>
public sealed record GedRead(List<GedNode> Records, string Encoding, List<string> Problems, int BadChars);

/// <summary>T-0788: GEDCOM 5.5.1 lesen und schreiben (eigene Umsetzung nach der öffentlichen Beschreibung des Formats).
/// Lesen: UTF-8 (mit/ohne BOM), UTF-16, ANSEL (wird erkannt und für die gängigen Zeichen nach Unicode umgerechnet), ASCII, ANSI/Windows-1252, IBM-PC (850), Macintosh.
/// Schreiben: immer UTF-8, Zeilen höchstens 255 Byte, lange Werte mit CONC (nie an einem Leerzeichen getrennt), Zeilenumbrüche mit CONT.</summary>
public static partial class Gedcom
{
    public const int MaxBytes = 50_000_000;
    public const int MaxDepth = 60;

    [GeneratedRegex(@"^\s*(\d{1,2})\s+(?:(@[^@\s][^@]*@)\s+)?([A-Za-z0-9_]{1,31})(?: (.*))?$")]
    private static partial Regex LineRx();
    [GeneratedRegex(@"(?im)^\s*1\s+CHAR\s+([^\r\n]+?)\s*$")]
    private static partial Regex CharRx();

    public static string SafeTag(string t) => t.Length is > 0 and <= 31 && t.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ? t : "_UNBEKANNT";

    // ---------------- Zeichenkodierung ----------------

    static readonly Encoding Utf8Strict = new UTF8Encoding(false, true);

    /// <summary>Bytes in Text umwandeln. Rückgabe: Text, Beschreibung der Kodierung für die Person, Zahl der Zeichen, die nicht umgerechnet werden konnten.</summary>
    public static (string Text, string Encoding, int Bad) Decode(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Utf8(b[3..], "UTF-8");
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return (System.Text.Encoding.Unicode.GetString(b, 2, b.Length - 2), "UTF-16 (Unicode)", 0);
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return (System.Text.Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2), "UTF-16 (Unicode)", 0);
        if (b.Length >= 2 && b[0] == (byte)'0' && b[1] == 0) return (System.Text.Encoding.Unicode.GetString(b), "UTF-16 (Unicode)", 0);
        if (b.Length >= 2 && b[0] == 0 && b[1] == (byte)'0') return (System.Text.Encoding.BigEndianUnicode.GetString(b), "UTF-16 (Unicode)", 0);
        var head = System.Text.Encoding.Latin1.GetString(b, 0, Math.Min(b.Length, 8000));
        var cs = CharRx().Match(head) is { Success: true } m ? m.Groups[1].Value.Trim().ToUpperInvariant() : "";
        var high = b.Any(x => x >= 0x80);
        switch (cs)
        {
            case "ANSEL":
            {
                var t = DecodeAnsel(b, out var bad);
                return (t, bad > 0 ? $"ANSEL – erkannt und nach Unicode umgerechnet, {bad} Zeichen unbekannt (als „�“)" : "ANSEL – erkannt und nach Unicode umgerechnet", bad);
            }
            case "UTF-8" or "UTF8" or "UNICODE": return Utf8(b, cs == "UNICODE" ? "UNICODE (als UTF-8 gelesen)" : "UTF-8");
            case "ASCII" or "US-ASCII":
                if (!high) return (System.Text.Encoding.ASCII.GetString(b), "ASCII", 0);
                if (TryUtf8(b, out var u)) return (u, "ASCII angegeben, Datei enthält aber UTF-8 – als UTF-8 gelesen", 0);
                return (Cp(1252, b), "ASCII angegeben, Datei enthält aber Sonderzeichen – als Windows-1252 gelesen", 0);
            case "ANSI" or "WINDOWS-1252" or "CP1252" or "IBM WINDOWS" or "WINDOWS" or "ISO-8859-1" or "LATIN1":
                if (TryUtf8(b, out var u2) && high) return (u2, $"{cs} angegeben, Datei ist aber UTF-8 – als UTF-8 gelesen", 0);
                return (Cp(1252, b), cs + " (Windows-1252)", 0);
            case "IBMPC" or "IBM PC" or "IBM DOS" or "CP850" or "MSDOS": return (Cp(850, b), cs + " (DOS, Codepage 850)", 0);
            case "MACINTOSH" or "MAC": return (Cp(10000, b), "Macintosh", 0);
        }
        if (TryUtf8(b, out var u3)) return (u3, cs.Length > 0 ? $"„{cs}“ unbekannt – als UTF-8 gelesen" : "ohne Angabe – als UTF-8 gelesen", 0);
        return (Cp(1252, b), cs.Length > 0 ? $"„{cs}“ unbekannt – als Windows-1252 gelesen" : "ohne Angabe – als Windows-1252 gelesen", 0);
    }

    static (string, string, int) Utf8(byte[] b, string name)
    {
        if (TryUtf8(b, out var t)) return (t, name, 0);
        var lax = System.Text.Encoding.UTF8.GetString(b);
        var bad = lax.Count(c => c == '�');
        return (lax, $"{name} – {bad} ungültige Zeichen (als „�“)", bad);
    }

    static bool TryUtf8(byte[] b, out string text)
    {
        try { text = Utf8Strict.GetString(b); return true; }
        catch (DecoderFallbackException) { text = ""; return false; }
    }

    static string Cp(int page, byte[] b)
    {
        System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return System.Text.Encoding.GetEncoding(page).GetString(b);
    }

    /// <summary>ANSEL (ANSI Z39.47 mit den GEDCOM-Ergänzungen) → Unicode. Akzente stehen in ANSEL VOR dem Buchstaben, in Unicode danach;
    /// das Ergebnis wird zu fertigen Zeichen zusammengesetzt (NFC: „ü“ statt „u“ + Trema).</summary>
    public static string DecodeAnsel(byte[] b, out int bad)
    {
        bad = 0;
        var sb = new StringBuilder(b.Length); var pending = new StringBuilder();
        foreach (var x in b)
        {
            if (x < 0x80) { sb.Append((char)x); if (pending.Length > 0) { sb.Append(pending); pending.Clear(); } continue; }
            if (AnselCombining.TryGetValue(x, out var mark)) { pending.Append(mark); continue; }
            if (AnselSpacing.TryGetValue(x, out var ch)) sb.Append(ch); else { sb.Append('�'); bad++; }
            if (pending.Length > 0) { sb.Append(pending); pending.Clear(); }
        }
        if (pending.Length > 0) sb.Append(pending);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Für Tests und Rundreisen: Unicode → ANSEL (nur Zeichen, die die Tabelle kennt; sonst „?“).</summary>
    public static byte[] EncodeAnsel(string s)
    {
        var o = new List<byte>();
        var rev = AnselSpacing.Where(k => k.Key != 0xC7).ToDictionary(k => k.Value, k => k.Key);
        var revMark = AnselCombining.ToDictionary(k => k.Value, k => k.Key);
        foreach (var e in s.Normalize(NormalizationForm.FormC).EnumerateRunes())
        {
            var str = e.ToString();
            if (e.Value < 0x80) { o.Add((byte)e.Value); continue; }
            if (str.Length == 1 && rev.TryGetValue(str[0], out var sp)) { o.Add(sp); continue; }
            var dec = str.Normalize(NormalizationForm.FormD);
            if (dec.Length >= 2 && dec[0] < 0x80 && dec.Skip(1).All(revMark.ContainsKey)) { foreach (var m in dec.Skip(1)) o.Add(revMark[m]); o.Add((byte)dec[0]); continue; }
            o.Add((byte)'?');
        }
        return [.. o];
    }

    static readonly Dictionary<byte, char> AnselSpacing = new()
    {
        [0xA1] = 'Ł', [0xA2] = 'Ø', [0xA3] = 'Đ', [0xA4] = 'Þ', [0xA5] = 'Æ', [0xA6] = 'Œ', [0xA7] = 'ʹ', [0xA8] = '·', [0xA9] = '♭', [0xAA] = '®', [0xAB] = '±',
        [0xAC] = 'Ơ', [0xAD] = 'Ư', [0xAE] = 'ʼ', [0xB0] = 'ʻ', [0xB1] = 'ł', [0xB2] = 'ø', [0xB3] = 'đ', [0xB4] = 'þ', [0xB5] = 'æ', [0xB6] = 'œ', [0xB7] = 'ʺ',
        [0xB8] = 'ı', [0xB9] = '£', [0xBA] = 'ð', [0xBC] = 'ơ', [0xBD] = 'ư', [0xBE] = '□', [0xBF] = '■', [0xC0] = '°', [0xC1] = 'ℓ', [0xC2] = '℗', [0xC3] = '©',
        [0xC4] = '♯', [0xC5] = '¿', [0xC6] = '¡', [0xC7] = 'ß', [0xC8] = '€', [0xCD] = 'e', [0xCE] = 'o', [0xCF] = 'ß',
    };

    static readonly Dictionary<byte, char> AnselCombining = new()
    {
        [0xE0] = '̉', [0xE1] = '̀', [0xE2] = '́', [0xE3] = '̂', [0xE4] = '̃', [0xE5] = '̄', [0xE6] = '̆', [0xE7] = '̇',
        [0xE8] = '̈', [0xE9] = '̌', [0xEA] = '̊', [0xEB] = '︠', [0xEC] = '︡', [0xED] = '̕', [0xEE] = '̋', [0xEF] = '̐',
        [0xF0] = '̧', [0xF1] = '̨', [0xF2] = '̣', [0xF3] = '̤', [0xF4] = '̥', [0xF5] = '̳', [0xF6] = '̲', [0xF7] = '̦',
        [0xF8] = '̜', [0xF9] = '̮', [0xFA] = '︢', [0xFB] = '︣', [0xFE] = '̓',
    };

    // ---------------- Lesen ----------------

    public static GedRead Read(byte[] bytes)
    {
        if (bytes.Length > MaxBytes) throw new InvalidDataException($"Die GEDCOM-Datei ist zu groß (höchstens {MaxBytes / 1_000_000} MB).");
        var (text, enc, bad) = Decode(bytes);
        var (records, probs) = Parse(text);
        return new(records, enc, probs, bad);
    }

    /// <summary>Text in Datensätze zerlegen. Fehlerhafte Zeilen werden gemeldet (mit Zeilennummer) und übersprungen, nie geraten.</summary>
    public static (List<GedNode> Records, List<string> Problems) Parse(string text)
    {
        var records = new List<GedNode>(); var probs = new List<string>();
        var joined = new Dictionary<GedNode, StringBuilder>(ReferenceEqualityComparer.Instance); // T-0788 (Gegenprüfung G3): CONC/CONT linear zusammensetzen
        var stack = new List<GedNode>(); var lineNo = 0; var longLines = 0;
        foreach (var raw in text.Split(["\r\n", "\n\r", "\r", "\n"], StringSplitOptions.None))
        {
            lineNo++;
            var line = raw.TrimStart('﻿');
            if (line.Trim().Length == 0) continue;
            if (Encoding.UTF8.GetByteCount(line) > 255) longLines++;
            var m = LineRx().Match(line);
            if (!m.Success) { if (probs.Count < 200) probs.Add($"Zeile {lineNo} nicht lesbar: „{Cut(line, 60)}“"); continue; }
            var level = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var node = new GedNode(m.Groups[3].Value.ToUpperInvariant() is var tg && tg.StartsWith('_') ? m.Groups[3].Value : tg, m.Groups[4].Success ? m.Groups[4].Value : "", m.Groups[2].Success ? m.Groups[2].Value : null);
            if (level == 0) { records.Add(node); stack.Clear(); stack.Add(node); continue; }
            if (stack.Count == 0) { if (probs.Count < 200) probs.Add($"Zeile {lineNo}: Unterzeile ohne Datensatz – übersprungen"); continue; }
            if (level > stack.Count) { if (probs.Count < 200) probs.Add($"Zeile {lineNo}: Ebene {level} springt (erwartet höchstens {stack.Count}) – angehängt"); level = stack.Count; }
            if (level > MaxDepth) { if (probs.Count < 200) probs.Add($"Zeile {lineNo}: zu tief verschachtelt – übersprungen"); continue; }
            var parent = stack[level - 1];
            if (node.Tag is "CONC" or "CONT" && node.Xref is null)
            {
                if (!joined.TryGetValue(parent, out var sb)) joined[parent] = sb = new StringBuilder(parent.Value);
                if (node.Tag == "CONT") sb.Append('\n');
                sb.Append(node.Value);
                continue;
            }
            parent.Kids.Add(node);
            if (stack.Count > level) stack.RemoveRange(level, stack.Count - level);
            stack.Add(node);
        }
        if (longLines > 0) probs.Add($"{longLines} Zeile(n) länger als 255 Byte – trotzdem gelesen");
        if (records.Count == 0 || records[0].Tag != "HEAD") probs.Add("Kein Kopf (HEAD) am Anfang – vermutlich keine GEDCOM-Datei");
        if (records.Count > 0 && records[^1].Tag != "TRLR") probs.Add("Kein Abschluss (TRLR) – Datei womöglich unvollständig");
        foreach (var (n, sb) in joined) n.Value = sb.ToString();
        return (records, probs);
    }

    static string Cut(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    // ---------------- Schreiben ----------------

    public const int MaxChunkBytes = 200;

    /// <summary>Datensätze als GEDCOM-Text (Zeilenende CR LF).</summary>
    public static string Write(IEnumerable<GedNode> records)
    {
        var sb = new StringBuilder();
        foreach (var r in records) WriteNode(sb, r, 0);
        return sb.ToString();
    }

    static void WriteNode(StringBuilder sb, GedNode n, int level)
    {
        if (level > MaxDepth) return;
        var lines = n.Value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var first = Chunks(lines[0]);
        Line(sb, level, n.Xref, n.Tag, first[0]);
        foreach (var c in first.Skip(1)) Line(sb, level + 1, null, "CONC", c);
        foreach (var l in lines.Skip(1))
        {
            var ch = Chunks(l);
            Line(sb, level + 1, null, "CONT", ch[0]);
            foreach (var c in ch.Skip(1)) Line(sb, level + 1, null, "CONC", c);
        }
        foreach (var k in n.Kids) WriteNode(sb, k, level + 1);
    }

    static bool IsMark(char c) => char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.NonSpacingMark or System.Globalization.UnicodeCategory.SpacingCombiningMark or System.Globalization.UnicodeCategory.EnclosingMark;

    static void Line(StringBuilder sb, int level, string? xref, string tag, string value)
    {
        sb.Append(level.ToString(CultureInfo.InvariantCulture)).Append(' ');
        if (xref is { Length: > 0 }) sb.Append(xref).Append(' ');
        sb.Append(SafeTag(tag));
        if (value.Length > 0) sb.Append(' ').Append(value);
        sb.Append("\r\n");
    }

    /// <summary>Wert in Stücke von höchstens <see cref="MaxChunkBytes"/> Byte (UTF-8) teilen – nie so, dass ein Stück mit einem Leerzeichen endet oder beginnt
    /// (manche Programme verlieren dort das Leerzeichen).</summary>
    public static List<string> Chunks(string v)
    {
        var res = new List<string>();
        if (Encoding.UTF8.GetByteCount(v) <= MaxChunkBytes) { res.Add(v); return res; }
        var i = 0;
        while (i < v.Length)
        {
            var end = i; var bytes = 0;
            while (end < v.Length)
            {
                var len = char.IsHighSurrogate(v[end]) && end + 1 < v.Length ? 2 : 1;
                var b = Encoding.UTF8.GetByteCount(v.AsSpan(end, len));
                if (bytes + b > MaxChunkBytes) break;
                bytes += b; end += len;
            }
            if (end < v.Length)
            {
                var cut = end;
                while (cut > i + 1 && (v[cut - 1] == ' ' || v[cut] == ' ' || char.IsLowSurrogate(v[cut]) || IsMark(v[cut]))) cut--; // T-0788 G9: nie vor einem Kombinationszeichen trennen
                if (cut > i + 1) end = cut;
            }
            res.Add(v[i..end]); i = end;
        }
        return res;
    }
}
