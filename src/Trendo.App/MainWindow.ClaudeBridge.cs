using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.Api;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Freigabe 09.11.2026: Claude bedient jedes Programm selbst, während es arbeitet. Trendo stellt dafür Werkzeuge bereit (lesen, füllen, klicken, Befehle, Dokumentdaten, Bild ansehen).
/// Nachgefragt wird nur bei Wirkung nach außen oder wenn sich etwas nicht zurückholen lässt, und zwar im Chat.</summary>
public partial class MainWindow
{
    bool? _handsFree;
    /// <summary>Claude arbeitet ohne Klicks. Im Testmodus aus, damit die Prüfungen des Plan-Wegs (lokale KIs) weiter gelten; neue Tests schalten es ein.</summary>
    public bool ClaudeNoClicks { get => _handsFree ?? (!_opt.Isolated && KiSourceSettings.Load(_core.DataRoot).ClaudeNoClicks); set => _handsFree = value; }
    /// <summary>Für Tests: Zustimmung im Chat nachstellen.</summary>
    public Func<string, Task<bool>>? ClaudeConsentOverride { get; set; }
    /// <summary>Durchgänge, bis Claude fertig sein muss.</summary>
    int ClaudeRounds => ClaudeNoClicks ? 8 : ClaudeProbe.Rounds;

    sealed class BridgeRun { public ClaudeDock? Dock; public required string Folder; public string? Page; public string? ChatId; public readonly HashSet<string> Allowed = []; }

    sealed class ClaudeBridgeSession(ClaudeBridgeHost host, string configFile) : IAsyncDisposable
    {
        public string ConfigFile => configFile;
        public async ValueTask DisposeAsync()
        {
            try { File.Delete(configFile); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            await host.DisposeAsync();
        }
    }

    /// <summary>Zugang für einen Claude-Lauf öffnen. null, wenn Claude nicht selbst arbeiten soll oder der Zugang nicht startet.</summary>
    async Task<ClaudeBridgeSession?> ClaudeBridgeOpen(ClaudeDock dock, string folder, string chatId)
    {
        if (!ClaudeNoClicks) return null;
        try
        {
            _claudeChanges.Clear(); // neuer Lauf: das Rückgängig bezieht sich auf diesen Lauf
            var host = await ClaudeBridgeHost.StartAsync(new TrendoMcp(BridgeTools(new BridgeRun { Dock = dock, Folder = folder, Page = dock.Page, ChatId = chatId })));
            var dir = Path.Combine(_core.DataRoot, "cache", "claude-bruecke"); Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".json");
            var access = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) access.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite; // das Kennzeichen geht nur den eigenen Nutzer an
            await using (var w = new StreamWriter(file, System.Text.Encoding.UTF8, access)) await w.WriteAsync(host.ConfigJson());
            return new ClaudeBridgeSession(host, file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { _core.Audit.Append(Principal.LocalOwner.Name, "claude.bruecke.fehler", e.Message); return null; }
    }

    /// <summary>Werkzeuge für Tests ohne Netz.</summary>
    public TrendoMcp ClaudeBridgeForTest(string folder, string? page = null, string? chatId = null) => new(BridgeTools(new BridgeRun { Folder = folder, Page = page ?? CurrentView, ChatId = chatId }));

