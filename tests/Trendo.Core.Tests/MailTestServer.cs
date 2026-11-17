using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.TestMail;

/// <summary>T-0689: Echter kleiner Mailserver für die Tests – spricht IMAP4rev1 (Ordner, Auswahl, Abruf mit Literalen, Kennzeichen, Kopieren/Verschieben,
/// Löschen, Suche, Ablegen) und SMTP (EHLO, STARTTLS, AUTH PLAIN/LOGIN, DATA mit Punkt-Verdopplung) über echte Loopback-Verbindungen und hält die
/// Postfächer im Speicher. Was per SMTP an die eigene Adresse geht, liegt danach im Posteingang und ist per IMAP abrufbar (Rundreise).
/// Kein Ersatz der Protokollschicht von Trendo: Der Server kennt keinen Trendo-Code außer <c>MailMime.Parse</c> für den Textvergleich bei SEARCH.
/// Ordnernamen werden so gespeichert, wie sie über die Leitung gehen (z. B. „Entw&amp;APw-rfe“).</summary>
public sealed class MailTestServer : IDisposable
{
    public sealed class Msg { public long Uid; public byte[] Raw = []; public HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase); }
    public sealed class Box { public string Name = ""; public string Special = ""; public long UidValidity = 7001; public long UidNext = 1; public List<Msg> Msgs = []; }

    readonly TcpListener _imap = new(IPAddress.Loopback, 0), _smtp = new(IPAddress.Loopback, 0);
    readonly CancellationTokenSource _cts = new();
    readonly object _lock = new();
    public string User { get; }
    public string Password { get; }
    public List<Box> Boxes { get; } = [];
    public List<string> ImapLog { get; } = [];
    public List<string> SmtpLog { get; } = [];
    /// <summary>Alles, was per SMTP angenommen wurde (Absender, Empfänger, Rohbytes).</summary>
    public List<(string From, List<string> To, byte[] Raw)> Accepted { get; } = [];
    public bool SupportsMove { get; set; } = true;
    public bool SupportsUidPlus { get; set; } = true;
    /// <summary>Gesetzt: IMAP läuft über TLS, SMTP bietet STARTTLS und verlangt es vor der Anmeldung.</summary>
    public X509Certificate2? Certificate { get; }
    public int ImapPort => ((IPEndPoint)_imap.LocalEndpoint).Port;
    public int SmtpPort => ((IPEndPoint)_smtp.LocalEndpoint).Port;

    public MailTestServer(string user = "erika@example.org", string password = "geheim-Testwort", bool tls = false)
    {
        User = user; Password = password;
        if (tls) Certificate = SelfSigned();
        foreach (var (n, s) in new[] { ("INBOX", ""), ("Entw&APw-rfe", "\\Drafts"), ("Gesendet", "\\Sent"), ("Papierkorb", "\\Trash"), ("Spam", "\\Junk"), ("Rechnungen", "") })
            Boxes.Add(new Box { Name = n, Special = s, UidValidity = 7001 + Boxes.Count });
        _imap.Start(); _smtp.Start();
        _ = Accept(_imap, Imap); _ = Accept(_smtp, Smtp);
    }

    static X509Certificate2 SelfSigned()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddDnsName("localhost"); san.AddIpAddress(IPAddress.Loopback); req.CertificateExtensions.Add(san.Build());
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null);
    }

    public Box Get(string name) => Boxes.First(b => b.Name.Equals(name, StringComparison.Ordinal) || name.Equals("INBOX", StringComparison.OrdinalIgnoreCase) && b.Name == "INBOX");
    Box? Find(string name) => Boxes.FirstOrDefault(b => b.Name.Equals(name, StringComparison.Ordinal) || name.Equals("INBOX", StringComparison.OrdinalIgnoreCase) && b.Name == "INBOX");

    /// <summary>Nachricht direkt in ein Postfach legen (wie eine eingegangene Mail).</summary>
    public long Deliver(string box, byte[] raw, params string[] flags)
    {
        lock (_lock) { var b = Get(box); var m = new Msg { Uid = b.UidNext++, Raw = raw }; foreach (var f in flags) m.Flags.Add(f); b.Msgs.Add(m); return m.Uid; }
    }
    public long Deliver(string box, string raw, params string[] flags) => Deliver(box, Encoding.UTF8.GetBytes(raw), flags);

    async Task Accept(TcpListener l, Func<Stream, Task> handle)
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var c = await l.AcceptTcpClientAsync(_cts.Token);
                _ = Task.Run(async () => { try { using (c) await handle(c.GetStream()); } catch (Exception e) when (e is IOException or ObjectDisposedException or OperationCanceledException or System.Security.Authentication.AuthenticationException) { } });
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException) { }
    }

    public void Dispose() { _cts.Cancel(); _imap.Stop(); _smtp.Stop(); Certificate?.Dispose(); }

    // ------------------------------------------------------------ Leitung
    sealed class Wire(Stream s)
    {
        public Stream S = s; readonly byte[] _b = new byte[8192]; int _p, _l;
        async Task<bool> Fill(CancellationToken ct) { _p = 0; _l = await S.ReadAsync(_b, ct); return _l > 0; }
        public async Task<byte[]?> LineBytes(CancellationToken ct)
        {
            var bytes = new List<byte>();
            while (true)
            {
                if (_p >= _l && !await Fill(ct)) return bytes.Count == 0 ? null : [.. bytes];
                var b = _b[_p++]; if (b == '\n') break; bytes.Add(b);
            }
            if (bytes.Count > 0 && bytes[^1] == '\r') bytes.RemoveAt(bytes.Count - 1);
            return [.. bytes];
        }
        public async Task<string?> Line(CancellationToken ct) => await LineBytes(ct) is { } b ? Encoding.UTF8.GetString(b) : null;
        public async Task<byte[]> Bytes(int n, CancellationToken ct)
        {
            var buf = new byte[n]; var got = 0;
            while (got < n) { if (_p >= _l && !await Fill(ct)) throw new IOException("zu Ende"); var take = Math.Min(n - got, _l - _p); Buffer.BlockCopy(_b, _p, buf, got, take); _p += take; got += take; }
            return buf;
        }
        public async Task Send(string text, CancellationToken ct) { await S.WriteAsync(Encoding.UTF8.GetBytes(text), ct); await S.FlushAsync(ct); }
        public async Task Send(byte[] data, CancellationToken ct) { await S.WriteAsync(data, ct); await S.FlushAsync(ct); }
        public bool Buffered => _p < _l;
    }

    // ------------------------------------------------------------ IMAP
    static List<object> Tokens(string s)
    {
        var res = new List<object>(); var i = 0;
        while (i < s.Length)
        {
            if (s[i] == ' ') { i++; continue; }
            if (s[i] == '"')
            {
                var sb = new StringBuilder(); i++;
                while (i < s.Length && s[i] != '"') { if (s[i] == '\\' && i + 1 < s.Length) i++; sb.Append(s[i++]); }
                i++; res.Add(sb.ToString()); continue;
            }
            var start = i; int paren = 0, bracket = 0;
            while (i < s.Length && (s[i] != ' ' || paren > 0 || bracket > 0))
            {
                if (s[i] == '(') paren++; else if (s[i] == ')') paren--; else if (s[i] == '[') bracket++; else if (s[i] == ']') bracket--;
                i++;
            }
            res.Add(s[start..i]);
        }
        return res;
    }

    static IEnumerable<int> SeqSet(string set, int max, Func<int, long> uidAt, bool uid)
    {
        if (max == 0) yield break;
        long Val(string v) => v == "*" ? (uid ? uidAt(max) : max) : long.Parse(v, CultureInfo.InvariantCulture);
        for (var seq = 1; seq <= max; seq++)
        {
            var n = uid ? uidAt(seq) : seq;
            foreach (var part in set.Split(','))
            {
                var r = part.Split(':'); var a = Val(r[0]); var b = r.Length > 1 ? Val(r[1]) : a;
                if (n >= Math.Min(a, b) && n <= Math.Max(a, b)) { yield return seq; break; }
            }
        }
    }

    static byte[] HeaderFields(byte[] raw, string[] names)
    {
        var text = Encoding.Latin1.GetString(raw);
        var end = Regex.Match(text, @"\r?\n\r?\n"); var head = end.Success ? text[..end.Index] : text;
        var sb = new StringBuilder(); var keep = false;
        foreach (var line in head.Split('\n'))
        {
            var l = line.TrimEnd('\r');
            if (l.Length > 0 && (l[0] == ' ' || l[0] == '\t')) { if (keep) sb.Append(l).Append("\r\n"); continue; }
            var name = l.Split(':')[0].Trim();
            keep = names.Contains(name, StringComparer.OrdinalIgnoreCase);
            if (keep) sb.Append(l).Append("\r\n");
        }
        return Encoding.Latin1.GetBytes(sb.Append("\r\n").ToString());
    }

    bool Matches(Msg m, List<object> crit, ref int i)
    {
        string Str(ref int k) => crit[k++] is byte[] b ? Encoding.UTF8.GetString(b) : (string)crit[k - 1];
        var key = ((string)crit[i++]).ToUpperInvariant();
        var p = new Lazy<Trendo.Core.ParsedMail>(() => Trendo.Core.MailMime.Parse(m.Raw));
        bool Has(string hay, string needle) => hay.Contains(needle, StringComparison.CurrentCultureIgnoreCase);
        switch (key)
        {
            case "ALL": return true;
            case "SEEN": return m.Flags.Contains("\\Seen");
            case "UNSEEN": return !m.Flags.Contains("\\Seen");
            case "OR": { var a = Matches(m, crit, ref i); var b = Matches(m, crit, ref i); return a || b; }
            case "SUBJECT": return Has(p.Value.Subject, Str(ref i));
            case "FROM": return Has(p.Value.From, Str(ref i));
            case "TO": return Has(p.Value.To, Str(ref i));
            case "BODY": return Has(p.Value.Body, Str(ref i));
            case "TEXT": { var t = Str(ref i); return Has(p.Value.Subject + p.Value.From + p.Value.Body, t); }
            case "HEADER": { var name = Str(ref i); var val = Str(ref i); return Has(Encoding.Latin1.GetString(HeaderFields(m.Raw, [name])), val); }
            default: throw new FormatException("SEARCH: " + key);
        }
    }

    async Task Imap(Stream raw)
    {
        var ct = _cts.Token; Stream s = raw;
        if (Certificate is not null) { var ssl = new SslStream(raw, false); await ssl.AuthenticateAsServerAsync(Certificate, false, false); s = ssl; }
        var w = new Wire(s);
        await w.Send("* OK IMAP4rev1 Trendo-Testserver bereit\r\n", ct);
        var authed = false; Box? sel = null; var readOnly = false;
        while (await w.Line(ct) is { } first)
        {
            // Befehl samt Literalen einsammeln
            var args = new List<object>(); var line = first; byte[]? lastLiteral = null;
            while (Regex.Match(line, @"\{(\d+)(\+?)\}$") is { Success: true } lm)
            {
                args.AddRange(Tokens(line[..lm.Index]));
                if (lm.Groups[2].Length == 0) await w.Send("+ weiter\r\n", ct);
                lastLiteral = await w.Bytes(int.Parse(lm.Groups[1].Value, CultureInfo.InvariantCulture), ct);
                args.Add(lastLiteral);
                line = await w.Line(ct) ?? "";
            }
            args.AddRange(Tokens(line));
            if (args.Count < 2 || args[0] is not string tag || args[1] is not string) { await w.Send("* BAD unverstanden\r\n", ct); continue; }
            var cmd = ((string)args[1]).ToUpperInvariant(); var a = args.Skip(2).ToList();
            var uidMode = false;
            if (cmd == "UID" && a.Count > 0) { uidMode = true; cmd = ((string)a[0]).ToUpperInvariant(); a = a.Skip(1).ToList(); }
            lock (ImapLog) ImapLog.Add(cmd == "LOGIN" ? $"{tag} LOGIN {a.ElementAtOrDefault(0)} ***" : (uidMode ? "UID " : "") + cmd + " " + string.Join(" ", a.Select(x => x is byte[] b ? $"{{{b.Length}}}" : (string)x)));
            string S(int k) => a[k] is byte[] b ? Encoding.UTF8.GetString(b) : (string)a[k];
            var sb = new StringBuilder(); var chunks = new List<byte[]>();
            void Out(string t) => chunks.Add(Encoding.UTF8.GetBytes(t));
            string Flags(Msg m) => string.Join(" ", m.Flags);
            try
            {
                lock (_lock)
                {
                    switch (cmd)
                    {
                        case "CAPABILITY": Out($"* CAPABILITY IMAP4rev1 SPECIAL-USE{(SupportsMove ? " MOVE" : "")}{(SupportsUidPlus ? " UIDPLUS" : "")}\r\n{tag} OK\r\n"); break;
                        case "NOOP": Out($"{tag} OK\r\n"); break;
                        case "LOGOUT": Out($"* BYE\r\n{tag} OK\r\n"); break;
                        case "LOGIN": authed = S(0) == User && S(1) == Password; Out(authed ? $"{tag} OK angemeldet\r\n" : $"{tag} NO [AUTHENTICATIONFAILED] falsches Passwort\r\n"); break;
                        case var _ when !authed: Out($"{tag} NO erst anmelden\r\n"); break;
                        case "LIST":
                            foreach (var b in Boxes) Out($"* LIST (\\HasNoChildren{(b.Special.Length > 0 ? " " + b.Special : "")}) \"/\" \"{b.Name}\"\r\n");
                            Out($"{tag} OK\r\n"); break;
                        case "CREATE": if (Find(S(0)) is null) Boxes.Add(new Box { Name = S(0), UidValidity = 7001 + Boxes.Count }); Out($"{tag} OK\r\n"); break;
                        case "SELECT" or "EXAMINE":
                            sel = Find(S(0)); readOnly = cmd == "EXAMINE";
                            if (sel is null) { Out($"{tag} NO Ordner gibt es nicht\r\n"); break; }
                            Out($"* {sel.Msgs.Count} EXISTS\r\n* 0 RECENT\r\n* FLAGS (\\Seen \\Answered \\Flagged \\Deleted \\Draft)\r\n* OK [UIDVALIDITY {sel.UidValidity}] ok\r\n* OK [UIDNEXT {sel.UidNext}] ok\r\n{tag} OK [{(readOnly ? "READ-ONLY" : "READ-WRITE")}] {cmd}\r\n");
                            break;
                        case var _ when sel is null && cmd is "FETCH" or "STORE" or "COPY" or "MOVE" or "EXPUNGE" or "SEARCH": Out($"{tag} BAD kein Ordner gewählt\r\n"); break;
                        case "FETCH":
                        {
                            var box = sel!; var items = string.Join(" ", a.Skip(1).Select(x => (string)x));
                            foreach (var seq in SeqSet(S(0), box.Msgs.Count, q => box.Msgs[q - 1].Uid, uidMode).ToList())
                            {
                                var m = box.Msgs[seq - 1]; byte[]? lit = null; var litName = "";
                                var hf = Regex.Match(items, @"BODY(\.PEEK)?\[HEADER\.FIELDS \(([^)]*)\)\]", RegexOptions.IgnoreCase);
                                var full = Regex.Match(items, @"BODY(\.PEEK)?\[\]", RegexOptions.IgnoreCase);
                                if (hf.Success) { lit = HeaderFields(m.Raw, hf.Groups[2].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)); litName = $"BODY[HEADER.FIELDS ({hf.Groups[2].Value})]"; }
                                else if (full.Success) { lit = m.Raw; litName = "BODY[]"; if (full.Groups[1].Length == 0 && !readOnly) m.Flags.Add("\\Seen"); }
                                var head = $"* {seq} FETCH (UID {m.Uid} FLAGS ({Flags(m)}) RFC822.SIZE {m.Raw.Length}";
                                if (lit is null) Out(head + ")\r\n");
                                else { Out($"{head} {litName} {{{lit.Length}}}\r\n"); chunks.Add(lit); Out(")\r\n"); }
                            }
                            Out($"{tag} OK FETCH\r\n"); break;
                        }
                        case "STORE":
                        {
                            if (readOnly) { Out($"{tag} NO Ordner ist nur lesend geöffnet\r\n"); break; }
                            var box = sel!; var op = S(1).ToUpperInvariant(); var flags = S(2).Trim('(', ')').Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var seq in SeqSet(S(0), box.Msgs.Count, q => box.Msgs[q - 1].Uid, uidMode).ToList())
                            {
                                var m = box.Msgs[seq - 1];
                                foreach (var f in flags) { if (op.StartsWith('+')) m.Flags.Add(f); else if (op.StartsWith('-')) m.Flags.Remove(f); }
                                if (!op.Contains("SILENT")) Out($"* {seq} FETCH (UID {m.Uid} FLAGS ({Flags(m)}))\r\n");
                            }
                            Out($"{tag} OK STORE\r\n"); break;
                        }
                        case "COPY" or "MOVE":
                        {
                            if (cmd == "MOVE" && !SupportsMove) { Out($"{tag} BAD MOVE unbekannt\r\n"); break; }
                            if (cmd == "MOVE" && readOnly) { Out($"{tag} NO Ordner ist nur lesend geöffnet\r\n"); break; }
                            var box = sel!; var target = Find(S(1));
                            if (target is null) { Out($"{tag} NO [TRYCREATE] Zielordner gibt es nicht\r\n"); break; }
                            var seqs = SeqSet(S(0), box.Msgs.Count, q => box.Msgs[q - 1].Uid, uidMode).ToList();
                            foreach (var seq in seqs) { var m = box.Msgs[seq - 1]; target.Msgs.Add(new Msg { Uid = target.UidNext++, Raw = m.Raw, Flags = new(m.Flags.Where(f => f != "\\Deleted"), StringComparer.OrdinalIgnoreCase) }); }
                            if (cmd == "MOVE") foreach (var seq in seqs.OrderByDescending(x => x)) { box.Msgs.RemoveAt(seq - 1); Out($"* {seq} EXPUNGE\r\n"); }
                            Out($"{tag} OK {cmd}\r\n"); break;
                        }
                        case "EXPUNGE":
                        {
                            if (uidMode && !SupportsUidPlus) { Out($"{tag} BAD UID EXPUNGE unbekannt\r\n"); break; }
                            if (readOnly) { Out($"{tag} NO Ordner ist nur lesend geöffnet\r\n"); break; }
                            var box = sel!; var only = uidMode ? SeqSet(S(0), box.Msgs.Count, q => box.Msgs[q - 1].Uid, true).ToHashSet() : null;
                            for (var seq = box.Msgs.Count; seq >= 1; seq--)
                                if (box.Msgs[seq - 1].Flags.Contains("\\Deleted") && (only is null || only.Contains(seq))) { box.Msgs.RemoveAt(seq - 1); Out($"* {seq} EXPUNGE\r\n"); }
                            Out($"{tag} OK EXPUNGE\r\n"); break;
                        }
                        case "SEARCH":
                        {
                            var box = sel!; var crit = a.ToList();
                            if (crit.Count > 1 && crit[0] is string c0 && c0.Equals("CHARSET", StringComparison.OrdinalIgnoreCase)) crit = crit.Skip(2).ToList();
                            var hits = new List<long>();
                            for (var seq = 1; seq <= box.Msgs.Count; seq++) { var k = 0; var ok = true; while (k < crit.Count) ok &= Matches(box.Msgs[seq - 1], crit, ref k); if (ok) hits.Add(uidMode ? box.Msgs[seq - 1].Uid : seq); }
                            Out($"* SEARCH{string.Concat(hits.Select(h => " " + h))}\r\n{tag} OK SEARCH\r\n"); break;
                        }
                        case "APPEND":
                        {
                            var target = Find(S(0));
                            if (target is null || lastLiteral is null) { Out($"{tag} NO [TRYCREATE] Zielordner gibt es nicht\r\n"); break; }
                            var m = new Msg { Uid = target.UidNext++, Raw = lastLiteral };
                            foreach (var f in a.Skip(1).OfType<string>().Where(x => x.StartsWith('(')).SelectMany(x => x.Trim('(', ')').Split(' ', StringSplitOptions.RemoveEmptyEntries))) m.Flags.Add(f);
                            target.Msgs.Add(m);
                            Out($"{tag} OK [APPENDUID {target.UidValidity} {m.Uid}] APPEND\r\n"); break;
                        }
                        default: Out($"{tag} BAD unbekannter Befehl\r\n"); break;
                    }
                }
            }
            catch (Exception e) when (e is FormatException or ArgumentOutOfRangeException or InvalidCastException or IndexOutOfRangeException) { chunks.Clear(); Out($"{tag} BAD {e.GetType().Name}\r\n"); }
            await w.Send(chunks.SelectMany(c => c).ToArray(), ct);
            if (cmd == "LOGOUT") break;
        }
    }

    // ------------------------------------------------------------ SMTP
    async Task Smtp(Stream raw)
    {
        var ct = _cts.Token; var w = new Wire(raw); var tls = false; var authed = false; var from = ""; var to = new List<string>();
        await w.Send("220 localhost Trendo-Testserver ESMTP\r\n", ct);
        void Log(string l) { lock (SmtpLog) SmtpLog.Add(l); }
        bool Check(string user, string pw) => user == User && pw == Password;
        while (await w.Line(ct) is { } line)
        {
            var up = line.ToUpperInvariant();
            Log(up.StartsWith("AUTH PLAIN ", StringComparison.Ordinal) ? "AUTH PLAIN ***" : line);
            if (up.StartsWith("EHLO", StringComparison.Ordinal))
                await w.Send($"250-localhost\r\n{(Certificate is not null && !tls ? "250-STARTTLS\r\n" : "")}250-AUTH PLAIN LOGIN\r\n250 8BITMIME\r\n", ct);
            else if (up == "STARTTLS" && Certificate is not null && !tls)
            {
                await w.Send("220 los\r\n", ct);
                var ssl = new SslStream(w.S, false); await ssl.AuthenticateAsServerAsync(Certificate, false, false);
                w = new Wire(ssl); tls = true;
            }
            else if (up.StartsWith("AUTH", StringComparison.Ordinal) && Certificate is not null && !tls) await w.Send("530 erst STARTTLS\r\n", ct);
            else if (up.StartsWith("AUTH PLAIN", StringComparison.Ordinal))
            {
                var parts = Encoding.UTF8.GetString(Convert.FromBase64String(line[10..].Trim())).Split('\0');
                authed = parts.Length == 3 && Check(parts[1], parts[2]);
                await w.Send(authed ? "235 angemeldet\r\n" : "535 falsches Passwort\r\n", ct);
            }
            else if (up == "AUTH LOGIN")
            {
                await w.Send("334 VXNlcm5hbWU6\r\n", ct); var u = Encoding.UTF8.GetString(Convert.FromBase64String(await w.Line(ct) ?? ""));
                await w.Send("334 UGFzc3dvcmQ6\r\n", ct); var p = Encoding.UTF8.GetString(Convert.FromBase64String(await w.Line(ct) ?? ""));
                Log("(Anmeldedaten)");
                authed = Check(u, p); await w.Send(authed ? "235 angemeldet\r\n" : "535 falsches Passwort\r\n", ct);
            }
            else if (up.StartsWith("MAIL FROM:", StringComparison.Ordinal))
            {
                if (!authed) { await w.Send("530 erst anmelden\r\n", ct); continue; }
                from = Regex.Match(line, "<([^>]*)>").Groups[1].Value; to.Clear(); await w.Send("250 ok\r\n", ct);
            }
            else if (up.StartsWith("RCPT TO:", StringComparison.Ordinal))
            {
                var adr = Regex.Match(line, "<([^>]*)>").Groups[1].Value;
                if (adr.EndsWith("@abgelehnt.example", StringComparison.OrdinalIgnoreCase)) await w.Send("550 Empfänger unbekannt\r\n", ct);
                else { to.Add(adr); await w.Send("250 ok\r\n", ct); }
            }
            else if (up == "DATA")
            {
                if (to.Count == 0) { await w.Send("503 kein Empfänger\r\n", ct); continue; }
                await w.Send("354 los\r\n", ct);
                var data = new List<byte>();
                while (await w.LineBytes(ct) is { } l)
                {
                    if (l.Length == 1 && l[0] == '.') break;
                    data.AddRange(l.Length > 0 && l[0] == '.' ? l[1..] : l); data.Add((byte)'\r'); data.Add((byte)'\n');
                }
                var msg = data.ToArray();
                lock (_lock) Accepted.Add((from, [.. to], msg));
                if (to.Any(t => t.Equals(User, StringComparison.OrdinalIgnoreCase))) Deliver("INBOX", msg);
                await w.Send("250 angenommen\r\n", ct);
            }
            else if (up == "QUIT") { await w.Send("221 bis bald\r\n", ct); break; }
            else if (up is "RSET" or "NOOP") await w.Send("250 ok\r\n", ct);
            else await w.Send("500 unbekannt\r\n", ct);
        }
    }

    // ------------------------------------------------------------ Hilfen für Tests
    /// <summary>Verbindung zum IMAP-Dienst; bei TLS wird nur genau dieses Testzertifikat angenommen.</summary>
    public async Task<Stream> ConnectImap(CancellationToken ct = default)
    {
        var tcp = new TcpClient(); await tcp.ConnectAsync(IPAddress.Loopback, ImapPort, ct);
        return Certificate is null ? tcp.GetStream() : await ClientTls(tcp.GetStream());
    }

    public async Task<Stream> ConnectSmtp(CancellationToken ct = default) { var tcp = new TcpClient(); await tcp.ConnectAsync(IPAddress.Loopback, SmtpPort, ct); return tcp.GetStream(); }

    /// <summary>TLS als Client aufbauen; vertraut wird ausschließlich dem Zertifikat dieses Testservers (Fingerabdruck-Vergleich).</summary>
    public async Task<Stream> ClientTls(Stream inner)
    {
        var ssl = new SslStream(inner, false, (_, cert, _, _) => cert is not null && cert.GetCertHashString() == Certificate!.GetCertHashString());
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost" });
        return ssl;
    }
}
