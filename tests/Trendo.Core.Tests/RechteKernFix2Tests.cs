using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0764, 2. Prüfrunde: zentrale Startprüfung (N-C/N-D) und Zustimmungsregeln der KI-Brücke (N-E/N-F) im Kern.</summary>
public class RechteKernFix2
{
    [Theory(DisplayName = "T-0764 N-D: Was braucht „Programme verwalten“? Installer, Verknüpfungen, Skripte, jede fremde .exe – Dokumente, Ordner, Adressen nicht")]
    [InlineData(@"D:\Downloads\vlc-3.0.21-win64.exe", true)]
    [InlineData(@"D:\Downloads\7z2408-x64.exe", true)]
    [InlineData(@"D:\Downloads\report.pdf.exe", true)]
    [InlineData(@"D:\Desktop\Spiel.lnk", true)]
    [InlineData(@"D:\x.url", true)]
    [InlineData(@"D:\x.cpl", true)]
    [InlineData(@"D:\x.hta", true)]
    [InlineData(@"D:\x.application", true)]
    [InlineData(@"D:\x.msu", true)]
    [InlineData(@"D:\x.jar", true)]
    [InlineData(@"D:\x.diagcab", true)]
    [InlineData(@"D:\x.settingcontent-ms", true)]
    [InlineData(@"D:\brief.pdf", false)]
    [InlineData(@"D:\foto.jpg", false)]
    [InlineData("https://example.org/setup.exe", false)]
    [InlineData("ms-settings:appsfeatures", false)]
    [InlineData("shell:RecycleBinFolder", true)] // 4. Prüfrunde M3: Papierkorb nur noch in Trendos Dateimanager, nicht im Windows-Explorer
    public void Recht_noetig(string target, bool needs) => Assert.Equal(needs, Software.NeedsSoftwareRight(target));

    [Fact(DisplayName = "T-0764 N-C: Trendo-eigene Programme gelten als vertrauenswürdig, fremde .exe nicht; Kind startet keine fremde .exe, Erwachsene schon")]
    public void Zentral_im_Kern()
    {
        using var f = new Fixture();
        var own = Path.Combine(f.Root, "werkzeuge", "notiz.exe"); Directory.CreateDirectory(Path.GetDirectoryName(own)!); File.WriteAllText(own, "MZ");
        var foreign = Path.Combine(f.Root, "downloads", "vlc-setup.exe"); Directory.CreateDirectory(Path.GetDirectoryName(foreign)!); File.WriteAllText(foreign, "MZ");
        f.Core.Integrations.TrustedProgramPath = p => p.StartsWith(Path.Combine(f.Root, "werkzeuge"), StringComparison.OrdinalIgnoreCase);
        var e = Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, foreign, f.Core.Audit));
        Assert.Equal(Rbac.NurErwachseneStart, e.Message);
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, @"C:\Programme\Player\player.exe", f.Core.Audit, foreign));
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(Principal.ChildProfile, foreign, f.Core.Audit, "", userExe: true));
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchArgs(Principal.ChildProfile, "cmd.exe", f.Core.Audit, [foreign]));
        Assert.Empty(f.Launcher.Opened);
        f.Core.Integrations.LaunchDirect(Principal.ChildProfile, own, f.Core.Audit);
        f.Core.Integrations.LaunchDirect(Principal.ChildProfile, "https://example.org/", f.Core.Audit);
        f.Core.Integrations.LaunchDirect(Principal.LocalOwner, foreign, f.Core.Audit);
        Assert.Equal(3, f.Launcher.Opened.Count);
        // andere Rollen (Agent, API) starten weiterhin gar nicht direkt
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Integrations.LaunchDirect(new Principal("agent", Role.User), own, f.Core.Audit));
    }

    [Fact(DisplayName = "T-0764 N-F: „Vorlage laden“/„Datei laden“ ohne Rückfrage; Installationskarten („Ja, laden“) und Programm-Downloads fragen weiter")]
    public void Laden_nur_mit_Programmbezug()
    {
        Assert.False(ClaudeActions.NeedsConsent("Vorlage laden", "x", true));
        Assert.False(ClaudeActions.NeedsConsent("Datei laden", "x", true));
        Assert.True(ClaudeActions.NeedsConsent("Ja, laden", "src-install-yes-jan", true));
        Assert.True(ClaudeActions.NeedsConsent("Herunterladen & einrichten", "x", true));
        Assert.True(ClaudeActions.NeedsConsent("Programm laden", "x", true));
        Assert.True(ClaudeActions.NeedsConsent("Zurück zu Version 0.9.108", "upd-rollback", true));
    }
}
