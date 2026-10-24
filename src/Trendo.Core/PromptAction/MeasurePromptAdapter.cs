using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Trendo.Core.PromptAction;

/// <summary>SYS-02: Ausdruck mit Calc und Notiz über NoteStore. Keine Musik, kein Verschieben.</summary>
public sealed class CalcNoteAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-02";
    public string ActionId => "helfer.rechnen";
    public string Label => "Rechnung oder Notiz";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Helfer_rechnet_und_merkt_notiz";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["ausdruck"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) =>
        new(ActionId, FieldKit.Read(prompt, known));

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        CalcFields.Missing(parameters);

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        CalcFields.Validate(parameters);

    public AdapterOutcome Produce(AdapterCall call) => CalcFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es gibt hier keine Wirkung nach außen.");
}

/// <summary>X-11: legt wirklich einen Projektordner an. Öffnet kein Fenster.</summary>
public sealed class ProjectCreateAdapter : IStudioActionAdapter
{
    public string StudioId => "X-11";
    public string ActionId => "projekt.anlegen";
    public string Label => "Projekt anlegen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Projekt_wird_angelegt";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["kennung", "name"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) =>
        new(ActionId, FieldKit.Read(prompt, known));

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        ProjectFields.Missing(parameters);

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        ProjectFields.Validate(parameters);

    public AdapterOutcome Produce(AdapterCall call) => ProjectFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es öffnet sich kein Fenster.");
}

/// <summary>SPEC-01: Fläche, Bedarf und Grundriss. Bestellt nichts.</summary>
public sealed class RoomPlanAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-01";
    public string ActionId => "wohnung.flaeche";
    public string Label => "Fläche rechnen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Wohnung_rechnet_flaeche";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["name", "laenge", "breite"];
    public IReadOnlyList<string> OutputKinds => ["svg"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) =>
        new(ActionId, FieldKit.Read(prompt, known));

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        RoomFields.Missing(parameters);

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        RoomFields.Validate(parameters);

    public AdapterOutcome Produce(AdapterCall call) => RoomFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts bestellt.");
}

/// <summary>SPEC-02: Ohmsches Gesetz oder LED-Vorwiderstand.</summary>
public sealed class CircuitAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-02";
    public string ActionId => "schaltung.rechnen";
    public string Label => "Schaltung rechnen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Schaltung_rechnet_ohm_und_led";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["spannung"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) =>
        new(ActionId, FieldKit.Read(prompt, known));

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        CircuitFields.Missing(parameters);

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        CircuitFields.Validate(parameters);

    public AdapterOutcome Produce(AdapterCall call) => CircuitFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird keine Schaltung aufgebaut.");
}

/// <summary>SPEC-03: Balken mit der eingebauten Rechnung.</summary>
public sealed class BeamCheckAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-03";
    public string ActionId => "balken.pruefen";
    public string Label => "Balken prüfen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Balken_prueft_last";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["laenge", "last", "breite", "hoehe", "werkstoff"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) =>
        new(ActionId, FieldKit.Read(prompt, known));

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        BeamFields.Missing(parameters);

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        BeamFields.Validate(parameters);

    public AdapterOutcome Produce(AdapterCall call) => BeamFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es gibt keine Statik-Freigabe.");
}

/// <summary>SPEC-10: eingetippte Messreihe als Tabelle. Verbindet kein Gerät.</summary>
public sealed class TwinSeriesAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-10";
    public string ActionId => "messreihe.zeigen";
    public string Label => "Messreihe zeigen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Messreihe_zeigt_ampel";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["anlage", "sensor", "werte"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) =>
        new(ActionId, FieldKit.Read(prompt, known));

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        TwinFields.Missing(parameters);

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        TwinFields.Validate(parameters);

    public AdapterOutcome Produce(AdapterCall call) => TwinFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird kein Gerät verbunden.");
}

/// <summary>SPEC-13: Strecke aus zwei Punkten oder einer GPX-Datei im Projekt.</summary>
public sealed class RouteAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-13";
    public string ActionId => "route.messen";
    public string Label => "Strecke messen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Route_misst_strecke";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["von", "nach"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) =>
        new(ActionId, FieldKit.Read(prompt, known));

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        RouteFields.Missing(parameters);

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        RouteFields.Validate(parameters);

    public AdapterOutcome Produce(AdapterCall call) => RouteFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird keine Karte geladen.");
}

