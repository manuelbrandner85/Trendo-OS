using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.AcroForms;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using Trendo.Core.Kreativ;

namespace Trendo.Core.Sys;

/// <summary>Formularfeld mit allem, was das Ausfüllen braucht: Art (Text, Ankreuzfeld, Auswahl, Liste, Optionsfeld, Unterschrift, Knopf), Wert,
/// Pflicht, schreibgeschützt, Auswahlmöglichkeiten, Lage (Seite 0-basiert, Punkte von links/oben), Hinweistext (/TU), Höchstlänge, mehrzeilig.</summary>
public sealed record FormField(string Name, string Kind, string Value, bool Required, bool ReadOnly, IReadOnlyList<string> Options,
    int Page, double X, double YTop, double W, double H, string Label = "", int MaxLength = 0, bool Multiline = false);

/// <summary>Neues Formularfeld (für „Formular vorbereiten“ und Tests): Art „text“, „haken“, „auswahl“ oder „optionen“.</summary>
public sealed record FormFieldSpec(string Name, string Kind, int Page, double X, double YTop, double W, double H, bool Required = false, IReadOnlyList<string>? Options = null, string Label = "");

/// <summary>Etwas frei auf die Seite setzen – für Formulare ohne Felder: „text“, „datum“, „haken“, „kreuz“ oder „bild“ (Unterschrift).
/// Lage in Punkten von links/oben; <paramref name="Size"/> = Schriftgröße bzw. Kantenlänge, <paramref name="W"/> = Bildbreite.</summary>
public sealed record FormOverlay(string Kind, int Page, double X, double YTop, string Text = "", double Size = 11, string? Image = null, double W = 150);

public sealed record FormFillResult(int Filled, IReadOnlyList<string> Unknown, IReadOnlyList<string> MissingRequired, int Overlays, bool Flattened);

/// <summary>STUDIO-FORMS-01 Formulare ausfüllen (T-0672): AcroForm-Felder lesen und füllen (Text, Ankreuzfeld, Auswahl, Liste, Optionsfeld),
/// frei setzen für Formulare ohne Felder, Unterschrift als Bild, Datum und Häkchen, Pflichtfeld-Prüfung und Glätten beim Export.
/// Clean-Room nach dem Bedienkonzept von Adobe Fill &amp; Sign, Xodo und PDF-XChange – mit PDFsharp 6.2.4 (MIT). Das Original bleibt immer unverändert.</summary>
public static class PdfFormular
{
    static PdfFormular() => PdfTools.EnsureFonts();

