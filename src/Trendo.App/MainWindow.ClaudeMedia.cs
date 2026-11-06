using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Bibliothek;

namespace Trendo.App;

/// <summary>Freie Bilder & Klänge – Claude als Rechercheur (Claude 28, 05.11.2026): Claude sucht passend zum Projekt, prüft jede Lizenz und
/// legt passende Treffer in eine Merkliste – die Namensnennungen schreibt Trendo dabei automatisch (Datei und Zwischenablage).</summary>
public partial class MainWindow
{
    const string MediaClaudeSearch = "mb-claude-suche", MediaClaudeCollect = "mb-claude-merken";

    IEnumerable<ClaudeActions.Field> ClaudeMediaFields()
    {
        yield return new(MediaClaudeSearch, "Suchwort für freie " + (MediaTab == "klaenge" ? "Klänge" : "Bilder") + " (Deutsch oder Englisch)", _mbQuery);
        yield return new(MediaClaudeCollect, "Merken: „Merkliste: Nr, Nr, …“ (Nummern aus der Trefferliste)", "");
    }

    string ClaudeMediaFocus()
    {
        var sb = new System.Text.StringBuilder("Freie Bilder & Klänge – du bist Rechercheur für das Projekt des Nutzers. ");
        sb.Append($"Bereich: {(MediaTab == "klaenge" ? "Klänge" : MediaTab == "bilder" ? "Bilder" : MediaTab)}; Suche „{_mbQuery}“; Filter Lizenz {_mbLicense}, Quelle {MediaProvider}.");
        if (MediaHits.Count > 0)
        {
            sb.Append("\nTreffer (Nr · Titel · Urheber · Lizenz · Größe):");
            for (var i = 0; i < MediaHits.Count && i < 24; i++)
            {
                var h = MediaHits[i];
                sb.Append($"\n{i + 1} · {h.Title} · {(h.Creator.Length > 0 ? h.Creator : "unbekannt")} · {h.License}{(h.Free ? " (frei)" : " (Namensnennung nötig)")} · " + (h.Width > 0 ? $"{h.Width}×{h.Height}" : h.Seconds > 0 ? $"{h.Seconds:0.#} s" : h.Format));
            }
        }
        else sb.Append("\nNoch keine Treffer.");
        var lists = Collections.All();
        if (lists.Count > 0) sb.Append("\nMerklisten: " + string.Join("; ", lists.Select(c => $"{c.Name} ({c.Entries.Count})")));
        sb.Append($"\nSo arbeitest du: erst passend zum Projekt suchen mit [[trendo:feld | {MediaClaudeSearch} | Suchwort]], dann die passenden Treffer prüfen (Lizenz, Größe, Inhalt) und merken mit [[trendo:feld | {MediaClaudeCollect} | Projektname: 1, 4, 7]].");
        sb.Append(" Nur freie Lizenzen (CC0, gemeinfrei, CC BY, CC BY-SA) – Trendo lehnt alles andere ab; die Namensnennung schreibt Trendo automatisch. Laden in die Bibliothek bestätigt der Nutzer.");
        return sb.ToString();
    }

    /// <summary>„Liste: 1, 4, 7“ lesen.</summary>
    (string? List, List<int>? Nums, string? Error) ParseCollect(string v)
    {
        var colon = v.LastIndexOf(':'); if (colon < 1) return (null, null, "Form „Merkliste: 1, 4, 7“");
        var name = v[..colon].Trim(); var nums = new List<int>();
        foreach (var p in v[(colon + 1)..].Split(',', ' ', ';'))
        {
            if (p.Trim().Length == 0) continue;
            if (!int.TryParse(p.Trim(), out var n) || n < 1 || n > MediaHits.Count) return (null, null, $"Nummer „{p.Trim()}“ gibt es in der Trefferliste nicht (1–{MediaHits.Count})");
            if (!nums.Contains(n)) nums.Add(n);
        }
        if (name.Length == 0 || nums.Count == 0) return (null, null, "Name oder Nummern fehlen");
        return (name, nums, null);
    }

    Control? ClaudeMediaPreview(ClaudeActions.Step s)
    {
        if (s.Kind != "feld" || s.Value is not { Length: > 0 } v) return null;
        TextBlock Warn(string t) => new() { Name = "claude-plan-warn", Text = "⚠ So nicht möglich: " + t, Foreground = Brush.Parse("#FFF8B26A"), FontSize = 12, TextWrapping = TextWrapping.Wrap };
        if (s.Target == MediaClaudeSearch) return v.Trim().Length < 2 ? Warn("zu kurzes Suchwort") : null;
        if (s.Target != MediaClaudeCollect) return null;
        var (list, nums, err) = ParseCollect(v); if (list is null) return Warn(err!);
        var sp = new StackPanel { Name = "claude-plan-media", Spacing = 2 };
        sp.Children.Add(new TextBlock { Text = $"In „{list}“:", Foreground = ClaudeFg, FontSize = 12 });
        foreach (var n in nums!)
        {
            var h = MediaHits[n - 1]; var lic = h.License.ToLowerInvariant();
            var ok = !System.Text.RegularExpressions.Regex.IsMatch(lic, @"(^|[\s\-])(nc|nd)([\s\-]|$)");
            sp.Children.Add(new TextBlock { Text = $"{(ok ? (h.Free ? "✓" : "✓ nennen") : "✕")} {n}. {h.Title} – {h.License}", Foreground = ok ? ClaudeFg : Brush.Parse("#FFF8B26A"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        }
        return sp;
    }

    bool ClaudeMediaApply(ClaudeActions.Step s)
    {
        if (s.Kind != "feld" || s.Value is not { Length: > 0 } v) return false;
        if (s.Target == MediaClaudeSearch) { if (v.Trim().Length < 2) return false; _ = MediaSearch(v.Trim()); return true; }
        if (s.Target != MediaClaudeCollect) return false;
        var (list, nums, err) = ParseCollect(v); if (list is null) { _mbMsg = "⚠ Claudes Auswahl nicht übernommen: " + err + "."; return false; }
        var added = 0; var refused = new List<string>();
        foreach (var n in nums!)
        {
            try { if (Collections.Add(list, MediaHits[n - 1])) added++; } catch (InvalidOperationException e) { refused.Add(e.Message); }
        }
        if (added == 0 && refused.Count > 0) { _mbMsg = "⚠ " + string.Join(" ", refused); return false; }
        // Namensnennungen automatisch: Datei in der Medien-Bibliothek und Zwischenablage
        var credits = Collections.Credits(list);
        var file = Path.Combine(AssetFolder, "Medien", $"Namensnennungen – {string.Concat(list.Split(Path.GetInvalidFileNameChars()))}.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, credits);
        LastCopied = credits; try { _ = Clipboard?.SetTextAsync(credits); } catch (Exception) { }
        _mbListSel = list; _mbList = list;
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.medien.merken", $"{list}: {added}");
        _mbMsg = $"✳ Claude hat {added} Treffer in „{list}“ gemerkt – Namensnennungen stehen in „{Path.GetFileName(file)}“ und in der Zwischenablage." + (refused.Count > 0 ? " Abgelehnt: " + string.Join(" ", refused) : "");
        return true;
    }
}
