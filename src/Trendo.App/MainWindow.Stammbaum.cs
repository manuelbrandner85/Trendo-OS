using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0788 (Etappe 0.9.115 „Wissen &amp; Lernen E“): Studio „Stammbaum“ (STUDIO-FAMILY-01).
/// Personen, Familien und Ereignisse (Geburt, Taufe, Heirat, Tod …) mit Orten und Quellen; Baum der Vorfahren und der Nachkommen mit Zoom und Verschieben;
/// GEDCOM 5.5.1 einlesen (UTF-8, UTF-16, ANSEL, ANSI) und schreiben (UTF-8) – unbekannte Angaben bleiben erhalten; Prüfung auf Widersprüche;
/// Bericht als PDF, DOCX und ODT; Baum als SVG, PNG und PDF; lebende Personen in allen Ausgaben auf Wunsch ausgeblendet (Standard an).
/// Die KI fügt Personen aus einem Text ein (Vorschau → „Ja, behalten“ → ein Rückgängig-Schritt); vorhandene Personen und Quellen darf sie nicht ändern.</summary>
public partial class MainWindow
{
    public const string FamKey = "stammbaum-programm";
    public const string FamStudio = "STUDIO-FAMILY-01";

    readonly Dictionary<string, string?> _famSel = [];
    readonly Dictionary<string, string> _famView = [];
    readonly Dictionary<string, (double Z, double X, double Y)> _famZoom = [];
    readonly Dictionary<string, string> _famSearch = [];
    string _famPaste = "";
    /// <summary>Für Tests: fester Tag für „lebt noch?“ (sonst heute).</summary>
    public DateTime? FamTodayOverride { get; set; }
    DateTime FamToday => FamTodayOverride ?? DateTime.Today;

    public static FamilyTree FamData(ProgramDocument d) => FamilyKit.FromJson(d.Data);

    static void FamSave(ProgramDocument d, FamilyTree t, string label) => d.Edit(label, x =>
    {
        var o = FamilyKit.ToJson(t);
        foreach (var k in o.Select(kv => kv.Key).ToList()) { var v = o[k]; o.Remove(k); x[k] = v; }
    });

    static JsonObject FamDoc(string title = "") => FamilyKit.ToJson(new FamilyTree { Title = title });

    static JsonObject FamSample()
    {
        var t = new FamilyTree { Title = "Familie Muster" };
        FamPerson P(string id, string g, string s, string sex, string birth, string bplace, string death = "", string dplace = "")
        {
            var p = new FamPerson { Id = id, Sex = sex }; FamilyKit.SetName(p, g, s);
            p.Events.Add(new FamEvent { Tag = "BIRT", Date = birth, Place = bplace });
            if (death.Length > 0) p.Events.Add(new FamEvent { Tag = "DEAT", Date = death, Place = dplace });
            t.Persons.Add(p); return p;
        }
        P("I1", "Karl", "Muster", "M", "12 MAR 1890", "Musterstadt", "4 JUN 1961", "Musterstadt").Events.Add(new FamEvent { Tag = "OCCU", Value = "Schreiner" });
        P("I2", "Luise", "Beispiel", "F", "ABT 1893", "Beispieldorf", "1970", "Musterstadt");
        P("I3", "Hans", "Muster", "M", "2 FEB 1921", "Musterstadt", "1999", "Neustadt");
        P("I4", "Grete", "Muster", "F", "1924", "Musterstadt", "2008", "Musterstadt");
        P("I5", "Erika", "Muster", "F", "15 AUG 1952", "Neustadt");
        P("I6", "Paul", "Muster", "M", "1955", "Neustadt");
        P("I7", "Marie", "Sommer", "F", "1925", "Talheim", "2011", "Neustadt");
        t.Sources.Add(new FamSource { Id = "S1", Title = "Kirchenbuch Musterstadt 1880–1930", Author = "Pfarramt Musterstadt" });
        t.P("I1")!.Ev("BIRT")!.Cites.Add(new FamCite { Source = "S1", Page = "S. 42, Nr. 7" });
        t.Families.Add(new FamFamily { Id = "F1", Husband = "I1", Wife = "I2", Children = ["I3", "I4"], Events = [new FamEvent { Tag = "MARR", Date = "5 MAY 1919", Place = "Musterstadt" }] });
        t.Families.Add(new FamFamily { Id = "F2", Husband = "I3", Wife = "I7", Children = ["I5", "I6"], Events = [new FamEvent { Tag = "MARR", Date = "1950", Place = "Neustadt" }] });
        t.Root = "I5";
        return FamilyKit.ToJson(t);
    }

    /// <summary>Gewählte Person; solange nichts gewählt wurde, die Person in der Mitte des Baums.</summary>
    public string? FamSelected(ProgramDocument d)
    {
        var t = FamData(d);
        if (!_famSel.TryGetValue(d.Id, out var id)) return t.P(t.Root)?.Id ?? t.Persons.FirstOrDefault()?.Id;
        return id is not null && t.P(id) is not null ? id : null;
    }
    public void FamSelect(ProgramDocument d, string? id) => _famSel[d.Id] = id;
    public string FamViewOf(ProgramDocument d) => _famView.GetValueOrDefault(d.Id, "vorfahren");
    public void FamSetView(ProgramDocument d, string view) { _famView[d.Id] = view; _famZoom.Remove(d.Id); }
    string? FamRootOf(FamilyTree t, ProgramDocument d) => t.P(t.Root)?.Id ?? FamSelected(d) ?? t.Persons.FirstOrDefault()?.Id;

    // ---------------- Bearbeiten ----------------

    static FamPerson FamNewPerson(FamilyTree t, string given, string surname, string sex)
    {
        if (t.Persons.Count >= FamilyKit.MaxPersons) throw new InvalidOperationException($"Höchstens {FamilyKit.MaxPersons} Personen.");
        var p = new FamPerson { Id = t.NewId("I"), Sex = sex is "M" or "F" ? sex : "U" };
        FamilyKit.SetName(p, KreativSafe.Cut(given, 120), KreativSafe.Cut(surname, 120));
        t.Persons.Add(p);
        return p;
    }

    public FamPerson FamAddPerson(ProgramDocument d, string given = "", string surname = "", string sex = "U")
    {
        var t = FamData(d);
        var p = FamNewPerson(t, given, surname, sex);
        t.Root ??= p.Id;
        FamSave(d, t, "Neue Person");
        _famSel[d.Id] = p.Id; _famZoom.Remove(d.Id);
        _progMsg[FamKey] = "Person angelegt – Name und Daten oben eintragen.";
        return p;
    }

