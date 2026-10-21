using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

public sealed record Contact(string Id, string Name, string Email, string Phone = "", string Note = "");
public sealed record MailDraft(string To, string Subject, string Body, string Cc = "");
public sealed record ParsedMail(string From, string To, string Subject, string Date, string Body);

/// <summary>SYS-09 Communication Mail & Collaboration Center: Kontakte (mit vCard), Entwürfe als .eml oder mailto für dein Mailprogramm,
/// eingegangene .eml-Dateien lesen. Trendo verschickt selbst nichts – das macht immer dein Mailprogramm, nachdem du es geprüft hast.</summary>
public sealed partial class MailCenter
{
    private readonly string _file; private readonly AuditLog _audit; private readonly List<Contact> _contacts;
    [GeneratedRegex(@"^[^@\s,;<>]+@[^@\s,;<>]+\.[A-Za-z]{2,}$")] private static partial Regex EmailRx();

    public MailCenter(string dataRoot, AuditLog audit)
    {
        _audit = audit; _file = Path.Combine(dataRoot, "kontakte.json");
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

    public string ExportVcf() => string.Concat(Contacts().Select(c => $"BEGIN:VCARD\r\nVERSION:3.0\r\nFN:{Esc(c.Name)}\r\n{(c.Email != "" ? $"EMAIL:{c.Email}\r\n" : "")}{(c.Phone != "" ? $"TEL:{Esc(c.Phone)}\r\n" : "")}{(c.Note != "" ? $"NOTE:{Esc(c.Note)}\r\n" : "")}END:VCARD\r\n"));

    public int ImportVcf(string vcf)
    {
        var n = 0;
        foreach (Match card in Regex.Matches(vcf.Replace("\r\n ", "").Replace("\n ", ""), @"BEGIN:VCARD(.*?)END:VCARD", RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
            string F(string key) => Regex.Match(card.Groups[1].Value, $@"^{key}(?:;[^:\r\n]*)?:(.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Groups[1].Value.Trim().Replace("\\,", ",").Replace("\\;", ";").Replace("\\n", "\n");
            var name = F("FN"); if (name == "") name = F("N").Replace(";", " ").Trim();
            var mail = F("EMAIL");
            if (name == "" || _contacts.Any(c => c.Name == name && c.Email == mail)) continue;
            if (mail != "" && !ValidEmail(mail)) mail = "";
            _contacts.Add(new(Guid.NewGuid().ToString("N")[..8], name, mail, F("TEL"), F("NOTE"))); n++;
        }
        Save(); _audit.Append(Principal.LocalOwner.Name, "contacts.import", $"{n}");
        return n;
    }

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
        var parts = raw.Replace("\r\n", "\n").Split("\n\n", 2);
        var headers = Regex.Replace(parts[0], @"\n[ \t]+", " ");
        string Hd(string k) => DecodeWord(Regex.Match(headers, $@"^{k}:\s*(.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Groups[1].Value.Trim());
        var body = parts.Length > 1 ? parts[1] : "";
        var ct = Hd("Content-Type");
        var bm = Regex.Match(ct, @"boundary=""?([^"";]+)""?", RegexOptions.IgnoreCase);
        var enc = Hd("Content-Transfer-Encoding");
        if (bm.Success)
        {
            var seg = body.Split("--" + bm.Groups[1].Value).Select(s => s.TrimStart('\n')).FirstOrDefault(s => Regex.IsMatch(s, @"(?i)content-type:\s*text/plain"));
            if (seg is not null)
            {
                var sp = seg.Split("\n\n", 2); body = sp.Length > 1 ? sp[1] : "";
                enc = Regex.Match(sp[0], @"(?im)^content-transfer-encoding:\s*(\S+)").Groups[1].Value;
            }
        }
        body = enc.ToLowerInvariant() switch
        {
            "base64" => TryB64(body),
            "quoted-printable" => QP(body),
            _ => body,
        };
        return new(Hd("From"), Hd("To"), Hd("Subject"), Hd("Date"), body.Trim());
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
