using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Trendo.Core.Sys;

namespace Trendo.Core.Kreativ;

/// <summary>Videos mit KI: Szenenplan aus einer Beschreibung, Längenprüfung, FFmpeg-Aufruf mit Kamerafahrt (zoompan) und Überblendung (xfade), Herkunftsnachweis.
/// Die Standbilder malt die lokale Bild-KI (<see cref="ImageAiKit"/>) oder die Person wählt eigene Bilder.</summary>
public static partial class VideoAiKit
{
    public sealed record Scene(string Prompt, double Seconds, string Motion, string Caption, string Image = "");
    public sealed record Clip(string Image, double Seconds, string Motion, string? CaptionPng = null);

    public static readonly IReadOnlyList<(string Id, string Label)> Motions = [("zoom-rein", "Zoom rein"), ("zoom-raus", "Zoom raus"), ("links", "Schwenk nach links"), ("rechts", "Schwenk nach rechts"), ("ruhig", "Ruhig")];
    public static readonly IReadOnlyList<(string Id, string Label, int W, int H)> Formats = [("quer", "Quer 16:9", 1280, 720), ("hoch", "Hoch 9:16", 720, 1280), ("quadrat", "Quadrat 1:1", 1080, 1080)];
    public const double MaxTotal = 60, MinScene = 1, MaxScene = 15, DefaultScene = 4, Fade = 0.5;
    public const int Fps = 30, MaxScenes = 20, MaxCaption = 80;