    /// <summary>Verwandte Person anlegen: „vater“, „mutter“, „partner“ oder „kind“ – mit der passenden Familie (vorhandene wird genutzt).</summary>
    public FamPerson FamAddRelative(ProgramDocument d, string personId, string kind)
    {
        var t = FamData(d); var p = t.P(personId) ?? throw new InvalidOperationException("Wähle zuerst eine Person.");
        FamPerson n;
        switch (kind)
        {
            case "vater" or "mutter":
            {
                var f = t.ParentFamilies(p.Id).FirstOrDefault();
                if (f is not null && (kind == "vater" ? f.Husband : f.Wife) is not null) throw new InvalidOperationException($"{p.Name} hat schon {(kind == "vater" ? "einen Vater" : "eine Mutter")}.");
                n = FamNewPerson(t, "", p.Surname.Length > 0 && kind == "vater" ? p.Surname : "", kind == "vater" ? "M" : "F");
                if (f is null) { f = new FamFamily { Id = t.NewId("F"), Children = [p.Id] }; t.Families.Add(f); }
                if (kind == "vater") f.Husband = n.Id; else f.Wife = n.Id;
                break;
            }
            case "partner":
            {
                n = FamNewPerson(t, "", "", p.Sex == "M" ? "F" : p.Sex == "F" ? "M" : "U");
                var f = t.OwnFamilies(p.Id).FirstOrDefault(x => x.Husband is null || x.Wife is null) ?? new FamFamily { Id = t.NewId("F") };
                if (!t.Families.Contains(f)) t.Families.Add(f);
                if (f.Husband == p.Id) f.Wife = n.Id; else if (f.Wife == p.Id) f.Husband = n.Id;
                else if (p.Sex == "F") { f.Wife = p.Id; f.Husband = n.Id; } else { f.Husband = p.Id; f.Wife = n.Id; }
                break;
            }
            case "kind":
            {
                n = FamNewPerson(t, "", p.Sex == "M" ? p.Surname : "", "U");
                var f = t.OwnFamilies(p.Id).FirstOrDefault();
                if (f is null) { f = new FamFamily { Id = t.NewId("F") }; if (p.Sex == "F") f.Wife = p.Id; else f.Husband = p.Id; t.Families.Add(f); }
                f.Children.Add(n.Id);
                break;
            }
            default: throw new ArgumentException("Unbekannte Verwandtschaft.");
        }
        FamSave(d, t, kind switch { "vater" => "Vater angelegt", "mutter" => "Mutter angelegt", "partner" => "Partner angelegt", _ => "Kind angelegt" });
        _famSel[d.Id] = n.Id; _famZoom.Remove(d.Id);
        _progMsg[FamKey] = $"{kind switch { "vater" => "Vater", "mutter" => "Mutter", "partner" => "Partner", _ => "Kind" }} von {p.Name} angelegt – Name oben eintragen.";
        return n;
    }

    /// <summary>Feld der Person setzen: given, surname, sex, birth, birthPlace, death, deathPlace, note.</summary>
    public void FamSet(ProgramDocument d, string id, string field, string value)
    {
        var t = FamData(d); var p = t.P(id) ?? throw new InvalidOperationException("Diese Person gibt es nicht mehr.");
        var v = (value ?? "").Trim();
        FamEvent Ev(string tag) { var e = p.Events.FirstOrDefault(x => x.Tag == tag); if (e is null) { e = new FamEvent { Tag = tag }; p.Events.Insert(tag == "BIRT" ? 0 : p.Events.Count, e); } return e; }
        var before = FamilyKit.PersonJson(p).ToJsonString();
        switch (field)
        {
            case "given": FamilyKit.SetName(p, KreativSafe.Cut(v, 120), p.Surname); break;
            case "surname": FamilyKit.SetName(p, p.Given, KreativSafe.Cut(v, 120)); break;
            case "sex": p.Sex = v is "M" or "F" ? v : "U"; break;
            case "note": p.Note = KreativSafe.Cut(value ?? "", 20000); break;
            case "birth": Ev("BIRT").Date = GenDate.FromInput(v); break;
            case "birthPlace": Ev("BIRT").Place = KreativSafe.Cut(v, 300); break;
            case "death": Ev("DEAT").Date = GenDate.FromInput(v); break;
            case "deathPlace": Ev("DEAT").Place = KreativSafe.Cut(v, 300); break;
            default: throw new ArgumentException("Unbekanntes Feld.");
        }
        p.Events.RemoveAll(e => e.Tag is "BIRT" or "DEAT" && e.Date.Length == 0 && e.Place.Length == 0 && e.Value.Length == 0 && e.Cites.Count == 0 && e.Extra.Count == 0 && e.Note.Length == 0 && e.Lat.Length == 0 && e.PlaceExtra.Count == 0 && field is "birth" or "birthPlace" or "death" or "deathPlace");
        if (FamilyKit.PersonJson(p).ToJsonString() == before) return;
        FamSave(d, t, field switch { "given" => "Vorname", "surname" => "Nachname", "sex" => "Geschlecht", "note" => "Notiz", "birth" => "Geburtsdatum", "birthPlace" => "Geburtsort", "death" => "Sterbedatum", _ => "Sterbeort" });
    }

    public void FamAddEvent(ProgramDocument d, string id, string tag)
    {
        var t = FamData(d); var p = t.P(id) ?? throw new InvalidOperationException("Wähle zuerst eine Person.");
        if (!FamilyKit.PersonEvents.Any(e => e.Tag == tag)) throw new ArgumentException("Unbekanntes Ereignis.");
        p.Events.Add(new FamEvent { Tag = tag });
        FamSave(d, t, FamilyKit.EventName(tag) + " hinzugefügt");
    }

    /// <summary>Feld eines Ereignisses (Nummer in der Liste der Person; <paramref name="family"/> = Familie statt Person): date, place, value, type, note, lat, long.</summary>
    public void FamSetEvent(ProgramDocument d, string id, int index, string field, string value, bool family = false)
    {
        var t = FamData(d);
        var list = family ? (t.F(id) ?? throw new InvalidOperationException("Diese Familie gibt es nicht mehr.")).Events : (t.P(id) ?? throw new InvalidOperationException("Diese Person gibt es nicht mehr.")).Events;
        if (index < 0 || index >= list.Count) throw new InvalidOperationException("Dieses Ereignis gibt es nicht mehr.");
        var e = list[index]; var v = (value ?? "").Trim(); var old = FamilyKit.EventJson(e).ToJsonString();
        switch (field)
        {
            case "date": e.Date = GenDate.FromInput(v); break;
            case "place": e.Place = KreativSafe.Cut(v, 300); break;
            case "value": e.Value = KreativSafe.Cut(v, 300); break;
            case "type": e.Type = KreativSafe.Cut(v, 120); break;
            case "note": e.Note = KreativSafe.Cut(value ?? "", 20000); break;
            case "lat" or "long": var c = FamCoord(v, field == "lat"); if (field == "lat") e.Lat = c; else e.Long = c; break;
            default: throw new ArgumentException("Unbekanntes Feld.");
        }
        if (FamilyKit.EventJson(e).ToJsonString() == old) return;
        FamSave(d, t, FamilyKit.EventName(e.Tag) + ": " + field switch { "date" => "Datum", "place" => "Ort", "note" => "Notiz", "lat" or "long" => "Koordinaten", _ => "Angabe" });
    }

    /// <summary>Koordinate als GEDCOM („N50.9375“, „E6.9603“) – Eingabe auch als Dezimalzahl mit Komma oder Punkt.</summary>
    public static string FamCoord(string v, bool lat)
    {
        v = v.Trim(); if (v.Length == 0) return "";
        var pos = lat ? 'N' : 'E'; var neg = lat ? 'S' : 'W';
        var sign = 1.0; var num = v.ToUpperInvariant();
        if (num[0] == pos || num[0] == neg) { sign = num[0] == neg ? -1 : 1; num = num[1..]; }
        if (!double.TryParse(num.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) || Math.Abs(x) > (lat ? 90 : 180)) throw new ArgumentException(lat ? "Breite als Zahl zwischen −90 und 90, z. B. 50,94" : "Länge als Zahl zwischen −180 und 180, z. B. 6,96");
        x *= sign;
        return (x < 0 ? neg : pos) + Math.Abs(x).ToString("0.####", CultureInfo.InvariantCulture);
    }

