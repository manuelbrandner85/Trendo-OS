using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0780 (Etappe 0.9.113 „Wissen &amp; Lernen“, Freigabe 26.11.2026): Studio „Zeitleisten“ (STUDIO-TIMELINE-01).
/// Editor für Ereignisse und Zeiträume in mehreren Spuren – Daten auch vor Christus und ungefähr („ca. 1200“, „3. Jh. v. Chr.“), Zoom und Verschieben mit Maus und Tasten,
/// Einträge mit der Maus in der Zeit verschieben, Bilder an Ereignissen. Ausgabe als SVG (Inkscape), PNG und PDF; Austausch als CSV (auch TimelineJS-Tabelle) und JSON.
/// Die KI fügt Ereignisse aus einem Text ein (Vorschau → „Ja, behalten“ → ein Rückgängig-Schritt).</summary>
public partial class MainWindow
{
    public const string TlKey = "zeitleisten-programm";
    public const string TlStudio = "STUDIO-TIMELINE-01";

    readonly Dictionary<string, (double From, double To)> _tlView = [];
    readonly Dictionary<string, string?> _tlSel = [];
    string _tlPaste = "";

    static JsonObject TlDoc(string title = "") => new() { ["title"] = title, ["tracks"] = new JsonArray(TimelineKit.TrackJson(new("t-allg", "Allgemein", TimelineKit.Palette[0]))), ["items"] = new JsonArray() };

    static JsonObject TlSample()
    {
        var t = new Timeline { Title = "Antike bis Mittelalter" };
        t.Tracks.AddRange([new("t-pol", "Politik", TimelineKit.Palette[0]), new("t-kul", "Kultur & Wissen", TimelineKit.Palette[2])]);
        void E(string s, string? e, string title, string track, string text = "") => t.Items.Add(new(TimelineKit.NewId("e"), title, HistDate.Parse(s)!.Value, e is null ? null : HistDate.Parse(e), track, text));
        E("753 v. Chr.", null, "Gründung Roms (Sage)", "t-pol", "Überliefertes Gründungsdatum");
        E("44 v. Chr.", null, "Tod Caesars", "t-pol");
        E("27 v. Chr.", "476", "Römisches Kaiserreich", "t-pol");
        E("ca. 1200", null, "Nibelungenlied", "t-kul", "Entstehung um 1200");
        E("1450", null, "Buchdruck mit beweglichen Lettern", "t-kul");
        E("5. Jh. v. Chr.", null, "Klassische Zeit in Athen", "t-kul");
        E("800", null, "Kaiserkrönung Karls des Großen", "t-pol");
        E("500", "1500", "Mittelalter", "t-kul");
        return TimelineKit.ToJson(t);
    }

    public static Timeline TlData(ProgramDocument d) => TimelineKit.FromJson(d.Data);
    static void TlSave(ProgramDocument d, Timeline t, string label) => d.Edit(label, x =>
    {
        x["title"] = t.Title;
        x["tracks"] = new JsonArray([.. t.Tracks.Select(tr => (JsonNode)TimelineKit.TrackJson(tr))]);
        x["items"] = new JsonArray([.. t.Items.Select(i => (JsonNode)TimelineKit.ItemJson(i))]);
    });

    public string? TlSelected(ProgramDocument d) => _tlSel.GetValueOrDefault(d.Id) is { } id && TlData(d).Items.Any(i => i.Id == id) ? id : null;
    public void TlSelect(ProgramDocument d, string? id) => _tlSel[d.Id] = id;
    public (double From, double To) TlViewOf(ProgramDocument d) => _tlView.TryGetValue(d.Id, out var v) ? v : TimelineKit.Fit(TlData(d));
    public void TlSetView(ProgramDocument d, double from, double to) => _tlView[d.Id] = (from, Math.Max(to, from + 1 / 12.0));

    // ---------------- Bearbeiten ----------------

