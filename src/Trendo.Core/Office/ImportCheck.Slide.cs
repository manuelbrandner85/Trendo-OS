using System.IO.Compression;
using System.Xml.Linq;

namespace Trendo.Core.Office;

/// <summary>T-0682: Importprüfung für Trendo Präsentation (PPTX, ODP). Gegenstück zu <see cref="SlideFiles.ReadPptx"/> und <see cref="SlideFiles.ReadOdp"/>.</summary>
public static partial class ImportCheck
{
    static readonly XNamespace Pn = "http://schemas.openxmlformats.org/presentationml/2006/main";

    static readonly Lazy<HashSet<string>> OwnSlideChartNames = new(() =>
    {
        var names = new HashSet<string>();
        foreach (var k in Enum.GetValues<ChartKind>())
            foreach (var text in new[] { "Titel", "" })
                foreach (var pair in ChartPairs(XDocument.Parse(SlideFiles.ChartXml(new SlideShape { Kind = ShapeKind.Diagramm, ChartKind = k, Text = text, ChartLabels = ["A", "B"], ChartValues = [1, 2] }, SlideThemes.All[0])).Root!)) names.Add(pair);
        return names;
    });

    static void Pptx(ZipArchive z, Losses l)
    {
        var pres = X(z, "ppt/presentation.xml"); if (pres?.Root is null) return;
        var presRels = RelsOf(z, "ppt/presentation.xml");

        // Design: Trendo erkennt nur seine eigenen Designs am Namen – alles andere wird durch das Standarddesign ersetzt
        var ownDesign = presRels.Values.FirstOrDefault(r => r.Type.EndsWith("/theme", StringComparison.Ordinal)) is { Target.Length: > 0 } tr && X(z, tr.Target)?.Root is { } theme && SlideThemes.All.Any(t => t.Name == (string?)theme.Attribute("name"));
        if (!ownDesign) l.Add("pptx.design", "Design der Vorlage (Farben, Schriften und Platzhalter-Formate – Trendo setzt sein Standarddesign)");
        var onMaster = 0;
        foreach (var e in z.Entries.Where(e => (e.FullName.StartsWith("ppt/slideMasters/", StringComparison.Ordinal) || e.FullName.StartsWith("ppt/slideLayouts/", StringComparison.Ordinal)) && e.FullName.EndsWith(".xml", StringComparison.Ordinal) && !e.FullName.Contains("/_rels/")))
        {
            var tree = e.LoadXml().Root?.Element(Pn + "cSld")?.Element(Pn + "spTree"); if (tree is null) continue;
            onMaster += tree.Elements(Pn + "pic").Count() + tree.Elements(Pn + "grpSp").Count() + tree.Elements(Pn + "cxnSp").Count() + tree.Elements(Pn + "sp").Count(sp => sp.Element(Pn + "nvSpPr")?.Element(Pn + "nvPr")?.Element(Pn + "ph") is null);
        }
        l.Add("pptx.vorlage", "Bilder und Formen aus der Folienvorlage (z. B. ein Logo auf jeder Folie)", onMaster);

        var no = 0;
        foreach (var sid in pres.Root.Element(Pn + "sldIdLst")?.Elements(Pn + "sldId") ?? [])
        {
            if ((string?)sid.Attribute(Rn + "id") is not { } rid || !presRels.TryGetValue(rid, out var rel) || X(z, rel.Target)?.Root is not { } sld) continue;
            var wo = "Folie " + ++no; var rels = RelsOf(z, rel.Target);
            if ((string?)sld.Attribute("show") is "0" or "false") l.Add("pptx.versteckt", "Ausgeblendete Folien (sind in Trendo sichtbar)", 1, wo);
            var bg = sld.Element(Pn + "cSld")?.Element(Pn + "bg");
            if (bg is not null && bg.Element(Pn + "bgPr")?.Element(An + "solidFill")?.Element(An + "srgbClr") is null) l.Add("pptx.hintergrund", "Folienhintergrund mit Bild, Verlauf oder Designfarbe", 1, wo);

            var handled = new HashSet<string>();
            foreach (var el in sld.Element(Pn + "cSld")?.Element(Pn + "spTree")?.Elements() ?? [])
            {
                string? Id() => (string?)el.Descendants(Pn + "cNvPr").FirstOrDefault()?.Attribute("id");
                switch (el.Name.LocalName)
                {
                    case "nvGrpSpPr" or "grpSpPr" or "extLst": break;
                    case "sp": PptxShape(el, wo, l); if (Id() is { } a) handled.Add(a); break;
                    case "pic":
                        if (el.Descendants().Any(e => e.Name.LocalName is "videoFile" or "audioFile" or "wavAudioFile" or "quickTimeFile")) l.Add("pptx.medien", "Videos und Ton (es bleibt höchstens das Standbild)", 1, wo);
                        else if ((string?)el.Descendants(An + "blip").FirstOrDefault()?.Attribute(Rn + "embed") is not { } emb || !rels.TryGetValue(emb, out var pr) || z.GetEntry(pr.Target) is null) l.Add("pptx.bild", "Bilder, die Trendo nicht übernimmt (verknüpft)", 1, wo);
                        else if (Id() is { } b) handled.Add(b);
                        if (el.Descendants(An + "srcRect").Any(s => s.HasAttributes)) l.Add("pptx.zuschnitt", "Zuschnitt von Bildern (Trendo zeigt das ganze Bild)", 1, wo);
                        break;
                    case "graphicFrame":
                        if (el.Descendants(An + "tbl").FirstOrDefault() is { } tbl)
                        {
                            l.Add("pptx.tabelle.verbunden", "Verbundene Zellen in Tabellen", tbl.Descendants(An + "tc").Count(tc => int.TryParse((string?)tc.Attribute("gridSpan"), out var g) && g > 1 || int.TryParse((string?)tc.Attribute("rowSpan"), out var rs) && rs > 1), wo);
                            l.Add("pptx.tabelle.format", "Tabellenvorlagen (Farben und Rahmen der Tabelle)", tbl.Element(An + "tblPr")?.Element(An + "tableStyleId") is not null ? 1 : 0, wo);
                            if (Id() is { } c) handled.Add(c);
                        }
                        else if ((string?)el.Descendants(Cn + "chart").FirstOrDefault()?.Attribute(Rn + "id") is { } cid && rels.TryGetValue(cid, out var cr) && X(z, cr.Target)?.Root is { } chart)
                        {
                            if (chart.Descendants(Cn + "ser").Count() > 1) l.Add("pptx.diagramm.reihen", "Weitere Datenreihen in Diagrammen (Trendo behält die erste)", 1, wo);
                            var own = OwnSlideChartNames.Value;
                            if (chart.Descendants(Cn + "plotArea").FirstOrDefault() is { } plot && !ChartKindKept(plot)) l.Add("pptx.diagramm.art", "Diagrammarten, die Trendo nur ähnlich kennt (z. B. gestapelt, liegend, Fläche, Punkte)", 1, wo);
                            if (ChartPairs(chart).Any(p => !own.Contains(p))) l.Add("pptx.diagramm", "Diagramm-Einzelheiten (Trendo behält Art, Werte und Titel – nicht z. B. Achsentitel, Farben, Legende)", 1, wo);
                            if (Id() is { } d) handled.Add(d);
                        }
                        else if (el.Descendants().Any(e => e.Name.LocalName == "oleObj")) l.Add("pptx.objekt", "Eingebettete Objekte (z. B. eine Excel-Tabelle)", 1, wo);
                        else if (el.Descendants().Any(e => e.Name.LocalName == "relIds")) l.Add("pptx.smartart", "SmartArt-Grafiken", 1, wo);
                        else l.Add("pptx.unbekannt", "Objekte, die Trendo nicht kennt", 1, wo);
                        break;
                    case "grpSp": l.Add("pptx.gruppe", "Gruppierte Objekte (fehlen in Trendo ganz)", 1, wo + (Short(string.Concat(el.Descendants(An + "t").Select(t => t.Value + " "))) is { Length: > 0 } gt ? $": „{gt}“" : "")); break;
                    case "cxnSp": l.Add("pptx.linie", "Linien und Verbinder", 1, wo); break;
                    case "contentPart": l.Add("pptx.freihand", "Freihand-Zeichnungen", 1, wo); break;
                    default: l.Add("pptx.unbekannt", "Objekte, die Trendo nicht kennt", 1, $"{wo}: {(el.Name == Mc + "AlternateContent" ? "neuere Inhalte (z. B. Formel, 3D-Modell)" : el.Name.LocalName)}"); break;
                }
            }

            // Übergang und Animationen
            var trans = sld.Element(Pn + "transition");
            if (trans is null && sld.Elements(Mc + "AlternateContent").Any(a => a.Descendants(Pn + "transition").Any())) l.Add("pptx.uebergang", "Folienübergänge, die Trendo nicht oder nur ähnlich kennt", 1, wo);
            else if (trans?.Elements().FirstOrDefault()?.Name.LocalName is { } tn && tn is not ("fade" or "push" or "wipe")) l.Add("pptx.uebergang", "Folienübergänge, die Trendo nicht oder nur ähnlich kennt", 1, wo);
            if (trans is not null && (trans.Attribute("advTm") is not null || trans.Elements().Any(e => e.Name.LocalName == "sndAc"))) l.Add("pptx.uebergang.zeit", "Automatischer Folienwechsel und Übergangston", 1, wo);
            foreach (var ctn in sld.Element(Pn + "timing")?.Descendants(Pn + "cTn").Where(c => c.Attribute("presetClass") is not null) ?? [])
            {
                var cls = (string?)ctn.Attribute("presetClass"); var spid = (string?)ctn.Descendants(Pn + "spTgt").FirstOrDefault()?.Attribute("spid");
                if (cls != "entr" || spid is null || !handled.Contains(spid)) l.Add("pptx.animation", "Animationen, die Trendo nicht kennt (z. B. Hervorheben, Ausgang, Bewegungspfad)", 1, wo);
                else if ((string?)ctn.Attribute("presetID") is not ("10" or "2" or "23")) l.Add("pptx.animation.ersetzt", "Eingangs-Animationen, die Trendo durch „Einblenden“ ersetzt", 1, wo);
                if (cls == "entr" && (string?)ctn.Attribute("nodeType") is "withEffect" or "afterEffect") l.Add("pptx.animation.zeit", "Zeitablauf von Animationen (automatisch oder gleichzeitig statt auf Klick)", 1, wo);
            }
            if (rels.Values.Any(r => r.Type.EndsWith("/comments", StringComparison.Ordinal) || r.Type.EndsWith("/2018/10/relationships/comments", StringComparison.Ordinal))) l.Add("pptx.kommentar", "Kommentare", 1, wo);
        }

        Parts(z, l, "pptx", n => n.StartsWith("docProps/", StringComparison.Ordinal) || n.StartsWith("customXml/", StringComparison.Ordinal) || n.StartsWith("ppt/theme/", StringComparison.Ordinal) || n.StartsWith("ppt/slides/", StringComparison.Ordinal)
                || n.StartsWith("ppt/slideLayouts/", StringComparison.Ordinal) || n.StartsWith("ppt/slideMasters/", StringComparison.Ordinal) || n.StartsWith("ppt/notesSlides/", StringComparison.Ordinal) || n.StartsWith("ppt/notesMasters/", StringComparison.Ordinal)
                || n.StartsWith("ppt/handoutMasters/", StringComparison.Ordinal) || n.StartsWith("ppt/charts/", StringComparison.Ordinal) || n.StartsWith("ppt/media/", StringComparison.Ordinal) || n.StartsWith("ppt/tags/", StringComparison.Ordinal)
                || n.StartsWith("ppt/diagrams/", StringComparison.Ordinal) || n.StartsWith("ppt/changesInfos/", StringComparison.Ordinal)
                || n is "ppt/presentation.xml" or "ppt/presProps.xml" or "ppt/viewProps.xml" or "ppt/tableStyles.xml" or "ppt/revisionInfo.xml",
            ("ppt/vbaProject", "pptx.makro", "Makros"), ("ppt/embeddings/", "pptx.einbettung", "Eingebettete Dateien (z. B. die Tabelle hinter einem Diagramm)"), ("ppt/comments/", "", ""), ("ppt/commentAuthors", "", ""), ("ppt/authors", "", ""),
            ("ppt/fonts/", "pptx.schrift", "Eingebettete Schriften"), ("ppt/activeX/", "pptx.formular", "Schaltflächen und Formularelemente"), ("ppt/ink/", "pptx.freihand", "Freihand-Zeichnungen"));
    }

