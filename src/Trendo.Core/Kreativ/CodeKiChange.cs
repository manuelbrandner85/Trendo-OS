using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Trendo.Core.Programs;
using Trendo.Core.Sys;

namespace Trendo.Core.Kreativ;

/// <summary>T-0723 (D6 „Code mit KI“ echt machen): Die KI des einen Assistenzwegs (<see cref="AssistChain"/>, Antwort über <c>IntelligenceCore</c>)
/// schlägt Änderungen am offenen Projekt vor – als Plan und je Datei als vollständiger neuer Inhalt. Hier entsteht daraus eine nachvollziehbare Vorschau:
/// Zeilen-Diff je Datei (<see cref="CodeAiKit.Diff"/>), aufgeteilt in einzelne Änderungen (Blöcke), die die Person einzeln mit Ja/Nein annimmt.
/// Angewendet wird nur Angenommenes, nur im Projektordner (ohne verknüpfte Ordner, ohne Bau-/Git-Ordner), nur Textdateien. Jede Anwendung ist ein Schritt
/// mit „vorher“/„nachher“ je Datei – so macht ein Rückgängig alle Dateien des Schritts auf einmal wieder wie vorher. Kein eigener KI-Kern, kein Netz.</summary>
public static partial class CodeKiChange
{
    /// <summary>Höchstens so viele Zeichen Projektinhalt sieht die KI (die offene Datei zuerst, dann kleine Textdateien).</summary>
    public const int MaxContext = 24_000;
    /// <summary>Höchstens so viele Dateien darf ein Vorschlag ändern.</summary>
    public const int MaxFiles = 12;

    /// <summary>Feld im Dokument (.tdoc), in dem die angewendeten KI-Schritte stehen (nur Kennung, Wunsch und Dateinamen – die Inhalte liegen im Verlauf unter den Trendo-Daten).</summary>
    public const string StepsField = "kiSchritte";

    /// <summary>Dateien, die eine KI nie anfasst (Zugangsdaten, Schlüssel).</summary>
    static readonly string[] SecretNames = [".env", ".npmrc", ".pypirc", "id_rsa", "id_ed25519", ".netrc", "credentials", "secrets.json"];
    static readonly string[] SecretExt = [".pem", ".key", ".pfx", ".p12", ".kdbx"];

    // ───────────── Auftrag an die KI ─────────────

    /// <summary>Anweisung an die KI (Systemteil): Rolle, fester Antwortaufbau. Vollständige Dateien statt JSON – lokale Modelle verlieren in JSON-Texten sonst Zeilenumbrüche und Anführungszeichen.</summary>
    public static string SystemPrompt(string projectLabel, string? testCommand) =>
        $"Du bist eine erfahrene Programmiererin im Studio „Code mit KI“ von Trendo OS. Projektart: {projectLabel}." + (testCommand is null ? "" : $" Die Tests laufen mit „{testCommand}“.") + "\n"
        + "Du änderst Dateien NICHT selbst und führst nichts aus. Du schlägst Änderungen vor; die Person sieht sie als Unterschied und entscheidet je Änderung.\n"
        + "Antworte genau in diesem Aufbau und mit nichts anderem davor oder danach:\n"
        + "PLAN:\n- erster Schritt in einem Satz\n- weiterer Schritt …\n"
        + "DATEI: relativer/pfad/datei.ext\n```\nvollständiger neuer Inhalt der Datei\n```\n"
        + "Für jede Datei, die du änderst oder neu anlegst, genau einen DATEI-Block mit dem VOLLSTÄNDIGEN neuen Inhalt (keine Auslassungen wie „…“ oder „rest unverändert“). "
        + "Dateien ohne Änderung lässt du weg. Pfade relativ zum Projektordner, nur Dateien im Projekt. Behalte vorhandenen Code, Stil und Einrückung bei und ändere nur, was der Auftrag verlangt. "
        + "Wenn Tests gewünscht sind, schreib sie so, dass der Testbefehl des Projekts sie findet. Schreib den Plan auf Deutsch.";