/// <summary>SPEC-14: Maschine, G-Code-Lauf und Freigabe im Text starten nichts.</summary>
public sealed class CncMachineAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-14";
    public string ActionId => "fraese.starten";
    public string Label => "Maschine starten";
    public SideEffectClass Effect => SideEffectClass.Machine;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Blocked;
    public string Evidence => "PromptActionTests.Fraese_startet_nichts";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => [];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return CncFields.WantsMachine(fields) ? new ActionDraft(ActionId, fields) : null;
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) =>
        FieldKit.Blocked("Die Maschine ist nicht angeschlossen. Es wurde nichts gefräst.");
    public AdapterOutcome SideEffect(AdapterCall call) =>
        FieldKit.Blocked("Die Maschine ist nicht angeschlossen. Es wurde nichts gefräst.");
}

/// <summary>SPEC-14: nur Drehzahl und Vorschub aus Cnc.Feeds.</summary>
public sealed class CncFeedAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-14";
    public string ActionId => "fraese.rechnen";
    public string Label => "Drehzahl rechnen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Fraese_rechnet_drehzahl";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["material", "durchmesser", "schneiden"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return CncFields.WantsMachine(fields) ? null : new ActionDraft(ActionId, fields);
    }

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        CncFields.Missing(parameters);

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        CncFields.Validate(parameters);

    public AdapterOutcome Produce(AdapterCall call) => CncFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) =>
        FieldKit.Blocked("Die Maschine ist nicht angeschlossen.");
}

/// <summary>SPEC-16: Jahreskosten aus VehiclePlus. Bucht nichts.</summary>
public sealed class VehicleCostAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-16";
    public string ActionId => "fahrt.kosten";
    public string Label => "Fahrtkosten";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Fahrt_rechnet_kosten";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["kilometer", "verbrauch", "preis"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) =>
        new(ActionId, FieldKit.Read(prompt, known));

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        VehicleFields.Missing(parameters);

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        VehicleFields.Validate(parameters);

    public AdapterOutcome Produce(AdapterCall call) => VehicleFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wurde nichts gebucht.");
}

/// <summary>SPEC-05: Mittelwert oder Gerade aus Stats.</summary>
public sealed class StatsAdapter : IStudioActionAdapter
{
    public string StudioId => "SPEC-05";
    public string ActionId => "messung.auswerten";
    public string Label => "Messwerte auswerten";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "PromptActionTests.Messung_rechnet_mittelwert";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["werte"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) =>
        new(ActionId, FieldKit.Read(prompt, known));

    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        StatsFields.Missing(parameters);

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        StatsFields.Validate(parameters);

    public AdapterOutcome Produce(AdapterCall call) => StatsFields.Write(call);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es gibt keine wissenschaftliche Beratung.");
}

static class CalcFields
{
    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        if (FieldKit.Has(parameters, "ausdruck") || FieldKit.Has(parameters, "notiz")) return [];
        return [new("ausdruck", "ausdruck", "Schreib einen Ausdruck als „Ausdruck: …“ oder eine Notiz als „Notiz: …“.", true)];
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ValidationFinding>();
        var expr = FieldKit.Text(parameters, "ausdruck");
        if (expr.Length > 200) list.Add(new("ausdruck", "Der Ausdruck ist zu lang.", true));
        else if (expr.Length > 0)
        {
            try { _ = Calc.Eval(expr); }
            catch (Exception ex) when (ex is FormatException or DivideByZeroException)
            { list.Add(new("ausdruck", ex.Message, true)); }
        }
        if (FieldKit.Text(parameters, "notiz").Length > 2000) list.Add(new("notiz", "Die Notiz ist zu lang.", true));
        return list;
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        try
        {
            var expr = FieldKit.Text(call.Parameters, "ausdruck");
            var note = FieldKit.Text(call.Parameters, "notiz");
            if (expr.Length == 0 && note.Length == 0) return FieldKit.Fail("Ausdruck oder Notiz fehlt.");
            double? value = expr.Length == 0 ? null : Calc.Eval(expr);
            if (note.Length > 0) new NoteStore(FieldKit.DataRoot(call)).Add(note);
            var body = new StringBuilder();
            if (value is { } number) body.Append("Ergebnis ").Append(Calc.Format(number)).Append('\n');
            if (expr.Length > 0) body.Append("Ausdruck ").Append(expr).Append('\n');
            if (note.Length > 0) body.Append("Notiz ").Append(note).Append('\n');
            var text = body.ToString();
            var preview = value is { } shown ? "Ergebnis " + Calc.Format(shown) : "Notiz " + Trim(note);
            var qa = new QaResult("helfer.rechnung", RoundTrip(call, expr, note, value), "Rechnung und Notiz gelesen.", null, expr, "Calc");
            return FieldKit.Save(call, FieldKit.Slot("helfer", call.Version, "ergebnis.txt"), text, preview, qa, ["Musik bleibt aus."]);
        }
        catch (Exception ex) when (ex is FormatException or DivideByZeroException or IOException)
        { return FieldKit.Fail(ex.Message); }
    }

    static bool RoundTrip(AdapterCall call, string expr, string note, double? value)
    {
        if (expr.Length > 0 && value is { } number && Calc.Eval(expr) != number) return false;
        if (note.Length == 0) return true;
        return new NoteStore(FieldKit.DataRoot(call)).All().Any(n => n.Text == note);
    }

    static string Trim(string text) => text.Length <= 40 ? text : text[..40];
}

