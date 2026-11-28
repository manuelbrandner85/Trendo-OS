using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Wissen;

/// <summary>Quellenangabe an einer Person oder einem Ereignis: Verweis auf eine Quelle, Fundstelle („Seite 12, Nr. 34“), unbekannte Unterzeilen.</summary>
public sealed class FamCite
{
    public string Source { get; set; } = "";
    public string Page { get; set; } = "";
    public List<GedNode> Extra { get; set; } = [];
}

/// <summary>Ereignis oder Merkmal (Geburt, Taufe, Heirat, Tod, Beruf …) mit Datum, Ort (auch Koordinaten), Notiz und Quellen.</summary>
public sealed class FamEvent
{
    public string Tag { get; set; } = "EVEN";
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";
    string _date = ""; GenDate? _when;
    public string Date { get => _date; set { _date = value ?? ""; _when = null; } }
    public string Place { get; set; } = "";
    public string Lat { get; set; } = "";
    public string Long { get; set; } = "";
    public List<GedNode> PlaceExtra { get; set; } = [];
    public string Note { get; set; } = "";
    public List<FamCite> Cites { get; set; } = [];
    public List<GedNode> Extra { get; set; } = [];
    /// <summary>Gelesenes Datum (zwischengespeichert, bis <see cref="Date"/> sich ändert – Gegenprüfung G2).</summary>
    public GenDate When => _when ??= GenDate.Parse(_date);
}

/// <summary>Unterzeilen an einer Verknüpfung (FAMC/FAMS/CHIL), z. B. „PEDI adopted“ – bleiben erhalten.</summary>
public sealed record FamLinkExtra(string Kind, string Fam, string Person, List<GedNode> Nodes);

public sealed class FamPerson
{
    public string Id { get; set; } = "";
    public string Given { get; set; } = "";
    public string Surname { get; set; } = "";
    /// <summary>Name wie in GEDCOM („Erika /Muster/ jun.“) – Zusätze hinter dem Nachnamen bleiben so erhalten.</summary>
    public string NameRaw { get; set; } = "";
    public List<GedNode> NameExtra { get; set; } = [];
    /// <summary>„M“, „F“ oder „U“ wie in GEDCOM.</summary>
    public string Sex { get; set; } = "U";
    public string Note { get; set; } = "";
    public List<FamEvent> Events { get; set; } = [];
    public List<FamCite> Cites { get; set; } = [];
    public List<GedNode> Extra { get; set; } = [];
    public List<FamLinkExtra> Links { get; set; } = [];

    public string Name => (Given + " " + Surname).Trim() is { Length: > 0 } n ? n : "(ohne Namen)";
    public FamEvent? Ev(params string[] tags) { foreach (var t in tags) if (Events.FirstOrDefault(e => e.Tag == t) is { } e) return e; return null; }
    public FamEvent? Birth => Ev("BIRT", "CHR", "BAPM");
    public FamEvent? Death => Ev("DEAT", "BURI", "CREM");
}

public sealed class FamFamily
{
    public string Id { get; set; } = "";
    public string? Husband { get; set; }
    public string? Wife { get; set; }
    public List<string> Children { get; set; } = [];
    public List<FamEvent> Events { get; set; } = [];
    public string Note { get; set; } = "";
    public List<FamCite> Cites { get; set; } = [];
    public List<GedNode> Extra { get; set; } = [];
    public List<FamLinkExtra> ChildExtra { get; set; } = [];
    public FamEvent? Marriage => Events.FirstOrDefault(e => e.Tag == "MARR");
}

public sealed class FamSource
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Publication { get; set; } = "";
    public string Text { get; set; } = "";
    public string Note { get; set; } = "";
    public List<GedNode> Extra { get; set; } = [];
}

/// <summary>Stammbaum: Personen, Familien (Paar + Kinder), Quellen, dazu alles aus der GEDCOM-Datei, das Trendo nicht bearbeitet (bleibt erhalten).</summary>
public sealed class FamilyTree
{
    public string Title { get; set; } = "";
    public string? Root { get; set; }
    /// <summary>Lebende Personen in allen Ausgaben ausblenden (Name „Lebende Person“, keine Daten) – Standard an.</summary>
    public bool HideLiving { get; set; } = true;
    public int Generations { get; set; } = 4;
    public List<FamPerson> Persons { get; set; } = [];
    public List<FamFamily> Families { get; set; } = [];
    public List<FamSource> Sources { get; set; } = [];
    /// <summary>Weitere Datensätze der Datei (Notizen, Medien, Archive, Einsender, eigene Datensätze) – unverändert.</summary>
    public List<GedNode> Records { get; set; } = [];
    /// <summary>Zeilen aus dem Kopf der Datei, die Trendo nicht selbst schreibt (Einsender, Sprache, Ortsformat …).</summary>
    public List<GedNode> Head { get; set; } = [];
    public string ImportNote { get; set; } = "";

    /// <summary>T-0788 (Gegenprüfung G2): Nachschlage-Tabellen für große Stammbäume. Nur gültig, solange niemand Personen oder Familien ändert –
    /// deshalb nur in einem <c>using (t.Indexed())</c>-Block um lesende Arbeit (Prüfung, Ausblenden, Baum, Bericht). Ohne Block: Suche über die Listen.</summary>
    sealed class Index
    {
        public readonly Dictionary<string, FamPerson> Persons = new(StringComparer.Ordinal);
        public readonly Dictionary<string, FamFamily> Families = new(StringComparer.Ordinal);
        public readonly Dictionary<string, List<FamFamily>> Parent = new(StringComparer.Ordinal);
        public readonly Dictionary<string, List<FamFamily>> Own = new(StringComparer.Ordinal);
        public Index(FamilyTree t)
        {
            foreach (var p in t.Persons) Persons.TryAdd(p.Id, p);
            foreach (var f in t.Families)
            {
                Families.TryAdd(f.Id, f);
                foreach (var c in f.Children.Distinct()) Add(Parent, c, f);
                if (f.Husband is { } h) Add(Own, h, f);
                if (f.Wife is { } w && w != f.Husband) Add(Own, w, f);
            }
        }
        static void Add(Dictionary<string, List<FamFamily>> d, string k, FamFamily f) { if (!d.TryGetValue(k, out var l)) d[k] = l = []; l.Add(f); }
    }
    Index? _ix; int _ixDepth;