    static IEnumerable<PdfAcroField> Terminal(PdfAcroField.PdfAcroFieldCollection fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            var f = fields[i];
            // Optionsfelder und mehrfach sichtbare Ankreuzfelder haben Widgets als Kinder (ohne eigenen Namen) – sie sind trotzdem ein Feld
            if (f.HasKids && f is not PdfRadioButtonField && !(f is PdfCheckBoxField && KidsAreWidgets(f)) && !KidsAreWidgets(f))
            { foreach (var k in Terminal(f.Fields)) yield return k; continue; }
            yield return f;
        }
    }

    static bool KidsAreWidgets(PdfAcroField f) =>
        f.Elements.GetObject("/Kids") is PdfArray a && a.Elements.Count > 0 && a.Elements.All(e => Deref(e) is PdfDictionary k && !k.Elements.ContainsKey("/T"));

    static bool HasForm(PdfDocument d) => d.Internals.Catalog.Elements.ContainsKey("/AcroForm");

    static PdfDictionary? Deref(PdfItem? e) => (e as PdfReference)?.Value as PdfDictionary ?? e as PdfDictionary;

    static List<PdfDictionary> Widgets(PdfAcroField f) =>
        f.Elements.GetObject("/Kids") is PdfArray a && KidsAreWidgets(f) ? [.. a.Elements.Select(Deref).OfType<PdfDictionary>()] : [f];

    static string KindOf(PdfAcroField f) => f switch
    {
        PdfTextField => "Text",
        PdfCheckBoxField => "Ankreuzfeld",
        PdfRadioButtonField => "Optionsfeld",
        PdfComboBoxField => "Auswahl",
        PdfListBoxField => "Liste",
        PdfSignatureField => "Unterschrift",
        PdfPushButtonField => "Knopf",
        _ => f.Elements.GetName("/FT") switch { "/Tx" => "Text", "/Ch" => "Auswahl", "/Sig" => "Unterschrift", _ => "Sonstiges" },
    };

    static string Str(PdfItem? i) => i switch { PdfString s => s.Value, PdfName n => n.Value.TrimStart('/'), PdfReference r => Str(r.Value), _ => "" };

    static List<string> ChoiceOptions(PdfAcroField f)
    {
        if (f.Elements.GetObject("/Opt") is not PdfArray a) return [];
        return [.. a.Elements.Select(e => e is PdfArray pair && pair.Elements.Count >= 2 ? Str(pair.Elements[1]) : Str(e is PdfReference r ? r.Value : e))];
    }

    static List<string> RadioOptions(PdfAcroField f)
    {
        var list = new List<string>();
        foreach (var w in Widgets(f))
            if ((w.Elements.GetObject("/AP") as PdfDictionary)?.Elements.GetObject("/N") is PdfDictionary n)
                foreach (var k in n.Elements.Keys) if (k != "/Off" && !list.Contains(k.TrimStart('/'))) list.Add(k.TrimStart('/'));
        return list;
    }

    static string CheckOn(PdfAcroField f) => RadioOptions(f).FirstOrDefault() ?? "Yes";

    static (int Page, double X, double YTop, double W, double H) Place(PdfDocument d, PdfDictionary widget)
    {
        var page = -1;
        if (widget.Elements.GetReference("/P") is { } pr) for (var i = 0; i < d.PageCount; i++) if (ReferenceEquals(d.Pages[i].Reference, pr) || d.Pages[i].Reference?.ObjectID == pr.ObjectID) { page = i; break; }
        if (page < 0)
            for (var i = 0; i < d.PageCount && page < 0; i++)
                if (d.Pages[i].Elements.GetObject("/Annots") is PdfArray an && an.Elements.Any(e => ReferenceEquals(Deref(e), widget))) page = i;
        if (page < 0 || widget.Elements.GetObject("/Rect") is not PdfArray r || r.Elements.Count < 4) return (Math.Max(page, 0), 0, 0, 0, 0);
        double N(int k) => r.Elements[k] switch { PdfReal x => x.Value, PdfInteger x => x.Value, _ => 0 };
        double x1 = Math.Min(N(0), N(2)), x2 = Math.Max(N(0), N(2)), y1 = Math.Min(N(1), N(3)), y2 = Math.Max(N(1), N(3));
        var mb = d.Pages[page].MediaBox;
        return (page, x1 - mb.X1, mb.Y2 - y2, x2 - x1, y2 - y1);
    }

    /// <summary>Alle ausfüllbaren Felder mit Lage und Pflicht-Angabe. Ohne Formular: leere Liste.</summary>
    public static List<FormField> Fields(string path, string? password = null)
    {
        using var d = password is null ? PdfReader.Open(path, PdfDocumentOpenMode.Import) : PdfReader.Open(path, password, PdfDocumentOpenMode.Import);
        var list = new List<FormField>();
        if (!HasForm(d)) return list;
        foreach (var f in Terminal(d.AcroForm.Fields))
        {
            var kind = KindOf(f); var (page, x, y, w, h) = Place(d, Widgets(f)[0]);
            var value = f switch
            {
                PdfTextField t => t.Text ?? "",
                PdfCheckBoxField c => c.Checked ? "ja" : "nein",
                PdfRadioButtonField => Str(f.Elements["/V"]) is var v && v != "Off" ? v : "",
                PdfListBoxField or PdfComboBoxField => Str(f.Elements["/V"]),
                _ => Str(f.Elements["/V"]),
            };
            var opts = kind is "Auswahl" or "Liste" ? ChoiceOptions(f) : kind == "Optionsfeld" ? RadioOptions(f) : [];
            var flags = (int)f.Flags;
            list.Add(new FormField(f.Name, kind, value, (flags & 2) != 0, f.ReadOnly, opts, page, Math.Round(x, 2), Math.Round(y, 2), Math.Round(w, 2), Math.Round(h, 2),
                f.Elements.GetString("/TU"), f.Elements.GetInteger("/MaxLen"), f is PdfTextField tf && tf.MultiLine));
        }
        return list;
    }

    static bool Yes(string v) => v.Trim().ToLowerInvariant() is "ja" or "x" or "1" or "true" or "on" or "yes" or "✓";

    /// <summary>Pflichtfelder, die leer bleiben würden (Wert aus <paramref name="values"/>, sonst der Wert in der Datei). Ankreuzfeld „nein“ zählt als leer.</summary>
    public static List<string> MissingRequired(IEnumerable<FormField> fields, IReadOnlyDictionary<string, string> values) =>
        [.. fields.Where(f => f.Required && !f.ReadOnly).Where(f =>
        {
            var v = values.TryGetValue(f.Name, out var nv) ? nv : f.Value;
            return f.Kind == "Ankreuzfeld" ? !Yes(v) : string.IsNullOrWhiteSpace(v);
        }).Select(f => f.Name)];

    /// <summary>Ausfüllen, frei Gesetztes zeichnen und als neue Datei speichern; <paramref name="flatten"/> = Felder fest in die Seite einbrennen (glätten),
    /// danach ist nichts mehr änderbar. Pflichtfelder werden nur gemeldet, nicht erzwungen – das Programm fragt vorher.</summary>
    public static FormFillResult Fill(string input, string output, IReadOnlyDictionary<string, string> values, IReadOnlyList<FormOverlay>? overlays = null, bool flatten = false, AuditLog? audit = null)
    {
        if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Das Ergebnis braucht einen neuen Dateinamen – die Vorlage wird nie überschrieben.");
        var fields = Fields(input);
        var missing = MissingRequired(fields, values);
        var unknown = values.Keys.Where(k => fields.All(f => f.Name != k)).ToList();
        var filled = 0; var ov = overlays ?? [];
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.Copy(input, output, true);
        try
        {
            using (var d = PdfReader.Open(output, PdfDocumentOpenMode.Modify))
            {
                if (HasForm(d))
                {
                    foreach (var f in Terminal(d.AcroForm.Fields))
                    {
                        if (!values.TryGetValue(f.Name, out var v) || f.ReadOnly) continue;
                        Set(f, v); filled++;
                    }
                    d.AcroForm.Elements["/NeedAppearances"] = new PdfBoolean(!flatten);
                }
                foreach (var o in ov) Draw(d, o);
                foreach (var p in d.Pages.Cast<PdfPage>())
                    if (p.Elements.GetObject("/Resources") is null) p.Elements["/Resources"] = new PdfDictionary(d);
                d.Save(output);
            }
            // PDFsharp zeichnet das Aussehen der Textfelder erst beim Speichern – darum erst danach glätten
            if (flatten)
                using (var d = PdfReader.Open(output, PdfDocumentOpenMode.Modify))
                {
                    if (HasForm(d)) { FlattenForm(d); d.Save(output); }
                }
        }
        catch { try { File.Delete(output); } catch (IOException) { } throw; }
        audit?.Append("app", "pdf.form.fill", $"{filled} Felder, {ov.Count} gesetzt{(flatten ? ", geglättet" : "")} → {Path.GetFileName(output)}"); // nie die Werte
        return new(filled, unknown, missing, ov.Count, flatten);
    }

    /// <summary>Glätten: Das Aussehen jedes Felds (/AP, bei Knöpfen der gewählte Zustand) wird als Form-XObject fest in den Seiteninhalt gezeichnet,
    /// danach verschwinden Widgets und Formular. Felder ohne Aussehen, aber mit Wert, werden als Text an ihre Stelle geschrieben.</summary>
    static void FlattenForm(PdfDocument d)
    {
        var fallback = new Dictionary<int, string>(); // nach Objektnummer – PDFsharp hält Felder und Widgets teils als getrennte Objekte
        foreach (var f in Terminal(d.AcroForm.Fields))
        {
            var text = f switch { PdfTextField t => t.Text ?? "", PdfComboBoxField or PdfListBoxField => Str(f.Elements["/V"]), _ => "" };
            if (text.Length > 0) foreach (var w in Widgets(f)) if (w.Reference is { } r) fallback[r.ObjectNumber] = text;
        }
        var n = 0;
        for (var i = 0; i < d.PageCount; i++)
        {
            var page = d.Pages[i];
            if (page.Elements.GetObject("/Annots") is not PdfArray annots) continue;
            var keep = new List<PdfItem>(); var ops = new StringBuilder(); var texts = new List<(double X, double Y, double W, double H, string Text)>();
            var mb = page.MediaBox;
            foreach (var item in annots.Elements)
            {
                var w = Deref(item);
                if (w is null || w.Elements.GetName("/Subtype") != "/Widget") { keep.Add(item); continue; }
                if ((w.Elements.GetInteger("/F") & 2) != 0) continue; // versteckt
                if (w.Elements.GetObject("/Rect") is not PdfArray r || r.Elements.Count < 4) continue;
                double R(int k) => r.Elements[k] switch { PdfReal x => x.Value, PdfInteger x => x.Value, _ => 0 };
                double x1 = Math.Min(R(0), R(2)), x2 = Math.Max(R(0), R(2)), y1 = Math.Min(R(1), R(3)), y2 = Math.Max(R(1), R(3));
                var normal = (w.Elements.GetObject("/AP") as PdfDictionary)?.Elements.GetObject("/N") as PdfDictionary;
                if (normal is not null && normal.Stream is null) normal = normal.Elements.GetObject(w.Elements.GetName("/AS") is { Length: > 0 } st ? st : "/Off") as PdfDictionary;
                if (normal?.Stream is not null)
                {
                    if (normal.Reference is null) d.Internals.AddObject(normal);
                    var bb = normal.Elements.GetObject("/BBox") as PdfArray;
                    double B(int k, double def) => bb is not null && bb.Elements.Count > k ? bb.Elements[k] switch { PdfReal x => x.Value, PdfInteger x => x.Value, _ => def } : def;
                    double bx = B(0, 0), by = B(1, 0), bw = Math.Max(0.01, B(2, x2 - x1) - bx), bh = Math.Max(0.01, B(3, y2 - y1) - by);
                    double sx = (x2 - x1) / bw, sy = (y2 - y1) / bh;
                    var name = "/TrFlat" + n++;
                    var res = page.Elements.GetObject("/Resources") as PdfDictionary ?? (PdfDictionary)(page.Elements["/Resources"] = new PdfDictionary(d));
                    if (res.Elements.GetObject("/XObject") is not PdfDictionary xo) { xo = new PdfDictionary(d); res.Elements["/XObject"] = xo; }
                    xo.Elements[name] = normal.Reference;
                    string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
                    ops.Append(CultureInfo.InvariantCulture, $"q {F(sx)} 0 0 {F(sy)} {F(x1 - bx * sx)} {F(y1 - by * sy)} cm {name} Do Q\n");
                }
                else if (w.Reference is { } wr && fallback.TryGetValue(wr.ObjectNumber, out var t)) texts.Add((x1 - mb.X1, mb.Y2 - y2, x2 - x1, y2 - y1, t));
            }
            if (keep.Count == 0) page.Elements.Remove("/Annots"); else { annots.Elements.Clear(); foreach (var k in keep) annots.Elements.Add(k); }
            if (ops.Length > 0)
            {
                page.Contents.PrependContent().CreateStream(Encoding.ASCII.GetBytes("q\n"));
                page.Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes("Q\n" + ops));
            }
            if (texts.Count > 0)
            {
                using var g = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                foreach (var (x, y, w, h, t) in texts)
                {
                    var font = new XFont("Arial", Math.Clamp(h * 0.6, 6, 12));
                    g.DrawString(t, font, XBrushes.Black, new XRect(x + 2, y, Math.Max(1, w - 4), h), XStringFormats.CenterLeft);
                }
            }
        }
        d.Internals.Catalog.Elements.Remove("/AcroForm");
    }

    static void Set(PdfAcroField f, string v)
    {
        switch (f)
        {
            case PdfTextField t:
                var max = t.MaxLength; t.Text = max > 0 && v.Length > max ? v[..max] : v; break;
            case PdfCheckBoxField c:
                if (KidsAreWidgets(c)) SetStates(c, Yes(v) ? CheckOn(c) : null); else c.Checked = Yes(v);
                break;
            case PdfRadioButtonField:
                var opts = RadioOptions(f);
                var pick = opts.FirstOrDefault(o => string.Equals(o, v.Trim(), StringComparison.OrdinalIgnoreCase));
                if (v.Trim().Length > 0 && pick is null) throw new ArgumentException($"„{v}“ gibt es im Feld „{f.Name}“ nicht. Möglich: {string.Join(", ", opts)}");
                SetStates(f, pick); break;
            case PdfComboBoxField or PdfListBoxField:
                var list = ChoiceOptions(f); var i = list.FindIndex(o => string.Equals(o, v.Trim(), StringComparison.OrdinalIgnoreCase));
                if (i < 0 && v.Trim().Length > 0 && (f is PdfListBoxField || ((int)f.Flags & (1 << 18)) == 0)) throw new ArgumentException($"„{v}“ gibt es im Feld „{f.Name}“ nicht. Möglich: {string.Join(", ", list)}");
                f.Elements["/V"] = new PdfString(i >= 0 ? ExportValue(f, i) : v.Trim(), PdfStringEncoding.Unicode);
                if (i >= 0) f.Elements["/I"] = new PdfArray(f.Owner, new PdfInteger(i)); else f.Elements.Remove("/I");
                break;
            case PdfSignatureField or PdfPushButtonField: break;
            default: f.Value = new PdfString(v, PdfStringEncoding.Unicode); break;
        }
    }

    static string ExportValue(PdfAcroField f, int i) =>
        f.Elements.GetObject("/Opt") is PdfArray a && i < a.Elements.Count && a.Elements[i] is PdfArray pair && pair.Elements.Count >= 1 ? Str(pair.Elements[0]) : ChoiceOptions(f)[i];

    /// <summary>Zustand eines Optionsfelds bzw. Ankreuzfelds mit Widget-Kindern: /V am Feld, /AS je Widget (gewählt oder /Off).</summary>
    static void SetStates(PdfAcroField f, string? on)
    {
        f.Elements["/V"] = new PdfName("/" + (on ?? "Off"));
        foreach (var w in Widgets(f))
        {
            var has = on is not null && (w.Elements.GetObject("/AP") as PdfDictionary)?.Elements.GetObject("/N") is PdfDictionary n && n.Elements.ContainsKey("/" + on);
            w.Elements["/AS"] = new PdfName(has ? "/" + on : "/Off");
        }
    }

    static void Draw(PdfDocument d, FormOverlay o)
    {
        if (o.Page < 0 || o.Page >= d.PageCount) throw new ArgumentException("Diese Seite gibt es nicht.");
        var page = d.Pages[o.Page];
        using var g = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
        var ink = new XSolidBrush(XColor.FromArgb(255, 12, 30, 80));
        switch (o.Kind)
        {
            case "text" or "datum":
                var text = o.Kind == "datum" && string.IsNullOrWhiteSpace(o.Text) ? DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : o.Text;
                if (string.IsNullOrWhiteSpace(text)) return;
                var font = new XFont("Arial", Math.Clamp(o.Size, 5, 48));
                var lh = font.GetHeight(); var y = o.YTop + lh * 0.8;
                foreach (var line in text.Replace("\r", "").Split('\n')) { g.DrawString(line, font, ink, o.X, y); y += lh; }
                break;
            case "haken":
                var s = Math.Clamp(o.Size, 4, 60);
                g.DrawLines(new XPen(XColor.FromArgb(255, 12, 30, 80), Math.Max(1.2, s / 7)) { LineCap = XLineCap.Round, LineJoin = XLineJoin.Round },
                    [new XPoint(o.X, o.YTop + s * 0.55), new XPoint(o.X + s * 0.38, o.YTop + s * 0.92), new XPoint(o.X + s, o.YTop + s * 0.08)]);
                break;
            case "kreuz":
                var k = Math.Clamp(o.Size, 4, 60); var pen = new XPen(XColor.FromArgb(255, 12, 30, 80), Math.Max(1.2, k / 7)) { LineCap = XLineCap.Round };
                g.DrawLine(pen, o.X, o.YTop, o.X + k, o.YTop + k); g.DrawLine(pen, o.X + k, o.YTop, o.X, o.YTop + k);
                break;
            case "bild":
                if (o.Image is null || !File.Exists(o.Image)) throw new ArgumentException("Das Bild der Unterschrift fehlt.");
                var (iw, ih) = XrPlus.ImageSize(File.ReadAllBytes(o.Image));
                if (iw <= 0 || ih <= 0) throw new ArgumentException("Bitte ein PNG- oder JPG-Bild.");
                using (var img = XImage.FromFile(o.Image)) g.DrawImage(img, o.X, o.YTop, o.W, o.W * ih / iw);
                break;
            default: throw new ArgumentException("Unbekannte Art: " + o.Kind);
        }
    }

    // ---------- Formular vorbereiten (Felder anlegen) ----------

    /// <summary>Formularfelder in eine PDF einfügen (wie „Formular vorbereiten“ in Acrobat): Textfeld, Ankreuzfeld, Auswahl, Optionsfeld.
    /// Lage in Punkten von links/oben. Gibt die Zahl der neuen Felder zurück.</summary>
    public static int AddFields(string input, string output, IReadOnlyList<FormFieldSpec> specs, AuditLog? audit = null)
    {
        if (specs.Count == 0) throw new ArgumentException("Keine Felder angegeben.");
        if (specs.GroupBy(s => s.Name).Any(g => g.Count() > 1 && g.First().Kind != "optionen")) throw new ArgumentException("Jeder Feldname darf nur einmal vorkommen.");
        if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Das Ergebnis braucht einen neuen Dateinamen – die Vorlage wird nie überschrieben.");
        var existing = Fields(input).Select(f => f.Name).ToHashSet();
        if (specs.FirstOrDefault(s => existing.Contains(s.Name)) is { } dup) throw new ArgumentException($"Das Feld „{dup.Name}“ gibt es schon.");
        File.Copy(input, output, true);
        try
        {
            using var d = PdfReader.Open(output, PdfDocumentOpenMode.Modify);
            var cat = d.Internals.Catalog;
            PdfDictionary form;
            if (cat.Elements.GetObject("/AcroForm") is PdfDictionary f0) form = f0;
            else { form = new PdfDictionary(d); d.Internals.AddObject(form); cat.Elements["/AcroForm"] = form.Reference; }
            if (form.Elements.GetObject("/Fields") is not PdfArray fieldsArr) { fieldsArr = new PdfArray(d); form.Elements["/Fields"] = fieldsArr; }
            var helv = new PdfDictionary(d);
            helv.Elements["/Type"] = new PdfName("/Font"); helv.Elements["/Subtype"] = new PdfName("/Type1"); helv.Elements["/BaseFont"] = new PdfName("/Helvetica"); helv.Elements["/Encoding"] = new PdfName("/WinAnsiEncoding");
            d.Internals.AddObject(helv);
            if (form.Elements.GetObject("/DR") is not PdfDictionary dr) { dr = new PdfDictionary(d); form.Elements["/DR"] = dr; }
            if (dr.Elements.GetObject("/Font") is not PdfDictionary fonts) { fonts = new PdfDictionary(d); dr.Elements["/Font"] = fonts; }
            if (!fonts.Elements.ContainsKey("/Helv")) fonts.Elements["/Helv"] = helv.Reference;
            if (!form.Elements.ContainsKey("/DA")) form.Elements["/DA"] = new PdfString("/Helv 0 Tf 0 g");

            var radios = new Dictionary<string, PdfDictionary>();
            foreach (var s in specs)
            {
                if (s.Page < 0 || s.Page >= d.PageCount) throw new ArgumentException($"Seite {s.Page + 1} gibt es nicht.");
                var page = d.Pages[s.Page]; var mb = page.MediaBox;
                var rect = Nums(d, mb.X1 + s.X, mb.Y2 - s.YTop - s.H, mb.X1 + s.X + s.W, mb.Y2 - s.YTop);
                PdfDictionary W()
                {
                    var w = new PdfDictionary(d);
                    w.Elements["/Type"] = new PdfName("/Annot"); w.Elements["/Subtype"] = new PdfName("/Widget");
                    w.Elements["/Rect"] = rect; w.Elements["/P"] = page.Reference; w.Elements["/F"] = new PdfInteger(4);
                    w.Elements["/MK"] = new PdfDictionary(d); ((PdfDictionary)w.Elements["/MK"]!).Elements["/BC"] = Nums(d, 0.45, 0.45, 0.45);
                    d.Internals.AddObject(w); Annots(page).Elements.Add(w.Reference!); return w;
                }
                switch (s.Kind)
                {
                    case "text":
                    {
                        var w = W(); w.Elements["/FT"] = new PdfName("/Tx"); Name(w, s); w.Elements["/DA"] = new PdfString("/Helv 10 Tf 0 g"); w.Elements["/V"] = new PdfString("");
                        if (s.Required) w.Elements["/Ff"] = new PdfInteger(2);
                        fieldsArr.Elements.Add(w.Reference!); break;
                    }
                    case "haken":
                    {
                        var w = W(); w.Elements["/FT"] = new PdfName("/Btn"); Name(w, s); w.Elements["/V"] = new PdfName("/Off"); w.Elements["/AS"] = new PdfName("/Off");
                        w.Elements["/AP"] = Appearance(d, s.W, s.H, "Ja", true); w.Elements["/DA"] = new PdfString("/ZaDb 0 Tf 0 g");
                        if (s.Required) w.Elements["/Ff"] = new PdfInteger(2);
                        fieldsArr.Elements.Add(w.Reference!); break;
                    }
                    case "auswahl":
                    {
                        if (s.Options is not { Count: > 0 }) throw new ArgumentException($"Das Auswahlfeld „{s.Name}“ braucht Einträge.");
                        var w = W(); w.Elements["/FT"] = new PdfName("/Ch"); Name(w, s); w.Elements["/DA"] = new PdfString("/Helv 10 Tf 0 g");
                        w.Elements["/Ff"] = new PdfInteger((1 << 17) | (s.Required ? 2 : 0));
                        var opt = new PdfArray(d); foreach (var o in s.Options) opt.Elements.Add(new PdfString(o, PdfStringEncoding.Unicode));
                        w.Elements["/Opt"] = opt; w.Elements["/V"] = new PdfString("");
                        fieldsArr.Elements.Add(w.Reference!); break;
                    }
                    case "optionen":
                    {
                        if (s.Options is not { Count: 1 }) throw new ArgumentException($"Jeder Knopf des Optionsfelds „{s.Name}“ braucht genau einen Wert (Options mit einem Eintrag).");
                        if (!radios.TryGetValue(s.Name, out var parent))
                        {
                            parent = new PdfDictionary(d); parent.Elements["/FT"] = new PdfName("/Btn"); Name(parent, s);
                            parent.Elements["/Ff"] = new PdfInteger((1 << 15) | (1 << 14) | (s.Required ? 2 : 0)); parent.Elements["/V"] = new PdfName("/Off");
                            parent.Elements["/Kids"] = new PdfArray(d); d.Internals.AddObject(parent); radios[s.Name] = parent; fieldsArr.Elements.Add(parent.Reference!);
                        }
                        var on = Regex.Replace(s.Options[0], @"[^A-Za-z0-9_\-]", "_");
                        var w = W(); w.Elements["/Parent"] = parent.Reference; w.Elements["/AS"] = new PdfName("/Off"); w.Elements["/AP"] = Appearance(d, s.W, s.H, on, false);
                        ((PdfArray)parent.Elements["/Kids"]!).Elements.Add(w.Reference!); break;
                    }
                    default: throw new ArgumentException("Unbekannte Feldart: " + s.Kind + " (text, haken, auswahl, optionen)");
                }
            }
            d.Save(output);
        }
        catch { try { File.Delete(output); } catch (IOException) { } throw; }
        audit?.Append("app", "pdf.form.prepare", $"{specs.Count} Felder → {Path.GetFileName(output)}");
        return specs.Count;
    }

    static void Name(PdfDictionary w, FormFieldSpec s)
    {
        w.Elements["/T"] = new PdfString(s.Name, PdfStringEncoding.Unicode);
        if (s.Label.Length > 0) w.Elements["/TU"] = new PdfString(s.Label, PdfStringEncoding.Unicode);
    }

    static PdfArray Annots(PdfPage page)
    {
        if (page.Elements.GetObject("/Annots") is PdfArray a) return a;
        var arr = new PdfArray(page.Owner); page.Elements["/Annots"] = arr; return arr;
    }

    static PdfArray Nums(PdfDocument d, params double[] v) { var a = new PdfArray(d); foreach (var x in v) a.Elements.Add(new PdfReal(Math.Round(x, 3))); return a; }

    /// <summary>Aussehen „an“ (Häkchen bzw. Punkt) und „aus“ (leer) für Ankreuz- und Optionsfelder.</summary>
    static PdfDictionary Appearance(PdfDocument d, double w, double h, string on, bool check)
    {
        PdfDictionary Xo(string ops)
        {
            var x = new PdfDictionary(d);
            x.Elements["/Type"] = new PdfName("/XObject"); x.Elements["/Subtype"] = new PdfName("/Form"); x.Elements["/BBox"] = Nums(d, 0, 0, w, h);
            x.Elements["/Resources"] = new PdfDictionary(d);
            x.CreateStream(Encoding.ASCII.GetBytes(ops)); d.Internals.AddObject(x); return x;
        }
        string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        var box = $"0.45 G 0.8 w 0.5 0.5 {F(w - 1)} {F(h - 1)} re S ";
        var mark = check
            ? $"0 0 0.35 RG {F(Math.Max(1, h / 8))} w 1 J 1 j {F(w * 0.2)} {F(h * 0.5)} m {F(w * 0.42)} {F(h * 0.22)} l {F(w * 0.82)} {F(h * 0.8)} l S"
            : $"0 0 0.35 rg {Circle(w / 2, h / 2, Math.Min(w, h) * 0.25)} f";
        var n = new PdfDictionary(d);
        n.Elements["/" + on] = Xo(box + mark).Reference; n.Elements["/Off"] = Xo(box).Reference;
        var ap = new PdfDictionary(d); ap.Elements["/N"] = n; return ap;
    }

    static string Circle(double cx, double cy, double r)
    {
        var k = r * 0.5523; string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        return $"{F(cx + r)} {F(cy)} m {F(cx + r)} {F(cy + k)} {F(cx + k)} {F(cy + r)} {F(cx)} {F(cy + r)} c {F(cx - k)} {F(cy + r)} {F(cx - r)} {F(cy + k)} {F(cx - r)} {F(cy)} c " +
               $"{F(cx - r)} {F(cy - k)} {F(cx - k)} {F(cy - r)} {F(cx)} {F(cy - r)} c {F(cx + k)} {F(cy - r)} {F(cx + r)} {F(cy - k)} {F(cx + r)} {F(cy)} c";
    }
}