    public TlItem TlAdd(ProgramDocument d, string start, string end = "", string title = "", string? track = null, string text = "")
    {
        var (s, e) = TimelineKit.CheckDates(start, end);
        var t = TlData(d);
        if (t.Items.Count >= TimelineKit.MaxItems) throw new InvalidOperationException($"Höchstens {TimelineKit.MaxItems} Einträge.");
        var tr = track is { Length: > 0 } && t.Tracks.Any(x => x.Id == track) ? track : TlSelected(d) is { } sel ? t.Items.First(i => i.Id == sel).Track : t.Tracks.FirstOrDefault()?.Id ?? TimelineKit.TrackFor(t, "Allgemein");
        var item = new TlItem(TimelineKit.NewId("e"), KreativSafe.Cut(title.Trim().Length > 0 ? title.Trim() : e is null ? "Neues Ereignis" : "Neuer Zeitraum", 200), s, e, tr, KreativSafe.Cut(text, 4000));
        t.Items.Add(item);
        TlSave(d, t, e is null ? "Neues Ereignis" : "Neuer Zeitraum");
        _tlSel[d.Id] = item.Id;
        // liegt der Eintrag außerhalb der Ansicht, die Ansicht mitnehmen
        var (f, to) = TlViewOf(d);
        if (_tlView.ContainsKey(d.Id) && (item.From < f || item.To > to)) _tlView.Remove(d.Id);
        _progMsg[TlKey] = $"{(e is null ? "Ereignis" : "Zeitraum")} angelegt: {s}" + (e is { } ee ? " – " + ee : "") + ".";
        return item;
    }

    public void TlSet(ProgramDocument d, string id, string field, string value)
    {
        var t = TlData(d); var i = t.Items.FindIndex(x => x.Id == id);
        if (i < 0) throw new InvalidOperationException("Diesen Eintrag gibt es nicht mehr.");
        var it = t.Items[i]; var v = (value ?? "").Trim();
        TlItem next = field switch
        {
            "title" => it with { Title = KreativSafe.Cut(v.Length > 0 ? v : "Ohne Titel", 200) },
            "text" => it with { Text = KreativSafe.Cut(value ?? "", 4000) },
            "start" => it with { Start = TimelineKit.CheckDates(v, it.End?.ToString() ?? "").Start },
            "end" => it with { End = TimelineKit.CheckDates(it.Start.ToString(), v).End },
            "track" => t.Tracks.Any(x => x.Id == v) ? it with { Track = v } : throw new ArgumentException("Diese Spur gibt es nicht."),
            "color" => v.Length == 0 || System.Text.RegularExpressions.Regex.IsMatch(v, "^#[0-9A-Fa-f]{6}$") ? it with { Color = v } : throw new ArgumentException("Farbe als #RRGGBB, z. B. #D4A017."),
            _ => throw new ArgumentException("Unbekanntes Feld."),
        };
        if (next == it) return;
        t.Items[i] = next;
        TlSave(d, t, field switch { "title" => "Titel", "text" => "Beschreibung", "start" => "Beginn", "end" => "Ende", "track" => "Spur", _ => "Farbe" });
    }

    public void TlRemove(ProgramDocument d, string id)
    {
        var t = TlData(d); var n = t.Items.RemoveAll(i => i.Id == id);
        if (n == 0) return;
        TlSave(d, t, "Eintrag gelöscht"); _tlSel[d.Id] = null;
    }

    public TlItem TlDuplicate(ProgramDocument d, string id)
    {
        var t = TlData(d); var it = t.Items.FirstOrDefault(i => i.Id == id) ?? throw new InvalidOperationException("Diesen Eintrag gibt es nicht mehr.");
        var copy = it with { Id = TimelineKit.NewId("e"), Title = it.Title + " (Kopie)" };
        t.Items.Add(copy); TlSave(d, t, "Eintrag verdoppelt"); _tlSel[d.Id] = copy.Id; return copy;
    }

    /// <summary>Eintrag mit der Maus verschoben: um so viele Jahre (astronomisch). Gerundet nach Genauigkeit des Datums – Tagesdaten auf Tage, sonst auf ganze Jahre (Jahrzehnt/Jh. auf ihr Raster).</summary>
    public void TlShift(ProgramDocument d, string id, double years)
    {
        var t = TlData(d); var i = t.Items.FindIndex(x => x.Id == id); if (i < 0) return;
        var it = t.Items[i];
        HistDate Move(HistDate h)
        {
            switch (h.Precision)
            {
                case DatePrecision.Day or DatePrecision.Month:
                {
                    var astro = h.AstroYear; var month = h.Month; var day = Math.Max(1, h.Day);
                    var days = (int)Math.Round(years * 365.25);
                    if (h.Precision == DatePrecision.Month) { var m = (int)Math.Round(years * 12); var total = astro * 12 + (month - 1) + m; var na = (int)Math.Floor(total / 12.0); return h with { Year = na <= 0 ? na - 1 : na, Month = (int)(total - na * 12) + 1 }; }
                    // Tage: über DateTime nur im gültigen Bereich, sonst ganze Jahre
                    if (astro is >= 1 and <= 9998) { var dt = new DateTime(astro, month, Math.Min(day, DateTime.DaysInMonth(astro, month))).AddDays(days); return h with { Year = dt.Year, Month = dt.Month, Day = dt.Day }; }
                    goto default;
                }
                case DatePrecision.Decade: { var step = (int)Math.Round(years / 10) * 10; return Shift(h, step); }
                case DatePrecision.Century: { var step = (int)Math.Round(years / 100) * 100; return Shift(h, step); }
                case DatePrecision.Millennium: { var step = (int)Math.Round(years / 1000) * 1000; return Shift(h, step); }
                default: return Shift(h, (int)Math.Round(years));
            }
        }
        static HistDate Shift(HistDate h, int by)
        {
            if (by == 0) return h;
            var a = h.AstroYear + by; var hist = a <= 0 ? a - 1 : a;
            return h with { Year = hist };
        }
        var ns = Move(it.Start); var ne = it.End is { } e ? Move(e) : (HistDate?)null;
        if (ns == it.Start && ne == it.End) return;
        t.Items[i] = it with { Start = ns, End = ne };
        TlSave(d, t, "Verschoben");
        _progMsg[TlKey] = $"„{it.Title}“ verschoben: {ns}" + (ne is { } x ? " – " + x : "") + ".";
    }