    [GeneratedRegex(@"(?<![\p{L}\p{N}])[\(\[]?\s*(\d{1,2}(?:[.,]\d)?)\s*(?:s|sek|sekunden|sec)\b\.?\s*[\)\]]?", RegexOptions.IgnoreCase)]
    private static partial Regex SecRx();
    [GeneratedRegex(@"\b(?:text|einblendung|untertitel)\s*:\s*(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex CaptionRx();

    static readonly (string Motion, string[] Words)[] MotionWords =
    [
        ("zoom-rein", ["zoom rein", "hineinzoomen", "heranzoomen", "ranzoomen", "näher heran", "zoom in"]),
        ("zoom-raus", ["zoom raus", "herauszoomen", "rauszoomen", "wegzoomen", "zoom out"]),
        ("links", ["schwenk links", "schwenk nach links", "nach links"]),
        ("rechts", ["schwenk rechts", "schwenk nach rechts", "nach rechts"]),
        ("ruhig", ["ruhig", "stillstand", "ohne bewegung", "standbild"]),
    ];
    // ohne Angabe abwechselnd, damit das Video lebendig wirkt
    static readonly string[] Cycle = ["zoom-rein", "rechts", "zoom-raus", "links"];

    public static string MotionLabel(string id) => Motions.FirstOrDefault(m => m.Id == id).Label ?? "Zoom rein";
    public static (int W, int H) Size(string format) { var f = Formats.FirstOrDefault(x => x.Id == format); return f.Id is null ? (Formats[0].W, Formats[0].H) : (f.W, f.H); }

    /// <summary>Eine Szene je Absatz (Leerzeile dazwischen) – gibt es keine Leerzeilen, eine Szene je Zeile.
    /// In einer Szene dürfen stehen: Dauer („5 s“), Bewegung („Zoom rein“, „Schwenk nach links“, „ruhig“) und „Text: Einblendung“.</summary>
    public static List<Scene> Plan(string text)
    {
        var t = (text ?? "").Replace("\r", "").Trim();
        if (t.Length == 0) return [];
        var paras = Regex.Split(t, @"\n\s*\n").Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        var parts = paras.Count > 1 ? paras.Select(p => Regex.Replace(p, @"\s*\n\s*", " ")).ToList() : t.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var list = new List<Scene>();
        foreach (var raw in parts.Take(MaxScenes))
        {
            var p = Regex.Replace(raw, @"^\s*(?:\d+[.)]|[-*•])\s+", ""); // Aufzählungszeichen weg
            var caption = "";
            if (CaptionRx().Match(p) is { Success: true } cm) { caption = KreativSafe.Cut(cm.Groups[1].Value.Trim().Trim('„', '“', '"'), MaxCaption); p = p[..cm.Index]; }
            var sec = DefaultScene;
            if (SecRx().Match(p) is { Success: true } sm && double.TryParse(sm.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) { sec = s; p = p.Remove(sm.Index, sm.Length); }
            var motion = "";
            var low = p.ToLowerInvariant();
            foreach (var (m, words) in MotionWords)
                if (words.Select(w => Regex.Match(low, @"(?<![\p{L}\p{N}])" + Regex.Escape(w) + @"(?![\p{L}\p{N}])")).FirstOrDefault(x => x.Success) is { } hit)
                {
                    motion = m; p = p.Remove(hit.Index, hit.Length); break;
                }
            if (motion.Length == 0) motion = Cycle[list.Count % Cycle.Length];
            // Reste der herausgenommenen Angaben aufräumen: doppelte Satzzeichen, Zeichen am Anfang und Ende
            p = Regex.Replace(Regex.Replace(p, @"\s*([,;])(?:\s*[,;])+", "$1"), @"\s{2,}", " ");
            p = Regex.Replace(p, @"^[\s,;:–\-.]+|[\s,;:–\-.]+$", "");
            if (p.Length == 0 && caption.Length == 0) continue;
            list.Add(new(p, Math.Clamp(Math.Round(sec, 1), MinScene, MaxScene), motion, caption));
        }
        return list;
    }

    static int Frames(double sec) => Math.Max(1, (int)Math.Round(Math.Clamp(sec, MinScene, MaxScene) * Fps));

    /// <summary>Gesamtlänge mit Überblendungen (jede Überblendung lässt zwei Szenen überlappen).</summary>
    public static double Total(IReadOnlyList<double> seconds) =>
        seconds.Count == 0 ? 0 : Math.Round(seconds.Sum(s => Frames(s) / (double)Fps) - Fade * (seconds.Count - 1), 2);

    /// <summary>Was vor dem Bauen noch fehlt oder nicht geht. ⚠ = Hinweis, ✕ = muss behoben werden.</summary>
    public static IReadOnlyList<string> Check(IReadOnlyList<Scene> scenes, bool needImages)
    {
        var f = new List<string>();
        if (scenes.Count == 0) { f.Add("✕ Noch keine Szene. Schreib je Zeile eine Szene und klicke „Szenen planen“."); return f; }
        if (scenes.Count > MaxScenes) f.Add($"✕ Höchstens {MaxScenes} Szenen.");
        var total = Total(scenes.Select(s => s.Seconds).ToList());
        if (total > MaxTotal) f.Add($"✕ Das Video wäre {total.ToString("0.#", CultureInfo.GetCultureInfo("de-DE"))} s lang – höchstens {MaxTotal:0} s. Kürze Szenen oder streiche eine.");
        for (var i = 0; i < scenes.Count; i++)
        {
            var s = scenes[i]; var n = $"Szene {i + 1}";
            if (s.Seconds < MinScene || s.Seconds > MaxScene) f.Add($"✕ {n}: Dauer zwischen {MinScene:0} und {MaxScene:0} Sekunden.");
            if (s.Image.Length == 0 && ImageAiKit.Blocked(s.Prompt) is { } why) f.Add($"✕ {n}: {why}");
            if (s.Caption.Length > MaxCaption) f.Add($"⚠ {n}: Die Einblendung ist lang – höchstens {MaxCaption} Zeichen sind gut lesbar.");
            if (needImages && s.Image.Length == 0) f.Add($"⚠ {n}: Noch kein Bild – „Bilder malen“ oder ein eigenes Bild wählen.");
            if (Motions.All(m => m.Id != s.Motion)) f.Add($"✕ {n}: Unbekannte Bewegung „{s.Motion}“.");
        }
        return f;
    }

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Kamerafahrt je Bewegung als zoompan-Ausdrücke (ohne Kommas, damit der Filtergraph eindeutig bleibt).</summary>
    public static (string Z, string X, string Y) ZoomPan(string motion, int frames)
    {
        var n = Math.Max(1, frames - 1).ToString(CultureInfo.InvariantCulture);
        const string cx = "iw/2-(iw/zoom/2)", cy = "ih/2-(ih/zoom/2)";
        return motion switch
        {
            "zoom-raus" => ($"1.25-0.25*on/{n}", cx, cy),
            "links" => ("1.2", $"(iw-iw/zoom)*(1-on/{n})", cy),
            "rechts" => ("1.2", $"(iw-iw/zoom)*on/{n}", cy),
            "ruhig" => ("1", "0", "0"),
            _ => ($"1+0.25*on/{n}", cx, cy),
        };
    }

    /// <summary>Filtergraph: jedes Bild auf doppelte Größe zuschneiden (ruhigere Fahrt), zoompan, Einblendung darüber, dann Überblendung von Szene zu Szene.</summary>
    public static string Filter(IReadOnlyList<Clip> clips, int w, int h)
    {
        var sb = new StringBuilder(); var cap = clips.Count;
        for (var i = 0; i < clips.Count; i++)
        {
            var c = clips[i]; var frames = Frames(c.Seconds); var (z, x, y) = ZoomPan(c.Motion, frames);
            sb.Append(CultureInfo.InvariantCulture, $"[{i}:v]scale={w * 2}:{h * 2}:force_original_aspect_ratio=increase,crop={w * 2}:{h * 2},setsar=1,");
            sb.Append(CultureInfo.InvariantCulture, $"zoompan=z='{z}':x='{x}':y='{y}':d={frames}:s={w}x{h}:fps={Fps},format=yuv420p,setpts=PTS-STARTPTS");
            if (c.CaptionPng is { Length: > 0 }) { sb.Append(CultureInfo.InvariantCulture, $"[b{i}];[b{i}][{cap}:v]overlay=0:0:format=auto,format=yuv420p[s{i}];"); cap++; }
            else sb.Append(CultureInfo.InvariantCulture, $"[s{i}];");
        }
        if (clips.Count == 1) return sb.Append("[s0]null[vout]").ToString();
        var t = 0.0; var last = "s0";
        for (var k = 1; k < clips.Count; k++)
        {
            t += Frames(clips[k - 1].Seconds) / (double)Fps;
            var label = k == clips.Count - 1 ? "vout" : $"x{k}";
            sb.Append(CultureInfo.InvariantCulture, $"[{last}][s{k}]xfade=transition=fade:duration={F(Fade)}:offset={F(t - Fade * k)}[{label}]");
            if (k < clips.Count - 1) sb.Append(';');
            last = label;
        }
        return sb.ToString();
    }

    /// <summary>FFmpeg-Argumente: Bilder, danach die Einblendungen, ein Filtergraph, H.264 für jeden Player. „-n“: eine vorhandene Datei wird nie überschrieben.</summary>
    public static IReadOnlyList<string> FfmpegArgs(IReadOnlyList<Clip> clips, int w, int h, string output)
    {
        if (clips.Count == 0) throw new ArgumentException("Keine Szene mit Bild.");
        if (Total(clips.Select(c => c.Seconds).ToList()) > MaxTotal) throw new ArgumentException($"Videos gehen bis {MaxTotal:0} Sekunden.");
        if (clips.Any(c => c.Seconds < MinScene || c.Seconds > MaxScene)) throw new ArgumentException($"Jede Szene dauert {MinScene:0} bis {MaxScene:0} Sekunden.");
        var a = new List<string> { "-hide_banner", "-nostdin", "-n" };
        foreach (var c in clips) { a.Add("-i"); a.Add(c.Image); }
        foreach (var c in clips.Where(c => c.CaptionPng is { Length: > 0 })) { a.Add("-i"); a.Add(c.CaptionPng!); }
        a.AddRange(["-filter_complex", Filter(clips, w, h), "-map", "[vout]", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-r", Fps.ToString(CultureInfo.InvariantCulture), "-movflags", "+faststart", "-an", output]);
        return a;
    }

    public sealed record SceneSource(string Prompt, double Seconds, string Motion, string Caption, string Source, string ImageSha);
    public sealed record Provenance(string Generator, IReadOnlyList<SceneSource> Scenes, int Width, int Height, double Seconds, DateTime CreatedUtc, string Sha256, bool AiGenerated = true);

    /// <summary>Herkunftsnachweis neben dem Video (Datei.mp4.json): Szenen, woher jedes Bild kommt (KI oder eigenes Bild), Prüfsummen.</summary>
    public static string WriteProvenance(string mp4, Provenance p)
    {
        var f = mp4 + ".json";
        File.WriteAllText(f, JsonSerializer.Serialize(p with { Sha256 = J.Sha(mp4) }, new JsonSerializerOptions { WriteIndented = true }));
        return f;
    }

    /// <summary>Szenenplan als Text (für Export und Claude).</summary>
    public static string PlanText(IReadOnlyList<Scene> scenes, string title)
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        var sb = new StringBuilder($"# Szenenplan: {title}\n\nGesamtlänge etwa {Total(scenes.Select(s => s.Seconds).ToList()).ToString("0.#", de)} s, {scenes.Count} Szenen, Überblendung {Fade.ToString("0.#", de)} s.\n\n");
        for (var i = 0; i < scenes.Count; i++)
        {
            var s = scenes[i];
            sb.Append(de, $"{i + 1}. {s.Prompt} – {s.Seconds:0.#} s, {MotionLabel(s.Motion)}");
            if (s.Caption.Length > 0) sb.Append($", Einblendung „{s.Caption}“");
            sb.Append('\n');
        }
        return sb.ToString();
    }
}

/// <summary>KI-Agenten bauen: einen eigenen Helfer beschreiben, prüfen und als Claude-Code-Subagent (Markdown mit YAML-Kopf) oder als Trendo-Rolle ausgeben.</summary>
public sealed partial record AgentBlueprint(string Name, string Description, string Instructions, IReadOnlyList<string> Tools, string Examples = "", string Limits = "")
{
    public sealed record Tool(string Id, string Label, string[] ClaudeTools, string? Warning = null);

    /// <summary>Feste, sichere Liste. „Befehle ausführen“ nur mit Warnung – damit kann der Helfer alles auf dem Rechner tun, was du auch kannst.</summary>
    public static readonly IReadOnlyList<Tool> AllTools =
    [
        new("lesen", "Dateien lesen", ["Read"]),
        new("suchen", "Im Projekt suchen", ["Grep", "Glob"]),
        new("schreiben", "Im Projekt schreiben", ["Edit", "Write"], "Der Helfer darf Dateien im Projekt ändern. Claude fragt vorher nach."),
        new("internet", "Im Internet nachsehen", ["WebFetch", "WebSearch"], "Der Helfer ruft Internetseiten ab. Gib ihm keine vertraulichen Daten mit."),
        new("befehle", "Befehle ausführen", ["Bash"], "Vorsicht: Mit Befehlen kann der Helfer alles tun, was du auf dem Rechner tun kannst – auch löschen. Nur wählen, wenn es wirklich nötig ist."),
    ];

    public const int MaxDescription = 300, MaxInstructions = 20000, MinInstructions = 20;

    [GeneratedRegex(@"^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$")]
    private static partial Regex NameRx();

    /// <summary>Muster für Schlüssel und Passwörter, die nie in einer Agent-Datei stehen dürfen.</summary>
    [GeneratedRegex(@"(sk-(?:ant-|proj-)?[A-Za-z0-9_\-]{16,}|AKIA[0-9A-Z]{16}|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{20,}|xox[abprs]-[A-Za-z0-9\-]{10,}|AIza[0-9A-Za-z_\-]{30,}|-----BEGIN [A-Z ]*PRIVATE KEY-----|\bBearer\s+[A-Za-z0-9\-._~+/]{20,}|\b(?:passwort|password|kennwort|api[_\- ]?key|secret|token)\s*[:=]\s*(?=\S*\d)\S{8,})", RegexOptions.IgnoreCase)]
    private static partial Regex SecretRx();

    public static bool HasSecret(string? text) => SecretRx().IsMatch(text ?? "");

    /// <summary>Kurzname aus einem Anzeigenamen: klein, Umlaute umschreiben, Bindestriche statt Leerzeichen.</summary>
    public static string Slug(string? name)
    {
        var s = (name ?? "").Trim().ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        var b = new StringBuilder();
        foreach (var c in s) b.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-');
        var r = Regex.Replace(b.ToString(), "-{2,}", "-").Trim('-');
        if (r.Length > 0 && !char.IsLetter(r[0])) r = "helfer-" + r;
        return r.Length > 40 ? r[..40].TrimEnd('-') : r;
    }

    public sealed record Finding(string Field, string Message, bool Error);

    /// <summary>Prüfung vor Export und Speichern. Fehler verhindern das Speichern, Hinweise nicht.</summary>
    public IReadOnlyList<Finding> Check()
    {
        var f = new List<Finding>();
        if (string.IsNullOrWhiteSpace(Name)) f.Add(new("name", "Gib dem Helfer einen Kurznamen, z. B. „texte-pruefen“.", true));
        else if (!NameRx().IsMatch(Name) || Name.Length is < 3 or > 40) f.Add(new("name", $"Der Kurzname darf nur Kleinbuchstaben, Ziffern und Bindestriche enthalten (3 bis 40 Zeichen), z. B. „{(Slug(Name) is { Length: >= 3 } s ? s : "texte-pruefen")}“.", true));
        if (Name is "yes" or "no" or "on" or "off" or "true" or "false" or "null") f.Add(new("name", "Dieses Wort liest YAML als Ja/Nein – bitte einen anderen Kurznamen.", true));
        if (string.IsNullOrWhiteSpace(Description)) f.Add(new("description", "Schreib in einem Satz, wofür der Helfer da ist und wann er gerufen werden soll.", true));
        else if (Description.Length > MaxDescription) f.Add(new("description", $"Die Aufgabe ist zu lang (höchstens {MaxDescription} Zeichen).", true));
        else if (Description.Any(c => char.IsControl(c) || c is '\u2028' or '\u2029')) f.Add(new("description", "Die Aufgabe bitte in einer Zeile, ohne Steuerzeichen.", true));
        if (string.IsNullOrWhiteSpace(Instructions) || Instructions.Trim().Length < MinInstructions) f.Add(new("instructions", "Schreib dem Helfer ein paar Sätze, wie er arbeiten soll.", true));
        else if (Instructions.Length > MaxInstructions) f.Add(new("instructions", $"Die Anweisungen sind zu lang (höchstens {MaxInstructions} Zeichen).", true));
        if (Tools.Count == 0) f.Add(new("tools", "Wähle mindestens ein Werkzeug. Ohne Angabe dürfte der Helfer in Claude Code alle Werkzeuge nutzen.", true));
        foreach (var t in Tools.Where(t => AllTools.All(a => a.Id != t))) f.Add(new("tools", $"Unbekanntes Werkzeug „{t}“.", true));
        foreach (var t in AllTools.Where(a => a.Warning is not null && Tools.Contains(a.Id))) f.Add(new("tools", t.Warning!, false));
        foreach (var (field, text) in new[] { ("name", Name), ("description", Description), ("instructions", Instructions), ("examples", Examples), ("limits", Limits) })
            if (HasSecret(text)) f.Add(new(field, "Das sieht nach einem Schlüssel oder Passwort aus. So etwas gehört nie in eine Agent-Datei – bitte entfernen.", true));
        if (string.IsNullOrWhiteSpace(Limits)) f.Add(new("limits", "Tipp: Schreib dazu, was der Helfer nicht tun soll (z. B. nichts löschen, nichts senden).", false));
        return f;
    }

    public bool Ok => Check().All(x => !x.Error);

    static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    static string Unquote(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"') return Regex.Replace(s[1..^1], @"\\(.)", "$1");
        if (s.Length >= 2 && s[0] == '\'' && s[^1] == '\'') return s[1..^1].Replace("''", "'");
        return s;
    }

    public IReadOnlyList<string> ClaudeToolNames() => AllTools.Where(t => Tools.Contains(t.Id)).SelectMany(t => t.ClaudeTools).ToList();

    const string ExamplesHead = "## Beispiele", LimitsHead = "## Grenzen";

    /// <summary>Claude-Code-Subagent: YAML-Kopf (name, description, tools), danach die Anweisungen als Text.</summary>
    public string ToClaudeMarkdown()
    {
        var sb = new StringBuilder("---\n");
        sb.Append("name: ").Append(Name.Trim()).Append('\n');
        sb.Append("description: ").Append(Quote(Description.Trim())).Append('\n');
        sb.Append("tools: ").Append(string.Join(", ", ClaudeToolNames())).Append('\n');
        sb.Append("---\n\n").Append(Instructions.Trim().Replace("\r", "")).Append('\n');
        if (Examples.Trim().Length > 0) sb.Append('\n').Append(ExamplesHead).Append("\n\n").Append(Examples.Trim().Replace("\r", "")).Append('\n');
        if (Limits.Trim().Length > 0) sb.Append('\n').Append(LimitsHead).Append("\n\n").Append(Limits.Trim().Replace("\r", "")).Append('\n');
        return sb.ToString();
    }

    /// <summary>Liest einen Subagenten wieder ein. Werkzeuge, die nicht auf der sicheren Liste stehen, werden weggelassen und gemeldet.</summary>
    public static (AgentBlueprint Agent, IReadOnlyList<string> Dropped) FromClaudeMarkdown(string text)
    {
        var t = (text ?? "").Replace("\r", "").TrimStart('\uFEFF');
        if (!t.StartsWith("---\n", StringComparison.Ordinal)) throw new InvalidDataException("Das ist keine Agent-Datei: Der Kopf mit „---“ fehlt.");
        var end = t.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0) throw new InvalidDataException("Das ist keine Agent-Datei: Der Kopf wird nicht mit „---“ geschlossen.");
        var head = end > 4 ? t[4..end] : ""; var body = t[(end + 4)..].TrimStart('-').Trim('\n');
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in head.Split('\n'))
        {
            var i = line.IndexOf(':');
            if (i > 0) map[line[..i].Trim()] = Unquote(line[(i + 1)..]);
        }
        var names = (map.GetValueOrDefault("tools") ?? "").Trim().Trim('[', ']').Split(',').Select(x => x.Trim().Trim('"', '\'')).Where(x => x.Length > 0).ToList();
        var ids = AllTools.Where(a => a.ClaudeTools.Any(names.Contains)).Select(a => a.Id).ToList();
        var dropped = names.Where(n => AllTools.All(a => !a.ClaudeTools.Contains(n))).ToList();
        // Abschnitt „## Beispiele“ bzw. „## Grenzen“ bis zur nächsten Überschrift „## “ herauslösen
        string Section(string h, out string rest)
        {
            var b = "\n" + body + "\n";
            var i = b.IndexOf("\n" + h + "\n", StringComparison.Ordinal);
            if (i < 0) { rest = body; return ""; }
            var after = b[(i + h.Length + 2)..];
            var next = after.IndexOf("\n## ", StringComparison.Ordinal);
            var sec = next < 0 ? after : after[..next];
            rest = (b[..i] + (next < 0 ? "" : after[next..])).Trim('\n');
            return sec.Trim('\n');
        }
        var limits = Section(LimitsHead, out var r1); body = r1;
        var examples = Section(ExamplesHead, out var r2); body = r2;
        return (new AgentBlueprint(map.GetValueOrDefault("name") ?? "", map.GetValueOrDefault("description") ?? "", body.Trim('\n'), ids, examples, limits), dropped);
    }

    /// <summary>Für Trendos KI-Team (eigene Rolle): Name, Aufgabe, worauf die Rolle achtet (Anweisungen und Grenzen gekürzt), ob sie schreiben darf.</summary>
    public (string Name, string Does, string Focus, bool MayWriteFiles) ToTrendoRole()
    {
        var focus = Instructions.Trim().Replace("\r", "").Replace('\n', ' ');
        if (Limits.Trim().Length > 0) focus += " Grenzen: " + Limits.Trim().Replace("\r", "").Replace('\n', ' ');
        return (Name.Trim(), Description.Trim().TrimEnd('.'), KreativSafe.Cut(Regex.Replace(focus, @"\s{2,}", " "), 800), Tools.Contains("schreiben"));
    }

    /// <summary>Legt die Datei unter &lt;Projekt&gt;/.claude/agents/&lt;name&gt;.md an. Eine vorhandene Datei wird nie überschrieben.</summary>
    public string SaveTo(string projectDir)
    {
        if (!KreativSafe.LocalDir(projectDir)) throw new ArgumentException("Wähle einen Projektordner auf diesem Rechner.");
        if (Check().FirstOrDefault(x => x.Error) is { } e) throw new InvalidOperationException(e.Message);
        var dir = Path.Combine(projectDir, ".claude", "agents");
        var file = Path.Combine(dir, Name + ".md");
        if (File.Exists(file)) throw new IOException($"Im Projekt gibt es schon einen Helfer „{Name}“. Wähle einen anderen Kurznamen – vorhandene Dateien werden nicht überschrieben.");
        Directory.CreateDirectory(dir);
        using (var fs = new FileStream(file, FileMode.CreateNew, FileAccess.Write)) using (var w = new StreamWriter(fs, new UTF8Encoding(false))) w.Write(ToClaudeMarkdown());
        return file;
    }
}

/// <summary>KI-Modelle vergleichen: Antworten messen, blind bewerten (gemischt mit festem Startwert), über mehrere Fragen auswerten und berichten.</summary>
public static partial class ModelCompare
{
    public sealed record Answer(string Source, string Text, double Seconds, bool Ok);
    public sealed record Round(string Question, IReadOnlyList<Answer> Answers, IReadOnlyList<int> Order, IReadOnlyList<int> Stars, bool Revealed);
    public sealed record Row(string Source, int Rounds, int Rated, double AvgStars, int Wins, double AvgSeconds, double AvgWordsPerSecond, int Failures);

    [GeneratedRegex(@"[\p{L}\p{N}][\p{L}\p{N}'’\-]*")]
    private static partial Regex WordRx();

    public static int Words(string? text) => WordRx().Matches(text ?? "").Count;
    public static double WordsPerSecond(Answer a) => a.Ok && a.Seconds > 0 ? Math.Round(Words(a.Text) / a.Seconds, 1) : 0;

    /// <summary>Gleiche Zahlenfolge auf jedem Rechner und in jeder .NET-Fassung (xorshift32), anders als <see cref="Random"/>.</summary>
    static uint Next(ref uint s) { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }

    /// <summary>Reihenfolge, in der die Antworten gezeigt werden (Fisher-Yates mit festem Startwert).</summary>
    public static IReadOnlyList<int> Shuffle(int n, int seed)
    {
        var a = Enumerable.Range(0, Math.Max(0, n)).ToArray();
        var s = (uint)seed ^ 0x9E3779B9u; if (s == 0) s = 1;
        for (var i = a.Length - 1; i > 0; i--) { var j = (int)(Next(ref s) % (uint)(i + 1)); (a[i], a[j]) = (a[j], a[i]); }
        return a;
    }

    public static string Letter(int position) => position < 26 ? ((char)('A' + position)).ToString() : (position + 1).ToString(CultureInfo.InvariantCulture);

    public static Round NewRound(string question, IReadOnlyList<Answer> answers, int seed) =>
        new(question, answers, Shuffle(answers.Count, seed), new int[answers.Count], false);

    /// <summary>Sterne für die Antwort an Stelle <paramref name="position"/> (so wie gezeigt). 1 bis 5, 0 = noch nicht bewertet.</summary>
    public static Round Rate(Round r, int position, int stars)
    {
        if (r.Revealed) throw new InvalidOperationException("Diese Runde ist schon aufgedeckt.");
        if (position < 0 || position >= r.Order.Count) throw new ArgumentOutOfRangeException(nameof(position));
        if (stars is < 1 or > 5) throw new ArgumentException("Bitte 1 bis 5 Sterne.");
        var s = r.Stars.ToArray(); s[r.Order[position]] = stars;
        return r with { Stars = s };
    }

    /// <summary>Aufdecken geht erst, wenn jede Antwort, die da ist, bewertet wurde.</summary>
    public static bool CanReveal(Round r) => !r.Revealed && r.Answers.Count > 0 && r.Answers.Select((a, i) => (a, i)).Where(x => x.a.Ok).All(x => r.Stars[x.i] > 0) && r.Answers.Any(a => a.Ok);

    public static Round Reveal(Round r) => CanReveal(r) ? r with { Revealed = true } : throw new InvalidOperationException("Bewerte zuerst jede Antwort, dann wird aufgedeckt.");

    /// <summary>Ergebnistabelle: nur aufgedeckte Runden zählen für Sterne und Siege. Gleichstand an der Spitze zählt für alle als Sieg.</summary>
    public static IReadOnlyList<Row> Table(IEnumerable<Round> rounds)
    {
        var rs = rounds.ToList();
        var sources = rs.SelectMany(r => r.Answers.Select(a => a.Source)).Distinct().ToList();
        var rows = new List<Row>();
        foreach (var src in sources)
        {
            var mine = rs.SelectMany(r => r.Answers.Select((a, i) => (r, a, i)).Where(x => x.a.Source == src)).ToList();
            var ok = mine.Where(x => x.a.Ok).ToList();
            var rated = mine.Where(x => x.r.Revealed && x.a.Ok && x.r.Stars[x.i] > 0).ToList();
            var wins = mine.Count(x => x.r.Revealed && x.a.Ok && x.r.Stars[x.i] > 0 && x.r.Stars[x.i] == x.r.Stars.Max());
            rows.Add(new(src, mine.Count, rated.Count, rated.Count == 0 ? 0 : Math.Round(rated.Average(x => (double)x.r.Stars[x.i]), 2), wins,
                ok.Count == 0 ? 0 : Math.Round(ok.Average(x => x.a.Seconds), 2), ok.Count == 0 ? 0 : Math.Round(ok.Average(x => WordsPerSecond(x.a)), 1), mine.Count - ok.Count));
        }
        return rows.OrderByDescending(r => r.AvgStars).ThenByDescending(r => r.Wins).ThenBy(r => r.AvgSeconds).ToList();
    }

    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    static string Cell(string s) => s.Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");

    /// <summary>Bericht als Markdown: Ergebnistabelle, danach jede Runde mit Antworten (aufgedeckt mit Quelle, sonst nur Buchstabe).</summary>
    public static string Markdown(IReadOnlyList<Round> rounds, string title)
    {
        var sb = new StringBuilder($"# {title}\n\n{rounds.Count} Fragen, {rounds.Count(r => r.Revealed)} bewertet und aufgedeckt.\n\n");
        sb.Append("| Quelle | Bewertet | Sterne (Schnitt) | Siege | Dauer (Schnitt) | Wörter/s | Fehlgeschlagen |\n|---|---:|---:|---:|---:|---:|---:|\n");
        foreach (var r in Table(rounds))
            sb.Append(De, $"| {Cell(r.Source)} | {r.Rated} | {r.AvgStars:0.0#} | {r.Wins} | {r.AvgSeconds:0.0} s | {r.AvgWordsPerSecond:0.0} | {r.Failures} |\n");
        for (var k = 0; k < rounds.Count; k++)
        {
            var r = rounds[k];
            sb.Append(De, $"\n## Frage {k + 1}\n\n{r.Question}\n");
            for (var p = 0; p < r.Order.Count; p++)
            {
                var i = r.Order[p]; var a = r.Answers[i];
                var who = r.Revealed ? $"Antwort {Letter(p)} – {a.Source}" : $"Antwort {Letter(p)}";
                sb.Append(De, $"\n### {who}\n\n");
                if (a.Ok) sb.Append(De, $"{a.Seconds:0.0} s · {Words(a.Text)} Wörter · {WordsPerSecond(a):0.0} Wörter/s"); else sb.Append("Keine Antwort");
                if (r.Stars[i] > 0) sb.Append(De, $" · {r.Stars[i]} von 5 Sternen");
                sb.Append("\n\n").Append(a.Text.Trim()).Append('\n');
            }
        }
        return sb.ToString();
    }

    static string Csv(string s) => s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    /// <summary>Eine Zeile je Antwort, Trennzeichen Semikolon (öffnet Excel und LibreOffice auf Deutsch direkt).</summary>
    public static string Csv(IReadOnlyList<Round> rounds)
    {
        var sb = new StringBuilder("frage;quelle;buchstabe;ok;sekunden;woerter;woerter_je_sekunde;sterne;aufgedeckt\n");
        for (var k = 0; k < rounds.Count; k++)
        {
            var r = rounds[k];
            for (var p = 0; p < r.Order.Count; p++)
            {
                var i = r.Order[p]; var a = r.Answers[i];
                sb.Append(CultureInfo.InvariantCulture, $"{Csv(r.Question)};{Csv(r.Revealed ? a.Source : "verdeckt")};{Letter(p)};{(a.Ok ? "ja" : "nein")};{a.Seconds:0.00};{Words(a.Text)};{WordsPerSecond(a):0.0};{r.Stars[i]};{(r.Revealed ? "ja" : "nein")}\n");
            }
        }
        return sb.ToString();
    }
}

/// <summary>Eigene KI trainieren: Textklassifikator (multinomialer Naive Bayes mit Laplace-Glättung), läuft ohne Internet in Millisekunden.</summary>
public sealed partial class TextClassifier
{
    public const string Format = "trendo-textklassifikator";
    public const int FormatVersion = 1, MinPerClass = 5;

    public double Alpha { get; init; } = 1.0;
    public bool RemoveStopwords { get; init; } = true;
    readonly Dictionary<string, int> _docs = new(StringComparer.Ordinal);
    readonly Dictionary<string, int> _tokens = new(StringComparer.Ordinal);
    readonly Dictionary<string, Dictionary<string, int>> _counts = new(StringComparer.Ordinal);
    readonly HashSet<string> _vocab = new(StringComparer.Ordinal);

    public IReadOnlyList<string> Labels => _docs.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
    public int VocabularySize => _vocab.Count;
    public int Documents => _docs.Values.Sum();

    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    static readonly HashSet<string> Stop = new(StringComparer.Ordinal)
    {
        "der","die","das","den","dem","des","ein","eine","einer","eines","einem","einen","und","oder","aber","ist","sind","war","waren","wird","werden","wurde",
        "hat","haben","hatte","in","im","am","an","auf","aus","bei","mit","nach","von","vor","zu","zum","zur","für","über","um","als","auch","noch","nur","so",
        "wie","wenn","dass","es","er","sie","wir","ihr","ich","du","man","sich","mein","meine","dein","deine","sein","seine","ihre","dieser","diese","dieses",
        "da","was","wer","the","and","of","to","a","is",
    };

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex TokenRx();

    /// <summary>Wortzerlegung: Kleinschreibung (deutsch), Wörter aus Buchstaben und Ziffern (Bindestrich trennt, z. B. „E-Mail“ → e, mail), Umlaute und ß bleiben, Ein-Zeichen-Wörter fallen weg.</summary>
    public static IReadOnlyList<string> Tokenize(string? text, bool removeStopwords = true) =>
        TokenRx().Matches((text ?? "").ToLower(De)).Select(m => m.Value).Where(t => t.Length > 1 && !(removeStopwords && Stop.Contains(t))).ToList();

    public sealed record Example(string Text, string Label);

    public static TextClassifier Train(IEnumerable<Example> examples, double alpha = 1.0, bool removeStopwords = true)
    {
        if (alpha <= 0 || !double.IsFinite(alpha)) throw new ArgumentException("Die Glättung muss größer als 0 sein.");
        var m = new TextClassifier { Alpha = alpha, RemoveStopwords = removeStopwords };
        foreach (var e in examples)
        {
            var label = (e.Label ?? "").Trim(); if (label.Length == 0) continue;
            m._docs[label] = m._docs.GetValueOrDefault(label) + 1;
            if (!m._counts.TryGetValue(label, out var c)) m._counts[label] = c = new(StringComparer.Ordinal);
            foreach (var t in Tokenize(e.Text, removeStopwords)) { c[t] = c.GetValueOrDefault(t) + 1; m._tokens[label] = m._tokens.GetValueOrDefault(label) + 1; m._vocab.Add(t); }
        }
        if (m._docs.Count < 2) throw new InvalidOperationException("Zum Trainieren braucht es Beispiele aus mindestens zwei Kategorien.");
        return m;
    }

    public sealed record Prediction(string Label, double Confidence, IReadOnlyList<(string Label, double Probability)> All, int KnownWords);

    /// <summary>Kategorie mit Wahrscheinlichkeit je Kategorie (Logarithmen, dann normiert). Unbekannte Wörter zählen nicht.</summary>
    public Prediction Predict(string text)
    {
        var toks = Tokenize(text, RemoveStopwords).Where(_vocab.Contains).ToList();
        var total = (double)Documents; var v = (double)_vocab.Count;
        var logs = Labels.Select(l =>
        {
            var lp = Math.Log(_docs[l] / total);
            var denom = _tokens.GetValueOrDefault(l) + Alpha * v; var c = _counts[l];
            foreach (var t in toks) lp += Math.Log((c.GetValueOrDefault(t) + Alpha) / denom);
            return (Label: l, Log: lp);
        }).ToList();
        var max = logs.Max(x => x.Log);
        var exp = logs.Select(x => (x.Label, P: Math.Exp(x.Log - max))).ToList(); var sum = exp.Sum(x => x.P);
        var all = exp.Select(x => (x.Label, Probability: x.P / sum)).OrderByDescending(x => x.Probability).ThenBy(x => x.Label, StringComparer.Ordinal).ToList();
        return new(all[0].Label, all[0].Probability, all, toks.Count);
    }

    sealed record Store(string Format, int Version, double Alpha, bool RemoveStopwords, Dictionary<string, int> Docs, Dictionary<string, Dictionary<string, int>> Counts);

    public string ToJson() => JsonSerializer.Serialize(new Store(Format, FormatVersion, Alpha, RemoveStopwords, _docs, _counts), new JsonSerializerOptions { WriteIndented = true });

    public static TextClassifier FromJson(string json)
    {
        Store? s;
        try { s = JsonSerializer.Deserialize<Store>(json); } catch (JsonException) { s = null; }
        if (s is null || s.Format != Format) throw new InvalidDataException("Das ist kein Trendo-Modell.");
        if (s.Version > FormatVersion) throw new InvalidDataException("Dieses Modell stammt aus einer neueren Trendo-Fassung.");
        if (s.Docs is null || s.Counts is null || s.Docs.Count < 2 || !(s.Alpha > 0 && double.IsFinite(s.Alpha)) || s.Docs.Values.Any(n => n <= 0)) throw new InvalidDataException("Das Modell ist unvollständig.");
        // fremde Datei: keine leeren Einträge, keine Zahlen, deren Summe überläuft
        const long Max = int.MaxValue / 4;
        if (s.Docs.Keys.Any(k => k is null) || s.Counts.Values.Any(v => v is null) || s.Docs.Values.Sum(n => (long)n) > Max
            || s.Counts.Values.Any(v => v.Values.Sum(n => (long)Math.Max(0, n)) > Max)) throw new InvalidDataException("Das Modell ist beschädigt.");
        var m = new TextClassifier { Alpha = s.Alpha, RemoveStopwords = s.RemoveStopwords };
        foreach (var (l, n) in s.Docs)
        {
            m._docs[l] = n;
            var c = s.Counts.TryGetValue(l, out var x) ? new Dictionary<string, int>(x.Where(kv => kv.Value > 0), StringComparer.Ordinal) : new(StringComparer.Ordinal);
            m._counts[l] = c; m._tokens[l] = c.Values.Sum();
            foreach (var t in c.Keys) m._vocab.Add(t);
        }
        return m;
    }

    /// <summary>Feste Aufteilung je Kategorie: von jeder Kategorie etwa <paramref name="testShare"/> zur Prüfung (mindestens eins, wenn sie zwei oder mehr Beispiele hat), Rest zum Training.</summary>
    public static (List<Example> Train, List<Example> Test) Split(IReadOnlyList<Example> examples, double testShare, int seed)
    {
        if (testShare is < 0 or >= 1) throw new ArgumentException("Der Prüfanteil liegt zwischen 0 und 1.");
        var train = new List<Example>(); var test = new List<Example>();
        foreach (var g in examples.Where(e => e.Label.Trim().Length > 0).GroupBy(e => e.Label.Trim()).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var items = g.ToList();
            var order = ModelCompare.Shuffle(items.Count, unchecked(seed + StableHash(g.Key)));
            var n = testShare == 0 || items.Count < 2 ? 0 : Math.Clamp((int)Math.Round(items.Count * testShare), 1, items.Count - 1);
            for (var i = 0; i < order.Count; i++) (i < n ? test : train).Add(items[order[i]]);
        }
        return (train, test);
    }

    static int StableHash(string s) { unchecked { var h = (int)2166136261; foreach (var c in s) h = (h ^ c) * 16777619; return h; } }

    public sealed record ClassStats(string Label, int Support, double Precision, double Recall, double F1);
    public sealed record Evaluation(double Accuracy, int Count, IReadOnlyList<string> Labels, int[,] Confusion, IReadOnlyList<ClassStats> PerClass);

    /// <summary>Genauigkeit, Verwechslungstabelle (Zeile = richtige, Spalte = vorhergesagte Kategorie), Präzision und Trefferquote je Kategorie.</summary>
    public Evaluation Evaluate(IReadOnlyList<Example> test)
    {
        var labels = Labels.Union(test.Select(e => e.Label.Trim())).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        var idx = labels.Select((l, i) => (l, i)).ToDictionary(x => x.l, x => x.i, StringComparer.Ordinal);
        var cm = new int[labels.Count, labels.Count]; var right = 0;
        foreach (var e in test)
        {
            var p = Predict(e.Text).Label; var a = e.Label.Trim();
            cm[idx[a], idx[p]]++; if (p == a) right++;
        }
        var stats = labels.Select((l, i) =>
        {
            var tp = cm[i, i]; var col = Enumerable.Range(0, labels.Count).Sum(r => cm[r, i]); var row = Enumerable.Range(0, labels.Count).Sum(c => cm[i, c]);
            var prec = col == 0 ? 0 : (double)tp / col; var rec = row == 0 ? 0 : (double)tp / row;
            return new ClassStats(l, row, Math.Round(prec, 4), Math.Round(rec, 4), prec + rec == 0 ? 0 : Math.Round(2 * prec * rec / (prec + rec), 4));
        }).ToList();
        return new(test.Count == 0 ? 0 : Math.Round((double)right / test.Count, 4), test.Count, labels, cm, stats);
    }

    /// <summary>Hinweise vor dem Training: zu wenige Beispiele je Kategorie, leere Texte, nur eine Kategorie.</summary>
    public static IReadOnlyList<string> Warnings(IReadOnlyList<Example> examples)
    {
        var w = new List<string>();
        var groups = examples.Where(e => e.Label.Trim().Length > 0).GroupBy(e => e.Label.Trim()).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
        if (groups.Count < 2) w.Add("✕ Es braucht Beispiele aus mindestens zwei Kategorien.");
        foreach (var g in groups.Where(g => g.Count() < MinPerClass)) w.Add($"⚠ Kategorie „{g.Key}“ hat nur {g.Count()} Beispiel{(g.Count() == 1 ? "" : "e")} – mindestens {MinPerClass}, besser 20 und mehr.");
        var noLabel = examples.Count(e => e.Label.Trim().Length == 0); if (noLabel > 0) w.Add($"⚠ {noLabel} Beispiel{(noLabel == 1 ? "" : "e")} ohne Kategorie {(noLabel == 1 ? "wird" : "werden")} nicht genutzt.");
        var empty = examples.Count(e => e.Label.Trim().Length > 0 && Tokenize(e.Text).Count == 0); if (empty > 0) w.Add($"⚠ {empty} Beispiel{(empty == 1 ? "" : "e")} ohne verwertbare Wörter.");
        if (groups.Count >= 2) { var max = groups.Max(g => g.Count()); var min = groups.Min(g => g.Count()); if (min > 0 && max >= 4 * min) w.Add("⚠ Die Kategorien sind sehr ungleich groß – die kleine wird seltener erkannt."); }
        return w;
    }

    /// <summary>Beispiele aus CSV: erste Spalte Text, zweite Kategorie (Trennzeichen ; , oder Tab). Eine Kopfzeile wie „text;kategorie“ wird erkannt.</summary>
    public static List<Example> FromCsv(string csv, int max = 50000)
    {
        var rows = Trendo.Core.Sys.Csv.Parse((csv ?? "").TrimStart('\uFEFF'), max + 1);
        var list = new List<Example>();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i]; if (r.Count < 2) continue;
            var text = r[0].Trim(); var label = r[1].Trim();
            if (i == 0 && text.ToLowerInvariant() is "text" or "satz" or "beispiel" && label.ToLowerInvariant() is "kategorie" or "label" or "klasse" or "category") continue;
            if (text.Length == 0 && label.Length == 0) continue;
            list.Add(new(text, label));
            if (list.Count >= max) break;
        }
        return list;
    }

    static string Pct(double v) => (v * 100).ToString("0.#", De) + " %";

    /// <summary>Auswertung als Markdown für den Export.</summary>
    public static string Report(Evaluation e, int trainCount, IReadOnlyList<string> warnings, string title)
    {
        var sb = new StringBuilder($"# Auswertung: {title}\n\nTrainiert mit {trainCount} Beispielen, geprüft mit {e.Count}. Genauigkeit: **{Pct(e.Accuracy)}**.\n\n");
        foreach (var w in warnings) sb.Append("- ").Append(w).Append('\n');
        if (warnings.Count > 0) sb.Append('\n');
        sb.Append("## Je Kategorie\n\n| Kategorie | Prüfbeispiele | Präzision | Trefferquote | F1 |\n|---|---:|---:|---:|---:|\n");
        foreach (var c in e.PerClass) sb.Append(De, $"| {c.Label.Replace("|", "\\|")} | {c.Support} | {Pct(c.Precision)} | {Pct(c.Recall)} | {c.F1:0.00} |\n");
        sb.Append("\n## Verwechslungstabelle\n\nZeile = richtige Kategorie, Spalte = erkannt als.\n\n| | ").Append(string.Join(" | ", e.Labels.Select(l => l.Replace("|", "\\|")))).Append(" |\n|---|").Append(string.Concat(e.Labels.Select(_ => "---:|"))).Append('\n');
        for (var i = 0; i < e.Labels.Count; i++)
            sb.Append("| ").Append(e.Labels[i].Replace("|", "\\|")).Append(" | ").Append(string.Join(" | ", Enumerable.Range(0, e.Labels.Count).Select(j => e.Confusion[i, j].ToString(CultureInfo.InvariantCulture)))).Append(" |\n");
        return sb.ToString();
    }
}