/// <summary>„Meine Angaben“ für Formulare: Name, Anschrift, Geburtsdaten, Kontakt, Bankverbindung. Wird nur lokal im Geheimnisspeicher von Trendo abgelegt
/// (nie in Dokumente, Protokolle oder das Projekt). Die IBAN wird nur maskiert angezeigt; in Dokumenten steht statt der IBAN der Platzhalter „@profil:iban“,
/// erst beim Export wird er ersetzt.</summary>
public sealed class FormProfile
{
    public const string SecretKey = "formulare.meine-angaben";
    public const string Ref = "@profil:";
    /// <summary>Schlüssel und Anzeigename – in dieser Reihenfolge im Programm.</summary>
    public static readonly (string Key, string Name)[] Keys =
    [
        ("anrede", "Anrede"), ("titel", "Titel"), ("vorname", "Vorname"), ("nachname", "Nachname"), ("geburtsname", "Geburtsname"), ("geburtsdatum", "Geburtsdatum"), ("geburtsort", "Geburtsort"),
        ("staatsangehoerigkeit", "Staatsangehörigkeit"), ("strasse", "Straße"), ("hausnummer", "Hausnummer"), ("plz", "PLZ"), ("ort", "Ort"), ("land", "Land"),
        ("telefon", "Telefon"), ("email", "E-Mail"), ("iban", "IBAN"), ("bic", "BIC"), ("bank", "Bank"), ("steuerid", "Steuer-ID"),
    ];
    /// <summary>Diese Angaben stehen nie im Dokument, nur als Platzhalter.</summary>
    public static readonly string[] Sensitive = ["iban", "steuerid"];

    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
    public string this[string key] { get => Values.GetValueOrDefault(key, ""); set { if (string.IsNullOrWhiteSpace(value)) Values.Remove(key); else Values[key] = value.Trim(); } }

