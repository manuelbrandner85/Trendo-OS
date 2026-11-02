using System.IO.Compression;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// 0.9.46 – Spiele veröffentlichen: Ziele (auch aus „platforms“ erkannt), Checkliste je Plattform, Pakete je Ziel, Hochladen nur nach Ja mit eigenem Konto.
public class GameRelease_Ui
{
    static string Built(string root)
    {
        var dir = Path.Combine(root, "gebaut"); Directory.CreateDirectory(Path.Combine(dir, ".trendo"));
        File.WriteAllText(Path.Combine(dir, "index.html"), "<!doctype html><title>Spiel</title><canvas></canvas>");
        File.WriteAllText(Path.Combine(dir, "spiel.js"), "start();"); File.WriteAllText(Path.Combine(dir, ".trendo", "x.txt"), "intern");
        return dir;
    }

    [Fact(DisplayName = "Release-Kern: Ziele aus „platforms“, Checkliste je Ziel, Pakete (Web, Windows, Mac, Handy, Steam) ohne Hilfsdateien, Hochladebefehle nur mit gültigen Angaben")]
    public void Core()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-release-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var d = JsonNode.Parse("""{"title":"Frontlinie: Nacht","platforms":"Windows und Mac, später Steam"}""")!.AsObject();
            Assert.Equal(["windows", "mac", "steam"], GameRelease.TargetsOf(d));
            Assert.Contains(GameRelease.ChecklistFor(["steam"]), i => i.Id == "konto-steam"); Assert.DoesNotContain(GameRelease.ChecklistFor(["browser"]), i => i.Id == "konto-steam");
            GameRelease.SetTargets(d, ["browser", "windows", "mac", "handy", "steam"]);
            var (files, notes) = GameRelease.Pack(d, Built(root), Path.Combine(root, "out"), GameRelease.TargetsOf(d));
            Assert.Equal(5, files.Count);
            using (var z = ZipFile.OpenRead(files.Single(f => f.EndsWith("-web.zip"))))
            { Assert.Contains(z.Entries, e => e.FullName == "index.html"); Assert.DoesNotContain(z.Entries, e => e.FullName.StartsWith(".trendo")); }
            using (var z = ZipFile.OpenRead(files.Single(f => f.EndsWith("-windows.zip")))) Assert.Contains(z.Entries, e => e.FullName == "Spielen.cmd");
            using (var z = ZipFile.OpenRead(files.Single(f => f.EndsWith("-mac.zip")))) Assert.Contains(z.Entries, e => e.FullName == "Spielen.command");
            using (var z = ZipFile.OpenRead(files.Single(f => f.EndsWith("-handy.zip")))) Assert.Contains(z.Entries, e => e.FullName == "manifest.webmanifest");
            var steam = files.Single(Directory.Exists);
            Assert.True(File.Exists(Path.Combine(steam, "content", "index.html"))); Assert.Contains("DEINE_APP_ID", File.ReadAllText(Path.Combine(steam, "scripts", "app_build.vdf")));
            Assert.Contains(notes, n => n.StartsWith("Steam:"));
            Assert.Null(GameRelease.Upload(d, "itchio", "x.zip")); // ohne itch.io-Seite kein Befehl
            GameRelease.Set(d, "itch", "erika/frontlinie"); Assert.Equal("butler push \"x.zip\" erika/frontlinie:html5", GameRelease.Upload(d, "itchio", "x.zip"));
            GameRelease.Set(d, "itch", "erika/front linie; del"); Assert.Null(GameRelease.Upload(d, "itchio", "x.zip")); // nichts Fremdes im Befehl
            Assert.Null(GameRelease.Upload(d, "steam", steam));
            GameRelease.Set(d, "steamApp", "123450"); GameRelease.Set(d, "steamUser", "erika"); Assert.StartsWith("steamcmd +login erika +run_app_build", GameRelease.Upload(d, "steam", steam));
            var unreal = GameRelease.Pack(d, Path.Combine(root, "leer"), Path.Combine(root, "out2"), ["windows"]);
            Assert.Empty(unreal.Files); Assert.Contains("Unreal", unreal.Notes.Single());
        }
        finally { Directory.Delete(root, true); }
    }

    [AvaloniaFact(DisplayName = "Spiele bauen → Veröffentlichen: Ziele wählen, Checkliste abhaken, Pakete bauen, Hochladen zu itch.io erst nach Ja – mit eigenem Konto")]
    public void Release_view()
    {
        var (w, _, root) = Ui.Open();
        var uploads = new List<string>(); MainWindow.UploadOverride = uploads.Add;
        try
        {
            var page = MainWindow.Programs.Keys.First(k => MainWindow.Programs[k].Studio == "CORE-06");
            w.Show(page); var d = w.ProgramNew(page);
            d.Set("title", JsonValue.Create("Frontlinie")); d.Set("built", JsonValue.Create(Built(root)));
            d.Edit("Ziele", data => GameRelease.SetTargets(data, [])); w.GameSection = "Veröffentlichen"; w.Show(page); // ohne Vorauswahl
            Assert.NotNull(w.TryFind<StackPanel>("game-release"));
            w.Find<Button>("game-rel-target-browser").Press(); w.Find<Button>("game-rel-target-itchio").Press();
            Assert.Contains("browser", GameRelease.TargetsOf(d.Data)); Assert.Contains("itchio", GameRelease.TargetsOf(d.Data)); // die Vorlage bringt schon Windows mit
            w.Find<CheckBox>("game-rel-check-test").IsChecked = true;
            Assert.True(GameRelease.Done(d.Data, "test"));
            w.Find<Button>("game-rel-pack").Press();
            Assert.Contains(w.GamePackages, f => f.EndsWith("-web.zip"));
            d.Edit("itch", data => GameRelease.Set(data, "itch", "erika/frontlinie")); w.Show(page);
            w.Find<Button>("game-rel-upload-itchio").Press();
            Assert.Empty(uploads); // erst nach Ja
            w.Find<Button>("game-rel-upload-yes").Press();
            Assert.Single(uploads); Assert.StartsWith("butler push", uploads[0]); Assert.EndsWith("erika/frontlinie:html5", uploads[0]);
        }
        finally { MainWindow.UploadOverride = null; w.Close(); }
    }
}
