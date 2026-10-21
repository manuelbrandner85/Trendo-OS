using System.Text.RegularExpressions;

namespace Trendo.Core;

public enum CheckLevel { Gut, Hinweis, Risiko }
public sealed record SecurityCheck(string Id, string Title, CheckLevel Level, string Detail, string? FixPage = null);
public sealed record PrivacyFinding(string File, int Line, string Kind, string Masked);

/// <summary>CORE-11 Security Privacy Recovery Fortress: prüft Schutz (Protokoll, Schlüsselspeicher, Sicherungen, Offline, Freigaben),
/// sucht in Ordnern nach offen liegenden Passwörtern/Schlüsseln/IBAN/Kartennummern (nur lesen, maskiert), Wiederherstellungs-Status.</summary>
public static partial class SecurityCenter
{
    public static IReadOnlyList<SecurityCheck> Checks(TrendoCore core, Backup backup, DateTime now)
    {
        var l = new List<SecurityCheck>();
        l.Add(core.Audit.Verify() ? new("audit", "Protokoll", CheckLevel.Gut, "Vollständig und unverändert.")
                                  : new("audit", "Protokoll", CheckLevel.Risiko, "Das Protokoll wurde verändert. Prüfe, wer Zugriff auf deinen PC hat.", "system"));
        var persistent = !core.Secrets.BackendName.StartsWith("InMemory");
        l.Add(persistent ? new("secrets", "Sicher gespeicherte Schlüssel", CheckLevel.Gut, $"Liegen im {core.Secrets.BackendName}.")
                         : new("secrets", "Sicher gespeicherte Schlüssel", CheckLevel.Hinweis, "Schlüssel werden nur bis zum Beenden gemerkt (kein sicherer Speicher gefunden)."));
        var last = backup.List().FirstOrDefault();
        l.Add(last is null ? new("backup", "Sicherung", CheckLevel.Risiko, "Es gibt noch keine Sicherung.", "sicherung")
            : (now - last.LastWriteTime).TotalDays > 7 ? new("backup", "Sicherung", CheckLevel.Hinweis, $"Letzte Sicherung ist {(int)(now - last.LastWriteTime).TotalDays} Tage alt.", "sicherung")
            : new("backup", "Sicherung", CheckLevel.Gut, $"Letzte Sicherung: {last.LastWriteTime:dd.MM.yyyy HH:mm} ({backup.List().Count} vorhanden)."));
        var pend = core.Approvals.Pending().Count;
        l.Add(pend == 0 ? new("approvals", "Freigaben", CheckLevel.Gut, "Nichts wartet auf deine Erlaubnis.") : new("approvals", "Freigaben", CheckLevel.Hinweis, $"{pend} Anfrage(n) warten auf dich.", "approvals"));
        l.Add(core.Offline ? new("offline", "Internet", CheckLevel.Gut, "Offline-Modus: Trendo geht nicht ins Internet.") : new("offline", "Internet", CheckLevel.Gut, "Trendo geht nur nach deiner Erlaubnis ins Internet. Die KI läuft auf diesem PC."));
        var denied = core.Audit.ReadAll().Count(e => e.Action is "rbac.denied" or "execution.blocked");
        l.Add(denied == 0 ? new("blocked", "Abgewehrte Aktionen", CheckLevel.Gut, "Keine.") : new("blocked", "Abgewehrte Aktionen", CheckLevel.Hinweis, $"{denied} Aktion(en) ohne Erlaubnis wurden gestoppt – siehe Protokoll.", "system"));
        return l;
    }

    public static int Score(IReadOnlyList<SecurityCheck> c) => c.Count == 0 ? 0 : (int)Math.Round(100.0 * c.Sum(x => x.Level switch { CheckLevel.Gut => 2, CheckLevel.Hinweis => 1, _ => 0 }) / (2.0 * c.Count));