    sealed class IndexScope(FamilyTree t) : IDisposable { public void Dispose() { if (--t._ixDepth == 0) t._ix = null; } }

    /// <summary>Tabellen bauen (verschachtelt erlaubt); beim Verlassen des äußersten Blocks werden sie verworfen.</summary>
    public IDisposable Indexed() { if (_ixDepth++ == 0) _ix = new Index(this); return new IndexScope(this); }

    public FamPerson? P(string? id) => id is null ? null : _ix is { } ix ? ix.Persons.GetValueOrDefault(id) : Persons.FirstOrDefault(p => p.Id == id);
    public FamFamily? F(string? id) => id is null ? null : _ix is { } ix ? ix.Families.GetValueOrDefault(id) : Families.FirstOrDefault(f => f.Id == id);
    public IEnumerable<FamFamily> ParentFamilies(string id) => _ix is { } ix ? ix.Parent.GetValueOrDefault(id) ?? [] : Families.Where(f => f.Children.Contains(id));
    public IEnumerable<FamFamily> OwnFamilies(string id) => _ix is { } ix ? ix.Own.GetValueOrDefault(id) ?? [] : Families.Where(f => f.Husband == id || f.Wife == id);
    public (FamPerson? Father, FamPerson? Mother) Parents(string id) { var f = ParentFamilies(id).FirstOrDefault(); return (P(f?.Husband), P(f?.Wife)); }
    public IEnumerable<FamPerson> Spouses(string id) => OwnFamilies(id).Select(f => P(f.Husband == id ? f.Wife : f.Husband)).OfType<FamPerson>();
    public IEnumerable<FamPerson> Children(string id) => OwnFamilies(id).SelectMany(f => f.Children).Distinct().Select(P).OfType<FamPerson>();