    public void FamRemoveEvent(ProgramDocument d, string id, int index)
    {
        var t = FamData(d); var p = t.P(id) ?? throw new InvalidOperationException("Diese Person gibt es nicht mehr.");
        if (index < 0 || index >= p.Events.Count) return;
        var tag = p.Events[index].Tag; p.Events.RemoveAt(index);
        FamSave(d, t, FamilyKit.EventName(tag) + " entfernt");
    }

    /// <summary>Heirat (Datum, Ort) der Familie setzen – legt das Ereignis an, falls es fehlt.</summary>
    public void FamSetMarriage(ProgramDocument d, string familyId, string field, string value)
    {
        var t = FamData(d); var f = t.F(familyId) ?? throw new InvalidOperationException("Diese Familie gibt es nicht mehr.");
        var m = f.Marriage; if (m is null) { m = new FamEvent { Tag = "MARR" }; f.Events.Insert(0, m); }
        var old = FamilyKit.EventJson(m).ToJsonString();
        if (field == "date") m.Date = GenDate.FromInput(value); else m.Place = KreativSafe.Cut((value ?? "").Trim(), 300);
        if (m.Date.Length == 0 && m.Place.Length == 0 && m.Cites.Count == 0 && m.Extra.Count == 0) f.Events.Remove(m);
        if (f.Marriage is { } mm && FamilyKit.EventJson(mm).ToJsonString() == old) return;
        FamSave(d, t, field == "date" ? "Heiratsdatum" : "Heiratsort");
    }

    public void FamRemove(ProgramDocument d, string id)
    {
        var t = FamData(d); var p = t.P(id); if (p is null) return;
        t.Persons.Remove(p);
        foreach (var f in t.Families) { if (f.Husband == id) f.Husband = null; if (f.Wife == id) f.Wife = null; f.Children.Remove(id); f.ChildExtra.RemoveAll(c => c.Person == id); }
        t.Families.RemoveAll(f => f.Husband is null && f.Wife is null && f.Children.Count == 0 && f.Events.Count == 0 && f.Extra.Count == 0);
        if (t.Root == id) t.Root = t.Persons.FirstOrDefault()?.Id;
        FamSave(d, t, "Person gelöscht");
        _famSel[d.Id] = null;
        _progMsg[FamKey] = $"„{p.Name}“ gelöscht.";
    }

    public void FamSetRoot(ProgramDocument d, string id)
    {
        var t = FamData(d); if (t.P(id) is not { } p || t.Root == id) return;
        t.Root = id; FamSave(d, t, "Mittelpunkt"); _famSel[d.Id] = id; _famZoom.Remove(d.Id);
        _progMsg[FamKey] = $"{p.Name} steht jetzt in der Mitte des Baums.";
    }

    public FamSource FamAddSource(ProgramDocument d, string title = "", string author = "")
    {
        var t = FamData(d);
        var s = new FamSource { Id = t.NewId("S"), Title = KreativSafe.Cut(title.Trim().Length > 0 ? title.Trim() : "Neue Quelle", 300), Author = KreativSafe.Cut(author.Trim(), 200) };
        t.Sources.Add(s); FamSave(d, t, "Neue Quelle");
        _progMsg[FamKey] = "Quelle angelegt – Titel, Verfasser und Veröffentlichung eintragen.";
        return s;
    }

    public void FamSetSource(ProgramDocument d, string id, string field, string value)
    {
        var t = FamData(d); var s = t.Sources.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Diese Quelle gibt es nicht mehr.");
        var v = KreativSafe.Cut((value ?? "").Trim(), 2000);
        switch (field) { case "title": s.Title = v; break; case "author": s.Author = v; break; case "publication": s.Publication = v; break; case "text": s.Text = v; break; default: throw new ArgumentException("Unbekanntes Feld."); }
        FamSave(d, t, "Quelle geändert");
    }

    /// <summary>Quelle an ein Ereignis der Person hängen (Nummer in der Liste; −1 = an die Person selbst), mit Fundstelle.</summary>
    public void FamCite(ProgramDocument d, string personId, int eventIndex, string sourceId, string page = "")
    {
        var t = FamData(d); var p = t.P(personId) ?? throw new InvalidOperationException("Wähle zuerst eine Person.");
        if (t.Sources.All(s => s.Id != sourceId)) throw new InvalidOperationException("Diese Quelle steht nicht in der Liste.");
        var list = eventIndex < 0 ? p.Cites : eventIndex < p.Events.Count ? p.Events[eventIndex].Cites : throw new InvalidOperationException("Dieses Ereignis gibt es nicht mehr.");
        list.Add(new FamCite { Source = sourceId, Page = KreativSafe.Cut(page.Trim(), 300) });
        FamSave(d, t, "Quelle angegeben");
        _progMsg[FamKey] = "Quelle angegeben.";
    }

    /// <summary>Ort überall umbenennen (alle Ereignisse mit diesem Ort) – z. B. Schreibweisen vereinheitlichen.</summary>
    public int FamRenamePlace(ProgramDocument d, string from, string to)
    {
        to = KreativSafe.Cut((to ?? "").Trim(), 300);
        if (to.Length == 0) throw new ArgumentException("Neuen Ortsnamen eintragen.");
        var t = FamData(d); var n = 0;
        foreach (var e in t.Persons.SelectMany(p => p.Events).Concat(t.Families.SelectMany(f => f.Events)))
            if (string.Equals(e.Place.Trim(), from.Trim(), StringComparison.OrdinalIgnoreCase) && e.Place != to) { e.Place = to; n++; }
        if (n > 0) FamSave(d, t, "Ort umbenannt");
        _progMsg[FamKey] = $"{n} Ereignis(se) auf „{to}“ umgestellt.";
        return n;
    }

    /// <summary>Koordinaten für alle Ereignisse an einem Ort setzen.</summary>
    public void FamPlaceCoords(ProgramDocument d, string place, string lat, string lon)
    {
        var la = FamCoord(lat, true); var lo = FamCoord(lon, false);
        var t = FamData(d); var n = 0;
        foreach (var e in t.Persons.SelectMany(p => p.Events).Concat(t.Families.SelectMany(f => f.Events)))
            if (string.Equals(e.Place.Trim(), place.Trim(), StringComparison.OrdinalIgnoreCase)) { e.Lat = la; e.Long = lo; n++; }
        if (n > 0) FamSave(d, t, "Koordinaten");
    }

    public void FamSetOption(ProgramDocument d, string field, string value)
    {
        var t = FamData(d);
        switch (field)
        {
            case "title": if (t.Title == value.Trim()) return; t.Title = KreativSafe.Cut(value.Trim(), 200); break;
            case "hideLiving": var b = value == "true"; if (t.HideLiving == b) return; t.HideLiving = b; break;
            case "gens": var g = Math.Clamp(int.TryParse(value, out var x) ? x : 4, 2, 6); if (t.Generations == g) return; t.Generations = g; _famZoom.Remove(d.Id); break;
            default: throw new ArgumentException("Unbekannte Einstellung.");
        }
        FamSave(d, t, field switch { "title" => "Titel", "hideLiving" => "Lebende ausblenden", _ => "Generationen" });
    }

