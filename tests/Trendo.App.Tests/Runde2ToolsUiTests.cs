using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 2 – Oberfläche: typst (Brief als PDF), mediamtx (Sendeserver), streamlink (Live-Sender). Programme nachgebildet, keine echten Starts.</summary>
public class Runde2ToolsUi
{
    static async Task Until(Func<bool> ok, int ms = 8000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static string Exe() { var f = Path.Combine(Path.GetTempPath(), "trendo-fake-tool-" + Guid.NewGuid().ToString("N") + ".exe"); File.WriteAllText(f, "x"); return f; }

    [AvaloniaFact(DisplayName = "typst-Oberfläche: ohne Programm Einrichten-Knopf; mit Programm Brief mit Posten → PDF im Ordner Dokumente; falscher Posten → Hinweis")]
    public void Typst_letter()
    {
        var exe = Exe();
        try
        {
            MainWindow.ToolExeOverride["typst"] = "";
            var (w, _, _) = Ui.Open(); w.Show("doku-schreiben");
            Assert.NotNull(w.Find<Button>("typst-setup"));
            MainWindow.ToolExeOverride["typst"] = exe; IReadOnlyList<string>? seen = null;
            MainWindow.ToolRunOverride = (_, a, _) => { seen = a; File.WriteAllText(a[4], "%PDF-1.7"); return new Cli.Result(0, "", ""); };
            w.Show("doku-schreiben");
            w.Find<TextBox>("typst-title").Text = "Rechnung 17"; w.Find<TextBox>("typst-body").Text = "Sehr geehrte Frau Muster,";
            w.Find<TextBox>("typst-items").Text = "2;Stühle;59,50\n1;Tisch;349";
            w.Find<Button>("typst-run").Press();
            Assert.StartsWith("✓ PDF gespeichert: Rechnung-17.pdf", w.TypstMessage);
            Assert.Contains("Gesamt: 468,00 €", File.ReadAllText(seen![3]));
            w.Find<TextBox>("typst-items").Text = "zwei Stühle"; w.Find<Button>("typst-run").Press();
            Assert.Contains("Menge;Artikel;Preis", w.TypstMessage);
        }
        finally { MainWindow.ToolExeOverride.Clear(); MainWindow.ToolRunOverride = null; File.Delete(exe); }
    }

    [AvaloniaFact(DisplayName = "mediamtx-Oberfläche: Sendeserver starten → OBS-Adresse und Ansehen-Adresse, nur 127.0.0.1; beenden; unsicherer Name abgelehnt")]
    public void Live_server()
    {
        var exe = Exe(); string? cfg = null;
        try
        {
            MainWindow.ToolExeOverride["mediamtx"] = exe;
            MainWindow.LiveServerStartOverride = (_, a, _) => { cfg = File.ReadAllText(a[0]); return true; };
            var (w, _, _) = Ui.Open(); w.BcMode = "studio"; w.Show("sendung");
            w.Find<TextBox>("mtx-path").Text = "../boese"; w.Find<Button>("mtx-start").Press();
            Assert.Contains("Sendungsname", w.LiveServerMessage); Assert.False(w.LiveServerRunning);
            w.Find<TextBox>("mtx-path").Text = "abendshow"; w.Find<Button>("mtx-start").Press();
            Assert.True(w.LiveServerRunning); Assert.Contains("rtsp://127.0.0.1:8554/abendshow", w.LiveServerMessage); Assert.Contains("127.0.0.1:8554", cfg);
            w.Find<Button>("mtx-stop").Press(); Assert.False(w.LiveServerRunning); Assert.Equal("Sendeserver beendet.", w.LiveServerMessage);
        }
        finally { MainWindow.ToolExeOverride.Clear(); MainWindow.LiveServerStartOverride = null; File.Delete(exe); }
    }

    [AvaloniaFact(DisplayName = "streamlink-Oberfläche: Mediathek-Seite zeigt Das Erste und arte; Klick löst den Stream auf und wechselt zum Player")]
    public async Task Live_channels()
    {
        var exe = Exe(); IReadOnlyList<string>? seen = null;
        try
        {
            MainWindow.ToolExeOverride["streamlink"] = exe;
            MainWindow.ToolRunOverride = (_, a, _) => { seen = a; return new Cli.Result(0, "https://example.invalid/live/master.m3u8\n", ""); };
            var (w, _, _) = Ui.Open(); w.TvTab = "mediathek"; w.Show("tv");
            Assert.NotNull(w.Find<Button>("sl-play-1"));
            w.Find<Button>("sl-play-0").Press();
            await Until(() => w.LiveResolveMessage.StartsWith('▶'));
            Assert.Equal(LiveResolve.Channels[0].Url, seen![3]); Assert.Equal("live", w.TvTab);
        }
        finally { MainWindow.ToolExeOverride.Clear(); MainWindow.ToolRunOverride = null; File.Delete(exe); }
    }
}