    public string NewId(string prefix)
    {
        var used = Persons.Select(p => p.Id).Concat(Families.Select(f => f.Id)).Concat(Sources.Select(s => s.Id)).Concat(Records.Select(r => (r.Xref ?? "").Trim('@'))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var n = used.Select(u => u.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(u[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var k) ? k : 0).DefaultIfEmpty(0).Max() + 1;
        while (used.Contains(prefix + n)) n++;
        return prefix + n;
    }
}

public sealed record FamIssue(bool Error, string? Person, string? Family, string Text);

/// <summary>T-0788 (Etappe 0.9.115 „Wissen &amp; Lernen E“): Fachkern des Studios „Stammbaum“ – GEDCOM-Abbildung, Prüfungen, Lebende, Bericht, Baum-Bilder.</summary>
public static partial class FamilyKit
{
    public const int MaxPersons = 50_000;

    public static readonly (string Tag, string Name)[] PersonEvents =
    [
        ("BIRT", "Geburt"), ("CHR", "Taufe"), ("BAPM", "Taufe"), ("DEAT", "Tod"), ("BURI", "Begräbnis"), ("CREM", "Einäscherung"), ("OCCU", "Beruf"), ("RESI", "Wohnort"),
        ("EDUC", "Ausbildung"), ("RELI", "Religion"), ("CONF", "Konfirmation"), ("FCOM", "Erstkommunion"), ("GRAD", "Abschluss"), ("EMIG", "Auswanderung"), ("IMMI", "Einwanderung"),
        ("NATU", "Einbürgerung"), ("CENS", "Volkszählung"), ("ADOP", "Adoption"), ("PROB", "Testamentseröffnung"), ("WILL", "Testament"), ("RETI", "Ruhestand"), ("TITL", "Titel"),
        ("NATI", "Herkunft"), ("DSCR", "Beschreibung"), ("EVEN", "Ereignis"),
    ];

    public static readonly (string Tag, string Name)[] FamilyEvents =
    [
        ("MARR", "Heirat"), ("ENGA", "Verlobung"), ("MARB", "Aufgebot"), ("MARC", "Ehevertrag"), ("DIV", "Scheidung"), ("DIVF", "Scheidungsantrag"), ("ANUL", "Annullierung"), ("EVEN", "Ereignis"),
    ];

    public static string EventName(string tag) => PersonEvents.Concat(FamilyEvents).FirstOrDefault(e => e.Tag == tag).Name ?? tag;
    public static string SexName(string s) => s switch { "M" => "männlich", "F" => "weiblich", _ => "unbekannt" };
    static string Strip(string v) => v.Trim().Trim('@');
    static string Ptr(string id) => "@" + id + "@";

    // ---------------- Name ----------------

    public static (string Given, string Surname) SplitName(string raw)
    {
        var a = raw.IndexOf('/'); if (a < 0) return (raw.Trim(), "");
        var b = raw.IndexOf('/', a + 1);
        var given = raw[..a].Trim(); var sur = b > a ? raw[(a + 1)..b].Trim() : raw[(a + 1)..].Trim();
        var suffix = b > a ? raw[(b + 1)..].Trim() : "";
        return (suffix.Length > 0 ? (given + " " + suffix).Trim() : given, sur);
    }

    /// <summary>Vor- und Nachname setzen: GEDCOM-Name und vorhandene Unterzeilen GIVN/SURN werden mitgeführt.</summary>
    public static void SetName(FamPerson p, string given, string surname)
    {
        p.Given = given.Trim(); p.Surname = surname.Trim();
        p.NameRaw = (p.Given + " /" + p.Surname + "/").Trim();
        foreach (var k in p.NameExtra) { if (k.Tag == "GIVN") k.Value = p.Given; if (k.Tag == "SURN") k.Value = p.Surname; }
    }

    // ---------------- Aus GEDCOM ----------------

    public static FamilyTree FromGedcom(byte[] bytes) { var r = Gedcom.Read(bytes); var t = FromRecords(r.Records, r.Problems); t.ImportNote = Note(r, t); return t; }

    static string Note(GedRead r, FamilyTree t) =>
        $"Zeichensatz: {r.Encoding}. {t.Persons.Count} Personen, {t.Families.Count} Familien, {t.Sources.Count} Quellen, {t.Records.Count} weitere Datensätze unverändert übernommen." +
        (r.Problems.Count > 0 ? $" {r.Problems.Count} Hinweis(e): {string.Join("; ", r.Problems.Take(3))}" : "");

    public const int MaxXref = 20;

    /// <summary>T-0788 (Gegenprüfung G6/G9): überlange Kennungen (über 20 Zeichen) bekommen eine kurze neue – alle Verweise darauf werden mitgeführt;
    /// doppelte Kennungen werden gemeldet und der zweite Datensatz eindeutig umbenannt (Verweise zeigen weiter auf den ersten, das ist in der Datei nicht eindeutig).</summary>
    static HashSet<string> FixXrefs(List<GedNode> records, List<string> problems)
    {
        var used = new HashSet<string>(records.Where(r => r.Xref is not null).Select(r => Strip(r.Xref!)), StringComparer.Ordinal);
        // T-0788 (2. Prüfrunde): auch Kennungen, die nur als (verwaister) Verweis vorkommen, sind vergeben – sonst bekäme ein umbenannter Datensatz fremde Verweise
        used.UnionWith(Refs(records));
        var dupRenamed = new HashSet<string>(StringComparer.Ordinal);
        string Fresh(string tag)
        {
            var prefix = tag switch { "INDI" => "I", "FAM" => "F", "SOUR" => "S", "NOTE" => "N", "OBJE" => "M", "REPO" => "R", "SUBM" => "U", _ => "X" };
            var n = used.Count + 1; while (used.Contains(prefix + n)) n++;
            used.Add(prefix + n); return prefix + n;
        }
        var rename = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in records.Where(r => r.Xref is not null && Strip(r.Xref).Length > MaxXref))
        {
            var old = Strip(r.Xref!); if (rename.ContainsKey(old)) continue;
            var neu = Fresh(r.Tag); rename[old] = neu;
            problems.Insert(0, $"Kennung @{(old.Length > 30 ? old[..30] + "…" : old)}@ zu lang (höchstens {MaxXref} Zeichen) – heißt jetzt @{neu}@, Verweise mitgeführt");
        }
        if (rename.Count > 0)
        {
            void Walk(GedNode n, int depth)
            {
                if (depth > Gedcom.MaxDepth) return;
                if (n.Xref is not null && rename.TryGetValue(Strip(n.Xref), out var x)) n.Xref = Ptr(x);
                if (n.IsPointer && rename.TryGetValue(Strip(n.Value), out var v)) n.Value = Ptr(v);
                foreach (var k in n.Kids) Walk(k, depth + 1);
            }
            foreach (var r in records) Walk(r, 0);
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in records.Where(r => r.Xref is not null))
        {
            var id = Strip(r.Xref!);
            if (seen.Add(id)) continue;
            var neu = Fresh(r.Tag); r.Xref = Ptr(neu); seen.Add(neu); dupRenamed.Add(neu);
            problems.Insert(0, $"Kennung @{id}@ doppelt – der zweite Datensatz ({r.Tag}) heißt jetzt @{neu}@; Verweise auf @{id}@ gelten dem ersten");
        }
        return dupRenamed;
    }

    public static FamilyTree FromRecords(List<GedNode> records, List<string>? problems = null)
    {
        var t = new FamilyTree(); problems ??= [];
        var dupRenamed = FixXrefs(records, problems);
        var links = new List<FamLinkExtra>();
        foreach (var r in records)
        {
            var id = Strip(r.Xref ?? "");
            switch (r.Tag)
            {
                case "HEAD":
                    t.Head = [.. r.Kids.Where(k => k.Tag is not ("SOUR" or "DEST" or "DATE" or "CHAR" or "GEDC" or "FILE")).Select(k => k.Copy())];
                    if (r.Kid("FILE") is { Value.Length: > 0 } file) t.Title = Path.GetFileNameWithoutExtension(file.Value);
                    break;
                case "TRLR": break;
                case "INDI" when id.Length > 0: if (t.Persons.Count < MaxPersons) t.Persons.Add(ReadPerson(r, id, links)); break;
                case "FAM" when id.Length > 0: t.Families.Add(ReadFamily(r, id)); break;
                case "SOUR" when id.Length > 0: t.Sources.Add(ReadSource(r, id)); break;
                default: t.Records.Add(r.Copy()); break;
            }
        }
        // Verknüpfungen: Familien sind maßgeblich; FAMC/FAMS ohne Gegenstück werden ergänzt und gemeldet
        using var ix = t.Indexed();
        foreach (var l in links)
        {
            // T-0788 (2. Prüfrunde): ein umbenannter doppelter Datensatz wird nicht still über FAMC/FAMS in eine Familie gehängt – die Familie meint den ersten
            if (dupRenamed.Contains(l.Person)) { problems.Add($"{l.Person}: Verknüpfung {l.Kind} zu {l.Fam} gelöst (doppelte Kennung – gemeint war vermutlich der erste Datensatz)"); t.P(l.Person)?.Links.RemoveAll(x => x.Fam == l.Fam); continue; }
            var f = t.F(l.Fam);
            if (f is null) { problems.Add($"{l.Person}: Verweis auf fehlende Familie {l.Fam}"); continue; }
            if (l.Kind == "FAMC" && !f.Children.Contains(l.Person)) { f.Children.Add(l.Person); problems.Add($"{l.Person}: als Kind in {l.Fam} ergänzt (stand nur bei der Person)"); }
            if (l.Kind == "FAMS" && f.Husband != l.Person && f.Wife != l.Person)
            {
                var p = t.P(l.Person);
                if (f.Husband is null && p?.Sex != "F") f.Husband = l.Person; else if (f.Wife is null) f.Wife = l.Person;
                problems.Add($"{l.Person}: als Partner in {l.Fam} ergänzt (stand nur bei der Person)");
            }
        }
        foreach (var f in t.Families)
            foreach (var pid in new[] { f.Husband, f.Wife }.Concat(f.Children).OfType<string>())
                if (t.P(pid) is null) problems.Add($"Familie {f.Id}: Person {pid} fehlt");
        return t;
    }

    static bool Plain(GedNode n) => n.Kids.Count == 0;

    static FamCite ReadCite(GedNode n)
    {
        var c = new FamCite { Source = Strip(n.Value) };
        foreach (var k in n.Kids) { if (k.Tag == "PAGE" && Plain(k) && c.Page.Length == 0) c.Page = k.Value; else c.Extra.Add(k.Copy()); }
        return c;
    }

    static FamEvent ReadEvent(GedNode n)
    {
        var e = new FamEvent { Tag = n.Tag, Value = n.Value == "Y" && n.Tag is "BIRT" or "DEAT" or "CHR" or "BURI" or "MARR" ? "" : n.Value };
        foreach (var k in n.Kids)
        {
            switch (k.Tag)
            {
                case "TYPE" when Plain(k) && e.Type.Length == 0: e.Type = k.Value; break;
                case "DATE" when Plain(k) && e.Date.Length == 0: e.Date = k.Value; break;
                case "PLAC" when e.Place.Length == 0 && e.PlaceExtra.Count == 0 && k.Value.Length > 0:
                    e.Place = k.Value;
                    foreach (var pk in k.Kids)
                    {
                        if (pk.Tag == "MAP" && pk.Value.Length == 0 && e.Lat.Length == 0 && pk.Kids.All(x => x.Tag is "LATI" or "LONG" && Plain(x)) && pk.Kids.Count(x => x.Tag == "LATI") <= 1 && pk.Kids.Count(x => x.Tag == "LONG") <= 1)
                        { e.Lat = pk.Kid("LATI")?.Value ?? ""; e.Long = pk.Kid("LONG")?.Value ?? ""; }
                        else e.PlaceExtra.Add(pk.Copy());
                    }
                    break;
                case "NOTE" when Plain(k) && !k.IsPointer && e.Note.Length == 0 && k.Value.Length > 0: e.Note = k.Value; break;
                case "SOUR" when k.IsPointer: e.Cites.Add(ReadCite(k)); break;
                default: e.Extra.Add(k.Copy()); break;
            }
        }
        return e;
    }

    static FamPerson ReadPerson(GedNode r, string id, List<FamLinkExtra> links)
    {
        var p = new FamPerson { Id = id };
        var named = false;
        foreach (var k in r.Kids)
        {
            switch (k.Tag)
            {
                case "NAME" when !named:
                    named = true; p.NameRaw = k.Value; (p.Given, p.Surname) = SplitName(k.Value);
                    p.NameExtra = [.. k.Kids.Select(x => x.Copy())];
                    if (p.Given.Length == 0 && k.Kid("GIVN") is { } gv) p.Given = gv.Value;
                    if (p.Surname.Length == 0 && k.Kid("SURN") is { } sv) p.Surname = sv.Value;
                    break;
                case "SEX" when Plain(k) && k.Value.Trim().ToUpperInvariant() is "M" or "F" or "U": p.Sex = k.Value.Trim().ToUpperInvariant(); break;
                case "NOTE" when Plain(k) && !k.IsPointer && p.Note.Length == 0 && k.Value.Length > 0: p.Note = k.Value; break;
                case "SOUR" when k.IsPointer: p.Cites.Add(ReadCite(k)); break;
                case "FAMC" or "FAMS" when k.IsPointer:
                    links.Add(new(k.Tag, Strip(k.Value), id, []));
                    if (k.Kids.Count > 0) p.Links.Add(new(k.Tag, Strip(k.Value), id, [.. k.Kids.Select(x => x.Copy())]));
                    break;
                default:
                    if (PersonEvents.Any(e => e.Tag == k.Tag)) p.Events.Add(ReadEvent(k)); else p.Extra.Add(k.Copy());
                    break;
            }
        }
        return p;
    }

    static FamFamily ReadFamily(GedNode r, string id)
    {
        var f = new FamFamily { Id = id };
        foreach (var k in r.Kids)
        {
            switch (k.Tag)
            {
                case "HUSB" when k.IsPointer && Plain(k) && f.Husband is null: f.Husband = Strip(k.Value); break;
                case "WIFE" when k.IsPointer && Plain(k) && f.Wife is null: f.Wife = Strip(k.Value); break;
                case "CHIL" when k.IsPointer && !f.Children.Contains(Strip(k.Value)):
                    f.Children.Add(Strip(k.Value));
                    if (k.Kids.Count > 0) f.ChildExtra.Add(new("CHIL", id, Strip(k.Value), [.. k.Kids.Select(x => x.Copy())]));
                    break;
                case "NOTE" when Plain(k) && !k.IsPointer && f.Note.Length == 0 && k.Value.Length > 0: f.Note = k.Value; break;
                case "SOUR" when k.IsPointer: f.Cites.Add(ReadCite(k)); break;
                default:
                    if (FamilyEvents.Any(e => e.Tag == k.Tag)) f.Events.Add(ReadEvent(k)); else f.Extra.Add(k.Copy());
                    break;
            }
        }
        return f;
    }

    static FamSource ReadSource(GedNode r, string id)
    {
        var s = new FamSource { Id = id };
        foreach (var k in r.Kids)
        {
            switch (k.Tag)
            {
                case "TITL" when Plain(k) && s.Title.Length == 0: s.Title = k.Value; break;
                case "AUTH" when Plain(k) && s.Author.Length == 0: s.Author = k.Value; break;
                case "PUBL" when Plain(k) && s.Publication.Length == 0: s.Publication = k.Value; break;
                case "TEXT" when Plain(k) && s.Text.Length == 0: s.Text = k.Value; break;
                case "NOTE" when Plain(k) && !k.IsPointer && s.Note.Length == 0: s.Note = k.Value; break;
                default: s.Extra.Add(k.Copy()); break;
            }
        }
        return s;
    }

    // ---------------- Nach GEDCOM ----------------

    static void Cites(GedNode n, List<FamCite> cites)
    {
        foreach (var c in cites)
        {
            var s = new GedNode("SOUR", Ptr(c.Source));
            if (c.Page.Length > 0) s.Kids.Add(new("PAGE", c.Page));
            s.Kids.AddRange(c.Extra.Select(x => x.Copy()));
            n.Kids.Add(s);
        }
    }

    static GedNode EventNode(FamEvent e)
    {
        var n = new GedNode(e.Tag, e.Value.Length == 0 && e.Tag is "BIRT" or "DEAT" or "CHR" or "BURI" or "MARR" && e.Date.Length == 0 && e.Place.Length == 0 ? "Y" : e.Value);
        if (e.Type.Length > 0) n.Kids.Add(new("TYPE", e.Type));
        if (e.Date.Length > 0) n.Kids.Add(new("DATE", e.Date));
        if (e.Place.Length > 0)
        {
            var pl = new GedNode("PLAC", e.Place);
            if (e.Lat.Length > 0 || e.Long.Length > 0)
            {
                var map = new GedNode("MAP");
                if (e.Lat.Length > 0) map.Kids.Add(new("LATI", e.Lat));
                if (e.Long.Length > 0) map.Kids.Add(new("LONG", e.Long));
                pl.Kids.Add(map);
            }
            pl.Kids.AddRange(e.PlaceExtra.Select(x => x.Copy()));
            n.Kids.Add(pl);
        }
        if (e.Note.Length > 0) n.Kids.Add(new("NOTE", e.Note));
        Cites(n, e.Cites);
        n.Kids.AddRange(e.Extra.Select(x => x.Copy()));
        return n;
    }

    /// <summary>Stammbaum als GEDCOM 5.5.1 (UTF-8). Mit <paramref name="hideLiving"/> werden lebende Personen auf „Lebende Person“ ohne Daten gekürzt.</summary>
    public static string ToGedcom(FamilyTree src, bool? hideLiving = null, DateTime? now = null)
    {
        var day = now ?? DateTime.Today;
        var t = hideLiving ?? src.HideLiving ? Private(src, day) : src;
        using var ix = t.Indexed(); // G2: FAMC/FAMS je Person über Tabellen
        var recs = new List<GedNode>();
        var head = new GedNode("HEAD");
        var sour = new GedNode("SOUR", "TRENDO") { Kids = { new("NAME", "Trendo OS Stammbaum"), new("VERS", "1.0") } };
        head.Kids.Add(sour);
        head.Kids.Add(new("DATE", $"{day.Day} {GenDate.Ged[day.Month - 1]} {day.Year}"));
        head.Kids.Add(new("GEDC") { Kids = { new("VERS", "5.5.1"), new("FORM", "LINEAGE-LINKED") } });
        head.Kids.Add(new("CHAR", "UTF-8"));
        head.Kids.AddRange(t.Head.Select(x => x.Copy()));
        var extraRecs = t.Records.Select(x => x.Copy()).ToList();
        if (!head.Kids.Any(k => k.Tag == "SUBM"))
        {
            var subm = extraRecs.FirstOrDefault(r => r.Tag == "SUBM" && r.Xref is not null);
            if (subm is null) { subm = new GedNode("SUBM", "", "@SUBM1@") { Kids = { new("NAME", "Unbekannt") } }; extraRecs.Insert(0, subm); }
            head.Kids.Add(new("SUBM", subm.Xref!));
        }
        recs.Add(head);
        foreach (var p in t.Persons)
        {
            var n = new GedNode("INDI", "", Ptr(p.Id));
            var name = new GedNode("NAME", p.NameRaw.Length > 0 ? p.NameRaw : (p.Given + " /" + p.Surname + "/").Trim());
            name.Kids.AddRange(p.NameExtra.Select(x => x.Copy()));
            n.Kids.Add(name);
            n.Kids.Add(new("SEX", p.Sex is "M" or "F" ? p.Sex : "U"));
            foreach (var e in p.Events) n.Kids.Add(EventNode(e));
            if (p.Note.Length > 0) n.Kids.Add(new("NOTE", p.Note));
            Cites(n, p.Cites);
            foreach (var f in t.ParentFamilies(p.Id)) n.Kids.Add(Link("FAMC", f.Id, p));
            foreach (var f in t.OwnFamilies(p.Id)) n.Kids.Add(Link("FAMS", f.Id, p));
            n.Kids.AddRange(p.Extra.Select(x => x.Copy()));
            recs.Add(n);
        }
        foreach (var f in t.Families)
        {
            var n = new GedNode("FAM", "", Ptr(f.Id));
            if (f.Husband is { } h) n.Kids.Add(new("HUSB", Ptr(h)));
            if (f.Wife is { } w) n.Kids.Add(new("WIFE", Ptr(w)));
            foreach (var c in f.Children)
            {
                var cn = new GedNode("CHIL", Ptr(c));
                if (f.ChildExtra.FirstOrDefault(x => x.Person == c) is { } ce) cn.Kids.AddRange(ce.Nodes.Select(x => x.Copy()));
                n.Kids.Add(cn);
            }
            foreach (var e in f.Events) n.Kids.Add(EventNode(e));
            if (f.Note.Length > 0) n.Kids.Add(new("NOTE", f.Note));
            Cites(n, f.Cites);
            n.Kids.AddRange(f.Extra.Select(x => x.Copy()));
            recs.Add(n);
        }
        foreach (var s in t.Sources)
        {
            var n = new GedNode("SOUR", "", Ptr(s.Id));
            if (s.Title.Length > 0) n.Kids.Add(new("TITL", s.Title));
            if (s.Author.Length > 0) n.Kids.Add(new("AUTH", s.Author));
            if (s.Publication.Length > 0) n.Kids.Add(new("PUBL", s.Publication));
            if (s.Text.Length > 0) n.Kids.Add(new("TEXT", s.Text));
            if (s.Note.Length > 0) n.Kids.Add(new("NOTE", s.Note));
            n.Kids.AddRange(s.Extra.Select(x => x.Copy()));
            recs.Add(n);
        }
        recs.AddRange(extraRecs);
        recs.Add(new GedNode("TRLR"));
        return Gedcom.Write(recs);
    }

    static GedNode Link(string kind, string fam, FamPerson p)
    {
        var n = new GedNode(kind, Ptr(fam));
        if (p.Links.FirstOrDefault(l => l.Kind == kind && l.Fam == fam) is { } x) n.Kids.AddRange(x.Nodes.Select(k => k.Copy()));
        return n;
    }

    // ---------------- Lebende Personen ----------------

    /// <summary>Lebt die Person vermutlich noch? Tod/Begräbnis eingetragen → nein. Geburt vor mehr als 110 Jahren → nein.
    /// Ohne Geburt wird über Eltern (+20 Jahre), Ehepartner und Kinder (−20 Jahre) geschätzt. Ohne jeden Anhalt: ja (im Zweifel schützen).</summary>
    public static bool IsLiving(FamilyTree t, FamPerson p, DateTime now)
    {
        // T-0788 (Gegenprüfung G4): im Zweifel lebend. Tot nur bei eingetragenem Tod/Begräbnis oder wenn die SPÄTESTE mögliche Geburt mehr als 110 Jahre zurückliegt.
        if (p.Death is not null) return false;
        var limit = now.Year - 110;
        if (p.Birth?.When is { Comparable: true } own) return !double.IsFinite(own.Hi) || own.Hi > limit; // „nach 1900“, „ab 1890“: kann jung sein
        double Hi(FamEvent? e) => e?.When is { Comparable: true } g ? g.Hi : double.PositiveInfinity;
        var latest = double.PositiveInfinity; // späteste mögliche Geburt
        foreach (var f in t.ParentFamilies(p.Id))
        {
            if (t.P(f.Husband) is { } fa) latest = Math.Min(latest, Math.Min(Hi(fa.Birth) + 80, Hi(fa.Death) + 1));
            if (t.P(f.Wife) is { } mo) latest = Math.Min(latest, Math.Min(Hi(mo.Birth) + 60, Hi(mo.Death)));
        }
        foreach (var f in t.OwnFamilies(p.Id))
        {
            latest = Math.Min(latest, Hi(f.Marriage) - 12);
            foreach (var c in f.Children) if (t.P(c) is { } ch) latest = Math.Min(latest, Hi(ch.Birth) - 12);
        }
        return !(latest <= limit);
    }

    static IEnumerable<string> Refs(IEnumerable<GedNode> nodes)
    {
        var stack = new Stack<(GedNode, int)>(nodes.Select(n => (n, 0)));
        while (stack.Count > 0)
        {
            var (n, d) = stack.Pop();
            if (n.Xref is null && n.IsPointer) yield return Strip(n.Value);
            if (d < Gedcom.MaxDepth) foreach (var k in n.Kids) stack.Push((k, d + 1));
        }
    }
    static IEnumerable<string> Refs(FamCite c) => Refs(c.Extra).Append(c.Source);
    static IEnumerable<string> Refs(FamEvent e) => Refs(e.Extra).Concat(Refs(e.PlaceExtra)).Concat(e.Cites.SelectMany(Refs));
    static IEnumerable<string> Refs(FamPerson p) => Refs(p.Extra).Concat(Refs(p.NameExtra)).Concat(p.Events.SelectMany(Refs)).Concat(p.Cites.SelectMany(Refs)).Concat(p.Links.SelectMany(l => Refs(l.Nodes)));
    static IEnumerable<string> Refs(FamFamily f) => Refs(f.Extra).Concat(f.Events.SelectMany(Refs)).Concat(f.Cites.SelectMany(Refs)).Concat(f.ChildExtra.SelectMany(l => Refs(l.Nodes)));

    /// <summary>Kopie mit gekürzten lebenden Personen: Name „Lebende Person“ (Nachname bleibt), keine Ereignisse, Notizen, Quellen und Zusatzzeilen;
    /// Ereignisse der Familie (Heirat …) fallen weg, sobald ein Partner lebt. Verknüpfungen bleiben, damit der Baum zusammenhängt.
    /// T-0788 (Gegenprüfung G5): Datensätze (Notizen, Medien, Quellen …), auf die nur noch Lebende verwiesen haben, fallen ebenfalls weg.</summary>
    public static FamilyTree Private(FamilyTree src, DateTime now)
    {
        var t = FromJson(ToJson(src));
        HashSet<string> living;
        using (src.Indexed()) living = src.Persons.Where(p => IsLiving(src, p, now)).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var hiddenRefs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in t.Persons.Where(p => living.Contains(p.Id)))
        {
            hiddenRefs.UnionWith(Refs(p));
            p.Given = "Lebende Person"; p.NameRaw = $"Lebende Person /{p.Surname}/"; p.NameExtra = [new("SURN", p.Surname)];
            if (p.Surname.Length == 0) p.NameExtra.Clear();
            p.Events.Clear(); p.Note = ""; p.Cites.Clear(); p.Extra.Clear(); p.Links.Clear();
        }
        foreach (var f in t.Families.Where(f => new[] { f.Husband, f.Wife }.Any(x => x is not null && living.Contains(x))))
        { hiddenRefs.UnionWith(Refs(f)); f.Events.Clear(); f.Note = ""; f.Cites.Clear(); f.Extra.Clear(); }
        foreach (var f in t.Families)
        {
            foreach (var c in f.ChildExtra.Where(c => living.Contains(c.Person))) hiddenRefs.UnionWith(Refs(c.Nodes));
            f.ChildExtra.RemoveAll(c => living.Contains(c.Person));
        }
        // T-0788 (2. Prüfrunde): Einsender (SUBM) ist oft eine lebende Person – Adresse, Telefon, E-Mail und Webseite fallen weg, der Name bleibt (Herkunft der Datei)
        foreach (var r in t.Records.Where(r => r.Tag == "SUBM")) r.Kids.RemoveAll(k => k.Tag is "ADDR" or "ADR1" or "ADR2" or "ADR3" or "CITY" or "POST" or "STAE" or "CTRY" or "PHON" or "EMAIL" or "FAX" or "WWW" || k.Tag.StartsWith('_'));
        if (hiddenRefs.Count > 0)
        {
            // was nach dem Kürzen noch erreichbar ist (von Personen, Familien, Kopf und von dort aus über Datensätze und Quellen)
            var kept = new HashSet<string>(t.Persons.SelectMany(Refs).Concat(t.Families.SelectMany(Refs)).Concat(Refs(t.Head)), StringComparer.Ordinal);
            var recs = t.Records.Where(r => r.Xref is not null).GroupBy(r => Strip(r.Xref!)).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            var srcs = t.Sources.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            void Close(HashSet<string> set)
            {
                var todo = new Queue<string>(set);
                while (todo.Count > 0)
                {
                    var id = todo.Dequeue();
                    var more = (recs.TryGetValue(id, out var rl) ? rl.SelectMany(r => Refs(r.Kids)) : []).Concat(srcs.TryGetValue(id, out var sl) ? sl.SelectMany(x => Refs(x.Extra)) : []);
                    foreach (var m in more) if (set.Add(m)) todo.Enqueue(m);
                }
            }
            Close(kept);
            Close(hiddenRefs); // T-0788 (2. Prüfrunde): auch was nur über ausgeblendete Datensätze erreichbar war (Lebender → NOTE → SOUR)
            t.Records.RemoveAll(r => r.Xref is not null && hiddenRefs.Contains(Strip(r.Xref)) && !kept.Contains(Strip(r.Xref)));
            t.Sources.RemoveAll(x => hiddenRefs.Contains(x.Id) && !kept.Contains(x.Id));
        }
        return t;
    }

