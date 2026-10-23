using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.App.Tests;

public class Audit_Studio_Ui
{
    static void Pumpen() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "T-0204: Regeln, Vorschau, ZIP und Unreal ohne Installation")]
    public void Spiele()
    {
        var (w, l, _) = Ui.Open();
        w.EngineFinder = () => [];
        w.Show("spiele");
        Assert.NotNull(w.TryFind<Button>("game-get-unreal"));
        Assert.NotNull(w.TryFind<Button>("game-ue-create"));
        Assert.Contains("nicht installiert", string.Join(" ", w.GetLogicalTexts()));
        w.Find<NumericUpDown>("game-tempo").Value = 9;
        Pumpen();
        Assert.Contains("Tempo 9", w.Find<TextBlock>("game-preview-text").Text);
        w.Find<Button>("game-zelle").Press();
        Assert.Contains("Feld oben links", w.Find<TextBlock>("game-zelle-stand").Text);
        w.Find<TextBox>("game-custom-name").Text = "Level Eins";
        w.Find<Button>("game-build").Press();
        var html = File.ReadAllText(Path.Combine(w.GameFolder, "Level-Eins", "index.html"));
        Assert.Contains("\"tempo\":9", html);
        w.Find<Button>("game-zip").Press();
        Assert.True(File.Exists(Path.Combine(w.GameFolder, "Level-Eins.zip")));
        w.Find<Button>("game-ue-create").Press();
        Assert.True(File.Exists(Path.Combine(w.GameFolder, "MeinUnrealSpiel", "MeinUnrealSpiel.uproject")));
        Assert.Contains("startet nicht", string.Join(" ", w.GetLogicalTexts()));
        var up = Path.Combine(w.GameFolder, "MeinUnrealSpiel", "MeinUnrealSpiel.uproject");
        w.PickOverride = _ => Task.FromResult<string[]?>([up]);
        w.Find<Button>("game-ue-open").Press();
        Pumpen();
        Assert.Contains("nicht installiert", string.Join(" ", w.GetLogicalTexts()));
        Assert.DoesNotContain(l.Opened, o => o.Contains("UnrealEditor", StringComparison.OrdinalIgnoreCase));
        w.Show("studios");
        var karte = string.Join(" ", w.Find<Button>("studio-card-CORE-06").GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
        Assert.Contains("Spiele bauen", karte);
        Assert.DoesNotContain("mit der Unreal Engine", karte);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0205: Bild drehen und Text, Dauer eines Films")]
    public void Medien()
    {
        var (w, _, root) = Ui.Open();
        var png = Path.Combine(root, "foto.png");
        var weiss = new byte[40 * 20 * 3];
        Array.Fill(weiss, (byte)240);
        BildStudio.SavePng(new RgbImage(40, 20, weiss), png);
        w.Show("kreativ-medien");
        w.Find<TextBox>("media-path").Text = png;
        w.Find<Button>("media-use").Press();
        Assert.NotNull(w.TryFind<Image>("media-preview"));
        w.Find<Button>("media-rot").Press();
        Assert.Contains("gedreht", string.Join(" ", w.GetLogicalTexts()));
        w.Find<Button>("media-text").Press();
        Assert.Contains("Text eingefügt", string.Join(" ", w.GetLogicalTexts()));
        Assert.Contains(Directory.GetFiles(w.MediaFolder), f => f.EndsWith(".png"));
        if (Cli.Find("ffmpeg") is { } ff)
        {
            var film = Path.Combine(root, "kurz.mp4");
            var gemacht = Cli.Run(ff, ["-y", "-f", "lavfi", "-i", "color=c=blue:s=160x120:d=2", "-f", "lavfi", "-i", "sine=f=440:d=2", film]);
            if (gemacht.Ok)
            {
                w.Show("kreativ-medien");
                w.Find<TextBox>("media-path").Text = film;
                w.Find<Button>("media-use").Press();
                Assert.Contains("Sekunden", w.Find<TextBlock>("media-duration").Text);
                Assert.NotNull(w.TryFind<Image>("media-thumb-0"));
                w.Find<NumericUpDown>("media-end").Value = 1;
                w.Find<Button>("media-clip-add").Press();
                w.Find<Button>("media-clip-add").Press();
                w.Find<Button>("media-concat").Press();
                Assert.Contains("Fertiger Film", string.Join(" ", w.GetLogicalTexts()));
            }
        }
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0206: Editor, Vorschau der Überschrift und Baukasten im Studio")]
    public void Webseite()
    {
        var (w, l, _) = Ui.Open();
        w.AppPathOverride = exe => exe == "code" ? "/usr/bin/true" : null;
        w.Show("dev");
        w.Find<TextBox>("dev-name").Text = "Hallo Web";
        w.Find<Button>("dev-create").Press();
        Assert.NotNull(w.TryFind<TextBox>("dev-editor"));
        Assert.Contains("1", w.Find<TextBlock>("dev-lines").Text);
        w.Find<Button>("dev-file-0").Press();
        w.Find<TextBox>("dev-ki").Text = "Ändere die Überschrift in Guten Tag";
        w.Find<Button>("dev-ki-preview").Press();
        Assert.Contains("Guten Tag", w.Find<TextBlock>("dev-ki-out").Text);
        var html = Directory.GetFiles(w.DevFolder, "index.html", SearchOption.AllDirectories)[0];
        Assert.DoesNotContain("Guten Tag", File.ReadAllText(html));
        w.Find<Button>("dev-ki-apply").Press();
        Assert.Contains("Guten Tag", File.ReadAllText(html));
        Assert.Contains("Guten Tag", string.Join(" ", w.Find<Border>("dev-preview").GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)));
        w.Show("baukasten");
        Assert.Contains("Willkommen", string.Join(" ", w.Find<StackPanel>("builder-live").GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)));
        w.Close();
        _ = l;
    }

    [AvaloniaFact(DisplayName = "T-0207: Listen statt Aufzählung, Einheit, Grundriss, Balken, Projekt")]
    public void Fach()
    {
        var (w, _, root) = Ui.Open();
        w.Show("technik");
        Assert.NotNull(w.TryFind<ComboBox>("eng-beam-case"));
        Assert.Equal("m", w.Find<TextBlock>("eng-beam-len-unit").Text);
        w.Find<TextBox>("eng-beam-len").Text = "abc";
        Pumpen();
        Assert.Equal("Bitte eine Zahl eingeben", w.Find<TextBlock>("eng-beam-len-hinweis").Text);
        Assert.NotNull(w.TryFind<Canvas>("eng-beam-bars"));
        foreach (var cb in w.GetLogicalDescendants().OfType<ComboBox>())
        {
            var werte = cb.Items.Cast<object>().Select(o => o?.ToString() ?? "").Where(s => s.Length > 0).ToList();
            if (werte.Count < 3) continue;
            var liste = string.Join(", ", werte);
            Assert.DoesNotContain(w.GetLogicalTexts(), t => t.Contains(liste, StringComparison.OrdinalIgnoreCase));
        }
        W6Ui.Run(w, "eng-beam", ("len", "2"));
        w.Find<Button>("fach-save").Press();
        Assert.True(File.Exists(Path.Combine(root, "fach", "technik", "stand.json")));
        w.Show("architektur");
        Assert.NotNull(w.TryFind<Canvas>("arch-plan"));
        W6Ui.Run(w, "arch-add", ("name", "Küche"), ("l", "4"), ("w", "3"));
        Assert.Contains("Küche", string.Join(" ", w.Find<Canvas>("arch-plan").GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)));
        w.Settings.SimpleMode = true;
        w.Show("architektur");
        Assert.Null(w.TryFind<Button>("arch-add-run"));
        Assert.Contains("ausgeblendet", string.Join(" ", w.GetLogicalTexts()));
        w.Settings.SimpleMode = false;
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0212: Schalter zeigen Ein oder Aus, einfacher Modus blendet Fachstudios aus, Zeichen statt nur Farbe")]
    public void Barriere()
    {
        var (w, _, _) = Ui.Open();
        w.Show("settings");
        var schalter = w.Find<CheckBox>("show-widget-ki");
        Assert.Contains("Ein", schalter.Content?.ToString());
        var vorher = w.GetLogicalDescendants().OfType<Control>().Count(c => c.IsVisible);
        schalter.IsChecked = false;
        Pumpen();
        Assert.Contains("Aus", schalter.Content?.ToString());
        w.Show("bedien-sehen");
        Assert.Contains("Aus", w.Find<CheckBox>("simple-mode").Content?.ToString());
        w.Find<CheckBox>("color-blind").IsChecked = true;
        Pumpen();
        w = w.ReopenedAs ?? w;
        Assert.Contains("✗", w.Find<TextBlock>("status-sample").Text);
        w.Find<CheckBox>("a11y-motion").IsChecked = true;
        Pumpen();
        w = w.ReopenedAs ?? w;
        w.Show("studios");
        var ruhig = w.GetLogicalDescendants().OfType<Button>().First(b => b.Name?.StartsWith("studio-card-") == true);
        Assert.True(ruhig.Transitions?.OfType<Avalonia.Animation.TransformOperationsTransition>().Any() != true);
        var karten = w.GetLogicalDescendants().OfType<Button>().Count(b => b.IsVisible && b.Name?.StartsWith("studio-card-") == true);
        w.Settings.SimpleMode = true;
        w.Show("studios");
        var danach = w.GetLogicalDescendants().OfType<Button>().Count(b => b.IsVisible && b.Name?.StartsWith("studio-card-") == true);
        Assert.True(danach < karten);
        Assert.Null(w.TryFind<Button>("studio-card-SPEC-01"));
        w.Show("bedien-sehen");
        Assert.True(w.Find<Button>("font-0").MinHeight >= 40);
        w.Show("bedien-hilfen");
        Assert.Contains("Konfiguration", w.Find<TextBlock>("easy-sample").Text);
        w.Find<CheckBox>("easy-lang").IsChecked = true;
        Pumpen();
        w = w.ReopenedAs ?? w;
        Assert.Contains("Einstellung", w.Find<TextBlock>("easy-sample").Text);
        Assert.True(w.GetLogicalDescendants().OfType<Control>().Count(c => c.IsVisible) > 0);
        Assert.True(vorher > 0);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "T-0202: Schlüssel ohne Text sagt Bescheid, offline mit Hinweis")]
    public void Schluessel()
    {
        var (w, _, _) = Ui.Open();
        w.Show("keys");
        w.Find<Button>("btn-savekey-xai").Press();
        Pumpen();
        Assert.Contains("Bitte zuerst", w.Find<TextBlock>("key-state-xai").Text);
        w.Close();
        var (aus, _, _) = Ui.Open(offline: true);
        aus.Show("keys");
        foreach (var id in new[] { "xai", "github" })
        {
            var holen = aus.Find<Button>("btn-getkey-" + id);
            var speichern = aus.Find<Button>("btn-savekey-" + id);
            Assert.False(holen.IsEnabled);
            Assert.False(speichern.IsEnabled);
            Assert.False(string.IsNullOrWhiteSpace(ToolTip.GetTip(holen) as string));
            Assert.False(string.IsNullOrWhiteSpace(ToolTip.GetTip(speichern) as string));
        }
        aus.Close();
    }
}

public class Audit_Jeder_Knopf
{
    [AvaloniaFact(DisplayName = "T-0202: Jeder sichtbare Knopf auf jeder Seite bewirkt etwas", Timeout = 3600000)]
    public void Alle_Seiten()
    {
        var (w, l, root) = Ui.Open();
        var datei = Path.Combine(root, "probe.txt");
        File.WriteAllText(datei, "probe");
        Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
        w.PickOverride = _ => Task.FromResult<string[]?>([datei]);
        w.InstallOverride = _ => Task.FromResult(false);
        var seiten = w.PageKeys.OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var fehler = ButtonProbe.Require(w, l, root, seiten);
        Assert.True(fehler.Count == 0, fehler.Count + " ohne Wirkung oder mit Absturz:\n" + string.Join("\n", fehler.Take(40)));
        w.Close();
    }
}
