using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>T-0689: MIME lesen und schreiben für das Mailprogramm – auf Bytes, damit Anhänge bytegleich bleiben und fremde Zeichensätze
/// (ISO-8859-1, Windows-1252 …) richtig ankommen. Schreiben: Text, Text + HTML, Anhänge, kodierte Betreffzeilen (RFC 2047, gefaltet),
/// Dateinamen nach RFC 2231 mit ASCII-Ausweichname. Kein Netz, keine Fremdbibliothek.</summary>
public static partial class MailMime
{
    static readonly Encoding Latin1 = Encoding.Latin1;
    static readonly UTF8Encoding StrictUtf8 = new(false, true);

    static MailMime() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    // ---------------------------------------------------------------- Schreiben

    /// <summary>Art einer Datei nach der Endung (für Anhänge).</summary>
    public static string ContentTypeOf(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf", ".txt" => "text/plain", ".csv" => "text/csv", ".htm" or ".html" => "text/html",
        ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp", ".svg" => "image/svg+xml",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".odt" => "application/vnd.oasis.opendocument.text", ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
        ".zip" => "application/zip", ".ics" => "text/calendar", ".vcf" => "text/vcard", ".eml" => "message/rfc822",
        ".mp3" => "audio/mpeg", ".wav" => "audio/wav", ".mp4" => "video/mp4", ".json" => "application/json", ".xml" => "application/xml",
        _ => "application/octet-stream",
    };

    static bool Ascii(string s) => s.All(c => c is >= ' ' and < (char)127);

    /// <summary>Kopfzeilen-Wert kodieren (RFC 2047): reines ASCII bleibt, sonst Base64-Wörter mit höchstens 75 Zeichen, mit „CRLF Leerzeichen“ gefaltet.
    /// Zeichen werden nie mitten in einer UTF-8-Folge getrennt.</summary>
    public static string EncodeWord(string s, int firstLineUsed = 0)
    {
        s = Regex.Replace(s, @"[\r\n]+", " ");
        if (Ascii(s)) return FoldAscii(s, firstLineUsed);
        var words = new List<string>(); var sb = new StringBuilder();
        const int room = 39; // 39 Bytes → 52 Base64-Zeichen + 12 Rahmen = 64, dazu „Subject: “
        var e = StringInfo.GetTextElementEnumerator(s);
        var bytes = 0;
        while (e.MoveNext())
        {
            var el = (string)e.Current; var n = Encoding.UTF8.GetByteCount(el);
            if (bytes + n > room && sb.Length > 0) { words.Add(sb.ToString()); sb.Clear(); bytes = 0; }
            sb.Append(el); bytes += n;
        }
        if (sb.Length > 0) words.Add(sb.ToString());
        return string.Join("\r\n ", words.Select(w => $"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(w))}?="));
    }

    static string FoldAscii(string s, int used)
    {
        if (used + s.Length <= 76) return s;
        var sb = new StringBuilder(); var line = used;
        foreach (var word in s.Split(' '))
        {
            if (line > 0 && line + word.Length + 1 > 76 && sb.Length > 0) { sb.Append("\r\n "); line = 1; }
            else if (sb.Length > 0) { sb.Append(' '); line++; }
            sb.Append(word); line += word.Length;
        }
        return sb.ToString();
    }

    /// <summary>Adressliste für die Kopfzeile: Namen mit Umlauten werden kodiert, die Adresse selbst bleibt.</summary>
    public static string AddressList(string list)
    {
        var parts = SplitAddresses(list).Select(a =>
        {
            var m = Regex.Match(a, @"^\s*""?([^""<]*?)""?\s*<([^>]+)>\s*$");
            if (!m.Success) return a.Trim();
            var name = m.Groups[1].Value.Trim(); var adr = m.Groups[2].Value.Trim();
            if (name.Length == 0) return adr;
            return (Ascii(name) ? (Regex.IsMatch(name, @"[,;:.()\[\]@\\]") ? "\"" + name.Replace("\\", "").Replace("\"", "") + "\"" : name) : EncodeWord(name)) + " <" + adr + ">";
        });
        return string.Join(",\r\n ", parts);
    }

