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
    private int? _edComp, _edProb, _edExp;

    private void InitW5Market() => RegisterTab(BusinessTabs, "biz-markt", "Markt", "BUS-003", MarketView, "mitbewerber", "konkurrenz", "markt", "nische", "zielgruppe", "validierung", "innovation");

    private string _bizMark = "";
    private void Mark(TextBox box)
    {
        if (box.Name is null || _bizMark != box.Name) return;
        box.BorderBrush = Brush.Parse("#F87171"); box.BorderThickness = new Avalonia.Thickness(2);
    }
    private void BizNeed(string view, string key, string msg) { _bizMark = key; Say(msg, view); }
    private void BizOk(string view, string msg) { _bizMark = ""; Say(msg, view); }

    private Control IdeaHeader(string view)
    {
        var all = Ideas.All(); var row = new WrapPanel { Children = { P("Idee:  ") } }; var n = 0;
        foreach (var x in all)
        {
            var idea = x;
            var b = Act($"{view}-idea-{n++}", (idea.Id == BizIdea.Id ? "● " : "") + idea.Name, () =>
            {
                if (idea.Id == BizIdea.Id) BizOk(view, $"Idee „{idea.Name}“ ist schon ausgewählt.");
                else { CurrentIdea = idea; BizOk(view, $"Idee „{idea.Name}“ ist ausgewählt."); }
            });
            b.Margin = new Avalonia.Thickness(0, 0, 6, 4); row.Children.Add(b);
        }
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
        Mark(cn);
        if (_edComp is int eci && eci >= 0 && eci < comps.Count) { var e = comps[eci]; cn.Text = e.Name; cu.Text = e.Url; cp.Text = e.Price.ToString("0.##", De); cs.Text = e.Strengths; cw.Text = e.Weaknesses; cd.Text = e.Description; }
        var csec = new StackPanel { Spacing = 4, Children = { H("Mitbewerber", 14), new WrapPanel { Children = { cn, cu, cp, cs, cw, cd,
            Act("mk-c-add", _edComp is null ? "Hinzufügen" : "Änderung speichern", () =>
            {
                if (string.IsNullOrWhiteSpace(cn.Text)) { BizNeed("biz-markt", "mk-c-name", "Bitte den Namen des Mitbewerbers eintragen."); return; }
                var item = new Competitor(cn.Text!.Trim(), cu.Text ?? "", ParseD(cp.Text), cs.Text ?? "", cw.Text ?? "", cd.Text ?? "");
                if (_edComp is int ei && ei >= 0 && ei < Competitors.All().Count) { var l = Competitors.All(); l[ei] = item; Competitors.Save(l); _edComp = null; BizOk("biz-markt", $"Mitbewerber „{item.Name}“ geändert."); }
                else { Competitors.Add(item); BizOk("biz-markt", $"Mitbewerber „{item.Name}“ steht in der Liste."); }
            }, true) } } } };
        for (var k = 0; k < comps.Count; k++)
        {
            var idx = k; var c = comps[k];
            csec.Children.Add(Row(P($"{c.Name} · {c.Price:0.##} € · + {c.Strengths} · − {c.Weaknesses}", Text, 11), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = {
                Act($"mk-c-edit-{k}", "Ändern", () => { _edComp = idx; BizOk("biz-markt", $"„{c.Name}“ steht in den Feldern. Änder und speichere."); }),
                Act($"mk-c-del-{k}", "Entfernen", () => { Competitors.RemoveAt(idx); if (_edComp == idx) _edComp = null; BizOk("biz-markt", $"Mitbewerber „{c.Name}“ entfernt."); }) } }));
        }
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
        var notes = Problems.All();
        var pw = new TextBox { Name = "mk-p-who", PlaceholderText = "Wer? z. B. Pflegekräfte", Width = 160 }; var pt = new TextBox { Name = "mk-p-text", PlaceholderText = "Welches Problem hat die Person?", Width = 280 };
        var pain = new NumericUpDown { Name = "mk-p-pain", Minimum = 1, Maximum = 5, Value = 3, Width = 110 }; var freq = new NumericUpDown { Name = "mk-p-freq", Minimum = 1, Maximum = 5, Value = 3, Width = 110 };
        Mark(pt);
        if (_edProb is int epi && epi >= 0 && epi < notes.Count) { var e = notes[epi]; pw.Text = e.Who; pt.Text = e.Problem; pain.Value = e.Pain; freq.Value = e.Frequency; }
        var psec = new StackPanel { Spacing = 4, Children = { H("Probleme und Nischen", 14), P("Schreib auf, was Menschen dir erzählen. Wie schlimm (1–5) und wie oft (1–5)?", null, 11),
            new WrapPanel { Children = { pw, pt, P(" schlimm "), pain, P(" oft "), freq, Act("mk-p-add", _edProb is null ? "Merken" : "Änderung speichern", () =>
            {
                if (string.IsNullOrWhiteSpace(pt.Text)) { BizNeed("biz-markt", "mk-p-text", "Bitte das Problem eintragen, das die Person hat."); return; }
                var item = new ProblemNote(pw.Text ?? "", pt.Text!.Trim(), (int)(pain.Value ?? 3), (int)(freq.Value ?? 3));
                if (_edProb is int ei && ei >= 0 && ei < Problems.All().Count) { var l = Problems.All(); l[ei] = item; Problems.Save(l); _edProb = null; BizOk("biz-markt", $"Problem „{item.Problem}“ geändert."); }
                else { Problems.Add(item); BizOk("biz-markt", $"Problem „{item.Problem}“ gemerkt."); }
            }) } } } };
        for (var pi = 0; pi < notes.Count; pi++)
        {
            var idx = pi; var p = notes[pi];
            psec.Children.Add(Row(P($"{p.Who}: {p.Problem}", Text, 11), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = {
                Act($"mk-p-edit-{pi}", "Ändern", () => { _edProb = idx; BizOk("biz-markt", $"„{p.Problem}“ steht in den Feldern."); }),
                Act($"mk-p-del-{pi}", "Löschen", () => { Problems.RemoveAt(idx); if (_edProb == idx) _edProb = null; BizOk("biz-markt", $"Problem „{p.Problem}“ gelöscht."); }) } }));
        }
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
        Mark(eh);
        if (_edExp is int eei && eei >= 0 && eei < ex.Count) { var e = ex[eei]; eh.Text = e.Hypothesis; em.Text = e.Metric; et.Text = e.Target.ToString("0.##", De); }
        var esec = new StackPanel { Spacing = 4, Children = { H("Am Markt testen", 14), new WrapPanel { Children = { eh, em, et, Act("mk-e-add", _edExp is null ? "Test anlegen" : "Test speichern", () =>
        {
            if (string.IsNullOrWhiteSpace(eh.Text)) { BizNeed("biz-markt", "mk-e-hyp", "Bitte die Annahme für den Test eintragen."); return; }
            if (_edExp is int ei && ei >= 0 && ei < Experiments.All().Count)
            {
                var l = Experiments.All(); l[ei] = l[ei] with { Hypothesis = eh.Text!.Trim(), Metric = em.Text ?? "", Target = ParseD(et.Text) }; Experiments.Save(l); _edExp = null;
                BizOk("biz-markt", $"Test „{eh.Text.Trim()}“ geändert.");
            }
            else { Experiments.Add(new(eh.Text!.Trim(), em.Text ?? "", ParseD(et.Text))); BizOk("biz-markt", $"Test „{eh.Text.Trim()}“ angelegt."); }
        }) } } } };
        if (ex.Count == 0) { var sug = Act("mk-e-suggest", "Vorschläge übernehmen", () => { foreach (var s in BusinessMarket.ExperimentIdeas) Experiments.Add(s); Show("biz-markt"); }); esec.Children.Add(sug); }
        for (var k = 0; k < ex.Count; k++)
        {
            var idx = k; var e = ex[k]; var res = new TextBox { Name = $"mk-e-res-{k}", Text = e.Result?.ToString("0.##") ?? "", PlaceholderText = "Ergebnis", Width = 80 };
            esec.Children.Add(Row(P($"{e.Hypothesis} · Ziel {e.Target:0.##} {e.Metric} · {e.State}", Text, 11), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { res,
                Act($"mk-e-save-{k}", "Eintragen", () => { var l = Experiments.All(); l[idx] = l[idx] with { Result = string.IsNullOrWhiteSpace(res.Text) ? null : ParseD(res.Text) }; Experiments.Save(l); BizOk("biz-markt", $"Ergebnis für „{e.Hypothesis}“ eingetragen."); }),
                Act($"mk-e-edit-{k}", "Ändern", () => { _edExp = idx; BizOk("biz-markt", $"Test „{e.Hypothesis}“ steht in den Feldern."); }),
                Act($"mk-e-del-{k}", "Löschen", () => { Experiments.RemoveAt(idx); if (_edExp == idx) _edExp = null; BizOk("biz-markt", $"Test „{e.Hypothesis}“ gelöscht."); }) } }));
        }
        var val = BusinessMarket.Validation(ex);
        esec.Children.Add(new TextBlock { Name = "mk-validation", Text = $"{val.Confirmed} von {val.Done} Tests bestätigt. {val.Verdict}", Foreground = Text, FontWeight = FontWeight.SemiBold });
        esec.Children.Add(Nav2("Angebotsseite zum Testen bauen", "produktion"));
        sp.Children.Add(Card(esec));
        return BusinessPage("Markt", sp);
    }

    private static double ParseD(string? s) { try { return (double)Finance.ParseAmount(s ?? ""); } catch (FormatException) { return 0; } }
}
