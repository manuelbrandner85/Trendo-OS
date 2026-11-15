using System.Globalization;
using System.Text;
using Trendo.Core.Kreativ;

namespace Trendo.Core.PromptAction;

/// <summary>STUDIO-ART-01 Künstlerstudio: Malvorlage planen – Format, Spiegeln, Palette (Harmonie) und Malschritte. Die KI malt kein fertiges Bild;
/// gemalt wird mit Stift oder Maus im Studio.</summary>
public sealed class KunstVorlageAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-ART-01";
    public string ActionId => "kunst.vorlage";
    public string Label => "Malvorlage anlegen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KuenstlerTests.Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["motiv"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    static readonly (string Id, string Name, int W, int H)[] Formats = [("a4", "Skizzenblatt A4", 1240, 1754), ("quer", "Querformat 16:9", 1920, 1080), ("quadrat", "Quadrat", 1600, 1600)];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "motiv") ? [] : [new("motiv", "motiv", "Was möchtest du malen? Schreib z. B. „Motiv: Fuchs im Schnee“.", true)];

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "farbe") && !PaintColors.IsHex(FieldKit.Text(parameters, "farbe"))) f.Add(new("farbe", "Grundfarbe als Hex-Wert, z. B. #2F86F6.", true));
        if (FieldKit.Has(parameters, "format") && !Formats.Any(x => x.Id == FieldKit.Text(parameters, "format").ToLowerInvariant())) f.Add(new("format", "Format: a4, quer oder quadrat.", true));
        if (FieldKit.Has(parameters, "harmonie") && !PaintColors.Harmonies.Any(h => h.Id == FieldKit.Text(parameters, "harmonie").ToLowerInvariant())) f.Add(new("harmonie", "Harmonie: komplementaer, analog, triade, teiltriade oder quadrat.", true));
        if (FieldKit.Has(parameters, "teile") && (!int.TryParse(FieldKit.Text(parameters, "teile"), out var n) || n is < 2 or > 32)) f.Add(new("teile", "Teile für das Mandala: 2 bis 32.", true));
        return f;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters; var motiv = FieldKit.Text(p, "motiv");
        var fmt = Formats.FirstOrDefault(x => x.Id == FieldKit.Text(p, "format").ToLowerInvariant()); if (fmt.Name is null) fmt = Formats[0];
        var color = FieldKit.Has(p, "farbe") ? FieldKit.Text(p, "farbe") : "#2F86F6";
        var harm = FieldKit.Has(p, "harmonie") ? FieldKit.Text(p, "harmonie").ToLowerInvariant() : "analog";
        var palette = PaintColors.Harmony(color, harm);
        var parts = int.TryParse(FieldKit.Text(p, "teile"), out var n) ? n : 0;
        var sb = new StringBuilder();
        sb.Append("Malvorlage: ").Append(motiv).Append('\n');
        sb.Append(CultureInfo.InvariantCulture, $"Leinwand: {fmt.Name} ({fmt.W} × {fmt.H} px)\n");
        sb.Append("Spiegeln: ").Append(parts > 0 ? $"radial, {parts} Teile" : "aus").Append('\n');
        sb.Append("Palette (").Append(PaintColors.Harmonies.First(h => h.Id == harm).Name).Append("): ").Append(string.Join(", ", palette)).Append('\n');
        sb.Append("\nEbenen und Schritte:\n");
        string[] steps = ["Skizze – mit dem Bleistift die großen Formen anlegen, locker und hell.", "Linien – „Skizze säubern“ oder mit Tusche nachziehen.",
            "Farbflächen – „Vorfärben“, dann die Grundfarben aus der Palette setzen.", "Schatten – eigene Ebene auf „Multiplizieren“, kühler, dunkler Ton.",
            "Licht – eigene Ebene auf „Negativ multiplizieren“ oder „Farbig abwedeln“, sparsam.", "Details – zum Schluss, auf eigener Ebene."];
        for (var i = 0; i < steps.Length; i++) sb.Append(CultureInfo.InvariantCulture, $"{i + 1}. {steps[i]}\n");
        sb.Append("\nGIMP-Palette (.gpl) zum Laden im Studio:\n").Append(Palettes.ToGpl(motiv, palette));
        var qa = new QaResult(ActionId, palette.Count >= 2, $"{palette.Count} Farben, {steps.Length} Schritte.", null, fmt.Id, "PaintColors");
        return FieldKit.Save(call, FieldKit.Slot("malvorlage", call.Version, "malvorlage.txt"), sb.ToString(), $"{fmt.Name}, {palette.Count} Farben", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Gemalt wird im Studio mit Stift oder Maus – die KI malt kein fertiges Bild.");
}
