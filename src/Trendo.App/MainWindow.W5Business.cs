using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>STUDIO-ONLINE-BUSINESS-01: Geschäftsideen prüfen.</summary>
public partial class MainWindow
{
    public IdeaStore Ideas => new(_core.DataRoot);
    public BusinessIdea? CurrentIdea;
    public string BusinessFolder => WorkFolder("Business");

    private void InitW5Business()
    {
        RegisterTab(BusinessTabs, "verkaufen", "Verkaufen", "STUDIO-ONLINE-BUSINESS-01", SellView, "verkaufen", "umsatz", "bestellung", "webshop", "shop", "artikel", "kunden");
        RegisterTab(BusinessTabs, "business", "Geschäftsidee", "STUDIO-ONLINE-BUSINESS-01", BusinessView, "business", "geschaeft", "idee", "gruenden", "selbststaendig", "bauplan");
    }

    private Control BusinessView()
    {
        var all = Ideas.All();
        CurrentIdea ??= all.FirstOrDefault() ?? new BusinessIdea { Name = "Meine Idee" };
        var i = CurrentIdea;
        var sp = new StackPanel { Spacing = 8, Children = { P("Prüfe deine Geschäftsidee ehrlich, bevor du Geld ausgibst. Alles bleibt auf diesem PC.") } };
        var list = new WrapPanel();
        var n = 0;
        foreach (var x in all) { var idea = x; var b = Act($"biz-idea-{n++}", (idea.Id == i.Id ? "● " : "") + idea.Name, () => { CurrentIdea = idea; Show("business"); }); b.Margin = new Avalonia.Thickness(0, 0, 6, 6); list.Children.Add(b); }
        list.Children.Add(Act("biz-new", "+ Neue Idee", () => { CurrentIdea = new BusinessIdea { Name = "Neue Idee" }; Show("business"); }));
        sp.Children.Add(list);

        TextBox T(string name, string val, string ph, bool multi = false) => new() { Name = name, Text = val, PlaceholderText = ph, AcceptsReturn = multi, TextWrapping = TextWrapping.Wrap, MinWidth = 380 };
        NumericUpDown N(string name, double val, double max = 1_000_000) => new() { Name = name, Value = (decimal)val, Minimum = 0, Maximum = (decimal)max, Increment = 1, Width = 150, FormatString = "0.##" };
        var name = T("biz-name", i.Name, "Name der Idee"); var prob = T("biz-problem", i.Problem, "Welches Problem löst du?", true);
        var aud = T("biz-audience", i.Audience, "Für wen? z. B. Hundebesitzer in Städten"); var sol = T("biz-solution", i.Solution, "Wie löst du es?", true);
        var model = new ComboBox { Name = "biz-model", ItemsSource = BusinessStudio.Models.Select(m => m.Name).ToList(), SelectedIndex = Math.Max(0, BusinessStudio.Models.ToList().FindIndex(m => m.Id == i.ModelId)), Width = 260 };
        var price = N("biz-price", i.Price); var vc = N("biz-varcost", i.VariableCost); var fc = N("biz-fixcost", i.FixedCostMonth);
        var cust = N("biz-customers", i.CustomersMonth); var comp = N("biz-competitors", i.Competitors, 1000); var skill = N("biz-skill", i.Skill, 5); skill.Minimum = 1;
        var hours = N("biz-hours", i.HoursWeek, 80); var cap = N("biz-capital", i.Capital);
        var cm = BusinessStudio.Model(i.ModelId);
        sp.Children.Add(Card(new StackPanel { Spacing = 5, Children = { H("1. Idee", 14), name, prob, aud, sol,
            new WrapPanel { Children = { P("Geschäftsmodell  "), model } }, P($"{cm.How} Plus: {cm.Plus}. Minus: {cm.Minus}.", null, 10.5) } }));
        Control L(string label, Control c) => new StackPanel { Margin = new Avalonia.Thickness(0, 0, 12, 6), Children = { P(label, null, 10.5), c } };
        sp.Children.Add(Card(new StackPanel { Spacing = 5, Children = { H("2. Zahlen (deine Schätzung)", 14), new WrapPanel { Children = {
            L("Preis in €", price), L("Kosten je Verkauf €", vc), L("Feste Kosten pro Monat €", fc), L("Verkäufe pro Monat", cust),
            L("Mitbewerber", comp), L("Können 1–5", skill), L("Stunden pro Woche", hours), L("Startgeld €", cap) } } } }));

        void Read()
        {
            double D(NumericUpDown x) => (double)(x.Value ?? 0);
            i.Name = string.IsNullOrWhiteSpace(name.Text) ? "Idee" : name.Text!; i.Problem = prob.Text ?? ""; i.Audience = aud.Text ?? ""; i.Solution = sol.Text ?? "";
            i.ModelId = BusinessStudio.Models[Math.Max(0, model.SelectedIndex)].Id; i.Price = D(price); i.VariableCost = D(vc); i.FixedCostMonth = D(fc);
            i.CustomersMonth = D(cust); i.Competitors = (int)D(comp); i.Skill = (int)D(skill); i.HoursWeek = D(hours); i.Capital = D(cap);
        }
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = {
            Act("biz-evaluate", "Speichern und bewerten", () => { Read(); Ideas.Save(i); _core.Audit.Append(Principal.LocalOwner.Name, "business.evaluate", i.Id); Show("business"); }, true),
            Act("biz-blueprint", "Bauplan erstellen", () => { Read(); Ideas.Save(i); var f = Path.Combine(BusinessFolder, DevStudio.SafeName(i.Name) + "-Bauplan.md"); File.WriteAllText(f, BusinessStudio.Blueprint(i)); Launch(f); Say("Bauplan gespeichert: " + Path.GetFileName(f), "business"); }),
            Act("biz-delete", "Löschen", () => { Ideas.Delete(i.Id); CurrentIdea = null; Show("business"); }) } });

        var v = BusinessStudio.Evaluate(i);
        var col = v.Decision switch { "Starten" => "#22C55E", "Erst testen" => "#FACC15", _ => "#F97316" };
        // U2: Score als Ring mit Ampel
        var lamp = new StackPanel { Name = "biz-lamp", Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (d, c) in new[] { ("Nicht starten", "#F0644B"), ("Erst testen", "#F5C524"), ("Starten", "#2FBF71") })
            lamp.Children.Add(new Avalonia.Controls.Shapes.Ellipse { Width = 16, Height = 16, Fill = Brush.Parse(v.Decision == d ? c : "#33808080"), Stroke = Brush.Parse("#33FFFFFF"), StrokeThickness = 1 });
        var ring = new Grid { Width = 92, Height = 92, Children = {
            new Avalonia.Controls.Shapes.Ellipse { Width = 92, Height = 92, Stroke = Edge, StrokeThickness = 9 },
            new Avalonia.Controls.Shapes.Arc { Name = "biz-ring", Width = 92, Height = 92, StrokeThickness = 9, StartAngle = -90, SweepAngle = 3.6 * v.Score, StrokeLineCap = PenLineCap.Round, Stroke = Brush.Parse(col) },
            new TextBlock { Text = v.Score.ToString(), FontSize = 26, FontWeight = FontWeight.Bold, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } } };
        var res = new StackPanel { Spacing = 3, Children = {
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Margin = new Avalonia.Thickness(0, 4, 0, 6), Children = { ring, lamp,
                new TextBlock { Name = "biz-verdict", Text = $"{v.Score} von 100 · {v.Decision}", FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse(col), VerticalAlignment = VerticalAlignment.Center } } } } };
        var be = BusinessStudio.BreakEven(i);
        res.Children.Add(P($"Gewinn pro Monat: {BusinessStudio.ProfitMonth(i):N0} € · Gewinnschwelle: {(be is null ? "nicht erreichbar" : be + " Verkäufe")} · Stundenlohn: {BusinessStudio.HourlyWage(i):N2} €", Text));
        foreach (var b in v.Blockers) res.Children.Add(P("⚠ " + b, Brush.Parse("#F97316")));
        foreach (var p in v.Parts) res.Children.Add(P($"{p.Label}: {p.Points}/{p.Max} – {p.Why}", null, 11));
        res.Children.Add(P("Das ist eine Schätzung aus deinen Zahlen, keine Garantie.", null, 10));
        sp.Children.Add(Card(new StackPanel { Children = { H("3. Ergebnis", 14), res } }));

        // U3: Schritte der Gründung als Fortschrittsleiste
        var fs = new FoundingSteps(_core.DataRoot); var done = fs.Done(i.Id);
        var steps = new WrapPanel { Name = "biz-steps" };
        for (var k = 0; k < FoundingSteps.Steps.Length; k++)
        {
            var step = k; var on = done.Contains(k);
            var b = new Button { Name = $"biz-step-{k}", Content = (on ? "✓ " : $"{k + 1}. ") + FoundingSteps.Steps[k], FontSize = 12, Padding = new Avalonia.Thickness(12, 5), Margin = new Avalonia.Thickness(0, 0, 6, 6), CornerRadius = new Avalonia.CornerRadius(14),
                Background = on ? Brush.Parse("#2FBF71") : Brushes.Transparent, Foreground = on ? Brushes.White : Text, BorderBrush = Edge, BorderThickness = new Avalonia.Thickness(on ? 0 : 1) };
            b.Click += (_, _) => { fs.Toggle(i.Id, step); Show("business"); };
            steps.Children.Add(b);
        }
        sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { Row(H("4. Gründung Schritt für Schritt", 14), P($"{fs.Percent(i.Id)} % geschafft", Text, 12)),
            new ProgressBar { Name = "biz-steps-progress", Value = fs.Percent(i.Id), Maximum = 100, Height = 8 }, steps } }));
        sp.Children.Add(SmallBizCheck("biz-ku"));
        return BusinessPage("Business-Studio", sp);
    }
}
