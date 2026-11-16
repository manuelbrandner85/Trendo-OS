using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>O1/O2: E-Mail-Konto (IMAP zum Lesen, SMTP zum Senden). Das Passwort liegt im Geheimnisspeicher, nie in einer Datei.</summary>
public sealed record MailAccount(string Email, string Name, string ImapHost, int ImapPort, string SmtpHost, int SmtpPort, bool SmtpImplicitTls, string User)
{
    public string SecretKey => $"mail-passwort-{Email.ToLowerInvariant()}";

    /// <summary>Servereinstellungen bekannter Anbieter aus der Adresse (<see cref="MailProviders"/>), sonst imap./smtp. + Domain.</summary>
    public static MailAccount Guess(string email, string name = "")
    {
        var domain = email.Split('@').LastOrDefault()?.Trim().ToLowerInvariant() ?? "";
        return MailProviders.For(email) is { } p
            ? new MailAccount(email.Trim(), name.Trim(), p.ImapHost, p.ImapPort, p.SmtpHost, p.SmtpPort, p.SmtpImplicitTls, email.Trim())
            : new MailAccount(email.Trim(), name.Trim(), $"imap.{domain}", 993, $"smtp.{domain}", 587, false, email.Trim());
    }
}

/// <summary>Kopfzeile einer Nachricht in der Liste.</summary>
public sealed record MailHeader(long Uid, string From, string Subject, DateTimeOffset? Date, bool Seen)
{
    public string To { get; init; } = "";
    public long Size { get; init; }
    /// <summary>Auf dem Server markiert (Stern).</summary>
    public bool Flagged { get; init; }
    public bool Answered { get; init; }
    /// <summary>Die Nachricht hat vermutlich Anhänge (multipart/mixed).</summary>
    public bool HasAttachment { get; init; }
}

/// <summary>Ordner auf dem Server. <paramref name="Role"/>: inbox, sent, drafts, trash, junk, archive, all oder leer.</summary>
public sealed record MailFolderInfo(string Path, string Name, string Role, bool Selectable = true)
{
    /// <summary>Anzeigename: bekannte Ordner auf Deutsch, sonst der Name vom Server.</summary>
    public string Label => Role switch { "inbox" => "Posteingang", "sent" => "Gesendet", "drafts" => "Entwürfe", "trash" => "Papierkorb", "junk" => "Spam", "archive" => "Archiv", "all" => "Alle Nachrichten", _ => Name };
    public int Order => Role switch { "inbox" => 0, "drafts" => 1, "sent" => 2, "archive" => 3, "junk" => 4, "trash" => 5, "all" => 6, _ => 9 };

    public static string RoleOf(string path, string flags, char delimiter)
    {
        if (path.Equals("INBOX", StringComparison.OrdinalIgnoreCase)) return "inbox";
        foreach (var (flag, role) in new[] { ("\\Sent", "sent"), ("\\Drafts", "drafts"), ("\\Trash", "trash"), ("\\Junk", "junk"), ("\\Archive", "archive"), ("\\All", "all") })
            if (flags.Contains(flag, StringComparison.OrdinalIgnoreCase)) return role;
        var last = (delimiter != '\0' ? path.Split(delimiter).Last() : path).Trim().ToLowerInvariant();
        return last switch
        {
            "sent" or "gesendet" or "sent items" or "sent messages" or "sent mail" or "gesendete elemente" or "gesendete objekte" or "gesendete nachrichten" => "sent",
            "drafts" or "draft" or "entwürfe" or "entwurf" => "drafts",
            "trash" or "papierkorb" or "deleted items" or "deleted messages" or "gelöschte elemente" or "gelöschte objekte" or "gelöscht" or "bin" => "trash",
            "junk" or "spam" or "junk-e-mail" or "junk e-mail" or "spamverdacht" or "bulk mail" => "junk",
            "archive" or "archiv" or "archives" => "archive",
            _ => "",
        };
    }
}

