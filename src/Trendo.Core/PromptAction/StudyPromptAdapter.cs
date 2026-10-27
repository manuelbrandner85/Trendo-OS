namespace Trendo.Core.PromptAction;

/// <summary>CORE-10: Eintrag ins eigene Wissen. Nichts wird hochgeladen.</summary>
public sealed class KnowledgeRememberAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-10";
    public string ActionId => "wissen.merken";
    public string Label => "Wissen merken";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Wissen_wird_gemerkt_und_gelesen";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["text"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "text") ? [] : [new("text", "text", "Text fehlt. Schreib „Text: …“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Text(parameters, "text").Length > 8000 ? [new("text", "Der Text ist zu lang.", true)] : [];
    public AdapterOutcome Produce(AdapterCall call) => KnowledgeFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts hochgeladen.");
}

/// <summary>SPEC-06: eine Karteikarte über LearnBox.</summary>
public sealed class LearnCardAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-06";
    public string ActionId => "karte.anlegen";
    public string Label => "Karte anlegen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Karte_wird_angelegt_und_exportiert";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["frage", "antwort"];
    public IReadOnlyList<string> OutputKinds => ["csv"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "frage")) list.Add(new("frage", "frage", "Frage fehlt.", true));
        if (!FieldKit.Has(parameters, "antwort")) list.Add(new("antwort", "antwort", "Antwort fehlt.", true));
        return list;
    }
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) => LearnFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es gibt keinen Kurs.");
}

/// <summary>CORE-12: Vorlage füllen und prüfen. Die KI wird nicht gefragt.</summary>
public sealed class PromptFillAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-12";
    public string ActionId => "anweisung.fuellen";
    public string Label => "Anweisung füllen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Anweisung_wird_gefuellt";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["text"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "text") || (FieldKit.Has(parameters, "rolle") && FieldKit.Has(parameters, "ziel"))
            ? []
            : [new("text", "text", "Text mit {{Platzhalter}} fehlt, oder schreib Rolle und Ziel.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => PromptFillFields.Validate(parameters);
    public AdapterOutcome Produce(AdapterCall call) => PromptFillFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Die KI wurde nicht gefragt.");
}

/// <summary>SPEC-08: Größe aus der eingebauten Tabelle.</summary>
public sealed class FashionSizeAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-08";
    public string ActionId => "groesse.pruefen";
    public string Label => "Größe prüfen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Groesse_kommt_aus_der_Tabelle";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["groesse"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "groesse") || (FieldKit.Has(parameters, "brust") && FieldKit.Has(parameters, "taille"))
            ? []
            : [new("groesse", "groesse", "EU-Größe fehlt, oder schreib Brust und Taille in Zentimetern.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => FashionFields.Validate(parameters);
    public AdapterOutcome Produce(AdapterCall call) => FashionFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts bestellt.");
}

/// <summary>STUDIO-TV-01: Senderliste (M3U) prüfen und ordnen – Sender, Filme, Serien je Gruppe. Abspielen und Aufnehmen macht das Studio selbst.</summary>
public sealed class TvListAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-TV-01";
    public string ActionId => "tv.liste.pruefen";
    public string Label => "Senderliste prüfen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "IpTvTests.Parse_m3u";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["liste"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new ActionDraft(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "liste") ? [] : [new("liste", "liste", "Senderliste fehlt. Füge den Inhalt der M3U-Datei als „Liste: …“ ein – oder öffne das Studio und füge sie unter „Quellen“ hinzu.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        Trendo.Core.Tv.M3u.Parse(FieldKit.Text(parameters, "liste")).Channels.Count == 0 ? [new("liste", "In der Liste steht kein Sender (#EXTINF und Adresse).", true)] : [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var (ch, epg) = Trendo.Core.Tv.M3u.Parse(FieldKit.Text(call.Parameters, "liste"));
        var sb = new System.Text.StringBuilder($"Senderliste: {ch.Count(c => c.Kind == "live")} Sender, {ch.Count(c => c.Kind == "film")} Filme, {ch.Count(c => c.Kind == "serie")} Serienfolgen" + Environment.NewLine);
        if (epg is not null) sb.Append("Programmführer: ").AppendLine(epg);
        foreach (var g in ch.GroupBy(c => (c.Kind, c.Group)).OrderBy(g => g.Key.Kind).ThenBy(g => g.Key.Group))
        {
            sb.AppendLine().AppendLine($"[{(g.Key.Kind == "live" ? "Sender" : g.Key.Kind == "film" ? "Filme" : "Serien")}] {g.Key.Group} ({g.Count()})");
            foreach (var c in g.Take(50)) sb.AppendLine("  " + c.Name);
        }
        var qa = new QaResult("tv.liste", ch.Count > 0, "Liste gelesen.", null, $"{ch.Count} Einträge", "M3u");
        return FieldKit.Save(call, FieldKit.Slot("tv", call.Version, "senderliste.txt"), sb.ToString(), $"{ch.Count} Einträge", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Abspielen und Aufnehmen gehen im Studio „TV & Live“.");
}

/// <summary>SPEC-15: Senden und Stream bleiben aus. LiveBroadcast.Go wird nicht gerufen.</summary>
public sealed class StreamSendAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-15";
    public string ActionId => "sendung.senden";
    public string Label => "Sendung starten";
    public SideEffectClass Effect => SideEffectClass.Publish;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Blocked;
    public string Evidence => "PromptActionTests.Sendung_sendet_nichts";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => [];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return BroadcastFields.WantsLive(fields) ? new ActionDraft(ActionId, fields) : null;
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) => FieldKit.Blocked("Der Stream ist nicht angeschlossen. Es wurde nichts gesendet.");
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Der Stream ist nicht angeschlossen. Es wurde nichts gesendet.");
}

/// <summary>SPEC-15: Sendeablauf mit BroadcastDesk. Nichts geht auf Sendung.</summary>
public sealed class RundownAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-15";
    public string ActionId => "sendung.planen";
    public string Label => "Sendeablauf";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Sendung_schreibt_ablauf";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["titel", "punkt"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return BroadcastFields.WantsLive(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => BroadcastFields.Missing(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => BroadcastFields.Validate(parameters);
    public AdapterOutcome Produce(AdapterCall call) => BroadcastFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wurde nichts gesendet.");
}

