using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Business;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Etappe „Business &amp; Geld“ Teil B (T-0676): Social Media planen, Newsletter gestalten, Flyer &amp; Visitenkarten, Verkaufsseiten.
/// Anforderungen und Prüfungen: docs/nachbau/&lt;studio&gt;/anforderungen.md. Trendo veröffentlicht, versendet, bestellt und kassiert nichts.</summary>
public partial class MainWindow
{
    public const string SocialKey = "social-programm", NewsKey = "newsletter-programm", FlyerKey = "flyer-programm", LandingKey = "verkaufsseiten-programm";

    /// <summary>Tests: ersetzt den KI-Kern bei Ideen, Varianten und Betreffvorschlägen (Anfrage → Antwort).</summary>
    public static Func<string, Task<string>>? BizAskOverride { get; set; }
    /// <summary>Was zuletzt in die Zwischenablage kopiert wurde (für Tests und Claude).</summary>
    public string BizCopied { get; private set; } = "";
    bool _bizAiBusy;

    private void InitBusinessStudios() { InitSocial(); InitNewsletter(); InitFlyer(); InitLanding(); }

    // ---------- gemeinsame Hilfen ----------

    static List<JsonObject> BzList(ProgramDocument d, string key) => [.. d.List(key).OfType<JsonObject>()];
    static void BzSetList(ProgramDocument d, string key, IEnumerable<JsonObject> items, string label) =>
        d.Edit(label, x => x[key] = new JsonArray([.. items.Select(o => (JsonNode)o.DeepClone())]));
    static JsonObject BzItem(ProgramDocument d, string key, string id) =>
        BzList(d, key).FirstOrDefault(o => Js(o["id"]) == id) ?? throw new InvalidOperationException("Diesen Eintrag gibt es nicht mehr.");
    static void BzField(ProgramDocument d, string key, string id, string field, JsonNode? value, string label) =>
        d.Edit(label, x =>
        {
            var o = (x[key] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(o => Js(o["id"]) == id) ?? throw new InvalidOperationException("Diesen Eintrag gibt es nicht mehr.");
            o[field] = value?.DeepClone();
        });
    static void BzMove(ProgramDocument d, string key, string id, int dir)
    {
        var l = BzList(d, key); var i = l.FindIndex(o => Js(o["id"]) == id); var j = i + dir;
        if (i < 0 || j < 0 || j >= l.Count) return;
        (l[i], l[j]) = (l[j], l[i]); BzSetList(d, key, l, dir < 0 ? "Nach oben" : "Nach unten");
    }
    static void BzRemove(ProgramDocument d, string key, string id, string label) => BzSetList(d, key, BzList(d, key).Where(o => Js(o["id"]) != id), label);

    /// <summary>Textfeld für ein Feld eines Listeneintrags – übernimmt nach einer Sekunde Pause und beim Verlassen (wie <see cref="KrBox"/>).</summary>
    TextBox BzBox(ProgramDocument d, string key, string id, string field, string name, string hint, bool multi = false)
    {
        var start = Js(BzList(d, key).FirstOrDefault(o => Js(o["id"]) == id)?[field]);
        var t = new TextBox { Name = name, Text = start, PlaceholderText = hint, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multi ? 70 : 0, FontSize = 12 };
        var shown = start; var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        void Commit()
        {
            timer.Stop(); var now = t.Text ?? "";
            if (now == shown) return;
            if (BzList(d, key).All(o => Js(o["id"]) != id)) return;
            BzField(d, key, id, field, now, hint); shown = now;
        }
        timer.Tick += (_, _) => Commit();
        t.TextChanged += (_, _) => { timer.Stop(); if ((t.Text ?? "") != shown) timer.Start(); };
        t.LostFocus += (_, _) => Commit();
        t.DetachedFromVisualTree += (_, _) => Commit();
        return t;
    }

    Button BzBtn(string key, string name, string label, Action a, bool primary = false, string? tip = null)
    {
        var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 4) };
        if (primary) b.Classes.Add("accent");
        if (tip is not null) ToolTip.SetTip(b, tip);
        b.Click += (_, _) => ProgramRun(key, a); return b;
    }

    static Control BzFindings<T>(string name, IEnumerable<T> items, Func<T, (string Text, bool Error)> map)
    {
        var sp = new StackPanel { Name = name, Spacing = 3 };
        var list = items.Select(map).ToList();
        if (list.Count == 0) sp.Children.Add(new TextBlock { Text = "✓ Alles in Ordnung.", Foreground = K4Good, FontSize = 12 });
        foreach (var (t, e) in list.OrderByDescending(x => x.Error))
            sp.Children.Add(new TextBlock { Text = (e ? "✕ " : "⚠ ") + t, Foreground = e ? K4Bad : K4Warn, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        return sp;
    }

    async Task<string> BizAsk(string prompt, string label)
    {
        if (BizAskOverride is { } o) return await o(prompt);
        using var kiLabel = KiActivityHub.Label(label);
        var a = await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false));
        if (!a.Ok || string.IsNullOrWhiteSpace(a.Text)) throw new InvalidOperationException("Die KI hat nichts geliefert. Ist ein Sprachmodell in der KI-Bibliothek geladen?");
        return a.Text.Trim();
    }

    async Task BizCopy(string key, string text, string what)
    {
        BizCopied = text;
        try { if (Clipboard is { } cb) await cb.SetTextAsync(text); _progMsg[key] = what + " kopiert – jetzt im Netzwerk oder Programm einfügen."; }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException) { _progMsg[key] = "Kopieren ging nicht: " + FriendlyError.Say(ex); }
        Show(key);
    }

    static (int W, int H)? BizImageSize(string file)
    {
        if (!File.Exists(file)) return null;
        try { using var c = SkiaSharp.SKCodec.Create(file); return c is null ? null : (c.Info.Width, c.Info.Height); } catch (Exception) { return null; }
    }
    static byte[]? BizRead(string file) { try { return File.Exists(file) && new FileInfo(file).Length <= KreativSafe.MaxImageBytes ? File.ReadAllBytes(file) : null; } catch (IOException) { return null; } }
    static long? BizBytes(string file) => File.Exists(file) ? new FileInfo(file).Length : null;
    static Control BizThumb(string file, double w, double h, Stretch stretch = Stretch.UniformToFill)
    {
        var b = new Border { Width = w, Height = h, Background = Brush.Parse("#33FFFFFF"), ClipToBounds = true, CornerRadius = new CornerRadius(4) };
        if (File.Exists(file)) try { b.Child = new Image { Source = new Bitmap(file), Stretch = stretch }; } catch (Exception) { b.Child = new TextBlock { Text = "Bild?", Foreground = Muted, FontSize = 10 }; }
        else b.Child = new TextBlock { Text = "kein Bild", Foreground = Muted, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        return b;
    }
    string BizDir(params string[] parts) { var p = Path.Combine([_core.DataRoot, "business", .. parts]); Directory.CreateDirectory(p); return p; }

    // ======================================================================
    // Social Media planen (STUDIO-SOCIAL-01)
    // ======================================================================

    /// <summary>Tests: „jetzt“ für die Warteschlange.</summary>
    public static Func<DateTime>? SocialNowOverride { get; set; }
    static DateTime SocialNow => SocialNowOverride?.Invoke() ?? DateTime.Now;
    readonly Dictionary<string, Pixmap> _socImg = [];

    static JsonObject SocialDoc(IEnumerable<JsonObject>? posts = null) => new()
    {
        ["view"] = "woche", ["day"] = SocialNow.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ["slots"] = "Mo 09:00, Mo 18:00, Di 09:00, Di 18:00, Mi 09:00, Mi 18:00, Do 09:00, Do 18:00, Fr 09:00, Fr 18:00, Sa 11:00",
        ["posts"] = new JsonArray([.. (posts ?? []).Select(p => (JsonNode)p)]), ["sel"] = "", ["ideas"] = new JsonArray(),
    };

    static JsonObject SocialPostJson(string id, DateTime when, string text, string tags, IEnumerable<string> nets, string image = "", string status = SocialPlan.Planned) => new()
    {
        ["id"] = id, ["when"] = when.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture), ["text"] = text, ["tags"] = tags,
        ["nets"] = new JsonArray([.. nets.Select(n => (JsonNode)n)]), ["image"] = image, ["status"] = status, ["own"] = new JsonObject(),
    };

    static SocialPost SocialOf(JsonObject o) => new(Js(o["id"]),
        DateTime.TryParse(Js(o["when"]), CultureInfo.InvariantCulture, DateTimeStyles.None, out var w) ? w : DateTime.Today.AddHours(9), Js(o["text"]), SocialPlan.Tags(Js(o["tags"])),
        [.. (o["nets"] as JsonArray ?? []).Select(n => Js(n)).Where(n => SocialPlan.Networks.Any(x => x.Id == n))], Js(o["image"]),
        SocialPlan.Statuses.Contains(Js(o["status"])) ? Js(o["status"]) : SocialPlan.Planned,
        (o["own"] as JsonObject)?.Where(kv => Js(kv.Value).Length > 0).ToDictionary(kv => kv.Key, kv => Js(kv.Value)));

    public List<SocialPost> SocialPosts(ProgramDocument d) => [.. BzList(d, "posts").Select(SocialOf).OrderBy(p => p.When)];
    static DateTime SocDay(ProgramDocument d) => DateTime.TryParseExact(d.Text("day"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var x) ? x : SocialNow.Date;
    static List<(DayOfWeek Day, TimeSpan Time)> SocSlots(ProgramDocument d) { try { return SocialPlan.ParseSlots(d.Text("slots")); } catch (FormatException) { return [.. SocialPlan.DefaultSlots]; } }

    /// <summary>Neuen Beitrag anlegen – ohne Zeit auf die nächste freie feste Zeit (Warteschlange).</summary>
    public SocialPost SocialAdd(ProgramDocument d, string text = "", DateTime? when = null, IEnumerable<string>? nets = null)
    {
        var t = when ?? SocialPlan.NextSlot(SocialPosts(d), SocSlots(d), SocialNow);
        var id = NewId("p");
        d.Edit("Neuer Beitrag", x =>
        {
            if (x["posts"] is not JsonArray a) x["posts"] = a = [];
            a.Add(SocialPostJson(id, t, text, "", nets ?? ["instagram", "facebook"], status: text.Length > 0 ? SocialPlan.Planned : SocialPlan.Draft));
            x["sel"] = id; x["day"] = t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        });
        _progMsg[SocialKey] = $"Beitrag für {t.ToString("dddd, d. MMMM, HH:mm", De)} angelegt.";
        return SocialPosts(d).Single(p => p.Id == id);
    }

    public void SocialSetNet(ProgramDocument d, string id, string net, bool on)
    {
        var nets = SocialOf(BzItem(d, "posts", id)).Networks.ToList();
        if (on && !nets.Contains(net)) nets.Add(net); if (!on) nets.Remove(net);
        BzField(d, "posts", id, "nets", new JsonArray([.. SocialPlan.Networks.Where(n => nets.Contains(n.Id)).Select(n => (JsonNode)n.Id)]), on ? "Netzwerk an" : "Netzwerk aus");
    }

    public void SocialSetWhen(ProgramDocument d, string id, string text)
    {
        if (!DateTime.TryParse(text, De, DateTimeStyles.None, out var t) && !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
            throw new FormatException("Zeit wie „16.11.2026 09:00“ eingeben.");
        BzField(d, "posts", id, "when", t.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture), "Zeit");
    }

    public void SocialSetOwn(ProgramDocument d, string id, string net, string text) =>
        d.Edit("Eigener Text", x =>
        {
            var o = (x["posts"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(o => Js(o["id"]) == id) ?? throw new InvalidOperationException("Diesen Beitrag gibt es nicht mehr.");
            if (o["own"] is not JsonObject own) o["own"] = own = new JsonObject();
            own[net] = text;
        });

    SocialPost? SocSel(ProgramDocument d) { var id = d.Text("sel"); return SocialPosts(d).FirstOrDefault(p => p.Id == id); }

    /// <summary>Beitragsideen über den vorhandenen KI-Kern – als Entwürfe an die nächsten freien Zeiten.</summary>
    public async Task<int> SocialIdeas(ProgramDocument d, string topic)
    {
        if (_bizAiBusy) return 0;
        if (string.IsNullOrWhiteSpace(topic)) { _progMsg[SocialKey] = "Schreib rechts ein Thema, z. B. „Herbstangebote in der Bäckerei“."; Show(SocialKey); return 0; }
        _bizAiBusy = true; _progMsg[SocialKey] = "Die KI sammelt Ideen …"; Show(SocialKey);
        try
        {
            var nets = SocSel(d)?.Networks ?? ["instagram", "facebook"];
            var ideas = SocialPlan.ParseIdeas(await BizAsk(SocialPlan.IdeaPrompt(topic.Trim(), nets), "Beitragsideen"));
            if (ideas.Count == 0) { _progMsg[SocialKey] = "Die KI hat keine verwertbaren Ideen geliefert."; return 0; }
            d.Edit("Ideen", x => x["ideas"] = new JsonArray([.. ideas.Select(i => (JsonNode)i)]));
            _progMsg[SocialKey] = $"{ideas.Count} Ideen rechts unter „Ideen“ – mit „Als Beitrag“ in den Kalender holen. Bitte prüfen, die KI kann sich irren.";
            return ideas.Count;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException) { _progMsg[SocialKey] = FriendlyError.Say(ex); return 0; }
        finally { _bizAiBusy = false; Show(SocialKey); }
    }

    /// <summary>Text für ein Netzwerk von der KI umschreiben lassen – landet als eigener Text des Netzwerks.</summary>
    public async Task<string?> SocialVariant(ProgramDocument d, string id, string net)
    {
        if (_bizAiBusy) return null;
        var p = SocialPosts(d).FirstOrDefault(x => x.Id == id); if (p is null) return null;
        if (p.Text.Trim().Length == 0) { _progMsg[SocialKey] = "Erst einen Text schreiben, dann eine Variante holen."; Show(SocialKey); return null; }
        _bizAiBusy = true; _progMsg[SocialKey] = $"Die KI schreibt eine Fassung für {SocialPlan.Network(net).Name} …"; Show(SocialKey);
        try
        {
            var t = (await BizAsk(SocialPlan.VariantPrompt(p.Text, net), "Beitragsvariante")).Trim().Trim('"', '„', '“');
            SocialSetOwn(d, id, net, t);
            var len = SocialPlan.Length(t, net); var max = SocialPlan.Network(net).MaxChars;
            _progMsg[SocialKey] = $"Fassung für {SocialPlan.Network(net).Name}: {len}/{max} Zeichen" + (len > max ? " – noch zu lang, bitte kürzen." : ". Bitte lesen und anpassen.");
            return t;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException) { _progMsg[SocialKey] = FriendlyError.Say(ex); return null; }
        finally { _bizAiBusy = false; Show(SocialKey); }
    }

    /// <summary>Erinnerungen in „E-Mails &amp; Termine“ eintragen (je Beitrag ein Termin mit Erinnerung) – Trendo postet nicht selbst.</summary>
    public int SocialToCalendar(ProgramDocument d)
    {
        var n = 0;
        foreach (var p in SocialPosts(d).Where(p => p.Status != SocialPlan.Posted && p.Networks.Count > 0 && p.When > SocialNow))
        {
            Cal.Add(new Trendo.Core.Sys.CalEvent("social-" + p.Id, p.When, p.When.AddMinutes(15), "Beitrag posten: " + string.Join(", ", p.Networks.Select(x => SocialPlan.Network(x).Name)), "",
                string.Join("\n\n", p.Networks.Select(x => SocialPlan.Network(x).Name + ":\n" + SocialPlan.Compose(p, x))), RemindMin: 10));
            n++;
        }
        _progMsg[SocialKey] = n == 0 ? "Keine kommenden Beiträge zum Eintragen." : $"{n} Erinnerungen im Kalender eingetragen (10 Minuten vorher).";
        return n;
    }

    Pixmap? SocLoad(string file)
    {
        if (_socImg.TryGetValue(file, out var p)) return p;
        if (!File.Exists(file)) return null;
        try { var img = ArtDecode(file); if (img.W * (long)img.H > 4_000_000) { var s = Math.Sqrt(4_000_000.0 / (img.W * (long)img.H)); img = img.Resize((int)(img.W * s), (int)(img.H * s)); } return _socImg[file] = img; }
        catch (Exception ex) when (ex is InvalidDataException or IOException) { return null; }
    }

    /// <summary>Bild eines Beitrags auf ein Format zuschneiden und als PNG speichern (für Tests und den Knopf „Zuschnitt speichern“).</summary>
    public string SocialCropSave(ProgramDocument d, string id, string net, string format)
    {
        var p = SocialPosts(d).First(x => x.Id == id);
        var img = SocLoad(p.Image) ?? throw new InvalidOperationException("Der Beitrag hat noch kein Bild.");
        var f = SocialPlan.Network(net).Formats.FirstOrDefault(x => x.Id == format) ?? SocialPlan.Network(net).Formats[0];
        var file = Path.Combine(BizDir("social", "zuschnitte"), $"{p.When:yyyy-MM-dd_HHmm}_{net}_{f.Id}.png");
        File.WriteAllBytes(file, PngCodec.Encode(SocialPlan.Crop(img, f.W, f.H, 0.5, d.Number("focusY", 0.5)), fast: false));
        _progMsg[SocialKey] = $"Zuschnitt {f.W} × {f.H} gespeichert: {file}";
        return file;
    }

    async Task SocialPickImage(ProgramDocument d, string id)
    {
        var f = await PickMediaFile("social-image", "Bild für den Beitrag", ["*.jpg", "*.jpeg", "*.png", "*.webp"]);
        if (f is not null) ProgramRun(SocialKey, () => { BzField(d, "posts", id, "image", f, "Bild"); _progMsg[SocialKey] = "Bild gesetzt – rechts siehst du den Zuschnitt je Netzwerk."; });
    }

    private void InitSocial() => RegisterProgram(FachTabs, new ProgramSpec(SocialKey, "STUDIO-SOCIAL-01", "Social Media planen")
    {
        Sentence = "Beiträge im Redaktionskalender planen – Text je Netzwerk mit Zeichenprüfung, Bildzuschnitt, Erinnerung. Gepostet wird von dir.",
        Folder = "Social Media",
        ScrollWorkspace = true,
        GroupLevels = new() { ["KI"] = 0, ["Ansicht"] = 0, ["Erinnern"] = 0 },
        Templates =
        [
            new("leer", "Leerer Plan", "Woche ab heute", () => SocialDoc()),
            new("beispiel", "Beispiel: Woche einer Bäckerei", "Drei Beiträge, drei Netzwerke", () =>
            {
                var mon = SocialPlan.WeekStart(SocialNow).AddDays(7);
                return SocialDoc([
                    SocialPostJson("p1", mon.AddHours(9), "Frisch aus dem Ofen: Ab heute gibt es wieder unsere Kürbisbrötchen. Wir freuen uns auf euch!", "#Bäckerei #Herbst #Handwerk", ["instagram", "facebook"], status: SocialPlan.Planned),
                    SocialPostJson("p2", mon.AddDays(2).AddHours(18), "Hinter den Kulissen: So entsteht unser Sauerteig – 24 Stunden Geduld für den vollen Geschmack. Mehr dazu auf https://example.org/sauerteig", "#Sauerteig #Handwerk", ["linkedin", "x", "mastodon"]),
                    SocialPostJson("p3", mon.AddDays(5).AddHours(11), "Samstagsfrühstück? Unsere Zimtschnecken sind ab 7 Uhr da.", "#Frühstück #Zimtschnecken", ["instagram", "pinterest"], status: SocialPlan.Draft),
                ]);
            }),
        ],
        Ribbon =
        [
            ("Beitrag", [
                new ProgramCmd("soc-new", "＋ Beitrag", d => SocialAdd(d), Primary: true, Tip: "Auf die nächste freie feste Zeit (Warteschlange)."),
                new ProgramCmd("soc-dup", "Kopieren", d => { var p = SocSel(d) ?? throw new InvalidOperationException("Erst einen Beitrag wählen."); var c = SocialAdd(d, p.Text, null, p.Networks); BzField(d, "posts", c.Id, "tags", string.Join(" ", p.Hashtags), "Hashtags"); BzField(d, "posts", c.Id, "image", p.Image, "Bild"); }),
                new ProgramCmd("soc-del", "Löschen", d => { var p = SocSel(d) ?? throw new InvalidOperationException("Erst einen Beitrag wählen."); BzRemove(d, "posts", p.Id, "Beitrag gelöscht"); d.Data["sel"] = ""; }),
                new ProgramCmd("soc-posted", "Als gepostet markieren", d => { var p = SocSel(d) ?? throw new InvalidOperationException("Erst einen Beitrag wählen."); BzField(d, "posts", p.Id, "status", SocialPlan.Posted, "Gepostet"); }, Level: 1),
            ]),
            ("KI", [
                new ProgramCmd("soc-ideas", "Ideen (KI)", d => _ = SocialIdeas(d, d.Text("topic")), Tip: "Thema rechts eintragen – die KI schlägt Beiträge vor."),
            ]),
            ("Ansicht", [
                new ProgramCmd("soc-week", "Woche", d => d.Data["view"] = "woche"),
                new ProgramCmd("soc-month", "Monat", d => d.Data["view"] = "monat"),
                new ProgramCmd("soc-table", "Netzwerke", d => d.Data["view"] = "netzwerke", Tip: "Zeichengrenzen und Bildformate"),
                new ProgramCmd("soc-prev", "◀", d => d.Data["day"] = SocDay(d).AddDays(d.Text("view") == "monat" ? -28 : -7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Tip: "Zurück"),
                new ProgramCmd("soc-today", "Heute", d => d.Data["day"] = SocialNow.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                new ProgramCmd("soc-next", "▶", d => d.Data["day"] = SocDay(d).AddDays(d.Text("view") == "monat" ? 35 : 7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Tip: "Weiter"),
            ]),
            ("Erinnern", [
                new ProgramCmd("soc-cal", "In den Kalender", d => SocialToCalendar(d), Tip: "Je Beitrag ein Termin mit Erinnerung in „E-Mails & Termine“."),
            ]),
        ],
        Exports =
        [
            new("csv", "Redaktionsplan (.csv)", ".csv", (d, f) => { File.WriteAllText(f, SocialPlan.ToCsv(SocialPosts(d)), new UTF8Encoding(true)); return f; }),
            new("ics", "Kalender mit Erinnerungen (.ics)", ".ics", (d, f) => { File.WriteAllText(f, SocialPlan.ToIcs(SocialPosts(d)), new UTF8Encoding(false)); return f; }),
            new("zip", "Bildpaket mit Texten (.zip)", ".zip", (d, f) => { if (File.Exists(f)) File.Delete(f); var n = SocialPlan.Package(SocialPosts(d), SocLoad, f); _progMsg[SocialKey] = $"Bildpaket mit {n} zugeschnittenen Bildern und allen Texten."; return f; }),
        ],
        SimpleExports = 2,
        Workspace = SocialWorkspace,
        Properties = SocialProperties,
        Status = d => { var ps = SocialPosts(d); var errs = ps.Count(p => SocialPlan.Check(p).Any(c => c.Error)); return $"{ps.Count} Beiträge · {ps.Count(p => p.Status == SocialPlan.Planned)} geplant · {ps.Count(p => p.Status == SocialPlan.Posted)} gepostet" + (errs > 0 ? $" · {errs} mit Fehlern" : ""); },
        Help = "Mit „＋ Beitrag“ landet ein Beitrag auf der nächsten freien festen Zeit (rechts einstellbar). Im Kalender einen Beitrag anklicken und rechts Text, Hashtags, Netzwerke und Bild eintragen – "
             + "die Zählung zeigt je Netzwerk, ob der Text passt. Zur geplanten Zeit „Kopieren“ und im Netzwerk einfügen. Trendo veröffentlicht nichts selbst; „In den Kalender“ erinnert dich.",
    }, "social media", "redaktionsplan", "redaktionskalender", "instagram", "facebook", "linkedin", "tiktok", "pinterest", "mastodon", "beitrag", "posten", "hashtag", "buffer", "later");

    Control SocialWorkspace(ProgramDocument d)
    {
        var posts = SocialPosts(d); var sel = d.Text("sel"); var view = d.Text("view", "woche"); var day = SocDay(d);
        var root = new StackPanel { Spacing = 8, Margin = new Thickness(4, 0, 10, 4) };
        if (view == "netzwerke")
        {
            var g = new Grid { Name = "soc-table", ColumnDefinitions = new ColumnDefinitions("110,90,80,*,2*") };
            void Row(int r, string a, string b, string c, string dd, string e, bool head = false)
            {
                g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                foreach (var (t, col) in new[] { (a, 0), (b, 1), (c, 2), (dd, 3), (e, 4) })
                {
                    var tb = new TextBlock { Text = t, Foreground = head ? Muted : Text, FontSize = 12, FontWeight = head ? FontWeight.SemiBold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 8, 3) };
                    Grid.SetRow(tb, r); Grid.SetColumn(tb, col); g.Children.Add(tb);
                }
            }
            Row(0, "Netzwerk", "Zeichen", "Hashtags", "Bildformate", "Hinweis", true);
            var i = 1;
            foreach (var n in SocialPlan.Networks) Row(i++, n.Name, n.MaxChars.ToString("N0", De), n.MaxHashtags.ToString(De), string.Join("\n", n.Formats.Select(f => $"{f.Name}: {f.W} × {f.H}")), n.Note);
            root.Children.Add(KrCard(new StackPanel { Spacing = 6, Children = { H("Netzwerke im Überblick", 14), g, P("Werte aus öffentlichen Übersichten (Stand November 2026). Die Netzwerke ändern sie gelegentlich.", Muted, 11) } }, "soc-table-card"));
            return root;
        }
        Control Chip(SocialPost p, bool small)
        {
            var errs = SocialPlan.Check(p).Count(c => c.Error);
            var color = p.Status switch { SocialPlan.Posted => "#4ADE80", SocialPlan.Draft => "#9CA3AF", _ => errs > 0 ? "#F87171" : "#F5A524" };
            var label = $"{p.When:HH:mm} · {string.Join(" ", p.Networks.Select(n => SocialPlan.Network(n).Name[..Math.Min(2, SocialPlan.Network(n).Name.Length)]))}";
            var body = new StackPanel { Spacing = 1, Children = { new TextBlock { Text = label, Foreground = Brush.Parse(color), FontSize = 11, FontWeight = FontWeight.SemiBold } } };
            if (!small) body.Children.Add(new TextBlock { Text = p.Text.Length == 0 ? "(noch ohne Text)" : p.Text, Foreground = Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, MaxHeight = 48, TextTrimming = TextTrimming.CharacterEllipsis });
            var b = new Button { Name = "soc-post-" + p.Id, Content = body, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(6, 4), Margin = new Thickness(0, 0, 0, 3),
                Background = p.Id == sel ? Brush.Parse("#55C9A227") : Brush.Parse("#22FFFFFF"), BorderBrush = Brush.Parse(color), BorderThickness = new Thickness(3, 0, 0, 0) };
            ToolTip.SetTip(b, p.Text);
            b.Click += (_, _) => { d.Data["sel"] = p.Id; Show(SocialKey); };
            return b;
        }
        Button Plus(DateTime dd)
        {
            var b = new Button { Name = "soc-add-" + dd.ToString("yyyyMMdd", CultureInfo.InvariantCulture), Content = "＋", Padding = new Thickness(6, 0), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right };
            ToolTip.SetTip(b, "Beitrag an diesem Tag (erste feste Zeit oder 9:00)");
            b.Click += (_, _) => ProgramRun(SocialKey, () =>
            {
                var slot = SocSlots(d).Where(s => s.Day == dd.DayOfWeek).Select(s => dd + s.Time).Where(t => posts.All(p => p.When != t)).DefaultIfEmpty(dd.AddHours(9)).First();
                SocialAdd(d, "", slot);
            });
            return b;
        }
        if (view == "monat")
        {
            var grid = new Grid { Name = "soc-month", ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*,*") };
            foreach (var (n, c) in new[] { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" }.Select((n, c) => (n, c))) { var h = new TextBlock { Text = n, Foreground = Muted, FontSize = 11, Margin = new Thickness(4, 0) }; Grid.SetColumn(h, c); grid.Children.Add(h); }
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var cells = SocialPlan.MonthGrid(day);
            for (var r = 0; r < 6; r++) grid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)) { MinHeight = 84 });
            for (var i = 0; i < 42; i++)
            {
                var dd = cells[i]; var inMonth = dd.Month == day.Month;
                var sp = new StackPanel { Spacing = 1 };
                sp.Children.Add(new DockPanel { Children = { Plus(dd), new TextBlock { Text = dd.Day.ToString(De), Foreground = dd == SocialNow.Date ? Accent : inMonth ? Text : Muted, FontSize = 12, FontWeight = dd == SocialNow.Date ? FontWeight.Bold : FontWeight.Normal } } });
                foreach (var p in SocialPlan.On(posts, dd)) sp.Children.Add(Chip(p, true));
                var cell = new Border { Child = sp, BorderBrush = Edge, BorderThickness = new Thickness(0.5), Padding = new Thickness(4), Background = inMonth ? Brushes.Transparent : Brush.Parse("#11000000") };
                Grid.SetRow(cell, 1 + i / 7); Grid.SetColumn(cell, i % 7); grid.Children.Add(cell);
            }
            root.Children.Add(H(day.ToString("MMMM yyyy", De), 15));
            root.Children.Add(grid);
        }
        else
        {
            var ws = SocialPlan.WeekStart(day);
            var grid = new Grid { Name = "soc-week", ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*,*") };
            for (var i = 0; i < 7; i++)
            {
                var dd = ws.AddDays(i);
                var sp = new StackPanel { Spacing = 2, MinHeight = 260 };
                sp.Children.Add(new DockPanel { Margin = new Thickness(0, 0, 0, 4), Children = { Plus(dd), new TextBlock { Text = dd.ToString("ddd d.M.", De), Foreground = dd == SocialNow.Date ? Accent : Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold } } });
                foreach (var p in SocialPlan.On(posts, dd)) sp.Children.Add(Chip(p, false));
                var cell = new Border { Child = sp, BorderBrush = Edge, BorderThickness = new Thickness(0.5), Padding = new Thickness(5) };
                Grid.SetColumn(cell, i); grid.Children.Add(cell);
            }
            root.Children.Add(H($"Woche vom {ws.ToString("d. MMMM", De)} bis {ws.AddDays(6).ToString("d. MMMM yyyy", De)}", 15));
            root.Children.Add(grid);
        }
        root.Children.Add(P("Farben: orange = geplant, rot = passt nicht (Zeichen, Bild fehlt), grau = Entwurf, grün = gepostet.", Muted, 11));
        return root;
    }

    Control SocialProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "soc-props", Spacing = 5 };
        var p = SocSel(d);
        if (p is null) sp.Children.Add(P("Einen Beitrag im Kalender anklicken oder mit „＋ Beitrag“ anlegen.", Muted, 12));
        else
        {
            var id = p.Id;
            sp.Children.Add(H("Beitrag", 13));
            var when = new TextBox { Name = "soc-when", Text = p.When.ToString("dd.MM.yyyy HH:mm", De), FontSize = 12 };
            OnEnter(when, () => ProgramRun(SocialKey, () => SocialSetWhen(d, id, when.Text ?? "")));
            when.LostFocus += (_, _) => { if ((when.Text ?? "") != p.When.ToString("dd.MM.yyyy HH:mm", De)) ProgramRun(SocialKey, () => SocialSetWhen(d, id, when.Text ?? "")); };
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "Zeit", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, when } });
            var status = new ComboBox { Name = "soc-status", ItemsSource = new[] { "Entwurf", "Geplant", "Gepostet" }, SelectedIndex = Array.IndexOf(SocialPlan.Statuses, p.Status), FontSize = 12 };
            status.SelectionChanged += (_, _) => { if (status.SelectedIndex >= 0 && SocialPlan.Statuses[status.SelectedIndex] != p.Status) ProgramRun(SocialKey, () => BzField(d, "posts", id, "status", SocialPlan.Statuses[status.SelectedIndex], "Status")); };
            sp.Children.Add(status);
            sp.Children.Add(BzBox(d, "posts", id, "text", "soc-text", "Text des Beitrags", true));
            sp.Children.Add(BzBox(d, "posts", id, "tags", "soc-tags", "Hashtags, z. B. #Bäckerei #Herbst"));
            var nets = new WrapPanel { Name = "soc-nets" };
            foreach (var n in SocialPlan.Networks)
            {
                var cb = new CheckBox { Name = "soc-net-" + n.Id, Content = n.Name, IsChecked = p.Networks.Contains(n.Id), FontSize = 12, Foreground = Text, Margin = new Thickness(0, 0, 8, 0) };
                var nid = n.Id; cb.IsCheckedChanged += (_, _) => { if ((cb.IsChecked == true) != p.Networks.Contains(nid)) ProgramRun(SocialKey, () => SocialSetNet(d, id, nid, cb.IsChecked == true)); };
                nets.Children.Add(cb);
            }
            sp.Children.Add(nets);

            // Bild und Zuschnitt
            sp.Children.Add(H("Bild", 12));
            sp.Children.Add(new WrapPanel { Children = { BzBtn(SocialKey, "soc-image", p.Image.Length == 0 ? "Bild wählen …" : "Anderes Bild …", () => _ = SocialPickImage(d, id)),
                BzBtn(SocialKey, "soc-image-clear", "Entfernen", () => BzField(d, "posts", id, "image", "", "Bild entfernt")) } });
            if (p.Image.Length > 0)
            {
                var crops = new WrapPanel { Name = "soc-crops" };
                var size = BizImageSize(p.Image);
                foreach (var n in p.Networks.Select(SocialPlan.Network))
                {
                    var f = n.Formats[0]; var w = 72.0; var h = w * f.H / f.W;
                    var loss = size is { } s ? SocialPlan.CropLoss(s.W, s.H, f.W, f.H) : 0;
                    crops.Children.Add(new StackPanel { Margin = new Thickness(0, 0, 8, 6), Children = { BizThumb(p.Image, w, h),
                        new TextBlock { Text = $"{n.Name}\n{f.W}×{f.H}" + (loss > 35 ? $"\n−{loss:0} %" : ""), Foreground = loss > 35 ? K4Warn : Muted, FontSize = 10 } } });
                }
                sp.Children.Add(crops);
                if (size is { } sz && p.Networks.Any(n => SocialPlan.Network(n).Formats[0].W > sz.W)) sp.Children.Add(P($"Das Bild ist nur {sz.W} px breit – für scharfe Beiträge mindestens 1080 px.", K4Warn, 11));
            }

            // Vorschau je Netzwerk mit Zählung
            sp.Children.Add(H("Je Netzwerk", 12));
            foreach (var n in p.Networks.Select(SocialPlan.Network))
            {
                var t = SocialPlan.Compose(p, n.Id); var len = SocialPlan.Length(t, n.Id); var over = len > n.MaxChars;
                var nid = n.Id;
                var row = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 6) };
                row.Children.Add(new DockPanel { Children = {
                    BzBtnDock(BzBtnAsync("soc-copy-" + n.Id, "Kopieren", () => BizCopy(SocialKey, t, $"Text für {n.Name}"))),
                    new TextBlock { Name = "soc-count-" + n.Id, Text = $"{n.Name}: {len}/{n.MaxChars:N0}", Foreground = over ? K4Bad : K4Good, FontSize = 12, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center } } });
                row.Children.Add(new TextBlock { Text = t, Foreground = Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, MaxHeight = 90, TextTrimming = TextTrimming.CharacterEllipsis });
                if (ProgramLevel >= UserLevel.Erweitert)
                {
                    var own = new TextBox { Name = "soc-own-" + n.Id, Text = p.PerNetwork?.GetValueOrDefault(n.Id) ?? "", PlaceholderText = $"Eigener Text für {n.Name} (leer = gemeinsamer Text)", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 11.5 };
                    var shown = own.Text; own.LostFocus += (_, _) => { if ((own.Text ?? "") != shown) ProgramRun(SocialKey, () => SocialSetOwn(d, id, nid, own.Text ?? "")); };
                    row.Children.Add(own);
                    row.Children.Add(BzBtnAsync("soc-variant-" + n.Id, "Fassung für " + n.Name + " (KI)", () => SocialVariant(d, id, nid)));
                }
                sp.Children.Add(row);
            }
            sp.Children.Add(H("Prüfung", 12));
            sp.Children.Add(BzFindings("soc-check", SocialPlan.Check(p), c => ((c.Network.Length > 0 ? c.Network + ": " : "") + c.Text, c.Error)));
        }

        sp.Children.Add(H("Feste Zeiten (Warteschlange)", 12));
        sp.Children.Add(KrBox(d, "slots", "soc-slots", "z. B. Mo 09:00, Mi 18:00, Sa 11:00"));
        sp.Children.Add(H("Ideen mit KI", 12));
        sp.Children.Add(KrBox(d, "topic", "soc-topic", "Thema, z. B. Herbstangebote"));
        sp.Children.Add(BzBtnAsync("soc-ideas-go", "Ideen holen", () => SocialIdeas(d, d.Text("topic"))));
        var ideas = d.List("ideas").Select(Js).Where(t => t.Length > 0).ToList();
        var ii = 0;
        foreach (var idea in ideas)
        {
            var text = idea; var take = BzBtn(SocialKey, "soc-idea-" + ii++, "Als Beitrag", () =>
            {
                SocialAdd(d, text, null, SocSel(d)?.Networks);
                d.Edit("Idee übernommen", x => x["ideas"] = new JsonArray([.. ideas.Where(i => i != text).Select(i => (JsonNode)i)]));
            });
            sp.Children.Add(new DockPanel { Children = { BzBtnDock(take), new TextBlock { Text = "• " + idea, Foreground = Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap } } });
        }
        return sp;
    }

    static Control BzBtnDock(Control b) { DockPanel.SetDock(b, Avalonia.Controls.Dock.Right); return b; }

    Button BzBtnAsync(string name, string label, Func<Task> a)
    {
        var b = new Button { Name = name, Content = label, FontSize = 11.5, Padding = new Thickness(8, 3), Margin = new Thickness(0, 0, 4, 2) };
        b.Click += async (_, _) => await a(); return b;
    }

    // ======================================================================
    // Newsletter gestalten (STUDIO-NEWSLETTER-01)
    // ======================================================================

    bool _nlConfirm;
    /// <summary>Zuletzt angelegter E-Mail-Entwurf des Newsletters.</summary>
    public string? NewsletterLastDraft { get; private set; }

    static JsonObject NlJson(string kind, params (string K, string V)[] f)
    {
        var o = new JsonObject { ["id"] = NewId("b"), ["kind"] = kind };
        foreach (var (k, v) in f) o[k] = v;
        if (kind == "bild" && o["width"] is null) o["width"] = "600";
        return o;
    }

    const string NlImpressumSample = "Bäckerei Muster · Erika Muster\nHauptstraße 1, 12345 Musterstadt\nkontakt@example.org";

    static JsonObject NewsDoc(string subject, string pre, IEnumerable<JsonObject> blocks) => new()
    {
        ["subject"] = subject, ["preheader"] = pre, ["from"] = "newsletter@example.org", ["accent"] = "#B45309", ["testTo"] = "",
        ["blocks"] = new JsonArray([.. blocks.Select(b => (JsonNode)b)]), ["sel"] = "", ["view"] = "desktop", ["recipients"] = new JsonArray(),
    };

    static NlBlock NlOf(JsonObject o) => new(Js(o["id"]), Js(o["kind"]), Js(o["title"]), Js(o["sub"]), Js(o["text"]), Js(o["text2"]), Js(o["src"]), Js(o["alt"]), Js(o["url"]),
        Js(o["label"]), Js(o["color"]), int.TryParse(Js(o["width"]), out var w) ? w : 600);

    public NlDoc NewsModel(ProgramDocument d) => new(d.Text("subject"), d.Text("preheader"), d.Text("from"), d.Text("accent", "#B45309"), [.. BzList(d, "blocks").Select(NlOf)]);
    public List<NlRecipient> NewsRecipients(ProgramDocument d) => [.. BzList(d, "recipients").Select(o => new NlRecipient(Js(o["email"]), Js(o["name"]), Js(o["status"]), Js(o["since"])))];
    static void NewsSetRecipients(ProgramDocument d, IEnumerable<NlRecipient> list, string label) =>
        BzSetList(d, "recipients", list.Select(r => new JsonObject { ["email"] = r.Email, ["name"] = r.Name, ["status"] = r.Status, ["since"] = r.Since }), label);

    public List<NlFinding> NewsCheck(ProgramDocument d) => NewsletterKit.Check(NewsModel(d), f => File.Exists(f) && BizImageSize(f) is { } s ? (new FileInfo(f).Length, s.W, s.H) : null);

    public string NewsAdd(ProgramDocument d, string kind)
    {
        var o = kind switch
        {
            "kopf" => NlJson("kopf", ("title", d.Text("subject", "Überschrift")), ("sub", "")),
            "text" => NlJson("text", ("title", "Zwischenüberschrift"), ("text", "Dein Text. **Fett** mit Sternchen, Links so: [Webseite](https://example.org).")),
            "bild" => NlJson("bild", ("src", ""), ("alt", ""), ("url", "")),
            "knopf" => NlJson("knopf", ("label", "Mehr erfahren"), ("url", "https://")),
            "spalten" => NlJson("spalten", ("text", "Linke Spalte"), ("text2", "Rechte Spalte")),
            "trenner" => NlJson("trenner"),
            "fuss" => NlJson("fuss", ("text", NlImpressumSample), ("url", "")),
            _ => throw new ArgumentException("Unbekannter Baustein: " + kind),
        };
        var list = BzList(d, "blocks");
        if (kind == "fuss" && list.Any(b => Js(b["kind"]) == "fuss")) throw new InvalidOperationException("Es gibt schon eine Fußzeile – sie steht immer ganz unten.");
        var foot = list.FindIndex(b => Js(b["kind"]) == "fuss");
        var selIdx = list.FindIndex(b => Js(b["id"]) == d.Text("sel"));
        var at = kind == "fuss" ? list.Count : selIdx >= 0 && selIdx < (foot < 0 ? list.Count : foot) ? selIdx + 1 : foot >= 0 ? foot : list.Count;
        list.Insert(at, o);
        d.Edit("Baustein " + NewsletterKit.Kinds.First(k => k.Id == kind).Name, x => { x["blocks"] = new JsonArray([.. list.Select(b => (JsonNode)b.DeepClone())]); x["sel"] = Js(o["id"]); });
        return Js(o["id"]);
    }

    async Task NewsPickImage(ProgramDocument d, string id)
    {
        var f = await PickMediaFile("newsletter-image", "Bild für den Newsletter", ["*.jpg", "*.jpeg", "*.png", "*.gif"]);
        if (f is not null) ProgramRun(NewsKey, () => BzField(d, "blocks", id, "src", f, "Bild"));
    }

    /// <summary>Schritt 1: Prüfen, dann um Bestätigung bitten. Schritt 2 (<see cref="NewsDraftConfirmed"/>) legt den Entwurf im E-Mail-Programm ab.</summary>
    public bool NewsDraftAsk(ProgramDocument d)
    {
        var errs = NewsCheck(d).Where(f => f.Error).ToList();
        if (errs.Count > 0) { _progMsg[NewsKey] = $"Erst {errs.Count} Pflichtpunkt(e) beheben: " + string.Join(" ", errs.Select(e => e.Text)); _nlConfirm = false; return false; }
        var to = d.Text("testTo").Trim();
        if (to.Length > 0 && !NewsletterKit.ValidEmail(to)) { _progMsg[NewsKey] = "Die Probe-Adresse ist keine gültige E-Mail-Adresse."; _nlConfirm = false; return false; }
        _nlConfirm = true; _progMsg[NewsKey] = "Bitte bestätigen: Der Entwurf geht nur an dein E-Mail-Programm – nicht an die Empfängerliste.";
        return true;
    }

    /// <summary>Schritt 2: EML-Entwurf in den Entwurfsordner von „E-Mail &amp; Kontakte“ schreiben und das E-Mail-Programm vorbereiten. Nichts wird gesendet.</summary>
    public string NewsDraftConfirmed(ProgramDocument d)
    {
        if (!_nlConfirm) throw new InvalidOperationException("Erst „Als Entwurf ins E-Mail-Programm“ wählen und bestätigen.");
        _nlConfirm = false;
        var m = NewsModel(d); var to = d.Text("testTo").Trim();
        Directory.CreateDirectory(MailDraftFolder);
        var safe = new string(m.Subject.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
        var file = Path.Combine(MailDraftFolder, $"{DateTime.Now:yyyy-MM-dd_HHmm} Newsletter {(safe.Length == 0 ? "Entwurf" : safe[..Math.Min(50, safe.Length)])}.eml");
        File.WriteAllText(file, NewsletterKit.Eml(m, to, DateTimeOffset.Now, BizRead), new UTF8Encoding(false));
        _core.Audit.Append(Principal.LocalOwner.Name, "newsletter.entwurf", $"{Path.GetFileName(file)} an {(to.Length == 0 ? "niemanden (ohne Empfänger)" : "eine Probe-Adresse")}");
        _mailTo = to; _mailSubj = m.Subject; _mailBody = NewsletterKit.Text(m);
        NewsletterLastDraft = file;
        _progMsg[NewsKey] = $"Entwurf gespeichert: {file} – mit Doppelklick im E-Mail-Programm öffnen und selbst prüfen. An die Liste verschickt dein Versanddienst, nicht Trendo.";
        NotifyFileResult(_progMsg[NewsKey]);
        return file;
    }

    public async Task<int> NewsSubjects(ProgramDocument d)
    {
        if (_bizAiBusy) return 0;
        var m = NewsModel(d); var body = NewsletterKit.Text(m);
        if (body.Trim().Length < 20) { _progMsg[NewsKey] = "Erst etwas Inhalt schreiben – dann schlägt die KI Betreffzeilen vor."; Show(NewsKey); return 0; }
        _bizAiBusy = true; _progMsg[NewsKey] = "Die KI überlegt Betreffzeilen …"; Show(NewsKey);
        try
        {
            var got = SocialPlan.ParseIdeas(await BizAsk("Schlage 5 kurze, ehrliche Betreffzeilen (höchstens 50 Zeichen, ohne Ausrufezeichen-Ketten, ohne Spam-Wörter) für diesen Newsletter vor, je eine Zeile, nummeriert:\n\n" + body[..Math.Min(3000, body.Length)], "Betreffzeilen"));
            d.Edit("Betreff-Ideen", x => x["subjectIdeas"] = new JsonArray([.. got.Select(s => (JsonNode)s)]));
            _progMsg[NewsKey] = got.Count == 0 ? "Die KI hat keine Betreffzeilen geliefert." : $"{got.Count} Vorschläge rechts unter „Betreff“ – einer per Klick.";
            return got.Count;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException) { _progMsg[NewsKey] = FriendlyError.Say(ex); return 0; }
        finally { _bizAiBusy = false; Show(NewsKey); }
    }

    string NewsExportHtml(ProgramDocument d, string f)
    {
        var m = NewsModel(d); var dir = Path.GetDirectoryName(Path.GetFullPath(f))!; var imgDir = Path.GetFileNameWithoutExtension(f) + "-bilder";
        var map = new Dictionary<string, string>();
        foreach (var b in m.Blocks.Where(b => b.Kind == "bild" && File.Exists(b.Src)))
        {
            Directory.CreateDirectory(Path.Combine(dir, imgDir));
            var name = Path.GetFileName(b.Src); File.Copy(b.Src, Path.Combine(dir, imgDir, name), true); map[b.Src] = imgDir + "/" + name;
        }
        File.WriteAllText(f, NewsletterKit.Html(m, b => map.GetValueOrDefault(b.Src, b.Src)), new UTF8Encoding(false));
        _progMsg[NewsKey] = map.Count > 0 ? $"HTML gespeichert, Bilder im Ordner „{imgDir}“ – beim Versanddienst hochladen und die Bildadressen dort setzen." : "HTML gespeichert.";
        return f;
    }

    private void InitNewsletter() => RegisterProgram(FachTabs, new ProgramSpec(NewsKey, "STUDIO-NEWSLETTER-01", "Newsletter gestalten")
    {
        Sentence = "Newsletter aus Bausteinen – E-Mail-taugliches HTML, Vorschau Desktop und Handy, Pflichtprüfung, Empfänger mit Double-Opt-in.",
        Folder = "Newsletter",
        ScrollWorkspace = true,
        GroupLevels = new() { ["Ansicht"] = 0, ["Prüfen"] = 0, ["Entwurf"] = 0 },
        CmdLevels = new() { ["nl-add-spalten"] = 1, ["nl-add-trenner"] = 1, ["nl-subjects"] = 1 },
        Templates =
        [
            new("leer", "Leerer Newsletter", "Kopf, Text und Fußzeile", () => NewsDoc("", "", [NlJson("kopf", ("title", "Unser Newsletter")), NlJson("text", ("title", ""), ("text", "")), NlJson("fuss", ("text", ""), ("url", ""))])),
            new("neuigkeiten", "Neuigkeiten aus dem Laden", "Kopf, Text, Bild, Knopf, Fußzeile", () => NewsDoc("Neu im November: Kürbisbrot und längere Öffnungszeiten", "Ab Montag haben wir samstags bis 14 Uhr geöffnet.", [
                NlJson("kopf", ("title", "Neues aus der Backstube"), ("sub", "November")),
                NlJson("text", ("title", "Längere Öffnungszeiten"), ("text", "Ab Montag haben wir **samstags bis 14 Uhr** geöffnet. Wir freuen uns auf euren Besuch!\n\nAlle Zeiten findet ihr auf unserer [Webseite](https://example.org).")),
                NlJson("knopf", ("label", "Öffnungszeiten ansehen"), ("url", "https://example.org/zeiten")),
                NlJson("trenner"),
                NlJson("spalten", ("text", "**Kürbisbrot**\nSaftig, mit Kernen – nur im Herbst."), ("text2", "**Zimtschnecken**\nJeden Samstag frisch ab 7 Uhr.")),
                NlJson("fuss", ("text", NlImpressumSample), ("url", "")),
            ])),
            new("angebot", "Angebot mit Gutschein", "Kopf, Text, Knopf", () => NewsDoc("Dein Gutschein für den Herbst", "10 % auf alles bis Sonntag.", [
                NlJson("kopf", ("title", "Herbstgutschein"), ("sub", "10 % auf deinen nächsten Einkauf")),
                NlJson("text", ("title", ""), ("text", "Zeig diese E-Mail an der Kasse oder nutze im Shop den Code **HERBST10**. Gültig bis Sonntag.")),
                NlJson("knopf", ("label", "Zum Shop"), ("url", "https://example.org/shop")),
                NlJson("fuss", ("text", NlImpressumSample), ("url", "")),
            ])),
        ],
        Ribbon =
        [
            ("Bausteine", [.. NewsletterKit.Kinds.Select(k => new ProgramCmd("nl-add-" + k.Id, "＋ " + k.Name, d => NewsAdd(d, k.Id), Primary: k.Id == "text")),
                new ProgramCmd("nl-up", "▲", d => BzMove(d, "blocks", d.Text("sel"), -1), Tip: "Baustein nach oben"),
                new ProgramCmd("nl-down", "▼", d => BzMove(d, "blocks", d.Text("sel"), 1), Tip: "Baustein nach unten"),
                new ProgramCmd("nl-del", "Baustein löschen", d => { var id = d.Text("sel"); if (id.Length == 0) throw new InvalidOperationException("Erst einen Baustein wählen."); BzRemove(d, "blocks", id, "Baustein gelöscht"); d.Data["sel"] = ""; })]),
            ("Ansicht", [
                new ProgramCmd("nl-desktop", "Desktop", d => d.Data["view"] = "desktop"),
                new ProgramCmd("nl-phone", "Handy", d => d.Data["view"] = "handy"),
                new ProgramCmd("nl-text", "Textfassung", d => d.Data["view"] = "text"),
                new ProgramCmd("nl-html", "HTML", d => d.Data["view"] = "html", Level: 2),
                new ProgramCmd("nl-recipients", "Empfänger", d => d.Data["view"] = "empfaenger"),
            ]),
            ("Prüfen", [
                new ProgramCmd("nl-check", "Prüfen", d => { var f = NewsCheck(d); _progMsg[NewsKey] = f.Count == 0 ? "✓ Alles in Ordnung." : $"{f.Count(x => x.Error)} Pflichtpunkte, {f.Count(x => !x.Error)} Hinweise – rechts unter „Prüfung“."; }),
                new ProgramCmd("nl-subjects", "Betreff-Ideen (KI)", d => _ = NewsSubjects(d)),
            ]),
            ("Entwurf", [
                new ProgramCmd("nl-draft", "Als Entwurf ins E-Mail-Programm", d => NewsDraftAsk(d), Tip: "Nur ein Entwurf (Probe an dich) – kein Versand an die Liste."),
            ]),
        ],
        Exports =
        [
            new("html", "E-Mail-HTML (.html)", ".html", (d, f) => NewsExportHtml(d, f)),
            new("eml", "E-Mail-Entwurf (.eml)", ".eml", (d, f) => { File.WriteAllText(f, NewsletterKit.Eml(NewsModel(d), d.Text("testTo"), DateTimeOffset.Now, BizRead), new UTF8Encoding(false)); return f; }),
            new("txt", "Textfassung (.txt)", ".txt", (d, f) => { File.WriteAllText(f, NewsletterKit.Text(NewsModel(d)), new UTF8Encoding(false)); return f; }),
            new("empfaenger", "Empfängerliste (.csv)", ".csv", (d, f) => { File.WriteAllText(f, NewsletterKit.ExportCsv(NewsRecipients(d)), new UTF8Encoding(true)); return f; }),
        ],
        SimpleExports = 2,
        Workspace = NewsWorkspace,
        Properties = NewsProperties,
        Status = d => { var m = NewsModel(d); var f = NewsCheck(d); var r = NewsRecipients(d); return $"{m.Blocks.Count} Bausteine · {Encoding.UTF8.GetByteCount(NewsletterKit.Html(m)) / 1024.0:0.#} KB HTML · " + (f.Any(x => x.Error) ? $"{f.Count(x => x.Error)} Pflichtpunkte offen" : "Pflichtangaben vollständig") + $" · {NewsletterKit.Sendable(r)} von {r.Count} Empfängern bestätigt"; },
        Help = "Bausteine oben einfügen, in der Vorschau anklicken und rechts bearbeiten. „Handy“ zeigt die schmale Ansicht. Die Fußzeile mit Impressum und Abmeldelink ist Pflicht. "
             + "„Prüfen“ findet fehlende Alt-Texte, zu große Bilder und Spam-Wörter. „Als Entwurf ins E-Mail-Programm“ legt nach deiner Bestätigung einen Entwurf an – an die Liste verschickt dein Versanddienst. Nur bestätigte Empfänger (Double-Opt-in) zählen.",
    }, "newsletter", "rundbrief", "mailing", "e-mail-marketing", "mailchimp", "brevo", "abmeldelink", "double-opt-in", "empfängerliste");

    Control NewsWorkspace(ProgramDocument d)
    {
        var m = NewsModel(d); var view = d.Text("view", "desktop"); var sel = d.Text("sel");
        var root = new StackPanel { Spacing = 8, Margin = new Thickness(4, 0, 10, 4) };
        if (_nlConfirm)
        {
            var to = d.Text("testTo").Trim();
            var yes = new Button { Name = "nl-confirm-yes", Content = "Ja, Entwurf anlegen", Padding = new Thickness(12, 5) }; yes.Classes.Add("accent");
            yes.Click += (_, _) => ProgramRun(NewsKey, () => NewsDraftConfirmed(d));
            var no = new Button { Name = "nl-confirm-no", Content = "Abbrechen", Padding = new Thickness(12, 5) };
            no.Click += (_, _) => { _nlConfirm = false; _progMsg[NewsKey] = "Abgebrochen – nichts angelegt."; Show(NewsKey); };
            root.Children.Add(KrCard(new StackPanel { Spacing = 6, Children = {
                H("Entwurf anlegen?", 14),
                P($"Betreff: „{m.Subject}“\nAn: {(to.Length == 0 ? "(leer – du trägst die Adresse im E-Mail-Programm ein)" : to + " (Probe an dich)")}\nDie Empfängerliste ({NewsRecipients(d).Count} Adressen) wird nicht benutzt. Gesendet wird nichts.", Text, 12),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { yes, no } } } }, "nl-confirm"));
        }
        if (view == "empfaenger") { root.Children.Add(NewsRecipientsView(d)); return root; }
        if (view is "text" or "html")
        {
            var src = view == "text" ? NewsletterKit.Text(m) : NewsletterKit.Html(m);
            root.Children.Add(KrCard(new SelectableTextBlock { Name = "nl-source", Text = src, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Cascadia Code, Consolas, monospace") }, "nl-source-card"));
            return root;
        }
        var width = view == "handy" ? 360.0 : 600.0; var acc = Brush.Parse(NlCol(m.Accent));
        var mail = new StackPanel { Name = "nl-preview", Width = width, Background = Brushes.White };
        foreach (var b in m.Blocks)
        {
            Control c = b.Kind switch
            {
                "kopf" => new Border { Background = Brush.Parse(NlCol(b.Color.Length > 0 ? b.Color : m.Accent)), Padding = new Thickness(20, 24), Child = new StackPanel { Children = {
                    new TextBlock { Text = b.Title, Foreground = Brushes.White, FontSize = view == "handy" ? 22 : 26, FontWeight = FontWeight.Bold, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = b.Sub, Foreground = Brushes.White, FontSize = 14, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, IsVisible = b.Sub.Length > 0 } } } },
                "text" => new StackPanel { Margin = new Thickness(20, 12), Spacing = 6, Children = {
                    new TextBlock { Text = b.Title, Foreground = Brush.Parse("#111827"), FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, IsVisible = b.Title.Length > 0 },
                    new TextBlock { Text = NewsPlain(b.Text), Foreground = Brush.Parse("#1F2937"), FontSize = 14, TextWrapping = TextWrapping.Wrap, LineHeight = 21 } } },
                "bild" => new Border { Margin = new Thickness(b.Width >= 600 ? 0 : 20, 6), HorizontalAlignment = HorizontalAlignment.Center,
                    Child = File.Exists(b.Src) ? BizThumb(b.Src, Math.Min(width - (b.Width >= 600 ? 0 : 40), b.Width), Math.Min(width, b.Width) * 0.5, Stretch.Uniform) : new Border { Width = width - 40, Height = 120, Background = Brush.Parse("#E5E7EB"), Child = new TextBlock { Text = b.Alt.Length > 0 ? "[" + b.Alt + "]" : "Bild wählen", Foreground = Brush.Parse("#6B7280"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } } },
                "knopf" => new Border { Background = Brush.Parse(NlCol(b.Color.Length > 0 ? b.Color : m.Accent)), CornerRadius = new CornerRadius(6), Padding = new Thickness(24, 10), Margin = new Thickness(0, 8, 0, 16), HorizontalAlignment = HorizontalAlignment.Center,
                    Child = new TextBlock { Text = b.Label, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeight.Bold } },
                "spalten" => view == "handy"
                    ? new StackPanel { Margin = new Thickness(20, 8), Spacing = 8, Children = { new TextBlock { Text = NewsPlain(b.Text), Foreground = Brush.Parse("#1F2937"), FontSize = 14, TextWrapping = TextWrapping.Wrap }, new TextBlock { Text = NewsPlain(b.Text2), Foreground = Brush.Parse("#1F2937"), FontSize = 14, TextWrapping = TextWrapping.Wrap } } }
                    : new Grid { Margin = new Thickness(20, 8), ColumnDefinitions = new ColumnDefinitions("*,16,*"), Children = { new TextBlock { Text = NewsPlain(b.Text), Foreground = Brush.Parse("#1F2937"), FontSize = 14, TextWrapping = TextWrapping.Wrap }, Pos(new TextBlock { Text = NewsPlain(b.Text2), Foreground = Brush.Parse("#1F2937"), FontSize = 14, TextWrapping = TextWrapping.Wrap }, 2) } },
                "trenner" => new Border { Height = 1, Background = Brush.Parse("#E5E7EB"), Margin = new Thickness(20, 10) },
                "fuss" => new Border { Background = Brush.Parse("#F9FAFB"), Padding = new Thickness(20, 16), Child = new StackPanel { Spacing = 8, Children = {
                    new TextBlock { Text = b.Text.Length > 0 ? b.Text : "Impressum fehlt", Foreground = b.Text.Length > 0 ? Brush.Parse("#6B7280") : Brush.Parse("#DC2626"), FontSize = 11, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = "Newsletter abbestellen", Foreground = Brush.Parse("#6B7280"), FontSize = 11, TextDecorations = TextDecorations.Underline, TextAlignment = TextAlignment.Center } } } },
                _ => new Border(),
            };
            var id = b.Id;
            var hit = new Button { Name = "nl-block-" + id, Content = c, Padding = new Thickness(0), Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                BorderBrush = id == sel ? acc : Brushes.Transparent, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(0) };
            ToolTip.SetTip(hit, NewsletterKit.Kinds.First(k => k.Id == b.Kind).Name + " – anklicken zum Bearbeiten");
            hit.Click += (_, _) => { d.Data["sel"] = id; Show(NewsKey); };
            mail.Children.Add(hit);
        }
        var inbox = new StackPanel { Spacing = 2, Width = width, Children = {
            new TextBlock { Text = m.Subject.Length > 0 ? m.Subject : "(ohne Betreff)", Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
            new TextBlock { Text = m.Preheader, Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis } } };
        root.Children.Add(new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 8, Children = {
            new TextBlock { Text = view == "handy" ? "Handy (360 px)" : "Desktop (600 px)", Foreground = Muted, FontSize = 11 }, inbox,
            new Border { Child = mail, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(view == "handy" ? 18 : 4), ClipToBounds = true, Padding = new Thickness(view == "handy" ? 6 : 0), Background = Brush.Parse("#F3F4F6") } } });
        root.Children.Add(P("Die Vorschau zeigt den Aufbau – wie das echte HTML in einem bestimmten Mailprogramm aussieht, prüfst du mit dem Entwurf an dich selbst.", Muted, 11));
        return root;
    }

    static string NlCol(string c) => System.Text.RegularExpressions.Regex.IsMatch(c ?? "", "^#[0-9A-Fa-f]{6}$") ? c! : "#B45309";
    static string NewsPlain(string t) => System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(t ?? "", @"\*\*(.+?)\*\*", "$1"), @"\[([^\]]+)\]\(([^)\s]+)\)", "$1");

    Control NewsRecipientsView(ProgramDocument d)
    {
        var list = NewsRecipients(d);
        var sp = new StackPanel { Name = "nl-recipients-view", Spacing = 6 };
        sp.Children.Add(H($"Empfänger ({NewsletterKit.Sendable(list)} bestätigt, {list.Count(r => r.Status == NewsletterKit.Pending)} unbestätigt, {list.Count(r => r.Status == NewsletterKit.Unsubscribed)} abgemeldet)", 14));
        sp.Children.Add(P("Nur Verwaltung: Wer sich anmeldet, bekommt eine Bestätigungs-E-Mail (Double-Opt-in). Erst nach dem Klick darauf ist die Adresse „bestätigt“. Trendo verschickt an diese Liste nichts.", Muted, 11));
        var mail = new TextBox { Name = "nl-r-email", PlaceholderText = "E-Mail", Width = 220 }; var name = new TextBox { Name = "nl-r-name", PlaceholderText = "Name (freiwillig)", Width = 160 };
        var add = new Button { Name = "nl-r-add", Content = "Hinzufügen (unbestätigt)" };
        add.Click += (_, _) => ProgramRun(NewsKey, () => { NewsSetRecipients(d, NewsletterKit.Add(NewsRecipients(d), mail.Text ?? "", name.Text ?? "", DateTime.Now), "Empfänger"); _progMsg[NewsKey] = "Adresse aufgenommen – sie wartet auf die Bestätigung."; });
        sp.Children.Add(new WrapPanel { Children = { MSp(mail), MSp(name), MSp(add) } });
        var csv = new TextBox { Name = "nl-r-csv", PlaceholderText = "Liste einfügen: eine Adresse je Zeile (E-Mail;Name)", AcceptsReturn = true, MinHeight = 50, TextWrapping = TextWrapping.Wrap };
        var imp = new Button { Name = "nl-r-import", Content = "Einlesen" };
        imp.Click += (_, _) => ProgramRun(NewsKey, () => { var (l, a, s) = NewsletterKit.ImportCsv(NewsRecipients(d), csv.Text ?? "", DateTime.Now); NewsSetRecipients(d, l, "Liste eingelesen"); _progMsg[NewsKey] = $"{a} Adressen aufgenommen (unbestätigt), {s} übersprungen."; });
        sp.Children.Add(csv); sp.Children.Add(imp);
        var i = 0;
        foreach (var r in list)
        {
            var e = r.Email; var idx = i++;
            var color = r.Status switch { NewsletterKit.Confirmed => K4Good, NewsletterKit.Unsubscribed => K4Bad, _ => K4Warn };
            var row = new WrapPanel { Children = {
                new TextBlock { Text = $"{r.Email}  {r.Name}", Foreground = Text, FontSize = 12.5, Width = 320, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = $"{r.Status} seit {r.Since}", Foreground = color, FontSize = 12, Width = 180, VerticalAlignment = VerticalAlignment.Center } } };
            if (r.Status == NewsletterKit.Pending) row.Children.Add(BzBtn(NewsKey, "nl-r-confirm-" + idx, "Bestätigt", () => NewsSetRecipients(d, NewsletterKit.SetStatus(NewsRecipients(d), e, NewsletterKit.Confirmed, DateTime.Now), "Bestätigt")));
            if (r.Status != NewsletterKit.Unsubscribed) row.Children.Add(BzBtn(NewsKey, "nl-r-unsub-" + idx, "Abgemeldet", () => NewsSetRecipients(d, NewsletterKit.SetStatus(NewsRecipients(d), e, NewsletterKit.Unsubscribed, DateTime.Now), "Abgemeldet")));
            row.Children.Add(BzBtn(NewsKey, "nl-r-del-" + idx, "✕", () => NewsSetRecipients(d, NewsRecipients(d).Where(x => x.Email != e), "Adresse gelöscht"), tip: "Adresse löschen (z. B. auf Wunsch nach DSGVO)"));
            sp.Children.Add(row);
        }
        sp.Children.Add(H("Bestätigungs-E-Mail (zum Kopieren in den Versanddienst)", 12));
        sp.Children.Add(new SelectableTextBlock { Text = NewsletterKit.OptInText(d.Text("from")), Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        return KrCard(sp, "nl-recipients-card");
    }

    Control NewsProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "nl-props", Spacing = 5 };
        sp.Children.Add(H("Betreff", 13));
        sp.Children.Add(KrBox(d, "subject", "nl-subject", "Betreff"));
        sp.Children.Add(KrBox(d, "preheader", "nl-preheader", "Vorschautext (neben dem Betreff sichtbar)"));
        var ideas = d.List("subjectIdeas").Select(Js).Where(s => s.Length > 0).ToList(); var si = 0;
        foreach (var s in ideas) { var ss = s; sp.Children.Add(BzBtn(NewsKey, "nl-subject-idea-" + si++, ss, () => d.Set("subject", ss, "Betreff"))); }
        sp.Children.Add(KrBox(d, "from", "nl-from", "Absender (E-Mail)"));
        sp.Children.Add(KrBox(d, "testTo", "nl-test-to", "Probe-Entwurf an (deine Adresse, freiwillig)"));
        sp.Children.Add(KrBox(d, "accent", "nl-accent", "Akzentfarbe, z. B. #B45309"));

        var sel = BzList(d, "blocks").FirstOrDefault(o => Js(o["id"]) == d.Text("sel"));
        if (sel is not null)
        {
            var b = NlOf(sel); var id = b.Id;
            sp.Children.Add(H("Baustein: " + NewsletterKit.Kinds.First(k => k.Id == b.Kind).Name, 13));
            TextBox F(string field, string hint, bool multi = false) => BzBox(d, "blocks", id, field, "nl-f-" + field, hint, multi);
            switch (b.Kind)
            {
                case "kopf": sp.Children.Add(F("title", "Überschrift")); sp.Children.Add(F("sub", "Unterzeile")); sp.Children.Add(F("color", "Farbe (leer = Akzentfarbe)")); break;
                case "text": sp.Children.Add(F("title", "Zwischenüberschrift (freiwillig)")); sp.Children.Add(F("text", "Text – **fett**, [Link](https://…), Leerzeile = neuer Absatz", true)); break;
                case "bild":
                    sp.Children.Add(new WrapPanel { Children = { BzBtn(NewsKey, "nl-image", b.Src.Length == 0 ? "Bild wählen …" : "Anderes Bild …", () => _ = NewsPickImage(d, id)) } });
                    if (b.Src.Length > 0) sp.Children.Add(P(Path.GetFileName(b.Src) + (BizImageSize(b.Src) is { } s ? $" · {s.W} × {s.H} px · {(BizBytes(b.Src) ?? 0) / 1024} KB" : ""), Muted, 11));
                    sp.Children.Add(F("alt", "Alt-Text (was ist zu sehen?) – Pflicht")); sp.Children.Add(F("url", "Link beim Klick (freiwillig)")); sp.Children.Add(F("width", "Breite in px (bis 600)")); break;
                case "knopf": sp.Children.Add(F("label", "Beschriftung")); sp.Children.Add(F("url", "Link (https://…)")); sp.Children.Add(F("color", "Farbe (leer = Akzentfarbe)")); break;
                case "spalten": sp.Children.Add(F("text", "Linke Spalte", true)); sp.Children.Add(F("text2", "Rechte Spalte", true)); break;
                case "fuss": sp.Children.Add(F("text", "Impressum: Name, Anschrift, E-Mail – Pflicht", true)); sp.Children.Add(F("url", $"Abmeldelink (leer = Platzhalter {NewsletterKit.Unsub})")); break;
                case "trenner": sp.Children.Add(P("Eine dünne Linie zwischen Abschnitten.", Muted, 11)); break;
            }
        }
        sp.Children.Add(H("Prüfung", 13));
        sp.Children.Add(BzFindings("nl-check", NewsCheck(d), f => (f.Text, f.Error)));
        return sp;
    }
}
