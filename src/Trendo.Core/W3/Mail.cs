using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Kontakt im Adressbuch (alle Felder außer Name sind freiwillig; ältere Daten ohne die neuen Felder bleiben lesbar).</summary>
public sealed record Contact(string Id, string Name, string Email, string Phone = "", string Note = "", string Mobile = "", string Company = "", string Street = "", string City = "",
    DateOnly? Birthday = null, List<string>? Groups = null, string Photo = "", bool Favorite = false, string Website = "");
public sealed record MailDraft(string To, string Subject, string Body, string Cc = "");
public sealed record ParsedMail(string From, string To, string Subject, string Date, string Body)
{
    /// <summary>Weitere Empfänger (Kopie) – für „Allen antworten“.</summary>
    public string Cc { get; init; } = "";
    /// <summary>Anhänge (Name, Art, Inhalt) – nur gelesen, nie automatisch geöffnet.</summary>
    public IReadOnlyList<MailAttachment> Attachments { get; init; } = [];
}

public sealed record MailAttachment(string Name, string ContentType, byte[] Data)
{
    public long Size => Data.LongLength;
}

/// <summary>SYS-09 Communication Mail & Collaboration Center: Kontakte (mit vCard), Entwürfe als .eml oder mailto für dein Mailprogramm,
/// eingegangene .eml-Dateien lesen. Trendo verschickt selbst nichts – das macht immer dein Mailprogramm, nachdem du es geprüft hast.</summary>
public sealed partial class MailCenter
{
    private readonly string _file, _root; private readonly AuditLog _audit; private readonly List<Contact> _contacts;
    [GeneratedRegex(@"^[^@\s,;<>]+@[^@\s,;<>]+\.[A-Za-z]{2,}$")] private static partial Regex EmailRx();

    public MailCenter(string dataRoot, AuditLog audit)
    {
        _audit = audit; _root = dataRoot; _file = Path.Combine(dataRoot, "kontakte.json");
        try { _contacts = File.Exists(_file) ? JsonSerializer.Deserialize<List<Contact>>(File.ReadAllText(_file)) ?? [] : []; } catch (JsonException) { _contacts = []; }
    }

    public static bool ValidEmail(string e) => EmailRx().IsMatch(e.Trim());
    public IReadOnlyList<Contact> Contacts() => _contacts.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

