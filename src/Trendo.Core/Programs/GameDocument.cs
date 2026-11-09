using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core.Programs;

/// <summary>T-0610 D3/D4: Spielbibel als lebendes Dokument. Abschnitte lassen sich umsortieren und ergänzen, Bilder bleiben im Abschnitt oder in der Ablage,
/// Export als Markdown und PDF. Zu jedem Abschnitt gibt es einen Chat; ein KI-Vorschlag ändert erst nach dem Übernehmen und nur diesen Abschnitt.
/// Die festen Abschnitte lesen die vorhandenen Felder der Spielbibel, damit Bauen und Prüfen unverändert weiterarbeiten.</summary>
public static class GameDocument
{
    public enum SectionKind { Text, Lines, Characters, World, Quests, Milestones }

    public sealed record Section(string Id, string Title, SectionKind Kind, string? Key)
    {
        public bool Custom => Key is null;
        public bool Editable => Kind is SectionKind.Text or SectionKind.Lines;
    }

    public sealed record ChatLine(string Role, string Text);
    public sealed record Reply(string Text, string? Suggestion);

    static readonly Section[] Builtins =
    [
        new("idee", "Idee", SectionKind.Text, "logline"),
        new("kern", "Kernschleife", SectionKind.Text, "loop"),
        new("mechaniken", "Mechaniken", SectionKind.Lines, "mechanics"),
        new("steuerung", "Steuerung", SectionKind.Text, "controls"),
        new("stil", "Stil", SectionKind.Text, "style"),
        new("figuren", "Figuren", SectionKind.Characters, "characters"),
        new("welt", "Welt", SectionKind.World, "world"),
        new("missionen", "Missionen", SectionKind.Quests, "quests"),
        new("systeme", "Systeme", SectionKind.Text, "systems"),
        new("ton", "Ton und Musik", SectionKind.Text, "sound"),
        new("menues", "Menüs", SectionKind.Text, "menus"),
        new("meilensteine", "Meilensteine", SectionKind.Milestones, "milestones"),
    ];

    static string S(JsonNode? n) => n?.GetValueKind() == JsonValueKind.String ? n.GetValue<string>() : "";
    static IEnumerable<JsonObject> Objs(JsonObject d, string key) => (d[key] as JsonArray ?? []).OfType<JsonObject>();
    static JsonArray Strings(IEnumerable<string> l) => new([.. l.Select(x => (JsonNode)x)]);
    static List<string> Texts(JsonNode? n) => [.. (n as JsonArray ?? []).Select(S).Where(x => x.Length > 0)];
    /// <summary>Listen-Einträge sicher lesen: KI-Bibeln enthalten statt Texten auch Einträge mit Name und Beschreibung.</summary>
    static List<string> Entries(JsonNode? n) => [.. (n as JsonArray ?? []).Select(x => x.AsText() ?? "").Where(x => x.Length > 0)];

    /// <summary>Abschnitte in der gewählten Reihenfolge; was in der Reihenfolge fehlt, kommt hinten dazu.</summary>
    public static List<Section> Sections(JsonObject d)
    {
        var all = Builtins.Concat(Objs(d, "docCustom").Where(c => S(c["id"]).Length > 0).Select(c => new Section(S(c["id"]), S(c["title"]), SectionKind.Text, null))).ToList();
        var result = Texts(d["docOrder"]).Select(id => all.FirstOrDefault(s => s.Id == id)).OfType<Section>().Distinct().ToList();
        result.AddRange(all.Except(result));
        return result;
    }

    public static Section? Find(JsonObject d, string id) => Sections(d).FirstOrDefault(s => s.Id == id);

    public static bool Move(JsonObject d, string id, int delta)
    {
        var ids = Sections(d).Select(s => s.Id).ToList();
        var i = ids.IndexOf(id); var j = i + delta;
        if (i < 0 || j < 0 || j >= ids.Count) return false;
        (ids[i], ids[j]) = (ids[j], ids[i]);
        d["docOrder"] = Strings(ids);
        return true;
    }