static class ProjectFields
{
    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "kennung")) list.Add(new("kennung", "kennung", "Kennung fehlt. Zum Beispiel „Kennung: kueche“.", true));
        if (!FieldKit.Has(parameters, "name")) list.Add(new("name", "name", "Name fehlt.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ValidationFinding>();
        var id = FieldKit.Text(parameters, "kennung");
        if (id.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{1,47}$"))
            list.Add(new("kennung", "Die Kennung darf nur kleine Buchstaben, Zahlen und Bindestriche haben (2–48 Zeichen).", true));
        var name = FieldKit.Text(parameters, "name");
        if (name.Contains('\n') || name.Length > 80) list.Add(new("name", "Der Name muss in einer Zeile bleiben.", true));
        return list;
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        var id = FieldKit.Text(call.Parameters, "kennung");
        var name = FieldKit.Text(call.Parameters, "name");
        if (id.Length == 0 || name.Length == 0) return FieldKit.Fail("Kennung oder Name fehlt.");
        TrendoProject created;
        var existing = call.Projects.List().FirstOrDefault(p => p.Id == id);
        if (existing is null)
        {
            try { created = call.Projects.Create(id, name); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return FieldKit.Fail(ex.Message); }
        }
        else if (!string.Equals(existing.Name, name, StringComparison.Ordinal))
            return FieldKit.Fail("Das Projekt " + id + " gibt es schon unter einem anderen Namen.");
        else created = existing;

        var body = "Projekt " + created.Id + "\nName " + created.Name + "\n";
        var jsonPath = Path.Combine(Directory.GetParent(call.Projects.ProjectFilesRoot(created.Id))!.FullName, "project.json");
        var back = JsonSerializer.Deserialize<TrendoProject>(File.ReadAllText(jsonPath));
        var qa = new QaResult("projekt.ordner", back is not null && back.Id == id && back.Name == name, "Projektordner gelesen.", null, id, "ProjectStore");
        return FieldKit.Save(call, FieldKit.Slot("projekte", call.Version, id + ".txt"), body, "Projekt " + id + " angelegt.", qa, ["Es öffnet sich kein Fenster."]);
    }
}

static class RoomFields
{
    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "name")) list.Add(new("name", "name", "Name des Raums fehlt.", true));
        if (!FieldKit.Has(parameters, "laenge")) list.Add(new("laenge", "laenge", "Länge in Metern fehlt.", true));
        if (!FieldKit.Has(parameters, "breite")) list.Add(new("breite", "breite", "Breite in Metern fehlt.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ValidationFinding>();
        Require(parameters, "laenge", 0, 200, "Die Länge muss in Metern größer als 0 und höchstens 200 sein.", list);
        Require(parameters, "breite", 0, 200, "Die Breite muss in Metern größer als 0 und höchstens 200 sein.", list);
        if (FieldKit.Has(parameters, "hoehe")) Require(parameters, "hoehe", 0, 20, "Die Höhe muss in Metern größer als 0 und höchstens 20 sein.", list);
        if (FieldKit.Has(parameters, "tueren")) RequireInt(parameters, "tueren", list);
        if (FieldKit.Has(parameters, "fenster")) RequireInt(parameters, "fenster", list);
        if (FieldKit.Text(parameters, "name").Length > 40) list.Add(new("name", "Der Raumname ist zu lang.", true));
        return list;
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        try
        {
            var room = Build(call.Parameters);
            var svg = RoomPlanner.Svg([room]);
            var need = RoomPlanner.Need([room]);
            var preview = room.Name + " · " + Fach.F(room.Floor) + " m²";
            var qa = new QaResult("wohnung.flaeche", svg.Contains(Fach.F(room.Floor), StringComparison.Ordinal) && need.Floor == Math.Round(room.Floor, 2), "Grundriss gelesen.", "m²", Fach.F(room.Floor), "RoomPlanner");
            return FieldKit.Save(call, FieldKit.Slot("wohnung", call.Version, "grundriss.svg"), svg, preview, qa, ["Nur Fläche, Bedarf und Grundriss."]);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return FieldKit.Fail(ex.Message); }
    }

    public static Room Build(IReadOnlyDictionary<string, string> parameters)
    {
        var height = FieldKit.Has(parameters, "hoehe") ? Fach.Num(FieldKit.Text(parameters, "hoehe"), "hoehe") : 2.5;
        var doors = FieldKit.Has(parameters, "tueren") ? (int)Math.Round(Fach.Num(FieldKit.Text(parameters, "tueren"), "tueren")) : 1;
        var windows = FieldKit.Has(parameters, "fenster") ? (int)Math.Round(Fach.Num(FieldKit.Text(parameters, "fenster"), "fenster")) : 1;
        return new Room(FieldKit.Text(parameters, "name"), Fach.Num(FieldKit.Text(parameters, "laenge"), "laenge"), Fach.Num(FieldKit.Text(parameters, "breite"), "breite"), height, doors, windows);
    }

    static void Require(IReadOnlyDictionary<string, string> parameters, string key, double min, double max, string message, List<ValidationFinding> list)
    {
        if (!FieldKit.TryNum(parameters, key, out var value) || value <= min || value > max) list.Add(new(key, message, true));
    }

    static void RequireInt(IReadOnlyDictionary<string, string> parameters, string key, List<ValidationFinding> list)
    {
        if (!FieldKit.TryNum(parameters, key, out var value) || value < 0 || value > 20 || Math.Abs(value - Math.Round(value)) > 0.001)
            list.Add(new(key, "Türen und Fenster sind ganze Zahlen ab 0.", true));
    }
}

