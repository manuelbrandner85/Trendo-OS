using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core.Media;

namespace Trendo.Core.Film;

/// <summary>Material einer Einstellung: eigener Videoclip oder eigenes Foto (auch ein Storyboard-Bild), Ausschnitt <see cref="In"/>–<see cref="Out"/> in Sekunden.
/// <paramref name="Source"/>: „eigen“ (selbst gewählt) oder „ki“ (von der lokalen Bild-KI gemalt). <paramref name="Voice"/>: Sprecher- oder Dialogton zu dieser Einstellung.</summary>
public sealed record FilmMaterial(string Id, string Signature, string File, double In, double Out, double SourceLength, bool HasAudio, string Source = "eigen", string Voice = "", double VoiceLength = 0)
{
    public bool IsImage => File.Length > 0 && MediaStudio.IsImage(File);
    public bool HasPicture => File.Length > 0;
}

/// <summary>Eine Einstellung des Drehbuchs mit ihrem Material (oder ohne).</summary>
public sealed record FilmShot(int Number, int SceneIndex, FilmScene Scene, ShotPlan.Shot Shot, int LineNo, FilmMaterial? Material)
{
    public string Id => Shot.Id;
    /// <summary>Länge im Film: beim Videoclip der gewählte Ausschnitt, sonst die geplante Dauer aus dem Drehbuch.</summary>
    public double Seconds => Material is { HasPicture: true, IsImage: false } m ? Math.Max(VideoEdit.MinLength, m.Out - m.In) : Shot.Seconds;
    public string Kind => Material is not { HasPicture: true } m ? "karte" : m.IsImage ? "bild" : "video";
}

/// <summary>Einstellungen des Rohschnitts – liegen im Film-Dokument, damit Rückgängig und Speichern greifen.</summary>
public sealed record FilmCutOptions(bool OpeningTitle, string SceneTitles, bool Credits, double Fade, string Music, double MusicLength, double MusicVolume, string Subtitles, int Width, int Height, string CreditsText)
{
    public const string TitlesNone = "keine", TitlesActs = "akt", TitlesScenes = "szene";
    public const string SubsFile = "srt", SubsBurn = "einbrennen", SubsNone = "keine";
    public const double TitleSeconds = 3, CardSeconds = 2, CreditSeconds = 4;

    public static FilmCutOptions Of(JsonObject film)
    {
        var c = film["cut"] as JsonObject ?? [];
        string S(string k, string fb) => c[k]?.GetValueKind() == System.Text.Json.JsonValueKind.String ? c[k]!.GetValue<string>() : fb;
        double N(string k, double fb) => c[k] is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.Number && double.TryParse(v.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : fb;
        bool B(string k, bool fb) => c[k]?.GetValueKind() switch { System.Text.Json.JsonValueKind.True => true, System.Text.Json.JsonValueKind.False => false, _ => fb };
        int w = (int)N("width", 1280), h = (int)N("height", 720);
        return new(B("title", true), S("sceneTitles", TitlesActs), B("credits", true), Math.Clamp(N("fade", 0.5), 0, 3), S("music", ""), N("musicLength", 0), Math.Clamp(N("musicVolume", 0.5), 0, 2),
            S("subs", SubsFile), w < 16 ? 1280 : w, h < 16 ? 720 : h, S("creditsText", ""));
    }

    public static void Set(JsonObject film, string key, JsonNode? value)
    {
        if (film["cut"] is not JsonObject c) film["cut"] = c = [];
        c[key] = value;
    }
}

/// <summary>Ein Stück des Rohschnitts in Reihenfolge: Titel, Zwischentitel, Einstellung (Clip, Bild oder Texttafel), Abspann.</summary>
public sealed record FilmPiece(string Kind, string Label, string File, double In, double Out, double SourceLength, bool HasAudio, IReadOnlyList<FilmCardLine>? Card = null, string ShotId = "", int SceneIndex = -1, double Fade = 0)
{
    public double Seconds => Math.Max(0, Out - In);
}

/// <summary>Zeile einer Texttafel. <paramref name="Style"/>: „titel“ (groß, Gold), „text“ (weiß), „klein“ (grau).</summary>
public sealed record FilmCardLine(string Text, string Style = "text");

public sealed record FilmCutPlan(IReadOnlyList<FilmPiece> Pieces, IReadOnlyList<FilmShot> Shots, IReadOnlyList<TimedText.Cue> Subtitles, IReadOnlyList<string> Problems, FilmCutOptions Options)
{
    /// <summary>Geplante Filmlänge: alle Stücke abzüglich der Überblendungen (dieselbe Rechnung wie der Videoschnitt).</summary>
    public double Seconds => VideoEdit.VideoLength(FilmCut.Clips(this));
    public IEnumerable<FilmPiece> Cards => Pieces.Where(p => p.Card is not null);
}

/// <summary>T-0693: Vom Drehbuch zum Film. Aus der Reihenfolge der Einstellungen entsteht eine Zeitleiste im Datenmodell des Videoschnitts (<see cref="VideoEdit"/>):
/// Titel, Zwischentitel, Clips und Bilder je Einstellung, Texttafel wo Material fehlt, Abspann, Musik, Sprecherton, Untertitel aus den Dialogzeilen.
/// Gerendert wird mit dem vorhandenen FFmpeg-Weg des Videoschnitts – hier entsteht nur das Projekt dafür.</summary>
public static class FilmCut
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ---------- Material je Einstellung ----------
    /// <summary>Erkennungszeichen einer Einstellung: Größe und Beschreibung. So bleibt das Material an der Einstellung, auch wenn im Drehbuch darüber etwas eingefügt wird.</summary>
    public static string Signature(ShotPlan.Shot s) => (s.Type + "|" + s.Description).Trim().ToLowerInvariant();

