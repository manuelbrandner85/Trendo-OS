using System.Text.Json.Nodes;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Pilot 4.0, Funktionssicherung: ein Projekt im alten Format (ohne Spuren-Angaben) muss unverändert laden und denselben Export ergeben wie vor dem Umbau.</summary>
public class VideoBaselineTests
{
    const string Marker = "@@DIR@@";

    static string RepoFile(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException(rel);
    }

    static (JsonObject Project, string Dir) Load()
    {
        var dir = Directory.CreateTempSubdirectory("trendo-baseline").FullName;
        foreach (var f in new[] { "a.mp4", "b.mp4", "logo.png", "musik.mp3", "stimme.wav" }) File.WriteAllBytes(Path.Combine(dir, f), [1, 2, 3]);
        var doc = JsonNode.Parse(File.ReadAllText(RepoFile("tests/Trendo.Core.Tests/Pilot40/altes-projekt.tdoc")).Replace(Marker, dir.Replace("\\", "/")))!.AsObject();
        return ((JsonObject)doc["data"]!, dir);
    }

    [Fact(DisplayName = "Funktionssicherung: altes Projekt lädt mit allen Stücken, Musik und Einblendungen")]
    public void Old_project_loads()
    {
        var (p, dir) = Load();
        try
        {
            Assert.Equal(2, VideoEdit.Clips(p).Count);
            Assert.Equal(2, VideoEdit.Clips(p, "music").Count);
            Assert.Single(VideoPro.Overlays(p));
            Assert.Equal(13.5, VideoEdit.VideoLength(VideoEdit.Clips(p)), 3);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Funktionssicherung: Laden und Speichern verändert die Spuren eines alten Projekts nicht")]
    public void Roundtrip_keeps_json()
    {
        var (p, dir) = Load();
        try
        {
            var clips = VideoEdit.Clips(p); var music = VideoEdit.Clips(p, "music");
            VideoEdit.Store(p, VideoEdit.Clips(p));
            VideoEdit.Store(p, VideoEdit.Clips(p, "music"), "music");
            var o = VideoPro.Overlays(p)[0];
            VideoPro.SetOverlay(p, o.Id, o.Start, o.Length, o.X, o.Y, o.Width, o.Opacity);
            Assert.Equal(clips, VideoEdit.Clips(p));
            Assert.Equal(music, VideoEdit.Clips(p, "music"));
            Assert.DoesNotContain("lane", p.ToJsonString());
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Funktionssicherung: Export eines alten Projekts ergibt exakt denselben ffmpeg-Aufruf wie vor dem Umbau")]
    public void Export_args_unchanged()
    {
        var (p, dir) = Load();
        try
        {
            var text = string.Join("\n", VideoEdit.ExportArgs(p, Path.Combine(dir, "aus.mp4"))).Replace(dir.Replace("\\", "/"), Marker).Replace(dir, Marker).Replace("\r", "");
            var golden = RepoFile("tests/Trendo.Core.Tests/Pilot40/export-altes-projekt.txt");
            if (Environment.GetEnvironmentVariable("TRENDO_UPDATE_GOLDEN") == "1") File.WriteAllText(golden, text);
            Assert.Equal(File.ReadAllText(golden).Replace("\r", ""), text);
        }
        finally { Directory.Delete(dir, true); }
    }
}
