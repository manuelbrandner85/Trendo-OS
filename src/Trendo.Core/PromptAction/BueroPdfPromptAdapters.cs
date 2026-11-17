using System.Globalization;
using System.Text;
using Trendo.Core.Sys;

namespace Trendo.Core.PromptAction;

/// <summary>STUDIO-PDFTOOLS-01 PDF-Werkstatt: Arbeitsauftrag prüfen und vorbereiten – Seitenangaben werden aufgelöst (wie sie die Werkstatt verwendet),
/// dazu die Schritte im Studio. Die KI bearbeitet keine Dateien selbst; ausgeführt wird in der Werkstatt, das Original bleibt.</summary>
public sealed class PdfWerkstattAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-PDFTOOLS-01";
    public string ActionId => "pdf.auftrag";
    public string Label => "PDF-Auftrag vorbereiten";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PdfWerkstattTests.Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["aktion"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    /// <summary>Werkzeuge der Werkstatt: Id, Name, Schritt im Studio.</summary>
    public static readonly (string Id, string Name, string How)[] Actions =
    [
        ("zusammenfuegen", "Zusammenfügen", "Dateien in der gewünschten Reihenfolge hinzufügen, mit ▲/▼ ordnen, „Ausführen“."),
        ("teilen", "Teilen nach Bereichen", "Datei hinzufügen, Bereiche eintragen (z. B. „1-3; 4-z“) oder „alle n Seiten“ wählen, „Ausführen“."),
        ("ordnen", "Seiten ordnen", "Datei hinzufügen, Seiten in der Übersicht anklicken, verschieben, drehen oder löschen, „Ausführen“."),
        ("drehen", "Seiten drehen", "Seiten in der Übersicht wählen, ⟳ drehen, „Ausführen“."),
        ("loeschen", "Seiten löschen", "Seiten in der Übersicht wählen, „Löschen“, „Ausführen“."),
        ("bilder", "Bilder zu PDF", "Bilder hinzufügen, Seitengröße wählen (A4 oder Bildgröße), „Ausführen“."),
        ("komprimieren", "Komprimieren", "Stufe wählen (leicht, mittel, stark), „Ausführen“ – vorher/nachher wird gemessen."),
        ("wasserzeichen", "Wasserzeichen", "Text, Deckkraft und Seiten wählen, „Ausführen“."),
        ("stempel", "Stempel", "Stempeltext (z. B. GEPRÜFT), Ecke und Datum wählen, „Ausführen“."),
        ("seitenzahlen", "Seitenzahlen", "Format (z. B. „Seite {n} von {N}“), Lage und Startzahl wählen, „Ausführen“."),
        ("metadaten", "Metadaten", "Titel, Verfasser, Thema eintragen oder „Alle entfernen“, „Ausführen“."),
        ("passwort", "Passwort", "Passwort setzen – oder mit dem bekannten Passwort entfernen, „Ausführen“."),
        ("schwaerzen", "Schwärzen", "Balken über die Stellen ziehen, „Ausführen“ – die Seite wird zum Bild, der Inhalt darunter ist weg."),
        ("vergleichen", "Vergleichen", "Zwei Dateien hinzufügen, „Ausführen“ – Unterschiede mit Seitenangabe."),
    ];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "aktion") ? [] : [new("aktion", "aktion", "Was soll mit der PDF passieren? Schreib z. B. „Aktion: teilen, Seiten: 1-3; 4-z“.", true)];

    static string Id(string raw)
    {
        var t = raw.Trim().ToLowerInvariant().Replace("ü", "ue").Replace("ä", "ae").Replace("ö", "oe");
        return Actions.FirstOrDefault(a => a.Id == t || a.Name.ToLowerInvariant().Replace("ü", "ue").Replace("ä", "ae").Replace("ö", "oe") == t).Id ?? "";
    }

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "aktion") && Id(FieldKit.Text(parameters, "aktion")).Length == 0)
            f.Add(new("aktion", "Aktion: " + string.Join(", ", Actions.Select(a => a.Id)) + ".", true));
        var hasCount = FieldKit.Has(parameters, "seitenzahl");
        if (hasCount && (!int.TryParse(FieldKit.Text(parameters, "seitenzahl"), out var n) || n is < 1 or > 100000)) f.Add(new("seitenzahl", "Seitenzahl als ganze Zahl, z. B. 12.", true));
        else if (FieldKit.Has(parameters, "seiten"))
        {
            var count = hasCount ? int.Parse(FieldKit.Text(parameters, "seitenzahl"), CultureInfo.InvariantCulture) : 100000;
            try { foreach (var g in Groups(FieldKit.Text(parameters, "seiten"))) PdfTools.ParsePages(g, count); }
            catch (Exception e) when (e is ArgumentException or FormatException or OverflowException) { f.Add(new("seiten", e.Message.Length > 0 ? e.Message : "Seitenangabe wie „1-3; 4-z“.", true)); }
        }
        return f;
    }

    static string[] Groups(string spec) => spec.Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters; var act = Actions.First(a => a.Id == Id(FieldKit.Text(p, "aktion")));
        var sb = new StringBuilder();
        sb.Append("PDF-Auftrag: ").Append(act.Name).Append('\n');
        var count = int.TryParse(FieldKit.Text(p, "seitenzahl"), out var n) ? n : 0;
        var spec = FieldKit.Text(p, "seiten"); var files = 0;
        if (spec.Length > 0 && count > 0)
        {
            var groups = act.Id == "teilen" ? Groups(spec) : [spec.Replace(';', ',')];
            for (var k = 0; k < groups.Length; k++)
            {
                var pages = PdfTools.ParsePages(groups[k], count).Select(i => (i + 1).ToString(CultureInfo.InvariantCulture));
                sb.Append(act.Id == "teilen" ? $"Datei {k + 1}: Seiten " : "Seiten: ").Append(string.Join(", ", pages)).Append('\n');
                files++;
            }
        }
        else if (spec.Length > 0) sb.Append("Seiten: ").Append(spec).Append(" (wird beim Ausführen mit der echten Seitenzahl geprüft)\n");
        if (FieldKit.Has(p, "text")) sb.Append("Text: ").Append(FieldKit.Text(p, "text")).Append('\n');
        sb.Append("\nSo geht es in der PDF-Werkstatt:\n1. Links „").Append(act.Name).Append("“ wählen.\n2. ").Append(act.How)
          .Append("\n3. Das Ergebnis ist eine neue Datei unter „Ergebnisse“ – das Original bleibt unverändert.\n");
        var qa = new QaResult(ActionId, true, files > 0 ? $"{files} Seitengruppe(n) aufgelöst." : "Auftrag ohne Seitenangabe.", null, act.Id, "PdfTools.ParsePages");
        return FieldKit.Save(call, FieldKit.Slot("pdf-auftrag", call.Version, "pdf-auftrag.txt"), sb.ToString(), act.Name, qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Dateien bearbeitet die PDF-Werkstatt im Studio – die KI ändert keine PDFs selbst.");
}