    IReadOnlyList<McpTool> BridgeTools(BridgeRun run)
    {
        McpTool T(string name, string text, JsonObject schema, Func<JsonObject, Task<McpResult>> body) => new(name, text, schema, a => BridgeUi(run, name, body, a));
        McpTool S(string name, string text, JsonObject schema, Func<JsonObject, McpResult> body) => T(name, text, schema, a => Task.FromResult(body(a)));
        var programArg = ("programm", "string", "Kennung des Programms; ohne Angabe das zuletzt benutzte", false);
        return
        [
            S("programme", "Alle Programme mit Dokumenten und alle Studio-Seiten mit Kennung und Name sowie die gerade offene Seite. Beides lässt sich mit programm_oeffnen öffnen.", TrendoMcp.Schema(), _ => new McpResult(new JsonObject
            {
                ["offen"] = CurrentView, ["aktuell"] = run.Page,
                ["seiten"] = new JsonArray(StudioPages.Where(p => !Programs.ContainsKey(p.Value)).OrderBy(p => p.Key).Select(p => (JsonNode)new JsonObject { ["programm"] = p.Value, ["name"] = StudioCatalog.NameOf(p.Key, p.Value), ["studio"] = p.Key }).ToArray()),
                ["programme"] = new JsonArray(Programs.Values.OrderBy(p => p.Title).Select(p => (JsonNode)new JsonObject { ["programm"] = p.Key, ["name"] = p.Title, ["studio"] = p.Studio, ["zweck"] = p.Sentence }).ToArray()),
            }.ToJsonString())),
            T("programm_oeffnen", "Öffnet ein Programm oder eine Seite. Ohne offenes Dokument oder mit neu=true entsteht ein neues, wahlweise aus einer Vorlage.", TrendoMcp.Schema(("programm", "string", "Kennung", true), ("vorlage", "string", "Kennung der Vorlage", false), ("neu", "boolean", "Neues Dokument anlegen", false)), async a =>
            {
                var key = Need(a, "programm");
                if (Programs.TryGetValue(key, out var spec))
                {
                    if (a["neu"].AsBool() || ProgramActive(key) is null)
                    {
                        var template = a["vorlage"].AsText();
                        if (template is { Length: > 0 } && spec.Templates.All(t => t.Id != template)) return McpResult.Fail("Diese Vorlage gibt es nicht. Vorhanden: " + string.Join(", ", spec.Templates.Select(t => t.Id)));
                        ProgramNew(key, template);
                    }
                    Show(key);
                }
                else if (_showMap.ContainsKey(key)) Show(key);
                else return McpResult.Fail("Diese Seite gibt es nicht. Liste mit „programme“ abrufen.");
                run.Page = key;
                await Pump();
                return BridgePage(run);
            }),
            S("seite_lesen", "Alles auf der offenen Seite: Texte, Felder, Auswahllisten, Schalter, Regler, Knöpfe. Ohne Geheimnisfelder.", TrendoMcp.Schema(), _ => BridgePage(run)),
            T("feld_setzen", "Trägt einen Wert in ein Feld, einen Regler oder einen Schalter ein. Ziel ist der Name oder die Beschriftung aus seite_lesen.", TrendoMcp.Schema(("ziel", "string", "Name oder Beschriftung", true), ("wert", "any", "Text, Zahl oder true/false", true)), async a =>
            {
                var c = BridgeFind(Need(a, "ziel")) ?? throw new ArgumentException("Dieses Element gibt es auf der Seite nicht: " + Need(a, "ziel"));
                if (await BridgeControlGuard(run, c, $"„{BridgeLabel(c)}“ ausfüllen") is { } refusedField) return refusedField;
                var value = a["wert"].AsText() ?? "";
                switch (c)
                {
                    case TextBox tb when tb.PasswordChar != default || ClaudeActions.Secret(tb.PlaceholderText ?? "", tb.Name ?? ""): return McpResult.Fail("Geheimnisfelder füllt nur der Nutzer selbst aus.");
                    case TextBox tb when tb.IsEffectivelyEnabled && !tb.IsReadOnly: tb.Focus(); tb.Text = value; break; // T-0780: wie getippt – beim Verlassen übernimmt das Programm den Wert (danach Prüfung des Schritts)
                    case Slider s when double.TryParse(value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture, out var v): s.Value = Math.Clamp(v, s.Minimum, s.Maximum); break;
                    case NumericUpDown n when decimal.TryParse(value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture, out var v): n.Value = v; break;
                    case ToggleButton t when bool.TryParse(value, out var v): t.IsChecked = v; break;
                    default: return McpResult.Fail("Dieses Element nimmt keinen Wert an.");
                }
                ClaudeFlash(c); await Pump();
                return BridgeDone(run);
            }),
            T("auswaehlen", "Wählt einen Eintrag in einer Auswahlliste, Liste oder einem Reiter.", TrendoMcp.Schema(("ziel", "string", "Name oder Beschriftung", true), ("eintrag", "string", "Text des Eintrags", true)), async a =>
            {
                var c = BridgeFind(Need(a, "ziel")) as SelectingItemsControl ?? throw new ArgumentException("Hier gibt es keine Auswahl: " + Need(a, "ziel"));
                if (await BridgeControlGuard(run, c, $"„{BridgeLabel(c)}“ auswählen") is { } refusedChoice) return refusedChoice;
                var wanted = Need(a, "eintrag");
                var item = c.Items.FirstOrDefault(i => ItemText(i).Equals(wanted, StringComparison.OrdinalIgnoreCase));
                if (item is null) return McpResult.Fail("Diesen Eintrag gibt es nicht. Vorhanden: " + string.Join(", ", c.Items.Select(ItemText).Take(60)));
                c.SelectedItem = item; ClaudeFlash(c); await Pump();
                return BridgeDone(run);
            }),
            T("klicken", "Drückt einen Knopf oder Menüeintrag der offenen Seite. Danach kommt der neue Stand zurück.", TrendoMcp.Schema(("ziel", "string", "Name oder Beschriftung", true)), async a =>
            {
                var c = BridgeFind(Need(a, "ziel")) ?? throw new ArgumentException("Dieses Element gibt es auf der Seite nicht: " + Need(a, "ziel"));
                var label = c is Button bt ? ButtonText(bt) : c is MenuItem mi0 ? mi0.Header?.ToString() ?? "" : "";
                if (await BridgeControlGuard(run, c, $"„{label}“ im Programm drücken") is { } refused) return refused;
                var page = BridgeKey(run); var doc = page is null ? null : ProgramActive(page);
                if (doc is not null) ClaudeChangeBegin(page!, doc);
                ClaudeFlash(c);
                switch (c)
                {
                    case ToggleButton t when t.IsEffectivelyEnabled: t.IsChecked = t.IsChecked != true; break;
                    case Button b when b.IsEffectivelyEnabled: b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); break;
                    case MenuItem m when m.IsEffectivelyEnabled: m.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); break;
                    default: return McpResult.Fail("Dieses Element lässt sich gerade nicht drücken.");
                }
                await Pump();
                if (doc is not null) ClaudeChangeAfter(page!, doc);
                return BridgeDone(run);
            }),
            S("befehle", "Vollständige Liste eines Programms, unabhängig von der Bedienstufe: Befehle je Gruppe, Vorlagen, Exporte, Werkzeuge.", TrendoMcp.Schema(programArg), a =>
            {
                var spec = BridgeSpec(run, a);
                return new McpResult(new JsonObject
                {
                    ["programm"] = spec.Key, ["name"] = spec.Title, ["hilfe"] = spec.Help,
                    ["befehle"] = new JsonArray(spec.Ribbon.SelectMany(g => g.Cmds.Select(c => (JsonNode)new JsonObject { ["gruppe"] = g.Group, ["name"] = c.Name, ["text"] = c.Label, ["hinweis"] = c.Tip, ["stufe"] = c.Level })).ToArray()),
                    ["vorlagen"] = new JsonArray(spec.Templates.Select(t => (JsonNode)new JsonObject { ["vorlage"] = t.Id, ["name"] = t.Name }).ToArray()),
                    ["exporte"] = new JsonArray(spec.Exports.Select(x => (JsonNode)new JsonObject { ["format"] = x.Name, ["text"] = x.Label, ["endung"] = x.Extension }).ToArray()),
                    ["werkzeuge"] = new JsonArray(spec.Tools.Select(t => (JsonNode)new JsonObject { ["name"] = t.Name, ["text"] = t.Label, ["hinweis"] = t.Tip }).ToArray()),
                }.ToJsonString());
            }),
            T("befehl", "Führt einen Befehl des Programms aus, auch wenn er in der aktuellen Bedienstufe versteckt ist.", TrendoMcp.Schema(("name", "string", "Name oder Text aus befehle", true), programArg), async a =>
            {
                var spec = BridgeSpec(run, a); var doc = BridgeOpenDoc(spec);
                var wanted = Need(a, "name");
                var cmd = spec.Ribbon.SelectMany(g => g.Cmds).FirstOrDefault(c => c.Name == wanted || c.Label.Equals(wanted, StringComparison.OrdinalIgnoreCase));
                if (cmd is null) return McpResult.Fail("Diesen Befehl gibt es nicht. Liste mit „befehle“ abrufen.");
                if ((_core.Approvals.Pending().Count > 0 || ClaudeActions.NeedsConsent(cmd.Label, cmd.Name, true)) && !await BridgeConsent(run, $"„{cmd.Label}“ ausführen")) return McpResult.Fail("Nicht bestätigt: Der Nutzer möchte das nicht.");
                run.Page = spec.Key; ClaudeChangeBegin(spec.Key, doc);
                cmd.Run(doc); await Pump();
                ClaudeChangeAfter(spec.Key, doc);
                return BridgeDone(run);
            }),
            T("werkzeug", "Wählt ein Werkzeug der Werkzeugleiste des Programms.", TrendoMcp.Schema(("name", "string", "Name aus befehle", true), programArg), async a =>
            {
                var spec = BridgeSpec(run, a); var doc = BridgeOpenDoc(spec);
                var tool = spec.Tools.FirstOrDefault(t => t.Name == Need(a, "name") || t.Label.Equals(Need(a, "name"), StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("Dieses Werkzeug gibt es nicht.");
                tool.Select(doc); run.Page = spec.Key; await Pump();
                return BridgeDone(run);
            }),
            S("dokument_lesen", "Liest die Daten des offenen Dokuments als JSON, ganz oder ab einem Pfad wie „levels/0“.", TrendoMcp.Schema(("pfad", "string", "Pfad, leer für alles", false), programArg), a =>
            {
                var pageData = a["programm"] is null && BridgeKey(run) is null && CurrentView == "3d" ? JsonNode.Parse(_scene.ToJson())!.AsObject() : null;
                var node = DocPath.Get(pageData ?? BridgeOpenDoc(BridgeSpec(run, a)).Data, a["pfad"].AsText());
                if (node is null) return McpResult.Fail("An diesem Pfad steht nichts.");
                var json = node.ToJsonString();
                if (json.Length <= MaxToolText) return new McpResult(json);
                var keys = node is JsonObject o ? "Felder: " + string.Join(", ", o.Select(p => p.Key)) : node is JsonArray arr ? $"Liste mit {arr.Count} Einträgen" : "";
                return new McpResult($"Zu groß für eine Antwort ({json.Length} Zeichen). Bitte einen Teilpfad lesen. {keys}");
            }),
            T("dokument_setzen", "Ändert eine Stelle im Dokument und zeigt sie sofort im Programm. Pfad wie „levels/0/name“, Wert als JSON. Mit loeschen=true wird die Stelle entfernt.", TrendoMcp.Schema(("pfad", "string", "Pfad", true), ("wert", "any", "Neuer Wert", false), ("loeschen", "boolean", "Stelle entfernen", false), programArg), async a =>
            {
                if (a["programm"] is null && BridgeKey(run) is null && CurrentView == "3d")
                {
                    var scene = JsonNode.Parse(_scene.ToJson())!.AsObject();
                    if (a["loeschen"].AsBool()) DocPath.Remove(scene, Need(a, "pfad")); else DocPath.Set(scene, Need(a, "pfad"), DocPath.Unwrap(a["wert"]?.DeepClone()));
                    var changed = Scene3D.FromJson(scene.ToJsonString()); // erst prüfen, dann übernehmen
                    Push(); _scene = changed; // T-0691: wie jede Änderung – Wiederholen leeren, als ungespeichert sichern
                    Show("3d"); await Pump();
                    return BridgeDone(run);
                }
                var spec = BridgeSpec(run, a); var doc = BridgeOpenDoc(spec); var path = Need(a, "pfad");
                if (System.Text.RegularExpressions.Regex.IsMatch(path, "consent|einwillig|freigabe|approved", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return McpResult.Fail("Einwilligungen und Freigaben trägt nur der Nutzer selbst ein.");
                var remove = a["loeschen"].AsBool();
                var value = remove ? null : DocPath.Unwrap(a["wert"]?.DeepClone());
                if (!remove) try { AssistChain.CheckEdit(doc.Data, path, value); } catch (ArgumentException e) { return McpResult.Fail(e.Message); } // T-0707: dieselbe Prüfung wie der Assistent
                // T-0780 (Gegenprüfung H3): Felder, die nur die Person setzt (ProgramSpec.KiLocked), gelten auch für Claude – dieselbe Sperr-Prüfung wie im Assistenten
                // (jedes Pfadsegment und jeder Schlüssel im geschriebenen Wert; Session.LockedField aus T-0779)
                if (spec.KiLocked.Count > 0 && ProgramAssistTarget.LockedFieldFor(spec.KiLocked, path, value?.ToJsonString() ?? "") is { } field)
                    return McpResult.Fail($"„{field}“ legt die Person selbst fest – Claude ändert das nicht.");
                if (spec.KiCheck is { } check) // T-0780 (H1/H2): dieselbe Prüfung wie im Assistenten, am Ergebnis vor dem Übernehmen
                {
                    var probe = doc.Data.DeepClone().AsObject();
                    if (remove) DocPath.Remove(probe, path); else DocPath.Set(probe, path, value?.DeepClone());
                    if (check(doc.Data, probe) is { } why) return McpResult.Fail(why);
                }
                run.Page = spec.Key; ClaudeChangeBegin(spec.Key, doc);
                doc.Edit("Claude: " + path, d => { if (remove) DocPath.Remove(d, path); else DocPath.Set(d, path, value); });
                ClaudeChangeAfter(spec.Key, doc);
                if (CurrentView == spec.Key) Show(spec.Key);
                await Pump();
                return BridgeDone(run);
            }),
            S("rueckgaengig", "Nimmt den letzten Schritt im Dokument zurück.", TrendoMcp.Schema(programArg), a =>
            {
                if (a["programm"] is null && BridgeKey(run) is null && CurrentView == "3d")
                {
                    if (_undo3d.Count == 0) return McpResult.Fail("Es gibt nichts zum Zurücknehmen.");
                    UndoSceneSilent(); Show("3d"); return BridgeDone(run);
                }
                var spec = BridgeSpec(run, a); var doc = BridgeOpenDoc(spec);
                if (!doc.Undo()) return McpResult.Fail("Es gibt nichts zum Zurücknehmen.");
                Show(spec.Key); return BridgeDone(run);
            }),
            S("speichern", "Speichert das Dokument im Projektordner.", TrendoMcp.Schema(programArg), a =>
            {
                var spec = BridgeSpec(run, a); var doc = BridgeOpenDoc(spec);
                ProgramSave(spec.Key, doc.Path is null ? BridgeInside(run, BridgeFileName(doc.Title) + ProgramDocument.Extension) : null);
                return new McpResult("Gespeichert: " + doc.Path);
            }),
            S("exportieren", "Exportiert das Dokument in eine Datei im Projektordner.", TrendoMcp.Schema(("format", "string", "Name aus befehle", true), ("datei", "string", "Dateiname im Projektordner", true), programArg), a =>
            {
                var spec = BridgeSpec(run, a); var doc = BridgeOpenDoc(spec);
                var export = spec.Exports.FirstOrDefault(x => x.Name == Need(a, "format") || x.Label.Equals(Need(a, "format"), StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("Dieses Format gibt es nicht.");
                var path = BridgeInside(run, Need(a, "datei"));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var note = export.Write(doc, path);
                BigExported(spec.Key, doc, File.Exists(note) ? note : path); // T-0743: Ergebnis zurück an die Karte eines Großprojekts
                return new McpResult(note is { Length: > 0 } ? note : "Exportiert: " + path);
            }),
            S("bild_ansehen", "Bildschirmfoto der offenen Trendo-Seite.", TrendoMcp.Schema(), _ => BridgeShot()),
            .. StudioIntelTools(run),
            .. HandoffTools(run),
            .. KieTools(run),
        ];
    }

    const int MaxToolText = 90000;

    static string Need(JsonObject a, string key) => a[key].AsText() is { Length: > 0 } s ? s : throw new ArgumentException($"Es fehlt „{key}“.");
    static string BridgeFileName(string title) => string.Concat(title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim() is { Length: > 0 } s ? s : "Dokument";

    static string BridgeInside(BridgeRun run, string relative)
    {
        var root = Path.GetFullPath(run.Folder) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, relative));
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : throw new ArgumentException("Dateien entstehen nur im Projektordner.");
    }

    /// <summary>Seiten, die nur der Nutzer selbst bedient: Hier werden Freigaben erteilt.</summary>
    public static readonly IReadOnlySet<string> ClaudeBlockedPages = new HashSet<string> { "approvals" };
    /// <summary>Seiten mit privaten oder sicherheitsrelevanten Inhalten: Claude braucht je Lauf einmal die Zustimmung.</summary>
    public static readonly IReadOnlySet<string> ClaudeSensitivePages = new HashSet<string> { "schutz", "sicherung", "forensik", "keys", "claude-verbinden", "personen", "posteingang", "post", "kontakte", "finanzen", "appcenter", "hinzufuegen", "components", "prog-pflege", "updates", "teilen", "settings" };
    static readonly HashSet<string> PageTools = ["programm_oeffnen", "seite_lesen", "feld_setzen", "auswaehlen", "klicken", "bild_ansehen"];

    /// <summary>Darf Claude dieses Element bedienen? Rechte-Schalter nie. Bei offener Freigabe oder Wirkung nach außen fragt es den Nutzer, damit Claude keine Freigabe selbst erteilt.</summary>
    async Task<McpResult?> BridgeControlGuard(BridgeRun run, Control c, string what)
    {
        var name = c.Name ?? "";
        if (name.StartsWith("claude-", StringComparison.Ordinal) || name.StartsWith("ki-cloud-use-", StringComparison.Ordinal)) return McpResult.Fail("Rechte von Claude ändert nur der Nutzer selbst.");
        if ((_core.Approvals.Pending().Count > 0 || ClaudeActions.NeedsConsent(BridgeLabel(c), name, BridgeDoc(run) is not null)) && !await BridgeConsent(run, what)) return McpResult.Fail("Nicht bestätigt: Der Nutzer möchte das nicht.");
        return null;
    }

    /// <summary>Darf ein Plan aus [[trendo:…]]-Zeilen ohne Klick laufen? Es gelten dieselben Regeln wie für die Werkzeuge.</summary>
    bool ClaudePlanSafe(string page, ClaudeActions.Page state, IReadOnlyList<ClaudeActions.Step> steps)
    {
        if (_core.Approvals.Halted || _core.Approvals.Pending().Count > 0 || ClaudeBlockedPages.Contains(page) || ClaudeSensitivePages.Contains(page)) return false;
        var undoable = Programs.ContainsKey(page) && ProgramActive(page) is not null;
        return steps.All(s => !ClaudeActions.StepRisky(s, state) && !ClaudeActions.NeedsConsent(ClaudeActions.Describe(s, state), s.Target, undoable)
            && !s.Target.StartsWith("claude-", StringComparison.Ordinal) && !s.Target.StartsWith("ki-cloud-use-", StringComparison.Ordinal));
    }

    /// <summary>Darf Claude diese Seite anfassen? null heißt ja, sonst der Grund.</summary>
    async Task<McpResult?> BridgeGuard(BridgeRun run, string name, JsonObject a)
    {
        if (!PageTools.Contains(name)) return null;
        var page = name == "programm_oeffnen" ? a["programm"].AsText() ?? "" : CurrentView;
        if (ClaudeBlockedPages.Contains(page)) return McpResult.Fail("Diese Seite bedient nur der Nutzer selbst. Dort werden Freigaben erteilt.");
        if (!ClaudeSensitivePages.Contains(page) || run.Allowed.Contains(page)) return null;
        if (!await BridgeConsent(run, $"die Seite „{page}“ mit privaten Inhalten öffnen und lesen")) return McpResult.Fail("Nicht bestätigt: Der Nutzer möchte das nicht.");
        run.Allowed.Add(page);
        return null;
    }

    async Task<McpResult> BridgeUi(BridgeRun run, string name, Func<JsonObject, Task<McpResult>> body, JsonObject a)
    {
        var result = await _ui.InvokeAsync(async () =>
        {
            if (_core.Approvals.Halted) return McpResult.Fail("Nicht bestätigt: Der Not-Stopp ist aktiv. " + _core.Approvals.HaltReason);
            // T-0780 (2./3. Gegenprüfung): jeder Claude-Schritt läuft durch den gemeinsamen Wächter (auch der Plan-Weg im Chat) – Vorab-Ablehnung, Stand merken,
            // Schritt, Felder verlassen, prüfen, bei Verstoß zurückrollen; auch wenn der Schritt mit einem Fehler endet
            try
            {
                if (await BridgeGuard(run, name, a) is { } refused) return refused; // Rückfragen/Zustimmung vor dem Merken – Eingaben der Person währenddessen zählen nicht als Claudes Schritt
                McpResult? res = null;
                var why = await KiStepGuarded(BridgeKey(run), async () => res = await body(a));
                return why is not null ? McpResult.Fail("Zurückgenommen: " + why) : res!;
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException or FormatException or KeyNotFoundException or NullReferenceException or IndexOutOfRangeException)
            { return McpResult.Fail(e.Message); }
        });
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.werkzeug", name + (result.Error ? " · abgelehnt" : ""));
        return result;
    }

    Task Pump() => _ui.InvokeAsync(() => { }, DispatcherPriority.Background).GetTask();

    /// <summary>T-0780 (2. Gegenprüfung H1): Dokumente der Programme mit <see cref="ProgramSpec.KiLocked"/> oder <see cref="ProgramSpec.KiCheck"/> – Stand vor dem Claude-Schritt.</summary>
    List<(ProgramSpec Spec, ProgramDocument Doc, ProgramDocument.Mark Before)> BridgeSnapshot() =>
        [.. _progDocs.Where(p => Programs.TryGetValue(p.Key, out var s) && (s.KiLocked.Count > 0 || s.KiCheck is not null))
            .SelectMany(p => p.Value.Select(d => (Programs[p.Key], d, d.Remember())))];

    /// <summary>Getipptes übernehmen: Felder übernehmen beim Verlassen – ein Feld, das Claude gerade gefüllt hat, verliert den Fokus, damit die Prüfung den echten Stand sieht.</summary>
    async Task BridgeFlushFields()
    {
        if (FocusManager?.GetFocusedElement() is not TextBox tb) return;
        // Fokus auf einen Knopf der Seite (wie ein Klick daneben) – das Feld meldet „verlassen“ und übernimmt den Wert
        var other = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(ActivePageRoot ?? (Control)this).OfType<Button>().FirstOrDefault(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled && b.Focusable);
        if (other is not null) other.Focus(); else Focus();
        await Pump();
        if (FocusManager?.GetFocusedElement() == tb && CurrentView is { Length: > 0 } v) { Show(v); await Pump(); } // notfalls neu zeichnen: Felder übernehmen beim Ablösen
    }

    /// <summary>Werte aller Felder mit gesperrtem Namen (jede Tiefe) – zum Vergleich vorher/nachher.</summary>
    static string LockedValues(JsonNode? n, IReadOnlyList<string> locked)
    {
        var sb = new System.Text.StringBuilder();
        void Walk(JsonNode? x, string path)
        {
            switch (x)
            {
                case JsonObject o: foreach (var (k, v) in o) { if (locked.Contains(k, StringComparer.OrdinalIgnoreCase)) sb.Append(path).Append('/').Append(k).Append('=').Append(v?.ToJsonString()).Append('\n'); else Walk(v, path + "/" + k); } break;
                case JsonArray a: for (var i = 0; i < a.Count; i++) Walk(a[i], path + "/" + i); break;
            }
        }
        Walk(n, "");
        return sb.ToString();
    }

    /// <summary>Nach dem Claude-Schritt: gesperrtes Feld geändert oder KiCheck findet einen Fehler → Dokument auf den Stand davor zurück (samt Rückgängig-Liste) und Fehler an Claude.</summary>
    /// <summary>T-0780 (3. Gegenprüfung): Vorab-Ablehnung eines KI-Schritts, bevor er läuft – klar abgegrenzte Stelle für weitere Regeln
    /// (z. B. T-0782: Programme, die nur lokale KI zulassen). null = Schritt darf laufen, sonst der Grund für die Person/Claude.</summary>
    string? KiStepPreReject(string? page) => null;

    /// <summary>T-0780 (3. Gegenprüfung): gemeinsamer Wächter für jeden KI-Schritt (MCP-Brücke und Plan-Weg im Chat): Vorab-Ablehnung → Stand der Dokumente mit KiLocked/KiCheck merken →
    /// Schritt → Felder verlassen (übernehmen) → prüfen; bei Verstoß Daten und Rückgängig-Liste zurück. Auch wenn der Schritt mit einem Fehler endet, wird geprüft und zurückgerollt
    /// (dann geht der Fehler weiter). Rückgabe null = in Ordnung, sonst der Grund.</summary>
    async Task<string?> KiStepGuarded(string? page, Func<Task> step)
    {
        if (KiStepPreReject(page) is { } no) return no;
        var guarded = BridgeSnapshot();
        try { await step(); }
        catch
        {
            try { await BridgeFlushFields(); } catch (Exception e) when (e is not OutOfMemoryException) { }
            BridgeAfterStep(guarded);
            throw;
        }
        await BridgeFlushFields();
        return BridgeAfterStep(guarded) is { } fail ? fail.Text.Replace("Zurückgenommen: ", "") : null;
    }

    /// <summary>T-0780 (4. Gegenprüfung): die eine Prüfung eines KI-Ergebnisses gegen den Stand davor – für jedes Programm mit KiLocked oder KiCheck und auf jedem Weg
    /// (MCP-Brücke, Plan im Chat, Claudes .tdoc im Projektordner; später auch Brief und Code). null = in Ordnung, sonst der Grund.</summary>
    static string? KiDataProblem(ProgramSpec spec, JsonObject before, JsonObject after)
    {
        if (spec.KiLocked.Count > 0 && LockedValues(before, spec.KiLocked) != LockedValues(after, spec.KiLocked))
            return $"„{string.Join("“, „", spec.KiLocked)}“ legt die Person selbst fest – Claude ändert das nicht.";
        return spec.KiCheck?.Invoke(before, after);
    }

    /// <summary>T-0780 (4. Gegenprüfung): Wächter für Daten, die nicht über einen Schritt in Trendo kommen (Claudes .tdoc): Vorab-Ablehnung, dann <see cref="KiDataProblem"/>.</summary>
    string? KiDataGuard(string page, JsonObject before, JsonObject after) =>
        !Programs.TryGetValue(page, out var spec) || spec.KiLocked.Count == 0 && spec.KiCheck is null && KiStepPreReject(page) is null ? null
        : KiStepPreReject(page) ?? KiDataProblem(spec, before, after);

    McpResult? BridgeAfterStep(List<(ProgramSpec Spec, ProgramDocument Doc, ProgramDocument.Mark Before)> guarded)
    {
        foreach (var (spec, doc, before) in guarded)
        {
            var now = doc.Data.ToJsonString();
            if (now == before.Data) continue;
            var why = KiDataProblem(spec, JsonNode.Parse(before.Data)!.AsObject(), doc.Data);
            if (why is null) continue;
            doc.Rollback(before);
            if (_claudeChanges.TryGetValue(spec.Key, out var c) && c.DocId == doc.Id && c.Before.Data == before.Data) _claudeChanges.Remove(spec.Key);
            _core.Audit.Append(Principal.LocalOwner.Name, "claude.zurueckgerollt", $"{spec.Key} {why}");
            if (CurrentView == spec.Key) Show(spec.Key);
            return McpResult.Fail("Zurückgenommen: " + why);
        }
        return null;
    }

    string? BridgeKey(BridgeRun run) => run.Page is { } p && Programs.ContainsKey(p) ? p : Programs.ContainsKey(CurrentView) ? CurrentView : null;
    ProgramDocument? BridgeDoc(BridgeRun run) => BridgeKey(run) is { } k ? ProgramActive(k) : null;

    ProgramSpec BridgeSpec(BridgeRun run, JsonObject a)
    {
        var key = a["programm"].AsText() ?? BridgeKey(run) ?? throw new ArgumentException("Diese Seite hat kein Dokumentprogramm und keine Befehlsliste. Bediene sie mit „seite_lesen“, „feld_setzen“, „auswaehlen“ und „klicken“.");
        return Programs.TryGetValue(key, out var spec) ? spec : throw new ArgumentException("Dieses Programm gibt es nicht. Liste mit „programme“ abrufen.");
    }

    ProgramDocument BridgeOpenDoc(ProgramSpec spec)
    {
        var doc = ProgramActive(spec.Key) ?? ProgramNew(spec.Key);
        if (CurrentView != spec.Key) Show(spec.Key);
        return doc;
    }

    Control? BridgeFind(string target)
    {
        if (ActivePageRoot is not { } root) return null;
        var all = PageControls(root).Where(c => c.IsVisible && !InsideClaude(c)).ToList();
        return all.FirstOrDefault(c => c.Name == target) ?? all.FirstOrDefault(c => BridgeLabel(c).Equals(target, StringComparison.OrdinalIgnoreCase));
    }

    static string BridgeLabel(Control c) => c switch
    {
        Button b => ButtonText(b),
        MenuItem m => m.Header?.ToString() ?? "",
        TextBox t => t.PlaceholderText ?? Avalonia.Automation.AutomationProperties.GetName(t) ?? "",
        ContentControl cc when cc.Content is string s => s,
        _ => Avalonia.Automation.AutomationProperties.GetName(c) ?? "",
    };

    McpResult BridgePage(BridgeRun run)
    {
        if (ActivePageRoot is not { } root) return McpResult.Fail("Es ist gerade keine Seite offen.");
        var texts = new List<string>(); var items = new JsonArray();
        JsonObject Item(string kind, Control c) => new() { ["art"] = kind, ["name"] = c.Name, ["text"] = BridgeLabel(c), ["aktiv"] = c.IsEffectivelyEnabled };
        foreach (var c in PageControls(root).Where(c => c.IsVisible && !InsideClaude(c)))
            switch (c)
            {
                case TextBox t when t.PasswordChar != default || ClaudeActions.Secret(BridgeLabel(t), t.Name ?? ""): break;
                case TextBox t: { var o = Item("feld", t); o["wert"] = t.Text ?? ""; items.Add(o); break; }
                case Slider s: { var o = Item("regler", s); o["wert"] = s.Value; o["min"] = s.Minimum; o["max"] = s.Maximum; items.Add(o); break; }
                case NumericUpDown n: { var o = Item("zahl", n); o["wert"] = n.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture); items.Add(o); break; }
                case MenuItem m when BridgeLabel(m).Length > 0: items.Add(Item("menue", m)); break;
                case SelectingItemsControl l: { var o = Item("auswahl", l); o["wert"] = l.SelectedItem is { } si ? ItemText(si) : null; o["eintraege"] = new JsonArray(l.Items.Select(ItemText).Where(x => x.Length > 0).Take(200).Select(x => (JsonNode)x).ToArray()); items.Add(o); break; }
                case ToggleButton t: { var o = Item("schalter", t); o["wert"] = t.IsChecked == true; items.Add(o); break; }
                case Button b when !(b.Name ?? "").StartsWith("claude-", StringComparison.Ordinal) && b.Name is not ("btn-help" or "ki-menu") && BridgeLabel(b).Length > 0: items.Add(Item("knopf", b)); break;
                case TextBlock t when t.Text is { Length: >= 2 } s && !texts.Contains(s) && texts.Count < 400: texts.Add(s.Length > 400 ? s[..400] + " …" : s); break;
            }
        var key = BridgeKey(run);
        var result = new JsonObject
        {
            ["seite"] = CurrentView,
            ["dokument"] = BridgeDoc(run)?.Title,
            ["meldung"] = key is not null && _progMsg.TryGetValue(key, out var msg) ? msg : null,
            ["elemente"] = items,
            ["texte"] = new JsonArray(texts.Select(x => (JsonNode)x).ToArray()),
        }.ToJsonString();
        return new McpResult(result.Length <= MaxToolText ? result : result[..MaxToolText] + " … (gekürzt, einzelne Daten mit dokument_lesen holen)");
    }

    McpResult BridgeDone(BridgeRun run)
    {
        var key = BridgeKey(run); var doc = BridgeDoc(run);
        return new McpResult(new JsonObject
        {
            ["ok"] = true, ["seite"] = CurrentView,
            ["meldung"] = key is not null && _progMsg.TryGetValue(key, out var msg) ? msg : null,
            ["status"] = key is not null && doc is not null ? Programs[key].Status?.Invoke(doc) : null,
        }.ToJsonString());
    }

    McpResult BridgeShot()
    {
        if (ActivePageRoot is not { } page) return McpResult.Fail("Es ist gerade keine Seite offen.");
        var (w, h) = ((int)page.Bounds.Width, (int)page.Bounds.Height); // nur die geprüfte Seite, keine anderen offenen Fenster
        if (w <= 0 || h <= 0) return McpResult.Fail("Die Seite ist gerade nicht sichtbar.");
        try
        {
            using var full = new RenderTargetBitmap(new PixelSize(w, h));
            full.Render(page);
            using var ms = new MemoryStream();
            full.Save(ms);
            if (w <= 1400) return new McpResult("Das ist die offene Seite.", ms.ToArray());
            ms.Position = 0;
            using var decoded = new Bitmap(ms); // verkleinern geht nur aus einem geladenen Bild
            using var small = decoded.CreateScaledBitmap(new PixelSize(1400, h * 1400 / w));
            using var scaled = new MemoryStream();
            small.Save(scaled);
            return new McpResult("Das ist die offene Seite.", scaled.ToArray());
        }
        catch (Exception e) when (e is InvalidOperationException or NullReferenceException or NotSupportedException or ArgumentException) { return McpResult.Fail("Bildschirmfoto nicht möglich: " + e.Message); }
    }

    // ───────────── Nachfrage im Chat, nur bei Wirkung nach außen ─────────────
    TaskCompletionSource<bool>? _claudeConsent;

    /// <summary>Über die Freigabe-Prüfung des Systems: gilt einmal, läuft ab, steht im Protokoll und endet beim Not-Stopp.</summary>
    async Task<bool> BridgeConsent(BridgeRun run, string what)
    {
        // B4/T-0707: freigeben darf nur die angemeldete Person mit Freigaberecht – Kinderprofil: sofort Nein, keine Rückfrage
        if (!CanApprove) { _core.Audit.Append(CurrentPrincipal.Name, "freigabe.nicht.erlaubt", "claude.programm " + CurrentView); return false; }
        var target = CurrentView;
        if (!await BridgeAsk(run, what)) { _core.Audit.Append(CurrentPrincipal.Name, "freigabe.nein", "claude.programm " + target); return false; }
        // „Ja“ auf der sichtbaren Zustimmungskarte im Claude-Bereich = Rückfrage-Karte: Anfrage im Kern und Freigabe durch die Person (CurrentPrincipal)
        if (FreigabeAusRueckfrage(new Wirkung("claude.programm", target, what, "Claude möchte: " + what, "Ja")) is not { } id) return false;
        return _core.Approvals.Execute(id, "claude.programm", target, () => true);
    }

    async Task<bool> BridgeAsk(BridgeRun run, string what)
    {
        if (ClaudeConsentOverride is { } o) return await o(what);
        if (run.Dock is not { } dock || !_chatUis.TryGetValue(dock, out var ui)) return false;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); _claudeConsent = tcs;
        var card = ConsentCard(what, tcs);
        ui.Log.Children.Add(card); ui.Scroll.ScrollToEnd();
        var finished = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(15)));
        ui.Log.Children.Remove(card); _claudeConsent = null;
        return finished == tcs.Task && tcs.Task.Result;
    }

    Control ConsentCard(string what, TaskCompletionSource<bool> tcs)
    {
        Button B(string name, string text, bool yes)
        {
            var b = new Button { Name = name, Content = text, FontSize = 12.5, Padding = new Thickness(12, 4), Margin = new Thickness(0, 4, 6, 0), Background = yes ? ClaudeAccent : Brushes.Transparent, Foreground = yes ? Brushes.White : ClaudeFg };
            b.Click += (_, _) => tcs.TrySetResult(yes);
            return b;
        }
        return Appear(CGlass(new StackPanel { Spacing = 6, Children = {
            new TextBlock { Text = $"⚠ Claude möchte {what}. Das wirkt nach außen oder lässt sich nicht zurückholen.", Foreground = ClaudeFg, FontSize = 13.5, TextWrapping = TextWrapping.Wrap },
            new StackPanel { Orientation = Orientation.Horizontal, Children = { B("claude-consent-yes", "Ja, machen", true), B("claude-consent-no", "Nein", false) } } } }, name: "claude-consent"));
    }
}
