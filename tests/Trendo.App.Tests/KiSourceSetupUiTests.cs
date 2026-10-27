using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>KI-Quellen automatisch laden und einrichten – GitHub (portabel) und Herstellerseite (Installer), ohne echte Downloads.</summary>
public class KiSourceSetupUiTests
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "KoboldCpp: Bestätigen → von GitHub laden → einrichten → Starten mit größtem Sprachmodell")]
    public async Task Portable_from_github()
    {
        var (w, _, root) = Ui.Open();
        var exeFile = Path.Combine(root, "dl-koboldcpp-nocuda.exe"); File.WriteAllText(exeFile, "MZ");
        w.KiSourceReleasesOverride = _ => Task.FromResult("""[{ "tag_name": "v1.0", "assets": [ { "name": "koboldcpp-nocuda.exe", "browser_download_url": "https://github.com/LostRuins/koboldcpp/releases/download/v1.0/koboldcpp-nocuda.exe", "size": 2 } ] }]""");
        w.KiDownloadOverride = (_, _) => Task.FromResult<(string, string?)>((exeFile, null));
        (string Exe, string Args)? started = null; w.KiServerStartOverride = (e, a) => started = (e, a);
        w.Show("ki-quellen"); Pump();
        w.Find<Button>("src-install-koboldcpp").Press(); Pump();
        Assert.NotNull(w.TryFind<Button>("src-install-yes-koboldcpp")); // erst bestätigen
        w.Find<Button>("src-install-no-koboldcpp").Press(); Pump();
        Assert.True(await w.KiSourceInstall("koboldcpp")); Pump();
        Assert.NotNull(w.KiSourceExe("koboldcpp"));
        Assert.True(w.SetupFinishShown); w.Find<Button>("setup-overlay-done").Press(); Pump();
        Assert.Throws<InvalidOperationException>(() => w.KiSourceStart("koboldcpp")); // noch kein Sprachmodell
        var gguf = Path.Combine(w.KiStore.Root, "qwen", "qwen.gguf"); Directory.CreateDirectory(Path.GetDirectoryName(gguf)!); File.WriteAllBytes(gguf, new byte[8]);
        Assert.True(w.KiSourceStart("koboldcpp"));
        Assert.Contains(gguf, started!.Value.Args);
        w.Show("ki-quellen"); Pump();
        Assert.NotNull(w.TryFind<Button>("src-start-koboldcpp"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "LM Studio: Installer von der Herstellerseite laden und starten, danach erkennen und Quelle einschalten")]
    public async Task Installer_from_vendor()
    {
        var (w, _, root) = Ui.Open();
        w.LocalAppDataDir = Path.Combine(root, "local");
        var setup = Path.Combine(root, "LM-Studio-Setup.exe"); File.WriteAllText(setup, "MZ");
        string? url = null;
        w.KiDownloadOverride = (u, _) => { url = u; return Task.FromResult<(string, string?)>((setup, null)); };
        w.KiInstallerRunOverride = _ => { var d = Path.Combine(w.LocalAppDataDir, "Programs", "LM Studio"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "LM Studio.exe"), "x"); return Task.CompletedTask; };
        KiSourceSettings.Update(w.Core.DataRoot, s => s.Disabled.Add("lmstudio"));
        Assert.True(await w.KiSourceInstall("lmstudio")); Pump();
        Assert.StartsWith("https://lmstudio.ai/", url);
        Assert.DoesNotContain("lmstudio", KiSourceSettings.Load(w.Core.DataRoot).Disabled);
        Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "ki.source.install.ok");
        w.Close();
    }
}