    public static List<FilmMaterial> Materials(JsonObject film) => (film["material"] as JsonArray ?? []).OfType<JsonObject>().Select(o => new FilmMaterial(
        Str(o["id"]), Str(o["sig"]), Str(o["file"]), Num(o["in"]), Num(o["out"]), Num(o["len"]), o["audio"]?.GetValueKind() == System.Text.Json.JsonValueKind.True,
        Str(o["source"]) is { Length: > 0 } s ? s : "eigen", Str(o["voice"]), Num(o["voiceLen"]))).ToList();

    static string Str(JsonNode? n) => n?.GetValueKind() == System.Text.Json.JsonValueKind.String ? n.GetValue<string>() : "";
    static double Num(JsonNode? n) => n is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.Number && double.TryParse(v.ToJsonString(), NumberStyles.Float, Inv, out var d) ? d : 0;
    static double R(double v) => Math.Round(v, 3);

    static void Store(JsonObject film, IEnumerable<FilmMaterial> list) => film["material"] = new JsonArray([.. list.Where(m => m.File.Length > 0 || m.Voice.Length > 0).Select(m => (JsonNode)new JsonObject
    {
        ["id"] = m.Id, ["sig"] = m.Signature, ["file"] = m.File, ["in"] = R(m.In), ["out"] = R(m.Out), ["len"] = R(m.SourceLength), ["audio"] = m.HasAudio, ["source"] = m.Source, ["voice"] = m.Voice, ["voiceLen"] = R(m.VoiceLength),
    })]);

    /// <summary>Alle Einstellungen des Drehbuchs in Reihenfolge, jede mit ihrem Material. Zuordnung zuerst über Größe und Beschreibung, sonst über die Nummer.</summary>
    public static List<FilmShot> Shots(JsonObject film)
    {
        var script = Fountain.Parse(Str(film["script"])); var b = FilmBreakdowns.Of(script);
        var lines = new List<int>(); var seenHead = false;
        foreach (var l in script.Lines) { if (l.Kind == FountainKind.Heading) seenHead = true; else if (l.Kind == FountainKind.Shot && seenHead) lines.Add(l.LineNo); }
        var mats = Materials(film); var used = new bool[mats.Count];
        var flat = b.Scenes.SelectMany((sc, si) => sc.Shots.Select(x => (sc, si, x))).ToList();
        var pick = new FilmMaterial?[flat.Count];
        for (var pass = 0; pass < 3; pass++)
            for (var i = 0; i < flat.Count; i++)
            {
                if (pick[i] is not null) continue;
                var sig = Signature(flat[i].x); var id = flat[i].x.Id;
                var k = -1;
                for (var n = 0; n < mats.Count && k < 0; n++)
                {
                    var m = mats[n];
                    if (!used[n] && pass switch { 0 => m.Signature == sig && m.Id == id, 1 => m.Signature == sig, _ => m.Id == id && !flat.Any(f => Signature(f.x) == m.Signature) }) k = n;
                }
                if (k >= 0) { used[k] = true; pick[i] = mats[k] with { Id = id, Signature = sig }; }
            }
        return [.. flat.Select((f, i) => new FilmShot(i + 1, f.si, f.sc, f.x, i < lines.Count ? lines[i] : 0, pick[i]))];
    }