    // ---------------- Dokument (JSON) ----------------

    static string S(JsonNode? n) => n?.GetValueKind() == JsonValueKind.String ? n.GetValue<string>() : "";
    static JsonArray Arr<T>(IEnumerable<T> xs, Func<T, JsonNode> f) => new([.. xs.Select(f)]);

    static JsonObject CiteJson(FamCite c) { var o = new JsonObject { ["src"] = c.Source }; if (c.Page.Length > 0) o["page"] = c.Page; if (c.Extra.Count > 0) o["extra"] = GedNode.ListJson(c.Extra); return o; }
    static FamCite CiteFrom(JsonNode? n) => new() { Source = S(n?["src"]), Page = S(n?["page"]), Extra = GedNode.ListFrom(n?["extra"]) };

    public static JsonObject EventJson(FamEvent e)
    {
        var o = new JsonObject { ["tag"] = e.Tag };
        void Put(string k, string v) { if (v.Length > 0) o[k] = v; }
        Put("type", e.Type); Put("value", e.Value); Put("date", e.Date); Put("place", e.Place); Put("lat", e.Lat); Put("long", e.Long); Put("note", e.Note);
        if (e.PlaceExtra.Count > 0) o["placeExtra"] = GedNode.ListJson(e.PlaceExtra);
        if (e.Cites.Count > 0) o["cites"] = Arr(e.Cites, c => CiteJson(c));
        if (e.Extra.Count > 0) o["extra"] = GedNode.ListJson(e.Extra);
        return o;
    }