    public TlTrack TlAddTrack(ProgramDocument d, string name = "")
    {
        var t = TlData(d);
        if (t.Tracks.Count >= TimelineKit.MaxTracks) throw new InvalidOperationException($"Höchstens {TimelineKit.MaxTracks} Spuren.");
        var n = name.Trim().Length > 0 ? name.Trim() : $"Spur {t.Tracks.Count + 1}";
        var tr = new TlTrack(TimelineKit.NewId("t"), KreativSafe.Cut(n, 60), TimelineKit.Palette[t.Tracks.Count % TimelineKit.Palette.Length]);
        t.Tracks.Add(tr); TlSave(d, t, "Neue Spur");
        return tr;
    }

    public void TlSetTrack(ProgramDocument d, string id, string? name = null, string? color = null)
    {
        var t = TlData(d); var i = t.Tracks.FindIndex(x => x.Id == id); if (i < 0) throw new InvalidOperationException("Diese Spur gibt es nicht mehr.");
        if (color is { Length: > 0 } && !System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$")) throw new ArgumentException("Farbe als #RRGGBB.");
        var next = t.Tracks[i] with { Name = name is null ? t.Tracks[i].Name : KreativSafe.Cut(name.Trim().Length > 0 ? name.Trim() : t.Tracks[i].Name, 60), Color = color is { Length: > 0 } ? color : t.Tracks[i].Color };
        if (next == t.Tracks[i]) return;
        t.Tracks[i] = next; TlSave(d, t, "Spur geändert");
    }

    public void TlRemoveTrack(ProgramDocument d, string id)
    {
        var t = TlData(d);
        if (t.Tracks.Count <= 1) throw new InvalidOperationException("Eine Spur muss bleiben.");
        var keep = t.Tracks.First(x => x.Id != id).Id;
        for (var i = 0; i < t.Items.Count; i++) if (t.Items[i].Track == id) t.Items[i] = t.Items[i] with { Track = keep };
        t.Tracks.RemoveAll(x => x.Id == id); TlSave(d, t, "Spur entfernt");
    }

    /// <summary>Zeilen „Datum | Titel | Spur | Beschreibung“ (oder „Beginn – Ende | Titel …“) als Einträge übernehmen. Doppelte (gleicher Titel, gleicher Beginn) werden übersprungen.</summary>
    public (int Added, List<string> Problems) TlAddLines(ProgramDocument d, string text, string label = "Aus Text eingefügt")
    {
        var (rows, bad) = TimelineKit.ParseLines(text);
        var t = TlData(d); var added = 0; string? last = null;
        foreach (var (s, e, title, track, desc) in rows)
        {
            if (t.Items.Any(i => i.Start == s && string.Equals(i.Title, title, StringComparison.OrdinalIgnoreCase))) continue;
            var tr = track.Length > 0 ? TimelineKit.TrackFor(t, track) : t.Tracks.FirstOrDefault()?.Id ?? TimelineKit.TrackFor(t, "Allgemein");
            var it = new TlItem(TimelineKit.NewId("e"), title, s, e, tr, desc); t.Items.Add(it); added++; last = it.Id;
            if (t.Items.Count >= TimelineKit.MaxItems) break;
        }
        if (added > 0) { TlSave(d, t, label); _tlView.Remove(d.Id); _tlSel[d.Id] = last; }
        _progMsg[TlKey] = added == 0 && bad.Count == 0 ? "Keine neuen Einträge (alles schon vorhanden)." : $"{added} Einträge eingefügt" + (bad.Count > 0 ? $", {bad.Count} Zeile(n) nicht lesbar: {bad[0]}." : ".");
        return (added, bad);
    }

    /// <summary>CSV oder JSON einlesen und an die Zeitleiste anhängen (Spuren werden nach Namen zugeordnet).</summary>
    public int TlImport(ProgramDocument d, string file)
    {
        if (!KreativSafe.LocalFile(file, TimelineKit.MaxImportBytes)) throw new InvalidOperationException("Wähle eine CSV- oder JSON-Datei auf diesem Rechner (höchstens 5 MB).");
        var text = File.ReadAllText(file);
        var (got, probs) = Path.GetExtension(file).Equals(".json", StringComparison.OrdinalIgnoreCase) ? TimelineKit.FromExchangeJson(text) : TimelineKit.FromCsv(text);
        var t = TlData(d); var added = 0;
        foreach (var it in got.Items)
        {
            if (t.Items.Count >= TimelineKit.MaxItems) break;
            if (t.Items.Any(i => i.Start == it.Start && string.Equals(i.Title, it.Title, StringComparison.OrdinalIgnoreCase))) continue;
            var tr = TimelineKit.TrackFor(t, got.TrackName(it.Track) is { Length: > 0 } n ? n : "Allgemein");
            if (got.Tracks.FirstOrDefault(x => x.Id == it.Track) is { } src && t.Tracks.FirstOrDefault(x => x.Id == tr) is { } mine && t.Items.All(i => i.Track != tr) && src.Color.Length > 0)
                t.Tracks[t.Tracks.IndexOf(mine)] = mine with { Color = src.Color };
            t.Items.Add(it with { Id = TimelineKit.NewId("e"), Track = tr }); added++;
        }
        if (t.Title.Trim().Length == 0 && got.Title.Length > 0) t.Title = got.Title;
        // Platzhalter-Spur „Allgemein“ ohne Einträge entfernen, wenn die Datei eigene Spuren mitbrachte
        if (t.Tracks.Count > 1 && t.Tracks.FirstOrDefault(x => x.Id == "t-allg") is { } allg && t.Items.All(i => i.Track != allg.Id)) t.Tracks.Remove(allg);
        TlSave(d, t, "Eingelesen: " + Path.GetFileName(file));
        _tlView.Remove(d.Id);
        _progMsg[TlKey] = $"{added} Einträge aus „{Path.GetFileName(file)}“ eingelesen" + (probs.Count > 0 ? $", {probs.Count} Hinweis(e): {probs[0]}." : ".");
        return added;
    }

    /// <summary>Bild an einen Eintrag hängen (verkleinert, im Dokument eingebettet).</summary>
    public void TlSetImage(ProgramDocument d, string id, string? file)
    {
        var t = TlData(d); var i = t.Items.FindIndex(x => x.Id == id); if (i < 0) throw new InvalidOperationException("Wähle zuerst einen Eintrag.");
        byte[]? img = null;
        if (file is not null)
        {
            if (!KreativSafe.LocalFile(file, 40_000_000)) throw new InvalidOperationException("Wähle ein Bild auf diesem Rechner.");
            img = TimelineCanvas.Thumb(file);
        }
        t.Items[i] = t.Items[i] with { Image = img };
        TlSave(d, t, img is null ? "Bild entfernt" : "Bild angehängt");
        _progMsg[TlKey] = img is null ? "Bild entfernt." : $"Bild angehängt ({img.Length / 1024} KB, verkleinert).";
    }

    async Task TlPickImage(ProgramDocument d)
    {
        if (TlSelected(d) is not { } id) { _progMsg[TlKey] = "Wähle zuerst einen Eintrag in der Zeitleiste."; Show(TlKey); return; }
        if (await PickMediaFile("tl-image", "Bild", ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"]) is { } f) ProgramRun(TlKey, () => TlSetImage(d, id, f));
    }

    async Task TlPickImport(ProgramDocument d)
    {
        if (await PickMediaFile("tl-import", "Zeitleiste als CSV oder JSON", ["*.csv", "*.json", "*.txt"]) is { } f) ProgramRun(TlKey, () => TlImport(d, f));
    }

    // ---------------- Ausgabe ----------------

    static TimelineKit.Scene TlExportScene(ProgramDocument d, bool paper)
    {
        var t = TlData(d);
        if (t.Items.Count == 0) throw new InvalidOperationException("Die Zeitleiste ist noch leer.");
        var (f, to) = TimelineKit.Fit(t);
        var span = to - f; var width = Math.Clamp(900 + t.Items.Count * 18, 1200, 3200);
        _ = span;
        return TimelineKit.Layout(t, f, to, width, null, true, paper);
    }

    static string TlWriteText(string file, string text, bool bom = false) { File.WriteAllText(file, text, new UTF8Encoding(bom)); return file; }

    // ---------------- KI-Fachaktion ----------------

    ProgramKiAction TlKiEvents => new(new AssistAction("zeitleiste.ereignisse", "Ereignisse aus Text einfügen", ["ereignisse", "ereignis", "einfügen", "einfuegen", "daten", "zeitleiste", "aus", "text", "jahreszahlen", "chronik"],
        [new AssistParam("ereignisse", "Ereignisse, eine Zeile je Eintrag: „Datum | Titel | Spur | Beschreibung“ oder „Beginn – Ende | Titel …“", Max: 50_000)]),
        (d, a) =>
        {
            var lines = a.GetValueOrDefault("ereignisse") ?? "";
            var (rows, bad) = TimelineKit.ParseLines(lines);
            if (rows.Count == 0) throw new InvalidOperationException("Die KI hat keine lesbaren Ereignisse geliefert" + (bad.Count > 0 ? $" ({bad[0]})" : "") + ". Nichts übernommen.");
            if (bad.Count > 0) throw new InvalidOperationException($"Die KI hat {bad.Count} Zeile(n) ohne lesbares Datum geliefert ({bad[0]}). Nichts übernommen – bitte den Auftrag genauer fassen.");
            TlAddLines(d, lines, "Ereignisse aus Text");
        },
        (_, _) => [])
    { OnApplyNote = "Erst bei „Ja, behalten“ kommt es ins Dokument – ein Rückgängig-Schritt nimmt es wieder heraus." };

    private void InitZeitleisten() => RegisterProgram(FachTabs, new ProgramSpec(TlKey, TlStudio, "Zeitleisten")
    {
        Sentence = "Ereignisse und Epochen auf einer Zeitachse – auch vor Christus und mit ungefähren Daten, in mehreren Spuren, mit Bildern; als SVG, PNG oder PDF.",
        Folder = "Zeitleisten",
        FillHeight = true,
        Templates =
        [
            new("leer", "Leere Zeitleiste", "Erstes Ereignis eintragen", () => TlDoc()),
            new("beispiel", "Beispiel: Antike bis Mittelalter", "Zwei Spuren, Daten vor Christus und ungefähre Angaben", TlSample),
            new("lebenslauf", "Lebenslauf / Projekt", "Spuren für Schule, Arbeit und Privat", () =>
            {
                var o = TlDoc("Mein Weg");
                o["tracks"] = new JsonArray(TimelineKit.TrackJson(new("t-a", "Ausbildung", TimelineKit.Palette[0])), TimelineKit.TrackJson(new("t-b", "Arbeit", TimelineKit.Palette[1])), TimelineKit.TrackJson(new("t-c", "Privat", TimelineKit.Palette[3])));
                return o;
            }),
        ],
        Ribbon =
        [
            ("Einfügen", [
                new ProgramCmd("tl-add-event", "Ereignis", d => TlAdd(d, DateTime.Today.Year.ToString(CultureInfo.InvariantCulture)), Primary: true, Tip: "Neues Ereignis – Datum und Titel oben eintragen") { NoKi = true },
                new ProgramCmd("tl-add-period", "Zeitraum", d => TlAdd(d, (DateTime.Today.Year - 10).ToString(CultureInfo.InvariantCulture), DateTime.Today.Year.ToString(CultureInfo.InvariantCulture))) { NoKi = true },
                new ProgramCmd("tl-add-track", "Spur", d => TlAddTrack(d), Level: 1) { NoKi = true },
                new ProgramCmd("tl-add-image", "Bild anhängen …", d => _ = TlPickImage(d), Tip: "Bild an den gewählten Eintrag (wird verkleinert und eingebettet)") { NoKi = true },
                new ProgramCmd("tl-import", "CSV/JSON einlesen …", d => _ = TlPickImport(d), Tip: "Auch die Tabelle von TimelineJS (Year, Month, Day, Headline …)", Level: 1) { NoKi = true },
            ]),
            ("Eintrag", [
                new ProgramCmd("tl-dup", "Verdoppeln", d => { if (TlSelected(d) is { } id) TlDuplicate(d, id); else throw new InvalidOperationException("Wähle zuerst einen Eintrag."); }, Level: 1) { NoKi = true },
                new ProgramCmd("tl-del", "Löschen", d => { if (TlSelected(d) is { } id) TlRemove(d, id); else throw new InvalidOperationException("Wähle zuerst einen Eintrag."); }) { NoKi = true },
            ]),
            ("Ansicht", [
                new ProgramCmd("tl-zoom-in", "Vergrößern", d => { var (f, t) = TlViewOf(d); var c = (f + t) / 2; var s = (t - f) * 0.8; TlSetView(d, c - s / 2, c + s / 2); }) { NoKi = true },
                new ProgramCmd("tl-zoom-out", "Verkleinern", d => { var (f, t) = TlViewOf(d); var c = (f + t) / 2; var s = (t - f) * 1.25; TlSetView(d, c - s / 2, c + s / 2); }) { NoKi = true },
                new ProgramCmd("tl-fit", "Alles zeigen", d => _tlView.Remove(d.Id)) { NoKi = true },
                new ProgramCmd("tl-left", "◀", d => { var (f, t) = TlViewOf(d); var s = (t - f) * 0.2; TlSetView(d, f - s, t - s); }, Tip: "Nach links (früher)", Level: 1) { NoKi = true },
                new ProgramCmd("tl-right", "▶", d => { var (f, t) = TlViewOf(d); var s = (t - f) * 0.2; TlSetView(d, f + s, t + s); }, Tip: "Nach rechts (später)", Level: 1) { NoKi = true },
            ]),
        ],
        Exports =
        [
            new("svg", "SVG für Inkscape (.svg)", ".svg", (d, f) => TlWriteText(f, TimelineKit.ToSvg(TlExportScene(d, true)))),
            new("png", "Bild (.png)", ".png", (d, f) => TimelineCanvas.SavePng(TlExportScene(d, true), f)),
            new("pdf", "PDF (.pdf)", ".pdf", (d, f) => { TimelineKit.ToPdf(TlExportScene(d, true), f); return f; }),
            new("csv", "Tabelle (.csv)", ".csv", (d, f) => TlWriteText(f, TimelineKit.ToCsv(TlData(d)), bom: true)),
            new("json", "Austausch (.json)", ".json", (d, f) => TlWriteText(f, TimelineKit.ToExchangeJson(TlData(d)))),
            new("svg-dunkel", "SVG dunkel wie am Bildschirm (.svg)", ".svg", (d, f) => TlWriteText(f, TimelineKit.ToSvg(TlExportScene(d, false)))),
        ],
        SimpleExports = 3,
        KiActions = [TlKiEvents],
        KiHint = "Ereignisse und Zeiträume fügst du nur mit „zeitleiste.ereignisse“ ein (alle in einem Schritt): eine Zeile je Eintrag, „Datum | Titel | Spur | Beschreibung“ (Spur und Beschreibung dürfen fehlen), "
               + "Zeiträume als „Beginn – Ende | Titel“. Daten wie „1789“, „14.07.1789“, „44 v. Chr.“, „ca. 1200“, „3. Jh. v. Chr.“, „1990er“. Nimm nur Daten, die im Text stehen – erfinde keine.",
        FirstStep = new("Erstes Ereignis", "Trag ein Ereignis ein – Datum auch vor Christus („44 v. Chr.“) oder ungefähr („ca. 1200“). Oder zieh eine CSV-/JSON-Datei hierher.",
            d => TlAdd(d, DateTime.Today.Year.ToString(CultureInfo.InvariantCulture)), d => TlData(d).Items.Count == 0),
        Workspace = TlWorkspace,
        Properties = TlProperties,
        Status = d => { var t = TlData(d); var (f, to) = TlViewOf(d); return $"{t.Items.Count(i => !i.IsPeriod)} Ereignisse · {t.Items.Count(i => i.IsPeriod)} Zeiträume · {t.Tracks.Count} Spuren · Ansicht {HistDate.AxisLabel(f, 1)} bis {HistDate.AxisLabel(to, 1)}"; },
        Help = "Oben „Ereignis“ oder „Zeitraum“, dann Datum und Titel in der Zeile über der Zeitleiste eintragen. Daten dürfen vor Christus liegen („44 v. Chr.“), ungefähr sein („ca. 1200“, „um 800“) "
             + "oder grob („3. Jh. v. Chr.“, „1990er“). Mausrad vergrößert und verkleinert, Ziehen auf freier Fläche verschiebt die Ansicht, Ziehen eines Eintrags verschiebt ihn in der Zeit, Doppelklick legt ein Ereignis an. "
             + "Rechtsklick auf einen Eintrag für mehr. Bilder, CSV- und JSON-Dateien kannst du auf die Zeitleiste ziehen. Ausgabe als SVG (Inkscape), PNG und PDF.",
    }, "zeitleiste", "zeitstrahl", "chronologie", "geschichte", "epochen", "timeline", "zeittafel", "ereignisse", "vor christus", "timelinejs", "preceden", "aeon timeline");

    Control TlWorkspace(ProgramDocument d)
    {
        var t = TlData(d); var sel = TlSelected(d); var (from, to) = TlViewOf(d);
        var canvas = new TimelineCanvas { Name = "tl-canvas", Data = t, From = from, To = to, Selected = sel };
        canvas.ViewChanged = (f, tt) => _tlView[d.Id] = (f, tt);
        canvas.Clicked = id => { _tlSel[d.Id] = id; Show(TlKey); };
        canvas.Moved = (id, years) => ProgramRun(TlKey, () => TlShift(d, id, years));
        canvas.AddAt = year => ProgramRun(TlKey, () => TlAdd(d, HistDate.AxisLabel(Math.Floor(year), 1)));
        canvas.DeleteSelected = () => { if (TlSelected(d) is { } id) ProgramRun(TlKey, () => TlRemove(d, id)); };
        canvas.ContextMenu = BueroMenu(TlKey, "tl-menu", null,
            ("tl-m-img", "Bild anhängen …", () => _ = TlPickImage(d), true),
            ("tl-m-noimg", "Bild entfernen", () => { if (TlSelected(d) is { } id) TlSetImage(d, id, null); }, true),
            ("tl-m-dup", "Verdoppeln", () => { if (TlSelected(d) is { } id) TlDuplicate(d, id); }, true),
            ("-", "", () => { }, true),
            ("tl-m-fit", "Alles zeigen", () => _tlView.Remove(d.Id), true),
            ("tl-m-del", "Löschen", () => { if (TlSelected(d) is { } id) TlRemove(d, id); }, true));
        BueroFileDrop(canvas, TlKey, BueroExt(".png", ".jpg", ".jpeg", ".webp", ".bmp", ".csv", ".json"), "Bilder (an den gewählten Eintrag) oder CSV-/JSON-Dateien", (doc, files) =>
        {
            foreach (var f in files.Where(f => Path.GetExtension(f).ToLowerInvariant() is ".csv" or ".json")) TlImport(doc, f);
            if (files.FirstOrDefault(f => Path.GetExtension(f).ToLowerInvariant() is not (".csv" or ".json")) is { } img)
            {
                if (TlSelected(doc) is not { } id) throw new InvalidOperationException("Wähle zuerst einen Eintrag – dann das Bild darauf ziehen.");
                TlSetImage(doc, id, img);
            }
        });
        var editor = TlEditor(d, t, sel);
        var scroll = new ScrollViewer { Content = canvas, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var frame = new Border { Name = "tl-frame", CornerRadius = new CornerRadius(8), ClipToBounds = true, BorderBrush = Brush.Parse("#33D4A017"), BorderThickness = new Thickness(1), Child = scroll };
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Thickness(4, 0, 10, 4) };
        grid.Children.Add(editor);
        Grid.SetRow(frame, 1); grid.Children.Add(frame);
        return grid;
    }

    /// <summary>Zeile über der Zeitleiste: gewählten Eintrag bearbeiten (auch in „Einfach“, wo die rechte Spalte fehlt).</summary>
    Control TlEditor(ProgramDocument d, Timeline t, string? sel)
    {
        var it = t.Items.FirstOrDefault(i => i.Id == sel);
        var row = new WrapPanel { Name = "tl-editor", Margin = new Thickness(0, 0, 0, 8) };
        if (it is null) { row.Children.Add(P("Einen Eintrag anklicken, um ihn zu bearbeiten – oder doppelt auf eine freie Stelle klicken.", Muted, 12)); return row; }
        TextBox Box(string field, string name, string value, string hint, double width)
        {
            var b = new TextBox { Name = name, Text = value, PlaceholderText = hint, Width = width, Margin = new Thickness(0, 0, 8, 4) };
            var shown = value;
            void Commit() { var now = b.Text ?? ""; if (now.Trim() == shown.Trim()) return; shown = now; ProgramRun(TlKey, () => TlSet(d, it.Id, field, now)); }
            b.LostFocus += (_, _) => Commit(); OnEnter(b, Commit);
            ToolTip.SetTip(b, hint);
            return b;
        }
        row.Children.Add(Box("start", "tl-f-start", it.Start.ToString(), "Beginn: 1789, 14.07.1789, 44 v. Chr., ca. 1200, 3. Jh. v. Chr.", 170));
        row.Children.Add(Box("end", "tl-f-end", it.End?.ToString() ?? "", "Ende (leer = Ereignis)", 150));
        row.Children.Add(Box("title", "tl-f-title", it.Title, "Titel", 280));
        var tracks = new ComboBox { Name = "tl-f-track", Width = 170, Margin = new Thickness(0, 0, 8, 4), ItemsSource = t.Tracks.Select(x => x.Name).ToList(), SelectedIndex = Math.Max(0, t.Tracks.FindIndex(x => x.Id == it.Track)) };
        tracks.SelectionChanged += (_, _) => { var i = tracks.SelectedIndex; if (i >= 0 && i < t.Tracks.Count && t.Tracks[i].Id != it.Track) ProgramRun(TlKey, () => TlSet(d, it.Id, "track", t.Tracks[i].Id)); };
        row.Children.Add(tracks);
        var info = new TextBlock { Name = "tl-f-info", Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Text = (it.IsPeriod ? "Zeitraum" : "Ereignis") + (it.Start.Approx || it.Start.Precision >= DatePrecision.Decade ? " · ungefähr" : "") + (it.Image is { Length: > 0 } ? " · mit Bild" : "") };
        row.Children.Add(info);
        return row;
    }

    Control TlProperties(ProgramDocument d)
    {
        var t = TlData(d); var sel = TlSelected(d); var it = t.Items.FirstOrDefault(i => i.Id == sel);
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H("Zeitleiste", 13));
        sp.Children.Add(KrBox(d, "title", "tl-p-title", "Titel der Zeitleiste"));
        if (it is not null)
        {
            sp.Children.Add(H("Beschreibung", 13));
            var desc = new TextBox { Name = "tl-p-text", Text = it.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, PlaceholderText = "Was ist passiert?" };
            var shown = it.Text;
            desc.LostFocus += (_, _) => { if ((desc.Text ?? "") != shown) { shown = desc.Text ?? ""; ProgramRun(TlKey, () => TlSet(d, it.Id, "text", shown)); } };
            sp.Children.Add(desc);
            var color = new TextBox { Name = "tl-p-color", Text = it.Color, PlaceholderText = "Eigene Farbe #RRGGBB (leer = Farbe der Spur)" };
            color.LostFocus += (_, _) => { if ((color.Text ?? "") != it.Color) ProgramRun(TlKey, () => TlSet(d, it.Id, "color", color.Text ?? "")); };
            sp.Children.Add(color);
        }
        sp.Children.Add(H("Spuren", 13));
        var ti = 0;
        foreach (var tr in t.Tracks)
        {
            var id = tr.Id;
            var name = new TextBox { Name = "tl-p-track-" + ti, Text = tr.Name };
            name.LostFocus += (_, _) => { if ((name.Text ?? "") != tr.Name) ProgramRun(TlKey, () => TlSetTrack(d, id, name: name.Text ?? "")); };
            var col = new TextBox { Name = "tl-p-trackcolor-" + ti, Text = tr.Color, Width = 86 };
            col.LostFocus += (_, _) => { if ((col.Text ?? "") != tr.Color) ProgramRun(TlKey, () => TlSetTrack(d, id, color: col.Text ?? "")); };
            var del = new Button { Name = "tl-p-trackdel-" + ti, Content = "✕", Padding = new Thickness(6, 1), IsEnabled = t.Tracks.Count > 1 };
            ToolTip.SetTip(del, "Spur entfernen – ihre Einträge wandern in die erste Spur");
            del.Click += (_, _) => ProgramRun(TlKey, () => TlRemoveTrack(d, id));
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("10,*,6,Auto,6,Auto"), Children = { new Border { Background = Brush.Parse(tr.Color), CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 4) }, Pos(name, 1), Pos(col, 3), Pos(del, 5) } });
            ti++;
        }
        sp.Children.Add(H("Aus Text einfügen", 13));
        sp.Children.Add(P("Eine Zeile je Eintrag: „1789 | Französische Revolution | Politik“ oder „1618 – 1648 | Dreißigjähriger Krieg“. Für Fließtext: oben in der Auftragszeile die KI bitten.", Muted, 11));
        var paste = new TextBox { Name = "tl-p-lines", Text = _tlPaste, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90 };
        paste.TextChanged += (_, _) => _tlPaste = paste.Text ?? "";
        var go = new Button { Name = "tl-p-lines-add", Content = "Einfügen", Padding = new Thickness(10, 4) };
        go.Click += (_, _) => ProgramRun(TlKey, () => { var (n, _) = TlAddLines(d, paste.Text ?? ""); if (n > 0) _tlPaste = ""; });
        sp.Children.Add(paste); sp.Children.Add(go);
        return sp;
    }
}
