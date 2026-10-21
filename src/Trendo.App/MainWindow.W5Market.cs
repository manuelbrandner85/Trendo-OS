using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>BUS-003/004/006/007/008/009: Markt & Idee.</summary>
public partial class MainWindow
{
    public BusinessIdea BizIdea => CurrentIdea ??= Ideas.All().FirstOrDefault() ?? NewIdeaSaved();
    private BusinessIdea NewIdeaSaved() { var i = new BusinessIdea { Name = "Meine Idee" }; Ideas.Save(i); return i; }
    public IdeaList<Competitor> Competitors => new(_core.DataRoot, BizIdea.Id, "mitbewerber");
    public IdeaList<ProblemNote> Problems => new(_core.DataRoot, BizIdea.Id, "probleme");
    public IdeaList<Experiment> Experiments => new(_core.DataRoot, BizIdea.Id, "tests");

    private void InitW5Market() => RegisterTab(BusinessTabs, "biz-markt", "Markt", "BUS-003", MarketView, "mitbewerber", "konkurrenz", "markt", "nische", "zielgruppe", "validierung", "innovation");

    private Control IdeaHeader(string view)
    {
        var all = Ideas.All(); var row = new WrapPanel { Children = { P("Idee:  ") } }; var n = 0;
        foreach (var x in all) { var idea = x; var b = Act($"{view}-idea-{n++}", (idea.Id == BizIdea.Id ? "● " : "") + idea.Name, () => { CurrentIdea = idea; Show(view); }); b.Margin = new Avalonia.Thickness(0, 0, 6, 4); row.Children.Add(b); }
        if (all.Count == 0) row.Children.Add(P(BizIdea.Name, Text));
        return row;
    }