    public static FamEvent EventFrom(JsonNode? n) => new()
    {
        Tag = Gedcom.SafeTag(S(n?["tag"]) is { Length: > 0 } tg ? tg : "EVEN"), Type = S(n?["type"]), Value = S(n?["value"]), Date = S(n?["date"]), Place = S(n?["place"]),
        Lat = S(n?["lat"]), Long = S(n?["long"]), Note = S(n?["note"]), PlaceExtra = GedNode.ListFrom(n?["placeExtra"]),
        Cites = n?["cites"] is JsonArray a ? [.. a.Select(CiteFrom)] : [], Extra = GedNode.ListFrom(n?["extra"]),
    };

    static JsonObject LinkJson(FamLinkExtra l) => new() { ["kind"] = l.Kind, ["fam"] = l.Fam, ["person"] = l.Person, ["nodes"] = GedNode.ListJson(l.Nodes) };
    static FamLinkExtra LinkFrom(JsonNode? n) => new(S(n?["kind"]), S(n?["fam"]), S(n?["person"]), GedNode.ListFrom(n?["nodes"]));

    public static JsonObject PersonJson(FamPerson p)
    {
        var o = new JsonObject { ["id"] = p.Id, ["given"] = p.Given, ["surname"] = p.Surname, ["sex"] = p.Sex };
        if (p.NameRaw.Length > 0) o["nameRaw"] = p.NameRaw;
        if (p.NameExtra.Count > 0) o["nameExtra"] = GedNode.ListJson(p.NameExtra);
        if (p.Note.Length > 0) o["note"] = p.Note;
        o["events"] = Arr(p.Events, e => EventJson(e));
        if (p.Cites.Count > 0) o["cites"] = Arr(p.Cites, c => CiteJson(c));
        if (p.Extra.Count > 0) o["extra"] = GedNode.ListJson(p.Extra);
        if (p.Links.Count > 0) o["links"] = Arr(p.Links, l => LinkJson(l));
        return o;
    }

