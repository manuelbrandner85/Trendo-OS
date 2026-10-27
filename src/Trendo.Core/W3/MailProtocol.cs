using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>O1/O2: E-Mail-Konto (IMAP zum Lesen, SMTP zum Senden). Das Passwort liegt im Geheimnisspeicher, nie in einer Datei.</summary>
public sealed record MailAccount(string Email, string Name, string ImapHost, int ImapPort, string SmtpHost, int SmtpPort, bool SmtpImplicitTls, string User)
{
    public string SecretKey => $"mail-passwort-{Email.ToLowerInvariant()}";

    /// <summary>Servereinstellungen bekannter Anbieter aus der Adresse (sonst imap./smtp. + Domain).</summary>
    public static MailAccount Guess(string email, string name = "")
    {
        var domain = email.Split('@').LastOrDefault()?.ToLowerInvariant() ?? "";
        (string Imap, string Smtp, int SmtpPort, bool Ssl) s = domain switch
        {
            "gmail.com" or "googlemail.com" => ("imap.gmail.com", "smtp.gmail.com", 465, true),
            "gmx.de" or "gmx.net" or "gmx.at" or "gmx.ch" => ("imap.gmx.net", "mail.gmx.net", 587, false),
            "web.de" => ("imap.web.de", "smtp.web.de", 587, false),
            "t-online.de" => ("secureimap.t-online.de", "securesmtp.t-online.de", 465, true),
            "outlook.com" or "hotmail.com" or "hotmail.de" or "live.com" or "live.de" => ("outlook.office365.com", "smtp-mail.outlook.com", 587, false),
            "posteo.de" or "posteo.net" => ("posteo.de", "posteo.de", 465, true),
            "mailbox.org" => ("imap.mailbox.org", "smtp.mailbox.org", 465, true),
            "icloud.com" or "me.com" => ("imap.mail.me.com", "smtp.mail.me.com", 587, false),
            "freenet.de" => ("mx.freenet.de", "mx.freenet.de", 587, false),
            "yahoo.com" or "yahoo.de" => ("imap.mail.yahoo.com", "smtp.mail.yahoo.com", 465, true),
            _ => ($"imap.{domain}", $"smtp.{domain}", 587, false),
        };
        return new MailAccount(email.Trim(), name.Trim(), s.Imap, 993, s.Smtp, s.SmtpPort, s.Ssl, email.Trim());
    }
}

/// <summary>Kopfzeile einer Nachricht im Posteingang.</summary>
public sealed record MailHeader(long Uid, string From, string Subject, DateTimeOffset? Date, bool Seen);

/// <summary>O1: IMAP über einen fertigen (TLS-)Stream – nur lesen: Postfach wird mit EXAMINE geöffnet, Nachrichten mit BODY.PEEK geholt,
/// dadurch ändert Trendo nichts auf dem Server (auch nicht „gelesen“).</summary>
public sealed class ImapSession(Stream stream)
{
    readonly Stream s = stream; int tag;
    readonly StringBuilder _log = new();

    async Task<string> Line(CancellationToken ct)
    {
        var bytes = new List<byte>(); var b = new byte[1];
        while (true) { if (await s.ReadAsync(b, ct) == 0) throw new IOException("Server hat die Verbindung beendet."); if (b[0] == '\n') break; if (b[0] != '\r') bytes.Add(b[0]); }
        return Encoding.UTF8.GetString([.. bytes]);
    }

    async Task<byte[]> Literal(int n, CancellationToken ct) { var buf = new byte[n]; var got = 0; while (got < n) { var r = await s.ReadAsync(buf.AsMemory(got), ct); if (r == 0) throw new IOException("Verbindung beendet."); got += r; } return buf; }