    /// <summary>Der Auftrag mit Projektinhalt: Dateiliste, die offene Datei vollständig, weitere kleine Textdateien bis <see cref="MaxContext"/> Zeichen.</summary>
    public static string UserPrompt(string wish, IReadOnlyList<(string Rel, string Text)> files, string? openRel, IReadOnlyList<string> allFiles)
    {
        var sb = new StringBuilder($"Auftrag: {wish.Trim()}\n\n");
        sb.AppendLine("Dateien im Projekt:");
        foreach (var f in allFiles.Take(200)) sb.AppendLine("- " + f);
        if (allFiles.Count > 200) sb.AppendLine($"- … und {allFiles.Count - 200} weitere");
        sb.AppendLine();
        var used = 0;
        foreach (var (rel, text) in files.OrderBy(f => string.Equals(f.Rel, openRel, StringComparison.OrdinalIgnoreCase) ? 0 : 1))
        {
            if (used + text.Length > MaxContext && used > 0) { sb.AppendLine($"(Weitere Dateien nicht gezeigt – zu groß für einen Auftrag.)"); break; }
            sb.AppendLine($"DATEI: {rel}" + (string.Equals(rel, openRel, StringComparison.OrdinalIgnoreCase) ? " (gerade geöffnet)" : "")).AppendLine("```").AppendLine(text.TrimEnd('\n', '\r')).AppendLine("```").AppendLine();
            used += text.Length;
        }
        return sb.ToString();
    }

    // ───────────── Antwort lesen ─────────────

    public sealed record FileProposal(string Path, string Content);
    public sealed record Proposal(IReadOnlyList<string> Plan, IReadOnlyList<FileProposal> Files);

    [GeneratedRegex(@"^\s*(?:[#>*\-_]+\s*)*(?:\*\*)?\s*(?:DATEI|Datei|FILE|File)\s*:\s*(?:\*\*)?\s*`?(?<p>[^`*\r\n]+?)`?\s*(?:\*\*)?\s*$")] private static partial Regex FileLine();
    [GeneratedRegex(@"^\s*(?:\*\*)?\s*PLAN\s*:?\s*(?:\*\*)?\s*$", RegexOptions.IgnoreCase)] private static partial Regex PlanLine();
    [GeneratedRegex(@"^\s*(?:[-*•]|\d+[.)])\s+(?<t>.+)$")] private static partial Regex Bullet();
    [GeneratedRegex(@"^\s*(`{3,}|~{3,})")] private static partial Regex Fence();

