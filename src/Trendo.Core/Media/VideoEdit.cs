using System.Globalization;
using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Ein Stück auf der Zeitleiste: Ausschnitt <see cref="In"/>–<see cref="Out"/> (Sekunden) aus einer Datei.
/// Bildspur: lückenlos hintereinander (Reihenfolge = Liste). Tonspur (Musik): eigene Startzeit und Lautstärke.</summary>
/// <summary><paramref name="Fade"/>: Überblenden ins nächste Stück (Sekunden), <paramref name="Title"/>: eingeblendeter Text.</summary>
public sealed record EditClip(string Id, string File, double In, double Out, double SourceLength, bool HasAudio = true, double Start = 0, double Volume = 1, double Fade = 0, string Title = "", int Lane = 0)
{
    public double Length => Math.Max(0, Out - In);
    public bool IsImage => MediaStudio.IsImage(File);
}

/// <summary>CORE-08 (Masterprompt 131): Videoschnitt-Projekt – Bildspur und Musikspur, Schneiden, Kürzen, Teilen, Verschieben, Export über FFmpeg.
/// Die Daten liegen als JSON im Programmdokument (.tdoc), damit Rückgängig, Autosave und Wiederherstellen der Programm-Hülle greifen.</summary>
/// <summary>E1 (KI-Musikvideo, 04.11.2026): Marke auf der Zeitleiste – Abschnitt („section“), Liedzeile mit Stimme („line“) oder Drehbuch-Einstellung („shot“).</summary>
public sealed record EditMarker(double Start, double End, string Kind, string Label, string Who = "");

public static class VideoEdit
{
    /// <summary>Marken des Projekts (z. B. aus „Lied verstehen“ und dem Drehbuch in Trendo Musik).</summary>
    public static List<EditMarker> Markers(JsonObject p) => (p["markers"] as JsonArray ?? []).OfType<JsonObject>().Select(m => new EditMarker(
        m["start"]?.GetValue<double>() ?? 0, m["end"]?.GetValue<double>() ?? 0, m["kind"]?.GetValue<string>() ?? "", m["label"]?.GetValue<string>() ?? "", m["who"]?.GetValue<string>() ?? "")).ToList();

    public static void SetMarkers(JsonObject p, IEnumerable<EditMarker> markers) =>
        p["markers"] = new JsonArray([.. markers.OrderBy(m => m.Start).Select(m => (JsonNode)new JsonObject { ["start"] = Math.Round(m.Start, 3), ["end"] = Math.Round(m.End, 3), ["kind"] = m.Kind, ["label"] = m.Label, ["who"] = m.Who })]);

    /// <summary>Verknüpftes Lied (Trendo Musik) – Id und Datei; null = freies Projekt.</summary>
    public static (string Id, string File)? Song(JsonObject p) => p["song"]?.GetValue<string>() is { Length: > 0 } id ? (id, p["songFile"]?.GetValue<string>() ?? "") : null;
    public static void LinkSong(JsonObject p, string id, string file) { p["song"] = id; p["songFile"] = file; }

    public const double ImageSeconds = 5, MinLength = 0.2;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static JsonObject NewProject(int width = 1280, int height = 720) => new() { ["clips"] = new JsonArray(), ["music"] = new JsonArray(), ["playhead"] = 0, ["width"] = width, ["height"] = height, ["fps"] = 30 };

    public static List<EditClip> Clips(JsonObject p, string track = "clips") => (p[track] as JsonArray ?? []).OfType<JsonObject>().Select(Read).ToList();
    static EditClip Read(JsonObject o) => ReadBase(o) with { Lane = o["lane"] is JsonValue v && v.TryGetValue<int>(out var lane) ? lane : 0 };
    static EditClip ReadBase(JsonObject o) => new((string?)o["id"] ?? Guid.NewGuid().ToString("N")[..8], (string?)o["file"] ?? "", D(o["in"]), D(o["out"]), D(o["len"]), (bool?)o["audio"] ?? true, D(o["start"]), o["vol"] is null ? 1 : D(o["vol"]), D(o["fade"]), (string?)o["title"] ?? "");
    static double D(JsonNode? n) => n is null ? 0 : double.TryParse(n.GetValueKind() == System.Text.Json.JsonValueKind.String ? n.GetValue<string>() : n.ToJsonString(), NumberStyles.Float, Inv, out var v) ? v : 0;
    static JsonObject Write(EditClip c) { var o = WriteBase(c); if (c.Lane != 0) o["lane"] = c.Lane; return o; } // ältere Projekte bleiben ohne „lane“
    static JsonObject WriteBase(EditClip c) => new() { ["id"] = c.Id, ["file"] = c.File, ["in"] = Math.Round(c.In, 3), ["out"] = Math.Round(c.Out, 3), ["len"] = Math.Round(c.SourceLength, 3), ["audio"] = c.HasAudio, ["start"] = Math.Round(c.Start, 3), ["vol"] = Math.Round(c.Volume, 2), ["fade"] = Math.Round(c.Fade, 2), ["title"] = c.Title };
    public static void Store(JsonObject p, List<EditClip> clips, string track = "clips") => p[track] = new JsonArray([.. clips.Select(c => (JsonNode)Write(c))]);