    public static string AddCustom(JsonObject d, string title)
    {
        var used = Sections(d).Select(s => s.Id).ToHashSet();
        var n = 1; while (used.Contains($"abschnitt-{n}")) n++;
        var id = $"abschnitt-{n}";
        var ids = Sections(d).Select(s => s.Id).Append(id).ToList();
        if (d["docCustom"] is not JsonArray custom) d["docCustom"] = custom = [];
        custom.Add(new JsonObject { ["id"] = id, ["title"] = string.IsNullOrWhiteSpace(title) ? "Neuer Abschnitt" : title.Trim(), ["text"] = "" });
        d["docOrder"] = Strings(ids);
        return id;
    }

    /// <summary>Eigenen Abschnitt entfernen; seine Bilder wandern in die Ablage. Feste Abschnitte bleiben.</summary>
    public static bool RemoveCustom(JsonObject d, string id)
    {
        if (d["docCustom"] is not JsonArray custom || custom.OfType<JsonObject>().FirstOrDefault(c => S(c["id"]) == id) is not { } item) return false;
        foreach (var img in Images(d, id)) ToVault(d, id, img);
        custom.Remove(item);
        (d["docImages"] as JsonObject)?.Remove(id);
        d["docOrder"] = Strings(Sections(d).Select(s => s.Id));
        return true;
    }

    public static void SetTitle(JsonObject d, string id, string title)
    {
        if (Objs(d, "docCustom").FirstOrDefault(c => S(c["id"]) == id) is { } c && title.Trim().Length > 0) c["title"] = title.Trim();
    }

    /// <summary>Inhalt zum Anzeigen und für die KI; Listen werden als Zeilen dargestellt, Mechaniken mit Komma.</summary>
    public static string Text(JsonObject d, Section s) => s.Kind switch
    {
        SectionKind.Text when s.Custom => S(Objs(d, "docCustom").FirstOrDefault(c => S(c["id"]) == s.Id)?["text"]),
        SectionKind.Text => S(d[s.Key!]),
        SectionKind.Lines => string.Join(", ", Entries(d[s.Key!])),
        _ => Body(d, s).Replace("**", ""),
    };

