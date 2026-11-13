using System.Globalization;
using Trendo.Core.Kreativ;
using Trendo.Core.Sys;

namespace Trendo.Core.PromptAction;

/// <summary>STUDIO-BOOK-01: Manuskript-Gerüst mit Kapiteln. Schreibt nur ins Projekt.</summary>
public sealed class BookOutlineAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-BOOK-01";
    public string ActionId => "buch.gliederung";
    public string Label => "Buch gliedern";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKits2Tests.Buch";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["titel"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "titel") ? [] : [new("titel", "titel", "Wie soll das Buch heißen? Schreib „Titel: …“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "kapitel") && !(int.TryParse(FieldKit.Text(parameters, "kapitel"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 60)
            ? [new("kapitel", "Gib zwischen 1 und 60 Kapitel an.", true)] : [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var title = FieldKit.Text(call.Parameters, "titel");
        var n = int.TryParse(FieldKit.Text(call.Parameters, "kapitel"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var k) ? k : 10;
        var idea = FieldKit.Text(call.Parameters, "idee", "thema");
        var body = $"{(idea.Length > 0 ? idea + "\n\n" : "")}" + string.Join("\n\n", Enumerable.Range(1, n).Select(i => $"# Kapitel {i}\n\nWorum geht es in diesem Kapitel?"));
        var qa = new QaResult(ActionId, BookKit.Chapters(body).Count >= n, $"{n} Kapitel angelegt.", null, title, "BookKit");
        return FieldKit.Save(call, FieldKit.Slot("buch", call.Version, "manuskript.md"), body, title, qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Veröffentlichen machst du selbst bei deinem Verlag oder Shop.");
}

/// <summary>STUDIO-ANIM-01: Kurze Text-Animation als animiertes SVG.</summary>
public sealed class AnimTitleAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-ANIM-01";
    public string ActionId => "animation.titel";
    public string Label => "Titel animieren";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKits2Tests.Animation";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["text"];
    public IReadOnlyList<string> OutputKinds => ["svg"];
    public static readonly IReadOnlyList<string> Moves = ["einblenden", "einfliegen", "pulsieren", "drehen"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "text") ? [] : [new("text", "text", "Welcher Text soll sich bewegen? Schreib „Text: …“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "bewegung") && !Moves.Contains(FieldKit.Text(parameters, "bewegung").ToLowerInvariant())
            ? [new("bewegung", "Bewegung: einblenden, einfliegen, pulsieren oder drehen.", true)] : [];
    public static AnimKit.Layer Title(string text, string move, string color)
    {
        var l = new AnimKit.Layer("titel", "text", text, LogoKit.IsHex(color) ? color : "#F5C542", 0, 96, []);
        switch (move)
        {
            case "einfliegen": AnimKit.SetKey(l, "x", 0, -400, "aus"); AnimKit.SetKey(l, "x", 1.2, AnimKit.StageW / 2); break;
            case "pulsieren": AnimKit.SetKey(l, "skalierung", 0, 1); AnimKit.SetKey(l, "skalierung", 0.6, 1.2); AnimKit.SetKey(l, "skalierung", 1.2, 1); break;
            case "drehen": AnimKit.SetKey(l, "drehung", 0, -180, "aus"); AnimKit.SetKey(l, "drehung", 1, 0); AnimKit.SetKey(l, "skalierung", 0, 0.2, "aus"); AnimKit.SetKey(l, "skalierung", 1, 1); break;
            default: AnimKit.SetKey(l, "deckkraft", 0, 0); AnimKit.SetKey(l, "deckkraft", 1, 1); break;
        }
        return l;
    }
    public AdapterOutcome Produce(AdapterCall call)
    {
        var text = FieldKit.Text(call.Parameters, "text");
        var move = FieldKit.Has(call.Parameters, "bewegung") ? FieldKit.Text(call.Parameters, "bewegung").ToLowerInvariant() : "einblenden";
        var svg = AnimKit.Svg([Title(text, move, FieldKit.Text(call.Parameters, "farbe"))], "#101827");
        var qa = new QaResult(ActionId, true, $"Bewegung „{move}“.", null, text, "AnimKit");
        return FieldKit.Save(call, FieldKit.Slot("animation", call.Version, "titel.svg"), svg, text, qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts veröffentlicht.");
}

/// <summary>STUDIO-PHOTOS-01: Nur der Plan zum Einsortieren. Verschoben oder kopiert wird im Studio nach Bestätigung.</summary>
public sealed class PhotoPlanAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-PHOTOS-01";
    public string ActionId => "fotos.plan";
    public string Label => "Fotos ordnen (Probelauf)";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKits2Tests.Fotos";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["ordner", "ziel"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var q = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "ordner")) q.Add(new("ordner", "ordner", "Wo liegen die Fotos? Schreib „Ordner: C:\\…“.", true));
        if (!FieldKit.Has(parameters, "ziel")) q.Add(new("ziel", "ziel", "Wohin sollen sie einsortiert werden? Schreib „Ziel: C:\\…“.", true));
        return q;
    }
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "ordner") && !(KreativSafe.LocalDir(FieldKit.Text(parameters, "ordner")))) f.Add(new("ordner", "Den Fotoordner gibt es auf diesem Rechner nicht.", true));
        if (FieldKit.Has(parameters, "ziel") && !Path.IsPathFullyQualified(FieldKit.Text(parameters, "ziel"))) f.Add(new("ziel", "Gib den Zielordner vollständig an, z. B. C:\\Fotos.", true));
        return f;
    }
    public AdapterOutcome Produce(AdapterCall call)
    {
        var photos = PhotoAlbum.Scan([FieldKit.Text(call.Parameters, "ordner")]);
        var plan = PhotoOrganizer.Plan(photos, FieldKit.Text(call.Parameters, "ziel"), "jahr-monat", rename: false);
        var body = $"Probelauf: {plan.Count} von {photos.Count} Fotos würden einsortiert (Jahr / Monat). Es wurde nichts verschoben.\n\n" +
                   string.Join("\n", plan.Take(500).Select(m => $"{Path.GetFileName(m.From)} → {m.To}"));
        var qa = new QaResult(ActionId, true, $"{plan.Count} Fotos im Plan.", null, FieldKit.Text(call.Parameters, "ordner"), "PhotoOrganizer");
        return FieldKit.Save(call, FieldKit.Slot("fotos", call.Version, "probelauf.txt"), body, $"{plan.Count} Fotos", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Einsortieren startest du im Studio „Fotos verwalten“ nach dem Probelauf.");
}

/// <summary>STUDIO-IMAGEAI-01: Bildauftrag mit Stil, Größe und Startwerten. Gemalt wird im Studio mit der lokalen Bild-KI.</summary>
public sealed class ImageAiPlanAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-IMAGEAI-01";
    public string ActionId => "bild.auftrag";
    public string Label => "Bildauftrag vorbereiten";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKits2Tests.BildKi";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["bild"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "bild") ? [] : [new("bild", "bild", "Was soll auf dem Bild sein? Schreib „Bild: …“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "bild") && ImageAiKit.Blocked(FieldKit.Text(parameters, "bild")) is { } why ? [new("bild", why, true)] : [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var prompt = FieldKit.Text(call.Parameters, "bild");
        var style = FieldKit.Has(call.Parameters, "stil") ? FieldKit.Text(call.Parameters, "stil").ToLowerInvariant() : "foto";
        var format = FieldKit.Has(call.Parameters, "format") ? FieldKit.Text(call.Parameters, "format").ToLowerInvariant() : "quadrat";
        var (w, h) = ImageAiKit.Size(format, 1024);
        var body = $"Bildauftrag\n\nBeschreibung: {prompt}\nStil: {style}\nFormat: {w} × {h}\nVollständig: {ImageAiKit.FullPrompt(prompt, style)}\nNicht im Bild: {ImageAiKit.Negative}\nStartwerte: {string.Join(", ", Enumerable.Range(0, 4).Select(i => ImageAiKit.Seed(42, i)))}\n\nGemalt wird im Studio „Bilder mit KI“ mit der lokalen Bild-KI. Jedes Bild bekommt einen Herkunftsnachweis.\n";
        var qa = new QaResult(ActionId, true, "Auftrag vorbereitet.", null, prompt, "ImageAiKit");
        return FieldKit.Save(call, FieldKit.Slot("bild-ki", call.Version, "bildauftrag.txt"), body, prompt, qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Gemalt wird im Studio mit der lokalen Bild-KI.");
}