    /// <summary>Zeilen „Name | Geschlecht | Geburt | Geburtsort | Tod | Sterbeort | Vater | Mutter“ und „Ehe: A + B | Datum | Ort“ übernehmen.</summary>
    public (int Persons, int Links, List<string> Problems) FamAddLines(ProgramDocument d, string text, string label = "Aus Text eingefügt")
    {
        var (ps, ms, bad) = FamilyKit.ParseLines(text);
        var t = FamData(d);
        var (np, nl) = FamilyKit.AddLines(t, ps, ms);
        t.Root ??= t.Persons.FirstOrDefault()?.Id;
        if (np + nl > 0) { FamSave(d, t, label); _famZoom.Remove(d.Id); }
        _progMsg[FamKey] = np + nl == 0 && bad.Count == 0 ? "Nichts Neues (alle Personen und Verknüpfungen gibt es schon)." : $"{np} Personen und {nl} Verknüpfungen eingefügt" + (bad.Count > 0 ? $", {bad.Count} Zeile(n) nicht lesbar: {bad[0]}." : ".");
        return (np, nl, bad);
    }

    /// <summary>GEDCOM-Datei einlesen. Ist der Stammbaum leer, kommt sie in dieses Dokument (ein Rückgängig-Schritt); sonst in ein neues Dokument (Reiter).</summary>
    public ProgramDocument FamImport(ProgramDocument d, string file)
    {
        if (!KreativSafe.LocalFile(file, Gedcom.MaxBytes)) throw new InvalidOperationException($"Wähle eine GEDCOM-Datei (.ged) auf diesem Rechner (höchstens {Gedcom.MaxBytes / 1_000_000} MB).");
        var t = FamilyKit.FromGedcom(File.ReadAllBytes(file));
        if (t.Persons.Count == 0 && t.Families.Count == 0) throw new InvalidDataException($"In „{Path.GetFileName(file)}“ stehen keine Personen. {t.ImportNote}");
        if (t.Title.Length == 0) t.Title = Path.GetFileNameWithoutExtension(file);
        t.Root = t.Persons[0].Id;
        var cur = FamData(d);
        ProgramDocument target;
        if (cur.Persons.Count == 0) { t.HideLiving = cur.HideLiving; FamSave(d, t, "Eingelesen: " + Path.GetFileName(file)); target = d; }
        else
        {
            target = new ProgramDocument(FamStudio, Path.GetFileNameWithoutExtension(file), FamilyKit.ToJson(t));
            var l = ProgramDocs(FamKey); l.Add(target); _progActive[FamKey] = l.Count - 1;
        }
        _famSel[target.Id] = t.Root; _famZoom.Remove(target.Id);
        _progMsg[FamKey] = $"„{Path.GetFileName(file)}“ eingelesen. {t.ImportNote}";
        _core.Audit.Append("app", "stammbaum.import", $"{t.Persons.Count} Personen, {t.Families.Count} Familien");
        return target;
    }

    async Task FamPickImport(ProgramDocument d)
    {
        if (await PickMediaFile("fam-import", "GEDCOM-Datei", ["*.ged", "*.gedcom", "*.txt"]) is { } f) ProgramRun(FamKey, () => FamImport(d, f));
    }

    // ---------------- Ausgabe ----------------

    TimelineKit.Scene FamScene(ProgramDocument d, bool paper)
    {
        var t = FamData(d);
        if (t.Persons.Count == 0) throw new InvalidOperationException("Der Stammbaum ist noch leer.");
        if (paper && t.HideLiving) { var root = FamRootOf(t, d); t = FamilyKit.Private(t, FamToday); t.Root = root; }
        var rootId = FamRootOf(t, d);
        return FamViewOf(d) == "nachkommen" ? FamilyKit.Descendants(t, rootId, t.Generations, paper ? null : FamSelected(d), paper) : FamilyKit.Ancestors(t, rootId, t.Generations, paper ? null : FamSelected(d), paper);
    }

    string FamWriteText(string file, string text, bool bom = false) { File.WriteAllText(file, text, new UTF8Encoding(bom)); return file; }

    string FamWriteReport(ProgramDocument d, string file, string ext)
    {
        var t = FamData(d);
        if (t.Persons.Count == 0) throw new InvalidOperationException("Der Stammbaum ist noch leer.");
        var doc = FamilyKit.BuildReport(t, FamToday);
        switch (ext) { case ".docx": DocxFormat.Write(doc, file); break; case ".odt": OdtFormat.Write(doc, file); break; default: TextPdf.Export(doc, file); break; }
        return file;
    }

    string FamWriteGedcom(ProgramDocument d, string file)
    {
        var t = FamData(d);
        if (t.Persons.Count == 0) throw new InvalidOperationException("Der Stammbaum ist noch leer.");
        return FamWriteText(file, FamilyKit.ToGedcom(t, now: FamToday));
    }

    // ---------------- KI-Fachaktion ----------------

    ProgramKiAction FamKiPersons => new(new AssistAction("stammbaum.personen", "Personen aus Text einfügen", ["personen", "person", "familie", "eltern", "kinder", "stammbaum", "einfügen", "einfuegen", "aus", "text", "vorfahren", "verwandte"],
        [new AssistParam("personen", "Eine Zeile je Person: „Name | Geschlecht (m/w) | Geburt | Geburtsort | Tod | Sterbeort | Vater | Mutter“, Ehen als „Ehe: Name + Name | Datum | Ort“", Max: 50_000, Multiline: true)]),
        (d, a) =>
        {
            var lines = a.GetValueOrDefault("personen") ?? "";
            var (ps, ms, bad) = FamilyKit.ParseLines(lines);
            if (ps.Count + ms.Count == 0) throw new InvalidOperationException("Die KI hat keine lesbaren Personen geliefert" + (bad.Count > 0 ? $" ({bad[0]})" : "") + ". Nichts übernommen.");
            if (bad.Count > 0) throw new InvalidOperationException($"Die KI hat {bad.Count} Zeile(n) mit unlesbarem Datum oder ohne Namen geliefert ({bad[0]}). Nichts übernommen – bitte den Auftrag genauer fassen.");
            FamAddLines(d, lines, "Personen aus Text");
        },
        (_, _) => [])
    { OnApplyNote = "Erst bei „Ja, behalten“ kommt es in den Stammbaum – ein Rückgängig-Schritt nimmt es wieder heraus.", MakesFile = false };

