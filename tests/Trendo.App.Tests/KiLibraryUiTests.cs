using System.IO.Compression;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>KI-Bibliothek: nach Arten sortiert, laden mit Prüfung, ausprobieren, entfernen (Vorgabe 2026-10-26).</summary>
public class KiLibraryUiTests
{
    [AvaloniaFact(DisplayName = "KI-Bibliothek (UI): alle Arten als Auswahl, jede Art zeigt ihre Modelle mit Lizenz und Größe")]
    public void Kinds_and_models()
    {
        var (w, _, _) = Ui.Open();
        w.Show("ki-bibliothek");
        foreach (var k in Enum.GetValues<KiKind>()) Assert.NotNull(w.Find<Button>("kilib-kind-" + k));
        foreach (var k in Enum.GetValues<KiKind>())
        {
            w.KiLibraryShow(k);
            foreach (var m in KiLibrary.OfKind(k)) Assert.NotNull(w.TryFind<Button>("kilib-get-" + m.Id) ?? w.TryFind<Button>("kilib-try-" + m.Id) ?? w.TryFind<Button>("kilib-use-" + m.Id));
        }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "KI-Bibliothek (UI): Vorlesen laden (Laufzeit + Stimme geprüft), ausprobieren, Ergebnis, entfernen")]
    public async Task Install_try_remove()
    {
        var (w, _, root) = Ui.Open();
        var m = KiLibrary.Models.First(x => x.Id == "piper-thorsten");
        var rt = KiLibrary.RuntimeInfo(KiRuntime.Piper);
        // Echte Prüfsummen passen nicht zu Test-Dateien: Laufzeit-Download schlägt deshalb fehl, nichts wird eingerichtet
        w.KiDownloadOverride = (url, name) => { var f = Path.Combine(root, "dl-" + name); File.WriteAllBytes(f, new byte[16]); return Task.FromResult<(string, string?)>((f, null)); };
        await w.KiLibraryInstall(m);
        Assert.False(w.KiStore.RuntimeReady(KiRuntime.Piper));
        Assert.False(w.SetupOverlayOpen);
        Assert.Contains("hat nicht geklappt", w.Find<TextBlock>("kilib-msg").Text);

        // Laufzeit vorhanden (wie nach echtem Laden), Modelldateien mit genau passender Größe
        var exe = w.KiStore.RuntimeExe(KiRuntime.Piper);
        Directory.CreateDirectory(Path.GetDirectoryName(exe)!); File.WriteAllText(exe, "MZ");
        w.KiDownloadOverride = (url, name) =>
        {
            var size = m.Files.First(f => f.FileName == name).Bytes;
            var f = Path.Combine(root, "dl-" + name);
            using (var s = File.Create(f)) s.SetLength(size);
            return Task.FromResult<(string, string?)>((f, null));
        };
        await w.KiLibraryInstall(m);
        Assert.True(w.KiStore.ModelReady(m));
        string? stdin = null;
        w.KiRunOverride = (e, args, input) =>
        {
            stdin = input;
            File.WriteAllText(args[args.ToList().IndexOf("--output_file") + 1], "RIFF");
            return Task.FromResult(new Cli.Result(0, "", ""));
        };
        w.KiLibraryShow(KiKind.TextZuSprache, m.Id);
        w.Find<TextBox>("kilib-prompt").Text = "Hallo Welt";
        w.Find<Button>("kilib-run").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for (var i = 0; i < 50 && stdin is null; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Hallo Welt", stdin);
        Assert.Contains("Fertig", w.Find<TextBlock>("kilib-msg").Text);
        w.KiLibraryShow(KiKind.TextZuSprache);
        w.Find<Button>("kilib-remove-" + m.Id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        w.Find<Button>("kilib-remove-" + m.Id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(w.KiStore.ModelReady(m));
        w.Close();
    }

    [Fact(DisplayName = "KI-Bibliothek: nur offizielle Server (GitHub, Hugging Face) über HTTPS")]
    public void Hosts()
    {
        Assert.True(MainWindow.KiHostAllowed(new Uri("https://huggingface.co/x")));
        Assert.True(MainWindow.KiHostAllowed(new Uri("https://us.aws.cdn.hf.co/x")));
        Assert.True(MainWindow.KiHostAllowed(new Uri("https://release-assets.githubusercontent.com/x")));
        Assert.False(MainWindow.KiHostAllowed(new Uri("http://huggingface.co/x")));
        Assert.False(MainWindow.KiHostAllowed(new Uri("https://evil-hf.co/x")));
        Assert.False(MainWindow.KiHostAllowed(new Uri("https://huggingface.co.evil.example/x")));
    }
}