    public static FamPerson PersonFrom(JsonNode? n) => new()
    {
        Id = S(n?["id"]), Given = S(n?["given"]), Surname = S(n?["surname"]), Sex = S(n?["sex"]) is "M" or "F" ? S(n?["sex"]) : "U", NameRaw = S(n?["nameRaw"]),
        NameExtra = GedNode.ListFrom(n?["nameExtra"]), Note = S(n?["note"]), Events = n?["events"] is JsonArray a ? [.. a.Select(EventFrom)] : [],
        Cites = n?["cites"] is JsonArray c ? [.. c.Select(CiteFrom)] : [], Extra = GedNode.ListFrom(n?["extra"]), Links = n?["links"] is JsonArray l ? [.. l.Select(LinkFrom)] : [],
    };

    public static JsonObject FamilyJson(FamFamily f)
    {
        var o = new JsonObject { ["id"] = f.Id };
        if (f.Husband is { } h) o["husband"] = h;
        if (f.Wife is { } w) o["wife"] = w;
        o["children"] = Arr(f.Children, c => JsonValue.Create(c)!);
        o["events"] = Arr(f.Events, e => EventJson(e));
        if (f.Note.Length > 0) o["note"] = f.Note;
        if (f.Cites.Count > 0) o["cites"] = Arr(f.Cites, c => CiteJson(c));
        if (f.Extra.Count > 0) o["extra"] = GedNode.ListJson(f.Extra);
        if (f.ChildExtra.Count > 0) o["childExtra"] = Arr(f.ChildExtra, l => LinkJson(l));
        return o;
    }

