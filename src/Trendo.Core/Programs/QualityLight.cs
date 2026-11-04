using System.Text.RegularExpressions;

namespace Trendo.Core.Programs;

/// <summary>Systemumbau 2, A1 (freigegeben 03.11.2026): Qualitäts-Ampel je Programm – aus dem Katalog belegt, nicht behauptet.
/// Bewertet Arbeitsfläche (echtes Programm statt Formular), Vorbild-Analyse, Funktionskatalog, Tests, Leistung (warm geöffnet) und ob es
/// unter Trendo OS (Linux) läuft. Reihenfolge der Umbauten: schwächstes Programm zuerst.</summary>
public static class QualityLight
{
    public sealed record Rating(string Light, int Score, IReadOnlyList<string> Missing, string Linux);

    /// <summary>Windows-gebundene Stellen im Quelltext (Name, Muster). Treffer heißt: unter Trendo OS (Linux) nur eingeschränkt.</summary>
    public static readonly (string Name, string Pattern)[] WindowsOnlyPatterns =
    [
        ("PowerShell", @"""powershell(\.exe)?"""), ("Windows-Explorer", @"""explorer(\.exe)?"""), ("winget", @"""winget(\.exe)?"""),
        ("Registry", @"Microsoft\.Win32\.Registry|Registry\.(CurrentUser|LocalMachine)"), ("cmd.exe", @"""cmd(\.exe)?"""),
        ("Windows-Dienste (WMI/CIM)", @"Get-CimInstance|Win32_\w+|ManagementObjectSearcher"), ("Windows-Laufwerke (\\\\.\\PhysicalDrive)", @"PhysicalDrive"),
    ];

    /// <summary>Windows-gebundene Stellen in einem Quelltext; Stellen mit eigenem Linux-Weg im selben Text zählen nicht.</summary>
    public static List<string> WindowsOnly(string source)
    {
        var res = new List<string>();
        foreach (var (name, p) in WindowsOnlyPatterns)
            if (Regex.IsMatch(source, p, RegexOptions.IgnoreCase)) res.Add(name);
        // eigener Linux-Weg vorhanden (z. B. xdg-open statt explorer, lsblk statt Get-Disk): dann nur ein Hinweis, kein Mangel
        if (Regex.IsMatch(source, @"xdg-open|IsLinux\(\)|""/usr/bin/|lsblk", RegexOptions.IgnoreCase)) res.RemoveAll(n => n is "Windows-Explorer" or "PowerShell" or "cmd.exe");
        return res;
    }

    public static Rating Rate(ProgramEntry e)
    {
        var missing = new List<string>();
        var score = 0;
        if (e.Type.Count > 0) score += 30; else missing.Add("keine eigene Arbeitsfläche (noch Formular)");
        if (e.Rea is not null) score += 15; else missing.Add("keine Vorbild-Analyse");
        if (e.Functions.Count > 0)
        {
            var done = e.Functions.Count(f => Regex.IsMatch(f.State, "fertig|umgesetzt|✓|ja|done|implemented|tested", RegexOptions.IgnoreCase));
            score += (int)Math.Round(15.0 * done / e.Functions.Count);
            if (done < e.Functions.Count) missing.Add($"{e.Functions.Count - done} von {e.Functions.Count} Vorbild-Funktionen offen");
        }
        else missing.Add("kein Funktionskatalog");
        if (e.Tests.Count >= 8) score += 20; else if (e.Tests.Count >= 4) { score += 10; missing.Add($"nur {e.Tests.Count} Tests"); } else missing.Add($"nur {e.Tests.Count} Tests");
        if (e.WarmMs is { } ms)
        {
            if (ms <= 150) score += 10; else if (ms <= 250) { score += 5; missing.Add($"öffnet in {ms:0} ms (Ziel ≤ 150)"); } else missing.Add($"öffnet langsam ({ms:0} ms)");
        }
        else score += 5; // nicht gemessen: neutral
        var linux = e.WindowsOnly.Count == 0 ? "ja" : "eingeschränkt";
        if (e.WindowsOnly.Count == 0) score += 10; else missing.Add("unter Trendo OS eingeschränkt: " + string.Join(", ", e.WindowsOnly));
        var light = e.Type.Count == 0 || e.Tests.Count < 4 ? "rot" : score >= 85 ? "grün" : "gelb";
        return new Rating(light, score, missing, linux);
    }

    /// <summary>Alle Programme, schwächstes zuerst (Reihenfolge des Umbaus).</summary>
    public static List<(ProgramEntry Entry, Rating Rating)> Ranked(IEnumerable<ProgramEntry> all) =>
        all.Select(e => (e, Rate(e))).OrderBy(x => x.Item2.Light == "rot" ? 0 : x.Item2.Light == "gelb" ? 1 : 2).ThenBy(x => x.Item2.Score).ThenBy(x => x.e.Name, StringComparer.Ordinal).ToList();
}
