using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Adressbuch (Masterprompt 131) im vorhandenen Kontaktspeicher: ändern, Gruppen, Favoriten, Fotos, Suche über alle Felder,
/// Doppelte finden und zusammenführen, Geburtstage, vCard 3.0 mit allen Feldern (auch Foto) hinein und hinaus.</summary>
public sealed partial class MailCenter
{
    public Contact Get(string id) => _contacts.FirstOrDefault(c => c.Id == id) ?? throw new InvalidOperationException("Diesen Kontakt gibt es nicht.");

    /// <summary>Kontakt speichern (neu oder geändert). Prüft Name und E-Mail; Gruppen ohne Leere/Doppelte.</summary>
    public Contact UpdateContact(Contact c)
    {
        if (string.IsNullOrWhiteSpace(c.Name)) throw new ArgumentException("Name fehlt.");
        if (c.Email.Trim().Length > 0 && !ValidEmail(c.Email)) throw new ArgumentException("Die E-Mail-Adresse sieht nicht richtig aus.");
        var groups = c.Groups?.Select(g => g.Trim()).Where(g => g.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
        c = c with { Name = c.Name.Trim(), Email = c.Email.Trim(), Groups = groups is { Count: > 0 } ? groups : null };
        var i = _contacts.FindIndex(x => x.Id == c.Id); if (i >= 0) _contacts[i] = c; else _contacts.Add(c);
        Save(); return c;
    }

    public IReadOnlyList<(string Name, int Count)> GroupNames() => _contacts.SelectMany(c => c.Groups ?? []).GroupBy(g => g, StringComparer.CurrentCultureIgnoreCase)
        .Select(g => (g.First(), g.Count())).OrderBy(g => g.Item1, StringComparer.CurrentCultureIgnoreCase).ToList();

    /// <summary>Suche in allen Feldern (Name, Firma, E-Mail, Telefon – auch nur Ziffern –, Ort, Notiz, Gruppen).</summary>
    public IReadOnlyList<Contact> Search(string q, string? group = null)
    {
        var digits = new string(q.Where(char.IsDigit).ToArray());
        return Contacts().Where(c => group is null || (c.Groups ?? []).Contains(group, StringComparer.CurrentCultureIgnoreCase))
            .Where(c => q.Trim().Length == 0 || string.Join(" ", c.Name, c.Company, c.Email, c.Phone, c.Mobile, c.Street, c.City, c.Note, string.Join(" ", c.Groups ?? [])).Contains(q.Trim(), StringComparison.CurrentCultureIgnoreCase)
                || digits.Length >= 4 && (Digits(c.Phone).Contains(digits) || Digits(c.Mobile).Contains(digits))).ToList();
    }
    static string Digits(string s) => new(s.Where(char.IsDigit).ToArray());

    static string NameKey(string n) => string.Join(' ', n.ToLowerInvariant().Split([' ', ',', '.', '-'], StringSplitOptions.RemoveEmptyEntries).Order());
    static string PhoneKey(string p) { var d = Digits(p); if (d.StartsWith("49")) d = d[2..]; d = d.TrimStart('0'); return d.Length >= 6 ? d : ""; }

    /// <summary>Vermutlich doppelte Kontakte: gleicher Name (auch „Muster, Erika“), gleiche E-Mail oder gleiche Telefonnummer (+49/0 egal).</summary>
    public List<List<Contact>> Duplicates()
    {
        var list = Contacts().ToList(); var parent = Enumerable.Range(0, list.Count).ToArray();
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        var seen = new Dictionary<string, int>();
        for (var i = 0; i < list.Count; i++)
        {
            var c = list[i];
            foreach (var key in new[] { "n:" + NameKey(c.Name), c.Email.Length > 0 ? "e:" + c.Email.ToLowerInvariant() : "", PhoneKey(c.Phone) is { Length: > 0 } p ? "t:" + p : "", PhoneKey(c.Mobile) is { Length: > 0 } m ? "t:" + m : "" }.Where(k => k.Length > 2))
                if (seen.TryGetValue(key, out var j)) parent[Find(i)] = Find(j); else seen[key] = i;
        }
        return Enumerable.Range(0, list.Count).GroupBy(Find).Where(g => g.Count() > 1).Select(g => g.Select(i => list[i]).ToList()).ToList();
    }

    /// <summary>Zusammenführen: <paramref name="keepId"/> bleibt, leere Felder werden aus den anderen gefüllt, Gruppen und Notizen vereint, die anderen entfernt.</summary>
    public Contact Merge(string keepId, IEnumerable<string> otherIds)
    {
        var keep = Get(keepId); var others = otherIds.Where(id => id != keepId).Select(Get).ToList();
        string Fill(string a, Func<Contact, string> f) => a.Length > 0 ? a : others.Select(f).FirstOrDefault(x => x.Length > 0) ?? "";
        var notes = new[] { keep.Note }.Concat(others.Select(o => o.Note)).Where(n => n.Length > 0).Distinct().ToList();
        var merged = keep with
        {
            Email = Fill(keep.Email, o => o.Email), Phone = Fill(keep.Phone, o => o.Phone), Mobile = Fill(keep.Mobile, o => o.Mobile), Company = Fill(keep.Company, o => o.Company),
            Street = Fill(keep.Street, o => o.Street), City = Fill(keep.City, o => o.City), Photo = Fill(keep.Photo, o => o.Photo), Website = Fill(keep.Website, o => o.Website),
            Birthday = keep.Birthday ?? others.Select(o => o.Birthday).FirstOrDefault(b => b is not null), Favorite = keep.Favorite || others.Any(o => o.Favorite),
            Note = string.Join("\n", notes), Groups = (keep.Groups ?? []).Concat(others.SelectMany(o => o.Groups ?? [])).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList(),
        };
        _contacts.RemoveAll(c => others.Any(o => o.Id == c.Id));
        merged = UpdateContact(merged); _audit.Append(Principal.LocalOwner.Name, "contacts.merge", $"{others.Count + 1} → 1");
        return merged;
    }

    /// <summary>Nächste Geburtstage ab heute innerhalb von <paramref name="days"/> Tagen: Datum und Alter an dem Tag (29.2. → 28.2. in Nicht-Schaltjahren).</summary>
    public List<(Contact Contact, DateOnly Next, int Age)> Birthdays(DateOnly today, int days = 30) =>
        _contacts.Where(c => c.Birthday is not null).Select(c => { var n = NextBirthday(c.Birthday!.Value, today); return (c, n, n.Year - c.Birthday!.Value.Year); })
            .Where(x => x.n.DayNumber - today.DayNumber <= days).OrderBy(x => x.n).ToList();
    public static DateOnly NextBirthday(DateOnly b, DateOnly today)
    {
        DateOnly In(int y) => new(y, b.Month, b.Month == 2 && b.Day == 29 && !DateTime.IsLeapYear(y) ? 28 : b.Day);
        var n = In(today.Year); return n < today ? In(today.Year + 1) : n;
    }

    /// <summary>Foto speichern (unter kontakte-fotos) und am Kontakt merken.</summary>
    public Contact SetPhoto(string id, byte[] bytes, string ext)
    {
        var dir = Path.Combine(_root, "kontakte-fotos"); Directory.CreateDirectory(dir);
        var f = Path.Combine(dir, $"{id}{(ext.StartsWith('.') ? ext : "." + ext).ToLowerInvariant()}"); File.WriteAllBytes(f, bytes);
        return UpdateContact(Get(id) with { Photo = f });
    }

    // ---------- vCard 3.0 ----------
    static string V(string s) => s.Replace("\\", "\\\\").Replace(",", "\\,").Replace(";", "\\;").Replace("\r\n", "\\n").Replace("\n", "\\n");
    static string Fold(string line) { if (line.Length <= 75) return line + "\r\n"; var sb = new StringBuilder(line[..75]).Append("\r\n"); for (var i = 75; i < line.Length; i += 74) sb.Append(' ').Append(line[i..Math.Min(line.Length, i + 74)]).Append("\r\n"); return sb.ToString(); }

    public string ExportVcf(IEnumerable<Contact>? only = null) => string.Concat((only ?? Contacts()).Select(Vcard));
    public static string Vcard(Contact c)
    {
        var sb = new StringBuilder("BEGIN:VCARD\r\nVERSION:3.0\r\n");
        var parts = c.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        sb.Append(Fold($"N:{V(parts.Length > 1 ? parts[^1] : c.Name)};{V(parts.Length > 1 ? string.Join(' ', parts[..^1]) : "")};;;"));
        sb.Append(Fold("FN:" + V(c.Name)));
        if (c.Company.Length > 0) sb.Append(Fold("ORG:" + V(c.Company)));
        if (c.Email.Length > 0) sb.Append(Fold("EMAIL;TYPE=INTERNET:" + c.Email));
        if (c.Phone.Length > 0) sb.Append(Fold("TEL;TYPE=VOICE:" + V(c.Phone)));
        if (c.Mobile.Length > 0) sb.Append(Fold("TEL;TYPE=CELL:" + V(c.Mobile)));
        if (c.Street.Length + c.City.Length > 0) sb.Append(Fold($"ADR;TYPE=HOME:;;{V(c.Street)};{V(c.City)};;;"));
        if (c.Birthday is { } b) sb.Append($"BDAY:{b:yyyy-MM-dd}\r\n");
        if (c.Groups is { Count: > 0 } g) sb.Append(Fold("CATEGORIES:" + string.Join(",", g.Select(V))));
        if (c.Website.Length > 0) sb.Append(Fold("URL:" + c.Website));
        if (c.Note.Length > 0) sb.Append(Fold("NOTE:" + V(c.Note)));
        if (c.Photo.Length > 0 && File.Exists(c.Photo)) { var t = Path.GetExtension(c.Photo).ToLowerInvariant() == ".png" ? "PNG" : "JPEG"; sb.Append(Fold($"PHOTO;ENCODING=b;TYPE={t}:" + Convert.ToBase64String(File.ReadAllBytes(c.Photo)))); }
        return sb.Append("END:VCARD\r\n").ToString();
    }

    static string Un(string s) => Regex.Replace(s, @"\\([\\;,nN])", m => m.Groups[1].Value is "n" or "N" ? "\n" : m.Groups[1].Value);
    static List<string> SplitUn(string s, char sep) { var r = new List<string>(); var cur = new StringBuilder(); for (var i = 0; i < s.Length; i++) { if (s[i] == '\\' && i + 1 < s.Length) { cur.Append(s[i]).Append(s[++i]); continue; } if (s[i] == sep) { r.Add(Un(cur.ToString())); cur.Clear(); } else cur.Append(s[i]); } r.Add(Un(cur.ToString())); return r; }

    /// <summary>vCard (2.1/3.0/4.0) einlesen: alle bekannten Felder; schon vorhandene (gleicher Name und gleiche E-Mail) werden übersprungen. Gibt die Zahl neuer Kontakte zurück.</summary>
    public int ImportVcf(string vcf)
    {
        var n = 0;
        foreach (Match card in Regex.Matches(vcf.Replace("\r\n ", "").Replace("\n ", "").Replace("\r\n\t", "").Replace("\n\t", ""), @"BEGIN:VCARD(.*?)END:VCARD", RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
            var lines = card.Groups[1].Value.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Contains(':')).ToList();
            IEnumerable<(string Key, string Params, string Value)> Props() => lines.Select(l => { var c = l.IndexOf(':'); var head = l[..c]; var semi = head.IndexOf(';'); var key = (semi < 0 ? head : head[..semi]); key = key.Contains('.') ? key[(key.IndexOf('.') + 1)..] : key; return (key.ToUpperInvariant(), semi < 0 ? "" : head[(semi + 1)..].ToUpperInvariant(), l[(c + 1)..]); });
            string F(string key) => Props().Where(p => p.Key == key).Select(p => Un(p.Value).Trim()).FirstOrDefault() ?? "";
            var name = F("FN"); if (name == "") name = string.Join(" ", SplitUn(Props().FirstOrDefault(p => p.Key == "N").Value ?? "", ';').Where(x => x.Trim().Length > 0)).Trim();
            var mail = F("EMAIL");
            if (name == "" || _contacts.Any(c => c.Name == name && c.Email == mail)) continue;
            if (mail != "" && !ValidEmail(mail)) mail = "";
            var tels = Props().Where(p => p.Key == "TEL").ToList();
            var mobile = tels.FirstOrDefault(t => t.Params.Contains("CELL")).Value?.Trim() ?? "";
            var phone = tels.FirstOrDefault(t => !t.Params.Contains("CELL")).Value?.Trim() ?? (mobile.Length == 0 ? "" : "");
            var adr = Props().FirstOrDefault(p => p.Key == "ADR").Value is { } a ? SplitUn(a, ';') : [];
            DateOnly? bday = null; var bd = F("BDAY").Replace("-", "");
            if (bd.Length == 8 && DateOnly.TryParseExact(bd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) bday = d;
            var cats = Props().Where(p => p.Key == "CATEGORIES").SelectMany(p => SplitUn(p.Value, ',')).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            var c = new Contact(Guid.NewGuid().ToString("N")[..8], name, mail, Un(phone), F("NOTE"), Un(mobile), SplitUn(F("ORG"), ';')[0], adr.Count > 2 ? adr[2] : "", adr.Count > 3 ? adr[3] : "", bday, cats.Count > 0 ? cats : null, "", false, F("URL"));
            _contacts.Add(c); n++;
            if (Props().FirstOrDefault(p => p.Key == "PHOTO") is { Value: { Length: > 4 } pv } ph && !pv.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                try { var b64 = pv.Contains("base64,") ? pv[(pv.IndexOf("base64,") + 7)..] : pv; SetPhoto(c.Id, Convert.FromBase64String(b64.Trim()), ph.Params.Contains("PNG") ? ".png" : ".jpg"); } catch (FormatException) { }
        }
        Save(); _audit.Append(Principal.LocalOwner.Name, "contacts.import", $"{n}");
        return n;
    }
}