static class CircuitFields
{
    public static bool Led(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "led") || FieldKit.Has(parameters, "versorgung") || FieldKit.Has(parameters, "milliampere");

    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        if (Led(parameters))
        {
            var list = new List<ClarificationQuestion>();
            if (!FieldKit.Has(parameters, "versorgung")) list.Add(new("versorgung", "versorgung", "Versorgungsspannung in Volt fehlt.", true));
            if (!FieldKit.Has(parameters, "led")) list.Add(new("led", "led", "LED-Spannung in Volt fehlt.", true));
            if (!FieldKit.Has(parameters, "milliampere") && !FieldKit.Has(parameters, "strom")) list.Add(new("strom", "strom", "Strom in Milliampere fehlt.", true));
            return list;
        }
        var named = (FieldKit.Has(parameters, "spannung") ? 1 : 0) + (FieldKit.Has(parameters, "strom") ? 1 : 0) + (FieldKit.Has(parameters, "widerstand") ? 1 : 0);
        if (named == 0) return [new("spannung", "spannung", "Gib genau zwei Werte an: Spannung in Volt, Strom in Ampere oder Widerstand in Ohm.", true)];
        if (named == 1 && Count(parameters) == 1) return [new("widerstand", "widerstand", "Es braucht genau einen zweiten Wert. Den dritten lässt du weg.", true)];
        return [];
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "anzahl") && (!FieldKit.TryNum(parameters, "anzahl", out var count) || count < 1 || count > 20 || Math.Abs(count - Math.Round(count)) > 0.001))
            list.Add(new("anzahl", "Die Anzahl ist eine ganze Zahl von 1 bis 20.", true));
        try
        {
            if (list.Count > 0) return list;
            if (Led(parameters)) _ = LedOf(parameters);
            else if (Count(parameters) != 2) list.Add(new("spannung", "Gib genau zwei Werte an und lass den dritten weg.", true));
            else _ = OhmOf(parameters);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { list.Add(new("spannung", ex.Message, true)); }
        return list;
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        try
        {
            if (Led(call.Parameters))
            {
                var led = LedOf(call.Parameters);
                var body = "Vorwiderstand " + Electronics.Ohm(led.E12) + "\nGenau " + Electronics.Ohm(led.Exact) + "\nStrom " + Fach.F(led.Current) + " mA\nLeistung " + Fach.F(led.Power, 3) + " W\n";
                var again = LedOf(call.Parameters);
                var qa = new QaResult("schaltung.led", again.E12 == led.E12, "Vorwiderstand gelesen.", "Ω", Electronics.Ohm(led.E12), "Electronics");
                return FieldKit.Save(call, FieldKit.Slot("schaltung", call.Version, "rechnung.txt"), body, "Vorwiderstand " + Electronics.Ohm(led.E12), qa);
            }
            var law = OhmOf(call.Parameters);
            var text = "Widerstand " + Electronics.Ohm(law.R) + "\nSpannung " + Fach.F(law.U, 3) + " V\nStrom " + Fach.F(law.I, 4) + " A\nLeistung " + Fach.F(law.P, 3) + " W\n";
            var back = OhmOf(call.Parameters);
            var check = new QaResult("schaltung.ohm", back.R == law.R && back.P == law.P, "Widerstand gelesen.", "Ω", Electronics.Ohm(law.R), "Electronics");
            return FieldKit.Save(call, FieldKit.Slot("schaltung", call.Version, "rechnung.txt"), text, "Widerstand " + Electronics.Ohm(law.R), check);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return FieldKit.Fail(ex.Message); }
    }

    static int Count(IReadOnlyDictionary<string, string> parameters) =>
        (Positive(parameters, "spannung") ? 1 : 0) + (Positive(parameters, "strom") ? 1 : 0) + (Positive(parameters, "widerstand") ? 1 : 0);

    static bool Positive(IReadOnlyDictionary<string, string> parameters, string key) =>
        FieldKit.TryNum(parameters, key, out var value) && value > 0;

    static (double U, double I, double R, double P) OhmOf(IReadOnlyDictionary<string, string> parameters) =>
        Electronics.OhmsLaw(Value(parameters, "spannung"), Value(parameters, "strom"), Value(parameters, "widerstand"));

    static (double Exact, double E12, double Current, double Power) LedOf(IReadOnlyDictionary<string, string> parameters)
    {
        var count = FieldKit.Has(parameters, "anzahl") ? (int)Math.Round(Fach.Num(FieldKit.Text(parameters, "anzahl"), "anzahl")) : 1;
        var milli = FieldKit.Has(parameters, "milliampere") ? Fach.Num(FieldKit.Text(parameters, "milliampere"), "milliampere") : Fach.Num(FieldKit.Text(parameters, "strom"), "strom");
        return Electronics.LedResistor(Fach.Num(FieldKit.Text(parameters, "versorgung"), "versorgung"), Fach.Num(FieldKit.Text(parameters, "led"), "led"), milli, count);
    }

    static double Value(IReadOnlyDictionary<string, string> parameters, string key) =>
        FieldKit.TryNum(parameters, key, out var value) && value > 0 ? value : 0;
}

