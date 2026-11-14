using Trendo.Core.Kreativ;

namespace Trendo.Core.PromptAction;

/// <summary>STUDIO-VIDEOAI-01: Szenenplan aus einer Beschreibung. Bilder und Video entstehen im Studio mit der lokalen Bild-KI und FFmpeg.</summary>
public sealed class VideoAiPlanAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-VIDEOAI-01";
    public string ActionId => "video-ki.szenenplan";
    public string Label => "Szenenplan für ein KI-Video";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKits3Tests.VideoKi";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["szenen"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "szenen") ? [] : [new("szenen", "szenen", "Was soll im Video zu sehen sein? Schreib „Szenen:“ und darunter je Zeile eine Szene.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (!FieldKit.Has(parameters, "szenen")) return [];
        return VideoAiKit.Check(VideoAiKit.Plan(FieldKit.Text(parameters, "szenen")), needImages: false).Where(x => x.StartsWith('✕')).Select(x => new ValidationFinding("szenen", x[1..].Trim(), true)).ToList();
    }
    public AdapterOutcome Produce(AdapterCall call)
    {
        var scenes = VideoAiKit.Plan(FieldKit.Text(call.Parameters, "szenen"));
        var title = FieldKit.Has(call.Parameters, "titel") ? FieldKit.Text(call.Parameters, "titel") : "Mein Video";
        var body = VideoAiKit.PlanText(scenes, title) + "\nDie Standbilder malt das Studio „Videos mit KI“ mit der lokalen Bild-KI, das Video baut FFmpeg. Jedes Bild und das Video bekommen einen Herkunftsnachweis.\n";
        var qa = new QaResult(ActionId, scenes.Count > 0, $"{scenes.Count} Szenen geplant.", null, title, "VideoAiKit");
        return FieldKit.Save(call, FieldKit.Slot("video-ki", call.Version, "szenenplan.md"), body, title, qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Das Video entsteht im Studio „Videos mit KI“. Es wird nichts veröffentlicht.");
}

/// <summary>STUDIO-AGENTS-01: Entwurf eines eigenen Helfers als Claude-Code-Subagent (Markdown). Nur lesende Werkzeuge, solange nichts anderes gewünscht ist.</summary>
public sealed class AgentDraftAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-AGENTS-01";
    public string ActionId => "agent.entwurf";
    public string Label => "Eigenen KI-Helfer entwerfen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKits3Tests.Agenten";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["name", "aufgabe"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var q = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "name")) q.Add(new("name", "name", "Wie soll der Helfer heißen? Schreib „Name: …“.", true));
        if (!FieldKit.Has(parameters, "aufgabe")) q.Add(new("aufgabe", "aufgabe", "Wofür ist der Helfer da? Schreib „Aufgabe: …“.", true));
        return q;
    }
    public static AgentBlueprint From(IReadOnlyDictionary<string, string> p)
    {
        var tools = FieldKit.Has(p, "werkzeuge")
            ? FieldKit.Text(p, "werkzeuge").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(t => t.ToLowerInvariant()).Distinct().ToList()
            : ["lesen", "suchen"];
        var task = FieldKit.Text(p, "aufgabe");
        var instructions = FieldKit.Has(p, "anweisungen") ? FieldKit.Text(p, "anweisungen")
            : $"Du bist ein Helfer mit dieser Aufgabe: {task.TrimEnd('.')}. Arbeite sorgfältig, frag nach, wenn etwas unklar ist, und erkläre kurz, was du getan hast.";
        return new AgentBlueprint(AgentBlueprint.Slug(FieldKit.Text(p, "name")), task, instructions, tools, FieldKit.Text(p, "beispiele"),
            FieldKit.Has(p, "grenzen") ? FieldKit.Text(p, "grenzen") : "Nichts löschen, nichts senden, nichts kaufen. Keine Passwörter oder Schlüssel.");
    }
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        !FieldKit.Has(parameters, "name") || !FieldKit.Has(parameters, "aufgabe") ? [] :
        From(parameters).Check().Where(f => f.Error).Select(f => new ValidationFinding(f.Field, f.Message, true)).ToList();
    public AdapterOutcome Produce(AdapterCall call)
    {
        var a = From(call.Parameters);
        var qa = new QaResult(ActionId, a.Ok, $"Helfer „{a.Name}“ mit {a.Tools.Count} Werkzeugen.", null, a.Name, "AgentBlueprint");
        return FieldKit.Save(call, FieldKit.Slot("agenten", call.Version, a.Name + ".md"), a.ToClaudeMarkdown(), a.Name, qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("In einen Projektordner speicherst du den Helfer im Studio „KI-Agenten bauen“ nach deiner Ordnerwahl.");
}

/// <summary>STUDIO-MODELS-01: Fragebogen für einen Blindvergleich. Gefragt und bewertet wird im Studio mit den eingerichteten KI-Quellen.</summary>
public sealed class ModelQuestionsAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-MODELS-01";
    public string ActionId => "modelle.fragebogen";
    public string Label => "Vergleichsfragen vorbereiten";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKits3Tests.Modelle";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["fragen"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "fragen") ? [] : [new("fragen", "fragen", "Welche Fragen sollen die KIs beantworten? Schreib „Fragen:“ und darunter je Zeile eine Frage.", true)];
    public static List<string> Questions(string raw) => raw.Replace("\r", "").Split('\n', ';').Select(q => q.Trim().TrimStart('-', '*', '•').Trim()).Where(q => q.Length > 0).Take(50).ToList();
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "fragen") && Questions(FieldKit.Text(parameters, "fragen")).Count == 0 ? [new("fragen", "Schreib mindestens eine Frage.", true)] : [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var qs = Questions(FieldKit.Text(call.Parameters, "fragen"));
        var body = "# Fragebogen für den Blindvergleich\n\nJede Frage geht an alle gewählten KI-Quellen. Die Antworten erscheinen gemischt als A, B, C …; erst nach deinen Sternen wird aufgedeckt, welche Antwort von wem kommt.\n\n"
            + string.Join("\n", qs.Select((q, i) => $"{i + 1}. {q}")) + "\n\nBewertung: 1 bis 5 Sterne je Antwort. Gemessen werden Dauer, Länge und Wörter je Sekunde.\n";
        var qa = new QaResult(ActionId, qs.Count > 0, $"{qs.Count} Fragen.", null, qs.FirstOrDefault() ?? "", "ModelCompare");
        return FieldKit.Save(call, FieldKit.Slot("modelle", call.Version, "fragebogen.md"), body, $"{qs.Count} Fragen", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Gefragt wird im Studio „KI-Modelle vergleichen“ mit deinen eingerichteten KI-Quellen.");
}

/// <summary>STUDIO-TRAIN-01: Trainiert aus Beispielen „Text = Kategorie“ einen echten Textklassifikator und legt Modell und Auswertung im Projekt ab.</summary>
public sealed class TrainClassifierAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-TRAIN-01";
    public string ActionId => "ki.trainieren";
    public string Label => "Textklassifikator trainieren";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKits3Tests.Training";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["beispiele"];
    public IReadOnlyList<string> OutputKinds => ["json"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "beispiele") ? [] : [new("beispiele", "beispiele", "Schreib „Beispiele:“ und darunter je Zeile „Text = Kategorie“.", true)];
    /// <summary>„Text = Kategorie“ oder „Text ; Kategorie“ je Zeile; die Kategorie steht hinter dem letzten Trennzeichen.</summary>
    public static List<TextClassifier.Example> Examples(string raw) =>
        raw.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Select(l =>
        {
            var i = Math.Max(l.LastIndexOf('='), l.LastIndexOf(';'));
            return i <= 0 ? new TextClassifier.Example(l, "") : new TextClassifier.Example(l[..i].Trim(), l[(i + 1)..].Trim());
        }).ToList();
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (!FieldKit.Has(parameters, "beispiele")) return [];
        var ex = Examples(FieldKit.Text(parameters, "beispiele"));
        return ex.Where(e => e.Label.Length > 0).Select(e => e.Label).Distinct().Count() < 2 ? [new("beispiele", "Es braucht Beispiele aus mindestens zwei Kategorien.", true)] : [];
    }
    public AdapterOutcome Produce(AdapterCall call)
    {
        var ex = Examples(FieldKit.Text(call.Parameters, "beispiele"));
        var model = TextClassifier.Train(ex.Where(e => e.Label.Length > 0));
        var warnings = TextClassifier.Warnings(ex);
        var qa = new QaResult(ActionId, true, $"{model.Documents} Beispiele, {model.Labels.Count} Kategorien, {model.VocabularySize} Wörter.", null, string.Join(", ", model.Labels), "TextClassifier");
        return FieldKit.Save(call, FieldKit.Slot("ki-training", call.Version, "modell.json"), model.ToJson(), string.Join(", ", model.Labels), qa, warnings.Count > 0 ? warnings : null);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Das Modell bleibt auf deinem Rechner. Es wird nichts hochgeladen.");
}