    /// <summary>Gesamtlänge: Bildspur, mindestens bis zum Ende der Musik.</summary>
    public static double Duration(JsonObject p) => Math.Max(VideoLength(Clips(p)), Clips(p, "music").Select(m => m.Start + m.Length).DefaultIfEmpty(0).Max());
    /// <summary>Länge der Bildspur: Überblendungen überlappen zwei Stücke und verkürzen sie entsprechend.</summary>
    public static double VideoLength(List<EditClip> clips) => clips.Sum(c => c.Length) - Enumerable.Range(0, Math.Max(0, clips.Count - 1)).Sum(i => FadeOf(clips, i));
    /// <summary>Wirksame Überblendung nach Stück i (begrenzt auf die Länge beider Stücke).</summary>
    public static double FadeOf(List<EditClip> clips, int i) => i < 0 || i >= clips.Count - 1 || clips[i].Fade <= 0 ? 0 : Math.Min(clips[i].Fade, Math.Min(clips[i].Length, clips[i + 1].Length) - 0.1) is var f && f > 0.05 ? f : 0;
    /// <summary>Startzeit jedes Bildspur-Stücks auf der Zeitleiste.</summary>
    public static List<double> Starts(List<EditClip> clips) { var t = 0.0; var l = new List<double>(); for (var i = 0; i < clips.Count; i++) { l.Add(t); t += clips[i].Length - FadeOf(clips, i); } return l; }

    /// <summary>Stück unter einer Zeitleisten-Zeit (Bildspur) und die Zeit innerhalb der Quelldatei.</summary>
    public static (int Index, double SourceTime)? At(JsonObject p, double t)
    {
        var clips = Clips(p); var s = Starts(clips);
        for (var i = 0; i < clips.Count; i++) if (t >= s[i] && t < s[i] + clips[i].Length) return (i, clips[i].In + (t - s[i]));
        return null;
    }

    public static EditClip NewClip(string file, double sourceLength, bool hasAudio) =>
        MediaStudio.IsImage(file) ? new(Guid.NewGuid().ToString("N")[..8], file, 0, ImageSeconds, 3600, false) : new(Guid.NewGuid().ToString("N")[..8], file, 0, sourceLength, sourceLength, hasAudio);

    public static void Add(JsonObject p, EditClip c, string track = "clips")
    {
        if (track == "music") c = c with { Start = Duration(p) > 0 && Clips(p, "music").Count > 0 ? Clips(p, "music").Max(m => m.Start + m.Length) : 0 };
        var l = Clips(p, track); l.Add(c); Store(p, l, track);
    }

    /// <summary>Am Zeitpunkt der Zeitleiste in zwei Stücke teilen.</summary>
    public static bool Split(JsonObject p, double t)
    {
        var clips = Clips(p); var s = Starts(clips);
        for (var i = 0; i < clips.Count; i++)
        {
            var local = t - s[i];
            if (local <= MinLength || local >= clips[i].Length - MinLength) continue;
            var c = clips[i]; var cut = c.In + local;
            clips[i] = c with { Out = cut }; clips.Insert(i + 1, c with { Id = Guid.NewGuid().ToString("N")[..8], In = cut });
            Store(p, clips); return true;
        }
        return false;
    }

    /// <summary>Anfang oder Ende eines Stücks verschieben (Sekunden), innerhalb der Quelle und mit Mindestlänge.</summary>
    public static void Trim(JsonObject p, int index, double newIn, double newOut, string track = "clips")
    {
        var l = Clips(p, track); if (index < 0 || index >= l.Count) return;
        var c = l[index];
        var max = c.IsImage ? 3600 : c.SourceLength;
        newIn = Math.Clamp(newIn, 0, max - MinLength); newOut = Math.Clamp(newOut, newIn + MinLength, max);
        l[index] = c with { In = newIn, Out = newOut }; Store(p, l, track);
    }

