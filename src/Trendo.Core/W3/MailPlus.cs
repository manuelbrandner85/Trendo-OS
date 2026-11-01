using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Posteingang als Mailprogramm (Masterprompt 131, Runde 7): Entwürfe und Gesendetes lokal, Unterhaltungen, „Allen antworten“, Zitat beim Antworten.
/// Senden bleibt wie bisher nur nach ausdrücklicher Bestätigung (Posteingang-Seite); hier nur Regeln und Ablage.</summary>
public sealed partial class MailFolders
{
    public sealed record Stored(string Id, string To, string Cc, string Subject, string Body, DateTimeOffset At);
    sealed record Saved(List<Stored> Drafts, List<Stored> Sent);

    readonly string _file;
    public List<Stored> Drafts { get; } = [];
    public List<Stored> Sent { get; } = [];

    public MailFolders(string dataRoot)
    {
        _file = Path.Combine(dataRoot, "post", "ordner.json");
        try { if (File.Exists(_file) && JsonSerializer.Deserialize<Saved>(File.ReadAllText(_file)) is { } s) { Drafts.AddRange(s.Drafts); Sent.AddRange(s.Sent); } }
        catch (Exception e) when (e is JsonException or IOException) { }
    }

    void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file + ".tmp", JsonSerializer.Serialize(new Saved(Drafts, Sent))); SafeFile.Move(_file + ".tmp", _file);
    }

    /// <summary>Entwurf speichern (neu oder vorhandenen ersetzen). Leere Entwürfe werden nicht gespeichert.</summary>
    public Stored? SaveDraft(MailDraft d, string? id = null)
    {
        if (d.To.Trim().Length == 0 && d.Subject.Trim().Length == 0 && d.Body.Trim().Length == 0) { if (id is not null) DeleteDraft(id); return null; }
        var s = new Stored(id ?? Guid.NewGuid().ToString("N")[..10], d.To, d.Cc, d.Subject, d.Body, DateTimeOffset.Now);
        Drafts.RemoveAll(x => x.Id == s.Id); Drafts.Insert(0, s); Save(); return s;
    }

    public void DeleteDraft(string id) { if (Drafts.RemoveAll(x => x.Id == id) > 0) Save(); }

    /// <summary>Nach dem Senden: in „Gesendet“ ablegen (höchstens 500, älteste fallen weg) und den Entwurf entfernen.</summary>
    public Stored AddSent(MailDraft d, string? draftId = null)
    {
        var s = new Stored(Guid.NewGuid().ToString("N")[..10], d.To, d.Cc, d.Subject, d.Body, DateTimeOffset.Now);
        Sent.Insert(0, s); if (Sent.Count > 500) Sent.RemoveRange(500, Sent.Count - 500);
        if (draftId is not null) Drafts.RemoveAll(x => x.Id == draftId);
        Save(); return s;
    }

    [GeneratedRegex(@"^\s*((re|aw|wg|fw|fwd|antw|sv|vs)\s*(\[\d+\])?\s*:\s*)+", RegexOptions.IgnoreCase)] private static partial Regex Prefix();
    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+")] private static partial Regex Address();

    /// <summary>Schlüssel einer Unterhaltung: Betreff ohne „Re:“, „AW:“, „WG:“, „Fwd:“ usw., klein geschrieben.</summary>
    public static string ThreadKey(string subject) => Regex.Replace(Prefix().Replace(subject, ""), @"\s+", " ").Trim().ToLowerInvariant();

    /// <summary>Kopfzeilen in Unterhaltungen gruppieren (neueste Unterhaltung zuerst, innen neueste zuerst).</summary>
    public static IReadOnlyList<(string Key, IReadOnlyList<MailHeader> Mails)> Threads(IEnumerable<MailHeader> mails) =>
        mails.GroupBy(m => ThreadKey(m.Subject)).Select(g => (g.Key, (IReadOnlyList<MailHeader>)g.OrderByDescending(m => m.Date).ToList()))
            .OrderByDescending(t => t.Item2[0].Date).ToList();

    /// <summary>„Allen antworten“: an den Absender, Kopie an alle übrigen Empfänger – ohne die eigene Adresse und ohne Doppelte.</summary>
    public static (string To, string Cc) ReplyAll(ParsedMail m, string ownEmail)
    {
        static IEnumerable<string> Adr(string s) => Address().Matches(s).Select(x => x.Value.ToLowerInvariant());
        var own = ownEmail.Trim().ToLowerInvariant();
        var to = Adr(m.From).FirstOrDefault() ?? "";
        var cc = Adr(m.To).Concat(Adr(m.Cc)).Where(a => a != own && a != to).Distinct().ToList();
        return (to, string.Join(", ", cc));
    }

    /// <summary>Antwort-Text mit Zitat („Am … schrieb …:“ und „> “ vor jeder Zeile).</summary>
    public static string Quote(ParsedMail m) =>
        $"\n\nAm {m.Date} schrieb {m.From}:\n" + string.Join("\n", m.Body.Replace("\r", "").Split('\n').Take(200).Select(l => "> " + l));

    /// <summary>Betreff für Antwort („AW: “) bzw. Weiterleitung („WG: “), ohne Präfixe zu stapeln.</summary>
    public static string ReplySubject(string subject, bool forward) => (forward ? "WG: " : "AW: ") + Prefix().Replace(subject, "").Trim();
}
