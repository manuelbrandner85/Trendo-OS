using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 11 – CORE-12: Anweisung gegen eigene Regeln testen und die aktuelle mit der vorherigen Fassung vergleichen.</summary>
public partial class MainWindow
{
    private const int PromptTestRuns = 3;
    public IReadOnlyList<PromptVariantResult> LastPromptTest { get; private set; } = [];
    private string _promptTestId = "";

    private Control PromptTestCard(PromptTemplate t, Action grab)
    {
        var sp = new StackPanel { Spacing = 6, Children = { H("Anweisung testen", 14),
            P("Schreib auf, was eine gute Antwort erfüllen muss. Trendo fragt die KI mehrmals und zählt, wie oft alles stimmt" + (t.Versions.Count > 1 ? " – für diese und die vorherige Fassung." : "."), Muted, 11.5) } };
        var box = new TextBox { Name = "ptest-checks", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 74, Text = Prompts.ChecksFor(t.Id),
            PlaceholderText = "Eine Regel pro Zeile, zum Beispiel:\nenthält: Preis\nnicht: leider\nhöchstens 80 Wörter\nist JSON\nMuster: \\d+ Euro" };
        var run = new Button { Name = "ptest-run", Content = $"Testen ({PromptTestRuns} Durchläufe je Fassung)", Background = Accent };
        run.Click += async (_, _) =>
        {
            grab();
            var (checks, errors) = PromptEval.Parse(box.Text ?? "");
            if (errors.Count > 0) { _promptMsg = string.Join(" ", errors); Show("ki-vorlagen"); return; }
            if (checks.Count == 0) { _promptMsg = "Bitte mindestens eine Regel eintragen."; Show("ki-vorlagen"); return; }
            var variants = new List<(string, string)>();
            foreach (var v in t.Versions.AsEnumerable().Reverse().Take(2))
            {
                var (text, missing) = PromptLibrary.Fill(v.Text, _promptVals);
                if (missing.Count > 0) { _promptMsg = "Bitte noch ausfüllen: " + string.Join(", ", missing); Show("ki-vorlagen"); return; }
                variants.Add(($"Version {v.Version}", text));
            }
            Prompts.SaveChecks(t.Id, box.Text ?? "");
            run.IsEnabled = false; box.IsEnabled = false;
            try
            {
                using var kiLabel = KiActivityHub.Label("KI-Anweisung testen");
                LastPromptTest = await PromptEval.RunAsync(variants, checks, PromptTestRuns, async (prompt, ct) =>
                {
                    var a = await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false), ct);
                    return (a.Ok, a.Text);
                });
                _promptTestId = t.Id; _promptMsg = "";
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { LastPromptTest = []; _promptMsg = "Test fehlgeschlagen: " + Trendo.Core.FriendlyError.Say(ex); }
            Show("ki-vorlagen");
        };
        sp.Children.Add(box); sp.Children.Add(run);
        if (_promptTestId == t.Id) foreach (var r in LastPromptTest) sp.Children.Add(PromptTestRow(r));
        return WCard(sp);
    }

    private Control PromptTestRow(PromptVariantResult r)
    {
        var all = r.Passed == r.Runs.Count;
        var col = new StackPanel { Spacing = 3, Margin = new Thickness(0, 6, 0, 0), Children = {
            new TextBlock { Name = "ptest-result-" + r.Label.Replace(" ", "-"), Text = $"{r.Label}: {r.Passed} von {r.Runs.Count} Durchläufen bestanden", Foreground = all ? Brush.Parse("#4ADE80") : Brush.Parse("#FACC15"), FontSize = 13, FontWeight = FontWeight.SemiBold } } };
        if (r.Runs.Any(x => !x.Answered)) col.Children.Add(P($"{r.Runs.Count(x => !x.Answered)} Durchläufe ohne Antwort: {r.Runs.First(x => !x.Answered).Answer}", Brush.Parse("#FACC15"), 11));
        var answered = r.Runs.Where(x => x.Answered).ToList();
        if (answered.Count > 0)
            foreach (var label in answered[0].Checks.Select(c => c.Check.Label))
            {
                var hits = answered.Count(x => x.Checks.First(c => c.Check.Label == label).Ok);
                col.Children.Add(P($"{(hits == answered.Count ? "✓" : "⚠")} {label}: {hits} von {answered.Count}", null, 11));
            }
        var failed = answered.FirstOrDefault(x => !x.Ok);
        if (failed is not null) col.Children.Add(new Expander { Header = "Beispiel einer nicht bestandenen Antwort", HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new SelectableTextBlock { Text = failed.Answer.Length > 1500 ? failed.Answer[..1500] + " …" : failed.Answer, Foreground = Text, TextWrapping = TextWrapping.Wrap, FontSize = 12 } });
        return col;
    }
}
