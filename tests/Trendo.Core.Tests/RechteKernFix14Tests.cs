using System.Text.RegularExpressions;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764, 14. Prüfrunde: Die Rolle eines Profils kommt aus einer geschützten Quelle (<see cref="ProfileRole"/>: rolle.json bzw. personen.json
/// im Hauptordner), nicht aus settings.json. Einlesen übernimmt nie Rolle, Person, Ordner oder Sperren. Fail-closed. Quelltext-Wächter für alle
/// Stellen, die Rolle, Projektordner oder die Einstellungen als Ganzes ersetzen.</summary>
[Collection("ChildWrite")]
public class RechteKernFix14
{
    static string Repo(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException(rel);
    }

    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-f14-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0764 H1/H2 (Kern): Hauptprofil – Rolle aus rolle.json; settings.json mit Child:false, gelöscht oder ersetzt macht kein Erwachsenen-Profil; unlesbare Quelle → Kind")]
    public void Hauptprofil()
    {
        var root = Tmp();
        new UserSettings { Child = true, Name = "Kind" }.Save(root);
        Assert.True(ProfileRole.IsChild(root)); // erster Start nach dem Umbau: einmalig aus settings.json übernommen
        Assert.True(File.Exists(ProfileRole.FileOf(root)));
        new UserSettings { Child = false, Name = "Boss" }.Save(root);
        Assert.True(ProfileRole.IsChild(root)); // manipulierte settings.json
        File.Delete(UserSettings.PathIn(root));
        Assert.True(ProfileRole.IsChild(root)); // gelöschte settings.json
        Software.ResetSettings(root);
        Assert.True(ProfileRole.IsChild(root)); // zurückgesetzt
        File.WriteAllText(ProfileRole.FileOf(root), "kaputt");
        Assert.True(ProfileRole.IsChild(root)); // unlesbar → Kind
        // Erwachsene: wie bisher; settings.json kann die Rolle nur verschärfen
        var adult = Tmp(); new UserSettings { Name = "Erika" }.Save(adult);
        Assert.False(ProfileRole.IsChild(adult));
        File.Delete(UserSettings.PathIn(adult)); Assert.False(ProfileRole.IsChild(adult));
        // Ersteinrichtung: nichts da → Besitzer
        var fresh = Tmp(); Assert.False(ProfileRole.IsChild(fresh)); Assert.True(File.Exists(ProfileRole.FileOf(fresh)));
        // Personen da, aber weder rolle.json noch settings.json → fail-closed
        var odd = Tmp(); File.WriteAllText(Path.Combine(odd, "personen.json"), "[]"); Assert.True(ProfileRole.IsChild(odd));
    }

    [Fact(DisplayName = "T-0764 H1/H2 (Kern): weitere Person – Rolle aus personen.json; settings.json der Person, fehlender Eintrag oder kaputte Liste → Kind")]
    public void Personen()
    {
        var root = Tmp(); new UserSettings { Name = "Erika" }.Save(root);
        var store = new PersonStore(root);
        var kid = store.Add("Mia", "emoji:🐼", true); var grown = store.Add("Tom", "emoji:🦊", false);
        Assert.True(ProfileRole.IsChild(store.RootOf(kid.Id))); Assert.False(ProfileRole.IsChild(store.RootOf(grown.Id)));
        new UserSettings { Child = false }.Save(store.RootOf(kid.Id));
        Assert.True(ProfileRole.IsChild(store.RootOf(kid.Id)));
        File.Delete(UserSettings.PathIn(store.RootOf(kid.Id))); Assert.True(ProfileRole.IsChild(store.RootOf(kid.Id)));
        var ghost = Path.Combine(root, "personen", "unbekannt"); Directory.CreateDirectory(ghost); new UserSettings().Save(ghost);
        Assert.True(ProfileRole.IsChild(ghost)); // ohne Eintrag
        File.WriteAllText(Path.Combine(root, "personen.json"), "kaputt");
        Assert.True(ProfileRole.IsChild(store.RootOf(grown.Id))); // Liste unlesbar → Kind
    }

    [Fact(DisplayName = "T-0764 (14.): Erwachsenen-Weg – ProfileRole.Set nur durch Erwachsene; Kind → Erwachsener und zurück, für Hauptprofil und Person")]
    public void Erwachsenen_Weg()
    {
        var root = Tmp(); new UserSettings { Child = true }.Save(root);
        var audit = new AuditLog(Path.Combine(root, "audit.jsonl"));
        Assert.True(ProfileRole.IsChild(root));
        Assert.Throws<UnauthorizedAccessException>(() => ProfileRole.Set(Principal.ChildProfile, root, false, audit));
        Assert.True(ProfileRole.IsChild(root));
        ProfileRole.Set(Principal.LocalOwner, root, false, audit);
        Assert.False(ProfileRole.IsChild(root)); Assert.False(UserSettings.Load(root).Child);
        var store = new PersonStore(root); var p = store.Add("Mia", "emoji:🐼", false);
        Assert.Throws<UnauthorizedAccessException>(() => ProfileRole.Set(Principal.ChildProfile, store.RootOf(p.Id), false, audit));
        ProfileRole.Set(Principal.LocalOwner, store.RootOf(p.Id), true, audit);
        Assert.True(ProfileRole.IsChild(store.RootOf(p.Id))); Assert.True(store.All().Single().Child);
    }

    [Fact(DisplayName = "T-0764 H1 (Kern): „Einstellungen einlesen“ übernimmt nie Rolle, Name, Bild, Projektordner, Sperren – für jede Rolle; Bedienfelder schon")]
    public void Einlesen()
    {
        var d = Tmp(); var f = Path.Combine(d, "x.json");
        A11y.ExportSettings(new UserSettings { Child = false, Name = "Boss", Avatar = "bild", ProjectsFolder = @"C:\Windows", LockOnStart = false, SetupDone = false, AutoUpdate = false, PhotoFolders = [@"C:\x"], Theme = "hell", FontMin = 14 }, f);
        foreach (var child in new[] { true, false })
        {
            var cur = new UserSettings { Child = child, Name = "Kind", Avatar = "emoji:🐼", ProjectsFolder = @"D:\Projekte", LockOnStart = true, SetupDone = true, AutoUpdate = true };
            var r = A11y.ImportSettings(f, cur);
            Assert.Equal("hell", r.Theme); Assert.Equal(14, r.FontMin);
            Assert.Equal(child, r.Child); Assert.Equal("Kind", r.Name); Assert.Equal("emoji:🐼", r.Avatar); Assert.Equal(@"D:\Projekte", r.ProjectsFolder);
            Assert.True(r.LockOnStart); Assert.True(r.SetupDone); Assert.True(r.AutoUpdate); Assert.Empty(r.PhotoFolders);
        }
        Assert.All(A11y.KeptOnImport, n => Assert.NotNull(typeof(UserSettings).GetProperty(n)));
    }

    [Fact(DisplayName = "T-0764 (14.): die geschützte Quelle ist für Kinder unbeschreibbar (rolle.json, personen.json, settings.json liegen außerhalb der Freigabe)")]
    public void Quelle_geschuetzt()
    {
        var root = Tmp(); var home = Path.Combine(root, "dokumente"); Directory.CreateDirectory(home);
        using var _ = ChildWrite.TestScope(home);
        using var __ = ChildWrite.TestScope(Path.Combine(root, "projekte"));
        foreach (var f in new[] { ProfileRole.FileOf(root), Path.Combine(root, "personen.json"), UserSettings.PathIn(root), Path.Combine(root, "personen", "abc", "settings.json") })
            Assert.NotNull(ChildWrite.Problem(Principal.ChildProfile, f));
        Assert.Null(ChildWrite.Problem(Principal.ChildProfile, Path.Combine(home, "x.json")));
    }

    /// <summary>Freigegebene Stellen: Datei → (Muster, Anzahl, Grund).</summary>
    static readonly (string File, string Pattern, int Count, string Why)[] Allowed =
    [
        ("src/Trendo.Core/Profile.cs", "child", 2, "PersonStore.Add (Erwachsenen-Weg, Spiegel) und ProfileRole.Set (Erwachsenen-Weg, Spiegel)"),
        ("src/Trendo.App/MainWindow.Freigabe.cs", "child", 1, "UseSettings: Spiegel aus der geschützten Quelle"),
        ("src/Trendo.App/MainWindow.Freigabe.cs", "settings", 1, "UseSettings: einzige Stelle, die _settings ersetzt"),
        ("src/Trendo.App/MainWindow.Setup.cs", "settings", 1, "Feld-Anfangswert (vor dem Laden; Rolle bis dahin Kind)"),
        ("src/Trendo.App/MainWindow.Setup.cs", "projects", 2, "Vorgabe beim Start (Datenordner bzw. Dokumente) und Assistent Schritt 2 (nur Erwachsene)"),
        ("src/Trendo.Core/Profile.cs", "with-child", 1, "PersonStore.SetChild (nur über ProfileRole.Set)"),
    ];

    [Fact(DisplayName = "T-0764 (14.) Wächter: Zuweisungen an Child, ProjectsFolder und das Ersetzen von _settings nur an freigegebenen Stellen; Rolle nur aus ProfileRole")]
    public void Rollen_Waechter()
    {
        var pats = new Dictionary<string, Regex>
        {
            ["child"] = new(@"\b(_settings|s|settings|main|cur|current|neu|x|u|us)\.Child\s*=(?!=)"),
            ["projects"] = new(@"\.ProjectsFolder\s*=(?!=)"),
            ["settings"] = new(@"\b_settings\s*=(?!=)"),
            ["new-settings-child"] = new(@"new\s+UserSettings\s*(\(\s*\))?\s*\{[^}]*\bChild\s*="),
            ["with-child"] = new(@"with\s*\{[^}]*\bChild\s*="),
        };
        var found = new List<string>();
        foreach (var f in Directory.EnumerateFiles(Repo("src"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)))
        {
            var rel = Path.GetRelativePath(Repo(""), f).Replace('\\', '/'); var s = File.ReadAllText(f);
            foreach (var (k, re) in pats)
            {
                var n = re.Matches(s).Count; if (n == 0) continue;
                var ok = Allowed.FirstOrDefault(a => a.File == rel && a.Pattern == k);
                if (ok.Count != n) found.Add($"{rel} {k}: {n} (frei: {ok.Count})");
            }
        }
        foreach (var a in Allowed)
            if (!found.Any(x => x.StartsWith(a.File + " " + a.Pattern + ":")) && pats[a.Pattern].Matches(File.ReadAllText(Repo(a.File))).Count != a.Count) found.Add($"{a.File} {a.Pattern}: verwaist");
        Assert.True(found.Count == 0, "Rolle/Projektordner/Einstellungen ohne Freigabe: " + string.Join("; ", found));
        // Rolle des Fensters und der Kommandozeile nur aus der geschützten Quelle
        var fg = File.ReadAllText(Repo("src/Trendo.App/MainWindow.Freigabe.cs"));
        Assert.Contains("CurrentPrincipal => Principal.ForProfile(_roleChild)", fg);
        Assert.Contains("_roleChild = ProfileRole.IsChild(_core.DataRoot)", fg);
        Assert.Contains("ProfileRole.IsChild(core.DataRoot)", File.ReadAllText(Repo("src/Trendo.App/Program.cs")));
        // ForProfile nur mit der geschützten Rolle
        foreach (var f in Directory.EnumerateFiles(Repo("src"), "*.cs", SearchOption.AllDirectories))
            foreach (Match m in Regex.Matches(File.ReadAllText(f), @"ForProfile\(([^)]*)\)"))
                Assert.True(m.Groups[1].Value is "_roleChild" or "ProfileRole.IsChild(core.DataRoot" or "bool child" or "child", $"{Path.GetFileName(f)}: ForProfile({m.Groups[1].Value})");
        // M: Projektordner für Kinder nur in der Freigabe – WorkFolder und ProjectsRoot gehen über ProjectsBase (an diese Prüfung ist die Wächter-Ausnahme „Trendos Ordner“ gebunden)
        Assert.Contains("var root = ProjectsBase(", File.ReadAllText(Repo("src/Trendo.App/MainWindow.W45.cs")));
        Assert.Contains("ProjectsRoot => ProjectsBase(", File.ReadAllText(Repo("src/Trendo.App/MainWindow.W1Apps.cs")));
        Assert.Matches(new Regex(@"internal string ProjectsBase\(string fallback\)[\s\S]{0,600}ChildWrite\.Problem"), fg);
    }
}