    /// <summary>Adressen trennen (Komma oder Semikolon), Anführungszeichen und spitze Klammern beachten.</summary>
    public static List<string> SplitAddresses(string list)
    {
        var res = new List<string>(); var sb = new StringBuilder(); var quote = false; var angle = false;
        foreach (var c in list ?? "")
        {
            if (c == '"') quote = !quote; else if (c == '<' && !quote) angle = true; else if (c == '>' && !quote) angle = false;
            if ((c is ',' or ';') && !quote && !angle) { if (sb.ToString().Trim().Length > 0) res.Add(sb.ToString().Trim()); sb.Clear(); }
            else sb.Append(c);
        }
        if (sb.ToString().Trim().Length > 0) res.Add(sb.ToString().Trim());
        return res;
    }

    /// <summary>Nur die Adresse aus „Name &lt;adresse&gt;“.</summary>
    public static string AddressOnly(string a) => Regex.Match(a, @"<([^>]+)>") is { Success: true } m ? m.Groups[1].Value.Trim() : a.Trim();

    static string Rfc5322Date(DateTimeOffset now)
    {
        var s = now.ToString("ddd, dd MMM yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture);
        return s.Remove(s.Length - 3, 1);
    }

    static void Base64(StringBuilder sb, byte[] data)
    {
        var b64 = Convert.ToBase64String(data);
        for (var i = 0; i < b64.Length; i += 76) sb.Append(b64, i, Math.Min(76, b64.Length - i)).Append("\r\n");
    }

    static string Crlf(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

    static void TextPart(StringBuilder sb, string type, string text)
    {
        sb.Append($"Content-Type: {type}; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\n");
        Base64(sb, Encoding.UTF8.GetBytes(Crlf(text)));
    }

    /// <summary>Dateiname für die Kopfzeilen: reines ASCII bleibt; sonst nur die RFC-2231-Angabe „filename*“ (UTF-8, bei langen Namen in Stücken)
    /// und in „name=“ ein kodiertes Wort für ältere Programme. Kein verlustbehafteter ASCII-Ausweichname – manche Leser nehmen sonst den
    /// (gefunden mit dem Python-Standardparser, Beleg reports/T-0689).</summary>
    static (string TypeName, string Disposition) FileParams(string name)
    {
        name = name.Replace("\r", "").Replace("\n", "").Replace("\"", "'");
        if (Ascii(name)) return ($"name=\"{name}\"", $"filename=\"{name}\"");
        var word = EncodeWord(name); // bei langen Namen mehrere Wörter, gefaltet
        var star = string.Concat(Encoding.UTF8.GetBytes(name).Select(b => b < 128 && (char.IsAsciiLetterOrDigit((char)b) || b is (byte)'.' or (byte)'-' or (byte)'_') ? ((char)b).ToString() : "%" + b.ToString("X2", CultureInfo.InvariantCulture)));
        if (star.Length <= 50) return ($"name=\"{word}\"", $"filename*=UTF-8''{star}");
        // Fortsetzungen „filename*0*=UTF-8''…; filename*1*=…“ – nie mitten in einem %XX trennen
        var parts = new List<string>(); var i = 0;
        while (i < star.Length)
        {
            var n = Math.Min(50, star.Length - i);
            if (i + n < star.Length) { var pct = star.LastIndexOf('%', i + n - 1, Math.Min(n, 3)); if (pct >= 0 && pct + 3 > i + n) n = pct - i; }
            parts.Add(star.Substring(i, n)); i += n;
        }
        return ($"name=\"{word}\"", string.Join(";\r\n ", parts.Select((p, k) => $"filename*{k}*={(k == 0 ? "UTF-8''" : "")}{p}")));
    }

    /// <summary>Neue Nachrichtenkennung für das Konto.</summary>
    public static string NewMessageId(MailAccount acc) => $"<{Guid.NewGuid():N}@{acc.Email.Split('@').Last()}>";

    /// <summary>Fertige Nachricht (RFC 5322 + MIME) als Bytes. Reiner Text → ein Teil; mit <see cref="MailDraft.Html"/> → multipart/alternative;
    /// mit Anhängen → multipart/mixed. Blindkopien stehen nie in den Kopfzeilen. <paramref name="unsent"/>: „X-Unsent: 1“ (Entwurf für fremde Programme).</summary>
    public static byte[] Build(MailAccount acc, MailDraft d, DateTimeOffset now, string? messageId = null, bool unsent = false)
    {
        var sb = new StringBuilder();
        if (unsent) sb.Append("X-Unsent: 1\r\n");
        sb.Append("From: ").Append(acc.Name.Length > 0 ? AddressList($"{acc.Name} <{acc.Email}>") : acc.Email).Append("\r\n");
        sb.Append("To: ").Append(AddressList(d.To)).Append("\r\n");
        if (d.Cc.Trim().Length > 0) sb.Append("Cc: ").Append(AddressList(d.Cc)).Append("\r\n");
        sb.Append("Subject: ").Append(EncodeWord(d.Subject, 9)).Append("\r\n");
        sb.Append("Date: ").Append(Rfc5322Date(now)).Append("\r\n");
        sb.Append("Message-ID: ").Append(messageId ?? NewMessageId(acc)).Append("\r\n");
        if (d.InReplyTo.Trim().Length > 0) sb.Append("In-Reply-To: ").Append(d.InReplyTo.Trim()).Append("\r\n");
        if (d.References.Trim().Length > 0) sb.Append("References: ").Append(string.Join("\r\n ", d.References.Split(' ', StringSplitOptions.RemoveEmptyEntries))).Append("\r\n");
        sb.Append("MIME-Version: 1.0\r\n");

        var files = (d.Attachments ?? []).ToList();
        foreach (var f in files) if (!File.Exists(f)) throw new InvalidOperationException($"Der Anhang „{Path.GetFileName(f)}“ ist nicht mehr da.");
        var id = Guid.NewGuid().ToString("N")[..16];
        void Body()
        {
            if (d.Html.Trim().Length == 0) { TextPart(sb, "text/plain", d.Body); return; }
            var alt = "trendo-alt-" + id;
            sb.Append($"Content-Type: multipart/alternative; boundary=\"{alt}\"\r\n\r\n");
            sb.Append($"--{alt}\r\n"); TextPart(sb, "text/plain", d.Body);
            sb.Append($"--{alt}\r\n"); TextPart(sb, "text/html", d.Html);
            sb.Append($"--{alt}--\r\n");
        }
        if (files.Count == 0) Body();
        else
        {
            var mix = "trendo-mix-" + id;
            sb.Append($"Content-Type: multipart/mixed; boundary=\"{mix}\"\r\n\r\n");
            sb.Append($"--{mix}\r\n"); Body();
            foreach (var f in files)
            {
                var (tn, disp) = FileParams(Path.GetFileName(f));
                sb.Append($"--{mix}\r\nContent-Type: {ContentTypeOf(f)};\r\n {tn}\r\nContent-Disposition: attachment;\r\n {disp}\r\nContent-Transfer-Encoding: base64\r\n\r\n");
                Base64(sb, File.ReadAllBytes(f));
            }
            sb.Append($"--{mix}--\r\n");
        }
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    /// <summary>Einfache Formatierung für HTML-Mails: Absätze, Zeilenumbrüche, Listen mit „- “, **fett**, _kursiv_, Adressen als Links. Alles andere wird maskiert.</summary>
    public static string TextToHtml(string text)
    {
        static string Inline(string s)
        {
            s = s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
            s = Regex.Replace(s, @"\*\*(.+?)\*\*", "<b>$1</b>");
            s = Regex.Replace(s, @"(?<![\w/])_(.+?)_(?![\w/])", "<i>$1</i>");
            return Regex.Replace(s, @"(?<![""=])\bhttps?://[^\s<]+[^\s<.,;:!?)]", m => $"<a href=\"{m.Value}\">{m.Value}</a>");
        }
        var sb = new StringBuilder("<html><body style=\"font-family:sans-serif\">\r\n");
        foreach (var block in Regex.Split(text.Replace("\r", "").Trim('\n'), @"\n{2,}"))
        {
            var lines = block.Split('\n');
            if (lines.All(l => l.TrimStart().StartsWith("- ", StringComparison.Ordinal)))
                sb.Append("<ul>").Append(string.Concat(lines.Select(l => "<li>" + Inline(l.TrimStart()[2..]) + "</li>"))).Append("</ul>\r\n");
            else if (lines.All(l => l.StartsWith('>')))
                sb.Append("<blockquote>").Append(string.Join("<br>\r\n", lines.Select(l => Inline(l.TrimStart('>').TrimStart())))).Append("</blockquote>\r\n");
            else sb.Append("<p>").Append(string.Join("<br>\r\n", lines.Select(Inline))).Append("</p>\r\n");
        }
        return sb.Append("</body></html>").ToString();
    }

    // ---------------------------------------------------------------- Lesen

    sealed record Part(string Head, string Body);

    /// <summary>Nachricht aus den rohen Bytes lesen (Kopfzeilen, Text, HTML, Anhänge, eingebettete Bilder).</summary>
    public static ParsedMail Parse(byte[] raw) => ParseLatin(Latin1.GetString(raw));

    /// <summary>Wie <see cref="Parse(byte[])"/>, für bereits als Text gelesene .eml-Dateien (UTF-8).</summary>
    public static ParsedMail Parse(string raw) => ParseLatin(Latin1.GetString(Encoding.UTF8.GetBytes(raw)));

    static ParsedMail ParseLatin(string latin)
    {
        var p = Split(latin);
        string Hd(string k) => DecodeWords(HeaderOf(p.Head, k));
        var texts = new List<(bool Html, string Text)>(); var atts = new List<MailAttachment>();
        Walk(p, texts, atts, 0);
        var html = texts.FirstOrDefault(t => t.Html).Text ?? "";
        var plain = texts.FirstOrDefault(t => !t.Html).Text;
        var date = Hd("Date");
        return new(Hd("From"), Hd("To"), Hd("Subject"), date, (plain ?? (html.Length > 0 ? MailCenter.HtmlToText(html) : "")).Trim())
        {
            Cc = Hd("Cc"), Attachments = atts, Html = html, MessageId = Hd("Message-ID"), References = Hd("References"), InReplyTo = Hd("In-Reply-To"),
            When = ParseDate(date),
        };
    }

    /// <summary>Datum einer Kopfzeile (Kommentar wie „(CET)“ wird überlesen).</summary>
    public static DateTimeOffset? ParseDate(string date) =>
        DateTimeOffset.TryParse(Regex.Replace(date, @"\s*\([^)]*\)\s*$", ""), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    static Part Split(string part)
    {
        if (part.StartsWith("\r\n", StringComparison.Ordinal)) return new("", part[2..]);
        if (part.StartsWith('\n')) return new("", part[1..]);
        var m = Regex.Match(part, @"\r?\n\r?\n");
        var head = m.Success ? part[..m.Index] : part; var body = m.Success ? part[(m.Index + m.Length)..] : "";
        return new(Regex.Replace(head, @"\r?\n[ \t]+", " "), body);
    }

    static string HeaderOf(string head, string k) =>
        Regex.Match(head, $@"^{Regex.Escape(k)}[ \t]*:[ \t]*(.*?)\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Groups[1].Value.Trim();

    /// <summary>Rohbytes (als Latin-1-Text gehalten) in lesbaren Text: erst strenges UTF-8, sonst Windows-1252.</summary>
    static string Guess(string latin)
    {
        if (Ascii(latin.Replace("\r", "").Replace("\n", "").Replace("\t", ""))) return latin;
        var b = Latin1.GetBytes(latin);
        try { return StrictUtf8.GetString(b); } catch (DecoderFallbackException) { return Encoding.GetEncoding(1252).GetString(b); }
    }

    static Encoding? Charset(string name)
    {
        name = name.Trim().Trim('"').ToLowerInvariant();
        if (name.Length == 0) return null;
        if (name is "utf8" or "utf-8" or "us-ascii" or "ascii") return Encoding.UTF8;
        if (name is "iso-8859-1" or "latin1") name = "windows-1252"; // wie Mailprogramme: Obermenge, zeigt auch „€“ und Anführungszeichen
        try { return Encoding.GetEncoding(name); } catch (ArgumentException) { return null; }
    }

    static string Decode(byte[] data, string charset) => Charset(charset) is { } e && e != Encoding.UTF8 ? e.GetString(data) : Guess(Latin1.GetString(data));

    static byte[] QpBytes(string s, bool header)
    {
        s = Regex.Replace(s, @"=[ \t]*\r?\n", "");
        var bytes = new List<byte>(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '=' && i + 2 < s.Length && Uri.IsHexDigit(s[i + 1]) && Uri.IsHexDigit(s[i + 2])) { bytes.Add(Convert.ToByte(s.Substring(i + 1, 2), 16)); i += 2; }
            else if (header && s[i] == '_') bytes.Add((byte)' ');
            else bytes.Add((byte)s[i]);
        }
        return [.. bytes];
    }

    static byte[] B64Bytes(string s)
    {
        var clean = Regex.Replace(s, @"[^A-Za-z0-9+/]", "");
        if (clean.Length % 4 == 1) clean = clean[..^1];
        while (clean.Length % 4 != 0) clean += "=";
        try { return Convert.FromBase64String(clean); } catch (FormatException) { return Latin1.GetBytes(s); }
    }

    /// <summary>Kodierte Wörter (RFC 2047) auflösen; Leerraum zwischen zwei kodierten Wörtern entfällt. Rohes UTF-8 in Kopfzeilen wird erkannt.</summary>
    public static string DecodeWords(string latin)
    {
        latin = Regex.Replace(latin, @"(\?=)\s+(=\?)", "$1$2");
        // aufeinanderfolgende Wörter gleicher Kodierung erst als Bytes verbinden (ein Zeichen kann über zwei Wörter gehen)
        var res = new StringBuilder(); var pos = 0; var pending = new List<byte>(); string pendingCs = "";
        void Flush() { if (pending.Count > 0) { res.Append(Decode([.. pending], pendingCs)); pending.Clear(); } }
        foreach (Match m in Regex.Matches(latin, @"=\?([^?\s]+)\?([BbQq])\?([^?]*)\?="))
        {
            if (m.Index > pos) { Flush(); res.Append(Guess(latin[pos..m.Index])); }
            var cs = m.Groups[1].Value.Split('*')[0];
            if (pending.Count > 0 && !cs.Equals(pendingCs, StringComparison.OrdinalIgnoreCase)) Flush();
            pendingCs = cs;
            pending.AddRange(m.Groups[2].Value is "B" or "b" ? B64Bytes(m.Groups[3].Value) : QpBytes(m.Groups[3].Value, true));
            pos = m.Index + m.Length;
        }
        Flush();
        if (pos < latin.Length) res.Append(Guess(latin[pos..]));
        return res.ToString();
    }

    /// <summary>Parameter einer Kopfzeile („boundary“, „charset“, „name“, „filename“) – mit Anführungszeichen, RFC 2231 („name*=UTF-8''…“, Fortsetzungen „name*0*=“).</summary>
    static string Param(string header, string key)
    {
        var parts = new SortedDictionary<int, (string Value, bool Encoded)>();
        foreach (Match m in Regex.Matches(header, $@";\s*{Regex.Escape(key)}(\*(\d+))?(\*)?\s*=\s*(""((?:[^""\\]|\\.)*)""|[^;\s]*)", RegexOptions.IgnoreCase))
        {
            var v = m.Groups[5].Success ? Regex.Replace(m.Groups[5].Value, @"\\(.)", "$1") : m.Groups[4].Value;
            var idx = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : m.Groups[3].Success ? -1 : -2;
            parts.TryAdd(idx, (v, m.Groups[3].Success));
        }
        if (parts.Count == 0) return "";
        if (parts.TryGetValue(-1, out var star)) return Star(star.Value);
        var cont = parts.Where(p => p.Key >= 0).ToList();
        if (cont.Count > 0)
        {
            var cs = ""; var bytes = new List<byte>();
            foreach (var (i, (v, enc)) in cont)
            {
                var val = v;
                if (enc && i == 0 && val.Split('\'') is { Length: >= 3 } sp) { cs = sp[0]; val = string.Join('\'', sp.Skip(2)); }
                bytes.AddRange(enc ? Percent(val) : Latin1.GetBytes(val));
            }
            return Decode([.. bytes], cs);
        }
        return DecodeWords(parts[-2].Value);
        static string Star(string v) { var sp = v.Split('\''); return sp.Length >= 3 ? Decode(Percent(string.Join('\'', sp.Skip(2))), sp[0]) : Decode(Percent(v), ""); }
    }

    static byte[] Percent(string s)
    {
        var bytes = new List<byte>();
        for (var i = 0; i < s.Length; i++)
            if (s[i] == '%' && i + 2 < s.Length && Uri.IsHexDigit(s[i + 1]) && Uri.IsHexDigit(s[i + 2])) { bytes.Add(Convert.ToByte(s.Substring(i + 1, 2), 16)); i += 2; }
            else bytes.Add((byte)s[i]);
        return [.. bytes];
    }

    /// <summary>Sicherer Dateiname für einen Anhang: keine Pfadteile, keine Steuerzeichen, nicht leer, höchstens 120 Zeichen.</summary>
    public static string SafeFileName(string name, int number)
    {
        var bad = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var safe = string.Concat(name.Where(c => !bad.Contains(c) && !char.IsControl(c))).Trim().Trim('.').Trim();
        if (safe.Length > 120) safe = safe[..(120 - Path.GetExtension(safe).Length)] + Path.GetExtension(safe);
        return safe.Length > 0 ? safe : "anhang" + (number > 0 ? number.ToString(CultureInfo.InvariantCulture) : "");
    }

    static void Walk(Part p, List<(bool, string)> texts, List<MailAttachment> atts, int depth)
    {
        if (depth > 12) return;
        var ct = HeaderOf(p.Head, "Content-Type"); var enc = HeaderOf(p.Head, "Content-Transfer-Encoding").ToLowerInvariant();
        var disp = HeaderOf(p.Head, "Content-Disposition");
        var type = ct.Split(';')[0].Trim().ToLowerInvariant();
        var boundary = Param(ct, "boundary");
        if (type.StartsWith("multipart/", StringComparison.Ordinal) && boundary.Length > 0)
        {
            var segs = Regex.Split("\n" + p.Body, @"\r?\n--" + Regex.Escape(boundary));
            foreach (var seg in segs.Skip(1))
            {
                if (seg.StartsWith("--", StringComparison.Ordinal)) break; // Ende
                var nl = seg.IndexOf('\n'); if (nl < 0) continue;          // Rest der Trennzeile überspringen
                Walk(Split(seg[(nl + 1)..]), texts, atts, depth + 1);
            }
            return;
        }
        var rawName = Param(disp, "filename") is { Length: > 0 } fn ? fn : Param(ct, "name");
        var isText = type.Length == 0 || type.StartsWith("text/", StringComparison.Ordinal);
        var cid = HeaderOf(p.Head, "Content-ID").Trim('<', '>', ' ');
        byte[] Data() => enc == "base64" ? B64Bytes(p.Body) : enc == "quoted-printable" ? QpBytes(p.Body, false) : Latin1.GetBytes(p.Body);
        if (disp.StartsWith("attachment", StringComparison.OrdinalIgnoreCase) || !isText)
        {
            var fallback = type == "message/rfc822" ? "nachricht.eml" : "";
            atts.Add(new MailAttachment(SafeFileName(rawName.Length > 0 ? rawName : fallback, atts.Count + 1), type.Length > 0 ? type : "application/octet-stream", Data())
            { ContentId = cid, Inline = cid.Length > 0 && !disp.StartsWith("attachment", StringComparison.OrdinalIgnoreCase) && type.StartsWith("image/", StringComparison.Ordinal) });
            return;
        }
        texts.Add((type == "text/html", Decode(Data(), Param(ct, "charset")).Replace("\r\n", "\n")));
    }
}
