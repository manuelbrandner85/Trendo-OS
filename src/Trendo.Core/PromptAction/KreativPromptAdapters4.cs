using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Trendo.Core.Kreativ;

namespace Trendo.Core.PromptAction;

/// <summary>STUDIO-CODEAI-01: Projekt untersuchen – Projektart und passender Testbefehl. Es wird nichts ausgeführt.</summary>
public sealed class CodeProjectAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-CODEAI-01";
    public string ActionId => "code.projekt";
    public string Label => "Projekt untersuchen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKits4Tests.Code";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["ordner"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "ordner") ? [] : [new("ordner", "ordner", "Wo liegt das Projekt? Schreib „Ordner: C:\\…“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "ordner") && !KreativSafe.LocalDir(FieldKit.Text(parameters, "ordner")) ? [new("ordner", "Den Projektordner gibt es auf diesem Rechner nicht.", true)] : [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var root = FieldKit.Text(call.Parameters, "ordner");
        var kinds = CodeAiKit.DetectProject(root);
        var files = CodeProject.Tree(root, null, 4000).Count(e => !e.IsDir);
        var sb = new StringBuilder($"# Projekt „{Path.GetFileName(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar))}“\n\n{files} Dateien (ohne Bau-, Paket- und Git-Ordner).\n\n");
        if (kinds.Count == 0) sb.Append("Keine bekannte Projektart erkannt (.NET, npm, Python, Cargo, Go).\n");
        foreach (var k in kinds)
            sb.Append($"- {k.Label} (erkannt an {k.Marker}): Test mit `{CodeAiKit.TestCommand(root, k.Id)?.Display ?? "–"}`\n");
        sb.Append("\nAusgeführt wurde nichts. Tests startest du im Studio „Code mit KI“ nach deiner Bestätigung.\n");
        var qa = new QaResult(ActionId, true, $"{kinds.Count} Projektart(en) erkannt.", null, root, "CodeAiKit");
        return FieldKit.Save(call, FieldKit.Slot("code-ki", call.Version, "projekt.md"), sb.ToString(), kinds.Count > 0 ? kinds[0].Label : "unbekannt", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Tests startest du im Studio „Code mit KI“ nach deiner Bestätigung.");
}

/// <summary>STUDIO-VISION-01: Bildfakten eines Ordners (Format, Größe, EXIF). Schärfe, Helligkeit und Farben prüft das Studio.</summary>
public sealed class VisionFactsAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-VISION-01";
    public string ActionId => "bilder.fakten";
    public string Label => "Bildfakten auflisten";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKits4Tests.Bilder";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["ordner"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    static readonly string[] Ext = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".heic"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "ordner") ? [] : [new("ordner", "ordner", "Wo liegen die Bilder? Schreib „Ordner: C:\\…“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "ordner") && !KreativSafe.LocalDir(FieldKit.Text(parameters, "ordner")) ? [new("ordner", "Den Bildordner gibt es auf diesem Rechner nicht.", true)] : [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var dir = FieldKit.Text(call.Parameters, "ordner");
        var files = Directory.EnumerateFiles(dir).Where(f => Ext.Contains(Path.GetExtension(f).ToLowerInvariant())).Order(StringComparer.OrdinalIgnoreCase).Take(500).ToList();
        var sb = new StringBuilder($"# Bildfakten\n\n{files.Count} Bilder in „{Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar))}“.\n\n| Datei | Format | Größe | Aufnahme | Kamera |\n|---|---|---|---|---|\n");
        foreach (var f in files)
        {
            try
            {
                var len = new FileInfo(f).Length;
                var head = new byte[(int)Math.Min(len, 256 * 1024)];
                using (var fs = File.OpenRead(f)) fs.ReadExactly(head);
                var exif = VisionKit.Exif(head);
                static string Cell(string s) => s.Replace("|", "/").Replace("\n", " ");
                sb.Append($"| {Cell(Path.GetFileName(f))} | {VisionKit.Format(head)} | {Math.Round(len / 1024.0).ToString(CultureInfo.InvariantCulture)} KB | {exif?.Taken?.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) ?? "–"} | {Cell(exif?.Camera is { Length: > 0 } c ? c : "–")} |\n");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        sb.Append("\nSchärfe, Helligkeit, Hauptfarben und Text im Bild prüft das Studio „Bilder erkennen“.\n");
        var qa = new QaResult(ActionId, true, $"{files.Count} Bilder aufgelistet.", null, dir, "VisionKit");
        return FieldKit.Save(call, FieldKit.Slot("bilder", call.Version, "bildfakten.md"), sb.ToString(), $"{files.Count} Bilder", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts verändert oder hochgeladen.");
}

/// <summary>STUDIO-CITE-01: Eine Quelle im gewählten Zitierstil, mit fehlenden Angaben und BibTeX.</summary>
public sealed class CiteSourceAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-CITE-01";
    public string ActionId => "quelle.zitieren";
    public string Label => "Quelle zitieren";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKits4Tests.Quellen";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["titel"];
    public IReadOnlyList<string> OutputKinds => ["md"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "titel") ? [] : [new("titel", "titel", "Wie heißt die Quelle? Schreib „Titel: …“ (dazu z. B. „Autorin:“, „Jahr:“, „Verlag:“).", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "stil") && !CiteKit.Styles.Any(s => s.Id == Style(parameters))) f.Add(new("stil", "Stil: APA, Harvard oder DIN.", true));
        if (FieldKit.Has(parameters, "art") && !CiteKit.Kinds.Any(k => k.Id == FieldKit.Text(parameters, "art").ToLowerInvariant())) f.Add(new("art", "Art: Buch, Artikel oder Webseite.", true));
        return f;
    }
    static string Style(IReadOnlyDictionary<string, string> p)
    {
        var s = FieldKit.Text(p, "stil").ToLowerInvariant();
        return s.StartsWith("apa", StringComparison.Ordinal) ? "apa" : s.StartsWith("din", StringComparison.Ordinal) ? "din" : s.StartsWith("harv", StringComparison.Ordinal) ? "harvard" : s.Length == 0 ? "apa" : s;
    }
    public static CiteKit.Source FromFields(IReadOnlyDictionary<string, string> p)
    {
        var url = FieldKit.Text(p, "url", "adresse", "link");
        var kind = FieldKit.Text(p, "art").ToLowerInvariant() is { Length: > 0 } k ? k : FieldKit.Has(p, "zeitschrift") ? "artikel" : url.Length > 0 ? "webseite" : "buch";
        return new CiteKit.Source
        {
            Id = "q1", Kind = kind, Authors = FieldKit.Text(p, "autorin", "autor", "autoren", "von"), Title = FieldKit.Text(p, "titel"), Year = FieldKit.Text(p, "jahr"),
            Publisher = FieldKit.Text(p, "verlag"), Place = FieldKit.Text(p, "ort"), Edition = FieldKit.Text(p, "auflage"), Journal = FieldKit.Text(p, "zeitschrift", "seite", "webseite"),
            Volume = FieldKit.Text(p, "band", "jahrgang"), Issue = FieldKit.Text(p, "heft", "ausgabe"), Pages = FieldKit.Text(p, "seiten"), Url = url, Doi = FieldKit.Text(p, "doi"),
            Accessed = CiteKit.Date(FieldKit.Text(p, "abruf", "abgerufen", "abrufdatum")) is { } d ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : FieldKit.Text(p, "abruf", "abgerufen", "abrufdatum"),
        };
    }
    public AdapterOutcome Produce(AdapterCall call)
    {
        var s = FromFields(call.Parameters); var style = Style(call.Parameters);
        var missing = CiteKit.Missing(s, style);
        var body = $"# Quelle\n\n{CiteKit.Format(s, style)}\n\nIm Text: {CiteKit.InText(s, style)}\n\n" +
                   (missing.Count > 0 ? "Fehlt noch: " + string.Join(", ", missing) + "\n\n" : "") + "```bibtex\n" + CiteKit.BibTex([s]) + "```\n";
        var qa = new QaResult(ActionId, true, missing.Count == 0 ? "Vollständig." : $"{missing.Count} Angaben fehlen.", null, s.Title, "CiteKit");
        return FieldKit.Save(call, FieldKit.Slot("quellen", call.Version, "literatur.md"), body, CiteKit.Plain(CiteKit.Format(s, style)), qa, missing.Count > 0 ? ["Fehlt: " + string.Join(", ", missing)] : null);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts gesendet oder veröffentlicht.");
}

/// <summary>STUDIO-DAYPLAN-01: Tag planen – Aufgaben um feste Termine herum, mit Pausen. Trägt nichts in den Kalender ein.</summary>
public sealed partial class DayPlanAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-DAYPLAN-01";
    public string ActionId => "tag.planen";
    public string Label => "Tag planen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "KreativKits4Tests.Tagesplaner";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["aufgaben"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "aufgaben") ? [] : [new("aufgaben", "aufgaben", "Was steht an? Schreib „Aufgaben: Angebot schreiben 90 min hoch; Einkauf 30 min“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        foreach (var k in new[] { "beginn", "ende" })
            if (FieldKit.Has(parameters, k) && DayPlanner.ParseTime(FieldKit.Text(parameters, k)) is null) f.Add(new(k, $"{(k == "beginn" ? "Beginn" : "Ende")} als Uhrzeit, z. B. 08:00.", true));
        if (FieldKit.Has(parameters, "termine")) try { Appointments(FieldKit.Text(parameters, "termine"), DateTime.Today); } catch (FormatException e) { f.Add(new("termine", e.Message, true)); }
        return f;
    }

    [GeneratedRegex(@"^(\d{1,2}[:.]\d{2})\s*[-–bis]+\s*(\d{1,2}[:.]\d{2})\s+(.+)$")]
    private static partial Regex ApptRx();

    /// <summary>„10:00-10:30 Teamrunde; 14:00–15:00 Zahnarzt“.</summary>
    public static List<DayPlanner.Appointment> Appointments(string text, DateTime day)
    {
        var res = new List<DayPlanner.Appointment>();
        foreach (var part in text.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var m = ApptRx().Match(part);
            if (!m.Success || DayPlanner.ParseTime(m.Groups[1].Value.Replace('.', ':')) is not { } s || DayPlanner.ParseTime(m.Groups[2].Value.Replace('.', ':')) is not { } e || e <= s)
                throw new FormatException($"Termin „{part}“ bitte als „10:00-10:30 Name“.");
            res.Add(new($"a{res.Count + 1}", m.Groups[3].Value.Trim(), day.Date + s, day.Date + e));
        }
        return res;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var day = DateTime.TryParseExact(FieldKit.Text(call.Parameters, "tag", "datum"), ["d.M.yyyy", "dd.MM.yyyy", "yyyy-MM-dd"], CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out var d) ? d : DateTime.Today;
        var items = DayPlanner.ParseItems(FieldKit.Text(call.Parameters, "aufgaben").Replace(';', '\n'), day);
        var appts = FieldKit.Has(call.Parameters, "termine") ? Appointments(FieldKit.Text(call.Parameters, "termine"), day) : [];
        var o = new DayPlanner.Options(DayPlanner.ParseTime(FieldKit.Text(call.Parameters, "beginn")) ?? TimeSpan.FromHours(8), DayPlanner.ParseTime(FieldKit.Text(call.Parameters, "ende")) ?? TimeSpan.FromHours(17));
        var plan = DayPlanner.Make(day, items, appts, o);
        var qa = new QaResult(ActionId, true, $"{plan.Blocks.Count(b => b.Kind == DayPlanner.Work)} Aufgaben eingeplant, {plan.Tomorrow.Count} für morgen.", null, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "DayPlanner");
        return FieldKit.Save(call, FieldKit.Slot("tagesplan", call.Version, "tagesplan.txt"), DayPlanner.Text(plan, o), $"{plan.Blocks.Count} Blöcke", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("In den Kalender trägst du den Plan selbst ein (Kalenderdatei aus dem Studio).");
}