/// <summary>X-07: Herunterladen und Klonen bleiben aus.</summary>
public sealed class LicenseFetchAdapter : IStudioActionAdapter
{
    public string StudioId => "X-07";
    public string ActionId => "quelle.holen";
    public string Label => "Quelle holen";
    public SideEffectClass Effect => SideEffectClass.Publish;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Blocked;
    public string Evidence => "PromptActionTests.Quelle_wird_nicht_geholt";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => [];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return LicenseFields.WantsFetch(fields) ? new ActionDraft(ActionId, fields) : null;
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) => FieldKit.Blocked("Herunterladen ist nicht angeschlossen. Es wurde nichts geholt.");
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Herunterladen ist nicht angeschlossen. Es wurde nichts geholt.");
}

/// <summary>X-07: Lizenz nur mit OpenSource.Explain. Kein Netz.</summary>
public sealed class LicenseNoteAdapter : IStudioActionAdapter
{
    public string StudioId => "X-07";
    public string ActionId => "lizenz.merken";
    public string Label => "Lizenz merken";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Lizenz_wird_erklaert";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["name", "lizenz"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return LicenseFields.WantsFetch(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => LicenseFields.Missing(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) => LicenseFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wurde nichts heruntergeladen.");
}

/// <summary>SPEC-04: Bildzeit mit XrInspector.FrameBudget. Keine Brille.</summary>
public sealed class XrBudgetAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-04";
    public string ActionId => "bildzeit.pruefen";
    public string Label => "Bildzeit prüfen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Bildzeit_rechnet_das_Budget";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["rate", "messung"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "rate")) list.Add(new("rate", "rate", "Bilder pro Sekunde fehlen. Zum Beispiel „Rate: 72“.", true));
        if (!FieldKit.Has(parameters, "messung")) list.Add(new("messung", "messung", "Gemessene Bildzeit in Millisekunden fehlt.", true));
        return list;
    }
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => XrFields.Validate(parameters);
    public AdapterOutcome Produce(AdapterCall call) => XrFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es läuft keine Brille.");
}