    private void InitStammbaum() => RegisterProgram(FachTabs, new ProgramSpec(FamKey, FamStudio, "Stammbaum")
    {
        Sentence = "Personen, Familien, Ereignisse, Orte und Quellen – Baum der Vorfahren und Nachkommen, GEDCOM ein und aus, Prüfung auf Widersprüche, Bericht als PDF.",
        Folder = "Stammbaum",
        FillHeight = true,
        Templates =
        [
            new("leer", "Leerer Stammbaum", "Erste Person eintragen oder GEDCOM-Datei hierher ziehen", () => FamDoc()),
            new("beispiel", "Beispiel: Familie Muster", "Drei Generationen mit Orten, Heiraten und einer Quelle", FamSample),
        ],
        Ribbon =
        [
            ("Person", [
                new ProgramCmd("fam-add-person", "Person", d => FamAddPerson(d), Primary: true, Tip: "Neue Person – Name und Daten oben eintragen") { NoKi = true },
                new ProgramCmd("fam-add-father", "Vater", d => FamAddRelative(d, FamSelected(d) ?? throw new InvalidOperationException("Wähle zuerst eine Person."), "vater")) { NoKi = true },
                new ProgramCmd("fam-add-mother", "Mutter", d => FamAddRelative(d, FamSelected(d) ?? throw new InvalidOperationException("Wähle zuerst eine Person."), "mutter")) { NoKi = true },
                new ProgramCmd("fam-add-partner", "Partner", d => FamAddRelative(d, FamSelected(d) ?? throw new InvalidOperationException("Wähle zuerst eine Person."), "partner"), Level: 1) { NoKi = true },
                new ProgramCmd("fam-add-child", "Kind", d => FamAddRelative(d, FamSelected(d) ?? throw new InvalidOperationException("Wähle zuerst eine Person."), "kind")) { NoKi = true },
                new ProgramCmd("fam-del", "Löschen", d => FamRemove(d, FamSelected(d) ?? throw new InvalidOperationException("Wähle zuerst eine Person.")), Level: 1) { NoKi = true },
            ]),
            ("Stammbaum", [
                new ProgramCmd("fam-import", "GEDCOM einlesen …", d => _ = FamPickImport(d), Tip: "GEDCOM 5.5.1 aus anderen Stammbaum-Programmen (UTF-8, ANSEL, ANSI)") { NoKi = true },
                new ProgramCmd("fam-root", "In die Mitte", d => FamSetRoot(d, FamSelected(d) ?? throw new InvalidOperationException("Wähle zuerst eine Person.")), Tip: "Gewählte Person in die Mitte des Baums (auch Doppelklick)", Level: 1) { NoKi = true },
                new ProgramCmd("fam-source", "Quelle", d => FamAddSource(d), Level: 1) { NoKi = true },
            ]),
            ("Ansicht", [
                new ProgramCmd("fam-view-anc", "Vorfahren", d => FamSetView(d, "vorfahren")) { NoKi = true },
                new ProgramCmd("fam-view-desc", "Nachkommen", d => FamSetView(d, "nachkommen")) { NoKi = true },
                new ProgramCmd("fam-view-places", "Orte", d => FamSetView(d, "orte"), Level: 1) { NoKi = true },
                new ProgramCmd("fam-view-sources", "Quellen", d => FamSetView(d, "quellen"), Level: 1) { NoKi = true },
                new ProgramCmd("fam-zoom-in", "Vergrößern", d => FamZoomBy(d, 1.25)) { NoKi = true },
                new ProgramCmd("fam-zoom-out", "Verkleinern", d => FamZoomBy(d, 0.8)) { NoKi = true },
                new ProgramCmd("fam-fit", "Einpassen", d => _famZoom.Remove(d.Id), Level: 1) { NoKi = true },
            ]),
        ],
        Exports =
        [
            new("ged", "GEDCOM 5.5.1 (.ged)", ".ged", FamWriteGedcom),
            new("pdf", "Bericht (.pdf)", ".pdf", (d, f) => FamWriteReport(d, f, ".pdf")),
            new("svg", "Baum als SVG (.svg)", ".svg", (d, f) => FamWriteText(f, TimelineKit.ToSvg(FamScene(d, true), "Stammbaum"))),
            new("docx", "Bericht für Word (.docx)", ".docx", (d, f) => FamWriteReport(d, f, ".docx")),
            new("odt", "Bericht OpenDocument (.odt)", ".odt", (d, f) => FamWriteReport(d, f, ".odt")),
            new("png", "Baum als Bild (.png)", ".png", (d, f) => TimelineCanvas.SavePng(FamScene(d, true), f)),
            new("baum-pdf", "Baum als PDF (.pdf)", ".pdf", (d, f) => { TimelineKit.ToPdf(FamScene(d, true), f, "Stammbaum"); return f; }),
            new("csv", "Personenliste (.csv)", ".csv", (d, f) => FamWriteText(f, FamilyKit.ToCsv(FamData(d), FamToday), bom: true)),
        ],
        SimpleExports = 3,
        KiActions = [FamKiPersons],
        KiLocked = ["sources", "hideLiving"],
        KiCheck = FamilyKit.KiProblem,
        KiHint = "Personen fügst du nur mit „stammbaum.personen“ ein (alle in einem Schritt): eine Zeile je Person „Name | m oder w | Geburt | Geburtsort | Tod | Sterbeort | Vater | Mutter“ "
               + "(leere Felder dürfen fehlen), Ehen als „Ehe: Name + Name | Datum | Ort“. Daten wie „12.03.1850“, „1850“, „um 1850“, „vor 1900“. Nimm nur Personen und Daten, die im Text stehen – erfinde nichts. "
               + "Vorhandene Personen änderst du nicht.",
        FirstStep = new("Erste Person", "Trag die erste Person ein – meist dich selbst oder die älteste bekannte Person. Oder zieh eine GEDCOM-Datei (.ged) hierher.",
            d => FamAddPerson(d), d => FamData(d).Persons.Count == 0),
        Workspace = FamWorkspace,
        Properties = FamProperties,
        Bottom = new ProgramPanel("Prüfung", FamIssuesPanel, 110),
        Status = d =>
        {
            var t = FamData(d); var issues = FamilyKit.Check(t);
            return $"{t.Persons.Count} Personen · {t.Families.Count} Familien · {t.Sources.Count} Quellen · Prüfung: {issues.Count(i => i.Error)} Fehler, {issues.Count(i => !i.Error)} Hinweise"
                   + (t.HideLiving ? " · Lebende werden in Ausgaben ausgeblendet" : " · Achtung: Lebende stehen in den Ausgaben");
        },
        Help = "Oben „Person“, dann Vorname, Nachname und Daten in der Zeile über dem Baum eintragen. Mit „Vater“, „Mutter“, „Partner“ und „Kind“ wächst der Baum; "
             + "im Baum auf „+ Vater“ oder „+ Mutter“ klicken geht auch. Daten dürfen ungefähr sein („um 1850“, „vor 1900“, „zwischen 1850 und 1860“). Mausrad vergrößert, Ziehen verschiebt, "
             + "Doppelklick setzt eine Person in die Mitte, Rechtsklick für mehr, Entf löscht. GEDCOM-Dateien anderer Stammbaum-Programme hierher ziehen oder einlesen – auch ANSEL wird erkannt; "
             + "Angaben, die Trendo nicht bearbeitet, bleiben beim Speichern als GEDCOM erhalten. Unten stehen Widersprüche (Geburt nach Tod, Eltern jünger als Kinder …). "
             + "Lebende Personen werden in allen Ausgaben ausgeblendet, solange der Haken rechts gesetzt ist.",
    }, "stammbaum", "ahnen", "ahnentafel", "ahnenforschung", "genealogie", "familienforschung", "gedcom", "vorfahren", "nachkommen", "familie", "gramps", "familysearch", "ahnenliste");

    void FamZoomBy(ProgramDocument d, double k)
    {
        var (z, x, y) = _famZoom.TryGetValue(d.Id, out var v) ? v : (0.9, 20.0, 10.0);
        _famZoom[d.Id] = (Math.Clamp(z * k, 0.1, 4), x, y);
    }

    // ---------------- Oberfläche ----------------

