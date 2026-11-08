using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Trendo.Core.Media;

/// <summary>Alle Studios, Runde 1 – CORE-08 „Videos schneiden“: Trendo-Schnitt als MLT-XML (MLT 7, LGPL – nur als Dateiformat und über das getrennte Programm melt).
/// Shotcut und melt öffnen die Datei direkt; melt aus dem mitgelieferten Kdenlive rendert sie ohne Fenster.
/// Übernommen: Bildspur (Schnitte, Bilder mit Dauer), Musikspur (Startzeit, Lautstärke), Bildgröße und Bildrate.
/// Nicht übernommen (meldet <see cref="Limits"/>): Überblendungen und eingeblendete Texte – die rendert Trendo selbst mit FFmpeg.</summary>
public static class MltProject
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static int Frames(double seconds, int fps) => (int)Math.Round(seconds * fps, MidpointRounding.AwayFromZero);

    public static string ToXml(JsonObject p, string title = "Trendo-Schnitt")
    {
        int w = (int?)p["width"] ?? 1280, h = (int?)p["height"] ?? 720, fps = Math.Clamp((int?)p["fps"] ?? 30, 1, 120);
        if (w is < 16 or > 8192 || h is < 16 or > 8192) throw new ArgumentException("Bildgröße außerhalb von 16–8192 Pixeln.");
        var clips = VideoEdit.Clips(p).Where(c => c.Length > 0).ToList(); var music = VideoEdit.Clips(p, "music").Where(c => c.Length > 0).ToList();
        if (clips.Count == 0) throw new ArgumentException("Die Bildspur ist leer – nichts zu speichern.");
        var root = new XElement("mlt", new XAttribute("LC_NUMERIC", "C"), new XAttribute("version", "7.0.0"), new XAttribute("title", title), new XAttribute("producer", "main"));
        root.Add(new XElement("profile", new XAttribute("description", $"Trendo {w}x{h} {fps} fps"), new XAttribute("width", w), new XAttribute("height", h),
            new XAttribute("progressive", 1), new XAttribute("sample_aspect_num", 1), new XAttribute("sample_aspect_den", 1), new XAttribute("display_aspect_num", w), new XAttribute("display_aspect_den", h),
            new XAttribute("frame_rate_num", fps), new XAttribute("frame_rate_den", 1), new XAttribute("colorspace", 709)));
        static XElement Prop(string n, object v) => new("property", new XAttribute("name", n), Convert.ToString(v, Inv));

        var video = new XElement("playlist", new XAttribute("id", "bildspur"));
        for (var i = 0; i < clips.Count; i++)
        {
            var c = clips[i]; var id = $"bild{i}";
            if (c.IsImage)
            {
                var len = Math.Max(1, Frames(c.Length, fps));
                root.Add(new XElement("producer", new XAttribute("id", id), new XAttribute("in", 0), new XAttribute("out", len - 1),
                    Prop("resource", c.File), Prop("length", len), Prop("ttl", 1)));
                video.Add(new XElement("entry", new XAttribute("producer", id), new XAttribute("in", 0), new XAttribute("out", len - 1)));
            }
            else
            {
                int a = Frames(c.In, fps), b = Math.Max(a, Frames(c.Out, fps) - 1);
                var prod = new XElement("producer", new XAttribute("id", id), Prop("resource", c.File));
                if (!c.HasAudio) prod.Add(Prop("audio_index", -1));
                root.Add(prod);
                video.Add(new XElement("entry", new XAttribute("producer", id), new XAttribute("in", a), new XAttribute("out", b)));
            }
        }
        root.Add(video);

        var tracks = new List<XElement> { new("track", new XAttribute("producer", "bildspur")) };
        if (music.Count > 0)
        {
            var pl = new XElement("playlist", new XAttribute("id", "musik")); var pos = 0;
            for (var i = 0; i < music.Count; i++)
            {
                var m = music.OrderBy(x => x.Start).ElementAt(i); var id = $"musik{i}";
                var start = Frames(m.Start, fps); if (start > pos) { pl.Add(new XElement("blank", new XAttribute("length", start - pos))); pos = start; }
                int a = Frames(m.In, fps), b = Math.Max(a, Frames(m.Out, fps) - 1);
                var prod = new XElement("producer", new XAttribute("id", id), Prop("resource", m.File), Prop("video_index", -1));
                if (Math.Abs(m.Volume - 1) > 0.001) prod.Add(new XElement("filter", Prop("mlt_service", "volume"), Prop("gain", Math.Round(Math.Clamp(m.Volume, 0, 4), 3))));
                root.Add(prod); pl.Add(new XElement("entry", new XAttribute("producer", id), new XAttribute("in", a), new XAttribute("out", b))); pos += b - a + 1;
            }
            root.Add(pl); tracks.Add(new XElement("track", new XAttribute("producer", "musik"), new XAttribute("hide", "video")));
        }
        var tractor = new XElement("tractor", new XAttribute("id", "main"), new XElement("multitrack", tracks));
        if (music.Count > 0) tractor.Add(new XElement("transition", Prop("mlt_service", "mix"), Prop("a_track", 0), Prop("b_track", 1), Prop("always_active", 1), Prop("sum", 1)));
        root.Add(tractor);
        return new XDocument(new XDeclaration("1.0", "utf-8", null), root).ToString();
    }

    /// <summary>Was das MLT-Projekt nicht enthält (für den Hinweis in der Oberfläche).</summary>
    public static IReadOnlyList<string> Limits(JsonObject p)
    {
        var c = VideoEdit.Clips(p); var l = new List<string>();
        if (c.Any(x => x.Fade > 0)) l.Add("Überblendungen");
        if (c.Any(x => x.Title.Length > 0)) l.Add("eingeblendete Texte");
        return l;
    }

    /// <summary>melt-Argumente zum Rendern (H.264/AAC in MP4) – Dateinamen als eigene Argumente.
    /// terminate_on_pause=1: ohne bleibt melt am Ende stehen und wartet auf Tasten (im echten Lauf 08.11.2026 gehangen); real_time=-1: kein Auslassen von Bildern.</summary>
    public static IReadOnlyList<string> RenderArgs(string mltFile, string output) =>
        [mltFile, "-consumer", "avformat:" + output, "vcodec=libx264", "preset=veryfast", "crf=20", "acodec=aac", "ab=192k", "terminate_on_pause=1", "real_time=-1", "-silent"];
}
