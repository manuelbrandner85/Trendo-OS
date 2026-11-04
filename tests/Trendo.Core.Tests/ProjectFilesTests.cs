using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Projektmappe (04.11.2026): Name → ID, Dateien, Ordner, Notiz, Umbenennen – alles innerhalb der Projektgrenze.</summary>
public class ProjectFilesTests
{
    [Fact(DisplayName = "Projektmappe: lesbarer Name wird ID, belegte bekommt Nummer, Dateien und Ordner, Info, Notiz, Umbenennen, kein Ausbruch")]
    public void Files()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-proj-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var audit = new AuditLog(Path.Combine(root, "audit.jsonl"));
            var s = new ProjectStore(Path.Combine(root, "p"), audit);
            Assert.Equal("urlaub-2026", ProjectStore.Slug("Urlaub 2026!")); Assert.Equal("gruesse-aus-koeln", ProjectStore.Slug("Grüße aus Köln"));
            var a = s.CreateNamed("Urlaub 2026"); var b = s.CreateNamed("Urlaub 2026");
            Assert.Equal(("urlaub-2026", "Urlaub 2026"), (a.Id, a.Name)); Assert.Equal("urlaub-2026-2", b.Id);
            var src = Path.Combine(root, "foto.jpg"); File.WriteAllBytes(src, new byte[1234]);
            Assert.Equal("foto.jpg", s.AddFile(a.Id, src)); Assert.Equal("foto (2).jpg", s.AddFile(a.Id, src));
            s.NewFolder(a.Id, "Bilder"); Assert.Equal("Bilder/foto.jpg", s.AddFile(a.Id, src, "Bilder"));
            var files = s.Files(a.Id);
            Assert.Equal("Bilder", files[0].Name); Assert.True(files[0].IsDir); Assert.Equal(1, files[1].Depth);
            var info = s.Info(a.Id); Assert.Equal(3, info.Files); Assert.Equal(3 * 1234, info.Bytes); Assert.Equal([".jpg"], info.Kinds);
            s.SetNote(a.Id, "Fotos sortieren"); Assert.Equal("Fotos sortieren", s.Note(a.Id));
            Assert.Equal("Sommer 2026", s.Rename(a.Id, "Sommer 2026").Name); Assert.Equal("Sommer 2026", s.List().First(p => p.Id == a.Id).Name);
            Assert.Throws<ProjectIsolationException>(() => s.NewFolder(a.Id, "../draussen"));
            Assert.Throws<InvalidOperationException>(() => s.NewFolder(a.Id, "Bilder"));
            Assert.Throws<ArgumentException>(() => s.CreateNamed("  "));
        }
        finally { Directory.Delete(root, true); }
    }
}