    public static void SetText(JsonObject d, string id, string text)
    {
        if (Find(d, id) is not { Editable: true } s) return;
        if (s.Kind == SectionKind.Lines) { d[s.Key!] = Strings(text.Split([',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)); return; }
        if (s.Custom) { Objs(d, "docCustom").First(c => S(c["id"]) == id)["text"] = text; return; }
        d[s.Key!] = text;
    }

    static string Body(JsonObject d, Section s) => s.Kind switch
    {
        SectionKind.Lines => string.Join("\n", Entries(d[s.Key!]).Select(m => "- " + m)),
        SectionKind.Characters => string.Join("\n", Objs(d, s.Key!).Select(c => $"- **{S(c["name"])}** ({S(c["role"])}{(S(c["role"]) == "Boss" ? $", {ProgramDocument.ToNumber(c["phases"]):0} Phasen" : "")}): {S(c["text"])}")),
        SectionKind.World => string.Join("\n", Objs(d, s.Key!).Select(r => $"- **{S(r["name"])}**: {S(r["text"])}")),
        SectionKind.Quests => string.Join("\n", Objs(d, s.Key!).Select(q => $"- **{S(q["title"])}** ({S(q["kind"])}): {S(q["goal"])}{(S(q["needs"]).Length > 0 ? $" · braucht {S(q["needs"])}" : "")}{(S(q["gives"]).Length > 0 ? $" · gibt {S(q["gives"])}" : "")}")),
        SectionKind.Milestones => string.Join("\n", Objs(d, s.Key!).Select(m => $"- [{(m["done"]?.GetValueKind() == JsonValueKind.True ? "x" : " ")}] {S(m["name"])}")),
        _ => Text(d, s),
    };

    static string Head(JsonObject d) =>
        $"**Umfang:** {GameDesign.ScaleOf(S(d["scale"])).Name} · **Genre:** {S(d["genre"])} · **Plattformen:** {S(d["platforms"])}{(S(d["audience"]).Length > 0 ? $" · **Zielgruppe:** {S(d["audience"])}" : "")}";

    /// <summary>Spielbibel als Markdown (für das Team, das Unreal-Projekt oder zum Ausdrucken), in der gewählten Reihenfolge.</summary>
    public static string Markdown(JsonObject d)
    {
        var sb = new StringBuilder($"# {S(d["title"])}\n\n{Head(d)}\n\n");
        foreach (var s in Sections(d))
        {
            sb.Append($"## {s.Title}\n\n{Body(d, s)}\n\n");
            foreach (var img in Images(d, s.Id)) sb.Append($"![{s.Title}]({Path.GetFileName(img)})\n\n"); // nur der Name: der Text geht auch an KIs und in Exporte
        }
        return sb.ToString().TrimEnd() + "\n";
    }

    // ───────────── Bilder ─────────────

    public static List<string> Images(JsonObject d, string id) => Texts((d["docImages"] as JsonObject)?[id]);
    public static List<string> Vault(JsonObject d) => Texts(d["docVault"]);

    /// <summary>Nur lokale PNG- und JPG-Dateien werden geöffnet: Pfade stehen im Dokument, das auch aus fremder Quelle stammen kann, und ein Netzwerkpfad würde beim Anzeigen eine Verbindung aufbauen.</summary>
    public static bool IsLocalImage(string path)
    {
        static bool Sep(char c) => c is '\\' or '/';
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0') || (path.Length > 1 && Sep(path[0]) && Sep(path[1])) || !Path.IsPathFullyQualified(path)) return false;
        return Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg";
    }

    static void Attach(JsonObject d, string id, string path)
    {
        if (d["docImages"] is not JsonObject map) d["docImages"] = map = [];
        if (map[id] is not JsonArray l) map[id] = l = [];
        if (!Texts(l).Contains(path)) l.Add(path);
    }

    static void Drop(JsonArray? l, string path) { if (l?.FirstOrDefault(x => S(x) == path) is { } n) l.Remove(n); }

    public static void AddImage(JsonObject d, string id, string path) { if (Find(d, id) is not null) Attach(d, id, path); }

    public static void ToVault(JsonObject d, string id, string path)
    {
        Drop((d["docImages"] as JsonObject)?[id] as JsonArray, path);
        if (d["docVault"] is not JsonArray vault) d["docVault"] = vault = [];
        if (!Texts(vault).Contains(path)) vault.Add(path);
    }

    public static void FromVault(JsonObject d, string id, string path)
    {
        if (Find(d, id) is null) return;
        Drop(d["docVault"] as JsonArray, path);
        Attach(d, id, path);
    }

    // ───────────── PDF ─────────────

    /// <summary>Spielbibel als PDF mit Text und Bildern der Abschnitte (die Ablage bleibt draußen); gibt die Seitenzahl zurück.</summary>
    public static int WritePdf(JsonObject d, string path)
    {
        const double Mm = 72 / 25.4, Left = 22, Width = 166, Top = 20, Bottom = 277, MaxImage = 120, MaxImageHeight = 150;
        Sys.PdfTools.EnsureFonts();
        using var pdf = new PdfDocument();
        pdf.Info.Title = S(d["title"]);
        var title = new XFont("Arial", 22, XFontStyleEx.Bold); var heading = new XFont("Arial", 14, XFontStyleEx.Bold); var text = new XFont("Arial", 10.5, XFontStyleEx.Regular);
        var measure = XGraphics.CreateMeasureContext(new XSize(595, 842), XGraphicsUnit.Point, XPageDirection.Downwards);
        XGraphics? g = null; double y = 0;
        void NewPage() { g?.Dispose(); var p = pdf.AddPage(); p.Width = XUnit.FromMillimeter(210); p.Height = XUnit.FromMillimeter(297); g = XGraphics.FromPdfPage(p); y = Top; }
        void Line(string t, XFont f, XColor c, double step)
        {
            if (y + step > Bottom) NewPage();
            g!.DrawString(t, f, new XSolidBrush(c), new XRect(Left * Mm, y * Mm, Width * Mm, step * Mm), XStringFormats.TopLeft);
            y += step;
        }
        void Paragraph(string t, XFont f, XColor c, double step)
        {
            foreach (var para in t.Replace("\r", "").Split('\n'))
            {
                var line = "";
                foreach (var word in para.Split(' '))
                {
                    var test = line.Length == 0 ? word : line + " " + word;
                    if (measure.MeasureString(test, f).Width <= Width * Mm || line.Length == 0) { line = test; continue; }
                    Line(line, f, c, step); line = word;
                }
                Line(line, f, c, step);
            }
        }
        NewPage();
        Paragraph(S(d["title"]), title, XColors.Black, 10);
        y += 2; Paragraph(Head(d).Replace("**", ""), text, XColor.FromArgb(0x5C, 0x6B, 0x7A), 5); y += 5;
        foreach (var s in Sections(d))
        {
            if (y + 22 > Bottom) NewPage(); // Überschrift nicht allein am Seitenende
            Line(s.Title, heading, XColors.Black, 7); y += 1;
            var body = Body(d, s).Replace("**", "");
            if (body.Trim().Length > 0) Paragraph(body, text, XColors.Black, 5);
            foreach (var file in Images(d, s.Id))
            {
                try
                {
                    if (!IsLocalImage(file)) throw new ArgumentException("Kein lokales Bild.");
                    using var img = XImage.FromFile(file);
                    var w = Math.Min(MaxImage, img.PixelWidth * 25.4 / 96); var h = w * img.PixelHeight / img.PixelWidth;
                    if (h > MaxImageHeight) { w *= MaxImageHeight / h; h = MaxImageHeight; }
                    y += 2;
                    if (y + h > Bottom) NewPage();
                    g!.DrawImage(img, Left * Mm, y * Mm, w * Mm, h * Mm);
                    y += h;
                }
                catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
                {
                    Line("[Bild konnte nicht eingebettet werden: " + Path.GetFileName(file) + "]", text, XColor.FromArgb(0x5C, 0x6B, 0x7A), 5);
                }
            }
            y += 5;
        }
        g?.Dispose();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var pages = pdf.PageCount;
        var tmp = path + ".tmp"; pdf.Save(tmp); SafeFile.Move(tmp, path);
        return pages;
    }

    // ───────────── Chat je Abschnitt ─────────────

    public static List<ChatLine> Chat(ProgramDocument doc, string id) =>
        [.. ((doc.Meta["docChats"] as JsonObject)?[id] as JsonArray ?? []).OfType<JsonObject>().Select(o => new ChatLine(S(o["role"]), S(o["text"])))];

    public static void AddChat(ProgramDocument doc, string id, string role, string text) => doc.EditMeta(m =>
    {
        if (m["docChats"] is not JsonObject all) m["docChats"] = all = [];
        if (all[id] is not JsonArray l) all[id] = l = [];
        l.Add(new JsonObject { ["role"] = role, ["text"] = text });
    });

    /// <summary>Verlauf eines entfernten Abschnitts löschen, damit ein neuer Abschnitt mit derselben Kennung ihn nicht erbt.</summary>
    public static void ClearChat(ProgramDocument doc, string id) { if ((doc.Meta["docChats"] as JsonObject)?.ContainsKey(id) == true) doc.EditMeta(m => (m["docChats"] as JsonObject)?.Remove(id)); }

    static bool ValidList(Section s, JsonNode? n)
    {
        if (n is not JsonArray { Count: > 0 } list) return false;
        return s.Kind switch
        {
            SectionKind.Milestones => list.All(x => (x is JsonObject o ? S(o["name"]) : S(x)).Length > 0),
            SectionKind.Quests => list.All(x => x is JsonObject o && S(o["title"]).Length > 0),
            _ => list.All(x => x is JsonObject o && S(o["name"]).Length > 0),
        };
    }

    public static string ChatInstructions(Section s)
    {
        var format = s.Kind switch
        {
            SectionKind.Text or SectionKind.Lines => "den vollständigen neuen Text des Abschnitts in einem Block ```abschnitt … ```" + (s.Kind == SectionKind.Lines ? " (Einträge mit Komma getrennt)" : ""),
            SectionKind.Characters => "die vollständige Liste als JSON-Array in einem Block ```json … ``` (name, role = Held|Begleiter|Gegner|Boss|Nebenfigur, text, phases)",
            SectionKind.World => "die vollständige Liste als JSON-Array in einem Block ```json … ``` (name, text)",
            SectionKind.Quests => "die vollständige Liste als JSON-Array in einem Block ```json … ``` (title, kind = Haupt|Neben, goal, needs, gives, giver)",
            _ => "die vollständige Liste der Meilensteine als JSON-Array aus Texten in einem Block ```json … ```",
        };
        return $"Du berätst bei der Spieleentwicklung und arbeitest am Abschnitt „{s.Title}“ eines Design-Dokuments. Antworte auf Deutsch, kurz und konkret, passend zum ganzen Spiel. " +
               $"Nur wenn du eine Änderung vorschlägst, liefere zusätzlich {format}. Keine echten Personen, Marken oder fremden Spielfiguren.";
    }

    public static string ChatPrompt(JsonObject d, Section s, string question, IEnumerable<ChatLine> history)
    {
        var all = Markdown(d); if (all.Length > 6000) all = all[..6000] + " …";
        var sb = new StringBuilder($"Das ganze Spiel:\n{all}\n\nAktueller Abschnitt „{s.Title}“:\n{Text(d, s)}\n\n");
        var past = history.TakeLast(10).ToList();
        if (past.Count > 0) { sb.Append("Bisheriges Gespräch:\n"); foreach (var l in past) sb.Append($"{l.Role}: {l.Text}\n"); sb.Append('\n'); }
        return sb.Append("Wunsch: ").Append(question.Trim()).ToString();
    }

    /// <summary>KI-Antwort trennen in Text und Vorschlag. Eine unlesbare Liste wird kein Vorschlag, damit sie nie ins Dokument gelangt.</summary>
    public static Reply ParseReply(string answer, Section s)
    {
        var m = Regex.Match(answer, @"```(?:[a-zA-ZäöüÄÖÜ]+[ \t]*\r?\n|\r?\n)?([\s\S]*?)```");
        if (!m.Success) return new Reply(answer.Trim(), null);
        var text = (answer[..m.Index] + answer[(m.Index + m.Length)..]).Trim();
        var suggestion = m.Groups[1].Value.Trim();
        if (suggestion.Length == 0) return new Reply(text, null);
        if (!s.Editable)
        {
            bool ok;
            try { ok = ValidList(s, JsonNode.Parse(suggestion)); }
            catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException) { ok = false; } // auch doppelte Schlüssel
            if (!ok) return new Reply(text, null);
        }
        return new Reply(text, suggestion);
    }

    /// <summary>Vorschlag auf genau diesen Abschnitt anwenden; alle anderen Felder bleiben unberührt.</summary>
    public static void Apply(JsonObject d, Section s, string suggestion)
    {
        if (s.Editable) { SetText(d, s.Id, suggestion); return; }
        var list = JsonNode.Parse(suggestion) as JsonArray ?? throw new InvalidDataException("Der Vorschlag ist keine Liste.");
        if (s.Kind == SectionKind.Milestones)
        {
            var done = Objs(d, "milestones").Where(m => m["done"]?.GetValueKind() == JsonValueKind.True).Select(m => S(m["name"])).ToHashSet();
            var names = list.Select(n => n is JsonObject o ? S(o["name"]) : S(n)).Where(x => x.Length > 0);
            d["milestones"] = new JsonArray([.. names.Select(n => (JsonNode)new JsonObject { ["name"] = n, ["done"] = done.Contains(n) })]);
            return;
        }
        var merged = d.DeepClone().AsObject();
        merged[s.Key!] = list;
        d[s.Key!] = GameDirector.Normalize(merged)[s.Key!]!.DeepClone();
    }

    /// <summary>Was würde sich ändern? Das Dokument selbst bleibt unverändert.</summary>
    public static List<string> Preview(JsonObject d, Section s, string suggestion)
    {
        var after = d.DeepClone().AsObject();
        Apply(after, s, suggestion);
        if (s.Kind == SectionKind.Text) return [suggestion];
        if (s.Kind == SectionKind.Milestones) return [.. Text(after, s).Split('\n')];
        var diff = GameDirector.Diff(d, after);
        return diff.Count > 0 ? diff : ["Keine Änderung"];
    }
}