    [GeneratedRegex(@"(?i)\b(passwor[td]|password|pwd|kennwort|pin)\s*[:=]\s*(\S{4,})")] private static partial Regex Pw();
    [GeneratedRegex(@"(?i)\b(api[_-]?key|secret|token|access[_-]?key)\s*[:=]\s*[""']?([A-Za-z0-9_\-\.]{12,})")] private static partial Regex Key();
    [GeneratedRegex(@"\b(sk-[A-Za-z0-9]{20,}|ghp_[A-Za-z0-9]{30,}|xai-[A-Za-z0-9]{20,}|AKIA[0-9A-Z]{16})\b")] private static partial Regex Known();
    [GeneratedRegex(@"\b([A-Z]{2}\d{2}(?: ?[A-Z0-9]{4}){3,7}(?: ?[A-Z0-9]{1,3})?)\b")] private static partial Regex Iban();
    [GeneratedRegex(@"\b(?:\d[ -]?){13,19}\b")] private static partial Regex Card();

    private static readonly HashSet<string> TextExt = [".txt", ".md", ".csv", ".json", ".env", ".ini", ".cfg", ".conf", ".yaml", ".yml", ".xml", ".log", ".ps1", ".sh", ".py", ".js", ".ts", ".cs"];

    /// <summary>Nur lesen. Treffer werden maskiert, nie im Klartext gespeichert oder angezeigt.</summary>
    public static IReadOnlyList<PrivacyFinding> ScanFolder(string folder, int maxFiles = 2000, long maxBytes = 1_000_000)
    {
        var res = new List<PrivacyFinding>();
        if (!Directory.Exists(folder)) return res;
        var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint };
        foreach (var f in Directory.EnumerateFiles(folder, "*", opts).Where(f => TextExt.Contains(Path.GetExtension(f).ToLowerInvariant()) || Path.GetFileName(f) == ".env").Take(maxFiles))
        {
            try
            {
                if (new FileInfo(f).Length > maxBytes) continue;
                var n = 0;
                foreach (var line in File.ReadLines(f))
                {
                    n++;
                    foreach (Match m in Pw().Matches(line)) res.Add(new(f, n, "Passwort", Mask(m.Groups[2].Value)));
                    foreach (Match m in Key().Matches(line)) res.Add(new(f, n, "Schlüssel", Mask(m.Groups[2].Value)));
                    foreach (Match m in Known().Matches(line)) if (!res.Any(r => r.File == f && r.Line == n)) res.Add(new(f, n, "Schlüssel", Mask(m.Value)));
                    foreach (Match m in Iban().Matches(line)) if (IbanOk(m.Value)) res.Add(new(f, n, "IBAN", Mask(m.Value.Replace(" ", ""))));
                    foreach (Match m in Card().Matches(line)) { var d = new string(m.Value.Where(char.IsDigit).ToArray()); if (d.Length is >= 13 and <= 19 && Luhn(d)) res.Add(new(f, n, "Kartennummer", Mask(d))); }
                }
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return res;
    }

    public static string Mask(string s) => s.Length <= 4 ? "****" : s[..2] + new string('•', Math.Min(8, s.Length - 4)) + s[^2..];

    public static bool IbanOk(string iban)
    {
        var s = iban.Replace(" ", "").ToUpperInvariant();
        if (s.Length < 15 || s.Length > 34) return false;
        var r = s[4..] + s[..4]; var mod = 0;
        foreach (var ch in r) { var v = char.IsDigit(ch) ? ch - '0' : char.IsLetter(ch) ? ch - 'A' + 10 : -1; if (v < 0) return false; mod = (v > 9 ? mod * 100 + v : mod * 10 + v) % 97; }
        return mod == 1;
    }

    public static bool Luhn(string d)
    {
        int sum = 0; var alt = false;
        for (var i = d.Length - 1; i >= 0; i--) { var x = d[i] - '0'; if (alt) { x *= 2; if (x > 9) x -= 9; } sum += x; alt = !alt; }
        return sum % 10 == 0;
    }
}