/// <summary>Ordnernamen im IMAP-Format („modified UTF-7“, RFC 3501 5.1.3): „Entwürfe“ ↔ „Entw&amp;APw-rfe“.</summary>
public static class ImapUtf7
{
    public static string Encode(string s)
    {
        var sb = new StringBuilder(); var run = new StringBuilder();
        void Flush()
        {
            if (run.Length == 0) return;
            sb.Append('&').Append(Convert.ToBase64String(Encoding.BigEndianUnicode.GetBytes(run.ToString())).TrimEnd('=').Replace('/', ',')).Append('-'); run.Clear();
        }
        foreach (var c in s)
        {
            if (c is >= ' ' and <= '~') { Flush(); sb.Append(c == '&' ? "&-" : c.ToString()); }
            else run.Append(c);
        }
        Flush();
        return sb.ToString();
    }

    public static string Decode(string s) => Regex.Replace(s, @"&([A-Za-z0-9+,]*)-", m =>
    {
        if (m.Groups[1].Length == 0) return "&";
        var b64 = m.Groups[1].Value.Replace(',', '/'); b64 += new string('=', (4 - b64.Length % 4) % 4);
        try { return Encoding.BigEndianUnicode.GetString(Convert.FromBase64String(b64)); } catch (FormatException) { return m.Value; }
    });
}

/// <summary>IMAP über einen fertigen (TLS-)Stream. Lesen ändert nichts auf dem Server (EXAMINE, BODY.PEEK); gelesen/markieren, verschieben,
/// löschen und ablegen laufen nur über die ausdrücklichen Methoden (SELECT, STORE, MOVE/COPY, EXPUNGE, APPEND). Kern ohne Netz: den Stream liefert die App.</summary>
public sealed class ImapSession(Stream stream)
{
    readonly Stream s = stream; int tag;
    readonly StringBuilder _log = new();
    readonly byte[] _rb = new byte[16384]; int _rp, _rl;
    const string HeaderItems = "(UID FLAGS RFC822.SIZE BODY.PEEK[HEADER.FIELDS (FROM TO SUBJECT DATE CONTENT-TYPE)])";

    /// <summary>Fähigkeiten des Servers (z. B. MOVE, UIDPLUS), nach <see cref="CapabilityAsync"/>.</summary>
    public IReadOnlySet<string> Caps { get; private set; } = new HashSet<string>();
    /// <summary>Kennung des zuletzt geöffneten Ordners (ändert sie sich, sind alle gemerkten Nummern ungültig).</summary>
    public long UidValidity { get; private set; }

    async ValueTask<bool> Fill(CancellationToken ct) { _rp = 0; _rl = await s.ReadAsync(_rb, ct); return _rl > 0; }

    async Task<string> Line(CancellationToken ct)
    {
        var bytes = new List<byte>(128);
        while (true)
        {
            if (_rp >= _rl && !await Fill(ct)) throw new IOException("Server hat die Verbindung beendet.");
            var b = _rb[_rp++];
            if (b == '\n') break;
            if (b != '\r') bytes.Add(b);
        }
        return Encoding.UTF8.GetString([.. bytes]);
    }

    async Task<byte[]> Literal(int n, CancellationToken ct)
    {
        var buf = new byte[n]; var got = 0;
        while (got < n)
        {
            if (_rp >= _rl && !await Fill(ct)) throw new IOException("Verbindung beendet.");
            var take = Math.Min(n - got, _rl - _rp);
            Buffer.BlockCopy(_rb, _rp, buf, got, take); _rp += take; got += take;
        }
        return buf;
    }