    public static FamFamily FamilyFrom(JsonNode? n) => new()
    {
        Id = S(n?["id"]), Husband = S(n?["husband"]) is { Length: > 0 } h ? h : null, Wife = S(n?["wife"]) is { Length: > 0 } w ? w : null,
        Children = n?["children"] is JsonArray c ? [.. c.Select(S).Where(x => x.Length > 0).Distinct()] : [],
        Events = n?["events"] is JsonArray e ? [.. e.Select(EventFrom)] : [], Note = S(n?["note"]),
        Cites = n?["cites"] is JsonArray ci ? [.. ci.Select(CiteFrom)] : [], Extra = GedNode.ListFrom(n?["extra"]),
        ChildExtra = n?["childExtra"] is JsonArray ce ? [.. ce.Select(LinkFrom)] : [],
    };

    public static JsonObject SourceJson(FamSource s)
    {
        var o = new JsonObject { ["id"] = s.Id, ["title"] = s.Title };
        void Put(string k, string v) { if (v.Length > 0) o[k] = v; }
        Put("author", s.Author); Put("publication", s.Publication); Put("text", s.Text); Put("note", s.Note);
        if (s.Extra.Count > 0) o["extra"] = GedNode.ListJson(s.Extra);
        return o;
    }

    public static FamSource SourceFrom(JsonNode? n) => new() { Id = S(n?["id"]), Title = S(n?["title"]), Author = S(n?["author"]), Publication = S(n?["publication"]), Text = S(n?["text"]), Note = S(n?["note"]), Extra = GedNode.ListFrom(n?["extra"]) };