    public static FormProfile Load(ISecretStore store)
    {
        var p = new FormProfile();
        try
        {
            if (store.Get(SecretKey) is { Length: > 0 } json && JsonSerializer.Deserialize<Dictionary<string, string>>(json) is { } d)
                foreach (var (k, v) in d) if (Keys.Any(x => x.Key == k)) p[k] = v;
        }
        catch (JsonException) { /* beschädigt: leer beginnen */ }
        return p;
    }

    public void Save(ISecretStore store) => store.Set(SecretKey, JsonSerializer.Serialize(Values));

    /// <summary>IBAN prüfen (Länge, Ländercode, Prüfziffer nach ISO 13616, mod 97).</summary>
    public static bool IbanValid(string? iban)
    {
        var s = Regex.Replace(iban ?? "", @"\s", "").ToUpperInvariant();
        if (s.Length is < 15 or > 34 || !Regex.IsMatch(s, "^[A-Z]{2}[0-9]{2}[A-Z0-9]+$")) return false;
        if (s.StartsWith("DE", StringComparison.Ordinal) && s.Length != 22) return false;
        var r = s[4..] + s[..4]; var mod = 0;
        foreach (var c in r) { var v = char.IsDigit(c) ? c - '0' : c - 'A' + 10; mod = v >= 10 ? (mod * 100 + v) % 97 : (mod * 10 + v) % 97; }
        return mod == 1;
    }

