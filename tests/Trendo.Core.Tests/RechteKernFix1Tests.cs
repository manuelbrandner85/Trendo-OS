using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764 Nachbesserung nach der Gegenprüfung (M1, M4, N1, N2, M3-Kern): KI-Brücke fragt bei Installationsknöpfen immer,
/// Auswurf-Schutz über das ganze Gerät, Programm-Seite zeigt nur echte Programme, Wiederherstellen nimmt keine veränderte Sicherung an.</summary>
public class RechteKernFix1
{
    [Theory(DisplayName = "T-0764 M1: Knöpfe zum Laden, Installieren, Entfernen von Programmen fragen immer – auch im rückgängig machbaren Dokument")]
    [InlineData("Ja, laden", "src-install-yes-jan")]
    [InlineData("Neu laden", "src-install-koboldcpp")]
    [InlineData("Herunterladen & einrichten", "x")]
    [InlineData("Ja, installieren", "hub-yes-7zip")]
    [InlineData("Weiter", "hub-yes-7zip")]
    [InlineData("Ja", "setup-yes-gimp-x")]
    [InlineData("Ja, entfernen", "prog-detail-remove-yes")]
    [InlineData("Deinstallieren", "x")]
    [InlineData("Auswerfen", "dev-eject-E")]
    [InlineData("Claude Code einrichten", "claude-install")]
    public void Bruecke_fragt(string label, string name) => Assert.True(ClaudeActions.NeedsConsent(label, name, undoableDocument: true));

    [Fact(DisplayName = "T-0764 M1: Bearbeiten im Dokument fragt weiter nicht (Stab entfernen, Rendern starten)")]
    public void Bruecke_Dokument_bleibt()
    {
        Assert.False(ClaudeActions.NeedsConsent("Stab entfernen", "x", true));
        Assert.False(ClaudeActions.NeedsConsent("Rendern starten", "x", true));
    }

    sealed class FakeDisks(Dictionary<string, string> links, Dictionary<string, (int, int)> disks) : IDiskResolver
    {
        public string FinalPath(string path)
        {
            var full = Path.GetFullPath(path);
            foreach (var (from, to) in links) if (full.StartsWith(from, StringComparison.OrdinalIgnoreCase)) return to + full[from.Length..];
            return full;
        }
        public (int Type, int Number)? DiskOf(string root) => disks.TryGetValue(root.ToUpperInvariant(), out var d) ? d : null;
    }

    sealed class CountEjector : IDriveEjector
    {
        public int Calls;
        public EjectResult Eject(string driveRoot) { Calls++; return new(true, "weg"); }
    }