static class BeamFields
{
    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "laenge")) list.Add(new("laenge", "laenge", "Länge in Metern fehlt.", true));
        if (!FieldKit.Has(parameters, "last")) list.Add(new("last", "last", "Last fehlt (Einzellast in kN).", true));
        if (!FieldKit.Has(parameters, "breite")) list.Add(new("breite", "breite", "Breite des Querschnitts in Millimetern fehlt.", true));
        if (!FieldKit.Has(parameters, "hoehe")) list.Add(new("hoehe", "hoehe", "Höhe des Querschnitts in Millimetern fehlt.", true));
        if (!FieldKit.Has(parameters, "werkstoff")) list.Add(new("werkstoff", "werkstoff", "Werkstoff fehlt. Zum Beispiel Baustahl oder Holz.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ValidationFinding>();
        try { _ = Compute(parameters); }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { list.Add(new("last", ex.Message, true)); }
        return list;
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        try
        {
            var (beam, mat) = Compute(call.Parameters);
            var body = beam.Verdict + "\nSicherheit " + Fach.F(beam.Safety) + "\nDurchbiegung " + Fach.F(beam.Deflection_mm) + " mm\nSpannung " + Fach.F(beam.Stress_MPa) + " MPa\nWerkstoff " + mat.Name + "\n";
            var again = Compute(call.Parameters).Result;
            var qa = new QaResult("balken.last", again.Verdict == beam.Verdict && again.Safety == beam.Safety && again.Ok == beam.Ok, "Balkenrechnung gelesen.", null, beam.Verdict, "Engineering");
            var preview = beam.Ok ? "Hält. Sicherheit " + Fach.F(beam.Safety) + "." : beam.Verdict;
            return FieldKit.Save(call, FieldKit.Slot("balken", call.Version, "pruefung.txt"), body, preview, qa, ["Nur die eingebaute Balkenrechnung."]);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return FieldKit.Fail(ex.Message); }
    }

    public static (BeamResult Result, Material Mat) Compute(IReadOnlyDictionary<string, string> parameters)
    {
        var mat = Engineering.Mat(FieldKit.Text(parameters, "werkstoff"));
        var section = Engineering.Rect(Fach.Num(FieldKit.Text(parameters, "breite"), "breite"), Fach.Num(FieldKit.Text(parameters, "hoehe"), "hoehe"));
        var beam = Engineering.Beam(Case(FieldKit.Text(parameters, "fall")), Fach.Num(FieldKit.Text(parameters, "laenge"), "laenge"), Fach.Num(FieldKit.Text(parameters, "last"), "last"), section, mat);
        return (beam, mat);
    }

    static BeamCase Case(string text)
    {
        var t = text.ToLowerInvariant();
        if (t.Length == 0 || t.Contains("einfeld") || t.Contains("mitte")) return t.Contains("strecke") || t.Contains("gleich") ? BeamCase.EinfeldStrecke : BeamCase.EinfeldMitte;
        if (t.Contains("krag") && (t.Contains("strecke") || t.Contains("gleich"))) return BeamCase.KragarmStrecke;
        if (t.Contains("krag")) return BeamCase.KragarmEnde;
        if (t.Contains("strecke") || t.Contains("gleich")) return BeamCase.EinfeldStrecke;
        throw new ArgumentException("Den Lastfall kenne ich nicht. Zum Beispiel Einfeld oder Kragarm.");
    }
}

