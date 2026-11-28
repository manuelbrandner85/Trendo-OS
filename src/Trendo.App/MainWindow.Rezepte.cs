using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0787 (Etappe 0.9.115 „Wissen &amp; Lernen E“, Freigabe 26.11.2026): Studio „Rezepte &amp; Ernährung“ (STUDIO-RECIPE-01) als Programm der Hülle.
/// Ein Rezeptbuch je Dokument mit drei Ansichten wie in Rezept-Programmen: Rezepte (Liste links, Rezept in der Mitte, Nährwerte rechts), Wochenplan (sieben Tage) und
/// Einkaufsliste (zusammengefasst, nach Abteilung im Laden). Fachkern: <see cref="RezeptKit"/>, Nährwerte aus <see cref="NaehrwertTabelle"/> (USDA SR Legacy, CC0) – nur Richtwerte.
/// Ein- und Ausgabe: schema.org/Recipe (JSON-LD, Rezeptseite HTML), PDF zum Drucken, DOCX/ODT, Einkaufsliste als Text/CSV.
/// Die KI legt über die eine Kette ein Rezept im offenen Buch an (Vorschau → „Ja, behalten“ → ein Rückgängig-Schritt); Gesundheitsversprechen nimmt Trendo nicht an.</summary>
public partial class MainWindow
{
    public const string RzKey = "rezepte-programm";
    public const string RzStudio = "STUDIO-RECIPE-01";
    public static readonly string[] RzModes = ["Rezepte", "Wochenplan", "Einkaufsliste"];

    readonly Dictionary<string, int> _rzMode = [];
    readonly Dictionary<string, string> _rzSel = [];
    readonly Dictionary<string, int> _rzView = [];
    readonly Dictionary<string, string> _rzFilter = [];

    private void InitRezepte() => RegisterProgram(FachTabs, RzSpec(), "rezept", "rezepte", "kochen", "backen", "kochbuch", "zutaten", "portionen", "nährwerte", "kalorien", "wochenplan", "essensplan", "einkaufsliste", "mealie", "paprika", "chefkoch");

    // ---------------- Zustand ----------------

    public static List<Rezept> RzRezepte(ProgramDocument d) => RezeptKit.Rezepte(d.Data);
    static void RzSave(ProgramDocument d, IEnumerable<Rezept> list, string label) => d.Edit(label, x => x["rezepte"] = RezeptKit.RezepteJson(list));
    public int RzModeOf(ProgramDocument d) => _rzMode.GetValueOrDefault(d.Id);
    public void RzSetMode(ProgramDocument d, int mode) => _rzMode[d.Id] = Math.Clamp(mode, 0, RzModes.Length - 1);
    public string? RzSelected(ProgramDocument d)
    {
        var list = RzRezepte(d);
        if (_rzSel.TryGetValue(d.Id, out var id) && list.Any(r => r.Id == id)) return id;
        return list.FirstOrDefault()?.Id;
    }
    public Rezept? RzCurrent(ProgramDocument d) => RzSelected(d) is { } id ? RzRezepte(d).FirstOrDefault(r => r.Id == id) : null;
    public void RzSelect(ProgramDocument d, string id) { _rzSel[d.Id] = id; _rzView.Remove(d.Id); }
    /// <summary>Für wie viele Portionen das Rezept gerade angezeigt wird (nur Ansicht; „So übernehmen“ rechnet das Rezept selbst um).</summary>
    public int RzViewPortions(ProgramDocument d) => _rzView.TryGetValue(d.Id, out var n) ? n : RzCurrent(d)?.Portionen ?? 4;
    public void RzSetView(ProgramDocument d, int n) => _rzView[d.Id] = Math.Clamp(n, 1, RezeptKit.MaxPortionen);

    Rezept RzNeed(ProgramDocument d) => RzCurrent(d) ?? throw new InvalidOperationException("Leg zuerst ein Rezept an („Neues Rezept“).");

    void RzReplace(ProgramDocument d, Rezept r, string label)
    {
        var list = RzRezepte(d); var i = list.FindIndex(x => x.Id == r.Id);
        if (i < 0) throw new InvalidOperationException("Dieses Rezept gibt es nicht mehr.");
        if (RezeptKit.RezeptJson(list[i]).ToJsonString() == RezeptKit.RezeptJson(r).ToJsonString()) return;
        list[i] = r; RzSave(d, list, label);
    }

    // ---------------- Rezepte ----------------

    public Rezept RzNew(ProgramDocument d, string name = "")
    {
        var list = RzRezepte(d);
        if (list.Count >= RezeptKit.MaxRezepte) throw new InvalidOperationException($"Höchstens {RezeptKit.MaxRezepte} Rezepte je Rezeptbuch.");
        var r = new Rezept(RezeptKit.NewId("r"), name.Trim().Length > 0 ? name.Trim() : "Neues Rezept", 4, [], []);
        list.Add(r); RzSave(d, list, "Neues Rezept");
        RzSelect(d, r.Id); _rzMode[d.Id] = 0;
        _progMsg[RzKey] = "Neues Rezept – Name, Zutaten (eine je Zeile) und Schritte eintragen.";
        return r;
    }

    public void RzSetField(ProgramDocument d, string id, string field, string value)
    {
        var r = RzRezepte(d).FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Dieses Rezept gibt es nicht mehr.");
        int Num(int max) => int.TryParse((value ?? "").Trim(), NumberStyles.Integer, RezeptKit.De, out var n) && n >= 0 ? Math.Min(n, max) : throw new ArgumentException("Bitte eine ganze Zahl eintragen.");
        var v = (value ?? "").Trim();
        var neu = field switch
        {
            "name" => r with { Name = v.Length > 200 ? v[..200] : v },
            "beschreibung" => r with { Beschreibung = v.Length > 4000 ? v[..4000] : v },
            "portionen" => r with { Portionen = Math.Max(1, Num(RezeptKit.MaxPortionen)) },
            "portionWort" => r with { PortionWort = v.Length == 0 ? "Portionen" : v.Length > 40 ? v[..40] : v },
            "vorbereitung" => r with { Vorbereitung = Num(100_000) },
            "garzeit" => r with { Garzeit = Num(100_000) },
            "kategorie" => r with { Kategorie = v.Length > 100 ? v[..100] : v },
            "kueche" => r with { Kueche = v.Length > 100 ? v[..100] : v },
            "stichwoerter" => r with { Stichwoerter = v.Length > 400 ? v[..400] : v },
            "quelle" => r with { Quelle = v.Length > 1000 ? v[..1000] : v },
            "notizen" => r with { Notizen = v.Length > 4000 ? v[..4000] : v },
            _ => throw new ArgumentException("Unbekanntes Feld."),
        };
        RzReplace(d, neu, field switch { "name" => "Name", "portionen" => "Portionen", "beschreibung" => "Beschreibung", _ => "Angabe geändert" });
        if (field == "portionen") _rzView.Remove(d.Id);
    }