    /// <summary>Abschnitte (Quellzeit) aus einem Stück herausschneiden – das Stück zerfällt in die Teile dazwischen (z. B. Stille entfernen).</summary>
    public static int CutOut(JsonObject p, int index, IEnumerable<(double Start, double End)> ranges)
    {
        var l = Clips(p); if (index < 0 || index >= l.Count) return 0;
        var c = l[index]; var parts = new List<EditClip>(); var t = c.In; var removed = 0;
        foreach (var (a0, b0) in ranges.OrderBy(r => r.Start))
        {
            var a = Math.Max(a0, c.In); var b = Math.Min(b0, c.Out); if (b - a < MinLength) continue;
            if (a - t >= MinLength) parts.Add(c with { Id = parts.Count == 0 ? c.Id : Guid.NewGuid().ToString("N")[..8], In = t, Out = a });
            t = b; removed++;
        }
        if (removed == 0) return 0;
        if (c.Out - t >= MinLength) parts.Add(c with { Id = parts.Count == 0 ? c.Id : Guid.NewGuid().ToString("N")[..8], In = t });
        l.RemoveAt(index); l.InsertRange(index, parts); Store(p, l); return removed;
    }

    public static void Move(JsonObject p, int from, int to)
    {
        var l = Clips(p); if (from < 0 || from >= l.Count) return;
        to = Math.Clamp(to, 0, l.Count - 1); var c = l[from]; l.RemoveAt(from); l.Insert(to, c); Store(p, l);
    }

    public static void Remove(JsonObject p, int index, string track = "clips") { var l = Clips(p, track); if (index >= 0 && index < l.Count) { l.RemoveAt(index); Store(p, l, track); } }

    /// <summary>Überblenden ins nächste Stück und Texteinblendung setzen.</summary>
    public static void SetClip(JsonObject p, int index, double fade, string title)
    {
        var l = Clips(p); if (index < 0 || index >= l.Count) return;
        l[index] = l[index] with { Fade = Math.Clamp(fade, 0, 5), Title = (title ?? "").Trim() }; Store(p, l);
    }

    /// <summary>Schrift für Texteinblendungen (Windows: Arial/Segoe UI, Linux: DejaVu Sans).</summary>
    public static string? FontFile() => new[] { @"C:\Windows\Fonts\arial.ttf", @"C:\Windows\Fonts\segoeui.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/TTF/DejaVuSans.ttf" }.FirstOrDefault(System.IO.File.Exists);
    /// <summary>Text für FFmpeg-drawtext maskieren (Doppelpunkt, Prozent, Backslash; Apostroph typografisch).</summary>
    public static string DrawText(string t) => t.Replace("\\", "\\\\").Replace("'", "’").Replace(":", "\\:").Replace("%", "\\%");

    public static void SetMusic(JsonObject p, int index, double start, double volume)
    {
        var l = Clips(p, "music"); if (index < 0 || index >= l.Count) return;
        l[index] = l[index] with { Start = Math.Max(0, start), Volume = Math.Clamp(volume, 0, 2) }; Store(p, l, "music");
    }

    static string F(double v) => v.ToString("0.###", Inv);

