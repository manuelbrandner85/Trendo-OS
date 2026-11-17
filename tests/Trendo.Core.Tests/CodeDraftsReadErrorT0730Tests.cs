using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0730: Ein vorübergehender Lesefehler (Datei gesperrt) löscht einen Code-Entwurf nicht mehr als „kaputt“; wirklich kaputte Entwürfe werden weiter aufgeräumt.</summary>
public class CodeDraftsReadErrorT0730Tests
{
    [Fact(DisplayName = "T-0730: Gesperrter Entwurf bleibt erhalten und wird danach wieder angeboten; unlesbarer Inhalt wird gelöscht")]
    public void Locked_draft_is_kept()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-drafts-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var data = Path.Combine(dir, "daten"); var proj = Path.Combine(dir, "proj"); Directory.CreateDirectory(proj);
            var file = Path.Combine(proj, "a.py"); File.WriteAllText(file, "print(1)\n");
            CodeDrafts.Save(data, proj, file, "print(2)\n", CodeDrafts.ShaOf(file));
            var draft = Assert.Single(Directory.GetFiles(Path.Combine(data, "code-editor", "entwuerfe"), "*.json"));
            using (new FileStream(draft, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) // anderer Prozess hält die Datei gerade
            {
                Assert.Empty(CodeDrafts.All(data)); // nicht lesbar → jetzt nicht anbieten …
                Assert.True(File.Exists(draft));    // … aber auch nicht löschen
            }
            Assert.Equal("print(2)\n", Assert.Single(CodeDrafts.All(data)).Text);

            File.WriteAllText(draft, "{ kein json");
            Assert.Empty(CodeDrafts.All(data));
            Assert.False(File.Exists(draft)); // wirklich kaputt → aufgeräumt
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