/// <summary>SPEC-09: kein Scan und keine Punktwolke aus dem Auftrag.</summary>
public sealed class ScanBlockedAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-09";
    public string ActionId => "scan.starten";
    public string Label => "Scan starten";
    public SideEffectClass Effect => SideEffectClass.Machine;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Blocked;
    public string Evidence => "PromptActionTests.Scan_erzeugt_keine_punktwolke";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => [];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) =>
        FieldKit.Blocked("Der Scan ist nicht angeschlossen. Es wurde keine Punktwolke erzeugt.");
    public AdapterOutcome SideEffect(AdapterCall call) =>
        FieldKit.Blocked("Der Scan ist nicht angeschlossen. Es wurde keine Punktwolke erzeugt.");
}

static class KnowledgeFields
{
    public static AdapterOutcome Write(AdapterCall call)
    {
        var text = FieldKit.Text(call.Parameters, "text");
        var title = FieldKit.Text(call.Parameters, "titel");
        if (text.Length == 0) return FieldKit.Fail("Text fehlt.");
        try
        {
            var root = FieldKit.DataRoot(call);
            var store = new KnowledgeStore(root, new AuditLog(Path.Combine(root, "audit", "audit.jsonl")));
            var item = store.Add(title, text, "Auftrag");
            var body = item.Title + "\n" + item.Text + "\n";
            var found = new KnowledgeStore(root, new AuditLog(Path.Combine(root, "audit", "audit.jsonl"))).All().Any(i => i.Id == item.Id && i.Text == text.Trim());
            var qa = new QaResult("wissen.eintrag", found, "Wissen gelesen.", null, item.Id, "KnowledgeStore");
            var preview = item.Title;
            return FieldKit.Save(call, FieldKit.Slot("wissen", call.Version, "eintrag.txt"), body, preview, qa, ["Bleibt auf diesem PC."]);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException) { return FieldKit.Fail(ex.Message); }
    }
}

static class LearnFields
{
    public static AdapterOutcome Write(AdapterCall call)
    {
        var front = FieldKit.Text(call.Parameters, "frage");
        var back = FieldKit.Text(call.Parameters, "antwort");
        var deck = FieldKit.Has(call.Parameters, "stapel") ? FieldKit.Text(call.Parameters, "stapel") : "Allgemein";
        if (front.Length == 0 || back.Length == 0) return FieldKit.Fail("Frage oder Antwort fehlt.");
        try
        {
            var box = new LearnBox(FieldKit.DataRoot(call));
            var card = box.Add(deck, front, back);
            var csv = box.Export(deck);
            var again = new LearnBox(FieldKit.DataRoot(call)).Store.Cards.Any(c => c.Id == card.Id && c.Front == front.Trim() && c.Back == back.Trim());
            var qa = new QaResult("karte.export", again && csv.Contains(front.Trim().Replace(';', ','), StringComparison.Ordinal), "Karte gelesen.", null, card.Id, "LearnBox");
            return FieldKit.Save(call, FieldKit.Slot("lernen", call.Version, "karten.csv"), csv, front.Trim() + " · " + back.Trim(), qa);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException) { return FieldKit.Fail(ex.Message); }
    }
}