    private Control MarketView()
    {
        var i = BizIdea; var sp = new StackPanel { Spacing = 8, Children = { P("Lerne deinen Markt kennen: Wer bietet schon was? Wo drückt der Schuh? Teste, bevor du Geld ausgibst."), IdeaHeader("biz-markt") } };

        // BUS-003 Mitbewerber
        var comps = Competitors.All();
        var cn = new TextBox { Name = "mk-c-name", PlaceholderText = "Name", Width = 140 }; var cu = new TextBox { Name = "mk-c-url", PlaceholderText = "Webseite", Width = 160 };
        var cp = new TextBox { Name = "mk-c-price", PlaceholderText = "Preis €", Width = 80 }; var cs = new TextBox { Name = "mk-c-strong", PlaceholderText = "Stärken", Width = 160 };
        var cw = new TextBox { Name = "mk-c-weak", PlaceholderText = "Schwächen", Width = 160 }; var cd = new TextBox { Name = "mk-c-desc", PlaceholderText = "Was bieten sie an?", Width = 300 };
        var csec = new StackPanel { Spacing = 4, Children = { H("Mitbewerber", 14), new WrapPanel { Children = { cn, cu, cp, cs, cw, cd,
            Act("mk-c-add", "Hinzufügen", () => { if (string.IsNullOrWhiteSpace(cn.Text)) return; Competitors.Add(new(cn.Text!, cu.Text ?? "", ParseD(cp.Text), cs.Text ?? "", cw.Text ?? "", cd.Text ?? "")); Show("biz-markt"); }, true) } } } };
        for (var k = 0; k < comps.Count; k++) { var idx = k; var c = comps[k]; csec.Children.Add(Row(P($"{c.Name} · {c.Price:0.##} € · + {c.Strengths} · − {c.Weaknesses}", Text, 11), Act($"mk-c-del-{k}", "Entfernen", () => { Competitors.RemoveAt(idx); Show("biz-markt"); }))); }
        if (BusinessMarket.Prices(comps, i.Price) is { } pp)
        {
            csec.Children.Add(new TextBlock { Name = "mk-price-pos", Text = $"Preise: {pp.Min:0.##} – {pp.Max:0.##} € (Mitte {pp.Median:0.##} €). {pp.Where}", Foreground = Text, TextWrapping = TextWrapping.Wrap });
            if (pp.CommonWeaknesses.Count > 0) csec.Children.Add(P("Lücke im Markt – mehrere Mitbewerber schwach bei: " + string.Join(", ", pp.CommonWeaknesses.Select(w => w.Word)), Brush.Parse("#22C55E"), 11));
        }
        // BUS-008
        var orig = BusinessMarket.Originality(i, comps);
        if (orig.Count > 0) csec.Children.Add(new TextBlock { Name = "mk-orig", Text = $"Eigenständigkeit: {BusinessMarket.OriginalityText(orig[0].Score)} (am ähnlichsten: {orig[0].Name}, {orig[0].Score:P0})", Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 11 });
        sp.Children.Add(Card(csec));

        // BUS-004 Probleme & Nischen
        var pw = new TextBox { Name = "mk-p-who", PlaceholderText = "Wer? z. B. Pflegekräfte", Width = 160 }; var pt = new TextBox { Name = "mk-p-text", PlaceholderText = "Welches Problem hat die Person?", Width = 280 };
        var pain = new NumericUpDown { Name = "mk-p-pain", Minimum = 1, Maximum = 5, Value = 3, Width = 110 }; var freq = new NumericUpDown { Name = "mk-p-freq", Minimum = 1, Maximum = 5, Value = 3, Width = 110 };
        var psec = new StackPanel { Spacing = 4, Children = { H("Probleme und Nischen", 14), P("Schreib auf, was Menschen dir erzählen. Wie schlimm (1–5) und wie oft (1–5)?", null, 11),
            new WrapPanel { Children = { pw, pt, P(" schlimm "), pain, P(" oft "), freq, Act("mk-p-add", "Merken", () => { if (string.IsNullOrWhiteSpace(pt.Text)) return; Problems.Add(new(pw.Text ?? "", pt.Text!, (int)(pain.Value ?? 3), (int)(freq.Value ?? 3))); Show("biz-markt"); }) } } } };
        var n = 0;
        foreach (var ni in BusinessMarket.Niches(Problems.All()).Take(6)) psec.Children.Add(new TextBlock { Name = $"mk-niche-{n++}", Text = $"{ni.Score} Punkte · {ni.Who}: {ni.Problem} ({ni.Mentions}× genannt)", Foreground = Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(Card(psec));

        // BUS-006/007 Varianten
        var vsec = new StackPanel { Spacing = 3, Children = { H("Neue Varianten deiner Idee", 14) } };
        foreach (var v in BusinessMarket.Variants(i).Concat(BusinessMarket.Fusions(i, 4))) vsec.Children.Add(P($"{v.Method}: {v.Text}", null, 11));
        sp.Children.Add(Card(vsec));

        // BUS-009 Tests
        var ex = Experiments.All();
        var eh = new TextBox { Name = "mk-e-hyp", PlaceholderText = "Annahme, z. B. 3 Vorbestellungen in 2 Wochen", Width = 320 }; var em = new TextBox { Name = "mk-e-metric", PlaceholderText = "Messgröße", Width = 140 }; var et = new TextBox { Name = "mk-e-target", PlaceholderText = "Ziel", Width = 70 };
        var esec = new StackPanel { Spacing = 4, Children = { H("Am Markt testen", 14), new WrapPanel { Children = { eh, em, et, Act("mk-e-add", "Test anlegen", () => { if (string.IsNullOrWhiteSpace(eh.Text)) return; Experiments.Add(new(eh.Text!, em.Text ?? "", ParseD(et.Text))); Show("biz-markt"); }) } } } };
        if (ex.Count == 0) { var sug = Act("mk-e-suggest", "Vorschläge übernehmen", () => { foreach (var s in BusinessMarket.ExperimentIdeas) Experiments.Add(s); Show("biz-markt"); }); esec.Children.Add(sug); }
        for (var k = 0; k < ex.Count; k++)
        {
            var idx = k; var e = ex[k]; var res = new TextBox { Name = $"mk-e-res-{k}", Text = e.Result?.ToString("0.##") ?? "", PlaceholderText = "Ergebnis", Width = 80 };
            esec.Children.Add(Row(P($"{e.Hypothesis} · Ziel {e.Target:0.##} {e.Metric} · {e.State}", Text, 11), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { res,
                Act($"mk-e-save-{k}", "Eintragen", () => { var l = Experiments.All(); l[idx] = l[idx] with { Result = string.IsNullOrWhiteSpace(res.Text) ? null : ParseD(res.Text) }; Experiments.Save(l); Show("biz-markt"); }) } }));
        }
        var val = BusinessMarket.Validation(ex);
        esec.Children.Add(new TextBlock { Name = "mk-validation", Text = $"{val.Confirmed} von {val.Done} Tests bestätigt. {val.Verdict}", Foreground = Text, FontWeight = FontWeight.SemiBold });
        esec.Children.Add(Nav2("Angebotsseite zum Testen bauen", "produktion"));
        sp.Children.Add(Card(esec));
        return BusinessPage("Markt", sp);
    }

    private static double ParseD(string? s) { try { return (double)Finance.ParseAmount(s ?? ""); } catch (FormatException) { return 0; } }
}
