using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.Sys;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>STUDIO-FORMS-01 Formulare ausfüllen (T-0672, Freigabe 09.11./15.11.2026): PDF-Formulare ausfüllen wie in Adobe Fill &amp; Sign oder Xodo –
/// Felder direkt auf der Seite, frei gesetzter Text, Datum, Häkchen und Unterschrift (dasselbe Zeichenfeld wie bei Briefen und Trendo PDF),
/// „Meine Angaben“ als Vorschlag, Pflichtfeld-Prüfung und Glätten beim Speichern. Das Original bleibt immer unverändert.
/// Anforderungen: docs/nachbau/formulare/anforderungen.md.</summary>
public partial class MainWindow
{
    public const string FoKey = "formulare-programm";
    const string FoStudio = "STUDIO-FORMS-01";
    /// <summary>Werkzeug auf der Seite: fill (Felder ausfüllen), text, datum, haken, kreuz, unterschrift.</summary>
    public string FoMode { get; set; } = "fill";
    /// <summary>Bildschirm-Pixel je PDF-Punkt in der Seitenansicht.</summary>
    public const double FoScale = 1.25;
    readonly Dictionary<string, (DateTime Stamp, List<FormField> Fields)> _foFields = [];
    readonly Dictionary<string, Bitmap?> _foPages = [];

    static readonly (string Id, string Icon, string Name, string Tip)[] FoTools =
    [
        ("fill", "✎", "Ausfüllen", "Felder direkt auf der Seite ausfüllen"),
        ("text", "T", "Text setzen", "Für Formulare ohne Felder: Text oben eintragen, dann auf die Stelle klicken"),
        ("datum", "📅", "Datum", "Heutiges Datum an die angeklickte Stelle"),
        ("haken", "✓", "Häkchen", "Häkchen an die angeklickte Stelle"),
        ("kreuz", "✗", "Kreuz", "Kreuz an die angeklickte Stelle"),
        ("unterschrift", "✍", "Unterschrift", "Gespeicherte Unterschrift an die angeklickte Stelle – zeichnen rechts unter „Unterschrift“"),
    ];

    static JsonObject FoNew() => new() { ["file"] = "", ["values"] = new JsonObject(), ["overlays"] = new JsonArray(), ["page"] = 0, ["placeText"] = "", ["flatten"] = false };

    private void InitFormulare() => RegisterProgram(FachTabs, new ProgramSpec(FoKey, FoStudio, "Formulare ausfüllen")
    {
        Sentence = "PDF-Formulare ausfüllen und unterschreiben – Felder direkt auf der Seite, deine Angaben als Vorschlag, Pflichtfelder geprüft.",
        Folder = "Formulare",
        ScrollWorkspace = true,
        GroupLevels = new() { ["Formular"] = 0, ["Seite"] = 0, ["Speichern"] = 0 },
        Templates =
        [
            new("oeffnen", "Formular öffnen", "PDF mit oder ohne Felder", FoNew),
            new("beispiel", "Beispiel-Antrag", "Zum Ausprobieren: Felder, Auswahl, Pflichtfelder", () => { var j = FoNew(); j["file"] = FoSample(); return j; }),
        ],
        Ribbon =
        [
            ("Formular", [
                new ProgramCmd("fo-open", "PDF öffnen …", d => _ = FoPick(d), Primary: true, Tip: "Formular (PDF) öffnen – das Original bleibt unverändert."),
                new ProgramCmd("fo-suggest", "Meine Angaben einsetzen", d => FoSuggest(d), Tip: "Leere Felder aus „Meine Angaben“ vorschlagen (Name, Anschrift, Geburtsdatum …)."),
                new ProgramCmd("fo-check", "Pflichtfelder prüfen", d => FoCheck(d)),
                new ProgramCmd("fo-clear", "Alles leeren", d => d.Edit("Alles leeren", x => { x["values"] = new JsonObject(); x["overlays"] = new JsonArray(); }), Level: 1),
            ]),
            ("Seite", [
                new ProgramCmd("fo-prev", "◀ Seite", d => FoGo(d, -1)),
                new ProgramCmd("fo-next", "Seite ▶", d => FoGo(d, 1)),
            ]),
            ("Speichern", [
                new ProgramCmd("fo-save", "Ausgefüllt speichern", d => FoSave(d, false), Primary: true, Tip: "Neue PDF mit deinen Eingaben – die Felder bleiben änderbar."),
                new ProgramCmd("fo-save-flat", "Fertig speichern (geglättet)", d => FoSave(d, true), Tip: "Werte fest in die Seite – danach nicht mehr änderbar. Gut zum Abschicken."),
            ]),
        ],
        Exports =
        [
            new("pdf", "Ausgefüllte PDF", ".pdf", (d, f) => FoExport(d, f, false)),
            new("pdf-flach", "Fertige PDF (geglättet)", ".pdf", (d, f) => FoExport(d, f, true)),
        ],
        Tools = [.. FoTools.Select(t => new ProgramTool("fo-tool-" + t.Id, t.Icon, t.Name, t.Tip, _ => FoMode = t.Id, () => FoMode == t.Id))],
        Workspace = FoWorkspace,
        Properties = FoProperties,
        Status = d =>
        {
            if (d.Text("file").Length == 0) return "Noch kein Formular geöffnet";
            var f = FoFieldsSafe(d); var v = FoValues(d);
            var filled = f.Count(x => FoShown(x, v).Length > 0 && !(x.Kind == "Ankreuzfeld" && FoShown(x, v) == "nein"));
            var miss = PdfFormular.MissingRequired(f, v).Count;
            return $"{f.Count} Felder · {filled} ausgefüllt" + (miss > 0 ? $" · {miss} Pflichtfeld(er) offen" : " · Pflichtfelder vollständig") + $" · {d.List("overlays").Count} gesetzt";
        },
        Help = "Formular öffnen und direkt auf der Seite ausfüllen. „Meine Angaben einsetzen“ schlägt Name, Anschrift und mehr vor – du siehst alles, bevor du speicherst. "
             + "Formulare ohne Felder: links „Text setzen“, „Datum“, „Häkchen“ oder „Unterschrift“ wählen und auf die Stelle klicken. Rot umrandet sind leere Pflichtfelder. "
             + "„Fertig speichern“ glättet: die Werte stehen fest auf der Seite. Abschicken machst du selbst.",
    }, "formulare", "formular ausfüllen", "antrag", "fill and sign", "ausfüllen", "unterschreiben", "pflichtfelder", "meine angaben", "pdf formular", "acroform");

