using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>X17 (04.11.2026): jedes Speichern behält den vorigen Stand, höchstens 20, Wiederherstellen ist rückgängig machbar.</summary>
public class ProgramVersionsTests
{
    [Fact(DisplayName = "Versionen: alter Stand bleibt beim Speichern, gleiche Stände nicht doppelt, höchstens 20, Wiederherstellen per Rückgängig zurück")]
    public void Versions()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-ver-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(dir);
        try
        {
            var f = Path.Combine(dir, "Projekt.tdoc");
            var d = new ProgramDocument("CORE-08", "Projekt"); d.Set("text", "eins"); d.Save(f);
            Assert.Empty(ProgramDocument.Versions(f));
            d.Set("text", "zwei"); d.Save(f);
            Assert.Single(ProgramDocument.Versions(f));
            d.Save(f); d.Save(f); // nichts geändert: kein weiterer Stand
            Assert.Single(ProgramDocument.Versions(f));
            var oldest = ProgramDocument.Versions(f)[^1].File;
            Assert.Equal("eins", ProgramDocument.Load(oldest).Text("text"));
            d.RestoreVersion(oldest);
            Assert.Equal("eins", d.Text("text")); Assert.Equal("Version wiederherstellen", d.UndoLabel);
            d.Undo(); Assert.Equal("zwei", d.Text("text"));
            for (var i = 0; i < 30; i++) { d.Set("text", "v" + i); d.Save(f); }
            Assert.Equal(ProgramDocument.MaxVersions, ProgramDocument.Versions(f).Count);
        }
        finally { Directory.Delete(dir, true); }
    }
}