    [Fact(DisplayName = "T-0764 M4: Schutz über das ganze Gerät – zweite Partition desselben Sticks, Junction/subst auf den Stick: nichts wird ausgeworfen")]
    public void Auswurf_ganzes_Geraet()
    {
        using var f = new Fixture();
        var e = new CountEjector();
        // Trendo-Daten auf F: (zweite Partition), Ziel E: – beide auf Gerät 7/3
        var twoParts = new FakeDisks([], new() { ["E:\\"] = (7, 3), ["F:\\"] = (7, 3), ["C:\\"] = (7, 0) });
        Assert.False(DeviceEject.Eject("E:\\", DriveType.Removable, [@"F:\Trendo\daten"], e, f.Core.Audit, resolver: twoParts).Ok);
        // Junction/subst: der Schutzpfad steht auf X:, zeigt aber auf E:
        var link = new FakeDisks(new() { [@"X:\"] = @"E:\" }, new() { ["E:\\"] = (7, 3), ["X:\\"] = (7, 0) });
        Assert.False(DeviceEject.Eject("E:\\", DriveType.Removable, [@"X:\Trendo"], e, f.Core.Audit, resolver: link).Ok);
        Assert.Equal(0, e.Calls);
        // anderes Gerät: erlaubt
        var other = new FakeDisks([], new() { ["E:\\"] = (7, 3), ["C:\\"] = (7, 0) });
        Assert.True(DeviceEject.Eject("E:\\", DriveType.Removable, [@"C:\Windows"], e, f.Core.Audit, resolver: other).Ok);
        Assert.Equal(1, e.Calls);
    }

    [Fact(DisplayName = "T-0764 M4 echt (nur lesen): Windows löst eine Junction zum Ziel auf und kennt den Datenträger des Systemlaufwerks")]
    public void Auswurf_Aufloesung_echt()
    {
        if (!OperatingSystem.IsWindows()) return; // nicht anwendbar
        using var f = new Fixture();
        var target = Path.Combine(f.Root, "ziel"); Directory.CreateDirectory(target);
        var junction = Path.Combine(f.Root, "verknuepft");
        var p = Process.Start(new ProcessStartInfo("cmd.exe", ["/c", "mklink", "/J", junction, target]) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        p.WaitForExit(10_000);
        if (!Directory.Exists(junction)) return; // Junction ließ sich hier nicht anlegen
        var r = new WindowsDiskResolver();
        Assert.Equal(Path.GetFullPath(target), r.FinalPath(Path.Combine(junction)), ignoreCase: true);
        Assert.Equal(Path.Combine(Path.GetFullPath(target), "fehlt", "noch"), r.FinalPath(Path.Combine(junction, "fehlt", "noch")), ignoreCase: true);
        Assert.NotNull(r.DiskOf(Path.GetPathRoot(Environment.SystemDirectory)!));
        Directory.Delete(junction); // nur die Verknüpfung
    }

    [Fact(DisplayName = "T-0764 N1: Programm-Seite zeigt nur eingerichtete Programme – „rueckgaengig“ und Ordner ohne Programm nie; Kennung von InstallPortable")]
    public void Nur_echte_Programme()
    {
        using var f = new Fixture();
        var p = Path.Combine(f.Root, "werkzeuge"); var t = Path.Combine(f.Root, "tools"); Directory.CreateDirectory(t);
        var undo = Path.Combine(p, "rueckgaengig"); Directory.CreateDirectory(undo); File.WriteAllText(Path.Combine(undo, "foto.exe"), "x");
        var noProg = Path.Combine(p, "notizen"); Directory.CreateDirectory(noProg); File.WriteAllText(Path.Combine(noProg, "a.txt"), "x");
        var old = Path.Combine(p, "alt"); Directory.CreateDirectory(old); File.WriteAllText(Path.Combine(old, "alt.exe"), "MZ");
        var zip = Path.Combine(f.Root, "neu.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) using (var w = new StreamWriter(z.CreateEntry("neu.exe").Open())) w.Write("MZ");
        Software.InstallPortable(Principal.LocalOwner, zip, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zip))), p, "neu", f.Core.Audit);
        Assert.True(File.Exists(Path.Combine(p, "neu", Software.PortableMarker)));
        var list = TrendoPrograms.List(p, t);
        Assert.Equal(["alt", "neu"], list.Select(e => e.Id).Order().ToArray());
        Assert.Contains("ohne Kennung", list.Single(e => e.Id == "alt").Quelle);
    }

    [Fact(DisplayName = "T-0764 N2: Wiederherstellen lehnt eine veränderte Sicherung ab (fremde Kennung, absoluter Programmpfad, ungültiges Projekt)")]
    public void Wiederherstellen_prueft_Sicherung()
    {
        using var f = new Fixture();
        var p = Path.Combine(f.Root, "werkzeuge"); var t = Path.Combine(f.Root, "tools"); Directory.CreateDirectory(p);
        var zip = Path.Combine(f.Root, "tool.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) using (var w = new StreamWriter(z.CreateEntry("tool.exe").Open())) w.Write("MZ");
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zip)));
        GitHubApps.Install(Principal.LocalOwner, t, "o/tool", new GitHubApps.Release("v1", "", []), new GitHubApps.Asset("tool-windows-x64.zip", "https://github.com/o/tool/x", new FileInfo(zip).Length, sha), zip, "MIT", "", f.Core.Audit);
        var s = TrendoPrograms.Remove(Principal.LocalOwner, TrendoPrograms.List(p, t).Single(), p, t, f.Core.Audit);
        var json = s.Ordner + ".json"; var orig = File.ReadAllText(json);
        foreach (var (key, val) in new[] { ("exe", (JsonNode)@"C:\Windows\System32\cmd.exe"), ("id", "gh-anderes"), ("repo", "../x"), ("exe", "../../boese.exe") })
        {
            var n = JsonNode.Parse(orig)!; n["GitHub"]![key] = val; File.WriteAllText(json, n.ToJsonString());
            Assert.Throws<ArgumentException>(() => TrendoPrograms.Restore(Principal.LocalOwner, s, p, t, f.Core.Audit));
            Assert.True(Directory.Exists(s.Ordner));
            Assert.Empty(GitHubApps.List(t));
        }
        File.WriteAllText(json, orig);
        TrendoPrograms.Restore(Principal.LocalOwner, s, p, t, f.Core.Audit);
        Assert.Single(GitHubApps.List(t));
    }

    [Theory(DisplayName = "T-0764 M3: Installer, Pakete und Skripte erkennen – Dokumente und gewöhnliche Programme nicht")]
    [InlineData("setup.exe", true)]
    [InlineData("Firefox Installer.exe", true)]
    [InlineData("unins000.exe", true)]
    [InlineData("paket.msi", true)]
    [InlineData("app.msix", true)]
    [InlineData("app.appx", true)]
    [InlineData("start.bat", true)]
    [InlineData("start.cmd", true)]
    [InlineData("skript.ps1", true)]
    [InlineData("makro.vbs", true)]
    [InlineData("einstellung.reg", true)]
    [InlineData("brief.pdf", false)]
    [InlineData("foto.jpg", false)]
    [InlineData("spiel.exe", false)]
    public void Installer_erkennen(string file, bool installer) => Assert.Equal(installer, Software.IsInstallerLike(Path.Combine("D:", file)));
}