    /// <summary>Maskiert zum Anzeigen: Ländercode, Prüfziffer und die letzten 4 Zeichen, dazwischen Punkte – z. B. „DE89 •••• •••• •••• •• 3000“.</summary>
    public static string Mask(string? value)
    {
        var s = Regex.Replace(value ?? "", @"\s", "");
        if (s.Length <= 8) return new string('•', s.Length);
        var mid = s.Length - 8; var sb = new StringBuilder(s[..4]);
        for (var i = 0; i < mid; i++) { if (i % 4 == 0) sb.Append(' '); sb.Append('•'); }
        return sb.Append(' ').Append(s[^4..]).ToString();
    }

    static string Norm(string s)
    {
        var t = s.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        return Regex.Replace(t, "[^a-z0-9]", "");
    }

    /// <summary>Regeln: Schlüssel(n) des Profils ← Teile im Feldnamen. Reihenfolge zählt (Geburtsort vor Ort, Vorname vor Name …).</summary>
    static readonly (string[] Keys, string[] Contains, string[] Exact)[] Rules =
    [
        (["plz", "ort"], ["plzort", "plzundort", "plzwohnort", "postleitzahlundort", "postleitzahlort"], []),
        (["strasse", "hausnummer"], ["strasseundhausnummer", "strassehausnummer", "strassehausnr", "strasseundhausnr", "strassenr", "anschrift", "address", "adresse"], []),
        (["vorname", "nachname"], ["vornameundname", "vornamenachname", "vollername", "fullname", "nameundvorname", "namevorname"], []),
        (["geburtsort"], ["geburtsort", "placeofbirth", "birthplace"], []),
        (["geburtsname"], ["geburtsname", "maidenname", "birthname"], []),
        (["geburtsdatum"], ["geburtsdatum", "geburtstag", "gebdatum", "gebdat", "dateofbirth", "birthdate", "birthday", "dob"], ["geboren", "geboram"]),
        (["staatsangehoerigkeit"], ["staatsangehoerigkeit", "nationalitaet", "nationality", "citizenship"], []),
        (["vorname"], ["vorname", "firstname", "givenname", "rufname"], []),
        (["nachname"], ["nachname", "familienname", "lastname", "surname", "zuname", "familyname"], ["name"]),
        (["hausnummer"], ["hausnummer", "hausnr", "housenumber"], ["nr", "hnr"]),
        (["strasse"], ["strasse", "street"], ["str"]),
        (["plz"], ["postleitzahl", "zipcode", "postalcode", "postcode"], ["plz", "zip"]),
        (["ort"], ["wohnort", "stadt", "city", "town", "gemeinde"], ["ort"]),
        (["land"], ["country", "wohnland"], ["land", "staat"]),
        (["telefon"], ["telefon", "phone", "mobil", "handy"], ["tel", "telnr"]),
        (["email"], ["email", "mail"], []),
        (["iban"], ["iban"], []),
        (["bic"], ["bic", "swift"], []),
        (["bank"], ["kreditinstitut", "bankname", "geldinstitut"], ["bank"]),
        (["steuerid"], ["steuerid", "steueridentifikationsnummer", "identifikationsnummer", "taxid"], []),
        (["anrede"], ["anrede", "salutation"], []),
        (["titel"], ["akademischergrad", "doktorgrad"], ["titel"]),
    ];

