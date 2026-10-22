using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-01: Studio-Fusion – Abläufe über mehrere Studios, mit Freigabe.</summary>
public partial class MainWindow
{
    public string FusionFolder => WorkFolder("Produktion");
    private int _fusionRecipe;
    private readonly Dictionary<string, string> _fusionInput = new();
    private string? _fusionApproval;
    private IReadOnlyList<StepResult> _fusionResult = [];

    private void InitW5Fusion() => RegisterTab(BusinessTabs, "produktion", "Produktion", "X-01", FusionView, "ablauf", "automatisch", "fusion", "kette", "produktion");

    private FusionContext FusionCtx(Recipe r) => new() { WorkDir = Path.Combine(FusionFolder, $"{DateTime.Now:yyyy-MM-dd-HHmmss}-{r.Id}"), Input = new(_fusionInput), Ffmpeg = FfmpegPath, Blender = BlenderPath };

    public void FusionRequest()
    {
        var r = Fusion.Recipes[_fusionRecipe];
        _fusionApproval = _core.Approvals.Request(Principal.LocalOwner, "fusion.run", $"Ablauf „{r.Label}“ ausführen", r.Id).Id;
        Show("produktion");
    }

    public void FusionApproveAndRun()
    {
        if (_fusionApproval is not { } id) return;
        var r = Fusion.Recipes[_fusionRecipe];
        _core.Approvals.Approve(id, Principal.LocalOwner);
        _fusionResult = _core.Approvals.Execute(id, "fusion.run", r.Id, () => Fusion.Run(r, FusionCtx(r), _core.Audit));
        _fusionApproval = null;
        Say(_fusionResult.All(x => x.Ok) ? "Fertig. Alle Schritte haben geklappt." : "Angehalten – ein Schritt hat nicht geklappt. Details unten.", "produktion");
    }

    private Control FusionView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Ein Ablauf verbindet mehrere Studios. Du siehst zuerst den Plan und gibst ihn frei. Dann erledigt Trendo alles nacheinander.") } };
        var pick = new WrapPanel();
        for (var k = 0; k < Fusion.Recipes.Count; k++)
        {
            var idx = k; var rr = Fusion.Recipes[k];
            var b = new Button { Name = "fusion-recipe-" + rr.Id, Width = 230, Margin = new Avalonia.Thickness(0, 0, 8, 8), Background = idx == _fusionRecipe ? Accent : TileBg, Content = new StackPanel { Children = { new TextBlock { Text = rr.Label, Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap }, P(rr.Help, null, 10.5) } } };
            b.Click += (_, _) => { _fusionRecipe = idx; _fusionApproval = null; _fusionResult = []; Show("produktion"); };
            pick.Children.Add(b);
        }
        sp.Children.Add(pick);
        var r = Fusion.Recipes[_fusionRecipe];
        var inputs = new StackPanel { Spacing = 4 };
        TextBox I(string key, string ph) { var t = new TextBox { Name = "fusion-in-" + key, Text = _fusionInput.GetValueOrDefault(key, ""), PlaceholderText = ph, MinWidth = 360 }; t.TextChanged += (_, _) => _fusionInput[key] = t.Text ?? ""; inputs.Children.Add(t); return t; }
        switch (r.Id)
        {
            case "idea-site": I("name", "Name der Idee"); I("problem", "Welches Problem löst du?"); I("solution", "Wie löst du es?"); I("audience", "Für wen?"); I("price", "Preis in €"); I("email", "Deine E-Mail für Anfragen"); break;
            case "video-pack":
                var video = new DateiFeld(this, "fusion-in-file", "Videodatei", _fusionInput.GetValueOrDefault("file", ""), false, "produktion", ["*.mp4", "*.mkv", "*.mov", "*.webm", "*.avi"]);
                video.Eingabe.MinWidth = 360;
                video.Eingabe.TextChanged += (_, _) => _fusionInput["file"] = video.Eingabe.Text ?? "";
                inputs.Children.Add(video);
                break;
            case "logo-print": I("text", "Wort für den Schriftzug, z. B. Trendo"); break;
        }
        sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { H("1. Angaben", 14), inputs } }));
        var ctx = FusionCtx(r); var missing = Fusion.Missing(r, ctx);
        var plan = new StackPanel { Spacing = 4, Children = { H("2. Plan", 14), P(Fusion.Plan(r), Text) } };
        foreach (var m in missing) plan.Children.Add(P("• " + m, Brush.Parse("#FACC15")));
        if (_fusionApproval is null)
        {
            var go = Act("fusion-request", "Plan freigeben …", FusionRequest, true); go.IsEnabled = missing.Count == 0;
            plan.Children.Add(go);
        }
        else plan.Children.Add(Card(Row(P("Soll Trendo diese Schritte jetzt ausführen?", Text), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
            Act("fusion-approve", "Ja, ausführen", FusionApproveAndRun, true), Act("fusion-deny", "Nein", () => { _core.Approvals.Deny(_fusionApproval!, Principal.LocalOwner); _fusionApproval = null; Show("produktion"); }) } })));
        sp.Children.Add(Card(plan));
        if (_fusionResult.Count > 0)
        {
            var res = new StackPanel { Spacing = 3, Children = { H("3. Ergebnis", 14) } };
            var n = 0;
            foreach (var x in _fusionResult) res.Children.Add(new TextBlock { Name = $"fusion-result-{n++}", Text = $"{(x.Ok ? "✓" : "✗")} {x.Label}: {x.Detail}", Foreground = x.Ok ? Text : Brush.Parse("#F97316"), TextWrapping = TextWrapping.Wrap });
            res.Children.Add(Act("fusion-folder", "Ergebnisse öffnen", () => Launch(FusionFolder)));
            sp.Children.Add(Card(res));
        }
        return BusinessPage("Produktion", sp);
    }
}