/// <summary>STUDIO-FORMS-01 Formulare ausfüllen: Feldnamen eines Formulars den „Meinen Angaben“ zuordnen (welches Feld bekommt was) – ohne Werte,
/// damit nichts Persönliches in das Projekt gelangt. Ausgefüllt wird im Studio.</summary>
public sealed class FormularZuordnenAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-FORMS-01";
    public string ActionId => "formular.zuordnen";
    public string Label => "Formularfelder zuordnen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "FormulareTests.Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["felder"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "felder") ? [] : [new("felder", "felder", "Welche Felder hat das Formular? Schreib z. B. „Felder: Vorname; Nachname; PLZ, Ort; Geburtsdatum“.", true)];

    static List<string> Names(string raw) => [.. raw.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct()];

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "felder") && Names(FieldKit.Text(parameters, "felder")).Count > 500) f.Add(new("felder", "Höchstens 500 Felder auf einmal.", true));
        return f;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var names = Names(FieldKit.Text(call.Parameters, "felder"));
        var label = FormProfile.Keys.ToDictionary(k => k.Key, k => k.Name);
        var sb = new StringBuilder("Zuordnung zu „Meine Angaben“ (ohne Werte):\n");
        var hit = 0;
        foreach (var n in names)
        {
            var keys = FormProfile.Match(n);
            if (keys.Length == 0) { sb.Append("– ").Append(n).Append(": selbst ausfüllen\n"); continue; }
            hit++;
            sb.Append("✓ ").Append(n).Append(" ← ").Append(string.Join(" + ", keys.Select(k => label[k])))
              .Append(keys.Any(FormProfile.Sensitive.Contains) ? " (wird nur beim Export eingesetzt, im Programm maskiert)" : "").Append('\n');
        }
        sb.Append(CultureInfo.InvariantCulture, $"\n{hit} von {names.Count} Feldern kann Trendo aus „Meine Angaben“ vorschlagen. Vorschläge prüfst du im Studio, bevor sie übernommen werden.\n");
        var qa = new QaResult(ActionId, names.Count > 0, $"{hit}/{names.Count} zugeordnet.", null, $"{names.Count} Felder", "FormProfile.Match");
        return FieldKit.Save(call, FieldKit.Slot("formular", call.Version, "formular-zuordnung.txt"), sb.ToString(), $"{hit} von {names.Count} Feldern", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Formulare füllst du im Studio aus und prüfst sie dort – die KI schickt nichts ab.");
}