    static void Put(JsonObject film, FilmShot shot, Func<FilmMaterial, FilmMaterial> change)
    {
        var shots = Shots(film);
        var list = shots.Where(s => s.Material is not null && s.Id != shot.Id).Select(s => s.Material!).ToList();
        var cur = shots.FirstOrDefault(s => s.Id == shot.Id)?.Material ?? new FilmMaterial(shot.Id, Signature(shot.Shot), "", 0, 0, 0, false);
        list.Add(change(cur));
        Store(film, list);
    }

    /// <summary>Clip oder Bild einer Einstellung zuweisen. Videos: Ausschnitt ab Anfang in geplanter Länge (höchstens so lang wie der Clip); Bilder stehen so lange wie geplant.</summary>
    public static void Assign(JsonObject film, FilmShot shot, string file, double sourceLength, bool hasAudio, string source = "eigen")
    {
        if (MediaStudio.IsAudio(file)) throw new InvalidOperationException("Das ist eine Tondatei. Als Bild einer Einstellung gehen nur Videos und Fotos – Ton legst du als Sprecherton oder Musik an.");
        var image = MediaStudio.IsImage(file);
        if (!image && sourceLength < VideoEdit.MinLength) throw new InvalidOperationException("Die Länge des Videos lässt sich nicht lesen.");
        Put(film, shot, m => image ? m with { File = file, In = 0, Out = shot.Shot.Seconds, SourceLength = 3600, HasAudio = false, Source = source }
            : m with { File = file, In = 0, Out = Math.Min(sourceLength, shot.Shot.Seconds), SourceLength = sourceLength, HasAudio = hasAudio, Source = source });
    }

    public static void Unassign(JsonObject film, FilmShot shot) => Put(film, shot, m => m with { File = "", In = 0, Out = 0, SourceLength = 0, HasAudio = false, Source = "eigen" });

    /// <summary>Ein- und Ausstiegspunkt eines Videoclips (Sekunden im Clip), begrenzt auf den Clip und die Mindestlänge.</summary>
    public static void Trim(JsonObject film, FilmShot shot, double newIn, double newOut)
    {
        if (shot.Material is not { HasPicture: true, IsImage: false } cur) throw new InvalidOperationException("Ein- und Ausstieg gibt es nur bei Videoclips. Bei Bildern und Tafeln stellst du die Dauer ein.");
        newIn = Math.Clamp(newIn, 0, cur.SourceLength - VideoEdit.MinLength); newOut = Math.Clamp(newOut, newIn + VideoEdit.MinLength, cur.SourceLength);
        Put(film, shot, m => m with { In = newIn, Out = newOut });
    }

    /// <summary>Sprecher- oder Dialogton einer Einstellung (leer = entfernen).</summary>
    public static void SetVoice(JsonObject film, FilmShot shot, string file, double length) => Put(film, shot, m => m with { Voice = file, VoiceLength = file.Length == 0 ? 0 : length });

    /// <summary>Geplante Dauer einer Einstellung im Drehbuch ändern („(4 s)“ am Zeilenende). Bild und Tafel stehen dann so lange.</summary>
    public static void SetSeconds(JsonObject film, FilmShot shot, double seconds)
    {
        seconds = Math.Clamp(Math.Round(seconds, 1), 0.5, 600);
        var src = Str(film["script"]).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (shot.LineNo < 1 || shot.LineNo > src.Length) throw new InvalidOperationException("Die Einstellung steht nicht mehr im Drehbuch.");
        var line = System.Text.RegularExpressions.Regex.Replace(src[shot.LineNo - 1].TrimEnd(), @"\s*\(\d+(?:[.,]\d+)?\s*s\)\s*$", "");
        src[shot.LineNo - 1] = $"{line} ({seconds.ToString("0.#", Inv).Replace('.', ',')} s)";
        film["script"] = string.Join('\n', src);
        if (shot.Material is { IsImage: true }) Put(film, shot with { Shot = shot.Shot with { Seconds = seconds } }, m => m with { Out = seconds });
    }