    /// <summary>Befehl senden; Antwortzeilen samt Literalen sammeln bis zur getaggten Antwort.</summary>
    async Task<List<(string Line, byte[]? Literal)>> Cmd(string cmd, CancellationToken ct, bool secret = false)
    {
        var t = $"T{++tag}";
        var data = Encoding.UTF8.GetBytes($"{t} {cmd}\r\n");
        await s.WriteAsync(data, ct); await s.FlushAsync(ct);
        _log.AppendLine(secret ? $"{t} LOGIN ***" : $"{t} {cmd}");
        var res = new List<(string, byte[]?)>();
        while (true)
        {
            var line = await Line(ct);
            byte[]? lit = null;
            var m = Regex.Match(line, @"\{(\d+)\}$");
            if (m.Success) { lit = await Literal(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), ct); line += " " + await Line(ct); }
            if (line.StartsWith(t + " ", StringComparison.Ordinal))
            {
                if (!line[(t.Length + 1)..].StartsWith("OK", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(cmd.StartsWith("LOGIN", StringComparison.Ordinal) ? "Anmeldung abgelehnt – Adresse oder Passwort prüfen (bei Gmail ein App-Passwort nutzen)." : "Server meldet: " + line[(t.Length + 1)..]);
                return res;
            }
            res.Add((line, lit));
        }
    }

    static string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    public async Task GreetingAsync(CancellationToken ct = default) { var g = await Line(ct); if (!g.StartsWith("* OK", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Kein IMAP-Server: " + g); }
    public Task LoginAsync(string user, string password, CancellationToken ct = default) => Cmd($"LOGIN {Q(user)} {Q(password)}", ct, secret: true);

    /// <summary>Postfach nur lesend öffnen; liefert die Anzahl der Nachrichten.</summary>
    public async Task<int> ExamineAsync(string box = "INBOX", CancellationToken ct = default)
    {
        var r = await Cmd($"EXAMINE {Q(box)}", ct);
        return r.Select(x => Regex.Match(x.Line, @"^\* (\d+) EXISTS")).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).LastOrDefault();
    }

    /// <summary>Die neuesten <paramref name="count"/> Kopfzeilen (neueste zuerst).</summary>
    public async Task<List<MailHeader>> HeadersAsync(int exists, int count = 50, CancellationToken ct = default)
    {
        if (exists <= 0) return [];
        var from = Math.Max(1, exists - count + 1);
        var r = await Cmd($"FETCH {from}:{exists} (UID FLAGS BODY.PEEK[HEADER.FIELDS (FROM SUBJECT DATE)])", ct);
        var list = new List<MailHeader>();
        foreach (var (line, lit) in r)
        {
            if (lit is null) continue;
            var uid = Regex.Match(line, @"UID (\d+)"); var flags = Regex.Match(line, @"FLAGS \(([^)]*)\)");
            var parsed = MailCenter.ParseEml(Encoding.UTF8.GetString(lit) + "\r\n\r\n");
            DateTimeOffset? date = DateTimeOffset.TryParse(Regex.Replace(parsed.Date, @"\s*\([^)]*\)\s*$", ""), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
            list.Add(new MailHeader(uid.Success ? long.Parse(uid.Groups[1].Value, CultureInfo.InvariantCulture) : 0, parsed.From, parsed.Subject, date, flags.Success && flags.Groups[1].Value.Contains("\\Seen", StringComparison.OrdinalIgnoreCase)));
        }
        list.Reverse();
        return list;
    }

    /// <summary>Ganze Nachricht (ohne sie als gelesen zu markieren).</summary>
    public async Task<ParsedMail> MessageAsync(long uid, CancellationToken ct = default)
    {
        var r = await Cmd($"UID FETCH {uid} (BODY.PEEK[])", ct);
        var lit = r.FirstOrDefault(x => x.Literal is not null).Literal ?? throw new InvalidOperationException("Nachricht nicht gefunden.");
        return MailCenter.ParseEml(Encoding.UTF8.GetString(lit));
    }

    public async Task LogoutAsync(CancellationToken ct = default) { try { await Cmd("LOGOUT", ct); } catch (IOException) { } }

    /// <summary>Befehlsprotokoll ohne Passwort (für die Fehlersuche).</summary>
    public string Log => _log.ToString();
}

/// <summary>O2: SMTP über einen fertigen Stream. STARTTLS wird über <paramref name="upgrade"/> von der App erledigt (Kern ohne Netz, QA-019).</summary>
public sealed class SmtpSession(Stream stream, Func<Stream, Task<Stream>>? upgrade = null)
{
    Stream s = stream;
    StreamReader? r;

    async Task<(int Code, string Text)> Reply(CancellationToken ct)
    {
        r ??= new StreamReader(s, Encoding.ASCII, false, 1, leaveOpen: true);
        var text = new StringBuilder(); string? line;
        do { line = await ReadLine(ct) ?? throw new IOException("Server hat die Verbindung beendet."); text.AppendLine(line); } while (line.Length > 3 && line[3] == '-');
        return (int.Parse(line[..3], CultureInfo.InvariantCulture), text.ToString().Trim());
    }

    async Task<string?> ReadLine(CancellationToken ct)
    {
        var bytes = new List<byte>(); var b = new byte[1];
        while (true) { if (await s.ReadAsync(b, ct) == 0) return bytes.Count == 0 ? null : Encoding.ASCII.GetString([.. bytes]); if (b[0] == '\n') break; if (b[0] != '\r') bytes.Add(b[0]); }
        return Encoding.ASCII.GetString([.. bytes]);
    }

    async Task<(int Code, string Text)> Cmd(string line, CancellationToken ct, params int[] ok)
    {
        await s.WriteAsync(Encoding.ASCII.GetBytes(line + "\r\n"), ct); await s.FlushAsync(ct);
        var rep = await Reply(ct);
        if (ok.Length > 0 && !ok.Contains(rep.Code)) throw new InvalidOperationException(rep.Code is 535 or 534 ? "Anmeldung abgelehnt – Passwort prüfen (bei Gmail ein App-Passwort nutzen)." : $"Server meldet {rep.Code}: {rep.Text}");
        return rep;
    }

    /// <summary>Nachricht senden: EHLO, ggf. STARTTLS, Anmeldung (PLAIN), Absender, Empfänger, Inhalt.</summary>
    public async Task SendAsync(string user, string password, string from, IReadOnlyList<string> recipients, string eml, CancellationToken ct = default)
    {
        var hello = await Reply(ct); if (hello.Code != 220) throw new InvalidOperationException("Kein SMTP-Server: " + hello.Text);
        var ehlo = await Cmd("EHLO trendo.local", ct, 250);
        if (upgrade is not null && !ehlo.Text.Contains("STARTTLS", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Der Server bietet keine Verschlüsselung an – Trendo sendet das Passwort nicht ungeschützt. Bitte Port 465 (SSL) versuchen.");
        if (upgrade is not null)
        {
            await Cmd("STARTTLS", ct, 220);
            s = await upgrade(s); r = null;
            await Cmd("EHLO trendo.local", ct, 250);
        }
        var plain = Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{user}\0{password}"));
        await Cmd($"AUTH PLAIN {plain}", ct, 235);
        await Cmd($"MAIL FROM:<{from}>", ct, 250);
        foreach (var to in recipients) await Cmd($"RCPT TO:<{to}>", ct, 250, 251);
        await Cmd("DATA", ct, 354);
        var body = string.Join("\r\n", eml.Replace("\r\n", "\n").Split('\n').Select(l => l.StartsWith('.') ? "." + l : l)); // Punkt am Zeilenanfang verdoppeln
        await s.WriteAsync(Encoding.UTF8.GetBytes(body + "\r\n.\r\n"), ct); await s.FlushAsync(ct);
        var done = await Reply(ct); if (done.Code != 250) throw new InvalidOperationException($"Nicht angenommen: {done.Text}");
        try { await Cmd("QUIT", ct); } catch (IOException) { }
    }

    /// <summary>Fertige Nachricht (RFC 5322) mit Absender, Datum und Kennung; Umlaute korrekt kodiert.</summary>
    public static string Compose(MailAccount acc, MailDraft d, DateTimeOffset now)
    {
        static string H(string s) => s.All(c => c < 128) ? s : $"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(s))}?=";
        var from = acc.Name.Length > 0 ? $"{H(acc.Name)} <{acc.Email}>" : acc.Email;
        var sb = new StringBuilder();
        sb.Append($"From: {from}\r\nTo: {d.To}\r\n");
        if (d.Cc.Length > 0) sb.Append($"Cc: {d.Cc}\r\n");
        sb.Append($"Subject: {H(d.Subject)}\r\nDate: {now.ToString("ddd, dd MMM yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture).Remove(now.ToString("ddd, dd MMM yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture).Length - 3, 1)}\r\n");
        sb.Append($"Message-ID: <{Guid.NewGuid():N}@{acc.Email.Split('@').Last()}>\r\nMIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\n");
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(d.Body.Replace("\r\n", "\n").Replace("\n", "\r\n")));
        for (var i = 0; i < b64.Length; i += 76) sb.Append(b64.AsSpan(i, Math.Min(76, b64.Length - i))).Append("\r\n");
        return sb.ToString();
    }

    public static List<string> Recipients(MailDraft d) => (d.To + "," + d.Cc).Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => Regex.Match(x, @"<([^>]+)>") is { Success: true } m ? m.Groups[1].Value : x).Where(MailCenter.ValidEmail).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>O6: Regelvorschlag für den Posteingang. Regeln zeigen nur an, was passieren würde; aktiv sortieren sie erst nach Freigabe – und auch dann
/// nur in Trendos Ansicht (Kategorie), nie auf dem Server.</summary>
public sealed record MailRule(string Id, string Name, string FromContains, string SubjectContains, string Category, bool Active = false)
{
    public bool Matches(MailHeader h) =>
        (FromContains.Length == 0 || h.From.Contains(FromContains, StringComparison.CurrentCultureIgnoreCase)) &&
        (SubjectContains.Length == 0 || h.Subject.Contains(SubjectContains, StringComparison.CurrentCultureIgnoreCase)) &&
        (FromContains.Length + SubjectContains.Length > 0);

    /// <summary>Vorschläge aus dem Posteingang: häufige Absender bekommen eine eigene Kategorie.</summary>
    public static List<MailRule> Suggest(IEnumerable<MailHeader> inbox, int minCount = 3) =>
        inbox.Select(h => Regex.Match(h.From, @"<?([^<>\s]+@([^<>\s]+))>?") is { Success: true } m ? m.Groups[2].Value.ToLowerInvariant() : "")
            .Where(d => d.Length > 0).GroupBy(d => d).Where(g => g.Count() >= minCount).OrderByDescending(g => g.Count()).Take(5)
            .Select(g => new MailRule(Guid.NewGuid().ToString("N")[..8], $"Post von {g.Key}", "@" + g.Key, "", g.Key.Split('.')[0])).ToList();
}
