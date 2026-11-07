using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;
using Trendo.App;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Paket 01 – Studio 3, S3-13 (07.11.2026): KI-Video im Videoschnitt.</summary>
public class VideoGen_Ui
{
    [AvaloniaFact(DisplayName = "S3-13 Videoschnitt: KI-Clip mit Zufallswert in die Zeitleiste, Herkunftsnachweis daneben (rückgängig machbar); ohne Modell nur Hinweis; liefert die KI nichts, wird nichts eingefügt")]
    public async Task Generate_clip()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); w.EditTab = "medien"; w.Show("schnitt"); Settle();
            // ohne Modell: nur Hinweis + Weg zur KI-Bibliothek, kein Knopf zum Erzeugen
            Assert.Null(w.TryFind<Button>("ve-gen-run")); Assert.Contains(w.GetLogicalDescendantsOfType<TextBlock>(), t => (t.Text ?? "").Contains("Kein Video-Modell eingerichtet"));
            var md = Path.Combine(root, "modell"); Directory.CreateDirectory(md);
            string F(string n) { var p = Path.Combine(md, n); File.WriteAllBytes(p, new byte[16]); return p; }
            w.EditGenModelOverride = new GenVideo.Model("wan2.2-szenen", "Wan 2.2 – Filmszenen (5B)", "Apache-2.0", F("a.gguf"), F("t5.gguf"), F("vae.safetensors"));
            var calls = new List<IReadOnlyList<string>>(); var deliver = true;
            w.VideoKiOverride = (exe, a) => { calls.Add(a); if (deliver) File.WriteAllBytes(a[a.ToList().IndexOf("-o") + 1], new byte[256]); return Task.FromResult(deliver); };
            w.FfmpegRunOverride = a => { File.WriteAllBytes(a[^1], new byte[512]); return new Cli.Result(0, "", ""); };
            w.Show("schnitt"); Settle();
            w.Find<TextBox>("ve-gen-prompt").Text = "Kaffee wird in eine Tasse gegossen, Morgenlicht";
            w.Find<TextBox>("ve-gen-seed").Text = "777";
            var b = w.Find<Button>("ve-gen-run"); b.BringIntoView(); Settle(); b.Press();
            for (var i = 0; i < 100 && w.EditGenLastClip is null; i++) { await Task.Delay(20); Settle(); }
            var d = w.ProgramActive("schnitt")!; var clips = VideoEdit.Clips(d.Data);
            Assert.Single(clips); Assert.StartsWith("KI: Kaffee", clips[0].Title); Assert.Equal(2.0, clips[0].Length);
            Assert.Equal("777", calls[0][calls[0].ToList().IndexOf("-s") + 1]);
            Assert.Null(GenVideo.VerifyProvenance(w.EditGenLastClip!));
            Assert.Contains("\"zufallswert\": 777", File.ReadAllText(GenVideo.ProvenancePath(w.EditGenLastClip!)));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(w.EditGenLastClip!)!, "*.roh.webm"));
            Assert.Equal("KI-Video", d.UndoLabel);
            // KI liefert nichts → nichts eingefügt, ehrliche Meldung
            deliver = false; w.Show("schnitt"); Settle(); w.Find<Button>("ve-gen-run").Press();
            for (var i = 0; i < 100 && !w.ProgramMessage("schnitt").Contains("keinen Clip"); i++) { await Task.Delay(20); Settle(); }
            Assert.Contains("keinen Clip geliefert – nichts eingefügt", w.ProgramMessage("schnitt"));
            Assert.Single(VideoEdit.Clips(w.ProgramActive("schnitt")!.Data));
        }
        finally { w.Close(); }
    }
}