    /// <summary>Welche Angaben passen zu einem Feld (nach Feldname und Hinweistext)? Leer = keine Zuordnung. Mehrere Schlüssel werden zusammengesetzt
    /// (z. B. „PLZ, Ort“ → „12345 Musterstadt“).</summary>
    public static string[] Match(string fieldName, string label = "")
    {
        // Name und Hinweistext getrennt prüfen; die genauere Zuordnung (mehr Angaben, z. B. „Straße und Hausnummer“) gewinnt
        string[] best = [];
        foreach (var raw in new[] { fieldName, label })
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var hit = MatchOne(raw);
            if (hit.Length > best.Length) best = hit;
        }
        return best;
    }

    static string[] MatchOne(string raw)
    {
        {
            // Teile hinter Punkten (verschachtelte Namen „antrag.person.vorname“) zählen einzeln, das letzte zuerst
            var parts = raw.Split('.', '/', '[').Reverse().Select(Norm).Where(p => p.Length > 0).ToList();
            foreach (var n in parts)
                foreach (var (keys, contains, exact) in Rules)
                    if (exact.Contains(n) || contains.Any(n.Contains)) return keys;
        }
        return [];
    }

    /// <summary>Text, der ins Feld käme (ohne Platzhalter). Fehlt eine nötige Angabe, ist das Ergebnis leer.</summary>
    public string Value(string[] keys)
    {
        if (keys.Length == 0 || keys.Any(k => this[k].Length == 0)) return "";
        return keys switch
        {
            ["plz", "ort"] => $"{this["plz"]} {this["ort"]}",
            ["strasse", "hausnummer"] => $"{this["strasse"]} {this["hausnummer"]}",
            ["vorname", "nachname"] => $"{this["vorname"]} {this["nachname"]}",
            _ => this[keys[0]],
        };
    }

    /// <summary>Vorschläge für leere Felder: Feldname → Eintrag fürs Dokument. Für Text, Auswahl und Liste; heikle Angaben (IBAN, Steuer-ID) als Platzhalter.
    /// Auswahlfelder bekommen nur einen Vorschlag, wenn der Wert zu einer Auswahl passt.</summary>
    public Dictionary<string, string> Suggest(IEnumerable<FormField> fields, IReadOnlyDictionary<string, string>? current = null)
    {
        var res = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in fields.Where(f => !f.ReadOnly && f.Kind is "Text" or "Auswahl" or "Liste"))
        {
            var have = current is not null && current.TryGetValue(f.Name, out var c) ? c : f.Value;
            if (!string.IsNullOrWhiteSpace(have)) continue;
            var keys = Match(f.Name, f.Label); var v = Value(keys);
            if (v.Length == 0) continue;
            if (f.Kind != "Text") { var opt = f.Options.FirstOrDefault(o => string.Equals(o, v, StringComparison.OrdinalIgnoreCase)); if (opt is null) continue; v = opt; }
            else if (keys.Length == 1 && Sensitive.Contains(keys[0])) v = Ref + keys[0];
            if (f.MaxLength > 0 && !v.StartsWith(Ref, StringComparison.Ordinal) && v.Length > f.MaxLength) continue; // passt nicht – lieber nicht vorschlagen
            res[f.Name] = v;
        }
        return res;
    }

    /// <summary>Platzhalter („@profil:iban“) durch die echten Angaben ersetzen – nur für den Export. Fehlt die Angabe, bleibt das Feld leer.</summary>
    public Dictionary<string, string> Resolve(IReadOnlyDictionary<string, string> values) =>
        values.ToDictionary(kv => kv.Key, kv => kv.Value.StartsWith(Ref, StringComparison.Ordinal) ? this[kv.Value[Ref.Length..]] : kv.Value, StringComparer.Ordinal);

    /// <summary>Zum Anzeigen: Platzhalter werden maskiert gezeigt.</summary>
    public string Display(string value) => value.StartsWith(Ref, StringComparison.Ordinal) ? "Aus „Meine Angaben“: " + Mask(this[value[Ref.Length..]]) : value;
}