    /// <summary>Form oder Textfeld: Trendo hält je Objekt einen Text mit einer Schriftgröße, fett ja/nein und einer Farbe.</summary>
    static void PptxShape(XElement sp, string wo, Losses l)
    {
        var spPr = sp.Element(Pn + "spPr");
        var text = Short(string.Concat(sp.Element(Pn + "txBody")?.Descendants(An + "t").Select(t => t.Value + " ") ?? []));
        var at = wo + (text.Length > 0 ? $": „{text}“" : "");
        var geom = (string?)spPr?.Element(An + "prstGeom")?.Attribute("prst");
        if (spPr?.Element(An + "custGeom") is not null || geom is not (null or "rect" or "ellipse" or "rightArrow")) l.Add("pptx.form", "Formen, die Trendo nur als Rechteck oder Textfeld kennt (z. B. Stern, abgerundetes Rechteck, Pfeil nach links)", 1, at);
        var effects = spPr is not null && (spPr.Element(An + "gradFill") is not null || spPr.Element(An + "pattFill") is not null || spPr.Element(An + "blipFill") is not null || spPr.Element(An + "effectLst")?.HasElements == true
            || spPr.Element(An + "ln") is { } ln && ln.Elements().Any(e => e.Name.LocalName is "solidFill" or "gradFill" or "prstDash")
            || spPr.Element(An + "xfrm") is { } xf && (Truthy((string?)xf.Attribute("rot")) || Truthy((string?)xf.Attribute("flipH")) || Truthy((string?)xf.Attribute("flipV"))));
        if (effects) l.Add("pptx.effekt", "Formeffekte (Rahmenlinie, Verlauf, Schatten, Drehung)", 1, at);

        // Trendo schreibt Unterpunkte selbst in 85 % der Schriftgröße – das ist kein zweites Format
        var baseSize = (int?)(sp.Descendants(An + "rPr").FirstOrDefault() ?? sp.Descendants(An + "endParaRPr").FirstOrDefault())?.Attribute("sz");
        string Sig(XElement run)
        {
            var r = run.Element(An + "rPr");
            var size = (int?)r?.Attribute("sz");
            return string.Join('|', baseSize is int b && size is int s && (s == b || s == (int)Math.Round(b / 100.0 * 0.85 * 100)) ? "=" : size?.ToString(), Truthy((string?)r?.Attribute("b")), Truthy((string?)r?.Attribute("i")), Truthy((string?)r?.Attribute("u")), (string?)r?.Element(An + "solidFill")?.Elements().FirstOrDefault()?.Attribute("val"), (string?)r?.Element(An + "latin")?.Attribute("typeface"));
        }
        var paras = sp.Element(Pn + "txBody")?.Elements(An + "p").ToList() ?? [];
        var top = new HashSet<string>(); var mixed = false; var extra = false; var link = false; var numbered = false; var breaks = 0;
        foreach (var p in paras)
        {
            var runs = p.Elements().Where(e => e.Name == An + "r" || e.Name == An + "fld").Where(r => (r.Element(An + "t")?.Value ?? "").Length > 0).ToList();
            var sigs = runs.Select(Sig).Distinct().ToList();
            if (sigs.Count > 1) mixed = true;
            if (((int?)p.Element(An + "pPr")?.Attribute("lvl") ?? 0) == 0) foreach (var s in sigs) top.Add(s);
            foreach (var r in runs.Select(r => r.Element(An + "rPr")).OfType<XElement>())
            {
                if (Truthy((string?)r.Attribute("i")) || Truthy((string?)r.Attribute("u")) || (string?)r.Attribute("strike") is not (null or "noStrike") || Truthy((string?)r.Attribute("baseline"))) extra = true;
                if (r.Element(An + "hlinkClick") is not null) link = true;
            }
            if (p.Element(An + "pPr")?.Element(An + "buAutoNum") is not null) numbered = true;
            breaks += p.Elements(An + "br").Count();
        }
        if (mixed || top.Count > 1) l.Add("pptx.textformat", "Mehrere Textformate in einem Textfeld (Trendo behält eines für das ganze Feld)", 1, at);
        if (extra) l.Add("pptx.schriftschnitt", "Kursiv, unterstrichen, durchgestrichen, hoch- oder tiefgestellt", 1, at);
        if (link) l.Add("pptx.link", "Links im Text", 1, at);
        if (numbered) l.Add("pptx.nummer", "Nummerierte Listen (werden Aufzählungspunkte)", 1, at);
        l.Add("pptx.umbruch", "Zeilenumbrüche innerhalb eines Absatzes", breaks, at);
        if (sp.Element(Pn + "nvSpPr")?.Element(Pn + "nvPr")?.Element(Pn + "ph") is { } ph && (string?)ph.Attribute("type") is "dt" or "ftr" or "sldNum") l.Add("pptx.fusszeile", "Datum, Fußzeile und Foliennummer als Felder (es bleibt fester Text)", 1, wo);
    }