    public Contact AddContact(string name, string email, string phone = "", string note = "")
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name fehlt.");
        if (email.Length > 0 && !ValidEmail(email)) throw new ArgumentException("Die E-Mail-Adresse sieht nicht richtig aus.");
        var c = new Contact(Guid.NewGuid().ToString("N")[..8], name.Trim(), email.Trim(), phone.Trim(), note.Trim());
        _contacts.Add(c); Save(); return c;
    }
    public bool RemoveContact(string id) { var n = _contacts.RemoveAll(c => c.Id == id); Save(); return n > 0; }
    public IReadOnlyList<Contact> Find(string q) => _contacts.Where(c => (c.Name + " " + c.Email).Contains(q.Trim(), StringComparison.CurrentCultureIgnoreCase)).ToList();

    public static IReadOnlyList<string> Check(MailDraft d)
    {
        var p = new List<string>();
        var to = d.To.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (to.Length == 0) p.Add("Empfänger fehlt.");
        p.AddRange(to.Concat(d.Cc.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Where(a => !ValidEmail(a)).Select(a => $"„{a}“ ist keine gültige E-Mail-Adresse."));
        if (string.IsNullOrWhiteSpace(d.Subject)) p.Add("Betreff fehlt.");
        if (Regex.IsMatch(d.Body, @"(?i)\b(anhang|anbei|im anhang|attached)\b")) p.Add("Du erwähnst einen Anhang – denk daran, ihn im Mailprogramm anzuhängen.");
        if (Regex.IsMatch(d.Body, @"(?i)(passwort|password|kennwort)\s*[:=]")) p.Add("Die Nachricht enthält ein Passwort. Passwörter besser nicht per E-Mail schicken.");
        return p;
    }

    public static string MailTo(MailDraft d) =>
        $"mailto:{string.Join(',', d.To.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))}?subject={Uri.EscapeDataString(d.Subject)}&body={Uri.EscapeDataString(d.Body.Replace("\r\n", "\n").Replace("\n", "\r\n"))}{(d.Cc != "" ? "&cc=" + Uri.EscapeDataString(d.Cc) : "")}";

    /// <summary>RFC-5322-Entwurf mit „X-Unsent: 1“, damit Outlook/Thunderbird ihn als Entwurf zum Abschicken öffnen.</summary>
    public static string ToEml(MailDraft d, DateTimeOffset now)
    {
        static string H(string s) => s.All(c => c < 128) ? s : $"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(s))}?=";
        var body = Convert.ToBase64String(Encoding.UTF8.GetBytes(d.Body.Replace("\r\n", "\n").Replace("\n", "\r\n")), Base64FormattingOptions.InsertLineBreaks);
        return $"X-Unsent: 1\r\nTo: {d.To}\r\n{(d.Cc != "" ? $"Cc: {d.Cc}\r\n" : "")}Subject: {H(d.Subject)}\r\nDate: {now:ddd, dd MMM yyyy HH:mm:ss zzz}\r\nMIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\n{body}\r\n".Replace("+01:00", "+0100").Replace("+02:00", "+0200");
    }

    public string SaveDraft(MailDraft d, string folder, DateTimeOffset now)
    {
        Directory.CreateDirectory(folder);
        var safe = new string(d.Subject.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
        var f = Path.Combine(folder, $"{now:yyyy-MM-dd_HHmm} {(safe == "" ? "Entwurf" : safe[..Math.Min(60, safe.Length)])}.eml");
        File.WriteAllText(f, ToEml(d, now));
        _audit.Append(Principal.LocalOwner.Name, "mail.draft", $"{Path.GetFileName(f)} an {d.To.Split(',').Length} Empfänger");
        return f;
    }

    /// <summary>Liest eine .eml-Datei (Text-Teil, Base64/Quoted-Printable, kodierte Kopfzeilen).</summary>
    public static ParsedMail ParseEml(string raw)
    {
        var (head, body) = SplitPart(raw.Replace("\r\n", "\n"));
        string Hd(string k) => DecodeWord(HeaderOf(head, k));
        var texts = new List<(bool Html, string Text)>(); var atts = new List<MailAttachment>();
        Walk(head, body, texts, atts, 0);
        var text = texts.FirstOrDefault(t => !t.Html).Text ?? (texts.FirstOrDefault(t => t.Html).Text is { } h ? HtmlToText(h) : "");
        return new(Hd("From"), Hd("To"), Hd("Subject"), Hd("Date"), text.Trim()) { Cc = Hd("Cc"), Attachments = atts };
    }

    static (string Head, string Body) SplitPart(string part) { var x = part.Split("\n\n", 2); return (Regex.Replace(x[0], @"\n[ \t]+", " "), x.Length > 1 ? x[1] : ""); }
    static string HeaderOf(string head, string k) => Regex.Match(head, $@"^{k}:\s*(.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Groups[1].Value.Trim();

    /// <summary>MIME-Teile rekursiv lesen (auch multipart/mixed mit multipart/alternative darin): Texte sammeln, Anhänge mit Name und Inhalt.</summary>
    static void Walk(string head, string body, List<(bool, string)> texts, List<MailAttachment> atts, int depth)
    {
        if (depth > 8) return;
        var ct = HeaderOf(head, "Content-Type"); var enc = HeaderOf(head, "Content-Transfer-Encoding").ToLowerInvariant();
        var disp = HeaderOf(head, "Content-Disposition");
        var bm = Regex.Match(ct, @"boundary=""?([^"";]+)""?", RegexOptions.IgnoreCase);
        if (ct.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase) && bm.Success)
        {
            foreach (var seg in body.Split("--" + bm.Groups[1].Value).Skip(1))
            {
                if (seg.StartsWith("--")) break; // Ende
                var (h, b) = SplitPart(seg.TrimStart('\n'));
                Walk(h, b.TrimEnd('\n'), texts, atts, depth + 1);
            }
            return;
        }
        var name = DecodeWord(Regex.Match(disp, @"filename\*?=""?([^"";]+)""?", RegexOptions.IgnoreCase) is { Success: true } fm ? fm.Groups[1].Value
            : Regex.Match(ct, @"name\*?=""?([^"";]+)""?", RegexOptions.IgnoreCase) is { Success: true } nm ? nm.Groups[1].Value : "");
        var isText = ct.Length == 0 || ct.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
        if (disp.StartsWith("attachment", StringComparison.OrdinalIgnoreCase) || name.Length > 0 && !isText || !isText)
        {
            var data = enc == "base64" ? TryBytes(body) : enc == "quoted-printable" ? Encoding.UTF8.GetBytes(QP(body)) : Encoding.UTF8.GetBytes(body);
            var safe = string.Concat((name.Length > 0 ? name : "anhang" + (atts.Count + 1)).Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            atts.Add(new MailAttachment(safe.Length > 0 ? safe : "anhang", ct.Split(';')[0].Trim().ToLowerInvariant() is { Length: > 0 } t ? t : "application/octet-stream", data));
            return;
        }
        var txt = enc switch { "base64" => TryB64(body), "quoted-printable" => QP(body), _ => body };
        texts.Add((ct.StartsWith("text/html", StringComparison.OrdinalIgnoreCase), txt));
    }

    static byte[] TryBytes(string s) { try { return Convert.FromBase64String(Regex.Replace(s, @"\s", "")); } catch (FormatException) { return Encoding.UTF8.GetBytes(s); } }

    /// <summary>HTML-Mail als lesbarer Text (Skripte und Stile entfernt, Zeilenumbrüche erhalten).</summary>
    public static string HtmlToText(string html)
    {
        var t = Regex.Replace(html, @"<(script|style|head)\b[^>]*>.*?</\1>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        t = Regex.Replace(t, @"<(br|/p|/div|/li|/tr|/h[1-6])\b[^>]*>", "\n", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, "<[^>]+>", "");
        return Regex.Replace(System.Net.WebUtility.HtmlDecode(t), @"\n{3,}", "\n\n").Trim();
    }

    private static string TryB64(string s) { try { return Encoding.UTF8.GetString(Convert.FromBase64String(Regex.Replace(s, @"\s", ""))); } catch (FormatException) { return s; } }
    private static string QP(string s)
    {
        s = s.Replace("=\n", "");
        var bytes = new List<byte>();
        for (var i = 0; i < s.Length; i++)
            if (s[i] == '=' && i + 2 < s.Length && Uri.IsHexDigit(s[i + 1]) && Uri.IsHexDigit(s[i + 2])) { bytes.Add(Convert.ToByte(s.Substring(i + 1, 2), 16)); i += 2; }
            else bytes.AddRange(Encoding.UTF8.GetBytes(s[i].ToString()));
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
    private static string DecodeWord(string s) => Regex.Replace(s, @"=\?([^?]+)\?([BbQq])\?([^?]*)\?=", m =>
    {
        try
        {
            var e = Encoding.GetEncoding(m.Groups[1].Value.Equals("utf8", StringComparison.OrdinalIgnoreCase) ? "utf-8" : m.Groups[1].Value);
            return m.Groups[2].Value.ToUpperInvariant() == "B" ? e.GetString(Convert.FromBase64String(m.Groups[3].Value)) : QP(m.Groups[3].Value.Replace('_', ' '));
        }
        catch { return m.Value; }
    });

    private static string Esc(string s) => s.Replace("\\", "\\\\").Replace(",", "\\,").Replace(";", "\\;").Replace("\n", "\\n");
    private void Save() => File.WriteAllText(_file, JsonSerializer.Serialize(_contacts));
}