static class TwinFields
{
    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "anlage")) list.Add(new("anlage", "anlage", "Anlage fehlt.", true));
        if (!FieldKit.Has(parameters, "sensor")) list.Add(new("sensor", "sensor", "Sensor fehlt.", true));
        if (!FieldKit.Has(parameters, "werte")) list.Add(new("werte", "werte", "Werte fehlen. Zum Beispiel „Werte: 70, 82, 96“.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ValidationFinding>();
        foreach (var key in new[] { "warnung", "alarm" })
            if (FieldKit.Has(parameters, key) && !FieldKit.TryNum(parameters, key, out _))
                list.Add(new(key, "Bei „" + key + "“ steht keine Zahl.", true));
        try { _ = Values(parameters); }
        catch (FormatException ex) { list.Add(new("werte", ex.Message, true)); }
        return list;
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        try
        {
            var values = Values(call.Parameters);
            var asset = FieldKit.Text(call.Parameters, "anlage");
            var sensorName = FieldKit.Text(call.Parameters, "sensor");
            var unit = FieldKit.Has(call.Parameters, "einheit") ? FieldKit.Text(call.Parameters, "einheit") : "";
            var sensor = new TwinSensor
            {
                Name = sensorName,
                Unit = unit,
                WarnAbove = Optional(call.Parameters, "warnung"),
                AlarmAbove = Optional(call.Parameters, "alarm")
            };
            var start = new DateTime(2026, 10, 22, 8, 0, 0);
            var model = new TwinModel
            {
                Assets = [new TwinAsset { Name = asset, Sensors = [sensor] }],
                Readings = values.Select((v, i) => new Reading(start.AddMinutes(i), asset, sensorName, v)).ToList()
            };
            var series = TwinPlus.Series(model, asset, sensorName);
            var sb = new StringBuilder();
            sb.Append(asset).Append(" · ").Append(sensorName).Append('\n');
            foreach (var reading in series)
                sb.Append(Fach.F(reading.Value)).Append(unit.Length == 0 ? "" : " " + unit).Append(" · ").Append(Ampel(TwinHub.Level(sensor, reading.Value))).Append('\n');
            sb.Append("Kein Gerät verbunden.\n");
            var body = sb.ToString();
            var last = series[^1];
            var level = TwinHub.Level(sensor, last.Value);
            var qa = new QaResult("zwilling.reihe", series.Count == values.Length && series.Select(r => r.Value).SequenceEqual(values), "Messreihe gelesen.", unit, Ampel(level), "TwinPlus");
            var preview = asset + " · " + Fach.F(last.Value) + (unit.Length == 0 ? "" : " " + unit) + " · " + Ampel(level);
            return FieldKit.Save(call, FieldKit.Slot("zwilling", call.Version, "reihe.txt"), body, preview, qa);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { return FieldKit.Fail(ex.Message); }
    }

    static double[] Values(IReadOnlyDictionary<string, string> parameters) => FieldKit.Numbers(FieldKit.Text(parameters, "werte"), "Werte");

    static double Optional(IReadOnlyDictionary<string, string> parameters, string key) =>
        FieldKit.TryNum(parameters, key, out var value) ? value : double.NaN;

    static string Ampel(TwinLevel level) => level switch
    {
        TwinLevel.Ok => "in Ordnung",
        TwinLevel.Warnung => "Warnung",
        TwinLevel.Alarm => "Alarm",
        _ => "unbekannt"
    };
}

static class RouteFields
{
    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        if (FieldKit.Has(parameters, "datei")) return [];
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "von")) list.Add(new("von", "von", "Startpunkt fehlt. Zum Beispiel „Von: 52.52, 13.405“.", true));
        if (!FieldKit.Has(parameters, "nach")) list.Add(new("nach", "nach", "Zielpunkt fehlt.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ValidationFinding>();
        var file = FieldKit.Text(parameters, "datei");
        if (file.Length > 0 && (Path.IsPathRooted(file) || file.Contains("..", StringComparison.Ordinal)))
            list.Add(new("datei", "Die Datei muss im Projekt liegen.", true));
        try
        {
            if (file.Length == 0)
            {
                _ = Geo.ParseCoord(FieldKit.Text(parameters, "von"));
                _ = Geo.ParseCoord(FieldKit.Text(parameters, "nach"));
            }
        }
        catch (FormatException ex) { list.Add(new("von", ex.Message, true)); }
        return list;
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        try
        {
            double km;
            int points;
            var file = FieldKit.Text(call.Parameters, "datei").Replace('\\', '/');
            if (file.Length > 0)
            {
                var full = call.Projects.ResolveInProject(call.ProjectId, file);
                if (new FileInfo(full).LinkTarget is not null) return FieldKit.Fail("Die Datei ist ein Verweis nach außen und wurde nicht gelesen.");
                if (!File.Exists(full)) return FieldKit.Fail("Die Datei liegt nicht im Projekt: " + file);
                var (track, wpt) = Geo.ReadGpx(full);
                var path = track.Count > 0 ? track : wpt;
                var stats = Geo.Analyse(path);
                km = stats.DistanceKm;
                points = stats.Points;
            }
            else
            {
                var from = Geo.ParseCoord(FieldKit.Text(call.Parameters, "von"));
                var to = Geo.ParseCoord(FieldKit.Text(call.Parameters, "nach"));
                km = Geo.Haversine(new GeoPoint(from.Lat, from.Lon), new GeoPoint(to.Lat, to.Lon));
                points = 2;
            }
            var body = "Strecke " + Fach.F(km, 3) + " km\nPunkte " + points + "\nKeine Karte geladen.\n";
            var preview = "Strecke " + Fach.F(km, 3) + " km";
            var qa = new QaResult("route.strecke", points >= 2 && km >= 0, "Strecke gelesen.", "km", Fach.F(km, 3), "Geo");
            return FieldKit.Save(call, FieldKit.Slot("route", call.Version, "strecke.txt"), body, preview, qa);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or IOException or ProjectIsolationException)
        { return FieldKit.Fail(ex.Message); }
    }
}