static class PromptFillFields
{
    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        try
        {
            var (_, missing) = Build(parameters);
            return missing.Count == 0 ? [] : [new("text", "Es fehlt: " + string.Join(", ", missing) + ".", true)];
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return [new("text", ex.Message, true)]; }
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        try
        {
            var (text, missing) = Build(call.Parameters);
            if (missing.Count > 0) return FieldKit.Fail("Es fehlt: " + string.Join(", ", missing) + ".");
            var hints = PromptLibrary.Check(text);
            var body = text + "\n" + string.Join("\n", hints.Select(h => h.Level + ": " + h.Text)) + "\n";
            var again = Build(call.Parameters).Text;
            var qa = new QaResult("anweisung.text", again == text, "Anweisung gelesen.", null, null, "PromptLibrary");
            var preview = text.Replace("\n", " ");
            if (preview.Length > 80) preview = preview[..80];
            return FieldKit.Save(call, FieldKit.Slot("anweisung", call.Version, "text.txt"), body, preview, qa, ["Die KI wurde nicht gefragt."]);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return FieldKit.Fail(ex.Message); }
    }

    public static (string Text, IReadOnlyList<string> Missing) Build(IReadOnlyDictionary<string, string> parameters)
    {
        if (!FieldKit.Has(parameters, "text"))
        {
            var made = PromptLibrary.Master(FieldKit.Text(parameters, "rolle"), FieldKit.Text(parameters, "ziel"), FieldKit.Text(parameters, "hintergrund"), FieldKit.Text(parameters, "format"), FieldKit.Text(parameters, "regeln"));
            return (made, []);
        }
        var template = FieldKit.Text(parameters, "text");
        var values = new Dictionary<string, string>();
        foreach (var (name, _) in PromptLibrary.Variables(template))
        {
            var norm = PromptFields.Norm(name);
            if (norm is "text") continue;
            if (FieldKit.Has(parameters, norm)) values[name] = FieldKit.Text(parameters, norm);
        }
        return PromptLibrary.Fill(template, values);
    }
}

static class FashionFields
{
    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        try { _ = Text(parameters); return []; }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return [new("groesse", ex.Message, true)]; }
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        try
        {
            var body = Text(call.Parameters) + "\n";
            var again = Text(call.Parameters);
            var qa = new QaResult("groesse.tabelle", again + "\n" == body, "Größe gelesen.", null, again, "Fashion");
            var preview = again.Length > 80 ? again[..80] : again;
            return FieldKit.Save(call, FieldKit.Slot("mode", call.Version, "groesse.txt"), body, preview, qa, ["Nur die Größentabelle."]);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return FieldKit.Fail(ex.Message); }
    }

    public static string Text(IReadOnlyDictionary<string, string> parameters)
    {
        if (FieldKit.Has(parameters, "groesse"))
            return Fashion.WomenSize((int)Math.Round(Fach.Num(FieldKit.Text(parameters, "groesse"), "groesse")));
        var women = !FieldKit.Yes(parameters, "herr");
        var hip = FieldKit.TryNum(parameters, "huefte", out var h) ? h : 0;
        var size = Fashion.FromBody(women, Fach.Num(FieldKit.Text(parameters, "brust"), "brust"), Fach.Num(FieldKit.Text(parameters, "taille"), "taille"), hip);
        return "EU " + size.Eu + " · " + size.Letter + " · " + size.Note;
    }
}

static class BroadcastFields
{
    public static bool WantsLive(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Yes(parameters, "senden") || FieldKit.Yes(parameters, "stream");

    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "titel")) list.Add(new("titel", "titel", "Titel der Sendung fehlt.", true));
        if (!FieldKit.Has(parameters, "punkt")) list.Add(new("punkt", "punkt", "Programmpunkt fehlt. Zum Beispiel „Punkt: Begrüßung | 30“.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        try { _ = Points(parameters); return []; }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { return [new("punkt", ex.Message, true)]; }
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        try
        {
            var points = Points(call.Parameters);
            var root = Path.Combine(call.Projects.ProjectFilesRoot(call.ProjectId), "sendung", "v" + call.Version, "arbeit");
            Directory.CreateDirectory(root);
            var desk = new BroadcastDesk(root);
            desk.Plan.Show = FieldKit.Text(call.Parameters, "titel");
            desk.Plan.Start = new DateTime(2026, 10, 22, 20, 0, 0);
            desk.Plan.SlotMinutes = 60;
            foreach (var (title, seconds) in points) desk.Add(title, seconds);
            var body = desk.Sheet() + "Es wurde nichts gesendet.\n";
            var qa = new QaResult("sendung.ablauf", desk.Rows().Count == points.Count && desk.Total == TimeSpan.FromSeconds(points.Sum(p => p.Seconds)), "Ablauf gelesen.", null, desk.Plan.Show, "BroadcastDesk");
            var preview = desk.Plan.Show + " · " + BroadcastDesk.Clock(desk.Total);
            return FieldKit.Save(call, FieldKit.Slot("sendung", call.Version, "ablauf.txt"), body, preview, qa);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or IOException) { return FieldKit.Fail(ex.Message); }
    }

    static List<(string Title, int Seconds)> Points(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<(string, int)>();
        foreach (var line in FieldKit.Text(parameters, "punkt").Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|', 2);
            if (parts.Length < 2) throw new FormatException("Programmpunkt als „Titel | Dauer“, zum Beispiel „Begrüßung | 30“.");
            list.Add((parts[0].Trim(), BroadcastDesk.ParseDuration(parts[1])));
        }
        if (list.Count == 0) throw new FormatException("Programmpunkt fehlt.");
        return list;
    }
}