    /// <summary>FFmpeg-Aufruf für den Export: jedes Stück zugeschnitten, auf gleiche Größe und Bildrate gebracht, aneinandergehängt;
    /// Stücke ohne Ton bekommen Stille; Musik mit Startzeit und Lautstärke darübergemischt. H.264 + AAC, MP4.</summary>
    public static List<string> ExportArgs(JsonObject p, string output, VideoExport.Options? o = null)
    {
        var clips = Clips(p); var music = Clips(p, "music").Where(m => VideoLanes.IsOn(p, "audio", m.Lane)).ToList();
        if (clips.Count == 0) throw new InvalidOperationException("Die Bildspur ist leer – zuerst Videos oder Bilder hinzufügen.");
        if (clips.FirstOrDefault(c => !System.IO.File.Exists(c.File)) is { } miss) throw new FileNotFoundException("Datei fehlt: " + Path.GetFileName(miss.File));
        int w = (int)D(p["width"]), h = (int)D(p["height"]), fps = (int)Math.Max(1, D(p["fps"])); if (w < 16) w = 1280; if (h < 16) h = 720;
        var a = new List<string> { "-hide_banner", "-loglevel", "error", "-y" };
        var f = new List<string>(); var input = 0; var concat = ""; var ids = new List<int>();
        foreach (var c in clips)
        {
            if (c.IsImage) a.AddRange(["-loop", "1", "-framerate", fps.ToString(Inv), "-t", F(c.Length), "-i", c.File]);
            else a.AddRange(["-ss", F(c.In), "-t", F(c.Length), "-i", c.File]);
            var vi = input++;
            var fx = VideoPro.Fx(p, c.Id); // Profi 23: Farbe, Titellage und -größe je Stück
            var title = c.Title.Length > 0 && FontFile() is { } font
                ? $",drawtext=fontfile='{font.Replace("\\", "/").Replace(":", "\\:")}':text='{DrawText(c.Title)}':fontcolor=white:fontsize=h/14*{F(fx.TitleSize)}:borderw=3:bordercolor=black@0.6:x=(w-text_w)/2:y={VideoPro.TitleY(fx.TitlePos)}" : "";
            f.Add($"[{vi}:v]scale={w}:{h}:force_original_aspect_ratio=decrease,pad={w}:{h}:(ow-iw)/2:(oh-ih)/2,setsar=1,fps={fps},format=yuv420p{VideoPro.ColorFilter(fx)}{VideoGrade.Filter(p, c.Id)}{VideoAnim.StackFilter(p, c.Id)}{title}[{(VideoAnim.HasTransform(p, c.Id) ? "vt" : "v")}{vi}]"); // V6: Effektstapel nach der Farbe
            if (VideoAnim.HasTransform(p, c.Id)) f.AddRange(VideoAnim.ClipTransform(p, c.Id, $"vt{vi}", $"v{vi}", w, h, fps, c.Length)); // V5: Bewegung, Größe, Deckkraft
            string ai;
            if (c.HasAudio && !c.IsImage) { ai = $"{vi}:a"; }
            else { a.AddRange(["-f", "lavfi", "-t", F(c.Length), "-i", "anullsrc=r=48000:cl=stereo"]); ai = $"{input++}:a"; }
            f.Add($"[{ai}]aresample=48000,aformat=channel_layouts=stereo{(Math.Abs(c.Volume * VideoLanes.BaseGain(p) - 1) > 0.001 ? $",volume={F(c.Volume * VideoLanes.BaseGain(p))}" : "")}{VideoAnim.VolumeFilter(p, c.Id)}[a{vi}]"); // Tonpegel je Stück, V5: Verlauf
            concat += $"[v{vi}][a{vi}]"; ids.Add(vi);
        }
        if (clips.Select((_, i) => FadeOf(clips, i)).All(x => x <= 0)) f.Add($"{concat}concat=n={clips.Count}:v=1:a=1[vbase][abase]");
        else
        {
            // Überblendungen: paarweise Kette, xfade (Bild) und acrossfade (Ton); ohne Überblendung wird einfach angehängt
            string cv = $"v{ids[0]}", ca = $"a{ids[0]}"; var len = clips[0].Length;
            for (var i = 1; i < clips.Count; i++)
            {
                var fd = FadeOf(clips, i - 1); var nv = i == clips.Count - 1 ? "vbase" : $"xv{i}"; var na = i == clips.Count - 1 ? "abase" : $"xa{i}";
                if (fd > 0)
                {
                    f.Add($"[{cv}][v{ids[i]}]xfade=transition={VideoPro.Fx(p, clips[i - 1].Id).Transition}:duration={F(fd)}:offset={F(len - fd)}[{nv}]"); // Übergangsart je Stück
                    f.Add($"[{ca}][a{ids[i]}]acrossfade=d={F(fd)}[{na}]");
                    len += clips[i].Length - fd;
                }
                else { f.Add($"[{cv}][{ca}][v{ids[i]}][a{ids[i]}]concat=n=2:v=1:a=1[{nv}][{na}]"); len += clips[i].Length; }
                cv = nv; ca = na;
            }
            if (clips.Count == 1) { f.Add($"[{cv}]null[vbase]"); f.Add($"[{ca}]anull[abase]"); }
        }
        a.AddRange(VideoPro.OverlayGraph(p, f, "vbase", "vout", ref input, w, h, fps)); // zweite Bildspur: Bild im Bild, Logos, Einblendungen
        var mix = "[abase]"; var n = 1;
        foreach (var m in music)
        {
            if (!System.IO.File.Exists(m.File)) throw new FileNotFoundException("Datei fehlt: " + Path.GetFileName(m.File));
            a.AddRange(["-ss", F(m.In), "-t", F(m.Length), "-i", m.File]);
            var ms = (long)Math.Round(m.Start * 1000);
            f.Add($"[{input++}:a]aresample=48000,aformat=channel_layouts=stereo,volume={F(m.Volume * VideoLanes.Gain(p, m.Lane))}{VideoAnim.VolumeFilter(p, m.Id)},adelay={ms}|{ms}[m{n}]");
            mix += $"[m{n}]"; n++;
        }
        var total = VideoLength(clips);
        o ??= new VideoExport.Options(VideoExport.Presets[0]);
        if (n > 1 && o.Ducking)
        {
            // X11 Ducking: Musik wird leiser, solange im Video gesprochen wird (Seitenkette über den Originalton)
            var musicIn = mix["[abase]".Length..];
            f.Add(n > 2 ? $"{musicIn}amix=inputs={n - 1}:duration=longest:normalize=0[mall]" : $"{musicIn}anull[mall]");
            f.Add("[abase]asplit=2[ab1][ab2]");
            f.Add("[mall][ab2]sidechaincompress=threshold=0.04:ratio=8:attack=20:release=500:makeup=1[mduck]");
            f.Add("[ab1][mduck]amix=inputs=2:duration=first:normalize=0[aout]");
        }
        else f.Add(n > 1 ? $"{mix}amix=inputs={n}:duration=first:normalize=0[aout]" : "[abase]anull[aout]");
        var (vo, ao) = VideoExport.Tail(f, "vout", "aout", w, h, o); // X1/X11: Bereich, Zielgröße, Lautheit
        var outLen = VideoExport.Length(total, o);
        if (o.Preset.AudioOnly)
        {
            f.Add($"[{vo}]nullsink"); // Bild verwerfen – ein offener Ausgang bricht FFmpeg ab
            a.AddRange(["-filter_complex", string.Join(";", f), "-map", $"[{ao}]", "-t", F(outLen), "-vn", "-c:a", "aac", "-b:a", "256k", "-movflags", "+faststart", output]);
            return a;
        }
        var (_, enc) = VideoExport.Encoder(o.Preset, o.Gpu, outLen); // X2: NVENC, wenn die Grafikkarte es kann
        if (o.Preset.Sequence)
        {
            // Paket 01 S3-18: Einzelbilder und Ton getrennt in einen Ordner
            var (frames, wav) = VideoExport.SequencePaths(output, o.Preset);
            a.AddRange(["-filter_complex", string.Join(";", f), "-map", $"[{vo}]", "-t", F(outLen), .. enc, "-start_number", "1", frames,
                "-map", $"[{ao}]", "-t", F(outLen), "-c:a", "pcm_s24le", wav]);
            return a;
        }
        var pro = o.Preset.Codec == "prores";
        a.AddRange(["-filter_complex", string.Join(";", f), "-map", $"[{vo}]", "-map", $"[{ao}]", "-t", F(outLen), .. enc]);
        if (!pro) a.AddRange(["-pix_fmt", "yuv420p"]);
        if (pro) a.AddRange(["-c:a", "pcm_s24le"]); else a.AddRange(["-c:a", "aac", "-b:a", o.Preset.MaxMb > 0 ? "96k" : "192k"]);
        a.AddRange(["-movflags", "+faststart", output]);
        return a;
    }