static class CncFields
{
    public static bool WantsMachine(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Yes(parameters, "maschine") || FieldKit.Yes(parameters, "starten");

    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "material")) list.Add(new("material", "material", "Werkstoff fehlt. Zum Beispiel Aluminium oder Holz.", true));
        if (!FieldKit.Has(parameters, "durchmesser")) list.Add(new("durchmesser", "durchmesser", "Werkzeugdurchmesser in Millimetern fehlt.", true));
        if (!FieldKit.Has(parameters, "schneiden")) list.Add(new("schneiden", "schneiden", "Schneidenzahl fehlt.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        try { _ = Feeds(parameters); return []; }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return [new("material", ex.Message, true)]; }
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        try
        {
            var feed = Feeds(call.Parameters);
            var body = "Drehzahl " + feed.Rpm.ToString("0", CultureInfo.InvariantCulture) + "\nVorschub " + feed.FeedMmMin.ToString("0", CultureInfo.InvariantCulture) + "\nDie Maschine ist nicht gelaufen.\n";
            var again = Feeds(call.Parameters);
            var qa = new QaResult("fraese.drehzahl", again.Rpm == feed.Rpm && again.FeedMmMin == feed.FeedMmMin, "Drehzahl gelesen.", "1/min", feed.Rpm.ToString("0", CultureInfo.InvariantCulture), "Cnc");
            var preview = "Drehzahl " + feed.Rpm.ToString("0", CultureInfo.InvariantCulture) + " · Maschine steht";
            return FieldKit.Save(call, FieldKit.Slot("fraese", call.Version, "drehzahl.txt"), body, preview, qa);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return FieldKit.Fail(ex.Message); }
    }

    static (double Rpm, double FeedMmMin, double Vc, double Fz, bool Limited) Feeds(IReadOnlyDictionary<string, string> parameters)
    {
        var flutes = (int)Math.Round(Fach.Num(FieldKit.Text(parameters, "schneiden"), "schneiden"));
        var max = FieldKit.Has(parameters, "grenze") ? Fach.Num(FieldKit.Text(parameters, "grenze"), "grenze") : 24000;
        return Cnc.Feeds(FieldKit.Text(parameters, "material"), Fach.Num(FieldKit.Text(parameters, "durchmesser"), "durchmesser"), flutes, max);
    }
}