    public static JsonObject ToJson(FamilyTree t) => new()
    {
        ["title"] = t.Title,
        ["root"] = t.Root,
        ["hideLiving"] = t.HideLiving,
        ["gens"] = t.Generations,
        ["persons"] = Arr(t.Persons, p => PersonJson(p)),
        ["families"] = Arr(t.Families, f => FamilyJson(f)),
        ["sources"] = Arr(t.Sources, s => SourceJson(s)),
        ["records"] = GedNode.ListJson(t.Records),
        ["head"] = GedNode.ListJson(t.Head),
        ["importNote"] = t.ImportNote,
    };

    public static FamilyTree FromJson(JsonObject o) => new()
    {
        Title = S(o["title"]),
        Root = S(o["root"]) is { Length: > 0 } r ? r : null,
        HideLiving = o["hideLiving"]?.GetValueKind() != JsonValueKind.False,
        Generations = o["gens"]?.GetValueKind() == JsonValueKind.Number ? Math.Clamp(o["gens"]!.GetValue<int>(), 2, 6) : 4,
        Persons = o["persons"] is JsonArray p ? [.. p.Take(MaxPersons).Select(PersonFrom).Where(x => x.Id.Length > 0)] : [],
        Families = o["families"] is JsonArray f ? [.. f.Select(FamilyFrom).Where(x => x.Id.Length > 0)] : [],
        Sources = o["sources"] is JsonArray s ? [.. s.Select(SourceFrom).Where(x => x.Id.Length > 0)] : [],
        Records = GedNode.ListFrom(o["records"]),
        Head = GedNode.ListFrom(o["head"]),
        ImportNote = S(o["importNote"]),
    };

    /// <summary>Nur der fachliche Inhalt (ohne Ansicht, Titel, Einstellungen und Import-Hinweis) – zum Vergleich bei Rundreisen.</summary>
    public static string Content(FamilyTree t)
    {
        var o = ToJson(t);
        foreach (var k in new[] { "title", "root", "hideLiving", "gens", "importNote" }) o.Remove(k);
        // SUBM, das Trendo beim Schreiben selbst ergänzt, zählt nicht
        if (o["records"] is JsonArray recs) foreach (var r in recs.ToList()) if (r?["t"]?.GetValue<string>() == "SUBM" && r["x"]?.GetValue<string>() == "@SUBM1@") recs.Remove(r);
        if (o["head"] is JsonArray head) foreach (var h in head.ToList()) if (h?["t"]?.GetValue<string>() == "SUBM" && h["v"]?.GetValue<string>() == "@SUBM1@") head.Remove(h);
        return o.ToJsonString();
    }
}