    /// <summary>Eine Antwortzeile samt aller Literale lesen (der Text nach einem Literal wird mit Leerzeichen angehängt).</summary>
    async Task<(string Line, List<byte[]> Literals)> Response(CancellationToken ct)
    {
        var line = await Line(ct); var lits = new List<byte[]>();
        while (Regex.Match(line, @"\{(\d+)\}$") is { Success: true } m)
        {
            lits.Add(await Literal(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), ct));
            line += " " + await Line(ct);
        }
        return (line, lits);
    }

    /// <summary>Befehl senden (Zeichenketten und Literale im Wechsel); Antwortzeilen samt Literalen sammeln bis zur getaggten Antwort.</summary>
    async Task<List<(string Line, byte[]? Literal, List<byte[]> Literals)>> Cmd(IReadOnlyList<object> parts, CancellationToken ct, string? logAs = null)
    {
        var t = $"T{++tag}";
        var res = new List<(string, byte[]?, List<byte[]>)>();
        var first = (string)parts[0];
        _log.AppendLine(logAs is not null ? $"{t} {logAs}" : $"{t} {first}{(parts.Count > 1 ? " …" : "")}");
        var pending = new StringBuilder($"{t} ");
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i] is string str) { pending.Append(str); continue; }
            var lit = (byte[])parts[i];
            pending.Append('{').Append(lit.Length.ToString(CultureInfo.InvariantCulture)).Append("}\r\n");
            await s.WriteAsync(Encoding.UTF8.GetBytes(pending.ToString()), ct); await s.FlushAsync(ct); pending.Clear();
            while (true) // auf „+“ warten
            {
                var (line, lits) = await Response(ct);
                if (line.StartsWith('+')) break;
                if (line.StartsWith(t + " ", StringComparison.Ordinal)) throw new InvalidOperationException("Server meldet: " + line[(t.Length + 1)..]);
                res.Add((line, lits.FirstOrDefault(), lits));
            }
            await s.WriteAsync(lit, ct);
        }
        pending.Append("\r\n");
        await s.WriteAsync(Encoding.UTF8.GetBytes(pending.ToString()), ct); await s.FlushAsync(ct);
        while (true)
        {
            var (line, lits) = await Response(ct);
            if (line.StartsWith(t + " ", StringComparison.Ordinal))
            {
                if (!line[(t.Length + 1)..].StartsWith("OK", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(first.StartsWith("LOGIN", StringComparison.Ordinal) ? "Anmeldung abgelehnt – Adresse oder Passwort prüfen (bei Gmail, iCloud, Yahoo und Telekom ein App-Passwort nutzen)." : "Server meldet: " + line[(t.Length + 1)..]);
                return res;
            }
            res.Add((line, lits.FirstOrDefault(), lits));
        }
    }

    Task<List<(string Line, byte[]? Literal, List<byte[]> Literals)>> Cmd(string cmd, CancellationToken ct, bool secret = false) => Cmd([cmd], ct, secret ? "LOGIN ***" : null);

    static string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    static string Box(string box) => Q(ImapUtf7.Encode(box));
    static string Set(IEnumerable<long> uids) => string.Join(",", uids.Distinct().OrderBy(u => u).Select(u => u.ToString(CultureInfo.InvariantCulture)));

    public async Task GreetingAsync(CancellationToken ct = default) { var g = await Line(ct); if (!g.StartsWith("* OK", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Kein IMAP-Server: " + g); }
    public Task LoginAsync(string user, string password, CancellationToken ct = default) => Cmd($"LOGIN {Q(user)} {Q(password)}", ct, secret: true);

    /// <summary>Fähigkeiten abfragen. Kennt der Server den Befehl nicht, bleibt die Liste leer.</summary>
    public async Task<IReadOnlySet<string>> CapabilityAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await Cmd("CAPABILITY", ct);
            Caps = r.Where(x => x.Line.StartsWith("* CAPABILITY", StringComparison.OrdinalIgnoreCase)).SelectMany(x => x.Line.Split(' ').Skip(2)).Select(c => c.ToUpperInvariant()).ToHashSet();
        }
        catch (InvalidOperationException) { }
        return Caps;
    }

    /// <summary>Alle Ordner des Postfachs (Posteingang zuerst, dann die bekannten, dann nach Name).</summary>
    public async Task<List<MailFolderInfo>> FoldersAsync(CancellationToken ct = default)
    {
        var r = await Cmd("LIST \"\" \"*\"", ct);
        var list = new List<MailFolderInfo>();
        foreach (var (line, lit, _) in r)
        {
            var m = Regex.Match(line, @"^\* LIST \(([^)]*)\) (NIL|""((?:[^""\\]|\\.)*)"") (.*)$", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            var rest = m.Groups[4].Value.Trim();
            var raw = lit is not null && rest.StartsWith('{') ? Encoding.UTF8.GetString(lit)
                : rest.StartsWith('"') ? Regex.Replace(rest.Trim('"'), @"\\(.)", "$1") : rest;
            var delim = m.Groups[3].Success && m.Groups[3].Length > 0 ? Regex.Replace(m.Groups[3].Value, @"\\(.)", "$1")[0] : '\0';
            var path = ImapUtf7.Decode(raw);
            if (path.Length == 0) continue;
            var name = delim != '\0' ? string.Join(" / ", path.Split(delim)) : path;
            list.Add(new MailFolderInfo(path, name, MailFolderInfo.RoleOf(path, m.Groups[1].Value, delim), !m.Groups[1].Value.Contains("\\Noselect", StringComparison.OrdinalIgnoreCase)));
        }
        // jede Rolle nur einmal (der erste Treffer gewinnt)
        var seen = new HashSet<string>();
        return list.OrderBy(f => f.Order).ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(f => f.Role.Length > 0 && !seen.Add(f.Role) ? f with { Role = "" } : f).OrderBy(f => f.Order).ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    async Task<int> Open(string verb, string box, CancellationToken ct)
    {
        var r = await Cmd($"{verb} {Box(box)}", ct);
        UidValidity = r.Select(x => Regex.Match(x.Line, @"\[UIDVALIDITY (\d+)\]", RegexOptions.IgnoreCase)).Where(m => m.Success).Select(m => long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).LastOrDefault();
        return r.Select(x => Regex.Match(x.Line, @"^\* (\d+) EXISTS")).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).LastOrDefault();
    }

    /// <summary>Postfach nur lesend öffnen; liefert die Anzahl der Nachrichten.</summary>
    public Task<int> ExamineAsync(string box = "INBOX", CancellationToken ct = default) => Open("EXAMINE", box, ct);

    /// <summary>Postfach zum Ändern öffnen (markieren, verschieben, löschen); liefert die Anzahl der Nachrichten.</summary>
    public Task<int> SelectAsync(string box = "INBOX", CancellationToken ct = default) => Open("SELECT", box, ct);

    static List<MailHeader> ParseHeaders(List<(string Line, byte[]? Literal, List<byte[]> Literals)> r)
    {
        var list = new List<MailHeader>();
        foreach (var (line, lit, _) in r)
        {
            if (lit is null || !Regex.IsMatch(line, @"^\* \d+ FETCH", RegexOptions.IgnoreCase)) continue;
            var uid = Regex.Match(line, @"UID (\d+)"); var flags = Regex.Match(line, @"FLAGS \(([^)]*)\)"); var size = Regex.Match(line, @"RFC822\.SIZE (\d+)");
            var parsed = MailMime.Parse([.. lit, (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n']);
            var f = flags.Success ? flags.Groups[1].Value : "";
            bool Has(string flag) => f.Contains(flag, StringComparison.OrdinalIgnoreCase);
            list.Add(new MailHeader(uid.Success ? long.Parse(uid.Groups[1].Value, CultureInfo.InvariantCulture) : 0, parsed.From, parsed.Subject, parsed.When, Has("\\Seen"))
            {
                To = parsed.To, Size = size.Success ? long.Parse(size.Groups[1].Value, CultureInfo.InvariantCulture) : 0, Flagged = Has("\\Flagged"), Answered = Has("\\Answered"),
                HasAttachment = Regex.IsMatch(Encoding.Latin1.GetString(lit), @"^Content-Type:\s*multipart/mixed", RegexOptions.IgnoreCase | RegexOptions.Multiline),
            });
        }
        return list;
    }

    /// <summary>Die neuesten <paramref name="count"/> Kopfzeilen (neueste zuerst).</summary>
    public Task<List<MailHeader>> HeadersAsync(int exists, int count = 50, CancellationToken ct = default) => HeadersPageAsync(exists, 0, count, ct);

    /// <summary>Eine Seite Kopfzeilen: <paramref name="skip"/> neueste überspringen, dann <paramref name="count"/> Stück (neueste zuerst).</summary>
    public async Task<List<MailHeader>> HeadersPageAsync(int exists, int skip, int count = 50, CancellationToken ct = default)
    {
        var to = exists - skip;
        if (to <= 0 || count <= 0) return [];
        var from = Math.Max(1, to - count + 1);
        var list = ParseHeaders(await Cmd($"FETCH {from}:{to} {HeaderItems}", ct));
        list.Reverse();
        return list;
    }

    /// <summary>Kopfzeilen bestimmter Nachrichten (z. B. Suchtreffer), neueste zuerst.</summary>
    public async Task<List<MailHeader>> HeadersByUidAsync(IReadOnlyCollection<long> uids, CancellationToken ct = default)
    {
        var list = new List<MailHeader>();
        foreach (var chunk in uids.Distinct().OrderBy(u => u).Chunk(200))
            list.AddRange(ParseHeaders(await Cmd($"UID FETCH {Set(chunk)} {HeaderItems}", ct)));
        return list.OrderByDescending(h => h.Uid).ToList();
    }

    /// <summary>Ganze Nachricht als Bytes (ohne sie als gelesen zu markieren).</summary>
    public async Task<byte[]> RawAsync(long uid, CancellationToken ct = default)
    {
        var r = await Cmd($"UID FETCH {uid} (BODY.PEEK[])", ct);
        return r.FirstOrDefault(x => x.Literal is not null).Literal ?? throw new InvalidOperationException("Nachricht nicht gefunden.");
    }

    /// <summary>Ganze Nachricht (ohne sie als gelesen zu markieren).</summary>
    public async Task<ParsedMail> MessageAsync(long uid, CancellationToken ct = default) => MailMime.Parse(await RawAsync(uid, ct));

    /// <summary>Kennzeichen setzen oder entfernen (z. B. „\Seen“ = gelesen, „\Flagged“ = Stern). Vorher <see cref="SelectAsync"/>.</summary>
    public async Task StoreAsync(IReadOnlyCollection<long> uids, string flag, bool add, CancellationToken ct = default)
    {
        if (uids.Count == 0) return;
        if (!Regex.IsMatch(flag, @"^\\?[A-Za-z$]+$")) throw new ArgumentException("Ungültiges Kennzeichen.");
        await Cmd($"UID STORE {Set(uids)} {(add ? "+" : "-")}FLAGS.SILENT ({flag})", ct);
    }

    /// <summary>Endgültig entfernen, was als gelöscht gekennzeichnet ist – mit UIDPLUS nur die genannten Nachrichten.</summary>
    public async Task ExpungeAsync(IReadOnlyCollection<long> uids, CancellationToken ct = default)
    {
        if (Caps.Contains("UIDPLUS") && uids.Count > 0) await Cmd($"UID EXPUNGE {Set(uids)}", ct); else await Cmd("EXPUNGE", ct);
    }

    /// <summary>In einen anderen Ordner verschieben (MOVE, sonst kopieren + löschen). Vorher <see cref="SelectAsync"/> und <see cref="CapabilityAsync"/>.</summary>
    public async Task MoveAsync(IReadOnlyCollection<long> uids, string target, CancellationToken ct = default)
    {
        if (uids.Count == 0) return;
        if (Caps.Contains("MOVE")) { await Cmd($"UID MOVE {Set(uids)} {Box(target)}", ct); return; }
        await Cmd($"UID COPY {Set(uids)} {Box(target)}", ct);
        await StoreAsync(uids, "\\Deleted", true, ct);
        await ExpungeAsync(uids, ct);
    }

    /// <summary>Endgültig löschen (kein Papierkorb). Vorher <see cref="SelectAsync"/>.</summary>
    public async Task DeleteForeverAsync(IReadOnlyCollection<long> uids, CancellationToken ct = default)
    {
        if (uids.Count == 0) return;
        await StoreAsync(uids, "\\Deleted", true, ct);
        await ExpungeAsync(uids, ct);
    }

    static List<long> SearchResult(List<(string Line, byte[]? Literal, List<byte[]> Literals)> r) =>
        r.Where(x => x.Line.StartsWith("* SEARCH", StringComparison.OrdinalIgnoreCase)).SelectMany(x => x.Line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(2))
            .Select(x => long.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var u) ? u : 0).Where(u => u > 0).Distinct().OrderByDescending(u => u).ToList();

    /// <summary>Auf dem Server suchen: Betreff, Absender, Empfänger oder Text enthält <paramref name="text"/>. Liefert die Nummern (neueste zuerst).</summary>
    public async Task<List<long>> SearchAsync(string text, CancellationToken ct = default)
    {
        text = text.Trim();
        if (text.Length == 0) return [];
        if (text.All(c => c is >= ' ' and < (char)127))
            return SearchResult(await Cmd($"UID SEARCH OR OR OR SUBJECT {Q(text)} FROM {Q(text)} TO {Q(text)} BODY {Q(text)}", ct));
        var lit = Encoding.UTF8.GetBytes(text);
        return SearchResult(await Cmd(["UID SEARCH CHARSET UTF-8 OR OR OR SUBJECT ", lit, " FROM ", lit, " TO ", lit, " BODY ", lit], ct));
    }

    /// <summary>Nachrichten mit einer bestimmten Kennung („Message-ID“) finden.</summary>
    public async Task<List<long>> SearchMessageIdAsync(string messageId, CancellationToken ct = default) =>
        messageId.Trim().Length == 0 || !messageId.Trim().All(c => c is > ' ' and < (char)127 && c != '"' && c != '\\') ? [] : SearchResult(await Cmd($"UID SEARCH HEADER Message-ID {Q(messageId.Trim())}", ct));

    /// <summary>Nachricht in einem Ordner ablegen (z. B. Kopie in „Gesendet“, Entwurf in „Entwürfe“).</summary>
    public Task AppendAsync(string box, byte[] raw, string flags = "\\Seen", CancellationToken ct = default) =>
        Cmd([$"APPEND {Box(box)} ({flags}) ", raw], ct, $"APPEND {Box(box)} ({flags}) {{{raw.Length}}}");

    public Task CreateAsync(string box, CancellationToken ct = default) => Cmd($"CREATE {Box(box)}", ct);

    public async Task LogoutAsync(CancellationToken ct = default) { try { await Cmd("LOGOUT", ct); } catch (IOException) { } catch (InvalidOperationException) { } }

    /// <summary>Befehlsprotokoll ohne Passwort und ohne Nachrichteninhalte (für die Fehlersuche).</summary>
    public string Log => _log.ToString();
}

/// <summary>O2: SMTP über einen fertigen Stream. STARTTLS wird über <paramref name="upgrade"/> von der App erledigt (Kern ohne Netz, QA-019).</summary>
public sealed class SmtpSession(Stream stream, Func<Stream, Task<Stream>>? upgrade = null)
{
    Stream s = stream;

    async Task<(int Code, string Text)> Reply(CancellationToken ct)
    {
        var text = new StringBuilder(); string? line;
        do { line = await ReadLine(ct) ?? throw new IOException("Server hat die Verbindung beendet."); text.AppendLine(line); } while (line.Length > 3 && line[3] == '-');
        return (line.Length >= 3 && int.TryParse(line[..3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) ? code : 0, text.ToString().Trim());
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
        if (ok.Length > 0 && !ok.Contains(rep.Code)) throw new InvalidOperationException(rep.Code is 535 or 534 ? "Anmeldung abgelehnt – Passwort prüfen (bei Gmail, iCloud, Yahoo und Telekom ein App-Passwort nutzen)." : $"Server meldet {rep.Code}: {rep.Text}");
        return rep;
    }

    /// <summary>Nachricht (als Text) senden – siehe <see cref="SendRawAsync"/>.</summary>
    public Task SendAsync(string user, string password, string from, IReadOnlyList<string> recipients, string eml, CancellationToken ct = default) =>
        SendRawAsync(user, password, from, recipients, Encoding.UTF8.GetBytes(eml), ct);

    /// <summary>Nachricht senden: EHLO, ggf. STARTTLS, Anmeldung (PLAIN, sonst LOGIN), Absender, Empfänger, Inhalt (Bytes unverändert bis auf die Punkt-Verdopplung).</summary>
    public async Task SendRawAsync(string user, string password, string from, IReadOnlyList<string> recipients, byte[] raw, CancellationToken ct = default)
    {
        if (recipients.Count == 0) throw new InvalidOperationException("Kein gültiger Empfänger.");
        var hello = await Reply(ct); if (hello.Code != 220) throw new InvalidOperationException("Kein SMTP-Server: " + hello.Text);
        var ehlo = await Cmd("EHLO trendo.local", ct, 250);
        if (upgrade is not null && !ehlo.Text.Contains("STARTTLS", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Der Server bietet keine Verschlüsselung an – Trendo sendet das Passwort nicht ungeschützt. Bitte Port 465 (SSL) versuchen.");
        if (upgrade is not null)
        {
            await Cmd("STARTTLS", ct, 220);
            s = await upgrade(s);
            ehlo = await Cmd("EHLO trendo.local", ct, 250);
        }
        var auth = Regex.Match(ehlo.Text, @"^250[ -]AUTH[ =](.*?)\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Groups[1].Value.ToUpperInvariant();
        if (auth.Length > 0 && !auth.Split(' ').Contains("PLAIN") && auth.Split(' ').Contains("LOGIN"))
        {
            await Cmd("AUTH LOGIN", ct, 334);
            await Cmd(Convert.ToBase64String(Encoding.UTF8.GetBytes(user)), ct, 334);
            await Cmd(Convert.ToBase64String(Encoding.UTF8.GetBytes(password)), ct, 235);
        }
        else await Cmd($"AUTH PLAIN {Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{user}\0{password}"))}", ct, 235);
        await Cmd($"MAIL FROM:<{from}>", ct, 250);
        foreach (var to in recipients) await Cmd($"RCPT TO:<{to}>", ct, 250, 251);
        await Cmd("DATA", ct, 354);
        var text = Encoding.Latin1.GetString(raw).Replace("\r\n", "\n").Replace("\r", "\n");
        if (text.EndsWith('\n')) text = text[..^1];
        var body = string.Join("\r\n", text.Split('\n').Select(l => l.StartsWith('.') ? "." + l : l)); // Punkt am Zeilenanfang verdoppeln
        await s.WriteAsync(Encoding.Latin1.GetBytes(body + "\r\n.\r\n"), ct); await s.FlushAsync(ct);
        var done = await Reply(ct); if (done.Code != 250) throw new InvalidOperationException($"Nicht angenommen: {done.Text}");
        try { await Cmd("QUIT", ct); } catch (IOException) { }
    }

    /// <summary>Fertige Nachricht (RFC 5322) mit Absender, Datum und Kennung; Umlaute korrekt kodiert. Siehe <see cref="MailMime.Build"/> (auch HTML und Anhänge).</summary>
    public static string Compose(MailAccount acc, MailDraft d, DateTimeOffset now) => Encoding.ASCII.GetString(MailMime.Build(acc, d, now));

    /// <summary>Alle Empfänger-Adressen (An, Kopie, Blindkopie), ohne Doppelte.</summary>
    public static List<string> Recipients(MailDraft d) => MailMime.SplitAddresses(d.To + "," + d.Cc + "," + d.Bcc)
        .Select(MailMime.AddressOnly).Where(MailCenter.ValidEmail).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