    /// <summary>Antwort der KI lesen: Plan-Punkte und je DATEI-Block der Inhalt zwischen den Code-Zäunen. Denkteil (&lt;think&gt;) wird entfernt.
    /// Ein Block ohne schließenden Zaun gilt als abgeschnitten und wird verworfen (nie eine halbe Datei schreiben).</summary>
    public static Proposal Parse(string answer)
    {
        var lines = AssistChain.CleanAnswer(answer ?? "").Replace("\r\n", "\n").Split('\n');
        var plan = new List<string>(); var files = new List<FileProposal>();
        var inPlan = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            if (PlanLine().IsMatch(l)) { inPlan = true; continue; }
            if (FileLine().Match(l) is { Success: true } fm)
            {
                inPlan = false;
                var path = fm.Groups["p"].Value.Trim().Trim('"', '\'', '„', '“');
                // nächster Zaun (Leerzeilen davor erlaubt)
                var j = i + 1; while (j < lines.Length && lines[j].Trim().Length == 0) j++;
                if (j >= lines.Length || Fence().Match(lines[j]) is not { Success: true } open) continue;
                var fence = open.Groups[1].Value; var body = new List<string>(); var closed = false;
                for (j++; j < lines.Length; j++)
                {
                    if (lines[j].Trim() == fence || lines[j].TrimEnd() == fence) { closed = true; break; }
                    body.Add(lines[j]);
                }
                if (!closed) break; // abgeschnitten
                files.RemoveAll(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)); // doppelt: der letzte gilt
                files.Add(new(path, string.Join("\n", body) + "\n"));
                i = j; continue;
            }
            if (inPlan && Bullet().Match(l) is { Success: true } b) plan.Add(b.Groups["t"].Value.Trim());
            else if (inPlan && l.Trim().Length > 0 && plan.Count == 0) plan.Add(l.Trim());
        }
        return new(plan, files);
    }

    // ───────────── Projektgrenzen ─────────────

    /// <summary>Darf die KI an <paramref name="rel"/> schreiben? null = ja, sonst der Grund in einem Satz.
    /// Nur relative Pfade im Projektordner, keine verknüpften Ordner, keine Bau-/Git-Ordner (<see cref="CodeProject.Hidden"/>), keine Zugangsdaten, keine Binärdateien.</summary>
    public static string? Blocked(string root, string rel) => Blocked(Principal.LocalOwner, root, rel);

    /// <summary>T-0764 (13. Prüfrunde): wie <see cref="Blocked(string, string)"/>, dazu für <paramref name="by"/> die Freigabe (<see cref="ChildWrite.Problem"/>):
    /// im Kinderprofil nur Projektordner in der Freigabe und keine ausführbare oder auslösende Endung (start.bat, .ps1, .url …). Erwachsene: unverändert.</summary>
    public static string? Blocked(Principal by, string root, string rel)
    {
        if (string.IsNullOrWhiteSpace(rel)) return "Die KI hat keinen Dateinamen genannt.";
        var r = rel.Trim().Replace('\\', '/');
        // „:“ kommt in einem relativen Pfad nie vor – auch keine alternativen Datenströme („x.pem:stream“) und kein Laufwerk
        if (Path.IsPathRooted(r) || r.StartsWith('/') || r.Contains(':')) return $"„{rel}“ ist kein Pfad im Projekt (nur relative Pfade).";
        var parts = r.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(p => p is "." or "..")) return $"„{rel}“ führt aus dem Projektordner hinaus.";
        // T-0730: Windows schneidet Punkte und Leerzeichen am Namensende ab („geheim.env.“ wird „geheim.env“, „.git “ wird „.git“) –
        // geprüft wird deshalb der Name, wie Windows ihn anlegt; bleibt nichts übrig („...“, „ “), ist es kein gültiger Name.
        var norm = parts.Select(p => p.TrimEnd('.', ' ')).ToArray();
        if (norm.Any(p => p.Length == 0)) return $"„{rel}“ ist kein gültiger Dateiname.";
        if (norm.Any(p => CodeProject.Hidden.Contains(p))) return $"„{rel}“ liegt in einem Bau-, Paket- oder Git-Ordner – dort ändert die KI nichts.";
        var name = norm[^1];
        if (SecretNames.Contains(name, StringComparer.OrdinalIgnoreCase) || name.StartsWith(".env", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".env", StringComparison.OrdinalIgnoreCase) || SecretExt.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            return $"„{rel}“ kann Zugangsdaten enthalten – solche Dateien ändert die KI nie.";
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return $"„{rel}“ ist kein gültiger Dateiname.";
        var full = Path.GetFullPath(Path.Combine(root, r.Replace('/', Path.DirectorySeparatorChar)));
        if (!CodeAiKit.InsideNoLinks(root, full)) return $"„{rel}“ liegt nicht im Projektordner (oder hinter einem verknüpften Ordner).";
        if (ChildWrite.Problem(by, full) is { } kid) return $"„{rel}“: {kid}";
        if (Directory.Exists(full)) return $"„{rel}“ ist ein Ordner.";
        if (File.Exists(full))
        {
            var fi = new FileInfo(full);
            if (fi.Length > CodeAiKit.MaxTextBytes) return $"„{rel}“ ist zu groß für eine KI-Änderung.";
            var head = new byte[Math.Min(8000, (int)fi.Length)];
            using (var fs = File.OpenRead(full)) fs.ReadExactly(head);
            if (Array.IndexOf(head, (byte)0) >= 0) return $"„{rel}“ ist keine Textdatei.";
        }
        return null;
    }

    // ───────────── Vorschau: Änderungen je Datei, Blöcke mit Ja/Nein ─────────────

    /// <summary>Ein Änderungsblock: zusammenhängende Zeilen „weg“/„neu“ im Diff (Index der ersten und letzten Diff-Zeile).</summary>
    public sealed record Hunk(int From, int To, int Added, int Removed);

    /// <summary>Vorschau einer Datei. <paramref name="Before"/> null = neue Datei. <paramref name="BeforeSha"/>: Stand, auf den sich der Vorschlag bezieht
    /// (wurde die Datei danach geändert, wird nichts überschrieben).</summary>
    public sealed record FilePreview(string Path, string? Before, string After, string? BeforeSha, IReadOnlyList<CodeAiKit.Line> Diff, IReadOnlyList<Hunk> Hunks)
    {
        public bool IsNew => Before is null;
        public int Added => Hunks.Sum(h => h.Added);
        public int Removed => Hunks.Sum(h => h.Removed);
    }

    public sealed record Preview(IReadOnlyList<string> Plan, IReadOnlyList<FilePreview> Files, IReadOnlyList<string> Rejected);

    public static string ShaOf(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>Zusammenhängende Änderungen im Diff.</summary>
    public static IReadOnlyList<Hunk> Hunks(IReadOnlyList<CodeAiKit.Line> diff)
    {
        var list = new List<Hunk>();
        for (var i = 0; i < diff.Count; i++)
        {
            if (diff[i].Op == CodeAiKit.Op.Gleich) continue;
            var from = i; int add = 0, rem = 0;
            while (i < diff.Count && diff[i].Op != CodeAiKit.Op.Gleich) { if (diff[i].Op == CodeAiKit.Op.Neu) add++; else rem++; i++; }
            list.Add(new(from, i - 1, add, rem)); i--;
        }
        return list;
    }

    /// <summary>Vorschau aus dem Vorschlag bauen: Pfade prüfen, aktuellen Inhalt lesen, Diff und Blöcke. Unveränderte Dateien fallen weg, Abgelehnte stehen in <see cref="Preview.Rejected"/>.</summary>
    public static Preview Build(Principal by, string root, Proposal p)
    {
        var files = new List<FilePreview>(); var rejected = new List<string>();
        foreach (var f in p.Files)
        {
            if (files.Count >= MaxFiles) { rejected.Add($"„{f.Path}“: mehr als {MaxFiles} Dateien in einem Vorschlag."); continue; }
            if (Blocked(by, root, f.Path) is { } why) { rejected.Add(why); continue; }
            if (f.Content.Length > CodeAiKit.MaxTextBytes) { rejected.Add($"„{f.Path}“: der neue Inhalt ist zu groß."); continue; }
            var rel = f.Path.Trim().Replace('\\', '/');
            var full = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            string? before = File.Exists(full) ? CodeProject.ReadText(full) : null;
            var after = f.Content;
            if (before is not null && Norm(before) == Norm(after)) continue;
            var diff = CodeAiKit.Diff(before, after);
            var hunks = Hunks(diff);
            if (hunks.Count == 0) continue;
            files.Add(new(rel, before, after, before is null ? null : ShaOf(Norm(before)), diff, hunks));
        }
        return new(p.Plan, files, rejected);
    }

    static string Norm(string s) => s.Replace("\r\n", "\n").TrimStart('﻿');

    /// <summary>Neuer Inhalt einer Datei, wenn nur die angenommenen Blöcke übernommen werden (Index in <see cref="FilePreview.Hunks"/>). Alle = Vorschlag, keine = vorher.
    /// Zeilenenden bleiben wie in der Datei (CRLF bleibt CRLF).</summary>
    public static string Merge(FilePreview f, IReadOnlySet<int> accepted)
    {
        string result;
        if (accepted.Count >= f.Hunks.Count && Enumerable.Range(0, f.Hunks.Count).All(accepted.Contains)) result = Norm(f.After);
        else if (accepted.Count == 0) result = Norm(f.Before ?? "");
        else
        {
            var take = new bool[f.Diff.Count];
            for (var h = 0; h < f.Hunks.Count; h++) if (accepted.Contains(h)) for (var i = f.Hunks[h].From; i <= f.Hunks[h].To; i++) take[i] = true;
            var lines = new List<string>();
            for (var i = 0; i < f.Diff.Count; i++)
            {
                var l = f.Diff[i];
                if (l.Op == CodeAiKit.Op.Gleich || (l.Op == CodeAiKit.Op.Neu && take[i]) || (l.Op == CodeAiKit.Op.Weg && !take[i])) lines.Add(l.Text);
            }
            result = string.Join("\n", lines);
            var nl = Norm(f.After).EndsWith('\n') || Norm(f.Before ?? "").EndsWith('\n');
            if (nl && lines.Count > 0) result += "\n";
        }
        return f.Before is not null && f.Before.Contains("\r\n") ? result.Replace("\n", "\r\n") : result;
    }

    // ───────────── Anwenden und Rückgängig ─────────────

    /// <summary>Eine Datei eines angewendeten Schritts: Inhalt vorher (null = Datei war neu) und nachher.</summary>
    public sealed record StepFile(string Path, string? Before, string After);
    /// <summary>Ein angewendeter KI-Schritt (eine Rückgängig-Einheit).</summary>
    public sealed record Step(string Id, string Wish, string Model, DateTime AtUtc, IReadOnlyList<StepFile> Files);

    /// <summary>Angenommenes anwenden: alle Dateien des Schritts auf einmal (jede Datei atomar). Ändert sich eine Datei seit der Vorschau, wird gar nichts geschrieben.
    /// <paramref name="accepted"/>: je Pfad die angenommenen Blöcke. Gibt den Schritt zurück (null, wenn nichts angenommen wurde).</summary>
    /// T-0764 (13. Prüfrunde): <paramref name="by"/> – im Kinderprofil nur ein Projektordner in der Freigabe und keine ausführbare Endung („rbac.denied“ und Ausnahme, es wird nichts geschrieben).
    public static Step? Apply(Principal by, AuditLog audit, string root, Preview p, IReadOnlyDictionary<string, IReadOnlySet<int>> accepted, string wish, string model, DateTime? nowUtc = null)
    {
        ChildWrite.Demand(by, root, audit, checkName: false);
        var plan = new List<(string Full, StepFile File)>();
        foreach (var f in p.Files)
        {
            if (!accepted.TryGetValue(f.Path, out var acc) || acc.Count == 0) continue;
            if (Blocked(root, f.Path) is { } why) throw new InvalidOperationException(why);
            var full = Path.Combine(root, f.Path.Replace('/', Path.DirectorySeparatorChar));
            ChildWrite.Demand(by, full, audit);
            var now = File.Exists(full) ? CodeProject.ReadText(full) : null;
            if ((now is null) != f.IsNew || now is not null && ShaOf(Norm(now)) != f.BeforeSha)
                throw new InvalidOperationException($"„{f.Path}“ wurde seit dem Vorschlag geändert. Es wurde nichts übernommen – bitte den Vorschlag neu holen.");
            var after = Merge(f, acc);
            plan.Add((full, new StepFile(f.Path, now, after)));
        }
        if (plan.Count == 0) return null;
        var done = new List<(string Full, StepFile File)>();
        try
        {
            foreach (var x in plan) { AtomicFile.WriteAllText(x.Full, x.File.After); done.Add(x); }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Write(by, audit, done.Select(x => x.File), root, back: true); // halbe Schritte gibt es nicht
            throw new InvalidOperationException("Übernehmen ging nicht: " + e.Message);
        }
        return new Step(Guid.NewGuid().ToString("N")[..12], wish, model, nowUtc ?? DateTime.UtcNow, plan.Select(x => x.File).ToList());
    }

    /// <summary>Schritt zurücknehmen (<paramref name="back"/>) oder wieder anwenden. Eine Datei wird nur angefasst, wenn sie genau den erwarteten Stand hat
    /// (sonst hat sie jemand danach geändert – dann bleibt sie und steht in der Rückgabe).</summary>
    public static IReadOnlyList<string> Write(Principal by, AuditLog audit, IEnumerable<StepFile> files, string root, bool back)
    {
        var skipped = new List<string>();
        foreach (var f in back ? files.Reverse() : files)
        {
            var full = Path.Combine(root, f.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!CodeAiKit.InsideNoLinks(root, full)) { skipped.Add(f.Path); continue; }
            ChildWrite.Demand(by, full, audit, checkName: (back ? f.Before : f.After) is not null); // T-0764 (13. Prüfrunde): Rückgängig/Wiederholen ebenso
            var now = File.Exists(full) ? Norm(File.ReadAllText(full)) : null;
            var expect = back ? f.After : f.Before; var target = back ? f.Before : f.After;
            if (now == (target is null ? null : Norm(target))) continue; // schon so
            if (now != (expect is null ? null : Norm(expect))) { skipped.Add(f.Path); continue; }
            if (target is null) File.Delete(full); else AtomicFile.WriteAllText(full, target);
        }
        return skipped;
    }

    /// <summary>Schritt als Datei sichern (Verlauf unter den Trendo-Daten, damit Rückgängig/Wiederholen ihn wiederfindet).</summary>
    public static void Save(string file, Step s) => AtomicFile.WriteAllText(file, JsonSerializer.Serialize(s));
    public static Step? Load(string file)
    {
        try { return File.Exists(file) ? JsonSerializer.Deserialize<Step>(File.ReadAllText(file)) : null; }
        catch (JsonException) { return null; }
    }

    /// <summary>Eintrag im Dokument: Kennung, Wunsch, Dateien (ohne Inhalte).</summary>
    public static JsonObject DocEntry(Step s) => new()
    {
        ["id"] = s.Id, ["wunsch"] = s.Wish, ["modell"] = s.Model, ["zeit"] = s.AtUtc.ToString("o"),
        ["dateien"] = new JsonArray([.. s.Files.Select(f => (JsonNode)JsonValue.Create(f.Path)!)]),
    };

    /// <summary>Kennungen der Schritte, die laut Dokument angewendet sind (in Reihenfolge).</summary>
    public static IReadOnlyList<string> AppliedIds(JsonObject data) =>
        data[StepsField] is JsonArray a ? a.Select(x => x?["id"]?.ToString()).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).ToList() : [];

    /// <summary>Unified Diff (wie git) des ganzen Vorschlags – für den Beleg und den Export.</summary>
    public static string UnifiedAll(Preview p)
    {
        var sb = new StringBuilder();
        foreach (var f in p.Files) sb.Append(CodeAiKit.Unified(f.Diff, f.Path, added: f.IsNew));
        return sb.ToString();
    }
}