    /// <summary>Einstellung innerhalb ihrer Szene eine Stelle nach vorn (−1) oder hinten (+1). Das Material wandert mit, weil es an der Beschreibung hängt.</summary>
    public static bool Move(JsonObject film, FilmShot shot, int by)
    {
        var shots = Shots(film); var i = shots.FindIndex(s => s.Id == shot.Id); var j = i + by;
        if (i < 0 || j < 0 || j >= shots.Count || shots[i].SceneIndex != shots[j].SceneIndex) return false;
        var src = Str(film["script"]).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int a = shots[i].LineNo - 1, b = shots[j].LineNo - 1;
        if (a < 0 || b < 0 || a >= src.Length || b >= src.Length) return false;
        (src[a], src[b]) = (src[b], src[a]);
        // die Nummern tauschen mit: Material zuerst auf die Beschreibung festlegen, dann neu einlesen
        Store(film, shots.Where(s => s.Material is not null).Select(s => s.Material! with { Id = "" }));
        film["script"] = string.Join('\n', src);
        Store(film, Shots(film).Where(s => s.Material is not null).Select(s => s.Material!));
        return true;
    }

    /// <summary>Clips ohne Drehbuch: je Datei eine Einstellung in einer Szene „Rohmaterial“ anlegen und das Material gleich zuweisen.</summary>
    public static int AddClipsAsShots(JsonObject film, IReadOnlyList<(string File, double Length, bool HasAudio)> files)
    {
        if (files.Count == 0) return 0;
        var script = Str(film["script"]).Replace("\r\n", "\n").TrimEnd();
        var sb = new StringBuilder(script);
        if (sb.Length > 0) sb.Append("\n\n");
        sb.Append("INNEN/AUSSEN. ROHMATERIAL – TAG\n\n");
        var names = new List<string>();
        foreach (var (file, length, _) in files)
        {
            var name = Path.GetFileNameWithoutExtension(file).Replace(':', ' ').Replace('(', ' ').Replace(')', ' ').Trim();
            if (name.Length < 2) name = "Clip";
            var n = name; for (var k = 2; names.Contains(n, StringComparer.OrdinalIgnoreCase); k++) n = $"{name} {k}";
            names.Add(n);
            var sec = MediaStudio.IsImage(file) ? 4 : Math.Clamp(Math.Round(length, 1), 0.5, 600);
            sb.Append($"- Totale: {n} ({sec.ToString("0.#", Inv).Replace('.', ',')} s)\n");
        }
        film["script"] = sb.ToString();
        var shots = Shots(film); var first = shots.Count - files.Count;
        for (var i = 0; i < files.Count; i++) Assign(film, Shots(film)[first + i], files[i].File, files[i].Length, files[i].HasAudio);
        return files.Count;
    }

    // ---------- Rohschnitt ----------
    /// <summary>Text auf Zeilen umbrechen (Texttafeln haben keinen automatischen Umbruch).</summary>
    public static List<string> Wrap(string text, int max)
    {
        var lines = new List<string>(); var cur = new StringBuilder();
        foreach (var w in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (cur.Length > 0 && cur.Length + 1 + w.Length > max) { lines.Add(cur.ToString()); cur.Clear(); }
            if (cur.Length > 0) cur.Append(' ');
            cur.Append(w.Length > max ? w[..max] : w);
        }
        if (cur.Length > 0) lines.Add(cur.ToString());
        return lines;
    }

    static string CardFile(string dir, IReadOnlyList<FilmCardLine> lines, int w, int h) =>
        Path.Combine(dir, "tafel-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{w}x{h}|" + string.Join('\n', lines.Select(l => l.Style + ":" + l.Text)))))[..16].ToLowerInvariant() + ".png");

    /// <summary>Den Rohschnitt planen: Reihenfolge, Längen, Tafeln, Untertitel. Ändert nichts und startet nichts.</summary>
    public static FilmCutPlan Plan(JsonObject film, string title, string cardDir)
    {
        var o = FilmCutOptions.Of(film); var script = Fountain.Parse(Str(film["script"])); var b = FilmBreakdowns.Of(script);
        var shots = Shots(film); var problems = new List<string>(); var pieces = new List<FilmPiece>();
        if (script.Title.Length > 0) title = script.Title;
        FilmPiece Card(string kind, string label, double seconds, List<FilmCardLine> lines, string shotId = "", int scene = -1) =>
            new(kind, label, CardFile(cardDir, lines, o.Width, o.Height), 0, seconds, 3600, false, lines, shotId, scene);

        // Akte: Abschnitts-Zeilen („# Anfang“) gehören zur nächsten Szene
        var acts = new Dictionary<int, string>(); var sceneNo = -1; string? pending = null;
        foreach (var l in script.Lines)
        {
            if (l.Kind == FountainKind.Section) pending = l.Text;
            else if (l.Kind == FountainKind.Heading) { sceneNo++; if (pending is not null && sceneNo < b.Scenes.Count) { acts[sceneNo] = pending; pending = null; } }
        }

        if (o.OpeningTitle)
        {
            var lines = Wrap(title, 26).Select(t => new FilmCardLine(t, "titel")).ToList();
            if (Str(film["logline"]) is { Length: > 0 } log) lines.AddRange(Wrap(log, 52).Take(3).Select(t => new FilmCardLine(t, "klein")));
            pieces.Add(Card("titel", "Titel", FilmCutOptions.TitleSeconds, lines));
        }
        var last = -1;
        foreach (var s in shots)
        {
            if (s.SceneIndex != last)
            {
                last = s.SceneIndex;
                if (o.SceneTitles == FilmCutOptions.TitlesActs && acts.TryGetValue(s.SceneIndex, out var act))
                    pieces.Add(Card("zwischentitel", act, FilmCutOptions.CardSeconds, [.. Wrap(act, 30).Select(t => new FilmCardLine(t, "titel"))], scene: s.SceneIndex));
                else if (o.SceneTitles == FilmCutOptions.TitlesScenes)
                    pieces.Add(Card("zwischentitel", s.Scene.Location, FilmCutOptions.CardSeconds,
                        [.. Wrap(s.Scene.Location, 30).Select(t => new FilmCardLine(t, "titel")), new FilmCardLine(s.Scene.Place + (s.Scene.Time.Length > 0 ? " · " + s.Scene.Time : ""), "klein")], scene: s.SceneIndex));
            }
            var m = s.Material;
            if (m is { HasPicture: true } && !File.Exists(m.File)) { problems.Add($"{s.Id}: Die Datei „{Path.GetFileName(m.File)}“ fehlt – stattdessen steht eine Texttafel im Film."); m = null; }
            if (m is { HasPicture: true })
                pieces.Add(m.IsImage ? new("shot", s.Id, m.File, 0, s.Shot.Seconds, 3600, false, null, s.Id, s.SceneIndex)
                    : new("shot", s.Id, m.File, m.In, Math.Max(m.In + VideoEdit.MinLength, m.Out), m.SourceLength, m.HasAudio, null, s.Id, s.SceneIndex));
            else
                pieces.Add(Card("shot", s.Id, s.Shot.Seconds, [new(s.Shot.Type + " · " + s.Scene.Location, "klein"), .. Wrap(s.Shot.Description, 44).Take(4).Select(t => new FilmCardLine(t, "text"))], s.Id, s.SceneIndex));
        }
        if (shots.Count == 0) problems.Add("Das Drehbuch hat noch keine Einstellungen. Schreib „- Totale: Beschreibung (4 s)“ unter einen Szenenkopf oder klick auf „Einstellungen vorschlagen“.");
        if (o.Credits && shots.Count > 0)
        {
            var names = b.Roles.Select(r => Name(r.Name)).ToList();
            var extra = o.CreditsText.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            var all = new List<FilmCardLine>();
            if (names.Count > 0) { all.Add(new("Mit", "klein")); all.AddRange(names.Select(n => new FilmCardLine(n))); }
            all.AddRange(extra.SelectMany(e => Wrap(e, 48)).Select(e => new FilmCardLine(e)));
            if (all.Count == 0) all.Add(new("Ende", "titel"));
            foreach (var page in all.Chunk(8)) pieces.Add(Card("abspann", "Abspann", FilmCutOptions.CreditSeconds, [.. page]));
        }
        // Überblenden nur dort, wo ein neuer Abschnitt beginnt: an Titeln, Zwischentiteln, Szenenwechseln und vor dem Abspann. In einer Szene wird hart geschnitten.
        if (o.Fade > 0)
            for (var i = 0; i < pieces.Count - 1; i++)
                if (pieces[i].Kind != "shot" || pieces[i + 1].Kind != "shot" || pieces[i].SceneIndex != pieces[i + 1].SceneIndex) pieces[i] = pieces[i] with { Fade = o.Fade };

        var plan = new FilmCutPlan(pieces, shots, [], problems, o);
        return plan with { Subtitles = Cues(script, plan) };
    }

    static string Name(string upper) => string.Join(' ', upper.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w.Length > 1 ? char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant() : w));

    /// <summary>Die Stücke des Plans als Bildspur des Videoschnitts.</summary>
    public static List<EditClip> Clips(FilmCutPlan plan) =>
        [.. plan.Pieces.Select((p, i) => new EditClip($"f{i + 1:000}", p.File, p.In, p.Out, p.SourceLength, p.HasAudio, 0, 1, p.Fade))];

    /// <summary>Untertitel aus den Dialogzeilen: Jede Rede kommt zu der Einstellung ihrer Szene, in deren Beschreibung die Figur steht – sonst zur gerade laufenden. Mehrere Reden teilen sich die Zeit der Einstellung.</summary>
    static List<TimedText.Cue> Cues(FountainScript script, FilmCutPlan plan)
    {
        var clips = Clips(plan); var starts = VideoEdit.Starts(clips);
        var byScene = new Dictionary<int, List<(string Who, string Text)>>(); var scene = -1;
        for (var i = 0; i < script.Lines.Count; i++)
        {
            var l = script.Lines[i];
            if (l.Kind == FountainKind.Heading) scene++;
            if (l.Kind != FountainKind.Character || scene < 0) continue;
            var said = string.Join(' ', script.Lines.Skip(i + 1).TakeWhile(x => x.Kind is FountainKind.Dialogue or FountainKind.Parenthetical).Where(x => x.Kind == FountainKind.Dialogue).Select(x => x.Text));
            if (said.Length == 0) continue;
            if (!byScene.TryGetValue(scene, out var list)) byScene[scene] = list = [];
            list.Add((l.Text, said));
        }
        var perPiece = new SortedDictionary<int, List<string>>();
        foreach (var (sc, speeches) in byScene)
        {
            var idx = plan.Pieces.Select((p, i) => (p, i)).Where(x => x.p.Kind == "shot" && x.p.SceneIndex == sc).Select(x => x.i).ToList();
            if (idx.Count == 0) continue;
            var at = 0;
            foreach (var (who, text) in speeches)
            {
                var hit = -1;
                for (var k = at; k < idx.Count && hit < 0; k++)
                    if (plan.Shots.FirstOrDefault(s => s.Id == plan.Pieces[idx[k]].ShotId)?.Shot.Description.Contains(who, StringComparison.OrdinalIgnoreCase) == true) hit = k;
                if (hit >= 0) at = hit;
                if (!perPiece.TryGetValue(idx[at], out var l)) perPiece[idx[at]] = l = [];
                l.Add(text);
            }
        }
        var cues = new List<TimedText.Cue>();
        foreach (var (i, texts) in perPiece)
        {
            var from = starts[i] + (i > 0 ? VideoEdit.FadeOf(clips, i - 1) : 0); var to = starts[i] + clips[i].Length - VideoEdit.FadeOf(clips, i);
            var each = Math.Max(0.4, (to - from) / texts.Count);
            for (var k = 0; k < texts.Count; k++) cues.Add(new(from + k * each, Math.Min(to, from + (k + 1) * each) - 0.05, string.Join('\n', Wrap(texts[k], 42))));
        }
        return cues;
    }

    public static string Srt(FilmCutPlan plan)
    {
        static string T(double s) { var ms = (long)Math.Round(Math.Max(0, s) * 1000, MidpointRounding.AwayFromZero); return $"{ms / 3600000:00}:{ms / 60000 % 60:00}:{ms / 1000 % 60:00},{ms % 1000:000}"; }
        var sb = new StringBuilder(); var n = 1;
        foreach (var c in plan.Subtitles) sb.Append(n++).Append('\n').Append(T(c.Start)).Append(" --> ").Append(T(c.End)).Append('\n').Append(c.Text).Append("\n\n");
        return sb.ToString();
    }

    /// <summary>FFmpeg-Aufruf für eine Texttafel als Bild: dunkler Grund, Zeilen mittig untereinander.</summary>
    public static List<string> CardArgs(IReadOnlyList<FilmCardLine> lines, string font, string output, int w, int h)
    {
        var f = font.Replace('\\', '/').Replace(":", "\\:");
        int Size(string style) => style switch { "titel" => h / 9, "klein" => h / 24, _ => h / 16 };
        var total = lines.Sum(l => Size(l.Style) * 1.45); var y = (h - total) / 2;
        var vf = new List<string>();
        foreach (var l in lines)
        {
            var size = Size(l.Style);
            if (l.Text.Trim().Length > 0)
                vf.Add($"drawtext=fontfile='{f}':text='{VideoEdit.DrawText(l.Text)}':fontcolor={(l.Style switch { "titel" => "0xE0B84A", "klein" => "0xA9AEB8", _ => "white" })}:fontsize={size}:x=(w-text_w)/2:y={((int)Math.Round(y)).ToString(Inv)}");
            y += size * 1.45;
        }
        if (vf.Count == 0) vf.Add("null");
        return ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", $"color=c=0x12151B:s={w}x{h}", "-vf", string.Join(",", vf), "-frames:v", "1", output];
    }

    /// <summary>Fehlende Texttafeln erzeugen (einmal je Inhalt – gleiche Tafeln werden wiederverwendet). Liefert die Tafeln, die nicht entstanden sind.</summary>
    public static List<string> MakeCards(FilmCutPlan plan, Func<IReadOnlyList<string>, Cli.Result> ffmpeg, string? font = null)
    {
        font ??= VideoEdit.FontFile(); var failed = new List<string>();
        foreach (var p in plan.Cards.DistinctBy(c => c.File))
        {
            if (File.Exists(p.File) && new FileInfo(p.File).Length > 0) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(p.File))!);
            if (font is null) { failed.Add($"{p.Label}: keine Schrift gefunden."); continue; }
            var r = ffmpeg(CardArgs(p.Card!, font, p.File, plan.Options.Width, plan.Options.Height));
            if (!r.Ok || !File.Exists(p.File)) failed.Add($"{p.Label}: {(r.Err.Trim().Split('\n').LastOrDefault() is { Length: > 0 } e ? e : "Tafel nicht erzeugt")}");
        }
        return failed;
    }

    /// <summary>Der Rohschnitt als Projekt des Videoschnitts: Bildspur, Marken je Szene und Einstellung, Musik, Sprecherton auf einer zweiten Tonspur.
    /// <paramref name="srtFile"/>: Untertiteldatei, die der Videoschnitt beim Export neben das Video legt.</summary>
    public static JsonObject ToProject(FilmCutPlan plan, string? srtFile = null)
    {
        var o = plan.Options; var p = VideoEdit.NewProject(o.Width, o.Height);
        var clips = Clips(plan); VideoEdit.Store(p, clips);
        var starts = VideoEdit.Starts(clips); var total = VideoEdit.VideoLength(clips);
        var marks = new List<EditMarker>(); var lastScene = -1;
        for (var i = 0; i < clips.Count; i++)
        {
            var pc = plan.Pieces[i];
            if (pc.Kind == "shot")
            {
                var shot = plan.Shots.First(s => s.Id == pc.ShotId);
                if (pc.SceneIndex != lastScene) { lastScene = pc.SceneIndex; marks.Add(new(starts[i], starts[i] + clips[i].Length, "section", shot.Scene.Code + " " + shot.Scene.Location)); }
                marks.Add(new(starts[i], starts[i] + clips[i].Length, "shot", pc.ShotId));
            }
            else marks.Add(new(starts[i], starts[i] + clips[i].Length, "section", pc.Label));
        }
        VideoEdit.SetMarkers(p, marks);
        var music = new List<EditClip>();
        if (o.Music.Length > 0 && o.MusicLength > VideoEdit.MinLength)
            music.Add(new EditClip("musik", o.Music, 0, Math.Min(o.MusicLength, total), o.MusicLength, true, 0, o.MusicVolume));
        for (var i = 0; i < clips.Count; i++)
            if (plan.Pieces[i].Kind == "shot" && plan.Shots.First(s => s.Id == plan.Pieces[i].ShotId).Material is { Voice.Length: > 0, VoiceLength: > 0 } m && File.Exists(m.Voice))
                music.Add(new EditClip($"ton{i + 1:000}", m.Voice, 0, m.VoiceLength, m.VoiceLength, true, starts[i], 1, 0, "", 1));
        VideoEdit.Store(p, music, "music");
        if (music.Any(m => m.Lane == 1)) VideoLanes.Add(p, "audio");
        if (srtFile is not null && plan.Subtitles.Count > 0) p["subs"] = srtFile;
        p["film"] = true; // Herkunft: aus „Filme machen“
        return p;
    }
}

