using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>SPEC-08: echter Schnitt, Stoffe, Zuschnitt, Anprobe, Produktion in der Oberfläche.</summary>
public class Garment_Ui
{
    static (MainWindow W, ProgramDocument D) Open()
    {
        var (w, _, _) = Ui.Open();
        var key = MainWindow.StudioPages["SPEC-08"]; w.Show(key); W2Ui.Settle();
        w.Find<Button>("prog-tpl-tshirt").Press(); W2Ui.Settle();
        return (w, w.ProgramActive(key)!);
    }

    [AvaloniaFact(DisplayName = "Kleidung: Schnittteile mit Kurven, Zuschnitt und Kosten erscheinen; Stoff und Schnitt lassen sich wechseln")]
    public void Panel()
    {
        var (w, d) = Open();
        try
        {
            Assert.NotNull(w.TryFind<Canvas>("garment-pattern")); Assert.NotNull(w.TryFind<Canvas>("garment-nest"));
            Assert.Contains("m Stoff", w.Find<TextBlock>("garment-nest-facts").Text);
            var before = w.Find<TextBlock>("garment-nest-facts").Text;
            w.Find<ComboBox>("garment-fabric").SelectedIndex = Array.FindIndex(Fabrics.All, f => f.Id == "seide"); W2Ui.Settle();
            Assert.Equal("seide", w.ProgramActive(MainWindow.StudioPages["SPEC-08"])!.Text("gFabric"));
            Assert.NotEqual(before, w.Find<TextBlock>("garment-nest-facts").Text);
            w.Find<Button>("garment-kind-rock").Press(); W2Ui.Settle();
            Assert.Equal("rock", w.ProgramActive(MainWindow.StudioPages["SPEC-08"])!.Text("gKind"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Kleidung: Anprobe schreibt einen vollständigen Auftrag, zeigt Bilder; Fehler von Blender werden gemeldet")]
    public async Task TryOn()
    {
        var (w, d) = Open();
        try
        {
            JsonObject? job = null;
            w.GarmentRunOverride = f =>
            {
                job = JsonNode.Parse(File.ReadAllText(f))!.AsObject();
                File.WriteAllBytes((string)job["out"]! + "-vorn.png", Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="));
                return Task.FromResult<string?>(null);
            };
            await w.GarmentTryOn(d); W2Ui.Settle();
            Assert.Equal(5, job!["parts"]!.AsArray().Count); Assert.Equal(15d, (double)job["fabric"]!["tension"]!);
            Assert.Single(w.GarmentImages); Assert.Contains("Fertig", w.GarmentMessage);
            Assert.NotNull(w.TryFind<WrapPanel>("garment-images"));
            w.GarmentRunOverride = _ => Task.FromResult<string?>("kaputt");
            await w.GarmentTryOn(d); W2Ui.Settle();
            Assert.Contains("kaputt", w.GarmentMessage);
        }
        finally { w.Close(); }
    }

    [AvaloniaTheory(DisplayName = "Kleidung: Produktionsdateien werden geschrieben")]
    [InlineData(".pdf")]
    [InlineData(".dxf")]
    [InlineData(".md")]
    public void Export(string ext)
    {
        var (w, d) = Open();
        try { var f = w.GarmentExport(d, ext); Assert.NotNull(f); Assert.True(new FileInfo(f!).Length > 100); Assert.Contains("Gespeichert", w.GarmentMessage); }
        finally { w.Close(); }
    }
}