    // ---------- Modell ----------

    public FormProfile FoProfile() => FormProfile.Load(_core.Secrets);
    public void FoSaveProfile(FormProfile p) { p.Save(_core.Secrets); _core.Audit.Append(Principal.LocalOwner.Name, "forms.profile", $"{p.Values.Count} Angaben"); } // nie die Werte

    static Dictionary<string, string> FoValues(ProgramDocument d) =>
        (d.Data["values"] as JsonObject)?.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value.AsText() ?? "", StringComparer.Ordinal) ?? [];

    /// <summary>Felder der geöffneten PDF (zwischengespeichert, bis sich die Datei ändert).</summary>
    public List<FormField> FoFields(ProgramDocument d)
    {
        var f = d.Text("file"); if (f.Length == 0 || !File.Exists(f)) return [];
        var stamp = File.GetLastWriteTimeUtc(f);
        if (_foFields.TryGetValue(f, out var c) && c.Stamp == stamp) return c.Fields;
        var list = PdfFormular.Fields(f); _foFields[f] = (stamp, list); return list;
    }
    List<FormField> FoFieldsSafe(ProgramDocument d) { try { return FoFields(d); } catch (Exception) { return []; } }

    /// <summary>Wert, der angezeigt wird: Eingabe im Dokument, sonst der Wert in der Datei.</summary>
    static string FoShown(FormField f, IReadOnlyDictionary<string, string> v) => v.TryGetValue(f.Name, out var x) ? x : f.Value;

    public void FoOpen(ProgramDocument d, string path)
    {
        if (!File.Exists(path)) throw new ArgumentException("Datei nicht gefunden: " + Path.GetFileName(path));
        if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Bitte ein Formular als PDF wählen.");
        if (PdfTools.Read(path).NeedsPassword) throw new InvalidOperationException("Dieses Formular ist mit Passwort geschützt – erst in der PDF-Werkstatt mit „Passwort“ entsperren.");
        var full = Path.GetFullPath(path);
        d.Edit("Formular öffnen", x => { x["file"] = full; x["values"] = new JsonObject(); x["overlays"] = new JsonArray(); x["page"] = 0; });
        if (d.Title is "Unbenannt" or "Formular öffnen") d.Title = Path.GetFileNameWithoutExtension(path);
        var fields = FoFields(d);
        var req = fields.Count(f => f.Required);
        _progMsg[FoKey] = fields.Count == 0 ? "Dieses Formular hat keine Felder – links „Text setzen“, „Datum“, „Häkchen“ oder „Unterschrift“ wählen und auf die Stelle klicken."
            : $"{fields.Count} Felder gefunden" + (req > 0 ? $", {req} davon Pflicht" : "") + ". Tipp: „Meine Angaben einsetzen“ füllt Name, Anschrift und mehr vor.";
        FoMode = fields.Count == 0 ? "text" : "fill";
        _core.Audit.Append(Principal.LocalOwner.Name, "forms.open", $"{Path.GetFileName(path)} ({fields.Count} Felder)");
    }

    async Task FoPick(ProgramDocument d)
    {
        var req = new PickRequest("fo-open", false, false, ["pdf"]);
        string[]? got;
        if (PickOverride is { } fake) got = await fake(req);
        else got = (await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Formular öffnen", FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("PDF") { Patterns = ["*.pdf"] }] }))
            .Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
        if (got is [var f, ..]) ProgramRun(FoKey, () => FoOpen(d, f));
    }

    public void FoSetValue(ProgramDocument d, string name, string value)
    {
        var f = FoFields(d).FirstOrDefault(x => x.Name == name) ?? throw new ArgumentException($"Das Feld „{name}“ gibt es nicht.");
        if (f.ReadOnly) throw new InvalidOperationException($"Das Feld „{name}“ ist schreibgeschützt.");
        if (f.Kind is "Auswahl" or "Liste" or "Optionsfeld" && value.Length > 0 && !f.Options.Contains(value)) throw new ArgumentException($"„{value}“ gibt es im Feld „{name}“ nicht.");
        if (FoValues(d).TryGetValue(name, out var old) && old == value) return;
        d.Edit("Feld " + name, x => { if (x["values"] is not JsonObject o) x["values"] = o = []; o[name] = value; });
    }

    /// <summary>Leere Felder aus „Meine Angaben“ vorschlagen. Gibt die Zahl der Vorschläge zurück.</summary>
    public int FoSuggest(ProgramDocument d)
    {
        if (d.Text("file").Length == 0) throw new InvalidOperationException("Bitte zuerst ein Formular öffnen.");
        var p = FoProfile();
        if (p.Values.Count == 0) { _progMsg[FoKey] = "„Meine Angaben“ sind noch leer – rechts unter „Meine Angaben“ eintragen und speichern (bleibt nur auf diesem PC)."; return 0; }
        var s = p.Suggest(FoFields(d), FoValues(d));
        if (s.Count > 0) d.Edit("Meine Angaben einsetzen", x => { if (x["values"] is not JsonObject o) x["values"] = o = []; foreach (var (k, v) in s) o[k] = v; });
        _progMsg[FoKey] = s.Count == 0 ? "Keine leeren Felder passen zu „Meinen Angaben“." : $"{s.Count} Felder aus „Meinen Angaben“ eingesetzt – bitte kurz prüfen.";
        _core.Audit.Append(Principal.LocalOwner.Name, "forms.suggest", $"{s.Count} Felder"); // nie die Werte
        return s.Count;
    }

    public List<string> FoMissing(ProgramDocument d) => PdfFormular.MissingRequired(FoFields(d), FoValues(d));

    void FoCheck(ProgramDocument d)
    {
        var m = FoMissing(d);
        _progMsg[FoKey] = m.Count == 0 ? "✓ Alle Pflichtfelder sind ausgefüllt." : $"Noch leer ({m.Count}): {string.Join(", ", m)} – rot umrandet auf der Seite.";
    }

    void FoGo(ProgramDocument d, int by)
    {
        var f = d.Text("file"); if (f.Length == 0) return;
        var n = PdfView.PageCount(f); var p = Math.Clamp((int)d.Number("page") + by, 0, Math.Max(0, n - 1));
        if (p != (int)d.Number("page")) d.Set("page", p, "Seite");
    }

    /// <summary>Etwas an eine Stelle setzen (Punkte von links/oben). Text und Unterschrift brauchen vorher Text bzw. eine gespeicherte Unterschrift.</summary>
    public void FoPlace(ProgramDocument d, string kind, int page, double x, double y)
    {
        if (d.Text("file").Length == 0) throw new InvalidOperationException("Bitte zuerst ein Formular öffnen.");
        JsonObject o;
        switch (kind)
        {
            case "text":
                var t = d.Text("placeText").Trim();
                if (t.Length == 0) throw new InvalidOperationException("Erst oben bei „Text zum Setzen“ etwas eintragen, dann auf die Stelle klicken.");
                o = new JsonObject { ["kind"] = "text", ["text"] = t, ["size"] = 11 }; y -= 8; break;
            case "datum": o = new JsonObject { ["kind"] = "datum", ["text"] = DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), ["size"] = 11 }; y -= 8; break;
            case "haken" or "kreuz": o = new JsonObject { ["kind"] = kind, ["size"] = 11 }; x -= 5.5; y -= 5.5; break;
            case "unterschrift":
                var f = _settings.LetterSignature;
                if (!File.Exists(f)) throw new InvalidOperationException("Bitte zuerst rechts unter „Unterschrift“ unterschreiben.");
                var (iw, ih) = XrPlus.ImageSize(File.ReadAllBytes(f)); var h = iw > 0 ? PdfSignWidth * ih / iw : 0;
                o = new JsonObject { ["kind"] = "bild", ["image"] = f, ["w"] = Math.Round(PdfSignWidth, 1) }; x -= PdfSignWidth / 2; y -= h / 2; break;
            default: throw new ArgumentException("Unbekannt: " + kind);
        }
        o["page"] = page; o["x"] = Math.Round(Math.Max(0, x), 1); o["y"] = Math.Round(Math.Max(0, y), 1);
        d.Edit(kind == "unterschrift" ? "Unterschrift setzen" : "Setzen", j => { if (j["overlays"] is not JsonArray a) j["overlays"] = a = []; a.Add(o); });
        _progMsg[FoKey] = kind == "unterschrift" ? "Unterschrift gesetzt – sie wird beim Speichern fest auf die Seite gezeichnet (keine qualifizierte elektronische Signatur)." : "Gesetzt. Mit ✕ in der Liste rechts wieder entfernen.";
    }

    static List<FormOverlay> FoOverlays(ProgramDocument d) => [.. d.List("overlays").OfType<JsonObject>().Select(o => new FormOverlay(
        o["kind"].AsText() ?? "text", (int)ProgramDocument.ToNumber(o["page"]), ProgramDocument.ToNumber(o["x"]), ProgramDocument.ToNumber(o["y"]),
        o["text"].AsText() ?? "", ProgramDocument.ToNumber(o["size"], 11), o["image"].AsText(), ProgramDocument.ToNumber(o["w"], 150)))];

    /// <summary>Ausgefüllte PDF schreiben: Platzhalter aus „Meine Angaben“ werden erst hier eingesetzt. Meldet leere Pflichtfelder.</summary>
    public string FoExport(ProgramDocument d, string target, bool flatten)
    {
        var f = d.Text("file"); if (f.Length == 0 || !File.Exists(f)) throw new InvalidOperationException("Bitte zuerst ein Formular öffnen.");
        var values = FoProfile().Resolve(FoValues(d));
        var r = PdfFormular.Fill(f, target, values, FoOverlays(d), flatten, _core.Audit);
        _progMsg[FoKey] = (flatten ? "Fertig gespeichert (geglättet, nicht mehr änderbar): " : "Gespeichert: ") + Path.GetFileName(target)
            + (r.MissingRequired.Count > 0 ? $" – Achtung, noch leere Pflichtfelder: {string.Join(", ", r.MissingRequired)}." : " – alle Pflichtfelder ausgefüllt.");
        NotifyFileResult(_progMsg[FoKey]);
        return target;
    }

    public string FoSave(ProgramDocument d, bool flatten)
    {
        var f = d.Text("file"); if (f.Length == 0) throw new InvalidOperationException("Bitte zuerst ein Formular öffnen.");
        var target = PdfTools.Free(Path.Combine(WorkFolder("Formulare"), DevStudio.SafeName(Path.GetFileNameWithoutExtension(f)) + (flatten ? "-fertig.pdf" : "-ausgefuellt.pdf")));
        FoExport(d, target, flatten);
        d.Edit("Gespeichert", x => x["lastSaved"] = target);
        return target;
    }

    /// <summary>Beispiel-Antrag zum Ausprobieren: Beschriftungen, Textfelder (zwei Pflicht), Ankreuzfeld, Auswahl, Optionsfeld.</summary>
    string FoSample()
    {
        var dir = WorkFolder("Formulare"); var target = Path.Combine(dir, "Beispiel-Antrag.pdf");
        if (File.Exists(target)) return target;
        var t1 = Path.Combine(Path.GetTempPath(), $"trendo-antrag-{Guid.NewGuid():N}.pdf"); var t2 = t1 + ".2.pdf";
        try
        {
            PdfTools.NewBlank(t1);
            FormOverlay L(double y, string s, double size = 10) => new("text", 0, 60, y, s, size);
            PdfFormular.Fill(t1, t2, new Dictionary<string, string>(),
            [
                L(60, "Antrag auf Musterleistung", 18), L(84, "Bitte vollständig ausfüllen. Felder mit * sind Pflicht.", 9),
                L(118, "Vorname *"), L(118 + 0, "") with { X = 290, Text = "Nachname *" }, L(168, "Straße und Hausnummer"), L(218, "PLZ und Ort"),
                L(268, "Geburtsdatum"), L(318, "IBAN (für die Auszahlung)"), L(368, "Familienstand"), L(418, "Zahlweise:      monatlich           vierteljährlich           jährlich"),
                L(462, "Ich bestätige, dass meine Angaben stimmen. *"), L(540, "Ort, Datum"), L(540, "") with { X = 290, Text = "Unterschrift" },
            ]);
            PdfFormular.AddFields(t2, target,
            [
                new("Vorname", "text", 0, 60, 132, 210, 20, Required: true), new("Nachname", "text", 0, 290, 132, 230, 20, Required: true),
                new("Strasse", "text", 0, 60, 182, 460, 20, Label: "Straße und Hausnummer"), new("PLZ_Ort", "text", 0, 60, 232, 460, 20, Label: "PLZ und Ort"),
                new("Geburtsdatum", "text", 0, 60, 282, 140, 20), new("IBAN", "text", 0, 60, 332, 300, 20),
                new("Familienstand", "auswahl", 0, 60, 382, 180, 20, Options: ["ledig", "verheiratet", "eingetragene Lebenspartnerschaft", "geschieden", "verwitwet"]),
                new("Zahlweise", "optionen", 0, 108, 430, 12, 12, Options: ["Monat"]), new("Zahlweise", "optionen", 0, 196, 430, 12, 12, Options: ["Quartal"]), new("Zahlweise", "optionen", 0, 300, 430, 12, 12, Options: ["Jahr"]),
                new("Bestaetigung", "haken", 0, 60, 474, 12, 12, Required: true, Label: "Angaben bestätigt"),
                new("Ort_Datum", "text", 0, 60, 552, 210, 20, Label: "Ort, Datum"),
            ]);
        }
        finally { foreach (var t in new[] { t1, t2 }) try { File.Delete(t); } catch (IOException) { } }
        return target;
    }

    // ---------- Oberfläche ----------

    Bitmap? FoPageBitmap(string file, int page)
    {
        var key = $"{file}|{page}|{File.GetLastWriteTimeUtc(file).Ticks}";
        if (_foPages.TryGetValue(key, out var b)) return b;
        try { b = PdfView.Render(file, page, (int)(72 * FoScale * 1.6)); } catch (Exception) { b = null; }
        return _foPages[key] = b;
    }

    /// <summary>Eingabe für ein Feld – auf der Seite und in der Liste (gleicher Wert). Getipptes wird nach einer Sekunde Pause übernommen.</summary>
    Control FoInput(ProgramDocument d, FormField f, string name, IReadOnlyDictionary<string, string> values, FormProfile p, double width = double.NaN, double height = double.NaN)
    {
        var v = FoShown(f, values);
        if (v.StartsWith(FormProfile.Ref, StringComparison.Ordinal))
        {
            var clear = new Button { Name = name + "-clear", Content = "✕", Padding = new Thickness(4, 0), FontSize = 10 };
            ToolTip.SetTip(clear, "Platzhalter entfernen");
            clear.Click += (_, _) => ProgramRun(FoKey, () => FoSetValue(d, f.Name, ""));
            return new Border { Name = name, Width = width, Height = height, Background = Brush.Parse("#2216A34A"), BorderBrush = Brush.Parse("#4ADE80"), BorderThickness = new Thickness(1),
                Child = new DockPanel { Children = { Dock(clear, Avalonia.Controls.Dock.Right), new TextBlock { Text = p.Display(v), FontSize = 11, Foreground = Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0), TextTrimming = TextTrimming.CharacterEllipsis } } } };
        }
        switch (f.Kind)
        {
            case "Ankreuzfeld":
                var cb = new CheckBox { Name = name, IsChecked = v == "ja", IsEnabled = !f.ReadOnly, MinHeight = 0, Padding = new Thickness(0) };
                cb.IsCheckedChanged += (_, _) => { var nv = cb.IsChecked == true ? "ja" : "nein"; if (nv != FoShown(f, FoValues(d))) ProgramRun(FoKey, () => FoSetValue(d, f.Name, nv)); };
                return cb;
            case "Auswahl" or "Liste" or "Optionsfeld":
                var items = new List<string> { "–" }; items.AddRange(f.Options);
                var combo = new ComboBox { Name = name, ItemsSource = items, SelectedIndex = Math.Max(0, items.IndexOf(v)), IsEnabled = !f.ReadOnly, FontSize = 12, MinWidth = 120, Width = width, MinHeight = 0 };
                combo.SelectionChanged += (_, _) => { var nv = combo.SelectedIndex <= 0 ? "" : items[combo.SelectedIndex]; if (nv != FoShown(f, FoValues(d))) ProgramRun(FoKey, () => FoSetValue(d, f.Name, nv)); };
                return combo;
            default:
                var tb = new TextBox { Name = name, Text = v, IsReadOnly = f.ReadOnly, FontSize = 12, Width = width, Height = height, MinHeight = 0, Padding = new Thickness(3, 1), AcceptsReturn = f.Multiline,
                    MaxLength = f.MaxLength > 0 ? f.MaxLength : 0, Watermark = f.Label.Length > 0 ? f.Label : null, VerticalContentAlignment = VerticalAlignment.Center };
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; var shown = v;
                void Commit() { timer.Stop(); var now = tb.Text ?? ""; if (now != shown && now != FoShown(f, FoValues(d))) { shown = now; ProgramRun(FoKey, () => FoSetValue(d, f.Name, now)); } }
                timer.Tick += (_, _) => Commit();
                tb.TextChanged += (_, _) => { timer.Stop(); if ((tb.Text ?? "") != shown) timer.Start(); };
                tb.LostFocus += (_, _) => Commit();
                return tb;
        }
    }

    static Control Dock(Control c, Avalonia.Controls.Dock side) { DockPanel.SetDock(c, side); return c; }

    Control FoWorkspace(ProgramDocument d)
    {
        var file = d.Text("file");
        if (file.Length == 0 || !File.Exists(file))
        {
            var open = new Button { Name = "fo-open-now", Content = "Formular öffnen …", Padding = new Thickness(18, 8) }; open.Classes.Add("accent");
            open.Click += (_, _) => _ = FoPick(d);
            return KrCard(new StackPanel { Spacing = 10, Children = { H("Formular ausfüllen", 16),
                P("Öffne ein PDF-Formular – mit Feldern (Anträge, Verträge) oder ohne (eingescannte Formulare). Du füllst direkt auf der Seite aus; das Original bleibt unverändert.", Muted, 12), open } }, "fo-empty");
        }
        var fields = FoFieldsSafe(d); var values = FoValues(d); var profile = FoProfile(); var missing = PdfFormular.MissingRequired(fields, values).ToHashSet();
        int count; try { count = PdfView.PageCount(file); } catch (Exception) { count = 1; }
        var page = Math.Clamp((int)d.Number("page"), 0, Math.Max(0, count - 1));
        var (pw, ph) = PdfView.Sizes(file)[page];

        // Seite mit Feldern
        var canvas = new Canvas { Name = "fo-page", Width = pw * FoScale, Height = ph * FoScale, Background = Brushes.White, ClipToBounds = true, Cursor = FoMode == "fill" ? Cursor.Default : new Cursor(StandardCursorType.Cross) };
        if (FoPageBitmap(file, page) is { } bmp) canvas.Children.Add(new Image { Source = bmp, Width = pw * FoScale, Height = ph * FoScale, IsHitTestVisible = false });
        for (var i = 0; i < fields.Count; i++)
        {
            var f = fields[i]; if (f.Page != page || f.W <= 0) continue;
            var empty = missing.Contains(f.Name); var has = FoShown(f, values) is { Length: > 0 } sv && !(f.Kind == "Ankreuzfeld" && sv == "nein");
            Control input = f.Kind == "Optionsfeld" || f.Kind is "Unterschrift" or "Knopf"
                ? new Border { Name = $"fo-onpage-{i}", Background = Brush.Parse("#332563EB") }
                : FoInput(d, f, $"fo-onpage-{i}", values, profile, Math.Max(f.Kind == "Ankreuzfeld" ? 14 : 30, f.W * FoScale), f.Kind == "Ankreuzfeld" ? double.NaN : Math.Max(16, f.H * FoScale));
            var frame = new Border { Width = Math.Max(14, f.W * FoScale) + 2, Height = Math.Max(14, f.H * FoScale) + 2, BorderThickness = new Thickness(empty ? 2 : 1),
                BorderBrush = Brush.Parse(empty ? "#F97316" : has ? "#22C55E" : "#602563EB"), Background = Brush.Parse(has ? "#00000000" : "#182563EB"), Child = input, IsHitTestVisible = FoMode == "fill" };
            ToolTip.SetTip(frame, (f.Label.Length > 0 ? f.Label : f.Name) + (f.Required ? " (Pflicht)" : "") + (f.Kind == "Optionsfeld" ? " – Auswahl in der Liste rechts" : ""));
            Canvas.SetLeft(frame, f.X * FoScale - 1); Canvas.SetTop(frame, f.YTop * FoScale - 1); canvas.Children.Add(frame);
        }
        var oi = 0;
        foreach (var o in FoOverlays(d))
        {
            var k = oi++; if (o.Page != page) continue;
            Control c = o.Kind switch
            {
                "haken" => new TextBlock { Text = "✓", FontSize = o.Size * FoScale * 1.1, Foreground = Brush.Parse("#0C1E50"), FontWeight = FontWeight.Bold },
                "kreuz" => new TextBlock { Text = "✗", FontSize = o.Size * FoScale * 1.1, Foreground = Brush.Parse("#0C1E50"), FontWeight = FontWeight.Bold },
                "bild" => new Image { Width = o.W * FoScale, Source = File.Exists(o.Image) ? new Bitmap(o.Image) : null },
                _ => new TextBlock { Text = o.Text, FontSize = o.Size * FoScale, Foreground = Brush.Parse("#0C1E50") },
            };
            c.Name = $"fo-overlay-{k}"; c.IsHitTestVisible = false;
            Canvas.SetLeft(c, o.X * FoScale); Canvas.SetTop(c, o.YTop * FoScale - (o.Kind is "haken" or "kreuz" ? o.Size * FoScale * 0.25 : 0)); canvas.Children.Add(c);
        }
        canvas.PointerPressed += (_, e) =>
        {
            if (FoMode == "fill") return;
            var p = e.GetPosition(canvas); var mode = FoMode;
            ProgramRun(FoKey, () => FoPlace(d, mode, page, p.X / FoScale, p.Y / FoScale));
            e.Handled = true;
        };

        // Leiste über der Seite
        var bar = new WrapPanel { Name = "fo-bar", Margin = new Thickness(0, 0, 0, 6) };
        var prev = new Button { Name = "fo-page-prev", Content = "◀", IsEnabled = page > 0, Padding = new Thickness(8, 2) }; prev.Click += (_, _) => ProgramRun(FoKey, () => FoGo(d, -1));
        var next = new Button { Name = "fo-page-next", Content = "▶", IsEnabled = page < count - 1, Padding = new Thickness(8, 2) }; next.Click += (_, _) => ProgramRun(FoKey, () => FoGo(d, 1));
        bar.Children.Add(prev); bar.Children.Add(new TextBlock { Text = $"  Seite {page + 1} von {count}  ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }); bar.Children.Add(next);
        var tool = FoTools.First(t => t.Id == FoMode);
        bar.Children.Add(new TextBlock { Text = $"   {tool.Icon} {tool.Name}: ", Foreground = GoldText(), FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        bar.Children.Add(new TextBlock { Text = FoMode == "fill" ? "in die Felder tippen" : FoMode == "text" ? "" : "auf die Stelle klicken", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center });
        if (FoMode == "text") { var pt = KrBox(d, "placeText", "fo-place-text", "Text zum Setzen, dann auf die Stelle klicken"); pt.Width = 280; bar.Children.Add(pt); }

        // Seite waagrecht scrollbar, damit die Feldliste rechts immer sichtbar bleibt
        var pageView = new ScrollViewer { Name = "fo-page-scroll", HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new Border { BoxShadow = BoxShadows.Parse("0 4 14 0 #66000000"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12), Child = canvas } };
        var left = new StackPanel { Children = { bar, pageView } };

        // Liste rechts: Felder, Pflicht, Gesetztes
        var list = new StackPanel { Name = "fo-fields", Spacing = 5 };
        list.Children.Add(H($"Felder ({fields.Count})", 14));
        var sug = new Button { Name = "fo-suggest-now", Content = "Meine Angaben einsetzen", Padding = new Thickness(10, 4), IsEnabled = fields.Count > 0 }; sug.Classes.Add("accent");
        sug.Click += (_, _) => ProgramRun(FoKey, () => FoSuggest(d));
        list.Children.Add(sug);
        if (missing.Count > 0) list.Children.Add(new TextBlock { Name = "fo-missing", Text = $"Noch leer (Pflicht): {string.Join(", ", missing)}", Foreground = Brush.Parse("#FDBA74"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        else if (fields.Any(f => f.Required)) list.Children.Add(new TextBlock { Name = "fo-missing", Text = "✓ Alle Pflichtfelder ausgefüllt", Foreground = Brush.Parse("#4ADE80"), FontSize = 12 });
        if (fields.Count == 0) list.Children.Add(P("Keine Formularfelder. Links „Text setzen“, „Datum“, „Häkchen“ oder „Unterschrift“ wählen und auf die Stelle klicken.", Muted, 12));
        for (var i = 0; i < fields.Count; i++)
        {
            var f = fields[i];
            list.Children.Add(new StackPanel { Spacing = 2, Children = {
                new TextBlock { Text = (f.Label.Length > 0 ? f.Label : f.Name) + (f.Required ? " *" : "") + (f.Page != page ? $"  (S. {f.Page + 1})" : ""), Foreground = missing.Contains(f.Name) ? Brush.Parse("#FDBA74") : Text, FontSize = 12 },
                FoInput(d, f, $"fo-field-{i}", values, profile) } });
        }
        var ovs = FoOverlays(d);
        if (ovs.Count > 0)
        {
            list.Children.Add(H($"Gesetzt ({ovs.Count})", 13));
            for (var i = 0; i < ovs.Count; i++)
            {
                var k = i; var o = ovs[i];
                var del = new Button { Name = $"fo-overlay-del-{i}", Content = "✕", Padding = new Thickness(6, 0), FontSize = 11 };
                del.Click += (_, _) => ProgramRun(FoKey, () => d.Edit("Entfernen", x => { if (x["overlays"] is JsonArray a && k < a.Count) a.RemoveAt(k); }));
                var label = o.Kind switch { "bild" => "Unterschrift", "haken" => "Häkchen", "kreuz" => "Kreuz", "datum" => "Datum " + o.Text, _ => "„" + o.Text + "“" };
                list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = $"S. {o.Page + 1}: {label}", Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis }, Pos(del, 1) } });
            }
        }
        var save = new Button { Name = "fo-save-now", Content = "Ausgefüllt speichern", Padding = new Thickness(10, 4) };
        save.Click += (_, _) => ProgramRun(FoKey, () => FoSave(d, false));
        var saveFlat = new Button { Name = "fo-save-flat-now", Content = "Fertig (geglättet)", Padding = new Thickness(10, 4) };
        saveFlat.Click += (_, _) => ProgramRun(FoKey, () => FoSave(d, true));
        list.Children.Add(new WrapPanel { Margin = new Thickness(0, 8, 0, 0), Children = { save, Sp(saveFlat) } });
        if (d.Text("lastSaved") is { Length: > 0 } last && File.Exists(last))
        {
            var openLast = new Button { Name = "fo-open-last", Content = "Gespeicherte Datei in Trendo PDF öffnen", Padding = new Thickness(10, 4), FontSize = 11 };
            openLast.Click += (_, _) => { OpenPdf(last); Show("pdf"); };
            list.Children.Add(openLast);
        }
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,330"), Margin = new Thickness(4, 0, 10, 4), Children = { left, Pos(KrCard(list, "fo-fields-card"), 2) } };
    }

    Control FoProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "fo-props", Spacing = 6 };
        // Unterschrift – dasselbe Zeichenfeld wie bei Briefen und Trendo PDF
        sp.Children.Add(H("Unterschrift", 13));
        if (File.Exists(_settings.LetterSignature))
        {
            var img = new Image { Name = "fo-sign-preview", Width = 180, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
            try { img.Source = new Bitmap(_settings.LetterSignature); } catch (Exception) { img.IsVisible = false; }
            sp.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(6), Padding = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Left, Child = img });
            var use = new Button { Name = "fo-sign-use", Content = "Auf die Seite setzen" }; use.Click += (_, _) => { FoMode = "unterschrift"; _progMsg[FoKey] = "Klick auf die Stelle, an der die Unterschrift stehen soll."; Show(FoKey); };
            sp.Children.Add(use);
        }
        sp.Children.Add(SignaturePad("fo-sig", FoKey, _ => { FoMode = "unterschrift"; _progMsg[FoKey] = "✓ Unterschrift gespeichert – jetzt auf die Stelle der Seite klicken."; Show(FoKey); },
            m => { _progMsg[FoKey] = m; Show(FoKey); }, 230, 90));
        sp.Children.Add(P("Die Unterschrift wird als Bild fest auf die Seite gezeichnet – keine qualifizierte elektronische Signatur.", Muted, 11));

        // Meine Angaben
        sp.Children.Add(H("Meine Angaben", 13));
        sp.Children.Add(P("Bleiben nur auf diesem PC (im geschützten Speicher von Trendo), nie im Dokument oder Protokoll. IBAN und Steuer-ID werden nur maskiert gezeigt und erst beim Speichern der PDF eingesetzt.", Muted, 11));
        var p = FoProfile(); var boxes = new Dictionary<string, TextBox>();
        foreach (var (k, n) in FormProfile.Keys)
        {
            var secret = FormProfile.Sensitive.Contains(k);
            var tb = new TextBox { Name = "fo-me-" + k, FontSize = 12, Text = secret ? "" : p[k], PlaceholderText = secret && p[k].Length > 0 ? FormProfile.Mask(p[k]) + " (zum Ändern neu eingeben)" : n, Width = 230, HorizontalAlignment = HorizontalAlignment.Left };
            boxes[k] = tb;
            sp.Children.Add(new StackPanel { Spacing = 1, Children = { P(n, null, 11), tb } });
        }
        var saveMe = new Button { Name = "fo-me-save", Content = "Meine Angaben speichern" }; saveMe.Classes.Add("accent");
        saveMe.Click += (_, _) => ProgramRun(FoKey, () =>
        {
            var q = FoProfile();
            foreach (var (k, tb) in boxes)
            {
                var v = (tb.Text ?? "").Trim();
                if (FormProfile.Sensitive.Contains(k))
                {
                    if (v.Length == 0) continue;
                    if (k == "iban") { v = new string(v.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant(); if (!FormProfile.IbanValid(v)) throw new ArgumentException("Die IBAN stimmt nicht (Prüfziffer). Bitte noch einmal prüfen."); }
                }
                q[k] = v;
            }
            FoSaveProfile(q);
            _progMsg[FoKey] = $"Meine Angaben gespeichert ({q.Values.Count} Einträge) – nur auf diesem PC.";
        });
        var wipe = new Button { Name = "fo-me-delete", Content = "Alle löschen" };
        wipe.Click += (_, _) => ProgramRun(FoKey, () => { _core.Secrets.Delete(FormProfile.SecretKey); _progMsg[FoKey] = "Meine Angaben gelöscht."; });
        sp.Children.Add(new WrapPanel { Children = { saveMe, Sp(wipe) } });
        return sp;
    }
}
