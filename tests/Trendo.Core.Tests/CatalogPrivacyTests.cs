using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Der Programmkatalog geht ins öffentliche Repo – Pfade im Benutzerordner dürfen keinen Benutzernamen verraten.</summary>
public class CatalogPrivacyTests
{
    [Fact(DisplayName = "Katalog: Benutzerordner in Pfaden wird zu C:\\Users\\<user>, andere Pfade und schon bereinigte bleiben")]
    public void Personal_paths_are_masked()
    {
        Assert.Equal(@"Lokaler Datenträger (C:\Users\<user>\AppData\Local\Temp\trendo-render\x)", ProgramCatalog.NoPersonalPaths(@"Lokaler Datenträger (C:\Users\Erika.Muster\AppData\Local\Temp\trendo-render\x)"));
        Assert.Equal(@"D:\Users\<user>\a und C:\Users\<user>\b", ProgramCatalog.NoPersonalPaths(@"D:\Users\max\a und C:\Users\<user>\b"));
        Assert.Equal(@"C:\Programme\Trendo", ProgramCatalog.NoPersonalPaths(@"C:\Programme\Trendo"));
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.DoesNotContain(Path.GetFileName(home), ProgramCatalog.NoPersonalPaths(Path.Combine(home, "Musik", "lied.mp3")).Replace("Musik", "").Replace("lied.mp3", ""));
    }
}
