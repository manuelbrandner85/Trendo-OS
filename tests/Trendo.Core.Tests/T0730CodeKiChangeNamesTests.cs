using Trendo.Core.Kreativ;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0730: Sperrliste für Zugangsdaten- und Git-Dateien lässt sich nicht mit Punkt/Leerzeichen am Namensende oder alternativen Datenströmen umgehen
/// (Windows legt „geheim.env.“ als „geheim.env“ an).
public class T0730CodeKiChangeNamesTests
{
    [Fact(DisplayName = "T-0730: CodeKiChange sperrt „geheim.env.“, „.git “, „x.pem:stream“ und Verwandte – normale Dateien bleiben erlaubt")]
    public void Blocked_normalisiert_Namen()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-t0730-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d);
        try
        {
            foreach (var rel in new[] { "geheim.env.", "geheim.env", "geheim.env . .", "GEHEIM.ENV.", ".env.", ".env ", ".git ", ".git /config", ".git./config", ".GIT/config",
                         "x.pem:stream", "x.pem.", "x.PEM ", "rechner.py:verborgen", "konfig/server.key.", "id_rsa.", "secrets.json ", "node_modules./a.js", "...", "a/ /b.py" })
                Assert.True(CodeKiChange.Blocked(d, rel) is not null, $"„{rel}“ hätte gesperrt sein müssen");
            Assert.Null(CodeKiChange.Blocked(d, "rechner.py"));
            Assert.Null(CodeKiChange.Blocked(d, "src/umgebung.py"));
            Assert.Null(CodeKiChange.Blocked(d, ".gitignore"));
        }
        finally { Directory.Delete(d, true); }
    }
}