static class LicenseFields
{
    public static bool WantsFetch(IReadOnlyDictionary<string, string> parameters) => FieldKit.Yes(parameters, "holen");

    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "name")) list.Add(new("name", "name", "Name der Quelle fehlt.", true));
        if (!FieldKit.Has(parameters, "lizenz")) list.Add(new("lizenz", "lizenz", "Lizenz fehlt. Zum Beispiel „Lizenz: MIT“.", true));
        return list;
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        var name = FieldKit.Text(call.Parameters, "name");
        var spdx = FieldKit.Text(call.Parameters, "lizenz");
        if (name.Length == 0 || spdx.Length == 0) return FieldKit.Fail("Name oder Lizenz fehlt.");
        var kind = OpenSource.Classify(spdx);
        var explain = OpenSource.Explain(spdx);
        var body = name + "\n" + explain + "\n" + kind + "\n";
        var again = OpenSource.Explain(spdx);
        var qa = new QaResult("lizenz.text", again == explain && OpenSource.Classify(spdx) == kind, "Lizenz gelesen.", null, spdx, "OpenSource");
        return FieldKit.Save(call, FieldKit.Slot("lizenz", call.Version, "hinweis.txt"), body, explain, qa, ["Es wurde nichts heruntergeladen."]);
    }
}

static class XrFields
{
    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (!FieldKit.TryNum(parameters, "rate", out var fps) || fps < 30 || fps > 240 || Math.Abs(fps - Math.Round(fps)) > 0.001)
            return [new("rate", "Die Rate ist eine ganze Zahl von 30 bis 240.", true)];
        if (!FieldKit.TryNum(parameters, "messung", out var ms) || ms <= 0 || ms > 1000)
            return [new("messung", "Die Messung muss in Millisekunden größer als 0 sein.", true)];
        return [];
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        try
        {
            var fps = (int)Math.Round(Fach.Num(FieldKit.Text(call.Parameters, "rate"), "rate"));
            var measured = Fach.Num(FieldKit.Text(call.Parameters, "messung"), "messung");
            var budget = XrInspector.FrameBudget(fps, measured);
            var state = budget.Smooth ? "flüssig" : "zu langsam";
            var body = "Bildzeit " + Fach.F(budget.BudgetMs, 2) + " ms\nMessung " + Fach.F(measured, 2) + " ms\n" + state + "\nKeine Brille ist gelaufen.\n";
            var again = XrInspector.FrameBudget(fps, measured);
            var qa = new QaResult("bildzeit.budget", again.BudgetMs == budget.BudgetMs && again.Smooth == budget.Smooth, "Bildzeit gelesen.", "ms", Fach.F(budget.BudgetMs, 2), "XrInspector");
            return FieldKit.Save(call, FieldKit.Slot("xr", call.Version, "bildzeit.txt"), body, Fach.F(budget.BudgetMs, 2) + " ms · " + state, qa);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return FieldKit.Fail(ex.Message); }
    }
}