    // ======================= ODP =======================
    static void Odp(ZipArchive z, Losses l)
    {
        var content = X(z, "content.xml"); if (content?.Root is null) return;
        var styles = new Dictionary<string, XElement>();
        foreach (var s in content.Root.Element(OOffice + "automatic-styles")?.Elements(OStyle + "style") ?? []) styles.TryAdd((string?)s.Attribute(OStyle + "name") ?? "", s);
        XElement? Props(string? style, string kind) => style is not null && styles.TryGetValue(style, out var s) ? s.Element(OStyle + kind) : null;
        bool HasFont(XElement holder) => holder.Descendants().Where(e => e.Name == OText + "span" || e.Name == OText + "p").Any(e => Props((string?)e.Attribute(OText + "style-name"), "text-properties")?.Attributes().Any(a => a.Name.LocalName is "font-size" or "color" or "font-weight" or "font-style" or "text-underline-style" or "font-family" or "font-name") == true);
        var no = 0;
        l.Add("odp.design", "Design der Vorlage (Farben, Schriften, Hintergrund und Elemente der Folienvorlage – Trendo setzt sein Standarddesign)");
        foreach (var page in content.Descendants(ODraw + "page"))
        {
            var wo = "Folie " + ++no;
            var dp = Props((string?)page.Attribute(ODraw + "style-name"), "drawing-page-properties");
            if ((string?)dp?.Attribute(ODraw + "fill") is "bitmap" or "gradient" or "hatch") l.Add("odp.hintergrund", "Folienhintergrund mit Bild oder Verlauf", 1, wo);
            foreach (var el in page.Elements())
            {
                var n = el.Name.LocalName;
                if (el.Name == OPres + "notes") continue;
                if (el.Name == OAnim + "par" || el.Name == OPres + "animations")
                {
                    l.Add("odp.animation", "Animationen (Trendo liest aus ODP keine Animationen)", Math.Max(1, el.Descendants().Count(e => e.Attribute(OPres + "preset-id") is not null)), wo);
                    continue;
                }
                var text = Short(string.Concat(el.Descendants(OText + "p").Select(p => p.Value + " ")));
                var at = wo + (text.Length > 0 ? $": „{text}“" : "");
                if (el.Name == ODraw + "frame")
                {
                    if (el.Element(OTable + "table") is { } tbl)
                        l.Add("odp.tabelle.verbunden", "Verbundene Zellen in Tabellen", tbl.Descendants(OTable + "table-cell").Count(c => int.TryParse((string?)c.Attribute(OTable + "number-columns-spanned"), out var cs) && cs > 1 || int.TryParse((string?)c.Attribute(OTable + "number-rows-spanned"), out var rs) && rs > 1), wo);
                    else if (el.Element(ODraw + "object") is { } obj)
                    {
                        var dir = ((string?)obj.Attribute(OXLink + "href") ?? "").TrimStart('.', '/').TrimEnd('/');
                        var chart = X(z, dir + "/content.xml")?.Descendants(OChart + "chart").FirstOrDefault();
                        if (chart is null) l.Add("odp.objekt", "Eingebettete Objekte (z. B. Formel oder Tabelle)", 1, wo);
                        else l.Add("odp.diagramm", "Diagramm-Einzelheiten (Trendo behält Art, Titel und die erste Datenreihe)", 1, wo);
                    }
                    else if (el.Element(ODraw + "image") is { } img) { if ((string?)img.Attribute(OXLink + "href") is not { } href || z.GetEntry(href.TrimStart('.', '/')) is null) l.Add("odp.bild", "Bilder, die Trendo nicht übernimmt (verknüpft)", 1, wo); }
                    else if (el.Element(ODraw + "text-box") is { } box)
                    {
                        if (HasFont(box)) l.Add("odp.textformat", "Schriftgröße, Farbe und Schriftschnitt in Textfeldern (Trendo setzt Standardwerte)", 1, at);
                        if (box.Descendants(OText + "a").Any()) l.Add("odp.link", "Links im Text", 1, at);
                    }
                    else l.Add("odp.unbekannt", "Objekte, die Trendo nicht kennt", 1, $"{wo}: {el.Elements().FirstOrDefault()?.Name.LocalName ?? n}");
                }
                else if (el.Name.Namespace == ODraw && n is "custom-shape" or "rect" or "ellipse")
                {
                    var spans = el.Descendants(OText + "span").Select(s => (string?)s.Attribute(OText + "style-name")).Distinct().Count();
                    if (spans > 1) l.Add("odp.textformat.misch", "Mehrere Textformate in einer Form (Trendo behält das erste)", 1, at);
                    var type = ((string?)el.Descendants(ODraw + "enhanced-geometry").FirstOrDefault()?.Attribute(ODraw + "type") ?? "").ToLowerInvariant();
                    if (n == "custom-shape" && type.Length > 0 && !type.Contains("ellipse") && !type.Contains("arrow") && type is not ("rectangle" or "non-primitive")) l.Add("odp.form", "Formen, die Trendo nur als Rechteck oder Textfeld kennt", 1, at);
                }
                else if (el.Name.Namespace == ODraw && n == "g") l.Add("odp.gruppe", "Gruppierte Objekte (fehlen in Trendo ganz)", 1, at);
                else if (el.Name.Namespace == ODraw) l.Add("odp.linie", "Linien, Verbinder und freie Formen", 1, wo);
                else if (el.Name == OOffice + "forms") { if (el.HasElements) l.Add("odp.formular", "Formularfelder", 1, wo); }
                else l.Add("odp.unbekannt", "Objekte, die Trendo nicht kennt", 1, $"{wo}: {n}");
            }
        }
        Parts(z, l, "odp", n => n is "content.xml" or "styles.xml" or "meta.xml" or "settings.xml" or "mimetype" or "manifest.rdf" || n.StartsWith("META-INF/", StringComparison.Ordinal) || n.StartsWith("Thumbnails/", StringComparison.Ordinal)
                || n.StartsWith("Pictures/", StringComparison.Ordinal) || n.StartsWith("Configurations2/", StringComparison.Ordinal) || n.StartsWith("Object", StringComparison.Ordinal),
            ("Basic/", "odp.makro", "Makros"), ("Scripts/", "odp.makro", "Makros"), ("Dialogs/", "", ""), ("Media/", "odp.medien", "Videos und Ton"));
    }
}