/// <summary>T-0693: Auftrag an die KI für Drehbuch und Shotliste – und die Prüfung der Antwort. Was die KI liefert, wird nur übernommen, wenn es ein lesbares Drehbuch ist.</summary>
public static class FilmWriter
{
    public static string Prompt(string idea, string genre, string current)
    {
        var sb = new StringBuilder("Schreibe ein kurzes Drehbuch für einen Kurzfilm auf Deutsch, im Fountain-Format. Nur das Drehbuch, keine Erklärung davor oder danach.\n\nRegeln:\n")
            .Append("- Erste Zeile „Titel: …“, danach eine Leerzeile.\n- Szenenkopf in Großbuchstaben wie „INNEN. KÜCHE – TAG“ oder „AUSSEN. STRASSE – NACHT“, davor und danach eine Leerzeile.\n")
            .Append("- Figurennamen in Großbuchstaben auf einer eigenen Zeile, die Rede direkt darunter.\n- Unter jedem Szenenkopf zwei bis vier Einstellungen in dieser Schreibweise: „- Totale: Beschreibung (4 s)“. Erlaubte Größen: ")
            .Append(string.Join(", ", ShotPlan.Types)).Append(". Jede Einstellung höchstens 5 Sekunden.\n- Zwei bis vier Szenen. Nur erfundene Figuren, keine echten Personen.\n\n")
            .Append("Idee: ").Append(idea.Trim()).Append('\n');
        if (genre.Trim().Length > 0) sb.Append("Genre: ").Append(genre.Trim()).Append('\n');
        if (current.Trim().Length > 0 && FilmBreakdowns.Of(Fountain.Parse(current)).Scenes.Count > 0) sb.Append("\nBisheriges Drehbuch (überarbeiten und fortführen):\n").Append(current.Trim()).Append('\n');
        return sb.ToString();
    }