    Control FamWorkspace(ProgramDocument d)
    {
        var t = FamData(d); var view = FamViewOf(d);
        var sel = FamSelected(d);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("240,10,*"), RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Thickness(4, 0, 10, 4) };
        var editor = FamEditor(d, t, sel); Grid.SetColumnSpan(editor, 3); grid.Children.Add(editor);
        var list = FamList(d, t, sel); Grid.SetRow(list, 1); grid.Children.Add(list);
        Control main = view switch { "orte" => FamPlacesView(d, t), "quellen" => FamSourcesView(d, t), _ => FamCanvas(d, t, sel, view) };
        var frame = new Border { Name = "fam-frame", CornerRadius = new CornerRadius(8), ClipToBounds = true, BorderBrush = Brush.Parse("#33D4A017"), BorderThickness = new Thickness(1), Child = main };
        Grid.SetRow(frame, 1); Grid.SetColumn(frame, 2); grid.Children.Add(frame);
        BueroFileDrop(grid, FamKey, BueroExt(".ged", ".gedcom"), "GEDCOM-Dateien (.ged)", (doc, files) => { foreach (var f in files) FamImport(doc, f); });
        return grid;
    }

    Control FamCanvas(ProgramDocument d, FamilyTree t, string? sel, string view)
    {
        var rootId = FamRootOf(t, d);
        var scene = view == "nachkommen" ? FamilyKit.Descendants(t, rootId, t.Generations, sel) : FamilyKit.Ancestors(t, rootId, t.Generations, sel);
        var canvas = new FamilyTreeCanvas { Name = "fam-canvas", Scene = scene, Selected = sel, View = _famZoom.TryGetValue(d.Id, out var v) ? v : null };
        if (rootId is not null && scene.Hits.TryGetValue(rootId, out var rh)) canvas.Focus = (rh.X + rh.W / 2 + (view == "nachkommen" ? 0 : 300), rh.Y + rh.H / 2);
        canvas.ViewChanged = (z, x, y) => { if (double.IsNaN(z)) _famZoom.Remove(d.Id); else _famZoom[d.Id] = (z, x, y); };
        canvas.Clicked = id => { _famSel[d.Id] = id; Show(FamKey); };
        canvas.Activated = id => ProgramRun(FamKey, () => FamSetRoot(d, id));
        canvas.Placeholder = h => ProgramRun(FamKey, () => FamAddRelative(d, h[(h.IndexOf(':') + 1)..], h.StartsWith("+vater", StringComparison.Ordinal) ? "vater" : "mutter"));
        canvas.DeleteSelected = () => { if (FamSelected(d) is { } id) ProgramRun(FamKey, () => FamRemove(d, id)); };
        string Need() => FamSelected(d) ?? throw new InvalidOperationException("Wähle zuerst eine Person.");
        canvas.ContextMenu = BueroMenu(FamKey, "fam-menu", null,
            ("fam-m-root", "In die Mitte", () => FamSetRoot(d, Need()), true),
            ("fam-m-father", "Vater hinzufügen", () => FamAddRelative(d, Need(), "vater"), true),
            ("fam-m-mother", "Mutter hinzufügen", () => FamAddRelative(d, Need(), "mutter"), true),
            ("fam-m-partner", "Partner hinzufügen", () => FamAddRelative(d, Need(), "partner"), true),
            ("fam-m-child", "Kind hinzufügen", () => FamAddRelative(d, Need(), "kind"), true),
            ("-", "", () => { }, true),
            ("fam-m-fit", "Einpassen", () => _famZoom.Remove(d.Id), true),
            ("fam-m-del", "Löschen", () => FamRemove(d, Need()), true));
        return canvas;
    }

    Control FamList(ProgramDocument d, FamilyTree t, string? sel)
    {
        var sp = new StackPanel { Spacing = 2 };
        var q = _famSearch.GetValueOrDefault(d.Id, "");
        var search = new TextBox { Name = "fam-search", Text = q, PlaceholderText = "Person suchen …", Margin = new Thickness(0, 0, 0, 6) };
        var items = new StackPanel { Spacing = 2 };
        void Fill(string query)
        {
            items.Children.Clear();
            var de = StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), true);
            var hits = t.Persons.Where(p => query.Length == 0 || p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).OrderBy(p => p.Surname, de).ThenBy(p => p.Given, de).Take(400).ToList();
            var i = 0;
            foreach (var p in hits)
            {
                var id = p.Id;
                var life = string.Join(" ", new[] { p.Birth is { } b ? "* " + b.When.Show(true) : "", p.Death is { } x ? "† " + x.When.Show(true) : "" }.Where(s => s.Length > 0));
                var b2 = new Button
                {
                    Name = "fam-person-" + i++, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 4),
                    Background = id == sel ? Brush.Parse("#33D4A017") : Brushes.Transparent, BorderThickness = new Thickness(0),
                    Content = new StackPanel { Children = { new TextBlock { Text = p.Name, Foreground = Text, FontSize = 12.5, FontWeight = id == sel ? FontWeight.SemiBold : FontWeight.Normal, TextTrimming = TextTrimming.CharacterEllipsis }, new TextBlock { Text = life, Foreground = Muted, FontSize = 10.5 } } },
                };
                b2.Click += (_, _) => { _famSel[d.Id] = id; Show(FamKey); };
                b2.DoubleTapped += (_, _) => ProgramRun(FamKey, () => FamSetRoot(d, id));
                items.Children.Add(b2);
            }
            if (hits.Count == 0) items.Children.Add(P(t.Persons.Count == 0 ? "Noch keine Personen." : "Keine Person gefunden.", Muted, 11));
        }
        Fill(q);
        search.TextChanged += (_, _) => { _famSearch[d.Id] = search.Text ?? ""; Fill(search.Text ?? ""); };
        sp.Children.Add(search);
        sp.Children.Add(new ScrollViewer { Content = items, MaxHeight = 900, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        return new Border { Name = "fam-list", Child = sp };
    }

    /// <summary>Zeile über dem Baum: gewählte Person bearbeiten (auch in „Einfach“, wo die rechte Spalte fehlt).</summary>
    Control FamEditor(ProgramDocument d, FamilyTree t, string? sel)
    {
        var p = t.P(sel);
        var row = new WrapPanel { Name = "fam-editor", Margin = new Thickness(0, 0, 0, 8) };
        if (p is null) { row.Children.Add(P(t.Persons.Count == 0 ? "Noch leer – oben „Person“ oder eine GEDCOM-Datei hierher ziehen." : "Eine Person anklicken, um sie zu bearbeiten – Doppelklick setzt sie in die Mitte.", Muted, 12)); return row; }
        TextBox Box(string field, string name, string value, string hint, double width)
        {
            var b = new TextBox { Name = name, Text = value, PlaceholderText = hint, Width = width, Margin = new Thickness(0, 0, 8, 4) };
            var shown = value;
            void Commit() { var now = b.Text ?? ""; if (now.Trim() == shown.Trim()) return; shown = now; ProgramRun(FamKey, () => FamSet(d, p.Id, field, now)); }
            b.LostFocus += (_, _) => Commit(); OnEnter(b, Commit);
            ToolTip.SetTip(b, hint);
            return b;
        }
        string DateText(FamEvent? e) => e is null ? "" : e.When.Kind == GenDateKind.Unreadable ? e.Date : e.When.Show();
        row.Children.Add(Box("given", "fam-f-given", p.Given, "Vorname(n)", 150));
        row.Children.Add(Box("surname", "fam-f-surname", p.Surname, "Nachname (Geburtsname)", 150));
        var sex = new ComboBox { Name = "fam-f-sex", Width = 120, Margin = new Thickness(0, 0, 8, 4), ItemsSource = new[] { "männlich", "weiblich", "unbekannt" }, SelectedIndex = p.Sex switch { "M" => 0, "F" => 1, _ => 2 } };
        sex.SelectionChanged += (_, _) => { var v = sex.SelectedIndex switch { 0 => "M", 1 => "F", _ => "U" }; if (v != p.Sex) ProgramRun(FamKey, () => FamSet(d, p.Id, "sex", v)); };
        row.Children.Add(sex);
        row.Children.Add(Box("birth", "fam-f-birth", DateText(p.Ev("BIRT")), "Geburt: 12.03.1850, um 1850, vor 1850 …", 140));
        row.Children.Add(Box("birthPlace", "fam-f-birthplace", p.Ev("BIRT")?.Place ?? "", "Geburtsort", 140));
        row.Children.Add(Box("death", "fam-f-death", DateText(p.Ev("DEAT")), "Tod (leer = lebt oder unbekannt)", 140));
        row.Children.Add(Box("deathPlace", "fam-f-deathplace", p.Ev("DEAT")?.Place ?? "", "Sterbeort", 140));
        var living = FamilyKit.IsLiving(t, p, FamToday);
        row.Children.Add(new TextBlock { Name = "fam-f-info", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Text = (living ? "lebt vermutlich" : "verstorben") + $" · {p.Id}" });
        return row;
    }

    Control FamPlacesView(ProgramDocument d, FamilyTree t)
    {
        var sp = new StackPanel { Name = "fam-places", Spacing = 6, Margin = new Thickness(12) };
        sp.Children.Add(H("Orte", 14));
        sp.Children.Add(P("Alle Orte aus Geburt, Taufe, Heirat, Tod und weiteren Ereignissen. Umbenennen ändert den Ort in jedem Ereignis – so werden Schreibweisen einheitlich. Koordinaten als Dezimalzahl (z. B. 50,94 und 6,96).", Muted, 11));
        var i = 0;
        foreach (var pl in FamilyKit.Places(t))
        {
            var name = pl.Name; var n = i++;
            var box = new TextBox { Name = "fam-place-" + n, Text = name, Width = 320 };
            box.LostFocus += (_, _) => { if ((box.Text ?? "").Trim() != name) ProgramRun(FamKey, () => FamRenamePlace(d, name, box.Text ?? "")); };
            var lat = new TextBox { Name = "fam-place-lat-" + n, Text = pl.Lat, Width = 110, PlaceholderText = "Breite" };
            var lon = new TextBox { Name = "fam-place-long-" + n, Text = pl.Long, Width = 110, PlaceholderText = "Länge" };
            void Coords() { if ((lat.Text ?? "") != pl.Lat || (lon.Text ?? "") != pl.Long) ProgramRun(FamKey, () => FamPlaceCoords(d, name, lat.Text ?? "", lon.Text ?? "")); }
            lat.LostFocus += (_, _) => Coords(); lon.LostFocus += (_, _) => Coords();
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { box, lat, lon, new TextBlock { Text = $"{pl.Count}×", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center } } });
        }
        if (i == 0) sp.Children.Add(P("Noch keine Orte eingetragen.", Muted, 12));
        return new ScrollViewer { Content = sp };
    }

    Control FamSourcesView(ProgramDocument d, FamilyTree t)
    {
        var sp = new StackPanel { Name = "fam-sources", Spacing = 6, Margin = new Thickness(12) };
        sp.Children.Add(H("Quellen", 14));
        sp.Children.Add(P("Kirchenbücher, Standesamtsregister, Urkunden, Briefe. Angeben bei einer Person rechts unter „Quellen“, mit Fundstelle (Seite, Nummer).", Muted, 11));
        var add = new Button { Name = "fam-src-add", Content = "Quelle anlegen", Padding = new Thickness(10, 4) };
        add.Click += (_, _) => ProgramRun(FamKey, () => FamAddSource(d));
        sp.Children.Add(add);
        var i = 0;
        foreach (var s in t.Sources)
        {
            var id = s.Id; var n = i++;
            TextBox F(string field, string value, string hint, double w)
            {
                var b = new TextBox { Name = $"fam-src-{field}-{n}", Text = value, PlaceholderText = hint, Width = w };
                b.LostFocus += (_, _) => { if ((b.Text ?? "") != value) ProgramRun(FamKey, () => FamSetSource(d, id, field, b.Text ?? "")); };
                return b;
            }
            var uses = t.Persons.Sum(p => p.Cites.Count(c => c.Source == id) + p.Events.Sum(e => e.Cites.Count(c => c.Source == id)));
            sp.Children.Add(new WrapPanel { Children = { F("title", s.Title, "Titel", 300), F("author", s.Author, "Verfasser / Stelle", 200), F("publication", s.Publication, "Veröffentlichung, Archiv, Signatur", 240), new TextBlock { Text = $"  {uses}× angegeben", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center } } });
        }
        return new ScrollViewer { Content = sp };
    }

    Control FamIssuesPanel(ProgramDocument d)
    {
        var t = FamData(d); var issues = FamilyKit.Check(t);
        var sp = new StackPanel { Name = "fam-issues", Spacing = 2 };
        if (issues.Count == 0) sp.Children.Add(P("Keine Widersprüche gefunden.", Muted, 12));
        var i = 0;
        foreach (var x in issues.OrderByDescending(x => x.Error).Take(300))
        {
            var pid = x.Person;
            var b = new Button { Name = "fam-issue-" + i++, Padding = new Thickness(6, 2), Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = new TextBlock { Text = (x.Error ? "⛔ " : "ⓘ ") + x.Text, Foreground = x.Error ? Brush.Parse("#F87171") : Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap } };
            b.Click += (_, _) => { if (pid is not null) { _famSel[d.Id] = pid; Show(FamKey); } };
            sp.Children.Add(b);
        }
        return new ScrollViewer { Content = sp };
    }

    Control FamProperties(ProgramDocument d)
    {
        var t = FamData(d); var sel = FamSelected(d); var p = t.P(sel);
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H("Stammbaum", 13));
        var title = new TextBox { Name = "fam-p-title", Text = t.Title, PlaceholderText = "Titel, z. B. Familie Muster" };
        title.LostFocus += (_, _) => { if ((title.Text ?? "") != t.Title) ProgramRun(FamKey, () => FamSetOption(d, "title", title.Text ?? "")); };
        sp.Children.Add(title);
        var hide = new CheckBox { Name = "fam-p-hideliving", Content = "Lebende Personen in Ausgaben ausblenden", IsChecked = t.HideLiving, Foreground = Text, FontSize = 12 };
        hide.IsCheckedChanged += (_, _) => { if ((hide.IsChecked == true) != t.HideLiving) ProgramRun(FamKey, () => FamSetOption(d, "hideLiving", hide.IsChecked == true ? "true" : "false")); };
        ToolTip.SetTip(hide, "Gilt für GEDCOM, Bericht, Baum-Bild und Tabelle: lebende Personen nur als „Lebende Person“ mit Nachnamen, ohne Daten. Als lebend gilt, wer keinen Tod hat und jünger als 110 Jahre ist (geschätzt über Eltern und Kinder).");
        sp.Children.Add(hide);
        var gens = new ComboBox { Name = "fam-p-gens", ItemsSource = new[] { "2 Generationen", "3 Generationen", "4 Generationen", "5 Generationen", "6 Generationen" }, SelectedIndex = t.Generations - 2, HorizontalAlignment = HorizontalAlignment.Stretch };
        gens.SelectionChanged += (_, _) => { if (gens.SelectedIndex + 2 != t.Generations) ProgramRun(FamKey, () => FamSetOption(d, "gens", (gens.SelectedIndex + 2).ToString(CultureInfo.InvariantCulture))); };
        sp.Children.Add(gens);
        if (p is not null)
        {
            sp.Children.Add(H(p.Name, 13));
            var (fa, mo) = t.Parents(p.Id);
            sp.Children.Add(P("Eltern: " + (fa is null && mo is null ? "noch keine" : string.Join(" und ", new[] { fa?.Name, mo?.Name }.OfType<string>())), Muted, 11));
            // Ereignisse
            sp.Children.Add(H("Ereignisse", 12));
            for (var i = 0; i < p.Events.Count; i++)
            {
                var e = p.Events[i]; var idx = i;
                TextBox F(string field, string value, string hint, double w)
                {
                    var b = new TextBox { Name = $"fam-ev-{field}-{idx}", Text = value, PlaceholderText = hint, Width = w, FontSize = 11.5 };
                    b.LostFocus += (_, _) => { if ((b.Text ?? "") != value) ProgramRun(FamKey, () => FamSetEvent(d, p.Id, idx, field, b.Text ?? "")); };
                    return b;
                }
                var del = new Button { Name = $"fam-ev-del-{idx}", Content = "✕", Padding = new Thickness(6, 1) };
                ToolTip.SetTip(del, "Ereignis entfernen");
                del.Click += (_, _) => ProgramRun(FamKey, () => FamRemoveEvent(d, p.Id, idx));
                var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = e.Type.Length > 0 ? e.Type : FamilyKit.EventName(e.Tag), Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center }, del } };
                sp.Children.Add(head);
                var wrap = new WrapPanel();
                if (e.Tag is "OCCU" or "RESI" or "EDUC" or "RELI" or "TITL" or "NATI" or "DSCR" or "EVEN") wrap.Children.Add(F("value", e.Value, "Angabe (z. B. Beruf)", 200));
                wrap.Children.Add(F("date", e.When.Kind == GenDateKind.Unreadable ? e.Date : e.When.Show(), "Datum", 120));
                wrap.Children.Add(F("place", e.Place, "Ort", 140));
                sp.Children.Add(wrap);
                if (e.Cites.Count > 0) sp.Children.Add(P("Quelle: " + string.Join("; ", e.Cites.Select(c => (t.Sources.FirstOrDefault(s => s.Id == c.Source)?.Title ?? c.Source) + (c.Page.Length > 0 ? ", " + c.Page : ""))), Muted, 10.5));
            }
            var evAdd = new ComboBox { Name = "fam-ev-add", PlaceholderText = "Ereignis hinzufügen …", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = FamilyKit.PersonEvents.Where(x => x.Tag is not ("BAPM")).Select(x => x.Name).ToList() };
            evAdd.SelectionChanged += (_, _) => { var list = FamilyKit.PersonEvents.Where(x => x.Tag is not ("BAPM")).ToList(); if (evAdd.SelectedIndex >= 0) { var tag = list[evAdd.SelectedIndex].Tag; ProgramRun(FamKey, () => FamAddEvent(d, p.Id, tag)); } };
            sp.Children.Add(evAdd);
            // Partner und Heirat
            var fi = 0;
            foreach (var f in t.OwnFamilies(p.Id).ToList())
            {
                var other = t.P(f.Husband == p.Id ? f.Wife : f.Husband); var fid = f.Id; var n = fi++;
                sp.Children.Add(H("Partner: " + (other?.Name ?? "unbekannt"), 12));
                var md = new TextBox { Name = "fam-marr-date-" + n, Text = f.Marriage?.When.Show() ?? "", PlaceholderText = "Heirat: Datum", Width = 120 };
                var mp = new TextBox { Name = "fam-marr-place-" + n, Text = f.Marriage?.Place ?? "", PlaceholderText = "Heirat: Ort", Width = 140 };
                var mdShown = md.Text ?? ""; var mpShown = mp.Text ?? "";
                md.LostFocus += (_, _) => { if ((md.Text ?? "") != mdShown) ProgramRun(FamKey, () => FamSetMarriage(d, fid, "date", md.Text ?? "")); };
                mp.LostFocus += (_, _) => { if ((mp.Text ?? "") != mpShown) ProgramRun(FamKey, () => FamSetMarriage(d, fid, "place", mp.Text ?? "")); };
                sp.Children.Add(new WrapPanel { Children = { md, mp } });
                var kids = f.Children.Select(t.P).OfType<FamPerson>().Select(k => k.Name).ToList();
                if (kids.Count > 0) sp.Children.Add(P("Kinder: " + string.Join(", ", kids), Muted, 11));
            }
            // Quellen
            sp.Children.Add(H("Quellen", 12));
            if (t.Sources.Count == 0) sp.Children.Add(P("Noch keine Quelle – unter „Ansicht → Quellen“ anlegen.", Muted, 11));
            else
            {
                var srcPick = new ComboBox { Name = "fam-cite-src", ItemsSource = t.Sources.Select(s => s.Title).ToList(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
                var evPick = new ComboBox { Name = "fam-cite-ev", ItemsSource = new[] { "zur Person" }.Concat(p.Events.Select(e => FamilyKit.EventName(e.Tag))).ToList(), SelectedIndex = p.Events.Count > 0 ? 1 : 0, HorizontalAlignment = HorizontalAlignment.Stretch };
                var page = new TextBox { Name = "fam-cite-page", PlaceholderText = "Fundstelle (Seite, Nummer)" };
                var go = new Button { Name = "fam-cite-add", Content = "Quelle angeben", Padding = new Thickness(10, 4) };
                go.Click += (_, _) => { var si = Math.Max(0, srcPick.SelectedIndex); var ei = evPick.SelectedIndex - 1; var pg = page.Text ?? ""; ProgramRun(FamKey, () => FamCite(d, p.Id, ei, t.Sources[si].Id, pg)); };
                sp.Children.AddRange([srcPick, evPick, page, go]);
            }
            sp.Children.Add(H("Notiz", 12));
            var note = new TextBox { Name = "fam-p-note", Text = p.Note, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, PlaceholderText = "Lebensgeschichte, Hinweise zur Forschung …" };
            var shownNote = p.Note;
            note.LostFocus += (_, _) => { if ((note.Text ?? "") != shownNote) { shownNote = note.Text ?? ""; ProgramRun(FamKey, () => FamSet(d, p.Id, "note", shownNote)); } };
            sp.Children.Add(note);
        }
        sp.Children.Add(H("Aus Text einfügen", 13));
        sp.Children.Add(P("Eine Zeile je Person: „Max Muster | m | 12.03.1850 | Musterstadt | 1920 | | Vater | Mutter“, Ehen: „Ehe: Max Muster + Erika Beispiel | 1875 | Musterstadt“. Für Fließtext: oben in der Auftragszeile die KI bitten.", Muted, 11));
        var paste = new TextBox { Name = "fam-p-lines", Text = _famPaste, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80 };
        paste.TextChanged += (_, _) => _famPaste = paste.Text ?? "";
        var add = new Button { Name = "fam-p-lines-add", Content = "Einfügen", Padding = new Thickness(10, 4) };
        add.Click += (_, _) => ProgramRun(FamKey, () => { var (n, l, _) = FamAddLines(d, paste.Text ?? ""); if (n + l > 0) _famPaste = ""; });
        sp.Children.Add(paste); sp.Children.Add(add);
        return sp;
    }
}