    /// <summary>X15: fehlende Quelldateien (verschoben oder umbenannter Ordner).</summary>
    public static List<string> Missing(JsonObject p) =>
        Clips(p).Concat(Clips(p, "music")).Select(c => c.File).Where(f => !System.IO.File.Exists(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>X15: fehlende Dateien im Ordner (mit Unterordnern) nach Dateinamen suchen und neu verknüpfen. Liefert die Anzahl der gefundenen Dateien.</summary>
    public static int Relink(JsonObject p, string folder)
    {
        var missing = Missing(p); if (missing.Count == 0 || !Directory.Exists(folder)) return 0;
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 8 }))
            byName.TryAdd(Path.GetFileName(f), f);
        var map = missing.Select(m => (m, byName.GetValueOrDefault(Path.GetFileName(m)))).Where(x => x.Item2 is not null).ToDictionary(x => x.m, x => x.Item2!, StringComparer.OrdinalIgnoreCase);
        foreach (var track in new[] { "clips", "music" })
            foreach (var c in (p[track] as JsonArray ?? []).OfType<JsonObject>())
                if (c["file"]?.GetValue<string>() is { } f && map.TryGetValue(f, out var to)) c["file"] = to;
        return map.Count;
    }

    /// <summary>Export ausführen; prüft danach, dass eine Datei mit Inhalt entstanden ist.</summary>
    public static string Export(string ffmpeg, JsonObject p, string output, int timeoutMs = 1800000, VideoExport.Options? o = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        if (o?.Preset.Sequence == true) Directory.CreateDirectory(output);
        var r = Cli.Run(ffmpeg, ExportArgs(p, output, o), Path.GetDirectoryName(Path.GetFullPath(output)), timeoutMs);
        if (!r.Ok || !VideoExport.Produced(output, o?.Preset ?? VideoExport.Presets[0]))
            throw new InvalidOperationException("Export hat nicht geklappt: " + (r.Err.Length > 0 ? r.Err.Trim().Split('\n')[^1] : $"FFmpeg-Code {r.Exit}"));
        return output;
    }

    /// <summary>Export im Hintergrund: Fortschritt aus FFmpeg (-progress), Abbrechen beendet nur den eigenen FFmpeg-Prozess und löscht die halbe Datei.</summary>
    public static async Task<string> ExportAsync(string ffmpeg, JsonObject p, string output, IProgress<double>? progress = null, CancellationToken ct = default, VideoExport.Options? o = null)
    {
        var args = ExportArgs(p, output, o); args.InsertRange(4, ["-progress", "pipe:1", "-nostats"]);
        var total = Math.Max(0.1, o is null ? VideoLength(Clips(p)) : VideoExport.Length(VideoLength(Clips(p)), o));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        if (o?.Preset.Sequence == true) Directory.CreateDirectory(output);
        var psi = new System.Diagnostics.ProcessStartInfo(ffmpeg) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg ließ sich nicht starten.");
        var err = proc.StandardError.ReadToEndAsync(ct);
        try
        {
            while (await proc.StandardOutput.ReadLineAsync(ct) is { } line)
                if ((line.StartsWith("out_time_us=") || line.StartsWith("out_time_ms=")) && long.TryParse(line[(line.IndexOf('=') + 1)..], out var us) && us >= 0)
                    progress?.Report(Math.Clamp(us / 1e6 / total, 0, 1));
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { if (!proc.HasExited) proc.Kill(true); } catch (InvalidOperationException) { }
            await Task.Delay(200, CancellationToken.None);
            try { if (System.IO.File.Exists(output)) System.IO.File.Delete(output); else if (o?.Preset.Sequence == true && Directory.Exists(output)) Directory.Delete(output, true); } catch (IOException) { }
            throw;
        }
        var e = await err;
        if (proc.ExitCode != 0 || !VideoExport.Produced(output, o?.Preset ?? VideoExport.Presets[0]))
            throw new InvalidOperationException("Export hat nicht geklappt: " + (e.Trim().Length > 0 ? e.Trim().Split('\n')[^1] : $"FFmpeg-Code {proc.ExitCode}"));
        progress?.Report(1);
        return output;
    }

    /// <summary>Vorschaubilder fürs Abspielen im Programm: ein FFmpeg-Lauf je Stück, <paramref name="fps"/> Bilder je Sekunde, 320 px breit, zwischengespeichert.</summary>
    public static IReadOnlyList<string> PlayFrames(string ffmpeg, EditClip c, string dir, int fps = 6)
    {
        Directory.CreateDirectory(dir);
        var have = Directory.GetFiles(dir, "f*.jpg").OrderBy(x => x, StringComparer.Ordinal).ToList();
        if (have.Count > 0) return have;
        if (c.IsImage) return [];
        Cli.Run(ffmpeg, ["-hide_banner", "-loglevel", "error", "-y", "-ss", F(c.In), "-t", F(c.Length), "-i", c.File, "-vf", $"fps={fps},scale=320:-2", "-q:v", "5", Path.Combine(dir, "f%05d.jpg")], dir, 300000);
        return Directory.GetFiles(dir, "f*.jpg").OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    /// <summary>Hat die Datei eine Tonspur? (ffprobe neben ffmpeg)</summary>
    public static bool HasAudioStream(string ffmpeg, string file)
    {
        var probe = Path.Combine(Path.GetDirectoryName(ffmpeg) ?? "", Path.GetFileName(ffmpeg).Replace("ffmpeg", "ffprobe"));
        var r = Cli.Run(System.IO.File.Exists(probe) ? probe : "ffprobe", ["-v", "error", "-select_streams", "a", "-show_entries", "stream=index", "-of", "csv=p=0", file], null, 30000);
        return r.Ok && r.Out.Trim().Length > 0;
    }
}