    /// <summary>Antwort der KI säubern und prüfen. Liefert das Drehbuch oder einen verständlichen Grund, warum nichts übernommen wird.</summary>
    public static (string? Script, string Message) Accept(string? answer)
    {
        var t = (answer ?? "").Replace("\r\n", "\n").Trim();
        if (t.Length == 0) return (null, "Die KI hat nichts geliefert.");
        var fence = t.IndexOf("```", StringComparison.Ordinal);
        if (fence >= 0)
        {
            var from = t.IndexOf('\n', fence); var to = t.LastIndexOf("```", StringComparison.Ordinal);
            if (from > 0 && to > from) t = t[(from + 1)..to].Trim();
        }
        var b = FilmBreakdowns.Of(Fountain.Parse(t));
        if (b.Scenes.Count == 0) return (null, "Die Antwort der KI ist kein Drehbuch – es fehlt ein Szenenkopf wie „INNEN. KÜCHE – TAG“. Es wurde nichts geändert.");
        if (b.ShotCount == 0 || b.Scenes.Any(s => s.Shots.Count == 0)) t = Fountain.InsertSuggestedShots(t);
        b = FilmBreakdowns.Of(Fountain.Parse(t));
        return (t + "\n", $"{b.Scenes.Count} Szenen, {b.ShotCount} Einstellungen.");
    }
}