static class VehicleFields
{
    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "kilometer")) list.Add(new("kilometer", "kilometer", "Jahreskilometer fehlen.", true));
        if (!FieldKit.Has(parameters, "verbrauch")) list.Add(new("verbrauch", "verbrauch", "Verbrauch in Liter je 100 km fehlt.", true));
        if (!FieldKit.Has(parameters, "preis")) list.Add(new("preis", "preis", "Preis je Liter fehlt.", true));
        return list;
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        foreach (var key in new[] { "kilometer", "verbrauch", "preis", "versicherung", "steuer", "wartung" })
            if (FieldKit.Has(parameters, key) && (!FieldKit.TryNum(parameters, key, out var value) || value < 0))
                return [new(key, "„" + key + "“ muss eine Zahl ab 0 sein.", true)];
        try { _ = Cost(parameters); return []; }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return [new("kilometer", ex.Message, true)]; }
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        try
        {
            var cost = Cost(call.Parameters);
            var body = "Im Jahr " + Money(cost.Year) + " €\nIm Monat " + Money(cost.Month) + " €\nJe Kilometer " + cost.PerKm.ToString("N3", Fach.De) + " €\nNicht gebucht.\n";
            var again = Cost(call.Parameters);
            var qa = new QaResult("fahrt.kosten", again.Year == cost.Year && again.PerKm == cost.PerKm, "Kosten gelesen.", "EUR", Money(cost.Year), "VehiclePlus");
            return FieldKit.Save(call, FieldKit.Slot("fahrt", call.Version, "kosten.txt"), body, "Im Jahr " + Money(cost.Year) + " €", qa, ["Nicht gebucht."]);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return FieldKit.Fail(ex.Message); }
    }

    static (double Year, double Month, double PerKm) Cost(IReadOnlyDictionary<string, string> parameters) =>
        VehiclePlus.YearCost(
            Fach.Num(FieldKit.Text(parameters, "kilometer"), "kilometer"),
            Fach.Num(FieldKit.Text(parameters, "verbrauch"), "verbrauch"),
            Fach.Num(FieldKit.Text(parameters, "preis"), "preis"),
            Optional(parameters, "versicherung"),
            Optional(parameters, "steuer"),
            Optional(parameters, "wartung"));

    static double Optional(IReadOnlyDictionary<string, string> parameters, string key) =>
        FieldKit.TryNum(parameters, key, out var value) ? value : 0;

    static string Money(double value) => value.ToString("N2", Fach.De);
}

static class StatsFields
{
    public static IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters)
    {
        if (FieldKit.Has(parameters, "x-werte") && FieldKit.Has(parameters, "y-werte")) return [];
        if (FieldKit.Has(parameters, "werte")) return [];
        if (FieldKit.Has(parameters, "x-werte") || FieldKit.Has(parameters, "y-werte"))
            return [new("y-werte", "y-werte", "Für eine Gerade brauchst du X-Werte und Y-Werte.", true)];
        return [new("werte", "werte", "Werte fehlen. Zum Beispiel „Werte: 2, 4, 4, 6“.", true)];
    }

    public static IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        try
        {
            if (FieldKit.Has(parameters, "x-werte"))
            {
                var x = FieldKit.Numbers(FieldKit.Text(parameters, "x-werte"), "X-Werte");
                var y = FieldKit.Numbers(FieldKit.Text(parameters, "y-werte"), "Y-Werte");
                if (x.Length != y.Length) return [new("y-werte", "X-Werte und Y-Werte müssen gleich viele Zahlen haben.", true)];
                _ = Stats.Linear(x, y);
            }
            else _ = Stats.Describe("werte", FieldKit.Numbers(FieldKit.Text(parameters, "werte"), "Werte"));
            return [];
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return [new("werte", ex.Message, true)]; }
    }

    public static AdapterOutcome Write(AdapterCall call)
    {
        try
        {
            if (FieldKit.Has(call.Parameters, "x-werte"))
            {
                var x = FieldKit.Numbers(FieldKit.Text(call.Parameters, "x-werte"), "X-Werte");
                var y = FieldKit.Numbers(FieldKit.Text(call.Parameters, "y-werte"), "Y-Werte");
                var line = Stats.Linear(x, y);
                var body = "Steigung " + Fach.F(line.Slope, 4) + "\nAchsenabschnitt " + Fach.F(line.Intercept, 4) + "\nPaare " + line.N + "\n";
                var again = Stats.Linear(x, y);
                var qa = new QaResult("messung.gerade", again.Slope == line.Slope && again.N == line.N, "Gerade gelesen.", null, Fach.F(line.Slope, 4), "Stats");
                return FieldKit.Save(call, FieldKit.Slot("messung", call.Version, "auswertung.txt"), body, "Steigung " + Fach.F(line.Slope, 4), qa);
            }
            var values = FieldKit.Numbers(FieldKit.Text(call.Parameters, "werte"), "Werte");
            var stats = Stats.Describe("werte", values);
            var text = "Mittelwert " + Fach.F(stats.Mean) + "\nMedian " + Fach.F(stats.Median) + "\nStreuung " + Fach.F(stats.StdDev) + "\nAnzahl " + stats.N + "\n";
            var back = Stats.Describe("werte", values);
            var check = new QaResult("messung.mittel", back.Mean == stats.Mean && back.N == stats.N, "Mittelwert gelesen.", null, Fach.F(stats.Mean), "Stats");
            return FieldKit.Save(call, FieldKit.Slot("messung", call.Version, "auswertung.txt"), text, "Mittelwert " + Fach.F(stats.Mean), check);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return FieldKit.Fail(ex.Message); }
    }
}