    /// <summary>Zutaten als Text übernehmen (eine je Zeile). Eine von Hand gewählte Zuordnung bleibt, solange die Zeile gleich bleibt.</summary>
    public void RzSetZutaten(ProgramDocument d, string id, string text)
    {
        var r = RzRezepte(d).FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Dieses Rezept gibt es nicht mehr.");
        var old = r.Zutaten.GroupBy(z => RezeptKit.Zeile(z)).ToDictionary(g => g.Key, g => g.First());
        var lines = (text ?? "").Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count > RezeptKit.MaxZutaten) throw new InvalidOperationException($"Höchstens {RezeptKit.MaxZutaten} Zutaten je Rezept.");
        var hinweise = new List<string>();
        var neu = lines.Select(l => { var (z, h) = RezeptKit.Lies(l); if (h is not null) hinweise.Add(h); return old.TryGetValue(RezeptKit.Zeile(z), out var o) ? z with { Lm = o.Lm } : z; }).Where(z => z.Name.Length > 0).ToList();
        RzReplace(d, r with { Zutaten = neu }, "Zutaten");
        if (hinweise.Count > 0) _progMsg[RzKey] = "⚠ " + string.Join(" ", hinweise.Take(3)) + (hinweise.Count > 3 ? $" (+{hinweise.Count - 3} weitere)" : "");
    }

    public void RzSetSchritte(ProgramDocument d, string id, string text)
    {
        var r = RzRezepte(d).FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Dieses Rezept gibt es nicht mehr.");
        var steps = (text ?? "").Replace("\r", "").Split('\n').Select(s => System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"^\d+[.)]\s*", "")).Where(s => s.Length > 0).ToList();
        if (steps.Count > RezeptKit.MaxSchritte) throw new InvalidOperationException($"Höchstens {RezeptKit.MaxSchritte} Schritte je Rezept.");
        RzReplace(d, r with { Schritte = steps }, "Schritte");
    }

    /// <summary>Zuordnung einer Zutat zur Nährwert-Tabelle von Hand wählen („-“ = keine).</summary>
    public void RzSetLm(ProgramDocument d, string id, int index, string lm)
    {
        var r = RzRezepte(d).FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Dieses Rezept gibt es nicht mehr.");
        if (index < 0 || index >= r.Zutaten.Count) throw new InvalidOperationException("Diese Zutat gibt es nicht mehr.");
        if (lm != Zutat.KeinLm && NaehrwertTabelle.Find(lm) is null) throw new ArgumentException("Dieses Lebensmittel steht nicht in der Tabelle.");
        var list = r.Zutaten.ToList(); list[index] = list[index] with { Lm = lm };
        RzReplace(d, r with { Zutaten = list }, "Zuordnung");
        _progMsg[RzKey] = lm == Zutat.KeinLm ? $"„{list[index].Name}“ zählt nicht zu den Nährwerten." : $"„{list[index].Name}“ rechnet jetzt mit „{NaehrwertTabelle.Find(lm)!.Name}“.";
    }

    /// <summary>Angezeigte Portionen ins Rezept übernehmen: alle Mengen werden umgerechnet (ein Rückgängig-Schritt).</summary>
    public void RzApplyScale(ProgramDocument d, int? portionen = null)
    {
        var r = RzNeed(d); var n = portionen ?? RzViewPortions(d);
        if (n == r.Portionen) { _progMsg[RzKey] = "Das Rezept ist schon für " + n + " " + r.PortionWort + "."; return; }
        RzReplace(d, RezeptKit.Skaliere(r, n), $"Auf {n} {r.PortionWort} umgerechnet");
        _rzView.Remove(d.Id);
        _progMsg[RzKey] = $"Umgerechnet von {r.Portionen} auf {n} {r.PortionWort} – Strg+Z macht es rückgängig.";
    }

    public void RzMetric(ProgramDocument d)
    {
        var r = RzNeed(d); var (m, n) = RezeptKit.InMetrisch(r);
        if (n == 0) { _progMsg[RzKey] = "Es gibt keine Löffel- oder Tassenangaben zum Umrechnen."; return; }
        RzReplace(d, m, "In Gramm und Milliliter");
        _progMsg[RzKey] = $"{n} Angabe(n) in Gramm bzw. Milliliter umgerechnet (EL = 15 ml, TL = 5 ml, Tasse = 250 ml).";
    }

    public Rezept RzDuplicate(ProgramDocument d)
    {
        var r = RzNeed(d); var list = RzRezepte(d); var copy = r with { Id = RezeptKit.NewId("r"), Name = r.Name + " (Kopie)" };
        list.Insert(list.FindIndex(x => x.Id == r.Id) + 1, copy); RzSave(d, list, "Rezept kopiert"); RzSelect(d, copy.Id);
        return copy;
    }

    public void RzDelete(ProgramDocument d, string id)
    {
        var list = RzRezepte(d); var i = list.FindIndex(r => r.Id == id); if (i < 0) return;
        var name = list[i].Name; list.RemoveAt(i);
        d.Edit("Rezept gelöscht", x =>
        {
            x["rezepte"] = RezeptKit.RezepteJson(list);
            x["plan"] = RezeptKit.PlanJson(RezeptKit.PlanStart(x), RezeptKit.Plan(x)); // Einträge des gelöschten Rezepts fallen aus dem Plan
        });
        if (list.Count > 0) _rzSel[d.Id] = list[Math.Min(i, list.Count - 1)].Id; else _rzSel.Remove(d.Id);
        _progMsg[RzKey] = $"„{name}“ gelöscht – Strg+Z holt es zurück.";
    }

    public void RzMove(ProgramDocument d, int from, int to)
    {
        var list = RzRezepte(d);
        if (from < 0 || from >= list.Count || to < 0 || to >= list.Count || from == to) return;
        var r = list[from]; list.RemoveAt(from); list.Insert(to, r);
        RzSave(d, list, "Rezept verschoben"); _rzSel[d.Id] = r.Id;
    }

    /// <summary>Rezepte aus JSON-LD oder einer gespeicherten Rezeptseite (HTML) übernehmen – die Datei bleibt unverändert.</summary>
    public int RzImportFile(ProgramDocument d, string file) => RzImportApply(d, RzImportRead(file), Path.GetExtension(file));

    /// <summary>Datei lesen und auswerten – ohne Dokument und ohne Oberfläche, darum auch im Hintergrund lauffähig (Gegenprüfung T-0787: nicht im UI-Thread).</summary>
    static (List<Rezept> Got, List<string> Notes) RzImportRead(string file)
    {
        if (!KreativSafe.LocalFile(file, 20_000_000)) throw new InvalidOperationException("Wähle eine Rezept-Datei (JSON-LD oder Webseite, höchstens 20 MB) auf diesem Rechner.");
        return RezeptKit.Import(File.ReadAllText(file));
    }

    /// <summary>Gelesene Rezepte ins Buch übernehmen (ein Rückgängig-Schritt).</summary>
    int RzImportApply(ProgramDocument d, (List<Rezept> Got, List<string> Notes) read, string ext)
    {
        var (got, notes) = read;
        if (got.Count == 0) throw new InvalidDataException(notes.FirstOrDefault() ?? "In der Datei steht kein Rezept.");
        var list = RzRezepte(d);
        if (list.Count + got.Count > RezeptKit.MaxRezepte) throw new InvalidOperationException($"Höchstens {RezeptKit.MaxRezepte} Rezepte je Rezeptbuch.");
        list.AddRange(got); RzSave(d, list, got.Count == 1 ? "Rezept eingelesen" : $"{got.Count} Rezepte eingelesen");
        RzSelect(d, got[0].Id); _rzMode[d.Id] = 0;
        var miss = got.Sum(r => RezeptKit.Berechne(r).Fehlt.Count);
        _progMsg[RzKey] = (got.Count == 1 ? $"„{got[0].Name}“ eingelesen" : $"{got.Count} Rezepte eingelesen") + (miss > 0 ? $" – {miss} Zutat(en) ohne Nährwerte, rechts lassen sie sich zuordnen." : ".") + (notes.Count > 0 ? " " + notes[0] : "");
        _core.Audit.Append(Principal.LocalOwner.Name, "rezepte.import", $"{got.Count} {ext}");
        return got.Count;
    }

    public Rezept RzFromText(ProgramDocument d, string text)
    {
        if ((text ?? "").Trim().Length < 10) throw new ArgumentException("Füg zuerst den Rezepttext ein (Name, Zutaten, Zubereitung).");
        var hinweise = new List<string>();
        var r = RezeptKit.AusText(text!, hinweise);
        if (r.Zutaten.Count == 0 && r.Schritte.Count == 0) throw new InvalidOperationException("Im Text stehen keine Zutaten oder Schritte.");
        var list = RzRezepte(d); list.Add(r); RzSave(d, list, "Rezept aus Text");
        RzSelect(d, r.Id); _rzMode[d.Id] = 0;
        _progMsg[RzKey] = $"„{r.Name}“ übernommen: {r.Zutaten.Count} Zutaten, {r.Schritte.Count} Schritte – bitte kurz prüfen." + (hinweise.Count > 0 ? " ⚠ " + string.Join(" ", hinweise.Take(3)) : "");
        return r;
    }

    /// <summary>Läuft gerade ein Einlesen im Hintergrund? (für Tests)</summary>
    public int RzImportBusy { get; private set; }

    /// <summary>„Rezept öffnen …“: die Sperre greift schon VOR dem Dateidialog (3. Prüfrunde) – ein zweiter Klick öffnet keinen zweiten Dialog.</summary>
    async Task RzPickImport(ProgramDocument d)
    {
        if (RzImportBusy > 0) { _progMsg[RzKey] = "Es wird gerade schon eine Datei gelesen – bitte kurz warten."; Show(RzKey); return; }
        RzImportBusy++;
        try
        {
            if (await PickMediaFile("rz-import", "Rezept-Datei (JSON-LD oder gespeicherte Rezeptseite)", ["*.json", "*.jsonld", "*.html", "*.htm"]) is not { } f) return;
            await RzImportCore(d, [f]);
        }
        finally { RzImportBusy--; }
    }

    /// <summary>Gegenprüfung T-0787 (Runde 2): EIN Weg für „Rezept öffnen …“ und Ziehen und Ablegen – Lesen und Auswerten im Hintergrund, Übernehmen im UI-Thread
    /// (ein Rückgängig-Schritt je Datei). Sperre: solange ein Einlesen läuft, startet kein zweites. Ist das Rezeptbuch inzwischen geschlossen, wird nichts übernommen.</summary>
    public async Task RzImportFilesAsync(ProgramDocument d, IReadOnlyList<string> files)
    {
        if (RzImportBusy > 0) { _progMsg[RzKey] = "Es wird gerade schon eine Datei gelesen – bitte kurz warten."; Show(RzKey); return; }
        RzImportBusy++;
        try { await RzImportCore(d, files); }
        finally { RzImportBusy--; }
    }

    /// <summary>Lesen (Hintergrund) und Übernehmen (UI-Thread) je Datei. Lese- und Übernahmefehler werden einheitlich gesammelt und am Ende genannt
    /// („2 von 3 Dateien eingelesen, „x.json“: …“); eine einzelne Datei meldet ihren Fehler direkt.</summary>
    async Task RzImportCore(ProgramDocument d, IReadOnlyList<string> files)
    {
        _progMsg[RzKey] = files.Count == 1 ? "Lese „" + Path.GetFileName(files[0]) + "“ …" : $"Lese {files.Count} Dateien …"; Show(RzKey);
        var n = 0; var ok = 0; var fehler = new List<string>();
        foreach (var f in files)
        {
            try
            {
                var read = await Task.Run(() => RzImportRead(f));
                if (!ProgramDocs(RzKey).Contains(d))
                {
                    _progMsg[RzKey] = $"Das Rezeptbuch „{d.Title}“ wurde geschlossen, während „{Path.GetFileName(f)}“ gelesen wurde – nichts übernommen.";
                    _core.Audit.Append(Principal.LocalOwner.Name, "rezepte.import.verworfen", Path.GetFileName(f));
                    Show(RzKey); return;
                }
                n += RzImportApply(d, read, Path.GetExtension(f)); ok++;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                fehler.Add($"„{Path.GetFileName(f)}“: {(ex is InvalidDataException or InvalidOperationException or ArgumentException ? ex.Message : FriendlyError.From(ex).Sentence)}"); // eigene Meldungen sind schon Alltagssätze
            }
        }
        if (files.Count == 1 && fehler.Count == 1) _progMsg[RzKey] = fehler[0][(fehler[0].IndexOf(':') + 2)..];
        else if (files.Count > 1) _progMsg[RzKey] = $"{ok} von {files.Count} Dateien eingelesen ({n} Rezepte)" + (fehler.Count > 0 ? ". " + string.Join(" · ", fehler.Take(5)) + (fehler.Count > 5 ? $" (+{fehler.Count - 5} weitere)" : "") : ".");
        Show(RzKey);
    }

    // ---------------- Wochenplan ----------------

    public PlanEintrag RzPlanAdd(ProgramDocument d, string rezeptId, int tag, string mahlzeit = "Mittag", int? portionen = null)
    {
        var r = RzRezepte(d).FirstOrDefault(x => x.Id == rezeptId) ?? throw new InvalidOperationException("Wähle ein Rezept für den Plan.");
        var plan = RezeptKit.Plan(d.Data);
        if (plan.Count >= RezeptKit.MaxPlan) throw new InvalidOperationException($"Höchstens {RezeptKit.MaxPlan} Einträge im Wochenplan.");
        if (!RezeptKit.Mahlzeiten.Contains(mahlzeit)) mahlzeit = "Mittag";
        var p = new PlanEintrag(RezeptKit.NewId("p"), Math.Clamp(tag, 0, 6), mahlzeit, r.Id, Math.Clamp(portionen ?? r.Portionen, 1, RezeptKit.MaxPortionen));
        plan.Add(p);
        d.Edit($"{r.Name} am {RezeptKit.Tage[p.Tag]}", x => x["plan"] = RezeptKit.PlanJson(RezeptKit.PlanStart(x), plan));
        _progMsg[RzKey] = $"„{r.Name}“ steht am {RezeptKit.Tage[p.Tag]} ({p.Mahlzeit}, {p.Portionen} {r.PortionWort}) – die Einkaufsliste rechnet mit.";
        return p;
    }

    public void RzPlanRemove(ProgramDocument d, string id)
    {
        var plan = RezeptKit.Plan(d.Data); if (plan.RemoveAll(p => p.Id == id) == 0) return;
        d.Edit("Aus dem Plan genommen", x => x["plan"] = RezeptKit.PlanJson(RezeptKit.PlanStart(x), plan));
    }

    public void RzPlanMove(ProgramDocument d, string id, int tag)
    {
        var plan = RezeptKit.Plan(d.Data); var i = plan.FindIndex(p => p.Id == id); if (i < 0) return;
        plan[i] = plan[i] with { Tag = Math.Clamp(tag, 0, 6) };
        d.Edit($"Auf {RezeptKit.Tage[plan[i].Tag]} verschoben", x => x["plan"] = RezeptKit.PlanJson(RezeptKit.PlanStart(x), plan));
    }

    public void RzPlanPortions(ProgramDocument d, string id, int portionen)
    {
        var plan = RezeptKit.Plan(d.Data); var i = plan.FindIndex(p => p.Id == id); if (i < 0) return;
        plan[i] = plan[i] with { Portionen = Math.Clamp(portionen, 1, RezeptKit.MaxPortionen) };
        d.Edit("Portionen im Plan", x => x["plan"] = RezeptKit.PlanJson(RezeptKit.PlanStart(x), plan));
    }

    /// <summary>Andere Woche: der Plan wandert mit (gleiche Wochentage), abgehakte Einträge der Liste werden zurückgesetzt.</summary>
    public void RzWeek(ProgramDocument d, int delta)
    {
        var start = RezeptKit.PlanStart(d.Data).AddDays(7 * delta);
        d.Edit(delta > 0 ? "Nächste Woche" : "Vorige Woche", x =>
        {
            x["plan"] = RezeptKit.PlanJson(start, RezeptKit.Plan(x));
            x["liste"] = new JsonObject { ["abgehakt"] = new JsonArray(), ["extra"] = new JsonArray([.. RezeptKit.Extra(x).Select(e => (JsonNode)e)]) };
        });
    }

    public void RzPlanClear(ProgramDocument d)
    {
        if (RezeptKit.Plan(d.Data).Count == 0) return;
        d.Edit("Wochenplan geleert", x => { x["plan"] = RezeptKit.PlanJson(RezeptKit.PlanStart(x), []); x["liste"] = new JsonObject { ["abgehakt"] = new JsonArray(), ["extra"] = new JsonArray([.. RezeptKit.Extra(x).Select(e => (JsonNode)e)]) }; });
    }

    // ---------------- Einkaufsliste ----------------

    public void RzToggle(ProgramDocument d, string key)
    {
        var done = RezeptKit.Abgehakt(d.Data);
        var on = done.Add(key); if (!on) done.Remove(key);
        d.Edit(on ? "Abgehakt" : "Haken entfernt", x => (x["liste"] ??= new JsonObject())["abgehakt"] = new JsonArray([.. done.Order().Select(k => (JsonNode)k)]));
    }

    public void RzAddExtra(ProgramDocument d, string text)
    {
        var t = (text ?? "").Trim(); if (t.Length == 0) throw new ArgumentException("Was soll noch auf die Liste?");
        var extra = RezeptKit.Extra(d.Data); if (extra.Count >= 200) throw new InvalidOperationException("Höchstens 200 eigene Einträge.");
        extra.Add(t.Length > 200 ? t[..200] : t);
        d.Edit("Auf die Liste", x => (x["liste"] ??= new JsonObject())["extra"] = new JsonArray([.. extra.Select(e => (JsonNode)e)]));
    }

    public void RzRemoveExtra(ProgramDocument d, int index)
    {
        var extra = RezeptKit.Extra(d.Data); if (index < 0 || index >= extra.Count) return;
        extra.RemoveAt(index);
        d.Edit("Von der Liste", x => (x["liste"] ??= new JsonObject())["extra"] = new JsonArray([.. extra.Select(e => (JsonNode)e)]));
    }

    public void RzResetChecks(ProgramDocument d)
    {
        if (RezeptKit.Abgehakt(d.Data).Count == 0) return;
        d.Edit("Haken entfernt", x => (x["liste"] ??= new JsonObject())["abgehakt"] = new JsonArray());
    }

    // ---------------- Ausgabe ----------------

    /// <summary>Datei erst daneben schreiben, dann in einem Schritt ersetzen – ein Abbruch hinterlässt nie eine halbe Datei.</summary>
    static string RzAtomic(string file, Action<string> write)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        var tmp = file + ".schreibt-" + Guid.NewGuid().ToString("N")[..6] + Path.GetExtension(file);
        try { write(tmp); File.Move(tmp, file, true); }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
        return file;
    }

    /// <summary>Das Rezept, das gedruckt wird – in der angezeigten Portionenzahl.</summary>
    Rezept RzForPrint(ProgramDocument d) { var r = RzNeed(d); var n = RzViewPortions(d); return n == r.Portionen ? r : RezeptKit.Skaliere(r, n); }

    static string RzText(ProgramDocument d, string file, TextDoc doc, string ext) => RzAtomic(file, f =>
    {
        switch (ext)
        {
            case ".docx": DocxFormat.Write(doc, f); break;
            case ".odt": OdtFormat.Write(doc, f); break;
            default: TextPdf.Export(doc, f); break;
        }
    });

    List<Rezept> RzAllOrThrow(ProgramDocument d) { var l = RzRezepte(d); return l.Count > 0 ? l : throw new InvalidOperationException("Das Rezeptbuch ist noch leer."); }

    // ---------------- KI-Fachaktion ----------------

    /// <summary>„Rezept anlegen“: Die KI liefert Name, Portionen, Zutaten (eine je Zeile) und Schritte; der eigene Kern liest die Zutaten und rechnet die Nährwerte.
    /// Vorschau → „Ja, behalten“ → EIN Rückgängig-Schritt. Keine Datei.</summary>
    ProgramKiAction RzKiRecipe => new(new AssistAction("rezept.anlegen", "Rezept ins Rezeptbuch schreiben (zutaten = eine Zutat je Zeile mit Menge und Einheit, schritte = ein Arbeitsschritt je Zeile)",
        ["rezept", "kochen", "backen", "gericht", "essen", "zutaten", "anlegen", "erstellen", "schreib", "mach", "vorschlag", "idee", "aus", "text"],
        [new AssistParam("name", "Name des Gerichts", Words: ["name"]), new AssistParam("portionen", "Portionen", Default: "4", Required: false, Words: ["portionen", "personen"]),
         new AssistParam("zutaten", "Zutaten, eine je Zeile („200 g Mehl“)", Max: 8000, Multiline: true), new AssistParam("schritte", "Arbeitsschritte, einer je Zeile", Max: 12_000, Multiline: true),
         new AssistParam("beschreibung", "Ein Satz zum Gericht", Required: false)]),
        (d, a) =>
        {
            var name = (a.GetValueOrDefault("name") ?? "").Trim(); var hinweise = new List<string>();
            var zutaten = (a.GetValueOrDefault("zutaten") ?? "").Replace("\\n", "\n").Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0).Take(RezeptKit.MaxZutaten).Select(l => RezeptKit.LiesMitPruefvermerk(l, hinweise)).Where(z => z.Name.Length > 0).ToList(); // mehrdeutige Mengen: „Menge prüfen“ im Hinweis der Zutat
            var schritte = (a.GetValueOrDefault("schritte") ?? "").Replace("\\n", "\n").Replace("\r", "").Split('\n').Select(s => System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"^\d+[.)]\s*", "")).Where(s => s.Length > 0).Take(RezeptKit.MaxSchritte).ToList();
            if (name.Length == 0 || zutaten.Count == 0 || schritte.Count == 0) throw new InvalidOperationException("Die KI hat kein vollständiges Rezept geliefert (Name, Zutaten und Schritte). Nichts übernommen.");
            var n = int.TryParse(System.Text.RegularExpressions.Regex.Match(a.GetValueOrDefault("portionen") ?? "", @"\d+").Value, out var p) ? Math.Clamp(p, 1, 100) : 4;
            var r = new Rezept(RezeptKit.NewId("r"), name.Length > 200 ? name[..200] : name, n, zutaten, schritte) { Beschreibung = (a.GetValueOrDefault("beschreibung") ?? "").Trim() is { } b && b.Length > 600 ? b[..600] : (a.GetValueOrDefault("beschreibung") ?? "").Trim() };
            if (RezeptKit.Gesundheitsversprechen(RezeptKit.AllerText(r)) is { Count: > 0 } v) // auch Zutaten und Hinweise (Gegenprüfung T-0787)
                throw new InvalidOperationException(v[0].StartsWith(RezeptKit.Zeitgrenze, StringComparison.Ordinal) ? v[0] + " – nicht übernommen." : $"Die KI hat ein Gesundheitsversprechen geschrieben („{v[0]}“) – Trendo nimmt das nicht an.");
            var list = RzRezepte(d); list.Add(r); RzSave(d, list, "Rezept: " + r.Name);
            _rzSel[d.Id] = r.Id; _rzMode[d.Id] = 0; _rzView.Remove(d.Id);
            var nw = RezeptKit.JePortion(r);
            _progMsg[RzKey] = $"KI: „{r.Name}“ mit {zutaten.Count} Zutaten und {schritte.Count} Schritten" + (nw.Erfasst > 0 ? $" – etwa {Math.Round(nw.Kcal)} kcal je Portion (Richtwert, gerechnet von Trendo)." : ".") + (hinweise.Count > 0 ? $" ⚠ {hinweise.Count} Menge(n) mehrdeutig, in den Zutaten mit „Menge prüfen“ markiert: {hinweise[0]}" : "")
                + (RezeptKit.Gesundheitshinweise(RezeptKit.AllerText(r)) is { Count: > 0 } weich ? $" ⚠ Bitte prüfen: „{weich[0]}“ – Trendo macht keine Gesundheitsaussagen." : "");
        },
        (_, _) => []) { MakesFile = false, OnApplyNote = "Erst bei „Ja, behalten“ kommt das Rezept ins Rezeptbuch – ein Rückgängig-Schritt nimmt es wieder heraus. Die Nährwerte rechnet Trendo selbst." };

    ProgramSpec RzSpec() => new(RzKey, RzStudio, "Rezepte & Ernährung")
    {
        Sentence = "Rezeptbuch mit Zutaten, Schritten und Portionen zum Umrechnen, Nährwerten je Portion (Richtwerte), Wochenplan und zusammengefasster Einkaufsliste – Ein- und Ausgabe als schema.org-Rezept.",
        Folder = "Rezepte",
        FillHeight = true,
        Templates =
        [
            new("leer", "Leeres Rezeptbuch", "Mit dem ersten Rezept beginnen", RezeptKit.Neu),
            new("beispiel", "Beispiel: eine Woche", "Drei Rezepte, Wochenplan und Einkaufsliste", RzSample),
        ],
        Ribbon =
        [
            ("Rezept", [
                new ProgramCmd("rz-new", "Neues Rezept", d => RzNew(d), Primary: true, Tip: "Leeres Rezept anlegen") { NoKi = true },
                new ProgramCmd("rz-import", "Rezept öffnen …", d => _ = RzPickImport(d), Tip: "schema.org-Rezept (JSON-LD) oder gespeicherte Rezeptseite (HTML) einlesen – auch per Ziehen auf die Arbeitsfläche") { NoKi = true },
                new ProgramCmd("rz-dup", "Kopieren", d => RzDuplicate(d), Level: 1) { NoKi = true },
                new ProgramCmd("rz-del", "Löschen", d => { if (RzSelected(d) is { } id) RzDelete(d, id); }, Level: 1) { NoKi = true },
            ]),
            ("Umrechnen", [
                new ProgramCmd("rz-half", "Halbe Menge", d => RzApplyScale(d, Math.Max(1, (int)Math.Round(RzNeed(d).Portionen / 2.0))), Level: 1) { NoKi = true },
                new ProgramCmd("rz-double", "Doppelte Menge", d => RzApplyScale(d, RzNeed(d).Portionen * 2), Level: 1) { NoKi = true },
                new ProgramCmd("rz-scale", "Portionen übernehmen", d => RzApplyScale(d), Tip: "Die angezeigten Portionen ins Rezept übernehmen (alle Mengen umgerechnet)") { NoKi = true },
                new ProgramCmd("rz-metric", "In g/ml umrechnen", d => RzMetric(d), Tip: "Esslöffel, Teelöffel und Tassen in Gramm bzw. Milliliter", Level: 1) { NoKi = true },
            ]),
            ("Woche", [
                new ProgramCmd("rz-to-plan", "Auf den Plan", d => { var p = RezeptKit.Plan(d.Data); var tag = Enumerable.Range(0, 7).FirstOrDefault(t => p.All(x => x.Tag != t)); RzPlanAdd(d, RzNeed(d).Id, tag); }, Tip: "Das gewählte Rezept auf den nächsten freien Tag legen") { NoKi = true },
                new ProgramCmd("rz-show-plan", "Wochenplan", d => RzSetMode(d, 1)) { NoKi = true },
                new ProgramCmd("rz-show-list", "Einkaufsliste", d => RzSetMode(d, 2)) { NoKi = true },
                new ProgramCmd("rz-week-next", "Nächste Woche", d => RzWeek(d, 1), Level: 1) { NoKi = true },
                new ProgramCmd("rz-plan-clear", "Plan leeren", d => RzPlanClear(d), Level: 2) { NoKi = true },
            ]),
        ],
        Exports =
        [
            new("pdf", "Rezept drucken (PDF)", ".pdf", (d, f) => RzText(d, f, RezeptKit.Druck(RzForPrint(d)), ".pdf")),
            new("woche", "Wochenplan und Einkaufsliste (PDF)", ".pdf", (d, f) => RzText(d, Path.Combine(Path.GetDirectoryName(f)!, Path.GetFileNameWithoutExtension(f) + " – Woche.pdf"), RezeptKit.WochenDruck(d.Data), ".pdf")),
            new("jsonld", "schema.org-Rezept (JSON-LD)", ".json", (d, f) => RzAtomic(f, t => AtomicFile.WriteAllText(t, RezeptKit.JsonLd(RzAllOrThrow(d))))),
            new("html", "Rezeptseite (HTML mit JSON-LD)", ".html", (d, f) => RzAtomic(f, t => AtomicFile.WriteAllText(t, RezeptKit.Html(RzAllOrThrow(d))))),
            new("kochbuch", "Kochbuch mit allen Rezepten (PDF)", ".pdf", (d, f) => RzText(d, Path.Combine(Path.GetDirectoryName(f)!, Path.GetFileNameWithoutExtension(f) + " – Kochbuch.pdf"), RezeptKit.Kochbuch(RzAllOrThrow(d), d.Title is { Length: > 0 } t && t != "Unbenannt" ? t : "Mein Kochbuch"), ".pdf")),
            new("docx", "Rezept als Word-Dokument (.docx)", ".docx", (d, f) => RzText(d, f, RezeptKit.Druck(RzForPrint(d)), ".docx")),
            new("odt", "Rezept als OpenDocument (.odt)", ".odt", (d, f) => RzText(d, f, RezeptKit.Druck(RzForPrint(d)), ".odt")),
            new("liste-txt", "Einkaufsliste als Text (.txt)", ".txt", (d, f) => RzAtomic(Path.Combine(Path.GetDirectoryName(f)!, Path.GetFileNameWithoutExtension(f) + " – Einkaufsliste.txt"), t => AtomicFile.WriteAllText(t, RezeptKit.EinkaufText(d.Data)))),
            new("liste-csv", "Einkaufsliste als Tabelle (.csv)", ".csv", (d, f) => RzAtomic(Path.Combine(Path.GetDirectoryName(f)!, Path.GetFileNameWithoutExtension(f) + " – Einkaufsliste.csv"), t => AtomicFile.WriteAllText(t, RezeptKit.EinkaufCsv(d.Data)))),
        ],
        SimpleExports = 3,
        KiActions = [RzKiRecipe],
        KiLocked = ["plan", "liste"],
        KiCheck = RezeptKit.KiCheck,
        KiHint = "Ein neues Rezept schreibst du mit „rezept.anlegen“: name, portionen (Zahl), zutaten (eine Zutat je Zeile mit Menge und Einheit, z. B. „200 g Weizenmehl“, „2 EL Olivenöl“, „1 Zwiebel“), "
               + "schritte (ein Arbeitsschritt je Zeile, ganze Sätze), beschreibung (ein sachlicher Satz). Je Auftrag genau diese eine Aktion. Steht im Auftrag ein Rezepttext, übernimm dessen Zutaten und Schritte. "
               + "Keine Nährwerte, Kalorien oder Gesundheitsversprechen schreiben (nichts wie „gesund“, „heilt“, „entgiftet“, „zum Abnehmen“) – die Nährwerte rechnet Trendo selbst als Richtwert. Wochenplan und Einkaufsliste ändert nur die Person.",
        FirstStep = new("Erstes Rezept anlegen", "Leg ein Rezept an – oder zieh eine Rezept-Datei (JSON-LD oder gespeicherte Rezeptseite) hierher. Wochenplan und Einkaufsliste folgen oben.",
            d => RzNew(d), d => RzRezepte(d).Count == 0),
        Workspace = RzWorkspace,
        Properties = RzProperties,
        Status = d =>
        {
            var l = RzRezepte(d); var plan = RezeptKit.Plan(d.Data);
            var cur = RzCurrent(d); var nw = cur is null ? null : RezeptKit.JePortion(cur);
            return $"{l.Count} Rezepte · {plan.Count} im Wochenplan · {RezeptKit.Einkaufsliste(d.Data).Count} auf der Einkaufsliste" + (nw is { Erfasst: > 0 } ? $" · „{cur!.Name}“: ≈ {Math.Round(nw.Kcal)} kcal je Portion (Richtwert)" : "");
        },
        Help = "Oben wählst du die Ansicht: Rezepte, Wochenplan oder Einkaufsliste. Zutaten schreibst du wie im Kochbuch, eine je Zeile („200 g Mehl“, „1/2 TL Salz“, „2 Eier, Größe M“, „Für den Teig:“ als Zwischenüberschrift). "
             + "Trendo erkennt Menge, Einheit und Zutat und ordnet sie der Nährwert-Tabelle zu – Rechtsklick auf eine Zutat wählt ein anderes Lebensmittel. Mit „–“/„+“ zeigst du das Rezept für mehr oder weniger Portionen; "
             + "„Portionen übernehmen“ rechnet es um. Einheiten: 1 EL = 15 ml, 1 TL = 5 ml, 1 Tasse = 250 ml, 1 Prise ≈ 0,5 g. "
             + "Nährwerte sind Richtwerte aus der Tabelle USDA FoodData Central (SR Legacy, gemeinfrei, CC0 1.0) für rohe Zutaten, ohne Garverluste – keine Ernährungs- oder Gesundheitsberatung. "
             + "Gesundheitsaussagen prüft Trendo in zwei Stufen: Eindeutige Versprechen (z. B. „heilt Erkältungen“, „senkt den Blutzucker“, „zum Abnehmen geeignet“, „Superfood“, „Detox“) nimmt Trendo von der KI nicht an; "
             + "weiche Wörter wie „gesund“, „gesündere Variante“ oder Aussagen über Stoffwechsel, Leber, Haut ergeben nur „Bitte prüfen“ – lieber ein Hinweis als ein abgelehntes Rezept. Kochsätze wie „hilft gegen das Ausflocken“ oder „vom Herd abnehmen“ bleiben frei. Die Prüfung nutzt Wortlisten und ist nicht vollständig. "
             + "Englische Rezepte: cups, tbsp, tsp, oz, lb, fl oz, pint, quart, can, stick (Butter 113 g) und pinch werden umgerechnet („8 oz milk“: Trendo fragt, ob fl oz gemeint ist), häufige englische Zutatennamen (flour, sugar, egg, milk …) zugeordnet – was nicht zugeordnet ist, meldet Trendo beim Einlesen. "
             + "Mengen mit Punkt: „1.000,5 g“ = 1000,5 g; „0.250 kg“ = 0,25 kg; „1.500 kg/l“ = 1,5; „1.500 g/ml/Stück“ = 1500 (gilt auch für Bereiche wie „1.500-2.000 g“); „1,000 g/ml“ = 1000 – Punkt bzw. Komma mit drei Ziffern meldet Trendo zum Prüfen. "
             + "Im Wochenplan legst du Rezepte auf Tage; die Einkaufsliste fasst gleiche Zutaten zusammen und sortiert nach Abteilung im Laden. Ausgabe als PDF zum Drucken, Word, OpenDocument, "
             + "schema.org-Rezept (JSON-LD, liest z. B. Mealie oder Tandoor) und Rezeptseite. Einlesen: JSON-LD-Datei oder gespeicherte Rezeptseite öffnen oder auf die Arbeitsfläche ziehen.",
    };

    static JsonObject RzSample()
    {
        var d = RezeptKit.Neu();
        Rezept R(string id, string name, int p, string zutaten, string schritte, string beschreibung, int vor, int gar, string kat) =>
            new(id, name, p, [.. zutaten.Split('\n').Select(RezeptKit.LiesZutat)], [.. schritte.Split('\n')]) { Beschreibung = beschreibung, Vorbereitung = vor, Garzeit = gar, Kategorie = kat };
        var a = R("r-pfannkuchen", "Pfannkuchen", 4, "250 g Weizenmehl\n500 ml Milch\n3 Eier\n1 Prise Salz\n2 EL Butter, zum Ausbacken",
            "Mehl, Milch, Eier und Salz zu einem glatten Teig verrühren und 10 Minuten quellen lassen.\nEtwas Butter in einer Pfanne erhitzen, eine Kelle Teig hineingeben und dünn verteilen.\nVon beiden Seiten goldbraun backen und warm servieren.",
            "Dünne Pfannkuchen für süß oder herzhaft.", 15, 20, "Hauptgericht");
        var b = R("r-gemuesesuppe", "Gemüsesuppe", 4, "2 Karotten\n2 Kartoffeln\n1 Stange Lauch\n1 Zwiebel\n1 EL Rapsöl\n1 l Gemüsebrühe\nSalz und Pfeffer nach Geschmack\n1 Bund Petersilie",
            "Gemüse schälen und in kleine Würfel schneiden.\nZwiebel im Öl glasig dünsten, übriges Gemüse kurz mitdünsten.\nMit Brühe aufgießen und 20 Minuten köcheln lassen.\nAbschmecken und mit gehackter Petersilie servieren.",
            "Einfache Suppe aus Wintergemüse.", 20, 25, "Suppe");
        var c = R("r-tomatenspaghetti", "Spaghetti mit Tomatensoße", 2, "200 g Spaghetti\n1 Dose gehackte Tomaten\n1 Zwiebel\n1 Knoblauchzehe\n2 EL Olivenöl\n1 TL Zucker\n30 g Parmesan, gerieben",
            "Spaghetti nach Packungsangabe in Salzwasser garen.\nZwiebel und Knoblauch fein würfeln und im Öl andünsten.\nTomaten und Zucker dazugeben und 15 Minuten köcheln lassen.\nSoße mit den Nudeln mischen und mit Parmesan bestreuen.",
            "Schnelles Nudelgericht.", 10, 20, "Hauptgericht");
        d["rezepte"] = RezeptKit.RezepteJson([a, b, c]);
        d["plan"] = RezeptKit.PlanJson(DateTime.Today, [new("p1", 0, "Mittag", b.Id, 4), new("p2", 2, "Abend", c.Id, 4), new("p3", 5, "Frühstück", a.Id, 2)]);
        return d;
    }

    // ---------------- Arbeitsfläche ----------------

    Control RzWorkspace(ProgramDocument d)
    {
        var mode = RzModeOf(d);
        var bar = new WrapPanel { Name = "rz-modes", Margin = new Thickness(0, 0, 0, 8) };
        for (var i = 0; i < RzModes.Length; i++)
        {
            var ii = i;
            var count = i switch { 0 => RzRezepte(d).Count, 1 => RezeptKit.Plan(d.Data).Count, _ => RezeptKit.Einkaufsliste(d.Data).Count };
            var b = new Button { Name = "rz-mode-" + i, Content = $"{RzModes[i]} ({count})", Padding = new Thickness(14, 5), Margin = new Thickness(0, 0, 6, 0),
                Background = i == mode ? Brush.Parse("#3AD4A017") : Brush.Parse("#151A22"), BorderBrush = i == mode ? Accent : Brush.Parse("#22FFFFFF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14) };
            b.Click += (_, _) => { _rzMode[d.Id] = ii; Show(RzKey); };
            bar.Children.Add(b);
        }
        var body = mode switch { 1 => RzPlanView(d), 2 => RzListView(d), _ => RzRecipeView(d) };
        var grid = new Grid { Name = "rz-work", RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Thickness(4, 0, 10, 4) };
        grid.Children.Add(bar);
        Grid.SetRow(body, 1); grid.Children.Add(body);
        // Ablegen nimmt denselben Weg wie „Rezept öffnen …“: im Hintergrund lesen, Sperre, Prüfung, ob das Buch noch offen ist
        BueroFileDrop(grid, RzKey, BueroExt(".json", ".jsonld", ".html", ".htm"), "Rezept-Dateien (JSON-LD oder gespeicherte Rezeptseite)", (doc, files) => _ = RzImportFilesAsync(doc, files));
        return grid;
    }

    Control RzRecipeView(ProgramDocument d)
    {
        var list = RzRezepte(d); var sel = RzSelected(d);
        // Liste links mit Suche
        var left = new StackPanel { Name = "rz-list", Spacing = 3 };
        left.Children.Add(H("Rezepte", 13));
        var filter = _rzFilter.GetValueOrDefault(d.Id, "");
        var search = new TextBox { Name = "rz-search", Text = filter, PlaceholderText = "Suchen (Name, Zutat, Stichwort)", Margin = new Thickness(0, 0, 0, 4) };
        search.TextChanged += (_, _) => { var t = search.Text ?? ""; if (t == _rzFilter.GetValueOrDefault(d.Id, "")) return; _rzFilter[d.Id] = t; Show(RzKey); };
        left.Children.Add(search);
        if (list.Count == 0) left.Children.Add(P("Noch kein Rezept – „Neues Rezept“ oben, oder eine Rezept-Datei hierher ziehen.", Muted, 11.5));
        var rows = new StackPanel { Name = "rz-list-rows", Spacing = 2 };
        for (var i = 0; i < list.Count; i++)
        {
            var r = list[i]; var idx = i;
            if (filter.Trim().Length > 0 && !(r.Name + " " + r.Stichwoerter + " " + r.Kategorie + " " + string.Join(" ", r.Zutaten.Select(z => z.Name))).Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            var nw = RezeptKit.JePortion(r);
            var row = new Button
            {
                Name = "rz-item-" + i, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 5),
                Background = r.Id == sel ? Brush.Parse("#33D4A017") : Brushes.Transparent, BorderBrush = r.Id == sel ? Accent : Brushes.Transparent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Content = new StackPanel { Spacing = 1, Children = {
                    new TextBlock { Text = r.Name.Length > 0 ? r.Name : "(ohne Namen)", Foreground = Text, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = $"{r.Portionen} {r.PortionWort}" + (r.Vorbereitung + r.Garzeit > 0 ? $" · {r.Vorbereitung + r.Garzeit} min" : "") + (nw.Erfasst > 0 ? $" · ≈ {Math.Round(nw.Kcal)} kcal" : ""), Foreground = Muted, FontSize = 10.5 } } },
            };
            row.Click += (_, _) => { RzSelect(d, r.Id); Show(RzKey); };
            row.ContextMenu = BueroMenu(RzKey, "rz-item-menu-" + i, () => RzSelect(d, r.Id),
                ("rz-m-plan", "Auf den Wochenplan", () => { var p = RezeptKit.Plan(d.Data); RzPlanAdd(d, r.Id, Enumerable.Range(0, 7).FirstOrDefault(t => p.All(x => x.Tag != t))); }, true),
                ("rz-m-dup", "Kopieren", () => RzDuplicate(d), true),
                ("rz-m-up", "Nach oben", () => RzMove(d, idx, idx - 1), idx > 0),
                ("rz-m-down", "Nach unten", () => RzMove(d, idx, idx + 1), idx < list.Count - 1),
                ("-", "", () => { }, true),
                ("rz-m-del", "Löschen", () => RzDelete(d, r.Id), true));
            if (filter.Trim().Length == 0) BueroReorder(rows, row, rows.Children.Count, RzKey, (from, to) => RzMove(d, from, to));
            rows.Children.Add(row);
        }
        left.Children.Add(rows);
        var cur = list.FirstOrDefault(r => r.Id == sel);
        var center = cur is null ? RzEmptyCenter(d) : RzEditor(d, cur);
        var leftCard = KrCard(new ScrollViewer { Content = left, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }, "rz-list-card");
        var mid = KrCard(new ScrollViewer { Content = center, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }, "rz-editor-card");
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("240,12,*") };
        grid.Children.Add(leftCard);
        Grid.SetColumn(mid, 2); grid.Children.Add(mid);
        return grid;
    }

    Control RzEmptyCenter(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "rz-empty", Spacing = 8, MaxWidth = 700, HorizontalAlignment = HorizontalAlignment.Left };
        sp.Children.Add(H("Rezept aus Text übernehmen", 15));
        sp.Children.Add(P("Rezept aus einer Mail oder Webseite kopieren und hier einfügen: erste Zeile = Name, darunter Zutaten und Zubereitung. Trendo erkennt Mengen, Einheiten und Schritte.", Muted, 12));
        sp.Children.Add(RzPasteBox(d));
        return sp;
    }

    Control RzPasteBox(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 6 };
        var box = new TextBox { Name = "rz-paste", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 140, PlaceholderText = "Tomatensoße\nFür 4 Portionen\n1 Zwiebel\n2 EL Olivenöl\n…\nZubereitung\n1. Zwiebel würfeln …" };
        var go = new Button { Name = "rz-paste-apply", Content = "Als Rezept übernehmen", Padding = new Thickness(12, 5) };
        go.Click += (_, _) => ProgramRun(RzKey, () => RzFromText(d, box.Text ?? ""));
        sp.Children.Add(box); sp.Children.Add(go);
        return sp;
    }

    Control RzEditor(ProgramDocument d, Rezept r)
    {
        var sp = new StackPanel { Name = "rz-editor", Spacing = 6, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        TextBox Field(string field, string name, string value, string label, bool multi = false, double? width = null)
        {
            var shown = value;
            var t = new TextBox { Name = name, Text = value, PlaceholderText = label, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multi ? 70 : 0 };
            if (width is { } w) { t.Width = w; t.HorizontalAlignment = HorizontalAlignment.Left; }
            void Commit() { var now = t.Text ?? ""; if (now == shown) return; shown = now; ProgramRun(RzKey, () => RzSetField(d, r.Id, field, now)); }
            t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
            return t;
        }
        var title = Field("name", "rz-name", r.Name, "Name des Rezepts"); title.FontSize = 18; title.FontWeight = FontWeight.SemiBold;
        sp.Children.Add(title);
        sp.Children.Add(Field("beschreibung", "rz-beschreibung", r.Beschreibung, "Kurze Beschreibung (optional)", multi: true));
        // Portionen und Zeiten
        var facts = new WrapPanel { Name = "rz-facts" };
        Control Labeled(string label, Control c) => new StackPanel { Margin = new Thickness(0, 0, 14, 4), Spacing = 2, Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 11 }, c } };
        facts.Children.Add(Labeled("Rezept für", Field("portionen", "rz-portionen", r.Portionen.ToString(RezeptKit.De), "4", width: 60)));
        facts.Children.Add(Labeled("Wort", Field("portionWort", "rz-portionwort", r.PortionWort, "Portionen", width: 110)));
        facts.Children.Add(Labeled("Vorbereitung (min)", Field("vorbereitung", "rz-vorbereitung", r.Vorbereitung.ToString(RezeptKit.De), "0", width: 70)));
        facts.Children.Add(Labeled("Garzeit (min)", Field("garzeit", "rz-garzeit", r.Garzeit.ToString(RezeptKit.De), "0", width: 70)));
        facts.Children.Add(Labeled("Kategorie", Field("kategorie", "rz-kategorie", r.Kategorie, "z. B. Suppe", width: 140)));
        facts.Children.Add(Labeled("Stichwörter", Field("stichwoerter", "rz-stichwoerter", r.Stichwoerter, "schnell, vegetarisch", width: 200)));
        sp.Children.Add(facts);

        // Zutaten: links Text (wie im Kochbuch), rechts erkannt und umgerechnet
        sp.Children.Add(H("Zutaten", 14));
        sp.Children.Add(P("Eine Zutat je Zeile, z. B. „200 g Mehl“, „1/2 TL Salz“, „2 Eier, Größe M“. Zwischenüberschrift mit Doppelpunkt („Für den Teig:“).", Muted, 11));
        var zShown = string.Join("\n", r.Zutaten.Select(z => RezeptKit.Zeile(z)));
        var zbox = new TextBox { Name = "rz-zutaten", Text = zShown, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120, FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"), FontSize = 12.5 };
        void CommitZ() { var now = zbox.Text ?? ""; if (now == zShown) return; zShown = now; ProgramRun(RzKey, () => RzSetZutaten(d, r.Id, now)); }
        zbox.LostFocus += (_, _) => CommitZ();
        sp.Children.Add(zbox);
        // Ansicht für n Portionen
        var view = RzViewPortions(d); var f = view / (double)Math.Max(1, r.Portionen);
        var scale = new StackPanel { Name = "rz-scale", Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 6, 0, 2) };
        scale.Children.Add(new TextBlock { Text = "Anzeigen für", Foreground = Text, VerticalAlignment = VerticalAlignment.Center });
        var minus = new Button { Name = "rz-view-minus", Content = "–", Padding = new Thickness(10, 2) }; minus.Click += (_, _) => { RzSetView(d, view - 1); Show(RzKey); };
        var plus = new Button { Name = "rz-view-plus", Content = "+", Padding = new Thickness(10, 2) }; plus.Click += (_, _) => { RzSetView(d, view + 1); Show(RzKey); };
        ToolTip.SetTip(minus, "Eine Portion weniger"); ToolTip.SetTip(plus, "Eine Portion mehr");
        scale.Children.Add(minus);
        scale.Children.Add(new TextBlock { Name = "rz-view", Text = $"{view} {r.PortionWort}", Foreground = view == r.Portionen ? Text : Accent, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, MinWidth = 90, TextAlignment = TextAlignment.Center });
        scale.Children.Add(plus);
        if (view != r.Portionen)
        {
            var apply = new Button { Name = "rz-view-apply", Content = $"So ins Rezept übernehmen", Padding = new Thickness(10, 2) };
            apply.Click += (_, _) => ProgramRun(RzKey, () => RzApplyScale(d));
            scale.Children.Add(apply);
            scale.Children.Add(new TextBlock { Text = $"(Rezept ist für {r.Portionen})", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        }
        sp.Children.Add(scale);
        // erkannte Zutaten als Tabelle
        var table = new Grid { Name = "rz-zt-table", ColumnDefinitions = new ColumnDefinitions("110,*,200,70,70"), Margin = new Thickness(0, 2, 0, 0) };
        void Cell(int row, int col, string text, IBrush? fg = null, string? name = null, bool bold = false)
        {
            var tb = new TextBlock { Name = name, Text = text, Foreground = fg ?? Text, FontSize = 12, FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 8, 2), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(tb, row); Grid.SetColumn(tb, col); table.Children.Add(tb);
        }
        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Cell(0, 0, "Menge", Muted); Cell(0, 1, "Zutat", Muted); Cell(0, 2, "gerechnet als", Muted); Cell(0, 3, "Gramm", Muted); Cell(0, 4, "kcal", Muted);
        for (var i = 0; i < r.Zutaten.Count; i++)
        {
            var z = r.Zutaten[i]; var ii = i; var row = i + 1;
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            if (z.Gruppe) { Cell(row, 0, z.Name + ":", Accent, bold: true); continue; }
            var lm = RezeptKit.LmVon(z); var g = RezeptKit.Gramm(z, f);
            Cell(row, 0, RezeptKit.MengeText(z, f), Text, "rz-zt-menge-" + i, bold: true);
            Cell(row, 1, z.Name + (z.Notiz.Length > 0 ? ", " + z.Notiz : ""));
            var pick = new Button { Name = "rz-zt-" + i, Padding = new Thickness(6, 1), Margin = new Thickness(0, 1, 8, 1), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent, BorderBrush = Brush.Parse("#22FFFFFF"), BorderThickness = new Thickness(1),
                Content = new TextBlock { Text = lm?.Name ?? (z.Lm == Zutat.KeinLm ? "– nicht mitgerechnet" : "? nicht zugeordnet"), Foreground = lm is null ? K4Warn : Text, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis } };
            ToolTip.SetTip(pick, lm is null ? "Klick oder Rechtsklick: Lebensmittel aus der Tabelle wählen" : $"{lm.Name} – {lm.Kcal.ToString("0", RezeptKit.De)} kcal je 100 g (USDA FDC {lm.FdcId}). Klick oder Rechtsklick: anderes Lebensmittel");
            var cands = NaehrwertTabelle.Kandidaten(z.Name, 6).Where(c => c.Id != lm?.Id).ToList();
            var items = cands.Select((c, k) => ($"rz-lm-{i}-{k}", c.Name, (Action)(() => RzSetLm(d, r.Id, ii, c.Id)), true)).ToList();
            items.Add(("-", "", () => { }, true));
            items.Add(($"rz-lm-{i}-none", "Nicht mitrechnen", () => RzSetLm(d, r.Id, ii, Zutat.KeinLm), z.Lm != Zutat.KeinLm));
            pick.ContextMenu = BueroMenu(RzKey, "rz-zt-menu-" + i, null, [.. items]);
            pick.Click += (_, _) => pick.ContextMenu.Open(pick);
            Grid.SetRow(pick, row); Grid.SetColumn(pick, 2); table.Children.Add(pick);
            Cell(row, 3, g is { } gg ? Math.Round(gg).ToString("0", RezeptKit.De) : "–", Muted);
            Cell(row, 4, g is { } g2 && lm is not null ? Math.Round(Math.Max(0, lm.Kcal) * g2 / 100).ToString("0", RezeptKit.De) : "–", Muted);
        }
        if (r.Zutaten.Count > 0) sp.Children.Add(new Border { Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(6), Background = Brush.Parse("#0E1116"), Child = table });

        // Schritte
        sp.Children.Add(H("Zubereitung", 14));
        var sShown = string.Join("\n", r.Schritte);
        var sbox = new TextBox { Name = "rz-schritte", Text = sShown, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120, PlaceholderText = "Ein Arbeitsschritt je Zeile" };
        void CommitS() { var now = sbox.Text ?? ""; if (now == sShown) return; sShown = now; ProgramRun(RzKey, () => RzSetSchritte(d, r.Id, now)); }
        sbox.LostFocus += (_, _) => CommitS();
        sp.Children.Add(sbox);
        sp.Children.Add(H("Notizen und Quelle", 13));
        sp.Children.Add(Field("notizen", "rz-notizen", r.Notizen, "Eigene Notizen (optional)", multi: true));
        sp.Children.Add(Field("quelle", "rz-quelle", r.Quelle, "Quelle, z. B. Buch oder Webadresse (optional)"));
        var hints = RezeptKit.Pruefe(r);
        if (hints.Count > 0) sp.Children.Add(new TextBlock { Name = "rz-hints", Text = string.Join("\n", hints.Select(h => "⚠ " + h)), Foreground = K4Warn, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
        var paste = new Expander { Name = "rz-paste-exp", Header = "Weiteres Rezept aus Text übernehmen", Content = RzPasteBox(d), Margin = new Thickness(0, 8, 0, 0) };
        sp.Children.Add(paste);
        return sp;
    }

    Control RzPlanView(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "rz-plan", Spacing = 8 };
        var start = RezeptKit.PlanStart(d.Data); var rezepte = RzRezepte(d).ToDictionary(r => r.Id); var plan = RezeptKit.Plan(d.Data);
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var prev = new Button { Name = "rz-week-prev", Content = "◀", Padding = new Thickness(10, 3) }; prev.Click += (_, _) => ProgramRun(RzKey, () => RzWeek(d, -1));
        var next = new Button { Name = "rz-week-nextbtn", Content = "▶", Padding = new Thickness(10, 3) }; next.Click += (_, _) => ProgramRun(RzKey, () => RzWeek(d, 1));
        ToolTip.SetTip(prev, "Vorige Woche"); ToolTip.SetTip(next, "Nächste Woche");
        head.Children.Add(prev);
        head.Children.Add(new TextBlock { Name = "rz-week", Text = $"Woche vom {start.ToString("d. MMMM", RezeptKit.De)} bis {start.AddDays(6).ToString("d. MMMM yyyy", RezeptKit.De)}", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        head.Children.Add(next);
        sp.Children.Add(head);
        // hinzufügen
        var add = new WrapPanel { Name = "rz-plan-add-row" };
        var rzBox = new ComboBox { Name = "rz-plan-rezept", MinWidth = 220, Margin = new Thickness(0, 0, 6, 4), PlaceholderText = "Rezept wählen" };
        foreach (var r in rezepte.Values) rzBox.Items.Add(new ComboBoxItem { Content = r.Name, Tag = r.Id });
        if (RzSelected(d) is { } selId) rzBox.SelectedIndex = rezepte.Keys.ToList().IndexOf(selId);
        var dayBox = new ComboBox { Name = "rz-plan-tag", MinWidth = 130, Margin = new Thickness(0, 0, 6, 4) };
        for (var t = 0; t < 7; t++) dayBox.Items.Add(new ComboBoxItem { Content = $"{RezeptKit.Tage[t]}, {start.AddDays(t).ToString("d. M.", RezeptKit.De)}" });
        dayBox.SelectedIndex = Enumerable.Range(0, 7).FirstOrDefault(t => plan.All(x => x.Tag != t));
        var mealBox = new ComboBox { Name = "rz-plan-mahlzeit", MinWidth = 110, Margin = new Thickness(0, 0, 6, 4) };
        foreach (var m in RezeptKit.Mahlzeiten) mealBox.Items.Add(new ComboBoxItem { Content = m });
        mealBox.SelectedIndex = 1;
        var portBox = new TextBox { Name = "rz-plan-portionen", Width = 60, PlaceholderText = "Port.", Margin = new Thickness(0, 0, 6, 4) };
        var addBtn = new Button { Name = "rz-plan-add", Content = "Auf den Plan", Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 6, 4) };
        addBtn.Click += (_, _) => ProgramRun(RzKey, () =>
        {
            if (rzBox.SelectedItem is not ComboBoxItem { Tag: string id }) throw new InvalidOperationException("Wähle zuerst ein Rezept.");
            int? n = int.TryParse(portBox.Text, out var pn) && pn > 0 ? pn : null;
            RzPlanAdd(d, id, Math.Max(0, dayBox.SelectedIndex), RezeptKit.Mahlzeiten[Math.Max(0, mealBox.SelectedIndex)], n);
        });
        add.Children.Add(rzBox); add.Children.Add(dayBox); add.Children.Add(mealBox); add.Children.Add(portBox); add.Children.Add(addBtn);
        if (rezepte.Count == 0) sp.Children.Add(P("Leg zuerst unter „Rezepte“ ein Rezept an – dann kommt es hier auf einen Tag.", Muted, 12));
        else sp.Children.Add(add);
        // sieben Tage
        var days = new Grid { Name = "rz-days", ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*,*") };
        var n = 0;
        for (var t = 0; t < 7; t++)
        {
            var col = new StackPanel { Name = "rz-day-" + t, Spacing = 4 };
            var isToday = start.AddDays(t) == DateTime.Today;
            col.Children.Add(new TextBlock { Text = RezeptKit.Tage[t], Foreground = isToday ? Accent : Text, FontWeight = FontWeight.SemiBold, FontSize = 12.5 });
            col.Children.Add(new TextBlock { Text = start.AddDays(t).ToString("d. MMM", RezeptKit.De), Foreground = Muted, FontSize = 10.5 });
            foreach (var p in plan.Where(p => p.Tag == t).OrderBy(p => Array.IndexOf(RezeptKit.Mahlzeiten, p.Mahlzeit)))
            {
                var r = rezepte[p.Rezept]; var pp = p; var k = n++;
                var card = new StackPanel { Spacing = 1 };
                card.Children.Add(new TextBlock { Text = p.Mahlzeit, Foreground = Muted, FontSize = 10 });
                card.Children.Add(new TextBlock { Text = r.Name, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
                card.Children.Add(new TextBlock { Text = $"{p.Portionen} {r.PortionWort}", Foreground = Muted, FontSize = 10.5 });
                var b = new Button { Name = "rz-pe-" + k, Content = card, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(6, 4),
                    Background = Brush.Parse("#151A22"), BorderBrush = Brush.Parse("#33D4A017"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6) };
                b.Click += (_, _) => { RzSelect(d, r.Id); _rzMode[d.Id] = 0; Show(RzKey); };
                ToolTip.SetTip(b, "Klick: Rezept zeigen · Rechtsklick: Tag, Portionen, entfernen");
                var menu = new List<(string, string, Action, bool)>();
                for (var t2 = 0; t2 < 7; t2++) { var tt = t2; if (tt != t) menu.Add(($"rz-pe-{k}-tag-{tt}", "Auf " + RezeptKit.Tage[tt], () => RzPlanMove(d, pp.Id, tt), true)); }
                menu.Add(("-", "", () => { }, true));
                menu.Add(($"rz-pe-{k}-plus", "Eine Portion mehr", () => RzPlanPortions(d, pp.Id, pp.Portionen + 1), true));
                menu.Add(($"rz-pe-{k}-minus", "Eine Portion weniger", () => RzPlanPortions(d, pp.Id, pp.Portionen - 1), pp.Portionen > 1));
                menu.Add(($"rz-pe-{k}-del", "Aus dem Plan nehmen", () => RzPlanRemove(d, pp.Id), true));
                b.ContextMenu = BueroMenu(RzKey, $"rz-pe-menu-{k}", null, [.. menu]);
                col.Children.Add(b);
            }
            var cell = new Border { Child = col, Padding = new Thickness(6), Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(8), MinHeight = 200,
                Background = Brush.Parse("#0E1116"), BorderBrush = isToday ? Accent : Brush.Parse("#22FFFFFF"), BorderThickness = new Thickness(1) };
            Grid.SetColumn(cell, t); days.Children.Add(cell);
        }
        sp.Children.Add(days);
        if (plan.Count > 0)
        {
            var go = new Button { Name = "rz-plan-to-list", Content = $"Zur Einkaufsliste ({RezeptKit.Einkaufsliste(d.Data).Count} Zutaten) →", Padding = new Thickness(12, 5) };
            go.Click += (_, _) => { _rzMode[d.Id] = 2; Show(RzKey); };
            sp.Children.Add(go);
        }
        return KrCard(new ScrollViewer { Content = sp, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }, "rz-plan-card");
    }

    Control RzListView(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "rz-liste", Spacing = 4, MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left };
        var start = RezeptKit.PlanStart(d.Data); var items = RezeptKit.Einkaufsliste(d.Data); var done = RezeptKit.Abgehakt(d.Data);
        sp.Children.Add(H($"Einkaufsliste für die Woche ab {start.ToString("d. MMMM", RezeptKit.De)}", 15));
        sp.Children.Add(P(items.Count == 0 ? "Noch leer – leg im Wochenplan Rezepte auf Tage, dann stehen die Zutaten hier zusammengefasst." : $"{items.Count} Zutaten aus {RezeptKit.Plan(d.Data).Count} geplanten Gerichten, gleiche Zutaten zusammengefasst, sortiert wie beim Gang durch den Laden. Abhaken per Klick.", Muted, 11.5));
        var n = 0;
        foreach (var g in items.GroupBy(p => p.Abteilung))
        {
            sp.Children.Add(new TextBlock { Name = "rz-li-group-" + Array.IndexOf(RezeptKit.Abteilungen, g.Key), Text = g.Key, Foreground = Accent, FontSize = 13, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 2) });
            foreach (var p in g)
            {
                var key = p.Key; var on = done.Contains(key);
                var cb = new CheckBox { Name = "rz-li-" + n++, IsChecked = on, Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = {
                    new TextBlock { Text = p.Text, Foreground = on ? Muted : Text, TextDecorations = on ? TextDecorations.Strikethrough : null, FontSize = 12.5 },
                    new TextBlock { Text = "für " + string.Join(", ", p.Fuer), Foreground = Muted, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center } } } };
                cb.IsCheckedChanged += (_, _) => { if ((cb.IsChecked == true) != on) ProgramRun(RzKey, () => RzToggle(d, key)); };
                sp.Children.Add(cb);
            }
        }
        var extra = RezeptKit.Extra(d.Data);
        sp.Children.Add(new TextBlock { Text = "Außerdem", Foreground = Accent, FontSize = 13, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 10, 0, 2) });
        for (var i = 0; i < extra.Count; i++)
        {
            var ii = i;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(new TextBlock { Text = "• " + extra[i], Foreground = Text, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center });
            var del = new Button { Name = "rz-li-extra-del-" + i, Content = "✕", Padding = new Thickness(6, 0) }; ToolTip.SetTip(del, "Von der Liste nehmen");
            del.Click += (_, _) => ProgramRun(RzKey, () => RzRemoveExtra(d, ii));
            row.Children.Add(del); sp.Children.Add(row);
        }
        var addRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var box = new TextBox { Name = "rz-li-extra", Width = 300, PlaceholderText = "Eigener Eintrag, z. B. Spülmittel" };
        var add = new Button { Name = "rz-li-extra-add", Content = "Dazu", Padding = new Thickness(10, 3) };
        void Add() => ProgramRun(RzKey, () => RzAddExtra(d, box.Text ?? ""));
        add.Click += (_, _) => Add(); OnEnter(box, Add);
        addRow.Children.Add(box); addRow.Children.Add(add); sp.Children.Add(addRow);
        if (done.Count > 0)
        {
            var reset = new Button { Name = "rz-li-reset", Content = "Alle Haken entfernen", Padding = new Thickness(10, 3), Margin = new Thickness(0, 8, 0, 0) };
            reset.Click += (_, _) => ProgramRun(RzKey, () => RzResetChecks(d));
            sp.Children.Add(reset);
        }
        return KrCard(new ScrollViewer { Content = sp, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }, "rz-liste-card");
    }

    Control RzProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 6 };
        var r = RzCurrent(d);
        sp.Children.Add(H("Nährwerte je Portion", 13));
        if (r is null) { sp.Children.Add(P("Erscheinen, sobald ein Rezept Zutaten hat.", Muted, 11)); }
        else
        {
            var view = RzViewPortions(d);
            var nw = RezeptKit.Berechne(r).Mal(1.0 / Math.Max(1, r.Portionen));
            sp.Children.Add(new TextBlock { Text = "Richtwerte – keine Ernährungsberatung", Name = "rz-nw-richtwert", Foreground = K4Warn, FontSize = 11, FontWeight = FontWeight.SemiBold });
            if (nw.Erfasst == 0) sp.Children.Add(P("Noch keine Zutat mit Menge und Zuordnung.", Muted, 11));
            else
            {
                var g = new Grid { Name = "rz-nw", ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                var i = 0;
                foreach (var (k, v) in RezeptKit.NaehrwertZeilen(nw))
                {
                    g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    var a = new TextBlock { Text = k, Foreground = k.StartsWith("davon") ? Muted : Text, FontSize = 11.5, Margin = new Thickness(k.StartsWith("davon") ? 10 : 0, 1, 6, 1) };
                    var b = new TextBlock { Name = "rz-nw-" + i, Text = v, Foreground = Text, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 1) };
                    Grid.SetRow(a, i); Grid.SetRow(b, i); Grid.SetColumn(b, 1); g.Children.Add(a); g.Children.Add(b); i++;
                }
                sp.Children.Add(g);
                sp.Children.Add(P($"Ganzes Rezept ({r.Portionen} {r.PortionWort}): ≈ {Math.Round(nw.Kcal * r.Portionen).ToString("0", RezeptKit.De)} kcal, {Math.Round(nw.Gramm * r.Portionen).ToString("0", RezeptKit.De)} g Zutaten." + (view != r.Portionen ? $" Für {view}: ≈ {Math.Round(nw.Kcal * view).ToString("0", RezeptKit.De)} kcal." : ""), Muted, 11));
            }
            if (nw.Fehlt.Count > 0) sp.Children.Add(new TextBlock { Name = "rz-nw-fehlt", Text = "Nicht mitgerechnet: " + string.Join(", ", nw.Fehlt) + " – in der Tabelle links zuordnen oder Menge mit Einheit angeben.", Foreground = K4Warn, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            if (nw.OhneMenge.Count > 0) sp.Children.Add(P("Ohne Menge (zählt nicht): " + string.Join(", ", nw.OhneMenge) + ".", Muted, 10.5));
        }
        sp.Children.Add(new TextBlock { Name = "rz-nw-hinweis", Text = NaehrwertTabelle.Hinweis, Foreground = Muted, FontSize = 10.5, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(H("Quelle der Nährwerte", 12));
        sp.Children.Add(P($"{NaehrwertTabelle.Quelle} – {NaehrwertTabelle.Lizenz}. {NaehrwertTabelle.Alle.Length} Lebensmittel, deutsche Namen von Trendo.", Muted, 10.5));
        sp.Children.Add(H("Einheiten", 12));
        sp.Children.Add(P("1 EL = 15 ml · 1 TL = 5 ml · 1 Tasse = 250 ml · 1 Becher = 200 ml · 1 Prise ≈ 0,5 g · 1 Msp. ≈ 0,3 g. Löffel werden über die Dichte in Gramm gerechnet.", Muted, 10.5));
        return sp;
    }
}
